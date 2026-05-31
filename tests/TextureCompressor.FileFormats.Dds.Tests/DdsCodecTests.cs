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
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
        Assert.Equal(DdsHeaderKind.Dxt10, texture.HeaderKind);
        Assert.Equal(DdsDxgiFormat.R8G8B8A8UNorm, texture.DxgiFormat);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
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
        Assert.Equal(TextureFormats.Rgba8UNorm, texture.Format);
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
        Assert.Equal(TextureFormats.Bc1Rgba, texture.Format);
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
        Assert.Equal(TextureFormats.Bc3Rgba, texture.Format);
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
        Assert.Equal(TextureFormats.Bgra8, texture.Format);
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

        Assert.Equal(2, texture.MipLevelCount);
        Assert.Equal(2, texture.MipLevels[0].Width);
        Assert.Equal(2, texture.MipLevels[0].Height);
        Assert.Equal(16, texture.MipLevels[0].Payload.Length);
        Assert.Equal(1, texture.MipLevels[1].Width);
        Assert.Equal(1, texture.MipLevels[1].Height);
        Assert.Equal(4, texture.MipLevels[1].Payload.Length);
        Assert.Equal(1, texture.MipLevels[0].Payload[0]);
        Assert.Equal(2, texture.MipLevels[1].Payload[0]);
    }

    [Fact]
    public void WriteMipMapChainWritesMipHeaderAndPayloads()
    {
        var texture = new DdsTexture(
            TextureFormats.Rgba8UNorm,
            [
                new TextureMipLevel(2, 2, Enumerable.Repeat((byte)1, 16).ToArray()),
                new TextureMipLevel(1, 1, Enumerable.Repeat((byte)2, 4).ToArray())
            ]);

        var dds = DdsCodec.Write(texture);
        var read = DdsCodec.Read(dds);

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(28, 4)));
        Assert.Equal(0x00401008u, BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(108, 4)));
        Assert.Equal(148 + 16 + 4, dds.Length);
        Assert.Equal(2, read.MipLevelCount);
        Assert.Equal(texture.MipLevels[0].Payload, read.MipLevels[0].Payload);
        Assert.Equal(texture.MipLevels[1].Payload, read.MipLevels[1].Payload);
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
        Assert.Equal(TextureFormats.Bc1Rgba, read.Format);
        Assert.Equal(3, read.MipLevelCount);
        Assert.Equal(new[] { 7, 3, 1 }, read.MipLevels.Select(level => level.Width));
        Assert.Equal(new[] { 5, 2, 1 }, read.MipLevels.Select(level => level.Height));
        Assert.Equal(new[] { 32, 8, 8 }, read.MipLevels.Select(level => level.Payload.Length));
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

        Assert.True(texture.IsCubeMap);
        Assert.Equal(6, texture.FaceCount);
        Assert.Equal(12, texture.Subresources.Count);
        Assert.Equal(2, texture.MipLevelCount);
        Assert.Equal(1, texture.GetSubresource(mipLevel: 0, faceIndex: 0).Payload[0]);
        Assert.Equal(6, texture.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
        Assert.Equal(16, texture.GetSubresource(mipLevel: 1, faceIndex: 5).Payload[0]);
        Assert.Equal(2, texture.MipLevels.Count);
        Assert.Equal(1, texture.Payload[0]);
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
        Assert.True(read.IsCubeMap);
        Assert.Equal(6, read.FaceCount);
        Assert.Equal(6, read.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
        Assert.Equal(16, read.GetSubresource(mipLevel: 1, faceIndex: 5).Payload[0]);
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
        Assert.True(read.IsCubeMap);
        Assert.Equal(6, read.GetSubresource(mipLevel: 0, faceIndex: 5).Payload[0]);
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

    private static uint MakeFourCc(string value) =>
        (byte)value[0]
        | ((uint)(byte)value[1] << 8)
        | ((uint)(byte)value[2] << 16)
        | ((uint)(byte)value[3] << 24);
}
