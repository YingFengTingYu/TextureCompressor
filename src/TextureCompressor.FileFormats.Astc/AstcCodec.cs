using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.FileFormats.Astc;

public static class AstcCodec
{
    private const int HeaderByteCount = 16;
    private const int BytesPerBlock = 16;
    private const int MaxDimension = 0xFFFFFF;

    private static ReadOnlySpan<byte> Magic => [0x13, 0xAB, 0xA1, 0x5C];

    public static AstcTexture Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, options: null);
    }

    public static AstcTexture Read(string path, AstcReadOptions? options)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, options);
    }

    public static AstcTexture Read(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Read(stream, options: null);
    }

    public static AstcTexture Read(ReadOnlySpan<byte> data, AstcReadOptions? options)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Read(stream, options);
    }

    public static AstcTexture Read(Stream stream) => Read(stream, options: null);

    public static AstcTexture Read(Stream stream, AstcReadOptions? options)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ReadHeader(stream);
        var format = GetReadTextureFormat(header, options);
        var payloadByteCount = GetExpectedPayloadByteCount(format, header.Width, header.Height, header.Depth);
        var payload = new byte[payloadByteCount];
        ReadExactly(stream, payload);

        return new AstcTexture(format, header.Width, header.Height, header.Depth, payload);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream, options: null);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(string path, AstcReadOptions? options)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream, options);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode(stream, options: null);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(ReadOnlySpan<byte> data, AstcReadOptions? options)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode(stream, options);
    }

    public static ArrayBitmap<Rgba8UNorm> Decode(Stream stream) => Decode<Rgba8UNorm>(stream, options: null);

    public static ArrayBitmap<Rgba8UNorm> Decode(Stream stream, AstcReadOptions? options) => Decode<Rgba8UNorm>(stream, options);

    public static ArrayBitmap<TPixel> Decode<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return Decode<TPixel>(stream, options: null);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(string path, AstcReadOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return Decode<TPixel>(stream, options);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode<TPixel>(stream, options: null);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(ReadOnlySpan<byte> data, AstcReadOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Decode<TPixel>(stream, options);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Decode<TPixel>(stream, options: null);

    public static ArrayBitmap<TPixel> Decode<TPixel>(Stream stream, AstcReadOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var texture = Read(stream, options);
        return Decode<TPixel>(texture);
    }

    public static ArrayBitmap<TPixel> Decode<TPixel>(AstcTexture texture)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.Depth != 1)
        {
            throw new NotSupportedException("Use DecodeVolume for ASTC 3D texture files.");
        }

        var bitmap = new ArrayBitmap<TPixel>(texture.Width, texture.Height);
        var coder = TextureCoderManager.Global.GetCoder(texture.Format);
        coder.Decode(texture.Payload, bitmap.AsView());
        return bitmap;
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(string path)
    {
        using var stream = File.OpenRead(path);
        return DecodeVolume(stream, options: null);
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(string path, AstcReadOptions? options)
    {
        using var stream = File.OpenRead(path);
        return DecodeVolume(stream, options);
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeVolume(stream, options: null);
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(ReadOnlySpan<byte> data, AstcReadOptions? options)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeVolume(stream, options);
    }

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(Stream stream) => DecodeVolume<Rgba8UNorm>(stream, options: null);

    public static ArrayVolumeBitmap<Rgba8UNorm> DecodeVolume(Stream stream, AstcReadOptions? options) => DecodeVolume<Rgba8UNorm>(stream, options);

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return DecodeVolume<TPixel>(stream, options: null);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(string path, AstcReadOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.OpenRead(path);
        return DecodeVolume<TPixel>(stream, options);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeVolume<TPixel>(stream, options: null);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(ReadOnlySpan<byte> data, AstcReadOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return DecodeVolume<TPixel>(stream, options);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        DecodeVolume<TPixel>(stream, options: null);

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(Stream stream, AstcReadOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var texture = Read(stream, options);
        return DecodeVolume<TPixel>(texture);
    }

    public static ArrayVolumeBitmap<TPixel> DecodeVolume<TPixel>(AstcTexture texture)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(texture);

        var bitmap = new ArrayVolumeBitmap<TPixel>(texture.Width, texture.Height, texture.Depth);
        var coder = TextureCoderManager.Global.GetCoder3D(texture.Format);
        coder.Decode(texture.Payload, bitmap.AsView());
        return bitmap;
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options: null);
    }

    public static byte[] Encode<TPixel>(IBitmap<TPixel> bitmap, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, options: null);

    public static byte[] Encode<TPixel>(BitmapView<TPixel> bitmap, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = new MemoryStream();
        Encode(bitmap, stream, options);
        return stream.ToArray();
    }

    public static byte[] Encode<TPixel>(IVolumeBitmap<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options: null);
    }

    public static byte[] Encode<TPixel>(IVolumeBitmap<TPixel> bitmap, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Encode(bitmap.AsView(), options);
    }

    public static byte[] Encode<TPixel>(VolumeBitmapView<TPixel> bitmap)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, options: null);

    public static byte[] Encode<TPixel>(VolumeBitmapView<TPixel> bitmap, AstcEncodingOptions? options)
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

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, string path, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(IVolumeBitmap<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options: null);
    }

    public static void Encode<TPixel>(IVolumeBitmap<TPixel> bitmap, string path, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = File.Create(path);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(VolumeBitmapView<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options: null);
    }

    public static void Encode<TPixel>(VolumeBitmapView<TPixel> bitmap, string path, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using var stream = File.Create(path);
        Encode(bitmap, stream, options: null);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, string path, AstcEncodingOptions? options)
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

    public static void Encode<TPixel>(IBitmap<TPixel> bitmap, Stream stream, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(IVolumeBitmap<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options: null);
    }

    public static void Encode<TPixel>(IVolumeBitmap<TPixel> bitmap, Stream stream, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Encode(bitmap.AsView(), stream, options);
    }

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(BitmapView<TPixel> bitmap, Stream stream, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var format = GetEncodingTextureFormat(options, defaultBlockDepth: 1);
        var coder = TextureCoderManager.Global.GetCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
        coder.Encode(bitmap, payload);
        Write(new AstcTexture(format, bitmap.Width, bitmap.Height, payload), stream);
    }

    public static void Encode<TPixel>(VolumeBitmapView<TPixel> bitmap, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(bitmap, stream, options: null);

    public static void Encode<TPixel>(VolumeBitmapView<TPixel> bitmap, Stream stream, AstcEncodingOptions? options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var format = GetEncodingTextureFormat(options, defaultBlockDepth: 4);
        var coder = TextureCoderManager.Global.GetCoder3D(format);
        var payload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height, bitmap.Depth)];
        coder.Encode(bitmap, payload);
        Write(new AstcTexture(format, bitmap.Width, bitmap.Height, bitmap.Depth, payload), stream);
    }

    public static byte[] Write(AstcTexture texture)
    {
        using var stream = new MemoryStream();
        Write(texture, stream);
        return stream.ToArray();
    }

    public static void Write(AstcTexture texture, string path)
    {
        using var stream = File.Create(path);
        Write(texture, stream);
    }

    public static void Write(AstcTexture texture, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        ValidateAstcFormat(texture.Format, nameof(texture));
        ValidateWritableDimension(texture.Width, nameof(texture));
        ValidateWritableDimension(texture.Height, nameof(texture));
        ValidateWritableDimension(texture.Depth, nameof(texture));
        if (texture.Format.BlockDepth == 1 && texture.Depth != 1)
        {
            throw new ArgumentException("ASTC 2D formats cannot be written with a texture depth greater than 1.", nameof(texture));
        }

        var expectedByteCount = GetExpectedPayloadByteCount(texture.Format, texture.Width, texture.Height, texture.Depth);
        if (texture.Payload.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"ASTC payload length is {texture.Payload.Length} bytes, but '{texture.Format.Name}' expects {expectedByteCount} bytes for {texture.Width}x{texture.Height}x{texture.Depth}.",
                nameof(texture));
        }

        WriteHeader(stream, texture.Format.BlockWidth, texture.Format.BlockHeight, texture.Format.BlockDepth, texture.Width, texture.Height, texture.Depth);
        stream.Write(texture.Payload);
    }

    private static AstcHeader ReadHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[HeaderByteCount];
        ReadExactly(stream, header);

        if (!header[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("The stream is not an ASTC file.");
        }

        var blockWidth = header[4];
        var blockHeight = header[5];
        var blockDepth = header[6];
        if (blockWidth == 0 || blockHeight == 0 || blockDepth == 0)
        {
            throw new InvalidDataException("ASTC block dimensions must be positive.");
        }

        var width = ReadPositiveUInt24(header.Slice(7, 3), "width");
        var height = ReadPositiveUInt24(header.Slice(10, 3), "height");
        var depth = ReadPositiveUInt24(header.Slice(13, 3), "depth");
        if (blockDepth == 1 && depth != 1)
        {
            throw new NotSupportedException("ASTC texture arrays are not supported.");
        }

        return new AstcHeader(blockWidth, blockHeight, blockDepth, width, height, depth);
    }

    private static void WriteHeader(Stream stream, int blockWidth, int blockHeight, int blockDepth, int width, int height, int depth)
    {
        Span<byte> header = stackalloc byte[HeaderByteCount];
        Magic.CopyTo(header);
        header[4] = checked((byte)blockWidth);
        header[5] = checked((byte)blockHeight);
        header[6] = checked((byte)blockDepth);
        WriteUInt24(header.Slice(7, 3), width);
        WriteUInt24(header.Slice(10, 3), height);
        WriteUInt24(header.Slice(13, 3), depth);

        stream.Write(header);
    }

    private static TextureFormat GetReadTextureFormat(AstcHeader header, AstcReadOptions? options)
    {
        if (options?.TextureFormat is { } format)
        {
            ValidateAstcFormat(format, nameof(options));
            if (format.BlockWidth != header.BlockWidth
                || format.BlockHeight != header.BlockHeight
                || format.BlockDepth != header.BlockDepth)
            {
                throw new InvalidDataException(
                    $"ASTC file block footprint is {header.BlockWidth}x{header.BlockHeight}x{header.BlockDepth}, but read options specify '{format.Name}'.");
            }

            return format;
        }

        return GetTextureFormat(header.BlockWidth, header.BlockHeight, header.BlockDepth, options?.Profile ?? AstcProfile.UNorm);
    }

    private static TextureFormat GetEncodingTextureFormat(AstcEncodingOptions? options, int defaultBlockDepth)
    {
        if (options?.TextureFormat is { } format)
        {
            ValidateAstcFormat(format, nameof(options));
            return format;
        }

        return GetTextureFormat(options?.BlockWidth ?? 4, options?.BlockHeight ?? 4, options?.BlockDepth ?? defaultBlockDepth, options?.Profile ?? AstcProfile.UNorm);
    }

    private static TextureFormat GetTextureFormat(int blockWidth, int blockHeight, int blockDepth, AstcProfile profile)
    {
        foreach (var format in GetSupportedAstcFormats(blockDepth))
        {
            if (format.BlockWidth == blockWidth
                && format.BlockHeight == blockHeight
                && format.BlockDepth == blockDepth
                && GetProfile(format) == profile)
            {
                return format;
            }
        }

        throw new NotSupportedException($"ASTC {blockWidth}x{blockHeight}x{blockDepth} {profile} textures are not supported.");
    }

    private static AstcProfile GetProfile(TextureFormat format) =>
        format.ValueKind switch
        {
            TextureValueKind.UNorm => AstcProfile.UNorm,
            TextureValueKind.Srgb => AstcProfile.Srgb,
            TextureValueKind.Float => AstcProfile.Float,
            _ => throw new NotSupportedException($"Texture format '{format.Name}' is not a supported ASTC profile.")
        };

    private static void ValidateAstcFormat(TextureFormat format, string parameterName)
    {
        if (!AstcTextureCoder.IsSupported(format) && !Astc3DTextureCoder.IsSupported(format))
        {
            throw new ArgumentException($"Texture format '{format.Name}' is not a supported ASTC format.", parameterName);
        }
    }

    private static int GetExpectedPayloadByteCount(TextureFormat format, int width, int height, int depth)
    {
        try
        {
            return depth == 1 && format.BlockDepth == 1
                ? TextureCoderManager.Global.GetCoder(format).GetEncodedByteCount(width, height)
                : TextureCoderManager.Global.GetCoder3D(format).GetEncodedByteCount(width, height, depth);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("ASTC payload size is outside the supported range.", exception);
        }
    }

    private static ReadOnlySpan<TextureFormat> GetSupportedAstcFormats(int blockDepth) =>
        blockDepth == 1 ? AstcTextureCoder.SupportedFormats : Astc3DTextureCoder.SupportedFormats;

    private static int ReadPositiveUInt24(ReadOnlySpan<byte> source, string fieldName)
    {
        var value = source[0] | (source[1] << 8) | (source[2] << 16);
        if (value == 0)
        {
            throw new InvalidDataException($"ASTC {fieldName} is outside the supported range.");
        }

        return value;
    }

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
    }

    private static void ValidateWritableDimension(int value, string parameterName)
    {
        if (value > MaxDimension)
        {
            throw new ArgumentException("ASTC dimensions must fit in 24 bits.", parameterName);
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("ASTC stream ended unexpectedly.", exception);
        }
    }

    private readonly record struct AstcHeader(int BlockWidth, int BlockHeight, int BlockDepth, int Width, int Height, int Depth);
}
