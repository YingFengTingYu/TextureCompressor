using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using CommunityToolkit.HighPerformance;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using BcColorRgbFloat = BCnEncoder.Shared.ColorRgbFloat;
using BcColorRgba32 = BCnEncoder.Shared.ColorRgba32;

namespace TextureCompressor.Codecs.BCnEncoder;

public sealed class BCnEncoderTextureCoder : IPitchTextureCoder
{
    private const int BlockSize = 4;
    private const int TexelsPerBlock = BlockSize * BlockSize;

    private static readonly FormatMapping[] SMappings =
    [
        new(TextureFormats.Bc1Rgb, CompressionFormat.Bc1, SourceKind.Rgba8),
        new(TextureFormats.Bc1Rgba, CompressionFormat.Bc1WithAlpha, SourceKind.Rgba8),
        new(TextureFormats.Bc1RgbSrgb, CompressionFormat.Bc1, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Bc1RgbaSrgb, CompressionFormat.Bc1WithAlpha, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt1Rgb, CompressionFormat.Bc1, SourceKind.Rgba8),
        new(TextureFormats.Dxt1Rgba, CompressionFormat.Bc1WithAlpha, SourceKind.Rgba8),
        new(TextureFormats.Dxt1RgbSrgb, CompressionFormat.Bc1, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt1RgbaSrgb, CompressionFormat.Bc1WithAlpha, SourceKind.Rgba8, IsSrgb: true),

        new(TextureFormats.Bc2Rgba, CompressionFormat.Bc2, SourceKind.Rgba8),
        new(TextureFormats.Bc2RgbaSrgb, CompressionFormat.Bc2, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt2Rgba, CompressionFormat.Bc2, SourceKind.Rgba8),
        new(TextureFormats.Dxt3Rgba, CompressionFormat.Bc2, SourceKind.Rgba8),
        new(TextureFormats.Dxt3RgbaSrgb, CompressionFormat.Bc2, SourceKind.Rgba8, IsSrgb: true),

        new(TextureFormats.Bc3Rgba, CompressionFormat.Bc3, SourceKind.Rgba8),
        new(TextureFormats.Bc3RgbaSrgb, CompressionFormat.Bc3, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt4Rgba, CompressionFormat.Bc3, SourceKind.Rgba8),
        new(TextureFormats.Dxt5Rgba, CompressionFormat.Bc3, SourceKind.Rgba8),
        new(TextureFormats.Dxt5RgbaSrgb, CompressionFormat.Bc3, SourceKind.Rgba8, IsSrgb: true),

        new(TextureFormats.Bc4UNorm, CompressionFormat.Bc4, SourceKind.Rgba8),
        new(TextureFormats.Rgtc1UNorm, CompressionFormat.Bc4, SourceKind.Rgba8),
        new(TextureFormats.Ati1UNorm, CompressionFormat.Bc4, SourceKind.Rgba8),
        new(TextureFormats.Latc1UNorm, CompressionFormat.Bc4, SourceKind.Rgba8),

        new(TextureFormats.Bc5UNorm, CompressionFormat.Bc5, SourceKind.Rgba8),
        new(TextureFormats.Rgtc2UNorm, CompressionFormat.Bc5, SourceKind.Rgba8),
        new(TextureFormats.Ati2UNorm, CompressionFormat.Bc5, SourceKind.Rgba8),
        new(TextureFormats.Latc2UNorm, CompressionFormat.Bc5, SourceKind.Rgba8),

        new(TextureFormats.Bc6HUFloat, CompressionFormat.Bc6U, SourceKind.RgbFloat),
        new(TextureFormats.Bc6HSFloat, CompressionFormat.Bc6S, SourceKind.RgbFloat),
        new(TextureFormats.RgbBptcUFloat, CompressionFormat.Bc6U, SourceKind.RgbFloat),
        new(TextureFormats.RgbBptcSFloat, CompressionFormat.Bc6S, SourceKind.RgbFloat),
        new(TextureFormats.Bc7UNorm, CompressionFormat.Bc7, SourceKind.Rgba8),
        new(TextureFormats.Bc7Srgb, CompressionFormat.Bc7, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.RgbaBptcUNorm, CompressionFormat.Bc7, SourceKind.Rgba8),
        new(TextureFormats.RgbaBptcSrgb, CompressionFormat.Bc7, SourceKind.Rgba8, IsSrgb: true)
    ];

