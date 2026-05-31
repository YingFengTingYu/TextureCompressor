namespace TextureCompressor.FileFormats.Gif;

public static class GifFileFormatRegistration
{
    public static IDisposable RegisterGifFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new GifFileFormat());
    }
}
