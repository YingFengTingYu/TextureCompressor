using System.Buffers.Binary;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Images;

namespace TextureCompressor.Codecs;

public sealed class PackedRgb422TextureCoder : IPitchTextureCoder
{
    private readonly PackedRgb422Transfer _transfer;

    public PackedRgb422TextureCoder(TextureFormat format)
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
            case PackedRgb422Transfer.RgBg8:
                Decode8<TPixel, RgBg8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.RgBg8BigEndian:
                Decode8<TPixel, RgBg8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GrGb8:
                Decode8<TPixel, GrGb8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GrGb8BigEndian:
                Decode8<TPixel, GrGb8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr8:
                Decode8<TPixel, GbGr8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg8:
                Decode8<TPixel, BgRg8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr10:
                Decode16<TPixel, GbGr10Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg10:
                Decode16<TPixel, BgRg10Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr12:
                Decode16<TPixel, GbGr12Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg12:
                Decode16<TPixel, BgRg12Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr16:
                Decode16<TPixel, GbGr16Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg16:
                Decode16<TPixel, BgRg16Transfer>(source, destination, rowPitch);
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
            case PackedRgb422Transfer.RgBg8:
                Encode8<TPixel, RgBg8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.RgBg8BigEndian:
                Encode8<TPixel, RgBg8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GrGb8:
                Encode8<TPixel, GrGb8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GrGb8BigEndian:
                Encode8<TPixel, GrGb8TransferBigEndian>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr8:
                Encode8<TPixel, GbGr8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg8:
                Encode8<TPixel, BgRg8Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr10:
                Encode16<TPixel, GbGr10Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg10:
                Encode16<TPixel, BgRg10Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr12:
                Encode16<TPixel, GbGr12Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg12:
                Encode16<TPixel, BgRg12Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.GbGr16:
                Encode16<TPixel, GbGr16Transfer>(source, destination, rowPitch);
                return;
            case PackedRgb422Transfer.BgRg16:
                Encode16<TPixel, BgRg16Transfer>(source, destination, rowPitch);
                return;
            default:
                throw CreateUnsupportedFormatException(Format);
        }
    }

    private void Decode8<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedRgb422Transfer<Rgba8UNorm>
    {
        var blocksPerRow = destination.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var block = source.Slice(blockOffset, TTransfer.BytesPerBlock);
                TTransfer.DecodeBlock(block, out var first, out var second);
                destinationRow[pixelX] = TPixel.FromRgba8UNorm(first);
                destinationRow[pixelX + 1] = TPixel.FromRgba8UNorm(second);

                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode8<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedRgb422Transfer<Rgba8UNorm>
    {
        var blocksPerRow = source.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var destinationBlock = destination.Slice(blockOffset, TTransfer.BytesPerBlock);
                TTransfer.EncodeBlock(
                    TPixel.ToRgba8UNorm(sourceRow[pixelX]),
                    TPixel.ToRgba8UNorm(sourceRow[pixelX + 1]),
                    destinationBlock);

                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Decode16<TPixel, TTransfer>(ReadOnlySpan<byte> source, ImageView<TPixel> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        var blocksPerRow = destination.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var destinationRow = destination.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var block = source.Slice(blockOffset, TTransfer.BytesPerBlock);
                TTransfer.DecodeBlock(block, out var first, out var second);
                destinationRow[pixelX] = TPixel.FromRgba16UNorm(first);
                destinationRow[pixelX + 1] = TPixel.FromRgba16UNorm(second);

                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private void Encode16<TPixel, TTransfer>(ImageView<TPixel> source, Span<byte> destination, int rowPitch)
        where TPixel : unmanaged, IPixel<TPixel>
        where TTransfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        var blocksPerRow = source.Width / 2;
        var rowOffset = 0;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceRow = source.GetRowSpan(y);
            var blockOffset = rowOffset;
            var pixelX = 0;
            for (var blockX = 0; blockX < blocksPerRow; blockX++)
            {
                var destinationBlock = destination.Slice(blockOffset, TTransfer.BytesPerBlock);
                TTransfer.EncodeBlock(
                    TPixel.ToRgba16UNorm(sourceRow[pixelX]),
                    TPixel.ToRgba16UNorm(sourceRow[pixelX + 1]),
                    destinationBlock);

                blockOffset = checked(blockOffset + TTransfer.BytesPerBlock);
                pixelX += 2;
            }

            rowOffset = checked(rowOffset + rowPitch);
        }
    }

    private interface IPackedRgb422Transfer<TCarrier>
    {
        static abstract int BytesPerBlock { get; }

        static abstract void DecodeBlock(ReadOnlySpan<byte> block, out TCarrier first, out TCarrier second);

        static abstract void EncodeBlock(TCarrier first, TCarrier second, Span<byte> block);
    }

    private static void DecodeBigEndianBlock<TTransfer, TCarrier>(
        ReadOnlySpan<byte> source,
        out TCarrier first,
        out TCarrier second,
        BigEndianByteSwapMode endianMode)
        where TTransfer : IPackedRgb422Transfer<TCarrier>
    {
        Span<byte> littleEndianBlock = stackalloc byte[TTransfer.BytesPerBlock];
        BigEndianByteSwap.CopyToLittleEndian(source, littleEndianBlock, endianMode);
        TTransfer.DecodeBlock(littleEndianBlock, out first, out second);
    }

    private static void EncodeBigEndianBlock<TTransfer, TCarrier>(
        TCarrier first,
        TCarrier second,
        Span<byte> destination,
        BigEndianByteSwapMode endianMode)
        where TTransfer : IPackedRgb422Transfer<TCarrier>
    {
        Span<byte> littleEndianBlock = stackalloc byte[TTransfer.BytesPerBlock];
        TTransfer.EncodeBlock(first, second, littleEndianBlock);
        BigEndianByteSwap.CopyFromLittleEndian(littleEndianBlock, destination, endianMode);
    }

    private readonly struct RgBg8Transfer : IPackedRgb422Transfer<Rgba8UNorm>
    {
        public static int BytesPerBlock => 4;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second)
        {
            var red = block[0];
            var green0 = block[1];
            var blue = block[2];
            var green1 = block[3];
            first = new Rgba8UNorm(red, green0, blue);
            second = new Rgba8UNorm(red, green1, blue);
        }

        public static void EncodeBlock(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block)
        {
            block[0] = AverageUNorm8(first.Red, second.Red);
            block[1] = first.Green;
            block[2] = AverageUNorm8(first.Blue, second.Blue);
            block[3] = second.Green;
        }
    }

    private readonly struct RgBg8TransferBigEndian : IPackedRgb422Transfer<Rgba8UNorm>
    {
        public static int BytesPerBlock => RgBg8Transfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second) =>
            DecodeBigEndianBlock<RgBg8Transfer, Rgba8UNorm>(block, out first, out second, BigEndianByteSwapMode.Swap8In32);

        public static void EncodeBlock(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block) =>
            EncodeBigEndianBlock<RgBg8Transfer, Rgba8UNorm>(first, second, block, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct GrGb8Transfer : IPackedRgb422Transfer<Rgba8UNorm>
    {
        public static int BytesPerBlock => 4;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second)
        {
            var green0 = block[0];
            var red = block[1];
            var green1 = block[2];
            var blue = block[3];
            first = new Rgba8UNorm(red, green0, blue);
            second = new Rgba8UNorm(red, green1, blue);
        }

        public static void EncodeBlock(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block)
        {
            block[0] = first.Green;
            block[1] = AverageUNorm8(first.Red, second.Red);
            block[2] = second.Green;
            block[3] = AverageUNorm8(first.Blue, second.Blue);
        }
    }

    private readonly struct GrGb8TransferBigEndian : IPackedRgb422Transfer<Rgba8UNorm>
    {
        public static int BytesPerBlock => GrGb8Transfer.BytesPerBlock;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second) =>
            DecodeBigEndianBlock<GrGb8Transfer, Rgba8UNorm>(block, out first, out second, BigEndianByteSwapMode.Swap8In32);

        public static void EncodeBlock(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block) =>
            EncodeBigEndianBlock<GrGb8Transfer, Rgba8UNorm>(first, second, block, BigEndianByteSwapMode.Swap8In32);
    }

    private readonly struct GbGr8Transfer : IPackedRgb422Transfer<Rgba8UNorm>
    {
        public static int BytesPerBlock => 4;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second) =>
            DecodeGbGr8(block, out first, out second);

        public static void EncodeBlock(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block) =>
            EncodeGbGr8(first, second, block);
    }

    private readonly struct BgRg8Transfer : IPackedRgb422Transfer<Rgba8UNorm>
    {
        public static int BytesPerBlock => 4;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second) =>
            DecodeBgRg8(block, out first, out second);

        public static void EncodeBlock(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block) =>
            EncodeBgRg8(first, second, block);
    }

    private readonly struct GbGr10Transfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second) =>
            DecodeGbGr16(block, unusedLowBits: 6, out first, out second);

        public static void EncodeBlock(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block) =>
            EncodeGbGr16(first, second, block, unusedLowBits: 6);
    }

    private readonly struct BgRg10Transfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second) =>
            DecodeBgRg16(block, unusedLowBits: 6, out first, out second);

        public static void EncodeBlock(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block) =>
            EncodeBgRg16(first, second, block, unusedLowBits: 6);
    }

    private readonly struct GbGr12Transfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second) =>
            DecodeGbGr16(block, unusedLowBits: 4, out first, out second);

        public static void EncodeBlock(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block) =>
            EncodeGbGr16(first, second, block, unusedLowBits: 4);
    }

    private readonly struct BgRg12Transfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second) =>
            DecodeBgRg16(block, unusedLowBits: 4, out first, out second);

        public static void EncodeBlock(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block) =>
            EncodeBgRg16(first, second, block, unusedLowBits: 4);
    }

    private readonly struct GbGr16Transfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second) =>
            DecodeGbGr16(block, unusedLowBits: 0, out first, out second);

        public static void EncodeBlock(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block) =>
            EncodeGbGr16(first, second, block, unusedLowBits: 0);
    }

    private readonly struct BgRg16Transfer : IPackedRgb422Transfer<Rgba16UNorm>
    {
        public static int BytesPerBlock => 8;

        public static void DecodeBlock(ReadOnlySpan<byte> block, out Rgba16UNorm first, out Rgba16UNorm second) =>
            DecodeBgRg16(block, unusedLowBits: 0, out first, out second);

        public static void EncodeBlock(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block) =>
            EncodeBgRg16(first, second, block, unusedLowBits: 0);
    }

    private static void DecodeGbGr8(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second)
    {
        var green0 = block[0];
        var blue = block[1];
        var green1 = block[2];
        var red = block[3];
        first = new Rgba8UNorm(red, green0, blue);
        second = new Rgba8UNorm(red, green1, blue);
    }

    private static void DecodeBgRg8(ReadOnlySpan<byte> block, out Rgba8UNorm first, out Rgba8UNorm second)
    {
        var blue = block[0];
        var green0 = block[1];
        var red = block[2];
        var green1 = block[3];
        first = new Rgba8UNorm(red, green0, blue);
        second = new Rgba8UNorm(red, green1, blue);
    }

    private static void EncodeGbGr8(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block)
    {
        block[0] = first.Green;
        block[1] = AverageUNorm8(first.Blue, second.Blue);
        block[2] = second.Green;
        block[3] = AverageUNorm8(first.Red, second.Red);
    }

    private static void EncodeBgRg8(Rgba8UNorm first, Rgba8UNorm second, Span<byte> block)
    {
        block[0] = AverageUNorm8(first.Blue, second.Blue);
        block[1] = first.Green;
        block[2] = AverageUNorm8(first.Red, second.Red);
        block[3] = second.Green;
    }

    private static void DecodeGbGr16(ReadOnlySpan<byte> block, int unusedLowBits, out Rgba16UNorm first, out Rgba16UNorm second)
    {
        var green0 = DecodeUInt16Component(block, 0, unusedLowBits);
        var blue = DecodeUInt16Component(block, 1, unusedLowBits);
        var green1 = DecodeUInt16Component(block, 2, unusedLowBits);
        var red = DecodeUInt16Component(block, 3, unusedLowBits);
        first = new Rgba16UNorm(red, green0, blue);
        second = new Rgba16UNorm(red, green1, blue);
    }

    private static void DecodeBgRg16(ReadOnlySpan<byte> block, int unusedLowBits, out Rgba16UNorm first, out Rgba16UNorm second)
    {
        var blue = DecodeUInt16Component(block, 0, unusedLowBits);
        var green0 = DecodeUInt16Component(block, 1, unusedLowBits);
        var red = DecodeUInt16Component(block, 2, unusedLowBits);
        var green1 = DecodeUInt16Component(block, 3, unusedLowBits);
        first = new Rgba16UNorm(red, green0, blue);
        second = new Rgba16UNorm(red, green1, blue);
    }

    private static void EncodeGbGr16(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block, int unusedLowBits)
    {
        EncodeUInt16Component(block, 0, first.Green, unusedLowBits);
        EncodeUInt16Component(block, 1, AverageUNorm16(first.Blue, second.Blue), unusedLowBits);
        EncodeUInt16Component(block, 2, second.Green, unusedLowBits);
        EncodeUInt16Component(block, 3, AverageUNorm16(first.Red, second.Red), unusedLowBits);
    }

    private static void EncodeBgRg16(Rgba16UNorm first, Rgba16UNorm second, Span<byte> block, int unusedLowBits)
    {
        EncodeUInt16Component(block, 0, AverageUNorm16(first.Blue, second.Blue), unusedLowBits);
        EncodeUInt16Component(block, 1, first.Green, unusedLowBits);
        EncodeUInt16Component(block, 2, AverageUNorm16(first.Red, second.Red), unusedLowBits);
        EncodeUInt16Component(block, 3, second.Green, unusedLowBits);
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

    private static ushort DecodeUInt16Component(ReadOnlySpan<byte> block, int component, int unusedLowBits)
    {
        var value = ReadUInt16(block, component);
        if (unusedLowBits == 0)
        {
            return value;
        }

        var bits = 16 - unusedLowBits;
        var componentValue = (uint)value >> unusedLowBits;
        return (ushort)((componentValue << unusedLowBits) | (componentValue >> (bits - unusedLowBits)));
    }

    private static void EncodeUInt16Component(Span<byte> block, int component, ushort value, int unusedLowBits)
    {
        if (unusedLowBits == 0)
        {
            WriteUInt16(block, component, value);
            return;
        }

        var packed = (ushort)(((uint)value >> unusedLowBits) << unusedLowBits);
        WriteUInt16(block, component, packed);
    }

    private static byte AverageUNorm8(byte first, byte second) =>
        RgbaColorConversions.FloatToUNorm8(
            (RgbaColorConversions.UNorm8ToFloat(first) + RgbaColorConversions.UNorm8ToFloat(second)) * 0.5f);

    private static ushort AverageUNorm16(ushort first, ushort second) =>
        RgbaColorConversions.FloatToUNorm16(
            (RgbaColorConversions.UNorm16ToFloat(first) + RgbaColorConversions.UNorm16ToFloat(second)) * 0.5f);

    private static bool TryGetTransfer(TextureFormat format, out PackedRgb422Transfer transfer)
    {
        if (format == TextureFormats.R8G8B8G8_422UNorm)
        {
            transfer = PackedRgb422Transfer.RgBg8;
            return true;
        }

        if (format == TextureFormats.R8G8B8G8_422UNormBigEndian)
        {
            transfer = PackedRgb422Transfer.RgBg8BigEndian;
            return true;
        }

        if (format == TextureFormats.G8R8G8B8_422UNorm)
        {
            transfer = PackedRgb422Transfer.GrGb8;
            return true;
        }

        if (format == TextureFormats.G8R8G8B8_422UNormBigEndian)
        {
            transfer = PackedRgb422Transfer.GrGb8BigEndian;
            return true;
        }

        if (format == TextureFormats.G8B8G8R8_422UNorm)
        {
            transfer = PackedRgb422Transfer.GbGr8;
            return true;
        }

        if (format == TextureFormats.B8G8R8G8_422UNorm)
        {
            transfer = PackedRgb422Transfer.BgRg8;
            return true;
        }

        if (format == TextureFormats.G10X6B10X6G10X6R10X6_422UNorm)
        {
            transfer = PackedRgb422Transfer.GbGr10;
            return true;
        }

        if (format == TextureFormats.B10X6G10X6R10X6G10X6_422UNorm)
        {
            transfer = PackedRgb422Transfer.BgRg10;
            return true;
        }

        if (format == TextureFormats.G12X4B12X4G12X4R12X4_422UNorm)
        {
            transfer = PackedRgb422Transfer.GbGr12;
            return true;
        }

        if (format == TextureFormats.B12X4G12X4R12X4G12X4_422UNorm)
        {
            transfer = PackedRgb422Transfer.BgRg12;
            return true;
        }

        if (format == TextureFormats.G16B16G16R16_422UNorm)
        {
            transfer = PackedRgb422Transfer.GbGr16;
            return true;
        }

        if (format == TextureFormats.B16G16R16G16_422UNorm)
        {
            transfer = PackedRgb422Transfer.BgRg16;
            return true;
        }

        transfer = default;
        return false;
    }

    private static NotSupportedException CreateUnsupportedFormatException(TextureFormat format) =>
        new($"Packed RGB 4:2:2 texture coder does not support texture format '{format.Name}'.");

    private enum PackedRgb422Transfer
    {
        RgBg8,
        RgBg8BigEndian,
        GrGb8,
        GrGb8BigEndian,
        GbGr8,
        BgRg8,
        GbGr10,
        BgRg10,
        GbGr12,
        BgRg12,
        GbGr16,
        BgRg16
    }
}
