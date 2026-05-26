using System.Buffers;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class PooledTextureBitmapTests
{
    [Fact]
    public void ArrayPoolConstructorRentsAndReturnsBuffer()
    {
        var pool = new TrackingArrayPool<Rgba8UNorm>();
        var bitmap = new PooledTextureBitmap<Rgba8UNorm>(
            2,
            2,
            pool,
            clearOnRent: false,
            clearOnReturn: true);

        bitmap.PixelSpan[3] = new Rgba8UNorm(20, 21, 22);

        Assert.Equal(4, pool.RentedMinimumLength);
        Assert.Same(pool.RentedArray, bitmap.RentedArray);
        Assert.Equal(TextureFormats.Rgba8UNorm, bitmap.Format);
        Assert.Equal(new Rgba8UNorm(20, 21, 22), bitmap.AsView()[1, 1]);

        bitmap.Dispose();

        Assert.Same(pool.RentedArray, pool.ReturnedArray);
        Assert.True(pool.ClearArrayOnReturn);
        Assert.Throws<ObjectDisposedException>(() => _ = bitmap.PixelSpan.Length);
    }

    [Fact]
    public void MemoryPoolConstructorDisposesOwner()
    {
        var pool = new TrackingMemoryPool<Rgba8UNorm>();
        var bitmap = new PooledTextureBitmap<Rgba8UNorm>(
            2,
            2,
            pool,
            clearOnRent: false);

        bitmap.PixelSpan[2] = new Rgba8UNorm(30, 31, 32);

        Assert.Equal(4, pool.RentedMinimumLength);
        Assert.Equal(new Rgba8UNorm(30, 31, 32), pool.Owner!.Buffer[2]);

        bitmap.Dispose();

        Assert.True(pool.Owner.Disposed);
        Assert.Throws<ObjectDisposedException>(() => _ = bitmap.Memory.Length);
    }

    private sealed class TrackingArrayPool<T> : ArrayPool<T>
    {
        public bool ClearArrayOnReturn { get; private set; }

        public T[]? RentedArray { get; private set; }

        public int RentedMinimumLength { get; private set; }

        public T[]? ReturnedArray { get; private set; }

        public override T[] Rent(int minimumLength)
        {
            RentedMinimumLength = minimumLength;
            RentedArray = new T[minimumLength];

            return RentedArray;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            ClearArrayOnReturn = clearArray;
            ReturnedArray = array;
        }
    }

    private sealed class TrackingMemoryPool<T> : MemoryPool<T>
    {
        public TrackingMemoryOwner<T>? Owner { get; private set; }

        public int RentedMinimumLength { get; private set; }

        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            RentedMinimumLength = minBufferSize;
            Owner = new TrackingMemoryOwner<T>(minBufferSize);

            return Owner;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed class TrackingMemoryOwner<T>(int length) : IMemoryOwner<T>
    {
        public T[] Buffer { get; } = new T[length];

        public bool Disposed { get; private set; }

        public Memory<T> Memory => Buffer;

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
