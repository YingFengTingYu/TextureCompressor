namespace TextureCompressor.FileFormats.Hdr;

public sealed class HdrEncodingOptions : IFileFormatOptions
{
    public bool UseRunLengthEncoding { get; set; } = true;
}
