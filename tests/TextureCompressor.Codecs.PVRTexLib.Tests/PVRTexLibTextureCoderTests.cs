using PVRTexLib;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Codecs.PVRTexLib;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.PVRTexLib.Tests;

public sealed class PVRTexLibTextureCoderTests
{
    private static readonly PVRTexLibCompressorOptions SFastOptions = new()
    {
        EtcQuality = PVRTexLibCompressorQuality.ETCFast,
        PvrtcQuality = PVRTexLibCompressorQuality.PVRTCFastest,
        AstcQuality = PVRTexLibCompressorQuality.ASTCVeryFast
    };

    [Fact]
    public void RegisterPVRTexLibCodersOverridesManagerUntilDisposed()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterPVRTexLibCoders(SFastOptions))
        {
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaPvrtcI4BppUNorm));
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
        }

        Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
    }

    [Fact]
    public void RegisterPVRTexLibCoderRegistersOnlySelectedFormat()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterPVRTexLibCoder(TextureFormats.RgbaEtc2EacUNorm, SFastOptions))
        {
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
            Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaPvrtcI4BppUNorm));
        }

        Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
    }

    [Fact]
    public void RegisterPVRTexLibCodersRegistersSelectedFormats()
    {
        var manager = new TextureCoderManager();
        var formats = new[]
        {
            TextureFormats.RgbaEtc2EacUNorm,
            TextureFormats.RgbaAstc4x4UNorm
        };

        using (manager.RegisterPVRTexLibCoders(formats, SFastOptions))
        {
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
            Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaPvrtcI4BppUNorm));
        }

        Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
        Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
    }

    [Theory]
    [MemberData(nameof(RepresentativeFormats))]
    public void EncodeThenDecodeProducesPixels(TextureFormat format)
    {
        var (width, height) = GetDimensions(format);
        var source = CreateSource(width, height);
        var coder = new PVRTexLibTextureCoder(format, SFastOptions);
        var payload = new byte[coder.GetEncodedByteCount(width, height, coder.GetDefaultPitch(width))];
        var decoded = new ArrayBitmap<Rgba8UNorm>(width, height);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(width));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(width));

        Assert.Equal(format.GetByteCount(width, height), payload.Length);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    [Fact]
    public void SupportsEveryMappedFormat()
    {
        Assert.DoesNotContain(TextureFormats.Bc7UNorm, PVRTexLibTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc5SNorm, PVRTexLibTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaAstc12x12Float, PVRTexLibTextureCoder.SupportedFormats.ToArray());
    }

    public static TheoryData<TextureFormat> RepresentativeFormats() => new()
    {
        TextureFormats.RgbEtc1UNorm,
        TextureFormats.RgbaEtc2EacUNorm,
        TextureFormats.Rg11EacSNorm,
        TextureFormats.RgbaPvrtcI4BppUNorm,
        TextureFormats.RgbPvrtcII8BppFloat,
        TextureFormats.Bc1Rgba,
        TextureFormats.Dxt5Rgba,
        TextureFormats.Bc5SNorm,
        TextureFormats.RgbaAstc4x4UNorm,
        TextureFormats.RgbaAstc8x8Srgb,
        TextureFormats.RgbaAstc12x12Float
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
