using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class EtcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(EtcFormats))]
    public void GlobalManagerFindsEtcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(EtcTextureCoder.IsSupported(format));
        Assert.IsType<EtcTextureCoder>(coder);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.RgbEtc1UNorm))]
    [InlineData(nameof(TextureFormats.RgbEtc2UNorm))]
    public void EtcRgbDecodesZeroIndividualBlockToRgba8(string formatName)
    {
        var format = GetFormat(formatName);
        var encoded = new byte[format.GetByteCount(4, 4)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(format);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(2, 2, 2, 255), pixel));
    }

    [Fact]
    public void Etc2SrgbDecodesStorageRgbToLinearRgba8()
    {
        var encoded = new byte[TextureFormats.RgbEtc2Srgb.GetByteCount(4, 4)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.RgbEtc2Srgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), pixel));
    }

    [Fact]
    public void Etc2PunchthroughTransparentIndexDecodesAlphaZero()
    {
        var encoded = new byte[TextureFormats.RgbA1Etc2UNorm.GetByteCount(4, 4)];
        WriteEtcRawIndices(encoded.AsSpan(4), rawIndex: 2);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.RgbA1Etc2UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(0, 0, 0, 0), pixel));
    }

    [Fact]
    public void Etc2RgbaEacDecodesAlphaAndColorBlocks()
    {
        var encoded = new byte[TextureFormats.RgbaEtc2EacUNorm.GetByteCount(4, 4)];
        WriteEacBlock(encoded, 255, 0, 7);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.RgbaEtc2EacUNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(2, 2, 2, 255), pixel));
    }

    [Fact]
    public void EacR11UNormDecodesUnsignedScalarToRed()
    {
        var encoded = new byte[TextureFormats.R11EacUNorm.GetByteCount(4, 4)];
        WriteEacBlock(encoded, 255, 0, 7);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.R11EacUNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), pixel));
    }

    [Fact]
    public void EacRg11SNormDecodesSignedScalarsToRedAndGreen()
    {
        var encoded = new byte[TextureFormats.Rg11EacSNorm.GetByteCount(4, 4)];
        WriteEacBlock(encoded, unchecked((byte)(sbyte)-127), 0, 3);
        WriteEacBlock(encoded.AsSpan(8), 127, 0, 7);
        var decoded = new ArrayBitmap<Rgba8SNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.Rg11EacSNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8SNorm(-127, 127, 0, 127), pixel));
    }

    [Fact]
    public void EncodeAndDecodeEtc2RgbRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(120, 64, 220, 32), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.RgbEtc2UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 112, 128);
            Assert.InRange(pixel.Green, 56, 72);
            Assert.InRange(pixel.Blue, 212, 228);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeEtc2RgbHighQualityFitsColorClustersWithEtc2Modes()
    {
        var pixels = Enumerable.Range(0, 16)
            .Select(i => (i & 1) == 0
                ? new Rgba8UNorm(255, 0, 0, 255)
                : new Rgba8UNorm(0, 255, 0, 255))
            .ToArray();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var fastCoder = new EtcTextureCoder(TextureFormats.RgbEtc2UNorm);
        var highCoder = new EtcTextureCoder(
            TextureFormats.RgbEtc2UNorm,
            new EtcCoderOptions { CompressionMode = TextureCompressionLevel.High });
        var rowPitch = fastCoder.GetDefaultPitch(source.Width);
        var fastEncoded = new byte[fastCoder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var highEncoded = new byte[highCoder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var fastDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var highDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        fastCoder.Encode(source.AsView(), fastEncoded, rowPitch);
        highCoder.Encode(source.AsView(), highEncoded, rowPitch);
        fastCoder.Decode(fastEncoded, fastDecoded.AsView(), rowPitch);
        highCoder.Decode(highEncoded, highDecoded.AsView(), rowPitch);

        Assert.Equal(0, RgbSquaredError(source, highDecoded));
        Assert.True(RgbSquaredError(source, highDecoded) < RgbSquaredError(source, fastDecoded));
    }

    [Theory]
    [MemberData(nameof(EtcCompressionModes))]
    public void EncodeAndDecodeEtc2RgbAcceptsCompressionModes(TextureCompressionLevel compressionMode)
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Range(0, 16)
                .Select(i => new Rgba8UNorm((byte)(40 + (i * 11)), (byte)(200 - (i * 7)), (byte)(80 + (i * 5)), 255))
                .ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(
            TextureFormats.RgbEtc2UNorm,
            new EtcCoderOptions { CompressionMode = compressionMode });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(255, pixel.Alpha));
    }

    [Fact]
    public void EncodeEtc2RgbCompressionModesDoNotIncreaseError()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            [
                new(32, 64, 224, 255), new(48, 72, 208, 255), new(192, 56, 40, 255), new(208, 72, 56, 255),
                new(40, 96, 216, 255), new(64, 112, 192, 255), new(184, 96, 64, 255), new(224, 104, 72, 255),
                new(24, 160, 96, 255), new(56, 176, 120, 255), new(160, 152, 176, 255), new(192, 168, 200, 255),
                new(16, 200, 80, 255), new(64, 216, 104, 255), new(144, 208, 192, 255), new(224, 224, 232, 255)
            ]);
        var previousError = long.MaxValue;

        foreach (var compressionMode in OrderedEtcCompressionModes)
        {
            var decoded = EncodeAndDecodeEtc2Rgb(source, compressionMode);
            var error = RgbSquaredError(source, decoded);

            Assert.True(error <= previousError, $"{compressionMode} error {error} exceeded previous mode error {previousError}.");
            previousError = error;
        }
    }

    [Fact]
    public void EncodeAndDecodeEtc2RgbaEacRoundTripsSolidAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(50, 80, 110, 180), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.RgbaEtc2EacUNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 42, 58);
            Assert.InRange(pixel.Green, 72, 88);
            Assert.InRange(pixel.Blue, 102, 118);
            Assert.Equal(180, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeEacRg11UNormRoundTripsFloatRg()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            4,
            4,
            Enumerable.Repeat(new Rgba32Float(0.25f, 0.75f, 0.5f, 0.25f), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba32Float>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.Rg11EacUNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 0.247f, 0.253f);
            Assert.InRange(pixel.Green, 0.747f, 0.753f);
            Assert.Equal(0f, pixel.Blue);
            Assert.Equal(1f, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeEacR11UNormUsesZeroMultiplierForLowDynamicRange()
    {
        int[] targets =
        [
            1025, 1022, 1019, 1013,
            1030, 1033, 1036, 1042,
            1025, 1022, 1019, 1013,
            1030, 1033, 1036, 1042
        ];
        var sourcePixels = targets
            .Select(value => new Rgba16UNorm(Unsigned11ToUNorm16(value), 0, 0, ushort.MaxValue))
            .ToArray();
        var source = new ArrayBitmap<Rgba16UNorm>(
            4,
            4,
            sourcePixels);
        var decoded = new ArrayBitmap<Rgba16UNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.R11EacUNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(0, encoded[1] >> 4);
        for (var i = 0; i < targets.Length; i++)
        {
            Assert.Equal(sourcePixels[i].Red, decoded.Pixels[i].Red);
        }
    }

    [Fact]
    public void EncodeEacR11UNormCompressionModesDoNotIncreaseError()
    {
        int[] targets =
        [
            0, 177, 356, 511,
            691, 870, 1048, 1204,
            1379, 1534, 1710, 1888,
            2047, 1633, 806, 251
        ];
        var sourcePixels = targets
            .Select(value => new Rgba16UNorm(Unsigned11ToUNorm16(value), 0, 0, ushort.MaxValue))
            .ToArray();
        var source = new ArrayBitmap<Rgba16UNorm>(4, 4, sourcePixels);
        var previousError = long.MaxValue;

        foreach (var compressionMode in OrderedEtcCompressionModes)
        {
            var decoded = EncodeAndDecodeEacR11UNorm(source, compressionMode);
            var error = RedSquaredError(source, decoded);

            Assert.True(error <= previousError, $"{compressionMode} error {error} exceeded previous mode error {previousError}.");
            previousError = error;
        }
    }

    [Fact]
    public void EncodeEacR11SNormUsesZeroMultiplierForLowDynamicRange()
    {
        int[] targets =
        [
            -3, -6, -9, -15,
            2, 5, 8, 14,
            -3, -6, -9, -15,
            2, 5, 8, 14
        ];
        var sourcePixels = targets
            .Select(value => new Rgba16SNorm(Signed11ToSNorm16(value), 0, 0, short.MaxValue))
            .ToArray();
        var source = new ArrayBitmap<Rgba16SNorm>(
            4,
            4,
            sourcePixels);
        var decoded = new ArrayBitmap<Rgba16SNorm>(4, 4);
        var coder = new EtcTextureCoder(TextureFormats.R11EacSNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(0, encoded[1] >> 4);
        for (var i = 0; i < targets.Length; i++)
        {
            Assert.Equal(sourcePixels[i].Red, decoded.Pixels[i].Red);
        }
    }

    [Fact]
    public void EncodeAndDecodeHonorsBlockRowPitch()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 25).ToArray());
        var coder = new EtcTextureCoder(TextureFormats.RgbEtc1UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 4;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(40, encoded.Length);
        Assert.All(encoded[16..20], value => Assert.Equal(0xcc, value));
        Assert.All(encoded[36..40], value => Assert.Equal(0xcc, value));

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.InRange(pixel.Red, 247, 255));
    }

    [Fact]
    public void EtcByteCountsUseFourByFourBlockRows()
    {
        var rgb = new EtcTextureCoder(TextureFormats.RgbEtc2UNorm);
        var rgba = new EtcTextureCoder(TextureFormats.RgbaEtc2EacUNorm);

        Assert.Equal(16, rgb.GetDefaultPitch(5));
        Assert.Equal(32, rgb.GetEncodedByteCount(5, 5, rgb.GetDefaultPitch(5)));
        Assert.Equal(32, rgba.GetDefaultPitch(5));
        Assert.Equal(64, rgba.GetEncodedByteCount(5, 5, rgba.GetDefaultPitch(5)));
    }

    private static TextureFormat GetFormat(string name) => name switch
    {
        nameof(TextureFormats.RgbEtc1UNorm) => TextureFormats.RgbEtc1UNorm,
        nameof(TextureFormats.RgbEtc2UNorm) => TextureFormats.RgbEtc2UNorm,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static ushort Unsigned11ToUNorm16(int value) =>
        (ushort)((value * ushort.MaxValue + 1023) / 2047);

    private static short Signed11ToSNorm16(int value) =>
        (short)(value >= 0
            ? ((value * short.MaxValue + 511) / 1023)
            : ((value * short.MaxValue - 511) / 1023));

    private static long RgbSquaredError(ArrayBitmap<Rgba8UNorm> expected, ArrayBitmap<Rgba8UNorm> actual)
    {
        var error = 0L;
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            var red = expected.Pixels[i].Red - actual.Pixels[i].Red;
            var green = expected.Pixels[i].Green - actual.Pixels[i].Green;
            var blue = expected.Pixels[i].Blue - actual.Pixels[i].Blue;
            error += (red * red) + (green * green) + (blue * blue);
        }

        return error;
    }

    private static long RedSquaredError(ArrayBitmap<Rgba16UNorm> expected, ArrayBitmap<Rgba16UNorm> actual)
    {
        var error = 0L;
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            var red = expected.Pixels[i].Red - actual.Pixels[i].Red;
            error += (long)red * red;
        }

        return error;
    }

    private static ArrayBitmap<Rgba8UNorm> EncodeAndDecodeEtc2Rgb(
        ArrayBitmap<Rgba8UNorm> source,
        TextureCompressionLevel compressionMode)
    {
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(
            TextureFormats.RgbEtc2UNorm,
            new EtcCoderOptions { CompressionMode = compressionMode });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        return decoded;
    }

    private static ArrayBitmap<Rgba16UNorm> EncodeAndDecodeEacR11UNorm(
        ArrayBitmap<Rgba16UNorm> source,
        TextureCompressionLevel compressionMode)
    {
        var decoded = new ArrayBitmap<Rgba16UNorm>(4, 4);
        var coder = new EtcTextureCoder(
            TextureFormats.R11EacUNorm,
            new EtcCoderOptions { CompressionMode = compressionMode });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        return decoded;
    }

    private static void WriteEtcRawIndices(Span<byte> destination, int rawIndex)
    {
        uint low = 0;
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var shift = (x * 4) + y;
                low |= (uint)(rawIndex & 1) << shift;
                low |= (uint)((rawIndex >> 1) & 1) << (shift + 16);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, low);
    }

    private static void WriteEacBlock(Span<byte> destination, byte baseCodeword, byte tableAndMultiplier, int index)
    {
        destination[0] = baseCodeword;
        destination[1] = tableAndMultiplier;

        ulong bits = 0;
        for (var order = 0; order < 16; order++)
        {
            bits |= (ulong)index << (45 - (order * 3));
        }

        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(bits >> (40 - (i * 8)));
        }
    }

    public static TheoryData<TextureFormat> EtcFormats() => new()
    {
        TextureFormats.RgbEtc1UNorm,
        TextureFormats.RgbEtc2UNorm,
        TextureFormats.RgbEtc2Srgb,
        TextureFormats.RgbA1Etc2UNorm,
        TextureFormats.RgbA1Etc2Srgb,
        TextureFormats.RgbaEtc2EacUNorm,
        TextureFormats.RgbaEtc2EacSrgb,
        TextureFormats.R11EacUNorm,
        TextureFormats.R11EacSNorm,
        TextureFormats.Rg11EacUNorm,
        TextureFormats.Rg11EacSNorm
    };

    public static TheoryData<TextureCompressionLevel> EtcCompressionModes() => new()
    {
        TextureCompressionLevel.Fast,
        TextureCompressionLevel.Normal,
        TextureCompressionLevel.High,
        TextureCompressionLevel.Exhaustive
    };

    private static TextureCompressionLevel[] OrderedEtcCompressionModes =>
    [
        TextureCompressionLevel.Fast,
        TextureCompressionLevel.Normal,
        TextureCompressionLevel.High,
        TextureCompressionLevel.Exhaustive
    ];
}
