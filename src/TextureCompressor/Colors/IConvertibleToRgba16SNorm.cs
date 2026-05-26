namespace TextureCompressor.Colors;

public interface IConvertibleToRgba16SNorm<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba16SNorm<TSelf>
{
    static abstract Rgba16SNorm ToRgba16SNorm(TSelf value);
    static abstract TSelf FromRgba16SNorm(Rgba16SNorm value);
}
