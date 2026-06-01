using TextureCompressor.Bitmaps;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

/// <summary>
/// Encodes and decodes raw Basis Universal UASTC LDR 4x4 block payloads.
/// </summary>
public sealed class BasisUastcLdr4x4TextureCoder : IPitchTextureCoder
{
    public const int BlockWidth = 4;
    public const int BlockHeight = 4;
    public const int BytesPerBlock = 16;

    private const int TexelsPerBlock = BlockWidth * BlockHeight;
    private const int UastcEndpointValueCount = 18;
    private const int UastcWeightValueCount = 32;
    private const int TritQuintValueCount = 8;
    private const int EndpointColorCount = 3 * 2;
    private const int RgbaComponentCount = 4;
    private const int BlockColorCount = 3 * 32;
    private const int AstcEndpointOrderValueCount = 256;
    private const int SolidColorModeEncodedValue = 0x17;
    private const int SolidColorModeEncodedBitCount = 5;
    private const int InterpolatedRgbaModeEncodedValue = 0x0d;
    private const int InterpolatedRgbaModeEncodedBitCount = 5;
    private const int InterpolatedRgbaHintBitCount = 23;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.RgbaBasisUastcLdr4x4UNorm,
        TextureFormats.RgbaBasisUastcLdr4x4Srgb
    ];

    public BasisUastcLdr4x4TextureCoder()
        : this(TextureFormats.RgbaBasisUastcLdr4x4UNorm)
    {
    }

    public BasisUastcLdr4x4TextureCoder(TextureFormat format)
    {
        if (!IsSupported(format))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.RgbaBasisUastcLdr4x4UNorm
        || format == TextureFormats.RgbaBasisUastcLdr4x4Srgb;

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

        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        var srgb = Format.ValueKind == TextureValueKind.Srgb;

        if (TextureCodingParallel.ShouldParallelize(blockCountX, blockCountY))
        {
            var width = destination.Width;
            var height = destination.Height;
            var pixelCount = checked(width * height);
            var sourceLength = source.Length;
            unsafe
            {
                fixed (byte* sourceBase = source)
                fixed (TPixel* destinationBase = destination.Pixels)
                {
                    var sourceAddress = (nint)sourceBase;
                    var destinationAddress = (nint)destinationBase;
                    Parallel.For(0, blockCountY, blockY =>
                    {
                        var localSource = new ReadOnlySpan<byte>((void*)sourceAddress, sourceLength);
                        var localDestination = new BitmapView<TPixel>(
                            new Span<TPixel>((void*)destinationAddress, pixelCount),
                            width,
                            height);
                        DecodeBlockRow(localSource, localDestination, rowPitch, blockCountX, blockY, srgb);
                    });
                }
            }

            return;
        }

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            DecodeBlockRow(source, destination, rowPitch, blockCountX, blockY, srgb);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);
        var srgb = Format.ValueKind == TextureValueKind.Srgb;

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
                        EncodeBlockRow(localSource, localDestination, rowPitch, blockCountX, blockY, srgb);
                    });
                }
            }

            return;
        }

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            EncodeBlockRow(source, destination, rowPitch, blockCountX, blockY, srgb);
        }
    }

    private static void DecodeBlockRow<TPixel>(
        ReadOnlySpan<byte> source,
        BitmapView<TPixel> destination,
        int rowPitch,
        int blockCountX,
        int blockY,
        bool srgb)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blockPixelsStorage = new Rgba8UNormTexelBlock();
        Span<Rgba8UNorm> blockPixels = blockPixelsStorage;
        var blockOffset = checked(blockY * rowPitch);
        for (var blockX = 0; blockX < blockCountX; blockX++)
        {
            DecodeBlock(source.Slice(blockOffset, BytesPerBlock), blockPixels);
            if (srgb)
            {
                DecodeSrgbColors(blockPixels);
            }

            var xCount = Math.Min(BlockWidth, destination.Width - (blockX * BlockWidth));
            var yCount = Math.Min(BlockHeight, destination.Height - (blockY * BlockHeight));
            for (var y = 0; y < yCount; y++)
            {
                for (var x = 0; x < xCount; x++)
                {
                    destination[(blockX * BlockWidth) + x, (blockY * BlockHeight) + y] =
                        TPixel.FromRgba8UNorm(blockPixels[(y * BlockWidth) + x]);
                }
            }

            blockOffset = checked(blockOffset + BytesPerBlock);
        }
    }

    private static void EncodeBlockRow<TPixel>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        int blockCountX,
        int blockY,
        bool srgb)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blockPixelsStorage = new Rgba8UNormTexelBlock();
        Span<Rgba8UNorm> blockPixels = blockPixelsStorage;
        var blockOffset = checked(blockY * rowPitch);
        for (var blockX = 0; blockX < blockCountX; blockX++)
        {
            LoadBlock(source, blockX, blockY, blockPixels);
            if (srgb)
            {
                EncodeSrgbColors(blockPixels);
            }

            EncodeBlock(blockPixels, destination.Slice(blockOffset, BytesPerBlock));
            blockOffset = checked(blockOffset + BytesPerBlock);
        }
    }

    private static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        var solidColor = AverageBlockColor(source);
        var solidError = EvaluateSolidBlock(source, solidColor);
        var endpoints = FindEndpointBounds(source);

        var interpolatedError = EvaluateInterpolatedRgbaBlock(source, endpoints.Low, endpoints.High);
        var swappedInterpolatedError = EvaluateInterpolatedRgbaBlock(source, endpoints.High, endpoints.Low);
        var useSwappedEndpoints = swappedInterpolatedError < interpolatedError;
        var bestInterpolatedError = useSwappedEndpoints ? swappedInterpolatedError : interpolatedError;

        if (solidError <= bestInterpolatedError)
        {
            EncodeSolidColorBlock(solidColor, destination);
            return;
        }

        var low = useSwappedEndpoints ? endpoints.High : endpoints.Low;
        var high = useSwappedEndpoints ? endpoints.Low : endpoints.High;
        EncodeInterpolatedRgbaBlock(source, low, high, destination);
    }

    private static void LoadBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockWidth;
        var originY = blockY * BlockHeight;
        var lastX = source.Width - 1;
        var lastY = source.Height - 1;
        for (var y = 0; y < BlockHeight; y++)
        {
            var sourceY = Math.Min(originY + y, lastY);
            for (var x = 0; x < BlockWidth; x++)
            {
                var sourceX = Math.Min(originX + x, lastX);
                destination[(y * BlockWidth) + x] = TPixel.ToRgba8UNorm(source[sourceX, sourceY]);
            }
        }
    }

    private static void EncodeSolidColorBlock(Rgba8UNorm color, Span<byte> destination)
    {
        destination[..BytesPerBlock].Clear();

        var bitOffset = 0;
        WriteBits(destination, ref bitOffset, SolidColorModeEncodedValue, SolidColorModeEncodedBitCount);
        WriteBits(destination, ref bitOffset, color.Red, 8);
        WriteBits(destination, ref bitOffset, color.Green, 8);
        WriteBits(destination, ref bitOffset, color.Blue, 8);
        WriteBits(destination, ref bitOffset, color.Alpha, 8);
    }

    private static void EncodeInterpolatedRgbaBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Rgba8UNorm low,
        Rgba8UNorm high,
        Span<byte> destination)
    {
        destination[..BytesPerBlock].Clear();

        var bitOffset = 0;
        WriteBits(destination, ref bitOffset, InterpolatedRgbaModeEncodedValue, InterpolatedRgbaModeEncodedBitCount);
        WriteBits(destination, ref bitOffset, 0, InterpolatedRgbaHintBitCount);
        WriteBits(destination, ref bitOffset, low.Red, 8);
        WriteBits(destination, ref bitOffset, high.Red, 8);
        WriteBits(destination, ref bitOffset, low.Green, 8);
        WriteBits(destination, ref bitOffset, high.Green, 8);
        WriteBits(destination, ref bitOffset, low.Blue, 8);
        WriteBits(destination, ref bitOffset, high.Blue, 8);
        WriteBits(destination, ref bitOffset, low.Alpha, 8);
        WriteBits(destination, ref bitOffset, high.Alpha, 8);

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var weight = FindBestInterpolatedWeight(source[i], low, high, i == 0 ? 1 : 3, out _);
            WriteBits(destination, ref bitOffset, weight, i == 0 ? 1 : 2);
        }
    }

    private static Rgba8UNorm AverageBlockColor(ReadOnlySpan<Rgba8UNorm> source)
    {
        var red = 0;
        var green = 0;
        var blue = 0;
        var alpha = 0;
        foreach (var texel in source[..TexelsPerBlock])
        {
            red += texel.Red;
            green += texel.Green;
            blue += texel.Blue;
            alpha += texel.Alpha;
        }

        return new Rgba8UNorm(
            AverageToByte(red, TexelsPerBlock),
            AverageToByte(green, TexelsPerBlock),
            AverageToByte(blue, TexelsPerBlock),
            AverageToByte(alpha, TexelsPerBlock));
    }

    private static byte AverageToByte(int total, int count) =>
        checked((byte)((total + (count / 2)) / count));

    private static long EvaluateSolidBlock(ReadOnlySpan<Rgba8UNorm> source, Rgba8UNorm color)
    {
        var error = 0L;
        foreach (var texel in source[..TexelsPerBlock])
        {
            error += ColorError(texel, color);
        }

        return error;
    }

    private static long EvaluateInterpolatedRgbaBlock(ReadOnlySpan<Rgba8UNorm> source, Rgba8UNorm low, Rgba8UNorm high)
    {
        var error = 0L;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            _ = FindBestInterpolatedWeight(source[i], low, high, i == 0 ? 1 : 3, out var texelError);
            error += texelError;
        }

        return error;
    }

    private static UastcRgbaEndpointPair FindEndpointBounds(ReadOnlySpan<Rgba8UNorm> source)
    {
        var minRed = byte.MaxValue;
        var minGreen = byte.MaxValue;
        var minBlue = byte.MaxValue;
        var minAlpha = byte.MaxValue;
        var maxRed = byte.MinValue;
        var maxGreen = byte.MinValue;
        var maxBlue = byte.MinValue;
        var maxAlpha = byte.MinValue;

        foreach (var texel in source[..TexelsPerBlock])
        {
            minRed = Math.Min(minRed, texel.Red);
            minGreen = Math.Min(minGreen, texel.Green);
            minBlue = Math.Min(minBlue, texel.Blue);
            minAlpha = Math.Min(minAlpha, texel.Alpha);
            maxRed = Math.Max(maxRed, texel.Red);
            maxGreen = Math.Max(maxGreen, texel.Green);
            maxBlue = Math.Max(maxBlue, texel.Blue);
            maxAlpha = Math.Max(maxAlpha, texel.Alpha);
        }

        return new(
            new Rgba8UNorm(minRed, minGreen, minBlue, minAlpha),
            new Rgba8UNorm(maxRed, maxGreen, maxBlue, maxAlpha));
    }

    private static int FindBestInterpolatedWeight(
        Rgba8UNorm texel,
        Rgba8UNorm low,
        Rgba8UNorm high,
        int maxWeight,
        out long bestError)
    {
        var bestWeight = 0;
        bestError = long.MaxValue;
        for (var weight = 0; weight <= maxWeight; weight++)
        {
            var color = Interpolate(low, high, InterpolatedWeights[weight]);
            var error = ColorError(texel, color);
            if (error < bestError)
            {
                bestWeight = weight;
                bestError = error;
            }
        }

        return bestWeight;
    }

    private static Rgba8UNorm Interpolate(Rgba8UNorm low, Rgba8UNorm high, uint weight) => new(
        Interpolate(low.Red, high.Red, weight),
        Interpolate(low.Green, high.Green, weight),
        Interpolate(low.Blue, high.Blue, weight),
        Interpolate(low.Alpha, high.Alpha, weight));

    private static byte Interpolate(byte low, byte high, uint weight)
    {
        var l = (uint)((low << 8) | low);
        var h = (uint)((high << 8) | high);
        var value = ((l * (64 - weight)) + (h * weight) + 32) >> 6;
        return checked((byte)(value >> 8));
    }

    private static long ColorError(Rgba8UNorm actual, Rgba8UNorm expected) =>
        Squared(actual.Red - expected.Red)
        + Squared(actual.Green - expected.Green)
        + Squared(actual.Blue - expected.Blue)
        + Squared(actual.Alpha - expected.Alpha);

    private static void DecodeSrgbColors(Span<Rgba8UNorm> block)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            block[i].Red = RgbaColorConversions.Srgb8ToLinearUNorm8(block[i].Red);
            block[i].Green = RgbaColorConversions.Srgb8ToLinearUNorm8(block[i].Green);
            block[i].Blue = RgbaColorConversions.Srgb8ToLinearUNorm8(block[i].Blue);
        }
    }

    private static void EncodeSrgbColors(Span<Rgba8UNorm> block)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            block[i].Red = RgbaColorConversions.LinearUNorm8ToSrgb8(block[i].Red);
            block[i].Green = RgbaColorConversions.LinearUNorm8ToSrgb8(block[i].Green);
            block[i].Blue = RgbaColorConversions.LinearUNorm8ToSrgb8(block[i].Blue);
        }
    }

    private static long Squared(int value) => (long)value * value;

    private static void WriteBits(Span<byte> destination, ref int bitOffset, int value, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            if (((value >> i) & 1) != 0)
            {
                var absoluteBit = bitOffset + i;
                destination[absoluteBit >> 3] |= checked((byte)(1 << (absoluteBit & 7)));
            }
        }

        bitOffset += bitCount;
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded UASTC LDR 4x4 texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded UASTC LDR 4x4 texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int dimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimension);
        return checked((dimension + 3) / 4);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Basis UASTC LDR 4x4 texture coder does not support texture format '{format.Name}'.");

    private static ReadOnlySpan<uint> InterpolatedWeights => [0, 21, 43, 64];

    private readonly record struct UastcRgbaEndpointPair(Rgba8UNorm Low, Rgba8UNorm High);

    [InlineArray(TexelsPerBlock)]
    private struct Rgba8UNormTexelBlock
    {
        private Rgba8UNorm _element0;
    }

    [InlineArray(UastcEndpointValueCount)]
    private struct UastcEndpointValueBlock
    {
        private byte _element0;
    }

    [InlineArray(UastcWeightValueCount)]
    private struct UastcWeightValueBlock
    {
        private byte _element0;
    }

    [InlineArray(TritQuintValueCount)]
    private struct TritQuintValueBlock
    {
        private uint _element0;
    }

    [InlineArray(EndpointColorCount)]
    private struct UastcEndpointColorBlock
    {
        private Rgba8UNorm _element0;
    }

    [InlineArray(RgbaComponentCount)]
    private struct RgbaComponentBlock
    {
        private byte _element0;
    }

    [InlineArray(BlockColorCount)]
    private struct UastcBlockColorBlock
    {
        private Rgba8UNorm _element0;
    }

    [InlineArray(AstcEndpointOrderValueCount)]
    private struct AstcEndpointOrderValueBlock
    {
        private int _element0;
    }

    private const int ReservedModeIndex = 19;
    private const int SolidColorMode = 8;
    private const int TotalAstcRanges = 21;
    private const int PatternTexelCount = 16;
    private const int PatternAnchorStride = 3;
    private const int Pattern2Count = 30;
    private const int Pattern3Count = 11;
    private const int Mode7Partition2Count = 19;

    private static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        if (source.Length < BytesPerBlock)
        {
            throw new InvalidDataException("UASTC LDR 4x4 block payload must be 16 bytes.");
        }

        if (destination.Length < PatternTexelCount)
        {
            throw new ArgumentException("Destination span must contain at least 16 pixels.", nameof(destination));
        }

        if (!TryDecodeModeIndex(source, out var mode, out var bitOffset) || mode == ReservedModeIndex)
        {
            FillErrorColor(destination);
            return;
        }

        if (mode == SolidColorMode)
        {
            var color = new Rgba8UNorm(
                checked((byte)ReadBits(source, ref bitOffset, 8)),
                checked((byte)ReadBits(source, ref bitOffset, 8)),
                checked((byte)ReadBits(source, ref bitOffset, 8)),
                checked((byte)ReadBits(source, ref bitOffset, 8)));
            destination[..PatternTexelCount].Fill(color);
            return;
        }

        bitOffset += GetHintBitCountBeforePartitionOrEndpoints(mode);

        var subsets = 1;
        var commonPattern = 0;
        switch (mode)
        {
            case 2:
            case 4:
            case 7:
            case 9:
            case 16:
                commonPattern = checked((int)ReadBits(source, ref bitOffset, 5));
                subsets = 2;
                break;
            case 3:
                commonPattern = checked((int)ReadBits(source, ref bitOffset, 4));
                subsets = 3;
                break;
        }

        ValidateCommonPattern(mode, commonPattern);

        var totalPlanes = 1;
        var colorComponentSelector = 0;
        switch (mode)
        {
            case 6:
            case 11:
            case 13:
                colorComponentSelector = checked((int)ReadBits(source, ref bitOffset, 2));
                totalPlanes = 2;
                break;
            case 17:
                colorComponentSelector = 3;
                totalPlanes = 2;
                break;
        }

        var endpointsStorage = new UastcEndpointValueBlock();
        var weightsStorage = new UastcWeightValueBlock();
        Span<byte> endpoints = endpointsStorage;
        Span<byte> weights = weightsStorage;
        UnpackEndpointAndWeightBits(source, ref bitOffset, mode, subsets, commonPattern, totalPlanes, endpoints, weights);
        UnpackPixels(mode, commonPattern, colorComponentSelector, endpoints, weights, destination);
    }

    private static void UnpackEndpointAndWeightBits(
        ReadOnlySpan<byte> source,
        ref int bitOffset,
        int mode,
        int subsets,
        int commonPattern,
        int totalPlanes,
        Span<byte> endpoints,
        Span<byte> weights)
    {
        var totalComponents = GetEndpointComponentCount(mode);
        var totalValues = checked(totalComponents * 2 * subsets);
        var endpointRange = GetAstcEndpointQuantizationRange(mode);
        var endpointBits = AstcIntegerSequenceRanges[endpointRange].Bits;
        var endpointTrits = AstcIntegerSequenceRanges[endpointRange].Trits;
        var endpointQuints = AstcIntegerSequenceRanges[endpointRange].Quints;

        var tqValuesStorage = new TritQuintValueBlock();
        Span<uint> tqValues = tqValuesStorage;
        var totalTqValues = 0;
        var bundleSize = 0;
        var multiplier = 0;
        if (endpointTrits != 0)
        {
            totalTqValues = (totalValues + 4) / 5;
            bundleSize = 5;
            multiplier = 3;
        }
        else if (endpointQuints != 0)
        {
            totalTqValues = (totalValues + 2) / 3;
            bundleSize = 3;
            multiplier = 5;
        }

        for (var i = 0; i < totalTqValues; i++)
        {
            var bitCount = endpointTrits != 0 ? 8 : 7;
            if (i == totalTqValues - 1)
            {
                var remaining = totalValues - ((totalTqValues - 1) * bundleSize);
                bitCount = endpointTrits != 0
                    ? remaining switch
                    {
                        1 => 2,
                        2 => 4,
                        3 => 5,
                        4 => 7,
                        _ => bitCount
                    }
                    : remaining switch
                    {
                        1 => 3,
                        2 => 5,
                        _ => bitCount
                    };
            }

            tqValues[i] = ReadBits(source, ref bitOffset, bitCount);
        }

        uint accum = 0;
        var accumRemaining = 0;
        var nextTqIndex = 0;
        for (var i = 0; i < totalValues; i++)
        {
            var value = ReadBits(source, ref bitOffset, endpointBits);
            if (totalTqValues != 0)
            {
                if (accumRemaining == 0)
                {
                    accum = tqValues[nextTqIndex++];
                    accumRemaining = bundleSize;
                }

                var extra = accum % (uint)multiplier;
                accum /= (uint)multiplier;
                accumRemaining--;
                value |= extra << endpointBits;
            }

            endpoints[i] = checked((byte)value);
        }

        var weightBits = GetWeightBitCount(mode);
        if (mode == 18)
        {
            for (var i = 0; i < PatternTexelCount; i++)
            {
                weights[i] = checked((byte)ReadBits(source, ref bitOffset, i == 0 ? weightBits - 1 : weightBits));
            }

            return;
        }

        if (totalPlanes == 2)
        {
            weights[0] = checked((byte)ReadBits(source, ref bitOffset, weightBits - 1));
            weights[1] = checked((byte)ReadBits(source, ref bitOffset, weightBits - 1));
            for (var i = 2; i < PatternTexelCount * 2; i++)
            {
                weights[i] = checked((byte)ReadBits(source, ref bitOffset, weightBits));
            }

            return;
        }

        if (subsets == 1)
        {
            weights[0] = checked((byte)ReadBits(source, ref bitOffset, weightBits - 1));
            for (var i = 1; i < PatternTexelCount; i++)
            {
                weights[i] = checked((byte)ReadBits(source, ref bitOffset, weightBits));
            }

            return;
        }

        var anchors = GetAnchorIndices(mode, commonPattern);
        for (var i = 0; i < PatternTexelCount; i++)
        {
            var isAnchor = i == anchors[0] || i == anchors[1] || i == anchors[2];
            weights[i] = checked((byte)ReadBits(source, ref bitOffset, isAnchor ? weightBits - 1 : weightBits));
        }
    }

    private static void UnpackPixels(
        int mode,
        int commonPattern,
        int colorComponentSelector,
        ReadOnlySpan<byte> endpointIndices,
        ReadOnlySpan<byte> weights,
        Span<Rgba8UNorm> destination)
    {
        var totalSubsets = GetSubsetCount(mode);
        var totalComponents = Math.Min(4, GetEndpointComponentCount(mode));
        var endpointRange = GetAstcEndpointQuantizationRange(mode);
        var totalPlanes = GetPlaneCount(mode);
        var weightBits = GetWeightBitCount(mode);
        var weightLevels = 1 << weightBits;

        var endpointColorsStorage = new UastcEndpointColorBlock();
        Span<Rgba8UNorm> endpointColors = endpointColorsStorage;
        for (var subset = 0; subset < totalSubsets; subset++)
        {
            if (totalComponents == 2)
            {
                var lowLuma = EndpointUnquantizationTable[endpointRange, endpointIndices[(subset * totalComponents * 2) + 0]];
                var highLuma = EndpointUnquantizationTable[endpointRange, endpointIndices[(subset * totalComponents * 2) + 1]];
                var lowAlpha = EndpointUnquantizationTable[endpointRange, endpointIndices[(subset * totalComponents * 2) + 2]];
                var highAlpha = EndpointUnquantizationTable[endpointRange, endpointIndices[(subset * totalComponents * 2) + 3]];
                endpointColors[(subset * 2) + 0] = new Rgba8UNorm(lowLuma, lowLuma, lowLuma, lowAlpha);
                endpointColors[(subset * 2) + 1] = new Rgba8UNorm(highLuma, highLuma, highLuma, highAlpha);
            }
            else
            {
                var lowStorage = new RgbaComponentBlock();
                var highStorage = new RgbaComponentBlock();
                Span<byte> low = lowStorage;
                Span<byte> high = highStorage;
                low.Fill(255);
                high.Fill(255);
                for (var component = 0; component < totalComponents; component++)
                {
                    low[component] = EndpointUnquantizationTable[endpointRange, endpointIndices[(subset * totalComponents * 2) + (component * 2) + 0]];
                    high[component] = EndpointUnquantizationTable[endpointRange, endpointIndices[(subset * totalComponents * 2) + (component * 2) + 1]];
                }

                endpointColors[(subset * 2) + 0] = new Rgba8UNorm(low[0], low[1], low[2], low[3]);
                endpointColors[(subset * 2) + 1] = new Rgba8UNorm(high[0], high[1], high[2], high[3]);
            }
        }

        var blockColorsStorage = new UastcBlockColorBlock();
        Span<Rgba8UNorm> blockColors = blockColorsStorage;
        for (var subset = 0; subset < totalSubsets; subset++)
        {
            var low = endpointColors[(subset * 2) + 0];
            var high = endpointColors[(subset * 2) + 1];
            for (var weight = 0; weight < weightLevels; weight++)
            {
                blockColors[(subset * 32) + weight] = new Rgba8UNorm(
                    Interpolate(low.Red, high.Red, GetWeight(weightBits, weight)),
                    Interpolate(low.Green, high.Green, GetWeight(weightBits, weight)),
                    Interpolate(low.Blue, high.Blue, GetWeight(weightBits, weight)),
                    Interpolate(low.Alpha, high.Alpha, GetWeight(weightBits, weight)));
            }
        }

        var partitionPattern = totalSubsets >= 2
            ? GetPartitionPattern(mode, commonPattern)
            : ZeroPattern;

        if (totalPlanes == 1)
        {
            for (var i = 0; i < PatternTexelCount; i++)
            {
                var subset = totalSubsets == 1 ? 0 : partitionPattern[i];
                destination[i] = blockColors[(subset * 32) + weights[i]];
            }

            return;
        }

        for (var i = 0; i < PatternTexelCount; i++)
        {
            var weight0 = weights[i * 2];
            var weight1 = weights[(i * 2) + 1];
            var color0 = blockColors[weight0];
            var color1 = blockColors[weight1];
            destination[i] = colorComponentSelector switch
            {
                0 => new Rgba8UNorm(color1.Red, color0.Green, color0.Blue, color0.Alpha),
                1 => new Rgba8UNorm(color0.Red, color1.Green, color0.Blue, color0.Alpha),
                2 => new Rgba8UNorm(color0.Red, color0.Green, color1.Blue, color0.Alpha),
                3 => new Rgba8UNorm(color0.Red, color0.Green, color0.Blue, color1.Alpha),
                _ => throw new InvalidDataException("UASTC LDR 4x4 block has an invalid dual-plane component selector.")
            };
        }
    }

    private static bool TryDecodeModeIndex(ReadOnlySpan<byte> source, out int mode, out int bitOffset)
    {
        var encodedPrefix = source[0] & 0x7f;
        foreach (var encoding in ModeIndexEncodings)
        {
            var mask = (1 << encoding.BitLength) - 1;
            if ((encodedPrefix & mask) == encoding.ModeValue)
            {
                mode = encoding.ModeIndex;
                bitOffset = encoding.BitLength;
                return true;
            }
        }

        mode = ReservedModeIndex;
        bitOffset = 0;
        return false;
    }

    private static void FillErrorColor(Span<Rgba8UNorm> destination) =>
        destination[..PatternTexelCount].Fill(new Rgba8UNorm(255, 0, 255, 255));

    private static int GetHintBitCountBeforePartitionOrEndpoints(int mode) =>
        (HasBc1H0(mode) ? 1 : 0)
        + (HasBc1H1(mode) ? 1 : 0)
        + (IsInterpolatedMode(mode) ? 1 : 0) // ETC1F
        + (IsInterpolatedMode(mode) ? 1 : 0) // ETC1D
        + (IsInterpolatedMode(mode) ? 3 : 0) // ETCI0
        + (IsInterpolatedMode(mode) ? 3 : 0) // ETCI1
        + (HasEtcBaseColorIndex(mode) ? 5 : 0)
        + (HasEtc2Hints(mode) ? 4 : 0) // ETC2T
        + (HasEtc2Hints(mode) ? 4 : 0); // ETC2M

    private static bool IsInterpolatedMode(int mode) => mode is >= 0 and <= 7 or >= 9 and <= 18;

    private static bool HasBc1H0(int mode) => IsInterpolatedMode(mode);

    private static bool HasBc1H1(int mode) => mode is >= 0 and <= 7 or 9 or >= 13 and <= 18;

    private static bool HasEtcBaseColorIndex(int mode) => mode is >= 0 and <= 9 or >= 13 and <= 18;

    private static bool HasEtc2Hints(int mode) => mode is >= 9 and <= 17;

    private static int GetSubsetCount(int mode) =>
        mode switch
        {
            3 => 3,
            2 or 4 or 7 or 9 or 16 => 2,
            SolidColorMode => 0,
            _ => 1
        };

    private static int GetPlaneCount(int mode) =>
        mode switch
        {
            6 or 11 or 13 or 17 => 2,
            SolidColorMode => 0,
            _ => 1
        };

    private static int GetEndpointComponentCount(int mode) =>
        mode switch
        {
            SolidColorMode => 4,
            >= 15 and <= 17 => 2,
            >= 9 and <= 14 => 4,
            _ => 3
        };

    private static int GetEndpointBitCount(int mode) =>
        mode switch
        {
            0 or 12 => 6,
            1 or 5 or 13 or 14 or 15 or 16 or 17 => 8,
            2 or 9 or 10 or 11 => 4,
            3 => 2,
            4 or 7 => 3,
            6 or 18 => 5,
            _ => throw new InvalidDataException("UASTC LDR 4x4 block has an invalid endpoint bit count.")
        };

    private static int GetWeightBitCount(int mode) =>
        mode switch
        {
            13 => 1,
            1 or 3 or 4 or 6 or 7 or 9 or 11 or 14 or 16 or 17 => 2,
            2 or 5 or 12 => 3,
            0 or 10 or 15 => 4,
            18 => 5,
            _ => throw new InvalidDataException("UASTC LDR 4x4 block has an invalid weight bit count.")
        };

    private static int GetAstcEndpointQuantizationRange(int mode)
    {
        var bits = GetEndpointBitCount(mode);
        var trits = mode is 0 or 3 or 10 or 11 or 12 ? 1 : 0;
        var quints = mode is 4 or 6 or 7 ? 1 : 0;
        for (var range = 0; range < AstcIntegerSequenceRanges.Length; range++)
        {
            var candidate = AstcIntegerSequenceRanges[range];
            if (candidate.Bits == bits && candidate.Trits == trits && candidate.Quints == quints)
            {
                return range;
            }
        }

        throw new InvalidDataException("UASTC LDR 4x4 block maps to an unsupported ASTC endpoint quantization range.");
    }

    private static uint ReadBits(ReadOnlySpan<byte> source, ref int bitOffset, int bitCount)
    {
        if ((uint)bitCount > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        if (bitCount == 0)
        {
            return 0;
        }

        if (bitOffset < 0 || bitOffset + bitCount > source.Length * 8)
        {
            throw new InvalidDataException("UASTC LDR 4x4 block ended before all expected bits could be read.");
        }

        uint result = 0;
        for (var i = 0; i < bitCount; i++)
        {
            var absoluteBit = bitOffset + i;
            var bit = (source[absoluteBit >> 3] >> (absoluteBit & 7)) & 1;
            result |= (uint)(bit << i);
        }

        bitOffset += bitCount;
        return result;
    }

    private static void ValidateCommonPattern(int mode, int commonPattern)
    {
        var limit = mode switch
        {
            3 => Pattern3Count,
            7 => Mode7Partition2Count,
            2 or 4 or 9 or 16 => Pattern2Count,
            _ => 1
        };

        if ((uint)commonPattern >= (uint)limit)
        {
            throw new InvalidDataException("UASTC LDR 4x4 block has an invalid common partition pattern.");
        }
    }

    private static ReadOnlySpan<byte> GetPartitionPattern(int mode, int commonPattern) =>
        mode switch
        {
            3 => UastcPartitionPatterns3Subset.AsSpan(commonPattern * PatternTexelCount, PatternTexelCount),
            7 => UastcPartitionPatternsMode7.AsSpan(commonPattern * PatternTexelCount, PatternTexelCount),
            _ => UastcPartitionPatterns2Subset.AsSpan(commonPattern * PatternTexelCount, PatternTexelCount)
        };

    private static ReadOnlySpan<byte> GetAnchorIndices(int mode, int commonPattern) =>
        mode switch
        {
            3 => UastcAnchorIndices3Subset.Slice(commonPattern * PatternAnchorStride, PatternAnchorStride),
            7 => UastcAnchorIndicesMode7.Slice(commonPattern * PatternAnchorStride, PatternAnchorStride),
            _ => UastcAnchorIndices2Subset.Slice(commonPattern * PatternAnchorStride, PatternAnchorStride)
        };

    private static int GetAstcLevelCount(int range) =>
        (1 + (2 * AstcIntegerSequenceRanges[range].Trits) + (4 * AstcIntegerSequenceRanges[range].Quints)) << AstcIntegerSequenceRanges[range].Bits;

    private static byte[,] CreateEndpointUnquantizationTable()
    {
        var result = new byte[TotalAstcRanges, 256];
        var valuesStorage = new AstcEndpointOrderValueBlock();
        Span<int> values = valuesStorage;
        for (var range = 0; range < TotalAstcRanges; range++)
        {
            if (!IsValidEndpointRange(range))
            {
                continue;
            }

            var levels = GetAstcLevelCount(range);
            for (var i = 0; i < levels; i++)
            {
                values[i] = (UnquantAstcEndpointValue(i, range) << 8) | i;
            }

            values[..levels].Sort();
            for (var i = 0; i < levels; i++)
            {
                var packed = values[i];
                var order = packed & 0xff;
                var unquantized = packed >> 8;
                result[range, order] = checked((byte)unquantized);
            }
        }

        return result;
    }

    private static bool IsValidEndpointRange(int range) =>
        (AstcIntegerSequenceRanges[range].Trits == 0 && AstcIntegerSequenceRanges[range].Quints == 0)
        || AstcEndpointUnquantizationCByRange[range] != 0;

    private static int UnquantAstcEndpointValue(int packedValue, int range)
    {
        var bits = AstcIntegerSequenceRanges[range].Bits;
        var trits = AstcIntegerSequenceRanges[range].Trits;
        var quints = AstcIntegerSequenceRanges[range].Quints;
        return trits == 0 && quints == 0
            ? UnquantizeAstcEndpointValueFromTable172(packedValue, 0, 0, range)
            : trits != 0
                ? UnquantizeAstcEndpointValueFromTable172(packedValue & ((1 << bits) - 1), packedValue >> bits, 0, range)
                : UnquantizeAstcEndpointValueFromTable172(packedValue & ((1 << bits) - 1), 0, packedValue >> bits, range);
    }

    private static int UnquantizeAstcEndpointValueFromTable172(int packedBits, int packedTrits, int packedQuints, int range)
    {
        var bits = AstcIntegerSequenceRanges[range].Bits;
        var trits = AstcIntegerSequenceRanges[range].Trits;
        var quints = AstcIntegerSequenceRanges[range].Quints;
        if (trits == 0 && quints == 0)
        {
            var value = 0;
            var bitsLeft = 8;
            while (bitsLeft > 0)
            {
                var v = packedBits;
                var count = Math.Min(bitsLeft, bits);
                if (count < bits)
                {
                    v >>= bits - count;
                }

                value |= v << (bitsLeft - count);
                bitsLeft -= count;
            }

            return value;
        }

        var a = (packedBits & 1) != 0 ? 511 : 0;
        var c = AstcEndpointUnquantizationCByRange[range];
        var d = trits != 0 ? packedTrits : packedQuints;
        var b = 0;
        var bString = AstcEndpointUnquantizationBLayoutByRange[range];
        for (var i = 0; i < 9; i++)
        {
            b <<= 1;
            var ch = bString[i];
            if (ch != '0')
            {
                b |= (packedBits >> (ch - 'a')) & 1;
            }
        }

        var valueWithA = ((d * c) + b) ^ a;
        return (a & 0x80) | (valueWithA >> 2);
    }

    private static uint GetWeight(int weightBits, int index) =>
        weightBits switch
        {
            1 => Bc7Weights1[index],
            2 => Bc7Weights2[index],
            3 => Bc7Weights3[index],
            4 => AstcWeights4[index],
            5 => AstcWeights5[index],
            _ => throw new InvalidDataException("UASTC LDR 4x4 block has an invalid weight bit count.")
        };

    private static readonly UastcModeIndexEncoding[] ModeIndexEncodings =
    [
        // KDF 1.4 Table 199: UASTC Mode Index encoding. Values are matched LSB-first.
        new(0x00, 2, 11),
        new(0x02, 3, 10),
        new(0x06, 3, 12),
        new(0x01, 4, 0),
        new(0x09, 4, 18),
        new(0x03, 5, 3),
        new(0x07, 5, 7),
        new(0x0b, 5, 5),
        new(0x0d, 5, 14),
        new(0x0f, 5, 9),
        new(0x13, 5, 4),
        new(0x17, 5, 8),
        new(0x1b, 5, 6),
        new(0x1d, 5, 2),
        new(0x1f, 5, 13),
        new(0x15, 6, 16),
        new(0x25, 6, 17),
        new(0x35, 6, 1),
        new(0x05, 7, 15),
        new(0x45, 7, ReservedModeIndex)
    ];

    private static readonly AstcIntegerSequenceRange[] AstcIntegerSequenceRanges =
    [
        // KDF 1.4 Table 172: ASTC color unquantization parameters, reduced to #bits/#trits/#quints.
        new(1, 0, 0), new(0, 1, 0), new(2, 0, 0), new(0, 0, 1), new(1, 1, 0),
        new(3, 0, 0), new(1, 0, 1), new(2, 1, 0), new(4, 0, 0), new(2, 0, 1),
        new(3, 1, 0), new(5, 0, 0), new(3, 0, 1), new(4, 1, 0), new(6, 0, 0),
        new(4, 0, 1), new(5, 1, 0), new(7, 0, 0), new(5, 0, 1), new(6, 1, 0),
        new(8, 0, 0)
    ];

    private static readonly string[] AstcEndpointUnquantizationBLayoutByRange =
    [
        // KDF 1.4 Table 172: ASTC endpoint unquantization B bit layout.
        "", "", "", "", "000000000", "", "000000000", "b000b0bb0", "", "b0000bb00",
        "cb000cbcb", "", "cb0000cbc", "dcb000dcb", "", "dcb0000dc", "edcb000ed",
        "", "edcb0000e", "fedcb000f", ""
    ];

    private static ReadOnlySpan<int> AstcEndpointUnquantizationCByRange =>
    [
        // KDF 1.4 Table 172: ASTC endpoint unquantization C value.
        0, 0, 0, 0, 204, 0, 113, 93, 0, 54, 44, 0, 26, 22, 0, 13, 11, 0, 6, 5, 0
    ];

    private static readonly byte[,] EndpointUnquantizationTable = CreateEndpointUnquantizationTable();

    private readonly record struct UastcModeIndexEncoding(int ModeValue, int BitLength, int ModeIndex);

    private readonly record struct AstcIntegerSequenceRange(byte Bits, byte Trits, byte Quints);

    private static ReadOnlySpan<uint> Bc7Weights1 => [0, 64];
    private static ReadOnlySpan<uint> Bc7Weights2 => [0, 21, 43, 64];
    private static ReadOnlySpan<uint> Bc7Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];
    private static ReadOnlySpan<uint> AstcWeights4 => [0, 4, 8, 12, 17, 21, 25, 29, 35, 39, 43, 47, 52, 56, 60, 64];
    private static ReadOnlySpan<uint> AstcWeights5 => [0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 62, 64];

    private static ReadOnlySpan<byte> ZeroPattern => [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static readonly byte[] UastcPartitionPatterns2Subset =
    [
        // KDF 1.4 Table 225: UASTC 2-subset partition table for modes 2, 4, 9, and 16.
        0,0,1,1,0,0,1,1,0,0,1,1,0,0,1,1, 0,0,0,1,0,0,0,1,0,0,0,1,0,0,0,1,
        1,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0, 0,0,0,1,0,0,1,1,0,0,1,1,0,1,1,1,
        1,1,1,1,1,1,1,0,1,1,1,0,1,1,0,0, 0,0,1,1,0,1,1,1,0,1,1,1,1,1,1,1,
        1,1,1,0,1,1,0,0,1,0,0,0,0,0,0,0, 1,1,1,1,1,1,1,0,1,1,0,0,1,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,1,0,0,1,1, 1,1,0,0,1,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,1,0,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,1,1,1,0,1,0,0,0,
        1,1,1,0,1,0,0,0,0,0,0,0,0,0,0,0, 1,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,
        0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0,
        1,0,0,0,1,1,1,0,1,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,0,1,1,1,0,0,0,1,
        0,1,1,1,0,0,1,1,0,0,0,1,0,0,0,0, 0,0,1,1,0,0,0,1,0,0,0,0,0,0,0,0,
        0,0,0,0,1,0,0,0,1,1,0,0,1,1,1,0, 1,1,1,1,1,1,1,1,0,1,1,1,0,0,1,1,
        1,0,0,0,1,1,0,0,1,1,0,0,1,1,1,0, 0,0,1,1,0,0,0,1,0,0,0,1,0,0,0,0,
        1,1,1,1,0,1,1,1,0,1,1,1,0,0,1,1, 0,1,1,0,0,1,1,0,0,1,1,0,0,1,1,0,
        1,1,1,1,0,0,0,0,0,0,0,0,1,1,1,1, 1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,
        1,1,1,1,0,0,0,0,1,1,1,1,0,0,0,0, 1,0,0,1,0,0,1,1,0,1,1,0,1,1,0,0
    ];

    private static readonly byte[] UastcPartitionPatterns3Subset =
    [
        // KDF 1.4 Table 226: UASTC 3-subset partition table for mode 3.
        0,0,0,0,0,0,0,0,1,1,2,2,1,1,2,2, 1,1,1,1,1,1,1,1,0,0,0,0,2,2,2,2,
        1,1,1,1,0,0,0,0,0,0,0,0,2,2,2,2, 1,1,1,1,2,2,2,2,0,0,0,0,0,0,0,0,
        1,1,2,0,1,1,2,0,1,1,2,0,1,1,2,0, 0,1,1,2,0,1,1,2,0,1,1,2,0,1,1,2,
        0,2,1,1,0,2,1,1,0,2,1,1,0,2,1,1, 2,0,0,0,2,0,0,0,2,1,1,1,2,1,1,1,
        2,0,1,2,2,0,1,2,2,0,1,2,2,0,1,2, 1,1,1,1,0,0,0,0,2,2,2,2,1,1,1,1,
        0,0,2,2,0,0,1,1,0,0,1,1,0,0,2,2
    ];

    private static readonly byte[] UastcPartitionPatternsMode7 =
    [
        // KDF 1.4 Table 224: UASTC 2-subset partition table for mode 7.
        0,0,0,0,1,1,1,1,0,0,0,0,0,0,0,0, 0,0,1,0,0,0,1,0,0,0,1,0,0,0,1,0,
        1,1,0,0,1,1,0,0,1,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,1,0,0,1,1,0,0,1,1,
        1,1,1,1,1,1,1,1,0,0,0,0,1,1,1,1, 0,1,0,0,0,1,0,0,0,1,0,0,0,1,0,0,
        0,0,0,1,0,0,1,1,1,1,1,1,1,1,1,1, 0,1,1,1,0,0,1,1,0,0,1,1,0,0,1,1,
        1,1,0,0,0,0,0,0,0,0,1,1,1,1,0,0, 0,1,1,1,0,1,1,1,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,1,1,1,0,1,1,1,0, 1,1,0,0,0,0,0,0,0,0,0,0,1,1,0,0,
        0,1,1,1,0,0,1,1,0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,1,
        1,1,1,1,1,1,1,1,1,1,1,1,0,1,1,0, 1,1,0,0,1,1,0,0,1,1,0,0,1,0,0,0,
        1,1,1,1,1,1,1,1,1,0,0,0,1,0,0,0, 0,0,1,1,0,1,1,0,1,1,0,0,1,0,0,0,
        1,1,1,1,0,1,1,1,0,0,0,0,0,0,0,0
    ];

    private static ReadOnlySpan<byte> UastcAnchorIndices2Subset =>
    [
        // KDF 1.4 Table 206: UASTC anchor indices for modes 2, 4, 9, and 16.
        0,2,0, 0,3,0, 1,0,0, 0,3,0, 7,0,0, 0,2,0, 3,0,0, 7,0,0,
        0,11,0, 2,0,0, 0,7,0, 11,0,0, 3,0,0, 8,0,0, 0,4,0, 12,0,0,
        1,0,0, 8,0,0, 0,1,0, 0,2,0, 0,4,0, 8,0,0, 1,0,0, 0,2,0,
        4,0,0, 0,1,0, 4,0,0, 1,0,0, 4,0,0, 1,0,0
    ];

    private static ReadOnlySpan<byte> UastcAnchorIndices3Subset =>
    [
        // KDF 1.4 Table 207: UASTC anchor indices for mode 3.
        0,8,10, 8,0,12, 4,0,12, 8,0,4, 3,0,2, 0,1,3, 0,2,1, 1,9,0, 1,2,0, 4,0,8, 0,6,2
    ];

    private static ReadOnlySpan<byte> UastcAnchorIndicesMode7 =>
    [
        // KDF 1.4 Table 208: UASTC anchor indices for mode 7.
        0,4,0, 0,2,0, 2,0,0, 0,7,0, 8,0,0, 0,1,0, 0,3,0, 0,1,0, 2,0,0, 0,1,0,
        0,8,0, 2,0,0, 0,1,0, 0,7,0, 12,0,0, 2,0,0, 9,0,0, 0,2,0, 4,0,0
    ];
}
