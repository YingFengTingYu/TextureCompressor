using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Pvr;
using TextureCompressor.Formats;

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

        var pvr = PvrCodec.Encode(source, new PvrEncodingOptions { PvrPixelFormat = PvrPixelFormat.Bc1 });
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
            PvrPixelFormat = PvrPixelFormat.Bc1,
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
            PvrPixelFormat = PvrPixelFormat.Bc1
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

    [Fact]
    public void ReadUnsupportedPixelFormatThrows()
    {
        var pvr = CreateHeader(pixelFormat: 51, colourSpace: 0, channelType: 0, width: 4, height: 4);

        Assert.Throws<NotSupportedException>(() => PvrCodec.Read(pvr));
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

    private static byte[] CreateLegacyHeader(
        uint headerSize,
        uint pixelType,
        int width,
        int height,
        uint payloadSize,
        uint mipMapCount = 0,
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
        BinaryPrimitives.WriteUInt32LittleEndian(pvr.AsSpan(24, 4), 32);
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
}
