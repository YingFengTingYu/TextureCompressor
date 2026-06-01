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

        using (manager.RegisterAllPVRTexLibCoders(SFastOptions))
        {
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaPvrtcI4BppUNorm));
            Assert.IsType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
            Assert.IsType<PVRTexLib3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
        }

        Assert.IsNotType<PVRTexLibTextureCoder>(manager.GetCoder(TextureFormats.RgbaEtc2EacUNorm));
        Assert.IsNotType<PVRTexLib3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
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
    public void RegisterPVRTexLibCoderRegisters3DFormatAs3DCoder()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterPVRTexLibCoder3D(TextureFormats.RgbaAstc3x3x3UNorm, SFastOptions))
        {
            Assert.False(manager.TryGetCoder(TextureFormats.RgbaAstc3x3x3UNorm, out _));
            Assert.IsType<PVRTexLib3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
        }

        Assert.IsType<Astc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
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

    [Theory]
    [MemberData(nameof(Representative3DFormats))]
    public void EncodeThenDecodeVolumeProducesPixels(TextureFormat format)
    {
        var (width, height, depth) = GetDimensions3D(format);
        var source = CreateVolumeSource(width, height, depth);
        var coder = new PVRTexLib3DTextureCoder(format, SFastOptions);
        var payload = new byte[coder.GetEncodedByteCount(
            width,
            height,
            depth,
            coder.GetDefaultPitch(width),
            coder.GetDefaultSlicePitch(width, height))];
        var decoded = new ArrayVolumeBitmap<Rgba8UNorm>(width, height, depth);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(width), coder.GetDefaultSlicePitch(width, height));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(width), coder.GetDefaultSlicePitch(width, height));

        Assert.Equal(
            16 * GetBlockCount(width, format.BlockWidth) * GetBlockCount(height, format.BlockHeight) * GetBlockCount(depth, format.BlockDepth),
            payload.Length);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    [Fact]
    public void SupportsEveryMappedFormat()
    {
        Assert.DoesNotContain(TextureFormats.Bc7UNorm, PVRTexLibTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc5SNorm, PVRTexLibTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaAstc12x12Float, PVRTexLibTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaAstc3x3x3UNorm, PVRTexLib3DTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.RgbaAstc6x6x6Float, PVRTexLib3DTextureCoder.SupportedFormats.ToArray());
        Assert.False(PVRTexLibTextureCoder.IsSupported(TextureFormats.RgbaAstc3x3x3UNorm));
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

    public static TheoryData<TextureFormat> Representative3DFormats() => new()
    {
        TextureFormats.RgbaAstc3x3x3UNorm,
        TextureFormats.RgbaAstc4x4x4Srgb,
        TextureFormats.RgbaAstc6x6x6Float
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

    private static ArrayVolumeBitmap<Rgba8UNorm> CreateVolumeSource(int width, int height, int depth)
    {
        var pixels = new Rgba8UNorm[checked(width * height * depth)];
        for (var z = 0; z < depth; z++)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[(z * width * height) + (y * width) + x] = new Rgba8UNorm(
                        (byte)(16 + (x * 151 / Math.Max(1, width - 1))),
                        (byte)(24 + (y * 137 / Math.Max(1, height - 1))),
                        (byte)(48 + (z * 113 / Math.Max(1, depth - 1))),
                        (byte)((x + y + z) % 2 == 0 ? 160 : 255));
                }
            }
        }

        return new ArrayVolumeBitmap<Rgba8UNorm>(width, height, depth, pixels);
    }

    private static (int Width, int Height) GetDimensions(TextureFormat format)
    {
        if (format.SizeMode is TexturePayloadSizeMode.PvrtcI or TexturePayloadSizeMode.PvrtcII)
        {
            return (8, 8);
        }

        return (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight));
    }

    private static (int Width, int Height, int Depth) GetDimensions3D(TextureFormat format) =>
        (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight), Math.Max(4, format.BlockDepth));

    private static int GetBlockCount(int value, int blockSize) =>
        checked((value + blockSize - 1) / blockSize);
}
