using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Dds;

public sealed class DdsTexture
{
    public DdsTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, DdsHeaderKind.Dxt10, dxgiFormat: null, legacyPixelFormat: null, DdsAlphaMode.Unknown)
    {
    }

    public DdsTexture(
        TextureFormat format,
        int width,
        int height,
        byte[] payload,
        DdsHeaderKind headerKind,
        DdsDxgiFormat? dxgiFormat,
        DdsLegacyPixelFormat? legacyPixelFormat,
        DdsAlphaMode alphaMode)
        : this(new TextureImage(format, width, height, payload), headerKind, dxgiFormat, legacyPixelFormat, alphaMode)
    {
    }

    public DdsTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int faceCount)
        : this(new TextureImage(format, subresources, faceCount))
    {
    }

    public DdsTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int arrayLayerCount, int faceCount)
        : this(new TextureImage(format, subresources, arrayLayerCount, faceCount))
    {
    }

    public DdsTexture(
        TextureFormat format,
        IReadOnlyList<TextureSubresource> subresources,
        int arrayLayerCount,
        int faceCount,
        DdsHeaderKind headerKind,
        DdsDxgiFormat? dxgiFormat,
        DdsLegacyPixelFormat? legacyPixelFormat,
        DdsAlphaMode alphaMode)
        : this(new TextureImage(format, subresources, arrayLayerCount, faceCount), headerKind, dxgiFormat, legacyPixelFormat, alphaMode)
    {
    }

    public DdsTexture(TextureImage texture)
        : this(texture, DdsHeaderKind.Dxt10, dxgiFormat: null, legacyPixelFormat: null, DdsAlphaMode.Unknown)
    {
    }

    public DdsTexture(
        TextureImage texture,
        DdsHeaderKind headerKind,
        DdsDxgiFormat? dxgiFormat,
        DdsLegacyPixelFormat? legacyPixelFormat,
        DdsAlphaMode alphaMode)
    {
        ArgumentNullException.ThrowIfNull(texture);

        Texture = texture;
        HeaderKind = headerKind;
        DxgiFormat = dxgiFormat;
        LegacyPixelFormat = legacyPixelFormat;
        AlphaMode = alphaMode;
    }

    public TextureImage Texture { get; }

    public DdsHeaderKind HeaderKind { get; }

    public DdsDxgiFormat? DxgiFormat { get; }

    public DdsLegacyPixelFormat? LegacyPixelFormat { get; }

    public DdsAlphaMode AlphaMode { get; }
}
