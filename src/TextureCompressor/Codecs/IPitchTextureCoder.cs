using TextureCompressor.Colors;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public interface IPitchTextureCoder : ITextureCoder
{
    void ITextureCoder.Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination) =>
        Decode(source, destination, GetDefaultPitch(destination.Width));

    void ITextureCoder.Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination) =>
        Encode(source, destination, GetDefaultPitch(source.Width));

    int ITextureCoder.GetEncodedByteCount(int width, int height) =>
        GetEncodedByteCount(width, height, GetDefaultPitch(width));

    void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>;

    void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>;

    int GetDefaultPitch(int width);

    int GetEncodedByteCount(int width, int height, int rowPitch);
}
