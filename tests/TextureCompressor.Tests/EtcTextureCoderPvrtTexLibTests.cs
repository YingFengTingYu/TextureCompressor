using System.Buffers.Binary;
using System.Runtime.InteropServices;
using PVRTexLib;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.Tests;

public sealed unsafe class EtcTextureCoderPvrtTexLibTests
{
    private const int Width = 8;
    private const int Height = 8;
    private const int EtcPixelTolerance = 2;
    private const int Eac16Tolerance = 32;
    private const int SignedEac16Tolerance = 32;

    private static readonly ulong Rgba8PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 8, 8, 8, 8);
    private static readonly ulong Rgba16PixelFormat = PVRDefine.PVRTGENPIXELID4('r', 'g', 'b', 'a', 16, 16, 16, 16);

    [Theory]
    [MemberData(nameof(ColorEtcFormats))]
    public void DecodePvrtTexLibColorPayloadMatchesPvrtTexLib(TextureFormat format)
    {
        var source = CreateColorSource(format, Width, Height);
        var payload = EncodeColorWithPvrtTexLib(format, Width, Height, source);
        var expected = DecodeColorWithPvrtTexLib(format, Width, Height, payload);
        var actual = new ArrayBitmap<Rgba8UNorm>(Width, Height);
        var coder = new EtcTextureCoder(format);

        coder.Decode(payload, actual.AsView(), coder.GetDefaultPitch(Width));

        Assert.Equal(format.GetByteCount(Width, Height), payload.Length);
        AssertRgba8Near(expected, actual.Pixels, format, EtcPixelTolerance);
    }

    [Theory]
    [MemberData(nameof(ColorEtcFormats))]
    public void EncodeColorPayloadIsDecodableByPvrtTexLib(TextureFormat format)
    {
        var sourcePixels = CreateColorSource(format, Width, Height);
        var source = new ArrayBitmap<Rgba8UNorm>(Width, Height, sourcePixels);
        var coder = new EtcTextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(Width);
        var payload = new byte[coder.GetEncodedByteCount(Width, Height, rowPitch)];
        var actual = new ArrayBitmap<Rgba8UNorm>(Width, Height);

        coder.Encode(source.AsView(), payload, rowPitch);
        var expected = DecodeColorWithPvrtTexLib(format, Width, Height, payload);
        coder.Decode(payload, actual.AsView(), rowPitch);

        AssertRgba8Near(expected, actual.Pixels, format, EtcPixelTolerance);
        AssertAverageColorErrorIsReasonable(sourcePixels, expected, format);
    }

    [Theory]
    [MemberData(nameof(Etc2ExtendedModeBlocks))]
    public void DecodeEtc2ExtendedModeBlockMatchesPvrtTexLib(TextureFormat format, byte[] payload)
    {
        var expected = DecodeColorWithPvrtTexLib(format, 4, 4, payload);
        var actual = new ArrayBitmap<Rgba8UNorm>(4, 4);
        var coder = new EtcTextureCoder(format);

        coder.Decode(payload, actual.AsView(), coder.GetDefaultPitch(4));

        AssertRgba8Near(expected, actual.Pixels, format, EtcPixelTolerance);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.R11EacUNorm))]
    [InlineData(nameof(TextureFormats.Rg11EacUNorm))]
    public void DecodePvrtTexLibUnsignedEacPayloadMatchesPvrtTexLib(string formatName)
    {
        var format = GetFormat(formatName);
        var source = CreateUnsignedEacSource(format, Width, Height);
        var payload = EncodeUnsignedEacWithPvrtTexLib(format, Width, Height, source);
        var expected = DecodeUnsignedEacWithPvrtTexLib(format, Width, Height, payload);
        var actual = new ArrayBitmap<Rgba16UNorm>(Width, Height);
        var coder = new EtcTextureCoder(format);

        coder.Decode(payload, actual.AsView(), coder.GetDefaultPitch(Width));

        Assert.Equal(format.GetByteCount(Width, Height), payload.Length);
        AssertRgba16UNormNear(expected, actual.Pixels, format);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.R11EacSNorm))]
    [InlineData(nameof(TextureFormats.Rg11EacSNorm))]
    public void DecodePvrtTexLibSignedEacPayloadMatchesPvrtTexLib(string formatName)
    {
        var format = GetFormat(formatName);
        var source = CreateSignedEacSource(format, Width, Height);
        var payload = EncodeSignedEacWithPvrtTexLib(format, Width, Height, source);
        var expected = DecodeSignedEacWithPvrtTexLib(format, Width, Height, payload);
        var actual = new ArrayBitmap<Rgba16SNorm>(Width, Height);
        var coder = new EtcTextureCoder(format);

        coder.Decode(payload, actual.AsView(), coder.GetDefaultPitch(Width));

        Assert.Equal(format.GetByteCount(Width, Height), payload.Length);
        AssertRgba16SNormNear(expected, actual.Pixels, format);
    }

    [Theory]
    [MemberData(nameof(EacReferenceBlocks))]
    public void DecodeEacBlockMatchesPvrtTexLib(TextureFormat format, byte[] payload)
    {
        var coder = new EtcTextureCoder(format);

        if (format.ValueKind == TextureValueKind.SNorm)
        {
            var expected = DecodeSignedEacWithPvrtTexLib(format, 4, 4, payload);
            var actual = new ArrayBitmap<Rgba16SNorm>(4, 4);

            coder.Decode(payload, actual.AsView(), coder.GetDefaultPitch(4));

            AssertRgba16SNormNear(expected, actual.Pixels, format);
            return;
        }

        var unsignedExpected = DecodeUnsignedEacWithPvrtTexLib(format, 4, 4, payload);
        var unsignedActual = new ArrayBitmap<Rgba16UNorm>(4, 4);

        coder.Decode(payload, unsignedActual.AsView(), coder.GetDefaultPitch(4));

        AssertRgba16UNormNear(unsignedExpected, unsignedActual.Pixels, format);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.R11EacUNorm))]
    [InlineData(nameof(TextureFormats.Rg11EacUNorm))]
    public void EncodeUnsignedEacPayloadIsDecodableByPvrtTexLib(string formatName)
    {
        var format = GetFormat(formatName);
        var sourcePixels = CreateUnsignedEacSource(format, Width, Height);
        var source = new ArrayBitmap<Rgba16UNorm>(Width, Height, sourcePixels);
        var coder = new EtcTextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(Width);
        var payload = new byte[coder.GetEncodedByteCount(Width, Height, rowPitch)];
        var actual = new ArrayBitmap<Rgba16UNorm>(Width, Height);

        coder.Encode(source.AsView(), payload, rowPitch);
        var expected = DecodeUnsignedEacWithPvrtTexLib(format, Width, Height, payload);
        coder.Decode(payload, actual.AsView(), rowPitch);

        AssertRgba16UNormNear(expected, actual.Pixels, format);
        AssertAverageUnsignedEacErrorIsReasonable(sourcePixels, expected, format);
    }

    [Theory]
    [InlineData(nameof(TextureFormats.R11EacSNorm))]
    [InlineData(nameof(TextureFormats.Rg11EacSNorm))]
    public void EncodeSignedEacPayloadIsDecodableByPvrtTexLib(string formatName)
    {
        var format = GetFormat(formatName);
        var sourcePixels = CreateSignedEacSource(format, Width, Height);
        var source = new ArrayBitmap<Rgba16SNorm>(Width, Height, sourcePixels);
        var coder = new EtcTextureCoder(format);
        var rowPitch = coder.GetDefaultPitch(Width);
        var payload = new byte[coder.GetEncodedByteCount(Width, Height, rowPitch)];
        var actual = new ArrayBitmap<Rgba16SNorm>(Width, Height);

        coder.Encode(source.AsView(), payload, rowPitch);
        var expected = DecodeSignedEacWithPvrtTexLib(format, Width, Height, payload);
        coder.Decode(payload, actual.AsView(), rowPitch);

        AssertRgba16SNormNear(expected, actual.Pixels, format);
        AssertAverageSignedEacErrorIsReasonable(sourcePixels, expected, format);
    }

    public static TheoryData<TextureFormat> ColorEtcFormats() => new()
    {
        TextureFormats.RgbEtc1UNorm,
        TextureFormats.RgbEtc2UNorm,
        TextureFormats.RgbEtc2Srgb,
        TextureFormats.RgbA1Etc2UNorm,
        TextureFormats.RgbA1Etc2Srgb,
        TextureFormats.RgbaEtc2EacUNorm,
        TextureFormats.RgbaEtc2EacSrgb
    };

    public static TheoryData<TextureFormat, byte[]> Etc2ExtendedModeBlocks() => new()
    {
        { TextureFormats.RgbEtc2UNorm, CreateInvalidDifferentialBlock(invalidChannel: 0, modeBit: true, low: 0x00000000u) },
        { TextureFormats.RgbEtc2UNorm, CreateInvalidDifferentialBlock(invalidChannel: 1, modeBit: true, low: 0x5a3cc3a5u) },
        { TextureFormats.RgbEtc2UNorm, CreateInvalidDifferentialBlock(invalidChannel: 2, modeBit: true, low: 0xffffffffu) },
        { TextureFormats.RgbA1Etc2UNorm, CreateInvalidDifferentialBlock(invalidChannel: 0, modeBit: false, low: 0x294a739cu) },
        { TextureFormats.RgbA1Etc2UNorm, CreateInvalidDifferentialBlock(invalidChannel: 1, modeBit: false, low: 0xc318a55au) },
        { TextureFormats.RgbA1Etc2UNorm, CreateInvalidDifferentialBlock(invalidChannel: 2, modeBit: false, low: 0x7bd31842u) }
    };

    public static TheoryData<TextureFormat, byte[]> EacReferenceBlocks() => new()
    {
        { TextureFormats.R11EacUNorm, CreateEacPayload(false, [0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc]) },
        { TextureFormats.R11EacUNorm, CreateEacPayload(false, [0xff, 0xf7, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54]) },
        { TextureFormats.R11EacSNorm, CreateEacPayload(false, [0x80, 0x0d, 0x24, 0x68, 0xac, 0xe0, 0x13, 0x57]) },
        { TextureFormats.R11EacSNorm, CreateEacPayload(false, [0x7f, 0xf1, 0x8f, 0x1e, 0x2d, 0x3c, 0x4b, 0x5a]) },
        { TextureFormats.Rg11EacUNorm, CreateEacPayload(true, [0x10, 0x31, 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc], [0xf0, 0xc4, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54]) },
        { TextureFormats.Rg11EacSNorm, CreateEacPayload(true, [0x80, 0x07, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab], [0x7f, 0xf8, 0xba, 0x98, 0x76, 0x54, 0x32, 0x10]) }
    };

    private static Rgba8UNorm[] CreateColorSource(TextureFormat format, int width, int height)
    {
        var pixels = new Rgba8UNorm[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = (byte)(20 + (x * 171 / Math.Max(1, width - 1)));
                var green = (byte)(33 + (y * 157 / Math.Max(1, height - 1)));
                var blue = (byte)(48 + ((x * 19 + y * 13) % 149));
                var alpha = format.Components == TextureComponents.Rgba
                    ? (byte)((x + y) % 3 == 0 ? 0 : 255)
                    : byte.MaxValue;
                pixels[(y * width) + x] = new Rgba8UNorm(red, green, blue, alpha);
            }
        }

        return pixels;
    }

    private static Rgba16UNorm[] CreateUnsignedEacSource(TextureFormat format, int width, int height)
    {
        var pixels = new Rgba16UNorm[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = (ushort)((x * ushort.MaxValue / Math.Max(1, width - 1)) ^ ((y & 1) * 0x1111));
                var green = format.Components == TextureComponents.Rg
                    ? (ushort)(y * ushort.MaxValue / Math.Max(1, height - 1))
                    : (ushort)0;
                pixels[(y * width) + x] = new Rgba16UNorm(red, green, 0, ushort.MaxValue);
            }
        }

        return pixels;
    }

    private static Rgba16SNorm[] CreateSignedEacSource(TextureFormat format, int width, int height)
    {
        var pixels = new Rgba16SNorm[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = (short)(short.MinValue + 1 + ((x * (short.MaxValue * 2) / Math.Max(1, width - 1))));
                var green = format.Components == TextureComponents.Rg
                    ? (short)(short.MinValue + 1 + ((y * (short.MaxValue * 2) / Math.Max(1, height - 1))))
                    : (short)0;
                pixels[(y * width) + x] = new Rgba16SNorm(red, green, 0, short.MaxValue);
            }
        }

        return pixels;
    }

    private static byte[] CreateInvalidDifferentialBlock(int invalidChannel, bool modeBit, uint low)
    {
        var r0 = invalidChannel == 0 ? 31 : 12;
        var dr = invalidChannel == 0 ? 1 : 0;
        var g0 = invalidChannel == 1 ? 31 : 10;
        var dg = invalidChannel == 1 ? 1 : 0;
        var b0 = invalidChannel == 2 ? 31 : 9;
        var db = invalidChannel == 2 ? 1 : 0;

        var high = ((uint)r0 << 27) |
                   ((uint)dr << 24) |
                   ((uint)g0 << 19) |
                   ((uint)dg << 16) |
                   ((uint)b0 << 11) |
                   ((uint)db << 8) |
                   (3u << 5) |
                   (5u << 2) |
                   (modeBit ? 0x2u : 0u);

        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), high);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), low);
        return payload;
    }

    private static byte[] CreateEacPayload(bool dualPlane, ReadOnlySpan<byte> red, ReadOnlySpan<byte> green = default)
    {
        var payload = new byte[dualPlane ? 16 : 8];
        red.CopyTo(payload);
        if (dualPlane)
        {
            green.CopyTo(payload.AsSpan(8));
        }

        return payload;
    }

    private static byte[] EncodeColorWithPvrtTexLib(TextureFormat format, int width, int height, Rgba8UNorm[] source)
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
            Transcode(texture, GetPvrTexLibPixelFormat(format), GetCompressedChannelType(format), GetColourSpace(format));
            return CopyTextureData(texture);
        }
    }

    private static byte[] EncodeUnsignedEacWithPvrtTexLib(TextureFormat format, int width, int height, Rgba16UNorm[] source)
    {
        fixed (Rgba16UNorm* sourcePtr = source)
        {
            using var header = new PVRTextureHeader(
                Rgba16PixelFormat,
                checked((uint)width),
                checked((uint)height),
                depth: 1,
                numMipMaps: 1,
                numArrayMembers: 1,
                numFaces: 1,
                PVRTexLibColourSpace.Linear,
                PVRTexLibVariableType.UnsignedShortNorm,
                preMultiplied: false);
            using var texture = new PVRTexture(header, sourcePtr);
            Transcode(texture, GetPvrTexLibPixelFormat(format), GetCompressedChannelType(format), PVRTexLibColourSpace.Linear);
            return CopyTextureData(texture);
        }
    }

    private static byte[] EncodeSignedEacWithPvrtTexLib(TextureFormat format, int width, int height, Rgba16SNorm[] source)
    {
        fixed (Rgba16SNorm* sourcePtr = source)
        {
            using var header = new PVRTextureHeader(
                Rgba16PixelFormat,
                checked((uint)width),
                checked((uint)height),
                depth: 1,
                numMipMaps: 1,
                numArrayMembers: 1,
                numFaces: 1,
                PVRTexLibColourSpace.Linear,
                PVRTexLibVariableType.SignedShortNorm,
                preMultiplied: false);
            using var texture = new PVRTexture(header, sourcePtr);
            Transcode(texture, GetPvrTexLibPixelFormat(format), GetCompressedChannelType(format), PVRTexLibColourSpace.Linear);
            return CopyTextureData(texture);
        }
    }

    private static Rgba8UNorm[] DecodeColorWithPvrtTexLib(TextureFormat format, int width, int height, byte[] payload)
    {
        fixed (byte* payloadPtr = payload)
        {
            using var header = CreateCompressedHeader(format, width, height);
            using var texture = new PVRTexture(header, payloadPtr);
            Transcode(texture, Rgba8PixelFormat, PVRTexLibVariableType.UnsignedByteNorm, PVRTexLibColourSpace.Linear);
            return CopyTextureData<Rgba8UNorm>(texture, checked(width * height));
        }
    }

    private static Rgba16UNorm[] DecodeUnsignedEacWithPvrtTexLib(TextureFormat format, int width, int height, byte[] payload)
    {
        fixed (byte* payloadPtr = payload)
        {
            using var header = CreateCompressedHeader(format, width, height);
            using var texture = new PVRTexture(header, payloadPtr);
            Transcode(texture, Rgba16PixelFormat, PVRTexLibVariableType.UnsignedShortNorm, PVRTexLibColourSpace.Linear);
            return CopyTextureData<Rgba16UNorm>(texture, checked(width * height));
        }
    }

    private static Rgba16SNorm[] DecodeSignedEacWithPvrtTexLib(TextureFormat format, int width, int height, byte[] payload)
    {
        fixed (byte* payloadPtr = payload)
        {
            using var header = CreateCompressedHeader(format, width, height);
            using var texture = new PVRTexture(header, payloadPtr);
            Transcode(texture, Rgba16PixelFormat, PVRTexLibVariableType.SignedShortNorm, PVRTexLibColourSpace.Linear);
            return CopyTextureData<Rgba16SNorm>(texture, checked(width * height));
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
            GetCompressedChannelType(format),
            preMultiplied: false);

    private static void Transcode(
        PVRTexture texture,
        ulong pixelFormat,
        PVRTexLibVariableType channelType,
        PVRTexLibColourSpace colourSpace)
    {
        if (!texture.Transcode(pixelFormat, channelType, colourSpace, PVRTexLibCompressorQuality.ETCNormal, doDither: false, maxRange: 0f, maxThreads: 0))
        {
            throw new InvalidOperationException("PVRTexLib failed to transcode the texture.");
        }
    }

    private static T[] CopyTextureData<T>(PVRTexture texture, int count)
        where T : unmanaged
    {
        var size = checked((int)texture.GetTextureDataSize(0, allSurfaces: false, allFaces: false));
        var requiredBytes = checked(count * Marshal.SizeOf<T>());
        var data = texture.GetTextureDataConstPointer(0, arrayMember: 0, faceNumber: 0, ZSlice: 0);
        Assert.True(data is not null && size >= requiredBytes, $"PVRTexLib returned {size} bytes; expected at least {requiredBytes}.");

        var result = new T[count];
        new ReadOnlySpan<T>(data, count).CopyTo(result);
        return result;
    }

    private static byte[] CopyTextureData(PVRTexture texture)
    {
        var size = checked((int)texture.GetTextureDataSize(0, allSurfaces: false, allFaces: false));
        var data = texture.GetTextureDataConstPointer(0, arrayMember: 0, faceNumber: 0, ZSlice: 0);
        Assert.True(data is not null && size > 0, "PVRTexLib returned an empty texture payload.");

        var result = new byte[size];
        new ReadOnlySpan<byte>(data, size).CopyTo(result);
        return result;
    }

    private static void AssertRgba8Near(Rgba8UNorm[] expected, Rgba8UNorm[] actual, TextureFormat format, int tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertChannelNear(expected[i].Red, actual[i].Red, format, i, nameof(Rgba8UNorm.Red), tolerance);
            AssertChannelNear(expected[i].Green, actual[i].Green, format, i, nameof(Rgba8UNorm.Green), tolerance);
            AssertChannelNear(expected[i].Blue, actual[i].Blue, format, i, nameof(Rgba8UNorm.Blue), tolerance);
            AssertChannelNear(expected[i].Alpha, actual[i].Alpha, format, i, nameof(Rgba8UNorm.Alpha), tolerance);
        }
    }

    private static void AssertRgba16UNormNear(Rgba16UNorm[] expected, Rgba16UNorm[] actual, TextureFormat format)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertChannelNear(expected[i].Red, actual[i].Red, format, i, nameof(Rgba16UNorm.Red), Eac16Tolerance);
            AssertChannelNear(expected[i].Green, actual[i].Green, format, i, nameof(Rgba16UNorm.Green), Eac16Tolerance);
            AssertChannelNear(expected[i].Blue, actual[i].Blue, format, i, nameof(Rgba16UNorm.Blue), Eac16Tolerance);
            AssertChannelNear(expected[i].Alpha, actual[i].Alpha, format, i, nameof(Rgba16UNorm.Alpha), Eac16Tolerance);
        }
    }

    private static void AssertRgba16SNormNear(Rgba16SNorm[] expected, Rgba16SNorm[] actual, TextureFormat format)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertChannelNear(expected[i].Red, actual[i].Red, format, i, nameof(Rgba16SNorm.Red), SignedEac16Tolerance);
            AssertChannelNear(expected[i].Green, actual[i].Green, format, i, nameof(Rgba16SNorm.Green), SignedEac16Tolerance);
            AssertChannelNear(expected[i].Blue, actual[i].Blue, format, i, nameof(Rgba16SNorm.Blue), SignedEac16Tolerance);
            AssertChannelNear(expected[i].Alpha, actual[i].Alpha, format, i, nameof(Rgba16SNorm.Alpha), SignedEac16Tolerance);
        }
    }

    private static void AssertChannelNear(byte expected, byte actual, TextureFormat format, int pixel, string channel, int tolerance)
    {
        var delta = Math.Abs(expected - actual);
        Assert.True(delta <= tolerance, $"{format.Name} pixel {pixel} {channel}: expected {expected}, actual {actual}, delta {delta}.");
    }

    private static void AssertChannelNear(ushort expected, ushort actual, TextureFormat format, int pixel, string channel, int tolerance)
    {
        var delta = Math.Abs(expected - actual);
        Assert.True(delta <= tolerance, $"{format.Name} pixel {pixel} {channel}: expected {expected}, actual {actual}, delta {delta}.");
    }

    private static void AssertChannelNear(short expected, short actual, TextureFormat format, int pixel, string channel, int tolerance)
    {
        var delta = Math.Abs(expected - actual);
        Assert.True(delta <= tolerance, $"{format.Name} pixel {pixel} {channel}: expected {expected}, actual {actual}, delta {delta}.");
    }

    private static void AssertAverageColorErrorIsReasonable(Rgba8UNorm[] source, Rgba8UNorm[] decoded, TextureFormat format)
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
        Assert.True(average <= 46d, $"{format.Name} average encode error was {average:0.00}.");
    }

    private static void AssertAverageUnsignedEacErrorIsReasonable(Rgba16UNorm[] source, Rgba16UNorm[] decoded, TextureFormat format)
    {
        Assert.Equal(source.Length, decoded.Length);
        var average = GetAverageError(source.Length, format.Components, i => Math.Abs(source[i].Red - decoded[i].Red), i => Math.Abs(source[i].Green - decoded[i].Green));
        Assert.True(average <= 4096d, $"{format.Name} average encode error was {average:0.00}.");
    }

    private static void AssertAverageSignedEacErrorIsReasonable(Rgba16SNorm[] source, Rgba16SNorm[] decoded, TextureFormat format)
    {
        Assert.Equal(source.Length, decoded.Length);
        var average = GetAverageError(source.Length, format.Components, i => Math.Abs(source[i].Red - decoded[i].Red), i => Math.Abs(source[i].Green - decoded[i].Green));
        Assert.True(average <= 4096d, $"{format.Name} average encode error was {average:0.00}.");
    }

    private static double GetAverageError(int pixelCount, TextureComponents components, Func<int, int> red, Func<int, int> green)
    {
        long total = 0;
        var channels = 0;
        for (var i = 0; i < pixelCount; i++)
        {
            total += red(i);
            channels++;
            if (components == TextureComponents.Rg)
            {
                total += green(i);
                channels++;
            }
        }

        return total / (double)channels;
    }

    private static PVRTexLibColourSpace GetColourSpace(TextureFormat format) =>
        format.ValueKind == TextureValueKind.Srgb
            ? PVRTexLibColourSpace.sRGB
            : PVRTexLibColourSpace.Linear;

    private static PVRTexLibVariableType GetCompressedChannelType(TextureFormat format)
    {
        if (format == TextureFormats.R11EacSNorm || format == TextureFormats.Rg11EacSNorm)
        {
            return PVRTexLibVariableType.SignedShortNorm;
        }

        if (format == TextureFormats.R11EacUNorm || format == TextureFormats.Rg11EacUNorm)
        {
            return PVRTexLibVariableType.UnsignedShortNorm;
        }

        return PVRTexLibVariableType.UnsignedByteNorm;
    }

    private static ulong GetPvrTexLibPixelFormat(TextureFormat format)
    {
        if (format == TextureFormats.RgbEtc1UNorm)
        {
            return (ulong)PVRTexLibPixelFormat.ETC1;
        }

        if (format == TextureFormats.RgbEtc2UNorm || format == TextureFormats.RgbEtc2Srgb)
        {
            return (ulong)PVRTexLibPixelFormat.ETC2_RGB;
        }

        if (format == TextureFormats.RgbA1Etc2UNorm || format == TextureFormats.RgbA1Etc2Srgb)
        {
            return (ulong)PVRTexLibPixelFormat.ETC2_RGB_A1;
        }

        if (format == TextureFormats.RgbaEtc2EacUNorm || format == TextureFormats.RgbaEtc2EacSrgb)
        {
            return (ulong)PVRTexLibPixelFormat.ETC2_RGBA;
        }

        if (format == TextureFormats.R11EacUNorm || format == TextureFormats.R11EacSNorm)
        {
            return (ulong)PVRTexLibPixelFormat.EAC_R11;
        }

        if (format == TextureFormats.Rg11EacUNorm || format == TextureFormats.Rg11EacSNorm)
        {
            return (ulong)PVRTexLibPixelFormat.EAC_RG11;
        }

        throw new NotSupportedException($"No PVRTexLib pixel format is mapped for '{format.Name}'.");
    }

    private static TextureFormat GetFormat(string name) => name switch
    {
        nameof(TextureFormats.R11EacUNorm) => TextureFormats.R11EacUNorm,
        nameof(TextureFormats.R11EacSNorm) => TextureFormats.R11EacSNorm,
        nameof(TextureFormats.Rg11EacUNorm) => TextureFormats.Rg11EacUNorm,
        nameof(TextureFormats.Rg11EacSNorm) => TextureFormats.Rg11EacSNorm,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
