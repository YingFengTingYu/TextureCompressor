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
}
