using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class TextureArrayCoderTests
{
    [Fact]
    public void GlobalManagerReturns2DCoderAs3DDefault()
    {
        var coder2D = TextureCoderManager.Global.GetCoder(TextureFormats.Rgba8UNorm);
        var coder3D = TextureCoderManager.Global.GetCoder3D(TextureFormats.Rgba8UNorm);

        Assert.Equal(coder2D.Format, coder3D.Format);
        Assert.IsAssignableFrom<IPitchTextureCoder3D>(coder3D);
    }

    [Fact]
    public void Default3DCoderEncodesAndDecodesEachDepthSlice()
    {
        var source = new ArrayVolumeBitmap<Rgba8UNorm>(
            2,
            2,
            2,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8),
                new Rgba8UNorm(9, 10, 11, 12),
                new Rgba8UNorm(13, 14, 15, 16),
                new Rgba8UNorm(17, 18, 19, 20),
                new Rgba8UNorm(21, 22, 23, 24),
                new Rgba8UNorm(25, 26, 27, 28),
                new Rgba8UNorm(29, 30, 31, 32)
            ]);
        var decoded = new ArrayVolumeBitmap<Rgba8UNorm>(source.Width, source.Height, source.Depth);
        var coder = TextureCoderManager.Global.GetCoder3D(TextureFormats.Rgba8UNorm);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, source.Depth)];

        coder.Encode(source.AsView(), encoded);
        coder.Decode(encoded, decoded.AsView());

        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Fact]
    public void ManagerWrapsRegistered2DCoderAsDefault3D()
    {
        var manager = new TextureCoderManager();
        using var registration = manager.Register(TextureFormats.Rgba8UNorm, new RedChannelTextureCoder(TextureFormats.Rgba8UNorm));
        var source = new ArrayVolumeBitmap<Rgba8UNorm>(
            1,
            1,
            3,
            [
                new Rgba8UNorm(10, 0, 0),
                new Rgba8UNorm(20, 0, 0),
                new Rgba8UNorm(30, 0, 0)
            ]);
        var decoded = new ArrayVolumeBitmap<Rgba8UNorm>(1, 1, 3);
        var coder = manager.GetCoder3D(TextureFormats.Rgba8UNorm);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, source.Depth)];

        coder.Encode(source.AsView(), encoded);
        coder.Decode(encoded, decoded.AsView());

        Assert.Equal([10, 20, 30], encoded);
        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Fact]
    public void Registered3DCoderStillTakesPrecedence()
    {
        var manager = new TextureCoderManager();
        var registered = new ExplicitTextureCoder3D(TextureFormats.Rgba8UNorm);
        using var registration = manager.Register(TextureFormats.Rgba8UNorm, registered);

        var coder = manager.GetCoder3D(TextureFormats.Rgba8UNorm);

        Assert.Same(registered, coder);
    }

    [Fact]
    public void DefaultPitched3DCoderHonorsSlicePitch()
    {
        var source = new ArrayVolumeBitmap<Rgba8UNorm>(
            2,
            2,
            2,
            [
                new Rgba8UNorm(1, 2, 3, 4),
                new Rgba8UNorm(5, 6, 7, 8),
                new Rgba8UNorm(9, 10, 11, 12),
                new Rgba8UNorm(13, 14, 15, 16),
                new Rgba8UNorm(17, 18, 19, 20),
                new Rgba8UNorm(21, 22, 23, 24),
                new Rgba8UNorm(25, 26, 27, 28),
                new Rgba8UNorm(29, 30, 31, 32)
            ]);
        var decoded = new ArrayVolumeBitmap<Rgba8UNorm>(source.Width, source.Height, source.Depth);
        var coder = Assert.IsAssignableFrom<IPitchTextureCoder3D>(TextureCoderManager.Global.GetCoder3D(TextureFormats.Rgba8UNorm));
        var rowPitch = 12;
        var slicePitch = 32;
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height, source.Depth, rowPitch, slicePitch)];
        Array.Fill(encoded, (byte)0xCD);

        coder.Encode(source.AsView(), encoded, rowPitch, slicePitch);
        coder.Decode(encoded, decoded.AsView(), rowPitch, slicePitch);

        Assert.Equal(source.Pixels, decoded.Pixels);
        AssertPadding(encoded, rowPitch, slicePitch, source.Depth);
    }

    [Fact]
    public void DefaultPitched3DCoderRejectsTooSmallSlicePitch()
    {
        var coder = Assert.IsAssignableFrom<IPitchTextureCoder3D>(TextureCoderManager.Global.GetCoder3D(TextureFormats.Rgba8UNorm));
        var rowPitch = coder.GetDefaultPitch(2);
        var slicePitch = coder.GetDefaultSlicePitch(2, 2, rowPitch) - 1;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            coder.GetEncodedByteCount(2, 2, 2, rowPitch, slicePitch));

        Assert.Equal("slicePitch", exception.ParamName);
    }

    private static void AssertPadding(byte[] encoded, int rowPitch, int slicePitch, int depth)
    {
        for (var z = 0; z < depth; z++)
        {
            var sliceOffset = z * slicePitch;

            Assert.All(encoded.AsSpan(sliceOffset + 8, rowPitch - 8).ToArray(), value => Assert.Equal(0xCD, value));
            Assert.All(encoded.AsSpan(sliceOffset + rowPitch + 8, rowPitch - 8).ToArray(), value => Assert.Equal(0xCD, value));
            Assert.All(encoded.AsSpan(sliceOffset + (rowPitch * 2), slicePitch - (rowPitch * 2)).ToArray(), value => Assert.Equal(0xCD, value));
        }
    }

    private sealed class RedChannelTextureCoder(TextureFormat format) : ITextureCoder
    {
        public TextureFormat Format { get; } = format;

        public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (source.Length < GetEncodedByteCount(destination.Width, destination.Height))
            {
                throw new ArgumentException("Source span is too small.", nameof(source));
            }

            for (var i = 0; i < destination.Pixels.Length; i++)
            {
                destination.Pixels[i] = TPixel.FromRgba8UNorm(new Rgba8UNorm(source[i], 0, 0));
            }
        }

        public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (destination.Length < GetEncodedByteCount(source.Width, source.Height))
            {
                throw new ArgumentException("Destination span is too small.", nameof(destination));
            }

            for (var i = 0; i < source.Pixels.Length; i++)
            {
                destination[i] = TPixel.ToRgba8UNorm(source.Pixels[i]).Red;
            }
        }

        public int GetEncodedByteCount(int width, int height) => checked(width * height);
    }

    private sealed class ExplicitTextureCoder3D(TextureFormat format) : ITextureCoder, ITextureCoder3D
    {
        public TextureFormat Format { get; } = format;

        public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel> =>
            throw new NotSupportedException();

        public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination)
            where TPixel : unmanaged, IPixel<TPixel> =>
            throw new NotSupportedException();

        public int GetEncodedByteCount(int width, int height) => 0;

        public void Decode<TPixel>(ReadOnlySpan<byte> source, VolumeBitmapView<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel> =>
            throw new NotSupportedException();

        public void Encode<TPixel>(VolumeBitmapView<TPixel> source, Span<byte> destination)
            where TPixel : unmanaged, IPixel<TPixel> =>
            throw new NotSupportedException();

        public int GetEncodedByteCount(int width, int height, int depth) => 0;
    }
}
