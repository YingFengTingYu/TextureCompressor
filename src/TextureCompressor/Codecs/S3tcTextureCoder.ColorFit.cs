using System.Numerics;
using TextureCompressor.Colors;
using TextureCompressor.Options;

namespace TextureCompressor.Codecs;

public sealed partial class S3tcTextureCoder
{
    // Search along the principal color axis as in Compressonator bc1_cmp.h,
    // then score the quantized endpoints with our software decoder palette.
    private static ColorBlockEncoding FitColorBlock(
        ReadOnlySpan<Rgba8UNorm> source, Dxt1ColorMode mode, bool punchThrough, TextureCompressionLevel level)
    {
        Span<Vector3> colors = stackalloc Vector3[TexelsPerBlock];
        int count = 0;
        var sum = Vector3.Zero;
        var squares = Vector3.Zero;
        var products = Vector3.Zero;
        for (int i = 0; i < TexelsPerBlock; i++)
        {
            var p = source[i];
            if (punchThrough && p.Alpha < AlphaCutoff)
            {
                continue;
            }

            var color = new Vector3(p.Red, p.Green, p.Blue);
            colors[count++] = color;
            sum += color;
            squares += color * color;
            products += color * new Vector3(color.Y, color.Z, color.X);
        }

        if (count == 0)
        {
            return new ColorBlockEncoding { Indices = uint.MaxValue };
        }

        var variance = count * squares - sum * sum;
        if (variance == Vector3.Zero)
        {
            return FitSolidColor(source, colors[0], mode, punchThrough);
        }

        var cross = count * products - sum * new Vector3(sum.Y, sum.Z, sum.X);
        var redAxis = new Vector3(variance.X, cross.X, cross.Z);
        var greenAxis = new Vector3(cross.X, variance.Y, cross.Y);
        var blueAxis = new Vector3(cross.Z, cross.Y, variance.Z);
        var axis = variance.X >= variance.Y && variance.X >= variance.Z
            ? redAxis : variance.Y >= variance.Z ? greenAxis : blueAxis;
        axis = redAxis * axis.X + greenAxis * axis.Y + blueAxis * axis.Z;

        Span<float> projections = stackalloc float[TexelsPerBlock];
        for (int i = 0; i < count; i++)
        {
            var color = colors[i];
            float key = Vector3.Dot(color, axis);
            int j = i;
            while (j > 0 && key < projections[j - 1])
            {
                projections[j] = projections[j - 1];
                colors[j] = colors[j - 1];
                j--;
            }

            projections[j] = key;
            colors[j] = color;
        }

        Span<Vector3> prefix = stackalloc Vector3[TexelsPerBlock + 1];
        prefix[0] = Vector3.Zero;
        for (int i = 0; i < count; i++)
        {
            prefix[i + 1] = prefix[i] + colors[i];
        }

        var best = new ColorBlockEncoding { Error = long.MaxValue };
        for (var tier = TextureCompressionLevel.Normal; tier <= level; tier++)
        {
            var candidate = FitProjectedRamp(source, projections[..count], prefix[..(count + 1)], mode,
                punchThrough, tier);
            if (candidate.Error < best.Error) best = candidate;
            if (best.Error == 0) break;
        }
        if (level == TextureCompressionLevel.Exhaustive && best.Error > 0)
        {
            RefineWithSelectorChanges(source, mode, punchThrough, ref best);
        }
        return best;
    }

