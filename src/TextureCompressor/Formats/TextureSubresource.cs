namespace TextureCompressor.Formats;

public sealed class TextureSubresource
{
    public TextureSubresource(
        int mipLevel,
        int arrayLayer,
        int faceIndex,
        int width,
        int height,
        byte[] payload)
        : this(mipLevel, arrayLayer, faceIndex, width, height, depth: 1, payload)
    {
    }

    public TextureSubresource(
        int mipLevel,
        int arrayLayer,
        int faceIndex,
        int width,
        int height,
        int depth,
        byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayLayer);
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentNullException.ThrowIfNull(payload);

        MipLevel = mipLevel;
        ArrayLayer = arrayLayer;
        FaceIndex = faceIndex;
        Width = width;
        Height = height;
        Depth = depth;
        Payload = payload;
    }

    public int MipLevel { get; }

    public int ArrayLayer { get; }

    public int FaceIndex { get; }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public byte[] Payload { get; }

    public byte[] Data => Payload;
}
