using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class PalettedTextureCoder : IPitchTextureCoder
{
    private static readonly byte[] SQuantize4Bits = CreateQuantizeTable(4);
    private static readonly byte[] SQuantize5Bits = CreateQuantizeTable(5);
    private static readonly byte[] SQuantize6Bits = CreateQuantizeTable(6);

    private readonly PalettedTransfer _transfer;

    public PalettedTextureCoder(TextureFormat format)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetTransfer(format, out _);

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed row byte count.");
        }

        return checked(Format.HeaderByteCount + (rowPitch * height));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        DecodeByTransfer(source, destination, rowPitch);
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        EncodeByTransfer(source, destination, rowPitch);
    }

    private void DecodeByTransfer<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case PalettedTransfer.Palette4Rgb8:
                Decode<TPixel, Palette4IndexTransfer, Rgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgba8:
                Decode<TPixel, Palette4IndexTransfer, Rgba8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Abgr8:
                Decode<TPixel, Palette4IndexTransfer, Abgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Argb8:
                Decode<TPixel, Palette4IndexTransfer, Argb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Bgra8:
                Decode<TPixel, Palette4IndexTransfer, Bgra8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Xbgr8:
                Decode<TPixel, Palette4IndexTransfer, Xbgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Xrgb8:
                Decode<TPixel, Palette4IndexTransfer, Xrgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgbx8:
                Decode<TPixel, Palette4IndexTransfer, Rgbx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Bgrx8:
                Decode<TPixel, Palette4IndexTransfer, Bgrx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgb565:
                Decode<TPixel, Palette4IndexTransfer, Rgb565PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgba4:
                Decode<TPixel, Palette4IndexTransfer, Rgba4PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgb5A1:
                Decode<TPixel, Palette4IndexTransfer, Rgb5A1PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgb8:
                Decode<TPixel, Palette8IndexTransfer, Rgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgba8:
                Decode<TPixel, Palette8IndexTransfer, Rgba8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Abgr8:
                Decode<TPixel, Palette8IndexTransfer, Abgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Argb8:
                Decode<TPixel, Palette8IndexTransfer, Argb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Bgra8:
                Decode<TPixel, Palette8IndexTransfer, Bgra8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Xbgr8:
                Decode<TPixel, Palette8IndexTransfer, Xbgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Xrgb8:
                Decode<TPixel, Palette8IndexTransfer, Xrgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgbx8:
                Decode<TPixel, Palette8IndexTransfer, Rgbx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Bgrx8:
                Decode<TPixel, Palette8IndexTransfer, Bgrx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgb565:
                Decode<TPixel, Palette8IndexTransfer, Rgb565PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgba4:
                Decode<TPixel, Palette8IndexTransfer, Rgba4PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgb5A1:
                Decode<TPixel, Palette8IndexTransfer, Rgb5A1PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeByTransfer<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case PalettedTransfer.Palette4Rgb8:
                Encode<TPixel, Palette4IndexTransfer, Rgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgba8:
                Encode<TPixel, Palette4IndexTransfer, Rgba8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Abgr8:
                Encode<TPixel, Palette4IndexTransfer, Abgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Argb8:
                Encode<TPixel, Palette4IndexTransfer, Argb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Bgra8:
                Encode<TPixel, Palette4IndexTransfer, Bgra8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Xbgr8:
                Encode<TPixel, Palette4IndexTransfer, Xbgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Xrgb8:
                Encode<TPixel, Palette4IndexTransfer, Xrgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgbx8:
                Encode<TPixel, Palette4IndexTransfer, Rgbx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Bgrx8:
                Encode<TPixel, Palette4IndexTransfer, Bgrx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgb565:
                Encode<TPixel, Palette4IndexTransfer, Rgb565PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgba4:
                Encode<TPixel, Palette4IndexTransfer, Rgba4PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette4Rgb5A1:
                Encode<TPixel, Palette4IndexTransfer, Rgb5A1PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgb8:
                Encode<TPixel, Palette8IndexTransfer, Rgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgba8:
                Encode<TPixel, Palette8IndexTransfer, Rgba8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Abgr8:
                Encode<TPixel, Palette8IndexTransfer, Abgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Argb8:
                Encode<TPixel, Palette8IndexTransfer, Argb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Bgra8:
                Encode<TPixel, Palette8IndexTransfer, Bgra8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Xbgr8:
                Encode<TPixel, Palette8IndexTransfer, Xbgr8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Xrgb8:
                Encode<TPixel, Palette8IndexTransfer, Xrgb8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgbx8:
                Encode<TPixel, Palette8IndexTransfer, Rgbx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Bgrx8:
                Encode<TPixel, Palette8IndexTransfer, Bgrx8PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgb565:
                Encode<TPixel, Palette8IndexTransfer, Rgb565PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgba4:
                Encode<TPixel, Palette8IndexTransfer, Rgba4PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            case PalettedTransfer.Palette8Rgb5A1:
                Encode<TPixel, Palette8IndexTransfer, Rgb5A1PaletteEntryTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void Decode<TPixel, TIndexTransfer, TEntryTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TIndexTransfer : struct, IPaletteIndexTransfer
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[TIndexTransfer.PaletteEntryCount];
        ReadPalette<TEntryTransfer>(source[..Format.HeaderByteCount], palette);

        var indexData = source[Format.HeaderByteCount..];
        var rowByteCount = Format.GetRowByteCount(destination.Width);
        var indexRowOffset = 0;
        var pixelRowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var row = indexData.Slice(indexRowOffset, rowByteCount);
            var destinationRow = destination.Pixels.Slice(pixelRowOffset, destination.Width);
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba8UNorm(palette[TIndexTransfer.ReadIndex(row, x)]);
            }

            indexRowOffset += rowPitch;
            pixelRowOffset += destination.Width;
        }
    }

    private void Encode<TPixel, TIndexTransfer, TEntryTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TIndexTransfer : struct, IPaletteIndexTransfer
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var pixelCount = checked(source.Width * source.Height);
        var pixelBuffer = ArrayPool<Rgba8UNorm>.Shared.Rent(pixelCount);
        var pixels = pixelBuffer.AsSpan(0, pixelCount);

        try
        {
            var pixelRowOffset = 0;
            for (var y = 0; y < source.Height; y++)
            {
                var sourceRow = source.Pixels.Slice(pixelRowOffset, source.Width);
                var pixelIndex = pixelRowOffset;
                for (var x = 0; x < source.Width; x++)
                {
                    pixels[pixelIndex++] = TPixel.ToRgba8UNorm(sourceRow[x]);
                }

                pixelRowOffset += source.Width;
            }

            Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[TIndexTransfer.PaletteEntryCount];
            BuildPalette<TEntryTransfer>(pixels, palette);
            WritePalette<TEntryTransfer>(palette, destination[..Format.HeaderByteCount]);

            var nearestPaletteIndices = new Dictionary<uint, int>(Math.Min(pixelCount, 4096));
            var indexData = destination[Format.HeaderByteCount..];
            var rowByteCount = Format.GetRowByteCount(source.Width);
            var indexRowOffset = 0;
            pixelRowOffset = 0;
            for (var y = 0; y < source.Height; y++)
            {
                var row = indexData.Slice(indexRowOffset, rowByteCount);
                if (TIndexTransfer.RequiresRowClear)
                {
                    row.Clear();
                }

                var pixelIndex = pixelRowOffset;
                for (var x = 0; x < source.Width; x++)
                {
                    TIndexTransfer.WriteIndex(row, x, FindNearestPaletteIndex<TEntryTransfer>(pixels[pixelIndex++], palette, nearestPaletteIndices));
                }

                indexRowOffset += rowPitch;
                pixelRowOffset += source.Width;
            }
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(pixelBuffer);
        }
    }

    private static void ReadPalette<TEntryTransfer>(ReadOnlySpan<byte> source, Span<Rgba8UNorm> palette)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var offset = 0;
        for (var i = 0; i < palette.Length; i++)
        {
            var entry = source.Slice(offset, TEntryTransfer.PaletteEntryByteCount);
            palette[i] = TEntryTransfer.Decode(entry);
            offset += TEntryTransfer.PaletteEntryByteCount;
        }
    }

    private static void WritePalette<TEntryTransfer>(ReadOnlySpan<Rgba8UNorm> palette, Span<byte> destination)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var offset = 0;
        for (var i = 0; i < palette.Length; i++)
        {
            TEntryTransfer.Encode(palette[i], destination.Slice(offset, TEntryTransfer.PaletteEntryByteCount));
            offset += TEntryTransfer.PaletteEntryByteCount;
        }
    }

    private interface IPaletteIndexTransfer
    {
        static abstract int PaletteEntryCount { get; }

        static abstract bool RequiresRowClear { get; }

        static abstract int ReadIndex(ReadOnlySpan<byte> row, int x);

        static abstract void WriteIndex(Span<byte> row, int x, int index);
    }

    private interface IPaletteEntryTransfer
    {
        static abstract int PaletteEntryByteCount { get; }

        static abstract bool HasAlpha { get; }

        static abstract Rgba8UNorm Decode(ReadOnlySpan<byte> source);

        static abstract void Encode(Rgba8UNorm color, Span<byte> destination);

        static abstract Rgba8UNorm Quantize(Rgba8UNorm color);
    }

    private readonly struct Palette4IndexTransfer : IPaletteIndexTransfer
    {
        public static int PaletteEntryCount => 16;

        public static bool RequiresRowClear => true;

        public static int ReadIndex(ReadOnlySpan<byte> row, int x)
        {
            var value = row[x / 2];
            return (x & 1) == 0 ? value >> 4 : value & 0xf;
        }

        public static void WriteIndex(Span<byte> row, int x, int index)
        {
            var offset = x / 2;
            if ((x & 1) == 0)
            {
                row[offset] = (byte)((row[offset] & 0x0f) | (index << 4));
                return;
            }

            row[offset] = (byte)((row[offset] & 0xf0) | index);
        }
    }

    private readonly struct Palette8IndexTransfer : IPaletteIndexTransfer
    {
        public static int PaletteEntryCount => 256;

        public static bool RequiresRowClear => false;

        public static int ReadIndex(ReadOnlySpan<byte> row, int x) => row[x];

        public static void WriteIndex(Span<byte> row, int x, int index) =>
            row[x] = (byte)index;
    }

    private readonly struct Rgb8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 3;

        public static bool HasAlpha => false;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[0], source[1], source[2]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Red;
            destination[1] = color.Green;
            destination[2] = color.Blue;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) =>
            new(color.Red, color.Green, color.Blue);
    }

    private readonly struct Rgba8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => true;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[0], source[1], source[2], source[3]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Red;
            destination[1] = color.Green;
            destination[2] = color.Blue;
            destination[3] = color.Alpha;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) => color;
    }

    private readonly struct Abgr8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => true;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[0], source[1], source[2], source[3]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Red;
            destination[1] = color.Green;
            destination[2] = color.Blue;
            destination[3] = color.Alpha;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) => color;
    }

    private readonly struct Argb8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => true;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[2], source[1], source[0], source[3]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Blue;
            destination[1] = color.Green;
            destination[2] = color.Red;
            destination[3] = color.Alpha;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) => color;
    }

    private readonly struct Bgra8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => true;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[1], source[2], source[3], source[0]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Alpha;
            destination[1] = color.Red;
            destination[2] = color.Green;
            destination[3] = color.Blue;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) => color;
    }

    private readonly struct Xbgr8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => false;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[0], source[1], source[2]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Red;
            destination[1] = color.Green;
            destination[2] = color.Blue;
            destination[3] = 0;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) =>
            new(color.Red, color.Green, color.Blue);
    }

    private readonly struct Xrgb8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => false;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[2], source[1], source[0]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = color.Blue;
            destination[1] = color.Green;
            destination[2] = color.Red;
            destination[3] = 0;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) =>
            new(color.Red, color.Green, color.Blue);
    }

    private readonly struct Rgbx8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => false;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[3], source[2], source[1]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = 0;
            destination[1] = color.Blue;
            destination[2] = color.Green;
            destination[3] = color.Red;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) =>
            new(color.Red, color.Green, color.Blue);
    }

    private readonly struct Bgrx8PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 4;

        public static bool HasAlpha => false;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source) =>
            new(source[1], source[2], source[3]);

        public static void Encode(Rgba8UNorm color, Span<byte> destination)
        {
            destination[0] = 0;
            destination[1] = color.Red;
            destination[2] = color.Green;
            destination[3] = color.Blue;
        }

        public static Rgba8UNorm Quantize(Rgba8UNorm color) =>
            new(color.Red, color.Green, color.Blue);
    }

    private readonly struct Rgb565PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 2;

        public static bool HasAlpha => false;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(source);
            var red = (packed >> 11) & 0x1f;
            var green = (packed >> 5) & 0x3f;
            var blue = packed & 0x1f;
            return new Rgba8UNorm(
                (byte)((red << 3) | (red >> 2)),
                (byte)((green << 2) | (green >> 4)),
                (byte)((blue << 3) | (blue >> 2)));
        }

        public static void Encode(Rgba8UNorm color, Span<byte> destination) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, PackRgb565(color));

        public static Rgba8UNorm Quantize(Rgba8UNorm color)
        {
            var red = SQuantize5Bits[color.Red];
            var green = SQuantize6Bits[color.Green];
            var blue = SQuantize5Bits[color.Blue];
            return new Rgba8UNorm(
                (byte)((red << 3) | (red >> 2)),
                (byte)((green << 2) | (green >> 4)),
                (byte)((blue << 3) | (blue >> 2)));
        }
    }

    private readonly struct Rgba4PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 2;

        public static bool HasAlpha => true;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(source);
            var red = (packed >> 12) & 0xf;
            var green = (packed >> 8) & 0xf;
            var blue = (packed >> 4) & 0xf;
            var alpha = packed & 0xf;
            return new Rgba8UNorm(
                (byte)((red << 4) | red),
                (byte)((green << 4) | green),
                (byte)((blue << 4) | blue),
                (byte)((alpha << 4) | alpha));
        }

        public static void Encode(Rgba8UNorm color, Span<byte> destination) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, PackRgba4(color));

        public static Rgba8UNorm Quantize(Rgba8UNorm color)
        {
            var red = SQuantize4Bits[color.Red];
            var green = SQuantize4Bits[color.Green];
            var blue = SQuantize4Bits[color.Blue];
            var alpha = SQuantize4Bits[color.Alpha];
            return new Rgba8UNorm(
                (byte)((red << 4) | red),
                (byte)((green << 4) | green),
                (byte)((blue << 4) | blue),
                (byte)((alpha << 4) | alpha));
        }
    }

    private readonly struct Rgb5A1PaletteEntryTransfer : IPaletteEntryTransfer
    {
        public static int PaletteEntryByteCount => 2;

        public static bool HasAlpha => true;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> source)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(source);
            var red = (packed >> 11) & 0x1f;
            var green = (packed >> 6) & 0x1f;
            var blue = (packed >> 1) & 0x1f;
            return new Rgba8UNorm(
                (byte)((red << 3) | (red >> 2)),
                (byte)((green << 3) | (green >> 2)),
                (byte)((blue << 3) | (blue >> 2)),
                (packed & 0x1) == 0 ? (byte)0 : byte.MaxValue);
        }

        public static void Encode(Rgba8UNorm color, Span<byte> destination) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, PackRgb5A1(color));

        public static Rgba8UNorm Quantize(Rgba8UNorm color)
        {
            var red = SQuantize5Bits[color.Red];
            var green = SQuantize5Bits[color.Green];
            var blue = SQuantize5Bits[color.Blue];
            return new Rgba8UNorm(
                (byte)((red << 3) | (red >> 2)),
                (byte)((green << 3) | (green >> 2)),
                (byte)((blue << 3) | (blue >> 2)),
                color.Alpha >= 128 ? byte.MaxValue : (byte)0);
        }
    }

    private static void BuildPalette<TEntryTransfer>(ReadOnlySpan<Rgba8UNorm> source, Span<Rgba8UNorm> destination)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var colors = CollectQuantizedColors<TEntryTransfer>(source);
        if (colors.Count == 0)
        {
            destination.Clear();
            return;
        }

        if (colors.Count <= destination.Length)
        {
            for (var i = 0; i < colors.Count; i++)
            {
                destination[i] = colors[i].Color;
            }

            FillRemainingPaletteEntries(destination, colors.Count);
            return;
        }

        var buckets = new List<List<WeightedColor>>(destination.Length) { colors };
        while (buckets.Count < destination.Length)
        {
            var splitIndex = SelectBucketToSplit<TEntryTransfer>(buckets);
            if (splitIndex < 0)
            {
                break;
            }

            var bucket = buckets[splitIndex];
            var channel = SelectSplitChannel<TEntryTransfer>(bucket);
            bucket.Sort((a, b) => GetChannel(a.Color, channel).CompareTo(GetChannel(b.Color, channel)));
            var splitPoint = FindWeightedSplitPoint(bucket);
            var rightCount = bucket.Count - splitPoint;
            var right = bucket.GetRange(splitPoint, rightCount);
            bucket.RemoveRange(splitPoint, rightCount);
            buckets.Add(right);
        }

        for (var i = 0; i < buckets.Count; i++)
        {
            destination[i] = QuantizeForPalette<TEntryTransfer>(AverageBucket<TEntryTransfer>(buckets[i]));
        }

        FillRemainingPaletteEntries(destination, buckets.Count);
    }

    private static List<WeightedColor> CollectQuantizedColors<TEntryTransfer>(ReadOnlySpan<Rgba8UNorm> source)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var colors = new List<WeightedColor>();
        var indices = new Dictionary<uint, int>();
        foreach (var pixel in source)
        {
            var color = QuantizeForPalette<TEntryTransfer>(pixel);
            var key = PackKey<TEntryTransfer>(color);
            if (indices.TryGetValue(key, out var existingIndex))
            {
                var existing = colors[existingIndex];
                colors[existingIndex] = existing with { Count = existing.Count + 1 };
                continue;
            }

            indices.Add(key, colors.Count);
            colors.Add(new WeightedColor(color, 1));
        }

        return colors;
    }

    private static Rgba8UNorm QuantizeForPalette<TEntryTransfer>(Rgba8UNorm color)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        return TEntryTransfer.Quantize(color);
    }

    private static int SelectBucketToSplit<TEntryTransfer>(List<List<WeightedColor>> buckets)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var bestIndex = -1;
        long bestScore = -1;
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            if (bucket.Count < 2)
            {
                continue;
            }

            GetBounds<TEntryTransfer>(bucket, out var minRed, out var maxRed, out var minGreen, out var maxGreen, out var minBlue, out var maxBlue, out var minAlpha, out var maxAlpha, out var totalWeight);
            var range = Math.Max(Math.Max(maxRed - minRed, maxGreen - minGreen), maxBlue - minBlue);
            if (TEntryTransfer.HasAlpha)
            {
                range = Math.Max(range, maxAlpha - minAlpha);
            }

            var score = (long)range * totalWeight;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int SelectSplitChannel<TEntryTransfer>(List<WeightedColor> bucket)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        GetBounds<TEntryTransfer>(bucket, out var minRed, out var maxRed, out var minGreen, out var maxGreen, out var minBlue, out var maxBlue, out var minAlpha, out var maxAlpha, out _);
        var channel = 0;
        var range = maxRed - minRed;
        if (maxGreen - minGreen > range)
        {
            channel = 1;
            range = maxGreen - minGreen;
        }

        if (maxBlue - minBlue > range)
        {
            channel = 2;
            range = maxBlue - minBlue;
        }

        if (TEntryTransfer.HasAlpha && maxAlpha - minAlpha > range)
        {
            channel = 3;
        }

        return channel;
    }

    private static void GetBounds<TEntryTransfer>(
        List<WeightedColor> bucket,
        out int minRed,
        out int maxRed,
        out int minGreen,
        out int maxGreen,
        out int minBlue,
        out int maxBlue,
        out int minAlpha,
        out int maxAlpha,
        out int totalWeight)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        minRed = minGreen = minBlue = minAlpha = 255;
        maxRed = maxGreen = maxBlue = maxAlpha = 0;
        totalWeight = 0;
        foreach (var entry in bucket)
        {
            var color = entry.Color;
            minRed = Math.Min(minRed, color.Red);
            maxRed = Math.Max(maxRed, color.Red);
            minGreen = Math.Min(minGreen, color.Green);
            maxGreen = Math.Max(maxGreen, color.Green);
            minBlue = Math.Min(minBlue, color.Blue);
            maxBlue = Math.Max(maxBlue, color.Blue);
            if (TEntryTransfer.HasAlpha)
            {
                minAlpha = Math.Min(minAlpha, color.Alpha);
                maxAlpha = Math.Max(maxAlpha, color.Alpha);
            }

            totalWeight += entry.Count;
        }
    }

    private static int FindWeightedSplitPoint(List<WeightedColor> bucket)
    {
        var total = 0;
        foreach (var entry in bucket)
        {
            total += entry.Count;
        }

        var half = total / 2;
        var cumulative = 0;
        for (var i = 0; i < bucket.Count - 1; i++)
        {
            cumulative += bucket[i].Count;
            if (cumulative >= half)
            {
                return i + 1;
            }
        }

        return bucket.Count / 2;
    }

    private static Rgba8UNorm AverageBucket<TEntryTransfer>(List<WeightedColor> bucket)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        Debug.Assert(bucket.Count > 0);

        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        var total = 0;
        foreach (var entry in bucket)
        {
            red += (long)entry.Color.Red * entry.Count;
            green += (long)entry.Color.Green * entry.Count;
            blue += (long)entry.Color.Blue * entry.Count;
            alpha += (long)entry.Color.Alpha * entry.Count;
            total += entry.Count;
        }

        Debug.Assert(total > 0);

        return new Rgba8UNorm(
            (byte)((red + (total / 2)) / total),
            (byte)((green + (total / 2)) / total),
            (byte)((blue + (total / 2)) / total),
            TEntryTransfer.HasAlpha ? (byte)((alpha + (total / 2)) / total) : byte.MaxValue);
    }

    private static void FillRemainingPaletteEntries(Span<Rgba8UNorm> destination, int start)
    {
        var fill = start > 0 ? destination[start - 1] : default;
        for (var i = start; i < destination.Length; i++)
        {
            destination[i] = fill;
        }
    }

    private static int FindNearestPaletteIndex<TEntryTransfer>(
        Rgba8UNorm color,
        ReadOnlySpan<Rgba8UNorm> palette,
        Dictionary<uint, int> nearestPaletteIndices)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        if (!TEntryTransfer.HasAlpha)
        {
            color = new Rgba8UNorm(color.Red, color.Green, color.Blue);
        }

        var key = PackKey<TEntryTransfer>(color);
        if (nearestPaletteIndices.TryGetValue(key, out var cachedIndex))
        {
            return cachedIndex;
        }

        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var distance = ColorDistance<TEntryTransfer>(color, palette[i]);
            if (distance == 0)
            {
                nearestPaletteIndices[key] = i;
                return i;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        nearestPaletteIndices[key] = bestIndex;
        return bestIndex;
    }

    private static int ColorDistance<TEntryTransfer>(Rgba8UNorm a, Rgba8UNorm b)
        where TEntryTransfer : struct, IPaletteEntryTransfer
    {
        var red = a.Red - b.Red;
        var green = a.Green - b.Green;
        var blue = a.Blue - b.Blue;
        var alpha = TEntryTransfer.HasAlpha ? a.Alpha - b.Alpha : 0;
        return (red * red) + (green * green) + (blue * blue) + (alpha * alpha);
    }

    private static uint PackKey<TEntryTransfer>(Rgba8UNorm color)
        where TEntryTransfer : struct, IPaletteEntryTransfer =>
        ((uint)color.Red << 24) | ((uint)color.Green << 16) | ((uint)color.Blue << 8) | (TEntryTransfer.HasAlpha ? color.Alpha : byte.MaxValue);

    private static int GetChannel(Rgba8UNorm color, int channel) => channel switch
    {
        0 => color.Red,
        1 => color.Green,
        2 => color.Blue,
        3 => color.Alpha,
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    private static ushort PackRgb565(Rgba8UNorm color) =>
        (ushort)((SQuantize5Bits[color.Red] << 11) | (SQuantize6Bits[color.Green] << 5) | SQuantize5Bits[color.Blue]);

    private static ushort PackRgba4(Rgba8UNorm color) =>
        (ushort)((SQuantize4Bits[color.Red] << 12) | (SQuantize4Bits[color.Green] << 8) | (SQuantize4Bits[color.Blue] << 4) | SQuantize4Bits[color.Alpha]);

    private static ushort PackRgb5A1(Rgba8UNorm color) =>
        (ushort)((SQuantize5Bits[color.Red] << 11) | (SQuantize5Bits[color.Green] << 6) | (SQuantize5Bits[color.Blue] << 1) | (color.Alpha >= 128 ? 1 : 0));

    private static byte[] CreateQuantizeTable(int bits)
    {
        var table = new byte[256];
        var maxValue = (1 << bits) - 1;
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)(((i * maxValue) + 127) / 255);
        }

        return table;
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException($"Source span is too small for {width}x{height} {Format.Name} data. Required at least {requiredBytes} bytes.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException($"Destination span is too small for {width}x{height} {Format.Name} data. Required at least {requiredBytes} bytes.", nameof(destination));
        }
    }

    private static bool TryGetTransfer(TextureFormat format, out PalettedTransfer transfer)
    {
        if (format == TextureFormats.Palette4Rgb8)
        {
            transfer = PalettedTransfer.Palette4Rgb8;
            return true;
        }

        if (format == TextureFormats.Palette4Rgba8)
        {
            transfer = PalettedTransfer.Palette4Rgba8;
            return true;
        }

        if (format == TextureFormats.Palette4Abgr8) { transfer = PalettedTransfer.Palette4Abgr8; return true; }
        if (format == TextureFormats.Palette4Argb8) { transfer = PalettedTransfer.Palette4Argb8; return true; }
        if (format == TextureFormats.Palette4Bgra8) { transfer = PalettedTransfer.Palette4Bgra8; return true; }
        if (format == TextureFormats.Palette4Xbgr8) { transfer = PalettedTransfer.Palette4Xbgr8; return true; }
        if (format == TextureFormats.Palette4Xrgb8) { transfer = PalettedTransfer.Palette4Xrgb8; return true; }
        if (format == TextureFormats.Palette4Rgbx8) { transfer = PalettedTransfer.Palette4Rgbx8; return true; }
        if (format == TextureFormats.Palette4Bgrx8) { transfer = PalettedTransfer.Palette4Bgrx8; return true; }

        if (format == TextureFormats.Palette4Rgb565)
        {
            transfer = PalettedTransfer.Palette4Rgb565;
            return true;
        }

        if (format == TextureFormats.Palette4Rgba4)
        {
            transfer = PalettedTransfer.Palette4Rgba4;
            return true;
        }

        if (format == TextureFormats.Palette4Rgb5A1)
        {
            transfer = PalettedTransfer.Palette4Rgb5A1;
            return true;
        }

        if (format == TextureFormats.Palette8Rgb8)
        {
            transfer = PalettedTransfer.Palette8Rgb8;
            return true;
        }

        if (format == TextureFormats.Palette8Rgba8)
        {
            transfer = PalettedTransfer.Palette8Rgba8;
            return true;
        }

        if (format == TextureFormats.Palette8Abgr8) { transfer = PalettedTransfer.Palette8Abgr8; return true; }
        if (format == TextureFormats.Palette8Argb8) { transfer = PalettedTransfer.Palette8Argb8; return true; }
        if (format == TextureFormats.Palette8Bgra8) { transfer = PalettedTransfer.Palette8Bgra8; return true; }
        if (format == TextureFormats.Palette8Xbgr8) { transfer = PalettedTransfer.Palette8Xbgr8; return true; }
        if (format == TextureFormats.Palette8Xrgb8) { transfer = PalettedTransfer.Palette8Xrgb8; return true; }
        if (format == TextureFormats.Palette8Rgbx8) { transfer = PalettedTransfer.Palette8Rgbx8; return true; }
        if (format == TextureFormats.Palette8Bgrx8) { transfer = PalettedTransfer.Palette8Bgrx8; return true; }

        if (format == TextureFormats.Palette8Rgb565)
        {
            transfer = PalettedTransfer.Palette8Rgb565;
            return true;
        }

        if (format == TextureFormats.Palette8Rgba4)
        {
            transfer = PalettedTransfer.Palette8Rgba4;
            return true;
        }

        if (format == TextureFormats.Palette8Rgb5A1)
        {
            transfer = PalettedTransfer.Palette8Rgb5A1;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Paletted texture coder does not support texture format '{format.Name}'.");

    private readonly record struct WeightedColor(Rgba8UNorm Color, int Count);

    private enum PalettedTransfer
    {
        Palette4Rgb8,
        Palette4Rgba8,
        Palette4Abgr8,
        Palette4Argb8,
        Palette4Bgra8,
        Palette4Xbgr8,
        Palette4Xrgb8,
        Palette4Rgbx8,
        Palette4Bgrx8,
        Palette4Rgb565,
        Palette4Rgba4,
        Palette4Rgb5A1,
        Palette8Rgb8,
        Palette8Rgba8,
        Palette8Abgr8,
        Palette8Argb8,
        Palette8Bgra8,
        Palette8Xbgr8,
        Palette8Xrgb8,
        Palette8Rgbx8,
        Palette8Bgrx8,
        Palette8Rgb565,
        Palette8Rgba4,
        Palette8Rgb5A1
    }
}
