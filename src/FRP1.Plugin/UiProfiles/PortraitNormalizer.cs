using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiProfiles;

public readonly record struct PortraitCrop(float CenterX = .5f, float CenterY = .5f, float Zoom = 1f)
{
    public PortraitCrop() : this(.5f, .5f, 1f) { }
}

[SupportedOSPlatform("windows")]
public static class PortraitNormalizer
{
    public const int MaximumInputBytes = 8 * 1024 * 1024;
    public const int MaximumInputDimension = 8192;
    public const long MaximumInputPixels = 16 * 1024 * 1024;

    public static async Task<PortraitThumbnail> FromFileAsync(string path, PortraitCrop crop, CancellationToken token = default)
    {
        var absolute = Path.GetFullPath(path);
        if (new Uri(absolute).IsUnc || new DriveInfo(Path.GetPathRoot(absolute)!).DriveType == DriveType.Network)
            throw new InvalidDataException("Only explicitly selected local image files are supported.");
        await using var file = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (file.Length is <= 0 or > MaximumInputBytes) throw new InvalidDataException("Image file exceeds the size limit.");
        var bytes = new byte[(int)file.Length];
        await file.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return await Task.Run(() => Normalize(bytes, crop, token), token).ConfigureAwait(false);
    }

    public static PortraitThumbnail Normalize(ReadOnlySpan<byte> source, PortraitCrop crop, CancellationToken token = default)
    {
        ValidateCrop(crop);
        if (source.Length is <= 0 or > MaximumInputBytes) throw new InvalidDataException("Image file exceeds the size limit.");
        var (width, height) = ReadDimensions(source);
        if (width is <= 0 or > MaximumInputDimension || height is <= 0 or > MaximumInputDimension ||
            (long)width * height > MaximumInputPixels)
            throw new InvalidDataException("Image dimensions exceed the limit.");
        token.ThrowIfCancellationRequested();
        using var memory = new MemoryStream(source.ToArray(), writable: false);
        using var image = Image.FromStream(memory, useEmbeddedColorManagement: false, validateImageData: true);
        if (image.Width != width || image.Height != height) throw new InvalidDataException("Inconsistent image dimensions.");
        var side = Math.Min(width, height) / crop.Zoom;
        var x = Math.Clamp(crop.CenterX * width - side / 2, 0, width - side);
        var y = Math.Clamp(crop.CenterY * height - side / 2, 0, height - side);
        foreach (var size in new[] { 256, 192, 128, 96, 64, 48, 32 })
        {
            token.ThrowIfCancellationRequested();
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(image, new Rectangle(0, 0, size, size), x, y, side, side, GraphicsUnit.Pixel);
            }
            using var encoded = new MemoryStream();
            bitmap.Save(encoded, ImageFormat.Png);
            var png = StripMetadata(encoded.ToArray());
            if (png.Length > PortraitValidator.MaximumBytes) continue;
            var result = new PortraitThumbnail("image/png", png);
            PortraitValidator.Validate(result);
            return result;
        }
        throw new InvalidDataException("Image cannot fit the portrait budget.");
    }

    public static void ValidateCrop(PortraitCrop crop)
    {
        if (!float.IsFinite(crop.CenterX) || !float.IsFinite(crop.CenterY) || !float.IsFinite(crop.Zoom) ||
            crop.CenterX is < 0 or > 1 || crop.CenterY is < 0 or > 1 || crop.Zoom is < 1 or > 4)
            throw new InvalidDataException("Invalid portrait crop.");
    }

    private static (int Width, int Height) ReadDimensions(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length >= 24 && bytes[..8].SequenceEqual(png) && bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            var width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);
            var height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
            if (width > int.MaxValue || height > int.MaxValue) throw new InvalidDataException("Invalid image dimensions.");
            return ((int)width, (int)height);
        }
        if (bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216)
            throw new InvalidDataException("Only PNG and JPEG images are supported.");
        var offset = 2;
        for (var markerCount = 0; markerCount < 256 && offset < bytes.Length; markerCount++)
        {
            if (bytes[offset++] != 255) throw new InvalidDataException("Invalid JPEG header.");
            while (offset < bytes.Length && bytes[offset] == 255) offset++;
            if (offset >= bytes.Length) break;
            var marker = bytes[offset++];
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) continue;
            if (marker is 0xDA or 0xD9 || offset + 2 > bytes.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
            if (length < 2 || length > bytes.Length - offset) break;
            if (marker is 0xC0 or 0xC2)
            {
                if (length < 8 || bytes[offset + 2] != 8) break;
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..]));
            }
            offset += length;
        }
        throw new InvalidDataException("Unsupported or incomplete JPEG header.");
    }

    private static byte[] StripMetadata(ReadOnlySpan<byte> png)
    {
        using var result = new MemoryStream();
        result.Write(png[..8]);
        var offset = 8;
        while (offset < png.Length)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("Invalid generated PNG.");
            var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(png[offset..]);
            if (lengthValue > png.Length - offset - 12) throw new InvalidDataException("Invalid generated PNG.");
            var length = (int)lengthValue;
            var kind = png.Slice(offset + 4, 4);
            if (kind.SequenceEqual("IHDR"u8) || kind.SequenceEqual("IDAT"u8) || kind.SequenceEqual("IEND"u8))
                result.Write(png.Slice(offset, length + 12));
            offset += length + 12;
        }
        return result.ToArray();
    }
}
