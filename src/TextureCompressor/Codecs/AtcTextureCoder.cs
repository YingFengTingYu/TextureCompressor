using System.Numerics;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;
using TextureCompressor.Options;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

public sealed class AtcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private readonly AtcTransfer _transfer;
    private readonly TextureCompressionOptions _options;

    public AtcTextureCoder(TextureFormat format, TextureCompressionOptions? options = null)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _options = options ?? new TextureCompressionOptions();
    }

    public TextureFormat Format { get; }

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

        return checked(rowPitch * GetBlockCount(height));
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
            case AtcTransfer.Rgb:
                Decode<TPixel, AtcRgbTransfer>(source, destination, rowPitch);
                return;
            case AtcTransfer.RgbaExplicitAlpha:
                Decode<TPixel, AtcRgbaExplicitAlphaTransfer>(source, destination, rowPitch);
                return;
            case AtcTransfer.RgbaInterpolatedAlpha:
                Decode<TPixel, AtcRgbaInterpolatedAlphaTransfer>(source, destination, rowPitch);
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
            case AtcTransfer.Rgb:
                Encode<TPixel, AtcRgbTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case AtcTransfer.RgbaExplicitAlpha:
                Encode<TPixel, AtcRgbaExplicitAlphaTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case AtcTransfer.RgbaInterpolatedAlpha:
                Encode<TPixel, AtcRgbaInterpolatedAlphaTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAtcTransfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                TTransfer.DecodeBlock(source.Slice(blockOffset, TTransfer.BytesPerBlock), block);
                StoreBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void Encode<TPixel, TTransfer>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        TextureCompressionLevel compressionMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAtcTransfer
    {
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);

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
                            TTransfer.EncodeBlock(
                                block,
                                localDestination.Slice(blockOffset, TTransfer.BytesPerBlock),
                                compressionMode);
                            blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
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
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IAtcTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode);
    }

    private readonly struct AtcRgbTransfer : IAtcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeColorBlock(source, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeColorBlock(source, destination, compressionMode);
    }

    private readonly struct AtcRgbaExplicitAlphaTransfer : IAtcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], destination);
            ExplicitAlphaBlock.Decode(source[..8], destination);
        }

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            ExplicitAlphaBlock.Encode(source, destination[..8]);
            EncodeColorBlock(source, destination[8..], compressionMode);
        }
    }

    private readonly struct AtcRgbaInterpolatedAlphaTransfer : IAtcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], destination);
            DecodeInterpolatedAlphaBlock(source[..8], destination);
        }

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            EncodeInterpolatedAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(source, destination[8..], compressionMode);
        }
    }

    private static void DecodeColorBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var color0 = BinaryPrimitives.ReadUInt16LittleEndian(source);
        var color1 = BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);
        var palette = new InlineArray4<Rgba8UNorm>();
        BuildColorPalette(color0, color1, palette);

        var indices = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = palette[(int)((indices >> (i * 2)) & 0x3u)];
        }
    }

    private static void EncodeColorBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeColorBlockFast(source, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeColorBlockOptimized(source, compressionMode, destination);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported ATC compression mode.");
        }
    }

    private static void EncodeColorBlockFast(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        FindColorBounds(source, out var min, out var max);

        var bestError = int.MaxValue;
        ushort bestColor0 = 0;
        ushort bestColor1 = 0;
        uint bestIndices = 0;

        TryEncodeColorCandidate(source, PackRgb555(min), PackRgb565(max), ref bestError, ref bestColor0, ref bestColor1, ref bestIndices);
        TryEncodeColorCandidate(source, PackRgb555(max), PackRgb565(min), ref bestError, ref bestColor0, ref bestColor1, ref bestIndices);
        TryEncodeColorCandidate(source, (ushort)(PackRgb555(max) | 0x8000), PackRgb565(min), ref bestError, ref bestColor0, ref bestColor1, ref bestIndices);
        TryEncodeColorCandidate(source, (ushort)(PackRgb555(max) | 0x8000), PackRgb565(max), ref bestError, ref bestColor0, ref bestColor1, ref bestIndices);

        BinaryPrimitives.WriteUInt16LittleEndian(destination, bestColor0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], bestColor1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], bestIndices);
    }

    private static void EncodeColorBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        Span<ColorEndpointPair> seeds = stackalloc ColorEndpointPair[
            compressionMode == TextureCompressionLevel.Exhaustive ? 536 : 24];
        var seedCount = 0;
        FindColorBounds(source, out var min, out var max);
        AddColorSeed(seeds, ref seedCount, PackRgb555(min), PackRgb565(max));
        AddColorSeed(seeds, ref seedCount, PackRgb555(max), PackRgb565(min));
        AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(max) | 0x8000), PackRgb565(min));
        AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(max) | 0x8000), PackRgb565(max));

        // The ATI reference splits the block into luminance regions and can
        // spend one selector on black. Seed both modes with actual region means.
        AddLuminanceSeeds(source, seeds, ref seedCount);
        if (TryInsetColorBounds(min, max, out var insetMin, out var insetMax))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb555(insetMin), PackRgb565(insetMax));
            AddColorSeed(seeds, ref seedCount, PackRgb555(insetMax), PackRgb565(insetMin));
            AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(insetMax) | 0x8000), PackRgb565(insetMin));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindFarthestColorEndpoints(source, out var farA, out var farB))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb555(farA), PackRgb565(farB));
            AddColorSeed(seeds, ref seedCount, PackRgb555(farB), PackRgb565(farA));
            AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(farA) | 0x8000), PackRgb565(farB));
            AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(farB) | 0x8000), PackRgb565(farA));
        }

        if (TryFindPrincipalAxisColorEndpoints(source, out var axisMin, out var axisMax))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb555(axisMin), PackRgb565(axisMax));
            AddColorSeed(seeds, ref seedCount, PackRgb555(axisMax), PackRgb565(axisMin));
            AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(axisMax) | 0x8000), PackRgb565(axisMin));
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindAverageColor(source, out var average))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb555(average), PackRgb565(average));
            AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(average) | 0x8000), PackRgb565(average));
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueColorSeeds(source, seeds, ref seedCount);
        }

        var best = new ColorBlockEncoding { Error = long.MaxValue };
        var iterationLimit = GetColorOptimizationIterationLimit(compressionMode);
        var refinementPasses = GetColorRefinementPassLimit(compressionMode);
        for (var i = 0; i < seedCount; i++)
        {
            var candidate = new ColorBlockEncoding { Error = long.MaxValue };
            OptimizeColorSeed(source, seeds[i].Color0, seeds[i].Color1, iterationLimit, ref candidate);
            if (compressionMode >= TextureCompressionLevel.High
                && (best.Error == long.MaxValue || candidate.Error <= best.Error + best.Error / 8)
                && (best.Error == long.MaxValue || candidate.Color0 != best.Color0 || candidate.Color1 != best.Color1))
            {
                RefineColorEndpoints(source, refinementPasses, ref candidate);
            }

            UpdateBestColorEncoding(candidate, ref best);
            if (best.Error == 0)
            {
                break;
            }
        }

        if (compressionMode == TextureCompressionLevel.Normal)
        {
            RefineColorEndpoints(source, refinementPasses, ref best);
        }
        else if (best.Error > 0)
        {
            Span<ushort> neighbors = stackalloc ushort[26];
            for (int pass = 0; pass < refinementPasses; pass++)
            {
                long initialError = best.Error;
                int count = BlockColorRefinement.GetNeighbors(best.Color0, true, neighbors);
                for (int i = 0; i < count; i++)
                {
                    UpdateBestColorEncoding(EvaluateColorCandidate(source, neighbors[i], best.Color1), ref best);
                }
                count = BlockColorRefinement.GetNeighbors(best.Color1, false, neighbors);
                for (int i = 0; i < count; i++)
                {
                    UpdateBestColorEncoding(EvaluateColorCandidate(source, best.Color0, neighbors[i]), ref best);
                }
                if (initialError == best.Error) break;
            }
        }

        BinaryPrimitives.WriteUInt16LittleEndian(destination, best.Color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], best.Color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], best.Indices);
    }

    private static void AddLuminanceSeeds(ReadOnlySpan<Rgba8UNorm> source, Span<ColorEndpointPair> seeds, ref int count)
    {
        Span<int> luma = stackalloc int[TexelsPerBlock];
        int low = 255, high = 0;
        for (int i = 0; i < luma.Length; i++)
        {
            var p = source[i];
            int y = (19 * p.Red + 38 * p.Green + 7 * p.Blue) >> 6;
            luma[i] = y;
            low = Math.Min(low, y);
            high = Math.Max(high, y);
        }
        if (low == high) return;

        Span<int> totals = stackalloc int[4];
        Span<Vector3> sums = stackalloc Vector3[4];
        totals.Clear();
        sums.Clear();
        for (int i = 0; i < luma.Length; i++)
        {
            var p = source[i];
            int region = Math.Min(3, 4 * (luma[i] - low) / (high - low));
            totals[region]++;
            sums[region] += new Vector3(p.Red, p.Green, p.Blue);
        }
        var dark = MeanColor(sums[0], totals[0]);
        var light = MeanColor(sums[3], totals[3]);
        AddColorSeed(seeds, ref count, PackRgb555(dark), PackRgb565(light));
        if (totals[2] > 0)
        {
            var middle = MeanColor(sums[2], totals[2]);
            AddColorSeed(seeds, ref count, (ushort)(0x8000 | PackRgb555(middle)), PackRgb565(light));
        }
    }

    private static Rgb24 MeanColor(Vector3 sum, int count) =>
        new((byte)(sum.X / count), (byte)(sum.Y / count), (byte)(sum.Z / count));

    private static void TryEncodeColorCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1,
        ref int bestError,
        ref ushort bestColor0,
        ref ushort bestColor1,
        ref uint bestIndices)
    {
        var palette = new InlineArray4<Rgba8UNorm>();
        BuildColorPalette(color0, color1, palette);

        var error = 0;
        uint indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = FindNearestColorIndex(source[i], palette, out var distance);
            error += distance;
            if (error >= bestError)
            {
                return;
            }

            indices |= (uint)index << (i * 2);
        }

        bestError = error;
        bestColor0 = color0;
        bestColor1 = color1;
        bestIndices = indices;
    }

    private static void AddColorSeed(Span<ColorEndpointPair> seeds, ref int count, ushort color0, ushort color1)
    {
        var seed = new ColorEndpointPair(color0, color1);
        for (var i = 0; i < count; i++)
        {
            if (seeds[i] == seed)
            {
                return;
            }
        }

        if (count < seeds.Length)
        {
            seeds[count++] = seed;
        }
    }

    private static void AddUniqueColorSeeds(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<ColorEndpointPair> seeds,
        ref int seedCount)
    {
        Span<Rgb24> colors = stackalloc Rgb24[TexelsPerBlock];
        var uniqueCount = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var color = ToRgb24(source[i]);
            var alreadyAdded = false;
            for (var j = 0; j < uniqueCount; j++)
            {
                if (colors[j] == color)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                colors[uniqueCount++] = color;
            }
        }

        for (var i = 0; i < uniqueCount; i++)
        {
            for (var j = 0; j < uniqueCount; j++)
            {
                AddColorSeed(seeds, ref seedCount, PackRgb555(colors[i]), PackRgb565(colors[j]));
                AddColorSeed(seeds, ref seedCount, (ushort)(PackRgb555(colors[i]) | 0x8000), PackRgb565(colors[j]));
            }
        }
    }

    private static void OptimizeColorSeed(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1,
        int iterationLimit,
        ref ColorBlockEncoding best)
    {
        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateColorCandidate(source, color0, color1);
            UpdateBestColorEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveColorEndpoints(current.Indices, source, IsSubtractiveColorMode(color0), out var nextColor0, out var nextColor1))
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

    private static ColorBlockEncoding EvaluateColorCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        ushort color0,
        ushort color1)
    {
        var palette = new InlineArray4<Rgba8UNorm>();
        BuildColorPalette(color0, color1, palette);

        long error = 0;
        uint indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = FindNearestColorIndex(source[i], palette, out var distance);
            error += distance;
            indices |= (uint)index << (i * 2);
        }

        return new ColorBlockEncoding(color0, color1, indices, error);
    }

    private static void UpdateBestColorEncoding(ColorBlockEncoding candidate, ref ColorBlockEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static bool TrySolveColorEndpoints(
        uint indices,
        ReadOnlySpan<Rgba8UNorm> source,
        bool subtractiveMode,
        out ushort color0,
        out ushort color1)
    {
        var aa = 0d;
        var ab = 0d;
        var bb = 0d;
        var ar = 0d;
        var ag = 0d;
        var ablu = 0d;
        var br = 0d;
        var bg = 0d;
        var bblu = 0d;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)((indices >> (i * 2)) & 0x3u);
            GetColorEndpointWeights(index, subtractiveMode, out var weightA, out var weightB);
            if (weightA == 0d && weightB == 0d)
            {
                continue;
            }

            var color = source[i];
            aa += weightA * weightA;
            ab += weightA * weightB;
            bb += weightB * weightB;
            ar += weightA * color.Red;
            ag += weightA * color.Green;
            ablu += weightA * color.Blue;
            br += weightB * color.Red;
            bg += weightB * color.Green;
            bblu += weightB * color.Blue;
        }

        var determinant = (aa * bb) - (ab * ab);
        if (Math.Abs(determinant) < 1e-8d)
        {
            color0 = 0;
            color1 = 0;
            return false;
        }

        var redA = SolveEndpointA(ar, br, aa, ab, bb, determinant);
        var greenA = SolveEndpointA(ag, bg, aa, ab, bb, determinant);
        var blueA = SolveEndpointA(ablu, bblu, aa, ab, bb, determinant);
        var redB = SolveEndpointB(ar, br, aa, ab, determinant);
        var greenB = SolveEndpointB(ag, bg, aa, ab, determinant);
        var blueB = SolveEndpointB(ablu, bblu, aa, ab, determinant);

        color0 = PackRgb555(new Rgb24(ClampToByte(redA), ClampToByte(greenA), ClampToByte(blueA)));
        if (subtractiveMode)
        {
            color0 |= 0x8000;
        }

        color1 = PackRgb565(new Rgb24(ClampToByte(redB), ClampToByte(greenB), ClampToByte(blueB)));
        return true;
    }

    private static void GetColorEndpointWeights(
        int index,
        bool subtractiveMode,
        out double weightA,
        out double weightB)
    {
        if (!subtractiveMode)
        {
            switch (index)
            {
                case 0:
                    weightA = 1d;
                    weightB = 0d;
                    return;
                case 1:
                    weightA = 5d / 8d;
                    weightB = 3d / 8d;
                    return;
                case 2:
                    weightA = 3d / 8d;
                    weightB = 5d / 8d;
                    return;
                default:
                    weightA = 0d;
                    weightB = 1d;
                    return;
            }
        }

        switch (index)
        {
            case 1:
                weightA = 1d;
                weightB = -0.25d;
                return;
            case 2:
                weightA = 1d;
                weightB = 0d;
                return;
            case 3:
                weightA = 0d;
                weightB = 1d;
                return;
            default:
                weightA = 0d;
                weightB = 0d;
                return;
        }
    }

    private static double SolveEndpointA(double ap, double bp, double aa, double ab, double bb, double determinant) =>
        ((ap * bb) - (bp * ab)) / determinant;

    private static double SolveEndpointB(double ap, double bp, double aa, double ab, double determinant) =>
        ((aa * bp) - (ab * ap)) / determinant;

    private static int GetColorOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        TextureCompressionLevel.Exhaustive => 12,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported ATC compression mode.")
    };

    private static int GetColorRefinementPassLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 1,
        TextureCompressionLevel.High => 2,
        TextureCompressionLevel.Exhaustive => 4,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported ATC compression mode.")
    };

    private static void RefineColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        int passLimit,
        ref ColorBlockEncoding best)
    {
        for (var pass = 0; pass < passLimit && best.Error > 0; pass++)
        {
            var mode = IsSubtractiveColorMode(best.Color0) ? ColorRamp.AtcBlack : ColorRamp.Atc;
            var pair = BlockColorRefinement.Refine(source, best.Color0, best.Color1, best.Indices, mode, passLimit == 1 ? 1 : 2);
            var candidate = EvaluateColorCandidate(source, pair.First, pair.Second);
            if (candidate.Error >= best.Error) break;
            best = candidate;
        }
    }

    private static void DecodeInterpolatedAlphaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var palette = new InlineArray8<byte>();
        InterpolatedAlphaBlock.BuildPalette(source[0], source[1], rounded: true, palette);

        var indices = ReadAlphaIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i].Alpha = palette[(int)((indices >> (i * 3)) & 0x7u)];
        }
    }

    private static void EncodeInterpolatedAlphaBlock(ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination, TextureCompressionLevel compressionMode)
    {
        Span<byte> values = stackalloc byte[TexelsPerBlock];
        for (int i = 0; i < values.Length; i++) values[i] = source[i].Alpha;
        InterpolatedAlphaBlock.Encode(values, destination, compressionMode, rounded: true);
    }

    private static void BuildColorPalette(ushort color0, ushort color1, Span<Rgba8UNorm> palette)
    {
        var c1 = UnpackRgb565(color1);
        if ((color0 & 0x8000) == 0)
        {
            var c0 = UnpackRgb555(color0);
            palette[0] = new Rgba8UNorm(c0.Red, c0.Green, c0.Blue);
            palette[1] = Interpolate(c0, c1, 5, 3, 8);
            palette[2] = Interpolate(c0, c1, 3, 5, 8);
            palette[3] = new Rgba8UNorm(c1.Red, c1.Green, c1.Blue);
            return;
        }

        var c2 = UnpackRgb555((ushort)(color0 & 0x7fff));
        palette[0] = new Rgba8UNorm(0, 0, 0);
        palette[1] = SubtractQuarter(c2, c1);
        palette[2] = new Rgba8UNorm(c2.Red, c2.Green, c2.Blue);
        palette[3] = new Rgba8UNorm(c1.Red, c1.Green, c1.Blue);
    }

    private static Rgba8UNorm Interpolate(Rgb24 a, Rgb24 b, int weightA, int weightB, int divisor) =>
        new(
            (byte)(((weightA * a.Red) + (weightB * b.Red)) / divisor),
            (byte)(((weightA * a.Green) + (weightB * b.Green)) / divisor),
            (byte)(((weightA * a.Blue) + (weightB * b.Blue)) / divisor));

    private static Rgba8UNorm SubtractQuarter(Rgb24 value, Rgb24 subtrahend) =>
        new(
            SubtractQuarter(value.Red, subtrahend.Red),
            SubtractQuarter(value.Green, subtrahend.Green),
            SubtractQuarter(value.Blue, subtrahend.Blue));

    private static byte SubtractQuarter(byte value, byte subtrahend) =>
        (byte)Math.Max(0, value - (subtrahend / 4));

    private static Rgb24 UnpackRgb555(ushort value)
    {
        var red = (value >> 10) & 0x1f;
        var green = (value >> 5) & 0x1f;
        var blue = value & 0x1f;
        return new Rgb24(Expand5(red), Expand5(green), Expand5(blue));
    }

    private static Rgb24 UnpackRgb565(ushort value)
    {
        var red = (value >> 11) & 0x1f;
        var green = (value >> 5) & 0x3f;
        var blue = value & 0x1f;
        return new Rgb24(Expand5(red), Expand6(green), Expand5(blue));
    }

    private static ushort PackRgb555(Rgb24 value)
    {
        var red = Quantize5(value.Red);
        var green = Quantize5(value.Green);
        var blue = Quantize5(value.Blue);
        return (ushort)((red << 10) | (green << 5) | blue);
    }

    private static ushort PackRgb565(Rgb24 value)
    {
        var red = Quantize5(value.Red);
        var green = Quantize6(value.Green);
        var blue = Quantize5(value.Blue);
        return (ushort)((red << 11) | (green << 5) | blue);
    }

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

    private static byte Expand6(int value) => (byte)((value << 2) | (value >> 4));

    private static ulong ReadAlphaIndices(ReadOnlySpan<byte> source)
    {
        ulong indices = 0;
        for (var i = 0; i < 6; i++)
        {
            indices |= (ulong)source[2 + i] << (8 * i);
        }

        return indices;
    }

    private static void FindColorBounds(ReadOnlySpan<Rgba8UNorm> source, out Rgb24 min, out Rgb24 max)
    {
        var minRed = byte.MaxValue;
        var minGreen = byte.MaxValue;
        var minBlue = byte.MaxValue;
        var maxRed = byte.MinValue;
        var maxGreen = byte.MinValue;
        var maxBlue = byte.MinValue;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            minRed = Math.Min(minRed, source[i].Red);
            minGreen = Math.Min(minGreen, source[i].Green);
            minBlue = Math.Min(minBlue, source[i].Blue);
            maxRed = Math.Max(maxRed, source[i].Red);
            maxGreen = Math.Max(maxGreen, source[i].Green);
            maxBlue = Math.Max(maxBlue, source[i].Blue);
        }

        min = new Rgb24(minRed, minGreen, minBlue);
        max = new Rgb24(maxRed, maxGreen, maxBlue);
    }

    private static bool TryInsetColorBounds(Rgb24 min, Rgb24 max, out Rgb24 insetMin, out Rgb24 insetMax)
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

        insetMin = new Rgb24(
            (byte)Math.Min(byte.MaxValue, min.Red + ((redRange + 8) / 16)),
            (byte)Math.Min(byte.MaxValue, min.Green + ((greenRange + 8) / 16)),
            (byte)Math.Min(byte.MaxValue, min.Blue + ((blueRange + 8) / 16)));
        insetMax = new Rgb24(
            (byte)Math.Max(byte.MinValue, max.Red - ((redRange + 8) / 16)),
            (byte)Math.Max(byte.MinValue, max.Green - ((greenRange + 8) / 16)),
            (byte)Math.Max(byte.MinValue, max.Blue - ((blueRange + 8) / 16)));
        return insetMin != min || insetMax != max;
    }

    private static bool TryFindFarthestColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        out Rgb24 endpointA,
        out Rgb24 endpointB)
    {
        var bestDistance = 0;
        endpointA = default;
        endpointB = default;
        for (var i = 0; i < TexelsPerBlock - 1; i++)
        {
            for (var j = i + 1; j < TexelsPerBlock; j++)
            {
                var distance = ColorDistance(source[i], source[j]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    endpointA = ToRgb24(source[i]);
                    endpointB = ToRgb24(source[j]);
                }
            }
        }

        return bestDistance > 0;
    }

    private static bool TryFindPrincipalAxisColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        out Rgb24 axisMin,
        out Rgb24 axisMax)
    {
        var averageRed = 0d;
        var averageGreen = 0d;
        var averageBlue = 0d;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            averageRed += source[i].Red;
            averageGreen += source[i].Green;
            averageBlue += source[i].Blue;
        }

        averageRed /= TexelsPerBlock;
        averageGreen /= TexelsPerBlock;
        averageBlue /= TexelsPerBlock;

        var cxx = 0d;
        var cxy = 0d;
        var cxz = 0d;
        var cyy = 0d;
        var cyz = 0d;
        var czz = 0d;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var red = source[i].Red - averageRed;
            var green = source[i].Green - averageGreen;
            var blue = source[i].Blue - averageBlue;
            cxx += red * red;
            cxy += red * green;
            cxz += red * blue;
            cyy += green * green;
            cyz += green * blue;
            czz += blue * blue;
        }

        var axisRed = 1d;
        var axisGreen = 1d;
        var axisBlue = 1d;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var nextRed = (cxx * axisRed) + (cxy * axisGreen) + (cxz * axisBlue);
            var nextGreen = (cxy * axisRed) + (cyy * axisGreen) + (cyz * axisBlue);
            var nextBlue = (cxz * axisRed) + (cyz * axisGreen) + (czz * axisBlue);
            if (!TryNormalizeVector(ref nextRed, ref nextGreen, ref nextBlue))
            {
                axisMin = default;
                axisMax = default;
                return false;
            }

            axisRed = nextRed;
            axisGreen = nextGreen;
            axisBlue = nextBlue;
        }

        var minProjection = double.PositiveInfinity;
        var maxProjection = double.NegativeInfinity;
        var minIndex = 0;
        var maxIndex = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var projection =
                ((source[i].Red - averageRed) * axisRed) +
                ((source[i].Green - averageGreen) * axisGreen) +
                ((source[i].Blue - averageBlue) * axisBlue);
            if (projection < minProjection)
            {
                minProjection = projection;
                minIndex = i;
            }

            if (projection > maxProjection)
            {
                maxProjection = projection;
                maxIndex = i;
            }
        }

        axisMin = ToRgb24(source[minIndex]);
        axisMax = ToRgb24(source[maxIndex]);
        return minIndex != maxIndex;
    }

    private static bool TryFindAverageColor(ReadOnlySpan<Rgba8UNorm> source, out Rgb24 average)
    {
        var red = 0;
        var green = 0;
        var blue = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            red += source[i].Red;
            green += source[i].Green;
            blue += source[i].Blue;
        }

        average = new Rgb24(
            (byte)((red + (TexelsPerBlock / 2)) / TexelsPerBlock),
            (byte)((green + (TexelsPerBlock / 2)) / TexelsPerBlock),
            (byte)((blue + (TexelsPerBlock / 2)) / TexelsPerBlock));
        return true;
    }

    private static int FindNearestColorIndex(Rgba8UNorm color, ReadOnlySpan<Rgba8UNorm> palette, out int bestDistance)
    {
        var bestIndex = 0;
        bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var red = color.Red - palette[i].Red;
            var green = color.Green - palette[i].Green;
            var blue = color.Blue - palette[i].Blue;
            var distance = (red * red) + (green * green) + (blue * blue);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool IsSubtractiveColorMode(ushort color0) => (color0 & 0x8000) != 0;

    private static Rgb24 ToRgb24(Rgba8UNorm color) => new(color.Red, color.Green, color.Blue);

    private static int ColorDistance(Rgba8UNorm a, Rgba8UNorm b)
    {
        var red = a.Red - b.Red;
        var green = a.Green - b.Green;
        var blue = a.Blue - b.Blue;
        return (red * red) + (green * green) + (blue * blue);
    }

    private static bool TryNormalizeVector(ref double x, ref double y, ref double z)
    {
        var lengthSquared = (x * x) + (y * y) + (z * z);
        if (lengthSquared <= double.Epsilon)
        {
            return false;
        }

        var scale = 1d / Math.Sqrt(lengthSquared);
        x *= scale;
        y *= scale;
        z *= scale;
        return true;
    }

    private static byte ClampToByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value), byte.MinValue, byte.MaxValue);

    private static int Quantize5(byte value)
    {
        var expanded = (value & 0xf8) | ((value & 0xe0) >> 5);
        var alternate = expanded ^ 0x08;
        return Math.Abs(value - alternate) < Math.Abs(value - expanded) ? alternate >> 3 : value >> 3;
    }

    private static int Quantize6(byte value)
    {
        var expanded = (value & 0xfc) | ((value & 0xc0) >> 6);
        var alternate = expanded ^ 0x04;
        return Math.Abs(value - alternate) < Math.Abs(value - expanded) ? alternate >> 2 : value >> 2;
    }

    private static void LoadBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
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

    private static void StoreBlock<TPixel>(
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

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded ATC texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded ATC texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size) => (size + BlockSize - 1) / BlockSize;

    private static bool TryGetTransfer(TextureFormat format, out AtcTransfer transfer)
    {
        if (format == TextureFormats.AtcRgb)
        {
            transfer = AtcTransfer.Rgb;
            return true;
        }

        if (format == TextureFormats.AtcRgbaExplicitAlpha)
        {
            transfer = AtcTransfer.RgbaExplicitAlpha;
            return true;
        }

        if (format == TextureFormats.AtcRgbaInterpolatedAlpha)
        {
            transfer = AtcTransfer.RgbaInterpolatedAlpha;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"ATC texture coder does not support texture format '{format.Name}'.");

    private readonly record struct Rgb24(byte Red, byte Green, byte Blue);

    private readonly record struct ColorEndpointPair(ushort Color0, ushort Color1);

    private readonly record struct ColorBlockEncoding(ushort Color0, ushort Color1, uint Indices, long Error);

    private enum AtcTransfer
    {
        Rgb,
        RgbaExplicitAlpha,
        RgbaInterpolatedAlpha
    }

}
