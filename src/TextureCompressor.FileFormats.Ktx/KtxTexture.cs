using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Ktx;

public sealed class KtxTexture
{
    public KtxTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, glType: null, glFormat: null, glInternalFormat: null, glBaseInternalFormat: null, vkFormat: null)
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
        : this(
            format,
            [new TextureMipLevel(width, height, payload)],
            glType,
            glFormat,
            glInternalFormat,
            glBaseInternalFormat,
            vkFormat)
    {
    }

    public KtxTexture(TextureFormat format, IReadOnlyList<TextureMipLevel> mipLevels)
        : this(format, mipLevels, glType: null, glFormat: null, glInternalFormat: null, glBaseInternalFormat: null, vkFormat: null)
    {
    }

    public KtxTexture(
        TextureFormat format,
        IReadOnlyList<TextureMipLevel> mipLevels,
        KtxGlFormat? glType,
        KtxGlFormat? glFormat,
        KtxGlFormat? glInternalFormat,
        KtxGlFormat? glBaseInternalFormat)
        : this(format, mipLevels, glType, glFormat, glInternalFormat, glBaseInternalFormat, vkFormat: null)
    {
    }

    public KtxTexture(
        TextureFormat format,
        IReadOnlyList<TextureMipLevel> mipLevels,
        KtxGlFormat? glType,
        KtxGlFormat? glFormat,
        KtxGlFormat? glInternalFormat,
        KtxGlFormat? glBaseInternalFormat,
        KtxVkFormat? vkFormat)
    {
        var levels = ValidateMipLevels(mipLevels);

        Format = format;
        MipLevels = levels;
        GlType = glType;
        GlFormat = glFormat;
        GlInternalFormat = glInternalFormat;
        GlBaseInternalFormat = glBaseInternalFormat;
        VkFormat = vkFormat;
    }

    public TextureFormat Format { get; }

    public int Width => MipLevels[0].Width;

    public int Height => MipLevels[0].Height;

    public IReadOnlyList<TextureMipLevel> MipLevels { get; }

    public int MipLevelCount => MipLevels.Count;

    public byte[] Payload => MipLevels[0].Payload;

    public byte[] Data => Payload;

    public KtxGlFormat? GlType { get; }

    public KtxGlFormat? GlFormat { get; }

    public KtxGlFormat? GlInternalFormat { get; }

    public KtxGlFormat? GlBaseInternalFormat { get; }

    public KtxVkFormat? VkFormat { get; }

    private static TextureMipLevel[] ValidateMipLevels(IReadOnlyList<TextureMipLevel> mipLevels)
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException("KTX texture must contain at least one mip level.", nameof(mipLevels));
        }

        var baseLevel = mipLevels[0] ?? throw new ArgumentException("KTX mip level cannot be null.", nameof(mipLevels));
        var fullMipLevelCount = TextureMipLevel.GetFullMipLevelCount(baseLevel.Width, baseLevel.Height);
        if (mipLevels.Count > fullMipLevelCount)
        {
            throw new ArgumentException("KTX mip level count exceeds the full mip chain for the base dimensions.", nameof(mipLevels));
        }

        var levels = new TextureMipLevel[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var level = mipLevels[i] ?? throw new ArgumentException("KTX mip level cannot be null.", nameof(mipLevels));
            var expectedWidth = TextureMipLevel.GetDimension(baseLevel.Width, i);
            var expectedHeight = TextureMipLevel.GetDimension(baseLevel.Height, i);
            if (level.Width != expectedWidth || level.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"KTX mip level {i} is {level.Width}x{level.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(mipLevels));
            }

            levels[i] = level;
        }

        return levels;
    }
}
