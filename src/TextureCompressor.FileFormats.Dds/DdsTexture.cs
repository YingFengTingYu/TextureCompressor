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
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(payload);

        Format = format;
        Width = width;
        Height = height;
        Payload = payload;
        HeaderKind = headerKind;
        DxgiFormat = dxgiFormat;
        LegacyPixelFormat = legacyPixelFormat;
        AlphaMode = alphaMode;
    }

    public TextureFormat Format { get; }

    public int Width { get; }

    public int Height { get; }

    public byte[] Payload { get; }

    public byte[] Data => Payload;

    public DdsHeaderKind HeaderKind { get; }

    public DdsDxgiFormat? DxgiFormat { get; }

    public DdsLegacyPixelFormat? LegacyPixelFormat { get; }

    public DdsAlphaMode AlphaMode { get; }
}
