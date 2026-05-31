using TextureCompressor.Utilities;

namespace TextureCompressor.Tests;

public sealed class BigEndianByteSwapTests
{
    [Fact]
    public void SwapInPlaceLeavesDataUnchangedForNone()
    {
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };

        BigEndianByteSwap.SwapInPlace(data, BigEndianByteSwapMode.None);

        Assert.Equal([0x12, 0x34, 0x56, 0x78], data);
    }

    [Fact]
    public void SwapInPlaceSwaps8BitChunksIn16BitWords()
    {
        var data = new byte[] { 0x12, 0x34, 0xab, 0xcd };

        BigEndianByteSwap.SwapInPlace(data, BigEndianByteSwapMode.Swap8In16);

        Assert.Equal([0x34, 0x12, 0xcd, 0xab], data);
    }

    [Fact]
    public void SwapInPlaceSwaps8BitChunksIn32BitWords()
    {
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78, 0xab, 0xcd, 0xef, 0x01 };

        BigEndianByteSwap.SwapInPlace(data, BigEndianByteSwapMode.Swap8In32);

        Assert.Equal([0x78, 0x56, 0x34, 0x12, 0x01, 0xef, 0xcd, 0xab], data);
    }

    [Fact]
    public void SwapInPlaceSwaps16BitChunksIn32BitWords()
    {
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78, 0xab, 0xcd, 0xef, 0x01 };

        BigEndianByteSwap.SwapInPlace(data, BigEndianByteSwapMode.Swap16In32);

        Assert.Equal([0x56, 0x78, 0x12, 0x34, 0xef, 0x01, 0xab, 0xcd], data);
    }

    [Fact]
    public void CopyToLittleEndianCopiesAndSwapsSource()
    {
        var source = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var destination = new byte[source.Length];

        BigEndianByteSwap.CopyToLittleEndian(source, destination, BigEndianByteSwapMode.Swap8In32);

        Assert.Equal([0x12, 0x34, 0x56, 0x78], source);
        Assert.Equal([0x78, 0x56, 0x34, 0x12], destination);
    }

    [Fact]
    public void CopyFromLittleEndianCopiesAndSwapsSource()
    {
        var source = new byte[] { 0x78, 0x56, 0x34, 0x12 };
        var destination = new byte[source.Length];

        BigEndianByteSwap.CopyFromLittleEndian(source, destination, BigEndianByteSwapMode.Swap8In32);

        Assert.Equal([0x78, 0x56, 0x34, 0x12], source);
        Assert.Equal([0x12, 0x34, 0x56, 0x78], destination);
    }

    [Fact]
    public void SwapInPlaceRejectsIncompleteChunks()
    {
        var data = new byte[] { 0x12, 0x34, 0x56 };

        Assert.Throws<ArgumentException>(() =>
            BigEndianByteSwap.SwapInPlace(data, BigEndianByteSwapMode.Swap8In32));
    }
}
