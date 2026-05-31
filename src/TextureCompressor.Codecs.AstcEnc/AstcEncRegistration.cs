using TextureCompressor.Codecs;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.AstcEnc;

public static class AstcEncRegistration
{
    public static IDisposable RegisterAstcEncCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new AstcEncTextureCoder(format, options));
    }

    public static IDisposable RegisterAstcEncCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new AstcEncTextureCoder(format, options));
    }

    public static IDisposable RegisterAstcEncCoders(
        this TextureCoderManager manager,
        AstcEncCoderOptions? options = null)
    {
        return manager.RegisterAstcEncCoders(AstcEncTextureCoder.SupportedFormats.ToArray(), options);
    }
}
