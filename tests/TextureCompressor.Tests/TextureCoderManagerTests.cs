using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureCoderManagerTests
{
    [Fact]
    public void GlobalManagerFindsSequentialUncompressedCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UNorm);

        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedUNormCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565UNorm);

        Assert.IsType<PackedUNormTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedFloatCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.R11G11B10Float);

        Assert.IsType<PackedFloatTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedIntegerCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UInt);

        Assert.IsType<PackedIntegerTextureCoder>(coder);
    }

    [Fact]
    public void SequentialUncompressedCoderDoesNotClaimPackedUNormFormats()
    {
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgb565UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgba4UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgb5A1UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgb10A2UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Bgra4UNorm));
    }

    [Fact]
    public void GlobalManagerDoesNotClaimCompressedFormats()
    {
        var found = TextureCoderManager.Global.TryGetCoder(TextureFormats.Bc1, out var coder);

        Assert.False(found);
        Assert.Null(coder);
    }

    [Fact]
    public void EncodeAndDecodeBgra8SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([3, 2, 1, 4], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgb565UsesPackedUNormCoder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0),
                new Rgba8UNorm(0, 255, 0)
            ]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0xf8, 0xe0, 0x07], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(0, decoded.Pixels[1].Red);
        Assert.Equal(255, decoded.Pixels[1].Green);
        Assert.Equal(0, decoded.Pixels[1].Blue);
    }

    [Fact]
    public void EncodeAndDecodePackedUNormHonorsRowPitch()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(255, 0, 0),
                new Rgba8UNorm(0, 255, 0),
                new Rgba8UNorm(0, 0, 255),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width) + 2;
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        Array.Fill<byte>(encoded, 0x7e);
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0xf8, 0xe0, 0x07, 0x7e, 0x7e, 0x1f, 0x00, 0xff, 0xff, 0x7e, 0x7e], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void EncodeAndDecodeRgba4UsesPackedUNormCoder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 170, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xaf, 0xf0], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(170, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb5A1UsesPackedUNormCoder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(0, 255, 0, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb5A1UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xc1, 0x07], encoded);
        Assert.Equal(0, decoded.Pixels[0].Red);
        Assert.Equal(255, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgra4SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 170, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0xa0], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(170, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2UsesRgba16Carrier()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, ushort.MaxValue, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x0f, 0xc0, 0xff], encoded);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Blue);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeR11G11B10FloatUsesPackedFloatCoder()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f)]);

        var coder = Assert.IsType<PackedFloatTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R11G11B10Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xc0, 0x03, 0x20, 0x88], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(2f, decoded.Pixels[0].Green);
        Assert.Equal(4f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb9E5UsesSharedExponent()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 1f, 1f)]);

        var coder = Assert.IsType<PackedFloatTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb9E5));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x01, 0x02, 0x84], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(1f, decoded.Pixels[0].Green);
        Assert.Equal(1f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2UIntUsesPackedIntegerCoder()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1023, 0, 512, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x03, 0x08, 0xc0, 0xff], encoded);
        Assert.Equal((ushort)1023, decoded.Pixels[0].Red);
        Assert.Equal((ushort)0, decoded.Pixels[0].Green);
        Assert.Equal((ushort)512, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2RevUIntReversesBitOrder()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1, 2, 3, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2RevUInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x01, 0x08, 0x30, 0xc0], encoded);
        Assert.Equal((ushort)1, decoded.Pixels[0].Red);
        Assert.Equal((ushort)2, decoded.Pixels[0].Green);
        Assert.Equal((ushort)3, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgr10A2RevUIntSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1023, 1, 512, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevUInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x06, 0xf0, 0xff], encoded);
        Assert.Equal((ushort)1023, decoded.Pixels[0].Red);
        Assert.Equal((ushort)1, decoded.Pixels[0].Green);
        Assert.Equal((ushort)512, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }
}
