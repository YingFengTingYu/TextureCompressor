#:project ../src/TextureCompressor.Codecs.PVRTexLib/TextureCompressor.Codecs.PVRTexLib.csproj
#:project ../src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj
#:property AllowUnsafeBlocks=true

using System.Diagnostics;
using PVRTexLib;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.PVRTexLib;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;

// dotnet run --file benchmarks/Etc2Premultiplied.cs -c Release -- input.png [comparison.png]
// The optional PNG shows reference, native ETC2_RGBA and separate RGB on black and white.
if (args.Length is < 1 or > 2) throw new ArgumentException("Expected input.png [comparison.png].");
var source = PngCodec.DecodeRgba8(args[0]);
var input = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height,
    Array.ConvertAll(source.Pixels, RgbaColorConversions.PremultiplyAlpha));
var format = TextureFormats.RgbaEtc2EacUNorm;
var native = Encode(false);
var separate = Encode(true);
// Hold the pixels, quality, dither and color space fixed; change only the native input header.
var tagged = EncodeWithNativePmaFlag();
int changedBlocks = 0;
for (int offset = 0; offset < native.Length; offset += 16)
    if (!native.AsSpan(offset, 16).SequenceEqual(tagged.AsSpan(offset, 16))) changedBlocks++;
Console.WriteLine($"Native premultiplied header changed {changedBlocks}/{native.Length / 16} blocks");
for (int offset = 0; offset < native.Length; offset += 16)
{
    if (!native.AsSpan(offset, 8).SequenceEqual(separate.AsSpan(offset, 8)))
        throw new InvalidDataException($"Alpha changed in block {offset / 16}.");
}
var before = Decode(native);
var after = Decode(separate);
ITextureCoder nativeDecoder = new PVRTexLibTextureCoder(format);
foreach (var (payload, expected) in new[] { (native, before), (separate, after) })
{
    var actual = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);
    nativeDecoder.Decode(payload, actual.AsView());
    if (!actual.Pixels.AsSpan().SequenceEqual(expected))
        throw new InvalidDataException("Managed and native decoders disagree.");
}
Console.WriteLine($"{Path.GetFileName(args[0])} {source.Width}x{source.Height}, ETCSlow, dither, RMSE in byte units");
Report("Native", before);
Report("Separate", after);
if (args.Length == 2)
{
    var comparison = new ArrayBitmap<Rgba8UNorm>(3 * source.Width, 2 * source.Height);
    Rgba8UNorm[][] columns = [input.Pixels, before, after];
    for (int column = 0; column < columns.Length; column++)
    for (int y = 0; y < source.Height; y++)
    for (int x = 0; x < source.Width; x++)
    {
        var pixel = columns[column][y * source.Width + x];
        int index = y * comparison.Width + column * source.Width + x;
        comparison.Pixels[index] = new(pixel.Red, pixel.Green, pixel.Blue, 255);
        comparison.Pixels[index + source.Height * comparison.Width] = new(
            OnWhite(pixel.Red, pixel.Alpha), OnWhite(pixel.Green, pixel.Alpha), OnWhite(pixel.Blue, pixel.Alpha), 255);
    }
    PngCodec.Encode(comparison, args[1]);
}

byte[] Encode(bool premultiplied)
{
    ITextureCoder coder = new PVRTexLibTextureCoder(format, new()
    {
        EtcQuality = PVRTexLibCompressorQuality.ETCSlow,
        Etc2InputIsPremultiplied = premultiplied,
        Dither = true,
        MaxThreads = Environment.ProcessorCount
    });
    var payload = new byte[coder.GetEncodedByteCount(input.Width, input.Height)];
    coder.Encode(input.AsView(), payload);
    var times = new double[3];
    for (int i = 0; i < times.Length; i++)
    {
        long start = Stopwatch.GetTimestamp();
        coder.Encode(input.AsView(), payload);
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    Array.Sort(times);
    Console.WriteLine($"{(premultiplied ? "Separate" : "Native")} encode {times[1]:F2} ms");
    return payload;
}

Rgba8UNorm[] Decode(byte[] payload)
{
    ITextureCoder coder = new EtcTextureCoder(format);
    var output = new ArrayBitmap<Rgba8UNorm>(input.Width, input.Height);
    coder.Decode(payload, output.AsView());
    return output.Pixels;
}

unsafe byte[] EncodeWithNativePmaFlag()
{
    ulong rgba = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 8, 8, 8, 8);
    using var header = new PVRTextureHeader(rgba, (uint)input.Width, (uint)input.Height, 1, 1, 1, 1,
        PVRTexLibColourSpace.Linear, PVRTexLibVariableType.UnsignedByteNorm, preMultiplied: true);
    fixed (Rgba8UNorm* pixels = input.Pixels)
    {
        using var texture = new PVRTexture(header, pixels);
        if (!texture.Transcode((ulong)PVRTexLibPixelFormat.ETC2_RGBA, PVRTexLibVariableType.UnsignedByteNorm,
                PVRTexLibColourSpace.Linear, PVRTexLibCompressorQuality.ETCSlow, true, 0, (uint)Environment.ProcessorCount))
            throw new InvalidOperationException("Native ETC2_RGBA encoding failed.");
        var data = texture.GetTextureDataConstPointer(0, 0, 0, 0);
        int length = checked((int)texture.GetTextureDataSize(0, false, false));
        if (data is null || length != native.Length) throw new InvalidDataException("Unexpected ETC2_RGBA payload.");
        return new ReadOnlySpan<byte>(data, length).ToArray();
    }
}

void Report(string label, Rgba8UNorm[] pixels)
{
    double black = 0, white = 0, alpha = 0;
    for (int i = 0; i < pixels.Length; i++)
    {
        var a = input.Pixels[i];
        var b = pixels[i];
        black += Square(a.Red - b.Red) + Square(a.Green - b.Green) + Square(a.Blue - b.Blue);
        white += Square(OnWhite(a.Red, a.Alpha) - OnWhite(b.Red, b.Alpha))
            + Square(OnWhite(a.Green, a.Alpha) - OnWhite(b.Green, b.Alpha))
            + Square(OnWhite(a.Blue, a.Alpha) - OnWhite(b.Blue, b.Alpha));
        alpha += Square(a.Alpha - b.Alpha);
    }
    Console.WriteLine($"{label,-8} black {Math.Sqrt(black / (3L * pixels.Length)):F2}, " +
        $"white {Math.Sqrt(white / (3L * pixels.Length)):F2}, alpha {Math.Sqrt(alpha / pixels.Length):F2}");
}

static int Square(int x) => x * x;
static byte OnWhite(byte color, byte alpha) => (byte)Math.Min(255, color + 255 - alpha);
