using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public ref struct BitmapView<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private Span<TPixel> _pixels;

    public BitmapView(Span<TPixel> pixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var requiredLength = checked(width * height);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException("Pixel span is too small for the bitmap dimensions.", nameof(pixels));
        }

        _pixels = pixels[..requiredLength];
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public Span<TPixel> Pixels => _pixels;

    public Span<TPixel> this[int y] => GetRowSpan(y);

    public ref TPixel this[int x, int y] => ref _pixels[GetIndex(x, y)];

    public Span<TPixel> GetRowSpan(int y)
    {
        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return _pixels.Slice(checked(y * Width), Width);
    }

    public readonly int GetIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return checked(y * Width + x);
    }
}
