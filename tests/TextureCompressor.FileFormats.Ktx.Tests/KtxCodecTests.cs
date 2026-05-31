using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Ktx;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Ktx.Tests;

public sealed class KtxCodecTests
{
    [Fact]
    public void EncodeRgba8WritesReadableKtx()
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

        var ktx = KtxCodec.Encode(source);
        var texture = KtxCodec.Read(ktx);
        var decoded = KtxCodec.Decode(ktx);

        AssertHeader(
            ktx,
            KtxGlFormat.UnsignedByte,
            glTypeSize: 1,
            KtxGlFormat.Rgba,
            KtxGlFormat.Rgba8,
            KtxGlFormat.Rgba,
            width: 2,
            height: 2,
            imageSize: 16);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(KtxGlFormat.Rgba8, texture.GlInternalFormat);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithDefaultFormatAndSrgbWritesRgba8Srgb()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(1, 2, 3, 4)]);

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions { IsSrgb = true });
        var texture = KtxCodec.Read(ktx);

        Assert.Equal((uint)KtxGlFormat.Srgb8Alpha8, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(28, 4)));
        Assert.Equal(TextureFormats.Rgba8Srgb, texture.Format);
    }

    [Fact]
    public void EncodeWithOptionsTextureFormatWritesSelectedFormat()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions { TextureFormat = TextureFormats.Bgra8 });
        var texture = KtxCodec.Read(ktx);
        var decoded = KtxCodec.Decode(ktx);

        Assert.Equal((uint)KtxGlFormat.Bgra, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(24, 4)));
        Assert.Equal((uint)KtxGlFormat.Rgba8, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(28, 4)));
        Assert.Equal(TextureFormats.Bgra8, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithOptionsGlInternalFormatWritesSelectedFormat()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(1, 2, 3, 4), 16).ToArray());

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions { GlInternalFormat = KtxGlFormat.CompressedRgbaS3tcDxt1 });
        var texture = KtxCodec.Read(ktx);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(16, 4)));
        Assert.Equal((uint)KtxGlFormat.CompressedRgbaS3tcDxt1, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(28, 4)));
        Assert.Equal(TextureFormats.Bc1Rgba, texture.Format);
    }

    [Fact]
    public void WriteRgb8PadsRowsAndReadRemovesPadding()
    {
        var texture = new KtxTexture(TextureFormats.Rgb8, 1, 2, [1, 2, 3, 4, 5, 6]);

        var ktx = KtxCodec.Write(texture);
        var read = KtxCodec.Read(ktx);

        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(64, 4)));
        Assert.Equal(68 + 8, ktx.Length);
        Assert.Equal(texture.Payload, read.Payload);
    }

    [Fact]
    public void WriteBc1TextureWritesCompressedHeader()
    {
        var texture = new KtxTexture(TextureFormats.Bc1Rgb, 4, 4, [1, 2, 3, 4, 5, 6, 7, 8]);

        var ktx = KtxCodec.Write(texture);
        var read = KtxCodec.Read(ktx);

        AssertHeader(
            ktx,
            glType: 0,
            glTypeSize: 1,
            glFormat: 0,
            KtxGlFormat.CompressedRgbS3tcDxt1,
            KtxGlFormat.Rgb,
            width: 4,
            height: 4,
            imageSize: 8);
        Assert.Equal(TextureFormats.Bc1Rgb, read.Format);
        Assert.Equal(texture.Payload, read.Payload);
    }

    [Fact]
    public void EncodeRgba8Version2WritesReadableKtx2()
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

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions { Version = KtxVersion.Version2 });
        var texture = KtxCodec.Read(ktx);
        var decoded = KtxCodec.Decode(ktx);

        AssertHeaderV2(ktx, KtxVkFormat.R8G8B8A8UNorm, width: 2, height: 2, levelSize: 16);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(KtxVkFormat.R8G8B8A8UNorm, texture.VkFormat);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeVersion2WithOptionsVkFormatWritesSelectedFormat()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(1, 2, 3, 4), 16).ToArray());

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions
        {
            Version = KtxVersion.Version2,
            VkFormat = KtxVkFormat.Bc1RgbaUNormBlock
        });
        var texture = KtxCodec.Read(ktx);

        AssertHeaderV2(ktx, KtxVkFormat.Bc1RgbaUNormBlock, width: 4, height: 4, levelSize: 8);
        Assert.Equal(TextureFormats.Bc1Rgba, texture.Format);
    }

    [Fact]
    public void WriteBc1TextureVersion2WritesCompressedHeader()
    {
        var texture = new KtxTexture(TextureFormats.Bc1Rgb, 4, 4, [1, 2, 3, 4, 5, 6, 7, 8]);

        var ktx = KtxCodec.Write(texture, new KtxEncodingOptions { Version = KtxVersion.Version2 });
        var read = KtxCodec.Read(ktx);

        AssertHeaderV2(ktx, KtxVkFormat.Bc1RgbUNormBlock, width: 4, height: 4, levelSize: 8);
        Assert.Equal(TextureFormats.Bc1Rgb, read.Format);
        Assert.Equal(texture.Payload, read.Payload);
    }

    [Fact]
    public void EncodeVersion2WithZstandardSupercompressionWritesReadableKtx2()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            16,
            16,
            Enumerable.Repeat(new Rgba8UNorm(32, 64, 128, 255), 16 * 16).ToArray());

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions
        {
            Version = KtxVersion.Version2,
            SupercompressionScheme = KtxSupercompressionScheme.Zstandard,
            ZstandardCompressionLevel = 5
        });
        var texture = KtxCodec.Read(ktx);
        var decoded = KtxCodec.Decode(ktx);
        var compressedSize = BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(88, 8));

        AssertHeaderV2(
            ktx,
            KtxVkFormat.R8G8B8A8UNorm,
            width: 16,
            height: 16,
            levelSize: compressedSize,
            uncompressedLevelSize: 16 * 16 * 4,
            KtxSupercompressionScheme.Zstandard);
        Assert.True(compressedSize < 16 * 16 * 4);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeVersion2WithZlibSupercompressionWritesReadableKtx2()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            16,
            16,
            Enumerable.Repeat(new Rgba8UNorm(16, 32, 48, 255), 16 * 16).ToArray());

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions
        {
            Version = KtxVersion.Version2,
            SupercompressionScheme = KtxSupercompressionScheme.Zlib
        });
        var texture = KtxCodec.Read(ktx);
        var decoded = KtxCodec.Decode(ktx);
        var compressedSize = BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(88, 8));

        AssertHeaderV2(
            ktx,
            KtxVkFormat.R8G8B8A8UNorm,
            width: 16,
            height: 16,
            levelSize: compressedSize,
            uncompressedLevelSize: 16 * 16 * 4,
            KtxSupercompressionScheme.Zlib);
        Assert.True(compressedSize < 16 * 16 * 4);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void WriteVersion2MipMapChainWritesReadableKtx2()
    {
        var texture = new KtxTexture(
            TextureFormats.Rgba8UNorm,
            [
                new TextureMipLevel(2, 2, Enumerable.Repeat((byte)1, 16).ToArray()),
                new TextureMipLevel(1, 1, Enumerable.Repeat((byte)2, 4).ToArray())
            ]);

        var ktx = KtxCodec.Write(texture, new KtxEncodingOptions { Version = KtxVersion.Version2 });
        var read = KtxCodec.Read(ktx);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(40, 4)));
        Assert.Equal(128u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(48, 4)));
        Assert.Equal(152ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(80, 8)));
        Assert.Equal(16ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(88, 8)));
        Assert.Equal(168ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(104, 8)));
        Assert.Equal(4ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(112, 8)));
        Assert.Equal(172, ktx.Length);
        Assert.Equal(2, read.MipLevelCount);
        Assert.Equal(texture.MipLevels[0].Payload, read.MipLevels[0].Payload);
        Assert.Equal(texture.MipLevels[1].Payload, read.MipLevels[1].Payload);
    }

    [Fact]
    public void WriteCubeMapWritesReadableKtx()
    {
        var texture = new KtxTexture(TextureFormats.Rgba8UNorm, CreateCubeSubresources(width: 1, height: 1, mipLevelCount: 1), faceCount: 6);

        var ktx = KtxCodec.Write(texture);
        var read = KtxCodec.Read(ktx);

        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(52, 4)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(64, 4)));
        Assert.Equal(68 + (6 * 4), ktx.Length);
        Assert.True(read.IsCubeMap);
        Assert.Equal(6, read.FaceCount);
        Assert.Equal(6, read.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
        Assert.Equal(1, read.Payload[0]);
    }

    [Fact]
    public void WriteCubeMapVersion2WritesReadableKtx2()
    {
        var texture = new KtxTexture(TextureFormats.Rgba8UNorm, CreateCubeSubresources(width: 1, height: 1, mipLevelCount: 1), faceCount: 6);

        var ktx = KtxCodec.Write(texture, new KtxEncodingOptions { Version = KtxVersion.Version2 });
        var read = KtxCodec.Read(ktx);

        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(36, 4)));
        Assert.Equal(24ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(88, 8)));
        Assert.Equal(24ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(96, 8)));
        Assert.Equal(128 + (6 * 4), ktx.Length);
        Assert.True(read.IsCubeMap);
        Assert.Equal(6, read.FaceCount);
        Assert.Equal(6, read.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
        Assert.Equal(1, read.Payload[0]);
    }

    [Fact]
    public void WriteTextureArrayWritesReadableKtx()
    {
        var texture = new KtxTexture(TextureFormats.Rgba8UNorm, CreateArraySubresources(width: 2, height: 2, mipLevelCount: 2, arrayLayerCount: 2), arrayLayerCount: 2, faceCount: 1);

        var ktx = KtxCodec.Write(texture);
        var read = KtxCodec.Read(ktx);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(48, 4)));
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(64, 4)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(100, 4)));
        Assert.Equal(112, ktx.Length);
        Assert.Equal(2, read.ArrayLayerCount);
        Assert.Equal(1, read.GetSubresource(mipLevel: 0, arrayLayer: 0).Payload[0]);
        Assert.Equal(2, read.GetSubresource(mipLevel: 0, arrayLayer: 1).Payload[0]);
        Assert.Equal(12, read.GetSubresource(mipLevel: 1, arrayLayer: 1).Payload[0]);
    }

    [Fact]
    public void WriteTextureArrayVersion2WritesReadableKtx2()
    {
        var texture = new KtxTexture(TextureFormats.Rgba8UNorm, CreateArraySubresources(width: 2, height: 2, mipLevelCount: 2, arrayLayerCount: 2), arrayLayerCount: 2, faceCount: 1);

        var ktx = KtxCodec.Write(texture, new KtxEncodingOptions { Version = KtxVersion.Version2 });
        var read = KtxCodec.Read(ktx);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(32, 4)));
        Assert.Equal(32ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(88, 8)));
        Assert.Equal(8ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(112, 8)));
        Assert.Equal(192, ktx.Length);
        Assert.Equal(2, read.ArrayLayerCount);
        Assert.Equal(1, read.GetSubresource(mipLevel: 0, arrayLayer: 0).Payload[0]);
        Assert.Equal(2, read.GetSubresource(mipLevel: 0, arrayLayer: 1).Payload[0]);
        Assert.Equal(12, read.GetSubresource(mipLevel: 1, arrayLayer: 1).Payload[0]);
    }

    [Fact]
    public void ReadVersion2SupercompressionThrows()
    {
        var ktx = CreateHeaderV2(KtxVkFormat.R8G8B8A8UNorm, width: 1, height: 1, supercompressionScheme: 1);

        Assert.Throws<NotSupportedException>(() => KtxCodec.Read(ktx));
    }

    [Fact]
    public void WriteMipMapChainWritesReadableKtx()
    {
        var texture = new KtxTexture(
            TextureFormats.Rgba8UNorm,
            [
                new TextureMipLevel(2, 2, Enumerable.Repeat((byte)1, 16).ToArray()),
                new TextureMipLevel(1, 1, Enumerable.Repeat((byte)2, 4).ToArray())
            ]);

        var ktx = KtxCodec.Write(texture);
        var read = KtxCodec.Read(ktx);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(56, 4)));
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(64, 4)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(84, 4)));
        Assert.Equal(92, ktx.Length);
        Assert.Equal(2, read.MipLevelCount);
        Assert.Equal(texture.MipLevels[0].Payload, read.MipLevels[0].Payload);
        Assert.Equal(texture.MipLevels[1].Payload, read.MipLevels[1].Payload);
    }

    [Theory]
    [InlineData(KtxVersion.Version1)]
    [InlineData(KtxVersion.Version2)]
    public void EncodeWithGenerateMipmapsWritesReadableCompressedMipChain(KtxVersion version)
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            7,
            5,
            Enumerable.Range(0, 7 * 5)
                .Select(value => new Rgba8UNorm((byte)value, (byte)(value * 2), (byte)(255 - value)))
                .ToArray());

        var ktx = KtxCodec.Encode(source, new KtxEncodingOptions
        {
            Version = version,
            TextureFormat = TextureFormats.Bc1Rgba,
            GenerateMipmaps = true
        });
        var read = KtxCodec.Read(ktx);

        Assert.Equal(TextureFormats.Bc1Rgba, read.Format);
        Assert.Equal(3, read.MipLevelCount);
        Assert.Equal(new[] { 7, 3, 1 }, read.MipLevels.Select(level => level.Width));
        Assert.Equal(new[] { 5, 2, 1 }, read.MipLevels.Select(level => level.Height));
        Assert.Equal(new[] { 32, 8, 8 }, read.MipLevels.Select(level => level.Payload.Length));
    }

    [Fact]
    public void ReadUnsupportedInternalFormatThrows()
    {
        var ktx = CreateHeader((KtxGlFormat)1, width: 1, height: 1);

        Assert.Throws<NotSupportedException>(() => KtxCodec.Read(ktx));
    }

    private static void AssertHeader(
        byte[] ktx,
        KtxGlFormat glType,
        uint glTypeSize,
        KtxGlFormat glFormat,
        KtxGlFormat glInternalFormat,
        KtxGlFormat glBaseInternalFormat,
        int width,
        int height,
        uint imageSize)
    {
        Assert.Equal(new byte[] { 0xab, 0x4b, 0x54, 0x58, 0x20, 0x31, 0x31, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a }, ktx[..12]);
        Assert.Equal(0x04030201u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(12, 4)));
        Assert.Equal((uint)glType, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(16, 4)));
        Assert.Equal(glTypeSize, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(20, 4)));
        Assert.Equal((uint)glFormat, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(24, 4)));
        Assert.Equal((uint)glInternalFormat, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(28, 4)));
        Assert.Equal((uint)glBaseInternalFormat, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(32, 4)));
        Assert.Equal((uint)width, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(36, 4)));
        Assert.Equal((uint)height, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(40, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(44, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(48, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(52, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(56, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(60, 4)));
        Assert.Equal(imageSize, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(64, 4)));
        Assert.Equal(68 + (int)imageSize, ktx.Length);
    }

    private static void AssertHeaderV2(
        byte[] ktx,
        KtxVkFormat vkFormat,
        int width,
        int height,
        ulong levelSize,
        ulong? uncompressedLevelSize = null,
        KtxSupercompressionScheme supercompressionScheme = KtxSupercompressionScheme.None)
    {
        Assert.Equal(new byte[] { 0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a }, ktx[..12]);
        Assert.Equal((uint)vkFormat, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(12, 4)));
        Assert.Equal((uint)width, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(20, 4)));
        Assert.Equal((uint)height, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(24, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(28, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(32, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(36, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(40, 4)));
        Assert.Equal((uint)supercompressionScheme, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(44, 4)));
        Assert.Equal(104u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(48, 4)));
        Assert.Equal(24u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(52, 4)));
        Assert.Equal(128ul, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(80, 8)));
        Assert.Equal(levelSize, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(88, 8)));
        Assert.Equal(uncompressedLevelSize ?? levelSize, BinaryPrimitives.ReadUInt64LittleEndian(ktx.AsSpan(96, 8)));
        Assert.Equal(24u, BinaryPrimitives.ReadUInt32LittleEndian(ktx.AsSpan(104, 4)));
        Assert.Equal(128 + (int)levelSize, ktx.Length);
    }

    private static byte[] CreateHeader(KtxGlFormat glInternalFormat, int width, int height, uint mipMapLevels = 1)
    {
        var ktx = new byte[68];
        new byte[] { 0xab, 0x4b, 0x54, 0x58, 0x20, 0x31, 0x31, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(ktx, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(12, 4), 0x04030201);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(16, 4), (uint)KtxGlFormat.UnsignedByte);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(20, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(24, 4), (uint)KtxGlFormat.Rgba);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(28, 4), (uint)glInternalFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(32, 4), (uint)KtxGlFormat.Rgba);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(36, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(40, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(52, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(56, 4), mipMapLevels);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(64, 4), checked((uint)(width * height * 4)));
        return ktx;
    }

    private static byte[] CreateHeaderV2(
        KtxVkFormat vkFormat,
        int width,
        int height,
        uint levelCount = 1,
        uint supercompressionScheme = 0)
    {
        var ktx = new byte[128 + width * height * 4];
        new byte[] { 0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(ktx, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(12, 4), (uint)vkFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(20, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(24, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(36, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(40, 4), levelCount);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(44, 4), supercompressionScheme);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(48, 4), 104);
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(52, 4), 24);
        BinaryPrimitives.WriteUInt64LittleEndian(ktx.AsSpan(80, 8), 128);
        BinaryPrimitives.WriteUInt64LittleEndian(ktx.AsSpan(88, 8), checked((ulong)(width * height * 4)));
        BinaryPrimitives.WriteUInt64LittleEndian(ktx.AsSpan(96, 8), checked((ulong)(width * height * 4)));
        BinaryPrimitives.WriteUInt32LittleEndian(ktx.AsSpan(104, 4), 24);
        return ktx;
    }

    private static TextureSubresource[] CreateCubeSubresources(int width, int height, int mipLevelCount)
    {
        var subresources = new TextureSubresource[checked(6 * mipLevelCount)];
        var index = 0;
        for (var face = 0; face < 6; face++)
        {
            for (var mipLevel = 0; mipLevel < mipLevelCount; mipLevel++)
            {
                var mipWidth = TextureMipLevel.GetDimension(width, mipLevel);
                var mipHeight = TextureMipLevel.GetDimension(height, mipLevel);
                var byteCount = checked(mipWidth * mipHeight * 4);
                subresources[index++] = new TextureSubresource(
                    mipLevel,
                    arrayLayer: 0,
                    face,
                    mipWidth,
                    mipHeight,
                    Enumerable.Repeat((byte)(face + 1 + (mipLevel * 10)), byteCount).ToArray());
            }
        }

        return subresources;
    }

    private static TextureSubresource[] CreateArraySubresources(int width, int height, int mipLevelCount, int arrayLayerCount)
    {
        var subresources = new TextureSubresource[checked(arrayLayerCount * mipLevelCount)];
        var index = 0;
        for (var layer = 0; layer < arrayLayerCount; layer++)
        {
            for (var mipLevel = 0; mipLevel < mipLevelCount; mipLevel++)
            {
                var mipWidth = TextureMipLevel.GetDimension(width, mipLevel);
                var mipHeight = TextureMipLevel.GetDimension(height, mipLevel);
                var byteCount = checked(mipWidth * mipHeight * 4);
                subresources[index++] = new TextureSubresource(
                    mipLevel,
                    layer,
                    faceIndex: 0,
                    mipWidth,
                    mipHeight,
                    Enumerable.Repeat((byte)(layer + 1 + (mipLevel * 10)), byteCount).ToArray());
            }
        }

        return subresources;
    }
}
