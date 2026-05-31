namespace TextureCompressor.Options;

public sealed class BptcCoderOptions
{
    public TextureCompressionLevel CompressionMode { get; init; } = TextureCompressionLevel.Fast;
}
