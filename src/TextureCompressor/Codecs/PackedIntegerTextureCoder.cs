using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedIntegerTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const uint AlphaMask = 0x3;
    private const int BytesPerTexel = sizeof(uint);
    private const uint ColorMask = 0x3ff;

    private readonly PackedIntegerKind _kind = GetPackedIntegerKind(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Rgb10A2UInt
        || format == TextureFormats.Rgb10A2RevUInt
        || format == TextureFormats.Rgb10A2RevSInt
        || format == TextureFormats.Bgr10A2RevUInt
        || format == TextureFormats.Bgr10A2RevSInt;

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
            case PackedIntegerKind.Rgb10A2UInt:
                Decode<TPixel, Rgb10A2UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgb10A2RevUInt:
                Decode<TPixel, Rgb10A2RevUIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Bgr10A2RevUInt:
                Decode<TPixel, Bgr10A2RevUIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgb10A2RevSInt:
                DecodeSigned<TPixel, Rgb10A2RevSIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Bgr10A2RevSInt:
                DecodeSigned<TPixel, Bgr10A2RevSIntTransfer>(source, destination, rowPitch);
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
            case PackedIntegerKind.Rgb10A2UInt:
                Encode<TPixel, Rgb10A2UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgb10A2RevUInt:
                Encode<TPixel, Rgb10A2RevUIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Bgr10A2RevUInt:
                Encode<TPixel, Bgr10A2RevUIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgb10A2RevSInt:
                EncodeSigned<TPixel, Rgb10A2RevSIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Bgr10A2RevSInt:
                EncodeSigned<TPixel, Bgr10A2RevSIntTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IPackedIntegerTransfer
    {
        static abstract Rgba16UNorm Decode(uint value);

        static abstract uint Encode(Rgba16UNorm value);
    }

    private interface IPackedSignedIntegerTransfer
    {
        static abstract Rgba16SNorm Decode(uint value);

        static abstract uint Encode(Rgba16SNorm value);
    }

    private readonly struct Rgb10A2UIntTransfer : IPackedIntegerTransfer
    {
        public static Rgba16UNorm Decode(uint value) =>
            new(
                (ushort)(value >> 22),
                (ushort)((value >> 12) & ColorMask),
                (ushort)((value >> 2) & ColorMask),
                (ushort)(value & AlphaMask));

        public static uint Encode(Rgba16UNorm value) =>
            (ClampUInt(value.Red, ColorMask) << 22)
            | (ClampUInt(value.Green, ColorMask) << 12)
            | (ClampUInt(value.Blue, ColorMask) << 2)
            | ClampUInt(value.Alpha, AlphaMask);
    }

    private readonly struct Rgb10A2RevUIntTransfer : IPackedIntegerTransfer
    {
        public static Rgba16UNorm Decode(uint value) =>
            new(
                (ushort)(value & ColorMask),
                (ushort)((value >> 10) & ColorMask),
                (ushort)((value >> 20) & ColorMask),
                (ushort)(value >> 30));

        public static uint Encode(Rgba16UNorm value) =>
            ClampUInt(value.Red, ColorMask)
            | (ClampUInt(value.Green, ColorMask) << 10)
            | (ClampUInt(value.Blue, ColorMask) << 20)
            | (ClampUInt(value.Alpha, AlphaMask) << 30);
    }

    private readonly struct Bgr10A2RevUIntTransfer : IPackedIntegerTransfer
    {
        public static Rgba16UNorm Decode(uint value) =>
            new(
                (ushort)((value >> 20) & ColorMask),
                (ushort)((value >> 10) & ColorMask),
                (ushort)(value & ColorMask),
                (ushort)(value >> 30));

        public static uint Encode(Rgba16UNorm value) =>
            ClampUInt(value.Blue, ColorMask)
            | (ClampUInt(value.Green, ColorMask) << 10)
            | (ClampUInt(value.Red, ColorMask) << 20)
            | (ClampUInt(value.Alpha, AlphaMask) << 30);
    }

    private readonly struct Rgb10A2RevSIntTransfer : IPackedSignedIntegerTransfer
    {
        public static Rgba16SNorm Decode(uint value) =>
            new(
                DecodeSInt(value & ColorMask, 10),
                DecodeSInt((value >> 10) & ColorMask, 10),
                DecodeSInt((value >> 20) & ColorMask, 10),
                DecodeSInt(value >> 30, 2));

        public static uint Encode(Rgba16SNorm value) =>
            EncodeSInt(value.Red, 10)
            | (EncodeSInt(value.Green, 10) << 10)
            | (EncodeSInt(value.Blue, 10) << 20)
            | (EncodeSInt(value.Alpha, 2) << 30);
    }

    private readonly struct Bgr10A2RevSIntTransfer : IPackedSignedIntegerTransfer
    {
        public static Rgba16SNorm Decode(uint value) =>
            new(
                DecodeSInt((value >> 20) & ColorMask, 10),
                DecodeSInt((value >> 10) & ColorMask, 10),
                DecodeSInt(value & ColorMask, 10),
                DecodeSInt(value >> 30, 2));

        public static uint Encode(Rgba16SNorm value) =>
            EncodeSInt(value.Blue, 10)
            | (EncodeSInt(value.Green, 10) << 10)
            | (EncodeSInt(value.Red, 10) << 20)
            | (EncodeSInt(value.Alpha, 2) << 30);
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedIntegerTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(texelOffset, BytesPerTexel));
                destinationRow[x] = TPixel.FromRgba16UNorm(TTransfer.Decode(value));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeSigned<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedSignedIntegerTransfer
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
        where TTransfer : IPackedIntegerTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                var value = TTransfer.Encode(TPixel.ToRgba16UNorm(sourceRow[x]));
                BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(texelOffset, BytesPerTexel), value);
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeSigned<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedSignedIntegerTransfer
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
            throw new ArgumentException("Source span is too small for the encoded packed integer texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded packed integer texture.", nameof(destination));
        }
    }

    private static uint ClampUInt(ushort value, uint max) =>
        value > max ? max : value;

    private static short DecodeSInt(uint value, int bits) =>
        (short)SignExtend(value, bits);

    private static uint EncodeSInt(short value, int bits)
    {
        var min = -(1 << (bits - 1));
        var max = (1 << (bits - 1)) - 1;
        var clamped = Math.Clamp((int)value, min, max);
        return (uint)clamped & ((1u << bits) - 1u);
    }

    private static int SignExtend(uint value, int bits)
    {
        var signBit = 1 << (bits - 1);
        var mask = (1 << bits) - 1;
        return (((int)value & mask) ^ signBit) - signBit;
    }

    private static PackedIntegerKind GetPackedIntegerKind(TextureFormat format)
    {
        if (format == TextureFormats.Rgb10A2UInt)
        {
            return PackedIntegerKind.Rgb10A2UInt;
        }

        if (format == TextureFormats.Rgb10A2RevUInt)
        {
            return PackedIntegerKind.Rgb10A2RevUInt;
        }

        if (format == TextureFormats.Rgb10A2RevSInt)
        {
            return PackedIntegerKind.Rgb10A2RevSInt;
        }

        if (format == TextureFormats.Bgr10A2RevUInt)
        {
            return PackedIntegerKind.Bgr10A2RevUInt;
        }

        if (format == TextureFormats.Bgr10A2RevSInt)
        {
            return PackedIntegerKind.Bgr10A2RevSInt;
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed integer texture codec does not support texture format '{format.Name}'.");

    private enum PackedIntegerKind
    {
        Rgb10A2UInt,
        Rgb10A2RevUInt,
        Bgr10A2RevUInt,
        Rgb10A2RevSInt,
        Bgr10A2RevSInt
    }
}
