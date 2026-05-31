using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class FxtcTextureCoder : IPitchTextureCoder
{
    private const int BlockWidth = 8;
    private const int BlockHeight = 4;
    private const int TexelsPerBlock = BlockWidth * BlockHeight;
    private const int BytesPerBlock = 16;
    private const int LeftTexelCount = 16;
    private const int ExhaustiveCcHiColorSeedLimit = 16;
    private const byte AlphaCutoff = 128;

    private readonly FxtcCoderOptions _options;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.RgbFxt1UNorm,
        TextureFormats.RgbaFxt1UNorm
    ];

    public FxtcTextureCoder(TextureFormat format, FxtcCoderOptions? options = null)
    {
        if (!IsSupported(format))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _options = options ?? new FxtcCoderOptions();
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.RgbFxt1UNorm || format == TextureFormats.RgbaFxt1UNorm;

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        return checked(rowPitch * GetBlockCountY(height));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        var blockCountX = GetBlockCountX(destination.Width);
        var blockCountY = GetBlockCountY(destination.Height);
        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                DecodeBlock(Format, source.Slice(blockOffset, BytesPerBlock), block);
                StoreBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var blockCountX = GetBlockCountX(source.Width);
        var blockCountY = GetBlockCountY(source.Height);

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
                        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

                        var blockOffset = checked(blockY * rowPitch);
                        for (var blockX = 0; blockX < blockCountX; blockX++)
                        {
                            LoadBlock(localSource, blockX, blockY, block);
                            EncodeBlock(block, localDestination.Slice(blockOffset, BytesPerBlock));
                            blockOffset = checked(blockOffset + BytesPerBlock);
                        }
                    });
                }
            }

            return;
        }

        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadBlock(source, blockX, blockY, block);
                EncodeBlock(block, destination.Slice(blockOffset, BytesPerBlock));
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void DecodeBlock(TextureFormat format, ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var mode = (int)ReadBits(source, 125, 3);
        if ((mode & 4) != 0)
        {
            DecodeCcMixedBlock(source, destination);
        }
        else if ((mode >> 1) == 0)
        {
            DecodeCcHiBlock(source, destination);
        }
        else if (mode == 2)
        {
            DecodeCcChromaBlock(source, destination);
        }
        else if (mode == 3)
        {
            DecodeCcAlphaBlock(source, destination);
        }
        else
        {
            throw new InvalidDataException("Invalid FXT1 block mode.");
        }

        if (format == TextureFormats.RgbFxt1UNorm)
        {
            for (var i = 0; i < TexelsPerBlock; i++)
            {
                destination[i].Alpha = byte.MaxValue;
            }
        }
    }

    private void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        switch (_options.CompressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeBlockFast(source, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeBlockOptimized(source, destination, _options.CompressionMode);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(FxtcCoderOptions.CompressionMode),
                    _options.CompressionMode,
                    "Unsupported FXT1 compression mode.");
        }
    }

    private void EncodeBlockFast(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        Span<byte> best = stackalloc byte[BytesPerBlock];
        Span<byte> candidate = stackalloc byte[BytesPerBlock];
        var bestError = long.MaxValue;

        EncodeCcMixedOpaqueBlock(source, candidate);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        EncodeCcHiBlock(source, candidate);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        EncodeCcChromaBlock(source, candidate);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        if (Format == TextureFormats.RgbaFxt1UNorm)
        {
            EncodeCcMixedAlphaBlock(source, candidate);
            UpdateBestCandidate(source, candidate, best, ref bestError);

            EncodeCcAlphaBlock(source, candidate);
            UpdateBestCandidate(source, candidate, best, ref bestError);
        }

        best.CopyTo(destination);
    }

    private void EncodeBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        Span<byte> best = stackalloc byte[BytesPerBlock];
        Span<byte> candidate = stackalloc byte[BytesPerBlock];
        var bestError = long.MaxValue;

        EncodeBlockFast(source, candidate);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive)
        {
            var previousMode = compressionMode == TextureCompressionLevel.High
                ? TextureCompressionLevel.Normal
                : TextureCompressionLevel.High;
            EncodeBlockOptimized(source, candidate, previousMode);
            UpdateBestCandidate(source, candidate, best, ref bestError);
        }

        EncodeCcMixedOpaqueBlockOptimized(source, candidate, compressionMode);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        EncodeCcHiBlockOptimized(source, candidate, compressionMode);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        EncodeCcChromaBlockOptimized(source, candidate, compressionMode);
        UpdateBestCandidate(source, candidate, best, ref bestError);

        if (Format == TextureFormats.RgbaFxt1UNorm)
        {
            EncodeCcMixedAlphaBlockOptimized(source, candidate, compressionMode);
            UpdateBestCandidate(source, candidate, best, ref bestError);

            EncodeCcAlphaBlockOptimized(source, candidate, compressionMode);
            UpdateBestCandidate(source, candidate, best, ref bestError);
        }

        best.CopyTo(destination);
    }

    private void UpdateBestCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        ReadOnlySpan<byte> candidate,
        Span<byte> best,
        ref long bestError)
    {
        var error = CalculateBlockError(source, candidate);
        if (error < bestError)
        {
            bestError = error;
            candidate.CopyTo(best);
        }
    }

    private long CalculateBlockError(ReadOnlySpan<Rgba8UNorm> source, ReadOnlySpan<byte> encoded)
    {
        Span<Rgba8UNorm> decoded = stackalloc Rgba8UNorm[TexelsPerBlock];
        DecodeBlock(Format, encoded, decoded);

        long error = 0;
        var compareAlpha = Format == TextureFormats.RgbaFxt1UNorm;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            error += SquaredDifference(source[i].Red, decoded[i].Red);
            error += SquaredDifference(source[i].Green, decoded[i].Green);
            error += SquaredDifference(source[i].Blue, decoded[i].Blue);
            if (compareAlpha)
            {
                error += SquaredDifference(source[i].Alpha, decoded[i].Alpha);
            }
        }

        return error;
    }

    private static void DecodeCcHiBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var color0 = UnpackRgb555((ushort)ReadBits(source, 96, 15), byte.MaxValue);
        var color1 = UnpackRgb555((ushort)ReadBits(source, 111, 15), byte.MaxValue);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[8];
        BuildCcHiPalette(color0, color1, palette);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = palette[(int)ReadBits(source, i * 3, 3)];
        }
    }

    private void EncodeCcHiBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        destination.Clear();
        FindColorBounds(source, 0, TexelsPerBlock, includeAlpha: false, ignoreTransparent: false, out var min, out var max);
        var color0 = PackRgb555(min);
        var color1 = PackRgb555(max);
        var decoded0 = UnpackRgb555(color0, byte.MaxValue);
        var decoded1 = UnpackRgb555(color1, byte.MaxValue);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[8];
        BuildCcHiPalette(decoded0, decoded1, palette);

        var compareAlpha = Format == TextureFormats.RgbaFxt1UNorm;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            WriteBits(destination, i * 3, (ulong)FindNearestColorIndex(source[i], palette, 8, compareAlpha), 3);
        }

        WriteBits(destination, 96, color0, 15);
        WriteBits(destination, 111, color1, 15);
    }

    private void EncodeCcHiBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        Span<CcHiEndpointPair> seeds = stackalloc CcHiEndpointPair[
            compressionMode == TextureCompressionLevel.Exhaustive ? 272 : 16];
        var seedCount = 0;
        var compareAlpha = Format == TextureFormats.RgbaFxt1UNorm;
        var ignoreTransparent = compareAlpha && HasTransparentTexel(source);

        if (ignoreTransparent && !HasOpaqueTexel(source))
        {
            destination.Clear();
            for (var i = 0; i < TexelsPerBlock; i++)
            {
                WriteBits(destination, i * 3, 7, 3);
            }

            return;
        }

        FindColorBounds(source, 0, TexelsPerBlock, includeAlpha: false, ignoreTransparent, out var min, out var max);
        AddCcHiSeed(seeds, ref seedCount, PackRgb555(min), PackRgb555(max));
        AddCcHiSeed(seeds, ref seedCount, PackRgb555(max), PackRgb555(min));

        if (TryInsetColorBounds(min, max, out var insetMin, out var insetMax))
        {
            AddCcHiSeed(seeds, ref seedCount, PackRgb555(insetMin), PackRgb555(insetMax));
            AddCcHiSeed(seeds, ref seedCount, PackRgb555(insetMax), PackRgb555(insetMin));
        }

        if (TryFindPrincipalAxisColorEndpoints(source, ignoreTransparent, out var axisMin, out var axisMax))
        {
            AddCcHiSeed(seeds, ref seedCount, PackRgb555Nearest(axisMin), PackRgb555Nearest(axisMax));
            AddCcHiSeed(seeds, ref seedCount, PackRgb555Nearest(axisMax), PackRgb555Nearest(axisMin));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindFarthestColorEndpoints(source, ignoreTransparent, out var farA, out var farB))
        {
            AddCcHiSeed(seeds, ref seedCount, PackRgb555(farA), PackRgb555(farB));
            AddCcHiSeed(seeds, ref seedCount, PackRgb555(farB), PackRgb555(farA));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindAverageColor(source, ignoreTransparent, out var average))
        {
            AddCcHiSeed(seeds, ref seedCount, PackRgb555Nearest(average), PackRgb555Nearest(average));
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueCcHiSeeds(source, ignoreTransparent, seeds, ref seedCount);
        }

        var best = new CcHiEncoding { Error = long.MaxValue };
        var iterationLimit = GetColorOptimizationIterationLimit(compressionMode);
        for (var i = 0; i < seedCount; i++)
        {
            OptimizeCcHiSeed(
                source,
                compareAlpha,
                ignoreTransparent,
                seeds[i].Color0,
                seeds[i].Color1,
                iterationLimit,
                ref best);
        }

        RefineCcHiEndpoints(
            source,
            compareAlpha,
            ignoreTransparent,
            GetColorRefinementPassLimit(compressionMode),
            ref best);
        WriteCcHiEncoding(source, best.Color0, best.Color1, compareAlpha, ignoreTransparent, destination);
    }

    private static void AddCcHiSeed(Span<CcHiEndpointPair> seeds, ref int seedCount, ushort color0, ushort color1)
    {
        for (var i = 0; i < seedCount; i++)
        {
            if (seeds[i].Color0 == color0 && seeds[i].Color1 == color1)
            {
                return;
            }
        }

        if (seedCount < seeds.Length)
        {
            seeds[seedCount++] = new CcHiEndpointPair(color0, color1);
        }
    }

    private static void AddUniqueCcHiSeeds(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        Span<CcHiEndpointPair> seeds,
        ref int seedCount)
    {
        Span<ushort> uniqueColors = stackalloc ushort[TexelsPerBlock];
        var colorCount = CollectUniqueRgb555Colors(source, 0, TexelsPerBlock, ignoreTransparent, uniqueColors);
        ReadOnlySpan<ushort> colors = uniqueColors[..colorCount];
        Span<ushort> representativeColors = stackalloc ushort[ExhaustiveCcHiColorSeedLimit];
        if (colorCount > representativeColors.Length)
        {
            SelectRepresentativeRgb555Colors(colors, representativeColors);
            colors = representativeColors;
            colorCount = colors.Length;
        }

        for (var i = 0; i < colorCount; i++)
        {
            for (var j = 0; j < colorCount; j++)
            {
                AddCcHiSeed(seeds, ref seedCount, colors[i], colors[j]);
            }
        }
    }

    private void OptimizeCcHiSeed(
        ReadOnlySpan<Rgba8UNorm> source,
        bool compareAlpha,
        bool forceTransparent,
        ushort color0,
        ushort color1,
        int iterationLimit,
        ref CcHiEncoding best)
    {
        Span<int> indices = stackalloc int[TexelsPerBlock];
        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateCcHiCandidate(source, color0, color1, compareAlpha, forceTransparent, indices);
            UpdateBestCcHiEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveRgb555Line(source, indices, paletteSteps: 6, out var nextColor0, out var nextColor1))
            {
                return;
            }

            if (nextColor0 == color0 && nextColor1 == color1)
            {
                return;
            }

            color0 = nextColor0;
            color1 = nextColor1;
        }
    }

    private CcHiEncoding EvaluateCcHiCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1,
        bool compareAlpha,
        bool forceTransparent,
        Span<int> indices)
    {
        Span<byte> encoded = stackalloc byte[BytesPerBlock];
        WriteCcHiEncoding(source, color0, color1, compareAlpha, forceTransparent, encoded, indices);
        return new CcHiEncoding
        {
            Color0 = color0,
            Color1 = color1,
            Error = CalculateBlockError(source, encoded)
        };
    }

    private void RefineCcHiEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        bool compareAlpha,
        bool forceTransparent,
        int passLimit,
        ref CcHiEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var improved = false;
            for (var endpoint = 0; endpoint < 2; endpoint++)
            {
                for (var component = 0; component < 3; component++)
                {
                    improved |= TryRefineCcHiEndpoint(source, compareAlpha, forceTransparent, endpoint, component, -1, ref best);
                    improved |= TryRefineCcHiEndpoint(source, compareAlpha, forceTransparent, endpoint, component, 1, ref best);
                }
            }

            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private bool TryRefineCcHiEndpoint(
        ReadOnlySpan<Rgba8UNorm> source,
        bool compareAlpha,
        bool forceTransparent,
        int endpoint,
        int component,
        int delta,
        ref CcHiEncoding best)
    {
        GetRgb555Components(best.Color0, out var red0, out var green0, out var blue0);
        GetRgb555Components(best.Color1, out var red1, out var green1, out var blue1);

        if (endpoint == 0)
        {
            if (!TryOffsetRgb555Component(ref red0, ref green0, ref blue0, component, delta))
            {
                return false;
            }
        }
        else if (!TryOffsetRgb555Component(ref red1, ref green1, ref blue1, component, delta))
        {
            return false;
        }

        var color0 = PackRgb555FromComponents(red0, green0, blue0);
        var color1 = PackRgb555FromComponents(red1, green1, blue1);
        if (color0 == best.Color0 && color1 == best.Color1)
        {
            return false;
        }

        Span<int> indices = stackalloc int[TexelsPerBlock];
        var candidate = EvaluateCcHiCandidate(source, color0, color1, compareAlpha, forceTransparent, indices);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private void WriteCcHiEncoding(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1,
        bool compareAlpha,
        bool forceTransparent,
        Span<byte> destination)
    {
        Span<int> indices = stackalloc int[TexelsPerBlock];
        WriteCcHiEncoding(source, color0, color1, compareAlpha, forceTransparent, destination, indices);
    }

    private static void WriteCcHiEncoding(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1,
        bool compareAlpha,
        bool forceTransparent,
        Span<byte> destination,
        Span<int> indices)
    {
        destination.Clear();
        var decoded0 = UnpackRgb555(color0, byte.MaxValue);
        var decoded1 = UnpackRgb555(color1, byte.MaxValue);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[8];
        BuildCcHiPalette(decoded0, decoded1, palette);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = forceTransparent && IsTransparent(source[i])
                ? 7
                : FindNearestColorIndex(source[i], palette, 8, compareAlpha);
            indices[i] = index == 7 ? -1 : index;
            WriteBits(destination, i * 3, (ulong)index, 3);
        }

        WriteBits(destination, 96, color0, 15);
        WriteBits(destination, 111, color1, 15);
    }

    private static void DecodeCcChromaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        palette[0] = UnpackRgb555((ushort)ReadBits(source, 64, 15), byte.MaxValue);
        palette[1] = UnpackRgb555((ushort)ReadBits(source, 79, 15), byte.MaxValue);
        palette[2] = UnpackRgb555((ushort)ReadBits(source, 94, 15), byte.MaxValue);
        palette[3] = UnpackRgb555((ushort)ReadBits(source, 109, 15), byte.MaxValue);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = palette[(int)ReadBits(source, i * 2, 2)];
        }
    }

    private static void EncodeCcChromaBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        destination.Clear();
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildClusterPalette(source, palette, 4);

        for (var i = 0; i < palette.Length; i++)
        {
            palette[i] = UnpackRgb555(PackRgb555(palette[i]), byte.MaxValue);
        }

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            WriteBits(destination, i * 2, (ulong)FindNearestColorIndex(source[i], palette, 4, compareAlpha: false), 2);
        }

        WriteBits(destination, 64, PackRgb555(palette[0]), 15);
        WriteBits(destination, 79, PackRgb555(palette[1]), 15);
        WriteBits(destination, 94, PackRgb555(palette[2]), 15);
        WriteBits(destination, 109, PackRgb555(palette[3]), 15);
        WriteBits(destination, 125, 0b010, 3);
    }

    private static void EncodeCcChromaBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        Span<ushort> colors = stackalloc ushort[4];
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildClusterPalette(source, palette, 4);
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = PackRgb555(palette[i]);
        }

        var best = EvaluateCcChromaCandidate(source, colors);
        if (compressionMode == TextureCompressionLevel.Exhaustive
            && TryBuildGreedyCcChromaSeed(source, colors))
        {
            var candidate = EvaluateCcChromaCandidate(source, colors);
            if (candidate.Error < best.Error)
            {
                best = candidate;
            }
        }

        Span<int> sumsRed = stackalloc int[4];
        Span<int> sumsGreen = stackalloc int[4];
        Span<int> sumsBlue = stackalloc int[4];
        Span<int> counts = stackalloc int[4];
        Span<ushort> nextColors = stackalloc ushort[4];
        for (var iteration = 0; iteration < GetChromaOptimizationIterationLimit(compressionMode); iteration++)
        {
            sumsRed.Clear();
            sumsGreen.Clear();
            sumsBlue.Clear();
            counts.Clear();

            for (var i = 0; i < TexelsPerBlock; i++)
            {
                var index = (int)((best.Indices >> (i * 2)) & 0x3UL);
                sumsRed[index] += source[i].Red;
                sumsGreen[index] += source[i].Green;
                sumsBlue[index] += source[i].Blue;
                counts[index]++;
            }

            for (var i = 0; i < nextColors.Length; i++)
            {
                if (counts[i] == 0)
                {
                    nextColors[i] = PackRgb555(source[FindWorstColorIndex(source, best)]);
                    continue;
                }

                nextColors[i] = PackRgb555Nearest(
                    (double)sumsRed[i] / counts[i],
                    (double)sumsGreen[i] / counts[i],
                    (double)sumsBlue[i] / counts[i]);
            }

            var candidate = EvaluateCcChromaCandidate(source, nextColors);
            if (candidate.Error >= best.Error)
            {
                break;
            }

            best = candidate;
            if (best.Error == 0)
            {
                break;
            }
        }

        RefineCcChromaPalette(source, GetChromaRefinementPassLimit(compressionMode), ref best);
        WriteCcChromaEncoding(best, destination);
    }

    private static bool TryBuildGreedyCcChromaSeed(ReadOnlySpan<Rgba8UNorm> source, Span<ushort> seed)
    {
        Span<ushort> colors = stackalloc ushort[TexelsPerBlock];
        var colorCount = CollectUniqueRgb555Colors(source, 0, TexelsPerBlock, ignoreTransparent: false, colors);
        if (colorCount == 0)
        {
            return false;
        }

        if (colorCount <= seed.Length)
        {
            for (var i = 0; i < seed.Length; i++)
            {
                seed[i] = colors[Math.Min(i, colorCount - 1)];
            }

            return true;
        }

        var bestDistance = -1;
        var bestA = 0;
        var bestB = 1;
        for (var i = 0; i < colorCount; i++)
        {
            var colorA = UnpackRgb555(colors[i], byte.MaxValue);
            for (var j = i + 1; j < colorCount; j++)
            {
                var distance = ColorDistance(colorA, UnpackRgb555(colors[j], byte.MaxValue), compareAlpha: false);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestA = i;
                    bestB = j;
                }
            }
        }

        seed[0] = colors[bestA];
        seed[1] = colors[bestB];
        var seedCount = 2;
        while (seedCount < seed.Length)
        {
            var nextIndex = -1;
            var nextDistance = -1;
            for (var i = 0; i < colorCount; i++)
            {
                if (ContainsColor(seed[..seedCount], colors[i]))
                {
                    continue;
                }

                var color = UnpackRgb555(colors[i], byte.MaxValue);
                var minDistance = int.MaxValue;
                for (var j = 0; j < seedCount; j++)
                {
                    minDistance = Math.Min(
                        minDistance,
                        ColorDistance(color, UnpackRgb555(seed[j], byte.MaxValue), compareAlpha: false));
                }

                if (minDistance > nextDistance)
                {
                    nextDistance = minDistance;
                    nextIndex = i;
                }
            }

            if (nextIndex < 0)
            {
                seed[seedCount] = seed[seedCount - 1];
            }
            else
            {
                seed[seedCount] = colors[nextIndex];
            }

            seedCount++;
        }

        return true;
    }

    private static CcChromaEncoding EvaluateCcChromaCandidate(ReadOnlySpan<Rgba8UNorm> source, ReadOnlySpan<ushort> colors)
    {
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        for (var i = 0; i < palette.Length; i++)
        {
            palette[i] = UnpackRgb555(colors[i], byte.MaxValue);
        }

        ulong indices = 0;
        long error = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = FindNearestColorIndex(source[i], palette, 4, compareAlpha: false);
            indices |= (ulong)index << (i * 2);
            error += ColorDistance(source[i], palette[index], compareAlpha: false);
        }

        return new CcChromaEncoding
        {
            Color0 = colors[0],
            Color1 = colors[1],
            Color2 = colors[2],
            Color3 = colors[3],
            Indices = indices,
            Error = error
        };
    }

    private static void RefineCcChromaPalette(
        ReadOnlySpan<Rgba8UNorm> source,
        int passLimit,
        ref CcChromaEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var improved = false;
            for (var colorIndex = 0; colorIndex < 4; colorIndex++)
            {
                for (var component = 0; component < 3; component++)
                {
                    improved |= TryRefineCcChromaColor(source, colorIndex, component, -1, ref best);
                    improved |= TryRefineCcChromaColor(source, colorIndex, component, 1, ref best);
                }
            }

            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private static bool TryRefineCcChromaColor(
        ReadOnlySpan<Rgba8UNorm> source,
        int colorIndex,
        int component,
        int delta,
        ref CcChromaEncoding best)
    {
        Span<ushort> colors = stackalloc ushort[4];
        colors[0] = best.Color0;
        colors[1] = best.Color1;
        colors[2] = best.Color2;
        colors[3] = best.Color3;

        GetRgb555Components(colors[colorIndex], out var red, out var green, out var blue);
        if (!TryOffsetRgb555Component(ref red, ref green, ref blue, component, delta))
        {
            return false;
        }

        colors[colorIndex] = PackRgb555FromComponents(red, green, blue);
        var candidate = EvaluateCcChromaCandidate(source, colors);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private static int FindWorstColorIndex(ReadOnlySpan<Rgba8UNorm> source, CcChromaEncoding encoding)
    {
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        palette[0] = UnpackRgb555(encoding.Color0, byte.MaxValue);
        palette[1] = UnpackRgb555(encoding.Color1, byte.MaxValue);
        palette[2] = UnpackRgb555(encoding.Color2, byte.MaxValue);
        palette[3] = UnpackRgb555(encoding.Color3, byte.MaxValue);

        var worstIndex = 0;
        var worstDistance = -1;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)((encoding.Indices >> (i * 2)) & 0x3UL);
            var distance = ColorDistance(source[i], palette[index], compareAlpha: false);
            if (distance > worstDistance)
            {
                worstDistance = distance;
                worstIndex = i;
            }
        }

        return worstIndex;
    }

    private static void WriteCcChromaEncoding(CcChromaEncoding encoding, Span<byte> destination)
    {
        destination.Clear();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            WriteBits(destination, i * 2, (encoding.Indices >> (i * 2)) & 0x3UL, 2);
        }

        WriteBits(destination, 64, encoding.Color0, 15);
        WriteBits(destination, 79, encoding.Color1, 15);
        WriteBits(destination, 94, encoding.Color2, 15);
        WriteBits(destination, 109, encoding.Color3, 15);
        WriteBits(destination, 125, 0b010, 3);
    }

    private static void DecodeCcMixedBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var alphaMode = ReadBits(source, 124, 1) != 0;
        var glsb1 = (int)ReadBits(source, 125, 1);
        var glsb3 = (int)ReadBits(source, 126, 1);
        var raw0 = (ushort)ReadBits(source, 64, 15);
        var raw1 = (ushort)ReadBits(source, 79, 15);
        var raw2 = (ushort)ReadBits(source, 94, 15);
        var raw3 = (ushort)ReadBits(source, 109, 15);

        var color1 = UnpackRgb565From15(raw1, glsb1, byte.MaxValue);
        var color3 = UnpackRgb565From15(raw3, glsb3, byte.MaxValue);
        var index0 = (int)ReadBits(source, 0, 2);
        var index16 = (int)ReadBits(source, 16 * 2, 2);
        var color0 = alphaMode
            ? UnpackRgb555(raw0, byte.MaxValue)
            : UnpackRgb565From15(raw0, ((index0 >> 1) & 1) ^ glsb1, byte.MaxValue);
        var color2 = alphaMode
            ? UnpackRgb555(raw2, byte.MaxValue)
            : UnpackRgb565From15(raw2, ((index16 >> 1) & 1) ^ glsb3, byte.MaxValue);

        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[8];
        BuildCcMixedPalette(color0, color1, alphaMode, palette[..4]);
        BuildCcMixedPalette(color2, color3, alphaMode, palette[4..]);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)ReadBits(source, i * 2, 2);
            var paletteOffset = i < LeftTexelCount ? 0 : 4;
            destination[i] = palette[paletteOffset + index];
        }
    }

    private static void EncodeCcMixedOpaqueBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        destination.Clear();
        EncodeCcMixedOpaqueHalf(source, destination, 0, 64, 79, 125);
        EncodeCcMixedOpaqueHalf(source, destination, LeftTexelCount, 94, 109, 126);
        WriteBits(destination, 127, 1, 1);
    }

    private static void EncodeCcMixedOpaqueBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        destination.Clear();
        var left = FindBestCcMixedOpaqueHalf(source, 0, 64, 79, 125, compressionMode);
        var right = FindBestCcMixedOpaqueHalf(source, LeftTexelCount, 94, 109, 126, compressionMode);
        WriteCcMixedOpaqueHalf(source, destination, 0, 64, 79, 125, left.Color0, left.Color1);
        WriteCcMixedOpaqueHalf(source, destination, LeftTexelCount, 94, 109, 126, right.Color0, right.Color1);
        WriteBits(destination, 127, 1, 1);
    }

    private static CcMixedOpaqueEncoding FindBestCcMixedOpaqueHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        TextureCompressionLevel compressionMode)
    {
        Span<Rgb565EndpointPair> seeds = stackalloc Rgb565EndpointPair[
            compressionMode == TextureCompressionLevel.Exhaustive ? 280 : 14];
        var seedCount = 0;

        FindColorBounds(source, start, LeftTexelCount, includeAlpha: false, ignoreTransparent: false, out var min, out var max);
        AddRgb565Seed(seeds, ref seedCount, PackRgb565(min), PackRgb565(max));
        AddRgb565Seed(seeds, ref seedCount, PackRgb565(max), PackRgb565(min));

        if (TryInsetColorBounds(min, max, out var insetMin, out var insetMax))
        {
            AddRgb565Seed(seeds, ref seedCount, PackRgb565(insetMin), PackRgb565(insetMax));
            AddRgb565Seed(seeds, ref seedCount, PackRgb565(insetMax), PackRgb565(insetMin));
        }

        if (TryFindPrincipalAxisColorEndpoints(source, start, LeftTexelCount, ignoreTransparent: false, out var axisMin, out var axisMax))
        {
            AddRgb565Seed(seeds, ref seedCount, PackRgb565Nearest(axisMin), PackRgb565Nearest(axisMax));
            AddRgb565Seed(seeds, ref seedCount, PackRgb565Nearest(axisMax), PackRgb565Nearest(axisMin));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindFarthestColorEndpoints(source, start, LeftTexelCount, ignoreTransparent: false, out var farA, out var farB))
        {
            AddRgb565Seed(seeds, ref seedCount, PackRgb565(farA), PackRgb565(farB));
            AddRgb565Seed(seeds, ref seedCount, PackRgb565(farB), PackRgb565(farA));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindAverageColor(source, start, LeftTexelCount, ignoreTransparent: false, out var average))
        {
            AddRgb565Seed(seeds, ref seedCount, PackRgb565Nearest(average), PackRgb565Nearest(average));
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueRgb565Seeds(source, start, LeftTexelCount, ignoreTransparent: false, seeds, ref seedCount);
        }

        var best = new CcMixedOpaqueEncoding { Error = long.MaxValue };
        var iterationLimit = GetColorOptimizationIterationLimit(compressionMode);
        for (var i = 0; i < seedCount; i++)
        {
            OptimizeCcMixedOpaqueSeed(
                source,
                start,
                color0BitOffset,
                color1BitOffset,
                greenLowBitOffset,
                seeds[i].Color0,
                seeds[i].Color1,
                iterationLimit,
                ref best);
        }

        RefineCcMixedOpaqueEndpoints(
            source,
            start,
            color0BitOffset,
            color1BitOffset,
            greenLowBitOffset,
            GetColorRefinementPassLimit(compressionMode),
            ref best);
        return best;
    }

    private static void AddRgb565Seed(Span<Rgb565EndpointPair> seeds, ref int seedCount, ushort color0, ushort color1)
    {
        for (var i = 0; i < seedCount; i++)
        {
            if (seeds[i].Color0 == color0 && seeds[i].Color1 == color1)
            {
                return;
            }
        }

        if (seedCount < seeds.Length)
        {
            seeds[seedCount++] = new Rgb565EndpointPair(color0, color1);
        }
    }

    private static void AddUniqueRgb565Seeds(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool ignoreTransparent,
        Span<Rgb565EndpointPair> seeds,
        ref int seedCount)
    {
        Span<ushort> colors = stackalloc ushort[LeftTexelCount];
        var colorCount = CollectUniqueRgb565Colors(source, start, count, ignoreTransparent, colors);
        for (var i = 0; i < colorCount; i++)
        {
            for (var j = 0; j < colorCount; j++)
            {
                AddRgb565Seed(seeds, ref seedCount, colors[i], colors[j]);
            }
        }
    }

    private static void OptimizeCcMixedOpaqueSeed(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1,
        int iterationLimit,
        ref CcMixedOpaqueEncoding best)
    {
        Span<int> indices = stackalloc int[LeftTexelCount];
        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateCcMixedOpaqueCandidate(
                source,
                start,
                color0BitOffset,
                color1BitOffset,
                greenLowBitOffset,
                color0,
                color1,
                indices);
            UpdateBestCcMixedOpaqueEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveRgb565Line(source, start, LeftTexelCount, indices, out var nextColor0, out var nextColor1))
            {
                return;
            }

            if (nextColor0 == color0 && nextColor1 == color1)
            {
                return;
            }

            color0 = nextColor0;
            color1 = nextColor1;
        }
    }

    private static CcMixedOpaqueEncoding EvaluateCcMixedOpaqueCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1,
        Span<int> solvedIndices)
    {
        Span<byte> encoded = stackalloc byte[BytesPerBlock];
        encoded.Clear();
        WriteCcMixedOpaqueHalf(source, encoded, start, color0BitOffset, color1BitOffset, greenLowBitOffset, color0, color1, solvedIndices);
        WriteBits(encoded, 127, 1, 1);

        Span<Rgba8UNorm> decoded = stackalloc Rgba8UNorm[TexelsPerBlock];
        DecodeCcMixedBlock(encoded, decoded);

        long error = 0;
        for (var i = 0; i < LeftTexelCount; i++)
        {
            var sourceIndex = start + i;
            error += ColorDistance(source[sourceIndex], decoded[sourceIndex], compareAlpha: false);
        }

        return new CcMixedOpaqueEncoding { Color0 = color0, Color1 = color1, Error = error };
    }

    private static void RefineCcMixedOpaqueEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        int passLimit,
        ref CcMixedOpaqueEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var improved = false;
            for (var endpoint = 0; endpoint < 2; endpoint++)
            {
                for (var component = 0; component < 3; component++)
                {
                    improved |= TryRefineCcMixedOpaqueEndpoint(source, start, color0BitOffset, color1BitOffset, greenLowBitOffset, endpoint, component, -1, ref best);
                    improved |= TryRefineCcMixedOpaqueEndpoint(source, start, color0BitOffset, color1BitOffset, greenLowBitOffset, endpoint, component, 1, ref best);
                }
            }

            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private static bool TryRefineCcMixedOpaqueEndpoint(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        int endpoint,
        int component,
        int delta,
        ref CcMixedOpaqueEncoding best)
    {
        GetRgb565Components(best.Color0, out var red0, out var green0, out var blue0);
        GetRgb565Components(best.Color1, out var red1, out var green1, out var blue1);

        if (endpoint == 0)
        {
            if (!TryOffsetRgb565Component(ref red0, ref green0, ref blue0, component, delta))
            {
                return false;
            }
        }
        else if (!TryOffsetRgb565Component(ref red1, ref green1, ref blue1, component, delta))
        {
            return false;
        }

        var color0 = PackRgb565FromComponents(red0, green0, blue0);
        var color1 = PackRgb565FromComponents(red1, green1, blue1);
        if (color0 == best.Color0 && color1 == best.Color1)
        {
            return false;
        }

        Span<int> indices = stackalloc int[LeftTexelCount];
        var candidate = EvaluateCcMixedOpaqueCandidate(
            source,
            start,
            color0BitOffset,
            color1BitOffset,
            greenLowBitOffset,
            color0,
            color1,
            indices);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private static void WriteCcMixedOpaqueHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1)
    {
        Span<int> indices = stackalloc int[LeftTexelCount];
        WriteCcMixedOpaqueHalf(source, destination, start, color0BitOffset, color1BitOffset, greenLowBitOffset, color0, color1, indices);
    }

    private static void WriteCcMixedOpaqueHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1,
        Span<int> solvedIndices)
    {
        var decoded0 = UnpackRgb565(color0, byte.MaxValue);
        var decoded1 = UnpackRgb565(color1, byte.MaxValue);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildFourColorPalette(decoded0, decoded1, includeAlpha: false, palette);

        Span<int> indices = stackalloc int[LeftTexelCount];
        for (var i = 0; i < LeftTexelCount; i++)
        {
            indices[i] = FindNearestColorIndex(source[start + i], palette, 4, compareAlpha: false);
            solvedIndices[i] = indices[i];
        }

        var green0LowBit = GetRgb565Green(color0) & 1;
        var green1LowBit = GetRgb565Green(color1) & 1;
        if (((indices[0] >> 1) & 1) != (green0LowBit ^ green1LowBit))
        {
            (color0, color1) = (color1, color0);
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = 3 - indices[i];
            }

            green1LowBit = GetRgb565Green(color1) & 1;
        }

        for (var i = 0; i < LeftTexelCount; i++)
        {
            WriteBits(destination, (start + i) * 2, (ulong)indices[i], 2);
        }

        WriteBits(destination, color0BitOffset, PackRgb565WithoutGreenLowBit(color0), 15);
        WriteBits(destination, color1BitOffset, PackRgb565WithoutGreenLowBit(color1), 15);
        WriteBits(destination, greenLowBitOffset, (ulong)green1LowBit, 1);
    }

    private static void EncodeCcMixedOpaqueHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset)
    {
        FindMinMaxByVariance(source, start, LeftTexelCount, out var color0, out var color1);

        Span<int> indices = stackalloc int[LeftTexelCount];
        BuildCcMixedOpaqueIndices(source, start, color0, color1, indices);

        var green0LowBit = Quantize6(color0.Green) & 1;
        var green1LowBit = Quantize6(color1.Green) & 1;
        if (((indices[0] >> 1) & 1) != (green0LowBit ^ green1LowBit))
        {
            (color0, color1) = (color1, color0);
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = 3 - indices[i];
            }

            green1LowBit = Quantize6(color1.Green) & 1;
        }

        for (var i = 0; i < LeftTexelCount; i++)
        {
            WriteBits(destination, (start + i) * 2, (ulong)indices[i], 2);
        }

        WriteBits(destination, color0BitOffset, PackRgb565WithoutGreenLowBit(color0), 15);
        WriteBits(destination, color1BitOffset, PackRgb565WithoutGreenLowBit(color1), 15);
        WriteBits(destination, greenLowBitOffset, (ulong)green1LowBit, 1);
    }

    private static void BuildCcMixedOpaqueIndices(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        Rgba8UNorm color0,
        Rgba8UNorm color1,
        Span<int> indices)
    {
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildFourColorPalette(color0, color1, includeAlpha: false, palette);
        for (var i = 0; i < LeftTexelCount; i++)
        {
            indices[i] = FindNearestColorIndex(source[start + i], palette, 4, compareAlpha: false);
        }
    }

    private static void EncodeCcMixedAlphaBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        destination.Clear();
        EncodeCcMixedAlphaHalf(source, destination, 0, 64, 79, 125);
        EncodeCcMixedAlphaHalf(source, destination, LeftTexelCount, 94, 109, 126);
        WriteBits(destination, 124, 1, 1);
        WriteBits(destination, 127, 1, 1);
    }

    private static void EncodeCcMixedAlphaBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        destination.Clear();
        var left = FindBestCcMixedAlphaHalf(source, 0, 64, 79, 125, compressionMode);
        var right = FindBestCcMixedAlphaHalf(source, LeftTexelCount, 94, 109, 126, compressionMode);
        WriteCcMixedAlphaHalf(source, destination, 0, 64, 79, 125, left.Color0, left.Color1);
        WriteCcMixedAlphaHalf(source, destination, LeftTexelCount, 94, 109, 126, right.Color0, right.Color1);
        WriteBits(destination, 124, 1, 1);
        WriteBits(destination, 127, 1, 1);
    }

    private static CcMixedAlphaEncoding FindBestCcMixedAlphaHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        TextureCompressionLevel compressionMode)
    {
        if (!HasOpaqueTexel(source, start, LeftTexelCount))
        {
            return new CcMixedAlphaEncoding { Color0 = 0, Color1 = 0, Error = 0 };
        }

        Span<MixedAlphaEndpointPair> seeds = stackalloc MixedAlphaEndpointPair[
            compressionMode == TextureCompressionLevel.Exhaustive ? 280 : 14];
        var seedCount = 0;

        FindColorBounds(source, start, LeftTexelCount, includeAlpha: false, ignoreTransparent: true, out var min, out var max);
        AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555(min), PackRgb565(max));
        AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555(max), PackRgb565(min));

        if (TryInsetColorBounds(min, max, out var insetMin, out var insetMax))
        {
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555(insetMin), PackRgb565(insetMax));
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555(insetMax), PackRgb565(insetMin));
        }

        if (TryFindPrincipalAxisColorEndpoints(source, start, LeftTexelCount, ignoreTransparent: true, out var axisMin, out var axisMax))
        {
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555Nearest(axisMin), PackRgb565Nearest(axisMax));
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555Nearest(axisMax), PackRgb565Nearest(axisMin));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindFarthestColorEndpoints(source, start, LeftTexelCount, ignoreTransparent: true, out var farA, out var farB))
        {
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555(farA), PackRgb565(farB));
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555(farB), PackRgb565(farA));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindAverageColor(source, start, LeftTexelCount, ignoreTransparent: true, out var average))
        {
            AddMixedAlphaSeed(seeds, ref seedCount, PackRgb555Nearest(average), PackRgb565Nearest(average));
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueMixedAlphaSeeds(source, start, seeds, ref seedCount);
        }

        var best = new CcMixedAlphaEncoding { Error = long.MaxValue };
        var iterationLimit = GetColorOptimizationIterationLimit(compressionMode);
        for (var i = 0; i < seedCount; i++)
        {
            OptimizeCcMixedAlphaSeed(
                source,
                start,
                color0BitOffset,
                color1BitOffset,
                greenLowBitOffset,
                seeds[i].Color0,
                seeds[i].Color1,
                iterationLimit,
                ref best);
        }

        RefineCcMixedAlphaEndpoints(
            source,
            start,
            color0BitOffset,
            color1BitOffset,
            greenLowBitOffset,
            GetColorRefinementPassLimit(compressionMode),
            ref best);
        return best;
    }

    private static void AddMixedAlphaSeed(Span<MixedAlphaEndpointPair> seeds, ref int seedCount, ushort color0, ushort color1)
    {
        for (var i = 0; i < seedCount; i++)
        {
            if (seeds[i].Color0 == color0 && seeds[i].Color1 == color1)
            {
                return;
            }
        }

        if (seedCount < seeds.Length)
        {
            seeds[seedCount++] = new MixedAlphaEndpointPair(color0, color1);
        }
    }

    private static void AddUniqueMixedAlphaSeeds(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        Span<MixedAlphaEndpointPair> seeds,
        ref int seedCount)
    {
        Span<ushort> colors555 = stackalloc ushort[LeftTexelCount];
        Span<ushort> colors565 = stackalloc ushort[LeftTexelCount];
        var colorCount555 = CollectUniqueRgb555Colors(source, start, LeftTexelCount, ignoreTransparent: true, colors555);
        var colorCount565 = CollectUniqueRgb565Colors(source, start, LeftTexelCount, ignoreTransparent: true, colors565);
        for (var i = 0; i < colorCount555; i++)
        {
            for (var j = 0; j < colorCount565; j++)
            {
                AddMixedAlphaSeed(seeds, ref seedCount, colors555[i], colors565[j]);
            }
        }
    }

    private static void OptimizeCcMixedAlphaSeed(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1,
        int iterationLimit,
        ref CcMixedAlphaEncoding best)
    {
        Span<int> indices = stackalloc int[LeftTexelCount];
        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateCcMixedAlphaCandidate(
                source,
                start,
                color0BitOffset,
                color1BitOffset,
                greenLowBitOffset,
                color0,
                color1,
                indices);
            UpdateBestCcMixedAlphaEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveMixedAlphaLine(source, start, indices, out var nextColor0, out var nextColor1))
            {
                return;
            }

            if (nextColor0 == color0 && nextColor1 == color1)
            {
                return;
            }

            color0 = nextColor0;
            color1 = nextColor1;
        }
    }

    private static CcMixedAlphaEncoding EvaluateCcMixedAlphaCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1,
        Span<int> solvedIndices)
    {
        Span<byte> encoded = stackalloc byte[BytesPerBlock];
        encoded.Clear();
        WriteCcMixedAlphaHalf(source, encoded, start, color0BitOffset, color1BitOffset, greenLowBitOffset, color0, color1, solvedIndices);
        WriteBits(encoded, 124, 1, 1);
        WriteBits(encoded, 127, 1, 1);

        Span<Rgba8UNorm> decoded = stackalloc Rgba8UNorm[TexelsPerBlock];
        DecodeCcMixedBlock(encoded, decoded);

        long error = 0;
        for (var i = 0; i < LeftTexelCount; i++)
        {
            var sourceIndex = start + i;
            error += ColorDistance(source[sourceIndex], decoded[sourceIndex], compareAlpha: true);
        }

        return new CcMixedAlphaEncoding { Color0 = color0, Color1 = color1, Error = error };
    }

    private static void RefineCcMixedAlphaEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        int passLimit,
        ref CcMixedAlphaEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var improved = false;
            for (var endpoint = 0; endpoint < 2; endpoint++)
            {
                for (var component = 0; component < 3; component++)
                {
                    improved |= TryRefineCcMixedAlphaEndpoint(source, start, color0BitOffset, color1BitOffset, greenLowBitOffset, endpoint, component, -1, ref best);
                    improved |= TryRefineCcMixedAlphaEndpoint(source, start, color0BitOffset, color1BitOffset, greenLowBitOffset, endpoint, component, 1, ref best);
                }
            }

            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private static bool TryRefineCcMixedAlphaEndpoint(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        int endpoint,
        int component,
        int delta,
        ref CcMixedAlphaEncoding best)
    {
        GetRgb555Components(best.Color0, out var red0, out var green0, out var blue0);
        GetRgb565Components(best.Color1, out var red1, out var green1, out var blue1);

        if (endpoint == 0)
        {
            if (!TryOffsetRgb555Component(ref red0, ref green0, ref blue0, component, delta))
            {
                return false;
            }
        }
        else if (!TryOffsetRgb565Component(ref red1, ref green1, ref blue1, component, delta))
        {
            return false;
        }

        var color0 = PackRgb555FromComponents(red0, green0, blue0);
        var color1 = PackRgb565FromComponents(red1, green1, blue1);
        if (color0 == best.Color0 && color1 == best.Color1)
        {
            return false;
        }

        Span<int> indices = stackalloc int[LeftTexelCount];
        var candidate = EvaluateCcMixedAlphaCandidate(
            source,
            start,
            color0BitOffset,
            color1BitOffset,
            greenLowBitOffset,
            color0,
            color1,
            indices);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private static void WriteCcMixedAlphaHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1)
    {
        Span<int> indices = stackalloc int[LeftTexelCount];
        WriteCcMixedAlphaHalf(source, destination, start, color0BitOffset, color1BitOffset, greenLowBitOffset, color0, color1, indices);
    }

    private static void WriteCcMixedAlphaHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset,
        ushort color0,
        ushort color1,
        Span<int> solvedIndices)
    {
        if (!HasOpaqueTexel(source, start, LeftTexelCount))
        {
            for (var i = 0; i < LeftTexelCount; i++)
            {
                solvedIndices[i] = -1;
                WriteBits(destination, (start + i) * 2, 3, 2);
            }

            return;
        }

        var decoded0 = UnpackRgb555(color0, byte.MaxValue);
        var decoded1 = UnpackRgb565(color1, byte.MaxValue);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildCcMixedPalette(decoded0, decoded1, alphaMode: true, palette);

        for (var i = 0; i < LeftTexelCount; i++)
        {
            var sourceIndex = start + i;
            var index = IsTransparent(source[sourceIndex])
                ? 3
                : FindNearestColorIndex(source[sourceIndex], palette, 3, compareAlpha: true);
            solvedIndices[i] = index == 3 ? -1 : index;
            WriteBits(destination, sourceIndex * 2, (ulong)index, 2);
        }

        WriteBits(destination, color0BitOffset, color0, 15);
        WriteBits(destination, color1BitOffset, PackRgb565WithoutGreenLowBit(color1), 15);
        WriteBits(destination, greenLowBitOffset, (ulong)(GetRgb565Green(color1) & 1), 1);
    }

    private static void EncodeCcMixedAlphaHalf(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        int start,
        int color0BitOffset,
        int color1BitOffset,
        int greenLowBitOffset)
    {
        FindNonTransparentMinMaxBySum(source, start, LeftTexelCount, out var color0, out var color1, out var hasColor);
        if (!hasColor)
        {
            for (var i = 0; i < LeftTexelCount; i++)
            {
                WriteBits(destination, (start + i) * 2, 3, 2);
            }

            return;
        }

        var green1LowBit = Quantize6(color1.Green) & 1;
        var decoded0 = UnpackRgb555(PackRgb555(color0), byte.MaxValue);
        var decoded1 = UnpackRgb565From15(PackRgb565WithoutGreenLowBit(color1), green1LowBit, byte.MaxValue);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildCcMixedPalette(decoded0, decoded1, alphaMode: true, palette);

        for (var i = 0; i < LeftTexelCount; i++)
        {
            var sourceIndex = start + i;
            var index = IsTransparent(source[sourceIndex])
                ? 3
                : FindNearestColorIndex(source[sourceIndex], palette, 3, compareAlpha: true);
            WriteBits(destination, sourceIndex * 2, (ulong)index, 2);
        }

        WriteBits(destination, color0BitOffset, PackRgb555(color0), 15);
        WriteBits(destination, color1BitOffset, PackRgb565WithoutGreenLowBit(color1), 15);
        WriteBits(destination, greenLowBitOffset, (ulong)green1LowBit, 1);
    }

    private static void DecodeCcAlphaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var color0 = UnpackRgb555((ushort)ReadBits(source, 64, 15), Expand5To8((int)ReadBits(source, 109, 5)));
        var color1 = UnpackRgb555((ushort)ReadBits(source, 79, 15), Expand5To8((int)ReadBits(source, 114, 5)));
        var color2 = UnpackRgb555((ushort)ReadBits(source, 94, 15), Expand5To8((int)ReadBits(source, 119, 5)));
        var lerp = ReadBits(source, 124, 1) != 0;
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[8];

        if (lerp)
        {
            BuildFourColorPalette(color0, color1, includeAlpha: true, palette[..4]);
            BuildFourColorPalette(color2, color1, includeAlpha: true, palette[4..]);
        }
        else
        {
            palette[0] = color0;
            palette[1] = color1;
            palette[2] = color2;
            palette[3] = default;
            palette[4] = color0;
            palette[5] = color1;
            palette[6] = color2;
            palette[7] = default;
        }

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)ReadBits(source, i * 2, 2);
            var paletteOffset = lerp && i >= LeftTexelCount ? 4 : 0;
            destination[i] = palette[paletteOffset + index];
        }
    }

    private static void EncodeCcAlphaBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        destination.Clear();
        FindMinMaxBySum(source, 0, LeftTexelCount, includeAlpha: true, out var leftMin, out var leftMax);
        FindMinMaxBySum(source, LeftTexelCount, LeftTexelCount, includeAlpha: true, out var rightMin, out var rightMax);
        ChooseCcAlphaEndpoints(leftMin, leftMax, rightMin, rightMax, out var color0, out var color1, out var color2);

        var decoded0 = UnpackRgb555(PackRgb555(color0), QuantizeAlpha5(color0.Alpha));
        var decoded1 = UnpackRgb555(PackRgb555(color1), QuantizeAlpha5(color1.Alpha));
        var decoded2 = UnpackRgb555(PackRgb555(color2), QuantizeAlpha5(color2.Alpha));

        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[8];
        BuildFourColorPalette(decoded0, decoded1, includeAlpha: true, palette[..4]);
        BuildFourColorPalette(decoded2, decoded1, includeAlpha: true, palette[4..]);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var paletteOffset = i < LeftTexelCount ? 0 : 4;
            WriteBits(destination, i * 2, (ulong)FindNearestColorIndex(source[i], palette[paletteOffset..(paletteOffset + 4)], 4, compareAlpha: true), 2);
        }

        WriteBits(destination, 64, PackRgb555(color0), 15);
        WriteBits(destination, 79, PackRgb555(color1), 15);
        WriteBits(destination, 94, PackRgb555(color2), 15);
        WriteBits(destination, 109, (ulong)Quantize5(color0.Alpha), 5);
        WriteBits(destination, 114, (ulong)Quantize5(color1.Alpha), 5);
        WriteBits(destination, 119, (ulong)Quantize5(color2.Alpha), 5);
        WriteBits(destination, 124, 1, 1);
        WriteBits(destination, 125, 0b011, 3);
    }

    private static void EncodeCcAlphaBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        Span<byte> best = stackalloc byte[BytesPerBlock];
        Span<byte> candidate = stackalloc byte[BytesPerBlock];
        var bestError = long.MaxValue;

        EncodeCcAlphaBlock(source, candidate);
        UpdateBestAlphaCandidate(source, candidate, best, ref bestError);

        EncodeCcAlphaExplicitBlockOptimized(source, candidate, compressionMode);
        UpdateBestAlphaCandidate(source, candidate, best, ref bestError);

        best.CopyTo(destination);
    }

    private static void UpdateBestAlphaCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        ReadOnlySpan<byte> candidate,
        Span<byte> best,
        ref long bestError)
    {
        Span<Rgba8UNorm> decoded = stackalloc Rgba8UNorm[TexelsPerBlock];
        DecodeCcAlphaBlock(candidate, decoded);

        long error = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            error += ColorDistance(source[i], decoded[i], compareAlpha: true);
        }

        if (error < bestError)
        {
            bestError = error;
            candidate.CopyTo(best);
        }
    }

    private static void EncodeCcAlphaExplicitBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        destination.Clear();
        if (!HasOpaqueTexel(source))
        {
            for (var i = 0; i < TexelsPerBlock; i++)
            {
                WriteBits(destination, i * 2, 3, 2);
            }

            WriteBits(destination, 125, 0b011, 3);
            return;
        }

        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[3];
        BuildRgbaClusterPalette(
            source,
            palette,
            3,
            ignoreTransparent: true,
            GetChromaOptimizationIterationLimit(compressionMode));

        Span<ushort> colors = stackalloc ushort[3];
        Span<byte> alphas = stackalloc byte[3];
        for (var i = 0; i < palette.Length; i++)
        {
            colors[i] = PackRgb555(palette[i]);
            alphas[i] = QuantizeAlpha5(palette[i].Alpha);
            palette[i] = UnpackRgb555(colors[i], alphas[i]);
        }

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = IsTransparent(source[i])
                ? 3
                : FindNearestColorIndex(source[i], palette, 3, compareAlpha: true);
            WriteBits(destination, i * 2, (ulong)index, 2);
        }

        WriteBits(destination, 64, colors[0], 15);
        WriteBits(destination, 79, colors[1], 15);
        WriteBits(destination, 94, colors[2], 15);
        WriteBits(destination, 109, (ulong)Quantize5(alphas[0]), 5);
        WriteBits(destination, 114, (ulong)Quantize5(alphas[1]), 5);
        WriteBits(destination, 119, (ulong)Quantize5(alphas[2]), 5);
        WriteBits(destination, 125, 0b011, 3);
    }

    private static void ChooseCcAlphaEndpoints(
        Rgba8UNorm leftMin,
        Rgba8UNorm leftMax,
        Rgba8UNorm rightMin,
        Rgba8UNorm rightMax,
        out Rgba8UNorm color0,
        out Rgba8UNorm color1,
        out Rgba8UNorm color2)
    {
        var leftEndpoints = new[] { leftMin, leftMax };
        var rightEndpoints = new[] { rightMin, rightMax };
        var bestLeft = 0;
        var bestRight = 0;
        var bestDistance = int.MaxValue;

        for (var left = 0; left < leftEndpoints.Length; left++)
        {
            for (var right = 0; right < rightEndpoints.Length; right++)
            {
                var distance = ColorDistance(leftEndpoints[left], rightEndpoints[right], compareAlpha: true);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLeft = left;
                    bestRight = right;
                }
            }
        }

        color0 = leftEndpoints[1 - bestLeft];
        color1 = Average(leftEndpoints[bestLeft], rightEndpoints[bestRight], includeAlpha: true);
        color2 = rightEndpoints[1 - bestRight];
    }

    private static void BuildCcHiPalette(Rgba8UNorm color0, Rgba8UNorm color1, Span<Rgba8UNorm> palette)
    {
        palette[0] = color0;
        for (var i = 1; i <= 5; i++)
        {
            palette[i] = Interpolate(color0, color1, 6 - i, i, 6, includeAlpha: false);
        }

        palette[6] = color1;
        palette[7] = default;
    }

    private static void BuildCcMixedPalette(Rgba8UNorm color0, Rgba8UNorm color1, bool alphaMode, Span<Rgba8UNorm> palette)
    {
        palette[0] = color0;
        if (alphaMode)
        {
            palette[1] = Interpolate(color0, color1, 1, 1, 2, includeAlpha: false);
            palette[2] = color1;
            palette[3] = default;
            return;
        }

        palette[1] = Interpolate(color0, color1, 2, 1, 3, includeAlpha: false);
        palette[2] = Interpolate(color0, color1, 1, 2, 3, includeAlpha: false);
        palette[3] = color1;
    }

    private static void BuildFourColorPalette(Rgba8UNorm color0, Rgba8UNorm color1, bool includeAlpha, Span<Rgba8UNorm> palette)
    {
        palette[0] = color0;
        palette[1] = Interpolate(color0, color1, 2, 1, 3, includeAlpha);
        palette[2] = Interpolate(color0, color1, 1, 2, 3, includeAlpha);
        palette[3] = color1;
    }

    private static Rgba8UNorm Interpolate(Rgba8UNorm a, Rgba8UNorm b, int weightA, int weightB, int divisor, bool includeAlpha) => new(
        (byte)(((weightA * a.Red) + (weightB * b.Red) + (divisor / 2)) / divisor),
        (byte)(((weightA * a.Green) + (weightB * b.Green) + (divisor / 2)) / divisor),
        (byte)(((weightA * a.Blue) + (weightB * b.Blue) + (divisor / 2)) / divisor),
        includeAlpha
            ? (byte)(((weightA * a.Alpha) + (weightB * b.Alpha) + (divisor / 2)) / divisor)
            : byte.MaxValue);

    private static Rgba8UNorm Average(Rgba8UNorm a, Rgba8UNorm b, bool includeAlpha) => new(
        (byte)((a.Red + b.Red + 1) / 2),
        (byte)((a.Green + b.Green + 1) / 2),
        (byte)((a.Blue + b.Blue + 1) / 2),
        includeAlpha ? (byte)((a.Alpha + b.Alpha + 1) / 2) : byte.MaxValue);

    private static Rgba8UNorm UnpackRgb555(ushort value, byte alpha)
    {
        var red = (value >> 10) & 0x1f;
        var green = (value >> 5) & 0x1f;
        var blue = value & 0x1f;
        return new Rgba8UNorm(Expand5To8(red), Expand5To8(green), Expand5To8(blue), alpha);
    }

    private static Rgba8UNorm UnpackRgb565From15(ushort value, int greenLowBit, byte alpha)
    {
        var red = (value >> 10) & 0x1f;
        var green = (((value >> 5) & 0x1f) << 1) | (greenLowBit & 1);
        var blue = value & 0x1f;
        return new Rgba8UNorm(Expand5To8(red), Expand6To8(green), Expand5To8(blue), alpha);
    }

    private static Rgba8UNorm UnpackRgb565(ushort value, byte alpha)
    {
        var red = (value >> 11) & 0x1f;
        var green = (value >> 5) & 0x3f;
        var blue = value & 0x1f;
        return new Rgba8UNorm(Expand5To8(red), Expand6To8(green), Expand5To8(blue), alpha);
    }

    private static ushort PackRgb555(Rgba8UNorm value) =>
        (ushort)((Quantize5(value.Red) << 10) | (Quantize5(value.Green) << 5) | Quantize5(value.Blue));

    private static ushort PackRgb555Nearest(RgbVector value) =>
        PackRgb555Nearest(value.Red, value.Green, value.Blue);

    private static ushort PackRgb555Nearest(double red, double green, double blue) =>
        PackRgb555FromComponents(
            QuantizeToBits(red, 31),
            QuantizeToBits(green, 31),
            QuantizeToBits(blue, 31));

    private static ushort PackRgb555FromComponents(int red, int green, int blue) =>
        (ushort)((red << 10) | (green << 5) | blue);

    private static ushort PackRgb565(Rgba8UNorm value) =>
        PackRgb565FromComponents(Quantize5(value.Red), Quantize6(value.Green), Quantize5(value.Blue));

    private static ushort PackRgb565Nearest(RgbVector value) =>
        PackRgb565Nearest(value.Red, value.Green, value.Blue);

    private static ushort PackRgb565Nearest(double red, double green, double blue) =>
        PackRgb565FromComponents(
            QuantizeToBits(red, 31),
            QuantizeToBits(green, 63),
            QuantizeToBits(blue, 31));

    private static ushort PackRgb565FromComponents(int red, int green, int blue) =>
        (ushort)((red << 11) | (green << 5) | blue);

    private static ushort PackRgb565WithoutGreenLowBit(Rgba8UNorm value)
    {
        var green6 = Quantize6(value.Green);
        return (ushort)((Quantize5(value.Red) << 10) | ((green6 >> 1) << 5) | Quantize5(value.Blue));
    }

    private static ushort PackRgb565WithoutGreenLowBit(ushort value)
    {
        GetRgb565Components(value, out var red, out var green, out var blue);
        return (ushort)((red << 10) | ((green >> 1) << 5) | blue);
    }

    private static int GetRgb565Green(ushort value) => (value >> 5) & 0x3f;

    private static int Quantize5(byte value) => (value * 31 + 127) / 255;

    private static int Quantize6(byte value) => (value * 63 + 127) / 255;

    private static byte QuantizeAlpha5(byte value) => Expand5To8(Quantize5(value));

    private static int QuantizeToBits(double value, int max)
    {
        var clamped = Math.Clamp(value, byte.MinValue, byte.MaxValue);
        return Math.Clamp((int)Math.Round(clamped * max / byte.MaxValue), 0, max);
    }

    private static byte Expand5To8(int value) => (byte)((value << 3) | (value >> 2));

    private static byte Expand6To8(int value) => (byte)((value << 2) | (value >> 4));

    private static void BuildClusterPalette(ReadOnlySpan<Rgba8UNorm> source, Span<Rgba8UNorm> palette, int paletteCount)
    {
        FindColorBounds(source, 0, TexelsPerBlock, includeAlpha: false, ignoreTransparent: false, out var min, out var max);
        for (var i = 0; i < paletteCount; i++)
        {
            palette[i] = paletteCount == 1
                ? min
                : Interpolate(min, max, paletteCount - 1 - i, i, paletteCount - 1, includeAlpha: false);
        }

        Span<int> sumsRed = stackalloc int[4];
        Span<int> sumsGreen = stackalloc int[4];
        Span<int> sumsBlue = stackalloc int[4];
        Span<int> counts = stackalloc int[4];

        for (var iteration = 0; iteration < 8; iteration++)
        {
            sumsRed.Clear();
            sumsGreen.Clear();
            sumsBlue.Clear();
            counts.Clear();

            for (var i = 0; i < TexelsPerBlock; i++)
            {
                var index = FindNearestColorIndex(source[i], palette, paletteCount, compareAlpha: false);
                sumsRed[index] += source[i].Red;
                sumsGreen[index] += source[i].Green;
                sumsBlue[index] += source[i].Blue;
                counts[index]++;
            }

            for (var i = 0; i < paletteCount; i++)
            {
                if (counts[i] == 0)
                {
                    palette[i] = source[FindWorstColorIndex(source, palette, paletteCount)];
                    continue;
                }

                palette[i] = new Rgba8UNorm(
                    (byte)((sumsRed[i] + (counts[i] / 2)) / counts[i]),
                    (byte)((sumsGreen[i] + (counts[i] / 2)) / counts[i]),
                    (byte)((sumsBlue[i] + (counts[i] / 2)) / counts[i]));
            }
        }
    }

    private static int FindWorstColorIndex(ReadOnlySpan<Rgba8UNorm> source, ReadOnlySpan<Rgba8UNorm> palette, int paletteCount)
    {
        var worstIndex = 0;
        var worstDistance = -1;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var nearest = palette[FindNearestColorIndex(source[i], palette, paletteCount, compareAlpha: false)];
            var distance = ColorDistance(source[i], nearest, compareAlpha: false);
            if (distance > worstDistance)
            {
                worstDistance = distance;
                worstIndex = i;
            }
        }

        return worstIndex;
    }

    private static void FindColorBounds(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool includeAlpha,
        bool ignoreTransparent,
        out Rgba8UNorm min,
        out Rgba8UNorm max)
    {
        var minRed = byte.MaxValue;
        var minGreen = byte.MaxValue;
        var minBlue = byte.MaxValue;
        var minAlpha = byte.MaxValue;
        var maxRed = byte.MinValue;
        var maxGreen = byte.MinValue;
        var maxBlue = byte.MinValue;
        var maxAlpha = byte.MinValue;
        var hasColor = false;

        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            hasColor = true;
            minRed = Math.Min(minRed, source[i].Red);
            minGreen = Math.Min(minGreen, source[i].Green);
            minBlue = Math.Min(minBlue, source[i].Blue);
            minAlpha = Math.Min(minAlpha, source[i].Alpha);
            maxRed = Math.Max(maxRed, source[i].Red);
            maxGreen = Math.Max(maxGreen, source[i].Green);
            maxBlue = Math.Max(maxBlue, source[i].Blue);
            maxAlpha = Math.Max(maxAlpha, source[i].Alpha);
        }

        if (!hasColor)
        {
            min = default;
            max = default;
            return;
        }

        min = new Rgba8UNorm(minRed, minGreen, minBlue, includeAlpha ? minAlpha : byte.MaxValue);
        max = new Rgba8UNorm(maxRed, maxGreen, maxBlue, includeAlpha ? maxAlpha : byte.MaxValue);
    }

    private static void FindMinMaxBySum(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool includeAlpha,
        out Rgba8UNorm min,
        out Rgba8UNorm max)
    {
        var minIndex = start;
        var maxIndex = start;
        var minSum = int.MaxValue;
        var maxSum = int.MinValue;

        for (var i = start; i < start + count; i++)
        {
            var sum = source[i].Red + source[i].Green + source[i].Blue + (includeAlpha ? source[i].Alpha : 0);
            if (sum < minSum)
            {
                minSum = sum;
                minIndex = i;
            }

            if (sum > maxSum)
            {
                maxSum = sum;
                maxIndex = i;
            }
        }

        min = WithAlpha(source[minIndex], includeAlpha ? source[minIndex].Alpha : byte.MaxValue);
        max = WithAlpha(source[maxIndex], includeAlpha ? source[maxIndex].Alpha : byte.MaxValue);
    }

    private static void FindNonTransparentMinMaxBySum(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        out Rgba8UNorm min,
        out Rgba8UNorm max,
        out bool hasColor)
    {
        var minIndex = start;
        var maxIndex = start;
        var minSum = int.MaxValue;
        var maxSum = int.MinValue;
        hasColor = false;

        for (var i = start; i < start + count; i++)
        {
            if (IsTransparent(source[i]))
            {
                continue;
            }

            hasColor = true;
            var sum = source[i].Red + source[i].Green + source[i].Blue;
            if (sum < minSum)
            {
                minSum = sum;
                minIndex = i;
            }

            if (sum > maxSum)
            {
                maxSum = sum;
                maxIndex = i;
            }
        }

        min = hasColor ? WithAlpha(source[minIndex], byte.MaxValue) : default;
        max = hasColor ? WithAlpha(source[maxIndex], byte.MaxValue) : default;
    }

    private static void FindMinMaxByVariance(ReadOnlySpan<Rgba8UNorm> source, int start, int count, out Rgba8UNorm min, out Rgba8UNorm max)
    {
        var channel = FindMaxVarianceChannel(source, start, count);
        var minIndex = start;
        var maxIndex = start;
        var minValue = int.MaxValue;
        var maxValue = int.MinValue;

        for (var i = start; i < start + count; i++)
        {
            var value = GetColorChannel(source[i], channel);
            if (value < minValue)
            {
                minValue = value;
                minIndex = i;
            }

            if (value > maxValue)
            {
                maxValue = value;
                maxIndex = i;
            }
        }

        min = WithAlpha(source[minIndex], byte.MaxValue);
        max = WithAlpha(source[maxIndex], byte.MaxValue);
    }

    private static int FindMaxVarianceChannel(ReadOnlySpan<Rgba8UNorm> source, int start, int count)
    {
        var bestChannel = 0;
        long bestVarianceScore = long.MinValue;

        for (var channel = 0; channel < 3; channel++)
        {
            long sum = 0;
            long sumSquared = 0;
            for (var i = start; i < start + count; i++)
            {
                var value = GetColorChannel(source[i], channel);
                sum += value;
                sumSquared += value * value;
            }

            var varianceScore = (sumSquared * count) - (sum * sum);
            if (varianceScore > bestVarianceScore)
            {
                bestVarianceScore = varianceScore;
                bestChannel = channel;
            }
        }

        return bestChannel;
    }

    private static bool TryInsetColorBounds(Rgba8UNorm min, Rgba8UNorm max, out Rgba8UNorm insetMin, out Rgba8UNorm insetMax)
    {
        var redRange = max.Red - min.Red;
        var greenRange = max.Green - min.Green;
        var blueRange = max.Blue - min.Blue;
        if (redRange == 0 && greenRange == 0 && blueRange == 0)
        {
            insetMin = default;
            insetMax = default;
            return false;
        }

        insetMin = new Rgba8UNorm(
            (byte)(min.Red + (redRange / 16)),
            (byte)(min.Green + (greenRange / 16)),
            (byte)(min.Blue + (blueRange / 16)));
        insetMax = new Rgba8UNorm(
            (byte)(max.Red - (redRange / 16)),
            (byte)(max.Green - (greenRange / 16)),
            (byte)(max.Blue - (blueRange / 16)));
        return true;
    }

    private static bool TryFindPrincipalAxisColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out RgbVector minEndpoint,
        out RgbVector maxEndpoint) =>
        TryFindPrincipalAxisColorEndpoints(source, 0, TexelsPerBlock, ignoreTransparent, out minEndpoint, out maxEndpoint);

    private static bool TryFindPrincipalAxisColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool ignoreTransparent,
        out RgbVector minEndpoint,
        out RgbVector maxEndpoint)
    {
        var colorCount = 0;
        var meanRed = 0d;
        var meanGreen = 0d;
        var meanBlue = 0d;
        var minRed = 255d;
        var minGreen = 255d;
        var minBlue = 255d;
        var maxRed = 0d;
        var maxGreen = 0d;
        var maxBlue = 0d;

        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            var red = source[i].Red;
            var green = source[i].Green;
            var blue = source[i].Blue;
            meanRed += red;
            meanGreen += green;
            meanBlue += blue;
            minRed = Math.Min(minRed, red);
            minGreen = Math.Min(minGreen, green);
            minBlue = Math.Min(minBlue, blue);
            maxRed = Math.Max(maxRed, red);
            maxGreen = Math.Max(maxGreen, green);
            maxBlue = Math.Max(maxBlue, blue);
            colorCount++;
        }

        if (colorCount == 0)
        {
            minEndpoint = default;
            maxEndpoint = default;
            return false;
        }

        meanRed /= colorCount;
        meanGreen /= colorCount;
        meanBlue /= colorCount;

        var covRedRed = 0d;
        var covRedGreen = 0d;
        var covRedBlue = 0d;
        var covGreenGreen = 0d;
        var covGreenBlue = 0d;
        var covBlueBlue = 0d;

        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            var red = source[i].Red - meanRed;
            var green = source[i].Green - meanGreen;
            var blue = source[i].Blue - meanBlue;
            covRedRed += red * red;
            covRedGreen += red * green;
            covRedBlue += red * blue;
            covGreenGreen += green * green;
            covGreenBlue += green * blue;
            covBlueBlue += blue * blue;
        }

        var axisRed = maxRed - minRed;
        var axisGreen = maxGreen - minGreen;
        var axisBlue = maxBlue - minBlue;
        if (!NormalizeVector(ref axisRed, ref axisGreen, ref axisBlue))
        {
            minEndpoint = default;
            maxEndpoint = default;
            return false;
        }

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var nextRed = (covRedRed * axisRed) + (covRedGreen * axisGreen) + (covRedBlue * axisBlue);
            var nextGreen = (covRedGreen * axisRed) + (covGreenGreen * axisGreen) + (covGreenBlue * axisBlue);
            var nextBlue = (covRedBlue * axisRed) + (covGreenBlue * axisGreen) + (covBlueBlue * axisBlue);
            if (!NormalizeVector(ref nextRed, ref nextGreen, ref nextBlue))
            {
                break;
            }

            axisRed = nextRed;
            axisGreen = nextGreen;
            axisBlue = nextBlue;
        }

        var minProjection = double.PositiveInfinity;
        var maxProjection = double.NegativeInfinity;
        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            var projection = ((source[i].Red - meanRed) * axisRed)
                + ((source[i].Green - meanGreen) * axisGreen)
                + ((source[i].Blue - meanBlue) * axisBlue);
            minProjection = Math.Min(minProjection, projection);
            maxProjection = Math.Max(maxProjection, projection);
        }

        if (maxProjection - minProjection < 0.5d)
        {
            minEndpoint = default;
            maxEndpoint = default;
            return false;
        }

        minEndpoint = new RgbVector(
            meanRed + (axisRed * minProjection),
            meanGreen + (axisGreen * minProjection),
            meanBlue + (axisBlue * minProjection));
        maxEndpoint = new RgbVector(
            meanRed + (axisRed * maxProjection),
            meanGreen + (axisGreen * maxProjection),
            meanBlue + (axisBlue * maxProjection));
        return true;
    }

    private static bool TryFindFarthestColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out Rgba8UNorm endpoint0,
        out Rgba8UNorm endpoint1) =>
        TryFindFarthestColorEndpoints(source, 0, TexelsPerBlock, ignoreTransparent, out endpoint0, out endpoint1);

    private static bool TryFindFarthestColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool ignoreTransparent,
        out Rgba8UNorm endpoint0,
        out Rgba8UNorm endpoint1)
    {
        endpoint0 = default;
        endpoint1 = default;
        var bestDistance = -1;

        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            for (var j = i + 1; j < start + count; j++)
            {
                if (ignoreTransparent && IsTransparent(source[j]))
                {
                    continue;
                }

                var distance = ColorDistance(source[i], source[j], compareAlpha: false);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    endpoint0 = WithAlpha(source[i], byte.MaxValue);
                    endpoint1 = WithAlpha(source[j], byte.MaxValue);
                }
            }
        }

        return bestDistance > 0;
    }

    private static bool TryFindAverageColor(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out RgbVector average) =>
        TryFindAverageColor(source, 0, TexelsPerBlock, ignoreTransparent, out average);

    private static bool TryFindAverageColor(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool ignoreTransparent,
        out RgbVector average)
    {
        var colorCount = 0;
        var red = 0d;
        var green = 0d;
        var blue = 0d;
        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            red += source[i].Red;
            green += source[i].Green;
            blue += source[i].Blue;
            colorCount++;
        }

        if (colorCount == 0)
        {
            average = default;
            return false;
        }

        average = new RgbVector(red / colorCount, green / colorCount, blue / colorCount);
        return true;
    }

    private static int GetColorOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        TextureCompressionLevel.Exhaustive => 12,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported FXT1 compression mode.")
    };

    private static int GetColorRefinementPassLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        TextureCompressionLevel.Exhaustive => 16,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported FXT1 compression mode.")
    };

    private static int GetChromaOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 6,
        TextureCompressionLevel.High => 12,
        TextureCompressionLevel.Exhaustive => 20,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported FXT1 compression mode.")
    };

    private static int GetChromaRefinementPassLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 2,
        TextureCompressionLevel.High => 8,
        TextureCompressionLevel.Exhaustive => 16,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported FXT1 compression mode.")
    };

    private static int CollectUniqueRgb555Colors(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool ignoreTransparent,
        Span<ushort> colors)
    {
        var colorCount = 0;
        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            var color = PackRgb555(source[i]);
            if (!ContainsColor(colors[..colorCount], color))
            {
                colors[colorCount++] = color;
            }
        }

        return colorCount;
    }

    private static int CollectUniqueRgb565Colors(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        bool ignoreTransparent,
        Span<ushort> colors)
    {
        var colorCount = 0;
        for (var i = start; i < start + count; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            var color = PackRgb565(source[i]);
            if (!ContainsColor(colors[..colorCount], color))
            {
                colors[colorCount++] = color;
            }
        }

        return colorCount;
    }

    private static bool ContainsColor(ReadOnlySpan<ushort> colors, ushort color)
    {
        for (var i = 0; i < colors.Length; i++)
        {
            if (colors[i] == color)
            {
                return true;
            }
        }

        return false;
    }

    private static void SelectRepresentativeRgb555Colors(ReadOnlySpan<ushort> colors, Span<ushort> representatives)
    {
        if (colors.Length == 0 || representatives.Length == 0)
        {
            return;
        }

        if (representatives.Length == 1 || colors.Length == 1)
        {
            representatives[0] = colors[0];
            return;
        }

        var bestDistance = -1;
        var bestA = 0;
        var bestB = 1;
        for (var i = 0; i < colors.Length; i++)
        {
            var colorA = UnpackRgb555(colors[i], byte.MaxValue);
            for (var j = i + 1; j < colors.Length; j++)
            {
                var distance = ColorDistance(colorA, UnpackRgb555(colors[j], byte.MaxValue), compareAlpha: false);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestA = i;
                    bestB = j;
                }
            }
        }

        representatives[0] = colors[bestA];
        representatives[1] = colors[bestB];
        var representativeCount = 2;
        while (representativeCount < representatives.Length)
        {
            var nextIndex = -1;
            var nextDistance = -1;
            for (var i = 0; i < colors.Length; i++)
            {
                if (ContainsColor(representatives[..representativeCount], colors[i]))
                {
                    continue;
                }

                var color = UnpackRgb555(colors[i], byte.MaxValue);
                var minDistance = int.MaxValue;
                for (var j = 0; j < representativeCount; j++)
                {
                    minDistance = Math.Min(
                        minDistance,
                        ColorDistance(color, UnpackRgb555(representatives[j], byte.MaxValue), compareAlpha: false));
                }

                if (minDistance > nextDistance)
                {
                    nextDistance = minDistance;
                    nextIndex = i;
                }
            }

            representatives[representativeCount] = nextIndex < 0
                ? representatives[representativeCount - 1]
                : colors[nextIndex];
            representativeCount++;
        }
    }

    private static bool TrySolveRgb555Line(
        ReadOnlySpan<Rgba8UNorm> source,
        ReadOnlySpan<int> indices,
        int paletteSteps,
        out ushort color0,
        out ushort color1)
    {
        return TrySolveRgbLine(source, 0, TexelsPerBlock, indices, paletteSteps, pack565: false, out color0, out color1);
    }

    private static bool TrySolveRgb565Line(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        ReadOnlySpan<int> indices,
        out ushort color0,
        out ushort color1)
    {
        return TrySolveRgbLine(source, start, count, indices, paletteSteps: 3, pack565: true, out color0, out color1);
    }

    private static bool TrySolveRgbLine(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        int count,
        ReadOnlySpan<int> indices,
        int paletteSteps,
        bool pack565,
        out ushort color0,
        out ushort color1)
    {
        var a00 = 0d;
        var a01 = 0d;
        var a11 = 0d;
        var b0Red = 0d;
        var b0Green = 0d;
        var b0Blue = 0d;
        var b1Red = 0d;
        var b1Green = 0d;
        var b1Blue = 0d;

        for (var i = 0; i < count; i++)
        {
            var index = indices[i];
            if (index < 0 || index > paletteSteps)
            {
                continue;
            }

            var weight0 = (double)(paletteSteps - index) / paletteSteps;
            var weight1 = (double)index / paletteSteps;
            var sourceIndex = start + i;
            a00 += weight0 * weight0;
            a01 += weight0 * weight1;
            a11 += weight1 * weight1;
            b0Red += weight0 * source[sourceIndex].Red;
            b0Green += weight0 * source[sourceIndex].Green;
            b0Blue += weight0 * source[sourceIndex].Blue;
            b1Red += weight1 * source[sourceIndex].Red;
            b1Green += weight1 * source[sourceIndex].Green;
            b1Blue += weight1 * source[sourceIndex].Blue;
        }

        var determinant = (a00 * a11) - (a01 * a01);
        if (Math.Abs(determinant) < 0.000001d)
        {
            color0 = 0;
            color1 = 0;
            return false;
        }

        var red0 = ((b0Red * a11) - (b1Red * a01)) / determinant;
        var green0 = ((b0Green * a11) - (b1Green * a01)) / determinant;
        var blue0 = ((b0Blue * a11) - (b1Blue * a01)) / determinant;
        var red1 = ((a00 * b1Red) - (a01 * b0Red)) / determinant;
        var green1 = ((a00 * b1Green) - (a01 * b0Green)) / determinant;
        var blue1 = ((a00 * b1Blue) - (a01 * b0Blue)) / determinant;

        if (pack565)
        {
            color0 = PackRgb565Nearest(red0, green0, blue0);
            color1 = PackRgb565Nearest(red1, green1, blue1);
        }
        else
        {
            color0 = PackRgb555Nearest(red0, green0, blue0);
            color1 = PackRgb555Nearest(red1, green1, blue1);
        }

        return true;
    }

    private static bool TrySolveMixedAlphaLine(
        ReadOnlySpan<Rgba8UNorm> source,
        int start,
        ReadOnlySpan<int> indices,
        out ushort color0,
        out ushort color1)
    {
        var a00 = 0d;
        var a01 = 0d;
        var a11 = 0d;
        var b0Red = 0d;
        var b0Green = 0d;
        var b0Blue = 0d;
        var b1Red = 0d;
        var b1Green = 0d;
        var b1Blue = 0d;

        for (var i = 0; i < LeftTexelCount; i++)
        {
            var index = indices[i];
            if (index < 0 || index > 2)
            {
                continue;
            }

            var weight0 = index switch
            {
                0 => 1d,
                1 => 0.5d,
                _ => 0d
            };
            var weight1 = index switch
            {
                0 => 0d,
                1 => 0.5d,
                _ => 1d
            };
            var sourceIndex = start + i;
            a00 += weight0 * weight0;
            a01 += weight0 * weight1;
            a11 += weight1 * weight1;
            b0Red += weight0 * source[sourceIndex].Red;
            b0Green += weight0 * source[sourceIndex].Green;
            b0Blue += weight0 * source[sourceIndex].Blue;
            b1Red += weight1 * source[sourceIndex].Red;
            b1Green += weight1 * source[sourceIndex].Green;
            b1Blue += weight1 * source[sourceIndex].Blue;
        }

        var determinant = (a00 * a11) - (a01 * a01);
        if (Math.Abs(determinant) < 0.000001d)
        {
            color0 = 0;
            color1 = 0;
            return false;
        }

        color0 = PackRgb555Nearest(
            ((b0Red * a11) - (b1Red * a01)) / determinant,
            ((b0Green * a11) - (b1Green * a01)) / determinant,
            ((b0Blue * a11) - (b1Blue * a01)) / determinant);
        color1 = PackRgb565Nearest(
            ((a00 * b1Red) - (a01 * b0Red)) / determinant,
            ((a00 * b1Green) - (a01 * b0Green)) / determinant,
            ((a00 * b1Blue) - (a01 * b0Blue)) / determinant);
        return true;
    }

    private static void BuildRgbaClusterPalette(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<Rgba8UNorm> palette,
        int paletteCount,
        bool ignoreTransparent,
        int iterationLimit)
    {
        FindColorBounds(source, 0, TexelsPerBlock, includeAlpha: true, ignoreTransparent, out var min, out var max);
        for (var i = 0; i < paletteCount; i++)
        {
            palette[i] = paletteCount == 1
                ? min
                : Interpolate(min, max, paletteCount - 1 - i, i, paletteCount - 1, includeAlpha: true);
        }

        Span<int> sumsRed = stackalloc int[4];
        Span<int> sumsGreen = stackalloc int[4];
        Span<int> sumsBlue = stackalloc int[4];
        Span<int> sumsAlpha = stackalloc int[4];
        Span<int> counts = stackalloc int[4];

        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            sumsRed.Clear();
            sumsGreen.Clear();
            sumsBlue.Clear();
            sumsAlpha.Clear();
            counts.Clear();

            for (var i = 0; i < TexelsPerBlock; i++)
            {
                if (ignoreTransparent && IsTransparent(source[i]))
                {
                    continue;
                }

                var index = FindNearestColorIndex(source[i], palette, paletteCount, compareAlpha: true);
                sumsRed[index] += source[i].Red;
                sumsGreen[index] += source[i].Green;
                sumsBlue[index] += source[i].Blue;
                sumsAlpha[index] += source[i].Alpha;
                counts[index]++;
            }

            for (var i = 0; i < paletteCount; i++)
            {
                if (counts[i] == 0)
                {
                    palette[i] = source[FindWorstRgbaColorIndex(source, palette, paletteCount, ignoreTransparent)];
                    continue;
                }

                palette[i] = new Rgba8UNorm(
                    (byte)((sumsRed[i] + (counts[i] / 2)) / counts[i]),
                    (byte)((sumsGreen[i] + (counts[i] / 2)) / counts[i]),
                    (byte)((sumsBlue[i] + (counts[i] / 2)) / counts[i]),
                    (byte)((sumsAlpha[i] + (counts[i] / 2)) / counts[i]));
            }
        }
    }

    private static int FindWorstRgbaColorIndex(
        ReadOnlySpan<Rgba8UNorm> source,
        ReadOnlySpan<Rgba8UNorm> palette,
        int paletteCount,
        bool ignoreTransparent)
    {
        var worstIndex = 0;
        var worstDistance = -1;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && IsTransparent(source[i]))
            {
                continue;
            }

            var nearest = palette[FindNearestColorIndex(source[i], palette, paletteCount, compareAlpha: true)];
            var distance = ColorDistance(source[i], nearest, compareAlpha: true);
            if (distance > worstDistance)
            {
                worstDistance = distance;
                worstIndex = i;
            }
        }

        return worstIndex;
    }

    private static void GetRgb555Components(ushort value, out int red, out int green, out int blue)
    {
        red = (value >> 10) & 0x1f;
        green = (value >> 5) & 0x1f;
        blue = value & 0x1f;
    }

    private static void GetRgb565Components(ushort value, out int red, out int green, out int blue)
    {
        red = (value >> 11) & 0x1f;
        green = (value >> 5) & 0x3f;
        blue = value & 0x1f;
    }

    private static bool TryOffsetRgb555Component(ref int red, ref int green, ref int blue, int component, int delta)
    {
        switch (component)
        {
            case 0:
                return TryOffsetComponent(ref red, delta, 31);
            case 1:
                return TryOffsetComponent(ref green, delta, 31);
            case 2:
                return TryOffsetComponent(ref blue, delta, 31);
            default:
                throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static bool TryOffsetRgb565Component(ref int red, ref int green, ref int blue, int component, int delta)
    {
        switch (component)
        {
            case 0:
                return TryOffsetComponent(ref red, delta, 31);
            case 1:
                return TryOffsetComponent(ref green, delta, 63);
            case 2:
                return TryOffsetComponent(ref blue, delta, 31);
            default:
                throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static bool TryOffsetComponent(ref int value, int delta, int max)
    {
        var next = value + delta;
        if (next < 0 || next > max)
        {
            return false;
        }

        value = next;
        return true;
    }

    private static bool NormalizeVector(ref double x, ref double y, ref double z)
    {
        var lengthSquared = (x * x) + (y * y) + (z * z);
        if (lengthSquared < 0.000001d)
        {
            return false;
        }

        var scale = 1d / Math.Sqrt(lengthSquared);
        x *= scale;
        y *= scale;
        z *= scale;
        return true;
    }

    private static byte GetColorChannel(Rgba8UNorm color, int channel) => channel switch
    {
        0 => color.Red,
        1 => color.Green,
        _ => color.Blue
    };

    private static int FindNearestColorIndex(Rgba8UNorm color, ReadOnlySpan<Rgba8UNorm> palette, int paletteCount, bool compareAlpha)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < paletteCount; i++)
        {
            var distance = ColorDistance(color, palette[i], compareAlpha);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int ColorDistance(Rgba8UNorm a, Rgba8UNorm b, bool compareAlpha)
    {
        var red = a.Red - b.Red;
        var green = a.Green - b.Green;
        var blue = a.Blue - b.Blue;
        var alpha = compareAlpha ? a.Alpha - b.Alpha : 0;
        return (red * red) + (green * green) + (blue * blue) + (alpha * alpha);
    }

    private static long SquaredDifference(byte a, byte b)
    {
        var difference = a - b;
        return difference * difference;
    }

    private static Rgba8UNorm WithAlpha(Rgba8UNorm color, byte alpha) => new(color.Red, color.Green, color.Blue, alpha);

    private static bool IsTransparent(Rgba8UNorm color) => color.Alpha < AlphaCutoff;

    private static bool HasTransparentTexel(ReadOnlySpan<Rgba8UNorm> source)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (IsTransparent(source[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOpaqueTexel(ReadOnlySpan<Rgba8UNorm> source) =>
        HasOpaqueTexel(source, 0, TexelsPerBlock);

    private static bool HasOpaqueTexel(ReadOnlySpan<Rgba8UNorm> source, int start, int count)
    {
        for (var i = start; i < start + count; i++)
        {
            if (!IsTransparent(source[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void UpdateBestCcHiEncoding(CcHiEncoding candidate, ref CcHiEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static void UpdateBestCcMixedOpaqueEncoding(CcMixedOpaqueEncoding candidate, ref CcMixedOpaqueEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static void UpdateBestCcMixedAlphaEncoding(CcMixedAlphaEncoding candidate, ref CcMixedAlphaEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static void LoadBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockWidth;
        var originY = blockY * BlockHeight;
        var lastSourceX = source.Width - 1;

        for (var y = 0; y < BlockHeight; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            for (var x = 0; x < BlockWidth; x++)
            {
                var sourceX = Math.Min(originX + x, lastSourceX);
                destination[GetTexelIndex(x, y)] = TPixel.ToRgba8UNorm(sourceRow[sourceX]);
            }
        }
    }

    private static void StoreBlock<TPixel>(
        ReadOnlySpan<Rgba8UNorm> block,
        int blockX,
        int blockY,
        BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockWidth;
        var originY = blockY * BlockHeight;

        for (var y = 0; y < BlockHeight; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            for (var x = 0; x < BlockWidth; x++)
            {
                var destinationX = originX + x;
                if (destinationX >= destination.Width)
                {
                    break;
                }

                destinationRow[destinationX] = TPixel.FromRgba8UNorm(block[GetTexelIndex(x, y)]);
            }
        }
    }

    private static int GetTexelIndex(int x, int y) =>
        x < 4 ? (y * 4) + x : LeftTexelCount + (y * 4) + (x - 4);

    private static ulong ReadBits(ReadOnlySpan<byte> source, int bitOffset, int bitCount)
    {
        ulong value = 0;
        for (var i = 0; i < bitCount; i++)
        {
            var sourceBit = bitOffset + i;
            if ((source[sourceBit >> 3] & (1 << (sourceBit & 7))) != 0)
            {
                value |= 1UL << i;
            }
        }

        return value;
    }

    private static void WriteBits(Span<byte> destination, int bitOffset, ulong value, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            if ((value & (1UL << i)) == 0)
            {
                continue;
            }

            var destinationBit = bitOffset + i;
            destination[destinationBit >> 3] |= (byte)(1 << (destinationBit & 7));
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded FXT1 texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded FXT1 texture.", nameof(destination));
        }
    }

    private static int GetBlockCountX(int width) => (width + BlockWidth - 1) / BlockWidth;

    private static int GetBlockCountY(int height) => (height + BlockHeight - 1) / BlockHeight;

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"FXT1 texture coder does not support texture format '{format.Name}'.");

    private readonly record struct RgbVector(double Red, double Green, double Blue);

    private readonly record struct CcHiEndpointPair(ushort Color0, ushort Color1);

    private readonly record struct Rgb565EndpointPair(ushort Color0, ushort Color1);

    private readonly record struct MixedAlphaEndpointPair(ushort Color0, ushort Color1);

    private struct CcHiEncoding
    {
        public ushort Color0;
        public ushort Color1;
        public long Error;
    }

    private struct CcChromaEncoding
    {
        public ushort Color0;
        public ushort Color1;
        public ushort Color2;
        public ushort Color3;
        public ulong Indices;
        public long Error;
    }

    private struct CcMixedOpaqueEncoding
    {
        public ushort Color0;
        public ushort Color1;
        public long Error;
    }

    private struct CcMixedAlphaEncoding
    {
        public ushort Color0;
        public ushort Color1;
        public long Error;
    }
}
