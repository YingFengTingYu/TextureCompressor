using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using ZstdSharp;
using TextureCompressor.Registry;

namespace TextureCompressor.FileFormats.Ktx;

public static class KtxCodec
{
    private const int HeaderByteCount = 64;
    private const int HeaderV2ByteCount = 80;
    private const int IdentifierByteCount = 12;
    private const int LevelIndexEntryByteCount = 24;
    private const int BasicDfdByteCount = 24;
    private const int BasisLzHeaderByteCount = 20;
    private const int BasisLzImageDescByteCount = 20;
    private const uint BasisLzImageFlagIsPFrame = 0x02;
    private const uint LittleEndianMarker = 0x04030201;
    private const uint BigEndianMarker = 0x01020304;

    private static readonly Lazy<Mappings> SFormatMappings = new(CreateFormatMappings);

    private static ReadOnlySpan<byte> Identifier => [0xab, 0x4b, 0x54, 0x58, 0x20, 0x31, 0x31, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a];
    private static ReadOnlySpan<byte> IdentifierV2 => [0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a];

    public static KtxTexture Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static KtxTexture Read(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Read(stream);
    }

    public static KtxTexture Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> identifier = stackalloc byte[IdentifierByteCount];
        ReadExactly(stream, identifier);
        if (identifier.SequenceEqual(IdentifierV2))
        {
            return ReadV2(stream);
        }

        if (!identifier.SequenceEqual(Identifier))
        {
            throw new InvalidDataException("The stream is not a KTX file.");
        }

        var header = ReadHeader(stream);
        ValidateHeader(header);
        SkipExactly(stream, checked((int)header.BytesOfKeyValueData));

        var format = GetTextureFormat(header);
        var depth = GetTextureDepth(header.PixelDepth);
        var coder = depth == 1 ? TextureCoderManager.Global.GetCoder(format) : null;
        var coder3D = depth == 1 ? null : TextureCoderManager.Global.GetCoder3D(format);
        var mipLevelCount = GetMipLevelCount(header.NumberOfMipmapLevels, header.Width, header.Height, depth, "KTX");
        var arrayLayerCount = GetArrayLayerCount(header.NumberOfArrayElements, "KTX");
        var faceCount = GetFaceCount(header.NumberOfFaces, "KTX");
        ValidateTextureShape(depth, arrayLayerCount, faceCount, "KTX");
        var subresources = ReadSubresources(stream, header, format, coder, coder3D, depth, mipLevelCount, arrayLayerCount, faceCount);

        return new KtxTexture(
            format,
            subresources,
            arrayLayerCount,
            faceCount,
            header.GlType == 0 ? null : (KtxGlFormat)header.GlType,
            header.GlFormat == 0 ? null : (KtxGlFormat)header.GlFormat,
            (KtxGlFormat)header.GlInternalFormat,
            (KtxGlFormat)header.GlBaseInternalFormat,
            vkFormat: null);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode(stream);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(Stream stream) => Decode<Rgba8UNorm>(stream);

