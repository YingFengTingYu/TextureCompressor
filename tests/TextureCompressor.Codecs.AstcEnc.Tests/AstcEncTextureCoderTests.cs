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
        }

        Assert.IsNotType<AstcEncTextureCoder>(manager.GetCoder(TextureFormats.RgbaAstc4x4UNorm));
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

    [Fact]
    public void SupportsEveryBuiltInAstcFormat()
    {
        Assert.True(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc4x4UNorm));
        Assert.True(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc8x8Srgb));
        Assert.True(AstcEncTextureCoder.IsSupported(TextureFormats.RgbaAstc12x12Float));
        Assert.False(AstcEncTextureCoder.IsSupported(TextureFormats.Bc7UNorm));
    }

    public static TheoryData<TextureFormat> RepresentativeFormats() => new()
    {
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

    private static (int Width, int Height) GetDimensions(TextureFormat format) =>
        (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight));
}
