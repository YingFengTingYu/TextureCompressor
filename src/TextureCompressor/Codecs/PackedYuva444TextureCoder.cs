using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedYuva444TextureCoder : IPitchTextureCoder
{
    private readonly PackedYuva444Transfer _transfer;

    public PackedYuva444TextureCoder(TextureFormat format)
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
        return Format.GetRowByteCount(width);
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
            case PackedYuva444Transfer.Ayuv:
                Decode<TPixel, AyuvTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Uyv10A2:
                Decode<TPixel, Uyv10A2Transfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua10Msb:
                Decode<TPixel, Vyua10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua10Lsb:
                Decode<TPixel, Vyua10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua12Msb:
                Decode<TPixel, Vyua12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua12Lsb:
                Decode<TPixel, Vyua12LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Uyva16:
                Decode<TPixel, Uyva16Transfer>(source, destination, rowPitch);
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
            case PackedYuva444Transfer.Ayuv:
                Encode<TPixel, AyuvTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Uyv10A2:
                Encode<TPixel, Uyv10A2Transfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua10Msb:
                Encode<TPixel, Vyua10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua10Lsb:
                Encode<TPixel, Vyua10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua12Msb:
                Encode<TPixel, Vyua12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Vyua12Lsb:
                Encode<TPixel, Vyua12LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuva444Transfer.Uyva16:
                Encode<TPixel, Uyva16Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedYuva444Transfer
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var pixelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba32Float(TTransfer.Decode(source.Slice(pixelOffset, TTransfer.BytesPerTexel)));
                pixelOffset = checked(pixelOffset + TTransfer.BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedYuva444Transfer
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var pixelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba32Float(sourceRow[x]), destination.Slice(pixelOffset, TTransfer.BytesPerTexel));
                pixelOffset = checked(pixelOffset + TTransfer.BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IPackedYuva444Transfer
    {
        static abstract int BytesPerTexel { get; }

        static abstract Rgba32Float Decode(ReadOnlySpan<byte> source);

        static abstract void Encode(Rgba32Float value, Span<byte> destination);
    }

    private interface IWideYuva444Transfer : IPackedYuva444Transfer
    {
        static abstract int BitsPerSample { get; }

        static abstract bool VFirst { get; }

        static abstract bool MsbAligned { get; }
    }

    private readonly struct AyuvTransfer : IPackedYuva444Transfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source)
        {
            var v = source[0];
            var u = source[1];
            var ySample = source[2];
            var alpha = source[3];
            return YuvToRgba32Float(ySample, u, v, bitsPerSample: 8, alpha / 255f);
        }

        public static void Encode(Rgba32Float value, Span<byte> destination)
        {
            RgbaToYuv(value, out var yValue, out var u, out var v);
            destination[0] = checked((byte)ChromaToYuvSample(v, bitsPerSample: 8));
            destination[1] = checked((byte)ChromaToYuvSample(u, bitsPerSample: 8));
            destination[2] = checked((byte)UnitToYuvSample(yValue, bitsPerSample: 8));
            destination[3] = checked((byte)UnitToYuvSample(value.Alpha, bitsPerSample: 8));
        }
    }

    private readonly struct Uyv10A2Transfer : IPackedYuva444Transfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(source);
            var u = value & 0x03ff;
            var ySample = (value >> 10) & 0x03ff;
            var v = (value >> 20) & 0x03ff;
            var alpha = (value >> 30) & 0x03;
            return YuvToRgba32Float(ySample, u, v, bitsPerSample: 10, alpha / 3f);
        }

        public static void Encode(Rgba32Float value, Span<byte> destination)
        {
            RgbaToYuv(value, out var yValue, out var u, out var v);
            var packed =
                (ChromaToYuvSample(u, bitsPerSample: 10) & 0x03ff)
                | ((UnitToYuvSample(yValue, bitsPerSample: 10) & 0x03ff) << 10)
                | ((ChromaToYuvSample(v, bitsPerSample: 10) & 0x03ff) << 20)
                | ((UnitToYuvSample(value.Alpha, bitsPerSample: 2) & 0x03) << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, packed);
        }
    }

    private readonly struct Vyua10MsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;
        public static int BitsPerSample => 10;
        public static bool VFirst => true;
        public static bool MsbAligned => true;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua10MsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua10MsbTransfer>(value, destination);
    }

    private readonly struct Vyua10LsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;
        public static int BitsPerSample => 10;
        public static bool VFirst => true;
        public static bool MsbAligned => false;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua10LsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua10LsbTransfer>(value, destination);
    }

    private readonly struct Vyua12MsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;
        public static int BitsPerSample => 12;
        public static bool VFirst => true;
        public static bool MsbAligned => true;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua12MsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua12MsbTransfer>(value, destination);
    }

    private readonly struct Vyua12LsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;
        public static int BitsPerSample => 12;
        public static bool VFirst => true;
        public static bool MsbAligned => false;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua12LsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua12LsbTransfer>(value, destination);
    }

    private readonly struct Uyva16Transfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;
        public static int BitsPerSample => 16;
        public static bool VFirst => false;
        public static bool MsbAligned => false;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Uyva16Transfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Uyva16Transfer>(value, destination);
    }

    private static Rgba32Float DecodeWide<TTransfer>(ReadOnlySpan<byte> source)
        where TTransfer : IWideYuva444Transfer
    {
        var maxSample = GetMaxYuvSample(TTransfer.BitsPerSample);
        var first = ReadYuvSample<TTransfer>(source);
        var ySample = ReadYuvSample<TTransfer>(source[2..]);
        var second = ReadYuvSample<TTransfer>(source[4..]);
        var alpha = ReadYuvSample<TTransfer>(source[6..]);
        var u = TTransfer.VFirst ? second : first;
        var v = TTransfer.VFirst ? first : second;
        return YuvToRgba32Float(ySample, u, v, TTransfer.BitsPerSample, alpha / (float)maxSample);
    }

    private static void EncodeWide<TTransfer>(Rgba32Float value, Span<byte> destination)
        where TTransfer : IWideYuva444Transfer
    {
        RgbaToYuv(value, out var yValue, out var u, out var v);
        var first = TTransfer.VFirst
            ? ChromaToYuvSample(v, TTransfer.BitsPerSample)
            : ChromaToYuvSample(u, TTransfer.BitsPerSample);
        var second = TTransfer.VFirst
            ? ChromaToYuvSample(u, TTransfer.BitsPerSample)
            : ChromaToYuvSample(v, TTransfer.BitsPerSample);
        WriteYuvSample<TTransfer>(destination, first);
        WriteYuvSample<TTransfer>(destination[2..], UnitToYuvSample(yValue, TTransfer.BitsPerSample));
        WriteYuvSample<TTransfer>(destination[4..], second);
        WriteYuvSample<TTransfer>(destination[6..], UnitToYuvSample(value.Alpha, TTransfer.BitsPerSample));
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

    private static bool TryGetTransfer(TextureFormat format, out PackedYuva444Transfer transfer)
    {
        if (format == TextureFormats.Ayuv444UNorm) { transfer = PackedYuva444Transfer.Ayuv; return true; }
        if (format == TextureFormats.Uyv10A2_444UNorm) { transfer = PackedYuva444Transfer.Uyv10A2; return true; }
        if (format == TextureFormats.Vyua10Msb444UNorm) { transfer = PackedYuva444Transfer.Vyua10Msb; return true; }
        if (format == TextureFormats.Vyua10Lsb444UNorm) { transfer = PackedYuva444Transfer.Vyua10Lsb; return true; }
        if (format == TextureFormats.Vyua12Msb444UNorm) { transfer = PackedYuva444Transfer.Vyua12Msb; return true; }
        if (format == TextureFormats.Vyua12Lsb444UNorm) { transfer = PackedYuva444Transfer.Vyua12Lsb; return true; }
        if (format == TextureFormats.Uyva16_444UNorm) { transfer = PackedYuva444Transfer.Uyva16; return true; }

        transfer = default;
        return false;
    }

    private static uint ReadYuvSample<TTransfer>(ReadOnlySpan<byte> source)
        where TTransfer : IWideYuva444Transfer
    {
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
        where TTransfer : IWideYuva444Transfer
    {
        var value = TTransfer.BitsPerSample switch
        {
            10 => TTransfer.MsbAligned ? sample << 6 : sample,
            12 => TTransfer.MsbAligned ? sample << 4 : sample,
            16 => sample,
            _ => throw new InvalidOperationException($"Unsupported YUV sample size {TTransfer.BitsPerSample}.")
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

    private enum PackedYuva444Transfer
    {
        Ayuv,
        Uyv10A2,
        Vyua10Msb,
        Vyua10Lsb,
        Vyua12Msb,
        Vyua12Lsb,
        Uyva16
    }

}
