using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;
using TextureCompressor.Options;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

public sealed class BptcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;
    private const int BytesPerBlock = 16;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.Bc6HUFloat,
        TextureFormats.Bc6HSFloat,
        TextureFormats.Bc7UNorm,
        TextureFormats.Bc7Srgb,
        TextureFormats.RgbBptcUFloat,
        TextureFormats.RgbBptcSFloat,
        TextureFormats.RgbaBptcUNorm,
        TextureFormats.RgbaBptcSrgb
    ];

    private readonly BptcTransfer _transfer;
    private readonly TextureCompressionOptions _options;

    public BptcTextureCoder(TextureFormat format, TextureCompressionOptions? options = null)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        _options = options ?? new TextureCompressionOptions();
        Format = format;
    }

    public TextureFormat Format { get; }

    public TextureCompressionOptions Options => _options;

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
            case BptcTransfer.Bc6HUFloat:
                DecodeBc6H<TPixel, Bc6HUFloatTransfer>(source, destination, rowPitch);
                return;
            case BptcTransfer.Bc6HSFloat:
                DecodeBc6H<TPixel, Bc6HSFloatTransfer>(source, destination, rowPitch);
                return;
            case BptcTransfer.Bc7UNorm:
                DecodeBc7<TPixel, Bc7UNormTransfer>(source, destination, rowPitch);
                return;
            case BptcTransfer.Bc7Srgb:
                DecodeBc7<TPixel, Bc7SrgbTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeByTransfer<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (_options.CompressionMode is < TextureCompressionLevel.Fast or > TextureCompressionLevel.Exhaustive)
            throw new ArgumentOutOfRangeException(nameof(_options.CompressionMode), _options.CompressionMode, "Unsupported BPTC compression mode.");

        switch (_transfer)
        {
            case BptcTransfer.Bc6HUFloat:
                EncodeBc6H<TPixel, Bc6HUFloatTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case BptcTransfer.Bc6HSFloat:
                EncodeBc6H<TPixel, Bc6HSFloatTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case BptcTransfer.Bc7UNorm:
                EncodeBc7<TPixel, Bc7UNormTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case BptcTransfer.Bc7Srgb:
                EncodeBc7<TPixel, Bc7SrgbTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void DecodeBc6H<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBc6HTransfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        var block = new InlineArray16<Rgba32Float>();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                TTransfer.DecodeBlock(source.Slice(blockOffset, TTransfer.BytesPerBlock), block);
                StoreFloatBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void EncodeBc6H<TPixel, TTransfer>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        TextureCompressionLevel compressionMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBc6HTransfer
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
                        var block = new InlineArray16<Rgba32Float>();

                        var blockOffset = checked(blockY * rowPitch);
                        for (var blockX = 0; blockX < blockCountX; blockX++)
                        {
                            LoadFloatBlock(localSource, blockX, blockY, block);
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

        var block = new InlineArray16<Rgba32Float>();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadFloatBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void DecodeBc7<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBc7Transfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        var block = new InlineArray16<Rgba8UNorm>();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                DecodeBc7Block(source.Slice(blockOffset, BytesPerBlock), block);
                StoreUNormBlock<TPixel, TTransfer>(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void EncodeBc7<TPixel, TTransfer>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        TextureCompressionLevel compressionMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBc7Transfer
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
                        var block = new InlineArray16<Rgba8UNorm>();

                        var blockOffset = checked(blockY * rowPitch);
                        for (var blockX = 0; blockX < blockCountX; blockX++)
                        {
                            LoadUNormBlock<TPixel, TTransfer>(localSource, blockX, blockY, block);
                            EncodeBc7Block(
                                block,
                                localDestination.Slice(blockOffset, BytesPerBlock),
                                compressionMode);
                            blockOffset = checked(blockOffset + BytesPerBlock);
                        }
                    });
                }
            }

            return;
        }

        var block = new InlineArray16<Rgba8UNorm>();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadUNormBlock<TPixel, TTransfer>(source, blockX, blockY, block);
                EncodeBc7Block(block, destination.Slice(blockOffset, BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IBc6HTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba32Float> destination);

        static abstract void EncodeBlock(
            ReadOnlySpan<Rgba32Float> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode);
    }

    private interface IBc7Transfer
    {
        static abstract Rgba8UNorm ToStorage<TPixel>(TPixel pixel) where TPixel : unmanaged, IPixel<TPixel>;
        static abstract TPixel FromStorage<TPixel>(Rgba8UNorm pixel) where TPixel : unmanaged, IPixel<TPixel>;
    }

    private readonly struct Bc6HUFloatTransfer : IBc6HTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba32Float> destination) =>
            DecodeBc6HBlock(source, signed: false, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba32Float> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBc6HBlock(source, signed: false, destination, compressionMode);
    }

    private readonly struct Bc6HSFloatTransfer : IBc6HTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba32Float> destination) =>
            DecodeBc6HBlock(source, signed: true, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba32Float> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBc6HBlock(source, signed: true, destination, compressionMode);
    }

    private readonly struct Bc7UNormTransfer : IBc7Transfer
    {
        public static Rgba8UNorm ToStorage<TPixel>(TPixel pixel) where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba8UNorm(pixel);

        public static TPixel FromStorage<TPixel>(Rgba8UNorm pixel) where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba8UNorm(pixel);
    }

    private readonly struct Bc7SrgbTransfer : IBc7Transfer
    {
        public static Rgba8UNorm ToStorage<TPixel>(TPixel pixel) where TPixel : unmanaged, IPixel<TPixel> =>
            RgbaColorConversions.ToSrgbStorage(pixel);

        public static TPixel FromStorage<TPixel>(Rgba8UNorm pixel) where TPixel : unmanaged, IPixel<TPixel> =>
            RgbaColorConversions.FromSrgbStorage<TPixel>(pixel);
    }

    private static void DecodeBc6HBlock(ReadOnlySpan<byte> source, bool signed, Span<Rgba32Float> destination)
    {
        var bit = new BptcBitReader(source);
        var mode = bit.Read(2);
        if ((mode & 2) != 0)
        {
            mode |= bit.Read(3) << 2;
        }

        var modeInfo = SBc6HModes[mode];
        if (modeInfo.EndpointBits == 0)
        {
            FillFloatBlock(new Rgba32Float(0f, 0f, 0f, 1f), destination);
            return;
        }

        var epR = new InlineArray4<int>();
        var epG = new InlineArray4<int>();
        var epB = new InlineArray4<int>();

        TransferBc6HEndpoints(mode, ref bit, epR, epG, epB);

        if (signed)
        {
            epR[0] = SignExtend(epR[0], modeInfo.EndpointBits);
            epG[0] = SignExtend(epG[0], modeInfo.EndpointBits);
            epB[0] = SignExtend(epB[0], modeInfo.EndpointBits);
        }

        var subsetCount = modeInfo.PartitionBits == 0 ? 1 : 2;
        var endpointCount = subsetCount * 2;
        for (var i = 1; i < endpointCount; i++)
        {
            if (signed || modeInfo.Transformed)
            {
                epR[i] = SignExtend(epR[i], modeInfo.DeltaBitsR);
                epG[i] = SignExtend(epG[i], modeInfo.DeltaBitsG);
                epB[i] = SignExtend(epB[i], modeInfo.DeltaBitsB);
            }

            if (modeInfo.Transformed)
            {
                var mask = (1 << modeInfo.EndpointBits) - 1;
                epR[i] = (epR[i] + epR[0]) & mask;
                epG[i] = (epG[i] + epG[0]) & mask;
                epB[i] = (epB[i] + epB[0]) & mask;

                if (signed)
                {
                    epR[i] = SignExtend(epR[i], modeInfo.EndpointBits);
                    epG[i] = SignExtend(epG[i], modeInfo.EndpointBits);
                    epB[i] = SignExtend(epB[i], modeInfo.EndpointBits);
                }
            }
        }

        for (var i = 0; i < endpointCount; i++)
        {
            epR[i] = UnquantizeBc6HEndpoint(epR[i], signed, modeInfo.EndpointBits);
            epG[i] = UnquantizeBc6HEndpoint(epG[i], signed, modeInfo.EndpointBits);
            epB[i] = UnquantizeBc6HEndpoint(epB[i], signed, modeInfo.EndpointBits);
        }

        var partitionSet = modeInfo.PartitionBits == 0 ? 0 : bit.Read(5);
        var indexBits = modeInfo.PartitionBits == 0 ? 4 : 3;
        var factors = SBptcFactors[indexBits - 2];

        for (var texel = 0; texel < TexelsPerBlock; texel++)
        {
            var subset = 0;
            var anchor = 0;
            if (modeInfo.PartitionBits != 0)
            {
                subset = (SBptcP2[partitionSet] >> texel) & 1;
                anchor = subset != 0 ? SBptcA2[partitionSet] : 0;
            }

            var readBits = indexBits - (texel == anchor ? 1 : 0);
            var index = bit.Read(readBits);
            var weight = factors[index];
            var endpoint = subset * 2;

            var red = InterpolateBc6HHalf(epR[endpoint], epR[endpoint + 1], weight, signed);
            var green = InterpolateBc6HHalf(epG[endpoint], epG[endpoint + 1], weight, signed);
            var blue = InterpolateBc6HHalf(epB[endpoint], epB[endpoint + 1], weight, signed);
            destination[texel] = new Rgba32Float(HalfBitsToSingle(red), HalfBitsToSingle(green), HalfBitsToSingle(blue), 1f);
        }
    }

    private static void EncodeBc6HBlock(ReadOnlySpan<Rgba32Float> source, bool signed, Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        var colors = new InlineArray16<Rgba32Float>();
        for (int i = 0; i < TexelsPerBlock; i++) colors[i] = ClampBc6HColor(source[i], signed);
        var seeds = new InlineArray4<Rgba32Float>();
        FindBc6HSubsetEndpoints(colors, 0xffff, out seeds[0], out seeds[1]);
        float bestError = float.MaxValue;
        TryBc6HMode(colors, signed, 3, 0, seeds, destination, ref bestError);
        if (compressionMode == TextureCompressionLevel.Fast || bestError == 0) return;
        foreach (int mode in (ReadOnlySpan<int>)[7, 11, 15])
            TryBc6HBothDirections(colors, signed, mode, seeds, destination, ref bestError);
        if (bestError == 0) return;

        // Rank the 32 legal partitions by within-subset RGB variance. Only the
        // highest-ranked partitions need endpoint quantization and a palette search.
        Span<float> scores = stackalloc float[32];
        Span<int> partitions = stackalloc int[32];
        var total = Vector3.Zero;
        for (int i = 0; i < TexelsPerBlock; i++) total += Bc6HRgb(colors[i]);
        for (int p = 0; p < 32; p++)
        {
            uint mask = SBptcP2[p];
            int count = BitOperations.PopCount(mask);
            var sum = Vector3.Zero;
            while (mask != 0)
            {
                sum += Bc6HRgb(colors[BitOperations.TrailingZeroCount(mask)]);
                mask &= mask - 1;
            }
            // Total sum of squares is constant across partitions.
            scores[p] = -(sum.LengthSquared() / count + (total - sum).LengthSquared() / (16 - count));
            partitions[p] = p;
        }
        scores.Sort(partitions);
        const int highPartitionCount = 2;
        int limit = compressionMode switch
        {
            TextureCompressionLevel.Normal => 1,
            TextureCompressionLevel.High => highPartitionCount,
            _ => 32
        };
        for (int j = 0; j < limit && bestError > 0; j++)
        {
            if (j == 1)
            {
                FindBc6HComponentBounds(colors, out seeds[0], out seeds[1]);
                foreach (int mode in (ReadOnlySpan<int>)[3, 7, 11, 15])
                    TryBc6HBothDirections(colors, signed, mode, seeds, destination, ref bestError);
            }
            int p = partitions[j], mask = SBptcP2[p];
            FindBc6HSubsetEndpoints(colors, 0xffff ^ mask, out seeds[0], out seeds[1]);
            FindBc6HSubsetEndpoints(colors, mask, out seeds[2], out seeds[3]);
            for (int s = 0; s < 2; s++)
            {
                var anchor = colors[s == 0 ? 0 : SBptcA2[p]];
                if (ColorDistance(anchor, seeds[2 * s]) > ColorDistance(anchor, seeds[2 * s + 1]))
                    (seeds[2 * s], seeds[2 * s + 1]) = (seeds[2 * s + 1], seeds[2 * s]);
            }
            foreach (int mode in (ReadOnlySpan<int>)[30, 1, 14, 0, 18, 22, 26, 2, 6, 10])
                TryBc6HMode(colors, signed, mode, p, seeds, destination, ref bestError);
            if (j == 0 || j == highPartitionCount - 1) RefineBc6HBlock(colors, signed, destination, ref bestError);
        }

        if (compressionMode != TextureCompressionLevel.Exhaustive || bestError == 0) return;
        for (int i = 0; i < TexelsPerBlock; i++)
        for (int j = i + 1; j < TexelsPerBlock; j++)
        {
            seeds[0] = colors[i];
            seeds[1] = colors[j];
            TryBc6HMode(colors, signed, 3, 0, seeds, destination, ref bestError);
        }
        RefineBc6HBlock(colors, signed, destination, ref bestError);
    }

    private static void RefineBc6HBlock(ReadOnlySpan<Rgba32Float> colors, bool signed, Span<byte> best, ref float bestError)
    {
        if (bestError == 0) return;
        int mode = (best[0] & 2) == 0 ? best[0] & 3 : best[0] & 31;
        bool partitioned = SBc6HModes[mode].PartitionBits != 0;
        int subsets = partitioned ? 2 : 1, indexBits = partitioned ? 3 : 4;
        int offset = 128 - (16 * indexBits - subsets);
        var bits = new BptcBitReader(best);
        int partition = partitioned ? bits.Peek(offset - 5, 5) : 0;
        int mask = partitioned ? SBptcP2[partition] : 0;
        Span<BptcEndpointFit> fits = stackalloc BptcEndpointFit[2];
        fits.Clear();
        for (int i = 0; i < TexelsPerBlock; i++)
        {
            int subset = (mask >> i) & 1;
            int count = indexBits - (i == 0 || (partitioned && i == SBptcA2[partition]) ? 1 : 0);
            int index = bits.Peek(offset, count);
            offset += count;
            // BC6H interpolates signed magnitudes of half bit patterns before conversion to float.
            var c = colors[i];
            var codes = new Vector4(HalfCode(c.Red), HalfCode(c.Green), HalfCode(c.Blue), 0);
            fits[subset].Add(codes, SBptcFactors[indexBits - 2][index]);
        }
        var seeds = new InlineArray4<Rgba32Float>();
        for (int s = 0; s < subsets; s++)
        {
            fits[s].Solve(out var a, out var b);
            seeds[2 * s] = new(FromCode(a.X), FromCode(a.Y), FromCode(a.Z), 1);
            seeds[2 * s + 1] = new(FromCode(b.X), FromCode(b.Y), FromCode(b.Z), 1);
        }
        TryBc6HMode(colors, signed, mode, partition, seeds, best, ref bestError);

        static int HalfCode(float value)
        {
            int raw = BitConverter.HalfToUInt16Bits((Half)value);
            return (raw & 0x8000) == 0 ? raw : -(raw & 0x7fff);
        }

        float FromCode(float value)
        {
            int code = Math.Clamp((int)MathF.Round(value), signed ? -0x7bff : 0, 0x7bff);
            return HalfBitsToSingle((ushort)(code < 0 ? 0x8000 | -code : code));
        }
    }

    private static Vector3 Bc6HRgb(Rgba32Float c) => new(c.Red, c.Green, c.Blue);

    private static void FindBc6HSubsetEndpoints(ReadOnlySpan<Rgba32Float> colors, int mask,
        out Rgba32Float a, out Rgba32Float b)
    {
        a = b = colors[BitOperations.TrailingZeroCount((uint)mask)];
        float bestDistance = 0;
        while (mask != 0)
        {
            int i = BitOperations.TrailingZeroCount((uint)mask);
            mask &= mask - 1;
            int remaining = mask;
            while (remaining != 0)
            {
                int j = BitOperations.TrailingZeroCount((uint)remaining);
                float distance = ColorDistance(colors[i], colors[j]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    a = colors[i];
                    b = colors[j];
                }
                remaining &= remaining - 1;
            }
        }
    }

    private static void TryBc6HBothDirections(ReadOnlySpan<Rgba32Float> colors, bool signed, int mode,
        Span<Rgba32Float> seeds, Span<byte> destination, ref float bestError)
    {
        TryBc6HMode(colors, signed, mode, 0, seeds, destination, ref bestError);
        if (!SBc6HModes[mode].Transformed) return;
        (seeds[0], seeds[1]) = (seeds[1], seeds[0]);
        TryBc6HMode(colors, signed, mode, 0, seeds, destination, ref bestError);
        (seeds[0], seeds[1]) = (seeds[1], seeds[0]);
    }

    private static void TryBc6HMode(ReadOnlySpan<Rgba32Float> colors, bool signed, int mode, int partition,
        ReadOnlySpan<Rgba32Float> seeds, Span<byte> destination, ref float bestError)
    {
        var info = SBc6HModes[mode];
        bool partitioned = info.PartitionBits != 0;
        int subsets = partitioned ? 2 : 1, indexBits = partitioned ? 3 : 4;
        int paletteSize = 1 << indexBits, mask = (1 << info.EndpointBits) - 1;
        Span<int> fields = stackalloc int[12];
        Span<int> unquantized = stackalloc int[12];
        for (int channel = 0; channel < 3; channel++)
        {
            int offset = 4 * channel;
            int deltaBits = channel switch { 0 => info.DeltaBitsR, 1 => info.DeltaBitsG, _ => info.DeltaBitsB };
            for (int e = 0; e < subsets * 2; e++)
            {
                var color = seeds[e];
                float value = channel switch { 0 => color.Red, 1 => color.Green, _ => color.Blue };
                int q = QuantizeBc6HEndpoint(value, signed, info.EndpointBits);
                if (e > 0 && info.Transformed)
                {
                    int delta = SignExtend((q - fields[offset]) & mask, info.EndpointBits);
                    int bound = 1 << (deltaBits - 1);
                    if (partitioned && (delta < -bound || delta >= bound)) return;
                    delta = Math.Clamp(delta, -bound, bound - 1);
                    fields[offset + e] = delta;
                    q = (fields[offset] + delta) & mask;
                }
                else
                {
                    fields[offset + e] = q;
                }
                unquantized[offset + e] = UnquantizeBc6HEndpoint(signed ? SignExtend(q, info.EndpointBits) : q,
                    signed, info.EndpointBits);
            }
        }

        var indices = new InlineArray16<int>();
        var palette = new InlineArray16<Rgba32Float>();
        float error = 0;
        int subsetMask = partitioned ? SBptcP2[partition] : 0;
        for (int s = 0; s < subsets; s++)
        {
            int e = 2 * s;
            for (int k = 0; k < paletteSize; k++)
            {
                int w = SBptcFactors[indexBits - 2][k];
                palette[k] = new(
                    HalfBitsToSingle(InterpolateBc6HHalf(unquantized[e], unquantized[e + 1], w, signed)),
                    HalfBitsToSingle(InterpolateBc6HHalf(unquantized[e + 4], unquantized[e + 5], w, signed)),
                    HalfBitsToSingle(InterpolateBc6HHalf(unquantized[e + 8], unquantized[e + 9], w, signed)), 1);
            }
            int anchor = s == 0 ? 0 : SBptcA2[partition];
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (((subsetMask >> i) & 1) != s) continue;
                int count = info.Transformed && i == anchor ? paletteSize / 2 : paletteSize;
                int index = FindNearestColorIndex(colors[i], palette, count);
                indices[i] = index;
                error += ColorDistance(colors[i], palette[index]);
            }
            if (error >= bestError) return;
            if (indices[anchor] < paletteSize / 2) continue;
            for (int c = 0; c < 3; c++)
            {
                int offset = 4 * c + e;
                (fields[offset], fields[offset + 1]) = (fields[offset + 1], fields[offset]);
            }
            for (int i = 0; i < TexelsPerBlock; i++)
                if (((subsetMask >> i) & 1) == s) indices[i] = paletteSize - 1 - indices[i];
        }

        bestError = error;
        var writer = new BptcBitWriter(destination);
        writer.Write(mode, (mode & 2) == 0 ? 2 : 5);
        TransferBc6HEndpoints(mode, ref writer, fields[..4], fields[4..8], fields[8..]);
        writer.Write(partition, info.PartitionBits);
        for (int i = 0; i < TexelsPerBlock; i++)
        {
            bool anchor = i == 0 || (partitioned && i == SBptcA2[partition]);
            writer.Write(indices[i], indexBits - (anchor ? 1 : 0));
        }
    }

    private static void DecodeBc7Block(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var bit = new BptcBitReader(source);
        var mode = 0;
        while (mode < 8 && bit.Read(1) == 0)
        {
            mode++;
        }

        if (mode == 8)
        {
            FillUNormBlock(new Rgba8UNorm(0, 0, 0, 0), destination);
            return;
        }

        var modeInfo = SBc7Modes[mode];
        var modePBits = modeInfo.EndpointPBits != 0 ? modeInfo.EndpointPBits : modeInfo.SharedPBits;
        var partitionSet = bit.Read(modeInfo.PartitionBits);
        var rotationMode = bit.Read(modeInfo.RotationBits);
        var indexSelectionMode = bit.Read(modeInfo.IndexSelectionBits);

        var epR = new InlineArray6<int>();
        var epG = new InlineArray6<int>();
        var epB = new InlineArray6<int>();
        var epA = new InlineArray6<int>();

        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            epR[subset * 2] = bit.Read(modeInfo.ColorBits) << modePBits;
            epR[(subset * 2) + 1] = bit.Read(modeInfo.ColorBits) << modePBits;
        }

        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            epG[subset * 2] = bit.Read(modeInfo.ColorBits) << modePBits;
            epG[(subset * 2) + 1] = bit.Read(modeInfo.ColorBits) << modePBits;
        }

        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            epB[subset * 2] = bit.Read(modeInfo.ColorBits) << modePBits;
            epB[(subset * 2) + 1] = bit.Read(modeInfo.ColorBits) << modePBits;
        }

        if (modeInfo.AlphaBits == 0)
        {
            for (var i = 0; i < modeInfo.SubsetCount * 2; i++)
            {
                epA[i] = 255;
            }
        }
        else
        {
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                epA[subset * 2] = bit.Read(modeInfo.AlphaBits) << modePBits;
                epA[(subset * 2) + 1] = bit.Read(modeInfo.AlphaBits) << modePBits;
            }
        }

        if (modePBits != 0)
        {
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                var p0 = bit.Read(modePBits);
                var p1 = modeInfo.SharedPBits == 0 ? bit.Read(modePBits) : p0;
                ApplyBc7PBit(epR, subset, p0, p1);
                ApplyBc7PBit(epG, subset, p0, p1);
                ApplyBc7PBit(epB, subset, p0, p1);
                ApplyBc7PBit(epA, subset, p0, p1);
            }
        }

        var colorBits = modeInfo.ColorBits + modePBits;
        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            var endpoint = subset * 2;
            epR[endpoint] = ExpandQuantizedBc7(epR[endpoint], colorBits);
            epR[endpoint + 1] = ExpandQuantizedBc7(epR[endpoint + 1], colorBits);
            epG[endpoint] = ExpandQuantizedBc7(epG[endpoint], colorBits);
            epG[endpoint + 1] = ExpandQuantizedBc7(epG[endpoint + 1], colorBits);
            epB[endpoint] = ExpandQuantizedBc7(epB[endpoint], colorBits);
            epB[endpoint + 1] = ExpandQuantizedBc7(epB[endpoint + 1], colorBits);
        }

        if (modeInfo.AlphaBits != 0)
        {
            var alphaBits = modeInfo.AlphaBits + modePBits;
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                var endpoint = subset * 2;
                epA[endpoint] = ExpandQuantizedBc7(epA[endpoint], alphaBits);
                epA[endpoint + 1] = ExpandQuantizedBc7(epA[endpoint + 1], alphaBits);
            }
        }

        var hasSecondaryIndices = modeInfo.IndexBits1 != 0;
        var factors0 = SBptcFactors[modeInfo.IndexBits0 - 2];
        var factors1 = SBptcFactors[(hasSecondaryIndices ? modeInfo.IndexBits1 : modeInfo.IndexBits0) - 2];
        var offset0 = 0;
        var offset1 = modeInfo.SubsetCount * ((16 * modeInfo.IndexBits0) - 1);

        for (var texel = 0; texel < TexelsPerBlock; texel++)
        {
            var subset = GetBc7Subset(modeInfo, partitionSet, texel);
            var anchor = GetBc7Anchor(modeInfo, partitionSet, subset);
            var isAnchor = texel == anchor;
            var bits0 = modeInfo.IndexBits0 - (isAnchor ? 1 : 0);
            var bits1 = hasSecondaryIndices ? modeInfo.IndexBits1 - (isAnchor ? 1 : 0) : 0;
            var index0 = bit.Peek(offset0, bits0);
            var index1 = hasSecondaryIndices ? bit.Peek(offset1, bits1) : index0;
            offset0 += bits0;
            offset1 += bits1;

            var colorIndex = indexSelectionMode == 1 ? index1 : index0;
            var alphaIndex = indexSelectionMode == 0 ? index1 : index0;
            var colorFactors = indexSelectionMode == 1 ? factors1 : factors0;
            var alphaFactors = indexSelectionMode == 0 ? factors1 : factors0;
            var colorWeight = colorFactors[colorIndex];
            var alphaWeight = alphaFactors[alphaIndex];
            var endpoint = subset * 2;

            var red = InterpolateByte(epR[endpoint], epR[endpoint + 1], colorWeight);
            var green = InterpolateByte(epG[endpoint], epG[endpoint + 1], colorWeight);
            var blue = InterpolateByte(epB[endpoint], epB[endpoint + 1], colorWeight);
            var alpha = InterpolateByte(epA[endpoint], epA[endpoint + 1], alphaWeight);

            switch (rotationMode)
            {
                case 1:
                    (alpha, red) = (red, alpha);
                    break;
                case 2:
                    (alpha, green) = (green, alpha);
                    break;
                case 3:
                    (alpha, blue) = (blue, alpha);
                    break;
            }

            destination[texel] = new Rgba8UNorm(
                (byte)red,
                (byte)green,
                (byte)blue,
                (byte)alpha);
        }
    }

    private static void EncodeBc7Block(
        ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode)
    {
        var storage = new InlineArray16<Bc7Color32>();
        int opaqueError = 0;
        for (int i = 0; i < TexelsPerBlock; i++)
        {
            var c = source[i];
            storage[i] = new(c.Red, c.Green, c.Blue, c.Alpha);
            opaqueError += Squared(255 - storage[i].Alpha);
        }

        var bestError = int.MaxValue;
        var endpoints = new InlineArray6<Bc7Color32>();
        FindBc7Endpoints(storage, true, out endpoints[0], out endpoints[1]);
        TryBc7Endpoints(storage, 6, 0, 0, 0, endpoints, destination, ref bestError);
        if (bestError == 0) return;
        FindBc7ComponentBounds(storage, out endpoints[0], out endpoints[1]);
        TryBc7Endpoints(storage, 6, 0, 0, 0, endpoints, destination, ref bestError);
        if (opaqueError != 0)
        {
            FindBc7Endpoints(storage, false, out endpoints[0], out endpoints[1]);
            TryBc7Endpoints(storage, 6, 0, 0, 0, endpoints, destination, ref bestError);
            FindBc7AlphaBounds(storage, out endpoints[0], out endpoints[1]);
            TryBc7Endpoints(storage, 6, 0, 0, 0, endpoints, destination, ref bestError);
        }
        if (compressionMode == TextureCompressionLevel.Fast || bestError == 0) return;

        var rotated = new InlineArray16<Bc7Color32>();
        for (int rotation = 0; rotation < 4 && bestError > 0; rotation++)
        {
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                rotated[i] = RotateBc7Color(storage[i], rotation);
            }
            FindBc7Endpoints(rotated, false, out var rgb0, out var rgb1);
            FindBc7AlphaBounds(rotated, out var alpha0, out var alpha1);
            endpoints[0] = rgb0 with { Alpha = alpha0.Alpha };
            endpoints[1] = rgb1 with { Alpha = alpha1.Alpha };
            TryBc7Endpoints(rotated, 4, 0, rotation, 0, endpoints, destination, ref bestError);
            TryBc7Endpoints(rotated, 4, 0, rotation, 1, endpoints, destination, ref bestError);
            TryBc7Endpoints(rotated, 5, 0, rotation, 0, endpoints, destination, ref bestError);
        }

        RefineBc7Block(storage, destination, ref bestError);
        if (compressionMode == TextureCompressionLevel.Normal || bestError == 0) return;

        int passes = compressionMode == TextureCompressionLevel.Exhaustive ? 2 : 1;
        for (int pass = 0; pass < passes; pass++)
        {
            int previous = bestError;
            foreach (int mode in (ReadOnlySpan<int>)[1, 3, 7, 0, 2])
            {
                var info = SBc7Modes[mode];
                if (info.AlphaBits == 0 && opaqueError >= bestError) continue;
                if (pass == 0 && info.SubsetCount == 3) continue;
                int start = pass == 0 || info.SubsetCount == 3 ? 0 : 16;
                int end = pass == 0 ? 16 : 1 << info.PartitionBits;
                for (int partition = start; partition < end && bestError > 0; partition++)
                {
                    for (int subset = 0; subset < info.SubsetCount; subset++)
                    {
                        FindBc7SubsetEndpoints(storage, info, partition, subset,
                            out endpoints[2 * subset], out endpoints[2 * subset + 1]);
                    }
                    TryBc7Endpoints(storage, mode, partition, 0, 0, endpoints, destination, ref bestError);
                }
            }
            if (bestError < previous) RefineBc7Block(storage, destination, ref bestError);
        }
    }

    private static Bc7Color32 RotateBc7Color(Bc7Color32 c, int rotation) => rotation switch
    {
        1 => new(c.Alpha, c.Green, c.Blue, c.Red),
        2 => new(c.Red, c.Alpha, c.Blue, c.Green),
        3 => new(c.Red, c.Green, c.Alpha, c.Blue),
        _ => c
    };

    private static void RefineBc7Block(ReadOnlySpan<Bc7Color32> source, Span<byte> best, ref int bestError)
    {
        int mode = BitOperations.TrailingZeroCount((uint)best[0]);
        var info = SBc7Modes[mode];
        var block = new InlineArray16<Bc7Color32>();
        var seeds = new InlineArray6<Bc7Color32>();
        Span<BptcEndpointFit> fits = stackalloc BptcEndpointFit[3];
        for (int pass = 0; pass < 2 && bestError > 0; pass++)
        {
            var bits = new BptcBitReader(best);
            bits.Read(mode + 1);
            int partition = bits.Read(info.PartitionBits);
            int rotation = bits.Read(info.RotationBits);
            int selection = bits.Read(info.IndexSelectionBits);
            bool dual = info.IndexBits1 != 0;
            int secondaryLength = dual ? 16 * info.IndexBits1 - 1 : 0;
            int primaryOffset = 128 - secondaryLength - (16 * info.IndexBits0 - info.SubsetCount);
            int secondaryOffset = 128 - secondaryLength;
            var selectors = new BptcBitReader(best);
            fits.Clear();
            var scalarFit = new BptcEndpointFit();
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                int subset = GetBc7Subset(info, partition, i);
                int fixup = i == GetBc7Anchor(info, partition, subset) ? 1 : 0;
                int primary = selectors.Peek(primaryOffset, info.IndexBits0 - fixup);
                primaryOffset += info.IndexBits0 - fixup;
                int w = SBptcFactors[info.IndexBits0 - 2][primary];
                var c = block[i] = RotateBc7Color(source[i], rotation);
                var v = new Vector4(c.Red, c.Green, c.Blue, c.Alpha);
                if (dual)
                {
                    int secondary = selectors.Peek(secondaryOffset, info.IndexBits1 - fixup);
                    secondaryOffset += info.IndexBits1 - fixup;
                    int w2 = SBptcFactors[info.IndexBits1 - 2][secondary];
                    if (selection == 1) (w, w2) = (w2, w);
                    scalarFit.Add(v, w2);
                }
                fits[subset].Add(v, w);
            }
            for (int subset = 0; subset < info.SubsetCount; subset++)
            {
                fits[subset].Solve(out var a, out var b);
                if (dual)
                {
                    scalarFit.Solve(out var scalar0, out var scalar1);
                    a.W = scalar0.W;
                    b.W = scalar1.W;
                }
                seeds[2 * subset] = ToBc7Color(a);
                seeds[2 * subset + 1] = ToBc7Color(b);
            }
            int previous = bestError;
            TryBc7Endpoints(block, mode, partition, rotation, selection, seeds, best, ref bestError);
            if (previous == bestError) break;
        }
    }

    private static Bc7Color32 ToBc7Color(Vector4 v)
    {
        v = Vector4.Clamp(Vector4.Round(v), Vector4.Zero, new Vector4(255));
        return new((byte)v.X, (byte)v.Y, (byte)v.Z, (byte)v.W);
    }

    // Solve the two-endpoint least-squares fit for a fixed set of interpolation weights.
    private struct BptcEndpointFit
    {
        private float _aa, _ab, _bb;
        private Vector4 _av, _bv;

        public void Add(Vector4 color, int weight)
        {
            float b = weight / 64f, a = 1 - b;
            _aa += a * a;
            _ab += a * b;
            _bb += b * b;
            _av += a * color;
            _bv += b * color;
        }

        public readonly void Solve(out Vector4 a, out Vector4 b)
        {
            float determinant = _aa * _bb - _ab * _ab;
            if (determinant <= 0)
            {
                a = b = (_av + _bv) / (_aa + 2 * _ab + _bb);
                return;
            }
            a = (_bb * _av - _ab * _bv) / determinant;
            b = (_aa * _bv - _ab * _av) / determinant;
        }
    }

    private static void TryBc7Endpoints(ReadOnlySpan<Bc7Color32> source, int mode, int partition, int rotation,
        int selection, ReadOnlySpan<Bc7Color32> seeds, Span<byte> destination, ref int bestError)
    {
        var info = SBc7Modes[mode];
        bool dual = info.IndexBits1 != 0;
        int rgbBits = selection == 0 ? info.IndexBits0 : info.IndexBits1;
        int alphaBits = selection == 0 ? info.IndexBits1 : info.IndexBits0;
        int rgbCount = 1 << rgbBits, alphaCount = 1 << alphaBits;
        var endpoints = new InlineArray6<Bc7Endpoint>();
        var rgbIndices = new InlineArray16<int>();
        var alphaIndices = new InlineArray16<int>();
        var rgbPalette = new InlineArray16<Bc7Color32>();
        var alphaPalette = new InlineArray16<Bc7Color32>();
        int error = 0;

        for (int subset = 0; subset < info.SubsetCount; subset++)
        {
            int offset = 2 * subset;
            QuantizeBc7EndpointPair(seeds[offset], seeds[offset + 1], info, out var a, out var b);
            BuildBc7Palette(a.Color, b.Color, rgbBits, rgbPalette);
            if (dual) BuildBc7Palette(a.Color, b.Color, alphaBits, alphaPalette);
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (GetBc7Subset(info, partition, i) != subset) continue;
                var c = source[i];
                int nearest = 0, distance = int.MaxValue;
                for (int k = 0; k < rgbCount; k++)
                {
                    int d = dual ? ColorDistanceRgb(c, rgbPalette[k]) : ColorDistance(c, rgbPalette[k]);
                    if (d < distance)
                    {
                        nearest = k;
                        distance = d;
                    }
                }
                rgbIndices[i] = nearest;
                error += distance;
                if (dual)
                {
                    distance = int.MaxValue;
                    for (int k = 0; k < alphaCount; k++)
                    {
                        int d = Squared(c.Alpha - alphaPalette[k].Alpha);
                        if (d < distance)
                        {
                            nearest = k;
                            distance = d;
                        }
                    }
                    alphaIndices[i] = nearest;
                    error += distance;
                }
            }
            if (error >= bestError) return;

            // Each anchor omits the top selector bit. Reverse only the endpoint pair
            // of the affected plane and complement its selectors to preserve all texels.
            int anchor = GetBc7Anchor(info, partition, subset);
            bool flipRgb = rgbIndices[anchor] >= rgbCount / 2;
            bool flipAlpha = dual && alphaIndices[anchor] >= alphaCount / 2;
            endpoints[offset] = flipRgb ? b : a;
            endpoints[offset + 1] = flipRgb ? a : b;
            if (dual)
            {
                endpoints[offset] = endpoints[offset] with { Alpha = flipAlpha ? b.Alpha : a.Alpha };
                endpoints[offset + 1] = endpoints[offset + 1] with { Alpha = flipAlpha ? a.Alpha : b.Alpha };
            }
            if (!flipRgb && !flipAlpha) continue;
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (GetBc7Subset(info, partition, i) != subset) continue;
                if (flipRgb) rgbIndices[i] = rgbCount - 1 - rgbIndices[i];
                if (flipAlpha) alphaIndices[i] = alphaCount - 1 - alphaIndices[i];
            }
        }
        bestError = error;
        if (selection == 0)
            WriteBc7ModeBlock(mode, partition, rotation, selection, endpoints, rgbIndices, alphaIndices, destination);
        else
            WriteBc7ModeBlock(mode, partition, rotation, selection, endpoints, alphaIndices, rgbIndices, destination);
    }

    private static void WriteBc7ModeBlock(int mode, int partitionSet, int rotation, int selection,
        ReadOnlySpan<Bc7Endpoint> endpoints, ReadOnlySpan<int> primaryIndices, ReadOnlySpan<int> secondaryIndices,
        Span<byte> destination)
    {
        var modeInfo = SBc7Modes[mode];
        var hasSecondaryIndices = modeInfo.IndexBits1 != 0;
        var writer = new BptcBitWriter(destination);
        writer.Write(0, mode);
        writer.Write(1, 1);
        writer.Write(partitionSet, modeInfo.PartitionBits);
        writer.Write(rotation, modeInfo.RotationBits);
        writer.Write(selection, modeInfo.IndexSelectionBits);

        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            writer.Write(endpoints[subset * 2].Red, modeInfo.ColorBits);
            writer.Write(endpoints[(subset * 2) + 1].Red, modeInfo.ColorBits);
        }

        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            writer.Write(endpoints[subset * 2].Green, modeInfo.ColorBits);
            writer.Write(endpoints[(subset * 2) + 1].Green, modeInfo.ColorBits);
        }

        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            writer.Write(endpoints[subset * 2].Blue, modeInfo.ColorBits);
            writer.Write(endpoints[(subset * 2) + 1].Blue, modeInfo.ColorBits);
        }

        if (modeInfo.AlphaBits != 0)
        {
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                writer.Write(endpoints[subset * 2].Alpha, modeInfo.AlphaBits);
                writer.Write(endpoints[(subset * 2) + 1].Alpha, modeInfo.AlphaBits);
            }
        }

        if (modeInfo.EndpointPBits != 0)
        {
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                writer.Write(endpoints[subset * 2].PBit, modeInfo.EndpointPBits);
                writer.Write(endpoints[(subset * 2) + 1].PBit, modeInfo.EndpointPBits);
            }
        }
        else if (modeInfo.SharedPBits != 0)
        {
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                writer.Write(endpoints[subset * 2].PBit, modeInfo.SharedPBits);
            }
        }

        for (var texel = 0; texel < TexelsPerBlock; texel++)
        {
            var subset = GetBc7Subset(modeInfo, partitionSet, texel);
            var anchor = GetBc7Anchor(modeInfo, partitionSet, subset);
            writer.Write(primaryIndices[texel], modeInfo.IndexBits0 - (texel == anchor ? 1 : 0));
        }

        if (!hasSecondaryIndices)
        {
            return;
        }

        for (var texel = 0; texel < TexelsPerBlock; texel++)
        {
            var subset = GetBc7Subset(modeInfo, partitionSet, texel);
            var anchor = GetBc7Anchor(modeInfo, partitionSet, subset);
            writer.Write(secondaryIndices[texel], modeInfo.IndexBits1 - (texel == anchor ? 1 : 0));
        }
    }

    // The same bit-field layout packs and unpacks the scattered endpoint bits in each wire mode.
    private static void TransferBc6HEndpoints<TBits>(
        int mode,
        scoped ref TBits bit,
        scoped Span<int> epR,
        scoped Span<int> epG,
        scoped Span<int> epB) where TBits : IEndpointFields, allows ref struct
    {
        switch (mode)
        {
            case 0:
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 5);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 5);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 5);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 5);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 1:
                bit.Field(ref epG[2], 1, 5);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epG[3], 1, 5);
                bit.Field(ref epR[0], 7);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[0], 7);
                bit.Field(ref epB[2], 1, 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[0], 7);
                bit.Field(ref epB[3], 1, 3);
                bit.Field(ref epB[3], 1, 5);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[1], 6);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 6);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 6);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 6);
                bit.Field(ref epR[3], 6);
                break;
            case 2:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 5);
                bit.Field(ref epR[0], 1, 10);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 4);
                bit.Field(ref epG[0], 1, 10);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 4);
                bit.Field(ref epB[0], 1, 10);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 5);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 3:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 10);
                bit.Field(ref epG[1], 10);
                bit.Field(ref epB[1], 10);
                break;
            case 6:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 4);
                bit.Field(ref epR[0], 1, 10);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 5);
                bit.Field(ref epG[0], 1, 10);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 4);
                bit.Field(ref epB[0], 1, 10);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 4);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 4);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 7:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 9);
                bit.Field(ref epR[0], 1, 10);
                bit.Field(ref epG[1], 9);
                bit.Field(ref epG[0], 1, 10);
                bit.Field(ref epB[1], 9);
                bit.Field(ref epB[0], 1, 10);
                break;
            case 10:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 4);
                bit.Field(ref epR[0], 1, 10);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 4);
                bit.Field(ref epG[0], 1, 10);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 5);
                bit.Field(ref epB[0], 1, 10);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 4);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 4);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 11:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 8);
                bit.Field(ref epR[0], 1, 11);
                bit.Field(ref epR[0], 1, 10);
                bit.Field(ref epG[1], 8);
                bit.Field(ref epG[0], 1, 11);
                bit.Field(ref epG[0], 1, 10);
                bit.Field(ref epB[1], 8);
                bit.Field(ref epB[0], 1, 11);
                bit.Field(ref epB[0], 1, 10);
                break;
            case 14:
                bit.Field(ref epR[0], 9);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[0], 9);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[0], 9);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[1], 5);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 5);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 5);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 5);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 15:
                bit.Field(ref epR[0], 10);
                bit.Field(ref epG[0], 10);
                bit.Field(ref epB[0], 10);
                bit.Field(ref epR[1], 4);
                bit.Field(ref epR[0], 1, 15);
                bit.Field(ref epR[0], 1, 14);
                bit.Field(ref epR[0], 1, 13);
                bit.Field(ref epR[0], 1, 12);
                bit.Field(ref epR[0], 1, 11);
                bit.Field(ref epR[0], 1, 10);
                bit.Field(ref epG[1], 4);
                bit.Field(ref epG[0], 1, 15);
                bit.Field(ref epG[0], 1, 14);
                bit.Field(ref epG[0], 1, 13);
                bit.Field(ref epG[0], 1, 12);
                bit.Field(ref epG[0], 1, 11);
                bit.Field(ref epG[0], 1, 10);
                bit.Field(ref epB[1], 4);
                bit.Field(ref epB[0], 1, 15);
                bit.Field(ref epB[0], 1, 14);
                bit.Field(ref epB[0], 1, 13);
                bit.Field(ref epB[0], 1, 12);
                bit.Field(ref epB[0], 1, 11);
                bit.Field(ref epB[0], 1, 10);
                break;
            case 18:
                bit.Field(ref epR[0], 8);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[0], 8);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[0], 8);
                bit.Field(ref epB[3], 1, 3);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[1], 6);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 5);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 5);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 6);
                bit.Field(ref epR[3], 6);
                break;
            case 22:
                bit.Field(ref epR[0], 8);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[0], 8);
                bit.Field(ref epG[2], 1, 5);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[0], 8);
                bit.Field(ref epG[3], 1, 5);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[1], 5);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 6);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 5);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 5);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 26:
                bit.Field(ref epR[0], 8);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[0], 8);
                bit.Field(ref epB[2], 1, 5);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[0], 8);
                bit.Field(ref epB[3], 1, 5);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[1], 5);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 5);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 6);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epR[3], 5);
                bit.Field(ref epB[3], 1, 3);
                break;
            case 30:
                bit.Field(ref epR[0], 6);
                bit.Field(ref epG[3], 1, 4);
                bit.Field(ref epB[3], 1);
                bit.Field(ref epB[3], 1, 1);
                bit.Field(ref epB[2], 1, 4);
                bit.Field(ref epG[0], 6);
                bit.Field(ref epG[2], 1, 5);
                bit.Field(ref epB[2], 1, 5);
                bit.Field(ref epB[3], 1, 2);
                bit.Field(ref epG[2], 1, 4);
                bit.Field(ref epB[0], 6);
                bit.Field(ref epG[3], 1, 5);
                bit.Field(ref epB[3], 1, 3);
                bit.Field(ref epB[3], 1, 5);
                bit.Field(ref epB[3], 1, 4);
                bit.Field(ref epR[1], 6);
                bit.Field(ref epG[2], 4);
                bit.Field(ref epG[1], 6);
                bit.Field(ref epG[3], 4);
                bit.Field(ref epB[1], 6);
                bit.Field(ref epB[2], 4);
                bit.Field(ref epR[2], 6);
                bit.Field(ref epR[3], 6);
                break;
        }
    }

    private static int UnquantizeBc6HEndpoint(int value, bool signed, int bits)
    {
        if (signed)
        {
            if (bits >= 16 || value == 0)
            {
                return value;
            }

            var sign = value < 0;
            var magnitude = Math.Abs(value);
            var maxMagnitude = (1 << (bits - 1)) - 1;
            int unquantized;
            if (magnitude >= maxMagnitude)
            {
                unquantized = 0x7fff;
            }
            else
            {
                unquantized = ((magnitude << 15) + 0x4000) >> (bits - 1);
            }

            return sign ? -unquantized : unquantized;
        }

        if (bits >= 15 || value == 0)
        {
            return value;
        }

        var maxValue = (1 << bits) - 1;
        if (value == maxValue)
        {
            return 0xffff;
        }

        return ((value << 16) + 0x8000) >> bits;
    }

    private static ushort InterpolateBc6HHalf(int endpoint0, int endpoint1, int weight, bool signed)
    {
        var value = ((endpoint0 * (64 - weight)) + (endpoint1 * weight) + 32) >> 6;
        return FinishBc6HUnquantize(value, signed);
    }

    private static ushort FinishBc6HUnquantize(int value, bool signed)
    {
        if (!signed)
        {
            return (ushort)(((uint)Math.Max(0, value) * 31u) >> 6);
        }

        var sign = value < 0;
        var magnitude = Math.Abs(value);
        var halfMagnitude = (magnitude * 31) >> 5;
        return (ushort)(halfMagnitude | (sign && halfMagnitude != 0 ? 0x8000 : 0));
    }

    private static float HalfBitsToSingle(ushort bits) => (float)BitConverter.UInt16BitsToHalf(bits);

    private static int SignExtend(int value, int bits)
    {
        var shift = 32 - bits;
        return (value << shift) >> shift;
    }

    private static void FindBc6HComponentBounds(ReadOnlySpan<Rgba32Float> source, out Rgba32Float minimum, out Rgba32Float maximum)
    {
        minimum = source[0];
        maximum = minimum;

        for (var i = 1; i < TexelsPerBlock; i++)
        {
            var color = source[i];
            minimum = new Rgba32Float(
                MathF.Min(minimum.Red, color.Red),
                MathF.Min(minimum.Green, color.Green),
                MathF.Min(minimum.Blue, color.Blue),
                1f);
            maximum = new Rgba32Float(
                MathF.Max(maximum.Red, color.Red),
                MathF.Max(maximum.Green, color.Green),
                MathF.Max(maximum.Blue, color.Blue),
                1f);
        }
    }

    private static Rgba32Float ClampBc6HColor(Rgba32Float color, bool signed) => new(
        ClampBc6HValue(color.Red, signed),
        ClampBc6HValue(color.Green, signed),
        ClampBc6HValue(color.Blue, signed),
        1f);

    private static float ClampBc6HValue(float value, bool signed)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        return signed
            ? Math.Clamp(value, -65504f, 65504f)
            : Math.Clamp(value, 0f, 65504f);
    }

    private static int QuantizeBc6HEndpoint(float value, bool signed, int bits)
    {
        int half = BitConverter.HalfToUInt16Bits((Half)value);
        int scale = signed ? 32 : 64;
        int magnitude = half & 0x7fff;
        // Invert the decoder's final truncation, including the full 16-bit mode.
        int target = (magnitude * scale + 30) / 31;
        int max = (1 << (signed ? bits - 1 : bits)) - 1;
        int q = Math.Min(target >> (16 - bits), max);
        if (bits < 16)
        {
            int reconstructed = FinishBc6HUnquantize(UnquantizeBc6HEndpoint(q, signed, bits), signed);
            int neighbor = Math.Clamp(q + Math.Sign(magnitude - reconstructed), 0, max);
            int other = FinishBc6HUnquantize(UnquantizeBc6HEndpoint(neighbor, signed, bits), signed);
            if (Math.Abs(magnitude - other) < Math.Abs(magnitude - reconstructed)) q = neighbor;
        }
        return (half & 0x8000) == 0 ? q : (-q & ((1 << bits) - 1));
    }

    private static void ApplyBc7PBit(Span<int> endpoints, int subset, int p0, int p1)
    {
        endpoints[subset * 2] |= p0;
        endpoints[(subset * 2) + 1] |= p1;
    }

    private static int ExpandQuantizedBc7(int value, int bits)
    {
        if (bits >= 8)
        {
            return value & 0xff;
        }

        value <<= 8 - bits;
        return (value | (value >> bits)) & 0xff;
    }

    private static int GetBc7Subset(Bc7ModeInfo modeInfo, int partitionSet, int texel)
    {
        return modeInfo.SubsetCount switch
        {
            2 => (SBptcP2[partitionSet] >> texel) & 1,
            3 => (int)((SBptcP3[partitionSet] >> (texel * 2)) & 3),
            _ => 0
        };
    }

    private static int GetBc7Anchor(Bc7ModeInfo modeInfo, int partitionSet, int subset)
    {
        return modeInfo.SubsetCount switch
        {
            2 => subset == 0 ? 0 : SBptcA2[partitionSet],
            3 => subset == 0 ? 0 : SBptcA3[subset - 1, partitionSet],
            _ => 0
        };
    }

    private static int InterpolateByte(int endpoint0, int endpoint1, int weight) =>
        ((endpoint0 * (64 - weight)) + (endpoint1 * weight) + 32) >> 6;

    private static void FindBc7SubsetEndpoints(
        ReadOnlySpan<Bc7Color32> source,
        Bc7ModeInfo modeInfo,
        int partitionSet,
        int subset,
        out Bc7Color32 endpoint0,
        out Bc7Color32 endpoint1)
    {
        endpoint0 = default;
        endpoint1 = default;
        var found = false;
        var bestDistance = -1;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (GetBc7Subset(modeInfo, partitionSet, i) != subset)
            {
                continue;
            }

            if (!found)
            {
                endpoint0 = source[i];
                endpoint1 = source[i];
                found = true;
            }

            for (var j = i + 1; j < TexelsPerBlock; j++)
            {
                if (GetBc7Subset(modeInfo, partitionSet, j) != subset)
                {
                    continue;
                }

                var distance = modeInfo.AlphaBits != 0
                    ? ColorDistance(source[i], source[j])
                    : ColorDistanceRgb(source[i], source[j]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    endpoint0 = source[i];
                    endpoint1 = source[j];
                }
            }
        }
    }

    private static void QuantizeBc7EndpointPair(
        Bc7Color32 endpoint0,
        Bc7Color32 endpoint1,
        Bc7ModeInfo modeInfo,
        out Bc7Endpoint quantized0,
        out Bc7Endpoint quantized1)
    {
        var modePBits = modeInfo.EndpointPBits != 0 ? modeInfo.EndpointPBits : modeInfo.SharedPBits;
        // A constant fully transparent or opaque subset can represent alpha exactly.
        // Modes 6 and 7 share each endpoint's low bit across all four channels.
        if (modeInfo.EndpointPBits != 0 && modeInfo.AlphaBits != 0 &&
            endpoint0.Alpha == endpoint1.Alpha && endpoint0.Alpha is 0 or 255)
        {
            int p = endpoint0.Alpha & 1;
            quantized0 = QuantizeBc7Endpoint(endpoint0, modeInfo, modePBits, p, out _);
            quantized1 = QuantizeBc7Endpoint(endpoint1, modeInfo, modePBits, p, out _);
            return;
        }

        if (modeInfo.SharedPBits != 0)
        {
            var bestError = int.MaxValue;
            quantized0 = default;
            quantized1 = default;
            for (var p = 0; p < (1 << modeInfo.SharedPBits); p++)
            {
                var candidate0 = QuantizeBc7Endpoint(endpoint0, modeInfo, modePBits, p, out var error0);
                var candidate1 = QuantizeBc7Endpoint(endpoint1, modeInfo, modePBits, p, out var error1);
                var error = error0 + error1;
                if (error < bestError)
                {
                    bestError = error;
                    quantized0 = candidate0;
                    quantized1 = candidate1;
                }
            }

            return;
        }

        quantized0 = QuantizeBc7EndpointWithBestPBit(endpoint0, modeInfo, modePBits);
        quantized1 = QuantizeBc7EndpointWithBestPBit(endpoint1, modeInfo, modePBits);
    }

    private static Bc7Endpoint QuantizeBc7EndpointWithBestPBit(Bc7Color32 endpoint, Bc7ModeInfo modeInfo, int modePBits)
    {
        var bestError = int.MaxValue;
        var best = default(Bc7Endpoint);
        var pCount = modeInfo.EndpointPBits == 0 ? 1 : 1 << modeInfo.EndpointPBits;
        for (var p = 0; p < pCount; p++)
        {
            var candidate = QuantizeBc7Endpoint(endpoint, modeInfo, modePBits, p, out var error);
            if (error < bestError)
            {
                bestError = error;
                best = candidate;
            }
        }

        return best;
    }

    private static Bc7Endpoint QuantizeBc7Endpoint(
        Bc7Color32 endpoint,
        Bc7ModeInfo modeInfo,
        int modePBits,
        int pBit,
        out int error)
    {
        var red = QuantizeBc7Scalar(endpoint.Red, modeInfo.ColorBits, modePBits, pBit, out var redValue);
        var green = QuantizeBc7Scalar(endpoint.Green, modeInfo.ColorBits, modePBits, pBit, out var greenValue);
        var blue = QuantizeBc7Scalar(endpoint.Blue, modeInfo.ColorBits, modePBits, pBit, out var blueValue);
        error =
            Squared(endpoint.Red - redValue) +
            Squared(endpoint.Green - greenValue) +
            Squared(endpoint.Blue - blueValue);

        var alpha = 0;
        var alphaValue = 255;
        if (modeInfo.AlphaBits != 0)
        {
            alpha = QuantizeBc7Scalar(endpoint.Alpha, modeInfo.AlphaBits, modePBits, pBit, out alphaValue);
            error += Squared(endpoint.Alpha - alphaValue);
        }

        return new Bc7Endpoint(
            red,
            green,
            blue,
            alpha,
            pBit,
            new Bc7Color32((byte)redValue, (byte)greenValue, (byte)blueValue, (byte)alphaValue));
    }

    private static int QuantizeBc7Scalar(byte value, int valueBits, int pBits, int pBit, out int reconstructed)
    {
        int precision = valueBits + pBits;
        int q = value >> (8 - valueBits);
        reconstructed = ExpandQuantizedBc7((q << pBits) | pBit, precision);
        int neighbor = Math.Clamp(q + Math.Sign(value - reconstructed), 0, (1 << valueBits) - 1);
        int other = ExpandQuantizedBc7((neighbor << pBits) | pBit, precision);
        if (Math.Abs(value - other) < Math.Abs(value - reconstructed))
        {
            reconstructed = other;
            return neighbor;
        }
        return q;
    }

    private static void FindBc7Endpoints(ReadOnlySpan<Bc7Color32> source, bool includeAlpha, out Bc7Color32 endpoint0, out Bc7Color32 endpoint1)
    {
        endpoint0 = source[0];
        endpoint1 = source[0];
        var bestDistance = -1;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            for (var j = i + 1; j < TexelsPerBlock; j++)
            {
                var distance = includeAlpha
                    ? ColorDistance(source[i], source[j])
                    : ColorDistanceRgb(source[i], source[j]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    endpoint0 = source[i];
                    endpoint1 = source[j];
                }
            }
        }
    }

    private static void FindBc7ComponentBounds(ReadOnlySpan<Bc7Color32> source, out Bc7Color32 minimum, out Bc7Color32 maximum)
    {
        var minRed = byte.MaxValue;
        var minGreen = byte.MaxValue;
        var minBlue = byte.MaxValue;
        var minAlpha = byte.MaxValue;
        var maxRed = byte.MinValue;
        var maxGreen = byte.MinValue;
        var maxBlue = byte.MinValue;
        var maxAlpha = byte.MinValue;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            minRed = Math.Min(minRed, source[i].Red);
            minGreen = Math.Min(minGreen, source[i].Green);
            minBlue = Math.Min(minBlue, source[i].Blue);
            minAlpha = Math.Min(minAlpha, source[i].Alpha);
            maxRed = Math.Max(maxRed, source[i].Red);
            maxGreen = Math.Max(maxGreen, source[i].Green);
            maxBlue = Math.Max(maxBlue, source[i].Blue);
            maxAlpha = Math.Max(maxAlpha, source[i].Alpha);
        }

        minimum = new Bc7Color32(minRed, minGreen, minBlue, minAlpha);
        maximum = new Bc7Color32(maxRed, maxGreen, maxBlue, maxAlpha);
    }

    private static void FindBc7AlphaBounds(ReadOnlySpan<Bc7Color32> source, out Bc7Color32 minimum, out Bc7Color32 maximum)
    {
        minimum = source[0];
        maximum = source[0];
        for (var i = 1; i < TexelsPerBlock; i++)
        {
            if (source[i].Alpha < minimum.Alpha)
            {
                minimum = source[i];
            }

            if (source[i].Alpha > maximum.Alpha)
            {
                maximum = source[i];
            }
        }
    }

    private static void BuildBc7Palette(Bc7Color32 endpoint0, Bc7Color32 endpoint1, int indexBits, Span<Bc7Color32> palette)
    {
        var weights = SBptcFactors[indexBits - 2];
        for (var i = 0; i < (1 << indexBits); i++)
        {
            var weight = weights[i];
            palette[i] = new Bc7Color32(
                (byte)InterpolateByte(endpoint0.Red, endpoint1.Red, weight),
                (byte)InterpolateByte(endpoint0.Green, endpoint1.Green, weight),
                (byte)InterpolateByte(endpoint0.Blue, endpoint1.Blue, weight),
                (byte)InterpolateByte(endpoint0.Alpha, endpoint1.Alpha, weight));
        }
    }

    private static int FindNearestColorIndex(Rgba32Float color, ReadOnlySpan<Rgba32Float> palette, int paletteCount)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < paletteCount; i++)
        {
            var distance = ColorDistance(color, palette[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static float ColorDistance(Rgba32Float a, Rgba32Float b)
    {
        var red = a.Red - b.Red;
        var green = a.Green - b.Green;
        var blue = a.Blue - b.Blue;
        return (red * red) + (green * green) + (blue * blue);
    }

    private static int ColorDistance(Bc7Color32 a, Bc7Color32 b) =>
        Squared(a.Red - b.Red) + Squared(a.Green - b.Green) + Squared(a.Blue - b.Blue) + Squared(a.Alpha - b.Alpha);

    private static int ColorDistanceRgb(Bc7Color32 a, Bc7Color32 b) =>
        Squared(a.Red - b.Red) + Squared(a.Green - b.Green) + Squared(a.Blue - b.Blue);

    private static int Squared(int value) => value * value;

    private static void LoadFloatBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba32Float> destination)
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
                destination[blockOffset++] = TPixel.ToRgba32Float(sourceRow[Math.Min(sourceX, lastSourceX)]);
                sourceX++;
            }
        }
    }

    private static void StoreFloatBlock<TPixel>(
        ReadOnlySpan<Rgba32Float> block,
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

                destinationRow[destinationX] = TPixel.FromRgba32Float(block[rowBlockOffset++]);
                destinationX++;
            }

            blockOffset += BlockSize;
        }
    }

    private static void LoadUNormBlock<TPixel, TTransfer>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBc7Transfer
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
                destination[blockOffset++] = TTransfer.ToStorage(sourceRow[Math.Min(sourceX, lastSourceX)]);
                sourceX++;
            }
        }
    }

    private static void StoreUNormBlock<TPixel, TTransfer>(
        ReadOnlySpan<Rgba8UNorm> block,
        int blockX,
        int blockY,
        BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IBc7Transfer
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

                destinationRow[destinationX] = TTransfer.FromStorage<TPixel>(block[rowBlockOffset++]);
                destinationX++;
            }

            blockOffset += BlockSize;
        }
    }

    private static void FillFloatBlock(Rgba32Float color, Span<Rgba32Float> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = color;
        }
    }

    private static void FillUNormBlock(Rgba8UNorm color, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = color;
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded BPTC texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded BPTC texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size) => (size + BlockSize - 1) / BlockSize;

    private static bool TryGetTransfer(TextureFormat format, out BptcTransfer transfer)
    {
        if (format == TextureFormats.Bc6HUFloat || format == TextureFormats.RgbBptcUFloat)
        {
            transfer = BptcTransfer.Bc6HUFloat;
            return true;
        }

        if (format == TextureFormats.Bc6HSFloat || format == TextureFormats.RgbBptcSFloat)
        {
            transfer = BptcTransfer.Bc6HSFloat;
            return true;
        }

        if (format == TextureFormats.Bc7UNorm || format == TextureFormats.RgbaBptcUNorm)
        {
            transfer = BptcTransfer.Bc7UNorm;
            return true;
        }

        if (format == TextureFormats.Bc7Srgb || format == TextureFormats.RgbaBptcSrgb)
        {
            transfer = BptcTransfer.Bc7Srgb;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"BPTC texture coder does not support texture format '{format.Name}'.");

    private interface IEndpointFields
    {
        void Field(scoped ref int value, int count, int shift = 0);
    }

    private struct BptcBitReader(ReadOnlySpan<byte> data) : IEndpointFields
    {
        private readonly UInt128 _bits = BinaryPrimitives.ReadUInt128LittleEndian(data);
        private int _position;

        public void Field(scoped ref int value, int count, int shift = 0) => value |= Read(count) << shift;

        public int Read(int count)
        {
            int value = Peek(0, count);
            _position += count;
            return value;
        }

        public readonly int Peek(int offset, int count) => (int)(_bits >> (_position + offset)) & ((1 << count) - 1);
    }

    private ref struct BptcBitWriter(Span<byte> data) : IEndpointFields
    {
        private readonly Span<byte> _data = data;
        private UInt128 _bits;
        private int _position;

        public void Field(scoped ref int value, int count, int shift = 0) => Write(value >> shift, count);

        public void Write(int value, int count)
        {
            _bits |= (UInt128)(uint)(value & ((1 << count) - 1)) << _position;
            _position += count;
            if (_position == 128) BinaryPrimitives.WriteUInt128LittleEndian(_data, _bits);
        }
    }

    private enum BptcTransfer
    {
        Bc6HUFloat,
        Bc6HSFloat,
        Bc7UNorm,
        Bc7Srgb
    }

    private readonly record struct Bc6HModeInfo(bool Transformed, int PartitionBits, int EndpointBits, int DeltaBitsR, int DeltaBitsG, int DeltaBitsB);

    private readonly record struct Bc7ModeInfo(
        int SubsetCount,
        int PartitionBits,
        int RotationBits,
        int IndexSelectionBits,
        int ColorBits,
        int AlphaBits,
        int EndpointPBits,
        int SharedPBits,
        int IndexBits0,
        int IndexBits1);

    private readonly record struct Bc7Color32(byte Red, byte Green, byte Blue, byte Alpha);

    private readonly record struct Bc7Endpoint(int Red, int Green, int Blue, int Alpha, int PBit, Bc7Color32 Color);

    private static readonly byte[][] SBptcFactors =
    [
        [0, 21, 43, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 9, 18, 27, 37, 46, 55, 64, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64],
    ];

    private static readonly ushort[] SBptcP2 =
    [
        0xcccc, 0x8888, 0xeeee, 0xecc8, 0xc880, 0xfeec, 0xfec8, 0xec80,
        0xc800, 0xffec, 0xfe80, 0xe800, 0xffe8, 0xff00, 0xfff0, 0xf000,
        0xf710, 0x008e, 0x7100, 0x08ce, 0x008c, 0x7310, 0x3100, 0x8cce,
        0x088c, 0x3110, 0x6666, 0x366c, 0x17e8, 0x0ff0, 0x718e, 0x399c,
        0xaaaa, 0xf0f0, 0x5a5a, 0x33cc, 0x3c3c, 0x55aa, 0x9696, 0xa55a,
        0x73ce, 0x13c8, 0x324c, 0x3bdc, 0x6996, 0xc33c, 0x9966, 0x0660,
        0x0272, 0x04e4, 0x4e40, 0x2720, 0xc936, 0x936c, 0x39c6, 0x639c,
        0x9336, 0x9cc6, 0x817e, 0xe718, 0xccf0, 0x0fcc, 0x7744, 0xee22,
    ];

    private static readonly byte[] SBptcA2 =
    [
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 2, 8, 2, 2, 8, 8, 15,
        2, 8, 2, 2, 8, 8, 2, 2,
        15, 15, 6, 8, 2, 8, 15, 15,
        2, 8, 2, 2, 2, 15, 15, 6,
        6, 2, 6, 8, 15, 15, 2, 2,
        15, 15, 15, 15, 15, 2, 2, 15,
    ];

    private static readonly uint[] SBptcP3 =
    [
        0xaa685050u, 0x6a5a5040u, 0x5a5a4200u, 0x5450a0a8u,
        0xa5a50000u, 0xa0a05050u, 0x5555a0a0u, 0x5a5a5050u,
        0xaa550000u, 0xaa555500u, 0xaaaa5500u, 0x90909090u,
        0x94949494u, 0xa4a4a4a4u, 0xa9a59450u, 0x2a0a4250u,
        0xa5945040u, 0x0a425054u, 0xa5a5a500u, 0x55a0a0a0u,
        0xa8a85454u, 0x6a6a4040u, 0xa4a45000u, 0x1a1a0500u,
        0x0050a4a4u, 0xaaa59090u, 0x14696914u, 0x69691400u,
        0xa08585a0u, 0xaa821414u, 0x50a4a450u, 0x6a5a0200u,
        0xa9a58000u, 0x5090a0a8u, 0xa8a09050u, 0x24242424u,
        0x00aa5500u, 0x24924924u, 0x24499224u, 0x50a50a50u,
        0x500aa550u, 0xaaaa4444u, 0x66660000u, 0xa5a0a5a0u,
        0x50a050a0u, 0x69286928u, 0x44aaaa44u, 0x66666600u,
        0xaa444444u, 0x54a854a8u, 0x95809580u, 0x96969600u,
        0xa85454a8u, 0x80959580u, 0xaa141414u, 0x96960000u,
        0xaaaa1414u, 0xa05050a0u, 0xa0a5a5a0u, 0x96000000u,
        0x40804080u, 0xa9a8a9a8u, 0xaaaaaa44u, 0x2a4a5254u,
    ];

    private static readonly byte[,] SBptcA3 =
    {
        {
            3, 3, 15, 15, 8, 3, 15, 15,
            8, 8, 6, 6, 6, 5, 3, 3,
            3, 3, 8, 15, 3, 3, 6, 10,
            5, 8, 8, 6, 8, 5, 15, 15,
            8, 15, 3, 5, 6, 10, 8, 15,
            15, 3, 15, 5, 15, 15, 15, 15,
            3, 15, 5, 5, 5, 8, 5, 10,
            5, 10, 8, 13, 15, 12, 3, 3,
        },
        {
            15, 8, 8, 3, 15, 15, 3, 8,
            15, 15, 15, 15, 15, 15, 15, 8,
            15, 8, 15, 3, 15, 8, 15, 8,
            3, 15, 6, 10, 15, 15, 10, 8,
            15, 3, 15, 10, 10, 8, 9, 10,
            6, 15, 8, 15, 3, 6, 6, 8,
            15, 3, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 3, 15, 15, 8,
        },
    };

    private static readonly Bc6HModeInfo[] SBc6HModes =
    [
        new(true, 5, 10, 5, 5, 5),
        new(true, 5, 7, 6, 6, 6),
        new(true, 5, 11, 5, 4, 4),
        new(false, 0, 10, 10, 10, 10),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(true, 5, 11, 4, 5, 4),
        new(true, 0, 11, 9, 9, 9),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(true, 5, 11, 4, 4, 5),
        new(true, 0, 12, 8, 8, 8),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(true, 5, 9, 5, 5, 5),
        new(true, 0, 16, 4, 4, 4),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(true, 5, 8, 6, 5, 5),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(true, 5, 8, 5, 6, 5),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(true, 5, 8, 5, 5, 6),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(false, 0, 0, 0, 0, 0),
        new(false, 5, 6, 6, 6, 6),
        new(false, 0, 0, 0, 0, 0),
    ];

    private static readonly Bc7ModeInfo[] SBc7Modes =
    [
        new(3, 4, 0, 0, 4, 0, 1, 0, 3, 0),
        new(2, 6, 0, 0, 6, 0, 0, 1, 3, 0),
        new(3, 6, 0, 0, 5, 0, 0, 0, 2, 0),
        new(2, 6, 0, 0, 7, 0, 1, 0, 2, 0),
        new(1, 0, 2, 1, 5, 6, 0, 0, 2, 3),
        new(1, 0, 2, 0, 7, 8, 0, 0, 2, 2),
        new(1, 0, 0, 0, 7, 7, 1, 0, 4, 0),
        new(2, 6, 0, 0, 5, 5, 1, 0, 2, 0),
    ];
}
