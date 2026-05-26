using TextureCompressor.Formats;

namespace TextureCompressor.Colors;

public struct Rgba16Float
    : IConvertibleToRgba8UNorm<Rgba16Float>,
      IConvertibleToRgba8SNorm<Rgba16Float>,
      IConvertibleToRgba16UNorm<Rgba16Float>,
      IConvertibleToRgba16SNorm<Rgba16Float>,
      IConvertibleToRgba32UNorm<Rgba16Float>,
      IConvertibleToRgba32SNorm<Rgba16Float>,
      IConvertibleToRgba16Float<Rgba16Float>,
      IConvertibleToRgba32Float<Rgba16Float>
{
    public Half Red;
    public Half Green;
    public Half Blue;
    public Half Alpha;

    public Rgba16Float(Half red, Half green, Half blue)
        : this(red, green, blue, (Half)1f)
    {
    }

    public Rgba16Float(Half red, Half green, Half blue, Half alpha)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public Rgba16Float(float red, float green, float blue, float alpha = 1f)
        : this((Half)red, (Half)green, (Half)blue, (Half)alpha)
    {
    }

    public static TextureFormat Format => TextureFormats.Rgba16Float;

    public static Rgba8UNorm ToRgba8UNorm(Rgba16Float value)
    {
        return new Rgba8UNorm(
            RgbaColorConversions.ToUNorm8(value.Red),
            RgbaColorConversions.ToUNorm8(value.Green),
            RgbaColorConversions.ToUNorm8(value.Blue),
            RgbaColorConversions.ToUNorm8(value.Alpha));
    }

    public static Rgba16Float FromRgba8UNorm(Rgba8UNorm value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba8SNorm ToRgba8SNorm(Rgba16Float value)
    {
        return new Rgba8SNorm(
            RgbaColorConversions.ToSNorm8(value.Red),
            RgbaColorConversions.ToSNorm8(value.Green),
            RgbaColorConversions.ToSNorm8(value.Blue),
            RgbaColorConversions.ToSNorm8(value.Alpha));
    }

    public static Rgba16Float FromRgba8SNorm(Rgba8SNorm value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba16UNorm ToRgba16UNorm(Rgba16Float value)
    {
        return new Rgba16UNorm(
            RgbaColorConversions.ToUNorm16(value.Red),
            RgbaColorConversions.ToUNorm16(value.Green),
            RgbaColorConversions.ToUNorm16(value.Blue),
            RgbaColorConversions.ToUNorm16(value.Alpha));
    }

    public static Rgba16Float FromRgba16UNorm(Rgba16UNorm value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba16SNorm ToRgba16SNorm(Rgba16Float value)
    {
        return new Rgba16SNorm(
            RgbaColorConversions.ToSNorm16(value.Red),
            RgbaColorConversions.ToSNorm16(value.Green),
            RgbaColorConversions.ToSNorm16(value.Blue),
            RgbaColorConversions.ToSNorm16(value.Alpha));
    }

    public static Rgba16Float FromRgba16SNorm(Rgba16SNorm value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba32UNorm ToRgba32UNorm(Rgba16Float value)
    {
        return new Rgba32UNorm(
            RgbaColorConversions.ToUNorm32(value.Red),
            RgbaColorConversions.ToUNorm32(value.Green),
            RgbaColorConversions.ToUNorm32(value.Blue),
            RgbaColorConversions.ToUNorm32(value.Alpha));
    }

    public static Rgba16Float FromRgba32UNorm(Rgba32UNorm value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba32SNorm ToRgba32SNorm(Rgba16Float value)
    {
        return new Rgba32SNorm(
            RgbaColorConversions.ToSNorm32(value.Red),
            RgbaColorConversions.ToSNorm32(value.Green),
            RgbaColorConversions.ToSNorm32(value.Blue),
            RgbaColorConversions.ToSNorm32(value.Alpha));
    }

    public static Rgba16Float FromRgba32SNorm(Rgba32SNorm value)
    {
        return new Rgba16Float(
            RgbaColorConversions.ToHalf(value.Red),
            RgbaColorConversions.ToHalf(value.Green),
            RgbaColorConversions.ToHalf(value.Blue),
            RgbaColorConversions.ToHalf(value.Alpha));
    }

    public static Rgba16Float ToRgba16Float(Rgba16Float value)
    {
        return value;
    }

    public static Rgba16Float FromRgba16Float(Rgba16Float value)
    {
        return value;
    }

    public static Rgba32Float ToRgba32Float(Rgba16Float value)
    {
        return new Rgba32Float(
            RgbaColorConversions.ToFloat(value.Red),
            RgbaColorConversions.ToFloat(value.Green),
            RgbaColorConversions.ToFloat(value.Blue),
            RgbaColorConversions.ToFloat(value.Alpha));
    }

    public static Rgba16Float FromRgba32Float(Rgba32Float value)
    {
        return new Rgba16Float(value.Red, value.Green, value.Blue, value.Alpha);
    }
}
