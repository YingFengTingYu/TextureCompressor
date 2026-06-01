using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc;

public sealed class AstcFileFormat : ITextureFileFormat
{
    private static ReadOnlySpan<byte> Magic => [0x13, 0xab, 0xa1, 0x5c];

    public string Name => "ASTC";

    public IReadOnlyList<string> Extensions { get; } = [".astc"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
        header.Length >= Magic.Length && header[..Magic.Length].SequenceEqual(Magic);

    public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null) =>
        AstcCodec.Read(stream, GetReadOptions(options));

    public void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null)
    {
        ValidateSingleImage(texture);
        ValidateEncodingOptions(texture, GetEncodingOptions(options));
        AstcCodec.Write(new AstcTexture(texture.Format, texture.Width, texture.Height, texture.Depth, texture.Payload), stream);
    }

    private static AstcReadOptions? GetReadOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            AstcReadOptions astcOptions => astcOptions,
            _ => throw new ArgumentException("ASTC texture read options must be AstcReadOptions.", nameof(options))
        };

    private static AstcEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            AstcEncodingOptions astcOptions => astcOptions,
            _ => throw new ArgumentException("ASTC texture write options must be AstcEncodingOptions.", nameof(options))
        };

    private static void ValidateSingleImage(TextureImage texture)
    {
        if (texture.MipLevelCount != 1 || texture.ArrayLayerCount != 1 || texture.FaceCount != 1)
        {
            throw new NotSupportedException("ASTC files support only one texture subresource.");
        }
    }

    private static void ValidateEncodingOptions(TextureImage texture, AstcEncodingOptions? options)
    {
        if (options?.TextureFormat is { } format && format != texture.Format)
        {
            throw new ArgumentException(
                $"ASTC encoding options specify texture format '{format.Name}', but the texture payload uses '{texture.Format.Name}'.",
                nameof(options));
        }
    }
}
