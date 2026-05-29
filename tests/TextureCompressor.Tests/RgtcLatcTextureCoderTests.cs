using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class RgtcLatcTextureCoderTests
{
    [Theory]
    [MemberData(nameof(RgtcLatcFormats))]
    public void GlobalManagerFindsRgtcLatcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(RgtcLatcTextureCoder.IsSupported(format));
        Assert.IsType<RgtcLatcTextureCoder>(coder);
    }

    [Fact]
    public void Bc4UNormDecodesRedBlockToRgba8()
    {
        var encoded = new byte[TextureFormats.Bc4UNorm.GetByteCount(4, 4)];
        WriteComponentBlock(encoded, 255, 0, 1);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Bc4UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Bc5UNormDecodesRedAndGreenBlocksToRgba8()
    {
        var encoded = new byte[TextureFormats.Bc5UNorm.GetByteCount(4, 4)];
        WriteComponentBlock(encoded, 255, 0, 1);
        WriteComponentBlock(encoded.AsSpan(8), 64, 192, 1);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Bc5UNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(0, 192, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(255, 64, 0, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void Bc4SNormDecodesSignedSpecialEndpoints()
    {
        var encoded = new byte[TextureFormats.Bc4SNorm.GetByteCount(4, 4)];
        WriteComponentBlock(encoded, SignedByte(0), SignedByte(1), 6);
        var decoded = new ArrayBitmap<Rgba8SNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Bc4SNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(-sbyte.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(sbyte.MaxValue, decoded.Pixels[0].Alpha);
        Assert.Equal(0, decoded.Pixels[1].Red);
    }

    [Fact]
    public void Latc2SNormDecodesLuminanceAndAlphaBlocks()
    {
        var encoded = new byte[TextureFormats.Latc2SNorm.GetByteCount(4, 4)];
        WriteComponentBlock(encoded, SignedByte(10), SignedByte(-10), 0);
        WriteComponentBlock(encoded.AsSpan(8), SignedByte(-20), SignedByte(20), 0);
        var decoded = new ArrayBitmap<Rgba8SNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Latc2SNorm);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8SNorm(10, 10, 10, -20), decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBc5UNormRoundTripsSolidRg8()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(34, 200, 123, 64), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Bc5UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(34, 200, 0, 255), pixel));
    }

    [Fact]
    public void EncodeAndDecodeLatc2UNormStoresLuminanceAndAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(40, 80, 120, 200), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Latc2UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(new Rgba8UNorm(40, 40, 40, 200), pixel));
    }

    [Fact]
    public void EncodeSignedCanonicalizesMinus128Endpoint()
    {
        var source = new ArrayBitmap<Rgba8SNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8SNorm(sbyte.MinValue, 0, 0), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8SNorm>(4, 4);
        var coder = new RgtcLatcTextureCoder(TextureFormats.Bc4SNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(SignedByte(-sbyte.MaxValue), encoded[0]);
        Assert.Equal(SignedByte(-sbyte.MaxValue), encoded[1]);
        Assert.All(decoded.Pixels, pixel => Assert.Equal(-sbyte.MaxValue, pixel.Red));
    }

    [Fact]
    public void EncodeAndDecodeHonorsBlockRowPitch()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            5,
            5,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 25).ToArray());
        var coder = new RgtcLatcTextureCoder(TextureFormats.Bc4UNorm);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 4;
        var encoded = Enumerable.Repeat((byte)0xcc, coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)).ToArray();

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(40, encoded.Length);
        Assert.All(encoded[16..20], value => Assert.Equal(0xcc, value));
        Assert.All(encoded[36..40], value => Assert.Equal(0xcc, value));

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 5);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.All(decoded.Pixels, pixel => Assert.Equal(255, pixel.Red));
    }

    [Fact]
    public void Bc4AndBc5ByteCountsUseFourByFourBlockRows()
    {
        var bc4 = new RgtcLatcTextureCoder(TextureFormats.Bc4UNorm);
        var bc5 = new RgtcLatcTextureCoder(TextureFormats.Bc5UNorm);

        Assert.Equal(16, bc4.GetDefaultPitch(5));
        Assert.Equal(32, bc4.GetEncodedByteCount(5, 5, bc4.GetDefaultPitch(5)));
        Assert.Equal(32, bc5.GetDefaultPitch(5));
        Assert.Equal(64, bc5.GetEncodedByteCount(5, 5, bc5.GetDefaultPitch(5)));
    }

    private static void WriteComponentBlock(Span<byte> destination, byte value0, byte value1, ulong indices)
    {
        destination[0] = value0;
        destination[1] = value1;
        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(indices >> (8 * i));
        }
    }

    private static byte SignedByte(int value) => unchecked((byte)(sbyte)value);

    public static TheoryData<TextureFormat> RgtcLatcFormats() => new()
    {
        TextureFormats.Bc4UNorm,
        TextureFormats.Bc4SNorm,
        TextureFormats.Bc5UNorm,
        TextureFormats.Bc5SNorm,
        TextureFormats.Ati1UNorm,
        TextureFormats.Ati1SNorm,
        TextureFormats.Ati2UNorm,
        TextureFormats.Ati2SNorm,
        TextureFormats.Rgtc1UNorm,
        TextureFormats.Rgtc1SNorm,
        TextureFormats.Rgtc2UNorm,
        TextureFormats.Rgtc2SNorm,
        TextureFormats.Latc1UNorm,
        TextureFormats.Latc1SNorm,
        TextureFormats.Latc2UNorm,
        TextureFormats.Latc2SNorm
    };
}
