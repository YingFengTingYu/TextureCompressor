namespace TextureCompressor.Formats;

public sealed class TextureMipLevel
{
    public TextureMipLevel(int width, int height, byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(payload);

        Width = width;
        Height = height;
        Payload = payload;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Payload { get; }

    public byte[] Data => Payload;

    public static int GetDimension(int baseDimension, int mipLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDimension);
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);

        return Math.Max(1, baseDimension >> mipLevel);
    }

    public static int GetFullMipLevelCount(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var count = 1;
        while (width > 1 || height > 1)
        {
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
            count++;
        }

        return count;
    }
}
