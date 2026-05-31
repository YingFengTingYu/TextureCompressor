using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats;

public interface IImageFileFormat : IFileFormat
{
    ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>;

    void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>;
}
