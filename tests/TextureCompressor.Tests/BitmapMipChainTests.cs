using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Tests;

public sealed class BitmapMipChainTests
{
    [Fact]
    public void GenerateReturnsAllLevelsToOneByOne()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            2,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(20, 0, 0),
                new Rgba8UNorm(80, 0, 0),
                new Rgba8UNorm(100, 0, 0),
                new Rgba8UNorm(40, 0, 0),
                new Rgba8UNorm(60, 0, 0),
                new Rgba8UNorm(120, 0, 0),
                new Rgba8UNorm(140, 0, 0)
            ]);

        var levels = BitmapMipChain.Generate(source);

        Assert.Equal(3, levels.Count);
        Assert.Equal(source.Width, levels[0].Width);
        Assert.Equal(source.Height, levels[0].Height);
        Assert.Equal(2, levels[1].Width);
        Assert.Equal(1, levels[1].Height);
        Assert.Equal(1, levels[2].Width);
        Assert.Equal(1, levels[2].Height);
        Assert.Equal(new Rgba8UNorm(30, 0, 0), levels[1].AsView()[0, 0]);
        Assert.Equal(new Rgba8UNorm(110, 0, 0), levels[1].AsView()[1, 0]);
        Assert.Equal(new Rgba8UNorm(70, 0, 0), levels[2].AsView()[0, 0]);
    }

    [Fact]
    public void GenerateHandlesOddDimensions()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(7, 5);

        var levels = BitmapMipChain.Generate(source);

        Assert.Equal(3, levels.Count);
        Assert.Equal(new[] { 7, 3, 1 }, levels.Select(level => level.Width));
        Assert.Equal(new[] { 5, 2, 1 }, levels.Select(level => level.Height));
    }

    [Fact]
    public void GenerateOneByOneReturnsBaseLevelOnly()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1);

        var levels = BitmapMipChain.Generate(source);

        Assert.Single(levels);
        Assert.Equal(source.Width, levels[0].Width);
        Assert.Equal(source.Height, levels[0].Height);
    }

    [Fact]
    public void GenerateRespectsMaxLevelCount()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(8, 8);

        var levels = BitmapMipChain.Generate(source, new MipmapGenerationOptions { MaxLevelCount = 2 });

        Assert.Equal(2, levels.Count);
        Assert.Equal(8, levels[0].Width);
        Assert.Equal(4, levels[1].Width);
    }

    [Fact]
    public void GenerateFromViewCopiesBaseLevel()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(1, 2, 3),
                new Rgba8UNorm(5, 6, 7)
            ]);

        var levels = BitmapMipChain.Generate(source.AsView());
        source.AsView()[0, 0] = new Rgba8UNorm(9, 10, 11);

        Assert.Equal(2, levels.Count);
        Assert.Equal(new Rgba8UNorm(1, 2, 3), levels[0].AsView()[0, 0]);
        Assert.Equal(new Rgba8UNorm(3, 4, 5), levels[1].AsView()[0, 0]);
    }

    [Fact]
    public void DownsampleUsesPremultipliedAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0, 0),
                new Rgba8UNorm(0, 0, 255, 255)
            ]);

        var mip = BitmapMipChain.Downsample(source);

        Assert.Equal(new Rgba8UNorm(0, 0, 255, 128), mip.AsView()[0, 0]);
    }

    [Fact]
    public void DownsampleCanUseStraightAlpha()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(255, 0, 0, 0),
                new Rgba8UNorm(0, 0, 255, 255)
            ]);

        var mip = BitmapMipChain.Downsample(source, new MipmapGenerationOptions { AlphaMode = MipmapAlphaMode.Straight });

        Assert.Equal(new Rgba8UNorm(128, 0, 128, 128), mip.AsView()[0, 0]);
    }

    [Fact]
    public void DownsampleCanUseSrgbColorSpace()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            2,
            1,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 255, 255)
            ]);

        var mip = BitmapMipChain.Downsample(source, new MipmapGenerationOptions { ColorSpace = MipmapColorSpace.Srgb });

        var expected = RgbaColorConversions.LinearFloatToSrgb8(0.5f);
        Assert.Equal(new Rgba8UNorm(expected, expected, expected), mip.AsView()[0, 0]);
    }

    [Fact]
    public void DownsampleCanUseTriangleFilter()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            1,
            [
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(0, 0, 0),
                new Rgba8UNorm(255, 0, 0),
                new Rgba8UNorm(255, 0, 0)
            ]);

        var mip = BitmapMipChain.Downsample(source, new MipmapGenerationOptions { Filter = MipmapFilter.Triangle });

        Assert.Equal(2, mip.Width);
        Assert.InRange((int)mip.AsView()[0, 0].Red, 1, 63);
        Assert.InRange((int)mip.AsView()[1, 0].Red, 192, 254);
    }

    [Fact]
    public void DownsampleRejectsOneByOneSource()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(1, 1);

        var exception = Assert.Throws<ArgumentException>(() => BitmapMipChain.Downsample(source));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void GenerateRejectsInvalidMaxLevelCount()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BitmapMipChain.Generate(source, new MipmapGenerationOptions { MaxLevelCount = 0 }));
    }

    [Fact]
    public void GenerateRejectsInvalidFilter()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BitmapMipChain.Generate(source, new MipmapGenerationOptions { Filter = (MipmapFilter)999 }));
    }
}
