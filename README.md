# TextureCompressor

[中文文档](README.zh-CN.md)

TextureCompressor is a .NET texture codec library for converting bitmap data to common GPU texture formats and for reading or writing DDS, KTX, PVR, PNG, and ASTC containers.

> The public API is still early and may change before the first stable release.

## Features

- Bitmap primitives: `ArrayBitmap<TPixel>`, `BitmapView<TPixel>`, and common RGBA pixel structs.
- Texture metadata: `TextureFormats` definitions for uncompressed, packed, paletted, planar YUV, and block-compressed formats.
- Built-in texture coders: representative coverage for S3TC/DXT, RGTC/LATC, BPTC, ETC/EAC, ASTC, ATC, PVRTC, FXT1, RGBM/RGBD, YUV, depth/stencil, and XR-style formats.
- The core `TextureCompressor` package and built-in coders are fully managed and do not require external native libraries or texture-compression tools.
- File-format packages: PNG, JPEG, GIF, DDS, KTX, PVR, and ASTC read/write helpers.
- Quality analysis: whole-image and per-channel MSE, RMSE, and PSNR.
- Development CLI: format search, container metadata inspection, conversion, and quality metric output.
- Source generator: automatically generates `TextureFormatCatalog` for format enumeration and name lookup.
- Optional external encoder adapters: BCnEncoder, AstcEncoderCSharp, Basis Universal, DirectXTex, and PVRTexLib.

## Texture Format Support

README only lists the major supported families. See [docs/texture-format-support.en.md](docs/texture-format-support.en.md) for the full support list.

- Compressed textures: S3TC / DXT / BC1-BC3, RGTC / LATC / ATI, BPTC / BC6H / BC7, ETC / EAC, ASTC 2D, ATC, PVRTC, and FXT1.
- Uncompressed and non-block-compressed textures: sequential pixels, alpha/luminance/intensity, packed formats, paletted/indexed formats, YUV, depth/stencil, XR/RGBM/RGBD, and related layouts.

## Project Layout

- `src/TextureCompressor.Bitmap`: pixel structs plus bitmap and view abstractions.
- `src/TextureCompressor`: texture format definitions, core coders, and `TextureCoderManager`.
- `src/TextureCompressor.SourceGenerators`: generates `TextureFormatCatalog` for format enumeration and name lookup.
- `src/TextureCompressor.Analysis`: bitmap quality metrics.
- `src/TextureCompressor.Cli`: development command-line tool.
- `src/TextureCompressor.FileFormats.Png`: PNG decoder and encoder.
- `src/TextureCompressor.FileFormats.Jpeg`: baseline JPEG decoder and encoder.
- `src/TextureCompressor.FileFormats.Gif`: static GIF decoder and encoder.
- `src/TextureCompressor.FileFormats.Dds`: DDS/DX10 and legacy DDS container I/O.
- `src/TextureCompressor.FileFormats.Ktx`: KTX v1/v2 container I/O, including KTX2 Zstandard supercompression.
- `src/TextureCompressor.FileFormats.Pvr`: PVR v1/v2/v3 container I/O.
- `src/TextureCompressor.FileFormats.Astc`: `.astc` container I/O.
- `src/TextureCompressor.Codecs.*`: optional third-party encoder adapters.
- `tests`: core codec, file-format, and adapter tests.

## Requirements

- .NET SDK 10.0 or newer.
- The core library does not require any external texture compressor or platform-native runtime. Optional third-party encoder adapters and some auxiliary tools bring their own NuGet dependencies.

## Add It To Your Project

The repository currently ships as source projects. Add project references from your app or library:

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.Bitmap/TextureCompressor.Bitmap.csproj
dotnet add YourApp.csproj reference src/TextureCompressor/TextureCompressor.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Jpeg/TextureCompressor.FileFormats.Jpeg.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Gif/TextureCompressor.FileFormats.Gif.csproj
```

Reference the container packages you need:

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Dds/TextureCompressor.FileFormats.Dds.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Ktx/TextureCompressor.FileFormats.Ktx.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Pvr/TextureCompressor.FileFormats.Pvr.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Astc/TextureCompressor.FileFormats.Astc.csproj
```

