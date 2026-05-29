using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class AstcTextureCoder : IPitchTextureCoder
{
    private const int BytesPerBlock = 16;
    private const int MaxBlockWidth = 12;
    private const int MaxBlockHeight = 12;
    private const int MaxTexelsPerBlock = MaxBlockWidth * MaxBlockHeight;
    private const int MaxWeightValues = 64;
    private const ushort LnsOne = 0x7800;

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

    public AstcTextureCoder(TextureFormat format)
    {
        if (!TryGetFormatInfo(format, out var info))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _transfer = info.Transfer;
        _blockWidth = info.BlockWidth;
        _blockHeight = info.BlockHeight;
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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);
        DecodeByTransfer(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeByTransfer(source, destination, rowPitch);
    }

    private void DecodeByTransfer<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
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

    private void EncodeByTransfer<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
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

    private void DecodeUNorm<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
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

    private void EncodeFloat<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAstcFloatTransfer
    {
        var blockCountX = GetBlockCount(source.Width, _blockWidth);
        var blockCountY = GetBlockCount(source.Height, _blockHeight);
        var block = new Rgba16FloatTexelBlock();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadFloatBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, _blockWidth, _blockHeight, destination.Slice(blockOffset, BytesPerBlock));
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeFloat<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
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

    private void EncodeUNorm<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAstcUNormTransfer
    {
        var blockCountX = GetBlockCount(source.Width, _blockWidth);
        var blockCountY = GetBlockCount(source.Height, _blockHeight);
        var block = new Rgba8UNormTexelBlock();

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadUNormBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, _blockWidth, _blockHeight, destination.Slice(blockOffset, BytesPerBlock));
                blockOffset = checked(blockOffset + BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IAstcUNormTransfer
    {
        static abstract void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, Span<byte> destination);
    }

    private interface IAstcFloatTransfer
    {
        static abstract void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba16Float> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba16Float> source, int blockWidth, int blockHeight, Span<byte> destination);
    }

    private readonly struct AstcLdrTransfer : IAstcUNormTransfer
    {
        public static void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba8UNorm> destination) =>
            DecodeLdrBlock(source, blockWidth, blockHeight, srgb: false, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, Span<byte> destination) =>
            EncodeLdrBlock(source, blockWidth, blockHeight, srgb: false, destination);
    }

    private readonly struct AstcSrgbTransfer : IAstcUNormTransfer
    {
        public static void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba8UNorm> destination) =>
            DecodeLdrBlock(source, blockWidth, blockHeight, srgb: true, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, Span<byte> destination) =>
            EncodeLdrBlock(source, blockWidth, blockHeight, srgb: true, destination);
    }

    private readonly struct AstcHdrTransfer : IAstcFloatTransfer
    {
        public static void DecodeBlock(ReadOnlySpan<byte> source, int blockWidth, int blockHeight, Span<Rgba16Float> destination) =>
            DecodeHdrBlock(source, blockWidth, blockHeight, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba16Float> source, int blockWidth, int blockHeight, Span<byte> destination) =>
            EncodeHdrBlock(source, blockWidth, blockHeight, destination);
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

    private static void EncodeLdrBlock(ReadOnlySpan<Rgba8UNorm> source, int blockWidth, int blockHeight, bool srgb, Span<byte> destination)
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

    private static void EncodeHdrBlock(ReadOnlySpan<Rgba16Float> source, int blockWidth, int blockHeight, Span<byte> destination)
    {
        var texelCount = blockWidth * blockHeight;
        if (IsSolidBlock(source, texelCount, out var solidColor))
        {
            WriteHdrVoidExtentBlock(solidColor, destination);
            return;
        }

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

    private static int QuantizeWeight(Rgba8UNorm color, Rgba8UNorm low, Rgba8UNorm high)
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
        return (int)Math.Clamp(Math.Round(projected * 3.0), 0.0, 3.0);
    }

    private static int QuantizeWeight(Rgba16Float color, Rgba16Float low, Rgba16Float high)
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
        return (int)Math.Clamp(Math.Round(projected * 3.0), 0.0, 3.0);
    }

    private static int GetEncoderGridCoordinate(int coordinate, int blockSize) =>
        ((coordinate * 3) + ((blockSize - 1) / 2)) / (blockSize - 1);

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

    private void LoadUNormBlock<TPixel>(ImageView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
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

    private void LoadFloatBlock<TPixel>(ImageView<TPixel> source, int blockX, int blockY, Span<Rgba16Float> destination)
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

    private void StoreUNormBlock<TPixel>(ReadOnlySpan<Rgba8UNorm> block, int blockX, int blockY, ImageView<TPixel> destination)
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

    private void StoreFloatBlock<TPixel>(ReadOnlySpan<Rgba16Float> block, int blockX, int blockY, ImageView<TPixel> destination)
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
