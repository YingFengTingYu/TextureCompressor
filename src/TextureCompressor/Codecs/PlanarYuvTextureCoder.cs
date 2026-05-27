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
        var bytesPerSample = GetBytesPerSample(_layout.BitsPerSample);
        var lumaSampleCount = checked(destination.Width * destination.Height);
        var lumaByteCount = checked(lumaSampleCount * bytesPerSample);
        var chromaWidth = checked((destination.Width + _layout.ChromaSubsampleX - 1) / _layout.ChromaSubsampleX);
        var chromaHeight = checked((destination.Height + _layout.ChromaSubsampleY - 1) / _layout.ChromaSubsampleY);
        var chromaSampleCount = checked(chromaWidth * chromaHeight);
        var chromaPlaneByteCount = checked(chromaSampleCount * bytesPerSample);

        var luma = source[..lumaByteCount];
        var chroma = source[lumaByteCount..];
        ReadOnlySpan<byte> firstChromaPlane;
        ReadOnlySpan<byte> secondChromaPlane;
        if (_layout.Biplanar)
        {
            firstChromaPlane = chroma;
            secondChromaPlane = chroma[bytesPerSample..];
        }
        else
        {
            firstChromaPlane = chroma[..chromaPlaneByteCount];
            secondChromaPlane = chroma.Slice(chromaPlaneByteCount, chromaPlaneByteCount);
        }

        var lumaRowOffset = 0;
        var lumaRowByteCount = checked(destination.Width * bytesPerSample);
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var chromaY = y / _layout.ChromaSubsampleY;
            var chromaRowIndex = chromaY * chromaWidth;
            var lumaOffset = lumaRowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var chromaX = x / _layout.ChromaSubsampleX;
                var chromaIndex = chromaRowIndex + chromaX;
                var firstChromaIndex = _layout.Biplanar
                    ? chromaIndex * 2 * bytesPerSample
                    : chromaIndex * bytesPerSample;
                var first = ReadYuvSample(firstChromaPlane[firstChromaIndex..], _layout.BitsPerSample, _layout.MsbAligned);
                var second = ReadYuvSample(secondChromaPlane[firstChromaIndex..], _layout.BitsPerSample, _layout.MsbAligned);
                var u = _layout.VFirst ? second : first;
                var v = _layout.VFirst ? first : second;
                var ySample = ReadYuvSample(luma[lumaOffset..], _layout.BitsPerSample, _layout.MsbAligned);
                destinationRow[x] = TPixel.FromRgba32Float(YuvToRgba32Float(ySample, u, v, _layout.BitsPerSample));
                lumaOffset = checked(lumaOffset + bytesPerSample);
            }

            lumaRowOffset = checked(lumaRowOffset + lumaRowByteCount);
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        var bytesPerSample = GetBytesPerSample(_layout.BitsPerSample);
        var texelCount = checked(source.Width * source.Height);
        var lumaByteCount = checked(texelCount * bytesPerSample);
        var chromaWidth = checked((source.Width + _layout.ChromaSubsampleX - 1) / _layout.ChromaSubsampleX);
        var chromaHeight = checked((source.Height + _layout.ChromaSubsampleY - 1) / _layout.ChromaSubsampleY);
        var chromaSampleCount = checked(chromaWidth * chromaHeight);
        var chromaPlaneByteCount = checked(chromaSampleCount * bytesPerSample);

        var lumaOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                RgbaToYuv(TPixel.ToRgba32Float(sourceRow[x]), out var yValue, out _, out _);
                WriteYuvSample(destination[lumaOffset..], UnitToYuvSample(yValue, _layout.BitsPerSample), _layout.BitsPerSample, _layout.MsbAligned);
                lumaOffset = checked(lumaOffset + bytesPerSample);
            }
        }

        var chroma = destination[lumaByteCount..];
        var firstChromaPlane = _layout.Biplanar ? chroma : chroma[..chromaPlaneByteCount];
        var secondChromaPlane = _layout.Biplanar ? chroma[bytesPerSample..] : chroma.Slice(chromaPlaneByteCount, chromaPlaneByteCount);
        for (var chromaY = 0; chromaY < chromaHeight; chromaY++)
        {
            var sourceY = chromaY * _layout.ChromaSubsampleY;
            var sourceHeight = Math.Min(_layout.ChromaSubsampleY, source.Height - sourceY);
            var chromaRowIndex = chromaY * chromaWidth;
            for (var chromaX = 0; chromaX < chromaWidth; chromaX++)
            {
                var sourceX = chromaX * _layout.ChromaSubsampleX;
                var sourceWidth = Math.Min(_layout.ChromaSubsampleX, source.Width - sourceX);
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

                var first = _layout.VFirst
                    ? ChromaToYuvSample(vTotal / sampleCount, _layout.BitsPerSample)
                    : ChromaToYuvSample(uTotal / sampleCount, _layout.BitsPerSample);
                var second = _layout.VFirst
                    ? ChromaToYuvSample(uTotal / sampleCount, _layout.BitsPerSample)
                    : ChromaToYuvSample(vTotal / sampleCount, _layout.BitsPerSample);
                var chromaIndex = chromaRowIndex + chromaX;
                var firstChromaIndex = _layout.Biplanar
                    ? chromaIndex * 2 * bytesPerSample
                    : chromaIndex * bytesPerSample;
                WriteYuvSample(firstChromaPlane[firstChromaIndex..], first, _layout.BitsPerSample, _layout.MsbAligned);
                WriteYuvSample(secondChromaPlane[firstChromaIndex..], second, _layout.BitsPerSample, _layout.MsbAligned);
            }
        }
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
