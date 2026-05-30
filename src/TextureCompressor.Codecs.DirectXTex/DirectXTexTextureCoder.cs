using System.Runtime.InteropServices;
using Hexa.NET.DirectXTex;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using DxTex = Hexa.NET.DirectXTex.DirectXTex;

namespace TextureCompressor.Codecs.DirectXTex;

public sealed unsafe class DirectXTexTextureCoder : IPitchTextureCoder
{
    private static readonly FormatMapping[] SMappings =
    [
        new(TextureFormats.Bc1Rgb, DxgiFormat.BC1UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc1Rgba, DxgiFormat.BC1UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc1RgbSrgb, DxgiFormat.BC1UNormSrgb, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Bc1RgbaSrgb, DxgiFormat.BC1UNormSrgb, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt1Rgb, DxgiFormat.BC1UNorm, SourceKind.Rgba8),
        new(TextureFormats.Dxt1Rgba, DxgiFormat.BC1UNorm, SourceKind.Rgba8),
        new(TextureFormats.Dxt1RgbSrgb, DxgiFormat.BC1UNormSrgb, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt1RgbaSrgb, DxgiFormat.BC1UNormSrgb, SourceKind.Rgba8, IsSrgb: true),

        new(TextureFormats.Bc2Rgba, DxgiFormat.BC2UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc2RgbaSrgb, DxgiFormat.BC2UNormSrgb, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt2Rgba, DxgiFormat.BC2UNorm, SourceKind.Rgba8),
        new(TextureFormats.Dxt3Rgba, DxgiFormat.BC2UNorm, SourceKind.Rgba8),
        new(TextureFormats.Dxt3RgbaSrgb, DxgiFormat.BC2UNormSrgb, SourceKind.Rgba8, IsSrgb: true),

        new(TextureFormats.Bc3Rgba, DxgiFormat.BC3UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc3RgbaSrgb, DxgiFormat.BC3UNormSrgb, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.Dxt4Rgba, DxgiFormat.BC3UNorm, SourceKind.Rgba8),
        new(TextureFormats.Dxt5Rgba, DxgiFormat.BC3UNorm, SourceKind.Rgba8),
        new(TextureFormats.Dxt5RgbaSrgb, DxgiFormat.BC3UNormSrgb, SourceKind.Rgba8, IsSrgb: true),

        new(TextureFormats.Bc4UNorm, DxgiFormat.BC4UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc4SNorm, DxgiFormat.BC4SNorm, SourceKind.Rgba8SNorm),
        new(TextureFormats.Rgtc1UNorm, DxgiFormat.BC4UNorm, SourceKind.Rgba8),
        new(TextureFormats.Rgtc1SNorm, DxgiFormat.BC4SNorm, SourceKind.Rgba8SNorm),
        new(TextureFormats.Ati1UNorm, DxgiFormat.BC4UNorm, SourceKind.Rgba8),
        new(TextureFormats.Ati1SNorm, DxgiFormat.BC4SNorm, SourceKind.Rgba8SNorm),

        new(TextureFormats.Bc5UNorm, DxgiFormat.BC5UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc5SNorm, DxgiFormat.BC5SNorm, SourceKind.Rgba8SNorm),
        new(TextureFormats.Rgtc2UNorm, DxgiFormat.BC5UNorm, SourceKind.Rgba8),
        new(TextureFormats.Rgtc2SNorm, DxgiFormat.BC5SNorm, SourceKind.Rgba8SNorm),
        new(TextureFormats.Ati2UNorm, DxgiFormat.BC5UNorm, SourceKind.Rgba8),
        new(TextureFormats.Ati2SNorm, DxgiFormat.BC5SNorm, SourceKind.Rgba8SNorm),

        new(TextureFormats.Bc6HUFloat, DxgiFormat.BC6HUFloat16, SourceKind.Rgba32Float),
        new(TextureFormats.Bc6HSFloat, DxgiFormat.BC6HSFloat16, SourceKind.Rgba32Float),
        new(TextureFormats.RgbBptcUFloat, DxgiFormat.BC6HUFloat16, SourceKind.Rgba32Float),
        new(TextureFormats.RgbBptcSFloat, DxgiFormat.BC6HSFloat16, SourceKind.Rgba32Float),
        new(TextureFormats.Bc7UNorm, DxgiFormat.BC7UNorm, SourceKind.Rgba8),
        new(TextureFormats.Bc7Srgb, DxgiFormat.BC7UNormSrgb, SourceKind.Rgba8, IsSrgb: true),
        new(TextureFormats.RgbaBptcUNorm, DxgiFormat.BC7UNorm, SourceKind.Rgba8),
        new(TextureFormats.RgbaBptcSrgb, DxgiFormat.BC7UNormSrgb, SourceKind.Rgba8, IsSrgb: true)
    ];

