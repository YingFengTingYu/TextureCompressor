using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class DepthStencilTextureCoder : IPitchTextureCoder
{
    private readonly DepthStencilTransfer _transfer;

    public DepthStencilTextureCoder(TextureFormat format)
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

        if (_transfer is DepthStencilTransfer.StencilIndex1 or DepthStencilTransfer.StencilIndex4)
        {
            DecodePackedStencil(source, destination, rowPitch);
            return;
        }

        DecodeTexels(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);

        if (_transfer is DepthStencilTransfer.StencilIndex1 or DepthStencilTransfer.StencilIndex4)
        {
            EncodePackedStencil(source, destination, rowPitch);
            return;
        }

        EncodeTexels(source, destination, rowPitch);
    }

    public int GetRequiredByteCount(int width, int height, int? rowPitch = null)
    {
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return checked(resolvedRowPitch * height);
    }

    public void EncodeDepth(
        ReadOnlySpan<float> depth,
        int width,
        int height,
        Span<byte> destination,
        int? rowPitch = null)
    {
        ValidateDepthFormat();
        ValidateTexelSpan(depth, width, height, nameof(depth));
        ValidateDestinationLength(width, height, destination, rowPitch);

        var bytesPerTexel = Format.BytesPerBlock;
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        var sourceIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var texelOffset = rowOffset;
            for (var x = 0; x < width; x++)
            {
                EncodeDepthTexel(depth[sourceIndex], destination.Slice(texelOffset, bytesPerTexel));
                sourceIndex++;
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + resolvedRowPitch);
        }
    }

    public void DecodeDepth(
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<float> depth,
        int? rowPitch = null)
    {
        ValidateDepthFormat();
        ValidateSourceLength(width, height, source, rowPitch);
        ValidateTexelSpan(depth, width, height, nameof(depth));

        var bytesPerTexel = Format.BytesPerBlock;
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        var destinationIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var texelOffset = rowOffset;
            for (var x = 0; x < width; x++)
            {
                depth[destinationIndex] = DecodeDepthTexel(source.Slice(texelOffset, bytesPerTexel));
                destinationIndex++;
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + resolvedRowPitch);
        }
    }

    public void EncodeStencil(
        ReadOnlySpan<uint> stencil,
        int width,
        int height,
        Span<byte> destination,
        int? rowPitch = null)
    {
        ValidateStencilFormat();
        ValidateTexelSpan(stencil, width, height, nameof(stencil));
        ValidateDestinationLength(width, height, destination, rowPitch);

        if (_transfer is DepthStencilTransfer.StencilIndex1 or DepthStencilTransfer.StencilIndex4)
        {
            EncodePackedStencil(stencil, width, height, destination, ResolveRowPitch(width, rowPitch));
            return;
        }

        var bytesPerTexel = Format.BytesPerBlock;
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        var sourceIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var texelOffset = rowOffset;
            for (var x = 0; x < width; x++)
            {
                EncodeStencilTexel(Format.RedBits, stencil[sourceIndex], destination.Slice(texelOffset, bytesPerTexel));
                sourceIndex++;
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + resolvedRowPitch);
        }
    }

    public void DecodeStencil(
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<uint> stencil,
        int? rowPitch = null)
    {
        ValidateStencilFormat();
        ValidateSourceLength(width, height, source, rowPitch);
        ValidateTexelSpan(stencil, width, height, nameof(stencil));

        if (_transfer is DepthStencilTransfer.StencilIndex1 or DepthStencilTransfer.StencilIndex4)
        {
            DecodePackedStencil(width, height, source, stencil, ResolveRowPitch(width, rowPitch));
            return;
        }

        var bytesPerTexel = Format.BytesPerBlock;
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        var destinationIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var texelOffset = rowOffset;
            for (var x = 0; x < width; x++)
            {
                stencil[destinationIndex] = DecodeStencilTexel(Format.RedBits, source.Slice(texelOffset, bytesPerTexel));
                destinationIndex++;
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + resolvedRowPitch);
        }
    }

    public void EncodeDepthStencil(
        ReadOnlySpan<float> depth,
        ReadOnlySpan<uint> stencil,
        int width,
        int height,
        Span<byte> destination,
        int? rowPitch = null)
    {
        ValidateDepthStencilFormat();
        ValidateTexelSpan(depth, width, height, nameof(depth));
        ValidateTexelSpan(stencil, width, height, nameof(stencil));
        ValidateDestinationLength(width, height, destination, rowPitch);

        var bytesPerTexel = Format.BytesPerBlock;
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        var sourceIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var texelOffset = rowOffset;
            for (var x = 0; x < width; x++)
            {
                EncodeDepthStencilTexel(depth[sourceIndex], stencil[sourceIndex], destination.Slice(texelOffset, bytesPerTexel));
                sourceIndex++;
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + resolvedRowPitch);
        }
    }

    public void DecodeDepthStencil(
        int width,
        int height,
        ReadOnlySpan<byte> source,
        Span<float> depth,
        Span<uint> stencil,
        int? rowPitch = null)
    {
        ValidateDepthStencilFormat();
        ValidateSourceLength(width, height, source, rowPitch);
        ValidateTexelSpan(depth, width, height, nameof(depth));
        ValidateTexelSpan(stencil, width, height, nameof(stencil));

        var bytesPerTexel = Format.BytesPerBlock;
        var resolvedRowPitch = ResolveRowPitch(width, rowPitch);
        var destinationIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var texelOffset = rowOffset;
            for (var x = 0; x < width; x++)
            {
                DecodeDepthStencilTexel(source.Slice(texelOffset, bytesPerTexel), out depth[destinationIndex], out stencil[destinationIndex]);
                destinationIndex++;
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + resolvedRowPitch);
        }
    }

    private void DecodeTexels<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case DepthStencilTransfer.DepthComponent8:
                DecodeTexels<TPixel, DepthComponent8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent16:
                DecodeTexels<TPixel, DepthComponent16Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent24:
                DecodeTexels<TPixel, DepthComponent24Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent32:
                DecodeTexels<TPixel, DepthComponent32Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent32Float:
                DecodeTexels<TPixel, DepthComponent32FloatTransfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.StencilIndex8:
                DecodeTexels<TPixel, StencilIndex8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.StencilIndex16:
                DecodeTexels<TPixel, StencilIndex16Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth16Stencil8:
                DecodeTexels<TPixel, Depth16Stencil8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth24Stencil8:
                DecodeTexels<TPixel, Depth24Stencil8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth32Stencil8:
                DecodeTexels<TPixel, Depth32Stencil8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth32FloatStencil8:
                DecodeTexels<TPixel, Depth32FloatStencil8Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void EncodeTexels<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (_transfer)
        {
            case DepthStencilTransfer.DepthComponent8:
                EncodeTexels<TPixel, DepthComponent8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent16:
                EncodeTexels<TPixel, DepthComponent16Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent24:
                EncodeTexels<TPixel, DepthComponent24Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent32:
                EncodeTexels<TPixel, DepthComponent32Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.DepthComponent32Float:
                EncodeTexels<TPixel, DepthComponent32FloatTransfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.StencilIndex8:
                EncodeTexels<TPixel, StencilIndex8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.StencilIndex16:
                EncodeTexels<TPixel, StencilIndex16Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth16Stencil8:
                EncodeTexels<TPixel, Depth16Stencil8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth24Stencil8:
                EncodeTexels<TPixel, Depth24Stencil8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth32Stencil8:
                EncodeTexels<TPixel, Depth32Stencil8Transfer>(source, destination, rowPitch);
                return;
            case DepthStencilTransfer.Depth32FloatStencil8:
                EncodeTexels<TPixel, Depth32FloatStencil8Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void DecodeTexels<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IDepthStencilPixelTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                destinationRow[x] = TPixel.FromRgba32Float(TTransfer.Decode(source.Slice(texelOffset, bytesPerTexel)));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeTexels<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IDepthStencilPixelTransfer
    {
        var bytesPerTexel = TTransfer.BytesPerTexel;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                TTransfer.Encode(TPixel.ToRgba32Float(sourceRow[x]), destination.Slice(texelOffset, bytesPerTexel));
                texelOffset = checked(texelOffset + bytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IDepthStencilPixelTransfer
    {
        static abstract int BytesPerTexel { get; }

        static abstract Rgba32Float Decode(ReadOnlySpan<byte> source);

        static abstract void Encode(Rgba32Float source, Span<byte> destination);
    }

    private readonly struct DepthComponent8Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 1;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(UNormToFloat(ReadUnsignedLittleEndian(source), 8), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            WriteUnsignedLittleEndian(destination, FloatToUNorm(source.Red, 8));
    }

    private readonly struct DepthComponent16Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 2;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(UNormToFloat(ReadUnsignedLittleEndian(source), 16), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            WriteUnsignedLittleEndian(destination, FloatToUNorm(source.Red, 16));
    }

    private readonly struct DepthComponent24Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 3;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(UNormToFloat(ReadUnsignedLittleEndian(source), 24), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            WriteUnsignedLittleEndian(destination, FloatToUNorm(source.Red, 24));
    }

    private readonly struct DepthComponent32Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(UNormToFloat(ReadUnsignedLittleEndian(source), 32), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            WriteUnsignedLittleEndian(destination, FloatToUNorm(source.Red, 32));
    }

    private readonly struct DepthComponent32FloatTransfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source)), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(source.Red));
    }

    private readonly struct StencilIndex8Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 1;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(UNormToFloat(DecodeStencilTexel(8, source), 8), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            EncodeStencilTexel(8, FloatToUNorm(source.Red, 8), destination);
    }

    private readonly struct StencilIndex16Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 2;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source) =>
            new(UNormToFloat(DecodeStencilTexel(16, source), 16), 0f, 0f);

        public static void Encode(Rgba32Float source, Span<byte> destination) =>
            EncodeStencilTexel(16, FloatToUNorm(source.Red, 16), destination);
    }

    private readonly struct Depth16Stencil8Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 3;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source)
        {
            var packed = ReadUnsignedLittleEndian(source);
            return new Rgba32Float(UNormToFloat(packed >> 8, 16), UNormToFloat(packed & 0xffu, 8), 0f);
        }

        public static void Encode(Rgba32Float source, Span<byte> destination)
        {
            var packed = (FloatToUNorm(source.Red, 16) << 8) | FloatToUNorm(source.Green, 8);
            WriteUnsignedLittleEndian(destination, packed);
        }
    }

    private readonly struct Depth24Stencil8Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 4;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(source);
            return new Rgba32Float(UNormToFloat(packed >> 8, 24), UNormToFloat(packed & 0xffu, 8), 0f);
        }

        public static void Encode(Rgba32Float source, Span<byte> destination)
        {
            var packed = (FloatToUNorm(source.Red, 24) << 8) | FloatToUNorm(source.Green, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, packed);
        }
    }

    private readonly struct Depth32Stencil8Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 5;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source)
        {
            var packed = ReadUnsignedLongLittleEndian(source);
            return new Rgba32Float(UNormToFloat((uint)(packed >> 8), 32), UNormToFloat((uint)(packed & 0xffu), 8), 0f);
        }

        public static void Encode(Rgba32Float source, Span<byte> destination)
        {
            var packed = ((ulong)FloatToUNorm(source.Red, 32) << 8) | FloatToUNorm(source.Green, 8);
            WriteUnsignedLongLittleEndian(destination, packed);
        }
    }

    private readonly struct Depth32FloatStencil8Transfer : IDepthStencilPixelTransfer
    {
        public static int BytesPerTexel => 8;

        public static Rgba32Float Decode(ReadOnlySpan<byte> source)
        {
            var depth = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source));
            var stencil = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]) & 0xffu;
            return new Rgba32Float(depth, UNormToFloat(stencil, 8), 0f);
        }

        public static void Encode(Rgba32Float source, Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(source.Red));
            BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], FloatToUNorm(source.Green, 8));
        }
    }

    private void DecodePackedStencil<TPixel>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowByteCount = GetDefaultPitch(destination.Width);
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var sourceRow = source.Slice(rowOffset, rowByteCount);
            var destinationRow = destination.GetRowSpan(y);
            for (var x = 0; x < destination.Width; x++)
            {
                var stencil = DecodePackedStencilTexel(sourceRow, x);
                destinationRow[x] = TPixel.FromRgba32Float(new Rgba32Float(UNormToFloat(stencil, Format.RedBits), 0f, 0f));
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodePackedStencil<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var rowByteCount = GetDefaultPitch(source.Width);
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var destinationRow = destination.Slice(rowOffset, rowByteCount);
            destinationRow.Clear();

            var sourceRow = source.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                var stencil = FloatToUNorm(TPixel.ToRgba32Float(sourceRow[x]).Red, Format.RedBits);
                EncodePackedStencilTexel(destinationRow, x, stencil);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodePackedStencil(ReadOnlySpan<uint> stencil, int width, int height, Span<byte> destination, int rowPitch)
    {
        var rowByteCount = GetDefaultPitch(width);
        var sourceIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var destinationRow = destination.Slice(rowOffset, rowByteCount);
            destinationRow.Clear();

            for (var x = 0; x < width; x++)
            {
                EncodePackedStencilTexel(destinationRow, x, stencil[sourceIndex]);
                sourceIndex++;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void DecodePackedStencil(int width, int height, ReadOnlySpan<byte> source, Span<uint> stencil, int rowPitch)
    {
        var rowByteCount = GetDefaultPitch(width);
        var destinationIndex = 0;
        var rowOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var sourceRow = source.Slice(rowOffset, rowByteCount);
            for (var x = 0; x < width; x++)
            {
                stencil[destinationIndex] = DecodePackedStencilTexel(sourceRow, x);
                destinationIndex++;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void EncodeDepthTexel(float depth, Span<byte> destination)
    {
        if (_transfer == DepthStencilTransfer.DepthComponent32Float)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(depth));
            return;
        }

        WriteUnsignedLittleEndian(destination, FloatToUNorm(depth, Format.RedBits));
    }

    private float DecodeDepthTexel(ReadOnlySpan<byte> source)
    {
        if (_transfer == DepthStencilTransfer.DepthComponent32Float)
        {
            return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source));
        }

        return UNormToFloat(ReadUnsignedLittleEndian(source), Format.RedBits);
    }

    private static void EncodeStencilTexel(int bits, uint stencil, Span<byte> destination) =>
        WriteUnsignedLittleEndian(destination, Math.Min(stencil, GetMaxUInt(bits)));

    private static uint DecodeStencilTexel(int bits, ReadOnlySpan<byte> source) =>
        ReadUnsignedLittleEndian(source) & GetMaxUInt(bits);

    private void EncodeDepthStencilTexel(float depth, uint stencil, Span<byte> destination)
    {
        if (_transfer == DepthStencilTransfer.Depth16Stencil8)
        {
            var packed = (FloatToUNorm(depth, 16) << 8) | Math.Min(stencil, 0xffu);
            WriteUnsignedLittleEndian(destination, packed);
            return;
        }

        if (_transfer == DepthStencilTransfer.Depth24Stencil8)
        {
            var packed = (FloatToUNorm(depth, 24) << 8) | Math.Min(stencil, 0xffu);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, packed);
            return;
        }

        if (_transfer == DepthStencilTransfer.Depth32Stencil8)
        {
            var packed = ((ulong)FloatToUNorm(depth, 32) << 8) | Math.Min(stencil, 0xffu);
            WriteUnsignedLongLittleEndian(destination, packed);
            return;
        }

        if (_transfer == DepthStencilTransfer.Depth32FloatStencil8)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(depth));
            BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], Math.Min(stencil, 0xffu));
            return;
        }

        throw CreateUnsupportedFormatException(Format);
    }

    private void DecodeDepthStencilTexel(ReadOnlySpan<byte> source, out float depth, out uint stencil)
    {
        if (_transfer == DepthStencilTransfer.Depth16Stencil8)
        {
            var packed = ReadUnsignedLittleEndian(source);
            depth = UNormToFloat(packed >> 8, 16);
            stencil = packed & 0xffu;
            return;
        }

        if (_transfer == DepthStencilTransfer.Depth24Stencil8)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(source);
            depth = UNormToFloat(packed >> 8, 24);
            stencil = packed & 0xffu;
            return;
        }

        if (_transfer == DepthStencilTransfer.Depth32Stencil8)
        {
            var packed = ReadUnsignedLongLittleEndian(source);
            depth = UNormToFloat((uint)(packed >> 8), 32);
            stencil = (uint)(packed & 0xffu);
            return;
        }

        if (_transfer == DepthStencilTransfer.Depth32FloatStencil8)
        {
            depth = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source));
            stencil = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]) & 0xffu;
            return;
        }

        throw CreateUnsupportedFormatException(Format);
    }

    private uint DecodePackedStencilTexel(ReadOnlySpan<byte> row, int x) =>
        _transfer switch
        {
            DepthStencilTransfer.StencilIndex1 => ReadBit(row, x) ? 1u : 0u,
            DepthStencilTransfer.StencilIndex4 => ReadNibble(row, x),
            _ => throw CreateUnsupportedFormatException(Format)
        };

    private void EncodePackedStencilTexel(Span<byte> row, int x, uint stencil)
    {
        switch (_transfer)
        {
            case DepthStencilTransfer.StencilIndex1:
                if (stencil != 0)
                {
                    SetBit(row, x);
                }

                return;
            case DepthStencilTransfer.StencilIndex4:
                WriteNibble(row, x, (byte)Math.Min(stencil, 0xfu));
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private static bool ReadBit(ReadOnlySpan<byte> row, int x) =>
        (row[x >> 3] & (1 << (7 - (x & 7)))) != 0;

    private static void SetBit(Span<byte> row, int x) =>
        row[x >> 3] |= (byte)(1 << (7 - (x & 7)));

    private static byte ReadNibble(ReadOnlySpan<byte> row, int x)
    {
        var packed = row[x >> 1];
        return (x & 1) == 0
            ? (byte)(packed >> 4)
            : (byte)(packed & 0x0f);
    }

    private static void WriteNibble(Span<byte> row, int x, byte value)
    {
        var byteIndex = x >> 1;
        if ((x & 1) == 0)
        {
            row[byteIndex] = (byte)((row[byteIndex] & 0x0f) | (value << 4));
        }
        else
        {
            row[byteIndex] = (byte)((row[byteIndex] & 0xf0) | (value & 0x0f));
        }
    }

    private static uint ReadUnsignedLittleEndian(ReadOnlySpan<byte> source)
    {
        uint value = 0;
        for (var i = 0; i < source.Length; i++)
        {
            value |= (uint)source[i] << (i * 8);
        }

        return value;
    }

    private static ulong ReadUnsignedLongLittleEndian(ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        for (var i = 0; i < source.Length; i++)
        {
            value |= (ulong)source[i] << (i * 8);
        }

        return value;
    }

    private static void WriteUnsignedLittleEndian(Span<byte> destination, uint value)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)(value >> (i * 8));
        }
    }

    private static void WriteUnsignedLongLittleEndian(Span<byte> destination, ulong value)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)(value >> (i * 8));
        }
    }

    private static float UNormToFloat(uint value, int bits) => value / (float)GetMaxUInt(bits);

    private static uint FloatToUNorm(float value, int bits)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0;
        }

        if (value >= 1f)
        {
            return GetMaxUInt(bits);
        }

        return (uint)MathF.Round(value * GetMaxUInt(bits));
    }

    private static uint GetMaxUInt(int bits) => bits == 32 ? uint.MaxValue : (1u << bits) - 1u;

    private void ValidateDepthFormat()
    {
        if (Format.Components != TextureComponents.Depth)
        {
            throw CreateUnsupportedFormatException(Format);
        }
    }

    private void ValidateStencilFormat()
    {
        if (Format.Components != TextureComponents.Stencil)
        {
            throw CreateUnsupportedFormatException(Format);
        }
    }

    private void ValidateDepthStencilFormat()
    {
        if (Format.Components != TextureComponents.DepthStencil)
        {
            throw CreateUnsupportedFormatException(Format);
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded depth/stencil texture.", nameof(source));
        }
    }

    private void ValidateSourceLength(int width, int height, ReadOnlySpan<byte> source, int? rowPitch)
    {
        var requiredBytes = GetRequiredByteCount(width, height, rowPitch);
        if (source.Length < requiredBytes)
        {
            throw new ArgumentException("Source span is too small for the encoded depth/stencil texture.", nameof(source));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int rowPitch)
    {
        var requiredBytes = GetEncodedByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded depth/stencil texture.", nameof(destination));
        }
    }

    private void ValidateDestinationLength(int width, int height, Span<byte> destination, int? rowPitch)
    {
        var requiredBytes = GetRequiredByteCount(width, height, rowPitch);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException("Destination span is too small for the encoded depth/stencil texture.", nameof(destination));
        }
    }

    private static void ValidateTexelSpan<T>(ReadOnlySpan<T> span, int width, int height, string paramName)
    {
        if (span.Length < checked(width * height))
        {
            throw new ArgumentException("Texel span is too small for the texture dimensions.", paramName);
        }
    }

    private static void ValidateTexelSpan<T>(Span<T> span, int width, int height, string paramName)
    {
        if (span.Length < checked(width * height))
        {
            throw new ArgumentException("Texel span is too small for the texture dimensions.", paramName);
        }
    }

    private int ResolveRowPitch(int width, int? rowPitch)
    {
        var rowByteCount = GetDefaultPitch(width);
        if (!rowPitch.HasValue)
        {
            return rowByteCount;
        }

        if (rowPitch.Value < rowByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowPitch), "Row pitch must be at least the packed row byte count.");
        }

        return rowPitch.Value;
    }

    private static bool TryGetTransfer(TextureFormat format, out DepthStencilTransfer transfer)
    {
        if (format == TextureFormats.DepthComponent8)
        {
            transfer = DepthStencilTransfer.DepthComponent8;
            return true;
        }

        if (format == TextureFormats.DepthComponent16)
        {
            transfer = DepthStencilTransfer.DepthComponent16;
            return true;
        }

        if (format == TextureFormats.DepthComponent24)
        {
            transfer = DepthStencilTransfer.DepthComponent24;
            return true;
        }

        if (format == TextureFormats.DepthComponent32)
        {
            transfer = DepthStencilTransfer.DepthComponent32;
            return true;
        }

        if (format == TextureFormats.DepthComponent32Float)
        {
            transfer = DepthStencilTransfer.DepthComponent32Float;
            return true;
        }

        if (format == TextureFormats.StencilIndex1)
        {
            transfer = DepthStencilTransfer.StencilIndex1;
            return true;
        }

        if (format == TextureFormats.StencilIndex4)
        {
            transfer = DepthStencilTransfer.StencilIndex4;
            return true;
        }

        if (format == TextureFormats.StencilIndex8)
        {
            transfer = DepthStencilTransfer.StencilIndex8;
            return true;
        }

        if (format == TextureFormats.StencilIndex16)
        {
            transfer = DepthStencilTransfer.StencilIndex16;
            return true;
        }

        if (format == TextureFormats.Depth16Stencil8)
        {
            transfer = DepthStencilTransfer.Depth16Stencil8;
            return true;
        }

        if (format == TextureFormats.Depth24Stencil8)
        {
            transfer = DepthStencilTransfer.Depth24Stencil8;
            return true;
        }

        if (format == TextureFormats.Depth32Stencil8)
        {
            transfer = DepthStencilTransfer.Depth32Stencil8;
            return true;
        }

        if (format == TextureFormats.Depth32FloatStencil8)
        {
            transfer = DepthStencilTransfer.Depth32FloatStencil8;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Depth/stencil texture coder does not support texture format '{format.Name}'.");

    private enum DepthStencilTransfer
    {
        DepthComponent8,
        DepthComponent16,
        DepthComponent24,
        DepthComponent32,
        DepthComponent32Float,
        StencilIndex1,
        StencilIndex4,
        StencilIndex8,
        StencilIndex16,
        Depth16Stencil8,
        Depth24Stencil8,
        Depth32Stencil8,
        Depth32FloatStencil8
    }
}
