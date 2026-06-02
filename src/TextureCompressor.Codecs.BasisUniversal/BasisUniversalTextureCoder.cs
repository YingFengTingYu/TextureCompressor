using System.Buffers.Binary;
using BasisUniversal;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.BasisUniversal;

public sealed class BasisUniversalTextureCoder : IPitchTextureCoder
{
    private const int Ktx2HeaderByteCount = 80;
    private const int Ktx2LevelIndexEntryByteCount = 24;
    private const int Ktx2UastcDfdByteCount = 44;
    private const int Ktx2UastcDfdDescriptorBlockHeaderByteCount = 24;
    private const int KhrDataFormatModelUastc = 166;
    private const int KhrDataFormatChannelUastcRgba = 3;
    private const int KhrDataFormatTransferLinear = 1;
    private const int KhrDataFormatTransferSrgb = 2;
    private const uint Ktx2VkFormatUndefined = 0;
    private const uint Ktx2SupercompressionNone = 0;

    private static readonly FormatMapping[] SMappings =
    [
        new(
            TextureFormats.RgbaBasisUastcLdr4x4UNorm,
            TranscoderTextureFormat.Rgba32,
            BasisTextureFormat.UastcLdr4x4,
            IsRawBasisPayload: true),
        new(
            TextureFormats.RgbaBasisUastcLdr4x4Srgb,
            TranscoderTextureFormat.Rgba32,
            BasisTextureFormat.UastcLdr4x4,
            IsRawBasisPayload: true),

        new(TextureFormats.RgbEtc1UNorm, TranscoderTextureFormat.Etc1Rgb),
        new(TextureFormats.RgbaEtc2EacUNorm, TranscoderTextureFormat.Etc2Rgba),
        new(TextureFormats.RgbaEtc2EacSrgb, TranscoderTextureFormat.Etc2Rgba),
        new(TextureFormats.R11EacUNorm, TranscoderTextureFormat.Etc2EacR11),
        new(TextureFormats.Rg11EacUNorm, TranscoderTextureFormat.Etc2EacRg11),

        new(TextureFormats.Bc1Rgb, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Bc1RgbSrgb, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Bc1Rgba, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Bc1RgbaSrgb, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Dxt1Rgb, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Dxt1RgbSrgb, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Dxt1Rgba, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Dxt1RgbaSrgb, TranscoderTextureFormat.Bc1Rgb),
        new(TextureFormats.Bc3Rgba, TranscoderTextureFormat.Bc3Rgba),
        new(TextureFormats.Bc3RgbaSrgb, TranscoderTextureFormat.Bc3Rgba),
        new(TextureFormats.Dxt5Rgba, TranscoderTextureFormat.Bc3Rgba),
        new(TextureFormats.Dxt5RgbaSrgb, TranscoderTextureFormat.Bc3Rgba),
        new(TextureFormats.Bc4UNorm, TranscoderTextureFormat.Bc4R),
        new(TextureFormats.Bc5UNorm, TranscoderTextureFormat.Bc5Rg),
        new(TextureFormats.Bc7UNorm, TranscoderTextureFormat.Bc7Rgba),
        new(TextureFormats.Bc7Srgb, TranscoderTextureFormat.Bc7Rgba),
        new(TextureFormats.RgbaBptcUNorm, TranscoderTextureFormat.Bc7Rgba),
        new(TextureFormats.RgbaBptcSrgb, TranscoderTextureFormat.Bc7Rgba),

        new(TextureFormats.RgbPvrtcI4BppUNorm, TranscoderTextureFormat.Pvrtc1_4Rgb),
        new(TextureFormats.RgbPvrtcI4BppSrgb, TranscoderTextureFormat.Pvrtc1_4Rgb),
        new(TextureFormats.RgbaPvrtcI4BppUNorm, TranscoderTextureFormat.Pvrtc1_4Rgba),
        new(TextureFormats.RgbaPvrtcI4BppSrgb, TranscoderTextureFormat.Pvrtc1_4Rgba),
        new(TextureFormats.RgbaPvrtcII4BppUNorm, TranscoderTextureFormat.Pvrtc2_4Rgba, BasisTextureFormat.Etc1S),
        new(TextureFormats.RgbaPvrtcII4BppSrgb, TranscoderTextureFormat.Pvrtc2_4Rgba, BasisTextureFormat.Etc1S),

        new(TextureFormats.RgbFxt1UNorm, TranscoderTextureFormat.Fxt1Rgb, BasisTextureFormat.Etc1S),
        new(TextureFormats.AtcRgb, TranscoderTextureFormat.AtcRgb, BasisTextureFormat.Etc1S),
        new(TextureFormats.AtcRgbaInterpolatedAlpha, TranscoderTextureFormat.AtcRgba, BasisTextureFormat.Etc1S),

        new(TextureFormats.RgbaAstc4x4UNorm, TranscoderTextureFormat.AstcLdr4x4Rgba, BasisTextureFormat.AstcLdr4x4),
        new(TextureFormats.RgbaAstc4x4Srgb, TranscoderTextureFormat.AstcLdr4x4Rgba, BasisTextureFormat.AstcLdr4x4),
        new(TextureFormats.RgbaAstc5x4UNorm, TranscoderTextureFormat.AstcLdr5x4Rgba, BasisTextureFormat.AstcLdr5x4),
        new(TextureFormats.RgbaAstc5x4Srgb, TranscoderTextureFormat.AstcLdr5x4Rgba, BasisTextureFormat.AstcLdr5x4),
        new(TextureFormats.RgbaAstc5x5UNorm, TranscoderTextureFormat.AstcLdr5x5Rgba, BasisTextureFormat.AstcLdr5x5),
        new(TextureFormats.RgbaAstc5x5Srgb, TranscoderTextureFormat.AstcLdr5x5Rgba, BasisTextureFormat.AstcLdr5x5),
        new(TextureFormats.RgbaAstc6x5UNorm, TranscoderTextureFormat.AstcLdr6x5Rgba, BasisTextureFormat.AstcLdr6x5),
        new(TextureFormats.RgbaAstc6x5Srgb, TranscoderTextureFormat.AstcLdr6x5Rgba, BasisTextureFormat.AstcLdr6x5),
        new(TextureFormats.RgbaAstc6x6UNorm, TranscoderTextureFormat.AstcLdr6x6Rgba, BasisTextureFormat.AstcLdr6x6),
        new(TextureFormats.RgbaAstc6x6Srgb, TranscoderTextureFormat.AstcLdr6x6Rgba, BasisTextureFormat.AstcLdr6x6),
        new(TextureFormats.RgbaAstc8x5UNorm, TranscoderTextureFormat.AstcLdr8x5Rgba, BasisTextureFormat.AstcLdr8x5),
        new(TextureFormats.RgbaAstc8x5Srgb, TranscoderTextureFormat.AstcLdr8x5Rgba, BasisTextureFormat.AstcLdr8x5),
        new(TextureFormats.RgbaAstc8x6UNorm, TranscoderTextureFormat.AstcLdr8x6Rgba, BasisTextureFormat.AstcLdr8x6),
        new(TextureFormats.RgbaAstc8x6Srgb, TranscoderTextureFormat.AstcLdr8x6Rgba, BasisTextureFormat.AstcLdr8x6),
        new(TextureFormats.RgbaAstc8x8UNorm, TranscoderTextureFormat.AstcLdr8x8Rgba, BasisTextureFormat.AstcLdr8x8),
        new(TextureFormats.RgbaAstc8x8Srgb, TranscoderTextureFormat.AstcLdr8x8Rgba, BasisTextureFormat.AstcLdr8x8),
        new(TextureFormats.RgbaAstc10x5UNorm, TranscoderTextureFormat.AstcLdr10x5Rgba, BasisTextureFormat.AstcLdr10x5),
        new(TextureFormats.RgbaAstc10x5Srgb, TranscoderTextureFormat.AstcLdr10x5Rgba, BasisTextureFormat.AstcLdr10x5),
        new(TextureFormats.RgbaAstc10x6UNorm, TranscoderTextureFormat.AstcLdr10x6Rgba, BasisTextureFormat.AstcLdr10x6),
        new(TextureFormats.RgbaAstc10x6Srgb, TranscoderTextureFormat.AstcLdr10x6Rgba, BasisTextureFormat.AstcLdr10x6),
        new(TextureFormats.RgbaAstc10x8UNorm, TranscoderTextureFormat.AstcLdr10x8Rgba, BasisTextureFormat.AstcLdr10x8),
        new(TextureFormats.RgbaAstc10x8Srgb, TranscoderTextureFormat.AstcLdr10x8Rgba, BasisTextureFormat.AstcLdr10x8),
        new(TextureFormats.RgbaAstc10x10UNorm, TranscoderTextureFormat.AstcLdr10x10Rgba, BasisTextureFormat.AstcLdr10x10),
        new(TextureFormats.RgbaAstc10x10Srgb, TranscoderTextureFormat.AstcLdr10x10Rgba, BasisTextureFormat.AstcLdr10x10),
        new(TextureFormats.RgbaAstc12x10UNorm, TranscoderTextureFormat.AstcLdr12x10Rgba, BasisTextureFormat.AstcLdr12x10),
        new(TextureFormats.RgbaAstc12x10Srgb, TranscoderTextureFormat.AstcLdr12x10Rgba, BasisTextureFormat.AstcLdr12x10),
        new(TextureFormats.RgbaAstc12x12UNorm, TranscoderTextureFormat.AstcLdr12x12Rgba, BasisTextureFormat.AstcLdr12x12),
        new(TextureFormats.RgbaAstc12x12Srgb, TranscoderTextureFormat.AstcLdr12x12Rgba, BasisTextureFormat.AstcLdr12x12)
    ];

