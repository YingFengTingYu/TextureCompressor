using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedYuva444TextureCoder : IPitchTextureCoder
{
    private readonly PackedYuva444Layout _layout;
    private readonly bool _isUyv10A2;

    public PackedYuva444TextureCoder(TextureFormat format)
    {
        if (format == TextureFormats.Uyv10A2_444UNorm)
        {
            _isUyv10A2 = true;
        }
        else if (!TryGetLayout(format, out _layout))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Uyv10A2_444UNorm || TryGetLayout(format, out _);

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return checked(width * (_isUyv10A2 ? 4 : 8));
    }

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);
        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed YUVA row byte count.");
        }

        return checked(rowPitch * height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        if (_isUyv10A2)
        {
            DecodeUyv10A2(source, destination, rowPitch);
            return;
        }

        DecodeWide(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        if (_isUyv10A2)
        {
            EncodeUyv10A2(source, destination, rowPitch);
            return;
        }

        EncodeWide(source, destination, rowPitch);
    }

    private void DecodeWide<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var maxSample = GetMaxYuvSample(_layout.BitsPerSample);
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var pixelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var first = ReadYuvSample(source[pixelOffset..], _layout.BitsPerSample, _layout.MsbAligned);
                var ySample = ReadYuvSample(source[(pixelOffset + 2)..], _layout.BitsPerSample, _layout.MsbAligned);
                var second = ReadYuvSample(source[(pixelOffset + 4)..], _layout.BitsPerSample, _layout.MsbAligned);
                var alpha = ReadYuvSample(source[(pixelOffset + 6)..], _layout.BitsPerSample, _layout.MsbAligned);
                var u = _layout.VFirst ? second : first;
                var v = _layout.VFirst ? first : second;
                destinationRow[x] = TPixel.FromRgba32Float(YuvToRgba32Float(ySample, u, v, _layout.BitsPerSample, alpha / (float)maxSample));
                pixelOffset = checked(pixelOffset + 8);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeWide<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var pixelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = TPixel.ToRgba32Float(sourceRow[x]);
                RgbaToYuv(pixel, out var yValue, out var u, out var v);
                var first = _layout.VFirst
                    ? ChromaToYuvSample(v, _layout.BitsPerSample)
                    : ChromaToYuvSample(u, _layout.BitsPerSample);
                var second = _layout.VFirst
                    ? ChromaToYuvSample(u, _layout.BitsPerSample)
                    : ChromaToYuvSample(v, _layout.BitsPerSample);
                var pixelBytes = destination.Slice(pixelOffset, 8);
                WriteYuvSample(pixelBytes, first, _layout.BitsPerSample, _layout.MsbAligned);
                WriteYuvSample(pixelBytes[2..], UnitToYuvSample(yValue, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                WriteYuvSample(pixelBytes[4..], second, _layout.BitsPerSample, _layout.MsbAligned);
                WriteYuvSample(pixelBytes[6..], UnitToYuvSample(pixel.Alpha, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                pixelOffset = checked(pixelOffset + 8);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void DecodeUyv10A2<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var pixelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(source[pixelOffset..]);
                var u = value & 0x03ff;
                var ySample = (value >> 10) & 0x03ff;
                var v = (value >> 20) & 0x03ff;
                var alpha = (value >> 30) & 0x03;
                destinationRow[x] = TPixel.FromRgba32Float(YuvToRgba32Float(ySample, u, v, bitsPerSample: 10, alpha / 3f));
                pixelOffset = checked(pixelOffset + 4);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void EncodeUyv10A2<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var pixelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = TPixel.ToRgba32Float(sourceRow[x]);
                RgbaToYuv(pixel, out var yValue, out var u, out var v);
                var value =
                    (ChromaToYuvSample(u, bitsPerSample: 10) & 0x03ff)
                    | ((UnitToYuvSample(yValue, bitsPerSample: 10) & 0x03ff) << 10)
                    | ((ChromaToYuvSample(v, bitsPerSample: 10) & 0x03ff) << 20)
                    | ((UnitToYuvSample(pixel.Alpha, bitsPerSample: 2) & 0x03) << 30);
                BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(pixelOffset, 4), value);
                pixelOffset = checked(pixelOffset + 4);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded YUVA texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded YUVA texture.", nameof(destination));
        }
    }

    private static bool TryGetLayout(TextureFormat format, out PackedYuva444Layout layout)
    {
        if (format == TextureFormats.Vyua10Msb444UNorm) { layout = new PackedYuva444Layout(10, VFirst: true, MsbAligned: true); return true; }
        if (format == TextureFormats.Vyua10Lsb444UNorm) { layout = new PackedYuva444Layout(10, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Vyua12Msb444UNorm) { layout = new PackedYuva444Layout(12, VFirst: true, MsbAligned: true); return true; }
        if (format == TextureFormats.Vyua12Lsb444UNorm) { layout = new PackedYuva444Layout(12, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Uyva16_444UNorm) { layout = new PackedYuva444Layout(16, VFirst: false, MsbAligned: false); return true; }

        layout = default;
        return false;
    }

    private static uint ReadYuvSample(ReadOnlySpan<byte> source, int bitsPerSample, bool msbAligned)
    {
        if (bitsPerSample <= 8)
        {
            return source[0];
        }

        var sample = BinaryPrimitives.ReadUInt16LittleEndian(source);
        return bitsPerSample switch
        {
            10 => msbAligned ? (uint)(sample >> 6) : (uint)(sample & 0x03ff),
            12 => msbAligned ? (uint)(sample >> 4) : (uint)(sample & 0x0fff),
            16 => sample,
            _ => throw new InvalidOperationException($"Unsupported YUV sample size {bitsPerSample}.")
        };
    }

    private static void WriteYuvSample(Span<byte> destination, uint sample, int bitsPerSample, bool msbAligned)
    {
        if (bitsPerSample <= 8)
        {
            destination[0] = (byte)sample;
            return;
        }

        var value = bitsPerSample switch
        {
            10 => msbAligned ? sample << 6 : sample,
            12 => msbAligned ? sample << 4 : sample,
            16 => sample,
            _ => throw new InvalidOperationException($"Unsupported YUV sample size {bitsPerSample}.")
        };
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)value));
    }

    private static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, int bitsPerSample, float alpha = 1f)
    {
        var maxSample = GetMaxYuvSample(bitsPerSample);
        var y = ySample / (float)maxSample;
        var neutralChroma = 1u << (bitsPerSample - 1);
        var u = ((int)uSample - (int)neutralChroma) / (float)maxSample;
        var v = ((int)vSample - (int)neutralChroma) / (float)maxSample;
        return new Rgba32Float(
            Clamp01(y + (1.402f * v)),
            Clamp01(y - (0.344136f * u) - (0.714136f * v)),
            Clamp01(y + (1.772f * u)),
            Clamp01(alpha));
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

    private static uint UnitToYuvSample(float value, int bitsPerSample) =>
        ScaleToYuvSample(Clamp01(value), bitsPerSample);

    private static uint ChromaToYuvSample(float value, int bitsPerSample)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        var maxSample = GetMaxYuvSample(bitsPerSample);
        var neutralChroma = 1u << (bitsPerSample - 1);
        var scaled = MathF.Round((value * maxSample) + neutralChroma);
        if (scaled < 0f)
        {
            return 0;
        }

        return scaled > maxSample ? maxSample : (uint)scaled;
    }

    private static uint ScaleToYuvSample(float value, int bitsPerSample)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        var maxSample = GetMaxYuvSample(bitsPerSample);
        return (uint)MathF.Round(value * maxSample);
    }

    private static uint GetMaxYuvSample(int bitsPerSample) =>
        bitsPerSample == 16 ? ushort.MaxValue : (1u << bitsPerSample) - 1u;

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

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed YUVA 4:4:4 texture coder does not support texture format '{format.Name}'.");

    private readonly record struct PackedYuva444Layout(int BitsPerSample, bool VFirst, bool MsbAligned);
}
