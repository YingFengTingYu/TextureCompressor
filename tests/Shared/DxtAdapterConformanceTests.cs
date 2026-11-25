using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public abstract class DxtAdapterConformanceTests
{
    protected abstract ITextureCoder CreateDxtCoder(TextureFormat format);

    protected void CheckSignedBc5AndFractionalBc4()
    {
        var image = new ArrayBitmap<Rgba32Float>(4, 4);
        var bc4 = CreateDxtCoder(TextureFormats.Bc4UNorm);
        bc4.Decode(Convert.FromHexString("0500020000000000"), image.AsView());
        var p = image.Pixels[0];
        Assert.InRange(Math.Abs(p.Red - 30f / (7 * 255)), 0, 0.000001f);
        Assert.Equal((0f, 0f, 1f), (p.Green, p.Blue, p.Alpha));

        var bc5 = CreateDxtCoder(TextureFormats.Bc5SNorm);
        bc5.Decode(Convert.FromHexString("01FB02000000000017F8070000000000"), image.AsView());
        p = image.Pixels[0];
        Assert.InRange(Math.Abs(p.Red - 1f / (7 * 127)), 0, 0.000001f);
        Assert.InRange(Math.Abs(p.Green + 25f / (7 * 127)), 0, 0.000001f);
        for (int i = 0; i < 16; i++) image.Pixels[i] = new Rgba32Float(-.8f + .1f * i, .6f - i / 12f, 0);
        var block = new byte[16];
        bc5.Encode(image.AsView(), block);
        ((ITextureCoder)new RgtcLatcTextureCoder(TextureFormats.Bc5SNorm)).Decode(block, image.AsView());
        for (int i = 0; i < 16; i++)
        {
            Assert.InRange(Math.Abs(image.Pixels[i].Red - (-.8f + .1f * i)), 0, .14f);
            Assert.InRange(Math.Abs(image.Pixels[i].Green - (.6f - i / 12f)), 0, .14f);
        }
    }

    [Fact]
    public void Dxt1BlackSelectorUsesTheRequestedAlphaConvention()
    {
        byte[] block = Convert.FromHexString("0000FFFFFFFFFFFF");
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        foreach (var (format, alpha) in new[] { (TextureFormats.Dxt1Rgb, 255), (TextureFormats.Dxt1Rgba, 0) })
        {
            CreateDxtCoder(format).Decode(block, bitmap.AsView());
            Assert.All(bitmap.Pixels, p => Assert.Equal(new Rgba8UNorm(0, 0, 0, (byte)alpha), p));
        }
    }

    [Fact]
    public void Dxt1RgbIgnoresAlphaDuringEncoding()
    {
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4, Enumerable.Repeat(new Rgba8UNorm(255, 0, 0, 0), 16).ToArray());
        var format = TextureFormats.Dxt1Rgb;
        var block = new byte[8];
        CreateDxtCoder(format).Encode(bitmap.AsView(), block);
        ((ITextureCoder)new S3tcTextureCoder(format)).Decode(block, bitmap.AsView());
        Assert.All(bitmap.Pixels, p => Assert.InRange(p.Red, 245, 255));
    }

    [Fact]
    public void Dxt1RgbaEncodesTheHalfAlphaCutoff()
    {
        byte[] alphas = [0, 127, 128, 255];
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        for (int i = 0; i < bitmap.Pixels.Length; i++)
        {
            bitmap.Pixels[i] = new Rgba8UNorm(200, 120, 40, alphas[i % 4]);
        }
        var format = TextureFormats.Dxt1Rgba;
        var block = new byte[8];
        CreateDxtCoder(format).Encode(bitmap.AsView(), block);
        ((ITextureCoder)new S3tcTextureCoder(format)).Decode(block, bitmap.AsView());
        for (int i = 0; i < bitmap.Pixels.Length; i++)
        {
            Assert.Equal(alphas[i % 4] < 128 ? 0 : 255, bitmap.Pixels[i].Alpha);
        }
    }

    [Theory]
    [InlineData(false, "88888888888888880080000000000000")]
    [InlineData(true, "88000000000000000080000000000000")]
    public void Dxt2And4ConvertStraightAlphaOnBothBoundaries(bool interpolated, string hex)
    {
        var format = interpolated ? TextureFormats.Dxt4Rgba : TextureFormats.Dxt2Rgba;
        var coder = CreateDxtCoder(format);
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        coder.Decode(Convert.FromHexString(hex), bitmap.AsView());
        Assert.All(bitmap.Pixels, p =>
        {
            Assert.InRange(p.Red, 245, 249);
            Assert.Equal(136, p.Alpha);
        });

        var block = new byte[16];
        coder.Encode(bitmap.AsView(), block);
        var raw = interpolated ? TextureFormats.Dxt5Rgba : TextureFormats.Dxt3Rgba;
        ((ITextureCoder)new S3tcTextureCoder(raw)).Decode(block, bitmap.AsView());
        Assert.All(bitmap.Pixels, p =>
        {
            Assert.InRange(p.Red, 126, 138);
            Assert.Equal(136, p.Alpha);
        });
    }

    [Fact]
    public void DxtSrgbConvertsRgbOnceAndLeavesAlphaLinear()
    {
        var format = TextureFormats.Dxt3RgbaSrgb;
        var coder = CreateDxtCoder(format);
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        coder.Decode(Convert.FromHexString("88888888888888880080000000000000"), bitmap.AsView());
        Assert.All(bitmap.Pixels, p =>
        {
            Assert.InRange(p.Red, 57, 60);
            Assert.Equal(136, p.Alpha);
        });

        Array.Fill(bitmap.Pixels, new Rgba8UNorm(128, 128, 128, 136));
        var block = new byte[16];
        coder.Encode(bitmap.AsView(), block);
        ((ITextureCoder)new S3tcTextureCoder(format)).Decode(block, bitmap.AsView());
        Assert.All(bitmap.Pixels, p =>
        {
            Assert.InRange(p.Red, 121, 135);
            Assert.Equal(136, p.Alpha);
        });
    }
}
