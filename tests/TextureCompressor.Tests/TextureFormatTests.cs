using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureFormatTests
{
    [Theory]
    [MemberData(nameof(MvpFormats))]
    public void MvpFormatPropertiesAreStable(
        TextureFormat format,
        string name,
        TextureFormatKind kind,
        TextureComponents components,
        int channelCount,
        int bitsPerBlock,
        int bytesPerBlock)
    {
        Assert.Equal(name, format.Name);
        Assert.Equal(kind, format.Kind);
        Assert.Equal(components, format.Components);
        Assert.Equal(TextureValueKind.UNorm, format.ValueKind);
        Assert.Equal(channelCount, format.ChannelCount);
        Assert.Equal(bitsPerBlock, format.BitsPerBlock);
        Assert.Equal(bytesPerBlock, format.BytesPerBlock);
        Assert.Equal(kind == TextureFormatKind.BlockCompressed, format.IsCompressed);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void R8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.R8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(7, 14)]
    public void Rg8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rg8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(7, 21)]
    public void Rgb8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rgb8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(7, 28)]
    public void Rgba8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rgba8UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(7, 28)]
    public void Bgra8RowByteCountMatchesWidth(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bgra8.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void Bc1RowByteCountUsesFourByFourBlocks(int width, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc1.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(3, 2, 24)]
    [InlineData(7, 5, 140)]
    public void Rgba8ByteCountMatchesPackedRows(int width, int height, int expected)
    {
        Assert.Equal(expected, TextureFormats.Rgba8UNorm.GetByteCount(width, height));
    }

    [Theory]
    [InlineData(1, 1, 8)]
    [InlineData(4, 4, 8)]
    [InlineData(5, 4, 16)]
    [InlineData(5, 5, 32)]
    public void Bc1ByteCountUsesFourByFourBlocks(int width, int height, int expected)
    {
        Assert.Equal(expected, TextureFormats.Bc1.GetByteCount(width, height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RowByteCountRejectsInvalidWidth(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureFormats.Rgba8UNorm.GetRowByteCount(width));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ByteCountRejectsInvalidHeight(int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureFormats.Rgba8UNorm.GetByteCount(1, height));
    }

    public static TheoryData<TextureFormat, string, TextureFormatKind, TextureComponents, int, int, int> MvpFormats() => new()
    {
        { TextureFormats.R8, "R8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.R, 1, 8, 1 },
        { TextureFormats.Rg8, "RG8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Rg, 2, 16, 2 },
        { TextureFormats.Rgb8, "RGB8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Rgb, 3, 24, 3 },
        { TextureFormats.Rgba8UNorm, "RGBA8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Rgba, 4, 32, 4 },
        { TextureFormats.Bgra8, "BGRA8_UNORM", TextureFormatKind.Uncompressed, TextureComponents.Bgra, 4, 32, 4 },
        { TextureFormats.Bc1, "BC1_UNORM", TextureFormatKind.BlockCompressed, TextureComponents.Rgba, 4, 64, 8 }
    };
}
