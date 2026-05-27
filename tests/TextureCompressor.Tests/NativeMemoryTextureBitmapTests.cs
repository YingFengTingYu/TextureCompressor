using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;

namespace TextureCompressor.Tests;

public sealed class NativeMemoryTextureBitmapTests
{
    [Fact]
    public void ConstructorAllocatesAlignedPixelSpan()
    {
        using var bitmap = new NativeMemoryTextureBitmap<Rgba8UNorm>(2, 2);

        bitmap.PixelSpan[1] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
        Assert.Equal(4096u, bitmap.Alignment);
        Assert.Equal(0, bitmap.Pointer.ToInt64() & 4095);
        Assert.Equal(4, bitmap.PixelSpan.Length);
        Assert.Equal(new Rgba8UNorm(20, 21, 22), bitmap.AsView()[1, 0]);
    }

    [Fact]
    public void PixelSpanRejectsAccessAfterDispose()
    {
        var bitmap = new NativeMemoryTextureBitmap<Rgba8UNorm>(1, 1);

        bitmap.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = bitmap.PixelSpan.Length);
    }
}
