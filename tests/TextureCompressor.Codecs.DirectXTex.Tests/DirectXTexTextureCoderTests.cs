using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs.DirectXTex;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.DirectXTex.Tests;

public sealed class DirectXTexTextureCoderTests
{
    [Fact]
    public void RegisterDirectXTexCodersOverridesManagerUntilDisposed()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterDirectXTexCoders())
        {
            Assert.IsType<DirectXTexTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
            Assert.IsType<DirectXTexTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
        }

        Assert.IsNotType<DirectXTexTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
    }

    [Fact]
    public void RegisterDirectXTexCoderRegistersOnlySelectedFormat()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterDirectXTexCoder(TextureFormats.Bc7UNorm))
        {
            Assert.IsType<DirectXTexTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
            Assert.IsNotType<DirectXTexTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
        }

        Assert.IsNotType<DirectXTexTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
    }

    [Theory]
    [MemberData(nameof(RepresentativeFormats))]
    public void EncodeThenDecodeProducesPixels(TextureFormat format)
    {
        var (width, height) = GetDimensions(format);
        var source = CreateSource(width, height, format);
        var coder = new DirectXTexTextureCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(width, height, coder.GetDefaultPitch(width))];
        var decoded = new ArrayBitmap<Rgba32Float>(width, height);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(width));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(width));

        Assert.Equal(format.GetByteCount(width, height), payload.Length);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    [Fact]
    public void SupportsExpectedDirectXBlockFormats()
    {
        Assert.Contains(TextureFormats.Bc1Rgba, DirectXTexTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc5SNorm, DirectXTexTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc6HUFloat, DirectXTexTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc7Srgb, DirectXTexTextureCoder.SupportedFormats.ToArray());
        Assert.DoesNotContain(TextureFormats.RgbaAstc4x4UNorm, DirectXTexTextureCoder.SupportedFormats.ToArray());
    }

    public static TheoryData<TextureFormat> RepresentativeFormats() => new()
    {
        TextureFormats.Bc1Rgba,
        TextureFormats.Bc1RgbaSrgb,
        TextureFormats.Bc2Rgba,
        TextureFormats.Bc3Rgba,
        TextureFormats.Bc4UNorm,
        TextureFormats.Bc5SNorm,
        TextureFormats.Bc6HUFloat,
        TextureFormats.Bc7UNorm
    };

    private static ArrayBitmap<Rgba32Float> CreateSource(int width, int height, TextureFormat format)
    {
        var pixels = new Rgba32Float[checked(width * height)];
        var isSigned = format.ValueKind == TextureValueKind.SNorm || format == TextureFormats.Bc6HSFloat || format == TextureFormats.RgbBptcSFloat;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = 0.1f + (x * 0.8f / Math.Max(1, width - 1));
                var green = 0.15f + (y * 0.7f / Math.Max(1, height - 1));
                var blue = 0.2f + (((x * 17) + (y * 29)) % 64 / 80f);
                if (isSigned)
                {
                    red = (red * 2f) - 1f;
                    green = (green * 2f) - 1f;
                    blue = (blue * 2f) - 1f;
                }

                pixels[(y * width) + x] = new Rgba32Float(red, green, blue, x == y ? 0.5f : 1f);
            }
        }

        return new ArrayBitmap<Rgba32Float>(width, height, pixels);
    }

    private static (int Width, int Height) GetDimensions(TextureFormat format) =>
        (Math.Max(4, format.BlockWidth), Math.Max(4, format.BlockHeight));
}
