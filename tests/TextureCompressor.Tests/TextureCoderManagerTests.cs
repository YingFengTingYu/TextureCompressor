using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureCoderManagerTests
{
    [Fact]
    public void GlobalManagerFindsSequentialUncompressedCoder()
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UNorm);

        Assert.IsType<SequentialUncompressedTextureCoder>(coder);
    }

    [Fact]
    public void GlobalManagerDoesNotClaimCompressedFormats()
    {
        var found = TextureCoderManager.Global.TryGetCoder(TextureFormats.Bc1, out var coder);

        Assert.False(found);
        Assert.Null(coder);
    }

    [Fact]
    public void EncodeAndDecodeBgra8SwizzlesChannels()
    {
        var source = new ArrayTextureBitmap<Rgba8UNorm>(
            1,
            1,
            [new Rgba8UNorm(1, 2, 3, 4)]);

        var coder = Assert.IsType<SequentialUncompressedTextureCoder>(TextureCoderManager.Global.GetCoder(TextureFormats.Bgra8));
        var rowPitch = coder.GetDefaultPitch(source.Width);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, rowPitch)];
        coder.Encode(source.AsView(), encoded, rowPitch);

        var decoded = new ArrayTextureBitmap<Rgba8UNorm>(1, 1);
        coder.Decode(encoded, decoded.AsView(), rowPitch);

        Assert.Equal([3, 2, 1, 4], encoded);
        Assert.Equal(source.Pixels[0], decoded.Pixels[0]);
    }
}
