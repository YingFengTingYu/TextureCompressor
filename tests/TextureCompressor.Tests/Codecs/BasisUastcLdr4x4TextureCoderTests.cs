using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.Tests;

public sealed class BasisUastcLdr4x4TextureCoderTests
{
    [Fact]
    public void FormatIsBasisUastcLdr4x4()
    {
        var coder = new BasisUastcLdr4x4TextureCoder();

        Assert.Equal(TextureFormats.RgbaBasisUastcLdr4x4UNorm, coder.Format);
    }

    [Fact]
    public void GlobalManagerFindsBasisUastcLdr4x4TextureCoder()
    {
        var coder = TextureCompressor.Registry.TextureCoderManager.Global.GetCoder(TextureFormats.RgbaBasisUastcLdr4x4UNorm);

        Assert.True(BasisUastcLdr4x4TextureCoder.IsSupported(TextureFormats.RgbaBasisUastcLdr4x4UNorm));
        Assert.IsType<BasisUastcLdr4x4TextureCoder>(coder);
        Assert.IsAssignableFrom<IPitchTextureCoder>(coder);
    }

    [Theory]
    [InlineData(1, 1, 16)]
    [InlineData(4, 4, 16)]
    [InlineData(5, 4, 32)]
    [InlineData(5, 5, 64)]
    public void GetEncodedByteCountReturnsRawUastcPayloadSize(int width, int height, int expectedByteCount)
    {
        var coder = new BasisUastcLdr4x4TextureCoder();
        var rowPitch = coder.GetDefaultPitch(width);

        Assert.Equal(expectedByteCount, coder.GetEncodedByteCount(width, height, rowPitch));
    }

    [Fact]
    public void DecodeSolidColorBlockFillsBitmap()
    {
        var payload = CreateSolidColorPayload(32, 64, 96, 192);
        var coder = new BasisUastcLdr4x4TextureCoder();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);

        coder.Decode(payload, bitmap.AsView(), coder.GetDefaultPitch(bitmap.Width));

