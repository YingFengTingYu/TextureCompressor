using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class BigEndianTextureFormatTests
{
    [Theory]
    [MemberData(nameof(RepresentativeFormats))]
    public void GlobalManagerFindsBigEndianCoders(TextureFormat format, Type coderType)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.IsType(coderType, coder);
        Assert.EndsWith("_BE", format.Name);
    }

    [Fact]
    public void Bgra8DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0x44, 0x11, 0x22, 0x33 };
        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Bgra8BigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba8UNorm(0x11, 0x22, 0x33, 0x44), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void R5G6B5DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0xf8, 0x00 };
        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        var coder = new PackedUNormTextureCoder(TextureFormats.Rgb565UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.Pixels[0]);

        var roundTrip = new byte[2];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void G8R8DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0x34, 0x12 };
        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rg8UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba8UNorm(0x12, 0x34, 0), decoded.Pixels[0]);

        var roundTrip = new byte[2];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void Q8W8V8U8DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0x40, 0x30, 0x20, 0x10 };
        var decoded = new ArrayBitmap<Rgba8SNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rgba8SNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba8SNorm(0x10, 0x20, 0x30, 0x40), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void G16R16DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0x33, 0x44, 0x11, 0x22 };
        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rg16UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba16UNorm(0x1122, 0x3344, 0), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void V16U16DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0x33, 0x44, 0x11, 0x22 };
        var decoded = new ArrayBitmap<Rgba16SNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rg16SNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba16SNorm(0x1122, 0x3344, 0), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void A16L16DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0x33, 0x44, 0x11, 0x22 };
        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Luminance16Alpha16UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba16UNorm(0x1122, 0x1122, 0x1122, 0x3344), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void Q16W16V16U16DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var decoded = new ArrayBitmap<Rgba16SNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rgba16SNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba16SNorm(0x0102, 0x0304, 0x0506, 0x0708), decoded.Pixels[0]);

        var roundTrip = new byte[8];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void G16R16FDecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0x40, 0x00, 0x3c, 0x00 };
        var decoded = new ArrayBitmap<Rgba16Float>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rg16FloatBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal((Half)1f, decoded.Pixels[0].Red);
        Assert.Equal((Half)2f, decoded.Pixels[0].Green);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void R10Gb11DecodesRedFromHighBits()
    {
        var encoded = new byte[] { 0xff, 0xc0, 0x00, 0x00 };
        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        var coder = new PackedUNormTextureCoder(TextureFormats.R10Gb11UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba16UNorm(ushort.MaxValue, 0, 0), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void Rg11B10DecodesRedFromHighBits()
    {
        var encoded = new byte[] { 0xff, 0xe0, 0x00, 0x00 };
        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        var coder = new PackedUNormTextureCoder(TextureFormats.Rg11B10UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba16UNorm(ushort.MaxValue, 0, 0), decoded.Pixels[0]);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void G32R32DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0x33, 0x44, 0x11, 0x22, 0x77, 0x88, 0x55, 0x66 };
        var decoded = new ArrayBitmap<Rgba32UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rg32UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba32UNorm(0x11223344, 0x55667788, 0), decoded.Pixels[0]);

        var roundTrip = new byte[8];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void A32L32DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0x33, 0x44, 0x11, 0x22, 0x77, 0x88, 0x55, 0x66 };
        var decoded = new ArrayBitmap<Rgba32UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Luminance32Alpha32UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba32UNorm(0x11223344, 0x11223344, 0x11223344, 0x55667788), decoded.Pixels[0]);

        var roundTrip = new byte[8];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void A32B32G32R32DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[]
        {
            0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08,
            0x09, 0x0a, 0x0b, 0x0c,
            0x0d, 0x0e, 0x0f, 0x10
        };
        var decoded = new ArrayBitmap<Rgba32UNorm>(1, 1);
        var coder = new SequentialUncompressedTextureCoder(TextureFormats.Rgba32UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba32UNorm(0x01020304, 0x05060708, 0x090a0b0c, 0x0d0e0f10), decoded.Pixels[0]);

        var roundTrip = new byte[16];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void Dxt1Decodes8In16SwappedBlocks()
    {
        var encoded = new byte[TextureFormats.Dxt1RgbaBigEndian.GetByteCount(4, 4)];
        BinaryPrimitives.WriteUInt16BigEndian(encoded, 0xf800);
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(2), 0xf800);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1RgbaBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(4));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), pixel));
    }

    [Fact]
    public void D16DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0x80, 0x00 };
        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        var coder = new DepthStencilTextureCoder(TextureFormats.DepthComponent16BigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.InRange(decoded.Pixels[0].Red, 0.5000f, 0.5001f);

        var roundTrip = new byte[2];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void Rgb655DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0xfc, 0x1f };
        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        var coder = new PackedUNormTextureCoder(TextureFormats.Rgb655UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(0f, decoded.Pixels[0].Green);
        Assert.Equal(1f, decoded.Pixels[0].Blue);

        var roundTrip = new byte[2];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void D24FS8DecodesAndEncodesWith8In32Swap()
    {
        var encoded = new byte[] { 0xf0, 0x00, 0x00, 0xff };
        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        var coder = new DepthStencilTextureCoder(TextureFormats.Depth24FloatStencil8BigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(1f, decoded.Pixels[0].Green);

        var roundTrip = new byte[4];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    public static TheoryData<TextureFormat, Type> RepresentativeFormats() => new()
    {
        { TextureFormats.Alpha8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Luminance8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Luminance16UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Luminance32UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Luminance8Alpha8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Luminance16Alpha16UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Luminance32Alpha32UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgb655UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg5SNormB6UNormRevBigEndian, typeof(PackedSNormTextureCoder) },
        { TextureFormats.A1Rgb5UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Argb4UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rgba4RevSNormBigEndian, typeof(PackedSNormTextureCoder) },
        { TextureFormats.Bgra8BigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Bgrx8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg8SNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba8SNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg8SNormB8UNormX8RevBigEndian, typeof(PackedSNormTextureCoder) },
        { TextureFormats.Rgb10SNormA2UNormRevBigEndian, typeof(PackedSNormTextureCoder) },
        { TextureFormats.R10Gb11UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg11B10UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.R10Gb11RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg11B10RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg11B10RevSNormBigEndian, typeof(PackedSNormTextureCoder) },
        { TextureFormats.R10Gb11RevSNormBigEndian, typeof(PackedSNormTextureCoder) },
        { TextureFormats.Bgr10A2RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rgb10A2RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg16UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg16SNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba16UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba16SNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.R16FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg16FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba16FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg32UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg32SNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba32UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba32SNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.R32FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg32FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba32FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgb565UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.X1Rgb5UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Xrgb4UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Bgr10X2RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.R11G11B10FloatBigEndian, typeof(PackedFloatTextureCoder) },
        { TextureFormats.Uyvy422UNormBigEndian, typeof(PackedYuv422TextureCoder) },
        { TextureFormats.Yuy2UNormBigEndian, typeof(PackedYuv422TextureCoder) },
        { TextureFormats.G8R8G8B8_422UNormBigEndian, typeof(PackedRgb422TextureCoder) },
        { TextureFormats.R8G8B8G8_422UNormBigEndian, typeof(PackedRgb422TextureCoder) },
        { TextureFormats.DepthComponent16BigEndian, typeof(DepthStencilTextureCoder) },
        { TextureFormats.Depth24X8BigEndian, typeof(DepthStencilTextureCoder) },
        { TextureFormats.Depth24Stencil8BigEndian, typeof(DepthStencilTextureCoder) },
        { TextureFormats.Depth24FloatStencil8BigEndian, typeof(DepthStencilTextureCoder) },
        { TextureFormats.Dxt1RgbBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt1RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt2RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt3RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt3ABigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt3A1111BigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt4RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt5RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt5ABigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.DxnBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Ctx1BigEndian, typeof(S3tcTextureCoder) }
    };
}
