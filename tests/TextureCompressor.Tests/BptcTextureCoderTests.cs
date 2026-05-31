using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;
using TextureCompressor.Options;

namespace TextureCompressor.Tests;

public sealed class BptcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(BptcFormats))]
    public void GlobalManagerFindsBptcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(BptcTextureCoder.IsSupported(format));
        Assert.IsType<BptcTextureCoder>(coder);
    }

    [Fact]
    public void ConstructorStoresCompressionOptions()
    {
        var options = new BptcCoderOptions { CompressionMode = TextureCompressionLevel.High };
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm, options);

        Assert.Same(options, coder.Options);
    }

    [Fact]
    public void Bc7InvalidModeDecodesToTransparentRgba8()
    {
        var encoded = new byte[TextureFormats.Bc7UNorm.GetByteCount(4, 4)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), pixel));
    }

    [Fact]
    public void EncodeAndDecodeBc7UNormRoundTripsSolidRgba8()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(34, 101, 202, 77), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(34, pixel.Red, 1);
            AssertClose(101, pixel.Green, 1);
            AssertClose(202, pixel.Blue, 1);
            AssertClose(77, pixel.Alpha, 1);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc7SrgbRoundTripsLinearRgba8ThroughStorageGamma()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(128, 32, 224, 200), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7Srgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(128, pixel.Red, 2);
            AssertClose(32, pixel.Green, 2);
            AssertClose(224, pixel.Blue, 2);
            Assert.Equal(200, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc6HUFloatRoundTripsHdrRgbaFloat()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(2f, 0.5f, 8f, 0.25f), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc6HUFloat);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(2f, pixel.Red, 0.02f);
            AssertClose(0.5f, pixel.Green, 0.01f);
            AssertClose(8f, pixel.Blue, 0.08f);
            Assert.Equal(1f, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc6HSFloatRoundTripsNegativeRgbaFloat()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(-2f, 0.5f, 3f, 1f), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc6HSFloat);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            AssertClose(-2f, pixel.Red, 0.03f);
            AssertClose(0.5f, pixel.Green, 0.01f);
            AssertClose(3f, pixel.Blue, 0.04f);
            Assert.Equal(1f, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeBc7HonorsBlockRowPitch()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(12, 34, 56, 78), 25).ToArray());
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 4;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(72, encoded.Length);
        Assert.All(encoded[32..36], value => Assert.Equal(0xcc, value));
        Assert.All(encoded[68..72], value => Assert.Equal(0xcc, value));

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(12, 34, 56, 78), pixel));
    }

    [Fact]
    public void FastEncodingUsesLegacyBc6HAndBc7Modes()
    {
        var bc6H = new BptcTextureCoder(TextureFormats.Bc6HUFloat);
        var bc6HSource = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(1f, 2f, 4f, 1f), 16).ToArray());
        var bc6HRowPitch = bc6H.GetDefaultPitch(bc6HSource.Width);
        var bc6HEncoded = new byte[bc6H.GetEncodedByteCount(bc6HSource.Width, bc6HSource.Height, bc6HRowPitch)];

        bc6H.Encode(bc6HSource.AsView(), bc6HEncoded, bc6HRowPitch);

        var bc7 = new BptcTextureCoder(TextureFormats.Bc7UNorm);
        var bc7Source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(32, 96, 192, 224), 16).ToArray());
        var bc7RowPitch = bc7.GetDefaultPitch(bc7Source.Width);
        var bc7Encoded = new byte[bc7.GetEncodedByteCount(bc7Source.Width, bc7Source.Height, bc7RowPitch)];

        bc7.Encode(bc7Source.AsView(), bc7Encoded, bc7RowPitch);

        Assert.Equal(3, ReadBc6HMode(bc6HEncoded));
        Assert.Equal(6, ReadBc7Mode(bc7Encoded));
    }

    [Fact]
    public void ExhaustiveBc6HCanSelectNonLegacyMode()
    {
        var pixels = new Rgba32Float[16];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                pixels[(y * 4) + x] = new Rgba32Float(
                    1f + (x * 0.0004f),
                    0.75f + (y * 0.0004f),
                    0.5f + ((x + y) * 0.0002f),
                    1f);
            }
        }

        var source = new ArrayBitmap<Rgba32Float>(4, 4, pixels);
        var coder = new BptcTextureCoder(
            TextureFormats.Bc6HUFloat,
            new BptcCoderOptions { CompressionMode = TextureCompressionLevel.Exhaustive });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.NotEqual(3, ReadBc6HMode(encoded));
    }

    [Fact]
    public void ExhaustiveBc7CanSelectNonLegacyMode()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(255, 0, 0, 255), new Rgba8UNorm(255, 0, 0, 255), new Rgba8UNorm(0, 255, 0, 64), new Rgba8UNorm(0, 255, 0, 64),
            new Rgba8UNorm(255, 0, 0, 255), new Rgba8UNorm(255, 0, 0, 255), new Rgba8UNorm(0, 255, 0, 64), new Rgba8UNorm(0, 255, 0, 64),
            new Rgba8UNorm(0, 0, 255, 160), new Rgba8UNorm(0, 0, 255, 160), new Rgba8UNorm(255, 255, 0, 16), new Rgba8UNorm(255, 255, 0, 16),
            new Rgba8UNorm(0, 0, 255, 160), new Rgba8UNorm(0, 0, 255, 160), new Rgba8UNorm(255, 255, 0, 16), new Rgba8UNorm(255, 255, 0, 16)
        };
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var coder = new BptcTextureCoder(
            TextureFormats.Bc7UNorm,
            new BptcCoderOptions { CompressionMode = TextureCompressionLevel.Exhaustive });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.NotEqual(6, ReadBc7Mode(encoded));
    }

    [Theory]
    [MemberData(nameof(BptcCompressionModes))]
    public void Bc7CompressionModesEncodeDecodablePayloads(TextureCompressionLevel compressionMode)
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Range(0, 16)
                .Select(i => new Rgba8UNorm((byte)(16 + (i * 13)), (byte)(32 + (i * 7)), (byte)(48 + (i * 11)), (byte)(64 + (i * 9))))
                .ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc7UNorm, new BptcCoderOptions { CompressionMode = compressionMode });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Contains(decoded.Pixels, pixel => pixel.Alpha != 0);
    }

    [Theory]
    [MemberData(nameof(BptcCompressionModes))]
    public void Bc6HCompressionModesEncodeDecodablePayloads(TextureCompressionLevel compressionMode)
    {
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Range(0, 16)
                .Select(i => new Rgba32Float(0.25f + (i * 0.125f), 0.5f + (i * 0.0625f), 1f + (i * 0.25f), 1f))
                .ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
        var coder = new BptcTextureCoder(TextureFormats.Bc6HUFloat, new BptcCoderOptions { CompressionMode = compressionMode });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Contains(decoded.Pixels, pixel => pixel.Blue > 0f);
    }

    [Theory]
    [InlineData(1, 16)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void BptcRowByteCountUsesFourByFourBlocks(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc6HUFloat.GetRowByteCount(width));
        Assert.Equal(expected, TextureFormats.Bc7UNorm.GetRowByteCount(width));
    }

    private static void AssertClose(float expected, float actual, float tolerance) =>
        Assert.True(MathF.Abs(expected - actual) <= tolerance, $"Expected {actual} to be within {tolerance} of {expected}.");

    private static void AssertClose(int expected, int actual, int tolerance) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);

    private static int ReadBc6HMode(ReadOnlySpan<byte> encoded)
    {
        var mode = ReadBits(encoded, offset: 0, bitCount: 2);
        if ((mode & 2) != 0)
        {
            mode |= ReadBits(encoded, offset: 2, bitCount: 3) << 2;
        }

        return mode;
    }

    private static int ReadBc7Mode(ReadOnlySpan<byte> encoded)
    {
        var mode = 0;
        while (mode < 8 && ReadBits(encoded, mode, bitCount: 1) == 0)
        {
            mode++;
        }

        return mode;
    }

    private static int ReadBits(ReadOnlySpan<byte> encoded, int offset, int bitCount)
    {
        var value = 0;
        for (var i = 0; i < bitCount; i++)
        {
            value |= ((encoded[(offset + i) >> 3] >> ((offset + i) & 7)) & 1) << i;
        }

        return value;
    }

    public static TheoryData<TextureCompressionLevel> BptcCompressionModes() => new()
    {
        TextureCompressionLevel.Fast,
        TextureCompressionLevel.Normal,
        TextureCompressionLevel.High,
        TextureCompressionLevel.Exhaustive
    };

    public static TheoryData<TextureFormat> BptcFormats() => new()
    {
        TextureFormats.Bc6HUFloat,
        TextureFormats.Bc6HSFloat,
        TextureFormats.Bc7UNorm,
        TextureFormats.Bc7Srgb,
        TextureFormats.RgbBptcUFloat,
        TextureFormats.RgbBptcSFloat,
        TextureFormats.RgbaBptcUNorm,
        TextureFormats.RgbaBptcSrgb
    };
}
