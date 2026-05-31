using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public static class BitmapMipChain
{
    public static IReadOnlyList<IBitmap<TPixel>> Generate<TPixel>(IBitmap<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(source);

        return Generate(source.AsView());
    }

    public static IReadOnlyList<IBitmap<TPixel>> Generate<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var current = new ArrayBitmap<TPixel>(source.Width, source.Height);
        source.Pixels.CopyTo(current.PixelSpan);

        var levels = new List<IBitmap<TPixel>>(GetFullMipLevelCount(source.Width, source.Height))
        {
            current
        };

        while (current.Width > 1 || current.Height > 1)
        {
            var next = Downsample(current);
            levels.Add(next);
            current = next;
        }

        return levels;
    }

    public static ArrayBitmap<TPixel> Downsample<TPixel>(IBitmap<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(source);

        return Downsample(source.AsView());
    }

    public static ArrayBitmap<TPixel> Downsample<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (source.Width == 1 && source.Height == 1)
        {
            throw new ArgumentException("Cannot downsample a 1x1 bitmap.", nameof(source));
        }

        var destination = new ArrayBitmap<TPixel>(
            Math.Max(1, source.Width >> 1),
            Math.Max(1, source.Height >> 1));

        Downsample(source, destination.AsView());
        return destination;
    }

    private static void Downsample<TPixel>(BitmapView<TPixel> source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var y = 0; y < destination.Height; y++)
        {
            for (var x = 0; x < destination.Width; x++)
            {
                destination[x, y] = SampleBox(source, x * 2, y * 2);
            }
        }
    }

    private static TPixel SampleBox<TPixel>(BitmapView<TPixel> source, int sourceX, int sourceY)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var maxX = Math.Min(sourceX + 2, source.Width);
        var maxY = Math.Min(sourceY + 2, source.Height);
        var red = 0f;
        var green = 0f;
        var blue = 0f;
        var alpha = 0f;
        var sampleCount = 0;

        for (var y = sourceY; y < maxY; y++)
        {
            for (var x = sourceX; x < maxX; x++)
            {
                var pixel = TPixel.ToRgba32Float(source[x, y]);
                red += pixel.Red * pixel.Alpha;
                green += pixel.Green * pixel.Alpha;
                blue += pixel.Blue * pixel.Alpha;
                alpha += pixel.Alpha;
                sampleCount++;
            }
        }

        var averageAlpha = alpha / sampleCount;
        if (alpha > 0f)
        {
            red /= alpha;
            green /= alpha;
            blue /= alpha;
        }

        return TPixel.FromRgba32Float(new Rgba32Float(red, green, blue, averageAlpha));
    }

    private static int GetFullMipLevelCount(int width, int height)
    {
        var count = 1;
        while (width > 1 || height > 1)
        {
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
            count++;
        }

        return count;
    }
}
