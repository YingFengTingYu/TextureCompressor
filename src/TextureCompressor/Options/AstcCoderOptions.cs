namespace TextureCompressor.Options;

public sealed class AstcCoderOptions
{
    public TextureCompressionLevel CompressionMode { get; init; } = TextureCompressionLevel.Fast;
}
