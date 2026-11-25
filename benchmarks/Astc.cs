#:project ../src/TextureCompressor.Codecs.AstcEnc/TextureCompressor.Codecs.AstcEnc.csproj
#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj

using System.Diagnostics;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.AstcEnc;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;

// dotnet run --file benchmarks/Astc.cs -c Release
var image = PngCodec.DecodeRgba8("tests/fixtures/images/normalized/natural-scene-512.png");
foreach (var format in new[] { TextureFormats.RgbaAstc4x4UNorm, TextureFormats.RgbaAstc6x6UNorm,
    TextureFormats.RgbaAstc8x8Srgb, TextureFormats.RgbaAstc6x6Float,
    TextureFormats.RgbaAstc3x3x3UNorm, TextureFormats.RgbaAstc4x4x4Float })
{
    bool volume = format.BlockDepth > 1;
    int width = volume ? 13 : 65, height = volume ? 11 : 61, depth = volume ? 7 : 1;
    var source = new Rgba32Float[width * height * depth];
    for (var z = 0; z < depth; z++)
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var p = image.Pixels[(y * image.Height / height) * image.Width + x * image.Width / width];
        var scale = format.ValueKind == TextureValueKind.Float ? 8f : 1f;
        source[(z * height + y) * width + x] = new(scale * p.Red / 255, scale * p.Green / 255,
            scale * (depth == 1 ? p.Blue / 255f : z / (depth - 1f)), p.Alpha / 255f);
    }

    ITextureCoder? native2D = volume ? null : new AstcEncTextureCoder(format);
    ITextureCoder? managed2D = volume ? null : new AstcTextureCoder(format);
    ITextureCoder3D? native3D = volume ? new AstcEnc3DTextureCoder(format) : null;
    ITextureCoder3D? managed3D = volume ? new Astc3DTextureCoder(format) : null;
    var count = volume ? native3D!.GetEncodedByteCount(width, height, depth) : native2D!.GetEncodedByteCount(width, height);
    foreach (var nativeEncode in new[]{ false, true })
    {
        var data = new byte[count];
        void Encode()
        {
            if (volume) (nativeEncode ? native3D : managed3D)!.Encode(new VolumeBitmapView<Rgba32Float>(source, width, height, depth), data);
            else (nativeEncode ? native2D : managed2D)!.Encode(new BitmapView<Rgba32Float>(source, width, height), data);
        }
        Encode();
        var times = new double[3];
        for (var i = 0; i < times.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            Encode();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(times);
        Rgba32Float[] Decode(bool native)
        {
            var output = new Rgba32Float[source.Length];
            if (volume) (native ? native3D : managed3D)!.Decode(data, new VolumeBitmapView<Rgba32Float>(output, width, height, depth));
            else (native ? native2D : managed2D)!.Decode(data, new BitmapView<Rgba32Float>(output, width, height));
            return output;
        }
        var reference = Decode(true);
        var check = Decode(false);
        double error = 0, max = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var a = source[i]; var b = reference[i]; var c = check[i];
            error += Math.Pow(a.Red - b.Red, 2) + Math.Pow(a.Green - b.Green, 2) + Math.Pow(a.Blue - b.Blue, 2);
            max = Math.Max(max, Math.Max(Math.Max(Math.Abs(b.Red-c.Red), Math.Abs(b.Green-c.Green)),
                Math.Max(Math.Abs(b.Blue-c.Blue), Math.Abs(b.Alpha-c.Alpha))));
        }
        Console.WriteLine($"{format.Name} {(nativeEncode ? "astcenc" : "managed")} {times[1]:F2} ms, RGB RMSE {Math.Sqrt(error / (source.Length*3)):F5}, decoder max delta {max:G4}");
    }
}
