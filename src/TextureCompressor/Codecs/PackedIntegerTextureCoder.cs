using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class PackedIntegerTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const uint AlphaMask = 0x3;
    private const uint ColorMask = 0x3ff;

    private readonly PackedIntegerKind _kind = GetPackedIntegerKind(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Rgb10A2UInt
        || format == TextureFormats.Rgb10A2RevUInt
        || format == TextureFormats.Rgb10A2RevSInt
        || format == TextureFormats.Bgr10A2RevUInt
        || format == TextureFormats.Bgr10A2RevSInt
        || format == TextureFormats.R10X6UInt
        || format == TextureFormats.R10X6G10X6UInt
        || format == TextureFormats.R10X6G10X6B10X6A10X6UInt
        || format == TextureFormats.R12X4UInt
        || format == TextureFormats.R12X4G12X4UInt
        || format == TextureFormats.R12X4G12X4B12X4A12X4UInt
        || format == TextureFormats.R14X2UInt
        || format == TextureFormats.R14X2G14X2UInt
        || format == TextureFormats.R14X2G14X2B14X2A14X2UInt;

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
            case PackedIntegerKind.R10X6UInt:
                Decode<TPixel, R10X6UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rg10X6UInt:
                Decode<TPixel, Rg10X6UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgba10X6UInt:
                Decode<TPixel, Rgba10X6UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.R12X4UInt:
                Decode<TPixel, R12X4UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rg12X4UInt:
                Decode<TPixel, Rg12X4UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgba12X4UInt:
                Decode<TPixel, Rgba12X4UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.R14X2UInt:
                Decode<TPixel, R14X2UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rg14X2UInt:
                Decode<TPixel, Rg14X2UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgba14X2UInt:
                Decode<TPixel, Rgba14X2UIntTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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
            case PackedIntegerKind.R10X6UInt:
                Encode<TPixel, R10X6UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rg10X6UInt:
                Encode<TPixel, Rg10X6UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgba10X6UInt:
                Encode<TPixel, Rgba10X6UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.R12X4UInt:
                Encode<TPixel, R12X4UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rg12X4UInt:
                Encode<TPixel, Rg12X4UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgba12X4UInt:
                Encode<TPixel, Rgba12X4UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.R14X2UInt:
                Encode<TPixel, R14X2UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rg14X2UInt:
                Encode<TPixel, Rg14X2UIntTransfer>(source, destination, rowPitch);
                return;
            case PackedIntegerKind.Rgba14X2UInt:
                Encode<TPixel, Rgba14X2UIntTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IPackedIntegerTransfer
    {
        static abstract int BytesPerTexel { get; }

        static abstract Rgba16UNorm Decode(ReadOnlySpan<byte> texel);

        static abstract void Encode(Rgba16UNorm value, Span<byte> texel);
    }

    private interface IPackedSignedIntegerTransfer
    {
        static abstract int BytesPerTexel { get; }

        static abstract Rgba16SNorm Decode(ReadOnlySpan<byte> texel);

        static abstract void Encode(Rgba16SNorm value, Span<byte> texel);
    }

    private readonly struct Rgb10A2UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new(
                (ushort)(value >> 22),
                (ushort)((value >> 12) & ColorMask),
                (ushort)((value >> 2) & ColorMask),
                (ushort)(value & AlphaMask));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(
                texel,
                (ClampUInt(value.Red, ColorMask) << 22)
                | (ClampUInt(value.Green, ColorMask) << 12)
                | (ClampUInt(value.Blue, ColorMask) << 2)
                | ClampUInt(value.Alpha, AlphaMask));
    }

    private readonly struct Rgb10A2RevUIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new(
                (ushort)(value & ColorMask),
                (ushort)((value >> 10) & ColorMask),
                (ushort)((value >> 20) & ColorMask),
                (ushort)(value >> 30));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(
                texel,
                ClampUInt(value.Red, ColorMask)
                | (ClampUInt(value.Green, ColorMask) << 10)
                | (ClampUInt(value.Blue, ColorMask) << 20)
                | (ClampUInt(value.Alpha, AlphaMask) << 30));
    }

    private readonly struct Bgr10A2RevUIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new(
                (ushort)((value >> 20) & ColorMask),
                (ushort)((value >> 10) & ColorMask),
                (ushort)(value & ColorMask),
                (ushort)(value >> 30));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(
                texel,
                ClampUInt(value.Blue, ColorMask)
                | (ClampUInt(value.Green, ColorMask) << 10)
                | (ClampUInt(value.Red, ColorMask) << 20)
                | (ClampUInt(value.Alpha, AlphaMask) << 30));
    }

    private readonly struct Rgb10A2RevSIntTransfer : IPackedSignedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new(
                DecodeSInt(value & ColorMask, 10),
                DecodeSInt((value >> 10) & ColorMask, 10),
                DecodeSInt((value >> 20) & ColorMask, 10),
                DecodeSInt(value >> 30, 2));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(
                texel,
                EncodeSInt(value.Red, 10)
                | (EncodeSInt(value.Green, 10) << 10)
                | (EncodeSInt(value.Blue, 10) << 20)
                | (EncodeSInt(value.Alpha, 2) << 30));
    }

    private readonly struct Bgr10A2RevSIntTransfer : IPackedSignedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new(
                DecodeSInt((value >> 20) & ColorMask, 10),
                DecodeSInt((value >> 10) & ColorMask, 10),
                DecodeSInt(value & ColorMask, 10),
                DecodeSInt(value >> 30, 2));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(
                texel,
                EncodeSInt(value.Blue, 10)
                | (EncodeSInt(value.Green, 10) << 10)
                | (EncodeSInt(value.Red, 10) << 20)
                | (EncodeSInt(value.Alpha, 2) << 30));
    }

    private readonly struct R10X6UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadPaddedUInt(texel, 0, 10), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            WritePaddedUInt(value.Red, texel, 0, 10);
    }

    private readonly struct Rg10X6UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => 2 * sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                ReadPaddedUInt(texel, 0, 10),
                ReadPaddedUInt(texel, 2, 10),
                0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WritePaddedUInt(value.Red, texel, 0, 10);
            WritePaddedUInt(value.Green, texel, 2, 10);
        }
    }

    private readonly struct Rgba10X6UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => 4 * sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                ReadPaddedUInt(texel, 0, 10),
                ReadPaddedUInt(texel, 2, 10),
                ReadPaddedUInt(texel, 4, 10),
                ReadPaddedUInt(texel, 6, 10));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WritePaddedUInt(value.Red, texel, 0, 10);
            WritePaddedUInt(value.Green, texel, 2, 10);
            WritePaddedUInt(value.Blue, texel, 4, 10);
            WritePaddedUInt(value.Alpha, texel, 6, 10);
        }
    }

    private readonly struct R12X4UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadPaddedUInt(texel, 0, 12), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            WritePaddedUInt(value.Red, texel, 0, 12);
    }

    private readonly struct Rg12X4UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => 2 * sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                ReadPaddedUInt(texel, 0, 12),
                ReadPaddedUInt(texel, 2, 12),
                0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WritePaddedUInt(value.Red, texel, 0, 12);
            WritePaddedUInt(value.Green, texel, 2, 12);
        }
    }

    private readonly struct Rgba12X4UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => 4 * sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                ReadPaddedUInt(texel, 0, 12),
                ReadPaddedUInt(texel, 2, 12),
                ReadPaddedUInt(texel, 4, 12),
                ReadPaddedUInt(texel, 6, 12));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WritePaddedUInt(value.Red, texel, 0, 12);
            WritePaddedUInt(value.Green, texel, 2, 12);
            WritePaddedUInt(value.Blue, texel, 4, 12);
            WritePaddedUInt(value.Alpha, texel, 6, 12);
        }
    }

    private readonly struct R14X2UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadPaddedUInt(texel, 0, 14), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            WritePaddedUInt(value.Red, texel, 0, 14);
    }

    private readonly struct Rg14X2UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => 2 * sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                ReadPaddedUInt(texel, 0, 14),
                ReadPaddedUInt(texel, 2, 14),
                0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WritePaddedUInt(value.Red, texel, 0, 14);
            WritePaddedUInt(value.Green, texel, 2, 14);
        }
    }

    private readonly struct Rgba14X2UIntTransfer : IPackedIntegerTransfer
    {
        public static int BytesPerTexel => 4 * sizeof(ushort);

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                ReadPaddedUInt(texel, 0, 14),
                ReadPaddedUInt(texel, 2, 14),
                ReadPaddedUInt(texel, 4, 14),
                ReadPaddedUInt(texel, 6, 14));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WritePaddedUInt(value.Red, texel, 0, 14);
            WritePaddedUInt(value.Green, texel, 2, 14);
            WritePaddedUInt(value.Blue, texel, 4, 14);
            WritePaddedUInt(value.Alpha, texel, 6, 14);
        }
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedIntegerTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba16UNorm(TTransfer.Decode(source.Slice(texelOffset, bytesPerTexel)));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeSigned<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedSignedIntegerTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba16SNorm(TTransfer.Decode(source.Slice(texelOffset, bytesPerTexel)));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedIntegerTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba16UNorm(sourceRow[x]), destination.Slice(texelOffset, bytesPerTexel));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeSigned<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedSignedIntegerTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba16SNorm(sourceRow[x]), destination.Slice(texelOffset, bytesPerTexel));
                texelOffset = checked(texelOffset + bytesPerTexel);
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

    private static ushort ReadPaddedUInt(ReadOnlySpan<byte> texel, int offset, int bits) =>
        (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(texel.Slice(offset, sizeof(ushort))) >> (16 - bits));

    private static void WritePaddedUInt(ushort value, Span<byte> texel, int offset, int bits)
    {
        var max = (1u << bits) - 1u;
        var packed = ClampUInt(value, max) << (16 - bits);
        BinaryPrimitives.WriteUInt16LittleEndian(texel.Slice(offset, sizeof(ushort)), checked((ushort)packed));
    }

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

        if (format == TextureFormats.R10X6UInt)
        {
            return PackedIntegerKind.R10X6UInt;
        }

        if (format == TextureFormats.R10X6G10X6UInt)
        {
            return PackedIntegerKind.Rg10X6UInt;
        }

        if (format == TextureFormats.R10X6G10X6B10X6A10X6UInt)
        {
            return PackedIntegerKind.Rgba10X6UInt;
        }

        if (format == TextureFormats.R12X4UInt)
        {
            return PackedIntegerKind.R12X4UInt;
        }

        if (format == TextureFormats.R12X4G12X4UInt)
        {
            return PackedIntegerKind.Rg12X4UInt;
        }

        if (format == TextureFormats.R12X4G12X4B12X4A12X4UInt)
        {
            return PackedIntegerKind.Rgba12X4UInt;
        }

        if (format == TextureFormats.R14X2UInt)
        {
            return PackedIntegerKind.R14X2UInt;
        }

        if (format == TextureFormats.R14X2G14X2UInt)
        {
            return PackedIntegerKind.Rg14X2UInt;
        }

        if (format == TextureFormats.R14X2G14X2B14X2A14X2UInt)
        {
            return PackedIntegerKind.Rgba14X2UInt;
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
        Bgr10A2RevSInt,
        R10X6UInt,
        Rg10X6UInt,
        Rgba10X6UInt,
        R12X4UInt,
        Rg12X4UInt,
        Rgba12X4UInt,
        R14X2UInt,
        Rg14X2UInt,
        Rgba14X2UInt
    }
}
