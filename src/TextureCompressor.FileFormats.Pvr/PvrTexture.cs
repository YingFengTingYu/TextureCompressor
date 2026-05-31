using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrTexture
{
    public PvrTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, [])
    {
    }

    public PvrTexture(TextureFormat format, int width, int height, byte[] payload, IReadOnlyList<PvrMetadata> metadata)
        : this(format, [new TextureMipLevel(width, height, payload)], metadata)
    {
    }

    public PvrTexture(TextureFormat format, IReadOnlyList<TextureMipLevel> mipLevels)
        : this(format, mipLevels, [])
    {
    }

    public PvrTexture(TextureFormat format, IReadOnlyList<TextureMipLevel> mipLevels, IReadOnlyList<PvrMetadata> metadata)
    {
        var levels = ValidateMipLevels(mipLevels);
        ArgumentNullException.ThrowIfNull(metadata);

        Format = format;
        MipLevels = levels;
        Metadata = metadata;
    }

    public TextureFormat Format { get; }

    public int Width => MipLevels[0].Width;

    public int Height => MipLevels[0].Height;

    public IReadOnlyList<TextureMipLevel> MipLevels { get; }

    public int MipLevelCount => MipLevels.Count;

    public byte[] Payload => MipLevels[0].Payload;

    public byte[] Data => Payload;

    public IReadOnlyList<PvrMetadata> Metadata { get; }

    private static TextureMipLevel[] ValidateMipLevels(IReadOnlyList<TextureMipLevel> mipLevels)
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException("PVR texture must contain at least one mip level.", nameof(mipLevels));
        }

        var baseLevel = mipLevels[0] ?? throw new ArgumentException("PVR mip level cannot be null.", nameof(mipLevels));
        var fullMipLevelCount = TextureMipLevel.GetFullMipLevelCount(baseLevel.Width, baseLevel.Height);
        if (mipLevels.Count > fullMipLevelCount)
        {
            throw new ArgumentException("PVR mip level count exceeds the full mip chain for the base dimensions.", nameof(mipLevels));
        }

        var levels = new TextureMipLevel[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var level = mipLevels[i] ?? throw new ArgumentException("PVR mip level cannot be null.", nameof(mipLevels));
            var expectedWidth = TextureMipLevel.GetDimension(baseLevel.Width, i);
            var expectedHeight = TextureMipLevel.GetDimension(baseLevel.Height, i);
            if (level.Width != expectedWidth || level.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"PVR mip level {i} is {level.Width}x{level.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(mipLevels));
            }

            levels[i] = level;
        }

        return levels;
    }
}
