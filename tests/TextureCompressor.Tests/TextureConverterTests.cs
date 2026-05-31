using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Conversion;
using TextureCompressor.FileFormats;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class TextureConverterTests
{
    [Fact]
    public void ConvertImageToTextureEncodesRequestedFormatAndMipmaps()
    {
        var manager = new TextureFileFormatManager();
        var imageFormat = new FakeImageFileFormat(".img", CreateImage(2, 2));
        var textureFormat = new FakeTextureFileFormat(".tex");
        using var imageRegistration = manager.Register(imageFormat);
        using var textureRegistration = manager.Register(textureFormat);
        var converter = new TextureConverter(manager);

        var result = converter.Convert(
            new MemoryStream(),
            ".img",
            new MemoryStream(),
            ".tex",
            new TextureConversionOptions
            {
                TargetFormat = TextureFormats.Rgba8UNorm,
                Mipmaps = TextureConversionMipmaps.Generate
            });

        Assert.Equal(TextureConversionFileKind.Image, result.SourceKind);
        Assert.Equal(TextureConversionFileKind.Texture, result.TargetKind);
        Assert.Equal(TextureFormats.Rgba8UNorm, textureFormat.WrittenTexture?.Format);
        Assert.Equal(2, textureFormat.WrittenTexture?.MipLevelCount);
    }

    [Fact]
    public void ConvertTextureToImageDecodesSelectedSubresource()
    {
        var sourceTexture = CreateMipTexture();
        var manager = new TextureFileFormatManager();
        var textureFormat = new FakeTextureFileFormat(".tex", sourceTexture);
        var imageFormat = new FakeImageFileFormat(".img", CreateImage(1, 1));
        using var textureRegistration = manager.Register(textureFormat);
        using var imageRegistration = manager.Register(imageFormat);
        var converter = new TextureConverter(manager);

        converter.Convert(
            new MemoryStream(),
            ".tex",
            new MemoryStream(),
            ".img",
            new TextureConversionOptions
            {
                SourceSubresource = new TextureSubresourceSelection(1, 0, null)
            });

        Assert.NotNull(imageFormat.WrittenImage);
        Assert.Equal(1, imageFormat.WrittenImage.Width);
        Assert.Equal(1, imageFormat.WrittenImage.Height);
        Assert.Equal(64, imageFormat.WrittenImage.PixelSpan[0].Red);
    }

    [Fact]
    public void ConvertTextureToTexturePreservesSubresourcesWhenNoSelectionIsSet()
    {
        var sourceTexture = CreateMipTexture();
        var manager = new TextureFileFormatManager();
        var inputFormat = new FakeTextureFileFormat(".in", sourceTexture);
        var outputFormat = new FakeTextureFileFormat(".out");
        using var inputRegistration = manager.Register(inputFormat);
        using var outputRegistration = manager.Register(outputFormat);
        var converter = new TextureConverter(manager);

        var result = converter.Convert(new MemoryStream(), ".in", new MemoryStream(), ".out");

        Assert.Same(sourceTexture, outputFormat.WrittenTexture);
        Assert.Equal(2, result.MipLevelCount);
        Assert.Equal(TextureFormats.Rgba8UNorm, result.TargetTextureFormat);
    }

    [Fact]
    public void ConvertImageToImageRejectsTextureOnlyOptions()
    {
        var manager = new TextureFileFormatManager();
        using var imageRegistration = manager.Register(new FakeImageFileFormat(".img", CreateImage(1, 1)));
        using var otherRegistration = manager.Register(new FakeImageFileFormat(".other", CreateImage(1, 1)));
        var converter = new TextureConverter(manager);

        Assert.Throws<NotSupportedException>(() =>
            converter.Convert(
                new MemoryStream(),
                ".img",
                new MemoryStream(),
                ".other",
                new TextureConversionOptions { TargetFormat = TextureFormats.Rgba8UNorm }));
    }

    private static ArrayBitmap<Rgba8UNorm> CreateImage(int width, int height)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Rgba8UNorm((byte)(16 + i), 0, 0, 255);
        }

        return new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
    }

    private static TextureImage CreateMipTexture()
    {
        return new TextureImage(
            TextureFormats.Rgba8UNorm,
            [
                new TextureSubresource(0, 0, 0, 2, 2, EncodePayload(CreateImage(2, 2))),
                new TextureSubresource(1, 0, 0, 1, 1, EncodePayload(new ArrayBitmap<Rgba8UNorm>(
                    1,
                    1,
                    [new Rgba8UNorm(64, 0, 0, 255)])))
            ],
            faceCount: 1);
    }

    private static byte[] EncodePayload(IBitmap<Rgba8UNorm> image)
    {
        var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UNorm);
        var payload = new byte[coder.GetEncodedByteCount(image.Width, image.Height)];
        coder.Encode(image.AsView(), payload);
        return payload;
    }

    private sealed class FakeImageFileFormat(string extension, ArrayBitmap<Rgba8UNorm> image) : IImageFileFormat
    {
        public string Name => "Image";

        public IReadOnlyList<string> Extensions { get; } = [extension];

        public ArrayBitmap<Rgba8UNorm>? WrittenImage { get; private set; }

        public bool CanRead(ReadOnlySpan<byte> header, string? extension) => true;

        public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var pixels = new TPixel[image.PixelSpan.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = TPixel.FromRgba8UNorm(image.PixelSpan[i]);
            }

            return new ArrayBitmap<TPixel>(image.Width, image.Height, pixels);
        }

        public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var source = image.AsView().Pixels;
            var pixels = new Rgba8UNorm[source.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = TPixel.ToRgba8UNorm(source[i]);
            }

            WrittenImage = new ArrayBitmap<Rgba8UNorm>(image.Width, image.Height, pixels);
        }
    }

    private sealed class FakeTextureFileFormat(string extension, TextureImage? texture = null) : ITextureFileFormat
    {
        public string Name => "Texture";

        public IReadOnlyList<string> Extensions { get; } = [extension];

        public TextureImage? WrittenTexture { get; private set; }

        public bool CanRead(ReadOnlySpan<byte> header, string? extension) => true;

        public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null) =>
            new FakeTextureFile(texture ?? CreateMipTexture());

        public void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null)
        {
            WrittenTexture = texture;
        }
    }

    private sealed class FakeTextureFile(TextureImage texture) : ITextureFile
    {
        public TextureImage Texture { get; } = texture;
    }
}
