using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Options;
using TextureCompressor.Registry;

namespace TextureCompressor.Conversion;

public sealed class TextureAssembler
{
    private readonly TextureCoderManager _coders;

    public TextureAssembler()
        : this(TextureCoderManager.Global)
    {
    }

    public TextureAssembler(TextureCoderManager coders)
    {
        ArgumentNullException.ThrowIfNull(coders);

        _coders = coders;
    }

    public TextureImage CreateArray(
        TextureFormat format,
        IReadOnlyList<IBitmap<Rgba8UNorm>> layers,
        TextureCompressionLevel? compressionLevel = null)
    {
        ArgumentNullException.ThrowIfNull(layers);
        EnsureImageCount(layers, minimumCount: 1, "Texture array");
        EnsureSameDimensions(layers, "Texture array layer");

        using var compressionRegistration = TextureCompressionRegistrationFactory.Create(_coders, format, compressionLevel);
        var subresources = new TextureSubresource[layers.Count];
        for (var layer = 0; layer < layers.Count; layer++)
        {
            subresources[layer] = EncodeSubresource(format, layers[layer], mipLevel: 0, arrayLayer: layer, faceIndex: 0);
        }

        return new TextureImage(format, subresources, arrayLayerCount: layers.Count, faceCount: 1);
    }

    public TextureImage CreateArrayMipChain(
        TextureFormat format,
        IReadOnlyList<IBitmap<Rgba8UNorm>> layers,
        TextureCompressionLevel? compressionLevel = null,
        MipmapGenerationOptions? mipmapOptions = null)
    {
        ArgumentNullException.ThrowIfNull(layers);
        EnsureImageCount(layers, minimumCount: 1, "Texture array");
        EnsureSameDimensions(layers, "Texture array layer");

        var mipLevelChains = GenerateMipLevelChains(format, layers, mipmapOptions);
        return CreateTexture(format, mipLevelChains, arrayLayerCount: layers.Count, faceCount: 1, compressionLevel);
    }

    public TextureImage CreateCube(
        TextureFormat format,
        IReadOnlyList<IBitmap<Rgba8UNorm>> faces,
        TextureCompressionLevel? compressionLevel = null)
    {
        ArgumentNullException.ThrowIfNull(faces);
        EnsureCubeFaces(faces);

        using var compressionRegistration = TextureCompressionRegistrationFactory.Create(_coders, format, compressionLevel);
        var subresources = new TextureSubresource[faces.Count];
        for (var face = 0; face < faces.Count; face++)
        {
            subresources[face] = EncodeSubresource(format, faces[face], mipLevel: 0, arrayLayer: 0, face);
        }

        return new TextureImage(format, subresources, arrayLayerCount: 1, faceCount: 6);
    }

    public TextureImage CreateCubeMipChain(
        TextureFormat format,
        IReadOnlyList<IBitmap<Rgba8UNorm>> faces,
        TextureCompressionLevel? compressionLevel = null,
        MipmapGenerationOptions? mipmapOptions = null)
    {
        ArgumentNullException.ThrowIfNull(faces);
        EnsureCubeFaces(faces);

        var mipLevelChains = GenerateMipLevelChains(format, faces, mipmapOptions);
        return CreateTexture(format, mipLevelChains, arrayLayerCount: 1, faceCount: 6, compressionLevel);
    }

    public TextureImage CreateMipChain(
        TextureFormat format,
        IBitmap<Rgba8UNorm> source,
        TextureCompressionLevel? compressionLevel = null,
        MipmapGenerationOptions? mipmapOptions = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return CreateMipChain(
            format,
            BitmapMipChain.Generate(source, TextureMipmapGenerationOptions.GetDefault(format, mipmapOptions)),
            compressionLevel);
    }

    public TextureImage CreateMipChain(
        TextureFormat format,
        IReadOnlyList<IBitmap<Rgba8UNorm>> mipLevels,
        TextureCompressionLevel? compressionLevel = null)
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        EnsureImageCount(mipLevels, minimumCount: 1, "Mip chain");

        var baseLevel = mipLevels[0];
        var fullMipLevelCount = TextureImage.GetFullMipLevelCount(baseLevel.Width, baseLevel.Height);
        if (mipLevels.Count > fullMipLevelCount)
        {
            throw new ArgumentException("Mip chain contains more images than the full mip chain for the base dimensions.", nameof(mipLevels));
        }

