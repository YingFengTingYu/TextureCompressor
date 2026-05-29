namespace TextureCompressor.Colors;

public struct Rgba64Float(double red, double green, double blue, double alpha = 1d)
    : IPixel<Rgba64Float>
{
    public double Red = red;
    public double Green = green;
    public double Blue = blue;
    public double Alpha = alpha;

    public static Rgba8UNorm ToRgba8UNorm(Rgba64Float value)
    {
        return new Rgba8UNorm(
            RgbaColorConversions.ToUNorm8(value.Red),
            RgbaColorConversions.ToUNorm8(value.Green),
            RgbaColorConversions.ToUNorm8(value.Blue),
            RgbaColorConversions.ToUNorm8(value.Alpha));
    }

    public static Rgba64Float FromRgba8UNorm(Rgba8UNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba8SNorm ToRgba8SNorm(Rgba64Float value)
    {
        return new Rgba8SNorm(
            RgbaColorConversions.ToSNorm8(value.Red),
            RgbaColorConversions.ToSNorm8(value.Green),
            RgbaColorConversions.ToSNorm8(value.Blue),
            RgbaColorConversions.ToSNorm8(value.Alpha));
    }

    public static Rgba64Float FromRgba8SNorm(Rgba8SNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba16UNorm ToRgba16UNorm(Rgba64Float value)
    {
        return new Rgba16UNorm(
            RgbaColorConversions.ToUNorm16(value.Red),
            RgbaColorConversions.ToUNorm16(value.Green),
            RgbaColorConversions.ToUNorm16(value.Blue),
            RgbaColorConversions.ToUNorm16(value.Alpha));
    }

    public static Rgba64Float FromRgba16UNorm(Rgba16UNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba16SNorm ToRgba16SNorm(Rgba64Float value)
    {
        return new Rgba16SNorm(
            RgbaColorConversions.ToSNorm16(value.Red),
            RgbaColorConversions.ToSNorm16(value.Green),
            RgbaColorConversions.ToSNorm16(value.Blue),
            RgbaColorConversions.ToSNorm16(value.Alpha));
    }

    public static Rgba64Float FromRgba16SNorm(Rgba16SNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba32UNorm ToRgba32UNorm(Rgba64Float value)
    {
        return new Rgba32UNorm(
            RgbaColorConversions.ToUNorm32(value.Red),
            RgbaColorConversions.ToUNorm32(value.Green),
            RgbaColorConversions.ToUNorm32(value.Blue),
            RgbaColorConversions.ToUNorm32(value.Alpha));
    }

    public static Rgba64Float FromRgba32UNorm(Rgba32UNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba32SNorm ToRgba32SNorm(Rgba64Float value)
    {
        return new Rgba32SNorm(
            RgbaColorConversions.ToSNorm32(value.Red),
            RgbaColorConversions.ToSNorm32(value.Green),
            RgbaColorConversions.ToSNorm32(value.Blue),
            RgbaColorConversions.ToSNorm32(value.Alpha));
    }

    public static Rgba64Float FromRgba32SNorm(Rgba32SNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba16Float ToRgba16Float(Rgba64Float value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba64Float FromRgba16Float(Rgba16Float value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba32Float ToRgba32Float(Rgba64Float value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba64Float FromRgba32Float(Rgba32Float value)
    {
        return new Rgba64Float(value.Red, value.Green, value.Blue, value.Alpha);
    }

    public static Rgba64UNorm ToRgba64UNorm(Rgba64Float value)
    {
        return new Rgba64UNorm(
            RgbaColorConversions.ToUInt64(value.Red),
            RgbaColorConversions.ToUInt64(value.Green),
            RgbaColorConversions.ToUInt64(value.Blue),
            RgbaColorConversions.ToUInt64(value.Alpha));
    }

    public static Rgba64Float FromRgba64UNorm(Rgba64UNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba64SNorm ToRgba64SNorm(Rgba64Float value)
    {
        return new Rgba64SNorm(
            RgbaColorConversions.ToSInt64(value.Red),
            RgbaColorConversions.ToSInt64(value.Green),
            RgbaColorConversions.ToSInt64(value.Blue),
            RgbaColorConversions.ToSInt64(value.Alpha));
    }

    public static Rgba64Float FromRgba64SNorm(Rgba64SNorm value)
    {
        return new Rgba64Float(
            RgbaColorConversions.ToDouble(value.Red),
            RgbaColorConversions.ToDouble(value.Green),
            RgbaColorConversions.ToDouble(value.Blue),
            RgbaColorConversions.ToDouble(value.Alpha));
    }

    public static Rgba64Float ToRgba64Float(Rgba64Float value)
    {
        return value;
    }

    public static Rgba64Float FromRgba64Float(Rgba64Float value)
    {
        return value;
    }
}
