using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Png;

public sealed class PngFileFormat : IImageFileFormat
{
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public string Name => "PNG";

    public IReadOnlyList<string> Extensions { get; } = [".png"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
        header.Length >= Signature.Length && header[..Signature.Length].SequenceEqual(Signature);

    public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RejectReadOptions(options);
        return PngCodec.Decode<TPixel>(stream);
    }

    public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PngCodec.Encode(image, stream, GetEncodingOptions(options));
    }

    private static PngEncodingOptions? GetEncodingOptions(IFileFormatOptions? options) =>
        options switch
        {
            null => null,
            PngEncodingOptions pngOptions => pngOptions,
            _ => throw new ArgumentException("PNG image write options must be PngEncodingOptions.", nameof(options))
        };

    private static void RejectReadOptions(IFileFormatOptions? options)
    {
        if (options is not null)
        {
            throw new ArgumentException("PNG image read options are not supported.", nameof(options));
        }
    }
}
