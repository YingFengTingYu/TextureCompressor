using TextureCompressor.Codecs;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.BasisUniversal;

public static class BasisUniversalRegistration
{
    private static readonly TextureFormat[] SSupportedFormats =
    [
        .. BasisUniversalTextureCoder.SupportedFormats.ToArray(),
        .. BasisUniversalEtc1sTextureCoder.SupportedFormats.ToArray()
    ];

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static IDisposable RegisterBasisUniversalCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        BasisUniversalCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, CreateCoder(format, options));
    }

    public static IDisposable RegisterBasisUniversalCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        BasisUniversalCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(formats, format => CreateCoder(format, options));
    }

    public static IDisposable RegisterBasisUniversalCoders(
        this TextureCoderManager manager,
        BasisUniversalCoderOptions? options = null)
    {
        return manager.RegisterBasisUniversalCoders(SupportedFormats.ToArray(), options);
    }

    private static ITextureCoder CreateCoder(TextureFormat format, BasisUniversalCoderOptions? options)
    {
        if (BasisUniversalEtc1sTextureCoder.IsSupported(format))
        {
            return new BasisUniversalEtc1sTextureCoder(format, options);
        }

        return new BasisUniversalTextureCoder(format, options);
    }
}
