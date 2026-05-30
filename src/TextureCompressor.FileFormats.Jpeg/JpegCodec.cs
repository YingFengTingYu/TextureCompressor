using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Jpeg;

public static class JpegCodec
{
    private const int BlockSize = 8;
    private const int BlockArea = 64;

    private static readonly int[] ZigZag =
    [
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    ];

    private static readonly byte[] LuminanceQuant =
    [
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77,
        24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103, 99
    ];

    private static readonly byte[] ChrominanceQuant =
    [
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
    ];

    private static readonly byte[] DcLuminanceBits = [0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] DcLuminanceValues = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    private static readonly byte[] DcChrominanceBits = [0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0];
    private static readonly byte[] DcChrominanceValues = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    private static readonly byte[] AcLuminanceBits = [0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 125];
    private static readonly byte[] AcLuminanceValues =
    [
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
        0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
        0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16,
        0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
        0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
        0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
        0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
        0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
        0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
        0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
        0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
        0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
        0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
        0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
        0xf9, 0xfa
    ];

    private static readonly byte[] AcChrominanceBits = [0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 119];
    private static readonly byte[] AcChrominanceValues =
    [
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
        0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
        0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
        0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34,
        0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
        0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38,
        0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
        0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
        0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
        0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
        0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
        0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
        0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2,
        0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
        0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9,
        0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
        0xf9, 0xfa
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

    public static ArrayBitmap<TPixel> Decode<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = DecodeRgba8(stream);
        var pixels = new TPixel[rgba.PixelSpan.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = TPixel.FromRgba8UNorm(rgba.PixelSpan[i]);
        }

        return new ArrayBitmap<TPixel>(rgba.Width, rgba.Height, pixels);
    }

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
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return DecodeBytes(memory.ToArray());
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, JpegEncodingOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, JpegEncodingOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        Encode(bitmap, stream, options);
        return stream.ToArray();
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, JpegEncodingOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, JpegEncodingOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);
        var quality = options?.Quality ?? 90;
        if (quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options), quality, "JPEG quality must be between 1 and 100.");
        }

        var qy = ScaleQuantTable(LuminanceQuant, quality);
        var qc = ScaleQuantTable(ChrominanceQuant, quality);
        var huffman = CreateStandardHuffmanTables();

        WriteMarker(stream, 0xd8);
        WriteApp0(stream);
        WriteDqt(stream, 0, qy);
        WriteDqt(stream, 1, qc);
        WriteSof0(stream, bitmap.Width, bitmap.Height);
        WriteDht(stream, 0, 0, DcLuminanceBits, DcLuminanceValues);
        WriteDht(stream, 1, 0, AcLuminanceBits, AcLuminanceValues);
        WriteDht(stream, 0, 1, DcChrominanceBits, DcChrominanceValues);
        WriteDht(stream, 1, 1, AcChrominanceBits, AcChrominanceValues);
        WriteSos(stream);

        var writer = new BitWriter(stream);
        Span<double> samples = stackalloc double[BlockArea];
        Span<int> coeffs = stackalloc int[BlockArea];
        Span<int> previousDc = stackalloc int[3];

        var blockWidth = (bitmap.Width + 7) / 8;
        var blockHeight = (bitmap.Height + 7) / 8;
        for (var by = 0; by < blockHeight; by++)
        {
            for (var bx = 0; bx < blockWidth; bx++)
            {
                for (var component = 0; component < 3; component++)
                {
                    LoadComponentBlock(bitmap, bx * 8, by * 8, component, samples);
                    ForwardDct(samples, coeffs);
                    Quantize(coeffs, component == 0 ? qy : qc);
                    EncodeBlock(coeffs, huffman[component == 0 ? 0 : 2], huffman[component == 0 ? 1 : 3], ref previousDc[component], writer);
                }
            }
        }

        writer.Flush();
        WriteMarker(stream, 0xd9);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, JpegEncodingOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, JpegEncodingOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeBytes(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xff || data[1] != 0xd8)
        {
            throw new InvalidDataException("The stream is not a JPEG file.");
        }

        var decoder = new DecoderState();
        var offset = 2;
        while (offset < data.Length)
        {
            var marker = ReadMarker(data, ref offset);
            if (marker == 0xd9)
            {
                break;
            }

            if (marker is >= 0xd0 and <= 0xd7 or 0x01)
            {
                continue;
            }

            var length = ReadUInt16(data, ref offset);
            if (length < 2 || offset + length - 2 > data.Length)
            {
                throw new InvalidDataException("JPEG segment length is invalid.");
            }

            var segment = data.AsSpan(offset, length - 2);
            offset += length - 2;

            switch (marker)
            {
                case 0xc0:
                    ReadSof0(segment, decoder);
                    break;
                case 0xc2:
                    throw new NotSupportedException("Progressive JPEG is not supported.");
                case 0xc4:
                    ReadDht(segment, decoder);
                    break;
                case 0xdb:
                    ReadDqt(segment, decoder);
                    break;
                case 0xda:
                    ReadSos(segment, decoder);
                    var scanData = ReadScanData(data, ref offset);
                    return DecodeScan(decoder, scanData);
            }
        }

        throw new InvalidDataException("JPEG is missing scan data.");
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeScan(DecoderState decoder, byte[] scanData)
    {
        if (decoder.Width <= 0 || decoder.Height <= 0 || decoder.ScanComponents.Count == 0)
        {
            throw new InvalidDataException("JPEG frame or scan header is missing.");
        }

        var maxH = decoder.Components.Max(c => c.HorizontalSampling);
        var maxV = decoder.Components.Max(c => c.VerticalSampling);
        var mcuWidth = maxH * 8;
        var mcuHeight = maxV * 8;
        var mcuCountX = (decoder.Width + mcuWidth - 1) / mcuWidth;
        var mcuCountY = (decoder.Height + mcuHeight - 1) / mcuHeight;

        foreach (var component in decoder.Components)
        {
            component.BlocksWide = mcuCountX * component.HorizontalSampling;
            component.BlocksHigh = mcuCountY * component.VerticalSampling;
            component.Samples = new byte[component.BlocksWide * component.BlocksHigh * BlockArea];
        }

        var reader = new BitReader(scanData);
        Span<int> coeffs = stackalloc int[BlockArea];
        Span<byte> block = stackalloc byte[BlockArea];

        for (var my = 0; my < mcuCountY; my++)
        {
            for (var mx = 0; mx < mcuCountX; mx++)
            {
                foreach (var scan in decoder.ScanComponents)
                {
                    var component = decoder.FindComponent(scan.Id);
                    for (var vy = 0; vy < component.VerticalSampling; vy++)
                    {
                        for (var hx = 0; hx < component.HorizontalSampling; hx++)
                        {
                            coeffs.Clear();
                            DecodeBlock(reader, decoder.DcTables[scan.DcTable], decoder.AcTables[scan.AcTable], coeffs, ref component.PreviousDc);
                            Dequantize(coeffs, decoder.QuantTables[component.QuantTable]);
                            InverseDct(coeffs, block);
                            CopyBlock(block, component, (mx * component.HorizontalSampling) + hx, (my * component.VerticalSampling) + vy);
                        }
                    }
                }
            }
        }

        var pixels = new Rgba8UNorm[decoder.Width * decoder.Height];
        for (var y = 0; y < decoder.Height; y++)
        {
            for (var x = 0; x < decoder.Width; x++)
            {
                if (decoder.Components.Count == 1)
                {
                    var gray = SampleComponent(decoder.Components[0], x, y, maxH, maxV);
                    pixels[(y * decoder.Width) + x] = new Rgba8UNorm(gray, gray, gray, 255);
                    continue;
                }

                var yy = SampleComponent(decoder.Components[0], x, y, maxH, maxV);
                var cb = SampleComponent(decoder.Components[1], x, y, maxH, maxV) - 128;
                var cr = SampleComponent(decoder.Components[2], x, y, maxH, maxV) - 128;
                var red = ClampToByte(yy + (1.402 * cr));
                var green = ClampToByte(yy - (0.344136 * cb) - (0.714136 * cr));
                var blue = ClampToByte(yy + (1.772 * cb));
                pixels[(y * decoder.Width) + x] = new Rgba8UNorm(red, green, blue, 255);
            }
        }

        return new ArrayBitmap<Rgba8UNorm>(decoder.Width, decoder.Height, pixels);
    }

    private static void DecodeBlock(BitReader reader, HuffmanDecodeTable dcTable, HuffmanDecodeTable acTable, Span<int> coeffs, ref int previousDc)
    {
        var dcSize = DecodeHuffman(reader, dcTable);
        previousDc += ReceiveAndExtend(reader, dcSize);
        coeffs[0] = previousDc;

        var k = 1;
        while (k < BlockArea)
        {
            var value = DecodeHuffman(reader, acTable);
            if (value == 0)
            {
                break;
            }

            if (value == 0xf0)
            {
                k += 16;
                continue;
            }

            k += value >> 4;
            if (k >= BlockArea)
            {
                throw new InvalidDataException("JPEG AC coefficient run exceeds the block.");
            }

            coeffs[ZigZag[k]] = ReceiveAndExtend(reader, value & 0x0f);
            k++;
        }
    }

    private static int DecodeHuffman(BitReader reader, HuffmanDecodeTable table)
    {
        var code = 0;
        for (var length = 1; length <= 16; length++)
        {
            code = (code << 1) | reader.ReadBit();
            if (table.TryDecode(length, code, out var value))
            {
                return value;
            }
        }

        throw new InvalidDataException("JPEG contains an invalid Huffman code.");
    }

    private static void EncodeBlock(Span<int> coeffs, HuffmanEncodeTable dcTable, HuffmanEncodeTable acTable, ref int previousDc, BitWriter writer)
    {
        var diff = coeffs[0] - previousDc;
        previousDc = coeffs[0];
        var dcCategory = GetCategory(diff);
        WriteHuffman(writer, dcTable, dcCategory);
        WriteMagnitude(writer, diff, dcCategory);

        var zeroRun = 0;
        for (var k = 1; k < BlockArea; k++)
        {
            var value = coeffs[ZigZag[k]];
            if (value == 0)
            {
                zeroRun++;
                continue;
            }

            while (zeroRun >= 16)
            {
                WriteHuffman(writer, acTable, 0xf0);
                zeroRun -= 16;
            }

            var category = GetCategory(value);
            WriteHuffman(writer, acTable, (zeroRun << 4) | category);
            WriteMagnitude(writer, value, category);
            zeroRun = 0;
        }

        if (zeroRun > 0)
        {
            WriteHuffman(writer, acTable, 0);
        }
    }

    private static void LoadComponentBlock<TPixel>(BitmapView<TPixel> bitmap, int startX, int startY, int component, Span<double> samples)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var y = 0; y < 8; y++)
        {
            var sourceY = Math.Min(bitmap.Height - 1, startY + y);
            for (var x = 0; x < 8; x++)
            {
                var sourceX = Math.Min(bitmap.Width - 1, startX + x);
                var rgba = TPixel.ToRgba8UNorm(bitmap[sourceX, sourceY]);
                var value = component switch
                {
                    0 => (0.299 * rgba.Red) + (0.587 * rgba.Green) + (0.114 * rgba.Blue),
                    1 => 128 - (0.168736 * rgba.Red) - (0.331264 * rgba.Green) + (0.5 * rgba.Blue),
                    _ => 128 + (0.5 * rgba.Red) - (0.418688 * rgba.Green) - (0.081312 * rgba.Blue)
                };
                samples[(y * 8) + x] = value - 128;
            }
        }
    }

    private static void ForwardDct(ReadOnlySpan<double> samples, Span<int> coeffs)
    {
        for (var v = 0; v < 8; v++)
        {
            for (var u = 0; u < 8; u++)
            {
                var sum = 0.0;
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        sum += samples[(y * 8) + x]
                            * Math.Cos(((2 * x + 1) * u * Math.PI) / 16)
                            * Math.Cos(((2 * y + 1) * v * Math.PI) / 16);
                    }
                }

                var cu = u == 0 ? 1 / Math.Sqrt(2) : 1;
                var cv = v == 0 ? 1 / Math.Sqrt(2) : 1;
                coeffs[(v * 8) + u] = (int)Math.Round(0.25 * cu * cv * sum);
            }
        }
    }

    private static void InverseDct(ReadOnlySpan<int> coeffs, Span<byte> block)
    {
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var sum = 0.0;
                for (var v = 0; v < 8; v++)
                {
                    for (var u = 0; u < 8; u++)
                    {
                        var cu = u == 0 ? 1 / Math.Sqrt(2) : 1;
                        var cv = v == 0 ? 1 / Math.Sqrt(2) : 1;
                        sum += cu * cv * coeffs[(v * 8) + u]
                            * Math.Cos(((2 * x + 1) * u * Math.PI) / 16)
                            * Math.Cos(((2 * y + 1) * v * Math.PI) / 16);
                    }
                }

                block[(y * 8) + x] = ClampToByte((0.25 * sum) + 128);
            }
        }
    }

    private static void Quantize(Span<int> coeffs, ReadOnlySpan<byte> table)
    {
        for (var i = 0; i < BlockArea; i++)
        {
            coeffs[i] = (int)Math.Round((double)coeffs[i] / table[i]);
        }
    }

    private static void Dequantize(Span<int> coeffs, ReadOnlySpan<int> table)
    {
        for (var i = 0; i < BlockArea; i++)
        {
            coeffs[i] *= table[i];
        }
    }

    private static void CopyBlock(ReadOnlySpan<byte> block, JpegComponent component, int blockX, int blockY)
    {
        var stride = component.BlocksWide * 8;
        for (var y = 0; y < 8; y++)
        {
            block.Slice(y * 8, 8).CopyTo(component.Samples.AsSpan(((blockY * 8 + y) * stride) + (blockX * 8), 8));
        }
    }

    private static byte SampleComponent(JpegComponent component, int x, int y, int maxH, int maxV)
    {
        var sx = Math.Min(component.BlocksWide * 8 - 1, (x * component.HorizontalSampling) / maxH);
        var sy = Math.Min(component.BlocksHigh * 8 - 1, (y * component.VerticalSampling) / maxV);
        return component.Samples[(sy * component.BlocksWide * 8) + sx];
    }

    private static byte[] ScaleQuantTable(byte[] source, int quality)
    {
        var scale = quality < 50 ? 5000 / quality : 200 - (quality * 2);
        var result = new byte[BlockArea];
        for (var i = 0; i < BlockArea; i++)
        {
            result[i] = (byte)Math.Clamp(((source[i] * scale) + 50) / 100, 1, 255);
        }

        return result;
    }

    private static void WriteApp0(Stream stream)
    {
        WriteMarker(stream, 0xe0);
        WriteUInt16(stream, 16);
        stream.Write("JFIF\0"u8);
        stream.WriteByte(1);
        stream.WriteByte(1);
        stream.WriteByte(0);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 1);
        stream.WriteByte(0);
        stream.WriteByte(0);
    }

    private static void WriteDqt(Stream stream, int id, ReadOnlySpan<byte> table)
    {
        WriteMarker(stream, 0xdb);
        WriteUInt16(stream, 67);
        stream.WriteByte((byte)id);
        for (var i = 0; i < BlockArea; i++)
        {
            stream.WriteByte(table[ZigZag[i]]);
        }
    }

    private static void WriteSof0(Stream stream, int width, int height)
    {
        WriteMarker(stream, 0xc0);
        WriteUInt16(stream, 17);
        stream.WriteByte(8);
        WriteUInt16(stream, height);
        WriteUInt16(stream, width);
        stream.WriteByte(3);
        stream.WriteByte(1);
        stream.WriteByte(0x11);
        stream.WriteByte(0);
        stream.WriteByte(2);
        stream.WriteByte(0x11);
        stream.WriteByte(1);
        stream.WriteByte(3);
        stream.WriteByte(0x11);
        stream.WriteByte(1);
    }

    private static void WriteDht(Stream stream, int tableClass, int id, ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values)
    {
        WriteMarker(stream, 0xc4);
        WriteUInt16(stream, 3 + 16 + values.Length);
        stream.WriteByte((byte)((tableClass << 4) | id));
        stream.Write(bits);
        stream.Write(values);
    }

    private static void WriteSos(Stream stream)
    {
        WriteMarker(stream, 0xda);
        WriteUInt16(stream, 12);
        stream.WriteByte(3);
        stream.WriteByte(1);
        stream.WriteByte(0x00);
        stream.WriteByte(2);
        stream.WriteByte(0x11);
        stream.WriteByte(3);
        stream.WriteByte(0x11);
        stream.WriteByte(0);
        stream.WriteByte(63);
        stream.WriteByte(0);
    }

    private static void ReadSof0(ReadOnlySpan<byte> data, DecoderState decoder)
    {
        if (data.Length < 6 || data[0] != 8)
        {
            throw new NotSupportedException("Only 8-bit baseline JPEG is supported.");
        }

        decoder.Height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(1, 2));
        decoder.Width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(3, 2));
        var count = data[5];
        if (count is not 1 and not 3 || data.Length != 6 + (count * 3))
        {
            throw new NotSupportedException("Only grayscale and YCbCr JPEG images are supported.");
        }

        decoder.Components.Clear();
        for (var i = 0; i < count; i++)
        {
            var offset = 6 + (i * 3);
            var sampling = data[offset + 1];
            decoder.Components.Add(new JpegComponent
            {
                Id = data[offset],
                HorizontalSampling = sampling >> 4,
                VerticalSampling = sampling & 0x0f,
                QuantTable = data[offset + 2]
            });
        }
    }

    private static void ReadDqt(ReadOnlySpan<byte> data, DecoderState decoder)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var descriptor = data[offset++];
            var precision = descriptor >> 4;
            var id = descriptor & 0x0f;
            if (id > 3)
            {
                throw new InvalidDataException("JPEG quantization table id is invalid.");
            }

            var bytesPerValue = precision == 0 ? 1 : 2;
            if (precision > 1 || offset + (64 * bytesPerValue) > data.Length)
            {
                throw new InvalidDataException("JPEG quantization table is invalid.");
            }

            var table = new int[BlockArea];
            for (var i = 0; i < BlockArea; i++)
            {
                table[ZigZag[i]] = bytesPerValue == 1
                    ? data[offset++]
                    : BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
                if (bytesPerValue == 2)
                {
                    offset += 2;
                }
            }

            decoder.QuantTables[id] = table;
        }
    }

    private static void ReadDht(ReadOnlySpan<byte> data, DecoderState decoder)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var descriptor = data[offset++];
            var tableClass = descriptor >> 4;
            var id = descriptor & 0x0f;
            if (tableClass > 1 || id > 3 || offset + 16 > data.Length)
            {
                throw new InvalidDataException("JPEG Huffman table header is invalid.");
            }

            var bits = data.Slice(offset, 16).ToArray();
            offset += 16;
            var valueCount = bits.Sum(x => x);
            if (offset + valueCount > data.Length)
            {
                throw new InvalidDataException("JPEG Huffman table is truncated.");
            }

            var values = data.Slice(offset, valueCount).ToArray();
            offset += valueCount;
            var table = new HuffmanDecodeTable(bits, values);
            if (tableClass == 0)
            {
                decoder.DcTables[id] = table;
            }
            else
            {
                decoder.AcTables[id] = table;
            }
        }
    }

    private static void ReadSos(ReadOnlySpan<byte> data, DecoderState decoder)
    {
        if (data.Length < 4)
        {
            throw new InvalidDataException("JPEG scan header is too short.");
        }

        var count = data[0];
        if (data.Length != 1 + (count * 2) + 3)
        {
            throw new InvalidDataException("JPEG scan header length is invalid.");
        }

        decoder.ScanComponents.Clear();
        for (var i = 0; i < count; i++)
        {
            var offset = 1 + (i * 2);
            var table = data[offset + 1];
            decoder.ScanComponents.Add(new ScanComponent(data[offset], table >> 4, table & 0x0f));
        }
    }

    private static byte[] ReadScanData(byte[] data, ref int offset)
    {
        using var stream = new MemoryStream();
        while (offset < data.Length)
        {
            var value = data[offset++];
            if (value != 0xff)
            {
                stream.WriteByte(value);
                continue;
            }

            while (offset < data.Length && data[offset] == 0xff)
            {
                offset++;
            }

            if (offset >= data.Length)
            {
                break;
            }

            var marker = data[offset++];
            if (marker == 0x00)
            {
                stream.WriteByte(0xff);
                continue;
            }

            if (marker is >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            offset -= 2;
            break;
        }

        return stream.ToArray();
    }

    private static int ReadMarker(byte[] data, ref int offset)
    {
        while (offset < data.Length && data[offset] != 0xff)
        {
            offset++;
        }

        while (offset < data.Length && data[offset] == 0xff)
        {
            offset++;
        }

        if (offset >= data.Length)
        {
            throw new EndOfStreamException("Unexpected end of JPEG stream.");
        }

        return data[offset++];
    }

    private static int ReadUInt16(byte[] data, ref int offset)
    {
        if (offset + 2 > data.Length)
        {
            throw new EndOfStreamException("Unexpected end of JPEG stream.");
        }

        var value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
        offset += 2;
        return value;
    }

    private static void WriteMarker(Stream stream, int marker)
    {
        stream.WriteByte(0xff);
        stream.WriteByte((byte)marker);
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)value));
        stream.Write(bytes);
    }

    private static void WriteHuffman(BitWriter writer, HuffmanEncodeTable table, int value)
    {
        var entry = table[value];
        writer.WriteBits(entry.Code, entry.Length);
    }

    private static void WriteMagnitude(BitWriter writer, int value, int category)
    {
        if (category == 0)
        {
            return;
        }

        var bits = value < 0 ? value + ((1 << category) - 1) : value;
        writer.WriteBits(bits, category);
    }

    private static int ReceiveAndExtend(BitReader reader, int length)
    {
        if (length == 0)
        {
            return 0;
        }

        var value = reader.ReadBits(length);
        var threshold = 1 << (length - 1);
        return value < threshold ? value + ((-1 << length) + 1) : value;
    }

    private static int GetCategory(int value)
    {
        var magnitude = Math.Abs(value);
        var category = 0;
        while (magnitude > 0)
        {
            category++;
            magnitude >>= 1;
        }

        return category;
    }

    private static HuffmanEncodeTable[] CreateStandardHuffmanTables() =>
    [
        new(DcLuminanceBits, DcLuminanceValues),
        new(AcLuminanceBits, AcLuminanceValues),
        new(DcChrominanceBits, DcChrominanceValues),
        new(AcChrominanceBits, AcChrominanceValues)
    ];

    private static byte ClampToByte(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private sealed class DecoderState
    {
        public int Width;
        public int Height;
        public List<JpegComponent> Components { get; } = [];
        public List<ScanComponent> ScanComponents { get; } = [];
        public int[][] QuantTables { get; } = new int[4][];
        public HuffmanDecodeTable[] DcTables { get; } = new HuffmanDecodeTable[4];
        public HuffmanDecodeTable[] AcTables { get; } = new HuffmanDecodeTable[4];

        public JpegComponent FindComponent(int id) =>
            Components.FirstOrDefault(component => component.Id == id)
            ?? throw new InvalidDataException("JPEG scan references an unknown component.");
    }

    private sealed class JpegComponent
    {
        public int Id;
        public int HorizontalSampling;
        public int VerticalSampling;
        public int QuantTable;
        public int PreviousDc;
        public int BlocksWide;
        public int BlocksHigh;
        public byte[] Samples = [];
    }

    private readonly record struct ScanComponent(int Id, int DcTable, int AcTable);

    private readonly record struct HuffmanCode(int Code, int Length);

    private sealed class HuffmanEncodeTable
    {
        private readonly HuffmanCode[] _codes = new HuffmanCode[256];

        public HuffmanEncodeTable(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values)
        {
            var code = 0;
            var valueIndex = 0;
            for (var length = 1; length <= 16; length++)
            {
                for (var i = 0; i < bits[length - 1]; i++)
                {
                    _codes[values[valueIndex++]] = new HuffmanCode(code, length);
                    code++;
                }

                code <<= 1;
            }
        }

        public HuffmanCode this[int value] => _codes[value];
    }

    private sealed class HuffmanDecodeTable
    {
        private readonly Dictionary<int, int>[] _codes = Enumerable.Range(0, 17)
            .Select(_ => new Dictionary<int, int>())
            .ToArray();

        public HuffmanDecodeTable(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values)
        {
            var code = 0;
            var valueIndex = 0;
            for (var length = 1; length <= 16; length++)
            {
                for (var i = 0; i < bits[length - 1]; i++)
                {
                    _codes[length][code] = values[valueIndex++];
                    code++;
                }

                code <<= 1;
            }
        }

        public bool TryDecode(int length, int code, out int value) => _codes[length].TryGetValue(code, out value);
    }

    private sealed class BitReader(byte[] data)
    {
        private int _bitBuffer;
        private int _bitCount;
        private int _offset;

        public int ReadBit() => ReadBits(1);

        public int ReadBits(int count)
        {
            while (_bitCount < count)
            {
                if (_offset >= data.Length)
                {
                    throw new EndOfStreamException("Unexpected end of JPEG entropy data.");
                }

                _bitBuffer = (_bitBuffer << 8) | data[_offset++];
                _bitCount += 8;
            }

            var result = (_bitBuffer >> (_bitCount - count)) & ((1 << count) - 1);
            _bitCount -= count;
            return result;
        }
    }

    private sealed class BitWriter(Stream stream)
    {
        private int _bitBuffer;
        private int _bitCount;

        public void WriteBits(int bits, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                _bitBuffer = (_bitBuffer << 1) | ((bits >> i) & 1);
                _bitCount++;
                if (_bitCount == 8)
                {
                    WriteByte(_bitBuffer);
                    _bitBuffer = 0;
                    _bitCount = 0;
                }
            }
        }

        public void Flush()
        {
            if (_bitCount == 0)
            {
                return;
            }

            WriteByte((_bitBuffer << (8 - _bitCount)) | ((1 << (8 - _bitCount)) - 1));
            _bitBuffer = 0;
            _bitCount = 0;
        }

        private void WriteByte(int value)
        {
            stream.WriteByte((byte)value);
            if ((byte)value == 0xff)
            {
                stream.WriteByte(0x00);
            }
        }
    }
}
