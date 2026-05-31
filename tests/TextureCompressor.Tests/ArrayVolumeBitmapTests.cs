using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Tests;

public sealed class ArrayVolumeBitmapTests
{
    [Fact]
    public void ConstructorInitializesDimensionsAndPixels()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(1, 2, 3),
            new Rgba8UNorm(4, 5, 6),
            new Rgba8UNorm(7, 8, 9),
            new Rgba8UNorm(10, 11, 12),
            new Rgba8UNorm(13, 14, 15),
            new Rgba8UNorm(16, 17, 18),
            new Rgba8UNorm(19, 20, 21),
            new Rgba8UNorm(22, 23, 24)
        };

        var bitmap = new ArrayVolumeBitmap<Rgba8UNorm>(2, 2, 2, pixels);

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
        Assert.Equal(2, bitmap.Depth);
        Assert.Same(pixels, bitmap.Pixels);
        Assert.Equal(8, bitmap.Pixels.Length);
        Assert.Equal(8, bitmap.PixelSpan.Length);
    }

    [Fact]
    public void ConstructorAllocatesExpectedPixelCount()
    {
        var bitmap = new ArrayVolumeBitmap<Rgba8UNorm>(3, 2, 4);

        Assert.Equal(24, bitmap.Pixels.Length);
        Assert.Equal(24, bitmap.PixelSpan.Length);
    }

    [Theory]
    [InlineData(0, 1, 1, "width")]
    [InlineData(-1, 1, 1, "width")]
    [InlineData(1, 0, 1, "height")]
    [InlineData(1, -1, 1, "height")]
    [InlineData(1, 1, 0, "depth")]
    [InlineData(1, 1, -1, "depth")]
    public void ConstructorRejectsInvalidDimensions(int width, int height, int depth, string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayVolumeBitmap<Rgba8UNorm>(width, height, depth, []));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void ConstructorRejectsTooSmallPixelArray()
    {
        var pixels = new Rgba8UNorm[7];

        var exception = Assert.Throws<ArgumentException>(() => new ArrayVolumeBitmap<Rgba8UNorm>(2, 2, 2, pixels));

        Assert.Equal("pixels", exception.ParamName);
    }

    [Fact]
    public void AsViewIndexesVolumePixels()
    {
        var pixels = Enumerable.Range(0, 8)
            .Select(static value => new Rgba8UNorm((byte)value, (byte)(value + 1), (byte)(value + 2)))
            .ToArray();
        var bitmap = new ArrayVolumeBitmap<Rgba8UNorm>(2, 2, 2, pixels);

        var view = bitmap.AsView();

        Assert.Equal(new Rgba8UNorm(6, 7, 8), view[0, 1, 1]);
    }

    [Fact]
    public void ImplementsCommonVolumeBitmapInterface()
    {
        IVolumeBitmap<Rgba8UNorm> bitmap = new ArrayVolumeBitmap<Rgba8UNorm>(2, 2, 2);

        bitmap.PixelSpan[6] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(new Rgba8UNorm(20, 21, 22), bitmap.AsView()[0, 1, 1]);
        Assert.Equal(new Rgba8UNorm(20, 21, 22), bitmap.GetSliceView(1)[0, 1]);
    }
}
