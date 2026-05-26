using TextureCompressor.Formats;

namespace TextureCompressor.Colors;

public interface IPixel<TSelf>
    where TSelf : unmanaged, IPixel<TSelf>
{
    static abstract TextureFormat Format { get; }
}
