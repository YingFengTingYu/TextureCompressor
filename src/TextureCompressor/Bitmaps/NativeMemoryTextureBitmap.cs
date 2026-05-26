using System.Runtime.InteropServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Bitmaps;

public sealed unsafe class NativeMemoryTextureBitmap<TPixel> : IBitmap<TPixel>, IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int _pixelCount;
    private TPixel* _pixels;
    private bool _disposed;

    public NativeMemoryTextureBitmap(int width, int height, nuint alignment = 4096, bool clear = true)
    {
        if (alignment == 0 || (alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be a non-zero power of two.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _pixelCount = checked(width * height);

        var byteCount = checked((nuint)_pixelCount * (nuint)sizeof(TPixel));
        _pixels = (TPixel*)NativeMemory.AlignedAlloc(byteCount, alignment);
        if (_pixels == null)
        {
            throw new OutOfMemoryException();
        }

        if (clear)
        {
            PixelSpan.Clear();
        }

        Width = width;
        Height = height;
        Alignment = alignment;
    }

    ~NativeMemoryTextureBitmap()
    {
        Free();
    }

    public int Width { get; }

    public int Height { get; }

    public nuint Alignment { get; }

    public TextureFormat Format => TPixel.Format;

    public TPixel* PixelPointer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _pixels;
        }
    }

    public nint Pointer => (nint)PixelPointer;

    public Span<TPixel> PixelSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Span<TPixel>(_pixels, _pixelCount);
        }
    }

    public ImageView<TPixel> AsView() => new(PixelSpan, Width, Height);

    public void Dispose()
    {
        Free();
        GC.SuppressFinalize(this);
    }

    private void Free()
    {
        if (_pixels is null)
        {
            return;
        }

        NativeMemory.AlignedFree(_pixels);
        _pixels = null;
        _disposed = true;
    }
}
