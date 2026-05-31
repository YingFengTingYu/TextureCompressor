using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class RgbmTextureCoderTests
{
    [Theory]
    [MemberData(nameof(RgbmFormats))]
    public void GlobalManagerFindsRgbmTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(RgbmTextureCoder.IsSupported(format));
        Assert.IsType<RgbmTextureCoder>(coder);
    }

    [Fact]
    public void DecodeRgbmAppliesAlphaMultiplierAndMaxRange()
    {
        var encoded = new byte[] { 128, 64, 255, 128 };
        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);

        var coder = new RgbmTextureCoder(TextureFormats.Rgbm, maxRange: 8f);
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(128 / 255f * 128 / 255f * 8f, decoded.Pixels[0].Red, precision: 6);
        Assert.Equal(64 / 255f * 128 / 255f * 8f, decoded.Pixels[0].Green, precision: 6);
        Assert.Equal(255 / 255f * 128 / 255f * 8f, decoded.Pixels[0].Blue, precision: 6);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void DecodeRgbdAppliesInverseAlphaDivisorAndMaxRange()
    {
        var encoded = new byte[] { 255, 128, 64, 2 };
        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);

        var coder = new RgbmTextureCoder(TextureFormats.Rgbd, maxRange: 8f);
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(4f, decoded.Pixels[0].Red, precision: 6);
        Assert.Equal(128 / 255f * 4f, decoded.Pixels[0].Green, precision: 6);
        Assert.Equal(64 / 255f * 4f, decoded.Pixels[0].Blue, precision: 6);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Theory]
    [MemberData(nameof(RgbmFormats))]
    public void EncodeAndDecodeRoundTripsHdrRgbWithinQuantization(TextureFormat format)
    {
        var source = new ArrayBitmap<Rgba32Float>(
            2,
            1,
            [
                new Rgba32Float(3.25f, 1.5f, 0.75f),
                new Rgba32Float(0f, 0f, 0f)
            ]);

        var coder = new RgbmTextureCoder(format, maxRange: 8f);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.InRange(decoded.Pixels[0].Red, source.Pixels[0].Red - 0.04f, source.Pixels[0].Red + 0.04f);
        Assert.InRange(decoded.Pixels[0].Green, source.Pixels[0].Green - 0.04f, source.Pixels[0].Green + 0.04f);
        Assert.InRange(decoded.Pixels[0].Blue, source.Pixels[0].Blue - 0.04f, source.Pixels[0].Blue + 0.04f);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
        Assert.Equal(0f, decoded.Pixels[1].Red);
        Assert.Equal(0f, decoded.Pixels[1].Green);
        Assert.Equal(0f, decoded.Pixels[1].Blue);
        Assert.Equal(1f, decoded.Pixels[1].Alpha);
    }

    [Fact]
    public void EncodeRgbmBlackWritesZeroMultiplier()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0f, 0f, 0f)]);

        var coder = new RgbmTextureCoder(TextureFormats.Rgbm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal([0, 0, 0, 0], encoded);
    }

    [Fact]
    public void EncodeRgbdBlackWritesMaxDivisor()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0f, 0f, 0f)]);

        var coder = new RgbmTextureCoder(TextureFormats.Rgbd);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal([0, 0, 0, 255], encoded);
    }

    [Fact]
    public void EncodeAndDecodeHonorsRowPitch()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            2,
            2,
            [
                new Rgba32Float(1f, 0f, 0f),
                new Rgba32Float(0f, 1f, 0f),
                new Rgba32Float(0f, 0f, 1f),
                new Rgba32Float(2f, 3f, 4f)
            ]);

        var coder = new RgbmTextureCoder(TextureFormats.Rgbm, maxRange: 8f);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 2;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(0xcc, encoded[8]);
        Assert.Equal(0xcc, encoded[9]);
        Assert.InRange(decoded.Pixels[3].Red, 1.99f, 2.01f);
        Assert.InRange(decoded.Pixels[3].Green, 2.99f, 3.01f);
        Assert.InRange(decoded.Pixels[3].Blue, 3.99f, 4.01f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ConstructorRejectsInvalidMaxRange(float maxRange)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new RgbmTextureCoder(TextureFormats.Rgbm, maxRange));

        Assert.Equal("maxRange", exception.ParamName);
    }

    public static TheoryData<TextureFormat> RgbmFormats() => new()
    {
        TextureFormats.Rgbm,
        TextureFormats.Rgbd
    };
}
