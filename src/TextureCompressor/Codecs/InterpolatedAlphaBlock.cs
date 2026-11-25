using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Buffers.Binary;
using TextureCompressor.Options;

namespace TextureCompressor.Codecs;

// DXT5 and ATC share the two BC3 alpha ramps. Keep the rounding used by each
// software decoder in the encoder's error metric as well.
internal static class InterpolatedAlphaBlock
{
    internal static void BuildPalette(int a, int b, bool rounded, Span<byte> palette)
    {
        palette[0] = (byte)a;
        palette[1] = (byte)b;
        if (a > b) FillRamp(a, b, 7, rounded ? 3 : 0, palette);
        else
        {
            FillRamp(a, b, 5, rounded ? 2 : 0, palette);
            palette[6] = 0;
            palette[7] = 255;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillRamp(int a, int b, int steps, int bias, Span<byte> palette)
    {
        int numerator = steps * a + bias;
        for (int i = 1; i < steps; i++)
        {
            numerator += b - a;
            palette[i + 1] = (byte)(numerator / steps);
        }
    }

    internal static void Encode(ReadOnlySpan<byte> values, Span<byte> destination,
        TextureCompressionLevel level, bool rounded)
    {
        if (level is < TextureCompressionLevel.Fast or > TextureCompressionLevel.Exhaustive)
            throw new ArgumentOutOfRangeException(nameof(level));

        int min = 255, max = 0, innerMin = 255, innerMax = 0;
        foreach (byte value in values)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            if (value != 0) innerMin = Math.Min(innerMin, value);
            if (value != 255) innerMax = Math.Max(innerMax, value);
        }
        if (min == max)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, (uint)(min | (min << 8)));
            return;
        }

