namespace TextureCompressor.Codecs;

public sealed class AstcCoderOptions
{
    public AstcCompressionMode CompressionMode { get; init; } = AstcCompressionMode.Fast;
}
