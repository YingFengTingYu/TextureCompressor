using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class S3tcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(S3tcFormats))]
    public void GlobalManagerFindsS3tcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(S3tcTextureCoder.IsSupported(format));
        Assert.IsType<S3tcTextureCoder>(coder);
    }

    [Fact]
    public void Dxt1RgbaDecodesTransparentIndexToRgba8AlphaZero()
    {
        var encoded = new byte[TextureFormats.Dxt1Rgba.GetByteCount(4, 4)];
        BinaryPrimitives.WriteUInt16LittleEndian(encoded, 0x0000);
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(2), 0xffff);
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(4), 0xffffffff);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(0, pixel.Alpha));
    }

    [Fact]
    public void Dxt1RgbDecodesTransparentIndexAsOpaqueBlack()
    {
        var encoded = new byte[TextureFormats.Dxt1Rgb.GetByteCount(4, 4)];
        BinaryPrimitives.WriteUInt16LittleEndian(encoded, 0x0000);
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(2), 0xffff);
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(4), 0xffffffff);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), pixel));
    }

    [Fact]
    public void Dxt3RgbaDecodesExplicitAlphaAndColorToRgba8()
    {
        var encoded = new byte[TextureFormats.Dxt3Rgba.GetByteCount(4, 4)];
        encoded[0] = 0x0f;
        WriteColorBlock(encoded.AsSpan(8), 0xf800, 0xf800, 0);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt3Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 0, 0, 0), decoded.Pixels[1]);
    }

    [Fact]
    public void Dxt5RgbaDecodesInterpolatedAlphaAndColorToRgba8()
    {
        var encoded = new byte[TextureFormats.Dxt5Rgba.GetByteCount(4, 4)];
        encoded[0] = 255;
        encoded[1] = 0;
        encoded[2] = 0x01;
        WriteColorBlock(encoded.AsSpan(8), 0x07e0, 0x07e0, 0);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Dxt1RgbaSrgbDecodesRgbAfterS3tcInterpolationAndKeepsAlphaLinear()
    {
        var encoded = new byte[TextureFormats.Dxt1RgbaSrgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0xf800, 0x7800, 0xaaaaaaaa);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1RgbaSrgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal(Srgb8ToLinearUNorm8(211), pixel.Red);
            Assert.Equal(0, pixel.Green);
            Assert.Equal(0, pixel.Blue);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeDxt5RgbaSrgbAppliesGammaToRgbAndUNormToAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(0.5f, 0f, 0f, 0.25f), 16).ToArray());
        var decoded = new ArrayTextureBitmap<Rgba32Float>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5RgbaSrgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 0.48f, 0.53f);
            Assert.Equal(0f, pixel.Green);
            Assert.Equal(0f, pixel.Blue);
            Assert.InRange(pixel.Alpha, 0.24f, 0.26f);
        });
    }

    [Fact]
    public void Dxt1DecodeUsesPaddedBlockWidthForNonMultipleOfFourWidth()
    {
        var encoded = new byte[TextureFormats.Dxt1Rgba.GetByteCount(5, 1)];
        WriteColorBlock(encoded, 0xf800, 0xf800, 0);
        WriteColorBlock(encoded.AsSpan(8), 0x07e0, 0x07e0, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(5, 1);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[4]);
    }

    [Fact]
    public void Dxt1DecodeUsesPaddedBlockHeightForNonMultipleOfFourHeight()
    {
        var encoded = new byte[TextureFormats.Dxt1Rgba.GetByteCount(1, 5)];
        WriteColorBlock(encoded, 0xf800, 0xf800, 0);
        WriteColorBlock(encoded.AsSpan(8), 0x001f, 0x001f, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 5);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255, 255), decoded.Pixels[4]);
    }

    [Fact]
    public void EncodeAndDecodeDxt1RgbaPreservesTransparentTexels()
    {
        var pixels = Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 16).ToArray();
        pixels[0] = new Rgba8UNorm(0, 0, 0, 0);
        var source = new ArrayTextureBitmap<Rgba8UNorm>(4, 4, pixels);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgba);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(0, decoded.Pixels[0].Alpha);
        Assert.Equal(255, decoded.Pixels[1].Alpha);
        Assert.Equal(255, decoded.Pixels[1].Red);
    }

    [Fact]
    public void EncodeAndDecodeDxt5RgbaRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5Rgba);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 35);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(128, pixel.Alpha);
        });
    }

    [Fact]
    public void Dxt2RgbaDecodeRecoversPremultipliedColor()
    {
        var encoded = new byte[TextureFormats.Dxt2Rgba.GetByteCount(4, 4)];
        for (var i = 0; i < 8; i++)
        {
            encoded[i] = 0x88;
        }

        WriteColorBlock(encoded.AsSpan(8), 0x6000, 0x6000, 0);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt2Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.InRange(decoded.Pixels[0].Red, 175, 190);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(136, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeHonorsBlockRowPitch()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 25).ToArray());
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgba);
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
    public void Dxt5RgbaByteCountUsesFourByFourBlockRows()
    {
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5Rgba);

        Assert.Equal(32, coder.GetDefaultPitch(5));
        Assert.Equal(64, coder.GetEncodedByteCount(5, 5, coder.GetDefaultPitch(5)));
        Assert.Equal(80, coder.GetEncodedByteCount(5, 5, 40));
    }

    private static void WriteColorBlock(Span<byte> destination, ushort color0, ushort color1, uint indices)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], indices);
    }

    private static byte Srgb8ToLinearUNorm8(byte value)
    {
        var srgb = value / 255f;
        var linear = srgb <= 0.04045f
            ? srgb / 12.92f
            : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);

        return (byte)MathF.Round(Math.Clamp(linear, 0f, 1f) * 255f);
    }

    public static TheoryData<TextureFormat> S3tcFormats() => new()
    {
        TextureFormats.Bc1Rgb,
        TextureFormats.Bc1RgbSrgb,
        TextureFormats.Bc1Rgba,
        TextureFormats.Bc1RgbaSrgb,
        TextureFormats.Bc2Rgba,
        TextureFormats.Bc2RgbaSrgb,
        TextureFormats.Bc3Rgba,
        TextureFormats.Bc3RgbaSrgb,
        TextureFormats.Dxt1Rgb,
        TextureFormats.Dxt1RgbSrgb,
        TextureFormats.Dxt1Rgba,
        TextureFormats.Dxt1RgbaSrgb,
        TextureFormats.Dxt2Rgba,
        TextureFormats.Dxt3Rgba,
        TextureFormats.Dxt3RgbaSrgb,
        TextureFormats.Dxt4Rgba,
        TextureFormats.Dxt5Rgba,
        TextureFormats.Dxt5RgbaSrgb
    };
}
