using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Jpeg;

public sealed class JpegFileFormat : IImageFileFormat
{
    public string Name => "JPEG";

    public IReadOnlyList<string> Extensions { get; } = [".jpg", ".jpeg"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
        header.Length >= 2 && header[0] == 0xff && header[1] == 0xd8;

    public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RejectReadOptions(options);
        return JpegCodec.Decode<TPixel>(stream);
    }

    public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegCodec.Encode(image, stream, GetEncodingOptions(options));
    }

    private static JpegEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            JpegEncodingOptions jpegOptions => jpegOptions,
            _ => throw new ArgumentException("JPEG image write options must be JpegEncodingOptions.", nameof(options))
        };

    private static void RejectReadOptions(IFileFormatOptions? options)
    {
        if (options is not null)
        {
            throw new ArgumentException("JPEG image read options are not supported.", nameof(options));
        }
    }
}
