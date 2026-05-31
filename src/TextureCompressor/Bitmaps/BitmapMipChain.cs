using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public static class BitmapMipChain
{
    public static IReadOnlyList<IBitmap<TPixel>> Generate<TPixel>(
        IBitmap<TPixel> source,
        MipmapGenerationOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(source);

        return Generate(source.AsView(), options);
    }

    public static IReadOnlyList<IBitmap<TPixel>> Generate<TPixel>(
        BitmapView<TPixel> source,
        MipmapGenerationOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        options = ValidateOptions(options);
        var maxLevelCount = GetGeneratedLevelCount(source.Width, source.Height, options);
        var current = new ArrayBitmap<TPixel>(source.Width, source.Height);
        source.Pixels.CopyTo(current.PixelSpan);

        var levels = new List<IBitmap<TPixel>>(maxLevelCount)
        {
            current
        };

        while (levels.Count < maxLevelCount && (current.Width > 1 || current.Height > 1))
        {
            var next = Downsample(current, options);
            levels.Add(next);
            current = next;
        }

        return levels;
    }

    public static ArrayBitmap<TPixel> Downsample<TPixel>(
        IBitmap<TPixel> source,
        MipmapGenerationOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(source);

        return Downsample(source.AsView(), options);
    }

    public static ArrayBitmap<TPixel> Downsample<TPixel>(
        BitmapView<TPixel> source,
        MipmapGenerationOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        options = ValidateOptions(options);
        if (source.Width == 1 && source.Height == 1)
        {
            throw new ArgumentException("Cannot downsample a 1x1 bitmap.", nameof(source));
        }

        var destination = new ArrayBitmap<TPixel>(
            Math.Max(1, source.Width >> 1),
            Math.Max(1, source.Height >> 1));

        Downsample(source, destination.AsView(), options);
        return destination;
    }

    private static void Downsample<TPixel>(
        BitmapView<TPixel> source,
        BitmapView<TPixel> destination,
        MipmapGenerationOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var y = 0; y < destination.Height; y++)
        {
            for (var x = 0; x < destination.Width; x++)
            {
                destination[x, y] = SampleBox(source, x * 2, y * 2, options);
            }
        }
    }

    private static TPixel SampleBox<TPixel>(
        BitmapView<TPixel> source,
        int sourceX,
        int sourceY,
        MipmapGenerationOptions options)
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
                var pixelRed = DecodeColor(pixel.Red, options.ColorSpace);
                var pixelGreen = DecodeColor(pixel.Green, options.ColorSpace);
                var pixelBlue = DecodeColor(pixel.Blue, options.ColorSpace);
                if (options.AlphaMode == MipmapAlphaMode.Premultiplied)
                {
                    pixelRed *= pixel.Alpha;
                    pixelGreen *= pixel.Alpha;
                    pixelBlue *= pixel.Alpha;
                }

                red += pixelRed;
                green += pixelGreen;
                blue += pixelBlue;
                alpha += pixel.Alpha;
                sampleCount++;
            }
        }

        var averageAlpha = alpha / sampleCount;
        if (options.AlphaMode == MipmapAlphaMode.Premultiplied)
        {
            if (alpha > 0f)
            {
                red /= alpha;
                green /= alpha;
                blue /= alpha;
            }
        }
        else
        {
            red /= sampleCount;
            green /= sampleCount;
            blue /= sampleCount;
        }

        return TPixel.FromRgba32Float(new Rgba32Float(
            EncodeColor(red, options.ColorSpace),
            EncodeColor(green, options.ColorSpace),
            EncodeColor(blue, options.ColorSpace),
            averageAlpha));
    }

    private static MipmapGenerationOptions ValidateOptions(MipmapGenerationOptions? options)
    {
        options ??= MipmapGenerationOptions.Default;
        if (options.MaxLevelCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum mip level count must be greater than zero.");
        }

        _ = options.ColorSpace switch
        {
            MipmapColorSpace.Linear or MipmapColorSpace.Srgb => true,
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported mip-map color space '{options.ColorSpace}'.")
        };

        _ = options.AlphaMode switch
        {
            MipmapAlphaMode.Premultiplied or MipmapAlphaMode.Straight => true,
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported mip-map alpha mode '{options.AlphaMode}'.")
        };

        return options;
    }

    private static int GetGeneratedLevelCount(int width, int height, MipmapGenerationOptions options)
    {
        var fullMipLevelCount = GetFullMipLevelCount(width, height);
        return options.MaxLevelCount is { } maxLevelCount
            ? Math.Min(maxLevelCount, fullMipLevelCount)
            : fullMipLevelCount;
    }

    private static float DecodeColor(float value, MipmapColorSpace colorSpace) =>
        colorSpace switch
        {
            MipmapColorSpace.Linear => value,
            MipmapColorSpace.Srgb => SrgbToLinear(value),
            _ => throw new ArgumentOutOfRangeException(nameof(colorSpace))
        };

    private static float EncodeColor(float value, MipmapColorSpace colorSpace) =>
        colorSpace switch
        {
            MipmapColorSpace.Linear => value,
            MipmapColorSpace.Srgb => LinearToSrgb(value),
            _ => throw new ArgumentOutOfRangeException(nameof(colorSpace))
        };

    private static float SrgbToLinear(float value)
    {
        value = Saturate(value);
        return value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    private static float LinearToSrgb(float value)
    {
        value = Saturate(value);
        return value <= 0.0031308f
            ? value * 12.92f
            : (1.055f * MathF.Pow(value, 1f / 2.4f)) - 0.055f;
    }

    private static float Saturate(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        return Math.Clamp(value, 0f, 1f);
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
