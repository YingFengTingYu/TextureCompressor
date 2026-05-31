using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;
using TextureCompressor.Options;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

public sealed class S3tcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;
    private const byte AlphaCutoff = 128;

    private readonly S3tcTransfer _transfer;
    private readonly S3tcCoderOptions _options;

    public S3tcTextureCoder(TextureFormat format, S3tcCoderOptions? options = null)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _options = options ?? new S3tcCoderOptions();
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

    private void EncodeByTransfer<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
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

    private static void Decode<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
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

    private void Encode<TPixel, TTransfer>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IS3tcTransfer
    {
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);
        var compressionMode = _options.CompressionMode;

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
                TTransfer.EncodeBlock(
                    block,
                    destination.Slice(blockOffset, TTransfer.BytesPerBlock),
                    compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IS3tcTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode);
    }

    private readonly struct Dxt1RgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeColorBlock(source, Dxt1ColorMode.Rgb, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeColorBlock(source, Dxt1ColorMode.Rgb, destination, compressionMode);
    }

    private readonly struct Dxt1RgbSrgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source, Dxt1ColorMode.Rgb, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeSrgbColorBlock(source, Dxt1ColorMode.Rgb, destination, compressionMode);
    }

    private readonly struct Dxt1RgbTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt1RgbTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt1RgbTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt1RgbTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Dxt1RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeColorBlock(source, Dxt1ColorMode.Rgba, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeColorBlock(source, Dxt1ColorMode.Rgba, destination, compressionMode);
    }

    private readonly struct Dxt1RgbaSrgbTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source, Dxt1ColorMode.Rgba, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeSrgbColorBlock(source, Dxt1ColorMode.Rgba, destination, compressionMode);
    }

    private readonly struct Dxt1RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt1RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt1RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt1RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
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

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            Span<Rgba8UNorm> premultipliedBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            PremultiplyAlpha(source, premultipliedBlock);
            EncodeExplicitAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(premultipliedBlock, Dxt1ColorMode.FourColor, destination[8..], compressionMode);
        }
    }

    private readonly struct Dxt2RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt2RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt2RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt2RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Dxt3RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
            DecodeExplicitAlphaBlock(source[..8], destination);
        }

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            EncodeExplicitAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(source, Dxt1ColorMode.FourColor, destination[8..], compressionMode);
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

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            EncodeSrgbColors(source, srgbBlock);
            EncodeExplicitAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(srgbBlock, Dxt1ColorMode.FourColor, destination[8..], compressionMode);
        }
    }

    private readonly struct Dxt3RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt3RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt3RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt3RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Dxt3ATransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeExplicitAlphaOnlyBlock(source, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeExplicitAlphaOnlyBlock(source, destination, compressionMode);
    }

    private readonly struct Dxt3ATransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt3ATransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt3ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt3ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Dxt3A1111Transfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeDxt3A1111Block(source, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel _) =>
            EncodeDxt3A1111Block(source, destination);
    }

    private readonly struct Dxt3A1111TransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt3A1111Transfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt3A1111Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt3A1111Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
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

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            Span<Rgba8UNorm> premultipliedBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            PremultiplyAlpha(source, premultipliedBlock);
            EncodeInterpolatedAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(premultipliedBlock, Dxt1ColorMode.FourColor, destination[8..], compressionMode);
        }
    }

    private readonly struct Dxt4RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt4RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt4RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt4RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Dxt5RgbaTransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeColorBlock(source[8..], Dxt1ColorMode.FourColor, destination);
            DecodeInterpolatedAlphaBlock(source[..8], destination);
        }

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            EncodeInterpolatedAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(source, Dxt1ColorMode.FourColor, destination[8..], compressionMode);
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

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
            EncodeSrgbColors(source, srgbBlock);
            EncodeInterpolatedAlphaBlock(source, destination[..8], compressionMode);
            EncodeColorBlock(srgbBlock, Dxt1ColorMode.FourColor, destination[8..], compressionMode);
        }
    }

    private readonly struct Dxt5RgbaTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt5RgbaTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt5RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt5RgbaTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Dxt5ATransfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeInterpolatedAlphaOnlyBlock(source, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeInterpolatedAlphaOnlyBlock(source, destination, compressionMode);
    }

    private readonly struct Dxt5ATransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Dxt5ATransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Dxt5ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Dxt5ATransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
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

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode)
        {
            EncodeInterpolatedComponentBlock(source, S3tcScalarComponent.Red, destination[..8], compressionMode);
            EncodeInterpolatedComponentBlock(source, S3tcScalarComponent.Green, destination[8..], compressionMode);
        }
    }

    private readonly struct DxnTransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => DxnTransfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<DxnTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<DxnTransfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
    }

    private readonly struct Ctx1Transfer : IS3tcTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeCtx1Block(source, destination);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel _) =>
            EncodeCtx1Block(source, destination);
    }

    private readonly struct Ctx1TransferBigEndian : IS3tcTransfer
    {
        public static int BytesPerBlock => Ctx1Transfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeBigEndianBlock<Ctx1Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16);

        public static void EncodeBlock(
            ReadOnlySpan<Rgba8UNorm> source,
            Span<byte> destination,
            TextureCompressionLevel compressionMode) =>
            EncodeBigEndianBlock<Ctx1Transfer>(source, destination, BigEndianByteSwapMode.Swap8In16, compressionMode);
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
        BigEndianByteSwapMode endianMode,
        TextureCompressionLevel compressionMode)
        where TTransfer : IS3tcTransfer
    {
        Span<byte> littleEndianBlock = stackalloc byte[TTransfer.BytesPerBlock];
        TTransfer.EncodeBlock(source, littleEndianBlock, compressionMode);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianBlock, destination, endianMode);
    }

    private static void EncodeSrgbColorBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
        EncodeSrgbColors(source, srgbBlock);
        EncodeColorBlock(srgbBlock, colorMode, destination, compressionMode);
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
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        var hasTransparent = colorMode == Dxt1ColorMode.Rgba && HasTransparentTexel(source);
        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeColorBlockFast(source, colorMode, hasTransparent, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeColorBlockOptimized(source, colorMode, hasTransparent, compressionMode, destination);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported S3TC compression mode.");
        }
    }

    private static void EncodeColorBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        bool hasTransparent,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        if (hasTransparent && !HasOpaqueTexel(source))
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, 0);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], 0xffffffff);
            return;
        }

        Span<ColorEndpointPair> seeds = stackalloc ColorEndpointPair[
            compressionMode == TextureCompressionLevel.Exhaustive ? 272 : 8];
        var seedCount = 0;
        FindColorBounds(source, ignoreTransparent: hasTransparent, out var min, out var max);
        AddColorSeed(seeds, ref seedCount, PackRgb565(max), PackRgb565(min), hasTransparent);

        if (TryInsetColorBounds(min, max, out var insetMin, out var insetMax))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb565(insetMax), PackRgb565(insetMin), hasTransparent);
        }

        if (TryFindPrincipalAxisColorEndpoints(source, hasTransparent, out var axisMin, out var axisMax))
        {
            AddColorSeed(
                seeds,
                ref seedCount,
                PackRgb565Nearest(axisMax),
                PackRgb565Nearest(axisMin),
                hasTransparent);
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindFarthestColorEndpoints(source, hasTransparent, out var farA, out var farB))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb565(farA), PackRgb565(farB), hasTransparent);
        }

        if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive
            && TryFindAverageColor(source, hasTransparent, out var average))
        {
            AddColorSeed(seeds, ref seedCount, PackRgb565Nearest(average), PackRgb565Nearest(average), hasTransparent);
        }

        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueColorSeeds(source, hasTransparent, seeds, ref seedCount);
        }

        var best = new ColorBlockEncoding { Error = long.MaxValue };
        var iterationLimit = GetColorOptimizationIterationLimit(compressionMode);
        for (var i = 0; i < seedCount; i++)
        {
            OptimizeColorSeed(
                source,
                colorMode,
                hasTransparent,
                seeds[i].Color0,
                seeds[i].Color1,
                iterationLimit,
                ref best);
        }

        RefineColorEndpoints(source, colorMode, hasTransparent, GetColorRefinementPassLimit(compressionMode), ref best);

        BinaryPrimitives.WriteUInt16LittleEndian(destination, best.Color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], best.Color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], best.Indices);
    }

    private static void EncodeColorBlockFast(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        bool hasTransparent,
        Span<byte> destination)
    {
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

    private static void EncodeExplicitAlphaBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        for (var i = 0; i < 8; i++)
        {
            var low = compressionMode == TextureCompressionLevel.Fast
                ? source[i * 2].Alpha >> 4
                : QuantizeAlpha4(source[i * 2].Alpha);
            var high = compressionMode == TextureCompressionLevel.Fast
                ? source[(i * 2) + 1].Alpha >> 4
                : QuantizeAlpha4(source[(i * 2) + 1].Alpha);
            destination[i] = (byte)(low | (high << 4));
        }
    }

    private static void EncodeExplicitAlphaOnlyBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode) =>
        EncodeExplicitAlphaBlock(source, destination, compressionMode);

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

    private static void EncodeInterpolatedAlphaBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        EncodeInterpolatedScalarBlock(source, S3tcScalarComponent.Alpha, destination, compressionMode);
    }

    private static void EncodeInterpolatedAlphaOnlyBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<byte> destination,
        TextureCompressionLevel compressionMode) =>
        EncodeInterpolatedComponentBlock(source, S3tcScalarComponent.Alpha, destination, compressionMode);

    private static void EncodeInterpolatedComponentBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        EncodeInterpolatedScalarBlock(source, component, destination, compressionMode);
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

    private static ushort PackRgb565Nearest(RgbVector value) =>
        PackRgb565Nearest(value.Red, value.Green, value.Blue);

    private static ushort PackRgb565Nearest(double red, double green, double blue) =>
        PackRgb565FromComponents(
            QuantizeToBits(red, 31),
            QuantizeToBits(green, 63),
            QuantizeToBits(blue, 31));

    private static ushort PackRgb565FromComponents(int red, int green, int blue) =>
        (ushort)((red << 11) | (green << 5) | blue);

    private static void GetRgb565Components(ushort value, out int red, out int green, out int blue)
    {
        red = (value >> 11) & 0x1f;
        green = (value >> 5) & 0x3f;
        blue = value & 0x1f;
    }

    private static int QuantizeToBits(double value, int max)
    {
        var clamped = Math.Clamp(value, byte.MinValue, byte.MaxValue);
        return Math.Clamp((int)Math.Round(clamped * max / byte.MaxValue), 0, max);
    }

    private static byte ClampToByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value), byte.MinValue, byte.MaxValue);

    private static int QuantizeAlpha4(byte alpha) => (alpha * 15 + 127) / byte.MaxValue;

    private static int ColorDistanceSquared(Rgb24 a, Rgb24 b)
    {
        var red = a.Red - b.Red;
        var green = a.Green - b.Green;
        var blue = a.Blue - b.Blue;
        return (red * red) + (green * green) + (blue * blue);
    }

    private static void NormalizeColorOrder(ref ushort color0, ref ushort color1, bool useThreeColorMode)
    {
        if (useThreeColorMode)
        {
            if (color0 > color1)
            {
                (color0, color1) = (color1, color0);
            }

            return;
        }

        if (color0 < color1)
        {
            (color0, color1) = (color1, color0);
        }
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

    private static void AddColorSeed(
        Span<ColorEndpointPair> seeds,
        ref int seedCount,
        ushort color0,
        ushort color1,
        bool useThreeColorMode)
    {
        NormalizeColorOrder(ref color0, ref color1, useThreeColorMode);

        for (var i = 0; i < seedCount; i++)
        {
            if (seeds[i].Color0 == color0 && seeds[i].Color1 == color1)
            {
                return;
            }
        }

        if (seedCount < seeds.Length)
        {
            seeds[seedCount++] = new ColorEndpointPair(color0, color1);
        }
    }

    private static void AddUniqueColorSeeds(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        Span<ColorEndpointPair> seeds,
        ref int seedCount)
    {
        Span<Rgb24> colors = stackalloc Rgb24[TexelsPerBlock];
        var uniqueCount = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            var color = new Rgb24(source[i].Red, source[i].Green, source[i].Blue);
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
                AddColorSeed(
                    seeds,
                    ref seedCount,
                    PackRgb565Nearest(colors[i].Red, colors[i].Green, colors[i].Blue),
                    PackRgb565Nearest(colors[j].Red, colors[j].Green, colors[j].Blue),
                    ignoreTransparent);
            }
        }
    }

    private static void OptimizeColorSeed(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        bool hasTransparent,
        ushort color0,
        ushort color1,
        int iterationLimit,
        ref ColorBlockEncoding best)
    {
        NormalizeColorOrder(ref color0, ref color1, hasTransparent);

        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateColorCandidate(source, colorMode, hasTransparent, color0, color1);
            UpdateBestColorEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveColorEndpoints(source, hasTransparent, current.Indices, out var nextColor0, out var nextColor1))
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
        Dxt1ColorMode colorMode,
        bool hasTransparent,
        ushort color0,
        ushort color1)
    {
        NormalizeColorOrder(ref color0, ref color1, hasTransparent);

        Span<Rgba8UNorm> palette = stackalloc Rgba8UNorm[4];
        BuildColorPalette(color0, color1, colorMode, palette);

        var paletteCount = hasTransparent ? 3 : 4;
        uint indices = 0;
        long error = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            int index;
            if (hasTransparent && source[i].Alpha < AlphaCutoff)
            {
                index = 3;
            }
            else
            {
                index = FindNearestColorIndex(source[i], palette, paletteCount, out var distance);
                error += distance;
            }

            indices |= (uint)index << (i * 2);
        }

        return new ColorBlockEncoding
        {
            Color0 = color0,
            Color1 = color1,
            Indices = indices,
            Error = error
        };
    }

    private static bool TrySolveColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        bool hasTransparent,
        uint indices,
        out ushort color0,
        out ushort color1)
    {
        var a00 = 0d;
        var a01 = 0d;
        var a11 = 0d;
        var b0Red = 0d;
        var b0Green = 0d;
        var b0Blue = 0d;
        var b1Red = 0d;
        var b1Green = 0d;
        var b1Blue = 0d;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (hasTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            var index = (int)((indices >> (i * 2)) & 0x3u);
            if (!TryGetColorEndpointWeights(index, hasTransparent, out var weight0, out var weight1))
            {
                continue;
            }

            a00 += weight0 * weight0;
            a01 += weight0 * weight1;
            a11 += weight1 * weight1;
            b0Red += weight0 * source[i].Red;
            b0Green += weight0 * source[i].Green;
            b0Blue += weight0 * source[i].Blue;
            b1Red += weight1 * source[i].Red;
            b1Green += weight1 * source[i].Green;
            b1Blue += weight1 * source[i].Blue;
        }

        var determinant = (a00 * a11) - (a01 * a01);
        if (Math.Abs(determinant) < 0.000001d)
        {
            color0 = 0;
            color1 = 0;
            return false;
        }

        color0 = PackRgb565Nearest(
            ((b0Red * a11) - (b1Red * a01)) / determinant,
            ((b0Green * a11) - (b1Green * a01)) / determinant,
            ((b0Blue * a11) - (b1Blue * a01)) / determinant);
        color1 = PackRgb565Nearest(
            ((a00 * b1Red) - (a01 * b0Red)) / determinant,
            ((a00 * b1Green) - (a01 * b0Green)) / determinant,
            ((a00 * b1Blue) - (a01 * b0Blue)) / determinant);
        NormalizeColorOrder(ref color0, ref color1, hasTransparent);
        return true;
    }

    private static bool TryGetColorEndpointWeights(
        int index,
        bool hasTransparent,
        out double weight0,
        out double weight1)
    {
        switch (index)
        {
            case 0:
                weight0 = 1d;
                weight1 = 0d;
                return true;
            case 1:
                weight0 = 0d;
                weight1 = 1d;
                return true;
            case 2:
                weight0 = hasTransparent ? 0.5d : 2d / 3d;
                weight1 = hasTransparent ? 0.5d : 1d / 3d;
                return true;
            case 3 when !hasTransparent:
                weight0 = 1d / 3d;
                weight1 = 2d / 3d;
                return true;
            default:
                weight0 = 0d;
                weight1 = 0d;
                return false;
        }
    }

    private static void RefineColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        bool hasTransparent,
        int passLimit,
        ref ColorBlockEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var improved = false;
            for (var endpoint = 0; endpoint < 2; endpoint++)
            {
                for (var component = 0; component < 3; component++)
                {
                    improved |= TryRefineColorEndpoint(source, colorMode, hasTransparent, endpoint, component, -1, ref best);
                    improved |= TryRefineColorEndpoint(source, colorMode, hasTransparent, endpoint, component, 1, ref best);
                }
            }

            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private static bool TryRefineColorEndpoint(
        ReadOnlySpan<Rgba8UNorm> source,
        Dxt1ColorMode colorMode,
        bool hasTransparent,
        int endpoint,
        int component,
        int delta,
        ref ColorBlockEncoding best)
    {
        GetRgb565Components(best.Color0, out var red0, out var green0, out var blue0);
        GetRgb565Components(best.Color1, out var red1, out var green1, out var blue1);

        if (endpoint == 0)
        {
            if (!TryOffsetRgb565Component(ref red0, ref green0, ref blue0, component, delta))
            {
                return false;
            }
        }
        else if (!TryOffsetRgb565Component(ref red1, ref green1, ref blue1, component, delta))
        {
            return false;
        }

        var color0 = PackRgb565FromComponents(red0, green0, blue0);
        var color1 = PackRgb565FromComponents(red1, green1, blue1);
        NormalizeColorOrder(ref color0, ref color1, hasTransparent);
        if (color0 == best.Color0 && color1 == best.Color1)
        {
            return false;
        }

        var candidate = EvaluateColorCandidate(source, colorMode, hasTransparent, color0, color1);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private static bool TryOffsetRgb565Component(
        ref int red,
        ref int green,
        ref int blue,
        int component,
        int delta)
    {
        switch (component)
        {
            case 0:
                return TryOffsetComponent(ref red, delta, 31);
            case 1:
                return TryOffsetComponent(ref green, delta, 63);
            case 2:
                return TryOffsetComponent(ref blue, delta, 31);
            default:
                throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static bool TryOffsetComponent(ref int value, int delta, int max)
    {
        var next = value + delta;
        if (next < 0 || next > max)
        {
            return false;
        }

        value = next;
        return true;
    }

    private static void UpdateBestColorEncoding(ColorBlockEncoding candidate, ref ColorBlockEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static bool TryFindPrincipalAxisColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out RgbVector minEndpoint,
        out RgbVector maxEndpoint)
    {
        var count = 0;
        var meanRed = 0d;
        var meanGreen = 0d;
        var meanBlue = 0d;
        var minRed = 255d;
        var minGreen = 255d;
        var minBlue = 255d;
        var maxRed = 0d;
        var maxGreen = 0d;
        var maxBlue = 0d;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            var red = source[i].Red;
            var green = source[i].Green;
            var blue = source[i].Blue;
            meanRed += red;
            meanGreen += green;
            meanBlue += blue;
            minRed = Math.Min(minRed, red);
            minGreen = Math.Min(minGreen, green);
            minBlue = Math.Min(minBlue, blue);
            maxRed = Math.Max(maxRed, red);
            maxGreen = Math.Max(maxGreen, green);
            maxBlue = Math.Max(maxBlue, blue);
            count++;
        }

        if (count == 0)
        {
            minEndpoint = default;
            maxEndpoint = default;
            return false;
        }

        meanRed /= count;
        meanGreen /= count;
        meanBlue /= count;

        var covRedRed = 0d;
        var covRedGreen = 0d;
        var covRedBlue = 0d;
        var covGreenGreen = 0d;
        var covGreenBlue = 0d;
        var covBlueBlue = 0d;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            var red = source[i].Red - meanRed;
            var green = source[i].Green - meanGreen;
            var blue = source[i].Blue - meanBlue;
            covRedRed += red * red;
            covRedGreen += red * green;
            covRedBlue += red * blue;
            covGreenGreen += green * green;
            covGreenBlue += green * blue;
            covBlueBlue += blue * blue;
        }

        var axisRed = maxRed - minRed;
        var axisGreen = maxGreen - minGreen;
        var axisBlue = maxBlue - minBlue;
        if (!NormalizeVector(ref axisRed, ref axisGreen, ref axisBlue))
        {
            minEndpoint = default;
            maxEndpoint = default;
            return false;
        }

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var nextRed = (covRedRed * axisRed) + (covRedGreen * axisGreen) + (covRedBlue * axisBlue);
            var nextGreen = (covRedGreen * axisRed) + (covGreenGreen * axisGreen) + (covGreenBlue * axisBlue);
            var nextBlue = (covRedBlue * axisRed) + (covGreenBlue * axisGreen) + (covBlueBlue * axisBlue);
            if (!NormalizeVector(ref nextRed, ref nextGreen, ref nextBlue))
            {
                break;
            }

            axisRed = nextRed;
            axisGreen = nextGreen;
            axisBlue = nextBlue;
        }

        var minProjection = double.PositiveInfinity;
        var maxProjection = double.NegativeInfinity;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            var projection = ((source[i].Red - meanRed) * axisRed)
                + ((source[i].Green - meanGreen) * axisGreen)
                + ((source[i].Blue - meanBlue) * axisBlue);
            minProjection = Math.Min(minProjection, projection);
            maxProjection = Math.Max(maxProjection, projection);
        }

        if (maxProjection - minProjection < 0.5d)
        {
            minEndpoint = default;
            maxEndpoint = default;
            return false;
        }

        minEndpoint = new RgbVector(
            meanRed + (axisRed * minProjection),
            meanGreen + (axisGreen * minProjection),
            meanBlue + (axisBlue * minProjection));
        maxEndpoint = new RgbVector(
            meanRed + (axisRed * maxProjection),
            meanGreen + (axisGreen * maxProjection),
            meanBlue + (axisBlue * maxProjection));
        return true;
    }

    private static int GetColorOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        TextureCompressionLevel.Exhaustive => 12,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported S3TC compression mode.")
    };

    private static int GetColorRefinementPassLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        TextureCompressionLevel.Exhaustive => 16,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported S3TC compression mode.")
    };

    private static bool TryFindFarthestColorEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out Rgb24 endpoint0,
        out Rgb24 endpoint1)
    {
        Span<Rgb24> colors = stackalloc Rgb24[TexelsPerBlock];
        var count = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            colors[count++] = new Rgb24(source[i].Red, source[i].Green, source[i].Blue);
        }

        var bestDistance = -1;
        endpoint0 = default;
        endpoint1 = default;
        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                var distance = ColorDistanceSquared(colors[i], colors[j]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    endpoint0 = colors[i];
                    endpoint1 = colors[j];
                }
            }
        }

        return bestDistance > 0;
    }

    private static bool TryFindAverageColor(
        ReadOnlySpan<Rgba8UNorm> source,
        bool ignoreTransparent,
        out RgbVector average)
    {
        var count = 0;
        var red = 0d;
        var green = 0d;
        var blue = 0d;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (ignoreTransparent && source[i].Alpha < AlphaCutoff)
            {
                continue;
            }

            red += source[i].Red;
            green += source[i].Green;
            blue += source[i].Blue;
            count++;
        }

        if (count == 0)
        {
            average = default;
            return false;
        }

        average = new RgbVector(red / count, green / count, blue / count);
        return true;
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
            (byte)(min.Red + (redRange / 16)),
            (byte)(min.Green + (greenRange / 16)),
            (byte)(min.Blue + (blueRange / 16)));
        insetMax = new Rgb24(
            (byte)(max.Red - (redRange / 16)),
            (byte)(max.Green - (greenRange / 16)),
            (byte)(max.Blue - (blueRange / 16)));
        return true;
    }

    private static bool NormalizeVector(ref double x, ref double y, ref double z)
    {
        var lengthSquared = (x * x) + (y * y) + (z * z);
        if (lengthSquared < 0.000001d)
        {
            return false;
        }

        var scale = 1d / Math.Sqrt(lengthSquared);
        x *= scale;
        y *= scale;
        z *= scale;
        return true;
    }

    private static void EncodeInterpolatedScalarBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        Span<byte> destination,
        TextureCompressionLevel compressionMode)
    {
        FindComponentBounds(source, component, out var min, out var max);
        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeInterpolatedScalarBlockFast(source, component, min, max, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
                EncodeInterpolatedScalarBlockOptimized(source, component, min, max, compressionMode, destination);
                return;
            case TextureCompressionLevel.Exhaustive:
                EncodeInterpolatedScalarBlockExhausted(source, component, destination);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(compressionMode),
                    compressionMode,
                    "Unsupported S3TC compression mode.");
        }
    }

    private static void EncodeInterpolatedScalarBlockFast(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        byte min,
        byte max,
        Span<byte> destination)
    {
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

    private static void EncodeInterpolatedScalarBlockOptimized(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        byte min,
        byte max,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var best = new ScalarBlockEncoding { Error = long.MaxValue };
        var iterationLimit = GetScalarOptimizationIterationLimit(compressionMode);
        OptimizeScalarSeed(source, component, min, max, AlphaEndpointMode.SixAlpha, iterationLimit, ref best);
        if (max > min)
        {
            OptimizeScalarSeed(source, component, max, min, AlphaEndpointMode.EightAlpha, iterationLimit, ref best);

            var padding = Math.Max(1, (max - min + 13) / 14);
            var expandedMin = (byte)Math.Max(byte.MinValue, min - padding);
            var expandedMax = (byte)Math.Min(byte.MaxValue, max + padding);
            OptimizeScalarSeed(
                source,
                component,
                expandedMin,
                expandedMax,
                AlphaEndpointMode.SixAlpha,
                iterationLimit,
                ref best);
            OptimizeScalarSeed(
                source,
                component,
                expandedMax,
                expandedMin,
                AlphaEndpointMode.EightAlpha,
                iterationLimit,
                ref best);
        }

        RefineScalarEndpoints(source, component, GetScalarRefinementPassLimit(compressionMode), ref best);
        WriteScalarBlock(best, destination);
    }

    private static void EncodeInterpolatedScalarBlockExhausted(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        Span<byte> destination)
    {
        var best = new ScalarBlockEncoding { Error = long.MaxValue };
        for (var endpoint0 = 0; endpoint0 <= byte.MaxValue; endpoint0++)
        {
            for (var endpoint1 = 0; endpoint1 <= byte.MaxValue; endpoint1++)
            {
                var candidate = EvaluateScalarCandidate(
                    source,
                    component,
                    (byte)endpoint0,
                    (byte)endpoint1,
                    best.Error);
                UpdateBestScalarEncoding(candidate, ref best);
                if (best.Error == 0)
                {
                    WriteScalarBlock(best, destination);
                    return;
                }
            }
        }

        WriteScalarBlock(best, destination);
    }

    private static void OptimizeScalarSeed(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        byte endpoint0,
        byte endpoint1,
        AlphaEndpointMode mode,
        int iterationLimit,
        ref ScalarBlockEncoding best)
    {
        NormalizeScalarOrder(ref endpoint0, ref endpoint1, mode);

        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var current = EvaluateScalarCandidate(source, component, endpoint0, endpoint1);
            UpdateBestScalarEncoding(current, ref best);
            if (current.Error == 0
                || !TrySolveScalarEndpoints(source, component, mode, current.Indices, out var nextEndpoint0, out var nextEndpoint1))
            {
                return;
            }

            if (nextEndpoint0 == endpoint0 && nextEndpoint1 == endpoint1)
            {
                return;
            }

            endpoint0 = nextEndpoint0;
            endpoint1 = nextEndpoint1;
        }
    }

    private static ScalarBlockEncoding EvaluateScalarCandidate(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        byte endpoint0,
        byte endpoint1,
        long maxError = long.MaxValue)
    {
        Span<byte> palette = stackalloc byte[8];
        BuildAlphaPalette(endpoint0, endpoint1, palette);

        ulong indices = 0;
        long error = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var value = GetComponent(source[i], component);
            var index = FindNearestAlphaIndex(value, palette);
            var difference = value - palette[index];
            error += difference * difference;
            indices |= (ulong)index << (i * 3);
            if (error >= maxError)
            {
                return new ScalarBlockEncoding
                {
                    Endpoint0 = endpoint0,
                    Endpoint1 = endpoint1,
                    Indices = indices,
                    Error = error
                };
            }
        }

        return new ScalarBlockEncoding
        {
            Endpoint0 = endpoint0,
            Endpoint1 = endpoint1,
            Indices = indices,
            Error = error
        };
    }

    private static bool TrySolveScalarEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        AlphaEndpointMode mode,
        ulong indices,
        out byte endpoint0,
        out byte endpoint1)
    {
        var a00 = 0d;
        var a01 = 0d;
        var a11 = 0d;
        var b0 = 0d;
        var b1 = 0d;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = (int)((indices >> (i * 3)) & 0x7u);
            if (!TryGetScalarEndpointWeights(index, mode, out var weight0, out var weight1))
            {
                continue;
            }

            var value = GetComponent(source[i], component);
            a00 += weight0 * weight0;
            a01 += weight0 * weight1;
            a11 += weight1 * weight1;
            b0 += weight0 * value;
            b1 += weight1 * value;
        }

        var determinant = (a00 * a11) - (a01 * a01);
        if (Math.Abs(determinant) < 0.000001d)
        {
            endpoint0 = 0;
            endpoint1 = 0;
            return false;
        }

        endpoint0 = ClampToByte(((b0 * a11) - (b1 * a01)) / determinant);
        endpoint1 = ClampToByte(((a00 * b1) - (a01 * b0)) / determinant);
        NormalizeScalarOrder(ref endpoint0, ref endpoint1, mode);
        return true;
    }

    private static bool TryGetScalarEndpointWeights(
        int index,
        AlphaEndpointMode mode,
        out double weight0,
        out double weight1)
    {
        if (mode == AlphaEndpointMode.EightAlpha)
        {
            switch (index)
            {
                case 0:
                    weight0 = 1d;
                    weight1 = 0d;
                    return true;
                case 1:
                    weight0 = 0d;
                    weight1 = 1d;
                    return true;
                default:
                    weight0 = (8d - index) / 7d;
                    weight1 = (index - 1d) / 7d;
                    return true;
            }
        }

        switch (index)
        {
            case 0:
                weight0 = 1d;
                weight1 = 0d;
                return true;
            case 1:
                weight0 = 0d;
                weight1 = 1d;
                return true;
            case >= 2 and <= 5:
                weight0 = (6d - index) / 5d;
                weight1 = (index - 1d) / 5d;
                return true;
            default:
                weight0 = 0d;
                weight1 = 0d;
                return false;
        }
    }

    private static int GetScalarOptimizationIterationLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported S3TC compression mode.")
    };

    private static int GetScalarRefinementPassLimit(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal => 4,
        TextureCompressionLevel.High => 8,
        _ => throw new ArgumentOutOfRangeException(
            nameof(compressionMode),
            compressionMode,
            "Unsupported S3TC compression mode.")
    };

    private static void RefineScalarEndpoints(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        int passLimit,
        ref ScalarBlockEncoding best)
    {
        for (var pass = 0; pass < passLimit; pass++)
        {
            var mode = best.Endpoint0 > best.Endpoint1
                ? AlphaEndpointMode.EightAlpha
                : AlphaEndpointMode.SixAlpha;
            var improved = false;
            improved |= TryRefineScalarEndpoint(source, component, mode, endpointIndex: 0, delta: -1, ref best);
            improved |= TryRefineScalarEndpoint(source, component, mode, endpointIndex: 0, delta: 1, ref best);
            improved |= TryRefineScalarEndpoint(source, component, mode, endpointIndex: 1, delta: -1, ref best);
            improved |= TryRefineScalarEndpoint(source, component, mode, endpointIndex: 1, delta: 1, ref best);
            if (!improved || best.Error == 0)
            {
                return;
            }
        }
    }

    private static bool TryRefineScalarEndpoint(
        ReadOnlySpan<Rgba8UNorm> source,
        S3tcScalarComponent component,
        AlphaEndpointMode mode,
        int endpointIndex,
        int delta,
        ref ScalarBlockEncoding best)
    {
        var endpoint0 = best.Endpoint0;
        var endpoint1 = best.Endpoint1;
        if (endpointIndex == 0)
        {
            if (!TryOffsetByte(ref endpoint0, delta))
            {
                return false;
            }
        }
        else if (!TryOffsetByte(ref endpoint1, delta))
        {
            return false;
        }

        NormalizeScalarOrder(ref endpoint0, ref endpoint1, mode);
        if (endpoint0 == best.Endpoint0 && endpoint1 == best.Endpoint1)
        {
            return false;
        }

        var candidate = EvaluateScalarCandidate(source, component, endpoint0, endpoint1);
        if (candidate.Error >= best.Error)
        {
            return false;
        }

        best = candidate;
        return true;
    }

    private static bool TryOffsetByte(ref byte value, int delta)
    {
        var next = value + delta;
        if (next < byte.MinValue || next > byte.MaxValue)
        {
            return false;
        }

        value = (byte)next;
        return true;
    }

    private static void NormalizeScalarOrder(ref byte endpoint0, ref byte endpoint1, AlphaEndpointMode mode)
    {
        if (mode == AlphaEndpointMode.EightAlpha)
        {
            if (endpoint0 < endpoint1)
            {
                (endpoint0, endpoint1) = (endpoint1, endpoint0);
            }
            else if (endpoint0 == endpoint1)
            {
                if (endpoint0 < byte.MaxValue)
                {
                    endpoint0++;
                }
                else
                {
                    endpoint1--;
                }
            }

            return;
        }

        if (endpoint0 > endpoint1)
        {
            (endpoint0, endpoint1) = (endpoint1, endpoint0);
        }
    }

    private static void UpdateBestScalarEncoding(ScalarBlockEncoding candidate, ref ScalarBlockEncoding best)
    {
        if (candidate.Error < best.Error)
        {
            best = candidate;
        }
    }

    private static void WriteScalarBlock(ScalarBlockEncoding encoding, Span<byte> destination)
    {
        destination[0] = encoding.Endpoint0;
        destination[1] = encoding.Endpoint1;
        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(encoding.Indices >> (8 * i));
        }
    }

    private static int FindNearestColorIndex(Rgba8UNorm color, ReadOnlySpan<Rgba8UNorm> palette, int paletteCount)
    {
        return FindNearestColorIndex(color, palette, paletteCount, out _);
    }

    private static int FindNearestColorIndex(
        Rgba8UNorm color,
        ReadOnlySpan<Rgba8UNorm> palette,
        int paletteCount,
        out int bestDistance)
    {
        var bestIndex = 0;
        bestDistance = int.MaxValue;
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

    private static bool HasOpaqueTexel(ReadOnlySpan<Rgba8UNorm> source)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (source[i].Alpha >= AlphaCutoff)
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

    private readonly record struct RgbVector(double Red, double Green, double Blue);

    private readonly record struct ColorEndpointPair(ushort Color0, ushort Color1);

    private struct ColorBlockEncoding
    {
        public ushort Color0;
        public ushort Color1;
        public uint Indices;
        public long Error;
    }

    private struct ScalarBlockEncoding
    {
        public byte Endpoint0;
        public byte Endpoint1;
        public ulong Indices;
        public long Error;
    }

    private enum S3tcScalarComponent
    {
        Red,
        Green,
        Alpha
    }

    private enum AlphaEndpointMode
    {
        EightAlpha,
        SixAlpha
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
