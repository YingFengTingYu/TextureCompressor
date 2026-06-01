using TextureCompressor.FileFormats;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrTexture : ITextureFile
{
    public PvrTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, [])
    {
    }

    public PvrTexture(TextureFormat format, int width, int height, byte[] payload, IReadOnlyList<PvrMetadata> metadata)
        : this(new TextureImage(format, width, height, payload), metadata)
    {
    }

    public PvrTexture(TextureFormat format, int width, int height, int depth, byte[] payload)
        : this(format, width, height, depth, payload, [])
    {
    }

    public PvrTexture(TextureFormat format, int width, int height, int depth, byte[] payload, IReadOnlyList<PvrMetadata> metadata)
        : this(new TextureImage(format, width, height, depth, payload), metadata)
    {
    }

    public PvrTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int faceCount)
        : this(new TextureImage(format, subresources, faceCount))
    {
    }

    public PvrTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int arrayLayerCount, int faceCount)
        : this(new TextureImage(format, subresources, arrayLayerCount, faceCount))
    {
    }

    public PvrTexture(
        TextureFormat format,
        IReadOnlyList<TextureSubresource> subresources,
        int arrayLayerCount,
        int faceCount,
        IReadOnlyList<PvrMetadata> metadata)
        : this(new TextureImage(format, subresources, arrayLayerCount, faceCount), metadata)
    {
    }

    public PvrTexture(TextureImage texture)
        : this(texture, [])
    {
    }

    public PvrTexture(TextureImage texture, IReadOnlyList<PvrMetadata> metadata)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(metadata);

        Texture = texture;
        Metadata = metadata;
    }

    public TextureImage Texture { get; }

    public IReadOnlyList<PvrMetadata> Metadata { get; }
}
