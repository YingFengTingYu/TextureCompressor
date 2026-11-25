using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class SrgbBlockPrecisionTests
{
    public static TheoryData<TextureFormat, TextureFormat> Formats => new()
    {
        { TextureFormats.RgbEtc2UNorm, TextureFormats.RgbEtc2Srgb },
        { TextureFormats.RgbA1Etc2UNorm, TextureFormats.RgbA1Etc2Srgb },
        { TextureFormats.RgbaEtc2EacUNorm, TextureFormats.RgbaEtc2EacSrgb },
        { TextureFormats.RgbaAstc4x4UNorm, TextureFormats.RgbaAstc4x4Srgb },
        { TextureFormats.RgbaPvrtcI4BppUNorm, TextureFormats.RgbaPvrtcI4BppSrgb },
        { TextureFormats.RgbaPvrtcII4BppUNorm, TextureFormats.RgbaPvrtcII4BppSrgb }
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void FloatEncodeAndDecodePreserveStoragePrecision(TextureFormat unorm, TextureFormat srgb)
    {
        var storageCoder = TextureCoderManager.Global.GetCoder(unorm);
        var srgbCoder = TextureCoderManager.Global.GetCoder(srgb);
        var storage = new ArrayBitmap<Rgba8UNorm>(16, 16);
        var linear = new ArrayBitmap<Rgba32Float>(16, 16);
        Array.Fill(storage.Pixels, new Rgba8UNorm(8, 2, 17, 187));
        Array.Fill(linear.Pixels, new Rgba32Float(8f / (255 * 12.92f), 2f / (255 * 12.92f),
            RgbaColorConversions.Srgb8ToLinearFloat(17), 187 / 255f));

        var expected = new byte[storageCoder.GetEncodedByteCount(16, 16)];
        var actual = new byte[expected.Length];
        storageCoder.Encode(storage.AsView(), expected);
        srgbCoder.Encode(linear.AsView(), actual);
        Assert.Equal(expected, actual);

        storageCoder.Decode(expected, storage.AsView());
        srgbCoder.Decode(expected, linear.AsView());
        for (var i = 0; i < linear.Pixels.Length; i++)
        {
            var color = storage.Pixels[i];
            Assert.Equal(RgbaColorConversions.Srgb8ToLinearFloat(color.Red), linear.Pixels[i].Red, 7);
            Assert.Equal(RgbaColorConversions.Srgb8ToLinearFloat(color.Green), linear.Pixels[i].Green, 7);
            Assert.Equal(RgbaColorConversions.Srgb8ToLinearFloat(color.Blue), linear.Pixels[i].Blue, 7);
            Assert.Equal(color.Alpha / 255f, linear.Pixels[i].Alpha, 6);
        }
    }

    [Fact]
    public void AstcVolumeRoundTripsDarkFloatAcrossBlocks()
    {
        var volume = new ArrayVolumeBitmap<Rgba32Float>(4, 4, 5);
        var color = new Rgba32Float(2f / (255 * 12.92f), 8f / (255 * 12.92f), 0, 128 / 255f);
        Array.Fill(volume.Pixels, color);
        var coder = TextureCoderManager.Global.GetCoder3D(TextureFormats.RgbaAstc3x3x3Srgb);
        var payload = new byte[coder.GetEncodedByteCount(4, 4, 5)];
        coder.Encode(volume.AsView(), payload);
        Array.Clear(volume.Pixels);
        coder.Decode(payload, volume.AsView());
        Assert.All(volume.Pixels, p =>
        {
            Assert.Equal(color.Red, p.Red, 7);
            Assert.Equal(color.Green, p.Green, 7);
            Assert.Equal(color.Alpha, p.Alpha, 6);
        });
    }
}
