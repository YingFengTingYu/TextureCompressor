#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj
#:project ../src/TextureCompressor.Codecs.DirectXTex/TextureCompressor.Codecs.DirectXTex.csproj

using System.Diagnostics;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.DirectXTex;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;
using TextureCompressor.Options;

// From the repository root: dotnet run --file benchmarks/DxtAtc.cs -- Normal 128
// Set DOTNET_PROCESSOR_COUNT=1 and DOTNET_TieredCompilation=0 for single-thread comparisons.
var quality = args.Length > 0 ? Enum.Parse<TextureCompressionLevel>(args[0], true) : TextureCompressionLevel.Normal;
int size = args.Length > 1 ? int.Parse(args[1]) : 128;
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
var options = new TextureCompressionOptions { CompressionMode = quality };
var images = Directory.GetFiles("tests/fixtures/images/normalized", "*.png")
    .Order(StringComparer.Ordinal).Select(path => PngCodec.Decode(path)).ToArray();
var source = new ArrayBitmap<Rgba8UNorm>(size, size);
for (int y = 0; y < size; y++)
{
    for (int x = 0; x < size; x++)
    {
        int bx = x / 4, by = y / 4;
        var image = images[(bx + 3 * by) % images.Length];
        int sx = (bx * 29 + by * 17) % (image.Width / 4) * 4 + x % 4;
        int sy = (by * 37 + bx * 19) % (image.Height / 4) * 4 + y % 4;
        var pixel = image.Pixels[sy * image.Width + sx];
        pixel.Alpha = (byte)((x * 7 + y * 3 + (bx % 4 == 0 ? 0 : 29)) % 256);
        source.Pixels[y * size + x] = pixel;
    }
}

Console.WriteLine($"Quality {quality}, {size}x{size}, {Environment.ProcessorCount} logical CPUs");
Console.WriteLine("Format                               median ms   RGB dB   Alpha dB");
foreach (var format in new[]
{
    TextureFormats.Dxt1Rgb, TextureFormats.Dxt1Rgba, TextureFormats.Dxt2Rgba, TextureFormats.Dxt3Rgba,
    TextureFormats.Dxt4Rgba, TextureFormats.Dxt5Rgba, TextureFormats.AtcRgb,
    TextureFormats.AtcRgbaExplicitAlpha, TextureFormats.AtcRgbaInterpolatedAlpha
})
{
    bool atc = AtcTextureCoder.IsSupported(format);
    ITextureCoder encoder = atc ? new AtcTextureCoder(format, options) : new S3tcTextureCoder(format, options);
    var packed = new byte[encoder.GetEncodedByteCount(size, size)];
    encoder.Encode(source.AsView(), packed);
    var times = new double[5];
    for (int i = 0; i < times.Length; i++)
    {
        long start = Stopwatch.GetTimestamp();
        encoder.Encode(source.AsView(), packed);
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    Array.Sort(times);

    bool premultiplied = format == TextureFormats.Dxt2Rgba || format == TextureFormats.Dxt4Rgba;
    var storedFormat = format == TextureFormats.Dxt2Rgba ? TextureFormats.Bc2Rgba :
        format == TextureFormats.Dxt4Rgba ? TextureFormats.Bc3Rgba : format;
    ITextureCoder decoder = atc ? new AtcTextureCoder(format) : new DirectXTexTextureCoder(storedFormat);
    var result = new ArrayBitmap<Rgba8UNorm>(size, size);
    decoder.Decode(packed, result.AsView());

    long rgbError = 0, alphaError = 0;
    int visible = 0;
    bool cutout = format == TextureFormats.Dxt1Rgba;
    for (int i = 0; i < source.Pixels.Length; i++)
    {
        var p = source.Pixels[i];
        var q = result.Pixels[i];
        if (cutout && (p.Alpha < 128) != (q.Alpha == 0)) throw new InvalidDataException("DXT1 transparency changed.");
        if (cutout && q.Alpha == 0) continue;
        if (premultiplied) p = RgbaColorConversions.PremultiplyAlpha(p);
        rgbError += Square(p.Red - q.Red) + Square(p.Green - q.Green) + Square(p.Blue - q.Blue);
        alphaError += Square(p.Alpha - q.Alpha);
        visible++;
    }
    bool continuousAlpha = format.Components == TextureComponents.Rgba && !cutout;
    string alpha = continuousAlpha ? Psnr(alphaError, visible).ToString("F3") : "—";
    Console.WriteLine($"{format.Name,-35} {times[2],9:F3} {Psnr(rgbError, 3L * visible),8:F3} {alpha,10}");
}

static int Square(int value) => value * value;
static double Psnr(long squaredError, long count) => 10 * Math.Log10(255.0 * 255 * count / squaredError);
