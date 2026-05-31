using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Bitmaps;
using TextureCompressor.Options;
using TextureCompressor.Utilities;

namespace TextureCompressor.Codecs;

public sealed class PvrtcTextureCoder : ITextureCoder
{
    private const int WordHeight = 4;
    private const int ModulationGridWidth = 16;
    private const int ModulationGridHeight = 8;

    private static readonly TextureFormat[] SSupportedFormats =
    [
        TextureFormats.RgbPvrtcI2BppUNorm,
        TextureFormats.RgbPvrtcI2BppSrgb,
        TextureFormats.RgbaPvrtcI2BppUNorm,
        TextureFormats.RgbaPvrtcI2BppSrgb,
        TextureFormats.RgbPvrtcI4BppUNorm,
        TextureFormats.RgbPvrtcI4BppSrgb,
        TextureFormats.RgbaPvrtcI4BppUNorm,
        TextureFormats.RgbaPvrtcI4BppSrgb,
        TextureFormats.RgbaPvrtcII2BppUNorm,
        TextureFormats.RgbaPvrtcII2BppSrgb,
        TextureFormats.RgbaPvrtcII4BppUNorm,
        TextureFormats.RgbaPvrtcII4BppSrgb,
        TextureFormats.RgbPvrtcI6BppFloat,
        TextureFormats.RgbPvrtcI8BppFloat,
        TextureFormats.RgbPvrtcII6BppFloat,
        TextureFormats.RgbPvrtcII8BppFloat
    ];

    private static readonly int[] SRepVals0 = [0, 3, 5, 8];
    private const float HdrMinLuma = 1e-6f;

    private readonly PvrtcCoderOptions _options;

    public PvrtcTextureCoder(TextureFormat format, PvrtcCoderOptions? options = null)
    {
        if (!IsSupported(format))
        {
            throw CreateUnsupportedFormatException(format);
        }

        Format = format;
        _options = options ?? new PvrtcCoderOptions();
    }

    public TextureFormat Format { get; }

    public PvrtcCoderOptions Options => _options;

    public static ReadOnlySpan<TextureFormat> SupportedFormats => SSupportedFormats;

    public static bool IsSupported(TextureFormat format)
    {
        foreach (var supportedFormat in SSupportedFormats)
        {
            if (format == supportedFormat)
            {
                return true;
            }
        }

        return false;
    }

    public int GetEncodedByteCount(int width, int height) =>
        GetEncodedByteCount(Format, width, height);

    public static int GetEncodedByteCount(TextureFormat format, int width, int height)
    {
        var info = GetFormatInfo(format);
        ValidateDimensions(info, width, height);
        return GetEncodedByteCount(info, width, height);
    }

    /// <summary>
    /// Decodes a PVRTC payload into <typeparamref name="TPixel"/> pixels. sRGB PVRTC formats are converted
    /// from sRGB storage bytes to the destination pixel type's normal color space.
    /// </summary>
    public void Decode<TPixel>(ReadOnlySpan<byte> source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var info = GetFormatInfo(Format);
        ValidateDimensions(info, destination.Width, destination.Height);
        ValidateSourceLength(Format, destination.Width, destination.Height, source);

        if (info.IsHdr)
        {
            if (typeof(TPixel) == typeof(Rgba32Float))
            {
                DecodeLinear(
                    Format,
                    destination.Width,
                    destination.Height,
                    source,
                    MemoryMarshal.Cast<TPixel, Rgba32Float>(destination.Pixels));
                return;
            }

            var texelCount = checked(destination.Width * destination.Height);
            var linear = ArrayPool<Rgba32Float>.Shared.Rent(texelCount);
            try
            {
                var linearSpan = linear.AsSpan(0, texelCount);
                DecodeLinear(Format, destination.Width, destination.Height, source, linearSpan);
                CopyFromLinear(linearSpan, destination);
                return;
            }
            finally
            {
                ArrayPool<Rgba32Float>.Shared.Return(linear);
            }
        }

        if (typeof(TPixel) == typeof(Rgba8UNorm))
        {
            var rgbaDestination = MemoryMarshal.Cast<TPixel, Rgba8UNorm>(destination.Pixels);
            DecodeRgba8(Format, destination.Width, destination.Height, source, rgbaDestination);
            if (IsSrgb(Format))
            {
                DecodeSrgbColors(rgbaDestination);
            }

            return;
        }

        var colorCount = checked(destination.Width * destination.Height);
        var colors = ArrayPool<Rgba8UNorm>.Shared.Rent(colorCount);
        try
        {
            var colorSpan = colors.AsSpan(0, colorCount);
            DecodeToRgba8Storage(info, destination.Width, destination.Height, source, colorSpan);
            var decodeSrgb = IsSrgb(Format);
            var pixels = destination.Pixels;
            for (var i = 0; i < colorSpan.Length; i++)
            {
                pixels[i] = TPixel.FromRgba8UNorm(DecodeStorageColor(colorSpan[i], decodeSrgb));
            }
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(colors);
        }
    }

    /// <summary>
    /// Encodes <typeparamref name="TPixel"/> pixels into PVRTC. sRGB PVRTC formats convert normalized source
    /// colors to sRGB storage bytes before compression.
    /// </summary>
    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var info = GetFormatInfo(Format);
        ValidateDimensions(info, source.Width, source.Height);
        ValidateDestinationLength(Format, source.Width, source.Height, destination);

        if (info.IsHdr)
        {
            if (typeof(TPixel) == typeof(Rgba32Float))
            {
                EncodeLinear(
                    MemoryMarshal.Cast<TPixel, Rgba32Float>(source.Pixels),
                    source.Width,
                    source.Height,
                    Format,
                    destination,
                    _options);
                return;
            }

            var texelCount = checked(source.Width * source.Height);
            var linear = ArrayPool<Rgba32Float>.Shared.Rent(texelCount);
            try
            {
                var linearSpan = linear.AsSpan(0, texelCount);
                CopyToLinear(source, linearSpan);
                EncodeLinear(linearSpan, source.Width, source.Height, Format, destination, _options);
                return;
            }
            finally
            {
                ArrayPool<Rgba32Float>.Shared.Return(linear);
            }
        }

        if (typeof(TPixel) == typeof(Rgba8UNorm))
        {
            var rgbaSource = MemoryMarshal.Cast<TPixel, Rgba8UNorm>(source.Pixels);
            EncodeStorageOrNormalizedRgba8(
                rgbaSource,
                source.Width,
                source.Height,
                Format,
                destination,
                IsSrgb(Format),
                _options);
            return;
        }

