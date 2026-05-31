namespace TextureCompressor.FileFormats.Astc;

public static class AstcFileFormatRegistration
{
    public static IDisposable RegisterAstcFileFormat(this TextureFileFormatManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(new AstcFileFormat());
    }
}
