using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats;
using TextureCompressor.Formats;
using TextureCompressor.Options;
using TextureCompressor.Registry;

namespace TextureCompressor.Conversion;

public sealed class TextureConverter
{
    private readonly TextureFileFormatManager _fileFormats;
    private readonly TextureCoderManager _coders;

    public TextureConverter()
        : this(TextureFileFormatManager.Global, TextureCoderManager.Global)
    {
    }

    public TextureConverter(TextureFileFormatManager fileFormats)
        : this(fileFormats, TextureCoderManager.Global)
    {
    }

    public TextureConverter(TextureFileFormatManager fileFormats, TextureCoderManager coders)
    {
        ArgumentNullException.ThrowIfNull(fileFormats);
        ArgumentNullException.ThrowIfNull(coders);

        _fileFormats = fileFormats;
        _coders = coders;
    }

    public TextureConversionResult Convert(string inputPath, string outputPath, TextureConversionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using var input = File.OpenRead(inputPath);
        using var output = File.Create(outputPath);
        return Convert(input, inputPath, output, outputPath, options);
    }

    public TextureConversionResult Convert(
        Stream input,
        string inputPathOrExtension,
        Stream output,
        string outputPathOrExtension,
        TextureConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPathOrExtension);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPathOrExtension);

        options ??= new TextureConversionOptions();

        var sourceKind = GetFileKind(inputPathOrExtension);
        var targetKind = GetFileKind(outputPathOrExtension);
        ValidateOptions(sourceKind, targetKind, options);

        return sourceKind switch
        {
            TextureConversionFileKind.Image => ConvertImageInput(input, inputPathOrExtension, output, outputPathOrExtension, targetKind, options),
            TextureConversionFileKind.Texture => ConvertTextureInput(input, inputPathOrExtension, output, outputPathOrExtension, targetKind, options),
            _ => throw new NotSupportedException($"Unsupported source file kind '{sourceKind}'.")
        };
    }

    public TextureImage EncodeTexture(
        IBitmap<Rgba8UNorm> image,
        TextureFormat format,
        TextureConversionMipmaps mipmaps = TextureConversionMipmaps.None,
        TextureCompressionLevel? compressionLevel = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var compressionRegistration = CreateTextureCompressionRegistration(format, compressionLevel);
        return EncodeTextureCore(image, format, mipmaps);
    }

    public ArrayBitmap<Rgba8UNorm> DecodeTexture(TextureImage texture, TextureSubresourceSelection selection = default)
    {
        ArgumentNullException.ThrowIfNull(texture);

        ValidateSelection(texture, selection);
        return DecodeSubresource(texture.Format, texture.GetSubresource(selection.MipLevel, selection.ArrayLayer, selection.FaceIndex));
    }

    public TextureImage TranscodeTexture(TextureImage texture, TextureFormat format, TextureCompressionLevel? compressionLevel = null)
    {
        ArgumentNullException.ThrowIfNull(texture);

        using var compressionRegistration = CreateTextureCompressionRegistration(format, compressionLevel);
        return texture.Format == format && compressionLevel is null
            ? texture
            : ReencodeTexture(texture, format);
    }

    private TextureConversionResult ConvertImageInput(
        Stream input,
        string inputPathOrExtension,
        Stream output,
        string outputPathOrExtension,
        TextureConversionFileKind targetKind,
        TextureConversionOptions options)
    {
        var image = _fileFormats.ReadImage<Rgba8UNorm>(input, inputPathOrExtension, options.ReadOptions);
        if (targetKind == TextureConversionFileKind.Image)
        {
            _fileFormats.WriteImage(image, output, outputPathOrExtension, options.WriteOptions);
            return CreateImageResult(TextureConversionFileKind.Image, TextureConversionFileKind.Image, image.Width, image.Height);
        }

        var format = options.TargetFormat ?? TextureFormats.Rgba8UNorm;
        using var compressionRegistration = CreateTextureCompressionRegistration(format, options.CompressionLevel);
        var texture = EncodeTextureCore(image, format, options.Mipmaps);
        _fileFormats.WriteTexture(texture, output, outputPathOrExtension, options.WriteOptions);
        return CreateTextureResult(TextureConversionFileKind.Image, texture, sourceFormat: null);
    }

    private TextureConversionResult ConvertTextureInput(
        Stream input,
        string inputPathOrExtension,
        Stream output,
        string outputPathOrExtension,
        TextureConversionFileKind targetKind,
        TextureConversionOptions options)
    {
        var source = _fileFormats.ReadTexture(input, inputPathOrExtension, options.ReadOptions).Texture;
        if (targetKind == TextureConversionFileKind.Image)
        {
            var selection = options.SourceSubresource ?? default;
            var image = DecodeTexture(source, selection);
            _fileFormats.WriteImage(image, output, outputPathOrExtension, options.WriteOptions);
            return new TextureConversionResult(
                TextureConversionFileKind.Texture,
                TextureConversionFileKind.Image,
                image.Width,
                image.Height,
                source.Format,
                TargetTextureFormat: null,
                MipLevelCount: 1,
                ArrayLayerCount: 1,
                FaceCount: 1);
        }

        var targetFormat = options.TargetFormat ?? source.Format;
        using var compressionRegistration = CreateTextureCompressionRegistration(targetFormat, options.CompressionLevel);
        var texture = options.SourceSubresource is null && options.Mipmaps == TextureConversionMipmaps.None
            ? TranscodeTextureCore(source, targetFormat, options.CompressionLevel)
            : EncodeTextureCore(DecodeTexture(source, options.SourceSubresource ?? default), targetFormat, options.Mipmaps);

        _fileFormats.WriteTexture(texture, output, outputPathOrExtension, options.WriteOptions);
        return CreateTextureResult(TextureConversionFileKind.Texture, texture, source.Format);
    }

    private TextureConversionFileKind GetFileKind(string pathOrExtension)
    {
        var hasImageFormat = _fileFormats.TryGetImageFormat(pathOrExtension, out _);
        var hasTextureFormat = _fileFormats.TryGetTextureFormat(pathOrExtension, out _);
        return (hasImageFormat, hasTextureFormat) switch
        {
            (true, false) => TextureConversionFileKind.Image,
            (false, true) => TextureConversionFileKind.Texture,
            (true, true) => throw new NotSupportedException($"Extension '{pathOrExtension}' is registered as both an image and texture format."),
            _ => throw new NotSupportedException($"No file format is registered for extension '{Path.GetExtension(pathOrExtension)}'.")
        };
    }

    private static void ValidateOptions(
        TextureConversionFileKind sourceKind,
        TextureConversionFileKind targetKind,
        TextureConversionOptions options)
    {
        if (sourceKind == TextureConversionFileKind.Image && options.SourceSubresource is not null)
        {
            throw new NotSupportedException("Source subresource selection applies only to texture inputs.");
        }

        if (targetKind == TextureConversionFileKind.Image)
        {
            if (options.Mipmaps != TextureConversionMipmaps.None)
            {
                throw new NotSupportedException("Mip-map generation applies only to texture outputs.");
            }

            if (options.TargetFormat is not null)
            {
                throw new NotSupportedException("Target texture format applies only to texture outputs.");
            }

            if (options.CompressionLevel is not null)
            {
                throw new NotSupportedException("Texture compression level applies only to texture outputs.");
            }
        }
    }

    private TextureImage EncodeTextureCore(IBitmap<Rgba8UNorm> image, TextureFormat format, TextureConversionMipmaps mipmaps) =>
        mipmaps switch
        {
            TextureConversionMipmaps.None => new TextureImage(format, image.Width, image.Height, EncodeSubresourcePayload(format, image)),
            TextureConversionMipmaps.Generate => EncodeMipChain(format, BitmapMipChain.Generate(image)),
            _ => throw new ArgumentOutOfRangeException(nameof(mipmaps), mipmaps, "Unsupported mip-map conversion mode.")
        };

    private TextureImage EncodeMipChain(TextureFormat format, IReadOnlyList<IBitmap<Rgba8UNorm>> mipLevels)
    {
        var subresources = new TextureSubresource[mipLevels.Count];
        for (var i = 0; i < mipLevels.Count; i++)
        {
            var mip = mipLevels[i];
            subresources[i] = new TextureSubresource(
                i,
                arrayLayer: 0,
                faceIndex: 0,
                mip.Width,
                mip.Height,
                EncodeSubresourcePayload(format, mip));
        }

        return new TextureImage(format, subresources, faceCount: 1);
    }

    private TextureImage TranscodeTextureCore(TextureImage texture, TextureFormat format, TextureCompressionLevel? compressionLevel) =>
        texture.Format == format && compressionLevel is null
            ? texture
            : ReencodeTexture(texture, format);

    private TextureImage ReencodeTexture(TextureImage texture, TextureFormat format)
    {
        var subresources = new TextureSubresource[texture.Subresources.Count];
        for (var i = 0; i < texture.Subresources.Count; i++)
        {
            var source = texture.Subresources[i];
            var bitmap = DecodeSubresource(texture.Format, source);
            subresources[i] = new TextureSubresource(
                source.MipLevel,
                source.ArrayLayer,
                source.FaceIndex,
                source.Width,
                source.Height,
                EncodeSubresourcePayload(format, bitmap));
        }

        return new TextureImage(format, subresources, texture.ArrayLayerCount, texture.FaceCount);
    }

    private byte[] EncodeSubresourcePayload(TextureFormat format, IBitmap<Rgba8UNorm> image)
    {
        var coder = _coders.GetCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(image.Width, image.Height)];
        coder.Encode(image.AsView(), payload);
        return payload;
    }

    private ArrayBitmap<Rgba8UNorm> DecodeSubresource(TextureFormat format, TextureSubresource subresource)
    {
        var bitmap = new ArrayBitmap<Rgba8UNorm>(subresource.Width, subresource.Height);
        _coders.GetCoder(format).Decode(subresource.Payload, bitmap.AsView());
        return bitmap;
    }

    private static void ValidateSelection(TextureImage texture, TextureSubresourceSelection selection)
    {
        if ((uint)selection.MipLevel >= (uint)texture.MipLevelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Mip level {selection.MipLevel} is outside the texture mip level count {texture.MipLevelCount}.");
        }

        if ((uint)selection.ArrayLayer >= (uint)texture.ArrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Array layer {selection.ArrayLayer} is outside the texture array layer count {texture.ArrayLayerCount}.");
        }

        if (selection.HasFace && texture.FaceCount != 6)
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "Face selection requires a cube-map texture.");
        }

        if ((uint)selection.FaceIndex >= (uint)texture.FaceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Face index {selection.FaceIndex} is outside the texture face count {texture.FaceCount}.");
        }
    }

    private IDisposable? CreateTextureCompressionRegistration(TextureFormat format, TextureCompressionLevel? compressionLevel)
    {
        if (compressionLevel is null)
        {
            return null;
        }

        var options = new TextureCompressionOptions { CompressionMode = compressionLevel.Value };
        if (S3tcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new S3tcTextureCoder(format, options));
        }

        if (FxtcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new FxtcTextureCoder(format, options));
        }

        if (EtcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new EtcTextureCoder(format, options));
        }

        if (AtcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new AtcTextureCoder(format, options));
        }

        if (RgtcLatcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new RgtcLatcTextureCoder(format, options));
        }

        if (BptcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new BptcTextureCoder(format, options));
        }

        if (PvrtcTextureCoder.IsSupported(format))
        {
            return _coders.Register(format, new PvrtcTextureCoder(format, options));
        }

        return AstcTextureCoder.IsSupported(format)
            ? _coders.Register(format, new AstcTextureCoder(format, options))
            : null;
    }

    private static TextureConversionResult CreateImageResult(
        TextureConversionFileKind sourceKind,
        TextureConversionFileKind targetKind,
        int width,
        int height) =>
        new(
            sourceKind,
            targetKind,
            width,
            height,
            SourceTextureFormat: null,
            TargetTextureFormat: null,
            MipLevelCount: 1,
            ArrayLayerCount: 1,
            FaceCount: 1);

    private static TextureConversionResult CreateTextureResult(
        TextureConversionFileKind sourceKind,
        TextureImage texture,
        TextureFormat? sourceFormat) =>
        new(
            sourceKind,
            TextureConversionFileKind.Texture,
            texture.Width,
            texture.Height,
            sourceFormat,
            texture.Format,
            texture.MipLevelCount,
            texture.ArrayLayerCount,
            texture.FaceCount);
}
