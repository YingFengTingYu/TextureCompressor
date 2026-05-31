using TextureCompressor.Codecs;
using TextureCompressor.Formats;
using TextureCompressor.Options;

namespace TextureCompressor.Tests;

public sealed class TextureCompressionOptionsTests
{
    [Fact]
    public void DefaultsUseNormalCompressionMode()
    {
        Assert.Equal(TextureCompressionLevel.Normal, new TextureCompressionOptions().CompressionMode);
    }

    [Fact]
    public void BuiltInCompressedCodersAcceptSharedCompressionOptions()
    {
        var options = new TextureCompressionOptions { CompressionMode = TextureCompressionLevel.High };

        Assert.Equal(TextureFormats.Bc1Rgb, new S3tcTextureCoder(TextureFormats.Bc1Rgb, options).Format);
        Assert.Equal(TextureFormats.RgbFxt1UNorm, new FxtcTextureCoder(TextureFormats.RgbFxt1UNorm, options).Format);
        Assert.Equal(TextureFormats.RgbEtc1UNorm, new EtcTextureCoder(TextureFormats.RgbEtc1UNorm, options).Format);
        Assert.Equal(TextureFormats.AtcRgb, new AtcTextureCoder(TextureFormats.AtcRgb, options).Format);
        Assert.Equal(TextureFormats.RgbaAstc4x4UNorm, new AstcTextureCoder(TextureFormats.RgbaAstc4x4UNorm, options).Format);

        Assert.Same(options, new RgtcLatcTextureCoder(TextureFormats.Bc5UNorm, options).Options);
        Assert.Same(options, new BptcTextureCoder(TextureFormats.Bc7UNorm, options).Options);
        Assert.Same(options, new PvrtcTextureCoder(TextureFormats.RgbaPvrtcI4BppUNorm, options).Options);
    }
}
