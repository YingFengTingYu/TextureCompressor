using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class PackedRgb422TextureCoderTests
{
    [Theory]
    [MemberData(nameof(PackedRgb422Formats))]
    public void GlobalManagerFindsPackedRgb422TextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PackedRgb422TextureCoder.IsSupported(format));
        Assert.IsType<PackedRgb422TextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(PackedRgb4228BitLayouts))]
    public void EncodeAndDecode8BitLayoutsAverageRedAndBlue(TextureFormat format, byte[] expected)
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(10, 22, 40),
                new Rgba8UNorm(12, 66, 50)
            ]);

        var coder = new PackedRgb422TextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(new Rgba8UNorm(11, 22, 45), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(11, 66, 45), decoded.Pixels[1]);
    }

    [Theory]
    [MemberData(nameof(PackedRgb42216BitLayouts))]
    public void EncodeAndDecode16BitLayoutsAverageRedAndBlue(TextureFormat format, byte[] expected)
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            2,
            1,
            [
                new Rgba16UNorm(1000, 2222, 3000),
                new Rgba16UNorm(3000, 4444, 7000)
            ]);

        var coder = new PackedRgb422TextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(new Rgba16UNorm(2000, 2222, 5000), decoded.Pixels[0]);
        Assert.Equal(new Rgba16UNorm(2000, 4444, 5000), decoded.Pixels[1]);
    }

    [Theory]
    [MemberData(nameof(PackedRgb422PaddedWordLayouts))]
    public void EncodeAndDecodePaddedWordLayoutsUseTopBits(TextureFormat format, byte[] expected)
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            2,
            1,
            [
                new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, 0),
                new Rgba16UNorm(ushort.MaxValue, 0, 0)
            ]);

        var coder = new PackedRgb422TextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodeHonorsRowPitch()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(10, 22, 40),
                new Rgba8UNorm(12, 66, 50),
                new Rgba8UNorm(100, 120, 140),
                new Rgba8UNorm(110, 160, 180)
            ]);

        var coder = new PackedRgb422TextureCoder(TextureFormats.R8G8B8G8_422UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 2;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(0xcc, encoded[4]);
        Assert.Equal(0xcc, encoded[5]);
        Assert.Equal(new Rgba8UNorm(105, 120, 160), decoded.Pixels[2]);
        Assert.Equal(new Rgba8UNorm(105, 160, 160), decoded.Pixels[3]);
    }

    [Fact]
    public void GetEncodedByteCountRejectsOddWidth()
    {
        var coder = new PackedRgb422TextureCoder(TextureFormats.R8G8B8G8_422UNorm);

        var exception = Assert.Throws<ArgumentException>(() => coder.GetEncodedByteCount(3, 1, coder.GetDefaultPitch(3)));

        Assert.Equal("width", exception.ParamName);
    }

    public static TheoryData<TextureFormat> PackedRgb422Formats() => new()
    {
        TextureFormats.R8G8B8G8_422UNorm,
        TextureFormats.G8R8G8B8_422UNorm,
        TextureFormats.G8B8G8R8_422UNorm,
        TextureFormats.B8G8R8G8_422UNorm,
        TextureFormats.G10X6B10X6G10X6R10X6_422UNorm,
        TextureFormats.B10X6G10X6R10X6G10X6_422UNorm,
        TextureFormats.G12X4B12X4G12X4R12X4_422UNorm,
        TextureFormats.B12X4G12X4R12X4G12X4_422UNorm,
        TextureFormats.G16B16G16R16_422UNorm,
        TextureFormats.B16G16R16G16_422UNorm
    };

    public static TheoryData<TextureFormat, byte[]> PackedRgb4228BitLayouts() => new()
    {
        { TextureFormats.R8G8B8G8_422UNorm, [11, 22, 45, 66] },
        { TextureFormats.G8R8G8B8_422UNorm, [22, 11, 66, 45] },
        { TextureFormats.G8B8G8R8_422UNorm, [22, 45, 66, 11] },
        { TextureFormats.B8G8R8G8_422UNorm, [45, 22, 11, 66] }
    };

    public static TheoryData<TextureFormat, byte[]> PackedRgb42216BitLayouts() => new()
    {
        { TextureFormats.G16B16G16R16_422UNorm, [0xae, 0x08, 0x88, 0x13, 0x5c, 0x11, 0xd0, 0x07] },
        { TextureFormats.B16G16R16G16_422UNorm, [0x88, 0x13, 0xae, 0x08, 0xd0, 0x07, 0x5c, 0x11] }
    };

    public static TheoryData<TextureFormat, byte[]> PackedRgb422PaddedWordLayouts() => new()
    {
        { TextureFormats.G10X6B10X6G10X6R10X6_422UNorm, [0xc0, 0xff, 0x00, 0x00, 0x00, 0x00, 0xc0, 0xff] },
        { TextureFormats.B10X6G10X6R10X6G10X6_422UNorm, [0x00, 0x00, 0xc0, 0xff, 0xc0, 0xff, 0x00, 0x00] },
        { TextureFormats.G12X4B12X4G12X4R12X4_422UNorm, [0xf0, 0xff, 0x00, 0x00, 0x00, 0x00, 0xf0, 0xff] },
        { TextureFormats.B12X4G12X4R12X4G12X4_422UNorm, [0x00, 0x00, 0xf0, 0xff, 0xf0, 0xff, 0x00, 0x00] }
    };
}
