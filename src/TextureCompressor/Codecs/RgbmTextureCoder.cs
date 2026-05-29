using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class RgbmTextureCoder : IPitchTextureCoder
{
    public const float DefaultMaxRange = 8f;

    private const int BytesPerTexel = 4;

    private readonly RgbmKind _kind;

    public RgbmTextureCoder(TextureFormat format, float maxRange = DefaultMaxRange)
    {
        ValidateMaxRange(maxRange);

        Format = format;
        MaxRange = maxRange;
        _kind = GetRgbmKind(format);
    }

    public TextureFormat Format { get; }

    public float MaxRange { get; }

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Rgbm
        || format == TextureFormats.Rgbd;

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
            case RgbmKind.Rgbm:
                Decode<TPixel, RgbmTransfer>(source, destination, rowPitch);
                return;
            case RgbmKind.Rgbd:
                Decode<TPixel, RgbdTransfer>(source, destination, rowPitch);
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
            case RgbmKind.Rgbm:
                Encode<TPixel, RgbmTransfer>(source, destination, rowPitch);
                return;
            case RgbmKind.Rgbd:
                Encode<TPixel, RgbdTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IRgbmTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba32Float(TTransfer.Decode(source.Slice(texelOffset, BytesPerTexel), MaxRange));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IRgbmTransfer
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba32Float(sourceRow[x]), MaxRange, destination.Slice(texelOffset, BytesPerTexel));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IRgbmTransfer
    {
        static abstract Rgba32Float Decode(ReadOnlySpan<byte> source, float maxRange);

        static abstract void Encode(Rgba32Float source, float maxRange, Span<byte> destination);
    }

    private readonly struct RgbmTransfer : IRgbmTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> source, float maxRange)
        {
            var multiplier = RgbaColorConversions.UNorm8ToFloat(source[3]) * maxRange;
            return new Rgba32Float(
                RgbaColorConversions.UNorm8ToFloat(source[0]) * multiplier,
                RgbaColorConversions.UNorm8ToFloat(source[1]) * multiplier,
                RgbaColorConversions.UNorm8ToFloat(source[2]) * multiplier);
        }

        public static void Encode(Rgba32Float source, float maxRange, Span<byte> destination)
        {
            GetClampedComponents(source, maxRange, out var red, out var green, out var blue, out var maxComponent);
            if (maxComponent <= 0f)
            {
                destination.Clear();
                return;
            }

            var multiplier = MathF.Ceiling((maxComponent / maxRange) * byte.MaxValue) / byte.MaxValue;
            multiplier = Math.Clamp(multiplier, 1f / byte.MaxValue, 1f);
            destination[0] = RgbaColorConversions.FloatToUNorm8(red / (multiplier * maxRange));
            destination[1] = RgbaColorConversions.FloatToUNorm8(green / (multiplier * maxRange));
            destination[2] = RgbaColorConversions.FloatToUNorm8(blue / (multiplier * maxRange));
            destination[3] = RgbaColorConversions.FloatToUNorm8(multiplier);
        }
    }

    private readonly struct RgbdTransfer : IRgbmTransfer
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> source, float maxRange)
        {
            var divisor = source[3];
            if (divisor == 0)
            {
                return new Rgba32Float(0f, 0f, 0f);
            }

            var scale = maxRange / divisor;
            return new Rgba32Float(
                RgbaColorConversions.UNorm8ToFloat(source[0]) * scale,
                RgbaColorConversions.UNorm8ToFloat(source[1]) * scale,
                RgbaColorConversions.UNorm8ToFloat(source[2]) * scale);
        }

        public static void Encode(Rgba32Float source, float maxRange, Span<byte> destination)
        {
            GetClampedComponents(source, maxRange, out var red, out var green, out var blue, out var maxComponent);
            if (maxComponent <= 0f)
            {
                destination[0] = 0;
                destination[1] = 0;
                destination[2] = 0;
                destination[3] = byte.MaxValue;
                return;
            }

            var divisor = (byte)Math.Clamp(MathF.Floor(maxRange / maxComponent), 1f, byte.MaxValue);
            destination[0] = RgbaColorConversions.FloatToUNorm8((red * divisor) / maxRange);
            destination[1] = RgbaColorConversions.FloatToUNorm8((green * divisor) / maxRange);
            destination[2] = RgbaColorConversions.FloatToUNorm8((blue * divisor) / maxRange);
            destination[3] = divisor;
        }
    }

    private static void GetClampedComponents(
        Rgba32Float source,
        float maxRange,
        out float red,
        out float green,
        out float blue,
        out float maxComponent)
    {
        red = ClampToMaxRange(source.Red, maxRange);
        green = ClampToMaxRange(source.Green, maxRange);
        blue = ClampToMaxRange(source.Blue, maxRange);
        maxComponent = MathF.Max(red, MathF.Max(green, blue));
    }

    private static float ClampToMaxRange(float value, float maxRange)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0f;
        }

        return MathF.Min(value, maxRange);
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded RGBM/RGBD texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded RGBM/RGBD texture.", nameof(destination));
        }
    }

    private static void ValidateMaxRange(float maxRange)
    {
        if (float.IsNaN(maxRange) || float.IsInfinity(maxRange) || maxRange < 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRange), "RGBM/RGBD max range must be a finite value greater than or equal to 1.");
        }
    }

    private static RgbmKind GetRgbmKind(TextureFormat format)
    {
        if (format == TextureFormats.Rgbm)
        {
            return RgbmKind.Rgbm;
        }

        if (format == TextureFormats.Rgbd)
        {
            return RgbmKind.Rgbd;
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"RGBM/RGBD texture coder does not support texture format '{format.Name}'.");

    private enum RgbmKind
    {
        Rgbm,
        Rgbd
    }
}
