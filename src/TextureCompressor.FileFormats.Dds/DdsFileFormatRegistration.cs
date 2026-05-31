namespace TextureCompressor.FileFormats.Dds;

public static class DdsFileFormatRegistration
{
    public static IDisposable RegisterDdsFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new DdsFileFormat());
    }
}
