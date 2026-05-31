using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.FileFormats.Dds;

public static class DdsCodec
{
    private const int FileHeaderByteCount = 128;
    private const int HeaderByteCount = 124;
    private const int PixelFormatByteCount = 32;
    private const int Dxt10HeaderByteCount = 20;

    private const uint Magic = 0x20534444;
    private const uint FourCcDx10 = 0x30315844;

    private const uint HeaderFlagCaps = 0x00000001;
    private const uint HeaderFlagHeight = 0x00000002;
    private const uint HeaderFlagWidth = 0x00000004;
    private const uint HeaderFlagPitch = 0x00000008;
    private const uint HeaderFlagPixelFormat = 0x00001000;
    private const uint HeaderFlagMipMapCount = 0x00020000;
    private const uint HeaderFlagLinearSize = 0x00080000;
    private const uint HeaderFlagDepth = 0x00800000;

    private const uint PixelFormatFlagAlphaPixels = 0x00000001;
    private const uint PixelFormatFlagAlpha = 0x00000002;
    private const uint PixelFormatFlagFourCc = 0x00000004;
    private const uint PixelFormatFlagRgb = 0x00000040;
    private const uint PixelFormatFlagYuv = 0x00000200;
    private const uint PixelFormatFlagLuminance = 0x00020000;

    private const uint CapsComplex = 0x00000008;
    private const uint CapsTexture = 0x00001000;
    private const uint CapsMipMap = 0x00400000;

    private const uint Caps2CubeMap = 0x00000200;
    private const uint Caps2CubeMapFaces = 0x0000fc00;
    private const uint Caps2Volume = 0x00200000;

    private const uint DdsDimensionTexture2D = 3;
    private const uint DdsResourceMiscTextureCube = 0x00000004;
    private const uint DdsMiscFlags2AlphaModeMask = 0x00000007;

    private static readonly Lazy<Mappings> SFormatMappings = new(CreateFormatMappings);

    public static DdsTexture Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static DdsTexture Read(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Read(stream);
    }

    public static DdsTexture Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ReadHeader(stream);
        ValidateHeader(header);

        TextureFormat format;
        DdsHeaderKind headerKind;
        DdsDxgiFormat? dxgiFormat = null;
        DdsLegacyPixelFormat? legacyPixelFormat = null;
        var alphaMode = DdsAlphaMode.Unknown;
        var arrayLayerCount = 1;
        var faceCount = 1;

        if (IsDxt10Header(header.PixelFormat))
        {
            var dxt10 = ReadDxt10Header(stream);
            ValidateDxt10Header(dxt10);
            format = GetTextureFormat(dxt10.DxgiFormat);
            headerKind = DdsHeaderKind.Dxt10;
            dxgiFormat = dxt10.DxgiFormat;
            alphaMode = GetAlphaMode(dxt10.MiscFlags2);
            arrayLayerCount = GetDxt10ArrayLayerCount(dxt10);
            faceCount = GetDxt10FaceCount(header, dxt10);
        }
        else
        {
            var mapping = GetLegacyTextureMapping(header.PixelFormat);
            format = mapping.TextureFormat;
            headerKind = DdsHeaderKind.Legacy;
            legacyPixelFormat = mapping.LegacyPixelFormat;
            faceCount = GetLegacyFaceCount(header);
        }

        var coder = TextureCoderManager.Global.GetCoder(format);
        var mipLevelCount = GetMipLevelCount(header);
        ValidateBasePitch(header, format);
        ValidateMipLevelCount(header.Width, header.Height, mipLevelCount);
        var subresources = ReadSubresources(stream, coder, header.Width, header.Height, mipLevelCount, arrayLayerCount, faceCount);

        return new DdsTexture(format, subresources, arrayLayerCount, faceCount, headerKind, dxgiFormat, legacyPixelFormat, alphaMode);
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

