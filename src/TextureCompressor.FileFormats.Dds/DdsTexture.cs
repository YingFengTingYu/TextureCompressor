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
        : this(format, [new TextureSubresource(0, 0, 0, width, height, payload)], 1, 1, headerKind, dxgiFormat, legacyPixelFormat, alphaMode)
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
        : this(format, ToSubresources(mipLevels, "DDS"), 1, 1, headerKind, dxgiFormat, legacyPixelFormat, alphaMode)
    {
    }

    public DdsTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int faceCount)
        : this(format, subresources, arrayLayerCount: 1, faceCount)
    {
    }

    public DdsTexture(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int arrayLayerCount, int faceCount)
        : this(format, subresources, arrayLayerCount, faceCount, DdsHeaderKind.Dxt10, dxgiFormat: null, legacyPixelFormat: null, DdsAlphaMode.Unknown)
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
    {
        var resources = ValidateSubresources(subresources, arrayLayerCount, faceCount, "DDS");

        Format = format;
        Subresources = resources;
        ArrayLayerCount = arrayLayerCount;
        FaceCount = faceCount;
        MipLevelCount = GetMipLevelCount(resources);
        MipLevels = CreateMipLevels(resources, MipLevelCount);
        HeaderKind = headerKind;
        DxgiFormat = dxgiFormat;
        LegacyPixelFormat = legacyPixelFormat;
        AlphaMode = alphaMode;
    }

    public TextureFormat Format { get; }

    public int Width => Subresources[0].Width;

    public int Height => Subresources[0].Height;

    public IReadOnlyList<TextureMipLevel> MipLevels { get; }

    public IReadOnlyList<TextureSubresource> Subresources { get; }

    public int MipLevelCount { get; }

    public int ArrayLayerCount { get; }

    public int FaceCount { get; }

    public bool IsCubeMap => FaceCount == 6;

    public byte[] Payload => Subresources[0].Payload;

    public byte[] Data => Payload;

    public DdsHeaderKind HeaderKind { get; }

    public DdsDxgiFormat? DxgiFormat { get; }

    public DdsLegacyPixelFormat? LegacyPixelFormat { get; }

    public DdsAlphaMode AlphaMode { get; }

    public TextureSubresource GetSubresource(int mipLevel, TextureCubeFace face, int arrayLayer = 0) =>
        GetSubresource(mipLevel, arrayLayer, (int)face);

    public TextureSubresource GetSubresource(int mipLevel, int arrayLayer = 0, int faceIndex = 0)
    {
        if ((uint)mipLevel >= (uint)MipLevelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel));
        }

        if ((uint)arrayLayer >= (uint)ArrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayLayer));
        }

        if ((uint)faceIndex >= (uint)FaceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        }

        return Subresources[GetSubresourceIndex(mipLevel, arrayLayer, faceIndex, MipLevelCount, FaceCount)];
    }

    private static TextureSubresource[] ToSubresources(IReadOnlyList<TextureMipLevel> mipLevels, string containerName)
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException($"{containerName} texture must contain at least one mip level.", nameof(mipLevels));
        }

        var subresources = new TextureSubresource[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var level = mipLevels[i] ?? throw new ArgumentException($"{containerName} mip level cannot be null.", nameof(mipLevels));
            subresources[i] = new TextureSubresource(i, arrayLayer: 0, faceIndex: 0, level.Width, level.Height, level.Payload);
        }

        return subresources;
    }

    private static TextureSubresource[] ValidateSubresources(
        IReadOnlyList<TextureSubresource> subresources,
        int arrayLayerCount,
        int faceCount,
        string containerName)
    {
        ArgumentNullException.ThrowIfNull(subresources);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arrayLayerCount);
        if (faceCount is not (1 or 6))
        {
            throw new ArgumentOutOfRangeException(nameof(faceCount), "Texture face count must be 1 or 6.");
        }

        if (subresources.Count == 0)
        {
            throw new ArgumentException($"{containerName} texture must contain at least one subresource.", nameof(subresources));
        }

        TextureSubresource? baseSubresource = null;
        var mipLevelCount = 0;
        for (var i = 0; i < subresources.Count; i++)
        {
            var subresource = subresources[i] ?? throw new ArgumentException($"{containerName} subresource cannot be null.", nameof(subresources));
            if (subresource.MipLevel == 0 && subresource.ArrayLayer == 0 && subresource.FaceIndex == 0)
            {
                baseSubresource = subresource;
            }

            mipLevelCount = Math.Max(mipLevelCount, subresource.MipLevel + 1);
        }

        if (baseSubresource is null)
        {
            throw new ArgumentException($"{containerName} texture is missing mip level 0 for array layer 0 face 0.", nameof(subresources));
        }

        var fullMipLevelCount = TextureMipLevel.GetFullMipLevelCount(baseSubresource.Width, baseSubresource.Height);
        if (mipLevelCount > fullMipLevelCount)
        {
            throw new ArgumentException($"{containerName} mip level count exceeds the full mip chain for the base dimensions.", nameof(subresources));
        }

        var expectedCount = checked(mipLevelCount * arrayLayerCount * faceCount);
        if (subresources.Count != expectedCount)
        {
            throw new ArgumentException(
                $"{containerName} texture has {subresources.Count} subresources, but {expectedCount} were expected for {arrayLayerCount} array layer(s), {faceCount} face(s), and {mipLevelCount} mip level(s).",
                nameof(subresources));
        }

        var resources = new TextureSubresource[expectedCount];
        for (var i = 0; i < subresources.Count; i++)
        {
            var subresource = subresources[i]!;
            if (subresource.ArrayLayer >= arrayLayerCount)
            {
                throw new ArgumentException(
                    $"{containerName} subresource array layer {subresource.ArrayLayer} is outside the declared array layer count {arrayLayerCount}.",
                    nameof(subresources));
            }

            if (subresource.FaceIndex >= faceCount)
            {
                throw new ArgumentException(
                    $"{containerName} subresource face {subresource.FaceIndex} is outside the declared face count {faceCount}.",
                    nameof(subresources));
            }

            var expectedWidth = TextureMipLevel.GetDimension(baseSubresource.Width, subresource.MipLevel);
            var expectedHeight = TextureMipLevel.GetDimension(baseSubresource.Height, subresource.MipLevel);
            if (subresource.Width != expectedWidth || subresource.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"{containerName} mip level {subresource.MipLevel} is {subresource.Width}x{subresource.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(subresources));
            }

            var index = GetSubresourceIndex(subresource.MipLevel, subresource.ArrayLayer, subresource.FaceIndex, mipLevelCount, faceCount);
            if (resources[index] is not null)
            {
                throw new ArgumentException(
                    $"{containerName} texture contains duplicate subresources for mip level {subresource.MipLevel}, array layer {subresource.ArrayLayer}, face {subresource.FaceIndex}.",
                    nameof(subresources));
            }

            resources[index] = subresource;
        }

        for (var i = 0; i < resources.Length; i++)
        {
            if (resources[i] is null)
            {
                throw new ArgumentException($"{containerName} texture is missing one or more subresources.", nameof(subresources));
            }
        }

        return resources;
    }

    private static TextureMipLevel[] CreateMipLevels(IReadOnlyList<TextureSubresource> subresources, int mipLevelCount)
    {
        var mipLevels = new TextureMipLevel[mipLevelCount];
        for (var i = 0; i < mipLevelCount; i++)
        {
            var subresource = subresources[i];
            mipLevels[i] = new TextureMipLevel(subresource.Width, subresource.Height, subresource.Payload);
        }

        return mipLevels;
    }

    private static int GetMipLevelCount(IReadOnlyList<TextureSubresource> subresources) =>
        subresources.Max(static subresource => subresource.MipLevel) + 1;

    private static int GetSubresourceIndex(int mipLevel, int arrayLayer, int faceIndex, int mipLevelCount, int faceCount) =>
        checked((((arrayLayer * faceCount) + faceIndex) * mipLevelCount) + mipLevel);
}
