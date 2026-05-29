using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class Nv11TextureCoder(TextureFormat format) : IPitchTextureCoder
{
    public TextureFormat Format { get; } = IsSupported(format) ? format : throw CreateUnsupportedFormatException(format);

    public static bool IsSupported(TextureFormat format) => format == TextureFormats.Nv11UNorm;

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return checked(((width + 3) / 4) * 4);
    }

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);
        ValidateWidthAlignment(width);
        ValidateRowPitch(width, rowPitch);
        return checked(rowPitch * height * 2);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        var lumaByteCount = checked(rowPitch * destination.Height);
        var lumaPlane = source[..lumaByteCount];
        var chromaRowPitch = rowPitch / 2;
        var chromaPlane = source.Slice(lumaByteCount, checked(chromaRowPitch * destination.Height));

        var lumaRowOffset = 0;
        var chromaRowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var lumaRow = lumaPlane.Slice(lumaRowOffset, rowPitch);
            var chromaRow = chromaPlane.Slice(chromaRowOffset, chromaRowPitch);
            var destinationRow = destination.GetRowSpan(y);
            var chromaOffset = 0;
            for (var x = 0; x < destination.Width; x += 4)
            {
                var u = chromaRow[chromaOffset];
                var v = chromaRow[chromaOffset + 1];
                var groupEnd = x + 4;
                for (var pixelX = x; pixelX < groupEnd; pixelX++)
                {
                    destinationRow[pixelX] = TPixel.FromRgba32Float(YuvToRgba32Float(lumaRow[pixelX], u, v));
                }

                chromaOffset = checked(chromaOffset + 2);
            }

            lumaRowOffset = checked(lumaRowOffset + rowPitch);
            chromaRowOffset = checked(chromaRowOffset + chromaRowPitch);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        destination.Clear();

        var lumaByteCount = checked(rowPitch * source.Height);
        var lumaPlane = destination[..lumaByteCount];
        var chromaRowPitch = rowPitch / 2;
        var chromaPlane = destination.Slice(lumaByteCount, checked(chromaRowPitch * source.Height));

        var lumaRowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var lumaRow = lumaPlane.Slice(lumaRowOffset, rowPitch);
            for (var x = 0; x < source.Width; x++)
            {
                RgbaToYuv(TPixel.ToRgba32Float(sourceRow[x]), out var yValue, out _, out _);
                lumaRow[x] = UnitToByte(yValue);
            }

            lumaRowOffset = checked(lumaRowOffset + rowPitch);
        }

        var chromaGroupCount = checked((source.Width + 3) / 4);
        var chromaRowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var chromaRow = chromaPlane.Slice(chromaRowOffset, chromaRowPitch);
            var sourceX = 0;
            var chromaOffset = 0;
            for (var group = 0; group < chromaGroupCount; group++)
            {
                var sourceWidth = Math.Min(4, source.Width - sourceX);
                var uTotal = 0f;
                var vTotal = 0f;
                for (var x = 0; x < sourceWidth; x++)
                {
                    RgbaToYuv(TPixel.ToRgba32Float(sourceRow[sourceX + x]), out _, out var u, out var v);
                    uTotal += u;
                    vTotal += v;
                }

                chromaRow[chromaOffset] = ChromaToByte(uTotal / sourceWidth);
                chromaRow[chromaOffset + 1] = ChromaToByte(vTotal / sourceWidth);
                sourceX = checked(sourceX + 4);
                chromaOffset = checked(chromaOffset + 2);
            }

            chromaRowOffset = checked(chromaRowOffset + chromaRowPitch);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded NV11 texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded NV11 texture.", nameof(destination));
        }
    }

    private void ValidateRowPitch(int width, int rowPitch)
    {
        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed NV11 row byte count.");
        }

        if ((rowPitch & 1) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "NV11 row pitch must be even.");
        }
    }

    private static Rgba32Float YuvToRgba32Float(byte ySample, byte uSample, byte vSample)
    {
        var y = ySample / 255f;
        var u = (uSample - 128) / 255f;
        var v = (vSample - 128) / 255f;
        return new Rgba32Float(
            Clamp01(y + (1.402f * v)),
            Clamp01(y - (0.344136f * u) - (0.714136f * v)),
            Clamp01(y + (1.772f * u)));
    }

    private static void RgbaToYuv(Rgba32Float color, out float y, out float u, out float v)
    {
        var red = Clamp01(color.Red);
        var green = Clamp01(color.Green);
        var blue = Clamp01(color.Blue);
        y = (0.299f * red) + (0.587f * green) + (0.114f * blue);
        u = (blue - y) / 1.772f;
        v = (red - y) / 1.402f;
    }

    private static byte UnitToByte(float value) => ScaleToByte(Clamp01(value));

    private static byte ChromaToByte(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        var scaled = MathF.Round((value * 255f) + 128f);
        if (scaled < 0f)
        {
            return 0;
        }

        return scaled > byte.MaxValue ? byte.MaxValue : (byte)scaled;
    }

    private static byte ScaleToByte(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        return (byte)MathF.Round(value * byte.MaxValue);
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || value < 0f)
        {
            return 0f;
        }

        return value > 1f ? 1f : value;
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
    }

    private static void ValidateWidthAlignment(int width)
    {
        if ((width & 3) != 0)
        {
            throw new ArgumentException("NV11 textures require a width that is a multiple of 4.", nameof(width));
        }
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"NV11 texture coder does not support texture format '{format.Name}'.");
}
