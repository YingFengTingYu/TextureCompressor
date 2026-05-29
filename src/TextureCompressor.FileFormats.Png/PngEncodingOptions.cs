using System.IO.Compression;

namespace TextureCompressor.FileFormats.Png;

public sealed class PngEncodingOptions
{
    public bool UseAppleCgbi { get; set; }

    public int MaxIdatChunkDataLength { get; set; } = 0x7fff;

    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.SmallestSize;
}
