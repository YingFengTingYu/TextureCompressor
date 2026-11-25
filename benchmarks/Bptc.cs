#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj
#:project ../src/TextureCompressor.FileFormats.Dds/TextureCompressor.FileFormats.Dds.csproj
#:project ../src/TextureCompressor.Codecs.DirectXTex/TextureCompressor.Codecs.DirectXTex.csproj

using System.Diagnostics;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.DirectXTex;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;
using TextureCompressor.Options;

// dotnet run --file benchmarks/Bptc.cs -c Release -- Normal 32 [referenceDirectory]
// For reproducible CPU timings set DOTNET_PROCESSOR_COUNT=1 and DOTNET_TieredCompilation=0.
// The optional directory receives the source DDS. Place tightly packed BC blocks from an
// external encoder in <format>.reference.bin to evaluate them with the same DirectXTex decoder.
var level = args.Length > 0 ? Enum.Parse<TextureCompressionLevel>(args[0], true) : TextureCompressionLevel.Normal;
int size = args.Length > 1 ? int.Parse(args[1]) : 32;
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
var images = Directory.GetFiles("tests/fixtures/images/normalized", "*.png")
    .Order(StringComparer.Ordinal).Select(path => PngCodec.Decode(path)).ToArray();
foreach (var format in new[] { TextureFormats.Bc7UNorm, TextureFormats.Bc6HUFloat, TextureFormats.Bc6HSFloat })
{
    bool hdr = format != TextureFormats.Bc7UNorm, signed = format == TextureFormats.Bc6HSFloat;
    var source = new ArrayBitmap<Rgba32Float>(size, size);
    for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++)
    {
        int bx = x / 4, by = y / 4;
        var image = images[(bx + 3 * by) % images.Length];
        int sx = (bx * 29 + by * 17) % (image.Width / 4) * 4 + x % 4;
        int sy = (by * 37 + bx * 19) % (image.Height / 4) * 4 + y % 4;
        var pixel = image.Pixels[sy * image.Width + sx];
        float exposure = hdr ? MathF.Pow(2, (bx * 3 + by * 7) % 16 - 5) : 1;
        float Channel(byte v) => hdr ? (float)(Half)(exposure * (v / 255f - (signed ? 0.45f : 0))) : v / 255f;
        byte alpha = (bx + by) % 2 == 0 ? (byte)255 : (byte)((x * 43 + y * 21) & 255);
        source.Pixels[y * size + x] = new(Channel(pixel.Red), Channel(pixel.Green), Channel(pixel.Blue), alpha / 255f);
    }
    ITextureCoder coder = new BptcTextureCoder(format, new() { CompressionMode = level });
    var data = new byte[coder.GetEncodedByteCount(size, size)];
    coder.Encode(source.AsView(), data);
    var times = new double[5];
    for (int i = 0; i < times.Length; i++)
    {
        long start = Stopwatch.GetTimestamp();
        coder.Encode(source.AsView(), data);
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    Array.Sort(times);
    ITextureCoder native = new DirectXTexTextureCoder(format);
    var decoded = new ArrayBitmap<Rgba32Float>(size, size);
    native.Decode(data, decoded.AsView());
    string Quality()
    {
        double error = 0, logError = 0;
        for (int i = 0; i < source.Pixels.Length; i++)
        {
            var a = source.Pixels[i];
            var b = decoded.Pixels[i];
            error += Square(a.Red - b.Red) + Square(a.Green - b.Green) + Square(a.Blue - b.Blue);
            if (hdr)
            {
                logError += Square(SignedLog(a.Red) - SignedLog(b.Red)) + Square(SignedLog(a.Green) - SignedLog(b.Green))
                    + Square(SignedLog(a.Blue) - SignedLog(b.Blue));
            }
            else error += Square(a.Alpha - b.Alpha);
        }
        double mse = error / (source.Pixels.Length * (hdr ? 3 : 4));
        return hdr ? $"RGB RMSE {Math.Sqrt(mse):F4}, signed-log RMSE {Math.Sqrt(logError / (3 * source.Pixels.Length)):F4}"
            : $"RGBA PSNR {-10 * Math.Log10(mse):F4} dB";
    }
    Console.WriteLine($"{format.Name} {level} {size}x{size}  {times[2]:F3} ms  {Quality()}");
    if (args.Length > 2)
    {
        Directory.CreateDirectory(args[2]);
        string prefix = Path.Combine(args[2], format.Name);
        DdsCodec.Encode(source, prefix + ".dds", new() { TextureFormat = hdr ? TextureFormats.Rgba16Float : TextureFormats.Rgba8UNorm });
        if (File.Exists(prefix + ".reference.bin"))
        {
            var reference = File.ReadAllBytes(prefix + ".reference.bin");
            if (reference.Length != data.Length) throw new InvalidDataException("Reference block count differs from the source.");
            native.Decode(reference, decoded.AsView());
            Console.WriteLine($"  external reference {Quality()}");
        }
    }
}

static double Square(double v) => v * v;

static double SignedLog(float value) => Math.CopySign(Math.Log(1 + Math.Abs((double)value)), value);
