using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Options;

namespace TextureCompressor.Tests;

public sealed class Bc4PrecisionTests
{
    private readonly record struct SignedRed(sbyte Value) : IPixel<SignedRed>
    {
        public static Rgba8UNorm ToRgba8UNorm(SignedRed value) => Rgba8UNorm.FromRgba8SNorm(ToRgba8SNorm(value));
        public static SignedRed FromRgba8UNorm(Rgba8UNorm value) => FromRgba8SNorm(Rgba8UNorm.ToRgba8SNorm(value));
        public static Rgba8SNorm ToRgba8SNorm(SignedRed value) => new(value.Value, 0, 0);
        public static SignedRed FromRgba8SNorm(Rgba8SNorm value) => new(value.Red);
    }

    [Fact]
    public void SignedPixelWithOnlyAnEightBitSignedTransferKeepsNegativeValues()
    {
        var image = new ArrayBitmap<SignedRed>(4, 4, Enumerable.Repeat(new SignedRed(-99), 16).ToArray());
        ITextureCoder coder = new RgtcLatcTextureCoder(TextureFormats.Bc4SNorm);
        var block = new byte[8];
        coder.Encode(image.AsView(), block);
        Assert.Equal(unchecked((byte)-99), block[0]);
        coder.Decode(block, image.AsView());
        Assert.All(image.Pixels, p => Assert.Equal(-99, p.Value));
    }

    [Theory]
    [InlineData(false, 231, 27)]
    [InlineData(false, 14, 188)]
    [InlineData(true, 55, -77)]
    [InlineData(true, -128, 32)]
    public void FloatOutputRetainsFractionalInterpolants(bool signed, int a, int b)
    {
        byte[] block = [(byte)a, (byte)b, 0x88, 0xc6, 0xfa, 0x88, 0xc6, 0xfa];
        var bitmap = new ArrayBitmap<Rgba32Float>(4, 4);
        ITextureCoder coder = new RgtcLatcTextureCoder(signed ? TextureFormats.Bc4SNorm : TextureFormats.Bc4UNorm);
        coder.Decode(block, bitmap.AsView());
        for (int i = 0; i < 16; i++)
        {
            double expected = Palette(Math.Max(-127, a), b, i % 8, signed) / (signed ? 127 : 255);
            var p = bitmap.Pixels[i];
            Assert.InRange(Math.Abs(p.Red - expected), 0, 1e-7);
            Assert.Equal((0f, 0f, 1f), (p.Green, p.Blue, p.Alpha));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlockRowsMatchIndependentClampedBlocksAndPreservePadding(bool signed)
    {
        const int width = 67, height = 31;
        var source = new ArrayBitmap<Rgba32Float>(width, height);
        for (int i = 0; i < source.Pixels.Length; i++)
            source.Pixels[i] = new Rgba32Float(MathF.Sin(i * .17f), MathF.Cos(i * .27f), 0, MathF.Sin(i * .33f));
        ITextureCoder coder = new RgtcLatcTextureCoder(signed ? TextureFormats.Latc2SNorm : TextureFormats.Bc5UNorm);
        int columns = (width + 3) / 4, rows = (height + 3) / 4, pitch = columns * 16 + 9;
        var encoded = Enumerable.Repeat((byte)0xab, pitch * rows).ToArray();
        ((IPitchTextureCoder)coder).Encode(source.AsView(), encoded, pitch);
        var tile = new ArrayBitmap<Rgba32Float>(4, 4);
        var expected = new byte[16];
        for (int by = 0; by < rows; by++)
        {
            for (int bx = 0; bx < columns; bx++)
            {
                for (int i = 0; i < 16; i++)
                    tile.Pixels[i] = source.Pixels[Math.Min(4 * by + i / 4, height - 1) * width + Math.Min(4 * bx + i % 4, width - 1)];
                coder.Encode(tile.AsView(), expected);
                Assert.Equal(expected, encoded.AsSpan(by * pitch + bx * 16, 16).ToArray());
            }
            Assert.All(encoded.AsSpan(by * pitch + columns * 16, 9).ToArray(), p => Assert.Equal(0xab, p));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustiveMatchesIndependentEndpointEnumeration(bool signed)
    {
        int lo = signed ? -127 : 0, hi = signed ? 127 : 255;
        var bitmap = new ArrayBitmap<Rgba32Float>(4, 4);
        for (int i = 0; i < 16; i++)
        {
            float value = lo + ((i * 71 + 3) % (hi - lo)) + .37f;
            bitmap.Pixels[i] = new Rgba32Float(value / hi, 0, 0);
        }
        ITextureCoder coder = new RgtcLatcTextureCoder(signed ? TextureFormats.Bc4SNorm : TextureFormats.Bc4UNorm,
            new TextureCompressionOptions { CompressionMode = TextureCompressionLevel.Exhaustive });
        var block = new byte[8];
        coder.Encode(bitmap.AsView(), block);
        int first = signed ? (sbyte)block[0] : block[0], second = signed ? (sbyte)block[1] : block[1];
        ulong selectors = BinaryPrimitives.ReadUInt64LittleEndian(block) >> 16;
        double actual = 0;
        foreach (var p in bitmap.Pixels)
        {
            actual += Math.Pow(p.Red * hi - Palette(first, second, (int)(selectors & 7), signed), 2);
            selectors >>= 3;
        }
        double optimum = double.PositiveInfinity;
        Span<double> palette = stackalloc double[8];
        for (int a = lo; a <= hi; a++)
        for (int b = lo; b <= hi; b++)
        {
            for (int k = 0; k < 8; k++) palette[k] = Palette(a, b, k, signed);
            double error = 0;
            foreach (var p in bitmap.Pixels)
            {
                double distance = double.PositiveInfinity;
                foreach (double entry in palette) distance = Math.Min(distance, Math.Abs(p.Red * hi - entry));
                error += distance * distance;
                if (error > optimum) break;
            }
            optimum = Math.Min(optimum, error);
        }
        Assert.InRange(actual - optimum, -1e-7, 0.001);
    }

    private static double Palette(int a, int b, int index, bool signed)
    {
        int steps = a > b ? 7 : 5;
        return index switch
        {
            0 => a, 1 => b, 6 when steps == 5 => signed ? -127 : 0, 7 when steps == 5 => signed ? 127 : 255,
            _ => (a * (steps + 1 - index) + b * (index - 1)) / (double)steps
        };
    }
}
