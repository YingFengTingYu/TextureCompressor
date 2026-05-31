namespace TextureCompressor.FileFormats.Ktx;

public static class KtxFileFormatRegistration
{
    public static IDisposable RegisterKtxFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new KtxFileFormat());
    }
}
