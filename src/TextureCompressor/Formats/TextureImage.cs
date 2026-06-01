namespace TextureCompressor.Formats;

public class TextureImage
{
    public TextureImage(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, payload, "Texture")
    {
    }

    public TextureImage(TextureFormat format, int width, int height, int depth, byte[] payload)
        : this(format, width, height, depth, payload, "Texture")
    {
    }

    protected TextureImage(TextureFormat format, int width, int height, byte[] payload, string textureDescription)
        : this(format, width, height, depth: 1, payload, textureDescription)
    {
    }

    protected TextureImage(TextureFormat format, int width, int height, int depth, byte[] payload, string textureDescription)
        : this(format, [new TextureSubresource(0, 0, 0, width, height, depth, payload)], arrayLayerCount: 1, faceCount: 1, textureDescription)
    {
    }

    public TextureImage(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int faceCount)
        : this(format, subresources, arrayLayerCount: 1, faceCount)
    {
    }

    public TextureImage(TextureFormat format, IReadOnlyList<TextureSubresource> subresources, int arrayLayerCount, int faceCount)
        : this(format, subresources, arrayLayerCount, faceCount, "Texture")
    {
    }

    protected TextureImage(
        TextureFormat format,
        IReadOnlyList<TextureSubresource> subresources,
        int arrayLayerCount,
        int faceCount,
        string textureDescription)
    {
        var resources = ValidateSubresources(subresources, arrayLayerCount, faceCount, textureDescription);

        Format = format;
        Subresources = resources;
        ArrayLayerCount = arrayLayerCount;
        FaceCount = faceCount;
        MipLevelCount = GetMipLevelCount(resources);
    }

    public TextureFormat Format { get; }

    public int Width => Subresources[0].Width;

    public int Height => Subresources[0].Height;

    public int Depth => Subresources[0].Depth;

    public IReadOnlyList<TextureSubresource> Subresources { get; }

    public int MipLevelCount { get; }

    public int ArrayLayerCount { get; }

    public int FaceCount { get; }

    public bool IsCubeMap => FaceCount == 6;

    public byte[] Payload => Subresources[0].Payload;

    public byte[] Data => Payload;

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

        return Subresources[GetSubresourceIndex(mipLevel, arrayLayer, faceIndex, MipLevelCount, ArrayLayerCount, FaceCount)];
    }

    public static int GetSubresourceIndex(
        int mipLevel,
        int arrayLayer,
        int faceIndex,
        int mipLevelCount,
        int arrayLayerCount,
        int faceCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayLayer);
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mipLevelCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arrayLayerCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(faceCount);

        if (mipLevel >= mipLevelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel));
        }

        if (arrayLayer >= arrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayLayer));
        }

        if (faceIndex >= faceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        }

        return checked((((arrayLayer * faceCount) + faceIndex) * mipLevelCount) + mipLevel);
    }

    public static int GetMipDimension(int baseDimension, int mipLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDimension);
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);

        return Math.Max(1, baseDimension >> mipLevel);
    }

    public static int GetFullMipLevelCount(int width, int height)
        => GetFullMipLevelCount(width, height, depth: 1);

    public static int GetFullMipLevelCount(int width, int height, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var count = 1;
        while (width > 1 || height > 1 || depth > 1)
        {
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
            depth = Math.Max(1, depth >> 1);
            count++;
        }

        return count;
    }

    private static TextureSubresource[] ValidateSubresources(
        IReadOnlyList<TextureSubresource> subresources,
        int arrayLayerCount,
        int faceCount,
        string textureDescription)
    {
        ArgumentNullException.ThrowIfNull(subresources);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arrayLayerCount);
        if (faceCount is not (1 or 6))
        {
            throw new ArgumentOutOfRangeException(nameof(faceCount), "Texture face count must be 1 or 6.");
        }

        if (subresources.Count == 0)
        {
            throw new ArgumentException($"{textureDescription} texture must contain at least one subresource.", nameof(subresources));
        }

        TextureSubresource? baseSubresource = null;
        var mipLevelCount = 0;
        for (var i = 0; i < subresources.Count; i++)
        {
            var subresource = subresources[i] ?? throw new ArgumentException($"{textureDescription} subresource cannot be null.", nameof(subresources));
            if (subresource.MipLevel == 0 && subresource.ArrayLayer == 0 && subresource.FaceIndex == 0)
            {
                baseSubresource = subresource;
            }

            mipLevelCount = Math.Max(mipLevelCount, subresource.MipLevel + 1);
        }

        if (baseSubresource is null)
        {
            throw new ArgumentException($"{textureDescription} texture is missing mip level 0 for array layer 0 face 0.", nameof(subresources));
        }

        var fullMipLevelCount = GetFullMipLevelCount(baseSubresource.Width, baseSubresource.Height, baseSubresource.Depth);
        if (mipLevelCount > fullMipLevelCount)
        {
            throw new ArgumentException($"{textureDescription} mip level count exceeds the full mip chain for the base dimensions.", nameof(subresources));
        }

        var expectedCount = checked(mipLevelCount * arrayLayerCount * faceCount);
        if (subresources.Count != expectedCount)
        {
            throw new ArgumentException(
                $"{textureDescription} texture has {subresources.Count} subresources, but {expectedCount} were expected for {arrayLayerCount} array layer(s), {faceCount} face(s), and {mipLevelCount} mip level(s).",
                nameof(subresources));
        }

        var ordered = new TextureSubresource?[expectedCount];
        for (var i = 0; i < subresources.Count; i++)
        {
            var subresource = subresources[i]!;
            if (subresource.ArrayLayer >= arrayLayerCount)
            {
                throw new ArgumentException(
                    $"{textureDescription} subresource array layer {subresource.ArrayLayer} is outside the declared array layer count {arrayLayerCount}.",
                    nameof(subresources));
            }

            if (subresource.FaceIndex >= faceCount)
            {
                throw new ArgumentException(
                    $"{textureDescription} subresource face {subresource.FaceIndex} is outside the declared face count {faceCount}.",
                    nameof(subresources));
            }

            var expectedWidth = GetMipDimension(baseSubresource.Width, subresource.MipLevel);
            var expectedHeight = GetMipDimension(baseSubresource.Height, subresource.MipLevel);
            var expectedDepth = GetMipDimension(baseSubresource.Depth, subresource.MipLevel);
            if (subresource.Width != expectedWidth || subresource.Height != expectedHeight || subresource.Depth != expectedDepth)
            {
                throw new ArgumentException(
                    $"{textureDescription} mip level {subresource.MipLevel} is {subresource.Width}x{subresource.Height}x{subresource.Depth}, but {expectedWidth}x{expectedHeight}x{expectedDepth} was expected.",
                    nameof(subresources));
            }

            var index = GetSubresourceIndex(subresource.MipLevel, subresource.ArrayLayer, subresource.FaceIndex, mipLevelCount, arrayLayerCount, faceCount);
            if (ordered[index] is not null)
            {
                throw new ArgumentException(
                    $"{textureDescription} texture contains duplicate subresources for mip level {subresource.MipLevel}, array layer {subresource.ArrayLayer}, face {subresource.FaceIndex}.",
                    nameof(subresources));
            }

            ordered[index] = subresource;
        }

        var resources = new TextureSubresource[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            resources[i] = ordered[i] ?? throw new ArgumentException($"{textureDescription} texture is missing one or more subresources.", nameof(subresources));
        }

        return resources;
    }

    private static int GetMipLevelCount(IReadOnlyList<TextureSubresource> subresources) =>
        subresources.Max(static subresource => subresource.MipLevel) + 1;
}
