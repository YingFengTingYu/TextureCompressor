namespace TextureCompressor.Colors;

public interface IConvertibleToRgba32Float<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba32Float<TSelf>
{
    static abstract Rgba32Float ToRgba32Float(TSelf value);
    static abstract TSelf FromRgba32Float(Rgba32Float value);
}