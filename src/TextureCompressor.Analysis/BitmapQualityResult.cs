namespace TextureCompressor.Analysis;

public sealed record BitmapQualityResult(
    int Width,
    int Height,
    bool IncludesAlpha,
    double MeanSquaredError,
    double RootMeanSquaredError,
    double PeakSignalToNoiseRatio,
    BitmapChannelQuality Red,
    BitmapChannelQuality Green,
    BitmapChannelQuality Blue,
    BitmapChannelQuality? Alpha);
