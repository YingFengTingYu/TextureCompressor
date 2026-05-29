using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrEncodingOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public PvrPixelFormat? PvrPixelFormat { get; init; }

    public PvrLegacyPixelType? PvrLegacyPixelType { get; init; }

    public PvrLegacyPixelTypePreference LegacyPixelTypePreference { get; init; }

    public bool IsSrgb { get; init; }

    public int Version { get; init; } = 3;
}
