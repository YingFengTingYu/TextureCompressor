#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj
#:project ../src/TextureCompressor.Codecs.BasisUniversal/TextureCompressor.Codecs.BasisUniversal.csproj

using System.Diagnostics;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.BasisUniversal;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;

// dotnet run --file benchmarks/BasisUastc.cs -c Release
const int size = 128;
var format = TextureFormats.RgbaBasisUastcLdr4x4UNorm;
ITextureCoder native = new BasisUniversalTextureCoder(format);
ITextureCoder managed = new BasisUastcLdr4x4TextureCoder(format);
foreach (var path in Directory.GetFiles("tests/fixtures/images/normalized", "*.png").Order(StringComparer.Ordinal))
{
    var original = PngCodec.DecodeRgba8(path);
    var source = new ArrayBitmap<Rgba8UNorm>(size, size);
    for (var y = 0; y < size; y++)
    for (var x = 0; x < size; x++)
        source.Pixels[y * size + x] = original.Pixels[(y * original.Height / size) * original.Width + x * original.Width / size];
    foreach (var encoder in new[] { managed, native })
    {
        var data = new byte[encoder.GetEncodedByteCount(size, size)];
        encoder.Encode(source.AsView(), data);
        var times = new double[5];
        for (var i = 0; i < times.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            encoder.Encode(source.AsView(), data);
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(times);
        var output = new ArrayBitmap<Rgba8UNorm>(size, size);
        native.Decode(data, output.AsView());
        long error = 0;
        for (var i = 0; i < source.Pixels.Length; i++)
        {
            var a = source.Pixels[i];
            var b = output.Pixels[i];
            error += Square(a.Red - b.Red) + Square(a.Green - b.Green) + Square(a.Blue - b.Blue) + Square(a.Alpha - b.Alpha);
        }
        var psnr = 10 * Math.Log10((4.0 * size * size * 255 * 255) / error);
        Console.WriteLine($"{Path.GetFileName(path),24} {(encoder == native ? "Basis Universal" : "Managed"),16}  {times[2],8:F2} ms  {psnr,6:F2} dB RGBA");
    }
}
static int Square(int x) => x * x;
