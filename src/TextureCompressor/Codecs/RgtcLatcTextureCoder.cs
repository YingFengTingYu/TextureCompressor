using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class RgtcLatcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private readonly RgtcLatcLayout _layout;
    private readonly bool _isSigned;

    public RgtcLatcTextureCoder(TextureFormat format)
    {
        if (!TryGetLayout(format, out _layout, out _isSigned))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        if (_isSigned)
        {
            DecodeSigned(source, destination, rowPitch);
        }
        else
        {
            DecodeUnsigned(source, destination, rowPitch);
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        if (_isSigned)
        {
            EncodeSigned(source, destination, rowPitch);
        }
        else
        {
            EncodeUnsigned(source, destination, rowPitch);
        }
    }

    private void DecodeUnsigned<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_layout)
        {
            case RgtcLatcLayout.R:
                DecodeUnsigned<TPixel, RLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Rg:
                DecodeUnsigned<TPixel, RgLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Luminance:
                DecodeUnsigned<TPixel, LuminanceLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.LuminanceAlpha:
                DecodeUnsigned<TPixel, LuminanceAlphaLayout>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void DecodeUnsigned<TPixel, TLayout>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TLayout : IRgtcLatcLayoutTransfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        var bytesPerBlock = Format.BytesPerBlock;
        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var encodedBlock = source.Slice(blockOffset, bytesPerBlock);
                DecodeUnsignedBlock<TLayout>(encodedBlock, block);
                StoreUnsignedBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeSigned<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_layout)
        {
            case RgtcLatcLayout.R:
                DecodeSigned<TPixel, RLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Rg:
                DecodeSigned<TPixel, RgLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Luminance:
                DecodeSigned<TPixel, LuminanceLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.LuminanceAlpha:
                DecodeSigned<TPixel, LuminanceAlphaLayout>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void DecodeSigned<TPixel, TLayout>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TLayout : IRgtcLatcLayoutTransfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        var bytesPerBlock = Format.BytesPerBlock;
        Span<Rgba8SNorm> block = stackalloc Rgba8SNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var encodedBlock = source.Slice(blockOffset, bytesPerBlock);
                DecodeSignedBlock<TLayout>(encodedBlock, block);
                StoreSignedBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeUnsigned<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_layout)
        {
            case RgtcLatcLayout.R:
                EncodeUnsigned<TPixel, RLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Rg:
                EncodeUnsigned<TPixel, RgLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Luminance:
                EncodeUnsigned<TPixel, LuminanceLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.LuminanceAlpha:
                EncodeUnsigned<TPixel, LuminanceAlphaLayout>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeUnsigned<TPixel, TLayout>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TLayout : IRgtcLatcLayoutTransfer
    {
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);
        var bytesPerBlock = Format.BytesPerBlock;
        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadUnsignedBlock(source, blockX, blockY, block);
                var encodedBlock = destination.Slice(blockOffset, bytesPerBlock);
                EncodeUnsignedBlock<TLayout>(block, encodedBlock);
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeSigned<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_layout)
        {
            case RgtcLatcLayout.R:
                EncodeSigned<TPixel, RLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Rg:
                EncodeSigned<TPixel, RgLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.Luminance:
                EncodeSigned<TPixel, LuminanceLayout>(source, destination, rowPitch);
                return;
            case RgtcLatcLayout.LuminanceAlpha:
                EncodeSigned<TPixel, LuminanceAlphaLayout>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeSigned<TPixel, TLayout>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TLayout : IRgtcLatcLayoutTransfer
    {
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);
        var bytesPerBlock = Format.BytesPerBlock;
        Span<Rgba8SNorm> block = stackalloc Rgba8SNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadSignedBlock(source, blockX, blockY, block);
                var encodedBlock = destination.Slice(blockOffset, bytesPerBlock);
                EncodeSignedBlock<TLayout>(block, encodedBlock);
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void DecodeUnsignedBlock<TLayout>(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        InitializeUnsignedBlock(destination);
        DecodeUNormFirstComponentBlock<TLayout>(source[..8], destination);

        if (TLayout.HasSecondComponent)
        {
            DecodeUNormSecondComponentBlock<TLayout>(source[8..], destination);
        }
    }

    private static void DecodeSignedBlock<TLayout>(ReadOnlySpan<byte> source, Span<Rgba8SNorm> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        InitializeSignedBlock(destination);
        DecodeSNormFirstComponentBlock<TLayout>(source[..8], destination);

        if (TLayout.HasSecondComponent)
        {
            DecodeSNormSecondComponentBlock<TLayout>(source[8..], destination);
        }
    }

    private static void EncodeUnsignedBlock<TLayout>(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        EncodeUNormFirstComponentBlock<TLayout>(source, destination[..8]);

        if (TLayout.HasSecondComponent)
        {
            EncodeUNormSecondComponentBlock<TLayout>(source, destination[8..]);
        }
    }

    private static void EncodeSignedBlock<TLayout>(ReadOnlySpan<Rgba8SNorm> source, Span<byte> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        EncodeSNormFirstComponentBlock<TLayout>(source, destination[..8]);

        if (TLayout.HasSecondComponent)
        {
            EncodeSNormSecondComponentBlock<TLayout>(source, destination[8..]);
        }
    }

    private static void DecodeUNormFirstComponentBlock<TLayout>(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<byte> palette = stackalloc byte[8];
        BuildUNormPalette(source[0], source[1], palette);

        var indices = ReadIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            TLayout.SetFirstComponent(ref destination[i], palette[(int)((indices >> (i * 3)) & 0x7u)]);
        }
    }

    private static void DecodeUNormSecondComponentBlock<TLayout>(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<byte> palette = stackalloc byte[8];
        BuildUNormPalette(source[0], source[1], palette);

        var indices = ReadIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            TLayout.SetSecondComponent(ref destination[i], palette[(int)((indices >> (i * 3)) & 0x7u)]);
        }
    }

    private static void DecodeSNormFirstComponentBlock<TLayout>(ReadOnlySpan<byte> source, Span<Rgba8SNorm> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<sbyte> palette = stackalloc sbyte[8];
        BuildSNormPalette(ReadSNormEndpoint(source[0]), ReadSNormEndpoint(source[1]), palette);

        var indices = ReadIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            TLayout.SetFirstComponent(ref destination[i], palette[(int)((indices >> (i * 3)) & 0x7u)]);
        }
    }

    private static void DecodeSNormSecondComponentBlock<TLayout>(ReadOnlySpan<byte> source, Span<Rgba8SNorm> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<sbyte> palette = stackalloc sbyte[8];
        BuildSNormPalette(ReadSNormEndpoint(source[0]), ReadSNormEndpoint(source[1]), palette);

        var indices = ReadIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            TLayout.SetSecondComponent(ref destination[i], palette[(int)((indices >> (i * 3)) & 0x7u)]);
        }
    }

    private static void EncodeUNormFirstComponentBlock<TLayout>(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        FindUNormFirstBounds<TLayout>(source, out var min, out var max);
        destination[0] = max;
        destination[1] = min;

        Span<byte> palette = stackalloc byte[8];
        BuildUNormPalette(max, min, palette);

        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            indices |= (ulong)FindNearestUNormIndex(TLayout.GetFirstComponent(source[i]), palette) << (i * 3);
        }

        WriteIndices(indices, destination);
    }

    private static void EncodeUNormSecondComponentBlock<TLayout>(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        FindUNormSecondBounds<TLayout>(source, out var min, out var max);
        destination[0] = max;
        destination[1] = min;

        Span<byte> palette = stackalloc byte[8];
        BuildUNormPalette(max, min, palette);

        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            indices |= (ulong)FindNearestUNormIndex(TLayout.GetSecondComponent(source[i]), palette) << (i * 3);
        }

        WriteIndices(indices, destination);
    }

    private static void EncodeSNormFirstComponentBlock<TLayout>(ReadOnlySpan<Rgba8SNorm> source, Span<byte> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        FindSNormFirstBounds<TLayout>(source, out var min, out var max);
        destination[0] = unchecked((byte)max);
        destination[1] = unchecked((byte)min);

        Span<sbyte> palette = stackalloc sbyte[8];
        BuildSNormPalette(max, min, palette);

        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = CanonicalSNorm(TLayout.GetFirstComponent(source[i]));
            indices |= (ulong)FindNearestSNormIndex(value, palette) << (i * 3);
        }

        WriteIndices(indices, destination);
    }

    private static void EncodeSNormSecondComponentBlock<TLayout>(ReadOnlySpan<Rgba8SNorm> source, Span<byte> destination)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        FindSNormSecondBounds<TLayout>(source, out var min, out var max);
        destination[0] = unchecked((byte)max);
        destination[1] = unchecked((byte)min);

        Span<sbyte> palette = stackalloc sbyte[8];
        BuildSNormPalette(max, min, palette);

        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = CanonicalSNorm(TLayout.GetSecondComponent(source[i]));
            indices |= (ulong)FindNearestSNormIndex(value, palette) << (i * 3);
        }

        WriteIndices(indices, destination);
    }

    private static void BuildUNormPalette(byte value0, byte value1, Span<byte> palette)
    {
        palette[0] = value0;
        palette[1] = value1;

        if (value0 > value1)
        {
            palette[2] = (byte)(((6 * value0) + value1) / 7);
            palette[3] = (byte)(((5 * value0) + (2 * value1)) / 7);
            palette[4] = (byte)(((4 * value0) + (3 * value1)) / 7);
            palette[5] = (byte)(((3 * value0) + (4 * value1)) / 7);
            palette[6] = (byte)(((2 * value0) + (5 * value1)) / 7);
            palette[7] = (byte)((value0 + (6 * value1)) / 7);
        }
        else
        {
            palette[2] = (byte)(((4 * value0) + value1) / 5);
            palette[3] = (byte)(((3 * value0) + (2 * value1)) / 5);
            palette[4] = (byte)(((2 * value0) + (3 * value1)) / 5);
            palette[5] = (byte)((value0 + (4 * value1)) / 5);
            palette[6] = byte.MinValue;
            palette[7] = byte.MaxValue;
        }
    }

    private static void BuildSNormPalette(sbyte value0, sbyte value1, Span<sbyte> palette)
    {
        palette[0] = value0;
        palette[1] = value1;

        if (value0 > value1)
        {
            palette[2] = InterpolateSNorm(value0, value1, 6, 1, 7);
            palette[3] = InterpolateSNorm(value0, value1, 5, 2, 7);
            palette[4] = InterpolateSNorm(value0, value1, 4, 3, 7);
            palette[5] = InterpolateSNorm(value0, value1, 3, 4, 7);
            palette[6] = InterpolateSNorm(value0, value1, 2, 5, 7);
            palette[7] = InterpolateSNorm(value0, value1, 1, 6, 7);
        }
        else
        {
            palette[2] = InterpolateSNorm(value0, value1, 4, 1, 5);
            palette[3] = InterpolateSNorm(value0, value1, 3, 2, 5);
            palette[4] = InterpolateSNorm(value0, value1, 2, 3, 5);
            palette[5] = InterpolateSNorm(value0, value1, 1, 4, 5);
            palette[6] = -sbyte.MaxValue;
            palette[7] = sbyte.MaxValue;
        }
    }

    private static sbyte InterpolateSNorm(sbyte value0, sbyte value1, int weight0, int weight1, int divisor) =>
        (sbyte)(((weight0 * value0) + (weight1 * value1)) / divisor);

    private static ulong ReadIndices(ReadOnlySpan<byte> source)
    {
        ulong indices = 0;
        for (var i = 0; i < 6; i++)
        {
            indices |= (ulong)source[2 + i] << (8 * i);
        }

        return indices;
    }

    private static void WriteIndices(ulong indices, Span<byte> destination)
    {
        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(indices >> (8 * i));
        }
    }

    private static void FindUNormFirstBounds<TLayout>(
        ReadOnlySpan<Rgba8UNorm> source,
        out byte min,
        out byte max)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        min = byte.MaxValue;
        max = byte.MinValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = TLayout.GetFirstComponent(source[i]);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private static void FindUNormSecondBounds<TLayout>(
        ReadOnlySpan<Rgba8UNorm> source,
        out byte min,
        out byte max)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        min = byte.MaxValue;
        max = byte.MinValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = TLayout.GetSecondComponent(source[i]);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private static void FindSNormFirstBounds<TLayout>(
        ReadOnlySpan<Rgba8SNorm> source,
        out sbyte min,
        out sbyte max)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        min = sbyte.MaxValue;
        max = -sbyte.MaxValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = CanonicalSNorm(TLayout.GetFirstComponent(source[i]));
            min = (sbyte)Math.Min(min, value);
            max = (sbyte)Math.Max(max, value);
        }
    }

    private static void FindSNormSecondBounds<TLayout>(
        ReadOnlySpan<Rgba8SNorm> source,
        out sbyte min,
        out sbyte max)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        min = sbyte.MaxValue;
        max = -sbyte.MaxValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = CanonicalSNorm(TLayout.GetSecondComponent(source[i]));
            min = (sbyte)Math.Min(min, value);
            max = (sbyte)Math.Max(max, value);
        }
    }

    private static int FindNearestUNormIndex(byte value, ReadOnlySpan<byte> palette)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var distance = Math.Abs(value - palette[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int FindNearestSNormIndex(sbyte value, ReadOnlySpan<sbyte> palette)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var distance = Math.Abs(value - palette[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static void InitializeUnsignedBlock(Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba8UNorm(0, 0, 0);
        }
    }

    private static void InitializeSignedBlock(Span<Rgba8SNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba8SNorm(0, 0, 0);
        }
    }

    private static sbyte ReadSNormEndpoint(byte value) => CanonicalSNorm(unchecked((sbyte)value));

    private static sbyte CanonicalSNorm(sbyte value) =>
        value == sbyte.MinValue ? (sbyte)-sbyte.MaxValue : value;

    private static void LoadUnsignedBlock<TPixel>(
        ImageView<TPixel> source,
        int blockX,
        int blockY,
        Span<Rgba8UNorm> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        var lastSourceX = source.Width - 1;
        var blockOffset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            var sourceX = originX;
            for (var x = 0; x < BlockSize; x++)
            {
                destination[blockOffset++] = TPixel.ToRgba8UNorm(sourceRow[Math.Min(sourceX, lastSourceX)]);
                sourceX++;
            }
        }
    }

    private static void LoadSignedBlock<TPixel>(
        ImageView<TPixel> source,
        int blockX,
        int blockY,
        Span<Rgba8SNorm> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        var lastSourceX = source.Width - 1;
        var blockOffset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            var sourceX = originX;
            for (var x = 0; x < BlockSize; x++)
            {
                destination[blockOffset++] = TPixel.ToRgba8SNorm(sourceRow[Math.Min(sourceX, lastSourceX)]);
                sourceX++;
            }
        }
    }

    private static void StoreUnsignedBlock<TPixel>(
        ReadOnlySpan<Rgba8UNorm> block,
        int blockX,
        int blockY,
        ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        var blockOffset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            var destinationX = originX;
            var rowBlockOffset = blockOffset;
            for (var x = 0; x < BlockSize; x++)
            {
                if (destinationX >= destination.Width)
                {
                    break;
                }

                destinationRow[destinationX] = TPixel.FromRgba8UNorm(block[rowBlockOffset++]);
                destinationX++;
            }

            blockOffset += BlockSize;
        }
    }

    private static void StoreSignedBlock<TPixel>(
        ReadOnlySpan<Rgba8SNorm> block,
        int blockX,
        int blockY,
        ImageView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        var blockOffset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            var destinationX = originX;
            var rowBlockOffset = blockOffset;
            for (var x = 0; x < BlockSize; x++)
            {
                if (destinationX >= destination.Width)
                {
                    break;
                }

                destinationRow[destinationX] = TPixel.FromRgba8SNorm(block[rowBlockOffset++]);
                destinationX++;
            }

            blockOffset += BlockSize;
        }
    }

    private interface IRgtcLatcLayoutTransfer
    {
        static abstract bool HasSecondComponent { get; }

        static abstract byte GetFirstComponent(Rgba8UNorm pixel);

        static abstract byte GetSecondComponent(Rgba8UNorm pixel);

        static abstract sbyte GetFirstComponent(Rgba8SNorm pixel);

        static abstract sbyte GetSecondComponent(Rgba8SNorm pixel);

        static abstract void SetFirstComponent(ref Rgba8UNorm pixel, byte value);

        static abstract void SetSecondComponent(ref Rgba8UNorm pixel, byte value);

        static abstract void SetFirstComponent(ref Rgba8SNorm pixel, sbyte value);

        static abstract void SetSecondComponent(ref Rgba8SNorm pixel, sbyte value);
    }

    private readonly struct RLayout : IRgtcLatcLayoutTransfer
    {
        public static bool HasSecondComponent => false;

        public static byte GetFirstComponent(Rgba8UNorm pixel) => pixel.Red;

        public static byte GetSecondComponent(Rgba8UNorm pixel) => throw CreateMissingSecondComponentException(nameof(RLayout));

        public static sbyte GetFirstComponent(Rgba8SNorm pixel) => pixel.Red;

        public static sbyte GetSecondComponent(Rgba8SNorm pixel) => throw CreateMissingSecondComponentException(nameof(RLayout));

        public static void SetFirstComponent(ref Rgba8UNorm pixel, byte value) => pixel.Red = value;

        public static void SetSecondComponent(ref Rgba8UNorm pixel, byte value) => throw CreateMissingSecondComponentException(nameof(RLayout));

        public static void SetFirstComponent(ref Rgba8SNorm pixel, sbyte value) => pixel.Red = value;

        public static void SetSecondComponent(ref Rgba8SNorm pixel, sbyte value) => throw CreateMissingSecondComponentException(nameof(RLayout));
    }

    private readonly struct RgLayout : IRgtcLatcLayoutTransfer
    {
        public static bool HasSecondComponent => true;

        public static byte GetFirstComponent(Rgba8UNorm pixel) => pixel.Red;

        public static byte GetSecondComponent(Rgba8UNorm pixel) => pixel.Green;

        public static sbyte GetFirstComponent(Rgba8SNorm pixel) => pixel.Red;

        public static sbyte GetSecondComponent(Rgba8SNorm pixel) => pixel.Green;

        public static void SetFirstComponent(ref Rgba8UNorm pixel, byte value) => pixel.Red = value;

        public static void SetSecondComponent(ref Rgba8UNorm pixel, byte value) => pixel.Green = value;

        public static void SetFirstComponent(ref Rgba8SNorm pixel, sbyte value) => pixel.Red = value;

        public static void SetSecondComponent(ref Rgba8SNorm pixel, sbyte value) => pixel.Green = value;
    }

    private readonly struct LuminanceLayout : IRgtcLatcLayoutTransfer
    {
        public static bool HasSecondComponent => false;

        public static byte GetFirstComponent(Rgba8UNorm pixel) => pixel.Red;

        public static byte GetSecondComponent(Rgba8UNorm pixel) => throw CreateMissingSecondComponentException(nameof(LuminanceLayout));

        public static sbyte GetFirstComponent(Rgba8SNorm pixel) => pixel.Red;

        public static sbyte GetSecondComponent(Rgba8SNorm pixel) => throw CreateMissingSecondComponentException(nameof(LuminanceLayout));

        public static void SetFirstComponent(ref Rgba8UNorm pixel, byte value)
        {
            pixel.Red = value;
            pixel.Green = value;
            pixel.Blue = value;
        }

        public static void SetSecondComponent(ref Rgba8UNorm pixel, byte value) =>
            throw CreateMissingSecondComponentException(nameof(LuminanceLayout));

        public static void SetFirstComponent(ref Rgba8SNorm pixel, sbyte value)
        {
            pixel.Red = value;
            pixel.Green = value;
            pixel.Blue = value;
        }

        public static void SetSecondComponent(ref Rgba8SNorm pixel, sbyte value) =>
            throw CreateMissingSecondComponentException(nameof(LuminanceLayout));
    }

    private readonly struct LuminanceAlphaLayout : IRgtcLatcLayoutTransfer
    {
        public static bool HasSecondComponent => true;

        public static byte GetFirstComponent(Rgba8UNorm pixel) => pixel.Red;

        public static byte GetSecondComponent(Rgba8UNorm pixel) => pixel.Alpha;

        public static sbyte GetFirstComponent(Rgba8SNorm pixel) => pixel.Red;

        public static sbyte GetSecondComponent(Rgba8SNorm pixel) => pixel.Alpha;

        public static void SetFirstComponent(ref Rgba8UNorm pixel, byte value)
        {
            pixel.Red = value;
            pixel.Green = value;
            pixel.Blue = value;
        }

        public static void SetSecondComponent(ref Rgba8UNorm pixel, byte value) => pixel.Alpha = value;

        public static void SetFirstComponent(ref Rgba8SNorm pixel, sbyte value)
        {
            pixel.Red = value;
            pixel.Green = value;
            pixel.Blue = value;
        }

        public static void SetSecondComponent(ref Rgba8SNorm pixel, sbyte value) => pixel.Alpha = value;
    }

    private static InvalidOperationException CreateMissingSecondComponentException(string layout) =>
        new($"{layout} RGTC/LATC layout does not have a second component.");

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