    public static ArrayBitmap<TPixel> Decode<TPixel>(DdsTexture texture)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        var bitmap = new ArrayBitmap<TPixel>(texture.Texture.Width, texture.Texture.Height);
        var coder = TextureCoderManager.Global.GetCoder(texture.Texture.Format);
        coder.Decode(texture.Texture.Payload, bitmap.AsView());
        return bitmap;
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView());
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, DdsEncodingOptions? options)
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

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, DdsEncodingOptions? options)
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

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, DdsEncodingOptions? options)
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

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, DdsEncodingOptions? options)
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

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, DdsEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, DdsEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var headerKind = GetEncodingHeaderKind(options);
        ValidateEncodingHeaderKind(headerKind);
        var format = GetEncodingTextureFormat(options, headerKind);
        var coder = TextureCoderManager.Global.GetCoder(format);
        if (options?.GenerateMipmaps == true)
        {
            var encodedSubresources = EncodeMipSubresources(BitmapMipChain.Generate(bitmap), coder);
            Write(new DdsTexture(format, encodedSubresources, faceCount: 1), stream, options);
            return;
        }

        var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
        coder.Encode(bitmap, payload);
        Write(new DdsTexture(format, bitmap.Width, bitmap.Height, payload), stream, options);
    }

    public static byte[] EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels)
        where TPixel : unmanaged, IPixel<TPixel> =>
        EncodeMipChain(mipLevels, options: null);

    public static byte[] EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, DdsEncodingOptions? options)
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

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, string path, DdsEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        EncodeMipChain(mipLevels, stream, options);
    }

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        EncodeMipChain(mipLevels, stream, options: null);

    public static void EncodeMipChain<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, Stream stream, DdsEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var headerKind = GetEncodingHeaderKind(options);
        ValidateEncodingHeaderKind(headerKind);
        var format = GetEncodingTextureFormat(options, headerKind);
        var coder = TextureCoderManager.Global.GetCoder(format);
        var encodedSubresources = EncodeMipSubresources(mipLevels, coder);
        Write(new DdsTexture(format, encodedSubresources, faceCount: 1), stream, options);
    }

    public static byte[] Write(DdsTexture texture)
    {
        using var stream = new MemoryStream();
        Write(texture, stream, options: null);
        return stream.ToArray();
    }

    public static byte[] Write(DdsTexture texture, DdsEncodingOptions? options)
    {
        using var stream = new MemoryStream();
        Write(texture, stream, options);
        return stream.ToArray();
    }

    public static void Write(DdsTexture texture, string path)
    {
        using var stream = File.Create(path);
        Write(texture, stream, options: null);
    }

    public static void Write(DdsTexture texture, string path, DdsEncodingOptions? options)
    {
        using var stream = File.Create(path);
        Write(texture, stream, options);
    }

    public static void Write(DdsTexture texture, Stream stream)
    {
        Write(texture, stream, options: null);
    }

    public static void Write(DdsTexture texture, Stream stream, DdsEncodingOptions? options)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        var headerKind = GetEncodingHeaderKind(options);
        ValidateEncodingHeaderKind(headerKind);

        var coder = TextureCoderManager.Global.GetCoder(texture.Texture.Format);
        ValidateTexturePayloads(texture, coder);

        if (texture.Texture.ArrayLayerCount != 1 && headerKind == DdsHeaderKind.Legacy)
        {
            throw new NotSupportedException("DDS texture arrays require a DX10 header.");
        }

        if (headerKind == DdsHeaderKind.Dxt10)
        {
            var dxgiDescriptor = GetDxgiDescriptor(texture.Texture.Format, options);
            WriteHeader(stream, CreateHeader(texture.Texture.Width, texture.Texture.Height, texture.Texture.Format, coder, CreateDxt10PixelFormat(), texture.Texture.MipLevelCount, texture.Texture.FaceCount));
            WriteDxt10Header(stream, dxgiDescriptor.DxgiFormat, GetEncodingAlphaMode(options), texture.Texture.IsCubeMap, texture.Texture.ArrayLayerCount);
        }
        else
        {
            var legacyDescriptor = GetLegacyDescriptor(texture.Texture.Format, options);
            WriteHeader(stream, CreateHeader(texture.Texture.Width, texture.Texture.Height, texture.Texture.Format, coder, legacyDescriptor.PixelFormat, texture.Texture.MipLevelCount, texture.Texture.FaceCount));
        }

        for (var arrayLayer = 0; arrayLayer < texture.Texture.ArrayLayerCount; arrayLayer++)
        {
            for (var face = 0; face < texture.Texture.FaceCount; face++)
            {
                for (var mipLevel = 0; mipLevel < texture.Texture.MipLevelCount; mipLevel++)
                {
                    stream.Write(texture.Texture.GetSubresource(mipLevel, arrayLayer, face).Payload);
                }
            }
        }
    }

    private static DdsHeader ReadHeader(Stream stream)
    {
        Byte128Buffer fileHeaderBuffer = default;
        Span<byte> fileHeader = fileHeaderBuffer;
        ReadExactly(stream, fileHeader);

        if (BinaryPrimitives.ReadUInt32LittleEndian(fileHeader) != Magic)
        {
            throw new InvalidDataException("The stream is not a DDS file.");
        }

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(4, 4));
        if (headerSize != HeaderByteCount)
        {
            throw new InvalidDataException($"DDS header size is {headerSize}, but {HeaderByteCount} was expected.");
        }

        var pixelFormatSize = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(76, 4));
        if (pixelFormatSize != PixelFormatByteCount)
        {
            throw new InvalidDataException($"DDS pixel format size is {pixelFormatSize}, but {PixelFormatByteCount} was expected.");
        }

        return new DdsHeader(
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(8, 4)),
            ReadPositiveInt(fileHeader.Slice(16, 4), "width"),
            ReadPositiveInt(fileHeader.Slice(12, 4), "height"),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(20, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(24, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(28, 4)),
            new DdsPixelFormat(
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(80, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(84, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(88, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(92, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(96, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(100, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(104, 4))),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(108, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(112, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(116, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(fileHeader.Slice(120, 4)));
    }

    private static DdsDxt10Header ReadDxt10Header(Stream stream)
    {
        Byte20Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        ReadExactly(stream, buffer);
        return new DdsDxt10Header(
            (DdsDxgiFormat)BinaryPrimitives.ReadUInt32LittleEndian(buffer),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(12, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(16, 4)));
    }

    private static void WriteHeader(Stream stream, DdsHeader header)
    {
        Byte128Buffer fileHeaderBuffer = default;
        Span<byte> fileHeader = fileHeaderBuffer;
        fileHeader.Clear();

        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(4, 4), HeaderByteCount);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(8, 4), header.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(12, 4), checked((uint)header.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(16, 4), checked((uint)header.Width));
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(20, 4), header.PitchOrLinearSize);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(24, 4), header.Depth);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(28, 4), header.MipMapCount);

        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(76, 4), PixelFormatByteCount);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(80, 4), header.PixelFormat.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(84, 4), header.PixelFormat.FourCc);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(88, 4), header.PixelFormat.RgbBitCount);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(92, 4), header.PixelFormat.RedMask);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(96, 4), header.PixelFormat.GreenMask);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(100, 4), header.PixelFormat.BlueMask);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(104, 4), header.PixelFormat.AlphaMask);

        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(108, 4), header.Caps);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(112, 4), header.Caps2);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(116, 4), header.Caps3);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader.Slice(120, 4), header.Caps4);

        stream.Write(fileHeader);
    }

    private static void WriteDxt10Header(
        Stream stream,
        DdsDxgiFormat dxgiFormat,
        DdsAlphaMode alphaMode,
        bool isCubeMap,
        int arrayLayerCount)
    {
        Byte20Buffer bufferStorage = default;
        Span<byte> buffer = bufferStorage;
        buffer.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)dxgiFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), DdsDimensionTexture2D);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), isCubeMap ? DdsResourceMiscTextureCube : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), checked((uint)arrayLayerCount));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), (uint)alphaMode);
        stream.Write(buffer);
    }

    private static DdsHeader CreateHeader(
        int width,
        int height,
        TextureFormat format,
        ITextureCoder coder,
        DdsPixelFormat pixelFormat,
        int mipLevelCount,
        int faceCount)
    {
        var isCompressed = format.Kind == TextureFormatKind.BlockCompressed;
        var isCubeMap = faceCount == 6;
        var flags = HeaderFlagCaps | HeaderFlagHeight | HeaderFlagWidth | HeaderFlagPixelFormat
            | (isCompressed ? HeaderFlagLinearSize : HeaderFlagPitch)
            | (mipLevelCount > 1 ? HeaderFlagMipMapCount : 0);
        var pitchOrLinearSize = isCompressed
            ? checked((uint)coder.GetEncodedByteCount(width, height))
            : checked((uint)format.GetRowByteCount(width));
        var caps = CapsTexture
            | (mipLevelCount > 1 ? CapsComplex | CapsMipMap : 0)
            | (isCubeMap ? CapsComplex : 0);
        var caps2 = isCubeMap ? Caps2CubeMap | Caps2CubeMapFaces : 0;

        return new DdsHeader(
            flags,
            width,
            height,
            pitchOrLinearSize,
            Depth: 0,
            MipMapCount: mipLevelCount > 1 ? checked((uint)mipLevelCount) : 0,
            pixelFormat,
            caps,
            Caps2: caps2,
            Caps3: 0,
            Caps4: 0);
    }

    private static void ValidateHeader(DdsHeader header)
    {
        if ((header.Flags & (HeaderFlagCaps | HeaderFlagHeight | HeaderFlagWidth | HeaderFlagPixelFormat))
            != (HeaderFlagCaps | HeaderFlagHeight | HeaderFlagWidth | HeaderFlagPixelFormat))
        {
            throw new InvalidDataException("DDS header is missing required flags.");
        }

        if ((header.Caps & CapsTexture) == 0)
        {
            throw new InvalidDataException("DDS header is missing the texture capability.");
        }

        if ((header.Caps2 & Caps2Volume) != 0 || ((header.Flags & HeaderFlagDepth) != 0 && header.Depth > 1))
        {
            throw new NotSupportedException("DDS volume textures are not supported.");
        }
    }

    private static void ValidateDxt10Header(DdsDxt10Header header)
    {
        if (header.ResourceDimension != DdsDimensionTexture2D)
        {
            throw new NotSupportedException($"DDS DX10 resource dimension {header.ResourceDimension} is not supported.");
        }

        if (header.ArraySize == 0 || header.ArraySize > int.MaxValue)
        {
            throw new InvalidDataException("DDS DX10 array size is outside the supported range.");
        }

        if ((header.MiscFlag & ~DdsResourceMiscTextureCube) != 0)
        {
            throw new NotSupportedException($"DDS DX10 misc flag value 0x{header.MiscFlag:x8} is not supported.");
        }

        if ((header.MiscFlags2 & ~DdsMiscFlags2AlphaModeMask) != 0)
        {
            throw new NotSupportedException($"DDS DX10 misc flags 2 value 0x{header.MiscFlags2:x8} is not supported.");
        }

        var alphaMode = GetAlphaMode(header.MiscFlags2);
        if (alphaMode > DdsAlphaMode.Custom)
        {
            throw new NotSupportedException($"DDS DX10 alpha mode {(uint)alphaMode} is not supported.");
        }
    }

    private static int GetDxt10ArrayLayerCount(DdsDxt10Header header) => (int)header.ArraySize;

    private static int GetMipLevelCount(DdsHeader header)
    {
        if (header.MipMapCount == 0)
        {
            return 1;
        }

        if (header.MipMapCount > int.MaxValue)
        {
            throw new InvalidDataException("DDS mip-map count is outside the supported range.");
        }

        return (int)header.MipMapCount;
    }

    private static void ValidateMipLevelCount(int width, int height, int mipLevelCount)
    {
        if (mipLevelCount > TextureImage.GetFullMipLevelCount(width, height))
        {
            throw new InvalidDataException("DDS mip-map count exceeds the full mip chain for the base dimensions.");
        }
    }

    private static int GetDxt10FaceCount(DdsHeader header, DdsDxt10Header dxt10)
    {
        var faceFlags = header.Caps2 & Caps2CubeMapFaces;
        if (faceFlags != 0 && faceFlags != Caps2CubeMapFaces)
        {
            throw new NotSupportedException("DDS partial cube maps are not supported.");
        }

        return (dxt10.MiscFlag & DdsResourceMiscTextureCube) != 0 || (header.Caps2 & Caps2CubeMap) != 0 || faceFlags != 0
            ? 6
            : 1;
    }

    private static int GetLegacyFaceCount(DdsHeader header)
    {
        if ((header.Caps2 & (Caps2CubeMap | Caps2CubeMapFaces)) == 0)
        {
            return 1;
        }

        if ((header.Caps2 & Caps2CubeMap) == 0 || (header.Caps2 & Caps2CubeMapFaces) != Caps2CubeMapFaces)
        {
            throw new NotSupportedException("DDS partial cube maps are not supported.");
        }

        return 6;
    }

    private static TextureSubresource[] ReadSubresources(
        Stream stream,
        ITextureCoder coder,
        int baseWidth,
        int baseHeight,
        int mipLevelCount,
        int arrayLayerCount,
        int faceCount)
    {
        var subresources = new TextureSubresource[checked(mipLevelCount * arrayLayerCount * faceCount)];
        var index = 0;
        for (var arrayLayer = 0; arrayLayer < arrayLayerCount; arrayLayer++)
        {
            for (var face = 0; face < faceCount; face++)
            {
                for (var mipLevel = 0; mipLevel < mipLevelCount; mipLevel++)
                {
                    var width = TextureImage.GetMipDimension(baseWidth, mipLevel);
                    var height = TextureImage.GetMipDimension(baseHeight, mipLevel);
                    var payload = new byte[coder.GetEncodedByteCount(width, height)];
                    ReadExactly(stream, payload);
                    subresources[index++] = new TextureSubresource(mipLevel, arrayLayer, face, width, height, payload);
                }
            }
        }

        return subresources;
    }

    private static void ValidateBasePitch(DdsHeader header, TextureFormat format)
    {
        if (format.Kind == TextureFormatKind.Uncompressed
            && (header.Flags & HeaderFlagPitch) != 0
            && header.PitchOrLinearSize != 0)
        {
            var defaultPitch = format.GetRowByteCount(header.Width);
            if (header.PitchOrLinearSize != defaultPitch)
            {
                throw new NotSupportedException(
                    $"DDS row pitch {header.PitchOrLinearSize} is not supported for '{format.Name}'; expected {defaultPitch}.");
            }
        }
    }

    private static TextureSubresource[] EncodeMipSubresources<TPixel>(IReadOnlyList<IBitmap<TPixel>> mipLevels, ITextureCoder coder)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(mipLevels);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException("DDS texture must contain at least one mip level.", nameof(mipLevels));
        }

        var baseLevel = mipLevels[0] ?? throw new ArgumentException("DDS mip level cannot be null.", nameof(mipLevels));
        if (mipLevels.Count > TextureImage.GetFullMipLevelCount(baseLevel.Width, baseLevel.Height))
        {
            throw new ArgumentException("DDS mip level count exceeds the full mip chain for the base dimensions.", nameof(mipLevels));
        }

        var subresources = new TextureSubresource[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var bitmap = mipLevels[i] ?? throw new ArgumentException("DDS mip level cannot be null.", nameof(mipLevels));
            var expectedWidth = TextureImage.GetMipDimension(baseLevel.Width, i);
            var expectedHeight = TextureImage.GetMipDimension(baseLevel.Height, i);
            if (bitmap.Width != expectedWidth || bitmap.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"DDS mip level {i} is {bitmap.Width}x{bitmap.Height}, but {expectedWidth}x{expectedHeight} was expected.",
                    nameof(mipLevels));
            }

            var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
            coder.Encode(bitmap.AsView(), payload);
            subresources[i] = new TextureSubresource(i, arrayLayer: 0, faceIndex: 0, bitmap.Width, bitmap.Height, payload);
        }

        return subresources;
    }

    private static void ValidateTexturePayloads(DdsTexture texture, ITextureCoder coder)
    {
        if (texture.Texture.MipLevelCount > TextureImage.GetFullMipLevelCount(texture.Texture.Width, texture.Texture.Height))
        {
            throw new ArgumentException("DDS mip level count exceeds the full mip chain for the base dimensions.", nameof(texture));
        }

        foreach (var subresource in texture.Texture.Subresources)
        {
            var expectedByteCount = coder.GetEncodedByteCount(subresource.Width, subresource.Height);
            if (subresource.Payload.Length != expectedByteCount)
            {
                throw new ArgumentException(
                    $"DDS subresource mip level {subresource.MipLevel}, array layer {subresource.ArrayLayer}, face {subresource.FaceIndex} payload length is {subresource.Payload.Length} bytes, but '{texture.Texture.Format.Name}' expects {expectedByteCount} bytes for {subresource.Width}x{subresource.Height}.",
                    nameof(texture));
            }
        }

    }

    private static bool IsDxt10Header(DdsPixelFormat pixelFormat) =>
        (pixelFormat.Flags & PixelFormatFlagFourCc) != 0 && pixelFormat.FourCc == FourCcDx10;

    private static DdsPixelFormat CreateDxt10PixelFormat() =>
        new(PixelFormatFlagFourCc, FourCcDx10, RgbBitCount: 0, RedMask: 0, GreenMask: 0, BlueMask: 0, AlphaMask: 0);

    private static TextureFormat GetTextureFormat(DdsDxgiFormat dxgiFormat)
    {
        if (SFormatMappings.Value.DxgiToTexture.TryGetValue(dxgiFormat, out var mapping))
        {
            return mapping.TextureFormat;
        }

        throw new NotSupportedException($"Unsupported DDS DXGI format '{dxgiFormat}' ({(uint)dxgiFormat}).");
    }

    private static LegacyFormatMapping GetLegacyTextureMapping(DdsPixelFormat pixelFormat)
    {
        var key = GetLegacyFormatKey(pixelFormat);
        if (SFormatMappings.Value.LegacyToTexture.TryGetValue(key, out var mapping))
        {
            return mapping;
        }

        throw new NotSupportedException(
            $"Unsupported DDS legacy pixel format flags 0x{pixelFormat.Flags:x8}, FourCC 0x{pixelFormat.FourCc:x8}, bit count {pixelFormat.RgbBitCount}, masks 0x{pixelFormat.RedMask:x8}/0x{pixelFormat.GreenMask:x8}/0x{pixelFormat.BlueMask:x8}/0x{pixelFormat.AlphaMask:x8}.");
    }

    private static DdsDxgiDescriptor GetDxgiDescriptor(TextureFormat format, DdsEncodingOptions? options)
    {
        if (options?.TextureFormat is { } optionFormat && optionFormat != format)
        {
            throw new ArgumentException(
                $"DDS encoding options specify texture format '{optionFormat.Name}', but the texture payload uses '{format.Name}'.",
                nameof(options));
        }

        if (options?.TextureFormat is null && options?.DxgiFormat is { } dxgiFormat)
        {
            var selection = GetDxgiEncodingSelection(dxgiFormat);
            if (selection.TextureFormat != format)
            {
                throw new ArgumentException(
                    $"DDS encoding options specify DXGI format '{dxgiFormat}', which maps to '{selection.TextureFormat.Name}', but the texture payload uses '{format.Name}'.",
                    nameof(options));
            }

            return selection.DxgiDescriptor!.Value;
        }

        if (SFormatMappings.Value.TextureToDxgi.TryGetValue(format, out var descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException($"Texture format '{format.Name}' cannot be written with a DDS DX10 header.");
    }

    private static LegacyFormatDescriptor GetLegacyDescriptor(TextureFormat format, DdsEncodingOptions? options)
    {
        if (options?.TextureFormat is { } optionFormat && optionFormat != format)
        {
            throw new ArgumentException(
                $"DDS encoding options specify texture format '{optionFormat.Name}', but the texture payload uses '{format.Name}'.",
                nameof(options));
        }

        if (options?.TextureFormat is null && options?.LegacyPixelFormat is { } legacyPixelFormat)
        {
            var selection = GetLegacyEncodingSelection(legacyPixelFormat);
            if (selection.TextureFormat != format)
            {
                throw new ArgumentException(
                    $"DDS encoding options specify legacy pixel format '{legacyPixelFormat}', which maps to '{selection.TextureFormat.Name}', but the texture payload uses '{format.Name}'.",
                    nameof(options));
            }

            return selection.LegacyDescriptor!.Value;
        }

        if (SFormatMappings.Value.TextureToLegacy.TryGetValue(format, out var descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException($"Texture format '{format.Name}' cannot be written with a DDS legacy header.");
    }

    private static TextureFormat GetEncodingTextureFormat(DdsEncodingOptions? options, DdsHeaderKind headerKind)
    {
        if (options?.TextureFormat is { } textureFormat)
        {
            return textureFormat;
        }

        if (headerKind == DdsHeaderKind.Dxt10 && options?.DxgiFormat is { } dxgiFormat)
        {
            return GetDxgiEncodingSelection(dxgiFormat).TextureFormat;
        }

        if (headerKind == DdsHeaderKind.Legacy && options?.LegacyPixelFormat is { } legacyPixelFormat)
        {
            return GetLegacyEncodingSelection(legacyPixelFormat).TextureFormat;
        }

        return TextureFormats.Rgba8UNorm;
    }

    private static EncodingSelection GetDxgiEncodingSelection(DdsDxgiFormat dxgiFormat)
    {
        if (SFormatMappings.Value.DxgiToTexture.TryGetValue(dxgiFormat, out var mapping))
        {
            return new EncodingSelection(mapping.TextureFormat, mapping.DxgiDescriptor, LegacyDescriptor: null);
        }

        throw new NotSupportedException($"DDS DXGI format '{dxgiFormat}' ({(uint)dxgiFormat}) cannot be used for encoding.");
    }

    private static EncodingSelection GetLegacyEncodingSelection(DdsLegacyPixelFormat legacyPixelFormat)
    {
        if (SFormatMappings.Value.LegacyPixelFormatToTexture.TryGetValue(legacyPixelFormat, out var mapping))
        {
            return new EncodingSelection(mapping.TextureFormat, DxgiDescriptor: null, mapping.LegacyDescriptor);
        }

        throw new NotSupportedException($"DDS legacy pixel format '{legacyPixelFormat}' cannot be used for encoding.");
    }

    private static DdsHeaderKind GetEncodingHeaderKind(DdsEncodingOptions? options) =>
        options?.HeaderKind ?? DdsHeaderKind.Dxt10;

    private static DdsAlphaMode GetEncodingAlphaMode(DdsEncodingOptions? options) =>
        options?.AlphaMode ?? DdsAlphaMode.Unknown;

    private static void ValidateEncodingHeaderKind(DdsHeaderKind headerKind)
    {
        if (headerKind is not (DdsHeaderKind.Dxt10 or DdsHeaderKind.Legacy))
        {
            throw new ArgumentOutOfRangeException(nameof(headerKind), "DDS header kind must be Dxt10 or Legacy.");
        }
    }

    private static DdsAlphaMode GetAlphaMode(uint miscFlags2) =>
        (DdsAlphaMode)(miscFlags2 & DdsMiscFlags2AlphaModeMask);

    private static Mappings CreateFormatMappings()
    {
        var textureToDxgi = new Dictionary<TextureFormat, DdsDxgiDescriptor>();
        var dxgiToTexture = new Dictionary<DdsDxgiFormat, DxgiFormatMapping>();
        var textureToLegacy = new Dictionary<TextureFormat, LegacyFormatDescriptor>();
        var legacyToTexture = new Dictionary<LegacyFormatKey, LegacyFormatMapping>();
        var legacyPixelFormatToTexture = new Dictionary<DdsLegacyPixelFormat, LegacyFormatMapping>();

        void AddDxgi(TextureFormat format, DdsDxgiFormat dxgiFormat)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            var descriptor = new DdsDxgiDescriptor(dxgiFormat);
            textureToDxgi.TryAdd(format, descriptor);
            dxgiToTexture.TryAdd(dxgiFormat, new DxgiFormatMapping(format, descriptor));
        }

        void AddLegacy(TextureFormat format, DdsLegacyPixelFormat legacyPixelFormat, LegacyFormatDescriptor descriptor)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            textureToLegacy.TryAdd(format, descriptor);

            var mapping = new LegacyFormatMapping(format, descriptor, legacyPixelFormat);
            legacyPixelFormatToTexture.TryAdd(legacyPixelFormat, mapping);
            legacyToTexture.TryAdd(GetLegacyFormatKey(descriptor.PixelFormat), mapping);
        }

        void AddLegacyAlias(TextureFormat format, LegacyFormatDescriptor descriptor)
        {
            if (!TextureCoderManager.Global.TryGetCoder(format, out _))
            {
                return;
            }

            textureToLegacy.TryAdd(format, descriptor);
            legacyToTexture.TryAdd(GetLegacyFormatKey(descriptor.PixelFormat), new LegacyFormatMapping(format, descriptor, LegacyPixelFormat: null));
        }

        LegacyFormatDescriptor LegacyLayout(
            uint flags,
            uint rgbBitCount,
            uint redMask,
            uint greenMask,
            uint blueMask,
            uint alphaMask = 0) =>
            new(new DdsPixelFormat(flags, FourCc: 0, rgbBitCount, redMask, greenMask, blueMask, alphaMask));

        LegacyFormatDescriptor LegacyFourCc(uint fourCc) =>
            new(new DdsPixelFormat(PixelFormatFlagFourCc, fourCc, RgbBitCount: 0, RedMask: 0, GreenMask: 0, BlueMask: 0, AlphaMask: 0));

        AddDxgi(TextureFormats.Rgba32Float, DdsDxgiFormat.R32G32B32A32Float);
        AddDxgi(TextureFormats.Rgba32UInt, DdsDxgiFormat.R32G32B32A32UInt);
        AddDxgi(TextureFormats.Rgba32SInt, DdsDxgiFormat.R32G32B32A32SInt);
        AddDxgi(TextureFormats.Rgb32Float, DdsDxgiFormat.R32G32B32Float);
        AddDxgi(TextureFormats.Rgb32UInt, DdsDxgiFormat.R32G32B32UInt);
        AddDxgi(TextureFormats.Rgb32SInt, DdsDxgiFormat.R32G32B32SInt);
        AddDxgi(TextureFormats.Rgba16Float, DdsDxgiFormat.R16G16B16A16Float);
        AddDxgi(TextureFormats.Rgba16UNorm, DdsDxgiFormat.R16G16B16A16UNorm);
        AddDxgi(TextureFormats.Rgba16UInt, DdsDxgiFormat.R16G16B16A16UInt);
        AddDxgi(TextureFormats.Rgba16SNorm, DdsDxgiFormat.R16G16B16A16SNorm);
        AddDxgi(TextureFormats.Rgba16SInt, DdsDxgiFormat.R16G16B16A16SInt);
        AddDxgi(TextureFormats.Rg32Float, DdsDxgiFormat.R32G32Float);
        AddDxgi(TextureFormats.Rg32UInt, DdsDxgiFormat.R32G32UInt);
        AddDxgi(TextureFormats.Rg32SInt, DdsDxgiFormat.R32G32SInt);
        AddDxgi(TextureFormats.Rgb10A2RevUNorm, DdsDxgiFormat.R10G10B10A2UNorm);
        AddDxgi(TextureFormats.Rgb10A2RevUInt, DdsDxgiFormat.R10G10B10A2UInt);
        AddDxgi(TextureFormats.R11G11B10Float, DdsDxgiFormat.R11G11B10Float);
        AddDxgi(TextureFormats.Rgba8UNorm, DdsDxgiFormat.R8G8B8A8UNorm);
        AddDxgi(TextureFormats.Rgba8Srgb, DdsDxgiFormat.R8G8B8A8UNormSrgb);
        AddDxgi(TextureFormats.Rgba8UInt, DdsDxgiFormat.R8G8B8A8UInt);
        AddDxgi(TextureFormats.Rgba8SNorm, DdsDxgiFormat.R8G8B8A8SNorm);
        AddDxgi(TextureFormats.Rgba8SInt, DdsDxgiFormat.R8G8B8A8SInt);
        AddDxgi(TextureFormats.Rg16Float, DdsDxgiFormat.R16G16Float);
        AddDxgi(TextureFormats.Rg16UNorm, DdsDxgiFormat.R16G16UNorm);
        AddDxgi(TextureFormats.Rg16UInt, DdsDxgiFormat.R16G16UInt);
        AddDxgi(TextureFormats.Rg16SNorm, DdsDxgiFormat.R16G16SNorm);
        AddDxgi(TextureFormats.Rg16SInt, DdsDxgiFormat.R16G16SInt);
        AddDxgi(TextureFormats.R32Float, DdsDxgiFormat.R32Float);
        AddDxgi(TextureFormats.R32UInt, DdsDxgiFormat.R32UInt);
        AddDxgi(TextureFormats.R32SInt, DdsDxgiFormat.R32SInt);
        AddDxgi(TextureFormats.Rg8, DdsDxgiFormat.R8G8UNorm);
        AddDxgi(TextureFormats.Rg8UInt, DdsDxgiFormat.R8G8UInt);
        AddDxgi(TextureFormats.Rg8SNorm, DdsDxgiFormat.R8G8SNorm);
        AddDxgi(TextureFormats.Rg8SInt, DdsDxgiFormat.R8G8SInt);
        AddDxgi(TextureFormats.R16Float, DdsDxgiFormat.R16Float);
        AddDxgi(TextureFormats.R16UNorm, DdsDxgiFormat.R16UNorm);
        AddDxgi(TextureFormats.R16UInt, DdsDxgiFormat.R16UInt);
        AddDxgi(TextureFormats.R16SNorm, DdsDxgiFormat.R16SNorm);
        AddDxgi(TextureFormats.R16SInt, DdsDxgiFormat.R16SInt);
        AddDxgi(TextureFormats.R8, DdsDxgiFormat.R8UNorm);
        AddDxgi(TextureFormats.R8UInt, DdsDxgiFormat.R8UInt);
        AddDxgi(TextureFormats.R8SNorm, DdsDxgiFormat.R8SNorm);
        AddDxgi(TextureFormats.R8SInt, DdsDxgiFormat.R8SInt);
        AddDxgi(TextureFormats.Alpha8UNorm, DdsDxgiFormat.A8UNorm);
        AddDxgi(TextureFormats.Bw1BppUNorm, DdsDxgiFormat.R1UNorm);
        AddDxgi(TextureFormats.Rgb9E5, DdsDxgiFormat.R9G9B9E5SharedExp);
        AddDxgi(TextureFormats.R8G8B8G8_422UNorm, DdsDxgiFormat.R8G8B8G8UNorm);
        AddDxgi(TextureFormats.G8R8G8B8_422UNorm, DdsDxgiFormat.G8R8G8B8UNorm);
        AddDxgi(TextureFormats.Bc1Rgba, DdsDxgiFormat.BC1UNorm);
        AddDxgi(TextureFormats.Bc1Rgb, DdsDxgiFormat.BC1UNorm);
        AddDxgi(TextureFormats.Bc1RgbaSrgb, DdsDxgiFormat.BC1UNormSrgb);
        AddDxgi(TextureFormats.Bc1RgbSrgb, DdsDxgiFormat.BC1UNormSrgb);
        AddDxgi(TextureFormats.Dxt1Rgba, DdsDxgiFormat.BC1UNorm);
        AddDxgi(TextureFormats.Dxt1Rgb, DdsDxgiFormat.BC1UNorm);
        AddDxgi(TextureFormats.Dxt1RgbaSrgb, DdsDxgiFormat.BC1UNormSrgb);
        AddDxgi(TextureFormats.Dxt1RgbSrgb, DdsDxgiFormat.BC1UNormSrgb);
        AddDxgi(TextureFormats.Bc2Rgba, DdsDxgiFormat.BC2UNorm);
        AddDxgi(TextureFormats.Bc2RgbaSrgb, DdsDxgiFormat.BC2UNormSrgb);
        AddDxgi(TextureFormats.Dxt3Rgba, DdsDxgiFormat.BC2UNorm);
        AddDxgi(TextureFormats.Dxt3RgbaSrgb, DdsDxgiFormat.BC2UNormSrgb);
        AddDxgi(TextureFormats.Bc3Rgba, DdsDxgiFormat.BC3UNorm);
        AddDxgi(TextureFormats.Bc3RgbaSrgb, DdsDxgiFormat.BC3UNormSrgb);
        AddDxgi(TextureFormats.Dxt5Rgba, DdsDxgiFormat.BC3UNorm);
        AddDxgi(TextureFormats.Dxt5RgbaSrgb, DdsDxgiFormat.BC3UNormSrgb);
        AddDxgi(TextureFormats.Bc4UNorm, DdsDxgiFormat.BC4UNorm);
        AddDxgi(TextureFormats.Bc4SNorm, DdsDxgiFormat.BC4SNorm);
        AddDxgi(TextureFormats.Rgtc1UNorm, DdsDxgiFormat.BC4UNorm);
        AddDxgi(TextureFormats.Rgtc1SNorm, DdsDxgiFormat.BC4SNorm);
        AddDxgi(TextureFormats.Ati1UNorm, DdsDxgiFormat.BC4UNorm);
        AddDxgi(TextureFormats.Ati1SNorm, DdsDxgiFormat.BC4SNorm);
        AddDxgi(TextureFormats.Bc5UNorm, DdsDxgiFormat.BC5UNorm);
        AddDxgi(TextureFormats.Bc5SNorm, DdsDxgiFormat.BC5SNorm);
        AddDxgi(TextureFormats.Rgtc2UNorm, DdsDxgiFormat.BC5UNorm);
        AddDxgi(TextureFormats.Rgtc2SNorm, DdsDxgiFormat.BC5SNorm);
        AddDxgi(TextureFormats.Ati2UNorm, DdsDxgiFormat.BC5UNorm);
        AddDxgi(TextureFormats.Ati2SNorm, DdsDxgiFormat.BC5SNorm);
        AddDxgi(TextureFormats.Rgb565UNorm, DdsDxgiFormat.B5G6R5UNorm);
        AddDxgi(TextureFormats.A1Rgb5UNorm, DdsDxgiFormat.B5G5R5A1UNorm);
        AddDxgi(TextureFormats.Bgra8, DdsDxgiFormat.B8G8R8A8UNorm);
        AddDxgi(TextureFormats.Bgrx8UNorm, DdsDxgiFormat.B8G8R8X8UNorm);
        AddDxgi(TextureFormats.Rgb10XRA2UNorm, DdsDxgiFormat.R10G10B10XRBiasA2UNorm);
        AddDxgi(TextureFormats.Bgra8Srgb, DdsDxgiFormat.B8G8R8A8UNormSrgb);
        AddDxgi(TextureFormats.Bgrx8Srgb, DdsDxgiFormat.B8G8R8X8UNormSrgb);
        AddDxgi(TextureFormats.Bc6HUFloat, DdsDxgiFormat.BC6HUFloat16);
        AddDxgi(TextureFormats.Bc6HSFloat, DdsDxgiFormat.BC6HSFloat16);
        AddDxgi(TextureFormats.RgbBptcUFloat, DdsDxgiFormat.BC6HUFloat16);
        AddDxgi(TextureFormats.RgbBptcSFloat, DdsDxgiFormat.BC6HSFloat16);
        AddDxgi(TextureFormats.Bc7UNorm, DdsDxgiFormat.BC7UNorm);
        AddDxgi(TextureFormats.Bc7Srgb, DdsDxgiFormat.BC7UNormSrgb);
        AddDxgi(TextureFormats.RgbaBptcUNorm, DdsDxgiFormat.BC7UNorm);
        AddDxgi(TextureFormats.RgbaBptcSrgb, DdsDxgiFormat.BC7UNormSrgb);
        AddDxgi(TextureFormats.Ayuv444UNorm, DdsDxgiFormat.AYUV);
        AddDxgi(TextureFormats.Yuy2UNorm, DdsDxgiFormat.YUY2);
        AddDxgi(TextureFormats.Bgra4UNorm, DdsDxgiFormat.B4G4R4A4UNorm);

        AddLegacy(
            TextureFormats.Rgba8UNorm,
            DdsLegacyPixelFormat.Rgba8UNorm,
            LegacyLayout(PixelFormatFlagRgb | PixelFormatFlagAlphaPixels, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000));
        AddLegacy(
            TextureFormats.Bgra8,
            DdsLegacyPixelFormat.Bgra8UNorm,
            LegacyLayout(PixelFormatFlagRgb | PixelFormatFlagAlphaPixels, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000));
        AddLegacy(
            TextureFormats.Bgrx8UNorm,
            DdsLegacyPixelFormat.Bgrx8UNorm,
            LegacyLayout(PixelFormatFlagRgb, 32, 0x00ff0000, 0x0000ff00, 0x000000ff));
        AddLegacy(
            TextureFormats.Rgb8,
            DdsLegacyPixelFormat.Rgb8UNorm,
            LegacyLayout(PixelFormatFlagRgb, 24, 0x000000ff, 0x0000ff00, 0x00ff0000));
        AddLegacy(
            TextureFormats.Bgr8UNorm,
            DdsLegacyPixelFormat.Bgr8UNorm,
            LegacyLayout(PixelFormatFlagRgb, 24, 0x00ff0000, 0x0000ff00, 0x000000ff));
        AddLegacy(
            TextureFormats.Rgb565UNorm,
            DdsLegacyPixelFormat.Rgb565UNorm,
            LegacyLayout(PixelFormatFlagRgb, 16, 0x0000f800, 0x000007e0, 0x0000001f));
        AddLegacy(
            TextureFormats.Bgr565UNorm,
            DdsLegacyPixelFormat.Bgr565UNorm,
            LegacyLayout(PixelFormatFlagRgb, 16, 0x0000001f, 0x000007e0, 0x0000f800));
        AddLegacy(
            TextureFormats.A1Rgb5UNorm,
            DdsLegacyPixelFormat.A1Rgb5UNorm,
            LegacyLayout(PixelFormatFlagRgb | PixelFormatFlagAlphaPixels, 16, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000));
        AddLegacy(
            TextureFormats.Rgb5A1UNorm,
            DdsLegacyPixelFormat.Rgb5A1UNorm,
            LegacyLayout(PixelFormatFlagRgb | PixelFormatFlagAlphaPixels, 16, 0x0000f800, 0x000007c0, 0x0000003e, 0x00000001));
        AddLegacy(
            TextureFormats.Rgba4UNorm,
            DdsLegacyPixelFormat.Rgba4UNorm,
            LegacyLayout(PixelFormatFlagRgb | PixelFormatFlagAlphaPixels, 16, 0x0000f000, 0x00000f00, 0x000000f0, 0x0000000f));
        AddLegacy(
            TextureFormats.Alpha8UNorm,
            DdsLegacyPixelFormat.Alpha8UNorm,
            LegacyLayout(PixelFormatFlagAlpha, 8, redMask: 0, greenMask: 0, blueMask: 0, alphaMask: 0x000000ff));
        AddLegacy(
            TextureFormats.Luminance8UNorm,
            DdsLegacyPixelFormat.Luminance8UNorm,
            LegacyLayout(PixelFormatFlagLuminance, 8, 0x000000ff, greenMask: 0, blueMask: 0));
        AddLegacy(
            TextureFormats.Luminance8Alpha8UNorm,
            DdsLegacyPixelFormat.Luminance8Alpha8UNorm,
            LegacyLayout(PixelFormatFlagLuminance | PixelFormatFlagAlphaPixels, 16, 0x000000ff, greenMask: 0, blueMask: 0, alphaMask: 0x0000ff00));

        var dxt1 = LegacyFourCc(MakeFourCc("DXT1"));
        AddLegacy(TextureFormats.Bc1Rgba, DdsLegacyPixelFormat.Dxt1, dxt1);
        AddLegacyAlias(TextureFormats.Bc1Rgb, dxt1);
        AddLegacyAlias(TextureFormats.Dxt1Rgba, dxt1);
        AddLegacyAlias(TextureFormats.Dxt1Rgb, dxt1);

        AddLegacy(TextureFormats.Dxt2Rgba, DdsLegacyPixelFormat.Dxt2, LegacyFourCc(MakeFourCc("DXT2")));

        var dxt3 = LegacyFourCc(MakeFourCc("DXT3"));
        AddLegacy(TextureFormats.Bc2Rgba, DdsLegacyPixelFormat.Dxt3, dxt3);
        AddLegacyAlias(TextureFormats.Dxt3Rgba, dxt3);

        AddLegacy(TextureFormats.Dxt4Rgba, DdsLegacyPixelFormat.Dxt4, LegacyFourCc(MakeFourCc("DXT4")));

        var dxt5 = LegacyFourCc(MakeFourCc("DXT5"));
        AddLegacy(TextureFormats.Bc3Rgba, DdsLegacyPixelFormat.Dxt5, dxt5);
        AddLegacyAlias(TextureFormats.Dxt5Rgba, dxt5);

        var ati1 = LegacyFourCc(MakeFourCc("ATI1"));
        AddLegacy(TextureFormats.Bc4UNorm, DdsLegacyPixelFormat.Ati1, ati1);
        AddLegacyAlias(TextureFormats.Rgtc1UNorm, ati1);
        AddLegacyAlias(TextureFormats.Ati1UNorm, ati1);
        AddLegacy(TextureFormats.Bc4UNorm, DdsLegacyPixelFormat.Bc4UNorm, LegacyFourCc(MakeFourCc("BC4U")));
        AddLegacy(TextureFormats.Bc4SNorm, DdsLegacyPixelFormat.Bc4SNorm, LegacyFourCc(MakeFourCc("BC4S")));

        var ati2 = LegacyFourCc(MakeFourCc("ATI2"));
        AddLegacy(TextureFormats.Bc5UNorm, DdsLegacyPixelFormat.Ati2, ati2);
        AddLegacyAlias(TextureFormats.Rgtc2UNorm, ati2);
        AddLegacyAlias(TextureFormats.Ati2UNorm, ati2);
        AddLegacy(TextureFormats.Bc5UNorm, DdsLegacyPixelFormat.Bc5UNorm, LegacyFourCc(MakeFourCc("BC5U")));
        AddLegacy(TextureFormats.Bc5SNorm, DdsLegacyPixelFormat.Bc5SNorm, LegacyFourCc(MakeFourCc("BC5S")));

        AddLegacy(TextureFormats.R16Float, DdsLegacyPixelFormat.R16Float, LegacyFourCc(111));
        AddLegacy(TextureFormats.Rg16Float, DdsLegacyPixelFormat.Rg16Float, LegacyFourCc(112));
        AddLegacy(TextureFormats.Rgba16Float, DdsLegacyPixelFormat.Rgba16Float, LegacyFourCc(113));
        AddLegacy(TextureFormats.R32Float, DdsLegacyPixelFormat.R32Float, LegacyFourCc(114));
        AddLegacy(TextureFormats.Rg32Float, DdsLegacyPixelFormat.Rg32Float, LegacyFourCc(115));
        AddLegacy(TextureFormats.Rgba32Float, DdsLegacyPixelFormat.Rgba32Float, LegacyFourCc(116));
        AddLegacy(TextureFormats.Uyvy422UNorm, DdsLegacyPixelFormat.Uyvy422, LegacyFourCc(MakeFourCc("UYVY")));
        AddLegacy(TextureFormats.Yuy2UNorm, DdsLegacyPixelFormat.Yuy2422, LegacyFourCc(MakeFourCc("YUY2")));
        AddLegacy(TextureFormats.R8G8B8G8_422UNorm, DdsLegacyPixelFormat.R8G8B8G8_422, LegacyFourCc(MakeFourCc("RGBG")));
        AddLegacy(TextureFormats.G8R8G8B8_422UNorm, DdsLegacyPixelFormat.G8R8G8B8_422, LegacyFourCc(MakeFourCc("GRGB")));

        return new Mappings(textureToDxgi, dxgiToTexture, textureToLegacy, legacyToTexture, legacyPixelFormatToTexture);
    }

    private static LegacyFormatKey GetLegacyFormatKey(DdsPixelFormat pixelFormat)
    {
        if ((pixelFormat.Flags & PixelFormatFlagFourCc) != 0)
        {
            return new LegacyFormatKey(LegacyFormatKind.FourCc, pixelFormat.FourCc, RgbBitCount: 0, RedMask: 0, GreenMask: 0, BlueMask: 0, AlphaMask: 0);
        }

        var kind = pixelFormat.Flags switch
        {
            var flags when (flags & PixelFormatFlagRgb) != 0 => LegacyFormatKind.Rgb,
            var flags when (flags & PixelFormatFlagLuminance) != 0 => LegacyFormatKind.Luminance,
            var flags when (flags & PixelFormatFlagAlpha) != 0 => LegacyFormatKind.Alpha,
            var flags when (flags & PixelFormatFlagYuv) != 0 => LegacyFormatKind.Yuv,
            _ => LegacyFormatKind.Unknown
        };

        return new LegacyFormatKey(
            kind,
            FourCc: 0,
            pixelFormat.RgbBitCount,
            pixelFormat.RedMask,
            pixelFormat.GreenMask,
            pixelFormat.BlueMask,
            pixelFormat.AlphaMask);
    }

    private static uint MakeFourCc(string value)
    {
        if (value.Length != 4)
        {
            throw new ArgumentException("DDS FourCC values must contain exactly four characters.", nameof(value));
        }

        return (byte)value[0]
            | ((uint)(byte)value[1] << 8)
            | ((uint)(byte)value[2] << 16)
            | ((uint)(byte)value[3] << 24);
    }

    private static int ReadPositiveInt(ReadOnlySpan<byte> source, string fieldName)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (value == 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"DDS {fieldName} is outside the supported range.");
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
            throw new InvalidDataException("DDS stream ended unexpectedly.", exception);
        }
    }

    private readonly record struct DdsHeader(
        uint Flags,
        int Width,
        int Height,
        uint PitchOrLinearSize,
        uint Depth,
        uint MipMapCount,
        DdsPixelFormat PixelFormat,
        uint Caps,
        uint Caps2,
        uint Caps3,
        uint Caps4);

    private readonly record struct DdsPixelFormat(
        uint Flags,
        uint FourCc,
        uint RgbBitCount,
        uint RedMask,
        uint GreenMask,
        uint BlueMask,
        uint AlphaMask);

    private readonly record struct DdsDxt10Header(
        DdsDxgiFormat DxgiFormat,
        uint ResourceDimension,
        uint MiscFlag,
        uint ArraySize,
        uint MiscFlags2);

    private readonly record struct DdsDxgiDescriptor(DdsDxgiFormat DxgiFormat);

    private readonly record struct DxgiFormatMapping(TextureFormat TextureFormat, DdsDxgiDescriptor DxgiDescriptor);

    private readonly record struct LegacyFormatDescriptor(DdsPixelFormat PixelFormat);

    private readonly record struct LegacyFormatKey(
        LegacyFormatKind Kind,
        uint FourCc,
        uint RgbBitCount,
        uint RedMask,
        uint GreenMask,
        uint BlueMask,
        uint AlphaMask);

    private readonly record struct LegacyFormatMapping(
        TextureFormat TextureFormat,
        LegacyFormatDescriptor LegacyDescriptor,
        DdsLegacyPixelFormat? LegacyPixelFormat);

    private readonly record struct EncodingSelection(
        TextureFormat TextureFormat,
        DdsDxgiDescriptor? DxgiDescriptor,
        LegacyFormatDescriptor? LegacyDescriptor);

    private sealed record Mappings(
        Dictionary<TextureFormat, DdsDxgiDescriptor> TextureToDxgi,
        Dictionary<DdsDxgiFormat, DxgiFormatMapping> DxgiToTexture,
        Dictionary<TextureFormat, LegacyFormatDescriptor> TextureToLegacy,
        Dictionary<LegacyFormatKey, LegacyFormatMapping> LegacyToTexture,
        Dictionary<DdsLegacyPixelFormat, LegacyFormatMapping> LegacyPixelFormatToTexture);

    private enum LegacyFormatKind
    {
        Unknown,
        FourCc,
        Rgb,
        Luminance,
        Alpha,
        Yuv
    }

    [InlineArray(Dxt10HeaderByteCount)]
    private struct Byte20Buffer
    {
        private byte _element0;
    }

    [InlineArray(FileHeaderByteCount)]
    private struct Byte128Buffer
    {
        private byte _element0;
    }
}