    private static readonly TextureFormat[] SSupportedFormats = SMappings.Select(static mapping => mapping.Format).ToArray();

    private readonly FormatMapping _mapping;
    private readonly BCnEncoderCoderOptions _options;

    public BCnEncoderTextureCoder(TextureFormat format, BCnEncoderCoderOptions? options = null)
    {
        if (!TryGetMapping(format, out _mapping))
        {
            throw new NotSupportedException($"BCnEncoder.NET does not have a mapped coder for texture format '{format.Name}'.");
        }

        Format = format;
        _options = options ?? new BCnEncoderCoderOptions();
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) => TryGetMapping(format, out _);

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

        if (_mapping.SourceKind == SourceKind.RgbFloat)
        {
            DecodeHdr(source, destination, rowPitch);
            return;
        }

        DecodeLdr(source, destination, rowPitch);
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        if (_mapping.SourceKind == SourceKind.RgbFloat)
        {
            EncodeHdr(source, destination, rowPitch);
            return;
        }

        EncodeLdr(source, destination, rowPitch);
    }

    private void DecodeLdr<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var decoder = CreateDecoder();
        Span<BcColorRgba32> block = stackalloc BcColorRgba32[TexelsPerBlock];
        var blockSize = decoder.GetBlockSize(_mapping.CompressionFormat);
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var rowOffset = checked(blockY * rowPitch);
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var blockOffset = checked(rowOffset + (blockX * blockSize));
                var decoded = decoder.DecodeBlock(source.Slice(blockOffset, blockSize), _mapping.CompressionFormat);
                CopyDecodedBlock(decoded.Span, block);
                StoreLdrBlock(block, blockX, blockY, destination);
            }
        }
    }

    private void DecodeHdr<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var decoder = CreateDecoder();
        Span<BcColorRgbFloat> block = stackalloc BcColorRgbFloat[TexelsPerBlock];
        var blockSize = decoder.GetBlockSize(_mapping.CompressionFormat);
        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var rowOffset = checked(blockY * rowPitch);
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var blockOffset = checked(rowOffset + (blockX * blockSize));
                var decoded = decoder.DecodeBlockHdr(source.Slice(blockOffset, blockSize), _mapping.CompressionFormat);
                CopyDecodedBlock(decoded.Span, block);
                StoreHdrBlock(block, blockX, blockY, destination);
            }
        }
    }

    private void EncodeLdr<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var encoder = CreateEncoder();
        Span<BcColorRgba32> block = stackalloc BcColorRgba32[TexelsPerBlock];
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var rowOffset = checked(blockY * rowPitch);
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadLdrBlock(source, blockX, blockY, block);
                var encoded = encoder.EncodeBlock(block);
                encoded.CopyTo(destination.Slice(checked(rowOffset + (blockX * encoded.Length)), encoded.Length));
            }
        }
    }

    private void EncodeHdr<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var encoder = CreateEncoder();
        Span<BcColorRgbFloat> block = stackalloc BcColorRgbFloat[TexelsPerBlock];
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            var rowOffset = checked(blockY * rowPitch);
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                LoadHdrBlock(source, blockX, blockY, block);
                var encoded = encoder.EncodeBlockHdr(block);
                encoded.CopyTo(destination.Slice(checked(rowOffset + (blockX * encoded.Length)), encoded.Length));
            }
        }
    }

    private BcEncoder CreateEncoder()
    {
        var encoder = new BcEncoder(_mapping.CompressionFormat);
        encoder.OutputOptions.Quality = _options.Quality;
        encoder.OutputOptions.Format = _mapping.CompressionFormat;
        encoder.InputOptions.Bc4Component = _options.Bc4Component;
        encoder.InputOptions.Bc5Component1 = _options.Bc5Component1;
        encoder.InputOptions.Bc5Component2 = _options.Bc5Component2;
        return encoder;
    }

    private BcDecoder CreateDecoder()
    {
        var decoder = new BcDecoder();
        decoder.OutputOptions.Bc4Component = _options.Bc4Component;
        decoder.OutputOptions.Bc5Component1 = _options.Bc5Component1;
        decoder.OutputOptions.Bc5Component2 = _options.Bc5Component2;
        return decoder;
    }

    private void LoadLdrBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<BcColorRgba32> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        var lastSourceX = source.Width - 1;
        var offset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            for (var x = 0; x < BlockSize; x++)
            {
                var pixel = TPixel.ToRgba8UNorm(sourceRow[Math.Min(originX + x, lastSourceX)]);
                if (_mapping.IsSrgb)
                {
                    pixel = EncodeSrgb(pixel);
                }

                destination[offset++] = new BcColorRgba32(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
            }
        }
    }

    private static void LoadHdrBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<BcColorRgbFloat> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        var lastSourceX = source.Width - 1;
        var offset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            var sourceY = Math.Min(originY + y, source.Height - 1);
            var sourceRow = source.GetRowSpan(sourceY);
            for (var x = 0; x < BlockSize; x++)
            {
                var pixel = TPixel.ToRgba32Float(sourceRow[Math.Min(originX + x, lastSourceX)]);
                destination[offset++] = new BcColorRgbFloat(pixel.Red, pixel.Green, pixel.Blue);
            }
        }
    }

    private void StoreLdrBlock<TPixel>(
        ReadOnlySpan<BcColorRgba32> block,
        int blockX,
        int blockY,
        BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        for (var y = 0; y < BlockSize; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            for (var x = 0; x < BlockSize; x++)
            {
                var destinationX = originX + x;
                if (destinationX >= destination.Width)
                {
                    break;
                }

                var blockOffset = checked((y * BlockSize) + x);
                var pixel = new Rgba8UNorm(
                    block[blockOffset].r,
                    block[blockOffset].g,
                    block[blockOffset].b,
                    block[blockOffset].a);
                if (_mapping.IsSrgb)
                {
                    pixel = DecodeSrgb(pixel);
                }

                destinationRow[destinationX] = TPixel.FromRgba8UNorm(pixel);
            }
        }
    }

    private static void StoreHdrBlock<TPixel>(
        ReadOnlySpan<BcColorRgbFloat> block,
        int blockX,
        int blockY,
        BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockSize;
        var originY = blockY * BlockSize;
        for (var y = 0; y < BlockSize; y++)
        {
            var destinationY = originY + y;
            if (destinationY >= destination.Height)
            {
                break;
            }

            var destinationRow = destination.GetRowSpan(destinationY);
            for (var x = 0; x < BlockSize; x++)
            {
                var destinationX = originX + x;
                if (destinationX >= destination.Width)
                {
                    break;
                }

                var blockOffset = checked((y * BlockSize) + x);
                destinationRow[destinationX] = TPixel.FromRgba32Float(new Rgba32Float(
                    block[blockOffset].r,
                    block[blockOffset].g,
                    block[blockOffset].b,
                    1f));
            }
        }
    }

    private static void CopyDecodedBlock<TPixel>(Span2D<TPixel> source, Span<TPixel> destination)
        where TPixel : struct
    {
        var offset = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                destination[offset++] = source[y, x];
            }
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded BCn texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded BCn texture.", nameof(destination));
        }
    }

    private static int GetBlockCount(int size) => checked((size + BlockSize - 1) / BlockSize);

    private static bool TryGetMapping(TextureFormat format, out FormatMapping mapping)
    {
        foreach (var candidate in SMappings)
        {
            if (candidate.Format == format)
            {
                mapping = candidate;
                return true;
            }
        }

        mapping = default;
        return false;
    }

    private static Rgba8UNorm EncodeSrgb(Rgba8UNorm pixel) =>
        new(
            RgbaColorConversions.LinearUNorm8ToSrgb8(pixel.Red),
            RgbaColorConversions.LinearUNorm8ToSrgb8(pixel.Green),
            RgbaColorConversions.LinearUNorm8ToSrgb8(pixel.Blue),
            pixel.Alpha);

    private static Rgba8UNorm DecodeSrgb(Rgba8UNorm pixel) =>
        new(
            RgbaColorConversions.Srgb8ToLinearUNorm8(pixel.Red),
            RgbaColorConversions.Srgb8ToLinearUNorm8(pixel.Green),
            RgbaColorConversions.Srgb8ToLinearUNorm8(pixel.Blue),
            pixel.Alpha);

    private readonly record struct FormatMapping(
        TextureFormat Format,
        CompressionFormat CompressionFormat,
        SourceKind SourceKind,
        bool IsSrgb = false);

    private enum SourceKind
    {
        Rgba8,
        RgbFloat
    }
}
