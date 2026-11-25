#:project ../src/TextureCompressor/TextureCompressor.csproj

using System.Buffers.Binary;
using System.Diagnostics;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Options;

// dotnet run --file benchmarks/Bc4Bc5.cs -c Release -- Normal 64
var quality = args.Length > 0 ? Enum.Parse<TextureCompressionLevel>(args[0], true) : TextureCompressionLevel.Normal;
int size = args.Length > 1 ? int.Parse(args[1]) : 64;
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
var options = new TextureCompressionOptions { CompressionMode = quality };
Console.WriteLine($"{quality}, {size}x{size}, {Environment.ProcessorCount} logical CPUs");
foreach (var format in new[] { TextureFormats.Bc4UNorm, TextureFormats.Bc4SNorm, TextureFormats.Bc5UNorm,
    TextureFormats.Bc5SNorm, TextureFormats.Latc2UNorm, TextureFormats.Dxn, TextureFormats.Ctx1 })
{
    var source = new ArrayBitmap<Rgba32Float>(size, size);
    bool signed = format.ValueKind == TextureValueKind.SNorm;
    bool second = format.BytesPerBlock == 16 || format == TextureFormats.Ctx1;
    var random = new Random(419);
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            float r = (float)(0.5 + 0.43 * Math.Sin(x * 0.21 + y * 0.13) + (random.NextDouble() - 0.5) * 0.1);
            float g = Math.Clamp((x / 4 % 2 == 0 ? 1 - r : r) + 0.06f * MathF.Cos(y * 0.7f), 0, 1);
            if (signed) { r = 2 * r - 1; g = 2 * g - 1; }
            source.Pixels[y * size + x] = new Rgba32Float(r, g, 0, g);
        }
    }
    ITextureCoder encoder = RgtcLatcTextureCoder.IsSupported(format)
        ? new RgtcLatcTextureCoder(format, options) : new S3tcTextureCoder(format, options);
    var bytes = new byte[encoder.GetEncodedByteCount(size, size)];
    encoder.Encode(source.AsView(), bytes);
    var times = new double[5];
    for (int i = 0; i < times.Length; i++)
    {
        long start = Stopwatch.GetTimestamp();
        encoder.Encode(source.AsView(), bytes);
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    Array.Sort(times);
    // Evaluate the encoded bytes independently of the encoder's own decoder.
    double error = 0;
    for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++)
    {
        var block = bytes.AsSpan(((y / 4) * ((size + 3) / 4) + x / 4) * format.BytesPerBlock);
        int texel = (y % 4) * 4 + x % 4;
        for (int c = 0; c < (second ? 2 : 1); c++)
        {
            float value;
            if (format == TextureFormats.Ctx1)
            {
                int a = block[1 - c], b = block[3 - c];
                int index = (int)(BinaryPrimitives.ReadUInt32LittleEndian(block[4..]) >> (2 * texel)) & 3;
                value = (index switch { 0 => a, 1 => b, 2 => (2 * a + b) / 3, _ => (a + 2 * b) / 3 }) / 255f;
            }
            else
            {
                var scalar = block[(c * 8)..];
                int a = signed ? Math.Max(-127, (int)(sbyte)scalar[0]) : scalar[0];
                int b = signed ? Math.Max(-127, (int)(sbyte)scalar[1]) : scalar[1];
                int index = (int)(BinaryPrimitives.ReadUInt64LittleEndian(scalar) >> (16 + 3 * texel)) & 7;
                int n = a > b ? 7 : 5;
                value = index switch { 0 => a, 1 => b, 6 when n == 5 => signed ? -127 : 0,
                    7 when n == 5 => signed ? 127 : 255, _ => ((n + 1 - index) * a + (index - 1) * b) / (float)n };
                if (format == TextureFormats.Dxn) value = MathF.Floor(value);
                value /= signed ? 127 : 255;
            }
            var pixel = source.Pixels[y * size + x];
            float expected = c == 0 ? pixel.Red : pixel.Green;
            error += Math.Pow(expected - value, 2);
        }
    }
    double peak = signed ? 2 : 1;
    double psnr = 10 * Math.Log10(peak * peak * size * size * (second ? 2 : 1) / error);
    Console.WriteLine($"{format.Name,-18} {times[2],9:F3} ms  {psnr,7:F3} dB");
}
