using TextureCompressor.Formats;

namespace TextureCompressor.Colors;

public struct Rgba8UNorm(byte red, byte green, byte blue, byte alpha = 255)
    : IConvertibleToRgba8UNorm<Rgba8UNorm>, IConvertibleToRgba32Float<Rgba8UNorm>
{
    public byte Red = red;
    public byte Green = green;
    public byte Blue = blue;
    public byte Alpha = alpha;

    public static TextureFormat Format => TextureFormats.Rgba8UNorm;

    public static Rgba8UNorm ToRgba8UNorm(Rgba8UNorm value)
    {
        return value;
    }

    public static Rgba8UNorm FromRgba8UNorm(Rgba8UNorm value)
    {
        return value;
    }
    
    public static Rgba32Float ToRgba32Float(Rgba8UNorm value)
    {
        return new Rgba32Float(value.Red / 255f, value.Green / 255f, value.Blue / 255f, value.Alpha / 255f);
    }

    public static Rgba8UNorm FromRgba32Float(Rgba32Float value)
    {
        return new Rgba8UNorm(
            FloatToByte(value.Red),
            FloatToByte(value.Green),
            FloatToByte(value.Blue),
            FloatToByte(value.Alpha));
    }

    private static byte FloatToByte(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        return (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }
}
