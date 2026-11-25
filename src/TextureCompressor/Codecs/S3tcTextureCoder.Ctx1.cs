using System.Buffers.Binary;
using System.Numerics;
using TextureCompressor.Colors;
using TextureCompressor.Options;

namespace TextureCompressor.Codecs;

public sealed partial class S3tcTextureCoder
{
    private static void EncodeCtx1Block(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel level)
    {
        Span<Vector3> colors = stackalloc Vector3[TexelsPerBlock];
        var min = new Vector3(255);
        var max = Vector3.Zero;
        var sum = Vector3.Zero;
        var squares = Vector3.Zero;
        int product = 0;
        for (int i = 0; i < colors.Length; i++)
        {
            var p = source[i];
            var c = new Vector3(p.Red, p.Green, 0);
            colors[i] = c;
            min = Vector3.Min(min, c);
            max = Vector3.Max(max, c);
            sum += c;
            squares += c * c;
            product += p.Red * p.Green;
        }

        var variance = TexelsPerBlock * squares - sum * sum;
        float covariance = TexelsPerBlock * product - sum.X * sum.Y;
        // Both channels share selectors. Negatively correlated channels need
        // opposite ends of their ranges even in Fast mode.
        if (covariance < 0) (min.Y, max.Y) = (max.Y, min.Y);
        var best = ScoreCtx1(source, PackCtx1(min), PackCtx1(max));
        if (level >= TextureCompressionLevel.Normal && best.Error > 0)
        {
            var axis = variance.X >= variance.Y ? new Vector3(variance.X, covariance, 0)
                : new Vector3(covariance, variance.Y, 0);
            Span<float> projections = stackalloc float[TexelsPerBlock];
            Span<Vector3> prefix = stackalloc Vector3[TexelsPerBlock + 1];
            for (var tier = TextureCompressionLevel.Normal; tier <= level; tier++)
            {
                ProjectAndSort(colors, axis, projections, prefix);
                FindRampEndpoints(projections, prefix, false, tier, out var low, out var high);
                var candidate = ScoreCtx1(source, PackCtx1(low), PackCtx1(high));
                if (candidate.Error < best.Error) best = candidate;
                if (best.Error == 0) break;
                axis = new Vector3((best.Color1 >> 8) - (best.Color0 >> 8),
                    (best.Color1 & 255) - (best.Color0 & 255), 0);
            }
        }
        // Each little-endian endpoint is 0xRRGG in Xenos linear block order.
        BinaryPrimitives.WriteUInt16LittleEndian(destination, best.Color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], best.Color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], best.Indices);
    }

    private static ushort PackCtx1(Vector3 c) =>
        (ushort)((Math.Clamp((int)MathF.Round(c.X), 0, 255) << 8) | Math.Clamp((int)MathF.Round(c.Y), 0, 255));

    private static ColorBlockEncoding ScoreCtx1(ReadOnlySpan<Rgba8UNorm> source, ushort a, ushort b)
    {
        Span<byte> red = stackalloc byte[4];
        Span<byte> green = stackalloc byte[4];
        BuildCtx1Palette((byte)(a >> 8), (byte)(b >> 8), red);
        BuildCtx1Palette((byte)a, (byte)b, green);
        var result = new ColorBlockEncoding { Color0 = a, Color1 = b };
        for (int i = 0; i < TexelsPerBlock; i++)
        {
            int selector = FindNearestCtx1Index(source[i], red, green, out int error);
            result.Indices |= (uint)selector << (2 * i);
            result.Error += error;
        }
        return result;
    }
}
