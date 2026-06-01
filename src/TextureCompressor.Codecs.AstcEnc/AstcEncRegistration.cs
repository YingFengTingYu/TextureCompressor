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

    public static IDisposable RegisterAstcEncCoder3D(
        this TextureCoderManager manager,
        TextureFormat format,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register3D(format, new AstcEnc3DTextureCoder(format, options));
    }

    public static IDisposable RegisterAstcEncCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new AstcEncTextureCoder(format, options));
    }

    public static IDisposable RegisterAstcEncCoders3D(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register3D(formats, format => new AstcEnc3DTextureCoder(format, options));
    }

    public static IDisposable RegisterAstcEncCoders3D(
        this TextureCoderManager manager,
        AstcEncCoderOptions? options = null)
    {
        return manager.RegisterAstcEncCoders3D(AstcEnc3DTextureCoder.SupportedFormats.ToArray(), options);
    }

    public static IDisposable RegisterAstcEncCoders(
        this TextureCoderManager manager,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Combine(
        [
            manager.RegisterAstcEncCoders(AstcEncTextureCoder.SupportedFormats.ToArray(), options),
            manager.RegisterAstcEncCoders3D(AstcEnc3DTextureCoder.SupportedFormats.ToArray(), options)
        ]);
    }
}
