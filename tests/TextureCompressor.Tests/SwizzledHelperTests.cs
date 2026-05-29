using TextureCompressor.Codecs;

namespace TextureCompressor.Tests;

public sealed class SwizzledHelperTests
{
    [Fact]
    public void Swizzle8BitsUsesMortonOrder()
    {
        byte[] source =
        [
            0, 1, 2, 3,
            4, 5, 6, 7,
            8, 9, 10, 11,
            12, 13, 14, 15,
        ];
        byte[] expected =
        [
            0, 4, 1, 5,
            8, 12, 9, 13,
            2, 6, 3, 7,
            10, 14, 11, 15,
        ];
        var swizzled = new byte[SwizzledHelper.GetStorageByteCount(4, 4, 8)];

        SwizzledHelper.Swizzle(source, swizzled, 4, 4, 8);

        Assert.Equal(expected, swizzled);
    }

    [Fact]
    public void Deswizzle8BitsUsesMortonOrder()
    {
        byte[] swizzled =
        [
            0, 4, 1, 5,
            8, 12, 9, 13,
            2, 6, 3, 7,
            10, 14, 11, 15,
        ];
        byte[] expected =
        [
            0, 1, 2, 3,
            4, 5, 6, 7,
            8, 9, 10, 11,
            12, 13, 14, 15,
        ];
        var linear = new byte[SwizzledHelper.GetLinearByteCount(4, 4, 8)];

        SwizzledHelper.Deswizzle(swizzled, linear, 4, 4, 8);

        Assert.Equal(expected, linear);
    }

    [Fact]
    public void SwizzleNonPowerOfTwoUsesExpandedStorage()
    {
        byte[] source =
        [
            0, 1, 2,
            3, 4, 5,
        ];
        byte[] expected =
        [
            0, 3, 1, 4,
            2, 5, 0xff, 0xff,
        ];
        var swizzled = new byte[SwizzledHelper.GetStorageByteCount(3, 2, 8)];
        Array.Fill(swizzled, (byte)0xff);

        SwizzledHelper.Swizzle(source, swizzled, 3, 2, 8);

        Assert.Equal(expected, swizzled);
    }

    [Fact]
    public void SwizzleAndDeswizzle4BitsRoundTrips()
    {
        byte[] source =
        [
            0x21, 0x43,
            0x65, 0x87,
        ];
        var swizzled = new byte[SwizzledHelper.GetStorageByteCount(4, 2, 4)];
        var linear = new byte[SwizzledHelper.GetLinearByteCount(4, 2, 4)];

        SwizzledHelper.Swizzle(source, swizzled, 4, 2, 4);
        SwizzledHelper.Deswizzle(swizzled, linear, 4, 2, 4);

        Assert.Equal(source, linear);
    }
}
