using System.Runtime.InteropServices;
using AstcEncoder;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.AstcEnc;

public sealed unsafe class AstcEnc3DTextureCoder : IPitchTextureCoder3D
{
    private static readonly TextureFormat[] SSupportedFormats = Astc3DTextureCoder.SupportedFormats.ToArray();
    private static readonly AstcencSwizzle SSwizzle = new()
    {
        r = AstcencSwz.AstcencSwzR,
        g = AstcencSwz.AstcencSwzG,
        b = AstcencSwz.AstcencSwzB,
        a = AstcencSwz.AstcencSwzA
    };

    private readonly AstcEncCoderOptions _options;
    private readonly AstcencProfile _profile;

    public AstcEnc3DTextureCoder(TextureFormat format, AstcEncCoderOptions? options = null)
    {
        if (!Astc3DTextureCoder.IsSupported(format))
        {
            throw new NotSupportedException($"astcenc does not have a mapped 3D coder for texture format '{format.Name}'.");
        }

        Format = format;
        _options = options ?? new AstcEncCoderOptions();
        _profile = GetProfile(format);
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) => Astc3DTextureCoder.IsSupported(format);

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetDefaultSlicePitch(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        return checked(rowPitch * GetBlockCount(height, Format.BlockHeight));
    }

    public int GetDefaultSlicePitch(int width, int height) =>
        GetDefaultSlicePitch(width, height, GetDefaultPitch(width));