Reference the analysis package when you need quality metrics:

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.Analysis/TextureCompressor.Analysis.csproj
```

Run the development CLI directly from the source project:

```bash
dotnet run --project src/TextureCompressor.Cli -- --help
```

## Quick Start: PNG To BC7 And Back

This example reads an RGBA8 PNG, encodes it to a BC7 texture payload, decodes it back, and writes a PNG preview:

```csharp
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

var source = PngCodec.DecodeRgba8("input.png");
var format = TextureFormats.Bc7UNorm;
var coder = TextureCoderManager.Global.GetCoder(format);

var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height)];
coder.Encode(source.AsView(), encoded);

var decoded = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);
coder.Decode(encoded, decoded.AsView());

PngCodec.Encode(decoded, "roundtrip.png");
```

`TextureCoderManager.Global` creates built-in coders by format. Built-in S3TC, FXT1, ETC/EAC, ASTC, ATC, RGTC/LATC, BPTC, and PVRTC coders also expose compression-mode options when you construct and register them yourself; see the next section. Register an external coder when you need a different implementation or higher production compression quality.

## Use Built-In High-Quality Encoding Modes

The default built-in coders use `TextureCompressionLevel.Normal` for predictable baseline conversion. The built-in S3TC, FXT1, ETC/EAC, ASTC, ATC, RGTC/LATC, BPTC, and PVRTC implementations expose faster or higher-quality search modes through the shared `TextureCompressionOptions` type and `TextureCompressionLevel` enum. Register an optioned coder with `TextureCoderManager`, and later `TextureCoderManager.Global.GetCoder(...)`, `DdsCodec.Encode(...)`, `KtxCodec.Encode(...)`, `PvrCodec.Encode(...)`, or `AstcCodec.Encode(...)` calls will prefer your registered coder. The default behavior is restored when the `using var` registration is disposed.

```csharp
using TextureCompressor.Codecs;
using TextureCompressor.Formats;
using TextureCompressor.Options;
using TextureCompressor.Registry;

var highQualityOptions = new TextureCompressionOptions
{
    CompressionMode = TextureCompressionLevel.High
};

var s3tcFormat = TextureFormats.Bc3Rgba;
using var highQualityS3tc = TextureCoderManager.Global.Register(
    s3tcFormat,
    new S3tcTextureCoder(
        s3tcFormat,
        highQualityOptions));

var fxt1Format = TextureFormats.RgbaFxt1UNorm;
using var highQualityFxt1 = TextureCoderManager.Global.Register(
    fxt1Format,
    new FxtcTextureCoder(
        fxt1Format,
        highQualityOptions));

var etcFormat = TextureFormats.RgbaEtc2EacUNorm;
using var highQualityEtc = TextureCoderManager.Global.Register(
    etcFormat,
    new EtcTextureCoder(
        etcFormat,
        highQualityOptions));

var atcFormat = TextureFormats.AtcRgbaInterpolatedAlpha;
using var highQualityAtc = TextureCoderManager.Global.Register(
    atcFormat,
    new AtcTextureCoder(
        atcFormat,
        highQualityOptions));

var rgtcFormat = TextureFormats.Bc5UNorm;
using var highQualityRgtc = TextureCoderManager.Global.Register(
    rgtcFormat,
    new RgtcLatcTextureCoder(
        rgtcFormat,
        highQualityOptions));

var bptcFormat = TextureFormats.Bc7UNorm;
using var highQualityBptc = TextureCoderManager.Global.Register(
    bptcFormat,
    new BptcTextureCoder(
        bptcFormat,
        highQualityOptions));

var astcFormat = TextureFormats.RgbaAstc8x8UNorm;
using var highQualityAstc = TextureCoderManager.Global.Register(
    astcFormat,
    new AstcTextureCoder(
        astcFormat,
        highQualityOptions));

var pvrtcFormat = TextureFormats.RgbaPvrtcI4BppUNorm;
using var exhaustivePvrtc = TextureCoderManager.Global.Register(
    pvrtcFormat,
    new PvrtcTextureCoder(
        pvrtcFormat,
        new TextureCompressionOptions { CompressionMode = TextureCompressionLevel.Exhaustive }));

