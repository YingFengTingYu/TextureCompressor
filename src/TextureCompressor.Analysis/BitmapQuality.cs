using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Analysis;

public static class BitmapQuality
{
    private const double MaxChannelValue = byte.MaxValue;

    public static BitmapQualityResult Compare<TExpected, TActual>(
        IBitmap<TExpected> expected,
        IBitmap<TActual> actual,
        bool includeAlpha = true)
        where TExpected : unmanaged, IPixel<TExpected>
        where TActual : unmanaged, IPixel<TActual>
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            throw new ArgumentException("Bitmaps must have the same dimensions.");
        }

        var expectedPixels = expected.PixelSpan;
        var actualPixels = actual.PixelSpan;
        var channelCount = includeAlpha ? 4 : 3;
        var sampleCount = checked(expectedPixels.Length * channelCount);

        long redSquaredError = 0;
        long greenSquaredError = 0;
        long blueSquaredError = 0;
        long alphaSquaredError = 0;

        for (var i = 0; i < expectedPixels.Length; i++)
        {
            var expectedRgba = TExpected.ToRgba8UNorm(expectedPixels[i]);
            var actualRgba = TActual.ToRgba8UNorm(actualPixels[i]);

            redSquaredError += SquaredDifference(expectedRgba.Red, actualRgba.Red);
            greenSquaredError += SquaredDifference(expectedRgba.Green, actualRgba.Green);
            blueSquaredError += SquaredDifference(expectedRgba.Blue, actualRgba.Blue);
            if (includeAlpha)
            {
                alphaSquaredError += SquaredDifference(expectedRgba.Alpha, actualRgba.Alpha);
            }
        }

        var totalSquaredError = redSquaredError + greenSquaredError + blueSquaredError + alphaSquaredError;
        var meanSquaredError = (double)totalSquaredError / sampleCount;
        var rmse = Math.Sqrt(meanSquaredError);

        return new BitmapQualityResult(
            expected.Width,
            expected.Height,
            includeAlpha,
            meanSquaredError,
            rmse,
            ToPsnr(meanSquaredError),
            Channel(redSquaredError, expectedPixels.Length),
            Channel(greenSquaredError, expectedPixels.Length),
            Channel(blueSquaredError, expectedPixels.Length),
            includeAlpha ? Channel(alphaSquaredError, expectedPixels.Length) : null);
    }

    private static BitmapChannelQuality Channel(long squaredError, int pixelCount)
    {
        var meanSquaredError = (double)squaredError / pixelCount;
        return new BitmapChannelQuality(meanSquaredError, Math.Sqrt(meanSquaredError), ToPsnr(meanSquaredError));
    }

    private static int SquaredDifference(byte expected, byte actual)
    {
        var difference = expected - actual;
        return difference * difference;
    }

    private static double ToPsnr(double meanSquaredError) =>
        meanSquaredError == 0
            ? double.PositiveInfinity
            : 20 * Math.Log10(MaxChannelValue / Math.Sqrt(meanSquaredError));
}
