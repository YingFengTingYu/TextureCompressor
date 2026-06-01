using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc;

public sealed class AstcEncodingOptions : IFileFormatOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public AstcProfile Profile { get; init; } = AstcProfile.UNorm;

    public int BlockWidth { get; init; } = 4;

    public int BlockHeight { get; init; } = 4;

    public int? BlockDepth { get; init; }
}
