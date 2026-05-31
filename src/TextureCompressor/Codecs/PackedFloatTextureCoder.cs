using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class PackedFloatTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const int BytesPerTexel = sizeof(uint);

    private readonly PackedFloatKind _kind = GetPackedFloatKind(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.R11G11B10Float
        || format == TextureFormats.Rgb9E5
        || format == TextureFormats.Rgb10FloatA2UNorm
        || format == TextureFormats.Rgb10FloatA2UNormRev
        || format == TextureFormats.Bgr10FloatA2UNormRev
        || format == TextureFormats.Rgb10FloatX2UNormRev
        || format == TextureFormats.Bgr10FloatX2UNormRev
        || format == TextureFormats.R32FloatAbs;

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
            case PackedFloatKind.R11G11B10Float:
                Decode<TPixel, R11G11B10FloatTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb9E5:
                Decode<TPixel, Rgb9E5Transfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb10FloatA2UNorm:
                Decode<TPixel, Rgb10FloatA2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb10FloatA2UNormRev:
                Decode<TPixel, Rgb10FloatA2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Bgr10FloatA2UNormRev:
                Decode<TPixel, Bgr10FloatA2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb10FloatX2UNormRev:
                Decode<TPixel, Rgb10FloatX2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Bgr10FloatX2UNormRev:
                Decode<TPixel, Bgr10FloatX2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.R32FloatAbs:
                Decode<TPixel, R32FloatAbsTransfer>(source, destination, rowPitch);
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
            case PackedFloatKind.R11G11B10Float:
                Encode<TPixel, R11G11B10FloatTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb9E5:
                Encode<TPixel, Rgb9E5Transfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb10FloatA2UNorm:
                Encode<TPixel, Rgb10FloatA2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb10FloatA2UNormRev:
                Encode<TPixel, Rgb10FloatA2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Bgr10FloatA2UNormRev:
                Encode<TPixel, Bgr10FloatA2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb10FloatX2UNormRev:
                Encode<TPixel, Rgb10FloatX2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Bgr10FloatX2UNormRev:
                Encode<TPixel, Bgr10FloatX2UNormRevTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.R32FloatAbs:
                Encode<TPixel, R32FloatAbsTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IPackedFloatTransfer
    {
        static abstract Rgba32Float Decode(ReadOnlySpan<byte> texel);

        static abstract void Encode(Rgba32Float value, Span<byte> texel);
    }

    private readonly struct R11G11B10FloatTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeValue(BinaryPrimitives.ReadUInt32LittleEndian(texel));

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(texel, EncodeValue(value));

        private static Rgba32Float DecodeValue(uint value) =>
            new(
                DecodeUnsignedFloat(value & 0x7ff, 6),
                DecodeUnsignedFloat((value >> 11) & 0x7ff, 6),
                DecodeUnsignedFloat((value >> 22) & 0x3ff, 5));

        private static uint EncodeValue(Rgba32Float value) =>
            EncodeUnsignedFloat(value.Red, 6)
            | (EncodeUnsignedFloat(value.Green, 6) << 11)
            | (EncodeUnsignedFloat(value.Blue, 5) << 22);
    }

    private readonly struct Rgb9E5Transfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeRgb9E5(BinaryPrimitives.ReadUInt32LittleEndian(texel));

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt32LittleEndian(texel, EncodeRgb9E5(value));
    }

    private readonly struct Rgb10FloatA2UNormTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba32Float(
                DecodeUnsignedFloat((packed >> 22) & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 12) & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 2) & 0x03ffu, 5),
                DecodeUNorm2(packed & 0x03u));
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            var packed = (EncodeUnsignedFloat(value.Red, 5) << 22)
                | (EncodeUnsignedFloat(value.Green, 5) << 12)
                | (EncodeUnsignedFloat(value.Blue, 5) << 2)
                | EncodeUNorm2(value.Alpha);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb10FloatA2UNormRevTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba32Float(
                DecodeUnsignedFloat(packed & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 10) & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 20) & 0x03ffu, 5),
                DecodeUNorm2(packed >> 30));
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            var packed = EncodeUnsignedFloat(value.Red, 5)
                | (EncodeUnsignedFloat(value.Green, 5) << 10)
                | (EncodeUnsignedFloat(value.Blue, 5) << 20)
                | (EncodeUNorm2(value.Alpha) << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr10FloatA2UNormRevTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba32Float(
                DecodeUnsignedFloat((packed >> 20) & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 10) & 0x03ffu, 5),
                DecodeUnsignedFloat(packed & 0x03ffu, 5),
                DecodeUNorm2(packed >> 30));
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            var packed = EncodeUnsignedFloat(value.Blue, 5)
                | (EncodeUnsignedFloat(value.Green, 5) << 10)
                | (EncodeUnsignedFloat(value.Red, 5) << 20)
                | (EncodeUNorm2(value.Alpha) << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb10FloatX2UNormRevTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba32Float(
                DecodeUnsignedFloat(packed & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 10) & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 20) & 0x03ffu, 5),
                1f);
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            var packed = EncodeUnsignedFloat(value.Red, 5)
                | (EncodeUnsignedFloat(value.Green, 5) << 10)
                | (EncodeUnsignedFloat(value.Blue, 5) << 20);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr10FloatX2UNormRevTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            return new Rgba32Float(
                DecodeUnsignedFloat((packed >> 20) & 0x03ffu, 5),
                DecodeUnsignedFloat((packed >> 10) & 0x03ffu, 5),
                DecodeUnsignedFloat(packed & 0x03ffu, 5),
                1f);
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            var packed = EncodeUnsignedFloat(value.Blue, 5)
                | (EncodeUnsignedFloat(value.Green, 5) << 10)
                | (EncodeUnsignedFloat(value.Red, 5) << 20);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct R32FloatAbsTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(texel) & 0x7fffffffu;
            return new Rgba32Float(BitConverter.UInt32BitsToSingle(bits), 0f, 0f);
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            var bits = BitConverter.SingleToUInt32Bits(value.Red) & 0x7fffffffu;
            BinaryPrimitives.WriteUInt32LittleEndian(texel, bits);
        }
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedFloatTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var encodedTexel = source.Slice(texelOffset, BytesPerTexel);
                destinationRow[x] = TPixel.FromRgba32Float(TTransfer.Decode(encodedTexel));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedFloatTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                var encodedTexel = destination.Slice(texelOffset, BytesPerTexel);
                TTransfer.Encode(TPixel.ToRgba32Float(sourceRow[x]), encodedTexel);
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
            throw new ArgumentException("Source span is too small for the encoded packed float texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded packed float texture.", nameof(destination));
        }
    }

    private static float DecodeUnsignedFloat(uint value, int mantissaBits)
    {
        var mantissaMask = (1u << mantissaBits) - 1u;
        var mantissa = value & mantissaMask;
        var exponent = value >> mantissaBits;

        if (exponent == 0)
        {
            return mantissa * Pow2(-14 - mantissaBits);
        }

        if (exponent == 0x1f)
        {
            return mantissa == 0 ? float.PositiveInfinity : float.NaN;
        }

        var singleExponent = exponent + 112u;
        var singleMantissa = mantissa << (23 - mantissaBits);
        return BitConverter.UInt32BitsToSingle((singleExponent << 23) | singleMantissa);
    }

    private static uint EncodeUnsignedFloat(float value, int mantissaBits)
    {
        if (!(value > 0f))
        {
            return 0;
        }

        var mantissaMax = (1u << mantissaBits) - 1u;
        var maxFinite = (2f - (1f / (1u << mantissaBits))) * Pow2(15);
        if (value >= maxFinite)
        {
            return (30u << mantissaBits) | mantissaMax;
        }

        const int bias = 15;
        var minNormal = Pow2(1 - bias);
        if (value < minNormal)
        {
            var subnormalMantissa = (uint)MathF.Round(value * Pow2(bias + mantissaBits - 1));
            return Math.Min(subnormalMantissa, mantissaMax);
        }

        var bits = BitConverter.SingleToUInt32Bits(value);
        var exponent = GetNormalExponent(bits);
        var encodedExponent = exponent + bias;
        var mantissa = RoundMantissa(bits & 0x7fffff, mantissaBits);
        if (mantissa == 1u << mantissaBits)
        {
            mantissa = 0;
            encodedExponent++;
        }

        if (encodedExponent >= 31)
        {
            return (30u << mantissaBits) | mantissaMax;
        }

        return ((uint)encodedExponent << mantissaBits) | mantissa;
    }

    private static Rgba32Float DecodeRgb9E5(uint value)
    {
        var exponent = value >> 27;
        var scale = Pow2((int)exponent - 24);
        return new Rgba32Float(
            (value & 0x1ff) * scale,
            ((value >> 9) & 0x1ff) * scale,
            ((value >> 18) & 0x1ff) * scale);
    }

    private static uint EncodeRgb9E5(Rgba32Float source)
    {
        var red = ClampRgb9E5(source.Red);
        var green = ClampRgb9E5(source.Green);
        var blue = ClampRgb9E5(source.Blue);
        var maxComponent = MathF.Max(red, MathF.Max(green, blue));

        if (maxComponent < Pow2(-24))
        {
            return 0;
        }

        var exponent = Math.Max(-16, GetNormalExponent(maxComponent)) + 1 + 15;
        var inverseScale = Pow2(24 - exponent);
        var maxMantissa = (int)MathF.Round(maxComponent * inverseScale);
        if (maxMantissa == 512)
        {
            exponent++;
        }

        exponent = Math.Min(exponent, 31);
        inverseScale = Pow2(24 - exponent);
        var redMantissa = FloatToRgb9E5Mantissa(red, inverseScale);
        var greenMantissa = FloatToRgb9E5Mantissa(green, inverseScale);
        var blueMantissa = FloatToRgb9E5Mantissa(blue, inverseScale);
        return redMantissa | (greenMantissa << 9) | (blueMantissa << 18) | ((uint)exponent << 27);
    }

    private static uint FloatToRgb9E5Mantissa(float value, float inverseScale) =>
        Math.Min(511u, (uint)MathF.Round(value * inverseScale));

    private static float DecodeUNorm2(uint value) => (value & 0x03u) / 3f;

    private static uint EncodeUNorm2(float value)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0;
        }

        return value >= 1f ? 3u : (uint)MathF.Round(value * 3f);
    }

    private static uint RoundMantissa(uint mantissa, int mantissaBits)
    {
        var shift = 23 - mantissaBits;
        var half = 1u << (shift - 1);
        var truncated = mantissa >> shift;
        var remainder = mantissa & ((1u << shift) - 1u);
        if (remainder > half || (remainder == half && (truncated & 1u) != 0))
        {
            truncated++;
        }

        return truncated;
    }

    private static float ClampRgb9E5(float value)
    {
        const float maxValue = 65408f;

        if (!(value > 0f))
        {
            return 0f;
        }

        return value > maxValue ? maxValue : value;
    }

    private static float Pow2(int exponent) =>
        BitConverter.Int32BitsToSingle((exponent + 127) << 23);

    private static int GetNormalExponent(float value) =>
        GetNormalExponent(BitConverter.SingleToUInt32Bits(value));

    private static int GetNormalExponent(uint bits) =>
        (int)((bits >> 23) & 0xff) - 127;

    private static PackedFloatKind GetPackedFloatKind(TextureFormat format)
    {
        if (format == TextureFormats.R11G11B10Float)
        {
            return PackedFloatKind.R11G11B10Float;
        }

        if (format == TextureFormats.Rgb9E5)
        {
            return PackedFloatKind.Rgb9E5;
        }

        if (format == TextureFormats.Rgb10FloatA2UNorm)
        {
            return PackedFloatKind.Rgb10FloatA2UNorm;
        }

        if (format == TextureFormats.Rgb10FloatA2UNormRev)
        {
            return PackedFloatKind.Rgb10FloatA2UNormRev;
        }

        if (format == TextureFormats.Bgr10FloatA2UNormRev)
        {
            return PackedFloatKind.Bgr10FloatA2UNormRev;
        }

        if (format == TextureFormats.Rgb10FloatX2UNormRev)
        {
            return PackedFloatKind.Rgb10FloatX2UNormRev;
        }

        if (format == TextureFormats.Bgr10FloatX2UNormRev)
        {
            return PackedFloatKind.Bgr10FloatX2UNormRev;
        }

        if (format == TextureFormats.R32FloatAbs)
        {
            return PackedFloatKind.R32FloatAbs;
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed float texture codec does not support texture format '{format.Name}'.");

    private enum PackedFloatKind
    {
        R11G11B10Float,
        Rgb9E5,
        Rgb10FloatA2UNorm,
        Rgb10FloatA2UNormRev,
        Bgr10FloatA2UNormRev,
        Rgb10FloatX2UNormRev,
        Bgr10FloatX2UNormRev,
        R32FloatAbs
    }
}