var coder = TextureCoderManager.Global.GetCoder(s3tcFormat);
```

These built-in quality modes currently cover S3TC, FXT1, ETC/EAC, ASTC, ATC, RGTC/LATC, BPTC/BC6H/BC7, and PVRTC. For specialized production encoders and other formats, use the optional third-party encoder adapters below.

## Read And Write PNG

```csharp
using TextureCompressor.FileFormats.Png;

var bitmap = PngCodec.DecodeRgba8("input.png");
PngCodec.Encode(bitmap, "copy.png");
```

Decode into another pixel type when needed:

```csharp
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;

var hdrReady = PngCodec.Decode<Rgba32Float>("input.png");
```

## Read And Write JPEG And GIF

The JPEG codec supports 8-bit baseline JPEG. Use `JpegEncodingOptions.Quality` to select an output quality from 1 to 100:

```csharp
using TextureCompressor.FileFormats.Jpeg;

var bitmap = JpegCodec.DecodeRgba8("photo.jpg");
JpegCodec.Encode(bitmap, "photo-copy.jpg", new JpegEncodingOptions
{
    Quality = 92
});
```

The GIF codec targets static GIF files. Encoding keeps an exact palette when the image has 256 or fewer colors; otherwise it falls back to an RGB332 palette and preserves transparency:

```csharp
using TextureCompressor.FileFormats.Gif;

var icon = GifCodec.DecodeRgba8("icon.gif");
GifCodec.Encode(icon, "icon-copy.gif");
```

## Query Texture Formats

`TextureFormatCatalog` is generated automatically by the source generator. Use it to enumerate all `TextureFormats` fields or resolve formats by field name or format name:

```csharp
using TextureCompressor.Formats;

var bc7 = TextureFormatCatalog.Get("Bc7UNorm");
var found = TextureFormatCatalog.TryGet("BC7_UNORM", out var byFormatName);

foreach (var format in TextureFormatCatalog.All.Where(static item => item.IsCompressed))
{
    Console.WriteLine($"{TextureFormatCatalog.GetFieldName(format)}: {format.Name}");
}
```

## Compare Compression Quality

`TextureCompressor.Analysis` compares two same-sized bitmaps and reports whole-image and per-channel MSE, RMSE, and PSNR:

```csharp
using TextureCompressor.Analysis;
using TextureCompressor.FileFormats.Png;

var expected = PngCodec.DecodeRgba8("source.png");
var actual = PngCodec.DecodeRgba8("roundtrip.png");
var quality = BitmapQuality.Compare(expected, actual);

Console.WriteLine($"RMSE: {quality.RootMeanSquaredError:F4}");
Console.WriteLine($"PSNR: {quality.PeakSignalToNoiseRatio:F2} dB");
Console.WriteLine($"R channel PSNR: {quality.Red.PeakSignalToNoiseRatio:F2} dB");
```

## Write DDS

DDS encoding writes a DX10 header by default. Use `DdsEncodingOptions` to select a DXGI format, a legacy header, or a legacy FourCC:

```csharp
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.FileFormats.Png;

var bitmap = PngCodec.DecodeRgba8("albedo.png");

DdsCodec.Encode(bitmap, "albedo-bc7.dds", new DdsEncodingOptions
{
    DxgiFormat = DdsDxgiFormat.BC7UNorm
});

DdsCodec.Encode(bitmap, "albedo-dxt5.dds", new DdsEncodingOptions
{
    HeaderKind = DdsHeaderKind.Legacy,
    LegacyPixelFormat = DdsLegacyPixelFormat.Dxt5
});
```

Read a DDS and export a PNG preview:

```csharp
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.FileFormats.Png;

var decoded = DdsCodec.Decode("albedo-bc7.dds");
PngCodec.Encode(decoded, "albedo-preview.png");
```

## Write KTX / KTX2

```csharp
using TextureCompressor.FileFormats.Ktx;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;

var bitmap = PngCodec.DecodeRgba8("input.png");

KtxCodec.Encode(bitmap, "texture.ktx", new KtxEncodingOptions
{
    TextureFormat = TextureFormats.Rgba8Srgb
});

