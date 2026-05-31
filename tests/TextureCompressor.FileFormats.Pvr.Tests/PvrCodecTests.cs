using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Pvr;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.FileFormats.Pvr.Tests;

public sealed class PvrCodecTests
{
    [Fact]
    public void EncodeRgba8WritesReadablePvr()
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

        var pvr = PvrCodec.Encode(source);
        var texture = PvrCodec.Read(pvr);
        var decoded = PvrCodec.Decode(pvr);

        AssertHeader(pvr, expectedPixelFormat: 0x0808080861626772, colourSpace: 0, channelType: 0, width: 2, height: 2);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithDefaultOptionsWritesVersion3()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(1, 2, 3, 4)]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions());

        Assert.Equal(0x03525650u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(0, 4)));
    }

    [Fact]
    public void EncodeWithDefaultFormatAndSrgbWritesRgba8Srgb()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(1, 2, 3, 4)]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions { IsSrgb = true });
        var texture = PvrCodec.Read(pvr);

        AssertHeader(pvr, expectedPixelFormat: 0x0808080861626772, colourSpace: 1, channelType: 0, width: 1, height: 1);
        Assert.Equal(TextureFormats.Rgba8Srgb, texture.Format);
    }

    [Fact]
    public void EncodeWithOptionsFormatWritesSelectedTextureFormat()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions { TextureFormat = TextureFormats.Bgra8 });
        var texture = PvrCodec.Read(pvr);
        var decoded = PvrCodec.Decode(pvr);

        AssertHeader(pvr, expectedPixelFormat: 0x0808080861726762, colourSpace: 0, channelType: 0, width: 2, height: 1);
        Assert.Equal(TextureFormats.Bgra8, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithOptionsPvrPixelFormatWritesSelectedVersion3Format()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(1, 2, 3, 4), 16).ToArray());

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions { PvrPixelFormat = PvrPixelFormat.Dxt1 });
        var texture = PvrCodec.Read(pvr);

        AssertHeader(pvr, expectedPixelFormat: 7, colourSpace: 0, channelType: 0, width: 4, height: 4);
        Assert.Equal(TextureFormats.Bc1Rgba, texture.Format);
    }

    [Fact]
    public void EncodeWithOptionsPvrPixelFormatAndSrgbWritesSrgbVersion3Format()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(1, 2, 3, 4), 16).ToArray());

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions
        {
            PvrPixelFormat = PvrPixelFormat.Dxt1,
            IsSrgb = true
        });
        var texture = PvrCodec.Read(pvr);

        AssertHeader(pvr, expectedPixelFormat: 7, colourSpace: 1, channelType: 0, width: 4, height: 4);
        Assert.Equal(TextureFormats.Bc1RgbaSrgb, texture.Format);
    }

    [Fact]
    public void EncodeWithOptionsTextureFormatOverridesPvrPixelFormat()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions
        {
            TextureFormat = TextureFormats.Bgra8,
            PvrPixelFormat = PvrPixelFormat.Dxt1
        });

        AssertHeader(pvr, expectedPixelFormat: 0x0808080861726762, colourSpace: 0, channelType: 0, width: 2, height: 1);
    }

    [Fact]
    public void EncodeRgba8Version2WritesReadableLegacyPvr()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions { Version = 2 });
        var texture = PvrCodec.Read(pvr);
        var decoded = PvrCodec.Decode(pvr);

        AssertLegacyHeader(pvr, headerSize: 52, pixelType: 0x12, hasAlpha: true, bitCount: 32, width: 2, height: 1, payloadSize: 8);
        Assert.Equal(0x21525650u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(44, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(48, 4)));
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithOptionsLegacyPixelTypeWritesSelectedLegacyType()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions
        {
            Version = 2,
            PvrLegacyPixelType = PvrLegacyPixelType.DxgiR8G8B8A8UNorm
        });
        var texture = PvrCodec.Read(pvr);

        AssertLegacyHeader(pvr, headerSize: 52, pixelType: 0x61, hasAlpha: true, bitCount: 32, width: 2, height: 1, payloadSize: 8);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
    }

    [Fact]
    public void EncodeWithOptionsTextureFormatOverridesLegacyPixelType()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions
        {
            Version = 2,
            TextureFormat = TextureFormats.Bgra8,
            PvrLegacyPixelType = PvrLegacyPixelType.GlRgba8888
        });

        AssertLegacyHeader(pvr, headerSize: 52, pixelType: 0x1a, hasAlpha: true, bitCount: 32, width: 2, height: 1, payloadSize: 8);
    }

    [Theory]
    [InlineData(PvrLegacyPixelTypePreference.Default, PvrLegacyPixelType.GlPvrtc2)]
    [InlineData(PvrLegacyPixelTypePreference.Gl, PvrLegacyPixelType.GlPvrtc2)]
    [InlineData(PvrLegacyPixelTypePreference.Mgl, PvrLegacyPixelType.MglPvrtc2)]
    public void WriteLegacyPvrtcUsesRequestedPixelTypePreference(
        PvrLegacyPixelTypePreference preference,
        PvrLegacyPixelType expectedPixelType)
    {
        var payload = new byte[TextureFormats.RgbPvrtcI2BppUNorm.GetByteCount(8, 4)];
        var texture = new PvrTexture(
            TextureFormats.RgbPvrtcI2BppUNorm,
            8,
            4,
            payload);

        var pvr = PvrCodec.Write(texture, new PvrEncodingOptions
        {
            Version = 2,
            LegacyPixelTypePreference = preference
        });

        AssertLegacyHeader(pvr, headerSize: 52, pixelType: (uint)expectedPixelType, hasAlpha: false, bitCount: 2, width: 8, height: 4, payloadSize: (uint)payload.Length);
    }

    [Theory]
    [InlineData(PvrLegacyPixelTypePreference.Default, PvrLegacyPixelType.GlRgba8888)]
    [InlineData(PvrLegacyPixelTypePreference.Gl, PvrLegacyPixelType.GlRgba8888)]
    [InlineData(PvrLegacyPixelTypePreference.Mgl, PvrLegacyPixelType.MglArgb8888)]
    [InlineData(PvrLegacyPixelTypePreference.Dxgi, PvrLegacyPixelType.DxgiR8G8B8A8UNorm)]
    public void WriteLegacyRgba8UsesRequestedPixelTypePreference(
        PvrLegacyPixelTypePreference preference,
        PvrLegacyPixelType expectedPixelType)
    {
        var texture = new PvrTexture(TextureFormats.Rgba8UNorm, 1, 1, [1, 2, 3, 4]);

        var pvr = PvrCodec.Write(texture, new PvrEncodingOptions
        {
            Version = 2,
            LegacyPixelTypePreference = preference
        });

        AssertLegacyHeader(pvr, headerSize: 52, pixelType: (uint)expectedPixelType, hasAlpha: true, bitCount: 32, width: 1, height: 1, payloadSize: 4);
    }

    [Fact]
    public void ExplicitLegacyPixelTypeOverridesPreference()
    {
        var payload = new byte[TextureFormats.RgbPvrtcI2BppUNorm.GetByteCount(8, 4)];
        var texture = new PvrTexture(
            TextureFormats.RgbPvrtcI2BppUNorm,
            8,
            4,
            payload);

        var pvr = PvrCodec.Write(texture, new PvrEncodingOptions
        {
            Version = 2,
            LegacyPixelTypePreference = PvrLegacyPixelTypePreference.Mgl,
            PvrLegacyPixelType = PvrLegacyPixelType.GlPvrtc2
        });

        AssertLegacyHeader(pvr, headerSize: 52, pixelType: (uint)PvrLegacyPixelType.GlPvrtc2, hasAlpha: false, bitCount: 2, width: 8, height: 4, payloadSize: (uint)payload.Length);
    }

    [Fact]
    public void WriteBc1Version1WritesReadableLegacyPvr()
    {
        var texture = new PvrTexture(
            TextureFormats.Bc1Rgb,
            4,
            4,
            [1, 2, 3, 4, 5, 6, 7, 8]);

        var pvr = PvrCodec.Write(texture, new PvrEncodingOptions { Version = 1 });
        var read = PvrCodec.Read(pvr);

        AssertLegacyHeader(pvr, headerSize: 44, pixelType: 0x20, hasAlpha: false, bitCount: 4, width: 4, height: 4, payloadSize: 8);
        Assert.Equal(TextureFormats.Bc1Rgb, read.Format);
        Assert.Equal(texture.Payload, read.Payload);
    }

    [Fact]
    public void WriteBc1TextureWritesExpectedHeader()
    {
        var texture = new PvrTexture(
            TextureFormats.Bc1Rgba,
            4,
            4,
            new byte[8]);

        var pvr = PvrCodec.Write(texture);
        var read = PvrCodec.Read(pvr);

        AssertHeader(pvr, expectedPixelFormat: 7, colourSpace: 0, channelType: 0, width: 4, height: 4);
        Assert.Equal(TextureFormats.Bc1Rgba, read.Format);
        Assert.Equal(texture.Payload, read.Payload);
    }

    [Theory]
    [MemberData(nameof(SupportedVersion3PlanarYuvFormats))]
    public void WriteVersion3PlanarYuvTextureWritesExpectedHeader(
        TextureFormat format,
        PvrPixelFormat expectedPixelFormat,
        uint expectedChannelType)
    {
        const int width = 4;
        const int height = 4;
        var payload = new byte[GetEncodedByteCount(format, width, height)];
        var texture = new PvrTexture(format, width, height, payload);

        var pvr = PvrCodec.Write(texture);
        var read = PvrCodec.Read(pvr);

        AssertHeader(pvr, expectedPixelFormat: (uint)expectedPixelFormat, colourSpace: 0, channelType: expectedChannelType, width, height);
        Assert.Equal(format, read.Format);
        Assert.Equal(payload, read.Payload);
    }

    [Theory]
    [MemberData(nameof(SupportedVersion3PlanarYuvFormats))]
    public void ReadVersion3PlanarYuvHeaderUsesExpectedFormat(
        TextureFormat expectedFormat,
        PvrPixelFormat pixelFormat,
        uint channelType)
    {
        const int width = 4;
        const int height = 4;
        var pvr = CreateHeaderWithPayload(
            pixelFormat: (uint)pixelFormat,
            colourSpace: 0,
            channelType: channelType,
            width: width,
            height: height,
            payloadSize: GetEncodedByteCount(expectedFormat, width, height));

        var texture = PvrCodec.Read(pvr);

        Assert.Equal(expectedFormat, texture.Format);
    }

    [Fact]
    public void ReadUnsupportedPixelFormatThrows()
    {
        var pvr = CreateHeader(pixelFormat: 51, colourSpace: 0, channelType: 0, width: 4, height: 4);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Read(pvr));
    }

    [Fact]
    public void ReadKnownImgicPixelFormatThrows()
    {
        var pvr = CreateHeader(pixelFormat: (uint)PvrPixelFormat.ImgicR8G8B8A8_8X8, colourSpace: 0, channelType: 0, width: 4, height: 4);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Read(pvr));
    }

    [Fact]
    public void EncodeKnownImgicPixelFormatThrows()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(1, 2, 3, 4)]);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Encode(source, new PvrEncodingOptions
        {
            PvrPixelFormat = PvrPixelFormat.ImgicR8G8B8A8_8X8
        }));
    }

    [Fact]
    public void ReadMipMapChainThrows()
    {
        var pvr = CreateHeader(pixelFormat: 0x0808080861626772, colourSpace: 0, channelType: 0, width: 2, height: 2, mipMapCount: 2);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Read(pvr));
    }

    [Fact]
    public void ReadLegacyMipMapChainThrows()
    {
        var pvr = CreateLegacyHeader(headerSize: 52, pixelType: 0x12, width: 2, height: 2, payloadSize: 16, mipMapCount: 1);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Read(pvr));
    }

    [Fact]
    public void ReadLegacyAliasWithSameMasksUsesRgba8()
    {
        var pvr = CreateLegacyHeader(headerSize: 52, pixelType: 0x61, width: 1, height: 1, payloadSize: 4);

        var texture = PvrCodec.Read(pvr);

        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
    }

    [Theory]
    [MemberData(nameof(SupportedLegacyReadAliases))]
    public void ReadSupportedLegacyAliasUsesExpectedFormat(
        PvrLegacyPixelType pixelType,
        TextureFormat expectedFormat,
        uint bitCount,
        int width)
    {
        var payloadSize = checked((uint)expectedFormat.GetByteCount(width, 1));
        var flags = (uint)pixelType | (expectedFormat.AlphaBits > 0 ? 1u << 15 : 0);
        var pvr = CreateLegacyHeader(
            headerSize: 52,
            pixelType: flags,
            width: width,
            height: 1,
            payloadSize: payloadSize,
            bitCount: bitCount,
            redMask: 0,
            greenMask: 0,
            blueMask: 0,
            alphaMask: 0);

        var texture = PvrCodec.Read(pvr);

        Assert.Equal(expectedFormat, texture.Format);
    }

    [Fact]
    public void ReadLegacy8888UsesMasksBeforePixelType()
    {
        var pvr = CreateLegacyHeader(
            headerSize: 52,
            pixelType: 0x12,
            width: 1,
            height: 1,
            payloadSize: 4,
            redMask: 0x00ff0000,
            greenMask: 0x0000ff00,
            blueMask: 0x000000ff,
            alphaMask: 0xff000000);

        var texture = PvrCodec.Read(pvr);

        Assert.Equal(TextureFormats.Bgra8, texture.Format);
    }

    [Fact]
    public void ReadLegacyUnrecognizedMasksThrow()
    {
        var pvr = CreateLegacyHeader(
            headerSize: 52,
            pixelType: 0x12,
            width: 1,
            height: 1,
            payloadSize: 4,
            redMask: 0x00000001,
            greenMask: 0x00000002,
            blueMask: 0x00000004,
            alphaMask: 0x00000008);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Read(pvr));
    }

    [Fact]
    public void WriteInvalidVersionThrows()
    {
        var texture = new PvrTexture(TextureFormats.Rgba8UNorm, 1, 1, [1, 2, 3, 4]);

        Assert.Throws<ArgumentOutOfRangeException>(() => PvrCodec.Write(texture, new PvrEncodingOptions { Version = 4 }));
    }

    public static TheoryData<PvrLegacyPixelType, TextureFormat, uint, int> SupportedLegacyReadAliases() => new()
    {
        { PvrLegacyPixelType.MglRgb555, TextureFormats.Rgb5UNorm, 16, 1 },
        { PvrLegacyPixelType.MglArgb8332, TextureFormats.A8Rgb332UNorm, 16, 1 },
        { PvrLegacyPixelType.MglOneBpp, TextureFormats.Bw1BppUNorm, 1, 8 },
        { PvrLegacyPixelType.MglVy1Uy0, TextureFormats.Vy1Uy0422UNorm, 16, 2 },
        { PvrLegacyPixelType.MglY1Vy0U, TextureFormats.Y1Vy0U422UNorm, 16, 2 },
        { PvrLegacyPixelType.GlRgb555, TextureFormats.Rgb5UNorm, 16, 1 },
        { PvrLegacyPixelType.D3dV8U8, TextureFormats.Rg8SNorm, 16, 1 },
        { PvrLegacyPixelType.D3dL16, TextureFormats.Luminance16UNorm, 16, 1 },
        { PvrLegacyPixelType.DxgiR32G32B32A32UInt, TextureFormats.Rgba32UInt, 128, 1 },
        { PvrLegacyPixelType.DxgiR32G32B32A32SInt, TextureFormats.Rgba32SInt, 128, 1 },
        { PvrLegacyPixelType.DxgiR32G32B32UInt, TextureFormats.Rgb32UInt, 96, 1 },
        { PvrLegacyPixelType.DxgiR32G32B32SInt, TextureFormats.Rgb32SInt, 96, 1 },
        { PvrLegacyPixelType.DxgiR16G16B16A16UInt, TextureFormats.Rgba16UInt, 64, 1 },
        { PvrLegacyPixelType.DxgiR16G16B16A16SInt, TextureFormats.Rgba16SInt, 64, 1 },
        { PvrLegacyPixelType.DxgiR32G32UInt, TextureFormats.Rg32UInt, 64, 1 },
        { PvrLegacyPixelType.DxgiR32G32SInt, TextureFormats.Rg32SInt, 64, 1 },
        { PvrLegacyPixelType.DxgiR10G10B10A2UInt, TextureFormats.Rgb10A2UInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR8G8B8A8UInt, TextureFormats.Rgba8UInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR8G8B8A8SInt, TextureFormats.Rgba8SInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR16G16UInt, TextureFormats.Rg16UInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR16G16SInt, TextureFormats.Rg16SInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR32UInt, TextureFormats.R32UInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR32SInt, TextureFormats.R32SInt, 32, 1 },
        { PvrLegacyPixelType.DxgiR8G8UInt, TextureFormats.Rg8UInt, 16, 1 },
        { PvrLegacyPixelType.DxgiR8G8SInt, TextureFormats.Rg8SInt, 16, 1 },
        { PvrLegacyPixelType.DxgiR16UInt, TextureFormats.R16UInt, 16, 1 },
        { PvrLegacyPixelType.DxgiR16SInt, TextureFormats.R16SInt, 16, 1 },
        { PvrLegacyPixelType.DxgiR8UInt, TextureFormats.R8UInt, 8, 1 },
        { PvrLegacyPixelType.DxgiR8SInt, TextureFormats.R8SInt, 8, 1 },
        { PvrLegacyPixelType.DxgiA8UNorm, TextureFormats.Alpha8UNorm, 8, 1 }
    };

    public static TheoryData<TextureFormat, PvrPixelFormat, uint> SupportedVersion3PlanarYuvFormats() => new()
    {
        { TextureFormats.Yuv3P444UNorm, PvrPixelFormat.Yuv3P444, 0u },
        { TextureFormats.Yuv10Msb3P444UNorm, PvrPixelFormat.Yuv10Msb3P444, 4u },
        { TextureFormats.Yuv10Lsb3P444UNorm, PvrPixelFormat.Yuv10Lsb3P444, 4u },
        { TextureFormats.Yuv12Msb3P444UNorm, PvrPixelFormat.Yuv12Msb3P444, 4u },
        { TextureFormats.Yuv12Lsb3P444UNorm, PvrPixelFormat.Yuv12Lsb3P444, 4u },
        { TextureFormats.Yuv16_3P444UNorm, PvrPixelFormat.Yuv16_3P444, 4u },
        { TextureFormats.Yuv3P422UNorm, PvrPixelFormat.Yuv3P422, 0u },
        { TextureFormats.Yuv10Msb3P422UNorm, PvrPixelFormat.Yuv10Msb3P422, 4u },
        { TextureFormats.Yuv10Lsb3P422UNorm, PvrPixelFormat.Yuv10Lsb3P422, 4u },
        { TextureFormats.Yuv12Msb3P422UNorm, PvrPixelFormat.Yuv12Msb3P422, 4u },
        { TextureFormats.Yuv12Lsb3P422UNorm, PvrPixelFormat.Yuv12Lsb3P422, 4u },
        { TextureFormats.Yuv16_3P422UNorm, PvrPixelFormat.Yuv16_3P422, 4u },
        { TextureFormats.Yuv3P420UNorm, PvrPixelFormat.Yuv3P420, 0u },
        { TextureFormats.Yuv10Msb3P420UNorm, PvrPixelFormat.Yuv10Msb3P420, 4u },
        { TextureFormats.Yuv10Lsb3P420UNorm, PvrPixelFormat.Yuv10Lsb3P420, 4u },
        { TextureFormats.Yuv12Msb3P420UNorm, PvrPixelFormat.Yuv12Msb3P420, 4u },
        { TextureFormats.Yuv12Lsb3P420UNorm, PvrPixelFormat.Yuv12Lsb3P420, 4u },
        { TextureFormats.Yuv16_3P420UNorm, PvrPixelFormat.Yuv16_3P420, 4u },
        { TextureFormats.Yvu3P420UNorm, PvrPixelFormat.Yvu3P420, 0u },
        { TextureFormats.Yuv2P422UNorm, PvrPixelFormat.Yuv2P422, 0u },
        { TextureFormats.Yuv10Msb2P422UNorm, PvrPixelFormat.Yuv10Msb2P422, 4u },
        { TextureFormats.Yuv10Lsb2P422UNorm, PvrPixelFormat.Yuv10Lsb2P422, 4u },
        { TextureFormats.Yuv12Msb2P422UNorm, PvrPixelFormat.Yuv12Msb2P422, 4u },
        { TextureFormats.Yuv12Lsb2P422UNorm, PvrPixelFormat.Yuv12Lsb2P422, 4u },
        { TextureFormats.Yuv16_2P422UNorm, PvrPixelFormat.Yuv16_2P422, 4u },
        { TextureFormats.Yuv2P420UNorm, PvrPixelFormat.Yuv2P420, 0u },
        { TextureFormats.Yuv10Msb2P420UNorm, PvrPixelFormat.Yuv10Msb2P420, 4u },
        { TextureFormats.Yuv10Lsb2P420UNorm, PvrPixelFormat.Yuv10Lsb2P420, 4u },
        { TextureFormats.Yuv12Msb2P420UNorm, PvrPixelFormat.Yuv12Msb2P420, 4u },
        { TextureFormats.Yuv12Lsb2P420UNorm, PvrPixelFormat.Yuv12Lsb2P420, 4u },
        { TextureFormats.Yuv16_2P420UNorm, PvrPixelFormat.Yuv16_2P420, 4u },
        { TextureFormats.Yuv2P444UNorm, PvrPixelFormat.Yuv2P444, 0u },
        { TextureFormats.Yvu2P444UNorm, PvrPixelFormat.Yvu2P444, 0u },
        { TextureFormats.Yuv10Msb2P444UNorm, PvrPixelFormat.Yuv10Msb2P444, 4u },
        { TextureFormats.Yuv10Lsb2P444UNorm, PvrPixelFormat.Yuv10Lsb2P444, 4u },
        { TextureFormats.Yvu10Msb2P444UNorm, PvrPixelFormat.Yvu10Msb2P444, 4u },
        { TextureFormats.Yvu10Lsb2P444UNorm, PvrPixelFormat.Yvu10Lsb2P444, 4u },
        { TextureFormats.Yvu2P422UNorm, PvrPixelFormat.Yvu2P422, 0u },
        { TextureFormats.Yvu10Msb2P422UNorm, PvrPixelFormat.Yvu10Msb2P422, 4u },
        { TextureFormats.Yvu10Lsb2P422UNorm, PvrPixelFormat.Yvu10Lsb2P422, 4u },
        { TextureFormats.Yvu2P420UNorm, PvrPixelFormat.Yvu2P420, 0u },
        { TextureFormats.Yvu10Msb2P420UNorm, PvrPixelFormat.Yvu10Msb2P420, 4u },
        { TextureFormats.Yvu10Lsb2P420UNorm, PvrPixelFormat.Yvu10Lsb2P420, 4u }
    };

    private static void AssertHeader(byte[] pvr, ulong expectedPixelFormat, uint colourSpace, uint channelType, int width, int height)
    {
        Assert.Equal(0x03525650u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(0, 4)));
        Assert.Equal(expectedPixelFormat, BinaryPrimitives.ReadUInt64LittleEndian(pvr.AsSpan(8, 8)));
        Assert.Equal(colourSpace, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(16, 4)));
        Assert.Equal(channelType, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(20, 4)));
        Assert.Equal((uint)height, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(24, 4)));
        Assert.Equal((uint)width, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(28, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(32, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(36, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(40, 4)));
    }

    private static void AssertLegacyHeader(
        byte[] pvr,
        uint headerSize,
        uint pixelType,
        bool hasAlpha,
        uint bitCount,
        int width,
        int height,
        uint payloadSize)
    {
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(16, 4));
        Assert.Equal(headerSize, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(0, 4)));
        Assert.Equal((uint)height, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(4, 4)));
        Assert.Equal((uint)width, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(8, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(12, 4)));
        Assert.Equal(pixelType, flags & 0xffu);
        Assert.Equal(hasAlpha, (flags & (1u << 15)) != 0);
        Assert.Equal(payloadSize, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(20, 4)));
        Assert.Equal(bitCount, BinaryPrimitives.ReadUInt32LittleEndian(pvr.AsSpan(24, 4)));
    }

    private static byte[] CreateHeader(
        ulong pixelFormat,
        uint colourSpace,
        uint channelType,
        int width,
        int height,
        uint mipMapCount = 1)
    {
        var pvr = new byte[52];
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(0, 4), 0x03525650);
        BinaryPrimitives.WriteUInt64LittleEndian(pvr.AsSpan(8, 8), pixelFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(16, 4), colourSpace);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(20, 4), channelType);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(24, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(28, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(32, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(36, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(40, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(44, 4), mipMapCount);
        return pvr;
    }

    private static byte[] CreateHeaderWithPayload(
        ulong pixelFormat,
        uint colourSpace,
        uint channelType,
        int width,
        int height,
        int payloadSize)
    {
        var header = CreateHeader(pixelFormat, colourSpace, channelType, width, height);
        var pvr = new byte[checked(header.Length + payloadSize)];
        header.CopyTo(pvr, 0);
        return pvr;
    }

    private static byte[] CreateLegacyHeader(
        uint headerSize,
        uint pixelType,
        int width,
        int height,
        uint payloadSize,
        uint mipMapCount = 0,
        uint bitCount = 32,
        uint redMask = 0x000000ff,
        uint greenMask = 0x0000ff00,
        uint blueMask = 0x00ff0000,
        uint alphaMask = 0xff000000)
    {
        var pvr = new byte[checked((int)(headerSize + payloadSize))];
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(0, 4), headerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(4, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(8, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(12, 4), mipMapCount);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(16, 4), pixelType);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(20, 4), payloadSize);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(24, 4), bitCount);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(28, 4), redMask);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(32, 4), greenMask);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(36, 4), blueMask);
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(40, 4), alphaMask);
        if (headerSize == 52)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(44, 4), 0x21525650);
            BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(48, 4), 1);
        }

        return pvr;
    }

    private static int GetEncodedByteCount(TextureFormat format, int width, int height) =>
        TextureCoderManager.Global.GetCoder(format).GetEncodedByteCount(width, height);
}
