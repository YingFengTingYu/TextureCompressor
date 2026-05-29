using System.Buffers.Binary;
using System.IO.Compression;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Png.Tests;

public sealed class PngCodecTests
{
    [Fact]
    public void DecodeRgb8PngReturnsRgbaPixels()
    {
        var png = CreatePng(
            width: 2,
            height: 1,
            bitDepth: 8,
            colorType: PngColorType.Truecolor,
            filteredScanlines: [0, 10, 20, 30, 40, 50, 60]);

        var bitmap = PngCodec.Decode(png);

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
        Assert.Equal(new Rgba8UNorm(10, 20, 30, 255), bitmap.PixelSpan[0]);
        Assert.Equal(new Rgba8UNorm(40, 50, 60, 255), bitmap.PixelSpan[1]);
    }

    [Fact]
    public void DecodeIndexedPngAppliesPaletteTransparency()
    {
        var png = CreatePng(
            width: 4,
            height: 1,
            bitDepth: 2,
            colorType: PngColorType.IndexedColor,
            filteredScanlines: [0, 0b00011011],
            palette: [255, 0, 0, 0, 255, 0, 0, 0, 255, 8, 9, 10],
            transparency: [255, 128, 0]);

        var bitmap = PngCodec.Decode(png);

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), bitmap.PixelSpan[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 128), bitmap.PixelSpan[1]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255, 0), bitmap.PixelSpan[2]);
        Assert.Equal(new Rgba8UNorm(8, 9, 10, 255), bitmap.PixelSpan[3]);
    }

    [Fact]
    public void EncodeRgba8WritesDecodablePng()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8),
                new Rgba8UNorm(9, 10, 11, 12),
                new Rgba8UNorm(13, 14, 15, 16)
            ]);

        var png = PngCodec.Encode(source);
        var decoded = PngCodec.Decode(png);

        AssertPngHeader(png, 2, 2, bitDepth: 8, PngColorType.TruecolorAlpha);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeRgba16Writes16BitPng()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0x8080, 0, ushort.MaxValue)]);

        var png = PngCodec.Encode(source);
        var decoded = PngCodec.Decode(png);

        AssertPngHeader(png, 1, 1, bitDepth: 16, PngColorType.TruecolorAlpha);
        Assert.Equal(new Rgba8UNorm(255, 128, 0, 255), decoded.PixelSpan[0]);
    }

    [Theory]
    [InlineData("normalized/fine-detail-512.png", 512, 512)]
    [InlineData("normalized/gradients-512.png", 512, 512)]
    [InlineData("normalized/hard-edges-512.png", 512, 512)]
    [InlineData("normalized/natural-scene-512.png", 512, 512)]
    [InlineData("source/fine-detail-source.png", 1254, 1254)]
    [InlineData("source/gradients-source.png", 1254, 1254)]
    [InlineData("source/hard-edges-source.png", 1254, 1254)]
    [InlineData("source/natural-scene-source.png", 1254, 1254)]
    public void DecodeRepositoryFixtures(string fileName, int width, int height)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../fixtures/images",
            fileName));

        var bitmap = PngCodec.Decode(path);

        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
        Assert.Equal(width * height, bitmap.PixelSpan.Length);
        Assert.Equal(byte.MaxValue, bitmap.PixelSpan[0].Alpha);
    }

    private static void AssertPngHeader(byte[] png, int width, int height, byte bitDepth, PngColorType colorType)
    {
        Assert.Equal([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], png.AsSpan(0, 8).ToArray());
        Assert.Equal("IHDR", ToAscii(png.AsSpan(12, 4)));
        Assert.Equal(width, checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4))));
        Assert.Equal(height, checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4))));
        Assert.Equal(bitDepth, png[24]);
        Assert.Equal((byte)colorType, png[25]);
    }

    private static byte[] CreatePng(
        int width,
        int height,
        byte bitDepth,
        PngColorType colorType,
        byte[] filteredScanlines,
        byte[]? palette = null,
        byte[]? transparency = null)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header[..4], checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(4, 4), checked((uint)height));
        header[8] = bitDepth;
        header[9] = (byte)colorType;
        WriteChunk(stream, "IHDR", header);

        if (palette is not null)
        {
            WriteChunk(stream, "PLTE", palette);
        }

        if (transparency is not null)
        {
            WriteChunk(stream, "tRNS", transparency);
        }

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(filteredScanlines);
        }

        WriteChunk(stream, "IDAT", idat.ToArray());
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        typeBytes[0] = (byte)type[0];
        typeBytes[1] = (byte)type[1];
        typeBytes[2] = (byte)type[2];
        typeBytes[3] = (byte)type[3];
        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc(typeBytes, data));
        stream.Write(crc);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc;
    }

    private static string ToAscii(ReadOnlySpan<byte> bytes) =>
        string.Create(bytes.Length, bytes.ToArray(), static (chars, source) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)source[i];
            }
        });
}
