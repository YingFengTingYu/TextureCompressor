using System.Text;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats;

namespace TextureCompressor.FileFormats.Hdr.Tests;

public sealed class HdrCodecTests
{
    [Fact]
    public void DecodeFlatHdrReturnsFloatPixels()
    {
        var hdr = CreateHdr(
            width: 2,
            height: 1,
            [
                128, 0, 0, 129,
                0, 128, 0, 128
            ]);

        var bitmap = HdrCodec.Decode(hdr);

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
        AssertPixelNear(new Rgba32Float(1f, 0f, 0f, 1f), bitmap.PixelSpan[0]);
        AssertPixelNear(new Rgba32Float(0f, 0.5f, 0f, 1f), bitmap.PixelSpan[1]);
    }

    [Fact]
    public void EncodeThenDecodePreservesHdrPixels()
    {
        var pixels = Enumerable.Range(0, 8)
            .Select(i => new Rgba32Float(i + 1, (i + 1) * 0.5f, (i + 1) * 0.25f, 1f))
            .ToArray();
        var source = new ArrayBitmap<Rgba32Float>(8, 1, pixels);

        var hdr = HdrCodec.Encode(source);
        var decoded = HdrCodec.Decode(hdr);

        Assert.Equal([2, 2, 0, 8], FindFirstScanline(hdr).ToArray());
        for (var i = 0; i < pixels.Length; i++)
        {
            AssertPixelNear(pixels[i], decoded.PixelSpan[i], tolerance: 0.04f);
        }
    }

    [Fact]
    public void EncodeWithRleDisabledWritesFlatPixels()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            8,
            1,
            Enumerable.Repeat(new Rgba32Float(1f, 0f, 0f, 1f), 8).ToArray());

        var hdr = HdrCodec.Encode(source, new HdrEncodingOptions { UseRunLengthEncoding = false });
        var firstPixel = FindFirstScanline(hdr);

        Assert.Equal([128, 0, 0, 129], firstPixel.ToArray());
        AssertPixelNear(source.PixelSpan[0], HdrCodec.Decode(hdr).PixelSpan[0]);
    }

    [Fact]
    public void DecodeRleScanline()
    {
        var hdr = CreateHdrWithRleScanline();

        var bitmap = HdrCodec.Decode(hdr);

        Assert.Equal(8, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
        AssertPixelNear(new Rgba32Float(1f, 0f, 0f, 1f), bitmap.PixelSpan[0]);
        AssertPixelNear(new Rgba32Float(1f, 1f, 0f, 1f), bitmap.PixelSpan[4]);
    }

    [Fact]
    public void DecodeRgba8ClampsHdrValues()
    {
        var hdr = CreateHdr(
            width: 1,
            height: 1,
            [
                128, 64, 32, 130
            ]);

        var bitmap = HdrCodec.DecodeRgba8(hdr);

        Assert.Equal(new Rgba8UNorm(255, 255, 128, 255), bitmap.PixelSpan[0]);
    }

    [Fact]
    public void RegisteredFileFormatRoundTripsHdr()
    {
        var format = new HdrFileFormat();
        var source = new ArrayBitmap<Rgba32Float>(1, 1, [new Rgba32Float(2f, 1f, 0.5f, 1f)]);
        using var stream = new MemoryStream();

        format.WriteImage(source, stream);
        stream.Position = 0;
        var decoded = format.ReadImage<Rgba32Float>(stream);

        Assert.True(format.CanRead(stream.ToArray(), ".hdr"));
        AssertPixelNear(source.PixelSpan[0], decoded.PixelSpan[0]);
    }

    [Fact]
    public void RejectsUnsupportedPixelFormat()
    {
        var data = Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_xyze\n\n-Y 1 +X 1\n");

        Assert.Throws<NotSupportedException>(() => HdrCodec.Decode(data));
    }

    private static byte[] CreateHdr(int width, int height, byte[] payload)
    {
        using var stream = new MemoryStream();
        WriteAscii(stream, $"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y {height} +X {width}\n");
        stream.Write(payload);
        return stream.ToArray();
    }

    private static byte[] CreateHdrWithRleScanline()
    {
        using var stream = new MemoryStream();
        WriteAscii(stream, "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 1 +X 8\n");
        stream.Write([2, 2, 0, 8]);
        stream.Write([136, 128]);
        stream.Write([8, 0, 32, 64, 96, 128, 160, 192, 224]);
        stream.Write([136, 0]);
        stream.Write([136, 129]);
        return stream.ToArray();
    }

    private static ReadOnlySpan<byte> FindFirstScanline(byte[] hdr)
    {
        var newlineCount = 0;
        for (var i = 0; i < hdr.Length; i++)
        {
            if (hdr[i] != '\n')
            {
                continue;
            }

            newlineCount++;
            if (newlineCount == 4)
            {
                return hdr.AsSpan(i + 1, 4);
            }
        }

        throw new InvalidDataException("Missing HDR scanline.");
    }

    private static void WriteAscii(Stream stream, string value) =>
        stream.Write(Encoding.ASCII.GetBytes(value));

    private static void AssertPixelNear(Rgba32Float expected, Rgba32Float actual, float tolerance = 0.0001f)
    {
        Assert.InRange(actual.Red, expected.Red - tolerance, expected.Red + tolerance);
        Assert.InRange(actual.Green, expected.Green - tolerance, expected.Green + tolerance);
        Assert.InRange(actual.Blue, expected.Blue - tolerance, expected.Blue + tolerance);
        Assert.InRange(actual.Alpha, expected.Alpha - tolerance, expected.Alpha + tolerance);
    }
}
