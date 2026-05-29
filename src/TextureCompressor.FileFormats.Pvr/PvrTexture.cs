using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrTexture
{
    public PvrTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, [])
    {
    }

    public PvrTexture(TextureFormat format, int width, int height, byte[] payload, IReadOnlyList<PvrMetadata> metadata)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(metadata);

        Format = format;
        Width = width;
        Height = height;
        Payload = payload;
        Metadata = metadata;
    }

    public TextureFormat Format { get; }

    public int Width { get; }

    public int Height { get; }

    public byte[] Payload { get; }

    public byte[] Data => Payload;

    public IReadOnlyList<PvrMetadata> Metadata { get; }
}
