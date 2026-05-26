using TextureCompressor.Formats;

namespace TextureCompressor.Colors;

public struct Rgba32Float(float red, float green, float blue, float alpha = 1f)
    : IConvertibleToRgba8UNorm<Rgba32Float>, IConvertibleToRgba32Float<Rgba32Float>
{
    public float Red = red;
    public float Green = green;
    public float Blue = blue;
    public float Alpha = alpha;

    public static TextureFormat Format => TextureFormats.Rgba32Float;
    
    public static Rgba8UNorm ToRgba8UNorm(Rgba32Float value)
    {
        return Rgba8UNorm.FromRgba32Float(value);
    }

    public static Rgba32Float FromRgba8UNorm(Rgba8UNorm value)
    {
        return Rgba8UNorm.ToRgba32Float(value);
    }

    public static Rgba32Float ToRgba32Float(Rgba32Float value)
    {
        return value;
    }

    public static Rgba32Float FromRgba32Float(Rgba32Float value)
    {
        return value;
    }
}
