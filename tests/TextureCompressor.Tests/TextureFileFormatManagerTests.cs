using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed class TextureFileFormatManagerTests
{
    [Fact]
    public void ReadImageUsesLastRegisteredFormatForExtension()
    {
        var manager = new TextureFileFormatManager();
        using var first = manager.Register(new FakeImageFileFormat("First", ".img", 1));
        var second = manager.Register(new FakeImageFileFormat("Second", ".img", 2));

        using var stream = new MemoryStream();
        var image = manager.ReadImage<Rgba8UNorm>(stream, ".img");

        Assert.Equal(2, image.PixelSpan[0].Red);

        second.Dispose();
        stream.Position = 0;
        image = manager.ReadImage<Rgba8UNorm>(stream, ".img");

        Assert.Equal(1, image.PixelSpan[0].Red);
    }

    [Fact]
    public void ReadImagePrefersHeaderMatchOverExtensionMatch()
    {
        var manager = new TextureFileFormatManager();
        using var extensionRegistration = manager.Register(new FakeImageFileFormat("Extension", ".img", 1, [0x01]));
        using var headerRegistration = manager.Register(new FakeImageFileFormat("Header", ".other", 2, [0x42]));

        using var stream = new MemoryStream([0x42]);
        var image = manager.ReadImage<Rgba8UNorm>(stream, ".img");

        Assert.Equal(2, image.PixelSpan[0].Red);
    }

    [Fact]
    public void WriteTextureUsesExtensionMatch()
    {
        var manager = new TextureFileFormatManager();
        using var registration = manager.Register(new FakeTextureFileFormat(".tex"));
        var texture = new TextureImage(TextureFormats.Rgba8UNorm, 1, 1, [0x7a]);
        using var stream = new MemoryStream();

        manager.WriteTexture(texture, stream, "tex");

        Assert.Equal([0x7a], stream.ToArray());
    }

    [Fact]
    public void ReadTextureReturnsTextureFile()
    {
        var manager = new TextureFileFormatManager();
        using var registration = manager.Register(new FakeTextureFileFormat(".tex"));
        using var stream = new MemoryStream([0x35]);

        var textureFile = manager.ReadTexture(stream, ".tex");

        Assert.Equal(0x35, textureFile.Texture.Payload[0]);
    }

    [Fact]
    public void UnknownExtensionThrows()
    {
        var manager = new TextureFileFormatManager();

        Assert.False(manager.TryGetImageFormat(".missing", out _));
        Assert.Throws<NotSupportedException>(() => manager.GetTextureFormat(".missing"));
    }

    private sealed class FakeImageFileFormat(
        string name,
        string extension,
        byte value,
        byte[]? magic = null) : IImageFileFormat
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Extensions { get; } = [extension];

        public bool CanRead(ReadOnlySpan<byte> header, string? extension) =>
            magic is not null
            && header.Length >= magic.Length
            && header[..magic.Length].SequenceEqual(magic);

        public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var pixel = TPixel.FromRgba8UNorm(new Rgba8UNorm(value, 0, 0, 255));
            return new ArrayBitmap<TPixel>(1, 1, [pixel]);
        }

        public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, IFileFormatOptions? options = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            stream.WriteByte(value);
        }
    }

    private sealed class FakeTextureFileFormat(string extension) : ITextureFileFormat
    {
        public string Name => "Texture";

        public IReadOnlyList<string> Extensions { get; } = [extension];

        public bool CanRead(ReadOnlySpan<byte> header, string? extension) => header.Length > 0;

        public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                throw new EndOfStreamException();
            }

            return new FakeTextureFile(new TextureImage(TextureFormats.Rgba8UNorm, 1, 1, [(byte)value]));
        }

        public void WriteTexture(TextureImage texture, Stream stream, IFileFormatOptions? options = null)
        {
            stream.WriteByte(texture.Payload[0]);
        }
    }

    private sealed class FakeTextureFile(TextureImage texture) : ITextureFile
    {
        public TextureImage Texture { get; } = texture;
    }
}
