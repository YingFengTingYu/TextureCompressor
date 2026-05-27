using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedSNormTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const int AlphaBits = 2;
    private const int ColorBits = 10;
    private const uint AlphaMask = 0x3;
    private const int BytesPerTexel = sizeof(uint);
    private const uint ColorMask = 0x3ff;

    private readonly PackedSNormKind _kind = GetPackedSNormKind(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Rgb10A2RevSNorm
        || format == TextureFormats.Bgr10A2RevSNorm;

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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        switch (_kind)
        {
            case PackedSNormKind.Rgb10A2RevSNorm:
                Decode<TPixel, Rgb10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Bgr10A2RevSNorm:
                Decode<TPixel, Bgr10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        switch (_kind)
        {
            case PackedSNormKind.Rgb10A2RevSNorm:
                Encode<TPixel, Rgb10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Bgr10A2RevSNorm:
                Encode<TPixel, Bgr10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IPackedSNormTransfer
    {
        static abstract Rgba16SNorm Decode(uint value);

        static abstract uint Encode(Rgba16SNorm value);
    }

    private readonly struct Rgb10A2RevSNormTransfer : IPackedSNormTransfer
    {
        public static Rgba16SNorm Decode(uint value) =>
            new(
                DecodeSNorm(value & ColorMask, ColorBits),
                DecodeSNorm((value >> 10) & ColorMask, ColorBits),
                DecodeSNorm((value >> 20) & ColorMask, ColorBits),
                DecodeSNorm(value >> 30, AlphaBits));

        public static uint Encode(Rgba16SNorm value) =>
            EncodeSNorm(value.Red, ColorBits)
            | (EncodeSNorm(value.Green, ColorBits) << 10)
            | (EncodeSNorm(value.Blue, ColorBits) << 20)
            | (EncodeSNorm(value.Alpha, AlphaBits) << 30);
    }

    private readonly struct Bgr10A2RevSNormTransfer : IPackedSNormTransfer
    {
        public static Rgba16SNorm Decode(uint value) =>
            new(
                DecodeSNorm((value >> 20) & ColorMask, ColorBits),
                DecodeSNorm((value >> 10) & ColorMask, ColorBits),
                DecodeSNorm(value & ColorMask, ColorBits),
                DecodeSNorm(value >> 30, AlphaBits));

        public static uint Encode(Rgba16SNorm value) =>
            EncodeSNorm(value.Blue, ColorBits)
            | (EncodeSNorm(value.Green, ColorBits) << 10)
            | (EncodeSNorm(value.Red, ColorBits) << 20)
            | (EncodeSNorm(value.Alpha, AlphaBits) << 30);
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedSNormTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(texelOffset, BytesPerTexel));
                destinationRow[x] = TPixel.FromRgba16SNorm(TTransfer.Decode(value));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedSNormTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                var value = TTransfer.Encode(TPixel.ToRgba16SNorm(sourceRow[x]));
                BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(texelOffset, BytesPerTexel), value);
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded packed SNorm texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded packed SNorm texture.", nameof(destination));
        }
    }

    private static short DecodeSNorm(uint value, int bits)
    {
        var sourceMax = (1 << (bits - 1)) - 1;
        return (short)ScaleSigned(SignExtend(value, bits), sourceMax, short.MaxValue);
    }

    private static uint EncodeSNorm(short value, int bits)
    {
        var targetMax = (1 << (bits - 1)) - 1;
        var signed = ScaleSigned(value, short.MaxValue, targetMax);
        return (uint)signed & ((1u << bits) - 1u);
    }

    private static int SignExtend(uint value, int bits)
    {
        var signBit = 1 << (bits - 1);
        var mask = (1 << bits) - 1;
        return ((int)value & mask ^ signBit) - signBit;
    }

    private static long ScaleSigned(long value, long sourceMax, long targetMax)
    {
        if (value <= -sourceMax)
        {
            return -targetMax;
        }

        if (value >= sourceMax)
        {
            return targetMax;
        }

        var magnitude = value < 0 ? (ulong)-value : (ulong)value;
        var scaled = (long)((magnitude * (ulong)targetMax + (ulong)sourceMax / 2) / (ulong)sourceMax);
        return value < 0 ? -scaled : scaled;
    }

    private static PackedSNormKind GetPackedSNormKind(TextureFormat format)
    {
        if (format == TextureFormats.Rgb10A2RevSNorm)
        {
            return PackedSNormKind.Rgb10A2RevSNorm;
        }

        if (format == TextureFormats.Bgr10A2RevSNorm)
        {
            return PackedSNormKind.Bgr10A2RevSNorm;
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed SNorm texture codec does not support texture format '{format.Name}'.");

    private enum PackedSNormKind
    {
        Rgb10A2RevSNorm,
        Bgr10A2RevSNorm
    }
}
