namespace TextureCompressor.Colors;

public interface IConvertibleToRgba16Float<TSelf> : IPixel<TSelf>
    where TSelf : unmanaged, IConvertibleToRgba16Float<TSelf>
{
    static abstract Rgba16Float ToRgba16Float(TSelf value);
    static abstract TSelf FromRgba16Float(Rgba16Float value);
}
