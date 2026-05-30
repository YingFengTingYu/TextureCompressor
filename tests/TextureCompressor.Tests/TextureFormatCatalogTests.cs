using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureFormatCatalogTests
{
    [Fact]
    public void TryGetFindsFormatByFieldName()
    {
        var found = TextureFormatCatalog.TryGet(nameof(TextureFormats.Bc7UNorm), out var format);

        Assert.True(found);
        Assert.Equal(TextureFormats.Bc7UNorm, format);
    }

    [Fact]
    public void TryGetFindsFormatByTextureFormatName()
    {
        var found = TextureFormatCatalog.TryGet("BC7_UNORM", out var format);

        Assert.True(found);
        Assert.Equal(TextureFormats.Bc7UNorm, format);
    }

    [Fact]
    public void TryGetIsCaseInsensitive()
    {
        var found = TextureFormatCatalog.TryGet("bc7_unorm", out var format);

        Assert.True(found);
        Assert.Equal(TextureFormats.Bc7UNorm, format);
    }

    [Fact]
    public void AllContainsKnownFormats()
    {
        Assert.Contains(TextureFormats.Rgba8UNorm, TextureFormatCatalog.All.ToArray());
        Assert.Contains(TextureFormats.Bc7UNorm, TextureFormatCatalog.All.ToArray());
    }

    [Fact]
    public void GetFieldNameReturnsGeneratedFieldName()
    {
        var fieldName = TextureFormatCatalog.GetFieldName(TextureFormats.Bc7UNorm);

        Assert.Equal(nameof(TextureFormats.Bc7UNorm), fieldName);
    }
}
