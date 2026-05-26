namespace TextureCompressor.Formats;

public static class TextureFormats
{
    public static readonly TextureFormat R8 =
        TextureFormat.Uncompressed("R8_UNORM", TextureComponents.R, TextureValueKind.UNorm, 8);

    public static readonly TextureFormat Rg8 =
        TextureFormat.Uncompressed("RG8_UNORM", TextureComponents.Rg, TextureValueKind.UNorm, 8, 8);

    public static readonly TextureFormat Rgb8 =
        TextureFormat.Uncompressed("RGB8_UNORM", TextureComponents.Rgb, TextureValueKind.UNorm, 8, 8, 8);

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

    public static readonly TextureFormat Bgra8 =
        TextureFormat.Uncompressed("BGRA8_UNORM", TextureComponents.Bgra, TextureValueKind.UNorm, 8, 8, 8, 8);

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
