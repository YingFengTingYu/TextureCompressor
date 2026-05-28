using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class AtcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(AtcFormats))]
    public void GlobalManagerFindsAtcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(AtcTextureCoder.IsSupported(format));
        Assert.IsType<AtcTextureCoder>(coder);
    }

    [Fact]
    public void AtcRgbDecodesModeZeroColorBlockToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0x7c00, 0xf800, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), pixel));
    }

    [Fact]
    public void AtcRgbDecodesModeZeroInterpolatedColorsToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0x7c00, 0x001f, (1u << 2) | (2u << 4) | (3u << 6));
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(159, 0, 95, 255), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(95, 0, 159, 255), decoded.Pixels[2]);
    }

    [Fact]
    public void AtcRgbDecodesModeOneColorBlockToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0xfc00, 0x07e0, (2u << 2) | (3u << 4));
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[2]);
    }

    [Fact]
    public void AtcRgbaExplicitAlphaDecodesExplicitAlphaAndColorToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgbaExplicitAlpha.GetByteCount(4, 4)];
        encoded[0] = 0x0f;
        WriteColorBlock(encoded.AsSpan(8), 0x7c00, 0xf800, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaExplicitAlpha);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 0, 0, 0), decoded.Pixels[1]);
    }

    [Fact]
    public void AtcRgbaInterpolatedAlphaDecodesInterpolatedAlphaAndColorToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgbaInterpolatedAlpha.GetByteCount(4, 4)];
        encoded[0] = 255;
        encoded[1] = 0;
        encoded[2] = 0x01;
        WriteColorBlock(encoded.AsSpan(8), 0x03e0, 0x07e0, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void AtcRgbaInterpolatedAlphaDecodesRoundedAlphaRampToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgbaInterpolatedAlpha.GetByteCount(4, 4)];
        encoded[0] = 255;
        encoded[1] = 0;
        encoded[2] = 0x02;
        WriteColorBlock(encoded.AsSpan(8), 0x03e0, 0x07e0, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 255, 0, 219), decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeAtcRgbIgnoresSourceAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 20), 16).ToArray());
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 34);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeAtcRgbaExplicitAlphaRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaExplicitAlpha);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 34);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(136, pixel.Alpha);
        });
    }

    [Fact]
    public void AtcRgbaExplicitAlphaEncodeQuantizesToNearestReplicatedAlpha()
    {
        var sourcePixels = Enumerable.Repeat(new Rgba8UNorm(0, 0, 0, 0), 16).ToArray();
        sourcePixels[0] = new Rgba8UNorm(0, 0, 0, 9);
        var source = new ArrayTextureBitmap<Rgba8UNorm>(4, 4, sourcePixels);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaExplicitAlpha);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(0x01, encoded[0] & 0x0f);
    }

    [Fact]
    public void EncodeAndDecodeAtcRgbaInterpolatedAlphaRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 34);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(128, pixel.Alpha);
        });
    }

    [Fact]
    public void AtcDecodeUsesPaddedBlockWidthForNonMultipleOfFourWidth()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(5, 1)];
        WriteColorBlock(encoded, 0x7c00, 0xf800, 0);
        WriteColorBlock(encoded.AsSpan(8), 0x03e0, 0x07e0, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(5, 1);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[4]);
    }

    [Fact]
    public void EncodeAndDecodeHonorsBlockRowPitch()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 25).ToArray());
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 4;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(40, encoded.Length);
        Assert.All(encoded[16..20], value => Assert.Equal(0xcc, value));
        Assert.All(encoded[36..40], value => Assert.Equal(0xcc, value));

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(5, 5);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal(255, pixel.Red);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void AtcByteCountsUseFourByFourBlockRows()
    {
        var rgb = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rgba = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);

        Assert.Equal(16, rgb.GetDefaultPitch(5));
        Assert.Equal(32, rgb.GetEncodedByteCount(5, 5, rgb.GetDefaultPitch(5)));
        Assert.Equal(32, rgba.GetDefaultPitch(5));
        Assert.Equal(64, rgba.GetEncodedByteCount(5, 5, rgba.GetDefaultPitch(5)));
        Assert.Equal(80, rgba.GetEncodedByteCount(5, 5, 40));
    }

    private static void WriteColorBlock(Span<byte> destination, ushort color0, ushort color1, uint indices)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], indices);
    }

    public static TheoryData<TextureFormat> AtcFormats() => new()
    {
        TextureFormats.AtcRgb,
        TextureFormats.AtcRgbaExplicitAlpha,
        TextureFormats.AtcRgbaInterpolatedAlpha
    };
}
