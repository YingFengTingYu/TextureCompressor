using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public interface IBitmap<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    int Width { get; }

    int Height { get; }

    Span<TPixel> PixelSpan { get; }

    BitmapView<TPixel> AsView() => new(PixelSpan, Width, Height);
}
