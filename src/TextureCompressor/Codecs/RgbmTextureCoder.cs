using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba32Float(DecodeTexel(source.Slice(texelOffset, BytesPerTexel)));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                EncodeTexel(TPixel.ToRgba32Float(sourceRow[x]), destination.Slice(texelOffset, BytesPerTexel));
                texelOffset = checked(texelOffset + BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private Rgba32Float DecodeTexel(ReadOnlySpan<byte> source)
    {
        var red = RgbaColorConversions.UNorm8ToFloat(source[0]);
        var green = RgbaColorConversions.UNorm8ToFloat(source[1]);
        var blue = RgbaColorConversions.UNorm8ToFloat(source[2]);

        return _kind switch
        {
            RgbmKind.Rgbm => DecodeRgbm(red, green, blue, source[3]),
            RgbmKind.Rgbd => DecodeRgbd(red, green, blue, source[3]),
            _ => throw CreateUnsupportedFormatException(Format)
        };
    }

    private Rgba32Float DecodeRgbm(float red, float green, float blue, byte alpha)
    {
        var multiplier = RgbaColorConversions.UNorm8ToFloat(alpha) * MaxRange;
        return new Rgba32Float(red * multiplier, green * multiplier, blue * multiplier);
    }

    private Rgba32Float DecodeRgbd(float red, float green, float blue, byte divisor)
    {
        if (divisor == 0)
        {
            return new Rgba32Float(0f, 0f, 0f);
        }

        var scale = MaxRange / divisor;
        return new Rgba32Float(red * scale, green * scale, blue * scale);
    }

    private void EncodeTexel(Rgba32Float source, Span<byte> destination)
    {
        var red = ClampToMaxRange(source.Red);
        var green = ClampToMaxRange(source.Green);
        var blue = ClampToMaxRange(source.Blue);
        var maxComponent = MathF.Max(red, MathF.Max(green, blue));

        switch (_kind)
        {
            case RgbmKind.Rgbm:
                EncodeRgbm(red, green, blue, maxComponent, destination);
                return;
            case RgbmKind.Rgbd:
                EncodeRgbd(red, green, blue, maxComponent, destination);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeRgbm(float red, float green, float blue, float maxComponent, Span<byte> destination)
    {
        if (maxComponent <= 0f)
        {
            destination.Clear();
            return;
        }

        var multiplier = MathF.Ceiling((maxComponent / MaxRange) * byte.MaxValue) / byte.MaxValue;
        multiplier = Math.Clamp(multiplier, 1f / byte.MaxValue, 1f);
        destination[0] = RgbaColorConversions.FloatToUNorm8(red / (multiplier * MaxRange));
        destination[1] = RgbaColorConversions.FloatToUNorm8(green / (multiplier * MaxRange));
        destination[2] = RgbaColorConversions.FloatToUNorm8(blue / (multiplier * MaxRange));
        destination[3] = RgbaColorConversions.FloatToUNorm8(multiplier);
    }

    private void EncodeRgbd(float red, float green, float blue, float maxComponent, Span<byte> destination)
    {
        if (maxComponent <= 0f)
        {
            destination[0] = 0;
            destination[1] = 0;
            destination[2] = 0;
            destination[3] = byte.MaxValue;
            return;
        }

        var divisor = (byte)Math.Clamp(MathF.Floor(MaxRange / maxComponent), 1f, byte.MaxValue);
        destination[0] = RgbaColorConversions.FloatToUNorm8((red * divisor) / MaxRange);
        destination[1] = RgbaColorConversions.FloatToUNorm8((green * divisor) / MaxRange);
        destination[2] = RgbaColorConversions.FloatToUNorm8((blue * divisor) / MaxRange);
        destination[3] = divisor;
    }

    private float ClampToMaxRange(float value)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0f;
        }

        return MathF.Min(value, MaxRange);
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
