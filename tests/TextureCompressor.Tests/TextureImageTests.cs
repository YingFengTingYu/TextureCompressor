using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureImageTests
{
    [Fact]
    public void SinglePayloadConstructorSetsBaseProperties()
    {
        var payload = new byte[] { 1, 2, 3, 4 };

        var image = new TextureImage(TextureFormats.Rgba8UNorm, width: 1, height: 1, payload);

        Assert.Equal(TextureFormats.Rgba8UNorm, image.Format);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(1, image.MipLevelCount);
        Assert.Equal(1, image.ArrayLayerCount);
        Assert.Equal(1, image.FaceCount);
        Assert.False(image.IsCubeMap);
        Assert.Same(payload, image.Payload);
        Assert.Same(payload, image.Data);
        Assert.Same(payload, image.GetSubresource(0).Payload);
    }

    [Fact]
    public void SubresourceConstructorCreatesMipChain()
    {
        var payload0 = new byte[] { 1 };
        var payload1 = new byte[] { 2 };
        var subresources = new[]
        {
            new TextureSubresource(0, arrayLayer: 0, faceIndex: 0, 2, 2, payload0),
            new TextureSubresource(1, arrayLayer: 0, faceIndex: 0, 1, 1, payload1)
        };

        var image = new TextureImage(TextureFormats.Rgba8UNorm, subresources, faceCount: 1);

        Assert.Equal(2, image.MipLevelCount);
        Assert.Equal(2, image.Subresources.Count);
        Assert.Same(payload0, image.GetSubresource(0).Payload);
        Assert.Same(payload1, image.GetSubresource(1).Payload);
    }

    [Fact]
    public void SubresourceConstructorOrdersByArrayLayerAndMipLevel()
    {
        var image = new TextureImage(
            TextureFormats.Rgba8UNorm,
            [
                Subresource(mipLevel: 1, arrayLayer: 1, faceIndex: 0, width: 1, height: 1, value: 12),
                Subresource(mipLevel: 0, arrayLayer: 0, faceIndex: 0, width: 2, height: 2, value: 1),
                Subresource(mipLevel: 0, arrayLayer: 1, faceIndex: 0, width: 2, height: 2, value: 2),
                Subresource(mipLevel: 1, arrayLayer: 0, faceIndex: 0, width: 1, height: 1, value: 11)
            ],
            arrayLayerCount: 2,
            faceCount: 1);

        Assert.Equal(new byte[] { 1, 11, 2, 12 }, image.Subresources.Select(static subresource => subresource.Payload[0]));
        Assert.Equal(12, image.GetSubresource(mipLevel: 1, arrayLayer: 1).Payload[0]);
        Assert.Equal(new[] { 2, 1 }, image.Subresources.Take(2).Select(static subresource => subresource.Width));
    }

    [Fact]
    public void CubeMapSubresourcesExposeFaceSelection()
    {
        var subresources = Enumerable
            .Range(0, 6)
            .Select(static face => Subresource(mipLevel: 0, arrayLayer: 0, face, width: 1, height: 1, value: face + 1))
            .ToArray();

        var image = new TextureImage(TextureFormats.Rgba8UNorm, subresources, faceCount: 6);

        Assert.True(image.IsCubeMap);
        Assert.Equal(6, image.FaceCount);
        Assert.Equal(6, image.GetSubresource(0, TextureCubeFace.NegativeZ).Payload[0]);
    }

    [Fact]
    public void StaticSubresourceIndexUsesMipFastestOrder()
    {
        var index = TextureImage.GetSubresourceIndex(mipLevel: 1, arrayLayer: 1, faceIndex: 2, mipLevelCount: 2, arrayLayerCount: 3, faceCount: 6);

        Assert.Equal(17, index);
    }

    [Fact]
    public void SubresourceConstructorRejectsMissingBaseSubresource()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TextureImage(
            TextureFormats.Rgba8UNorm,
            [Subresource(mipLevel: 1, arrayLayer: 0, faceIndex: 0, width: 1, height: 1, value: 1)],
            arrayLayerCount: 1,
            faceCount: 1));

        Assert.Equal("subresources", exception.ParamName);
    }

    [Fact]
    public void SubresourceConstructorRejectsDuplicateSubresources()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TextureImage(
            TextureFormats.Rgba8UNorm,
            [
                Subresource(mipLevel: 0, arrayLayer: 0, faceIndex: 0, width: 2, height: 2, value: 1),
                Subresource(mipLevel: 0, arrayLayer: 0, faceIndex: 0, width: 2, height: 2, value: 2)
            ],
            arrayLayerCount: 2,
            faceCount: 1));

        Assert.Equal("subresources", exception.ParamName);
    }

    [Fact]
    public void SubresourceConstructorRejectsUnexpectedMipDimensions()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TextureImage(
            TextureFormats.Rgba8UNorm,
            [
                Subresource(mipLevel: 0, arrayLayer: 0, faceIndex: 0, width: 2, height: 2, value: 1),
                Subresource(mipLevel: 1, arrayLayer: 0, faceIndex: 0, width: 2, height: 1, value: 2)
            ],
            arrayLayerCount: 1,
            faceCount: 1));

        Assert.Equal("subresources", exception.ParamName);
    }

    [Fact]
    public void GetSubresourceRejectsOutOfRangeCoordinates()
    {
        var image = new TextureImage(TextureFormats.Rgba8UNorm, width: 1, height: 1, new byte[] { 1 });

        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSubresource(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSubresource(0, arrayLayer: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSubresource(0, faceIndex: 1));
    }

    private static TextureSubresource Subresource(int mipLevel, int arrayLayer, int faceIndex, int width, int height, int value) =>
        new(mipLevel, arrayLayer, faceIndex, width, height, [(byte)value]);
}
