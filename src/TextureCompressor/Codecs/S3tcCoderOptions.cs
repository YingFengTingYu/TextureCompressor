namespace TextureCompressor.Codecs;

public sealed class S3tcCoderOptions
{
    public S3tcCompressionMode CompressionMode { get; init; } = S3tcCompressionMode.Fast;
}
