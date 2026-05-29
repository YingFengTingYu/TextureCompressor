using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Astc;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc.Tests;

public sealed class AstcCodecTests
{
    [Fact]
    public void EncodeWritesReadableAstc()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(64, 128, 255, 255), 16).ToArray());

        var astc = AstcCodec.Encode(source, new AstcEncodingOptions { TextureFormat = TextureFormats.RgbaAstc4x4UNorm });
        var texture = AstcCodec.Read(astc);
        var decoded = AstcCodec.Decode(astc);

        AssertHeader(astc, blockWidth: 4, blockHeight: 4, width: 4, height: 4, payloadSize: 16);
        Assert.Equal(TextureFormats.RgbaAstc4x4UNorm, texture.Format);
        Assert.Equal(source.PixelSpan.ToArray(), decoded.PixelSpan.ToArray());
    }

    [Fact]
    public void EncodeWithBlockOptionsWritesSelectedFootprint()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            12,
            12,
            Enumerable.Repeat(new Rgba8UNorm(1, 2, 3, 4), 144).ToArray());

        var astc = AstcCodec.Encode(source, new AstcEncodingOptions
        {
            BlockWidth = 12,
            BlockHeight = 12,
            Profile = AstcProfile.Srgb
        });
        var texture = AstcCodec.Read(astc, new AstcReadOptions { Profile = AstcProfile.Srgb });

        AssertHeader(astc, blockWidth: 12, blockHeight: 12, width: 12, height: 12, payloadSize: 16);
        Assert.Equal(TextureFormats.RgbaAstc12x12Srgb, texture.Format);
    }

    [Fact]
    public void ReadWithProfileOptionSelectsSrgbFormat()
    {
        var astc = CreateAstc(blockWidth: 5, blockHeight: 4, width: 5, height: 4, new byte[16]);

        var texture = AstcCodec.Read(astc, new AstcReadOptions { Profile = AstcProfile.Srgb });

        Assert.Equal(TextureFormats.RgbaAstc5x4Srgb, texture.Format);
    }

    [Fact]
    public void ReadWithTextureFormatRequiresMatchingFootprint()
    {
        var astc = CreateAstc(blockWidth: 5, blockHeight: 5, width: 5, height: 5, new byte[16]);

        Assert.Throws<InvalidDataException>(() => AstcCodec.Read(astc, new AstcReadOptions
        {
            TextureFormat = TextureFormats.RgbaAstc4x4UNorm
        }));
    }

    [Fact]
    public void WriteRejectsNonAstcFormat()
    {
        var texture = new AstcTexture(TextureFormats.Rgba8UNorm, 1, 1, new byte[4]);

        Assert.Throws<ArgumentException>(() => AstcCodec.Write(texture));
    }

    [Fact]
    public void WriteRejectsWrongPayloadLength()
    {
        var texture = new AstcTexture(TextureFormats.RgbaAstc4x4UNorm, 4, 4, []);

        Assert.Throws<ArgumentException>(() => AstcCodec.Write(texture));
    }

    [Fact]
    public void ReadUnsupportedFootprintThrows()
    {
        var astc = CreateAstc(blockWidth: 3, blockHeight: 3, width: 3, height: 3, new byte[16]);

        Assert.Throws<NotSupportedException>(() => AstcCodec.Read(astc));
    }

    [Fact]
    public void Read3DTextureThrows()
    {
        var astc = CreateAstc(blockWidth: 4, blockHeight: 4, width: 4, height: 4, payload: new byte[16], blockDepth: 4, depth: 4);

        Assert.Throws<NotSupportedException>(() => AstcCodec.Read(astc));
    }

    private static void AssertHeader(byte[] astc, int blockWidth, int blockHeight, int width, int height, int payloadSize)
    {
        Assert.Equal([0x13, 0xAB, 0xA1, 0x5C], astc.AsSpan(0, 4).ToArray());
        Assert.Equal((byte)blockWidth, astc[4]);
        Assert.Equal((byte)blockHeight, astc[5]);
        Assert.Equal(1, astc[6]);
        Assert.Equal(width, ReadUInt24(astc.AsSpan(7, 3)));
        Assert.Equal(height, ReadUInt24(astc.AsSpan(10, 3)));
        Assert.Equal(1, ReadUInt24(astc.AsSpan(13, 3)));
        Assert.Equal(16 + payloadSize, astc.Length);
    }

    private static byte[] CreateAstc(
        int blockWidth,
        int blockHeight,
        int width,
        int height,
        byte[] payload,
        int blockDepth = 1,
        int depth = 1)
    {
        var astc = new byte[16 + payload.Length];
        astc[0] = 0x13;
        astc[1] = 0xAB;
        astc[2] = 0xA1;
        astc[3] = 0x5C;
        astc[4] = checked((byte)blockWidth);
        astc[5] = checked((byte)blockHeight);
        astc[6] = checked((byte)blockDepth);
        WriteUInt24(astc.AsSpan(7, 3), width);
        WriteUInt24(astc.AsSpan(10, 3), height);
        WriteUInt24(astc.AsSpan(13, 3), depth);
        payload.CopyTo(astc.AsSpan(16));
        return astc;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> source) =>
        source[0] | (source[1] << 8) | (source[2] << 16);

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
    }
}
