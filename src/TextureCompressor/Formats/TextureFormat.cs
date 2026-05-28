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
    int BitsPerBlock,
    int HeaderByteCount = 0,
    int BlockDepth = 1,
    bool IsVariableSize = false,
    TexturePayloadSizeMode SizeMode = TexturePayloadSizeMode.Default)
{
    private const int PvrtcWordHeight = 4;
    private const int PvrtcWordByteCount = 8;

    public int ChannelCount => Components switch
    {
        TextureComponents.R => 1,
        TextureComponents.Rg => 2,
        TextureComponents.Rgb or TextureComponents.Yuv or TextureComponents.Bgr => 3,
        TextureComponents.Rgba or TextureComponents.Yuva or TextureComponents.Bgra or TextureComponents.Bgrx or TextureComponents.Argb or TextureComponents.Abgr => 4,
        TextureComponents.Alpha or TextureComponents.Luminance or TextureComponents.Intensity => 1,
        TextureComponents.LuminanceAlpha => 2,
        TextureComponents.Depth or TextureComponents.Stencil => 1,
        TextureComponents.DepthStencil => 2,
        _ => throw new InvalidOperationException($"Unsupported texture component layout '{Components}'.")
    };

    public int BitsPerTexel => BitsPerBlock / checked(BlockWidth * BlockHeight * BlockDepth);

    public int BytesPerBlock => (BitsPerBlock + 7) / 8;

    public bool IsCompressed => Kind != TextureFormatKind.Uncompressed;

    public int GetRowByteCount(int width) => checked((int)GetRowByteCount64(width));

    public long GetRowByteCount64(int width)
    {
        ValidateLayout();
        ValidateFixedSizeLayout();
        ValidateWidth(width);

        if (SizeMode is TexturePayloadSizeMode.PvrtcI or TexturePayloadSizeMode.PvrtcII)
        {
            return GetPvrtcRowByteCount(width);
        }

        var blockCountX = (width + BlockWidth - 1L) / BlockWidth;
        return checked(blockCountX * BytesPerBlock);
    }

    public int GetByteCount(int width, int height) => checked((int)GetByteCount64(width, height));

    public long GetByteCount64(int width, int height)
    {
        if (SizeMode is TexturePayloadSizeMode.PvrtcI or TexturePayloadSizeMode.PvrtcII)
        {
            ValidateLayout();
            ValidateFixedSizeLayout();
            ValidateWidth(width);
            ValidateHeight(height);
            return checked(HeaderByteCount + GetPvrtcPayloadByteCount(width, height));
        }

        var rowByteCount = GetRowByteCount64(width);
        ValidateHeight(height);

        var blockCountY = (height + BlockHeight - 1L) / BlockHeight;
        return checked(HeaderByteCount + (rowByteCount * blockCountY));
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

    public static TextureFormat Paletted(
        string name,
        TextureComponents components,
        TextureValueKind valueKind,
        int redBits,
        int greenBits,
        int blueBits,
        int alphaBits,
        int indexBits,
        int paletteEntryCount,
        int paletteEntryByteCount)
    {
        if (indexBits is not (4 or 8))
        {
            throw new ArgumentOutOfRangeException(nameof(indexBits), "Paletted formats currently support 4-bit or 8-bit indices.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(paletteEntryCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(paletteEntryByteCount);

        return new(
            name,
            TextureFormatKind.Paletted,
            components,
            valueKind,
            redBits,
            greenBits,
            blueBits,
            alphaBits,
            8 / indexBits,
            1,
            8,
            checked(paletteEntryCount * paletteEntryByteCount));
    }

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
        int bitsPerBlock,
        TexturePayloadSizeMode sizeMode = TexturePayloadSizeMode.Default) => new(
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
            bitsPerBlock,
            SizeMode: sizeMode);

    private void ValidateLayout()
    {
        if (BlockWidth <= 0 || BlockHeight <= 0 || BlockDepth <= 0 || BitsPerBlock <= 0 || HeaderByteCount < 0)
        {
            throw new InvalidOperationException("Texture format block dimensions and bit count must be positive.");
        }
    }

    private void ValidateFixedSizeLayout()
    {
        if (IsVariableSize)
        {
            throw new NotSupportedException($"Texture format '{Name}' has a variable-size payload.");
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

    private long GetPvrtcRowByteCount(int width)
    {
        ValidatePvrtcIDimension(width, nameof(width));
        var bitsPerTexel = BitsPerTexel;
        return bitsPerTexel switch
        {
            6 => checked(GetPvrtcStreamRowByteCount(width, 4) + GetPvrtcStreamRowByteCount(width, 2)),
            8 => checked(GetPvrtcStreamRowByteCount(width, 4) + GetPvrtcStreamRowByteCount(width, 4)),
            2 or 4 => GetPvrtcStreamRowByteCount(width, bitsPerTexel),
            _ => throw new InvalidOperationException($"Unsupported PVRTC bit rate '{bitsPerTexel}'.")
        };
    }

    private long GetPvrtcPayloadByteCount(int width, int height)
    {
        ValidatePvrtcIDimension(width, nameof(width));
        ValidatePvrtcIDimension(height, nameof(height));
        var bitsPerTexel = BitsPerTexel;
        return bitsPerTexel switch
        {
            6 => checked(GetPvrtcStreamByteCount(width, height, 4) + GetPvrtcStreamByteCount(width, height, 2)),
            8 => checked(GetPvrtcStreamByteCount(width, height, 4) + GetPvrtcStreamByteCount(width, height, 4)),
            2 or 4 => GetPvrtcStreamByteCount(width, height, bitsPerTexel),
            _ => throw new InvalidOperationException($"Unsupported PVRTC bit rate '{bitsPerTexel}'.")
        };
    }

    private long GetPvrtcStreamByteCount(int width, int height, int bitsPerTexel)
    {
        var rowByteCount = GetPvrtcStreamRowByteCount(width, bitsPerTexel);
        var blockCountY = Math.Max(RoundUpDiv(height, PvrtcWordHeight), GetPvrtcMinimumWordCount());
        return checked(rowByteCount * blockCountY);
    }

    private long GetPvrtcStreamRowByteCount(int width, int bitsPerTexel)
    {
        var wordWidth = bitsPerTexel == 2 ? 8 : 4;
        var blockCountX = Math.Max(RoundUpDiv(width, wordWidth), GetPvrtcMinimumWordCount());
        return checked(blockCountX * PvrtcWordByteCount);
    }

    private long GetPvrtcMinimumWordCount() =>
        SizeMode == TexturePayloadSizeMode.PvrtcI ? 2 : 1;

    private void ValidatePvrtcIDimension(int value, string parameterName)
    {
        if (SizeMode == TexturePayloadSizeMode.PvrtcI && !IsPowerOfTwo(value))
        {
            throw new ArgumentException("PVRTC I textures must have power-of-two dimensions.", parameterName);
        }
    }

    private static long RoundUpDiv(long value, long divisor) =>
        checked((value + divisor - 1) / divisor);

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}
