using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.DirectXTex;

public static class DirectXTexRegistration
{
    public static IDisposable RegisterDirectXTexCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        DirectXTexCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new DirectXTexTextureCoder(format, options));
    }

    public static IDisposable RegisterDirectXTexCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        DirectXTexCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new DirectXTexTextureCoder(format, options));
    }

    public static IDisposable RegisterDirectXTexCoders(
        this TextureCoderManager manager,
        DirectXTexCoderOptions? options = null)
    {
        return manager.RegisterDirectXTexCoders(DirectXTexTextureCoder.SupportedFormats.ToArray(), options);
    }
}
