using System.Text;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Dds.Tests;

public sealed class DdsSwizzleTests
{
    // Payloads emitted by Compressonator CLI 4.6.0. In slot order, 'x' is unused.
    public static TheoryData<TextureFormat, string, string, string> Swizzles => new()
    {
        { TextureFormats.Dxt5Xgbr, "xGBR", "xGBR", "D4D3499224499224A704A604AAAAAAAA" },
        { TextureFormats.Dxt5Rxbg, "RxBG", "RxBG", "969549922449922407D806B8AAAAAAAA" },
        { TextureFormats.Dxt5Rbxg, "RBxG", "RBxG", "9695499224499224E0D940B9AAAAAAAA" },
        { TextureFormats.Dxt5Xrbg, "xRBG", "xRBG", "969549922449922487068606AAAAAAAA" },
        { TextureFormats.Dxt5Rgxb, "RGxB", "RGxB", "3635499224499224A0DCA0BCAAAAAAAA" },
        { TextureFormats.Dxt5Xgxr, "xGxR", "xGxR", "D4D3499224499224A004A004AAAAAAAA" },
        { TextureFormats.Ati2NDxt5, "A2D5", "xGxR", "3635499224499224A004A004AAAAAAAA" }
    };

    [Theory]
    [MemberData(nameof(Swizzles))]
    public void ReadsAndWritesPhysicalBc3Channels(TextureFormat format, string fourCc, string slots, string amdBlock)
    {
        var input = new Rgba8UNorm(53, 149, 211, 17);
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, Enumerable.Repeat(input, 16).ToArray());
        var dds = DdsCodec.Encode(source, new DdsEncodingOptions { TextureFormat = format });
        Assert.Equal(fourCc, Encoding.ASCII.GetString(dds, 84, 4));
        Assert.Equal(format, DdsCodec.Read(dds).Texture.Format);
        Assert.Throws<NotSupportedException>(() => DdsCodec.Encode(source,
            new DdsEncodingOptions { HeaderKind = DdsHeaderKind.Dxt10, TextureFormat = format }));
        var stored = new ArrayBitmap<Rgba8UNorm>(4, 4);
        ITextureCoder rawDecoder = new S3tcTextureCoder(TextureFormats.Bc3Rgba);
        rawDecoder.Decode(dds.AsSpan(128), stored.AsView());
        byte[] channels = [stored.Pixels[0].Red, stored.Pixels[0].Green, stored.Pixels[0].Blue, stored.Pixels[0].Alpha];
        for (int i = 0; i < 4; i++)
        {
            int expected = slots[i] switch { 'R' => input.Red, 'G' => input.Green, 'B' => input.Blue, _ => 0 };
            Assert.InRange(Math.Abs(channels[i] - expected), 0, 4);
        }

        var block = Convert.FromHexString(amdBlock);
        block.CopyTo(dds, 128);
        rawDecoder.Decode(block, stored.AsView());
        var decoded = DdsCodec.Decode(dds);
        for (int i = 0; i < 16; i++)
        {
            var raw = stored.Pixels[i];
            byte[] components = [raw.Red, raw.Green, raw.Blue, raw.Alpha];
            int blue = slots.IndexOf('B');
            Assert.Equal(new Rgba8UNorm(components[slots.IndexOf('R')], components[slots.IndexOf('G')],
                blue < 0 ? (byte)0 : components[blue], 255), decoded.Pixels[i]);
        }

        // Older AMD files put the logical layout in the secondary FourCC.
        Encoding.ASCII.GetBytes("DXT5").CopyTo(dds, 84);
        Encoding.ASCII.GetBytes(fourCc).CopyTo(dds, 88);
        Assert.Equal(format, DdsCodec.Read(dds).Texture.Format);
    }

    [Fact]
    public void Ati2NXyKeepsItsSecondaryFourCc()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4, Enumerable.Repeat(new Rgba8UNorm(53, 149, 211), 16).ToArray());
        var dds = DdsCodec.Encode(source, new DdsEncodingOptions { TextureFormat = TextureFormats.Ati2NXy });
        Assert.Equal("ATI2A2XY", Encoding.ASCII.GetString(dds, 84, 8));
        Convert.FromHexString("36354992244992249695499224499224").CopyTo(dds, 128);
        Assert.Equal(TextureFormats.Ati2NXy, DdsCodec.Read(dds).Texture.Format);
        Assert.Equal(new Rgba8UNorm(53, 149, 0), DdsCodec.Decode(dds).Pixels[0]);
        dds.AsSpan(88, 4).Clear();
        Assert.Equal(TextureFormats.Bc5UNorm, DdsCodec.Read(dds).Texture.Format);
    }
}
