using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class S3tcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;
    private const byte AlphaCutoff = 128;

    private readonly DxtFormat _dxtFormat;
    private readonly bool _isSrgb;

    public S3tcTextureCoder(TextureFormat format)
    {
        if (!TryGetDxtFormat(format, out _dxtFormat))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _isSrgb = format.ValueKind == TextureValueKind.Srgb;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetDxtFormat(format, out _);

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
                DecodeBlock(source.Slice(blockOffset, bytesPerBlock), block);
                StoreBlock(block, blockX, blockY, destination);
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

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
                LoadBlock(source, blockX, blockY, block);
                EncodeBlock(block, destination.Slice(blockOffset, bytesPerBlock));
                blockOffset = checked(blockOffset + bytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        switch (_dxtFormat)
        {
            case DxtFormat.Dxt1Rgb:
                DecodeColorBlock(source, Dxt1ColorMode.Rgb, destination);
                break;
            case DxtFormat.Dxt1Rgba:
                DecodeColorBlock(source, Dxt1ColorMode.Rgba, destination);
                break;
            case DxtFormat.Dxt2Rgba:
                DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
                DecodeExplicitAlphaBlock(source[..8], destination);
                RecoverPremultipliedAlpha(destination);
                break;
            case DxtFormat.Dxt3Rgba:
                DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
                DecodeExplicitAlphaBlock(source[..8], destination);
                break;
            case DxtFormat.Dxt4Rgba:
                DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
                DecodeInterpolatedAlphaBlock(source[..8], destination);
                RecoverPremultipliedAlpha(destination);
                break;
            case DxtFormat.Dxt5Rgba:
                DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
                DecodeInterpolatedAlphaBlock(source[..8], destination);
                break;
            default:
                throw CreateUnsupportedFormatException(Format);
        }

        if (_isSrgb)
        {
            DecodeSrgbColors(destination);
        }
    }

    private void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        if (_isSrgb)
        {
            Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            EncodeSrgbColors(source, srgbBlock);
            EncodeBlockCore(srgbBlock, destination);
            return;
        }

        EncodeBlockCore(source, destination);
    }

    private void EncodeBlockCore(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        switch (_dxtFormat)
        {
            case DxtFormat.Dxt1Rgb:
                EncodeColorBlock(source, Dxt1ColorMode.Rgb, destination);
                return;
            case DxtFormat.Dxt1Rgba:
                EncodeColorBlock(source, Dxt1ColorMode.Rgba, destination);
                return;
            case DxtFormat.Dxt2Rgba:
                Span<Rgba8UNorm> dxt2Block = stackalloc Rgba8UNorm[TexelsPerBlock];
                PremultiplyAlpha(source, dxt2Block);
                EncodeExplicitAlphaBlock(source, destination[..8]);
                EncodeColorBlock(dxt2Block, Dxt1ColorMode.FourColor, destination[8..]);
                return;
            case DxtFormat.Dxt3Rgba:
                EncodeExplicitAlphaBlock(source, destination[..8]);
                EncodeColorBlock(source, Dxt1ColorMode.FourColor, destination[8..]);
                return;
            case DxtFormat.Dxt4Rgba:
                Span<Rgba8UNorm> dxt4Block = stackalloc Rgba8UNorm[TexelsPerBlock];
                PremultiplyAlpha(source, dxt4Block);
                EncodeInterpolatedAlphaBlock(source, destination[..8]);
                EncodeColorBlock(dxt4Block, Dxt1ColorMode.FourColor, destination[8..]);
                return;
            case DxtFormat.Dxt5Rgba:
                EncodeInterpolatedAlphaBlock(source, destination[..8]);
                EncodeColorBlock(source, Dxt1ColorMode.FourColor, destination[8..]);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void DecodeColorBlock(
        ReadOnlySpan<byte> source,
        Dxt1ColorMode colorMode,
        Span<Rgba8UNorm> destination)
    {
        var color0 = BinaryPrimitives.ReadUInt16LittleEndian(source);
        var color1 = BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);
        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildColorPalette(color0, color1, colorMode, palette);

        var indices = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = palette[(int)((indices >> (i * 2)) & 0x3u)];
        }
    }

    private static void EncodeColorBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        Span<byte> destination)
    {
        var hasTransparent = colorMode == Dxt1ColorMode.Rgba && HasTransparentTexel(source);
        FindColorBounds(source, ignoreTransparent: hasTransparent, out var min, out var max);

        ushort color0;
        ushort color1;
        if (hasTransparent)
        {
            color0 = PackRgb565(min);
            color1 = PackRgb565(max);
            if (color0 > color1)
            {
                (color0, color1) = (color1, color0);
            }
        }
        else
        {
            color0 = PackRgb565(max);
            color1 = PackRgb565(min);
            if (color0 < color1)
            {
                (color0, color1) = (color1, color0);
            }
        }

        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildColorPalette(color0, color1, colorMode, palette);

        uint indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = hasTransparent && source[i].Alpha < AlphaCutoff
                ? 3
                : FindNearestColorIndex(source[i], palette, hasTransparent ? 3 : 4);
            indices |= (uint)index << (i * 2);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(destination, color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], indices);
    }

    private static void DecodeExplicitAlphaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var packed = source[i >> 1];
            var alpha4 = (i & 1) == 0 ? packed & 0x0f : packed >> 4;
            destination[i].Alpha = (byte)((alpha4 << 4) | alpha4);
        }
    }

    private static void EncodeExplicitAlphaBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        for (var i = 0; i < 8; i++)
        {
            var low = source[i * 2].Alpha >> 4;
            var high = source[(i * 2) + 1].Alpha >> 4;
            destination[i] = (byte)(low | (high << 4));
        }
    }

    private static void DecodeInterpolatedAlphaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        Span<byte> palette = stackalloc byte[8];
        BuildAlphaPalette(source[0], source[1], palette);

        var indices = ReadAlphaIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i].Alpha = palette[(int)((indices >> (i * 3)) & 0x7u)];
        }
    }

    private static void EncodeInterpolatedAlphaBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        FindAlphaBounds(source, out var min, out var max);
        destination[0] = max;
        destination[1] = min;

        Span<byte> palette = stackalloc byte[8];
        BuildAlphaPalette(max, min, palette);

        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            indices |= (ulong)FindNearestAlphaIndex(source[i].Alpha, palette) << (i * 3);
        }

        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(indices >> (8 * i));
        }
    }

    private static void BuildColorPalette(
        ushort color0,
        ushort color1,
        Dxt1ColorMode colorMode,
        Span<Rgba8UNorm> palette)
    {
        var c0 = UnpackRgb565(color0);
        var c1 = UnpackRgb565(color1);
        palette[0] = new Rgba8UNorm(c0.Red, c0.Green, c0.Blue);
        palette[1] = new Rgba8UNorm(c1.Red, c1.Green, c1.Blue);

        if (colorMode == Dxt1ColorMode.FourColor || color0 > color1)
        {
            palette[2] = Interpolate(c0, c1, 2, 1, 3);
            palette[3] = Interpolate(c0, c1, 1, 2, 3);
        }
        else
        {
            palette[2] = Interpolate(c0, c1, 1, 1, 2);
            palette[3] = colorMode == Dxt1ColorMode.Rgb
                ? new Rgba8UNorm(0, 0, 0, 255)
                : new Rgba8UNorm(0, 0, 0, 0);
        }
    }

    private static Rgba8UNorm Interpolate(Rgb24 a, Rgb24 b, int weightA, int weightB, int divisor)
    {
        var bias = divisor == 3 ? 1 : 0;
        return new Rgba8UNorm(
            (byte)(((weightA * a.Red) + (weightB * b.Red) + bias) / divisor),
            (byte)(((weightA * a.Green) + (weightB * b.Green) + bias) / divisor),
            (byte)(((weightA * a.Blue) + (weightB * b.Blue) + bias) / divisor));
    }

    private static Rgb24 UnpackRgb565(ushort value)
    {
        var red = (value >> 11) & 0x1f;
        var green = (value >> 5) & 0x3f;
        var blue = value & 0x1f;
        return new Rgb24(
            (byte)((red << 3) | (red >> 2)),
            (byte)((green << 2) | (green >> 4)),
            (byte)((blue << 3) | (blue >> 2)));
    }

    private static ushort PackRgb565(Rgb24 value)
    {
        var red = value.Red >> 3;
        var green = value.Green >> 2;
        var blue = value.Blue >> 3;
        return (ushort)((red << 11) | (green << 5) | blue);
    }

    private static void BuildAlphaPalette(byte alpha0, byte alpha1, Span<byte> palette)
    {
        palette[0] = alpha0;
        palette[1] = alpha1;

        if (alpha0 > alpha1)
        {
            palette[2] = (byte)(((6 * alpha0) + alpha1) / 7);
            palette[3] = (byte)(((5 * alpha0) + (2 * alpha1)) / 7);
            palette[4] = (byte)(((4 * alpha0) + (3 * alpha1)) / 7);
            palette[5] = (byte)(((3 * alpha0) + (4 * alpha1)) / 7);
            palette[6] = (byte)(((2 * alpha0) + (5 * alpha1)) / 7);
            palette[7] = (byte)((alpha0 + (6 * alpha1)) / 7);
        }
        else
        {
            palette[2] = (byte)(((4 * alpha0) + alpha1) / 5);
            palette[3] = (byte)(((3 * alpha0) + (2 * alpha1)) / 5);
            palette[4] = (byte)(((2 * alpha0) + (3 * alpha1)) / 5);
            palette[5] = (byte)((alpha0 + (4 * alpha1)) / 5);
            palette[6] = 0;
            palette[7] = 255;
        }
    }

    private static ulong ReadAlphaIndices(ReadOnlySpan<byte> source)
    {
        ulong indices = 0;
        for (var i = 0; i < 6; i++)
        {
            indices |= (ulong)source[2 + i] << (8 * i);
        }

        return indices;
    }

    private static void FindColorBounds(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out Rgb24 min,
        out Rgb24 max)
    {
        var minRed = byte.MaxValue;
        var minGreen = byte.MaxValue;
        var minBlue = byte.MaxValue;
        var maxRed = byte.MinValue;
        var maxGreen = byte.MinValue;
        var maxBlue = byte.MinValue;
        var found = false;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            minRed = Math.Min(minRed, source[i].Red);
            minGreen = Math.Min(minGreen, source[i].Green);
            minBlue = Math.Min(minBlue, source[i].Blue);
            maxRed = Math.Max(maxRed, source[i].Red);
            maxGreen = Math.Max(maxGreen, source[i].Green);
            maxBlue = Math.Max(maxBlue, source[i].Blue);
            found = true;
        }

        min = found
            ? new Rgb24(minRed, minGreen, minBlue)
            : new Rgb24(0, 0, 0);
        max = found
            ? new Rgb24(maxRed, maxGreen, maxBlue)
            : new Rgb24(0, 0, 0);
    }

    private static void FindAlphaBounds(ReadOnlySpan<Rgba8UNorm> source, out byte min, out byte max)
    {
        min = byte.MaxValue;
        max = byte.MinValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            min = Math.Min(min, source[i].Alpha);
            max = Math.Max(max, source[i].Alpha);
        }
    }

    private static int FindNearestColorIndex(Rgba8UNorm color, ReadOnlySpan<Rgba8UNorm> palette, int paletteCount)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < paletteCount; i++)
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

    private static int FindNearestAlphaIndex(byte alpha, ReadOnlySpan<byte> palette)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var distance = Math.Abs(alpha - palette[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool HasTransparentTexel(ReadOnlySpan<Rgba8UNorm> source)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (source[i].Alpha < AlphaCutoff)
            {
                return true;
            }
        }

        return false;
    }

    private static void DecodeSrgbColors(Span<Rgba8UNorm> block)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            block[i].Red = DecodeSrgb(block[i].Red);
            block[i].Green = DecodeSrgb(block[i].Green);
            block[i].Blue = DecodeSrgb(block[i].Blue);
        }
    }

    private static void EncodeSrgbColors(ReadOnlySpan<Rgba8UNorm> source, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba8UNorm(
                EncodeSrgb(source[i].Red),
                EncodeSrgb(source[i].Green),
                EncodeSrgb(source[i].Blue),
                source[i].Alpha);
        }
    }

    private static byte DecodeSrgb(byte value) =>
        RgbaColorConversions.FloatToUNorm8(RgbaColorConversions.Srgb8ToLinearFloat(value));

    private static byte EncodeSrgb(byte value) =>
        RgbaColorConversions.LinearFloatToSrgb8(RgbaColorConversions.UNorm8ToFloat(value));

    private static void PremultiplyAlpha(ReadOnlySpan<Rgba8UNorm> source, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var alpha = source[i].Alpha;
            destination[i] = new Rgba8UNorm(
                PremultiplyChannel(source[i].Red, alpha),
                PremultiplyChannel(source[i].Green, alpha),
                PremultiplyChannel(source[i].Blue, alpha),
                alpha);
        }
    }

    private static byte PremultiplyChannel(byte value, byte alpha) => (byte)((value * alpha) / byte.MaxValue);

    private static void RecoverPremultipliedAlpha(Span<Rgba8UNorm> block)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var alpha = block[i].Alpha;
            if (alpha == 0)
            {
                block[i].Red = 0;
                block[i].Green = 0;
                block[i].Blue = 0;
                continue;
            }

            block[i].Red = RecoverPremultipliedChannel(block[i].Red, alpha);
            block[i].Green = RecoverPremultipliedChannel(block[i].Green, alpha);
            block[i].Blue = RecoverPremultipliedChannel(block[i].Blue, alpha);
        }
    }

    private static byte RecoverPremultipliedChannel(byte value, byte alpha)
    {
        var recovered = value * byte.MaxValue / alpha;
        return (byte)Math.Min(recovered, byte.MaxValue);
    }

    private static void LoadBlock<TPixel>(ImageView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination)
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

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded S3TC texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded S3TC texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size) => (size + BlockSize - 1) / BlockSize;

    private static bool TryGetDxtFormat(TextureFormat format, out DxtFormat dxtFormat)
    {
        if (format == TextureFormats.Bc1Rgb
            || format == TextureFormats.Bc1RgbSrgb
            || format == TextureFormats.Dxt1Rgb
            || format == TextureFormats.Dxt1RgbSrgb)
        {
            dxtFormat = DxtFormat.Dxt1Rgb;
            return true;
        }

        if (format == TextureFormats.Bc1Rgba
            || format == TextureFormats.Bc1RgbaSrgb
            || format == TextureFormats.Dxt1Rgba
            || format == TextureFormats.Dxt1RgbaSrgb)
        {
            dxtFormat = DxtFormat.Dxt1Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt2Rgba)
        {
            dxtFormat = DxtFormat.Dxt2Rgba;
            return true;
        }

        if (format == TextureFormats.Bc2Rgba
            || format == TextureFormats.Bc2RgbaSrgb
            || format == TextureFormats.Dxt3Rgba
            || format == TextureFormats.Dxt3RgbaSrgb)
        {
            dxtFormat = DxtFormat.Dxt3Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt4Rgba)
        {
            dxtFormat = DxtFormat.Dxt4Rgba;
            return true;
        }

        if (format == TextureFormats.Bc3Rgba
            || format == TextureFormats.Bc3RgbaSrgb
            || format == TextureFormats.Dxt5Rgba
            || format == TextureFormats.Dxt5RgbaSrgb)
        {
            dxtFormat = DxtFormat.Dxt5Rgba;
            return true;
        }

        dxtFormat = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"S3TC texture coder does not support texture format '{format.Name}'.");

    private readonly record struct Rgb24(byte Red, byte Green, byte Blue);

    private enum DxtFormat
    {
        Dxt1Rgb,
        Dxt1Rgba,
        Dxt2Rgba,
        Dxt3Rgba,
        Dxt4Rgba,
        Dxt5Rgba
    }

    private enum Dxt1ColorMode
    {
        Rgb,
        Rgba,
        FourColor
    }
}
