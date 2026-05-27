using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedYuv422TextureCoder : IPitchTextureCoder
{
    private readonly PackedYuv422Layout _layout;
    private readonly bool _is8BitPacked;

    public PackedYuv422TextureCoder(TextureFormat format)
    {
        if (!TryGetLayout(format, out _layout, out _is8BitPacked))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetLayout(format, out _, out _);

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return _is8BitPacked
            ? Format.GetRowByteCount(width)
            : GetHighBitRowByteCount(width);
    }

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);
        if (_is8BitPacked)
        {
            ValidatePacked8BitWidth(width);
        }

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed YUV row byte count.");
        }

        return checked(rowPitch * height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        if (_is8BitPacked)
        {
            Decode8Bit(source, destination, rowPitch);
            return;
        }

        DecodeHighBit(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        if (_is8BitPacked)
        {
            Encode8Bit(source, destination, rowPitch);
            return;
        }

        EncodeHighBit(source, destination, rowPitch);
    }

    private void Decode8Bit<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blocksPerRow = destination.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var block = source.Slice(blockOffset, 4);
                byte y0;
                byte y1;
                byte u;
                byte v;
                if (_layout.UFirst)
                {
                    u = block[0];
                    y0 = block[1];
                    v = block[2];
                    y1 = block[3];
                }
                else
                {
                    y0 = block[0];
                    u = block[1];
                    y1 = block[2];
                    v = block[3];
                }

                destinationRow[pixelX] = TPixel.FromRgba32Float(YuvToRgba32Float(y0, u, v, bitsPerSample: 8));
                destinationRow[pixelX + 1] = TPixel.FromRgba32Float(YuvToRgba32Float(y1, u, v, bitsPerSample: 8));
                blockOffset = checked(blockOffset + 4);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode8Bit<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blocksPerRow = source.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                RgbaToYuv(TPixel.ToRgba32Float(sourceRow[pixelX]), out var y0, out var u0, out var v0);
                RgbaToYuv(TPixel.ToRgba32Float(sourceRow[pixelX + 1]), out var y1, out var u1, out var v1);
                var u = ChromaToYuvSample((u0 + u1) * 0.5f, bitsPerSample: 8);
                var v = ChromaToYuvSample((v0 + v1) * 0.5f, bitsPerSample: 8);
                var block = destination.Slice(blockOffset, 4);
                if (_layout.UFirst)
                {
                    block[0] = (byte)u;
                    block[1] = (byte)UnitToYuvSample(y0, bitsPerSample: 8);
                    block[2] = (byte)v;
                    block[3] = (byte)UnitToYuvSample(y1, bitsPerSample: 8);
                }
                else
                {
                    block[0] = (byte)UnitToYuvSample(y0, bitsPerSample: 8);
                    block[1] = (byte)u;
                    block[2] = (byte)UnitToYuvSample(y1, bitsPerSample: 8);
                    block[3] = (byte)v;
                }

                blockOffset = checked(blockOffset + 4);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeHighBit<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blockCountX = (destination.Width + 1) / 2;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                uint y0;
                uint y1;
                uint u;
                uint v;
                if (_layout.UFirst)
                {
                    u = ReadYuvSample(source[blockOffset..], _layout.BitsPerSample, _layout.MsbAligned);
                    y0 = ReadYuvSample(source[(blockOffset + 2)..], _layout.BitsPerSample, _layout.MsbAligned);
                    v = ReadYuvSample(source[(blockOffset + 4)..], _layout.BitsPerSample, _layout.MsbAligned);
                    y1 = ReadYuvSample(source[(blockOffset + 6)..], _layout.BitsPerSample, _layout.MsbAligned);
                }
                else
                {
                    y0 = ReadYuvSample(source[blockOffset..], _layout.BitsPerSample, _layout.MsbAligned);
                    u = ReadYuvSample(source[(blockOffset + 2)..], _layout.BitsPerSample, _layout.MsbAligned);
                    y1 = ReadYuvSample(source[(blockOffset + 4)..], _layout.BitsPerSample, _layout.MsbAligned);
                    v = ReadYuvSample(source[(blockOffset + 6)..], _layout.BitsPerSample, _layout.MsbAligned);
                }

                destinationRow[pixelX] = TPixel.FromRgba32Float(YuvToRgba32Float(y0, u, v, _layout.BitsPerSample));
                if (pixelX + 1 < destination.Width)
                {
                    destinationRow[pixelX + 1] = TPixel.FromRgba32Float(YuvToRgba32Float(y1, u, v, _layout.BitsPerSample));
                }

                blockOffset = checked(blockOffset + 8);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeHighBit<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blockCountX = (source.Width + 1) / 2;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var first = TPixel.ToRgba32Float(sourceRow[pixelX]);
                var second = pixelX + 1 < source.Width ? TPixel.ToRgba32Float(sourceRow[pixelX + 1]) : first;
                RgbaToYuv(first, out var y0, out var u0, out var v0);
                RgbaToYuv(second, out var y1, out var u1, out var v1);
                var u = ChromaToYuvSample((u0 + u1) * 0.5f, _layout.BitsPerSample);
                var v = ChromaToYuvSample((v0 + v1) * 0.5f, _layout.BitsPerSample);
                var block = destination.Slice(blockOffset, 8);
                if (_layout.UFirst)
                {
                    WriteYuvSample(block, u, _layout.BitsPerSample, _layout.MsbAligned);
                    WriteYuvSample(block[2..], UnitToYuvSample(y0, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                    WriteYuvSample(block[4..], v, _layout.BitsPerSample, _layout.MsbAligned);
                    WriteYuvSample(block[6..], UnitToYuvSample(y1, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                }
                else
                {
                    WriteYuvSample(block, UnitToYuvSample(y0, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                    WriteYuvSample(block[2..], u, _layout.BitsPerSample, _layout.MsbAligned);
                    WriteYuvSample(block[4..], UnitToYuvSample(y1, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                    WriteYuvSample(block[6..], v, _layout.BitsPerSample, _layout.MsbAligned);
                }

                blockOffset = checked(blockOffset + 8);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded YUV texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded YUV texture.", nameof(destination));
        }
    }

    private static bool TryGetLayout(TextureFormat format, out PackedYuv422Layout layout, out bool is8BitPacked)
    {
        if (format == TextureFormats.Uyvy422UNorm) { layout = new PackedYuv422Layout(8, UFirst: true, MsbAligned: false); is8BitPacked = true; return true; }
        if (format == TextureFormats.Yuy2UNorm) { layout = new PackedYuv422Layout(8, UFirst: false, MsbAligned: false); is8BitPacked = true; return true; }
        if (format == TextureFormats.Yuyv16_422UNorm) { layout = new PackedYuv422Layout(16, UFirst: false, MsbAligned: false); is8BitPacked = false; return true; }
        if (format == TextureFormats.Uyvy16_422UNorm) { layout = new PackedYuv422Layout(16, UFirst: true, MsbAligned: false); is8BitPacked = false; return true; }
        if (format == TextureFormats.Yuyv10Msb422UNorm) { layout = new PackedYuv422Layout(10, UFirst: false, MsbAligned: true); is8BitPacked = false; return true; }
        if (format == TextureFormats.Yuyv10Lsb422UNorm) { layout = new PackedYuv422Layout(10, UFirst: false, MsbAligned: false); is8BitPacked = false; return true; }
        if (format == TextureFormats.Uyvy10Msb422UNorm) { layout = new PackedYuv422Layout(10, UFirst: true, MsbAligned: true); is8BitPacked = false; return true; }
        if (format == TextureFormats.Uyvy10Lsb422UNorm) { layout = new PackedYuv422Layout(10, UFirst: true, MsbAligned: false); is8BitPacked = false; return true; }
        if (format == TextureFormats.Yuyv12Msb422UNorm) { layout = new PackedYuv422Layout(12, UFirst: false, MsbAligned: true); is8BitPacked = false; return true; }
        if (format == TextureFormats.Yuyv12Lsb422UNorm) { layout = new PackedYuv422Layout(12, UFirst: false, MsbAligned: false); is8BitPacked = false; return true; }
        if (format == TextureFormats.Uyvy12Msb422UNorm) { layout = new PackedYuv422Layout(12, UFirst: true, MsbAligned: true); is8BitPacked = false; return true; }
        if (format == TextureFormats.Uyvy12Lsb422UNorm) { layout = new PackedYuv422Layout(12, UFirst: true, MsbAligned: false); is8BitPacked = false; return true; }

        layout = default;
        is8BitPacked = false;
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

    private static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, int bitsPerSample)
    {
        var maxSample = GetMaxYuvSample(bitsPerSample);
        var y = ySample / (float)maxSample;
        var neutralChroma = 1u << (bitsPerSample - 1);
        var u = ((int)uSample - (int)neutralChroma) / (float)maxSample;
        var v = ((int)vSample - (int)neutralChroma) / (float)maxSample;
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

    private static int GetHighBitRowByteCount(int width) => checked(((width + 1) / 2) * 8);

    private static void ValidatePacked8BitWidth(int width)
    {
        if ((width & 1) != 0)
        {
            throw new ArgumentException("Packed 8-bit YUV 4:2:2 textures require an even width.", nameof(width));
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed YUV 4:2:2 texture coder does not support texture format '{format.Name}'.");

    private readonly record struct PackedYuv422Layout(int BitsPerSample, bool UFirst, bool MsbAligned);
}
