using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class AstcTextureCoder : IPitchTextureCoder
{
    private const int BytesPerBlock = 16;
    private const int MaxBlockWidth = 12;
    private const int MaxBlockHeight = 12;
    private const int MaxTexelsPerBlock = MaxBlockWidth * MaxBlockHeight;
    private const int MaxWeightValues = 64;
    private const ushort LnsOne = 0x7800;

    private static readonly int[] STritEncodings = CreateTritEncodings();
    private static readonly int[] SQuintEncodings = CreateQuintEncodings();

    private static readonly AstcFormatInfo[] SSupportedFormatInfo =
    [
        new(TextureFormats.RgbaAstc4x4UNorm, 4, 4, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc4x4Srgb, 4, 4, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc4x4Float, 4, 4, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc5x4UNorm, 5, 4, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc5x4Srgb, 5, 4, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc5x4Float, 5, 4, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc5x5UNorm, 5, 5, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc5x5Srgb, 5, 5, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc5x5Float, 5, 5, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc6x5UNorm, 6, 5, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc6x5Srgb, 6, 5, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc6x5Float, 6, 5, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc6x6UNorm, 6, 6, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc6x6Srgb, 6, 6, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc6x6Float, 6, 6, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc8x5UNorm, 8, 5, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc8x5Srgb, 8, 5, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc8x5Float, 8, 5, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc8x6UNorm, 8, 6, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc8x6Srgb, 8, 6, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc8x6Float, 8, 6, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc8x8UNorm, 8, 8, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc8x8Srgb, 8, 8, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc8x8Float, 8, 8, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc10x5UNorm, 10, 5, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc10x5Srgb, 10, 5, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc10x5Float, 10, 5, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc10x6UNorm, 10, 6, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc10x6Srgb, 10, 6, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc10x6Float, 10, 6, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc10x8UNorm, 10, 8, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc10x8Srgb, 10, 8, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc10x8Float, 10, 8, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc10x10UNorm, 10, 10, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc10x10Srgb, 10, 10, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc10x10Float, 10, 10, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc12x10UNorm, 12, 10, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc12x10Srgb, 12, 10, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc12x10Float, 12, 10, AstcTransfer.Hdr),
        new(TextureFormats.RgbaAstc12x12UNorm, 12, 12, AstcTransfer.Ldr),
        new(TextureFormats.RgbaAstc12x12Srgb, 12, 12, AstcTransfer.Srgb),
        new(TextureFormats.RgbaAstc12x12Float, 12, 12, AstcTransfer.Hdr)
    ];

    private static readonly TextureFormat[] SSupportedFormats = CreateSupportedFormats();

    private readonly AstcTransfer _transfer;
    private readonly int _blockWidth;
    private readonly int _blockHeight;
    private readonly AstcCoderOptions _options;

    public AstcTextureCoder(TextureFormat format, AstcCoderOptions? options = null)
    {
        if (!TryGetFormatInfo(format, out var info))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _transfer = info.Transfer;
        _blockWidth = info.BlockWidth;
        _blockHeight = info.BlockHeight;
        _options = options ?? new AstcCoderOptions();
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) => TryGetTransfer(format, out _);

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        return checked(rowPitch * GetBlockCount(height, _blockHeight));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        DecodeByTransfer(source, destination, rowPitch);
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeByTransfer(source, destination, rowPitch);
    }

    private void DecodeByTransfer<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case AstcTransfer.Ldr:
                DecodeUNorm<TPixel, AstcLdrTransfer>(source, destination, rowPitch);
                return;
            case AstcTransfer.Srgb:
                DecodeUNorm<TPixel, AstcSrgbTransfer>(source, destination, rowPitch);
                return;
            case AstcTransfer.Hdr:
                DecodeFloat<TPixel, AstcHdrTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeByTransfer<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case AstcTransfer.Ldr:
                EncodeUNorm<TPixel, AstcLdrTransfer>(source, destination, rowPitch);
                return;
            case AstcTransfer.Srgb:
                EncodeUNorm<TPixel, AstcSrgbTransfer>(source, destination, rowPitch);
                return;
            case AstcTransfer.Hdr:
                EncodeFloat<TPixel, AstcHdrTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void DecodeUNorm<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAstcUNormTransfer
    {
        var blockCountX = GetBlockCount(destination.Width, _blockWidth);
        var blockCountY = GetBlockCount(destination.Height, _blockHeight);
        var block = new Rgba8UNormTexelBlock();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                TTransfer.DecodeBlock(source.Slice(blockOffset, BytesPerBlock), _blockWidth, _blockHeight, block);
                StoreUNormBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeFloat<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAstcFloatTransfer
    {
        var blockCountX = GetBlockCount(source.Width, _blockWidth);
        var blockCountY = GetBlockCount(source.Height, _blockHeight);
        var compressionMode = _options.CompressionMode;

        if (TextureCodingParallel.ShouldParallelize(blockCountX, blockCountY))
        {
            var width = source.Width;
            var height = source.Height;
            var pixelCount = checked(width * height);
            var destinationLength = destination.Length;
            unsafe
            {
                fixed (TPixel* sourceBase = source.Pixels)
                fixed (byte* destinationBase = destination)
                {
                    var sourceAddress = (nint)sourceBase;
                    var destinationAddress = (nint)destinationBase;
                    Parallel.For(0, blockCountY, blockY =>
                    {
                        var localSource = new BitmapView<TPixel>(
                            new Span<TPixel>((void*)sourceAddress, pixelCount),
                            width,
                            height);
                        var localDestination = new Span<byte>((void*)destinationAddress, destinationLength);
                        var block = new Rgba16FloatTexelBlock();

                        var blockOffset = checked(blockY * rowPitch);
                        for (var blockX = 0; blockX < blockCountX; blockX++)
                        {
                            LoadFloatBlock(localSource, blockX, blockY, block);
                            TTransfer.EncodeBlock(
                                block,
                                _blockWidth,
                                _blockHeight,
                                compressionMode,
                                localDestination.Slice(blockOffset, BytesPerBlock));
                            blockOffset = checked(blockOffset + BytesPerBlock);
                        }
                    });
                }
            }

            return;
        }

        var block = new Rgba16FloatTexelBlock();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadFloatBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, _blockWidth, _blockHeight, compressionMode, destination.Slice(blockOffset, BytesPerBlock));
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeFloat<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAstcFloatTransfer
    {
        var blockCountX = GetBlockCount(destination.Width, _blockWidth);
        var blockCountY = GetBlockCount(destination.Height, _blockHeight);
        var block = new Rgba16FloatTexelBlock();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                TTransfer.DecodeBlock(source.Slice(blockOffset, BytesPerBlock), _blockWidth, _blockHeight, block);
                StoreFloatBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeUNorm<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAstcUNormTransfer
    {
        var blockCountX = GetBlockCount(source.Width, _blockWidth);
        var blockCountY = GetBlockCount(source.Height, _blockHeight);
        var compressionMode = _options.CompressionMode;

        if (TextureCodingParallel.ShouldParallelize(blockCountX, blockCountY))
        {
            var width = source.Width;
            var height = source.Height;
            var pixelCount = checked(width * height);
            var destinationLength = destination.Length;
            unsafe
            {
                fixed (TPixel* sourceBase = source.Pixels)
                fixed (byte* destinationBase = destination)
                {
                    var sourceAddress = (nint)sourceBase;
                    var destinationAddress = (nint)destinationBase;
                    Parallel.For(0, blockCountY, blockY =>
                    {
                        var localSource = new BitmapView<TPixel>(
                            new Span<TPixel>((void*)sourceAddress, pixelCount),
                            width,
                            height);
                        var localDestination = new Span<byte>((void*)destinationAddress, destinationLength);
                        var block = new Rgba8UNormTexelBlock();

                        var blockOffset = checked(blockY * rowPitch);
                        for (var blockX = 0; blockX < blockCountX; blockX++)
                        {
                            LoadUNormBlock(localSource, blockX, blockY, block);
                            TTransfer.EncodeBlock(
                                block,
                                _blockWidth,
                                _blockHeight,
                                compressionMode,
                                localDestination.Slice(blockOffset, BytesPerBlock));
                            blockOffset = checked(blockOffset + BytesPerBlock);
                        }
                    });
                }
            }

            return;
        }

        var block = new Rgba8UNormTexelBlock();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadUNormBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, _blockWidth, _blockHeight, compressionMode, destination.Slice(blockOffset, BytesPerBlock));
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IAstcUNormTransfer
    {
        static abstract void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, TextureCompressionLevel compressionMode, Span<byte> destination);
    }

    private interface IAstcFloatTransfer
    {
        static abstract void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba16Float> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba16Float> source, int blockWidth, int blockHeight, TextureCompressionLevel compressionMode, Span<byte> destination);
    }

    private readonly struct AstcLdrTransfer : IAstcUNormTransfer
    {
        public static void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba8UNorm> destination) =>
            DecodeLdrBlock(source, blockWidth, blockHeight, srgb: false, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, TextureCompressionLevel compressionMode, Span<byte> destination) =>
            EncodeLdrBlock(source, blockWidth, blockHeight, srgb: false, compressionMode, destination);
    }

    private readonly struct AstcSrgbTransfer : IAstcUNormTransfer
    {
        public static void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba8UNorm> destination) =>
            DecodeLdrBlock(source, blockWidth, blockHeight, srgb: true, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, TextureCompressionLevel compressionMode, Span<byte> destination) =>
            EncodeLdrBlock(source, blockWidth, blockHeight, srgb: true, compressionMode, destination);
    }

    private readonly struct AstcHdrTransfer : IAstcFloatTransfer
    {
        public static void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba16Float> destination) =>
            DecodeHdrBlock(source, blockWidth, blockHeight, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba16Float> source, int blockWidth, int blockHeight, TextureCompressionLevel compressionMode, Span<byte> destination) =>
            EncodeHdrBlock(source, blockWidth, blockHeight, compressionMode, destination);
    }

    private static void DecodeLdrBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, bool srgb, Span<Rgba8UNorm> destination)
    {
        var bits = ReadBlockBits(source);
        if (!TryDecodeBlockInfo(bits, blockWidth, blockHeight, out var info))
        {
            FillUNormBlock(destination, blockWidth, blockHeight, ErrorUNorm);
            return;
        }

        if (info.IsVoidExtent)
        {
            if (info.VoidExtentIsHdr)
            {
                FillUNormBlock(destination, blockWidth, blockHeight, ErrorUNorm);
                return;
            }

            var color = ReadVoidExtentUNorm(source, srgb);
            FillUNormBlock(destination, blockWidth, blockHeight, color);
            return;
        }

        var endpoints = new InlineArray4<AstcEndpointPair>();
        if (!DecodeEndpointPairs(bits, info, endpoints))
        {
            FillUNormBlock(destination, blockWidth, blockHeight, ErrorUNorm);
            return;
        }

        var weights0 = new IntTexelBlock();
        var weights1 = new IntTexelBlock();
        if (!DecodeWeights(bits, info, blockWidth, blockHeight, weights0, weights1))
        {
            FillUNormBlock(destination, blockWidth, blockHeight, ErrorUNorm);
            return;
        }

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var partition = GetPartitionIndex(info.PartitionCount, info.PartitionIndex, x, y, blockWidth, blockHeight);
                var endpoint = endpoints[partition];
                destination[texelIndex] = endpoint.RgbHdr || endpoint.AlphaHdr
                    ? ErrorUNorm
                    : DecodeLdrTexel(endpoint, weights0[texelIndex], GetDualPlaneWeight(info, weights0, weights1, texelIndex, 0), GetDualPlaneWeight(info, weights0, weights1, texelIndex, 1), GetDualPlaneWeight(info, weights0, weights1, texelIndex, 2), GetDualPlaneWeight(info, weights0, weights1, texelIndex, 3), srgb);
            }
        }
    }

    private static void DecodeHdrBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba16Float> destination)
    {
        var bits = ReadBlockBits(source);
        if (!TryDecodeBlockInfo(bits, blockWidth, blockHeight, out var info))
        {
            FillFloatBlock(destination, blockWidth, blockHeight, ErrorFloat);
            return;
        }

        if (info.IsVoidExtent)
        {
            var color = info.VoidExtentIsHdr
                ? ReadVoidExtentFloat(source)
                : ReadVoidExtentUNormAsFloat(source);
            FillFloatBlock(destination, blockWidth, blockHeight, color);
            return;
        }

        var endpoints = new InlineArray4<AstcEndpointPair>();
        if (!DecodeEndpointPairs(bits, info, endpoints))
        {
            FillFloatBlock(destination, blockWidth, blockHeight, ErrorFloat);
            return;
        }

        var weights0 = new IntTexelBlock();
        var weights1 = new IntTexelBlock();
        if (!DecodeWeights(bits, info, blockWidth, blockHeight, weights0, weights1))
        {
            FillFloatBlock(destination, blockWidth, blockHeight, ErrorFloat);
            return;
        }

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var partition = GetPartitionIndex(info.PartitionCount, info.PartitionIndex, x, y, blockWidth, blockHeight);
                var endpoint = endpoints[partition];
                destination[texelIndex] = DecodeHdrTexel(
                    endpoint,
                    GetDualPlaneWeight(info, weights0, weights1, texelIndex, 0),
                    GetDualPlaneWeight(info, weights0, weights1, texelIndex, 1),
                    GetDualPlaneWeight(info, weights0, weights1, texelIndex, 2),
                    GetDualPlaneWeight(info, weights0, weights1, texelIndex, 3));
            }
        }
    }

    private static void EncodeLdrBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        int blockWidth,
        int blockHeight,
        bool srgb,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var texelCount = blockWidth * blockHeight;
        var storage = new Rgba8UNormTexelBlock();
        for (var i = 0; i < texelCount; i++)
        {
            storage[i] = EncodeStorageColor(source[i], srgb);
        }

        if (IsSolidBlock(storage, texelCount, out var solidColor))
        {
            WriteLdrVoidExtentBlock(solidColor, destination);
            return;
        }

        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeLdrBlockFast(storage, blockWidth, blockHeight, texelCount, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeLdrBlockOptimized(storage, blockWidth, blockHeight, texelCount, compressionMode, destination);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported ASTC compression mode.");
        }
    }

    private static void EncodeLdrBlockFast(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        Span<byte> destination)
    {
        FindRgbaBounds(storage, texelCount, out var low, out var high);

        var weightAccumulators = new InlineArray16<int>();
        var weightCounts = new InlineArray16<int>();
        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var gridX = GetEncoderGridCoordinate(x, blockWidth);
                var gridY = GetEncoderGridCoordinate(y, blockHeight);
                var gridIndex = (gridY * 4) + gridX;
                weightAccumulators[gridIndex] += QuantizeWeight(storage[texelIndex], low, high);
                weightCounts[gridIndex]++;
            }
        }

        uint weightStream = 0;
        for (var i = 0; i < 16; i++)
        {
            var weight = weightCounts[i] == 0
                ? 0
                : (weightAccumulators[i] + (weightCounts[i] / 2)) / weightCounts[i];
            weightStream |= (uint)Math.Clamp(weight, 0, 3) << (i * 2);
        }

        UInt128 bits = 66;
        bits |= (UInt128)12 << 13;
        bits |= (UInt128)low.Red << 17;
        bits |= (UInt128)high.Red << 25;
        bits |= (UInt128)low.Green << 33;
        bits |= (UInt128)high.Green << 41;
        bits |= (UInt128)low.Blue << 49;
        bits |= (UInt128)high.Blue << 57;
        bits |= (UInt128)low.Alpha << 65;
        bits |= (UInt128)high.Alpha << 73;
        bits |= (UInt128)ReverseBits32(weightStream) << 96;

        WriteBlockBits(bits, destination);
    }

    private static void EncodeLdrBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        Span<AstcWeightGridCandidate> candidates = stackalloc AstcWeightGridCandidate[128];
        var candidateCount = GetWeightGridCandidates(blockWidth, blockHeight, compressionMode, candidates);
        if (candidateCount == 0)
        {
            EncodeLdrBlockFast(storage, blockWidth, blockHeight, texelCount, destination);
            return;
        }

        FindRgbaBounds(storage, texelCount, out var boundsLow, out var boundsHigh);
        var best = new AstcLdrEncodingResult(UInt128.Zero, long.MaxValue);
        var iterationLimit = GetLdrEndpointOptimizationIterationLimit(compressionMode);
        var hasPrincipalEndpoints = TryFindPrincipalRgbaEndpoints(storage, texelCount, out var principalLow, out var principalHigh);
        var isOpaque = IsOpaqueLdrBlock(storage, texelCount);

        for (var i = 0; i < candidateCount; i++)
        {
            TryEncodeLdrRgbaCandidate(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidates[i],
                boundsLow,
                boundsHigh,
                iterationLimit,
                ref best);

            if (isOpaque)
            {
                TryEncodeLdrRgbCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidates[i],
                    boundsLow,
                    boundsHigh,
                    iterationLimit,
                    ref best);
            }

            if (hasPrincipalEndpoints)
            {
                TryEncodeLdrRgbaCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidates[i],
                    principalLow,
                    principalHigh,
                    iterationLimit,
                    ref best);

                if (isOpaque)
                {
                    TryEncodeLdrRgbCandidate(
                        storage,
                        blockWidth,
                        blockHeight,
                        texelCount,
                        candidates[i],
                        principalLow,
                        principalHigh,
                        iterationLimit,
                        ref best);
                }
            }
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive)
        {
            if (isOpaque && compressionMode == TextureCompressionLevel.Exhaustive)
            {
                TryEncodeLdrDualPlaneRgbCandidates(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    compressionMode,
                    boundsLow,
                    boundsHigh,
                    hasPrincipalEndpoints,
                    principalLow,
                    principalHigh,
                    iterationLimit,
                    ref best);
            }

            TryEncodeLdrTwoPartitionCandidates(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidates[..candidateCount],
                compressionMode,
                isOpaque,
                iterationLimit,
                ref best);

            if (isOpaque)
            {
                TryEncodeLdrThreePartitionRgbCandidates(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidates[..candidateCount],
                    compressionMode,
                    iterationLimit,
                    ref best);
            }
        }

        if (best.Error == long.MaxValue)
        {
            EncodeLdrBlockFast(storage, blockWidth, blockHeight, texelCount, destination);
            return;
        }

        WriteBlockBits(best.Bits, destination);
    }

    private static void TryEncodeLdrRgbaCandidate(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm initialLow,
        Rgba8UNorm initialHigh,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        if (!TryGetColorRange(candidate, colorValueCount: 8, out var colorRange))
        {
            return;
        }

        var low = initialLow;
        var high = initialHigh;
        NormalizeLdrEndpointOrder(ref low, ref high);

        var rawWeights = new IntWeightBlock();
        var texelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[8];
        Rgba8UNorm quantizedLow = default;
        Rgba8UNorm quantizedHigh = default;

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            QuantizeLdrEndpoints(low, high, colorRange, endpointValues, out quantizedLow, out quantizedHigh);
            BuildLdrRawWeights(storage, blockWidth, blockHeight, candidate, quantizedLow, quantizedHigh, rawWeights);
            InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);

            if (pass == iterationLimit
                || !TryOptimizeLdrEndpoints(storage, texelCount, texelWeights, out low, out high))
            {
                break;
            }

            NormalizeLdrEndpointOrder(ref low, ref high);
        }

        var error = GetLdrSquaredError(storage, texelCount, texelWeights, quantizedLow, quantizedHigh);
        if (error >= best.Error)
        {
            return;
        }

        if (ShouldRefineLdrWeights(iterationLimit))
        {
            error = RefineLdrRawWeights(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidate,
                quantizedLow,
                quantizedHigh,
                rawWeights,
                texelWeights,
                error);
        }

        var bits = EncodeLdrSinglePartitionBlock(candidate, endpointMode: 12, endpointValues, endpointValueCount: 8, colorRange, rawWeights);
        best = new AstcLdrEncodingResult(bits, error);
    }

    private static void TryEncodeLdrRgbCandidate(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm initialLow,
        Rgba8UNorm initialHigh,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        if (!TryGetColorRange(candidate, colorValueCount: 6, out var colorRange))
        {
            return;
        }

        var low = new Rgba8UNorm(initialLow.Red, initialLow.Green, initialLow.Blue, 255);
        var high = new Rgba8UNorm(initialHigh.Red, initialHigh.Green, initialHigh.Blue, 255);
        NormalizeLdrEndpointOrder(ref low, ref high);

        var rawWeights = new IntWeightBlock();
        var texelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[6];
        Rgba8UNorm quantizedLow = default;
        Rgba8UNorm quantizedHigh = default;

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            QuantizeLdrRgbEndpoints(low, high, colorRange, endpointValues, out quantizedLow, out quantizedHigh);
            BuildLdrRawWeights(storage, blockWidth, blockHeight, candidate, quantizedLow, quantizedHigh, rawWeights);
            InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);

            if (pass == iterationLimit
                || !TryOptimizeLdrRgbEndpoints(storage, texelCount, texelWeights, out low, out high))
            {
                break;
            }

            NormalizeLdrEndpointOrder(ref low, ref high);
        }

        var error = GetLdrSquaredError(storage, texelCount, texelWeights, quantizedLow, quantizedHigh);
        if (error >= best.Error)
        {
            return;
        }

        if (ShouldRefineLdrWeights(iterationLimit))
        {
            error = RefineLdrRawWeights(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidate,
                quantizedLow,
                quantizedHigh,
                rawWeights,
                texelWeights,
                error);
        }

        var bits = EncodeLdrSinglePartitionBlock(candidate, endpointMode: 8, endpointValues, endpointValueCount: 6, colorRange, rawWeights);
        best = new AstcLdrEncodingResult(bits, error);
    }

    private static void TryEncodeLdrDualPlaneRgbCandidates(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        TextureCompressionLevel compressionMode,
        Rgba8UNorm boundsLow,
        Rgba8UNorm boundsHigh,
        bool hasPrincipalEndpoints,
        Rgba8UNorm principalLow,
        Rgba8UNorm principalHigh,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        Span<AstcWeightGridCandidate> candidates = stackalloc AstcWeightGridCandidate[128];
        var candidateCount = GetDualPlaneWeightGridCandidates(blockWidth, blockHeight, compressionMode, candidates);
        var candidateLimit = compressionMode == TextureCompressionLevel.Exhaustive ? 32 : 4;

        for (var i = 0; i < candidateCount && i < candidateLimit; i++)
        {
            var candidate = candidates[i];
            for (var dualPlaneChannel = 0; dualPlaneChannel < 3; dualPlaneChannel++)
            {
                TryEncodeLdrDualPlaneRgbCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidate,
                    boundsLow,
                    boundsHigh,
                    dualPlaneChannel,
                    iterationLimit,
                    ref best);

                if (hasPrincipalEndpoints)
                {
                    TryEncodeLdrDualPlaneRgbCandidate(
                        storage,
                        blockWidth,
                        blockHeight,
                        texelCount,
                        candidate,
                        principalLow,
                        principalHigh,
                        dualPlaneChannel,
                        iterationLimit,
                        ref best);
                }
            }
        }
    }

    private static void TryEncodeLdrDualPlaneRgbCandidate(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm initialLow,
        Rgba8UNorm initialHigh,
        int dualPlaneChannel,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        if (!TryGetColorRange(candidate, colorValueCount: 6, out var colorRange))
        {
            return;
        }

        var low = new Rgba8UNorm(initialLow.Red, initialLow.Green, initialLow.Blue, 255);
        var high = new Rgba8UNorm(initialHigh.Red, initialHigh.Green, initialHigh.Blue, 255);
        NormalizeLdrEndpointOrder(ref low, ref high);

        var rawWeights = new IntWeightBlock();
        var sharedTexelWeights = new IntTexelBlock();
        var planeTexelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[6];
        Rgba8UNorm quantizedLow = default;
        Rgba8UNorm quantizedHigh = default;

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            QuantizeLdrRgbEndpoints(low, high, colorRange, endpointValues, out quantizedLow, out quantizedHigh);
            BuildLdrDualPlaneRawWeights(
                storage,
                blockWidth,
                blockHeight,
                candidate,
                quantizedLow,
                quantizedHigh,
                dualPlaneChannel,
                rawWeights);
            InfillLdrDualPlaneWeights(rawWeights, candidate, blockWidth, blockHeight, sharedTexelWeights, planeTexelWeights);

            if (pass == iterationLimit
                || !TryOptimizeLdrDualPlaneRgbEndpoints(storage, texelCount, sharedTexelWeights, planeTexelWeights, dualPlaneChannel, out low, out high))
            {
                break;
            }

            NormalizeLdrEndpointOrder(ref low, ref high);
        }

        var error = GetLdrDualPlaneSquaredError(
            storage,
            texelCount,
            sharedTexelWeights,
            planeTexelWeights,
            quantizedLow,
            quantizedHigh,
            dualPlaneChannel);
        if (error >= best.Error)
        {
            return;
        }

        if (ShouldRefineLdrWeights(iterationLimit))
        {
            error = RefineLdrDualPlaneRawWeights(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidate,
                quantizedLow,
                quantizedHigh,
                dualPlaneChannel,
                rawWeights,
                sharedTexelWeights,
                planeTexelWeights,
                error);
        }

        var bits = EncodeLdrDualPlaneSinglePartitionBlock(
            candidate,
            endpointMode: 8,
            endpointValues,
            endpointValueCount: 6,
            colorRange,
            rawWeights,
            dualPlaneChannel);
        best = new AstcLdrEncodingResult(bits, error);
    }

    private static void TryEncodeLdrTwoPartitionCandidates(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        ReadOnlySpan<AstcWeightGridCandidate> candidates,
        TextureCompressionLevel compressionMode,
        bool isOpaque,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        Span<int> partitionSeeds = stackalloc int[32];
        var seedCount = GetLdrPartitionSeedCandidates(storage, blockWidth, blockHeight, texelCount, partitionCount: 2, compressionMode, partitionSeeds);
        var candidateLimit = GetTwoPartitionWeightCandidateLimit(compressionMode);
        var endpointMode = isOpaque ? 8 : 12;
        var endpointValueCount = isOpaque ? 6 : 8;

        for (var seedIndex = 0; seedIndex < seedCount; seedIndex++)
        {
            var partitionSeed = partitionSeeds[seedIndex];
            for (var candidateIndex = 0; candidateIndex < candidates.Length && candidateIndex < candidateLimit; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                if (!TryGetColorRange(candidate, partitionCount: 2, colorValueCount: endpointValueCount * 2, out var colorRange))
                {
                    continue;
                }

                TryEncodeLdrTwoPartitionCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidate,
                    partitionSeed,
                    endpointMode,
                    endpointValueCount,
                    colorRange,
                    isOpaque,
                    iterationLimit,
                    ref best);
            }
        }
    }

    private static void TryEncodeLdrThreePartitionRgbCandidates(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        ReadOnlySpan<AstcWeightGridCandidate> candidates,
        TextureCompressionLevel compressionMode,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        const int PartitionCount = 3;
        const int EndpointMode = 8;
        const int EndpointValueCount = 6;

        Span<int> partitionSeeds = stackalloc int[32];
        var seedCount = GetLdrPartitionSeedCandidates(storage, blockWidth, blockHeight, texelCount, PartitionCount, compressionMode, partitionSeeds);
        var candidateLimit = GetThreePartitionWeightCandidateLimit(compressionMode);

        for (var seedIndex = 0; seedIndex < seedCount; seedIndex++)
        {
            var partitionSeed = partitionSeeds[seedIndex];
            for (var candidateIndex = 0; candidateIndex < candidates.Length && candidateIndex < candidateLimit; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                if (!TryGetColorRange(candidate, PartitionCount, colorValueCount: EndpointValueCount * PartitionCount, out var colorRange))
                {
                    continue;
                }

                TryEncodeLdrThreePartitionRgbCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidate,
                    partitionSeed,
                    EndpointMode,
                    EndpointValueCount,
                    colorRange,
                    iterationLimit,
                    ref best);
            }
        }
    }

    private static void TryEncodeLdrTwoPartitionCandidate(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        int partitionSeed,
        int endpointMode,
        int endpointValueCount,
        int colorRange,
        bool rgbOnly,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        var partitions = new IntTexelBlock();
        if (!TryBuildPartitionAssignments(partitionSeed, blockWidth, blockHeight, partitions))
        {
            return;
        }

        FindLdrPartitionBounds(storage, texelCount, partitions, partition: 0, rgbOnly, out var low0, out var high0);
        FindLdrPartitionBounds(storage, texelCount, partitions, partition: 1, rgbOnly, out var low1, out var high1);

        var rawWeights = new IntWeightBlock();
        var texelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[16];
        Rgba8UNorm quantizedLow0 = default;
        Rgba8UNorm quantizedHigh0 = default;
        Rgba8UNorm quantizedLow1 = default;
        Rgba8UNorm quantizedHigh1 = default;

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            if (rgbOnly)
            {
                QuantizeLdrRgbEndpoints(low0, high0, colorRange, endpointValues[..endpointValueCount], out quantizedLow0, out quantizedHigh0);
                QuantizeLdrRgbEndpoints(low1, high1, colorRange, endpointValues.Slice(endpointValueCount, endpointValueCount), out quantizedLow1, out quantizedHigh1);
            }
            else
            {
                QuantizeLdrEndpoints(low0, high0, colorRange, endpointValues[..endpointValueCount], out quantizedLow0, out quantizedHigh0);
                QuantizeLdrEndpoints(low1, high1, colorRange, endpointValues.Slice(endpointValueCount, endpointValueCount), out quantizedLow1, out quantizedHigh1);
            }

            BuildLdrPartitionedRawWeights(
                storage,
                blockWidth,
                blockHeight,
                candidate,
                partitions,
                quantizedLow0,
                quantizedHigh0,
                quantizedLow1,
                quantizedHigh1,
                rawWeights);
            InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);

            if (pass == iterationLimit
                || !TryOptimizeLdrPartitionEndpoints(storage, texelCount, partitions, texelWeights, partition: 0, rgbOnly, out low0, out high0)
                || !TryOptimizeLdrPartitionEndpoints(storage, texelCount, partitions, texelWeights, partition: 1, rgbOnly, out low1, out high1))
            {
                break;
            }

            NormalizeLdrEndpointOrder(ref low0, ref high0);
            NormalizeLdrEndpointOrder(ref low1, ref high1);
        }

        var error = GetLdrPartitionedSquaredError(
            storage,
            texelCount,
            partitions,
            texelWeights,
            quantizedLow0,
            quantizedHigh0,
            quantizedLow1,
            quantizedHigh1);
        if (error >= best.Error)
        {
            return;
        }

        if (ShouldRefineLdrWeights(iterationLimit))
        {
            Span<Rgba8UNorm> lows = stackalloc Rgba8UNorm[2];
            Span<Rgba8UNorm> highs = stackalloc Rgba8UNorm[2];
            lows[0] = quantizedLow0;
            lows[1] = quantizedLow1;
            highs[0] = quantizedHigh0;
            highs[1] = quantizedHigh1;
            error = RefineLdrPartitionedRawWeights(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidate,
                partitions,
                lows,
                highs,
                rawWeights,
                texelWeights,
                error);
        }

        var bits = EncodeLdrTwoPartitionBlock(
            candidate,
            partitionSeed,
            endpointMode,
            endpointValues,
            endpointValueCount * 2,
            colorRange,
            rawWeights);
        best = new AstcLdrEncodingResult(bits, error);
    }

    private static void TryEncodeLdrThreePartitionRgbCandidate(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        int partitionSeed,
        int endpointMode,
        int endpointValueCount,
        int colorRange,
        int iterationLimit,
        ref AstcLdrEncodingResult best)
    {
        const int PartitionCount = 3;

        var partitions = new IntTexelBlock();
        if (!TryBuildPartitionAssignments(PartitionCount, partitionSeed, blockWidth, blockHeight, partitions))
        {
            return;
        }

        Span<Rgba8UNorm> lows = stackalloc Rgba8UNorm[PartitionCount];
        Span<Rgba8UNorm> highs = stackalloc Rgba8UNorm[PartitionCount];
        Span<Rgba8UNorm> quantizedLows = stackalloc Rgba8UNorm[PartitionCount];
        Span<Rgba8UNorm> quantizedHighs = stackalloc Rgba8UNorm[PartitionCount];
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            FindLdrPartitionBounds(storage, texelCount, partitions, partition, rgbOnly: true, out lows[partition], out highs[partition]);
        }

        var rawWeights = new IntWeightBlock();
        var texelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[18];

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                QuantizeLdrRgbEndpoints(
                    lows[partition],
                    highs[partition],
                    colorRange,
                    endpointValues.Slice(partition * endpointValueCount, endpointValueCount),
                    out quantizedLows[partition],
                    out quantizedHighs[partition]);
            }

            BuildLdrPartitionedRawWeights(
                storage,
                blockWidth,
                blockHeight,
                candidate,
                partitions,
                quantizedLows,
                quantizedHighs,
                rawWeights);
            InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);

            if (pass == iterationLimit)
            {
                break;
            }

            var optimized = true;
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                if (!TryOptimizeLdrPartitionEndpoints(
                    storage,
                    texelCount,
                    partitions,
                    texelWeights,
                    partition,
                    rgbOnly: true,
                    out lows[partition],
                    out highs[partition]))
                {
                    optimized = false;
                    break;
                }

                NormalizeLdrEndpointOrder(ref lows[partition], ref highs[partition]);
            }

            if (!optimized)
            {
                break;
            }
        }

        var error = GetLdrPartitionedSquaredError(
            storage,
            texelCount,
            partitions,
            texelWeights,
            quantizedLows,
            quantizedHighs);
        if (error >= best.Error)
        {
            return;
        }

        if (ShouldRefineLdrWeights(iterationLimit))
        {
            error = RefineLdrPartitionedRawWeights(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidate,
                partitions,
                quantizedLows,
                quantizedHighs,
                rawWeights,
                texelWeights,
                error);
        }

        var bits = EncodeLdrMultiPartitionBlock(
            candidate,
            partitionCount: PartitionCount,
            partitionSeed,
            endpointMode,
            endpointValues,
            endpointValueCount * PartitionCount,
            colorRange,
            rawWeights);
        best = new AstcLdrEncodingResult(bits, error);
    }

    private static UInt128 EncodeLdrSinglePartitionBlock(
        AstcWeightGridCandidate candidate,
        int endpointMode,
        ReadOnlySpan<int> endpointValues,
        int endpointValueCount,
        int colorRange,
        ReadOnlySpan<int> rawWeights)
    {
        var endpointBits = EncodeBiseSequence(endpointValues, endpointValueCount, colorRange);
        var weightValueCount = candidate.Width * candidate.Height;
        var weightBits = EncodeBiseSequence(rawWeights, weightValueCount, candidate.WeightRange);

        var bits = (UInt128)(uint)candidate.BlockMode;
        bits |= (UInt128)endpointMode << 13;
        bits |= endpointBits << 17;
        bits |= ReverseLowBits(weightBits, candidate.WeightBitCount) << (128 - candidate.WeightBitCount);
        return bits;
    }

    private static UInt128 EncodeLdrDualPlaneSinglePartitionBlock(
        AstcWeightGridCandidate candidate,
        int endpointMode,
        ReadOnlySpan<int> endpointValues,
        int endpointValueCount,
        int colorRange,
        ReadOnlySpan<int> rawWeights,
        int dualPlaneChannel)
    {
        var endpointBits = EncodeBiseSequence(endpointValues, endpointValueCount, colorRange);
        var weightValueCount = candidate.Width * candidate.Height * 2;
        var weightBits = EncodeBiseSequence(rawWeights, weightValueCount, candidate.WeightRange);
        var dualPlaneBitStart = 128 - candidate.WeightBitCount - 2;

        var bits = (UInt128)(uint)candidate.BlockMode;
        bits |= (UInt128)endpointMode << 13;
        bits |= endpointBits << 17;
        bits |= (UInt128)(dualPlaneChannel & 0x3) << dualPlaneBitStart;
        bits |= ReverseLowBits(weightBits, candidate.WeightBitCount) << (128 - candidate.WeightBitCount);
        return bits;
    }

    private static UInt128 EncodeLdrTwoPartitionBlock(
        AstcWeightGridCandidate candidate,
        int partitionSeed,
        int endpointMode,
        ReadOnlySpan<int> endpointValues,
        int endpointValueCount,
        int colorRange,
        ReadOnlySpan<int> rawWeights)
    {
        return EncodeLdrMultiPartitionBlock(
            candidate,
            partitionCount: 2,
            partitionSeed,
            endpointMode,
            endpointValues,
            endpointValueCount,
            colorRange,
            rawWeights);
    }

    private static UInt128 EncodeLdrMultiPartitionBlock(
        AstcWeightGridCandidate candidate,
        int partitionCount,
        int partitionSeed,
        int endpointMode,
        ReadOnlySpan<int> endpointValues,
        int endpointValueCount,
        int colorRange,
        ReadOnlySpan<int> rawWeights)
    {
        var endpointBits = EncodeBiseSequence(endpointValues, endpointValueCount, colorRange);
        var weightValueCount = candidate.Width * candidate.Height;
        var weightBits = EncodeBiseSequence(rawWeights, weightValueCount, candidate.WeightRange);

        var bits = (UInt128)(uint)candidate.BlockMode;
        bits |= (UInt128)(partitionCount - 1) << 11;
        bits |= (UInt128)(partitionSeed & 0x3FF) << 13;
        bits |= (UInt128)endpointMode << 25;
        bits |= endpointBits << 29;
        bits |= ReverseLowBits(weightBits, candidate.WeightBitCount) << (128 - candidate.WeightBitCount);
        return bits;
    }

    private static int GetWeightGridCandidates(
        int blockWidth,
        int blockHeight,
        TextureCompressionLevel compressionMode,
        Span<AstcWeightGridCandidate> candidates)
    {
        ValidateCompressionMode(compressionMode);

        var count = 0;
        AddWeightGridCandidate(blockWidth, blockHeight, 4, 4, 3, candidates, ref count);

        if (compressionMode == TextureCompressionLevel.Normal)
        {
            return count;
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            for (var blockMode = 0; blockMode < 2048; blockMode++)
            {
                if (TryCreateWeightGridCandidate(blockMode, blockWidth, blockHeight, out var candidate))
                {
                    AddUniqueWeightGridCandidate(candidate, candidates, ref count);
                }
            }

            return count;
        }

        AddWeightGridCandidate(blockWidth, blockHeight, 4, 4, 7, candidates, ref count);
        AddWeightGridCandidate(blockWidth, blockHeight, 5, 4, 3, candidates, ref count);
        AddWeightGridCandidate(blockWidth, blockHeight, 4, 5, 3, candidates, ref count);
        AddWeightGridCandidate(blockWidth, blockHeight, 5, 5, 3, candidates, ref count);
        AddWeightGridCandidate(blockWidth, blockHeight, 6, 5, 3, candidates, ref count);
        AddWeightGridCandidate(blockWidth, blockHeight, 5, 6, 3, candidates, ref count);
        AddWeightGridCandidate(blockWidth, blockHeight, 6, 6, 3, candidates, ref count);

        return count;
    }

    private static void AddWeightGridCandidate(
        int blockWidth,
        int blockHeight,
        int gridWidth,
        int gridHeight,
        int weightRange,
        Span<AstcWeightGridCandidate> candidates,
        ref int count)
    {
        if (gridWidth > blockWidth || gridHeight > blockHeight)
        {
            return;
        }

        for (var blockMode = 0; blockMode < 2048; blockMode++)
        {
            if (TryCreateWeightGridCandidate(blockMode, blockWidth, blockHeight, out var candidate)
                && candidate.Width == gridWidth
                && candidate.Height == gridHeight
                && candidate.WeightRange == weightRange)
            {
                AddUniqueWeightGridCandidate(candidate, candidates, ref count);
                return;
            }
        }
    }

    private static void AddUniqueWeightGridCandidate(
        AstcWeightGridCandidate candidate,
        Span<AstcWeightGridCandidate> candidates,
        ref int count)
    {
        for (var i = 0; i < count; i++)
        {
            var existing = candidates[i];
            if (existing.Width == candidate.Width
                && existing.Height == candidate.Height
                && existing.WeightRange == candidate.WeightRange
                && existing.DualPlane == candidate.DualPlane
                && existing.ColorRange == candidate.ColorRange)
            {
                return;
            }
        }

        if (count < candidates.Length)
        {
            candidates[count++] = candidate;
        }
    }

    private static bool TryCreateWeightGridCandidate(
        int blockMode,
        int blockWidth,
        int blockHeight,
        out AstcWeightGridCandidate candidate) =>
        TryCreateWeightGridCandidate(blockMode, blockWidth, blockHeight, requireDualPlane: false, colorValueCount: 8, out candidate);

    private static bool TryCreateWeightGridCandidate(
        int blockMode,
        int blockWidth,
        int blockHeight,
        bool requireDualPlane,
        int colorValueCount,
        out AstcWeightGridCandidate candidate)
    {
        if (!TryDecodeWeightGrid((ulong)blockMode, out var weightWidth, out var weightHeight, out var weightRange, out var widthA6HeightB6)
            || weightWidth > blockWidth
            || weightHeight > blockHeight)
        {
            candidate = default;
            return false;
        }

        var dualPlane = !widthA6HeightB6 && ((blockMode >> 10) & 1) != 0;
        if (dualPlane != requireDualPlane)
        {
            candidate = default;
            return false;
        }

        var weightValueCount = weightWidth * weightHeight * (dualPlane ? 2 : 1);
        if (weightValueCount > MaxWeightValues)
        {
            candidate = default;
            return false;
        }

        var weightBitCount = GetBiseBitCount(weightValueCount, weightRange);
        if (weightBitCount is < 24 or > 96)
        {
            candidate = default;
            return false;
        }

        var maxColorBits = 128 - weightBitCount - 17 - (dualPlane ? 2 : 0);
        if (!TryFitColorRange(colorValueCount, maxColorBits, out var colorRange, out var colorBitCount))
        {
            candidate = default;
            return false;
        }

        candidate = new AstcWeightGridCandidate(
            weightWidth,
            weightHeight,
            weightRange,
            blockMode,
            weightBitCount,
            dualPlane,
            colorRange,
            colorBitCount);
        return true;
    }

    private static bool TryGetColorRange(AstcWeightGridCandidate candidate, int colorValueCount, out int colorRange) =>
        TryGetColorRange(candidate, partitionCount: 1, colorValueCount, out colorRange);

    private static bool TryGetColorRange(AstcWeightGridCandidate candidate, int partitionCount, int colorValueCount, out int colorRange)
    {
        var colorStartBit = partitionCount == 1 ? 17 : 29;
        var maxColorBits = 128 - candidate.WeightBitCount - colorStartBit - (candidate.DualPlane ? 2 : 0);
        if (TryFitColorRange(colorValueCount, maxColorBits, out colorRange, out _))
        {
            return true;
        }

        colorRange = 0;
        return false;
    }

    private static int GetLdrPartitionSeedCandidates(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        int partitionCount,
        TextureCompressionLevel compressionMode,
        Span<int> seeds)
    {
        Span<double> scores = stackalloc double[32];
        var count = 0;
        var seedStep = compressionMode == TextureCompressionLevel.Exhaustive ? 1 : 32;
        var seedLimit = compressionMode == TextureCompressionLevel.Exhaustive ? 16 : 4;
        var partitions = new IntTexelBlock();

        for (var seed = 0; seed < 1024; seed += seedStep)
        {
            if (!TryBuildPartitionAssignments(partitionCount, seed, blockWidth, blockHeight, partitions))
            {
                continue;
            }

            var score = GetLdrMeanPartitionScore(storage, texelCount, partitions, partitionCount);
            InsertPartitionSeedCandidate(seed, score, seedLimit, seeds, scores, ref count);
        }

        return count;
    }

    private static int GetDualPlaneWeightGridCandidates(
        int blockWidth,
        int blockHeight,
        TextureCompressionLevel compressionMode,
        Span<AstcWeightGridCandidate> candidates)
    {
        ValidateCompressionMode(compressionMode);

        var count = 0;
        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            for (var blockMode = 0; blockMode < 2048; blockMode++)
            {
                if (TryCreateWeightGridCandidate(blockMode, blockWidth, blockHeight, requireDualPlane: true, colorValueCount: 6, out var candidate))
                {
                    AddUniqueWeightGridCandidate(candidate, candidates, ref count);
                }
            }

            return count;
        }

        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 4, 4, 3, candidates, ref count);
        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 4, 4, 7, candidates, ref count);
        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 5, 4, 3, candidates, ref count);
        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 4, 5, 3, candidates, ref count);
        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 5, 5, 3, candidates, ref count);
        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 6, 5, 3, candidates, ref count);
        AddDualPlaneWeightGridCandidate(blockWidth, blockHeight, 5, 6, 3, candidates, ref count);

        return count;
    }

    private static void AddDualPlaneWeightGridCandidate(
        int blockWidth,
        int blockHeight,
        int gridWidth,
        int gridHeight,
        int weightRange,
        Span<AstcWeightGridCandidate> candidates,
        ref int count)
    {
        if (gridWidth > blockWidth || gridHeight > blockHeight)
        {
            return;
        }

        for (var blockMode = 0; blockMode < 2048; blockMode++)
        {
            if (TryCreateWeightGridCandidate(blockMode, blockWidth, blockHeight, requireDualPlane: true, colorValueCount: 6, out var candidate)
                && candidate.Width == gridWidth
                && candidate.Height == gridHeight
                && candidate.WeightRange == weightRange)
            {
                AddUniqueWeightGridCandidate(candidate, candidates, ref count);
                return;
            }
        }
    }

    private static void InsertPartitionSeedCandidate(
        int seed,
        double score,
        int seedLimit,
        Span<int> seeds,
        Span<double> scores,
        ref int count)
    {
        var insertIndex = 0;
        while (insertIndex < count && scores[insertIndex] <= score)
        {
            insertIndex++;
        }

        if (insertIndex >= seedLimit)
        {
            return;
        }

        var maxMove = Math.Min(count, seedLimit - 1);
        for (var i = maxMove; i > insertIndex; i--)
        {
            seeds[i] = seeds[i - 1];
            scores[i] = scores[i - 1];
        }

        seeds[insertIndex] = seed;
        scores[insertIndex] = score;
        count = Math.Min(count + 1, seedLimit);
    }

    private static double GetLdrMeanPartitionScore(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> partitions,
        int partitionCount)
    {
        Span<int> counts = stackalloc int[partitionCount];
        Span<int> sumR = stackalloc int[partitionCount];
        Span<int> sumG = stackalloc int[partitionCount];
        Span<int> sumB = stackalloc int[partitionCount];
        Span<int> sumA = stackalloc int[partitionCount];

        for (var i = 0; i < texelCount; i++)
        {
            var partition = partitions[i];
            counts[partition]++;
            sumR[partition] += storage[i].Red;
            sumG[partition] += storage[i].Green;
            sumB[partition] += storage[i].Blue;
            sumA[partition] += storage[i].Alpha;
        }

        var score = 0.0;
        for (var i = 0; i < texelCount; i++)
        {
            var partition = partitions[i];
            var count = counts[partition];
            var red = sumR[partition] / count;
            var green = sumG[partition] / count;
            var blue = sumB[partition] / count;
            var alpha = sumA[partition] / count;
            score += Squared(storage[i].Red - red);
            score += Squared(storage[i].Green - green);
            score += Squared(storage[i].Blue - blue);
            score += Squared(storage[i].Alpha - alpha);
        }

        return score;
    }

    private static int GetTwoPartitionWeightCandidateLimit(TextureCompressionLevel compressionMode) =>
        compressionMode == TextureCompressionLevel.Exhaustive ? 64 : 8;

    private static int GetThreePartitionWeightCandidateLimit(TextureCompressionLevel compressionMode) =>
        compressionMode == TextureCompressionLevel.Exhaustive ? 64 : 8;

    private static bool TryBuildPartitionAssignments(
        int partitionSeed,
        int blockWidth,
        int blockHeight,
        Span<int> partitions) =>
        TryBuildPartitionAssignments(partitionCount: 2, partitionSeed, blockWidth, blockHeight, partitions);

    private static bool TryBuildPartitionAssignments(
        int partitionCount,
        int partitionSeed,
        int blockWidth,
        int blockHeight,
        Span<int> partitions)
    {
        Span<int> counts = stackalloc int[4];
        var texelIndex = 0;
        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var partition = GetPartitionIndex(partitionCount, partitionSeed, x, y, blockWidth, blockHeight);
                partitions[texelIndex++] = partition;
                counts[partition]++;
            }
        }

        for (var partition = 0; partition < partitionCount; partition++)
        {
            if (counts[partition] == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void FindLdrPartitionBounds(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> partitions,
        int partition,
        bool rgbOnly,
        out Rgba8UNorm low,
        out Rgba8UNorm high)
    {
        var minR = 255;
        var minG = 255;
        var minB = 255;
        var minA = rgbOnly ? 255 : 255;
        var maxR = 0;
        var maxG = 0;
        var maxB = 0;
        var maxA = rgbOnly ? 255 : 0;

        for (var i = 0; i < texelCount; i++)
        {
            if (partitions[i] != partition)
            {
                continue;
            }

            var color = storage[i];
            minR = Math.Min(minR, color.Red);
            minG = Math.Min(minG, color.Green);
            minB = Math.Min(minB, color.Blue);
            maxR = Math.Max(maxR, color.Red);
            maxG = Math.Max(maxG, color.Green);
            maxB = Math.Max(maxB, color.Blue);
            if (!rgbOnly)
            {
                minA = Math.Min(minA, color.Alpha);
                maxA = Math.Max(maxA, color.Alpha);
            }
        }

        low = new Rgba8UNorm((byte)minR, (byte)minG, (byte)minB, (byte)minA);
        high = new Rgba8UNorm((byte)maxR, (byte)maxG, (byte)maxB, (byte)maxA);
        NormalizeLdrEndpointOrder(ref low, ref high);
    }

    private static void BuildLdrPartitionedRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        AstcWeightGridCandidate candidate,
        ReadOnlySpan<int> partitions,
        Rgba8UNorm low0,
        Rgba8UNorm high0,
        Rgba8UNorm low1,
        Rgba8UNorm high1,
        Span<int> rawWeights)
    {
        Span<Rgba8UNorm> lows = stackalloc Rgba8UNorm[2];
        Span<Rgba8UNorm> highs = stackalloc Rgba8UNorm[2];
        lows[0] = low0;
        lows[1] = low1;
        highs[0] = high0;
        highs[1] = high1;
        BuildLdrPartitionedRawWeights(storage, blockWidth, blockHeight, candidate, partitions, lows, highs, rawWeights);
    }

    private static void BuildLdrPartitionedRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        AstcWeightGridCandidate candidate,
        ReadOnlySpan<int> partitions,
        ReadOnlySpan<Rgba8UNorm> lows,
        ReadOnlySpan<Rgba8UNorm> highs,
        Span<int> rawWeights)
    {
        Span<int> accumulators = stackalloc int[MaxWeightValues];
        Span<int> counts = stackalloc int[MaxWeightValues];
        var gridSize = candidate.Width * candidate.Height;

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var partition = partitions[texelIndex];
                var gridX = GetEncoderGridCoordinate(x, blockWidth, candidate.Width);
                var gridY = GetEncoderGridCoordinate(y, blockHeight, candidate.Height);
                var gridIndex = (gridY * candidate.Width) + gridX;
                accumulators[gridIndex] += QuantizeWeight(storage[texelIndex], lows[partition], highs[partition], candidate.WeightRange);
                counts[gridIndex]++;
            }
        }

        for (var i = 0; i < gridSize; i++)
        {
            rawWeights[i] = counts[i] == 0
                ? 0
                : Math.Clamp((accumulators[i] + (counts[i] / 2)) / counts[i], 0, candidate.WeightRange);
        }
    }

    private static bool TryOptimizeLdrPartitionEndpoints(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> partitions,
        ReadOnlySpan<int> texelWeights,
        int partition,
        bool rgbOnly,
        out Rgba8UNorm low,
        out Rgba8UNorm high)
    {
        if (!TryOptimizeLdrPartitionEndpointChannel(storage, texelCount, partitions, texelWeights, partition, 0, out var lowR, out var highR)
            || !TryOptimizeLdrPartitionEndpointChannel(storage, texelCount, partitions, texelWeights, partition, 1, out var lowG, out var highG)
            || !TryOptimizeLdrPartitionEndpointChannel(storage, texelCount, partitions, texelWeights, partition, 2, out var lowB, out var highB))
        {
            low = default;
            high = default;
            return false;
        }

        if (rgbOnly)
        {
            low = new Rgba8UNorm(lowR, lowG, lowB, 255);
            high = new Rgba8UNorm(highR, highG, highB, 255);
            return true;
        }

        if (!TryOptimizeLdrPartitionEndpointChannel(storage, texelCount, partitions, texelWeights, partition, 3, out var lowA, out var highA))
        {
            low = default;
            high = default;
            return false;
        }

        low = new Rgba8UNorm(lowR, lowG, lowB, lowA);
        high = new Rgba8UNorm(highR, highG, highB, highA);
        return true;
    }

    private static bool TryOptimizeLdrPartitionEndpointChannel(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> partitions,
        ReadOnlySpan<int> texelWeights,
        int partition,
        int channel,
        out byte low,
        out byte high)
    {
        var aa = 0.0;
        var ab = 0.0;
        var bb = 0.0;
        var ac = 0.0;
        var bc = 0.0;

        for (var i = 0; i < texelCount; i++)
        {
            if (partitions[i] != partition)
            {
                continue;
            }

            var weight1 = texelWeights[i];
            var weight0 = 64 - weight1;
            var value = GetRgbaChannel(storage[i], channel) * 64.0;
            aa += weight0 * weight0;
            ab += weight0 * weight1;
            bb += weight1 * weight1;
            ac += weight0 * value;
            bc += weight1 * value;
        }

        var determinant = (aa * bb) - (ab * ab);
        if (determinant <= double.Epsilon)
        {
            low = 0;
            high = 0;
            return false;
        }

        low = ClampToByte((int)Math.Round(((ac * bb) - (bc * ab)) / determinant));
        high = ClampToByte((int)Math.Round(((bc * aa) - (ac * ab)) / determinant));
        return true;
    }

    private static long GetLdrPartitionedSquaredError(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> partitions,
        ReadOnlySpan<int> texelWeights,
        Rgba8UNorm low0,
        Rgba8UNorm high0,
        Rgba8UNorm low1,
        Rgba8UNorm high1)
    {
        Span<Rgba8UNorm> lows = stackalloc Rgba8UNorm[2];
        Span<Rgba8UNorm> highs = stackalloc Rgba8UNorm[2];
        lows[0] = low0;
        lows[1] = low1;
        highs[0] = high0;
        highs[1] = high1;
        return GetLdrPartitionedSquaredError(storage, texelCount, partitions, texelWeights, lows, highs);
    }

    private static long GetLdrPartitionedSquaredError(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> partitions,
        ReadOnlySpan<int> texelWeights,
        ReadOnlySpan<Rgba8UNorm> lows,
        ReadOnlySpan<Rgba8UNorm> highs)
    {
        long error = 0;
        for (var i = 0; i < texelCount; i++)
        {
            var partition = partitions[i];
            var low = lows[partition];
            var high = highs[partition];
            var weight = texelWeights[i];
            var red = Interpolate(low.Red * 257, high.Red * 257, weight) >> 8;
            var green = Interpolate(low.Green * 257, high.Green * 257, weight) >> 8;
            var blue = Interpolate(low.Blue * 257, high.Blue * 257, weight) >> 8;
            var alpha = Interpolate(low.Alpha * 257, high.Alpha * 257, weight) >> 8;
            error += Squared(storage[i].Red - red);
            error += Squared(storage[i].Green - green);
            error += Squared(storage[i].Blue - blue);
            error += Squared(storage[i].Alpha - alpha);
        }

        return error;
    }

    private static void BuildLdrRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm low,
        Rgba8UNorm high,
        Span<int> rawWeights)
    {
        Span<int> accumulators = stackalloc int[MaxWeightValues];
        Span<int> counts = stackalloc int[MaxWeightValues];
        var gridSize = candidate.Width * candidate.Height;

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var gridX = GetEncoderGridCoordinate(x, blockWidth, candidate.Width);
                var gridY = GetEncoderGridCoordinate(y, blockHeight, candidate.Height);
                var gridIndex = (gridY * candidate.Width) + gridX;
                accumulators[gridIndex] += QuantizeWeight(storage[texelIndex], low, high, candidate.WeightRange);
                counts[gridIndex]++;
            }
        }

        for (var i = 0; i < gridSize; i++)
        {
            rawWeights[i] = counts[i] == 0
                ? 0
                : Math.Clamp((accumulators[i] + (counts[i] / 2)) / counts[i], 0, candidate.WeightRange);
        }
    }

    private static void BuildLdrDualPlaneRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm low,
        Rgba8UNorm high,
        int dualPlaneChannel,
        Span<int> rawWeights)
    {
        Span<int> sharedAccumulators = stackalloc int[MaxWeightValues];
        Span<int> planeAccumulators = stackalloc int[MaxWeightValues];
        Span<int> counts = stackalloc int[MaxWeightValues];
        var gridSize = candidate.Width * candidate.Height;

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var gridX = GetEncoderGridCoordinate(x, blockWidth, candidate.Width);
                var gridY = GetEncoderGridCoordinate(y, blockHeight, candidate.Height);
                var gridIndex = (gridY * candidate.Width) + gridX;
                sharedAccumulators[gridIndex] += QuantizeRgbWeightExcludingChannel(
                    storage[texelIndex],
                    low,
                    high,
                    candidate.WeightRange,
                    dualPlaneChannel);
                planeAccumulators[gridIndex] += QuantizeWeightChannel(
                    storage[texelIndex],
                    low,
                    high,
                    candidate.WeightRange,
                    dualPlaneChannel);
                counts[gridIndex]++;
            }
        }

        for (var i = 0; i < gridSize; i++)
        {
            var shared = counts[i] == 0
                ? 0
                : Math.Clamp((sharedAccumulators[i] + (counts[i] / 2)) / counts[i], 0, candidate.WeightRange);
            var plane = counts[i] == 0
                ? 0
                : Math.Clamp((planeAccumulators[i] + (counts[i] / 2)) / counts[i], 0, candidate.WeightRange);
            rawWeights[i * 2] = shared;
            rawWeights[(i * 2) + 1] = plane;
        }
    }

    private static void InfillLdrWeights(
        ReadOnlySpan<int> rawWeights,
        AstcWeightGridCandidate candidate,
        int blockWidth,
        int blockHeight,
        Span<int> texelWeights)
    {
        var gridWeights = new IntWeightBlock();
        var gridSize = candidate.Width * candidate.Height;
        for (var i = 0; i < gridSize; i++)
        {
            gridWeights[i] = UnquantizeWeight(rawWeights[i], candidate.WeightRange);
        }

        InfillWeights(gridWeights, candidate.Width, candidate.Height, blockWidth, blockHeight, texelWeights);
    }

    private static void InfillLdrDualPlaneWeights(
        ReadOnlySpan<int> rawWeights,
        AstcWeightGridCandidate candidate,
        int blockWidth,
        int blockHeight,
        Span<int> sharedTexelWeights,
        Span<int> planeTexelWeights)
    {
        var sharedGridWeights = new IntWeightBlock();
        var planeGridWeights = new IntWeightBlock();
        var gridSize = candidate.Width * candidate.Height;
        for (var i = 0; i < gridSize; i++)
        {
            sharedGridWeights[i] = UnquantizeWeight(rawWeights[i * 2], candidate.WeightRange);
            planeGridWeights[i] = UnquantizeWeight(rawWeights[(i * 2) + 1], candidate.WeightRange);
        }

        InfillWeights(sharedGridWeights, candidate.Width, candidate.Height, blockWidth, blockHeight, sharedTexelWeights);
        InfillWeights(planeGridWeights, candidate.Width, candidate.Height, blockWidth, blockHeight, planeTexelWeights);
    }

    private static bool ShouldRefineLdrWeights(int iterationLimit) => iterationLimit >= 4;

    private static long RefineLdrRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm low,
        Rgba8UNorm high,
        Span<int> rawWeights,
        Span<int> texelWeights,
        long bestError)
    {
        var weightValueCount = candidate.Width * candidate.Height;
        for (var i = 0; i < weightValueCount; i++)
        {
            var original = rawWeights[i];
            var bestValue = original;
            for (var delta = -1; delta <= 1; delta += 2)
            {
                var candidateWeight = original + delta;
                if ((uint)candidateWeight > (uint)candidate.WeightRange)
                {
                    continue;
                }

                rawWeights[i] = candidateWeight;
                InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);
                var error = GetLdrSquaredError(storage, texelCount, texelWeights, low, high);
                if (error < bestError)
                {
                    bestError = error;
                    bestValue = candidateWeight;
                }
            }

            rawWeights[i] = bestValue;
        }

        InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);
        return bestError;
    }

    private static long RefineLdrPartitionedRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        ReadOnlySpan<int> partitions,
        ReadOnlySpan<Rgba8UNorm> lows,
        ReadOnlySpan<Rgba8UNorm> highs,
        Span<int> rawWeights,
        Span<int> texelWeights,
        long bestError)
    {
        var weightValueCount = candidate.Width * candidate.Height;
        for (var i = 0; i < weightValueCount; i++)
        {
            var original = rawWeights[i];
            var bestValue = original;
            for (var delta = -1; delta <= 1; delta += 2)
            {
                var candidateWeight = original + delta;
                if ((uint)candidateWeight > (uint)candidate.WeightRange)
                {
                    continue;
                }

                rawWeights[i] = candidateWeight;
                InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);
                var error = GetLdrPartitionedSquaredError(storage, texelCount, partitions, texelWeights, lows, highs);
                if (error < bestError)
                {
                    bestError = error;
                    bestValue = candidateWeight;
                }
            }

            rawWeights[i] = bestValue;
        }

        InfillLdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);
        return bestError;
    }

    private static long RefineLdrDualPlaneRawWeights(
        ReadOnlySpan<Rgba8UNorm> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba8UNorm low,
        Rgba8UNorm high,
        int dualPlaneChannel,
        Span<int> rawWeights,
        Span<int> sharedTexelWeights,
        Span<int> planeTexelWeights,
        long bestError)
    {
        var weightValueCount = candidate.Width * candidate.Height * 2;
        for (var i = 0; i < weightValueCount; i++)
        {
            var original = rawWeights[i];
            var bestValue = original;
            for (var delta = -1; delta <= 1; delta += 2)
            {
                var candidateWeight = original + delta;
                if ((uint)candidateWeight > (uint)candidate.WeightRange)
                {
                    continue;
                }

                rawWeights[i] = candidateWeight;
                InfillLdrDualPlaneWeights(rawWeights, candidate, blockWidth, blockHeight, sharedTexelWeights, planeTexelWeights);
                var error = GetLdrDualPlaneSquaredError(
                    storage,
                    texelCount,
                    sharedTexelWeights,
                    planeTexelWeights,
                    low,
                    high,
                    dualPlaneChannel);
                if (error < bestError)
                {
                    bestError = error;
                    bestValue = candidateWeight;
                }
            }

            rawWeights[i] = bestValue;
        }

        InfillLdrDualPlaneWeights(rawWeights, candidate, blockWidth, blockHeight, sharedTexelWeights, planeTexelWeights);
        return bestError;
    }

    private static void QuantizeLdrEndpoints(
        Rgba8UNorm low,
        Rgba8UNorm high,
        int colorRange,
        Span<int> endpointValues,
        out Rgba8UNorm quantizedLow,
        out Rgba8UNorm quantizedHigh)
    {
        QuantizeEndpointByte(low.Red, colorRange, out endpointValues[0], out var lowR);
        QuantizeEndpointByte(high.Red, colorRange, out endpointValues[1], out var highR);
        QuantizeEndpointByte(low.Green, colorRange, out endpointValues[2], out var lowG);
        QuantizeEndpointByte(high.Green, colorRange, out endpointValues[3], out var highG);
        QuantizeEndpointByte(low.Blue, colorRange, out endpointValues[4], out var lowB);
        QuantizeEndpointByte(high.Blue, colorRange, out endpointValues[5], out var highB);
        QuantizeEndpointByte(low.Alpha, colorRange, out endpointValues[6], out var lowA);
        QuantizeEndpointByte(high.Alpha, colorRange, out endpointValues[7], out var highA);

        quantizedLow = new Rgba8UNorm(lowR, lowG, lowB, lowA);
        quantizedHigh = new Rgba8UNorm(highR, highG, highB, highA);
        if (GetRgbSum(quantizedHigh) >= GetRgbSum(quantizedLow))
        {
            return;
        }

        (endpointValues[0], endpointValues[1]) = (endpointValues[1], endpointValues[0]);
        (endpointValues[2], endpointValues[3]) = (endpointValues[3], endpointValues[2]);
        (endpointValues[4], endpointValues[5]) = (endpointValues[5], endpointValues[4]);
        (endpointValues[6], endpointValues[7]) = (endpointValues[7], endpointValues[6]);
        (quantizedLow, quantizedHigh) = (quantizedHigh, quantizedLow);
    }

    private static void QuantizeLdrRgbEndpoints(
        Rgba8UNorm low,
        Rgba8UNorm high,
        int colorRange,
        Span<int> endpointValues,
        out Rgba8UNorm quantizedLow,
        out Rgba8UNorm quantizedHigh)
    {
        QuantizeEndpointByte(low.Red, colorRange, out endpointValues[0], out var lowR);
        QuantizeEndpointByte(high.Red, colorRange, out endpointValues[1], out var highR);
        QuantizeEndpointByte(low.Green, colorRange, out endpointValues[2], out var lowG);
        QuantizeEndpointByte(high.Green, colorRange, out endpointValues[3], out var highG);
        QuantizeEndpointByte(low.Blue, colorRange, out endpointValues[4], out var lowB);
        QuantizeEndpointByte(high.Blue, colorRange, out endpointValues[5], out var highB);

        quantizedLow = new Rgba8UNorm(lowR, lowG, lowB, 255);
        quantizedHigh = new Rgba8UNorm(highR, highG, highB, 255);
        if (GetRgbSum(quantizedHigh) >= GetRgbSum(quantizedLow))
        {
            return;
        }

        (endpointValues[0], endpointValues[1]) = (endpointValues[1], endpointValues[0]);
        (endpointValues[2], endpointValues[3]) = (endpointValues[3], endpointValues[2]);
        (endpointValues[4], endpointValues[5]) = (endpointValues[5], endpointValues[4]);
        (quantizedLow, quantizedHigh) = (quantizedHigh, quantizedLow);
    }

    private static void QuantizeEndpointByte(byte value, int range, out int encoded, out byte decoded)
    {
        var bestValue = 0;
        var bestError = int.MaxValue;
        for (var candidate = 0; candidate <= range; candidate++)
        {
            var unquantized = UnquantizeEndpointValue(candidate, range);
            var error = Math.Abs(unquantized - value);
            if (error < bestError)
            {
                bestError = error;
                bestValue = candidate;
                if (error == 0)
                {
                    break;
                }
            }
        }

        encoded = bestValue;
        decoded = (byte)UnquantizeEndpointValue(bestValue, range);
    }

    private static bool TryOptimizeLdrEndpoints(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        out Rgba8UNorm low,
        out Rgba8UNorm high)
    {
        if (!TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 0, out var lowR, out var highR)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 1, out var lowG, out var highG)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 2, out var lowB, out var highB)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 3, out var lowA, out var highA))
        {
            low = default;
            high = default;
            return false;
        }

        low = new Rgba8UNorm(lowR, lowG, lowB, lowA);
        high = new Rgba8UNorm(highR, highG, highB, highA);
        return true;
    }

    private static bool TryOptimizeLdrRgbEndpoints(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        out Rgba8UNorm low,
        out Rgba8UNorm high)
    {
        if (!TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 0, out var lowR, out var highR)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 1, out var lowG, out var highG)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, texelWeights, 2, out var lowB, out var highB))
        {
            low = default;
            high = default;
            return false;
        }

        low = new Rgba8UNorm(lowR, lowG, lowB, 255);
        high = new Rgba8UNorm(highR, highG, highB, 255);
        return true;
    }

    private static bool TryOptimizeLdrDualPlaneRgbEndpoints(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> sharedTexelWeights,
        ReadOnlySpan<int> planeTexelWeights,
        int dualPlaneChannel,
        out Rgba8UNorm low,
        out Rgba8UNorm high)
    {
        if (!TryOptimizeLdrEndpointChannel(storage, texelCount, dualPlaneChannel == 0 ? planeTexelWeights : sharedTexelWeights, 0, out var lowR, out var highR)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, dualPlaneChannel == 1 ? planeTexelWeights : sharedTexelWeights, 1, out var lowG, out var highG)
            || !TryOptimizeLdrEndpointChannel(storage, texelCount, dualPlaneChannel == 2 ? planeTexelWeights : sharedTexelWeights, 2, out var lowB, out var highB))
        {
            low = default;
            high = default;
            return false;
        }

        low = new Rgba8UNorm(lowR, lowG, lowB, 255);
        high = new Rgba8UNorm(highR, highG, highB, 255);
        return true;
    }

    private static bool TryOptimizeLdrEndpointChannel(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        int channel,
        out byte low,
        out byte high)
    {
        var aa = 0.0;
        var ab = 0.0;
        var bb = 0.0;
        var ac = 0.0;
        var bc = 0.0;

        for (var i = 0; i < texelCount; i++)
        {
            var weight1 = texelWeights[i];
            var weight0 = 64 - weight1;
            var value = GetRgbaChannel(storage[i], channel) * 64.0;
            aa += weight0 * weight0;
            ab += weight0 * weight1;
            bb += weight1 * weight1;
            ac += weight0 * value;
            bc += weight1 * value;
        }

        var determinant = (aa * bb) - (ab * ab);
        if (determinant <= double.Epsilon)
        {
            low = 0;
            high = 0;
            return false;
        }

        low = ClampToByte((int)Math.Round(((ac * bb) - (bc * ab)) / determinant));
        high = ClampToByte((int)Math.Round(((bc * aa) - (ac * ab)) / determinant));
        return true;
    }

    private static bool TryFindPrincipalRgbaEndpoints(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        out Rgba8UNorm low,
        out Rgba8UNorm high)
    {
        var meanR = 0.0;
        var meanG = 0.0;
        var meanB = 0.0;
        var meanA = 0.0;
        for (var i = 0; i < texelCount; i++)
        {
            meanR += storage[i].Red;
            meanG += storage[i].Green;
            meanB += storage[i].Blue;
            meanA += storage[i].Alpha;
        }

        meanR /= texelCount;
        meanG /= texelCount;
        meanB /= texelCount;
        meanA /= texelCount;

        var rr = 0.0;
        var rg = 0.0;
        var rb = 0.0;
        var ra = 0.0;
        var gg = 0.0;
        var gb = 0.0;
        var ga = 0.0;
        var bb = 0.0;
        var ba = 0.0;
        var aa = 0.0;

        for (var i = 0; i < texelCount; i++)
        {
            var dr = storage[i].Red - meanR;
            var dg = storage[i].Green - meanG;
            var db = storage[i].Blue - meanB;
            var da = storage[i].Alpha - meanA;
            rr += dr * dr;
            rg += dr * dg;
            rb += dr * db;
            ra += dr * da;
            gg += dg * dg;
            gb += dg * db;
            ga += dg * da;
            bb += db * db;
            ba += db * da;
            aa += da * da;
        }

        var axisR = 1.0;
        var axisG = 0.0;
        var axisB = 0.0;
        var axisA = 0.0;
        if (gg > rr && gg >= bb && gg >= aa)
        {
            axisR = 0.0;
            axisG = 1.0;
        }
        else if (bb > rr && bb > gg && bb >= aa)
        {
            axisR = 0.0;
            axisB = 1.0;
        }
        else if (aa > rr && aa > gg && aa > bb)
        {
            axisR = 0.0;
            axisA = 1.0;
        }

        for (var i = 0; i < 6; i++)
        {
            var nextR = (rr * axisR) + (rg * axisG) + (rb * axisB) + (ra * axisA);
            var nextG = (rg * axisR) + (gg * axisG) + (gb * axisB) + (ga * axisA);
            var nextB = (rb * axisR) + (gb * axisG) + (bb * axisB) + (ba * axisA);
            var nextA = (ra * axisR) + (ga * axisG) + (ba * axisB) + (aa * axisA);
            if (!NormalizeVector(ref nextR, ref nextG, ref nextB, ref nextA))
            {
                low = default;
                high = default;
                return false;
            }

            axisR = nextR;
            axisG = nextG;
            axisB = nextB;
            axisA = nextA;
        }

        var lowIndex = 0;
        var highIndex = 0;
        var lowProjection = double.PositiveInfinity;
        var highProjection = double.NegativeInfinity;
        for (var i = 0; i < texelCount; i++)
        {
            var projection =
                ((storage[i].Red - meanR) * axisR)
                + ((storage[i].Green - meanG) * axisG)
                + ((storage[i].Blue - meanB) * axisB)
                + ((storage[i].Alpha - meanA) * axisA);
            if (projection < lowProjection)
            {
                lowProjection = projection;
                lowIndex = i;
            }

            if (projection > highProjection)
            {
                highProjection = projection;
                highIndex = i;
            }
        }

        if (lowIndex == highIndex)
        {
            low = default;
            high = default;
            return false;
        }

        low = storage[lowIndex];
        high = storage[highIndex];
        NormalizeLdrEndpointOrder(ref low, ref high);
        return true;
    }

    private static long GetLdrSquaredError(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        Rgba8UNorm low,
        Rgba8UNorm high)
    {
        long error = 0;
        for (var i = 0; i < texelCount; i++)
        {
            var weight = texelWeights[i];
            var red = Interpolate(low.Red * 257, high.Red * 257, weight) >> 8;
            var green = Interpolate(low.Green * 257, high.Green * 257, weight) >> 8;
            var blue = Interpolate(low.Blue * 257, high.Blue * 257, weight) >> 8;
            var alpha = Interpolate(low.Alpha * 257, high.Alpha * 257, weight) >> 8;
            error += Squared(storage[i].Red - red);
            error += Squared(storage[i].Green - green);
            error += Squared(storage[i].Blue - blue);
            error += Squared(storage[i].Alpha - alpha);
        }

        return error;
    }

    private static long GetLdrDualPlaneSquaredError(
        ReadOnlySpan<Rgba8UNorm> storage,
        int texelCount,
        ReadOnlySpan<int> sharedTexelWeights,
        ReadOnlySpan<int> planeTexelWeights,
        Rgba8UNorm low,
        Rgba8UNorm high,
        int dualPlaneChannel)
    {
        long error = 0;
        for (var i = 0; i < texelCount; i++)
        {
            var sharedWeight = sharedTexelWeights[i];
            var planeWeight = planeTexelWeights[i];
            var redWeight = dualPlaneChannel == 0 ? planeWeight : sharedWeight;
            var greenWeight = dualPlaneChannel == 1 ? planeWeight : sharedWeight;
            var blueWeight = dualPlaneChannel == 2 ? planeWeight : sharedWeight;
            var red = Interpolate(low.Red * 257, high.Red * 257, redWeight) >> 8;
            var green = Interpolate(low.Green * 257, high.Green * 257, greenWeight) >> 8;
            var blue = Interpolate(low.Blue * 257, high.Blue * 257, blueWeight) >> 8;
            error += Squared(storage[i].Red - red);
            error += Squared(storage[i].Green - green);
            error += Squared(storage[i].Blue - blue);
            error += Squared(storage[i].Alpha - byte.MaxValue);
        }

        return error;
    }

    private static int GetLdrEndpointOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 1,
        TextureCompressionLevel.High => 2,
        TextureCompressionLevel.Exhaustive => 4,
        _ => 0
    };

    private static void NormalizeLdrEndpointOrder(ref Rgba8UNorm low, ref Rgba8UNorm high)
    {
        if (GetRgbSum(high) < GetRgbSum(low))
        {
            (low, high) = (high, low);
        }
    }

    private static int GetRgbSum(Rgba8UNorm color) => color.Red + color.Green + color.Blue;

    private static bool IsOpaqueLdrBlock(ReadOnlySpan<Rgba8UNorm> storage, int texelCount)
    {
        for (var i = 0; i < texelCount; i++)
        {
            if (storage[i].Alpha != 255)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetRgbaChannel(Rgba8UNorm color, int channel) => channel switch
    {
        0 => color.Red,
        1 => color.Green,
        2 => color.Blue,
        _ => color.Alpha
    };

    private static int Squared(int value) => value * value;

    private static double Squared(double value) => value * value;

    private static void EncodeHdrBlock(
        ReadOnlySpan<Rgba16Float> source,
        int blockWidth,
        int blockHeight,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var texelCount = blockWidth * blockHeight;
        var storage = new Rgba16FloatTexelBlock();
        for (var i = 0; i < texelCount; i++)
        {
            storage[i] = SanitizeHdrColor(source[i]);
        }

        if (IsSolidBlock(source, texelCount, out var solidColor))
        {
            WriteHdrVoidExtentBlock(solidColor, destination);
            return;
        }

        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeHdrBlockFast(storage, blockWidth, blockHeight, texelCount, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeHdrBlockOptimized(storage, blockWidth, blockHeight, texelCount, compressionMode, destination);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported ASTC compression mode.");
        }
    }

    private static void EncodeHdrBlockFast(
        ReadOnlySpan<Rgba16Float> source,
        int blockWidth,
        int blockHeight,
        int texelCount,
        Span<byte> destination)
    {
        FindHdrBounds(source, texelCount, out var low, out var high);

        var weightAccumulators = new InlineArray16<int>();
        var weightCounts = new InlineArray16<int>();
        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var gridX = GetEncoderGridCoordinate(x, blockWidth);
                var gridY = GetEncoderGridCoordinate(y, blockHeight);
                var gridIndex = (gridY * 4) + gridX;
                weightAccumulators[gridIndex] += QuantizeWeight(source[texelIndex], low, high);
                weightCounts[gridIndex]++;
            }
        }

        uint weightStream = 0;
        for (var i = 0; i < 16; i++)
        {
            var weight = weightCounts[i] == 0
                ? 0
                : (weightAccumulators[i] + (weightCounts[i] / 2)) / weightCounts[i];
            weightStream |= (uint)Math.Clamp(weight, 0, 3) << (i * 2);
        }

        var lowR = HalfToLnsBits(low.Red);
        var highR = HalfToLnsBits(high.Red);
        var lowG = HalfToLnsBits(low.Green);
        var highG = HalfToLnsBits(high.Green);
        var lowB = HalfToLnsBits(low.Blue);
        var highB = HalfToLnsBits(high.Blue);

        UInt128 bits = 66;
        bits |= (UInt128)14 << 13;
        bits |= (UInt128)(lowR >> 8) << 17;
        bits |= (UInt128)(highR >> 8) << 25;
        bits |= (UInt128)(lowG >> 8) << 33;
        bits |= (UInt128)(highG >> 8) << 41;
        bits |= (UInt128)(0x80 | (lowB >> 9)) << 49;
        bits |= (UInt128)(0x80 | (highB >> 9)) << 57;
        bits |= (UInt128)RgbaColorConversions.ToUNorm8(low.Alpha) << 65;
        bits |= (UInt128)RgbaColorConversions.ToUNorm8(high.Alpha) << 73;
        bits |= (UInt128)ReverseBits32(weightStream) << 96;

        WriteBlockBits(bits, destination);
    }

    private static void EncodeHdrBlockOptimized(
        ReadOnlySpan<Rgba16Float> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        Span<AstcWeightGridCandidate> candidates = stackalloc AstcWeightGridCandidate[128];
        var candidateCount = GetWeightGridCandidates(blockWidth, blockHeight, compressionMode, candidates);
        if (candidateCount == 0)
        {
            EncodeHdrBlockFast(storage, blockWidth, blockHeight, texelCount, destination);
            return;
        }

        FindHdrBounds(storage, texelCount, out var boundsLow, out var boundsHigh);
        var best = new AstcHdrEncodingResult(UInt128.Zero, double.PositiveInfinity);
        var iterationLimit = GetHdrEndpointOptimizationIterationLimit(compressionMode);
        var hasPrincipalEndpoints = TryFindPrincipalHdrEndpoints(storage, texelCount, out var principalLow, out var principalHigh);
        var isOpaque = IsOpaqueHdrBlock(storage, texelCount);

        for (var i = 0; i < candidateCount; i++)
        {
            TryEncodeHdrRgbaCandidate(
                storage,
                blockWidth,
                blockHeight,
                texelCount,
                candidates[i],
                boundsLow,
                boundsHigh,
                iterationLimit,
                ref best);

            if (isOpaque)
            {
                TryEncodeHdrRgbCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidates[i],
                    boundsLow,
                    boundsHigh,
                    iterationLimit,
                    ref best);
            }

            if (hasPrincipalEndpoints)
            {
                TryEncodeHdrRgbaCandidate(
                    storage,
                    blockWidth,
                    blockHeight,
                    texelCount,
                    candidates[i],
                    principalLow,
                    principalHigh,
                    iterationLimit,
                    ref best);

                if (isOpaque)
                {
                    TryEncodeHdrRgbCandidate(
                        storage,
                        blockWidth,
                        blockHeight,
                        texelCount,
                        candidates[i],
                        principalLow,
                        principalHigh,
                        iterationLimit,
                        ref best);
                }
            }
        }

        if (double.IsPositiveInfinity(best.Error))
        {
            EncodeHdrBlockFast(storage, blockWidth, blockHeight, texelCount, destination);
            return;
        }

        WriteBlockBits(best.Bits, destination);
    }

    private static void TryEncodeHdrRgbaCandidate(
        ReadOnlySpan<Rgba16Float> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba16Float initialLow,
        Rgba16Float initialHigh,
        int iterationLimit,
        ref AstcHdrEncodingResult best)
    {
        if (!TryGetColorRange(candidate, colorValueCount: 8, out var colorRange))
        {
            return;
        }

        var low = initialLow;
        var high = initialHigh;
        var rawWeights = new IntWeightBlock();
        var texelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[8];
        var endpoint = default(AstcEndpointPair);

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            if (!TryQuantizeHdrEndpoints(low, high, colorRange, endpointValues, out endpoint))
            {
                return;
            }

            BuildHdrRawWeights(storage, blockWidth, blockHeight, candidate, endpoint, rawWeights);
            InfillHdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);

            if (pass == iterationLimit
                || !TryOptimizeHdrEndpoints(storage, texelCount, texelWeights, out low, out high))
            {
                break;
            }
        }

        var error = GetHdrSquaredError(storage, texelCount, texelWeights, endpoint);
        if (error >= best.Error)
        {
            return;
        }

        var bits = EncodeHdrSinglePartitionBlock(candidate, endpointMode: 14, endpointValues, endpointValueCount: 8, colorRange, rawWeights);
        best = new AstcHdrEncodingResult(bits, error);
    }

    private static void TryEncodeHdrRgbCandidate(
        ReadOnlySpan<Rgba16Float> storage,
        int blockWidth,
        int blockHeight,
        int texelCount,
        AstcWeightGridCandidate candidate,
        Rgba16Float initialLow,
        Rgba16Float initialHigh,
        int iterationLimit,
        ref AstcHdrEncodingResult best)
    {
        if (!TryGetColorRange(candidate, colorValueCount: 6, out var colorRange))
        {
            return;
        }

        var low = new Rgba16Float(initialLow.Red, initialLow.Green, initialLow.Blue, (Half)1f);
        var high = new Rgba16Float(initialHigh.Red, initialHigh.Green, initialHigh.Blue, (Half)1f);
        var rawWeights = new IntWeightBlock();
        var texelWeights = new IntTexelBlock();
        Span<int> endpointValues = stackalloc int[6];
        var endpoint = default(AstcEndpointPair);

        for (var pass = 0; pass <= iterationLimit; pass++)
        {
            if (!TryQuantizeHdrRgbEndpoints(low, high, colorRange, endpointValues, out endpoint))
            {
                return;
            }

            BuildHdrRawWeights(storage, blockWidth, blockHeight, candidate, endpoint, rawWeights);
            InfillHdrWeights(rawWeights, candidate, blockWidth, blockHeight, texelWeights);

            if (pass == iterationLimit
                || !TryOptimizeHdrRgbEndpoints(storage, texelCount, texelWeights, out low, out high))
            {
                break;
            }
        }

        var error = GetHdrSquaredError(storage, texelCount, texelWeights, endpoint);
        if (error >= best.Error)
        {
            return;
        }

        var bits = EncodeHdrSinglePartitionBlock(candidate, endpointMode: 11, endpointValues, endpointValueCount: 6, colorRange, rawWeights);
        best = new AstcHdrEncodingResult(bits, error);
    }

    private static UInt128 EncodeHdrSinglePartitionBlock(
        AstcWeightGridCandidate candidate,
        int endpointMode,
        ReadOnlySpan<int> endpointValues,
        int endpointValueCount,
        int colorRange,
        ReadOnlySpan<int> rawWeights)
    {
        var endpointBits = EncodeBiseSequence(endpointValues, endpointValueCount, colorRange);
        var weightValueCount = candidate.Width * candidate.Height;
        var weightBits = EncodeBiseSequence(rawWeights, weightValueCount, candidate.WeightRange);

        var bits = (UInt128)(uint)candidate.BlockMode;
        bits |= (UInt128)endpointMode << 13;
        bits |= endpointBits << 17;
        bits |= ReverseLowBits(weightBits, candidate.WeightBitCount) << (128 - candidate.WeightBitCount);
        return bits;
    }

    private static void BuildHdrRawWeights(
        ReadOnlySpan<Rgba16Float> storage,
        int blockWidth,
        int blockHeight,
        AstcWeightGridCandidate candidate,
        AstcEndpointPair endpoint,
        Span<int> rawWeights)
    {
        Span<int> accumulators = stackalloc int[MaxWeightValues];
        Span<int> counts = stackalloc int[MaxWeightValues];
        var low = GetHdrEndpointColor(endpoint, endpointIndex: 0);
        var high = GetHdrEndpointColor(endpoint, endpointIndex: 1);
        var gridSize = candidate.Width * candidate.Height;

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                var texelIndex = (y * blockWidth) + x;
                var gridX = GetEncoderGridCoordinate(x, blockWidth, candidate.Width);
                var gridY = GetEncoderGridCoordinate(y, blockHeight, candidate.Height);
                var gridIndex = (gridY * candidate.Width) + gridX;
                accumulators[gridIndex] += QuantizeWeight(storage[texelIndex], low, high, candidate.WeightRange);
                counts[gridIndex]++;
            }
        }

        for (var i = 0; i < gridSize; i++)
        {
            rawWeights[i] = counts[i] == 0
                ? 0
                : Math.Clamp((accumulators[i] + (counts[i] / 2)) / counts[i], 0, candidate.WeightRange);
        }
    }

    private static void InfillHdrWeights(
        ReadOnlySpan<int> rawWeights,
        AstcWeightGridCandidate candidate,
        int blockWidth,
        int blockHeight,
        Span<int> texelWeights)
    {
        var gridWeights = new IntWeightBlock();
        var gridSize = candidate.Width * candidate.Height;
        for (var i = 0; i < gridSize; i++)
        {
            gridWeights[i] = UnquantizeWeight(rawWeights[i], candidate.WeightRange);
        }

        InfillWeights(gridWeights, candidate.Width, candidate.Height, blockWidth, blockHeight, texelWeights);
    }

    private static bool TryQuantizeHdrEndpoints(
        Rgba16Float low,
        Rgba16Float high,
        int colorRange,
        Span<int> endpointValues,
        out AstcEndpointPair endpoint)
    {
        var lowR = HalfToLnsBits(low.Red);
        var highR = HalfToLnsBits(high.Red);
        var lowG = HalfToLnsBits(low.Green);
        var highG = HalfToLnsBits(high.Green);
        var lowB = HalfToLnsBits(low.Blue);
        var highB = HalfToLnsBits(high.Blue);

        if (!TryQuantizeHdrEndpointValue(lowR >> 8, colorRange, requireHighBit: false, out endpointValues[0])
            || !TryQuantizeHdrEndpointValue(highR >> 8, colorRange, requireHighBit: false, out endpointValues[1])
            || !TryQuantizeHdrEndpointValue(lowG >> 8, colorRange, requireHighBit: false, out endpointValues[2])
            || !TryQuantizeHdrEndpointValue(highG >> 8, colorRange, requireHighBit: false, out endpointValues[3])
            || !TryQuantizeHdrEndpointValue(0x80 | ((lowB >> 9) & 0x7F), colorRange, requireHighBit: true, out endpointValues[4])
            || !TryQuantizeHdrEndpointValue(0x80 | ((highB >> 9) & 0x7F), colorRange, requireHighBit: true, out endpointValues[5])
            || !TryQuantizeHdrEndpointValue(RgbaColorConversions.ToUNorm8(low.Alpha), colorRange, requireHighBit: false, out endpointValues[6])
            || !TryQuantizeHdrEndpointValue(RgbaColorConversions.ToUNorm8(high.Alpha), colorRange, requireHighBit: false, out endpointValues[7]))
        {
            endpoint = default;
            return false;
        }

        Span<int> decodedValues = stackalloc int[8];
        for (var i = 0; i < decodedValues.Length; i++)
        {
            decodedValues[i] = UnquantizeEndpointValue(endpointValues[i], colorRange);
        }

        endpoint = DecodeHdrRgbDirectLdrAlpha(decodedValues);
        return true;
    }

    private static bool TryQuantizeHdrRgbEndpoints(
        Rgba16Float low,
        Rgba16Float high,
        int colorRange,
        Span<int> endpointValues,
        out AstcEndpointPair endpoint)
    {
        var lowR = HalfToLnsBits(low.Red);
        var highR = HalfToLnsBits(high.Red);
        var lowG = HalfToLnsBits(low.Green);
        var highG = HalfToLnsBits(high.Green);
        var lowB = HalfToLnsBits(low.Blue);
        var highB = HalfToLnsBits(high.Blue);

        if (!TryQuantizeHdrEndpointValue(lowR >> 8, colorRange, requireHighBit: false, out endpointValues[0])
            || !TryQuantizeHdrEndpointValue(highR >> 8, colorRange, requireHighBit: false, out endpointValues[1])
            || !TryQuantizeHdrEndpointValue(lowG >> 8, colorRange, requireHighBit: false, out endpointValues[2])
            || !TryQuantizeHdrEndpointValue(highG >> 8, colorRange, requireHighBit: false, out endpointValues[3])
            || !TryQuantizeHdrEndpointValue(0x80 | ((lowB >> 9) & 0x7F), colorRange, requireHighBit: true, out endpointValues[4])
            || !TryQuantizeHdrEndpointValue(0x80 | ((highB >> 9) & 0x7F), colorRange, requireHighBit: true, out endpointValues[5]))
        {
            endpoint = default;
            return false;
        }

        Span<int> decodedValues = stackalloc int[6];
        for (var i = 0; i < decodedValues.Length; i++)
        {
            decodedValues[i] = UnquantizeEndpointValue(endpointValues[i], colorRange);
        }

        endpoint = DecodeHdrRgbDirect(decodedValues);
        return true;
    }

    private static bool TryQuantizeHdrEndpointValue(int value, int range, bool requireHighBit, out int encoded)
    {
        var bestValue = 0;
        var bestError = int.MaxValue;
        for (var candidate = 0; candidate <= range; candidate++)
        {
            var unquantized = UnquantizeEndpointValue(candidate, range);
            if (requireHighBit && (unquantized & 0x80) == 0)
            {
                continue;
            }

            var error = Math.Abs(unquantized - value);
            if (error < bestError)
            {
                bestError = error;
                bestValue = candidate;
                if (error == 0)
                {
                    break;
                }
            }
        }

        encoded = bestValue;
        return bestError < int.MaxValue;
    }

    private static bool TryOptimizeHdrEndpoints(
        ReadOnlySpan<Rgba16Float> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        out Rgba16Float low,
        out Rgba16Float high)
    {
        if (!TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 0, out var lowR, out var highR)
            || !TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 1, out var lowG, out var highG)
            || !TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 2, out var lowB, out var highB)
            || !TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 3, out var lowA, out var highA))
        {
            low = default;
            high = default;
            return false;
        }

        low = new Rgba16Float(ToHdrHalf(lowR), ToHdrHalf(lowG), ToHdrHalf(lowB), ToHdrHalf(lowA));
        high = new Rgba16Float(ToHdrHalf(highR), ToHdrHalf(highG), ToHdrHalf(highB), ToHdrHalf(highA));
        return true;
    }

    private static bool TryOptimizeHdrRgbEndpoints(
        ReadOnlySpan<Rgba16Float> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        out Rgba16Float low,
        out Rgba16Float high)
    {
        if (!TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 0, out var lowR, out var highR)
            || !TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 1, out var lowG, out var highG)
            || !TryOptimizeHdrEndpointChannel(storage, texelCount, texelWeights, 2, out var lowB, out var highB))
        {
            low = default;
            high = default;
            return false;
        }

        low = new Rgba16Float(ToHdrHalf(lowR), ToHdrHalf(lowG), ToHdrHalf(lowB), (Half)1f);
        high = new Rgba16Float(ToHdrHalf(highR), ToHdrHalf(highG), ToHdrHalf(highB), (Half)1f);
        return true;
    }

    private static bool TryOptimizeHdrEndpointChannel(
        ReadOnlySpan<Rgba16Float> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        int channel,
        out double low,
        out double high)
    {
        var aa = 0.0;
        var ab = 0.0;
        var bb = 0.0;
        var ac = 0.0;
        var bc = 0.0;

        for (var i = 0; i < texelCount; i++)
        {
            var weight1 = texelWeights[i];
            var weight0 = 64 - weight1;
            var value = GetHdrChannel(storage[i], channel) * 64.0;
            aa += weight0 * weight0;
            ab += weight0 * weight1;
            bb += weight1 * weight1;
            ac += weight0 * value;
            bc += weight1 * value;
        }

        var determinant = (aa * bb) - (ab * ab);
        if (determinant <= double.Epsilon)
        {
            low = 0.0;
            high = 0.0;
            return false;
        }

        low = Math.Max(0.0, ((ac * bb) - (bc * ab)) / determinant);
        high = Math.Max(0.0, ((bc * aa) - (ac * ab)) / determinant);
        return true;
    }

    private static bool TryFindPrincipalHdrEndpoints(
        ReadOnlySpan<Rgba16Float> storage,
        int texelCount,
        out Rgba16Float low,
        out Rgba16Float high)
    {
        var meanR = 0.0;
        var meanG = 0.0;
        var meanB = 0.0;
        var meanA = 0.0;
        for (var i = 0; i < texelCount; i++)
        {
            meanR += (double)storage[i].Red;
            meanG += (double)storage[i].Green;
            meanB += (double)storage[i].Blue;
            meanA += (double)storage[i].Alpha;
        }

        meanR /= texelCount;
        meanG /= texelCount;
        meanB /= texelCount;
        meanA /= texelCount;

        var rr = 0.0;
        var rg = 0.0;
        var rb = 0.0;
        var ra = 0.0;
        var gg = 0.0;
        var gb = 0.0;
        var ga = 0.0;
        var bb = 0.0;
        var ba = 0.0;
        var aa = 0.0;

        for (var i = 0; i < texelCount; i++)
        {
            var dr = (double)storage[i].Red - meanR;
            var dg = (double)storage[i].Green - meanG;
            var db = (double)storage[i].Blue - meanB;
            var da = (double)storage[i].Alpha - meanA;
            rr += dr * dr;
            rg += dr * dg;
            rb += dr * db;
            ra += dr * da;
            gg += dg * dg;
            gb += dg * db;
            ga += dg * da;
            bb += db * db;
            ba += db * da;
            aa += da * da;
        }

        var axisR = 1.0;
        var axisG = 0.0;
        var axisB = 0.0;
        var axisA = 0.0;
        if (gg > rr && gg >= bb && gg >= aa)
        {
            axisR = 0.0;
            axisG = 1.0;
        }
        else if (bb > rr && bb > gg && bb >= aa)
        {
            axisR = 0.0;
            axisB = 1.0;
        }
        else if (aa > rr && aa > gg && aa > bb)
        {
            axisR = 0.0;
            axisA = 1.0;
        }

        for (var i = 0; i < 6; i++)
        {
            var nextR = (rr * axisR) + (rg * axisG) + (rb * axisB) + (ra * axisA);
            var nextG = (rg * axisR) + (gg * axisG) + (gb * axisB) + (ga * axisA);
            var nextB = (rb * axisR) + (gb * axisG) + (bb * axisB) + (ba * axisA);
            var nextA = (ra * axisR) + (ga * axisG) + (ba * axisB) + (aa * axisA);
            if (!NormalizeVector(ref nextR, ref nextG, ref nextB, ref nextA))
            {
                low = default;
                high = default;
                return false;
            }

            axisR = nextR;
            axisG = nextG;
            axisB = nextB;
            axisA = nextA;
        }

        var lowIndex = 0;
        var highIndex = 0;
        var lowProjection = double.PositiveInfinity;
        var highProjection = double.NegativeInfinity;
        for (var i = 0; i < texelCount; i++)
        {
            var projection =
                (((double)storage[i].Red - meanR) * axisR)
                + (((double)storage[i].Green - meanG) * axisG)
                + (((double)storage[i].Blue - meanB) * axisB)
                + (((double)storage[i].Alpha - meanA) * axisA);
            if (projection < lowProjection)
            {
                lowProjection = projection;
                lowIndex = i;
            }

            if (projection > highProjection)
            {
                highProjection = projection;
                highIndex = i;
            }
        }

        if (lowIndex == highIndex)
        {
            low = default;
            high = default;
            return false;
        }

        low = storage[lowIndex];
        high = storage[highIndex];
        return true;
    }

    private static double GetHdrSquaredError(
        ReadOnlySpan<Rgba16Float> storage,
        int texelCount,
        ReadOnlySpan<int> texelWeights,
        AstcEndpointPair endpoint)
    {
        var error = 0.0;
        for (var i = 0; i < texelCount; i++)
        {
            var decoded = DecodeHdrTexel(endpoint, texelWeights[i], texelWeights[i], texelWeights[i], texelWeights[i]);
            error += Squared((double)storage[i].Red - (double)decoded.Red);
            error += Squared((double)storage[i].Green - (double)decoded.Green);
            error += Squared((double)storage[i].Blue - (double)decoded.Blue);
            error += Squared((double)storage[i].Alpha - (double)decoded.Alpha);
        }

        return error;
    }

    private static Rgba16Float GetHdrEndpointColor(AstcEndpointPair endpoint, int endpointIndex)
    {
        var weight = endpointIndex == 0 ? 0 : 64;
        return DecodeHdrTexel(endpoint, weight, weight, weight, weight);
    }

    private static int GetHdrEndpointOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 1,
        TextureCompressionLevel.High => 2,
        TextureCompressionLevel.Exhaustive => 4,
        _ => 0
    };

    private static bool IsOpaqueHdrBlock(ReadOnlySpan<Rgba16Float> storage, int texelCount)
    {
        var one = BitConverter.HalfToUInt16Bits((Half)1f);
        for (var i = 0; i < texelCount; i++)
        {
            if (BitConverter.HalfToUInt16Bits(storage[i].Alpha) != one)
            {
                return false;
            }
        }

        return true;
    }

    private static double GetHdrChannel(Rgba16Float color, int channel) => channel switch
    {
        0 => (double)color.Red,
        1 => (double)color.Green,
        2 => (double)color.Blue,
        _ => (double)color.Alpha
    };

    private static bool IsSolidBlock(ReadOnlySpan<Rgba8UNorm> source, int texelCount, out Rgba8UNorm color)
    {
        color = source[0];
        for (var i = 1; i < texelCount; i++)
        {
            if (!ColorEquals(source[i], color))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSolidBlock(ReadOnlySpan<Rgba16Float> source, int texelCount, out Rgba16Float color)
    {
        color = SanitizeHdrColor(source[0]);
        for (var i = 1; i < texelCount; i++)
        {
            if (!ColorEquals(SanitizeHdrColor(source[i]), color))
            {
                return false;
            }
        }

        return true;
    }

    private static void FindRgbaBounds(ReadOnlySpan<Rgba8UNorm> source, int texelCount, out Rgba8UNorm low, out Rgba8UNorm high)
    {
        var minR = 255;
        var minG = 255;
        var minB = 255;
        var minA = 255;
        var maxR = 0;
        var maxG = 0;
        var maxB = 0;
        var maxA = 0;

        for (var i = 0; i < texelCount; i++)
        {
            var color = source[i];
            minR = Math.Min(minR, color.Red);
            minG = Math.Min(minG, color.Green);
            minB = Math.Min(minB, color.Blue);
            minA = Math.Min(minA, color.Alpha);
            maxR = Math.Max(maxR, color.Red);
            maxG = Math.Max(maxG, color.Green);
            maxB = Math.Max(maxB, color.Blue);
            maxA = Math.Max(maxA, color.Alpha);
        }

        low = new Rgba8UNorm((byte)minR, (byte)minG, (byte)minB, (byte)minA);
        high = new Rgba8UNorm((byte)maxR, (byte)maxG, (byte)maxB, (byte)maxA);
    }

    private static void FindHdrBounds(ReadOnlySpan<Rgba16Float> source, int texelCount, out Rgba16Float low, out Rgba16Float high)
    {
        var minR = (double)Half.MaxValue;
        var minG = (double)Half.MaxValue;
        var minB = (double)Half.MaxValue;
        var minA = (double)Half.MaxValue;
        var maxR = 0.0;
        var maxG = 0.0;
        var maxB = 0.0;
        var maxA = 0.0;

        for (var i = 0; i < texelCount; i++)
        {
            var color = SanitizeHdrColor(source[i]);
            var red = (double)color.Red;
            var green = (double)color.Green;
            var blue = (double)color.Blue;
            var alpha = (double)color.Alpha;
            minR = Math.Min(minR, red);
            minG = Math.Min(minG, green);
            minB = Math.Min(minB, blue);
            minA = Math.Min(minA, alpha);
            maxR = Math.Max(maxR, red);
            maxG = Math.Max(maxG, green);
            maxB = Math.Max(maxB, blue);
            maxA = Math.Max(maxA, alpha);
        }

        low = new Rgba16Float(ToHdrHalf(minR), ToHdrHalf(minG), ToHdrHalf(minB), ToHdrHalf(minA));
        high = new Rgba16Float(ToHdrHalf(maxR), ToHdrHalf(maxG), ToHdrHalf(maxB), ToHdrHalf(maxA));
    }

    private static int QuantizeWeight(Rgba8UNorm color, Rgba8UNorm low, Rgba8UNorm high) =>
        QuantizeWeight(color, low, high, range: 3);

    private static int QuantizeWeight(Rgba8UNorm color, Rgba8UNorm low, Rgba8UNorm high, int range)
    {
        var deltaR = high.Red - low.Red;
        var deltaG = high.Green - low.Green;
        var deltaB = high.Blue - low.Blue;
        var deltaA = high.Alpha - low.Alpha;
        var denominator =
            (deltaR * deltaR)
            + (deltaG * deltaG)
            + (deltaB * deltaB)
            + (deltaA * deltaA);

        if (denominator == 0)
        {
            return 0;
        }

        var numerator =
            ((color.Red - low.Red) * deltaR)
            + ((color.Green - low.Green) * deltaG)
            + ((color.Blue - low.Blue) * deltaB)
            + ((color.Alpha - low.Alpha) * deltaA);
        var projected = numerator / (double)denominator;
        return (int)Math.Clamp(Math.Round(projected * range), 0.0, range);
    }

    private static int QuantizeRgbWeightExcludingChannel(
        Rgba8UNorm color,
        Rgba8UNorm low,
        Rgba8UNorm high,
        int range,
        int excludedChannel)
    {
        var denominator = 0;
        var numerator = 0;
        for (var channel = 0; channel < 3; channel++)
        {
            if (channel == excludedChannel)
            {
                continue;
            }

            var delta = GetRgbaChannel(high, channel) - GetRgbaChannel(low, channel);
            denominator += delta * delta;
            numerator += (GetRgbaChannel(color, channel) - GetRgbaChannel(low, channel)) * delta;
        }

        if (denominator == 0)
        {
            return 0;
        }

        var projected = numerator / (double)denominator;
        return (int)Math.Clamp(Math.Round(projected * range), 0.0, range);
    }

    private static int QuantizeWeightChannel(
        Rgba8UNorm color,
        Rgba8UNorm low,
        Rgba8UNorm high,
        int range,
        int channel)
    {
        var delta = GetRgbaChannel(high, channel) - GetRgbaChannel(low, channel);
        if (delta == 0)
        {
            return 0;
        }

        var projected = (GetRgbaChannel(color, channel) - GetRgbaChannel(low, channel)) / (double)delta;
        return (int)Math.Clamp(Math.Round(projected * range), 0.0, range);
    }

    private static int QuantizeWeight(Rgba16Float color, Rgba16Float low, Rgba16Float high) =>
        QuantizeWeight(color, low, high, range: 3);

    private static int QuantizeWeight(Rgba16Float color, Rgba16Float low, Rgba16Float high, int range)
    {
        color = SanitizeHdrColor(color);
        var deltaR = (double)high.Red - (double)low.Red;
        var deltaG = (double)high.Green - (double)low.Green;
        var deltaB = (double)high.Blue - (double)low.Blue;
        var deltaA = (double)high.Alpha - (double)low.Alpha;
        var denominator =
            (deltaR * deltaR)
            + (deltaG * deltaG)
            + (deltaB * deltaB)
            + (deltaA * deltaA);

        if (denominator == 0.0)
        {
            return 0;
        }

        var numerator =
            (((double)color.Red - (double)low.Red) * deltaR)
            + (((double)color.Green - (double)low.Green) * deltaG)
            + (((double)color.Blue - (double)low.Blue) * deltaB)
            + (((double)color.Alpha - (double)low.Alpha) * deltaA);
        var projected = numerator / denominator;
        return (int)Math.Clamp(Math.Round(projected * range), 0.0, range);
    }

    private static int GetEncoderGridCoordinate(int coordinate, int blockSize) =>
        GetEncoderGridCoordinate(coordinate, blockSize, gridSize: 4);

    private static int GetEncoderGridCoordinate(int coordinate, int blockSize, int gridSize) =>
        blockSize <= 1 || gridSize <= 1
            ? 0
            : ((coordinate * (gridSize - 1)) + ((blockSize - 1) / 2)) / (blockSize - 1);

    private static void WriteLdrVoidExtentBlock(Rgba8UNorm color, Span<byte> destination)
    {
        destination.Clear();
        ReadOnlySpan<byte> header = [0xFC, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        header.CopyTo(destination);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], ReplicateByte(color.Red));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], ReplicateByte(color.Green));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], ReplicateByte(color.Blue));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[14..], ReplicateByte(color.Alpha));
    }

    private static void WriteHdrVoidExtentBlock(Rgba16Float color, Span<byte> destination)
    {
        destination.Clear();
        ReadOnlySpan<byte> header = [0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        header.CopyTo(destination);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], BitConverter.HalfToUInt16Bits(color.Red));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], BitConverter.HalfToUInt16Bits(color.Green));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], BitConverter.HalfToUInt16Bits(color.Blue));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[14..], BitConverter.HalfToUInt16Bits(color.Alpha));
    }

    private static ushort ReplicateByte(byte value) => (ushort)((value << 8) | value);

    private static double SanitizeHdrValue(Half value)
    {
        var result = (double)value;
        if (double.IsNaN(result) || result < 0.0)
        {
            return 0.0;
        }

        return Math.Min(result, (double)Half.MaxValue);
    }

    private static Rgba16Float SanitizeHdrColor(Rgba16Float color) => new(
        ToHdrHalf(SanitizeHdrValue(color.Red)),
        ToHdrHalf(SanitizeHdrValue(color.Green)),
        ToHdrHalf(SanitizeHdrValue(color.Blue)),
        ToHdrHalf(SanitizeHdrValue(color.Alpha)));

    private static Half ToHdrHalf(double value)
    {
        if (double.IsNaN(value) || value <= 0.0)
        {
            return (Half)0f;
        }

        return (Half)Math.Min(value, (double)Half.MaxValue);
    }

    private static int HalfToLnsBits(Half value)
    {
        var bits = BitConverter.HalfToUInt16Bits(ToHdrHalf(SanitizeHdrValue(value)));
        bits = Math.Min(bits, (ushort)0x7BFF);
        var exponent = bits >> 10;
        var mantissa = bits & 0x3FF;

        int lnsMantissa;
        if (mantissa < 192)
        {
            lnsMantissa = ((mantissa * 8) + 1) / 3;
        }
        else if (mantissa < 704)
        {
            lnsMantissa = (mantissa + 64) * 2;
        }
        else
        {
            lnsMantissa = ((mantissa * 8) + 2050) / 5;
        }

        return (exponent << 11) | Math.Clamp(lnsMantissa, 0, 0x7FF);
    }

    private static Rgba8UNorm EncodeStorageColor(Rgba8UNorm color, bool srgb) => srgb
        ? new Rgba8UNorm(
            RgbaColorConversions.LinearUNorm8ToSrgb8(color.Red),
            RgbaColorConversions.LinearUNorm8ToSrgb8(color.Green),
            RgbaColorConversions.LinearUNorm8ToSrgb8(color.Blue),
            color.Alpha)
        : color;

    private static bool ColorEquals(Rgba8UNorm left, Rgba8UNorm right) =>
        left.Red == right.Red
        && left.Green == right.Green
        && left.Blue == right.Blue
        && left.Alpha == right.Alpha;

    private static bool ColorEquals(Rgba16Float left, Rgba16Float right) =>
        BitConverter.HalfToUInt16Bits(left.Red) == BitConverter.HalfToUInt16Bits(right.Red)
        && BitConverter.HalfToUInt16Bits(left.Green) == BitConverter.HalfToUInt16Bits(right.Green)
        && BitConverter.HalfToUInt16Bits(left.Blue) == BitConverter.HalfToUInt16Bits(right.Blue)
        && BitConverter.HalfToUInt16Bits(left.Alpha) == BitConverter.HalfToUInt16Bits(right.Alpha);

    private static Rgba8UNorm DecodeLdrTexel(AstcEndpointPair endpoint, int sharedWeight, int redWeight, int greenWeight, int blueWeight, int alphaWeight, bool srgb)
    {
        redWeight = endpoint.DualPlaneChannel == 0 ? redWeight : sharedWeight;
        greenWeight = endpoint.DualPlaneChannel == 1 ? greenWeight : sharedWeight;
        blueWeight = endpoint.DualPlaneChannel == 2 ? blueWeight : sharedWeight;
        alphaWeight = endpoint.DualPlaneChannel == 3 ? alphaWeight : sharedWeight;

        var red = (byte)(Interpolate(ExpandLdrEndpoint(endpoint.R0, srgb, 0), ExpandLdrEndpoint(endpoint.R1, srgb, 0), redWeight) >> 8);
        var green = (byte)(Interpolate(ExpandLdrEndpoint(endpoint.G0, srgb, 1), ExpandLdrEndpoint(endpoint.G1, srgb, 1), greenWeight) >> 8);
        var blue = (byte)(Interpolate(ExpandLdrEndpoint(endpoint.B0, srgb, 2), ExpandLdrEndpoint(endpoint.B1, srgb, 2), blueWeight) >> 8);
        var alpha = (byte)(Interpolate(endpoint.A0 * 257, endpoint.A1 * 257, alphaWeight) >> 8);

        return srgb
            ? new Rgba8UNorm(RgbaColorConversions.Srgb8ToLinearUNorm8(red), RgbaColorConversions.Srgb8ToLinearUNorm8(green), RgbaColorConversions.Srgb8ToLinearUNorm8(blue), alpha)
            : new Rgba8UNorm(red, green, blue, alpha);
    }

    private static Rgba16Float DecodeHdrTexel(AstcEndpointPair endpoint, int redWeight, int greenWeight, int blueWeight, int alphaWeight)
    {
        var red = Interpolate(ExpandHdrEndpoint(endpoint.R0, endpoint.RgbHdr), ExpandHdrEndpoint(endpoint.R1, endpoint.RgbHdr), redWeight);
        var green = Interpolate(ExpandHdrEndpoint(endpoint.G0, endpoint.RgbHdr), ExpandHdrEndpoint(endpoint.G1, endpoint.RgbHdr), greenWeight);
        var blue = Interpolate(ExpandHdrEndpoint(endpoint.B0, endpoint.RgbHdr), ExpandHdrEndpoint(endpoint.B1, endpoint.RgbHdr), blueWeight);
        var alpha = Interpolate(ExpandHdrEndpoint(endpoint.A0, endpoint.AlphaHdr), ExpandHdrEndpoint(endpoint.A1, endpoint.AlphaHdr), alphaWeight);

        return new Rgba16Float(
            DecodeHdrLane(red, endpoint.RgbHdr),
            DecodeHdrLane(green, endpoint.RgbHdr),
            DecodeHdrLane(blue, endpoint.RgbHdr),
            DecodeHdrLane(alpha, endpoint.AlphaHdr));
    }

    private static int GetDualPlaneWeight(AstcBlockInfo info, ReadOnlySpan<int> weights0, ReadOnlySpan<int> weights1, int texelIndex, int channel) =>
        info.DualPlane && info.DualPlaneChannel == channel ? weights1[texelIndex] : weights0[texelIndex];

    private static int ExpandLdrEndpoint(int value, bool srgb, int channel) =>
        srgb && channel != 3 ? ((value << 8) | 0x80) : value * 257;

    private static int ExpandHdrEndpoint(int value, bool hdr) => hdr ? value : value * 257;

    private static Half DecodeHdrLane(int value, bool hdr) =>
        hdr
            ? BitConverter.UInt16BitsToHalf(LnsToFloat16Bits(value))
            : RgbaColorConversions.ToHalf((ushort)value);

    private static int Interpolate(int endpoint0, int endpoint1, int weight) =>
        ((endpoint0 * (64 - weight)) + (endpoint1 * weight) + 32) >> 6;

    private static bool DecodeEndpointPairs(UInt128 bits, AstcBlockInfo info, Span<AstcEndpointPair> endpoints)
    {
        var colorValues = new InlineArray18<int>();
        DecodeBiseValues(bits, info.ColorStartBit, info.ColorBitCount, info.ColorRange, info.ColorValueCount, colorValues);

        for (var i = 0; i < info.ColorValueCount; i++)
        {
            colorValues[i] = UnquantizeEndpointValue(colorValues[i], info.ColorRange);
        }

        var colorOffset = 0;
        for (var partition = 0; partition < info.PartitionCount; partition++)
        {
            var mode = info.GetEndpointMode(partition);
            var valueCount = GetEndpointValueCount(mode);
            if (colorOffset + valueCount > info.ColorValueCount)
            {
                return false;
            }

            Span<int> colorValueSpan = colorValues;
            endpoints[partition] = DecodeEndpointPair(mode, colorValueSpan.Slice(colorOffset, valueCount));
            endpoints[partition].DualPlaneChannel = info.DualPlaneChannel;
            colorOffset += valueCount;
        }

        return colorOffset == info.ColorValueCount;
    }

    private static AstcEndpointPair DecodeEndpointPair(int mode, ReadOnlySpan<int> values)
    {
        return mode switch
        {
            0 => DecodeLdrLuminanceDirect(values),
            1 => DecodeLdrLuminanceBaseOffset(values),
            2 => DecodeHdrLuminanceLarge(values),
            3 => DecodeHdrLuminanceSmall(values),
            4 => DecodeLdrLuminanceAlphaDirect(values),
            5 => DecodeLdrLuminanceAlphaBaseOffset(values),
            6 => DecodeLdrRgbBaseScale(values),
            7 => DecodeHdrRgbBaseScale(values),
            8 => DecodeLdrRgbDirect(values),
            9 => DecodeLdrRgbBaseOffset(values),
            10 => DecodeLdrRgbBaseScaleAlpha(values),
            11 => DecodeHdrRgbDirect(values),
            12 => DecodeLdrRgbaDirect(values),
            13 => DecodeLdrRgbaBaseOffset(values),
            14 => DecodeHdrRgbDirectLdrAlpha(values),
            15 => DecodeHdrRgbaDirect(values),
            _ => default
        };
    }

    private static AstcEndpointPair DecodeLdrLuminanceDirect(ReadOnlySpan<int> v) =>
        LdrPair(v[0], v[0], v[0], 255, v[1], v[1], v[1], 255);

    private static AstcEndpointPair DecodeLdrLuminanceBaseOffset(ReadOnlySpan<int> v)
    {
        var low = (v[0] >> 2) | (v[1] & 0xC0);
        var high = Math.Min(low + (v[1] & 0x3F), 255);
        return LdrPair(low, low, low, 255, high, high, high, 255);
    }

    private static AstcEndpointPair DecodeLdrLuminanceAlphaDirect(ReadOnlySpan<int> v) =>
        LdrPair(v[0], v[0], v[0], v[2], v[1], v[1], v[1], v[3]);

    private static AstcEndpointPair DecodeLdrLuminanceAlphaBaseOffset(ReadOnlySpan<int> v)
    {
        TransferPrecision(v[1], v[0], out var deltaL, out var baseL);
        TransferPrecision(v[3], v[2], out var deltaA, out var baseA);
        return LdrPair(baseL, baseL, baseL, baseA, baseL + deltaL, baseL + deltaL, baseL + deltaL, baseA + deltaA);
    }

    private static AstcEndpointPair DecodeLdrRgbBaseScale(ReadOnlySpan<int> v) =>
        LdrPair((v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, 255, v[0], v[1], v[2], 255);

    private static AstcEndpointPair DecodeLdrRgbDirect(ReadOnlySpan<int> v)
    {
        if (v[1] + v[3] + v[5] < v[0] + v[2] + v[4])
        {
            return LdrPair((v[1] + v[5]) >> 1, (v[3] + v[5]) >> 1, v[5], 255, (v[0] + v[4]) >> 1, (v[2] + v[4]) >> 1, v[4], 255);
        }

        return LdrPair(v[0], v[2], v[4], 255, v[1], v[3], v[5], 255);
    }

    private static AstcEndpointPair DecodeLdrRgbBaseOffset(ReadOnlySpan<int> v)
    {
        TransferPrecision(v[1], v[0], out var deltaR, out var baseR);
        TransferPrecision(v[3], v[2], out var deltaG, out var baseG);
        TransferPrecision(v[5], v[4], out var deltaB, out var baseB);

        if (deltaR + deltaG + deltaB < 0)
        {
            return LdrPair((baseR + deltaR + baseB + deltaB) >> 1, (baseG + deltaG + baseB + deltaB) >> 1, baseB + deltaB, 255, (baseR + baseB) >> 1, (baseG + baseB) >> 1, baseB, 255);
        }

        return LdrPair(baseR, baseG, baseB, 255, baseR + deltaR, baseG + deltaG, baseB + deltaB, 255);
    }

    private static AstcEndpointPair DecodeLdrRgbBaseScaleAlpha(ReadOnlySpan<int> v) =>
        LdrPair((v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, v[4], v[0], v[1], v[2], v[5]);

    private static AstcEndpointPair DecodeLdrRgbaDirect(ReadOnlySpan<int> v)
    {
        if (v[1] + v[3] + v[5] < v[0] + v[2] + v[4])
        {
            return LdrPair((v[1] + v[5]) >> 1, (v[3] + v[5]) >> 1, v[5], v[7], (v[0] + v[4]) >> 1, (v[2] + v[4]) >> 1, v[4], v[6]);
        }

        return LdrPair(v[0], v[2], v[4], v[6], v[1], v[3], v[5], v[7]);
    }

    private static AstcEndpointPair DecodeLdrRgbaBaseOffset(ReadOnlySpan<int> v)
    {
        TransferPrecision(v[1], v[0], out var deltaR, out var baseR);
        TransferPrecision(v[3], v[2], out var deltaG, out var baseG);
        TransferPrecision(v[5], v[4], out var deltaB, out var baseB);
        TransferPrecision(v[7], v[6], out var deltaA, out var baseA);

        if (deltaR + deltaG + deltaB < 0)
        {
            return LdrPair((baseR + deltaR + baseB + deltaB) >> 1, (baseG + deltaG + baseB + deltaB) >> 1, baseB + deltaB, baseA + deltaA, (baseR + baseB) >> 1, (baseG + baseB) >> 1, baseB, baseA);
        }

        return LdrPair(baseR, baseG, baseB, baseA, baseR + deltaR, baseG + deltaG, baseB + deltaB, baseA + deltaA);
    }

    private static AstcEndpointPair DecodeHdrLuminanceLarge(ReadOnlySpan<int> v)
    {
        int y0;
        int y1;
        if (v[1] >= v[0])
        {
            y0 = v[0] << 4;
            y1 = v[1] << 4;
        }
        else
        {
            y0 = (v[1] << 4) + 8;
            y1 = (v[0] << 4) - 8;
        }

        return HdrPair(y0 << 4, y0 << 4, y0 << 4, LnsOne, y1 << 4, y1 << 4, y1 << 4, LnsOne, alphaHdr: true);
    }

    private static AstcEndpointPair DecodeHdrLuminanceSmall(ReadOnlySpan<int> v)
    {
        int y0;
        int y1;
        if ((v[0] & 0x80) != 0)
        {
            y0 = ((v[1] & 0xE0) << 4) | ((v[0] & 0x7F) << 2);
            y1 = (v[1] & 0x1F) << 2;
        }
        else
        {
            y0 = ((v[1] & 0xF0) << 4) | ((v[0] & 0x7F) << 1);
            y1 = (v[1] & 0x0F) << 1;
        }

        y1 = Math.Min(y0 + y1, 0xFFF);
        return HdrPair(y0 << 4, y0 << 4, y0 << 4, LnsOne, y1 << 4, y1 << 4, y1 << 4, LnsOne, alphaHdr: true);
    }

    private static AstcEndpointPair DecodeHdrRgbBaseScale(ReadOnlySpan<int> v)
    {
        var modeValue = ((v[0] & 0xC0) >> 6) | (((v[1] & 0x80) >> 7) << 2) | (((v[2] & 0x80) >> 7) << 3);

        int majorComponent;
        int mode;
        if ((modeValue & 0xC) != 0xC)
        {
            majorComponent = modeValue >> 2;
            mode = modeValue & 3;
        }
        else if (modeValue != 0xF)
        {
            majorComponent = modeValue & 3;
            mode = 4;
        }
        else
        {
            majorComponent = 0;
            mode = 5;
        }

        var red = v[0] & 0x3F;
        var green = v[1] & 0x1F;
        var blue = v[2] & 0x1F;
        var scale = v[3] & 0x1F;
        var oneHot = 1 << mode;

        AddBitIf(ref green, oneHot, 0x30, (v[1] >> 6) & 1, 6);
        AddBitIf(ref green, oneHot, 0x3A, (v[1] >> 5) & 1, 5);
        AddBitIf(ref blue, oneHot, 0x30, (v[2] >> 6) & 1, 6);
        AddBitIf(ref blue, oneHot, 0x3A, (v[2] >> 5) & 1, 5);
        AddBitIf(ref scale, oneHot, 0x3D, (v[3] >> 5) & 1, 5);
        AddBitIf(ref scale, oneHot, 0x2D, (v[3] >> 6) & 1, 6);
        AddBitIf(ref scale, oneHot, 0x04, (v[3] >> 7) & 1, 7);
        AddBitIf(ref red, oneHot, 0x3B, (v[3] >> 7) & 1, 6);
        AddBitIf(ref red, oneHot, 0x04, (v[2] >> 5) & 1, 6);
        AddBitIf(ref red, oneHot, 0x10, (v[3] >> 6) & 1, 7);
        AddBitIf(ref red, oneHot, 0x0F, (v[2] >> 6) & 1, 7);
        AddBitIf(ref red, oneHot, 0x05, (v[1] >> 5) & 1, 8);
        AddBitIf(ref red, oneHot, 0x0A, (v[1] >> 6) & 1, 8);
        AddBitIf(ref red, oneHot, 0x05, (v[1] >> 6) & 1, 9);
        AddBitIf(ref red, oneHot, 0x02, (v[3] >> 5) & 1, 9);
        AddBitIf(ref red, oneHot, 0x01, (v[2] >> 5) & 1, 10);
        AddBitIf(ref red, oneHot, 0x02, (v[3] >> 6) & 1, 10);

        var shift = mode switch
        {
            0 or 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            _ => 5
        };

        red <<= shift;
        green <<= shift;
        blue <<= shift;
        scale <<= shift;

        if (mode != 5)
        {
            green = red - green;
            blue = red - blue;
        }

        return HdrRgbPairWithComponentSwap(
            Math.Max(red - scale, 0),
            Math.Max(green - scale, 0),
            Math.Max(blue - scale, 0),
            Math.Max(red, 0),
            Math.Max(green, 0),
            Math.Max(blue, 0),
            majorComponent);
    }

    private static AstcEndpointPair DecodeHdrRgbDirect(ReadOnlySpan<int> v)
    {
        var modeValue = ((v[1] & 0x80) >> 7) | (((v[2] & 0x80) >> 7) << 1) | (((v[3] & 0x80) >> 7) << 2);
        var majorComponent = ((v[4] & 0x80) >> 7) | (((v[5] & 0x80) >> 7) << 1);

        if (majorComponent == 3)
        {
            return HdrPair(v[0] << 8, v[2] << 8, (v[4] & 0x7F) << 9, LnsOne, v[1] << 8, v[3] << 8, (v[5] & 0x7F) << 9, LnsOne, alphaHdr: true);
        }

        var a = v[0] | ((v[1] & 0x40) << 2);
        var b0 = v[2] & 0x3F;
        var b1 = v[3] & 0x3F;
        var c = v[1] & 0x3F;
        var d0 = v[4] & 0x7F;
        var d1 = v[5] & 0x7F;
        var oneHot = 1 << modeValue;

        AddBitIf(ref a, oneHot, 0xA4, (v[2] >> 6) & 1, 9);
        AddBitIf(ref a, oneHot, 0x08, (v[4] >> 6) & 1, 9);
        AddBitIf(ref a, oneHot, 0x50, (v[4] >> 5) & 1, 9);
        AddBitIf(ref a, oneHot, 0x50, (v[5] >> 5) & 1, 10);
        AddBitIf(ref a, oneHot, 0xA0, (v[3] >> 6) & 1, 10);
        AddBitIf(ref a, oneHot, 0xC0, (v[4] >> 6) & 1, 11);
        AddBitIf(ref c, oneHot, 0x04, (v[3] >> 6) & 1, 6);
        AddBitIf(ref c, oneHot, 0xE8, (v[5] >> 6) & 1, 6);
        AddBitIf(ref c, oneHot, 0x20, (v[4] >> 6) & 1, 7);

        if ((oneHot & 0x5B) != 0)
        {
            b0 |= ((v[2] >> 6) & 1) << 6;
            b1 |= ((v[3] >> 6) & 1) << 6;
        }

        if ((oneHot & 0x12) != 0)
        {
            b0 |= ((v[4] >> 6) & 1) << 7;
            b1 |= ((v[5] >> 6) & 1) << 7;
        }

        if ((oneHot & 0xAF) != 0)
        {
            d0 |= ((v[4] >> 5) & 1) << 5;
            d1 |= ((v[5] >> 5) & 1) << 5;
        }

        if ((oneHot & 0x05) != 0)
        {
            d0 |= ((v[4] >> 6) & 1) << 6;
            d1 |= ((v[5] >> 6) & 1) << 6;
        }

        var deltaBits = modeValue switch
        {
            0 or 2 => 7,
            4 or 6 => 5,
            _ => 6
        };
        d0 = SignExtend(d0, deltaBits);
        d1 = SignExtend(d1, deltaBits);

        var valueShift = (modeValue >> 1) ^ 3;
        a = SafeLeftShift(a, valueShift);
        b0 = SafeLeftShift(b0, valueShift);
        b1 = SafeLeftShift(b1, valueShift);
        c = SafeLeftShift(c, valueShift);
        d0 = SafeLeftShift(d0, valueShift);
        d1 = SafeLeftShift(d1, valueShift);

        return HdrRgbPairWithComponentSwap(
            Math.Clamp(a - c, 0, 0xFFF),
            Math.Clamp(a - b0 - c - d0, 0, 0xFFF),
            Math.Clamp(a - b1 - c - d1, 0, 0xFFF),
            Math.Clamp(a, 0, 0xFFF),
            Math.Clamp(a - b0, 0, 0xFFF),
            Math.Clamp(a - b1, 0, 0xFFF),
            majorComponent);
    }

    private static AstcEndpointPair DecodeHdrRgbDirectLdrAlpha(ReadOnlySpan<int> v)
    {
        var rgb = DecodeHdrRgbDirect(v);
        rgb.A0 = v[6];
        rgb.A1 = v[7];
        rgb.AlphaHdr = false;
        return rgb;
    }

    private static AstcEndpointPair DecodeHdrRgbaDirect(ReadOnlySpan<int> v)
    {
        var rgba = DecodeHdrRgbDirect(v);
        UnpackHdrAlpha(v[6], v[7], out rgba.A0, out rgba.A1);
        rgba.AlphaHdr = true;
        return rgba;
    }

    private static void UnpackHdrAlpha(int v6, int v7, out int low, out int high)
    {
        var mode = ((v6 >> 7) & 1) | ((v7 >> 6) & 2);
        v6 &= 0x7F;
        v7 &= 0x7F;

        int a0;
        int a1;
        if (mode == 3)
        {
            a0 = v6 << 5;
            a1 = v7 << 5;
        }
        else
        {
            v6 |= (v7 << (mode + 1)) & 0x780;
            v7 &= 0x3F >> mode;
            v7 ^= 32 >> mode;
            v7 -= 32 >> mode;
            v6 <<= 4 - mode;
            v7 = SafeLeftShift(v7, 4 - mode);
            a0 = v6;
            a1 = Math.Clamp(v6 + v7, 0, 0xFFF);
        }

        low = Math.Clamp(a0, 0, 0xFFF) << 4;
        high = Math.Clamp(a1, 0, 0xFFF) << 4;
    }

    private static AstcEndpointPair HdrRgbPairWithComponentSwap(int red0, int green0, int blue0, int red1, int green1, int blue1, int majorComponent)
    {
        switch (majorComponent)
        {
            case 1:
                (red0, green0) = (green0, red0);
                (red1, green1) = (green1, red1);
                break;
            case 2:
                (red0, blue0) = (blue0, red0);
                (red1, blue1) = (blue1, red1);
                break;
        }

        return HdrPair(red0 << 4, green0 << 4, blue0 << 4, LnsOne, red1 << 4, green1 << 4, blue1 << 4, LnsOne, alphaHdr: true);
    }

    private static AstcEndpointPair LdrPair(int r0, int g0, int b0, int a0, int r1, int g1, int b1, int a1) => new()
    {
        R0 = ClampToByte(r0),
        G0 = ClampToByte(g0),
        B0 = ClampToByte(b0),
        A0 = ClampToByte(a0),
        R1 = ClampToByte(r1),
        G1 = ClampToByte(g1),
        B1 = ClampToByte(b1),
        A1 = ClampToByte(a1)
    };

    private static AstcEndpointPair HdrPair(int r0, int g0, int b0, int a0, int r1, int g1, int b1, int a1, bool alphaHdr) => new()
    {
        R0 = Math.Clamp(r0, 0, 0xFFFF),
        G0 = Math.Clamp(g0, 0, 0xFFFF),
        B0 = Math.Clamp(b0, 0, 0xFFFF),
        A0 = Math.Clamp(a0, 0, 0xFFFF),
        R1 = Math.Clamp(r1, 0, 0xFFFF),
        G1 = Math.Clamp(g1, 0, 0xFFFF),
        B1 = Math.Clamp(b1, 0, 0xFFFF),
        A1 = Math.Clamp(a1, 0, 0xFFFF),
        RgbHdr = true,
        AlphaHdr = alphaHdr
    };

    private static byte ClampToByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private static void TransferPrecision(int aSource, int bSource, out int a, out int b)
    {
        a = aSource >> 1;
        b = (bSource >> 1) | (aSource & 0x80);
        a &= 0x3F;
        if ((a & 0x20) != 0)
        {
            a -= 0x40;
        }
    }

    private static void AddBitIf(ref int target, int oneHotMode, int modeMask, int bit, int shift)
    {
        if ((oneHotMode & modeMask) != 0)
        {
            target |= bit << shift;
        }
    }

    private static int SignExtend(int value, int bits)
    {
        var shift = 32 - bits;
        return (value << shift) >> shift;
    }

    private static int SafeLeftShift(int value, int shift) => (int)((uint)value << shift);

    private static void ValidateCompressionMode(TextureCompressionLevel compressionMode)
    {
        if (compressionMode is not (TextureCompressionLevel.Fast
            or TextureCompressionLevel.Normal
            or TextureCompressionLevel.High
            or TextureCompressionLevel.Exhaustive))
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressionMode),
                compressionMode,
                "Unsupported ASTC compression mode.");
        }
    }

    private static bool NormalizeVector(ref double x, ref double y, ref double z, ref double w)
    {
        var length = Math.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
        if (length <= double.Epsilon)
        {
            return false;
        }

        x /= length;
        y /= length;
        z /= length;
        w /= length;
        return true;
    }

    private static bool DecodeWeights(UInt128 bits, AstcBlockInfo info, int blockWidth, int blockHeight, Span<int> texelWeights0, Span<int> texelWeights1)
    {
        var gridSize = info.WeightWidth * info.WeightHeight;
        var rawWeightCount = info.DualPlane ? gridSize * 2 : gridSize;
        if (rawWeightCount > MaxWeightValues)
        {
            return false;
        }

        var rawWeights = new IntWeightBlock();
        var gridWeights0 = new IntWeightBlock();
        var gridWeights1 = new IntWeightBlock();
        DecodeBiseWeights(bits, info.WeightBitCount, info.WeightRange, rawWeightCount, rawWeights);

        if (info.DualPlane)
        {
            for (var i = 0; i < gridSize; i++)
            {
                gridWeights0[i] = UnquantizeWeight(rawWeights[i * 2], info.WeightRange);
                gridWeights1[i] = UnquantizeWeight(rawWeights[(i * 2) + 1], info.WeightRange);
            }
        }
        else
        {
            for (var i = 0; i < gridSize; i++)
            {
                gridWeights0[i] = UnquantizeWeight(rawWeights[i], info.WeightRange);
            }
        }

        InfillWeights(gridWeights0, info.WeightWidth, info.WeightHeight, blockWidth, blockHeight, texelWeights0);
        if (info.DualPlane)
        {
            InfillWeights(gridWeights1, info.WeightWidth, info.WeightHeight, blockWidth, blockHeight, texelWeights1);
        }

        return true;
    }

    private static void InfillWeights(ReadOnlySpan<int> gridWeights, int gridWidth, int gridHeight, int blockWidth, int blockHeight, Span<int> texelWeights)
    {
        if (gridWidth == blockWidth && gridHeight == blockHeight)
        {
            gridWeights[..(blockWidth * blockHeight)].CopyTo(texelWeights);
            return;
        }

        var ds = (1024 + (blockWidth / 2)) / (blockWidth - 1);
        var dt = (1024 + (blockHeight / 2)) / (blockHeight - 1);
        var texelIndex = 0;
        for (var y = 0; y < blockHeight; y++)
        {
            var ct = dt * y;
            var gt = ((ct * (gridHeight - 1)) + 32) >> 6;
            var jt = gt >> 4;
            var ft = gt & 0xF;
            for (var x = 0; x < blockWidth; x++)
            {
                var cs = ds * x;
                var gs = ((cs * (gridWidth - 1)) + 32) >> 6;
                var js = gs >> 4;
                var fs = gs & 0xF;

                var p00 = GetGridWeight(gridWeights, gridWidth, gridHeight, js, jt);
                var p01 = GetGridWeight(gridWeights, gridWidth, gridHeight, js + 1, jt);
                var p10 = GetGridWeight(gridWeights, gridWidth, gridHeight, js, jt + 1);
                var p11 = GetGridWeight(gridWeights, gridWidth, gridHeight, js + 1, jt + 1);

                var factor11 = ((fs * ft) + 8) >> 4;
                var factor10 = ft - factor11;
                var factor01 = fs - factor11;
                var factor00 = 16 - fs - ft + factor11;
                var weight = (8
                    + (p00 * factor00)
                    + (p01 * factor01)
                    + (p10 * factor10)
                    + (p11 * factor11)) >> 4;

                texelWeights[texelIndex++] = weight;
            }
        }
    }

    private static int GetGridWeight(ReadOnlySpan<int> gridWeights, int gridWidth, int gridHeight, int x, int y) =>
        x >= gridWidth || y >= gridHeight ? 0 : gridWeights[(y * gridWidth) + x];

    private static bool TryDecodeBlockInfo(UInt128 bits, int blockWidth, int blockHeight, out AstcBlockInfo info)
    {
        var lowBits = (ulong)bits;
        if ((lowBits & 0x1FFUL) == 0x1FC)
        {
            if (GetBits(bits, 10, 2) != 0x3)
            {
                info = default;
                return false;
            }

            var lowS = (int)GetBits(bits, 12, 13);
            var highS = (int)GetBits(bits, 25, 13);
            var lowT = (int)GetBits(bits, 38, 13);
            var highT = (int)GetBits(bits, 51, 13);
            var allOnes = lowS == 0x1FFF && highS == 0x1FFF && lowT == 0x1FFF && highT == 0x1FFF;
            if (!allOnes && (lowS >= highS || lowT >= highT))
            {
                info = default;
                return false;
            }

            info = new AstcBlockInfo
            {
                IsVoidExtent = true,
                VoidExtentIsHdr = ((lowBits >> 9) & 1) != 0
            };
            return true;
        }

        if (!TryDecodeWeightGrid(lowBits, out var weightWidth, out var weightHeight, out var weightRange, out var widthA6HeightB6))
        {
            info = default;
            return false;
        }

        if (weightWidth > blockWidth || weightHeight > blockHeight)
        {
            info = default;
            return false;
        }

        var dualPlane = !widthA6HeightB6 && ((lowBits >> 10) & 1) != 0;
        var partitionCount = 1 + (int)((lowBits >> 11) & 0x3);
        var weightValueCount = weightWidth * weightHeight * (dualPlane ? 2 : 1);
        if (weightValueCount > MaxWeightValues || (dualPlane && partitionCount == 4))
        {
            info = default;
            return false;
        }

        var weightBitCount = GetBiseBitCount(weightValueCount, weightRange);
        if (weightBitCount is < 24 or > 96)
        {
            info = default;
            return false;
        }

        var endpointModes = new InlineArray4<int>();
        var colorValueCount = DecodeEndpointModes(bits, lowBits, partitionCount, weightBitCount, endpointModes, out var extraCemBits);
        if (colorValueCount is < 1 or > 18)
        {
            info = default;
            return false;
        }

        var dualPlaneBitStart = 128 - weightBitCount - extraCemBits;
        if (dualPlane)
        {
            dualPlaneBitStart -= 2;
        }

        var colorStartBit = partitionCount == 1 ? 17 : 29;
        var maxColorBits = dualPlaneBitStart - colorStartBit;
        if (!TryFitColorRange(colorValueCount, maxColorBits, out var colorRange, out var colorBitCount))
        {
            info = default;
            return false;
        }

        info = new AstcBlockInfo
        {
            WeightWidth = weightWidth,
            WeightHeight = weightHeight,
            WeightRange = weightRange,
            WeightBitCount = weightBitCount,
            PartitionCount = partitionCount,
            PartitionIndex = partitionCount == 1 ? 0 : (int)GetBits(bits, 13, 10),
            DualPlane = dualPlane,
            DualPlaneChannel = dualPlane ? (int)GetBits(bits, dualPlaneBitStart, 2) : -1,
            ColorStartBit = colorStartBit,
            ColorBitCount = colorBitCount,
            ColorRange = colorRange,
            ColorValueCount = colorValueCount,
            EndpointMode0 = endpointModes[0],
            EndpointMode1 = endpointModes[1],
            EndpointMode2 = endpointModes[2],
            EndpointMode3 = endpointModes[3]
        };
        return true;
    }

    private static bool TryDecodeWeightGrid(ulong lowBits, out int width, out int height, out int range, out bool widthA6HeightB6)
    {
        widthA6HeightB6 = false;
        uint rangeSelector;

        if ((lowBits & 0x3) != 0)
        {
            var modeBits = (int)((lowBits >> 2) & 0x3);
            var a = (int)((lowBits >> 5) & 0x3);
            var b = (int)((lowBits >> 7) & 0x3);
            switch (modeBits)
            {
                case 0:
                    width = b + 4;
                    height = a + 2;
                    break;
                case 1:
                    width = b + 8;
                    height = a + 2;
                    break;
                case 2:
                    width = a + 2;
                    height = b + 8;
                    break;
                default:
                    if (((lowBits >> 8) & 1) != 0)
                    {
                        width = (b & 1) + 2;
                        height = a + 2;
                    }
                    else
                    {
                        width = a + 2;
                        height = (b & 1) + 6;
                    }

                    break;
            }

            rangeSelector = (uint)(((lowBits >> 4) & 1) | ((lowBits & 0x3) << 1));
        }
        else
        {
            if ((lowBits & 0xF) == 0)
            {
                width = 0;
                height = 0;
                range = 0;
                return false;
            }

            var a = (int)((lowBits >> 5) & 0x3);
            var b = (int)((lowBits >> 9) & 0x3);
            switch ((int)((lowBits >> 7) & 0x3))
            {
                case 0:
                    width = 12;
                    height = a + 2;
                    break;
                case 1:
                    width = a + 2;
                    height = 12;
                    break;
                case 2:
                    width = a + 6;
                    height = b + 6;
                    widthA6HeightB6 = true;
                    break;
                default:
                    if (a == 0)
                    {
                        width = 6;
                        height = 10;
                    }
                    else if (a == 1)
                    {
                        width = 10;
                        height = 6;
                    }
                    else
                    {
                        width = 0;
                        height = 0;
                        range = 0;
                        return false;
                    }

                    break;
            }

            rangeSelector = (uint)(((lowBits >> 4) & 1) | (((lowBits >> 2) & 0x3) << 1));
        }

        var highPrecision = widthA6HeightB6 ? 0u : (uint)((lowBits >> 9) & 1);
        range = GetWeightRange((int)((highPrecision << 3) | rangeSelector));
        return range > 0;
    }

    private static int DecodeEndpointModes(UInt128 bits, ulong lowBits, int partitionCount, int weightBitCount, Span<int> endpointModes, out int extraCemBits)
    {
        extraCemBits = 0;

        if (partitionCount == 1)
        {
            var mode = (int)((lowBits >> 13) & 0xF);
            endpointModes[0] = mode;
            return GetEndpointValueCount(mode);
        }

        if (((lowBits >> 23) & 0x3) == 0)
        {
            var sharedMode = (int)((lowBits >> 25) & 0xF);
            var valueCount = 0;
            for (var i = 0; i < partitionCount; i++)
            {
                endpointModes[i] = sharedMode;
                valueCount += GetEndpointValueCount(sharedMode);
            }

            return valueCount;
        }

        extraCemBits = partitionCount switch
        {
            2 => 2,
            3 => 5,
            4 => 8,
            _ => 0
        };

        var extraCemStart = 128 - extraCemBits - weightBitCount;
        var cemValue = (lowBits >> 23) & 0x3F;
        var baseMode = (int)(((cemValue & 0x3) - 1) * 4);
        cemValue >>= 2;
        var cemBits = cemValue | ((ulong)GetBits(bits, extraCemStart, extraCemBits) << 4);

        var c = new InlineArray4<int>();
        for (var i = 0; i < partitionCount; i++)
        {
            c[i] = (int)(cemBits & 1);
            cemBits >>= 1;
        }

        var total = 0;
        for (var i = 0; i < partitionCount; i++)
        {
            var m = (int)(cemBits & 0x3);
            cemBits >>= 2;
            var mode = baseMode + (4 * c[i]) + m;
            endpointModes[i] = mode;
            total += GetEndpointValueCount(mode);
        }

        return total;
    }

    private static bool TryFitColorRange(int colorValueCount, int maxColorBits, out int range, out int bitCount)
    {
        ReadOnlySpan<int> ranges = [255, 191, 159, 127, 95, 79, 63, 47, 39, 31, 23, 19, 15, 11, 9, 7, 5];
        foreach (var candidate in ranges)
        {
            var candidateBits = GetBiseBitCount(colorValueCount, candidate);
            if (candidateBits <= maxColorBits)
            {
                range = candidate;
                bitCount = candidateBits;
                return true;
            }
        }

        range = 0;
        bitCount = 0;
        return false;
    }

    private static int GetWeightRange(int index) => index switch
    {
        2 => 1,
        3 => 2,
        4 => 3,
        5 => 4,
        6 => 5,
        7 => 7,
        10 => 9,
        11 => 11,
        12 => 15,
        13 => 19,
        14 => 23,
        15 => 31,
        _ => -1
    };

    private static int GetEndpointValueCount(int mode) => ((mode >> 2) + 1) * 2;

    private static UInt128 EncodeBiseSequence(ReadOnlySpan<int> values, int valueCount, int range)
    {
        var packing = GetBisePacking(range);
        var totalBits = GetBiseBitCount(valueCount, range);
        var result = UInt128.Zero;
        var bitOffset = 0;

        if (packing.Mode == BiseMode.Bits)
        {
            var mask = packing.Bits == 0 ? 0 : (1 << packing.Bits) - 1;
            for (var i = 0; i < valueCount; i++)
            {
                result |= (UInt128)(values[i] & mask) << bitOffset;
                bitOffset += packing.Bits;
            }

            return result;
        }

        var valuesPerBlock = packing.Mode == BiseMode.Trits ? 5 : 3;
        var blockBitCount = (valuesPerBlock * packing.Bits) + (packing.Mode == BiseMode.Trits ? 8 : 7);
        var valueIndex = 0;
        while (bitOffset < totalBits)
        {
            var encodedBlock = packing.Mode == BiseMode.Trits
                ? EncodeTritBlock(values, valueIndex, valueCount, packing.Bits)
                : EncodeQuintBlock(values, valueIndex, valueCount, packing.Bits);
            var bitsToWrite = Math.Min(blockBitCount, totalBits - bitOffset);
            result |= (UInt128)(encodedBlock & (ulong)GetMask(bitsToWrite)) << bitOffset;
            bitOffset += bitsToWrite;
            valueIndex += valuesPerBlock;
        }

        return result;
    }

    private static ulong EncodeTritBlock(ReadOnlySpan<int> values, int valueIndex, int valueCount, int bits)
    {
        Span<int> mantissas = stackalloc int[5];
        Span<int> trits = stackalloc int[5];
        var mantissaMask = bits == 0 ? 0 : (1 << bits) - 1;
        for (var i = 0; i < 5; i++)
        {
            var value = valueIndex + i < valueCount ? values[valueIndex + i] : 0;
            mantissas[i] = value & mantissaMask;
            trits[i] = value >> bits;
        }

        var packedTrits = STritEncodings[GetTritEncodingKey(trits)];
        return PackTritBlock(mantissas, packedTrits, bits);
    }

    private static ulong EncodeQuintBlock(ReadOnlySpan<int> values, int valueIndex, int valueCount, int bits)
    {
        Span<int> mantissas = stackalloc int[3];
        Span<int> quints = stackalloc int[3];
        var mantissaMask = bits == 0 ? 0 : (1 << bits) - 1;
        for (var i = 0; i < 3; i++)
        {
            var value = valueIndex + i < valueCount ? values[valueIndex + i] : 0;
            mantissas[i] = value & mantissaMask;
            quints[i] = value >> bits;
        }

        var packedQuints = SQuintEncodings[GetQuintEncodingKey(quints)];
        return PackQuintBlock(mantissas, packedQuints, bits);
    }

    private static ulong PackTritBlock(ReadOnlySpan<int> mantissas, int packedTrits, int bits)
    {
        var result = 0UL;
        var bitPosition = 0;
        WritePackedBits(ref result, ref bitPosition, mantissas[0], bits);
        WritePackedBits(ref result, ref bitPosition, packedTrits & 0x3, 2);
        WritePackedBits(ref result, ref bitPosition, mantissas[1], bits);
        WritePackedBits(ref result, ref bitPosition, (packedTrits >> 2) & 0x3, 2);
        WritePackedBits(ref result, ref bitPosition, mantissas[2], bits);
        WritePackedBits(ref result, ref bitPosition, (packedTrits >> 4) & 0x1, 1);
        WritePackedBits(ref result, ref bitPosition, mantissas[3], bits);
        WritePackedBits(ref result, ref bitPosition, (packedTrits >> 5) & 0x3, 2);
        WritePackedBits(ref result, ref bitPosition, mantissas[4], bits);
        WritePackedBits(ref result, ref bitPosition, (packedTrits >> 7) & 0x1, 1);
        return result;
    }

    private static ulong PackQuintBlock(ReadOnlySpan<int> mantissas, int packedQuints, int bits)
    {
        var result = 0UL;
        var bitPosition = 0;
        WritePackedBits(ref result, ref bitPosition, mantissas[0], bits);
        WritePackedBits(ref result, ref bitPosition, packedQuints & 0x7, 3);
        WritePackedBits(ref result, ref bitPosition, mantissas[1], bits);
        WritePackedBits(ref result, ref bitPosition, (packedQuints >> 3) & 0x3, 2);
        WritePackedBits(ref result, ref bitPosition, mantissas[2], bits);
        WritePackedBits(ref result, ref bitPosition, (packedQuints >> 5) & 0x3, 2);
        return result;
    }

    private static void WritePackedBits(ref ulong result, ref int bitPosition, int value, int bitCount)
    {
        if (bitCount == 0)
        {
            return;
        }

        result |= ((ulong)value & ((1UL << bitCount) - 1)) << bitPosition;
        bitPosition += bitCount;
    }

    private static int GetTritEncodingKey(ReadOnlySpan<int> trits)
    {
        var key = 0;
        var scale = 1;
        for (var i = 0; i < 5; i++)
        {
            key += trits[i] * scale;
            scale *= 3;
        }

        return key;
    }

    private static int GetQuintEncodingKey(ReadOnlySpan<int> quints)
    {
        var key = 0;
        var scale = 1;
        for (var i = 0; i < 3; i++)
        {
            key += quints[i] * scale;
            scale *= 5;
        }

        return key;
    }

    private static void DecodeBiseValues(UInt128 bits, int startBit, int bitCount, int range, int valueCount, Span<int> result)
    {
        var source = GetBits(bits, startBit, bitCount);
        DecodeBiseSequence(source, range, valueCount, result);
    }

    private static void DecodeBiseWeights(UInt128 bits, int bitCount, int range, int valueCount, Span<int> result)
    {
        var source = ReverseBits(bits) & GetMask(bitCount);
        DecodeBiseSequence(source, range, valueCount, result);
    }

    private static void DecodeBiseSequence(UInt128 source, int range, int valueCount, Span<int> result)
    {
        var packing = GetBisePacking(range);
        var bitOffset = 0;
        if (packing.Mode == BiseMode.Bits)
        {
            for (var i = 0; i < valueCount; i++)
            {
                result[i] = (int)ReadBits(source, ref bitOffset, packing.Bits);
            }

            return;
        }

        var blockValues = new InlineArray5<int>();
        var valuesPerBlock = packing.Mode == BiseMode.Trits ? 5 : 3;
        var blockBitCount = (valuesPerBlock * packing.Bits) + (packing.Mode == BiseMode.Trits ? 8 : 7);
        var totalBits = GetBiseBitCount(valueCount, range);
        var resultIndex = 0;

        while (bitOffset < totalBits)
        {
            var bitsToRead = Math.Min(blockBitCount, totalBits - bitOffset);
            var encodedBlock = ReadBits(source, ref bitOffset, bitsToRead);
            if (packing.Mode == BiseMode.Trits)
            {
                DecodeTritBlock(encodedBlock, packing.Bits, blockValues);
            }
            else
            {
                DecodeQuintBlock(encodedBlock, packing.Bits, blockValues);
            }

            for (var i = 0; i < valuesPerBlock && resultIndex < valueCount; i++)
            {
                result[resultIndex++] = blockValues[i];
            }
        }
    }

    private static void DecodeTritBlock(ulong encodedBlock, int bits, Span<int> values)
    {
        var bitPosition = 0;
        var mantissaMask = bits == 0 ? 0UL : (1UL << bits) - 1;
        var m0 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        bitPosition += bits;
        var packedTrits = (encodedBlock >> bitPosition) & 0x3;
        bitPosition += 2;
        var m1 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        bitPosition += bits;
        packedTrits |= ((encodedBlock >> bitPosition) & 0x3) << 2;
        bitPosition += 2;
        var m2 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        bitPosition += bits;
        packedTrits |= ((encodedBlock >> bitPosition) & 0x1) << 4;
        bitPosition++;
        var m3 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        bitPosition += bits;
        packedTrits |= ((encodedBlock >> bitPosition) & 0x3) << 5;
        bitPosition += 2;
        var m4 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        packedTrits |= ((encodedBlock >> (bitPosition + bits)) & 0x1) << 7;

        DecodePackedTrits((int)packedTrits, out var t0, out var t1, out var t2, out var t3, out var t4);
        values[0] = (t0 << bits) | m0;
        values[1] = (t1 << bits) | m1;
        values[2] = (t2 << bits) | m2;
        values[3] = (t3 << bits) | m3;
        values[4] = (t4 << bits) | m4;
    }

    private static void DecodeQuintBlock(ulong encodedBlock, int bits, Span<int> values)
    {
        var bitPosition = 0;
        var mantissaMask = bits == 0 ? 0UL : (1UL << bits) - 1;
        var m0 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        bitPosition += bits;
        var packedQuints = (encodedBlock >> bitPosition) & 0x7;
        bitPosition += 3;
        var m1 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        bitPosition += bits;
        packedQuints |= ((encodedBlock >> bitPosition) & 0x3) << 3;
        bitPosition += 2;
        var m2 = (int)((encodedBlock >> bitPosition) & mantissaMask);
        packedQuints |= ((encodedBlock >> (bitPosition + bits)) & 0x3) << 5;

        DecodePackedQuints((int)packedQuints, out var q0, out var q1, out var q2);
        values[0] = (q0 << bits) | m0;
        values[1] = (q1 << bits) | m1;
        values[2] = (q2 << bits) | m2;
    }

    private static void DecodePackedTrits(int packed, out int t0, out int t1, out int t2, out int t3, out int t4)
    {
        int c;
        if (((packed >> 2) & 0x7) == 0x7)
        {
            c = (((packed >> 5) & 0x7) << 2) | (packed & 0x3);
            t4 = 2;
            t3 = 2;
        }
        else
        {
            c = packed & 0x1F;
            if (((packed >> 5) & 0x3) == 0x3)
            {
                t4 = 2;
                t3 = (packed >> 7) & 1;
            }
            else
            {
                t4 = (packed >> 7) & 1;
                t3 = (packed >> 5) & 0x3;
            }
        }

        if ((c & 0x3) == 0x3)
        {
            t2 = 2;
            t1 = (c >> 4) & 1;
            var c3 = (c >> 3) & 1;
            var c2 = (c >> 2) & 1;
            t0 = (c3 << 1) | (c2 & (c3 ^ 1));
        }
        else if (((c >> 2) & 0x3) == 0x3)
        {
            t2 = 2;
            t1 = 2;
            t0 = c & 0x3;
        }
        else
        {
            t2 = (c >> 4) & 1;
            t1 = (c >> 2) & 0x3;
            var c1 = (c >> 1) & 1;
            var c0 = c & 1;
            t0 = (c1 << 1) | (c0 & (c1 ^ 1));
        }
    }

    private static void DecodePackedQuints(int packed, out int q0, out int q1, out int q2)
    {
        int c;
        if (((packed >> 1) & 0x3) == 0x3 && ((packed >> 5) & 0x3) == 0)
        {
            var q0Bit = packed & 1;
            q2 = (q0Bit << 2) | (((packed >> 4) & 1 & (q0Bit ^ 1)) << 1) | (((packed >> 3) & 1) & (q0Bit ^ 1));
            q1 = 4;
            q0 = 4;
            return;
        }

        if (((packed >> 1) & 0x3) == 0x3)
        {
            q2 = 4;
            c = (((packed >> 3) & 0x3) << 3) | ((((packed >> 5) & 0x3) ^ 0x3) << 1) | (packed & 1);
        }
        else
        {
            q2 = (packed >> 5) & 0x3;
            c = packed & 0x1F;
        }

        if ((c & 0x7) == 0x5)
        {
            q1 = 4;
            q0 = (c >> 3) & 0x3;
        }
        else
        {
            q1 = (c >> 3) & 0x3;
            q0 = c & 0x7;
        }
    }

    private static int[] CreateTritEncodings()
    {
        var encodings = new int[243];
        Array.Fill(encodings, -1);
        for (var packed = 0; packed < 256; packed++)
        {
            DecodePackedTrits(packed, out var t0, out var t1, out var t2, out var t3, out var t4);
            var key = t0 + (t1 * 3) + (t2 * 9) + (t3 * 27) + (t4 * 81);
            if (encodings[key] < 0)
            {
                encodings[key] = packed;
            }
        }

        return encodings;
    }

    private static int[] CreateQuintEncodings()
    {
        var encodings = new int[125];
        Array.Fill(encodings, -1);
        for (var packed = 0; packed < 128; packed++)
        {
            DecodePackedQuints(packed, out var q0, out var q1, out var q2);
            var key = q0 + (q1 * 5) + (q2 * 25);
            if (encodings[key] < 0)
            {
                encodings[key] = packed;
            }
        }

        return encodings;
    }

    private static int UnquantizeEndpointValue(int value, int range)
    {
        var packing = GetBisePacking(range);
        var mantissa = packing.Bits == 0 ? 0 : value & ((1 << packing.Bits) - 1);
        var extra = value >> packing.Bits;

        return packing.Mode switch
        {
            BiseMode.Bits => ReplicateBits(value, packing.Bits, 8),
            BiseMode.Trits => UnquantizeEndpointTrit(extra, mantissa, range),
            BiseMode.Quints => UnquantizeEndpointQuint(extra, mantissa, range),
            _ => 0
        };
    }

    private static int UnquantizeWeight(int value, int range)
    {
        var packing = GetBisePacking(range);
        var mantissa = packing.Bits == 0 ? 0 : value & ((1 << packing.Bits) - 1);
        var extra = value >> packing.Bits;

        var unquantized = packing.Mode switch
        {
            BiseMode.Bits => ReplicateBits(value, packing.Bits, 6),
            BiseMode.Trits => UnquantizeWeightTrit(extra, mantissa, range),
            BiseMode.Quints => UnquantizeWeightQuint(extra, mantissa, range),
            _ => 0
        };

        return unquantized > 32 ? unquantized + 1 : unquantized;
    }

    private static int UnquantizeEndpointTrit(int trit, int bits, int range)
    {
        var a = (bits & 1) != 0 ? 0x1FF : 0;
        int b;
        int c;
        switch (range)
        {
            case 5:
                b = 0;
                c = 204;
                break;
            case 11:
                var x11 = (bits >> 1) & 1;
                b = (x11 << 1) | (x11 << 2) | (x11 << 4) | (x11 << 8);
                c = 93;
                break;
            case 23:
                var x23 = (bits >> 1) & 3;
                b = x23 | (x23 << 2) | (x23 << 7);
                c = 44;
                break;
            case 47:
                var x47 = (bits >> 1) & 7;
                b = x47 | (x47 << 6);
                c = 22;
                break;
            case 95:
                var x95 = (bits >> 1) & 15;
                b = (x95 >> 2) | (x95 << 5);
                c = 11;
                break;
            default:
                var x191 = (bits >> 1) & 31;
                b = (x191 >> 4) | (x191 << 4);
                c = 5;
                break;
        }

        var value = ((trit * c) + b) ^ a;
        return (a & 0x80) | (value >> 2);
    }

    private static int UnquantizeEndpointQuint(int quint, int bits, int range)
    {
        var a = (bits & 1) != 0 ? 0x1FF : 0;
        int b;
        int c;
        switch (range)
        {
            case 9:
                b = 0;
                c = 113;
                break;
            case 19:
                var x19 = (bits >> 1) & 1;
                b = (x19 << 2) | (x19 << 3) | (x19 << 8);
                c = 54;
                break;
            case 39:
                var x39 = (bits >> 1) & 3;
                b = (x39 >> 1) | (x39 << 1) | (x39 << 7);
                c = 26;
                break;
            case 79:
                var x79 = (bits >> 1) & 7;
                b = (x79 >> 1) | (x79 << 6);
                c = 13;
                break;
            default:
                var x159 = (bits >> 1) & 15;
                b = (x159 >> 3) | (x159 << 5);
                c = 6;
                break;
        }

        var value = ((quint * c) + b) ^ a;
        return (a & 0x80) | (value >> 2);
    }

    private static int UnquantizeWeightTrit(int trit, int bits, int range)
    {
        if (range == 2)
        {
            return trit switch
            {
                0 => 0,
                1 => 32,
                _ => 63
            };
        }

        var a = (bits & 1) != 0 ? 0x7F : 0;
        int b;
        int c;
        switch (range)
        {
            case 5:
                b = 0;
                c = 50;
                break;
            case 11:
                var x11 = (bits >> 1) & 1;
                b = x11 | (x11 << 2) | (x11 << 6);
                c = 23;
                break;
            default:
                var x23 = (bits >> 1) & 3;
                b = x23 | (x23 << 5);
                c = 11;
                break;
        }

        var value = ((trit * c) + b) ^ a;
        return (a & 0x20) | (value >> 2);
    }

    private static int UnquantizeWeightQuint(int quint, int bits, int range)
    {
        if (range == 4)
        {
            return quint switch
            {
                0 => 0,
                1 => 16,
                2 => 32,
                3 => 47,
                _ => 63
            };
        }

        var a = (bits & 1) != 0 ? 0x7F : 0;
        int b;
        int c;
        switch (range)
        {
            case 9:
                b = 0;
                c = 28;
                break;
            default:
                var x19 = (bits >> 1) & 1;
                b = (x19 << 1) | (x19 << 6);
                c = 13;
                break;
        }

        var value = ((quint * c) + b) ^ a;
        return (a & 0x20) | (value >> 2);
    }

    private static int ReplicateBits(int value, int sourceBits, int targetBits)
    {
        if (sourceBits == 0)
        {
            return 0;
        }

        var result = value;
        var resultBits = sourceBits;
        while (resultBits < targetBits)
        {
            var bitsToAdd = Math.Min(sourceBits, targetBits - resultBits);
            result = (result << bitsToAdd) | (value >> (sourceBits - bitsToAdd));
            resultBits += bitsToAdd;
        }

        return result;
    }

    private static BisePacking GetBisePacking(int range)
    {
        var values = range + 1;
        if (IsPowerOfTwo(values))
        {
            return new BisePacking(BiseMode.Bits, Log2(values));
        }

        if (values % 3 == 0 && IsPowerOfTwo(values / 3))
        {
            return new BisePacking(BiseMode.Trits, Log2(values / 3));
        }

        return new BisePacking(BiseMode.Quints, Log2(values / 5));
    }

    private static int GetBiseBitCount(int valueCount, int range)
    {
        var packing = GetBisePacking(range);
        return packing.Mode switch
        {
            BiseMode.Trits => (valueCount * packing.Bits) + (((valueCount * 8) + 4) / 5),
            BiseMode.Quints => (valueCount * packing.Bits) + (((valueCount * 7) + 2) / 3),
            _ => valueCount * packing.Bits
        };
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static int Log2(int value)
    {
        var result = 0;
        while (value > 1)
        {
            result++;
            value >>= 1;
        }

        return result;
    }

    private static ushort LnsToFloat16Bits(int lns)
    {
        var exponent = (lns >> 11) & 0x1F;
        var mantissa = lns & 0x7FF;
        var adjustedMantissa = mantissa switch
        {
            < 512 => 3 * mantissa,
            < 1536 => (4 * mantissa) - 512,
            _ => (5 * mantissa) - 2048
        };

        var half = (exponent << 10) | (adjustedMantissa >> 3);
        return (ushort)Math.Min(half, 0x7BFF);
    }

    private static int GetPartitionIndex(int partitionCount, int partitionSeed, int x, int y, int blockWidth, int blockHeight)
    {
        if (partitionCount <= 1)
        {
            return 0;
        }

        if (blockWidth * blockHeight < 31)
        {
            x <<= 1;
            y <<= 1;
        }

        var random = ScramblePartitionSeed(partitionSeed, partitionCount);
        var seed = new InlineArray12<uint>();
        seed[0] = random & 0xF;
        seed[1] = (random >> 4) & 0xF;
        seed[2] = (random >> 8) & 0xF;
        seed[3] = (random >> 12) & 0xF;
        seed[4] = (random >> 16) & 0xF;
        seed[5] = (random >> 20) & 0xF;
        seed[6] = (random >> 24) & 0xF;
        seed[7] = (random >> 28) & 0xF;
        seed[8] = (random >> 18) & 0xF;
        seed[9] = (random >> 22) & 0xF;
        seed[10] = (random >> 26) & 0xF;
        seed[11] = ((random >> 30) | (random << 2)) & 0xF;

        for (var i = 0; i < 12; i++)
        {
            seed[i] *= seed[i];
        }

        int shift1;
        int shift2;
        if ((partitionSeed & 1) != 0)
        {
            shift1 = (partitionSeed & 2) != 0 ? 4 : 5;
            shift2 = partitionCount == 3 ? 6 : 5;
        }
        else
        {
            shift1 = partitionCount == 3 ? 6 : 5;
            shift2 = (partitionSeed & 2) != 0 ? 4 : 5;
        }

        var shift3 = (partitionSeed & 0x10) != 0 ? shift1 : shift2;
        seed[0] >>= shift1;
        seed[1] >>= shift2;
        seed[2] >>= shift1;
        seed[3] >>= shift2;
        seed[4] >>= shift1;
        seed[5] >>= shift2;
        seed[6] >>= shift1;
        seed[7] >>= shift2;
        seed[8] >>= shift3;
        seed[9] >>= shift3;
        seed[10] >>= shift3;
        seed[11] >>= shift3;

        var a = (int)((seed[0] * (uint)x) + (seed[1] * (uint)y) + (random >> 14)) & 0x3F;
        var b = (int)((seed[2] * (uint)x) + (seed[3] * (uint)y) + (random >> 10)) & 0x3F;
        var c = (int)((seed[4] * (uint)x) + (seed[5] * (uint)y) + (random >> 6)) & 0x3F;
        var d = (int)((seed[6] * (uint)x) + (seed[7] * (uint)y) + (random >> 2)) & 0x3F;

        if (partitionCount <= 3)
        {
            d = 0;
        }

        if (partitionCount <= 2)
        {
            c = 0;
        }

        if (a >= b && a >= c && a >= d)
        {
            return 0;
        }

        return b >= c && b >= d ? 1 : c >= d ? 2 : 3;
    }

    private static uint ScramblePartitionSeed(int partitionSeed, int partitionCount)
    {
        var random = (uint)(partitionSeed + ((partitionCount - 1) * 1024));
        random ^= random >> 15;
        random -= random << 17;
        random += random << 7;
        random += random << 4;
        random ^= random >> 5;
        random += random << 16;
        random ^= random >> 7;
        random ^= random >> 3;
        random ^= random << 6;
        random ^= random >> 17;
        return random;
    }

    private static Rgba8UNorm ReadVoidExtentUNorm(ReadOnlySpan<byte> source, bool srgb)
    {
        var red = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[8..]) >> 8);
        var green = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[10..]) >> 8);
        var blue = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[12..]) >> 8);
        var alpha = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[14..]) >> 8);

        return srgb
            ? new Rgba8UNorm(RgbaColorConversions.Srgb8ToLinearUNorm8(red), RgbaColorConversions.Srgb8ToLinearUNorm8(green), RgbaColorConversions.Srgb8ToLinearUNorm8(blue), alpha)
            : new Rgba8UNorm(red, green, blue, alpha);
    }

    private static Rgba16Float ReadVoidExtentUNormAsFloat(ReadOnlySpan<byte> source) => new(
        RgbaColorConversions.ToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[8..])),
        RgbaColorConversions.ToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[10..])),
        RgbaColorConversions.ToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[12..])),
        RgbaColorConversions.ToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[14..])));

    private static Rgba16Float ReadVoidExtentFloat(ReadOnlySpan<byte> source) => new(
        BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[8..])),
        BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[10..])),
        BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[12..])),
        BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source[14..])));

    private static UInt128 ReadBlockBits(ReadOnlySpan<byte> source)
    {
        var low = BinaryPrimitives.ReadUInt64LittleEndian(source);
        var high = BinaryPrimitives.ReadUInt64LittleEndian(source[8..]);
        return ((UInt128)high << 64) | low;
    }

    private static void WriteBlockBits(UInt128 bits, Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(destination, (ulong)bits);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], (ulong)(bits >> 64));
    }

    private static UInt128 GetBits(UInt128 value, int start, int count) =>
        count <= 0 ? UInt128.Zero : (value >> start) & GetMask(count);

    private static UInt128 GetMask(int bitCount) =>
        bitCount >= 128 ? UInt128.MaxValue : ((UInt128.One << bitCount) - 1);

    private static ulong ReadBits(UInt128 value, ref int offset, int count)
    {
        var bits = (ulong)GetBits(value, offset, count);
        offset += count;
        return bits;
    }

    private static UInt128 ReverseBits(UInt128 value)
    {
        var low = (ulong)value;
        var high = (ulong)(value >> 64);
        return ((UInt128)ReverseBits64(low) << 64) | ReverseBits64(high);
    }

    private static UInt128 ReverseLowBits(UInt128 value, int bitCount) =>
        ReverseBits(value) >> (128 - bitCount);

    private static uint ReverseBits32(uint value)
    {
        value = ((value & 0x55555555U) << 1) | ((value >> 1) & 0x55555555U);
        value = ((value & 0x33333333U) << 2) | ((value >> 2) & 0x33333333U);
        value = ((value & 0x0F0F0F0FU) << 4) | ((value >> 4) & 0x0F0F0F0FU);
        value = ((value & 0x00FF00FFU) << 8) | ((value >> 8) & 0x00FF00FFU);
        return (value << 16) | (value >> 16);
    }

    private static ulong ReverseBits64(ulong value)
    {
        value = ((value & 0x5555555555555555UL) << 1) | ((value >> 1) & 0x5555555555555555UL);
        value = ((value & 0x3333333333333333UL) << 2) | ((value >> 2) & 0x3333333333333333UL);
        value = ((value & 0x0F0F0F0F0F0F0F0FUL) << 4) | ((value >> 4) & 0x0F0F0F0F0F0F0F0FUL);
        value = ((value & 0x00FF00FF00FF00FFUL) << 8) | ((value >> 8) & 0x00FF00FF00FF00FFUL);
        value = ((value & 0x0000FFFF0000FFFFUL) << 16) | ((value >> 16) & 0x0000FFFF0000FFFFUL);
        return (value << 32) | (value >> 32);
    }

    private void LoadUNormBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * _blockWidth;
        var originY = blockY * _blockHeight;
        var lastSourceX = source.Width - 1;
        var blockOffset = 0;
        for (var y = 0; y < _blockHeight; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            for (var x = 0; x < _blockWidth; x++)
            {
                var sourceX = Math.Min(originX + x, lastSourceX);
                destination[blockOffset++] = TPixel.ToRgba8UNorm(sourceRow[sourceX]);
            }
        }
    }

    private void LoadFloatBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba16Float> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * _blockWidth;
        var originY = blockY * _blockHeight;
        var lastSourceX = source.Width - 1;
        var blockOffset = 0;
        for (var y = 0; y < _blockHeight; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            for (var x = 0; x < _blockWidth; x++)
            {
                var sourceX = Math.Min(originX + x, lastSourceX);
                destination[blockOffset++] = TPixel.ToRgba16Float(sourceRow[sourceX]);
            }
        }
    }

    private void StoreUNormBlock<TPixel>(ReadOnlySpan<Rgba8UNorm> block, int blockX, int blockY, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * _blockWidth;
        var originY = blockY * _blockHeight;
        for (var y = 0; y < _blockHeight; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            for (var x = 0; x < _blockWidth; x++)
            {
                var destinationX = originX + x;
                if (destinationX >= destination.Width)
                {
                    break;
                }

                destinationRow[destinationX] = TPixel.FromRgba8UNorm(block[(y * _blockWidth) + x]);
            }
        }
    }

    private void StoreFloatBlock<TPixel>(ReadOnlySpan<Rgba16Float> block, int blockX, int blockY, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * _blockWidth;
        var originY = blockY * _blockHeight;
        for (var y = 0; y < _blockHeight; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            for (var x = 0; x < _blockWidth; x++)
            {
                var destinationX = originX + x;
                if (destinationX >= destination.Width)
                {
                    break;
                }

                destinationRow[destinationX] = TPixel.FromRgba16Float(block[(y * _blockWidth) + x]);
            }
        }
    }

    private static void FillUNormBlock(Span<Rgba8UNorm> destination, int blockWidth, int blockHeight, Rgba8UNorm color)
    {
        for (var i = 0; i < blockWidth * blockHeight; i++)
        {
            destination[i] = color;
        }
    }

    private static void FillFloatBlock(Span<Rgba16Float> destination, int blockWidth, int blockHeight, Rgba16Float color)
    {
        for (var i = 0; i < blockWidth * blockHeight; i++)
        {
            destination[i] = color;
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded ASTC texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded ASTC texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size, int blockSize) => (size + blockSize - 1) / blockSize;

    private static bool TryGetTransfer(TextureFormat format, out AstcTransfer transfer)
    {
        if (TryGetFormatInfo(format, out var info))
        {
            transfer = info.Transfer;
            return true;
        }

        transfer = default;
        return false;
    }

    private static bool TryGetFormatInfo(TextureFormat format, out AstcFormatInfo info)
    {
        foreach (var candidate in SSupportedFormatInfo)
        {
            if (candidate.Format == format)
            {
                info = candidate;
                return true;
            }
        }

        info = default;
        return false;
    }

    private static TextureFormat[] CreateSupportedFormats()
    {
        var formats = new TextureFormat[SSupportedFormatInfo.Length];
        for (var i = 0; i < formats.Length; i++)
        {
            formats[i] = SSupportedFormatInfo[i].Format;
        }

        return formats;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Texture format '{format.Name}' is not a supported ASTC format.");

    private static Rgba8UNorm ErrorUNorm => new(255, 0, 255, 255);

    private static Rgba16Float ErrorFloat => new(1f, 0f, 1f, 1f);

    private enum AstcTransfer
    {
        Ldr,
        Srgb,
        Hdr
    }

    private enum BiseMode
    {
        Bits,
        Trits,
        Quints
    }

    private readonly record struct AstcFormatInfo(TextureFormat Format, int BlockWidth, int BlockHeight, AstcTransfer Transfer);

    private readonly record struct AstcLdrEncodingResult(UInt128 Bits, long Error);

    private readonly record struct AstcHdrEncodingResult(UInt128 Bits, double Error);

    private readonly record struct AstcWeightGridCandidate(
        int Width,
        int Height,
        int WeightRange,
        int BlockMode,
        int WeightBitCount,
        bool DualPlane,
        int ColorRange,
        int ColorBitCount);

    private readonly record struct BisePacking(BiseMode Mode, int Bits);

    private struct AstcBlockInfo
    {
        public bool IsVoidExtent;
        public bool VoidExtentIsHdr;
        public int WeightWidth;
        public int WeightHeight;
        public int WeightRange;
        public int WeightBitCount;
        public int PartitionCount;
        public int PartitionIndex;
        public bool DualPlane;
        public int DualPlaneChannel;
        public int ColorStartBit;
        public int ColorBitCount;
        public int ColorRange;
        public int ColorValueCount;
        public int EndpointMode0;
        public int EndpointMode1;
        public int EndpointMode2;
        public int EndpointMode3;

        public readonly int GetEndpointMode(int index) => index switch
        {
            0 => EndpointMode0,
            1 => EndpointMode1,
            2 => EndpointMode2,
            _ => EndpointMode3
        };
    }

    private struct AstcEndpointPair
    {
        public int R0;
        public int G0;
        public int B0;
        public int A0;
        public int R1;
        public int G1;
        public int B1;
        public int A1;
        public bool RgbHdr;
        public bool AlphaHdr;
        public int DualPlaneChannel;
    }

    [InlineArray(18)]
    private struct InlineArray18<T>
    {
        private T _element0;
    }

    [InlineArray(MaxWeightValues)]
    private struct IntWeightBlock
    {
        private int _element0;
    }

    [InlineArray(MaxTexelsPerBlock)]
    private struct IntTexelBlock
    {
        private int _element0;
    }

    [InlineArray(MaxTexelsPerBlock)]
    private struct Rgba8UNormTexelBlock
    {
        private Rgba8UNorm _element0;
    }

    [InlineArray(MaxTexelsPerBlock)]
    private struct Rgba16FloatTexelBlock
    {
        private Rgba16Float _element0;
    }
}
