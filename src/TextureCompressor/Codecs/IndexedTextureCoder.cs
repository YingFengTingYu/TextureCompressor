using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class IndexedTextureCoder : IPitchTextureCoder
{
    private readonly IndexedTransfer _transfer;

    public IndexedTextureCoder(TextureFormat format)
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
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed indexed row byte count.");
        }

        return checked(rowPitch * height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        switch (_transfer)
        {
            case IndexedTransfer.Ai44:
                Decode<TPixel, Ai44Transfer>(source, destination, rowPitch);
                return;
            case IndexedTransfer.Ia44:
                Decode<TPixel, Ia44Transfer>(source, destination, rowPitch);
                return;
            case IndexedTransfer.P8:
                Decode<TPixel, P8Transfer>(source, destination, rowPitch);
                return;
            case IndexedTransfer.A8P8:
                Decode<TPixel, A8P8Transfer>(source, destination, rowPitch);
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
            case IndexedTransfer.Ai44:
                Encode<TPixel, Ai44Transfer>(source, destination, rowPitch);
                return;
            case IndexedTransfer.Ia44:
                Encode<TPixel, Ia44Transfer>(source, destination, rowPitch);
                return;
            case IndexedTransfer.P8:
                Encode<TPixel, P8Transfer>(source, destination, rowPitch);
                return;
            case IndexedTransfer.A8P8:
                Encode<TPixel, A8P8Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : struct, IIndexedTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var sourceRow = source[rowOffset..];
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba8UNorm(TTransfer.Decode(sourceRow, x));
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : struct, IIndexedTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var destinationRow = destination[rowOffset..];
            var sourceRow = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba8UNorm(sourceRow[x]), destinationRow, x);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IIndexedTransfer
    {
        static abstract Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x);

        static abstract void Encode(Rgba8UNorm color, Span<byte> row, int x);
    }

    private readonly struct Ai44Transfer : IIndexedTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x)
        {
            var value = row[x];
            return IntensityAlpha(ExpandNibble(value & 0x0f), ExpandNibble(value >> 4));
        }

        public static void Encode(Rgba8UNorm color, Span<byte> row, int x) =>
            row[x] = (byte)((QuantizeNibble(color.Alpha) << 4) | QuantizeNibble(color.Red));
    }

    private readonly struct Ia44Transfer : IIndexedTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x)
        {
            var value = row[x];
            return IntensityAlpha(ExpandNibble(value >> 4), ExpandNibble(value & 0x0f));
        }

        public static void Encode(Rgba8UNorm color, Span<byte> row, int x) =>
            row[x] = (byte)((QuantizeNibble(color.Red) << 4) | QuantizeNibble(color.Alpha));
    }

    private readonly struct P8Transfer : IIndexedTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x) => Intensity(row[x]);

        public static void Encode(Rgba8UNorm color, Span<byte> row, int x) => row[x] = color.Red;
    }

    private readonly struct A8P8Transfer : IIndexedTransfer
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> row, int x) => IntensityAlpha(row[x * 2], row[(x * 2) + 1]);

        public static void Encode(Rgba8UNorm color, Span<byte> row, int x)
        {
            row[x * 2] = color.Red;
            row[(x * 2) + 1] = color.Alpha;
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded indexed texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded indexed texture.", nameof(destination));
        }
    }

    private static Rgba8UNorm Intensity(byte value) => new(value, value, value);

    private static Rgba8UNorm IntensityAlpha(byte value, byte alpha) => new(value, value, value, alpha);

    private static byte ExpandNibble(int value) => (byte)((value << 4) | value);

    private static int QuantizeNibble(byte value) => ((value * 15) + 127) / 255;

    private static bool TryGetTransfer(TextureFormat format, out IndexedTransfer transfer)
    {
        if (format == TextureFormats.Ai44)
        {
            transfer = IndexedTransfer.Ai44;
            return true;
        }

        if (format == TextureFormats.Ia44)
        {
            transfer = IndexedTransfer.Ia44;
            return true;
        }

        if (format == TextureFormats.P8)
        {
            transfer = IndexedTransfer.P8;
            return true;
        }

        if (format == TextureFormats.A8P8)
        {
            transfer = IndexedTransfer.A8P8;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Indexed texture coder does not support texture format '{format.Name}'.");

    private enum IndexedTransfer
    {
        Ai44,
        Ia44,
        P8,
        A8P8
    }
}