    private static readonly TextureFormat[] SSupportedFormats = SMappings.Select(static mapping => mapping.Format).ToArray();

    private readonly FormatMapping _mapping;
    private readonly DirectXTexCoderOptions _options;

    public DirectXTexTextureCoder(TextureFormat format, DirectXTexCoderOptions? options = null)
    {
        if (!TryGetMapping(format, out _mapping))
        {
            throw new NotSupportedException($"DirectXTex does not have a mapped coder for texture format '{format.Name}'.");
        }

        Format = format;
        _options = options ?? new DirectXTexCoderOptions();
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
        var packed = CopyToPackedRows(source, destination.Width, destination.Height, rowPitch);
        fixed (byte* sourcePtr = packed)
        {
            var sourceImage = new Image
            {
                Width = checked((nuint)destination.Width),
                Height = checked((nuint)destination.Height),
                Format = _mapping.DxgiFormat,
                RowPitch = checked((nuint)Format.GetRowByteCount(destination.Width)),
                SlicePitch = checked((nuint)packed.Length),
                Pixels = sourcePtr
            };

            var scratch = DxTex.CreateScratchImage();
            try
            {
                Check(DxTex.Decompress(ref sourceImage, GetSourceDxgiFormat(_mapping.SourceKind), ref scratch));
                var image = GetOnlyImage(scratch);
                CopyImageToBitmap(image, destination);
            }
            finally
            {
                DxTex.ScratchImageRelease(scratch);
            }
        }
    }

    public void Encode<TPixel>(BitmapView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        var packed = EncodePacked(source);
        CopyPackedRowsToDestination(packed, source.Width, source.Height, destination, rowPitch);
    }

