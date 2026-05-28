using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class SequentialUncompressedTextureCoder : IPitchTextureCoder
{
    private readonly SequentialTransfer _transfer;

    public SequentialUncompressedTextureCoder(TextureFormat format)
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
            case SequentialTransfer.Alpha8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Alpha8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Alpha8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Alpha16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Alpha16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Alpha32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Alpha32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Alpha16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Alpha32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Luminance8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8SInt:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Luminance8SIntTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16UNormBigEndian:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16SInt:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Luminance16SIntTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32UNormBigEndian:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Luminance32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Luminance16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Luminance8Alpha8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8UNormBigEndian:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Luminance8Alpha8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8SInt:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Luminance8Alpha8SIntTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16Alpha16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16UNormBigEndian:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16Alpha16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Luminance16Alpha16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Luminance16Alpha16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32Alpha32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32UNormBigEndian:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32Alpha32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Luminance32Alpha32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance32Alpha32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance8Alpha8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Intensity8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Intensity8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Intensity16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Intensity16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Intensity32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Intensity32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Intensity16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Intensity32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, R8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, R8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, R16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, R16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, R32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, R32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, R16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16FloatBigEndian:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, R16FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32FloatBigEndian:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R32FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R64UNorm:
                Decode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, R64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R64SNorm:
                Decode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, R64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R64Float:
                Decode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, R64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rg8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8UNormBigEndian:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rg8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rg8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8SNormBigEndian:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rg8SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rg16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16UNormBigEndian:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rg16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rg16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16SNormBigEndian:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rg16SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rg32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32UNormBigEndian:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rg32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rg32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32SNormBigEndian:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rg32SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rg16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16FloatBigEndian:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rg16FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32FloatBigEndian:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg32FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg64UNorm:
                Decode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, Rg64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg64SNorm:
                Decode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, Rg64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg64Float:
                Decode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, Rg64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgb8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgb8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgb16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgb16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgb32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgb32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgb16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgb32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb64UNorm:
                Decode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, Rgb64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb64SNorm:
                Decode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, Rgb64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb64Float:
                Decode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, Rgb64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgb8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgr8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Bgr8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Bgr16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Bgr16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Bgr32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Bgr32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Bgr16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgr32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgr8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgba8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8UNormBigEndian:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgba8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgba8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8SNormBigEndian:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgba8SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgba16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16UNormBigEndian:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgba16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgba16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16SNormBigEndian:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgba16SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgba32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32UNormBigEndian:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgba32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgba32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32SNormBigEndian:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgba32SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgba16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16FloatBigEndian:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgba16FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32FloatBigEndian:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba32FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba64UNorm:
                Decode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, Rgba64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba64SNorm:
                Decode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, Rgba64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba64Float:
                Decode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, Rgba64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Abgr8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Abgr8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Abgr8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgra8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8UNormBigEndian:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgra8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Bgra8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgra8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgrx8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8UNormBigEndian:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgrx8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8Srgb:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgrx8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Bgra16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Bgra16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Bgra32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Bgra32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Bgra16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgra32FloatTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Alpha8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Alpha8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Alpha8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Alpha16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Alpha16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Alpha32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Alpha32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Alpha16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Alpha32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Alpha32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Luminance8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8SInt:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Luminance8SIntTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16UNormBigEndian:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16SInt:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Luminance16SIntTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32UNormBigEndian:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Luminance32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Luminance16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Luminance8Alpha8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8UNormBigEndian:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Luminance8Alpha8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8SInt:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Luminance8Alpha8SIntTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16Alpha16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16UNormBigEndian:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16Alpha16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Luminance16Alpha16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance16Alpha16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Luminance16Alpha16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32Alpha32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32UNormBigEndian:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32Alpha32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Luminance32Alpha32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance32Alpha32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance8Alpha8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance8Alpha8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Intensity8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Intensity8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Intensity16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Intensity16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Intensity32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Intensity32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Intensity16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Intensity32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Intensity32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, R8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, R8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, R16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, R16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, R32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, R32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, R16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R16FloatBigEndian:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, R16FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R32FloatBigEndian:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R32FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R64UNorm:
                Encode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, R64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R64SNorm:
                Encode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, R64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R64Float:
                Encode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, R64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.R8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rg8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8UNormBigEndian:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rg8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rg8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8SNormBigEndian:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rg8SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rg16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16UNormBigEndian:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rg16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rg16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16SNormBigEndian:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rg16SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rg32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32UNormBigEndian:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rg32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rg32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32SNormBigEndian:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rg32SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rg16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16FloatBigEndian:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rg16FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32FloatBigEndian:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg32FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg64UNorm:
                Encode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, Rg64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg64SNorm:
                Encode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, Rg64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg64Float:
                Encode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, Rg64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgb8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgb8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgb16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgb16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgb32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgb32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgb16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgb32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb64UNorm:
                Encode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, Rgb64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb64SNorm:
                Encode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, Rgb64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb64Float:
                Encode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, Rgb64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgb8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgb8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgr8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Bgr8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Bgr16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Bgr16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Bgr32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Bgr32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Bgr16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgr32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgr8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgr8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgba8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8UNormBigEndian:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgba8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgba8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8SNormBigEndian:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgba8SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgba16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16UNormBigEndian:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgba16UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgba16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16SNormBigEndian:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgba16SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgba32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32UNormBigEndian:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgba32UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgba32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32SNormBigEndian:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgba32SNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgba16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16FloatBigEndian:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgba16FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32FloatBigEndian:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba32FloatTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba64UNorm:
                Encode<TPixel, Rgba64UNorm, Rgba64UNormCarrierTransfer, Rgba64UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba64SNorm:
                Encode<TPixel, Rgba64SNorm, Rgba64SNormCarrierTransfer, Rgba64SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba64Float:
                Encode<TPixel, Rgba64Float, Rgba64FloatCarrierTransfer, Rgba64FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Abgr8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Abgr8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Abgr8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgra8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8UNormBigEndian:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgra8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Bgra8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgra8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgrx8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8UNormBigEndian:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgrx8UNormTransferBigEndian>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8Srgb:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgrx8SrgbTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Bgra16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Bgra16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Bgra32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Bgra32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Bgra16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Bgra32FloatTransfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private interface ISequentialCarrierTransfer<TCarrier>
    {
        static abstract TPixel FromCarrier<TPixel>(TCarrier value)
            where TPixel : unmanaged, IPixel<TPixel>;

        static abstract TCarrier ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel>;
    }

    private interface ISequentialTransfer<TCarrier>
    {
        static abstract TCarrier Decode(ReadOnlySpan<byte> texel);

        static abstract void Encode(TCarrier value, Span<byte> texel);
    }

    private readonly struct Rgba8UNormCarrierTransfer : ISequentialCarrierTransfer<Rgba8UNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba8UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba8UNorm(value);

        public static Rgba8UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba8UNorm(value);
    }

    private readonly struct Rgba16UNormCarrierTransfer : ISequentialCarrierTransfer<Rgba16UNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba16UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba16UNorm(value);

        public static Rgba16UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba16UNorm(value);
    }

    private readonly struct Rgba32UNormCarrierTransfer : ISequentialCarrierTransfer<Rgba32UNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba32UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba32UNorm(value);

        public static Rgba32UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba32UNorm(value);
    }

    private readonly struct Rgba8SNormCarrierTransfer : ISequentialCarrierTransfer<Rgba8SNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba8SNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba8SNorm(value);

        public static Rgba8SNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba8SNorm(value);
    }

    private readonly struct Rgba16SNormCarrierTransfer : ISequentialCarrierTransfer<Rgba16SNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba16SNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba16SNorm(value);

        public static Rgba16SNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba16SNorm(value);
    }

    private readonly struct Rgba32SNormCarrierTransfer : ISequentialCarrierTransfer<Rgba32SNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba32SNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba32SNorm(value);

        public static Rgba32SNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba32SNorm(value);
    }

    private readonly struct Rgba16FloatCarrierTransfer : ISequentialCarrierTransfer<Rgba16Float>
    {
        public static TPixel FromCarrier<TPixel>(Rgba16Float value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba16Float(value);

        public static Rgba16Float ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba16Float(value);
    }

    private readonly struct Rgba32FloatCarrierTransfer : ISequentialCarrierTransfer<Rgba32Float>
    {
        public static TPixel FromCarrier<TPixel>(Rgba32Float value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba32Float(value);

        public static Rgba32Float ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba32Float(value);
    }

    private readonly struct Rgba64UNormCarrierTransfer : ISequentialCarrierTransfer<Rgba64UNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba64UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba64UNorm(value);

        public static Rgba64UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba64UNorm(value);
    }

    private readonly struct Rgba64SNormCarrierTransfer : ISequentialCarrierTransfer<Rgba64SNorm>
    {
        public static TPixel FromCarrier<TPixel>(Rgba64SNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba64SNorm(value);

        public static Rgba64SNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba64SNorm(value);
    }

    private readonly struct Rgba64FloatCarrierTransfer : ISequentialCarrierTransfer<Rgba64Float>
    {
        public static TPixel FromCarrier<TPixel>(Rgba64Float value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba64Float(value);

        public static Rgba64Float ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba64Float(value);
    }

    private void Decode<TPixel, TCarrier, TCarrierTransfer, TTransfer>(
        ReadOnlySpan<byte> source,
        ImageView<TPixel> destination,
        int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TCarrierTransfer : ISequentialCarrierTransfer<TCarrier>
        where TTransfer : ISequentialTransfer<TCarrier>
    {
        var bytesPerTexel = Format.BytesPerBlock;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var carrier = TTransfer.Decode(source.Slice(texelOffset, bytesPerTexel));
                destinationRow[x] = TCarrierTransfer.FromCarrier<TPixel>(carrier);
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TCarrier, TCarrierTransfer, TTransfer>(
        ImageView<TPixel> source,
        Span<byte> destination,
        int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TCarrierTransfer : ISequentialCarrierTransfer<TCarrier>
        where TTransfer : ISequentialTransfer<TCarrier>
    {
        var bytesPerTexel = Format.BytesPerBlock;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TCarrierTransfer.ToCarrier(sourceRow[x]), destination.Slice(texelOffset, bytesPerTexel));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private static TCarrier DecodeBigEndianTexel<TTransfer, TCarrier>(
        ReadOnlySpan<byte> source,
        BigEndianByteSwapMode endianMode)
        where TTransfer : ISequentialTransfer<TCarrier>
    {
        Span<byte> littleEndianTexel = stackalloc byte[source.Length];
        BigEndianByteSwap.CopyToLittleEndian(source, littleEndianTexel, endianMode);
        return TTransfer.Decode(littleEndianTexel);
    }

    private static void EncodeBigEndianTexel<TTransfer, TCarrier>(
        TCarrier value,
        Span<byte> destination,
        BigEndianByteSwapMode endianMode)
        where TTransfer : ISequentialTransfer<TCarrier>
    {
        Span<byte> littleEndianTexel = stackalloc byte[destination.Length];
        TTransfer.Encode(value, littleEndianTexel);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianTexel, destination, endianMode);
    }

    private readonly struct Luminance16UNormTransferBigEndian : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Luminance16UNormTransfer, Rgba16UNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Luminance16UNormTransfer, Rgba16UNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Luminance32UNormTransferBigEndian : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Luminance32UNormTransfer, Rgba32UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba32UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Luminance32UNormTransfer, Rgba32UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Luminance8Alpha8UNormTransferBigEndian : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Luminance8Alpha8UNormTransfer, Rgba8UNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Luminance8Alpha8UNormTransfer, Rgba8UNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Luminance16Alpha16UNormTransferBigEndian : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Luminance16Alpha16UNormTransfer, Rgba16UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Luminance16Alpha16UNormTransfer, Rgba16UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Luminance32Alpha32UNormTransferBigEndian : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Luminance32Alpha32UNormTransfer, Rgba32UNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba32UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Luminance32Alpha32UNormTransfer, Rgba32UNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct R16FloatTransferBigEndian : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<R16FloatTransfer, Rgba16Float>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16Float value, Span<byte> texel) =>
            EncodeBigEndianTexel<R16FloatTransfer, Rgba16Float>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct R32FloatTransferBigEndian : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<R32FloatTransfer, Rgba32Float>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            EncodeBigEndianTexel<R32FloatTransfer, Rgba32Float>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rg8UNormTransferBigEndian : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg8UNormTransfer, Rgba8UNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg8UNormTransfer, Rgba8UNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rg8SNormTransferBigEndian : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg8SNormTransfer, Rgba8SNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba8SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg8SNormTransfer, Rgba8SNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rg16UNormTransferBigEndian : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg16UNormTransfer, Rgba16UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg16UNormTransfer, Rgba16UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rg16SNormTransferBigEndian : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg16SNormTransfer, Rgba16SNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg16SNormTransfer, Rgba16SNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rg16FloatTransferBigEndian : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg16FloatTransfer, Rgba16Float>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba16Float value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg16FloatTransfer, Rgba16Float>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rg32UNormTransferBigEndian : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg32UNormTransfer, Rgba32UNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba32UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg32UNormTransfer, Rgba32UNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rg32SNormTransferBigEndian : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg32SNormTransfer, Rgba32SNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba32SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg32SNormTransfer, Rgba32SNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rg32FloatTransferBigEndian : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rg32FloatTransfer, Rgba32Float>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rg32FloatTransfer, Rgba32Float>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgba8UNormTransferBigEndian : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba8UNormTransfer, Rgba8UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba8UNormTransfer, Rgba8UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgba8SNormTransferBigEndian : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba8SNormTransfer, Rgba8SNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba8SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba8SNormTransfer, Rgba8SNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgba16UNormTransferBigEndian : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba16UNormTransfer, Rgba16UNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba16UNormTransfer, Rgba16UNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rgba16SNormTransferBigEndian : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba16SNormTransfer, Rgba16SNorm>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba16SNormTransfer, Rgba16SNorm>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rgba16FloatTransferBigEndian : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba16FloatTransfer, Rgba16Float>(texel, BigEndianByteSwapMode.Swap8In16);

        public static void Encode(Rgba16Float value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba16FloatTransfer, Rgba16Float>(value, texel, BigEndianByteSwapMode.Swap8In16);
    }

    private readonly struct Rgba32UNormTransferBigEndian : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba32UNormTransfer, Rgba32UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba32UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba32UNormTransfer, Rgba32UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgba32SNormTransferBigEndian : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba32SNormTransfer, Rgba32SNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba32SNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba32SNormTransfer, Rgba32SNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Rgba32FloatTransferBigEndian : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Rgba32FloatTransfer, Rgba32Float>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            EncodeBigEndianTexel<Rgba32FloatTransfer, Rgba32Float>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Bgra8UNormTransferBigEndian : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Bgra8UNormTransfer, Rgba8UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Bgra8UNormTransfer, Rgba8UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Bgrx8UNormTransferBigEndian : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) =>
            DecodeBigEndianTexel<Bgrx8UNormTransfer, Rgba8UNorm>(texel, BigEndianByteSwapMode.Swap8In32);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) =>
            EncodeBigEndianTexel<Bgrx8UNormTransfer, Rgba8UNorm>(value, texel, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct Alpha8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(0, 0, 0, texel[0]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) => texel[0] = value.Alpha;
    }

    private readonly struct Alpha8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) => new(0, 0, 0, (sbyte)texel[0]);

        public static void Encode(Rgba8SNorm value, Span<byte> texel) => texel[0] = unchecked((byte)value.Alpha);
    }

    private readonly struct Alpha16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(0, 0, 0, ReadUInt16(texel, 0));

        public static void Encode(Rgba16UNorm value, Span<byte> texel) => WriteUInt16(texel, 0, value.Alpha);
    }

    private readonly struct Alpha16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) => new(0, 0, 0, ReadInt16(texel, 0));

        public static void Encode(Rgba16SNorm value, Span<byte> texel) => WriteInt16(texel, 0, value.Alpha);
    }

    private readonly struct Alpha32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) => new(0, 0, 0, ReadUInt32(texel, 0));

        public static void Encode(Rgba32UNorm value, Span<byte> texel) => WriteUInt32(texel, 0, value.Alpha);
    }

    private readonly struct Alpha32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) => new(0, 0, 0, ReadInt32(texel, 0));

        public static void Encode(Rgba32SNorm value, Span<byte> texel) => WriteInt32(texel, 0, value.Alpha);
    }

    private readonly struct Alpha16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            new((Half)0f, (Half)0f, (Half)0f, ReadHalf(texel, 0));

        public static void Encode(Rgba16Float value, Span<byte> texel) => WriteHalf(texel, 0, value.Alpha);
    }

    private readonly struct Alpha32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) => new(0f, 0f, 0f, ReadSingle(texel, 0));

        public static void Encode(Rgba32Float value, Span<byte> texel) => WriteSingle(texel, 0, value.Alpha);
    }

    private readonly struct Luminance8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], texel[0], texel[0]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) => texel[0] = value.Red;
    }

    private readonly struct Luminance8SIntTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = (sbyte)texel[0];
            return new Rgba8SNorm(value, value, value);
        }

        public static void Encode(Rgba8SNorm value, Span<byte> texel) => texel[0] = unchecked((byte)value.Red);
    }

    private readonly struct Luminance16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadUInt16(texel, 0);
            return new Rgba16UNorm(value, value, value);
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) => WriteUInt16(texel, 0, value.Red);
    }

    private readonly struct Luminance16SIntTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadInt16(texel, 0);
            return new Rgba16SNorm(value, value, value);
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel) => WriteInt16(texel, 0, value.Red);
    }

    private readonly struct Luminance32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadUInt32(texel, 0);
            return new Rgba32UNorm(value, value, value);
        }

        public static void Encode(Rgba32UNorm value, Span<byte> texel) => WriteUInt32(texel, 0, value.Red);
    }

    private readonly struct Luminance32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadInt32(texel, 0);
            return new Rgba32SNorm(value, value, value);
        }

        public static void Encode(Rgba32SNorm value, Span<byte> texel) => WriteInt32(texel, 0, value.Red);
    }

    private readonly struct Luminance16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadHalf(texel, 0);
            return new Rgba16Float(value, value, value);
        }

        public static void Encode(Rgba16Float value, Span<byte> texel) => WriteHalf(texel, 0, value.Red);
    }

    private readonly struct Luminance32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadSingle(texel, 0);
            return new Rgba32Float(value, value, value);
        }

        public static void Encode(Rgba32Float value, Span<byte> texel) => WriteSingle(texel, 0, value.Red);
    }

    private readonly struct Luminance8Alpha8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], texel[0], texel[0], texel[1]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Red;
            texel[1] = value.Alpha;
        }
    }

    private readonly struct Luminance8Alpha8SIntTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = (sbyte)texel[0];
            return new Rgba8SNorm(luminance, luminance, luminance, (sbyte)texel[1]);
        }

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Red);
            texel[1] = unchecked((byte)value.Alpha);
        }
    }

    private readonly struct Luminance16Alpha16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = ReadUInt16(texel, 0);
            return new Rgba16UNorm(luminance, luminance, luminance, ReadUInt16(texel, 2));
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WriteUInt16(texel, 0, value.Red);
            WriteUInt16(texel, 2, value.Alpha);
        }
    }

    private readonly struct Luminance16Alpha16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = ReadInt16(texel, 0);
            return new Rgba16SNorm(luminance, luminance, luminance, ReadInt16(texel, 2));
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            WriteInt16(texel, 0, value.Red);
            WriteInt16(texel, 2, value.Alpha);
        }
    }

    private readonly struct Luminance16Alpha16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = ReadHalf(texel, 0);
            return new Rgba16Float(luminance, luminance, luminance, ReadHalf(texel, 2));
        }

        public static void Encode(Rgba16Float value, Span<byte> texel)
        {
            WriteHalf(texel, 0, value.Red);
            WriteHalf(texel, 2, value.Alpha);
        }
    }

    private readonly struct Luminance32Alpha32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = ReadUInt32(texel, 0);
            return new Rgba32UNorm(luminance, luminance, luminance, ReadUInt32(texel, 4));
        }

        public static void Encode(Rgba32UNorm value, Span<byte> texel)
        {
            WriteUInt32(texel, 0, value.Red);
            WriteUInt32(texel, 4, value.Alpha);
        }
    }

    private readonly struct Luminance32Alpha32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = ReadInt32(texel, 0);
            return new Rgba32SNorm(luminance, luminance, luminance, ReadInt32(texel, 4));
        }

        public static void Encode(Rgba32SNorm value, Span<byte> texel)
        {
            WriteInt32(texel, 0, value.Red);
            WriteInt32(texel, 4, value.Alpha);
        }
    }

    private readonly struct Luminance32Alpha32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = ReadSingle(texel, 0);
            return new Rgba32Float(luminance, luminance, luminance, ReadSingle(texel, 4));
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            WriteSingle(texel, 0, value.Red);
            WriteSingle(texel, 4, value.Alpha);
        }
    }

    private readonly struct Intensity8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], texel[0], texel[0], texel[0]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) => texel[0] = value.Red;
    }

    private readonly struct Intensity8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = (sbyte)texel[0];
            return new Rgba8SNorm(value, value, value, value);
        }

        public static void Encode(Rgba8SNorm value, Span<byte> texel) => texel[0] = unchecked((byte)value.Red);
    }

    private readonly struct Intensity16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadUInt16(texel, 0);
            return new Rgba16UNorm(value, value, value, value);
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) => WriteUInt16(texel, 0, value.Red);
    }

    private readonly struct Intensity16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadInt16(texel, 0);
            return new Rgba16SNorm(value, value, value, value);
        }

        public static void Encode(Rgba16SNorm value, Span<byte> texel) => WriteInt16(texel, 0, value.Red);
    }

    private readonly struct Intensity32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadUInt32(texel, 0);
            return new Rgba32UNorm(value, value, value, value);
        }

        public static void Encode(Rgba32UNorm value, Span<byte> texel) => WriteUInt32(texel, 0, value.Red);
    }

    private readonly struct Intensity32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadInt32(texel, 0);
            return new Rgba32SNorm(value, value, value, value);
        }

        public static void Encode(Rgba32SNorm value, Span<byte> texel) => WriteInt32(texel, 0, value.Red);
    }

    private readonly struct Intensity16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadHalf(texel, 0);
            return new Rgba16Float(value, value, value, value);
        }

        public static void Encode(Rgba16Float value, Span<byte> texel) => WriteHalf(texel, 0, value.Red);
    }

    private readonly struct Intensity32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadSingle(texel, 0);
            return new Rgba32Float(value, value, value, value);
        }

        public static void Encode(Rgba32Float value, Span<byte> texel) => WriteSingle(texel, 0, value.Red);
    }

    private readonly struct R8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], 0, 0);

        public static void Encode(Rgba8UNorm value, Span<byte> texel) => texel[0] = value.Red;
    }

    private readonly struct R8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) => new((sbyte)texel[0], 0, 0);

        public static void Encode(Rgba8SNorm value, Span<byte> texel) => texel[0] = unchecked((byte)value.Red);
    }

    private readonly struct R16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) => new(ReadUInt16(texel, 0), 0, 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel) => WriteUInt16(texel, 0, value.Red);
    }

    private readonly struct R16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) => new(ReadInt16(texel, 0), 0, 0);

        public static void Encode(Rgba16SNorm value, Span<byte> texel) => WriteInt16(texel, 0, value.Red);
    }

    private readonly struct R32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) => new(ReadUInt32(texel, 0), 0, 0);

        public static void Encode(Rgba32UNorm value, Span<byte> texel) => WriteUInt32(texel, 0, value.Red);
    }

    private readonly struct R32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) => new(ReadInt32(texel, 0), 0, 0);

        public static void Encode(Rgba32SNorm value, Span<byte> texel) => WriteInt32(texel, 0, value.Red);
    }

    private readonly struct R16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) => new(ReadHalf(texel, 0), (Half)0f, (Half)0f);

        public static void Encode(Rgba16Float value, Span<byte> texel) => WriteHalf(texel, 0, value.Red);
    }

    private readonly struct R32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) => new(ReadSingle(texel, 0), 0f, 0f);

        public static void Encode(Rgba32Float value, Span<byte> texel) => WriteSingle(texel, 0, value.Red);
    }

    private readonly struct R64UNormTransfer : ISequentialTransfer<Rgba64UNorm>
    {
        public static Rgba64UNorm Decode(ReadOnlySpan<byte> texel) => new(ReadUInt64(texel, 0), 0, 0);

        public static void Encode(Rgba64UNorm value, Span<byte> texel) => WriteUInt64(texel, 0, value.Red);
    }

    private readonly struct R64SNormTransfer : ISequentialTransfer<Rgba64SNorm>
    {
        public static Rgba64SNorm Decode(ReadOnlySpan<byte> texel) => new(ReadInt64(texel, 0), 0, 0);

        public static void Encode(Rgba64SNorm value, Span<byte> texel) => WriteInt64(texel, 0, value.Red);
    }

    private readonly struct R64FloatTransfer : ISequentialTransfer<Rgba64Float>
    {
        public static Rgba64Float Decode(ReadOnlySpan<byte> texel) => new(ReadDouble(texel, 0), 0d, 0d);

        public static void Encode(Rgba64Float value, Span<byte> texel) => WriteDouble(texel, 0, value.Red);
    }

    private readonly struct Rg8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], texel[1], 0);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Red;
            texel[1] = value.Green;
        }
    }

    private readonly struct Rg8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) => new((sbyte)texel[0], (sbyte)texel[1], 0);

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Red);
            texel[1] = unchecked((byte)value.Green);
        }
    }

    private readonly struct Rg16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt16(texel, 0), ReadUInt16(texel, 2), 0);

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WriteUInt16(texel, 0, value.Red);
            WriteUInt16(texel, 2, value.Green);
        }
    }

    private readonly struct Rg16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt16(texel, 0), ReadInt16(texel, 2), 0);

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            WriteInt16(texel, 0, value.Red);
            WriteInt16(texel, 2, value.Green);
        }
    }

    private readonly struct Rg32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt32(texel, 0), ReadUInt32(texel, 4), 0);

        public static void Encode(Rgba32UNorm value, Span<byte> texel)
        {
            WriteUInt32(texel, 0, value.Red);
            WriteUInt32(texel, 4, value.Green);
        }
    }

    private readonly struct Rg32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt32(texel, 0), ReadInt32(texel, 4), 0);

        public static void Encode(Rgba32SNorm value, Span<byte> texel)
        {
            WriteInt32(texel, 0, value.Red);
            WriteInt32(texel, 4, value.Green);
        }
    }

    private readonly struct Rg16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadHalf(texel, 0), ReadHalf(texel, 2), (Half)0f);

        public static void Encode(Rgba16Float value, Span<byte> texel)
        {
            WriteHalf(texel, 0, value.Red);
            WriteHalf(texel, 2, value.Green);
        }
    }

    private readonly struct Rg32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadSingle(texel, 0), ReadSingle(texel, 4), 0f);

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            WriteSingle(texel, 0, value.Red);
            WriteSingle(texel, 4, value.Green);
        }
    }

    private readonly struct Rg64UNormTransfer : ISequentialTransfer<Rgba64UNorm>
    {
        public static Rgba64UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt64(texel, 0), ReadUInt64(texel, 8), 0);

        public static void Encode(Rgba64UNorm value, Span<byte> texel)
        {
            WriteUInt64(texel, 0, value.Red);
            WriteUInt64(texel, 8, value.Green);
        }
    }

    private readonly struct Rg64SNormTransfer : ISequentialTransfer<Rgba64SNorm>
    {
        public static Rgba64SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt64(texel, 0), ReadInt64(texel, 8), 0);

        public static void Encode(Rgba64SNorm value, Span<byte> texel)
        {
            WriteInt64(texel, 0, value.Red);
            WriteInt64(texel, 8, value.Green);
        }
    }

    private readonly struct Rg64FloatTransfer : ISequentialTransfer<Rgba64Float>
    {
        public static Rgba64Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadDouble(texel, 0), ReadDouble(texel, 8), 0d);

        public static void Encode(Rgba64Float value, Span<byte> texel)
        {
            WriteDouble(texel, 0, value.Red);
            WriteDouble(texel, 8, value.Green);
        }
    }

    private readonly struct Rgb8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], texel[1], texel[2]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Red;
            texel[1] = value.Green;
            texel[2] = value.Blue;
        }
    }

    private readonly struct Rgb8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            new((sbyte)texel[0], (sbyte)texel[1], (sbyte)texel[2]);

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Red);
            texel[1] = unchecked((byte)value.Green);
            texel[2] = unchecked((byte)value.Blue);
        }
    }

    private readonly struct Rgb16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt16(texel, 0), ReadUInt16(texel, 2), ReadUInt16(texel, 4));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WriteUInt16(texel, 0, value.Red);
            WriteUInt16(texel, 2, value.Green);
            WriteUInt16(texel, 4, value.Blue);
        }
    }

    private readonly struct Rgb16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt16(texel, 0), ReadInt16(texel, 2), ReadInt16(texel, 4));

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            WriteInt16(texel, 0, value.Red);
            WriteInt16(texel, 2, value.Green);
            WriteInt16(texel, 4, value.Blue);
        }
    }

    private readonly struct Rgb32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt32(texel, 0), ReadUInt32(texel, 4), ReadUInt32(texel, 8));

        public static void Encode(Rgba32UNorm value, Span<byte> texel)
        {
            WriteUInt32(texel, 0, value.Red);
            WriteUInt32(texel, 4, value.Green);
            WriteUInt32(texel, 8, value.Blue);
        }
    }

    private readonly struct Rgb32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt32(texel, 0), ReadInt32(texel, 4), ReadInt32(texel, 8));

        public static void Encode(Rgba32SNorm value, Span<byte> texel)
        {
            WriteInt32(texel, 0, value.Red);
            WriteInt32(texel, 4, value.Green);
            WriteInt32(texel, 8, value.Blue);
        }
    }

    private readonly struct Rgb16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadHalf(texel, 0), ReadHalf(texel, 2), ReadHalf(texel, 4));

        public static void Encode(Rgba16Float value, Span<byte> texel)
        {
            WriteHalf(texel, 0, value.Red);
            WriteHalf(texel, 2, value.Green);
            WriteHalf(texel, 4, value.Blue);
        }
    }

    private readonly struct Rgb32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadSingle(texel, 0), ReadSingle(texel, 4), ReadSingle(texel, 8));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            WriteSingle(texel, 0, value.Red);
            WriteSingle(texel, 4, value.Green);
            WriteSingle(texel, 8, value.Blue);
        }
    }

    private readonly struct Rgb64UNormTransfer : ISequentialTransfer<Rgba64UNorm>
    {
        public static Rgba64UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt64(texel, 0), ReadUInt64(texel, 8), ReadUInt64(texel, 16));

        public static void Encode(Rgba64UNorm value, Span<byte> texel)
        {
            WriteUInt64(texel, 0, value.Red);
            WriteUInt64(texel, 8, value.Green);
            WriteUInt64(texel, 16, value.Blue);
        }
    }

    private readonly struct Rgb64SNormTransfer : ISequentialTransfer<Rgba64SNorm>
    {
        public static Rgba64SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt64(texel, 0), ReadInt64(texel, 8), ReadInt64(texel, 16));

        public static void Encode(Rgba64SNorm value, Span<byte> texel)
        {
            WriteInt64(texel, 0, value.Red);
            WriteInt64(texel, 8, value.Green);
            WriteInt64(texel, 16, value.Blue);
        }
    }

    private readonly struct Rgb64FloatTransfer : ISequentialTransfer<Rgba64Float>
    {
        public static Rgba64Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadDouble(texel, 0), ReadDouble(texel, 8), ReadDouble(texel, 16));

        public static void Encode(Rgba64Float value, Span<byte> texel)
        {
            WriteDouble(texel, 0, value.Red);
            WriteDouble(texel, 8, value.Green);
            WriteDouble(texel, 16, value.Blue);
        }
    }

    private readonly struct Bgr8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[2], texel[1], texel[0]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Blue;
            texel[1] = value.Green;
            texel[2] = value.Red;
        }
    }

    private readonly struct Bgr8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            new((sbyte)texel[2], (sbyte)texel[1], (sbyte)texel[0]);

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Blue);
            texel[1] = unchecked((byte)value.Green);
            texel[2] = unchecked((byte)value.Red);
        }
    }

    private readonly struct Bgr16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt16(texel, 4), ReadUInt16(texel, 2), ReadUInt16(texel, 0));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WriteUInt16(texel, 0, value.Blue);
            WriteUInt16(texel, 2, value.Green);
            WriteUInt16(texel, 4, value.Red);
        }
    }

    private readonly struct Bgr16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt16(texel, 4), ReadInt16(texel, 2), ReadInt16(texel, 0));

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            WriteInt16(texel, 0, value.Blue);
            WriteInt16(texel, 2, value.Green);
            WriteInt16(texel, 4, value.Red);
        }
    }

    private readonly struct Bgr32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt32(texel, 8), ReadUInt32(texel, 4), ReadUInt32(texel, 0));

        public static void Encode(Rgba32UNorm value, Span<byte> texel)
        {
            WriteUInt32(texel, 0, value.Blue);
            WriteUInt32(texel, 4, value.Green);
            WriteUInt32(texel, 8, value.Red);
        }
    }

    private readonly struct Bgr32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt32(texel, 8), ReadInt32(texel, 4), ReadInt32(texel, 0));

        public static void Encode(Rgba32SNorm value, Span<byte> texel)
        {
            WriteInt32(texel, 0, value.Blue);
            WriteInt32(texel, 4, value.Green);
            WriteInt32(texel, 8, value.Red);
        }
    }

    private readonly struct Bgr16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadHalf(texel, 4), ReadHalf(texel, 2), ReadHalf(texel, 0));

        public static void Encode(Rgba16Float value, Span<byte> texel)
        {
            WriteHalf(texel, 0, value.Blue);
            WriteHalf(texel, 2, value.Green);
            WriteHalf(texel, 4, value.Red);
        }
    }

    private readonly struct Bgr32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadSingle(texel, 8), ReadSingle(texel, 4), ReadSingle(texel, 0));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            WriteSingle(texel, 0, value.Blue);
            WriteSingle(texel, 4, value.Green);
            WriteSingle(texel, 8, value.Red);
        }
    }

    private readonly struct Rgba8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[0], texel[1], texel[2], texel[3]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Red;
            texel[1] = value.Green;
            texel[2] = value.Blue;
            texel[3] = value.Alpha;
        }
    }

    private readonly struct Rgba8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            new((sbyte)texel[0], (sbyte)texel[1], (sbyte)texel[2], (sbyte)texel[3]);

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Red);
            texel[1] = unchecked((byte)value.Green);
            texel[2] = unchecked((byte)value.Blue);
            texel[3] = unchecked((byte)value.Alpha);
        }
    }

    private readonly struct Rgba16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt16(texel, 0), ReadUInt16(texel, 2), ReadUInt16(texel, 4), ReadUInt16(texel, 6));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WriteUInt16(texel, 0, value.Red);
            WriteUInt16(texel, 2, value.Green);
            WriteUInt16(texel, 4, value.Blue);
            WriteUInt16(texel, 6, value.Alpha);
        }
    }

    private readonly struct Rgba16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt16(texel, 0), ReadInt16(texel, 2), ReadInt16(texel, 4), ReadInt16(texel, 6));

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            WriteInt16(texel, 0, value.Red);
            WriteInt16(texel, 2, value.Green);
            WriteInt16(texel, 4, value.Blue);
            WriteInt16(texel, 6, value.Alpha);
        }
    }

    private readonly struct Rgba32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt32(texel, 0), ReadUInt32(texel, 4), ReadUInt32(texel, 8), ReadUInt32(texel, 12));

        public static void Encode(Rgba32UNorm value, Span<byte> texel)
        {
            WriteUInt32(texel, 0, value.Red);
            WriteUInt32(texel, 4, value.Green);
            WriteUInt32(texel, 8, value.Blue);
            WriteUInt32(texel, 12, value.Alpha);
        }
    }

    private readonly struct Rgba32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt32(texel, 0), ReadInt32(texel, 4), ReadInt32(texel, 8), ReadInt32(texel, 12));

        public static void Encode(Rgba32SNorm value, Span<byte> texel)
        {
            WriteInt32(texel, 0, value.Red);
            WriteInt32(texel, 4, value.Green);
            WriteInt32(texel, 8, value.Blue);
            WriteInt32(texel, 12, value.Alpha);
        }
    }

    private readonly struct Rgba16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadHalf(texel, 0), ReadHalf(texel, 2), ReadHalf(texel, 4), ReadHalf(texel, 6));

        public static void Encode(Rgba16Float value, Span<byte> texel)
        {
            WriteHalf(texel, 0, value.Red);
            WriteHalf(texel, 2, value.Green);
            WriteHalf(texel, 4, value.Blue);
            WriteHalf(texel, 6, value.Alpha);
        }
    }

    private readonly struct Rgba32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadSingle(texel, 0), ReadSingle(texel, 4), ReadSingle(texel, 8), ReadSingle(texel, 12));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            WriteSingle(texel, 0, value.Red);
            WriteSingle(texel, 4, value.Green);
            WriteSingle(texel, 8, value.Blue);
            WriteSingle(texel, 12, value.Alpha);
        }
    }

    private readonly struct Rgba64UNormTransfer : ISequentialTransfer<Rgba64UNorm>
    {
        public static Rgba64UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt64(texel, 0), ReadUInt64(texel, 8), ReadUInt64(texel, 16), ReadUInt64(texel, 24));

        public static void Encode(Rgba64UNorm value, Span<byte> texel)
        {
            WriteUInt64(texel, 0, value.Red);
            WriteUInt64(texel, 8, value.Green);
            WriteUInt64(texel, 16, value.Blue);
            WriteUInt64(texel, 24, value.Alpha);
        }
    }

    private readonly struct Rgba64SNormTransfer : ISequentialTransfer<Rgba64SNorm>
    {
        public static Rgba64SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt64(texel, 0), ReadInt64(texel, 8), ReadInt64(texel, 16), ReadInt64(texel, 24));

        public static void Encode(Rgba64SNorm value, Span<byte> texel)
        {
            WriteInt64(texel, 0, value.Red);
            WriteInt64(texel, 8, value.Green);
            WriteInt64(texel, 16, value.Blue);
            WriteInt64(texel, 24, value.Alpha);
        }
    }

    private readonly struct Rgba64FloatTransfer : ISequentialTransfer<Rgba64Float>
    {
        public static Rgba64Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadDouble(texel, 0), ReadDouble(texel, 8), ReadDouble(texel, 16), ReadDouble(texel, 24));

        public static void Encode(Rgba64Float value, Span<byte> texel)
        {
            WriteDouble(texel, 0, value.Red);
            WriteDouble(texel, 8, value.Green);
            WriteDouble(texel, 16, value.Blue);
            WriteDouble(texel, 24, value.Alpha);
        }
    }

    private readonly struct Abgr8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[3], texel[2], texel[1], texel[0]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Alpha;
            texel[1] = value.Blue;
            texel[2] = value.Green;
            texel[3] = value.Red;
        }
    }

    private readonly struct Abgr8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            new((sbyte)texel[3], (sbyte)texel[2], (sbyte)texel[1], (sbyte)texel[0]);

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Alpha);
            texel[1] = unchecked((byte)value.Blue);
            texel[2] = unchecked((byte)value.Green);
            texel[3] = unchecked((byte)value.Red);
        }
    }

    private readonly struct Bgra8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[2], texel[1], texel[0], texel[3]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Blue;
            texel[1] = value.Green;
            texel[2] = value.Red;
            texel[3] = value.Alpha;
        }
    }

    private readonly struct Bgra8SNormTransfer : ISequentialTransfer<Rgba8SNorm>
    {
        public static Rgba8SNorm Decode(ReadOnlySpan<byte> texel) =>
            new((sbyte)texel[2], (sbyte)texel[1], (sbyte)texel[0], (sbyte)texel[3]);

        public static void Encode(Rgba8SNorm value, Span<byte> texel)
        {
            texel[0] = unchecked((byte)value.Blue);
            texel[1] = unchecked((byte)value.Green);
            texel[2] = unchecked((byte)value.Red);
            texel[3] = unchecked((byte)value.Alpha);
        }
    }

    private readonly struct Bgrx8UNormTransfer : ISequentialTransfer<Rgba8UNorm>
    {
        public static Rgba8UNorm Decode(ReadOnlySpan<byte> texel) => new(texel[2], texel[1], texel[0]);

        public static void Encode(Rgba8UNorm value, Span<byte> texel)
        {
            texel[0] = value.Blue;
            texel[1] = value.Green;
            texel[2] = value.Red;
            texel[3] = 0;
        }
    }

    private readonly struct Luminance8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = DecodeSrgb(texel[0]);
            return new Rgba32Float(luminance, luminance, luminance);
        }

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            texel[0] = EncodeSrgb(value.Red);
    }

    private readonly struct Luminance8Alpha8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel)
        {
            var luminance = DecodeSrgb(texel[0]);
            return new Rgba32Float(luminance, luminance, luminance, DecodeUNorm(texel[1]));
        }

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Red);
            texel[1] = EncodeUNorm(value.Alpha);
        }
    }

    private readonly struct R8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) => new(DecodeSrgb(texel[0]), 0f, 0f);

        public static void Encode(Rgba32Float value, Span<byte> texel) =>
            texel[0] = EncodeSrgb(value.Red);
    }

    private readonly struct Rg8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[0]), DecodeSrgb(texel[1]), 0f);

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Red);
            texel[1] = EncodeSrgb(value.Green);
        }
    }

    private readonly struct Rgb8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[0]), DecodeSrgb(texel[1]), DecodeSrgb(texel[2]));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Red);
            texel[1] = EncodeSrgb(value.Green);
            texel[2] = EncodeSrgb(value.Blue);
        }
    }

    private readonly struct Bgr8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[2]), DecodeSrgb(texel[1]), DecodeSrgb(texel[0]));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Blue);
            texel[1] = EncodeSrgb(value.Green);
            texel[2] = EncodeSrgb(value.Red);
        }
    }

    private readonly struct Rgba8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[0]), DecodeSrgb(texel[1]), DecodeSrgb(texel[2]), DecodeUNorm(texel[3]));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Red);
            texel[1] = EncodeSrgb(value.Green);
            texel[2] = EncodeSrgb(value.Blue);
            texel[3] = EncodeUNorm(value.Alpha);
        }
    }

    private readonly struct Abgr8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[3]), DecodeSrgb(texel[2]), DecodeSrgb(texel[1]), DecodeUNorm(texel[0]));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeUNorm(value.Alpha);
            texel[1] = EncodeSrgb(value.Blue);
            texel[2] = EncodeSrgb(value.Green);
            texel[3] = EncodeSrgb(value.Red);
        }
    }

    private readonly struct Bgra8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[2]), DecodeSrgb(texel[1]), DecodeSrgb(texel[0]), DecodeUNorm(texel[3]));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Blue);
            texel[1] = EncodeSrgb(value.Green);
            texel[2] = EncodeSrgb(value.Red);
            texel[3] = EncodeUNorm(value.Alpha);
        }
    }

    private readonly struct Bgrx8SrgbTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(DecodeSrgb(texel[2]), DecodeSrgb(texel[1]), DecodeSrgb(texel[0]));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            texel[0] = EncodeSrgb(value.Blue);
            texel[1] = EncodeSrgb(value.Green);
            texel[2] = EncodeSrgb(value.Red);
            texel[3] = 0;
        }
    }

    private readonly struct Bgra16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt16(texel, 4), ReadUInt16(texel, 2), ReadUInt16(texel, 0), ReadUInt16(texel, 6));

        public static void Encode(Rgba16UNorm value, Span<byte> texel)
        {
            WriteUInt16(texel, 0, value.Blue);
            WriteUInt16(texel, 2, value.Green);
            WriteUInt16(texel, 4, value.Red);
            WriteUInt16(texel, 6, value.Alpha);
        }
    }

    private readonly struct Bgra16SNormTransfer : ISequentialTransfer<Rgba16SNorm>
    {
        public static Rgba16SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt16(texel, 4), ReadInt16(texel, 2), ReadInt16(texel, 0), ReadInt16(texel, 6));

        public static void Encode(Rgba16SNorm value, Span<byte> texel)
        {
            WriteInt16(texel, 0, value.Blue);
            WriteInt16(texel, 2, value.Green);
            WriteInt16(texel, 4, value.Red);
            WriteInt16(texel, 6, value.Alpha);
        }
    }

    private readonly struct Bgra32UNormTransfer : ISequentialTransfer<Rgba32UNorm>
    {
        public static Rgba32UNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadUInt32(texel, 8), ReadUInt32(texel, 4), ReadUInt32(texel, 0), ReadUInt32(texel, 12));

        public static void Encode(Rgba32UNorm value, Span<byte> texel)
        {
            WriteUInt32(texel, 0, value.Blue);
            WriteUInt32(texel, 4, value.Green);
            WriteUInt32(texel, 8, value.Red);
            WriteUInt32(texel, 12, value.Alpha);
        }
    }

    private readonly struct Bgra32SNormTransfer : ISequentialTransfer<Rgba32SNorm>
    {
        public static Rgba32SNorm Decode(ReadOnlySpan<byte> texel) =>
            new(ReadInt32(texel, 8), ReadInt32(texel, 4), ReadInt32(texel, 0), ReadInt32(texel, 12));

        public static void Encode(Rgba32SNorm value, Span<byte> texel)
        {
            WriteInt32(texel, 0, value.Blue);
            WriteInt32(texel, 4, value.Green);
            WriteInt32(texel, 8, value.Red);
            WriteInt32(texel, 12, value.Alpha);
        }
    }

    private readonly struct Bgra16FloatTransfer : ISequentialTransfer<Rgba16Float>
    {
        public static Rgba16Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadHalf(texel, 4), ReadHalf(texel, 2), ReadHalf(texel, 0), ReadHalf(texel, 6));

        public static void Encode(Rgba16Float value, Span<byte> texel)
        {
            WriteHalf(texel, 0, value.Blue);
            WriteHalf(texel, 2, value.Green);
            WriteHalf(texel, 4, value.Red);
            WriteHalf(texel, 6, value.Alpha);
        }
    }

    private readonly struct Bgra32FloatTransfer : ISequentialTransfer<Rgba32Float>
    {
        public static Rgba32Float Decode(ReadOnlySpan<byte> texel) =>
            new(ReadSingle(texel, 8), ReadSingle(texel, 4), ReadSingle(texel, 0), ReadSingle(texel, 12));

        public static void Encode(Rgba32Float value, Span<byte> texel)
        {
            WriteSingle(texel, 0, value.Blue);
            WriteSingle(texel, 4, value.Green);
            WriteSingle(texel, 8, value.Red);
            WriteSingle(texel, 12, value.Alpha);
        }
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

    private static bool TryGetTransfer(TextureFormat format, out SequentialTransfer transfer)
    {
        if (TryGetIntegerTransfer(format, out transfer))
        {
            return true;
        }

        if (format == TextureFormats.Alpha8UNorm
            || format == TextureFormats.Alpha8UNormBigEndian)
        {
            transfer = SequentialTransfer.Alpha8UNorm;
            return true;
        }

        if (format == TextureFormats.Alpha8SNorm)
        {
            transfer = SequentialTransfer.Alpha8SNorm;
            return true;
        }

        if (format == TextureFormats.Alpha16UNorm)
        {
            transfer = SequentialTransfer.Alpha16UNorm;
            return true;
        }

        if (format == TextureFormats.Alpha16SNorm)
        {
            transfer = SequentialTransfer.Alpha16SNorm;
            return true;
        }

        if (format == TextureFormats.Alpha32UNorm)
        {
            transfer = SequentialTransfer.Alpha32UNorm;
            return true;
        }

        if (format == TextureFormats.Alpha32SNorm)
        {
            transfer = SequentialTransfer.Alpha32SNorm;
            return true;
        }

        if (format == TextureFormats.Alpha16Float)
        {
            transfer = SequentialTransfer.Alpha16Float;
            return true;
        }

        if (format == TextureFormats.Alpha32Float)
        {
            transfer = SequentialTransfer.Alpha32Float;
            return true;
        }

        if (format == TextureFormats.Luminance8UNorm
            || format == TextureFormats.Luminance8UNormBigEndian)
        {
            transfer = SequentialTransfer.Luminance8UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16UNorm)
        {
            transfer = SequentialTransfer.Luminance16UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16UNormBigEndian)
        {
            transfer = SequentialTransfer.Luminance16UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Luminance32UNorm)
        {
            transfer = SequentialTransfer.Luminance32UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32UNormBigEndian)
        {
            transfer = SequentialTransfer.Luminance32UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Luminance32SNorm)
        {
            transfer = SequentialTransfer.Luminance32SNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16Float)
        {
            transfer = SequentialTransfer.Luminance16Float;
            return true;
        }

        if (format == TextureFormats.Luminance32Float)
        {
            transfer = SequentialTransfer.Luminance32Float;
            return true;
        }

        if (format == TextureFormats.Luminance8Alpha8UNorm)
        {
            transfer = SequentialTransfer.Luminance8Alpha8UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance8Alpha8UNormBigEndian)
        {
            transfer = SequentialTransfer.Luminance8Alpha8UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Luminance16Alpha16UNorm)
        {
            transfer = SequentialTransfer.Luminance16Alpha16UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16Alpha16UNormBigEndian)
        {
            transfer = SequentialTransfer.Luminance16Alpha16UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Luminance16Alpha16SNorm)
        {
            transfer = SequentialTransfer.Luminance16Alpha16SNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16Alpha16Float)
        {
            transfer = SequentialTransfer.Luminance16Alpha16Float;
            return true;
        }

        if (format == TextureFormats.Luminance32Alpha32UNorm)
        {
            transfer = SequentialTransfer.Luminance32Alpha32UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32Alpha32UNormBigEndian)
        {
            transfer = SequentialTransfer.Luminance32Alpha32UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Luminance32Alpha32SNorm)
        {
            transfer = SequentialTransfer.Luminance32Alpha32SNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32Alpha32Float)
        {
            transfer = SequentialTransfer.Luminance32Alpha32Float;
            return true;
        }

        if (format == TextureFormats.Luminance8Srgb)
        {
            transfer = SequentialTransfer.Luminance8Srgb;
            return true;
        }

        if (format == TextureFormats.Luminance8Alpha8Srgb)
        {
            transfer = SequentialTransfer.Luminance8Alpha8Srgb;
            return true;
        }

        if (format == TextureFormats.Intensity8UNorm)
        {
            transfer = SequentialTransfer.Intensity8UNorm;
            return true;
        }

        if (format == TextureFormats.Intensity8SNorm)
        {
            transfer = SequentialTransfer.Intensity8SNorm;
            return true;
        }

        if (format == TextureFormats.Intensity16UNorm)
        {
            transfer = SequentialTransfer.Intensity16UNorm;
            return true;
        }

        if (format == TextureFormats.Intensity16SNorm)
        {
            transfer = SequentialTransfer.Intensity16SNorm;
            return true;
        }

        if (format == TextureFormats.Intensity32UNorm)
        {
            transfer = SequentialTransfer.Intensity32UNorm;
            return true;
        }

        if (format == TextureFormats.Intensity32SNorm)
        {
            transfer = SequentialTransfer.Intensity32SNorm;
            return true;
        }

        if (format == TextureFormats.Intensity16Float)
        {
            transfer = SequentialTransfer.Intensity16Float;
            return true;
        }

        if (format == TextureFormats.Intensity32Float)
        {
            transfer = SequentialTransfer.Intensity32Float;
            return true;
        }

        if (format == TextureFormats.R8)
        {
            transfer = SequentialTransfer.R8UNorm;
            return true;
        }

        if (format == TextureFormats.R8SNorm)
        {
            transfer = SequentialTransfer.R8SNorm;
            return true;
        }

        if (format == TextureFormats.R16UNorm)
        {
            transfer = SequentialTransfer.R16UNorm;
            return true;
        }

        if (format == TextureFormats.R16SNorm)
        {
            transfer = SequentialTransfer.R16SNorm;
            return true;
        }

        if (format == TextureFormats.R32UNorm)
        {
            transfer = SequentialTransfer.R32UNorm;
            return true;
        }

        if (format == TextureFormats.R32SNorm)
        {
            transfer = SequentialTransfer.R32SNorm;
            return true;
        }

        if (format == TextureFormats.R16Float)
        {
            transfer = SequentialTransfer.R16Float;
            return true;
        }

        if (format == TextureFormats.R16FloatBigEndian)
        {
            transfer = SequentialTransfer.R16FloatBigEndian;
            return true;
        }

        if (format == TextureFormats.R32Float)
        {
            transfer = SequentialTransfer.R32Float;
            return true;
        }

        if (format == TextureFormats.R32FloatBigEndian)
        {
            transfer = SequentialTransfer.R32FloatBigEndian;
            return true;
        }

        if (format == TextureFormats.R64Float)
        {
            transfer = SequentialTransfer.R64Float;
            return true;
        }

        if (format == TextureFormats.R8Srgb)
        {
            transfer = SequentialTransfer.R8Srgb;
            return true;
        }

        if (format == TextureFormats.Rg8)
        {
            transfer = SequentialTransfer.Rg8UNorm;
            return true;
        }

        if (format == TextureFormats.Rg8UNormBigEndian)
        {
            transfer = SequentialTransfer.Rg8UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg8SNorm)
        {
            transfer = SequentialTransfer.Rg8SNorm;
            return true;
        }

        if (format == TextureFormats.Rg8SNormBigEndian)
        {
            transfer = SequentialTransfer.Rg8SNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg16UNorm)
        {
            transfer = SequentialTransfer.Rg16UNorm;
            return true;
        }

        if (format == TextureFormats.Rg16UNormBigEndian)
        {
            transfer = SequentialTransfer.Rg16UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg16SNorm)
        {
            transfer = SequentialTransfer.Rg16SNorm;
            return true;
        }

        if (format == TextureFormats.Rg16SNormBigEndian)
        {
            transfer = SequentialTransfer.Rg16SNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg32UNorm)
        {
            transfer = SequentialTransfer.Rg32UNorm;
            return true;
        }

        if (format == TextureFormats.Rg32UNormBigEndian)
        {
            transfer = SequentialTransfer.Rg32UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg32SNorm)
        {
            transfer = SequentialTransfer.Rg32SNorm;
            return true;
        }

        if (format == TextureFormats.Rg32SNormBigEndian)
        {
            transfer = SequentialTransfer.Rg32SNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg16Float)
        {
            transfer = SequentialTransfer.Rg16Float;
            return true;
        }

        if (format == TextureFormats.Rg16FloatBigEndian)
        {
            transfer = SequentialTransfer.Rg16FloatBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg32Float)
        {
            transfer = SequentialTransfer.Rg32Float;
            return true;
        }

        if (format == TextureFormats.Rg32FloatBigEndian)
        {
            transfer = SequentialTransfer.Rg32FloatBigEndian;
            return true;
        }

        if (format == TextureFormats.Rg64Float)
        {
            transfer = SequentialTransfer.Rg64Float;
            return true;
        }

        if (format == TextureFormats.Rg8Srgb)
        {
            transfer = SequentialTransfer.Rg8Srgb;
            return true;
        }

        if (format == TextureFormats.Rgb8)
        {
            transfer = SequentialTransfer.Rgb8UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb8SNorm)
        {
            transfer = SequentialTransfer.Rgb8SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb16UNorm)
        {
            transfer = SequentialTransfer.Rgb16UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb16SNorm)
        {
            transfer = SequentialTransfer.Rgb16SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb32UNorm)
        {
            transfer = SequentialTransfer.Rgb32UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb32SNorm)
        {
            transfer = SequentialTransfer.Rgb32SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb16Float)
        {
            transfer = SequentialTransfer.Rgb16Float;
            return true;
        }

        if (format == TextureFormats.Rgb32Float)
        {
            transfer = SequentialTransfer.Rgb32Float;
            return true;
        }

        if (format == TextureFormats.Rgb64Float)
        {
            transfer = SequentialTransfer.Rgb64Float;
            return true;
        }

        if (format == TextureFormats.Rgb8Srgb)
        {
            transfer = SequentialTransfer.Rgb8Srgb;
            return true;
        }

        if (format == TextureFormats.Bgr8UNorm)
        {
            transfer = SequentialTransfer.Bgr8UNorm;
            return true;
        }

        if (format == TextureFormats.Bgr8SNorm)
        {
            transfer = SequentialTransfer.Bgr8SNorm;
            return true;
        }

        if (format == TextureFormats.Bgr16UNorm)
        {
            transfer = SequentialTransfer.Bgr16UNorm;
            return true;
        }

        if (format == TextureFormats.Bgr16SNorm)
        {
            transfer = SequentialTransfer.Bgr16SNorm;
            return true;
        }

        if (format == TextureFormats.Bgr32UNorm)
        {
            transfer = SequentialTransfer.Bgr32UNorm;
            return true;
        }

        if (format == TextureFormats.Bgr32SNorm)
        {
            transfer = SequentialTransfer.Bgr32SNorm;
            return true;
        }

        if (format == TextureFormats.Bgr16Float)
        {
            transfer = SequentialTransfer.Bgr16Float;
            return true;
        }

        if (format == TextureFormats.Bgr32Float)
        {
            transfer = SequentialTransfer.Bgr32Float;
            return true;
        }

        if (format == TextureFormats.Bgr8Srgb)
        {
            transfer = SequentialTransfer.Bgr8Srgb;
            return true;
        }

        if (format == TextureFormats.Rgba8UNorm)
        {
            transfer = SequentialTransfer.Rgba8UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba8UNormBigEndian)
        {
            transfer = SequentialTransfer.Rgba8UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba8SNorm)
        {
            transfer = SequentialTransfer.Rgba8SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba8SNormBigEndian)
        {
            transfer = SequentialTransfer.Rgba8SNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba16UNorm)
        {
            transfer = SequentialTransfer.Rgba16UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16UNormBigEndian)
        {
            transfer = SequentialTransfer.Rgba16UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba16SNorm)
        {
            transfer = SequentialTransfer.Rgba16SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16SNormBigEndian)
        {
            transfer = SequentialTransfer.Rgba16SNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba32UNorm)
        {
            transfer = SequentialTransfer.Rgba32UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba32UNormBigEndian)
        {
            transfer = SequentialTransfer.Rgba32UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba32SNorm)
        {
            transfer = SequentialTransfer.Rgba32SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba32SNormBigEndian)
        {
            transfer = SequentialTransfer.Rgba32SNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba16Float)
        {
            transfer = SequentialTransfer.Rgba16Float;
            return true;
        }

        if (format == TextureFormats.Rgba16FloatBigEndian)
        {
            transfer = SequentialTransfer.Rgba16FloatBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba32Float)
        {
            transfer = SequentialTransfer.Rgba32Float;
            return true;
        }

        if (format == TextureFormats.Rgba32FloatBigEndian)
        {
            transfer = SequentialTransfer.Rgba32FloatBigEndian;
            return true;
        }

        if (format == TextureFormats.Rgba64Float)
        {
            transfer = SequentialTransfer.Rgba64Float;
            return true;
        }

        if (format == TextureFormats.Rgba8Srgb)
        {
            transfer = SequentialTransfer.Rgba8Srgb;
            return true;
        }

        if (format == TextureFormats.Abgr8UNorm)
        {
            transfer = SequentialTransfer.Abgr8UNorm;
            return true;
        }

        if (format == TextureFormats.Abgr8SNorm)
        {
            transfer = SequentialTransfer.Abgr8SNorm;
            return true;
        }

        if (format == TextureFormats.Abgr8Srgb)
        {
            transfer = SequentialTransfer.Abgr8Srgb;
            return true;
        }

        if (format == TextureFormats.Bgra8)
        {
            transfer = SequentialTransfer.Bgra8UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra8BigEndian)
        {
            transfer = SequentialTransfer.Bgra8UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Bgra8SNorm)
        {
            transfer = SequentialTransfer.Bgra8SNorm;
            return true;
        }

        if (format == TextureFormats.Bgra8Srgb)
        {
            transfer = SequentialTransfer.Bgra8Srgb;
            return true;
        }

        if (format == TextureFormats.Bgrx8UNorm)
        {
            transfer = SequentialTransfer.Bgrx8UNorm;
            return true;
        }

        if (format == TextureFormats.Bgrx8UNormBigEndian)
        {
            transfer = SequentialTransfer.Bgrx8UNormBigEndian;
            return true;
        }

        if (format == TextureFormats.Bgrx8Srgb)
        {
            transfer = SequentialTransfer.Bgrx8Srgb;
            return true;
        }

        if (format == TextureFormats.Bgra16UNorm)
        {
            transfer = SequentialTransfer.Bgra16UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra16SNorm)
        {
            transfer = SequentialTransfer.Bgra16SNorm;
            return true;
        }

        if (format == TextureFormats.Bgra32UNorm)
        {
            transfer = SequentialTransfer.Bgra32UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra32SNorm)
        {
            transfer = SequentialTransfer.Bgra32SNorm;
            return true;
        }

        if (format == TextureFormats.Bgra16Float)
        {
            transfer = SequentialTransfer.Bgra16Float;
            return true;
        }

        if (format == TextureFormats.Bgra32Float)
        {
            transfer = SequentialTransfer.Bgra32Float;
            return true;
        }

        transfer = default;
        return false;
    }

    private static bool TryGetIntegerTransfer(TextureFormat format, out SequentialTransfer transfer)
    {
        if (format == TextureFormats.Alpha8UInt)
        {
            transfer = SequentialTransfer.Alpha8UNorm;
            return true;
        }

        if (format == TextureFormats.Alpha8SInt)
        {
            transfer = SequentialTransfer.Alpha8SNorm;
            return true;
        }

        if (format == TextureFormats.Alpha16UInt)
        {
            transfer = SequentialTransfer.Alpha16UNorm;
            return true;
        }

        if (format == TextureFormats.Alpha16SInt)
        {
            transfer = SequentialTransfer.Alpha16SNorm;
            return true;
        }

        if (format == TextureFormats.Alpha32UInt)
        {
            transfer = SequentialTransfer.Alpha32UNorm;
            return true;
        }

        if (format == TextureFormats.Alpha32SInt)
        {
            transfer = SequentialTransfer.Alpha32SNorm;
            return true;
        }

        if (format == TextureFormats.Luminance8UInt)
        {
            transfer = SequentialTransfer.Luminance8UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance8SInt)
        {
            transfer = SequentialTransfer.Luminance8SInt;
            return true;
        }

        if (format == TextureFormats.Luminance16UInt)
        {
            transfer = SequentialTransfer.Luminance16UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16SInt)
        {
            transfer = SequentialTransfer.Luminance16SInt;
            return true;
        }

        if (format == TextureFormats.Luminance32UInt)
        {
            transfer = SequentialTransfer.Luminance32UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32SInt)
        {
            transfer = SequentialTransfer.Luminance32SNorm;
            return true;
        }

        if (format == TextureFormats.Luminance8Alpha8UInt)
        {
            transfer = SequentialTransfer.Luminance8Alpha8UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance8Alpha8SInt)
        {
            transfer = SequentialTransfer.Luminance8Alpha8SInt;
            return true;
        }

        if (format == TextureFormats.Luminance16Alpha16UInt)
        {
            transfer = SequentialTransfer.Luminance16Alpha16UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16Alpha16SInt)
        {
            transfer = SequentialTransfer.Luminance16Alpha16SNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32Alpha32UInt)
        {
            transfer = SequentialTransfer.Luminance32Alpha32UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32Alpha32SInt)
        {
            transfer = SequentialTransfer.Luminance32Alpha32SNorm;
            return true;
        }

        if (format == TextureFormats.Intensity8UInt)
        {
            transfer = SequentialTransfer.Intensity8UNorm;
            return true;
        }

        if (format == TextureFormats.Intensity8SInt)
        {
            transfer = SequentialTransfer.Intensity8SNorm;
            return true;
        }

        if (format == TextureFormats.Intensity16UInt)
        {
            transfer = SequentialTransfer.Intensity16UNorm;
            return true;
        }

        if (format == TextureFormats.Intensity16SInt)
        {
            transfer = SequentialTransfer.Intensity16SNorm;
            return true;
        }

        if (format == TextureFormats.Intensity32UInt)
        {
            transfer = SequentialTransfer.Intensity32UNorm;
            return true;
        }

        if (format == TextureFormats.Intensity32SInt)
        {
            transfer = SequentialTransfer.Intensity32SNorm;
            return true;
        }

        if (format == TextureFormats.R8UInt)
        {
            transfer = SequentialTransfer.R8UNorm;
            return true;
        }

        if (format == TextureFormats.R8SInt)
        {
            transfer = SequentialTransfer.R8SNorm;
            return true;
        }

        if (format == TextureFormats.R16UInt)
        {
            transfer = SequentialTransfer.R16UNorm;
            return true;
        }

        if (format == TextureFormats.R16SInt)
        {
            transfer = SequentialTransfer.R16SNorm;
            return true;
        }

        if (format == TextureFormats.R32UInt)
        {
            transfer = SequentialTransfer.R32UNorm;
            return true;
        }

        if (format == TextureFormats.R32SInt)
        {
            transfer = SequentialTransfer.R32SNorm;
            return true;
        }

        if (format == TextureFormats.R64UInt)
        {
            transfer = SequentialTransfer.R64UNorm;
            return true;
        }

        if (format == TextureFormats.R64SInt)
        {
            transfer = SequentialTransfer.R64SNorm;
            return true;
        }

        if (format == TextureFormats.Rg8UInt)
        {
            transfer = SequentialTransfer.Rg8UNorm;
            return true;
        }

        if (format == TextureFormats.Rg8SInt)
        {
            transfer = SequentialTransfer.Rg8SNorm;
            return true;
        }

        if (format == TextureFormats.Rg16UInt)
        {
            transfer = SequentialTransfer.Rg16UNorm;
            return true;
        }

        if (format == TextureFormats.Rg16SInt)
        {
            transfer = SequentialTransfer.Rg16SNorm;
            return true;
        }

        if (format == TextureFormats.Rg32UInt)
        {
            transfer = SequentialTransfer.Rg32UNorm;
            return true;
        }

        if (format == TextureFormats.Rg32SInt)
        {
            transfer = SequentialTransfer.Rg32SNorm;
            return true;
        }

        if (format == TextureFormats.Rg64UInt)
        {
            transfer = SequentialTransfer.Rg64UNorm;
            return true;
        }

        if (format == TextureFormats.Rg64SInt)
        {
            transfer = SequentialTransfer.Rg64SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb8UInt)
        {
            transfer = SequentialTransfer.Rgb8UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb8SInt)
        {
            transfer = SequentialTransfer.Rgb8SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb16UInt)
        {
            transfer = SequentialTransfer.Rgb16UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb16SInt)
        {
            transfer = SequentialTransfer.Rgb16SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb32UInt)
        {
            transfer = SequentialTransfer.Rgb32UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb32SInt)
        {
            transfer = SequentialTransfer.Rgb32SNorm;
            return true;
        }

        if (format == TextureFormats.Rgb64UInt)
        {
            transfer = SequentialTransfer.Rgb64UNorm;
            return true;
        }

        if (format == TextureFormats.Rgb64SInt)
        {
            transfer = SequentialTransfer.Rgb64SNorm;
            return true;
        }

        if (format == TextureFormats.Bgr8UInt)
        {
            transfer = SequentialTransfer.Bgr8UNorm;
            return true;
        }

        if (format == TextureFormats.Bgr8SInt)
        {
            transfer = SequentialTransfer.Bgr8SNorm;
            return true;
        }

        if (format == TextureFormats.Bgr16UInt)
        {
            transfer = SequentialTransfer.Bgr16UNorm;
            return true;
        }

        if (format == TextureFormats.Bgr16SInt)
        {
            transfer = SequentialTransfer.Bgr16SNorm;
            return true;
        }

        if (format == TextureFormats.Bgr32UInt)
        {
            transfer = SequentialTransfer.Bgr32UNorm;
            return true;
        }

        if (format == TextureFormats.Bgr32SInt)
        {
            transfer = SequentialTransfer.Bgr32SNorm;
            return true;
        }

        if (format == TextureFormats.Abgr8UInt)
        {
            transfer = SequentialTransfer.Abgr8UNorm;
            return true;
        }

        if (format == TextureFormats.Abgr8SInt)
        {
            transfer = SequentialTransfer.Abgr8SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba8UInt)
        {
            transfer = SequentialTransfer.Rgba8UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba8SInt)
        {
            transfer = SequentialTransfer.Rgba8SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16UInt)
        {
            transfer = SequentialTransfer.Rgba16UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16SInt)
        {
            transfer = SequentialTransfer.Rgba16SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba32UInt)
        {
            transfer = SequentialTransfer.Rgba32UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba32SInt)
        {
            transfer = SequentialTransfer.Rgba32SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba64UInt)
        {
            transfer = SequentialTransfer.Rgba64UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba64SInt)
        {
            transfer = SequentialTransfer.Rgba64SNorm;
            return true;
        }

        if (format == TextureFormats.Bgra8UInt)
        {
            transfer = SequentialTransfer.Bgra8UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra8SInt)
        {
            transfer = SequentialTransfer.Bgra8SNorm;
            return true;
        }

        if (format == TextureFormats.Bgra16UInt)
        {
            transfer = SequentialTransfer.Bgra16UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra16SInt)
        {
            transfer = SequentialTransfer.Bgra16SNorm;
            return true;
        }

        if (format == TextureFormats.Bgra32UInt)
        {
            transfer = SequentialTransfer.Bgra32UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra32SInt)
        {
            transfer = SequentialTransfer.Bgra32SNorm;
            return true;
        }

        transfer = default;
        return false;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));

    private static short ReadInt16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset, sizeof(short)));

    private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));

    private static int ReadInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));

    private static ulong ReadUInt64(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, sizeof(ulong)));

    private static long ReadInt64(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset, sizeof(long)));

    private static Half ReadHalf(ReadOnlySpan<byte> source, int offset) =>
        BitConverter.UInt16BitsToHalf(ReadUInt16(source, offset));

    private static float ReadSingle(ReadOnlySpan<byte> source, int offset) =>
        BitConverter.Int32BitsToSingle(ReadInt32(source, offset));

    private static double ReadDouble(ReadOnlySpan<byte> source, int offset) =>
        BitConverter.Int64BitsToDouble(ReadInt64(source, offset));

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);

    private static void WriteInt16(Span<byte> destination, int offset, short value) =>
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(offset, sizeof(short)), value);

    private static void WriteUInt32(Span<byte> destination, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);

    private static void WriteInt32(Span<byte> destination, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), value);

    private static void WriteUInt64(Span<byte> destination, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, sizeof(ulong)), value);

    private static void WriteInt64(Span<byte> destination, int offset, long value) =>
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, sizeof(long)), value);

    private static void WriteHalf(Span<byte> destination, int offset, Half value) =>
        WriteUInt16(destination, offset, BitConverter.HalfToUInt16Bits(value));

    private static void WriteSingle(Span<byte> destination, int offset, float value) =>
        WriteInt32(destination, offset, BitConverter.SingleToInt32Bits(value));

    private static void WriteDouble(Span<byte> destination, int offset, double value) =>
        WriteInt64(destination, offset, BitConverter.DoubleToInt64Bits(value));

    private static float DecodeSrgb(byte value) =>
        RgbaColorConversions.Srgb8ToLinearFloat(value);

    private static byte EncodeSrgb(float value) =>
        RgbaColorConversions.LinearFloatToSrgb8(value);

    private static float DecodeUNorm(byte value) =>
        RgbaColorConversions.UNorm8ToFloat(value);

    private static byte EncodeUNorm(float value) =>
        RgbaColorConversions.FloatToUNorm8(value);

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Sequential uncompressed texture coder does not support texture format '{format.Name}'.");

    private enum SequentialTransfer
    {
        Alpha8UNorm,
        Alpha8SNorm,
        Alpha16UNorm,
        Alpha16SNorm,
        Alpha32UNorm,
        Alpha32SNorm,
        Alpha16Float,
        Alpha32Float,
        Luminance8UNorm,
        Luminance8SInt,
        Luminance16UNorm,
        Luminance16UNormBigEndian,
        Luminance16SInt,
        Luminance32UNorm,
        Luminance32UNormBigEndian,
        Luminance32SNorm,
        Luminance16Float,
        Luminance32Float,
        Luminance8Alpha8UNorm,
        Luminance8Alpha8UNormBigEndian,
        Luminance8Alpha8SInt,
        Luminance16Alpha16UNorm,
        Luminance16Alpha16UNormBigEndian,
        Luminance16Alpha16SNorm,
        Luminance16Alpha16Float,
        Luminance32Alpha32UNorm,
        Luminance32Alpha32UNormBigEndian,
        Luminance32Alpha32SNorm,
        Luminance32Alpha32Float,
        Luminance8Srgb,
        Luminance8Alpha8Srgb,
        Intensity8UNorm,
        Intensity8SNorm,
        Intensity16UNorm,
        Intensity16SNorm,
        Intensity32UNorm,
        Intensity32SNorm,
        Intensity16Float,
        Intensity32Float,
        R8UNorm,
        R8SNorm,
        R16UNorm,
        R16SNorm,
        R32UNorm,
        R32SNorm,
        R16Float,
        R16FloatBigEndian,
        R32Float,
        R32FloatBigEndian,
        R64UNorm,
        R64SNorm,
        R64Float,
        R8Srgb,
        Rg8UNorm,
        Rg8UNormBigEndian,
        Rg8SNorm,
        Rg8SNormBigEndian,
        Rg16UNorm,
        Rg16UNormBigEndian,
        Rg16SNorm,
        Rg16SNormBigEndian,
        Rg32UNorm,
        Rg32UNormBigEndian,
        Rg32SNorm,
        Rg32SNormBigEndian,
        Rg16Float,
        Rg16FloatBigEndian,
        Rg32Float,
        Rg32FloatBigEndian,
        Rg64UNorm,
        Rg64SNorm,
        Rg64Float,
        Rg8Srgb,
        Rgb8UNorm,
        Rgb8SNorm,
        Rgb16UNorm,
        Rgb16SNorm,
        Rgb32UNorm,
        Rgb32SNorm,
        Rgb16Float,
        Rgb32Float,
        Rgb64UNorm,
        Rgb64SNorm,
        Rgb64Float,
        Rgb8Srgb,
        Bgr8UNorm,
        Bgr8SNorm,
        Bgr16UNorm,
        Bgr16SNorm,
        Bgr32UNorm,
        Bgr32SNorm,
        Bgr16Float,
        Bgr32Float,
        Bgr8Srgb,
        Rgba8UNorm,
        Rgba8UNormBigEndian,
        Rgba8SNorm,
        Rgba8SNormBigEndian,
        Rgba16UNorm,
        Rgba16UNormBigEndian,
        Rgba16SNorm,
        Rgba16SNormBigEndian,
        Rgba32UNorm,
        Rgba32UNormBigEndian,
        Rgba32SNorm,
        Rgba32SNormBigEndian,
        Rgba16Float,
        Rgba16FloatBigEndian,
        Rgba32Float,
        Rgba32FloatBigEndian,
        Rgba64UNorm,
        Rgba64SNorm,
        Rgba64Float,
        Rgba8Srgb,
        Abgr8UNorm,
        Abgr8SNorm,
        Abgr8Srgb,
        Bgra8UNorm,
        Bgra8UNormBigEndian,
        Bgra8SNorm,
        Bgra8Srgb,
        Bgrx8UNorm,
        Bgrx8UNormBigEndian,
        Bgrx8Srgb,
        Bgra16UNorm,
        Bgra16SNorm,
        Bgra32UNorm,
        Bgra32SNorm,
        Bgra16Float,
        Bgra32Float
    }
}
