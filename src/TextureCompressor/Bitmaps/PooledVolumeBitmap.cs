using System.Buffers;
using TextureCompressor.Colors;

namespace TextureCompressor.Bitmaps;

public sealed class PooledVolumeBitmap<TPixel> : IVolumeBitmap<TPixel>, IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ArrayPool<TPixel>? _arrayPool;
    private readonly bool _clearArrayOnReturn;
    private readonly IMemoryOwner<TPixel>? _memoryOwner;
    private readonly int _pixelCount;
    private readonly TPixel[]? _rentedArray;
    private bool _disposed;

    public PooledVolumeBitmap(int width, int height, int depth)
        : this(width, height, depth, ArrayPool<TPixel>.Shared)
    {
    }

    public PooledVolumeBitmap(
        int width,
        int height,
        int depth,
        ArrayPool<TPixel> arrayPool,
        bool clearOnRent = true,
        bool clearOnReturn = false)
    {
        ArgumentNullException.ThrowIfNull(arrayPool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        _pixelCount = checked(width * height * depth);
        _arrayPool = arrayPool;
        _clearArrayOnReturn = clearOnReturn;
        _rentedArray = arrayPool.Rent(_pixelCount);

        if (clearOnRent)
        {
            _rentedArray.AsSpan(0, _pixelCount).Clear();
        }

        Width = width;
        Height = height;
        Depth = depth;
    }

    public PooledVolumeBitmap(
        int width,
        int height,
        int depth,
        MemoryPool<TPixel> memoryPool,
        bool clearOnRent = true)
    {
        ArgumentNullException.ThrowIfNull(memoryPool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        _pixelCount = checked(width * height * depth);

        var memoryOwner = memoryPool.Rent(_pixelCount);
        if (memoryOwner.Memory.Length < _pixelCount)
        {
            memoryOwner.Dispose();
            throw new InvalidOperationException("MemoryPool returned a buffer that is too small.");
        }

        _memoryOwner = memoryOwner;

        if (clearOnRent)
        {
            _memoryOwner.Memory.Span[.._pixelCount].Clear();
        }

        Width = width;
        Height = height;
        Depth = depth;
    }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public TPixel[]? RentedArray
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _rentedArray;
        }
    }

    public Memory<TPixel> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _rentedArray?.AsMemory(0, _pixelCount) ?? _memoryOwner!.Memory[.._pixelCount];
        }
    }

    public Span<TPixel> PixelSpan => Memory.Span;

    public VolumeBitmapView<TPixel> AsView() => new(PixelSpan, Width, Height, Depth);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_rentedArray is not null)
        {
            _arrayPool!.Return(_rentedArray, _clearArrayOnReturn);
        }

        _memoryOwner?.Dispose();
    }
}
