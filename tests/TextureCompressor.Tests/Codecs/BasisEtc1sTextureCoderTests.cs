using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.Tests;

public sealed class BasisEtc1sTextureCoderTests
{
    [Fact]
    public void FixedLengthTextureCoderMethodsPointToBasisEtc1sInterface()
    {
        ITextureCoder coder = new BasisEtc1sTextureCoder(TextureFormats.RgbaBasisEtc1sUNorm);
        var source = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var destination = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var encoded = new byte[8];

        var countException = Assert.Throws<NotSupportedException>(() => coder.GetEncodedByteCount(4, 4));
        var encodeException = Assert.Throws<NotSupportedException>(() => coder.Encode(source.AsView(), encoded));
        var decodeException = Assert.Throws<NotSupportedException>(() => coder.Decode(encoded, destination.AsView()));

        Assert.Contains(nameof(IBasisEtc1sTextureCoder), countException.Message);
        Assert.Contains(nameof(IBasisEtc1sTextureCoder), encodeException.Message);
        Assert.Contains(nameof(IBasisEtc1sTextureCoder), decodeException.Message);
    }

    [Fact]
    public void BasisEtc1sInterfaceEncodesAndDecodesVariableLengthPayload()
    {
        IBasisEtc1sTextureCoder coder = new BasisEtc1sTextureCoder(TextureFormats.RgbaBasisEtc1sUNorm);
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(120, 64, 220, 180), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        var payload = coder.Encode(source.AsView());
        coder.Decode(payload.AsRawPayload(), decoded.AsView());

        Assert.False(payload.RgbSliceData.IsEmpty);
        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 112, 128);
            Assert.InRange(pixel.Green, 56, 72);
            Assert.InRange(pixel.Blue, 212, 228);
            Assert.InRange(pixel.Alpha, 172, 188);
        });
    }

    [Fact]
    public void DecodeMinimalRawEtc1sPayload()
    {
        var payload = CreateMinimalEncodedEtc1sPayload();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);

        BasisEtc1sTextureCoder.Decode(payload.AsRawPayload(), bitmap.AsView());

        Assert.All(bitmap.Pixels, pixel =>
        {
            Assert.Equal(134, pixel.Red);
            Assert.Equal(134, pixel.Green);
            Assert.Equal(134, pixel.Blue);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void DecodeAcceptsRawPayloadParts()
    {
        var endpointData = CreateEndpointCodebook();
        var selectorData = CreateSelectorCodebook();
        var tableData = CreateTables();
        var sliceData = CreateSliceData();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var payload = new BasisEtc1sRawPayload(
            endpointCount: 1,
            endpointData,
            selectorCount: 1,
            selectorData,
            tableData,
            sliceData);

        BasisEtc1sTextureCoder.Decode(payload, bitmap.AsView());

        Assert.All(bitmap.Pixels, pixel =>
        {
            Assert.Equal(134, pixel.Red);
            Assert.Equal(134, pixel.Green);
            Assert.Equal(134, pixel.Blue);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void DecodeAcceptsCompressedSelectorCodebook()
    {
        var endpointData = CreateEndpointCodebook();
        var selectorData = CreateCompressedSelectorCodebook();
        var tableData = CreateTables(selectorCodeSizes: [1, 1]);
        var sliceData = CreateSliceData(selectorCodeSizes: [1, 1], selectorSymbol: 1);
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var payload = new BasisEtc1sRawPayload(
            endpointCount: 1,
            endpointData,
            selectorCount: 2,
            selectorData,
            tableData,
            sliceData);

        BasisEtc1sTextureCoder.Decode(payload, bitmap.AsView());

        Assert.All(bitmap.Pixels, pixel =>
        {
            Assert.Equal(134, pixel.Red);
            Assert.Equal(134, pixel.Green);
            Assert.Equal(134, pixel.Blue);
            Assert.Equal(255, pixel.Alpha);
        });
    }

    [Fact]
    public void DecodeRejectsSelectorCodebookReservedHeaderBits()
    {
        var endpointData = CreateEndpointCodebook();
        var selectorData = CreateSelectorCodebookWithReservedHeaderBit();
        var tableData = CreateTables();
        var sliceData = CreateSliceData();
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var payload = new BasisEtc1sRawPayload(
            endpointCount: 1,
            endpointData,
            selectorCount: 1,
            selectorData,
            tableData,
            sliceData);

        try
        {
            BasisEtc1sTextureCoder.Decode(payload, bitmap.AsView());
        }
        catch (InvalidDataException)
        {
            return;
        }

        Assert.Fail("Reserved selector codebook header bits should be rejected.");
    }

    [Fact]
    public void DecodeRejectsPFrameRawPayload()
    {
        var payload = CreateMinimalEncodedEtc1sPayload(isPFrame: true);
        var bitmap = new ArrayBitmap<Rgba8UNorm>(4, 4);

        Assert.Throws<NotSupportedException>(() => BasisEtc1sTextureCoder.Decode(payload.AsRawPayload(), bitmap.AsView()));
    }

    [Fact]
    public void EncodeAndDecodeSolidRgbaBasisEtc1sRoundTripsWithinQuantization()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(120, 64, 220, 180), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        var payload = BasisEtc1sTextureCoder.Encode(source.AsView());
        BasisEtc1sTextureCoder.Decode(payload.AsRawPayload(), decoded.AsView());

        Assert.False(payload.EndpointData.IsEmpty);
        Assert.False(payload.SelectorData.IsEmpty);
        Assert.False(payload.TablesData.IsEmpty);
        Assert.False(payload.RgbSliceData.IsEmpty);
        Assert.False(payload.AlphaSliceData.IsEmpty);
        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 112, 128);
            Assert.InRange(pixel.Green, 56, 72);
            Assert.InRange(pixel.Blue, 212, 228);
            Assert.InRange(pixel.Alpha, 172, 188);
        });
    }

    [Fact]
    public void EncodeAndDecodeSrgbRgbaBasisEtc1sRoundTripsThroughStorageColorSpace()
    {
        var source = new ArrayBitmap<Rgba8UNorm>(
            4,
            4,
            Enumerable.Repeat(new Rgba8UNorm(64, 128, 192, 220), 16).ToArray());
        var decoded = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var storageDecoded = new ArrayBitmap<Rgba8UNorm>(4, 4);

        var payload = BasisEtc1sTextureCoder.Encode(source.AsView(), srgb: true);
        BasisEtc1sTextureCoder.Decode(payload.AsRawPayload(), decoded.AsView(), srgb: true);
        BasisEtc1sTextureCoder.Decode(payload.AsRawPayload(), storageDecoded.AsView());

        Assert.All(decoded.Pixels, pixel =>
        {
            Assert.InRange(pixel.Red, 56, 72);
            Assert.InRange(pixel.Green, 120, 136);
            Assert.InRange(pixel.Blue, 180, 200);
            Assert.InRange(pixel.Alpha, 212, 228);
        });
        Assert.All(storageDecoded.Pixels, pixel =>
        {
            Assert.True(pixel.Red > 64);
            Assert.True(pixel.Green > 128);
            Assert.True(pixel.Blue > 192);
        });
    }

    private static BasisEtc1sEncodedPayload CreateMinimalEncodedEtc1sPayload(bool isPFrame = false)
    {
        var endpointData = CreateEndpointCodebook();
        var selectorData = CreateSelectorCodebook();
        var tableData = CreateTables();
        var sliceData = CreateSliceData();
        return new BasisEtc1sEncodedPayload(
            endpointCount: 1,
            endpointData,
            selectorCount: 1,
            selectorData,
            tableData,
            sliceData,
            isPFrame: isPFrame);
    }

    private static byte[] CreateEndpointCodebook()
    {
        var writer = new BitWriter();
        WriteHuffmanTable(writer, [1]);
        WriteHuffmanTable(writer, [1]);
        WriteHuffmanTable(writer, [1]);
        WriteHuffmanTable(writer, [1]);
        writer.WriteBits(1, 1);
        writer.WriteBits(0, 1);
        writer.WriteBits(0, 1);
        return writer.ToArray();
    }

    private static byte[] CreateSelectorCodebook()
    {
        var writer = new BitWriter();
        writer.WriteBits(0, 1);
        writer.WriteBits(0, 1);
        writer.WriteBits(1, 1);
        writer.WriteBits(0xaa, 8);
        writer.WriteBits(0xaa, 8);
        writer.WriteBits(0xaa, 8);
        writer.WriteBits(0xaa, 8);
        return writer.ToArray();
    }

    private static byte[] CreateCompressedSelectorCodebook()
    {
        var writer = new BitWriter();
        writer.WriteBits(0, 2);
        writer.WriteBits(0, 1);

        var deltaCodeSizes = new byte[0xab];
        deltaCodeSizes[0] = 1;
        deltaCodeSizes[0xaa] = 1;
        WriteHuffmanTable(writer, deltaCodeSizes);

        writer.WriteBits(0, 8);
        writer.WriteBits(0, 8);
        writer.WriteBits(0, 8);
        writer.WriteBits(0, 8);

        var deltaCodes = BuildHuffmanCodes(deltaCodeSizes);
        for (var row = 0; row < 4; row++)
        {
            writer.WriteBits(deltaCodes[0xaa].Code, deltaCodes[0xaa].Length);
        }

        return writer.ToArray();
    }

    private static byte[] CreateSelectorCodebookWithReservedHeaderBit()
    {
        var writer = new BitWriter();
        writer.WriteBits(1, 1);
        writer.WriteBits(0, 1);
        writer.WriteBits(1, 1);
        writer.WriteBits(0xaa, 8);
        writer.WriteBits(0xaa, 8);
        writer.WriteBits(0xaa, 8);
        writer.WriteBits(0xaa, 8);
        return writer.ToArray();
    }

    private static byte[] CreateTables(ReadOnlySpan<byte> selectorCodeSizes = default)
    {
        var writer = new BitWriter();
        WriteHuffmanTable(writer, [0, 0, 0, 1]);
        WriteHuffmanTable(writer, [1]);
        WriteHuffmanTable(writer, selectorCodeSizes.IsEmpty ? [1] : selectorCodeSizes);
        WriteHuffmanTable(writer, [1]);
        writer.WriteBits(1, 13);
        return writer.ToArray();
    }

    private static byte[] CreateSliceData(ReadOnlySpan<byte> selectorCodeSizes = default, int selectorSymbol = 0)
    {
        var writer = new BitWriter();
        var selectorCodes = BuildHuffmanCodes(selectorCodeSizes.IsEmpty ? [1] : selectorCodeSizes);
        writer.WriteBits(0, 1);
        writer.WriteBits(0, 1);
        writer.WriteBits(selectorCodes[selectorSymbol].Code, selectorCodes[selectorSymbol].Length);
        return writer.ToArray();
    }

    private static void WriteHuffmanTable(BitWriter writer, ReadOnlySpan<byte> codeSizes)
    {
        writer.WriteBits(codeSizes.Length, 14);
        if (codeSizes.Length == 0)
        {
            return;
        }

        Span<byte> codeLengthCodeSizes = stackalloc byte[21];
        foreach (var codeSize in codeSizes)
        {
            codeLengthCodeSizes[codeSize] = 1;
        }

        var sortedCodeLengthCodes = new byte[]
        {
            17, 18, 19, 20, 0, 8, 7, 9, 6, 0x0a, 5, 0x0b, 4, 0x0c, 3, 0x0d, 2, 0x0e, 1, 0x0f, 0x10
        };

        var codeLengthCodeCount = 0;
        for (var i = 0; i < sortedCodeLengthCodes.Length; i++)
        {
            if (codeLengthCodeSizes[sortedCodeLengthCodes[i]] != 0)
            {
                codeLengthCodeCount = i + 1;
            }
        }

        writer.WriteBits(codeLengthCodeCount, 5);
        for (var i = 0; i < codeLengthCodeCount; i++)
        {
            writer.WriteBits(codeLengthCodeSizes[sortedCodeLengthCodes[i]], 3);
        }

        var codeLengthCodes = BuildHuffmanCodes(codeLengthCodeSizes);
        foreach (var codeSize in codeSizes)
        {
            writer.WriteBits(codeLengthCodes[codeSize].Code, codeLengthCodes[codeSize].Length);
        }
    }

    private static HuffmanCode[] BuildHuffmanCodes(ReadOnlySpan<byte> codeSizes)
    {
        Span<int> symbolsUsingCodeSize = stackalloc int[32];
        foreach (var codeSize in codeSizes)
        {
            symbolsUsingCodeSize[codeSize]++;
        }

        Span<int> nextCode = stackalloc int[32];
        nextCode[0] = 0;
        nextCode[1] = 0;
        var total = 0;
        for (var i = 1; i < 31; i++)
        {
            total = (total + symbolsUsingCodeSize[i]) << 1;
            nextCode[i + 1] = total;
        }

        var result = new HuffmanCode[codeSizes.Length];
        for (var symbol = 0; symbol < codeSizes.Length; symbol++)
        {
            var codeSize = codeSizes[symbol];
            if (codeSize == 0)
            {
                continue;
            }

            var currentCode = nextCode[codeSize]++;
            var reversedCode = 0;
            for (var bit = codeSize; bit > 0; bit--, currentCode >>= 1)
            {
                reversedCode = (reversedCode << 1) | (currentCode & 1);
            }

            result[symbol] = new HuffmanCode(reversedCode, codeSize);
        }

        return result;
    }

    private readonly record struct HuffmanCode(int Code, int Length);

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bitOffset;

        public void WriteBits(int value, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                if (_bitOffset == 0)
                {
                    _bytes.Add(0);
                }

                if (((value >> i) & 1) != 0)
                {
                    _bytes[^1] |= checked((byte)(1 << _bitOffset));
                }

                _bitOffset = (_bitOffset + 1) & 7;
            }
        }

        public byte[] ToArray() => [.. _bytes];
    }

}
