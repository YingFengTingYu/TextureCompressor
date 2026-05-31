using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs;

public interface ITextureCoder3D
{
    TextureFormat Format { get; }

    void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    int GetEncodedByteCount(int width, int height, int depth);
}
