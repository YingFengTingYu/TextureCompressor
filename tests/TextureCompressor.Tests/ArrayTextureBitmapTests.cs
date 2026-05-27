using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Tests;

public sealed class ArrayTextureBitmapTests
{
    [Fact]
    public void ConstructorInitializesDimensionsAndPixels()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(255, 0, 0),
            new Rgba8UNorm(0, 255, 0),
            new Rgba8UNorm(0, 0, 255),
            new Rgba8UNorm(255, 255, 255)
        };

        var bitmap = new ArrayTextureBitmap<Rgba8UNorm>(2, 2, pixels);

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
        Assert.Same(pixels, bitmap.Pixels);
        Assert.Equal(4, bitmap.Pixels.Length);
        Assert.Equal(4, bitmap.PixelSpan.Length);
    }

    [Fact]
    public void ConstructorAllocatesExpectedPixelCount()
    {
        var bitmap = new ArrayTextureBitmap<Rgba8UNorm>(3, 2);

        Assert.Equal(6, bitmap.Pixels.Length);
        Assert.Equal(6, bitmap.PixelSpan.Length);
    }

    [Theory]
    [InlineData(0, 1, "width")]
    [InlineData(-1, 1, "width")]
    [InlineData(1, 0, "height")]
    [InlineData(1, -1, "height")]
    public void ConstructorRejectsInvalidDimensions(int width, int height, string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayTextureBitmap<Rgba8UNorm>(width, height, []));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void ConstructorRejectsTooSmallPixelArray()
    {
        var pixels = new Rgba8UNorm[3];

        var exception = Assert.Throws<ArgumentException>(() => new ArrayTextureBitmap<Rgba8UNorm>(2, 2, pixels));

        Assert.Equal("pixels", exception.ParamName);
    }

    [Fact]
    public void AsViewIndexesBitmapPixels()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(1, 2, 3),
            new Rgba8UNorm(4, 5, 6),
            new Rgba8UNorm(7, 8, 9),
            new Rgba8UNorm(10, 11, 12)
        };
        var bitmap = new ArrayTextureBitmap<Rgba8UNorm>(2, 2, pixels);

        var view = bitmap.AsView();

        Assert.Equal(new Rgba8UNorm(7, 8, 9), view[0, 1]);
    }

    [Fact]
    public void ImplementsCommonBitmapInterface()
    {
        IBitmap<Rgba8UNorm> bitmap = new ArrayTextureBitmap<Rgba8UNorm>(2, 2);

        bitmap.PixelSpan[3] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(new Rgba8UNorm(20, 21, 22), bitmap.AsView()[1, 1]);
    }
}
