using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class TextureCoderManagerTests
{
    [Fact]
    public void RegisterFormatsUsesFactoryForEachFormat()
    {
        var manager = new TextureCoderManager();

        var registration = manager.Register(
            [TextureFormats.Rgba8UNorm, TextureFormats.Bc1Rgba],
            format => new TestTextureCoder(format));

        Assert.IsType<TestTextureCoder>(manager.GetCoder(TextureFormats.Rgba8UNorm));
        Assert.IsType<TestTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));

        registration.Dispose();

        Assert.IsType<SequentialUncompressedTextureCoder>(manager.GetCoder(TextureFormats.Rgba8UNorm));
        Assert.IsType<S3tcTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
    }

    [Fact]
    public void RegisterFormatsRollsBackWhenFactoryThrows()
    {
        var manager = new TextureCoderManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.Register(
                [TextureFormats.Rgba8UNorm, TextureFormats.Bc1Rgba],
                format =>
                {
                    if (format == TextureFormats.Bc1Rgba)
                    {
                        throw new InvalidOperationException();
                    }

                    return new TestTextureCoder(format);
                }));

        Assert.IsType<SequentialUncompressedTextureCoder>(manager.GetCoder(TextureFormats.Rgba8UNorm));
    }

    [Fact]
    public void CombineDisposesRegistrationsInReverseOrder()
    {
        var manager = new TextureCoderManager();
        var disposed = new List<int>();

        using (manager.Combine(
            new TestRegistration(() => disposed.Add(1)),
            new TestRegistration(() => disposed.Add(2))))
        {
        }

        Assert.Equal([2, 1], disposed);
    }

    [Fact]
    public void CombineDisposesOnlyOnce()
    {
        var manager = new TextureCoderManager();
        var disposeCount = 0;
        var registration = manager.Combine(new TestRegistration(() => disposeCount++));

        registration.Dispose();
        registration.Dispose();

        Assert.Equal(1, disposeCount);
    }

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

    [Theory]
    [MemberData(nameof(RepresentativePackedIntegerFormats))]
    public void GlobalManagerFindsPackedIntegerCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PackedIntegerTextureCoder.IsSupported(format));
        Assert.IsType<PackedIntegerTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(XrFormats))]
    public void GlobalManagerFindsXrCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(XrTextureCoder.IsSupported(format));
        Assert.IsType<XrTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(IndexedFormats))]
    public void GlobalManagerFindsIndexedCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(IndexedTextureCoder.IsSupported(format));
        Assert.IsType<IndexedTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsNv11Coder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Nv11UNorm);

        Assert.True(Nv11TextureCoder.IsSupported(TextureFormats.Nv11UNorm));
        Assert.IsType<Nv11TextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerFindsPackedSNormCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevSNorm);

        Assert.IsType<PackedSNormTextureCoder>(coder);
    }

    [Theory]
    [MemberData(nameof(ConsolePackedFormats))]
    public void GlobalManagerFindsConsolePackedCoders(TextureFormat format, Type expectedCoderType)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PackedUNormTextureCoder.IsSupported(format) || PackedSNormTextureCoder.IsSupported(format));
        Assert.Equal(expectedCoderType, coder.GetType());
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
    public void DepthStencilCoderRoundTripsD24FS8With20e4Depth()
    {
        byte[] encoded = [0, 0, 0, 0];
        var coder = new DepthStencilTextureCoder(TextureFormats.Depth24FloatStencil8);

        coder.EncodeDepthStencil([1f], [0xffu], 1, 1, encoded);

        var depth = new float[1];
        var stencil = new uint[1];
        coder.DecodeDepthStencil(1, 1, encoded, depth, stencil);

        Assert.Equal([0xff, 0x00, 0x00, 0xf0], encoded);
        Assert.Equal(1f, depth[0]);
        Assert.Equal(0xffu, stencil[0]);
    }

    [Fact]
    public void EncodeAndDecodeDepth16Stencil8UsesDepthStencilCoder()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0.5f, 0xab / 255f, 0f)]);

        var coder = Assert.IsType<DepthStencilTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Depth16Stencil8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xab, 0x00, 0x80], encoded);
        AssertClose(0.5f, decoded.Pixels[0].Red, 0.00002f);
        AssertClose(0xab / 255f, decoded.Pixels[0].Green, 0.0001f);
        Assert.Equal(0f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeD24FS8UsesFloat24DepthAndStencil()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 1f, 0f)]);

        var coder = Assert.IsType<DepthStencilTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Depth24FloatStencil8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x00, 0x00, 0xf0], encoded);
        Assert.Equal(1f, decoded.Pixels[0].Red);
        Assert.Equal(1f, decoded.Pixels[0].Green);
        Assert.Equal(0f, decoded.Pixels[0].Blue);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeDepth24X8StoresDepthInHighBits()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0.5f, 0f, 0f)]);

        var coder = Assert.IsType<DepthStencilTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Depth24X8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x00, 0x00, 0x80], encoded);
        AssertClose(0.5f, decoded.Pixels[0].Red, 0.000001f);
        Assert.Equal(0f, decoded.Pixels[0].Green);
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
    public void X24Stencil8StoresStencilInLowByte()
    {
        byte[] encoded = [0xff, 0xff, 0xff, 0xff];
        var coder = new DepthStencilTextureCoder(TextureFormats.X24Stencil8);

        coder.EncodeStencil([0xabu], 1, 1, encoded);

        var stencil = new uint[1];
        coder.DecodeStencil(1, 1, encoded, stencil);

        Assert.Equal([0xab, 0x00, 0x00, 0x00], encoded);
        Assert.Equal(0xabu, stencil[0]);
    }

    [Fact]
    public void X32Stencil8StoresStencilAfterX32Padding()
    {
        byte[] encoded = [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff];
        var coder = new DepthStencilTextureCoder(TextureFormats.X32Stencil8);

        coder.EncodeStencil([0xabu], 1, 1, encoded);

        var stencil = new uint[1];
        coder.DecodeStencil(1, 1, encoded, stencil);

        Assert.Equal([0x00, 0x00, 0x00, 0x00, 0xab, 0x00, 0x00, 0x00], encoded);
        Assert.Equal(0xabu, stencil[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgr10XRUsesPackedBgrOrder()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0.5f, 0f)]);

        var coder = Assert.IsType<XrTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10XR));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x80, 0xfd, 0xe9, 0x37], encoded);
        AssertClose(1f, decoded.Pixels[0].Red, 0.000001f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.000001f);
        AssertClose(0f, decoded.Pixels[0].Blue, 0.000001f);
        Assert.Equal(1f, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgra10XRUsesPaddedWords()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0.5f, 0f, 1f)]);

        var coder = Assert.IsType<XrTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra10XR));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x60, 0xc0, 0x9f, 0x80, 0xdf, 0x80, 0xdf], encoded);
        AssertClose(1f, decoded.Pixels[0].Red, 0.000001f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.000001f);
        AssertClose(0f, decoded.Pixels[0].Blue, 0.000001f);
        AssertClose(1f, decoded.Pixels[0].Alpha, 0.000001f);
    }

    [Fact]
    public void EncodeAndDecodeBgr10XRSrgbAppliesSrgbTransfer()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0.25f, 0.5f, 1f)]);

        var coder = Assert.IsType<XrTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10XRSrgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        AssertClose(0.25f, decoded.Pixels[0].Red, 0.002f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.002f);
        AssertClose(1f, decoded.Pixels[0].Blue, 0.002f);
    }

    [Fact]
    public void EncodeAndDecodeRgb10XRA2UsesDxgiBitOrder()
    {
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0.5f, 0f, 1f)]);

        var coder = Assert.IsType<XrTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10XRA2UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x7e, 0xff, 0x09, 0xd8], encoded);
        AssertClose(1f, decoded.Pixels[0].Red, 0.000001f);
        AssertClose(0.5f, decoded.Pixels[0].Green, 0.000001f);
        AssertClose(0f, decoded.Pixels[0].Blue, 0.000001f);
        AssertClose(1f, decoded.Pixels[0].Alpha, 0.000001f);
    }

    [Fact]
    public void EncodeAndDecodeBgra8SwizzlesChannels()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([3, 2, 1, 4], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeAlpha8UsesAlphaChannel()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Alpha8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(9, 20, 30, 40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(0x1234, 0x5678, 0x9abc, 0xdef0)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Intensity16UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra8SNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8SNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rg32Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(9, 20, 30, 40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8Alpha8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16SNorm>(
            1,
            1,
            [new Rgba16SNorm(-1000, 2000, -3000, 4000)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance16Alpha16SNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16SNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance32Alpha32Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16Float>(
            1,
            1,
            [new Rgba16Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr16Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Abgr8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([4, 3, 2, 1], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgrx8WritesZeroPaddingAndRestoresOpaqueAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgrx8UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0f, 0.5f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 0.5f, 0f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0f, 0.5f, 1f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgrx8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(0.5f, 0.25f, 0f, 0.25f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8Alpha8Srgb));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([1, 2, 3, 4], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba16SIntUsesSignedCarrier()
    {
        var source = new ArrayBitmap<Rgba16SNorm>(
            1,
            1,
            [new Rgba16SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba16SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0xff, 0x02, 0x00, 0xfd, 0xff, 0x04, 0x00], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeLuminance8SIntStoresSignedLuminance()
    {
        var source = new ArrayBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-9, 20, 30, 40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8SNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-9, 20, 30, -40)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance8Alpha8SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8SNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba32UNorm>(
            1,
            1,
            [new Rgba32UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr32UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8SNorm>(
            1,
            1,
            [new Rgba8SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Abgr8SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x04, 0xfd, 0x02, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgra16FloatSwizzlesChannels()
    {
        var source = new ArrayBitmap<Rgba16Float>(
            1,
            1,
            [new Rgba16Float(1f, 2f, 4f, 8f)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra16Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba64UNorm>(
            1,
            1,
            [new Rgba64UNorm(1, 2, 0x0102030405060708ul, ulong.MaxValue)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba64UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba64UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba64SNorm>(
            1,
            1,
            [new Rgba64SNorm(-1, 2, -3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba64SInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba64SNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba64Float>(
            1,
            1,
            [new Rgba64Float(1d, 2d, 4d, 8d)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba64Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba64Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 2);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 170, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(0, 255, 0, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb5A1UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 170, 255)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 2);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(5, 2);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x2d], encoded);
        AssertIntensity(decoded.Pixels[0], 2);
        AssertIntensity(decoded.Pixels[1], 13);
    }

    [Fact]
    public void EncodeAndDecodeBw1UsesRedAndHonorsRowPitchPadding()
    {
        var source = new ArrayBitmap<Rgba32Float>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(9, 2);
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
    [InlineData(nameof(TextureFormats.Ai44), new byte[] { 0xa5 })]
    [InlineData(nameof(TextureFormats.Ia44), new byte[] { 0x5a })]
    public void EncodeAndDecodeIndexed4BitFormatsUseDocumentedNibbleOrder(string formatName, byte[] expected)
    {
        var format = formatName == nameof(TextureFormats.Ai44)
            ? TextureFormats.Ai44
            : TextureFormats.Ia44;
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(0x55, 0x55, 0x55, 0xaa)]);

        var coder = Assert.IsType<IndexedTextureCoder>(TextureCoderManager.Global.GetCoder(format));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.P8), new byte[] { 0x7f })]
    [InlineData(nameof(TextureFormats.A8P8), new byte[] { 0x7f, 0xee })]
    public void EncodeAndDecodeIndexed8BitFormatsPreserveIndexAndAlpha(string formatName, byte[] expected)
    {
        var format = formatName == nameof(TextureFormats.P8)
            ? TextureFormats.P8
            : TextureFormats.A8P8;
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(0x7f, 0x7f, 0x7f, format == TextureFormats.A8P8 ? (byte)0xee : byte.MaxValue)]);

        var coder = Assert.IsType<IndexedTextureCoder>(TextureCoderManager.Global.GetCoder(format));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.Uyvy422UNorm), new byte[] { 128, 0, 128, 255 })]
    [InlineData(nameof(TextureFormats.Yuy2UNorm), new byte[] { 0, 128, 255, 128 })]
    public void EncodeAndDecodePackedYuv4228BitUsesSharedChroma(string formatName, byte[] expected)
    {
        var format = formatName == nameof(TextureFormats.Uyvy422UNorm)
            ? TextureFormats.Uyvy422UNorm
            : TextureFormats.Yuy2UNorm;
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodePackedYuv4228BitHonorsRowPitchPadding()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 2);
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
    public void EncodeAndDecodeAyuv444UsesDxgiViewByteOrder()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 255, 255, 128)]);

        var coder = Assert.IsType<PackedYuva444TextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Ayuv444UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([128, 128, 255, 128], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodePackedYuv42216BitUsesSharedChroma()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
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

        var decoded = new ArrayBitmap<Rgba16UNorm>(2, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0xff, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
    }

    [Fact]
    public void EncodeAndDecodePlanarYuv420UsesVariablePayload()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
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

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(2, rowPitch);
        Assert.Equal([0, 255, 0, 255, 128, 128], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void EncodeAndDecodeV208UsesPlanarYuv440Layout()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255),
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<PlanarYuvTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.V208UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(2, rowPitch);
        Assert.Equal([0, 255, 0, 255, 128, 128, 128, 128], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void EncodeAndDecodeV408UsesPlanarYuv444Layout()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            2,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255),
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<PlanarYuvTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.V408UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(2, rowPitch);
        Assert.Equal([0, 255, 0, 255, 128, 128, 128, 128, 128, 128, 128, 128], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void EncodeAndDecodeNv11UsesDxgiPadded411Layout()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            1,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255),
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var coder = Assert.IsType<Nv11TextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Nv11UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(4, rowPitch);
        Assert.Equal([0, 255, 0, 255, 128, 128, 0, 0], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
        Assert.Equal(source.Pixels[1], decoded.Pixels[1]);
        Assert.Equal(source.Pixels[2], decoded.Pixels[2]);
        Assert.Equal(source.Pixels[3], decoded.Pixels[3]);
    }

    [Fact]
    public void Nv11RejectsWidthThatIsNotMultipleOfFour()
    {
        var coder = Assert.IsType<Nv11TextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Nv11UNorm));
        var rowPitch = coder.GetDefaultPitch(5);

        Assert.Throws<ArgumentException>(() => coder.GetEncodedByteCount(5, 1, rowPitch));
    }

    [Fact]
    public void EncodeAndDecodePlanarYuv12Msb2P444UsesInterleavedChroma()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)]);

        var coder = Assert.IsType<PlanarYuvTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Yuv12Msb2P444UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(2, rowPitch);
        Assert.Equal([0xf0, 0xff, 0x00, 0x80, 0x00, 0x80], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodePlanarYuv14Msb2P420UsesInterleavedChroma()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            2,
            2,
            [
                new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
                new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
                new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue),
                new Rgba16UNorm(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)
            ]);

        var coder = Assert.IsType<PlanarYuvTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Yuv14Msb2P420UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(2, 2);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(4, rowPitch);
        Assert.Equal([0xfc, 0xff, 0xfc, 0xff, 0xfc, 0xff, 0xfc, 0xff, 0x00, 0x80, 0x00, 0x80], encoded);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, ushort.MaxValue, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 170, 85, 34)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rg4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1, 2, 3, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Alpha12UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Intensity12UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(170, 0, 0, 85)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Luminance4Alpha4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb565RevUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x1f, 0x00], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
    }

    [Fact]
    public void EncodeAndDecodeBgr565SwizzlesChannels()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(255, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr565UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x1f, 0x00], encoded);
        Assert.Equal(255, decoded.Pixels[0].Red);
        Assert.Equal(0, decoded.Pixels[0].Green);
        Assert.Equal(0, decoded.Pixels[0].Blue);
    }

    [Fact]
    public void EncodeAndDecodeArgb4StoresAlphaFirst()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [NibbleRgba(1, 2, 3, 4)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Argb4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x23, 0x41], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeAbgr4RevStoresAlphaFirstAndReversesBitOrder()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            1,
            1,
            [NibbleRgba(1, 2, 3, 4)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Abgr4RevUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x34, 0x12], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeBgr10A2RevUNormUsesRgba16Carrier()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, ushort.MaxValue, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevUNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgba12UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 2f, 4f)]);

        var coder = Assert.IsType<PackedFloatTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R11G11B10Float));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba32Float>(
            1,
            1,
            [new Rgba32Float(1f, 1f, 1f)]);

        var coder = Assert.IsType<PackedFloatTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb9E5));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1023, 0, 512, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1, 2, 3, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2RevUInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
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
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(1023, 1, 512, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevUInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x00, 0x06, 0xf0, 0xff], encoded);
        Assert.Equal((ushort)1023, decoded.Pixels[0].Red);
        Assert.Equal((ushort)1, decoded.Pixels[0].Green);
        Assert.Equal((ushort)512, decoded.Pixels[0].Blue);
        Assert.Equal((ushort)3, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeAndDecodeBgr10A2RevSNormUsesPackedSNormCoder()
    {
        var source = new ArrayBitmap<Rgba16SNorm>(
            1,
            1,
            [new Rgba16SNorm(short.MaxValue, 0, -short.MaxValue, -short.MaxValue)]);

        var coder = Assert.IsType<PackedSNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgr10A2RevSNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0x01, 0x02, 0xf0, 0xdf], encoded);
        Assert.Equal(short.MaxValue, decoded.Pixels[0].Red);
        Assert.Equal((short)0, decoded.Pixels[0].Green);
        Assert.Equal((short)-short.MaxValue, decoded.Pixels[0].Blue);
        Assert.Equal((short)-short.MaxValue, decoded.Pixels[0].Alpha);
    }

    [Theory]
    [MemberData(nameof(ConsolePackedRoundTripCases))]
    public void EncodeAndDecodeConsolePackedFormats(TextureFormat format, Rgba32Float sourcePixel, byte[] expected, Rgba32Float expectedPixel)
    {
        var source = new ArrayBitmap<Rgba32Float>(1, 1, [sourcePixel]);

        var coder = Assert.IsAssignableFrom<IPitchTextureCoder>(TextureCoderManager.Global.GetCoder(format));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba32Float>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal(expected, encoded);
        AssertClose(expectedPixel.Red, decoded.Pixels[0].Red, 0.0001f);
        AssertClose(expectedPixel.Green, decoded.Pixels[0].Green, 0.0001f);
        AssertClose(expectedPixel.Blue, decoded.Pixels[0].Blue, 0.0001f);
        AssertClose(expectedPixel.Alpha, decoded.Pixels[0].Alpha, 0.0001f);
    }

    [Theory]
    [MemberData(nameof(ConsolePackedRevRedLowBitCases))]
    public void EncodeConsolePackedRevFormatsStoreRedInLowBits(TextureFormat format, Rgba32Float sourcePixel, byte[] expected)
    {
        var source = new ArrayBitmap<Rgba32Float>(1, 1, [sourcePixel]);

        var coder = Assert.IsAssignableFrom<IPitchTextureCoder>(TextureCoderManager.Global.GetCoder(format));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(expected, encoded);
    }

    [Theory]
    [MemberData(nameof(ConsolePackedNonRevRedHighBitCases))]
    public void EncodeConsolePackedNonRevFormatsStoreRedInHighBits(TextureFormat format, Rgba32Float sourcePixel, byte[] expected)
    {
        var source = new ArrayBitmap<Rgba32Float>(1, 1, [sourcePixel]);

        var coder = Assert.IsAssignableFrom<IPitchTextureCoder>(TextureCoderManager.Global.GetCoder(format));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(expected, encoded);
    }

    [Fact]
    public void EncodeAndDecodeRgb10A2RevSIntUsesPackedIntegerCoder()
    {
        var source = new ArrayBitmap<Rgba16SNorm>(
            1,
            1,
            [new Rgba16SNorm(-1, 2, -3, -1)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Rgb10A2RevSInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16SNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xff, 0x0b, 0xd0, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeR10X6UNormStoresComponentInTopBits()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R10X6UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xc0, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba12X4UNormStoresComponentsInTopBits()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R12X4G12X4B12X4A12X4UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xf0, 0xff, 0x00, 0x00, 0x00, 0x00, 0xf0, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba12X4UIntStoresComponentsInTopBits()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(4095, 1, 2, 3)]);

        var coder = Assert.IsType<PackedIntegerTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R12X4G12X4B12X4A12X4UInt));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xf0, 0xff, 0x10, 0x00, 0x20, 0x00, 0x30, 0x00], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }

    [Fact]
    public void EncodeAndDecodeRgba14X2UNormStoresComponentsInTopBits()
    {
        var source = new ArrayBitmap<Rgba16UNorm>(
            1,
            1,
            [new Rgba16UNorm(ushort.MaxValue, 0, 0, ushort.MaxValue)]);

        var coder = Assert.IsType<PackedUNormTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.R14X2G14X2B14X2A14X2UNorm));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayBitmap<Rgba16UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([0xfc, 0xff, 0x00, 0x00, 0x00, 0x00, 0xfc, 0xff], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
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

    private sealed class TestTextureCoder(TextureFormat format) : ITextureCoder
    {
        public TextureFormat Format { get; } = format;

        public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            throw new NotSupportedException();
        }

        public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            throw new NotSupportedException();
        }

        public int GetEncodedByteCount(int width, int height) => 0;
    }

    private sealed class TestRegistration(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
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
        TextureFormats.R10X6UNorm,
        TextureFormats.R10X6G10X6UNorm,
        TextureFormats.R10X6G10X6B10X6A10X6UNorm,
        TextureFormats.R12X4UNorm,
        TextureFormats.R12X4G12X4UNorm,
        TextureFormats.R12X4G12X4B12X4A12X4UNorm,
        TextureFormats.R14X2UNorm,
        TextureFormats.R14X2G14X2UNorm,
        TextureFormats.R14X2G14X2B14X2A14X2UNorm,
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

    public static TheoryData<TextureFormat, Type> ConsolePackedFormats() => new()
    {
        { TextureFormats.Rgb655UNorm, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg5SNormB6UNormRev, typeof(PackedSNormTextureCoder) },
        { TextureFormats.Rgba4RevSNorm, typeof(PackedSNormTextureCoder) },
        { TextureFormats.Rg8SNormB8UNormX8Rev, typeof(PackedSNormTextureCoder) },
        { TextureFormats.Rgb10SNormA2UNormRev, typeof(PackedSNormTextureCoder) },
        { TextureFormats.R10Gb11UNorm, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg11B10UNorm, typeof(PackedUNormTextureCoder) },
        { TextureFormats.R10Gb11RevUNorm, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg11B10RevUNorm, typeof(PackedUNormTextureCoder) },
        { TextureFormats.Rg11B10RevSNorm, typeof(PackedSNormTextureCoder) },
        { TextureFormats.R10Gb11RevSNorm, typeof(PackedSNormTextureCoder) }
    };

    public static TheoryData<TextureFormat, Rgba32Float, byte[], Rgba32Float> ConsolePackedRoundTripCases() => new()
    {
        { TextureFormats.Rgb655UNorm, new Rgba32Float(1f, 0f, 1f), [0x1f, 0xfc], new Rgba32Float(1f, 0f, 1f) },
        { TextureFormats.Rg5SNormB6UNormRev, new Rgba32Float(1f, -1f, 1f), [0x2f, 0xfe], new Rgba32Float(1f, -1f, 1f) },
        { TextureFormats.Rgba4RevSNorm, new Rgba32Float(1f, 0f, -1f, 1f), [0x07, 0x79], new Rgba32Float(1f, 0f, -1f, 1f) },
        { TextureFormats.Rg8SNormB8UNormX8Rev, new Rgba32Float(1f, -1f, 1f), [0x7f, 0x81, 0xff, 0x00], new Rgba32Float(1f, -1f, 1f) },
        { TextureFormats.Rgb10SNormA2UNormRev, new Rgba32Float(1f, -1f, 0f, 1f), [0xff, 0x05, 0x08, 0xc0], new Rgba32Float(1f, -1f, 0f, 1f) },
        { TextureFormats.R10Gb11UNorm, new Rgba32Float(1f, 0f, 1f), [0xff, 0x07, 0xc0, 0xff], new Rgba32Float(1f, 0f, 1f) },
        { TextureFormats.Rg11B10UNorm, new Rgba32Float(1f, 0f, 1f), [0xff, 0x03, 0xe0, 0xff], new Rgba32Float(1f, 0f, 1f) },
        { TextureFormats.R10Gb11RevUNorm, new Rgba32Float(1f, 0f, 1f), [0xff, 0x03, 0xe0, 0xff], new Rgba32Float(1f, 0f, 1f) },
        { TextureFormats.Rg11B10RevUNorm, new Rgba32Float(1f, 0f, 1f), [0xff, 0x07, 0xc0, 0xff], new Rgba32Float(1f, 0f, 1f) },
        { TextureFormats.Rg11B10RevSNorm, new Rgba32Float(1f, -1f, 1f), [0xff, 0x0b, 0xe0, 0x7f], new Rgba32Float(1f, -1f, 1f) },
        { TextureFormats.R10Gb11RevSNorm, new Rgba32Float(1f, -1f, 1f), [0xff, 0x05, 0xf0, 0x7f], new Rgba32Float(1f, -1f, 1f) }
    };

    public static TheoryData<TextureFormat, Rgba32Float, byte[]> ConsolePackedRevRedLowBitCases() => new()
    {
        { TextureFormats.Rg5SNormB6UNormRev, new Rgba32Float(1f, 0f, 0f), [0x0f, 0x00] },
        { TextureFormats.Rgba4RevSNorm, new Rgba32Float(1f, 0f, 0f, 0f), [0x07, 0x00] },
        { TextureFormats.Rg8SNormB8UNormX8Rev, new Rgba32Float(1f, 0f, 0f), [0x7f, 0x00, 0x00, 0x00] },
        { TextureFormats.Rgb10SNormA2UNormRev, new Rgba32Float(1f, 0f, 0f, 0f), [0xff, 0x01, 0x00, 0x00] },
        { TextureFormats.R10Gb11RevUNorm, new Rgba32Float(1f, 0f, 0f), [0xff, 0x03, 0x00, 0x00] },
        { TextureFormats.Rg11B10RevUNorm, new Rgba32Float(1f, 0f, 0f), [0xff, 0x07, 0x00, 0x00] },
        { TextureFormats.Rg11B10RevSNorm, new Rgba32Float(1f, 0f, 0f), [0xff, 0x03, 0x00, 0x00] },
        { TextureFormats.R10Gb11RevSNorm, new Rgba32Float(1f, 0f, 0f), [0xff, 0x01, 0x00, 0x00] }
    };

    public static TheoryData<TextureFormat, Rgba32Float, byte[]> ConsolePackedNonRevRedHighBitCases() => new()
    {
        { TextureFormats.R10Gb11UNorm, new Rgba32Float(1f, 0f, 0f), [0x00, 0x00, 0xc0, 0xff] },
        { TextureFormats.Rg11B10UNorm, new Rgba32Float(1f, 0f, 0f), [0x00, 0x00, 0xe0, 0xff] }
    };

    public static TheoryData<TextureFormat> RepresentativePackedYuv422Formats() => new()
    {
        TextureFormats.Uyvy422UNorm,
        TextureFormats.Yuy2UNorm,
        TextureFormats.Vy1Uy0422UNorm,
        TextureFormats.Y1Vy0U422UNorm,
        TextureFormats.Uyvy16_422UNorm,
        TextureFormats.Yuyv10Msb422UNorm
    };

    public static TheoryData<TextureFormat> RepresentativePackedYuva444Formats() => new()
    {
        TextureFormats.Ayuv444UNorm,
        TextureFormats.Vyua10Msb444UNorm,
        TextureFormats.Uyv10A2_444UNorm,
        TextureFormats.Uyva16_444UNorm
    };

    public static TheoryData<TextureFormat> RepresentativePackedIntegerFormats() => new()
    {
        TextureFormats.Rgb10A2UInt,
        TextureFormats.Rgb10A2RevUInt,
        TextureFormats.Bgr10A2RevUInt,
        TextureFormats.Rgb10A2RevSInt,
        TextureFormats.Bgr10A2RevSInt,
        TextureFormats.R10X6UInt,
        TextureFormats.R10X6G10X6UInt,
        TextureFormats.R10X6G10X6B10X6A10X6UInt,
        TextureFormats.R12X4UInt,
        TextureFormats.R12X4G12X4UInt,
        TextureFormats.R12X4G12X4B12X4A12X4UInt,
        TextureFormats.R14X2UInt,
        TextureFormats.R14X2G14X2UInt,
        TextureFormats.R14X2G14X2B14X2A14X2UInt
    };

    public static TheoryData<TextureFormat> XrFormats() => new()
    {
        TextureFormats.Bgr10XR,
        TextureFormats.Bgr10XRSrgb,
        TextureFormats.Rgb10XRA2UNorm,
        TextureFormats.Bgra10XR,
        TextureFormats.Bgra10XRSrgb
    };

    public static TheoryData<TextureFormat> IndexedFormats() => new()
    {
        TextureFormats.Ai44,
        TextureFormats.Ia44,
        TextureFormats.P8,
        TextureFormats.A8P8
    };

    public static TheoryData<TextureFormat> RepresentativePlanarYuvFormats() => new()
    {
        TextureFormats.Yuv3P420UNorm,
        TextureFormats.Yuv2P420UNorm,
        TextureFormats.Yvu10Lsb2P422UNorm,
        TextureFormats.Yuv12Msb2P444UNorm,
        TextureFormats.Yuv16_2P444UNorm,
        TextureFormats.V208UNorm,
        TextureFormats.V408UNorm,
        TextureFormats.Yuv14Msb2P420UNorm,
        TextureFormats.Yuv14Msb2P422UNorm
    };

    public static TheoryData<TextureFormat> DepthStencilFormats() => new()
    {
        TextureFormats.DepthComponent8,
        TextureFormats.DepthComponent16,
        TextureFormats.DepthComponent24,
        TextureFormats.Depth24X8,
        TextureFormats.DepthComponent32,
        TextureFormats.DepthComponent32Float,
        TextureFormats.StencilIndex1,
        TextureFormats.StencilIndex4,
        TextureFormats.StencilIndex8,
        TextureFormats.StencilIndex16,
        TextureFormats.X32Stencil8,
        TextureFormats.X24Stencil8,
        TextureFormats.Depth16Stencil8,
        TextureFormats.Depth24Stencil8,
        TextureFormats.Depth24FloatStencil8,
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