        var colorCount = checked(source.Width * source.Height);
        var colors = ArrayPool<Rgba8UNorm>.Shared.Rent(colorCount);
        try
        {
            var colorSpan = colors.AsSpan(0, colorCount);
            var encodeSrgb = IsSrgb(Format);
            var hasAlpha = info.HasAlpha;
            var pixels = source.Pixels;
            for (var i = 0; i < colorSpan.Length; i++)
            {
                colorSpan[i] = EncodeStorageColor(TPixel.ToRgba8UNorm(pixels[i]), encodeSrgb, hasAlpha);
            }

            EncodeFromRgba8Storage(info, source.Width, source.Height, colorSpan, destination, _options);
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(colors);
        }
    }

    public static byte[] EncodeRgba8(
        ReadOnlySpan<Rgba8UNorm> source,
        int width,
        int height,
        TextureFormat format,
        PvrtcCoderOptions? options = null)
    {
        var result = new byte[GetEncodedByteCount(format, width, height)];
        EncodeRgba8(source, width, height, format, result, options);
        return result;
    }

    public static byte[] EncodeLinear(
        ReadOnlySpan<Rgba32Float> source,
        int width,
        int height,
        TextureFormat format,
        PvrtcCoderOptions? options = null)
    {
        var result = new byte[GetEncodedByteCount(format, width, height)];
        EncodeLinear(source, width, height, format, result, options);
        return result;
    }

    /// <summary>
    /// Decodes into RGBA8 storage bytes. For sRGB PVRTC formats, RGB remains sRGB encoded; use
    /// <see cref="Decode{TPixel}"/> or <see cref="DecodeLinear"/> when color-space conversion is desired.
    /// </summary>
    public static void DecodeRgba8(
        TextureFormat format,
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<Rgba8UNorm> destination)
    {
        var info = GetFormatInfo(format);
        ValidateDimensions(info, width, height);
        ValidateSourceLength(format, width, height, source);
        ValidateTexelSpan(width, height, destination.Length, nameof(destination));

        if (info.IsHdr)
        {
            var texelCount = checked(width * height);
            var linear = ArrayPool<Rgba32Float>.Shared.Rent(texelCount);
            try
            {
                var linearSpan = linear.AsSpan(0, texelCount);
                DecodeLinear(format, width, height, source, linearSpan);
                for (var i = 0; i < linearSpan.Length; i++)
                {
                    destination[i] = Rgba8UNorm.FromRgba32Float(linearSpan[i]);
                }

                return;
            }
            finally
            {
                ArrayPool<Rgba32Float>.Shared.Return(linear);
            }
        }

        var colorCount = checked(width * height);
        DecodeToRgba8Storage(info, width, height, source, destination[..colorCount]);
    }

    /// <summary>
    /// Decodes into linear RGBA float pixels. sRGB PVRTC formats are converted from sRGB storage bytes.
    /// </summary>
    public static void DecodeLinear(
        TextureFormat format,
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<Rgba32Float> destination)
    {
        var info = GetFormatInfo(format);
        ValidateDimensions(info, width, height);
        ValidateSourceLength(format, width, height, source);
        ValidateTexelSpan(width, height, destination.Length, nameof(destination));

        if (info.IsHdr)
        {
            DecodeHdr(info, width, height, source, destination);
            return;
        }

        var colorCount = checked(width * height);
        var colors = ArrayPool<Rgba8UNorm>.Shared.Rent(colorCount);
        try
        {
            var colorSpan = colors.AsSpan(0, colorCount);
            DecodeToRgba8Storage(info, width, height, source, colorSpan);
            var srgb = IsSrgb(format);
            for (var i = 0; i < colorSpan.Length; i++)
            {
                destination[i] = StorageRgba8ToLinear(colorSpan[i], srgb);
            }
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(colors);
        }
    }

    /// <summary>
    /// Encodes RGBA8 storage bytes. For sRGB PVRTC formats, RGB is expected to already be sRGB encoded; use
    /// <see cref="Encode{TPixel}"/> or <see cref="EncodeLinear"/> when color-space conversion is desired.
    /// </summary>
    public static void EncodeRgba8(
        ReadOnlySpan<Rgba8UNorm> source,
        int width,
        int height,
        TextureFormat format,
        Span<byte> destination,
        PvrtcCoderOptions? options = null) =>
        EncodeStorageOrNormalizedRgba8(source, width, height, format, destination, encodeSrgbColors: false, options);

    private static void EncodeStorageOrNormalizedRgba8(
        ReadOnlySpan<Rgba8UNorm> source,
        int width,
        int height,
        TextureFormat format,
        Span<byte> destination,
        bool encodeSrgbColors,
        PvrtcCoderOptions? options = null,
        bool scoreEndpointCandidates = false)
    {
        options ??= new PvrtcCoderOptions();
        var info = GetFormatInfo(format);
        ValidateDimensions(info, width, height);
        ValidateTexelSpan(width, height, source.Length, nameof(source));
        ValidateDestinationLength(format, width, height, destination);

        if (info.IsHdr)
        {
            var texelCount = checked(width * height);
            var linear = ArrayPool<Rgba32Float>.Shared.Rent(texelCount);
            try
            {
                var linearSpan = linear.AsSpan(0, texelCount);
                for (var i = 0; i < linearSpan.Length; i++)
                {
                    linearSpan[i] = Rgba8UNorm.ToRgba32Float(source[i]);
                }

                EncodeHdr(info, width, height, linearSpan, destination, options);
                return;
            }
            finally
            {
                ArrayPool<Rgba32Float>.Shared.Return(linear);
            }
        }

        var colorCount = checked(width * height);
        if (!encodeSrgbColors)
        {
            EncodeFromRgba8Storage(info, width, height, source[..colorCount], destination, options, scoreEndpointCandidates);
            return;
        }

        var colors = ArrayPool<Rgba8UNorm>.Shared.Rent(colorCount);
        try
        {
            var colorSpan = colors.AsSpan(0, colorCount);
            CopyToStorageRgba8(source, colorSpan, encodeSrgb: true, hasAlpha: info.HasAlpha);
            EncodeFromRgba8Storage(info, width, height, colorSpan, destination, options, scoreEndpointCandidates);
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(colors);
        }
    }

    /// <summary>
    /// Encodes linear RGBA float pixels. sRGB PVRTC formats are converted to sRGB storage bytes before compression.
    /// </summary>
    public static void EncodeLinear(
        ReadOnlySpan<Rgba32Float> source,
        int width,
        int height,
        TextureFormat format,
        Span<byte> destination,
        PvrtcCoderOptions? options = null)
    {
        options ??= new PvrtcCoderOptions();
        var info = GetFormatInfo(format);
        ValidateDimensions(info, width, height);
        ValidateTexelSpan(width, height, source.Length, nameof(source));
        ValidateDestinationLength(format, width, height, destination);
        var texelCount = checked(width * height);

        if (info.IsHdr)
        {
            EncodeHdr(info, width, height, source, destination, options);
            return;
        }

        var colors = ArrayPool<Rgba8UNorm>.Shared.Rent(texelCount);
        try
        {
            var colorSpan = colors.AsSpan(0, texelCount);
            var srgb = IsSrgb(format);
            for (var i = 0; i < colorSpan.Length; i++)
            {
                ValidateUNormInput(source[i].Red, format, nameof(Rgba32Float.Red));
                ValidateUNormInput(source[i].Green, format, nameof(Rgba32Float.Green));
                ValidateUNormInput(source[i].Blue, format, nameof(Rgba32Float.Blue));
                if (info.HasAlpha)
                {
                    ValidateUNormInput(source[i].Alpha, format, nameof(Rgba32Float.Alpha));
                }

                colorSpan[i] = LinearToStorageRgba8(info.HasAlpha ? source[i] : WithAlpha(source[i], 1f), srgb);
            }

            EncodeFromRgba8Storage(info, width, height, colorSpan, destination, options);
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(colors);
        }
    }

    private static void CopyFromLinear<TPixel>(ReadOnlySpan<Rgba32Float> source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var pixelIndex = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var row = destination.GetRowSpan(y);
            for (var x = 0; x < destination.Width; x++)
            {
                row[x] = TPixel.FromRgba32Float(source[pixelIndex++]);
            }
        }
    }

    private static void CopyToStorageRgba8(
        ReadOnlySpan<Rgba8UNorm> source,
        Span<Rgba8UNorm> destination,
        bool encodeSrgb,
        bool hasAlpha)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = EncodeStorageColor(source[i], encodeSrgb, hasAlpha);
        }
    }

    private static Rgba8UNorm DecodeStorageColor(Rgba8UNorm color, bool decodeSrgb) => decodeSrgb
        ? new Rgba8UNorm(DecodeSrgb(color.Red), DecodeSrgb(color.Green), DecodeSrgb(color.Blue), color.Alpha)
        : color;

    private static Rgba8UNorm EncodeStorageColor(Rgba8UNorm color, bool encodeSrgb, bool hasAlpha) => encodeSrgb
        ? new Rgba8UNorm(
            EncodeSrgb(color.Red),
            EncodeSrgb(color.Green),
            EncodeSrgb(color.Blue),
            hasAlpha ? color.Alpha : byte.MaxValue)
        : hasAlpha || color.Alpha == byte.MaxValue
            ? color
            : new Rgba8UNorm(color.Red, color.Green, color.Blue, byte.MaxValue);

    private static void CopyToLinear<TPixel>(BitmapView<TPixel> source, Span<Rgba32Float> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var pixelIndex = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var row = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                destination[pixelIndex++] = TPixel.ToRgba32Float(row[x]);
            }
        }
    }

    private static void DecodeHdr(
        PvrtcFormatInfo info,
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<Rgba32Float> destination)
    {
        var lumaByteCount = GetPvrtcByteCount(info, width, height, 4);
        var chromaByteCount = GetPvrtcByteCount(info, width, height, info.ChromaBitsPerPixel);
        var texelCount = checked(width * height);
        var luma = ArrayPool<Rgba8UNorm>.Shared.Rent(texelCount);
        var chroma = ArrayPool<Rgba8UNorm>.Shared.Rent(texelCount);
        try
        {
            var lumaSpan = luma.AsSpan(0, texelCount);
            var chromaSpan = chroma.AsSpan(0, texelCount);
            DecodeRgba8(GetLumaFormat(info), width, height, source[..lumaByteCount], lumaSpan);
            DecodeRgba8(
                GetChromaFormat(info),
                width,
                height,
                source.Slice(lumaByteCount, chromaByteCount),
                chromaSpan);

            for (var i = 0; i < destination.Length; i++)
            {
                var chromaRed = ByteToUnit(chromaSpan[i].Red);
                var chromaGreen = ByteToUnit(chromaSpan[i].Green);
                var chromaBlue = ByteToUnit(chromaSpan[i].Blue);
                if (chromaRed <= 0f && chromaGreen <= 0f && chromaBlue <= 0f)
                {
                    destination[i] = new Rgba32Float(0f, 0f, 0f, 1f);
                    continue;
                }

                var decodedLuma = DecodeHdrLumaBytes(lumaSpan[i]);
                destination[i] = new Rgba32Float(
                    decodedLuma * 4f * chromaRed,
                    decodedLuma * 2f * chromaGreen,
                    decodedLuma * 4f * chromaBlue,
                    1f);
            }
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(luma);
            ArrayPool<Rgba8UNorm>.Shared.Return(chroma);
        }
    }

    // PVRTC HDR stores log2((R + 2G + B) / 4) across two luma channels and
    // derives chroma from the decoded luma stream so both payloads agree after
    // PVRTC interpolation.
    private static void EncodeHdr(
        PvrtcFormatInfo info,
        int width,
        int height,
        ReadOnlySpan<Rgba32Float> source,
        Span<byte> destination,
        PvrtcCoderOptions options)
    {
        var lumaByteCount = GetPvrtcByteCount(info, width, height, 4);
        var chromaByteCount = GetPvrtcByteCount(info, width, height, info.ChromaBitsPerPixel);
        var texelCount = checked(width * height);
        var scoreEndpointCandidates = UsesEndpointCandidateScoring(options);
        var lumaPixels = ArrayPool<Rgba8UNorm>.Shared.Rent(texelCount);
        var chromaPixels = ArrayPool<Rgba8UNorm>.Shared.Rent(texelCount);
        var decodedLumaPixels = ArrayPool<Rgba8UNorm>.Shared.Rent(texelCount);
        try
        {
            var lumaSpan = lumaPixels.AsSpan(0, texelCount);
            var chromaSpan = chromaPixels.AsSpan(0, texelCount);
            var decodedLumaSpan = decodedLumaPixels.AsSpan(0, texelCount);
            var lumaFormat = GetLumaFormat(info);
            var chromaFormat = GetChromaFormat(info);
            var lumaPayload = destination[..lumaByteCount];
            var chromaPayload = destination.Slice(lumaByteCount, chromaByteCount);

            ValidateHdrSource(source);
            BuildHdrLumaPlane(source, lumaSpan);
            EncodeStorageOrNormalizedRgba8(
                lumaSpan,
                width,
                height,
                lumaFormat,
                lumaPayload,
                encodeSrgbColors: false,
                options,
                scoreEndpointCandidates);
            DecodeRgba8(lumaFormat, width, height, lumaPayload, decodedLumaSpan);
            BuildHdrChromaPlane(source, decodedLumaSpan, chromaSpan);
            EncodeStorageOrNormalizedRgba8(
                chromaSpan,
                width,
                height,
                chromaFormat,
                chromaPayload,
                encodeSrgbColors: false,
                options,
                scoreEndpointCandidates);
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(lumaPixels);
            ArrayPool<Rgba8UNorm>.Shared.Return(chromaPixels);
            ArrayPool<Rgba8UNorm>.Shared.Return(decodedLumaPixels);
        }
    }

    private static bool UsesEndpointCandidateScoring(PvrtcCoderOptions options) =>
        options.CompressionMode switch
        {
            TextureCompressionLevel.Fast or TextureCompressionLevel.Normal => false,
            TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(PvrtcCoderOptions.CompressionMode),
                options.CompressionMode,
                "Unsupported PVRTC compression mode.")
        };

    private static bool UsesHardTransitionSearch(PvrtcCoderOptions options) =>
        options.CompressionMode switch
        {
            TextureCompressionLevel.Fast or TextureCompressionLevel.Normal => false,
            TextureCompressionLevel.High or TextureCompressionLevel.Exhaustive => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(PvrtcCoderOptions.CompressionMode),
                options.CompressionMode,
                "Unsupported PVRTC compression mode.")
        };

    private static bool UsesModulationModeSearch(PvrtcCoderOptions options) =>
        options.CompressionMode switch
        {
            TextureCompressionLevel.Fast or TextureCompressionLevel.Normal or TextureCompressionLevel.High => false,
            TextureCompressionLevel.Exhaustive => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(PvrtcCoderOptions.CompressionMode),
                options.CompressionMode,
                "Unsupported PVRTC compression mode.")
        };

    private static bool UsesEndpointRefinement(PvrtcCoderOptions options) =>
        options.CompressionMode switch
        {
            TextureCompressionLevel.Fast or TextureCompressionLevel.Normal or TextureCompressionLevel.High => false,
            TextureCompressionLevel.Exhaustive => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(PvrtcCoderOptions.CompressionMode),
                options.CompressionMode,
                "Unsupported PVRTC compression mode.")
        };

    private static void DecodeToRgba8Storage(
        PvrtcFormatInfo info,
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<Rgba8UNorm> destination)
    {
        var storageExtent = GetStorageExtent(info, width, height, info.BitsPerPixel);
        var wordCount = GetWordCount(info, storageExtent.Width, storageExtent.Height);
        var words = ArrayPool<PvrtcWord>.Shared.Rent(wordCount);

        var wordWidth = GetWordWidth(info.BitsPerPixel);
        var numXWords = storageExtent.Width / wordWidth;
        var numYWords = storageExtent.Height / WordHeight;

        try
        {
            var wordSpan = words.AsSpan(0, wordCount);
            ReadWords(info, storageExtent.Width, storageExtent.Height, source, wordSpan);

            for (var wordY = -1; wordY < numYWords - 1; wordY++)
            {
                for (var wordX = -1; wordX < numXWords - 1; wordX++)
                {
                    var indices = new WordIndices(
                        new WordCoordinate(WrapWordIndex(numXWords, wordX), WrapWordIndex(numYWords, wordY)),
                        new WordCoordinate(WrapWordIndex(numXWords, wordX + 1), WrapWordIndex(numYWords, wordY)),
                        new WordCoordinate(WrapWordIndex(numXWords, wordX), WrapWordIndex(numYWords, wordY + 1)),
                        new WordCoordinate(WrapWordIndex(numXWords, wordX + 1), WrapWordIndex(numYWords, wordY + 1)));

                    var p = wordSpan[GetStorageIndex(numXWords, numYWords, indices.P.X, indices.P.Y, info.VersionII)];
                    var q = wordSpan[GetStorageIndex(numXWords, numYWords, indices.Q.X, indices.Q.Y, info.VersionII)];
                    var r = wordSpan[GetStorageIndex(numXWords, numYWords, indices.R.X, indices.R.Y, info.VersionII)];
                    var s = wordSpan[GetStorageIndex(numXWords, numYWords, indices.S.X, indices.S.Y, info.VersionII)];

                    var pixels = DecompressWord(p, q, r, s, info.BitsPerPixel, info.VersionII);
                    MapDecompressedData(
                        destination,
                        width,
                        height,
                        ref pixels,
                        indices,
                        info.BitsPerPixel,
                        info.HasAlpha);
                }
            }
        }
        finally
        {
            ArrayPool<PvrtcWord>.Shared.Return(words);
        }
    }

    private static void ReadWords(PvrtcFormatInfo info, int width, int height, ReadOnlySpan<byte> source, Span<PvrtcWord> destination)
    {
        var wordCount = GetWordCount(info, width, height);
        for (var i = 0; i < wordCount; i++)
        {
            destination[i] = ReadWord(source, i);
        }
    }

    private static PvrtcWord ReadWord(ReadOnlySpan<byte> source, int wordIndex)
    {
        var offset = wordIndex * 8;
        return new PvrtcWord(
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset + 4, 4)));
    }

    private static ColorBlock DecompressWord(
        PvrtcWord p,
        PvrtcWord q,
        PvrtcWord r,
        PvrtcWord s,
        byte bitsPerPixel,
        bool versionII)
    {
        var wordWidth = GetWordWidth(bitsPerPixel);
        var halfWordWidth = wordWidth >> 1;
        const int halfWordHeight = WordHeight >> 1;
        var values = new IntGrid();
        var modes = new IntGrid();
        var upscaledColorA = new PixelBlock();
        var upscaledColorB = new PixelBlock();
        var pixels = new ColorBlock();

        UnpackModulations(p, 0, 0, ref values, ref modes, bitsPerPixel);
        UnpackModulations(q, wordWidth, 0, ref values, ref modes, bitsPerPixel);
        UnpackModulations(r, 0, WordHeight, ref values, ref modes, bitsPerPixel);
        UnpackModulations(s, wordWidth, WordHeight, ref values, ref modes, bitsPerPixel);

        var pa = GetColorA(p.ColorData, versionII);
        var pb = GetColorB(p.ColorData, versionII);
        var qa = GetColorA(q.ColorData, versionII);
        var qb = GetColorB(q.ColorData, versionII);
        var ra = GetColorA(r.ColorData, versionII);
        var rb = GetColorB(r.ColorData, versionII);
        var sa = GetColorA(s.ColorData, versionII);
        var sb = GetColorB(s.ColorData, versionII);

        if (versionII && (p.ColorData & 0x8000) != 0)
        {
            var pa8 = Color5554To8888(pa);
            var pb8 = Color5554To8888(pb);
            var qa8 = Color5554To8888(qa);
            var qb8 = Color5554To8888(qb);
            var ra8 = Color5554To8888(ra);
            var rb8 = Color5554To8888(rb);
            var sa8 = Color5554To8888(sa);
            var sb8 = Color5554To8888(sb);

            for (var y = 0; y < WordHeight; y++)
            {
                for (var x = 0; x < wordWidth; x++)
                {
                    var mod = GetModulationValue(ref values, ref modes, x + halfWordWidth, y + halfWordHeight, bitsPerPixel);
                    Pixel128 result;
                    if (bitsPerPixel != 2 && GetGrid(ref modes, x + halfWordWidth, y + halfWordHeight) == 1)
                    {
                        result = GetLocalPaletteColor(pa8, pb8, qa8, qb8, ra8, rb8, sa8, sb8, y, x, mod);
                    }
                    else
                    {
                        var localX = x;
                        var localY = y;
                        if (bitsPerPixel != 2)
                        {
                            localX = y;
                            localY = x;
                        }

                        var (a, b) = SelectHardTransitionEndpoints(
                            localX,
                            localY,
                            halfWordWidth,
                            halfWordHeight,
                            pa8,
                            pb8,
                            qa8,
                            qb8,
                            ra8,
                            rb8,
                            sa8,
                            sb8);
                        result = InterpolatePixel(a, b, mod);
                    }

                    pixels[WordPixelIndex(x, y, bitsPerPixel)] = result.ToColor();
                }
            }

            return pixels;
        }

        InterpolateColors(pa, qa, ra, sa, ref upscaledColorA, bitsPerPixel);
        InterpolateColors(pb, qb, rb, sb, ref upscaledColorB, bitsPerPixel);
        for (var y = 0; y < WordHeight; y++)
        {
            for (var x = 0; x < wordWidth; x++)
            {
                var mod = GetModulationValue(ref values, ref modes, x + halfWordWidth, y + halfWordHeight, bitsPerPixel);
                Pixel128 result;
                if (mod > 10)
                {
                    result = default;
                }
                else
                {
                    var index = y * wordWidth + x;
                    result = InterpolatePixel(upscaledColorA[index], upscaledColorB[index], mod);
                }

                pixels[WordPixelIndex(x, y, bitsPerPixel)] = result.ToColor();
            }
        }

        return pixels;
    }

    private static (Pixel128 A, Pixel128 B) SelectHardTransitionEndpoints(
        int x,
        int y,
        int halfWordWidth,
        int halfWordHeight,
        Pixel128 pa,
        Pixel128 pb,
        Pixel128 qa,
        Pixel128 qb,
        Pixel128 ra,
        Pixel128 rb,
        Pixel128 sa,
        Pixel128 sb)
    {
        if (x < halfWordWidth)
        {
            return y < halfWordHeight ? (pa, pb) : (ra, rb);
        }

        return y < halfWordHeight ? (qa, qb) : (sa, sb);
    }

    private static Pixel128 GetLocalPaletteColor(
        Pixel128 pa,
        Pixel128 pb,
        Pixel128 qa,
        Pixel128 qb,
        Pixel128 ra,
        Pixel128 rb,
        Pixel128 sa,
        Pixel128 sb,
        int x,
        int y,
        int modulation)
    {
        var paletteIndex = modulation switch
        {
            0 => 0,
            3 or 4 => 1,
            5 or > 10 => 2,
            8 => 3,
            _ => -1
        };

        if (paletteIndex < 0)
        {
            return default;
        }

        return GetLocalPaletteEndpoint(pa, pb, qa, qb, ra, rb, sa, sb, x, y, paletteIndex);
    }

    private static Pixel128 GetLocalPaletteEndpoint(
        Pixel128 pa,
        Pixel128 pb,
        Pixel128 qa,
        Pixel128 qb,
        Pixel128 ra,
        Pixel128 rb,
        Pixel128 sa,
        Pixel128 sb,
        int x,
        int y,
        int paletteIndex)
    {
        if (y == 0)
        {
            if (x == 0)
            {
                return paletteIndex switch
                {
                    0 => pa,
                    1 => InterpolatePixel(pa, pb, 3),
                    2 => InterpolatePixel(pa, pb, 5),
                    _ => pb
                };
            }

            return paletteIndex switch
            {
                0 => pa,
                1 => pb,
                2 => qa,
                _ => qb
            };
        }

        if (y == 1)
        {
            if (x == 0)
            {
                return paletteIndex switch
                {
                    0 => pa,
                    1 => pb,
                    2 => ra,
                    _ => rb
                };
            }

            if (x == 1)
            {
                return paletteIndex switch
                {
                    0 => pa,
                    1 => pb,
                    2 => qa,
                    _ => rb
                };
            }

            if (x == 2)
            {
                return paletteIndex switch
                {
                    0 => pa,
                    1 => pb,
                    2 => qa,
                    _ => qb
                };
            }

            return paletteIndex switch
            {
                0 => sa,
                1 => pb,
                2 => qa,
                _ => qb
            };
        }

        if (y == 2)
        {
            if (x <= 1)
            {
                return paletteIndex switch
                {
                    0 => pa,
                    1 => pb,
                    2 => ra,
                    _ => rb
                };
            }

            if (x == 2)
            {
                return paletteIndex switch
                {
                    0 => pa,
                    1 => sb,
                    2 => ra,
                    _ => qb
                };
            }

            return paletteIndex switch
            {
                0 => sa,
                1 => sb,
                2 => qa,
                _ => qb
            };
        }

        if (x == 0)
        {
            return paletteIndex switch
            {
                0 => pa,
                1 => pb,
                2 => ra,
                _ => rb
            };
        }

        if (x == 1)
        {
            return paletteIndex switch
            {
                0 => pa,
                1 => sb,
                2 => ra,
                _ => rb
            };
        }

        if (x == 2)
        {
            return paletteIndex switch
            {
                0 => sa,
                1 => sb,
                2 => ra,
                _ => rb
            };
        }

        return paletteIndex switch
        {
            0 => sa,
            1 => sb,
            2 => ra,
            _ => qb
        };
    }

    private static Pixel128 InterpolatePixel(Pixel128 a, Pixel128 b, int modulation) => new(
        (a.Red * (8 - modulation) + b.Red * modulation) / 8,
        (a.Green * (8 - modulation) + b.Green * modulation) / 8,
        (a.Blue * (8 - modulation) + b.Blue * modulation) / 8,
        (a.Alpha * (8 - modulation) + b.Alpha * modulation) / 8);

    private static int WordPixelIndex(int x, int y, byte bitsPerPixel)
    {
        var wordWidth = GetWordWidth(bitsPerPixel);
        return bitsPerPixel == 2 ? (y * wordWidth) + x : y + (x * WordHeight);
    }

    private static Rgba8UNorm GetColorA(uint colorData, bool versionII)
    {
        var mask = versionII ? 0x80000000u : 0x8000u;
        if ((colorData & mask) != 0)
        {
            return new Rgba8UNorm(
                (byte)((colorData & 0x7c00) >> 10),
                (byte)((colorData & 0x3e0) >> 5),
                (byte)((colorData & 0x1e) | ((colorData & 0x1e) >> 4)),
                0xf);
        }

        return new Rgba8UNorm(
            (byte)(((colorData & 0xf00) >> 7) | ((colorData & 0xf00) >> 11)),
            (byte)(((colorData & 0xf0) >> 3) | ((colorData & 0xf0) >> 7)),
            (byte)(((colorData & 0xe) << 1) | ((colorData & 0xe) >> 2)),
            (byte)((colorData & 0x7000) >> 11));
    }

    private static Rgba8UNorm GetColorB(uint colorData, bool versionII)
    {
        if ((colorData & 0x80000000) != 0)
        {
            return new Rgba8UNorm(
                (byte)((colorData & 0x7c000000) >> 26),
                (byte)((colorData & 0x3e00000) >> 21),
                (byte)((colorData & 0x1f0000) >> 16),
                0xf);
        }

        var adds = versionII ? 1u : 0u;
        return new Rgba8UNorm(
            (byte)(((colorData & 0xf000000) >> 23) | ((colorData & 0xf000000) >> 27)),
            (byte)(((colorData & 0xf00000) >> 19) | ((colorData & 0xf00000) >> 23)),
            (byte)(((colorData & 0xf0000) >> 15) | ((colorData & 0xf0000) >> 19)),
            (byte)(((colorData & 0x70000000) >> 27) | adds));
    }

    private static Pixel128 Color5554To8888(Rgba8UNorm color) => new(
        (color.Red << 3) | (color.Red >> 2),
        (color.Green << 3) | (color.Green >> 2),
        (color.Blue << 3) | (color.Blue >> 2),
        (color.Alpha << 4) | color.Alpha);

    private static void InterpolateColors(Rgba8UNorm p, Rgba8UNorm q, Rgba8UNorm r, Rgba8UNorm s, ref PixelBlock pixels, byte bitsPerPixel)
    {
        var wordWidth = GetWordWidth(bitsPerPixel);
        var hp = new Pixel128(p.Red, p.Green, p.Blue, p.Alpha);
        var hq = new Pixel128(q.Red, q.Green, q.Blue, q.Alpha);
        var hr = new Pixel128(r.Red, r.Green, r.Blue, r.Alpha);
        var hs = new Pixel128(s.Red, s.Green, s.Blue, s.Alpha);
        var qMinusP = hq - hp;
        var sMinusR = hs - hr;

        hp *= wordWidth;
        hr *= wordWidth;

        if (bitsPerPixel == 2)
        {
            for (var x = 0; x < wordWidth; x++)
            {
                var result = hp * 4;
                var dy = hr - hp;
                for (var y = 0; y < WordHeight; y++)
                {
                    pixels[(y * wordWidth) + x] = new Pixel128(
                        (result.Red >> 7) + (result.Red >> 2),
                        (result.Green >> 7) + (result.Green >> 2),
                        (result.Blue >> 7) + (result.Blue >> 2),
                        (result.Alpha >> 5) + (result.Alpha >> 1));
                    result += dy;
                }

                hp += qMinusP;
                hr += sMinusR;
            }
        }
        else
        {
            for (var y = 0; y < WordHeight; y++)
            {
                var result = hp * 4;
                var dy = hr - hp;
                for (var x = 0; x < wordWidth; x++)
                {
                    pixels[(y * wordWidth) + x] = new Pixel128(
                        (result.Red >> 6) + (result.Red >> 1),
                        (result.Green >> 6) + (result.Green >> 1),
                        (result.Blue >> 6) + (result.Blue >> 1),
                        (result.Alpha >> 4) + result.Alpha);
                    result += dy;
                }

                hp += qMinusP;
                hr += sMinusR;
            }
        }
    }

    private static void UnpackModulations(
        PvrtcWord word,
        int offsetX,
        int offsetY,
        ref IntGrid values,
        ref IntGrid modes,
        byte bitsPerPixel)
    {
        var wordModMode = word.ColorData & 1;
        var modulationBits = word.ModulationData;
        if (bitsPerPixel == 2)
        {
            if (wordModMode != 0)
            {
                if ((modulationBits & 1) != 0)
                {
                    wordModMode = (modulationBits & (1u << 20)) != 0 ? 3u : 2u;
                    modulationBits = (modulationBits & (1u << 21)) != 0
                        ? modulationBits | (1u << 20)
                        : modulationBits & ~(1u << 20);
                }

                modulationBits = (modulationBits & 2) != 0
                    ? modulationBits | 1u
                    : modulationBits & ~1u;

                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        SetGrid(ref modes, x + offsetX, y + offsetY, (int)wordModMode);
                        if (((x ^ y) & 1) == 0)
                        {
                            SetGrid(ref values, x + offsetX, y + offsetY, (int)(modulationBits & 3));
                            modulationBits >>= 2;
                        }
                    }
                }
            }
            else
            {
                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        SetGrid(ref modes, x + offsetX, y + offsetY, 0);
                        SetGrid(ref values, x + offsetX, y + offsetY, (modulationBits & 1) != 0 ? 3 : 0);
                        modulationBits >>= 1;
                    }
                }
            }

            return;
        }

        if (wordModMode != 0)
        {
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    var value = (int)(modulationBits & 3);
                    value = value switch
                    {
                        1 => 4,
                        2 => 14,
                        3 => 8,
                        _ => value
                    };

                    SetGrid(ref modes, y + offsetY, x + offsetX, (int)wordModMode);
                    SetGrid(ref values, y + offsetY, x + offsetX, value);
                    modulationBits >>= 2;
                }
            }
        }
        else
        {
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    var value = (int)(modulationBits & 3) * 3;
                    if (value > 3)
                    {
                        value--;
                    }

                    SetGrid(ref modes, y + offsetY, x + offsetX, 0);
                    SetGrid(ref values, y + offsetY, x + offsetX, value);
                    modulationBits >>= 2;
                }
            }
        }
    }

    private static int GetModulationValue(ref IntGrid values, ref IntGrid modes, int x, int y, byte bitsPerPixel)
    {
        if (bitsPerPixel != 2)
        {
            return GetGrid(ref values, x, y);
        }

        var mode = GetGrid(ref modes, x, y);
        if (mode == 0 || ((x ^ y) & 1) == 0)
        {
            return SRepVals0[GetGrid(ref values, x, y)];
        }

        if (mode == 1)
        {
            return (
                SRepVals0[GetGrid(ref values, x, y - 1)] +
                SRepVals0[GetGrid(ref values, x, y + 1)] +
                SRepVals0[GetGrid(ref values, x - 1, y)] +
                SRepVals0[GetGrid(ref values, x + 1, y)] +
                2) >> 2;
        }

        if (mode == 2)
        {
            return (SRepVals0[GetGrid(ref values, x - 1, y)] + SRepVals0[GetGrid(ref values, x + 1, y)] + 1) >> 1;
        }

        return (SRepVals0[GetGrid(ref values, x, y - 1)] + SRepVals0[GetGrid(ref values, x, y + 1)] + 1) >> 1;
    }

    private static void MapDecompressedData(
        Span<Rgba8UNorm> output,
        int outputWidth,
        int outputHeight,
        ref ColorBlock word,
        WordIndices words,
        byte bitsPerPixel,
        bool hasAlpha)
    {
        var wordWidth = GetWordWidth(bitsPerPixel);
        var dw = wordWidth >> 1;
        const int dh = WordHeight >> 1;

        for (var y = 0; y < dh; y++)
        {
            for (var x = 0; x < dw; x++)
            {
                WriteOutput(
                    output,
                    outputWidth,
                    outputHeight,
                    ((words.P.Y * WordHeight) + y + dh, (words.P.X * wordWidth) + x + dw),
                    word[(y * wordWidth) + x],
                    hasAlpha);
                WriteOutput(
                    output,
                    outputWidth,
                    outputHeight,
                    ((words.Q.Y * WordHeight) + y + dh, (words.Q.X * wordWidth) + x),
                    word[(y * wordWidth) + x + dw],
                    hasAlpha);
                WriteOutput(
                    output,
                    outputWidth,
                    outputHeight,
                    ((words.R.Y * WordHeight) + y, (words.R.X * wordWidth) + x + dw),
                    word[((y + dh) * wordWidth) + x],
                    hasAlpha);
                WriteOutput(
                    output,
                    outputWidth,
                    outputHeight,
                    ((words.S.Y * WordHeight) + y, (words.S.X * wordWidth) + x),
                    word[((y + dh) * wordWidth) + x + dw],
                    hasAlpha);
            }
        }
    }

    private static void WriteOutput(
        Span<Rgba8UNorm> output,
        int width,
        int height,
        (int Y, int X) position,
        Rgba8UNorm color,
        bool hasAlpha)
    {
        if ((uint)position.X >= (uint)width || (uint)position.Y >= (uint)height)
        {
            return;
        }

        output[(position.Y * width) + position.X] = new Rgba8UNorm(
            color.Red,
            color.Green,
            color.Blue,
            hasAlpha ? color.Alpha : byte.MaxValue);
    }

    private static void EncodeFromRgba8Storage(
        PvrtcFormatInfo info,
        int width,
        int height,
        ReadOnlySpan<Rgba8UNorm> image,
        Span<byte> destination,
        PvrtcCoderOptions? options = null,
        bool scoreEndpointCandidates = false)
    {
        options ??= new PvrtcCoderOptions();
        var storageExtent = GetStorageExtent(info, width, height, info.BitsPerPixel);
        var storageTexelCount = checked(storageExtent.Width * storageExtent.Height);
        var wordWidth = GetWordWidth(info.BitsPerPixel);
        var wordCount = GetWordCount(info, storageExtent.Width, storageExtent.Height);
        var useHardTransitionSearch = UsesHardTransitionSearch(options);
        var useModulationModeSearch = UsesModulationModeSearch(options);
        var useEndpointRefinement = UsesEndpointRefinement(options);
        var imageA = ArrayPool<Rgba8UNorm>.Shared.Rent(wordCount);
        var imageB = ArrayPool<Rgba8UNorm>.Shared.Rent(wordCount);
        var modulation = ArrayPool<byte>.Shared.Rent(storageTexelCount);

        try
        {
            var imageASpan = imageA.AsSpan(0, wordCount);
            var imageBSpan = imageB.AsSpan(0, wordCount);
            var modulationSpan = modulation.AsSpan(0, storageTexelCount);
            var storageWidth = storageExtent.Width;
            var storageHeight = storageExtent.Height;
            if (info.BitsPerPixel == 2)
            {
                Morph(
                    image,
                    width,
                    height,
                    storageWidth,
                    storageHeight,
                    wordWidth,
                    imageASpan,
                    imageBSpan,
                    info.HasAlpha,
                    scoreEndpointCandidates);
                Modulate2Bpp(
                    image,
                    width,
                    height,
                    storageWidth,
                    storageHeight,
                    imageASpan,
                    imageBSpan,
                    modulationSpan,
                    info.HasAlpha);
                EncodeWords2Bpp(
                    storageWidth,
                    storageHeight,
                    imageASpan,
                    imageBSpan,
                    modulationSpan,
                    destination,
                    info.VersionII,
                    info.HasAlpha);
            }
            else
            {
                Morph(
                    image,
                    width,
                    height,
                    storageWidth,
                    storageHeight,
                    wordWidth,
                    imageASpan,
                    imageBSpan,
                    info.HasAlpha,
                    scoreEndpointCandidates);
                Modulate4Bpp(
                    image,
                    width,
                    height,
                    storageWidth,
                    storageHeight,
                    imageASpan,
                    imageBSpan,
                    modulationSpan,
                    info.HasAlpha);
                EncodeWords4Bpp(
                    storageWidth,
                    storageHeight,
                    imageASpan,
                    imageBSpan,
                    modulationSpan,
                    destination,
                    info.VersionII,
                    info.HasAlpha,
                    image,
                    width,
                    height,
                    useHardTransitionSearch,
                    useModulationModeSearch,
                    useEndpointRefinement);
            }
        }
        finally
        {
            ArrayPool<Rgba8UNorm>.Shared.Return(imageA);
            ArrayPool<Rgba8UNorm>.Shared.Return(imageB);
            ArrayPool<byte>.Shared.Return(modulation);
        }
    }

    private static void Morph(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int storageWidth,
        int storageHeight,
        int wordWidth,
        Span<Rgba8UNorm> outA,
        Span<Rgba8UNorm> outB,
        bool hasAlpha,
        bool scoreEndpointCandidates)
    {
        var blockCountX = storageWidth / wordWidth;
        var blockCountY = storageHeight / WordHeight;
        if (TextureCodingParallel.ShouldParallelize(blockCountX, blockCountY))
        {
            var imageLength = image.Length;
            var outALength = outA.Length;
            var outBLength = outB.Length;
            unsafe
            {
                fixed (Rgba8UNorm* imageBase = image)
                fixed (Rgba8UNorm* outABase = outA)
                fixed (Rgba8UNorm* outBBase = outB)
                {
                    var imageAddress = (nint)imageBase;
                    var outAAddress = (nint)outABase;
                    var outBAddress = (nint)outBBase;
                    Parallel.For(0, blockCountY, wordY =>
                    {
                        var localImage = new ReadOnlySpan<Rgba8UNorm>((void*)imageAddress, imageLength);
                        var localOutA = new Span<Rgba8UNorm>((void*)outAAddress, outALength);
                        var localOutB = new Span<Rgba8UNorm>((void*)outBAddress, outBLength);
                        var y = wordY * WordHeight;

                        for (var wordX = 0; wordX < blockCountX; wordX++)
                        {
                            var x = wordX * wordWidth;
                            GetExtremesFast(
                                localImage,
                                imageWidth,
                                imageHeight,
                                x,
                                y,
                                wordWidth,
                                hasAlpha,
                                scoreEndpointCandidates,
                                out var indexA,
                                out var indexB);
                            var outputIndex = (wordY * blockCountX) + wordX;
                            localOutA[outputIndex] = ApplyColorChannelReduction(localImage[indexA], isB: false, hasAlpha);
                            localOutB[outputIndex] = ApplyColorChannelReduction(localImage[indexB], isB: true, hasAlpha);
                        }
                    });
                }
            }

            return;
        }

        for (var y = 0; y < storageHeight; y += WordHeight)
        {
            for (var x = 0; x < storageWidth; x += wordWidth)
            {
                GetExtremesFast(
                    image,
                    imageWidth,
                    imageHeight,
                    x,
                    y,
                    wordWidth,
                    hasAlpha,
                    scoreEndpointCandidates,
                    out var indexA,
                    out var indexB);
                var outputIndex = (y / WordHeight * (storageWidth / wordWidth)) + (x / wordWidth);
                outA[outputIndex] = ApplyColorChannelReduction(image[indexA], isB: false, hasAlpha);
                outB[outputIndex] = ApplyColorChannelReduction(image[indexB], isB: true, hasAlpha);
            }
        }
    }

    private static void Modulate4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int storageWidth,
        int storageHeight,
        ReadOnlySpan<Rgba8UNorm> imageA,
        ReadOnlySpan<Rgba8UNorm> imageB,
        Span<byte> modulation,
        bool hasAlpha)
    {
        if (TextureCodingParallel.ShouldParallelize(storageWidth, storageHeight))
        {
            var imageLength = image.Length;
            var imageALength = imageA.Length;
            var imageBLength = imageB.Length;
            var modulationLength = modulation.Length;
            unsafe
            {
                fixed (Rgba8UNorm* imageBase = image)
                fixed (Rgba8UNorm* imageABase = imageA)
                fixed (Rgba8UNorm* imageBBase = imageB)
                fixed (byte* modulationBase = modulation)
                {
                    var imageAddress = (nint)imageBase;
                    var imageAAddress = (nint)imageABase;
                    var imageBAddress = (nint)imageBBase;
                    var modulationAddress = (nint)modulationBase;
                    Parallel.For(0, storageHeight, y =>
                    {
                        var localImage = new ReadOnlySpan<Rgba8UNorm>((void*)imageAddress, imageLength);
                        var localImageA = new ReadOnlySpan<Rgba8UNorm>((void*)imageAAddress, imageALength);
                        var localImageB = new ReadOnlySpan<Rgba8UNorm>((void*)imageBAddress, imageBLength);
                        var localModulation = new Span<byte>((void*)modulationAddress, modulationLength);

                        for (var x = 0; x < storageWidth; x++)
                        {
                            var colorA = GetInterpolatedColor(localImageA, storageWidth, storageHeight, 4, x, y);
                            var colorB = GetInterpolatedColor(localImageB, storageWidth, storageHeight, 4, x, y);
                            localModulation[(y * storageWidth) + x] = (byte)BestModulation4Bpp(
                                SampleImage(localImage, imageWidth, imageHeight, x, y),
                                colorA,
                                colorB,
                                hasAlpha);
                        }
                    });
                }
            }

            return;
        }

        for (var y = 0; y < storageHeight; y++)
        {
            for (var x = 0; x < storageWidth; x++)
            {
                var colorA = GetInterpolatedColor(imageA, storageWidth, storageHeight, 4, x, y);
                var colorB = GetInterpolatedColor(imageB, storageWidth, storageHeight, 4, x, y);
                modulation[(y * storageWidth) + x] = (byte)BestModulation4Bpp(
                    SampleImage(image, imageWidth, imageHeight, x, y),
                    colorA,
                    colorB,
                    hasAlpha);
            }
        }
    }

    private static void Modulate2Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int storageWidth,
        int storageHeight,
        ReadOnlySpan<Rgba8UNorm> imageA,
        ReadOnlySpan<Rgba8UNorm> imageB,
        Span<byte> modulation,
        bool hasAlpha)
    {
        if (TextureCodingParallel.ShouldParallelize(storageWidth, storageHeight))
        {
            var imageLength = image.Length;
            var imageALength = imageA.Length;
            var imageBLength = imageB.Length;
            var modulationLength = modulation.Length;
            unsafe
            {
                fixed (Rgba8UNorm* imageBase = image)
                fixed (Rgba8UNorm* imageABase = imageA)
                fixed (Rgba8UNorm* imageBBase = imageB)
                fixed (byte* modulationBase = modulation)
                {
                    var imageAddress = (nint)imageBase;
                    var imageAAddress = (nint)imageABase;
                    var imageBAddress = (nint)imageBBase;
                    var modulationAddress = (nint)modulationBase;
                    Parallel.For(0, storageHeight, y =>
                    {
                        var localImage = new ReadOnlySpan<Rgba8UNorm>((void*)imageAddress, imageLength);
                        var localImageA = new ReadOnlySpan<Rgba8UNorm>((void*)imageAAddress, imageALength);
                        var localImageB = new ReadOnlySpan<Rgba8UNorm>((void*)imageBAddress, imageBLength);
                        var localModulation = new Span<byte>((void*)modulationAddress, modulationLength);

                        for (var x = 0; x < storageWidth; x++)
                        {
                            var colorA = GetInterpolatedColor(localImageA, storageWidth, storageHeight, 8, x, y);
                            var colorB = GetInterpolatedColor(localImageB, storageWidth, storageHeight, 8, x, y);
                            localModulation[(y * storageWidth) + x] = (byte)BestModulation2Bpp(
                                SampleImage(localImage, imageWidth, imageHeight, x, y),
                                colorA,
                                colorB,
                                hasAlpha);
                        }
                    });
                }
            }

            return;
        }

        for (var y = 0; y < storageHeight; y++)
        {
            for (var x = 0; x < storageWidth; x++)
            {
                var colorA = GetInterpolatedColor(imageA, storageWidth, storageHeight, 8, x, y);
                var colorB = GetInterpolatedColor(imageB, storageWidth, storageHeight, 8, x, y);
                modulation[(y * storageWidth) + x] = (byte)BestModulation2Bpp(
                    SampleImage(image, imageWidth, imageHeight, x, y),
                    colorA,
                    colorB,
                    hasAlpha);
            }
        }
    }

    private static void EncodeWords4Bpp(
        int width,
        int height,
        ReadOnlySpan<Rgba8UNorm> imageA,
        ReadOnlySpan<Rgba8UNorm> imageB,
        ReadOnlySpan<byte> modulation,
        Span<byte> destination,
        bool versionII,
        bool hasAlpha,
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        bool useHardTransitionSearch,
        bool useModulationModeSearch,
        bool useEndpointRefinement)
    {
        var blockCountX = width / 4;
        var blockCountY = height / 4;
        var wordCount = blockCountX * blockCountY;
        if (TextureCodingParallel.ShouldParallelize(blockCountX, blockCountY))
        {
            var imageALength = imageA.Length;
            var imageBLength = imageB.Length;
            var modulationLength = modulation.Length;
            var destinationLength = destination.Length;
            unsafe
            {
                fixed (Rgba8UNorm* imageABase = imageA)
                fixed (Rgba8UNorm* imageBBase = imageB)
                fixed (byte* modulationBase = modulation)
                fixed (byte* destinationBase = destination)
                {
                    var imageAAddress = (nint)imageABase;
                    var imageBAddress = (nint)imageBBase;
                    var modulationAddress = (nint)modulationBase;
                    var destinationAddress = (nint)destinationBase;
                    Parallel.For(0, wordCount, i =>
                    {
                        var localImageA = new ReadOnlySpan<Rgba8UNorm>((void*)imageAAddress, imageALength);
                        var localImageB = new ReadOnlySpan<Rgba8UNorm>((void*)imageBAddress, imageBLength);
                        var localModulation = new ReadOnlySpan<byte>((void*)modulationAddress, modulationLength);
                        var localDestination = new Span<byte>((void*)destinationAddress, destinationLength);

                        FromZOrder(i, blockCountX, blockCountY, versionII, out var blockX, out var blockY);
                        var modData = CalculateBlockModulationData4Bpp(localModulation, width, blockX, blockY);
                        var colorData = EncodeColors4Bpp(
                            localImageA[(blockY * blockCountX) + blockX],
                            localImageB[(blockY * blockCountX) + blockX],
                            versionII,
                            hasAlpha);
                        WriteWord(localDestination, i, modData, colorData);
                    });
                }
            }
        }
        else
        {
            for (var i = 0; i < wordCount; i++)
            {
                FromZOrder(i, blockCountX, blockCountY, versionII, out var blockX, out var blockY);
                var modData = CalculateBlockModulationData4Bpp(modulation, width, blockX, blockY);
                var colorData = EncodeColors4Bpp(
                    imageA[(blockY * blockCountX) + blockX],
                    imageB[(blockY * blockCountX) + blockX],
                    versionII,
                    hasAlpha);
                WriteWord(destination, i, modData, colorData);
            }
        }

        if (versionII && useHardTransitionSearch)
        {
            OptimizeHardTransitions4Bpp(
                image,
                imageWidth,
                imageHeight,
                blockCountX,
                blockCountY,
                destination,
                hasAlpha);
        }

        if (versionII && useEndpointRefinement)
        {
            OptimizeOpaqueEndpoints4Bpp(
                image,
                imageWidth,
                imageHeight,
                blockCountX,
                blockCountY,
                destination,
                hasAlpha);
        }

        if (versionII && useModulationModeSearch)
        {
            OptimizeModulationModes4Bpp(
                image,
                imageWidth,
                imageHeight,
                blockCountX,
                blockCountY,
                destination,
                hasAlpha);
        }

        if (versionII && useHardTransitionSearch && useModulationModeSearch)
        {
            OptimizeHardTransitions4Bpp(
                image,
                imageWidth,
                imageHeight,
                blockCountX,
                blockCountY,
                destination,
                hasAlpha);
        }

        if (versionII && useEndpointRefinement)
        {
            OptimizeOpaqueEndpoints4Bpp(
                image,
                imageWidth,
                imageHeight,
                blockCountX,
                blockCountY,
                destination,
                hasAlpha);

            if (useModulationModeSearch)
            {
                OptimizeModulationModes4Bpp(
                    image,
                    imageWidth,
                    imageHeight,
                    blockCountX,
                    blockCountY,
                    destination,
                    hasAlpha);
            }

            if (useHardTransitionSearch)
            {
                OptimizeHardTransitions4Bpp(
                    image,
                    imageWidth,
                    imageHeight,
                    blockCountX,
                    blockCountY,
                    destination,
                    hasAlpha);
            }
        }
    }

    private static void OptimizeOpaqueEndpoints4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        Span<byte> words,
        bool hasAlpha)
    {
        const uint opaqueEndpointBit = 0x80000000u;
        ReadOnlySpan<EndpointField> fields =
        [
            new EndpointField(1, 4),
            new EndpointField(5, 5),
            new EndpointField(10, 5),
            new EndpointField(16, 5),
            new EndpointField(21, 5),
            new EndpointField(26, 5)
        ];

        for (var pass = 0; pass < 2; pass++)
        {
            for (var blockY = 0; blockY < blockCountY; blockY++)
            {
                for (var blockX = 0; blockX < blockCountX; blockX++)
                {
                    var wordIndex = GetStorageIndex(blockCountX, blockCountY, blockX, blockY, versionII: true);
                    var current = ReadWord(words, wordIndex);
                    if ((current.ColorData & opaqueEndpointBit) == 0)
                    {
                        continue;
                    }

                    var bestError = CalculateAffectedWordError4Bpp(
                        image,
                        imageWidth,
                        imageHeight,
                        blockCountX,
                        blockCountY,
                        blockX,
                        blockY,
                        current,
                        words,
                        hasAlpha);

                    for (var i = 0; i < fields.Length; i++)
                    {
                        current = OptimizeEndpointField4Bpp(
                            image,
                            imageWidth,
                            imageHeight,
                            blockCountX,
                            blockCountY,
                            blockX,
                            blockY,
                            current,
                            words,
                            fields[i],
                            hasAlpha,
                            ref bestError);
                    }

                    WriteWord(words, wordIndex, current);
                }
            }
        }
    }

    private static PvrtcWord OptimizeEndpointField4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        int blockX,
        int blockY,
        PvrtcWord word,
        Span<byte> words,
        EndpointField field,
        bool hasAlpha,
        ref ulong bestError)
    {
        const int searchRadius = 2;
        var code = GetBits(word.ColorData, field.StartBit, field.BitCount);
        var minCode = Math.Max(0, code - searchRadius);
        var maxCode = Math.Min((int)GetMask(field.BitCount), code + searchRadius);
        var best = word;

        for (var candidateCode = minCode; candidateCode <= maxCode; candidateCode++)
        {
            if (candidateCode == code)
            {
                continue;
            }

            var candidate = new PvrtcWord(
                word.ModulationData,
                WithBits(field.StartBit, field.BitCount, candidateCode, word.ColorData));
            var error = CalculateAffectedWordError4Bpp(
                image,
                imageWidth,
                imageHeight,
                blockCountX,
                blockCountY,
                blockX,
                blockY,
                candidate,
                words,
                hasAlpha);

            if (error < bestError)
            {
                best = candidate;
                bestError = error;
            }
        }

        return best;
    }

    private static void OptimizeModulationModes4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        Span<byte> words,
        bool hasAlpha)
    {
        const uint modeBit = 1u;

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var wordIndex = GetStorageIndex(blockCountX, blockCountY, blockX, blockY, versionII: true);
                var current = ReadWord(words, wordIndex);
                var best = current;
                var bestError = CalculateAffectedWordError4Bpp(
                    image,
                    imageWidth,
                    imageHeight,
                    blockCountX,
                    blockCountY,
                    blockX,
                    blockY,
                    current,
                    words,
                    hasAlpha);

                for (var mode = 0u; mode <= 1u; mode++)
                {
                    var colorData = (current.ColorData & ~modeBit) | mode;
                    var modulationData = OptimizeModulationData4Bpp(
                        image,
                        imageWidth,
                        imageHeight,
                        blockCountX,
                        blockCountY,
                        blockX,
                        blockY,
                        new PvrtcWord(current.ModulationData, colorData),
                        words,
                        hasAlpha);
                    var candidate = new PvrtcWord(modulationData, colorData);
                    var error = CalculateAffectedWordError4Bpp(
                        image,
                        imageWidth,
                        imageHeight,
                        blockCountX,
                        blockCountY,
                        blockX,
                        blockY,
                        candidate,
                        words,
                        hasAlpha);

                    if (error < bestError)
                    {
                        best = candidate;
                        bestError = error;
                    }
                }

                WriteWord(words, wordIndex, best);
            }
        }
    }

    private static uint OptimizeModulationData4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        int blockX,
        int blockY,
        PvrtcWord word,
        Span<byte> words,
        bool hasAlpha)
    {
        var result = word.ModulationData;
        for (var bitPosition = 0; bitPosition < 32; bitPosition += 2)
        {
            var bestBits = result;
            var bestError = ulong.MaxValue;
            for (var code = 0; code < 4; code++)
            {
                var candidateBits = WithBits(bitPosition, 2, code, result);
                var candidate = new PvrtcWord(candidateBits, word.ColorData);
                var error = CalculateAffectedWordError4Bpp(
                    image,
                    imageWidth,
                    imageHeight,
                    blockCountX,
                    blockCountY,
                    blockX,
                    blockY,
                    candidate,
                    words,
                    hasAlpha);
                if (error < bestError)
                {
                    bestBits = candidateBits;
                    bestError = error;
                }
            }

            result = bestBits;
        }

        return result;
    }

    private static ulong CalculateAffectedWordError4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        int blockX,
        int blockY,
        PvrtcWord word,
        Span<byte> words,
        bool hasAlpha)
    {
        var error = 0ul;
        error += CalculateWordGroupError4Bpp(
            image,
            imageWidth,
            imageHeight,
            blockCountX,
            blockCountY,
            blockX,
            blockY,
            blockX,
            blockY,
            word,
            words,
            hasAlpha);
        error += CalculateWordGroupError4Bpp(
            image,
            imageWidth,
            imageHeight,
            blockCountX,
            blockCountY,
            blockX - 1,
            blockY,
            blockX,
            blockY,
            word,
            words,
            hasAlpha);
        error += CalculateWordGroupError4Bpp(
            image,
            imageWidth,
            imageHeight,
            blockCountX,
            blockCountY,
            blockX,
            blockY - 1,
            blockX,
            blockY,
            word,
            words,
            hasAlpha);
        error += CalculateWordGroupError4Bpp(
            image,
            imageWidth,
            imageHeight,
            blockCountX,
            blockCountY,
            blockX - 1,
            blockY - 1,
            blockX,
            blockY,
            word,
            words,
            hasAlpha);
        return error;
    }

    private static ulong CalculateWordGroupError4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        int groupBlockX,
        int groupBlockY,
        int targetBlockX,
        int targetBlockY,
        PvrtcWord targetWord,
        Span<byte> words,
        bool hasAlpha)
    {
        groupBlockX = WrapWordIndex(blockCountX, groupBlockX);
        groupBlockY = WrapWordIndex(blockCountY, groupBlockY);
        var qBlockX = WrapWordIndex(blockCountX, groupBlockX + 1);
        var rBlockY = WrapWordIndex(blockCountY, groupBlockY + 1);
        var p = ReadWordOrTarget4Bpp(words, blockCountX, blockCountY, groupBlockX, groupBlockY, targetBlockX, targetBlockY, targetWord);
        var q = ReadWordOrTarget4Bpp(words, blockCountX, blockCountY, qBlockX, groupBlockY, targetBlockX, targetBlockY, targetWord);
        var r = ReadWordOrTarget4Bpp(words, blockCountX, blockCountY, groupBlockX, rBlockY, targetBlockX, targetBlockY, targetWord);
        var s = ReadWordOrTarget4Bpp(words, blockCountX, blockCountY, qBlockX, rBlockY, targetBlockX, targetBlockY, targetWord);

        return CalculateMappedWordError4Bpp(
            image,
            imageWidth,
            imageHeight,
            blockCountX,
            blockCountY,
            groupBlockX,
            groupBlockY,
            p,
            q,
            r,
            s,
            hasAlpha);
    }

    private static PvrtcWord ReadWordOrTarget4Bpp(
        Span<byte> words,
        int blockCountX,
        int blockCountY,
        int blockX,
        int blockY,
        int targetBlockX,
        int targetBlockY,
        PvrtcWord targetWord)
    {
        if (blockX == targetBlockX && blockY == targetBlockY)
        {
            return targetWord;
        }

        return ReadWord(words, GetStorageIndex(blockCountX, blockCountY, blockX, blockY, versionII: true));
    }

    private static void OptimizeHardTransitions4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        Span<byte> words,
        bool hasAlpha)
    {
        const uint hardTransitionBit = 0x8000u;
        const uint opaqueEndpointBit = 0x80000000u;

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var pIndex = GetStorageIndex(blockCountX, blockCountY, blockX, blockY, versionII: true);
                var p = ReadWord(words, pIndex);
                if ((p.ColorData & opaqueEndpointBit) == 0)
                {
                    WriteWord(words, pIndex, new PvrtcWord(p.ModulationData, p.ColorData & ~hardTransitionBit));
                    continue;
                }

                var qIndex = GetStorageIndex(blockCountX, blockCountY, WrapWordIndex(blockCountX, blockX + 1), blockY, versionII: true);
                var rIndex = GetStorageIndex(blockCountX, blockCountY, blockX, WrapWordIndex(blockCountY, blockY + 1), versionII: true);
                var sIndex = GetStorageIndex(blockCountX, blockCountY, WrapWordIndex(blockCountX, blockX + 1), WrapWordIndex(blockCountY, blockY + 1), versionII: true);
                var q = ReadWord(words, qIndex);
                var r = ReadWord(words, rIndex);
                var s = ReadWord(words, sIndex);

                var softP = new PvrtcWord(p.ModulationData, p.ColorData & ~hardTransitionBit);
                var hardP = new PvrtcWord(p.ModulationData, p.ColorData | hardTransitionBit);
                var softError = CalculateMappedWordError4Bpp(
                    image,
                    imageWidth,
                    imageHeight,
                    blockCountX,
                    blockCountY,
                    blockX,
                    blockY,
                    softP,
                    q,
                    r,
                    s,
                    hasAlpha);
                var hardError = CalculateMappedWordError4Bpp(
                    image,
                    imageWidth,
                    imageHeight,
                    blockCountX,
                    blockCountY,
                    blockX,
                    blockY,
                    hardP,
                    q,
                    r,
                    s,
                    hasAlpha);

                WriteWord(words, pIndex, hardError < softError ? hardP : softP);
            }
        }
    }

    private static ulong CalculateMappedWordError4Bpp(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        int blockCountX,
        int blockCountY,
        int blockX,
        int blockY,
        PvrtcWord p,
        PvrtcWord q,
        PvrtcWord r,
        PvrtcWord s,
        bool hasAlpha)
    {
        var pixels = DecompressWord(p, q, r, s, bitsPerPixel: 4, versionII: true);
        var indices = new WordIndices(
            new WordCoordinate(blockX, blockY),
            new WordCoordinate(WrapWordIndex(blockCountX, blockX + 1), blockY),
            new WordCoordinate(blockX, WrapWordIndex(blockCountY, blockY + 1)),
            new WordCoordinate(WrapWordIndex(blockCountX, blockX + 1), WrapWordIndex(blockCountY, blockY + 1)));

        return CalculateMappedError(image, imageWidth, imageHeight, ref pixels, indices, bitsPerPixel: 4, hasAlpha);
    }

    private static ulong CalculateMappedError(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        ref ColorBlock word,
        WordIndices words,
        byte bitsPerPixel,
        bool hasAlpha)
    {
        var wordWidth = GetWordWidth(bitsPerPixel);
        var dw = wordWidth >> 1;
        const int dh = WordHeight >> 1;
        var error = 0ul;

        for (var y = 0; y < dh; y++)
        {
            for (var x = 0; x < dw; x++)
            {
                error += MappedPixelError(
                    image,
                    imageWidth,
                    imageHeight,
                    ((words.P.Y * WordHeight) + y + dh, (words.P.X * wordWidth) + x + dw),
                    word[(y * wordWidth) + x],
                    hasAlpha);
                error += MappedPixelError(
                    image,
                    imageWidth,
                    imageHeight,
                    ((words.Q.Y * WordHeight) + y + dh, (words.Q.X * wordWidth) + x),
                    word[(y * wordWidth) + x + dw],
                    hasAlpha);
                error += MappedPixelError(
                    image,
                    imageWidth,
                    imageHeight,
                    ((words.R.Y * WordHeight) + y, (words.R.X * wordWidth) + x + dw),
                    word[((y + dh) * wordWidth) + x],
                    hasAlpha);
                error += MappedPixelError(
                    image,
                    imageWidth,
                    imageHeight,
                    ((words.S.Y * WordHeight) + y, (words.S.X * wordWidth) + x),
                    word[((y + dh) * wordWidth) + x + dw],
                    hasAlpha);
            }
        }

        return error;
    }

    private static ulong MappedPixelError(
        ReadOnlySpan<Rgba8UNorm> image,
        int imageWidth,
        int imageHeight,
        (int Y, int X) position,
        Rgba8UNorm color,
        bool hasAlpha)
    {
        if ((uint)position.X >= (uint)imageWidth || (uint)position.Y >= (uint)imageHeight)
        {
            return 0;
        }

        var expected = image[(position.Y * imageWidth) + position.X];
        return SquaredColorError(expected, color, hasAlpha);
    }

    private static ulong SquaredColorError(Rgba8UNorm expected, Rgba8UNorm actual, bool hasAlpha)
    {
        var red = expected.Red - actual.Red;
        var green = expected.Green - actual.Green;
        var blue = expected.Blue - actual.Blue;
        var alpha = hasAlpha ? expected.Alpha - actual.Alpha : 0;
        return (ulong)((red * red) + (green * green) + (blue * blue) + (alpha * alpha));
    }

    private static void EncodeWords2Bpp(
        int width,
        int height,
        ReadOnlySpan<Rgba8UNorm> imageA,
        ReadOnlySpan<Rgba8UNorm> imageB,
        ReadOnlySpan<byte> modulation,
        Span<byte> destination,
        bool versionII,
        bool hasAlpha)
    {
        var blockCountX = width / 8;
        var blockCountY = height / 4;
        var wordCount = blockCountX * blockCountY;
        if (TextureCodingParallel.ShouldParallelize(blockCountX, blockCountY))
        {
            var imageALength = imageA.Length;
            var imageBLength = imageB.Length;
            var modulationLength = modulation.Length;
            var destinationLength = destination.Length;
            unsafe
            {
                fixed (Rgba8UNorm* imageABase = imageA)
                fixed (Rgba8UNorm* imageBBase = imageB)
                fixed (byte* modulationBase = modulation)
                fixed (byte* destinationBase = destination)
                {
                    var imageAAddress = (nint)imageABase;
                    var imageBAddress = (nint)imageBBase;
                    var modulationAddress = (nint)modulationBase;
                    var destinationAddress = (nint)destinationBase;
                    Parallel.For(0, wordCount, i =>
                    {
                        var localImageA = new ReadOnlySpan<Rgba8UNorm>((void*)imageAAddress, imageALength);
                        var localImageB = new ReadOnlySpan<Rgba8UNorm>((void*)imageBAddress, imageBLength);
                        var localModulation = new ReadOnlySpan<byte>((void*)modulationAddress, modulationLength);
                        var localDestination = new Span<byte>((void*)destinationAddress, destinationLength);

                        FromZOrder(i, blockCountX, blockCountY, versionII, out var blockX, out var blockY);
                        var mode = CalculateBlockModulationMode2Bpp(localModulation, width, height, blockX, blockY);
                        var modData = CalculateBlockModulationData2Bpp(localModulation, width, blockX, blockY, mode);
                        var colorData = EncodeColors2Bpp(
                            localImageA[(blockY * blockCountX) + blockX],
                            localImageB[(blockY * blockCountX) + blockX],
                            mode,
                            versionII,
                            hasAlpha);
                        WriteWord(localDestination, i, modData, colorData);
                    });
                }
            }

            return;
        }

        for (var i = 0; i < wordCount; i++)
        {
            FromZOrder(i, blockCountX, blockCountY, versionII, out var blockX, out var blockY);
            var mode = CalculateBlockModulationMode2Bpp(modulation, width, height, blockX, blockY);
            var modData = CalculateBlockModulationData2Bpp(modulation, width, blockX, blockY, mode);
            var colorData = EncodeColors2Bpp(
                imageA[(blockY * blockCountX) + blockX],
                imageB[(blockY * blockCountX) + blockX],
                mode,
                versionII,
                hasAlpha);
            WriteWord(destination, i, modData, colorData);
        }
    }

    private static void WriteWord(Span<byte> destination, int wordIndex, uint modulationData, uint colorData)
    {
        var offset = wordIndex * 8;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), modulationData);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset + 4, 4), colorData);
    }

    private static void WriteWord(Span<byte> destination, int wordIndex, PvrtcWord word) =>
        WriteWord(destination, wordIndex, word.ModulationData, word.ColorData);

    private static uint CalculateBlockModulationData4Bpp(ReadOnlySpan<byte> modulation, int width, int blockX, int blockY)
    {
        var result = 0u;
        var bitPosition = 0;
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                SetBits(bitPosition, 2, modulation[((blockY * 4 + y) * width) + (blockX * 4 + x)], ref result);
                bitPosition += 2;
            }
        }

        return result;
    }

    private static uint CalculateBlockModulationData2Bpp(
        ReadOnlySpan<byte> modulation,
        int width,
        int blockX,
        int blockY,
        ModulationMode2Bpp mode)
    {
        var result = 0u;
        var bitPosition = 0;
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var index = ((blockY * 4 + y) * width) + (blockX * 8 + x);
                if (mode == ModulationMode2Bpp.Direct1Bpp)
                {
                    SetBits(bitPosition, 1, modulation[index] / 2, ref result);
                    bitPosition++;
                    continue;
                }

                if (((x ^ y) & 1) != 0)
                {
                    continue;
                }

                var bit = modulation[index];
                if (bitPosition == 0)
                {
                    bit = mode == ModulationMode2Bpp.Interpolated2Bpp ? (byte)(bit & 2) : (byte)(bit | 1);
                }
                else if (bitPosition == 20)
                {
                    bit = mode == ModulationMode2Bpp.VerticallyInterpolated2Bpp ? (byte)(bit | 1) : (byte)(bit & 2);
                }

                SetBits(bitPosition, 2, bit, ref result);
                bitPosition += 2;
            }
        }

        return result;
    }

    private static ModulationMode2Bpp CalculateBlockModulationMode2Bpp(
        ReadOnlySpan<byte> modulation,
        int width,
        int height,
        int blockX,
        int blockY)
    {
        var intermediateValueCount = 0u;
        var horizontalCount = 0u;
        var verticalCount = 0u;

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var pixelX = blockX * 8 + x;
                var pixelY = blockY * 4 + y;
                var index = (pixelY * width) + pixelX;
                if (modulation[index] is 1 or 2)
                {
                    intermediateValueCount++;
                }

                var rightIndex = (pixelY * width) + ((pixelX + 1) % width);
                var downIndex = (((pixelY + 1) % height) * width) + pixelX;
                horizontalCount += Abs(modulation[index] - modulation[rightIndex]);
                verticalCount += Abs(modulation[index] - modulation[downIndex]);
            }
        }

        if (intermediateValueCount <= 4)
        {
            return ModulationMode2Bpp.Direct1Bpp;
        }

        const uint absoluteThreshold = 10;
        const uint ratioThreshold = 2;
        if (verticalCount > absoluteThreshold && verticalCount > horizontalCount * ratioThreshold)
        {
            return ModulationMode2Bpp.VerticallyInterpolated2Bpp;
        }

        if (horizontalCount > absoluteThreshold && horizontalCount > verticalCount * ratioThreshold)
        {
            return ModulationMode2Bpp.HorizontallyInterpolated2Bpp;
        }

        return ModulationMode2Bpp.Interpolated2Bpp;
    }

    private static uint EncodeColors4Bpp(Rgba8UNorm colorA, Rgba8UNorm colorB, bool versionII, bool hasAlpha)
    {
        var value = 0u;
        if (versionII)
        {
            EncodeColorPairVersionII(colorA, colorB, hasAlpha, ref value);
        }
        else
        {
            EncodeColorA(colorA, hasAlpha, ref value);
            EncodeColorB(colorB, hasAlpha, versionII, ref value);
        }

        SetBits(0, 1, 0, ref value);
        return value;
    }

    private static uint EncodeColors2Bpp(Rgba8UNorm colorA, Rgba8UNorm colorB, ModulationMode2Bpp mode, bool versionII, bool hasAlpha)
    {
        var value = 0u;
        if (versionII)
        {
            EncodeColorPairVersionII(colorA, colorB, hasAlpha, ref value);
        }
        else
        {
            EncodeColorA(colorA, hasAlpha, ref value);
            EncodeColorB(colorB, hasAlpha, versionII, ref value);
        }

        SetBits(0, 1, mode == ModulationMode2Bpp.Direct1Bpp ? 0 : 1, ref value);
        return value;
    }

    private static void EncodeColorPairVersionII(Rgba8UNorm colorA, Rgba8UNorm colorB, bool hasAlpha, ref uint value)
    {
        if (!hasAlpha || (colorA.Alpha == byte.MaxValue && colorB.Alpha == byte.MaxValue))
        {
            SetBits(31, 1, 1, ref value);
            SetBits(16, 5, colorB.Blue >> 3, ref value);
            SetBits(21, 5, colorB.Green >> 3, ref value);
            SetBits(26, 5, colorB.Red >> 3, ref value);
            SetBits(15, 1, 0, ref value);
            SetBits(1, 4, colorA.Blue >> 4, ref value);
            SetBits(5, 5, colorA.Green >> 3, ref value);
            SetBits(10, 5, colorA.Red >> 3, ref value);
            return;
        }

        SetBits(31, 1, 0, ref value);
        SetBits(16, 4, colorB.Blue >> 4, ref value);
        SetBits(20, 4, colorB.Green >> 4, ref value);
        SetBits(24, 4, colorB.Red >> 4, ref value);
        SetBits(28, 3, colorB.Alpha >> 5, ref value);
        SetBits(15, 1, 0, ref value);
        SetBits(1, 3, colorA.Blue >> 5, ref value);
        SetBits(4, 4, colorA.Green >> 4, ref value);
        SetBits(8, 4, colorA.Red >> 4, ref value);
        SetBits(12, 3, colorA.Alpha >> 5, ref value);
    }

    private static void EncodeColorA(Rgba8UNorm color, bool hasAlpha, ref uint value)
    {
        if (!hasAlpha || color.Alpha == byte.MaxValue)
        {
            SetBits(15, 1, 1, ref value);
            SetBits(1, 4, color.Blue >> 4, ref value);
            SetBits(5, 5, color.Green >> 3, ref value);
            SetBits(10, 5, color.Red >> 3, ref value);
            return;
        }

        SetBits(15, 1, 0, ref value);
        SetBits(1, 3, color.Blue >> 5, ref value);
        SetBits(4, 4, color.Green >> 4, ref value);
        SetBits(8, 4, color.Red >> 4, ref value);
        SetBits(12, 3, color.Alpha >> 5, ref value);
    }

    private static void EncodeColorB(Rgba8UNorm color, bool hasAlpha, bool versionII, ref uint value)
    {
        if (!hasAlpha || color.Alpha == byte.MaxValue)
        {
            SetBits(31, 1, 1, ref value);
            SetBits(16, 5, color.Blue >> 3, ref value);
            SetBits(21, 5, color.Green >> 3, ref value);
            SetBits(26, 5, color.Red >> 3, ref value);
            return;
        }

        var alpha = color.Alpha >> 5;
        if (versionII)
        {
            alpha &= 0x6;
        }

        SetBits(31, 1, 0, ref value);
        SetBits(16, 4, color.Blue >> 4, ref value);
        SetBits(20, 4, color.Green >> 4, ref value);
        SetBits(24, 4, color.Red >> 4, ref value);
        SetBits(28, 3, alpha, ref value);
    }

    private static Rgba8UNorm ApplyColorChannelReduction(Rgba8UNorm color, bool isB, bool hasAlpha)
    {
        if (!hasAlpha || color.Alpha == byte.MaxValue)
        {
            color.Red = ApplyBitDepthReduction(color.Red, 5);
            color.Green = ApplyBitDepthReduction(color.Green, 5);
            color.Blue = ApplyBitDepthReduction(color.Blue, isB ? 5 : 4);
        }
        else
        {
            color.Red = ApplyBitDepthReduction(color.Red, 4);
            color.Green = ApplyBitDepthReduction(color.Green, 4);
            color.Blue = ApplyBitDepthReduction(color.Blue, isB ? 4 : 3);
            color.Alpha = ApplyBitDepthReduction(color.Alpha, 3);
        }

        return color;
    }

    private static byte ApplyBitDepthReduction(byte input, int bitDepth)
    {
        var quantized = QuantizeToBitDepth(input, bitDepth);
        return ExpandFromBitDepth(quantized, bitDepth);
    }

    private static int QuantizeToBitDepth(byte input, int bitDepth)
    {
        var maxValue = (1 << bitDepth) - 1;
        return (input * maxValue + 127) / byte.MaxValue;
    }

    private static byte ExpandFromBitDepth(int input, int bitDepth)
    {
        var maxValue = (1 << bitDepth) - 1;
        return (byte)((input * byte.MaxValue + (maxValue >> 1)) / maxValue);
    }

    private static Rgba8UNorm GetInterpolatedColor(ReadOnlySpan<Rgba8UNorm> source, int width, int height, int wordWidth, int x, int y)
    {
        var sourceWidth = width / wordWidth;
        var sourceHeight = height / WordHeight;
        var sourceLeft = Wrap(x - (wordWidth / 2), width) / wordWidth;
        var sourceTop = Wrap(y - (WordHeight / 2), height) / WordHeight;
        var sourceRight = (sourceLeft + 1) % sourceWidth;
        var sourceBottom = (sourceTop + 1) % sourceHeight;
        var xWeight = Wrap(x + (wordWidth / 2), wordWidth);
        var yWeight = Wrap(y + (WordHeight / 2), WordHeight);

        var color00 = source[(sourceTop * sourceWidth) + sourceLeft];
        var color01 = source[(sourceTop * sourceWidth) + sourceRight];
        var color10 = source[(sourceBottom * sourceWidth) + sourceLeft];
        var color11 = source[(sourceBottom * sourceWidth) + sourceRight];

        return Interpolate4(color00, color01, color10, color11, wordWidth, xWeight, yWeight);
    }

    private static Rgba8UNorm SampleImage(ReadOnlySpan<Rgba8UNorm> image, int width, int height, int x, int y) =>
        image[(Wrap(y, height) * width) + Wrap(x, width)];

    private static Rgba8UNorm Interpolate4(Rgba8UNorm color00, Rgba8UNorm color01, Rgba8UNorm color10, Rgba8UNorm color11, int wordWidth, int px, int py)
    {
        var a = (uint)((WordHeight - py) * (wordWidth - px));
        var b = (uint)((WordHeight - py) * px);
        var c = (uint)(py * (wordWidth - px));
        var d = (uint)(py * px);
        var downscale = (uint)(wordWidth * WordHeight);
        return CreateColor(
            ((a * color00.Red) + (b * color01.Red) + (c * color10.Red) + (d * color11.Red)) / downscale,
            ((a * color00.Green) + (b * color01.Green) + (c * color10.Green) + (d * color11.Green)) / downscale,
            ((a * color00.Blue) + (b * color01.Blue) + (c * color10.Blue) + (d * color11.Blue)) / downscale,
            ((a * color00.Alpha) + (b * color01.Alpha) + (c * color10.Alpha) + (d * color11.Alpha)) / downscale);
    }

    private static void GetExtremesFast(
        ReadOnlySpan<Rgba8UNorm> image,
        int width,
        int height,
        int x0,
        int y0,
        int wordWidth,
        bool hasAlpha,
        bool scoreEndpointCandidates,
        out int indexA,
        out int indexB)
    {
        Span<uint> bestFitness = stackalloc uint[10];
        Span<int> bestIndex = stackalloc int[10];
        for (var i = 0; i < 5; i++)
        {
            bestFitness[i * 2] = uint.MaxValue;
            bestFitness[(i * 2) + 1] = 0;
        }

        for (var y = y0; y < y0 + WordHeight; y++)
        {
            for (var x = x0; x < x0 + wordWidth; x++)
            {
                var xWrapped = Wrap(x, width);
                var yWrapped = Wrap(y, height);
                var index = (yWrapped * width) + xWrapped;
                var color = image[index];
                var lightness = (uint)((77 * color.Red + 150 * color.Green + 28 * color.Blue) / 256);
                TrackExtreme(lightness, index, bestFitness, bestIndex, 0);

                for (var component = 0; component < 4; component++)
                {
                    TrackExtreme(GetChannel(color, component, hasAlpha), index, bestFitness, bestIndex, component + 1);
                }
            }
        }

        var bestPair = 0;
        var bestReversed = false;
        if (scoreEndpointCandidates)
        {
            var bestError = uint.MaxValue;
            for (var i = 0; i < 5; i++)
            {
                var lowIndex = bestIndex[i * 2];
                var highIndex = bestIndex[(i * 2) + 1];
                var error = EndpointPairError(image, width, height, x0, y0, wordWidth, hasAlpha, lowIndex, highIndex);
                if (error < bestError)
                {
                    bestError = error;
                    bestPair = i;
                    bestReversed = false;
                }

                error = EndpointPairError(image, width, height, x0, y0, wordWidth, hasAlpha, highIndex, lowIndex);
                if (error < bestError)
                {
                    bestError = error;
                    bestPair = i;
                    bestReversed = true;
                }
            }
        }
        else
        {
            var bestPairDiff = 0u;
            for (var i = 0; i < 5; i++)
            {
                var diff = ColorDiff(image[bestIndex[i * 2]], image[bestIndex[(i * 2) + 1]], hasAlpha);
                if (diff > bestPairDiff)
                {
                    bestPair = i;
                    bestPairDiff = diff;
                }
            }
        }

        indexA = bestIndex[bestPair * 2];
        indexB = bestIndex[(bestPair * 2) + 1];
        if (scoreEndpointCandidates
                ? bestReversed
                : ColorBrightnessOrder(image[indexB], hasAlpha) < ColorBrightnessOrder(image[indexA], hasAlpha))
        {
            (indexA, indexB) = (indexB, indexA);
        }
    }

    private static void TrackExtreme(uint value, int index, Span<uint> bestFitness, Span<int> bestIndex, int pair)
    {
        var low = pair * 2;
        var high = low + 1;
        if (value < bestFitness[low])
        {
            bestFitness[low] = value;
            bestIndex[low] = index;
        }

        if (value > bestFitness[high])
        {
            bestFitness[high] = value;
            bestIndex[high] = index;
        }
    }

    private static uint EndpointPairError(
        ReadOnlySpan<Rgba8UNorm> image,
        int width,
        int height,
        int x0,
        int y0,
        int wordWidth,
        bool hasAlpha,
        int indexA,
        int indexB)
    {
        var colorA = ApplyColorChannelReduction(image[indexA], isB: false, hasAlpha);
        var colorB = ApplyColorChannelReduction(image[indexB], isB: true, hasAlpha);
        return EndpointPairError(image, width, height, x0, y0, wordWidth, hasAlpha, colorA, colorB);
    }

    private static uint EndpointPairError(
        ReadOnlySpan<Rgba8UNorm> image,
        int width,
        int height,
        int x0,
        int y0,
        int wordWidth,
        bool hasAlpha,
        Rgba8UNorm colorA,
        Rgba8UNorm colorB)
    {
        var error = 0u;

        for (var y = y0; y < y0 + WordHeight; y++)
        {
            for (var x = x0; x < x0 + wordWidth; x++)
            {
                error += BestEndpointPixelError(
                    SampleImage(image, width, height, x, y),
                    colorA,
                    colorB,
                    hasAlpha);
            }
        }

        return error;
    }

    private static uint BestEndpointPixelError(Rgba8UNorm color, Rgba8UNorm colorA, Rgba8UNorm colorB, bool hasAlpha)
    {
        var bestDiff = ColorDiff(color, colorA, hasAlpha);
        for (var currentMod = 1u; currentMod < 4; currentMod++)
        {
            bestDiff = Math.Min(bestDiff, ColorDiff(color, ApplyModulation4Bpp(colorA, colorB, currentMod), hasAlpha));
        }

        return bestDiff;
    }

    private static uint BestModulation4Bpp(Rgba8UNorm color, Rgba8UNorm colorA, Rgba8UNorm colorB, bool hasAlpha)
    {
        var bestDiff = ColorDiff(color, colorA, hasAlpha);
        var bestMod = 0u;
        for (var currentMod = 1u; currentMod < 4; currentMod++)
        {
            var diff = ColorDiff(color, ApplyModulation4Bpp(colorA, colorB, currentMod), hasAlpha);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestMod = currentMod;
            }
        }

        return bestMod;
    }

    private static uint BestModulation2Bpp(Rgba8UNorm color, Rgba8UNorm colorA, Rgba8UNorm colorB, bool hasAlpha)
    {
        var bestDiff = ColorDiff(color, colorA, hasAlpha);
        var bestMod = 0u;
        for (var currentMod = 1u; currentMod < 4; currentMod++)
        {
            var diff = ColorDiff(color, ApplyModulation2Bpp(colorA, colorB, currentMod), hasAlpha);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestMod = currentMod;
            }
        }

        return bestMod;
    }

    private static Rgba8UNorm ApplyModulation4Bpp(Rgba8UNorm colorA, Rgba8UNorm colorB, uint modulation) => modulation switch
    {
        0 => colorA,
        1 => new Rgba8UNorm(
            (byte)((5 * colorA.Red + 3 * colorB.Red) >> 3),
            (byte)((5 * colorA.Green + 3 * colorB.Green) >> 3),
            (byte)((5 * colorA.Blue + 3 * colorB.Blue) >> 3),
            (byte)((5 * colorA.Alpha + 3 * colorB.Alpha) >> 3)),
        2 => new Rgba8UNorm(
            (byte)((3 * colorA.Red + 5 * colorB.Red) >> 3),
            (byte)((3 * colorA.Green + 5 * colorB.Green) >> 3),
            (byte)((3 * colorA.Blue + 5 * colorB.Blue) >> 3),
            (byte)((3 * colorA.Alpha + 5 * colorB.Alpha) >> 3)),
        _ => colorB
    };

    private static Rgba8UNorm ApplyModulation2Bpp(Rgba8UNorm colorA, Rgba8UNorm colorB, uint modulation) =>
        ApplyModulation4Bpp(colorA, colorB, modulation);

    private static uint ColorDiff(Rgba8UNorm color0, Rgba8UNorm color1, bool hasAlpha)
    {
        var delta = Abs(color0.Red - color1.Red) + Abs(color0.Green - color1.Green) + Abs(color0.Blue - color1.Blue);
        if (hasAlpha)
        {
            delta += Abs(color0.Alpha - color1.Alpha);
        }

        return delta;
    }

    private static uint ColorBrightnessOrder(Rgba8UNorm color, bool hasAlpha) =>
        (uint)color.Red + color.Green + color.Blue + GetChannel(color, 3, hasAlpha);

    private static byte GetChannel(Rgba8UNorm color, int channel, bool hasAlpha) => channel switch
    {
        0 => color.Red,
        1 => color.Green,
        2 => color.Blue,
        3 => hasAlpha ? color.Alpha : byte.MaxValue,
        _ => 0
    };

    private static float HdrLuma(float red, float green, float blue) => (red + (2f * green) + blue) * 0.25f;

    private static void ValidateHdrSource(ReadOnlySpan<Rgba32Float> source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            ValidateHdrInput(source[i].Red, nameof(Rgba32Float.Red));
            ValidateHdrInput(source[i].Green, nameof(Rgba32Float.Green));
            ValidateHdrInput(source[i].Blue, nameof(Rgba32Float.Blue));
        }
    }

    private static void BuildHdrLumaPlane(
        ReadOnlySpan<Rgba32Float> source,
        Span<Rgba8UNorm> luma)
    {
        for (var i = 0; i < luma.Length; i++)
        {
            var red = MathF.Max(source[i].Red, 0f);
            var green = MathF.Max(source[i].Green, 0f);
            var blue = MathF.Max(source[i].Blue, 0f);
            var encodedLuma = EncodeHdrLumaValue(HdrLuma(red, green, blue));
            luma[i] = new Rgba8UNorm(
                (byte)MathF.Floor(encodedLuma),
                (byte)MathF.Ceiling(encodedLuma),
                0);
        }
    }

    private static void BuildHdrChromaPlane(
        ReadOnlySpan<Rgba32Float> source,
        ReadOnlySpan<Rgba8UNorm> decodedLuma,
        Span<Rgba8UNorm> chroma)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var red = MathF.Max(source[i].Red, 0f);
            var green = MathF.Max(source[i].Green, 0f);
            var blue = MathF.Max(source[i].Blue, 0f);
            var lumaValue = DecodeHdrLumaBytes(decodedLuma[i]);
            chroma[i] = new Rgba8UNorm(
                HdrChromaToByte(red / (4f * lumaValue)),
                HdrChromaToByte(green / (2f * lumaValue)),
                HdrChromaToByte(blue / (4f * lumaValue)));
        }
    }

    private static float EncodeHdrLumaValue(float value)
    {
        var encoded = 127.5f + (8f * MathF.Log2(MathF.Max(value, HdrMinLuma)));
        return Math.Clamp(encoded, byte.MinValue, byte.MaxValue);
    }

    private static float DecodeHdrLumaBytes(Rgba8UNorm value) =>
        MathF.Pow(2f, ((value.Red + value.Green) - 255f) / 16f);

    private static byte HdrChromaToByte(float value)
    {
        if (value <= 0f)
        {
            return byte.MinValue;
        }

        var encoded = MathF.Ceiling(value * byte.MaxValue);
        return encoded >= byte.MaxValue ? byte.MaxValue : (byte)encoded;
    }

    private static Rgba32Float StorageRgba8ToLinear(Rgba8UNorm rgba, bool srgb) => srgb
        ? new Rgba32Float(
            RgbaColorConversions.Srgb8ToLinearFloat(rgba.Red),
            RgbaColorConversions.Srgb8ToLinearFloat(rgba.Green),
            RgbaColorConversions.Srgb8ToLinearFloat(rgba.Blue),
            ByteToUnit(rgba.Alpha))
        : Rgba8UNorm.ToRgba32Float(rgba);

    private static Rgba8UNorm LinearToStorageRgba8(Rgba32Float rgba, bool srgb)
    {
        if (!srgb)
        {
            return Rgba8UNorm.FromRgba32Float(rgba);
        }

        return new Rgba8UNorm(
            RgbaColorConversions.LinearFloatToSrgb8(rgba.Red),
            RgbaColorConversions.LinearFloatToSrgb8(rgba.Green),
            RgbaColorConversions.LinearFloatToSrgb8(rgba.Blue),
            UnitToByte(rgba.Alpha));
    }

    private static Rgba32Float WithAlpha(Rgba32Float value, float alpha) =>
        new(value.Red, value.Green, value.Blue, alpha);

    private static float ByteToUnit(byte value) =>
        RgbaColorConversions.UNorm8ToFloat(value);

    private static byte UnitToByte(float value) =>
        RgbaColorConversions.FloatToUNorm8(value);

    private static Rgba8UNorm CreateColor(uint red, uint green, uint blue, uint alpha) =>
        new(ToByte(red), ToByte(green), ToByte(blue), ToByte(alpha));

    private static byte ToByte(uint value) => value >= byte.MaxValue ? byte.MaxValue : (byte)value;

    private static void DecodeSrgbColors(Span<Rgba8UNorm> pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i].Red = DecodeSrgb(pixels[i].Red);
            pixels[i].Green = DecodeSrgb(pixels[i].Green);
            pixels[i].Blue = DecodeSrgb(pixels[i].Blue);
        }
    }

    private static byte DecodeSrgb(byte value) =>
        RgbaColorConversions.Srgb8ToLinearUNorm8(value);

    private static byte EncodeSrgb(byte value) =>
        RgbaColorConversions.LinearUNorm8ToSrgb8(value);

    private static void ValidateHdrInput(float value, string channel)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"PVRTC HDR expects finite non-negative input; channel {channel} must be in the [0, +inf) range.");
        }
    }

    private static TextureFormat GetLumaFormat(PvrtcFormatInfo info) => info.VersionII
        ? TextureFormats.RgbaPvrtcII4BppUNorm
        : TextureFormats.RgbPvrtcI4BppUNorm;

    private static TextureFormat GetChromaFormat(PvrtcFormatInfo info)
    {
        if (info.VersionII)
        {
            return info.ChromaBitsPerPixel == 2
                ? TextureFormats.RgbaPvrtcII2BppUNorm
                : TextureFormats.RgbaPvrtcII4BppUNorm;
        }

        return info.ChromaBitsPerPixel == 2
            ? TextureFormats.RgbPvrtcI2BppUNorm
            : TextureFormats.RgbPvrtcI4BppUNorm;
    }

    private static void FromZOrder(int z, int width, int height, bool versionII, out int x, out int y)
    {
        if (versionII)
        {
            y = z / width;
            x = z % width;
            return;
        }

        var minB = Math.Min(width, height);
        var addTimes = z / (minB * minB);
        var readZ = z & ((minB * minB) - 1);
        x = 0;
        y = 0;
        for (var j = 0; j < 16; j++)
        {
            x |= ((readZ >> (j * 2 + 1)) & 1) << j;
            y |= ((readZ >> (j * 2)) & 1) << j;
        }

        if (width > height)
        {
            x += addTimes * minB;
        }
        else if (width < height)
        {
            y += addTimes * minB;
        }
    }

    private static int GetStorageIndex(int xSize, int ySize, int xPos, int yPos, bool versionII)
    {
        if (versionII)
        {
            return (yPos * xSize) + xPos;
        }

        var minDimension = xSize;
        var maxValue = yPos;
        var twiddled = 0;
        var sourceBitPosition = 1;
        var destinationBitPosition = 1;
        var shiftCount = 0;
        if (ySize < xSize)
        {
            minDimension = ySize;
            maxValue = xPos;
        }

        while (sourceBitPosition < minDimension)
        {
            if ((yPos & sourceBitPosition) != 0)
            {
                twiddled |= destinationBitPosition;
            }

            if ((xPos & sourceBitPosition) != 0)
            {
                twiddled |= destinationBitPosition << 1;
            }

            sourceBitPosition <<= 1;
            destinationBitPosition <<= 2;
            shiftCount++;
        }

        maxValue >>= shiftCount;
        twiddled |= maxValue << (shiftCount << 1);
        return twiddled;
    }

    private static int WrapWordIndex(int wordCount, int word) => (word + wordCount) % wordCount;

    private static int Wrap(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static uint Abs(int value) => value <= 0 ? (uint)-value : (uint)value;

    private static uint GetMask(int bitCount) => (1u << bitCount) - 1;

    private static void SetBits(int startBit, int bitCount, int value, ref uint bits)
    {
        var mask = GetMask(bitCount);
        var unsignedValue = (uint)value & mask;
        bits = (bits & ~(mask << startBit)) | (unsignedValue << startBit);
    }

    private static void SetBits(int startBit, int bitCount, byte value, ref uint bits) =>
        SetBits(startBit, bitCount, (int)value, ref bits);

    private static uint WithBits(int startBit, int bitCount, int value, uint bits)
    {
        SetBits(startBit, bitCount, value, ref bits);
        return bits;
    }

    private static int GetBits(uint bits, int startBit, int bitCount) =>
        (int)((bits >> startBit) & GetMask(bitCount));

    private static int GetWordWidth(byte bitsPerPixel) => bitsPerPixel == 2 ? 8 : 4;

    private static int GetWordCount(PvrtcFormatInfo info, int width, int height) =>
        checked((width / GetWordWidth(info.BitsPerPixel)) * (height / WordHeight));

    private static int GetEncodedByteCount(PvrtcFormatInfo info, int width, int height)
    {
        if (!info.IsHdr)
        {
            return GetPvrtcByteCount(info, width, height, info.BitsPerPixel);
        }

        return checked(
            GetPvrtcByteCount(info, width, height, 4) +
            GetPvrtcByteCount(info, width, height, info.ChromaBitsPerPixel));
    }

    private static int GetPvrtcByteCount(PvrtcFormatInfo info, int width, int height, byte bitsPerPixel)
    {
        var storageExtent = GetStorageExtent(info, width, height, bitsPerPixel);
        return checked((int)(((long)storageExtent.Width * storageExtent.Height * bitsPerPixel) / 8));
    }

    private static StorageExtent GetStorageExtent(PvrtcFormatInfo info, int width, int height, byte bitsPerPixel)
    {
        var wordWidth = GetWordWidth(bitsPerPixel);
        var minWordCount = info.VersionII ? 1 : 2;
        var storageWidth = info.VersionII ? RoundUp(width, wordWidth) : width;
        var storageHeight = info.VersionII ? RoundUp(height, WordHeight) : height;

        return new StorageExtent(
            Math.Max(storageWidth, wordWidth * minWordCount),
            Math.Max(storageHeight, WordHeight * minWordCount));
    }

    private static int RoundUp(int value, int alignment) =>
        checked(((value + alignment - 1) / alignment) * alignment);

    private static PvrtcFormatInfo GetFormatInfo(TextureFormat format)
    {
        if (format == TextureFormats.RgbPvrtcI2BppUNorm || format == TextureFormats.RgbPvrtcI2BppSrgb)
        {
            return new PvrtcFormatInfo(2, VersionII: false, HasAlpha: false, IsHdr: false, ChromaBitsPerPixel: 0);
        }

        if (format == TextureFormats.RgbaPvrtcI2BppUNorm || format == TextureFormats.RgbaPvrtcI2BppSrgb)
        {
            return new PvrtcFormatInfo(2, VersionII: false, HasAlpha: true, IsHdr: false, ChromaBitsPerPixel: 0);
        }

        if (format == TextureFormats.RgbPvrtcI4BppUNorm || format == TextureFormats.RgbPvrtcI4BppSrgb)
        {
            return new PvrtcFormatInfo(4, VersionII: false, HasAlpha: false, IsHdr: false, ChromaBitsPerPixel: 0);
        }

        if (format == TextureFormats.RgbaPvrtcI4BppUNorm || format == TextureFormats.RgbaPvrtcI4BppSrgb)
        {
            return new PvrtcFormatInfo(4, VersionII: false, HasAlpha: true, IsHdr: false, ChromaBitsPerPixel: 0);
        }

        if (format == TextureFormats.RgbaPvrtcII2BppUNorm || format == TextureFormats.RgbaPvrtcII2BppSrgb)
        {
            return new PvrtcFormatInfo(2, VersionII: true, HasAlpha: true, IsHdr: false, ChromaBitsPerPixel: 0);
        }

        if (format == TextureFormats.RgbaPvrtcII4BppUNorm || format == TextureFormats.RgbaPvrtcII4BppSrgb)
        {
            return new PvrtcFormatInfo(4, VersionII: true, HasAlpha: true, IsHdr: false, ChromaBitsPerPixel: 0);
        }

        if (format == TextureFormats.RgbPvrtcI6BppFloat)
        {
            return new PvrtcFormatInfo(6, VersionII: false, HasAlpha: false, IsHdr: true, ChromaBitsPerPixel: 2);
        }

        if (format == TextureFormats.RgbPvrtcI8BppFloat)
        {
            return new PvrtcFormatInfo(8, VersionII: false, HasAlpha: false, IsHdr: true, ChromaBitsPerPixel: 4);
        }

        if (format == TextureFormats.RgbPvrtcII6BppFloat)
        {
            return new PvrtcFormatInfo(6, VersionII: true, HasAlpha: false, IsHdr: true, ChromaBitsPerPixel: 2);
        }

        if (format == TextureFormats.RgbPvrtcII8BppFloat)
        {
            return new PvrtcFormatInfo(8, VersionII: true, HasAlpha: false, IsHdr: true, ChromaBitsPerPixel: 4);
        }

        throw CreateUnsupportedFormatException(format);
    }

    private static void ValidateDimensions(PvrtcFormatInfo info, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (!info.VersionII && (!IsPowerOfTwo(width) || !IsPowerOfTwo(height)))
        {
            throw new ArgumentException("PVRTC I textures must have power-of-two dimensions.");
        }
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static void ValidateSourceLength(TextureFormat format, int width, int height, ReadOnlySpan<byte> source)
    {
        var requiredBytes = GetEncodedByteCount(format, width, height);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded texture.", nameof(source));
        }
    }

    private static void ValidateDestinationLength(TextureFormat format, int width, int height, Span<byte> destination)
    {
        var requiredBytes = GetEncodedByteCount(format, width, height);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded texture.", nameof(destination));
        }
    }

    private static void ValidateTexelSpan(int width, int height, int length, string parameterName)
    {
        var texelCount = checked(width * height);
        if (length < texelCount)
        {
            throw new ArgumentException("Pixel span is too small for the texture dimensions.", parameterName);
        }
    }

    private static void ValidateUNormInput(float value, TextureFormat format, string channel)
    {
        if (float.IsNaN(value) || value < 0f || value > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Texture format '{format.Name}' expects normalized LDR input; channel {channel} must be in the [0, 1] range.");
        }
    }

    private static bool IsSrgb(TextureFormat format) => format.ValueKind == TextureValueKind.Srgb;

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"PVRTC texture codec does not support texture format '{format.Name}'.");

    private static int GridIndex(int x, int y) => (y * ModulationGridWidth) + x;

    private static int GetGrid(ref IntGrid grid, int x, int y) => grid[GridIndex(x, y)];

    private static void SetGrid(ref IntGrid grid, int x, int y, int value) => grid[GridIndex(x, y)] = value;

    private readonly record struct PvrtcFormatInfo(
        byte BitsPerPixel,
        bool VersionII,
        bool HasAlpha,
        bool IsHdr,
        byte ChromaBitsPerPixel);

    private readonly record struct StorageExtent(int Width, int Height);

    private readonly record struct PvrtcWord(uint ModulationData, uint ColorData);

    private readonly record struct WordCoordinate(int X, int Y);

    private readonly record struct WordIndices(WordCoordinate P, WordCoordinate Q, WordCoordinate R, WordCoordinate S);

    private readonly record struct EndpointField(int StartBit, int BitCount);

    private enum ModulationMode2Bpp
    {
        Direct1Bpp,
        Interpolated2Bpp,
        VerticallyInterpolated2Bpp,
        HorizontallyInterpolated2Bpp
    }

    private readonly record struct Pixel128(int Red, int Green, int Blue, int Alpha)
    {
        public static Pixel128 operator +(Pixel128 left, Pixel128 right) => new(
            left.Red + right.Red,
            left.Green + right.Green,
            left.Blue + right.Blue,
            left.Alpha + right.Alpha);

        public static Pixel128 operator -(Pixel128 left, Pixel128 right) => new(
            left.Red - right.Red,
            left.Green - right.Green,
            left.Blue - right.Blue,
            left.Alpha - right.Alpha);

        public static Pixel128 operator *(Pixel128 value, int scale) => new(
            value.Red * scale,
            value.Green * scale,
            value.Blue * scale,
            value.Alpha * scale);

        public Rgba8UNorm ToColor() => new(ClampByte(Red), ClampByte(Green), ClampByte(Blue), ClampByte(Alpha));

        private static byte ClampByte(int value)
        {
            if (value <= byte.MinValue)
            {
                return byte.MinValue;
            }

            return value >= byte.MaxValue ? byte.MaxValue : (byte)value;
        }
    }

    [InlineArray(ModulationGridWidth * ModulationGridHeight)]
    private struct IntGrid
    {
        private int _element0;
    }

    [InlineArray(32)]
    private struct PixelBlock
    {
        private Pixel128 _element0;
    }

    [InlineArray(32)]
    private struct ColorBlock
    {
        private Rgba8UNorm _element0;
    }
}
