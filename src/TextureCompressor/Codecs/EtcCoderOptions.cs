namespace TextureCompressor.Codecs;

public sealed class EtcCoderOptions
{
    public TextureCompressionLevel CompressionMode { get; init; } = TextureCompressionLevel.Fast;
}
