using TextureCompressor.Analysis;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Tests;

public sealed class BitmapQualityTests
{
    [Fact]
    public void CompareIdenticalBitmapsReturnsZeroErrorAndInfinitePsnr()
    {
        var expected = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(10, 20, 30, 40)]);
        var actual = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(10, 20, 30, 40)]);

        var quality = BitmapQuality.Compare(expected, actual);

        Assert.Equal(0, quality.MeanSquaredError);
        Assert.Equal(0, quality.RootMeanSquaredError);
        Assert.True(double.IsPositiveInfinity(quality.PeakSignalToNoiseRatio));
        Assert.NotNull(quality.Alpha);
    }

    [Fact]
    public void CompareCanIgnoreAlpha()
    {
        var expected = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(10, 20, 30, 0)]);
        var actual = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(10, 20, 30, 255)]);

        var quality = BitmapQuality.Compare(expected, actual, includeAlpha: false);

        Assert.Equal(0, quality.MeanSquaredError);
        Assert.Null(quality.Alpha);
    }

    [Fact]
    public void CompareReturnsExpectedRgbError()
    {
        var expected = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(10, 20, 30)]);
        var actual = new ArrayBitmap<Rgba8UNorm>(1, 1, [new Rgba8UNorm(13, 24, 30)]);

        var quality = BitmapQuality.Compare(expected, actual, includeAlpha: false);

        Assert.Equal(25.0 / 3, quality.MeanSquaredError, precision: 6);
        Assert.Equal(Math.Sqrt(25.0 / 3), quality.RootMeanSquaredError, precision: 6);
        Assert.Equal(9, quality.Red.MeanSquaredError);
        Assert.Equal(16, quality.Green.MeanSquaredError);
        Assert.Equal(0, quality.Blue.MeanSquaredError);
    }

    [Fact]
    public void CompareDifferentDimensionsThrows()
    {
        var expected = new ArrayBitmap<Rgba8UNorm>(1, 1);
        var actual = new ArrayBitmap<Rgba8UNorm>(2, 1);

        Assert.Throws<ArgumentException>(() => BitmapQuality.Compare(expected, actual));
    }
}
