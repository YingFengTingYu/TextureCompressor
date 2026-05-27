namespace TextureCompressor.Formats;

public static class TextureFormats
{
    public static readonly TextureFormat Alpha4UNorm = new(
        "ALPHA4_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Alpha,
        TextureValueKind.UNorm,
        0,
        0,
        0,
        4,
        2,
        1,
        8);

    public static readonly TextureFormat Alpha8UNorm =
        TextureFormat.Uncompressed("ALPHA8_UNORM", TextureComponents.Alpha, TextureValueKind.UNorm, 0, alphaBits: 8);

    public static readonly TextureFormat Alpha8SNorm =
        TextureFormat.Uncompressed("ALPHA8_SNORM", TextureComponents.Alpha, TextureValueKind.SNorm, 0, alphaBits: 8);

    public static readonly TextureFormat Alpha8UInt =
        TextureFormat.Uncompressed("ALPHA8_UINT", TextureComponents.Alpha, TextureValueKind.UInt, 0, alphaBits: 8);

    public static readonly TextureFormat Alpha8SInt =
        TextureFormat.Uncompressed("ALPHA8_SINT", TextureComponents.Alpha, TextureValueKind.SInt, 0, alphaBits: 8);

    public static readonly TextureFormat Alpha12UNorm =
        TextureFormat.Uncompressed("ALPHA12_UNORM", TextureComponents.Alpha, TextureValueKind.UNorm, 0, alphaBits: 12);

