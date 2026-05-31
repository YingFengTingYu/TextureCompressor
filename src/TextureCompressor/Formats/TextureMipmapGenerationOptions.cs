using TextureCompressor.Bitmaps;

namespace TextureCompressor.Formats;

public static class TextureMipmapGenerationOptions
{
    private static readonly MipmapGenerationOptions STextureSrgbDefault = new()
    {
        ColorSpace = MipmapColorSpace.Srgb
    };

    public static MipmapGenerationOptions GetDefault(TextureFormat format) =>
        UsesSrgbColorSpace(format)
            ? STextureSrgbDefault
            : MipmapGenerationOptions.Default;

    public static MipmapGenerationOptions GetDefault(TextureFormat format, MipmapGenerationOptions? options) =>
        options ?? GetDefault(format);

    public static bool UsesSrgbColorSpace(TextureFormat format) =>
        format.ValueKind is TextureValueKind.Srgb or TextureValueKind.XRSrgb;
}
