using System.Numerics;
using TextureCompressor.Colors;

namespace TextureCompressor.Codecs;

internal enum ColorRamp { BcFour, BcPunchThrough, Atc, AtcBlack }

internal static class BlockColorRefinement
{
    // Compressonator refines a quantized ramp one channel at a time. For fixed
    // selectors, counts and channel sums suffice to score every endpoint pair.
    internal static (ushort First, ushort Second) Refine(ReadOnlySpan<Rgba8UNorm> source,
        ushort first, ushort second, uint indices, ColorRamp mode, int radius)
    {
        Span<int> counts = stackalloc int[4];
        Span<Vector3> sums = stackalloc Vector3[4];
        counts.Clear();
        sums.Clear();
        for (int i = 0; i < 16; i++)
        {
            int selector = (int)(indices & 3);
            indices >>= 2;
            if (mode == ColorRamp.BcPunchThrough && selector == 3) continue;
            var p = source[i];
            counts[selector]++;
            sums[selector] += new Vector3(p.Red, p.Green, p.Blue);
        }

        bool atc = mode is ColorRamp.Atc or ColorRamp.AtcBlack;
        int a = mode == ColorRamp.AtcBlack ? 0x8000 : 0;
        int b = 0;
        var n = new Vector4(counts[0], counts[1], counts[2], counts[3]);
        for (int channel = 0; channel < 3; channel++)
        {
            int bitsB = channel == 1 ? 6 : 5;
            int bitsA = atc ? 5 : bitsB;
            int shiftB = channel switch { 0 => 11, 1 => 5, _ => 0 };
            int shiftA = channel == 0 && atc ? 10 : shiftB;
            int maxA = (1 << bitsA) - 1, maxB = (1 << bitsB) - 1;
            int a0 = (first >> shiftA) & maxA, b0 = (second >> shiftB) & maxB;
            int bestA = a0, bestB = b0;
            float bestError = float.PositiveInfinity;
            var sum = new Vector4(sums[0][channel], sums[1][channel], sums[2][channel], sums[3][channel]);
            for (int ca = Math.Max(0, a0 - radius); ca <= Math.Min(maxA, a0 + radius); ca++)
            {
                int x = Expand(ca, bitsA);
                for (int cb = Math.Max(0, b0 - radius); cb <= Math.Min(maxB, b0 + radius); cb++)
                {
                    int y = Expand(cb, bitsB);
                    var palette = mode switch
                    {
                        ColorRamp.BcFour => new Vector4(x, y, (2 * x + y + 1) / 3, (x + 2 * y + 1) / 3),
                        ColorRamp.BcPunchThrough => new Vector4(x, y, (x + y) / 2, 0),
                        ColorRamp.Atc => new Vector4(x, (5 * x + 3 * y) / 8, (3 * x + 5 * y) / 8, y),
                        _ => new Vector4(0, Math.Max(0, x - y / 4), x, y)
                    };
                    float error = Vector4.Dot(palette, n * palette - 2 * sum);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestA = ca;
                        bestB = cb;
                    }
                }
            }
            a |= bestA << shiftA;
            b |= bestB << shiftB;
        }
        return ((ushort)a, (ushort)b);
    }

    internal static int Expand(int value, int bits) =>
        (value << (8 - bits)) | (value >> (2 * bits - 8));

    internal static int GetNeighbors(ushort endpoint, bool rgb555, Span<ushort> neighbors)
    {
        int redShift = rgb555 ? 10 : 11;
        int greenMax = rgb555 ? 31 : 63;
        int r = (endpoint >> redShift) & 31, g = (endpoint >> 5) & greenMax, b = endpoint & 31;
        int count = 0;
        for (int red = Math.Max(0, r - 1); red <= Math.Min(31, r + 1); red++)
        {
            for (int green = Math.Max(0, g - 1); green <= Math.Min(greenMax, g + 1); green++)
            {
                for (int blue = Math.Max(0, b - 1); blue <= Math.Min(31, b + 1); blue++)
                {
                    int next = endpoint + ((red - r) << redShift) + ((green - g) << 5) + blue - b;
                    if (next != endpoint) neighbors[count++] = (ushort)next;
                }
            }
        }
        return count;
    }
}
