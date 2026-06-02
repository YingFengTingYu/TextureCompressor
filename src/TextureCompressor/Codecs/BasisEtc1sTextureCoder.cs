using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs;

/// <summary>
/// Encodes and decodes raw BasisLZ/ETC1S payloads.
/// </summary>
/// <remarks>
/// Container formats such as .basis and KTX2 should parse their own headers and pass the ETC1S
/// codebooks, Huffman tables, and slice data to this codec.
/// </remarks>
public sealed class BasisEtc1sTextureCoder : IBasisEtc1sTextureCoder
{
    private readonly TextureFormat _format;
    private readonly bool _srgb;

    public BasisEtc1sTextureCoder(TextureFormat format)
    {
        if (!IsSupported(format))
        {
            throw new NotSupportedException($"Texture format '{format.Name}' is not a supported Basis ETC1S format.");
        }

        _format = format;
        _srgb = format.ValueKind == TextureValueKind.Srgb;
    }

    TextureFormat ITextureCoder.Format => _format;

    public static bool IsSupported(TextureFormat format) =>
        format == TextureFormats.RgbaBasisEtc1sUNorm
        || format == TextureFormats.RgbaBasisEtc1sSrgb;

    public static void Decode<TPixel>(BasisEtc1sRawPayload source, BitmapView<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Decode(source, destination, srgb: false);

    public static void Decode<TPixel>(BasisEtc1sRawPayload source, BitmapView<TPixel> destination, bool srgb)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (source.IsPFrame)
        {
            throw new NotSupportedException("Basis ETC1S P-frame slices are not implemented yet.");
        }

        var blockCountX = GetBlockCount(destination.Width);
        var blockCountY = GetBlockCount(destination.Height);

        var bitstream = new BasisEtc1sBitstreamDecoder();
        bitstream.DecodePalettes(
            source.EndpointCount,
            source.EndpointData,
            source.SelectorCount,
            source.SelectorData);
        bitstream.DecodeTables(source.TablesData);
        bitstream.DecodeRgbSlice(source.RgbSliceData, blockCountX, blockCountY, destination, srgb);

        if (!source.AlphaSliceData.IsEmpty)
        {
            bitstream.DecodeAlphaSlice(source.AlphaSliceData, blockCountX, blockCountY, destination);
        }
    }

