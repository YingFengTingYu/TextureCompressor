using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedYuv422TextureCoder : IPitchTextureCoder
{
    private readonly PackedYuv422Transfer _transfer;

    public PackedYuv422TextureCoder(TextureFormat format)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetTransfer(format, out _);

    public int GetDefaultPitch(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return Is8BitPacked(_transfer)
            ? Format.GetRowByteCount(width)
            : GetHighBitRowByteCount(width);
    }

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);
        if (Is8BitPacked(_transfer))
        {
            ValidatePacked8BitWidth(width);
        }

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed YUV row byte count.");
        }

        return checked(rowPitch * height);
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
            case PackedYuv422Transfer.Uyvy8:
                Decode<TPixel, Uyvy8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy8BigEndian:
                Decode<TPixel, Uyvy8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv8:
                Decode<TPixel, Yuyv8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv8BigEndian:
                Decode<TPixel, Yuyv8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv16:
                Decode<TPixel, Yuyv16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy16:
                Decode<TPixel, Uyvy16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Msb:
                Decode<TPixel, Yuyv10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Lsb:
                Decode<TPixel, Yuyv10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Msb:
                Decode<TPixel, Uyvy10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Lsb:
                Decode<TPixel, Uyvy10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Msb:
                Decode<TPixel, Yuyv12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Lsb:
                Decode<TPixel, Yuyv12LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Msb:
                Decode<TPixel, Uyvy12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Lsb:
                Decode<TPixel, Uyvy12LsbTransfer>(source, destination, rowPitch);
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
            case PackedYuv422Transfer.Uyvy8:
                Encode<TPixel, Uyvy8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy8BigEndian:
                Encode<TPixel, Uyvy8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv8:
                Encode<TPixel, Yuyv8Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv8BigEndian:
                Encode<TPixel, Yuyv8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv16:
                Encode<TPixel, Yuyv16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy16:
                Encode<TPixel, Uyvy16Transfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Msb:
                Encode<TPixel, Yuyv10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv10Lsb:
                Encode<TPixel, Yuyv10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Msb:
                Encode<TPixel, Uyvy10MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy10Lsb:
                Encode<TPixel, Uyvy10LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Msb:
                Encode<TPixel, Yuyv12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Yuyv12Lsb:
                Encode<TPixel, Yuyv12LsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Msb:
                Encode<TPixel, Uyvy12MsbTransfer>(source, destination, rowPitch);
                return;
            case PackedYuv422Transfer.Uyvy12Lsb:
                Encode<TPixel, Uyvy12LsbTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : struct, IPackedYuv422Transfer
    {
        var blockCountX = TTransfer.GetBlockCountX(destination.Width);
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                TTransfer.DecodeBlock(TTransfer.SliceSourceBlock(source, blockOffset), out var y0, out var y1, out var u, out var v);
                destinationRow[pixelX] = TPixel.FromRgba32Float(TTransfer.YuvToRgba32Float(y0, u, v));
                if (pixelX + 1 < destination.Width)
                {
                    destinationRow[pixelX + 1] = TPixel.FromRgba32Float(TTransfer.YuvToRgba32Float(y1, u, v));
                }

                blockOffset = TTransfer.AdvanceBlockOffset(blockOffset);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : struct, IPackedYuv422Transfer
    {
        var blockCountX = TTransfer.GetBlockCountX(source.Width);
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var first = TPixel.ToRgba32Float(sourceRow[pixelX]);
                var second = pixelX + 1 < source.Width ? TPixel.ToRgba32Float(sourceRow[pixelX + 1]) : first;
                RgbaToYuv(first, out var y0, out var u0, out var v0);
                RgbaToYuv(second, out var y1, out var u1, out var v1);
                var u = TTransfer.ChromaToYuvSample((u0 + u1) * 0.5f);
                var v = TTransfer.ChromaToYuvSample((v0 + v1) * 0.5f);
                TTransfer.EncodeBlock(TTransfer.SliceDestinationBlock(destination, blockOffset), y0, y1, u, v);

                blockOffset = TTransfer.AdvanceBlockOffset(blockOffset);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IPackedYuv422Transfer
    {
        static abstract int GetBlockCountX(int width);

        static abstract ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset);

        static abstract Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset);

        static abstract int AdvanceBlockOffset(int blockOffset);

        static abstract Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample);

        static abstract uint ChromaToYuvSample(float value);

        static abstract void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v);

        static abstract void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v);
    }

    private interface IPackedYuv422SampleTransfer
    {
        static abstract uint ReadFirst(ReadOnlySpan<byte> block);

        static abstract uint ReadSecond(ReadOnlySpan<byte> block);

        static abstract uint ReadThird(ReadOnlySpan<byte> block);

        static abstract uint ReadFourth(ReadOnlySpan<byte> block);

        static abstract void WriteFirst(Span<byte> block, uint sample);

        static abstract void WriteSecond(Span<byte> block, uint sample);

        static abstract void WriteThird(Span<byte> block, uint sample);

        static abstract void WriteFourth(Span<byte> block, uint sample);

        static abstract Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample);

        static abstract uint UnitToYuvSample(float value);

        static abstract uint ChromaToYuvSample(float value);
    }

    private readonly struct Uyvy8Transfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetEvenBlockCountX(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock4(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock4(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock4(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample8Transfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample8Transfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeUyvyBlock<Sample8Transfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeUyvyBlock<Sample8Transfer>(block, y0, y1, u, v);
    }

    private readonly struct Uyvy8TransferBigEndian : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => Uyvy8Transfer.GetBlockCountX(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock4(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock4(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock4(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample8Transfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample8Transfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeBigEndianUyvyBlock<Sample8Transfer>(block, out y0, out y1, out u, out v, BigEndianByteSwapMode.Swap8In32);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeBigEndianUyvyBlock<Sample8Transfer>(block, y0, y1, u, v, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Yuyv8Transfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetEvenBlockCountX(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock4(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock4(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock4(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample8Transfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample8Transfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeYuyvBlock<Sample8Transfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeYuyvBlock<Sample8Transfer>(block, y0, y1, u, v);
    }

    private readonly struct Yuyv8TransferBigEndian : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => Yuyv8Transfer.GetBlockCountX(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock4(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock4(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock4(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample8Transfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample8Transfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeBigEndianYuyvBlock<Sample8Transfer>(block, out y0, out y1, out u, out v, BigEndianByteSwapMode.Swap8In32);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeBigEndianYuyvBlock<Sample8Transfer>(block, y0, y1, u, v, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Yuyv16Transfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample16Transfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample16Transfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeYuyvBlock<Sample16Transfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeYuyvBlock<Sample16Transfer>(block, y0, y1, u, v);
    }

    private readonly struct Uyvy16Transfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample16Transfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample16Transfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeUyvyBlock<Sample16Transfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeUyvyBlock<Sample16Transfer>(block, y0, y1, u, v);
    }

    private readonly struct Yuyv10MsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample10MsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample10MsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeYuyvBlock<Sample10MsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeYuyvBlock<Sample10MsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Yuyv10LsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample10LsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample10LsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeYuyvBlock<Sample10LsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeYuyvBlock<Sample10LsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Uyvy10MsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample10MsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample10MsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeUyvyBlock<Sample10MsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeUyvyBlock<Sample10MsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Uyvy10LsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample10LsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample10LsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeUyvyBlock<Sample10LsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeUyvyBlock<Sample10LsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Yuyv12MsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample12MsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample12MsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeYuyvBlock<Sample12MsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeYuyvBlock<Sample12MsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Yuyv12LsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample12LsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample12LsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeYuyvBlock<Sample12LsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeYuyvBlock<Sample12LsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Uyvy12MsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample12MsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample12MsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeUyvyBlock<Sample12MsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeUyvyBlock<Sample12MsbTransfer>(block, y0, y1, u, v);
    }

    private readonly struct Uyvy12LsbTransfer : IPackedYuv422Transfer
    {
        public static int GetBlockCountX(int width) => GetBlockCountXWithTrailingPixel(width);

        public static ReadOnlySpan<byte> SliceSourceBlock(ReadOnlySpan<byte> source, int blockOffset) => SliceBlock8(source, blockOffset);

        public static Span<byte> SliceDestinationBlock(Span<byte> destination, int blockOffset) => SliceBlock8(destination, blockOffset);

        public static int AdvanceBlockOffset(int blockOffset) => AdvanceBlock8(blockOffset);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            Sample12LsbTransfer.YuvToRgba32Float(ySample, uSample, vSample);

        public static uint ChromaToYuvSample(float value) => Sample12LsbTransfer.ChromaToYuvSample(value);

        public static void DecodeBlock(ReadOnlySpan<byte> block, out uint y0, out uint y1, out uint u, out uint v) =>
            DecodeUyvyBlock<Sample12LsbTransfer>(block, out y0, out y1, out u, out v);

        public static void EncodeBlock(Span<byte> block, float y0, float y1, uint u, uint v) =>
            EncodeUyvyBlock<Sample12LsbTransfer>(block, y0, y1, u, v);
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded YUV texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded YUV texture.", nameof(destination));
        }
    }

    private static bool TryGetTransfer(TextureFormat format, out PackedYuv422Transfer transfer)
    {
        if (format == TextureFormats.Uyvy422UNorm)
        {
            transfer = PackedYuv422Transfer.Uyvy8;
            return true;
        }

        if (format == TextureFormats.Uyvy422UNormBigEndian)
        {
            transfer = PackedYuv422Transfer.Uyvy8BigEndian;
            return true;
        }

        if (format == TextureFormats.Yuy2UNorm)
        {
            transfer = PackedYuv422Transfer.Yuyv8;
            return true;
        }

        if (format == TextureFormats.Yuy2UNormBigEndian)
        {
            transfer = PackedYuv422Transfer.Yuyv8BigEndian;
            return true;
        }

        if (format == TextureFormats.Yuyv16_422UNorm) { transfer = PackedYuv422Transfer.Yuyv16; return true; }
        if (format == TextureFormats.Uyvy16_422UNorm) { transfer = PackedYuv422Transfer.Uyvy16; return true; }
        if (format == TextureFormats.Yuyv10Msb422UNorm) { transfer = PackedYuv422Transfer.Yuyv10Msb; return true; }
        if (format == TextureFormats.Yuyv10Lsb422UNorm) { transfer = PackedYuv422Transfer.Yuyv10Lsb; return true; }
        if (format == TextureFormats.Uyvy10Msb422UNorm) { transfer = PackedYuv422Transfer.Uyvy10Msb; return true; }
        if (format == TextureFormats.Uyvy10Lsb422UNorm) { transfer = PackedYuv422Transfer.Uyvy10Lsb; return true; }
        if (format == TextureFormats.Yuyv12Msb422UNorm) { transfer = PackedYuv422Transfer.Yuyv12Msb; return true; }
        if (format == TextureFormats.Yuyv12Lsb422UNorm) { transfer = PackedYuv422Transfer.Yuyv12Lsb; return true; }
        if (format == TextureFormats.Uyvy12Msb422UNorm) { transfer = PackedYuv422Transfer.Uyvy12Msb; return true; }
        if (format == TextureFormats.Uyvy12Lsb422UNorm) { transfer = PackedYuv422Transfer.Uyvy12Lsb; return true; }

        transfer = default;
        return false;
    }

    private static bool Is8BitPacked(PackedYuv422Transfer transfer) =>
        transfer is PackedYuv422Transfer.Uyvy8
            or PackedYuv422Transfer.Uyvy8BigEndian
            or PackedYuv422Transfer.Yuyv8
            or PackedYuv422Transfer.Yuyv8BigEndian;

    private static int GetEvenBlockCountX(int width) => width / 2;

    private static int GetBlockCountXWithTrailingPixel(int width) => checked((width + 1) / 2);

    private static ReadOnlySpan<byte> SliceBlock4(ReadOnlySpan<byte> source, int blockOffset) =>
        source.Slice(blockOffset, 4);

    private static Span<byte> SliceBlock4(Span<byte> destination, int blockOffset) =>
        destination.Slice(blockOffset, 4);

    private static int AdvanceBlock4(int blockOffset) => checked(blockOffset + 4);

    private static ReadOnlySpan<byte> SliceBlock8(ReadOnlySpan<byte> source, int blockOffset) =>
        source.Slice(blockOffset, 8);

    private static Span<byte> SliceBlock8(Span<byte> destination, int blockOffset) =>
        destination.Slice(blockOffset, 8);

    private static int AdvanceBlock8(int blockOffset) => checked(blockOffset + 8);

    private static void DecodeUyvyBlock<TSample>(
        ReadOnlySpan<byte> block,
        out uint y0,
        out uint y1,
        out uint u,
        out uint v)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        u = TSample.ReadFirst(block);
        y0 = TSample.ReadSecond(block);
        v = TSample.ReadThird(block);
        y1 = TSample.ReadFourth(block);
    }

    private static void DecodeYuyvBlock<TSample>(
        ReadOnlySpan<byte> block,
        out uint y0,
        out uint y1,
        out uint u,
        out uint v)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        y0 = TSample.ReadFirst(block);
        u = TSample.ReadSecond(block);
        y1 = TSample.ReadThird(block);
        v = TSample.ReadFourth(block);
    }

    private static void DecodeBigEndianUyvyBlock<TSample>(
        ReadOnlySpan<byte> block,
        out uint y0,
        out uint y1,
        out uint u,
        out uint v,
        BigEndianByteSwapMode endianMode)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[block.Length];
        BigEndianByteSwap.CopyToLittleEndian(block, littleEndianBlock, endianMode);
        DecodeUyvyBlock<TSample>(littleEndianBlock, out y0, out y1, out u, out v);
    }

    private static void DecodeBigEndianYuyvBlock<TSample>(
        ReadOnlySpan<byte> block,
        out uint y0,
        out uint y1,
        out uint u,
        out uint v,
        BigEndianByteSwapMode endianMode)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[block.Length];
        BigEndianByteSwap.CopyToLittleEndian(block, littleEndianBlock, endianMode);
        DecodeYuyvBlock<TSample>(littleEndianBlock, out y0, out y1, out u, out v);
    }

    private static void EncodeUyvyBlock<TSample>(Span<byte> block, float y0, float y1, uint u, uint v)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        TSample.WriteFirst(block, u);
        TSample.WriteSecond(block, TSample.UnitToYuvSample(y0));
        TSample.WriteThird(block, v);
        TSample.WriteFourth(block, TSample.UnitToYuvSample(y1));
    }

    private static void EncodeYuyvBlock<TSample>(Span<byte> block, float y0, float y1, uint u, uint v)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        TSample.WriteFirst(block, TSample.UnitToYuvSample(y0));
        TSample.WriteSecond(block, u);
        TSample.WriteThird(block, TSample.UnitToYuvSample(y1));
        TSample.WriteFourth(block, v);
    }

    private static void EncodeBigEndianUyvyBlock<TSample>(
        Span<byte> block,
        float y0,
        float y1,
        uint u,
        uint v,
        BigEndianByteSwapMode endianMode)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[block.Length];
        EncodeUyvyBlock<TSample>(littleEndianBlock, y0, y1, u, v);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianBlock, block, endianMode);
    }

    private static void EncodeBigEndianYuyvBlock<TSample>(
        Span<byte> block,
        float y0,
        float y1,
        uint u,
        uint v,
        BigEndianByteSwapMode endianMode)
        where TSample : struct, IPackedYuv422SampleTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[block.Length];
        EncodeYuyvBlock<TSample>(littleEndianBlock, y0, y1, u, v);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianBlock, block, endianMode);
    }

    private readonly struct Sample8Transfer : IPackedYuv422SampleTransfer
    {
        public static uint ReadFirst(ReadOnlySpan<byte> block) => block[0];

        public static uint ReadSecond(ReadOnlySpan<byte> block) => block[1];

        public static uint ReadThird(ReadOnlySpan<byte> block) => block[2];

        public static uint ReadFourth(ReadOnlySpan<byte> block) => block[3];

        public static void WriteFirst(Span<byte> block, uint sample) => block[0] = checked((byte)sample);

        public static void WriteSecond(Span<byte> block, uint sample) => block[1] = checked((byte)sample);

        public static void WriteThird(Span<byte> block, uint sample) => block[2] = checked((byte)sample);

        public static void WriteFourth(Span<byte> block, uint sample) => block[3] = checked((byte)sample);

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            PackedYuv422TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 8);

        public static uint UnitToYuvSample(float value) =>
            PackedYuv422TextureCoder.UnitToYuvSample(value, bitsPerSample: 8);

        public static uint ChromaToYuvSample(float value) =>
            PackedYuv422TextureCoder.ChromaToYuvSample(value, bitsPerSample: 8);
    }

    private readonly struct Sample10MsbTransfer : IPackedYuv422SampleTransfer
    {
        public static uint ReadFirst(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block) >> 6;

        public static uint ReadSecond(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[2..]) >> 6;

        public static uint ReadThird(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) >> 6;

        public static uint ReadFourth(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) >> 6;

        public static void WriteFirst(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block, checked((ushort)(sample << 6)));

        public static void WriteSecond(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], checked((ushort)(sample << 6)));

        public static void WriteThird(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[4..], checked((ushort)(sample << 6)));

        public static void WriteFourth(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[6..], checked((ushort)(sample << 6)));

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            PackedYuv422TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 10);

        public static uint UnitToYuvSample(float value) =>
            PackedYuv422TextureCoder.UnitToYuvSample(value, bitsPerSample: 10);

        public static uint ChromaToYuvSample(float value) =>
            PackedYuv422TextureCoder.ChromaToYuvSample(value, bitsPerSample: 10);
    }

    private readonly struct Sample10LsbTransfer : IPackedYuv422SampleTransfer
    {
        public static uint ReadFirst(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block) & 0x03ff;

        public static uint ReadSecond(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[2..]) & 0x03ff;

        public static uint ReadThird(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) & 0x03ff;

        public static uint ReadFourth(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) & 0x03ff;

        public static void WriteFirst(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block, checked((ushort)sample));

        public static void WriteSecond(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], checked((ushort)sample));

        public static void WriteThird(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[4..], checked((ushort)sample));

        public static void WriteFourth(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[6..], checked((ushort)sample));

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            PackedYuv422TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 10);

        public static uint UnitToYuvSample(float value) =>
            PackedYuv422TextureCoder.UnitToYuvSample(value, bitsPerSample: 10);

        public static uint ChromaToYuvSample(float value) =>
            PackedYuv422TextureCoder.ChromaToYuvSample(value, bitsPerSample: 10);
    }

    private readonly struct Sample12MsbTransfer : IPackedYuv422SampleTransfer
    {
        public static uint ReadFirst(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block) >> 4;

        public static uint ReadSecond(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[2..]) >> 4;

        public static uint ReadThird(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) >> 4;

        public static uint ReadFourth(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) >> 4;

        public static void WriteFirst(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block, checked((ushort)(sample << 4)));

        public static void WriteSecond(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], checked((ushort)(sample << 4)));

        public static void WriteThird(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[4..], checked((ushort)(sample << 4)));

        public static void WriteFourth(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[6..], checked((ushort)(sample << 4)));

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            PackedYuv422TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 12);

        public static uint UnitToYuvSample(float value) =>
            PackedYuv422TextureCoder.UnitToYuvSample(value, bitsPerSample: 12);

        public static uint ChromaToYuvSample(float value) =>
            PackedYuv422TextureCoder.ChromaToYuvSample(value, bitsPerSample: 12);
    }

    private readonly struct Sample12LsbTransfer : IPackedYuv422SampleTransfer
    {
        public static uint ReadFirst(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block) & 0x0fff;

        public static uint ReadSecond(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[2..]) & 0x0fff;

        public static uint ReadThird(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) & 0x0fff;

        public static uint ReadFourth(ReadOnlySpan<byte> block) => (uint)BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) & 0x0fff;

        public static void WriteFirst(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block, checked((ushort)sample));

        public static void WriteSecond(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], checked((ushort)sample));

        public static void WriteThird(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[4..], checked((ushort)sample));

        public static void WriteFourth(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[6..], checked((ushort)sample));

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            PackedYuv422TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 12);

        public static uint UnitToYuvSample(float value) =>
            PackedYuv422TextureCoder.UnitToYuvSample(value, bitsPerSample: 12);

        public static uint ChromaToYuvSample(float value) =>
            PackedYuv422TextureCoder.ChromaToYuvSample(value, bitsPerSample: 12);
    }

    private readonly struct Sample16Transfer : IPackedYuv422SampleTransfer
    {
        public static uint ReadFirst(ReadOnlySpan<byte> block) => BinaryPrimitives.ReadUInt16LittleEndian(block);

        public static uint ReadSecond(ReadOnlySpan<byte> block) => BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);

        public static uint ReadThird(ReadOnlySpan<byte> block) => BinaryPrimitives.ReadUInt16LittleEndian(block[4..]);

        public static uint ReadFourth(ReadOnlySpan<byte> block) => BinaryPrimitives.ReadUInt16LittleEndian(block[6..]);

        public static void WriteFirst(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block, checked((ushort)sample));

        public static void WriteSecond(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], checked((ushort)sample));

        public static void WriteThird(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[4..], checked((ushort)sample));

        public static void WriteFourth(Span<byte> block, uint sample) =>
            BinaryPrimitives.WriteUInt16LittleEndian(block[6..], checked((ushort)sample));

        public static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample) =>
            PackedYuv422TextureCoder.YuvToRgba32Float(ySample, uSample, vSample, bitsPerSample: 16);

        public static uint UnitToYuvSample(float value) =>
            PackedYuv422TextureCoder.UnitToYuvSample(value, bitsPerSample: 16);

        public static uint ChromaToYuvSample(float value) =>
            PackedYuv422TextureCoder.ChromaToYuvSample(value, bitsPerSample: 16);
    }

    private static Rgba32Float YuvToRgba32Float(uint ySample, uint uSample, uint vSample, int bitsPerSample)
    {
        var maxSample = GetMaxYuvSample(bitsPerSample);
        var y = ySample / (float)maxSample;
        var neutralChroma = 1u << (bitsPerSample - 1);
        var u = ((int)uSample - (int)neutralChroma) / (float)maxSample;
        var v = ((int)vSample - (int)neutralChroma) / (float)maxSample;
        return new Rgba32Float(
            Clamp01(y + (1.402f * v)),
            Clamp01(y - (0.344136f * u) - (0.714136f * v)),
            Clamp01(y + (1.772f * u)));
    }

    private static void RgbaToYuv(Rgba32Float color, out float y, out float u, out float v)
    {
        var red = Clamp01(color.Red);
        var green = Clamp01(color.Green);
        var blue = Clamp01(color.Blue);
        y = (0.299f * red) + (0.587f * green) + (0.114f * blue);
        u = (blue - y) / 1.772f;
        v = (red - y) / 1.402f;
    }

    private static uint UnitToYuvSample(float value, int bitsPerSample) =>
        ScaleToYuvSample(Clamp01(value), bitsPerSample);

    private static uint ChromaToYuvSample(float value, int bitsPerSample)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        var maxSample = GetMaxYuvSample(bitsPerSample);
        var neutralChroma = 1u << (bitsPerSample - 1);
        var scaled = MathF.Round((value * maxSample) + neutralChroma);
        if (scaled < 0f)
        {
            return 0;
        }

        return scaled > maxSample ? maxSample : (uint)scaled;
    }

    private static uint ScaleToYuvSample(float value, int bitsPerSample)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        var maxSample = GetMaxYuvSample(bitsPerSample);
        return (uint)MathF.Round(value * maxSample);
    }

    private static uint GetMaxYuvSample(int bitsPerSample) =>
        bitsPerSample == 16 ? ushort.MaxValue : (1u << bitsPerSample) - 1u;

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || value < 0f)
        {
            return 0f;
        }

        return value > 1f ? 1f : value;
    }

    private static int GetHighBitRowByteCount(int width) => checked(((width + 1) / 2) * 8);

    private static void ValidatePacked8BitWidth(int width)
    {
        if ((width & 1) != 0)
        {
            throw new ArgumentException("Packed 8-bit YUV 4:2:2 textures require an even width.", nameof(width));
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed YUV 4:2:2 texture coder does not support texture format '{format.Name}'.");

    private enum PackedYuv422Transfer
    {
        Uyvy8,
        Uyvy8BigEndian,
        Yuyv8,
        Yuyv8BigEndian,
        Yuyv16,
        Uyvy16,
        Yuyv10Msb,
        Yuyv10Lsb,
        Uyvy10Msb,
        Uyvy10Lsb,
        Yuyv12Msb,
        Yuyv12Lsb,
        Uyvy12Msb,
        Uyvy12Lsb
    }

}
