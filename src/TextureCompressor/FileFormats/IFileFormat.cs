namespace TextureCompressor.FileFormats;

public interface IFileFormat
{
    string Name { get; }

    IReadOnlyList<string> Extensions { get; }

    bool CanRead(ReadOnlySpan<byte> header, string? extension);
}
