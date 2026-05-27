using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class PalettedTextureCoderTests
{
    [Theory]
    [MemberData(nameof(PalettedFormats))]
    public void GlobalManagerFindsPalettedCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PalettedTextureCoder.IsSupported(format));
        Assert.IsType<PalettedTextureCoder>(coder);
    }

    [Fact]
    public void Palette4Rgba8EncodesPaletteHeaderAndPackedIndices()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0, 255),
                new Rgba8UNorm(0, 255, 0, 128)
            ]);

        var coder = new PalettedTextureCoder(TextureFormats.Palette4Rgba8);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(64, TextureFormats.Palette4Rgba8.HeaderByteCount);
        Assert.Equal(1, rowPitch);
        Assert.Equal([255, 0, 0, 255], encoded[..4]);
        Assert.Equal([0, 255, 0, 128], encoded[4..8]);
        Assert.Equal(0x01, encoded[TextureFormats.Palette4Rgba8.HeaderByteCount]);
    }

    [Fact]
    public void Palette4Rgba8DecodesPackedHighLowNibbles()
    {
        var encoded = new byte[TextureFormats.Palette4Rgba8.GetByteCount(2, 1)];
        encoded[0] = 255;
        encoded[1] = 0;
        encoded[2] = 0;
        encoded[3] = 255;
        encoded[4] = 0;
        encoded[5] = 255;
        encoded[6] = 0;
        encoded[7] = 128;
        encoded[TextureFormats.Palette4Rgba8.HeaderByteCount] = 0x01;

        var coder = new PalettedTextureCoder(TextureFormats.Palette4Rgba8);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 255, 0, 128), decoded.Pixels[1]);
    }

    [Fact]
    public void Palette8Rgb565EncodesPaletteHeaderAndByteIndices()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0, 7),
                new Rgba8UNorm(0, 0, 255, 9)
            ]);

        var coder = new PalettedTextureCoder(TextureFormats.Palette8Rgb565);
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];

        coder.Encode(source.AsView(), encoded, rowPitch);

        Assert.Equal(512, TextureFormats.Palette8Rgb565.HeaderByteCount);
        Assert.Equal(2, rowPitch);
        Assert.Equal([0x00, 0xf8], encoded[..2]);
        Assert.Equal([0x1f, 0x00], encoded[2..4]);
        Assert.Equal([0x00, 0x01], encoded[TextureFormats.Palette8Rgb565.HeaderByteCount..]);
    }

    [Fact]
    public void Palette8Rgb565DecodesPaletteHeaderAndByteIndices()
    {
        var encoded = new byte[TextureFormats.Palette8Rgb565.GetByteCount(2, 1)];
        encoded[0] = 0x00;
        encoded[1] = 0xf8;
        encoded[2] = 0x1f;
        encoded[3] = 0x00;
        encoded[TextureFormats.Palette8Rgb565.HeaderByteCount] = 0;
        encoded[TextureFormats.Palette8Rgb565.HeaderByteCount + 1] = 1;

        var coder = new PalettedTextureCoder(TextureFormats.Palette8Rgb565);
        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(2, 1);

        coder.Decode(encoded, decoded.AsView(), coder.GetDefaultPitch(decoded.Width));

        Assert.Equal(new Rgba8UNorm(255, 0, 0, 255), decoded.Pixels[0]);
        Assert.Equal(new Rgba8UNorm(0, 0, 255, 255), decoded.Pixels[1]);
    }

    [Fact]
    public void PalettedByteCountIncludesHeaderAndRowPitch()
    {
        var coder = new PalettedTextureCoder(TextureFormats.Palette4Rgba8);

        Assert.Equal(2, TextureFormats.Palette4Rgba8.GetRowByteCount(3));
        Assert.Equal(68, TextureFormats.Palette4Rgba8.GetByteCount(3, 2));
        Assert.Equal(68, coder.GetEncodedByteCount(3, 2, 2));
        Assert.Equal(72, coder.GetEncodedByteCount(3, 2, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => coder.GetEncodedByteCount(3, 2, 1));
    }

    public static TheoryData<TextureFormat> PalettedFormats() => new()
    {
        TextureFormats.Palette4Rgb8,
        TextureFormats.Palette4Rgba8,
        TextureFormats.Palette4Rgb565,
        TextureFormats.Palette4Rgba4,
        TextureFormats.Palette4Rgb5A1,
        TextureFormats.Palette8Rgb8,
        TextureFormats.Palette8Rgba8,
        TextureFormats.Palette8Rgb565,
        TextureFormats.Palette8Rgba4,
        TextureFormats.Palette8Rgb5A1
    };
}
