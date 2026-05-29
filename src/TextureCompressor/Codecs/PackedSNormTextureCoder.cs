using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class PackedSNormTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const int AlphaBits = 2;
    private const int ColorBits = 10;
    private const uint AlphaMask = 0x3;
    private const uint ColorMask = 0x3ff;

    private readonly PackedSNormKind _kind = GetPackedSNormKind(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) => TryGetPackedSNormKind(format, out _);

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
            case PackedSNormKind.Rg5SNormB6UNormRev:
                Decode<TPixel, Rg5SNormB6UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg5SNormB6UNormRevBigEndian:
                Decode<TPixel, Rg5SNormB6UNormRevTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgba4RevSNorm:
                Decode<TPixel, Rgba4RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgba4RevSNormBigEndian:
                Decode<TPixel, Rgba4RevSNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg8SNormB8UNormX8Rev:
                Decode<TPixel, Rg8SNormB8UNormX8RevTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg8SNormB8UNormX8RevBigEndian:
                Decode<TPixel, Rg8SNormB8UNormX8RevTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgb10SNormA2UNormRev:
                Decode<TPixel, Rgb10SNormA2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgb10SNormA2UNormRevBigEndian:
                Decode<TPixel, Rgb10SNormA2UNormRevTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgb10A2RevSNorm:
                Decode<TPixel, Rgb10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Bgr10A2RevSNorm:
                Decode<TPixel, Bgr10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg11B10RevSNorm:
                Decode<TPixel, Rg11B10RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg11B10RevSNormBigEndian:
                Decode<TPixel, Rg11B10RevSNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.R10Gb11RevSNorm:
                Decode<TPixel, R10Gb11RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.R10Gb11RevSNormBigEndian:
                Decode<TPixel, R10Gb11RevSNormTransferBigEndian>(source, destination, rowPitch);
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
            case PackedSNormKind.Rg5SNormB6UNormRev:
                Encode<TPixel, Rg5SNormB6UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg5SNormB6UNormRevBigEndian:
                Encode<TPixel, Rg5SNormB6UNormRevTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgba4RevSNorm:
                Encode<TPixel, Rgba4RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgba4RevSNormBigEndian:
                Encode<TPixel, Rgba4RevSNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg8SNormB8UNormX8Rev:
                Encode<TPixel, Rg8SNormB8UNormX8RevTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg8SNormB8UNormX8RevBigEndian:
                Encode<TPixel, Rg8SNormB8UNormX8RevTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgb10SNormA2UNormRev:
                Encode<TPixel, Rgb10SNormA2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgb10SNormA2UNormRevBigEndian:
                Encode<TPixel, Rgb10SNormA2UNormRevTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rgb10A2RevSNorm:
                Encode<TPixel, Rgb10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Bgr10A2RevSNorm:
                Encode<TPixel, Bgr10A2RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg11B10RevSNorm:
                Encode<TPixel, Rg11B10RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.Rg11B10RevSNormBigEndian:
                Encode<TPixel, Rg11B10RevSNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedSNormKind.R10Gb11RevSNorm:
                Encode<TPixel, R10Gb11RevSNormTransfer>(source, destination, rowPitch);
                return;
            case PackedSNormKind.R10Gb11RevSNormBigEndian:
                Encode<TPixel, R10Gb11RevSNormTransferBigEndian>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IPackedSNormTransfer
    {
        static abstract int BytesPerTexel { get; }

        static abstract Rgba16SNorm Decode(ReadOnlySpan<byte> texel);

        static abstract void Encode(Rgba16SNorm value, Span<byte> texel);
    }

    private readonly struct Rg5SNormB6UNormRevTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => 2;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm((uint)packed & 0x1fu, 5),
                DecodeSNorm(((uint)packed >> 5) & 0x1fu, 5),
                DecodeUNorm(((uint)packed >> 10) & 0x3fu, 6));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = (ushort)(EncodeSNorm(value.Red, 5)
                | (EncodeSNorm(value.Green, 5) << 5)
                | (EncodeUNorm(value.Blue, 6) << 10));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rg5SNormB6UNormRevTransferBigEndian : IPackedSNormTransfer
    {
        public static int BytesPerTexel => Rg5SNormB6UNormRevTransfer.BytesPerTexel;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg5SNormB6UNormRevTransfer>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg5SNormB6UNormRevTransfer>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rgba4RevSNormTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => 2;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm((uint)packed & 0x0fu, 4),
                DecodeSNorm(((uint)packed >> 4) & 0x0fu, 4),
                DecodeSNorm(((uint)packed >> 8) & 0x0fu, 4),
                DecodeSNorm(((uint)packed >> 12) & 0x0fu, 4));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = (ushort)(EncodeSNorm(value.Red, 4)
                | (EncodeSNorm(value.Green, 4) << 4)
                | (EncodeSNorm(value.Blue, 4) << 8)
                | (EncodeSNorm(value.Alpha, 4) << 12));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgba4RevSNormTransferBigEndian : IPackedSNormTransfer
    {
        public static int BytesPerTexel => Rgba4RevSNormTransfer.BytesPerTexel;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba4RevSNormTransfer>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba4RevSNormTransfer>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rg8SNormB8UNormX8RevTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(
                DecodeSNorm(texel[0], 8),
                DecodeSNorm(texel[1], 8),
                DecodeUNorm(texel[2], 8));

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            texel[0] = (byte)EncodeSNorm(value.Red, 8);
            texel[1] = (byte)EncodeSNorm(value.Green, 8);
            texel[2] = (byte)EncodeUNorm(value.Blue, 8);
            texel[3] = 0;
        }
    }

    private readonly struct Rg8SNormB8UNormX8RevTransferBigEndian : IPackedSNormTransfer
    {
        public static int BytesPerTexel => Rg8SNormB8UNormX8RevTransfer.BytesPerTexel;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg8SNormB8UNormX8RevTransfer>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg8SNormB8UNormX8RevTransfer>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgb10SNormA2UNormRevTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm(packed & 0x03ffu, 10),
                DecodeSNorm((packed >> 10) & 0x03ffu, 10),
                DecodeSNorm((packed >> 20) & 0x03ffu, 10),
                DecodeUNorm(packed >> 30, 2));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = EncodeSNorm(value.Red, 10)
                | (EncodeSNorm(value.Green, 10) << 10)
                | (EncodeSNorm(value.Blue, 10) << 20)
                | (EncodeUNorm(value.Alpha, 2) << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb10SNormA2UNormRevTransferBigEndian : IPackedSNormTransfer
    {
        public static int BytesPerTexel => Rgb10SNormA2UNormRevTransfer.BytesPerTexel;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgb10SNormA2UNormRevTransfer>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgb10SNormA2UNormRevTransfer>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgb10A2RevSNormTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm(packed & ColorMask, ColorBits),
                DecodeSNorm((packed >> 10) & ColorMask, ColorBits),
                DecodeSNorm((packed >> 20) & ColorMask, ColorBits),
                DecodeSNorm(packed >> 30, AlphaBits));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = EncodeSNorm(value.Red, ColorBits)
                | (EncodeSNorm(value.Green, ColorBits) << 10)
                | (EncodeSNorm(value.Blue, ColorBits) << 20)
                | (EncodeSNorm(value.Alpha, AlphaBits) << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr10A2RevSNormTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm((packed >> 20) & ColorMask, ColorBits),
                DecodeSNorm((packed >> 10) & ColorMask, ColorBits),
                DecodeSNorm(packed & ColorMask, ColorBits),
                DecodeSNorm(packed >> 30, AlphaBits));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = EncodeSNorm(value.Blue, ColorBits)
                | (EncodeSNorm(value.Green, ColorBits) << 10)
                | (EncodeSNorm(value.Red, ColorBits) << 20)
                | (EncodeSNorm(value.Alpha, AlphaBits) << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rg11B10RevSNormTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm(packed & 0x07ffu, 11),
                DecodeSNorm((packed >> 11) & 0x07ffu, 11),
                DecodeSNorm(packed >> 22, 10));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = EncodeSNorm(value.Red, 11)
                | (EncodeSNorm(value.Green, 11) << 11)
                | (EncodeSNorm(value.Blue, 10) << 22);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rg11B10RevSNormTransferBigEndian : IPackedSNormTransfer
    {
        public static int BytesPerTexel => Rg11B10RevSNormTransfer.BytesPerTexel;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg11B10RevSNormTransfer>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg11B10RevSNormTransfer>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct R10Gb11RevSNormTransfer : IPackedSNormTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba16SNorm(
                DecodeSNorm(packed & 0x03ffu, 10),
                DecodeSNorm((packed >> 10) & 0x07ffu, 11),
                DecodeSNorm(packed >> 21, 11));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            var packed = EncodeSNorm(value.Red, 10)
                | (EncodeSNorm(value.Green, 11) << 10)
                | (EncodeSNorm(value.Blue, 11) << 21);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct R10Gb11RevSNormTransferBigEndian : IPackedSNormTransfer
    {
        public static int BytesPerTexel => R10Gb11RevSNormTransfer.BytesPerTexel;

        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<R10Gb11RevSNormTransfer>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<R10Gb11RevSNormTransfer>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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
                destinationRow[x] = TPixel.FromRgba16SNorm(TTransfer.Decode(source.Slice(texelOffset, TTransfer.BytesPerTexel)));
                texelOffset = checked(texelOffset + TTransfer.BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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
                TTransfer.Encode(TPixel.ToRgba16SNorm(sourceRow[x]), destination.Slice(texelOffset, TTransfer.BytesPerTexel));
                texelOffset = checked(texelOffset + TTransfer.BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static Rgba16SNorm DecodeBigEndianTexel<TTransfer>(
        ReadOnlySpan<byte> source,
        BigEndianByteSwapMode endianMode)
        where TTransfer : IPackedSNormTransfer
    {
        Span<byte> littleEndianTexel = stackalloc byte[TTransfer.BytesPerTexel];
        BigEndianByteSwap.CopyToLittleEndian(source, littleEndianTexel, endianMode);
        return TTransfer.Decode(littleEndianTexel);
    }

    private static void EncodeBigEndianTexel<TTransfer>(
        Rgba16SNorm value,
        Span<byte> destination,
        BigEndianByteSwapMode endianMode)
        where TTransfer : IPackedSNormTransfer
    {
        Span<byte> littleEndianTexel = stackalloc byte[TTransfer.BytesPerTexel];
        TTransfer.Encode(value, littleEndianTexel);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianTexel, destination, endianMode);
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

    private static short DecodeUNorm(uint value, int bits)
    {
        var sourceMax = GetMaxUInt(bits);
        return (short)((value * (ulong)short.MaxValue + sourceMax / 2) / sourceMax);
    }

    private static uint EncodeSNorm(short value, int bits)
    {
        var targetMax = (1 << (bits - 1)) - 1;
        var signed = ScaleSigned(value, short.MaxValue, targetMax);
        return (uint)signed & ((1u << bits) - 1u);
    }

    private static uint EncodeUNorm(short value, int bits)
    {
        if (value <= 0)
        {
            return 0;
        }

        var targetMax = GetMaxUInt(bits);
        return (uint)(((ulong)value * targetMax + short.MaxValue / 2u) / (uint)short.MaxValue);
    }

    private static int SignExtend(uint value, int bits)
    {
        var signBit = 1 << (bits - 1);
        var mask = (1 << bits) - 1;
        return ((int)value & mask ^ signBit) - signBit;
    }

    private static uint GetMaxUInt(int bits) => (1u << bits) - 1u;

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
        if (TryGetPackedSNormKind(format, out var kind))
        {
            return kind;
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static bool TryGetPackedSNormKind(TextureFormat format, out PackedSNormKind kind)
    {
        if (format == TextureFormats.Rg5SNormB6UNormRev)
        {
            kind = PackedSNormKind.Rg5SNormB6UNormRev;
            return true;
        }

        if (format == TextureFormats.Rg5SNormB6UNormRevBigEndian)
        {
            kind = PackedSNormKind.Rg5SNormB6UNormRevBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba4RevSNorm)
        {
            kind = PackedSNormKind.Rgba4RevSNorm;
            return true;
        }

        if (format == TextureFormats.Rgba4RevSNormBigEndian)
        {
            kind = PackedSNormKind.Rgba4RevSNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg8SNormB8UNormX8Rev)
        {
            kind = PackedSNormKind.Rg8SNormB8UNormX8Rev;
            return true;
        }

        if (format == TextureFormats.Rg8SNormB8UNormX8RevBigEndian)
        {
            kind = PackedSNormKind.Rg8SNormB8UNormX8RevBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgb10SNormA2UNormRev)
        {
            kind = PackedSNormKind.Rgb10SNormA2UNormRev;
            return true;
        }

        if (format == TextureFormats.Rgb10SNormA2UNormRevBigEndian)
        {
            kind = PackedSNormKind.Rgb10SNormA2UNormRevBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgb10A2RevSNorm)
        {
            kind = PackedSNormKind.Rgb10A2RevSNorm;
            return true;
        }

        if (format == TextureFormats.Bgr10A2RevSNorm)
        {
            kind = PackedSNormKind.Bgr10A2RevSNorm;
            return true;
        }

        if (format == TextureFormats.Rg11B10RevSNorm)
        {
            kind = PackedSNormKind.Rg11B10RevSNorm;
            return true;
        }

        if (format == TextureFormats.Rg11B10RevSNormBigEndian)
        {
            kind = PackedSNormKind.Rg11B10RevSNormBigEndian;
            return true;
        }

        if (format == TextureFormats.R10Gb11RevSNorm)
        {
            kind = PackedSNormKind.R10Gb11RevSNorm;
            return true;
        }

        if (format == TextureFormats.R10Gb11RevSNormBigEndian)
        {
            kind = PackedSNormKind.R10Gb11RevSNormBigEndian;
            return true;
        }

        kind = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed SNorm texture codec does not support texture format '{format.Name}'.");

    private enum PackedSNormKind
    {
        Rg5SNormB6UNormRev,
        Rg5SNormB6UNormRevBigEndian,
        Rgba4RevSNorm,
        Rgba4RevSNormBigEndian,
        Rg8SNormB8UNormX8Rev,
        Rg8SNormB8UNormX8RevBigEndian,
        Rgb10SNormA2UNormRev,
        Rgb10SNormA2UNormRevBigEndian,
        Rgb10A2RevSNorm,
        Bgr10A2RevSNorm,
        Rg11B10RevSNorm,
        Rg11B10RevSNormBigEndian,
        R10Gb11RevSNorm,
        R10Gb11RevSNormBigEndian
    }
}
