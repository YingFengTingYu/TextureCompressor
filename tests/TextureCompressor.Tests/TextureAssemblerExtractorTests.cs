using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Conversion;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureAssemblerExtractorTests
{
    [Fact]
    public void CreateArrayAndExtractPreservesLayerOrder()
    {
        var assembler = new TextureAssembler();
        var extractor = new TextureExtractor();
        IBitmap<Rgba8UNorm>[] layers =
        [
            CreateSolidImage(2, 2, new Rgba8UNorm(16, 0, 0, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(32, 0, 0, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(64, 0, 0, 255))
        ];

        var texture = assembler.CreateArray(TextureFormats.Rgba8UNorm, layers);
        var extracted = extractor.Extract(texture, new TextureSubresourceFilter(ArrayLayer: 1));

        Assert.Equal(3, texture.ArrayLayerCount);
        Assert.Equal(1, texture.MipLevelCount);
        var image = Assert.Single(extracted);
        Assert.Equal(1, image.ArrayLayer);
        Assert.Null(image.Face);
        Assert.Equal(32, image.Image.PixelSpan[0].Red);
    }

    [Fact]
    public void CreateCubeAndExtractPreservesFaceOrder()
    {
        var assembler = new TextureAssembler();
        var extractor = new TextureExtractor();
        IBitmap<Rgba8UNorm>[] faces =
        [
            CreateSolidImage(2, 2, new Rgba8UNorm(255, 0, 0, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(0, 255, 0, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(0, 0, 255, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(255, 255, 0, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(255, 0, 255, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(0, 255, 255, 255))
        ];

        var texture = assembler.CreateCube(TextureFormats.Rgba8UNorm, faces);
        var extracted = extractor.Extract(texture, new TextureSubresourceFilter(Face: TextureCubeFace.PositiveZ));

        Assert.True(texture.IsCubeMap);
        var image = Assert.Single(extracted);
        Assert.Equal(TextureCubeFace.PositiveZ, image.Face);
        Assert.Equal(4, image.FaceIndex);
        Assert.Equal(255, image.Image.PixelSpan[0].Red);
        Assert.Equal(0, image.Image.PixelSpan[0].Green);
        Assert.Equal(255, image.Image.PixelSpan[0].Blue);
    }

    [Fact]
    public void CreateMipChainAndExtractCanSelectMipLevel()
    {
        var assembler = new TextureAssembler();
        var extractor = new TextureExtractor();
        IBitmap<Rgba8UNorm>[] mipLevels =
        [
            CreateSolidImage(4, 4, new Rgba8UNorm(16, 0, 0, 255)),
            CreateSolidImage(2, 2, new Rgba8UNorm(32, 0, 0, 255)),
            CreateSolidImage(1, 1, new Rgba8UNorm(64, 0, 0, 255))
        ];

        var texture = assembler.CreateMipChain(TextureFormats.Rgba8UNorm, mipLevels);
        var extracted = extractor.ExtractSubresource(texture, new TextureSubresourceSelection(2, 0, null));

        Assert.Equal(3, texture.MipLevelCount);
        Assert.Equal(2, extracted.MipLevel);
        Assert.Equal(1, extracted.Image.Width);
        Assert.Equal(1, extracted.Image.Height);
        Assert.Equal(64, extracted.Image.PixelSpan[0].Red);
    }

    [Fact]
    public void CreateMipChainGeneratesFullChainFromSource()
    {
        var texture = new TextureAssembler().CreateMipChain(
            TextureFormats.Rgba8UNorm,
            CreateSolidImage(4, 4, new Rgba8UNorm(16, 0, 0, 255)));

        Assert.Equal(3, texture.MipLevelCount);
        Assert.Equal(4, texture.GetSubresource(0).Width);
        Assert.Equal(2, texture.GetSubresource(1).Width);
        Assert.Equal(1, texture.GetSubresource(2).Width);
    }

    [Fact]
    public void CreateArrayMipChainGeneratesMipLevelsForEachLayer()
    {
        var assembler = new TextureAssembler();
        var extractor = new TextureExtractor();
        IBitmap<Rgba8UNorm>[] layers =
        [
            CreateSolidImage(4, 4, new Rgba8UNorm(16, 0, 0, 255)),
            CreateSolidImage(4, 4, new Rgba8UNorm(64, 0, 0, 255))
        ];

        var texture = assembler.CreateArrayMipChain(
            TextureFormats.Rgba8UNorm,
            layers,
            mipmapOptions: new MipmapGenerationOptions { MaxLevelCount = 2 });
        var extracted = extractor.ExtractSubresource(texture, new TextureSubresourceSelection(1, 1, null));

        Assert.Equal(2, texture.ArrayLayerCount);
        Assert.Equal(2, texture.MipLevelCount);
        Assert.Equal(4, texture.Subresources.Count);
        Assert.Equal(2, extracted.Image.Width);
        Assert.Equal(2, extracted.Image.Height);
        Assert.Equal(64, extracted.Image.PixelSpan[0].Red);
    }

    [Fact]
    public void CreateCubeMipChainGeneratesMipLevelsForEachFace()
    {
        var assembler = new TextureAssembler();
        var extractor = new TextureExtractor();
        IBitmap<Rgba8UNorm>[] faces =
        [
            CreateSolidImage(4, 4, new Rgba8UNorm(255, 0, 0, 255)),
            CreateSolidImage(4, 4, new Rgba8UNorm(0, 255, 0, 255)),
            CreateSolidImage(4, 4, new Rgba8UNorm(0, 0, 255, 255)),
            CreateSolidImage(4, 4, new Rgba8UNorm(255, 255, 0, 255)),
            CreateSolidImage(4, 4, new Rgba8UNorm(255, 0, 255, 255)),
            CreateSolidImage(4, 4, new Rgba8UNorm(0, 255, 255, 255))
        ];

        var texture = assembler.CreateCubeMipChain(
            TextureFormats.Rgba8UNorm,
            faces,
            mipmapOptions: new MipmapGenerationOptions { MaxLevelCount = 2 });
        var extracted = extractor.ExtractSubresource(texture, new TextureSubresourceSelection(1, 0, TextureCubeFace.PositiveZ));

        Assert.True(texture.IsCubeMap);
        Assert.Equal(2, texture.MipLevelCount);
        Assert.Equal(12, texture.Subresources.Count);
        Assert.Equal(TextureCubeFace.PositiveZ, extracted.Face);
        Assert.Equal(2, extracted.Image.Width);
        Assert.Equal(2, extracted.Image.Height);
        Assert.Equal(255, extracted.Image.PixelSpan[0].Red);
        Assert.Equal(255, extracted.Image.PixelSpan[0].Blue);
    }

    [Fact]
    public void CreateArrayRejectsMismatchedDimensions()
    {
        var assembler = new TextureAssembler();
        IBitmap<Rgba8UNorm>[] layers =
        [
            CreateSolidImage(2, 2, new Rgba8UNorm(16, 0, 0, 255)),
            CreateSolidImage(4, 2, new Rgba8UNorm(32, 0, 0, 255))
        ];

        Assert.Throws<ArgumentException>(() => assembler.CreateArray(TextureFormats.Rgba8UNorm, layers));
    }

    [Fact]
    public void ExtractRejectsFaceFilterForNonCubeTexture()
    {
        var texture = new TextureAssembler().CreateArray(
            TextureFormats.Rgba8UNorm,
            [CreateSolidImage(2, 2, new Rgba8UNorm(16, 0, 0, 255))]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TextureExtractor().Extract(texture, new TextureSubresourceFilter(Face: TextureCubeFace.PositiveX)));
    }

    private static ArrayBitmap<Rgba8UNorm> CreateSolidImage(int width, int height, Rgba8UNorm color)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        Array.Fill(pixels, color);
        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }
}
