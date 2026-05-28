using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class FxtcTextureCoder : IPitchTextureCoder
{
    private const int BlockWidth = 8;
    private const int BlockHeight = 4;
    private const int TexelsPerBlock = BlockWidth * BlockHeight;
    private const int BytesPerBlock = 16;
    private const int LeftTexelCount = 16;
    private const byte AlphaCutoff = 128;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.RgbFxt1UNorm,
        TextureFormats.RgbaFxt1UNorm
    ];

    public FxtcTextureCoder(TextureFormat format)
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

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
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

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var blockCountX = GetBlockCountX(source.Width);
        var blockCountY = GetBlockCountY(source.Height);
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

    private static ushort PackRgb555(Rgba8UNorm value) =>
        (ushort)((Quantize5(value.Red) << 10) | (Quantize5(value.Green) << 5) | Quantize5(value.Blue));

    private static ushort PackRgb565WithoutGreenLowBit(Rgba8UNorm value)
    {
        var green6 = Quantize6(value.Green);
        return (ushort)((Quantize5(value.Red) << 10) | ((green6 >> 1) << 5) | Quantize5(value.Blue));
    }

    private static int Quantize5(byte value) => (value * 31 + 127) / 255;

    private static int Quantize6(byte value) => (value * 63 + 127) / 255;

    private static byte QuantizeAlpha5(byte value) => Expand5To8(Quantize5(value));

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

    private static void LoadBlock<TPixel>(ImageView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
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
        ImageView<TPixel> destination)
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
}
