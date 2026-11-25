using PVRTexLib;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.PVRTexLib.Tests;

public sealed class Etc2PremultipliedAlphaTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PremultipliedEtc2PreservesItsIndependentRgbAndAlphaPlanes(bool srgb, bool dither)
    {
        var format = srgb ? TextureFormats.RgbaEtc2EacSrgb : TextureFormats.RgbaEtc2EacUNorm;
        var rgbFormat = srgb ? TextureFormats.RgbEtc2Srgb : TextureFormats.RgbEtc2UNorm;
        var options = new PVRTexLibCompressorOptions { EtcQuality = PVRTexLibCompressorQuality.ETCSlow, MaxThreads = 1, Dither = dither };
        var separate = new PVRTexLibTextureCoder(format, new()
        {
            Etc2InputIsPremultiplied = true, EtcQuality = options.EtcQuality, MaxThreads = 1, Dither = dither
        });
        var image = new ArrayBitmap<Rgba8UNorm>(7, 5);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            byte alpha = (byte)((x % 4) * 85);
            image.Pixels[y * image.Width + x] = RgbaColorConversions.PremultiplyAlpha(new(224, 96, 32, alpha));
        }
        var original = image.Pixels.ToArray();
        var opaque = new ArrayBitmap<Rgba8UNorm>(7, 5, Array.ConvertAll(original, c => new Rgba8UNorm(c.Red, c.Green, c.Blue, 255)));
        byte[] colors = Encode(new PVRTexLibTextureCoder(rgbFormat, options), opaque);
        byte[] rgba = Encode(new PVRTexLibTextureCoder(format, options), image);
        int rowBytes = separate.GetDefaultPitch(image.Width), pitch = rowBytes + 7;
        var bytes = Enumerable.Repeat((byte)0xcd, pitch * 2).ToArray();
        separate.Encode(image.AsView(), bytes, pitch);
        Assert.Equal(original, image.Pixels);
        for (int row = 0; row < 2; row++)
        {
            for (int x = 0; x < rowBytes; x += 16)
            {
                int offset = row * rowBytes + x;
                Assert.Equal(rgba.AsSpan(offset, 8).ToArray(), bytes.AsSpan(row * pitch + x, 8).ToArray());
                Assert.Equal(colors.AsSpan(offset / 2, 8).ToArray(), bytes.AsSpan(row * pitch + x + 8, 8).ToArray());
            }
            Assert.All(bytes[(row * pitch + rowBytes)..((row + 1) * pitch)], value => Assert.Equal(0xcd, value));
        }

        var decoded = new ArrayBitmap<Rgba8UNorm>(7, 5);
        new EtcTextureCoder(format).Decode(bytes, decoded.AsView(), pitch);
        var native = new ArrayBitmap<Rgba8UNorm>(7, 5);
        separate.Decode(bytes, native.AsView(), pitch);
        for (int i = 0; i < native.Pixels.Length; i++)
        {
            var a = decoded.Pixels[i];
            var b = native.Pixels[i];
            Assert.InRange(Math.Abs(a.Red - b.Red), 0, srgb ? 1 : 0);
            Assert.InRange(Math.Abs(a.Green - b.Green), 0, srgb ? 1 : 0);
            Assert.InRange(Math.Abs(a.Blue - b.Blue), 0, srgb ? 1 : 0);
            Assert.Equal(a.Alpha, b.Alpha);
        }
    }

    [Fact]
    public void PremultipliedVolumeUsesTheSamePlanesAndPreservesSlicePadding()
    {
        var options = new PVRTexLibCompressorOptions { Etc2InputIsPremultiplied = true, MaxThreads = 1 };
        var format = TextureFormats.RgbaEtc2EacUNorm;
        var coder = new PVRTexLib3DTextureCoder(format, options);
        var flat = new PVRTexLibTextureCoder(format, options);
        var image = new VolumeBitmapView<Rgba8UNorm>(new Rgba8UNorm[5 * 3 * 2], 5, 3, 2);
        for (int i = 0; i < image.Pixels.Length; i++)
            image.Pixels[i] = RgbaColorConversions.PremultiplyAlpha(new(224, 96, 32, (byte)(i * 7)));
        int rowPitch = flat.GetDefaultPitch(5) + 3, slicePitch = rowPitch + 11;
        var encoded = Enumerable.Repeat((byte)0xcd, 2 * slicePitch).ToArray();
        coder.Encode(image, encoded, rowPitch, slicePitch);
        for (int z = 0; z < 2; z++)
        {
            var expected = Enumerable.Repeat((byte)0xcd, slicePitch).ToArray();
            flat.Encode(image.GetSliceView(z), expected, rowPitch);
            Assert.Equal(expected, encoded[(z * slicePitch)..((z + 1) * slicePitch)]);
        }
    }

    private static byte[] Encode(ITextureCoder coder, ArrayBitmap<Rgba8UNorm> image)
    {
        var bytes = new byte[coder.GetEncodedByteCount(image.Width, image.Height)];
        coder.Encode(image.AsView(), bytes);
        return bytes;
    }
}