KtxCodec.Encode(bitmap, "texture.ktx2", new KtxEncodingOptions
{
    Version = KtxVersion.Version2,
    TextureFormat = TextureFormats.Bc7Srgb,
    SupercompressionScheme = KtxSupercompressionScheme.Zstandard
});
```

KTX APIs can either read container metadata or decode directly to a bitmap:

```csharp
var texture = KtxCodec.Read("texture.ktx2");
var bitmap = KtxCodec.Decode("texture.ktx2");
```

## Write PVR

```csharp
using TextureCompressor.FileFormats.Png;
using TextureCompressor.FileFormats.Pvr;
using TextureCompressor.Formats;

var bitmap = PngCodec.DecodeRgba8("input.png");

PvrCodec.Encode(bitmap, "texture.pvr", new PvrEncodingOptions
{
    TextureFormat = TextureFormats.RgbaPvrtcI4BppUNorm
});
```

PVR v1/v2/v3 files can be read. When writing legacy PVR, use `PvrEncodingOptions` to select the version and legacy pixel type preference.

## Write ASTC

```csharp
using TextureCompressor.FileFormats.Astc;
using TextureCompressor.FileFormats.Png;

var bitmap = PngCodec.DecodeRgba8("input.png");

AstcCodec.Encode(bitmap, "texture.astc", new AstcEncodingOptions
{
    BlockWidth = 6,
    BlockHeight = 6,
    Profile = AstcProfile.UNorm
});
```

When reading ASTC, pass `AstcReadOptions` if you need to choose the UNorm, sRGB, or Float profile:

```csharp
var bitmap = AstcCodec.Decode("texture.astc", new AstcReadOptions
{
    Profile = AstcProfile.UNorm
});
```

## Register Optional External Encoders

Built-in coders are useful for basic conversion and tests. For production compression quality, reference the adapter project and register it with `TextureCoderManager`. Later registrations take precedence over built-in coders; when multiple adapters support the same format, register them in the priority order you want.

Available third-party encoder adapters:

- `TextureCompressor.Codecs.BCnEncoder`: based on BCnEncoder.Net, targeting BC1/BC2/BC3/BC4/BC5/BC6H/BC7-related formats.
- `TextureCompressor.Codecs.AstcEnc`: based on AstcEncoderCSharp, targeting ASTC 2D formats.
- `TextureCompressor.Codecs.BasisUniversal`: based on BasisUniversal.NET, targeting ETC/EAC, BC/DXT, PVRTC, FXT1, ATC, and some ASTC LDR formats.
- `TextureCompressor.Codecs.DirectXTex`: based on DirectXTex, targeting BC1-BC7, DXT, RGTC/ATI, and BPTC formats.
- `TextureCompressor.Codecs.PVRTexLib`: based on PVRTexLib.NET, targeting ETC/EAC, PVRTC, BC/DXT, and ASTC formats.

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.Codecs.BCnEncoder/TextureCompressor.Codecs.BCnEncoder.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.Codecs.AstcEnc/TextureCompressor.Codecs.AstcEnc.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.Codecs.BasisUniversal/TextureCompressor.Codecs.BasisUniversal.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.Codecs.DirectXTex/TextureCompressor.Codecs.DirectXTex.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.Codecs.PVRTexLib/TextureCompressor.Codecs.PVRTexLib.csproj
```

```csharp
using TextureCompressor.Codecs;
using TextureCompressor.Codecs.AstcEnc;
using TextureCompressor.Codecs.BasisUniversal;
using TextureCompressor.Codecs.BCnEncoder;
using TextureCompressor.Codecs.DirectXTex;
using TextureCompressor.Codecs.PVRTexLib;
using TextureCompressor.Registry;

using var bcn = TextureCoderManager.Global.RegisterBCnEncoderCoders();
using var astc = TextureCoderManager.Global.RegisterAstcEncCoders();
using var basis = TextureCoderManager.Global.RegisterBasisUniversalCoders();
using var directXTex = TextureCoderManager.Global.RegisterDirectXTexCoders();
using var pvrt = TextureCoderManager.Global.RegisterPVRTexLibCoders();

// Later DdsCodec/KtxCodec/AstcCodec/TextureCoderManager.Global.GetCoder(...)
// calls use the registered external coders.
```

