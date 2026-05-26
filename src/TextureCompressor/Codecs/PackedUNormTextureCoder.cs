using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedUNormTextureCoder(TextureFormat format) : IPitchTextureCoder
{
    private readonly PackedUNormPlan _plan = CreatePlan(format);

    public TextureFormat Format { get; } = format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.Rgb565UNorm
        || format == TextureFormats.Rgba4UNorm
        || format == TextureFormats.Rgb5A1UNorm
        || format == TextureFormats.Rgb10A2UNorm
        || format == TextureFormats.Bgra4UNorm;

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
        if (_plan.MaxComponentBits <= 8)
        {
            Decode<TPixel, Rgba8UNorm, Rgba8UNormTransfer>(source, destination, rowPitch);
            return;
        }

        Decode<TPixel, Rgba16UNorm, Rgba16UNormTransfer>(source, destination, rowPitch);
    }

    public void Encode<TPixel>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ValidateDestinationLength(source.Width, source.Height, destination, rowPitch);
        if (_plan.MaxComponentBits <= 8)
        {
            Encode<TPixel, Rgba8UNorm, Rgba8UNormTransfer>(source, destination, rowPitch);
            return;
        }

        Encode<TPixel, Rgba16UNorm, Rgba16UNormTransfer>(source, destination, rowPitch);
    }

    private interface IUNormTransfer<TCarrier>
    {
        static abstract ulong MaxValue { get; }

        static abstract TCarrier FromValues(ulong red, ulong green, ulong blue, ulong alpha);

        static abstract void ToValues(
            TCarrier source,
            out ulong red,
            out ulong green,
            out ulong blue,
            out ulong alpha);

        static abstract TPixel FromCarrier<TPixel>(TCarrier value)
            where TPixel : unmanaged, IPixel<TPixel>;

        static abstract TCarrier ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel>;
    }

    private readonly struct Rgba8UNormTransfer : IUNormTransfer<Rgba8UNorm>
    {
        public static ulong MaxValue => byte.MaxValue;

        public static Rgba8UNorm FromValues(ulong red, ulong green, ulong blue, ulong alpha) =>
            new((byte)red, (byte)green, (byte)blue, (byte)alpha);

        public static void ToValues(
            Rgba8UNorm source,
            out ulong red,
            out ulong green,
            out ulong blue,
            out ulong alpha)
        {
            red = source.Red;
            green = source.Green;
            blue = source.Blue;
            alpha = source.Alpha;
        }

        public static TPixel FromCarrier<TPixel>(Rgba8UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba8UNorm(value);

        public static Rgba8UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba8UNorm(value);
    }

    private readonly struct Rgba16UNormTransfer : IUNormTransfer<Rgba16UNorm>
    {
        public static ulong MaxValue => ushort.MaxValue;

        public static Rgba16UNorm FromValues(ulong red, ulong green, ulong blue, ulong alpha) =>
            new((ushort)red, (ushort)green, (ushort)blue, (ushort)alpha);

        public static void ToValues(
            Rgba16UNorm source,
            out ulong red,
            out ulong green,
            out ulong blue,
            out ulong alpha)
        {
            red = source.Red;
            green = source.Green;
            blue = source.Blue;
            alpha = source.Alpha;
        }

        public static TPixel FromCarrier<TPixel>(Rgba16UNorm value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.FromRgba16UNorm(value);

        public static Rgba16UNorm ToCarrier<TPixel>(TPixel value)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TPixel.ToRgba16UNorm(value);
    }

    private void Decode<TPixel, TCarrier, TTransfer>(
        ReadOnlySpan<byte> source,
        ImageView<TPixel> destination,
        int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IUNormTransfer<TCarrier>
    {
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < destination.Width; x++)
            {
                var carrier = DecodePackedComponents<TCarrier, TTransfer>(source.Slice(texelOffset, _plan.BytesPerTexel));
                destinationRow[x] = TTransfer.FromCarrier<TPixel>(carrier);
                texelOffset = checked(texelOffset + _plan.BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode<TPixel, TCarrier, TTransfer>(
        ImageView<TPixel> source,
        Span<byte> destination,
        int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IUNormTransfer<TCarrier>
    {
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var texelOffset = rowOffset;
            for (var x = 0; x < source.Width; x++)
            {
                EncodePackedComponents<TCarrier, TTransfer>(
                    TTransfer.ToCarrier(sourceRow[x]),
                    destination.Slice(texelOffset, _plan.BytesPerTexel));
                texelOffset = checked(texelOffset + _plan.BytesPerTexel);
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private TCarrier DecodePackedComponents<TCarrier, TTransfer>(ReadOnlySpan<byte> texel)
        where TTransfer : IUNormTransfer<TCarrier>
    {
        var destinationMax = TTransfer.MaxValue;
        ulong red = 0;
        ulong green = 0;
        ulong blue = 0;
        var alpha = destinationMax;
        var packed = ReadPackedUIntLittleEndian(texel);

        foreach (var component in _plan.Components)
        {
            var value = ScaleUnsigned((packed >> component.Shift) & component.Mask, component.Mask, destinationMax);
            SetComponentValue(component.ChannelIndex, value, ref red, ref green, ref blue, ref alpha);
        }

        return TTransfer.FromValues(red, green, blue, alpha);
    }

    private void EncodePackedComponents<TCarrier, TTransfer>(TCarrier carrier, Span<byte> texel)
        where TTransfer : IUNormTransfer<TCarrier>
    {
        TTransfer.ToValues(carrier, out var red, out var green, out var blue, out var alpha);

        var sourceMax = TTransfer.MaxValue;
        ulong packed = 0;

        foreach (var component in _plan.Components)
        {
            var value = ScaleUnsigned(
                GetComponentValue(component.ChannelIndex, red, green, blue, alpha),
                sourceMax,
                component.Mask);
            packed |= value << component.Shift;
        }

        WritePackedUIntLittleEndian(texel, packed);
    }

    private readonly record struct PackedUNormPlan(
        int BytesPerTexel,
        int MaxComponentBits,
        PackedComponentPlan[] Components);

    private readonly record struct PackedComponentPlan(
        int ChannelIndex,
        int Shift,
        ulong Mask);

    private static PackedUNormPlan CreatePlan(TextureFormat format)
    {
        if (!IsSupported(format))
        {
            throw CreateUnsupportedFormatException(format);
        }

        var components = new PackedComponentPlan[format.ChannelCount];
        var shift = format.BitsPerBlock;
        var maxBits = 0;
        for (var storageComponent = 0; storageComponent < components.Length; storageComponent++)
        {
            var channelIndex = GetStorageChannelIndex(format.Components, storageComponent);
            var bits = GetComponentBits(format, channelIndex);
            shift -= bits;
            components[storageComponent] = new PackedComponentPlan(channelIndex, shift, GetUnsignedMax(bits));
            maxBits = Math.Max(maxBits, bits);
        }

        return new PackedUNormPlan(format.BytesPerBlock, maxBits, components);
    }

    private static int GetComponentBits(TextureFormat format, int component) => component switch
    {
        0 => format.RedBits,
        1 => format.GreenBits,
        2 => format.BlueBits,
        3 => format.AlphaBits,
        _ => throw new ArgumentOutOfRangeException(nameof(component))
    };

    private static int GetStorageChannelIndex(TextureComponents components, int storageComponent) => components switch
    {
        TextureComponents.Bgra => storageComponent switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            3 => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(storageComponent))
        },
        _ => storageComponent
    };

    private static void SetComponentValue(
        int component,
        ulong value,
        ref ulong red,
        ref ulong green,
        ref ulong blue,
        ref ulong alpha)
    {
        switch (component)
        {
            case 0:
                red = value;
                return;
            case 1:
                green = value;
                return;
            case 2:
                blue = value;
                return;
            case 3:
                alpha = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static ulong GetComponentValue(int component, ulong red, ulong green, ulong blue, ulong alpha) => component switch
    {
        0 => red,
        1 => green,
        2 => blue,
        3 => alpha,
        _ => throw new ArgumentOutOfRangeException(nameof(component))
    };

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

    private static ulong GetUnsignedMax(int bits) => (1UL << bits) - 1UL;

    private static ulong ScaleUnsigned(ulong value, ulong sourceMax, ulong destinationMax)
    {
        if (sourceMax == destinationMax)
        {
            return value;
        }

        return ((value * destinationMax) + (sourceMax / 2UL)) / sourceMax;
    }

    private static ulong ReadPackedUIntLittleEndian(ReadOnlySpan<byte> source) => source.Length switch
    {
        1 => source[0],
        2 => BinaryPrimitives.ReadUInt16LittleEndian(source),
        4 => BinaryPrimitives.ReadUInt32LittleEndian(source),
        8 => BinaryPrimitives.ReadUInt64LittleEndian(source),
        _ => ReadPackedUIntLittleEndianSlow(source)
    };

    private static ulong ReadPackedUIntLittleEndianSlow(ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        for (var i = 0; i < source.Length; i++)
        {
            value |= (ulong)source[i] << (i << 3);
        }

        return value;
    }

    private static void WritePackedUIntLittleEndian(Span<byte> destination, ulong value)
    {
        switch (destination.Length)
        {
            case 1:
                destination[0] = (byte)value;
                return;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)value);
                return;
            case 4:
                BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)value);
                return;
            case 8:
                BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
                return;
            default:
                WritePackedUIntLittleEndianSlow(destination, value);
                return;
        }
    }

    private static void WritePackedUIntLittleEndianSlow(Span<byte> destination, ulong value)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)(value >> (i << 3));
        }
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed UNorm texture codec does not support texture format '{format.Name}'.");
}
