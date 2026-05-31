using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.BasisUniversal;

public static class BasisUniversalRegistration
{
    public static IDisposable RegisterBasisUniversalCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        BasisUniversalCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new BasisUniversalTextureCoder(format, options));
    }

    public static IDisposable RegisterBasisUniversalCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        BasisUniversalCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new BasisUniversalTextureCoder(format, options));
    }

    public static IDisposable RegisterBasisUniversalCoders(
        this TextureCoderManager manager,
        BasisUniversalCoderOptions? options = null)
    {
        return manager.RegisterBasisUniversalCoders(BasisUniversalTextureCoder.SupportedFormats.ToArray(), options);
    }
}
