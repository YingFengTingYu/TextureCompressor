using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;

namespace TextureCompressor.Codecs;

public sealed class AtcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private readonly AtcTransfer _transfer;

    public AtcTextureCoder(TextureFormat format)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
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
                Encode<TPixel, AtcRgbTransfer>(source, destination, rowPitch);
                return;
            case AtcTransfer.RgbaExplicitAlpha:
                Encode<TPixel, AtcRgbaExplicitAlphaTransfer>(source, destination, rowPitch);
                return;
            case AtcTransfer.RgbaInterpolatedAlpha:
                Encode<TPixel, AtcRgbaInterpolatedAlphaTransfer>(source, destination, rowPitch);
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

    private static void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IAtcTransfer
    {
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);
        Span<Rgba8UNorm> block = stackalloc Rgba8UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock));
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IAtcTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination);
    }

    private readonly struct AtcRgbTransfer : IAtcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeColorBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeColorBlock(source, destination);
    }

    private readonly struct AtcRgbaExplicitAlphaTransfer : IAtcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], destination);
            DecodeExplicitAlphaBlock(source[..8], destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            EncodeExplicitAlphaBlock(source, destination[..8]);
            EncodeColorBlock(source, destination[8..]);
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

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            EncodeInterpolatedAlphaBlock(source, destination[..8]);
            EncodeColorBlock(source, destination[8..]);
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

    private static void EncodeColorBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
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
            var low = QuantizeExplicitAlpha(source[i * 2].Alpha);
            var high = QuantizeExplicitAlpha(source[(i * 2) + 1].Alpha);
            destination[i] = (byte)(low | (high << 4));
        }
    }

    private static void DecodeInterpolatedAlphaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var palette = new InlineArray8<byte>();
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

        var bestError = int.MaxValue;
        byte bestAlpha0 = max;
        byte bestAlpha1 = min;
        ulong bestIndices = 0;
        TryEncodeInterpolatedAlphaCandidate(source, max, min, ref bestError, ref bestAlpha0, ref bestAlpha1, ref bestIndices);
        TryEncodeInterpolatedAlphaCandidate(source, min, max, ref bestError, ref bestAlpha0, ref bestAlpha1, ref bestIndices);

        destination[0] = bestAlpha0;
        destination[1] = bestAlpha1;

        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(bestIndices >> (8 * i));
        }
    }

    private static void TryEncodeInterpolatedAlphaCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        byte alpha0,
        byte alpha1,
        ref int bestError,
        ref byte bestAlpha0,
        ref byte bestAlpha1,
        ref ulong bestIndices)
    {
        var palette = new InlineArray8<byte>();
        BuildAlphaPalette(alpha0, alpha1, palette);

        var error = 0;
        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = FindNearestAlphaIndex(source[i].Alpha, palette, out var distance);
            error += distance;
            if (error >= bestError)
            {
                return;
            }

            indices |= (ulong)index << (i * 3);
        }

        bestError = error;
        bestAlpha0 = alpha0;
        bestAlpha1 = alpha1;
        bestIndices = indices;
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

    private static void BuildAlphaPalette(byte alpha0, byte alpha1, Span<byte> palette)
    {
        palette[0] = alpha0;
        palette[1] = alpha1;

        if (alpha0 > alpha1)
        {
            palette[2] = (byte)(((6 * alpha0) + alpha1 + 3) / 7);
            palette[3] = (byte)(((5 * alpha0) + (2 * alpha1) + 3) / 7);
            palette[4] = (byte)(((4 * alpha0) + (3 * alpha1) + 3) / 7);
            palette[5] = (byte)(((3 * alpha0) + (4 * alpha1) + 3) / 7);
            palette[6] = (byte)(((2 * alpha0) + (5 * alpha1) + 3) / 7);
            palette[7] = (byte)((alpha0 + (6 * alpha1) + 3) / 7);
        }
        else
        {
            palette[2] = (byte)(((4 * alpha0) + alpha1 + 2) / 5);
            palette[3] = (byte)(((3 * alpha0) + (2 * alpha1) + 2) / 5);
            palette[4] = (byte)(((2 * alpha0) + (3 * alpha1) + 2) / 5);
            palette[5] = (byte)((alpha0 + (4 * alpha1) + 2) / 5);
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

    private static int FindNearestAlphaIndex(byte alpha, ReadOnlySpan<byte> palette, out int bestDistance)
    {
        var bestIndex = 0;
        bestDistance = int.MaxValue;
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

    private static int QuantizeExplicitAlpha(byte alpha)
    {
        var alpha4 = alpha >> 4;
        var quantized = (alpha + (alpha4 < 8 ? 7 : 8) - alpha4) >> 4;
        return Math.Min(quantized, 0x0f);
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

    private enum AtcTransfer
    {
        Rgb,
        RgbaExplicitAlpha,
        RgbaInterpolatedAlpha
    }
}
