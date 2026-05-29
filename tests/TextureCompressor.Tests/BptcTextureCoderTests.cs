using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class BptcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(BptcFormats))]
    public void GlobalManagerFindsBptcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(BptcTextureCoder.IsSupported(format));
        Assert.IsType<BptcTextureCoder>(coder);
    }

    [Fact]
    public void Bc7InvalidModeDecodesToTransparentRgba8()
    {
        var encoded = new byte[TextureFormats.Bc7UNorm.GetByteCount(4, 4)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), pixel));
    }

    [Fact]
    public void EncodeAndDecodeBc7UNormRoundTripsSolidRgba8()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(34, 101, 202, 77), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(34, pixel.Red, 1);
            AssertClose(101, pixel.Green, 1);
            AssertClose(202, pixel.Blue, 1);
            AssertClose(77, pixel.Alpha, 1);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc7SrgbRoundTripsLinearRgba8ThroughStorageGamma()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(128, 32, 224, 200), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7Srgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(128, pixel.Red, 2);
            AssertClose(32, pixel.Green, 2);
            AssertClose(224, pixel.Blue, 2);
            Assert.Equal(200, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc6HUFloatRoundTripsHdrRgbaFloat()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(2f, 0.5f, 8f, 0.25f), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc6HUFloat);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(2f, pixel.Red, 0.02f);
            AssertClose(0.5f, pixel.Green, 0.01f);
            AssertClose(8f, pixel.Blue, 0.08f);
            Assert.Equal(1f, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc6HSFloatRoundTripsNegativeRgbaFloat()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(-2f, 0.5f, 3f, 1f), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc6HSFloat);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(-2f, pixel.Red, 0.03f);
            AssertClose(0.5f, pixel.Green, 0.01f);
            AssertClose(3f, pixel.Blue, 0.04f);
            Assert.Equal(1f, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc7HonorsBlockRowPitch()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(12, 34, 56, 78), 25).ToArray());
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 4;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(72, encoded.Length);
        Assert.All(encoded[32..36], value => Assert.Equal(0xcc, value));
        Assert.All(encoded[68..72], value => Assert.Equal(0xcc, value));

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(12, 34, 56, 78), pixel));
    }

    [Theory]
    [InlineData(1, 16)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void BptcRowByteCountUsesFourByFourBlocks(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc6HUFloat.GetRowByteCount(width));
        Assert.Equal(expected, TextureFormats.Bc7UNorm.GetRowByteCount(width));
    }

    private static void AssertClose(float expected, float actual, float tolerance) =>
        Assert.True(MathF.Abs(expected - actual) <= tolerance, $"Expected {actual} to be within {tolerance} of {expected}.");

    private static void AssertClose(int expected, int actual, int tolerance) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);

    public static TheoryData<TextureFormat> BptcFormats() => new()
    {
        TextureFormats.Bc6HUFloat,
        TextureFormats.Bc6HSFloat,
        TextureFormats.Bc7UNorm,
        TextureFormats.Bc7Srgb,
        TextureFormats.RgbBptcUFloat,
        TextureFormats.RgbBptcSFloat,
        TextureFormats.RgbaBptcUNorm,
        TextureFormats.RgbaBptcSrgb
    };
}
