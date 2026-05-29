using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class AstcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(AstcFormats))]
    public void GlobalManagerFindsAstcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(AstcTextureCoder.IsSupported(format));
        Assert.IsType<AstcTextureCoder>(coder);
    }

    [Fact]
    public void AstcRowByteCountUsesFootprintBlocks()
    {
        Assert.Equal(16, TextureFormats.RgbaAstc4x4UNorm.GetRowByteCount(1));
        Assert.Equal(16, TextureFormats.RgbaAstc4x4UNorm.GetRowByteCount(4));
        Assert.Equal(32, TextureFormats.RgbaAstc4x4UNorm.GetRowByteCount(5));
        Assert.Equal(16, TextureFormats.RgbaAstc12x12UNorm.GetRowByteCount(12));
        Assert.Equal(32, TextureFormats.RgbaAstc12x12UNorm.GetRowByteCount(13));
    }

    [Theory]
    [MemberData(nameof(AstcUNormFormats))]
    public void DecodeLdrVoidExtentSupportsEveryFootprint(TextureFormat format)
    {
        var encoded = CreateVoidExtentBlock(
            hdr: false,
            red: 0x2200,
            green: 0x4400,
            blue: 0x8800,
            alpha: 0xCC00);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        var coder = new AstcTextureCoder(format);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0x22, 0x44, 0x88, 0xCC), decoded.Pixels[0]);
    }

    [Theory]
    [MemberData(nameof(AstcFloatFormats))]
    public void DecodeHdrVoidExtentSupportsEveryFootprint(TextureFormat format)
    {
        var encoded = CreateVoidExtentBlock(
            hdr: true,
            red: BitConverter.HalfToUInt16Bits((Half)0.25f),
            green: BitConverter.HalfToUInt16Bits((Half)0.5f),
            blue: BitConverter.HalfToUInt16Bits((Half)2f),
            alpha: BitConverter.HalfToUInt16Bits((Half)1f));
        var decoded = new ArrayTextureBitmap<Rgba16Float>(1, 1);
        var coder = new AstcTextureCoder(format);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba16Float((Half)0.25f, (Half)0.5f, (Half)2f, (Half)1f), decoded.Pixels[0]);
    }

    [Fact]
    public void DecodeLdrVoidExtentFillsClippedImage()
    {
        var encoded = CreateVoidExtentBlock(
            hdr: false,
            red: 0x1234,
            green: 0xABCD,
            blue: 0x4001,
            alpha: 0xFE20);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(3, 2);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0x12, 0xAB, 0x40, 0xFE), pixel));
    }

    [Fact]
    public void DecodeSrgbVoidExtentConvertsRgbOnly()
    {
        var encoded = CreateVoidExtentBlock(
            hdr: false,
            red: 0x8080,
            green: 0x4040,
            blue: 0xFFFF,
            alpha: 0x2020);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4Srgb);
        var expected = new Rgba8UNorm(
            Srgb8ToLinearUNorm8(0x80),
            Srgb8ToLinearUNorm8(0x40),
            Srgb8ToLinearUNorm8(0xFF),
            0x20);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(expected, pixel));
    }

    [Fact]
    public void DecodeHdrVoidExtentFillsRgba16Float()
    {
        var one = BitConverter.HalfToUInt16Bits((Half)1f);
        var half = BitConverter.HalfToUInt16Bits((Half)0.5f);
        var two = BitConverter.HalfToUInt16Bits((Half)2f);
        var quarter = BitConverter.HalfToUInt16Bits((Half)0.25f);
        var encoded = CreateVoidExtentBlock(hdr: true, one, half, two, quarter);
        var decoded = new ArrayTextureBitmap<Rgba16Float>(2, 2);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4Float);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal((Half)1f, pixel.Red);
            Assert.Equal((Half)0.5f, pixel.Green);
            Assert.Equal((Half)2f, pixel.Blue);
            Assert.Equal((Half)0.25f, pixel.Alpha);
        });
    }

    [Fact]
    public void DecodeLdrOrdinaryBlockUsesEndpointWeights()
    {
        var encoded = CreateSinglePartitionLumaBlock(0x10, 0xE0, quantizedWeight: 3);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0xE0, 0xE0, 0xE0, 255), pixel));
    }

    [Fact]
    public void DecodeHdrOrdinaryBlockUsesRgba16FloatOutput()
    {
        var encoded = CreateSinglePartitionLumaBlock(0x10, 0xE0, quantizedWeight: 3);
        var decoded = new ArrayTextureBitmap<Rgba16Float>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4Float);
        var expected = new Rgba16Float((Half)(0xE0 / 255f), (Half)(0xE0 / 255f), (Half)(0xE0 / 255f), (Half)1f);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(expected, pixel));
    }

    [Fact]
    public void DecodeLdrProfileRejectsHdrEndpointMode()
    {
        var encoded = CreateSinglePartitionHdrLumaBlock(0x20, 0x40);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 255, 255), pixel));
    }

    [Fact]
    public void DecodeTwoPartitionBlockUsesPartitionSeed()
    {
        var encoded = CreateTwoPartitionLumaBlock();
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);
        var dark = new Rgba8UNorm(0x20, 0x20, 0x20, 255);
        var bright = new Rgba8UNorm(0xD0, 0xD0, 0xD0, 255);
        var expected = new[,]
        {
            { dark, bright, bright, dark },
            { bright, bright, bright, dark },
            { bright, bright, dark, dark },
            { bright, bright, dark, dark }
        };

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(expected[y, x], decoded.AsView().GetRowSpan(y)[x]);
            }
        }
    }

    [Fact]
    public void DecodeDualPlaneBlockUsesSeparateChannelWeight()
    {
        var encoded = CreateDualPlaneLumaBlock();
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0xE0, 0x10, 0x10, 255), pixel));
    }

    [Fact]
    public void DecodeDualPlaneFourPartitionBlockIsIllegal()
    {
        var encoded = CreateDualPlaneFourPartitionBlock();
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 255, 255), pixel));
    }

    [Fact]
    public void InvalidBlockDecodesToMagenta()
    {
        var encoded = new byte[16];
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 255, 255), pixel));
    }

    [Fact]
    public void DecodeHonorsRowPitch()
    {
        var rowPitch = 32;
        var encoded = new byte[rowPitch * 2];
        CreateVoidExtentBlock(hdr: false, 0xFF00, 0x0000, 0x0000, 0xFFFF).CopyTo(encoded.AsSpan(0, 16));
        CreateVoidExtentBlock(hdr: false, 0x0000, 0xFF00, 0x0000, 0xFFFF).CopyTo(encoded.AsSpan(rowPitch, 16));
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 5);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Decode(encoded, decoded.AsView(), rowPitch);

        for (var y = 0; y < 4; y++)
        {
            Assert.All(decoded.AsView().GetRowSpan(y).ToArray(), pixel => Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), pixel));
        }

        Assert.All(decoded.AsView().GetRowSpan(4).ToArray(), pixel => Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), pixel));
    }

    [Fact]
    public void SourceTooSmallThrows()
    {
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        var exception = Assert.Throws<ArgumentException>(() => coder.Decode([], decoded.AsView(), coder.GetDefaultPitch(decoded.Width)));
        Assert.Equal("source", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(AstcUNormFormats))]
    public void EncodeLdrSolidBlockRoundTripsEveryFootprint(TextureFormat format)
    {
        var color = new Rgba8UNorm(0x22, 0x44, 0x88, 0xCC);
        var source = new ArrayTextureBitmap<Rgba8UNorm>(1, 1, [color]);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        var coder = new AstcTextureCoder(format);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];

        coder.Encode(source.AsView(), encoded, coder.GetDefaultPitch(source.Width));
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(color, decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeLdrGradientRoundTripsFastWeightGrid()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var values = new byte[] { 0, 84, 171, 255 };
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                source.Pixels[(y * source.Width) + x] = new Rgba8UNorm(values[x], values[x], values[x], 255);
            }
        }

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];

        coder.Encode(source.AsView(), encoded, coder.GetDefaultPitch(source.Width));
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Fact]
    public void EncodeLdrLargeFootprintGradientProducesLegalBlock()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(12, 12);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var value = (byte)Math.Round(x * (255.0 / (source.Width - 1)));
                source.Pixels[(y * source.Width) + x] = new Rgba8UNorm(value, value, value, 255);
            }
        }

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(12, 12);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc12x12UNorm);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];

        coder.Encode(source.AsView(), encoded, coder.GetDefaultPitch(source.Width));
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.NotEqual(new Rgba8UNorm(255, 0, 255, 255), decoded.Pixels[0]);
        Assert.True(decoded.Pixels[0].Red < decoded.Pixels[^1].Red);
    }

    [Fact]
    public void EncodeSrgbUsesSrgbStorageForRgbOnly()
    {
        var sourceColor = new Rgba8UNorm(40, 100, 200, 123);
        var expected = new Rgba8UNorm(
            Srgb8ToLinearUNorm8(LinearUNorm8ToSrgb8(sourceColor.Red)),
            Srgb8ToLinearUNorm8(LinearUNorm8ToSrgb8(sourceColor.Green)),
            Srgb8ToLinearUNorm8(LinearUNorm8ToSrgb8(sourceColor.Blue)),
            sourceColor.Alpha);
        var source = new ArrayTextureBitmap<Rgba8UNorm>(2, 2, [sourceColor, sourceColor, sourceColor, sourceColor]);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4Srgb);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];

        coder.Encode(source.AsView(), encoded, coder.GetDefaultPitch(source.Width));
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(expected, pixel));
    }

    [Fact]
    public void EncodeHonorsRowPitch()
    {
        var rowPitch = 32;
        var red = new Rgba8UNorm(255, 0, 0, 255);
        var green = new Rgba8UNorm(0, 255, 0, 255);
        var source = new ArrayTextureBitmap<Rgba8UNorm>(4, 5);
        for (var i = 0; i < source.Pixels.Length; i++)
        {
            source.Pixels[i] = i < 16 ? red : green;
        }

        var encoded = new byte[rowPitch * 2];
        Array.Fill(encoded, (byte)0xCD);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(4, 5);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(red, decoded.Pixels[0]);
        Assert.Equal(red, decoded.Pixels[15]);
        Assert.Equal(green, decoded.Pixels[16]);
        Assert.All(encoded.AsSpan(16, 16).ToArray(), value => Assert.Equal(0xCD, value));
    }

    [Theory]
    [MemberData(nameof(AstcFloatFormats))]
    public void EncodeHdrSolidBlockRoundTripsEveryFootprint(TextureFormat format)
    {
        var color = new Rgba16Float((Half)0.25f, (Half)2f, (Half)8f, (Half)1f);
        var source = new ArrayTextureBitmap<Rgba16Float>(1, 1, [color]);
        var decoded = new ArrayTextureBitmap<Rgba16Float>(1, 1);
        var coder = new AstcTextureCoder(format);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];

        coder.Encode(source.AsView(), encoded, coder.GetDefaultPitch(source.Width));
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(color, decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeHdrNonSolidBlockPreservesEndpointVariation()
    {
        var low = new Rgba16Float((Half)1f, (Half)2f, (Half)4f, (Half)1f);
        var high = new Rgba16Float((Half)3f, (Half)6f, (Half)8f, (Half)1f);
        var source = new ArrayTextureBitmap<Rgba16Float>(4, 4);
        for (var i = 0; i < source.Pixels.Length; i++)
        {
            source.Pixels[i] = (i & 1) == 0 ? low : high;
        }

        var decoded = new ArrayTextureBitmap<Rgba16Float>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4Float);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];

        coder.Encode(source.AsView(), encoded, coder.GetDefaultPitch(source.Width));
        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.True(decoded.Pixels[0].Red < decoded.Pixels[1].Red);
        Assert.True(decoded.Pixels[0].Green < decoded.Pixels[1].Green);
        Assert.True(decoded.Pixels[0].Blue < decoded.Pixels[1].Blue);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red > (Half)1f);
    }

    [Fact]
    public void EncodeHdrHonorsRowPitch()
    {
        var rowPitch = 32;
        var first = new Rgba16Float((Half)1f, (Half)2f, (Half)4f, (Half)1f);
        var second = new Rgba16Float((Half)8f, (Half)4f, (Half)2f, (Half)1f);
        var source = new ArrayTextureBitmap<Rgba16Float>(4, 5);
        for (var i = 0; i < source.Pixels.Length; i++)
        {
            source.Pixels[i] = i < 16 ? first : second;
        }

        var encoded = new byte[rowPitch * 2];
        Array.Fill(encoded, (byte)0xCD);
        var decoded = new ArrayTextureBitmap<Rgba16Float>(4, 5);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4Float);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(first, decoded.Pixels[0]);
        Assert.Equal(first, decoded.Pixels[15]);
        Assert.Equal(second, decoded.Pixels[16]);
        Assert.All(encoded.AsSpan(16, 16).ToArray(), value => Assert.Equal(0xCD, value));
    }

    [Fact]
    public void DestinationTooSmallThrows()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(4, 4);
        var coder = new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm);

        var exception = Assert.Throws<ArgumentException>(() => coder.Encode(source.AsView(), [], coder.GetDefaultPitch(source.Width)));
        Assert.Equal("destination", exception.ParamName);
    }

    public static TheoryData<TextureFormat> AstcFormats()
    {
        var formats = new TheoryData<TextureFormat>();
        foreach (var format in AstcTextureCoder.SupportedFormats)
        {
            formats.Add(format);
        }

        return formats;
    }

    public static TheoryData<TextureFormat> AstcUNormFormats() => new()
    {
        TextureFormats.RgbaAstc4x4UNorm,
        TextureFormats.RgbaAstc5x4UNorm,
        TextureFormats.RgbaAstc5x5UNorm,
        TextureFormats.RgbaAstc6x5UNorm,
        TextureFormats.RgbaAstc6x6UNorm,
        TextureFormats.RgbaAstc8x5UNorm,
        TextureFormats.RgbaAstc8x6UNorm,
        TextureFormats.RgbaAstc8x8UNorm,
        TextureFormats.RgbaAstc10x5UNorm,
        TextureFormats.RgbaAstc10x6UNorm,
        TextureFormats.RgbaAstc10x8UNorm,
        TextureFormats.RgbaAstc10x10UNorm,
        TextureFormats.RgbaAstc12x10UNorm,
        TextureFormats.RgbaAstc12x12UNorm
    };

    public static TheoryData<TextureFormat> AstcFloatFormats() => new()
    {
        TextureFormats.RgbaAstc4x4Float,
        TextureFormats.RgbaAstc5x4Float,
        TextureFormats.RgbaAstc5x5Float,
        TextureFormats.RgbaAstc6x5Float,
        TextureFormats.RgbaAstc6x6Float,
        TextureFormats.RgbaAstc8x5Float,
        TextureFormats.RgbaAstc8x6Float,
        TextureFormats.RgbaAstc8x8Float,
        TextureFormats.RgbaAstc10x5Float,
        TextureFormats.RgbaAstc10x6Float,
        TextureFormats.RgbaAstc10x8Float,
        TextureFormats.RgbaAstc10x10Float,
        TextureFormats.RgbaAstc12x10Float,
        TextureFormats.RgbaAstc12x12Float
    };

    private static byte[] CreateVoidExtentBlock(bool hdr, ushort red, ushort green, ushort blue, ushort alpha)
    {
        var block = new byte[16];
        var header = hdr
            ? new byte[] { 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }
            : new byte[] { 0xFC, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        header.CopyTo(block.AsSpan(0, 8));
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(8), red);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(10), green);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(12), blue);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(14), alpha);
        return block;
    }

    private static byte[] CreateSinglePartitionLumaBlock(byte endpoint0, byte endpoint1, int quantizedWeight)
    {
        UInt128 bits = 0;

        // 4x4 weight grid, 2-bit weight range, single partition, CEM 0 (luminance direct).
        bits |= 66;
        bits |= (UInt128)endpoint0 << 17;
        bits |= (UInt128)endpoint1 << 25;

        uint weightStream = 0;
        for (var i = 0; i < 16; i++)
        {
            weightStream |= (uint)(quantizedWeight & 3) << (i * 2);
        }

        bits |= (UInt128)ReverseBits32(weightStream) << 96;

        var block = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(0), (ulong)bits);
        BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(8), (ulong)(bits >> 64));
        return block;
    }

    private static byte[] CreateSinglePartitionHdrLumaBlock(byte endpoint0, byte endpoint1)
    {
        UInt128 bits = 0;

        bits |= 66;
        bits |= (UInt128)2 << 13;
        bits |= (UInt128)endpoint0 << 17;
        bits |= (UInt128)endpoint1 << 25;

        uint weightStream = 0;
        for (var i = 0; i < 16; i++)
        {
            weightStream |= 3U << (i * 2);
        }

        bits |= (UInt128)ReverseBits32(weightStream) << 96;
        return WriteBlock(bits);
    }

    private static byte[] CreateTwoPartitionLumaBlock()
    {
        UInt128 bits = 0;

        bits |= 66;
        bits |= (UInt128)1 << 11;
        bits |= (UInt128)1 << 13;
        bits |= (UInt128)0x00 << 29;
        bits |= (UInt128)0x20 << 37;
        bits |= (UInt128)0x00 << 45;
        bits |= (UInt128)0xD0 << 53;

        uint weightStream = 0;
        for (var i = 0; i < 16; i++)
        {
            weightStream |= 3U << (i * 2);
        }

        bits |= (UInt128)ReverseBits32(weightStream) << 96;
        return WriteBlock(bits);
    }

    private static byte[] CreateDualPlaneLumaBlock()
    {
        UInt128 bits = 0;

        bits |= 66;
        bits |= (UInt128)1 << 10;
        bits |= (UInt128)0x10 << 17;
        bits |= (UInt128)0xE0 << 25;

        ulong weightStream = 0;
        for (var i = 0; i < 16; i++)
        {
            weightStream |= 3UL << ((i * 4) + 2);
        }

        bits |= (UInt128)ReverseBits64(weightStream) << 64;
        return WriteBlock(bits);
    }

    private static byte[] CreateDualPlaneFourPartitionBlock()
    {
        UInt128 bits = 0;
        bits |= 66;
        bits |= (UInt128)1 << 10;
        bits |= (UInt128)3 << 11;
        return WriteBlock(bits);
    }

    private static byte[] WriteBlock(UInt128 bits)
    {
        var block = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(0), (ulong)bits);
        BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(8), (ulong)(bits >> 64));
        return block;
    }

    private static uint ReverseBits32(uint value)
    {
        value = ((value & 0x55555555U) << 1) | ((value >> 1) & 0x55555555U);
        value = ((value & 0x33333333U) << 2) | ((value >> 2) & 0x33333333U);
        value = ((value & 0x0F0F0F0FU) << 4) | ((value >> 4) & 0x0F0F0F0FU);
        value = ((value & 0x00FF00FFU) << 8) | ((value >> 8) & 0x00FF00FFU);
        return (value << 16) | (value >> 16);
    }

    private static ulong ReverseBits64(ulong value)
    {
        value = ((value & 0x5555555555555555UL) << 1) | ((value >> 1) & 0x5555555555555555UL);
        value = ((value & 0x3333333333333333UL) << 2) | ((value >> 2) & 0x3333333333333333UL);
        value = ((value & 0x0F0F0F0F0F0F0F0FUL) << 4) | ((value >> 4) & 0x0F0F0F0F0F0F0F0FUL);
        value = ((value & 0x00FF00FF00FF00FFUL) << 8) | ((value >> 8) & 0x00FF00FF00FF00FFUL);
        value = ((value & 0x0000FFFF0000FFFFUL) << 16) | ((value >> 16) & 0x0000FFFF0000FFFFUL);
        return (value << 32) | (value >> 32);
    }

    private static byte Srgb8ToLinearUNorm8(byte value)
    {
        var srgb = value / 255.0;
        var linear = srgb <= 0.04045
            ? srgb / 12.92
            : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        return (byte)Math.Clamp(Math.Round(linear * 255.0), 0.0, 255.0);
    }

    private static byte LinearUNorm8ToSrgb8(byte value)
    {
        var linear = value / 255.0;
        var srgb = linear <= 0.0031308
            ? linear * 12.92
            : (1.055 * Math.Pow(linear, 1.0 / 2.4)) - 0.055;
        return (byte)Math.Clamp(Math.Round(srgb * 255.0), 0.0, 255.0);
    }
}
