namespace TextureCompressor.Utilities;

internal enum BigEndianByteSwapMode
{
    None,
    Swap8In16,
    Swap8In32,
    Swap16In32
}

internal static class BigEndianByteSwap
{
    public static void CopyToLittleEndian(ReadOnlySpan<byte> source, Span<byte> destination, BigEndianByteSwapMode mode)
    {
        source.CopyTo(destination);
        SwapInPlace(destination, mode);
    }

    public static void CopyFromLittleEndian(ReadOnlySpan<byte> source, Span<byte> destination, BigEndianByteSwapMode mode)
    {
        source.CopyTo(destination);
        SwapInPlace(destination, mode);
    }

    public static void SwapInPlace(Span<byte> data, BigEndianByteSwapMode mode)
    {
        switch (mode)
        {
            case BigEndianByteSwapMode.None:
                return;
            case BigEndianByteSwapMode.Swap8In16:
                Swap8In16(data);
                return;
            case BigEndianByteSwapMode.Swap8In32:
                Swap8In32(data);
                return;
            case BigEndianByteSwapMode.Swap16In32:
                Swap16In32(data);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static void Swap8In16(Span<byte> data)
    {
        ValidateLength(data, 2);
        for (var i = 0; i < data.Length; i += 2)
        {
            (data[i], data[i + 1]) = (data[i + 1], data[i]);
        }
    }

    private static void Swap8In32(Span<byte> data)
    {
        ValidateLength(data, 4);
        for (var i = 0; i < data.Length; i += 4)
        {
            (data[i], data[i + 3]) = (data[i + 3], data[i]);
            (data[i + 1], data[i + 2]) = (data[i + 2], data[i + 1]);
        }
    }

    private static void Swap16In32(Span<byte> data)
    {
        ValidateLength(data, 4);
        for (var i = 0; i < data.Length; i += 4)
        {
            (data[i], data[i + 2]) = (data[i + 2], data[i]);
            (data[i + 1], data[i + 3]) = (data[i + 3], data[i + 1]);
        }
    }

    private static void ValidateLength(Span<byte> data, int chunkSize)
    {
        if (data.Length % chunkSize != 0)
        {
            throw new ArgumentException("Big-endian byte swaps require complete chunks.", nameof(data));
        }
    }
}
