using System.Runtime.InteropServices;
using AstcEncoder;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.AstcEnc;

public sealed class AstcEncTextureCoder : IPitchTextureCoder
{
    private static readonly TextureFormat[] SSupportedFormats = AstcTextureCoder.SupportedFormats.ToArray();
    private static readonly AstcencSwizzle SSwizzle = new()
    {
        r = AstcencSwz.AstcencSwzR,
        g = AstcencSwz.AstcencSwzG,
        b = AstcencSwz.AstcencSwzB,
        a = AstcencSwz.AstcencSwzA
    };

    private readonly AstcEncCoderOptions _options;
    private readonly AstcencProfile _profile;

    public AstcEncTextureCoder(TextureFormat format, AstcEncCoderOptions? options = null)
    {
        if (!AstcTextureCoder.IsSupported(format))
        {
            throw new NotSupportedException($"astcenc does not have a mapped coder for texture format '{format.Name}'.");
        }

        Format = format;
        _options = options ?? new AstcEncCoderOptions();
        _profile = GetProfile(format);
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) => AstcTextureCoder.IsSupported(format);

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        return checked(rowPitch * GetBlockRowCount(width, height));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var packed = CopyToPackedRows(source, destination.Width, destination.Height, rowPitch);
        using var context = CreateContext(AstcencFlags.DecompressOnly);

        if (Format.ValueKind == TextureValueKind.Float)
        {
            DecodeFloat(packed, destination, context);
            return;
        }

        DecodeUnorm(packed, destination, context);
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        using var context = CreateContext(GetEncodeFlags());

        var packed = new byte[Format.GetByteCount(source.Width, source.Height)];
        if (Format.ValueKind == TextureValueKind.Float)
        {
            EncodeFloat(source, packed, context);
        }
        else
        {
            EncodeUnorm(source, packed, context);
        }

