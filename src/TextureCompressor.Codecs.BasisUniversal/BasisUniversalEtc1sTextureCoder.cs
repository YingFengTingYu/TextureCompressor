using System.Buffers.Binary;
using BasisUniversal;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.BasisUniversal;

public sealed class BasisUniversalEtc1sTextureCoder : IBasisEtc1sTextureCoder
{
    private const int Ktx2HeaderByteCount = 80;
    private const int Ktx2LevelIndexEntryByteCount = 24;
    private const int Ktx2Etc1sDfdByteCount = 44;
    private const int Ktx2Etc1sDfdDescriptorBlockHeaderByteCount = 24;
    private const int Ktx2BasisLzHeaderByteCount = 20;
    private const int Ktx2BasisLzImageDescByteCount = 20;
    private const uint Ktx2VkFormatUndefined = 0;
    private const uint Ktx2SupercompressionBasisLz = 1;
    private const byte KhrDataFormatModelEtc1s = 0xa3;
    private const byte KhrDataFormatTransferLinear = 1;
    private const byte KhrDataFormatTransferSrgb = 2;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.RgbaBasisEtc1sUNorm,
        TextureFormats.RgbaBasisEtc1sSrgb
    ];

    private readonly BasisUniversalCoderOptions _options;

    public BasisUniversalEtc1sTextureCoder(TextureFormat format, BasisUniversalCoderOptions? options = null)
    {
        if (!IsSupported(format))
        {
            throw new NotSupportedException($"Texture format '{format.Name}' is not a supported BasisUniversal.NET ETC1S format.");
        }

        Format = format;
        _options = options ?? new BasisUniversalCoderOptions();
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.RgbaBasisEtc1sUNorm
        || format == TextureFormats.RgbaBasisEtc1sSrgb;

    public void Decode<TPixel>(BasisEtc1sRawPayload source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var ktx2 = CreateBasisLzKtx2(destination.Width, destination.Height, source, Format.ValueKind == TextureValueKind.Srgb);
        using var texture = BasisKtx2Texture.Open(ktx2);

        var info = texture.Info;
        if (info.BasisTextureFormat != BasisTextureFormat.Etc1S)
        {
            throw new InvalidOperationException($"BasisUniversal.NET opened '{info.BasisTextureFormat}', but ETC1S was expected.");
        }

        var rgba32 = new byte[checked(destination.Width * destination.Height * 4)];
        var bytesWritten = texture.TranscodeImageLevel(rgba32, TranscoderTextureFormat.Rgba32, decodeFlags: _options.DecodeFlags);
        if (bytesWritten != rgba32.Length)
        {
            throw new InvalidOperationException($"BasisUniversal.NET wrote {bytesWritten} RGBA bytes; expected {rgba32.Length}.");
        }

        CopyFromRgba32(rgba32, destination);
    }

    public BasisEtc1sEncodedPayload Encode<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba32 = CopyToRgba32(source);
        var ktx2 = BasisUniversalCodec.EncodeKtx2(rgba32, source.Width, source.Height, CreateEncoderOptions());
        using var texture = BasisKtx2Texture.Open(ktx2);

        var info = texture.Info;
        if (info.BasisTextureFormat != BasisTextureFormat.Etc1S)
        {
            throw new InvalidOperationException($"BasisUniversal.NET encoded '{info.BasisTextureFormat}', but ETC1S was expected.");
        }

        return ExtractSingleImageBasisLzPayload(ktx2);
    }

    private BasisEncoderOptions CreateEncoderOptions()
    {
        if (_options.Format is { } format && format != BasisTextureFormat.Etc1S)
        {
            throw new NotSupportedException(
                $"Texture format '{Format.Name}' must be encoded as Basis texture format '{BasisTextureFormat.Etc1S}', not '{format}'.");
        }

        var flags = _options.Flags | BasisCompressionFlags.Ktx2Output;
        if (Format.ValueKind == TextureValueKind.Srgb)
        {
            flags |= BasisCompressionFlags.Srgb;
        }

        return new BasisEncoderOptions
        {
            Format = BasisTextureFormat.Etc1S,
            QualityLevel = _options.QualityLevel,
            EffortLevel = _options.EffortLevel,
            Flags = flags,
            RdoOrDctQuality = _options.RdoOrDctQuality
        };
    }

    private static BasisEtc1sEncodedPayload ExtractSingleImageBasisLzPayload(ReadOnlySpan<byte> ktx2)
    {
        ValidateKtx2Header(ktx2);

        var levelCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ktx2.Slice(40, 4)));
        var layerCount = Math.Max(1, checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ktx2.Slice(32, 4))));
        var faceCount = Math.Max(1, checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ktx2.Slice(36, 4))));
        if (levelCount == 0)
        {
            throw new InvalidDataException("BasisUniversal.NET encoded a KTX2 texture without levels.");
        }

        var imageCount = checked(levelCount * layerCount * faceCount);
        var supercompressionScheme = BinaryPrimitives.ReadUInt32LittleEndian(ktx2.Slice(44, 4));
        if (supercompressionScheme != Ktx2SupercompressionBasisLz)
        {
            throw new InvalidDataException("BasisUniversal.NET encoded KTX2 payload does not use BasisLZ supercompression.");
        }

        var sgd = SliceKtx2Range(ktx2, 64, 72, "supercompression global data");
        var levelPayload = SliceKtx2Range(ktx2, Ktx2HeaderByteCount, Ktx2HeaderByteCount + 8, "level 0 payload");
        return ReadBasisLzPayload(sgd, levelPayload, imageCount);
    }

    private static BasisEtc1sEncodedPayload ReadBasisLzPayload(ReadOnlySpan<byte> sgd, ReadOnlySpan<byte> levelPayload, int imageCount)
    {
        var imageDescBytes = checked(imageCount * Ktx2BasisLzImageDescByteCount);
        if (sgd.Length < Ktx2BasisLzHeaderByteCount + imageDescBytes)
        {
            throw new InvalidDataException("KTX2 BasisLZ supercompression global data is truncated.");
        }

        var endpointCount = BinaryPrimitives.ReadUInt16LittleEndian(sgd);
        var selectorCount = BinaryPrimitives.ReadUInt16LittleEndian(sgd.Slice(2, 2));
        var endpointByteLength = ReadDataLength(sgd.Slice(4, 4), "endpoint");
        var selectorByteLength = ReadDataLength(sgd.Slice(8, 4), "selector");
        var tableByteLength = ReadDataLength(sgd.Slice(12, 4), "Huffman table");
        var extendedByteLength = BinaryPrimitives.ReadUInt32LittleEndian(sgd.Slice(16, 4));
        if (extendedByteLength != 0)
        {
            throw new NotSupportedException("KTX2 BasisLZ extended data is not supported for ETC1S textures.");
        }

        var expectedByteLength = checked(Ktx2BasisLzHeaderByteCount + imageDescBytes + endpointByteLength + selectorByteLength + tableByteLength);
        if (sgd.Length != expectedByteLength)
        {
            throw new InvalidDataException($"KTX2 BasisLZ supercompression global data is {sgd.Length} bytes, but {expectedByteLength} bytes were expected.");
        }

        var firstImageDesc = sgd.Slice(Ktx2BasisLzHeaderByteCount, Ktx2BasisLzImageDescByteCount);
        var imageFlags = BinaryPrimitives.ReadUInt32LittleEndian(firstImageDesc);
        if ((imageFlags & ~0x02u) != 0)
        {
            throw new InvalidDataException("KTX2 BasisLZ image descriptor contains unsupported flags.");
        }

        var rgbSliceByteOffset = BinaryPrimitives.ReadUInt32LittleEndian(firstImageDesc.Slice(4, 4));
        var rgbSliceByteLength = BinaryPrimitives.ReadUInt32LittleEndian(firstImageDesc.Slice(8, 4));
        var alphaSliceByteOffset = BinaryPrimitives.ReadUInt32LittleEndian(firstImageDesc.Slice(12, 4));
        var alphaSliceByteLength = BinaryPrimitives.ReadUInt32LittleEndian(firstImageDesc.Slice(16, 4));

        var codebookOffset = checked(Ktx2BasisLzHeaderByteCount + imageDescBytes);
        var endpointData = sgd.Slice(codebookOffset, endpointByteLength).ToArray();
        codebookOffset = checked(codebookOffset + endpointByteLength);
        var selectorData = sgd.Slice(codebookOffset, selectorByteLength).ToArray();
        codebookOffset = checked(codebookOffset + selectorByteLength);
        var tableData = sgd.Slice(codebookOffset, tableByteLength).ToArray();
        var rgbSliceData = SliceBasisLzLevelPayload(levelPayload, rgbSliceByteOffset, rgbSliceByteLength, "RGB").ToArray();
        var alphaSliceData = alphaSliceByteLength == 0
            ? []
            : SliceBasisLzLevelPayload(levelPayload, alphaSliceByteOffset, alphaSliceByteLength, "alpha").ToArray();

        return new BasisEtc1sEncodedPayload(
            endpointCount,
            endpointData,
            selectorCount,
            selectorData,
            tableData,
            rgbSliceData,
            alphaSliceData,
            (imageFlags & 0x02u) != 0);
    }

    private static int ReadDataLength(ReadOnlySpan<byte> source, string sectionName)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (value == 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"KTX2 BasisLZ {sectionName} data length is outside the supported range.");
        }

        return (int)value;
    }

    private static ReadOnlySpan<byte> SliceBasisLzLevelPayload(ReadOnlySpan<byte> levelPayload, uint byteOffset, uint byteLength, string sliceName)
    {
        if (byteLength == 0)
        {
            throw new InvalidDataException($"KTX2 BasisLZ {sliceName} slice byte length must not be zero.");
        }

        var end = checked((ulong)byteOffset + byteLength);
        if (end > (ulong)levelPayload.Length)
        {
            throw new InvalidDataException($"KTX2 BasisLZ {sliceName} slice points outside its mip level payload.");
        }

        return levelPayload.Slice(checked((int)byteOffset), checked((int)byteLength));
    }

    private static byte[] CreateBasisLzKtx2(int width, int height, BasisEtc1sRawPayload payload, bool srgb)
    {
        var levelPayloadLength = checked(payload.RgbSliceData.Length + payload.AlphaSliceData.Length);
        var levelPayload = new byte[levelPayloadLength];
        payload.RgbSliceData.CopyTo(levelPayload);
        payload.AlphaSliceData.CopyTo(levelPayload.AsSpan(payload.RgbSliceData.Length));

        var sgdLength = checked(
            Ktx2BasisLzHeaderByteCount +
            Ktx2BasisLzImageDescByteCount +
            payload.EndpointData.Length +
            payload.SelectorData.Length +
            payload.TablesData.Length);
        var sgd = new byte[sgdLength];
        BinaryPrimitives.WriteUInt16LittleEndian(sgd.AsSpan(0, 2), checked((ushort)payload.EndpointCount));
        BinaryPrimitives.WriteUInt16LittleEndian(sgd.AsSpan(2, 2), checked((ushort)payload.SelectorCount));
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(4, 4), checked((uint)payload.EndpointData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(8, 4), checked((uint)payload.SelectorData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(12, 4), checked((uint)payload.TablesData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(20, 4), payload.IsPFrame ? 0x02u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(24, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(28, 4), checked((uint)payload.RgbSliceData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(32, 4), checked((uint)payload.RgbSliceData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(sgd.AsSpan(36, 4), checked((uint)payload.AlphaSliceData.Length));

        var offset = Ktx2BasisLzHeaderByteCount + Ktx2BasisLzImageDescByteCount;
        payload.EndpointData.CopyTo(sgd.AsSpan(offset));
        offset = checked(offset + payload.EndpointData.Length);
        payload.SelectorData.CopyTo(sgd.AsSpan(offset));
        offset = checked(offset + payload.SelectorData.Length);
        payload.TablesData.CopyTo(sgd.AsSpan(offset));

        var dfdOffset = checked(Ktx2HeaderByteCount + Ktx2LevelIndexEntryByteCount);
        var sgdOffset = checked(dfdOffset + Ktx2Etc1sDfdByteCount);
        var levelOffset = checked(sgdOffset + sgd.Length);
        var result = new byte[checked(levelOffset + levelPayload.Length)];
        Ktx2Identifier.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), Ktx2VkFormatUndefined);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24, 4), checked((uint)height));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(36, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(40, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(44, 4), Ktx2SupercompressionBasisLz);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(48, 4), checked((uint)dfdOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(52, 4), Ktx2Etc1sDfdByteCount);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(64, 8), checked((ulong)sgdOffset));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(72, 8), checked((ulong)sgd.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(80, 8), checked((ulong)levelOffset));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(88, 8), checked((ulong)levelPayload.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(96, 8), 0);

        WriteEtc1sDfd(result.AsSpan(dfdOffset, Ktx2Etc1sDfdByteCount), srgb);
        sgd.CopyTo(result.AsSpan(sgdOffset));
        levelPayload.CopyTo(result.AsSpan(levelOffset));
        return result;
    }

    private static void WriteEtc1sDfd(Span<byte> destination, bool srgb)
    {
        destination.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Ktx2Etc1sDfdByteCount);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10, 2), Ktx2Etc1sDfdByteCount - 4);
        destination[12] = KhrDataFormatModelEtc1s;
        destination[13] = 1;
        destination[14] = srgb ? KhrDataFormatTransferSrgb : KhrDataFormatTransferLinear;
        destination[16] = 3;
        destination[17] = 3;
        destination[20] = 8;

        const int sampleOffset = 4 + Ktx2Etc1sDfdDescriptorBlockHeaderByteCount;
        destination[sampleOffset + 2] = 63;
        destination[sampleOffset + 12] = byte.MaxValue;
        destination[sampleOffset + 13] = byte.MaxValue;
        destination[sampleOffset + 14] = byte.MaxValue;
        destination[sampleOffset + 15] = byte.MaxValue;
    }

    private static ReadOnlySpan<byte> SliceKtx2Range(ReadOnlySpan<byte> ktx2, int offsetOffset, int lengthOffset, string name)
    {
        var byteOffset = BinaryPrimitives.ReadUInt64LittleEndian(ktx2.Slice(offsetOffset, 8));
        var byteLength = BinaryPrimitives.ReadUInt64LittleEndian(ktx2.Slice(lengthOffset, 8));
        var byteEnd = checked(byteOffset + byteLength);
        if (byteLength > int.MaxValue || byteEnd > (ulong)ktx2.Length)
        {
            throw new InvalidDataException($"KTX2 {name} points outside the payload.");
        }

        return ktx2.Slice(checked((int)byteOffset), checked((int)byteLength));
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

    private static ReadOnlySpan<byte> Ktx2Identifier =>
    [
        0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a
    ];
}
