namespace TextureCompressor.Codecs;

public sealed class BptcCoderOptions
{
    public TextureCompressionLevel CompressionMode { get; init; } = TextureCompressionLevel.Fast;
}
