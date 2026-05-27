using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedFloatTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private const int BytesPerTexel = sizeof(uint);

    private readonly PackedFloatKind _kind = GetPackedFloatKind(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.R11G11B10Float
        || format == TextureFormats.Rgb9E5;

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
            case PackedFloatKind.R11G11B10Float:
                Decode<TPixel, R11G11B10FloatTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb9E5:
                Decode<TPixel, Rgb9E5Transfer>(source, destination, rowPitch);
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
            case PackedFloatKind.R11G11B10Float:
                Encode<TPixel, R11G11B10FloatTransfer>(source, destination, rowPitch);
                return;
            case PackedFloatKind.Rgb9E5:
                Encode<TPixel, Rgb9E5Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IPackedFloatTransfer
    {
        static abstract Rgba32Float Decode(uint value);

        static abstract uint Encode(Rgba32Float value);
    }

    private readonly struct R11G11B10FloatTransfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(uint value) =>
            new(
                DecodeUnsignedFloat(value & 0x7ff, 6),
                DecodeUnsignedFloat((value >> 11) & 0x7ff, 6),
                DecodeUnsignedFloat((value >> 22) & 0x3ff, 5));

        public static uint Encode(Rgba32Float value) =>
            EncodeUnsignedFloat(value.Red, 6)
            | (EncodeUnsignedFloat(value.Green, 6) << 11)
            | (EncodeUnsignedFloat(value.Blue, 5) << 22);
    }

    private readonly struct Rgb9E5Transfer : IPackedFloatTransfer
    {
        public static Rgba32Float Decode(uint value) => DecodeRgb9E5(value);

        public static uint Encode(Rgba32Float value) => EncodeRgb9E5(value);
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
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
                var value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(texelOffset, BytesPerTexel));
                destinationRow[x] = TPixel.FromRgba32Float(TTransfer.Decode(value));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
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
                var value = TTransfer.Encode(TPixel.ToRgba32Float(sourceRow[x]));
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

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed float texture codec does not support texture format '{format.Name}'.");

    private enum PackedFloatKind
    {
        R11G11B10Float,
        Rgb9E5
    }
}