    public static BasisEtc1sEncodedPayload Encode<TPixel>(BitmapView<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Encode(source, srgb: false);

    public static BasisEtc1sEncodedPayload Encode<TPixel>(BitmapView<TPixel> source, bool srgb)
        where TPixel : unmanaged, IPixel<TPixel> =>
        EncodeBasisEtc1s(source, srgb);

    void IBasisEtc1sTextureCoder.Decode<TPixel>(BasisEtc1sRawPayload source, BitmapView<TPixel> destination) =>
        Decode(source, destination, _srgb);

    BasisEtc1sEncodedPayload IBasisEtc1sTextureCoder.Encode<TPixel>(BitmapView<TPixel> source) =>
        Encode(source, _srgb);

    private const int BlockWidth = 4;
    private const int BlockHeight = 4;
    private const int TexelsPerBlock = BlockWidth * BlockHeight;
    private const int MaxHuffmanSymbols = 1 << 14;

    private static ReadOnlySpan<int> Etc1IntensityModifiers =>
    [
        -8, -2, 2, 8,
        -17, -5, 5, 17,
        -29, -9, 9, 29,
        -42, -13, 13, 42,
        -60, -18, 18, 60,
        -80, -24, 24, 80,
        -106, -33, 33, 106,
        -183, -47, 47, 183
    ];

    private static BasisEtc1sEncodedPayload EncodeBasisEtc1s<TPixel>(BitmapView<TPixel> source, bool srgb)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var blockCountX = GetBlockCount(source.Width);
        var blockCountY = GetBlockCount(source.Height);
        var blockCount = checked(blockCountX * blockCountY);
        ValidateSupportedBlockCount(blockCount);

        var endpointCount = checked(blockCount * 2);
        var endpoints = new BasisEtc1sEncodedEndpoint[endpointCount];
        var selectors = new BasisEtc1sEncodedSelector[endpointCount];

        Span<Rgba8UNorm> texels = stackalloc Rgba8UNorm[TexelsPerBlock];
        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                var blockIndex = (blockY * blockCountX) + blockX;
                LoadBlock(source, blockX, blockY, texels, srgb);

                EncodeColorBlock(texels, out endpoints[blockIndex], out selectors[blockIndex]);
                EncodeAlphaBlock(texels, out endpoints[blockCount + blockIndex], out selectors[blockCount + blockIndex]);
            }
        }

        var endpointData = EncodeEndpointCodebook(endpoints);
        var selectorData = EncodeSelectorCodebook(selectors);
        var huffmanTables = EncodeSliceHuffmanTables(blockCount, endpointCount);
        var colorSliceData = EncodeSliceData(blockCountX, blockCountY, endpointStart: 0, selectorStart: 0, endpointCount, selectorCount: endpointCount);
        var alphaSliceData = EncodeSliceData(blockCountX, blockCountY, endpointStart: blockCount, selectorStart: blockCount, endpointCount, selectorCount: endpointCount);

        return new BasisEtc1sEncodedPayload(
            endpointCount,
            endpointData,
            endpointCount,
            selectorData,
            huffmanTables,
            colorSliceData,
            alphaSliceData);
    }

    private static byte[] EncodeEndpointCodebook(ReadOnlySpan<BasisEtc1sEncodedEndpoint> endpoints)
    {
        var colorDeltaCodes = BasisHuffmanEncodingTable.CreateFixed(maxSymbol: 31);
        var intensityDeltaCodes = BasisHuffmanEncodingTable.CreateFixed(maxSymbol: 7);
        var writer = new BasisBitwiseWriter();

        writer.WriteHuffmanTable(colorDeltaCodes.CodeSizes);
        writer.WriteHuffmanTable(colorDeltaCodes.CodeSizes);
        writer.WriteHuffmanTable(colorDeltaCodes.CodeSizes);
        writer.WriteHuffmanTable(intensityDeltaCodes.CodeSizes);
        writer.WriteBits(0, 1);

        Span<int> previousColor5 = [16, 16, 16];
        var previousIntensity = 0;
        foreach (var endpoint in endpoints)
        {
            var intensityDelta = (endpoint.Intensity - previousIntensity) & 7;
            writer.WriteHuffmanSymbol(intensityDeltaCodes, intensityDelta);
            previousIntensity = endpoint.Intensity;

            Span<int> color5 = [endpoint.Red5, endpoint.Green5, endpoint.Blue5];
            for (var component = 0; component < 3; component++)
            {
                var delta = (color5[component] - previousColor5[component]) & 31;
                writer.WriteHuffmanSymbol(colorDeltaCodes, delta);
                previousColor5[component] = color5[component];
            }
        }

        return writer.ToArray();
    }

    private static byte[] EncodeSelectorCodebook(ReadOnlySpan<BasisEtc1sEncodedSelector> selectors)
    {
        var writer = new BasisBitwiseWriter();
        writer.WriteBits(0, 1);
        writer.WriteBits(0, 1);
        writer.WriteBits(1, 1);
        foreach (var selector in selectors)
        {
            for (var row = 0; row < BlockHeight; row++)
            {
                writer.WriteBits(selector.Rows[row], 8);
            }
        }

        return writer.ToArray();
    }

    private static byte[] EncodeSliceHuffmanTables(int blockCount, int selectorCount)
    {
        var endpointPredCodes = BasisHuffmanEncodingTable.CreateFixed(maxSymbol: 255);
        var deltaEndpointCodes = BasisHuffmanEncodingTable.CreateFixed(Math.Max(1, blockCount));
        var selectorCodes = BasisHuffmanEncodingTable.CreateFixed(selectorCount - 1);
        var selectorHistoryRleCodes = BasisHuffmanEncodingTable.CreateFixed(maxSymbol: 0);
        var writer = new BasisBitwiseWriter();

        writer.WriteHuffmanTable(endpointPredCodes.CodeSizes);
        writer.WriteHuffmanTable(deltaEndpointCodes.CodeSizes);
        writer.WriteHuffmanTable(selectorCodes.CodeSizes);
        writer.WriteHuffmanTable(selectorHistoryRleCodes.CodeSizes);
        writer.WriteBits(1, 13);
        return writer.ToArray();
    }

    private static byte[] EncodeSliceData(
        int blockCountX,
        int blockCountY,
        int endpointStart,
        int selectorStart,
        int endpointCount,
        int selectorCount)
    {
        var endpointPredCodes = BasisHuffmanEncodingTable.CreateFixed(maxSymbol: 255);
        var blockCount = checked(blockCountX * blockCountY);
        var deltaEndpointCodes = BasisHuffmanEncodingTable.CreateFixed(Math.Max(1, blockCount));
        var selectorCodes = BasisHuffmanEncodingTable.CreateFixed(selectorCount - 1);
        var writer = new BasisBitwiseWriter();
        var previousEndpointIndex = 0;

        for (var blockY = 0; blockY < blockCountY; blockY++)
        {
            for (var blockX = 0; blockX < blockCountX; blockX++)
            {
                if ((blockX & 1) == 0 && (blockY & 1) == 0)
                {
                    writer.WriteHuffmanSymbol(endpointPredCodes, CreateNoPredictionGroupSymbol(blockX, blockY, blockCountX, blockCountY));
                }

                var blockIndex = (blockY * blockCountX) + blockX;
                var endpointIndex = endpointStart + blockIndex;
                var endpointDelta = endpointIndex - previousEndpointIndex;
                if (endpointDelta < 0)
                {
                    endpointDelta += endpointCount;
                }

                writer.WriteHuffmanSymbol(deltaEndpointCodes, endpointDelta);
                previousEndpointIndex = endpointIndex;

                writer.WriteHuffmanSymbol(selectorCodes, selectorStart + blockIndex);
            }
        }

        return writer.ToArray();
    }

    private static int CreateNoPredictionGroupSymbol(int blockX, int blockY, int blockCountX, int blockCountY)
    {
        var symbol = 0;
        for (var dy = 0; dy < 2; dy++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                if (blockX + dx < blockCountX && blockY + dy < blockCountY)
                {
                    symbol |= 3 << (((dy * 2) + dx) * 2);
                }
            }
        }

        return symbol;
    }

    private static void EncodeColorBlock(
        ReadOnlySpan<Rgba8UNorm> texels,
        out BasisEtc1sEncodedEndpoint endpoint,
        out BasisEtc1sEncodedSelector selector)
    {
        var red5 = Quantize8To5(Average(texels, static pixel => pixel.Red));
        var green5 = Quantize8To5(Average(texels, static pixel => pixel.Green));
        var blue5 = Quantize8To5(Average(texels, static pixel => pixel.Blue));
        var baseRed = Expand5To8(red5);
        var baseGreen = Expand5To8(green5);
        var baseBlue = Expand5To8(blue5);
        var bestError = long.MaxValue;
        var bestIntensity = 0;
        var bestSelector = default(BasisEtc1sEncodedSelector);

        for (var intensity = 0; intensity < 8; intensity++)
        {
            var candidate = new BasisEtc1sEncodedSelector();
            var error = 0L;
            for (var y = 0; y < BlockHeight; y++)
            {
                var row = 0;
                for (var x = 0; x < BlockWidth; x++)
                {
                    var texel = texels[(y * BlockWidth) + x];
                    var selectorIndex = FindBestColorSelector(texel, baseRed, baseGreen, baseBlue, intensity, out var selectorError);
                    row |= selectorIndex << (x * 2);
                    error += selectorError;
                }

                candidate.Rows[y] = checked((byte)row);
            }

            if (error < bestError)
            {
                bestError = error;
                bestIntensity = intensity;
                bestSelector = candidate;
            }
        }

        endpoint = new BasisEtc1sEncodedEndpoint(red5, green5, blue5, bestIntensity);
        selector = bestSelector;
    }

    private static void EncodeAlphaBlock(
        ReadOnlySpan<Rgba8UNorm> texels,
        out BasisEtc1sEncodedEndpoint endpoint,
        out BasisEtc1sEncodedSelector selector)
    {
        var alpha5 = Quantize8To5(Average(texels, static pixel => pixel.Alpha));
        var baseAlpha = Expand5To8(alpha5);
        var bestError = long.MaxValue;
        var bestIntensity = 0;
        var bestSelector = default(BasisEtc1sEncodedSelector);

        for (var intensity = 0; intensity < 8; intensity++)
        {
            var candidate = new BasisEtc1sEncodedSelector();
            var error = 0L;
            for (var y = 0; y < BlockHeight; y++)
            {
                var row = 0;
                for (var x = 0; x < BlockWidth; x++)
                {
                    var alpha = texels[(y * BlockWidth) + x].Alpha;
                    var selectorIndex = FindBestAlphaSelector(alpha, baseAlpha, intensity, out var selectorError);
                    row |= selectorIndex << (x * 2);
                    error += selectorError;
                }

                candidate.Rows[y] = checked((byte)row);
            }

            if (error < bestError)
            {
                bestError = error;
                bestIntensity = intensity;
                bestSelector = candidate;
            }
        }

        endpoint = new BasisEtc1sEncodedEndpoint(alpha5, alpha5, alpha5, bestIntensity);
        selector = bestSelector;
    }

    private static int FindBestColorSelector(
        Rgba8UNorm texel,
        int baseRed,
        int baseGreen,
        int baseBlue,
        int intensity,
        out int bestError)
    {
        var bestSelector = 0;
        bestError = int.MaxValue;
        for (var selector = 0; selector < 4; selector++)
        {
            var modifier = Etc1IntensityModifiers[(intensity * 4) + selector];
            var red = ClampToByte(baseRed + modifier);
            var green = ClampToByte(baseGreen + modifier);
            var blue = ClampToByte(baseBlue + modifier);
            var error = Squared(texel.Red - red) + Squared(texel.Green - green) + Squared(texel.Blue - blue);
            if (error < bestError)
            {
                bestError = error;
                bestSelector = selector;
            }
        }

        return bestSelector;
    }

    private static int FindBestAlphaSelector(byte alpha, int baseAlpha, int intensity, out int bestError)
    {
        var bestSelector = 0;
        bestError = int.MaxValue;
        for (var selector = 0; selector < 4; selector++)
        {
            var decodedAlpha = ClampToByte(baseAlpha + Etc1IntensityModifiers[(intensity * 4) + selector]);
            var error = Squared(alpha - decodedAlpha);
            if (error < bestError)
            {
                bestError = error;
                bestSelector = selector;
            }
        }

        return bestSelector;
    }

    private static void LoadBlock<TPixel>(BitmapView<TPixel> source, int blockX, int blockY, Span<Rgba8UNorm> destination, bool srgb)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        var originX = blockX * BlockWidth;
        var originY = blockY * BlockHeight;
        var lastX = source.Width - 1;
        var lastY = source.Height - 1;
        for (var y = 0; y < BlockHeight; y++)
        {
            var sourceY = Math.Min(originY + y, lastY);
            for (var x = 0; x < BlockWidth; x++)
            {
                var sourceX = Math.Min(originX + x, lastX);
                var color = TPixel.ToRgba8UNorm(source[sourceX, sourceY]);
                destination[(y * BlockWidth) + x] = srgb ? EncodeStorageColor(color) : color;
            }
        }
    }

    private static Rgba8UNorm EncodeStorageColor(Rgba8UNorm color) => new(
        RgbaColorConversions.LinearUNorm8ToSrgb8(color.Red),
        RgbaColorConversions.LinearUNorm8ToSrgb8(color.Green),
        RgbaColorConversions.LinearUNorm8ToSrgb8(color.Blue),
        color.Alpha);

    private static Rgba8UNorm DecodeStorageColor(Rgba8UNorm color) => new(
        RgbaColorConversions.Srgb8ToLinearUNorm8(color.Red),
        RgbaColorConversions.Srgb8ToLinearUNorm8(color.Green),
        RgbaColorConversions.Srgb8ToLinearUNorm8(color.Blue),
        color.Alpha);

    private static int Average(ReadOnlySpan<Rgba8UNorm> texels, Func<Rgba8UNorm, byte> selector)
    {
        var total = 0;
        foreach (var texel in texels)
        {
            total += selector(texel);
        }

        return (total + (texels.Length / 2)) / texels.Length;
    }

    private static int GetBlockCount(int dimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimension);
        return checked((dimension + 3) / 4);
    }

    private static void ValidateSupportedBlockCount(int blockCount)
    {
        var paletteEntries = checked(blockCount * 2);
        if (paletteEntries > MaxHuffmanSymbols)
        {
            throw new NotSupportedException(
                $"The first managed Basis ETC1S encoder supports up to {MaxHuffmanSymbols / 2} blocks because it emits direct endpoint and selector symbols.");
        }
    }

    private static int Quantize8To5(int value) => Math.Clamp(((value * 31) + 127) / 255, 0, 31);

    private static int Expand5To8(int value) => (value << 3) | (value >> 2);

    private static byte ClampToByte(int value) => checked((byte)Math.Clamp(value, 0, 255));

    private static int Squared(int value) => value * value;

    private struct BasisEtc1sEncodedSelector
    {
        public byte[] Rows = new byte[BlockHeight];

        public BasisEtc1sEncodedSelector()
        {
        }
    }

    private readonly record struct BasisEtc1sEncodedEndpoint(int Red5, int Green5, int Blue5, int Intensity);

    private sealed class BasisEtc1sBitstreamDecoder
    {
        private const int BlockWidth = 4;
        private const int BlockHeight = 4;
        private const int TexelsPerBlock = BlockWidth * BlockHeight;
        private const int Color5Pal0PrevHigh = 9;
        private const int Color5Pal1PrevHigh = 21;
        private const int EndpointPredTotalSymbols = (4 * 4 * 4 * 4) + 1;
        private const int EndpointPredRepeatLastSymbol = EndpointPredTotalSymbols - 1;
        private const int EndpointPredMinRepeatCount = 3;
        private const int EndpointPredCountVlcBits = 4;
        private const int ConditionalReplenishmentEndpointPredIndex = 2;
        private const int SelectorHistoryBufferRleCountThreshold = 3;
        private const int SelectorHistoryBufferRleCountTotal = 1 << 6;

        private readonly List<BasisEtc1sEndpoint> _endpoints = [];
        private readonly List<BasisEtc1sSelector> _selectors = [];
        private readonly BasisHuffmanDecodingTable _endpointPredModel = new();
        private readonly BasisHuffmanDecodingTable _deltaEndpointModel = new();
        private readonly BasisHuffmanDecodingTable _selectorModel = new();
        private readonly BasisHuffmanDecodingTable _selectorHistoryBufferRleModel = new();

        private int _selectorHistoryBufferSize;

        private static ReadOnlySpan<int> Etc1IntensityModifiers =>
        [
            -8, -2, 2, 8,
            -17, -5, 5, 17,
            -29, -9, 9, 29,
            -42, -13, 13, 42,
            -60, -18, 18, 60,
            -80, -24, 24, 80,
            -106, -33, 33, 106,
            -183, -47, 47, 183
        ];

        public void DecodePalettes(
            int endpointCount,
            ReadOnlySpan<byte> endpointData,
            int selectorCount,
            ReadOnlySpan<byte> selectorData)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(endpointCount);
            ArgumentOutOfRangeException.ThrowIfNegative(selectorCount);

            var endpointDecoder = new BasisBitwiseDecoder(endpointData);
            var color5DeltaModel0 = endpointDecoder.ReadHuffmanTable();
            var color5DeltaModel1 = endpointDecoder.ReadHuffmanTable();
            var color5DeltaModel2 = endpointDecoder.ReadHuffmanTable();
            var intensityDeltaModel = endpointDecoder.ReadHuffmanTable();

            if (!color5DeltaModel0.IsValid || !color5DeltaModel1.IsValid || !color5DeltaModel2.IsValid || !intensityDeltaModel.IsValid)
            {
                throw new InvalidDataException("Basis ETC1S endpoint codebook contains an invalid Huffman model.");
            }

            var endpointsAreGrayscale = endpointDecoder.GetBits(1) != 0;
            _endpoints.Clear();
            _endpoints.Capacity = Math.Max(_endpoints.Capacity, endpointCount);

            Span<byte> previousColor5 = [16, 16, 16];
            var previousIntensity = 0;
            for (var i = 0; i < endpointCount; i++)
            {
                var intensityDelta = endpointDecoder.DecodeHuffman(intensityDeltaModel);
                var intensity = (intensityDelta + previousIntensity) & 7;
                previousIntensity = intensity;

                var red5 = (byte)0;
                var green5 = (byte)0;
                var blue5 = (byte)0;
                var componentCount = endpointsAreGrayscale ? 1 : 3;
                for (var component = 0; component < componentCount; component++)
                {
                    var model = previousColor5[component] <= Color5Pal0PrevHigh
                        ? color5DeltaModel0
                        : previousColor5[component] <= Color5Pal1PrevHigh
                            ? color5DeltaModel1
                            : color5DeltaModel2;

                    var delta = endpointDecoder.DecodeHuffman(model);
                    var value = (previousColor5[component] + delta) & 31;
                    switch (component)
                    {
                        case 0:
                            red5 = checked((byte)value);
                            break;
                        case 1:
                            green5 = checked((byte)value);
                            break;
                        case 2:
                            blue5 = checked((byte)value);
                            break;
                    }

                    previousColor5[component] = checked((byte)value);
                }

                if (endpointsAreGrayscale)
                {
                    green5 = red5;
                    blue5 = red5;
                }

                _endpoints.Add(new BasisEtc1sEndpoint(red5, green5, blue5, checked((byte)intensity)));
            }

            var selectorDecoder = new BasisBitwiseDecoder(selectorData);
            _selectors.Clear();
            _selectors.Capacity = Math.Max(_selectors.Capacity, selectorCount);

            var reservedSelectorHeaderBits = selectorDecoder.GetBits(2);
            if (reservedSelectorHeaderBits != 0)
            {
                throw new InvalidDataException("Basis ETC1S selector codebook reserved header bits must be zero.");
            }

            var usesRawEncoding = selectorDecoder.GetBits(1) != 0;
            if (usesRawEncoding)
            {
                Span<byte> rows = stackalloc byte[4];
                for (var i = 0; i < selectorCount; i++)
                {
                    for (var row = 0; row < 4; row++)
                    {
                        rows[row] = checked((byte)selectorDecoder.GetBits(8));
                    }

                    _selectors.Add(BasisEtc1sSelector.FromRows(rows));
                }

                return;
            }

            var deltaSelectorModel = selectorDecoder.ReadHuffmanTable();
            if (selectorCount > 1 && !deltaSelectorModel.IsValid)
            {
                throw new InvalidDataException("Basis ETC1S selector codebook contains an invalid Huffman model.");
            }

            Span<byte> previousRows = [0, 0, 0, 0];
            Span<byte> currentRows = stackalloc byte[4];
            for (var i = 0; i < selectorCount; i++)
            {
                if (i == 0)
                {
                    for (var row = 0; row < 4; row++)
                    {
                        currentRows[row] = checked((byte)selectorDecoder.GetBits(8));
                        previousRows[row] = currentRows[row];
                    }
                }
                else
                {
                    for (var row = 0; row < 4; row++)
                    {
                        var deltaRow = selectorDecoder.DecodeHuffman(deltaSelectorModel);
                        currentRows[row] = checked((byte)(deltaRow ^ previousRows[row]));
                        previousRows[row] = currentRows[row];
                    }
                }

                _selectors.Add(BasisEtc1sSelector.FromRows(currentRows));
            }
        }

        public void DecodeTables(ReadOnlySpan<byte> tableData)
        {
            var decoder = new BasisBitwiseDecoder(tableData);
            decoder.ReadHuffmanTable(_endpointPredModel);
            if (!_endpointPredModel.IsValid)
            {
                throw new InvalidDataException("Basis ETC1S endpoint predictor Huffman model is invalid.");
            }

            decoder.ReadHuffmanTable(_deltaEndpointModel);
            if (!_deltaEndpointModel.IsValid)
            {
                throw new InvalidDataException("Basis ETC1S endpoint delta Huffman model is invalid.");
            }

            decoder.ReadHuffmanTable(_selectorModel);
            if (!_selectorModel.IsValid)
            {
                throw new InvalidDataException("Basis ETC1S selector Huffman model is invalid.");
            }

            decoder.ReadHuffmanTable(_selectorHistoryBufferRleModel);
            if (!_selectorHistoryBufferRleModel.IsValid)
            {
                throw new InvalidDataException("Basis ETC1S selector history RLE Huffman model is invalid.");
            }

            _selectorHistoryBufferSize = decoder.GetBits(13);
            if (_selectorHistoryBufferSize <= 0)
            {
                throw new InvalidDataException("Basis ETC1S selector history buffer size must be positive.");
            }
        }

        public void DecodeRgbSlice<TPixel>(ReadOnlySpan<byte> imageData, int blockCountX, int blockCountY, BitmapView<TPixel> destination, bool srgb)
            where TPixel : unmanaged, IPixel<TPixel> =>
            DecodeSlice(imageData, blockCountX, blockCountY, destination, alphaOnly: false, srgb: srgb);

        public void DecodeAlphaSlice<TPixel>(ReadOnlySpan<byte> imageData, int blockCountX, int blockCountY, BitmapView<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel> =>
            DecodeSlice(imageData, blockCountX, blockCountY, destination, alphaOnly: true, srgb: false);

        private void DecodeSlice<TPixel>(ReadOnlySpan<byte> imageData, int blockCountX, int blockCountY, BitmapView<TPixel> destination, bool alphaOnly, bool srgb)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockCountX);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockCountY);
            if (_endpoints.Count == 0 || _selectors.Count == 0)
            {
                throw new InvalidOperationException("Basis ETC1S palettes must be decoded before slice data.");
            }

            if (_selectorHistoryBufferSize <= 0)
            {
                throw new InvalidOperationException("Basis ETC1S Huffman tables must be decoded before slice data.");
            }

            var decoder = new BasisBitwiseDecoder(imageData);
            var selectorHistory = new BasisApproximateMoveToFront(_selectorHistoryBufferSize);
            var selectorRleCount = 0;
            var endpointPredRows = new BasisEtc1sEndpointPredictionRow[2, blockCountX];
            var previousEndpointPredSymbol = 0;
            var endpointPredRepeatCount = 0;
            var previousEndpointIndex = 0;
            var selectorHistoryFirstSymbolIndex = _selectors.Count;
            var selectorHistoryRleSymbolIndex = _selectorHistoryBufferSize + selectorHistoryFirstSymbolIndex;

            Span<Rgba8UNorm> blockPixels = stackalloc Rgba8UNorm[TexelsPerBlock];
            for (var blockY = 0; blockY < blockCountY; blockY++)
            {
                var currentPredictionRow = blockY & 1;
                var currentPredBits = 0;

                for (var blockX = 0; blockX < blockCountX; blockX++)
                {
                    if ((blockX & 1) == 0)
                    {
                        if ((blockY & 1) == 0)
                        {
                            if (endpointPredRepeatCount > 0)
                            {
                                endpointPredRepeatCount--;
                                currentPredBits = previousEndpointPredSymbol;
                            }
                            else
                            {
                                currentPredBits = decoder.DecodeHuffman(_endpointPredModel);
                                if (currentPredBits == EndpointPredRepeatLastSymbol)
                                {
                                    endpointPredRepeatCount = decoder.DecodeVlc(EndpointPredCountVlcBits) + EndpointPredMinRepeatCount - 1;
                                    currentPredBits = previousEndpointPredSymbol;
                                }
                                else
                                {
                                    previousEndpointPredSymbol = currentPredBits;
                                }
                            }

                            endpointPredRows[currentPredictionRow ^ 1, blockX].PredictionBits = currentPredBits >> 4;
                        }
                        else
                        {
                            currentPredBits = endpointPredRows[currentPredictionRow, blockX].PredictionBits;
                        }
                    }

                    var prediction = currentPredBits & 3;
                    currentPredBits >>= 2;

                    int endpointIndex;
                    var selectorIndex = 0;
                    switch (prediction)
                    {
                        case 0:
                            if (blockX == 0)
                            {
                                throw new InvalidDataException("Basis ETC1S endpoint stream used a left predictor on the left edge.");
                            }

                            endpointIndex = previousEndpointIndex;
                            break;
                        case 1:
                            if (blockY == 0)
                            {
                                throw new InvalidDataException("Basis ETC1S endpoint stream used an upper predictor on the top edge.");
                            }

                            endpointIndex = endpointPredRows[currentPredictionRow ^ 1, blockX].EndpointIndex;
                            break;
                        case ConditionalReplenishmentEndpointPredIndex:
                            if (blockX == 0 || blockY == 0)
                            {
                                throw new InvalidDataException("Basis ETC1S endpoint stream used an upper-left predictor on an image edge.");
                            }

                            endpointIndex = endpointPredRows[currentPredictionRow ^ 1, blockX - 1].EndpointIndex;
                            break;
                        default:
                            var deltaSymbol = decoder.DecodeHuffman(_deltaEndpointModel);
                            endpointIndex = deltaSymbol + previousEndpointIndex;
                            if (endpointIndex >= _endpoints.Count)
                            {
                                endpointIndex -= _endpoints.Count;
                            }

                            break;
                    }

                    endpointPredRows[currentPredictionRow, blockX].EndpointIndex = endpointIndex;
                    previousEndpointIndex = endpointIndex;

                    int selectorSymbol;
                    if (selectorRleCount > 0)
                    {
                        selectorRleCount--;
                        selectorSymbol = _selectors.Count;
                    }
                    else
                    {
                        selectorSymbol = decoder.DecodeHuffman(_selectorModel);
                        if (selectorSymbol == selectorHistoryRleSymbolIndex)
                        {
                            var runSymbol = decoder.DecodeHuffman(_selectorHistoryBufferRleModel);
                            selectorRleCount = runSymbol == SelectorHistoryBufferRleCountTotal - 1
                                ? decoder.DecodeVlc(7) + SelectorHistoryBufferRleCountThreshold
                                : runSymbol + SelectorHistoryBufferRleCountThreshold;
                            if (selectorRleCount > blockCountX * blockCountY)
                            {
                                throw new InvalidDataException("Basis ETC1S selector history RLE count is invalid.");
                            }

                            selectorSymbol = _selectors.Count;
                            selectorRleCount--;
                        }
                    }

                    if (selectorSymbol >= _selectors.Count)
                    {
                        var historyBufferIndex = selectorSymbol - _selectors.Count;
                        if (historyBufferIndex >= selectorHistory.Count)
                        {
                            throw new InvalidDataException("Basis ETC1S selector history index is invalid.");
                        }

                        selectorIndex = selectorHistory[historyBufferIndex];
                        if (historyBufferIndex != 0)
                        {
                            selectorHistory.Use(historyBufferIndex);
                        }
                    }
                    else
                    {
                        selectorIndex = selectorSymbol;
                        selectorHistory.Add(selectorIndex);
                    }

                    if ((uint)endpointIndex >= (uint)_endpoints.Count || (uint)selectorIndex >= (uint)_selectors.Count)
                    {
                        throw new InvalidDataException("Basis ETC1S block references an endpoint or selector outside its codebook.");
                    }

                    if (alphaOnly)
                    {
                        DecodeAlphaBlock(_endpoints[endpointIndex], _selectors[selectorIndex], blockPixels);
                        CopyAlphaBlock(blockPixels, destination, blockX, blockY);
                    }
                    else
                    {
                        DecodeRgbBlock(_endpoints[endpointIndex], _selectors[selectorIndex], blockPixels);
                        CopyColorBlock(blockPixels, destination, blockX, blockY, srgb);
                    }
                }
            }
        }

        private static void DecodeRgbBlock(BasisEtc1sEndpoint endpoint, BasisEtc1sSelector selector, Span<Rgba8UNorm> destination)
        {
            Span<Rgba8UNorm> colors = stackalloc Rgba8UNorm[4];
            DecodeEndpointColors(endpoint, colors);
            for (var y = 0; y < BlockHeight; y++)
            {
                var selectorRow = selector.Rows[y];
                for (var x = 0; x < BlockWidth; x++)
                {
                    destination[(y * BlockWidth) + x] = colors[(selectorRow >> (x * 2)) & 3];
                }
            }
        }

        private static void DecodeAlphaBlock(BasisEtc1sEndpoint endpoint, BasisEtc1sSelector selector, Span<Rgba8UNorm> destination)
        {
            Span<byte> alphaValues = stackalloc byte[4];
            DecodeEndpointAlphaValues(endpoint, alphaValues);
            for (var y = 0; y < BlockHeight; y++)
            {
                var selectorRow = selector.Rows[y];
                for (var x = 0; x < BlockWidth; x++)
                {
                    var alpha = alphaValues[(selectorRow >> (x * 2)) & 3];
                    destination[(y * BlockWidth) + x] = new Rgba8UNorm(0, 0, 0, alpha);
                }
            }
        }

        private static void DecodeEndpointColors(BasisEtc1sEndpoint endpoint, Span<Rgba8UNorm> colors)
        {
            var red = Expand5To8(endpoint.Red5);
            var green = Expand5To8(endpoint.Green5);
            var blue = Expand5To8(endpoint.Blue5);
            for (var i = 0; i < 4; i++)
            {
                var modifier = Etc1IntensityModifiers[(endpoint.Intensity * 4) + i];
                colors[i] = new Rgba8UNorm(
                    ClampToByte(red + modifier),
                    ClampToByte(green + modifier),
                    ClampToByte(blue + modifier));
            }
        }

        private static void DecodeEndpointAlphaValues(BasisEtc1sEndpoint endpoint, Span<byte> alphaValues)
        {
            var alpha = Expand5To8(endpoint.Green5);
            for (var i = 0; i < 4; i++)
            {
                alphaValues[i] = ClampToByte(alpha + Etc1IntensityModifiers[(endpoint.Intensity * 4) + i]);
            }
        }

        private static void CopyColorBlock<TPixel>(ReadOnlySpan<Rgba8UNorm> source, BitmapView<TPixel> destination, int blockX, int blockY, bool srgb)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var xCount = Math.Min(BlockWidth, destination.Width - (blockX * BlockWidth));
            var yCount = Math.Min(BlockHeight, destination.Height - (blockY * BlockHeight));
            for (var y = 0; y < yCount; y++)
            {
                for (var x = 0; x < xCount; x++)
                {
                    var color = source[(y * BlockWidth) + x];
                    destination[(blockX * BlockWidth) + x, (blockY * BlockHeight) + y] =
                        TPixel.FromRgba8UNorm(srgb ? DecodeStorageColor(color) : color);
                }
            }
        }

        private static void CopyAlphaBlock<TPixel>(ReadOnlySpan<Rgba8UNorm> source, BitmapView<TPixel> destination, int blockX, int blockY)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var xCount = Math.Min(BlockWidth, destination.Width - (blockX * BlockWidth));
            var yCount = Math.Min(BlockHeight, destination.Height - (blockY * BlockHeight));
            for (var y = 0; y < yCount; y++)
            {
                for (var x = 0; x < xCount; x++)
                {
                    var destinationX = (blockX * BlockWidth) + x;
                    var destinationY = (blockY * BlockHeight) + y;
                    var color = TPixel.ToRgba8UNorm(destination[destinationX, destinationY]);
                    destination[destinationX, destinationY] =
                        TPixel.FromRgba8UNorm(new Rgba8UNorm(color.Red, color.Green, color.Blue, source[(y * BlockWidth) + x].Alpha));
                }
            }
        }

        private static int Expand5To8(int value) => (value << 3) | (value >> 2);

        private static byte ClampToByte(int value) => checked((byte)Math.Clamp(value, 0, 255));

        private readonly record struct BasisEtc1sEndpoint(byte Red5, byte Green5, byte Blue5, byte Intensity);

        private struct BasisEtc1sEndpointPredictionRow
        {
            public int PredictionBits;
            public int EndpointIndex;
        }

        private readonly struct BasisEtc1sSelector
        {
            private BasisEtc1sSelector(byte row0, byte row1, byte row2, byte row3)
            {
                Rows = [row0, row1, row2, row3];
                var low = 3;
                var high = 0;
                var unique = 0;
                Span<int> histogram = [0, 0, 0, 0];
                for (var y = 0; y < 4; y++)
                {
                    var row = Rows[y];
                    for (var x = 0; x < 4; x++)
                    {
                        histogram[(row >> (x * 2)) & 3]++;
                    }
                }

                for (var i = 0; i < 4; i++)
                {
                    if (histogram[i] == 0)
                    {
                        continue;
                    }

                    unique++;
                    low = Math.Min(low, i);
                    high = Math.Max(high, i);
                }

                LowSelector = checked((byte)low);
                HighSelector = checked((byte)high);
                UniqueSelectorCount = checked((byte)unique);
            }

            public byte[] Rows { get; }

            public byte LowSelector { get; }

            public byte HighSelector { get; }

            public byte UniqueSelectorCount { get; }

            public static BasisEtc1sSelector FromRows(ReadOnlySpan<byte> rows) =>
                new(rows[0], rows[1], rows[2], rows[3]);
        }
    }

    private sealed class BasisBitwiseDecoder
    {
        private readonly ReadOnlyMemory<byte> _source;
        private int _byteOffset;
        private uint _bitBuffer;
        private int _bitBufferSize;

        public BasisBitwiseDecoder(ReadOnlySpan<byte> source)
        {
            _source = source.ToArray();
        }

        public uint PeekBits(int bitCount)
        {
            if (bitCount == 0)
            {
                return 0;
            }

            if ((uint)bitCount > 25)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount));
            }

            while (_bitBufferSize < bitCount)
            {
                var next = _byteOffset < _source.Length ? _source.Span[_byteOffset++] : 0;
                _bitBuffer |= (uint)(next << _bitBufferSize);
                _bitBufferSize += 8;
            }

            return _bitBuffer & ((1u << bitCount) - 1);
        }

        public void RemoveBits(int bitCount)
        {
            if (bitCount > _bitBufferSize)
            {
                throw new InvalidDataException("Basis bitstream attempted to remove more bits than were buffered.");
            }

            _bitBuffer >>= bitCount;
            _bitBufferSize -= bitCount;
        }

        public int GetBits(int bitCount)
        {
            if (bitCount > 25)
            {
                if (bitCount > 32)
                {
                    throw new ArgumentOutOfRangeException(nameof(bitCount));
                }

                var low = PeekBits(25);
                RemoveBits(25);
                var remaining = bitCount - 25;
                var high = PeekBits(remaining);
                RemoveBits(remaining);
                return checked((int)(low | (high << 25)));
            }

            var bits = PeekBits(bitCount);
            RemoveBits(bitCount);
            return checked((int)bits);
        }

        public int DecodeVlc(int chunkBits)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkBits);
            var chunkSize = 1 << chunkBits;
            var chunkMask = chunkSize - 1;
            var value = 0;
            var offset = 0;

            while (true)
            {
                var symbol = GetBits(chunkBits + 1);
                value |= (symbol & chunkMask) << offset;
                offset += chunkBits;
                if ((symbol & chunkSize) == 0)
                {
                    return value;
                }

                if (offset >= 32)
                {
                    throw new InvalidDataException("Basis VLC value exceeded 32 bits.");
                }
            }
        }

        public int DecodeHuffman(BasisHuffmanDecodingTable table) => table.Decode(this);

        public BasisHuffmanDecodingTable ReadHuffmanTable()
        {
            var table = new BasisHuffmanDecodingTable();
            ReadHuffmanTable(table);
            return table;
        }

        public void ReadHuffmanTable(BasisHuffmanDecodingTable table)
        {
            table.Clear();
            var totalUsedSymbols = GetBits(BasisHuffmanDecodingTable.MaxSymbolsLog2);
            if (totalUsedSymbols == 0)
            {
                return;
            }

            if (totalUsedSymbols > BasisHuffmanDecodingTable.MaxSymbols)
            {
                throw new InvalidDataException("Basis Huffman table has too many symbols.");
            }

            Span<byte> codeLengthCodeSizes = stackalloc byte[BasisHuffmanDecodingTable.TotalCodeLengthCodes];
            var codeLengthCodeCount = GetBits(5);
            if (codeLengthCodeCount < 1 || codeLengthCodeCount > BasisHuffmanDecodingTable.TotalCodeLengthCodes)
            {
                throw new InvalidDataException("Basis Huffman table has an invalid code-length code count.");
            }

            for (var i = 0; i < codeLengthCodeCount; i++)
            {
                codeLengthCodeSizes[BasisHuffmanDecodingTable.SortedCodeLengthCodes[i]] = checked((byte)GetBits(3));
            }

            var codeLengthTable = new BasisHuffmanDecodingTable();
            codeLengthTable.Initialize(codeLengthCodeSizes);
            if (!codeLengthTable.IsValid)
            {
                throw new InvalidDataException("Basis Huffman table has an invalid code-length Huffman model.");
            }

            var codeSizes = new byte[totalUsedSymbols];
            var current = 0;
            while (current < totalUsedSymbols)
            {
                var code = DecodeHuffman(codeLengthTable);
                switch (code)
                {
                    case <= 16:
                        codeSizes[current++] = checked((byte)code);
                        break;
                    case BasisHuffmanDecodingTable.SmallZeroRunCode:
                        current += GetBits(BasisHuffmanDecodingTable.SmallZeroRunExtraBits) + BasisHuffmanDecodingTable.SmallZeroRunSizeMin;
                        break;
                    case BasisHuffmanDecodingTable.BigZeroRunCode:
                        current += GetBits(BasisHuffmanDecodingTable.BigZeroRunExtraBits) + BasisHuffmanDecodingTable.BigZeroRunSizeMin;
                        break;
                    default:
                        if (current == 0)
                        {
                            throw new InvalidDataException("Basis Huffman repeat run has no previous code length.");
                        }

                        var runLength = code == BasisHuffmanDecodingTable.SmallRepeatCode
                            ? GetBits(BasisHuffmanDecodingTable.SmallRepeatExtraBits) + BasisHuffmanDecodingTable.SmallRepeatSizeMin
                            : GetBits(BasisHuffmanDecodingTable.BigRepeatExtraBits) + BasisHuffmanDecodingTable.BigRepeatSizeMin;
                        var previous = codeSizes[current - 1];
                        if (previous == 0)
                        {
                            throw new InvalidDataException("Basis Huffman repeat run attempted to repeat a zero code length.");
                        }

                        do
                        {
                            if (current >= totalUsedSymbols)
                            {
                                throw new InvalidDataException("Basis Huffman repeat run exceeded symbol count.");
                            }

                            codeSizes[current++] = previous;
                        }
                        while (--runLength > 0);
                        break;
                }

                if (current > totalUsedSymbols)
                {
                    throw new InvalidDataException("Basis Huffman zero run exceeded symbol count.");
                }
            }

            table.Initialize(codeSizes);
        }
    }

    private sealed class BasisHuffmanDecodingTable
    {
        public const int MaxSupportedInternalCodeSize = 31;
        public const int MaxSymbolsLog2 = 14;
        public const int MaxSymbols = 1 << MaxSymbolsLog2;
        public const int TotalCodeLengthCodes = 21;
        public const int SmallZeroRunSizeMin = 3;
        public const int SmallZeroRunExtraBits = 3;
        public const int BigZeroRunSizeMin = 11;
        public const int BigZeroRunExtraBits = 7;
        public const int SmallRepeatSizeMin = 3;
        public const int SmallRepeatExtraBits = 2;
        public const int BigRepeatSizeMin = 7;
        public const int BigRepeatExtraBits = 7;
        public const int SmallZeroRunCode = 17;
        public const int BigZeroRunCode = 18;
        public const int SmallRepeatCode = 19;
        public const int BigRepeatCode = 20;

        public static ReadOnlySpan<byte> SortedCodeLengthCodes =>
        [
            SmallZeroRunCode, BigZeroRunCode, SmallRepeatCode, BigRepeatCode,
            0, 8, 7, 9, 6, 0x0a, 5, 0x0b, 4, 0x0c, 3, 0x0d, 2, 0x0e, 1, 0x0f, 0x10
        ];

        private readonly Dictionary<(int Length, int Code), int> _symbols = [];
        private int _maxCodeSize;

        public bool IsValid => _symbols.Count > 0;

        public void Clear()
        {
            _symbols.Clear();
            _maxCodeSize = 0;
        }

        public void Initialize(ReadOnlySpan<byte> codeSizes)
        {
            Clear();
            if (codeSizes.Length == 0)
            {
                return;
            }

            Span<int> symbolsUsingCodeSize = stackalloc int[MaxSupportedInternalCodeSize + 1];
            for (var i = 0; i < codeSizes.Length; i++)
            {
                if (codeSizes[i] > MaxSupportedInternalCodeSize)
                {
                    throw new InvalidDataException("Basis Huffman code size exceeds the supported maximum.");
                }

                symbolsUsingCodeSize[codeSizes[i]]++;
            }

            Span<int> nextCode = stackalloc int[MaxSupportedInternalCodeSize + 1];
            nextCode[0] = 0;
            nextCode[1] = 0;

            var usedSymbols = 0;
            var total = 0;
            for (var i = 1; i < MaxSupportedInternalCodeSize; i++)
            {
                usedSymbols += symbolsUsingCodeSize[i];
                total = (total + symbolsUsingCodeSize[i]) << 1;
                nextCode[i + 1] = total;
            }

            if ((1u << MaxSupportedInternalCodeSize) != (uint)total && usedSymbols != 1)
            {
                throw new InvalidDataException("Basis Huffman code lengths do not form a complete prefix code.");
            }

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

                if (!_symbols.TryAdd((codeSize, reversedCode), symbol))
                {
                    throw new InvalidDataException("Basis Huffman code lengths produced a duplicate code.");
                }

                _maxCodeSize = Math.Max(_maxCodeSize, codeSize);
            }
        }

        public int Decode(BasisBitwiseDecoder decoder)
        {
            if (!IsValid)
            {
                throw new InvalidDataException("Basis Huffman model is empty.");
            }

            var code = 0;
            for (var length = 1; length <= _maxCodeSize; length++)
            {
                code |= decoder.GetBits(1) << (length - 1);
                if (_symbols.TryGetValue((length, code), out var symbol))
                {
                    return symbol;
                }
            }

            throw new InvalidDataException("Basis Huffman stream contains an invalid code.");
        }
    }

    private sealed class BasisApproximateMoveToFront
    {
        private readonly int[] _values;
        private int _rover;

        public BasisApproximateMoveToFront(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            _values = new int[count];
            _rover = count / 2;
        }

        public int Count => _values.Length;

        public int this[int index] => _values[index];

        public void Add(int value)
        {
            _values[_rover++] = value;
            if (_rover == _values.Length)
            {
                _rover = _values.Length / 2;
            }
        }

        public void Use(int index)
        {
            if (index == 0)
            {
                return;
            }

            (_values[index / 2], _values[index]) = (_values[index], _values[index / 2]);
        }
    }

    private sealed class BasisBitwiseWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bitOffset;

        public void WriteBits(int value, int bitCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bitCount);
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

        public void WriteHuffmanTable(ReadOnlySpan<byte> codeSizes)
        {
            WriteBits(codeSizes.Length, BasisHuffmanDecodingTable.MaxSymbolsLog2);
            if (codeSizes.Length == 0)
            {
                return;
            }

            Span<byte> codeLengthCodeSizes = stackalloc byte[BasisHuffmanDecodingTable.TotalCodeLengthCodes];
            foreach (var codeSize in codeSizes)
            {
                codeLengthCodeSizes[codeSize] = 1;
            }

            var codeLengthCodeCount = 0;
            for (var i = 0; i < BasisHuffmanDecodingTable.SortedCodeLengthCodes.Length; i++)
            {
                if (codeLengthCodeSizes[BasisHuffmanDecodingTable.SortedCodeLengthCodes[i]] != 0)
                {
                    codeLengthCodeCount = i + 1;
                }
            }

            WriteBits(codeLengthCodeCount, 5);
            for (var i = 0; i < codeLengthCodeCount; i++)
            {
                WriteBits(codeLengthCodeSizes[BasisHuffmanDecodingTable.SortedCodeLengthCodes[i]], 3);
            }

            var codeLengthCodes = BasisHuffmanEncodingTable.Build(codeLengthCodeSizes);
            foreach (var codeSize in codeSizes)
            {
                WriteHuffmanSymbol(codeLengthCodes, codeSize);
            }
        }

        public void WriteHuffmanSymbol(BasisHuffmanEncodingTable table, int symbol)
        {
            var code = table[symbol];
            WriteBits(code.Code, code.Length);
        }

        public byte[] ToArray() => [.. _bytes];
    }

    private sealed class BasisHuffmanEncodingTable
    {
        private readonly BasisHuffmanCode[] _codes;

        private BasisHuffmanEncodingTable(byte[] codeSizes, BasisHuffmanCode[] codes)
        {
            CodeSizes = codeSizes;
            _codes = codes;
        }

        public byte[] CodeSizes { get; }

        public BasisHuffmanCode this[int symbol] => _codes[symbol];

        public static BasisHuffmanEncodingTable CreateFixed(int maxSymbol)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxSymbol);
            var symbolCount = NextPowerOfTwo(maxSymbol + 1);
            if (symbolCount > BasisHuffmanDecodingTable.MaxSymbols)
            {
                throw new NotSupportedException("Basis Huffman tables support at most 16384 symbols.");
            }

            var codeSize = symbolCount == 1 ? 1 : Log2(symbolCount);
            var codeSizes = new byte[symbolCount];
            Array.Fill(codeSizes, checked((byte)codeSize));
            return Build(codeSizes);
        }

        public static BasisHuffmanEncodingTable Build(ReadOnlySpan<byte> codeSizes)
        {
            var copiedCodeSizes = codeSizes.ToArray();
            Span<int> symbolsUsingCodeSize = stackalloc int[BasisHuffmanDecodingTable.MaxSupportedInternalCodeSize + 1];
            foreach (var codeSize in codeSizes)
            {
                if (codeSize > BasisHuffmanDecodingTable.MaxSupportedInternalCodeSize)
                {
                    throw new InvalidDataException("Basis Huffman code size exceeds the supported maximum.");
                }

                symbolsUsingCodeSize[codeSize]++;
            }

            Span<int> nextCode = stackalloc int[BasisHuffmanDecodingTable.MaxSupportedInternalCodeSize + 1];
            nextCode[0] = 0;
            nextCode[1] = 0;
            var total = 0;
            for (var i = 1; i < BasisHuffmanDecodingTable.MaxSupportedInternalCodeSize; i++)
            {
                total = (total + symbolsUsingCodeSize[i]) << 1;
                nextCode[i + 1] = total;
            }

            var codes = new BasisHuffmanCode[codeSizes.Length];
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

                codes[symbol] = new BasisHuffmanCode(reversedCode, codeSize);
            }

            return new BasisHuffmanEncodingTable(copiedCodeSizes, codes);
        }

        private static int NextPowerOfTwo(int value)
        {
            var result = 1;
            while (result < value)
            {
                result <<= 1;
            }

            return result;
        }

        private static int Log2(int powerOfTwo)
        {
            var result = 0;
            while ((1 << result) < powerOfTwo)
            {
                result++;
            }

            return result;
        }
    }

    private readonly record struct BasisHuffmanCode(int Code, int Length);
}
