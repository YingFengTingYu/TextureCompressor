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
    private const int Bc6HEncoderMode = 3;
    private const int Bc7EncoderMode = 6;
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
                TTransfer.DecodeBlock(source.Slice(blockOffset, TTransfer.BytesPerBlock), block);
                StoreUNormBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
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
                            LoadUNormBlock(localSource, blockX, blockY, block);
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

        var block = new InlineArray16<Rgba8UNorm>();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadUNormBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
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
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode);
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
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBc7Block(source, srgb: false, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBc7Block(source, srgb: false, destination, compressionMode);
    }

    private readonly struct Bc7SrgbTransfer : IBc7Transfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBc7Block(source, srgb: true, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBc7Block(source, srgb: true, destination, compressionMode);
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

        ReadBc6HEndpoints(mode, ref bit, epR, epG, epB);

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

    private static void EncodeBc6HBlock(
        ReadOnlySpan<Rgba32Float> source,
        bool signed,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                WriteBc6HMode3Block(source, signed, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeBc6HBlockWithModeSearch(source, signed, destination, compressionMode);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported BPTC compression mode.");
        }
    }

    private static void WriteBc6HMode3Block(ReadOnlySpan<Rgba32Float> source, bool signed, Span<byte> destination)
    {
        destination.Clear();
        FindBc6HEndpoints(source, signed, out var endpoint0, out var endpoint1);

        var q0 = new InlineArray3<int>();
        var q1 = new InlineArray3<int>();
        q0[0] = QuantizeBc6HEndpoint(endpoint0.Red, signed, 10);
        q0[1] = QuantizeBc6HEndpoint(endpoint0.Green, signed, 10);
        q0[2] = QuantizeBc6HEndpoint(endpoint0.Blue, signed, 10);
        q1[0] = QuantizeBc6HEndpoint(endpoint1.Red, signed, 10);
        q1[1] = QuantizeBc6HEndpoint(endpoint1.Green, signed, 10);
        q1[2] = QuantizeBc6HEndpoint(endpoint1.Blue, signed, 10);

        var palette = new InlineArray16<Rgba32Float>();
        BuildBc6HMode3Palette(q0, q1, signed, palette);

        var indices = new InlineArray16<int>();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var maxIndex = i == 0 ? 8 : 16;
            indices[i] = FindNearestColorIndex(ClampBc6HColor(source[i], signed), palette, maxIndex);
        }

        var writer = new BptcBitWriter(destination);
        writer.Write(Bc6HEncoderMode, 2);
        writer.Write(0, 3);
        writer.Write(q0[0], 10);
        writer.Write(q0[1], 10);
        writer.Write(q0[2], 10);
        writer.Write(q1[0], 10);
        writer.Write(q1[1], 10);
        writer.Write(q1[2], 10);
        writer.Write(indices[0], 3);
        for (var i = 1; i < TexelsPerBlock; i++)
        {
            writer.Write(indices[i], 4);
        }
    }

    private static void EncodeBc6HBlockWithModeSearch(
        ReadOnlySpan<Rgba32Float> source,
        bool signed,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        Span<byte> best = stackalloc byte[BytesPerBlock];
        Span<byte> candidate = stackalloc byte[BytesPerBlock];
        var bestError = float.MaxValue;

        TryBc6HModeCandidates(source, signed, Bc6HEncoderMode, compressionMode, candidate, best, ref bestError);
        TryBc6HModeCandidates(source, signed, mode: 7, compressionMode, candidate, best, ref bestError);
        TryBc6HModeCandidates(source, signed, mode: 11, compressionMode, candidate, best, ref bestError);
        TryBc6HModeCandidates(source, signed, mode: 15, compressionMode, candidate, best, ref bestError);

        best.CopyTo(destination);
    }

    private static void TryBc6HModeCandidates(
        ReadOnlySpan<Rgba32Float> source,
        bool signed,
        int mode,
        TextureCompressionLevel compressionMode,
        Span<byte> candidate,
        Span<byte> best,
        ref float bestError)
    {
        FindBc6HEndpoints(source, signed, out var endpoint0, out var endpoint1);
        TryBc6HModeCandidate(source, signed, mode, endpoint0, endpoint1, candidate, best, ref bestError);

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive)
        {
            FindBc6HComponentBounds(source, signed, out endpoint0, out endpoint1);
            TryBc6HModeCandidate(source, signed, mode, endpoint0, endpoint1, candidate, best, ref bestError);
        }

        if (compressionMode != TextureCompressionLevel.Exhaustive)
        {
            return;
        }

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var a = ClampBc6HColor(source[i], signed);
            for (var j = i + 1; j < TexelsPerBlock; j++)
            {
                var b = ClampBc6HColor(source[j], signed);
                TryBc6HModeCandidate(source, signed, mode, a, b, candidate, best, ref bestError);
            }
        }
    }

    private static void TryBc6HModeCandidate(
        ReadOnlySpan<Rgba32Float> source,
        bool signed,
        int mode,
        Rgba32Float endpoint0,
        Rgba32Float endpoint1,
        Span<byte> candidate,
        Span<byte> best,
        ref float bestError)
    {
        WriteBc6HModeBlock(source, signed, mode, endpoint0, endpoint1, candidate);
        var error = ScoreBc6HBlock(source, signed, candidate);
        if (error < bestError)
        {
            bestError = error;
            candidate.CopyTo(best);
        }

        WriteBc6HModeBlock(source, signed, mode, endpoint1, endpoint0, candidate);
        error = ScoreBc6HBlock(source, signed, candidate);
        if (error < bestError)
        {
            bestError = error;
            candidate.CopyTo(best);
        }
    }

    private static void WriteBc6HModeBlock(
        ReadOnlySpan<Rgba32Float> source,
        bool signed,
        int mode,
        Rgba32Float endpoint0,
        Rgba32Float endpoint1,
        Span<byte> destination)
    {
        destination.Clear();

        var modeInfo = SBc6HModes[mode];
        var q0 = new InlineArray3<int>();
        var q1 = new InlineArray3<int>();
        QuantizeBc6HModeEndpointPair(endpoint0, endpoint1, signed, modeInfo, q0, q1);

        var palette = new InlineArray16<Rgba32Float>();
        BuildBc6HPalette(mode, q0, q1, signed, palette);

        var indices = new InlineArray16<int>();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var maxIndex = i == 0 ? 8 : 16;
            indices[i] = FindNearestColorIndex(ClampBc6HColor(source[i], signed), palette, maxIndex);
        }

        var writer = new BptcBitWriter(destination);
        WriteBc6HModeHeader(ref writer, mode);
        WriteBc6HModeEndpoints(ref writer, mode, q0[0], q0[1], q0[2], q1[0], q1[1], q1[2]);
        writer.Write(indices[0], 3);
        for (var i = 1; i < TexelsPerBlock; i++)
        {
            writer.Write(indices[i], 4);
        }
    }

    private static void QuantizeBc6HModeEndpointPair(
        Rgba32Float endpoint0,
        Rgba32Float endpoint1,
        bool signed,
        Bc6HModeInfo modeInfo,
        Span<int> q0,
        Span<int> q1)
    {
        q0[0] = QuantizeBc6HEndpoint(endpoint0.Red, signed, modeInfo.EndpointBits);
        q0[1] = QuantizeBc6HEndpoint(endpoint0.Green, signed, modeInfo.EndpointBits);
        q0[2] = QuantizeBc6HEndpoint(endpoint0.Blue, signed, modeInfo.EndpointBits);

        var target1 = new InlineArray3<int>();
        target1[0] = QuantizeBc6HEndpoint(endpoint1.Red, signed, modeInfo.EndpointBits);
        target1[1] = QuantizeBc6HEndpoint(endpoint1.Green, signed, modeInfo.EndpointBits);
        target1[2] = QuantizeBc6HEndpoint(endpoint1.Blue, signed, modeInfo.EndpointBits);

        if (!modeInfo.Transformed)
        {
            q1[0] = target1[0];
            q1[1] = target1[1];
            q1[2] = target1[2];
            return;
        }

        q1[0] = QuantizeBc6HDelta(q0[0], target1[0], modeInfo.EndpointBits, modeInfo.DeltaBitsR);
        q1[1] = QuantizeBc6HDelta(q0[1], target1[1], modeInfo.EndpointBits, modeInfo.DeltaBitsG);
        q1[2] = QuantizeBc6HDelta(q0[2], target1[2], modeInfo.EndpointBits, modeInfo.DeltaBitsB);
    }

    private static int QuantizeBc6HDelta(int anchor, int target, int endpointBits, int deltaBits)
    {
        var modulo = 1 << endpointBits;
        var delta = target - anchor;
        if (delta > modulo / 2)
        {
            delta -= modulo;
        }
        else if (delta < -modulo / 2)
        {
            delta += modulo;
        }

        var minimum = -(1 << (deltaBits - 1));
        var maximum = (1 << (deltaBits - 1)) - 1;
        delta = Math.Clamp(delta, minimum, maximum);
        return delta & ((1 << deltaBits) - 1);
    }

    private static void BuildBc6HPalette(int mode, ReadOnlySpan<int> q0, ReadOnlySpan<int> q1, bool signed, Span<Rgba32Float> palette)
    {
        var modeInfo = SBc6HModes[mode];
        var e0 = new InlineArray3<int>();
        var e1 = new InlineArray3<int>();
        var mask = (1 << modeInfo.EndpointBits) - 1;

        for (var i = 0; i < 3; i++)
        {
            e0[i] = signed ? SignExtend(q0[i], modeInfo.EndpointBits) : q0[i];
            if (modeInfo.Transformed)
            {
                var deltaBits = i switch
                {
                    0 => modeInfo.DeltaBitsR,
                    1 => modeInfo.DeltaBitsG,
                    _ => modeInfo.DeltaBitsB
                };
                e1[i] = (q0[i] + SignExtend(q1[i], deltaBits)) & mask;
                if (signed)
                {
                    e1[i] = SignExtend(e1[i], modeInfo.EndpointBits);
                }
            }
            else
            {
                e1[i] = signed ? SignExtend(q1[i], modeInfo.EndpointBits) : q1[i];
            }

            e0[i] = UnquantizeBc6HEndpoint(e0[i], signed, modeInfo.EndpointBits);
            e1[i] = UnquantizeBc6HEndpoint(e1[i], signed, modeInfo.EndpointBits);
        }

        var weights = SBptcFactors[2];
        for (var i = 0; i < 16; i++)
        {
            var weight = weights[i];
            palette[i] = new Rgba32Float(
                HalfBitsToSingle(InterpolateBc6HHalf(e0[0], e1[0], weight, signed)),
                HalfBitsToSingle(InterpolateBc6HHalf(e0[1], e1[1], weight, signed)),
                HalfBitsToSingle(InterpolateBc6HHalf(e0[2], e1[2], weight, signed)),
                1f);
        }
    }

    private static void WriteBc6HModeHeader(ref BptcBitWriter writer, int mode)
    {
        writer.Write(mode & 3, 2);
        if ((mode & 2) != 0)
        {
            writer.Write(mode >> 2, 3);
        }
    }

    private static void WriteBc6HModeEndpoints(
        ref BptcBitWriter writer,
        int mode,
        int q0R,
        int q0G,
        int q0B,
        int q1R,
        int q1G,
        int q1B)
    {
        switch (mode)
        {
            case 3:
                writer.Write(q0R, 10);
                writer.Write(q0G, 10);
                writer.Write(q0B, 10);
                writer.Write(q1R, 10);
                writer.Write(q1G, 10);
                writer.Write(q1B, 10);
                break;
            case 7:
                writer.Write(q0R, 10);
                writer.Write(q0G, 10);
                writer.Write(q0B, 10);
                writer.Write(q1R, 9);
                writer.Write(q0R >> 10, 1);
                writer.Write(q1G, 9);
                writer.Write(q0G >> 10, 1);
                writer.Write(q1B, 9);
                writer.Write(q0B >> 10, 1);
                break;
            case 11:
                writer.Write(q0R, 10);
                writer.Write(q0G, 10);
                writer.Write(q0B, 10);
                writer.Write(q1R, 8);
                writer.Write(q0R >> 11, 1);
                writer.Write(q0R >> 10, 1);
                writer.Write(q1G, 8);
                writer.Write(q0G >> 11, 1);
                writer.Write(q0G >> 10, 1);
                writer.Write(q1B, 8);
                writer.Write(q0B >> 11, 1);
                writer.Write(q0B >> 10, 1);
                break;
            case 15:
                writer.Write(q0R, 10);
                writer.Write(q0G, 10);
                writer.Write(q0B, 10);
                writer.Write(q1R, 4);
                WriteBitsDescending(ref writer, q0R, 15, 10);
                writer.Write(q1G, 4);
                WriteBitsDescending(ref writer, q0G, 15, 10);
                writer.Write(q1B, 4);
                WriteBitsDescending(ref writer, q0B, 15, 10);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported BC6H encoder mode.");
        }
    }

    private static void WriteBitsDescending(ref BptcBitWriter writer, int value, int firstBit, int lastBit)
    {
        for (var bit = firstBit; bit >= lastBit; bit--)
        {
            writer.Write(value >> bit, 1);
        }
    }

    private static float ScoreBc6HBlock(ReadOnlySpan<Rgba32Float> source, bool signed, ReadOnlySpan<byte> candidate)
    {
        var decoded = new InlineArray16<Rgba32Float>();
        DecodeBc6HBlock(candidate, signed, decoded);

        var error = 0f;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            error += ColorDistance(ClampBc6HColor(source[i], signed), decoded[i]);
        }

        return error;
    }

    private static void DecodeBc7Block(ReadOnlySpan<byte> source, bool srgb, Span<Rgba8UNorm> destination)
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
                DecodeStorageByte((byte)red, srgb),
                DecodeStorageByte((byte)green, srgb),
                DecodeStorageByte((byte)blue, srgb),
                (byte)alpha);
        }
    }

    private static void EncodeBc7Block(
        ReadOnlySpan<Rgba8UNorm> source,
        bool srgb,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                WriteBc7Mode6FastBlock(source, srgb, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeBc7BlockWithModeSearch(source, srgb, destination, compressionMode);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported BPTC compression mode.");
        }
    }

    private static void WriteBc7Mode6FastBlock(ReadOnlySpan<Rgba8UNorm> source, bool srgb, Span<byte> destination)
    {
        var storage = new InlineArray16<Bc7Color32>();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            storage[i] = ToBc7StorageColor(source[i], srgb);
        }

        FindBc7Endpoints(storage, includeAlpha: true, out var a, out var b);
        var bestA = a;
        var bestB = b;
        var bestError = double.MaxValue;
        TryBc7Candidate(storage, a, b, ref bestA, ref bestB, ref bestError);

        FindBc7Endpoints(storage, includeAlpha: false, out a, out b);
        TryBc7Candidate(storage, a, b, ref bestA, ref bestB, ref bestError);

        FindBc7ComponentBounds(storage, out a, out b);
        TryBc7Candidate(storage, a, b, ref bestA, ref bestB, ref bestError);

        FindBc7AlphaBounds(storage, out a, out b);
        TryBc7Candidate(storage, a, b, ref bestA, ref bestB, ref bestError);

        WriteBc7Mode6Block(storage, bestA, bestB, destination);
    }

    private static void EncodeBc7BlockWithModeSearch(
        ReadOnlySpan<Rgba8UNorm> source,
        bool srgb,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        var storage = new InlineArray16<Bc7Color32>();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            storage[i] = ToBc7StorageColor(source[i], srgb);
        }

        Span<byte> best = stackalloc byte[BytesPerBlock];
        Span<byte> candidate = stackalloc byte[BytesPerBlock];
        WriteBc7Mode6FastBlock(source, srgb, best);
        var bestError = ScoreBc7Block(storage, best);

        TryBc7ModeCandidates(storage, mode: 6, partitionLimit: 1, candidate, best, ref bestError);
        TryBc7ModeCandidates(storage, mode: 5, partitionLimit: 1, candidate, best, ref bestError);

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive)
        {
            var partitionLimit = compressionMode == TextureCompressionLevel.High ? 16 : 64;
            TryBc7ModeCandidates(storage, mode: 1, partitionLimit, candidate, best, ref bestError);
            TryBc7ModeCandidates(storage, mode: 3, partitionLimit, candidate, best, ref bestError);
            TryBc7ModeCandidates(storage, mode: 7, partitionLimit, candidate, best, ref bestError);
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            TryBc7ModeCandidates(storage, mode: 0, partitionLimit: 64, candidate, best, ref bestError);
            TryBc7ModeCandidates(storage, mode: 2, partitionLimit: 64, candidate, best, ref bestError);
        }

        best.CopyTo(destination);
    }

    private static void TryBc7ModeCandidates(
        ReadOnlySpan<Bc7Color32> source,
        int mode,
        int partitionLimit,
        Span<byte> candidate,
        Span<byte> best,
        ref int bestError)
    {
        var modeInfo = SBc7Modes[mode];
        var partitionCount = modeInfo.PartitionBits == 0 ? 1 : Math.Min(partitionLimit, 1 << modeInfo.PartitionBits);
        for (var partitionSet = 0; partitionSet < partitionCount; partitionSet++)
        {
            TryBc7ModePartitionCandidate(source, mode, partitionSet, candidate, best, ref bestError);
        }
    }

    private static void TryBc7ModePartitionCandidate(
        ReadOnlySpan<Bc7Color32> source,
        int mode,
        int partitionSet,
        Span<byte> candidate,
        Span<byte> best,
        ref int bestError)
    {
        var modeInfo = SBc7Modes[mode];
        var endpoints = new InlineArray6<Bc7Color32>();
        for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
        {
            FindBc7SubsetEndpoints(source, modeInfo, partitionSet, subset, out endpoints[subset * 2], out endpoints[(subset * 2) + 1]);
        }

        var orientationCount = 1 << modeInfo.SubsetCount;
        var quantized = new InlineArray6<Bc7Endpoint>();
        for (var orientation = 0; orientation < orientationCount; orientation++)
        {
            for (var subset = 0; subset < modeInfo.SubsetCount; subset++)
            {
                var endpoint0 = endpoints[subset * 2];
                var endpoint1 = endpoints[(subset * 2) + 1];
                if (((orientation >> subset) & 1) != 0)
                {
                    (endpoint0, endpoint1) = (endpoint1, endpoint0);
                }

                QuantizeBc7EndpointPair(endpoint0, endpoint1, modeInfo, out quantized[subset * 2], out quantized[(subset * 2) + 1]);
            }

            WriteBc7ModeBlock(source, mode, partitionSet, quantized, candidate);
            var error = ScoreBc7Block(source, candidate);
            if (error < bestError)
            {
                bestError = error;
                candidate.CopyTo(best);
            }
        }
    }

    private static void WriteBc7ModeBlock(
        ReadOnlySpan<Bc7Color32> source,
        int mode,
        int partitionSet,
        ReadOnlySpan<Bc7Endpoint> endpoints,
        Span<byte> destination)
    {
        destination.Clear();

        var modeInfo = SBc7Modes[mode];
        var hasSecondaryIndices = modeInfo.IndexBits1 != 0;
        var primaryIndices = new InlineArray16<int>();
        var secondaryIndices = new InlineArray16<int>();

        for (var texel = 0; texel < TexelsPerBlock; texel++)
        {
            var subset = GetBc7Subset(modeInfo, partitionSet, texel);
            var anchor = GetBc7Anchor(modeInfo, partitionSet, subset);
            var primaryPaletteCount = 1 << (modeInfo.IndexBits0 - (texel == anchor ? 1 : 0));
            var endpoint = subset * 2;

            if (hasSecondaryIndices)
            {
                primaryIndices[texel] = FindNearestBc7RgbIndex(
                    source[texel],
                    endpoints[endpoint].Color,
                    endpoints[endpoint + 1].Color,
                    modeInfo.IndexBits0,
                    primaryPaletteCount);

                var secondaryPaletteCount = 1 << (modeInfo.IndexBits1 - (texel == anchor ? 1 : 0));
                secondaryIndices[texel] = FindNearestBc7AlphaIndex(
                    source[texel].Alpha,
                    endpoints[endpoint].Color.Alpha,
                    endpoints[endpoint + 1].Color.Alpha,
                    modeInfo.IndexBits1,
                    secondaryPaletteCount);
            }
            else
            {
                primaryIndices[texel] = FindNearestBc7Index(
                    source[texel],
                    endpoints[endpoint].Color,
                    endpoints[endpoint + 1].Color,
                    modeInfo.IndexBits0,
                    primaryPaletteCount);
            }
        }

        var writer = new BptcBitWriter(destination);
        writer.Write(0, mode);
        writer.Write(1, 1);
        writer.Write(partitionSet, modeInfo.PartitionBits);
        writer.Write(0, modeInfo.RotationBits);
        writer.Write(0, modeInfo.IndexSelectionBits);

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

    private static void ReadBc6HEndpoints(
        int mode,
        scoped ref BptcBitReader bit,
        scoped Span<int> epR,
        scoped Span<int> epG,
        scoped Span<int> epB)
    {
        switch (mode)
        {
            case 0:
                epG[2] |= bit.Read(1) << 4;
                epB[2] |= bit.Read(1) << 4;
                epB[3] |= bit.Read(1) << 4;
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(5);
                epG[3] |= bit.Read(1) << 4;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(5);
                epB[3] |= bit.Read(1);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 3;
                break;
            case 1:
                epG[2] |= bit.Read(1) << 5;
                epG[3] |= bit.Read(1) << 4;
                epG[3] |= bit.Read(1) << 5;
                epR[0] |= bit.Read(7);
                epB[3] |= bit.Read(1);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(1) << 4;
                epG[0] |= bit.Read(7);
                epB[2] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 2;
                epG[2] |= bit.Read(1) << 4;
                epB[0] |= bit.Read(7);
                epB[3] |= bit.Read(1) << 3;
                epB[3] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 4;
                epR[1] |= bit.Read(6);
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(6);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(6);
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(6);
                epR[3] |= bit.Read(6);
                break;
            case 2:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(5);
                epR[0] |= bit.Read(1) << 10;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(4);
                epG[0] |= bit.Read(1) << 10;
                epB[3] |= bit.Read(1);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(4);
                epB[0] |= bit.Read(1) << 10;
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 3;
                break;
            case 3:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(10);
                epG[1] |= bit.Read(10);
                epB[1] |= bit.Read(10);
                break;
            case 6:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(4);
                epR[0] |= bit.Read(1) << 10;
                epG[3] |= bit.Read(1) << 4;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(5);
                epG[0] |= bit.Read(1) << 10;
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(4);
                epB[0] |= bit.Read(1) << 10;
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(4);
                epB[3] |= bit.Read(1);
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(4);
                epG[2] |= bit.Read(1) << 4;
                epB[3] |= bit.Read(1) << 3;
                break;
            case 7:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(9);
                epR[0] |= bit.Read(1) << 10;
                epG[1] |= bit.Read(9);
                epG[0] |= bit.Read(1) << 10;
                epB[1] |= bit.Read(9);
                epB[0] |= bit.Read(1) << 10;
                break;
            case 10:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(4);
                epR[0] |= bit.Read(1) << 10;
                epB[2] |= bit.Read(1) << 4;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(4);
                epG[0] |= bit.Read(1) << 10;
                epB[3] |= bit.Read(1);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(5);
                epB[0] |= bit.Read(1) << 10;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(4);
                epB[3] |= bit.Read(1) << 1;
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(4);
                epB[3] |= bit.Read(1) << 4;
                epB[3] |= bit.Read(1) << 3;
                break;
            case 11:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(8);
                epR[0] |= bit.Read(1) << 11;
                epR[0] |= bit.Read(1) << 10;
                epG[1] |= bit.Read(8);
                epG[0] |= bit.Read(1) << 11;
                epG[0] |= bit.Read(1) << 10;
                epB[1] |= bit.Read(8);
                epB[0] |= bit.Read(1) << 11;
                epB[0] |= bit.Read(1) << 10;
                break;
            case 14:
                epR[0] |= bit.Read(9);
                epB[2] |= bit.Read(1) << 4;
                epG[0] |= bit.Read(9);
                epG[2] |= bit.Read(1) << 4;
                epB[0] |= bit.Read(9);
                epB[3] |= bit.Read(1) << 4;
                epR[1] |= bit.Read(5);
                epG[3] |= bit.Read(1) << 4;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(5);
                epB[3] |= bit.Read(1);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 3;
                break;
            case 15:
                epR[0] |= bit.Read(10);
                epG[0] |= bit.Read(10);
                epB[0] |= bit.Read(10);
                epR[1] |= bit.Read(4);
                epR[0] |= bit.Read(1) << 15;
                epR[0] |= bit.Read(1) << 14;
                epR[0] |= bit.Read(1) << 13;
                epR[0] |= bit.Read(1) << 12;
                epR[0] |= bit.Read(1) << 11;
                epR[0] |= bit.Read(1) << 10;
                epG[1] |= bit.Read(4);
                epG[0] |= bit.Read(1) << 15;
                epG[0] |= bit.Read(1) << 14;
                epG[0] |= bit.Read(1) << 13;
                epG[0] |= bit.Read(1) << 12;
                epG[0] |= bit.Read(1) << 11;
                epG[0] |= bit.Read(1) << 10;
                epB[1] |= bit.Read(4);
                epB[0] |= bit.Read(1) << 15;
                epB[0] |= bit.Read(1) << 14;
                epB[0] |= bit.Read(1) << 13;
                epB[0] |= bit.Read(1) << 12;
                epB[0] |= bit.Read(1) << 11;
                epB[0] |= bit.Read(1) << 10;
                break;
            case 18:
                epR[0] |= bit.Read(8);
                epG[3] |= bit.Read(1) << 4;
                epB[2] |= bit.Read(1) << 4;
                epG[0] |= bit.Read(8);
                epB[3] |= bit.Read(1) << 2;
                epG[2] |= bit.Read(1) << 4;
                epB[0] |= bit.Read(8);
                epB[3] |= bit.Read(1) << 3;
                epB[3] |= bit.Read(1) << 4;
                epR[1] |= bit.Read(6);
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(5);
                epB[3] |= bit.Read(1);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(6);
                epR[3] |= bit.Read(6);
                break;
            case 22:
                epR[0] |= bit.Read(8);
                epB[3] |= bit.Read(1);
                epB[2] |= bit.Read(1) << 4;
                epG[0] |= bit.Read(8);
                epG[2] |= bit.Read(1) << 5;
                epG[2] |= bit.Read(1) << 4;
                epB[0] |= bit.Read(8);
                epG[3] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 4;
                epR[1] |= bit.Read(5);
                epG[3] |= bit.Read(1) << 4;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(6);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 3;
                break;
            case 26:
                epR[0] |= bit.Read(8);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(1) << 4;
                epG[0] |= bit.Read(8);
                epB[2] |= bit.Read(1) << 5;
                epG[2] |= bit.Read(1) << 4;
                epB[0] |= bit.Read(8);
                epB[3] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 4;
                epR[1] |= bit.Read(5);
                epG[3] |= bit.Read(1) << 4;
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(5);
                epB[3] |= bit.Read(1);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(6);
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 2;
                epR[3] |= bit.Read(5);
                epB[3] |= bit.Read(1) << 3;
                break;
            case 30:
                epR[0] |= bit.Read(6);
                epG[3] |= bit.Read(1) << 4;
                epB[3] |= bit.Read(1);
                epB[3] |= bit.Read(1) << 1;
                epB[2] |= bit.Read(1) << 4;
                epG[0] |= bit.Read(6);
                epG[2] |= bit.Read(1) << 5;
                epB[2] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 2;
                epG[2] |= bit.Read(1) << 4;
                epB[0] |= bit.Read(6);
                epG[3] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 3;
                epB[3] |= bit.Read(1) << 5;
                epB[3] |= bit.Read(1) << 4;
                epR[1] |= bit.Read(6);
                epG[2] |= bit.Read(4);
                epG[1] |= bit.Read(6);
                epG[3] |= bit.Read(4);
                epB[1] |= bit.Read(6);
                epB[2] |= bit.Read(4);
                epR[2] |= bit.Read(6);
                epR[3] |= bit.Read(6);
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

    private static void FindBc6HEndpoints(ReadOnlySpan<Rgba32Float> source, bool signed, out Rgba32Float endpoint0, out Rgba32Float endpoint1)
    {
        endpoint0 = ClampBc6HColor(source[0], signed);
        endpoint1 = endpoint0;
        var bestDistance = -1f;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var a = ClampBc6HColor(source[i], signed);
            for (var j = i + 1; j < TexelsPerBlock; j++)
            {
                var b = ClampBc6HColor(source[j], signed);
                var distance = ColorDistance(a, b);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    endpoint0 = a;
                    endpoint1 = b;
                }
            }
        }
    }

    private static void FindBc6HComponentBounds(ReadOnlySpan<Rgba32Float> source, bool signed, out Rgba32Float minimum, out Rgba32Float maximum)
    {
        minimum = ClampBc6HColor(source[0], signed);
        maximum = minimum;

        for (var i = 1; i < TexelsPerBlock; i++)
        {
            var color = ClampBc6HColor(source[i], signed);
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
        var halfBits = BitConverter.HalfToUInt16Bits((Half)ClampBc6HValue(value, signed));
        if (!signed)
        {
            var target = (int)MathF.Round(((halfBits & 0x7fff) * 64f) / 31f);
            return QuantizeBc6HUnsigned(target, bits);
        }

        var sign = (halfBits & 0x8000) != 0;
        var targetMagnitude = (int)MathF.Round(((halfBits & 0x7fff) * 32f) / 31f);
        var quantizedMagnitude = QuantizeBc6HSignedMagnitude(targetMagnitude, bits);
        if (!sign || quantizedMagnitude == 0)
        {
            return quantizedMagnitude;
        }

        return ((1 << bits) - quantizedMagnitude) & ((1 << bits) - 1);
    }

    private static int QuantizeBc6HUnsigned(int value, int bits)
    {
        value = Math.Clamp(value, 0, 0xffff);
        var max = (1 << bits) - 1;
        return (value * max + 32767) / 65535;
    }

    private static int QuantizeBc6HSignedMagnitude(int value, int bits)
    {
        value = Math.Clamp(value, 0, 0x7fff);
        var max = (1 << (bits - 1)) - 1;
        return (value * max + 16383) / 32767;
    }

    private static void BuildBc6HMode3Palette(ReadOnlySpan<int> q0, ReadOnlySpan<int> q1, bool signed, Span<Rgba32Float> palette)
    {
        var e0 = new InlineArray3<int>();
        var e1 = new InlineArray3<int>();
        for (var i = 0; i < 3; i++)
        {
            e0[i] = signed ? SignExtend(q0[i], 10) : q0[i];
            e1[i] = signed ? SignExtend(q1[i], 10) : q1[i];
            e0[i] = UnquantizeBc6HEndpoint(e0[i], signed, 10);
            e1[i] = UnquantizeBc6HEndpoint(e1[i], signed, 10);
        }

        var weights = SBptcFactors[2];
        for (var i = 0; i < 16; i++)
        {
            var weight = weights[i];
            palette[i] = new Rgba32Float(
                HalfBitsToSingle(InterpolateBc6HHalf(e0[0], e1[0], weight, signed)),
                HalfBitsToSingle(InterpolateBc6HHalf(e0[1], e1[1], weight, signed)),
                HalfBitsToSingle(InterpolateBc6HHalf(e0[2], e1[2], weight, signed)),
                1f);
        }
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

    private static Bc7Color32 ToBc7StorageColor(Rgba8UNorm color, bool srgb) => new(
        EncodeStorageByte(color.Red, srgb),
        EncodeStorageByte(color.Green, srgb),
        EncodeStorageByte(color.Blue, srgb),
        color.Alpha);

    private static void FindBc7SubsetEndpoints(
        ReadOnlySpan<Bc7Color32> source,
        Bc7ModeInfo modeInfo,
        int partitionSet,
        int subset,
        out Bc7Color32 endpoint0,
        out Bc7Color32 endpoint1)
    {
        if (modeInfo.IndexBits1 != 0)
        {
            FindBc7SubsetColorEndpoints(source, modeInfo, partitionSet, subset, includeAlpha: false, out var color0, out var color1);
            FindBc7SubsetAlphaBounds(source, modeInfo, partitionSet, subset, out var alpha0, out var alpha1);
            endpoint0 = new Bc7Color32(color0.Red, color0.Green, color0.Blue, alpha0);
            endpoint1 = new Bc7Color32(color1.Red, color1.Green, color1.Blue, alpha1);
            return;
        }

        FindBc7SubsetColorEndpoints(
            source,
            modeInfo,
            partitionSet,
            subset,
            includeAlpha: modeInfo.AlphaBits != 0,
            out endpoint0,
            out endpoint1);

        if (modeInfo.AlphaBits == 0)
        {
            endpoint0 = new Bc7Color32(endpoint0.Red, endpoint0.Green, endpoint0.Blue, byte.MaxValue);
            endpoint1 = new Bc7Color32(endpoint1.Red, endpoint1.Green, endpoint1.Blue, byte.MaxValue);
        }
    }

    private static void FindBc7SubsetColorEndpoints(
        ReadOnlySpan<Bc7Color32> source,
        Bc7ModeInfo modeInfo,
        int partitionSet,
        int subset,
        bool includeAlpha,
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

    private static void FindBc7SubsetAlphaBounds(
        ReadOnlySpan<Bc7Color32> source,
        Bc7ModeInfo modeInfo,
        int partitionSet,
        int subset,
        out byte minimum,
        out byte maximum)
    {
        minimum = byte.MaxValue;
        maximum = byte.MinValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (GetBc7Subset(modeInfo, partitionSet, i) != subset)
            {
                continue;
            }

            minimum = Math.Min(minimum, source[i].Alpha);
            maximum = Math.Max(maximum, source[i].Alpha);
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
        var best = 0;
        var bestError = int.MaxValue;
        reconstructed = 0;
        for (var quantized = 0; quantized < (1 << valueBits); quantized++)
        {
            var expanded = ExpandQuantizedBc7((quantized << pBits) | pBit, valueBits + pBits);
            var error = Squared(value - expanded);
            if (error < bestError)
            {
                bestError = error;
                best = quantized;
                reconstructed = expanded;
            }
        }

        return best;
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

    private static int FindNearestBc7Index(
        Bc7Color32 color,
        Bc7Color32 endpoint0,
        Bc7Color32 endpoint1,
        int indexBits,
        int paletteCount)
    {
        var palette = new InlineArray16<Bc7Color32>();
        BuildBc7Palette(endpoint0, endpoint1, indexBits, palette);
        return FindNearestBc7Index(color, palette, paletteCount);
    }

    private static int FindNearestBc7RgbIndex(
        Bc7Color32 color,
        Bc7Color32 endpoint0,
        Bc7Color32 endpoint1,
        int indexBits,
        int paletteCount)
    {
        var weights = SBptcFactors[indexBits - 2];
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < paletteCount; i++)
        {
            var weight = weights[i];
            var red = InterpolateByte(endpoint0.Red, endpoint1.Red, weight);
            var green = InterpolateByte(endpoint0.Green, endpoint1.Green, weight);
            var blue = InterpolateByte(endpoint0.Blue, endpoint1.Blue, weight);
            var distance =
                Squared(color.Red - red) +
                Squared(color.Green - green) +
                Squared(color.Blue - blue);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int FindNearestBc7AlphaIndex(byte alpha, byte endpoint0, byte endpoint1, int indexBits, int paletteCount)
    {
        var weights = SBptcFactors[indexBits - 2];
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < paletteCount; i++)
        {
            var value = InterpolateByte(endpoint0, endpoint1, weights[i]);
            var distance = Squared(alpha - value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int ScoreBc7Block(ReadOnlySpan<Bc7Color32> source, ReadOnlySpan<byte> candidate)
    {
        var decoded = new InlineArray16<Rgba8UNorm>();
        DecodeBc7Block(candidate, srgb: false, decoded);

        var error = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var color = new Bc7Color32(decoded[i].Red, decoded[i].Green, decoded[i].Blue, decoded[i].Alpha);
            error += ColorDistance(source[i], color);
        }

        return error;
    }

    private static void TryBc7Candidate(
        ReadOnlySpan<Bc7Color32> source,
        Bc7Color32 endpoint0,
        Bc7Color32 endpoint1,
        ref Bc7Color32 bestA,
        ref Bc7Color32 bestB,
        ref double bestError)
    {
        var error = ScoreBc7Mode6(source, endpoint0, endpoint1);
        if (error < bestError)
        {
            bestError = error;
            bestA = endpoint0;
            bestB = endpoint1;
        }

        error = ScoreBc7Mode6(source, endpoint1, endpoint0);
        if (error < bestError)
        {
            bestError = error;
            bestA = endpoint1;
            bestB = endpoint0;
        }
    }

    private static float ScoreBc7Mode6(ReadOnlySpan<Bc7Color32> source, Bc7Color32 endpoint0, Bc7Color32 endpoint1)
    {
        QuantizeBc7Mode6Endpoint(endpoint0, out var quantized0);
        QuantizeBc7Mode6Endpoint(endpoint1, out var quantized1);
        var palette = new InlineArray16<Bc7Color32>();
        BuildBc7Palette(quantized0.Color, quantized1.Color, palette);
        var error = 0f;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var maxIndex = i == 0 ? 8 : 16;
            var index = FindNearestBc7Index(source[i], palette, maxIndex);
            error += ColorDistance(source[i], palette[index]);
        }

        return error;
    }

    private static void WriteBc7Mode6Block(ReadOnlySpan<Bc7Color32> source, Bc7Color32 endpoint0, Bc7Color32 endpoint1, Span<byte> destination)
    {
        destination.Clear();
        QuantizeBc7Mode6Endpoint(endpoint0, out var quantized0);
        QuantizeBc7Mode6Endpoint(endpoint1, out var quantized1);
        var palette = new InlineArray16<Bc7Color32>();
        BuildBc7Palette(quantized0.Color, quantized1.Color, palette);

        var writer = new BptcBitWriter(destination);
        writer.Write(0, Bc7EncoderMode);
        writer.Write(1, 1);
        writer.Write(quantized0.Red7, 7);
        writer.Write(quantized1.Red7, 7);
        writer.Write(quantized0.Green7, 7);
        writer.Write(quantized1.Green7, 7);
        writer.Write(quantized0.Blue7, 7);
        writer.Write(quantized1.Blue7, 7);
        writer.Write(quantized0.Alpha7, 7);
        writer.Write(quantized1.Alpha7, 7);
        writer.Write(quantized0.PBit, 1);
        writer.Write(quantized1.PBit, 1);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var maxIndex = i == 0 ? 8 : 16;
            var index = FindNearestBc7Index(source[i], palette, maxIndex);
            writer.Write(index, i == 0 ? 3 : 4);
        }
    }

    private static void QuantizeBc7Mode6Endpoint(Bc7Color32 source, out Bc7Mode6Endpoint endpoint)
    {
        var bestError = int.MaxValue;
        endpoint = default;
        for (var p = 0; p <= 1; p++)
        {
            var red7 = QuantizeBc7EndpointByte(source.Red, p, out var red);
            var green7 = QuantizeBc7EndpointByte(source.Green, p, out var green);
            var blue7 = QuantizeBc7EndpointByte(source.Blue, p, out var blue);
            var alpha7 = QuantizeBc7EndpointByte(source.Alpha, p, out var alpha);
            var error = Squared(source.Red - red) + Squared(source.Green - green) + Squared(source.Blue - blue) + Squared(source.Alpha - alpha);
            if (error < bestError)
            {
                bestError = error;
                endpoint = new Bc7Mode6Endpoint(red7, green7, blue7, alpha7, p, new Bc7Color32((byte)red, (byte)green, (byte)blue, (byte)alpha));
            }
        }
    }

    private static int QuantizeBc7EndpointByte(byte value, int pBit, out int reconstructed)
    {
        var quantized = Math.Clamp((value - pBit + 1) / 2, 0, 127);
        reconstructed = (quantized << 1) | pBit;
        return quantized;
    }

    private static void BuildBc7Palette(Bc7Color32 endpoint0, Bc7Color32 endpoint1, Span<Bc7Color32> palette) =>
        BuildBc7Palette(endpoint0, endpoint1, indexBits: 4, palette);

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

    private static int FindNearestBc7Index(Bc7Color32 color, ReadOnlySpan<Bc7Color32> palette, int paletteCount)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
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

    private static byte DecodeStorageByte(byte value, bool srgb) =>
        srgb ? RgbaColorConversions.Srgb8ToLinearUNorm8(value) : value;

    private static byte EncodeStorageByte(byte value, bool srgb) =>
        srgb ? RgbaColorConversions.LinearUNorm8ToSrgb8(value) : value;

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

    private static void LoadUNormBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
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

    private static void StoreUNormBlock<TPixel>(
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

    private ref struct BptcBitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _bitPosition;

        public BptcBitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _bitPosition = 0;
        }

        public int Read(int bitCount)
        {
            var value = Peek(0, bitCount);
            _bitPosition += bitCount;
            return value;
        }

        public int Peek(int offset, int bitCount)
        {
            if (bitCount == 0)
            {
                return 0;
            }

            var position = _bitPosition + offset;
            var value = 0;
            for (var i = 0; i < bitCount; i++)
            {
                var bit = (_data[(position + i) >> 3] >> ((position + i) & 7)) & 1;
                value |= bit << i;
            }

            return value;
        }
    }

    private ref struct BptcBitWriter
    {
        private readonly Span<byte> _data;
        private int _bitPosition;

        public BptcBitWriter(Span<byte> data)
        {
            _data = data;
            _bitPosition = 0;
        }

        public void Write(int value, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                if (((value >> i) & 1) != 0)
                {
                    _data[_bitPosition >> 3] |= (byte)(1 << (_bitPosition & 7));
                }

                _bitPosition++;
            }
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

    private readonly record struct Bc7Mode6Endpoint(int Red7, int Green7, int Blue7, int Alpha7, int PBit, Bc7Color32 Color);

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
