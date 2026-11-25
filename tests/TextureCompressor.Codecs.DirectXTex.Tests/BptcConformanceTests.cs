using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs.DirectXTex;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Options;

namespace TextureCompressor.Codecs.DirectXTex.Tests;

public sealed class BptcConformanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bc6HMatchesDirectXTexForEveryWireMode(bool signed)
    {
        int[] prefixes = [0, 1, 2, 6, 10, 14, 18, 22, 26, 30, 3, 7, 11, 15, 19, 23, 27, 31];
        var format = signed ? TextureFormats.Bc6HSFloat : TextureFormats.Bc6HUFloat;
        foreach (int prefix in prefixes)
        {
            var bytes = MakeBlocks(prefix, prefix < 2 ? 2 : 5);
            var ours = Decode<Rgba32Float>(new BptcTextureCoder(format), bytes);
            var native = Decode<Rgba32Float>(new DirectXTexTextureCoder(format), bytes);
            for (int i = 0; i < ours.Length; i++)
            {
                Assert.True(ours[i].Equals(native[i]), $"BC6H prefix {prefix}, texel {i}: " +
                    $"{ours[i].Red},{ours[i].Green},{ours[i].Blue} vs {native[i].Red},{native[i].Green},{native[i].Blue}");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bc7MatchesDirectXTexForEveryMode(bool srgb)
    {
        var format = srgb ? TextureFormats.Bc7Srgb : TextureFormats.Bc7UNorm;
        for (int mode = 0; mode <= 8; mode++)
        {
            var bytes = MakeBlocks(mode == 8 ? 0 : 1 << mode, Math.Min(8, mode + 1));
            var ours = Decode<Rgba8UNorm>(new BptcTextureCoder(format), bytes);
            var native = Decode<Rgba8UNorm>(new DirectXTexTextureCoder(format), bytes);
            int tolerance = srgb ? 1 : 0;
            for (int i = 0; i < ours.Length; i++)
            {
                var a = ours[i];
                var b = native[i];
                Assert.True(Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance
                    && Math.Abs(a.Blue - b.Blue) <= tolerance && a.Alpha == b.Alpha, $"BC7 mode {mode}, texel {i}");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bc6HReproducesIndependentHdrRamps(bool signed)
    {
        var format = signed ? TextureFormats.Bc6HSFloat : TextureFormats.Bc6HUFloat;
        var image = new ArrayBitmap<Rgba32Float>(4, 4);
        int[] weights = [0, 9, 18, 27, 37, 46, 55, 64];
        // Prefix 30, partition 0. The two color lines use independent 6-bit endpoints.
        for (int i = 0; i < 16; i++)
        {
            bool right = (i & 2) != 0;
            int w = weights[(7 - (i / 4 * 2 + i % 2))];
            int sign = signed ? -1 : 1;
            image.Pixels[i] = right
                ? new(Channel(8, 11), Channel(24 * sign, 27 * sign), Channel(12, 14), 1)
                : new(Channel(24, 27), Channel(8, 9), Channel(10 * sign, 14 * sign), 1);

            float Channel(int a, int b)
            {
                int Endpoint(int q) => Math.Sign(q) * ((Math.Abs(q) * 1024) + 512);
                int interpolated = ((64 - w) * Endpoint(a) + w * Endpoint(b) + 32) >> 6;
                int magnitude = Math.Abs(interpolated) * 31 >> (signed ? 5 : 6);
                return (float)BitConverter.UInt16BitsToHalf((ushort)(magnitude | (interpolated < 0 ? 0x8000 : 0)));
            }
        }
        var encoded = Encode(format, image, TextureCompressionLevel.Exhaustive);
        Assert.Equal(30, encoded[0] & 31);
        Assert.Equal(image.Pixels, DecodeBlock<Rgba32Float>(format, encoded));
    }

    [Fact]
    public void Bc6HFullPrecisionUnsignedModePreservesBrightConstant()
    {
        var image = new ArrayBitmap<Rgba32Float>(4, 4);
        var pixel = new Rgba32Float(40160, 48480, 63968, 1);
        Array.Fill(image.Pixels, pixel);
        var encoded = Encode(TextureFormats.Bc6HUFloat, image, TextureCompressionLevel.Normal);
        Assert.Equal(15, encoded[0] & 31);
        Assert.Equal(image.Pixels, DecodeBlock<Rgba32Float>(TextureFormats.Bc6HUFloat, encoded));
    }

    [Theory]
    [InlineData(4, 0, 0)]
    [InlineData(4, 0, 1)]
    [InlineData(4, 1, 0)]
    [InlineData(4, 1, 1)]
    [InlineData(4, 2, 0)]
    [InlineData(4, 2, 1)]
    [InlineData(4, 3, 0)]
    [InlineData(4, 3, 1)]
    [InlineData(5, 0, 0)]
    [InlineData(5, 1, 0)]
    [InlineData(5, 2, 0)]
    [InlineData(5, 3, 0)]
    public void Bc7PreservesIndependentPlanes(int mode, int rotation, int selection)
    {
        var image = new ArrayBitmap<Rgba8UNorm>(4, 4);
        int[] four = [0, 21, 43, 64], eight = [0, 9, 18, 27, 37, 46, 55, 64];
        var rgb = selection == 0 ? four : eight;
        var alpha = mode == 4 && selection == 0 ? eight : four;
        for (int i = 0; i < 16; i++)
        {
            int w = rgb[(i * 3 + 7) % rgb.Length], v = alpha[(i / 4 + i * 2) % alpha.Length];
            static byte Mix(int a, int b, int weight) => (byte)((a * (64 - weight) + b * weight + 32) >> 6);
            byte r = Mix(33, 206, w), g = Mix(66, 173, w), b = Mix(99, 231, w), a = Mix(12, 219, v);
            if (mode == 5)
            {
                r = Mix(4, 213, w);
                g = Mix(124, 131, w);
                b = Mix(68, 235, w);
                a = Mix(5, 237, v);
            }
            image.Pixels[i] = rotation switch
            {
                1 => new(a, g, b, r), 2 => new(r, a, b, g), 3 => new(r, g, a, b), _ => new(r, g, b, a)
            };
        }
        var encoded = Encode(TextureFormats.Bc7UNorm, image, TextureCompressionLevel.Normal);
        int header = mode == 4 ? 16 | (rotation << 5) | (selection << 7) : 32 | (rotation << 6);
        Assert.Equal(header, encoded[0]);
        Assert.Equal(image.Pixels, DecodeBlock<Rgba8UNorm>(TextureFormats.Bc7UNorm, encoded));
    }

    [Theory]
    [InlineData(TextureCompressionLevel.Fast)]
    [InlineData(TextureCompressionLevel.Normal)]
    [InlineData(TextureCompressionLevel.High)]
    [InlineData(TextureCompressionLevel.Exhaustive)]
    public void Bc7PreservesFullyTransparentAndOpaqueBlocks(TextureCompressionLevel level)
    {
        foreach (byte alpha in (byte[])[0, 255])
        {
            var source = new ArrayBitmap<Rgba8UNorm>(4, 4);
            for (int i = 0; i < 16; i++)
                source.Pixels[i] = new((byte)(20 + 4 * i), (byte)(150 - 4 * i), (byte)(2 * i), alpha);
            var encoded = Encode(TextureFormats.Bc7UNorm, source, level);
            Assert.All(DecodeBlock<Rgba8UNorm>(TextureFormats.Bc7UNorm, encoded), pixel => Assert.Equal(alpha, pixel.Alpha));
        }
    }

    [Fact]
    public void Bc7SrgbRetainsDarkColorsFromFloatInput()
    {
        var image = new ArrayBitmap<Rgba32Float>(4, 4);
        var pixel = new Rgba32Float(4 / (255f * 12.92f), 2 / (255f * 12.92f), 6 / (255f * 12.92f), 200 / 255f);
        Array.Fill(image.Pixels, pixel);
        var encoded = Encode(TextureFormats.Bc7Srgb, image, TextureCompressionLevel.Normal);
        Assert.All(DecodeBlock<Rgba8UNorm>(TextureFormats.Bc7UNorm, encoded), c => Assert.Equal(new(4, 2, 6, 200), c));
        ITextureCoder coder = new BptcTextureCoder(TextureFormats.Bc7Srgb);
        coder.Decode(encoded, image.AsView());
        Assert.All(image.Pixels, c =>
        {
            Assert.Equal(pixel.Red, c.Red, 8);
            Assert.Equal(pixel.Green, c.Green, 8);
            Assert.Equal(pixel.Blue, c.Blue, 8);
            Assert.Equal(pixel.Alpha, c.Alpha, 6);
        });
    }

    private static byte[] Encode<TPixel>(TextureFormat format, ArrayBitmap<TPixel> source, TextureCompressionLevel level)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ITextureCoder coder = new BptcTextureCoder(format, new() { CompressionMode = level });
        var bytes = new byte[16];
        coder.Encode(source.AsView(), bytes);
        return bytes;
    }

    private static TPixel[] DecodeBlock<TPixel>(TextureFormat format, byte[] bytes) where TPixel : unmanaged, IPixel<TPixel>
    {
        var image = new ArrayBitmap<TPixel>(4, 4);
        ITextureCoder native = new DirectXTexTextureCoder(format);
        native.Decode(bytes, image.AsView());
        return image.Pixels;
    }

    private static byte[] MakeBlocks(int prefix, int bits)
    {
        var data = new byte[16 * 128];
        new Random(61703 + prefix).NextBytes(data);
        for (int i = 0; i < data.Length; i += 16) data[i] = (byte)((data[i] & ~((1 << bits) - 1)) | prefix);
        return data;
    }

    private static TPixel[] Decode<TPixel>(ITextureCoder coder, byte[] data) where TPixel : unmanaged, IPixel<TPixel>
    {
        var image = new ArrayBitmap<TPixel>(64, 32);
        coder.Decode(data, image.AsView());
        return image.Pixels;
    }
}