    private byte[] EncodePacked<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_mapping.SourceKind)
        {
            case SourceKind.Rgba8:
                return EncodeRgba8(source);
            case SourceKind.Rgba8SNorm:
                return EncodeRgba8SNorm(source);
            case SourceKind.Rgba32Float:
                return EncodeRgba32Float(source);
            default:
                throw new InvalidOperationException($"Unsupported DirectXTex source kind '{_mapping.SourceKind}'.");
        }
    }

    private byte[] EncodeRgba8<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new Rgba8UNorm[checked(source.Width * source.Height)];
        for (var i = 0; i < rgba.Length; i++)
        {
            var pixel = TPixel.ToRgba8UNorm(source.Pixels[i]);
            rgba[i] = _mapping.IsSrgb ? EncodeSrgb(pixel) : pixel;
        }

        return Compress(source.Width, source.Height, DxgiFormat.R8G8B8A8UNorm, rgba);
    }

    private byte[] EncodeRgba8SNorm<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new Rgba8SNorm[checked(source.Width * source.Height)];
        for (var i = 0; i < rgba.Length; i++)
        {
            rgba[i] = TPixel.ToRgba8SNorm(source.Pixels[i]);
        }

        return Compress(source.Width, source.Height, DxgiFormat.R8G8B8A8SNorm, rgba);
    }

    private byte[] EncodeRgba32Float<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rgba = new Rgba32Float[checked(source.Width * source.Height)];
        for (var i = 0; i < rgba.Length; i++)
        {
            rgba[i] = TPixel.ToRgba32Float(source.Pixels[i]);
        }

        return Compress(source.Width, source.Height, DxgiFormat.R32G32B32A32Float, rgba);
    }

    private byte[] Compress<TSource>(int width, int height, int sourceDxgiFormat, TSource[] source)
        where TSource : unmanaged
    {
        fixed (TSource* sourcePtr = source)
        {
            var sourceBytes = MemoryMarshal.AsBytes(source.AsSpan());
            var sourceImage = new Image
            {
                Width = checked((nuint)width),
                Height = checked((nuint)height),
                Format = sourceDxgiFormat,
                RowPitch = checked((nuint)(sourceBytes.Length / height)),
                SlicePitch = checked((nuint)sourceBytes.Length),
                Pixels = (byte*)sourcePtr
            };

            var scratch = DxTex.CreateScratchImage();
            try
            {
                Check(DxTex.Compress(ref sourceImage, _mapping.DxgiFormat, _options.Flags, _options.AlphaWeight, ref scratch));
                var image = GetOnlyImage(scratch);
                var length = checked((int)image->SlicePitch);
                var result = new byte[length];
                new ReadOnlySpan<byte>(image->Pixels, length).CopyTo(result);
                return result;
            }
            finally
            {
                DxTex.ScratchImageRelease(scratch);
            }
        }
    }

    private void CopyImageToBitmap<TPixel>(Image* image, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (image->Width != (nuint)destination.Width || image->Height != (nuint)destination.Height)
        {
            throw new InvalidOperationException("DirectXTex returned an image with unexpected dimensions.");
        }

        switch (_mapping.SourceKind)
        {
            case SourceKind.Rgba8:
                CopyRgba8ImageToBitmap(image, destination);
                return;
            case SourceKind.Rgba8SNorm:
                CopyRgba8SNormImageToBitmap(image, destination);
                return;
            case SourceKind.Rgba32Float:
                CopyRgba32FloatImageToBitmap(image, destination);
                return;
            default:
                throw new InvalidOperationException($"Unsupported DirectXTex source kind '{_mapping.SourceKind}'.");
        }
    }

    private void CopyRgba8ImageToBitmap<TPixel>(Image* image, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var y = 0; y < destination.Height; y++)
        {
            var row = new ReadOnlySpan<Rgba8UNorm>(image->Pixels + checked((int)image->RowPitch * y), destination.Width);
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                var pixel = _mapping.IsSrgb ? DecodeSrgb(row[x]) : row[x];
                destinationRow[x] = TPixel.FromRgba8UNorm(pixel);
            }
        }
    }

    private static void CopyRgba8SNormImageToBitmap<TPixel>(Image* image, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var y = 0; y < destination.Height; y++)
        {
            var row = new ReadOnlySpan<Rgba8SNorm>(image->Pixels + checked((int)image->RowPitch * y), destination.Width);
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                destinationRow[x] = TPixel.FromRgba8SNorm(row[x]);
            }
        }
    }

    private static void CopyRgba32FloatImageToBitmap<TPixel>(Image* image, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (var y = 0; y < destination.Height; y++)
        {
            var row = new ReadOnlySpan<Rgba32Float>(image->Pixels + checked((int)image->RowPitch * y), destination.Width);
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                destinationRow[x] = TPixel.FromRgba32Float(row[x]);
            }
        }
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

        var required = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination span is too small for the texture dimensions and row pitch.", nameof(destination));
        }
    }

    private int GetBlockRowCount(int width, int height) =>
        checked(Format.GetByteCount(width, height) / Format.GetRowByteCount(width));

    private static Image* GetOnlyImage(ScratchImage scratch)
    {
        var image = DxTex.GetImage(scratch, 0, 0, 0);
        if (image is null || image->Pixels is null)
        {
            throw new InvalidOperationException("DirectXTex returned an empty texture payload.");
        }

        return image;
    }

    private static void Check(HexaGen.Runtime.HResult result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"DirectXTex failed with HRESULT 0x{result.Value:x8}.");
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

    private static int GetSourceDxgiFormat(SourceKind sourceKind) => sourceKind switch
    {
        SourceKind.Rgba8 => DxgiFormat.R8G8B8A8UNorm,
        SourceKind.Rgba8SNorm => DxgiFormat.R8G8B8A8SNorm,
        SourceKind.Rgba32Float => DxgiFormat.R32G32B32A32Float,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null)
    };

    private static Rgba8UNorm EncodeSrgb(Rgba8UNorm pixel) =>
        new(
            RgbaColorConversions.LinearUNorm8ToSrgb8(pixel.Red),
            RgbaColorConversions.LinearUNorm8ToSrgb8(pixel.Green),
            RgbaColorConversions.LinearUNorm8ToSrgb8(pixel.Blue),
            pixel.Alpha);

    private static Rgba8UNorm DecodeSrgb(Rgba8UNorm pixel) =>
        new(
            RgbaColorConversions.Srgb8ToLinearUNorm8(pixel.Red),
            RgbaColorConversions.Srgb8ToLinearUNorm8(pixel.Green),
            RgbaColorConversions.Srgb8ToLinearUNorm8(pixel.Blue),
            pixel.Alpha);

    private readonly record struct FormatMapping(
        TextureFormat Format,
        int DxgiFormat,
        SourceKind SourceKind,
        bool IsSrgb = false);

    private enum SourceKind
    {
        Rgba8,
        Rgba8SNorm,
        Rgba32Float
    }

    private static class DxgiFormat
    {
        public const int R32G32B32A32Float = 2;
        public const int R8G8B8A8UNorm = 28;
        public const int R8G8B8A8SNorm = 31;
        public const int BC1UNorm = 71;
        public const int BC1UNormSrgb = 72;
        public const int BC2UNorm = 74;
        public const int BC2UNormSrgb = 75;
        public const int BC3UNorm = 77;
        public const int BC3UNormSrgb = 78;
        public const int BC4UNorm = 80;
        public const int BC4SNorm = 81;
        public const int BC5UNorm = 83;
        public const int BC5SNorm = 84;
        public const int BC6HUFloat16 = 95;
        public const int BC6HSFloat16 = 96;
        public const int BC7UNorm = 98;
        public const int BC7UNormSrgb = 99;
    }
}
