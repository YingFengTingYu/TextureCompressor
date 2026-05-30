namespace TextureCompressor.Codecs;

public sealed class BptcCoderOptions
{
    public BptcCompressionMode CompressionMode { get; init; } = BptcCompressionMode.Fast;
}
