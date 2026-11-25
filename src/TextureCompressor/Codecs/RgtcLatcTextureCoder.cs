using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;
using TextureCompressor.Options;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

public sealed class RgtcLatcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private readonly RgtcLatcLayout _layout;
    private readonly bool _isSigned;
    private readonly TextureCompressionOptions _options;

    public RgtcLatcTextureCoder(TextureFormat format, TextureCompressionOptions? options = null)
    {
        if (!TryGetLayout(format, out _layout, out _isSigned))
        {
            throw CreateUnsupportedFormatException(format);
        }

        _options = options ?? new TextureCompressionOptions();
        Format = format;
    }

    public TextureFormat Format { get; }

    public TextureCompressionOptions Options => _options;

    public static bool IsSupported(TextureFormat format) => TryGetLayout(format, out _, out _);

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        return checked(rowPitch * GetBlockCount(height));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        bool dual = Format.BytesPerBlock == 16;
        Span<float> first = stackalloc float[TexelsPerBlock];
        Span<float> second = stackalloc float[TexelsPerBlock];
        int blockColumns = GetBlockCount(destination.Width);
        int blockRows = GetBlockCount(destination.Height);
        for (int by = 0; by < blockRows; by++)
        {
            for (int bx = 0; bx < blockColumns; bx++)
            {
                var block = source.Slice(by * rowPitch + bx * Format.BytesPerBlock, Format.BytesPerBlock);
                Bc4Block.Decode(block, _isSigned, first);
                if (dual) Bc4Block.Decode(block[8..], _isSigned, second);
                int width = Math.Min(4, destination.Width - bx * 4);
                int height = Math.Min(4, destination.Height - by * 4);
                for (int y = 0; y < height; y++)
                {
                    var row = destination.GetRowSpan(by * 4 + y).Slice(bx * 4, width);
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * 4 + x;
                        float r = first[i];
                        var pixel = _layout switch
                        {
                            RgtcLatcLayout.R => new Rgba32Float(r, 0, 0),
                            RgtcLatcLayout.Rg => new Rgba32Float(r, second[i], 0),
                            RgtcLatcLayout.Luminance => new Rgba32Float(r, r, r),
                            _ => new Rgba32Float(r, r, r, second[i])
                        };
                        row[x] = _isSigned ? RgbaColorConversions.FromSignedRgba32Float<TPixel>(pixel) : TPixel.FromRgba32Float(pixel);
                    }
                }
            }
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        var level = _options.CompressionMode;
        if (level is < TextureCompressionLevel.Fast or > TextureCompressionLevel.Exhaustive)
            throw new ArgumentOutOfRangeException(nameof(Options), level, "Unsupported RGTC/LATC compression mode.");

        int columns = GetBlockCount(source.Width), rows = GetBlockCount(source.Height);
        if (TextureCodingParallel.ShouldParallelize(columns, rows))
        {
            int width = source.Width, height = source.Height;
            unsafe
            {
                fixed (TPixel* pixels = source.Pixels)
                fixed (byte* bytes = destination)
                {
                    nint input = (nint)pixels, output = (nint)bytes;
                    Parallel.For(0, rows, y =>
                    {
                        var view = new BitmapView<TPixel>(new Span<TPixel>((void*)input, checked(width * height)), width, height);
                        var row = new Span<byte>((void*)(output + y * rowPitch), rowPitch);
                        EncodeRow(view, row, y, columns, level);
                    });
                }
            }
        }
        else
        {
            for (int y = 0; y < rows; y++)
                EncodeRow(source, destination.Slice(y * rowPitch, rowPitch), y, columns, level);
        }
    }

    private void EncodeRow<TPixel>(BitmapView<TPixel> source, Span<byte> row, int by, int columns, TextureCompressionLevel level)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool dual = Format.BytesPerBlock == 16;
        Span<float> first = stackalloc float[TexelsPerBlock];
        Span<float> second = stackalloc float[TexelsPerBlock];
        for (int bx = 0; bx < columns; bx++)
        {
            for (int y = 0; y < 4; y++)
            {
                var pixels = source.GetRowSpan(Math.Min(4 * by + y, source.Height - 1));
                for (int x = 0; x < 4; x++)
                {
                    var texel = pixels[Math.Min(4 * bx + x, source.Width - 1)];
                    var p = _isSigned ? RgbaColorConversions.ToSignedRgba32Float(texel) : TPixel.ToRgba32Float(texel);
                    first[4 * y + x] = p.Red;
                    if (dual) second[4 * y + x] = _layout == RgtcLatcLayout.Rg ? p.Green : p.Alpha;
                }
            }

            var block = row.Slice(bx * Format.BytesPerBlock, Format.BytesPerBlock);
            Bc4Block.Encode(first, _isSigned, level, block);
            if (dual) Bc4Block.Encode(second, _isSigned, level, block[8..]);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded RGTC/LATC texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded RGTC/LATC texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size) => (size + BlockSize - 1) / BlockSize;

    private static bool TryGetLayout(
        TextureFormat format,
        out RgtcLatcLayout layout,
        out bool isSigned)
    {
        if (format == TextureFormats.Bc4UNorm
            || format == TextureFormats.Ati1N
            || format == TextureFormats.Ati1UNorm
            || format == TextureFormats.Rgtc1UNorm)
        {
            layout = RgtcLatcLayout.R;
            isSigned = false;
            return true;
        }

        if (format == TextureFormats.Bc4SNorm
            || format == TextureFormats.Ati1SNorm
            || format == TextureFormats.Rgtc1SNorm)
        {
            layout = RgtcLatcLayout.R;
            isSigned = true;
            return true;
        }

        if (format == TextureFormats.Bc5UNorm
            || format == TextureFormats.Ati2N
            || format == TextureFormats.Ati2NXy
            || format == TextureFormats.Ati2UNorm
            || format == TextureFormats.Rgtc2UNorm)
        {
            layout = RgtcLatcLayout.Rg;
            isSigned = false;
            return true;
        }

        if (format == TextureFormats.Bc5SNorm
            || format == TextureFormats.Ati2SNorm
            || format == TextureFormats.Rgtc2SNorm)
        {
            layout = RgtcLatcLayout.Rg;
            isSigned = true;
            return true;
        }

        if (format == TextureFormats.Latc1UNorm)
        {
            layout = RgtcLatcLayout.Luminance;
            isSigned = false;
            return true;
        }

        if (format == TextureFormats.Latc1SNorm)
        {
            layout = RgtcLatcLayout.Luminance;
            isSigned = true;
            return true;
        }

        if (format == TextureFormats.Latc2UNorm)
        {
            layout = RgtcLatcLayout.LuminanceAlpha;
            isSigned = false;
            return true;
        }

        if (format == TextureFormats.Latc2SNorm)
        {
            layout = RgtcLatcLayout.LuminanceAlpha;
            isSigned = true;
            return true;
        }

        layout = default;
        isSigned = false;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"RGTC/LATC texture coder does not support texture format '{format.Name}'.");

    private enum RgtcLatcLayout
    {
        R,
        Rg,
        Luminance,
        LuminanceAlpha
    }
}
