using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using FRP1.Core.Profiles;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class PortraitTests
{
    [Theory]
    [InlineData(1, 1, 2)] [InlineData(1, 1, 6)] [InlineData(256, 256, 6)]
    public void NormalizedRgbAndRgbaPngThumbnailsAreAccepted(int width, int height, byte colorType) =>
        PortraitValidator.Validate(new("image/png", PngFixture.Create(width, height, colorType)));

    [Theory]
    [InlineData(0, 1, 6)] [InlineData(257, 1, 6)] [InlineData(1, 257, 6)] [InlineData(1, 1, 3)]
    public void UnsupportedDimensionsAndColorTypesAreRejected(int width, int height, byte colorType) =>
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", PngFixture.Create(width, height, colorType))));

    [Fact]
    public void MetadataChunksInvalidCrcAndTruncatedFilesAreRejected()
    {
        var bytes = PngFixture.Create();
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", PngFixture.Create(metadata: true))));
        var corrupt = (byte[])bytes.Clone();
        corrupt[29] ^= 1;
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", corrupt)));
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", bytes[..^3])));
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/jpeg", bytes)));
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", new byte[PortraitValidator.MaximumBytes + 1])));
    }

    [Fact]
    public void DecompressedPixelBudgetFiltersAndMissingPixelsAreChecked()
    {
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", PngFixture.Create(rawPixels: new byte[1024 * 1024]))));
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", PngFixture.Create(rawPixels: [5, 1, 2, 3, 255]))));
        Assert.Throws<InvalidDataException>(() => PortraitValidator.Validate(new("image/png", PngFixture.Create(rawPixels: [0, 1]))));
    }
}

internal static class PngFixture
{
    public static byte[] Create(int width = 1, int height = 1, byte colorType = 6, bool metadata = false, byte[]? rawPixels = null)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = colorType;
        Chunk(output, "IHDR", header);
        if (metadata) Chunk(output, "tEXt", Encoding.ASCII.GetBytes("Comment\0Forbidden metadata"));
        using var encoded = new MemoryStream();
        using (var compressor = new ZLibStream(encoded, CompressionLevel.SmallestSize, leaveOpen: true))
            compressor.Write(rawPixels ?? new byte[Math.Max(1, (Math.Max(0, width) * (colorType == 2 ? 3 : 4) + 1) * Math.Max(0, height))]);
        Chunk(output, "IDAT", encoded.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] payload)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        output.Write(length);
        output.Write(typeBytes);
        output.Write(payload);
        var checksumInput = typeBytes.Concat(payload).ToArray();
        uint crc = 0xffffffff;
        foreach (var value in checksumInput)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
        }
        BinaryPrimitives.WriteUInt32BigEndian(length, ~crc);
        output.Write(length);
    }
}