    public int GetEncodedByteCount(int width, int height, int depth, int rowPitch, int slicePitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var sliceByteCount = GetDefaultSlicePitch(width, height, rowPitch);
        if (slicePitch < sliceByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slicePitch), "Slice pitch must be at least the packed block-slice byte count.");
        }

        return checked(slicePitch * GetBlockCount(depth, Format.BlockDepth));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, int rowPitch, int slicePitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, destination.Depth, source, rowPitch, slicePitch);

        var packed = CopyToPackedBlocks(source, destination.Width, destination.Height, destination.Depth, rowPitch, slicePitch);
        using var context = CreateContext(AstcencFlags.DecompressOnly);

        if (Format.ValueKind == TextureValueKind.Float)
        {
            DecodeFloat(packed, destination, context);
            return;
        }

        DecodeUnorm(packed, destination, context);
    }

    public void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, int rowPitch, int slicePitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, source.Depth, destination, rowPitch, slicePitch);
        using var context = CreateContext(GetEncodeFlags());

        var packed = new byte[GetPackedByteCount(source.Width, source.Height, source.Depth)];
        if (Format.ValueKind == TextureValueKind.Float)
        {
            EncodeFloat(source, packed, context);
        }
        else
        {
            EncodeUnorm(source, packed, context);
        }

        CopyPackedBlocksToDestination(packed, source.Width, source.Height, source.Depth, destination, rowPitch, slicePitch);
    }

    private void EncodeUnorm<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = CopyToRgba8(source);
        CompressImage(
            context.Handle,
            checked((uint)source.Width),
            checked((uint)source.Height),
            checked((uint)source.Depth),
            AstcencType.AstcencTypeU8,
            rgba,
            destination);
    }

    private void EncodeFloat<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = CopyToRgba32Float(source);
        CompressImage(
            context.Handle,
            checked((uint)source.Width),
            checked((uint)source.Height),
            checked((uint)source.Depth),
            AstcencType.AstcencTypeF32,
            MemoryMarshal.AsBytes(rgba.AsSpan()),
            destination);
    }

    private void DecodeUnorm<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new byte[checked(destination.Width * destination.Height * destination.Depth * 4)];
        DecompressImage(
            context.Handle,
            source,
            checked((uint)destination.Width),
            checked((uint)destination.Height),
            checked((uint)destination.Depth),
            AstcencType.AstcencTypeU8,
            rgba);
        CopyFromRgba8(rgba, destination);
    }

    private void DecodeFloat<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination, AstcEncContext context)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new Rgba32Float[checked(destination.Width * destination.Height * destination.Depth)];
        DecompressImage(
            context.Handle,
            source,
            checked((uint)destination.Width),
            checked((uint)destination.Height),
            checked((uint)destination.Depth),
            AstcencType.AstcencTypeF32,
            MemoryMarshal.AsBytes(rgba.AsSpan()));
        for (var i = 0; i < rgba.Length; i++)
        {
            destination.Pixels[i] = TPixel.FromRgba32Float(rgba[i]);
        }
    }

    private AstcEncContext CreateContext(AstcencFlags additionalFlags)
    {
        var flags = GetCommonFlags() | additionalFlags;
        Check(Astcenc.AstcencConfigInit(
            _profile,
            checked((uint)Format.BlockWidth),
            checked((uint)Format.BlockHeight),
            checked((uint)Format.BlockDepth),
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

    private static void CompressImage(
        AstcencContext context,
        uint width,
        uint height,
        uint depth,
        AstcencType dataType,
        Span<byte> imageData,
        Span<byte> destination)
    {
        fixed (byte* imagePtr = imageData)
        {
            var slicePointers = stackalloc byte*[checked((int)depth)];
            FillSlicePointers(slicePointers, imagePtr, width, height, depth, dataType);

            var nativeImage = new AstcencImageUnmanaged
            {
                dimX = width,
                dimY = height,
                dimZ = depth,
                dataType = dataType,
                data = slicePointers
            };

            Check(Astcenc.AstcencCompressImage(context, ref nativeImage, SSwizzle, destination, 0));
        }
    }

    private static void DecompressImage(
        AstcencContext context,
        ReadOnlySpan<byte> source,
        uint width,
        uint height,
        uint depth,
        AstcencType dataType,
        Span<byte> imageData)
    {
        var sourceCopy = source.ToArray();
        fixed (byte* imagePtr = imageData)
        {
            var slicePointers = stackalloc byte*[checked((int)depth)];
            FillSlicePointers(slicePointers, imagePtr, width, height, depth, dataType);

            var nativeImage = new AstcencImageUnmanaged
            {
                dimX = width,
                dimY = height,
                dimZ = depth,
                dataType = dataType,
                data = slicePointers
            };

            Check(Astcenc.AstcencDecompressImage(context, sourceCopy, ref nativeImage, SSwizzle, 0));
        }
    }

    private static void FillSlicePointers(
        byte** slicePointers,
        byte* imageData,
        uint width,
        uint height,
        uint depth,
        AstcencType dataType)
    {
        var sliceByteCount = checked((nuint)width * (nuint)height * GetBytesPerPixel(dataType));
        for (var z = 0; z < depth; z++)
        {
            slicePointers[z] = imageData + checked((nint)(sliceByteCount * (nuint)z));
        }
    }

    private static nuint GetBytesPerPixel(AstcencType type) => type switch
    {
        AstcencType.AstcencTypeU8 => 4,
        AstcencType.AstcencTypeF16 => 8,
        AstcencType.AstcencTypeF32 => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private byte[] CopyToPackedBlocks(ReadOnlySpan<byte> source, int width, int height, int depth, int rowPitch, int slicePitch)
    {
        var rowByteCount = Format.GetRowByteCount(width);
        var blockRows = GetBlockCount(height, Format.BlockHeight);
        var blockSlices = GetBlockCount(depth, Format.BlockDepth);
        var packedSliceByteCount = checked(rowByteCount * blockRows);
        var packedSize = checked(packedSliceByteCount * blockSlices);
        var packed = new byte[packedSize];

        if (rowPitch == rowByteCount && slicePitch == packedSliceByteCount)
        {
            source[..packedSize].CopyTo(packed);
            return packed;
        }

        for (var z = 0; z < blockSlices; z++)
        {
            for (var row = 0; row < blockRows; row++)
            {
                source.Slice(checked((z * slicePitch) + (row * rowPitch)), rowByteCount)
                    .CopyTo(packed.AsSpan(checked((z * packedSliceByteCount) + (row * rowByteCount))));
            }
        }

        return packed;
    }

    private void CopyPackedBlocksToDestination(ReadOnlySpan<byte> packed, int width, int height, int depth, Span<byte> destination, int rowPitch, int slicePitch)
    {
        var rowByteCount = Format.GetRowByteCount(width);
        var blockRows = GetBlockCount(height, Format.BlockHeight);
        var blockSlices = GetBlockCount(depth, Format.BlockDepth);
        var packedSliceByteCount = checked(rowByteCount * blockRows);
        if (rowPitch == rowByteCount && slicePitch == packedSliceByteCount)
        {
            packed.CopyTo(destination);
            return;
        }

        for (var z = 0; z < blockSlices; z++)
        {
            for (var row = 0; row < blockRows; row++)
            {
                packed.Slice(checked((z * packedSliceByteCount) + (row * rowByteCount)), rowByteCount)
                    .CopyTo(destination.Slice(checked((z * slicePitch) + (row * rowPitch)), rowByteCount));
            }
        }
    }

    private void ValidateSourceLength(int width, int height, int depth, ReadOnlySpan<byte> source, int rowPitch, int slicePitch)
    {
        var required = GetEncodedByteCount(width, height, depth, rowPitch, slicePitch);
        if (source.Length < required)
        {
            throw new ArgumentException("Source span is too small for the encoded 3D texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, int depth, Span<byte> destination, int rowPitch, int slicePitch)
    {
        var required = GetEncodedByteCount(width, height, depth, rowPitch, slicePitch);
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination span is too small for the encoded 3D texture.", nameof(destination));
        }
    }

    private int GetPackedByteCount(int width, int height, int depth) =>
        checked(GetDefaultSlicePitch(width, height) * GetBlockCount(depth, Format.BlockDepth));

    private static int GetBlockCount(int value, int blockSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

        return checked((value + blockSize - 1) / blockSize);
    }

    private static byte[] CopyToRgba8<TPixel>(VolumeBitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var result = new byte[checked(source.Width * source.Height * source.Depth * 4)];
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

    private static Rgba32Float[] CopyToRgba32Float<TPixel>(VolumeBitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var result = new Rgba32Float[checked(source.Width * source.Height * source.Depth)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = TPixel.ToRgba32Float(source.Pixels[i]);
        }

        return result;
    }

    private static void CopyFromRgba8<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination)
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
