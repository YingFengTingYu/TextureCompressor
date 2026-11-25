using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs;

public sealed partial class S3tcTextureCoder
{
    private enum Dxt5Swizzle { None, Xgbr, Rxbg, Rbxg, Xrbg, Rgxb, Xgxr }

    private static Dxt5Swizzle GetDxt5Swizzle(TextureFormat format)
    {
        if (format == TextureFormats.Dxt5Xgbr) return Dxt5Swizzle.Xgbr;
        if (format == TextureFormats.Dxt5Rxbg) return Dxt5Swizzle.Rxbg;
        if (format == TextureFormats.Dxt5Rbxg) return Dxt5Swizzle.Rbxg;
        if (format == TextureFormats.Dxt5Xrbg) return Dxt5Swizzle.Xrbg;
        if (format == TextureFormats.Dxt5Rgxb) return Dxt5Swizzle.Rgxb;
        if (format == TextureFormats.Dxt5Xgxr || format == TextureFormats.Ati2NDxt5) return Dxt5Swizzle.Xgxr;
        return Dxt5Swizzle.None;
    }

    private static void SwizzleDxt5(Span<Rgba8UNorm> block, Dxt5Swizzle layout)
    {
        foreach (ref var p in block)
        {
            p = layout switch
            {
                Dxt5Swizzle.Xgbr => new(0, p.Green, p.Blue, p.Red),
                Dxt5Swizzle.Rxbg => new(p.Red, 0, p.Blue, p.Green),
                Dxt5Swizzle.Rbxg => new(p.Red, p.Blue, 0, p.Green),
                Dxt5Swizzle.Xrbg => new(0, p.Red, p.Blue, p.Green),
                Dxt5Swizzle.Rgxb => new(p.Red, p.Green, 0, p.Blue),
                Dxt5Swizzle.Xgxr => new(0, p.Green, 0, p.Red),
                _ => throw new ArgumentOutOfRangeException(nameof(layout))
            };
        }
    }

    private static void UnswizzleDxt5(Span<Rgba8UNorm> block, Dxt5Swizzle layout)
    {
        foreach (ref var p in block)
        {
            p = layout switch
            {
                Dxt5Swizzle.Xgbr => new(p.Alpha, p.Green, p.Blue),
                Dxt5Swizzle.Rxbg => new(p.Red, p.Alpha, p.Blue),
                Dxt5Swizzle.Rbxg => new(p.Red, p.Alpha, p.Green),
                Dxt5Swizzle.Xrbg => new(p.Green, p.Alpha, p.Blue),
                Dxt5Swizzle.Rgxb => new(p.Red, p.Green, p.Alpha),
                Dxt5Swizzle.Xgxr => new(p.Alpha, p.Green, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(layout))
            };
        }
    }
}
