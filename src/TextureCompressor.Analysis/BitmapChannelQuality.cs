namespace TextureCompressor.Analysis;

public sealed record BitmapChannelQuality(
    double MeanSquaredError,
    double RootMeanSquaredError,
    double PeakSignalToNoiseRatio);
