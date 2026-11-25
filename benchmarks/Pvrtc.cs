#:project ../src/TextureCompressor.Codecs.PVRTexLib/TextureCompressor.Codecs.PVRTexLib.csproj
#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj

using System.Diagnostics;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.PVRTexLib;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;

// dotnet run --file benchmarks/Pvrtc.cs -c Release -- [straight-alpha sprite.png]
// The sprite is premultiplied once and padded to a PVRTC-I-compatible square.
var image = PngCodec.DecodeRgba8(args.Length > 0 ? args[0] : "tests/fixtures/images/normalized/natural-scene-512.png");
var size = 1;
while (size < Math.Max(image.Width, image.Height)) size *= 2;
var ldr = new ArrayBitmap<Rgba8UNorm>(size, size);
for (var y = 0; y < image.Height; y++)
for (var x = 0; x < image.Width; x++)
    ldr.Pixels[y * size + x] = RgbaColorConversions.PremultiplyAlpha(image.Pixels[y * image.Width + x]);

foreach (var f in new[] { TextureFormats.RgbaPvrtcI2BppUNorm, TextureFormats.RgbaPvrtcI4BppUNorm,
    TextureFormats.RgbaPvrtcII2BppUNorm, TextureFormats.RgbaPvrtcII4BppUNorm })
{
    ITextureCoder native = new PVRTexLibTextureCoder(f);
    ITextureCoder managed = new PvrtcTextureCoder(f);
    foreach (var encoder in new[] { managed, native })
    {
        var data = Measure(encoder, ldr, out var ms);
        var output = new ArrayBitmap<Rgba8UNorm>(size, size);
        var check = new ArrayBitmap<Rgba8UNorm>(size, size);
        native.Decode(data, output.AsView());
        managed.Decode(data, check.AsView());
        var maxDelta = 0;
        double compositeError = 0;
        long samples = 0;
        for (var i = 0; i < ldr.Pixels.Length; i++)
        {
            var a = ldr.Pixels[i]; var b = output.Pixels[i]; var c = check.Pixels[i];
            maxDelta = Math.Max(maxDelta, Math.Max(Math.Max(Math.Abs(b.Red - c.Red), Math.Abs(b.Green - c.Green)),
                Math.Max(Math.Abs(b.Blue - c.Blue), Math.Abs(b.Alpha - c.Alpha))));
            if (args.Length > 0 && a.Alpha is 0 or 255) continue;
            int da = a.Alpha - b.Alpha;
            compositeError += CompositeError(a.Red - b.Red, da) + CompositeError(a.Green - b.Green, da)
                + CompositeError(a.Blue - b.Blue, da);
            samples += 6;
        }
        var edgeMetric = samples == 0 ? "no translucent samples" : $"{Math.Sqrt(compositeError / samples):F2} bytes";
        Console.WriteLine($"{f.Name} {(encoder == managed ? "managed" : "PVRTexLib")} {ms:F2} ms, black/white RMSE {edgeMetric}, decoder max delta {maxDelta}");
    }
}

var hdr = new ArrayBitmap<Rgba32Float>(32, 32);
for (var y = 0; y < hdr.Height; y++)
for (var x = 0; x < hdr.Width; x++)
    hdr.Pixels[y * hdr.Width + x] = new Rgba32Float(0.25f + x / 4f, 0.25f + y / 6f, (1 + x + y) / 8f, 1);
foreach (var f in new[]{ TextureFormats.RgbPvrtcI6BppFloat, TextureFormats.RgbPvrtcI8BppFloat,
    TextureFormats.RgbPvrtcII6BppFloat, TextureFormats.RgbPvrtcII8BppFloat })
{
    ITextureCoder native = new PVRTexLibTextureCoder(f);
    ITextureCoder managed = new PvrtcTextureCoder(f);
    foreach (var encoder in new[] { managed, native })
    {
        var data = Measure(encoder, hdr, out var ms);
        var output = new ArrayBitmap<Rgba32Float>(32, 32);
        var check = new ArrayBitmap<Rgba32Float>(32, 32);
        native.Decode(data, output.AsView());
        managed.Decode(data, check.AsView());
        double error = 0, maxDelta = 0;
        for (var i = 0; i < hdr.Pixels.Length; i++)
        {
            var a = hdr.Pixels[i]; var b = output.Pixels[i]; var c = check.Pixels[i];
            error += Math.Pow(a.Red-b.Red,2) + Math.Pow(a.Green-b.Green,2) + Math.Pow(a.Blue-b.Blue,2);
            maxDelta = Math.Max(maxDelta, Math.Max(Math.Abs(b.Red-c.Red), Math.Max(Math.Abs(b.Green-c.Green), Math.Abs(b.Blue-c.Blue))));
        }
        Console.WriteLine($"{f.Name} {(encoder == managed ? "managed" : "PVRTexLib")} {ms:F2} ms, HDR RMSE {Math.Sqrt(error/(3*hdr.Pixels.Length)):F4}, decoder max delta {maxDelta:G3}");
    }
}

static int CompositeError(int delta, int alphaDelta) => delta * delta + (delta - alphaDelta) * (delta - alphaDelta);

static byte[] Measure<T>(ITextureCoder coder, ArrayBitmap<T> source, out double medianMs) where T : unmanaged, IPixel<T>
{
    var data = new byte[coder.GetEncodedByteCount(source.Width, source.Height)];
    coder.Encode(source.AsView(), data);
    var times = new double[3];
    for (var i = 0; i < times.Length; i++)
    {
        var start = Stopwatch.GetTimestamp();
        coder.Encode(source.AsView(), data);
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    Array.Sort(times);
    medianMs = times[1];
    return data;
}
