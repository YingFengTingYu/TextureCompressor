namespace TextureCompressor.FileFormats.Hdr;

public static class HdrFileFormatRegistration
{
    public static IDisposable RegisterHdrFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new HdrFileFormat());
    }
}
