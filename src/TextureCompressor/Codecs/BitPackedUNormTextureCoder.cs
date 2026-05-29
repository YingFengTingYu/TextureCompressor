using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class BitPackedUNormTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const byte NibbleMask = 0x0f;

    private readonly BitPackedUNormTransfer _transfer = GetTransfer(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Alpha4UNorm
        || format == TextureFormats.Luminance4UNorm
        || format == TextureFormats.Intensity4UNorm
        || format == TextureFormats.Bw1BppUNorm;

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed row byte count.");
        }

        return checked(rowPitch * height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        switch (_transfer)
        {
            case BitPackedUNormTransfer.Alpha:
                Decode<TPixel, Alpha4Transfer>(source, destination, rowPitch);
                return;
            case BitPackedUNormTransfer.Luminance:
                Decode<TPixel, Luminance4Transfer>(source, destination, rowPitch);
                return;
            case BitPackedUNormTransfer.Intensity:
                Decode<TPixel, Intensity4Transfer>(source, destination, rowPitch);
                return;
            case BitPackedUNormTransfer.Bw1:
                Decode<TPixel, Bw1Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        switch (_transfer)
        {
            case BitPackedUNormTransfer.Alpha:
                Encode<TPixel, Alpha4Transfer>(source, destination, rowPitch);
                return;
            case BitPackedUNormTransfer.Luminance:
                Encode<TPixel, Luminance4Transfer>(source, destination, rowPitch);
                return;
            case BitPackedUNormTransfer.Intensity:
                Encode<TPixel, Intensity4Transfer>(source, destination, rowPitch);
                return;
            case BitPackedUNormTransfer.Bw1:
                Encode<TPixel, Bw1Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBitPackedUNormTransfer
    {
        var rowByteCount = GetDefaultPitch(destination.Width);
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var sourceRow = source.Slice(rowOffset, rowByteCount);
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba8UNorm(TTransfer.Decode(sourceRow, x));
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBitPackedUNormTransfer
    {
        var rowByteCount = GetDefaultPitch(source.Width);
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var destinationRow = destination.Slice(rowOffset, rowByteCount);
            destinationRow.Clear();

            var sourceRow = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(sourceRow[x], destinationRow, x);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IBitPackedUNormTransfer
    {
        static abstract Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x);

        static abstract void Encode<TPixel>(TPixel source, Span<byte> row, int x)
            where TPixel : unmanaged, IPixel<TPixel>;
    }

    private readonly struct Alpha4Transfer : IBitPackedUNormTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x) =>
            new(0, 0, 0, ExpandNibble(ReadNibble(row, x)));

        public static void Encode<TPixel>(TPixel source, Span<byte> row, int x)
            where TPixel : unmanaged, IPixel<TPixel> =>
            WriteNibble(row, x, (byte)(TPixel.ToRgba8UNorm(source).Alpha >> 4));
    }

    private readonly struct Luminance4Transfer : IBitPackedUNormTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x)
        {
            var value = ExpandNibble(ReadNibble(row, x));
            return new Rgba8UNorm(value, value, value);
        }

        public static void Encode<TPixel>(TPixel source, Span<byte> row, int x)
            where TPixel : unmanaged, IPixel<TPixel> =>
            WriteNibble(row, x, (byte)(TPixel.ToRgba8UNorm(source).Red >> 4));
    }

    private readonly struct Intensity4Transfer : IBitPackedUNormTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x)
        {
            var value = ExpandNibble(ReadNibble(row, x));
            return new Rgba8UNorm(value, value, value, value);
        }

        public static void Encode<TPixel>(TPixel source, Span<byte> row, int x)
            where TPixel : unmanaged, IPixel<TPixel> =>
            WriteNibble(row, x, (byte)(TPixel.ToRgba8UNorm(source).Red >> 4));
    }

    private readonly struct Bw1Transfer : IBitPackedUNormTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x)
        {
            var value = ReadBit(row, x) ? byte.MaxValue : byte.MinValue;
            return new Rgba8UNorm(value, value, value);
        }

        public static void Encode<TPixel>(TPixel source, Span<byte> row, int x)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (TPixel.ToRgba32Float(source).Red >= 0.5f)
            {
                SetBit(row, x);
            }
        }
    }

    private static bool ReadBit(ReadOnlySpan<byte> row, int x) =>
        (row[x >> 3] & (1 << (7 - (x & 7)))) != 0;

    private static void SetBit(Span<byte> row, int x) =>
        row[x >> 3] |= (byte)(1 << (7 - (x & 7)));

    private static byte ReadNibble(ReadOnlySpan<byte> row, int x)
    {
        var packed = row[x >> 1];
        return (x & 1) == 0
            ? (byte)(packed >> 4)
            : (byte)(packed & NibbleMask);
    }

    private static void WriteNibble(Span<byte> row, int x, byte value)
    {
        var byteIndex = x >> 1;
        if ((x & 1) == 0)
        {
            row[byteIndex] = (byte)((row[byteIndex] & NibbleMask) | (value << 4));
        }
        else
        {
            row[byteIndex] = (byte)((row[byteIndex] & 0xf0) | value);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded bit-packed UNorm texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded bit-packed UNorm texture.", nameof(destination));
        }
    }

    private static byte ExpandNibble(byte value) =>
        (byte)((value << 4) | value);

    private static BitPackedUNormTransfer GetTransfer(TextureFormat format)
    {
        if (format == TextureFormats.Alpha4UNorm)
        {
            return BitPackedUNormTransfer.Alpha;
        }

        if (format == TextureFormats.Luminance4UNorm)
        {
            return BitPackedUNormTransfer.Luminance;
        }

        if (format == TextureFormats.Intensity4UNorm)
        {
            return BitPackedUNormTransfer.Intensity;
        }

        if (format == TextureFormats.Bw1BppUNorm)
        {
            return BitPackedUNormTransfer.Bw1;
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Bit-packed UNorm texture codec does not support texture format '{format.Name}'.");

    private enum BitPackedUNormTransfer
    {
        Alpha,
        Luminance,
        Intensity,
        Bw1
    }
}
