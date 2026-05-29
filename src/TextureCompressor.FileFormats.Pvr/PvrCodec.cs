using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public static class PvrCodec
{
    private const int HeaderByteCount = 52;
    private const uint Version = 0x03525650;
    private const uint LegacyHeaderV1ByteCount = 44;
    private const uint LegacyHeaderV2ByteCount = 52;
    private const uint LegacyIdentifierV2 = 0x21525650;
    private const uint LegacyPixelTypeMask = 0xff;
    private const uint LegacyFlagMipMap = 1 << 8;
    private const uint LegacyFlagBumpMap = 1 << 10;
    private const uint LegacyFlagCubeMap = 1 << 12;
    private const uint LegacyFlagVolumeTexture = 1 << 14;
    private const uint LegacyFlagHasAlpha = 1 << 15;
    private const uint LegacyFlagVerticalFlip = 1 << 16;
    private const uint LegacySupportedFlags = LegacyPixelTypeMask | LegacyFlagHasAlpha;

    private static readonly Lazy<Mappings> SFormatMappings = new(CreateFormatMappings);

    public static PvrTexture Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static PvrTexture Read(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Read(stream);
    }

    public static PvrTexture Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ReadHeader(stream);
        ValidateHeader(header);

        var metadata = header.ContainerVersion == 3
            ? ReadMetadata(stream, checked((int)header.MetadataSize))
            : [];
        var format = header.ContainerVersion == 3
            ? GetTextureFormat(header.PixelFormat, header.ColourSpace, header.ChannelType)
            : GetLegacyTextureFormat(
                header.LegacyPixelType,
                header.LegacyHasAlpha,
                header.LegacyBitCount,
                header.LegacyRedMask,
                header.LegacyGreenMask,
                header.LegacyBlueMask,
                header.LegacyAlphaMask);
        var coder = TextureCoderManager.Global.GetCoder(format);
        var expectedPayloadByteCount = coder.GetEncodedByteCount(header.Width, header.Height);
        var payloadByteCount = header.PayloadByteCount == 0 ? expectedPayloadByteCount : header.PayloadByteCount;
        if (payloadByteCount != expectedPayloadByteCount)
        {
            throw new InvalidDataException(
                $"PVR texture payload is {payloadByteCount} bytes, but '{format.Name}' expects {expectedPayloadByteCount} bytes for {header.Width}x{header.Height}.");
        }

        var payload = new byte[payloadByteCount];
        ReadExactly(stream, payload);

        return new PvrTexture(format, header.Width, header.Height, payload, metadata);
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

    public static ArrayBitmap<TPixel> Decode<TPixel>(PvrTexture texture)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        var bitmap = new ArrayBitmap<TPixel>(texture.Width, texture.Height);
        var coder = TextureCoderManager.Global.GetCoder(texture.Format);
        coder.Decode(texture.Payload, bitmap.AsView());
        return bitmap;
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView());
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, PvrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        Encode(bitmap, stream);
        return stream.ToArray();
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, PvrEncodingOptions? options)
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
        Encode(bitmap.AsView(), stream);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, PvrEncodingOptions? options)
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
        Encode(bitmap, stream);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, PvrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream);
    }

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, PvrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, PvrEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var version = GetEncodingVersion(options);
        ValidateEncodingVersion(version);
        var selection = GetEncodingSelection(options, version);
        var format = selection.TextureFormat;
        var coder = TextureCoderManager.Global.GetCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
        coder.Encode(bitmap, payload);
        Write(new PvrTexture(format, bitmap.Width, bitmap.Height, payload), stream, options);
    }

    public static byte[] Write(PvrTexture texture)
    {
        using var stream = new MemoryStream();
        Write(texture, stream, options: null);
        return stream.ToArray();
    }

    public static byte[] Write(PvrTexture texture, PvrEncodingOptions? options)
    {
        using var stream = new MemoryStream();
        Write(texture, stream, options);
        return stream.ToArray();
    }

    public static void Write(PvrTexture texture, string path)
    {
        using var stream = File.Create(path);
        Write(texture, stream, options: null);
    }

    public static void Write(PvrTexture texture, string path, PvrEncodingOptions? options)
    {
        using var stream = File.Create(path);
        Write(texture, stream, options);
    }

    public static void Write(PvrTexture texture, Stream stream)
    {
        Write(texture, stream, options: null);
    }

    public static void Write(PvrTexture texture, Stream stream, PvrEncodingOptions? options)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        var version = GetEncodingVersion(options);
        ValidateEncodingVersion(version);

        var coder = TextureCoderManager.Global.GetCoder(texture.Format);
        var expectedByteCount = coder.GetEncodedByteCount(texture.Width, texture.Height);
        if (texture.Payload.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"PVR payload length is {texture.Payload.Length} bytes, but '{texture.Format.Name}' expects {expectedByteCount} bytes for {texture.Width}x{texture.Height}.",
                nameof(texture));
        }

        if (version == 3)
        {
            var descriptor = GetPvrDescriptor(texture.Format, options);
            var metadataSize = GetMetadataByteCount(texture.Metadata);
            WriteHeader(stream, new PvrHeader(
                ContainerVersion: 3,
                descriptor.PixelFormat,
                Flags: 0,
                descriptor.ColourSpace,
                descriptor.ChannelType,
                texture.Height,
                texture.Width,
                Depth: 1,
                SurfaceCount: 1,
                FaceCount: 1,
                MipMapCount: 1,
                metadataSize,
                PayloadByteCount: 0,
                LegacyPixelType: 0,
                LegacyHasAlpha: false,
                LegacyBitCount: 0,
                LegacyRedMask: 0,
                LegacyGreenMask: 0,
                LegacyBlueMask: 0,
                LegacyAlphaMask: 0));
            WriteMetadata(stream, texture.Metadata);
        }
        else
        {
            if (texture.Metadata.Count != 0)
            {
                throw new NotSupportedException("PVR v1/v2 does not support metadata.");
            }

            WriteLegacyHeader(stream, texture, version, checked((uint)expectedByteCount), options);
        }

        stream.Write(texture.Payload);
    }

    private static PvrHeader ReadHeader(Stream stream)
    {
        Byte52Buffer headerBuffer = default;
        Span<byte> header = headerBuffer;
        ReadExactly(stream, header[..4]);

        var firstWord = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (firstWord == Version)
        {
            ReadExactly(stream, header[4..HeaderByteCount]);
            return new PvrHeader(
                ContainerVersion: 3,
                BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(8, 8)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4)),
                ReadPositiveInt(header.Slice(24, 4), "height"),
                ReadPositiveInt(header.Slice(28, 4), "width"),
                ReadPositiveInt(header.Slice(32, 4), "depth"),
                ReadPositiveInt(header.Slice(36, 4), "surface count"),
                ReadPositiveInt(header.Slice(40, 4), "face count"),
                ReadPositiveInt(header.Slice(44, 4), "mip-map count"),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(48, 4)),
                PayloadByteCount: 0,
                LegacyPixelType: 0,
                LegacyHasAlpha: false,
                LegacyBitCount: 0,
                LegacyRedMask: 0,
                LegacyGreenMask: 0,
                LegacyBlueMask: 0,
                LegacyAlphaMask: 0);
        }

        if (firstWord == LegacyHeaderV1ByteCount)
        {
            ReadExactly(stream, header.Slice(4, checked((int)LegacyHeaderV1ByteCount - 4)));
            return ReadLegacyHeader(header[..(int)LegacyHeaderV1ByteCount], containerVersion: 1);
        }

        if (firstWord == LegacyHeaderV2ByteCount)
        {
            ReadExactly(stream, header[4..HeaderByteCount]);
            return ReadLegacyHeader(header, containerVersion: 2);
        }

        throw new InvalidDataException("The stream is not a supported PVR file.");
    }

    private static void WriteHeader(Stream stream, PvrHeader header)
    {
        Byte52Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, Version);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), header.Flags);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(8, 8), header.PixelFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), header.ColourSpace);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(20, 4), header.ChannelType);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24, 4), checked((uint)header.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(28, 4), checked((uint)header.Width));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), checked((uint)header.Depth));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(36, 4), checked((uint)header.SurfaceCount));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(40, 4), checked((uint)header.FaceCount));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(44, 4), checked((uint)header.MipMapCount));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(48, 4), header.MetadataSize);
        stream.Write(buffer);
    }

    private static PvrHeader ReadLegacyHeader(ReadOnlySpan<byte> header, int containerVersion)
    {
        var pixelFormatAndFlags = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4));
        var unsupportedFlags = pixelFormatAndFlags & ~LegacySupportedFlags;
        if (unsupportedFlags != 0)
        {
            throw new NotSupportedException($"PVR v{containerVersion} texture flags 0x{unsupportedFlags:x8} are not supported.");
        }

        var mipMapCount = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(12, 4));
        if (mipMapCount != 0)
        {
            throw new NotSupportedException("PVR v1/v2 mip-map chains are not supported.");
        }

        var surfaceCount = 1;
        if (containerVersion == 2)
        {
            var identifier = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(44, 4));
            if (identifier != LegacyIdentifierV2)
            {
                throw new InvalidDataException("PVR v2 header is missing the PVR! identifier.");
            }

            surfaceCount = ReadPositiveInt(header.Slice(48, 4), "surface count");
            if (surfaceCount != 1)
            {
                throw new NotSupportedException("PVR v2 texture arrays are not supported.");
            }
        }

        var dataSize = ReadPositiveInt(header.Slice(20, 4), "payload size");
        return new PvrHeader(
            containerVersion,
            PixelFormat: 0,
            Flags: pixelFormatAndFlags,
            ColourSpace: 0,
            ChannelType: 0,
            ReadPositiveInt(header.Slice(4, 4), "height"),
            ReadPositiveInt(header.Slice(8, 4), "width"),
            Depth: 1,
            SurfaceCount: surfaceCount,
            FaceCount: 1,
            MipMapCount: 1,
            MetadataSize: 0,
            dataSize,
            LegacyPixelType: pixelFormatAndFlags & LegacyPixelTypeMask,
            LegacyHasAlpha: (pixelFormatAndFlags & LegacyFlagHasAlpha) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(40, 4)) != 0,
            LegacyBitCount: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24, 4)),
            LegacyRedMask: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(28, 4)),
            LegacyGreenMask: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(32, 4)),
            LegacyBlueMask: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(36, 4)),
            LegacyAlphaMask: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(40, 4)));
    }

    private static void WriteLegacyHeader(Stream stream, PvrTexture texture, int version, uint payloadByteCount, PvrEncodingOptions? options)
    {
        var descriptor = GetLegacyPvrDescriptor(texture.Format, options);
        var headerSize = version == 1 ? LegacyHeaderV1ByteCount : LegacyHeaderV2ByteCount;

        Byte52Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, headerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), checked((uint)texture.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), checked((uint)texture.Width));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), (uint)descriptor.PixelType | (descriptor.HasAlpha ? LegacyFlagHasAlpha : 0));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(20, 4), payloadByteCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24, 4), descriptor.BitCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(28, 4), descriptor.RedMask);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), descriptor.GreenMask);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(36, 4), descriptor.BlueMask);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(40, 4), descriptor.AlphaMask);

        if (version == 2)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(44, 4), LegacyIdentifierV2);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(48, 4), 1);
        }

        stream.Write(buffer[..(int)headerSize]);
    }

    private static void ValidateHeader(PvrHeader header)
    {
        if (header.ContainerVersion != 3)
        {
            return;
        }

        if (header.Flags != 0)
        {
            throw new NotSupportedException($"PVR texture flags 0x{header.Flags:x8} are not supported.");
        }

        if (header.Depth != 1)
        {
            throw new NotSupportedException("PVR 3D textures are not supported.");
        }

        if (header.SurfaceCount != 1)
        {
            throw new NotSupportedException("PVR texture arrays are not supported.");
        }

        if (header.FaceCount != 1)
        {
            throw new NotSupportedException("PVR cube maps are not supported.");
        }

        if (header.MipMapCount != 1)
        {
            throw new NotSupportedException("PVR mip-map chains are not supported.");
        }
    }

    private static IReadOnlyList<PvrMetadata> ReadMetadata(Stream stream, int metadataSize)
    {
        if (metadataSize == 0)
        {
            return [];
        }

        var metadataBytes = new byte[metadataSize];
        ReadExactly(stream, metadataBytes);

        var metadata = new List<PvrMetadata>();
        var offset = 0;
        while (offset < metadataBytes.Length)
        {
            if (metadataBytes.Length - offset < 12)
            {
                throw new InvalidDataException("PVR metadata block is truncated.");
            }

            var devFourCC = BinaryPrimitives.ReadUInt32LittleEndian(metadataBytes.AsSpan(offset, 4));
            var key = BinaryPrimitives.ReadUInt32LittleEndian(metadataBytes.AsSpan(offset + 4, 4));
            var dataSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(metadataBytes.AsSpan(offset + 8, 4)));
            offset += 12;
            if (dataSize > metadataBytes.Length - offset)
            {
                throw new InvalidDataException("PVR metadata block data is truncated.");
            }

            var data = metadataBytes.AsSpan(offset, dataSize).ToArray();
            metadata.Add(new PvrMetadata(devFourCC, key, data));
            offset += dataSize;
        }

        return metadata;
    }

    private static void WriteMetadata(Stream stream, IReadOnlyList<PvrMetadata> metadata)
    {
        Byte12Buffer headerBuffer = default;
        Span<byte> header = headerBuffer;
        foreach (var item in metadata)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(item.Data);

            BinaryPrimitives.WriteUInt32LittleEndian(header, item.DevFourCC);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), item.Key);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), checked((uint)item.Data.Length));
            stream.Write(header);
            stream.Write(item.Data);
        }
    }

    private static uint GetMetadataByteCount(IReadOnlyList<PvrMetadata> metadata)
    {
        var byteCount = 0;
        foreach (var item in metadata)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(item.Data);
            byteCount = checked(byteCount + 12 + item.Data.Length);
        }

        return checked((uint)byteCount);
    }

    private static TextureFormat GetTextureFormat(ulong pixelFormat, uint colourSpace, uint channelType)
    {
        var key = new PvrFormatKey(pixelFormat, colourSpace, channelType);
        if (SFormatMappings.Value.PvrToTexture.TryGetValue(key, out var format))
        {
            return format;
        }

        throw new NotSupportedException($"Unsupported PVR pixel format 0x{pixelFormat:x16}, colour space {colourSpace}, channel type {channelType}.");
    }

    private static PvrFormatDescriptor GetPvrDescriptor(TextureFormat format, PvrEncodingOptions? options)
    {
        if (options?.TextureFormat is { } optionFormat && optionFormat != format)
        {
            throw new ArgumentException(
                $"PVR encoding options specify texture format '{optionFormat.Name}', but the texture payload uses '{format.Name}'.",
                nameof(options));
        }

        if (options?.TextureFormat is null && options?.PvrPixelFormat is { } pvrPixelFormat)
        {
            var selection = GetPvrEncodingSelection(pvrPixelFormat, options.IsSrgb);
            if (selection.TextureFormat != format)
            {
                throw new ArgumentException(
                    $"PVR encoding options specify pixel format '{pvrPixelFormat}', which maps to '{selection.TextureFormat.Name}', but the texture payload uses '{format.Name}'.",
                    nameof(options));
            }

            return selection.PvrDescriptor!.Value;
        }

        return GetPvrDescriptor(format);
    }

    private static PvrFormatDescriptor GetPvrDescriptor(TextureFormat format)
    {
        if (SFormatMappings.Value.TextureToPvr.TryGetValue(format, out var descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException($"Texture format '{format.Name}' cannot be written as a PVR v3 texture.");
    }

    private static TextureFormat GetLegacyTextureFormat(
        uint pixelType,
        bool hasAlpha,
        uint bitCount,
        uint redMask,
        uint greenMask,
        uint blueMask,
        uint alphaMask)
    {
        var mappings = SFormatMappings.Value;
        var hasLayoutMasks = HasLegacyLayoutMasks(redMask, greenMask, blueMask, alphaMask);
        if (hasLayoutMasks
            && mappings.LegacyLayoutToTexture.TryGetValue(new LegacyLayoutKey(bitCount, redMask, greenMask, blueMask, alphaMask), out var format))
        {
            return format;
        }

        if (TryGetLegacyTextureFormatByType(mappings, pixelType, hasAlpha, out format))
        {
            if (hasLayoutMasks && format.Kind == TextureFormatKind.Uncompressed)
            {
                throw new NotSupportedException(
                    $"Unsupported PVR v1/v2 pixel layout bit count {bitCount}, masks 0x{redMask:x8}/0x{greenMask:x8}/0x{blueMask:x8}/0x{alphaMask:x8}.");
            }

            return format;
        }

        throw new NotSupportedException($"Unsupported PVR v1/v2 pixel type 0x{pixelType:x2}.");
    }

    private static bool TryGetLegacyTextureFormatByType(Mappings mappings, uint pixelType, bool hasAlpha, out TextureFormat format) =>
        mappings.LegacyToTexture.TryGetValue(new LegacyFormatKey(pixelType, hasAlpha), out format)
        || mappings.LegacyToTexture.TryGetValue(new LegacyFormatKey(pixelType, HasAlpha: false), out format);

    private static bool HasLegacyLayoutMasks(uint redMask, uint greenMask, uint blueMask, uint alphaMask) =>
        (redMask | greenMask | blueMask | alphaMask) != 0;

    private static LegacyFormatDescriptor GetLegacyPvrDescriptor(TextureFormat format, PvrEncodingOptions? options)
    {
        if (options?.TextureFormat is { } optionFormat && optionFormat != format)
        {
            throw new ArgumentException(
                $"PVR encoding options specify texture format '{optionFormat.Name}', but the texture payload uses '{format.Name}'.",
                nameof(options));
        }

        if (options?.TextureFormat is null && options?.PvrLegacyPixelType is { } legacyPixelType)
        {
            if (SFormatMappings.Value.TextureToExplicitLegacy.TryGetValue(new LegacyExplicitKey(format, legacyPixelType), out var descriptor))
            {
                return descriptor;
            }

            var selection = GetLegacyEncodingSelection(legacyPixelType);
            if (selection.TextureFormat != format)
            {
                throw new ArgumentException(
                    $"PVR encoding options specify legacy pixel type '{legacyPixelType}', which maps to '{selection.TextureFormat.Name}', but the texture payload uses '{format.Name}'.",
                    nameof(options));
            }

            return selection.LegacyDescriptor!.Value;
        }

        return GetLegacyPvrDescriptor(format, options?.LegacyPixelTypePreference ?? PvrLegacyPixelTypePreference.Default);
    }

    private static LegacyFormatDescriptor GetLegacyPvrDescriptor(TextureFormat format, PvrLegacyPixelTypePreference preference)
    {
        var mappings = SFormatMappings.Value;
        if (preference != PvrLegacyPixelTypePreference.Default
            && mappings.TextureToPreferredLegacy.TryGetValue(new LegacyPreferenceKey(format, preference), out var descriptor))
        {
            return descriptor;
        }

        if (mappings.TextureToLegacy.TryGetValue(format, out descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException($"Texture format '{format.Name}' cannot be written as a PVR v1/v2 texture.");
    }

    private static EncodingSelection GetEncodingSelection(PvrEncodingOptions? options, int version)
    {
        if (options?.TextureFormat is { } textureFormat)
        {
            return new EncodingSelection(textureFormat, PvrDescriptor: null, LegacyDescriptor: null);
        }

        if (version == 3 && options?.PvrPixelFormat is { } pvrPixelFormat)
        {
            return GetPvrEncodingSelection(pvrPixelFormat, options.IsSrgb);
        }

        if (version != 3 && options?.PvrLegacyPixelType is { } legacyPixelType)
        {
            return GetLegacyEncodingSelection(legacyPixelType);
        }

        var defaultFormat = version == 3 && options?.IsSrgb == true
            ? TextureFormats.Rgba8Srgb
            : TextureFormats.Rgba8UNorm;
        return new EncodingSelection(defaultFormat, PvrDescriptor: null, LegacyDescriptor: null);
    }

    private static EncodingSelection GetPvrEncodingSelection(PvrPixelFormat pixelFormat, bool isSrgb)
    {
        var key = new PvrPixelFormatOptionKey(pixelFormat, isSrgb);
        if (SFormatMappings.Value.PvrPixelFormatToTexture.TryGetValue(key, out var mapping))
        {
            return new EncodingSelection(mapping.TextureFormat, mapping.Descriptor, LegacyDescriptor: null);
        }

        throw new NotSupportedException($"PVR v3 pixel format '{pixelFormat}' with {(isSrgb ? "sRGB" : "linear")} colour space is not supported.");
    }

    private static EncodingSelection GetLegacyEncodingSelection(PvrLegacyPixelType pixelType)
    {
        if (SFormatMappings.Value.LegacyPixelTypeToTexture.TryGetValue(pixelType, out var mapping))
        {
            return new EncodingSelection(mapping.TextureFormat, PvrDescriptor: null, mapping.Descriptor);
        }

        throw new NotSupportedException($"PVR v1/v2 pixel type '{pixelType}' cannot be used for encoding.");
    }

    private static int GetEncodingVersion(PvrEncodingOptions? options) => options?.Version ?? 3;

    private static void ValidateEncodingVersion(int version)
    {
        if (version is not (1 or 2 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(version), "PVR encoding version must be 1, 2, or 3.");
        }
    }

    private static Mappings CreateFormatMappings()
    {
        var textureToPvr = new Dictionary<TextureFormat, PvrFormatDescriptor>();
        var pvrToTexture = new Dictionary<PvrFormatKey, TextureFormat>();
        var pvrPixelFormatToTexture = new Dictionary<PvrPixelFormatOptionKey, PvrFormatMapping>();
        var textureToLegacy = new Dictionary<TextureFormat, LegacyFormatDescriptor>();
        var textureToPreferredLegacy = new Dictionary<LegacyPreferenceKey, LegacyFormatDescriptor>();
        var textureToExplicitLegacy = new Dictionary<LegacyExplicitKey, LegacyFormatDescriptor>();
        var legacyToTexture = new Dictionary<LegacyFormatKey, TextureFormat>();
        var legacyPixelTypeToTexture = new Dictionary<PvrLegacyPixelType, LegacyFormatMapping>();
        var legacyLayoutToTexture = new Dictionary<LegacyLayoutKey, TextureFormat>();

        void Add(TextureFormat format, ulong pixelFormat, PvrColourSpace colourSpace, PvrChannelType channelType)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            var descriptor = new PvrFormatDescriptor(pixelFormat, (uint)colourSpace, (uint)channelType);
            textureToPvr.TryAdd(format, descriptor);
            pvrToTexture.TryAdd(new PvrFormatKey(pixelFormat, (uint)colourSpace, (uint)channelType), format);
            if (TryGetPvrPixelFormat(pixelFormat, out var pvrPixelFormat))
            {
                pvrPixelFormatToTexture.TryAdd(
                    new PvrPixelFormatOptionKey(pvrPixelFormat, colourSpace == PvrColourSpace.Srgb),
                    new PvrFormatMapping(format, descriptor));
            }
        }

        void AddLegacy(
            TextureFormat format,
            PvrLegacyPixelType pixelType,
            uint bitCount,
            uint redMask = 0,
            uint greenMask = 0,
            uint blueMask = 0,
            uint alphaMask = 0,
            bool distinguishAlpha = false,
            PvrLegacyPixelTypePreference preference = PvrLegacyPixelTypePreference.Default)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            var hasAlpha = alphaMask != 0 || format.AlphaBits > 0;
            var descriptor = new LegacyFormatDescriptor(pixelType, bitCount, redMask, greenMask, blueMask, alphaMask, hasAlpha);
            textureToLegacy.TryAdd(format, descriptor);
            textureToExplicitLegacy.TryAdd(new LegacyExplicitKey(format, pixelType), descriptor);
            if (preference != PvrLegacyPixelTypePreference.Default)
            {
                textureToPreferredLegacy.TryAdd(new LegacyPreferenceKey(format, preference), descriptor);
            }

            legacyPixelTypeToTexture.TryAdd(pixelType, new LegacyFormatMapping(format, descriptor));
            AddLegacyLayout(format, bitCount, redMask, greenMask, blueMask, alphaMask);
            if (distinguishAlpha)
            {
                legacyToTexture.TryAdd(new LegacyFormatKey((uint)pixelType, hasAlpha), format);
            }
            else
            {
                legacyToTexture.TryAdd(new LegacyFormatKey((uint)pixelType, HasAlpha: false), format);
                legacyToTexture.TryAdd(new LegacyFormatKey((uint)pixelType, HasAlpha: true), format);
            }
        }

        void AddLegacyLayout(TextureFormat format, uint bitCount, uint redMask, uint greenMask, uint blueMask, uint alphaMask)
        {
            if (format.Kind == TextureFormatKind.Uncompressed && HasLegacyLayoutMasks(redMask, greenMask, blueMask, alphaMask))
            {
                legacyLayoutToTexture.TryAdd(new LegacyLayoutKey(bitCount, redMask, greenMask, blueMask, alphaMask), format);
            }
        }

        void AddLegacyRead(TextureFormat format, PvrLegacyPixelType pixelType, bool hasAlpha)
        {
            if (TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                legacyToTexture.TryAdd(new LegacyFormatKey((uint)pixelType, hasAlpha), format);
            }
        }

        void AddUncompressed(TextureFormat format, string channels, byte bits, PvrColourSpace colourSpace, PvrChannelType channelType) =>
            AddUncompressedWithBits(format, channels, CreateRepeatedBits(channels.Length, bits), colourSpace, channelType);

        void AddUncompressedWithBits(TextureFormat format, string channels, byte[] bits, PvrColourSpace colourSpace, PvrChannelType channelType) =>
            Add(format, MakePixelId(channels, bits), colourSpace, channelType);

        void AddAstc(TextureFormat linear, TextureFormat srgb, TextureFormat hdr, PvrPixelFormat pixelFormat)
        {
            Add(linear, (uint)pixelFormat, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
            Add(srgb, (uint)pixelFormat, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
            Add(hdr, (uint)pixelFormat, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        }

        AddUncompressed(TextureFormats.Alpha8UNorm, "a", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Alpha8SNorm, "a", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Alpha16UNorm, "a", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Alpha16SNorm, "a", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Alpha32UNorm, "a", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Alpha32SNorm, "a", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Alpha16Float, "a", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Alpha32Float, "a", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);

        AddUncompressed(TextureFormats.Luminance8UNorm, "l", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Luminance16UNorm, "l", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Luminance32UNorm, "l", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Luminance32SNorm, "l", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Luminance16Float, "l", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Luminance32Float, "l", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Luminance8Srgb, "l", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Luminance8Alpha8UNorm, "la", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Luminance16Alpha16UNorm, "la", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Luminance16Alpha16SNorm, "la", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Luminance16Alpha16Float, "la", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Luminance32Alpha32UNorm, "la", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Luminance32Alpha32SNorm, "la", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Luminance32Alpha32Float, "la", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Luminance8Alpha8Srgb, "la", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Intensity8UNorm, "i", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Intensity8SNorm, "i", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Intensity16UNorm, "i", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Intensity16SNorm, "i", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Intensity32UNorm, "i", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Intensity32SNorm, "i", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Intensity16Float, "i", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Intensity32Float, "i", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);

        AddUncompressed(TextureFormats.R8, "r", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.R8SNorm, "r", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.R16UNorm, "r", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.R16SNorm, "r", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.R32UNorm, "r", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.R32SNorm, "r", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.R16Float, "r", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.R32Float, "r", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.R8Srgb, "r", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Rg8, "rg", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Rg8SNorm, "rg", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Rg16UNorm, "rg", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Rg16SNorm, "rg", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Rg32UNorm, "rg", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Rg32SNorm, "rg", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Rg16Float, "rg", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Rg32Float, "rg", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Rg8Srgb, "rg", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Rgb8, "rgb", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Rgb8SNorm, "rgb", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Rgb16UNorm, "rgb", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Rgb16SNorm, "rgb", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Rgb32UNorm, "rgb", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Rgb32SNorm, "rgb", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Rgb16Float, "rgb", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Rgb32Float, "rgb", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Rgb8Srgb, "rgb", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Bgr8UNorm, "bgr", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Bgr8SNorm, "bgr", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Bgr16UNorm, "bgr", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Bgr16SNorm, "bgr", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Bgr32UNorm, "bgr", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Bgr32SNorm, "bgr", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Bgr16Float, "bgr", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Bgr32Float, "bgr", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Bgr8Srgb, "bgr", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Rgba8UNorm, "rgba", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Rgba8SNorm, "rgba", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Rgba16UNorm, "rgba", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Rgba16SNorm, "rgba", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Rgba32UNorm, "rgba", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Rgba32SNorm, "rgba", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Rgba16Float, "rgba", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Rgba32Float, "rgba", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Rgba8Srgb, "rgba", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Abgr8UNorm, "abgr", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Abgr8SNorm, "abgr", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Abgr8Srgb, "abgr", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Bgra8, "bgra", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Bgra8SNorm, "bgra", 8, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        AddUncompressed(TextureFormats.Bgra16UNorm, "bgra", 16, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        AddUncompressed(TextureFormats.Bgra16SNorm, "bgra", 16, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        AddUncompressed(TextureFormats.Bgra32UNorm, "bgra", 32, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        AddUncompressed(TextureFormats.Bgra32SNorm, "bgra", 32, PvrColourSpace.Linear, PvrChannelType.SignedIntegerNorm);
        AddUncompressed(TextureFormats.Bgra16Float, "bgra", 16, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Bgra32Float, "bgra", 32, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        AddUncompressed(TextureFormats.Bgra8Srgb, "bgra", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        AddUncompressed(TextureFormats.Bgrx8UNorm, "bgrx", 8, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        AddUncompressed(TextureFormats.Bgrx8Srgb, "bgrx", 8, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        Add(TextureFormats.RgbPvrtcI2BppUNorm, (uint)PvrPixelFormat.PvrtcI2BppRgb, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbPvrtcI2BppSrgb, (uint)PvrPixelFormat.PvrtcI2BppRgb, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcI2BppUNorm, (uint)PvrPixelFormat.PvrtcI2BppRgba, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcI2BppSrgb, (uint)PvrPixelFormat.PvrtcI2BppRgba, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbPvrtcI4BppUNorm, (uint)PvrPixelFormat.PvrtcI4BppRgb, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbPvrtcI4BppSrgb, (uint)PvrPixelFormat.PvrtcI4BppRgb, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcI4BppUNorm, (uint)PvrPixelFormat.PvrtcI4BppRgba, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcI4BppSrgb, (uint)PvrPixelFormat.PvrtcI4BppRgba, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcII2BppUNorm, (uint)PvrPixelFormat.PvrtcII2Bpp, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcII2BppSrgb, (uint)PvrPixelFormat.PvrtcII2Bpp, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcII4BppUNorm, (uint)PvrPixelFormat.PvrtcII4Bpp, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaPvrtcII4BppSrgb, (uint)PvrPixelFormat.PvrtcII4Bpp, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbPvrtcI6BppFloat, (uint)PvrPixelFormat.PvrtcIHdr6Bpp, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        Add(TextureFormats.RgbPvrtcI8BppFloat, (uint)PvrPixelFormat.PvrtcIHdr8Bpp, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        Add(TextureFormats.RgbPvrtcII6BppFloat, (uint)PvrPixelFormat.PvrtcIIHdr6Bpp, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        Add(TextureFormats.RgbPvrtcII8BppFloat, (uint)PvrPixelFormat.PvrtcIIHdr8Bpp, PvrColourSpace.Linear, PvrChannelType.SignedFloat);

        Add(TextureFormats.RgbEtc1UNorm, (uint)PvrPixelFormat.Etc1, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbEtc2UNorm, (uint)PvrPixelFormat.Etc2Rgb, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbEtc2Srgb, (uint)PvrPixelFormat.Etc2Rgb, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbA1Etc2UNorm, (uint)PvrPixelFormat.Etc2RgbA1, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbA1Etc2Srgb, (uint)PvrPixelFormat.Etc2RgbA1, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaEtc2EacUNorm, (uint)PvrPixelFormat.Etc2Rgba, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaEtc2EacSrgb, (uint)PvrPixelFormat.Etc2Rgba, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.R11EacUNorm, (uint)PvrPixelFormat.EacR11, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.R11EacSNorm, (uint)PvrPixelFormat.EacR11, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);
        Add(TextureFormats.Rg11EacUNorm, (uint)PvrPixelFormat.EacRg11, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Rg11EacSNorm, (uint)PvrPixelFormat.EacRg11, PvrColourSpace.Linear, PvrChannelType.SignedShortNorm);

        Add(TextureFormats.Bc1Rgba, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc1RgbaSrgb, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc1Rgb, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc1RgbSrgb, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt1Rgba, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt1RgbaSrgb, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt1Rgb, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt1RgbSrgb, (uint)PvrPixelFormat.Dxt1, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt2Rgba, (uint)PvrPixelFormat.Dxt2, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc2Rgba, (uint)PvrPixelFormat.Dxt3, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc2RgbaSrgb, (uint)PvrPixelFormat.Dxt3, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt3Rgba, (uint)PvrPixelFormat.Dxt3, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt3RgbaSrgb, (uint)PvrPixelFormat.Dxt3, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt4Rgba, (uint)PvrPixelFormat.Dxt4, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc3Rgba, (uint)PvrPixelFormat.Dxt5, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc3RgbaSrgb, (uint)PvrPixelFormat.Dxt5, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt5Rgba, (uint)PvrPixelFormat.Dxt5, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Dxt5RgbaSrgb, (uint)PvrPixelFormat.Dxt5, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        Add(TextureFormats.Bc4UNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Rgtc1UNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Ati1UNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Latc1UNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc4SNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Rgtc1SNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Ati1SNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Latc1SNorm, (uint)PvrPixelFormat.Bc4, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Bc5UNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Rgtc2UNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Ati2UNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Latc2UNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc5SNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Rgtc2SNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Ati2SNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Latc2SNorm, (uint)PvrPixelFormat.Bc5, PvrColourSpace.Linear, PvrChannelType.SignedByteNorm);
        Add(TextureFormats.Bc6HUFloat, (uint)PvrPixelFormat.Bc6, PvrColourSpace.Linear, PvrChannelType.UnsignedFloat);
        Add(TextureFormats.RgbBptcUFloat, (uint)PvrPixelFormat.Bc6, PvrColourSpace.Linear, PvrChannelType.UnsignedFloat);
        Add(TextureFormats.Bc6HSFloat, (uint)PvrPixelFormat.Bc6, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        Add(TextureFormats.RgbBptcSFloat, (uint)PvrPixelFormat.Bc6, PvrColourSpace.Linear, PvrChannelType.SignedFloat);
        Add(TextureFormats.Bc7UNorm, (uint)PvrPixelFormat.Bc7, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaBptcUNorm, (uint)PvrPixelFormat.Bc7, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bc7Srgb, (uint)PvrPixelFormat.Bc7, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.RgbaBptcSrgb, (uint)PvrPixelFormat.Bc7, PvrColourSpace.Srgb, PvrChannelType.UnsignedByteNorm);

        Add(TextureFormats.Uyvy422UNorm, (uint)PvrPixelFormat.Uyvy422, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuy2UNorm, (uint)PvrPixelFormat.Yuy2422, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Bw1BppUNorm, (uint)PvrPixelFormat.Bw1Bpp, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Rgb9E5, (uint)PvrPixelFormat.SharedExponentR9G9B9E5, PvrColourSpace.Linear, PvrChannelType.UnsignedFloat);
        Add(TextureFormats.R8G8B8G8_422UNorm, (uint)PvrPixelFormat.Rgbg8888, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.G8R8G8B8_422UNorm, (uint)PvrPixelFormat.Grgb8888, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);

        AddAstc(TextureFormats.RgbaAstc4x4UNorm, TextureFormats.RgbaAstc4x4Srgb, TextureFormats.RgbaAstc4x4Float, PvrPixelFormat.Astc4X4);
        AddAstc(TextureFormats.RgbaAstc5x4UNorm, TextureFormats.RgbaAstc5x4Srgb, TextureFormats.RgbaAstc5x4Float, PvrPixelFormat.Astc5X4);
        AddAstc(TextureFormats.RgbaAstc5x5UNorm, TextureFormats.RgbaAstc5x5Srgb, TextureFormats.RgbaAstc5x5Float, PvrPixelFormat.Astc5X5);
        AddAstc(TextureFormats.RgbaAstc6x5UNorm, TextureFormats.RgbaAstc6x5Srgb, TextureFormats.RgbaAstc6x5Float, PvrPixelFormat.Astc6X5);
        AddAstc(TextureFormats.RgbaAstc6x6UNorm, TextureFormats.RgbaAstc6x6Srgb, TextureFormats.RgbaAstc6x6Float, PvrPixelFormat.Astc6X6);
        AddAstc(TextureFormats.RgbaAstc8x5UNorm, TextureFormats.RgbaAstc8x5Srgb, TextureFormats.RgbaAstc8x5Float, PvrPixelFormat.Astc8X5);
        AddAstc(TextureFormats.RgbaAstc8x6UNorm, TextureFormats.RgbaAstc8x6Srgb, TextureFormats.RgbaAstc8x6Float, PvrPixelFormat.Astc8X6);
        AddAstc(TextureFormats.RgbaAstc8x8UNorm, TextureFormats.RgbaAstc8x8Srgb, TextureFormats.RgbaAstc8x8Float, PvrPixelFormat.Astc8X8);
        AddAstc(TextureFormats.RgbaAstc10x5UNorm, TextureFormats.RgbaAstc10x5Srgb, TextureFormats.RgbaAstc10x5Float, PvrPixelFormat.Astc10X5);
        AddAstc(TextureFormats.RgbaAstc10x6UNorm, TextureFormats.RgbaAstc10x6Srgb, TextureFormats.RgbaAstc10x6Float, PvrPixelFormat.Astc10X6);
        AddAstc(TextureFormats.RgbaAstc10x8UNorm, TextureFormats.RgbaAstc10x8Srgb, TextureFormats.RgbaAstc10x8Float, PvrPixelFormat.Astc10X8);
        AddAstc(TextureFormats.RgbaAstc10x10UNorm, TextureFormats.RgbaAstc10x10Srgb, TextureFormats.RgbaAstc10x10Float, PvrPixelFormat.Astc10X10);
        AddAstc(TextureFormats.RgbaAstc12x10UNorm, TextureFormats.RgbaAstc12x10Srgb, TextureFormats.RgbaAstc12x10Float, PvrPixelFormat.Astc12X10);
        AddAstc(TextureFormats.RgbaAstc12x12UNorm, TextureFormats.RgbaAstc12x12Srgb, TextureFormats.RgbaAstc12x12Float, PvrPixelFormat.Astc12X12);

        Add(TextureFormats.Rgbm, (uint)PvrPixelFormat.Rgbm, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Rgbd, (uint)PvrPixelFormat.Rgbd, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);

        Add(TextureFormats.Vyua10Msb444UNorm, (uint)PvrPixelFormat.Vyua10Msb444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Vyua10Lsb444UNorm, (uint)PvrPixelFormat.Vyua10Lsb444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Vyua12Msb444UNorm, (uint)PvrPixelFormat.Vyua12Msb444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Vyua12Lsb444UNorm, (uint)PvrPixelFormat.Vyua12Lsb444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Uyv10A2_444UNorm, (uint)PvrPixelFormat.Uyv10A2_444, PvrColourSpace.Linear, PvrChannelType.UnsignedIntegerNorm);
        Add(TextureFormats.Uyva16_444UNorm, (uint)PvrPixelFormat.Uyva16_444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuyv16_422UNorm, (uint)PvrPixelFormat.Yuyv16_422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Uyvy16_422UNorm, (uint)PvrPixelFormat.Uyvy16_422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuyv10Msb422UNorm, (uint)PvrPixelFormat.Yuyv10Msb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuyv10Lsb422UNorm, (uint)PvrPixelFormat.Yuyv10Lsb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Uyvy10Msb422UNorm, (uint)PvrPixelFormat.Uyvy10Msb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Uyvy10Lsb422UNorm, (uint)PvrPixelFormat.Uyvy10Lsb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuyv12Msb422UNorm, (uint)PvrPixelFormat.Yuyv12Msb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuyv12Lsb422UNorm, (uint)PvrPixelFormat.Yuyv12Lsb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Uyvy12Msb422UNorm, (uint)PvrPixelFormat.Uyvy12Msb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Uyvy12Lsb422UNorm, (uint)PvrPixelFormat.Uyvy12Lsb422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv3P444UNorm, (uint)PvrPixelFormat.Yuv3P444, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv10Msb3P444UNorm, (uint)PvrPixelFormat.Yuv10Msb3P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv10Lsb3P444UNorm, (uint)PvrPixelFormat.Yuv10Lsb3P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Msb3P444UNorm, (uint)PvrPixelFormat.Yuv12Msb3P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Lsb3P444UNorm, (uint)PvrPixelFormat.Yuv12Lsb3P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv16_3P444UNorm, (uint)PvrPixelFormat.Yuv16_3P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv3P422UNorm, (uint)PvrPixelFormat.Yuv3P422, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv10Msb3P422UNorm, (uint)PvrPixelFormat.Yuv10Msb3P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv10Lsb3P422UNorm, (uint)PvrPixelFormat.Yuv10Lsb3P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Msb3P422UNorm, (uint)PvrPixelFormat.Yuv12Msb3P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Lsb3P422UNorm, (uint)PvrPixelFormat.Yuv12Lsb3P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv16_3P422UNorm, (uint)PvrPixelFormat.Yuv16_3P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv3P420UNorm, (uint)PvrPixelFormat.Yuv3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv10Msb3P420UNorm, (uint)PvrPixelFormat.Yuv10Msb3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv10Lsb3P420UNorm, (uint)PvrPixelFormat.Yuv10Lsb3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Msb3P420UNorm, (uint)PvrPixelFormat.Yuv12Msb3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Lsb3P420UNorm, (uint)PvrPixelFormat.Yuv12Lsb3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv16_3P420UNorm, (uint)PvrPixelFormat.Yuv16_3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu3P420UNorm, (uint)PvrPixelFormat.Yvu3P420, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv2P422UNorm, (uint)PvrPixelFormat.Yuv2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv10Msb2P422UNorm, (uint)PvrPixelFormat.Yuv10Msb2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv10Lsb2P422UNorm, (uint)PvrPixelFormat.Yuv10Lsb2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Msb2P422UNorm, (uint)PvrPixelFormat.Yuv12Msb2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Lsb2P422UNorm, (uint)PvrPixelFormat.Yuv12Lsb2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv16_2P422UNorm, (uint)PvrPixelFormat.Yuv16_2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv2P420UNorm, (uint)PvrPixelFormat.Yuv2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv10Msb2P420UNorm, (uint)PvrPixelFormat.Yuv10Msb2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv10Lsb2P420UNorm, (uint)PvrPixelFormat.Yuv10Lsb2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Msb2P420UNorm, (uint)PvrPixelFormat.Yuv12Msb2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv12Lsb2P420UNorm, (uint)PvrPixelFormat.Yuv12Lsb2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv16_2P420UNorm, (uint)PvrPixelFormat.Yuv16_2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv2P444UNorm, (uint)PvrPixelFormat.Yuv2P444, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yvu2P444UNorm, (uint)PvrPixelFormat.Yvu2P444, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yuv10Msb2P444UNorm, (uint)PvrPixelFormat.Yuv10Msb2P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yuv10Lsb2P444UNorm, (uint)PvrPixelFormat.Yuv10Lsb2P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu10Msb2P444UNorm, (uint)PvrPixelFormat.Yvu10Msb2P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu10Lsb2P444UNorm, (uint)PvrPixelFormat.Yvu10Lsb2P444, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu2P422UNorm, (uint)PvrPixelFormat.Yvu2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yvu10Msb2P422UNorm, (uint)PvrPixelFormat.Yvu10Msb2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu10Lsb2P422UNorm, (uint)PvrPixelFormat.Yvu10Lsb2P422, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu2P420UNorm, (uint)PvrPixelFormat.Yvu2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedByteNorm);
        Add(TextureFormats.Yvu10Msb2P420UNorm, (uint)PvrPixelFormat.Yvu10Msb2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);
        Add(TextureFormats.Yvu10Lsb2P420UNorm, (uint)PvrPixelFormat.Yvu10Lsb2P420, PvrColourSpace.Linear, PvrChannelType.UnsignedShortNorm);

        AddLegacy(TextureFormats.Rgba4UNorm, PvrLegacyPixelType.GlRgba4444, 16, 0xf000, 0x0f00, 0x00f0, 0x000f, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Rgb5A1UNorm, PvrLegacyPixelType.GlRgba5551, 16, 0xf800, 0x07c0, 0x003e, 0x0001, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Rgb565UNorm, PvrLegacyPixelType.GlRgb565, 16, 0xf800, 0x07e0, 0x001f, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Rgb5UNorm, PvrLegacyPixelType.GlRgb555, 16, 0x7c00, 0x03e0, 0x001f, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Rgba8UNorm, PvrLegacyPixelType.GlRgba8888, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Rgb8, PvrLegacyPixelType.GlRgb888, 24, 0x0000ff, 0x00ff00, 0xff0000, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Luminance8UNorm, PvrLegacyPixelType.GlI8, 8, 0xff, 0xff, 0xff, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Luminance8Alpha8UNorm, PvrLegacyPixelType.GlAi88, 16, 0x00ff, 0x00ff, 0x00ff, 0xff00, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Bgra8, PvrLegacyPixelType.GlBgra8888, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.Alpha8UNorm, PvrLegacyPixelType.GlA8, 8, alphaMask: 0xff, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.RgbaPvrtcI2BppUNorm, PvrLegacyPixelType.GlPvrtc2, 2, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.RgbPvrtcI2BppUNorm, PvrLegacyPixelType.GlPvrtc2, 2, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.RgbaPvrtcI4BppUNorm, PvrLegacyPixelType.GlPvrtc4, 4, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.RgbPvrtcI4BppUNorm, PvrLegacyPixelType.GlPvrtc4, 4, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.RgbaPvrtcII4BppUNorm, PvrLegacyPixelType.GlPvrtcII4, 4, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.Gl);
        AddLegacy(TextureFormats.RgbaPvrtcII2BppUNorm, PvrLegacyPixelType.GlPvrtcII2, 2, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.Gl);

        AddLegacy(TextureFormats.Rgba4UNorm, PvrLegacyPixelType.MglArgb4444, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Rgb5A1UNorm, PvrLegacyPixelType.MglArgb1555, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Rgb565UNorm, PvrLegacyPixelType.MglRgb565, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Rgb5UNorm, PvrLegacyPixelType.MglRgb555, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Rgb8, PvrLegacyPixelType.MglRgb888, 24, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Rgba8UNorm, PvrLegacyPixelType.MglArgb8888, 32, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.A8Rgb332UNorm, PvrLegacyPixelType.MglArgb8332, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Luminance8UNorm, PvrLegacyPixelType.MglI8, 8, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Luminance8Alpha8UNorm, PvrLegacyPixelType.MglAi88, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Bw1BppUNorm, PvrLegacyPixelType.MglOneBpp, 1, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Vy1Uy0422UNorm, PvrLegacyPixelType.MglVy1Uy0, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.Y1Vy0U422UNorm, PvrLegacyPixelType.MglY1Vy0U, 16, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.RgbaPvrtcI2BppUNorm, PvrLegacyPixelType.MglPvrtc2, 2, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.RgbPvrtcI2BppUNorm, PvrLegacyPixelType.MglPvrtc2, 2, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.RgbaPvrtcI4BppUNorm, PvrLegacyPixelType.MglPvrtc4, 4, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Mgl);
        AddLegacy(TextureFormats.RgbPvrtcI4BppUNorm, PvrLegacyPixelType.MglPvrtc4, 4, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Mgl);

        AddLegacy(TextureFormats.Bc1Rgba, PvrLegacyPixelType.D3dDxt1, 4, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Bc1Rgb, PvrLegacyPixelType.D3dDxt1, 4, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Dxt1Rgba, PvrLegacyPixelType.D3dDxt1, 4, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Dxt1Rgb, PvrLegacyPixelType.D3dDxt1, 4, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Dxt2Rgba, PvrLegacyPixelType.D3dDxt2, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Bc2Rgba, PvrLegacyPixelType.D3dDxt3, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Dxt3Rgba, PvrLegacyPixelType.D3dDxt3, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Dxt4Rgba, PvrLegacyPixelType.D3dDxt4, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Bc3Rgba, PvrLegacyPixelType.D3dDxt5, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Dxt5Rgba, PvrLegacyPixelType.D3dDxt5, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.RgbEtc1UNorm, PvrLegacyPixelType.EtcRgb4Bpp, 4);
        AddLegacy(TextureFormats.Alpha8UNorm, PvrLegacyPixelType.D3dA8, 8, alphaMask: 0xff, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Rg8SNorm, PvrLegacyPixelType.D3dV8U8, 16, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Luminance16UNorm, PvrLegacyPixelType.D3dL16, 16, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Luminance8UNorm, PvrLegacyPixelType.D3dL8, 8, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Luminance8Alpha8UNorm, PvrLegacyPixelType.D3dA8L8, 16, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Uyvy422UNorm, PvrLegacyPixelType.D3dUyvy, 16, preference: PvrLegacyPixelTypePreference.D3d);
        AddLegacy(TextureFormats.Yuy2UNorm, PvrLegacyPixelType.D3dYuy2, 16, preference: PvrLegacyPixelTypePreference.D3d);

        AddLegacy(TextureFormats.Rgba32Float, PvrLegacyPixelType.DxgiR32G32B32A32Float, 128, 0xffffffff, 0xffffffff, 0xffffffff, 0xffffffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba32UInt, PvrLegacyPixelType.DxgiR32G32B32A32UInt, 128, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba32SInt, PvrLegacyPixelType.DxgiR32G32B32A32SInt, 128, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgb32Float, PvrLegacyPixelType.DxgiR32G32B32Float, 96, 0xffffffff, 0xffffffff, 0xffffffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgb32UInt, PvrLegacyPixelType.DxgiR32G32B32UInt, 96, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgb32SInt, PvrLegacyPixelType.DxgiR32G32B32SInt, 96, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba16Float, PvrLegacyPixelType.DxgiR16G16B16A16Float, 64, 0xffff, 0xffff, 0xffff, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba16UNorm, PvrLegacyPixelType.DxgiR16G16B16A16UNorm, 64, 0xffff, 0xffff, 0xffff, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba16UInt, PvrLegacyPixelType.DxgiR16G16B16A16UInt, 64, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba16SNorm, PvrLegacyPixelType.DxgiR16G16B16A16SNorm, 64, 0xffff, 0xffff, 0xffff, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba16SInt, PvrLegacyPixelType.DxgiR16G16B16A16SInt, 64, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg32Float, PvrLegacyPixelType.DxgiR32G32Float, 64, 0xffffffff, 0xffffffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg32UInt, PvrLegacyPixelType.DxgiR32G32UInt, 64, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg32SInt, PvrLegacyPixelType.DxgiR32G32SInt, 64, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgb10A2RevUNorm, PvrLegacyPixelType.DxgiR10G10B10A2UNorm, 32, 0x000003ff, 0x000ffc00, 0x3ff00000, 0xc0000000, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgb10A2UInt, PvrLegacyPixelType.DxgiR10G10B10A2UInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R11G11B10Float, PvrLegacyPixelType.DxgiR11G11B10Float, 32, 0x000007ff, 0x003ff800, 0xffc00000, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba8UNorm, PvrLegacyPixelType.DxgiR8G8B8A8UNorm, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba8Srgb, PvrLegacyPixelType.DxgiR8G8B8A8UNormSrgb, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba8UInt, PvrLegacyPixelType.DxgiR8G8B8A8UInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba8SNorm, PvrLegacyPixelType.DxgiR8G8B8A8SNorm, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgba8SInt, PvrLegacyPixelType.DxgiR8G8B8A8SInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg16Float, PvrLegacyPixelType.DxgiR16G16Float, 32, 0xffff, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg16UNorm, PvrLegacyPixelType.DxgiR16G16UNorm, 32, 0xffff, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg16UInt, PvrLegacyPixelType.DxgiR16G16UInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg16SNorm, PvrLegacyPixelType.DxgiR16G16SNorm, 32, 0xffff, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg16SInt, PvrLegacyPixelType.DxgiR16G16SInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R32Float, PvrLegacyPixelType.DxgiR32Float, 32, 0xffffffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R32UInt, PvrLegacyPixelType.DxgiR32UInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R32SInt, PvrLegacyPixelType.DxgiR32SInt, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg8, PvrLegacyPixelType.DxgiR8G8UNorm, 16, 0x00ff, 0xff00, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg8UInt, PvrLegacyPixelType.DxgiR8G8UInt, 16, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg8SNorm, PvrLegacyPixelType.DxgiR8G8SNorm, 16, 0x00ff, 0xff00, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rg8SInt, PvrLegacyPixelType.DxgiR8G8SInt, 16, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R16Float, PvrLegacyPixelType.DxgiR16Float, 16, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R16UNorm, PvrLegacyPixelType.DxgiR16UNorm, 16, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R16UInt, PvrLegacyPixelType.DxgiR16UInt, 16, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R16SNorm, PvrLegacyPixelType.DxgiR16SNorm, 16, 0xffff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R16SInt, PvrLegacyPixelType.DxgiR16SInt, 16, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R8, PvrLegacyPixelType.DxgiR8UNorm, 8, 0xff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R8UInt, PvrLegacyPixelType.DxgiR8UInt, 8, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R8SNorm, PvrLegacyPixelType.DxgiR8SNorm, 8, 0xff, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R8SInt, PvrLegacyPixelType.DxgiR8SInt, 8, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Alpha8UNorm, PvrLegacyPixelType.DxgiA8UNorm, 8, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bw1BppUNorm, PvrLegacyPixelType.DxgiR1UNorm, 1, 0x1, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Rgb9E5, PvrLegacyPixelType.DxgiR9G9B9E5, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.R8G8B8G8_422UNorm, PvrLegacyPixelType.DxgiR8G8B8G8UNorm, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.G8R8G8B8_422UNorm, PvrLegacyPixelType.DxgiG8R8G8B8UNorm, 32, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc1Rgba, PvrLegacyPixelType.DxgiBc1UNorm, 4, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc1Rgb, PvrLegacyPixelType.DxgiBc1UNorm, 4, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc1RgbaSrgb, PvrLegacyPixelType.DxgiBc1UNormSrgb, 4, alphaMask: 0x1, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc1RgbSrgb, PvrLegacyPixelType.DxgiBc1UNormSrgb, 4, distinguishAlpha: true, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc2Rgba, PvrLegacyPixelType.DxgiBc2UNorm, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc2RgbaSrgb, PvrLegacyPixelType.DxgiBc2UNormSrgb, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc3Rgba, PvrLegacyPixelType.DxgiBc3UNorm, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc3RgbaSrgb, PvrLegacyPixelType.DxgiBc3UNormSrgb, 8, alphaMask: 0x1, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc4UNorm, PvrLegacyPixelType.DxgiBc4UNorm, 4, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc4SNorm, PvrLegacyPixelType.DxgiBc4SNorm, 4, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc5UNorm, PvrLegacyPixelType.DxgiBc5UNorm, 8, preference: PvrLegacyPixelTypePreference.Dxgi);
        AddLegacy(TextureFormats.Bc5SNorm, PvrLegacyPixelType.DxgiBc5SNorm, 8, preference: PvrLegacyPixelTypePreference.Dxgi);

        AddLegacyRead(TextureFormats.Rgba4UNorm, PvrLegacyPixelType.MglArgb4444, hasAlpha: true);
        AddLegacyRead(TextureFormats.Rgb5A1UNorm, PvrLegacyPixelType.MglArgb1555, hasAlpha: true);
        AddLegacyRead(TextureFormats.Rgb565UNorm, PvrLegacyPixelType.MglRgb565, hasAlpha: false);
        AddLegacyRead(TextureFormats.Rgb5UNorm, PvrLegacyPixelType.MglRgb555, hasAlpha: false);
        AddLegacyRead(TextureFormats.Rgb8, PvrLegacyPixelType.MglRgb888, hasAlpha: false);
        AddLegacyRead(TextureFormats.Rgba8UNorm, PvrLegacyPixelType.MglArgb8888, hasAlpha: true);
        AddLegacyRead(TextureFormats.A8Rgb332UNorm, PvrLegacyPixelType.MglArgb8332, hasAlpha: true);
        AddLegacyRead(TextureFormats.Luminance8UNorm, PvrLegacyPixelType.MglI8, hasAlpha: false);
        AddLegacyRead(TextureFormats.Luminance8Alpha8UNorm, PvrLegacyPixelType.MglAi88, hasAlpha: true);
        AddLegacyRead(TextureFormats.Bw1BppUNorm, PvrLegacyPixelType.MglOneBpp, hasAlpha: false);
        AddLegacyRead(TextureFormats.Vy1Uy0422UNorm, PvrLegacyPixelType.MglVy1Uy0, hasAlpha: false);
        AddLegacyRead(TextureFormats.Y1Vy0U422UNorm, PvrLegacyPixelType.MglY1Vy0U, hasAlpha: false);
        AddLegacyRead(TextureFormats.RgbPvrtcI2BppUNorm, PvrLegacyPixelType.MglPvrtc2, hasAlpha: false);
        AddLegacyRead(TextureFormats.RgbaPvrtcI2BppUNorm, PvrLegacyPixelType.MglPvrtc2, hasAlpha: true);
        AddLegacyRead(TextureFormats.RgbPvrtcI4BppUNorm, PvrLegacyPixelType.MglPvrtc4, hasAlpha: false);
        AddLegacyRead(TextureFormats.RgbaPvrtcI4BppUNorm, PvrLegacyPixelType.MglPvrtc4, hasAlpha: true);
        AddLegacyRead(TextureFormats.Alpha8UNorm, PvrLegacyPixelType.D3dA8, hasAlpha: true);
        AddLegacyRead(TextureFormats.Rg8SNorm, PvrLegacyPixelType.D3dV8U8, hasAlpha: false);
        AddLegacyRead(TextureFormats.Luminance16UNorm, PvrLegacyPixelType.D3dL16, hasAlpha: false);
        AddLegacyRead(TextureFormats.Luminance8UNorm, PvrLegacyPixelType.D3dL8, hasAlpha: false);
        AddLegacyRead(TextureFormats.Luminance8Alpha8UNorm, PvrLegacyPixelType.D3dA8L8, hasAlpha: true);
        AddLegacyRead(TextureFormats.Uyvy422UNorm, PvrLegacyPixelType.D3dUyvy, hasAlpha: false);
        AddLegacyRead(TextureFormats.Yuy2UNorm, PvrLegacyPixelType.D3dYuy2, hasAlpha: false);

        return new Mappings(textureToPvr, pvrToTexture, pvrPixelFormatToTexture, textureToLegacy, textureToPreferredLegacy, textureToExplicitLegacy, legacyToTexture, legacyPixelTypeToTexture, legacyLayoutToTexture);
    }

    private static bool TryGetPvrPixelFormat(ulong pixelFormat, out PvrPixelFormat pvrPixelFormat)
    {
        if (pixelFormat > uint.MaxValue)
        {
            pvrPixelFormat = default;
            return false;
        }

        pvrPixelFormat = (PvrPixelFormat)pixelFormat;
        return Enum.IsDefined(pvrPixelFormat);
    }

    private static byte[] CreateRepeatedBits(int count, byte bits)
    {
        var result = new byte[count];
        Array.Fill(result, bits);
        return result;
    }

    private static ulong MakePixelId(string channels, ReadOnlySpan<byte> bits)
    {
        if (channels.Length is < 1 or > 4 || channels.Length != bits.Length)
        {
            throw new ArgumentException("PVR uncompressed pixel IDs must have one to four channels matching their bit widths.", nameof(channels));
        }

        ulong pixelFormat = 0;
        for (var i = 0; i < channels.Length; i++)
        {
            pixelFormat |= (ulong)(byte)channels[i] << (i * 8);
            pixelFormat |= (ulong)bits[i] << (32 + (i * 8));
        }

        return pixelFormat;
    }

    private static int ReadPositiveInt(ReadOnlySpan<byte> source, string fieldName)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (value == 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"PVR {fieldName} is outside the supported range.");
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
            throw new InvalidDataException("PVR stream ended unexpectedly.", exception);
        }
    }

    private readonly record struct PvrHeader(
        int ContainerVersion,
        ulong PixelFormat,
        uint Flags,
        uint ColourSpace,
        uint ChannelType,
        int Height,
        int Width,
        int Depth,
        int SurfaceCount,
        int FaceCount,
        int MipMapCount,
        uint MetadataSize,
        int PayloadByteCount,
        uint LegacyPixelType,
        bool LegacyHasAlpha,
        uint LegacyBitCount,
        uint LegacyRedMask,
        uint LegacyGreenMask,
        uint LegacyBlueMask,
        uint LegacyAlphaMask);

    private readonly record struct PvrFormatDescriptor(ulong PixelFormat, uint ColourSpace, uint ChannelType);

    private readonly record struct PvrFormatKey(ulong PixelFormat, uint ColourSpace, uint ChannelType);

    private readonly record struct PvrPixelFormatOptionKey(PvrPixelFormat PixelFormat, bool IsSrgb);

    private readonly record struct PvrFormatMapping(TextureFormat TextureFormat, PvrFormatDescriptor Descriptor);

    private readonly record struct LegacyFormatDescriptor(
        PvrLegacyPixelType PixelType,
        uint BitCount,
        uint RedMask,
        uint GreenMask,
        uint BlueMask,
        uint AlphaMask,
        bool HasAlpha);

    private readonly record struct LegacyFormatKey(uint PixelType, bool HasAlpha);

    private readonly record struct LegacyPreferenceKey(TextureFormat TextureFormat, PvrLegacyPixelTypePreference Preference);

    private readonly record struct LegacyExplicitKey(TextureFormat TextureFormat, PvrLegacyPixelType PixelType);

    private readonly record struct LegacyFormatMapping(TextureFormat TextureFormat, LegacyFormatDescriptor Descriptor);

    private readonly record struct LegacyLayoutKey(uint BitCount, uint RedMask, uint GreenMask, uint BlueMask, uint AlphaMask);

    private readonly record struct EncodingSelection(TextureFormat TextureFormat, PvrFormatDescriptor? PvrDescriptor, LegacyFormatDescriptor? LegacyDescriptor);

    private sealed record Mappings(
        Dictionary<TextureFormat, PvrFormatDescriptor> TextureToPvr,
        Dictionary<PvrFormatKey, TextureFormat> PvrToTexture,
        Dictionary<PvrPixelFormatOptionKey, PvrFormatMapping> PvrPixelFormatToTexture,
        Dictionary<TextureFormat, LegacyFormatDescriptor> TextureToLegacy,
        Dictionary<LegacyPreferenceKey, LegacyFormatDescriptor> TextureToPreferredLegacy,
        Dictionary<LegacyExplicitKey, LegacyFormatDescriptor> TextureToExplicitLegacy,
        Dictionary<LegacyFormatKey, TextureFormat> LegacyToTexture,
        Dictionary<PvrLegacyPixelType, LegacyFormatMapping> LegacyPixelTypeToTexture,
        Dictionary<LegacyLayoutKey, TextureFormat> LegacyLayoutToTexture);

    private enum PvrColourSpace : uint
    {
        Linear = 0,
        Srgb = 1
    }

    private enum PvrChannelType : uint
    {
        UnsignedByteNorm = 0,
        SignedByteNorm = 1,
        UnsignedShortNorm = 4,
        SignedShortNorm = 5,
        UnsignedIntegerNorm = 8,
        SignedIntegerNorm = 9,
        SignedFloat = 12,
        UnsignedFloat = 13
    }

    [InlineArray(12)]
    private struct Byte12Buffer
    {
        private byte _element0;
    }

    [InlineArray(HeaderByteCount)]
    private struct Byte52Buffer
    {
        private byte _element0;
    }
}
