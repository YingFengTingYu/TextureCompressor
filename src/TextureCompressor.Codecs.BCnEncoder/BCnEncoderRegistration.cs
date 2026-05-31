using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.BCnEncoder;

public static class BCnEncoderRegistration
{
    public static IDisposable RegisterBCnEncoderCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        BCnEncoderCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new BCnEncoderTextureCoder(format, options));
    }

    public static IDisposable RegisterBCnEncoderCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        BCnEncoderCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => new BCnEncoderTextureCoder(format, options));
    }

    public static IDisposable RegisterBCnEncoderCoders(
        this TextureCoderManager manager,
        BCnEncoderCoderOptions? options = null)
    {
        return manager.RegisterBCnEncoderCoders(BCnEncoderTextureCoder.SupportedFormats.ToArray(), options);
    }
}
