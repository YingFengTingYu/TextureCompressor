using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Ktx;

public sealed class KtxEncodingOptions
{
    public KtxVersion Version { get; set; } = KtxVersion.Version1;

    public TextureFormat? TextureFormat { get; set; }

    public KtxGlFormat? GlInternalFormat { get; set; }

    public KtxVkFormat? VkFormat { get; set; }

    public KtxSupercompressionScheme SupercompressionScheme { get; set; } = KtxSupercompressionScheme.None;

    public int ZstandardCompressionLevel { get; set; } = 3;

    public System.IO.Compression.CompressionLevel ZlibCompressionLevel { get; set; } = System.IO.Compression.CompressionLevel.Optimal;

    public bool IsSrgb { get; set; }
}
