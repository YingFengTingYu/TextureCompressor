namespace TextureCompressor.FileFormats.Jpeg;

public static class JpegFileFormatRegistration
{
    public static IDisposable RegisterJpegFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new JpegFileFormat());
    }
}
