using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public ref struct VolumeBitmapView<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private Span<TPixel> _pixels;

    public VolumeBitmapView(Span<TPixel> pixels, int width, int height, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var requiredLength = checked(width * height * depth);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException("Pixel span is too small for the volume bitmap dimensions.", nameof(pixels));
        }

        _pixels = pixels[..requiredLength];
        Width = width;
        Height = height;
        Depth = depth;
    }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public readonly int SliceLength => checked(Width * Height);

    public Span<TPixel> Pixels => _pixels;

    public BitmapView<TPixel> this[int z] => GetSliceView(z);

    public ref TPixel this[int x, int y, int z] => ref _pixels[GetIndex(x, y, z)];

    public BitmapView<TPixel> GetSliceView(int z) => new(GetSliceSpan(z), Width, Height);

    public Span<TPixel> GetSliceSpan(int z)
    {
        if ((uint)z >= (uint)Depth)
        {
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        return _pixels.Slice(checked(z * SliceLength), SliceLength);
    }

    public Span<TPixel> GetRowSpan(int y, int z)
    {
        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        if ((uint)z >= (uint)Depth)
        {
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        return _pixels.Slice(checked((z * SliceLength) + (y * Width)), Width);
    }

    public readonly int GetIndex(int x, int y, int z)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        if ((uint)z >= (uint)Depth)
        {
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        return checked((z * SliceLength) + (y * Width) + x);
    }
}
