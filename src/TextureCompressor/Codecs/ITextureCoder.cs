using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public interface ITextureCoder
{
    TextureFormat Format { get; }

    void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    int GetEncodedByteCount(int width, int height);
}
