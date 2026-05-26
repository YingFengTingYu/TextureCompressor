namespace TextureCompressor.Colors;

public interface IConvertibleToRgba8SNorm<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba8SNorm<TSelf>
{
    static abstract Rgba8SNorm ToRgba8SNorm(TSelf value);
    static abstract TSelf FromRgba8SNorm(Rgba8SNorm value);
}