You can also register one format:

```csharp
using TextureCompressor.Codecs.BCnEncoder;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

using var registration = TextureCoderManager.Global.RegisterBCnEncoderCoder(TextureFormats.Bc7UNorm);
```

## CLI Tool

`TextureCompressor.Cli` is a development command-line tool for format search, container metadata inspection, conversion, and quality metric output:

```bash
dotnet run --project src/TextureCompressor.Cli -- --help
dotnet run --project src/TextureCompressor.Cli -- formats bc7 --compressed
dotnet run --project src/TextureCompressor.Cli -- formats rgba --uncompressed
dotnet run --project src/TextureCompressor.Cli -- info input.ktx2
dotnet run --project src/TextureCompressor.Cli -- convert input.png output.dds --format Bc7UNorm --mipmaps Generate --metrics
dotnet run --project src/TextureCompressor.Cli -- convert input.png output.ktx2 --format Bc7Srgb --ktx-version 2 --quality High
dotnet run --project src/TextureCompressor.Cli -- quality source.png output.dds
```

Common commands:

- `formats [query]`: list or search texture formats accepted by `--format`; add `--compressed` or `--uncompressed` to filter.
- `info <input>` / `inspect <input>`: print container metadata such as size, texture format, mip levels, payload size, and container-specific header fields.
- `convert <input> <output>`: convert between image and texture containers. The output container is inferred from the extension unless `--container` is passed explicitly.
- `quality <expected> <actual>`: decode two image/texture files and print MSE, RMSE, and PSNR; add `--ignore-alpha` to ignore alpha.

Image containers support PNG, JPEG, and GIF. `convert` provides `--png-color-space`, `--jpg-color-space`, and `--gif-color-space` conversions between Linear and Srgb. JPEG output quality is controlled with `--jpeg-quality`; S3TC, FXT1, ETC/EAC, ASTC, ATC, RGTC/LATC, BPTC, and PVRTC built-in texture encoding quality can be selected with `--quality`. DDS, KTX, and PVR outputs can generate full mip-map chains with `--mipmaps Generate`.

## Common Workflows

### PNG To DDS BC3

```csharp
var bitmap = PngCodec.DecodeRgba8("input.png");
DdsCodec.Encode(bitmap, "output.dds", new DdsEncodingOptions
{
    HeaderKind = DdsHeaderKind.Legacy,
    LegacyPixelFormat = DdsLegacyPixelFormat.Dxt5
});
```

### DDS To PNG Preview

```csharp
var bitmap = DdsCodec.Decode("input.dds");
PngCodec.Encode(bitmap, "preview.png");
```

### Work Directly With Container Payloads

If you already have a compressed payload, create the container object directly and write it:

```csharp
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.Formats;

var texture = new DdsTexture(
    TextureFormats.Bc1Rgba,
    width: 512,
    height: 512,
    payload: compressedBytes);

DdsCodec.Write(texture, "texture.dds");
```

## Current Limitations

- File-format helpers currently focus on 2D textures.
- DDS, KTX v1/v2, and PVR v3 support explicit mip-map chains. ASTC mip-map chains, legacy PVR v1/v2 mip-map chains, texture arrays, cube maps, and 3D textures are not supported yet.
- PNG supports common static PNG files. Animated PNG is not supported.
- JPEG supports baseline JPEG. Progressive JPEG is not supported.
- GIF reads the first image frame. Animation frame sequences are not emitted as animation.

## Build And Test

```bash
dotnet restore TextureCompressor.slnx
dotnet build TextureCompressor.slnx --configuration Release --no-restore
dotnet test TextureCompressor.slnx --configuration Release --no-build
dotnet format TextureCompressor.slnx --verify-no-changes --verbosity minimal
```

The test suite includes generated PNG fixtures under `tests/fixtures/images`.
Texture codec tests consume the `source/*-source.png` entries from `assets-manifest.json`.

## License

This project is licensed under the MIT License.
