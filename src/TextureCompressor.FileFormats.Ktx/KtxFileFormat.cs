using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Ktx;

public sealed class KtxFileFormat : ITextureFileFormat
{
    private static ReadOnlySpan<byte> IdentifierV1 => [0xab, 0x4b, 0x54, 0x58, 0x20, 0x31, 0x31, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a];
    private static ReadOnlySpan<byte> IdentifierV2 => [0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a];

    public string Name => "KTX";

    public IReadOnlyList<string> Extensions { get; } = [".ktx", ".ktx2"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
        header.Length >= IdentifierV1.Length
        && (header[..IdentifierV1.Length].SequenceEqual(IdentifierV1) || header[..IdentifierV2.Length].SequenceEqual(IdentifierV2));

    public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null)
    {
        RejectReadOptions(options);
        return KtxCodec.Read(stream);
    }

    public void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null)
    {
        KtxCodec.Write(new KtxTexture(texture), stream, GetEncodingOptions(options));
    }

    private static KtxEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            KtxEncodingOptions ktxOptions => ktxOptions,
            _ => throw new ArgumentException("KTX texture write options must be KtxEncodingOptions.", nameof(options))
        };

    private static void RejectReadOptions(IFileFormatOptions? options)
    {
        if (options is not null)
        {
            throw new ArgumentException("KTX texture read options are not supported.", nameof(options));
        }
    }
}
