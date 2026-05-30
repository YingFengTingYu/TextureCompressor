using BCnEncoder.Encoder;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs.BCnEncoder;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.BCnEncoder.Tests;

public sealed class BCnEncoderTextureCoderTests
{
    private static readonly BCnEncoderCoderOptions SFastOptions = new()
    {
        Quality = CompressionQuality.Fast
    };

    [Fact]
    public void RegisterBCnEncoderCodersOverridesManagerUntilDisposed()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterBCnEncoderCoders(SFastOptions))
        {
            Assert.IsType<BCnEncoderTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
            Assert.IsType<BCnEncoderTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
        }

        Assert.IsNotType<BCnEncoderTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
    }

    [Fact]
    public void RegisterBCnEncoderCoderRegistersOnlySelectedFormat()
    {
        var manager = new TextureCoderManager();

        using (manager.RegisterBCnEncoderCoder(TextureFormats.Bc7UNorm, SFastOptions))
        {
            Assert.IsType<BCnEncoderTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
            Assert.IsNotType<BCnEncoderTextureCoder>(manager.GetCoder(TextureFormats.Bc1Rgba));
        }

        Assert.IsNotType<BCnEncoderTextureCoder>(manager.GetCoder(TextureFormats.Bc7UNorm));
    }

    [Theory]
    [MemberData(nameof(RepresentativeFormats))]
    public void EncodeThenDecodeProducesPixels(TextureFormat format)
    {
        var (width, height) = GetDimensions(format);
        var source = CreateSource(width, height, format);
        var coder = new BCnEncoderTextureCoder(format, SFastOptions);
        var payload = new byte[coder.GetEncodedByteCount(width, height, coder.GetDefaultPitch(width))];
        var decoded = new ArrayBitmap<Rgba32Float>(width, height);

        coder.Encode(source.AsView(), payload, coder.GetDefaultPitch(width));
        coder.Decode(payload, decoded.AsView(), coder.GetDefaultPitch(width));

        Assert.Equal(format.GetByteCount(width, height), payload.Length);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    [Fact]
    public void EncodeThenDecodeHonorsBlockRowPitch()
    {
        var format = TextureFormats.Bc7UNorm;
        var source = CreateSource(width: 7, height: 5, format);
        var coder = new BCnEncoderTextureCoder(format, SFastOptions);
        var rowPitch = coder.GetDefaultPitch(source.Width) + 16;
        var payload = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);

        coder.Encode(source.AsView(), payload, rowPitch);
        coder.Decode(payload, decoded.AsView(), rowPitch);

        Assert.Contains(decoded.Pixels, pixel => pixel.Red != 0 || pixel.Green != 0 || pixel.Blue != 0);
    }

    [Fact]
    public void SupportsExpectedBCnEncoderFormats()
    {
        Assert.Contains(TextureFormats.Bc1Rgba, BCnEncoderTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc5UNorm, BCnEncoderTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc6HSFloat, BCnEncoderTextureCoder.SupportedFormats.ToArray());
        Assert.Contains(TextureFormats.Bc7Srgb, BCnEncoderTextureCoder.SupportedFormats.ToArray());
        Assert.DoesNotContain(TextureFormats.Bc5SNorm, BCnEncoderTextureCoder.SupportedFormats.ToArray());
        Assert.DoesNotContain(TextureFormats.RgbaAstc4x4UNorm, BCnEncoderTextureCoder.SupportedFormats.ToArray());
    }

    public static TheoryData<TextureFormat> RepresentativeFormats() => new()
    {
        TextureFormats.Bc1Rgba,
        TextureFormats.Bc1RgbaSrgb,
        TextureFormats.Bc2Rgba,
        TextureFormats.Bc3Rgba,
        TextureFormats.Bc4UNorm,
        TextureFormats.Bc5UNorm,
        TextureFormats.Bc6HUFloat,
        TextureFormats.Bc6HSFloat,
        TextureFormats.Bc7UNorm
    };

    private static ArrayBitmap<Rgba32Float> CreateSource(int width, int height, TextureFormat format)
    {
        var pixels = new Rgba32Float[checked(width * height)];
        var isSigned = format == TextureFormats.Bc6HSFloat || format == TextureFormats.RgbBptcSFloat;
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
