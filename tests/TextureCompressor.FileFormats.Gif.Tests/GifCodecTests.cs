using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Gif.Tests;

public sealed class GifCodecTests
{
    [Fact]
    public void EncodeRgba8WritesDecodableGif()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(255, 0, 0),
                new Rgba8UNorm(0, 255, 0),
                new Rgba8UNorm(0, 0, 255),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var gif = GifCodec.Encode(source);
        var decoded = GifCodec.Decode(gif);

        Assert.Equal("GIF89a"u8.ToArray(), gif.AsSpan(0, 6).ToArray());
        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeTransparentPixelWritesGraphicsControlExtension()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(10, 20, 30, 255),
                new Rgba8UNorm(100, 110, 120, 0)
            ]);

        var gif = GifCodec.Encode(source);
        var decoded = GifCodec.Decode(gif);

        Assert.Contains(gif, value => value == 0xf9);
        Assert.Equal(new Rgba8UNorm(10, 20, 30), decoded.PixelSpan[0]);
        Assert.Equal(0, decoded.PixelSpan[1].Alpha);
    }

    [Fact]
    public void DecodeReturnsFirstFrameOfAnimatedGif()
    {
        var first = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(255, 0, 0)]);
        var second = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(0, 0, 255)]);
        var animated = AppendSecondFrame(GifCodec.Encode(first), GifCodec.Encode(second));

        var decoded = GifCodec.Decode(animated);

        Assert.Equal(1, decoded.Width);
        Assert.Equal(1, decoded.Height);
        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.PixelSpan[0]);
    }

    [Fact]
    public void DecodeInterlacedGifRestoresRows()
    {
        var gif = CreateInterlacedGif();

        var decoded = GifCodec.Decode(gif);

        Assert.Equal(2, decoded.Width);
        Assert.Equal(4, decoded.Height);
        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.PixelSpan[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0), decoded.PixelSpan[2]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255), decoded.PixelSpan[4]);
        Assert.Equal(new Rgba8UNorm(255, 255, 255), decoded.PixelSpan[6]);
    }

    private static byte[] AppendSecondFrame(byte[] first, byte[] second)
    {
        var secondImageOffset = Array.IndexOf(second, (byte)0x2c);
        Assert.True(secondImageOffset > 0);

        using var stream = new MemoryStream();
        stream.Write(first.AsSpan(0, first.Length - 1));
        stream.Write(second.AsSpan(secondImageOffset, second.Length - secondImageOffset - 1));
        stream.WriteByte(0x3b);
        return stream.ToArray();
    }

    private static byte[] CreateInterlacedGif()
    {
        var palette = new[]
        {
            new Rgba8UNorm(255, 0, 0),
            new Rgba8UNorm(0, 255, 0),
            new Rgba8UNorm(0, 0, 255),
            new Rgba8UNorm(255, 255, 255)
        };

        var interlacedOrderIndices = new byte[]
        {
            0, 0,
            2, 2,
            1, 1,
            3, 3
        };

        using var stream = new MemoryStream();
        stream.Write("GIF89a"u8);
        WriteUInt16(stream, 2);
        WriteUInt16(stream, 4);
        stream.WriteByte(0x81);
        stream.WriteByte(0);
        stream.WriteByte(0);
        foreach (var color in palette)
        {
            stream.WriteByte(color.Red);
            stream.WriteByte(color.Green);
            stream.WriteByte(color.Blue);
        }

        stream.WriteByte(0x2c);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 2);
        WriteUInt16(stream, 4);
        stream.WriteByte(0x40);
        stream.WriteByte(2);
        WriteSubBlocks(stream, EncodeLzw(interlacedOrderIndices, 2));
        stream.WriteByte(0x3b);
        return stream.ToArray();
    }

    private static byte[] EncodeLzw(byte[] indices, int minimumCodeSize)
    {
        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;
        var writer = new LsbBitWriter();
        var codeSize = minimumCodeSize + 1;
        foreach (var index in indices)
        {
            writer.WriteBits(clearCode, codeSize);
            writer.WriteBits(index, codeSize);
        }

        writer.WriteBits(endCode, codeSize);
        return writer.ToArray();
    }

    private static void WriteSubBlocks(Stream stream, ReadOnlySpan<byte> data)
    {
        stream.WriteByte((byte)data.Length);
        stream.Write(data);
        stream.WriteByte(0);
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
    }

    private sealed class LsbBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _currentByte;
        private int _bitCount;

        public void WriteBits(int value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _currentByte |= ((value >> i) & 1) << _bitCount;
                _bitCount++;
                if (_bitCount == 8)
                {
                    _bytes.Add((byte)_currentByte);
                    _currentByte = 0;
                    _bitCount = 0;
                }
            }
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                _bytes.Add((byte)_currentByte);
            }

            return _bytes.ToArray();
        }
    }
}
