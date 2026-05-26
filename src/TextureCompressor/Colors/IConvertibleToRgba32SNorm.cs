namespace TextureCompressor.Colors;

public interface IConvertibleToRgba32SNorm<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba32SNorm<TSelf>
{
    static abstract Rgba32SNorm ToRgba32SNorm(TSelf value);
    static abstract TSelf FromRgba32SNorm(Rgba32SNorm value);
}
