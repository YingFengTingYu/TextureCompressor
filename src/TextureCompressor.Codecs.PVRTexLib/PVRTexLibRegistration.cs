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

    public static IDisposable RegisterPVRTexLibCoder3D(
        this TextureCoderManager manager,
        TextureFormat format,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register3D(format, new PVRTexLib3DTextureCoder(format, options));
    }

    public static IDisposable RegisterPVRTexLibCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new PVRTexLibTextureCoder(format, options));
    }

    public static IDisposable RegisterPVRTexLibCoders3D(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register3D(formats, format => new PVRTexLib3DTextureCoder(format, options));
    }

    public static IDisposable RegisterAllPVRTexLibCoders(
        this TextureCoderManager manager,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Combine(
        [
            manager.RegisterPVRTexLibCoders(PVRTexLibTextureCoder.SupportedFormats.ToArray(), options),
            manager.RegisterPVRTexLibCoders3D(PVRTexLib3DTextureCoder.SupportedFormats.ToArray(), options)
        ]);
    }
}
