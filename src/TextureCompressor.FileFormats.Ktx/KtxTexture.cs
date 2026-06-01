using TextureCompressor.FileFormats;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Ktx;

public sealed class KtxTexture : ITextureFile
{
    public KtxTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, glType: null, glFormat: null, glInternalFormat: null, glBaseInternalFormat: null, vkFormat: null)
    {
    }

    public KtxTexture(TextureFormat format, int width, int height, int depth, byte[] payload)
        : this(new TextureImage(format, width, height, depth, payload), glType: null, glFormat: null, glInternalFormat: null, glBaseInternalFormat: null, vkFormat: null)
    {
    }

    public KtxTexture(
        TextureFormat format,
        int width,
        int height,
        byte[] payload,
        KtxGlFormat? glType,
        KtxGlFormat? glFormat,
        KtxGlFormat? glInternalFormat,
        KtxGlFormat? glBaseInternalFormat)
        : this(format, width, height, payload, glType, glFormat, glInternalFormat, glBaseInternalFormat, vkFormat: null)
    {
    }

    public KtxTexture(
        TextureFormat format,
        int width,
        int height,
        byte[] payload,
        KtxGlFormat? glType,
        KtxGlFormat? glFormat,
        KtxGlFormat? glInternalFormat,
        KtxGlFormat? glBaseInternalFormat,
        KtxVkFormat? vkFormat)
        : this(new TextureImage(format, width, height, payload), glType, glFormat, glInternalFormat, glBaseInternalFormat, vkFormat)
    {
    }

    public KtxTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int faceCount)
        : this(new TextureImage(format, subresources, faceCount))
    {
    }

    public KtxTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int arrayLayerCount, int faceCount)
        : this(new TextureImage(format, subresources, arrayLayerCount, faceCount))
    {
    }

    public KtxTexture(
        TextureFormat format,
        IReadOnlyList<TextureSubresource> subresources,
        int arrayLayerCount,
        int faceCount,
        KtxGlFormat? glType,
        KtxGlFormat? glFormat,
        KtxGlFormat? glInternalFormat,
        KtxGlFormat? glBaseInternalFormat,
        KtxVkFormat? vkFormat)
        : this(new TextureImage(format, subresources, arrayLayerCount, faceCount), glType, glFormat, glInternalFormat, glBaseInternalFormat, vkFormat)
    {
    }

    public KtxTexture(TextureImage texture)
        : this(texture, glType: null, glFormat: null, glInternalFormat: null, glBaseInternalFormat: null, vkFormat: null)
    {
    }

    public KtxTexture(
        TextureImage texture,
        KtxGlFormat? glType,
        KtxGlFormat? glFormat,
        KtxGlFormat? glInternalFormat,
        KtxGlFormat? glBaseInternalFormat,
        KtxVkFormat? vkFormat)
    {
        ArgumentNullException.ThrowIfNull(texture);

        Texture = texture;
        GlType = glType;
        GlFormat = glFormat;
        GlInternalFormat = glInternalFormat;
        GlBaseInternalFormat = glBaseInternalFormat;
        VkFormat = vkFormat;
    }

    public TextureImage Texture { get; }

    public KtxGlFormat? GlType { get; }

    public KtxGlFormat? GlFormat { get; }

    public KtxGlFormat? GlInternalFormat { get; }

    public KtxGlFormat? GlBaseInternalFormat { get; }

    public KtxVkFormat? VkFormat { get; }
}
