using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Codecs;

public interface IPitchTextureCoder3D : ITextureCoder3D
{
    void ITextureCoder3D.Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination) =>
        Decode(source, destination, GetDefaultPitch(destination.Width), GetDefaultSlicePitch(destination.Width, destination.Height));

    void ITextureCoder3D.Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination) =>
        Encode(source, destination, GetDefaultPitch(source.Width), GetDefaultSlicePitch(source.Width, source.Height));

    int ITextureCoder3D.GetEncodedByteCount(int width, int height, int depth) =>
        GetEncodedByteCount(width, height, depth, GetDefaultPitch(width), GetDefaultSlicePitch(width, height));

    void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, int rowPitch, int slicePitch)
        where TPixel : unmanaged, IPixel<TPixel>;

    void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Decode(source, destination, rowPitch, GetDefaultSlicePitch(destination.Width, destination.Height, rowPitch));

    void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, int rowPitch, int slicePitch)
        where TPixel : unmanaged, IPixel<TPixel>;

    void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(source, destination, rowPitch, GetDefaultSlicePitch(source.Width, source.Height, rowPitch));

    int GetDefaultPitch(int width);

    int GetDefaultSlicePitch(int width, int height) =>
        GetDefaultSlicePitch(width, height, GetDefaultPitch(width));

    int GetDefaultSlicePitch(int width, int height, int rowPitch);

    int GetEncodedByteCount(int width, int height, int depth, int rowPitch, int slicePitch);

    int GetEncodedByteCount(int width, int height, int depth, int rowPitch) =>
        GetEncodedByteCount(width, height, depth, rowPitch, GetDefaultSlicePitch(width, height, rowPitch));
}