        var best = new Candidate { Error = int.MaxValue };
        TryEndpoints(values, max, min, rounded, ref best);
        if (level != TextureCompressionLevel.Fast && best.Error > 0)
        {
            // Like Compressonator's CompBlock1X, fit both a free eight-level
            // ramp and a six-level ramp with exact 0 and 255 outside it.
            FitRamp(values, max, min, rounded, level, ref best);
            FitRamp(values, Math.Min(innerMin, innerMax), Math.Max(innerMin, innerMax), rounded, level, ref best);
            if (level >= TextureCompressionLevel.High && best.Error > 0)
            {
                SearchRampSeeds(values, max, min, rounded, level, ref best);
                if (innerMin < innerMax) SearchRampSeeds(values, innerMin, innerMax, rounded, level, ref best);
                FitSeparatedRanges(values, rounded, level, ref best);
            }
            if (level == TextureCompressionLevel.Exhaustive && best.Error > 0)
            {
                SearchAll(values, min, max, innerMin, innerMax, rounded, ref best);
            }
        }
        BinaryPrimitives.WriteUInt64LittleEndian(destination,
            (best.Indices << 16) | (uint)(best.A | (best.B << 8)));
    }

    private static void FitSeparatedRanges(ReadOnlySpan<byte> values, bool rounded,
        TextureCompressionLevel level, ref Candidate best)
    {
        // The six-level ramp can cover one cluster while its fixed 0 or 255
        // selector represents the other side of a sharp alpha transition.
        Span<byte> sorted = stackalloc byte[16];
        values.CopyTo(sorted);
        sorted.Sort();
        int split = 1;
        for (int i = 2; i < sorted.Length; i++)
        {
            if (sorted[i] - sorted[i - 1] > sorted[split] - sorted[split - 1])
            {
                split = i;
            }
        }
        if (5 * (sorted[split] - sorted[split - 1]) <= sorted[^1] - sorted[0]) return;
        if (sorted[0] < sorted[split - 1]) FitRamp(values, sorted[0], sorted[split - 1], rounded, level, ref best);
        if (sorted[split] < sorted[^1]) FitRamp(values, sorted[split], sorted[^1], rounded, level, ref best);
    }

    private static void SearchRampSeeds(ReadOnlySpan<byte> values, int a, int b, bool rounded,
        TextureCompressionLevel level, ref Candidate best)
    {
        int stride = Math.Max(2, (Math.Abs(b - a) + 4) / 8);
        var seed = new Candidate { Error = int.MaxValue };
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                int x = Math.Clamp(a + i * stride, 0, 255);
                int y = Math.Clamp(b + j * stride, 0, 255);
                if ((x > y) == (a > b)) TryEndpoints(values, x, y, rounded, ref seed);
            }
        }
        if (seed.Error < int.MaxValue) FitRamp(values, seed.A, seed.B, rounded, level, ref best);
    }

    private static void FitRamp(ReadOnlySpan<byte> values, int a, int b, bool rounded,
        TextureCompressionLevel level, ref Candidate best)
    {
        var fit = new Candidate { Error = int.MaxValue };
        TryEndpoints(values, a, b, rounded, ref fit);
        bool eight = a > b;
        int steps = eight ? 7 : 5;
        for (int pass = 0; pass < 4 && fit.Error > 0; pass++)
        {
            int aa = 0, ab = 0, bb = 0, av = 0, bv = 0;
            ulong indices = fit.Indices;
            foreach (byte value in values)
            {
                int index = (int)(indices & 7);
                indices >>= 3;
                if (!eight && index >= 6) continue;
                int wb = index switch { 0 => 0, 1 => steps, _ => index - 1 };
                int wa = steps - wb;
                aa += wa * wa;
                ab += wa * wb;
                bb += wb * wb;
                av += wa * value;
                bv += wb * value;
            }
            int det = aa * bb - ab * ab;
            if (det == 0) break;
            int na = Math.Clamp((int)MathF.Round((float)(steps * (av * bb - bv * ab)) / det), 0, 255);
            int nb = Math.Clamp((int)MathF.Round((float)(steps * (bv * aa - av * ab)) / det), 0, 255);
            if ((na > nb) != eight || !TryEndpoints(values, na, nb, rounded, ref fit)) break;
        }

        int radius = level == TextureCompressionLevel.Normal ? 2 : 3;
        int centerA = fit.A, centerB = fit.B;
        for (int x = Math.Max(0, centerA - radius); x <= Math.Min(255, centerA + radius); x++)
        {
            for (int y = Math.Max(0, centerB - radius); y <= Math.Min(255, centerB + radius); y++)
            {
                if (x != centerA || y != centerB) TryEndpoints(values, x, y, rounded, ref fit);
            }
        }
        if (fit.Error < best.Error) best = fit;
    }

    private static void SearchAll(ReadOnlySpan<byte> values, int min, int max, int innerMin, int innerMax,
        bool rounded, ref Candidate best)
    {
        for (int a = 0; a <= 255 && best.Error > 0; a++)
        {
            // For the eight-level mode, each endpoint must reach the observed
            // extrema within sqrt(SSE). For six levels, 0 and 255 can absorb them.
            int tolerance = (int)Math.Sqrt(best.Error);
            if (a >= max - tolerance)
            {
                for (int b = 0; b <= Math.Min(a - 1, min + tolerance); b++)
                    TryEndpoints(values, a, b, rounded, ref best);
            }
            if (innerMin <= innerMax && innerMin > tolerance && a > innerMin + tolerance) continue;
            int lower = innerMin <= innerMax && 255 - innerMax > tolerance ? innerMax - tolerance : a;
            for (int b = Math.Max(a, lower); b <= 255; b++)
                TryEndpoints(values, a, b, rounded, ref best);
        }
    }

    private static bool TryEndpoints(ReadOnlySpan<byte> values, int a, int b, bool rounded, ref Candidate best)
    {
        Span<byte> palette = stackalloc byte[8];
        BuildPalette(a, b, rounded, palette);
        if (Sse2.IsSupported && best.Error < int.MaxValue)
        {
            // Score all 16 alpha samples at once. Only improving candidates
            // need selector bits packed in texture order.
            var v = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(values));
            var distance = Vector128.Create(byte.MaxValue);
            foreach (byte entry in palette)
            {
                var color = Vector128.Create(entry);
                distance = Vector128.Min(distance, Vector128.Max(v, color) - Vector128.Min(v, color));
            }
            var lo = Vector128.WidenLower(distance).AsInt16();
            var hi = Vector128.WidenUpper(distance).AsInt16();
            int sse = Vector128.Sum(Sse2.MultiplyAddAdjacent(lo, lo) + Sse2.MultiplyAddAdjacent(hi, hi));
            if (sse >= best.Error) return false;
        }
        ReadOnlySpan<byte> order = a > b ? [1, 7, 6, 5, 4, 3, 2, 0] : [6, 0, 2, 3, 4, 5, 1, 7];
        Span<byte> mid = stackalloc byte[7];
        for (int i = 0; i < 7; i++) mid[i] = (byte)((palette[order[i]] + palette[order[i + 1]]) >> 1);

        int error = 0;
        ulong indices = 0;
        for (int i = 0; i < values.Length; i++)
        {
            int value = values[i];
            int n = value > mid[3] ? 4 : 0;
            if (value > mid[n + 1]) n += 2;
            if (value > mid[n]) n++;
            int index = order[n];
            int delta = value - palette[index];
            error += delta * delta;
            if (error >= best.Error) return false;
            indices |= (ulong)index << (3 * i);
        }
        best = new Candidate { A = a, B = b, Indices = indices, Error = error };
        return true;
    }

    private struct Candidate
    {
        public int A, B, Error;
        public ulong Indices;
    }
}
