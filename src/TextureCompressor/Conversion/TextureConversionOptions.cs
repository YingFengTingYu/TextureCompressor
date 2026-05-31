using TextureCompressor.FileFormats;
using TextureCompressor.Formats;
using TextureCompressor.Options;

namespace TextureCompressor.Conversion;

public sealed class TextureConversionOptions
{
    public TextureFormat? TargetFormat { get; init; }

    public TextureSubresourceSelection? SourceSubresource { get; init; }

    public TextureConversionMipmaps Mipmaps { get; init; }

    public TextureCompressionLevel? CompressionLevel { get; init; }

    public IFileFormatOptions? ReadOptions { get; init; }

    public IFileFormatOptions? WriteOptions { get; init; }
}
