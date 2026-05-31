using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;
using TextureCompressor.Options;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

public sealed class EtcTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.RgbEtc1UNorm,
        TextureFormats.RgbEtc2UNorm,
        TextureFormats.RgbEtc2Srgb,
        TextureFormats.RgbA1Etc2UNorm,
        TextureFormats.RgbA1Etc2Srgb,
        TextureFormats.RgbaEtc2EacUNorm,
        TextureFormats.RgbaEtc2EacSrgb,
        TextureFormats.R11EacUNorm,
        TextureFormats.R11EacSNorm,
        TextureFormats.Rg11EacUNorm,
        TextureFormats.Rg11EacSNorm
    ];

    private static readonly int[] SEtcModifierTable =
    [
        -8, -2, 2, 8,
        -17, -5, 5, 17,
        -29, -9, 9, 29,
        -42, -13, 13, 42,
        -60, -18, 18, 60,
        -80, -24, 24, 80,
        -106, -33, 33, 106,
        -183, -47, 47, 183
    ];

    private static readonly int[] SEtcUnscramble = [2, 3, 1, 0];
    private static readonly int[] SEtcScramble = [3, 2, 0, 1];
    private static readonly int[] SEtcDistanceTable = [3, 6, 11, 16, 23, 32, 41, 64];

    private static readonly int[] SEacModifierTable =
    [
        -3, -6, -9, -15, 2, 5, 8, 14,
        -3, -7, -10, -13, 2, 6, 9, 12,
        -2, -5, -8, -13, 1, 4, 7, 12,
        -2, -4, -6, -13, 1, 3, 5, 12,
        -3, -6, -8, -12, 2, 5, 7, 11,
        -3, -7, -9, -11, 2, 6, 8, 10,
        -4, -7, -8, -11, 3, 6, 7, 10,
        -3, -5, -8, -11, 2, 4, 7, 10,
        -2, -6, -8, -10, 1, 5, 7, 9,
        -2, -5, -8, -10, 1, 4, 7, 9,
        -2, -4, -8, -10, 1, 3, 7, 9,
        -2, -5, -7, -10, 1, 4, 6, 9,
        -3, -4, -7, -10, 2, 3, 6, 9,
        -1, -2, -3, -10, 0, 1, 2, 9,
        -4, -6, -8, -9, 3, 5, 7, 8,
        -3, -5, -7, -9, 2, 4, 6, 8
    ];

    private readonly EtcTransfer _transfer;
    private readonly TextureCompressionOptions _options;

    public EtcTextureCoder(TextureFormat format, TextureCompressionOptions? options = null)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _options = options ?? new TextureCompressionOptions();
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
            case EtcTransfer.RgbEtc1UNorm:
                DecodeColor<TPixel, RgbEtc1Transfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.RgbEtc2UNorm:
                DecodeColor<TPixel, RgbEtc2Transfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.RgbEtc2Srgb:
                DecodeColor<TPixel, RgbEtc2SrgbTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.RgbA1Etc2UNorm:
                DecodeColor<TPixel, RgbA1Etc2Transfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.RgbA1Etc2Srgb:
                DecodeColor<TPixel, RgbA1Etc2SrgbTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.RgbaEtc2EacUNorm:
                DecodeColor<TPixel, RgbaEtc2EacTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.RgbaEtc2EacSrgb:
                DecodeColor<TPixel, RgbaEtc2EacSrgbTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.R11EacUNorm:
                DecodeUnsignedEac<TPixel, R11EacTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.R11EacSNorm:
                DecodeSignedEac<TPixel, R11EacSignedTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.Rg11EacUNorm:
                DecodeUnsignedEac<TPixel, Rg11EacTransfer>(source, destination, rowPitch);
                return;
            case EtcTransfer.Rg11EacSNorm:
                DecodeSignedEac<TPixel, Rg11EacSignedTransfer>(source, destination, rowPitch);
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
            case EtcTransfer.RgbEtc1UNorm:
                EncodeColor<TPixel, RgbEtc1Transfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.RgbEtc2UNorm:
                EncodeColor<TPixel, RgbEtc2Transfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.RgbEtc2Srgb:
                EncodeColor<TPixel, RgbEtc2SrgbTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.RgbA1Etc2UNorm:
                EncodeColor<TPixel, RgbA1Etc2Transfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.RgbA1Etc2Srgb:
                EncodeColor<TPixel, RgbA1Etc2SrgbTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.RgbaEtc2EacUNorm:
                EncodeColor<TPixel, RgbaEtc2EacTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.RgbaEtc2EacSrgb:
                EncodeColor<TPixel, RgbaEtc2EacSrgbTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.R11EacUNorm:
                EncodeUnsignedEac<TPixel, R11EacTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.R11EacSNorm:
                EncodeSignedEac<TPixel, R11EacSignedTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.Rg11EacUNorm:
                EncodeUnsignedEac<TPixel, Rg11EacTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            case EtcTransfer.Rg11EacSNorm:
                EncodeSignedEac<TPixel, Rg11EacSignedTransfer>(source, destination, rowPitch, _options.CompressionMode);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    internal static void EncodeUnsignedR11Block(ReadOnlySpan<int> source, Span<byte> destination)
    {
        var targets = new IntBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            targets[i] = Math.Clamp(source[i], 0, 2047);
        }

        EncodeEacBlock(ref targets, EacBlockKind.Unsigned11, TextureCompressionLevel.Fast, destination);
    }

    private static void DecodeColor<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IEtcColorTransfer
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

    private static void EncodeColor<TPixel, TTransfer>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        TextureCompressionLevel compressionMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IEtcColorTransfer
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
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void DecodeUnsignedEac<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IUnsignedEacTransfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        Span<Rgba16UNorm> block = stackalloc Rgba16UNorm[TexelsPerBlock];

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

    private static void EncodeUnsignedEac<TPixel, TTransfer>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        TextureCompressionLevel compressionMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IUnsignedEacTransfer
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
                        Span<Rgba16UNorm> block = stackalloc Rgba16UNorm[TexelsPerBlock];

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

        Span<Rgba16UNorm> block = stackalloc Rgba16UNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static void DecodeSignedEac<TPixel, TTransfer>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : ISignedEacTransfer
    {
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);
        Span<Rgba16SNorm> block = stackalloc Rgba16SNorm[TexelsPerBlock];

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

    private static void EncodeSignedEac<TPixel, TTransfer>(
        BitmapView<TPixel> source,
        Span<byte> destination,
        int rowPitch,
        TextureCompressionLevel compressionMode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : ISignedEacTransfer
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
                        Span<Rgba16SNorm> block = stackalloc Rgba16SNorm[TexelsPerBlock];

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

        Span<Rgba16SNorm> block = stackalloc Rgba16SNorm[TexelsPerBlock];

        var rowOffset = 0;
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var blockOffset = rowOffset;
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadBlock(source, blockX, blockY, block);
                TTransfer.EncodeBlock(block, destination.Slice(blockOffset, TTransfer.BytesPerBlock), compressionMode);
                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IEtcColorTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode);
    }

    private readonly struct RgbEtc1Transfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeEtcColorBlock(source, etc2: false, punchthrough: false, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeEtcColorBlock(source, punchthrough: false, etc2: false, compressionMode, destination);
    }

    private readonly struct RgbEtc2Transfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeEtcColorBlock(source, etc2: true, punchthrough: false, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeEtcColorBlock(source, punchthrough: false, etc2: true, compressionMode, destination);
    }

    private readonly struct RgbEtc2SrgbTransfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeEtcColorBlock(source, etc2: true, punchthrough: false, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeSrgbEtcColorBlock(source, punchthrough: false, compressionMode, destination);
    }

    private readonly struct RgbA1Etc2Transfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeEtcColorBlock(source, etc2: true, punchthrough: true, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeEtcColorBlock(source, punchthrough: true, etc2: true, compressionMode, destination);
    }

    private readonly struct RgbA1Etc2SrgbTransfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
        {
            DecodeEtcColorBlock(source, etc2: true, punchthrough: true, destination);
            DecodeSrgbColors(destination);
        }

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeSrgbEtcColorBlock(source, punchthrough: true, compressionMode, destination);
    }

    private readonly struct RgbaEtc2EacTransfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeEtc2RgbaBlock(source, srgb: false, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeEtc2RgbaBlock(source, srgb: false, compressionMode, destination);
    }

    private readonly struct RgbaEtc2EacSrgbTransfer : IEtcColorTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination) =>
            DecodeEtc2RgbaBlock(source, srgb: true, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba8UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeEtc2RgbaBlock(source, srgb: true, compressionMode, destination);
    }

    private interface IUnsignedEacTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba16UNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba16UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode);
    }

    private readonly struct R11EacTransfer : IUnsignedEacTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba16UNorm> destination) =>
            DecodeUnsignedEacRBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba16UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeUnsignedEacRBlock(source, compressionMode, destination);
    }

    private readonly struct Rg11EacTransfer : IUnsignedEacTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba16UNorm> destination) =>
            DecodeUnsignedEacRgBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba16UNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeUnsignedEacRgBlock(source, compressionMode, destination);
    }

    private interface ISignedEacTransfer
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba16SNorm> destination);

        static abstract void EncodeBlock(ReadOnlySpan<Rgba16SNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode);
    }

    private readonly struct R11EacSignedTransfer : ISignedEacTransfer
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba16SNorm> destination) =>
            DecodeSignedEacRBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba16SNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeSignedEacRBlock(source, compressionMode, destination);
    }

    private readonly struct Rg11EacSignedTransfer : ISignedEacTransfer
    {
        public static int BytesPerBlock => 16;

        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<Rgba16SNorm> destination) =>
            DecodeSignedEacRgBlock(source, destination);

        public static void EncodeBlock(ReadOnlySpan<Rgba16SNorm> source, Span<byte> destination, TextureCompressionLevel compressionMode) =>
            EncodeSignedEacRgBlock(source, compressionMode, destination);
    }

    private static void DecodeEtc2RgbaBlock(ReadOnlySpan<byte> source, bool srgb, Span<Rgba8UNorm> destination)
    {
        DecodeEtcColorBlock(source[8..], etc2: true, punchthrough: false, destination);
        if (srgb)
        {
            DecodeSrgbColors(destination);
        }

        DecodeEtc2AlphaBlock(source[..8], destination);
    }

    private static void EncodeEtc2RgbaBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        bool srgb,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        EncodeEtc2AlphaBlock(source, compressionMode, destination[..8]);
        if (srgb)
        {
            EncodeSrgbEtcColorBlock(source, punchthrough: false, compressionMode, destination[8..]);
            return;
        }

        EncodeEtcColorBlock(source, punchthrough: false, etc2: true, compressionMode, destination[8..]);
    }

    private static void DecodeEtcColorBlock(
        ReadOnlySpan<byte> source,
        bool etc2,
        bool punchthrough,
        Span<Rgba8UNorm> destination)
    {
        var high = BinaryPrimitives.ReadUInt32BigEndian(source);
        var low = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
        var colors = new EtcColorBlock();

        if (punchthrough)
        {
            DecodeEtc2PunchthroughBlock(high, low, ref colors);
        }
        else if (etc2)
        {
            DecodeEtc2RgbBlock(high, low, ref colors);
        }
        else
        {
            DecodeDiffFlip(high, low, forceDifferential: false, transparentPunchthrough: false, ref colors);
        }

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            ref readonly var color = ref colors[i];
            destination[i] = new Rgba8UNorm(color.Red, color.Green, color.Blue, color.Alpha);
        }
    }

    private static void DecodeEtc2RgbBlock(uint high, uint low, ref EtcColorBlock destination)
    {
        if (IsDifferentialMode(high) && TryGetInvalidDifferentialChannel(high, out var channel))
        {
            DecodeEtc2InvalidMode(high, low, channel, transparentPunchthrough: false, ref destination);
            return;
        }

        DecodeDiffFlip(high, low, forceDifferential: false, transparentPunchthrough: false, ref destination);
    }

    private static void DecodeEtc2PunchthroughBlock(uint high, uint low, ref EtcColorBlock destination)
    {
        var opaque = IsDifferentialMode(high);
        if (TryGetInvalidDifferentialChannel(high, out var channel))
        {
            if (channel == 2)
            {
                DecodeEtc2InvalidMode(high, low, channel, transparentPunchthrough: false, ref destination);
                return;
            }

            DecodeEtc2InvalidMode(high, low, channel, transparentPunchthrough: !opaque, ref destination);
            return;
        }

        DecodeDiffFlip(high, low, forceDifferential: true, transparentPunchthrough: !opaque, ref destination);
    }

    private static void DecodeEtc2InvalidMode(
        uint high,
        uint low,
        int invalidChannel,
        bool transparentPunchthrough,
        ref EtcColorBlock destination)
    {
        switch (invalidChannel)
        {
            case 0:
                Unstuff59Bits(high, low, out var tHigh, out var tLow);
                DecodeTMode(tHigh, tLow, transparentPunchthrough, ref destination);
                break;
            case 1:
                Unstuff58Bits(high, low, out var hHigh, out var hLow);
                DecodeHMode(hHigh, hLow, transparentPunchthrough, ref destination);
                break;
            case 2:
                Unstuff57Bits(high, low, out var pHigh, out var pLow);
                DecodePlanarMode(pHigh, pLow, ref destination);
                break;
        }
    }

    private static void DecodeDiffFlip(
        uint high,
        uint low,
        bool forceDifferential,
        bool transparentPunchthrough,
        ref EtcColorBlock destination)
    {
        var differential = forceDifferential || IsDifferentialMode(high);
        var flip = GetBitsHigh(high, 1, 32) != 0;
        var baseColors = new InlineArray2<EtcColor>();
        var tables = new InlineArray2<int>();
        tables[0] = (int)GetBitsHigh(high, 3, 39);
        tables[1] = (int)GetBitsHigh(high, 3, 36);

        if (differential)
        {
            var r0 = (int)GetBitsHigh(high, 5, 63);
            var g0 = (int)GetBitsHigh(high, 5, 55);
            var b0 = (int)GetBitsHigh(high, 5, 47);
            var r1 = r0 + SignExtend3((int)GetBitsHigh(high, 3, 58));
            var g1 = g0 + SignExtend3((int)GetBitsHigh(high, 3, 50));
            var b1 = b0 + SignExtend3((int)GetBitsHigh(high, 3, 42));
            baseColors[0] = new EtcColor(Expand5To8(r0), Expand5To8(g0), Expand5To8(b0));
            baseColors[1] = new EtcColor(Expand5To8(r1), Expand5To8(g1), Expand5To8(b1));
        }
        else
        {
            baseColors[0] = new EtcColor(
                Expand4To8((int)GetBitsHigh(high, 4, 63)),
                Expand4To8((int)GetBitsHigh(high, 4, 55)),
                Expand4To8((int)GetBitsHigh(high, 4, 47)));
            baseColors[1] = new EtcColor(
                Expand4To8((int)GetBitsHigh(high, 4, 59)),
                Expand4To8((int)GetBitsHigh(high, 4, 51)),
                Expand4To8((int)GetBitsHigh(high, 4, 43)));
        }

        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var texel = (y * BlockSize) + x;
                var subblock = flip ? (y < 2 ? 0 : 1) : (x < 2 ? 0 : 1);
                var modifierIndex = SEtcUnscramble[GetEtcRawIndex(low, x, y)];
                if (transparentPunchthrough && modifierIndex == 1)
                {
                    destination[texel] = new EtcColor(0, 0, 0, 0);
                    continue;
                }

                var modifier = GetEtcModifier(tables[subblock], modifierIndex, transparentPunchthrough);
                ref readonly var baseColor = ref baseColors[subblock];
                destination[texel] = new EtcColor(
                    ClampToByte(baseColor.Red + modifier),
                    ClampToByte(baseColor.Green + modifier),
                    ClampToByte(baseColor.Blue + modifier));
            }
        }
    }

    private static void DecodePlanarMode(uint high, uint low, ref EtcColorBlock destination)
    {
        var origin = new EtcColor(
            Expand6To8((int)GetBitsHigh(high, 6, 63)),
            Expand7To8((int)GetBitsHigh(high, 7, 57)),
            Expand6To8((int)GetBitsHigh(high, 6, 50)));
        var horizontal = new EtcColor(
            Expand6To8((int)GetBitsHigh(high, 6, 44)),
            Expand7To8((int)GetBitsHigh(high, 7, 38)),
            Expand6To8((int)GetBits(low, 6, 31)));
        var vertical = new EtcColor(
            Expand6To8((int)GetBits(low, 6, 25)),
            Expand7To8((int)GetBits(low, 7, 19)),
            Expand6To8((int)GetBits(low, 6, 12)));

        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                destination[(y * BlockSize) + x] = new EtcColor(
                    ClampToByte(((x * (horizontal.Red - origin.Red)) + (y * (vertical.Red - origin.Red)) + (4 * origin.Red) + 2) >> 2),
                    ClampToByte(((x * (horizontal.Green - origin.Green)) + (y * (vertical.Green - origin.Green)) + (4 * origin.Green) + 2) >> 2),
                    ClampToByte(((x * (horizontal.Blue - origin.Blue)) + (y * (vertical.Blue - origin.Blue)) + (4 * origin.Blue) + 2) >> 2));
            }
        }
    }

    private static void DecodeHMode(uint high, uint low, bool transparentPunchthrough, ref EtcColorBlock destination)
    {
        var color0 = new EtcColor(
            Expand4To8((int)GetBitsHigh(high, 4, 57)),
            Expand4To8((int)GetBitsHigh(high, 4, 53)),
            Expand4To8((int)GetBitsHigh(high, 4, 49)));
        var color1 = new EtcColor(
            Expand4To8((int)GetBitsHigh(high, 4, 45)),
            Expand4To8((int)GetBitsHigh(high, 4, 41)),
            Expand4To8((int)GetBitsHigh(high, 4, 37)));
        var distance = (int)GetBitsHigh(high, 2, 33) << 1;
        if (GetBitsHigh(high, 12, 57) >= GetBitsHigh(high, 12, 45))
        {
            distance |= 1;
        }

        var paint = new InlineArray4<EtcColor>();
        BuildHModePaintColors(color0, color1, distance, paint);
        DecodeTOrHModeTexels(low, transparentPunchthrough, paint, ref destination);
    }

    private static void DecodeTMode(uint high, uint low, bool transparentPunchthrough, ref EtcColorBlock destination)
    {
        var color0 = new EtcColor(
            Expand4To8((int)GetBitsHigh(high, 4, 58)),
            Expand4To8((int)GetBitsHigh(high, 4, 54)),
            Expand4To8((int)GetBitsHigh(high, 4, 50)));
        var color1 = new EtcColor(
            Expand4To8((int)GetBitsHigh(high, 4, 46)),
            Expand4To8((int)GetBitsHigh(high, 4, 42)),
            Expand4To8((int)GetBitsHigh(high, 4, 38)));
        var distance = (int)GetBitsHigh(high, 3, 34);
        var paint = new InlineArray4<EtcColor>();
        BuildTModePaintColors(color0, color1, distance, paint);
        DecodeTOrHModeTexels(low, transparentPunchthrough, paint, ref destination);
    }

    private static void DecodeTOrHModeTexels(
        uint low,
        bool transparentPunchthrough,
        ReadOnlySpan<EtcColor> paint,
        ref EtcColorBlock destination)
    {
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var texel = (y * BlockSize) + x;
                var index = GetEtcRawIndex(low, x, y);
                if (transparentPunchthrough && index == 2)
                {
                    destination[texel] = new EtcColor(0, 0, 0, 0);
                    continue;
                }

                destination[texel] = paint[index];
            }
        }
    }

    private static void BuildHModePaintColors(EtcColor color0, EtcColor color1, int distanceIndex, Span<EtcColor> paint)
    {
        var distance = SEtcDistanceTable[distanceIndex];
        paint[0] = AddScalar(color0, distance);
        paint[1] = AddScalar(color0, -distance);
        paint[2] = AddScalar(color1, distance);
        paint[3] = AddScalar(color1, -distance);
    }

    private static void BuildTModePaintColors(EtcColor color0, EtcColor color1, int distanceIndex, Span<EtcColor> paint)
    {
        var distance = SEtcDistanceTable[distanceIndex];
        paint[0] = color0;
        paint[1] = AddScalar(color1, distance);
        paint[2] = color1;
        paint[3] = AddScalar(color1, -distance);
    }

    private static void DecodeEtc2AlphaBlock(ReadOnlySpan<byte> source, Span<Rgba8UNorm> destination)
    {
        var baseCodeword = source[0];
        var table = source[1] & 0xf;
        var multiplier = source[1] >> 4;
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var index = ReadEacIndex(source, x, y);
                var modifier = GetEacModifier(table, index) * multiplier;
                destination[(y * BlockSize) + x].Alpha = ClampToByte(baseCodeword + modifier);
            }
        }
    }

    private static void EncodeEtc2AlphaBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var alpha = new IntBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            alpha[i] = source[i].Alpha;
        }

        EncodeEacBlock(ref alpha, EacBlockKind.Alpha8, compressionMode, destination);
    }

    private static void DecodeUnsignedEacRBlock(ReadOnlySpan<byte> source, Span<Rgba16UNorm> destination)
    {
        InitializeUnsignedEacBlock(destination);
        DecodeUnsignedEacComponent(source, component: 0, destination);
    }

    private static void DecodeUnsignedEacRgBlock(ReadOnlySpan<byte> source, Span<Rgba16UNorm> destination)
    {
        InitializeUnsignedEacBlock(destination);
        DecodeUnsignedEacComponent(source[..8], component: 0, destination);
        DecodeUnsignedEacComponent(source[8..], component: 1, destination);
    }

    private static void DecodeSignedEacRBlock(ReadOnlySpan<byte> source, Span<Rgba16SNorm> destination)
    {
        InitializeSignedEacBlock(destination);
        DecodeSignedEacComponent(source, component: 0, destination);
    }

    private static void DecodeSignedEacRgBlock(ReadOnlySpan<byte> source, Span<Rgba16SNorm> destination)
    {
        InitializeSignedEacBlock(destination);
        DecodeSignedEacComponent(source[..8], component: 0, destination);
        DecodeSignedEacComponent(source[8..], component: 1, destination);
    }

    private static void DecodeUnsignedEacComponent(ReadOnlySpan<byte> source, int component, Span<Rgba16UNorm> destination)
    {
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var texel = (y * BlockSize) + x;
                var index = ReadEacIndex(source, x, y);
                var decoded = Unsigned11ToUNorm16(DecodeUnsignedEac11(source[0], source[1], index));
                if (component == 0)
                {
                    destination[texel].Red = decoded;
                }
                else
                {
                    destination[texel].Green = decoded;
                }
            }
        }
    }

    private static void DecodeSignedEacComponent(ReadOnlySpan<byte> source, int component, Span<Rgba16SNorm> destination)
    {
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var texel = (y * BlockSize) + x;
                var index = ReadEacIndex(source, x, y);
                var decoded = Signed11ToSNorm16(DecodeSignedEac11(source[0], source[1], index));
                if (component == 0)
                {
                    destination[texel].Red = decoded;
                }
                else
                {
                    destination[texel].Green = decoded;
                }
            }
        }
    }

    private static void EncodeUnsignedEacRBlock(
        ReadOnlySpan<Rgba16UNorm> source,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var red = new IntBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            red[i] = UNorm16ToUnsigned11(source[i].Red);
        }

        EncodeEacBlock(ref red, EacBlockKind.Unsigned11, compressionMode, destination);
    }

    private static void EncodeUnsignedEacRgBlock(
        ReadOnlySpan<Rgba16UNorm> source,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var red = new IntBlock();
        var green = new IntBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            red[i] = UNorm16ToUnsigned11(source[i].Red);
            green[i] = UNorm16ToUnsigned11(source[i].Green);
        }

        EncodeEacBlock(ref red, EacBlockKind.Unsigned11, compressionMode, destination[..8]);
        EncodeEacBlock(ref green, EacBlockKind.Unsigned11, compressionMode, destination[8..]);
    }

    private static void EncodeSignedEacRBlock(
        ReadOnlySpan<Rgba16SNorm> source,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var red = new IntBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            red[i] = SNorm16ToSigned11(source[i].Red);
        }

        EncodeEacBlock(ref red, EacBlockKind.Signed11, compressionMode, destination);
    }

    private static void EncodeSignedEacRgBlock(
        ReadOnlySpan<Rgba16SNorm> source,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var red = new IntBlock();
        var green = new IntBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            red[i] = SNorm16ToSigned11(source[i].Red);
            green[i] = SNorm16ToSigned11(source[i].Green);
        }

        EncodeEacBlock(ref red, EacBlockKind.Signed11, compressionMode, destination[..8]);
        EncodeEacBlock(ref green, EacBlockKind.Signed11, compressionMode, destination[8..]);
    }

    private static void EncodeSrgbEtcColorBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        bool punchthrough,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        Span<Rgba8UNorm> srgbBlock = stackalloc Rgba8UNorm[TexelsPerBlock];
        EncodeSrgbColors(source, srgbBlock);
        EncodeEtcColorBlock(srgbBlock, punchthrough, etc2: true, compressionMode, destination);
    }

    private static void EncodeEtcColorBlock(
        ReadOnlySpan<Rgba8UNorm> source,
        bool punchthrough,
        bool etc2,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        var colors = new EtcColorBlock();
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            colors[i] = new EtcColor(
                source[i].Red,
                source[i].Green,
                source[i].Blue,
                source[i].Alpha);
        }

        var encoded = compressionMode switch
        {
            TextureCompressionLevel.Fast => EncodeEtcColorBlockFast(ref colors, punchthrough),
            TextureCompressionLevel.Normal or TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive =>
                EncodeEtcColorBlockOptimized(ref colors, punchthrough, etc2, compressionMode),
            _ => throw CreateUnsupportedCompressionModeException(compressionMode)
        };

        BinaryPrimitives.WriteUInt32BigEndian(destination, encoded.High);
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..], encoded.Low);
    }

    private static EtcColorEncoding EncodeEtcColorBlockFast(ref EtcColorBlock colors, bool punchthrough) =>
        punchthrough
            ? EncodeDifferential(ref colors, HasTransparentTexel(ref colors))
            : BestEncoding(EncodeIndividual(ref colors), EncodeDifferential(ref colors, transparentPunchthrough: false));

    private static EtcColorEncoding EncodeEtcColorBlockOptimized(
        ref EtcColorBlock colors,
        bool punchthrough,
        bool etc2,
        TextureCompressionLevel compressionMode)
    {
        var hasTransparent = punchthrough && HasTransparentTexel(ref colors);
        var best = compressionMode == TextureCompressionLevel.Exhaustive
            ? EncodeEtcColorBlockOptimized(ref colors, punchthrough, etc2, TextureCompressionLevel.High)
            : RecalculateEtcColorEncodingError(
                ref colors,
                EncodeEtcColorBlockFast(ref colors, punchthrough),
                punchthrough,
                etc2,
                hasTransparent);
        var differential = RecalculateEtcColorEncodingError(
            ref colors,
            EncodeDifferentialOptimized(ref colors, hasTransparent, compressionMode),
            punchthrough,
            etc2,
            hasTransparent);
        best = punchthrough
            ? BestEncoding(best, differential)
            : BestEncoding(
                best,
                BestEncoding(
                    RecalculateEtcColorEncodingError(
                        ref colors,
                        EncodeIndividualOptimized(ref colors, compressionMode),
                        punchthrough,
                        etc2,
                        transparentPunchthrough: false),
                    differential));

        if (etc2 && !hasTransparent)
        {
            best = BestEncoding(
                best,
                RecalculateEtcColorEncodingError(
                    ref colors,
                    EncodePlanarModeHigh(ref colors),
                    punchthrough,
                    etc2,
                    transparentPunchthrough: false));

            if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive)
            {
                best = BestEncoding(
                    best,
                    RecalculateEtcColorEncodingError(
                        ref colors,
                        EncodeTModeHigh(ref colors, transparentPunchthrough: false, compressionMode),
                        punchthrough,
                        etc2,
                        transparentPunchthrough: false));
                best = BestEncoding(
                    best,
                    RecalculateEtcColorEncodingError(
                        ref colors,
                        EncodeHModeHigh(ref colors, transparentPunchthrough: false, compressionMode),
                        punchthrough,
                        etc2,
                        transparentPunchthrough: false));
            }
        }

        return best;
    }

    private static EtcColorEncoding EncodeIndividual(ref EtcColorBlock colors)
    {
        var best = EtcColorEncoding.Worst;
        for (var flip = 0; flip <= 1; flip++)
        {
            var high = (uint)flip;
            EncodeIndividualSubblock(ref colors, flip != 0, 0, out var sub0);
            EncodeIndividualSubblock(ref colors, flip != 0, 1, out var sub1);
            high |= (uint)sub0.Red << 28;
            high |= (uint)sub1.Red << 24;
            high |= (uint)sub0.Green << 20;
            high |= (uint)sub1.Green << 16;
            high |= (uint)sub0.Blue << 12;
            high |= (uint)sub1.Blue << 8;
            high |= (uint)sub0.Table << 5;
            high |= (uint)sub1.Table << 2;
            high |= (uint)flip;
            var candidate = new EtcColorEncoding(sub0.Error + sub1.Error, high, sub0.Low | sub1.Low);
            best = BestEncoding(best, candidate);
        }

        return best;
    }

    private static void EncodeIndividualSubblock(
        ref EtcColorBlock colors,
        bool flip,
        int subblock,
        out IndividualSubblockEncoding encoding)
    {
        AverageSubblock(ref colors, flip, subblock, ignoreTransparent: false, out var average);
        var red = QuantizeByte(average.Red, 15);
        var green = QuantizeByte(average.Green, 15);
        var blue = QuantizeByte(average.Blue, 15);
        var baseColor = new EtcColor(Expand4To8(red), Expand4To8(green), Expand4To8(blue));
        FindBestTableAndIndices(ref colors, flip, subblock, baseColor, transparentPunchthrough: false, out var table, out var low, out var error);
        encoding = new IndividualSubblockEncoding(red, green, blue, table, error, low);
    }

    private static EtcColorEncoding EncodeIndividualOptimized(
        ref EtcColorBlock colors,
        TextureCompressionLevel compressionMode)
    {
        var best = EtcColorEncoding.Worst;
        for (var flip = 0; flip <= 1; flip++)
        {
            var high = (uint)flip;
            EncodeIndividualSubblockOptimized(ref colors, flip != 0, 0, compressionMode, out var sub0);
            EncodeIndividualSubblockOptimized(ref colors, flip != 0, 1, compressionMode, out var sub1);
            high |= (uint)sub0.Red << 28;
            high |= (uint)sub1.Red << 24;
            high |= (uint)sub0.Green << 20;
            high |= (uint)sub1.Green << 16;
            high |= (uint)sub0.Blue << 12;
            high |= (uint)sub1.Blue << 8;
            high |= (uint)sub0.Table << 5;
            high |= (uint)sub1.Table << 2;
            high |= (uint)flip;
            var candidate = new EtcColorEncoding(sub0.Error + sub1.Error, high, sub0.Low | sub1.Low);
            best = BestEncoding(best, candidate);
        }

        return best;
    }

    private static void EncodeIndividualSubblockOptimized(
        ref EtcColorBlock colors,
        bool flip,
        int subblock,
        TextureCompressionLevel compressionMode,
        out IndividualSubblockEncoding encoding)
    {
        AverageSubblock(ref colors, flip, subblock, ignoreTransparent: false, out var average);
        var redCenter = QuantizeByte(average.Red, 15);
        var greenCenter = QuantizeByte(average.Green, 15);
        var blueCenter = QuantizeByte(average.Blue, 15);
        encoding = new IndividualSubblockEncoding(redCenter, greenCenter, blueCenter, 0, long.MaxValue, 0);

        var radius = GetIndividualEndpointSearchRadius(compressionMode);
        for (var red = Math.Max(0, redCenter - radius); red <= Math.Min(15, redCenter + radius); red++)
        {
            for (var green = Math.Max(0, greenCenter - radius); green <= Math.Min(15, greenCenter + radius); green++)
            {
                for (var blue = Math.Max(0, blueCenter - radius); blue <= Math.Min(15, blueCenter + radius); blue++)
                {
                    var baseColor = new EtcColor(Expand4To8(red), Expand4To8(green), Expand4To8(blue));
                    FindBestTableAndIndices(
                        ref colors,
                        flip,
                        subblock,
                        baseColor,
                        transparentPunchthrough: false,
                        out var table,
                        out var low,
                        out var error);
                    if (error < encoding.Error)
                    {
                        encoding = new IndividualSubblockEncoding(red, green, blue, table, error, low);
                    }
                }
            }
        }
    }

    private static EtcColorEncoding EncodeDifferential(ref EtcColorBlock colors, bool transparentPunchthrough)
    {
        var best = EtcColorEncoding.Worst;
        for (var flip = 0; flip <= 1; flip++)
        {
            AverageSubblock(ref colors, flip != 0, 0, transparentPunchthrough, out var average0);
            AverageSubblock(ref colors, flip != 0, 1, transparentPunchthrough, out var average1);

            var red0 = QuantizeByte(average0.Red, 31);
            var green0 = QuantizeByte(average0.Green, 31);
            var blue0 = QuantizeByte(average0.Blue, 31);
            var red1 = QuantizeByte(average1.Red, 31);
            var green1 = QuantizeByte(average1.Green, 31);
            var blue1 = QuantizeByte(average1.Blue, 31);

            for (var r0 = Math.Max(0, red0 - 1); r0 <= Math.Min(31, red0 + 1); r0++)
            {
                var r1 = Math.Clamp(red1, r0 - 4, r0 + 3);
                if ((uint)r1 > 31)
                {
                    continue;
                }

                for (var g0 = Math.Max(0, green0 - 1); g0 <= Math.Min(31, green0 + 1); g0++)
                {
                    var g1 = Math.Clamp(green1, g0 - 4, g0 + 3);
                    if ((uint)g1 > 31)
                    {
                        continue;
                    }

                    for (var b0 = Math.Max(0, blue0 - 1); b0 <= Math.Min(31, blue0 + 1); b0++)
                    {
                        var b1 = Math.Clamp(blue1, b0 - 4, b0 + 3);
                        if ((uint)b1 > 31)
                        {
                            continue;
                        }

                        var base0 = new EtcColor(Expand5To8(r0), Expand5To8(g0), Expand5To8(b0));
                        var base1 = new EtcColor(Expand5To8(r1), Expand5To8(g1), Expand5To8(b1));
                        FindBestTableAndIndices(ref colors, flip != 0, 0, base0, transparentPunchthrough, out var table0, out var low0, out var error0);
                        FindBestTableAndIndices(ref colors, flip != 0, 1, base1, transparentPunchthrough, out var table1, out var low1, out var error1);

                        var high = ((uint)r0 << 27) |
                                   ((uint)PackSigned3(r1 - r0) << 24) |
                                   ((uint)g0 << 19) |
                                   ((uint)PackSigned3(g1 - g0) << 16) |
                                   ((uint)b0 << 11) |
                                   ((uint)PackSigned3(b1 - b0) << 8) |
                                   ((uint)table0 << 5) |
                                   ((uint)table1 << 2) |
                                   (transparentPunchthrough ? 0u : 0x2u) |
                                   (uint)flip;
                        var candidate = new EtcColorEncoding(error0 + error1, high, low0 | low1);
                        best = BestEncoding(best, candidate);
                    }
                }
            }
        }

        return best;
    }

    private static EtcColorEncoding EncodeDifferentialOptimized(
        ref EtcColorBlock colors,
        bool transparentPunchthrough,
        TextureCompressionLevel compressionMode)
    {
        var best = EtcColorEncoding.Worst;
        var capacity = GetDifferentialSubblockCandidateCapacity(compressionMode);
        Span<DifferentialSubblockEncoding> subblock0 = stackalloc DifferentialSubblockEncoding[capacity];
        Span<DifferentialSubblockEncoding> subblock1 = stackalloc DifferentialSubblockEncoding[capacity];
        for (var flip = 0; flip <= 1; flip++)
        {
            var count0 = BuildDifferentialSubblockCandidates(
                ref colors,
                flip != 0,
                subblock: 0,
                transparentPunchthrough,
                compressionMode,
                subblock0);
            var count1 = BuildDifferentialSubblockCandidates(
                ref colors,
                flip != 0,
                subblock: 1,
                transparentPunchthrough,
                compressionMode,
                subblock1);

            for (var i = 0; i < count0; i++)
            {
                var left = subblock0[i];
                for (var j = 0; j < count1; j++)
                {
                    var right = subblock1[j];
                    var dr = right.Red - left.Red;
                    if (dr is < -4 or > 3)
                    {
                        continue;
                    }

                    var dg = right.Green - left.Green;
                    if (dg is < -4 or > 3)
                    {
                        continue;
                    }

                    var db = right.Blue - left.Blue;
                    if (db is < -4 or > 3)
                    {
                        continue;
                    }

                    var high = ((uint)left.Red << 27) |
                               ((uint)PackSigned3(dr) << 24) |
                               ((uint)left.Green << 19) |
                               ((uint)PackSigned3(dg) << 16) |
                               ((uint)left.Blue << 11) |
                               ((uint)PackSigned3(db) << 8) |
                               ((uint)left.Table << 5) |
                               ((uint)right.Table << 2) |
                               (transparentPunchthrough ? 0u : 0x2u) |
                               (uint)flip;
                    var candidate = new EtcColorEncoding(left.Error + right.Error, high, left.Low | right.Low);
                    best = BestEncoding(best, candidate);
                }
            }
        }

        return best;
    }

    private static int BuildDifferentialSubblockCandidates(
        ref EtcColorBlock colors,
        bool flip,
        int subblock,
        bool transparentPunchthrough,
        TextureCompressionLevel compressionMode,
        Span<DifferentialSubblockEncoding> destination)
    {
        AverageSubblock(ref colors, flip, subblock, transparentPunchthrough, out var average);
        var redCenter = QuantizeByte(average.Red, 31);
        var greenCenter = QuantizeByte(average.Green, 31);
        var blueCenter = QuantizeByte(average.Blue, 31);

        var count = 0;
        var radius = GetDifferentialEndpointSearchRadius(compressionMode);
        for (var red = Math.Max(0, redCenter - radius); red <= Math.Min(31, redCenter + radius); red++)
        {
            for (var green = Math.Max(0, greenCenter - radius); green <= Math.Min(31, greenCenter + radius); green++)
            {
                for (var blue = Math.Max(0, blueCenter - radius); blue <= Math.Min(31, blueCenter + radius); blue++)
                {
                    var baseColor = new EtcColor(Expand5To8(red), Expand5To8(green), Expand5To8(blue));
                    FindBestTableAndIndices(
                        ref colors,
                        flip,
                        subblock,
                        baseColor,
                        transparentPunchthrough,
                        out var table,
                        out var low,
                        out var error);
                    destination[count++] = new DifferentialSubblockEncoding(red, green, blue, table, low, error);
                }
            }
        }

        return count;
    }

    private static EtcColorEncoding EncodeDifferentialCandidate(
        ref EtcColorBlock colors,
        bool flip,
        bool transparentPunchthrough,
        int r0,
        int g0,
        int b0,
        int r1,
        int g1,
        int b1)
    {
        var base0 = new EtcColor(Expand5To8(r0), Expand5To8(g0), Expand5To8(b0));
        var base1 = new EtcColor(Expand5To8(r1), Expand5To8(g1), Expand5To8(b1));
        FindBestTableAndIndices(ref colors, flip, 0, base0, transparentPunchthrough, out var table0, out var low0, out var error0);
        FindBestTableAndIndices(ref colors, flip, 1, base1, transparentPunchthrough, out var table1, out var low1, out var error1);

        var high = ((uint)r0 << 27) |
                   ((uint)PackSigned3(r1 - r0) << 24) |
                   ((uint)g0 << 19) |
                   ((uint)PackSigned3(g1 - g0) << 16) |
                   ((uint)b0 << 11) |
                   ((uint)PackSigned3(b1 - b0) << 8) |
                   ((uint)table0 << 5) |
                   ((uint)table1 << 2) |
                   (transparentPunchthrough ? 0u : 0x2u) |
                   (uint)(flip ? 1 : 0);
        return new EtcColorEncoding(error0 + error1, high, low0 | low1);
    }

    private static EtcColorEncoding EncodeTModeHigh(
        ref EtcColorBlock colors,
        bool transparentPunchthrough,
        TextureCompressionLevel compressionMode)
    {
        Span<EtcColorPairSeed> seeds = stackalloc EtcColorPairSeed[GetEtc2ModeSeedCapacity(compressionMode)];
        var seedCount = 0;
        AddEtc2ModeSeeds(ref colors, transparentPunchthrough, seeds, ref seedCount);
        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueEtc2ModeSeeds(ref colors, transparentPunchthrough, seeds, ref seedCount);
        }

        var best = EtcColorEncoding.Worst;
        Span<EtcColor> paint = stackalloc EtcColor[4];
        for (var seedIndex = 0; seedIndex < seedCount; seedIndex++)
        {
            var seed = seeds[seedIndex];
            for (var distance = 0; distance < 8; distance++)
            {
                BuildTModePaintColors(seed.Color0, seed.Color1, distance, paint);
                var candidate = EvaluateEtc2PaintMode(ref colors, paint, transparentPunchthrough);
                if (candidate.Error >= best.Error)
                {
                    continue;
                }

                if (TryPackTMode(seed.Color0, seed.Color1, distance, candidate.Low, transparentPunchthrough, out var high, out var low))
                {
                    best = new EtcColorEncoding(candidate.Error, high, low);
                }
            }
        }

        return best;
    }

    private static EtcColorEncoding EncodeHModeHigh(
        ref EtcColorBlock colors,
        bool transparentPunchthrough,
        TextureCompressionLevel compressionMode)
    {
        Span<EtcColorPairSeed> seeds = stackalloc EtcColorPairSeed[GetEtc2ModeSeedCapacity(compressionMode)];
        var seedCount = 0;
        AddEtc2ModeSeeds(ref colors, transparentPunchthrough, seeds, ref seedCount);
        if (compressionMode == TextureCompressionLevel.Exhaustive)
        {
            AddUniqueEtc2ModeSeeds(ref colors, transparentPunchthrough, seeds, ref seedCount);
        }

        var best = EtcColorEncoding.Worst;
        Span<EtcColor> paint = stackalloc EtcColor[4];
        for (var seedIndex = 0; seedIndex < seedCount; seedIndex++)
        {
            var seed = seeds[seedIndex];
            for (var distance = 0; distance < 8; distance++)
            {
                if (!CanPackHModeDistance(seed.Color0, seed.Color1, distance))
                {
                    continue;
                }

                BuildHModePaintColors(seed.Color0, seed.Color1, distance, paint);
                var candidate = EvaluateEtc2PaintMode(ref colors, paint, transparentPunchthrough);
                if (candidate.Error >= best.Error)
                {
                    continue;
                }

                if (TryPackHMode(seed.Color0, seed.Color1, distance, candidate.Low, transparentPunchthrough, out var high, out var low))
                {
                    best = new EtcColorEncoding(candidate.Error, high, low);
                }
            }
        }

        return best;
    }

    private static EtcColorEncoding EncodePlanarModeHigh(ref EtcColorBlock colors)
    {
        Span<PlanarEndpointSet> endpoints = stackalloc PlanarEndpointSet[4];
        var endpointCount = 0;
        AddLeastSquaresPlanarEndpoint(ref colors, endpoints, ref endpointCount);
        AddCornerPlanarEndpoint(ref colors, endpoints, ref endpointCount);

        var best = EtcColorEncoding.Worst;
        for (var i = 0; i < endpointCount; i++)
        {
            var candidate = EvaluatePlanarMode(ref colors, endpoints[i]);
            if (candidate.Error >= best.Error)
            {
                continue;
            }

            if (TryPackPlanarMode(endpoints[i], out var high, out var low))
            {
                best = new EtcColorEncoding(candidate.Error, high, low);
            }
        }

        return best;
    }

    private static EtcColorEncoding EvaluateEtc2PaintMode(
        ref EtcColorBlock colors,
        ReadOnlySpan<EtcColor> paint,
        bool transparentPunchthrough)
    {
        var low = 0u;
        var error = 0L;
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var texel = (y * BlockSize) + x;
                var source = colors[texel];
                var index = FindBestEtc2PaintIndex(source, paint, transparentPunchthrough);
                var reconstructed = transparentPunchthrough && index == 2
                    ? new EtcColor(0, 0, 0, 0)
                    : paint[index];
                error += GetColorError(source, reconstructed, transparentPunchthrough);
                SetEtcRawIndex(ref low, x, y, index);
            }
        }

        return new EtcColorEncoding(error, 0, low);
    }

    private static int FindBestEtc2PaintIndex(
        EtcColor source,
        ReadOnlySpan<EtcColor> paint,
        bool transparentPunchthrough)
    {
        if (transparentPunchthrough && IsTransparent(source))
        {
            return 2;
        }

        var bestIndex = 0;
        var bestError = long.MaxValue;
        for (var index = 0; index < 4; index++)
        {
            if (transparentPunchthrough && index == 2)
            {
                continue;
            }

            var error = GetColorError(source, paint[index], transparentPunchthrough);
            if (error < bestError)
            {
                bestError = error;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static EtcColorEncoding EvaluatePlanarMode(ref EtcColorBlock colors, PlanarEndpointSet endpoints)
    {
        var error = 0L;
        var origin = endpoints.Origin;
        var horizontal = endpoints.Horizontal;
        var vertical = endpoints.Vertical;
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var reconstructed = ReconstructPlanarColor(origin, horizontal, vertical, x, y);
                error += GetColorError(colors[(y * BlockSize) + x], reconstructed, transparentPunchthrough: false);
            }
        }

        return new EtcColorEncoding(error, 0, 0);
    }

    private static EtcColor ReconstructPlanarColor(EtcColor origin, EtcColor horizontal, EtcColor vertical, int x, int y) => new(
        ClampToByte(((x * (horizontal.Red - origin.Red)) + (y * (vertical.Red - origin.Red)) + (4 * origin.Red) + 2) >> 2),
        ClampToByte(((x * (horizontal.Green - origin.Green)) + (y * (vertical.Green - origin.Green)) + (4 * origin.Green) + 2) >> 2),
        ClampToByte(((x * (horizontal.Blue - origin.Blue)) + (y * (vertical.Blue - origin.Blue)) + (4 * origin.Blue) + 2) >> 2));

    private static void AddEtc2ModeSeeds(
        ref EtcColorBlock colors,
        bool ignoreTransparent,
        Span<EtcColorPairSeed> seeds,
        ref int count)
    {
        GetColorBounds(ref colors, ignoreTransparent, out var min, out var max, out var hasOpaque);
        if (!hasOpaque)
        {
            AddEtc2ModeSeed(new EtcColor(0, 0, 0), new EtcColor(0, 0, 0), seeds, ref count);
            return;
        }

        AddEtc2ModeSeed(max, min, seeds, ref count);
        AddEtc2ModeSeed(min, max, seeds, ref count);

        if (TryFindFarthestEtcColors(ref colors, ignoreTransparent, out var farA, out var farB))
        {
            AddEtc2ModeSeed(farA, farB, seeds, ref count);
            AddEtc2ModeSeed(farB, farA, seeds, ref count);
            if (TryFindSplitAverageEtcColors(ref colors, ignoreTransparent, farA, farB, out var averageA, out var averageB))
            {
                AddEtc2ModeSeed(averageA, averageB, seeds, ref count);
                AddEtc2ModeSeed(averageB, averageA, seeds, ref count);
            }
        }

        AverageBlock(ref colors, ignoreTransparent, out var average);
        AddEtc2ModeSeed(average, average, seeds, ref count);
        AddEtc2ModeSeed(max, average, seeds, ref count);
        AddEtc2ModeSeed(average, min, seeds, ref count);
    }

    private static void AddUniqueEtc2ModeSeeds(
        ref EtcColorBlock colors,
        bool ignoreTransparent,
        Span<EtcColorPairSeed> seeds,
        ref int count)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var color0 = colors[i];
            if (ignoreTransparent && IsTransparent(color0))
            {
                continue;
            }

            for (var j = 0; j < TexelsPerBlock; j++)
            {
                var color1 = colors[j];
                if (ignoreTransparent && IsTransparent(color1))
                {
                    continue;
                }

                AddEtc2ModeSeed(color0, color1, seeds, ref count);
            }
        }
    }

    private static void AddEtc2ModeSeed(EtcColor color0, EtcColor color1, Span<EtcColorPairSeed> seeds, ref int count)
    {
        if (count >= seeds.Length)
        {
            return;
        }

        color0 = QuantizeEtc4Color(color0);
        color1 = QuantizeEtc4Color(color1);
        var packed0 = PackColor444(color0);
        var packed1 = PackColor444(color1);
        for (var i = 0; i < count; i++)
        {
            if (PackColor444(seeds[i].Color0) == packed0 && PackColor444(seeds[i].Color1) == packed1)
            {
                return;
            }
        }

        seeds[count++] = new EtcColorPairSeed(color0, color1);
    }

    private static void GetColorBounds(
        ref EtcColorBlock colors,
        bool ignoreTransparent,
        out EtcColor min,
        out EtcColor max,
        out bool hasColor)
    {
        var minRed = byte.MaxValue;
        var minGreen = byte.MaxValue;
        var minBlue = byte.MaxValue;
        var maxRed = byte.MinValue;
        var maxGreen = byte.MinValue;
        var maxBlue = byte.MinValue;
        hasColor = false;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var color = colors[i];
            if (ignoreTransparent && IsTransparent(color))
            {
                continue;
            }

            minRed = Math.Min(minRed, color.Red);
            minGreen = Math.Min(minGreen, color.Green);
            minBlue = Math.Min(minBlue, color.Blue);
            maxRed = Math.Max(maxRed, color.Red);
            maxGreen = Math.Max(maxGreen, color.Green);
            maxBlue = Math.Max(maxBlue, color.Blue);
            hasColor = true;
        }

        min = hasColor ? new EtcColor(minRed, minGreen, minBlue) : new EtcColor(0, 0, 0);
        max = hasColor ? new EtcColor(maxRed, maxGreen, maxBlue) : new EtcColor(0, 0, 0);
    }

    private static bool TryFindFarthestEtcColors(
        ref EtcColorBlock colors,
        bool ignoreTransparent,
        out EtcColor color0,
        out EtcColor color1)
    {
        color0 = new EtcColor(0, 0, 0);
        color1 = new EtcColor(0, 0, 0);
        var bestDistance = -1L;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var left = colors[i];
            if (ignoreTransparent && IsTransparent(left))
            {
                continue;
            }

            for (var j = i + 1; j < TexelsPerBlock; j++)
            {
                var right = colors[j];
                if (ignoreTransparent && IsTransparent(right))
                {
                    continue;
                }

                var distance = GetColorDistanceSquared(left, right);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    color0 = left;
                    color1 = right;
                }
            }
        }

        return bestDistance >= 0;
    }

    private static bool TryFindSplitAverageEtcColors(
        ref EtcColorBlock colors,
        bool ignoreTransparent,
        EtcColor seed0,
        EtcColor seed1,
        out EtcColor average0,
        out EtcColor average1)
    {
        var red0 = 0;
        var green0 = 0;
        var blue0 = 0;
        var count0 = 0;
        var red1 = 0;
        var green1 = 0;
        var blue1 = 0;
        var count1 = 0;

        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var color = colors[i];
            if (ignoreTransparent && IsTransparent(color))
            {
                continue;
            }

            if (GetColorDistanceSquared(color, seed0) <= GetColorDistanceSquared(color, seed1))
            {
                red0 += color.Red;
                green0 += color.Green;
                blue0 += color.Blue;
                count0++;
            }
            else
            {
                red1 += color.Red;
                green1 += color.Green;
                blue1 += color.Blue;
                count1++;
            }
        }

        if (count0 == 0 || count1 == 0)
        {
            average0 = seed0;
            average1 = seed1;
            return false;
        }

        average0 = new EtcColor(
            (byte)((red0 + (count0 / 2)) / count0),
            (byte)((green0 + (count0 / 2)) / count0),
            (byte)((blue0 + (count0 / 2)) / count0));
        average1 = new EtcColor(
            (byte)((red1 + (count1 / 2)) / count1),
            (byte)((green1 + (count1 / 2)) / count1),
            (byte)((blue1 + (count1 / 2)) / count1));
        return true;
    }

    private static void AverageBlock(ref EtcColorBlock colors, bool ignoreTransparent, out EtcColor average)
    {
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var color = colors[i];
            if (ignoreTransparent && IsTransparent(color))
            {
                continue;
            }

            red += color.Red;
            green += color.Green;
            blue += color.Blue;
            count++;
        }

        if (count == 0)
        {
            average = new EtcColor(0, 0, 0);
            return;
        }

        average = new EtcColor(
            (byte)((red + (count / 2)) / count),
            (byte)((green + (count / 2)) / count),
            (byte)((blue + (count / 2)) / count));
    }

    private static long GetColorDistanceSquared(EtcColor left, EtcColor right)
    {
        var red = left.Red - right.Red;
        var green = left.Green - right.Green;
        var blue = left.Blue - right.Blue;
        return (red * red) + (green * green) + (blue * blue);
    }

    private static EtcColor QuantizeEtc4Color(EtcColor color) => new(
        Expand4To8(QuantizeByte(color.Red, 15)),
        Expand4To8(QuantizeByte(color.Green, 15)),
        Expand4To8(QuantizeByte(color.Blue, 15)));

    private static int PackColor444(EtcColor color) =>
        (QuantizeByte(color.Red, 15) << 8) |
        (QuantizeByte(color.Green, 15) << 4) |
        QuantizeByte(color.Blue, 15);

    private static void AddLeastSquaresPlanarEndpoint(
        ref EtcColorBlock colors,
        Span<PlanarEndpointSet> endpoints,
        ref int count)
    {
        FitPlanarChannel(ref colors, 0, out var originRed, out var horizontalRed, out var verticalRed);
        FitPlanarChannel(ref colors, 1, out var originGreen, out var horizontalGreen, out var verticalGreen);
        FitPlanarChannel(ref colors, 2, out var originBlue, out var horizontalBlue, out var verticalBlue);
        AddPlanarEndpoint(
            originRed,
            originGreen,
            originBlue,
            horizontalRed,
            horizontalGreen,
            horizontalBlue,
            verticalRed,
            verticalGreen,
            verticalBlue,
            endpoints,
            ref count);
    }

    private static void AddCornerPlanarEndpoint(ref EtcColorBlock colors, Span<PlanarEndpointSet> endpoints, ref int count)
    {
        var origin = colors[0];
        var right = colors[3];
        var bottom = colors[12];
        AddPlanarEndpoint(
            origin.Red,
            origin.Green,
            origin.Blue,
            ClampToByte(DivRound((4 * right.Red) - origin.Red, 3)),
            ClampToByte(DivRound((4 * right.Green) - origin.Green, 3)),
            ClampToByte(DivRound((4 * right.Blue) - origin.Blue, 3)),
            ClampToByte(DivRound((4 * bottom.Red) - origin.Red, 3)),
            ClampToByte(DivRound((4 * bottom.Green) - origin.Green, 3)),
            ClampToByte(DivRound((4 * bottom.Blue) - origin.Blue, 3)),
            endpoints,
            ref count);
    }

    private static void FitPlanarChannel(ref EtcColorBlock colors, int channel, out byte origin, out byte horizontal, out byte vertical)
    {
        var m00 = 0d;
        var m01 = 0d;
        var m02 = 0d;
        var m11 = 0d;
        var m12 = 0d;
        var m22 = 0d;
        var b0 = 0d;
        var b1 = 0d;
        var b2 = 0d;

        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var a = 4 - x - y;
                var b = x;
                var c = y;
                var value = GetColorChannel(colors[(y * BlockSize) + x], channel) * 4d;
                m00 += a * a;
                m01 += a * b;
                m02 += a * c;
                m11 += b * b;
                m12 += b * c;
                m22 += c * c;
                b0 += a * value;
                b1 += b * value;
                b2 += c * value;
            }
        }

        SolveSymmetric3x3(m00, m01, m02, m11, m12, m22, b0, b1, b2, out var o, out var h, out var v);
        origin = ClampToByte((int)Math.Round(o));
        horizontal = ClampToByte((int)Math.Round(h));
        vertical = ClampToByte((int)Math.Round(v));
    }

    private static void SolveSymmetric3x3(
        double m00,
        double m01,
        double m02,
        double m11,
        double m12,
        double m22,
        double b0,
        double b1,
        double b2,
        out double x0,
        out double x1,
        out double x2)
    {
        var determinant = (m00 * ((m11 * m22) - (m12 * m12))) -
                          (m01 * ((m01 * m22) - (m12 * m02))) +
                          (m02 * ((m01 * m12) - (m11 * m02)));
        if (Math.Abs(determinant) < 0.000001d)
        {
            x0 = x1 = x2 = 0d;
            return;
        }

        x0 = ((b0 * ((m11 * m22) - (m12 * m12))) -
              (m01 * ((b1 * m22) - (m12 * b2))) +
              (m02 * ((b1 * m12) - (m11 * b2)))) / determinant;
        x1 = ((m00 * ((b1 * m22) - (m12 * b2))) -
              (b0 * ((m01 * m22) - (m12 * m02))) +
              (m02 * ((m01 * b2) - (b1 * m02)))) / determinant;
        x2 = ((m00 * ((m11 * b2) - (b1 * m12))) -
              (m01 * ((m01 * b2) - (b1 * m02))) +
              (b0 * ((m01 * m12) - (m11 * m02)))) / determinant;
    }

    private static int GetColorChannel(EtcColor color, int channel) => channel switch
    {
        0 => color.Red,
        1 => color.Green,
        _ => color.Blue
    };

    private static void AddPlanarEndpoint(
        int originRed,
        int originGreen,
        int originBlue,
        int horizontalRed,
        int horizontalGreen,
        int horizontalBlue,
        int verticalRed,
        int verticalGreen,
        int verticalBlue,
        Span<PlanarEndpointSet> endpoints,
        ref int count)
    {
        if (count >= endpoints.Length)
        {
            return;
        }

        var endpoint = new PlanarEndpointSet(
            QuantizeByte(ClampToByte(originRed), 63),
            QuantizeByte(ClampToByte(originGreen), 127),
            QuantizeByte(ClampToByte(originBlue), 63),
            QuantizeByte(ClampToByte(horizontalRed), 63),
            QuantizeByte(ClampToByte(horizontalGreen), 127),
            QuantizeByte(ClampToByte(horizontalBlue), 63),
            QuantizeByte(ClampToByte(verticalRed), 63),
            QuantizeByte(ClampToByte(verticalGreen), 127),
            QuantizeByte(ClampToByte(verticalBlue), 63));
        for (var i = 0; i < count; i++)
        {
            if (endpoints[i] == endpoint)
            {
                return;
            }
        }

        endpoints[count++] = endpoint;
    }

    private static bool CanPackHModeDistance(EtcColor color0, EtcColor color1, int distance) =>
        (PackColor444(color0) >= PackColor444(color1)) == ((distance & 1) != 0);

    private static bool TryPackTMode(
        EtcColor color0,
        EtcColor color1,
        int distance,
        uint modeLow,
        bool transparentPunchthrough,
        out uint high,
        out uint low)
    {
        var tHigh = 0u;
        PutBitsHigh(ref tHigh, QuantizeByte(color0.Red, 15), 4, 58);
        PutBitsHigh(ref tHigh, QuantizeByte(color0.Green, 15), 4, 54);
        PutBitsHigh(ref tHigh, QuantizeByte(color0.Blue, 15), 4, 50);
        PutBitsHigh(ref tHigh, QuantizeByte(color1.Red, 15), 4, 46);
        PutBitsHigh(ref tHigh, QuantizeByte(color1.Green, 15), 4, 42);
        PutBitsHigh(ref tHigh, QuantizeByte(color1.Blue, 15), 4, 38);
        PutBitsHigh(ref tHigh, distance, 3, 34);
        return TryStuff59Bits(tHigh, modeLow, transparentPunchthrough, out high, out low);
    }

    private static bool TryPackHMode(
        EtcColor color0,
        EtcColor color1,
        int distance,
        uint modeLow,
        bool transparentPunchthrough,
        out uint high,
        out uint low)
    {
        high = 0;
        low = 0;
        if (!CanPackHModeDistance(color0, color1, distance))
        {
            return false;
        }

        var hHigh = 0u;
        PutBitsHigh(ref hHigh, QuantizeByte(color0.Red, 15), 4, 57);
        PutBitsHigh(ref hHigh, QuantizeByte(color0.Green, 15), 4, 53);
        PutBitsHigh(ref hHigh, QuantizeByte(color0.Blue, 15), 4, 49);
        PutBitsHigh(ref hHigh, QuantizeByte(color1.Red, 15), 4, 45);
        PutBitsHigh(ref hHigh, QuantizeByte(color1.Green, 15), 4, 41);
        PutBitsHigh(ref hHigh, QuantizeByte(color1.Blue, 15), 4, 37);
        PutBitsHigh(ref hHigh, distance >> 1, 2, 33);
        return TryStuff58Bits(hHigh, modeLow, transparentPunchthrough, out high, out low);
    }

    private static bool TryPackPlanarMode(PlanarEndpointSet endpoints, out uint high, out uint low)
    {
        var planarHigh = 0u;
        var planarLow = 0u;
        PutBitsHigh(ref planarHigh, endpoints.OriginRed, 6, 63);
        PutBitsHigh(ref planarHigh, endpoints.OriginGreen, 7, 57);
        PutBitsHigh(ref planarHigh, endpoints.OriginBlue, 6, 50);
        PutBitsHigh(ref planarHigh, endpoints.HorizontalRed, 6, 44);
        PutBitsHigh(ref planarHigh, endpoints.HorizontalGreen, 7, 38);
        PutBits(ref planarLow, endpoints.HorizontalBlue, 6, 31);
        PutBits(ref planarLow, endpoints.VerticalRed, 6, 25);
        PutBits(ref planarLow, endpoints.VerticalGreen, 7, 19);
        PutBits(ref planarLow, endpoints.VerticalBlue, 6, 12);
        return TryStuff57Bits(planarHigh, planarLow, out high, out low);
    }

    private static void FindBestTableAndIndices(
        ref EtcColorBlock colors,
        bool flip,
        int subblock,
        EtcColor baseColor,
        bool transparentPunchthrough,
        out int table,
        out uint low,
        out long error)
    {
        table = 0;
        low = 0;
        error = long.MaxValue;
        for (var candidateTable = 0; candidateTable < 8; candidateTable++)
        {
            var candidateError = 0L;
            var candidateLow = 0u;
            for (var y = 0; y < BlockSize; y++)
            {
                for (var x = 0; x < BlockSize; x++)
                {
                    var currentSubblock = flip ? (y < 2 ? 0 : 1) : (x < 2 ? 0 : 1);
                    if (currentSubblock != subblock)
                    {
                        continue;
                    }

                    var texel = (y * BlockSize) + x;
                    var source = colors[texel];
                    var index = FindBestEtcModifierIndex(source, baseColor, candidateTable, transparentPunchthrough);
                    var reconstructed = ReconstructEtcColor(baseColor, candidateTable, index, transparentPunchthrough);
                    candidateError += GetColorError(source, reconstructed, transparentPunchthrough);
                    SetEtcRawIndex(ref candidateLow, x, y, SEtcScramble[index]);
                }
            }

            if (candidateError < error)
            {
                table = candidateTable;
                low = candidateLow;
                error = candidateError;
            }
        }
    }

    private static int FindBestEtcModifierIndex(
        EtcColor source,
        EtcColor baseColor,
        int table,
        bool transparentPunchthrough)
    {
        if (transparentPunchthrough && IsTransparent(source))
        {
            return 1;
        }

        var bestIndex = 0;
        var bestError = long.MaxValue;
        for (var index = 0; index < 4; index++)
        {
            if (transparentPunchthrough && index == 1)
            {
                continue;
            }

            var reconstructed = ReconstructEtcColor(baseColor, table, index, transparentPunchthrough);
            var error = GetColorError(source, reconstructed, transparentPunchthrough);
            if (error < bestError)
            {
                bestError = error;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static EtcColor ReconstructEtcColor(EtcColor baseColor, int table, int index, bool transparentPunchthrough)
    {
        if (transparentPunchthrough && index == 1)
        {
            return new EtcColor(0, 0, 0, 0);
        }

        var modifier = GetEtcModifier(table, index, transparentPunchthrough);
        return new EtcColor(
            ClampToByte(baseColor.Red + modifier),
            ClampToByte(baseColor.Green + modifier),
            ClampToByte(baseColor.Blue + modifier));
    }

    private static long GetColorError(EtcColor source, EtcColor reconstructed, bool transparentPunchthrough)
    {
        if (transparentPunchthrough && IsTransparent(source) && reconstructed.Alpha == 0)
        {
            return 0;
        }

        var red = source.Red - reconstructed.Red;
        var green = source.Green - reconstructed.Green;
        var blue = source.Blue - reconstructed.Blue;
        var alpha = transparentPunchthrough ? source.Alpha - reconstructed.Alpha : 0;
        return (red * red) + (green * green) + (blue * blue) + (alpha * alpha);
    }

    private static EtcColorEncoding RecalculateEtcColorEncodingError(
        ref EtcColorBlock colors,
        EtcColorEncoding encoding,
        bool punchthrough,
        bool etc2,
        bool transparentPunchthrough)
    {
        if (encoding.Error == long.MaxValue)
        {
            return encoding;
        }

        var decoded = new EtcColorBlock();
        if (etc2)
        {
            if (punchthrough)
            {
                DecodeEtc2PunchthroughBlock(encoding.High, encoding.Low, ref decoded);
            }
            else
            {
                DecodeEtc2RgbBlock(encoding.High, encoding.Low, ref decoded);
            }
        }
        else
        {
            DecodeDiffFlip(encoding.High, encoding.Low, forceDifferential: false, transparentPunchthrough: false, ref decoded);
        }

        var error = 0L;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            error += GetColorError(colors[i], decoded[i], transparentPunchthrough);
        }

        return new EtcColorEncoding(error, encoding.High, encoding.Low);
    }

    private static void AverageSubblock(
        ref EtcColorBlock colors,
        bool flip,
        int subblock,
        bool ignoreTransparent,
        out EtcColor average)
    {
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var currentSubblock = flip ? (y < 2 ? 0 : 1) : (x < 2 ? 0 : 1);
                if (currentSubblock != subblock)
                {
                    continue;
                }

                var color = colors[(y * BlockSize) + x];
                if (ignoreTransparent && IsTransparent(color))
                {
                    continue;
                }

                red += color.Red;
                green += color.Green;
                blue += color.Blue;
                count++;
            }
        }

        if (count == 0)
        {
            average = new EtcColor(0, 0, 0);
            return;
        }

        average = new EtcColor(
            (byte)((red + (count / 2)) / count),
            (byte)((green + (count / 2)) / count),
            (byte)((blue + (count / 2)) / count));
    }

    private static void EncodeEacBlock(
        ref IntBlock source,
        EacBlockKind kind,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        switch (compressionMode)
        {
            case TextureCompressionLevel.Fast:
                EncodeEacBlockFast(ref source, kind, destination);
                return;
            case TextureCompressionLevel.Normal:
            case TextureCompressionLevel.High:
            case TextureCompressionLevel.Exhaustive:
                EncodeEacBlockOptimized(ref source, kind, compressionMode, destination);
                return;
            default:
                throw CreateUnsupportedCompressionModeException(compressionMode);
        }
    }

    private static void EncodeEacBlockFast(ref IntBlock source, EacBlockKind kind, Span<byte> destination)
    {
        GetTargetStats(ref source, out var min, out var max, out var average);
        var best = EacEncoding.Worst;
        var targetRange = max - min;
        var multipliers = new InlineArray3<int>();
        var bases = new InlineArray4<int>();
        var palette = new InlineArray8<int>();

        for (var table = 0; table < 16; table++)
        {
            var multiplierCount = 0;
            var estimatedMultiplier = EstimateEacMultiplier(kind, table, targetRange);
            AddMultiplierCandidate(kind, estimatedMultiplier, multipliers, ref multiplierCount);
            AddMultiplierCandidate(kind, estimatedMultiplier - 1, multipliers, ref multiplierCount);
            AddMultiplierCandidate(kind, estimatedMultiplier + 1, multipliers, ref multiplierCount);

            for (var multiplierIndex = 0; multiplierIndex < multiplierCount; multiplierIndex++)
            {
                var multiplier = multipliers[multiplierIndex];
                GetTermBounds(kind, table, multiplier, out var minTerm, out var maxTerm);
                var baseCount = 0;
                AddBaseCandidate(kind, min, minTerm, bases, ref baseCount);
                AddBaseCandidate(kind, max, maxTerm, bases, ref baseCount);
                AddBaseCandidate(kind, average, (minTerm + maxTerm) / 2, bases, ref baseCount);
                AddBaseCandidate(kind, average, 0, bases, ref baseCount);

                for (var i = 0; i < baseCount; i++)
                {
                    BuildEacPalette(kind, bases[i], table, multiplier, palette);
                    var candidate = EvaluateEacCandidate(ref source, bases[i], table, multiplier, palette, best.Error);
                    best = BestEncoding(best, candidate);
                }
            }
        }

        WriteEacEncoding(best, destination);
    }

    private static void EncodeEacBlockOptimized(
        ref IntBlock source,
        EacBlockKind kind,
        TextureCompressionLevel compressionMode,
        Span<byte> destination)
    {
        GetTargetStats(ref source, out var min, out var max, out var average);
        var best = EacEncoding.Worst;
        var targetRange = max - min;
        Span<int> multipliers = stackalloc int[compressionMode == TextureCompressionLevel.Exhaustive ? 16 : 5];
        Span<int> bases = stackalloc int[compressionMode == TextureCompressionLevel.Exhaustive ? 160 : 32];
        Span<int> palette = stackalloc int[8];

        for (var table = 0; table < 16; table++)
        {
            var multiplierCount = 0;
            var estimatedMultiplier = EstimateEacMultiplier(kind, table, targetRange);
            if (compressionMode == TextureCompressionLevel.Exhaustive)
            {
                for (var multiplier = 0; multiplier <= 15; multiplier++)
                {
                    AddMultiplierCandidate(kind, multiplier, multipliers, ref multiplierCount);
                }
            }
            else
            {
                AddMultiplierCandidate(kind, estimatedMultiplier - 2, multipliers, ref multiplierCount);
                AddMultiplierCandidate(kind, estimatedMultiplier - 1, multipliers, ref multiplierCount);
                AddMultiplierCandidate(kind, estimatedMultiplier, multipliers, ref multiplierCount);
                AddMultiplierCandidate(kind, estimatedMultiplier + 1, multipliers, ref multiplierCount);
                AddMultiplierCandidate(kind, estimatedMultiplier + 2, multipliers, ref multiplierCount);
            }

            for (var multiplierIndex = 0; multiplierIndex < multiplierCount; multiplierIndex++)
            {
                var multiplier = multipliers[multiplierIndex];
                GetTermBounds(kind, table, multiplier, out var minTerm, out var maxTerm);
                var baseCount = 0;
                AddBaseCandidate(kind, min, minTerm, bases, ref baseCount);
                AddBaseCandidate(kind, max, maxTerm, bases, ref baseCount);
                AddBaseCandidate(kind, average, (minTerm + maxTerm) / 2, bases, ref baseCount);
                AddBaseCandidate(kind, average, 0, bases, ref baseCount);

                if (compressionMode is TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive)
                {
                    for (var index = 0; index < 8; index++)
                    {
                        var term = GetEacTerm(kind, table, multiplier, index);
                        AddBaseCandidate(kind, min, term, bases, ref baseCount);
                        AddBaseCandidate(kind, max, term, bases, ref baseCount);
                        AddBaseCandidate(kind, average, term, bases, ref baseCount);
                    }
                }

                if (compressionMode == TextureCompressionLevel.Exhaustive)
                {
                    for (var texel = 0; texel < TexelsPerBlock; texel++)
                    {
                        for (var index = 0; index < 8; index++)
                        {
                            var term = GetEacTerm(kind, table, multiplier, index);
                            AddBaseCandidate(kind, source[texel], term, bases, ref baseCount);
                        }
                    }
                }

                for (var baseIndex = 0; baseIndex < baseCount; baseIndex++)
                {
                    BuildEacPalette(kind, bases[baseIndex], table, multiplier, palette);
                    var candidate = EvaluateEacCandidate(ref source, bases[baseIndex], table, multiplier, palette, best.Error);
                    best = BestEncoding(best, candidate);
                }
            }
        }

        WriteEacEncoding(best, destination);
    }

    private static void WriteEacEncoding(EacEncoding best, Span<byte> destination)
    {
        destination[0] = unchecked((byte)best.BaseCodeword);
        destination[1] = (byte)((best.Multiplier << 4) | best.Table);

        ulong bits = 0;
        for (var x = 0; x < BlockSize; x++)
        {
            for (var y = 0; y < BlockSize; y++)
            {
                var order = (x * BlockSize) + y;
                var texel = (y * BlockSize) + x;
                bits |= (ulong)best.Indices[texel] << (45 - (order * 3));
            }
        }

        for (var i = 0; i < 6; i++)
        {
            destination[2 + i] = (byte)(bits >> (40 - (i * 8)));
        }
    }

    private static int EstimateEacMultiplier(EacBlockKind kind, int table, int targetRange)
    {
        GetEacModifierBounds(table, out var minModifier, out var maxModifier);
        var modifierRange = maxModifier - minModifier;
        if (modifierRange == 0)
        {
            return kind == EacBlockKind.Alpha8 ? 0 : 1;
        }

        var unitRange = kind == EacBlockKind.Alpha8 ? modifierRange : modifierRange * 8;
        var multiplier = targetRange == 0 ? 0 : DivRound(targetRange, unitRange);
        return ClampEacMultiplier(multiplier);
    }

    private static void AddMultiplierCandidate(EacBlockKind kind, int multiplier, Span<int> multipliers, ref int count)
    {
        multiplier = ClampEacMultiplier(multiplier);
        for (var i = 0; i < count; i++)
        {
            if (multipliers[i] == multiplier)
            {
                return;
            }
        }

        multipliers[count++] = multiplier;
    }

    private static int ClampEacMultiplier(int multiplier) => Math.Clamp(multiplier, 0, 15);

    private static void BuildEacPalette(EacBlockKind kind, int baseCodeword, int table, int multiplier, Span<int> palette)
    {
        for (var index = 0; index < 8; index++)
        {
            palette[index] = DecodeEacValue(kind, baseCodeword, table, multiplier, index);
        }
    }

    private static EacEncoding EvaluateEacCandidate(
        ref IntBlock source,
        int baseCodeword,
        int table,
        int multiplier,
        ReadOnlySpan<int> palette,
        long maxError)
    {
        var indices = new EacIndexBlock();
        var error = 0L;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            var index = FindBestEacIndex(palette, source[i], out var reconstructed);
            indices[i] = (byte)index;
            var delta = source[i] - reconstructed;
            error += (long)delta * delta;
            if (error >= maxError)
            {
                return new EacEncoding(error, baseCodeword, table, multiplier, indices);
            }
        }

        return new EacEncoding(error, baseCodeword, table, multiplier, indices);
    }

    private static int FindBestEacIndex(ReadOnlySpan<int> palette, int target, out int reconstructed)
    {
        var bestIndex = 0;
        var bestValue = palette[0];
        var bestDistance = Math.Abs(target - bestValue);
        for (var index = 1; index < 8; index++)
        {
            var value = palette[index];
            var distance = Math.Abs(target - value);
            if (distance < bestDistance)
            {
                bestIndex = index;
                bestValue = value;
                bestDistance = distance;
            }
        }

        reconstructed = bestValue;
        return bestIndex;
    }

    private static int DecodeEacValue(EacBlockKind kind, int baseCodeword, int table, int multiplier, int index)
    {
        var term = GetEacTerm(kind, table, multiplier, index);
        return kind switch
        {
            EacBlockKind.Alpha8 => Math.Clamp(baseCodeword + term, 0, 255),
            EacBlockKind.Unsigned11 => Math.Clamp((baseCodeword * 8) + 4 + term, 0, 2047),
            EacBlockKind.Signed11 => Math.Clamp((baseCodeword * 8) + term, -1023, 1023),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static int GetEacTerm(EacBlockKind kind, int table, int multiplier, int index)
    {
        var modifier = GetEacModifier(table, index);
        return kind switch
        {
            EacBlockKind.Alpha8 => modifier * multiplier,
            EacBlockKind.Unsigned11 or EacBlockKind.Signed11 => multiplier == 0 ? modifier : modifier * multiplier * 8,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static int DecodeUnsignedEac11(byte baseCodeword, byte tableAndMultiplier, int index)
    {
        var table = tableAndMultiplier & 0xf;
        var multiplier = tableAndMultiplier >> 4;
        return DecodeEacValue(EacBlockKind.Unsigned11, baseCodeword, table, multiplier, index);
    }

    private static int DecodeSignedEac11(byte baseCodeword, byte tableAndMultiplier, int index)
    {
        var signedBase = unchecked((sbyte)baseCodeword);
        if (signedBase == sbyte.MinValue)
        {
            signedBase = -127;
        }

        var table = tableAndMultiplier & 0xf;
        var multiplier = tableAndMultiplier >> 4;
        return DecodeEacValue(EacBlockKind.Signed11, signedBase, table, multiplier, index);
    }

    private static void AddBaseCandidate(EacBlockKind kind, int target, int term, Span<int> bases, ref int count)
    {
        var baseCodeword = kind switch
        {
            EacBlockKind.Alpha8 => target - term,
            EacBlockKind.Unsigned11 => DivRound(target - term - 4, 8),
            EacBlockKind.Signed11 => DivRound(target - term, 8),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        baseCodeword = ClampEacBase(kind, baseCodeword);
        for (var i = 0; i < count; i++)
        {
            if (bases[i] == baseCodeword)
            {
                return;
            }
        }

        if (count >= bases.Length)
        {
            return;
        }

        bases[count++] = baseCodeword;
    }

    private static void GetTermBounds(EacBlockKind kind, int table, int multiplier, out int min, out int max)
    {
        min = int.MaxValue;
        max = int.MinValue;
        for (var index = 0; index < 8; index++)
        {
            var term = GetEacTerm(kind, table, multiplier, index);
            min = Math.Min(min, term);
            max = Math.Max(max, term);
        }
    }

    private static void GetEacModifierBounds(int table, out int min, out int max)
    {
        min = int.MaxValue;
        max = int.MinValue;
        for (var index = 0; index < 8; index++)
        {
            var modifier = GetEacModifier(table, index);
            min = Math.Min(min, modifier);
            max = Math.Max(max, modifier);
        }
    }

    private static void GetTargetStats(ref IntBlock source, out int min, out int max, out int average)
    {
        min = int.MaxValue;
        max = int.MinValue;
        var sum = 0;
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            min = Math.Min(min, source[i]);
            max = Math.Max(max, source[i]);
            sum += source[i];
        }

        average = (sum + (TexelsPerBlock / 2)) / TexelsPerBlock;
    }

    private static void Unstuff57Bits(uint high, uint low, out uint planarHigh, out uint planarLow)
    {
        var ro = (int)GetBitsHigh(high, 6, 62);
        var go1 = (int)GetBitsHigh(high, 1, 56);
        var go2 = (int)GetBitsHigh(high, 6, 54);
        var bo1 = (int)GetBitsHigh(high, 1, 48);
        var bo2 = (int)GetBitsHigh(high, 2, 44);
        var bo3 = (int)GetBitsHigh(high, 3, 41);
        var rh1 = (int)GetBitsHigh(high, 5, 38);
        var rh2 = (int)GetBitsHigh(high, 1, 32);
        var gh = (int)GetBits(low, 7, 31);
        var bh = (int)GetBits(low, 6, 24);
        var rv = (int)GetBits(low, 6, 18);
        var gv = (int)GetBits(low, 7, 12);
        var bv = (int)GetBits(low, 6, 5);

        planarHigh = 0;
        planarLow = 0;
        PutBitsHigh(ref planarHigh, ro, 6, 63);
        PutBitsHigh(ref planarHigh, go1, 1, 57);
        PutBitsHigh(ref planarHigh, go2, 6, 56);
        PutBitsHigh(ref planarHigh, bo1, 1, 50);
        PutBitsHigh(ref planarHigh, bo2, 2, 49);
        PutBitsHigh(ref planarHigh, bo3, 3, 47);
        PutBitsHigh(ref planarHigh, rh1, 5, 44);
        PutBitsHigh(ref planarHigh, rh2, 1, 39);
        PutBitsHigh(ref planarHigh, gh, 7, 38);
        PutBits(ref planarLow, bh, 6, 31);
        PutBits(ref planarLow, rv, 6, 25);
        PutBits(ref planarLow, gv, 7, 19);
        PutBits(ref planarLow, bv, 6, 12);
    }

    private static bool TryStuff57Bits(uint planarHigh, uint planarLow, out uint high, out uint low)
    {
        high = 0;
        low = 0;
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 6, 63), 6, 62);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 1, 57), 1, 56);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 6, 56), 6, 54);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 1, 50), 1, 48);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 2, 49), 2, 44);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 3, 47), 3, 41);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 5, 44), 5, 38);
        PutBitsHigh(ref high, (int)GetBitsHigh(planarHigh, 1, 39), 1, 32);
        PutBits(ref low, (int)GetBitsHigh(planarHigh, 7, 38), 7, 31);
        PutBits(ref low, (int)GetBits(planarLow, 6, 31), 6, 24);
        PutBits(ref low, (int)GetBits(planarLow, 6, 25), 6, 18);
        PutBits(ref low, (int)GetBits(planarLow, 7, 19), 7, 12);
        PutBits(ref low, (int)GetBits(planarLow, 6, 12), 6, 5);

        Span<int> freeBits = [63, 55, 47, 46, 45, 42, 33];
        return TryFillStuffedModeBits(
            high,
            low,
            freeBits,
            invalidChannel: 2,
            Unstuff57Bits,
            planarHigh,
            planarLow,
            out high,
            out low);
    }

    private static void Unstuff58Bits(uint high, uint low, out uint hHigh, out uint hLow)
    {
        var part0 = (int)GetBitsHigh(high, 7, 62);
        var part1 = (int)GetBitsHigh(high, 2, 52);
        var part2 = (int)GetBitsHigh(high, 16, 49);
        var part3 = (int)GetBitsHigh(high, 1, 32);
        hHigh = 0;
        PutBitsHigh(ref hHigh, part0, 7, 57);
        PutBitsHigh(ref hHigh, part1, 2, 50);
        PutBitsHigh(ref hHigh, part2, 16, 48);
        PutBitsHigh(ref hHigh, part3, 1, 32);
        hLow = low;
    }

    private static bool TryStuff58Bits(uint hHigh, uint hLow, bool transparentPunchthrough, out uint high, out uint low)
    {
        high = 0;
        low = hLow;
        PutBitsHigh(ref high, (int)GetBitsHigh(hHigh, 7, 57), 7, 62);
        PutBitsHigh(ref high, (int)GetBitsHigh(hHigh, 2, 50), 2, 52);
        PutBitsHigh(ref high, (int)GetBitsHigh(hHigh, 16, 48), 16, 49);
        PutBitsHigh(ref high, (int)GetBitsHigh(hHigh, 1, 32), 1, 32);
        PutBitsHigh(ref high, transparentPunchthrough ? 0 : 1, 1, 33);

        Span<int> freeBits = [63, 55, 54, 53, 50];
        return TryFillStuffedModeBits(
            high,
            low,
            freeBits,
            invalidChannel: 1,
            Unstuff58Bits,
            hHigh,
            hLow,
            out high,
            out low);
    }

    private static void Unstuff59Bits(uint high, uint low, out uint tHigh, out uint tLow)
    {
        tHigh = high >> 1;
        PutBitsHigh(ref tHigh, (int)high, 1, 32);
        var r0a = (int)GetBitsHigh(high, 2, 60);
        PutBitsHigh(ref tHigh, r0a, 2, 58);
        PutBitsHigh(ref tHigh, 0, 5, 63);
        tLow = low;
    }

    private static bool TryStuff59Bits(uint tHigh, uint tLow, bool transparentPunchthrough, out uint high, out uint low)
    {
        high = 0;
        low = tLow;
        PutBitsHigh(ref high, (int)GetBitsHigh(tHigh, 2, 58), 2, 60);
        for (var position = 33; position <= 56; position++)
        {
            PutBitsHigh(ref high, (int)GetBitsHigh(tHigh, 1, position), 1, position + 1);
        }

        PutBitsHigh(ref high, (int)GetBitsHigh(tHigh, 1, 32), 1, 32);
        PutBitsHigh(ref high, transparentPunchthrough ? 0 : 1, 1, 33);

        Span<int> freeBits = [63, 62, 61, 58];
        return TryFillStuffedModeBits(
            high,
            low,
            freeBits,
            invalidChannel: 0,
            Unstuff59Bits,
            tHigh,
            tLow,
            out high,
            out low);
    }

    private delegate void UnstuffMode(uint high, uint low, out uint modeHigh, out uint modeLow);

    private static bool TryFillStuffedModeBits(
        uint baseHigh,
        uint baseLow,
        ReadOnlySpan<int> freeBits,
        int invalidChannel,
        UnstuffMode unstuff,
        uint expectedHigh,
        uint expectedLow,
        out uint high,
        out uint low)
    {
        var combinationCount = 1 << freeBits.Length;
        for (var mask = 0; mask < combinationCount; mask++)
        {
            var candidateHigh = baseHigh;
            for (var bit = 0; bit < freeBits.Length; bit++)
            {
                PutBitsHigh(ref candidateHigh, (mask >> bit) & 1, 1, freeBits[bit]);
            }

            unstuff(candidateHigh, baseLow, out var modeHigh, out var modeLow);
            if (modeHigh == expectedHigh &&
                modeLow == expectedLow &&
                TryGetInvalidDifferentialChannel(candidateHigh, out var channel) &&
                channel == invalidChannel)
            {
                high = candidateHigh;
                low = baseLow;
                return true;
            }
        }

        high = 0;
        low = 0;
        return false;
    }

    private static bool TryGetInvalidDifferentialChannel(uint high, out int channel)
    {
        var r0 = (int)GetBitsHigh(high, 5, 63);
        var g0 = (int)GetBitsHigh(high, 5, 55);
        var b0 = (int)GetBitsHigh(high, 5, 47);
        var r1 = r0 + SignExtend3((int)GetBitsHigh(high, 3, 58));
        var g1 = g0 + SignExtend3((int)GetBitsHigh(high, 3, 50));
        var b1 = b0 + SignExtend3((int)GetBitsHigh(high, 3, 42));
        if ((uint)r1 > 31)
        {
            channel = 0;
            return true;
        }

        if ((uint)g1 > 31)
        {
            channel = 1;
            return true;
        }

        if ((uint)b1 > 31)
        {
            channel = 2;
            return true;
        }

        channel = -1;
        return false;
    }

    private static uint GetBits(uint source, int size, int startPosition) =>
        (source >> (startPosition - size + 1)) & ((1u << size) - 1u);

    private static uint GetBitsHigh(uint source, int size, int startPosition) =>
        (source >> (startPosition - size - 31)) & ((1u << size) - 1u);

    private static void PutBits(ref uint destination, int data, int size, int startPosition)
    {
        var shift = startPosition - size + 1;
        var mask = ((1u << size) - 1u) << shift;
        destination = (destination & ~mask) | (((uint)data << shift) & mask);
    }

    private static void PutBitsHigh(ref uint destination, int data, int size, int startPosition)
    {
        var shift = startPosition - size - 31;
        var mask = ((1u << size) - 1u) << shift;
        destination = (destination & ~mask) | (((uint)data << shift) & mask);
    }

    private static int GetEtcRawIndex(uint low, int x, int y)
    {
        var shift = (x * BlockSize) + y;
        return (int)((((low >> (shift + 16)) & 1u) << 1) | ((low >> shift) & 1u));
    }

    private static void SetEtcRawIndex(ref uint low, int x, int y, int rawIndex)
    {
        var shift = (x * BlockSize) + y;
        low |= (uint)(rawIndex & 1) << shift;
        low |= (uint)((rawIndex >> 1) & 1) << (shift + 16);
    }

    private static int ReadEacIndex(ReadOnlySpan<byte> source, int x, int y)
    {
        var order = (x * BlockSize) + y;
        var bitPosition = order * 3;
        var byteOffset = 2 + (bitPosition / 8);
        var bitOffset = bitPosition & 7;
        var value = 0;
        for (var i = 0; i < 3; i++)
        {
            var sourceBit = 7 - bitOffset;
            value |= ((source[byteOffset] >> sourceBit) & 1) << (2 - i);
            bitOffset++;
            if (bitOffset > 7)
            {
                bitOffset = 0;
                byteOffset++;
            }
        }

        return value;
    }

    private static int GetEtcModifier(int table, int index, bool transparentPunchthrough) =>
        transparentPunchthrough && (index == 1 || index == 2)
            ? 0
            : SEtcModifierTable[(table * 4) + index];

    private static int GetEacModifier(int table, int index) => SEacModifierTable[(table * 8) + index];

    private static bool IsDifferentialMode(uint high) => GetBitsHigh(high, 1, 33) != 0;

    private static int SignExtend3(int value) => (value & 0x4) != 0 ? value - 8 : value;

    private static int PackSigned3(int value) => value & 0x7;

    private static byte Expand4To8(int value) => (byte)((value << 4) | value);

    private static byte Expand5To8(int value) => (byte)((value << 3) | (value >> 2));

    private static byte Expand6To8(int value) => (byte)((value << 2) | (value >> 4));

    private static byte Expand7To8(int value) => (byte)((value << 1) | (value >> 6));

    private static int QuantizeByte(byte value, int max) => ((value * max) + 127) / 255;

    private static EtcColor AddScalar(EtcColor color, int value) => new(
        ClampToByte(color.Red + value),
        ClampToByte(color.Green + value),
        ClampToByte(color.Blue + value),
        color.Alpha);

    private static byte ClampToByte(int value)
    {
        if (value <= byte.MinValue)
        {
            return byte.MinValue;
        }

        return value >= byte.MaxValue ? byte.MaxValue : (byte)value;
    }

    private static bool IsTransparent(EtcColor color) => color.Alpha < 128;

    private static bool HasTransparentTexel(ref EtcColorBlock colors)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            if (IsTransparent(colors[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static int ClampEacBase(EacBlockKind kind, int baseCodeword) => kind switch
    {
        EacBlockKind.Alpha8 or EacBlockKind.Unsigned11 => Math.Clamp(baseCodeword, 0, 255),
        EacBlockKind.Signed11 => Math.Clamp(baseCodeword, -127, 127),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static int DivRound(int value, int divisor) => value >= 0
        ? (value + (divisor / 2)) / divisor
        : (value - (divisor / 2)) / divisor;

    private static ushort Unsigned11ToUNorm16(int value) =>
        (ushort)DivRound(Math.Clamp(value, 0, 2047) * ushort.MaxValue, 2047);

    private static int UNorm16ToUnsigned11(ushort value) =>
        DivRound(value * 2047, ushort.MaxValue);

    private static short Signed11ToSNorm16(int value) =>
        (short)DivRound(Math.Clamp(value, -1023, 1023) * short.MaxValue, 1023);

    private static int SNorm16ToSigned11(short value) =>
        DivRound(Math.Clamp((int)value, -short.MaxValue, short.MaxValue) * 1023, short.MaxValue);

    private static void InitializeUnsignedEacBlock(Span<Rgba16UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba16UNorm(0, 0, 0, ushort.MaxValue);
        }
    }

    private static void InitializeSignedEacBlock(Span<Rgba16SNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba16SNorm(0, 0, 0, short.MaxValue);
        }
    }

    private static void DecodeSrgbColors(Span<Rgba8UNorm> block)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            block[i].Red = RgbaColorConversions.Srgb8ToLinearUNorm8(block[i].Red);
            block[i].Green = RgbaColorConversions.Srgb8ToLinearUNorm8(block[i].Green);
            block[i].Blue = RgbaColorConversions.Srgb8ToLinearUNorm8(block[i].Blue);
        }
    }

    private static void EncodeSrgbColors(ReadOnlySpan<Rgba8UNorm> source, Span<Rgba8UNorm> destination)
    {
        for (var i = 0; i < TexelsPerBlock; i++)
        {
            destination[i] = new Rgba8UNorm(
                RgbaColorConversions.LinearUNorm8ToSrgb8(source[i].Red),
                RgbaColorConversions.LinearUNorm8ToSrgb8(source[i].Green),
                RgbaColorConversions.LinearUNorm8ToSrgb8(source[i].Blue),
                source[i].Alpha);
        }
    }

    private static EtcColorEncoding BestEncoding(EtcColorEncoding left, EtcColorEncoding right) =>
        left.Error <= right.Error ? left : right;

    private static EacEncoding BestEncoding(EacEncoding left, EacEncoding right) =>
        left.Error <= right.Error ? left : right;

    private static int GetIndividualEndpointSearchRadius(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal or TextureCompressionLevel.High => 1,
        TextureCompressionLevel.Exhaustive => 2,
        _ => throw CreateUnsupportedCompressionModeException(compressionMode)
    };

    private static int GetDifferentialEndpointSearchRadius(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.Normal or TextureCompressionLevel.High => 1,
        TextureCompressionLevel.Exhaustive => 2,
        _ => throw CreateUnsupportedCompressionModeException(compressionMode)
    };

    private static int GetDifferentialSubblockCandidateCapacity(TextureCompressionLevel compressionMode)
    {
        var radius = GetDifferentialEndpointSearchRadius(compressionMode);
        var diameter = (radius * 2) + 1;
        return diameter * diameter * diameter;
    }

    private static int GetEtc2ModeSeedCapacity(TextureCompressionLevel compressionMode) => compressionMode switch
    {
        TextureCompressionLevel.High => 16,
        TextureCompressionLevel.Exhaustive => 272,
        _ => throw CreateUnsupportedCompressionModeException(compressionMode)
    };

    private static void LoadBlock<TPixel>(
        BitmapView<TPixel> source,
        int blockX,
        int blockY,
        Span<Rgba8UNorm> destination)
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

    private static void LoadBlock<TPixel>(
        BitmapView<TPixel> source,
        int blockX,
        int blockY,
        Span<Rgba16UNorm> destination)
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
                destination[blockOffset++] = TPixel.ToRgba16UNorm(sourceRow[Math.Min(sourceX, lastSourceX)]);
                sourceX++;
            }
        }
    }

    private static void LoadBlock<TPixel>(
        BitmapView<TPixel> source,
        int blockX,
        int blockY,
        Span<Rgba16SNorm> destination)
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
                destination[blockOffset++] = TPixel.ToRgba16SNorm(sourceRow[Math.Min(sourceX, lastSourceX)]);
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

    private static void StoreBlock<TPixel>(
        ReadOnlySpan<Rgba16UNorm> block,
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

                destinationRow[destinationX] = TPixel.FromRgba16UNorm(block[rowBlockOffset++]);
                destinationX++;
            }

            blockOffset += BlockSize;
        }
    }

    private static void StoreBlock<TPixel>(
        ReadOnlySpan<Rgba16SNorm> block,
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

                destinationRow[destinationX] = TPixel.FromRgba16SNorm(block[rowBlockOffset++]);
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
            throw new ArgumentException("Source span is too small for the encoded ETC/EAC texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded ETC/EAC texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size) => (size + BlockSize - 1) / BlockSize;

    private static bool TryGetTransfer(TextureFormat format, out EtcTransfer transfer)
    {
        if (format == TextureFormats.RgbEtc1UNorm)
        {
            transfer = EtcTransfer.RgbEtc1UNorm;
            return true;
        }

        if (format == TextureFormats.RgbEtc2UNorm)
        {
            transfer = EtcTransfer.RgbEtc2UNorm;
            return true;
        }

        if (format == TextureFormats.RgbEtc2Srgb)
        {
            transfer = EtcTransfer.RgbEtc2Srgb;
            return true;
        }

        if (format == TextureFormats.RgbA1Etc2UNorm)
        {
            transfer = EtcTransfer.RgbA1Etc2UNorm;
            return true;
        }

        if (format == TextureFormats.RgbA1Etc2Srgb)
        {
            transfer = EtcTransfer.RgbA1Etc2Srgb;
            return true;
        }

        if (format == TextureFormats.RgbaEtc2EacUNorm)
        {
            transfer = EtcTransfer.RgbaEtc2EacUNorm;
            return true;
        }

        if (format == TextureFormats.RgbaEtc2EacSrgb)
        {
            transfer = EtcTransfer.RgbaEtc2EacSrgb;
            return true;
        }

        if (format == TextureFormats.R11EacUNorm)
        {
            transfer = EtcTransfer.R11EacUNorm;
            return true;
        }

        if (format == TextureFormats.R11EacSNorm)
        {
            transfer = EtcTransfer.R11EacSNorm;
            return true;
        }

        if (format == TextureFormats.Rg11EacUNorm)
        {
            transfer = EtcTransfer.Rg11EacUNorm;
            return true;
        }

        if (format == TextureFormats.Rg11EacSNorm)
        {
            transfer = EtcTransfer.Rg11EacSNorm;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"ETC/EAC texture coder does not support texture format '{format.Name}'.");

    private static ArgumentOutOfRangeException CreateUnsupportedCompressionModeException(TextureCompressionLevel compressionMode) =>
        new(
            nameof(compressionMode),
            compressionMode,
            "Unsupported ETC/EAC compression mode.");

    private enum EacBlockKind
    {
        Alpha8,
        Unsigned11,
        Signed11
    }

    private enum EtcTransfer
    {
        RgbEtc1UNorm,
        RgbEtc2UNorm,
        RgbEtc2Srgb,
        RgbA1Etc2UNorm,
        RgbA1Etc2Srgb,
        RgbaEtc2EacUNorm,
        RgbaEtc2EacSrgb,
        R11EacUNorm,
        R11EacSNorm,
        Rg11EacUNorm,
        Rg11EacSNorm
    }

    private readonly record struct EtcColor(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue);

    private readonly record struct EtcColorEncoding(long Error, uint High, uint Low)
    {
        public static EtcColorEncoding Worst => new(long.MaxValue, 0, 0);
    }

    private readonly record struct EtcColorPairSeed(EtcColor Color0, EtcColor Color1);

    private readonly record struct DifferentialSubblockEncoding(
        int Red,
        int Green,
        int Blue,
        int Table,
        uint Low,
        long Error);

    private readonly record struct PlanarEndpointSet(
        int OriginRed,
        int OriginGreen,
        int OriginBlue,
        int HorizontalRed,
        int HorizontalGreen,
        int HorizontalBlue,
        int VerticalRed,
        int VerticalGreen,
        int VerticalBlue)
    {
        public EtcColor Origin => new(Expand6To8(OriginRed), Expand7To8(OriginGreen), Expand6To8(OriginBlue));

        public EtcColor Horizontal => new(Expand6To8(HorizontalRed), Expand7To8(HorizontalGreen), Expand6To8(HorizontalBlue));

        public EtcColor Vertical => new(Expand6To8(VerticalRed), Expand7To8(VerticalGreen), Expand6To8(VerticalBlue));
    }

    private readonly record struct IndividualSubblockEncoding(int Red, int Green, int Blue, int Table, long Error, uint Low);

    private readonly record struct EacEncoding(long Error, int BaseCodeword, int Table, int Multiplier, EacIndexBlock Indices)
    {
        public static EacEncoding Worst => new(long.MaxValue, 0, 0, 0, new EacIndexBlock());
    }

    [InlineArray(TexelsPerBlock)]
    private struct EtcColorBlock
    {
        private EtcColor _element0;
    }

    [InlineArray(TexelsPerBlock)]
    private struct IntBlock
    {
        private int _element0;
    }

    [InlineArray(TexelsPerBlock)]
    private struct EacIndexBlock
    {
        private byte _element0;
    }
}
