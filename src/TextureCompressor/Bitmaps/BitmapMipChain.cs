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
        switch (options.Filter)
        {
            case MipmapFilter.Box:
                DownsampleBox(source, destination, options);
                break;
            case MipmapFilter.Triangle:
                DownsampleTriangle(source, destination, options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported mip-map filter '{options.Filter}'.");
        }
    }

    private static void DownsampleBox<TPixel>(
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

    private static void DownsampleTriangle<TPixel>(
        BitmapView<TPixel> source,
        BitmapView<TPixel> destination,
        MipmapGenerationOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var scaleX = source.Width / (float)destination.Width;
        var scaleY = source.Height / (float)destination.Height;
        var radiusX = Math.Max(1f, scaleX);
        var radiusY = Math.Max(1f, scaleY);

        for (var y = 0; y < destination.Height; y++)
        {
            var centerY = ((y + 0.5f) * scaleY) - 0.5f;
            for (var x = 0; x < destination.Width; x++)
            {
                var centerX = ((x + 0.5f) * scaleX) - 0.5f;
                destination[x, y] = SampleTriangle(source, centerX, centerY, radiusX, radiusY, options);
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
        var accumulator = new PixelAccumulator();

        for (var y = sourceY; y < maxY; y++)
        {
            for (var x = sourceX; x < maxX; x++)
            {
                accumulator.Add(source[x, y], 1f, options);
            }
        }

        return accumulator.ToPixel<TPixel>(options);
    }

    private static TPixel SampleTriangle<TPixel>(
        BitmapView<TPixel> source,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY,
        MipmapGenerationOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var minX = Math.Max(0, (int)MathF.Floor(centerX - radiusX));
        var minY = Math.Max(0, (int)MathF.Floor(centerY - radiusY));
        var maxX = Math.Min(source.Width - 1, (int)MathF.Ceiling(centerX + radiusX));
        var maxY = Math.Min(source.Height - 1, (int)MathF.Ceiling(centerY + radiusY));
        var accumulator = new PixelAccumulator();

        for (var y = minY; y <= maxY; y++)
        {
            var weightY = TriangleWeight(centerY, y, radiusY);
            if (weightY <= 0f)
            {
                continue;
            }

            for (var x = minX; x <= maxX; x++)
            {
                var weightX = TriangleWeight(centerX, x, radiusX);
                if (weightX <= 0f)
                {
                    continue;
                }

                accumulator.Add(source[x, y], weightX * weightY, options);
            }
        }

        return accumulator.ToPixel<TPixel>(options);
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

        _ = options.Filter switch
        {
            MipmapFilter.Box or MipmapFilter.Triangle => true,
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported mip-map filter '{options.Filter}'.")
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

    private static float TriangleWeight(float center, int sample, float radius)
    {
        var normalizedDistance = MathF.Abs(sample - center) / radius;
        return normalizedDistance >= 1f
            ? 0f
            : 1f - normalizedDistance;
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

    private struct PixelAccumulator
    {
        private float red;
        private float green;
        private float blue;
        private float alpha;
        private float weight;

        public void Add<TPixel>(TPixel source, float sampleWeight, MipmapGenerationOptions options)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (sampleWeight <= 0f)
            {
                return;
            }

            var pixel = TPixel.ToRgba32Float(source);
            var pixelRed = DecodeColor(pixel.Red, options.ColorSpace);
            var pixelGreen = DecodeColor(pixel.Green, options.ColorSpace);
            var pixelBlue = DecodeColor(pixel.Blue, options.ColorSpace);
            if (options.AlphaMode == MipmapAlphaMode.Premultiplied)
            {
                pixelRed *= pixel.Alpha;
                pixelGreen *= pixel.Alpha;
                pixelBlue *= pixel.Alpha;
            }

            red += pixelRed * sampleWeight;
            green += pixelGreen * sampleWeight;
            blue += pixelBlue * sampleWeight;
            alpha += pixel.Alpha * sampleWeight;
            weight += sampleWeight;
        }

        public readonly TPixel ToPixel<TPixel>(MipmapGenerationOptions options)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (weight <= 0f)
            {
                return TPixel.FromRgba32Float(new Rgba32Float(0f, 0f, 0f, 0f));
            }

            var outputRed = red;
            var outputGreen = green;
            var outputBlue = blue;
            var outputAlpha = alpha / weight;
            if (options.AlphaMode == MipmapAlphaMode.Premultiplied)
            {
                if (alpha > 0f)
                {
                    outputRed /= alpha;
                    outputGreen /= alpha;
                    outputBlue /= alpha;
                }
            }
            else
            {
                outputRed /= weight;
                outputGreen /= weight;
                outputBlue /= weight;
            }

            return TPixel.FromRgba32Float(new Rgba32Float(
                EncodeColor(outputRed, options.ColorSpace),
                EncodeColor(outputGreen, options.ColorSpace),
                EncodeColor(outputBlue, options.ColorSpace),
                outputAlpha));
        }
    }
}
