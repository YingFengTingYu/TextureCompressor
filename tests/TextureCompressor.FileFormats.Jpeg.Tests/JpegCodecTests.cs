using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Jpeg.Tests;

public sealed class JpegCodecTests
{
    [Fact]
    public void EncodeWritesJfifBaselineJpeg()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0, 255),
                new Rgba8UNorm(0, 255, 0, 255)
            ]);

        var jpeg = JpegCodec.Encode(source, new JpegEncodingOptions { Quality = 90 });

        Assert.Equal([0xff, 0xd8], jpeg.AsSpan(0, 2).ToArray());
        Assert.True(IndexOf(jpeg, "JFIF"u8) >= 0);
        Assert.True(IndexOf(jpeg, [0xff, 0xc0]) >= 0);
        Assert.Equal([0xff, 0xd9], jpeg.AsSpan(jpeg.Length - 2, 2).ToArray());
    }

    [Fact]
    public void EncodeThenDecodePreservesDimensionsAndApproximatePixels()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            8,
            8,
            Enumerable.Range(0, 64)
                .Select(i => new Rgba8UNorm((byte)(i * 3), (byte)(255 - (i * 2)), (byte)(40 + i), 255))
                .ToArray());

        var jpeg = JpegCodec.Encode(source, new JpegEncodingOptions { Quality = 100 });
        var decoded = JpegCodec.Decode(jpeg);

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        AssertPixelNear(source.PixelSpan[0], decoded.PixelSpan[0], tolerance: 3);
        AssertPixelNear(source.PixelSpan[63], decoded.PixelSpan[63], tolerance: 3);
    }

    [Fact]
    public void DecodeRejectsNonJpegData()
    {
        Assert.Throws<InvalidDataException>(() => JpegCodec.Decode([1, 2, 3, 4]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void EncodeRejectsInvalidQuality(int quality)
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(1, 2, 3, 255)]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JpegCodec.Encode(source, new JpegEncodingOptions { Quality = quality }));
    }

    private static void AssertPixelNear(Rgba8UNorm expected, Rgba8UNorm actual, int tolerance)
    {
        Assert.InRange(actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
        Assert.InRange(actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
        Assert.InRange(actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
        Assert.Equal(byte.MaxValue, actual.Alpha);
    }

    private static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        for (var i = 0; i <= source.Length - value.Length; i++)
        {
            if (source.Slice(i, value.Length).SequenceEqual(value))
            {
                return i;
            }
        }

        return -1;
    }
}
