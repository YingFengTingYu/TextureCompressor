#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj

using System.Diagnostics;
using System.Runtime.InteropServices;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;
using TextureCompressor.Options;

// dotnet run --file benchmarks/Fxt1.cs -c Release -- [blocks.bin reference-rgba8.bin]
// Each 16-byte block has 32 reference pixels in 8x4 raster order.
if (args.Length == 2)
{
    var blocks = File.ReadAllBytes(args[0]);
    var reference = File.ReadAllBytes(args[1]);
    if (blocks.Length % 16 != 0 || reference.LongLength != blocks.LongLength * 8)
        throw new ArgumentException("Expected 128 reference RGBA bytes per FXT1 block.");
    foreach (var format in new[]{ TextureFormats.RgbFxt1UNorm, TextureFormats.RgbaFxt1UNorm })
    {
        ITextureCoder coder = new FxtcTextureCoder(format);
        var output = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var maxima = new int[8];
        for (var offset = 0; offset < blocks.Length; offset += 16)
        {
            coder.Decode(blocks.AsSpan(offset, 16), output.AsView());
            var actual = MemoryMarshal.AsBytes(output.PixelSpan);
            var mode = blocks[offset + 15] >> 5;
            for (var i = 0; i < actual.Length; i++)
            {
                var expected = format == TextureFormats.RgbFxt1UNorm && i % 4 == 3 ? 255 : reference[offset * 8 + i];
                maxima[mode] = Math.Max(maxima[mode], Math.Abs(actual[i] - expected));
            }
        }
        Console.WriteLine($"{format.Name}, max channel delta by mode field 0..7: {string.Join(", ", maxima)}");
    }
}
else if (args.Length != 0) throw new ArgumentException("Expected either zero arguments or two corpus files.");

var image = PngCodec.DecodeRgba8("tests/fixtures/images/normalized/natural-scene-512.png");
const int size = 32;
foreach (var format in new[]{ TextureFormats.RgbFxt1UNorm, TextureFormats.RgbaFxt1UNorm })
{
    var source = new ArrayBitmap<Rgba8UNorm>(size, size);
    for (var y = 0; y < size; y++)
    for (var x = 0; x < size; x++)
    {
        var p = image.Pixels[y * image.Height / size * image.Width + x * image.Width / size];
        p.Alpha = format == TextureFormats.RgbaFxt1UNorm ? (byte)((x * 7 + y * 3) % 256) : (byte)255;
        source.Pixels[y * size + x] = p;
    }
    foreach (var quality in Enum.GetValues<TextureCompressionLevel>())
    {
        ITextureCoder coder = new FxtcTextureCoder(format, new TextureCompressionOptions { CompressionMode = quality });
        var data = new byte[coder.GetEncodedByteCount(size, size)];
        coder.Encode(source.AsView(), data);
        var times = new double[3];
        for (var i = 0; i < times.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            coder.Encode(source.AsView(), data);
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(times);
        var output = new ArrayBitmap<Rgba8UNorm>(size, size);
        coder.Decode(data, output.AsView());
        var input = MemoryMarshal.AsBytes(source.PixelSpan);
        var decoded = MemoryMarshal.AsBytes(output.PixelSpan);
        long error = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var delta = input[i] - decoded[i];
            error += delta * delta;
        }
        Console.WriteLine($"{format.Name} {quality} {times[1]:F2} ms, RGBA RMSE {Math.Sqrt((double)error/input.Length):F2} bytes");
    }
}
