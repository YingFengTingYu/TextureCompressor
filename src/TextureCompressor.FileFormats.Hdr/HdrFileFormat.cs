using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Hdr;

public sealed class HdrFileFormat : IImageFileFormat
{
    public string Name => "Radiance HDR";

    public IReadOnlyList<string> Extensions { get; } = [".hdr"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) => HdrCodec.HasRadianceHeader(header);

    public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RejectReadOptions(options);
        return HdrCodec.Decode<TPixel>(stream);
    }

    public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HdrCodec.Encode(image, stream, GetEncodingOptions(options));
    }

    private static HdrEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            HdrEncodingOptions hdrOptions => hdrOptions,
            _ => throw new ArgumentException("HDR image write options must be HdrEncodingOptions.", nameof(options))
        };

    private static void RejectReadOptions(IFileFormatOptions? options)
    {
        if (options is not null)
        {
            throw new ArgumentException("HDR image read options are not supported.", nameof(options));
        }
    }
}
