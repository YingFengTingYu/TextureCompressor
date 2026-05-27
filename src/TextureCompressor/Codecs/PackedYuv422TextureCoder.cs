using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedYuv422TextureCoder : IPitchTextureCoder
{
    private readonly PackedYuv422Transfer _transfer;

    public PackedYuv422TextureCoder(TextureFormat format)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetTransfer(format, out _);

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return Is8BitPacked(_transfer)
            ? Format.GetRowByteCount(width)
            : GetHighBitRowByteCount(width);
    }

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);
        if (Is8BitPacked(_transfer))
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
            case PackedYuv422Transfer.Uyvy8:
                Decode<TPixel, Uyvy8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv8:
                Decode<TPixel, Yuyv8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv16:
                Decode<TPixel, Yuyv16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy16:
                Decode<TPixel, Uyvy16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Msb:
                Decode<TPixel, Yuyv10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Lsb:
                Decode<TPixel, Yuyv10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Msb:
                Decode<TPixel, Uyvy10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Lsb:
                Decode<TPixel, Uyvy10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Msb:
                Decode<TPixel, Yuyv12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Lsb:
                Decode<TPixel, Yuyv12LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Msb:
                Decode<TPixel, Uyvy12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Lsb:
                Decode<TPixel, Uyvy12LsbTransfer>(source, destination, rowPitch);
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
            case PackedYuv422Transfer.Uyvy8:
                Encode<TPixel, Uyvy8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv8:
                Encode<TPixel, Yuyv8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv16:
                Encode<TPixel, Yuyv16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy16:
                Encode<TPixel, Uyvy16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Msb:
                Encode<TPixel, Yuyv10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Lsb:
                Encode<TPixel, Yuyv10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Msb:
                Encode<TPixel, Uyvy10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Lsb:
                Encode<TPixel, Uyvy10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Msb:
                Encode<TPixel, Yuyv12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Lsb:
                Encode<TPixel, Yuyv12LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Msb:
                Encode<TPixel, Uyvy12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Lsb:
                Encode<TPixel, Uyvy12LsbTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedYuv422Transfer
    {
        var blockCountX = TTransfer.RequiresEvenWidth
            ? destination.Width / 2
            : (destination.Width + 1) / 2;
        var bytesPerSample = TTransfer.BytesPerBlock / 4;
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
                if (TTransfer.UFirst)
                {
                    u = ReadYuvSample<TTransfer>(source[blockOffset..]);
                    y0 = ReadYuvSample<TTransfer>(source[(blockOffset + bytesPerSample)..]);
                    v = ReadYuvSample<TTransfer>(source[(blockOffset + (2 * bytesPerSample))..]);
                    y1 = ReadYuvSample<TTransfer>(source[(blockOffset + (3 * bytesPerSample))..]);
                }
                else
                {
                    y0 = ReadYuvSample<TTransfer>(source[blockOffset..]);
                    u = ReadYuvSample<TTransfer>(source[(blockOffset + bytesPerSample)..]);
                    y1 = ReadYuvSample<TTransfer>(source[(blockOffset + (2 * bytesPerSample))..]);
                    v = ReadYuvSample<TTransfer>(source[(blockOffset + (3 * bytesPerSample))..]);
                }

                destinationRow[pixelX] = TPixel.FromRgba32Float(YuvToRgba32Float(y0, u, v, TTransfer.BitsPerSample));
                if (pixelX + 1 < destination.Width)
                {
                    destinationRow[pixelX + 1] = TPixel.FromRgba32Float(YuvToRgba32Float(y1, u, v, TTransfer.BitsPerSample));
                }

                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedYuv422Transfer
    {
        var blockCountX = TTransfer.RequiresEvenWidth
            ? source.Width / 2
            : (source.Width + 1) / 2;
        var bytesPerSample = TTransfer.BytesPerBlock / 4;
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
                var u = ChromaToYuvSample((u0 + u1) * 0.5f, TTransfer.BitsPerSample);
                var v = ChromaToYuvSample((v0 + v1) * 0.5f, TTransfer.BitsPerSample);
                var block = destination.Slice(blockOffset, TTransfer.BytesPerBlock);
                if (TTransfer.UFirst)
                {
                    WriteYuvSample<TTransfer>(block, u);
                    WriteYuvSample<TTransfer>(block[bytesPerSample..], UnitToYuvSample(y0, TTransfer.BitsPerSample));
                    WriteYuvSample<TTransfer>(block[(2 * bytesPerSample)..], v);
                    WriteYuvSample<TTransfer>(block[(3 * bytesPerSample)..], UnitToYuvSample(y1, TTransfer.BitsPerSample));
                }
                else
                {
                    WriteYuvSample<TTransfer>(block, UnitToYuvSample(y0, TTransfer.BitsPerSample));
                    WriteYuvSample<TTransfer>(block[bytesPerSample..], u);
                    WriteYuvSample<TTransfer>(block[(2 * bytesPerSample)..], UnitToYuvSample(y1, TTransfer.BitsPerSample));
                    WriteYuvSample<TTransfer>(block[(3 * bytesPerSample)..], v);
                }

                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IPackedYuv422Transfer
    {
        static abstract int BitsPerSample { get; }

        static abstract bool UFirst { get; }

        static abstract bool MsbAligned { get; }

        static abstract bool RequiresEvenWidth { get; }

        static abstract int BytesPerBlock { get; }
    }

    private readonly struct Uyvy8Transfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 8;
        public static bool UFirst => true;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => true;
        public static int BytesPerBlock => 4;
    }

    private readonly struct Yuyv8Transfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 8;
        public static bool UFirst => false;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => true;
        public static int BytesPerBlock => 4;
    }

    private readonly struct Yuyv16Transfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 16;
        public static bool UFirst => false;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Uyvy16Transfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 16;
        public static bool UFirst => true;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Yuyv10MsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 10;
        public static bool UFirst => false;
        public static bool MsbAligned => true;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Yuyv10LsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 10;
        public static bool UFirst => false;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Uyvy10MsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 10;
        public static bool UFirst => true;
        public static bool MsbAligned => true;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Uyvy10LsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 10;
        public static bool UFirst => true;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Yuyv12MsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 12;
        public static bool UFirst => false;
        public static bool MsbAligned => true;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Yuyv12LsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 12;
        public static bool UFirst => false;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Uyvy12MsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 12;
        public static bool UFirst => true;
        public static bool MsbAligned => true;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
    }

    private readonly struct Uyvy12LsbTransfer : IPackedYuv422Transfer
    {
        public static int BitsPerSample => 12;
        public static bool UFirst => true;
        public static bool MsbAligned => false;
        public static bool RequiresEvenWidth => false;
        public static int BytesPerBlock => 8;
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

    private static bool TryGetTransfer(TextureFormat format, out PackedYuv422Transfer transfer)
    {
        if (format == TextureFormats.Uyvy422UNorm) { transfer = PackedYuv422Transfer.Uyvy8; return true; }
        if (format == TextureFormats.Yuy2UNorm) { transfer = PackedYuv422Transfer.Yuyv8; return true; }
        if (format == TextureFormats.Yuyv16_422UNorm) { transfer = PackedYuv422Transfer.Yuyv16; return true; }
        if (format == TextureFormats.Uyvy16_422UNorm) { transfer = PackedYuv422Transfer.Uyvy16; return true; }
        if (format == TextureFormats.Yuyv10Msb422UNorm) { transfer = PackedYuv422Transfer.Yuyv10Msb; return true; }
        if (format == TextureFormats.Yuyv10Lsb422UNorm) { transfer = PackedYuv422Transfer.Yuyv10Lsb; return true; }
        if (format == TextureFormats.Uyvy10Msb422UNorm) { transfer = PackedYuv422Transfer.Uyvy10Msb; return true; }
        if (format == TextureFormats.Uyvy10Lsb422UNorm) { transfer = PackedYuv422Transfer.Uyvy10Lsb; return true; }
        if (format == TextureFormats.Yuyv12Msb422UNorm) { transfer = PackedYuv422Transfer.Yuyv12Msb; return true; }
        if (format == TextureFormats.Yuyv12Lsb422UNorm) { transfer = PackedYuv422Transfer.Yuyv12Lsb; return true; }
        if (format == TextureFormats.Uyvy12Msb422UNorm) { transfer = PackedYuv422Transfer.Uyvy12Msb; return true; }
        if (format == TextureFormats.Uyvy12Lsb422UNorm) { transfer = PackedYuv422Transfer.Uyvy12Lsb; return true; }

        transfer = default;
        return false;
    }

    private static bool Is8BitPacked(PackedYuv422Transfer transfer) =>
        transfer is PackedYuv422Transfer.Uyvy8 or PackedYuv422Transfer.Yuyv8;

    private static uint ReadYuvSample<TTransfer>(ReadOnlySpan<byte> source)
        where TTransfer : IPackedYuv422Transfer
    {
        if (TTransfer.BitsPerSample <= 8)
        {
            return source[0];
        }

        var sample = BinaryPrimitives.ReadUInt16LittleEndian(source);
        return TTransfer.BitsPerSample switch
        {
            10 => TTransfer.MsbAligned ? (uint)(sample >> 6) : (uint)(sample & 0x03ff),
            12 => TTransfer.MsbAligned ? (uint)(sample >> 4) : (uint)(sample & 0x0fff),
            16 => sample,
            _ => throw new InvalidOperationException($"Unsupported YUV sample size {TTransfer.BitsPerSample}.")
        };
    }

    private static void WriteYuvSample<TTransfer>(Span<byte> destination, uint sample)
        where TTransfer : IPackedYuv422Transfer
    {
        if (TTransfer.BitsPerSample <= 8)
        {
            destination[0] = (byte)sample;
            return;
        }

        var value = TTransfer.BitsPerSample switch
        {
            10 => TTransfer.MsbAligned ? sample << 6 : sample,
            12 => TTransfer.MsbAligned ? sample << 4 : sample,
            16 => sample,
            _ => throw new InvalidOperationException($"Unsupported YUV sample size {TTransfer.BitsPerSample}.")
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

    private enum PackedYuv422Transfer
    {
        Uyvy8,
        Yuyv8,
        Yuyv16,
        Uyvy16,
        Yuyv10Msb,
        Yuyv10Lsb,
        Uyvy10Msb,
        Uyvy10Lsb,
        Yuyv12Msb,
        Yuyv12Lsb,
        Uyvy12Msb,
        Uyvy12Lsb
    }

}
