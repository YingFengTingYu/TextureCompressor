using System.Diagnostics;
using System.Text.Json;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;

namespace TextureCompressor.Cli.Tests;

[Collection(CliTestCollection.Name)]
public sealed class CliWorkflowTests
{
    [Fact]
    public async Task FormatsJsonPrintsStructuredFormatEntries()
    {
        using var workspace = new CliWorkspace();

        var output = await RunCliAsync(workspace, "formats", "bc7", "--compressed", "--json");

        using var document = JsonDocument.Parse(output.StandardOutput);
        Assert.Equal("bc7", document.RootElement.GetProperty("query").GetString());
        Assert.True(document.RootElement.GetProperty("filters").GetProperty("compressed").GetBoolean());
        Assert.False(document.RootElement.GetProperty("filters").GetProperty("uncompressed").GetBoolean());
        Assert.True(document.RootElement.GetProperty("count").GetInt32() > 0);
        var firstFormat = document.RootElement.GetProperty("formats").EnumerateArray().First();
        Assert.True(firstFormat.TryGetProperty("fieldName", out _));
        Assert.True(firstFormat.TryGetProperty("formatName", out _));
        Assert.True(firstFormat.GetProperty("isCompressed").GetBoolean());
        Assert.True(firstFormat.GetProperty("bitsPerBlock").GetInt32() > 0);
    }

