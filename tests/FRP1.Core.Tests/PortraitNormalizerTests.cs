using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FRP1.Core.Profiles;
using FRP1.Plugin.UiProfiles;
using Xunit;

namespace FRP1.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class PortraitNormalizerTests
{
    [Fact]
    public void NormalizationProducesFreshBoundedPngAndRemovesInputMetadata()
    {
        var input = PngFixture.Create(4, 3, metadata: true);
        var output = PortraitNormalizer.Normalize(input, new());
        PortraitValidator.Validate(output);
        Assert.Equal("image/png", output.MediaType);
        Assert.InRange(output.Data.Length, 57, PortraitValidator.MaximumBytes);
        Assert.Equal(new[] { "IHDR", "IDAT", "IEND" }, ChunkTypes(output.Data).Distinct());
        Assert.DoesNotContain("Forbidden metadata", System.Text.Encoding.ASCII.GetString(output.Data));
        Assert.NotEqual(input, output.Data);
    }

    [Fact]
    public void ExplicitCropSelectsDifferentImageRegions()
    {
        using var bitmap = new Bitmap(32, 16, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Red);
            graphics.FillRectangle(Brushes.Blue, 16, 0, 16, 16);
        }
        using var encoded = new MemoryStream();
        bitmap.Save(encoded, ImageFormat.Png);
        var left = PortraitNormalizer.Normalize(encoded.ToArray(), new(0, .5f, 1));
        var right = PortraitNormalizer.Normalize(encoded.ToArray(), new(1, .5f, 1));
        using var leftData = new MemoryStream(left.Data);
        using var rightData = new MemoryStream(right.Data);
        using var leftImage = new Bitmap(leftData);
        using var rightImage = new Bitmap(rightData);
        var leftPixel = leftImage.GetPixel(leftImage.Width / 2, leftImage.Height / 2);
        var rightPixel = rightImage.GetPixel(rightImage.Width / 2, rightImage.Height / 2);
        Assert.True(leftPixel.R > leftPixel.B);
        Assert.True(rightPixel.B > rightPixel.R);
    }

    [Theory]
    [InlineData(float.NaN, .5f, 1)]
    [InlineData(.5f, float.PositiveInfinity, 1)]
    [InlineData(-.1f, .5f, 1)]
    [InlineData(.5f, 1.1f, 1)]
    [InlineData(.5f, .5f, .9f)]
    [InlineData(.5f, .5f, 4.1f)]
    public void InvalidCropIsRejectedBeforeDecoding(float x, float y, float zoom) =>
        Assert.Throws<InvalidDataException>(() => PortraitNormalizer.Normalize(PngFixture.Create(), new(x, y, zoom)));

    [Fact]
    public void InputBudgetUnsupportedFormatsAndCancellationAreChecked()
    {
        Assert.Throws<InvalidDataException>(() => PortraitNormalizer.Normalize([], new()));
        Assert.Throws<InvalidDataException>(() => PortraitNormalizer.Normalize("not an image"u8, new()));
        Assert.Throws<InvalidDataException>(() => PortraitNormalizer.Normalize(new byte[PortraitNormalizer.MaximumInputBytes + 1], new()));
        var largeDimensions = PngFixture.Create();
        BinaryPrimitives.WriteInt32BigEndian(largeDimensions.AsSpan(16), PortraitNormalizer.MaximumInputDimension + 1);
        Assert.Throws<InvalidDataException>(() => PortraitNormalizer.Normalize(largeDimensions, new()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PortraitNormalizer.Normalize(PngFixture.Create(), new(), cancellation.Token));
    }

    [Fact]
    public async Task ExplicitLocalFileNormalizationDoesNotModifySourceImage()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "synthetic-input.png");
        var input = PngFixture.Create(2, 2, metadata: true);
        await File.WriteAllBytesAsync(path, input);
        var result = await PortraitNormalizer.FromFileAsync(path, new());
        PortraitValidator.Validate(result);
        Assert.Equal(input, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    private static IReadOnlyList<string> ChunkTypes(byte[] data)
    {
        var types = new List<string>();
        for (var offset = 8; offset < data.Length;)
        {
            var size = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            types.Add(System.Text.Encoding.ASCII.GetString(data, offset + 4, 4));
            offset = checked(offset + size + 12);
        }
        return types;
    }
}
