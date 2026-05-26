using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Bitmaps;

public sealed class ArrayTextureBitmap<TPixel> : IBitmap<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int _pixelCount;

    public ArrayTextureBitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var requiredLength = checked(width * height);

        Width = width;
        Height = height;
        _pixelCount = requiredLength;
        Pixels = new TPixel[requiredLength];
    }

    public ArrayTextureBitmap(int width, int height, TPixel[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        ArgumentNullException.ThrowIfNull(pixels);

        var requiredLength = checked(width * height);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException("Pixel array is too small for the bitmap dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixelCount = requiredLength;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public TextureFormat Format => TPixel.Format;

    public TPixel[] Pixels { get; }

    public Span<TPixel> PixelSpan => Pixels.AsSpan(0, _pixelCount);

    public ImageView<TPixel> AsView() => new(PixelSpan, Width, Height);
}
