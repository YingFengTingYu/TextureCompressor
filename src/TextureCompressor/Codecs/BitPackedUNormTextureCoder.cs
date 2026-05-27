using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class BitPackedUNormTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const byte NibbleMask = 0x0f;

    private readonly BitPackedUNormTransfer _transfer = GetTransfer(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Alpha4UNorm
        || format == TextureFormats.Luminance4UNorm
        || format == TextureFormats.Intensity4UNorm;

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return checked((width + 1) / 2);
    }

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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        var rowByteCount = GetDefaultPitch(destination.Width);
        for (var y = 0; y < destination.Height; y++)
        {
            var sourceRow = source.Slice(checked(y * rowPitch), rowByteCount);
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba8UNorm(DecodeTexel(sourceRow, x));
            }
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var rowByteCount = GetDefaultPitch(source.Width);
        for (var y = 0; y < source.Height; y++)
        {
            var destinationRow = destination.Slice(checked(y * rowPitch), rowByteCount);
            destinationRow.Clear();

            var sourceRow = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                EncodeTexel(TPixel.ToRgba8UNorm(sourceRow[x]), destinationRow, x);
            }
        }
    }

    private Rgba8UNorm DecodeTexel(ReadOnlySpan<byte> row, int x)
    {
        var value = ExpandNibble(ReadNibble(row, x));
        return _transfer switch
        {
            BitPackedUNormTransfer.Alpha => new Rgba8UNorm(0, 0, 0, value),
            BitPackedUNormTransfer.Luminance => new Rgba8UNorm(value, value, value),
            BitPackedUNormTransfer.Intensity => new Rgba8UNorm(value, value, value, value),
            _ => throw CreateUnsupportedFormatException(Format)
        };
    }

    private void EncodeTexel(Rgba8UNorm source, Span<byte> row, int x)
    {
        // Single-channel luminance/intensity formats use red as their scalar carrier.
        var value = _transfer == BitPackedUNormTransfer.Alpha
            ? source.Alpha
            : source.Red;

        WriteNibble(row, x, (byte)(value >> 4));
    }

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

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Bit-packed UNorm texture codec does not support texture format '{format.Name}'.");

    private enum BitPackedUNormTransfer
    {
        Alpha,
        Luminance,
        Intensity
    }
}
