using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        DecodeByTransfer(source, destination, rowPitch);
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeByTransfer(source, destination, rowPitch);
    }

    private void DecodeByTransfer<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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

    private void EncodeByTransfer<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : struct, IPackedYuva444Transfer
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

    private static void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : struct, IPackedYuva444Transfer
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
        static abstract uint ReadFirst(ReadOnlySpan<byte> source);

        static abstract uint ReadY(ReadOnlySpan<byte> source);

        static abstract uint ReadSecond(ReadOnlySpan<byte> source);

        static abstract uint ReadAlpha(ReadOnlySpan<byte> source);

        static abstract void WriteFirst(Span<byte> destination, uint sample);

        static abstract void WriteY(Span<byte> destination, uint sample);

        static abstract void WriteSecond(Span<byte> destination, uint sample);

        static abstract void WriteAlpha(Span<byte> destination, uint sample);

        static abstract uint GetU(uint first, uint second);

        static abstract uint GetV(uint first, uint second);

        static abstract uint GetFirstChromaSample(float u, float v);

        static abstract uint GetSecondChromaSample(float u, float v);

        static abstract uint UnitToYuvSample(float value);

        static abstract Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, uint alpha);
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

        public static uint ReadFirst(ReadOnlySpan<byte> source) => Read10Msb(source);

        public static uint ReadY(ReadOnlySpan<byte> source) => Read10Msb(source[2..]);

        public static uint ReadSecond(ReadOnlySpan<byte> source) => Read10Msb(source[4..]);

        public static uint ReadAlpha(ReadOnlySpan<byte> source) => Read10Msb(source[6..]);

        public static void WriteFirst(Span<byte> destination, uint sample) => Write10Msb(destination, sample);

        public static void WriteY(Span<byte> destination, uint sample) => Write10Msb(destination[2..], sample);

        public static void WriteSecond(Span<byte> destination, uint sample) => Write10Msb(destination[4..], sample);

        public static void WriteAlpha(Span<byte> destination, uint sample) => Write10Msb(destination[6..], sample);

        public static uint GetU(uint first, uint second) => second;

        public static uint GetV(uint first, uint second) => first;

        public static uint GetFirstChromaSample(float u, float v) => ChromaToYuvSample(v, bitsPerSample: 10);

        public static uint GetSecondChromaSample(float u, float v) => ChromaToYuvSample(u, bitsPerSample: 10);

        public static uint UnitToYuvSample(float value) =>
            PackedYuva444TextureCoder.UnitToYuvSample(value, bitsPerSample: 10);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, uint alpha) =>
            PackedYuva444TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 10, alpha / (float)GetMaxYuvSample(bitsPerSample: 10));

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua10MsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua10MsbTransfer>(value, destination);
    }

    private readonly struct Vyua10LsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;

        public static uint ReadFirst(ReadOnlySpan<byte> source) => Read10Lsb(source);

        public static uint ReadY(ReadOnlySpan<byte> source) => Read10Lsb(source[2..]);

        public static uint ReadSecond(ReadOnlySpan<byte> source) => Read10Lsb(source[4..]);

        public static uint ReadAlpha(ReadOnlySpan<byte> source) => Read10Lsb(source[6..]);

        public static void WriteFirst(Span<byte> destination, uint sample) => Write10Lsb(destination, sample);

        public static void WriteY(Span<byte> destination, uint sample) => Write10Lsb(destination[2..], sample);

        public static void WriteSecond(Span<byte> destination, uint sample) => Write10Lsb(destination[4..], sample);

        public static void WriteAlpha(Span<byte> destination, uint sample) => Write10Lsb(destination[6..], sample);

        public static uint GetU(uint first, uint second) => second;

        public static uint GetV(uint first, uint second) => first;

        public static uint GetFirstChromaSample(float u, float v) => ChromaToYuvSample(v, bitsPerSample: 10);

        public static uint GetSecondChromaSample(float u, float v) => ChromaToYuvSample(u, bitsPerSample: 10);

        public static uint UnitToYuvSample(float value) =>
            PackedYuva444TextureCoder.UnitToYuvSample(value, bitsPerSample: 10);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, uint alpha) =>
            PackedYuva444TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 10, alpha / (float)GetMaxYuvSample(bitsPerSample: 10));

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua10LsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua10LsbTransfer>(value, destination);
    }

    private readonly struct Vyua12MsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;

        public static uint ReadFirst(ReadOnlySpan<byte> source) => Read12Msb(source);

        public static uint ReadY(ReadOnlySpan<byte> source) => Read12Msb(source[2..]);

        public static uint ReadSecond(ReadOnlySpan<byte> source) => Read12Msb(source[4..]);

        public static uint ReadAlpha(ReadOnlySpan<byte> source) => Read12Msb(source[6..]);

        public static void WriteFirst(Span<byte> destination, uint sample) => Write12Msb(destination, sample);

        public static void WriteY(Span<byte> destination, uint sample) => Write12Msb(destination[2..], sample);

        public static void WriteSecond(Span<byte> destination, uint sample) => Write12Msb(destination[4..], sample);

        public static void WriteAlpha(Span<byte> destination, uint sample) => Write12Msb(destination[6..], sample);

        public static uint GetU(uint first, uint second) => second;

        public static uint GetV(uint first, uint second) => first;

        public static uint GetFirstChromaSample(float u, float v) => ChromaToYuvSample(v, bitsPerSample: 12);

        public static uint GetSecondChromaSample(float u, float v) => ChromaToYuvSample(u, bitsPerSample: 12);

        public static uint UnitToYuvSample(float value) =>
            PackedYuva444TextureCoder.UnitToYuvSample(value, bitsPerSample: 12);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, uint alpha) =>
            PackedYuva444TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 12, alpha / (float)GetMaxYuvSample(bitsPerSample: 12));

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua12MsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua12MsbTransfer>(value, destination);
    }

    private readonly struct Vyua12LsbTransfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;

        public static uint ReadFirst(ReadOnlySpan<byte> source) => Read12Lsb(source);

        public static uint ReadY(ReadOnlySpan<byte> source) => Read12Lsb(source[2..]);

        public static uint ReadSecond(ReadOnlySpan<byte> source) => Read12Lsb(source[4..]);

        public static uint ReadAlpha(ReadOnlySpan<byte> source) => Read12Lsb(source[6..]);

        public static void WriteFirst(Span<byte> destination, uint sample) => Write12Lsb(destination, sample);

        public static void WriteY(Span<byte> destination, uint sample) => Write12Lsb(destination[2..], sample);

        public static void WriteSecond(Span<byte> destination, uint sample) => Write12Lsb(destination[4..], sample);

        public static void WriteAlpha(Span<byte> destination, uint sample) => Write12Lsb(destination[6..], sample);

        public static uint GetU(uint first, uint second) => second;

        public static uint GetV(uint first, uint second) => first;

        public static uint GetFirstChromaSample(float u, float v) => ChromaToYuvSample(v, bitsPerSample: 12);

        public static uint GetSecondChromaSample(float u, float v) => ChromaToYuvSample(u, bitsPerSample: 12);

        public static uint UnitToYuvSample(float value) =>
            PackedYuva444TextureCoder.UnitToYuvSample(value, bitsPerSample: 12);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, uint alpha) =>
            PackedYuva444TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 12, alpha / (float)GetMaxYuvSample(bitsPerSample: 12));

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Vyua12LsbTransfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Vyua12LsbTransfer>(value, destination);
    }

    private readonly struct Uyva16Transfer : IWideYuva444Transfer
    {
        public static int BytesPerTexel => 8;

        public static uint ReadFirst(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);

        public static uint ReadY(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);

        public static uint ReadSecond(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);

        public static uint ReadAlpha(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source[6..]);

        public static void WriteFirst(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)sample));

        public static void WriteY(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], checked((ushort)sample));

        public static void WriteSecond(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], checked((ushort)sample));

        public static void WriteAlpha(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], checked((ushort)sample));

        public static uint GetU(uint first, uint second) => first;

        public static uint GetV(uint first, uint second) => second;

        public static uint GetFirstChromaSample(float u, float v) => ChromaToYuvSample(u, bitsPerSample: 16);

        public static uint GetSecondChromaSample(float u, float v) => ChromaToYuvSample(v, bitsPerSample: 16);

        public static uint UnitToYuvSample(float value) =>
            PackedYuva444TextureCoder.UnitToYuvSample(value, bitsPerSample: 16);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, uint alpha) =>
            PackedYuva444TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 16, alpha / (float)GetMaxYuvSample(bitsPerSample: 16));

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) => DecodeWide<Uyva16Transfer>(source);

        public static void Encode(Rgba32Float value, Span<byte> destination) => EncodeWide<Uyva16Transfer>(value, destination);
    }

    private static Rgba32Float DecodeWide<TTransfer>(ReadOnlySpan<byte> source)
        where TTransfer : struct, IWideYuva444Transfer
    {
        var first = TTransfer.ReadFirst(source);
        var ySample = TTransfer.ReadY(source);
        var second = TTransfer.ReadSecond(source);
        var alpha = TTransfer.ReadAlpha(source);
        return TTransfer.YuvToRgba32Float(ySample, TTransfer.GetU(first, second), TTransfer.GetV(first, second), alpha);
    }

    private static void EncodeWide<TTransfer>(Rgba32Float value, Span<byte> destination)
        where TTransfer : struct, IWideYuva444Transfer
    {
        RgbaToYuv(value, out var yValue, out var u, out var v);
        TTransfer.WriteFirst(destination, TTransfer.GetFirstChromaSample(u, v));
        TTransfer.WriteY(destination, TTransfer.UnitToYuvSample(yValue));
        TTransfer.WriteSecond(destination, TTransfer.GetSecondChromaSample(u, v));
        TTransfer.WriteAlpha(destination, TTransfer.UnitToYuvSample(value.Alpha));
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

    private static uint Read10Msb(ReadOnlySpan<byte> source) =>
        (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) >> 6;

    private static uint Read10Lsb(ReadOnlySpan<byte> source) =>
        (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) & 0x03ff;

    private static uint Read12Msb(ReadOnlySpan<byte> source) =>
        (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) >> 4;

    private static uint Read12Lsb(ReadOnlySpan<byte> source) =>
        (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) & 0x0fff;

    private static void Write10Msb(Span<byte> destination, uint sample) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)(sample << 6)));

    private static void Write10Lsb(Span<byte> destination, uint sample) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)sample));

    private static void Write12Msb(Span<byte> destination, uint sample) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)(sample << 4)));

    private static void Write12Lsb(Span<byte> destination, uint sample) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)sample));

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
