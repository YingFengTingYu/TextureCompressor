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

    public static readonly TextureFormat Alpha16UNorm =
        TextureFormat.Uncompressed("ALPHA16_UNORM", TextureComponents.Alpha, TextureValueKind.UNorm, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha16SNorm =
        TextureFormat.Uncompressed("ALPHA16_SNORM", TextureComponents.Alpha, TextureValueKind.SNorm, 0, alphaBits: 16);

    public static readonly TextureFormat Alpha32UNorm =
        TextureFormat.Uncompressed("ALPHA32_UNORM", TextureComponents.Alpha, TextureValueKind.UNorm, 0, alphaBits: 32);

    public static readonly TextureFormat Alpha32SNorm =
        TextureFormat.Uncompressed("ALPHA32_SNORM", TextureComponents.Alpha, TextureValueKind.SNorm, 0, alphaBits: 32);

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

    public static readonly TextureFormat Luminance16UNorm =
        TextureFormat.Uncompressed("LUMINANCE16_UNORM", TextureComponents.Luminance, TextureValueKind.UNorm, 16);

    public static readonly TextureFormat Luminance32UNorm =
        TextureFormat.Uncompressed("LUMINANCE32_UNORM", TextureComponents.Luminance, TextureValueKind.UNorm, 32);

    public static readonly TextureFormat Luminance32SNorm =
        TextureFormat.Uncompressed("LUMINANCE32_SNORM", TextureComponents.Luminance, TextureValueKind.SNorm, 32);

    public static readonly TextureFormat Luminance16Float =
        TextureFormat.Uncompressed("LUMINANCE16_FLOAT", TextureComponents.Luminance, TextureValueKind.Float, 16);

    public static readonly TextureFormat Luminance32Float =
        TextureFormat.Uncompressed("LUMINANCE32_FLOAT", TextureComponents.Luminance, TextureValueKind.Float, 32);

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

    public static readonly TextureFormat Intensity16UNorm =
        TextureFormat.Uncompressed("INTENSITY16_UNORM", TextureComponents.Intensity, TextureValueKind.UNorm, 16);

    public static readonly TextureFormat Intensity16SNorm =
        TextureFormat.Uncompressed("INTENSITY16_SNORM", TextureComponents.Intensity, TextureValueKind.SNorm, 16);

    public static readonly TextureFormat Intensity32UNorm =
        TextureFormat.Uncompressed("INTENSITY32_UNORM", TextureComponents.Intensity, TextureValueKind.UNorm, 32);

    public static readonly TextureFormat Intensity32SNorm =
        TextureFormat.Uncompressed("INTENSITY32_SNORM", TextureComponents.Intensity, TextureValueKind.SNorm, 32);

    public static readonly TextureFormat Intensity16Float =
        TextureFormat.Uncompressed("INTENSITY16_FLOAT", TextureComponents.Intensity, TextureValueKind.Float, 16);

    public static readonly TextureFormat Intensity32Float =
        TextureFormat.Uncompressed("INTENSITY32_FLOAT", TextureComponents.Intensity, TextureValueKind.Float, 32);

    public static readonly TextureFormat R8 =
        TextureFormat.Uncompressed("R8_UNORM", TextureComponents.R, TextureValueKind.UNorm, 8);

    public static readonly TextureFormat R8SNorm =
        TextureFormat.Uncompressed("R8_SNORM", TextureComponents.R, TextureValueKind.SNorm, 8);

    public static readonly TextureFormat R16UNorm =
        TextureFormat.Uncompressed("R16_UNORM", TextureComponents.R, TextureValueKind.UNorm, 16);

    public static readonly TextureFormat R16SNorm =
        TextureFormat.Uncompressed("R16_SNORM", TextureComponents.R, TextureValueKind.SNorm, 16);

    public static readonly TextureFormat R32UNorm =
        TextureFormat.Uncompressed("R32_UNORM", TextureComponents.R, TextureValueKind.UNorm, 32);

    public static readonly TextureFormat R32SNorm =
        TextureFormat.Uncompressed("R32_SNORM", TextureComponents.R, TextureValueKind.SNorm, 32);

    public static readonly TextureFormat R16Float =
        TextureFormat.Uncompressed("R16_FLOAT", TextureComponents.R, TextureValueKind.Float, 16);

    public static readonly TextureFormat R32Float =
        TextureFormat.Uncompressed("R32_FLOAT", TextureComponents.R, TextureValueKind.Float, 32);

    public static readonly TextureFormat Rg8 =
        TextureFormat.Uncompressed("RG8_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 8, 8);

    public static readonly TextureFormat Rg8SNorm =
        TextureFormat.Uncompressed("RG8_SNORM", TextureComponents.Rg, TextureValueKind.SNorm, 8, 8);

    public static readonly TextureFormat Rg16UNorm =
        TextureFormat.Uncompressed("RG16_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 16, 16);

    public static readonly TextureFormat Rg16SNorm =
        TextureFormat.Uncompressed("RG16_SNORM", TextureComponents.Rg, TextureValueKind.SNorm, 16, 16);

    public static readonly TextureFormat Rg32UNorm =
        TextureFormat.Uncompressed("RG32_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 32, 32);

    public static readonly TextureFormat Rg32SNorm =
        TextureFormat.Uncompressed("RG32_SNORM", TextureComponents.Rg, TextureValueKind.SNorm, 32, 32);

    public static readonly TextureFormat Rg16Float =
        TextureFormat.Uncompressed("RG16_FLOAT", TextureComponents.Rg, TextureValueKind.Float, 16, 16);

    public static readonly TextureFormat Rg32Float =
        TextureFormat.Uncompressed("RG32_FLOAT", TextureComponents.Rg, TextureValueKind.Float, 32, 32);

    public static readonly TextureFormat Rgb8 =
        TextureFormat.Uncompressed("RGB8_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 8, 8, 8);

    public static readonly TextureFormat Rgb8SNorm =
        TextureFormat.Uncompressed("RGB8_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 8, 8, 8);

    public static readonly TextureFormat Rgb16UNorm =
        TextureFormat.Uncompressed("RGB16_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 16, 16, 16);

    public static readonly TextureFormat Rgb16SNorm =
        TextureFormat.Uncompressed("RGB16_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 16, 16, 16);

    public static readonly TextureFormat Rgb32UNorm =
        TextureFormat.Uncompressed("RGB32_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 32, 32, 32);

    public static readonly TextureFormat Rgb32SNorm =
        TextureFormat.Uncompressed("RGB32_SNORM", TextureComponents.Rgb, TextureValueKind.SNorm, 32, 32, 32);

    public static readonly TextureFormat Rgb16Float =
        TextureFormat.Uncompressed("RGB16_FLOAT", TextureComponents.Rgb, TextureValueKind.Float, 16, 16, 16);

    public static readonly TextureFormat Rgb32Float =
        TextureFormat.Uncompressed("RGB32_FLOAT", TextureComponents.Rgb, TextureValueKind.Float, 32, 32, 32);

    public static readonly TextureFormat Rgb565UNorm =
        TextureFormat.Uncompressed("RGB565_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 5, 6, 5);

    public static readonly TextureFormat Rgba4UNorm =
        TextureFormat.Uncompressed("RGBA4_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 4, 4, 4, 4);

    public static readonly TextureFormat Rgb5A1UNorm =
        TextureFormat.Uncompressed("RGB5_A1_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 5, 5, 5, 1);

    public static readonly TextureFormat Rgb10A2UNorm =
        TextureFormat.Uncompressed("RGB10_A2_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 10, 10, 10, 2);

    public static readonly TextureFormat Rgb10A2UInt =
        TextureFormat.Uncompressed("RGB10_A2_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 10, 10, 10, 2);

    public static readonly TextureFormat Rgb10A2RevUInt =
        TextureFormat.Uncompressed("RGB10_A2_REV_UINT", TextureComponents.Rgba, TextureValueKind.UInt, 10, 10, 10, 2);

    public static readonly TextureFormat Rgba8UNorm =
        TextureFormat.Uncompressed("RGBA8_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba8SNorm =
        TextureFormat.Uncompressed("RGBA8_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 8, 8, 8, 8);

    public static readonly TextureFormat Rgba16UNorm =
        TextureFormat.Uncompressed("RGBA16_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba16SNorm =
        TextureFormat.Uncompressed("RGBA16_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba32UNorm =
        TextureFormat.Uncompressed("RGBA32_UNORM", TextureComponents.Rgba, TextureValueKind.UNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Rgba32SNorm =
        TextureFormat.Uncompressed("RGBA32_SNORM", TextureComponents.Rgba, TextureValueKind.SNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Rgba16Float =
        TextureFormat.Uncompressed("RGBA16_FLOAT", TextureComponents.Rgba, TextureValueKind.Float, 16, 16, 16, 16);

    public static readonly TextureFormat Rgba32Float =
        TextureFormat.Uncompressed("RGBA32_FLOAT", TextureComponents.Rgba, TextureValueKind.Float, 32, 32, 32, 32);

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

    public static readonly TextureFormat Bgra16UNorm =
        TextureFormat.Uncompressed("BGRA16_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra16SNorm =
        TextureFormat.Uncompressed("BGRA16_SNORM", TextureComponents.Bgra, TextureValueKind.SNorm, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra32UNorm =
        TextureFormat.Uncompressed("BGRA32_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra32SNorm =
        TextureFormat.Uncompressed("BGRA32_SNORM", TextureComponents.Bgra, TextureValueKind.SNorm, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra16Float =
        TextureFormat.Uncompressed("BGRA16_FLOAT", TextureComponents.Bgra, TextureValueKind.Float, 16, 16, 16, 16);

    public static readonly TextureFormat Bgra32Float =
        TextureFormat.Uncompressed("BGRA32_FLOAT", TextureComponents.Bgra, TextureValueKind.Float, 32, 32, 32, 32);

    public static readonly TextureFormat Bgra4UNorm =
        TextureFormat.Uncompressed("BGRA4_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 4, 4, 4, 4);

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
}
