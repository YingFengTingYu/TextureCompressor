using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public interface IVolumeBitmap<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    int Width { get; }

    int Height { get; }

    int Depth { get; }

    Span<TPixel> PixelSpan { get; }

    VolumeBitmapView<TPixel> AsView() => new(PixelSpan, Width, Height, Depth);

    Span<TPixel> GetSliceSpan(int z) => AsView().GetSliceSpan(z);

    BitmapView<TPixel> GetSliceView(int z) => AsView().GetSliceView(z);
}
