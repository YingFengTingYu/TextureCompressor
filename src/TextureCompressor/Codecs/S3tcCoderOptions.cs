namespace TextureCompressor.Codecs;

public sealed class S3tcCoderOptions
{
    public TextureCompressionLevel CompressionMode { get; init; } = TextureCompressionLevel.Fast;
}
