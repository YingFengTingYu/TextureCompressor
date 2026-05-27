using TextureCompressor.Colors;
using TextureCompressor.Images;

namespace TextureCompressor.Bitmaps;

public interface IBitmap<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    int Width { get; }

    int Height { get; }

    Span<TPixel> PixelSpan { get; }

    ImageView<TPixel> AsView() => new(PixelSpan, Width, Height);
}