    private static void RefineWithSelectorChanges(ReadOnlySpan<Rgba8UNorm> source, Dxt1ColorMode mode,
        bool punchThrough, ref ColorBlockEncoding best)
    {
        // Nearby endpoint pairs can change which palette entry a texel uses.
        // Score those assignments directly after the fixed-selector fit converges.
        Span<ushort> neighbors = stackalloc ushort[26];
        for (int pass = 0; pass < 3; pass++)
        {
            long initialError = best.Error;
            for (int channel = 0; channel < 3; channel++)
            {
                int shift = channel switch { 0 => 11, 1 => 5, _ => 0 };
                int mask = channel == 1 ? 63 : 31;
                int a = best.Color0, b = best.Color1;
                int x = (a >> shift) & mask, y = (b >> shift) & mask;
                for (int i = Math.Max(0, x - 2); i <= Math.Min(mask, x + 2); i++)
                {
                    for (int j = Math.Max(0, y - 2); j <= Math.Min(mask, y + 2); j++)
                    {
                        if (i == x && j == y) continue;
                        var candidate = EvaluateColorCandidate(source, mode, punchThrough,
                            (ushort)(a + ((i - x) << shift)), (ushort)(b + ((j - y) << shift)));
                        if (candidate.Error < best.Error) best = candidate;
                    }
                }
            }
            ushort first = best.Color0, second = best.Color1;
            int count = BlockColorRefinement.GetNeighbors(first, false, neighbors);
            for (int i = 0; i < count; i++)
            {
                var candidate = EvaluateColorCandidate(source, mode, punchThrough, neighbors[i], second);
                if (candidate.Error < best.Error) best = candidate;
            }
            first = best.Color0;
            count = BlockColorRefinement.GetNeighbors(best.Color1, false, neighbors);
            for (int i = 0; i < count; i++)
            {
                var candidate = EvaluateColorCandidate(source, mode, punchThrough, first, neighbors[i]);
                if (candidate.Error < best.Error) best = candidate;
            }
            if (best.Error == initialError) break;
        }
    }

    private static ColorBlockEncoding FitProjectedRamp(ReadOnlySpan<Rgba8UNorm> source, ReadOnlySpan<float> projections,
        ReadOnlySpan<Vector3> prefix, Dxt1ColorMode mode, bool punchThrough, TextureCompressionLevel level)
    {
        int count = projections.Length;
        var sum = prefix[count];
        int steps = punchThrough ? 2 : 3;
        float range = projections[count - 1] - projections[0];
        int first = 0, middle = 0, last = 0;
        for (int i = 0; i < count; i++)
        {
            float position = (projections[i] - projections[0]) * (2 * steps);
            if (position < range) first++;
            if (position < 3 * range) middle++;
            if (position < 5 * range) last++;
        }

        int radius = level switch
        {
            TextureCompressionLevel.Normal => 1,
            TextureCompressionLevel.High => 2,
            _ => count
        };
        float bestScore = float.PositiveInfinity;
        var low = prefix[1];
        var high = sum - prefix[count - 1];
        // Move the ramp ends on the projected axis. Prefix sums allow each
        // set of assigned selectors to be refitted without walking 16 texels.
        int subdivisions = level == TextureCompressionLevel.Normal ? 1 : 2;
        for (int a = -subdivisions; a <= subdivisions; a++)
        {
            float start = projections[0] + range * (a * 0.125f / subdivisions);
            for (int b = -subdivisions; b <= subdivisions; b++)
            {
                float end = projections[count - 1] + range * (b * 0.125f / subdivisions);
                float spacing = (end - start) / steps;
                int i = CountBelow(projections, start + 0.5f * spacing);
                int j = CountBelow(projections, start + 1.5f * spacing);
                if (punchThrough)
                {
                    FitPartition(steps, count, i + j, 3 * i + j, prefix[i] + prefix[j], sum,
                        ref bestScore, ref low, ref high);
                }
                else
                {
                    int k = CountBelow(projections, start + 2.5f * spacing);
                    FitPartition(steps, count, i + j + k, 5 * i + 3 * j + k, prefix[i] + prefix[j] + prefix[k], sum,
                        ref bestScore, ref low, ref high);
                }
            }
        }

        for (int i = Math.Max(0, first - radius); i <= Math.Min(count, first + radius); i++)
        {
            for (int j = Math.Max(i, middle - radius); j <= Math.Min(count, middle + radius); j++)
            {
                if (punchThrough)
                {
                    FitPartition(steps, count, i + j, 3 * i + j, prefix[i] + prefix[j], sum,
                        ref bestScore, ref low, ref high);
                    continue;
                }

                var pairSum = prefix[i] + prefix[j];
                for (int k = Math.Max(j, last - radius); k <= Math.Min(count, last + radius); k++)
                {
                    FitPartition(steps, count, i + j + k, 5 * i + 3 * j + k, pairSum + prefix[k], sum,
                        ref bestScore, ref low, ref high);
                }
            }
        }

        var best = EvaluateColorCandidate(source, mode, punchThrough, PackNearest565(low), PackNearest565(high));
        if (best.Error != 0)
        {
            var ramp = punchThrough ? ColorRamp.BcPunchThrough : ColorRamp.BcFour;
            int passes = level == TextureCompressionLevel.Normal ? 2 : 4;
            for (int pass = 0; pass < passes; pass++)
            {
                var pair = BlockColorRefinement.Refine(source, best.Color0, best.Color1, best.Indices, ramp, 1);
                var improved = EvaluateColorCandidate(source, mode, punchThrough, pair.First, pair.Second);
                if (improved.Error >= best.Error) break;
                best = improved;
            }
        }
        return best;
    }

