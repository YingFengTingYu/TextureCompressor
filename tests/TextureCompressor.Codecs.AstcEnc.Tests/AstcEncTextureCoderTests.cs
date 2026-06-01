using AstcEncoder;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs.AstcEnc;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.AstcEnc.Tests;

public sealed class AstcEncTextureCoderTests
{
    private static readonly AstcEncCoderOptions SFastOptions = new()
    {
        Quality = Astcenc.AstcencPreFastest
    };

    [Fact]
    public void RegisterAstcEncCodersOverridesManagerUntilDisposed()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterAstcEncCoders(SFastOptions))
        {
            Assert.IsType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
            Assert.IsType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc8x8Srgb));
            Assert.IsType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc12x12Float));
            Assert.IsType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
        }

        Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
        Assert.IsNotType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
    }

    [Fact]
    public void RegisterAstcEncCoderRegistersOnlySelectedFormat()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterAstcEncCoder(TextureFormats.RgbaAstc4x4UNorm, SFastOptions))
        {
            Assert.IsType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
            Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc8x8Srgb));
        }

        Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
    }

    [Fact]
    public void RegisterAstcEncCoderRegisters3DFormatAs3DCoder()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterAstcEncCoder3D(TextureFormats.RgbaAstc3x3x3UNorm, SFastOptions))
        {
            Assert.False(manager.TryGetCoder(TextureFormats.RgbaAstc3x3x3UNorm, out _));
            Assert.IsType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
        }

        Assert.IsType<Astc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
    }

    [Fact]
    public void RegisterAstcEncCodersRegistersSelectedFormats()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterAstcEncCoders([TextureFormats.RgbaAstc4x4UNorm, TextureFormats.RgbaAstc8x8Srgb], SFastOptions))
        {
            Assert.IsType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
            Assert.IsType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc8x8Srgb));
            Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc12x12Float));
        }

        Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
        Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc8x8Srgb));
    }

    [Fact]
    public void RegisterAstcEncCoders3DRegistersSelectedFormats()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterAstcEncCoders3D([TextureFormats.RgbaAstc3x3x3UNorm, TextureFormats.RgbaAstc4x4x4Srgb], SFastOptions))
        {
            Assert.IsType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
            Assert.IsType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc4x4x4Srgb));
            Assert.IsNotType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc6x6x6Float));
        }

        Assert.IsNotType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc3x3x3UNorm));
        Assert.IsNotType<AstcEnc3DTextureCoder>(manager.GetCoder3D(TextureFormats.RgbaAstc4x4x4Srgb));
    }

    [Theory]
    [MemberData(nameof(RepresentativeFormats))]
    public void EncodeThenDecodeProducesPixels(TextureFormat format)
    {
        var (width, height) = GetDimensions(format);
        var source = CreateSource(width, height);
        var coder = new AstcEncTextureCoder(format, SFastOptions);
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
        var coder = new AstcEnc3DTextureCoder(format, SFastOptions);
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
    public void SupportsEveryBuiltInAstcFormat()
    {
        Assert.True(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc4x4UNorm));
        Assert.True(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc8x8Srgb));
        Assert.True(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc12x12Float));
        Assert.False(AstcEncTextureCoder.IsSupported(TextureFormats.Bc7UNorm));
        Assert.True(AstcEnc3DTextureCoder.IsSupported(TextureFormats.RgbaAstc3x3x3UNorm));
        Assert.True(AstcEnc3DTextureCoder.IsSupported(TextureFormats.RgbaAstc6x6x6Float));
        Assert.False(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc3x3x3UNorm));
        Assert.False(AstcEnc3DTextureCoder.IsSupported(TextureFormats.RgbaAstc4x4UNorm));
    }

    public static TheoryData<TextureFormat> RepresentativeFormats() => new()
    {
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

    private static (int Width, int Height) GetDimensions(TextureFormat format) =>
        (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight));

    private static (int Width, int Height, int Depth) GetDimensions3D(TextureFormat format) =>
        (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight), Math.Max(4, format.BlockDepth));

    private static int GetBlockCount(int value, int blockSize) =>
        checked((value + blockSize - 1) / blockSize);
}
