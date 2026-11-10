using System.Buffers.Binary;
using System.IO.Compression;

namespace FRP1.Core.Profiles;

public static class PortraitValidator
{
    public const int MaximumBytes = 12 * 1024;
    public const int MaximumDimension = 256;

    // A deliberately small normalized PNG subset, not a general image decoder.
    // Ancillary chunks (EXIF, XMP and text included) are never accepted.
    public static void Validate(PortraitThumbnail portrait)
    {
        ArgumentNullException.ThrowIfNull(portrait);
        if (portrait.MediaType != "image/png" || portrait.Data is null ||
            portrait.Data.Length is < 57 or > MaximumBytes)
            throw new InvalidDataException("Unsupported portrait thumbnail.");
        var data = portrait.Data.AsSpan();
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!data[..8].SequenceEqual(signature)) throw new InvalidDataException("Invalid portrait header.");
        var offset = 8;
        var width = 0;
        var height = 0;
        var channels = 0;
        var sawData = false;
        var sawEnd = false;
        using var compressed = new MemoryStream();
        while (offset < data.Length)
        {
            if (data.Length - offset < 12) throw new InvalidDataException("Invalid portrait chunk.");
            var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (lengthValue > data.Length - offset - 12) throw new InvalidDataException("Invalid portrait chunk.");
            var length = (int)lengthValue;
            var kind = data.Slice(offset + 4, 4);
            var payload = data.Slice(offset + 8, length);
            if (Checksum(data.Slice(offset + 4, length + 4)) !=
                BinaryPrimitives.ReadUInt32BigEndian(data[(offset + length + 8)..]))
                throw new InvalidDataException("Invalid portrait checksum.");
            if (kind.SequenceEqual("IHDR"u8))
            {
                if (offset != 8 || length != 13) throw new InvalidDataException("Invalid portrait header.");
                var w = BinaryPrimitives.ReadUInt32BigEndian(payload);
                var h = BinaryPrimitives.ReadUInt32BigEndian(payload[4..]);
                if (w is 0 or > MaximumDimension || h is 0 or > MaximumDimension ||
                    payload[8] != 8 || payload[9] is not (2 or 6) ||
                    payload[10] != 0 || payload[11] != 0 || payload[12] != 0)
                    throw new InvalidDataException("Unsupported portrait dimensions or pixels.");
                width = (int)w;
                height = (int)h;
                channels = payload[9] == 2 ? 3 : 4;
            }
            else if (kind.SequenceEqual("IDAT"u8))
            {
                if (width == 0 || sawEnd) throw new InvalidDataException("Invalid portrait data.");
                compressed.Write(payload);
                sawData = true;
            }
            else if (kind.SequenceEqual("IEND"u8))
            {
                if (!sawData || length != 0 || offset + 12 != data.Length)
                    throw new InvalidDataException("Invalid portrait end.");
                sawEnd = true;
            }
            else throw new InvalidDataException("Portrait metadata and unknown chunks are not permitted.");
            offset += length + 12;
        }
        if (!sawEnd) throw new InvalidDataException("Incomplete portrait.");
        compressed.Position = 0;
        try
        {
            using var pixels = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[checked(width * channels + 1)];
            for (var y = 0; y < height; y++)
            {
                pixels.ReadExactly(row);
                if (row[0] > 4) throw new InvalidDataException("Invalid portrait filter.");
            }
            if (pixels.ReadByte() != -1) throw new InvalidDataException("Portrait pixel budget exceeded.");
        }
        catch (IOException ex) { throw new InvalidDataException("Invalid portrait pixel data.", ex); }
    }

    private static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
        }
        return ~crc;
    }
}
