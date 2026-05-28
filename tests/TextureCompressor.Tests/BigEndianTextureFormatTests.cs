using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

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
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
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
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        var coder = new PackedUNormTextureCoder(TextureFormats.Rgb565UNormBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.Pixels[0]);

        var roundTrip = new byte[2];
        coder.Encode(decoded.AsView(), roundTrip, coder.GetDefaultPitch(1));

        Assert.Equal(encoded, roundTrip);
    }

    [Fact]
    public void Dxt1Decodes8In16SwappedBlocks()
    {
        var encoded = new byte[TextureFormats.Dxt1RgbaBigEndian.GetByteCount(4, 4)];
        BinaryPrimitives.WriteUInt16BigEndian(encoded, 0xf800);
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(2), 0xf800);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1RgbaBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(4));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), pixel));
    }

    [Fact]
    public void D16DecodesAndEncodesWith8In16Swap()
    {
        var encoded = new byte[] { 0x80, 0x00 };
        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        var coder = new DepthStencilTextureCoder(TextureFormats.DepthComponent16BigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(1));

        Assert.InRange(decoded.Pixels[0].Red, 0.5000f, 0.5001f);

        var roundTrip = new byte[2];
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
        { TextureFormats.A1Rgb5UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Argb4UNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Bgra8BigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Bgrx8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba8UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Bgr10A2RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rgb10A2RevUNormBigEndian, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg16UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba16UNormBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.R16FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rg16FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
        { TextureFormats.Rgba16FloatBigEndian, typeof(SequentialUncompressedTextureCoder) },
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
        { TextureFormats.Dxt1RgbBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt1RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt2RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt3RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt3ABigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt4RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt5RgbaBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Dxt5ABigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.DxnBigEndian, typeof(S3tcTextureCoder) },
        { TextureFormats.Ctx1BigEndian, typeof(S3tcTextureCoder) }
    };
}
