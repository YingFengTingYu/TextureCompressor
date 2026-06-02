using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Codecs;

public interface IBasisEtc1sTextureCoder : ITextureCoder
{
    void ITextureCoder.Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination) =>
        throw CreateVariableLengthException();

    void ITextureCoder.Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination) =>
        throw CreateVariableLengthException();

    int ITextureCoder.GetEncodedByteCount(int width, int height) =>
        throw CreateVariableLengthException();

    void Decode<TPixel>(BasisEtc1sRawPayload source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    BasisEtc1sEncodedPayload Encode<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>;

    private static NotSupportedException CreateVariableLengthException() =>
        new("Basis ETC1S uses variable-length BasisLZ payloads. Cast the coder to IBasisEtc1sTextureCoder and use the Basis ETC1S Encode/Decode overloads.");
}


/// <summary>
/// Encoded BasisLZ/ETC1S data produced by <see cref="BasisEtc1sTextureCoder"/>.
/// </summary>
public sealed class BasisEtc1sEncodedPayload
{
    public BasisEtc1sEncodedPayload(
        int endpointCount,
        ReadOnlyMemory<byte> endpointData,
        int selectorCount,
        ReadOnlyMemory<byte> selectorData,
        ReadOnlyMemory<byte> tablesData,
        ReadOnlyMemory<byte> rgbSliceData,
        ReadOnlyMemory<byte> alphaSliceData = default,
        bool isPFrame = false)
    {
        _ = new BasisEtc1sRawPayload(
            endpointCount,
            endpointData.Span,
            selectorCount,
            selectorData.Span,
            tablesData.Span,
            rgbSliceData.Span,
            alphaSliceData.Span,
            isPFrame);

        EndpointCount = endpointCount;
        EndpointData = endpointData;
        SelectorCount = selectorCount;
        SelectorData = selectorData;
        TablesData = tablesData;
        RgbSliceData = rgbSliceData;
        AlphaSliceData = alphaSliceData;
        IsPFrame = isPFrame;
    }

    public int EndpointCount { get; }

    public ReadOnlyMemory<byte> EndpointData { get; }

    public int SelectorCount { get; }

    public ReadOnlyMemory<byte> SelectorData { get; }

    public ReadOnlyMemory<byte> TablesData { get; }

    public ReadOnlyMemory<byte> RgbSliceData { get; }

    public ReadOnlyMemory<byte> AlphaSliceData { get; }

    public bool IsPFrame { get; }

    public BasisEtc1sRawPayload AsRawPayload() => new(
        EndpointCount,
        EndpointData.Span,
        SelectorCount,
        SelectorData.Span,
        TablesData.Span,
        RgbSliceData.Span,
        AlphaSliceData.Span,
        IsPFrame);
}

/// <summary>
/// Raw BasisLZ/ETC1S data for a single already-selected image level.
/// </summary>
public readonly ref struct BasisEtc1sRawPayload
{
    public BasisEtc1sRawPayload(
        int endpointCount,
        ReadOnlySpan<byte> endpointData,
        int selectorCount,
        ReadOnlySpan<byte> selectorData,
        ReadOnlySpan<byte> tablesData,
        ReadOnlySpan<byte> rgbSliceData,
        ReadOnlySpan<byte> alphaSliceData = default,
        bool isPFrame = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(endpointCount);
        ArgumentOutOfRangeException.ThrowIfNegative(selectorCount);

        if (endpointData.IsEmpty)
        {
            throw new ArgumentException("Basis ETC1S endpoint data cannot be empty.", nameof(endpointData));
        }

        if (selectorData.IsEmpty)
        {
            throw new ArgumentException("Basis ETC1S selector data cannot be empty.", nameof(selectorData));
        }

        if (tablesData.IsEmpty)
        {
            throw new ArgumentException("Basis ETC1S Huffman table data cannot be empty.", nameof(tablesData));
        }

        if (rgbSliceData.IsEmpty)
        {
            throw new ArgumentException("Basis ETC1S RGB slice data cannot be empty.", nameof(rgbSliceData));
        }

        EndpointCount = endpointCount;
        EndpointData = endpointData;
        SelectorCount = selectorCount;
        SelectorData = selectorData;
        TablesData = tablesData;
        RgbSliceData = rgbSliceData;
        AlphaSliceData = alphaSliceData;
        IsPFrame = isPFrame;
    }

    public int EndpointCount { get; }

    public ReadOnlySpan<byte> EndpointData { get; }

    public int SelectorCount { get; }

    public ReadOnlySpan<byte> SelectorData { get; }

    public ReadOnlySpan<byte> TablesData { get; }

    public ReadOnlySpan<byte> RgbSliceData { get; }

    public ReadOnlySpan<byte> AlphaSliceData { get; }

    public bool IsPFrame { get; }
}
