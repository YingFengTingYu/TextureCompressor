using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs;

internal sealed class PitchTextureArrayCoder(IPitchTextureCoder coder) : IPitchTextureCoder3D
{
    private readonly IPitchTextureCoder _coder = coder ?? throw new ArgumentNullException(nameof(coder));

    public TextureFormat Format => _coder.Format;

    public void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Decode(source, destination, GetDefaultPitch(destination.Width), GetDefaultSlicePitch(destination.Width, destination.Height));

    public void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(source, destination, GetDefaultPitch(source.Width), GetDefaultSlicePitch(source.Width, source.Height));

    public int GetEncodedByteCount(int width, int height, int depth) =>
        GetEncodedByteCount(width, height, depth, GetDefaultPitch(width), GetDefaultSlicePitch(width, height));

    public void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, int rowPitch, int slicePitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var coder = _coder;
        var sliceByteCount = coder.GetEncodedByteCount(destination.Width, destination.Height, rowPitch);
        ValidateSlicePitch(slicePitch, sliceByteCount);
        ValidateSourceLength(source, GetVolumeByteCount(slicePitch, destination.Depth));

        for (var z = 0; z < destination.Depth; z++)
        {
            var sliceOffset = checked(z * slicePitch);
            coder.Decode(source.Slice(sliceOffset, slicePitch), destination.GetSliceView(z), rowPitch);
        }
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Decode(source, destination, rowPitch, GetDefaultSlicePitch(destination.Width, destination.Height, rowPitch));

    public void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, int rowPitch, int slicePitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var coder = _coder;
        var sliceByteCount = coder.GetEncodedByteCount(source.Width, source.Height, rowPitch);
        ValidateSlicePitch(slicePitch, sliceByteCount);
        ValidateDestinationLength(destination, GetVolumeByteCount(slicePitch, source.Depth));

        for (var z = 0; z < source.Depth; z++)
        {
            var sliceOffset = checked(z * slicePitch);
            coder.Encode(source.GetSliceView(z), destination.Slice(sliceOffset, slicePitch), rowPitch);
        }
    }

    public void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(source, destination, rowPitch, GetDefaultSlicePitch(source.Width, source.Height, rowPitch));

    public int GetDefaultPitch(int width) => _coder.GetDefaultPitch(width);

    public int GetDefaultSlicePitch(int width, int height) =>
        GetDefaultSlicePitch(width, height, GetDefaultPitch(width));

    public int GetDefaultSlicePitch(int width, int height, int rowPitch) =>
        _coder.GetEncodedByteCount(width, height, rowPitch);

    public int GetEncodedByteCount(int width, int height, int depth, int rowPitch, int slicePitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var sliceByteCount = _coder.GetEncodedByteCount(width, height, rowPitch);
        ValidateSlicePitch(slicePitch, sliceByteCount);
        return GetVolumeByteCount(slicePitch, depth);
    }

    public int GetEncodedByteCount(int width, int height, int depth, int rowPitch) =>
        GetEncodedByteCount(width, height, depth, rowPitch, GetDefaultSlicePitch(width, height, rowPitch));

    private static int GetVolumeByteCount(int slicePitch, int depth) =>
        checked(slicePitch * depth);

    private static void ValidateSlicePitch(int slicePitch, int sliceByteCount)
    {
        if (slicePitch < sliceByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slicePitch), "Slice pitch must be at least the encoded 2D slice byte count.");
        }
    }

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
