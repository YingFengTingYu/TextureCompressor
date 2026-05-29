using TextureCompressor.Colors;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Tests;

public sealed class BitmapViewTests
{
    [Fact]
    public void ConstructorTrimsPixelsToBitmapDimensions()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(1, 2, 3),
            new Rgba8UNorm(4, 5, 6),
            new Rgba8UNorm(7, 8, 9),
            new Rgba8UNorm(10, 11, 12),
            new Rgba8UNorm(13, 14, 15)
        };

        var view = new BitmapView<Rgba8UNorm>(pixels, 2, 2);

        Assert.Equal(4, view.Pixels.Length);
        Assert.Equal(new Rgba8UNorm(10, 11, 12), view.Pixels[3]);
    }

    [Fact]
    public void PixelsReturnsMutableBitmapSpan()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(1, 2, 3),
            new Rgba8UNorm(4, 5, 6),
            new Rgba8UNorm(7, 8, 9),
            new Rgba8UNorm(10, 11, 12)
        };
        var view = new BitmapView<Rgba8UNorm>(pixels, 2, 2);

        view.Pixels[2] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(new Rgba8UNorm(20, 21, 22), view[0, 1]);
    }

    [Fact]
    public void GetRowSpanReturnsMutableRow()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(1, 2, 3),
            new Rgba8UNorm(4, 5, 6),
            new Rgba8UNorm(7, 8, 9),
            new Rgba8UNorm(10, 11, 12)
        };
        var view = new BitmapView<Rgba8UNorm>(pixels, 2, 2);

        var row = view.GetRowSpan(1);
        row[1] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(2, row.Length);
        Assert.Equal(new Rgba8UNorm(20, 21, 22), view[1, 1]);
    }

    [Fact]
    public void RowIndexerReturnsMutableRow()
    {
        var pixels = new[]
        {
            new Rgba8UNorm(1, 2, 3),
            new Rgba8UNorm(4, 5, 6),
            new Rgba8UNorm(7, 8, 9),
            new Rgba8UNorm(10, 11, 12)
        };
        var view = new BitmapView<Rgba8UNorm>(pixels, 2, 2);

        var row = view[1];
        row[0] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(2, row.Length);
        Assert.Equal(new Rgba8UNorm(20, 21, 22), view[0, 1]);
    }

    [Fact]
    public void GetRowSpanRejectsOutOfRangeY()
    {
        static void Act()
        {
            var view = new BitmapView<Rgba8UNorm>(new Rgba8UNorm[4], 2, 2);
            view.GetRowSpan(2);
        }

        var exception = Assert.Throws<ArgumentOutOfRangeException>(Act);

        Assert.Equal("y", exception.ParamName);
    }
}
