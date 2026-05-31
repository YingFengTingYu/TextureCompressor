using System.Buffers.Binary;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrFileFormat : ITextureFileFormat
{
    private const uint Version = 0x03525650;
    private const uint LegacyHeaderV1ByteCount = 44;
    private const uint LegacyHeaderV2ByteCount = 52;

    public string Name => "PVR";

    public IReadOnlyList<string> Extensions { get; } = [".pvr"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension)
    {
        if (header.Length < 4)
        {
            return false;
        }

        var firstWord = BinaryPrimitives.ReadUInt32LittleEndian(header[..4]);
        return firstWord == Version
            || (IsPvrExtension(extension) && firstWord is LegacyHeaderV1ByteCount or LegacyHeaderV2ByteCount);
    }

    public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null)
    {
        RejectReadOptions(options);
        return PvrCodec.Read(stream);
    }

    public void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null)
    {
        PvrCodec.Write(new PvrTexture(texture), stream, GetEncodingOptions(options));
    }

    private static PvrEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            PvrEncodingOptions pvrOptions => pvrOptions,
            _ => throw new ArgumentException("PVR texture write options must be PvrEncodingOptions.", nameof(options))
        };

    private static void RejectReadOptions(IFileFormatOptions? options)
    {
        if (options is not null)
        {
            throw new ArgumentException("PVR texture read options are not supported.", nameof(options));
        }
    }

    private static bool IsPvrExtension(string? extension) =>
        string.Equals(extension, ".pvr", StringComparison.OrdinalIgnoreCase);
}
