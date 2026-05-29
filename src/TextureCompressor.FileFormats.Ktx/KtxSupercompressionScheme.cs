namespace TextureCompressor.FileFormats.Ktx;

public enum KtxSupercompressionScheme : uint
{
    None = 0,
    BasisLz = 1,
    Zstandard = 2,
    Zlib = 3
}
