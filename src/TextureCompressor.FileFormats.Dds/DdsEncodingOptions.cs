using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Dds;

public sealed class DdsEncodingOptions : IFileFormatOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public bool GenerateMipmaps { get; init; }

    public DdsHeaderKind HeaderKind { get; init; } = DdsHeaderKind.Dxt10;

    public DdsDxgiFormat? DxgiFormat { get; init; }

    public DdsLegacyPixelFormat? LegacyPixelFormat { get; init; }

    public DdsAlphaMode AlphaMode { get; init; } = DdsAlphaMode.Unknown;
}