    public static readonly TextureFormat Alpha16UNorm =
        TextureFormat.Uncompressed("ALPHA16_UNORM", TextureComponents.Alpha, TextureValueKind.UNorm, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha16SNorm =
        TextureFormat.Uncompressed("ALPHA16_SNORM", TextureComponents.Alpha, TextureValueKind.SNorm, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha16UInt =
        TextureFormat.Uncompressed("ALPHA16_UINT", TextureComponents.Alpha, TextureValueKind.UInt, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha16SInt =
        TextureFormat.Uncompressed("ALPHA16_SINT", TextureComponents.Alpha, TextureValueKind.SInt, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha32UNorm =
        TextureFormat.Uncompressed("ALPHA32_UNORM", TextureComponents.Alpha, TextureValueKind.UNorm, 0, alphaBits: 32);

    public static readonly TextureFormat Alpha32SNorm =
        TextureFormat.Uncompressed("ALPHA32_SNORM", TextureComponents.Alpha, TextureValueKind.SNorm, 0, alphaBits: 32);

    public static readonly TextureFormat Alpha32UInt =
        TextureFormat.Uncompressed("ALPHA32_UINT", TextureComponents.Alpha, TextureValueKind.UInt, 0, alphaBits: 32);

    public static readonly TextureFormat Alpha32SInt =
        TextureFormat.Uncompressed("ALPHA32_SINT", TextureComponents.Alpha, TextureValueKind.SInt, 0, alphaBits: 32);

    public static readonly TextureFormat Alpha16Float =
        TextureFormat.Uncompressed("ALPHA16_FLOAT", TextureComponents.Alpha, TextureValueKind.Float, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha32Float =
        TextureFormat.Uncompressed("ALPHA32_FLOAT", TextureComponents.Alpha, TextureValueKind.Float, 0, alphaBits: 32);

    public static readonly TextureFormat Luminance4UNorm = new(
        "LUMINANCE4_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Luminance,
        TextureValueKind.UNorm,
        4,
        0,
        0,
        0,
        2,
        1,
        8);

    public static readonly TextureFormat Luminance8UNorm =
        TextureFormat.Uncompressed("LUMINANCE8_UNORM", TextureComponents.Luminance, TextureValueKind.UNorm, 8);

    public static readonly TextureFormat Luminance8UInt =
        TextureFormat.Uncompressed("LUMINANCE8_UINT", TextureComponents.Luminance, TextureValueKind.UInt, 8);

    public static readonly TextureFormat Luminance8SInt =
        TextureFormat.Uncompressed("LUMINANCE8_SINT", TextureComponents.Luminance, TextureValueKind.SInt, 8);

    public static readonly TextureFormat Luminance12UNorm =
        TextureFormat.Uncompressed("LUMINANCE12_UNORM", TextureComponents.Luminance, TextureValueKind.UNorm, 12);

    public static readonly TextureFormat Luminance16UNorm =
        TextureFormat.Uncompressed("LUMINANCE16_UNORM", TextureComponents.Luminance, TextureValueKind.UNorm, 16);

    public static readonly TextureFormat Luminance16UInt =
        TextureFormat.Uncompressed("LUMINANCE16_UINT", TextureComponents.Luminance, TextureValueKind.UInt, 16);

    public static readonly TextureFormat Luminance16SInt =
        TextureFormat.Uncompressed("LUMINANCE16_SINT", TextureComponents.Luminance, TextureValueKind.SInt, 16);

    public static readonly TextureFormat Luminance32UNorm =
        TextureFormat.Uncompressed("LUMINANCE32_UNORM", TextureComponents.Luminance, TextureValueKind.UNorm, 32);

    public static readonly TextureFormat Luminance32SNorm =
        TextureFormat.Uncompressed("LUMINANCE32_SNORM", TextureComponents.Luminance, TextureValueKind.SNorm, 32);

    public static readonly TextureFormat Luminance32UInt =
        TextureFormat.Uncompressed("LUMINANCE32_UINT", TextureComponents.Luminance, TextureValueKind.UInt, 32);

    public static readonly TextureFormat Luminance32SInt =
        TextureFormat.Uncompressed("LUMINANCE32_SINT", TextureComponents.Luminance, TextureValueKind.SInt, 32);

    public static readonly TextureFormat Luminance16Float =
        TextureFormat.Uncompressed("LUMINANCE16_FLOAT", TextureComponents.Luminance, TextureValueKind.Float, 16);

    public static readonly TextureFormat Luminance32Float =
        TextureFormat.Uncompressed("LUMINANCE32_FLOAT", TextureComponents.Luminance, TextureValueKind.Float, 32);

    public static readonly TextureFormat Luminance4Alpha4UNorm =
        TextureFormat.Uncompressed("LUMINANCE4_ALPHA4_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 4, alphaBits: 4);

    public static readonly TextureFormat Luminance6Alpha2UNorm =
        TextureFormat.Uncompressed("LUMINANCE6_ALPHA2_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 6, alphaBits: 2);

    public static readonly TextureFormat Luminance8Alpha8UNorm =
        TextureFormat.Uncompressed("LUMINANCE8_ALPHA8_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 8, alphaBits: 8);

    public static readonly TextureFormat Luminance8Alpha8UInt =
        TextureFormat.Uncompressed("LUMINANCE8_ALPHA8_UINT", TextureComponents.LuminanceAlpha, TextureValueKind.UInt, 8, alphaBits: 8);

    public static readonly TextureFormat Luminance8Alpha8SInt =
        TextureFormat.Uncompressed("LUMINANCE8_ALPHA8_SINT", TextureComponents.LuminanceAlpha, TextureValueKind.SInt, 8, alphaBits: 8);

    public static readonly TextureFormat Luminance12Alpha4UNorm =
        TextureFormat.Uncompressed("LUMINANCE12_ALPHA4_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 12, alphaBits: 4);

    public static readonly TextureFormat Luminance12Alpha12UNorm =
        TextureFormat.Uncompressed("LUMINANCE12_ALPHA12_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 12, alphaBits: 12);

    public static readonly TextureFormat Luminance16Alpha16UNorm =
        TextureFormat.Uncompressed("LUMINANCE16_ALPHA16_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 16, alphaBits: 16);

    public static readonly TextureFormat Luminance16Alpha16SNorm =
        TextureFormat.Uncompressed("LUMINANCE16_ALPHA16_SNORM", TextureComponents.LuminanceAlpha, TextureValueKind.SNorm, 16, alphaBits: 16);

    public static readonly TextureFormat Luminance16Alpha16UInt =
        TextureFormat.Uncompressed("LUMINANCE16_ALPHA16_UINT", TextureComponents.LuminanceAlpha, TextureValueKind.UInt, 16, alphaBits: 16);

    public static readonly TextureFormat Luminance16Alpha16SInt =
        TextureFormat.Uncompressed("LUMINANCE16_ALPHA16_SINT", TextureComponents.LuminanceAlpha, TextureValueKind.SInt, 16, alphaBits: 16);

    public static readonly TextureFormat Luminance16Alpha16Float =
        TextureFormat.Uncompressed("LUMINANCE16_ALPHA16_FLOAT", TextureComponents.LuminanceAlpha, TextureValueKind.Float, 16, alphaBits: 16);

    public static readonly TextureFormat Luminance32Alpha32UNorm =
        TextureFormat.Uncompressed("LUMINANCE32_ALPHA32_UNORM", TextureComponents.LuminanceAlpha, TextureValueKind.UNorm, 32, alphaBits: 32);

    public static readonly TextureFormat Luminance32Alpha32SNorm =
        TextureFormat.Uncompressed("LUMINANCE32_ALPHA32_SNORM", TextureComponents.LuminanceAlpha, TextureValueKind.SNorm, 32, alphaBits: 32);

    public static readonly TextureFormat Luminance32Alpha32UInt =
        TextureFormat.Uncompressed("LUMINANCE32_ALPHA32_UINT", TextureComponents.LuminanceAlpha, TextureValueKind.UInt, 32, alphaBits: 32);

    public static readonly TextureFormat Luminance32Alpha32SInt =
        TextureFormat.Uncompressed("LUMINANCE32_ALPHA32_SINT", TextureComponents.LuminanceAlpha, TextureValueKind.SInt, 32, alphaBits: 32);

    public static readonly TextureFormat Luminance32Alpha32Float =
        TextureFormat.Uncompressed("LUMINANCE32_ALPHA32_FLOAT", TextureComponents.LuminanceAlpha, TextureValueKind.Float, 32, alphaBits: 32);

    public static readonly TextureFormat Luminance8Srgb =
        TextureFormat.Uncompressed("LUMINANCE8_SRGB", TextureComponents.Luminance, TextureValueKind.Srgb, 8);

    public static readonly TextureFormat Luminance8Alpha8Srgb =
        TextureFormat.Uncompressed("LUMINANCE8_ALPHA8_SRGB", TextureComponents.LuminanceAlpha, TextureValueKind.Srgb, 8, alphaBits: 8);

    public static readonly TextureFormat Intensity4UNorm = new(
        "INTENSITY4_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Intensity,
        TextureValueKind.UNorm,
        4,
        0,
        0,
        0,
        2,
        1,
        8);

    public static readonly TextureFormat Intensity8UNorm =
        TextureFormat.Uncompressed("INTENSITY8_UNORM", TextureComponents.Intensity, TextureValueKind.UNorm, 8);

    public static readonly TextureFormat Intensity8SNorm =
        TextureFormat.Uncompressed("INTENSITY8_SNORM", TextureComponents.Intensity, TextureValueKind.SNorm, 8);

    public static readonly TextureFormat Intensity8UInt =
        TextureFormat.Uncompressed("INTENSITY8_UINT", TextureComponents.Intensity, TextureValueKind.UInt, 8);

    public static readonly TextureFormat Intensity8SInt =
        TextureFormat.Uncompressed("INTENSITY8_SINT", TextureComponents.Intensity, TextureValueKind.SInt, 8);

    public static readonly TextureFormat Intensity12UNorm =
        TextureFormat.Uncompressed("INTENSITY12_UNORM", TextureComponents.Intensity, TextureValueKind.UNorm, 12);

    public static readonly TextureFormat Intensity16UNorm =
        TextureFormat.Uncompressed("INTENSITY16_UNORM", TextureComponents.Intensity, TextureValueKind.UNorm, 16);

    public static readonly TextureFormat Intensity16SNorm =
        TextureFormat.Uncompressed("INTENSITY16_SNORM", TextureComponents.Intensity, TextureValueKind.SNorm, 16);

    public static readonly TextureFormat Intensity16UInt =
        TextureFormat.Uncompressed("INTENSITY16_UINT", TextureComponents.Intensity, TextureValueKind.UInt, 16);

    public static readonly TextureFormat Intensity16SInt =
        TextureFormat.Uncompressed("INTENSITY16_SINT", TextureComponents.Intensity, TextureValueKind.SInt, 16);

    public static readonly TextureFormat Intensity32UNorm =
        TextureFormat.Uncompressed("INTENSITY32_UNORM", TextureComponents.Intensity, TextureValueKind.UNorm, 32);

    public static readonly TextureFormat Intensity32SNorm =
        TextureFormat.Uncompressed("INTENSITY32_SNORM", TextureComponents.Intensity, TextureValueKind.SNorm, 32);

    public static readonly TextureFormat Intensity32UInt =
        TextureFormat.Uncompressed("INTENSITY32_UINT", TextureComponents.Intensity, TextureValueKind.UInt, 32);

    public static readonly TextureFormat Intensity32SInt =
        TextureFormat.Uncompressed("INTENSITY32_SINT", TextureComponents.Intensity, TextureValueKind.SInt, 32);

    public static readonly TextureFormat Intensity16Float =
        TextureFormat.Uncompressed("INTENSITY16_FLOAT", TextureComponents.Intensity, TextureValueKind.Float, 16);

    public static readonly TextureFormat Intensity32Float =
        TextureFormat.Uncompressed("INTENSITY32_FLOAT", TextureComponents.Intensity, TextureValueKind.Float, 32);

    public static readonly TextureFormat R8 =
        TextureFormat.Uncompressed("R8_UNORM", TextureComponents.R, TextureValueKind.UNorm, 8);

    public static readonly TextureFormat R8SNorm =
        TextureFormat.Uncompressed("R8_SNORM", TextureComponents.R, TextureValueKind.SNorm, 8);

    public static readonly TextureFormat R8UInt =
        TextureFormat.Uncompressed("R8_UINT", TextureComponents.R, TextureValueKind.UInt, 8);

    public static readonly TextureFormat R8SInt =
        TextureFormat.Uncompressed("R8_SINT", TextureComponents.R, TextureValueKind.SInt, 8);

    public static readonly TextureFormat R8Srgb =
        TextureFormat.Uncompressed("R8_SRGB", TextureComponents.R, TextureValueKind.Srgb, 8);

    public static readonly TextureFormat R16UNorm =
        TextureFormat.Uncompressed("R16_UNORM", TextureComponents.R, TextureValueKind.UNorm, 16);

    public static readonly TextureFormat R16SNorm =
        TextureFormat.Uncompressed("R16_SNORM", TextureComponents.R, TextureValueKind.SNorm, 16);

    public static readonly TextureFormat R16UInt =
        TextureFormat.Uncompressed("R16_UINT", TextureComponents.R, TextureValueKind.UInt, 16);

    public static readonly TextureFormat R16SInt =
        TextureFormat.Uncompressed("R16_SINT", TextureComponents.R, TextureValueKind.SInt, 16);

    public static readonly TextureFormat R32UNorm =
        TextureFormat.Uncompressed("R32_UNORM", TextureComponents.R, TextureValueKind.UNorm, 32);

    public static readonly TextureFormat R32SNorm =
        TextureFormat.Uncompressed("R32_SNORM", TextureComponents.R, TextureValueKind.SNorm, 32);

    public static readonly TextureFormat R32UInt =
        TextureFormat.Uncompressed("R32_UINT", TextureComponents.R, TextureValueKind.UInt, 32);

    public static readonly TextureFormat R32SInt =
        TextureFormat.Uncompressed("R32_SINT", TextureComponents.R, TextureValueKind.SInt, 32);

    public static readonly TextureFormat R16Float =
        TextureFormat.Uncompressed("R16_FLOAT", TextureComponents.R, TextureValueKind.Float, 16);

    public static readonly TextureFormat R32Float =
        TextureFormat.Uncompressed("R32_FLOAT", TextureComponents.R, TextureValueKind.Float, 32);

    public static readonly TextureFormat Rg8 =
        TextureFormat.Uncompressed("RG8_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 8, 8);

    public static readonly TextureFormat Rg4UNorm =
        TextureFormat.Uncompressed("RG4_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 4, 4);

    public static readonly TextureFormat Rg8SNorm =
        TextureFormat.Uncompressed("RG8_SNORM", TextureComponents.Rg, TextureValueKind.SNorm, 8, 8);

    public static readonly TextureFormat Rg8UInt =
        TextureFormat.Uncompressed("RG8_UINT", TextureComponents.Rg, TextureValueKind.UInt, 8, 8);

    public static readonly TextureFormat Rg8SInt =
        TextureFormat.Uncompressed("RG8_SINT", TextureComponents.Rg, TextureValueKind.SInt, 8, 8);

    public static readonly TextureFormat Rg8Srgb =
        TextureFormat.Uncompressed("RG8_SRGB", TextureComponents.Rg, TextureValueKind.Srgb, 8, 8);

    public static readonly TextureFormat Rg16UNorm =
        TextureFormat.Uncompressed("RG16_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 16, 16);

    public static readonly TextureFormat Rg16SNorm =
        TextureFormat.Uncompressed("RG16_SNORM", TextureComponents.Rg, TextureValueKind.SNorm, 16, 16);

    public static readonly TextureFormat Rg16UInt =
        TextureFormat.Uncompressed("RG16_UINT", TextureComponents.Rg, TextureValueKind.UInt, 16, 16);

    public static readonly TextureFormat Rg16SInt =
        TextureFormat.Uncompressed("RG16_SINT", TextureComponents.Rg, TextureValueKind.SInt, 16, 16);

    public static readonly TextureFormat Rg32UNorm =
        TextureFormat.Uncompressed("RG32_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 32, 32);

    public static readonly TextureFormat Rg32SNorm =
        TextureFormat.Uncompressed("RG32_SNORM", TextureComponents.Rg, TextureValueKind.SNorm, 32, 32);

    public static readonly TextureFormat Rg32UInt =
        TextureFormat.Uncompressed("RG32_UINT", TextureComponents.Rg, TextureValueKind.UInt, 32, 32);

    public static readonly TextureFormat Rg32SInt =
        TextureFormat.Uncompressed("RG32_SINT", TextureComponents.Rg, TextureValueKind.SInt, 32, 32);

    public static readonly TextureFormat Rg16Float =
        TextureFormat.Uncompressed("RG16_FLOAT", TextureComponents.Rg, TextureValueKind.Float, 16, 16);

    public static readonly TextureFormat Rg32Float =
        TextureFormat.Uncompressed("RG32_FLOAT", TextureComponents.Rg, TextureValueKind.Float, 32, 32);

    public static readonly TextureFormat Rgb8 =
        TextureFormat.Uncompressed("RGB8_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 8, 8, 8);

    public static readonly TextureFormat Rgb8SNorm =
        TextureFormat.Uncompressed("RGB8_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 8, 8, 8);

    public static readonly TextureFormat Rgb8UInt =
        TextureFormat.Uncompressed("RGB8_UINT", TextureComponents.Rgb, TextureValueKind.UInt, 8, 8, 8);

    public static readonly TextureFormat Rgb8SInt =
        TextureFormat.Uncompressed("RGB8_SINT", TextureComponents.Rgb, TextureValueKind.SInt, 8, 8, 8);

    public static readonly TextureFormat Rgb8Srgb =
        TextureFormat.Uncompressed("RGB8_SRGB", TextureComponents.Rgb, TextureValueKind.Srgb, 8, 8, 8);

    public static readonly TextureFormat Rgb16UNorm =
        TextureFormat.Uncompressed("RGB16_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 16, 16, 16);

    public static readonly TextureFormat Rgb16SNorm =
        TextureFormat.Uncompressed("RGB16_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 16, 16, 16);

    public static readonly TextureFormat Rgb16UInt =
        TextureFormat.Uncompressed("RGB16_UINT", TextureComponents.Rgb, TextureValueKind.UInt, 16, 16, 16);

    public static readonly TextureFormat Rgb16SInt =
        TextureFormat.Uncompressed("RGB16_SINT", TextureComponents.Rgb, TextureValueKind.SInt, 16, 16, 16);

    public static readonly TextureFormat Rgb32UNorm =
        TextureFormat.Uncompressed("RGB32_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 32, 32, 32);

    public static readonly TextureFormat Rgb32SNorm =
        TextureFormat.Uncompressed("RGB32_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 32, 32, 32);

    public static readonly TextureFormat Rgb32UInt =
        TextureFormat.Uncompressed("RGB32_UINT", TextureComponents.Rgb, TextureValueKind.UInt, 32, 32, 32);

    public static readonly TextureFormat Rgb32SInt =
        TextureFormat.Uncompressed("RGB32_SINT", TextureComponents.Rgb, TextureValueKind.SInt, 32, 32, 32);

    public static readonly TextureFormat Rgb16Float =
        TextureFormat.Uncompressed("RGB16_FLOAT", TextureComponents.Rgb, TextureValueKind.Float, 16, 16, 16);

    public static readonly TextureFormat Rgb32Float =
        TextureFormat.Uncompressed("RGB32_FLOAT", TextureComponents.Rgb, TextureValueKind.Float, 32, 32, 32);

    public static readonly TextureFormat R3G3B2UNorm =
        TextureFormat.Uncompressed("R3_G3_B2_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 3, 2);

    public static readonly TextureFormat R3G3B2RevUNorm =
        TextureFormat.Uncompressed("R3_G3_B2_REV_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 3, 3, 2);

    public static readonly TextureFormat Rgb4UNorm =
        TextureFormat.Uncompressed("RGB4_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 4, 4, 4);

    public static readonly TextureFormat Rgb5UNorm =
        TextureFormat.Uncompressed("RGB5_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 5, 5, 5);

    public static readonly TextureFormat Rgb565UNorm =
        TextureFormat.Uncompressed("RGB565_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 5, 6, 5);

    public static readonly TextureFormat Rgb565RevUNorm =
        TextureFormat.Uncompressed("RGB565_REV_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 5, 6, 5);

    public static readonly TextureFormat Rgb10UNorm =
        TextureFormat.Uncompressed("RGB10_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 10, 10, 10);

    public static readonly TextureFormat Rgb12UNorm =
        TextureFormat.Uncompressed("RGB12_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 12, 12, 12);

    public static readonly TextureFormat R8G8B8G8_422UNorm = new(
        "R8G8_B8G8_422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.UNorm,
        8,
        8,
        8,
        0,
        2,
        1,
        32);

    public static readonly TextureFormat G8R8G8B8_422UNorm = new(
        "G8R8_G8B8_422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.UNorm,
        8,
        8,
        8,
        0,
        2,
        1,
        32);

    public static readonly TextureFormat G8B8G8R8_422UNorm = new(
        "G8B8_G8R8_422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.UNorm,
        8,
        8,
        8,
        0,
        2,
        1,
        32);

    public static readonly TextureFormat B8G8R8G8_422UNorm = new(
        "B8G8_R8G8_422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.UNorm,
        8,
        8,
        8,
        0,
        2,
        1,
        32);

    public static readonly TextureFormat G16B16G16R16_422UNorm = new(
        "G16B16_G16R16_422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.UNorm,
        16,
        16,
        16,
        0,
        2,
        1,
        64);

    public static readonly TextureFormat B16G16R16G16_422UNorm = new(
        "B16G16_R16G16_422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.UNorm,
        16,
        16,
        16,
        0,
        2,
        1,
        64);

    public static readonly TextureFormat Bw1BppUNorm = new(
        "BW1BPP_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.R,
        TextureValueKind.UNorm,
        1,
        0,
        0,
        0,
        8,
        1,
        8);

    public static readonly TextureFormat Uyvy422UNorm = new(
        "UYVY422_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Yuv,
        TextureValueKind.UNorm,
        8,
        8,
        8,
        0,
        2,
        1,
        32);

    public static readonly TextureFormat Yuy2UNorm = new(
        "YUY2_UNORM",
        TextureFormatKind.Uncompressed,
        TextureComponents.Yuv,
        TextureValueKind.UNorm,
        8,
        8,
        8,
        0,
        2,
        1,
        32);

    public static readonly TextureFormat Vyua10Msb444UNorm =
        PackedYuva444("VYUA10MSB_444_UNORM", 10, 10, 10, 10, 64);

    public static readonly TextureFormat Vyua10Lsb444UNorm =
        PackedYuva444("VYUA10LSB_444_UNORM", 10, 10, 10, 10, 64);

    public static readonly TextureFormat Vyua12Msb444UNorm =
        PackedYuva444("VYUA12MSB_444_UNORM", 12, 12, 12, 12, 64);

    public static readonly TextureFormat Vyua12Lsb444UNorm =
        PackedYuva444("VYUA12LSB_444_UNORM", 12, 12, 12, 12, 64);

    public static readonly TextureFormat Uyv10A2_444UNorm =
        PackedYuva444("UYV10A2_444_UNORM", 10, 10, 10, 2, 32);

    public static readonly TextureFormat Uyva16_444UNorm =
        PackedYuva444("UYVA16_444_UNORM", 16, 16, 16, 16, 64);

    public static readonly TextureFormat Yuyv16_422UNorm =
        PackedYuv422("YUYV16_422_UNORM", 16);

    public static readonly TextureFormat Uyvy16_422UNorm =
        PackedYuv422("UYVY16_422_UNORM", 16);

    public static readonly TextureFormat Yuyv10Msb422UNorm =
        PackedYuv422("YUYV10MSB_422_UNORM", 10);

    public static readonly TextureFormat Yuyv10Lsb422UNorm =
        PackedYuv422("YUYV10LSB_422_UNORM", 10);

    public static readonly TextureFormat Uyvy10Msb422UNorm =
        PackedYuv422("UYVY10MSB_422_UNORM", 10);

    public static readonly TextureFormat Uyvy10Lsb422UNorm =
        PackedYuv422("UYVY10LSB_422_UNORM", 10);

    public static readonly TextureFormat Yuyv12Msb422UNorm =
        PackedYuv422("YUYV12MSB_422_UNORM", 12);

    public static readonly TextureFormat Yuyv12Lsb422UNorm =
        PackedYuv422("YUYV12LSB_422_UNORM", 12);

    public static readonly TextureFormat Uyvy12Msb422UNorm =
        PackedYuv422("UYVY12MSB_422_UNORM", 12);

    public static readonly TextureFormat Uyvy12Lsb422UNorm =
        PackedYuv422("UYVY12LSB_422_UNORM", 12);

    public static readonly TextureFormat Yuv3P444UNorm =
        PlanarYuv("YUV_3P_444_UNORM", 8);

    public static readonly TextureFormat Yuv10Msb3P444UNorm =
        PlanarYuv("YUV10MSB_3P_444_UNORM", 10);

    public static readonly TextureFormat Yuv10Lsb3P444UNorm =
        PlanarYuv("YUV10LSB_3P_444_UNORM", 10);

    public static readonly TextureFormat Yuv12Msb3P444UNorm =
        PlanarYuv("YUV12MSB_3P_444_UNORM", 12);

    public static readonly TextureFormat Yuv12Lsb3P444UNorm =
        PlanarYuv("YUV12LSB_3P_444_UNORM", 12);

    public static readonly TextureFormat Yuv16_3P444UNorm =
        PlanarYuv("YUV16_3P_444_UNORM", 16);

    public static readonly TextureFormat Yuv3P422UNorm =
        PlanarYuv("YUV_3P_422_UNORM", 8);

    public static readonly TextureFormat Yuv10Msb3P422UNorm =
        PlanarYuv("YUV10MSB_3P_422_UNORM", 10);

    public static readonly TextureFormat Yuv10Lsb3P422UNorm =
        PlanarYuv("YUV10LSB_3P_422_UNORM", 10);

    public static readonly TextureFormat Yuv12Msb3P422UNorm =
        PlanarYuv("YUV12MSB_3P_422_UNORM", 12);

    public static readonly TextureFormat Yuv12Lsb3P422UNorm =
        PlanarYuv("YUV12LSB_3P_422_UNORM", 12);

    public static readonly TextureFormat Yuv16_3P422UNorm =
        PlanarYuv("YUV16_3P_422_UNORM", 16);

    public static readonly TextureFormat Yuv3P420UNorm =
        PlanarYuv("YUV_3P_420_UNORM", 8);

    public static readonly TextureFormat Yuv10Msb3P420UNorm =
        PlanarYuv("YUV10MSB_3P_420_UNORM", 10);

    public static readonly TextureFormat Yuv10Lsb3P420UNorm =
        PlanarYuv("YUV10LSB_3P_420_UNORM", 10);

    public static readonly TextureFormat Yuv12Msb3P420UNorm =
        PlanarYuv("YUV12MSB_3P_420_UNORM", 12);

    public static readonly TextureFormat Yuv12Lsb3P420UNorm =
        PlanarYuv("YUV12LSB_3P_420_UNORM", 12);

    public static readonly TextureFormat Yuv16_3P420UNorm =
        PlanarYuv("YUV16_3P_420_UNORM", 16);

    public static readonly TextureFormat Yvu3P420UNorm =
        PlanarYuv("YVU_3P_420_UNORM", 8);

    public static readonly TextureFormat Yuv2P422UNorm =
        PlanarYuv("YUV_2P_422_UNORM", 8);

    public static readonly TextureFormat Yuv10Msb2P422UNorm =
        PlanarYuv("YUV10MSB_2P_422_UNORM", 10);

    public static readonly TextureFormat Yuv10Lsb2P422UNorm =
        PlanarYuv("YUV10LSB_2P_422_UNORM", 10);

    public static readonly TextureFormat Yuv12Msb2P422UNorm =
        PlanarYuv("YUV12MSB_2P_422_UNORM", 12);

    public static readonly TextureFormat Yuv12Lsb2P422UNorm =
        PlanarYuv("YUV12LSB_2P_422_UNORM", 12);

    public static readonly TextureFormat Yuv16_2P422UNorm =
        PlanarYuv("YUV16_2P_422_UNORM", 16);

    public static readonly TextureFormat Yuv2P420UNorm =
        PlanarYuv("YUV_2P_420_UNORM", 8);

    public static readonly TextureFormat Yuv10Msb2P420UNorm =
        PlanarYuv("YUV10MSB_2P_420_UNORM", 10);

    public static readonly TextureFormat Yuv10Lsb2P420UNorm =
        PlanarYuv("YUV10LSB_2P_420_UNORM", 10);

    public static readonly TextureFormat Yuv12Msb2P420UNorm =
        PlanarYuv("YUV12MSB_2P_420_UNORM", 12);

    public static readonly TextureFormat Yuv12Lsb2P420UNorm =
        PlanarYuv("YUV12LSB_2P_420_UNORM", 12);

    public static readonly TextureFormat Yuv16_2P420UNorm =
        PlanarYuv("YUV16_2P_420_UNORM", 16);

    public static readonly TextureFormat Yuv2P444UNorm =
        PlanarYuv("YUV_2P_444_UNORM", 8);

    public static readonly TextureFormat Yvu2P444UNorm =
        PlanarYuv("YVU_2P_444_UNORM", 8);

    public static readonly TextureFormat Yuv10Msb2P444UNorm =
        PlanarYuv("YUV10MSB_2P_444_UNORM", 10);

    public static readonly TextureFormat Yuv10Lsb2P444UNorm =
        PlanarYuv("YUV10LSB_2P_444_UNORM", 10);

    public static readonly TextureFormat Yvu10Msb2P444UNorm =
        PlanarYuv("YVU10MSB_2P_444_UNORM", 10);

    public static readonly TextureFormat Yvu10Lsb2P444UNorm =
        PlanarYuv("YVU10LSB_2P_444_UNORM", 10);

    public static readonly TextureFormat Yvu2P422UNorm =
        PlanarYuv("YVU_2P_422_UNORM", 8);

    public static readonly TextureFormat Yvu10Msb2P422UNorm =
        PlanarYuv("YVU10MSB_2P_422_UNORM", 10);

    public static readonly TextureFormat Yvu10Lsb2P422UNorm =
        PlanarYuv("YVU10LSB_2P_422_UNORM", 10);

    public static readonly TextureFormat Yvu2P420UNorm =
        PlanarYuv("YVU_2P_420_UNORM", 8);

    public static readonly TextureFormat Yvu10Msb2P420UNorm =
        PlanarYuv("YVU10MSB_2P_420_UNORM", 10);

    public static readonly TextureFormat Yvu10Lsb2P420UNorm =
        PlanarYuv("YVU10LSB_2P_420_UNORM", 10);

    public static readonly TextureFormat Bgr565UNorm =
        TextureFormat.Uncompressed("BGR565_UNORM", TextureComponents.Bgr, TextureValueKind.UNorm, 5, 6, 5);

    public static readonly TextureFormat Bgr565RevUNorm =
        TextureFormat.Uncompressed("BGR565_REV_UNORM", TextureComponents.Bgr, TextureValueKind.UNorm, 5, 6, 5);

    public static readonly TextureFormat Bgr8UNorm =
        TextureFormat.Uncompressed("BGR8_UNORM", TextureComponents.Bgr, TextureValueKind.UNorm, 8, 8, 8);

    public static readonly TextureFormat Bgr8SNorm =
        TextureFormat.Uncompressed("BGR8_SNORM", TextureComponents.Bgr, TextureValueKind.SNorm, 8, 8, 8);

    public static readonly TextureFormat Bgr8UInt =
        TextureFormat.Uncompressed("BGR8_UINT", TextureComponents.Bgr, TextureValueKind.UInt, 8, 8, 8);

    public static readonly TextureFormat Bgr8SInt =
        TextureFormat.Uncompressed("BGR8_SINT", TextureComponents.Bgr, TextureValueKind.SInt, 8, 8, 8);

    public static readonly TextureFormat Bgr8Srgb =
        TextureFormat.Uncompressed("BGR8_SRGB", TextureComponents.Bgr, TextureValueKind.Srgb, 8, 8, 8);

    public static readonly TextureFormat Bgr16UNorm =
        TextureFormat.Uncompressed("BGR16_UNORM", TextureComponents.Bgr, TextureValueKind.UNorm, 16, 16, 16);

    public static readonly TextureFormat Bgr16SNorm =
        TextureFormat.Uncompressed("BGR16_SNORM", TextureComponents.Bgr, TextureValueKind.SNorm, 16, 16, 16);

    public static readonly TextureFormat Bgr16UInt =
        TextureFormat.Uncompressed("BGR16_UINT", TextureComponents.Bgr, TextureValueKind.UInt, 16, 16, 16);

    public static readonly TextureFormat Bgr16SInt =
        TextureFormat.Uncompressed("BGR16_SINT", TextureComponents.Bgr, TextureValueKind.SInt, 16, 16, 16);

    public static readonly TextureFormat Bgr16Float =
        TextureFormat.Uncompressed("BGR16_FLOAT", TextureComponents.Bgr, TextureValueKind.Float, 16, 16, 16);

    public static readonly TextureFormat Bgr32UNorm =
        TextureFormat.Uncompressed("BGR32_UNORM", TextureComponents.Bgr, TextureValueKind.UNorm, 32, 32, 32);

    public static readonly TextureFormat Bgr32SNorm =
        TextureFormat.Uncompressed("BGR32_SNORM", TextureComponents.Bgr, TextureValueKind.SNorm, 32, 32, 32);

    public static readonly TextureFormat Bgr32UInt =
        TextureFormat.Uncompressed("BGR32_UINT", TextureComponents.Bgr, TextureValueKind.UInt, 32, 32, 32);

    public static readonly TextureFormat Bgr32SInt =
        TextureFormat.Uncompressed("BGR32_SINT", TextureComponents.Bgr, TextureValueKind.SInt, 32, 32, 32);

    public static readonly TextureFormat Bgr32Float =
        TextureFormat.Uncompressed("BGR32_FLOAT", TextureComponents.Bgr, TextureValueKind.Float, 32, 32, 32);

    public static readonly TextureFormat Rgba2UNorm =
        TextureFormat.Uncompressed("RGBA2_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 2, 2, 2, 2);

    public static readonly TextureFormat Rgba4UNorm =
        TextureFormat.Uncompressed("RGBA4_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Rgba4RevUNorm =
        TextureFormat.Uncompressed("RGBA4_REV_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Argb4UNorm =
        TextureFormat.Uncompressed("ARGB4_UNORM", TextureComponents.Argb, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Argb4RevUNorm =
        TextureFormat.Uncompressed("ARGB4_REV_UNORM", TextureComponents.Argb, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Abgr4UNorm =
        TextureFormat.Uncompressed("ABGR4_UNORM", TextureComponents.Abgr, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Abgr4RevUNorm =
        TextureFormat.Uncompressed("ABGR4_REV_UNORM", TextureComponents.Abgr, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Abgr8UNorm =
        TextureFormat.Uncompressed("ABGR8_UNORM", TextureComponents.Abgr, TextureValueKind.UNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Abgr8SNorm =
        TextureFormat.Uncompressed("ABGR8_SNORM", TextureComponents.Abgr, TextureValueKind.SNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Abgr8UInt =
        TextureFormat.Uncompressed("ABGR8_UINT", TextureComponents.Abgr, TextureValueKind.UInt, 8, 8, 8, 8);

    public static readonly TextureFormat Abgr8SInt =
        TextureFormat.Uncompressed("ABGR8_SINT", TextureComponents.Abgr, TextureValueKind.SInt, 8, 8, 8, 8);

    public static readonly TextureFormat Abgr8Srgb =
        TextureFormat.Uncompressed("ABGR8_SRGB", TextureComponents.Abgr, TextureValueKind.Srgb, 8, 8, 8, 8);

    public static readonly TextureFormat Rgb5A1UNorm =
        TextureFormat.Uncompressed("RGB5_A1_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat Rgb5A1RevUNorm =
        TextureFormat.Uncompressed("RGB5_A1_REV_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat A1Rgb5UNorm =
        TextureFormat.Uncompressed("A1_RGB5_UNORM", TextureComponents.Argb, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat A1Rgb5RevUNorm =
        TextureFormat.Uncompressed("A1_RGB5_REV_UNORM", TextureComponents.Argb, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat A1Bgr5UNorm =
        TextureFormat.Uncompressed("A1_BGR5_UNORM", TextureComponents.Abgr, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat A1Bgr5RevUNorm =
        TextureFormat.Uncompressed("A1_BGR5_REV_UNORM", TextureComponents.Abgr, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat Rgb10A2UNorm =
        TextureFormat.Uncompressed("RGB10_A2_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 10, 10, 10, 2);

    public static readonly TextureFormat Rgb10A2RevUNorm =
        TextureFormat.Uncompressed("RGB10_A2_REV_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 10, 10, 10, 2);

    public static readonly TextureFormat Rgb10A2UInt =
        TextureFormat.Uncompressed("RGB10_A2_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 10, 10, 10, 2);

    public static readonly TextureFormat Rgb10A2RevUInt =
        TextureFormat.Uncompressed("RGB10_A2_REV_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 10, 10, 10, 2);

    public static readonly TextureFormat Rgba12UNorm =
        TextureFormat.Uncompressed("RGBA12_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 12, 12, 12, 12);

    public static readonly TextureFormat Rgba8UNorm =
        TextureFormat.Uncompressed("RGBA8_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba8SNorm =
        TextureFormat.Uncompressed("RGBA8_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba8UInt =
        TextureFormat.Uncompressed("RGBA8_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba8SInt =
        TextureFormat.Uncompressed("RGBA8_SINT", TextureComponents.Rgba, TextureValueKind.SInt, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba8Srgb =
        TextureFormat.Uncompressed("RGBA8_SRGB", TextureComponents.Rgba, TextureValueKind.Srgb, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba16UNorm =
        TextureFormat.Uncompressed("RGBA16_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba16SNorm =
        TextureFormat.Uncompressed("RGBA16_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba16UInt =
        TextureFormat.Uncompressed("RGBA16_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba16SInt =
        TextureFormat.Uncompressed("RGBA16_SINT", TextureComponents.Rgba, TextureValueKind.SInt, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba32UNorm =
        TextureFormat.Uncompressed("RGBA32_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Rgba32SNorm =
        TextureFormat.Uncompressed("RGBA32_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Rgba32UInt =
        TextureFormat.Uncompressed("RGBA32_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 32, 32, 32, 32);

    public static readonly TextureFormat Rgba32SInt =
        TextureFormat.Uncompressed("RGBA32_SINT", TextureComponents.Rgba, TextureValueKind.SInt, 32, 32, 32, 32);

    public static readonly TextureFormat Rgba16Float =
        TextureFormat.Uncompressed("RGBA16_FLOAT", TextureComponents.Rgba, TextureValueKind.Float, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba32Float =
        TextureFormat.Uncompressed("RGBA32_FLOAT", TextureComponents.Rgba, TextureValueKind.Float, 32, 32, 32, 32);

    public static readonly TextureFormat Rgbm =
        TextureFormat.Uncompressed("RGBM", TextureComponents.Rgba, TextureValueKind.Float, 8, 8, 8, 8);

    public static readonly TextureFormat Rgbd =
        TextureFormat.Uncompressed("RGBD", TextureComponents.Rgba, TextureValueKind.Float, 8, 8, 8, 8);

    public static readonly TextureFormat R11G11B10Float =
        TextureFormat.Uncompressed("R11G11B10_FLOAT", TextureComponents.Rgb, TextureValueKind.Float, 11, 11, 10);

    public static readonly TextureFormat Rgb9E5 = new(
        "RGB9_E5",
        TextureFormatKind.Uncompressed,
        TextureComponents.Rgb,
        TextureValueKind.Float,
        9,
        9,
        9,
        0,
        1,
        1,
        32);

    public static readonly TextureFormat Bgra8 =
        TextureFormat.Uncompressed("BGRA8_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Bgra8SNorm =
        TextureFormat.Uncompressed("BGRA8_SNORM", TextureComponents.Bgra, TextureValueKind.SNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Bgra8UInt =
        TextureFormat.Uncompressed("BGRA8_UINT", TextureComponents.Bgra, TextureValueKind.UInt, 8, 8, 8, 8);

    public static readonly TextureFormat Bgra8SInt =
        TextureFormat.Uncompressed("BGRA8_SINT", TextureComponents.Bgra, TextureValueKind.SInt, 8, 8, 8, 8);

    public static readonly TextureFormat Bgra8Srgb =
        TextureFormat.Uncompressed("BGRA8_SRGB", TextureComponents.Bgra, TextureValueKind.Srgb, 8, 8, 8, 8);

    public static readonly TextureFormat Bgrx8UNorm =
        TextureFormat.Uncompressed("BGRX8_UNORM", TextureComponents.Bgrx, TextureValueKind.UNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Bgrx8Srgb =
        TextureFormat.Uncompressed("BGRX8_SRGB", TextureComponents.Bgrx, TextureValueKind.Srgb, 8, 8, 8, 8);

    public static readonly TextureFormat Bgra16UNorm =
        TextureFormat.Uncompressed("BGRA16_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra16SNorm =
        TextureFormat.Uncompressed("BGRA16_SNORM", TextureComponents.Bgra, TextureValueKind.SNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra16UInt =
        TextureFormat.Uncompressed("BGRA16_UINT", TextureComponents.Bgra, TextureValueKind.UInt, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra16SInt =
        TextureFormat.Uncompressed("BGRA16_SINT", TextureComponents.Bgra, TextureValueKind.SInt, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra32UNorm =
        TextureFormat.Uncompressed("BGRA32_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra32SNorm =
        TextureFormat.Uncompressed("BGRA32_SNORM", TextureComponents.Bgra, TextureValueKind.SNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra32UInt =
        TextureFormat.Uncompressed("BGRA32_UINT", TextureComponents.Bgra, TextureValueKind.UInt, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra32SInt =
        TextureFormat.Uncompressed("BGRA32_SINT", TextureComponents.Bgra, TextureValueKind.SInt, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra16Float =
        TextureFormat.Uncompressed("BGRA16_FLOAT", TextureComponents.Bgra, TextureValueKind.Float, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra32Float =
        TextureFormat.Uncompressed("BGRA32_FLOAT", TextureComponents.Bgra, TextureValueKind.Float, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra4UNorm =
        TextureFormat.Uncompressed("BGRA4_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Bgra4RevUNorm =
        TextureFormat.Uncompressed("BGRA4_REV_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Bgr5A1UNorm =
        TextureFormat.Uncompressed("BGR5_A1_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat Bgr5A1RevUNorm =
        TextureFormat.Uncompressed("BGR5_A1_REV_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat Bgr10A2RevUNorm =
        TextureFormat.Uncompressed("BGR10_A2_REV_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 10, 10, 10, 2);

    public static readonly TextureFormat Bgr10A2RevUInt =
        TextureFormat.Uncompressed("BGR10_A2_REV_UINT", TextureComponents.Bgra, TextureValueKind.UInt, 10, 10, 10, 2);

    public static readonly TextureFormat Bc1 =
        TextureFormat.BlockCompressed(
            "BC1_UNORM",
            TextureComponents.Rgba,
            TextureValueKind.UNorm,
            5,
            6,
            5,
            1,
            4,
            4,
            64);

    private static TextureFormat PackedYuva444(string name, int yBits, int uBits, int vBits, int alphaBits, int bitsPerBlock) => new(
        name,
        TextureFormatKind.Uncompressed,
        TextureComponents.Yuva,
        TextureValueKind.UNorm,
        yBits,
        uBits,
        vBits,
        alphaBits,
        1,
        1,
        bitsPerBlock);

    private static TextureFormat PackedYuv422(string name, int bitsPerComponent) => new(
        name,
        TextureFormatKind.Uncompressed,
        TextureComponents.Yuv,
        TextureValueKind.UNorm,
        bitsPerComponent,
        bitsPerComponent,
        bitsPerComponent,
        0,
        2,
        1,
        64);

    private static TextureFormat PlanarYuv(string name, int bitsPerComponent) => new(
        name,
        TextureFormatKind.Uncompressed,
        TextureComponents.Yuv,
        TextureValueKind.UNorm,
        bitsPerComponent,
        bitsPerComponent,
        bitsPerComponent,
        0,
        1,
        1,
        bitsPerComponent * 3,
        IsVariableSize: true);
}
