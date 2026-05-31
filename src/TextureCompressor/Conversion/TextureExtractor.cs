using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Conversion;

public sealed class TextureExtractor
{
    private readonly TextureCoderManager _coders;

    public TextureExtractor()
        : this(TextureCoderManager.Global)
    {
    }

    public TextureExtractor(TextureCoderManager coders)
    {
        ArgumentNullException.ThrowIfNull(coders);

        _coders = coders;
    }

    public IReadOnlyList<TextureExtractedImage<Rgba8UNorm>> Extract(
        TextureImage texture,
        TextureSubresourceFilter filter = default) =>
        Extract<Rgba8UNorm>(texture, filter);

    public IReadOnlyList<TextureExtractedImage<TPixel>> Extract<TPixel>(
        TextureImage texture,
        TextureSubresourceFilter filter = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        var subresources = SelectSubresources(texture, filter);
        var images = new TextureExtractedImage<TPixel>[subresources.Count];
        for (var i = 0; i < subresources.Count; i++)
        {
            var subresource = subresources[i];
            images[i] = new TextureExtractedImage<TPixel>(
                subresource.MipLevel,
                subresource.ArrayLayer,
                texture.FaceCount == 6 ? (TextureCubeFace)subresource.FaceIndex : null,
                subresource.FaceIndex,
                DecodeSubresource<TPixel>(texture.Format, subresource));
        }

        return images;
    }

    public TextureExtractedImage<Rgba8UNorm> ExtractSubresource(
        TextureImage texture,
        TextureSubresourceSelection selection = default) =>
        ExtractSubresource<Rgba8UNorm>(texture, selection);

    public TextureExtractedImage<TPixel> ExtractSubresource<TPixel>(
        TextureImage texture,
        TextureSubresourceSelection selection = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        ValidateSelection(texture, selection);
        var subresource = texture.GetSubresource(selection.MipLevel, selection.ArrayLayer, selection.FaceIndex);
        return new TextureExtractedImage<TPixel>(
            subresource.MipLevel,
            subresource.ArrayLayer,
            texture.FaceCount == 6 ? (TextureCubeFace)subresource.FaceIndex : null,
            subresource.FaceIndex,
            DecodeSubresource<TPixel>(texture.Format, subresource));
    }

    public ArrayBitmap<Rgba8UNorm> Decode(
        TextureImage texture,
        TextureSubresourceSelection selection = default) =>
        Decode<Rgba8UNorm>(texture, selection);

    public ArrayBitmap<TPixel> Decode<TPixel>(
        TextureImage texture,
        TextureSubresourceSelection selection = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        ValidateSelection(texture, selection);
        return DecodeSubresource<TPixel>(
            texture.Format,
            texture.GetSubresource(selection.MipLevel, selection.ArrayLayer, selection.FaceIndex));
    }

    public static IReadOnlyList<TextureSubresource> SelectSubresources(
        TextureImage texture,
        TextureSubresourceFilter filter = default)
    {
        ArgumentNullException.ThrowIfNull(texture);

        ValidateFilter(texture, filter);
        if (filter.IsDefault)
        {
            return texture.Subresources;
        }

        var selected = new List<TextureSubresource>();
        var faceIndex = filter.FaceIndex;
        foreach (var subresource in texture.Subresources)
        {
            if (filter.MipLevel is { } mipLevel && subresource.MipLevel != mipLevel)
            {
                continue;
            }

            if (filter.ArrayLayer is { } arrayLayer && subresource.ArrayLayer != arrayLayer)
            {
                continue;
            }

            if (faceIndex is { } selectedFaceIndex && subresource.FaceIndex != selectedFaceIndex)
            {
                continue;
            }

            selected.Add(subresource);
        }

        return selected;
    }

    private ArrayBitmap<TPixel> DecodeSubresource<TPixel>(TextureFormat format, TextureSubresource subresource)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var bitmap = new ArrayBitmap<TPixel>(subresource.Width, subresource.Height);
        _coders.GetCoder(format).Decode(subresource.Payload, bitmap.AsView());
        return bitmap;
    }

    private static void ValidateSelection(TextureImage texture, TextureSubresourceSelection selection)
    {
        if ((uint)selection.MipLevel >= (uint)texture.MipLevelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Mip level {selection.MipLevel} is outside the texture mip level count {texture.MipLevelCount}.");
        }

        if ((uint)selection.ArrayLayer >= (uint)texture.ArrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Array layer {selection.ArrayLayer} is outside the texture array layer count {texture.ArrayLayerCount}.");
        }

        if (selection.HasFace && texture.FaceCount != 6)
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "Face selection requires a cube-map texture.");
        }

        if ((uint)selection.FaceIndex >= (uint)texture.FaceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Face index {selection.FaceIndex} is outside the texture face count {texture.FaceCount}.");
        }
    }

    private static void ValidateFilter(TextureImage texture, TextureSubresourceFilter filter)
    {
        if (filter.MipLevel is { } mipLevel && (uint)mipLevel >= (uint)texture.MipLevelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"Mip level {mipLevel} is outside the texture mip level count {texture.MipLevelCount}.");
        }

        if (filter.ArrayLayer is { } arrayLayer && (uint)arrayLayer >= (uint)texture.ArrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"Array layer {arrayLayer} is outside the texture array layer count {texture.ArrayLayerCount}.");
        }

        if (filter.Face is not null && texture.FaceCount != 6)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Face selection requires a cube-map texture.");
        }

        if (filter.FaceIndex is { } faceIndex && (uint)faceIndex >= (uint)texture.FaceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"Face index {faceIndex} is outside the texture face count {texture.FaceCount}.");
        }
    }
}
