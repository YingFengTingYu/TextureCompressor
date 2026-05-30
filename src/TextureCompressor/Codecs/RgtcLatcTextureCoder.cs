using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class RgtcLatcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private readonly RgtcLatcLayout _layout;
    private readonly bool _isSigned;
    private readonly RgtcLatcCoderOptions _options;

    public RgtcLatcTextureCoder(TextureFormat format, RgtcLatcCoderOptions? options = null)
    {
        if (!TryGetLayout(format, out _layout, out _isSigned))
        {
            throw CreateUnsupportedFormatException(format);
        }

        _options = options ?? new RgtcLatcCoderOptions();
        Format = format;
    }

    public TextureFormat Format { get; }

    public RgtcLatcCoderOptions Options => _options;

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

        if (_isSigned)
        {
            DecodeSigned(source, destination, rowPitch);
        }
        else
        {
            DecodeUnsigned(source, destination, rowPitch);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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

    private void DecodeUnsigned<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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

    private void DecodeUnsigned<TPixel, TLayout>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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

    private void DecodeSigned<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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

    private void DecodeSigned<TPixel, TLayout>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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

    private void EncodeUnsigned<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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

    private void EncodeUnsigned<TPixel, TLayout>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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
                EncodeUnsignedBlock<TLayout>(block, encodedBlock, _options.CompressionMode);
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeSigned<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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

    private void EncodeSigned<TPixel, TLayout>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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
                EncodeSignedBlock<TLayout>(block, encodedBlock, _options.CompressionMode);
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

    private static void EncodeUnsignedBlock<TLayout>(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        RgtcLatcCompressionMode compressionMode)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        EncodeUNormFirstComponentBlock<TLayout>(source, destination[..8], compressionMode);

        if (TLayout.HasSecondComponent)
        {
            EncodeUNormSecondComponentBlock<TLayout>(source, destination[8..], compressionMode);
        }
    }

    private static void EncodeSignedBlock<TLayout>(
        ReadOnlySpan<Rgba8SNorm> source,
        Span<byte> destination,
        RgtcLatcCompressionMode compressionMode)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        EncodeSNormFirstComponentBlock<TLayout>(source, destination[..8], compressionMode);

        if (TLayout.HasSecondComponent)
        {
            EncodeSNormSecondComponentBlock<TLayout>(source, destination[8..], compressionMode);
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

    private static void EncodeUNormFirstComponentBlock<TLayout>(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        RgtcLatcCompressionMode compressionMode)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<int> values = stackalloc int[TexelsPerBlock];
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            values[i] = TLayout.GetFirstComponent(source[i]);
        }

        EncodeScalarBlock(values, byte.MinValue, byte.MaxValue, compressionMode, destination);
    }

    private static void EncodeUNormSecondComponentBlock<TLayout>(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        RgtcLatcCompressionMode compressionMode)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<int> values = stackalloc int[TexelsPerBlock];
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            values[i] = TLayout.GetSecondComponent(source[i]);
        }

        EncodeScalarBlock(values, byte.MinValue, byte.MaxValue, compressionMode, destination);
    }

    private static void EncodeSNormFirstComponentBlock<TLayout>(
        ReadOnlySpan<Rgba8SNorm> source,
        Span<byte> destination,
        RgtcLatcCompressionMode compressionMode)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<int> values = stackalloc int[TexelsPerBlock];
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            values[i] = CanonicalSNorm(TLayout.GetFirstComponent(source[i]));
        }

        EncodeScalarBlock(values, -sbyte.MaxValue, sbyte.MaxValue, compressionMode, destination);
    }

    private static void EncodeSNormSecondComponentBlock<TLayout>(
        ReadOnlySpan<Rgba8SNorm> source,
        Span<byte> destination,
        RgtcLatcCompressionMode compressionMode)
        where TLayout : IRgtcLatcLayoutTransfer
    {
        Span<int> values = stackalloc int[TexelsPerBlock];
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            values[i] = CanonicalSNorm(TLayout.GetSecondComponent(source[i]));
        }

        EncodeScalarBlock(values, -sbyte.MaxValue, sbyte.MaxValue, compressionMode, destination);
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

    private static void EncodeScalarBlock(
        ReadOnlySpan<int> source,
        int minValue,
        int maxValue,
        RgtcLatcCompressionMode compressionMode,
        Span<byte> destination)
    {
        switch (compressionMode)
        {
            case RgtcLatcCompressionMode.Fast:
                EncodeScalarBlockFast(source, minValue, maxValue, destination);
                return;
            case RgtcLatcCompressionMode.Normal:
                EncodeScalarBlockOptimized(source, minValue, maxValue, highQuality: false, destination);
                return;
            case RgtcLatcCompressionMode.High:
                EncodeScalarBlockOptimized(source, minValue, maxValue, highQuality: true, destination);
                return;
            case RgtcLatcCompressionMode.Exhaustive:
                EncodeScalarBlockExhaustive(source, minValue, maxValue, destination);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported RGTC/LATC compression mode.");
        }
    }

    private static void EncodeScalarBlockFast(
        ReadOnlySpan<int> source,
        int minValue,
        int maxValue,
        Span<byte> destination)
    {
        FindScalarBounds(source, out var min, out var max);
        var encoding = EvaluateScalarCandidate(source, max, min, minValue, maxValue, long.MaxValue);
        WriteScalarBlock(encoding, minValue, destination);
    }

    private static void EncodeScalarBlockOptimized(
        ReadOnlySpan<int> source,
        int minValue,
        int maxValue,
        bool highQuality,
        Span<byte> destination)
    {
        FindScalarBounds(source, out var min, out var max);
        var best = EvaluateScalarCandidate(source, max, min, minValue, maxValue, long.MaxValue);
        var iterationLimit = highQuality ? 8 : 4;

        OptimizeScalarSeed(source, min, max, ScalarEndpointMode.SixValue, minValue, maxValue, iterationLimit, ref best);
        if (max > min)
        {
            OptimizeScalarSeed(source, max, min, ScalarEndpointMode.EightValue, minValue, maxValue, iterationLimit, ref best);

            var padding = Math.Max(1, (max - min + 13) / 14);
            var expandedMin = Math.Max(minValue, min - padding);
            var expandedMax = Math.Min(maxValue, max + padding);
            OptimizeScalarSeed(source, expandedMin, expandedMax, ScalarEndpointMode.SixValue, minValue, maxValue, iterationLimit, ref best);
            OptimizeScalarSeed(source, expandedMax, expandedMin, ScalarEndpointMode.EightValue, minValue, maxValue, iterationLimit, ref best);
        }

        if (highQuality)
        {
            OptimizeUniqueScalarSeeds(source, minValue, maxValue, ref best);
        }

        RefineScalarEndpoints(source, minValue, maxValue, highQuality ? 8 : 4, ref best);
        WriteScalarBlock(best, minValue, destination);
    }

    private static void EncodeScalarBlockExhaustive(
        ReadOnlySpan<int> source,
        int minValue,
        int maxValue,
        Span<byte> destination)
    {
        var best = new ScalarBlockEncoding { Error = long.MaxValue };
        for (var endpoint0 = minValue; endpoint0 <= maxValue; endpoint0++)
        {
            for (var endpoint1 = minValue; endpoint1 <= maxValue; endpoint1++)
            {
                var candidate = EvaluateScalarCandidate(source, endpoint0, endpoint1, minValue, maxValue, best.Error);
                UpdateBestScalarEncoding(candidate, ref best);
                if (best.Error == 0)
                {
                    WriteScalarBlock(best, minValue, destination);
                    return;
                }
            }
        }

        WriteScalarBlock(best, minValue, destination);
    }

    private static void OptimizeScalarSeed(
        ReadOnlySpan<int> source,
        int endpoint0,
        int endpoint1,
        ScalarEndpointMode mode,
        int minValue,
        int maxValue,
        int iterationLimit,
        ref ScalarBlockEncoding best)
    {
        NormalizeScalarOrder(ref endpoint0, ref endpoint1, mode, minValue, maxValue);

        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateScalarCandidate(source, endpoint0, endpoint1, minValue, maxValue, long.MaxValue);
            UpdateBestScalarEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveScalarEndpoints(source, mode, current.Indices, minValue, maxValue, out var nextEndpoint0, out var nextEndpoint1))
            {
                return;
            }

            if (nextEndpoint0 == endpoint0 && nextEndpoint1 == endpoint1)
            {
                return;
            }

            endpoint0 = nextEndpoint0;
            endpoint1 = nextEndpoint1;
        }
    }

    private static void OptimizeUniqueScalarSeeds(
        ReadOnlySpan<int> source,
        int minValue,
        int maxValue,
        ref ScalarBlockEncoding best)
    {
        Span<int> uniqueValues = stackalloc int[TexelsPerBlock];
        var uniqueCount = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = source[i];
            var alreadyAdded = false;
            for (var j = 0; j < uniqueCount; j++)
            {
                if (uniqueValues[j] == value)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                uniqueValues[uniqueCount++] = value;
            }
        }

        for (var i = 0; i < uniqueCount; i++)
        {
            for (var j = 0; j < uniqueCount; j++)
            {
                OptimizeScalarSeed(source, uniqueValues[i], uniqueValues[j], ScalarEndpointMode.SixValue, minValue, maxValue, 8, ref best);
                OptimizeScalarSeed(source, uniqueValues[i], uniqueValues[j], ScalarEndpointMode.EightValue, minValue, maxValue, 8, ref best);
                if (best.Error == 0)
                {
                    return;
                }
            }
        }
    }

    private static ScalarBlockEncoding EvaluateScalarCandidate(
        ReadOnlySpan<int> source,
        int endpoint0,
        int endpoint1,
        int minValue,
        int maxValue,
        long maxError)
    {
        Span<int> palette = stackalloc int[8];
        BuildScalarPalette(endpoint0, endpoint1, minValue, maxValue, palette);

        ulong indices = 0;
        long error = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = source[i];
            var index = FindNearestScalarIndex(value, palette);
            var difference = value - palette[index];
            error += difference * difference;
            indices |= (ulong)index << (i * 3);
            if (error >= maxError)
            {
                return new ScalarBlockEncoding
                {
                    Endpoint0 = endpoint0,
                    Endpoint1 = endpoint1,
                    Indices = indices,
                    Error = error
                };
            }
        }

        return new ScalarBlockEncoding
        {
            Endpoint0 = endpoint0,
            Endpoint1 = endpoint1,
            Indices = indices,
            Error = error
        };
    }

    private static bool TrySolveScalarEndpoints(
        ReadOnlySpan<int> source,
        ScalarEndpointMode mode,
        ulong indices,
        int minValue,
        int maxValue,
        out int endpoint0,
        out int endpoint1)
    {
        var a00 = 0d;
        var a01 = 0d;
        var a11 = 0d;
        var b0 = 0d;
        var b1 = 0d;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)((indices >> (i * 3)) & 0x7u);
            if (!TryGetScalarEndpointWeights(index, mode, out var weight0, out var weight1))
            {
                continue;
            }

            var value = source[i];
            a00 += weight0 * weight0;
            a01 += weight0 * weight1;
            a11 += weight1 * weight1;
            b0 += weight0 * value;
            b1 += weight1 * value;
        }

        var determinant = (a00 * a11) - (a01 * a01);
        if (Math.Abs(determinant) < 0.000001d)
        {
            endpoint0 = 0;
            endpoint1 = 0;
            return false;
        }

        endpoint0 = ClampToScalar(((b0 * a11) - (b1 * a01)) / determinant, minValue, maxValue);
        endpoint1 = ClampToScalar(((a00 * b1) - (a01 * b0)) / determinant, minValue, maxValue);
        NormalizeScalarOrder(ref endpoint0, ref endpoint1, mode, minValue, maxValue);
        return true;
    }

    private static bool TryGetScalarEndpointWeights(
        int index,
        ScalarEndpointMode mode,
        out double weight0,
        out double weight1)
    {
        if (mode == ScalarEndpointMode.EightValue)
        {
            switch (index)
            {
                case 0:
                    weight0 = 1d;
                    weight1 = 0d;
                    return true;
                case 1:
                    weight0 = 0d;
                    weight1 = 1d;
                    return true;
                default:
                    weight0 = (8d - index) / 7d;
                    weight1 = (index - 1d) / 7d;
                    return true;
            }
        }

        switch (index)
        {
            case 0:
                weight0 = 1d;
                weight1 = 0d;
                return true;
            case 1:
                weight0 = 0d;
                weight1 = 1d;
                return true;
            case >= 2 and <= 5:
                weight0 = (6d - index) / 5d;
                weight1 = (index - 1d) / 5d;
                return true;
            default:
                weight0 = 0d;
                weight1 = 0d;
                return false;
        }
    }

    private static void RefineScalarEndpoints(
        ReadOnlySpan<int> source,
        int minValue,
        int maxValue,
        int passLimit,
        ref ScalarBlockEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var mode = best.Endpoint0 > best.Endpoint1
                ? ScalarEndpointMode.EightValue
                : ScalarEndpointMode.SixValue;
            var improved = false;
            improved |= TryRefineScalarEndpoint(source, mode, minValue, maxValue, endpointIndex: 0, delta: -1, ref best);
            improved |= TryRefineScalarEndpoint(source, mode, minValue, maxValue, endpointIndex: 0, delta: 1, ref best);
            improved |= TryRefineScalarEndpoint(source, mode, minValue, maxValue, endpointIndex: 1, delta: -1, ref best);
            improved |= TryRefineScalarEndpoint(source, mode, minValue, maxValue, endpointIndex: 1, delta: 1, ref best);
            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private static bool TryRefineScalarEndpoint(
        ReadOnlySpan<int> source,
        ScalarEndpointMode mode,
        int minValue,
        int maxValue,
        int endpointIndex,
        int delta,
        ref ScalarBlockEncoding best)
    {
        var endpoint0 = best.Endpoint0;
        var endpoint1 = best.Endpoint1;
        if (endpointIndex == 0)
        {
            if (!TryOffsetScalarEndpoint(ref endpoint0, delta, minValue, maxValue))
            {
                return false;
            }
        }
        else if (!TryOffsetScalarEndpoint(ref endpoint1, delta, minValue, maxValue))
        {
            return false;
        }

        NormalizeScalarOrder(ref endpoint0, ref endpoint1, mode, minValue, maxValue);
        if (endpoint0 == best.Endpoint0 && endpoint1 == best.Endpoint1)
        {
            return false;
        }

        var candidate = EvaluateScalarCandidate(source, endpoint0, endpoint1, minValue, maxValue, best.Error);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private static void BuildScalarPalette(int value0, int value1, int minValue, int maxValue, Span<int> palette)
    {
        palette[0] = value0;
        palette[1] = value1;

        if (value0 > value1)
        {
            palette[2] = ((6 * value0) + value1) / 7;
            palette[3] = ((5 * value0) + (2 * value1)) / 7;
            palette[4] = ((4 * value0) + (3 * value1)) / 7;
            palette[5] = ((3 * value0) + (4 * value1)) / 7;
            palette[6] = ((2 * value0) + (5 * value1)) / 7;
            palette[7] = (value0 + (6 * value1)) / 7;
        }
        else
        {
            palette[2] = ((4 * value0) + value1) / 5;
            palette[3] = ((3 * value0) + (2 * value1)) / 5;
            palette[4] = ((2 * value0) + (3 * value1)) / 5;
            palette[5] = (value0 + (4 * value1)) / 5;
            palette[6] = minValue;
            palette[7] = maxValue;
        }
    }

    private static void FindScalarBounds(ReadOnlySpan<int> source, out int min, out int max)
    {
        min = int.MaxValue;
        max = int.MinValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            min = Math.Min(min, source[i]);
            max = Math.Max(max, source[i]);
        }
    }

    private static int FindNearestScalarIndex(int value, ReadOnlySpan<int> palette)
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

    private static void NormalizeScalarOrder(
        ref int endpoint0,
        ref int endpoint1,
        ScalarEndpointMode mode,
        int minValue,
        int maxValue)
    {
        if (mode == ScalarEndpointMode.EightValue)
        {
            if (endpoint0 < endpoint1)
            {
                (endpoint0, endpoint1) = (endpoint1, endpoint0);
            }
            else if (endpoint0 == endpoint1)
            {
                if (endpoint0 < maxValue)
                {
                    endpoint0++;
                }
                else
                {
                    endpoint1--;
                }
            }

            return;
        }

        if (endpoint0 > endpoint1)
        {
            (endpoint0, endpoint1) = (endpoint1, endpoint0);
        }
    }

    private static bool TryOffsetScalarEndpoint(ref int endpoint, int delta, int minValue, int maxValue)
    {
        var next = endpoint + delta;
        if (next < minValue || next > maxValue)
        {
            return false;
        }

        endpoint = next;
        return true;
    }

    private static int ClampToScalar(double value, int minValue, int maxValue) =>
        Math.Clamp((int)Math.Round(value), minValue, maxValue);

    private static void UpdateBestScalarEncoding(ScalarBlockEncoding candidate, ref ScalarBlockEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static void WriteScalarBlock(ScalarBlockEncoding encoding, int minValue, Span<byte> destination)
    {
        destination[0] = EncodeScalarEndpoint(encoding.Endpoint0, minValue);
        destination[1] = EncodeScalarEndpoint(encoding.Endpoint1, minValue);
        WriteIndices(encoding.Indices, destination);
    }

    private static byte EncodeScalarEndpoint(int value, int minValue) =>
        minValue < 0 ? unchecked((byte)(sbyte)value) : (byte)value;

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
        BitmapView<TPixel> source,
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
        BitmapView<TPixel> source,
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
        BitmapView<TPixel> destination)
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
        BitmapView<TPixel> destination)
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

    private struct ScalarBlockEncoding
    {
        public int Endpoint0;
        public int Endpoint1;
        public ulong Indices;
        public long Error;
    }

    private enum ScalarEndpointMode
    {
        SixValue,
        EightValue
    }

}
