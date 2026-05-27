using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedUNormTextureCoder : IPitchTextureCoder
{
    private readonly PackedUNormTransfer _transfer;

    public PackedUNormTextureCoder(TextureFormat format)
    {
        if (!TryGetTransfer(format, out _transfer))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
    }

    public TextureFormat Format { get; }

    public static bool IsSupported(TextureFormat format) => TryGetTransfer(format, out _);

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
        DecodeByTransfer(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        EncodeByTransfer(source, destination, rowPitch);
    }

    private void DecodeByTransfer<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case PackedUNormTransfer.Alpha12:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Alpha12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance12:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Luminance12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance4Alpha4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Luminance4Alpha4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance6Alpha2:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Luminance6Alpha2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance12Alpha4:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Luminance12Alpha4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance12Alpha12:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Luminance12Alpha12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Intensity12:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Intensity12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rg4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R3G3B2:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, R3G3B2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R3G3B2Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, R3G3B2RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb5:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb5UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb565:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb565UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb565Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb565RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr565:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr565UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr565Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr565RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb10:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb10UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb12:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R10X6:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, R10X6UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg10X6:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rg10X6UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba10X6:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba10X6UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R12X4:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, R12X4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg12X4:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rg12X4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba12X4:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba12X4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R14X2:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, R14X2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg14X2:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rg14X2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba14X2:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba14X2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba2:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgba2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgba4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba4Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgba4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Argb4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Argb4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Argb4Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Argb4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Abgr4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Abgr4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Abgr4Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Abgr4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb5A1:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb5A1UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb5A1Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb5A1RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Rgb5:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Rgb5UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Rgb5Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Rgb5RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Bgr5:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Bgr5UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Bgr5Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Bgr5RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb10A2:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb10A2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb10A2Rev:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb10A2RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr10A2Rev:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Bgr10A2RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba12:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgra4:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgra4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgra4Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgra4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr5A1:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr5A1UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr5A1Rev:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr5A1RevUNormTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeByTransfer<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case PackedUNormTransfer.Alpha12:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Alpha12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance12:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Luminance12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance4Alpha4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Luminance4Alpha4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance6Alpha2:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Luminance6Alpha2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance12Alpha4:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Luminance12Alpha4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Luminance12Alpha12:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Luminance12Alpha12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Intensity12:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Intensity12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rg4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R3G3B2:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, R3G3B2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R3G3B2Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, R3G3B2RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb5:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb5UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb565:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb565UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb565Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb565RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr565:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr565UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr565Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr565RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb10:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb10UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb12:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R10X6:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, R10X6UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg10X6:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rg10X6UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba10X6:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba10X6UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R12X4:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, R12X4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg12X4:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rg12X4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba12X4:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba12X4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.R14X2:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, R14X2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rg14X2:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rg14X2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba14X2:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba14X2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba2:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgba2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgba4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba4Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgba4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Argb4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Argb4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Argb4Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Argb4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Abgr4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Abgr4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Abgr4Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Abgr4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb5A1:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb5A1UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb5A1Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Rgb5A1RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Rgb5:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Rgb5UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Rgb5Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Rgb5RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Bgr5:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Bgr5UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.A1Bgr5Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, A1Bgr5RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb10A2:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb10A2UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgb10A2Rev:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgb10A2RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr10A2Rev:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Bgr10A2RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Rgba12:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer, Rgba12UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgra4:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgra4UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgra4Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgra4RevUNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr5A1:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr5A1UNormTransfer>(source, destination, rowPitch);
                return;
            case PackedUNormTransfer.Bgr5A1Rev:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer, Bgr5A1RevUNormTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface IUNormCarrierTransfer<TCarrier>
    {
        static abstract TPixel FromCarrier<TPixel>(TCarrier value)
            where TPixel : unmanaged, IPixel<TPixel>;

        static abstract TCarrier ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel>;
    }

    private interface IPackedUNormTransfer<TCarrier>
    {
        static abstract int BytesPerTexel { get; }

        static abstract TCarrier Decode(ReadOnlySpan<byte> texel);

        static abstract void Encode(TCarrier value, Span<byte> texel);
    }

    private readonly struct Rgba8UNormTransfer : IUNormCarrierTransfer<Rgba8UNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba8UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba8UNorm(value);

        public static Rgba8UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba8UNorm(value);
    }

    private readonly struct Rgba16UNormTransfer : IUNormCarrierTransfer<Rgba16UNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba16UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba16UNorm(value);

        public static Rgba16UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba16UNorm(value);
    }

    private void Decode<TPixel, TCarrier, TCarrierTransfer, TTransfer>(
        ReadOnlySpan<byte> source,
        ImageView<TPixel> destination,
        int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TCarrierTransfer : IUNormCarrierTransfer<TCarrier>
        where TTransfer : IPackedUNormTransfer<TCarrier>
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var carrier = TTransfer.Decode(source.Slice(texelOffset, bytesPerTexel));
                destinationRow[x] = TCarrierTransfer.FromCarrier<TPixel>(carrier);
                texelOffset += bytesPerTexel;
            }

            rowOffset += rowPitch;
        }
    }

    private void Encode<TPixel, TCarrier, TCarrierTransfer, TTransfer>(
        ImageView<TPixel> source,
        Span<byte> destination,
        int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TCarrierTransfer : IUNormCarrierTransfer<TCarrier>
        where TTransfer : IPackedUNormTransfer<TCarrier>
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(
                    TCarrierTransfer.ToCarrier(sourceRow[x]),
                    destination.Slice(texelOffset, bytesPerTexel));
                texelOffset += bytesPerTexel;
            }

            rowOffset += rowPitch;
        }
    }

    private readonly struct Alpha12UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var alpha = (uint)BinaryPrimitives.ReadUInt16LittleEndian(texel) & 0x0fffu;
            return new Rgba16UNorm(0, 0, 0, (ushort)((alpha << 4) | (alpha >> 8)));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt16LittleEndian(texel, (ushort)((uint)value.Alpha >> 4));
    }

    private readonly struct Luminance12UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var luminance12 = (uint)BinaryPrimitives.ReadUInt16LittleEndian(texel) & 0x0fffu;
            var value = (ushort)((luminance12 << 4) | (luminance12 >> 8));
            return new Rgba16UNorm(value, value, value);
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt16LittleEndian(texel, (ushort)((uint)value.Red >> 4));
    }

    private readonly struct Luminance4Alpha4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 1;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = texel[0];
            var luminance4 = (uint)packed >> 4;
            var alpha4 = (uint)packed & 0x0fu;
            var luminance = (byte)((luminance4 << 4) | luminance4);
            return new Rgba8UNorm(
                luminance,
                luminance,
                luminance,
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var luminance = (uint)value.Red >> 4;
            var alpha = (uint)value.Alpha >> 4;
            texel[0] = (byte)((luminance << 4) | alpha);
        }
    }

    private readonly struct Luminance6Alpha2UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 1;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = texel[0];
            var luminance6 = (uint)packed >> 2;
            var alpha2 = (uint)packed & 0x03u;
            var luminance = (byte)((luminance6 << 2) | (luminance6 >> 4));
            return new Rgba8UNorm(
                luminance,
                luminance,
                luminance,
                (byte)((alpha2 << 6) | (alpha2 << 4) | (alpha2 << 2) | alpha2));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var luminance = (uint)value.Red >> 2;
            var alpha = (uint)value.Alpha >> 6;
            texel[0] = (byte)((luminance << 2) | alpha);
        }
    }

    private readonly struct Luminance12Alpha4UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var luminance12 = (uint)packed >> 4;
            var alpha4 = (uint)packed & 0x000fu;
            var luminance = (ushort)((luminance12 << 4) | (luminance12 >> 8));
            return new Rgba16UNorm(
                luminance,
                luminance,
                luminance,
                (ushort)((alpha4 << 12) | (alpha4 << 8) | (alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var luminance = (uint)value.Red >> 4;
            var alpha = (uint)value.Alpha >> 12;
            var packed = (ushort)((luminance << 4) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Luminance12Alpha12UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 3;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = (uint)BinaryPrimitives.ReadUInt16LittleEndian(texel)
                | ((uint)texel[2] << 16);
            var luminance12 = (packed >> 12) & 0x0fffu;
            var alpha12 = packed & 0x0fffu;
            var luminance = (ushort)((luminance12 << 4) | (luminance12 >> 8));
            return new Rgba16UNorm(
                luminance,
                luminance,
                luminance,
                (ushort)((alpha12 << 4) | (alpha12 >> 8)));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var luminance = (uint)value.Red >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (luminance << 12) | alpha;
            BinaryPrimitives.WriteUInt16LittleEndian(texel, (ushort)packed);
            texel[2] = (byte)(packed >> 16);
        }
    }

    private readonly struct Intensity12UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var intensity12 = (uint)BinaryPrimitives.ReadUInt16LittleEndian(texel) & 0x0fffu;
            var value = (ushort)((intensity12 << 4) | (intensity12 >> 8));
            return new Rgba16UNorm(value, value, value, value);
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            BinaryPrimitives.WriteUInt16LittleEndian(texel, (ushort)((uint)value.Red >> 4));
    }

    private readonly struct Rg4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 1;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = texel[0];
            var red4 = (uint)packed >> 4;
            var green4 = (uint)packed & 0x0fu;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                0);
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            texel[0] = (byte)((red << 4) | green);
        }
    }

    private readonly struct R3G3B2UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 1;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = texel[0];
            var red3 = (uint)packed >> 5;
            var green3 = ((uint)packed >> 2) & 0x07u;
            var blue2 = (uint)packed & 0x03u;
            return new Rgba8UNorm(
                (byte)((red3 << 5) | (red3 << 2) | (red3 >> 1)),
                (byte)((green3 << 5) | (green3 << 2) | (green3 >> 1)),
                (byte)((blue2 << 6) | (blue2 << 4) | (blue2 << 2) | blue2));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 5;
            var green = (uint)value.Green >> 5;
            var blue = (uint)value.Blue >> 6;
            texel[0] = (byte)((red << 5) | (green << 2) | blue);
        }
    }

    private readonly struct R3G3B2RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 1;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = texel[0];
            var red3 = (uint)packed & 0x07u;
            var green3 = ((uint)packed >> 3) & 0x07u;
            var blue2 = (uint)packed >> 6;
            return new Rgba8UNorm(
                (byte)((red3 << 5) | (red3 << 2) | (red3 >> 1)),
                (byte)((green3 << 5) | (green3 << 2) | (green3 >> 1)),
                (byte)((blue2 << 6) | (blue2 << 4) | (blue2 << 2) | blue2));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 5;
            var green = (uint)value.Green >> 5;
            var blue = (uint)value.Blue >> 6;
            texel[0] = (byte)(red | (green << 3) | (blue << 6));
        }
    }

    private readonly struct Rgb4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = ((uint)packed >> 8) & 0x0fu;
            var green4 = ((uint)packed >> 4) & 0x0fu;
            var blue4 = (uint)packed & 0x0fu;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var packed = (ushort)((red << 8) | (green << 4) | blue);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb5UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 10) & 0x1fu;
            var green5 = ((uint)packed >> 5) & 0x1fu;
            var blue5 = (uint)packed & 0x1fu;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var packed = (ushort)((red << 10) | (green << 5) | blue);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb565UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 11) & 0x1fu;
            var green6 = ((uint)packed >> 5) & 0x3fu;
            var blue5 = (uint)packed & 0x1fu;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green6 << 2) | (green6 >> 4)),
                (byte)((blue5 << 3) | (blue5 >> 2)));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 2;
            var blue = (uint)value.Blue >> 3;
            var packed = (ushort)((red << 11) | (green << 5) | blue);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb565RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = (uint)packed & 0x1fu;
            var green6 = ((uint)packed >> 5) & 0x3fu;
            var blue5 = (uint)packed >> 11;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green6 << 2) | (green6 >> 4)),
                (byte)((blue5 << 3) | (blue5 >> 2)));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 2;
            var blue = (uint)value.Blue >> 3;
            var packed = (ushort)(red | (green << 5) | (blue << 11));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr565UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = (uint)packed & 0x1fu;
            var green6 = ((uint)packed >> 5) & 0x3fu;
            var blue5 = (uint)packed >> 11;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green6 << 2) | (green6 >> 4)),
                (byte)((blue5 << 3) | (blue5 >> 2)));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 2;
            var blue = (uint)value.Blue >> 3;
            var packed = (ushort)(red | (green << 5) | (blue << 11));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr565RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 11) & 0x1fu;
            var green6 = ((uint)packed >> 5) & 0x3fu;
            var blue5 = (uint)packed & 0x1fu;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green6 << 2) | (green6 >> 4)),
                (byte)((blue5 << 3) | (blue5 >> 2)));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 2;
            var blue = (uint)value.Blue >> 3;
            var packed = (ushort)((red << 11) | (green << 5) | blue);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb10UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            var red10 = (packed >> 20) & 0x03ffu;
            var green10 = (packed >> 10) & 0x03ffu;
            var blue10 = packed & 0x03ffu;
            return new Rgba16UNorm(
                (ushort)((red10 << 6) | (red10 >> 4)),
                (ushort)((green10 << 6) | (green10 >> 4)),
                (ushort)((blue10 << 6) | (blue10 >> 4)));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 6;
            var green = (uint)value.Green >> 6;
            var blue = (uint)value.Blue >> 6;
            var packed = (red << 20) | (green << 10) | blue;
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb12UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 5;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(texel)
                | ((ulong)texel[4] << 32);
            var red12 = (uint)((packed >> 24) & 0x0ffful);
            var green12 = (uint)((packed >> 12) & 0x0ffful);
            var blue12 = (uint)packed & 0x0fffu;
            return new Rgba16UNorm(
                (ushort)((red12 << 4) | (red12 >> 8)),
                (ushort)((green12 << 4) | (green12 >> 8)),
                (ushort)((blue12 << 4) | (blue12 >> 8)));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var red = (ulong)((uint)value.Red >> 4);
            var green = (ulong)((uint)value.Green >> 4);
            var blue = (uint)value.Blue >> 4;
            var packed = (red << 24) | (green << 12) | blue;
            BinaryPrimitives.WriteUInt32LittleEndian(texel, (uint)packed);
            texel[4] = (byte)(packed >> 32);
        }
    }

    private readonly struct R10X6UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(DecodePaddedWord(texel, 0, 10), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodePaddedWord(value.Red, texel, 0, 10);
    }

    private readonly struct Rg10X6UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(
            DecodePaddedWord(texel, 0, 10),
            DecodePaddedWord(texel, 2, 10),
            0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            EncodePaddedWord(value.Red, texel, 0, 10);
            EncodePaddedWord(value.Green, texel, 2, 10);
        }
    }

    private readonly struct Rgba10X6UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 8;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(
            DecodePaddedWord(texel, 0, 10),
            DecodePaddedWord(texel, 2, 10),
            DecodePaddedWord(texel, 4, 10),
            DecodePaddedWord(texel, 6, 10));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            EncodePaddedWord(value.Red, texel, 0, 10);
            EncodePaddedWord(value.Green, texel, 2, 10);
            EncodePaddedWord(value.Blue, texel, 4, 10);
            EncodePaddedWord(value.Alpha, texel, 6, 10);
        }
    }

    private readonly struct R12X4UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(DecodePaddedWord(texel, 0, 12), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodePaddedWord(value.Red, texel, 0, 12);
    }

    private readonly struct Rg12X4UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(
            DecodePaddedWord(texel, 0, 12),
            DecodePaddedWord(texel, 2, 12),
            0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            EncodePaddedWord(value.Red, texel, 0, 12);
            EncodePaddedWord(value.Green, texel, 2, 12);
        }
    }

    private readonly struct Rgba12X4UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 8;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(
            DecodePaddedWord(texel, 0, 12),
            DecodePaddedWord(texel, 2, 12),
            DecodePaddedWord(texel, 4, 12),
            DecodePaddedWord(texel, 6, 12));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            EncodePaddedWord(value.Red, texel, 0, 12);
            EncodePaddedWord(value.Green, texel, 2, 12);
            EncodePaddedWord(value.Blue, texel, 4, 12);
            EncodePaddedWord(value.Alpha, texel, 6, 12);
        }
    }

    private readonly struct R14X2UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(DecodePaddedWord(texel, 0, 14), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodePaddedWord(value.Red, texel, 0, 14);
    }

    private readonly struct Rg14X2UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(
            DecodePaddedWord(texel, 0, 14),
            DecodePaddedWord(texel, 2, 14),
            0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            EncodePaddedWord(value.Red, texel, 0, 14);
            EncodePaddedWord(value.Green, texel, 2, 14);
        }
    }

    private readonly struct Rgba14X2UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 8;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(
            DecodePaddedWord(texel, 0, 14),
            DecodePaddedWord(texel, 2, 14),
            DecodePaddedWord(texel, 4, 14),
            DecodePaddedWord(texel, 6, 14));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            EncodePaddedWord(value.Red, texel, 0, 14);
            EncodePaddedWord(value.Green, texel, 2, 14);
            EncodePaddedWord(value.Blue, texel, 4, 14);
            EncodePaddedWord(value.Alpha, texel, 6, 14);
        }
    }

    private readonly struct Rgba2UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 1;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = texel[0];
            var red2 = (uint)packed >> 6;
            var green2 = ((uint)packed >> 4) & 0x03u;
            var blue2 = ((uint)packed >> 2) & 0x03u;
            var alpha2 = (uint)packed & 0x03u;
            return new Rgba8UNorm(
                (byte)((red2 << 6) | (red2 << 4) | (red2 << 2) | red2),
                (byte)((green2 << 6) | (green2 << 4) | (green2 << 2) | green2),
                (byte)((blue2 << 6) | (blue2 << 4) | (blue2 << 2) | blue2),
                (byte)((alpha2 << 6) | (alpha2 << 4) | (alpha2 << 2) | alpha2));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 6;
            var green = (uint)value.Green >> 6;
            var blue = (uint)value.Blue >> 6;
            var alpha = (uint)value.Alpha >> 6;
            texel[0] = (byte)((red << 6) | (green << 4) | (blue << 2) | alpha);
        }
    }

    private readonly struct Rgba4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = (uint)packed >> 12;
            var green4 = ((uint)packed >> 8) & 0x0fu;
            var blue4 = ((uint)packed >> 4) & 0x0fu;
            var alpha4 = (uint)packed & 0x0fu;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)((red << 12) | (green << 8) | (blue << 4) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgba4RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = (uint)packed & 0x0fu;
            var green4 = ((uint)packed >> 4) & 0x0fu;
            var blue4 = ((uint)packed >> 8) & 0x0fu;
            var alpha4 = (uint)packed >> 12;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)(red | (green << 4) | (blue << 8) | (alpha << 12));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Argb4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = ((uint)packed >> 8) & 0x0fu;
            var green4 = ((uint)packed >> 4) & 0x0fu;
            var blue4 = (uint)packed & 0x0fu;
            var alpha4 = (uint)packed >> 12;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)((red << 8) | (green << 4) | blue | (alpha << 12));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Argb4RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = ((uint)packed >> 4) & 0x0fu;
            var green4 = ((uint)packed >> 8) & 0x0fu;
            var blue4 = (uint)packed >> 12;
            var alpha4 = (uint)packed & 0x0fu;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)((red << 4) | (green << 8) | (blue << 12) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Abgr4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = (uint)packed & 0x0fu;
            var green4 = ((uint)packed >> 4) & 0x0fu;
            var blue4 = ((uint)packed >> 8) & 0x0fu;
            var alpha4 = (uint)packed >> 12;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)(red | (green << 4) | (blue << 8) | (alpha << 12));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Abgr4RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = (uint)packed >> 12;
            var green4 = ((uint)packed >> 8) & 0x0fu;
            var blue4 = ((uint)packed >> 4) & 0x0fu;
            var alpha4 = (uint)packed & 0x0fu;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)((red << 12) | (green << 8) | (blue << 4) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb5A1UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = (uint)packed >> 11;
            var green5 = ((uint)packed >> 6) & 0x1fu;
            var blue5 = ((uint)packed >> 1) & 0x1fu;
            var alpha1 = (uint)packed & 0x0001u;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)((red << 11) | (green << 6) | (blue << 1) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb5A1RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = (uint)packed & 0x1fu;
            var green5 = ((uint)packed >> 5) & 0x1fu;
            var blue5 = ((uint)packed >> 10) & 0x1fu;
            var alpha1 = (uint)packed >> 15;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)(red | (green << 5) | (blue << 10) | (alpha << 15));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct A1Rgb5UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 10) & 0x1fu;
            var green5 = ((uint)packed >> 5) & 0x1fu;
            var blue5 = (uint)packed & 0x1fu;
            var alpha1 = (uint)packed >> 15;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)((red << 10) | (green << 5) | blue | (alpha << 15));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct A1Rgb5RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 1) & 0x1fu;
            var green5 = ((uint)packed >> 6) & 0x1fu;
            var blue5 = (uint)packed >> 11;
            var alpha1 = (uint)packed & 0x0001u;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)((red << 1) | (green << 6) | (blue << 11) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct A1Bgr5UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = (uint)packed & 0x1fu;
            var green5 = ((uint)packed >> 5) & 0x1fu;
            var blue5 = ((uint)packed >> 10) & 0x1fu;
            var alpha1 = (uint)packed >> 15;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)(red | (green << 5) | (blue << 10) | (alpha << 15));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct A1Bgr5RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = (uint)packed >> 11;
            var green5 = ((uint)packed >> 6) & 0x1fu;
            var blue5 = ((uint)packed >> 1) & 0x1fu;
            var alpha1 = (uint)packed & 0x0001u;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)((red << 11) | (green << 6) | (blue << 1) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb10A2UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            var red10 = (packed >> 22) & 0x03ffu;
            var green10 = (packed >> 12) & 0x03ffu;
            var blue10 = (packed >> 2) & 0x03ffu;
            var alpha2 = packed & 0x0003u;
            return new Rgba16UNorm(
                (ushort)((red10 << 6) | (red10 >> 4)),
                (ushort)((green10 << 6) | (green10 >> 4)),
                (ushort)((blue10 << 6) | (blue10 >> 4)),
                (ushort)((alpha2 << 14) | (alpha2 << 12) | (alpha2 << 10) | (alpha2 << 8) | (alpha2 << 6) | (alpha2 << 4) | (alpha2 << 2) | alpha2));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 6;
            var green = (uint)value.Green >> 6;
            var blue = (uint)value.Blue >> 6;
            var alpha = (uint)value.Alpha >> 14;
            var packed = (red << 22) | (green << 12) | (blue << 2) | alpha;
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgb10A2RevUNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            var red10 = packed & 0x03ffu;
            var green10 = (packed >> 10) & 0x03ffu;
            var blue10 = (packed >> 20) & 0x03ffu;
            var alpha2 = packed >> 30;
            return new Rgba16UNorm(
                (ushort)((red10 << 6) | (red10 >> 4)),
                (ushort)((green10 << 6) | (green10 >> 4)),
                (ushort)((blue10 << 6) | (blue10 >> 4)),
                (ushort)((alpha2 << 14) | (alpha2 << 12) | (alpha2 << 10) | (alpha2 << 8) | (alpha2 << 6) | (alpha2 << 4) | (alpha2 << 2) | alpha2));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 6;
            var green = (uint)value.Green >> 6;
            var blue = (uint)value.Blue >> 6;
            var alpha = (uint)value.Alpha >> 14;
            var packed = red | (green << 10) | (blue << 20) | (alpha << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr10A2RevUNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 4;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(texel);
            var red10 = (packed >> 20) & 0x03ffu;
            var green10 = (packed >> 10) & 0x03ffu;
            var blue10 = packed & 0x03ffu;
            var alpha2 = packed >> 30;
            return new Rgba16UNorm(
                (ushort)((red10 << 6) | (red10 >> 4)),
                (ushort)((green10 << 6) | (green10 >> 4)),
                (ushort)((blue10 << 6) | (blue10 >> 4)),
                (ushort)((alpha2 << 14) | (alpha2 << 12) | (alpha2 << 10) | (alpha2 << 8) | (alpha2 << 6) | (alpha2 << 4) | (alpha2 << 2) | alpha2));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 6;
            var green = (uint)value.Green >> 6;
            var blue = (uint)value.Blue >> 6;
            var alpha = (uint)value.Alpha >> 14;
            var packed = (red << 20) | (green << 10) | blue | (alpha << 30);
            BinaryPrimitives.WriteUInt32LittleEndian(texel, packed);
        }
    }

    private readonly struct Rgba12UNormTransfer : IPackedUNormTransfer<Rgba16UNorm>
    {
        public static int BytesPerTexel => 6;

        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(texel)
                | ((ulong)BinaryPrimitives.ReadUInt16LittleEndian(texel[4..]) << 32);
            var red12 = (uint)((packed >> 36) & 0x0ffful);
            var green12 = (uint)((packed >> 24) & 0x0ffful);
            var blue12 = (uint)((packed >> 12) & 0x0ffful);
            var alpha12 = (uint)packed & 0x0fffu;
            return new Rgba16UNorm(
                (ushort)((red12 << 4) | (red12 >> 8)),
                (ushort)((green12 << 4) | (green12 >> 8)),
                (ushort)((blue12 << 4) | (blue12 >> 8)),
                (ushort)((alpha12 << 4) | (alpha12 >> 8)));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            var red = (ulong)((uint)value.Red >> 4);
            var green = (ulong)((uint)value.Green >> 4);
            var blue = (ulong)((uint)value.Blue >> 4);
            var alpha = (uint)value.Alpha >> 4;
            var packed = (red << 36) | (green << 24) | (blue << 12) | alpha;
            BinaryPrimitives.WriteUInt32LittleEndian(texel, (uint)packed);
            BinaryPrimitives.WriteUInt16LittleEndian(texel[4..], (ushort)(packed >> 32));
        }
    }

    private readonly struct Bgra4UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = ((uint)packed >> 4) & 0x0fu;
            var green4 = ((uint)packed >> 8) & 0x0fu;
            var blue4 = (uint)packed >> 12;
            var alpha4 = (uint)packed & 0x0fu;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)((red << 4) | (green << 8) | (blue << 12) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgra4RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red4 = ((uint)packed >> 8) & 0x0fu;
            var green4 = ((uint)packed >> 4) & 0x0fu;
            var blue4 = (uint)packed & 0x0fu;
            var alpha4 = (uint)packed >> 12;
            return new Rgba8UNorm(
                (byte)((red4 << 4) | red4),
                (byte)((green4 << 4) | green4),
                (byte)((blue4 << 4) | blue4),
                (byte)((alpha4 << 4) | alpha4));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 4;
            var green = (uint)value.Green >> 4;
            var blue = (uint)value.Blue >> 4;
            var alpha = (uint)value.Alpha >> 4;
            var packed = (ushort)((red << 8) | (green << 4) | blue | (alpha << 12));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr5A1UNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 1) & 0x1fu;
            var green5 = ((uint)packed >> 6) & 0x1fu;
            var blue5 = (uint)packed >> 11;
            var alpha1 = (uint)packed & 0x0001u;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)((red << 1) | (green << 6) | (blue << 11) | alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private readonly struct Bgr5A1RevUNormTransfer : IPackedUNormTransfer<Rgba8UNorm>
    {
        public static int BytesPerTexel => 2;

        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var packed = BinaryPrimitives.ReadUInt16LittleEndian(texel);
            var red5 = ((uint)packed >> 10) & 0x1fu;
            var green5 = ((uint)packed >> 5) & 0x1fu;
            var blue5 = (uint)packed & 0x1fu;
            var alpha1 = (uint)packed >> 15;
            return new Rgba8UNorm(
                (byte)((red5 << 3) | (red5 >> 2)),
                (byte)((green5 << 3) | (green5 >> 2)),
                (byte)((blue5 << 3) | (blue5 >> 2)),
                (byte)((alpha1 << 8) - alpha1));
        }

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            var red = (uint)value.Red >> 3;
            var green = (uint)value.Green >> 3;
            var blue = (uint)value.Blue >> 3;
            var alpha = (uint)value.Alpha >> 7;
            var packed = (ushort)((red << 10) | (green << 5) | blue | (alpha << 15));
            BinaryPrimitives.WriteUInt16LittleEndian(texel, packed);
        }
    }

    private static ushort DecodePaddedWord(ReadOnlySpan<byte> texel, int offset, int bits)
    {
        var shift = 16 - bits;
        var value = (uint)BinaryPrimitives.ReadUInt16LittleEndian(texel.Slice(offset, sizeof(ushort))) >> shift;
        return (ushort)((value << shift) | (value >> (bits - shift)));
    }

    private static void EncodePaddedWord(ushort value, Span<byte> texel, int offset, int bits)
    {
        var shift = 16 - bits;
        var packed = (ushort)(((uint)value >> shift) << shift);
        BinaryPrimitives.WriteUInt16LittleEndian(texel.Slice(offset, sizeof(ushort)), packed);
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded packed UNorm texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded packed UNorm texture.", nameof(destination));
        }
    }

    private static bool TryGetTransfer(TextureFormat format, out PackedUNormTransfer transfer)
    {
        if (format == TextureFormats.Alpha12UNorm)
        {
            transfer = PackedUNormTransfer.Alpha12;
            return true;
        }

        if (format == TextureFormats.Luminance12UNorm)
        {
            transfer = PackedUNormTransfer.Luminance12;
            return true;
        }

        if (format == TextureFormats.Luminance4Alpha4UNorm)
        {
            transfer = PackedUNormTransfer.Luminance4Alpha4;
            return true;
        }

        if (format == TextureFormats.Luminance6Alpha2UNorm)
        {
            transfer = PackedUNormTransfer.Luminance6Alpha2;
            return true;
        }

        if (format == TextureFormats.Luminance12Alpha4UNorm)
        {
            transfer = PackedUNormTransfer.Luminance12Alpha4;
            return true;
        }

        if (format == TextureFormats.Luminance12Alpha12UNorm)
        {
            transfer = PackedUNormTransfer.Luminance12Alpha12;
            return true;
        }

        if (format == TextureFormats.Intensity12UNorm)
        {
            transfer = PackedUNormTransfer.Intensity12;
            return true;
        }

        if (format == TextureFormats.Rg4UNorm)
        {
            transfer = PackedUNormTransfer.Rg4;
            return true;
        }

        if (format == TextureFormats.R3G3B2UNorm)
        {
            transfer = PackedUNormTransfer.R3G3B2;
            return true;
        }

        if (format == TextureFormats.R3G3B2RevUNorm)
        {
            transfer = PackedUNormTransfer.R3G3B2Rev;
            return true;
        }

        if (format == TextureFormats.Rgb4UNorm)
        {
            transfer = PackedUNormTransfer.Rgb4;
            return true;
        }

        if (format == TextureFormats.Rgb5UNorm)
        {
            transfer = PackedUNormTransfer.Rgb5;
            return true;
        }

        if (format == TextureFormats.Rgb565UNorm)
        {
            transfer = PackedUNormTransfer.Rgb565;
            return true;
        }

        if (format == TextureFormats.Rgb565RevUNorm)
        {
            transfer = PackedUNormTransfer.Rgb565Rev;
            return true;
        }

        if (format == TextureFormats.Bgr565UNorm)
        {
            transfer = PackedUNormTransfer.Bgr565;
            return true;
        }

        if (format == TextureFormats.Bgr565RevUNorm)
        {
            transfer = PackedUNormTransfer.Bgr565Rev;
            return true;
        }

        if (format == TextureFormats.Rgb10UNorm)
        {
            transfer = PackedUNormTransfer.Rgb10;
            return true;
        }

        if (format == TextureFormats.Rgb12UNorm)
        {
            transfer = PackedUNormTransfer.Rgb12;
            return true;
        }

        if (format == TextureFormats.Rgba2UNorm)
        {
            transfer = PackedUNormTransfer.Rgba2;
            return true;
        }

        if (format == TextureFormats.R10X6UNorm)
        {
            transfer = PackedUNormTransfer.R10X6;
            return true;
        }

        if (format == TextureFormats.R10X6G10X6UNorm)
        {
            transfer = PackedUNormTransfer.Rg10X6;
            return true;
        }

        if (format == TextureFormats.R10X6G10X6B10X6A10X6UNorm)
        {
            transfer = PackedUNormTransfer.Rgba10X6;
            return true;
        }

        if (format == TextureFormats.R12X4UNorm)
        {
            transfer = PackedUNormTransfer.R12X4;
            return true;
        }

        if (format == TextureFormats.R12X4G12X4UNorm)
        {
            transfer = PackedUNormTransfer.Rg12X4;
            return true;
        }

        if (format == TextureFormats.R12X4G12X4B12X4A12X4UNorm)
        {
            transfer = PackedUNormTransfer.Rgba12X4;
            return true;
        }

        if (format == TextureFormats.R14X2UNorm)
        {
            transfer = PackedUNormTransfer.R14X2;
            return true;
        }

        if (format == TextureFormats.R14X2G14X2UNorm)
        {
            transfer = PackedUNormTransfer.Rg14X2;
            return true;
        }

        if (format == TextureFormats.R14X2G14X2B14X2A14X2UNorm)
        {
            transfer = PackedUNormTransfer.Rgba14X2;
            return true;
        }

        if (format == TextureFormats.Rgba4UNorm)
        {
            transfer = PackedUNormTransfer.Rgba4;
            return true;
        }

        if (format == TextureFormats.Rgba4RevUNorm)
        {
            transfer = PackedUNormTransfer.Rgba4Rev;
            return true;
        }

        if (format == TextureFormats.Argb4UNorm)
        {
            transfer = PackedUNormTransfer.Argb4;
            return true;
        }

        if (format == TextureFormats.Argb4RevUNorm)
        {
            transfer = PackedUNormTransfer.Argb4Rev;
            return true;
        }

        if (format == TextureFormats.Abgr4UNorm)
        {
            transfer = PackedUNormTransfer.Abgr4;
            return true;
        }

        if (format == TextureFormats.Abgr4RevUNorm)
        {
            transfer = PackedUNormTransfer.Abgr4Rev;
            return true;
        }

        if (format == TextureFormats.Rgb5A1UNorm)
        {
            transfer = PackedUNormTransfer.Rgb5A1;
            return true;
        }

        if (format == TextureFormats.Rgb5A1RevUNorm)
        {
            transfer = PackedUNormTransfer.Rgb5A1Rev;
            return true;
        }

        if (format == TextureFormats.A1Rgb5UNorm)
        {
            transfer = PackedUNormTransfer.A1Rgb5;
            return true;
        }

        if (format == TextureFormats.A1Rgb5RevUNorm)
        {
            transfer = PackedUNormTransfer.A1Rgb5Rev;
            return true;
        }

        if (format == TextureFormats.A1Bgr5UNorm)
        {
            transfer = PackedUNormTransfer.A1Bgr5;
            return true;
        }

        if (format == TextureFormats.A1Bgr5RevUNorm)
        {
            transfer = PackedUNormTransfer.A1Bgr5Rev;
            return true;
        }

        if (format == TextureFormats.Rgb10A2UNorm)
        {
            transfer = PackedUNormTransfer.Rgb10A2;
            return true;
        }

        if (format == TextureFormats.Rgb10A2RevUNorm)
        {
            transfer = PackedUNormTransfer.Rgb10A2Rev;
            return true;
        }

        if (format == TextureFormats.Bgr10A2RevUNorm)
        {
            transfer = PackedUNormTransfer.Bgr10A2Rev;
            return true;
        }

        if (format == TextureFormats.Rgba12UNorm)
        {
            transfer = PackedUNormTransfer.Rgba12;
            return true;
        }

        if (format == TextureFormats.Bgra4UNorm)
        {
            transfer = PackedUNormTransfer.Bgra4;
            return true;
        }

        if (format == TextureFormats.Bgra4RevUNorm)
        {
            transfer = PackedUNormTransfer.Bgra4Rev;
            return true;
        }

        if (format == TextureFormats.Bgr5A1UNorm)
        {
            transfer = PackedUNormTransfer.Bgr5A1;
            return true;
        }

        if (format == TextureFormats.Bgr5A1RevUNorm)
        {
            transfer = PackedUNormTransfer.Bgr5A1Rev;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed UNorm texture codec does not support texture format '{format.Name}'.");

    private enum PackedUNormTransfer
    {
        Alpha12,
        Luminance12,
        Luminance4Alpha4,
        Luminance6Alpha2,
        Luminance12Alpha4,
        Luminance12Alpha12,
        Intensity12,
        Rg4,
        R3G3B2,
        R3G3B2Rev,
        Rgb4,
        Rgb5,
        Rgb565,
        Rgb565Rev,
        Bgr565,
        Bgr565Rev,
        Rgb10,
        Rgb12,
        R10X6,
        Rg10X6,
        Rgba10X6,
        R12X4,
        Rg12X4,
        Rgba12X4,
        R14X2,
        Rg14X2,
        Rgba14X2,
        Rgba2,
        Rgba4,
        Rgba4Rev,
        Argb4,
        Argb4Rev,
        Abgr4,
        Abgr4Rev,
        Rgb5A1,
        Rgb5A1Rev,
        A1Rgb5,
        A1Rgb5Rev,
        A1Bgr5,
        A1Bgr5Rev,
        Rgb10A2,
        Rgb10A2Rev,
        Bgr10A2Rev,
        Rgba12,
        Bgra4,
        Bgra4Rev,
        Bgr5A1,
        Bgr5A1Rev
    }
}
