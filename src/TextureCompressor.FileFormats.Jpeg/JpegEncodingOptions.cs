namespace TextureCompressor.FileFormats.Jpeg;

public sealed class JpegEncodingOptions : IFileFormatOptions
{
    public int Quality { get; set; } = 90;
}
