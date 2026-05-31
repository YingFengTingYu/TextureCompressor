using System.Text.Json;
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Tests;

public sealed class SourceImageTextureCoderTests
{
    private const int SampleWidth = 16;
    private const int SampleHeight = 16;

    [Theory]
    [MemberData(nameof(SourceFixtureNames))]
    public void ManifestTestFixturesPointAtSourceImages(string fixtureName)
    {
        Assert.StartsWith("source/", fixtureName, StringComparison.Ordinal);
        Assert.EndsWith("-source.png", fixtureName, StringComparison.Ordinal);
        Assert.True(File.Exists(GetFixturePath(fixtureName)));
    }

    [Theory]
    [MemberData(nameof(LosslessTextureCases))]
    public void SourceFixturesRoundTripThroughLosslessTextureFormats(string fixtureName, TextureFormat format)
    {
        var source = LoadSample(fixtureName);
        var decoded = EncodeAndDecode(format, source);

        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Theory]
    [MemberData(nameof(LossyTextureCases))]
    public void SourceFixturesEncodeAndDecodeThroughRepresentativeLossyTextureFormats(
        string fixtureName,
        TextureFormat format,
        double averageTolerance)
    {
        var source = LoadSample(fixtureName);
        var decoded = EncodeAndDecode(format, source);

        AssertAverageRgbErrorWithin(source.Pixels, decoded.Pixels, averageTolerance, format, fixtureName);
    }

    public static TheoryData<string> SourceFixtureNames()
    {
        var data = new TheoryData<string>();
        foreach (var fixtureName in ReadManifestTestFixtures())
        {
            data.Add(fixtureName);
        }

        return data;
    }

    public static TheoryData<string, TextureFormat> LosslessTextureCases()
    {
        var data = new TheoryData<string, TextureFormat>();
        foreach (var fixtureName in ReadManifestTestFixtures())
        {
            data.Add(fixtureName, TextureFormats.Rgba8UNorm);
            data.Add(fixtureName, TextureFormats.Bgra8);
        }

        return data;
    }

    public static TheoryData<string, TextureFormat, double> LossyTextureCases()
    {
        var data = new TheoryData<string, TextureFormat, double>();
        foreach (var fixtureName in ReadManifestTestFixtures())
        {
            data.Add(fixtureName, TextureFormats.Rgb565UNorm, 3.5d);
            data.Add(fixtureName, TextureFormats.Bc1Rgba, 60d);
            data.Add(fixtureName, TextureFormats.Dxt5Rgba, 45d);
            data.Add(fixtureName, TextureFormats.Bc7UNorm, 22d);
            data.Add(fixtureName, TextureFormats.RgbaEtc2EacUNorm, 60d);
            data.Add(fixtureName, TextureFormats.RgbaAstc4x4UNorm, 75d);
        }

        return data;
    }

    private static ArrayBitmap<Rgba8UNorm> LoadSample(string fixtureName)
    {
        var source = PngCodec.DecodeRgba8(GetFixturePath(fixtureName));
        var pixels = new Rgba8UNorm[SampleWidth * SampleHeight];

        for (var y = 0; y < SampleHeight; y++)
        {
            var sourceY = MapCoordinate(y, SampleHeight, source.Height);
            for (var x = 0; x < SampleWidth; x++)
            {
                var sourceX = MapCoordinate(x, SampleWidth, source.Width);
                pixels[(y * SampleWidth) + x] = source.Pixels[(sourceY * source.Width) + sourceX];
            }
        }

        return new ArrayBitmap<Rgba8UNorm>(SampleWidth, SampleHeight, pixels);
    }

    private static ArrayBitmap<Rgba8UNorm> EncodeAndDecode(TextureFormat format, ArrayBitmap<Rgba8UNorm> source)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);
        var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height)];
        var decoded = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);

        coder.Encode(source.AsView(), encoded);
        coder.Decode(encoded, decoded.AsView());

        return decoded;
    }

    private static void AssertAverageRgbErrorWithin(
        ReadOnlySpan<Rgba8UNorm> source,
        ReadOnlySpan<Rgba8UNorm> decoded,
        double averageTolerance,
        TextureFormat format,
        string fixtureName)
    {
        long error = 0;
        for (var i = 0; i < source.Length; i++)
        {
            error += Math.Abs(source[i].Red - decoded[i].Red);
            error += Math.Abs(source[i].Green - decoded[i].Green);
            error += Math.Abs(source[i].Blue - decoded[i].Blue);
        }

        var average = error / (double)(source.Length * 3);
        Assert.True(
            average <= averageTolerance,
            $"{format.Name} average RGB error for {fixtureName} was {average:0.##}, expected <= {averageTolerance:0.##}.");
    }

    private static int MapCoordinate(int coordinate, int destinationSize, int sourceSize)
    {
        if (destinationSize == 1)
        {
            return 0;
        }

        return (int)Math.Round(coordinate * (sourceSize - 1d) / (destinationSize - 1d));
    }

    private static string GetFixturePath(string fixtureName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/images", fixtureName));

    private static string GetManifestPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/images/assets-manifest.json"));

    private static string[] ReadManifestTestFixtures()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(GetManifestPath()));
        return manifest.RootElement
            .GetProperty("assets")
            .EnumerateArray()
            .Select(asset => asset.GetProperty("test").GetString() ?? throw new InvalidDataException("Fixture asset is missing a test path."))
            .ToArray();
    }
}
