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
            case SequentialTransfer.Luminance16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32UNormTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Luminance16Alpha16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16Alpha16UNormTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Luminance32Alpha32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Luminance32Alpha32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance32Alpha32FloatTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.R32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rg8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rg8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rg16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rg16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rg32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rg32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rg16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg32FloatTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Rgba8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgba8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgba8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16UNorm:
                Decode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgba16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16SNorm:
                Decode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgba16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32UNorm:
                Decode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgba32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32SNorm:
                Decode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgba32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16Float:
                Decode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgba16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32Float:
                Decode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Abgr8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Abgr8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgra8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8SNorm:
                Decode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Bgra8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8UNorm:
                Decode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgrx8UNormTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Luminance16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Luminance32UNormTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Luminance16Alpha16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Luminance16Alpha16UNormTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Luminance32Alpha32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Luminance32Alpha32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Luminance32Alpha32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Luminance32Alpha32FloatTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.R32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, R32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rg8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rg8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rg16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rg16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rg32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rg32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rg16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rg32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rg32FloatTransfer>(source, destination, rowPitch);
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
            case SequentialTransfer.Rgba8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Rgba8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Rgba8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16UNorm:
                Encode<TPixel, Rgba16UNorm, Rgba16UNormCarrierTransfer, Rgba16UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16SNorm:
                Encode<TPixel, Rgba16SNorm, Rgba16SNormCarrierTransfer, Rgba16SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32UNorm:
                Encode<TPixel, Rgba32UNorm, Rgba32UNormCarrierTransfer, Rgba32UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32SNorm:
                Encode<TPixel, Rgba32SNorm, Rgba32SNormCarrierTransfer, Rgba32SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba16Float:
                Encode<TPixel, Rgba16Float, Rgba16FloatCarrierTransfer, Rgba16FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Rgba32Float:
                Encode<TPixel, Rgba32Float, Rgba32FloatCarrierTransfer, Rgba32FloatTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Abgr8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Abgr8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Abgr8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgra8UNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgra8SNorm:
                Encode<TPixel, Rgba8SNorm, Rgba8SNormCarrierTransfer, Bgra8SNormTransfer>(source, destination, rowPitch);
                return;
            case SequentialTransfer.Bgrx8UNorm:
                Encode<TPixel, Rgba8UNorm, Rgba8UNormCarrierTransfer, Bgrx8UNormTransfer>(source, destination, rowPitch);
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
                TTransfer.Encode(
                    TCarrierTransfer.ToCarrier(sourceRow[x]),
                    destination.Slice(texelOffset, bytesPerTexel));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
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

    private readonly struct Luminance16UNormTransfer : ISequentialTransfer<Rgba16UNorm>
    {
        public static Rgba16UNorm Decode(ReadOnlySpan<byte> texel)
        {
            var value = ReadUInt16(texel, 0);
            return new Rgba16UNorm(value, value, value);
        }

        public static void Encode(Rgba16UNorm value, Span<byte> texel) => WriteUInt16(texel, 0, value.Red);
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
        if (format == TextureFormats.Alpha8UNorm)
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

        if (format == TextureFormats.Luminance8UNorm)
        {
            transfer = SequentialTransfer.Luminance8UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance16UNorm)
        {
            transfer = SequentialTransfer.Luminance16UNorm;
            return true;
        }

        if (format == TextureFormats.Luminance32UNorm)
        {
            transfer = SequentialTransfer.Luminance32UNorm;
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

        if (format == TextureFormats.Luminance16Alpha16UNorm)
        {
            transfer = SequentialTransfer.Luminance16Alpha16UNorm;
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

        if (format == TextureFormats.R32Float)
        {
            transfer = SequentialTransfer.R32Float;
            return true;
        }

        if (format == TextureFormats.Rg8)
        {
            transfer = SequentialTransfer.Rg8UNorm;
            return true;
        }

        if (format == TextureFormats.Rg8SNorm)
        {
            transfer = SequentialTransfer.Rg8SNorm;
            return true;
        }

        if (format == TextureFormats.Rg16UNorm)
        {
            transfer = SequentialTransfer.Rg16UNorm;
            return true;
        }

        if (format == TextureFormats.Rg16SNorm)
        {
            transfer = SequentialTransfer.Rg16SNorm;
            return true;
        }

        if (format == TextureFormats.Rg32UNorm)
        {
            transfer = SequentialTransfer.Rg32UNorm;
            return true;
        }

        if (format == TextureFormats.Rg32SNorm)
        {
            transfer = SequentialTransfer.Rg32SNorm;
            return true;
        }

        if (format == TextureFormats.Rg16Float)
        {
            transfer = SequentialTransfer.Rg16Float;
            return true;
        }

        if (format == TextureFormats.Rg32Float)
        {
            transfer = SequentialTransfer.Rg32Float;
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

        if (format == TextureFormats.Rgba8UNorm)
        {
            transfer = SequentialTransfer.Rgba8UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba8SNorm)
        {
            transfer = SequentialTransfer.Rgba8SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16UNorm)
        {
            transfer = SequentialTransfer.Rgba16UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16SNorm)
        {
            transfer = SequentialTransfer.Rgba16SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba32UNorm)
        {
            transfer = SequentialTransfer.Rgba32UNorm;
            return true;
        }

        if (format == TextureFormats.Rgba32SNorm)
        {
            transfer = SequentialTransfer.Rgba32SNorm;
            return true;
        }

        if (format == TextureFormats.Rgba16Float)
        {
            transfer = SequentialTransfer.Rgba16Float;
            return true;
        }

        if (format == TextureFormats.Rgba32Float)
        {
            transfer = SequentialTransfer.Rgba32Float;
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

        if (format == TextureFormats.Bgra8)
        {
            transfer = SequentialTransfer.Bgra8UNorm;
            return true;
        }

        if (format == TextureFormats.Bgra8SNorm)
        {
            transfer = SequentialTransfer.Bgra8SNorm;
            return true;
        }

        if (format == TextureFormats.Bgrx8UNorm)
        {
            transfer = SequentialTransfer.Bgrx8UNorm;
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

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));

    private static short ReadInt16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset, sizeof(short)));

    private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));

    private static int ReadInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));

    private static Half ReadHalf(ReadOnlySpan<byte> source, int offset) =>
        BitConverter.UInt16BitsToHalf(ReadUInt16(source, offset));

    private static float ReadSingle(ReadOnlySpan<byte> source, int offset) =>
        BitConverter.Int32BitsToSingle(ReadInt32(source, offset));

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);

    private static void WriteInt16(Span<byte> destination, int offset, short value) =>
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(offset, sizeof(short)), value);

    private static void WriteUInt32(Span<byte> destination, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);

    private static void WriteInt32(Span<byte> destination, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), value);

    private static void WriteHalf(Span<byte> destination, int offset, Half value) =>
        WriteUInt16(destination, offset, BitConverter.HalfToUInt16Bits(value));

    private static void WriteSingle(Span<byte> destination, int offset, float value) =>
        WriteInt32(destination, offset, BitConverter.SingleToInt32Bits(value));

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
        Luminance16UNorm,
        Luminance32UNorm,
        Luminance32SNorm,
        Luminance16Float,
        Luminance32Float,
        Luminance8Alpha8UNorm,
        Luminance16Alpha16UNorm,
        Luminance16Alpha16SNorm,
        Luminance16Alpha16Float,
        Luminance32Alpha32UNorm,
        Luminance32Alpha32SNorm,
        Luminance32Alpha32Float,
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
        R32Float,
        Rg8UNorm,
        Rg8SNorm,
        Rg16UNorm,
        Rg16SNorm,
        Rg32UNorm,
        Rg32SNorm,
        Rg16Float,
        Rg32Float,
        Rgb8UNorm,
        Rgb8SNorm,
        Rgb16UNorm,
        Rgb16SNorm,
        Rgb32UNorm,
        Rgb32SNorm,
        Rgb16Float,
        Rgb32Float,
        Bgr8UNorm,
        Bgr8SNorm,
        Bgr16UNorm,
        Bgr16SNorm,
        Bgr32UNorm,
        Bgr32SNorm,
        Bgr16Float,
        Bgr32Float,
        Rgba8UNorm,
        Rgba8SNorm,
        Rgba16UNorm,
        Rgba16SNorm,
        Rgba32UNorm,
        Rgba32SNorm,
        Rgba16Float,
        Rgba32Float,
        Abgr8UNorm,
        Abgr8SNorm,
        Bgra8UNorm,
        Bgra8SNorm,
        Bgrx8UNorm,
        Bgra16UNorm,
        Bgra16SNorm,
        Bgra32UNorm,
        Bgra32SNorm,
        Bgra16Float,
        Bgra32Float
    }
}
