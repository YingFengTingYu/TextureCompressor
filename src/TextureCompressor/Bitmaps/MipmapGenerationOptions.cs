namespace TextureCompressor.Bitmaps;

public sealed class MipmapGenerationOptions
{
    public static MipmapGenerationOptions Default { get; } = new();

    public int? MaxLevelCount { get; init; }

    public MipmapColorSpace ColorSpace { get; init; }

    public MipmapAlphaMode AlphaMode { get; init; }
}
