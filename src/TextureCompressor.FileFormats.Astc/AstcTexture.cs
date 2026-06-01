using TextureCompressor.FileFormats;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Astc;

public sealed class AstcTexture : ITextureFile
{
    public AstcTexture(TextureFormat format, int width, int height, byte[] payload)
        : this(format, width, height, depth: 1, payload)
    {
    }

    public AstcTexture(TextureFormat format, int width, int height, int depth, byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentNullException.ThrowIfNull(payload);

        Texture = new TextureImage(format, width, height, depth, payload);
        Format = format;
        Width = width;
        Height = height;
        Depth = depth;
        Payload = payload;
    }

    public TextureImage Texture { get; }

    public TextureFormat Format { get; }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public byte[] Payload { get; }

    public byte[] Data => Payload;

    public int BlockWidth => Format.BlockWidth;

    public int BlockHeight => Format.BlockHeight;

    public int BlockDepth => Format.BlockDepth;
}