        using var compressionRegistration = TextureCompressionRegistrationFactory.Create(_coders, format, compressionLevel);
        var subresources = new TextureSubresource[mipLevels.Count];
        for (var mipLevel = 0; mipLevel < mipLevels.Count; mipLevel++)
        {
            var image = mipLevels[mipLevel];
            var expectedWidth = TextureImage.GetMipDimension(baseLevel.Width, mipLevel);
            var expectedHeight = TextureImage.GetMipDimension(baseLevel.Height, mipLevel);
            if (image.Width != expectedWidth || image.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"Mip level {mipLevel} is {image.Width}x{image.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(mipLevels));
            }

            subresources[mipLevel] = EncodeSubresource(format, image, mipLevel, arrayLayer: 0, faceIndex: 0);
        }

        return new TextureImage(format, subresources, arrayLayerCount: 1, faceCount: 1);
    }

    private TextureImage CreateTexture(
        TextureFormat format,
        IReadOnlyList<IReadOnlyList<IBitmap<Rgba8UNorm>>> mipLevelChains,
        int arrayLayerCount,
        int faceCount,
        TextureCompressionLevel? compressionLevel)
    {
        var mipLevelCount = mipLevelChains[0].Count;
        var subresources = new TextureSubresource[checked(mipLevelCount * arrayLayerCount * faceCount)];

        using var compressionRegistration = TextureCompressionRegistrationFactory.Create(_coders, format, compressionLevel);
        for (var layer = 0; layer < arrayLayerCount; layer++)
        {
            for (var face = 0; face < faceCount; face++)
            {
                var chain = mipLevelChains[(layer * faceCount) + face];
                for (var mipLevel = 0; mipLevel < mipLevelCount; mipLevel++)
                {
                    var index = TextureImage.GetSubresourceIndex(mipLevel, layer, face, mipLevelCount, arrayLayerCount, faceCount);
                    subresources[index] = EncodeSubresource(format, chain[mipLevel], mipLevel, layer, face);
                }
            }
        }

        return new TextureImage(format, subresources, arrayLayerCount, faceCount);
    }

    private static IReadOnlyList<IBitmap<Rgba8UNorm>>[] GenerateMipLevelChains(
        TextureFormat format,
        IReadOnlyList<IBitmap<Rgba8UNorm>> images,
        MipmapGenerationOptions? mipmapOptions)
    {
        var options = TextureMipmapGenerationOptions.GetDefault(format, mipmapOptions);
        var mipLevelChains = new IReadOnlyList<IBitmap<Rgba8UNorm>>[images.Count];
        for (var i = 0; i < images.Count; i++)
        {
            mipLevelChains[i] = BitmapMipChain.Generate(images[i], options);
        }

        return mipLevelChains;
    }

    private TextureSubresource EncodeSubresource(
        TextureFormat format,
        IBitmap<Rgba8UNorm> image,
        int mipLevel,
        int arrayLayer,
        int faceIndex)
    {
        ArgumentNullException.ThrowIfNull(image);

        var coder = _coders.GetCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(image.Width, image.Height)];
        coder.Encode(image.AsView(), payload);
        return new TextureSubresource(mipLevel, arrayLayer, faceIndex, image.Width, image.Height, payload);
    }

    private static void EnsureImageCount(IReadOnlyList<IBitmap<Rgba8UNorm>> images, int minimumCount, string description)
    {
        if (images.Count < minimumCount)
        {
            throw new ArgumentException($"{description} requires at least {minimumCount} image(s).", nameof(images));
        }
    }

    private static void EnsureSameDimensions(IReadOnlyList<IBitmap<Rgba8UNorm>> images, string description)
    {
        var firstImage = images[0] ?? throw new ArgumentException($"{description} 0 cannot be null.", nameof(images));
        var width = firstImage.Width;
        var height = firstImage.Height;
        for (var i = 1; i < images.Count; i++)
        {
            var image = images[i] ?? throw new ArgumentException($"{description} {i} cannot be null.", nameof(images));
            if (image.Width != width || image.Height != height)
            {
                throw new ArgumentException(
                    $"{description} {i} is {image.Width}x{image.Height}, but {width}x{height} was expected.",
                    nameof(images));
            }
        }
    }

    private static void EnsureCubeFaces(IReadOnlyList<IBitmap<Rgba8UNorm>> faces)
    {
        if (faces.Count != 6)
        {
            throw new ArgumentException("Cube map requires exactly six faces.", nameof(faces));
        }

        EnsureSameDimensions(faces, "Cube map face");
        var firstFace = faces[0];
        if (firstFace.Width != firstFace.Height)
        {
            throw new ArgumentException("Cube map faces must be square.", nameof(faces));
        }
    }
}
