using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class XrTextureCoder : IPitchTextureCoder
{
    private const float Xr10Bias = 384f;
    private const float Xr10Scale = 510f;
    private const uint Xr10Mask = 0x3ffu;

    private readonly XrTransfer _transfer;

    public XrTextureCoder(TextureFormat format)
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
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed XR row byte count.");
        }

        return checked(rowPitch * height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        DecodeByTransfer(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeByTransfer(source, destination, rowPitch);
    }

    private void DecodeByTransfer<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case XrTransfer.Bgr10XR:
                Decode<TPixel, Bgr10XRTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Bgr10XRSrgb:
                Decode<TPixel, Bgr10XRSrgbTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Rgb10XRA2UNorm:
                Decode<TPixel, Rgb10XRA2UNormTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Bgra10XR:
                Decode<TPixel, Bgra10XRTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Bgra10XRSrgb:
                Decode<TPixel, Bgra10XRSrgbTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeByTransfer<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case XrTransfer.Bgr10XR:
                Encode<TPixel, Bgr10XRTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Bgr10XRSrgb:
                Encode<TPixel, Bgr10XRSrgbTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Rgb10XRA2UNorm:
                Encode<TPixel, Rgb10XRA2UNormTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Bgra10XR:
                Encode<TPixel, Bgra10XRTransfer>(source, destination, rowPitch);
                return;
            case XrTransfer.Bgra10XRSrgb:
                Encode<TPixel, Bgra10XRSrgbTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IXrTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba32Float(TTransfer.Decode(source.Slice(texelOffset, bytesPerTexel)));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IXrTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba32Float(sourceRow[x]), destination.Slice(texelOffset, bytesPerTexel));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IXrTransfer
    {
        static abstract int BytesPerTexel { get; }

        static abstract Rgba32Float Decode(ReadOnlySpan<byte> source);

        static abstract void Encode(Rgba32Float value, Span<byte> destination);
    }

    private readonly struct Bgr10XRTransfer : IXrTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            DecodeBgr10(source, isSrgb: false);

        public static void Encode(Rgba32Float value, Span<byte> destination) =>
            EncodeBgr10(value, destination, isSrgb: false);
    }

    private readonly struct Bgr10XRSrgbTransfer : IXrTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            DecodeBgr10(source, isSrgb: true);

        public static void Encode(Rgba32Float value, Span<byte> destination) =>
            EncodeBgr10(value, destination, isSrgb: true);
    }

    private readonly struct Rgb10XRA2UNormTransfer : IXrTransfer
    {
        public static int BytesPerTexel => sizeof(uint);

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            DecodeRgb10A2(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) =>
            EncodeRgb10A2(value, destination);
    }

    private readonly struct Bgra10XRTransfer : IXrTransfer
    {
        public static int BytesPerTexel => 4 * sizeof(ushort);

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            DecodeBgra10(source, isSrgb: false);

        public static void Encode(Rgba32Float value, Span<byte> destination) =>
            EncodeBgra10(value, destination, isSrgb: false);
    }

    private readonly struct Bgra10XRSrgbTransfer : IXrTransfer
    {
        public static int BytesPerTexel => 4 * sizeof(ushort);

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            DecodeBgra10(source, isSrgb: true);

        public static void Encode(Rgba32Float value, Span<byte> destination) =>
            EncodeBgra10(value, destination, isSrgb: true);
    }

    private static Rgba32Float DecodeBgr10(ReadOnlySpan<byte> source, bool isSrgb)
    {
        var packed = BinaryPrimitives.ReadUInt32LittleEndian(source);
        var blue = DecodeColor(packed & Xr10Mask, isSrgb);
        var green = DecodeColor((packed >> 10) & Xr10Mask, isSrgb);
        var red = DecodeColor((packed >> 20) & Xr10Mask, isSrgb);
        return new Rgba32Float(red, green, blue);
    }

    private static void EncodeBgr10(Rgba32Float value, Span<byte> destination, bool isSrgb)
    {
        var red = EncodeColor(value.Red, isSrgb);
        var green = EncodeColor(value.Green, isSrgb);
        var blue = EncodeColor(value.Blue, isSrgb);
        var packed = blue | (green << 10) | (red << 20);
        BinaryPrimitives.WriteUInt32LittleEndian(destination, packed);
    }

    private static Rgba32Float DecodeRgb10A2(ReadOnlySpan<byte> source)
    {
        var packed = BinaryPrimitives.ReadUInt32LittleEndian(source);
        var red = DecodeColor(packed & Xr10Mask, isSrgb: false);
        var green = DecodeColor((packed >> 10) & Xr10Mask, isSrgb: false);
        var blue = DecodeColor((packed >> 20) & Xr10Mask, isSrgb: false);
        var alpha = ((packed >> 30) & 0x3u) / 3f;
        return new Rgba32Float(red, green, blue, alpha);
    }

    private static void EncodeRgb10A2(Rgba32Float value, Span<byte> destination)
    {
        var red = EncodeColor(value.Red, isSrgb: false);
        var green = EncodeColor(value.Green, isSrgb: false);
        var blue = EncodeColor(value.Blue, isSrgb: false);
        var alpha = EncodeAlpha2(value.Alpha);
        var packed = red | (green << 10) | (blue << 20) | (alpha << 30);
        BinaryPrimitives.WriteUInt32LittleEndian(destination, packed);
    }

    private static Rgba32Float DecodeBgra10(ReadOnlySpan<byte> source, bool isSrgb)
    {
        var blue = DecodeColor((uint)BinaryPrimitives.ReadUInt16LittleEndian(source) >> 6, isSrgb);
        var green = DecodeColor((uint)BinaryPrimitives.ReadUInt16LittleEndian(source[2..]) >> 6, isSrgb);
        var red = DecodeColor((uint)BinaryPrimitives.ReadUInt16LittleEndian(source[4..]) >> 6, isSrgb);
        var alpha = Math.Clamp(DecodeXr10((uint)BinaryPrimitives.ReadUInt16LittleEndian(source[6..]) >> 6), 0f, 1f);
        return new Rgba32Float(red, green, blue, alpha);
    }

    private static void EncodeBgra10(Rgba32Float value, Span<byte> destination, bool isSrgb)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)(EncodeColor(value.Blue, isSrgb) << 6));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], (ushort)(EncodeColor(value.Green, isSrgb) << 6));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], (ushort)(EncodeColor(value.Red, isSrgb) << 6));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], (ushort)(EncodeXr10(Math.Clamp(value.Alpha, 0f, 1f)) << 6));
    }

    private static float DecodeColor(uint value, bool isSrgb)
    {
        var decoded = DecodeXr10(value);
        return isSrgb ? DecodeSrgb(decoded) : decoded;
    }

    private static uint EncodeColor(float value, bool isSrgb) =>
        EncodeXr10(isSrgb ? EncodeSrgb(value) : value);

    private static float DecodeXr10(uint value) => (value - Xr10Bias) / Xr10Scale;

    private static uint EncodeXr10(float value)
    {
        if (float.IsNaN(value))
        {
            value = 0f;
        }

        var encoded = MathF.Round((value * Xr10Scale) + Xr10Bias);
        if (encoded <= 0f)
        {
            return 0;
        }

        if (encoded >= Xr10Mask)
        {
            return Xr10Mask;
        }

        return (uint)encoded;
    }

    private static uint EncodeAlpha2(float value)
    {
        if (float.IsNaN(value))
        {
            value = 0f;
        }

        var encoded = MathF.Round(Math.Clamp(value, 0f, 1f) * 3f);
        return (uint)encoded;
    }

    private static float DecodeSrgb(float value) =>
        value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    private static float EncodeSrgb(float value) =>
        value <= 0.0031308f
            ? value * 12.92f
            : (1.055f * MathF.Pow(value, 1f / 2.4f)) - 0.055f;

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded XR texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded XR texture.", nameof(destination));
        }
    }

    private static bool TryGetTransfer(TextureFormat format, out XrTransfer transfer)
    {
        if (format == TextureFormats.Bgr10XR)
        {
            transfer = XrTransfer.Bgr10XR;
            return true;
        }

        if (format == TextureFormats.Bgr10XRSrgb)
        {
            transfer = XrTransfer.Bgr10XRSrgb;
            return true;
        }

        if (format == TextureFormats.Rgb10XRA2UNorm)
        {
            transfer = XrTransfer.Rgb10XRA2UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra10XR)
        {
            transfer = XrTransfer.Bgra10XR;
            return true;
        }

        if (format == TextureFormats.Bgra10XRSrgb)
        {
            transfer = XrTransfer.Bgra10XRSrgb;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"XR texture coder does not support texture format '{format.Name}'.");

    private enum XrTransfer
    {
        Bgr10XR,
        Bgr10XRSrgb,
        Rgb10XRA2UNorm,
        Bgra10XR,
        Bgra10XRSrgb
    }
}
