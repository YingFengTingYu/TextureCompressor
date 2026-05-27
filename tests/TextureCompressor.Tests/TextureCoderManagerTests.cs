using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureCoderManagerTests
{
    [Fact]
    public void GlobalManagerFindsSequentialUncompressedCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UNorm);

        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedUNormCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565UNorm);

        Assert.IsType<PackedUNormTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(PackedUNormFormats))]
    public void GlobalManagerFindsPackedUNormCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PackedUNormTextureCoder.IsSupported(format));
        Assert.IsType<PackedUNormTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedFloatCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.R11G11B10Float);

        Assert.IsType<PackedFloatTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedIntegerCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UInt);

        Assert.IsType<PackedIntegerTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsBitPackedUNormCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Luminance4UNorm);

        Assert.IsType<BitPackedUNormTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsBw1BitPackedUNormCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Bw1BppUNorm);

        Assert.True(BitPackedUNormTextureCoder.IsSupported(TextureFormats.Bw1BppUNorm));
        Assert.IsType<BitPackedUNormTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(RepresentativePackedYuv422Formats))]
    public void GlobalManagerFindsPackedYuv422Coders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PackedYuv422TextureCoder.IsSupported(format));
        Assert.IsType<PackedYuv422TextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(RepresentativePackedYuva444Formats))]
    public void GlobalManagerFindsPackedYuva444Coders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PackedYuva444TextureCoder.IsSupported(format));
        Assert.IsType<PackedYuva444TextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(RepresentativePlanarYuvFormats))]
    public void GlobalManagerFindsPlanarYuvCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PlanarYuvTextureCoder.IsSupported(format));
        Assert.IsType<PlanarYuvTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(DepthStencilFormats))]
    public void GlobalManagerFindsDepthStencilCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(DepthStencilTextureCoder.IsSupported(format));
        Assert.IsType<DepthStencilTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(FirstBatchSequentialFormats))]
    public void GlobalManagerFindsFirstBatchSequentialUncompressedCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(SequentialUncompressedTextureCoder.IsSupported(format));
        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(SecondBatchSequentialFormats))]
    public void GlobalManagerFindsSecondBatchSequentialUncompressedCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(SequentialUncompressedTextureCoder.IsSupported(format));
        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(SrgbSequentialFormats))]
    public void GlobalManagerFindsSrgbSequentialUncompressedCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(SequentialUncompressedTextureCoder.IsSupported(format));
        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(IntegerSequentialFormats))]
    public void GlobalManagerFindsIntegerSequentialUncompressedCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(SequentialUncompressedTextureCoder.IsSupported(format));
        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Fact]
    public void SequentialUncompressedCoderDoesNotClaimPackedUNormFormats()
    {
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgb565UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgba4UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgb5A1UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Rgb10A2UNorm));
        Assert.False(SequentialUncompressedTextureCoder.IsSupported(TextureFormats.Bgra4UNorm));
    }

    [Fact]
    public void GlobalManagerFindsS3tcCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Bc1Rgba);

        Assert.True(S3tcTextureCoder.IsSupported(TextureFormats.Bc1Rgba));
        Assert.IsType<S3tcTextureCoder>(coder);
    }

    [Fact]
    public void DepthStencilCoderRoundTripsDepth16Stencil8()
    {
        byte[] encoded = [0, 0, 0];
        var coder = new DepthStencilTextureCoder(TextureFormats.Depth16Stencil8);

        coder.EncodeDepthStencil([0.5f], [0xabu], 1, 1, encoded);

        var depth = new float[1];
        var stencil = new uint[1];
        coder.DecodeDepthStencil(1, 1, encoded, depth, stencil);

        Assert.Equal([0xab, 0x00, 0x80], encoded);
        Assert.InRange(depth[0], 0.5f, 0.50002f);
        Assert.Equal(0xabu, stencil[0]);
    }

    [Fact]
    public void EncodeAndDecodeDepth16Stencil8UsesDepthStencilCoder()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0.5f, 0xab / 255f, 0f)]);

        var coder = Assert.IsType<DepthStencilTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Depth16Stencil8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xab, 0x00, 0x80], encoded);
        AssertClose(0.5f, decoded.Pixels[0].Red, 0.00002f);
        AssertClose(0xab / 255f, decoded.Pixels[0].Green, 0.0001f);
        Assert.Equal(0f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void DepthStencilCoderPacksStencilIndex4()
    {
        byte[] encoded = [0];
        var coder = new DepthStencilTextureCoder(TextureFormats.StencilIndex4);

        coder.EncodeStencil([0xau, 0x5u], 2, 1, encoded);

        var stencil = new uint[2];
        coder.DecodeStencil(2, 1, encoded, stencil);

        Assert.Equal([0xa5], encoded);
        Assert.Equal([0xau, 0x5u], stencil);
    }

    [Fact]
    public void EncodeAndDecodeBgra8SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([3, 2, 1, 4], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeAlpha8UsesAlphaChannel()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Alpha8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([4], encoded);
        Assert.Equal(0, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(4, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance8UsesRedForRgb()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(9, 20, 30, 40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([9], encoded);
        Assert.Equal(9, decoded.Pixels[0].Red);
        Assert.Equal(9, decoded.Pixels[0].Green);
        Assert.Equal(9, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeIntensity16UsesRedForAllChannels()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(0x1234, 0x5678, 0x9abc, 0xdef0)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Intensity16UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x34, 0x12], encoded);
        Assert.Equal((ushort)0x1234, decoded.Pixels[0].Red);
        Assert.Equal((ushort)0x1234, decoded.Pixels[0].Green);
        Assert.Equal((ushort)0x1234, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)0x1234, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgra8SNormSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra8SNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xfd, 0x02, 0xff, 0x04], encoded);
        Assert.Equal(source.Pixels[0].Red, decoded.Pixels[0].Red);
        Assert.Equal(source.Pixels[0].Green, decoded.Pixels[0].Green);
        Assert.Equal(source.Pixels[0].Blue, decoded.Pixels[0].Blue);
        Assert.Equal(source.Pixels[0].Alpha, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRg32FloatStoresRedAndGreen()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rg32Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x00, 0x80, 0x3f, 0x00, 0x00, 0x00, 0x40], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(2f, decoded.Pixels[0].Green);
        Assert.Equal(0f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance8Alpha8StoresLuminanceAndAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(9, 20, 30, 40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8Alpha8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([9, 40], encoded);
        Assert.Equal(9, decoded.Pixels[0].Red);
        Assert.Equal(9, decoded.Pixels[0].Green);
        Assert.Equal(9, decoded.Pixels[0].Blue);
        Assert.Equal(40, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance16Alpha16SNormStoresLuminanceAndAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba16SNorm>(
            1,
            1,
            [new Rgba16SNorm(-1000, 2000, -3000, 4000)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance16Alpha16SNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x18, 0xfc, 0xa0, 0x0f], encoded);
        Assert.Equal((short)-1000, decoded.Pixels[0].Red);
        Assert.Equal((short)-1000, decoded.Pixels[0].Green);
        Assert.Equal((short)-1000, decoded.Pixels[0].Blue);
        Assert.Equal((short)4000, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance32Alpha32FloatStoresLuminanceAndAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance32Alpha32Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x00, 0x80, 0x3f, 0x00, 0x00, 0x00, 0x41], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(1f, decoded.Pixels[0].Green);
        Assert.Equal(1f, decoded.Pixels[0].Blue);
        Assert.Equal(8f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgr8SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([3, 2, 1], encoded);
        Assert.Equal(1, decoded.Pixels[0].Red);
        Assert.Equal(2, decoded.Pixels[0].Green);
        Assert.Equal(3, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgr16FloatSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba16Float>(
            1,
            1,
            [new Rgba16Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr16Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x44, 0x00, 0x40, 0x00, 0x3c], encoded);
        Assert.Equal((Half)1f, decoded.Pixels[0].Red);
        Assert.Equal((Half)2f, decoded.Pixels[0].Green);
        Assert.Equal((Half)4f, decoded.Pixels[0].Blue);
        Assert.Equal((Half)1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeAbgr8StoresAlphaFirstAndSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Abgr8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([4, 3, 2, 1], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgrx8WritesZeroPaddingAndRestoresOpaqueAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgrx8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([3, 2, 1, 0], encoded);
        Assert.Equal(1, decoded.Pixels[0].Red);
        Assert.Equal(2, decoded.Pixels[0].Green);
        Assert.Equal(3, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgba8SrgbAppliesGammaToRgbAndUNormToAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0f, 0.5f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([255, 0, 188, 64], encoded);
        AssertClose(1f, decoded.Pixels[0].Red, 0.0001f);
        AssertClose(0f, decoded.Pixels[0].Green, 0.0001f);
        AssertClose(0.5f, decoded.Pixels[0].Blue, 0.005f);
        AssertClose(0.25f, decoded.Pixels[0].Alpha, 0.002f);
    }

    [Fact]
    public void EncodeAndDecodeBgr8SrgbSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0.5f, 0f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0, 188, 255], encoded);
        AssertClose(1f, decoded.Pixels[0].Red, 0.0001f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.005f);
        AssertClose(0f, decoded.Pixels[0].Blue, 0.0001f);
        AssertClose(1f, decoded.Pixels[0].Alpha, 0.0001f);
    }

    [Fact]
    public void EncodeAndDecodeBgrx8SrgbWritesZeroPaddingAndRestoresOpaqueAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0f, 0.5f, 1f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgrx8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([255, 188, 0, 0], encoded);
        AssertClose(0f, decoded.Pixels[0].Red, 0.0001f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.005f);
        AssertClose(1f, decoded.Pixels[0].Blue, 0.0001f);
        AssertClose(1f, decoded.Pixels[0].Alpha, 0.0001f);
    }

    [Fact]
    public void EncodeAndDecodeLuminance8Alpha8SrgbStoresGammaLuminanceAndLinearAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0.5f, 0.25f, 0f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8Alpha8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([188, 64], encoded);
        AssertClose(0.5f, decoded.Pixels[0].Red, 0.005f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.005f);
        AssertClose(0.5f, decoded.Pixels[0].Blue, 0.005f);
        AssertClose(0.25f, decoded.Pixels[0].Alpha, 0.002f);
    }

    [Fact]
    public void EncodeAndDecodeRgba8UIntUsesUnsignedCarrier()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([1, 2, 3, 4], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba16SIntUsesSignedCarrier()
    {
        var source = new ArrayTextureBitmap<Rgba16SNorm>(
            1,
            1,
            [new Rgba16SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba16SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0xff, 0x02, 0x00, 0xfd, 0xff, 0x04, 0x00], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeLuminance8SIntStoresSignedLuminance()
    {
        var source = new ArrayTextureBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-9, 20, 30, 40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xf7], encoded);
        Assert.Equal((sbyte)-9, decoded.Pixels[0].Red);
        Assert.Equal((sbyte)-9, decoded.Pixels[0].Green);
        Assert.Equal((sbyte)-9, decoded.Pixels[0].Blue);
        Assert.Equal(sbyte.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance8Alpha8SIntStoresSignedLuminanceAndAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-9, 20, 30, -40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8Alpha8SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xf7, 0xd8], encoded);
        Assert.Equal((sbyte)-9, decoded.Pixels[0].Red);
        Assert.Equal((sbyte)-9, decoded.Pixels[0].Green);
        Assert.Equal((sbyte)-9, decoded.Pixels[0].Blue);
        Assert.Equal((sbyte)-40, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgr32UIntSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba32UNorm>(
            1,
            1,
            [new Rgba32UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr32UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x03, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00], encoded);
        Assert.Equal(1u, decoded.Pixels[0].Red);
        Assert.Equal(2u, decoded.Pixels[0].Green);
        Assert.Equal(3u, decoded.Pixels[0].Blue);
        Assert.Equal(uint.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeAbgr8SIntStoresAlphaFirstAndSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Abgr8SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x04, 0xfd, 0x02, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgra16FloatSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba16Float>(
            1,
            1,
            [new Rgba16Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra16Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x44, 0x00, 0x40, 0x00, 0x3c, 0x00, 0x48], encoded);
        Assert.Equal((Half)1f, decoded.Pixels[0].Red);
        Assert.Equal((Half)2f, decoded.Pixels[0].Green);
        Assert.Equal((Half)4f, decoded.Pixels[0].Blue);
        Assert.Equal((Half)8f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgba64UIntUsesUNormCarrier()
    {
        var source = new ArrayTextureBitmap<Rgba64UNorm>(
            1,
            1,
            [new Rgba64UNorm(1, 2, 0x0102030405060708ul, ulong.MaxValue)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba64UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba64UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(
            [
                0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff
            ],
            encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba64SIntUsesSNormCarrier()
    {
        var source = new ArrayTextureBitmap<Rgba64SNorm>(
            1,
            1,
            [new Rgba64SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba64SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba64SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(
            [
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xfd, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            ],
            encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba64FloatUsesDoubleCarrier()
    {
        var source = new ArrayTextureBitmap<Rgba64Float>(
            1,
            1,
            [new Rgba64Float(1d, 2d, 4d, 8d)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba64Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba64Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(
            [
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf0, 0x3f,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x40,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x40
            ],
            encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgb565UsesPackedUNormCoder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0),
                new Rgba8UNorm(0, 255, 0)
            ]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0xf8, 0xe0, 0x07], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(0, decoded.Pixels[1].Red);
        Assert.Equal(255, decoded.Pixels[1].Green);
        Assert.Equal(0, decoded.Pixels[1].Blue);
    }

    [Fact]
    public void EncodeAndDecodePackedUNormHonorsRowPitch()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(255, 0, 0),
                new Rgba8UNorm(0, 255, 0),
                new Rgba8UNorm(0, 0, 255),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width) + 2;
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        Array.Fill<byte>(encoded, 0x7e);
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0xf8, 0xe0, 0x07, 0x7e, 0x7e, 0x1f, 0x00, 0xff, 0xff, 0x7e, 0x7e], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void EncodeAndDecodeRgba4UsesPackedUNormCoder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 170, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xaf, 0xf0], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(170, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb5A1UsesPackedUNormCoder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(0, 255, 0, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb5A1UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xc1, 0x07], encoded);
        Assert.Equal(0, decoded.Pixels[0].Red);
        Assert.Equal(255, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgra4SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 170, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0xa0], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(170, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance4DoesNotPackAcrossRows()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            5,
            2,
            [
                Nibble(0),
                Nibble(1),
                Nibble(2),
                Nibble(3),
                Nibble(4),
                Nibble(5),
                Nibble(6),
                Nibble(7),
                Nibble(8),
                Nibble(9)
            ]);

        var coder = Assert.IsType<BitPackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(5, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(3, rowPitch);
        Assert.Equal([0x01, 0x23, 0x40, 0x56, 0x78, 0x90], encoded);
        AssertLuminance(decoded.Pixels[0], 0);
        AssertLuminance(decoded.Pixels[4], 4);
        AssertLuminance(decoded.Pixels[5], 5);
        AssertLuminance(decoded.Pixels[9], 9);
    }

    [Fact]
    public void EncodeAndDecodeLuminance4HonorsRowPitchPadding()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            5,
            2,
            [
                Nibble(0),
                Nibble(1),
                Nibble(2),
                Nibble(3),
                Nibble(4),
                Nibble(5),
                Nibble(6),
                Nibble(7),
                Nibble(8),
                Nibble(9)
            ]);

        var coder = Assert.IsType<BitPackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width) + 1;
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        Array.Fill<byte>(encoded, 0x7e);
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(5, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x01, 0x23, 0x40, 0x7e, 0x56, 0x78, 0x90, 0x7e], encoded);
        AssertLuminance(decoded.Pixels[0], 0);
        AssertLuminance(decoded.Pixels[4], 4);
        AssertLuminance(decoded.Pixels[5], 5);
        AssertLuminance(decoded.Pixels[9], 9);
    }

    [Fact]
    public void EncodeAndDecodeAlpha4UsesAlphaChannel()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 255, 255, 0),
                new Rgba8UNorm(0, 0, 0, 255)
            ]);

        var coder = Assert.IsType<BitPackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Alpha4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x0f], encoded);
        Assert.Equal(0, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(0, decoded.Pixels[0].Alpha);
        Assert.Equal(0, decoded.Pixels[1].Red);
        Assert.Equal(0, decoded.Pixels[1].Green);
        Assert.Equal(0, decoded.Pixels[1].Blue);
        Assert.Equal(255, decoded.Pixels[1].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeIntensity4UsesRedForAllChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                Nibble(2),
                Nibble(13)
            ]);

        var coder = Assert.IsType<BitPackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Intensity4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x2d], encoded);
        AssertIntensity(decoded.Pixels[0], 2);
        AssertIntensity(decoded.Pixels[1], 13);
    }

    [Fact]
    public void EncodeAndDecodeBw1UsesRedAndHonorsRowPitchPadding()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            9,
            2,
            [
                new Rgba32Float(0f, 0f, 0f),
                new Rgba32Float(1f, 1f, 1f),
                new Rgba32Float(0f, 1f, 1f),
                new Rgba32Float(1f, 0f, 0f),
                new Rgba32Float(0.5f, 0f, 0f),
                new Rgba32Float(0f, 0f, 1f),
                new Rgba32Float(0.49f, 0.49f, 0.49f),
                new Rgba32Float(0.5f, 0.5f, 0.5f),
                new Rgba32Float(1f, 1f, 1f),
                new Rgba32Float(1f, 1f, 1f),
                new Rgba32Float(0f, 0f, 0f),
                new Rgba32Float(1f, 1f, 1f),
                new Rgba32Float(0f, 0f, 0f),
                new Rgba32Float(1f, 1f, 1f),
                new Rgba32Float(0f, 0f, 0f),
                new Rgba32Float(1f, 1f, 1f),
                new Rgba32Float(0f, 0f, 0f),
                new Rgba32Float(0f, 0f, 0f)
            ]);

        var coder = Assert.IsType<BitPackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bw1BppUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width) + 1;
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        Array.Fill<byte>(encoded, 0x7e);
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(9, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(2, coder.GetDefaultPitch(source.Width));
        Assert.Equal([0x59, 0x80, 0x7e, 0xaa, 0x00, 0x7e], encoded);
        Assert.Equal(0, decoded.Pixels[0].Red);
        Assert.Equal(255, decoded.Pixels[1].Red);
        Assert.Equal(0, decoded.Pixels[2].Red);
        Assert.Equal(255, decoded.Pixels[3].Red);
        Assert.Equal(255, decoded.Pixels[8].Red);
        Assert.Equal(255, decoded.Pixels[9].Red);
        Assert.Equal(0, decoded.Pixels[17].Red);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.Uyvy422UNorm), new byte[] { 128, 0, 128, 255 })]
    [InlineData(nameof(TextureFormats.Yuy2UNorm), new byte[] { 0, 128, 255, 128 })]
    public void EncodeAndDecodePackedYuv4228BitUsesSharedChroma(string formatName, byte[] expected)
    {
        var format = formatName == nameof(TextureFormats.Uyvy422UNorm)
            ? TextureFormats.Uyvy422UNorm
            : TextureFormats.Yuy2UNorm;
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<PackedYuv422TextureCoder>(TextureCoderManager.Global.GetCoder(format));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodePackedYuv4228BitHonorsRowPitchPadding()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255),
                new Rgba8UNorm(255, 255, 255),
                new Rgba8UNorm(0, 0, 0)
            ]);

        var coder = Assert.IsType<PackedYuv422TextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Uyvy422UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width) + 2;
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        Array.Fill<byte>(encoded, 0x7e);
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([128, 0, 128, 255, 0x7e, 0x7e, 128, 255, 128, 0, 0x7e, 0x7e], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void PackedYuv4228BitRejectsOddWidth()
    {
        var coder = Assert.IsType<PackedYuv422TextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Yuy2UNorm));
        var rowPitch = coder.GetDefaultPitch(1);

        Assert.Throws<ArgumentException>(() => coder.GetEncodedByteCount(1, 1, rowPitch));
    }

    [Fact]
    public void EncodeAndDecodePackedYuv42216BitUsesSharedChroma()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            2,
            1,
            [
                new Rgba16UNorm(0, 0, 0),
                new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)
            ]);

        var coder = Assert.IsType<PackedYuv422TextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Uyvy16_422UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0xff, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodePlanarYuv420UsesVariablePayload()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255),
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<PlanarYuvTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Yuv3P420UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(2, rowPitch);
        Assert.Equal([0, 255, 0, 255, 128, 128], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void PlanarYuvRejectsRowPitchOverride()
    {
        var coder = Assert.IsType<PlanarYuvTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Yuv3P420UNorm));
        var rowPitch = coder.GetDefaultPitch(2) + 1;

        Assert.Throws<NotSupportedException>(() => coder.GetEncodedByteCount(2, 2, rowPitch));
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2UsesRgba16Carrier()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, ushort.MaxValue, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x0f, 0xc0, 0xff], encoded);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Blue);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRg4StoresRedAndGreen()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 170, 85, 34)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rg4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xfa], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(170, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(255, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeAlpha12UsesAlphaChannel()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1, 2, 3, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Alpha12UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x0f], encoded);
        Assert.Equal(0, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeIntensity12UsesRedForAllChannels()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Intensity12UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x0f], encoded);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Green);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Blue);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeLuminance4Alpha4StoresLuminanceAndAlpha()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(170, 0, 0, 85)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance4Alpha4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xa5], encoded);
        Assert.Equal(170, decoded.Pixels[0].Red);
        Assert.Equal(170, decoded.Pixels[0].Green);
        Assert.Equal(170, decoded.Pixels[0].Blue);
        Assert.Equal(85, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb565RevReversesBitOrder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565RevUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x1f, 0x00], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
    }

    [Fact]
    public void EncodeAndDecodeBgr565SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr565UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x1f, 0x00], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
    }

    [Fact]
    public void EncodeAndDecodeArgb4StoresAlphaFirst()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [NibbleRgba(1, 2, 3, 4)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Argb4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x23, 0x41], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeAbgr4RevStoresAlphaFirstAndReversesBitOrder()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [NibbleRgba(1, 2, 3, 4)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Abgr4RevUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x34, 0x12], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgr10A2RevUNormUsesRgba16Carrier()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, ushort.MaxValue, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x03, 0xf0, 0xff], encoded);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Blue);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgba12UsesSixBytes()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba12UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x0f, 0x00, 0x00, 0xf0, 0xff], encoded);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
        Assert.Equal(ushort.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeR11G11B10FloatUsesPackedFloatCoder()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f)]);

        var coder = Assert.IsType<PackedFloatTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R11G11B10Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xc0, 0x03, 0x20, 0x88], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(2f, decoded.Pixels[0].Green);
        Assert.Equal(4f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb9E5UsesSharedExponent()
    {
        var source = new ArrayTextureBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 1f, 1f)]);

        var coder = Assert.IsType<PackedFloatTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb9E5));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x01, 0x02, 0x84], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(1f, decoded.Pixels[0].Green);
        Assert.Equal(1f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2UIntUsesPackedIntegerCoder()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1023, 0, 512, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x03, 0x08, 0xc0, 0xff], encoded);
        Assert.Equal((ushort)1023, decoded.Pixels[0].Red);
        Assert.Equal((ushort)0, decoded.Pixels[0].Green);
        Assert.Equal((ushort)512, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2RevUIntReversesBitOrder()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1, 2, 3, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2RevUInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x01, 0x08, 0x30, 0xc0], encoded);
        Assert.Equal((ushort)1, decoded.Pixels[0].Red);
        Assert.Equal((ushort)2, decoded.Pixels[0].Green);
        Assert.Equal((ushort)3, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgr10A2RevUIntSwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1023, 1, 512, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevUInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x06, 0xf0, 0xff], encoded);
        Assert.Equal((ushort)1023, decoded.Pixels[0].Red);
        Assert.Equal((ushort)1, decoded.Pixels[0].Green);
        Assert.Equal((ushort)512, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }

    private static Rgba8UNorm Nibble(int value) =>
        new((byte)(value * 17), 0, 0);

    private static Rgba8UNorm NibbleRgba(int red, int green, int blue, int alpha) =>
        new((byte)(red * 17), (byte)(green * 17), (byte)(blue * 17), (byte)(alpha * 17));

    private static void AssertLuminance(Rgba8UNorm pixel, int nibble)
    {
        var value = (byte)(nibble * 17);
        Assert.Equal(value, pixel.Red);
        Assert.Equal(value, pixel.Green);
        Assert.Equal(value, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }

    private static void AssertIntensity(Rgba8UNorm pixel, int nibble)
    {
        var value = (byte)(nibble * 17);
        Assert.Equal(value, pixel.Red);
        Assert.Equal(value, pixel.Green);
        Assert.Equal(value, pixel.Blue);
        Assert.Equal(value, pixel.Alpha);
    }

    private static void AssertClose(float expected, float actual, float tolerance)
    {
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);
    }

    public static TheoryData<TextureFormat> PackedUNormFormats() => new()
    {
        TextureFormats.Alpha12UNorm,
        TextureFormats.Luminance12UNorm,
        TextureFormats.Luminance4Alpha4UNorm,
        TextureFormats.Luminance6Alpha2UNorm,
        TextureFormats.Luminance12Alpha4UNorm,
        TextureFormats.Luminance12Alpha12UNorm,
        TextureFormats.Intensity12UNorm,
        TextureFormats.Rg4UNorm,
        TextureFormats.R3G3B2UNorm,
        TextureFormats.R3G3B2RevUNorm,
        TextureFormats.Rgb4UNorm,
        TextureFormats.Rgb5UNorm,
        TextureFormats.Rgb565UNorm,
        TextureFormats.Rgb565RevUNorm,
        TextureFormats.Bgr565UNorm,
        TextureFormats.Bgr565RevUNorm,
        TextureFormats.Rgb10UNorm,
        TextureFormats.Rgb12UNorm,
        TextureFormats.Rgba2UNorm,
        TextureFormats.Rgba4UNorm,
        TextureFormats.Rgba4RevUNorm,
        TextureFormats.Argb4UNorm,
        TextureFormats.Argb4RevUNorm,
        TextureFormats.Abgr4UNorm,
        TextureFormats.Abgr4RevUNorm,
        TextureFormats.Rgb5A1UNorm,
        TextureFormats.Rgb5A1RevUNorm,
        TextureFormats.A1Rgb5UNorm,
        TextureFormats.A1Rgb5RevUNorm,
        TextureFormats.A1Bgr5UNorm,
        TextureFormats.A1Bgr5RevUNorm,
        TextureFormats.Rgb10A2UNorm,
        TextureFormats.Rgb10A2RevUNorm,
        TextureFormats.Bgr10A2RevUNorm,
        TextureFormats.Rgba12UNorm,
        TextureFormats.Bgra4UNorm,
        TextureFormats.Bgra4RevUNorm,
        TextureFormats.Bgr5A1UNorm,
        TextureFormats.Bgr5A1RevUNorm
    };

    public static TheoryData<TextureFormat> RepresentativePackedYuv422Formats() => new()
    {
        TextureFormats.Uyvy422UNorm,
        TextureFormats.Yuy2UNorm,
        TextureFormats.Uyvy16_422UNorm,
        TextureFormats.Yuyv10Msb422UNorm
    };

    public static TheoryData<TextureFormat> RepresentativePackedYuva444Formats() => new()
    {
        TextureFormats.Vyua10Msb444UNorm,
        TextureFormats.Uyv10A2_444UNorm,
        TextureFormats.Uyva16_444UNorm
    };

    public static TheoryData<TextureFormat> RepresentativePlanarYuvFormats() => new()
    {
        TextureFormats.Yuv3P420UNorm,
        TextureFormats.Yuv2P420UNorm,
        TextureFormats.Yvu10Lsb2P422UNorm
    };

    public static TheoryData<TextureFormat> DepthStencilFormats() => new()
    {
        TextureFormats.DepthComponent8,
        TextureFormats.DepthComponent16,
        TextureFormats.DepthComponent24,
        TextureFormats.DepthComponent32,
        TextureFormats.DepthComponent32Float,
        TextureFormats.StencilIndex1,
        TextureFormats.StencilIndex4,
        TextureFormats.StencilIndex8,
        TextureFormats.StencilIndex16,
        TextureFormats.Depth16Stencil8,
        TextureFormats.Depth24Stencil8,
        TextureFormats.Depth32Stencil8,
        TextureFormats.Depth32FloatStencil8
    };

    public static TheoryData<TextureFormat> FirstBatchSequentialFormats() => new()
    {
        TextureFormats.Alpha8UNorm,
        TextureFormats.Alpha8SNorm,
        TextureFormats.Alpha16UNorm,
        TextureFormats.Alpha16SNorm,
        TextureFormats.Alpha32UNorm,
        TextureFormats.Alpha32SNorm,
        TextureFormats.Alpha16Float,
        TextureFormats.Alpha32Float,
        TextureFormats.Luminance8UNorm,
        TextureFormats.Luminance16UNorm,
        TextureFormats.Luminance32UNorm,
        TextureFormats.Luminance32SNorm,
        TextureFormats.Luminance16Float,
        TextureFormats.Luminance32Float,
        TextureFormats.Intensity8UNorm,
        TextureFormats.Intensity8SNorm,
        TextureFormats.Intensity16UNorm,
        TextureFormats.Intensity16SNorm,
        TextureFormats.Intensity32UNorm,
        TextureFormats.Intensity32SNorm,
        TextureFormats.Intensity16Float,
        TextureFormats.Intensity32Float,
        TextureFormats.R8SNorm,
        TextureFormats.R16UNorm,
        TextureFormats.R16SNorm,
        TextureFormats.R32UNorm,
        TextureFormats.R32SNorm,
        TextureFormats.R16Float,
        TextureFormats.R32Float,
        TextureFormats.R64Float,
        TextureFormats.Rg8SNorm,
        TextureFormats.Rg16UNorm,
        TextureFormats.Rg16SNorm,
        TextureFormats.Rg32UNorm,
        TextureFormats.Rg32SNorm,
        TextureFormats.Rg16Float,
        TextureFormats.Rg32Float,
        TextureFormats.Rg64Float,
        TextureFormats.Rgb8SNorm,
        TextureFormats.Rgb16UNorm,
        TextureFormats.Rgb16SNorm,
        TextureFormats.Rgb32UNorm,
        TextureFormats.Rgb32SNorm,
        TextureFormats.Rgb16Float,
        TextureFormats.Rgb32Float,
        TextureFormats.Rgb64Float,
        TextureFormats.Rgba64Float,
        TextureFormats.Bgra8SNorm,
        TextureFormats.Bgra16UNorm,
        TextureFormats.Bgra16SNorm,
        TextureFormats.Bgra32UNorm,
        TextureFormats.Bgra32SNorm,
        TextureFormats.Bgra16Float,
        TextureFormats.Bgra32Float
    };

    public static TheoryData<TextureFormat> SecondBatchSequentialFormats() => new()
    {
        TextureFormats.Luminance8Alpha8UNorm,
        TextureFormats.Luminance16Alpha16UNorm,
        TextureFormats.Luminance16Alpha16SNorm,
        TextureFormats.Luminance16Alpha16Float,
        TextureFormats.Luminance32Alpha32UNorm,
        TextureFormats.Luminance32Alpha32SNorm,
        TextureFormats.Luminance32Alpha32Float,
        TextureFormats.Bgr8UNorm,
        TextureFormats.Bgr8SNorm,
        TextureFormats.Bgr16UNorm,
        TextureFormats.Bgr16SNorm,
        TextureFormats.Bgr32UNorm,
        TextureFormats.Bgr32SNorm,
        TextureFormats.Bgr16Float,
        TextureFormats.Bgr32Float,
        TextureFormats.Abgr8UNorm,
        TextureFormats.Abgr8SNorm,
        TextureFormats.Bgrx8UNorm
    };

    public static TheoryData<TextureFormat> SrgbSequentialFormats() => new()
    {
        TextureFormats.Luminance8Srgb,
        TextureFormats.Luminance8Alpha8Srgb,
        TextureFormats.R8Srgb,
        TextureFormats.Rg8Srgb,
        TextureFormats.Rgb8Srgb,
        TextureFormats.Bgr8Srgb,
        TextureFormats.Rgba8Srgb,
        TextureFormats.Abgr8Srgb,
        TextureFormats.Bgra8Srgb,
        TextureFormats.Bgrx8Srgb
    };

    public static TheoryData<TextureFormat> IntegerSequentialFormats() => new()
    {
        TextureFormats.Alpha8UInt,
        TextureFormats.Alpha8SInt,
        TextureFormats.Alpha16UInt,
        TextureFormats.Alpha16SInt,
        TextureFormats.Alpha32UInt,
        TextureFormats.Alpha32SInt,
        TextureFormats.Luminance8UInt,
        TextureFormats.Luminance8SInt,
        TextureFormats.Luminance16UInt,
        TextureFormats.Luminance16SInt,
        TextureFormats.Luminance32UInt,
        TextureFormats.Luminance32SInt,
        TextureFormats.Luminance8Alpha8UInt,
        TextureFormats.Luminance8Alpha8SInt,
        TextureFormats.Luminance16Alpha16UInt,
        TextureFormats.Luminance16Alpha16SInt,
        TextureFormats.Luminance32Alpha32UInt,
        TextureFormats.Luminance32Alpha32SInt,
        TextureFormats.Intensity8UInt,
        TextureFormats.Intensity8SInt,
        TextureFormats.Intensity16UInt,
        TextureFormats.Intensity16SInt,
        TextureFormats.Intensity32UInt,
        TextureFormats.Intensity32SInt,
        TextureFormats.R8UInt,
        TextureFormats.R8SInt,
        TextureFormats.R16UInt,
        TextureFormats.R16SInt,
        TextureFormats.R32UInt,
        TextureFormats.R32SInt,
        TextureFormats.R64UInt,
        TextureFormats.R64SInt,
        TextureFormats.Rg8UInt,
        TextureFormats.Rg8SInt,
        TextureFormats.Rg16UInt,
        TextureFormats.Rg16SInt,
        TextureFormats.Rg32UInt,
        TextureFormats.Rg32SInt,
        TextureFormats.Rg64UInt,
        TextureFormats.Rg64SInt,
        TextureFormats.Rgb8UInt,
        TextureFormats.Rgb8SInt,
        TextureFormats.Rgb16UInt,
        TextureFormats.Rgb16SInt,
        TextureFormats.Rgb32UInt,
        TextureFormats.Rgb32SInt,
        TextureFormats.Rgb64UInt,
        TextureFormats.Rgb64SInt,
        TextureFormats.Bgr8UInt,
        TextureFormats.Bgr8SInt,
        TextureFormats.Bgr16UInt,
        TextureFormats.Bgr16SInt,
        TextureFormats.Bgr32UInt,
        TextureFormats.Bgr32SInt,
        TextureFormats.Abgr8UInt,
        TextureFormats.Abgr8SInt,
        TextureFormats.Rgba8UInt,
        TextureFormats.Rgba8SInt,
        TextureFormats.Rgba16UInt,
        TextureFormats.Rgba16SInt,
        TextureFormats.Rgba32UInt,
        TextureFormats.Rgba32SInt,
        TextureFormats.Rgba64UInt,
        TextureFormats.Rgba64SInt,
        TextureFormats.Bgra8UInt,
        TextureFormats.Bgra8SInt,
        TextureFormats.Bgra16UInt,
        TextureFormats.Bgra16SInt,
        TextureFormats.Bgra32UInt,
        TextureFormats.Bgra32SInt
    };
}
