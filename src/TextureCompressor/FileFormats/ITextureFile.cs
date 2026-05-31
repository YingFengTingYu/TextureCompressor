using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats;

public interface ITextureFile
{
    TextureImage Texture { get; }
}