    private static readonly TextureFormat[] SSupportedFormats = SMappings.Select(static mapping => mapping.Format).ToArray();

    private readonly FormatMapping _mapping;
    private readonly BasisUniversalCoderOptions _options;
    private readonly ITextureCoder? _decoder;

    public BasisUniversalTextureCoder(TextureFormat format, BasisUniversalCoderOptions? options = null)
    {
        if (!TryGetMapping(format, out _mapping))
        {
            throw new NotSupportedException($"BasisUniversal.NET does not have a mapped coder for texture format '{format.Name}'.");
        }

        Format = format;
        _options = options ?? new BasisUniversalCoderOptions();
        _decoder = _mapping.IsRawBasisPayload ? null : CreateDecoder(format);
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

        return checked(rowPitch * GetBlockRowCount(width, height));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (_mapping.IsRawBasisPayload)
        {
            DecodeRawBasisPayload(source, destination, rowPitch);
            return;
        }

        if (_decoder is IPitchTextureCoder pitchDecoder)
        {
            pitchDecoder.Decode(source, destination, rowPitch);
            return;
        }

        var defaultPitch = GetDefaultPitch(destination.Width);
        if (rowPitch != defaultPitch)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "This format does not support non-default row pitch decoding.");
        }

        var decoder = _decoder ?? throw new InvalidOperationException("A BasisUniversal.NET decoder was not created for this texture format.");
        decoder.Decode(source, destination);
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        if (_mapping.IsRawBasisPayload)
        {
            EncodeRawBasisPayload(source, destination, rowPitch);
            return;
        }

        var rgba32 = CopyToRgba32(source);
        var ktx2 = BasisUniversalCodec.EncodeKtx2(rgba32, source.Width, source.Height, CreateEncoderOptions());
        using var texture = BasisKtx2Texture.Open(ktx2);

        if (!BasisUniversalCodec.IsTranscoderFormatSupported(_mapping.TranscoderFormat, texture.Info.BasisTextureFormat))
        {
            throw new NotSupportedException(
                $"Basis texture format '{texture.Info.BasisTextureFormat}' cannot transcode to '{_mapping.TranscoderFormat}'.");
        }

        var packed = new byte[Format.GetByteCount(source.Width, source.Height)];
        var bytesWritten = texture.TranscodeImageLevel(
            packed,
            _mapping.TranscoderFormat,
            levelIndex: 0,
            layerIndex: 0,
            faceIndex: 0,
            _options.DecodeFlags);

        if (bytesWritten != packed.Length)
        {
            throw new InvalidOperationException($"BasisUniversal.NET wrote {bytesWritten} bytes; expected {packed.Length}.");
        }

        CopyPackedRowsToDestination(packed, source.Width, source.Height, destination, rowPitch);
    }

    private BasisEncoderOptions CreateEncoderOptions()
    {
        var flags = _options.Flags | BasisCompressionFlags.Ktx2Output;
        if (Format.ValueKind == TextureValueKind.Srgb)
        {
            flags |= BasisCompressionFlags.Srgb;
        }

        return new BasisEncoderOptions
        {
            Format = _options.Format ?? _mapping.EncoderFormat,
            QualityLevel = _options.QualityLevel,
            EffortLevel = _options.EffortLevel,
            Flags = flags,
            RdoOrDctQuality = _options.RdoOrDctQuality
        };
    }

    private void DecodeRawBasisPayload<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        var packed = CopyPackedRowsFromSource(source, destination.Width, destination.Height, rowPitch);
        var ktx2 = CreateUastcKtx2(packed, destination.Width, destination.Height, Format.ValueKind == TextureValueKind.Srgb);
        using var texture = BasisKtx2Texture.Open(ktx2);

        var info = texture.Info;
        if (info.BasisTextureFormat != BasisTextureFormat.UastcLdr4x4)
        {
            throw new InvalidOperationException($"BasisUniversal.NET opened '{info.BasisTextureFormat}', but UASTC LDR 4x4 was expected.");
        }

        var rgba32 = new byte[checked(destination.Width * destination.Height * 4)];
        var bytesWritten = texture.TranscodeImageLevel(rgba32, TranscoderTextureFormat.Rgba32, decodeFlags: _options.DecodeFlags);
        if (bytesWritten != rgba32.Length)
        {
            throw new InvalidOperationException($"BasisUniversal.NET wrote {bytesWritten} RGBA bytes; expected {rgba32.Length}.");
        }

        CopyFromRgba32(rgba32, destination);
    }

    private void EncodeRawBasisPayload<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba32 = CopyToRgba32(source);
        var ktx2 = BasisUniversalCodec.EncodeKtx2(rgba32, source.Width, source.Height, CreateRawBasisEncoderOptions());
        using var texture = BasisKtx2Texture.Open(ktx2);

        var info = texture.Info;
        if (info.BasisTextureFormat != BasisTextureFormat.UastcLdr4x4)
        {
            throw new InvalidOperationException($"BasisUniversal.NET encoded '{info.BasisTextureFormat}', but UASTC LDR 4x4 was expected.");
        }

        var packed = ExtractSingleLevelKtx2Payload(ktx2);
        if (packed.Length != Format.GetByteCount(source.Width, source.Height))
        {
            throw new InvalidOperationException($"BasisUniversal.NET wrote {packed.Length} UASTC bytes; expected {Format.GetByteCount(source.Width, source.Height)}.");
        }

        CopyPackedRowsToDestination(packed, source.Width, source.Height, destination, rowPitch);
    }

    private BasisEncoderOptions CreateRawBasisEncoderOptions()
    {
        if (_options.Format is { } format && format != _mapping.EncoderFormat)
        {
            throw new NotSupportedException(
                $"Texture format '{Format.Name}' must be encoded as Basis texture format '{_mapping.EncoderFormat}', not '{format}'.");
        }

        var flags = _options.Flags | BasisCompressionFlags.Ktx2Output;
        if ((flags & BasisCompressionFlags.Ktx2UastcZstd) != 0)
        {
            throw new NotSupportedException($"Texture format '{Format.Name}' requires raw UASTC blocks and cannot use KTX2 UASTC Zstandard supercompression.");
        }

        if (Format.ValueKind == TextureValueKind.Srgb)
        {
            flags |= BasisCompressionFlags.Srgb;
        }

        return new BasisEncoderOptions
        {
            Format = _mapping.EncoderFormat,
            QualityLevel = _options.QualityLevel,
            EffortLevel = _options.EffortLevel,
            Flags = flags,
            RdoOrDctQuality = _options.RdoOrDctQuality
        };
    }

    private static byte[] CopyToRgba32<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var result = new byte[checked(source.Width * source.Height * 4)];
        var offset = 0;
        foreach (var pixel in source.Pixels)
        {
            var rgba = TPixel.ToRgba8UNorm(pixel);
            result[offset++] = rgba.Red;
            result[offset++] = rgba.Green;
            result[offset++] = rgba.Blue;
            result[offset++] = rgba.Alpha;
        }

        return result;
    }

    private static void CopyFromRgba32<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var offset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            for (var x = 0; x < destination.Width; x++)
            {
                destination[x, y] = TPixel.FromRgba8UNorm(new Rgba8UNorm(
                    source[offset],
                    source[offset + 1],
                    source[offset + 2],
                    source[offset + 3]));
                offset += 4;
            }
        }
    }

    private byte[] CopyPackedRowsFromSource(ReadOnlySpan<byte> source, int width, int height, int rowPitch)
    {
        var rowByteCount = Format.GetRowByteCount(width);
        var blockRows = GetBlockRowCount(width, height);
        var byteCount = Format.GetByteCount(width, height);
        if (rowPitch == rowByteCount)
        {
            return source[..byteCount].ToArray();
        }

        var packed = new byte[byteCount];
        for (var row = 0; row < blockRows; row++)
        {
            source.Slice(checked(row * rowPitch), rowByteCount).CopyTo(packed.AsSpan(checked(row * rowByteCount), rowByteCount));
        }

        return packed;
    }

    private void CopyPackedRowsToDestination(ReadOnlySpan<byte> packed, int width, int height, Span<byte> destination, int rowPitch)
    {
        var rowByteCount = Format.GetRowByteCount(width);
        var blockRows = GetBlockRowCount(width, height);
        if (rowPitch == rowByteCount)
        {
            packed.CopyTo(destination);
            return;
        }

        for (var row = 0; row < blockRows; row++)
        {
            packed.Slice(checked(row * rowByteCount), rowByteCount).CopyTo(destination.Slice(checked(row * rowPitch), rowByteCount));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = Format.GetRowByteCount(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        var required = checked(rowPitch * GetBlockRowCount(width, height));
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination span is too small for the texture dimensions and row pitch.", nameof(destination));
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = Format.GetRowByteCount(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        var required = checked(rowPitch * GetBlockRowCount(width, height));
        if (source.Length < required)
        {
            throw new ArgumentException("Source span is too small for the texture dimensions and row pitch.", nameof(source));
        }
    }

    private int GetBlockRowCount(int width, int height) =>
        checked(Format.GetByteCount(width, height) / Format.GetRowByteCount(width));

    private static byte[] ExtractSingleLevelKtx2Payload(ReadOnlySpan<byte> ktx2)
    {
        ValidateKtx2Header(ktx2);

        var levelCount = BinaryPrimitives.ReadUInt32LittleEndian(ktx2.Slice(40, 4));
        if (levelCount == 0)
        {
            throw new InvalidDataException("BasisUniversal.NET encoded a KTX2 texture without levels.");
        }

        var supercompressionScheme = BinaryPrimitives.ReadUInt32LittleEndian(ktx2.Slice(44, 4));
        if (supercompressionScheme != Ktx2SupercompressionNone)
        {
            throw new NotSupportedException("BasisUniversal.NET encoded a supercompressed KTX2 level; raw UASTC blocks were expected.");
        }

        var levelByteOffset = BinaryPrimitives.ReadUInt64LittleEndian(ktx2.Slice(Ktx2HeaderByteCount, 8));
        var levelByteLength = BinaryPrimitives.ReadUInt64LittleEndian(ktx2.Slice(Ktx2HeaderByteCount + 8, 8));
        var levelEnd = checked(levelByteOffset + levelByteLength);
        if (levelByteLength > int.MaxValue || levelEnd > (ulong)ktx2.Length)
        {
            throw new InvalidDataException("BasisUniversal.NET encoded a KTX2 level outside the payload.");
        }

        return ktx2.Slice(checked((int)levelByteOffset), checked((int)levelByteLength)).ToArray();
    }

    private static byte[] CreateUastcKtx2(ReadOnlySpan<byte> payload, int width, int height, bool srgb)
    {
        var dfdOffset = checked(Ktx2HeaderByteCount + Ktx2LevelIndexEntryByteCount);
        var levelOffset = AlignUp(checked(dfdOffset + Ktx2UastcDfdByteCount), BasisUastcLdr4x4TextureCoder.BytesPerBlock);
        var result = new byte[checked(levelOffset + payload.Length)];

        Ktx2Identifier.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), Ktx2VkFormatUndefined);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(36, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(40, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(44, 4), Ktx2SupercompressionNone);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(48, 4), checked((uint)dfdOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(52, 4), Ktx2UastcDfdByteCount);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(80, 8), checked((ulong)levelOffset));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(88, 8), checked((ulong)payload.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(96, 8), checked((ulong)payload.Length));

        WriteUastcDfd(result.AsSpan(dfdOffset, Ktx2UastcDfdByteCount), srgb);
        payload.CopyTo(result.AsSpan(levelOffset));
        return result;
    }

    private static void WriteUastcDfd(Span<byte> destination, bool srgb)
    {
        destination.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Ktx2UastcDfdByteCount);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10, 2), Ktx2UastcDfdByteCount - 4);
        destination[12] = KhrDataFormatModelUastc;
        destination[13] = 1;
        destination[14] = (byte)(srgb ? KhrDataFormatTransferSrgb : KhrDataFormatTransferLinear);
        destination[16] = BasisUastcLdr4x4TextureCoder.BlockWidth - 1;
        destination[17] = BasisUastcLdr4x4TextureCoder.BlockHeight - 1;
        destination[20] = BasisUastcLdr4x4TextureCoder.BytesPerBlock;

        const int sampleOffset = 4 + Ktx2UastcDfdDescriptorBlockHeaderByteCount;
        destination[sampleOffset + 2] = 127;
        destination[sampleOffset + 3] = KhrDataFormatChannelUastcRgba;
        destination[sampleOffset + 12] = byte.MaxValue;
        destination[sampleOffset + 13] = byte.MaxValue;
        destination[sampleOffset + 14] = byte.MaxValue;
        destination[sampleOffset + 15] = byte.MaxValue;
    }

    private static void ValidateKtx2Header(ReadOnlySpan<byte> ktx2)
    {
        if (ktx2.Length < Ktx2HeaderByteCount + Ktx2LevelIndexEntryByteCount)
        {
            throw new InvalidDataException("BasisUniversal.NET encoded a truncated KTX2 payload.");
        }

        if (!ktx2[..Ktx2Identifier.Length].SequenceEqual(Ktx2Identifier))
        {
            throw new InvalidDataException("BasisUniversal.NET encoded payload is not KTX2.");
        }
    }

    private static int AlignUp(int value, int alignment)
    {
        var mask = alignment - 1;
        return checked((value + mask) & ~mask);
    }

    private static ReadOnlySpan<byte> Ktx2Identifier =>
    [
        0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a
    ];

    private static ITextureCoder CreateDecoder(TextureFormat format)
    {
        if (EtcTextureCoder.IsSupported(format))
        {
            return new EtcTextureCoder(format);
        }

        if (S3tcTextureCoder.IsSupported(format))
        {
            return new S3tcTextureCoder(format);
        }

        if (RgtcLatcTextureCoder.IsSupported(format))
        {
            return new RgtcLatcTextureCoder(format);
        }

        if (BptcTextureCoder.IsSupported(format))
        {
            return new BptcTextureCoder(format);
        }

        if (PvrtcTextureCoder.IsSupported(format))
        {
            return new PvrtcTextureCoder(format);
        }

        if (FxtcTextureCoder.IsSupported(format))
        {
            return new FxtcTextureCoder(format);
        }

        if (AtcTextureCoder.IsSupported(format))
        {
            return new AtcTextureCoder(format);
        }

        if (AstcTextureCoder.IsSupported(format))
        {
            return new AstcTextureCoder(format);
        }

        throw new NotSupportedException($"No built-in decoder is available for texture format '{format.Name}'.");
    }

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

    private readonly record struct FormatMapping(
        TextureFormat Format,
        TranscoderTextureFormat TranscoderFormat,
        BasisTextureFormat EncoderFormat = BasisTextureFormat.UastcLdr4x4,
        bool IsRawBasisPayload = false);
}
