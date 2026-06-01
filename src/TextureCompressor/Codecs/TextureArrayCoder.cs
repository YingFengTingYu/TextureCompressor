using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs;

internal sealed class TextureArrayCoder(ITextureCoder coder) : ITextureCoder3D
{
    private readonly ITextureCoder _coder = coder ?? throw new ArgumentNullException(nameof(coder));

    public TextureFormat Format => _coder.Format;

    public void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var coder = _coder;
        var sliceByteCount = coder.GetEncodedByteCount(destination.Width, destination.Height);
        ValidateSourceLength(source, GetVolumeByteCount(sliceByteCount, destination.Depth));

        for (var z = 0; z < destination.Depth; z++)
        {
            var sliceOffset = checked(z * sliceByteCount);
            coder.Decode(source.Slice(sliceOffset, sliceByteCount), destination.GetSliceView(z));
        }
    }

    public void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var coder = _coder;
        var sliceByteCount = coder.GetEncodedByteCount(source.Width, source.Height);
        ValidateDestinationLength(destination, GetVolumeByteCount(sliceByteCount, source.Depth));

        for (var z = 0; z < source.Depth; z++)
        {
            var sliceOffset = checked(z * sliceByteCount);
            coder.Encode(source.GetSliceView(z), destination.Slice(sliceOffset, sliceByteCount));
        }
    }

    public int GetEncodedByteCount(int width, int height, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        return GetVolumeByteCount(_coder.GetEncodedByteCount(width, height), depth);
    }

    private static int GetVolumeByteCount(int sliceByteCount, int depth) =>
        checked(sliceByteCount * depth);

    private static void ValidateSourceLength(ReadOnlySpan<byte> source, int requiredByteCount)
    {
        if (source.Length < requiredByteCount)
        {
            throw new ArgumentException("Source span is too small for the encoded 3D texture.", nameof(source));
        }
    }

    private static void ValidateDestinationLength(Span<byte> destination, int requiredByteCount)
    {
        if (destination.Length < requiredByteCount)
        {
            throw new ArgumentException("Destination span is too small for the encoded 3D texture.", nameof(destination));
        }
    }
}
