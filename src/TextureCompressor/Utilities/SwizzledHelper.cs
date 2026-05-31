namespace TextureCompressor.Utilities;

public static class SwizzledHelper
{
    public static int GetLinearByteCount(int width, int height, int bitsPerElement)
    {
        ValidateDimensions(width, height);
        ValidateBitsPerElement(bitsPerElement);

        if (bitsPerElement == 4)
        {
            return checked(Align(width, 2) * height / 2);
        }

        return checked(width * height * (bitsPerElement / 8));
    }

    public static int GetStorageByteCount(int width, int height, int bitsPerElement)
    {
        ValidateDimensions(width, height);
        ValidateBitsPerElement(bitsPerElement);

        var storageWidth = EnclosingPowerOfTwo(width);
        var storageHeight = EnclosingPowerOfTwo(height);
        var elementCount = checked(storageWidth * storageHeight);
        if (bitsPerElement == 4)
        {
            return checked((elementCount + 1) / 2);
        }

        return checked(elementCount * (bitsPerElement / 8));
    }

    public static void Swizzle(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        int bitsPerElement)
    {
        ValidateBuffers(source, destination, width, height, bitsPerElement);

        if (bitsPerElement == 4)
        {
            Swizzle4Bits(source, destination, width, height);
            return;
        }

        var storageWidth = EnclosingPowerOfTwo(width);
        var storageHeight = EnclosingPowerOfTwo(height);
        var xMask = GetMortonNumber(storageWidth - 1, 0, storageWidth, storageHeight);
        var yMask = GetMortonNumber(0, storageHeight - 1, storageWidth, storageHeight);
        var elementSize = bitsPerElement / 8;

        var sourceOffset = 0;
        var yOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var xOffset = 0;
            for (var x = 0; x < width; x++)
            {
                var destinationOffset = checked((xOffset + yOffset) * elementSize);
                source.Slice(sourceOffset, elementSize).CopyTo(destination.Slice(destinationOffset, elementSize));
                sourceOffset = checked(sourceOffset + elementSize);
                xOffset = (xOffset - xMask) & xMask;
            }

            yOffset = (yOffset - yMask) & yMask;
        }
    }

    public static void Deswizzle(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        int bitsPerElement)
    {
        ValidateBuffers(destination, source, width, height, bitsPerElement);

        if (bitsPerElement == 4)
        {
            Deswizzle4Bits(source, destination, width, height);
            return;
        }

        var storageWidth = EnclosingPowerOfTwo(width);
        var storageHeight = EnclosingPowerOfTwo(height);
        var xMask = GetMortonNumber(storageWidth - 1, 0, storageWidth, storageHeight);
        var yMask = GetMortonNumber(0, storageHeight - 1, storageWidth, storageHeight);
        var elementSize = bitsPerElement / 8;

        var destinationOffset = 0;
        var yOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var xOffset = 0;
            for (var x = 0; x < width; x++)
            {
                var sourceOffset = checked((xOffset + yOffset) * elementSize);
                source.Slice(sourceOffset, elementSize).CopyTo(destination.Slice(destinationOffset, elementSize));
                destinationOffset = checked(destinationOffset + elementSize);
                xOffset = (xOffset - xMask) & xMask;
            }

            yOffset = (yOffset - yMask) & yMask;
        }
    }

    private static void Swizzle4Bits(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
    {
        var xMask = GetMortonNumber(width - 1, 0, width, height);
        var yMask = GetMortonNumber(0, height - 1, width, height);
        var lineStride = Align(width, 2);

        var yOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var xOffset = 0;
            for (var x = 0; x < width; x++)
            {
                CopyNibble(source, destination, y * lineStride + x, xOffset + yOffset);
                xOffset = (xOffset - xMask) & xMask;
            }

            yOffset = (yOffset - yMask) & yMask;
        }
    }

    private static void Deswizzle4Bits(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
    {
        var xMask = GetMortonNumber(width - 1, 0, width, height);
        var yMask = GetMortonNumber(0, height - 1, width, height);
        var lineStride = Align(width, 2);

        var yOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var xOffset = 0;
            for (var x = 0; x < width; x++)
            {
                CopyNibble(source, destination, xOffset + yOffset, y * lineStride + x);
                xOffset = (xOffset - xMask) & xMask;
            }

            yOffset = (yOffset - yMask) & yMask;
        }
    }

    private static void CopyNibble(ReadOnlySpan<byte> source, Span<byte> destination, int sourceNibbleOffset, int destinationNibbleOffset)
    {
        var sourceOffset = sourceNibbleOffset >> 1;
        var destinationOffset = destinationNibbleOffset >> 1;
        var sourceShift = (sourceNibbleOffset & 1) << 2;
        var destinationShift = (destinationNibbleOffset & 1) << 2;
        var nibble = (source[sourceOffset] >> sourceShift) & 0xf;
        destination[destinationOffset] = (byte)((destination[destinationOffset] & (0xf0 >> destinationShift)) | (nibble << destinationShift));
    }

    private static int GetMortonNumber(int x, int y, int width, int height)
    {
        var logWidth = BitScanReverse(width);
        var logHeight = BitScanReverse(height);
        var sharedBits = Math.Min(logWidth, logHeight);
        var morton = 0;

        for (var i = 0; i < sharedBits; i++)
        {
            morton |= ((x & (1 << i)) << (i + 1)) | ((y & (1 << i)) << i);
        }

        if (width < height)
        {
            morton |= (y & ~(width - 1)) << sharedBits;
        }
        else
        {
            morton |= (x & ~(height - 1)) << sharedBits;
        }

        return morton;
    }

    private static int EnclosingPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value)
        {
            result = checked(result << 1);
        }

        return result;
    }

    private static int BitScanReverse(int value)
    {
        var result = 0;
        while ((value >>= 1) != 0)
        {
            result++;
        }

        return result;
    }

    private static int Align(int value, int alignment) =>
        checked((value + alignment - 1) / alignment * alignment);

    private static void ValidateBuffers(
        ReadOnlySpan<byte> linear,
        ReadOnlySpan<byte> swizzled,
        int width,
        int height,
        int bitsPerElement)
    {
        var linearByteCount = GetLinearByteCount(width, height, bitsPerElement);
        if (linear.Length < linearByteCount)
        {
            throw new ArgumentException("Buffer is shorter than the linear data size.", nameof(linear));
        }

        var storageByteCount = GetStorageByteCount(width, height, bitsPerElement);
        if (swizzled.Length < storageByteCount)
        {
            throw new ArgumentException("Buffer is shorter than the swizzled storage size.", nameof(swizzled));
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }
    }

    private static void ValidateBitsPerElement(int bitsPerElement)
    {
        if (bitsPerElement != 4 && (bitsPerElement <= 0 || bitsPerElement % 8 != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerElement), "Element size must be 4 bits or a positive byte multiple.");
        }
    }
}
