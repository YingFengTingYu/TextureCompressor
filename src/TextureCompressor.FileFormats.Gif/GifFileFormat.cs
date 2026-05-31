using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.FileFormats.Gif;

public sealed class GifFileFormat : IImageFileFormat
{
    public string Name => "GIF";

    public IReadOnlyList<string> Extensions { get; } = [".gif"];

    public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
        header.Length >= 6
        && (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8));

    public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RejectOptions(options, "read");
        return GifCodec.Decode<TPixel>(stream);
    }

    public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RejectOptions(options, "write");
        GifCodec.Encode(image, stream);
    }

    private static void RejectOptions(IFileFormatOptions? options, string operation)
    {
        if (options is not null)
        {
            throw new ArgumentException($"GIF image {operation} options are not supported.", nameof(options));
        }
    }
}
