namespace TextureCompressor.FileFormats.Png;

public static class PngFileFormatRegistration
{
    public static IDisposable RegisterPngFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new PngFileFormat());
    }
}
