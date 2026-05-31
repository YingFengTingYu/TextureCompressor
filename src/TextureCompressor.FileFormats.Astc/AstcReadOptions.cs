using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc;

public sealed class AstcReadOptions : IFileFormatOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public AstcProfile Profile { get; init; } = AstcProfile.UNorm;
}
