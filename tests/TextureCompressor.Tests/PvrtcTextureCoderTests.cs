using System.Runtime.InteropServices;
using PVRTexLib;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;
using TextureCompressor.Registry;
using TextureCompressor.Options;

namespace TextureCompressor.Tests;

public sealed unsafe class PvrtcTextureCoderTests
{
    private const int Width = 32;
    private const int Height = 32;
    private const int PixelTolerance = 36;

    private static readonly ulong Rgba8PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 8, 8, 8, 8);
    private static readonly ulong Rgba32PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 32, 32, 32, 32);

    [Theory]
    [MemberData(nameof(PvrtcFormats))]
    public void GlobalManagerFindsPvrtcTextureCoders(TextureFormat format)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);

        Assert.True(PvrtcTextureCoder.IsSupported(format));
        Assert.IsType<PvrtcTextureCoder>(coder);
    }

    [Fact]
    public void ConstructorStoresCompressionOptions()
    {
        var options = new TextureCompressionOptions { CompressionMode = TextureCompressionLevel.High };
        var coder = new PvrtcTextureCoder(TextureFormats.RgbaPvrtcII4BppUNorm, options);

        Assert.Same(options, coder.Options);
    }

    [Theory]
    [MemberData(nameof(PvrtcFormats))]
    public void DecodePvrtTexLibPayloadMatchesPvrtTexLib(TextureFormat format)
    {
        var source = CreateSource(format);
        var payload = EncodeWithPvrTexLib(format, Width, Height, source);
        var expected = DecodeWithPvrTexLib(format, Width, Height, payload);
        var actual = new ArrayBitmap<Rgba8UNorm>(Width, Height);
        var coder = new PvrtcTextureCoder(format);

        coder.Decode(payload, actual.AsView());

        AssertPixelsNear(expected, actual.Pixels, format);
    }

    [Theory]
    [MemberData(nameof(PvrtcFormats))]
    public void EncodePayloadIsDecodableByPvrtTexLib(TextureFormat format)
    {
        var source = new ArrayBitmap<Rgba8UNorm>(Width, Height, CreateSource(format));
        var coder = new PvrtcTextureCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(Width, Height)];
        var actual = new ArrayBitmap<Rgba8UNorm>(Width, Height);

        coder.Encode(source.AsView(), payload);
        var expected = DecodeWithPvrTexLib(format, Width, Height, payload);
        coder.Decode(payload, actual.AsView());

        AssertAverageErrorIsReasonable(source.Pixels, expected, format);
        AssertPixelsNear(expected, actual.Pixels, format);
    }

    [Theory]
    [MemberData(nameof(HdrPvrtcFormats))]
    public void DecodePvrtTexLibHdrPayloadMatchesPvrtTexLibFloat(TextureFormat format)
    {
        var source = CreateHdrSource(Width, Height);
        var payload = EncodeFloatWithPvrTexLib(format, Width, Height, source);
        var expected = DecodeFloatWithPvrTexLib(format, Width, Height, payload);
        var actual = new ArrayBitmap<Rgba32Float>(Width, Height);
        var coder = new PvrtcTextureCoder(format);

        coder.Decode(payload, actual.AsView());

        AssertFloatPixelsNear(expected, actual.Pixels, format, absoluteTolerance: 0.5f);
    }

    [Theory]
    [MemberData(nameof(HdrPvrtcFormats))]
    public void EncodeHdrPayloadIsDecodableByPvrtTexLibFloat(TextureFormat format)
    {
        var pixels = CreateHdrSource(Width, Height);
        var source = new ArrayBitmap<Rgba32Float>(Width, Height, pixels);
        var coder = new PvrtcTextureCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(Width, Height)];

        coder.Encode(source.AsView(), payload);
        var decoded = DecodeFloatWithPvrTexLib(format, Width, Height, payload);

        AssertAverageFloatErrorIsReasonable(pixels, decoded, format, maxAverageError: 1.25f);
    }

    [Theory]
    [MemberData(nameof(RgbPvrtcFormats))]
    public void EncodeRgbPayloadIgnoresSourceAlpha(TextureFormat format)
    {
        var opaquePixels = CreateSource(format);
        var variedAlphaPixels = new Rgba8UNorm[opaquePixels.Length];
        for (var i = 0; i < variedAlphaPixels.Length; i++)
        {
            var color = opaquePixels[i];
            color.Alpha = (byte)(17 + (i * 31 % 211));
            variedAlphaPixels[i] = color;
        }

        var opaque = new ArrayBitmap<Rgba8UNorm>(Width, Height, opaquePixels);
        var variedAlpha = new ArrayBitmap<Rgba8UNorm>(Width, Height, variedAlphaPixels);
        var coder = new PvrtcTextureCoder(format);
        var opaquePayload = new byte[coder.GetEncodedByteCount(Width, Height)];
        var variedAlphaPayload = new byte[opaquePayload.Length];

        coder.Encode(opaque.AsView(), opaquePayload);
        coder.Encode(variedAlpha.AsView(), variedAlphaPayload);

        Assert.Equal(opaquePayload, variedAlphaPayload);
    }

    [Fact]
    public void PvrtcIRejectsNonPowerOfTwoDimensions()
    {
        var coder = new PvrtcTextureCoder(TextureFormats.RgbPvrtcI4BppUNorm);
        var pixels = new ArrayBitmap<Rgba8UNorm>(12, 8);
        var payload = new byte[12 * 8 / 2];

        Assert.Throws<ArgumentException>(() => coder.Encode(pixels.AsView(), payload));
        Assert.Throws<ArgumentException>(() => coder.Decode(payload, pixels.AsView()));
    }

    [Theory]
    [MemberData(nameof(PvrtcStorageSizeCases))]
    public void EncodedByteCountMatchesPvrtTexLibStorage(TextureFormat format, int width, int height, int expectedBytes)
    {
        var coder = new PvrtcTextureCoder(format);

        Assert.Equal(expectedBytes, coder.GetEncodedByteCount(width, height));
        Assert.Equal(expectedBytes, PvrtcTextureCoder.GetEncodedByteCount(format, width, height));
        Assert.Equal(expectedBytes, format.GetByteCount(width, height));
    }

    [Theory]
    [MemberData(nameof(PvrtcEdgeCases))]
    public void DecodePvrtTexLibEdgePayloadMatchesPvrtTexLib(TextureFormat format, int width, int height)
    {
        var source = CreateSource(format, width, height);
        var payload = EncodeWithPvrTexLib(format, width, height, source);
        var expected = DecodeWithPvrTexLib(format, width, height, payload);
        var actual = new ArrayBitmap<Rgba8UNorm>(width, height);
        var coder = new PvrtcTextureCoder(format);

        coder.Decode(payload, actual.AsView());

        Assert.Equal(PvrtcTextureCoder.GetEncodedByteCount(format, width, height), payload.Length);
        AssertPixelsNear(expected, actual.Pixels, format);
    }

    [Theory]
    [MemberData(nameof(PvrtcEdgeCases))]
    public void EncodeEdgePayloadIsDecodableByPvrtTexLib(TextureFormat format, int width, int height)
    {
        var pixels = CreateSource(format, width, height);
        var source = new ArrayBitmap<Rgba8UNorm>(width, height, pixels);
        var coder = new PvrtcTextureCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(width, height)];

        coder.Encode(source.AsView(), payload);
        var decoded = DecodeWithPvrTexLib(format, width, height, payload);

        AssertAverageErrorIsReasonable(pixels, decoded, format);
    }

    [Fact]
    public void DecodeSrgbUsesRgba8CarrierForNonFloatPixel()
    {
        var format = TextureFormats.RgbaPvrtcI4BppSrgb;
        var payload = EncodeWithPvrTexLib(format, Width, Height, CreateSource(format));
        var decoded = new ArrayBitmap<Rgba8OnlyPixel>(Width, Height);
        var coder = new PvrtcTextureCoder(format);

        coder.Decode(payload, decoded.AsView());

        Assert.NotEqual(0, decoded.Pixels[0].Alpha);
    }

    [Fact]
    public void EncodeSrgbUsesRgba8CarrierForNonFloatPixel()
    {
        var format = TextureFormats.RgbaPvrtcI4BppSrgb;
        var pixels = CreateSource(format);
        var source = new Rgba8OnlyPixel[pixels.Length];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = Rgba8OnlyPixel.FromRgba8UNorm(pixels[i]);
        }

        var bitmap = new ArrayBitmap<Rgba8OnlyPixel>(Width, Height, source);
        var coder = new PvrtcTextureCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(Width, Height)];

        coder.Encode(bitmap.AsView(), payload);
        var decoded = DecodeWithPvrTexLib(format, Width, Height, payload);

        AssertAverageErrorIsReasonable(pixels, decoded, format);
    }

    public static TheoryData<TextureFormat> PvrtcFormats() => new()
    {
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
    };

    public static TheoryData<TextureFormat> RgbPvrtcFormats() => new()
    {
        TextureFormats.RgbPvrtcI2BppUNorm,
        TextureFormats.RgbPvrtcI4BppUNorm
    };

    public static TheoryData<TextureFormat> HdrPvrtcFormats() => new()
    {
        TextureFormats.RgbPvrtcI6BppFloat,
        TextureFormats.RgbPvrtcI8BppFloat,
        TextureFormats.RgbPvrtcII6BppFloat,
        TextureFormats.RgbPvrtcII8BppFloat
    };

    public static TheoryData<TextureFormat, int, int, int> PvrtcStorageSizeCases() => new()
    {
        { TextureFormats.RgbPvrtcI4BppUNorm, 1, 1, 32 },
        { TextureFormats.RgbPvrtcI4BppUNorm, 2, 2, 32 },
        { TextureFormats.RgbPvrtcI4BppUNorm, 4, 4, 32 },
        { TextureFormats.RgbPvrtcI4BppUNorm, 8, 8, 32 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 1, 1, 32 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 2, 2, 32 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 4, 4, 32 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 16, 8, 32 },
        { TextureFormats.RgbaPvrtcII4BppUNorm, 1, 1, 8 },
        { TextureFormats.RgbaPvrtcII4BppUNorm, 4, 4, 8 },
        { TextureFormats.RgbaPvrtcII4BppUNorm, 127, 129, 8448 },
        { TextureFormats.RgbaPvrtcII2BppUNorm, 1, 1, 8 },
        { TextureFormats.RgbaPvrtcII2BppUNorm, 8, 4, 8 },
        { TextureFormats.RgbaPvrtcII2BppUNorm, 127, 129, 4224 },
        { TextureFormats.RgbPvrtcI6BppFloat, 1, 1, 64 },
        { TextureFormats.RgbPvrtcI8BppFloat, 1, 1, 64 },
        { TextureFormats.RgbPvrtcII6BppFloat, 1, 1, 16 },
        { TextureFormats.RgbPvrtcII8BppFloat, 1, 1, 16 }
    };

    public static TheoryData<TextureFormat, int, int> PvrtcEdgeCases() => new()
    {
        { TextureFormats.RgbPvrtcI4BppUNorm, 1, 1 },
        { TextureFormats.RgbaPvrtcI4BppUNorm, 2, 2 },
        { TextureFormats.RgbPvrtcI2BppUNorm, 2, 2 },
        { TextureFormats.RgbaPvrtcI2BppUNorm, 4, 4 },
        { TextureFormats.RgbaPvrtcII4BppUNorm, 7, 9 },
        { TextureFormats.RgbaPvrtcII2BppUNorm, 7, 9 }
    };

    private static Rgba8UNorm[] CreateSource(TextureFormat format) =>
        CreateSource(format, Width, Height);

    private static Rgba8UNorm[] CreateSource(TextureFormat format, int width, int height)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = (byte)(32 + (width == 1 ? 0 : x * 160 / (width - 1)));
                var green = (byte)(24 + (height == 1 ? 0 : y * 176 / (height - 1)));
                var blue = (byte)(48 + (width + height == 2 ? 0 : (x + y) * 80 / (width + height - 2)));
                var alpha = format.Components == TextureComponents.Rgba
                    ? (byte)(96 + ((x * 3 + y * 5) % 128))
                    : byte.MaxValue;
                pixels[(y * width) + x] = new Rgba8UNorm(red, green, blue, alpha);
            }
        }

        return pixels;
    }

    private static Rgba32Float[] CreateHdrSource(int width, int height)
    {
        var pixels = new Rgba32Float[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = new Rgba32Float(
                    0.125f + (x * 4f / Math.Max(1, width - 1)),
                    0.25f + (y * 6f / Math.Max(1, height - 1)),
                    0.5f + ((x + y) * 3f / Math.Max(1, width + height - 2)),
                    1f);
            }
        }

        return pixels;
    }

    private static byte[] EncodeWithPvrTexLib(TextureFormat format, int width, int height, Rgba8UNorm[] source)
    {
        fixed (Rgba8UNorm* sourcePtr = source)
        {
            using var header = new PVRTextureHeader(
                Rgba8PixelFormat,
                checked((uint)width),
                checked((uint)height),
                depth: 1,
                numMipMaps: 1,
                numArrayMembers: 1,
                numFaces: 1,
                PVRTexLibColourSpace.Linear,
                PVRTexLibVariableType.UnsignedByteNorm,
                preMultiplied: false);
            using var texture = new PVRTexture(header, sourcePtr);
            Transcode(
                texture,
                GetPvrTexLibPixelFormat(format),
                GetChannelType(format),
                GetColourSpace(format),
                PVRTexLibCompressorQuality.PVRTCNormal);
            return CopyTextureData(texture);
        }
    }

    private static byte[] EncodeFloatWithPvrTexLib(TextureFormat format, int width, int height, Rgba32Float[] source)
    {
        fixed (Rgba32Float* sourcePtr = source)
        {
            using var header = new PVRTextureHeader(
                Rgba32PixelFormat,
                checked((uint)width),
                checked((uint)height),
                depth: 1,
                numMipMaps: 1,
                numArrayMembers: 1,
                numFaces: 1,
                PVRTexLibColourSpace.Linear,
                PVRTexLibVariableType.SignedFloat,
                preMultiplied: false);
            using var texture = new PVRTexture(header, sourcePtr);
            Transcode(
                texture,
                GetPvrTexLibPixelFormat(format),
                GetChannelType(format),
                GetColourSpace(format),
                PVRTexLibCompressorQuality.PVRTCNormal);
            return CopyTextureData(texture);
        }
    }

    private static Rgba8UNorm[] DecodeWithPvrTexLib(TextureFormat format, int width, int height, byte[] payload)
    {
        fixed (byte* payloadPtr = payload)
        {
            using var header = CreateCompressedHeader(format, width, height);
            using var texture = new PVRTexture(header, payloadPtr);
            Transcode(
                texture,
                Rgba8PixelFormat,
                PVRTexLibVariableType.UnsignedByteNorm,
                PVRTexLibColourSpace.Linear,
                PVRTexLibCompressorQuality.PVRTCNormal);

            var rgba = CopyTextureData(texture);
            var requiredBytes = checked(width * height * 4);
            Assert.True(rgba.Length >= requiredBytes, $"PVRTexLib returned {rgba.Length} bytes; expected at least {requiredBytes}.");
            var pixels = new Rgba8UNorm[checked(width * height)];
            MemoryMarshal.Cast<byte, Rgba8UNorm>(rgba.AsSpan(0, requiredBytes)).CopyTo(pixels);
            return pixels;
        }
    }

    private static Rgba32Float[] DecodeFloatWithPvrTexLib(TextureFormat format, int width, int height, byte[] payload)
    {
        fixed (byte* payloadPtr = payload)
        {
            using var header = CreateCompressedHeader(format, width, height);
            using var texture = new PVRTexture(header, payloadPtr);
            Transcode(
                texture,
                Rgba32PixelFormat,
                PVRTexLibVariableType.SignedFloat,
                PVRTexLibColourSpace.Linear,
                PVRTexLibCompressorQuality.PVRTCNormal);

            var rgba = CopyTextureData(texture);
            var requiredBytes = checked(width * height * sizeof(float) * 4);
            Assert.True(rgba.Length >= requiredBytes, $"PVRTexLib returned {rgba.Length} bytes; expected at least {requiredBytes}.");
            var pixels = new Rgba32Float[checked(width * height)];
            MemoryMarshal.Cast<byte, Rgba32Float>(rgba.AsSpan(0, requiredBytes)).CopyTo(pixels);
            return pixels;
        }
    }

    private static PVRTextureHeader CreateCompressedHeader(TextureFormat format, int width, int height) =>
        new(
            GetPvrTexLibPixelFormat(format),
            checked((uint)width),
            checked((uint)height),
            depth: 1,
            numMipMaps: 1,
            numArrayMembers: 1,
            numFaces: 1,
            GetColourSpace(format),
            GetChannelType(format),
            preMultiplied: false);

    private static void Transcode(
        PVRTexture texture,
        ulong pixelFormat,
        PVRTexLibVariableType channelType,
        PVRTexLibColourSpace colourSpace,
        PVRTexLibCompressorQuality quality)
    {
        if (!texture.Transcode(pixelFormat, channelType, colourSpace, quality, doDither: false, maxRange: 0f, maxThreads: 0))
        {
            throw new InvalidOperationException("PVRTexLib failed to transcode the texture.");
        }
    }

    private static byte[] CopyTextureData(PVRTexture texture)
    {
        var size = checked((int)texture.GetTextureDataSize(0, allSurfaces: false, allFaces: false));
        var data = texture.GetTextureDataConstPointer(0, arrayMember: 0, faceNumber: 0, ZSlice: 0);
        if (data is null || size == 0)
        {
            throw new InvalidOperationException("PVRTexLib returned an empty texture payload.");
        }

        var result = new byte[size];
        new ReadOnlySpan<byte>(data, size).CopyTo(result);
        return result;
    }

    private static void AssertPixelsNear(Rgba8UNorm[] expected, Rgba8UNorm[] actual, TextureFormat format)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertChannelNear(expected[i].Red, actual[i].Red, format, i, nameof(Rgba8UNorm.Red));
            AssertChannelNear(expected[i].Green, actual[i].Green, format, i, nameof(Rgba8UNorm.Green));
            AssertChannelNear(expected[i].Blue, actual[i].Blue, format, i, nameof(Rgba8UNorm.Blue));
            AssertChannelNear(expected[i].Alpha, actual[i].Alpha, format, i, nameof(Rgba8UNorm.Alpha));
        }
    }

    private static void AssertAverageErrorIsReasonable(Rgba8UNorm[] source, Rgba8UNorm[] decoded, TextureFormat format)
    {
        Assert.Equal(source.Length, decoded.Length);
        long total = 0;
        var channels = 0;
        for (var i = 0; i < source.Length; i++)
        {
            total += Math.Abs(source[i].Red - decoded[i].Red);
            total += Math.Abs(source[i].Green - decoded[i].Green);
            total += Math.Abs(source[i].Blue - decoded[i].Blue);
            channels += 3;
            if (format.Components == TextureComponents.Rgba)
            {
                total += Math.Abs(source[i].Alpha - decoded[i].Alpha);
                channels++;
            }
        }

        var average = total / (double)channels;
        Assert.True(average <= 40d, $"{format.Name} average encode error was {average:0.00}.");
    }

    private static void AssertFloatPixelsNear(Rgba32Float[] expected, Rgba32Float[] actual, TextureFormat format, float absoluteTolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertFloatChannelNear(expected[i].Red, actual[i].Red, format, i, nameof(Rgba32Float.Red), absoluteTolerance);
            AssertFloatChannelNear(expected[i].Green, actual[i].Green, format, i, nameof(Rgba32Float.Green), absoluteTolerance);
            AssertFloatChannelNear(expected[i].Blue, actual[i].Blue, format, i, nameof(Rgba32Float.Blue), absoluteTolerance);
        }
    }

    private static void AssertAverageFloatErrorIsReasonable(
        Rgba32Float[] source,
        Rgba32Float[] decoded,
        TextureFormat format,
        float maxAverageError)
    {
        Assert.Equal(source.Length, decoded.Length);
        var total = 0d;
        for (var i = 0; i < source.Length; i++)
        {
            total += Math.Abs(source[i].Red - decoded[i].Red);
            total += Math.Abs(source[i].Green - decoded[i].Green);
            total += Math.Abs(source[i].Blue - decoded[i].Blue);
        }

        var average = total / (source.Length * 3d);
        Assert.True(average <= maxAverageError, $"{format.Name} average HDR encode error was {average:0.00}.");
    }

    private static void AssertChannelNear(byte expected, byte actual, TextureFormat format, int pixel, string channel)
    {
        var delta = Math.Abs(expected - actual);
        Assert.True(
            delta <= PixelTolerance,
            $"{format.Name} pixel {pixel} {channel}: expected {expected}, actual {actual}, delta {delta}.");
    }

    private static void AssertFloatChannelNear(
        float expected,
        float actual,
        TextureFormat format,
        int pixel,
        string channel,
        float absoluteTolerance)
    {
        var delta = Math.Abs(expected - actual);
        Assert.True(
            delta <= absoluteTolerance,
            $"{format.Name} pixel {pixel} {channel}: expected {expected:0.###}, actual {actual:0.###}, delta {delta:0.###}.");
    }

    private static PVRTexLibColourSpace GetColourSpace(TextureFormat format) =>
        format.ValueKind == TextureValueKind.Srgb
            ? PVRTexLibColourSpace.sRGB
            : PVRTexLibColourSpace.Linear;

    private static PVRTexLibVariableType GetChannelType(TextureFormat format) =>
        format.ValueKind == TextureValueKind.Float
            ? PVRTexLibVariableType.SignedFloat
            : PVRTexLibVariableType.UnsignedByteNorm;

    private static ulong GetPvrTexLibPixelFormat(TextureFormat format)
    {
        if (format == TextureFormats.RgbPvrtcI2BppUNorm || format == TextureFormats.RgbPvrtcI2BppSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCI_2bpp_RGB;
        }

        if (format == TextureFormats.RgbaPvrtcI2BppUNorm || format == TextureFormats.RgbaPvrtcI2BppSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCI_2bpp_RGBA;
        }

        if (format == TextureFormats.RgbPvrtcI4BppUNorm || format == TextureFormats.RgbPvrtcI4BppSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCI_4bpp_RGB;
        }

        if (format == TextureFormats.RgbaPvrtcI4BppUNorm || format == TextureFormats.RgbaPvrtcI4BppSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCI_4bpp_RGBA;
        }

        if (format == TextureFormats.RgbaPvrtcII2BppUNorm || format == TextureFormats.RgbaPvrtcII2BppSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCII_2bpp;
        }

        if (format == TextureFormats.RgbaPvrtcII4BppUNorm || format == TextureFormats.RgbaPvrtcII4BppSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCII_4bpp;
        }

        if (format == TextureFormats.RgbPvrtcI6BppFloat)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCI_HDR_6bpp;
        }

        if (format == TextureFormats.RgbPvrtcI8BppFloat)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCI_HDR_8bpp;
        }

        if (format == TextureFormats.RgbPvrtcII6BppFloat)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCII_HDR_6bpp;
        }

        if (format == TextureFormats.RgbPvrtcII8BppFloat)
        {
            return (ulong)PVRTexLibPixelFormat.PVRTCII_HDR_8bpp;
        }

        throw new NotSupportedException($"No PVRTexLib pixel format is mapped for '{format.Name}'.");
    }

    private struct Rgba8OnlyPixel(byte red, byte green, byte blue, byte alpha = 255)
        : IPixel<Rgba8OnlyPixel>
    {
        public byte Red = red;
        public byte Green = green;
        public byte Blue = blue;
        public byte Alpha = alpha;

        public static Rgba8UNorm ToRgba8UNorm(Rgba8OnlyPixel value) =>
            new(value.Red, value.Green, value.Blue, value.Alpha);

        public static Rgba8OnlyPixel FromRgba8UNorm(Rgba8UNorm value) =>
            new(value.Red, value.Green, value.Blue, value.Alpha);
    }
}
