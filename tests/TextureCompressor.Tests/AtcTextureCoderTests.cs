using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;
using TextureCompressor.Options;

namespace TextureCompressor.Tests;

public sealed class AtcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(AtcFormats))]
    public void GlobalManagerFindsAtcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(AtcTextureCoder.IsSupported(format));
        Assert.IsType<AtcTextureCoder>(coder);
    }

    [Fact]
    public void AtcRgbDecodesModeZeroColorBlockToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0x7c00, 0xf800, 0);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), pixel));
    }

    [Fact]
    public void AtcRgbDecodesModeZeroInterpolatedColorsToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0x7c00, 0x001f, (1u << 2) | (2u << 4) | (3u << 6));
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(159, 0, 95, 255), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(95, 0, 159, 255), decoded.Pixels[2]);
    }

    [Fact]
    public void AtcRgbDecodesModeOneColorBlockToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(4, 4)];
        WriteColorBlock(encoded, 0xfc00, 0x07e0, (2u << 2) | (3u << 4));
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[1]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[2]);
    }

    [Fact]
    public void AtcRgbaExplicitAlphaDecodesExplicitAlphaAndColorToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgbaExplicitAlpha.GetByteCount(4, 4)];
        encoded[0] = 0x0f;
        WriteColorBlock(encoded.AsSpan(8), 0x7c00, 0xf800, 0);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaExplicitAlpha);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 0, 0, 0), decoded.Pixels[1]);
    }

    [Fact]
    public void AtcRgbaInterpolatedAlphaDecodesInterpolatedAlphaAndColorToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgbaInterpolatedAlpha.GetByteCount(4, 4)];
        encoded[0] = 255;
        encoded[1] = 0;
        encoded[2] = 0x01;
        WriteColorBlock(encoded.AsSpan(8), 0x03e0, 0x07e0, 0);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 255, 0, 0), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void AtcRgbaInterpolatedAlphaDecodesRoundedAlphaRampToRgba8()
    {
        var encoded = new byte[TextureFormats.AtcRgbaInterpolatedAlpha.GetByteCount(4, 4)];
        encoded[0] = 255;
        encoded[1] = 0;
        encoded[2] = 0x02;
        WriteColorBlock(encoded.AsSpan(8), 0x03e0, 0x07e0, 0);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 255, 0, 219), decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeAtcRgbIgnoresSourceAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 20), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 34);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAndDecodeAtcRgbaExplicitAlphaRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaExplicitAlpha);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 34);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(136, pixel.Alpha);
        });
    }

    [Fact]
    public void AtcRgbaExplicitAlphaEncodeQuantizesToNearestReplicatedAlpha()
    {
        var sourcePixels = Enumerable.Repeat(new Rgba8UNorm(0, 0, 0, 0), 16).ToArray();
        sourcePixels[0] = new Rgba8UNorm(0, 0, 0, 9);
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, sourcePixels);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaExplicitAlpha);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(0x01, encoded[0] & 0x0f);
    }

    [Fact]
    public void EncodeAndDecodeAtcRgbaInterpolatedAlphaRoundTripsSolidRgba8WithinQuantization()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(17, 34, 51, 128), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 16, 17);
            Assert.InRange(pixel.Green, 32, 34);
            Assert.InRange(pixel.Blue, 49, 52);
            Assert.Equal(128, pixel.Alpha);
        });
    }

    [Fact]
    public void EncodeAtcRgbUsesFastBoundsByDefault()
    {
        var pixels = CreateAlternatingRedGreenBlock();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(EncodeFastAtcRgbBlock(pixels), encoded);
    }

    [Fact]
    public void EncodeAtcRgbHighQualityFitsColorClustersInsteadOfRgbBounds()
    {
        var pixels = CreateAlternatingRedGreenBlock();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var options = new AtcCoderOptions { CompressionMode = TextureCompressionLevel.High };
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb, options);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var boundsDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);
        coder.Decode(EncodeFastAtcRgbBlock(pixels), boundsDecoded.AsView(), rowPitch);

        Assert.Equal(0, RgbSquaredError(source, decoded));
        Assert.True(RgbSquaredError(source, decoded) < RgbSquaredError(source, boundsDecoded));
    }

    [Fact]
    public void EncodeAtcRgbaInterpolatedAlphaHighQualityIsNoWorseThanFastSearch()
    {
        var pixels = Enumerable.Range(0, 16)
            .Select(i => new Rgba8UNorm(64, 96, 128, (byte)((i * 17) + ((i & 1) * 11))))
            .ToArray();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var fastCoder = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);
        var highCoder = new AtcTextureCoder(
            TextureFormats.AtcRgbaInterpolatedAlpha,
            new AtcCoderOptions { CompressionMode = TextureCompressionLevel.High });
        var rowPitch = fastCoder.GetDefaultPitch(source.Width);
        var fastEncoded = new byte[fastCoder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var highEncoded = new byte[highCoder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var fastDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var highDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        fastCoder.Encode(source.AsView(), fastEncoded, rowPitch);
        highCoder.Encode(source.AsView(), highEncoded, rowPitch);
        fastCoder.Decode(fastEncoded, fastDecoded.AsView(), rowPitch);
        highCoder.Decode(highEncoded, highDecoded.AsView(), rowPitch);

        Assert.True(AlphaSquaredError(source, highDecoded) <= AlphaSquaredError(source, fastDecoded));
    }

    [Theory]
    [MemberData(nameof(AtcFormatCompressionModes))]
    public void EncodeAndDecodeAtcSupportsCompressionMode(TextureFormat format, TextureCompressionLevel compressionMode)
    {
        var pixels = Enumerable.Range(0, 16)
            .Select(i => new Rgba8UNorm(
                (byte)(24 + (i * 9)),
                (byte)(220 - (i * 7)),
                (byte)(32 + (i * 11)),
                (byte)(16 + (i * 13))))
            .ToArray();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new AtcTextureCoder(format, new AtcCoderOptions { CompressionMode = compressionMode });
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Contains(encoded, value => value != 0);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
        Assert.Contains(decoded.Pixels, pixel => pixel.Alpha != 0);
    }

    [Fact]
    public void EncodeAtcRgbaInterpolatedAlphaExhaustiveIsNoWorseThanHighQualitySearch()
    {
        var pixels = Enumerable.Range(0, 16)
            .Select(i => new Rgba8UNorm(48, 80, 112, (byte)((i * 15) + ((i % 3) * 7))))
            .ToArray();
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, pixels);
        var highCoder = new AtcTextureCoder(
            TextureFormats.AtcRgbaInterpolatedAlpha,
            new AtcCoderOptions { CompressionMode = TextureCompressionLevel.High });
        var exhaustiveCoder = new AtcTextureCoder(
            TextureFormats.AtcRgbaInterpolatedAlpha,
            new AtcCoderOptions { CompressionMode = TextureCompressionLevel.Exhaustive });
        var rowPitch = highCoder.GetDefaultPitch(source.Width);
        var highEncoded = new byte[highCoder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var exhaustiveEncoded = new byte[exhaustiveCoder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var highDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var exhaustiveDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        highCoder.Encode(source.AsView(), highEncoded, rowPitch);
        exhaustiveCoder.Encode(source.AsView(), exhaustiveEncoded, rowPitch);
        highCoder.Decode(highEncoded, highDecoded.AsView(), rowPitch);
        exhaustiveCoder.Decode(exhaustiveEncoded, exhaustiveDecoded.AsView(), rowPitch);

        Assert.True(AlphaSquaredError(source, exhaustiveDecoded) <= AlphaSquaredError(source, highDecoded));
    }

    [Fact]
    public void AtcDecodeUsesPaddedBlockWidthForNonMultipleOfFourWidth()
    {
        var encoded = new byte[TextureFormats.AtcRgb.GetByteCount(5, 1)];
        WriteColorBlock(encoded, 0x7c00, 0xf800, 0);
        WriteColorBlock(encoded.AsSpan(8), 0x03e0, 0x07e0, 0);
        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 1);
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[3]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 255), decoded.Pixels[4]);
    }

    [Fact]
    public void EncodeAndDecodeHonorsBlockRowPitch()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 25).ToArray());
        var coder = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 4;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(40, encoded.Length);
        Assert.All(encoded[16..20], value => Assert.Equal(0xcc, value));
        Assert.All(encoded[36..40], value => Assert.Equal(0xcc, value));

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.Equal(255, pixel.Red);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void AtcByteCountsUseFourByFourBlockRows()
    {
        var rgb = new AtcTextureCoder(TextureFormats.AtcRgb);
        var rgba = new AtcTextureCoder(TextureFormats.AtcRgbaInterpolatedAlpha);

        Assert.Equal(16, rgb.GetDefaultPitch(5));
        Assert.Equal(32, rgb.GetEncodedByteCount(5, 5, rgb.GetDefaultPitch(5)));
        Assert.Equal(32, rgba.GetDefaultPitch(5));
        Assert.Equal(64, rgba.GetEncodedByteCount(5, 5, rgba.GetDefaultPitch(5)));
        Assert.Equal(80, rgba.GetEncodedByteCount(5, 5, 40));
    }

    private static void WriteColorBlock(Span<byte> destination, ushort color0, ushort color1, uint indices)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], indices);
    }

    private static byte[] EncodeFastAtcRgbBlock(ReadOnlySpan<Rgba8UNorm> source)
    {
        var encoded = new byte[8];
        WriteColorBlock(encoded, 0x0000, 0xffe0, GetNearestAtcColorIndices(source, 0x0000, 0xffe0));
        Span<byte> candidate = stackalloc byte[8];
        TryUseBetterColorCandidate(source, 0x7fe0, 0x0000, encoded, candidate);
        TryUseBetterColorCandidate(source, 0xffe0, 0x0000, encoded, candidate);
        TryUseBetterColorCandidate(source, 0xffe0, 0xffe0, encoded, candidate);
        return encoded;
    }

    private static void TryUseBetterColorCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1,
        Span<byte> best,
        Span<byte> candidate)
    {
        WriteColorBlock(candidate, color0, color1, GetNearestAtcColorIndices(source, color0, color1));
        if (RgbSquaredError(source, candidate) < RgbSquaredError(source, best))
        {
            candidate.CopyTo(best);
        }
    }

    private static uint GetNearestAtcColorIndices(ReadOnlySpan<Rgba8UNorm> source, ushort color0, ushort color1)
    {
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildAtcColorPalette(color0, color1, palette);
        uint indices = 0;
        for (var i = 0; i < 16; i++)
        {
            var index = 0;
            var bestDistance = int.MaxValue;
            for (var paletteIndex = 0; paletteIndex < palette.Length; paletteIndex++)
            {
                var distance = RgbSquaredDistance(source[i], palette[paletteIndex]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    index = paletteIndex;
                }
            }

            indices |= (uint)index << (i * 2);
        }

        return indices;
    }

    private static void BuildAtcColorPalette(ushort color0, ushort color1, Span<Rgba8UNorm> palette)
    {
        var c1 = UnpackRgb565(color1);
        if ((color0 & 0x8000) == 0)
        {
            var c0 = UnpackRgb555(color0);
            palette[0] = new Rgba8UNorm(c0.Red, c0.Green, c0.Blue);
            palette[1] = Interpolate(c0, c1, 5, 3, 8);
            palette[2] = Interpolate(c0, c1, 3, 5, 8);
            palette[3] = new Rgba8UNorm(c1.Red, c1.Green, c1.Blue);
            return;
        }

        var c2 = UnpackRgb555((ushort)(color0 & 0x7fff));
        palette[0] = new Rgba8UNorm(0, 0, 0);
        palette[1] = new Rgba8UNorm(
            (byte)Math.Max(0, c2.Red - (c1.Red / 4)),
            (byte)Math.Max(0, c2.Green - (c1.Green / 4)),
            (byte)Math.Max(0, c2.Blue - (c1.Blue / 4)));
        palette[2] = new Rgba8UNorm(c2.Red, c2.Green, c2.Blue);
        palette[3] = new Rgba8UNorm(c1.Red, c1.Green, c1.Blue);
    }

    private static Rgba8UNorm Interpolate(Rgb24 a, Rgb24 b, int weightA, int weightB, int divisor) =>
        new(
            (byte)(((weightA * a.Red) + (weightB * b.Red)) / divisor),
            (byte)(((weightA * a.Green) + (weightB * b.Green)) / divisor),
            (byte)(((weightA * a.Blue) + (weightB * b.Blue)) / divisor));

    private static Rgb24 UnpackRgb555(ushort value)
    {
        var red = (value >> 10) & 0x1f;
        var green = (value >> 5) & 0x1f;
        var blue = value & 0x1f;
        return new Rgb24(Expand5(red), Expand5(green), Expand5(blue));
    }

    private static Rgb24 UnpackRgb565(ushort value)
    {
        var red = (value >> 11) & 0x1f;
        var green = (value >> 5) & 0x3f;
        var blue = value & 0x1f;
        return new Rgb24(Expand5(red), Expand6(green), Expand5(blue));
    }

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

    private static byte Expand6(int value) => (byte)((value << 2) | (value >> 4));

    private static int RgbSquaredError(ArrayBitmap<Rgba8UNorm> expected, ArrayBitmap<Rgba8UNorm> actual)
    {
        var error = 0;
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            error += RgbSquaredDistance(expected.Pixels[i], actual.Pixels[i]);
        }

        return error;
    }

    private static int RgbSquaredError(ReadOnlySpan<Rgba8UNorm> source, ReadOnlySpan<byte> encoded)
    {
        Span<Rgba8UNorm> decoded = stackalloc Rgba8UNorm[16];
        BuildAtcColorPalette(
            BinaryPrimitives.ReadUInt16LittleEndian(encoded),
            BinaryPrimitives.ReadUInt16LittleEndian(encoded[2..]),
            decoded);
        var indices = BinaryPrimitives.ReadUInt32LittleEndian(encoded[4..]);
        var error = 0;
        for (var i = 0; i < 16; i++)
        {
            error += RgbSquaredDistance(source[i], decoded[(int)((indices >> (i * 2)) & 0x3u)]);
        }

        return error;
    }

    private static int RgbSquaredDistance(Rgba8UNorm a, Rgba8UNorm b)
    {
        var red = a.Red - b.Red;
        var green = a.Green - b.Green;
        var blue = a.Blue - b.Blue;
        return (red * red) + (green * green) + (blue * blue);
    }

    private static int AlphaSquaredError(ArrayBitmap<Rgba8UNorm> expected, ArrayBitmap<Rgba8UNorm> actual)
    {
        var error = 0;
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            var alpha = expected.Pixels[i].Alpha - actual.Pixels[i].Alpha;
            error += alpha * alpha;
        }

        return error;
    }

    private static Rgba8UNorm[] CreateAlternatingRedGreenBlock() =>
        Enumerable.Range(0, 16)
            .Select(i => (i & 1) == 0
                ? new Rgba8UNorm(255, 0, 0, 255)
                : new Rgba8UNorm(0, 255, 0, 255))
            .ToArray();

    private readonly record struct Rgb24(byte Red, byte Green, byte Blue);

    public static TheoryData<TextureFormat> AtcFormats() => new()
    {
        TextureFormats.AtcRgb,
        TextureFormats.AtcRgbaExplicitAlpha,
        TextureFormats.AtcRgbaInterpolatedAlpha
    };

    public static TheoryData<TextureFormat, TextureCompressionLevel> AtcFormatCompressionModes() => new()
    {
        { TextureFormats.AtcRgb, TextureCompressionLevel.Fast },
        { TextureFormats.AtcRgb, TextureCompressionLevel.Normal },
        { TextureFormats.AtcRgb, TextureCompressionLevel.High },
        { TextureFormats.AtcRgb, TextureCompressionLevel.Exhaustive },
        { TextureFormats.AtcRgbaExplicitAlpha, TextureCompressionLevel.Fast },
        { TextureFormats.AtcRgbaExplicitAlpha, TextureCompressionLevel.Normal },
        { TextureFormats.AtcRgbaExplicitAlpha, TextureCompressionLevel.High },
        { TextureFormats.AtcRgbaExplicitAlpha, TextureCompressionLevel.Exhaustive },
        { TextureFormats.AtcRgbaInterpolatedAlpha, TextureCompressionLevel.Fast },
        { TextureFormats.AtcRgbaInterpolatedAlpha, TextureCompressionLevel.Normal },
        { TextureFormats.AtcRgbaInterpolatedAlpha, TextureCompressionLevel.High },
        { TextureFormats.AtcRgbaInterpolatedAlpha, TextureCompressionLevel.Exhaustive }
    };
}
