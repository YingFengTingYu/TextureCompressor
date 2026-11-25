using PVRTexLib;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.PVRTexLib;

public sealed unsafe class PVRTexLibTextureCoder : IPitchTextureCoder
{
    private static readonly ulong SRgba8PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 8, 8, 8, 8);
    private static readonly ulong SRgba16PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 16, 16, 16, 16);
    private static readonly ulong SRgba32PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 32, 32, 32, 32);

    private static readonly FormatMapping[] SMappings =
    [
        new(TextureFormats.RgbEtc1UNorm, PVRTexLibPixelFormat.ETC1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.RgbEtc2UNorm, PVRTexLibPixelFormat.ETC2_RGB, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.RgbEtc2Srgb, PVRTexLibPixelFormat.ETC2_RGB, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.RgbA1Etc2UNorm, PVRTexLibPixelFormat.ETC2_RGB_A1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.RgbA1Etc2Srgb, PVRTexLibPixelFormat.ETC2_RGB_A1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.RgbaEtc2EacUNorm, PVRTexLibPixelFormat.ETC2_RGBA, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.RgbaEtc2EacSrgb, PVRTexLibPixelFormat.ETC2_RGBA, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Etc),
        new(TextureFormats.R11EacUNorm, PVRTexLibPixelFormat.EAC_R11, SourceKind.Rgba16UNorm, PVRTexLibVariableType.UnsignedShortNorm, CompressorFamily.Etc),
        new(TextureFormats.R11EacSNorm, PVRTexLibPixelFormat.EAC_R11, SourceKind.Rgba16SNorm, PVRTexLibVariableType.SignedShortNorm, CompressorFamily.Etc),
        new(TextureFormats.Rg11EacUNorm, PVRTexLibPixelFormat.EAC_RG11, SourceKind.Rgba16UNorm, PVRTexLibVariableType.UnsignedShortNorm, CompressorFamily.Etc),
        new(TextureFormats.Rg11EacSNorm, PVRTexLibPixelFormat.EAC_RG11, SourceKind.Rgba16SNorm, PVRTexLibVariableType.SignedShortNorm, CompressorFamily.Etc),

        new(TextureFormats.RgbPvrtcI2BppUNorm, PVRTexLibPixelFormat.PVRTCI_2bpp_RGB, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcI2BppSrgb, PVRTexLibPixelFormat.PVRTCI_2bpp_RGB, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcI2BppUNorm, PVRTexLibPixelFormat.PVRTCI_2bpp_RGBA, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcI2BppSrgb, PVRTexLibPixelFormat.PVRTCI_2bpp_RGBA, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcI4BppUNorm, PVRTexLibPixelFormat.PVRTCI_4bpp_RGB, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcI4BppSrgb, PVRTexLibPixelFormat.PVRTCI_4bpp_RGB, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcI4BppUNorm, PVRTexLibPixelFormat.PVRTCI_4bpp_RGBA, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcI4BppSrgb, PVRTexLibPixelFormat.PVRTCI_4bpp_RGBA, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcII2BppUNorm, PVRTexLibPixelFormat.PVRTCII_2bpp, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcII2BppSrgb, PVRTexLibPixelFormat.PVRTCII_2bpp, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcII4BppUNorm, PVRTexLibPixelFormat.PVRTCII_4bpp, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbaPvrtcII4BppSrgb, PVRTexLibPixelFormat.PVRTCII_4bpp, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcI6BppFloat, PVRTexLibPixelFormat.PVRTCI_HDR_6bpp, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcI8BppFloat, PVRTexLibPixelFormat.PVRTCI_HDR_8bpp, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcII6BppFloat, PVRTexLibPixelFormat.PVRTCII_HDR_6bpp, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Pvrtc),
        new(TextureFormats.RgbPvrtcII8BppFloat, PVRTexLibPixelFormat.PVRTCII_HDR_8bpp, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Pvrtc),

        new(TextureFormats.Bc1Rgb, PVRTexLibPixelFormat.BC1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other, Opaque: true),
        new(TextureFormats.Bc1RgbSrgb, PVRTexLibPixelFormat.BC1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other, Opaque: true),
        new(TextureFormats.Bc1Rgba, PVRTexLibPixelFormat.BC1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc1RgbaSrgb, PVRTexLibPixelFormat.BC1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Dxt1Rgb, PVRTexLibPixelFormat.DXT1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other, Opaque: true),
        new(TextureFormats.Dxt1RgbSrgb, PVRTexLibPixelFormat.DXT1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other, Opaque: true),
        new(TextureFormats.Dxt1Rgba, PVRTexLibPixelFormat.DXT1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Dxt1RgbaSrgb, PVRTexLibPixelFormat.DXT1, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Dxt2Rgba, PVRTexLibPixelFormat.DXT2, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other, Premultiplied: true),
        new(TextureFormats.Dxt3Rgba, PVRTexLibPixelFormat.DXT3, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Dxt3RgbaSrgb, PVRTexLibPixelFormat.DXT3, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Dxt4Rgba, PVRTexLibPixelFormat.DXT4, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other, Premultiplied: true),
        new(TextureFormats.Dxt5Rgba, PVRTexLibPixelFormat.DXT5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Dxt5RgbaSrgb, PVRTexLibPixelFormat.DXT5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc2Rgba, PVRTexLibPixelFormat.BC2, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc2RgbaSrgb, PVRTexLibPixelFormat.BC2, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc3Rgba, PVRTexLibPixelFormat.BC3, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc3RgbaSrgb, PVRTexLibPixelFormat.BC3, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc4UNorm, PVRTexLibPixelFormat.BC4, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc4SNorm, PVRTexLibPixelFormat.BC4, SourceKind.SignedBc45, PVRTexLibVariableType.SignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc5UNorm, PVRTexLibPixelFormat.BC5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.Bc5SNorm, PVRTexLibPixelFormat.BC5, SourceKind.SignedBc45, PVRTexLibVariableType.SignedByteNorm, CompressorFamily.Other),
        new(TextureFormats.RgbaAstc4x4UNorm, PVRTexLibPixelFormat.ASTC_4x4, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc4x4Srgb, PVRTexLibPixelFormat.ASTC_4x4, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc4x4Float, PVRTexLibPixelFormat.ASTC_4x4, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc5x4UNorm, PVRTexLibPixelFormat.ASTC_5x4, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc5x4Srgb, PVRTexLibPixelFormat.ASTC_5x4, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc5x4Float, PVRTexLibPixelFormat.ASTC_5x4, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc5x5UNorm, PVRTexLibPixelFormat.ASTC_5x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc5x5Srgb, PVRTexLibPixelFormat.ASTC_5x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc5x5Float, PVRTexLibPixelFormat.ASTC_5x5, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc6x5UNorm, PVRTexLibPixelFormat.ASTC_6x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc6x5Srgb, PVRTexLibPixelFormat.ASTC_6x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc6x5Float, PVRTexLibPixelFormat.ASTC_6x5, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc6x6UNorm, PVRTexLibPixelFormat.ASTC_6x6, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc6x6Srgb, PVRTexLibPixelFormat.ASTC_6x6, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc6x6Float, PVRTexLibPixelFormat.ASTC_6x6, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x5UNorm, PVRTexLibPixelFormat.ASTC_8x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x5Srgb, PVRTexLibPixelFormat.ASTC_8x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x5Float, PVRTexLibPixelFormat.ASTC_8x5, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x6UNorm, PVRTexLibPixelFormat.ASTC_8x6, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x6Srgb, PVRTexLibPixelFormat.ASTC_8x6, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x6Float, PVRTexLibPixelFormat.ASTC_8x6, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x8UNorm, PVRTexLibPixelFormat.ASTC_8x8, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x8Srgb, PVRTexLibPixelFormat.ASTC_8x8, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc8x8Float, PVRTexLibPixelFormat.ASTC_8x8, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x5UNorm, PVRTexLibPixelFormat.ASTC_10x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x5Srgb, PVRTexLibPixelFormat.ASTC_10x5, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x5Float, PVRTexLibPixelFormat.ASTC_10x5, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x6UNorm, PVRTexLibPixelFormat.ASTC_10x6, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x6Srgb, PVRTexLibPixelFormat.ASTC_10x6, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x6Float, PVRTexLibPixelFormat.ASTC_10x6, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x8UNorm, PVRTexLibPixelFormat.ASTC_10x8, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x8Srgb, PVRTexLibPixelFormat.ASTC_10x8, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x8Float, PVRTexLibPixelFormat.ASTC_10x8, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x10UNorm, PVRTexLibPixelFormat.ASTC_10x10, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x10Srgb, PVRTexLibPixelFormat.ASTC_10x10, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc10x10Float, PVRTexLibPixelFormat.ASTC_10x10, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc12x10UNorm, PVRTexLibPixelFormat.ASTC_12x10, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc12x10Srgb, PVRTexLibPixelFormat.ASTC_12x10, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc12x10Float, PVRTexLibPixelFormat.ASTC_12x10, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc12x12UNorm, PVRTexLibPixelFormat.ASTC_12x12, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc12x12Srgb, PVRTexLibPixelFormat.ASTC_12x12, SourceKind.Rgba8, PVRTexLibVariableType.UnsignedByteNorm, CompressorFamily.Astc),
        new(TextureFormats.RgbaAstc12x12Float, PVRTexLibPixelFormat.ASTC_12x12, SourceKind.Rgba32Float, PVRTexLibVariableType.SignedFloat, CompressorFamily.Astc)
    ];

    private static readonly TextureFormat[] SSupportedFormats = SMappings.Select(static mapping => mapping.Format).ToArray();

    private readonly FormatMapping _mapping;
    private readonly PVRTexLibCompressorOptions _options;
    private readonly RgtcLatcTextureCoder? _bc45;

    public PVRTexLibTextureCoder(TextureFormat format, PVRTexLibCompressorOptions? options = null)
    {
        if (!TryGetMapping(format, out _mapping))
        {
            throw new NotSupportedException($"PVRTexLib does not have a mapped coder for texture format '{format.Name}'.");
        }

        Format = format;
        if (_mapping.PixelFormat is PVRTexLibPixelFormat.BC4 or PVRTexLibPixelFormat.BC5)
            _bc45 = new RgtcLatcTextureCoder(format);
        _options = options ?? new PVRTexLibCompressorOptions();
    }

    public TextureFormat Format { get; }

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format) => TryGetMapping(format, out _);

    public int GetDefaultPitch(int width) => Format.GetRowByteCount(width);

    public int GetEncodedByteCount(int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = GetDefaultPitch(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        return checked(rowPitch * GetBlockRowCount(width, height));
    }

    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The native BC4/5 decoder quantizes interpolants to bytes and treats
        // signed endpoints as unsigned when selecting the interpolation mode.
        if (_bc45 is not null)
        {
            _bc45.Decode(source, destination, rowPitch);
            return;
        }
        var packed = CopyToPackedRows(source, destination.Width, destination.Height, rowPitch);
        fixed (byte* sourcePtr = packed)
        {
            using var header = CreateCompressedHeader(destination.Width, destination.Height);
            using var texture = new PVRTexture(header, sourcePtr);
            Transcode(texture, GetIntermediatePixelFormat(_mapping.SourceKind), _mapping.ChannelType, PVRTexLibColourSpace.Linear);
            CopyTextureToBitmap(texture, destination);
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        EncodeCore(source, destination, rowPitch);
    }

    private void EncodeCore<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var pixelCount = checked(source.Width * source.Height);
        switch (_mapping.SourceKind)
        {
            case SourceKind.Rgba8:
                var rgba8 = new Rgba8UNorm[pixelCount];
                for (var i = 0; i < rgba8.Length; i++)
                {
                    var pixel = TPixel.ToRgba8UNorm(source.Pixels[i]);
                    if (_mapping.Opaque) pixel.Alpha = 255;
                    rgba8[i] = _mapping.Premultiplied ? RgbaColorConversions.PremultiplyAlpha(pixel) : pixel;
                }

                fixed (Rgba8UNorm* ptr = rgba8)
                {
                    if (_options.Etc2InputIsPremultiplied && _mapping.PixelFormat == PVRTexLibPixelFormat.ETC2_RGBA
                        && Array.Exists(rgba8, static pixel => pixel.Alpha != 255))
                    {
                        EncodePremultipliedEtc2(ptr, source.Width, source.Height, destination, rowPitch);
                        return;
                    }
                    TranscodeAndCopy(SRgba8PixelFormat, source.Width, source.Height, PVRTexLibVariableType.UnsignedByteNorm, ptr, destination, rowPitch);
                    return;
                }

            case SourceKind.SignedBc45:
                _bc45!.Encode(source, destination, rowPitch);
                return;

            case SourceKind.Rgba16UNorm:
                var rgba16 = new Rgba16UNorm[pixelCount];
                for (var i = 0; i < rgba16.Length; i++)
                {
                    rgba16[i] = TPixel.ToRgba16UNorm(source.Pixels[i]);
                }

                fixed (Rgba16UNorm* ptr = rgba16)
                {
                    TranscodeAndCopy(SRgba16PixelFormat, source.Width, source.Height, PVRTexLibVariableType.UnsignedShortNorm, ptr, destination, rowPitch);
                    return;
                }

            case SourceKind.Rgba16SNorm:
                var rgba16SNorm = new Rgba16SNorm[pixelCount];
                for (var i = 0; i < rgba16SNorm.Length; i++)
                {
                    rgba16SNorm[i] = TPixel.ToRgba16SNorm(source.Pixels[i]);
                }

                fixed (Rgba16SNorm* ptr = rgba16SNorm)
                {
                    TranscodeAndCopy(SRgba16PixelFormat, source.Width, source.Height, PVRTexLibVariableType.SignedShortNorm, ptr, destination, rowPitch);
                    return;
                }

            case SourceKind.Rgba32Float:
                var rgba32Float = new Rgba32Float[pixelCount];
                for (var i = 0; i < rgba32Float.Length; i++)
                {
                    rgba32Float[i] = TPixel.ToRgba32Float(source.Pixels[i]);
                }

                fixed (Rgba32Float* ptr = rgba32Float)
                {
                    TranscodeAndCopy(SRgba32PixelFormat, source.Width, source.Height, _mapping.ChannelType, ptr, destination, rowPitch);
                    return;
                }

            default:
                throw new InvalidOperationException($"Unsupported PVRTexLib source kind '{_mapping.SourceKind}'.");
        }
    }

    private void CopyTextureToBitmap<TPixel>(PVRTexture texture, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var pixelCount = checked(destination.Width * destination.Height);
        var data = texture.GetTextureDataConstPointer(0, arrayMember: 0, faceNumber: 0, ZSlice: 0);
        if (data is null)
        {
            throw new InvalidOperationException("PVRTexLib returned an empty texture payload.");
        }

        switch (_mapping.SourceKind)
        {
            case SourceKind.Rgba8:
                var pixels = new ReadOnlySpan<Rgba8UNorm>(data, pixelCount);
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    if (_mapping.Opaque) pixel.Alpha = 255;
                    if (_mapping.Premultiplied) pixel = RgbaColorConversions.UnpremultiplyAlpha(pixel);
                    destination.Pixels[i] = TPixel.FromRgba8UNorm(pixel);
                }
                return;
            case SourceKind.Rgba16UNorm:
                CopyPixels(new ReadOnlySpan<Rgba16UNorm>(data, pixelCount), destination, static value => TPixel.FromRgba16UNorm(value));
                return;
            case SourceKind.Rgba16SNorm:
                CopyPixels(new ReadOnlySpan<Rgba16SNorm>(data, pixelCount), destination, static value => TPixel.FromRgba16SNorm(value));
                return;
            case SourceKind.Rgba32Float:
                CopyPixels(new ReadOnlySpan<Rgba32Float>(data, pixelCount), destination, static value => TPixel.FromRgba32Float(value));
                return;
            default:
                throw new InvalidOperationException($"Unsupported PVRTexLib source kind '{_mapping.SourceKind}'.");
        }
    }

    private void Transcode(PVRTexture texture, ulong pixelFormat, PVRTexLibVariableType channelType, PVRTexLibColourSpace colourSpace)
    {
        if (!texture.Transcode(pixelFormat, channelType, colourSpace, GetQuality(), _options.Dither, _options.MaxRange, checked((uint)_options.MaxThreads)))
        {
            throw new InvalidOperationException($"PVRTexLib failed to transcode '{Format.Name}'.");
        }
    }

    private PVRTexLibCompressorQuality GetQuality() => _mapping.Family switch
    {
        CompressorFamily.Etc => _options.EtcQuality,
        CompressorFamily.Pvrtc => _options.PvrtcQuality,
        CompressorFamily.Astc => _options.AstcQuality,
        CompressorFamily.Basis => _options.BasisQuality,
        _ => PVRTexLibCompressorQuality.PVRTCFastest
    };

    private PVRTextureHeader CreateCompressedHeader(int width, int height) =>
        new(
            (ulong)_mapping.PixelFormat,
            checked((uint)width),
            checked((uint)height),
            depth: 1,
            numMipMaps: 1,
            numArrayMembers: 1,
            numFaces: 1,
            GetColourSpace(Format),
            _mapping.ChannelType,
            preMultiplied: false);

    private void EncodePremultipliedEtc2(Rgba8UNorm* data, int width, int height,
        Span<byte> destination, int rowPitch)
    {
        // PVRTexLib 5.6 can encode transparent black with nonzero RGB in ETC2_RGBA,
        // even when the input header marks it as premultiplied. Premultiplied blending
        // adds that RGB directly to the background, producing a bright fringe.
        // ETC2_RGB fits the caller's already-premultiplied colors independently of alpha.
        using var header = new PVRTextureHeader(SRgba8PixelFormat, (uint)width, (uint)height, 1, 1, 1, 1,
            PVRTexLibColourSpace.Linear, PVRTexLibVariableType.UnsignedByteNorm, preMultiplied: true);
        using var color = new PVRTexture(header, data);
        var space = GetColourSpace(Format);
        Transcode(color, (ulong)PVRTexLibPixelFormat.ETC2_RGB, _mapping.ChannelType, space);
        var colorBytes = GetPayload(color, Format.GetByteCount(width, height) / 2);

        // Reuse the encoder's temporary pixel buffer for the alpha pass. Only the EAC
        // alpha bytes from this pass are used, so its RGB input can be constant black.
        var pixels = new Span<Rgba8UNorm>(data, checked(width * height));
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Rgba8UNorm(0, 0, 0, pixels[i].Alpha);
        }
        using var alpha = new PVRTexture(header, data);
        Transcode(alpha, (ulong)PVRTexLibPixelFormat.ETC2_RGBA, _mapping.ChannelType, space);
        var alphaBytes = GetPayload(alpha, Format.GetByteCount(width, height));
        // Each 4x4 ETC2_RGBA block stores 8 bytes of EAC alpha followed by 8 bytes of ETC2 RGB.
        int rowBytes = GetDefaultPitch(width);
        for (int row = 0; row < alphaBytes.Length / rowBytes; row++)
        {
            var output = destination.Slice(row * rowPitch, rowBytes);
            for (int x = 0; x < rowBytes; x += 16)
            {
                int offset = row * rowBytes + x;
                alphaBytes.Slice(offset, 8).CopyTo(output[x..]);
                colorBytes.Slice(offset / 2, 8).CopyTo(output[(x + 8)..]);
            }
        }
    }

    private void TranscodeAndCopy(
        ulong pixelFormat,
        int width,
        int height,
        PVRTexLibVariableType channelType,
        void* data,
        Span<byte> destination,
        int rowPitch)
    {
        using var header = new PVRTextureHeader(
            pixelFormat,
            checked((uint)width),
            checked((uint)height),
            depth: 1,
            numMipMaps: 1,
            numArrayMembers: 1,
            numFaces: 1,
            PVRTexLibColourSpace.Linear,
            channelType,
            preMultiplied: false);
        using var texture = new PVRTexture(header, data);
        Transcode(texture, (ulong)_mapping.PixelFormat, _mapping.ChannelType, GetColourSpace(Format));

        CopyPackedRowsToDestination(GetPayload(texture, Format.GetByteCount(width, height)), width, height, destination, rowPitch);
    }

    private static ReadOnlySpan<byte> GetPayload(PVRTexture texture, int packedSize)
    {
        var textureData = texture.GetTextureDataConstPointer(0, arrayMember: 0, faceNumber: 0, ZSlice: 0);
        if (textureData is null || texture.GetTextureDataSize(0, allSurfaces: false, allFaces: false) < (ulong)packedSize)
        {
            throw new InvalidOperationException("PVRTexLib returned an empty or incomplete texture payload.");
        }

        return new ReadOnlySpan<byte>(textureData, packedSize);
    }

    private byte[] CopyToPackedRows(ReadOnlySpan<byte> source, int width, int height, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = Format.GetRowByteCount(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        var blockRows = GetBlockRowCount(width, height);
        var required = checked(rowPitch * blockRows);
        if (source.Length < required)
        {
            throw new ArgumentException("Source span is too small for the texture dimensions and row pitch.", nameof(source));
        }

        var packedSize = checked(rowByteCount * blockRows);
        var packed = new byte[packedSize];

        if (rowPitch == rowByteCount)
        {
            source[..packedSize].CopyTo(packed);
            return packed;
        }

        for (var row = 0; row < blockRows; row++)
        {
            source.Slice(checked(row * rowPitch), rowByteCount).CopyTo(packed.AsSpan(checked(row * rowByteCount)));
        }

        return packed;
    }

    private void CopyPackedRowsToDestination(ReadOnlySpan<byte> packed, int width, int height, Span<byte> destination, int rowPitch)
    {
        var rowByteCount = Format.GetRowByteCount(width);
        var blockRows = GetBlockRowCount(width, height);
        if (rowPitch == rowByteCount)
        {
            packed.CopyTo(destination);
            return;
        }

        for (var row = 0; row < blockRows; row++)
        {
            packed.Slice(checked(row * rowByteCount), rowByteCount).CopyTo(destination.Slice(checked(row * rowPitch), rowByteCount));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var rowByteCount = Format.GetRowByteCount(width);
        if (rowPitch < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed block-row byte count.");
        }

        var required = checked(rowPitch * GetBlockRowCount(width, height));
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination span is too small for the texture dimensions and row pitch.", nameof(destination));
        }
    }

    private int GetBlockRowCount(int width, int height) =>
        checked(Format.GetByteCount(width, height) / Format.GetRowByteCount(width));

    private static void CopyPixels<TSource, TPixel>(ReadOnlySpan<TSource> source, BitmapView<TPixel> destination, Func<TSource, TPixel> convert)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination.Pixels[i] = convert(source[i]);
        }
    }

    private static bool TryGetMapping(TextureFormat format, out FormatMapping mapping)
    {
        foreach (var candidate in SMappings)
        {
            if (candidate.Format == format)
            {
                mapping = candidate;
                return true;
            }
        }

        mapping = default;
        return false;
    }

    private static PVRTexLibColourSpace GetColourSpace(TextureFormat format) =>
        format.ValueKind == TextureValueKind.Srgb
            ? PVRTexLibColourSpace.sRGB
            : PVRTexLibColourSpace.Linear;

    private static ulong GetIntermediatePixelFormat(SourceKind sourceKind) => sourceKind switch
    {
        SourceKind.Rgba8 => SRgba8PixelFormat,
        SourceKind.Rgba16UNorm or SourceKind.Rgba16SNorm => SRgba16PixelFormat,
        SourceKind.Rgba32Float => SRgba32PixelFormat,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null)
    };

    private readonly record struct FormatMapping(
        TextureFormat Format,
        PVRTexLibPixelFormat PixelFormat,
        SourceKind SourceKind,
        PVRTexLibVariableType ChannelType,
        CompressorFamily Family,
        bool Opaque = false,
        bool Premultiplied = false);

    private enum SourceKind
    {
        Rgba8,
        SignedBc45,
        Rgba16UNorm,
        Rgba16SNorm,
        Rgba32Float
    }

    private enum CompressorFamily
    {
        Other,
        Etc,
        Pvrtc,
        Astc,
        Basis
    }
}
