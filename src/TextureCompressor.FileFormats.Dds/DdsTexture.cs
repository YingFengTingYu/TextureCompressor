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
        : this(
            format,
            [new TextureMipLevel(width, height, payload)],
            headerKind,
            dxgiFormat,
            legacyPixelFormat,
            alphaMode)
    {
    }

    public DdsTexture(TextureFormat format, IReadOnlyList<TextureMipLevel> mipLevels)
        : this(format, mipLevels, DdsHeaderKind.Dxt10, dxgiFormat: null, legacyPixelFormat: null, DdsAlphaMode.Unknown)
    {
    }

    public DdsTexture(
        TextureFormat format,
        IReadOnlyList<TextureMipLevel> mipLevels,
        DdsHeaderKind headerKind,
        DdsDxgiFormat? dxgiFormat,
        DdsLegacyPixelFormat? legacyPixelFormat,
        DdsAlphaMode alphaMode)
    {
        var levels = ValidateMipLevels(mipLevels);

        Format = format;
        MipLevels = levels;
        HeaderKind = headerKind;
        DxgiFormat = dxgiFormat;
        LegacyPixelFormat = legacyPixelFormat;
        AlphaMode = alphaMode;
    }

    public TextureFormat Format { get; }

    public int Width => MipLevels[0].Width;

    public int Height => MipLevels[0].Height;

    public IReadOnlyList<TextureMipLevel> MipLevels { get; }

    public int MipLevelCount => MipLevels.Count;

    public byte[] Payload => MipLevels[0].Payload;

    public byte[] Data => Payload;

    public DdsHeaderKind HeaderKind { get; }

    public DdsDxgiFormat? DxgiFormat { get; }

    public DdsLegacyPixelFormat? LegacyPixelFormat { get; }

    public DdsAlphaMode AlphaMode { get; }

    private static TextureMipLevel[] ValidateMipLevels(IReadOnlyList<TextureMipLevel> mipLevels)
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException("DDS texture must contain at least one mip level.", nameof(mipLevels));
        }

        var baseLevel = mipLevels[0] ?? throw new ArgumentException("DDS mip level cannot be null.", nameof(mipLevels));
        var fullMipLevelCount = TextureMipLevel.GetFullMipLevelCount(baseLevel.Width, baseLevel.Height);
        if (mipLevels.Count > fullMipLevelCount)
        {
            throw new ArgumentException("DDS mip level count exceeds the full mip chain for the base dimensions.", nameof(mipLevels));
        }

        var levels = new TextureMipLevel[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var level = mipLevels[i] ?? throw new ArgumentException("DDS mip level cannot be null.", nameof(mipLevels));
            var expectedWidth = TextureMipLevel.GetDimension(baseLevel.Width, i);
            var expectedHeight = TextureMipLevel.GetDimension(baseLevel.Height, i);
            if (level.Width != expectedWidth || level.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"DDS mip level {i} is {level.Width}x{level.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(mipLevels));
            }

            levels[i] = level;
        }

        return levels;
    }
}
