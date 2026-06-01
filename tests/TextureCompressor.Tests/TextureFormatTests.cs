using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureFormatTests
{
    [Fact]
    public void TextureFormatsDoesNotExposeBigEndianPlatformVariants()
    {
        var fields = typeof(TextureFormats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.DoesNotContain(fields, field => field.Name.EndsWith("BigEndian", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(MvpFormats))]
    public void MvpFormatPropertiesAreStable(
        TextureFormat format,
        string name,
        TextureFormatKind kind,
        TextureComponents components,
        int channelCount,
        int bitsPerBlock,
        int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(kind, format.Kind);
        Assert.Equal(components, format.Components);
        Assert.Equal(TextureValueKind.UNorm, format.ValueKind);
        Assert.Equal(channelCount, format.ChannelCount);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
        Assert.Equal(kind == TextureFormatKind.BlockCompressed, format.IsCompressed);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void R8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.R8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(7, 14)]
    public void Rg8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rg8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(7, 21)]
    public void Rgb8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rgb8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(7, 28)]
    public void Rgba8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rgba8UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(7, 28)]
    public void Bgra8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bgra8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    public void Alpha4RowByteCountRoundsEachRowToWholeBytes(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Alpha4UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    public void Bw1RowByteCountRoundsEachRowToWholeBytes(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bw1BppUNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void Bc1RowByteCountUsesFourByFourBlocks(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc1Rgba.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void Bc4RowByteCountUsesFourByFourBlocks(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc4UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 16)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void Bc5RowByteCountUsesFourByFourBlocks(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc5UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(3, 2, 24)]
    [InlineData(7, 5, 140)]
    public void Rgba8ByteCountMatchesPackedRows(int width, int height, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rgba8UNorm.GetByteCount(width, height));
    }

    [Fact]
    public void Luminance4ByteCountRoundsEveryRowIndependently()
    {
        Assert.Equal(6, TextureFormats.Luminance4UNorm.GetByteCount(5, 2));
    }

    [Theory]
    [InlineData(1, 1, 8)]
    [InlineData(4, 4, 8)]
    [InlineData(5, 4, 16)]
    [InlineData(5, 5, 32)]
    public void Bc1ByteCountUsesFourByFourBlocks(int width, int height, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc1Rgba.GetByteCount(width, height));
    }

    [Theory]
    [MemberData(nameof(PvrtcByteCountCases))]
    public void PvrtcByteCountUsesPvrtcStorageRules(TextureFormat format, int width, int height, int expectedRowBytes, int expectedBytes)
    {
        Assert.Equal(expectedRowBytes, format.GetRowByteCount(width));
        Assert.Equal(expectedBytes, format.GetByteCount(width, height));
    }

    [Fact]
    public void PvrtcIByteCountRejectsNonPowerOfTwoDimensions()
    {
        Assert.Throws<ArgumentException>(() => TextureFormats.RgbPvrtcI4BppUNorm.GetRowByteCount(127));
        Assert.Throws<ArgumentException>(() => TextureFormats.RgbPvrtcI4BppUNorm.GetByteCount(127, 129));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RowByteCountRejectsInvalidWidth(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureFormats.Rgba8UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ByteCountRejectsInvalidHeight(int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureFormats.Rgba8UNorm.GetByteCount(1, height));
    }

    [Theory]
    [MemberData(nameof(SrgbFormats))]
    public void SrgbFormatNamesUseSrgbSuffix(TextureFormat format, string name)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureValueKind.Srgb, format.ValueKind);
    }

    [Theory]
    [MemberData(nameof(EacNormFormats))]
    public void EacFormatNamesUseNormSuffix(TextureFormat format, string name, TextureValueKind valueKind)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.BlockCompressed, format.Kind);
        Assert.Equal(valueKind, format.ValueKind);
    }

    [Theory]
    [MemberData(nameof(VulkanUncompressedFormats))]
    public void VulkanUncompressedFormatMetadataIsStable(
        TextureFormat format,
        string name,
        TextureComponents components,
        TextureValueKind valueKind,
        int bitsPerBlock,
        int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(components, format.Components);
        Assert.Equal(valueKind, format.ValueKind);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
    }

    [Theory]
    [MemberData(nameof(ConsolePackedFormats))]
    public void ConsolePackedFormatMetadataIsStable(
        TextureFormat format,
        string name,
        TextureComponents components,
        TextureValueKind valueKind,
        int channelCount,
        int bitsPerBlock,
        int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(components, format.Components);
        Assert.Equal(valueKind, format.ValueKind);
        Assert.Equal(channelCount, format.ChannelCount);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.Rgbm), "RGBM")]
    [InlineData(nameof(TextureFormats.Rgbd), "RGBD")]
    public void RgbmAndRgbdFormatsAreFloatRgbaByteEncoded(string formatName, string name)
    {
        var format = formatName == nameof(TextureFormats.Rgbm)
            ? TextureFormats.Rgbm
            : TextureFormats.Rgbd;

        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(TextureComponents.Rgba, format.Components);
        Assert.Equal(TextureValueKind.Float, format.ValueKind);
        Assert.Equal(4, format.ChannelCount);
        Assert.Equal(32, format.BitsPerBlock);
        Assert.Equal(4, format.BytesPerBlock);
        Assert.Equal(28, format.GetRowByteCount(7));
    }

    [Theory]
    [MemberData(nameof(PackedRgb422Formats))]
    public void PackedRgb422FormatsDescribeTwoPixelBlocks(TextureFormat format, string name, int bitsPerBlock, int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(TextureComponents.Rgb, format.Components);
        Assert.Equal(TextureValueKind.UNorm, format.ValueKind);
        Assert.Equal(2, format.BlockWidth);
        Assert.Equal(1, format.BlockHeight);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
        Assert.Equal(bytesPerBlock * 3, format.GetRowByteCount(6));
    }

    [Theory]
    [MemberData(nameof(PackedYuv422Formats))]
    public void PackedYuv422FormatsDescribeTwoPixelBlocks(TextureFormat format, string name, int bitsPerBlock, int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(TextureComponents.Yuv, format.Components);
        Assert.Equal(TextureValueKind.UNorm, format.ValueKind);
        Assert.Equal(3, format.ChannelCount);
        Assert.Equal(2, format.BlockWidth);
        Assert.Equal(1, format.BlockHeight);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
        Assert.Equal(bytesPerBlock * 3, format.GetRowByteCount(6));
    }

    [Theory]
    [MemberData(nameof(PackedYuva444Formats))]
    public void PackedYuva444FormatsDescribeSinglePixelBlocks(TextureFormat format, string name, int bitsPerBlock, int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(TextureComponents.Yuva, format.Components);
        Assert.Equal(TextureValueKind.UNorm, format.ValueKind);
        Assert.Equal(4, format.ChannelCount);
        Assert.Equal(1, format.BlockWidth);
        Assert.Equal(1, format.BlockHeight);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
        Assert.Equal(bytesPerBlock * 7, format.GetRowByteCount(7));
    }

    [Fact]
    public void PlanarYuvFormatsAreVariableSizePayloads()
    {
        var format = TextureFormats.Yuv3P420UNorm;

        Assert.Equal("YUV_3P_420_UNORM", format.Name);
        Assert.Equal(TextureComponents.Yuv, format.Components);
        Assert.True(format.IsVariableSize);
        Assert.Throws<NotSupportedException>(() => format.GetRowByteCount(4));
        Assert.Throws<NotSupportedException>(() => format.GetByteCount(4, 4));
    }

    [Fact]
    public void StencilSubByteFormatsDescribePackedBlocks()
    {
        Assert.Equal(8, TextureFormats.StencilIndex1.BlockWidth);
        Assert.Equal(8, TextureFormats.StencilIndex1.BitsPerBlock);
        Assert.Equal(2, TextureFormats.StencilIndex1.GetRowByteCount(9));

        Assert.Equal(2, TextureFormats.StencilIndex4.BlockWidth);
        Assert.Equal(8, TextureFormats.StencilIndex4.BitsPerBlock);
        Assert.Equal(2, TextureFormats.StencilIndex4.GetRowByteCount(3));
    }

    [Theory]
    [MemberData(nameof(XrFormats))]
    public void XrFormatMetadataIsStable(
        TextureFormat format,
        string name,
        TextureComponents components,
        TextureValueKind valueKind,
        int channelCount,
        int bitsPerBlock,
        int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(components, format.Components);
        Assert.Equal(valueKind, format.ValueKind);
        Assert.Equal(channelCount, format.ChannelCount);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
    }

    [Theory]
    [MemberData(nameof(XStencilFormats))]
    public void XStencilFormatsDescribeStencilViews(TextureFormat format, string name, int bitsPerBlock, int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(TextureComponents.Stencil, format.Components);
        Assert.Equal(TextureValueKind.UInt, format.ValueKind);
        Assert.Equal(1, format.ChannelCount);
        Assert.Equal(8, format.RedBits);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
    }

    [Theory]
    [MemberData(nameof(IndexedFormats))]
    public void IndexedFormatsDescribePackedIndexStorage(
        TextureFormat format,
        string name,
        TextureComponents components,
        int channelCount,
        int bitsPerBlock,
        int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(TextureFormatKind.Uncompressed, format.Kind);
        Assert.Equal(components, format.Components);
        Assert.Equal(TextureValueKind.UNorm, format.ValueKind);
        Assert.Equal(channelCount, format.ChannelCount);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
    }

    public static TheoryData<TextureFormat, string, TextureFormatKind, TextureComponents, int, int, int> MvpFormats() => new()
    {
        { TextureFormats.R8, "R8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.R, 1, 8, 1 },
        { TextureFormats.Rg8, "RG8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Rg, 2, 16, 2 },
        { TextureFormats.Rgb8, "RGB8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Rgb, 3, 24, 3 },
        { TextureFormats.Bgr565UNorm, "BGR565_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Bgr, 3, 16, 2 },
        { TextureFormats.Rgba8UNorm, "RGBA8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Rgba, 4, 32, 4 },
        { TextureFormats.Argb4UNorm, "ARGB4_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Argb, 4, 16, 2 },
        { TextureFormats.Abgr4UNorm, "ABGR4_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Abgr, 4, 16, 2 },
        { TextureFormats.Bgra8, "BGRA8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Bgra, 4, 32, 4 },
        { TextureFormats.Luminance4Alpha4UNorm, "LUMINANCE4_ALPHA4_UNORM", TextureFormatKind.Uncompressed, TextureComponents.LuminanceAlpha, 2, 8, 1 },
        { TextureFormats.Bc1Rgb, "BC1_RGB_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgb, 3, 64, 8 },
        { TextureFormats.Bc1Rgba, "BC1_RGBA_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 64, 8 },
        { TextureFormats.Bc4UNorm, "BC4_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.R, 1, 64, 8 },
        { TextureFormats.Bc5UNorm, "BC5_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rg, 2, 128, 16 },
        { TextureFormats.Latc2UNorm, "LATC2_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.LuminanceAlpha, 2, 128, 16 },
        { TextureFormats.Dxt3A, "DXT3A", TextureFormatKind.BlockCompressed, TextureComponents.Alpha, 1, 64, 8 },
        { TextureFormats.Dxt3A1111, "DXT3A_1111", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 64, 8 },
        { TextureFormats.Dxt5A, "DXT5A", TextureFormatKind.BlockCompressed, TextureComponents.Alpha, 1, 64, 8 },
        { TextureFormats.Dxn, "DXN", TextureFormatKind.BlockCompressed, TextureComponents.Rg, 2, 128, 16 },
        { TextureFormats.Ctx1, "CTX1", TextureFormatKind.BlockCompressed, TextureComponents.Rg, 2, 64, 8 },
        { TextureFormats.RgbEtc1UNorm, "RGB_ETC1_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgb, 3, 64, 8 },
        { TextureFormats.RgbEtc2UNorm, "RGB_ETC2_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgb, 3, 64, 8 },
        { TextureFormats.RgbA1Etc2UNorm, "RGB_A1_ETC2_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 64, 8 },
        { TextureFormats.RgbaEtc2EacUNorm, "RGBA_ETC2_EAC_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 128, 16 },
        { TextureFormats.R11EacUNorm, "R11_EAC_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.R, 1, 64, 8 },
        { TextureFormats.Rg11EacUNorm, "RG11_EAC_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rg, 2, 128, 16 },
        { TextureFormats.RgbFxt1UNorm, "RGB_FXT1_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgb, 3, 128, 16 },
        { TextureFormats.RgbaFxt1UNorm, "RGBA_FXT1_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 128, 16 },
        { TextureFormats.AtcRgb, "ATC_RGB_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgb, 3, 64, 8 },
        { TextureFormats.AtcRgbaInterpolatedAlpha, "ATC_RGBA_INTERPOLATED_ALPHA_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 128, 16 }
    };

    public static TheoryData<TextureFormat, string> SrgbFormats() => new()
    {
        { TextureFormats.Luminance8Srgb, "LUMINANCE8_SRGB" },
        { TextureFormats.Luminance8Alpha8Srgb, "LUMINANCE8_ALPHA8_SRGB" },
        { TextureFormats.R8Srgb, "R8_SRGB" },
        { TextureFormats.Rg8Srgb, "RG8_SRGB" },
        { TextureFormats.Rgb8Srgb, "RGB8_SRGB" },
        { TextureFormats.Bgr8Srgb, "BGR8_SRGB" },
        { TextureFormats.Rgba8Srgb, "RGBA8_SRGB" },
        { TextureFormats.Abgr8Srgb, "ABGR8_SRGB" },
        { TextureFormats.Bgra8Srgb, "BGRA8_SRGB" },
        { TextureFormats.Bgrx8Srgb, "BGRX8_SRGB" },
        { TextureFormats.Bc1RgbSrgb, "BC1_RGB_SRGB" },
        { TextureFormats.Bc1RgbaSrgb, "BC1_RGBA_SRGB" },
        { TextureFormats.Bc2RgbaSrgb, "BC2_RGBA_SRGB" },
        { TextureFormats.Bc3RgbaSrgb, "BC3_RGBA_SRGB" },
        { TextureFormats.Dxt1RgbSrgb, "RGB_DXT1_SRGB" },
        { TextureFormats.Dxt1RgbaSrgb, "RGBA_DXT1_SRGB" },
        { TextureFormats.Dxt3RgbaSrgb, "RGBA_DXT3_SRGB" },
        { TextureFormats.Dxt5RgbaSrgb, "RGBA_DXT5_SRGB" },
        { TextureFormats.RgbPvrtcI2BppSrgb, "RGB_PVRTC1_2BPP_SRGB" },
        { TextureFormats.RgbaPvrtcI2BppSrgb, "RGBA_PVRTC1_2BPP_SRGB" },
        { TextureFormats.RgbPvrtcI4BppSrgb, "RGB_PVRTC1_4BPP_SRGB" },
        { TextureFormats.RgbaPvrtcI4BppSrgb, "RGBA_PVRTC1_4BPP_SRGB" },
        { TextureFormats.RgbaPvrtcII2BppSrgb, "RGBA_PVRTC2_2BPP_SRGB" },
        { TextureFormats.RgbaPvrtcII4BppSrgb, "RGBA_PVRTC2_4BPP_SRGB" },
        { TextureFormats.RgbEtc2Srgb, "RGB_ETC2_SRGB" },
        { TextureFormats.RgbA1Etc2Srgb, "RGB_A1_ETC2_SRGB" },
        { TextureFormats.RgbaEtc2EacSrgb, "RGBA_ETC2_EAC_SRGB" },
        { TextureFormats.RgbaBasisEtc1sSrgb, "RGBA_BASIS_ETC1S_SRGB" },
        { TextureFormats.RgbaAstc4x4Srgb, "RGBA_ASTC_4X4_SRGB" },
        { TextureFormats.RgbaAstc5x4Srgb, "RGBA_ASTC_5X4_SRGB" },
        { TextureFormats.RgbaAstc5x5Srgb, "RGBA_ASTC_5X5_SRGB" },
        { TextureFormats.RgbaAstc6x5Srgb, "RGBA_ASTC_6X5_SRGB" },
        { TextureFormats.RgbaAstc6x6Srgb, "RGBA_ASTC_6X6_SRGB" },
        { TextureFormats.RgbaAstc8x5Srgb, "RGBA_ASTC_8X5_SRGB" },
        { TextureFormats.RgbaAstc8x6Srgb, "RGBA_ASTC_8X6_SRGB" },
        { TextureFormats.RgbaAstc8x8Srgb, "RGBA_ASTC_8X8_SRGB" },
        { TextureFormats.RgbaAstc10x5Srgb, "RGBA_ASTC_10X5_SRGB" },
        { TextureFormats.RgbaAstc10x6Srgb, "RGBA_ASTC_10X6_SRGB" },
        { TextureFormats.RgbaAstc10x8Srgb, "RGBA_ASTC_10X8_SRGB" },
        { TextureFormats.RgbaAstc10x10Srgb, "RGBA_ASTC_10X10_SRGB" },
        { TextureFormats.RgbaAstc12x10Srgb, "RGBA_ASTC_12X10_SRGB" },
        { TextureFormats.RgbaAstc12x12Srgb, "RGBA_ASTC_12X12_SRGB" }
    };

    public static TheoryData<TextureFormat, string, TextureValueKind> EacNormFormats() => new()
    {
        { TextureFormats.R11EacUNorm, "R11_EAC_UNORM", TextureValueKind.UNorm },
        { TextureFormats.R11EacSNorm, "R11_EAC_SNORM", TextureValueKind.SNorm },
        { TextureFormats.Rg11EacUNorm, "RG11_EAC_UNORM", TextureValueKind.UNorm },
        { TextureFormats.Rg11EacSNorm, "RG11_EAC_SNORM", TextureValueKind.SNorm }
    };

    public static TheoryData<TextureFormat, int, int, int, int> PvrtcByteCountCases() => new()
    {
        { TextureFormats.RgbPvrtcI4BppUNorm, 1, 1, 16, 32 },
        { TextureFormats.RgbPvrtcI4BppUNorm, 4, 4, 16, 32 },
        { TextureFormats.RgbPvrtcI4BppUNorm, 8, 8, 16, 32 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 1, 1, 16, 32 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 16, 8, 16, 32 },
        { TextureFormats.RgbPvrtcI6BppFloat, 1, 1, 32, 64 },
        { TextureFormats.RgbPvrtcI6BppFloat, 32, 32, 96, 768 },
        { TextureFormats.RgbPvrtcI8BppFloat, 1, 1, 32, 64 },
        { TextureFormats.RgbaPvrtcII4BppUNorm, 1, 1, 8, 8 },
        { TextureFormats.RgbaPvrtcII4BppUNorm, 127, 129, 256, 8448 },
        { TextureFormats.RgbaPvrtcII2BppUNorm, 1, 1, 8, 8 },
        { TextureFormats.RgbaPvrtcII2BppUNorm, 127, 129, 128, 4224 },
        { TextureFormats.RgbPvrtcII6BppFloat, 1, 1, 16, 16 },
        { TextureFormats.RgbPvrtcII8BppFloat, 1, 1, 16, 16 }
    };

    public static TheoryData<TextureFormat, string, int, int> PackedRgb422Formats() => new()
    {
        { TextureFormats.R8G8B8G8_422UNorm, "R8G8_B8G8_422_UNORM", 32, 4 },
        { TextureFormats.G8R8G8B8_422UNorm, "G8R8_G8B8_422_UNORM", 32, 4 },
        { TextureFormats.G8B8G8R8_422UNorm, "G8B8_G8R8_422_UNORM", 32, 4 },
        { TextureFormats.B8G8R8G8_422UNorm, "B8G8_R8G8_422_UNORM", 32, 4 },
        { TextureFormats.G10X6B10X6G10X6R10X6_422UNorm, "G10X6B10X6G10X6R10X6_422_UNORM_4PACK16", 64, 8 },
        { TextureFormats.B10X6G10X6R10X6G10X6_422UNorm, "B10X6G10X6R10X6G10X6_422_UNORM_4PACK16", 64, 8 },
        { TextureFormats.G12X4B12X4G12X4R12X4_422UNorm, "G12X4B12X4G12X4R12X4_422_UNORM_4PACK16", 64, 8 },
        { TextureFormats.B12X4G12X4R12X4G12X4_422UNorm, "B12X4G12X4R12X4G12X4_422_UNORM_4PACK16", 64, 8 },
        { TextureFormats.G16B16G16R16_422UNorm, "G16B16_G16R16_422_UNORM", 64, 8 },
        { TextureFormats.B16G16R16G16_422UNorm, "B16G16_R16G16_422_UNORM", 64, 8 }
    };

    public static TheoryData<TextureFormat, string, TextureComponents, TextureValueKind, int, int> VulkanUncompressedFormats() => new()
    {
        { TextureFormats.R10X6UNorm, "R10X6_UNORM_PACK16", TextureComponents.R, TextureValueKind.UNorm, 16, 2 },
        { TextureFormats.R10X6G10X6UNorm, "R10X6G10X6_UNORM_2PACK16", TextureComponents.Rg, TextureValueKind.UNorm, 32, 4 },
        { TextureFormats.R10X6G10X6B10X6A10X6UNorm, "R10X6G10X6B10X6A10X6_UNORM_4PACK16", TextureComponents.Rgba, TextureValueKind.UNorm, 64, 8 },
        { TextureFormats.R12X4UNorm, "R12X4_UNORM_PACK16", TextureComponents.R, TextureValueKind.UNorm, 16, 2 },
        { TextureFormats.R12X4G12X4UNorm, "R12X4G12X4_UNORM_2PACK16", TextureComponents.Rg, TextureValueKind.UNorm, 32, 4 },
        { TextureFormats.R12X4G12X4B12X4A12X4UNorm, "R12X4G12X4B12X4A12X4_UNORM_4PACK16", TextureComponents.Rgba, TextureValueKind.UNorm, 64, 8 }
    };

    public static TheoryData<TextureFormat, string, TextureComponents, TextureValueKind, int, int, int> ConsolePackedFormats() => new()
    {
        { TextureFormats.Rgb655UNorm, "RGB655_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 16, 2 },
        { TextureFormats.Rg5SNormB6UNormRev, "RG5_SNORM_B6_UNORM_REV", TextureComponents.Rgb, TextureValueKind.SNorm, 3, 16, 2 },
        { TextureFormats.Rgba4RevSNorm, "RGBA4_REV_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 4, 16, 2 },
        { TextureFormats.Rg8SNormB8UNormX8Rev, "RG8_SNORM_B8_UNORM_X8_REV", TextureComponents.Rgb, TextureValueKind.SNorm, 3, 32, 4 },
        { TextureFormats.Rgb10SNormA2UNormRev, "RGB10_SNORM_A2_UNORM_REV", TextureComponents.Rgba, TextureValueKind.SNorm, 4, 32, 4 },
        { TextureFormats.R10Gb11UNorm, "R10_GB11_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 32, 4 },
        { TextureFormats.Rg11B10UNorm, "RG11_B10_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 32, 4 },
        { TextureFormats.R10Gb11RevUNorm, "R10_GB11_REV_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 32, 4 },
        { TextureFormats.Rg11B10RevUNorm, "RG11_B10_REV_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 32, 4 },
        { TextureFormats.Rg11B10RevSNorm, "RG11_B10_REV_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 3, 32, 4 },
        { TextureFormats.R10Gb11RevSNorm, "R10_GB11_REV_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 3, 32, 4 },
        { TextureFormats.Depth24FloatStencil8, "D24FS8", TextureComponents.DepthStencil, TextureValueKind.DepthStencil, 2, 32, 4 }
    };

    public static TheoryData<TextureFormat, string, TextureComponents, TextureValueKind, int, int, int> XrFormats() => new()
    {
        { TextureFormats.Bgr10XR, "BGR10_XR", TextureComponents.Bgr, TextureValueKind.XR, 3, 32, 4 },
        { TextureFormats.Bgr10XRSrgb, "BGR10_XR_SRGB", TextureComponents.Bgr, TextureValueKind.XRSrgb, 3, 32, 4 },
        { TextureFormats.Rgb10XRA2UNorm, "RGB10_XR_BIAS_A2_UNORM", TextureComponents.Rgba, TextureValueKind.XR, 4, 32, 4 },
        { TextureFormats.Bgra10XR, "BGRA10_XR", TextureComponents.Bgra, TextureValueKind.XR, 4, 64, 8 },
        { TextureFormats.Bgra10XRSrgb, "BGRA10_XR_SRGB", TextureComponents.Bgra, TextureValueKind.XRSrgb, 4, 64, 8 }
    };

    public static TheoryData<TextureFormat, string, int, int> XStencilFormats() => new()
    {
        { TextureFormats.X32Stencil8, "X32_STENCIL8", 64, 8 },
        { TextureFormats.X24Stencil8, "X24_STENCIL8", 32, 4 }
    };

    public static TheoryData<TextureFormat, string, int, int> PackedYuv422Formats() => new()
    {
        { TextureFormats.Uyvy422UNorm, "UYVY422_UNORM", 32, 4 },
        { TextureFormats.Yuy2UNorm, "YUY2_UNORM", 32, 4 },
        { TextureFormats.Vy1Uy0422UNorm, "VY1UY0422_UNORM", 32, 4 },
        { TextureFormats.Y1Vy0U422UNorm, "Y1VY0U422_UNORM", 32, 4 },
        { TextureFormats.Yuyv16_422UNorm, "YUYV16_422_UNORM", 64, 8 },
        { TextureFormats.Uyvy16_422UNorm, "UYVY16_422_UNORM", 64, 8 },
        { TextureFormats.Yuyv10Msb422UNorm, "YUYV10MSB_422_UNORM", 64, 8 },
        { TextureFormats.Uyvy10Lsb422UNorm, "UYVY10LSB_422_UNORM", 64, 8 }
    };

    public static TheoryData<TextureFormat, string, int, int> PackedYuva444Formats() => new()
    {
        { TextureFormats.Ayuv444UNorm, "AYUV444_UNORM", 32, 4 },
        { TextureFormats.Vyua10Msb444UNorm, "VYUA10MSB_444_UNORM", 64, 8 },
        { TextureFormats.Vyua10Lsb444UNorm, "VYUA10LSB_444_UNORM", 64, 8 },
        { TextureFormats.Vyua12Msb444UNorm, "VYUA12MSB_444_UNORM", 64, 8 },
        { TextureFormats.Vyua12Lsb444UNorm, "VYUA12LSB_444_UNORM", 64, 8 },
        { TextureFormats.Uyv10A2_444UNorm, "UYV10A2_444_UNORM", 32, 4 },
        { TextureFormats.Uyva16_444UNorm, "UYVA16_444_UNORM", 64, 8 }
    };

    public static TheoryData<TextureFormat, string, TextureComponents, int, int, int> IndexedFormats() => new()
    {
        { TextureFormats.Ai44, "AI44", TextureComponents.LuminanceAlpha, 2, 8, 1 },
        { TextureFormats.Ia44, "IA44", TextureComponents.LuminanceAlpha, 2, 8, 1 },
        { TextureFormats.P8, "P8", TextureComponents.Luminance, 1, 8, 1 },
        { TextureFormats.A8P8, "A8P8", TextureComponents.LuminanceAlpha, 2, 16, 2 }
    };
}
