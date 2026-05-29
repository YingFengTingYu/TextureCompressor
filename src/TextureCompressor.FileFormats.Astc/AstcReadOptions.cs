using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc;

public sealed class AstcReadOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public AstcProfile Profile { get; init; } = AstcProfile.UNorm;
}
