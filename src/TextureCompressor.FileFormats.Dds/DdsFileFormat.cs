using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Dds;

public sealed class DdsFileFormat : ITextureFileFormat
{
    public string Name => "DDS";

    public IReadOnlyList<string> Extensions { get; } = [".dds"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
        header.Length >= 4 && header[..4].SequenceEqual("DDS "u8);

    public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null)
    {
        RejectReadOptions(options);
        return DdsCodec.Read(stream);
    }

    public void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null)
    {
        DdsCodec.Write(new DdsTexture(texture), stream, GetEncodingOptions(options));
    }

    private static DdsEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            DdsEncodingOptions ddsOptions => ddsOptions,
            _ => throw new ArgumentException("DDS texture write options must be DdsEncodingOptions.", nameof(options))
        };

    private static void RejectReadOptions(IFileFormatOptions? options)
    {
        if (options is not null)
        {
            throw new ArgumentException("DDS texture read options are not supported.", nameof(options));
        }
    }
}
