using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class SequentialUncompressedTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.R8
        || format == TextureFormats.Rg8
        || format == TextureFormats.Rgb8
        || format == TextureFormats.Rgba8UNorm
        || format == TextureFormats.Rgba8SNorm
        || format == TextureFormats.Rgba16UNorm
        || format == TextureFormats.Rgba16SNorm
        || format == TextureFormats.Rgba32UNorm
        || format == TextureFormats.Rgba32SNorm
        || format == TextureFormats.Rgba16Float
        || format == TextureFormats.Rgba32Float
        || format == TextureFormats.Bgra8;

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

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
        DecodeConverted(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeConverted(source, destination, rowPitch);
    }

    private void DecodeConverted<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = DecodeTexel<TPixel>(source.Slice(texelOffset, Format.BytesPerBlock));
                texelOffset = checked(texelOffset + Format.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeConverted<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                EncodeTexel(sourceRow[x], destination.Slice(texelOffset, Format.BytesPerBlock));
                texelOffset = checked(texelOffset + Format.BytesPerBlock);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private TPixel DecodeTexel<TPixel>(ReadOnlySpan<byte> texel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (Format.ValueKind == TextureValueKind.UNorm && GetMaxComponentBits() <= 8)
        {
            return TPixel.FromRgba8UNorm(DecodeRgba8UNorm(texel));
        }

        if (Format == TextureFormats.Rgba8SNorm)
        {
            return TPixel.FromRgba8SNorm(new Rgba8SNorm((sbyte)texel[0], (sbyte)texel[1], (sbyte)texel[2], (sbyte)texel[3]));
        }

        if (Format == TextureFormats.Rgba16UNorm)
        {
            return TPixel.FromRgba16UNorm(new Rgba16UNorm(
                BinaryPrimitives.ReadUInt16LittleEndian(texel),
                BinaryPrimitives.ReadUInt16LittleEndian(texel[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(texel[4..]),
                BinaryPrimitives.ReadUInt16LittleEndian(texel[6..])));
        }

        if (Format == TextureFormats.Rgba16SNorm)
        {
            return TPixel.FromRgba16SNorm(new Rgba16SNorm(
                BinaryPrimitives.ReadInt16LittleEndian(texel),
                BinaryPrimitives.ReadInt16LittleEndian(texel[2..]),
                BinaryPrimitives.ReadInt16LittleEndian(texel[4..]),
                BinaryPrimitives.ReadInt16LittleEndian(texel[6..])));
        }

        if (Format == TextureFormats.Rgba32UNorm)
        {
            return TPixel.FromRgba32UNorm(new Rgba32UNorm(
                BinaryPrimitives.ReadUInt32LittleEndian(texel),
                BinaryPrimitives.ReadUInt32LittleEndian(texel[4..]),
                BinaryPrimitives.ReadUInt32LittleEndian(texel[8..]),
                BinaryPrimitives.ReadUInt32LittleEndian(texel[12..])));
        }

        if (Format == TextureFormats.Rgba32SNorm)
        {
            return TPixel.FromRgba32SNorm(new Rgba32SNorm(
                BinaryPrimitives.ReadInt32LittleEndian(texel),
                BinaryPrimitives.ReadInt32LittleEndian(texel[4..]),
                BinaryPrimitives.ReadInt32LittleEndian(texel[8..]),
                BinaryPrimitives.ReadInt32LittleEndian(texel[12..])));
        }

        if (Format == TextureFormats.Rgba16Float)
        {
            return TPixel.FromRgba16Float(new Rgba16Float(
                BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(texel)),
                BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(texel[2..])),
                BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(texel[4..])),
                BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(texel[6..]))));
        }

        if (Format == TextureFormats.Rgba32Float)
        {
            return TPixel.FromRgba32Float(new Rgba32Float(
                BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(texel)),
                BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(texel[4..])),
                BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(texel[8..])),
                BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(texel[12..]))));
        }

        throw CreateUnsupportedFormatException(Format);
    }

    private void EncodeTexel<TPixel>(TPixel pixel, Span<byte> texel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (Format.ValueKind == TextureValueKind.UNorm && GetMaxComponentBits() <= 8)
        {
            EncodeRgba8UNorm(TPixel.ToRgba8UNorm(pixel), texel);
            return;
        }

        if (Format == TextureFormats.Rgba8SNorm)
        {
            var value = TPixel.ToRgba8SNorm(pixel);
            texel[0] = unchecked((byte)value.Red);
            texel[1] = unchecked((byte)value.Green);
            texel[2] = unchecked((byte)value.Blue);
            texel[3] = unchecked((byte)value.Alpha);
            return;
        }

        if (Format == TextureFormats.Rgba16UNorm)
        {
            var value = TPixel.ToRgba16UNorm(pixel);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, value.Red);
            BinaryPrimitives.WriteUInt16LittleEndian(texel[2..], value.Green);
            BinaryPrimitives.WriteUInt16LittleEndian(texel[4..], value.Blue);
            BinaryPrimitives.WriteUInt16LittleEndian(texel[6..], value.Alpha);
            return;
        }

        if (Format == TextureFormats.Rgba16SNorm)
        {
            var value = TPixel.ToRgba16SNorm(pixel);
            BinaryPrimitives.WriteInt16LittleEndian(texel, value.Red);
            BinaryPrimitives.WriteInt16LittleEndian(texel[2..], value.Green);
            BinaryPrimitives.WriteInt16LittleEndian(texel[4..], value.Blue);
            BinaryPrimitives.WriteInt16LittleEndian(texel[6..], value.Alpha);
            return;
        }

        if (Format == TextureFormats.Rgba32UNorm)
        {
            var value = TPixel.ToRgba32UNorm(pixel);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, value.Red);
            BinaryPrimitives.WriteUInt32LittleEndian(texel[4..], value.Green);
            BinaryPrimitives.WriteUInt32LittleEndian(texel[8..], value.Blue);
            BinaryPrimitives.WriteUInt32LittleEndian(texel[12..], value.Alpha);
            return;
        }

        if (Format == TextureFormats.Rgba32SNorm)
        {
            var value = TPixel.ToRgba32SNorm(pixel);
            BinaryPrimitives.WriteInt32LittleEndian(texel, value.Red);
            BinaryPrimitives.WriteInt32LittleEndian(texel[4..], value.Green);
            BinaryPrimitives.WriteInt32LittleEndian(texel[8..], value.Blue);
            BinaryPrimitives.WriteInt32LittleEndian(texel[12..], value.Alpha);
            return;
        }

        if (Format == TextureFormats.Rgba16Float)
        {
            var value = TPixel.ToRgba16Float(pixel);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, BitConverter.HalfToUInt16Bits(value.Red));
            BinaryPrimitives.WriteUInt16LittleEndian(texel[2..], BitConverter.HalfToUInt16Bits(value.Green));
            BinaryPrimitives.WriteUInt16LittleEndian(texel[4..], BitConverter.HalfToUInt16Bits(value.Blue));
            BinaryPrimitives.WriteUInt16LittleEndian(texel[6..], BitConverter.HalfToUInt16Bits(value.Alpha));
            return;
        }

        if (Format == TextureFormats.Rgba32Float)
        {
            var value = TPixel.ToRgba32Float(pixel);
            BinaryPrimitives.WriteInt32LittleEndian(texel, BitConverter.SingleToInt32Bits(value.Red));
            BinaryPrimitives.WriteInt32LittleEndian(texel[4..], BitConverter.SingleToInt32Bits(value.Green));
            BinaryPrimitives.WriteInt32LittleEndian(texel[8..], BitConverter.SingleToInt32Bits(value.Blue));
            BinaryPrimitives.WriteInt32LittleEndian(texel[12..], BitConverter.SingleToInt32Bits(value.Alpha));
            return;
        }

        throw CreateUnsupportedFormatException(Format);
    }

    private Rgba8UNorm DecodeRgba8UNorm(ReadOnlySpan<byte> texel)
    {
        if (Format == TextureFormats.R8)
        {
            return new Rgba8UNorm(texel[0], 0, 0);
        }

        if (Format == TextureFormats.Rg8)
        {
            return new Rgba8UNorm(texel[0], texel[1], 0);
        }

        if (Format == TextureFormats.Rgb8)
        {
            return new Rgba8UNorm(texel[0], texel[1], texel[2]);
        }

        if (Format == TextureFormats.Bgra8)
        {
            return new Rgba8UNorm(texel[2], texel[1], texel[0], texel[3]);
        }

        return new Rgba8UNorm(texel[0], texel[1], texel[2], texel[3]);
    }

    private void EncodeRgba8UNorm(Rgba8UNorm value, Span<byte> texel)
    {
        if (Format == TextureFormats.R8)
        {
            texel[0] = value.Red;
            return;
        }

        if (Format == TextureFormats.Rg8)
        {
            texel[0] = value.Red;
            texel[1] = value.Green;
            return;
        }

        if (Format == TextureFormats.Rgb8)
        {
            texel[0] = value.Red;
            texel[1] = value.Green;
            texel[2] = value.Blue;
            return;
        }

        if (Format == TextureFormats.Bgra8)
        {
            texel[0] = value.Blue;
            texel[1] = value.Green;
            texel[2] = value.Red;
            texel[3] = value.Alpha;
            return;
        }

        texel[0] = value.Red;
        texel[1] = value.Green;
        texel[2] = value.Blue;
        texel[3] = value.Alpha;
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded texture.", nameof(destination));
        }
    }

    private int GetMaxComponentBits() =>
        Math.Max(Math.Max(Format.RedBits, Format.GreenBits), Math.Max(Format.BlueBits, Format.AlphaBits));

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Sequential uncompressed texture coder does not support texture format '{format.Name}'.");
}
