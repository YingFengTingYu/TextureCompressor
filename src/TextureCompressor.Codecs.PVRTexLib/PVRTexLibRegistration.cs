using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.PVRTexLib;

public static class PVRTexLibRegistration
{
    public static IDisposable RegisterPVRTexLibCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new PVRTexLibTextureCoder(format, options));
    }

    public static IDisposable RegisterPVRTexLibCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new PVRTexLibTextureCoder(format, options));
    }

    public static IDisposable RegisterPVRTexLibCoders(
        this TextureCoderManager manager,
        PVRTexLibCompressorOptions? options = null)
    {
        return manager.RegisterPVRTexLibCoders(PVRTexLibTextureCoder.SupportedFormats.ToArray(), options);
    }
}
