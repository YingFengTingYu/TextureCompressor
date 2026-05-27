namespace TextureCompressor.Formats;

public readonly record struct TextureFormat(
    string Name,
    TextureFormatKind Kind,
    TextureComponents Components,
    TextureValueKind ValueKind,
    int RedBits,
    int GreenBits,
    int BlueBits,
    int AlphaBits,
    int BlockWidth,
    int BlockHeight,
    int BitsPerBlock)
{
    public int ChannelCount => Components switch
    {
        TextureComponents.R => 1,
        TextureComponents.Rg => 2,
        TextureComponents.Rgb or TextureComponents.Bgr => 3,
        TextureComponents.Rgba or TextureComponents.Bgra or TextureComponents.Bgrx or TextureComponents.Argb or TextureComponents.Abgr => 4,
        TextureComponents.Alpha or TextureComponents.Luminance or TextureComponents.Intensity => 1,
        TextureComponents.LuminanceAlpha => 2,
        _ => throw new InvalidOperationException($"Unsupported texture component layout '{Components}'.")
    };

    public int BitsPerTexel => BitsPerBlock / checked(BlockWidth * BlockHeight);

    public int BytesPerBlock => (BitsPerBlock + 7) / 8;

    public bool IsCompressed => Kind != TextureFormatKind.Uncompressed;

    public int GetRowByteCount(int width) => checked((int)GetRowByteCount64(width));

    public long GetRowByteCount64(int width)
    {
        ValidateLayout();
        ValidateWidth(width);

        var blockCountX = (width + BlockWidth - 1L) / BlockWidth;
        return checked(blockCountX * BytesPerBlock);
    }

    public int GetByteCount(int width, int height) => checked((int)GetByteCount64(width, height));

    public long GetByteCount64(int width, int height)
    {
        var rowByteCount = GetRowByteCount64(width);
        ValidateHeight(height);

        var blockCountY = (height + BlockHeight - 1L) / BlockHeight;
        return checked(rowByteCount * blockCountY);
    }

    public static TextureFormat Uncompressed(
        string name,
        TextureComponents components,
        TextureValueKind valueKind,
        int redBits,
        int greenBits = 0,
        int blueBits = 0,
        int alphaBits = 0) => new(
            name,
            TextureFormatKind.Uncompressed,
            components,
            valueKind,
            redBits,
            greenBits,
            blueBits,
            alphaBits,
            1,
            1,
            checked(redBits + greenBits + blueBits + alphaBits));

    public static TextureFormat BlockCompressed(
        string name,
        TextureComponents components,
        TextureValueKind valueKind,
        int redBits,
        int greenBits,
        int blueBits,
        int alphaBits,
        int blockWidth,
        int blockHeight,
        int bitsPerBlock) => new(
            name,
            TextureFormatKind.BlockCompressed,
            components,
            valueKind,
            redBits,
            greenBits,
            blueBits,
            alphaBits,
            blockWidth,
            blockHeight,
            bitsPerBlock);

    private void ValidateLayout()
    {
        if (BlockWidth <= 0 || BlockHeight <= 0 || BitsPerBlock <= 0)
        {
            throw new InvalidOperationException("Texture format block dimensions and bit count must be positive.");
        }
    }

    private static void ValidateWidth(int width)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
    }

    private static void ValidateHeight(int height)
    {
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
    }
}