    public static ArrayBitmap<TPixel> Decode<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return Decode<TPixel>(stream);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode<TPixel>(stream);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var texture = Read(stream);
        return Decode<TPixel>(texture);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(KtxTexture texture)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.Texture.Depth != 1)
        {
            throw new NotSupportedException("Use DecodeVolume for KTX 3D texture files.");
        }

        var bitmap = new ArrayBitmap<TPixel>(texture.Texture.Width, texture.Texture.Height);
        var coder = TextureCoderManager.Global.GetCoder(texture.Texture.Format);
        coder.Decode(texture.Texture.Payload, bitmap.AsView());
        return bitmap;
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(string path)
    {
        using var stream = File.OpenRead(path);
        return DecodeVolume(stream);
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeVolume(stream);
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(Stream stream) => DecodeVolume<Rgba8UNorm>(stream);

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return DecodeVolume<TPixel>(stream);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeVolume<TPixel>(stream);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var texture = Read(stream);
        return DecodeVolume<TPixel>(texture);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(KtxTexture texture)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        var bitmap = new ArrayVolumeBitmap<TPixel>(texture.Texture.Width, texture.Texture.Height, texture.Texture.Depth);
        var coder = TextureCoderManager.Global.GetCoder3D(texture.Texture.Format);
        coder.Decode(texture.Texture.Payload, bitmap.AsView());
        return bitmap;
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options: null);
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, options: null);

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        Encode(bitmap, stream, options);
        return stream.ToArray();
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options: null);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options: null);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options: null);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var format = GetEncodingTextureFormat(options);
        var coder = TextureCoderManager.Global.GetCoder(format);
        if (options?.GenerateMipmaps == true)
        {
            var encodedSubresources = EncodeMipSubresources(
                BitmapMipChain.Generate(bitmap, TextureMipmapGenerationOptions.GetDefault(format, options.MipmapOptions)),
                coder);
            Write(new KtxTexture(format, encodedSubresources, faceCount: 1), stream, options);
            return;
        }

        var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
        coder.Encode(bitmap, payload);
        Write(new KtxTexture(format, bitmap.Width, bitmap.Height, payload), stream, options);
    }

    public static byte[] EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels)
        where TPixel : unmanaged, IPixel<TPixel> =>
        EncodeMipChain(mipLevels, options: null);

    public static byte[] EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        EncodeMipChain(mipLevels, stream, options);
        return stream.ToArray();
    }

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        EncodeMipChain(mipLevels, stream, options: null);
    }

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, string path, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        EncodeMipChain(mipLevels, stream, options);
    }

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        EncodeMipChain(mipLevels, stream, options: null);

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, Stream stream, KtxEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var format = GetEncodingTextureFormat(options);
        var coder = TextureCoderManager.Global.GetCoder(format);
        var encodedSubresources = EncodeMipSubresources(mipLevels, coder);
        Write(new KtxTexture(format, encodedSubresources, faceCount: 1), stream, options);
    }

    public static byte[] Write(KtxTexture texture)
    {
        using var stream = new MemoryStream();
        Write(texture, stream, options: null);
        return stream.ToArray();
    }

    public static byte[] Write(KtxTexture texture, KtxEncodingOptions? options)
    {
        using var stream = new MemoryStream();
        Write(texture, stream, options);
        return stream.ToArray();
    }

    public static void Write(KtxTexture texture, string path)
    {
        using var stream = File.Create(path);
        Write(texture, stream, options: null);
    }

    public static void Write(KtxTexture texture, string path, KtxEncodingOptions? options)
    {
        using var stream = File.Create(path);
        Write(texture, stream, options);
    }

    public static void Write(KtxTexture texture, Stream stream)
    {
        Write(texture, stream, options: null);
    }

    public static void Write(KtxTexture texture, Stream stream, KtxEncodingOptions? options)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        var coder = texture.Texture.Depth == 1 ? TextureCoderManager.Global.GetCoder(texture.Texture.Format) : null;
        var coder3D = texture.Texture.Depth == 1 ? null : TextureCoderManager.Global.GetCoder3D(texture.Texture.Format);
        ValidateTexturePayloads(texture, coder, coder3D);

        var version = options?.Version ?? KtxVersion.Version1;
        ValidateEncodingVersion(version);

        if (version == KtxVersion.Version2)
        {
            ValidateV2EncodingSelection(texture.Texture.Format, options);
            WriteV2(texture, stream, options);
            return;
        }

        if ((options?.SupercompressionScheme ?? KtxSupercompressionScheme.None) != KtxSupercompressionScheme.None)
        {
            throw new NotSupportedException("KTX v1 supercompression is not supported.");
        }

        var descriptor = GetDescriptor(texture.Texture.Format, options);
        WriteHeader(stream, new KtxHeader(
            LittleEndian: true,
            descriptor.GlType,
            descriptor.GlTypeSize,
            descriptor.GlFormat,
            descriptor.GlInternalFormat,
            descriptor.GlBaseInternalFormat,
            texture.Texture.Width,
            texture.Texture.Height,
            PixelDepth: texture.Texture.Depth == 1 ? 0 : checked((uint)texture.Texture.Depth),
            NumberOfArrayElements: texture.Texture.ArrayLayerCount == 1 ? 0 : checked((uint)texture.Texture.ArrayLayerCount),
            NumberOfFaces: checked((uint)texture.Texture.FaceCount),
            NumberOfMipmapLevels: checked((uint)texture.Texture.MipLevelCount),
            BytesOfKeyValueData: 0));

        for (var mipLevel = 0; mipLevel < texture.Texture.MipLevelCount; mipLevel++)
        {
            var level = texture.Texture.GetSubresource(mipLevel);
            var imageByteCount = GetImageByteCount(
                texture.Texture.Format,
                level.Width,
                level.Height,
                level.Depth,
                GetEncodedByteCount(coder, coder3D, level.Width, level.Height, level.Depth));

            WriteUInt32(stream, checked((uint)imageByteCount));
            for (var arrayLayer = 0; arrayLayer < texture.Texture.ArrayLayerCount; arrayLayer++)
            {
                for (var face = 0; face < texture.Texture.FaceCount; face++)
                {
                    var subresource = texture.Texture.GetSubresource(mipLevel, arrayLayer, face);
                    var image = RequiresRowPadding(texture.Texture.Format, subresource.Width)
                        ? AddRowPadding(subresource.Payload, texture.Texture.Format.GetRowByteCount(subresource.Width), checked(subresource.Height * subresource.Depth))
                        : subresource.Payload;

                    stream.Write(image);
                    WritePadding(stream, GetPaddingByteCount(image.Length));
                }
            }
        }
    }

    private static KtxHeader ReadHeader(Stream stream)
    {
        Byte52Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        ReadExactly(stream, buffer);

        var endianness = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        var littleEndian = endianness switch
        {
            LittleEndianMarker => true,
            BigEndianMarker => false,
            _ => throw new InvalidDataException("KTX endianness marker is invalid.")
        };

        return new KtxHeader(
            littleEndian,
            ReadUInt32(buffer.Slice(4, 4), littleEndian),
            ReadUInt32(buffer.Slice(8, 4), littleEndian),
            ReadUInt32(buffer.Slice(12, 4), littleEndian),
            ReadUInt32(buffer.Slice(16, 4), littleEndian),
            ReadUInt32(buffer.Slice(20, 4), littleEndian),
            ReadPositiveInt(buffer.Slice(24, 4), littleEndian, "width"),
            ReadPositiveInt(buffer.Slice(28, 4), littleEndian, "height"),
            ReadUInt32(buffer.Slice(32, 4), littleEndian),
            ReadUInt32(buffer.Slice(36, 4), littleEndian),
            ReadUInt32(buffer.Slice(40, 4), littleEndian),
            ReadUInt32(buffer.Slice(44, 4), littleEndian),
            ReadUInt32(buffer.Slice(48, 4), littleEndian));
    }

    private static void WriteHeader(Stream stream, KtxHeader header)
    {
        Byte64Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        buffer.Clear();

        Identifier.CopyTo(buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), LittleEndianMarker);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), header.GlType);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(20, 4), header.GlTypeSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24, 4), header.GlFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(28, 4), header.GlInternalFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), header.GlBaseInternalFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(36, 4), checked((uint)header.Width));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(40, 4), checked((uint)header.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(44, 4), header.PixelDepth);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(48, 4), header.NumberOfArrayElements);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(52, 4), header.NumberOfFaces);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(56, 4), header.NumberOfMipmapLevels);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(60, 4), header.BytesOfKeyValueData);
        stream.Write(buffer);
    }

    private static KtxTexture ReadV2(Stream stream)
    {
        Byte68Buffer headerStorage = default;
        Span<byte> header = headerStorage;
        ReadExactly(stream, header);

        var ktxHeader = new KtxHeaderV2(
            (KtxVkFormat)BinaryPrimitives.ReadUInt32LittleEndian(header),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4)),
            ReadPositiveInt(header.Slice(8, 4), littleEndian: true, "width"),
            ReadPositiveInt(header.Slice(12, 4), littleEndian: true, "height"),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(28, 4)),
            (KtxSupercompressionScheme)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(32, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(36, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(40, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(44, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(48, 4)),
            BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(52, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(60, 8)));
        ValidateHeader(ktxHeader);

        var depth = GetTextureDepth(ktxHeader.PixelDepth);
        var mipLevelCount = GetMipLevelCount(ktxHeader.LevelCount, ktxHeader.Width, ktxHeader.Height, depth, "KTX2");
        var levelIndexes = new KtxLevelIndex[mipLevelCount];
        Byte24Buffer levelStorage = default;
        Span<byte> levelBuffer = levelStorage;
        for (var i = 0; i < levelIndexes.Length; i++)
        {
            ReadExactly(stream, levelBuffer);
            levelIndexes[i] = new KtxLevelIndex(
                BinaryPrimitives.ReadUInt64LittleEndian(levelBuffer),
                BinaryPrimitives.ReadUInt64LittleEndian(levelBuffer.Slice(8, 8)),
                BinaryPrimitives.ReadUInt64LittleEndian(levelBuffer.Slice(16, 8)));
        }

        if (ktxHeader.SupercompressionScheme == KtxSupercompressionScheme.BasisLz)
        {
            return ReadV2BasisLzEtc1s(
                stream,
                ktxHeader,
                levelIndexes,
                checked((ulong)(HeaderV2ByteCount + (LevelIndexEntryByteCount * mipLevelCount))));
        }

        var format = GetTextureFormat(ktxHeader.VkFormat, isSrgb: IsSrgb(ktxHeader.VkFormat));
        var coder = depth == 1 ? TextureCoderManager.Global.GetCoder(format) : null;
        var coder3D = depth == 1 ? null : TextureCoderManager.Global.GetCoder3D(format);
        var arrayLayerCount = GetArrayLayerCount(ktxHeader.LayerCount, "KTX2");
        var faceCount = GetFaceCount(ktxHeader.FaceCount, "KTX2");
        ValidateTextureShape(depth, arrayLayerCount, faceCount, "KTX2");
        var subresources = new TextureSubresource[checked(mipLevelCount * arrayLayerCount * faceCount)];
        var currentOffset = checked((ulong)(HeaderV2ByteCount + (LevelIndexEntryByteCount * mipLevelCount)));
        var subresourceIndex = 0;
        for (var i = 0; i < mipLevelCount; i++)
        {
            var level = levelIndexes[i];
            var width = TextureImage.GetMipDimension(ktxHeader.Width, i);
            var height = TextureImage.GetMipDimension(ktxHeader.Height, i);
            var levelDepth = TextureImage.GetMipDimension(depth, i);
            var expectedFacePayloadByteCount = GetEncodedByteCount(coder, coder3D, width, height, levelDepth);
            var expectedLevelPayloadByteCount = checked(expectedFacePayloadByteCount * arrayLayerCount * faceCount);
            if (level.UncompressedByteLength != (ulong)expectedLevelPayloadByteCount)
            {
                throw new InvalidDataException(
                    $"KTX2 level {i} payload decompresses to {level.UncompressedByteLength} bytes, but '{format.Name}' expects {expectedLevelPayloadByteCount} bytes for {width}x{height}x{levelDepth} with {arrayLayerCount} array layer(s) and {faceCount} face(s).");
            }

            if (level.ByteLength > int.MaxValue)
            {
                throw new InvalidDataException("KTX2 level payload is outside the supported range.");
            }

            SkipToOffset(stream, level.ByteOffset, currentOffset);
            currentOffset = level.ByteOffset;

            var levelPayload = new byte[checked((int)level.ByteLength)];
            ReadExactly(stream, levelPayload);
            currentOffset = checked(currentOffset + level.ByteLength);
            var payload = Decompress(levelPayload, expectedLevelPayloadByteCount, ktxHeader.SupercompressionScheme);
            for (var arrayLayer = 0; arrayLayer < arrayLayerCount; arrayLayer++)
            {
                for (var face = 0; face < faceCount; face++)
                {
                    var offset = checked(((arrayLayer * faceCount) + face) * expectedFacePayloadByteCount);
                    var facePayload = payload.AsSpan(offset, expectedFacePayloadByteCount).ToArray();
                    subresources[subresourceIndex++] = new TextureSubresource(i, arrayLayer, face, width, height, levelDepth, facePayload);
                }
            }
        }

        return new KtxTexture(
            format,
            subresources,
            arrayLayerCount,
            faceCount,
            glType: null,
            glFormat: null,
            glInternalFormat: null,
            glBaseInternalFormat: null,
            ktxHeader.VkFormat);
    }

    private static KtxTexture ReadV2BasisLzEtc1s(
        Stream stream,
        KtxHeaderV2 ktxHeader,
        IReadOnlyList<KtxLevelIndex> levelIndexes,
        ulong currentOffset)
    {
        if (ktxHeader.VkFormat != KtxVkFormat.Undefined)
        {
            throw new InvalidDataException("KTX2 BasisLZ files must use VK_FORMAT_UNDEFINED.");
        }

        var depth = GetTextureDepth(ktxHeader.PixelDepth);
        if (depth != 1)
        {
            throw new NotSupportedException("KTX2 BasisLZ 3D textures are not supported yet.");
        }

        var mipLevelCount = GetMipLevelCount(ktxHeader.LevelCount, ktxHeader.Width, ktxHeader.Height, depth, "KTX2");
        var arrayLayerCount = GetArrayLayerCount(ktxHeader.LayerCount, "KTX2");
        var faceCount = GetFaceCount(ktxHeader.FaceCount, "KTX2");
        ValidateTextureShape(depth, arrayLayerCount, faceCount, "KTX2");

        var imageCount = checked(mipLevelCount * arrayLayerCount * faceCount);
        if (ktxHeader.SgdByteOffset == 0 || ktxHeader.SgdByteLength == 0)
        {
            throw new InvalidDataException("KTX2 BasisLZ supercompression global data is missing.");
        }

        if (ktxHeader.SgdByteLength > int.MaxValue)
        {
            throw new InvalidDataException("KTX2 BasisLZ supercompression global data is outside the supported range.");
        }

        foreach (var level in levelIndexes)
        {
            if (level.UncompressedByteLength != 0)
            {
                throw new InvalidDataException("KTX2 BasisLZ levels must have an uncompressed byte length of 0.");
            }
        }

        SkipToOffset(stream, ktxHeader.SgdByteOffset, currentOffset);
        currentOffset = ktxHeader.SgdByteOffset;

        var globalDataBytes = new byte[checked((int)ktxHeader.SgdByteLength)];
        ReadExactly(stream, globalDataBytes);
        currentOffset = checked(currentOffset + ktxHeader.SgdByteLength);
        var globalData = ReadBasisLzGlobalData(globalDataBytes, imageCount);

        var subresources = new List<TextureSubresource>(imageCount);
        var imageDescIndex = 0;
        for (var mipLevel = 0; mipLevel < mipLevelCount; mipLevel++)
        {
            var level = levelIndexes[mipLevel];
            if (level.ByteLength > int.MaxValue)
            {
                throw new InvalidDataException("KTX2 BasisLZ level payload is outside the supported range.");
            }

            SkipToOffset(stream, level.ByteOffset, currentOffset);
            currentOffset = level.ByteOffset;

            var levelPayload = new byte[checked((int)level.ByteLength)];
            ReadExactly(stream, levelPayload);
            currentOffset = checked(currentOffset + level.ByteLength);

            var width = TextureImage.GetMipDimension(ktxHeader.Width, mipLevel);
            var height = TextureImage.GetMipDimension(ktxHeader.Height, mipLevel);
            for (var arrayLayer = 0; arrayLayer < arrayLayerCount; arrayLayer++)
            {
                for (var face = 0; face < faceCount; face++)
                {
                    var imageDesc = globalData.ImageDescs[imageDescIndex++];
                    var bitmap = DecodeBasisLzEtc1sImage(globalData, levelPayload, imageDesc, width, height);
                    subresources.Add(new TextureSubresource(
                        mipLevel,
                        arrayLayer,
                        face,
                        width,
                        height,
                        CopyRgba8Pixels(bitmap)));
                }
            }
        }

        return new KtxTexture(
            TextureFormats.Rgba8UNorm,
            subresources,
            arrayLayerCount,
            faceCount,
            glType: null,
            glFormat: null,
            glInternalFormat: null,
            glBaseInternalFormat: null,
            vkFormat: ktxHeader.VkFormat);
    }

    private static KtxBasisLzGlobalData ReadBasisLzGlobalData(ReadOnlySpan<byte> data, int imageCount)
    {
        var imageDescBytes = checked(imageCount * BasisLzImageDescByteCount);
        if (data.Length < BasisLzHeaderByteCount + imageDescBytes)
        {
            throw new InvalidDataException("KTX2 BasisLZ supercompression global data is truncated.");
        }

        var endpointCount = BinaryPrimitives.ReadUInt16LittleEndian(data);
        var selectorCount = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
        var endpointByteLength = ReadBasisLzDataLength(data.Slice(4, 4), "endpoint");
        var selectorByteLength = ReadBasisLzDataLength(data.Slice(8, 4), "selector");
        var tableByteLength = ReadBasisLzDataLength(data.Slice(12, 4), "Huffman table");
        var extendedByteLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
        if (extendedByteLength != 0)
        {
            throw new NotSupportedException("KTX2 BasisLZ extended data is not supported for ETC1S textures.");
        }

        var expectedByteLength = checked(BasisLzHeaderByteCount + imageDescBytes + endpointByteLength + selectorByteLength + tableByteLength);
        if (data.Length != expectedByteLength)
        {
            throw new InvalidDataException($"KTX2 BasisLZ supercompression global data is {data.Length} bytes, but {expectedByteLength} bytes were expected.");
        }

        var imageDescs = new KtxBasisLzImageDesc[imageCount];
        var offset = BasisLzHeaderByteCount;
        for (var i = 0; i < imageDescs.Length; i++)
        {
            var imageDesc = data.Slice(offset, BasisLzImageDescByteCount);
            var imageFlags = BinaryPrimitives.ReadUInt32LittleEndian(imageDesc);
            if ((imageFlags & ~BasisLzImageFlagIsPFrame) != 0)
            {
                throw new InvalidDataException("KTX2 BasisLZ image descriptor contains unsupported flags.");
            }

            imageDescs[i] = new KtxBasisLzImageDesc(
                imageFlags,
                BinaryPrimitives.ReadUInt32LittleEndian(imageDesc.Slice(4, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(imageDesc.Slice(8, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(imageDesc.Slice(12, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(imageDesc.Slice(16, 4)));
            offset = checked(offset + BasisLzImageDescByteCount);
        }

        var endpointData = data.Slice(offset, endpointByteLength).ToArray();
        offset = checked(offset + endpointByteLength);
        var selectorData = data.Slice(offset, selectorByteLength).ToArray();
        offset = checked(offset + selectorByteLength);
        var tableData = data.Slice(offset, tableByteLength).ToArray();

        return new KtxBasisLzGlobalData(endpointCount, selectorCount, endpointData, selectorData, tableData, imageDescs);
    }

    private static int ReadBasisLzDataLength(ReadOnlySpan<byte> source, string sectionName)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (value == 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"KTX2 BasisLZ {sectionName} data length is outside the supported range.");
        }

        return (int)value;
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeBasisLzEtc1sImage(
        KtxBasisLzGlobalData globalData,
        ReadOnlySpan<byte> levelPayload,
        KtxBasisLzImageDesc imageDesc,
        int width,
        int height)
    {
        if (imageDesc.IsPFrame)
        {
            throw new NotSupportedException("KTX2 BasisLZ P-frame images are not supported yet.");
        }

        var rgbSlice = SliceBasisLzLevelPayload(levelPayload, imageDesc.RgbSliceByteOffset, imageDesc.RgbSliceByteLength, "RGB");
        var alphaSlice = imageDesc.AlphaSliceByteLength == 0
            ? default
            : SliceBasisLzLevelPayload(levelPayload, imageDesc.AlphaSliceByteOffset, imageDesc.AlphaSliceByteLength, "alpha");

        var bitmap = new ArrayBitmap<Rgba8UNorm>(width, height);
        var rawPayload = new BasisEtc1sRawPayload(
            globalData.EndpointCount,
            globalData.EndpointData,
            globalData.SelectorCount,
            globalData.SelectorData,
            globalData.TableData,
            rgbSlice,
            alphaSlice);
        BasisEtc1sTextureCoder.Decode(rawPayload, bitmap.AsView());
        return bitmap;
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

    private static byte[] CopyRgba8Pixels(ArrayBitmap<Rgba8UNorm> bitmap)
    {
        var result = new byte[checked(bitmap.PixelSpan.Length * 4)];
        var offset = 0;
        foreach (var pixel in bitmap.PixelSpan)
        {
            result[offset++] = pixel.Red;
            result[offset++] = pixel.Green;
            result[offset++] = pixel.Blue;
            result[offset++] = pixel.Alpha;
        }

        return result;
    }

    private static void WriteV2(KtxTexture texture, Stream stream, KtxEncodingOptions? options)
    {
        var vkFormat = GetVkFormat(texture.Texture.Format);
        var supercompressionScheme = GetEncodingSupercompressionScheme(options);
        var levelPayloads = new byte[texture.Texture.MipLevelCount][];
        for (var i = 0; i < texture.Texture.MipLevelCount; i++)
        {
            levelPayloads[i] = Compress(ConcatenateLevelPayload(texture, i), supercompressionScheme, options);
        }

        var dfdOffset = checked(HeaderV2ByteCount + (LevelIndexEntryByteCount * texture.Texture.MipLevelCount));
        var levelOffset = checked(dfdOffset + BasicDfdByteCount);

        Byte80Buffer headerStorage = default;
        Span<byte> header = headerStorage;
        header.Clear();
        IdentifierV2.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), (uint)vkFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), GetKtx2TypeSize(texture.Texture.Format));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(20, 4), checked((uint)texture.Texture.Width));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(24, 4), checked((uint)texture.Texture.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(28, 4), texture.Texture.Depth == 1 ? 0 : checked((uint)texture.Texture.Depth));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(32, 4), texture.Texture.ArrayLayerCount == 1 ? 0 : checked((uint)texture.Texture.ArrayLayerCount));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(36, 4), checked((uint)texture.Texture.FaceCount));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(40, 4), checked((uint)texture.Texture.MipLevelCount));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(44, 4), (uint)supercompressionScheme);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(48, 4), checked((uint)dfdOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(52, 4), BasicDfdByteCount);
        stream.Write(header);

        Byte24Buffer levelStorage = default;
        Span<byte> level = levelStorage;
        var currentOffset = checked((ulong)levelOffset);
        for (var i = 0; i < texture.Texture.MipLevelCount; i++)
        {
            var payload = levelPayloads[i];
            level.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(level, currentOffset);
            BinaryPrimitives.WriteUInt64LittleEndian(level.Slice(8, 8), checked((ulong)payload.Length));
            BinaryPrimitives.WriteUInt64LittleEndian(level.Slice(16, 8), checked((ulong)GetLevelPayloadByteCount(texture, i)));
            stream.Write(level);
            currentOffset = checked(currentOffset + (ulong)payload.Length);
        }

        WriteBasicDfd(stream, texture.Texture.Format);
        foreach (var payload in levelPayloads)
        {
            stream.Write(payload);
        }
    }

    private static void WriteBasicDfd(Stream stream, TextureFormat format)
    {
        Byte24Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        buffer.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, BasicDfdByteCount);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(8, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(10, 2), BasicDfdByteCount - 4);
        buffer[12] = format.Kind == TextureFormatKind.BlockCompressed ? (byte)0xff : (byte)1;
        buffer[13] = 1;
        buffer[14] = format.ValueKind == TextureValueKind.Srgb ? (byte)2 : (byte)1;
        buffer[16] = checked((byte)(format.BlockWidth - 1));
        buffer[17] = checked((byte)(format.BlockHeight - 1));
        buffer[18] = checked((byte)(format.BlockDepth - 1));
        stream.Write(buffer);
    }

    private static byte[] Compress(byte[] payload, KtxSupercompressionScheme scheme, KtxEncodingOptions? options) =>
        scheme switch
        {
            KtxSupercompressionScheme.None => payload,
            KtxSupercompressionScheme.Zstandard => CompressZstandard(payload, options?.ZstandardCompressionLevel ?? 3),
            KtxSupercompressionScheme.Zlib => CompressZlib(payload, options?.ZlibCompressionLevel ?? CompressionLevel.Optimal),
            KtxSupercompressionScheme.BasisLz => throw new NotSupportedException("KTX2 BasisLZ supercompression is not supported."),
            _ => throw new NotSupportedException($"KTX2 supercompression scheme '{scheme}' is not supported.")
        };

    private static byte[] Decompress(byte[] payload, int expectedByteCount, KtxSupercompressionScheme scheme) =>
        scheme switch
        {
            KtxSupercompressionScheme.None => ValidateUncompressedPayload(payload, expectedByteCount),
            KtxSupercompressionScheme.Zstandard => DecompressZstandard(payload, expectedByteCount),
            KtxSupercompressionScheme.Zlib => DecompressZlib(payload, expectedByteCount),
            KtxSupercompressionScheme.BasisLz => throw new NotSupportedException("KTX2 BasisLZ supercompression is not supported."),
            _ => throw new NotSupportedException($"KTX2 supercompression scheme '{scheme}' is not supported.")
        };

    private static byte[] CompressZstandard(byte[] payload, int level)
    {
        using var compressor = new Compressor(level);
        return compressor.Wrap(payload).ToArray();
    }

    private static byte[] DecompressZstandard(byte[] payload, int expectedByteCount)
    {
        var result = new byte[expectedByteCount];
        using var decompressor = new Decompressor();
        var byteCount = decompressor.Unwrap(payload, result);
        if (byteCount != expectedByteCount)
        {
            throw new InvalidDataException($"KTX2 Zstandard payload decompressed to {byteCount} bytes, but {expectedByteCount} bytes were expected.");
        }

        return result;
    }

    private static byte[] CompressZlib(byte[] payload, CompressionLevel level)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, level, leaveOpen: true))
        {
            zlib.Write(payload);
        }

        return output.ToArray();
    }

    private static byte[] DecompressZlib(byte[] payload, int expectedByteCount)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expectedByteCount);
        zlib.CopyTo(output);

        var result = output.ToArray();
        if (result.Length != expectedByteCount)
        {
            throw new InvalidDataException($"KTX2 Zlib payload decompressed to {result.Length} bytes, but {expectedByteCount} bytes were expected.");
        }

        return result;
    }

    private static byte[] ValidateUncompressedPayload(byte[] payload, int expectedByteCount)
    {
        if (payload.Length != expectedByteCount)
        {
            throw new InvalidDataException($"KTX2 level payload is {payload.Length} bytes, but {expectedByteCount} bytes were expected.");
        }

        return payload;
    }

    private static void ValidateHeader(KtxHeader header)
    {
        if (!header.LittleEndian)
        {
            throw new NotSupportedException("Big-endian KTX files are not supported.");
        }

        if (header.NumberOfFaces is not (1 or 6))
        {
            throw new NotSupportedException("KTX partial cube maps are not supported.");
        }

        if (header.GlType == 0 != (header.GlFormat == 0))
        {
            throw new InvalidDataException("KTX glType and glFormat must either both be zero or both be non-zero.");
        }

        if (header.GlType == 0 && header.GlTypeSize != 1)
        {
            throw new InvalidDataException("KTX compressed textures must use a glTypeSize of 1.");
        }
    }

    private static void ValidateHeader(KtxHeaderV2 header)
    {
        if (header.VkFormat == KtxVkFormat.Undefined && header.SupercompressionScheme != KtxSupercompressionScheme.BasisLz)
        {
            throw new NotSupportedException("KTX2 Basis Universal and VK_FORMAT_UNDEFINED files are not supported.");
        }

        if (header.VkFormat != KtxVkFormat.Undefined && header.SupercompressionScheme == KtxSupercompressionScheme.BasisLz)
        {
            throw new InvalidDataException("KTX2 BasisLZ files must use VK_FORMAT_UNDEFINED.");
        }

        if (header.Height == 0)
        {
            throw new NotSupportedException("KTX2 1D textures are not supported.");
        }

        if (header.FaceCount is not (1 or 6))
        {
            throw new NotSupportedException("KTX2 partial cube maps are not supported.");
        }

        if (header.SupercompressionScheme is not (
            KtxSupercompressionScheme.None
            or KtxSupercompressionScheme.BasisLz
            or KtxSupercompressionScheme.Zstandard
            or KtxSupercompressionScheme.Zlib))
        {
            throw new NotSupportedException($"KTX2 supercompression scheme '{header.SupercompressionScheme}' is not supported.");
        }

        if (header.DfdByteOffset == 0 || header.DfdByteLength == 0)
        {
            throw new InvalidDataException("KTX2 data format descriptor is missing.");
        }

        if (header.KvdByteLength != 0 && header.KvdByteOffset == 0)
        {
            throw new InvalidDataException("KTX2 key/value data offset is missing.");
        }

        if (header.SupercompressionScheme == KtxSupercompressionScheme.BasisLz)
        {
            if (header.SgdByteOffset == 0 || header.SgdByteLength == 0)
            {
                throw new InvalidDataException("KTX2 BasisLZ supercompression global data is missing.");
            }
        }
        else if (header.SgdByteLength != 0)
        {
            throw new NotSupportedException("KTX2 supercompression global data is not supported.");
        }
    }

    private static int GetMipLevelCount(uint mipLevelCount, int width, int height, int depth, string containerName)
    {
        if (mipLevelCount > int.MaxValue)
        {
            throw new InvalidDataException($"{containerName} mip-map count is outside the supported range.");
        }

        var count = mipLevelCount == 0 ? 1 : (int)mipLevelCount;
        if (count > TextureImage.GetFullMipLevelCount(width, height, depth))
        {
            throw new InvalidDataException($"{containerName} mip-map count exceeds the full mip chain for the base dimensions.");
        }

        return count;
    }

    private static int GetTextureDepth(uint pixelDepth) =>
        pixelDepth == 0 ? 1 : pixelDepth > int.MaxValue
            ? throw new InvalidDataException("KTX pixel depth is outside the supported range.")
            : (int)pixelDepth;

    private static void ValidateTextureShape(int depth, int arrayLayerCount, int faceCount, string containerName)
    {
        if (depth > 1 && (arrayLayerCount != 1 || faceCount != 1))
        {
            throw new NotSupportedException($"{containerName} 3D texture arrays and cube maps are not supported.");
        }
    }

    private static TextureSubresource[] ReadSubresources(
        Stream stream,
        KtxHeader header,
        TextureFormat format,
        ITextureCoder? coder,
        ITextureCoder3D? coder3D,
        int baseDepth,
        int mipLevelCount,
        int arrayLayerCount,
        int faceCount)
    {
        var subresources = new TextureSubresource[checked(mipLevelCount * arrayLayerCount * faceCount)];
        var subresourceIndex = 0;
        for (var i = 0; i < mipLevelCount; i++)
        {
            var width = TextureImage.GetMipDimension(header.Width, i);
            var height = TextureImage.GetMipDimension(header.Height, i);
            var depth = TextureImage.GetMipDimension(baseDepth, i);
            var expectedPayloadByteCount = GetEncodedByteCount(coder, coder3D, width, height, depth);
            var expectedImageByteCount = GetImageByteCount(format, width, height, depth, expectedPayloadByteCount);
            var imageByteCount = ReadUInt32(stream, header.LittleEndian);
            if (imageByteCount != (uint)expectedImageByteCount)
            {
                throw new InvalidDataException(
                    $"KTX mip level {i} image payload is {imageByteCount} bytes, but '{format.Name}' expects {expectedImageByteCount} bytes for one {width}x{height}x{depth} face.");
            }

            for (var arrayLayer = 0; arrayLayer < arrayLayerCount; arrayLayer++)
            {
                for (var face = 0; face < faceCount; face++)
                {
                    var image = new byte[checked((int)imageByteCount)];
                    ReadExactly(stream, image);
                    SkipExactly(stream, GetPaddingByteCount(image.Length));

                    var payload = RequiresRowPadding(format, width)
                        ? RemoveRowPadding(image, format.GetRowByteCount(width), checked(height * depth))
                        : image;
                    subresources[subresourceIndex++] = new TextureSubresource(i, arrayLayer, face, width, height, depth, payload);
                }
            }
        }

        return subresources;
    }

    private static TextureSubresource[] EncodeMipSubresources<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, ITextureCoder coder)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException("KTX texture must contain at least one mip level.", nameof(mipLevels));
        }

        var baseLevel = mipLevels[0] ?? throw new ArgumentException("KTX mip level cannot be null.", nameof(mipLevels));
        var fullMipLevelCount = TextureImage.GetFullMipLevelCount(baseLevel.Width, baseLevel.Height);
        if (mipLevels.Count > fullMipLevelCount)
        {
            throw new ArgumentException("KTX mip level count exceeds the full mip chain for the base dimensions.", nameof(mipLevels));
        }

        var subresources = new TextureSubresource[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var bitmap = mipLevels[i] ?? throw new ArgumentException("KTX mip level cannot be null.", nameof(mipLevels));
            var expectedWidth = TextureImage.GetMipDimension(baseLevel.Width, i);
            var expectedHeight = TextureImage.GetMipDimension(baseLevel.Height, i);
            if (bitmap.Width != expectedWidth || bitmap.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"KTX mip level {i} is {bitmap.Width}x{bitmap.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(mipLevels));
            }

            var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
            coder.Encode(bitmap.AsView(), payload);
            subresources[i] = new TextureSubresource(i, arrayLayer: 0, faceIndex: 0, bitmap.Width, bitmap.Height, payload);
        }

        return subresources;
    }

    private static void ValidateTexturePayloads(KtxTexture texture, ITextureCoder? coder, ITextureCoder3D? coder3D)
    {
        ValidateTextureShape(texture.Texture.Depth, texture.Texture.ArrayLayerCount, texture.Texture.FaceCount, "KTX");

        var fullMipLevelCount = TextureImage.GetFullMipLevelCount(texture.Texture.Width, texture.Texture.Height, texture.Texture.Depth);
        if (texture.Texture.MipLevelCount > fullMipLevelCount)
        {
            throw new ArgumentException("KTX mip level count exceeds the full mip chain for the base dimensions.", nameof(texture));
        }

        foreach (var subresource in texture.Texture.Subresources)
        {
            var expectedByteCount = GetEncodedByteCount(coder, coder3D, subresource.Width, subresource.Height, subresource.Depth);
            if (subresource.Payload.Length != expectedByteCount)
            {
                throw new ArgumentException(
                    $"KTX subresource mip level {subresource.MipLevel}, array layer {subresource.ArrayLayer}, face {subresource.FaceIndex} payload length is {subresource.Payload.Length} bytes, but '{texture.Texture.Format.Name}' expects {expectedByteCount} bytes for {subresource.Width}x{subresource.Height}x{subresource.Depth}.",
                    nameof(texture));
            }
        }
    }

    private static int GetEncodedByteCount(ITextureCoder? coder, ITextureCoder3D? coder3D, int width, int height, int depth) =>
        depth == 1 && coder is not null
            ? coder.GetEncodedByteCount(width, height)
            : coder3D!.GetEncodedByteCount(width, height, depth);

    private static int GetFaceCount(uint faceCount, string containerName)
    {
        if (faceCount is 1 or 6)
        {
            return (int)faceCount;
        }

        throw new NotSupportedException($"{containerName} partial cube maps are not supported.");
    }

    private static int GetArrayLayerCount(uint arrayLayerCount, string containerName)
    {
        if (arrayLayerCount > int.MaxValue)
        {
            throw new InvalidDataException($"{containerName} array layer count is outside the supported range.");
        }

        return arrayLayerCount == 0 ? 1 : (int)arrayLayerCount;
    }

    private static int GetLevelPayloadByteCount(KtxTexture texture, int mipLevel)
    {
        var byteCount = 0;
        for (var arrayLayer = 0; arrayLayer < texture.Texture.ArrayLayerCount; arrayLayer++)
        {
            for (var face = 0; face < texture.Texture.FaceCount; face++)
            {
                byteCount = checked(byteCount + texture.Texture.GetSubresource(mipLevel, arrayLayer, face).Payload.Length);
            }
        }

        return byteCount;
    }

    private static byte[] ConcatenateLevelPayload(KtxTexture texture, int mipLevel)
    {
        var result = new byte[GetLevelPayloadByteCount(texture, mipLevel)];
        var offset = 0;
        for (var arrayLayer = 0; arrayLayer < texture.Texture.ArrayLayerCount; arrayLayer++)
        {
            for (var face = 0; face < texture.Texture.FaceCount; face++)
            {
                var payload = texture.Texture.GetSubresource(mipLevel, arrayLayer, face).Payload;
                payload.AsSpan().CopyTo(result.AsSpan(offset));
                offset = checked(offset + payload.Length);
            }
        }

        return result;
    }

    private static TextureFormat GetTextureFormat(KtxHeader header)
    {
        var mappings = SFormatMappings.Value;
        if (header.GlType == 0)
        {
            if (mappings.CompressedToTexture.TryGetValue((KtxGlFormat)header.GlInternalFormat, out var compressedFormat))
            {
                return compressedFormat;
            }
        }
        else
        {
            var key = new UncompressedFormatKey(
                (KtxGlFormat)header.GlType,
                (KtxGlFormat)header.GlFormat,
                (KtxGlFormat)header.GlInternalFormat);
            if (mappings.UncompressedToTexture.TryGetValue(key, out var uncompressedFormat))
            {
                return uncompressedFormat;
            }
        }

        throw new NotSupportedException($"KTX GL internal format 0x{header.GlInternalFormat:x8} is not supported.");
    }

    private static TextureFormat GetEncodingTextureFormat(KtxEncodingOptions? options)
    {
        if (options?.TextureFormat is { } textureFormat)
        {
            ValidateTextureFormat(textureFormat, nameof(options));
            return textureFormat;
        }

        if (options?.VkFormat is { } vkFormat)
        {
            return GetTextureFormat(vkFormat, options.IsSrgb);
        }

        if (options?.GlInternalFormat is { } glInternalFormat)
        {
            return GetTextureFormat(glInternalFormat, options.IsSrgb);
        }

        return options?.IsSrgb == true ? TextureFormats.Rgba8Srgb : TextureFormats.Rgba8UNorm;
    }

    private static TextureFormat GetTextureFormat(KtxGlFormat glInternalFormat, bool isSrgb)
    {
        var mappings = SFormatMappings.Value;
        if (mappings.GlInternalFormatToTexture.TryGetValue(new GlInternalFormatKey(glInternalFormat, isSrgb), out var mapping))
        {
            return mapping.TextureFormat;
        }

        if (mappings.GlInternalFormatToTexture.TryGetValue(new GlInternalFormatKey(glInternalFormat, IsSrgb: false), out mapping))
        {
            return mapping.TextureFormat;
        }

        throw new NotSupportedException($"KTX GL internal format '{glInternalFormat}' is not supported.");
    }

    private static TextureFormat GetTextureFormat(KtxVkFormat vkFormat, bool isSrgb)
    {
        var mappings = SFormatMappings.Value;
        if (mappings.VkFormatToTexture.TryGetValue(new VkFormatKey(vkFormat, isSrgb), out var mapping))
        {
            return mapping.TextureFormat;
        }

        if (mappings.VkFormatToTexture.TryGetValue(new VkFormatKey(vkFormat, IsSrgb: false), out mapping))
        {
            return mapping.TextureFormat;
        }

        throw new NotSupportedException($"KTX Vulkan format '{vkFormat}' is not supported.");
    }

    private static KtxVkFormat GetVkFormat(TextureFormat textureFormat)
    {
        if (SFormatMappings.Value.TextureToVk.TryGetValue(textureFormat, out var vkFormat))
        {
            return vkFormat;
        }

        throw new NotSupportedException($"Texture format '{textureFormat.Name}' is not a supported KTX2 format.");
    }

    private static uint GetKtx2TypeSize(TextureFormat format)
    {
        if (format.Kind == TextureFormatKind.BlockCompressed)
        {
            return 1;
        }

        var bits = Math.Max(Math.Max(format.RedBits, format.GreenBits), Math.Max(format.BlueBits, format.AlphaBits));
        return bits <= 8 ? 1u : bits <= 16 ? 2u : 4u;
    }

    private static void ValidateV2EncodingSelection(TextureFormat textureFormat, KtxEncodingOptions? options)
    {
        if (options?.VkFormat is { } vkFormat)
        {
            var selectedFormat = GetTextureFormat(vkFormat, options.IsSrgb);
            if (selectedFormat != textureFormat)
            {
                throw new ArgumentException(
                    $"KTX2 encoding options select '{selectedFormat.Name}', but the texture payload uses '{textureFormat.Name}'.",
                    nameof(options));
            }
        }

        _ = GetVkFormat(textureFormat);
    }

    private static KtxSupercompressionScheme GetEncodingSupercompressionScheme(KtxEncodingOptions? options)
    {
        var scheme = options?.SupercompressionScheme ?? KtxSupercompressionScheme.None;
        if (scheme is KtxSupercompressionScheme.BasisLz)
        {
            throw new NotSupportedException("KTX2 BasisLZ supercompression is not supported.");
        }

        if (scheme is not (
            KtxSupercompressionScheme.None
            or KtxSupercompressionScheme.Zstandard
            or KtxSupercompressionScheme.Zlib))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "KTX2 supercompression scheme must be None, Zstandard, or Zlib.");
        }

        return scheme;
    }

    private static bool IsSrgb(KtxVkFormat vkFormat) =>
        vkFormat.ToString().Contains("Srgb", StringComparison.Ordinal);

    private static KtxFormatDescriptor GetDescriptor(TextureFormat textureFormat, KtxEncodingOptions? options)
    {
        var effectiveFormat = options?.TextureFormat is null && options?.GlInternalFormat is { } glInternalFormat
            ? GetTextureFormat(glInternalFormat, options.IsSrgb)
            : textureFormat;

        if (effectiveFormat != textureFormat)
        {
            throw new ArgumentException(
                $"KTX encoding options select '{effectiveFormat.Name}', but the texture payload uses '{textureFormat.Name}'.",
                nameof(options));
        }

        var mappings = SFormatMappings.Value;
        if (mappings.TextureToKtx.TryGetValue(textureFormat, out var descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException($"Texture format '{textureFormat.Name}' is not a supported KTX format.");
    }

    private static void ValidateTextureFormat(TextureFormat format, string parameterName)
    {
        var mappings = SFormatMappings.Value;
        if (!mappings.TextureToKtx.ContainsKey(format) && !mappings.TextureToVk.ContainsKey(format))
        {
            throw new ArgumentException($"Texture format '{format.Name}' is not a supported KTX format.", parameterName);
        }
    }

    private static void ValidateEncodingVersion(KtxVersion version)
    {
        if (version is not (KtxVersion.Version1 or KtxVersion.Version2))
        {
            throw new ArgumentOutOfRangeException(nameof(version), "KTX version must be Version1 or Version2.");
        }
    }

    private static int GetImageByteCount(TextureFormat format, int width, int height, int depth, int payloadByteCount)
    {
        if (format.Kind == TextureFormatKind.BlockCompressed)
        {
            return payloadByteCount;
        }

        var rowByteCount = format.GetRowByteCount(width);
        return checked(Align4(rowByteCount) * height * depth);
    }

    private static bool RequiresRowPadding(TextureFormat format, int width) =>
        format.Kind != TextureFormatKind.BlockCompressed && Align4(format.GetRowByteCount(width)) != format.GetRowByteCount(width);

    private static byte[] RemoveRowPadding(byte[] image, int rowByteCount, int height)
    {
        var paddedRowByteCount = Align4(rowByteCount);
        var payload = new byte[checked(rowByteCount * height)];
        for (var y = 0; y < height; y++)
        {
            image.AsSpan(y * paddedRowByteCount, rowByteCount).CopyTo(payload.AsSpan(y * rowByteCount, rowByteCount));
        }

        return payload;
    }

    private static byte[] AddRowPadding(byte[] payload, int rowByteCount, int height)
    {
        var paddedRowByteCount = Align4(rowByteCount);
        var image = new byte[checked(paddedRowByteCount * height)];
        for (var y = 0; y < height; y++)
        {
            payload.AsSpan(y * rowByteCount, rowByteCount).CopyTo(image.AsSpan(y * paddedRowByteCount, rowByteCount));
        }

        return image;
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static int GetPaddingByteCount(int byteCount) => (4 - (byteCount & 3)) & 3;

    private static uint ReadUInt32(Stream stream, bool littleEndian)
    {
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(stream, buffer);
        return ReadUInt32(buffer, littleEndian);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> source, bool littleEndian) =>
        littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(source) : BinaryPrimitives.ReadUInt32BigEndian(source);

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static int ReadPositiveInt(ReadOnlySpan<byte> source, bool littleEndian, string fieldName)
    {
        var value = ReadUInt32(source, littleEndian);
        if (value == 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"KTX {fieldName} is outside the supported range.");
        }

        return (int)value;
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("KTX stream ended unexpectedly.", exception);
        }
    }

    private static void SkipExactly(Stream stream, int byteCount)
    {
        if (byteCount == 0)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[Math.Min(byteCount, 256)];
        var remaining = byteCount;
        while (remaining > 0)
        {
            var read = Math.Min(remaining, buffer.Length);
            ReadExactly(stream, buffer[..read]);
            remaining -= read;
        }
    }

    private static void SkipToOffset(Stream stream, ulong offset, ulong currentOffset)
    {
        if (offset < currentOffset)
        {
            throw new InvalidDataException("KTX2 level data offset points before the current stream position.");
        }

        if (offset > int.MaxValue)
        {
            throw new InvalidDataException("KTX2 level data offset is outside the supported range.");
        }

        SkipExactly(stream, checked((int)(offset - currentOffset)));
    }

    private static void WritePadding(Stream stream, int byteCount)
    {
        Span<byte> padding = stackalloc byte[3];
        stream.Write(padding[..byteCount]);
    }

    private static Mappings CreateFormatMappings()
    {
        var textureToKtx = new Dictionary<TextureFormat, KtxFormatDescriptor>();
        var uncompressedToTexture = new Dictionary<UncompressedFormatKey, TextureFormat>();
        var compressedToTexture = new Dictionary<KtxGlFormat, TextureFormat>();
        var glInternalFormatToTexture = new Dictionary<GlInternalFormatKey, GlInternalFormatMapping>();
        var textureToVk = new Dictionary<TextureFormat, KtxVkFormat>();
        var vkFormatToTexture = new Dictionary<VkFormatKey, VkFormatMapping>();

        void Add(TextureFormat format, KtxFormatDescriptor descriptor, bool isSrgb = false)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            textureToKtx.TryAdd(format, descriptor);
            glInternalFormatToTexture.TryAdd(new GlInternalFormatKey((KtxGlFormat)descriptor.GlInternalFormat, isSrgb), new GlInternalFormatMapping(format, descriptor));

            if (descriptor.GlType == 0)
            {
                compressedToTexture.TryAdd((KtxGlFormat)descriptor.GlInternalFormat, format);
            }
            else
            {
                uncompressedToTexture.TryAdd(
                    new UncompressedFormatKey((KtxGlFormat)descriptor.GlType, (KtxGlFormat)descriptor.GlFormat, (KtxGlFormat)descriptor.GlInternalFormat),
                    format);
            }
        }

        void AddVk(TextureFormat format, KtxVkFormat vkFormat, bool isSrgb = false)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            textureToVk.TryAdd(format, vkFormat);
            vkFormatToTexture.TryAdd(new VkFormatKey(vkFormat, isSrgb), new VkFormatMapping(format, vkFormat));
        }

        KtxFormatDescriptor Uncompressed(KtxGlFormat type, uint typeSize, KtxGlFormat format, KtxGlFormat internalFormat, KtxGlFormat baseInternalFormat) =>
            new((uint)type, typeSize, (uint)format, (uint)internalFormat, (uint)baseInternalFormat);

        KtxFormatDescriptor Compressed(KtxGlFormat internalFormat, KtxGlFormat baseInternalFormat) =>
            new(GlType: 0, GlTypeSize: 1, GlFormat: 0, (uint)internalFormat, (uint)baseInternalFormat);

        Add(TextureFormats.R8, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Red, KtxGlFormat.R8, KtxGlFormat.Red));
        Add(TextureFormats.R8SNorm, Uncompressed(KtxGlFormat.Byte, 1, KtxGlFormat.Red, KtxGlFormat.R8SNorm, KtxGlFormat.Red));
        Add(TextureFormats.R8UInt, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.RedInteger, KtxGlFormat.R8UI, KtxGlFormat.Red));
        Add(TextureFormats.R8SInt, Uncompressed(KtxGlFormat.Byte, 1, KtxGlFormat.RedInteger, KtxGlFormat.R8I, KtxGlFormat.Red));
        Add(TextureFormats.R16UNorm, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.Red, KtxGlFormat.R16, KtxGlFormat.Red));
        Add(TextureFormats.R16SNorm, Uncompressed(KtxGlFormat.Short, 2, KtxGlFormat.Red, KtxGlFormat.R16SNorm, KtxGlFormat.Red));
        Add(TextureFormats.R16Float, Uncompressed(KtxGlFormat.HalfFloat, 2, KtxGlFormat.Red, KtxGlFormat.R16F, KtxGlFormat.Red));
        Add(TextureFormats.R16UInt, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.RedInteger, KtxGlFormat.R16UI, KtxGlFormat.Red));
        Add(TextureFormats.R16SInt, Uncompressed(KtxGlFormat.Short, 2, KtxGlFormat.RedInteger, KtxGlFormat.R16I, KtxGlFormat.Red));
        Add(TextureFormats.R32Float, Uncompressed(KtxGlFormat.Float, 4, KtxGlFormat.Red, KtxGlFormat.R32F, KtxGlFormat.Red));
        Add(TextureFormats.R32UInt, Uncompressed(KtxGlFormat.UnsignedInt, 4, KtxGlFormat.RedInteger, KtxGlFormat.R32UI, KtxGlFormat.Red));
        Add(TextureFormats.R32SInt, Uncompressed(KtxGlFormat.Int, 4, KtxGlFormat.RedInteger, KtxGlFormat.R32I, KtxGlFormat.Red));

        Add(TextureFormats.Rg8, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Rg, KtxGlFormat.Rg8, KtxGlFormat.Rg));
        Add(TextureFormats.Rg8SNorm, Uncompressed(KtxGlFormat.Byte, 1, KtxGlFormat.Rg, KtxGlFormat.Rg8SNorm, KtxGlFormat.Rg));
        Add(TextureFormats.Rg8UInt, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.RgInteger, KtxGlFormat.Rg8UI, KtxGlFormat.Rg));
        Add(TextureFormats.Rg8SInt, Uncompressed(KtxGlFormat.Byte, 1, KtxGlFormat.RgInteger, KtxGlFormat.Rg8I, KtxGlFormat.Rg));
        Add(TextureFormats.Rg16UNorm, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.Rg, KtxGlFormat.Rg16, KtxGlFormat.Rg));
        Add(TextureFormats.Rg16SNorm, Uncompressed(KtxGlFormat.Short, 2, KtxGlFormat.Rg, KtxGlFormat.Rg16SNorm, KtxGlFormat.Rg));
        Add(TextureFormats.Rg16Float, Uncompressed(KtxGlFormat.HalfFloat, 2, KtxGlFormat.Rg, KtxGlFormat.Rg16F, KtxGlFormat.Rg));
        Add(TextureFormats.Rg16UInt, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.RgInteger, KtxGlFormat.Rg16UI, KtxGlFormat.Rg));
        Add(TextureFormats.Rg16SInt, Uncompressed(KtxGlFormat.Short, 2, KtxGlFormat.RgInteger, KtxGlFormat.Rg16I, KtxGlFormat.Rg));
        Add(TextureFormats.Rg32Float, Uncompressed(KtxGlFormat.Float, 4, KtxGlFormat.Rg, KtxGlFormat.Rg32F, KtxGlFormat.Rg));
        Add(TextureFormats.Rg32UInt, Uncompressed(KtxGlFormat.UnsignedInt, 4, KtxGlFormat.RgInteger, KtxGlFormat.Rg32UI, KtxGlFormat.Rg));
        Add(TextureFormats.Rg32SInt, Uncompressed(KtxGlFormat.Int, 4, KtxGlFormat.RgInteger, KtxGlFormat.Rg32I, KtxGlFormat.Rg));

        Add(TextureFormats.Rgb8, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Rgb, KtxGlFormat.Rgb8, KtxGlFormat.Rgb));
        Add(TextureFormats.Rgb8Srgb, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Rgb, KtxGlFormat.Srgb8, KtxGlFormat.Rgb), isSrgb: true);
        Add(TextureFormats.Rgb16UNorm, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.Rgb, KtxGlFormat.Rgb16, KtxGlFormat.Rgb));
        Add(TextureFormats.Rgb16Float, Uncompressed(KtxGlFormat.HalfFloat, 2, KtxGlFormat.Rgb, KtxGlFormat.Rgb16F, KtxGlFormat.Rgb));
        Add(TextureFormats.Rgb32Float, Uncompressed(KtxGlFormat.Float, 4, KtxGlFormat.Rgb, KtxGlFormat.Rgb32F, KtxGlFormat.Rgb));

        Add(TextureFormats.Rgba8UNorm, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Rgba, KtxGlFormat.Rgba8, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba8Srgb, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Rgba, KtxGlFormat.Srgb8Alpha8, KtxGlFormat.Rgba), isSrgb: true);
        Add(TextureFormats.Rgba8SNorm, Uncompressed(KtxGlFormat.Byte, 1, KtxGlFormat.Rgba, KtxGlFormat.Rgba8SNorm, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba8UInt, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.RgbaInteger, KtxGlFormat.Rgba8UI, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba8SInt, Uncompressed(KtxGlFormat.Byte, 1, KtxGlFormat.RgbaInteger, KtxGlFormat.Rgba8I, KtxGlFormat.Rgba));
        Add(TextureFormats.Bgra8, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Bgra, KtxGlFormat.Rgba8, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba16UNorm, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.Rgba, KtxGlFormat.Rgba16, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba16SNorm, Uncompressed(KtxGlFormat.Short, 2, KtxGlFormat.Rgba, KtxGlFormat.Rgba16SNorm, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba16Float, Uncompressed(KtxGlFormat.HalfFloat, 2, KtxGlFormat.Rgba, KtxGlFormat.Rgba16F, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba16UInt, Uncompressed(KtxGlFormat.UnsignedShort, 2, KtxGlFormat.RgbaInteger, KtxGlFormat.Rgba16UI, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba16SInt, Uncompressed(KtxGlFormat.Short, 2, KtxGlFormat.RgbaInteger, KtxGlFormat.Rgba16I, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba32Float, Uncompressed(KtxGlFormat.Float, 4, KtxGlFormat.Rgba, KtxGlFormat.Rgba32F, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba32UInt, Uncompressed(KtxGlFormat.UnsignedInt, 4, KtxGlFormat.RgbaInteger, KtxGlFormat.Rgba32UI, KtxGlFormat.Rgba));
        Add(TextureFormats.Rgba32SInt, Uncompressed(KtxGlFormat.Int, 4, KtxGlFormat.RgbaInteger, KtxGlFormat.Rgba32I, KtxGlFormat.Rgba));

        Add(TextureFormats.Alpha8UNorm, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Alpha, KtxGlFormat.Alpha, KtxGlFormat.Alpha));
        Add(TextureFormats.Luminance8UNorm, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.Luminance, KtxGlFormat.Luminance, KtxGlFormat.Luminance));
        Add(TextureFormats.Luminance8Alpha8UNorm, Uncompressed(KtxGlFormat.UnsignedByte, 1, KtxGlFormat.LuminanceAlpha, KtxGlFormat.LuminanceAlpha, KtxGlFormat.LuminanceAlpha));

        Add(TextureFormats.Bc1Rgb, Compressed(KtxGlFormat.CompressedRgbS3tcDxt1, KtxGlFormat.Rgb));
        Add(TextureFormats.Bc1Rgba, Compressed(KtxGlFormat.CompressedRgbaS3tcDxt1, KtxGlFormat.Rgba));
        Add(TextureFormats.Bc1RgbSrgb, Compressed(KtxGlFormat.CompressedSrgbS3tcDxt1, KtxGlFormat.Rgb), isSrgb: true);
        Add(TextureFormats.Bc1RgbaSrgb, Compressed(KtxGlFormat.CompressedSrgbAlphaS3tcDxt1, KtxGlFormat.Rgba), isSrgb: true);
        Add(TextureFormats.Bc2Rgba, Compressed(KtxGlFormat.CompressedRgbaS3tcDxt3, KtxGlFormat.Rgba));
        Add(TextureFormats.Bc2RgbaSrgb, Compressed(KtxGlFormat.CompressedSrgbAlphaS3tcDxt3, KtxGlFormat.Rgba), isSrgb: true);
        Add(TextureFormats.Bc3Rgba, Compressed(KtxGlFormat.CompressedRgbaS3tcDxt5, KtxGlFormat.Rgba));
        Add(TextureFormats.Bc3RgbaSrgb, Compressed(KtxGlFormat.CompressedSrgbAlphaS3tcDxt5, KtxGlFormat.Rgba), isSrgb: true);
        Add(TextureFormats.Bc4UNorm, Compressed(KtxGlFormat.CompressedRedRgtc1, KtxGlFormat.Red));
        Add(TextureFormats.Bc4SNorm, Compressed(KtxGlFormat.CompressedSignedRedRgtc1, KtxGlFormat.Red));
        Add(TextureFormats.Bc5UNorm, Compressed(KtxGlFormat.CompressedRgRgtc2, KtxGlFormat.Rg));
        Add(TextureFormats.Bc5SNorm, Compressed(KtxGlFormat.CompressedSignedRgRgtc2, KtxGlFormat.Rg));
        Add(TextureFormats.Bc6HSFloat, Compressed(KtxGlFormat.CompressedRgbBptcSignedFloat, KtxGlFormat.Rgb));
        Add(TextureFormats.Bc6HUFloat, Compressed(KtxGlFormat.CompressedRgbBptcUnsignedFloat, KtxGlFormat.Rgb));
        Add(TextureFormats.Bc7UNorm, Compressed(KtxGlFormat.CompressedRgbaBptcUnorm, KtxGlFormat.Rgba));
        Add(TextureFormats.Bc7Srgb, Compressed(KtxGlFormat.CompressedSrgbAlphaBptcUnorm, KtxGlFormat.Rgba), isSrgb: true);

        Add(TextureFormats.RgbEtc1UNorm, Compressed(KtxGlFormat.Etc1Rgb8Oes, KtxGlFormat.Rgb));
        Add(TextureFormats.RgbEtc2UNorm, Compressed(KtxGlFormat.CompressedRgb8Etc2, KtxGlFormat.Rgb));
        Add(TextureFormats.RgbEtc2Srgb, Compressed(KtxGlFormat.CompressedSrgb8Etc2, KtxGlFormat.Rgb), isSrgb: true);
        Add(TextureFormats.RgbA1Etc2UNorm, Compressed(KtxGlFormat.CompressedRgb8PunchthroughAlpha1Etc2, KtxGlFormat.Rgba));
        Add(TextureFormats.RgbA1Etc2Srgb, Compressed(KtxGlFormat.CompressedSrgb8PunchthroughAlpha1Etc2, KtxGlFormat.Rgba), isSrgb: true);
        Add(TextureFormats.RgbaEtc2EacUNorm, Compressed(KtxGlFormat.CompressedRgba8Etc2Eac, KtxGlFormat.Rgba));
        Add(TextureFormats.RgbaEtc2EacSrgb, Compressed(KtxGlFormat.CompressedSrgb8Alpha8Etc2Eac, KtxGlFormat.Rgba), isSrgb: true);
        Add(TextureFormats.R11EacUNorm, Compressed(KtxGlFormat.CompressedR11Eac, KtxGlFormat.Red));
        Add(TextureFormats.R11EacSNorm, Compressed(KtxGlFormat.CompressedSignedR11Eac, KtxGlFormat.Red));
        Add(TextureFormats.Rg11EacUNorm, Compressed(KtxGlFormat.CompressedRg11Eac, KtxGlFormat.Rg));
        Add(TextureFormats.Rg11EacSNorm, Compressed(KtxGlFormat.CompressedSignedRg11Eac, KtxGlFormat.Rg));

        AddVk(TextureFormats.R8, KtxVkFormat.R8UNorm);
        AddVk(TextureFormats.R8SNorm, KtxVkFormat.R8SNorm);
        AddVk(TextureFormats.R8UInt, KtxVkFormat.R8UInt);
        AddVk(TextureFormats.R8SInt, KtxVkFormat.R8SInt);
        AddVk(TextureFormats.R8Srgb, KtxVkFormat.R8Srgb, isSrgb: true);
        AddVk(TextureFormats.Rg8, KtxVkFormat.R8G8UNorm);
        AddVk(TextureFormats.Rg8SNorm, KtxVkFormat.R8G8SNorm);
        AddVk(TextureFormats.Rg8UInt, KtxVkFormat.R8G8UInt);
        AddVk(TextureFormats.Rg8SInt, KtxVkFormat.R8G8SInt);
        AddVk(TextureFormats.Rg8Srgb, KtxVkFormat.R8G8Srgb, isSrgb: true);
        AddVk(TextureFormats.Rgb8, KtxVkFormat.R8G8B8UNorm);
        AddVk(TextureFormats.Rgb8SNorm, KtxVkFormat.R8G8B8SNorm);
        AddVk(TextureFormats.Rgb8UInt, KtxVkFormat.R8G8B8UInt);
        AddVk(TextureFormats.Rgb8SInt, KtxVkFormat.R8G8B8SInt);
        AddVk(TextureFormats.Rgb8Srgb, KtxVkFormat.R8G8B8Srgb, isSrgb: true);
        AddVk(TextureFormats.Bgr8UNorm, KtxVkFormat.B8G8R8UNorm);
        AddVk(TextureFormats.Bgr8Srgb, KtxVkFormat.B8G8R8Srgb, isSrgb: true);
        AddVk(TextureFormats.Rgba8UNorm, KtxVkFormat.R8G8B8A8UNorm);
        AddVk(TextureFormats.Rgba8SNorm, KtxVkFormat.R8G8B8A8SNorm);
        AddVk(TextureFormats.Rgba8UInt, KtxVkFormat.R8G8B8A8UInt);
        AddVk(TextureFormats.Rgba8SInt, KtxVkFormat.R8G8B8A8SInt);
        AddVk(TextureFormats.Rgba8Srgb, KtxVkFormat.R8G8B8A8Srgb, isSrgb: true);
        AddVk(TextureFormats.Bgra8, KtxVkFormat.B8G8R8A8UNorm);
        AddVk(TextureFormats.Bgra8Srgb, KtxVkFormat.B8G8R8A8Srgb, isSrgb: true);
        AddVk(TextureFormats.R16UNorm, KtxVkFormat.R16UNorm);
        AddVk(TextureFormats.R16SNorm, KtxVkFormat.R16SNorm);
        AddVk(TextureFormats.R16UInt, KtxVkFormat.R16UInt);
        AddVk(TextureFormats.R16SInt, KtxVkFormat.R16SInt);
        AddVk(TextureFormats.R16Float, KtxVkFormat.R16SFloat);
        AddVk(TextureFormats.Rg16UNorm, KtxVkFormat.R16G16UNorm);
        AddVk(TextureFormats.Rg16SNorm, KtxVkFormat.R16G16SNorm);
        AddVk(TextureFormats.Rg16UInt, KtxVkFormat.R16G16UInt);
        AddVk(TextureFormats.Rg16SInt, KtxVkFormat.R16G16SInt);
        AddVk(TextureFormats.Rg16Float, KtxVkFormat.R16G16SFloat);
        AddVk(TextureFormats.Rgb16UNorm, KtxVkFormat.R16G16B16UNorm);
        AddVk(TextureFormats.Rgb16Float, KtxVkFormat.R16G16B16SFloat);
        AddVk(TextureFormats.Rgba16UNorm, KtxVkFormat.R16G16B16A16UNorm);
        AddVk(TextureFormats.Rgba16SNorm, KtxVkFormat.R16G16B16A16SNorm);
        AddVk(TextureFormats.Rgba16UInt, KtxVkFormat.R16G16B16A16UInt);
        AddVk(TextureFormats.Rgba16SInt, KtxVkFormat.R16G16B16A16SInt);
        AddVk(TextureFormats.Rgba16Float, KtxVkFormat.R16G16B16A16SFloat);
        AddVk(TextureFormats.R32UInt, KtxVkFormat.R32UInt);
        AddVk(TextureFormats.R32SInt, KtxVkFormat.R32SInt);
        AddVk(TextureFormats.R32Float, KtxVkFormat.R32SFloat);
        AddVk(TextureFormats.Rg32UInt, KtxVkFormat.R32G32UInt);
        AddVk(TextureFormats.Rg32SInt, KtxVkFormat.R32G32SInt);
        AddVk(TextureFormats.Rg32Float, KtxVkFormat.R32G32SFloat);
        AddVk(TextureFormats.Rgb32Float, KtxVkFormat.R32G32B32SFloat);
        AddVk(TextureFormats.Rgba32UInt, KtxVkFormat.R32G32B32A32UInt);
        AddVk(TextureFormats.Rgba32SInt, KtxVkFormat.R32G32B32A32SInt);
        AddVk(TextureFormats.Rgba32Float, KtxVkFormat.R32G32B32A32SFloat);

        AddVk(TextureFormats.Bc1Rgb, KtxVkFormat.Bc1RgbUNormBlock);
        AddVk(TextureFormats.Bc1RgbSrgb, KtxVkFormat.Bc1RgbSrgbBlock, isSrgb: true);
        AddVk(TextureFormats.Bc1Rgba, KtxVkFormat.Bc1RgbaUNormBlock);
        AddVk(TextureFormats.Bc1RgbaSrgb, KtxVkFormat.Bc1RgbaSrgbBlock, isSrgb: true);
        AddVk(TextureFormats.Bc2Rgba, KtxVkFormat.Bc2UNormBlock);
        AddVk(TextureFormats.Bc2RgbaSrgb, KtxVkFormat.Bc2SrgbBlock, isSrgb: true);
        AddVk(TextureFormats.Bc3Rgba, KtxVkFormat.Bc3UNormBlock);
        AddVk(TextureFormats.Bc3RgbaSrgb, KtxVkFormat.Bc3SrgbBlock, isSrgb: true);
        AddVk(TextureFormats.Bc4UNorm, KtxVkFormat.Bc4UNormBlock);
        AddVk(TextureFormats.Bc4SNorm, KtxVkFormat.Bc4SNormBlock);
        AddVk(TextureFormats.Bc5UNorm, KtxVkFormat.Bc5UNormBlock);
        AddVk(TextureFormats.Bc5SNorm, KtxVkFormat.Bc5SNormBlock);
        AddVk(TextureFormats.Bc6HUFloat, KtxVkFormat.Bc6HUFloatBlock);
        AddVk(TextureFormats.Bc6HSFloat, KtxVkFormat.Bc6HSFloatBlock);
        AddVk(TextureFormats.Bc7UNorm, KtxVkFormat.Bc7UNormBlock);
        AddVk(TextureFormats.Bc7Srgb, KtxVkFormat.Bc7SrgbBlock, isSrgb: true);
        AddVk(TextureFormats.RgbEtc2UNorm, KtxVkFormat.Etc2R8G8B8UNormBlock);
        AddVk(TextureFormats.RgbEtc2Srgb, KtxVkFormat.Etc2R8G8B8SrgbBlock, isSrgb: true);
        AddVk(TextureFormats.RgbA1Etc2UNorm, KtxVkFormat.Etc2R8G8B8A1UNormBlock);
        AddVk(TextureFormats.RgbA1Etc2Srgb, KtxVkFormat.Etc2R8G8B8A1SrgbBlock, isSrgb: true);
        AddVk(TextureFormats.RgbaEtc2EacUNorm, KtxVkFormat.Etc2R8G8B8A8UNormBlock);
        AddVk(TextureFormats.RgbaEtc2EacSrgb, KtxVkFormat.Etc2R8G8B8A8SrgbBlock, isSrgb: true);
        AddVk(TextureFormats.R11EacUNorm, KtxVkFormat.EacR11UNormBlock);
        AddVk(TextureFormats.R11EacSNorm, KtxVkFormat.EacR11SNormBlock);
        AddVk(TextureFormats.Rg11EacUNorm, KtxVkFormat.EacR11G11UNormBlock);
        AddVk(TextureFormats.Rg11EacSNorm, KtxVkFormat.EacR11G11SNormBlock);
        AddVk(TextureFormats.RgbaAstc4x4UNorm, KtxVkFormat.Astc4x4UNormBlock);
        AddVk(TextureFormats.RgbaAstc4x4Srgb, KtxVkFormat.Astc4x4SrgbBlock, isSrgb: true);

        return new Mappings(textureToKtx, uncompressedToTexture, compressedToTexture, glInternalFormatToTexture, textureToVk, vkFormatToTexture);
    }

    private readonly record struct KtxHeader(
        bool LittleEndian,
        uint GlType,
        uint GlTypeSize,
        uint GlFormat,
        uint GlInternalFormat,
        uint GlBaseInternalFormat,
        int Width,
        int Height,
        uint PixelDepth,
        uint NumberOfArrayElements,
        uint NumberOfFaces,
        uint NumberOfMipmapLevels,
        uint BytesOfKeyValueData);

    private readonly record struct KtxHeaderV2(
        KtxVkFormat VkFormat,
        uint TypeSize,
        int Width,
        int Height,
        uint PixelDepth,
        uint LayerCount,
        uint FaceCount,
        uint LevelCount,
        KtxSupercompressionScheme SupercompressionScheme,
        uint DfdByteOffset,
        uint DfdByteLength,
        uint KvdByteOffset,
        uint KvdByteLength,
        ulong SgdByteOffset,
        ulong SgdByteLength);

    private readonly record struct KtxLevelIndex(ulong ByteOffset, ulong ByteLength, ulong UncompressedByteLength);

    private readonly record struct KtxBasisLzGlobalData(
        int EndpointCount,
        int SelectorCount,
        byte[] EndpointData,
        byte[] SelectorData,
        byte[] TableData,
        KtxBasisLzImageDesc[] ImageDescs);

    private readonly record struct KtxBasisLzImageDesc(
        uint ImageFlags,
        uint RgbSliceByteOffset,
        uint RgbSliceByteLength,
        uint AlphaSliceByteOffset,
        uint AlphaSliceByteLength)
    {
        public bool IsPFrame => (ImageFlags & BasisLzImageFlagIsPFrame) != 0;
    }

    private readonly record struct KtxFormatDescriptor(
        uint GlType,
        uint GlTypeSize,
        uint GlFormat,
        uint GlInternalFormat,
        uint GlBaseInternalFormat);

    private readonly record struct UncompressedFormatKey(KtxGlFormat GlType, KtxGlFormat GlFormat, KtxGlFormat GlInternalFormat);

    private readonly record struct GlInternalFormatKey(KtxGlFormat GlInternalFormat, bool IsSrgb);

    private readonly record struct GlInternalFormatMapping(TextureFormat TextureFormat, KtxFormatDescriptor Descriptor);

    private readonly record struct VkFormatKey(KtxVkFormat VkFormat, bool IsSrgb);

    private readonly record struct VkFormatMapping(TextureFormat TextureFormat, KtxVkFormat VkFormat);

    private sealed record Mappings(
        Dictionary<TextureFormat, KtxFormatDescriptor> TextureToKtx,
        Dictionary<UncompressedFormatKey, TextureFormat> UncompressedToTexture,
        Dictionary<KtxGlFormat, TextureFormat> CompressedToTexture,
        Dictionary<GlInternalFormatKey, GlInternalFormatMapping> GlInternalFormatToTexture,
        Dictionary<TextureFormat, KtxVkFormat> TextureToVk,
        Dictionary<VkFormatKey, VkFormatMapping> VkFormatToTexture);

    [InlineArray(LevelIndexEntryByteCount)]
    private struct Byte24Buffer
    {
        private byte _element0;
    }

    [InlineArray(HeaderByteCount - IdentifierByteCount)]
    private struct Byte52Buffer
    {
        private byte _element0;
    }

    [InlineArray(HeaderByteCount)]
    private struct Byte64Buffer
    {
        private byte _element0;
    }

    [InlineArray(HeaderV2ByteCount - IdentifierByteCount)]
    private struct Byte68Buffer
    {
        private byte _element0;
    }

    [InlineArray(HeaderV2ByteCount)]
    private struct Byte80Buffer
    {
        private byte _element0;
    }
}
