using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class FxtcTextureCoderTests
{
    public static IEnumerable<object[]> Fxt1Formats()
    {
        foreach (var format in FxtcTextureCoder.SupportedFormats.ToArray())
        {
            yield return new object[] { format };
        }
    }

    [Theory]
    [MemberData(nameof(Fxt1Formats))]
    public void GlobalManagerFindsFxtcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(FxtcTextureCoder.IsSupported(format));
        Assert.IsType<FxtcTextureCoder>(coder);
    }

    [Fact]
    public void DecodeCcHiDecodesSevenColorPalette()
    {
        Span<byte> block = stackalloc byte[16];
        WriteBits(block, 0, 0, 3);
        WriteBits(block, 3, 3, 3);
        WriteBits(block, 6, 6, 3);
        WriteBits(block, 9, 7, 3);
        WriteBits(block, 96, PackRgb555(255, 0, 0), 15);
        WriteBits(block, 111, PackRgb555(0, 0, 255), 15);
        var decoded = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var coder = new FxtcTextureCoder(TextureFormats.RgbaFxt1UNorm);

        coder.Decode(block, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(128, 0, 128), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255), decoded.Pixels[2]);
        Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), decoded.Pixels[3]);
    }

    [Fact]
    public void DecodeRgbFxt1ForcesTransparentCcHiIndexOpaque()
    {
        Span<byte> block = stackalloc byte[16];
        WriteBits(block, 0, 7, 3);
        var decoded = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var coder = new FxtcTextureCoder(TextureFormats.RgbFxt1UNorm);

        coder.Decode(block, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[0]);
    }

    [Fact]
    public void DecodeCcChromaDecodesFourExplicitColors()
    {
        Span<byte> block = stackalloc byte[16];
        WriteBits(block, 0, 0, 2);
        WriteBits(block, 2, 1, 2);
        WriteBits(block, 4, 2, 2);
        WriteBits(block, 6, 3, 2);
        WriteBits(block, 64, PackRgb555(255, 0, 0), 15);
        WriteBits(block, 79, PackRgb555(0, 255, 0), 15);
        WriteBits(block, 94, PackRgb555(0, 0, 255), 15);
        WriteBits(block, 109, PackRgb555(255, 255, 255), 15);
        WriteBits(block, 125, 0b010, 3);
        var decoded = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var coder = new FxtcTextureCoder(TextureFormats.RgbaFxt1UNorm);

        coder.Decode(block, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255), decoded.Pixels[2]);
        Assert.Equal(new Rgba8UNorm(255, 255, 255), decoded.Pixels[3]);
    }

    [Fact]
    public void DecodeCcMixedOpaqueModeDecodesTwoFourColorPalettes()
    {
        Span<byte> block = stackalloc byte[16];
        WriteBits(block, 0, 0, 2);
        WriteBits(block, 2, 1, 2);
        WriteBits(block, 4, 2, 2);
        WriteBits(block, 6, 3, 2);
        WriteBits(block, 32, 0, 2);
        WriteBits(block, 34, 3, 2);
        WriteBits(block, 64, PackRgb565WithoutGreenLowBit(255, 0, 0), 15);
        WriteBits(block, 79, PackRgb565WithoutGreenLowBit(0, 0, 255), 15);
        WriteBits(block, 94, PackRgb565WithoutGreenLowBit(0, 255, 0), 15);
        WriteBits(block, 109, PackRgb565WithoutGreenLowBit(255, 255, 255), 15);
        WriteBits(block, 125, 0, 1);
        WriteBits(block, 126, 1, 1);
        WriteBits(block, 127, 1, 1);
        var decoded = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var coder = new FxtcTextureCoder(TextureFormats.RgbaFxt1UNorm);

        coder.Decode(block, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(170, 0, 85), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(85, 0, 170), decoded.Pixels[2]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0), decoded.Pixels[4]);
        Assert.Equal(new Rgba8UNorm(255, 255, 255), decoded.Pixels[5]);
    }

    [Fact]
    public void DecodeCcMixedAlphaModeDecodesHalfBlocks()
    {
        Span<byte> block = stackalloc byte[16];
        WriteBits(block, 0, 0, 2);
        WriteBits(block, 2, 1, 2);
        WriteBits(block, 4, 2, 2);
        WriteBits(block, 6, 3, 2);
        WriteBits(block, 32, 0, 2);
        WriteBits(block, 34, 2, 2);
        WriteBits(block, 64, PackRgb555(255, 0, 0), 15);
        WriteBits(block, 79, PackRgb565WithoutGreenLowBit(0, 0, 255), 15);
        WriteBits(block, 94, PackRgb555(0, 255, 0), 15);
        WriteBits(block, 109, PackRgb565WithoutGreenLowBit(255, 255, 255), 15);
        WriteBits(block, 124, 1, 1);
        WriteBits(block, 125, 0, 1);
        WriteBits(block, 126, 1, 1);
        WriteBits(block, 127, 1, 1);
        var decoded = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var coder = new FxtcTextureCoder(TextureFormats.RgbaFxt1UNorm);

        coder.Decode(block, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(128, 0, 128), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255), decoded.Pixels[2]);
        Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0), decoded.Pixels[4]);
        Assert.Equal(new Rgba8UNorm(255, 255, 255), decoded.Pixels[5]);
    }

    [Fact]
    public void DecodeCcAlphaLerpModeDecodesAlphaPalette()
    {
        Span<byte> block = stackalloc byte[16];
        WriteBits(block, 0, 0, 2);
        WriteBits(block, 2, 1, 2);
        WriteBits(block, 4, 2, 2);
        WriteBits(block, 6, 3, 2);
        WriteBits(block, 32, 0, 2);
        WriteBits(block, 34, 3, 2);
        WriteBits(block, 64, PackRgb555(255, 0, 0), 15);
        WriteBits(block, 79, PackRgb555(0, 0, 255), 15);
        WriteBits(block, 94, PackRgb555(0, 255, 0), 15);
        WriteBits(block, 109, 31, 5);
        WriteBits(block, 114, 15, 5);
        WriteBits(block, 119, 0, 5);
        WriteBits(block, 124, 1, 1);
        WriteBits(block, 125, 0b011, 3);
        var decoded = new ArrayBitmap<Rgba8UNorm>(8, 4);
        var coder = new FxtcTextureCoder(TextureFormats.RgbaFxt1UNorm);

        coder.Decode(block, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(170, 0, 85, 211), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(85, 0, 170, 167), decoded.Pixels[2]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255, 123), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 0), decoded.Pixels[4]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255, 123), decoded.Pixels[5]);
    }

    [Theory]
    [MemberData(nameof(Fxt1Formats))]
    public void EncodeAndDecodeWithRowPitchProducesDecodableFxt1(TextureFormat format)
    {
        const int width = 19;
        const int height = 11;
        var source = new ArrayBitmap<Rgba8UNorm>(width, height, CreateGradient(width, height, includeAlpha: format == TextureFormats.RgbaFxt1UNorm));
        var coder = new FxtcTextureCoder(format);
        var rowByteCount = coder.GetDefaultPitch(width);
        var rowPitch = rowByteCount + 16;
        var encoded = new byte[coder.GetEncodedByteCount(width, height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(width, height);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(format.GetByteCount(width, height) + (16 * ((height + format.BlockHeight - 1) / format.BlockHeight)), encoded.Length);
        Assert.True(
            decoded.Pixels.Distinct().Count() > 2,
            $"{format.Name} encoded output collapsed to too few decoded colors.");
        AssertBlockRowPaddingRemainsZero(encoded, format, width, height, rowPitch);
    }

    [Theory]
    [MemberData(nameof(Fxt1Formats))]
    public void EncodeAndDecodeHighQualityProducesDecodableFxt1(TextureFormat format)
    {
        const int width = 19;
        const int height = 11;
        var source = new ArrayBitmap<Rgba8UNorm>(width, height, CreateGradient(width, height, includeAlpha: format == TextureFormats.RgbaFxt1UNorm));
        var options = new FxtcCoderOptions { CompressionMode = FxtcCompressionMode.High };
        var coder = new FxtcTextureCoder(format, options);
        var rowPitch = coder.GetDefaultPitch(width);
        var encoded = new byte[coder.GetEncodedByteCount(width, height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(width, height);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.True(
            decoded.Pixels.Distinct().Count() > 2,
            $"{format.Name} high-quality encoded output collapsed to too few decoded colors.");
    }

    [Fact]
    public void EncodeRgbFxt1HighQualityImprovesGradientError()
    {
        const int width = 8;
        const int height = 4;
        var source = new ArrayBitmap<Rgba8UNorm>(width, height, CreateGradient(width, height, includeAlpha: false));
        var fastDecoded = EncodeDecode(source, new FxtcTextureCoder(TextureFormats.RgbFxt1UNorm));
        var highDecoded = EncodeDecode(
            source,
            new FxtcTextureCoder(
                TextureFormats.RgbFxt1UNorm,
                new FxtcCoderOptions { CompressionMode = FxtcCompressionMode.High }));

        Assert.True(
            RgbSquaredError(source, highDecoded) < RgbSquaredError(source, fastDecoded),
            "High-quality FXT1 should improve this non-trivial gradient block over the fast endpoint heuristic.");
    }

    [Theory]
    [MemberData(nameof(Fxt1Formats))]
    public void EncodeAndDecodeSolidImageRoundTripsExactly(TextureFormat format)
    {
        const int width = 11;
        const int height = 7;
        var expected = new Rgba8UNorm(255, 0, 0, 255);
        var source = new ArrayBitmap<Rgba8UNorm>(width, height, Enumerable.Repeat(expected, width * height).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(width, height);
        var coder = new FxtcTextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(width);
        var encoded = new byte[coder.GetEncodedByteCount(width, height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(expected, pixel));
    }

    private static Rgba8UNorm[] CreateGradient(int width, int height, bool includeAlpha)
    {
        var source = new Rgba8UNorm[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                source[(y * width) + x] = new Rgba8UNorm(
                    (byte)((x * 17 + y * 3) & 0xff),
                    (byte)((y * 19 + x * 5) & 0xff),
                    (byte)((x * 7 + y * 11) & 0xff),
                    includeAlpha ? (byte)(96 + ((x * 3 + y * 5) & 0x7f)) : byte.MaxValue);
            }
        }

        return source;
    }

    private static ArrayBitmap<Rgba8UNorm> EncodeDecode(ArrayBitmap<Rgba8UNorm> source, FxtcTextureCoder coder)
    {
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        return decoded;
    }

    private static long RgbSquaredError(ArrayBitmap<Rgba8UNorm> expected, ArrayBitmap<Rgba8UNorm> actual)
    {
        long error = 0;
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            var red = expected.Pixels[i].Red - actual.Pixels[i].Red;
            var green = expected.Pixels[i].Green - actual.Pixels[i].Green;
            var blue = expected.Pixels[i].Blue - actual.Pixels[i].Blue;
            error += (red * red) + (green * green) + (blue * blue);
        }

        return error;
    }

    private static void AssertBlockRowPaddingRemainsZero(
        ReadOnlySpan<byte> encoded,
        TextureFormat format,
        int width,
        int height,
        int rowPitch)
    {
        var rowByteCount = format.GetRowByteCount(width);
        var blockRows = (height + format.BlockHeight - 1) / format.BlockHeight;
        for (var blockRow = 0; blockRow < blockRows; blockRow++)
        {
            var padding = encoded.Slice((blockRow * rowPitch) + rowByteCount, rowPitch - rowByteCount);
            Assert.True(
                padding.IndexOfAnyExcept((byte)0) < 0,
                $"{format.Name} wrote outside the packed FXT1 row.");
        }
    }

    private static ushort PackRgb555(byte red, byte green, byte blue)
    {
        var r = (red * 31 + 127) / 255;
        var g = (green * 31 + 127) / 255;
        var b = (blue * 31 + 127) / 255;
        return (ushort)((r << 10) | (g << 5) | b);
    }

    private static ushort PackRgb565WithoutGreenLowBit(byte red, byte green, byte blue)
    {
        var r = (red * 31 + 127) / 255;
        var g = ((green * 63 + 127) / 255) >> 1;
        var b = (blue * 31 + 127) / 255;
        return (ushort)((r << 10) | (g << 5) | b);
    }

    private static void WriteBits(Span<byte> destination, int bitOffset, ulong value, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            if ((value & (1UL << i)) == 0)
            {
                continue;
            }

            var destinationBit = bitOffset + i;
            destination[destinationBit >> 3] |= (byte)(1 << (destinationBit & 7));
        }
    }
}
