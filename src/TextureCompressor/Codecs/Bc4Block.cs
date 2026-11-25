using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using TextureCompressor.Options;

namespace TextureCompressor.Codecs;

// BC4 endpoints are integers, but the reconstructed values are fractions.
// Work in endpoint units so signed and unsigned blocks use the same fitter.
internal static class Bc4Block
{
    internal static void Decode(ReadOnlySpan<byte> block, bool signed, Span<float> values)
    {
        int a = signed ? Math.Max(-127, (int)(sbyte)block[0]) : block[0];
        int b = signed ? Math.Max(-127, (int)(sbyte)block[1]) : block[1];
        int maximum = signed ? 127 : 255;
        int steps = a > b ? 7 : 5;
        Span<float> palette = stackalloc float[8];
        palette[0] = a / (float)maximum;
        palette[1] = b / (float)maximum;
        for (int i = 1; i < steps; i++)
        {
            palette[i + 1] = ((steps - i) * a + i * b) / (float)(steps * maximum);
        }
        if (steps == 5)
        {
            palette[6] = signed ? -1f : 0f;
            palette[7] = 1f;
        }

        ulong indices = BinaryPrimitives.ReadUInt64LittleEndian(block) >> 16;
        for (int i = 0; i < values.Length; i++, indices >>= 3)
        {
            values[i] = palette[(int)(indices & 7)];
        }
    }

    internal static void Encode(Span<float> values, bool signed, TextureCompressionLevel level, Span<byte> block)
    {
        int lo = signed ? -127 : 0, hi = signed ? 127 : 255;
        float min = hi, max = lo, innerMin = hi, innerMax = lo;
        for (int i = 0; i < values.Length; i++)
        {
            float value = values[i] * hi;
            value = float.IsNaN(value) ? 0 : Math.Clamp(value, lo, hi);
            values[i] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            if (value > lo && value < hi)
            {
                innerMin = Math.Min(innerMin, value);
                innerMax = Math.Max(innerMax, value);
            }
        }
        var fit = new Fitter(values, lo, hi);
        int a = (int)MathF.Round(max), b = (int)MathF.Round(min);
        fit.Try(a, b);
        if (level >= TextureCompressionLevel.Normal && fit.Error > 0)
        {
            fit.Optimize(a, b);
            fit.Optimize((int)MathF.Round(innerMin), (int)MathF.Round(innerMax));
            fit.Refine(1);
        }
        if (level >= TextureCompressionLevel.High && fit.Error > 0)
        {
            int stride = Math.Max(1, (a - b + 3) / 7);
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                int first = Math.Clamp(a + i * stride, lo, hi);
                int last = Math.Clamp(b + j * stride, lo, hi);
                fit.Optimize(first, last);
                fit.Optimize(last, first);
            }
            // In a block crossing a mask edge one fixed selector may cover an
            // entire cluster. Fit the six interpolated entries on the other side.
            Span<float> sorted = stackalloc float[16];
            values.CopyTo(sorted);
            sorted.Sort();
            int split = 1;
            for (int i = 2; i < sorted.Length; i++)
                if (sorted[i] - sorted[i - 1] > sorted[split] - sorted[split - 1]) split = i;
            fit.Optimize(b, (int)MathF.Round(sorted[split - 1]));
            fit.Optimize((int)MathF.Round(sorted[split]), a);
            fit.Refine(2);
        }
        if (level == TextureCompressionLevel.Exhaustive && fit.Error > 0)
        {
            float tolerance = MathF.Sqrt(fit.Error) + 0.001f;
            float neededMin = hi, neededMax = lo;
            foreach (float v in values)
            {
                if (v > lo + tolerance && v < hi - tolerance)
                {
                    neededMin = Math.Min(neededMin, v);
                    neededMax = Math.Max(neededMax, v);
                }
            }
            for (int x = lo; x <= hi && fit.Error > 0; x++)
            {
                if (x >= max - tolerance)
                    for (int y = lo; y < x && y <= min + tolerance; y++) fit.Try(x, y);
                if (x <= neededMin + tolerance)
                    for (int y = Math.Max(x, (int)MathF.Ceiling(neededMax - tolerance)); y <= hi; y++) fit.Try(x, y);
            }
        }
        ulong bits = (byte)fit.A | ((ulong)(byte)fit.B << 8);
        for (int i = 0; i < values.Length; i++)
        {
            int selector = fit.Select(values[i], fit.A, fit.B);
            bits |= (ulong)selector << (16 + i * 3);
        }
        BinaryPrimitives.WriteUInt64LittleEndian(block, bits);
    }

    private ref struct Fitter(ReadOnlySpan<float> values, int lo, int hi)
    {
        private readonly ReadOnlySpan<float> _values = values;
        public int A, B;
        public float Error = float.PositiveInfinity;

        public int Select(float value, int a, int b)
        {
            int steps = a > b ? 7 : 5;
            int k = a == b ? 0 : Math.Clamp((int)MathF.Round((value - a) * steps / (b - a)), 0, steps);
            if (steps == 5)
            {
                float distance = MathF.Abs(value - (a + k * (b - a) / 5f));
                if (value - lo < distance && value - lo <= hi - value) return 6;
                if (hi - value < distance) return 7;
            }
            return k == 0 ? 0 : k == steps ? 1 : k + 1;
        }

        public void Try(int a, int b)
        {
            int steps = a > b ? 7 : 5;
            float scale = a == b ? 0 : steps / (float)(b - a);
            var origin = new Vector4(a);
            var errors = Vector4.Zero;
            foreach (var v in MemoryMarshal.Cast<float, Vector4>(_values))
            {
                var k = Vector4.Clamp(Vector4.Round((v - origin) * scale), Vector4.Zero, new Vector4(steps));
                var delta = Vector4.Abs(v - (origin + k * ((b - a) / (float)steps)));
                if (steps == 5) delta = Vector4.Min(delta, Vector4.Min(v - new Vector4(lo), new Vector4(hi) - v));
                errors += delta * delta;
            }
            float error = Vector4.Dot(errors, Vector4.One);
            if (error < Error)
            {
                A = a;
                B = b;
                Error = error;
            }
        }

        public void Optimize(int a, int b)
        {
            bool eight = a > b;
            int steps = eight ? 7 : 5;
            for (int pass = 0; pass < 5; pass++)
            {
                Try(a, b);
                int aa = 0, ab = 0, bb = 0;
                float av = 0, bv = 0;
                foreach (float v in _values)
                {
                    int index = Select(v, a, b);
                    if (!eight && index >= 6) continue;
                    int wb = index switch { 0 => 0, 1 => steps, _ => index - 1 };
                    int wa = steps - wb;
                    aa += wa * wa;
                    ab += wa * wb;
                    bb += wb * wb;
                    av += wa * v;
                    bv += wb * v;
                }
                int det = aa * bb - ab * ab;
                if (det == 0) break;
                int x = Math.Clamp((int)MathF.Round((av * bb - bv * ab) * steps / det), lo, hi);
                int y = Math.Clamp((int)MathF.Round((bv * aa - av * ab) * steps / det), lo, hi);
                if ((x > y) != eight || (x == a && y == b)) break;
                a = x;
                b = y;
            }
        }

        public void Refine(int radius)
        {
            int a = A, b = B;
            for (int x = Math.Max(lo, a - radius); x <= Math.Min(hi, a + radius); x++)
            for (int y = Math.Max(lo, b - radius); y <= Math.Min(hi, b + radius); y++) Try(x, y);
        }
    }
}
