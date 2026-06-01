using System.Buffers.Binary;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Dds.Tests;

public sealed class DdsCodecTests
{
    [Fact]
    public void EncodeRgba8WritesReadableDx10Dds()
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

        var dds = DdsCodec.Encode(source);
        var texture = DdsCodec.Read(dds);
        var decoded = DdsCodec.Decode(dds);

        AssertDx10Header(dds, DdsDxgiFormat.R8G8B8A8UNorm, width: 2, height: 2, payloadSize: 16);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Texture.Format);
        Assert.Equal(DdsHeaderKind.Dxt10, texture.HeaderKind);
        Assert.Equal(DdsDxgiFormat.R8G8B8A8UNorm, texture.DxgiFormat);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void WriteVolumeTextureWritesReadableDx10Dds()
    {
        var payload = Enumerable.Range(0, 2 * 2 * 2 * 4).Select(value => (byte)value).ToArray();
        var texture = new DdsTexture(TextureFormats.Rgba8UNorm, width: 2, height: 2, depth: 2, payload);

        var dds = DdsCodec.Write(texture);
        var read = DdsCodec.Read(dds);
        var decoded = DdsCodec.DecodeVolume<Rgba8UNorm>(dds);

        Assert.Equal(0x0080100fu, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(8, 4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(24, 4)));
        Assert.Equal(0x00200000u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(112, 4)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(132, 4)));
        Assert.Equal(2, read.Texture.Depth);
        Assert.Equal(payload, read.Texture.Payload);
        Assert.Equal(8, decoded.PixelSpan.Length);
    }

    [Fact]
    public void EncodeWithLegacyHeaderWritesOrdinaryDdsHeader()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8)
            ]);

        var dds = DdsCodec.Encode(source, new DdsEncodingOptions { HeaderKind = DdsHeaderKind.Legacy });
        var texture = DdsCodec.Read(dds);
        var decoded = DdsCodec.Decode(dds);

        AssertLegacyRgba8Header(dds, width: 2, height: 1, payloadSize: 8);
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Texture.Format);
        Assert.Equal(DdsHeaderKind.Legacy, texture.HeaderKind);
        Assert.Equal(DdsLegacyPixelFormat.Rgba8UNorm, texture.LegacyPixelFormat);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithOptionsDxgiFormatWritesSelectedDx10Format()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 255), 16).ToArray());

        var dds = DdsCodec.Encode(source, new DdsEncodingOptions { DxgiFormat = DdsDxgiFormat.BC1UNorm });
        var texture = DdsCodec.Read(dds);

        AssertDx10Header(dds, DdsDxgiFormat.BC1UNorm, width: 4, height: 4, payloadSize: 8);
        Assert.Equal(TextureFormats.Bc1Rgba, texture.Texture.Format);
    }

    [Fact]
    public void EncodeWithOptionsLegacyPixelFormatWritesSelectedOrdinaryFormat()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(64, 128, 255, 255), 16).ToArray());

        var dds = DdsCodec.Encode(source, new DdsEncodingOptions
        {
            HeaderKind = DdsHeaderKind.Legacy,
            LegacyPixelFormat = DdsLegacyPixelFormat.Dxt5
        });
        var texture = DdsCodec.Read(dds);

        Assert.Equal(MakeFourCc("DXT5"), BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(84, 4)));
        Assert.Equal(128 + 16, dds.Length);
        Assert.Equal(TextureFormats.Bc3Rgba, texture.Texture.Format);
        Assert.Equal(DdsLegacyPixelFormat.Dxt5, texture.LegacyPixelFormat);
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

        var dds = DdsCodec.Encode(source, new DdsEncodingOptions
        {
            HeaderKind = DdsHeaderKind.Legacy,
            TextureFormat = TextureFormats.Bgra8
        });
        var texture = DdsCodec.Read(dds);
        var decoded = DdsCodec.Decode(dds);

        Assert.Equal(0x00ff0000u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(92, 4)));
        Assert.Equal(0x0000ff00u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(96, 4)));
        Assert.Equal(0x000000ffu, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(100, 4)));
        Assert.Equal(0xff000000u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(104, 4)));
        Assert.Equal(TextureFormats.Bgra8, texture.Texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void ReadUnsupportedDxgiFormatThrows()
    {
        var dds = CreateDx10Header((DdsDxgiFormat)1, width: 1, height: 1);

        Assert.Throws<NotSupportedException>(() => DdsCodec.Read(dds));
    }

    [Fact]
    public void ReadMipMapChainReadsAllLevels()
    {
        var dds = CreateDx10Header(DdsDxgiFormat.R8G8B8A8UNorm, width: 2, height: 2, mipMapCount: 2);
        Array.Resize(ref dds, 148 + 16 + 4);
        dds[148] = 1;
        dds[148 + 16] = 2;

        var texture = DdsCodec.Read(dds);

        Assert.Equal(2, texture.Texture.MipLevelCount);
        Assert.Equal(2, texture.Texture.GetSubresource(0).Width);
        Assert.Equal(2, texture.Texture.GetSubresource(0).Height);
        Assert.Equal(16, texture.Texture.GetSubresource(0).Payload.Length);
        Assert.Equal(1, texture.Texture.GetSubresource(1).Width);
        Assert.Equal(1, texture.Texture.GetSubresource(1).Height);
        Assert.Equal(4, texture.Texture.GetSubresource(1).Payload.Length);
        Assert.Equal(1, texture.Texture.GetSubresource(0).Payload[0]);
        Assert.Equal(2, texture.Texture.GetSubresource(1).Payload[0]);
    }

    [Fact]
    public void WriteMipMapChainWritesMipHeaderAndPayloads()
    {
        var texture = new DdsTexture(
            TextureFormats.Rgba8UNorm,
            [
                new TextureSubresource(0, 0, 0, 2, 2, Enumerable.Repeat((byte)1, 16).ToArray()),
                new TextureSubresource(1, 0, 0, 1, 1, Enumerable.Repeat((byte)2, 4).ToArray())
            ],
            faceCount: 1);

        var dds = DdsCodec.Write(texture);
        var read = DdsCodec.Read(dds);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(28, 4)));
        Assert.Equal(0x00401008u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(108, 4)));
        Assert.Equal(148 + 16 + 4, dds.Length);
        Assert.Equal(2, read.Texture.MipLevelCount);
        Assert.Equal(texture.Texture.GetSubresource(0).Payload, read.Texture.GetSubresource(0).Payload);
        Assert.Equal(texture.Texture.GetSubresource(1).Payload, read.Texture.GetSubresource(1).Payload);
    }

    [Fact]
    public void EncodeWithGenerateMipmapsWritesReadableCompressedMipChain()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            7,
            5,
            Enumerable.Range(0, 7 * 5)
                .Select(value => new Rgba8UNorm((byte)value, (byte)(value * 2), (byte)(255 - value)))
                .ToArray());

        var dds = DdsCodec.Encode(source, new DdsEncodingOptions
        {
            TextureFormat = TextureFormats.Bc1Rgba,
            GenerateMipmaps = true
        });
        var read = DdsCodec.Read(dds);

        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(28, 4)));
        Assert.Equal(TextureFormats.Bc1Rgba, read.Texture.Format);
        Assert.Equal(3, read.Texture.MipLevelCount);
        Assert.Equal(new[] { 7, 3, 1 }, read.Texture.Subresources.Select(level => level.Width));
        Assert.Equal(new[] { 5, 2, 1 }, read.Texture.Subresources.Select(level => level.Height));
        Assert.Equal(new[] { 32, 8, 8 }, read.Texture.Subresources.Select(level => level.Payload.Length));
    }

    [Fact]
    public void EncodeWithGenerateMipmapsUsesMipmapOptions()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(8, 8);

        var dds = DdsCodec.Encode(source, new DdsEncodingOptions
        {
            TextureFormat = TextureFormats.Rgba8UNorm,
            GenerateMipmaps = true,
            MipmapOptions = new MipmapGenerationOptions { MaxLevelCount = 2 }
        });
        var read = DdsCodec.Read(dds);

        Assert.Equal(2, read.Texture.MipLevelCount);
        Assert.Equal(new[] { 8, 4 }, read.Texture.Subresources.Select(level => level.Width));
    }

    [Fact]
    public void ReadDx10CubeMapReadsFacesAndMipChains()
    {
        var dds = CreateDx10Header(
            DdsDxgiFormat.R8G8B8A8UNorm,
            width: 2,
            height: 2,
            mipMapCount: 2,
            caps2: 0x0000fe00,
            miscFlag: 0x00000004);
        Array.Resize(ref dds, 148 + (6 * (16 + 4)));
        var offset = 148;
        for (var face = 0; face < 6; face++)
        {
            dds[offset] = (byte)(face + 1);
            offset += 16;
            dds[offset] = (byte)(face + 11);
            offset += 4;
        }

        var texture = DdsCodec.Read(dds);

        Assert.True(texture.Texture.IsCubeMap);
        Assert.Equal(6, texture.Texture.FaceCount);
        Assert.Equal(12, texture.Texture.Subresources.Count);
        Assert.Equal(2, texture.Texture.MipLevelCount);
        Assert.Equal(1, texture.Texture.GetSubresource(mipLevel: 0, faceIndex: 0).Payload[0]);
        Assert.Equal(6, texture.Texture.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
        Assert.Equal(16, texture.Texture.GetSubresource(mipLevel: 1, faceIndex: 5).Payload[0]);
        Assert.Equal(2, texture.Texture.MipLevelCount);
        Assert.Equal(1, texture.Texture.Payload[0]);
    }

    [Fact]
    public void WriteCubeMapWritesDx10HeaderAndPayloads()
    {
        var texture = new DdsTexture(TextureFormats.Rgba8UNorm, CreateCubeSubresources(width: 2, height: 2, mipLevelCount: 2), faceCount: 6);

        var dds = DdsCodec.Write(texture);
        var read = DdsCodec.Read(dds);

        Assert.Equal(0x0000fe00u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(112, 4)));
        Assert.Equal(0x00000004u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(136, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(140, 4)));
        Assert.Equal(148 + (6 * (16 + 4)), dds.Length);
        Assert.True(read.Texture.IsCubeMap);
        Assert.Equal(6, read.Texture.FaceCount);
        Assert.Equal(6, read.Texture.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
        Assert.Equal(16, read.Texture.GetSubresource(mipLevel: 1, faceIndex: 5).Payload[0]);
    }

    [Fact]
    public void WriteCubeMapWithLegacyHeaderWritesCaps2Faces()
    {
        var texture = new DdsTexture(TextureFormats.Rgba8UNorm, CreateCubeSubresources(width: 1, height: 1, mipLevelCount: 1), faceCount: 6);

        var dds = DdsCodec.Write(texture, new DdsEncodingOptions { HeaderKind = DdsHeaderKind.Legacy });
        var read = DdsCodec.Read(dds);

        Assert.Equal(0x0000fe00u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(112, 4)));
        Assert.Equal(128 + (6 * 4), dds.Length);
        Assert.Equal(DdsHeaderKind.Legacy, read.HeaderKind);
        Assert.True(read.Texture.IsCubeMap);
        Assert.Equal(6, read.Texture.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
    }

    [Fact]
    public void ReadDx10TextureArrayReadsLayersAndMipChains()
    {
        var dds = CreateDx10Header(
            DdsDxgiFormat.R8G8B8A8UNorm,
            width: 2,
            height: 2,
            mipMapCount: 2,
            arraySize: 2);
        Array.Resize(ref dds, 148 + (2 * (16 + 4)));
        var offset = 148;
        for (var layer = 0; layer < 2; layer++)
        {
            dds[offset] = (byte)(layer + 1);
            offset += 16;
            dds[offset] = (byte)(layer + 11);
            offset += 4;
        }

        var texture = DdsCodec.Read(dds);

        Assert.Equal(2, texture.Texture.ArrayLayerCount);
        Assert.Equal(1, texture.Texture.FaceCount);
        Assert.Equal(4, texture.Texture.Subresources.Count);
        Assert.Equal(1, texture.Texture.GetSubresource(mipLevel: 0, arrayLayer: 0).Payload[0]);
        Assert.Equal(2, texture.Texture.GetSubresource(mipLevel: 0, arrayLayer: 1).Payload[0]);
        Assert.Equal(12, texture.Texture.GetSubresource(mipLevel: 1, arrayLayer: 1).Payload[0]);
    }

    [Fact]
    public void WriteTextureArrayWritesDx10HeaderAndPayloads()
    {
        var texture = new DdsTexture(TextureFormats.Rgba8UNorm, CreateArraySubresources(width: 2, height: 2, mipLevelCount: 2, arrayLayerCount: 2), arrayLayerCount: 2, faceCount: 1);

        var dds = DdsCodec.Write(texture);
        var read = DdsCodec.Read(dds);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(140, 4)));
        Assert.Equal(148 + (2 * (16 + 4)), dds.Length);
        Assert.Equal(2, read.Texture.ArrayLayerCount);
        Assert.Equal(1, read.Texture.GetSubresource(mipLevel: 0, arrayLayer: 0).Payload[0]);
        Assert.Equal(2, read.Texture.GetSubresource(mipLevel: 0, arrayLayer: 1).Payload[0]);
        Assert.Equal(12, read.Texture.GetSubresource(mipLevel: 1, arrayLayer: 1).Payload[0]);
    }

    [Fact]
    public void WriteTextureArrayWithLegacyHeaderThrows()
    {
        var texture = new DdsTexture(TextureFormats.Rgba8UNorm, CreateArraySubresources(width: 1, height: 1, mipLevelCount: 1, arrayLayerCount: 2), arrayLayerCount: 2, faceCount: 1);

        Assert.Throws<NotSupportedException>(() => DdsCodec.Write(texture, new DdsEncodingOptions { HeaderKind = DdsHeaderKind.Legacy }));
    }

    private static void AssertDx10Header(byte[] dds, DdsDxgiFormat expectedDxgiFormat, int width, int height, int payloadSize)
    {
        Assert.Equal(MakeFourCc("DDS "), BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(0, 4)));
        Assert.Equal(124u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(4, 4)));
        Assert.Equal((uint)height, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(12, 4)));
        Assert.Equal((uint)width, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(16, 4)));
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(76, 4)));
        Assert.Equal(0x00000004u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(80, 4)));
        Assert.Equal(MakeFourCc("DX10"), BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(84, 4)));
        Assert.Equal((uint)expectedDxgiFormat, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(128, 4)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(132, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(140, 4)));
        Assert.Equal(128 + 20 + payloadSize, dds.Length);
    }

    private static void AssertLegacyRgba8Header(byte[] dds, int width, int height, int payloadSize)
    {
        Assert.Equal(MakeFourCc("DDS "), BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(0, 4)));
        Assert.Equal(124u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(4, 4)));
        Assert.Equal((uint)height, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(12, 4)));
        Assert.Equal((uint)width, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(16, 4)));
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(76, 4)));
        Assert.Equal(0x00000041u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(80, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(84, 4)));
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(88, 4)));
        Assert.Equal(0x000000ffu, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(92, 4)));
        Assert.Equal(0x0000ff00u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(96, 4)));
        Assert.Equal(0x00ff0000u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(100, 4)));
        Assert.Equal(0xff000000u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(104, 4)));
        Assert.Equal(128 + payloadSize, dds.Length);
    }

    private static byte[] CreateDx10Header(
        DdsDxgiFormat dxgiFormat,
        int width,
        int height,
        uint mipMapCount = 0,
        uint caps2 = 0,
        uint miscFlag = 0,
        uint arraySize = 1)
    {
        var dds = new byte[148];
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(0, 4), MakeFourCc("DDS "));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(4, 4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8, 4), 0x00021007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20, 4), checked((uint)(width * 4)));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(28, 4), mipMapCount);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(76, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80, 4), 0x00000004);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(84, 4), MakeFourCc("DX10"));
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(108, 4), 0x00001000);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(112, 4), caps2);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(128, 4), (uint)dxgiFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(132, 4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(136, 4), miscFlag);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(140, 4), arraySize);
        return dds;
    }

    private static TextureSubresource[] CreateCubeSubresources(int width, int height, int mipLevelCount)
    {
        var subresources = new TextureSubresource[checked(6 * mipLevelCount)];
        var index = 0;
        for (var face = 0; face < 6; face++)
        {
            for (var mipLevel = 0; mipLevel < mipLevelCount; mipLevel++)
            {
                var mipWidth = TextureImage.GetMipDimension(width, mipLevel);
                var mipHeight = TextureImage.GetMipDimension(height, mipLevel);
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
                var mipWidth = TextureImage.GetMipDimension(width, mipLevel);
                var mipHeight = TextureImage.GetMipDimension(height, mipLevel);
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

    private static uint MakeFourCc(string value) =>
        (byte)value[0]
        | ((uint)(byte)value[1] << 8)
        | ((uint)(byte)value[2] << 16)
        | ((uint)(byte)value[3] << 24);
}