    private static int CountBelow(ReadOnlySpan<float> sorted, float threshold)
    {
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (sorted[mid] < threshold) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static void FitPartition(int steps, int count, int weightSum, int aa, Vector3 weightedSum,
        Vector3 sum, ref float bestScore, ref Vector3 bestLow, ref Vector3 bestHigh)
    {
        // Weights for endpoint A descend from 'steps' to zero. Each boundary adds
        // its prefix once to weightedSum. The 2x2 normal equations need no texel loop.
        int ab = steps * weightSum - aa;
        int bb = steps * steps * count - aa - 2 * ab;
        int det = aa * bb - ab * ab;
        if (det == 0) return;

        var other = steps * sum - weightedSum;
        float scale = (float)steps / det;
        var low = (bb * weightedSum - ab * other) * scale;
        var high = (aa * other - ab * weightedSum) * scale;
        // At the unconstrained least-squares solution SSE is sum(x*x) minus
        // dot(endpoint, weightedSum)/steps. The omitted terms are constant.
        float score = -(Vector3.Dot(low, weightedSum) + Vector3.Dot(high, other));
        if (score >= bestScore) return;
        // Endpoints outside the representable range can have a misleadingly
        // good unconstrained fit. Penalize the error introduced by clamping.
        var dl = Vector3.Clamp(low, Vector3.Zero, new Vector3(255)) - low;
        var dh = Vector3.Clamp(high, Vector3.Zero, new Vector3(255)) - high;
        score += (aa * dl.LengthSquared() + 2 * ab * Vector3.Dot(dl, dh) + bb * dh.LengthSquared()) / steps;
        if (score < bestScore)
        {
            bestScore = score;
            bestLow = low;
            bestHigh = high;
        }
    }

    private static ushort PackNearest565(Vector3 color)
    {
        var scaled = Vector3.Clamp(color, Vector3.Zero, new Vector3(255)) *
            new Vector3(31f / 255, 63f / 255, 31f / 255) + new Vector3(0.5f);
        return (ushort)(((int)scaled.X << 11) | ((int)scaled.Y << 5) | (int)scaled.Z);
    }

    private static ColorBlockEncoding FitSolidColor(ReadOnlySpan<Rgba8UNorm> source, Vector3 color,
        Dxt1ColorMode mode, bool punchThrough)
    {
        // Each channel can use the same selector with independently chosen 565
        // endpoints. Prefer nearby endpoints when two pairs give the same color.
        var red = SolidEndpoints.Get((int)color.X, false, punchThrough);
        var green = SolidEndpoints.Get((int)color.Y, true, punchThrough);
        var blue = SolidEndpoints.Get((int)color.Z, false, punchThrough);
        ushort a = (ushort)((red.A << 11) | (green.A << 5) | blue.A);
        ushort b = (ushort)((red.B << 11) | (green.B << 5) | blue.B);
        return EvaluateColorCandidate(source, mode, punchThrough, a, b);
    }

    private static class SolidEndpoints
    {
        private static readonly (byte A, byte B)[] Table = Build();

        public static (byte A, byte B) Get(int value, bool green, bool half) =>
            Table[value + (green ? 256 : 0) + (half ? 512 : 0)];

        private static (byte A, byte B)[] Build()
        {
            var table = new (byte, byte)[1024];
            for (int group = 0; group < 4; group++)
            {
                int bits = (group & 1) == 0 ? 5 : 6;
                int levels = 1 << bits;
                for (int value = 0; value < 256; value++)
                {
                    int best = int.MaxValue;
                    for (int a = 0; a < levels; a++)
                    {
                        int x = BlockColorRefinement.Expand(a, bits);
                        for (int b = 0; b < levels; b++)
                        {
                            int y = BlockColorRefinement.Expand(b, bits);
                            int v = group < 2 ? (2 * x + y + 1) / 3 : (x + y) / 2;
                            int score = 256 * Math.Abs(value - v) + Math.Abs(x - y);
                            if (score < best)
                            {
                                best = score;
                                table[group * 256 + value] = ((byte)a, (byte)b);
                            }
                        }
                    }
                }
            }
            return table;
        }
    }

}
