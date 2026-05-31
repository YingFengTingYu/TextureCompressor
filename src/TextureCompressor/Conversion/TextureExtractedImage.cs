using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Conversion;

public sealed record TextureExtractedImage<TPixel>(
    int MipLevel,
    int ArrayLayer,
    TextureCubeFace? Face,
    int FaceIndex,
    ArrayBitmap<TPixel> Image)
    where TPixel : unmanaged, IPixel<TPixel>;
