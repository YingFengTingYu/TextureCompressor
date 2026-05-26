namespace TextureCompressor.Colors;

public interface IConvertibleToRgba32UNorm<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba32UNorm<TSelf>
{
    static abstract Rgba32UNorm ToRgba32UNorm(TSelf value);
    static abstract TSelf FromRgba32UNorm(Rgba32UNorm value);
}
