using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Png;

public static class PngCodec
{
    private const int DefaultMaxIdatChunkDataLength = 0x7fff;
    private static readonly byte[] CgbiChunkData = [0x50, 0x00, 0x20, 0x02];

    private static readonly byte[] Signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static readonly Adam7Pass[] Adam7Passes =
    [
        new(0, 0, 8, 8),
        new(4, 0, 8, 8),
        new(0, 4, 4, 8),
        new(2, 0, 4, 4),
        new(0, 2, 2, 4),
        new(1, 0, 2, 2),
        new(0, 1, 1, 2)
    ];

    public static ArrayBitmap<Rgba8UNorm> Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode(stream);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(Stream stream) => DecodeRgba8(stream);

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
        var rgba = DecodeRgba8(stream);
        var pixels = new TPixel[rgba.PixelSpan.Length];
        var source = rgba.PixelSpan;
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = TPixel.FromRgba8UNorm(source[i]);
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

    public static ArrayBitmap<Rgba8UNorm> DecodeRgba8(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Byte8Buffer signatureBuffer = default;
        Span<byte> signature = signatureBuffer;
        ReadExactly(stream, signature);
        if (!((ReadOnlySpan<byte>)signature).SequenceEqual(Signature))
        {
            throw new InvalidDataException("The stream is not a PNG file.");
        }

        PngHeader? header = null;
        var isAppleCgbi = false;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();
        var sawEnd = false;

        while (!sawEnd)
        {
            var chunk = ReadChunk(stream);
            switch (chunk.Type)
            {
                case "IHDR":
                    if (header is not null)
                    {
                        throw new InvalidDataException("PNG contains more than one IHDR chunk.");
                    }

                    header = ReadHeader(chunk.Data);
                    break;

                case "CgBI":
                    if (header is not null)
                    {
                        throw new InvalidDataException("PNG CgBI chunk must appear before IHDR.");
                    }

                    isAppleCgbi = true;
                    break;

                case "IDAT":
                    if (header is null)
                    {
                        throw new InvalidDataException("PNG IDAT chunk appears before IHDR.");
                    }

                    idat.Write(chunk.Data);
                    break;

                case "PLTE":
                    ValidatePalette(chunk.Data);
                    palette = chunk.Data;
                    break;

                case "IEND":
                    sawEnd = true;
                    break;

                case "tRNS":
                    transparency = chunk.Data;
                    break;

                case "acTL":
                case "fcTL":
                case "fdAT":
                    throw new NotSupportedException("Animated PNG chunks are not supported yet.");

                default:
                    if (IsCriticalChunk(chunk.Type))
                    {
                        throw new NotSupportedException($"Unsupported critical PNG chunk '{chunk.Type}'.");
                    }

                    break;
            }
        }

        if (header is null)
        {
            throw new InvalidDataException("PNG is missing an IHDR chunk.");
        }

        if (idat.Length == 0)
        {
            throw new InvalidDataException("PNG is missing IDAT data.");
        }

        idat.Position = 0;
        return DecodeImage(header.Value, idat, palette, transparency, isAppleCgbi);
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options: null);
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, PngEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(bitmap, options: null);

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, PngEncodingOptions? options)
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

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, PngEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(bitmap, path, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, PngEncodingOptions? options)
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

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, PngEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, PngEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        ValidateEncodingOptions(options);
        var layout = GetEncodingLayout<TPixel>(options);
        stream.Write(Signature);
        if (layout.UseAppleCgbi)
        {
            WriteChunk(stream, "CgBI", CgbiChunkData);
        }

        WriteHeader(stream, bitmap.Width, bitmap.Height, layout);

        using var idatStream = new IdatChunkStream(stream, options?.MaxIdatChunkDataLength ?? DefaultMaxIdatChunkDataLength);
        using (var compressed = CreateCompressedStream(idatStream, layout.UseAppleCgbi, options?.CompressionLevel ?? CompressionLevel.SmallestSize))
        {
            WriteScanlines(bitmap, layout, compressed);
        }

        idatStream.Finish();
        WriteChunk(stream, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeImage(
        PngHeader header,
        Stream compressedData,
        byte[]? palette,
        byte[]? transparency,
        bool isAppleCgbi)
    {
        ValidateHeader(header);
        ValidateCgbi(header, transparency, isAppleCgbi);

        var bitsPerPixel = GetBitsPerPixel(header.ColorType, header.BitDepth);
        var filterBytesPerPixel = GetFilterBytesPerPixel(bitsPerPixel);
        using var compressed = CreateCompressedStream(compressedData, isAppleCgbi);
        var pixels = header.InterlaceMethod == 0
            ? DecodeNonInterlaced(compressed, header.Width, header.Height, bitsPerPixel, filterBytesPerPixel)
            : DecodeAdam7(compressed, header.Width, header.Height, bitsPerPixel, filterBytesPerPixel);
        if (isAppleCgbi)
        {
            ConvertCgbiBgraToRgba(pixels);
        }

        return (header.ColorType, header.BitDepth) switch
        {
            (PngColorType.Grayscale, 1 or 2 or 4) => DecodePackedGrayscale(header.Width, header.Height, header.BitDepth, pixels, transparency),
            (PngColorType.Grayscale, 8) => DecodeGrayscale8(header.Width, header.Height, pixels, transparency),
            (PngColorType.Grayscale, 16) => DecodeGrayscale16(header.Width, header.Height, pixels, transparency),
            (PngColorType.GrayscaleAlpha, 8) => DecodeGrayscaleAlpha8(header.Width, header.Height, pixels),
            (PngColorType.GrayscaleAlpha, 16) => DecodeGrayscaleAlpha16(header.Width, header.Height, pixels),
            (PngColorType.Truecolor, 8) => DecodeRgb8(header.Width, header.Height, pixels, transparency),
            (PngColorType.Truecolor, 16) => DecodeRgb16(header.Width, header.Height, pixels, transparency),
            (PngColorType.IndexedColor, 1 or 2 or 4 or 8) => DecodeIndexed(header.Width, header.Height, header.BitDepth, pixels, palette, transparency),
            (PngColorType.TruecolorAlpha, 8) => DecodeRgba8(header.Width, header.Height, pixels),
            (PngColorType.TruecolorAlpha, 16) => DecodeRgba16(header.Width, header.Height, pixels),
            _ => throw new NotSupportedException($"PNG color type {header.ColorType} with bit depth {header.BitDepth} is not supported.")
        };
    }

    private static ArrayBitmap<Rgba8UNorm> DecodePackedGrayscale(int width, int height, byte bitDepth, byte[] data, byte[]? transparency)
    {
        var rowBytes = GetScanlineByteCount(width, bitDepth);
        var transparentValue = transparency is null ? -1 : ReadTransparencyValue(transparency, 2);
        var pixels = new Rgba8UNorm[checked(width * height)];

        for (var y = 0; y < height; y++)
        {
            var row = data.AsSpan(y * rowBytes, rowBytes);
            for (var x = 0; x < width; x++)
            {
                var sample = ReadPackedSample(row, x, bitDepth);
                var value = ScaleSampleToByte(sample, bitDepth);
                var alpha = sample == transparentValue ? byte.MinValue : byte.MaxValue;
                pixels[(y * width) + x] = new Rgba8UNorm(value, value, value, alpha);
            }
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeGrayscale8(int width, int height, byte[] data, byte[]? transparency)
    {
        var transparentValue = transparency is null ? -1 : ReadTransparencyValue(transparency, 2);
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var value = data[i];
            var alpha = value == transparentValue ? byte.MinValue : byte.MaxValue;
            pixels[i] = new Rgba8UNorm(value, value, value, alpha);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeGrayscale16(int width, int height, byte[] data, byte[]? transparency)
    {
        var transparentValue = transparency is null ? -1 : ReadTransparencyValue(transparency, 2);
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var value16 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i * 2, 2));
            var value = RgbaColorConversions.ToUNorm8(value16);
            var alpha = value16 == transparentValue ? byte.MinValue : byte.MaxValue;
            pixels[i] = new Rgba8UNorm(value, value, value, alpha);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeGrayscaleAlpha8(int width, int height, byte[] data)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 2;
            var value = data[offset];
            pixels[i] = new Rgba8UNorm(value, value, value, data[offset + 1]);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeGrayscaleAlpha16(int width, int height, byte[] data)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 4;
            var value = RgbaColorConversions.ToUNorm8(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2)));
            var alpha = RgbaColorConversions.ToUNorm8(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2)));
            pixels[i] = new Rgba8UNorm(value, value, value, alpha);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeRgb8(int width, int height, byte[] data, byte[]? transparency)
    {
        var hasTransparency = transparency is not null;
        var transparentRed = 0;
        var transparentGreen = 0;
        var transparentBlue = 0;
        if (transparency is not null)
        {
            ValidateTruecolorTransparency(transparency);
            transparentRed = BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(0, 2));
            transparentGreen = BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2, 2));
            transparentBlue = BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4, 2));
        }

        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 3;
            var red = data[offset];
            var green = data[offset + 1];
            var blue = data[offset + 2];
            var alpha = hasTransparency && red == transparentRed && green == transparentGreen && blue == transparentBlue
                ? byte.MinValue
                : byte.MaxValue;
            pixels[i] = new Rgba8UNorm(red, green, blue, alpha);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeRgb16(int width, int height, byte[] data, byte[]? transparency)
    {
        var hasTransparency = transparency is not null;
        var transparentRed = 0;
        var transparentGreen = 0;
        var transparentBlue = 0;
        if (transparency is not null)
        {
            ValidateTruecolorTransparency(transparency);
            transparentRed = BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(0, 2));
            transparentGreen = BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2, 2));
            transparentBlue = BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4, 2));
        }

        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 6;
            var red16 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
            var green16 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2));
            var blue16 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4, 2));
            var alpha = hasTransparency && red16 == transparentRed && green16 == transparentGreen && blue16 == transparentBlue
                ? byte.MinValue
                : byte.MaxValue;
            pixels[i] = new Rgba8UNorm(
                RgbaColorConversions.ToUNorm8(red16),
                RgbaColorConversions.ToUNorm8(green16),
                RgbaColorConversions.ToUNorm8(blue16),
                alpha);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeIndexed(
        int width,
        int height,
        byte bitDepth,
        byte[] data,
        byte[]? palette,
        byte[]? transparency)
    {
        if (palette is null)
        {
            throw new InvalidDataException("Indexed PNG is missing a PLTE chunk.");
        }

        var rowBytes = GetScanlineByteCount(width, bitDepth);
        var pixels = new Rgba8UNorm[checked(width * height)];

        for (var y = 0; y < height; y++)
        {
            var row = data.AsSpan(y * rowBytes, rowBytes);
            for (var x = 0; x < width; x++)
            {
                var index = bitDepth == 8 ? row[x] : ReadPackedSample(row, x, bitDepth);
                var paletteOffset = index * 3;
                if (paletteOffset + 2 >= palette.Length)
                {
                    throw new InvalidDataException("Indexed PNG references a palette entry that does not exist.");
                }

                var alpha = transparency is not null && index < transparency.Length
                    ? transparency[index]
                    : byte.MaxValue;
                pixels[(y * width) + x] = new Rgba8UNorm(
                    palette[paletteOffset],
                    palette[paletteOffset + 1],
                    palette[paletteOffset + 2],
                    alpha);
            }
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeRgba8(int width, int height, byte[] data)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 4;
            pixels[i] = new Rgba8UNorm(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeRgba16(int width, int height, byte[] data)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 8;
            pixels[i] = new Rgba8UNorm(
                RgbaColorConversions.ToUNorm8(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2))),
                RgbaColorConversions.ToUNorm8(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2))),
                RgbaColorConversions.ToUNorm8(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4, 2))),
                RgbaColorConversions.ToUNorm8(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 6, 2))));
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static void WriteScanlines<TPixel>(
        BitmapView<TPixel> bitmap,
        PngEncodingLayout layout,
        Stream destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowBytes = checked(bitmap.Width * layout.BytesPerPixel);
        var rowBuffer = ArrayPool<byte>.Shared.Rent(rowBytes);
        try
        {
            Byte1Buffer filterBuffer = default;
            Span<byte> filter = filterBuffer;
            var row = rowBuffer.AsSpan(0, rowBytes);
            for (var y = 0; y < bitmap.Height; y++)
            {
                filter[0] = 0;
                destination.Write(filter);
                WritePixelRow(bitmap.Pixels, y * bitmap.Width, bitmap.Width, layout, row);
                destination.Write(row);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rowBuffer);
        }
    }

    private static void WritePixelRow<TPixel>(
        Span<TPixel> pixels,
        int pixelOffset,
        int width,
        PngEncodingLayout layout,
        Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (layout.UseAppleCgbi)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = TPixel.ToRgba8UNorm(pixels[pixelOffset + x]);
                var offset = x * 4;
                destination[offset] = Premultiply(pixel.Blue, pixel.Alpha);
                destination[offset + 1] = Premultiply(pixel.Green, pixel.Alpha);
                destination[offset + 2] = Premultiply(pixel.Red, pixel.Alpha);
                destination[offset + 3] = pixel.Alpha;
            }

            return;
        }

        if (layout.BitDepth == 16)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = TPixel.ToRgba16UNorm(pixels[pixelOffset + x]);
                var offset = x * 8;
                BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(offset, 2), pixel.Red);
                BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(offset + 2, 2), pixel.Green);
                BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(offset + 4, 2), pixel.Blue);
                BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(offset + 6, 2), pixel.Alpha);
            }

            return;
        }

        for (var x = 0; x < width; x++)
        {
            var pixel = TPixel.ToRgba8UNorm(pixels[pixelOffset + x]);
            var offset = x * 4;
            destination[offset] = pixel.Red;
            destination[offset + 1] = pixel.Green;
            destination[offset + 2] = pixel.Blue;
            destination[offset + 3] = pixel.Alpha;
        }
    }

    private static PngEncodingLayout GetEncodingLayout<TPixel>(PngEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (options?.UseAppleCgbi == true)
        {
            return new PngEncodingLayout(PngColorType.TruecolorAlpha, 8, 4, UseAppleCgbi: true);
        }

        return typeof(TPixel) == typeof(Rgba16UNorm)
            ? new PngEncodingLayout(PngColorType.TruecolorAlpha, 16, 8, UseAppleCgbi: false)
            : new PngEncodingLayout(PngColorType.TruecolorAlpha, 8, 4, UseAppleCgbi: false);
    }

    private static Stream CreateCompressedStream(Stream input, bool rawDeflate) => rawDeflate
        ? new DeflateStream(input, CompressionMode.Decompress)
        : new ZLibStream(input, CompressionMode.Decompress);

    private static Stream CreateCompressedStream(Stream output, bool rawDeflate, CompressionLevel compressionLevel) => rawDeflate
        ? new DeflateStream(output, compressionLevel, leaveOpen: true)
        : new ZLibStream(output, compressionLevel, leaveOpen: true);

    private static void ReadExactlyOrThrow(Stream stream, Span<byte> destination, string message)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead != destination.Length)
        {
            throw new InvalidDataException(message);
        }
    }

    private static void ConvertCgbiBgraToRgba(byte[] pixels)
    {
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var blue = pixels[offset];
            var green = pixels[offset + 1];
            var red = pixels[offset + 2];
            var alpha = pixels[offset + 3];

            pixels[offset] = Unpremultiply(red, alpha);
            pixels[offset + 1] = Unpremultiply(green, alpha);
            pixels[offset + 2] = Unpremultiply(blue, alpha);
            pixels[offset + 3] = alpha;
        }
    }

    private static byte Unpremultiply(byte value, byte alpha)
    {
        if (alpha == 0)
        {
            return 0;
        }

        if (alpha == byte.MaxValue)
        {
            return value;
        }

        return (byte)Math.Min(byte.MaxValue, ((value * byte.MaxValue) + (alpha / 2)) / alpha);
    }

    private static byte Premultiply(byte value, byte alpha) =>
        (byte)(((value * alpha) + (byte.MaxValue / 2)) / byte.MaxValue);

    private static byte[] DecodeNonInterlaced(
        Stream compressed,
        int width,
        int height,
        int bitsPerPixel,
        int filterBytesPerPixel)
    {
        var rowBytes = GetScanlineByteCount(width, bitsPerPixel);
        var result = new byte[checked(rowBytes * height)];
        var filteredRowBuffer = ArrayPool<byte>.Shared.Rent(rowBytes + 1);
        try
        {
            for (var y = 0; y < height; y++)
            {
                var filteredRow = filteredRowBuffer.AsSpan(0, rowBytes + 1);
                ReadExactlyOrThrow(compressed, filteredRow, "PNG IDAT data ended before all scanlines were decoded.");

                var destination = result.AsSpan(y * rowBytes, rowBytes);
                var previous = y == 0
                    ? ReadOnlySpan<byte>.Empty
                    : result.AsSpan((y - 1) * rowBytes, rowBytes);
                UnfilterRow(filteredRow[0], filteredRow[1..], destination, previous, y > 0, filterBytesPerPixel);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(filteredRowBuffer);
        }

        return result;
    }

    private static byte[] DecodeAdam7(
        Stream compressed,
        int width,
        int height,
        int bitsPerPixel,
        int filterBytesPerPixel)
    {
        var resultRowBytes = GetScanlineByteCount(width, bitsPerPixel);
        var result = new byte[checked(resultRowBytes * height)];

        foreach (var pass in Adam7Passes)
        {
            var passWidth = GetAdam7PassSize(width, pass.StartX, pass.StepX);
            var passHeight = GetAdam7PassSize(height, pass.StartY, pass.StepY);
            if (passWidth == 0 || passHeight == 0)
            {
                continue;
            }

            var passRowBytes = GetScanlineByteCount(passWidth, bitsPerPixel);
            DecodeAdam7Pass(compressed, result, width, bitsPerPixel, filterBytesPerPixel, pass, passWidth, passHeight, passRowBytes);
        }

        return result;
    }

    private static void DecodeAdam7Pass(
        Stream compressed,
        byte[] result,
        int width,
        int bitsPerPixel,
        int filterBytesPerPixel,
        Adam7Pass pass,
        int passWidth,
        int passHeight,
        int passRowBytes)
    {
        var filteredRowBuffer = ArrayPool<byte>.Shared.Rent(passRowBytes + 1);
        var currentRowBuffer = ArrayPool<byte>.Shared.Rent(passRowBytes);
        var previousRowBuffer = ArrayPool<byte>.Shared.Rent(passRowBytes);

        try
        {
            for (var passY = 0; passY < passHeight; passY++)
            {
                var filteredRow = filteredRowBuffer.AsSpan(0, passRowBytes + 1);
                var currentRow = currentRowBuffer.AsSpan(0, passRowBytes);
                var previousRow = previousRowBuffer.AsSpan(0, passRowBytes);
                ReadExactlyOrThrow(compressed, filteredRow, "PNG Adam7 data ended before all passes were decoded.");
                UnfilterRow(filteredRow[0], filteredRow[1..], currentRow, previousRow, passY > 0, filterBytesPerPixel);
                CopyAdam7PassRow(currentRow, result, width, bitsPerPixel, pass, passWidth, passY);
                currentRow.CopyTo(previousRow);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(filteredRowBuffer);
            ArrayPool<byte>.Shared.Return(currentRowBuffer);
            ArrayPool<byte>.Shared.Return(previousRowBuffer);
        }
    }

    private static void CopyAdam7PassRow(
        ReadOnlySpan<byte> passRow,
        byte[] result,
        int width,
        int bitsPerPixel,
        Adam7Pass pass,
        int passWidth,
        int passY)
    {
        var resultRowBytes = GetScanlineByteCount(width, bitsPerPixel);
        var targetY = pass.StartY + (passY * pass.StepY);
        var targetRow = result.AsSpan(targetY * resultRowBytes, resultRowBytes);

        if (bitsPerPixel >= 8)
        {
            var bytesPerPixel = bitsPerPixel / 8;
            for (var passX = 0; passX < passWidth; passX++)
            {
                var targetX = pass.StartX + (passX * pass.StepX);
                passRow.Slice(passX * bytesPerPixel, bytesPerPixel)
                    .CopyTo(targetRow.Slice(targetX * bytesPerPixel, bytesPerPixel));
            }

            return;
        }

        for (var passX = 0; passX < passWidth; passX++)
        {
            var targetX = pass.StartX + (passX * pass.StepX);
            WritePackedSample(targetRow, targetX, bitsPerPixel, ReadPackedSample(passRow, passX, bitsPerPixel));
        }
    }

    private static void UnfilterRow(
        byte filter,
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        ReadOnlySpan<byte> previous,
        bool hasPrevious,
        int bytesPerPixel)
    {
        for (var x = 0; x < destination.Length; x++)
        {
            var raw = source[x];
            var left = x >= bytesPerPixel ? destination[x - bytesPerPixel] : (byte)0;
            var up = hasPrevious ? previous[x] : (byte)0;
            var upLeft = hasPrevious && x >= bytesPerPixel ? previous[x - bytesPerPixel] : (byte)0;

            destination[x] = filter switch
            {
                0 => raw,
                1 => unchecked((byte)(raw + left)),
                2 => unchecked((byte)(raw + up)),
                3 => unchecked((byte)(raw + ((left + up) >> 1))),
                4 => unchecked((byte)(raw + Paeth(left, up, upLeft))),
                _ => throw new InvalidDataException($"Unsupported PNG scanline filter {filter}.")
            };
        }
    }

    private static byte Paeth(byte left, byte up, byte upLeft)
    {
        var p = left + up - upLeft;
        var pa = Math.Abs(p - left);
        var pb = Math.Abs(p - up);
        var pc = Math.Abs(p - upLeft);

        if (pa <= pb && pa <= pc)
        {
            return left;
        }

        return pb <= pc ? up : upLeft;
    }

    private static int GetBitsPerPixel(PngColorType colorType, byte bitDepth)
    {
        var channels = colorType switch
        {
            PngColorType.Grayscale => 1,
            PngColorType.Truecolor => 3,
            PngColorType.GrayscaleAlpha => 2,
            PngColorType.TruecolorAlpha => 4,
            PngColorType.IndexedColor => 1,
            _ => throw new InvalidDataException($"Invalid PNG color type {colorType}.")
        };

        return bitDepth * channels;
    }

    private static int GetFilterBytesPerPixel(int bitsPerPixel) => Math.Max(1, (bitsPerPixel + 7) / 8);

    private static int GetScanlineByteCount(int width, int bitsPerPixel) => checked(((width * bitsPerPixel) + 7) / 8);

    private static int ReadPackedSample(ReadOnlySpan<byte> row, int x, int bitDepth)
    {
        var bitOffset = x * bitDepth;
        var byteOffset = bitOffset >> 3;
        var shift = 8 - bitDepth - (bitOffset & 7);
        var mask = (1 << bitDepth) - 1;
        return (row[byteOffset] >> shift) & mask;
    }

    private static void WritePackedSample(Span<byte> row, int x, int bitDepth, int value)
    {
        var bitOffset = x * bitDepth;
        var byteOffset = bitOffset >> 3;
        var shift = 8 - bitDepth - (bitOffset & 7);
        var sampleMask = (1 << bitDepth) - 1;
        var shiftedMask = sampleMask << shift;
        row[byteOffset] = (byte)((row[byteOffset] & ~shiftedMask) | ((value & sampleMask) << shift));
    }

    private static int GetAdam7PassSize(int size, int start, int step)
    {
        if (size <= start)
        {
            return 0;
        }

        return ((size - start) + step - 1) / step;
    }

    private static byte ScaleSampleToByte(int value, int bitDepth)
    {
        if (bitDepth == 8)
        {
            return (byte)value;
        }

        var max = (1 << bitDepth) - 1;
        return (byte)((value * 255 + (max / 2)) / max);
    }

    private static int ReadTransparencyValue(byte[] transparency, int requiredBytes)
    {
        if (transparency.Length < requiredBytes)
        {
            throw new InvalidDataException("PNG tRNS chunk is too short for the image color type.");
        }

        return BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(0, 2));
    }

    private static void ValidateHeader(PngHeader header)
    {
        if (header.Width <= 0 || header.Height <= 0)
        {
            throw new InvalidDataException("PNG width and height must be positive.");
        }

        if (header.CompressionMethod != 0)
        {
            throw new NotSupportedException($"PNG compression method {header.CompressionMethod} is not supported.");
        }

        if (header.FilterMethod != 0)
        {
            throw new NotSupportedException($"PNG filter method {header.FilterMethod} is not supported.");
        }

        if (header.InterlaceMethod is not 0 and not 1)
        {
            throw new NotSupportedException($"PNG interlace method {header.InterlaceMethod} is not supported.");
        }

        if (!IsSupportedBitDepth(header.ColorType, header.BitDepth))
        {
            throw new NotSupportedException($"PNG color type {header.ColorType} does not support bit depth {header.BitDepth} here.");
        }

        _ = GetBitsPerPixel(header.ColorType, header.BitDepth);
    }

    private static void ValidateCgbi(PngHeader header, byte[]? transparency, bool isAppleCgbi)
    {
        if (!isAppleCgbi)
        {
            return;
        }

        if (header.ColorType != PngColorType.TruecolorAlpha || header.BitDepth != 8)
        {
            throw new NotSupportedException("CgBI PNG decoding currently supports only 8-bit truecolor-alpha BGRA8888 images.");
        }

        if (transparency is not null)
        {
            throw new InvalidDataException("CgBI PNG with a tRNS chunk is not supported.");
        }
    }

    private static bool IsSupportedBitDepth(PngColorType colorType, byte bitDepth) => colorType switch
    {
        PngColorType.Grayscale => bitDepth is 1 or 2 or 4 or 8 or 16,
        PngColorType.Truecolor => bitDepth is 8 or 16,
        PngColorType.IndexedColor => bitDepth is 1 or 2 or 4 or 8,
        PngColorType.GrayscaleAlpha => bitDepth is 8 or 16,
        PngColorType.TruecolorAlpha => bitDepth is 8 or 16,
        _ => false
    };

    private static void ValidatePalette(byte[] palette)
    {
        if (palette.Length == 0 || palette.Length % 3 != 0 || palette.Length > 256 * 3)
        {
            throw new InvalidDataException("PNG PLTE chunk must contain between 1 and 256 RGB palette entries.");
        }
    }

    private static void ValidateTruecolorTransparency(byte[] transparency)
    {
        if (transparency.Length < 6)
        {
            throw new InvalidDataException("PNG truecolor tRNS chunk must contain 6 bytes.");
        }
    }

    private static void ValidateEncodingOptions(PngEncodingOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.MaxIdatChunkDataLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxIdatChunkDataLength,
                "PNG IDAT chunk data length must be positive.");
        }
    }

    private static PngHeader ReadHeader(byte[] data)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException("PNG IHDR chunk must be 13 bytes.");
        }

        return new PngHeader(
            checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4))),
            checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4))),
            data[8],
            (PngColorType)data[9],
            data[10],
            data[11],
            data[12]);
    }

    private static PngChunk ReadChunk(Stream stream)
    {
        Byte4Buffer lengthBuffer = default;
        Span<byte> lengthBytes = lengthBuffer;
        ReadExactly(stream, lengthBytes);
        var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        if (length > int.MaxValue)
        {
            throw new InvalidDataException("PNG chunk is too large.");
        }

        Byte4Buffer typeBuffer = default;
        Span<byte> typeBytes = typeBuffer;
        ReadExactly(stream, typeBytes);
        var data = new byte[length];
        ReadExactly(stream, data);

        Byte4Buffer crcBuffer = default;
        Span<byte> crcBytes = crcBuffer;
        ReadExactly(stream, crcBytes);
        var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
        var actualCrc = PngCrc32.Compute(typeBytes, data);
        if (expectedCrc != actualCrc)
        {
            var typeName = ToChunkType(typeBytes);
            throw new InvalidDataException($"PNG chunk '{typeName}' has an invalid CRC.");
        }

        return new PngChunk(ToChunkType(typeBytes), data);
    }

    private static void WriteHeader(Stream stream, int width, int height, PngEncodingLayout layout)
    {
        Byte13Buffer headerBuffer = default;
        Span<byte> header = headerBuffer;
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(0, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(4, 4), checked((uint)height));
        header[8] = layout.BitDepth;
        header[9] = (byte)layout.ColorType;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(stream, "IHDR", header);
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Byte4Buffer lengthBuffer = default;
        Span<byte> lengthBytes = lengthBuffer;
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, checked((uint)data.Length));
        stream.Write(lengthBytes);

        Byte4Buffer typeBuffer = default;
        Span<byte> typeBytes = typeBuffer;
        typeBytes[0] = (byte)type[0];
        typeBytes[1] = (byte)type[1];
        typeBytes[2] = (byte)type[2];
        typeBytes[3] = (byte)type[3];

        stream.Write(typeBytes);
        stream.Write(data);

        Byte4Buffer crcBuffer = default;
        Span<byte> crcBytes = crcBuffer;
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, PngCrc32.Compute(typeBytes, data));
        stream.Write(crcBytes);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of PNG stream.");
            }

            totalRead += read;
        }
    }

    private static bool IsCriticalChunk(string type) => (type[0] & 0x20) == 0;

    private static string ToChunkType(ReadOnlySpan<byte> typeBytes) =>
        string.Create(4, typeBytes.ToArray(), static (chars, bytes) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)bytes[i];
            }
        });

    private readonly record struct PngChunk(string Type, byte[] Data);

    private readonly record struct PngHeader(
        int Width,
        int Height,
        byte BitDepth,
        PngColorType ColorType,
        byte CompressionMethod,
        byte FilterMethod,
        byte InterlaceMethod);

    private readonly record struct PngEncodingLayout(PngColorType ColorType, byte BitDepth, int BytesPerPixel, bool UseAppleCgbi);

    private readonly record struct Adam7Pass(int StartX, int StartY, int StepX, int StepY);

    [InlineArray(1)]
    private struct Byte1Buffer
    {
        private byte _element0;
    }

    [InlineArray(4)]
    private struct Byte4Buffer
    {
        private byte _element0;
    }

    [InlineArray(8)]
    private struct Byte8Buffer
    {
        private byte _element0;
    }

    [InlineArray(13)]
    private struct Byte13Buffer
    {
        private byte _element0;
    }

    private sealed class IdatChunkStream : Stream
    {
        private readonly Stream _destination;
        private readonly byte[] _buffer;
        private int _bufferedLength;
        private bool _finished;

        public IdatChunkStream(Stream destination, int maxChunkDataLength)
        {
            _destination = destination;
            _buffer = new byte[maxChunkDataLength];
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            WriteBufferedChunk();
            _destination.Flush();
        }

        public void Finish()
        {
            if (_finished)
            {
                return;
            }

            WriteBufferedChunk();
            _finished = true;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_finished)
            {
                throw new InvalidOperationException("Cannot write to a finished IDAT chunk stream.");
            }

            while (!buffer.IsEmpty)
            {
                var available = _buffer.Length - _bufferedLength;
                var copyLength = Math.Min(available, buffer.Length);
                buffer[..copyLength].CopyTo(_buffer.AsSpan(_bufferedLength));
                _bufferedLength += copyLength;
                buffer = buffer[copyLength..];

                if (_bufferedLength == _buffer.Length)
                {
                    WriteBufferedChunk();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Finish();
            }

            base.Dispose(disposing);
        }

        private void WriteBufferedChunk()
        {
            if (_bufferedLength == 0)
            {
                return;
            }

            WriteChunk(_destination, "IDAT", _buffer.AsSpan(0, _bufferedLength));
            _bufferedLength = 0;
        }
    }
}
