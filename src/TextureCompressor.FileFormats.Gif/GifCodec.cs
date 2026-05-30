using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Gif;

public static class GifCodec
{
    private const int MaxPaletteSize = 256;
    private const int MaxLzwCode = 4096;

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
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = TPixel.FromRgba8UNorm(rgba.PixelSpan[i]);
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

        Span<byte> header = stackalloc byte[6];
        ReadExactly(stream, header);
        if (!header.SequenceEqual("GIF87a"u8) && !header.SequenceEqual("GIF89a"u8))
        {
            throw new InvalidDataException("The stream is not a GIF file.");
        }

        var screenWidth = ReadUInt16(stream);
        var screenHeight = ReadUInt16(stream);
        var packed = ReadByte(stream);
        var hasGlobalColorTable = (packed & 0x80) != 0;
        var globalColorTableSize = 1 << ((packed & 0x07) + 1);
        var backgroundColorIndex = ReadByte(stream);
        _ = ReadByte(stream);

        var globalColorTable = hasGlobalColorTable ? ReadColorTable(stream, globalColorTableSize) : null;
        var graphicsControl = GraphicsControl.Default;

        while (true)
        {
            var introducer = ReadByte(stream);
            switch (introducer)
            {
                case 0x21:
                    ReadExtension(stream, ref graphicsControl);
                    break;

                case 0x2c:
                    return ReadImage(stream, screenWidth, screenHeight, globalColorTable, backgroundColorIndex, graphicsControl);

                case 0x3b:
                    throw new InvalidDataException("GIF does not contain an image frame.");

                default:
                    throw new InvalidDataException($"Unexpected GIF block introducer 0x{introducer:x2}.");
            }
        }
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView());
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        Encode(bitmap, stream);
        return stream.ToArray();
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var indexed = Quantize(bitmap);
        var tableSize = NextPowerOfTwo(Math.Max(2, indexed.Palette.Length));
        var tablePower = GetTablePower(tableSize);
        var lzwMinimumCodeSize = Math.Max(2, tablePower + 1);
        var compressed = EncodeLzw(indexed.Indices, lzwMinimumCodeSize);

        stream.Write("GIF89a"u8);
        WriteUInt16(stream, bitmap.Width);
        WriteUInt16(stream, bitmap.Height);
        stream.WriteByte((byte)(0x80 | 0x70 | tablePower));
        stream.WriteByte(0);
        stream.WriteByte(0);
        WriteColorTable(stream, indexed.Palette, tableSize);

        if (indexed.TransparentIndex is not null)
        {
            stream.WriteByte(0x21);
            stream.WriteByte(0xf9);
            stream.WriteByte(4);
            stream.WriteByte(0x01);
            WriteUInt16(stream, 0);
            stream.WriteByte((byte)indexed.TransparentIndex.Value);
            stream.WriteByte(0);
        }

        stream.WriteByte(0x2c);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, bitmap.Width);
        WriteUInt16(stream, bitmap.Height);
        stream.WriteByte(0);
        stream.WriteByte((byte)lzwMinimumCodeSize);
        WriteSubBlocks(stream, compressed);
        stream.WriteByte(0x3b);
    }

    private static ArrayBitmap<Rgba8UNorm> ReadImage(
        Stream stream,
        int screenWidth,
        int screenHeight,
        Rgba8UNorm[]? globalColorTable,
        int backgroundColorIndex,
        GraphicsControl graphicsControl)
    {
        var left = ReadUInt16(stream);
        var top = ReadUInt16(stream);
        var width = ReadUInt16(stream);
        var height = ReadUInt16(stream);
        var packed = ReadByte(stream);
        var hasLocalColorTable = (packed & 0x80) != 0;
        var interlaced = (packed & 0x40) != 0;
        var localColorTableSize = 1 << ((packed & 0x07) + 1);
        var colorTable = hasLocalColorTable ? ReadColorTable(stream, localColorTableSize) : globalColorTable;
        if (colorTable is null)
        {
            throw new InvalidDataException("GIF image is missing a color table.");
        }

        var lzwMinimumCodeSize = ReadByte(stream);
        if (lzwMinimumCodeSize is < 2 or > 8)
        {
            throw new InvalidDataException("GIF LZW minimum code size must be between 2 and 8.");
        }

        var compressed = ReadSubBlocks(stream);
        var indices = DecodeLzw(compressed, lzwMinimumCodeSize, checked(width * height));
        if (interlaced)
        {
            indices = Deinterlace(indices, width, height);
        }

        var pixels = CreateBackground(screenWidth, screenHeight, colorTable, backgroundColorIndex, graphicsControl);
        for (var y = 0; y < height; y++)
        {
            var targetY = top + y;
            if ((uint)targetY >= (uint)screenHeight)
            {
                continue;
            }

            for (var x = 0; x < width; x++)
            {
                var targetX = left + x;
                if ((uint)targetX >= (uint)screenWidth)
                {
                    continue;
                }

                var index = indices[(y * width) + x];
                if (graphicsControl.TransparentColorIndex == index)
                {
                    continue;
                }

                if (index >= colorTable.Length)
                {
                    throw new InvalidDataException("GIF image references a color table entry that does not exist.");
                }

                pixels[(targetY * screenWidth) + targetX] = colorTable[index];
            }
        }

        return new ArrayBitmap<Rgba8UNorm>(screenWidth, screenHeight, pixels);
    }

    private static Rgba8UNorm[] CreateBackground(
        int width,
        int height,
        Rgba8UNorm[] colorTable,
        int backgroundColorIndex,
        GraphicsControl graphicsControl)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        if (backgroundColorIndex >= colorTable.Length || graphicsControl.TransparentColorIndex == backgroundColorIndex)
        {
            return pixels;
        }

        Array.Fill(pixels, colorTable[backgroundColorIndex]);
        return pixels;
    }

    private static void ReadExtension(Stream stream, ref GraphicsControl graphicsControl)
    {
        var label = ReadByte(stream);
        if (label == 0xf9)
        {
            var blockSize = ReadByte(stream);
            if (blockSize != 4)
            {
                throw new InvalidDataException("GIF graphics control extension has an invalid size.");
            }

            var packed = ReadByte(stream);
            var delay = ReadUInt16(stream);
            var transparentColorIndex = ReadByte(stream);
            var terminator = ReadByte(stream);
            if (terminator != 0)
            {
                throw new InvalidDataException("GIF graphics control extension is not terminated.");
            }

            graphicsControl = new GraphicsControl(
                (packed & 0x01) != 0 ? transparentColorIndex : -1,
                delay,
                (packed >> 2) & 0x07);
            return;
        }

        SkipSubBlocks(stream);
    }

    private static Rgba8UNorm[] ReadColorTable(Stream stream, int count)
    {
        var colors = new Rgba8UNorm[count];
        Span<byte> rgb = stackalloc byte[3];
        for (var i = 0; i < colors.Length; i++)
        {
            ReadExactly(stream, rgb);
            colors[i] = new Rgba8UNorm(rgb[0], rgb[1], rgb[2], byte.MaxValue);
        }

        return colors;
    }

    private static byte[] DecodeLzw(byte[] data, int minimumCodeSize, int expectedPixelCount)
    {
        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;
        var dictionary = new byte[MaxLzwCode][];
        var reader = new LsbBitReader(data);
        var output = new List<byte>(expectedPixelCount);
        var codeSize = minimumCodeSize + 1;
        var nextCode = ResetDictionary(dictionary, clearCode);
        byte[]? previous = null;

        while (output.Count < expectedPixelCount)
        {
            var code = reader.ReadBits(codeSize);
            if (code == clearCode)
            {
                codeSize = minimumCodeSize + 1;
                nextCode = ResetDictionary(dictionary, clearCode);
                previous = null;
                continue;
            }

            if (code == endCode)
            {
                break;
            }

            byte[] entry;
            if (code < nextCode && dictionary[code] is not null)
            {
                entry = dictionary[code];
            }
            else if (code == nextCode && previous is not null)
            {
                entry = AppendByte(previous, previous[0]);
            }
            else
            {
                throw new InvalidDataException("GIF LZW stream contains an invalid code.");
            }

            output.AddRange(entry);
            if (previous is not null && nextCode < MaxLzwCode)
            {
                dictionary[nextCode++] = AppendByte(previous, entry[0]);
                if (nextCode == (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previous = entry;
        }

        if (output.Count < expectedPixelCount)
        {
            throw new InvalidDataException("GIF LZW stream ended before the image was fully decoded.");
        }

        return output.GetRange(0, expectedPixelCount).ToArray();
    }

    private static byte[] EncodeLzw(byte[] indices, int minimumCodeSize)
    {
        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;
        var codeSize = minimumCodeSize + 1;
        var nextCode = endCode + 1;
        var maxCodeForSize = 1 << codeSize;
        var dictionary = new Dictionary<int, int>(MaxLzwCode);
        var writer = new LsbBitWriter();

        WriteCode(clearCode);
        var prefix = (int)indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            var pixel = indices[i];
            var key = (prefix << 8) | pixel;
            if (dictionary.TryGetValue(key, out var existingCode))
            {
                prefix = existingCode;
                continue;
            }

            WriteCode(prefix);
            if (nextCode < MaxLzwCode)
            {
                dictionary[key] = nextCode++;
            }
            else
            {
                WriteCode(clearCode);
                dictionary.Clear();
                codeSize = minimumCodeSize + 1;
                maxCodeForSize = 1 << codeSize;
                nextCode = endCode + 1;
            }

            prefix = pixel;
        }

        WriteCode(prefix);
        WriteCode(endCode);
        return writer.ToArray();

        void WriteCode(int code)
        {
            writer.WriteBits(code, codeSize);
            if (nextCode >= maxCodeForSize && codeSize < 12)
            {
                maxCodeForSize = 1 << ++codeSize;
            }
        }
    }

    private static int ResetDictionary(byte[][] dictionary, int clearCode)
    {
        Array.Clear(dictionary);
        for (var i = 0; i < clearCode; i++)
        {
            dictionary[i] = [(byte)i];
        }

        return clearCode + 2;
    }

    private static byte[] AppendByte(byte[] source, byte value)
    {
        var result = new byte[source.Length + 1];
        source.CopyTo(result, 0);
        result[^1] = value;
        return result;
    }

    private static byte[] Deinterlace(byte[] indices, int width, int height)
    {
        var result = new byte[indices.Length];
        var sourceOffset = 0;
        CopyInterlacePass(indices, result, width, height, 0, 8, ref sourceOffset);
        CopyInterlacePass(indices, result, width, height, 4, 8, ref sourceOffset);
        CopyInterlacePass(indices, result, width, height, 2, 4, ref sourceOffset);
        CopyInterlacePass(indices, result, width, height, 1, 2, ref sourceOffset);
        return result;
    }

    private static void CopyInterlacePass(byte[] source, byte[] destination, int width, int height, int startY, int stepY, ref int sourceOffset)
    {
        for (var y = startY; y < height; y += stepY)
        {
            Array.Copy(source, sourceOffset, destination, y * width, width);
            sourceOffset += width;
        }
    }

    private static IndexedImage Quantize<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var transparentIndex = -1;
        var palette = new List<Rgba8UNorm>(MaxPaletteSize);
        var colorToIndex = new Dictionary<int, byte>();
        var indices = new byte[checked(bitmap.Width * bitmap.Height)];

        if (HasTransparency(bitmap))
        {
            transparentIndex = 0;
            palette.Add(new Rgba8UNorm(0, 0, 0, 0));
        }

        var maxOpaqueColors = MaxPaletteSize - palette.Count;
        var useExactPalette = TryBuildExactPalette(bitmap, colorToIndex, palette, maxOpaqueColors);
        for (var i = 0; i < bitmap.Pixels.Length; i++)
        {
            var rgba = TPixel.ToRgba8UNorm(bitmap.Pixels[i]);
            if (transparentIndex >= 0 && rgba.Alpha < 128)
            {
                indices[i] = (byte)transparentIndex;
                continue;
            }

            var key = ToRgbKey(rgba);
            if (useExactPalette)
            {
                indices[i] = colorToIndex[key];
                continue;
            }

            indices[i] = GetRgb332Index(rgba, transparentIndex >= 0);
        }

        if (!useExactPalette)
        {
            BuildRgb332Palette(palette, transparentIndex >= 0);
        }

        return new IndexedImage(palette.ToArray(), indices, transparentIndex >= 0 ? transparentIndex : null);
    }

    private static bool HasTransparency<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (var pixel in bitmap.Pixels)
        {
            if (TPixel.ToRgba8UNorm(pixel).Alpha < 128)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildExactPalette<TPixel>(
        BitmapView<TPixel> bitmap,
        Dictionary<int, byte> colorToIndex,
        List<Rgba8UNorm> palette,
        int maxOpaqueColors)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (var pixel in bitmap.Pixels)
        {
            var rgba = TPixel.ToRgba8UNorm(pixel);
            if (rgba.Alpha < 128)
            {
                continue;
            }

            var key = ToRgbKey(rgba);
            if (colorToIndex.ContainsKey(key))
            {
                continue;
            }

            if (colorToIndex.Count == maxOpaqueColors)
            {
                palette.RemoveRange(palette.Count - colorToIndex.Count, colorToIndex.Count);
                colorToIndex.Clear();
                return false;
            }

            colorToIndex[key] = (byte)palette.Count;
            palette.Add(new Rgba8UNorm(rgba.Red, rgba.Green, rgba.Blue, byte.MaxValue));
        }

        return true;
    }

    private static void BuildRgb332Palette(List<Rgba8UNorm> palette, bool hasTransparentColor)
    {
        if (hasTransparentColor)
        {
            for (var i = 0; i < 255; i++)
            {
                palette.Add(FromRgb332Index((byte)i));
            }

            return;
        }

        for (var i = 0; i < 256; i++)
        {
            palette.Add(FromRgb332Index((byte)i));
        }
    }

    private static byte GetRgb332Index(Rgba8UNorm color, bool hasTransparentColor)
    {
        var index = (byte)((color.Red & 0xe0) | ((color.Green & 0xe0) >> 3) | (color.Blue >> 6));
        return hasTransparentColor ? (byte)(index == 255 ? 254 : index + 1) : index;
    }

    private static Rgba8UNorm FromRgb332Index(byte index)
    {
        var red = ExpandBits((index >> 5) & 0x07, 3);
        var green = ExpandBits((index >> 2) & 0x07, 3);
        var blue = ExpandBits(index & 0x03, 2);
        return new Rgba8UNorm(red, green, blue, byte.MaxValue);
    }

    private static byte ExpandBits(int value, int bits)
    {
        var max = (1 << bits) - 1;
        return (byte)((value * 255 + (max / 2)) / max);
    }

    private static int ToRgbKey(Rgba8UNorm color) => (color.Red << 16) | (color.Green << 8) | color.Blue;

    private static byte[] ReadSubBlocks(Stream stream)
    {
        using var data = new MemoryStream();
        while (true)
        {
            var length = ReadByte(stream);
            if (length == 0)
            {
                return data.ToArray();
            }

            var buffer = new byte[length];
            ReadExactly(stream, buffer);
            data.Write(buffer);
        }
    }

    private static void SkipSubBlocks(Stream stream)
    {
        while (true)
        {
            var length = ReadByte(stream);
            if (length == 0)
            {
                return;
            }

            var buffer = new byte[length];
            ReadExactly(stream, buffer);
        }
    }

    private static void WriteSubBlocks(Stream stream, ReadOnlySpan<byte> data)
    {
        while (!data.IsEmpty)
        {
            var length = Math.Min(255, data.Length);
            stream.WriteByte((byte)length);
            stream.Write(data[..length]);
            data = data[length..];
        }

        stream.WriteByte(0);
    }

    private static void WriteColorTable(Stream stream, Rgba8UNorm[] palette, int tableSize)
    {
        for (var i = 0; i < tableSize; i++)
        {
            var color = i < palette.Length ? palette[i] : default;
            stream.WriteByte(color.Red);
            stream.WriteByte(color.Green);
            stream.WriteByte(color.Blue);
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }

    private static int GetTablePower(int tableSize)
    {
        var power = 0;
        while ((1 << (power + 1)) < tableSize)
        {
            power++;
        }

        return power;
    }

    private static int ReadByte(Stream stream)
    {
        var value = stream.ReadByte();
        return value < 0 ? throw new EndOfStreamException("Unexpected end of GIF stream.") : value;
    }

    private static int ReadUInt16(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[2];
        ReadExactly(stream, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
        stream.Write(bytes);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of GIF stream.");
            }

            totalRead += read;
        }
    }

    private readonly record struct GraphicsControl(int TransparentColorIndex, int DelayTime, int DisposalMethod)
    {
        public static GraphicsControl Default { get; } = new(-1, 0, 0);
    }

    private readonly record struct IndexedImage(Rgba8UNorm[] Palette, byte[] Indices, int? TransparentIndex);

    private sealed class LsbBitReader(byte[] data)
    {
        private int _bitOffset;

        public int ReadBits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                if (_bitOffset >= data.Length * 8)
                {
                    throw new EndOfStreamException("Unexpected end of GIF LZW data.");
                }

                if ((data[_bitOffset >> 3] & (1 << (_bitOffset & 7))) != 0)
                {
                    value |= 1 << i;
                }

                _bitOffset++;
            }

            return value;
        }
    }

    private sealed class LsbBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _currentByte;
        private int _bitCount;

        public void WriteBits(int value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _currentByte |= ((value >> i) & 1) << _bitCount;
                _bitCount++;
                if (_bitCount == 8)
                {
                    _bytes.Add((byte)_currentByte);
                    _currentByte = 0;
                    _bitCount = 0;
                }
            }
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                _bytes.Add((byte)_currentByte);
                _currentByte = 0;
                _bitCount = 0;
            }

            return _bytes.ToArray();
        }
    }
}
