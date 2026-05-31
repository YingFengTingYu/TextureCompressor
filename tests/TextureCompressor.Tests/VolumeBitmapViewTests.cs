using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Tests;

public sealed class VolumeBitmapViewTests
{
    [Fact]
    public void ConstructorTrimsPixelsToVolumeDimensions()
    {
        var pixels = Enumerable.Range(0, 9)
            .Select(static value => new Rgba8UNorm((byte)value, (byte)(value + 1), (byte)(value + 2)))
            .ToArray();

        var view = new VolumeBitmapView<Rgba8UNorm>(pixels, 2, 2, 2);

        Assert.Equal(8, view.Pixels.Length);
        Assert.Equal(new Rgba8UNorm(7, 8, 9), view.Pixels[7]);
    }

    [Fact]
    public void PixelsReturnsMutableVolumeSpan()
    {
        var pixels = new Rgba8UNorm[8];
        var view = new VolumeBitmapView<Rgba8UNorm>(pixels, 2, 2, 2);

        view.Pixels[6] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(new Rgba8UNorm(20, 21, 22), view[0, 1, 1]);
    }

    [Fact]
    public void GetSliceSpanReturnsMutableSlice()
    {
        var pixels = new Rgba8UNorm[8];
        var view = new VolumeBitmapView<Rgba8UNorm>(pixels, 2, 2, 2);

        var slice = view.GetSliceSpan(1);
        slice[3] = new Rgba8UNorm(30, 31, 32);

        Assert.Equal(4, slice.Length);
        Assert.Equal(new Rgba8UNorm(30, 31, 32), view[1, 1, 1]);
    }

    [Fact]
    public void GetSliceViewReturnsMutableBitmapView()
    {
        var pixels = new Rgba8UNorm[8];
        var view = new VolumeBitmapView<Rgba8UNorm>(pixels, 2, 2, 2);

        var slice = view.GetSliceView(1);
        slice[0, 1] = new Rgba8UNorm(40, 41, 42);

        Assert.Equal(2, slice.Width);
        Assert.Equal(2, slice.Height);
        Assert.Equal(new Rgba8UNorm(40, 41, 42), view[0, 1, 1]);
    }

    [Fact]
    public void SliceIndexerReturnsMutableBitmapView()
    {
        var pixels = new Rgba8UNorm[8];
        var view = new VolumeBitmapView<Rgba8UNorm>(pixels, 2, 2, 2);

        var slice = view[1];
        slice[1, 0] = new Rgba8UNorm(50, 51, 52);

        Assert.Equal(new Rgba8UNorm(50, 51, 52), view[1, 0, 1]);
    }

    [Fact]
    public void GetRowSpanReturnsMutableRow()
    {
        var pixels = new Rgba8UNorm[8];
        var view = new VolumeBitmapView<Rgba8UNorm>(pixels, 2, 2, 2);

        var row = view.GetRowSpan(1, 1);
        row[1] = new Rgba8UNorm(60, 61, 62);

        Assert.Equal(2, row.Length);
        Assert.Equal(new Rgba8UNorm(60, 61, 62), view[1, 1, 1]);
    }

    [Fact]
    public void GetIndexReturnsLinearVolumeOffset()
    {
        var view = new VolumeBitmapView<Rgba8UNorm>(new Rgba8UNorm[24], 3, 2, 4);

        Assert.Equal(17, view.GetIndex(2, 1, 2));
    }

    [Fact]
    public void ConstructorRejectsTooSmallPixelSpan()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VolumeBitmapView<Rgba8UNorm>(new Rgba8UNorm[7], 2, 2, 2));

        Assert.Equal("pixels", exception.ParamName);
    }

    [Fact]
    public void GetSliceSpanRejectsOutOfRangeZ()
    {
        static void Act()
        {
            var view = new VolumeBitmapView<Rgba8UNorm>(new Rgba8UNorm[8], 2, 2, 2);
            view.GetSliceSpan(2);
        }

        var exception = Assert.Throws<ArgumentOutOfRangeException>(Act);

        Assert.Equal("z", exception.ParamName);
    }

    [Fact]
    public void GetRowSpanRejectsOutOfRangeY()
    {
        static void Act()
        {
            var view = new VolumeBitmapView<Rgba8UNorm>(new Rgba8UNorm[8], 2, 2, 2);
            view.GetRowSpan(2, 0);
        }

        var exception = Assert.Throws<ArgumentOutOfRangeException>(Act);

        Assert.Equal("y", exception.ParamName);
    }
}
