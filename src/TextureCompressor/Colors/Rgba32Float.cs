using TextureCompressor.Formats;

namespace TextureCompressor.Colors;

public struct Rgba32Float(float red, float green, float blue, float alpha = 1f)
    : IPixel<Rgba32Float>
{
    public float Red = red;
    public float Green = green;
    public float Blue = blue;
    public float Alpha = alpha;

    public static TextureFormat Format => TextureFormats.Rgba32Float;

    public static Rgba8UNorm ToRgba8UNorm(Rgba32Float value)
    {
        return new Rgba8UNorm(
            RgbaColorConversions.ToUNorm8(value.Red),
            RgbaColorConversions.ToUNorm8(value.Green),
            RgbaColorConversions.ToUNorm8(value.Blue),
            RgbaColorConversions.ToUNorm8(value.Alpha));
    }

    public static Rgba32Float FromRgba8UNorm(Rgba8UNorm value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba8SNorm ToRgba8SNorm(Rgba32Float value)
    {
        return new Rgba8SNorm(
            RgbaColorConversions.ToSNorm8(value.Red),
            RgbaColorConversions.ToSNorm8(value.Green),
            RgbaColorConversions.ToSNorm8(value.Blue),
            RgbaColorConversions.ToSNorm8(value.Alpha));
    }

    public static Rgba32Float FromRgba8SNorm(Rgba8SNorm value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba16UNorm ToRgba16UNorm(Rgba32Float value)
    {
        return new Rgba16UNorm(
            RgbaColorConversions.ToUNorm16(value.Red),
            RgbaColorConversions.ToUNorm16(value.Green),
            RgbaColorConversions.ToUNorm16(value.Blue),
            RgbaColorConversions.ToUNorm16(value.Alpha));
    }

    public static Rgba32Float FromRgba16UNorm(Rgba16UNorm value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba16SNorm ToRgba16SNorm(Rgba32Float value)
    {
        return new Rgba16SNorm(
            RgbaColorConversions.ToSNorm16(value.Red),
            RgbaColorConversions.ToSNorm16(value.Green),
            RgbaColorConversions.ToSNorm16(value.Blue),
            RgbaColorConversions.ToSNorm16(value.Alpha));
    }

    public static Rgba32Float FromRgba16SNorm(Rgba16SNorm value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba32UNorm ToRgba32UNorm(Rgba32Float value)
    {
        return new Rgba32UNorm(
            RgbaColorConversions.ToUNorm32(value.Red),
            RgbaColorConversions.ToUNorm32(value.Green),
            RgbaColorConversions.ToUNorm32(value.Blue),
            RgbaColorConversions.ToUNorm32(value.Alpha));
    }

    public static Rgba32Float FromRgba32UNorm(Rgba32UNorm value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba32SNorm ToRgba32SNorm(Rgba32Float value)
    {
        return new Rgba32SNorm(
            RgbaColorConversions.ToSNorm32(value.Red),
            RgbaColorConversions.ToSNorm32(value.Green),
            RgbaColorConversions.ToSNorm32(value.Blue),
            RgbaColorConversions.ToSNorm32(value.Alpha));
    }

    public static Rgba32Float FromRgba32SNorm(Rgba32SNorm value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba16Float ToRgba16Float(Rgba32Float value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba32Float FromRgba16Float(Rgba16Float value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
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
