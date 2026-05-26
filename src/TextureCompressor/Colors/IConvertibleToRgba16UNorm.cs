namespace TextureCompressor.Colors;

public interface IConvertibleToRgba16UNorm<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba16UNorm<TSelf>
{
    static abstract Rgba16UNorm ToRgba16UNorm(TSelf value);
    static abstract TSelf FromRgba16UNorm(Rgba16UNorm value);
}
