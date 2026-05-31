namespace TextureCompressor.FileFormats.Pvr;

public static class PvrFileFormatRegistration
{
    public static IDisposable RegisterPvrFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new PvrFileFormat());
    }
}
