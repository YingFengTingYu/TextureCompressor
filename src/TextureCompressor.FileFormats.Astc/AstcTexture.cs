using TextureCompressor.FileFormats;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc;

public sealed class AstcTexture : ITextureFile
{
    public AstcTexture(TextureFormat format, int width, int height, byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(payload);

        Texture = new TextureImage(format, width, height, payload);
        Format = format;
        Width = width;
        Height = height;
        Payload = payload;
    }

    public TextureImage Texture { get; }

    public TextureFormat Format { get; }

    public int Width { get; }

    public int Height { get; }

    public byte[] Payload { get; }

    public byte[] Data => Payload;

    public int BlockWidth => Format.BlockWidth;

    public int BlockHeight => Format.BlockHeight;
}
