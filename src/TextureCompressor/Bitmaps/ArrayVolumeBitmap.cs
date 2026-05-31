using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public sealed class ArrayVolumeBitmap<TPixel> : IVolumeBitmap<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int _pixelCount;

    public ArrayVolumeBitmap(int width, int height, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var requiredLength = checked(width * height * depth);

        Width = width;
        Height = height;
        Depth = depth;
        _pixelCount = requiredLength;
        Pixels = new TPixel[requiredLength];
    }

    public ArrayVolumeBitmap(int width, int height, int depth, TPixel[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        ArgumentNullException.ThrowIfNull(pixels);

        var requiredLength = checked(width * height * depth);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException("Pixel array is too small for the volume bitmap dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Depth = depth;
        _pixelCount = requiredLength;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public TPixel[] Pixels { get; }

    public Span<TPixel> PixelSpan => Pixels.AsSpan(0, _pixelCount);

    public VolumeBitmapView<TPixel> AsView() => new(PixelSpan, Width, Height, Depth);
}
