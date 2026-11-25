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

    /// <summary>True when the PVR v3 header declares that stored RGB is already multiplied by alpha.</summary>
    public bool IsPremultiplied { get; init; }

    public IReadOnlyList<PvrMetadata> Metadata { get; }
}
