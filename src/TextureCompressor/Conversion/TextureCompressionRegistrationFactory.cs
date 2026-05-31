using TextureCompressor.Codecs;
using TextureCompressor.Formats;
using TextureCompressor.Options;
using TextureCompressor.Registry;

namespace TextureCompressor.Conversion;

internal static class TextureCompressionRegistrationFactory
{
    public static IDisposable? Create(
        TextureCoderManager coders,
        TextureFormat format,
        TextureCompressionLevel? compressionLevel)
    {
        ArgumentNullException.ThrowIfNull(coders);

        if (compressionLevel is null)
        {
            return null;
        }

        var options = new TextureCompressionOptions { CompressionMode = compressionLevel.Value };
        if (S3tcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new S3tcTextureCoder(format, options));
        }

        if (FxtcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new FxtcTextureCoder(format, options));
        }

        if (EtcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new EtcTextureCoder(format, options));
        }

        if (AtcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new AtcTextureCoder(format, options));
        }

        if (RgtcLatcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new RgtcLatcTextureCoder(format, options));
        }

        if (BptcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new BptcTextureCoder(format, options));
        }

        if (PvrtcTextureCoder.IsSupported(format))
        {
            return coders.Register(format, new PvrtcTextureCoder(format, options));
        }

        return AstcTextureCoder.IsSupported(format)
            ? coders.Register(format, new AstcTextureCoder(format, options))
            : null;
    }
}
