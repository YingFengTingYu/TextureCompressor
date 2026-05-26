namespace TextureCompressor.Colors;

public interface IConvertibleToRgba8UNorm<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba8UNorm<TSelf>
{
    static abstract Rgba8UNorm ToRgba8UNorm(TSelf value);
    static abstract TSelf FromRgba8UNorm(Rgba8UNorm value);
}