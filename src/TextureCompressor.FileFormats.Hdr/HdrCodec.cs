using System.Globalization;
using System.Text;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Hdr;

public static class HdrCodec
{
    private const int MaxHeaderLineLength = 8192;
    private const double MinRgbEValue = 1e-32d;
    private static readonly double MaxRgbEValue = 255d * Math.Pow(2d, 119d);

    public static bool HasRadianceHeader(ReadOnlySpan<byte> header) =>
        header.StartsWith("#?RADIANCE"u8) || header.StartsWith("#?RGBE"u8);

    public static ArrayBitmap<Rgba32Float> Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    public static ArrayBitmap<Rgba32Float> Decode(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode(stream);
    }

    public static ArrayBitmap<Rgba32Float> Decode(Stream stream) => DecodeRgba32Float(stream);

    public static ArrayBitmap<TPixel> Decode<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return Decode<TPixel>(stream);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode<TPixel>(stream);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = DecodeRgba32Float(stream);
        var pixels = new TPixel[rgba.PixelSpan.Length];
        var source = rgba.PixelSpan;
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = TPixel.FromRgba32Float(source[i]);
        }

        return new ArrayBitmap<TPixel>(rgba.Width, rgba.Height, pixels);
    }

    public static ArrayBitmap<Rgba8UNorm> DecodeRgba8(string path)
    {
        using var stream = File.OpenRead(path);
        return DecodeRgba8(stream);
    }

    public static ArrayBitmap<Rgba8UNorm> DecodeRgba8(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeRgba8(stream);
    }

    public static ArrayBitmap<Rgba8UNorm> DecodeRgba8(Stream stream) => Decode<Rgba8UNorm>(stream);

    public static ArrayBitmap<Rgba32Float> DecodeRgba32Float(string path)
    {
        using var stream = File.OpenRead(path);
        return DecodeRgba32Float(stream);
    }

    public static ArrayBitmap<Rgba32Float> DecodeRgba32Float(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeRgba32Float(stream);
    }

    public static ArrayBitmap<Rgba32Float> DecodeRgba32Float(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ReadHeader(stream);
        var pixels = new Rgba32Float[checked(header.Width * header.Height)];
        var scanline = new byte[checked(header.Width * 4)];

        for (var fileY = 0; fileY < header.Height; fileY++)
        {
            ReadScanline(stream, header.Width, scanline);

            var targetY = header.FlipY ? header.Height - 1 - fileY : fileY;
            for (var fileX = 0; fileX < header.Width; fileX++)
            {
                var targetX = header.FlipX ? header.Width - 1 - fileX : fileX;
                pixels[checked((targetY * header.Width) + targetX)] = DecodeRgbE(scanline.AsSpan(fileX * 4, 4));
            }
        }

        return new ArrayBitmap<Rgba32Float>(header.Width, header.Height, pixels);
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options: null);
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, HdrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(bitmap, options: null);

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, HdrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        Encode(bitmap, stream, options);
        return stream.ToArray();
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options: null);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, HdrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(bitmap, path, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, HdrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options: null);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, HdrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, HdrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        options ??= new HdrEncodingOptions();
        WriteAscii(stream, string.Create(
            CultureInfo.InvariantCulture,
            $"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y {bitmap.Height} +X {bitmap.Width}\n"));

        var scanline = new byte[checked(bitmap.Width * 4)];
        var useRle = options.UseRunLengthEncoding && bitmap.Width is >= 8 and <= 0x7fff;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var row = bitmap.GetRowSpan(y);
            for (var x = 0; x < bitmap.Width; x++)
            {
                EncodeRgbE(TPixel.ToRgba32Float(row[x]), scanline.AsSpan(x * 4, 4));
            }

            if (useRle)
            {
                WriteRleScanline(stream, bitmap.Width, scanline);
            }
            else
            {
                stream.Write(scanline);
            }
        }
    }

    private static HdrHeader ReadHeader(Stream stream)
    {
        var firstLine = ReadAsciiLine(stream);
        if (firstLine is null || !IsRadianceProgramType(firstLine))
        {
            throw new InvalidDataException("The stream is not a Radiance HDR file.");
        }

        string? format = null;
        while (true)
        {
            var line = ReadAsciiLine(stream);
            if (line is null)
            {
                throw new InvalidDataException("HDR header is missing the resolution line.");
            }

            if (line.Length == 0)
            {
                break;
            }

            if (line.StartsWith("FORMAT=", StringComparison.OrdinalIgnoreCase))
            {
                format = line["FORMAT=".Length..].Trim();
            }
        }

        if (format is not null && !string.Equals(format, "32-bit_rle_rgbe", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported HDR pixel format '{format}'.");
        }

        string? resolutionLine;
        do
        {
            resolutionLine = ReadAsciiLine(stream);
            if (resolutionLine is null)
            {
                throw new InvalidDataException("HDR header is missing the resolution line.");
            }
        }
        while (resolutionLine.Length == 0);

        return ParseResolution(resolutionLine);
    }

    private static HdrHeader ParseResolution(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            throw new InvalidDataException($"Invalid HDR resolution line '{line}'.");
        }

        var major = ParseAxis(parts[0], parts[1], line);
        var minor = ParseAxis(parts[2], parts[3], line);
        if (major.Axis != 'Y' || minor.Axis != 'X')
        {
            throw new NotSupportedException("Only Y-major, X-minor Radiance HDR orientation is supported.");
        }

        return new HdrHeader(
            Width: minor.Length,
            Height: major.Length,
            FlipX: minor.Sign == '-',
            FlipY: major.Sign == '+');
    }

    private static HdrAxis ParseAxis(string token, string lengthToken, string line)
    {
        if (token.Length != 2 || token[0] is not ('+' or '-'))
        {
            throw new InvalidDataException($"Invalid HDR resolution line '{line}'.");
        }

        var axis = char.ToUpperInvariant(token[1]);
        if (axis is not ('X' or 'Y')
            || !int.TryParse(lengthToken, NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            || length <= 0)
        {
            throw new InvalidDataException($"Invalid HDR resolution line '{line}'.");
        }

        return new HdrAxis(axis, token[0], length);
    }

    private static void ReadScanline(Stream stream, int width, Span<byte> scanline)
    {
        Span<byte> marker = stackalloc byte[4];
        stream.ReadExactly(marker);

        if (width is >= 8 and <= 0x7fff && IsNewRleMarker(marker))
        {
            var encodedWidth = (marker[2] << 8) | marker[3];
            if (encodedWidth != width)
            {
                throw new InvalidDataException("HDR RLE scanline width does not match the header.");
            }

            ReadRleScanline(stream, width, scanline);
            return;
        }

        ReadFlatScanline(stream, width, marker, scanline);
    }

    private static bool IsNewRleMarker(ReadOnlySpan<byte> marker) =>
        marker[0] == 2 && marker[1] == 2 && (marker[2] & 0x80) == 0;

    private static void ReadRleScanline(Stream stream, int width, Span<byte> scanline)
    {
        for (var channel = 0; channel < 4; channel++)
        {
            var x = 0;
            while (x < width)
            {
                var code = ReadByteOrThrow(stream);
                if (code == 0)
                {
                    throw new InvalidDataException("HDR RLE scanline contains an empty packet.");
                }

                if (code > 128)
                {
                    var count = code - 128;
                    if (x + count > width)
                    {
                        throw new InvalidDataException("HDR RLE packet overruns the scanline.");
                    }

                    var value = ReadByteOrThrow(stream);
                    for (var i = 0; i < count; i++)
                    {
                        scanline[((x + i) * 4) + channel] = value;
                    }

                    x += count;
                }
                else
                {
                    var count = code;
                    if (x + count > width)
                    {
                        throw new InvalidDataException("HDR RLE literal packet overruns the scanline.");
                    }

                    for (var i = 0; i < count; i++)
                    {
                        scanline[((x + i) * 4) + channel] = ReadByteOrThrow(stream);
                    }

                    x += count;
                }
            }
        }
    }

    private static void ReadFlatScanline(
        Stream stream,
        int width,
        ReadOnlySpan<byte> firstPixel,
        Span<byte> scanline)
    {
        Span<byte> previous = stackalloc byte[4];
        Span<byte> rgbe = stackalloc byte[4];
        var x = 0;
        var repeatShift = 0;

        AppendFlatPixel(firstPixel, scanline, previous, width, ref x, ref repeatShift);
        while (x < width)
        {
            stream.ReadExactly(rgbe);
            AppendFlatPixel(rgbe, scanline, previous, width, ref x, ref repeatShift);
        }
    }

    private static void AppendFlatPixel(
        ReadOnlySpan<byte> rgbe,
        Span<byte> scanline,
        Span<byte> previous,
        int width,
        ref int x,
        ref int repeatShift)
    {
        if (rgbe[0] == 1 && rgbe[1] == 1 && rgbe[2] == 1)
        {
            if (x == 0)
            {
                throw new InvalidDataException("HDR old-style RLE repeat appears before any pixel.");
            }

            if (repeatShift > 24)
            {
                throw new InvalidDataException("HDR old-style RLE repeat count is too large.");
            }

            var repeatCount = rgbe[3] << repeatShift;
            repeatShift += 8;
            if (repeatCount <= 0 || x + repeatCount > width)
            {
                throw new InvalidDataException("HDR old-style RLE repeat overruns the scanline.");
            }

            for (var i = 0; i < repeatCount; i++)
            {
                previous.CopyTo(scanline.Slice((x + i) * 4, 4));
            }

            x += repeatCount;
            return;
        }

        rgbe.CopyTo(scanline.Slice(x * 4, 4));
        rgbe.CopyTo(previous);
        x++;
        repeatShift = 0;
    }

    private static void WriteRleScanline(Stream stream, int width, ReadOnlySpan<byte> scanline)
    {
        stream.WriteByte(2);
        stream.WriteByte(2);
        stream.WriteByte((byte)(width >> 8));
        stream.WriteByte((byte)width);

        for (var channel = 0; channel < 4; channel++)
        {
            WriteRleChannel(stream, width, scanline, channel);
        }
    }

    private static void WriteRleChannel(Stream stream, int width, ReadOnlySpan<byte> scanline, int channel)
    {
        var x = 0;
        while (x < width)
        {
            var runLength = CountRun(scanline, width, channel, x);
            if (runLength >= 4)
            {
                stream.WriteByte((byte)(128 + runLength));
                stream.WriteByte(scanline[(x * 4) + channel]);
                x += runLength;
                continue;
            }

            var literalStart = x;
            x += runLength;
            while (x < width && x - literalStart < 128)
            {
                runLength = CountRun(scanline, width, channel, x);
                if (runLength >= 4)
                {
                    break;
                }

                x += runLength;
            }

            var literalCount = x - literalStart;
            stream.WriteByte((byte)literalCount);
            for (var i = 0; i < literalCount; i++)
            {
                stream.WriteByte(scanline[((literalStart + i) * 4) + channel]);
            }
        }
    }

    private static int CountRun(ReadOnlySpan<byte> scanline, int width, int channel, int x)
    {
        var value = scanline[(x * 4) + channel];
        var length = 1;
        var maxLength = Math.Min(width - x, 127);
        while (length < maxLength && scanline[((x + length) * 4) + channel] == value)
        {
            length++;
        }

        return length;
    }

    private static Rgba32Float DecodeRgbE(ReadOnlySpan<byte> rgbe)
    {
        if (rgbe[3] == 0)
        {
            return new Rgba32Float(0f, 0f, 0f, 1f);
        }

        var scale = MathF.Pow(2f, rgbe[3] - 136);
        return new Rgba32Float(rgbe[0] * scale, rgbe[1] * scale, rgbe[2] * scale, 1f);
    }

    private static void EncodeRgbE(Rgba32Float source, Span<byte> destination)
    {
        var red = CleanChannel(source.Red);
        var green = CleanChannel(source.Green);
        var blue = CleanChannel(source.Blue);
        var max = Math.Max(red, Math.Max(green, blue));
        if (max < MinRgbEValue)
        {
            destination.Clear();
            return;
        }

        var exponent = (int)Math.Floor(Math.Log2(max)) + 1;
        exponent = Math.Clamp(exponent, -128, 127);
        var scale = Math.Pow(2d, 8 - exponent);

        destination[0] = ToRgbEByte(red * scale);
        destination[1] = ToRgbEByte(green * scale);
        destination[2] = ToRgbEByte(blue * scale);
        destination[3] = checked((byte)(exponent + 128));
    }

    private static double CleanChannel(float value)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0d;
        }

        return double.IsPositiveInfinity(value) || value > MaxRgbEValue
            ? MaxRgbEValue
            : value;
    }

    private static byte ToRgbEByte(double value) =>
        (byte)Math.Clamp((int)value, 0, byte.MaxValue);

    private static bool IsRadianceProgramType(string value) =>
        string.Equals(value, "#?RADIANCE", StringComparison.Ordinal)
        || string.Equals(value, "#?RGBE", StringComparison.Ordinal);

    private static string? ReadAsciiLine(Stream stream)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                return bytes.Count == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray());
            }

            if (value == '\n')
            {
                if (bytes.Count > 0 && bytes[^1] == '\r')
                {
                    bytes.RemoveAt(bytes.Count - 1);
                }

                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            if (bytes.Count >= MaxHeaderLineLength)
            {
                throw new InvalidDataException("HDR header line is too long.");
            }

            bytes.Add((byte)value);
        }
    }

    private static byte ReadByteOrThrow(Stream stream)
    {
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException();
        }

        return (byte)value;
    }

    private static void WriteAscii(Stream stream, string value) =>
        stream.Write(Encoding.ASCII.GetBytes(value));

    private readonly record struct HdrHeader(int Width, int Height, bool FlipX, bool FlipY);

    private readonly record struct HdrAxis(char Axis, char Sign, int Length);
}