    [Fact]
    public async Task ConvertAndExtractCanSelectGeneratedPvrMips()
    {
        using var workspace = new CliWorkspace();
        var source = GetNormalizedFixturePath("gradients-512.png");
        var pvr = workspace.GetPath("generated.pvr");
        var convertedMip = workspace.GetPath("converted-mip2.png");
        var extractedDirectory = workspace.GetPath("extracted");
        var fullExtractedDirectory = workspace.GetPath("full-extracted");
        var rebuiltPvr = workspace.GetPath("rebuilt.pvr");
        var rebuiltMip = workspace.GetPath("rebuilt-mip2.png");

        await RunCliAsync(workspace, "convert", source, pvr, "--format", "Rgba8UNorm", "--mipmaps", "Generate");
        var info = await RunCliAsync(workspace, "info", pvr, "--subresources");
        Assert.Contains("Container: PVR", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=1 layer=0 face=0 size=256x256", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=2 layer=0 face=0 size=128x128", info.StandardOutput, StringComparison.Ordinal);

        await RunCliAsync(workspace, "convert", pvr, convertedMip, "--mip", "2");
        AssertPngSize(convertedMip, 128, 128);

        await RunCliAsync(workspace, "extract", pvr, extractedDirectory, "--mip", "1", "--manifest");
        AssertPngSize(Path.Combine(extractedDirectory, "mip1_layer0_face0.png"), 256, 256);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(extractedDirectory, "manifest.json")));
        Assert.Equal("PVR", manifest.RootElement.GetProperty("sourceContainer").GetString());
        Assert.Equal("Rgba8UNorm", manifest.RootElement.GetProperty("sourceFormatName").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("images").GetArrayLength());
        Assert.Equal(1, manifest.RootElement.GetProperty("images")[0].GetProperty("mip").GetInt32());

        await RunCliAsync(workspace, "extract", pvr, fullExtractedDirectory, "--manifest");
        await RunCliAsync(workspace, "assemble", rebuiltPvr, "--manifest", Path.Combine(fullExtractedDirectory, "manifest.json"));
        var rebuiltInfo = await RunCliAsync(workspace, "info", rebuiltPvr, "--subresources");
        Assert.Contains("Container: PVR", rebuiltInfo.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=2 layer=0 face=0 size=128x128", rebuiltInfo.StandardOutput, StringComparison.Ordinal);
        await RunCliAsync(workspace, "convert", rebuiltPvr, rebuiltMip, "--mip", "2");
        AssertPngSize(rebuiltMip, 128, 128);
    }

    [Fact]
    public async Task AssembleAndExtractPreserveKtxArrayLayerOrder()
    {
        using var workspace = new CliWorkspace();
        var layer0 = GetNormalizedFixturePath("gradients-512.png");
        var layer1 = GetNormalizedFixturePath("hard-edges-512.png");
        var layer2 = GetNormalizedFixturePath("natural-scene-512.png");
        var ktx = workspace.GetPath("array.ktx2");
        var extractedDirectory = workspace.GetPath("extracted");
        var fullExtractedDirectory = workspace.GetPath("full-extracted");
        var rebuiltKtx = workspace.GetPath("rebuilt.ktx2");
        var rebuiltExtractedDirectory = workspace.GetPath("rebuilt-extracted");

        await RunCliAsync(workspace, "assemble", ktx, "--layers", layer0, layer1, layer2, "--ktx-version", "2");
        var info = await RunCliAsync(workspace, "info", ktx, "--subresources");
        Assert.Contains("Container: KTX", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Array layers: 3", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=0 layer=1 face=0 size=512x512", info.StandardOutput, StringComparison.Ordinal);
        var jsonInfo = await RunCliAsync(workspace, "info", ktx, "--json", "--subresources");
        using var infoDocument = JsonDocument.Parse(jsonInfo.StandardOutput);
        Assert.Equal("KTX", infoDocument.RootElement.GetProperty("container").GetString());
        Assert.Equal("Rgba8UNorm", infoDocument.RootElement.GetProperty("format").GetString());
        Assert.Equal("RGBA8_UNORM", infoDocument.RootElement.GetProperty("formatName").GetString());
        Assert.Equal(3, infoDocument.RootElement.GetProperty("arrayLayers").GetInt32());
        Assert.Equal(3, infoDocument.RootElement.GetProperty("subresources").GetArrayLength());
        Assert.Equal(1, infoDocument.RootElement.GetProperty("subresources")[1].GetProperty("layer").GetInt32());
        Assert.Equal(2, infoDocument.RootElement.GetProperty("ktx").GetProperty("version").GetInt32());

        await RunCliAsync(workspace, "extract", ktx, extractedDirectory, "--layer", "1", "--manifest");

        var extracted = Path.Combine(extractedDirectory, "mip0_layer1_face0.png");
        Assert.Single(Directory.EnumerateFiles(extractedDirectory, "*.png"));
        AssertPngPixelsEqual(layer1, extracted);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(extractedDirectory, "manifest.json")));
        Assert.Equal("KTX", manifest.RootElement.GetProperty("sourceContainer").GetString());
        Assert.Equal(3, manifest.RootElement.GetProperty("arrayLayers").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("images")[0].GetProperty("layer").GetInt32());

        await RunCliAsync(workspace, "extract", ktx, fullExtractedDirectory, "--manifest");
        await RunCliAsync(workspace, "assemble", rebuiltKtx, "--manifest", Path.Combine(fullExtractedDirectory, "manifest.json"), "--ktx-version", "2");
        var rebuiltInfo = await RunCliAsync(workspace, "info", rebuiltKtx, "--subresources");
        Assert.Contains("Array layers: 3", rebuiltInfo.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=0 layer=2 face=0 size=512x512", rebuiltInfo.StandardOutput, StringComparison.Ordinal);
        await RunCliAsync(workspace, "extract", rebuiltKtx, rebuiltExtractedDirectory, "--layer", "2");
        AssertPngPixelsEqual(layer2, Path.Combine(rebuiltExtractedDirectory, "mip0_layer2_face0.png"));
    }

    [Fact]
    public async Task AssembleAndExtractPreserveDdsCubeFaceOrder()
    {
        using var workspace = new CliWorkspace();
        var faceFiles = new[]
        {
            WriteSolidPng(workspace, "positive-x.png", new Rgba8UNorm(255, 0, 0, 255)),
            WriteSolidPng(workspace, "negative-x.png", new Rgba8UNorm(0, 255, 0, 255)),
            WriteSolidPng(workspace, "positive-y.png", new Rgba8UNorm(0, 0, 255, 255)),
            WriteSolidPng(workspace, "negative-y.png", new Rgba8UNorm(255, 255, 0, 255)),
            WriteSolidPng(workspace, "positive-z.png", new Rgba8UNorm(255, 0, 255, 255)),
            WriteSolidPng(workspace, "negative-z.png", new Rgba8UNorm(0, 255, 255, 255))
        };
        var dds = workspace.GetPath("cube.dds");
        var extractedDirectory = workspace.GetPath("extracted");
        var fullExtractedDirectory = workspace.GetPath("full-extracted");
        var rebuiltDds = workspace.GetPath("rebuilt.dds");
        var rebuiltExtractedDirectory = workspace.GetPath("rebuilt-extracted");

        await RunCliAsync(workspace, new[] { "assemble", dds, "--cube" }.Concat(faceFiles).ToArray());
        var info = await RunCliAsync(workspace, "info", dds, "--subresources");
        Assert.Contains("Container: DDS", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Faces: 6", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=0 layer=0 face=PositiveZ size=8x8", info.StandardOutput, StringComparison.Ordinal);

        await RunCliAsync(
            workspace,
            "extract",
            dds,
            extractedDirectory,
            "--face",
            "PositiveZ",
            "--pattern",
            "face_{face}_{faceIndex}",
            "--manifest");

        var extracted = Path.Combine(extractedDirectory, "face_PositiveZ_4.png");
        Assert.Single(Directory.EnumerateFiles(extractedDirectory, "*.png"));
        AssertPngPixelsEqual(faceFiles[4], extracted);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(extractedDirectory, "manifest.json")));
        Assert.Equal("DDS", manifest.RootElement.GetProperty("sourceContainer").GetString());
        Assert.Equal(6, manifest.RootElement.GetProperty("faces").GetInt32());
        Assert.Equal("PositiveZ", manifest.RootElement.GetProperty("images")[0].GetProperty("face").GetString());
        Assert.Equal(4, manifest.RootElement.GetProperty("images")[0].GetProperty("faceIndex").GetInt32());

        await RunCliAsync(workspace, "extract", dds, fullExtractedDirectory, "--manifest");
        await RunCliAsync(workspace, "assemble", rebuiltDds, "--manifest", Path.Combine(fullExtractedDirectory, "manifest.json"));
        var rebuiltInfo = await RunCliAsync(workspace, "info", rebuiltDds, "--subresources");
        Assert.Contains("Faces: 6", rebuiltInfo.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mip=0 layer=0 face=PositiveZ size=8x8", rebuiltInfo.StandardOutput, StringComparison.Ordinal);
        await RunCliAsync(workspace, "extract", rebuiltDds, rebuiltExtractedDirectory, "--face", "PositiveZ");
        AssertPngPixelsEqual(faceFiles[4], Path.Combine(rebuiltExtractedDirectory, "mip0_layer0_facePositiveZ.png"));
    }

    private static async Task<CliResult> RunCliAsync(CliWorkspace workspace, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workspace.Root,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add(GetCliDllPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync();
        var completedTask = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(60)));
        if (completedTask != waitTask)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"CLI command timed out: {string.Join(" ", arguments)}");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        var result = new CliResult(process.ExitCode, standardOutput, standardError);
        Assert.True(
            result.ExitCode == 0,
            $"CLI command failed with exit code {result.ExitCode}: {string.Join(" ", arguments)}{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}");

        return result;
    }

    private static string GetCliDllPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TextureCompressor.Cli.dll"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/TextureCompressor.Cli/bin", GetBuildConfiguration(), "net10.0", "TextureCompressor.Cli.dll"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not find TextureCompressor.Cli.dll.", string.Join(Environment.NewLine, candidates));
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string GetNormalizedFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/images/normalized", fileName));

    private static string WriteSolidPng(CliWorkspace workspace, string fileName, Rgba8UNorm color)
    {
        var path = workspace.GetPath(fileName);
        PngCodec.Encode(new ArrayBitmap<Rgba8UNorm>(8, 8, Enumerable.Repeat(color, 64).ToArray()), path);
        return path;
    }

    private static void AssertPngSize(string path, int width, int height)
    {
        var bitmap = PngCodec.DecodeRgba8(path);
        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
    }

    private static void AssertPngPixelsEqual(string expectedPath, string actualPath)
    {
        var expected = PngCodec.DecodeRgba8(expectedPath);
        var actual = PngCodec.DecodeRgba8(actualPath);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Pixels, actual.Pixels);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CliTestCollection
{
    public const string Name = "CLI";
}

internal sealed class CliWorkspace : IDisposable
{
    public CliWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "TextureCompressor.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string GetPath(string fileName) => Path.Combine(Root, fileName);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
