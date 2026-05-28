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

    private readonly S3tcTransfer _transfer;

    public S3tcTextureCoder(TextureFormat format)
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
            case S3tcTransfer.Dxt1Rgb:
                Decode<TPixel, Dxt1RgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbSrgb:
                Decode<TPixel, Dxt1RgbSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbBigEndian:
                Decode<TPixel, Dxt1RgbTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1Rgba:
                Decode<TPixel, Dxt1RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbaSrgb:
                Decode<TPixel, Dxt1RgbaSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbaBigEndian:
                Decode<TPixel, Dxt1RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt2Rgba:
                Decode<TPixel, Dxt2RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt2RgbaBigEndian:
                Decode<TPixel, Dxt2RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3Rgba:
                Decode<TPixel, Dxt3RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3RgbaSrgb:
                Decode<TPixel, Dxt3RgbaSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3RgbaBigEndian:
                Decode<TPixel, Dxt3RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3A:
                Decode<TPixel, Dxt3ATransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3ABigEndian:
                Decode<TPixel, Dxt3ATransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3A1111:
                Decode<TPixel, Dxt3A1111Transfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3A1111BigEndian:
                Decode<TPixel, Dxt3A1111TransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt4Rgba:
                Decode<TPixel, Dxt4RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt4RgbaBigEndian:
                Decode<TPixel, Dxt4RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5Rgba:
                Decode<TPixel, Dxt5RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5RgbaSrgb:
                Decode<TPixel, Dxt5RgbaSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5RgbaBigEndian:
                Decode<TPixel, Dxt5RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5A:
                Decode<TPixel, Dxt5ATransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5ABigEndian:
                Decode<TPixel, Dxt5ATransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxn:
                Decode<TPixel, DxnTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.DxnBigEndian:
                Decode<TPixel, DxnTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Ctx1:
                Decode<TPixel, Ctx1Transfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Ctx1BigEndian:
                Decode<TPixel, Ctx1TransferBigEndian>(source, destination, rowPitch);
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
            case S3tcTransfer.Dxt1Rgb:
                Encode<TPixel, Dxt1RgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbSrgb:
                Encode<TPixel, Dxt1RgbSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbBigEndian:
                Encode<TPixel, Dxt1RgbTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1Rgba:
                Encode<TPixel, Dxt1RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbaSrgb:
                Encode<TPixel, Dxt1RgbaSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt1RgbaBigEndian:
                Encode<TPixel, Dxt1RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt2Rgba:
                Encode<TPixel, Dxt2RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt2RgbaBigEndian:
                Encode<TPixel, Dxt2RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3Rgba:
                Encode<TPixel, Dxt3RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3RgbaSrgb:
                Encode<TPixel, Dxt3RgbaSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3RgbaBigEndian:
                Encode<TPixel, Dxt3RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3A:
                Encode<TPixel, Dxt3ATransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3ABigEndian:
                Encode<TPixel, Dxt3ATransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3A1111:
                Encode<TPixel, Dxt3A1111Transfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt3A1111BigEndian:
                Encode<TPixel, Dxt3A1111TransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt4Rgba:
                Encode<TPixel, Dxt4RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt4RgbaBigEndian:
                Encode<TPixel, Dxt4RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5Rgba:
                Encode<TPixel, Dxt5RgbaTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5RgbaSrgb:
                Encode<TPixel, Dxt5RgbaSrgbTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5RgbaBigEndian:
                Encode<TPixel, Dxt5RgbaTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5A:
                Encode<TPixel, Dxt5ATransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxt5ABigEndian:
                Encode<TPixel, Dxt5ATransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Dxn:
                Encode<TPixel, DxnTransfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.DxnBigEndian:
                Encode<TPixel, DxnTransferBigEndian>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Ctx1:
                Encode<TPixel, Ctx1Transfer>(source, destination, rowPitch);
                return;
            case S3tcTransfer.Ctx1BigEndian:
                Encode<TPixel, Ctx1TransferBigEndian>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IS3tcTransfer
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

    private static void Encode<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IS3tcTransfer
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

    private interface IS3tcTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination);
    }

    private readonly struct Dxt1RgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeColorBlock(source, Dxt1ColorMode.Rgb, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeColorBlock(source, Dxt1ColorMode.Rgb, destination);
    }

    private readonly struct Dxt1RgbSrgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source, Dxt1ColorMode.Rgb, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeSrgbColorBlock(source, Dxt1ColorMode.Rgb, destination);
    }

    private readonly struct Dxt1RgbTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt1RgbTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt1RgbTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt1RgbTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt1RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeColorBlock(source, Dxt1ColorMode.Rgba, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeColorBlock(source, Dxt1ColorMode.Rgba, destination);
    }

    private readonly struct Dxt1RgbaSrgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source, Dxt1ColorMode.Rgba, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeSrgbColorBlock(source, Dxt1ColorMode.Rgba, destination);
    }

    private readonly struct Dxt1RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt1RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt1RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt1RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt2RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
            DecodeExplicitAlphaBlock(source[..8], destination);
            RecoverPremultipliedAlpha(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            Span<Rgba8UNorm> premultipliedBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            PremultiplyAlpha(source, premultipliedBlock);
            EncodeExplicitAlphaBlock(source, destination[..8]);
            EncodeColorBlock(premultipliedBlock, Dxt1ColorMode.FourColor, destination[8..]);
        }
    }

    private readonly struct Dxt2RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt2RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt2RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt2RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt3RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
            DecodeExplicitAlphaBlock(source[..8], destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            EncodeExplicitAlphaBlock(source, destination[..8]);
            EncodeColorBlock(source, Dxt1ColorMode.FourColor, destination[8..]);
        }
    }

    private readonly struct Dxt3RgbaSrgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            Dxt3RgbaTransfer.DecodeBlock(source, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            EncodeSrgbColors(source, srgbBlock);
            EncodeExplicitAlphaBlock(source, destination[..8]);
            EncodeColorBlock(srgbBlock, Dxt1ColorMode.FourColor, destination[8..]);
        }
    }

    private readonly struct Dxt3RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt3RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt3RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt3RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt3ATransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeExplicitAlphaOnlyBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeExplicitAlphaOnlyBlock(source, destination);
    }

    private readonly struct Dxt3ATransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt3ATransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt3ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt3ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt3A1111Transfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeDxt3A1111Block(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeDxt3A1111Block(source, destination);
    }

    private readonly struct Dxt3A1111TransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt3A1111Transfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt3A1111Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt3A1111Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt4RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
            DecodeInterpolatedAlphaBlock(source[..8], destination);
            RecoverPremultipliedAlpha(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            Span<Rgba8UNorm> premultipliedBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            PremultiplyAlpha(source, premultipliedBlock);
            EncodeInterpolatedAlphaBlock(source, destination[..8]);
            EncodeColorBlock(premultipliedBlock, Dxt1ColorMode.FourColor, destination[8..]);
        }
    }

    private readonly struct Dxt4RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt4RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt4RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt4RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt5RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
            DecodeInterpolatedAlphaBlock(source[..8], destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            EncodeInterpolatedAlphaBlock(source, destination[..8]);
            EncodeColorBlock(source, Dxt1ColorMode.FourColor, destination[8..]);
        }
    }

    private readonly struct Dxt5RgbaSrgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            Dxt5RgbaTransfer.DecodeBlock(source, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            EncodeSrgbColors(source, srgbBlock);
            EncodeInterpolatedAlphaBlock(source, destination[..8]);
            EncodeColorBlock(srgbBlock, Dxt1ColorMode.FourColor, destination[8..]);
        }
    }

    private readonly struct Dxt5RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt5RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt5RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt5RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Dxt5ATransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeInterpolatedAlphaOnlyBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeInterpolatedAlphaOnlyBlock(source, destination);
    }

    private readonly struct Dxt5ATransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt5ATransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt5ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Dxt5ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct DxnTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            InitializeScalarBlock(destination);
            DecodeInterpolatedComponentBlock(source[..8], S3tcScalarComponent.Red, destination);
            DecodeInterpolatedComponentBlock(source[8..], S3tcScalarComponent.Green, destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
        {
            EncodeInterpolatedComponentBlock(source, S3tcScalarComponent.Red, destination[..8]);
            EncodeInterpolatedComponentBlock(source, S3tcScalarComponent.Green, destination[8..]);
        }
    }

    private readonly struct DxnTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => DxnTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<DxnTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<DxnTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Ctx1Transfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeCtx1Block(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeCtx1Block(source, destination);
    }

    private readonly struct Ctx1TransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Ctx1Transfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Ctx1Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
            EncodeBigEndianBlock<Ctx1Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16);
    }

    private static void DecodeBigEndianBlock<TTransfer>(
        ReadOnlySpan<byte> source,
        Span<Rgba8UNorm> destination,
        BigEndianByteSwapMode endianMode)
        where TTransfer : IS3tcTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[TTransfer.BytesPerBlock];
        BigEndianByteSwap.CopyToLittleEndian(source, littleEndianBlock, endianMode);
        TTransfer.DecodeBlock(littleEndianBlock, destination);
    }

    private static void EncodeBigEndianBlock<TTransfer>(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        BigEndianByteSwapMode endianMode)
        where TTransfer : IS3tcTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[TTransfer.BytesPerBlock];
        TTransfer.EncodeBlock(source, littleEndianBlock);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianBlock, destination, endianMode);
    }

    private static void EncodeSrgbColorBlock(ReadOnlySpan<Rgba8UNorm> source, Dxt1ColorMode colorMode, Span<byte> destination)
    {
        Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
        EncodeSrgbColors(source, srgbBlock);
        EncodeColorBlock(srgbBlock, colorMode, destination);
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

    private static void DecodeExplicitAlphaOnlyBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var packed = source[i >> 1];
            var alpha4 = (i & 1) == 0 ? packed & 0x0f : packed >> 4;
            destination[i] = new Rgba8UNorm(0, 0, 0, (byte)((alpha4 << 4) | alpha4));
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

    private static void EncodeExplicitAlphaOnlyBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
        EncodeExplicitAlphaBlock(source, destination);

    private static void DecodeDxt3A1111Block(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var packed = source[i >> 1];
            var bits = (i & 1) == 0 ? packed & 0x0f : packed >> 4;
            destination[i] = new Rgba8UNorm(
                ExpandOneBit((bits >> 3) & 0x1),
                ExpandOneBit((bits >> 2) & 0x1),
                ExpandOneBit((bits >> 1) & 0x1),
                ExpandOneBit(bits & 0x1));
        }
    }

    private static void EncodeDxt3A1111Block(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        for (var i = 0; i < 8; i++)
        {
            var low = PackDxt3A1111Texel(source[i * 2]);
            var high = PackDxt3A1111Texel(source[(i * 2) + 1]);
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

    private static void DecodeInterpolatedAlphaOnlyBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        InitializeScalarBlock(destination);
        DecodeInterpolatedComponentBlock(source, S3tcScalarComponent.Alpha, destination);
    }

    private static void DecodeInterpolatedComponentBlock(
        ReadOnlySpan<byte> source,
        S3tcScalarComponent component,
        Span<Rgba8UNorm> destination)
    {
        Span<byte> palette = stackalloc byte[8];
        BuildAlphaPalette(source[0], source[1], palette);

        var indices = ReadAlphaIndices(source);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            SetComponent(ref destination[i], component, palette[(int)((indices >> (i * 3)) & 0x7u)]);
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

    private static void EncodeInterpolatedAlphaOnlyBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination) =>
        EncodeInterpolatedComponentBlock(source, S3tcScalarComponent.Alpha, destination);

    private static void EncodeInterpolatedComponentBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        Span<byte> destination)
    {
        FindComponentBounds(source, component, out var min, out var max);
        destination[0] = max;
        destination[1] = min;

        Span<byte> palette = stackalloc byte[8];
        BuildAlphaPalette(max, min, palette);

        ulong indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            indices |= (ulong)FindNearestAlphaIndex(GetComponent(source[i], component), palette) << (i * 3);
        }

        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(indices >> (8 * i));
        }
    }

    private static void DecodeCtx1Block(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        Span<byte> redPalette = stackalloc byte[4];
        Span<byte> greenPalette = stackalloc byte[4];
        BuildCtx1Palette(source[0], source[1], redPalette);
        BuildCtx1Palette(source[2], source[3], greenPalette);

        var indices = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)((indices >> (i * 2)) & 0x3u);
            destination[i] = new Rgba8UNorm(redPalette[index], greenPalette[index], 0, 255);
        }
    }

    private static void EncodeCtx1Block(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination)
    {
        FindComponentBounds(source, S3tcScalarComponent.Red, out var redMin, out var redMax);
        FindComponentBounds(source, S3tcScalarComponent.Green, out var greenMin, out var greenMax);
        destination[0] = redMax;
        destination[1] = redMin;
        destination[2] = greenMax;
        destination[3] = greenMin;

        Span<byte> redPalette = stackalloc byte[4];
        Span<byte> greenPalette = stackalloc byte[4];
        BuildCtx1Palette(redMax, redMin, redPalette);
        BuildCtx1Palette(greenMax, greenMin, greenPalette);

        uint indices = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = FindNearestCtx1Index(source[i], redPalette, greenPalette);
            indices |= (uint)index << (i * 2);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], indices);
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

    private static void BuildCtx1Palette(byte value0, byte value1, Span<byte> palette)
    {
        palette[0] = value0;
        palette[1] = value1;
        palette[2] = Interpolate(value0, value1, 2, 1, 3);
        palette[3] = Interpolate(value0, value1, 1, 2, 3);
    }

    private static Rgba8UNorm Interpolate(Rgb24 a, Rgb24 b, int weightA, int weightB, int divisor)
    {
        var bias = divisor == 3 ? 1 : 0;
        return new Rgba8UNorm(
            (byte)(((weightA * a.Red) + (weightB * b.Red) + bias) / divisor),
            (byte)(((weightA * a.Green) + (weightB * b.Green) + bias) / divisor),
            (byte)(((weightA * a.Blue) + (weightB * b.Blue) + bias) / divisor));
    }

    private static byte Interpolate(byte a, byte b, int weightA, int weightB, int divisor)
    {
        var bias = divisor == 3 ? 1 : 0;
        return (byte)(((weightA * a) + (weightB * b) + bias) / divisor);
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

    private static void FindComponentBounds(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        out byte min,
        out byte max)
    {
        min = byte.MaxValue;
        max = byte.MinValue;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = GetComponent(source[i], component);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
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

    private static int FindNearestCtx1Index(
        Rgba8UNorm color,
        ReadOnlySpan<byte> redPalette,
        ReadOnlySpan<byte> greenPalette)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < redPalette.Length; i++)
        {
            var red = color.Red - redPalette[i];
            var green = color.Green - greenPalette[i];
            var distance = (red * red) + (green * green);
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

    private static byte PackDxt3A1111Texel(Rgba8UNorm pixel)
    {
        var packed = 0;
        if (pixel.Red >= AlphaCutoff)
        {
            packed |= 0x8;
        }

        if (pixel.Green >= AlphaCutoff)
        {
            packed |= 0x4;
        }

        if (pixel.Blue >= AlphaCutoff)
        {
            packed |= 0x2;
        }

        if (pixel.Alpha >= AlphaCutoff)
        {
            packed |= 0x1;
        }

        return (byte)packed;
    }

    private static byte ExpandOneBit(int value) => value == 0 ? byte.MinValue : byte.MaxValue;

    private static byte GetComponent(Rgba8UNorm pixel, S3tcScalarComponent component) => component switch
    {
        S3tcScalarComponent.Red => pixel.Red,
        S3tcScalarComponent.Green => pixel.Green,
        S3tcScalarComponent.Alpha => pixel.Alpha,
        _ => throw new ArgumentOutOfRangeException(nameof(component))
    };

    private static void SetComponent(ref Rgba8UNorm pixel, S3tcScalarComponent component, byte value)
    {
        switch (component)
        {
            case S3tcScalarComponent.Red:
                pixel.Red = value;
                return;
            case S3tcScalarComponent.Green:
                pixel.Green = value;
                return;
            case S3tcScalarComponent.Alpha:
                pixel.Alpha = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static void InitializeScalarBlock(Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba8UNorm(0, 0, 0);
        }
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
        RgbaColorConversions.Srgb8ToLinearUNorm8(value);

    private static byte EncodeSrgb(byte value) =>
        RgbaColorConversions.LinearUNorm8ToSrgb8(value);

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

    private static bool TryGetTransfer(TextureFormat format, out S3tcTransfer transfer)
    {
        if (format == TextureFormats.Bc1Rgb
            || format == TextureFormats.Dxt1Rgb
            || format == TextureFormats.Bc1RgbSrgb
            || format == TextureFormats.Dxt1RgbSrgb)
        {
            transfer = format.ValueKind == TextureValueKind.Srgb ? S3tcTransfer.Dxt1RgbSrgb : S3tcTransfer.Dxt1Rgb;
            return true;
        }

        if (format == TextureFormats.Dxt1RgbBigEndian)
        {
            transfer = S3tcTransfer.Dxt1RgbBigEndian;
            return true;
        }

        if (format == TextureFormats.Bc1Rgba
            || format == TextureFormats.Dxt1Rgba
            || format == TextureFormats.Bc1RgbaSrgb
            || format == TextureFormats.Dxt1RgbaSrgb)
        {
            transfer = format.ValueKind == TextureValueKind.Srgb ? S3tcTransfer.Dxt1RgbaSrgb : S3tcTransfer.Dxt1Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt1RgbaBigEndian)
        {
            transfer = S3tcTransfer.Dxt1RgbaBigEndian;
            return true;
        }

        if (format == TextureFormats.Dxt2Rgba)
        {
            transfer = S3tcTransfer.Dxt2Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt2RgbaBigEndian)
        {
            transfer = S3tcTransfer.Dxt2RgbaBigEndian;
            return true;
        }

        if (format == TextureFormats.Bc2Rgba
            || format == TextureFormats.Dxt3Rgba
            || format == TextureFormats.Bc2RgbaSrgb
            || format == TextureFormats.Dxt3RgbaSrgb)
        {
            transfer = format.ValueKind == TextureValueKind.Srgb ? S3tcTransfer.Dxt3RgbaSrgb : S3tcTransfer.Dxt3Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt3RgbaBigEndian)
        {
            transfer = S3tcTransfer.Dxt3RgbaBigEndian;
            return true;
        }

        if (format == TextureFormats.Dxt3A)
        {
            transfer = S3tcTransfer.Dxt3A;
            return true;
        }

        if (format == TextureFormats.Dxt3ABigEndian)
        {
            transfer = S3tcTransfer.Dxt3ABigEndian;
            return true;
        }

        if (format == TextureFormats.Dxt3A1111)
        {
            transfer = S3tcTransfer.Dxt3A1111;
            return true;
        }

        if (format == TextureFormats.Dxt3A1111BigEndian)
        {
            transfer = S3tcTransfer.Dxt3A1111BigEndian;
            return true;
        }

        if (format == TextureFormats.Dxt4Rgba)
        {
            transfer = S3tcTransfer.Dxt4Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt4RgbaBigEndian)
        {
            transfer = S3tcTransfer.Dxt4RgbaBigEndian;
            return true;
        }

        if (format == TextureFormats.Bc3Rgba
            || format == TextureFormats.Dxt5Rgba
            || format == TextureFormats.Bc3RgbaSrgb
            || format == TextureFormats.Dxt5RgbaSrgb)
        {
            transfer = format.ValueKind == TextureValueKind.Srgb ? S3tcTransfer.Dxt5RgbaSrgb : S3tcTransfer.Dxt5Rgba;
            return true;
        }

        if (format == TextureFormats.Dxt5RgbaBigEndian)
        {
            transfer = S3tcTransfer.Dxt5RgbaBigEndian;
            return true;
        }

        if (format == TextureFormats.Dxt5A)
        {
            transfer = S3tcTransfer.Dxt5A;
            return true;
        }

        if (format == TextureFormats.Dxt5ABigEndian)
        {
            transfer = S3tcTransfer.Dxt5ABigEndian;
            return true;
        }

        if (format == TextureFormats.Dxn)
        {
            transfer = S3tcTransfer.Dxn;
            return true;
        }

        if (format == TextureFormats.DxnBigEndian)
        {
            transfer = S3tcTransfer.DxnBigEndian;
            return true;
        }

        if (format == TextureFormats.Ctx1)
        {
            transfer = S3tcTransfer.Ctx1;
            return true;
        }

        if (format == TextureFormats.Ctx1BigEndian)
        {
            transfer = S3tcTransfer.Ctx1BigEndian;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"S3TC texture coder does not support texture format '{format.Name}'.");

    private readonly record struct Rgb24(byte Red, byte Green, byte Blue);

    private enum S3tcScalarComponent
    {
        Red,
        Green,
        Alpha
    }

    private enum S3tcTransfer
    {
        Dxt1Rgb,
        Dxt1RgbSrgb,
        Dxt1RgbBigEndian,
        Dxt1Rgba,
        Dxt1RgbaSrgb,
        Dxt1RgbaBigEndian,
        Dxt2Rgba,
        Dxt2RgbaBigEndian,
        Dxt3Rgba,
        Dxt3RgbaSrgb,
        Dxt3RgbaBigEndian,
        Dxt3A,
        Dxt3ABigEndian,
        Dxt3A1111,
        Dxt3A1111BigEndian,
        Dxt4Rgba,
        Dxt4RgbaBigEndian,
        Dxt5Rgba,
        Dxt5RgbaSrgb,
        Dxt5RgbaBigEndian,
        Dxt5A,
        Dxt5ABigEndian,
        Dxn,
        DxnBigEndian,
        Ctx1,
        Ctx1BigEndian
    }

    private enum Dxt1ColorMode
    {
        Rgb,
        Rgba,
        FourColor
    }
}
