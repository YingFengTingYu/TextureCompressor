using System.Buffers;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Bitmaps;

public sealed class PooledTextureBitmap<TPixel> : IBitmap<TPixel>, IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ArrayPool<TPixel>? _arrayPool;
    private readonly bool _clearArrayOnReturn;
    private readonly IMemoryOwner<TPixel>? _memoryOwner;
    private readonly int _pixelCount;
    private readonly TPixel[]? _rentedArray;
    private bool _disposed;

    public PooledTextureBitmap(int width, int height)
        : this(width, height, ArrayPool<TPixel>.Shared)
    {
    }

    public PooledTextureBitmap(
        int width,
        int height,
        ArrayPool<TPixel> arrayPool,
        bool clearOnRent = true,
        bool clearOnReturn = false)
    {
        ArgumentNullException.ThrowIfNull(arrayPool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _pixelCount = checked(width * height);
        _arrayPool = arrayPool;
        _clearArrayOnReturn = clearOnReturn;
        _rentedArray = arrayPool.Rent(_pixelCount);

        if (clearOnRent)
        {
            _rentedArray.AsSpan(0, _pixelCount).Clear();
        }

        Width = width;
        Height = height;
    }

    public PooledTextureBitmap(
        int width,
        int height,
        MemoryPool<TPixel> memoryPool,
        bool clearOnRent = true)
    {
        ArgumentNullException.ThrowIfNull(memoryPool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _pixelCount = checked(width * height);

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
    }

    public int Width { get; }

    public int Height { get; }

    public TextureFormat Format => TPixel.Format;

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

    public ImageView<TPixel> AsView() => new(PixelSpan, Width, Height);

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
