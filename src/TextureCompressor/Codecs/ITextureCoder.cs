using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public interface ITextureCoder
{
    TextureFormat Format { get; }

    void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    int GetEncodedByteCount(int width, int height);
}
