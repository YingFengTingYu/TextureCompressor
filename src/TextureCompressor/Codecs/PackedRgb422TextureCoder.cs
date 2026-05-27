using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedRgb422TextureCoder : IPitchTextureCoder
{
    private readonly PackedRgb422Plan _plan;

    public PackedRgb422TextureCoder(TextureFormat format)
    {
        _plan = GetPlan(format);
        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.R8G8B8G8_422UNorm
        || format == TextureFormats.G8R8G8B8_422UNorm
        || format == TextureFormats.G8B8G8R8_422UNorm
        || format == TextureFormats.B8G8R8G8_422UNorm
        || format == TextureFormats.G16B16G16R16_422UNorm
        || format == TextureFormats.B16G16R16G16_422UNorm;

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ValidateDimensions(width, height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed row byte count.");
        }

        return checked(rowPitch * height);
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateSourceLength(destination.Width, destination.Height, source, rowPitch);

        var blocksPerRow = destination.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var block = source.Slice(blockOffset, _plan.BytesPerBlock);
                if (_plan.BitsPerComponent == 8)
                {
                    DecodeBlock8(block, out var first, out var second);
                    destinationRow[pixelX] = TPixel.FromRgba8UNorm(first);
                    destinationRow[pixelX + 1] = TPixel.FromRgba8UNorm(second);
                }
                else
                {
                    DecodeBlock16(block, out var first, out var second);
                    destinationRow[pixelX] = TPixel.FromRgba16UNorm(first);
                    destinationRow[pixelX + 1] = TPixel.FromRgba16UNorm(second);
                }

                blockOffset = checked(blockOffset + _plan.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var blocksPerRow = source.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var block = destination.Slice(blockOffset, _plan.BytesPerBlock);
                if (_plan.BitsPerComponent == 8)
                {
                    EncodeBlock8(TPixel.ToRgba8UNorm(sourceRow[pixelX]), TPixel.ToRgba8UNorm(sourceRow[pixelX + 1]), block);
                }
                else
                {
                    EncodeBlock16(TPixel.ToRgba16UNorm(sourceRow[pixelX]), TPixel.ToRgba16UNorm(sourceRow[pixelX + 1]), block);
                }

                blockOffset = checked(blockOffset + _plan.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodeBlock8(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second)
    {
        var c0 = block[0];
        var c1 = block[1];
        var c2 = block[2];
        var c3 = block[3];

        byte red;
        byte green0;
        byte green1;
        byte blue;
        switch (_plan.Layout)
        {
            case PackedRgb422Layout.RgBg:
                red = c0;
                green0 = c1;
                blue = c2;
                green1 = c3;
                break;
            case PackedRgb422Layout.GrGb:
                green0 = c0;
                red = c1;
                green1 = c2;
                blue = c3;
                break;
            case PackedRgb422Layout.GbGr:
                green0 = c0;
                blue = c1;
                green1 = c2;
                red = c3;
                break;
            case PackedRgb422Layout.BgRg:
                blue = c0;
                green0 = c1;
                red = c2;
                green1 = c3;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_plan));
        }

        first = new Rgba8UNorm(red, green0, blue);
        second = new Rgba8UNorm(red, green1, blue);
    }

    private void DecodeBlock16(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second)
    {
        var c0 = ReadUInt16(block, 0);
        var c1 = ReadUInt16(block, 1);
        var c2 = ReadUInt16(block, 2);
        var c3 = ReadUInt16(block, 3);

        ushort red;
        ushort green0;
        ushort green1;
        ushort blue;
        switch (_plan.Layout)
        {
            case PackedRgb422Layout.GbGr:
                green0 = c0;
                blue = c1;
                green1 = c2;
                red = c3;
                break;
            case PackedRgb422Layout.BgRg:
                blue = c0;
                green0 = c1;
                red = c2;
                green1 = c3;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_plan));
        }

        first = new Rgba16UNorm(red, green0, blue);
        second = new Rgba16UNorm(red, green1, blue);
    }

    private void EncodeBlock8(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block)
    {
        var red = AverageUNorm8(first.Red, second.Red);
        var blue = AverageUNorm8(first.Blue, second.Blue);

        switch (_plan.Layout)
        {
            case PackedRgb422Layout.RgBg:
                block[0] = red;
                block[1] = first.Green;
                block[2] = blue;
                block[3] = second.Green;
                break;
            case PackedRgb422Layout.GrGb:
                block[0] = first.Green;
                block[1] = red;
                block[2] = second.Green;
                block[3] = blue;
                break;
            case PackedRgb422Layout.GbGr:
                block[0] = first.Green;
                block[1] = blue;
                block[2] = second.Green;
                block[3] = red;
                break;
            case PackedRgb422Layout.BgRg:
                block[0] = blue;
                block[1] = first.Green;
                block[2] = red;
                block[3] = second.Green;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_plan));
        }
    }

    private void EncodeBlock16(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block)
    {
        var red = AverageUNorm16(first.Red, second.Red);
        var blue = AverageUNorm16(first.Blue, second.Blue);

        switch (_plan.Layout)
        {
            case PackedRgb422Layout.GbGr:
                WriteUInt16(block, 0, first.Green);
                WriteUInt16(block, 1, blue);
                WriteUInt16(block, 2, second.Green);
                WriteUInt16(block, 3, red);
                break;
            case PackedRgb422Layout.BgRg:
                WriteUInt16(block, 0, blue);
                WriteUInt16(block, 1, first.Green);
                WriteUInt16(block, 2, red);
                WriteUInt16(block, 3, second.Green);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_plan));
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded packed RGB 4:2:2 texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded packed RGB 4:2:2 texture.", nameof(destination));
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if ((width & 1) != 0)
        {
            throw new ArgumentException("Packed RGB 4:2:2 textures require an even width.", nameof(width));
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> block, int component) =>
        BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(component * sizeof(ushort), sizeof(ushort)));

    private static void WriteUInt16(Span<byte> block, int component, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(block.Slice(component * sizeof(ushort), sizeof(ushort)), value);

    private static byte AverageUNorm8(byte first, byte second) =>
        RgbaColorConversions.FloatToUNorm8(
            (RgbaColorConversions.UNorm8ToFloat(first) + RgbaColorConversions.UNorm8ToFloat(second)) * 0.5f);

    private static ushort AverageUNorm16(ushort first, ushort second) =>
        RgbaColorConversions.FloatToUNorm16(
            (RgbaColorConversions.UNorm16ToFloat(first) + RgbaColorConversions.UNorm16ToFloat(second)) * 0.5f);

    private static PackedRgb422Plan GetPlan(TextureFormat format)
    {
        if (format == TextureFormats.R8G8B8G8_422UNorm)
        {
            return new PackedRgb422Plan(PackedRgb422Layout.RgBg, 8, format.BytesPerBlock);
        }

        if (format == TextureFormats.G8R8G8B8_422UNorm)
        {
            return new PackedRgb422Plan(PackedRgb422Layout.GrGb, 8, format.BytesPerBlock);
        }

        if (format == TextureFormats.G8B8G8R8_422UNorm)
        {
            return new PackedRgb422Plan(PackedRgb422Layout.GbGr, 8, format.BytesPerBlock);
        }

        if (format == TextureFormats.B8G8R8G8_422UNorm)
        {
            return new PackedRgb422Plan(PackedRgb422Layout.BgRg, 8, format.BytesPerBlock);
        }

        if (format == TextureFormats.G16B16G16R16_422UNorm)
        {
            return new PackedRgb422Plan(PackedRgb422Layout.GbGr, 16, format.BytesPerBlock);
        }

        if (format == TextureFormats.B16G16R16G16_422UNorm)
        {
            return new PackedRgb422Plan(PackedRgb422Layout.BgRg, 16, format.BytesPerBlock);
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed RGB 4:2:2 texture coder does not support texture format '{format.Name}'.");

    private readonly record struct PackedRgb422Plan(PackedRgb422Layout Layout, int BitsPerComponent, int BytesPerBlock);

    private enum PackedRgb422Layout
    {
        RgBg,
        GrGb,
        GbGr,
        BgRg
    }
}
