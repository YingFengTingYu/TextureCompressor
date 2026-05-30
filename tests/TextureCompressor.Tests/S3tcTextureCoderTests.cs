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

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5Rgba);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Dxt3ADecodesExplicitAlphaToRgba8Alpha()
    {
        var encoded = new byte[TextureFormats.Dxt3A.GetByteCount(4, 4)];
        encoded[0] = 0x0f;

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt3A);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), decoded.Pixels[1]);
    }

    [Fact]
    public void Dxt3A1111DecodesExplicitAlphaBitsToRgba8Channels()
    {
        var encoded = new byte[TextureFormats.Dxt3A1111.GetByteCount(4, 4)];
        encoded[0] = 0x1e;

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt3A1111);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 255, 255, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Dxt3A1111BigEndianDecodes8In16SwappedExplicitAlphaBits()
    {
        var littleEndian = new byte[TextureFormats.Dxt3A1111BigEndian.GetByteCount(4, 4)];
        littleEndian[0] = 0x1e;
        var encoded = Swap8In16(littleEndian);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt3A1111BigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 255, 255, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodeDxt3A1111RoundTripsSolidOneBitRgba()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 255, 0), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt3A1111);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(encoded, value => Assert.Equal(0xaa, value));
        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 255, 0), pixel));
    }

    [Fact]
    public void Dxt5ADecodesInterpolatedAlphaToRgba8Alpha()
    {
        var encoded = new byte[TextureFormats.Dxt5A.GetByteCount(4, 4)];
        WriteAlphaBlock(encoded, 255, 0, 1);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5A);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodeDxt5ARoundTripsSolidAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt5A);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0, 0, 0, 128), pixel));
    }

    [Fact]
    public void DxnDecodesTwoDxt5AChannelsToRgba8RedGreen()
    {
        var encoded = new byte[TextureFormats.Dxn.GetByteCount(4, 4)];
        WriteAlphaBlock(encoded, 255, 0, 1);
        WriteAlphaBlock(encoded.AsSpan(8), 64, 192, 1);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxn);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 192, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 64, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodeDxnRoundTripsSolidRedGreen()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(34, 200, 123, 64), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Dxn);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(34, 200, 0, 255), pixel));
    }

    [Fact]
    public void DxnBigEndianDecodes8In16SwappedDxt5ABlocks()
    {
        var littleEndian = new byte[TextureFormats.DxnBigEndian.GetByteCount(4, 4)];
        WriteAlphaBlock(littleEndian, 255, 0, 1);
        WriteAlphaBlock(littleEndian.AsSpan(8), 64, 192, 1);
        var encoded = Swap8In16(littleEndian);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.DxnBigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 192, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 64, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Ctx1DecodesSharedIndicesToRgba8RedGreen()
    {
        var encoded = new byte[TextureFormats.Ctx1.GetByteCount(4, 4)];
        WriteCtx1Block(encoded, 255, 0, 64, 192, 1);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Ctx1);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 192, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 64, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Ctx1BigEndianDecodes8In16SwappedBlock()
    {
        var littleEndian = new byte[TextureFormats.Ctx1BigEndian.GetByteCount(4, 4)];
        WriteCtx1Block(littleEndian, 255, 0, 64, 192, 1);
        var encoded = Swap8In16(littleEndian);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Ctx1BigEndian);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 192, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 64, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodeCtx1RoundTripsSolidRedGreen()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(34, 200, 123, 64), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new S3tcTextureCoder(TextureFormats.Ctx1);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(34, 200, 0, 255), pixel));
    }

    [Fact]
    public void Dxt1RgbaSrgbDecodesRgbAfterS3tcInterpolationAndKeepsAlphaLinear()
    {
        var encoded = new byte[TextureFormats.Dxt1RgbaSrgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0xf800, 0x7800, 0xaaaaaaaa);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(0.5f, 0f, 0f, 0.25f), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
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
        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 1);
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
        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 5);
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
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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
    public void EncodeDxt1RgbUsesFastBoundsByDefault()
    {
        var pixels = Enumerable.Range(0, 16)
            .Select(i => (i & 1) == 0
                ? new Rgba8UNorm(255, 0, 0, 255)
                : new Rgba8UNorm(0, 255, 0, 255))
            .ToArray();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(EncodeBoundsDxt1RgbBlock(pixels), encoded);
    }

    [Fact]
    public void EncodeDxt1RgbHighQualityFitsColorClustersInsteadOfRgbBounds()
    {
        var pixels = Enumerable.Range(0, 16)
            .Select(i => (i & 1) == 0
                ? new Rgba8UNorm(255, 0, 0, 255)
                : new Rgba8UNorm(0, 255, 0, 255))
            .ToArray();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var options = new S3tcCoderOptions { CompressionMode = S3tcCompressionMode.High };
        var coder = new S3tcTextureCoder(TextureFormats.Dxt1Rgb, options);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var boundsDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);
        coder.Decode(EncodeBoundsDxt1RgbBlock(pixels), boundsDecoded.AsView(), rowPitch);

        Assert.Equal(0, RgbSquaredError(source, decoded));
        Assert.True(RgbSquaredError(source, decoded) < RgbSquaredError(source, boundsDecoded));
    }

    [Fact]
    public void EncodeAndDecodeDxt5RgbaRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);
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

    private static byte[] EncodeBoundsDxt1RgbBlock(ReadOnlySpan<Rgba8UNorm> source)
    {
        var min = new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue);
        var max = new Rgb24(byte.MinValue, byte.MinValue, byte.MinValue);
        for (var i = 0; i < source.Length; i++)
        {
            min = new Rgb24(
                Math.Min(min.Red, source[i].Red),
                Math.Min(min.Green, source[i].Green),
                Math.Min(min.Blue, source[i].Blue));
            max = new Rgb24(
                Math.Max(max.Red, source[i].Red),
                Math.Max(max.Green, source[i].Green),
                Math.Max(max.Blue, source[i].Blue));
        }

        var color0 = PackRgb565(max);
        var color1 = PackRgb565(min);
        if (color0 < color1)
        {
            (color0, color1) = (color1, color0);
        }

        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildDxt1RgbPalette(color0, color1, palette);
        uint indices = 0;
        for (var i = 0; i < source.Length; i++)
        {
            indices |= (uint)FindNearestColorIndex(source[i], palette) << (i * 2);
        }

        var encoded = new byte[8];
        WriteColorBlock(encoded, color0, color1, indices);
        return encoded;
    }

    private static void BuildDxt1RgbPalette(ushort color0, ushort color1, Span<Rgba8UNorm> palette)
    {
        var c0 = UnpackRgb565(color0);
        var c1 = UnpackRgb565(color1);
        palette[0] = new Rgba8UNorm(c0.Red, c0.Green, c0.Blue, 255);
        palette[1] = new Rgba8UNorm(c1.Red, c1.Green, c1.Blue, 255);
        if (color0 > color1)
        {
            palette[2] = Interpolate(c0, c1, 2, 1, 3);
            palette[3] = Interpolate(c0, c1, 1, 2, 3);
        }
        else
        {
            palette[2] = Interpolate(c0, c1, 1, 1, 2);
            palette[3] = new Rgba8UNorm(0, 0, 0, 255);
        }
    }

    private static int FindNearestColorIndex(Rgba8UNorm color, ReadOnlySpan<Rgba8UNorm> palette)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var red = color.Red - palette[i].Red;
            var green = color.Green - palette[i].Green;
            var blue = color.Blue - palette[i].Blue;
            var distance = (red * red) + (green * green) + (blue * blue);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static Rgba8UNorm Interpolate(Rgb24 a, Rgb24 b, int weightA, int weightB, int divisor)
    {
        var bias = divisor == 3 ? 1 : 0;
        return new Rgba8UNorm(
            (byte)(((weightA * a.Red) + (weightB * b.Red) + bias) / divisor),
            (byte)(((weightA * a.Green) + (weightB * b.Green) + bias) / divisor),
            (byte)(((weightA * a.Blue) + (weightB * b.Blue) + bias) / divisor),
            255);
    }

    private static Rgb24 UnpackRgb565(ushort value)
    {
        var red = (value >> 11) & 0x1f;
        var green = (value >> 5) & 0x3f;
        var blue = value & 0x1f;
        return new Rgb24(
            (byte)((red << 3) | (red >> 2)),
            (byte)((green << 2) | (green >> 4)),
            (byte)((blue << 3) | (blue >> 2)));
    }

    private static ushort PackRgb565(Rgb24 value)
    {
        var red = value.Red >> 3;
        var green = value.Green >> 2;
        var blue = value.Blue >> 3;
        return (ushort)((red << 11) | (green << 5) | blue);
    }

    private static long RgbSquaredError(ArrayBitmap<Rgba8UNorm> expected, ArrayBitmap<Rgba8UNorm> actual)
    {
        long error = 0;
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            var red = expected.Pixels[i].Red - actual.Pixels[i].Red;
            var green = expected.Pixels[i].Green - actual.Pixels[i].Green;
            var blue = expected.Pixels[i].Blue - actual.Pixels[i].Blue;
            error += (red * red) + (green * green) + (blue * blue);
        }

        return error;
    }

    private static void WriteAlphaBlock(Span<byte> destination, byte value0, byte value1, ulong indices)
    {
        destination[0] = value0;
        destination[1] = value1;
        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(indices >> (8 * i));
        }
    }

    private static void WriteCtx1Block(
        Span<byte> destination,
        byte red0,
        byte red1,
        byte green0,
        byte green1,
        uint indices)
    {
        destination[0] = red0;
        destination[1] = red1;
        destination[2] = green0;
        destination[3] = green1;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], indices);
    }

    private static byte[] Swap8In16(ReadOnlySpan<byte> source)
    {
        var destination = source.ToArray();
        for (var i = 0; i < destination.Length; i += 2)
        {
            (destination[i], destination[i + 1]) = (destination[i + 1], destination[i]);
        }

        return destination;
    }

    private static byte Srgb8ToLinearUNorm8(byte value)
    {
        var srgb = value / 255f;
        var linear = srgb <= 0.04045f
            ? srgb / 12.92f
            : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);

        return (byte)MathF.Round(Math.Clamp(linear, 0f, 1f) * 255f);
    }

    private readonly record struct Rgb24(byte Red, byte Green, byte Blue);

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
        TextureFormats.Dxt3A,
        TextureFormats.Dxt3A1111,
        TextureFormats.Dxt4Rgba,
        TextureFormats.Dxt5Rgba,
        TextureFormats.Dxt5RgbaSrgb,
        TextureFormats.Dxt5A,
        TextureFormats.Dxn,
        TextureFormats.Ctx1,
        TextureFormats.Dxt1RgbBigEndian,
        TextureFormats.Dxt1RgbaBigEndian,
        TextureFormats.Dxt2RgbaBigEndian,
        TextureFormats.Dxt3RgbaBigEndian,
        TextureFormats.Dxt3ABigEndian,
        TextureFormats.Dxt3A1111BigEndian,
        TextureFormats.Dxt4RgbaBigEndian,
        TextureFormats.Dxt5RgbaBigEndian,
        TextureFormats.Dxt5ABigEndian,
        TextureFormats.DxnBigEndian,
        TextureFormats.Ctx1BigEndian
    };
}
