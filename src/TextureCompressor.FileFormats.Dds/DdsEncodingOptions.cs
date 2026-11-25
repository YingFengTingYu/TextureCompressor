using TextureCompressor.Bitmaps;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Dds;

public sealed class DdsEncodingOptions : IFileFormatOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public bool GenerateMipmaps { get; init; }

    public MipmapGenerationOptions? MipmapOptions { get; init; }

    private DdsHeaderKind? _headerKind;

    public DdsHeaderKind HeaderKind
    {
        get => _headerKind ?? DdsHeaderKind.Dxt10;
        init => _headerKind = value;
    }

    internal DdsHeaderKind? RequestedHeaderKind => _headerKind;

    public DdsDxgiFormat? DxgiFormat { get; init; }

    public DdsLegacyPixelFormat? LegacyPixelFormat { get; init; }

    public DdsAlphaMode AlphaMode { get; init; } = DdsAlphaMode.Unknown;
}
