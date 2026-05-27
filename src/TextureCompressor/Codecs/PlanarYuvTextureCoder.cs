using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PlanarYuvTextureCoder : IPitchTextureCoder
{
    private readonly PlanarYuvLayout _layout;

    public PlanarYuvTextureCoder(TextureFormat format)
    {
        if (!TryGetLayout(format, out _layout))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetLayout(format, out _);

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return checked(width * GetBytesPerSample(_layout.BitsPerSample));
    }

    public int GetEncodedByteCount(int width, int height, int rowPitch) =>
        checked((int)GetEncodedByteCount64(width, height, rowPitch));

    public long GetEncodedByteCount64(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);
        ValidateRowPitch(width, rowPitch);
        return GetPlanarYuvByteCount(width, height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        DecodeBySample(source, destination);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeBySample(source, destination);
    }

    private void DecodeBySample<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_layout.BitsPerSample)
        {
            case 8:
                DecodeByLayout<TPixel, Sample8Transfer>(source, destination);
                return;
            case 10 when _layout.MsbAligned:
                DecodeByLayout<TPixel, Sample10MsbTransfer>(source, destination);
                return;
            case 10:
                DecodeByLayout<TPixel, Sample10LsbTransfer>(source, destination);
                return;
            case 12 when _layout.MsbAligned:
                DecodeByLayout<TPixel, Sample12MsbTransfer>(source, destination);
                return;
            case 12:
                DecodeByLayout<TPixel, Sample12LsbTransfer>(source, destination);
                return;
            case 14 when _layout.MsbAligned:
                DecodeByLayout<TPixel, Sample14MsbTransfer>(source, destination);
                return;
            case 16:
                DecodeByLayout<TPixel, Sample16Transfer>(source, destination);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeBySample<TPixel>(ImageView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_layout.BitsPerSample)
        {
            case 8:
                EncodeByLayout<TPixel, Sample8Transfer>(source, destination);
                return;
            case 10 when _layout.MsbAligned:
                EncodeByLayout<TPixel, Sample10MsbTransfer>(source, destination);
                return;
            case 10:
                EncodeByLayout<TPixel, Sample10LsbTransfer>(source, destination);
                return;
            case 12 when _layout.MsbAligned:
                EncodeByLayout<TPixel, Sample12MsbTransfer>(source, destination);
                return;
            case 12:
                EncodeByLayout<TPixel, Sample12LsbTransfer>(source, destination);
                return;
            case 14 when _layout.MsbAligned:
                EncodeByLayout<TPixel, Sample14MsbTransfer>(source, destination);
                return;
            case 16:
                EncodeByLayout<TPixel, Sample16Transfer>(source, destination);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void DecodeByLayout<TPixel, TSample>(ReadOnlySpan<byte> source, ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : IPlanarYuvSampleTransfer
    {
        if (_layout.Biplanar)
        {
            if (_layout.VFirst)
            {
                DecodeBySubsample<TPixel, TSample, BiplanarTransfer, VuTransfer>(source, destination);
            }
            else
            {
                DecodeBySubsample<TPixel, TSample, BiplanarTransfer, UvTransfer>(source, destination);
            }

            return;
        }

        if (_layout.VFirst)
        {
            DecodeBySubsample<TPixel, TSample, ThreePlaneTransfer, VuTransfer>(source, destination);
        }
        else
        {
            DecodeBySubsample<TPixel, TSample, ThreePlaneTransfer, UvTransfer>(source, destination);
        }
    }

    private void EncodeByLayout<TPixel, TSample>(ImageView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : IPlanarYuvSampleTransfer
    {
        if (_layout.Biplanar)
        {
            if (_layout.VFirst)
            {
                EncodeBySubsample<TPixel, TSample, BiplanarTransfer, VuTransfer>(source, destination);
            }
            else
            {
                EncodeBySubsample<TPixel, TSample, BiplanarTransfer, UvTransfer>(source, destination);
            }

            return;
        }

        if (_layout.VFirst)
        {
            EncodeBySubsample<TPixel, TSample, ThreePlaneTransfer, VuTransfer>(source, destination);
        }
        else
        {
            EncodeBySubsample<TPixel, TSample, ThreePlaneTransfer, UvTransfer>(source, destination);
        }
    }

    private void DecodeBySubsample<TPixel, TSample, TPlane, TOrder>(ReadOnlySpan<byte> source, ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : IPlanarYuvSampleTransfer
        where TPlane : IPlanarYuvPlaneTransfer
        where TOrder : IPlanarYuvOrderTransfer
    {
        switch ((_layout.ChromaSubsampleX, _layout.ChromaSubsampleY))
        {
            case (1, 1):
                Decode<TPixel, TSample, TPlane, TOrder, Subsample444Transfer>(source, destination);
                return;
            case (2, 1):
                Decode<TPixel, TSample, TPlane, TOrder, Subsample422Transfer>(source, destination);
                return;
            case (2, 2):
                Decode<TPixel, TSample, TPlane, TOrder, Subsample420Transfer>(source, destination);
                return;
            case (1, 2):
                Decode<TPixel, TSample, TPlane, TOrder, Subsample412Transfer>(source, destination);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeBySubsample<TPixel, TSample, TPlane, TOrder>(ImageView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : IPlanarYuvSampleTransfer
        where TPlane : IPlanarYuvPlaneTransfer
        where TOrder : IPlanarYuvOrderTransfer
    {
        switch ((_layout.ChromaSubsampleX, _layout.ChromaSubsampleY))
        {
            case (1, 1):
                Encode<TPixel, TSample, TPlane, TOrder, Subsample444Transfer>(source, destination);
                return;
            case (2, 1):
                Encode<TPixel, TSample, TPlane, TOrder, Subsample422Transfer>(source, destination);
                return;
            case (2, 2):
                Encode<TPixel, TSample, TPlane, TOrder, Subsample420Transfer>(source, destination);
                return;
            case (1, 2):
                Encode<TPixel, TSample, TPlane, TOrder, Subsample412Transfer>(source, destination);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TSample, TPlane, TOrder, TSubsample>(ReadOnlySpan<byte> source, ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : IPlanarYuvSampleTransfer
        where TPlane : IPlanarYuvPlaneTransfer
        where TOrder : IPlanarYuvOrderTransfer
        where TSubsample : IPlanarYuvSubsampleTransfer
    {
        var lumaSampleCount = checked(destination.Width * destination.Height);
        var lumaByteCount = checked(lumaSampleCount * TSample.BytesPerSample);
        var chromaWidth = checked((destination.Width + TSubsample.X - 1) / TSubsample.X);
        var chromaHeight = checked((destination.Height + TSubsample.Y - 1) / TSubsample.Y);
        var chromaSampleCount = checked(chromaWidth * chromaHeight);
        var chromaPlaneByteCount = checked(chromaSampleCount * TSample.BytesPerSample);

        var luma = source[..lumaByteCount];
        var chroma = source[lumaByteCount..];
        ReadOnlySpan<byte> firstChromaPlane;
        ReadOnlySpan<byte> secondChromaPlane;
        if (TPlane.Biplanar)
        {
            firstChromaPlane = chroma;
            secondChromaPlane = chroma[TSample.BytesPerSample..];
        }
        else
        {
            firstChromaPlane = chroma[..chromaPlaneByteCount];
            secondChromaPlane = chroma.Slice(chromaPlaneByteCount, chromaPlaneByteCount);
        }

        var lumaRowOffset = 0;
        var lumaRowByteCount = checked(destination.Width * TSample.BytesPerSample);
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var chromaY = y / TSubsample.Y;
            var chromaRowIndex = chromaY * chromaWidth;
            var lumaOffset = lumaRowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var chromaX = x / TSubsample.X;
                var chromaIndex = chromaRowIndex + chromaX;
                var firstChromaIndex = TPlane.Biplanar
                    ? chromaIndex * 2 * TSample.BytesPerSample
                    : chromaIndex * TSample.BytesPerSample;
                var first = TSample.Read(firstChromaPlane[firstChromaIndex..]);
                var second = TSample.Read(secondChromaPlane[firstChromaIndex..]);
                var u = TOrder.VFirst ? second : first;
                var v = TOrder.VFirst ? first : second;
                var ySample = TSample.Read(luma[lumaOffset..]);
                destinationRow[x] = TPixel.FromRgba32Float(YuvToRgba32Float(ySample, u, v, TSample.BitsPerSample));
                lumaOffset = checked(lumaOffset + TSample.BytesPerSample);
            }

            lumaRowOffset = checked(lumaRowOffset + lumaRowByteCount);
        }
    }

    private static void Encode<TPixel, TSample, TPlane, TOrder, TSubsample>(ImageView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : IPlanarYuvSampleTransfer
        where TPlane : IPlanarYuvPlaneTransfer
        where TOrder : IPlanarYuvOrderTransfer
        where TSubsample : IPlanarYuvSubsampleTransfer
    {
        var texelCount = checked(source.Width * source.Height);
        var lumaByteCount = checked(texelCount * TSample.BytesPerSample);
        var chromaWidth = checked((source.Width + TSubsample.X - 1) / TSubsample.X);
        var chromaHeight = checked((source.Height + TSubsample.Y - 1) / TSubsample.Y);
        var chromaSampleCount = checked(chromaWidth * chromaHeight);
        var chromaPlaneByteCount = checked(chromaSampleCount * TSample.BytesPerSample);

        var lumaOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                RgbaToYuv(TPixel.ToRgba32Float(sourceRow[x]), out var yValue, out _, out _);
                TSample.Write(destination[lumaOffset..], UnitToYuvSample(yValue, TSample.BitsPerSample));
                lumaOffset = checked(lumaOffset + TSample.BytesPerSample);
            }
        }

        var chroma = destination[lumaByteCount..];
        var firstChromaPlane = TPlane.Biplanar ? chroma : chroma[..chromaPlaneByteCount];
        var secondChromaPlane = TPlane.Biplanar ? chroma[TSample.BytesPerSample..] : chroma.Slice(chromaPlaneByteCount, chromaPlaneByteCount);
        for (var chromaY = 0; chromaY < chromaHeight; chromaY++)
        {
            var sourceY = chromaY * TSubsample.Y;
            var sourceHeight = Math.Min(TSubsample.Y, source.Height - sourceY);
            var chromaRowIndex = chromaY * chromaWidth;
            for (var chromaX = 0; chromaX < chromaWidth; chromaX++)
            {
                var sourceX = chromaX * TSubsample.X;
                var sourceWidth = Math.Min(TSubsample.X, source.Width - sourceX);
                var uTotal = 0f;
                var vTotal = 0f;
                var sampleCount = 0;
                for (var y = 0; y < sourceHeight; y++)
                {
                    var sourceRow = source.GetRowSpan(sourceY + y);
                    for (var x = 0; x < sourceWidth; x++)
                    {
                        RgbaToYuv(TPixel.ToRgba32Float(sourceRow[sourceX + x]), out _, out var u, out var v);
                        uTotal += u;
                        vTotal += v;
                        sampleCount++;
                    }
                }

                var first = TOrder.VFirst
                    ? ChromaToYuvSample(vTotal / sampleCount, TSample.BitsPerSample)
                    : ChromaToYuvSample(uTotal / sampleCount, TSample.BitsPerSample);
                var second = TOrder.VFirst
                    ? ChromaToYuvSample(uTotal / sampleCount, TSample.BitsPerSample)
                    : ChromaToYuvSample(vTotal / sampleCount, TSample.BitsPerSample);
                var chromaIndex = chromaRowIndex + chromaX;
                var firstChromaIndex = TPlane.Biplanar
                    ? chromaIndex * 2 * TSample.BytesPerSample
                    : chromaIndex * TSample.BytesPerSample;
                TSample.Write(firstChromaPlane[firstChromaIndex..], first);
                TSample.Write(secondChromaPlane[firstChromaIndex..], second);
            }
        }
    }

    private interface IPlanarYuvSampleTransfer
    {
        static abstract int BitsPerSample { get; }

        static abstract int BytesPerSample { get; }

        static abstract uint Read(ReadOnlySpan<byte> source);

        static abstract void Write(Span<byte> destination, uint sample);
    }

    private interface IPlanarYuvPlaneTransfer
    {
        static abstract bool Biplanar { get; }
    }

    private interface IPlanarYuvOrderTransfer
    {
        static abstract bool VFirst { get; }
    }

    private interface IPlanarYuvSubsampleTransfer
    {
        static abstract int X { get; }

        static abstract int Y { get; }
    }

    private readonly struct Sample8Transfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 8;
        public static int BytesPerSample => 1;

        public static uint Read(ReadOnlySpan<byte> source) => source[0];

        public static void Write(Span<byte> destination, uint sample) => destination[0] = (byte)sample;
    }

    private readonly struct Sample10MsbTransfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 10;
        public static int BytesPerSample => 2;

        public static uint Read(ReadOnlySpan<byte> source) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) >> 6;

        public static void Write(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)(sample << 6)));
    }

    private readonly struct Sample10LsbTransfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 10;
        public static int BytesPerSample => 2;

        public static uint Read(ReadOnlySpan<byte> source) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) & 0x03ff;

        public static void Write(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)sample));
    }

    private readonly struct Sample12MsbTransfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 12;
        public static int BytesPerSample => 2;

        public static uint Read(ReadOnlySpan<byte> source) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) >> 4;

        public static void Write(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)(sample << 4)));
    }

    private readonly struct Sample12LsbTransfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 12;
        public static int BytesPerSample => 2;

        public static uint Read(ReadOnlySpan<byte> source) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) & 0x0fff;

        public static void Write(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)sample));
    }

    private readonly struct Sample14MsbTransfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 14;
        public static int BytesPerSample => 2;

        public static uint Read(ReadOnlySpan<byte> source) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(source) >> 2;

        public static void Write(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)(sample << 2)));
    }

    private readonly struct Sample16Transfer : IPlanarYuvSampleTransfer
    {
        public static int BitsPerSample => 16;
        public static int BytesPerSample => 2;

        public static uint Read(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);

        public static void Write(Span<byte> destination, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)sample));
    }

    private readonly struct ThreePlaneTransfer : IPlanarYuvPlaneTransfer
    {
        public static bool Biplanar => false;
    }

    private readonly struct BiplanarTransfer : IPlanarYuvPlaneTransfer
    {
        public static bool Biplanar => true;
    }

    private readonly struct UvTransfer : IPlanarYuvOrderTransfer
    {
        public static bool VFirst => false;
    }

    private readonly struct VuTransfer : IPlanarYuvOrderTransfer
    {
        public static bool VFirst => true;
    }

    private readonly struct Subsample444Transfer : IPlanarYuvSubsampleTransfer
    {
        public static int X => 1;
        public static int Y => 1;
    }

    private readonly struct Subsample422Transfer : IPlanarYuvSubsampleTransfer
    {
        public static int X => 2;
        public static int Y => 1;
    }

    private readonly struct Subsample420Transfer : IPlanarYuvSubsampleTransfer
    {
        public static int X => 2;
        public static int Y => 2;
    }

    private readonly struct Subsample412Transfer : IPlanarYuvSubsampleTransfer
    {
        public static int X => 1;
        public static int Y => 2;
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount64(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded YUV texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount64(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded YUV texture.", nameof(destination));
        }
    }

    private void ValidateRowPitch(int width, int rowPitch)
    {
        if (rowPitch != GetDefaultPitch(width))
        {
            throw new NotSupportedException("Planar YUV formats do not support row pitch override.");
        }
    }

    private long GetPlanarYuvByteCount(int width, int height)
    {
        var bytesPerSample = GetBytesPerSample(_layout.BitsPerSample);
        var lumaByteCount = checked((long)width * height * bytesPerSample);
        var chromaWidth = (width + _layout.ChromaSubsampleX - 1L) / _layout.ChromaSubsampleX;
        var chromaHeight = (height + _layout.ChromaSubsampleY - 1L) / _layout.ChromaSubsampleY;
        var chromaByteCount = checked(chromaWidth * chromaHeight * bytesPerSample);
        return checked(lumaByteCount + (2 * chromaByteCount));
    }

    private static bool TryGetLayout(TextureFormat format, out PlanarYuvLayout layout)
    {
        if (format == TextureFormats.Yuv3P444UNorm) { layout = new PlanarYuvLayout(8, 1, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv10Msb3P444UNorm) { layout = new PlanarYuvLayout(10, 1, 1, Biplanar: false, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv10Lsb3P444UNorm) { layout = new PlanarYuvLayout(10, 1, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv12Msb3P444UNorm) { layout = new PlanarYuvLayout(12, 1, 1, Biplanar: false, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv12Lsb3P444UNorm) { layout = new PlanarYuvLayout(12, 1, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv16_3P444UNorm) { layout = new PlanarYuvLayout(16, 1, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.V408UNorm) { layout = new PlanarYuvLayout(8, 1, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv3P422UNorm) { layout = new PlanarYuvLayout(8, 2, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv10Msb3P422UNorm) { layout = new PlanarYuvLayout(10, 2, 1, Biplanar: false, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv10Lsb3P422UNorm) { layout = new PlanarYuvLayout(10, 2, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv12Msb3P422UNorm) { layout = new PlanarYuvLayout(12, 2, 1, Biplanar: false, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv12Lsb3P422UNorm) { layout = new PlanarYuvLayout(12, 2, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv16_3P422UNorm) { layout = new PlanarYuvLayout(16, 2, 1, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv3P420UNorm) { layout = new PlanarYuvLayout(8, 2, 2, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv10Msb3P420UNorm) { layout = new PlanarYuvLayout(10, 2, 2, Biplanar: false, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv10Lsb3P420UNorm) { layout = new PlanarYuvLayout(10, 2, 2, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv12Msb3P420UNorm) { layout = new PlanarYuvLayout(12, 2, 2, Biplanar: false, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv12Lsb3P420UNorm) { layout = new PlanarYuvLayout(12, 2, 2, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv16_3P420UNorm) { layout = new PlanarYuvLayout(16, 2, 2, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu3P420UNorm) { layout = new PlanarYuvLayout(8, 2, 2, Biplanar: false, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv2P422UNorm) { layout = new PlanarYuvLayout(8, 2, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv10Msb2P422UNorm) { layout = new PlanarYuvLayout(10, 2, 1, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv10Lsb2P422UNorm) { layout = new PlanarYuvLayout(10, 2, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv12Msb2P422UNorm) { layout = new PlanarYuvLayout(12, 2, 1, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv12Lsb2P422UNorm) { layout = new PlanarYuvLayout(12, 2, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv16_2P422UNorm) { layout = new PlanarYuvLayout(16, 2, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv2P420UNorm) { layout = new PlanarYuvLayout(8, 2, 2, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv10Msb2P420UNorm) { layout = new PlanarYuvLayout(10, 2, 2, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv10Lsb2P420UNorm) { layout = new PlanarYuvLayout(10, 2, 2, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv12Msb2P420UNorm) { layout = new PlanarYuvLayout(12, 2, 2, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv12Lsb2P420UNorm) { layout = new PlanarYuvLayout(12, 2, 2, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv16_2P420UNorm) { layout = new PlanarYuvLayout(16, 2, 2, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv2P444UNorm) { layout = new PlanarYuvLayout(8, 1, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu2P444UNorm) { layout = new PlanarYuvLayout(8, 1, 1, Biplanar: true, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv10Msb2P444UNorm) { layout = new PlanarYuvLayout(10, 1, 1, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv10Lsb2P444UNorm) { layout = new PlanarYuvLayout(10, 1, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu10Msb2P444UNorm) { layout = new PlanarYuvLayout(10, 1, 1, Biplanar: true, VFirst: true, MsbAligned: true); return true; }
        if (format == TextureFormats.Yvu10Lsb2P444UNorm) { layout = new PlanarYuvLayout(10, 1, 1, Biplanar: true, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv12Msb2P444UNorm) { layout = new PlanarYuvLayout(12, 1, 1, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv16_2P444UNorm) { layout = new PlanarYuvLayout(16, 1, 1, Biplanar: true, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu2P422UNorm) { layout = new PlanarYuvLayout(8, 2, 1, Biplanar: true, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu10Msb2P422UNorm) { layout = new PlanarYuvLayout(10, 2, 1, Biplanar: true, VFirst: true, MsbAligned: true); return true; }
        if (format == TextureFormats.Yvu10Lsb2P422UNorm) { layout = new PlanarYuvLayout(10, 2, 1, Biplanar: true, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu2P420UNorm) { layout = new PlanarYuvLayout(8, 2, 2, Biplanar: true, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.Yvu10Msb2P420UNorm) { layout = new PlanarYuvLayout(10, 2, 2, Biplanar: true, VFirst: true, MsbAligned: true); return true; }
        if (format == TextureFormats.Yvu10Lsb2P420UNorm) { layout = new PlanarYuvLayout(10, 2, 2, Biplanar: true, VFirst: true, MsbAligned: false); return true; }
        if (format == TextureFormats.V208UNorm) { layout = new PlanarYuvLayout(8, 1, 2, Biplanar: false, VFirst: false, MsbAligned: false); return true; }
        if (format == TextureFormats.Yuv14Msb2P420UNorm) { layout = new PlanarYuvLayout(14, 2, 2, Biplanar: true, VFirst: false, MsbAligned: true); return true; }
        if (format == TextureFormats.Yuv14Msb2P422UNorm) { layout = new PlanarYuvLayout(14, 2, 1, Biplanar: true, VFirst: false, MsbAligned: true); return true; }

        layout = default;
        return false;
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

    private static int GetBytesPerSample(int bitsPerSample) => bitsPerSample <= 8 ? 1 : 2;

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
        new($"Planar YUV texture coder does not support texture format '{format.Name}'.");

    private readonly record struct PlanarYuvLayout(
        int BitsPerSample,
        int ChromaSubsampleX,
        int ChromaSubsampleY,
        bool Biplanar,
        bool VFirst,
        bool MsbAligned);
}