        CopyPackedRowsToDestination(packed, source.Width, source.Height, destination, rowPitch);
    }

    private void EncodeUnorm<TPixel>(BitmapView<TPixel> source, Span<byte> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = CopyToRgba8(source);
        var image = new AstcencImage
        {
            dimX = checked((uint)source.Width),
            dimY = checked((uint)source.Height),
            dimZ = 1,
            dataType = AstcencType.AstcencTypeU8,
            data = [rgba]
        };

        Check(Astcenc.AstcencCompressImage(context.Handle, ref image, SSwizzle, destination, 0));
    }

    private void EncodeFloat<TPixel>(BitmapView<TPixel> source, Span<byte> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = CopyToRgba32Float(source);
        var image = new AstcencImage
        {
            dimX = checked((uint)source.Width),
            dimY = checked((uint)source.Height),
            dimZ = 1,
            dataType = AstcencType.AstcencTypeF32,
            data = [MemoryMarshal.AsBytes(rgba.AsSpan()).ToArray()]
        };

        Check(Astcenc.AstcencCompressImage(context.Handle, ref image, SSwizzle, destination, 0));
    }

    private void DecodeUnorm<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new byte[checked(destination.Width * destination.Height * 4)];
        var image = new AstcencImage
        {
            dimX = checked((uint)destination.Width),
            dimY = checked((uint)destination.Height),
            dimZ = 1,
            dataType = AstcencType.AstcencTypeU8,
            data = [rgba]
        };

        Check(Astcenc.AstcencDecompressImage(context.Handle, source.ToArray(), ref image, SSwizzle, 0));
        CopyFromRgba8(rgba, destination);
    }

    private void DecodeFloat<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new Rgba32Float[checked(destination.Width * destination.Height)];
        var rgbaBytes = new byte[MemoryMarshal.AsBytes(rgba.AsSpan()).Length];
        var image = new AstcencImage
        {
            dimX = checked((uint)destination.Width),
            dimY = checked((uint)destination.Height),
            dimZ = 1,
            dataType = AstcencType.AstcencTypeF32,
            data = [rgbaBytes]
        };

        Check(Astcenc.AstcencDecompressImage(context.Handle, source.ToArray(), ref image, SSwizzle, 0));
        var decoded = MemoryMarshal.Cast<byte, Rgba32Float>(rgbaBytes);
        for (var i = 0; i < rgba.Length; i++)
        {
            destination.Pixels[i] = TPixel.FromRgba32Float(decoded[i]);
        }
    }

    private AstcEncContext CreateContext(AstcencFlags additionalFlags)
    {
        var flags = GetCommonFlags() | additionalFlags;
        Check(Astcenc.AstcencConfigInit(
            _profile,
            checked((uint)Format.BlockWidth),
            checked((uint)Format.BlockHeight),
            blockZ: 1,
            _options.Quality,
            flags,
            out var config));

        Check(Astcenc.AstcencContextAlloc(ref config, threadCount: 1, out var context, AstcencContext.Null));
        return new AstcEncContext(context);
    }

    private AstcencFlags GetCommonFlags()
    {
        var flags = _options.Flags;
        if (Format.ValueKind == TextureValueKind.Float)
        {
            flags &= ~AstcencFlags.UseDecodeUnorm8;
        }

        return flags;
    }

    private AstcencFlags GetEncodeFlags() =>
        GetCommonFlags();

    private byte[] CopyToPackedRows(ReadOnlySpan<byte> source, int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = Format.GetRowByteCount(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        var blockRows = GetBlockRowCount(width, height);
        var required = checked(rowPitch * blockRows);
        if (source.Length < required)
        {
            throw new ArgumentException("Source span is too small for the texture dimensions and row pitch.", nameof(source));
        }

        var packedSize = checked(rowByteCount * blockRows);
        var packed = new byte[packedSize];
        if (rowPitch == rowByteCount)
        {
            source[..packedSize].CopyTo(packed);
            return packed;
        }

        for (var row = 0; row < blockRows; row++)
        {
            source.Slice(checked(row * rowPitch), rowByteCount).CopyTo(packed.AsSpan(checked(row * rowByteCount)));
        }

        return packed;
    }

    private void CopyPackedRowsToDestination(ReadOnlySpan<byte> packed, int width, int height, Span<byte> destination, int rowPitch)
    {
        var rowByteCount = Format.GetRowByteCount(width);
        var blockRows = GetBlockRowCount(width, height);
        if (rowPitch == rowByteCount)
        {
            packed.CopyTo(destination);
            return;
        }

        for (var row = 0; row < blockRows; row++)
        {
            packed.Slice(checked(row * rowByteCount), rowByteCount).CopyTo(destination.Slice(checked(row * rowPitch), rowByteCount));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = Format.GetRowByteCount(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        var required = checked(rowPitch * GetBlockRowCount(width, height));
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination span is too small for the texture dimensions and row pitch.", nameof(destination));
        }
    }

    private int GetBlockRowCount(int width, int height) =>
        checked(Format.GetByteCount(width, height) / Format.GetRowByteCount(width));

    private static byte[] CopyToRgba8<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var result = new byte[checked(source.Width * source.Height * 4)];
        var offset = 0;
        foreach (var pixel in source.Pixels)
        {
            var rgba = TPixel.ToRgba8UNorm(pixel);
            result[offset++] = rgba.Red;
            result[offset++] = rgba.Green;
            result[offset++] = rgba.Blue;
            result[offset++] = rgba.Alpha;
        }

        return result;
    }

    private static Rgba32Float[] CopyToRgba32Float<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var result = new Rgba32Float[checked(source.Width * source.Height)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = TPixel.ToRgba32Float(source.Pixels[i]);
        }

        return result;
    }

    private static void CopyFromRgba8<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var offset = 0;
        for (var i = 0; i < destination.Pixels.Length; i++)
        {
            destination.Pixels[i] = TPixel.FromRgba8UNorm(new Rgba8UNorm(
                source[offset++],
                source[offset++],
                source[offset++],
                source[offset++]));
        }
    }

    private static AstcencProfile GetProfile(TextureFormat format) => format.ValueKind switch
    {
        TextureValueKind.Srgb => AstcencProfile.AstcencPrfLdrSrgb,
        TextureValueKind.Float => AstcencProfile.AstcencPrfHdr,
        _ => AstcencProfile.AstcencPrfLdr
    };

    private static void Check(AstcencError error)
    {
        if (error != AstcencError.AstcencSuccess)
        {
            throw new InvalidOperationException(Astcenc.GetErrorString(error));
        }
    }

    private sealed class AstcEncContext(AstcencContext handle) : IDisposable
    {
        private AstcencContext _handle = handle;
        private bool _disposed;

        public AstcencContext Handle => _handle;

        public void Dispose()
        {
            if (!_disposed)
            {
                Astcenc.AstcencContextFree(_handle);
                _handle = AstcencContext.Null;
                _disposed = true;
            }
        }
    }
}