        Assert.All(bitmap.Pixels, pixel =>
        {
            Assert.Equal(32, pixel.Red);
            Assert.Equal(64, pixel.Green);
            Assert.Equal(96, pixel.Blue);
            Assert.Equal(192, pixel.Alpha);
        });
    }

    [Fact]
    public void DecodeMultipleSolidColorBlocksCropsEdges()
    {
        var payload = new byte[4 * BasisUastcLdr4x4TextureCoder.BytesPerBlock];
        CreateSolidColorPayload(255, 0, 0, 255).CopyTo(payload.AsSpan(0 * BasisUastcLdr4x4TextureCoder.BytesPerBlock));
        CreateSolidColorPayload(0, 255, 0, 255).CopyTo(payload.AsSpan(1 * BasisUastcLdr4x4TextureCoder.BytesPerBlock));
        CreateSolidColorPayload(0, 0, 255, 255).CopyTo(payload.AsSpan(2 * BasisUastcLdr4x4TextureCoder.BytesPerBlock));
        CreateSolidColorPayload(255, 255, 0, 255).CopyTo(payload.AsSpan(3 * BasisUastcLdr4x4TextureCoder.BytesPerBlock));
        var coder = new BasisUastcLdr4x4TextureCoder();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(5, 5);

        coder.Decode(payload, bitmap.AsView(), coder.GetDefaultPitch(bitmap.Width));

        AssertPixel(bitmap, 0, 0, new Rgba8UNorm(255, 0, 0));
        AssertPixel(bitmap, 4, 0, new Rgba8UNorm(0, 255, 0));
        AssertPixel(bitmap, 0, 4, new Rgba8UNorm(0, 0, 255));
        AssertPixel(bitmap, 4, 4, new Rgba8UNorm(255, 255, 0));
    }

    [Fact]
    public void DecodeRejectsWrongPayloadSize()
    {
        var coder = new BasisUastcLdr4x4TextureCoder();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);

        Assert.Throws<ArgumentException>(() => coder.Decode(ReadOnlySpan<byte>.Empty, bitmap.AsView(), coder.GetDefaultPitch(bitmap.Width)));
    }

    [Fact]
    public void DecodeReservedModeReturnsSpecErrorColor()
    {
        var payload = new byte[BasisUastcLdr4x4TextureCoder.BytesPerBlock];
        var bitOffset = 0;
        WriteBits(payload, ref bitOffset, 0x45, 7);
        var coder = new BasisUastcLdr4x4TextureCoder();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);

        coder.Decode(payload, bitmap.AsView(), coder.GetDefaultPitch(bitmap.Width));

        Assert.All(bitmap.Pixels, pixel =>
        {
            Assert.Equal(255, pixel.Red);
            Assert.Equal(0, pixel.Green);
            Assert.Equal(255, pixel.Blue);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeSolidColorBlockRoundTripsExactly()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(10, 20, 30, 40), 16).ToArray());
        var coder = new BasisUastcLdr4x4TextureCoder();
        var payload = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(source.Width));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(CreateSolidColorPayload(10, 20, 30, 40), payload);
        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal(10, pixel.Red);
            Assert.Equal(20, pixel.Green);
            Assert.Equal(30, pixel.Blue);
            Assert.Equal(40, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeUsesRowPitchPadding()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(64, 128, 192, 255), 25).ToArray());
        var coder = new BasisUastcLdr4x4TextureCoder();
        var rowPitch = coder.GetDefaultPitch(source.Width) + BasisUastcLdr4x4TextureCoder.BytesPerBlock;
        var payload = Enumerable.Repeat((byte)0xff, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();
        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);

        coder.Encode(source.AsView(), payload, rowPitch);
        coder.Decode(payload, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal(64, pixel.Red);
            Assert.Equal(128, pixel.Green);
            Assert.Equal(192, pixel.Blue);
            Assert.Equal(255, pixel.Alpha);
        });
        Assert.Equal(0xff, payload[coder.GetDefaultPitch(source.Width)]);
    }

    [Fact]
    public void EncodeDecodeLargeSolidTextureRoundTripsExactly()
    {
        const int width = 64;
        const int height = 32;
        var source = new ArrayBitmap<Rgba8UNorm>(
            width,
            height,
            Enumerable.Repeat(new Rgba8UNorm(12, 34, 56, 78), width * height).ToArray());
        var coder = new BasisUastcLdr4x4TextureCoder();
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var payload = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(width, height);

        coder.Encode(source.AsView(), payload, rowPitch);
        coder.Decode(payload, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal(12, pixel.Red);
            Assert.Equal(34, pixel.Green);
            Assert.Equal(56, pixel.Blue);
            Assert.Equal(78, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeNonUniformBlockUsesInterpolatedMode()
    {
        var pixels = new Rgba8UNorm[16];
        Array.Fill(pixels, new Rgba8UNorm(0, 64, 128, 255), 0, 8);
        Array.Fill(pixels, new Rgba8UNorm(255, 192, 128, 255), 8, 8);
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var coder = new BasisUastcLdr4x4TextureCoder();
        var payload = new byte[coder.GetEncodedByteCount(source.Width, source.Height, coder.GetDefaultPitch(source.Width))];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(source.Width));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(0, decoded.Pixels[i].Red);
            Assert.Equal(64, decoded.Pixels[i].Green);
            Assert.Equal(128, decoded.Pixels[i].Blue);
            Assert.Equal(255, decoded.Pixels[i].Alpha);
        }

        for (var i = 8; i < 16; i++)
        {
            Assert.Equal(255, decoded.Pixels[i].Red);
            Assert.Equal(192, decoded.Pixels[i].Green);
            Assert.Equal(128, decoded.Pixels[i].Blue);
            Assert.Equal(255, decoded.Pixels[i].Alpha);
        }
    }

    [Fact]
    public void EncodeRejectsTooSmallDestination()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BasisUastcLdr4x4TextureCoder();

        Assert.Throws<ArgumentException>(() => coder.Encode(source.AsView(), new byte[BasisUastcLdr4x4TextureCoder.BytesPerBlock - 1], coder.GetDefaultPitch(source.Width)));
    }

    private static byte[] CreateSolidColorPayload(byte red, byte green, byte blue, byte alpha)
    {
        var payload = new byte[BasisUastcLdr4x4TextureCoder.BytesPerBlock];
        var bitOffset = 0;
        WriteBits(payload, ref bitOffset, 0x17, 5);
        WriteBits(payload, ref bitOffset, red, 8);
        WriteBits(payload, ref bitOffset, green, 8);
        WriteBits(payload, ref bitOffset, blue, 8);
        WriteBits(payload, ref bitOffset, alpha, 8);
        return payload;
    }

    private static void WriteBits(Span<byte> destination, ref int bitOffset, int value, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            if (((value >> i) & 1) != 0)
            {
                var absoluteBit = bitOffset + i;
                destination[absoluteBit >> 3] |= (byte)(1 << (absoluteBit & 7));
            }
        }

        bitOffset += bitCount;
    }

    private static void AssertPixel(ArrayBitmap<Rgba8UNorm> bitmap, int x, int y, Rgba8UNorm expected)
    {
        var actual = bitmap.Pixels[(y * bitmap.Width) + x];
        Assert.Equal(expected.Red, actual.Red);
        Assert.Equal(expected.Green, actual.Green);
        Assert.Equal(expected.Blue, actual.Blue);
        Assert.Equal(expected.Alpha, actual.Alpha);
    }
}
