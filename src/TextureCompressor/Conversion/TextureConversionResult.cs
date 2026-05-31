using TextureCompressor.Formats;

namespace TextureCompressor.Conversion;

public sealed record TextureConversionResult(
    TextureConversionFileKind SourceKind,
    TextureConversionFileKind TargetKind,
    int Width,
    int Height,
    TextureFormat? SourceTextureFormat,
    TextureFormat? TargetTextureFormat,
    int MipLevelCount,
    int ArrayLayerCount,
    int FaceCount);
