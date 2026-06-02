using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.BasisUniversal;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.BasisUniversal.Tests;

public sealed class BasisUniversalTextureCoderTests
{
    private static readonly BasisUniversalCoderOptions SFastOptions = new()
    {
        EffortLevel = 1,
        QualityLevel = 50
    };

    [Fact]
    public void RegisterBasisUniversalCodersOverridesManagerUntilDisposed()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterBasisUniversalCoders(SFastOptions))
        {
            Assert.IsType<BasisUniversalEtc1sTextureCoder>(manager.GetCoder(TextureFormats.RgbaBasisEtc1sUNorm));
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaBasisUastcLdr4x4UNorm));
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
        }

        Assert.IsNotType<BasisUniversalEtc1sTextureCoder>(manager.GetCoder(TextureFormats.RgbaBasisEtc1sUNorm));
        Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaBasisUastcLdr4x4UNorm));
        Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
    }

    [Fact]
    public void RegisterBasisUniversalCoderRegistersOnlySelectedFormat()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterBasisUniversalCoder(TextureFormats.Bc7UNorm, SFastOptions))
        {
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
            Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
        }

        Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
    }

    [Fact]
    public void RegisterBasisUniversalCoderRegistersBasisEtc1sFormat()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterBasisUniversalCoder(TextureFormats.RgbaBasisEtc1sUNorm, SFastOptions))
        {
            Assert.IsType<BasisUniversalEtc1sTextureCoder>(manager.GetCoder(TextureFormats.RgbaBasisEtc1sUNorm));
            Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
        }

        Assert.IsNotType<BasisUniversalEtc1sTextureCoder>(manager.GetCoder(TextureFormats.RgbaBasisEtc1sUNorm));
    }

    [Fact]
    public void RegisterBasisUniversalCodersRegistersSelectedFormats()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterBasisUniversalCoders([TextureFormats.Bc7UNorm, TextureFormats.RgbaAstc4x4UNorm], SFastOptions))
        {
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
            Assert.IsType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
            Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
        }

        Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
        Assert.IsNotType<BasisUniversalTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
    }

    [Theory]
    [MemberData(nameof(RepresentativeFormats))]
    public void EncodeThenDecodeProducesPixels(TextureFormat format)
    {
        var (width, height) = GetDimensions(format);
        var source = CreateSource(width, height);
        var coder = new BasisUniversalTextureCoder(format, SFastOptions);
        var payload = new byte[coder.GetEncodedByteCount(width, height, coder.GetDefaultPitch(width))];
        var decoded = new ArrayBitmap<Rgba8UNorm>(width, height);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(width));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(width));

        Assert.Equal(format.GetByteCount(width, height), payload.Length);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    [Fact]
    public void SupportsBasisUniversalTranscoderTargetsAndRawUastc()
    {
        Assert.Contains(TextureFormats.Bc7UNorm, BasisUniversalTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaBasisUastcLdr4x4UNorm, BasisUniversalTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaPvrtcII4BppUNorm, BasisUniversalTextureCoder.SupportedFormats.ToArray());
        Assert.DoesNotContain(TextureFormats.RgbaBasisEtc1sUNorm, BasisUniversalTextureCoder.SupportedFormats.ToArray());
        Assert.DoesNotContain(TextureFormats.Bc6HUFloat, BasisUniversalTextureCoder.SupportedFormats.ToArray());
        Assert.DoesNotContain(TextureFormats.RgbaPvrtcI2BppUNorm, BasisUniversalTextureCoder.SupportedFormats.ToArray());
    }

    [Fact]
    public void RegistrationSupportsBasisUniversalTranscoderAndRawBasisFormats()
    {
        Assert.Contains(TextureFormats.Bc7UNorm, BasisUniversalRegistration.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaBasisEtc1sUNorm, BasisUniversalRegistration.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaBasisEtc1sSrgb, BasisUniversalRegistration.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaBasisUastcLdr4x4UNorm, BasisUniversalRegistration.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaBasisUastcLdr4x4Srgb, BasisUniversalRegistration.SupportedFormats.ToArray());
    }

    [Fact]
    public void BasisUniversalEtc1sInterfaceEncodesAndDecodesWithExternalCodec()
    {
        IBasisEtc1sTextureCoder coder = new BasisUniversalEtc1sTextureCoder(TextureFormats.RgbaBasisEtc1sUNorm, SFastOptions);
        var source = CreateSource(4, 4);
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        var payload = coder.Encode(source.AsView());
        coder.Decode(payload.AsRawPayload(), decoded.AsView());

        Assert.False(payload.EndpointData.IsEmpty);
        Assert.False(payload.SelectorData.IsEmpty);
        Assert.False(payload.TablesData.IsEmpty);
        Assert.False(payload.RgbSliceData.IsEmpty);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    public static TheoryData<TextureFormat> RepresentativeFormats() => new()
    {
        TextureFormats.RgbaEtc2EacUNorm,
        TextureFormats.R11EacUNorm,
        TextureFormats.Bc1Rgba,
        TextureFormats.Bc5UNorm,
        TextureFormats.Bc7UNorm,
        TextureFormats.RgbaPvrtcI4BppUNorm,
        TextureFormats.RgbaPvrtcII4BppUNorm,
        TextureFormats.RgbFxt1UNorm,
        TextureFormats.AtcRgbaInterpolatedAlpha,
        TextureFormats.RgbaBasisUastcLdr4x4UNorm,
        TextureFormats.RgbaAstc4x4UNorm,
        TextureFormats.RgbaAstc8x8Srgb
    };

    private static ArrayBitmap<Rgba8UNorm> CreateSource(int width, int height)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = new Rgba8UNorm(
                    (byte)(16 + (x * 173 / Math.Max(1, width - 1))),
                    (byte)(24 + (y * 151 / Math.Max(1, height - 1))),
                    (byte)(48 + ((x * 17 + y * 29) % 160)),
                    (byte)(x == y ? 128 : 255));
            }
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static (int Width, int Height) GetDimensions(TextureFormat format)
    {
        if (format.SizeMode is TexturePayloadSizeMode.PvrtcI or TexturePayloadSizeMode.PvrtcII)
        {
            return (8, 8);
        }

        return (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight));
    }
}
