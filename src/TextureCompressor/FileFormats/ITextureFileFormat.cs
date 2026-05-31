using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats;

public interface ITextureFileFormat : IFileFormat
{
    ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null);

    void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null);
}
