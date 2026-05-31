# TextureCompressor

TextureCompressor 是一个面向 .NET 的纹理编解码库，用于把普通位图数据转换为常见 GPU 纹理格式，也可以读取和写入 DDS、KTX、PVR、PNG、ASTC 等文件容器。

> 当前公共 API 仍处于早期阶段，在首个稳定版本发布前可能会调整。

## 功能概览

- 位图基础类型：`ArrayBitmap<TPixel>`、`BitmapView<TPixel>` 和常用 RGBA 像素结构。
- 纹理格式元数据：`TextureFormats` 提供未压缩、打包、调色板、平面 YUV、块压缩等格式定义。
- 内置纹理 coder：支持 S3TC/DXT、RGTC/LATC、BPTC、ETC/EAC、ASTC、ATC、PVRTC、FXT1、RGBM/RGBD、YUV、深度/模板和 XR 风格格式中的代表性格式。
- 核心 `TextureCompressor` 包和内置 coder 为纯托管实现，不依赖外部原生库或压缩工具。
- 文件格式包：PNG、JPEG、GIF、DDS、KTX、PVR、ASTC 的读取、写入和位图转换。
- 质量分析：对两张位图计算整体和逐通道 MSE、RMSE、PSNR。
- 开发 CLI：可进行格式查询、容器元数据查看、容器转换和质量指标输出。
- Source generator：自动生成 `TextureFormatCatalog`，用于按字段名或格式名查询纹理格式。
- 可选外部编码器适配：BCnEncoder、AstcEncoderCSharp、Basis Universal、DirectXTex、PVRTexLib。

## 纹理格式支持情况

README 只列主要支持的大类。完整支持列表见 [docs/texture-format-support.zh-CN.md](docs/texture-format-support.zh-CN.md)。

- 压缩纹理：S3TC / DXT / BC1-BC3、RGTC / LATC / ATI、BPTC / BC6H / BC7、ETC / EAC、ASTC 2D、ATC、PVRTC、FXT1。
- 未压缩和非块压缩纹理：顺序像素、Alpha/Luminance/Intensity、打包格式、调色板/索引格式、YUV、深度/模板、XR/RGBM/RGBD 等。

## 项目结构

- `src/TextureCompressor.Bitmap`：像素结构、位图和 view 抽象。
- `src/TextureCompressor`：纹理格式定义、核心 coder 和 `TextureCoderManager`。
- `src/TextureCompressor.SourceGenerators`：生成 `TextureFormatCatalog`，便于枚举和按名称查找格式。
- `src/TextureCompressor.Analysis`：位图质量指标计算。
- `src/TextureCompressor.Cli`：开发用命令行工具。
- `src/TextureCompressor.FileFormats.Png`：PNG 解码和编码。
- `src/TextureCompressor.FileFormats.Jpeg`：baseline JPEG 解码和编码。
- `src/TextureCompressor.FileFormats.Gif`：静态 GIF 解码和编码。
- `src/TextureCompressor.FileFormats.Dds`：DDS/DX10 与 legacy DDS 容器读写。
- `src/TextureCompressor.FileFormats.Ktx`：KTX v1/v2 容器读写，KTX2 支持 Zstandard supercompression。
- `src/TextureCompressor.FileFormats.Pvr`：PVR v1/v2/v3 容器读写。
- `src/TextureCompressor.FileFormats.Astc`：`.astc` 容器读写。
- `src/TextureCompressor.Codecs.*`：可选第三方编码器适配层。
- `tests`：核心 codec、文件格式和第三方适配测试。

## 环境要求

- .NET SDK 10.0 或更新版本。
- 核心库不需要安装任何外部纹理压缩器或平台原生运行时。只有可选第三方编码器适配项目和部分附加工具会引入各自的 NuGet 依赖。

## 安装到你的项目

当前仓库以源码项目为主，可以在同一个 solution 中用项目引用接入：

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.Bitmap/TextureCompressor.Bitmap.csproj
dotnet add YourApp.csproj reference src/TextureCompressor/TextureCompressor.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Png/TextureCompressor.FileFormats.Png.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Jpeg/TextureCompressor.FileFormats.Jpeg.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Gif/TextureCompressor.FileFormats.Gif.csproj
```

如果需要 DDS、KTX、PVR 或 ASTC 容器，再引用对应项目：

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Dds/TextureCompressor.FileFormats.Dds.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Ktx/TextureCompressor.FileFormats.Ktx.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Pvr/TextureCompressor.FileFormats.Pvr.csproj
dotnet add YourApp.csproj reference src/TextureCompressor.FileFormats.Astc/TextureCompressor.FileFormats.Astc.csproj
```

如果需要质量指标计算或复用 CLI 里的格式查询能力，可以引用：

```bash
dotnet add YourApp.csproj reference src/TextureCompressor.Analysis/TextureCompressor.Analysis.csproj
```

开发 CLI 可直接通过源码项目运行：

```bash
dotnet run --project src/TextureCompressor.Cli -- --help
```

## 快速开始：PNG 转 BC7 再转回 PNG

下面的示例从 PNG 读取 RGBA8 位图，编码为 BC7 纹理载荷，再解码回 PNG：

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

`TextureCoderManager.Global` 会按格式自动创建内置 coder。内置 S3TC、FXT1、ETC/EAC、ASTC、ATC、RGTC/LATC、BPTC、PVRTC coder 也支持自行构造并注册时传入压缩质量选项，见下一节。如果没有内置支持或你想替换为高质量第三方编码器，可以注册自定义 coder，见后文。

## 使用内置高质量编码模式

默认的内置 coder 使用 `TextureCompressionLevel.Normal`，提供可预测的基础转换。S3TC、FXT1、ETC/EAC、ASTC、ATC、RGTC/LATC、BPTC、PVRTC 的内置实现通过统一的 `TextureCompressionOptions` 类型和 `TextureCompressionLevel` 枚举提供更快或更高质量的搜索模式；把带选项的 coder 注册到 `TextureCoderManager` 后，后续 `TextureCoderManager.Global.GetCoder(...)`、`DdsCodec.Encode(...)`、`KtxCodec.Encode(...)`、`PvrCodec.Encode(...)` 或 `AstcCodec.Encode(...)` 都会优先使用你注册的版本。`using var` 作用域结束后会自动恢复默认行为。

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

这些高质量模式目前覆盖 S3TC、FXT1、ETC/EAC、ASTC、ATC、RGTC/LATC、BPTC/BC6H/BC7、PVRTC。专用生产级编码器以及其他格式可继续使用后文的可选第三方编码器适配。

## 读写 PNG

```csharp
using TextureCompressor.FileFormats.Png;

var bitmap = PngCodec.DecodeRgba8("input.png");
PngCodec.Encode(bitmap, "copy.png");
```

也可以解码成其他像素类型：

```csharp
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;

var hdrReady = PngCodec.Decode<Rgba32Float>("input.png");
```

## 读写 JPEG 和 GIF

JPEG codec 支持 8-bit baseline JPEG。编码时可通过 `JpegEncodingOptions.Quality` 设置 1 到 100 的质量值：

```csharp
using TextureCompressor.FileFormats.Jpeg;

var bitmap = JpegCodec.DecodeRgba8("photo.jpg");
JpegCodec.Encode(bitmap, "photo-copy.jpg", new JpegEncodingOptions
{
    Quality = 92
});
```

GIF codec 面向静态 GIF。编码时会优先保留 256 色以内的精确调色板；颜色超过限制时会降到 RGB332 调色板，并保留透明色：

```csharp
using TextureCompressor.FileFormats.Gif;

var icon = GifCodec.DecodeRgba8("icon.gif");
GifCodec.Encode(icon, "icon-copy.gif");
```

## 查询纹理格式

`TextureFormatCatalog` 由 source generator 自动生成，可用于枚举所有 `TextureFormats` 字段，或者按字段名/格式名查询：

```csharp
using TextureCompressor.Formats;

var bc7 = TextureFormatCatalog.Get("Bc7UNorm");
var found = TextureFormatCatalog.TryGet("BC7_UNORM", out var byFormatName);

foreach (var format in TextureFormatCatalog.All.Where(static item => item.IsCompressed))
{
    Console.WriteLine($"{TextureFormatCatalog.GetFieldName(format)}: {format.Name}");
}
```

## 比较压缩质量

`TextureCompressor.Analysis` 可以比较两张尺寸相同的位图，输出整体和逐通道的 MSE、RMSE、PSNR：

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

## 写入 DDS

默认写入 DX10 DDS header。通过 `DdsEncodingOptions` 可以选择 DXGI 格式、legacy header 或 legacy FourCC：

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

读取 DDS 并导出 PNG：

```csharp
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.FileFormats.Png;

var decoded = DdsCodec.Decode("albedo-bc7.dds");
PngCodec.Encode(decoded, "albedo-preview.png");
```

## 写入 KTX / KTX2

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

KTX 读取同样分为读取容器元数据和直接解码到位图：

```csharp
var texture = KtxCodec.Read("texture.ktx2");
var bitmap = KtxCodec.Decode("texture.ktx2");
```

## 写入 PVR

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

PVR v1/v2/v3 均可读取；写入 legacy PVR 时可使用 `PvrEncodingOptions` 选择版本和 legacy pixel type 偏好。

## 写入 ASTC

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

读取 ASTC 时，如果需要指定 UNorm、sRGB 或 Float profile，可以传入 `AstcReadOptions`：

```csharp
var bitmap = AstcCodec.Decode("texture.astc", new AstcReadOptions
{
    Profile = AstcProfile.UNorm
});
```

## 注册可选第三方编码器

内置 coder 适合基础转换和测试。如果需要更成熟的压缩质量，可以引用相应适配项目并注册到 `TextureCoderManager`。后注册的 coder 会优先于内置 coder；如果多个第三方适配器支持同一格式，请按你希望的优先级注册。

当前提供的第三方编码器适配：

- `TextureCompressor.Codecs.BCnEncoder`：基于 BCnEncoder.Net，面向 BC1/BC2/BC3/BC4/BC5/BC6H/BC7 相关格式。
- `TextureCompressor.Codecs.AstcEnc`：基于 AstcEncoderCSharp，面向 ASTC 2D 相关格式。
- `TextureCompressor.Codecs.BasisUniversal`：基于 BasisUniversal.NET，面向 ETC/EAC、BC/DXT、PVRTC、FXT1、ATC、部分 ASTC LDR 相关格式。
- `TextureCompressor.Codecs.DirectXTex`：基于 DirectXTex，面向 BC1-BC7、DXT、RGTC/ATI、BPTC 相关格式。
- `TextureCompressor.Codecs.PVRTexLib`：基于 PVRTexLib.NET，面向 ETC/EAC、PVRTC、BC/DXT、ASTC 相关格式。

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

// 后续 DdsCodec/KtxCodec/AstcCodec/TextureCoderManager.Global.GetCoder(...)
// 会使用已注册的外部 coder。
```

也可以只为特定格式注册：

```csharp
using TextureCompressor.Codecs.BCnEncoder;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

using var registration = TextureCoderManager.Global.RegisterBCnEncoderCoder(TextureFormats.Bc7UNorm);
```

## CLI 工具

`TextureCompressor.Cli` 是开发用命令行工具，可以查询格式、查看容器元数据、转换容器并输出质量指标：

```bash
dotnet run --project src/TextureCompressor.Cli -- --help
dotnet run --project src/TextureCompressor.Cli -- formats bc7 --compressed
dotnet run --project src/TextureCompressor.Cli -- formats rgba --uncompressed
dotnet run --project src/TextureCompressor.Cli -- info input.ktx2
dotnet run --project src/TextureCompressor.Cli -- convert input.png output.dds --format Bc7UNorm --mipmaps Generate --metrics
dotnet run --project src/TextureCompressor.Cli -- convert input.png output.ktx2 --format Bc7Srgb --ktx-version 2 --quality High
dotnet run --project src/TextureCompressor.Cli -- quality source.png output.dds
```

常用命令：

- `formats [query]`：列出或搜索 `--format` 可用的纹理格式；可加 `--compressed` 或 `--uncompressed` 过滤。
- `info <input>` / `inspect <input>`：输出容器元数据，例如尺寸、纹理格式、mip 级数、payload 大小和容器特有 header 字段；可加 `--subresources` 列出每个 mip/layer/face 的 payload。
- `convert <input> <output>`：在图片和纹理容器之间转换。输出容器默认由扩展名推断，也可以用 `--container` 显式指定。纹理输入可用 `--mip`、`--layer`、`--face` 选择 subresource。
- `quality <expected> <actual>`：解码两张图片/纹理并输出 MSE、RMSE、PSNR；可加 `--ignore-alpha` 忽略 Alpha。可用 `--mip`/`--layer`/`--face` 同时选择两个输入的 subresource，或用 `--expected-*` 与 `--actual-*` 分别选择。

图片容器支持 PNG、JPEG、GIF。`convert` 提供 `--png-color-space`、`--jpg-color-space`、`--gif-color-space` 在 Linear 与 Srgb 之间转换；JPEG 输出可用 `--jpeg-quality` 设置质量；内置 S3TC、FXT1、ETC/EAC、ASTC、ATC、RGTC/LATC、BPTC、PVRTC 纹理编码质量可用统一的 `--quality` 选择。DDS、KTX、PVR 输出可用 `--mipmaps Generate` 生成完整 mip-map chain。

## 常见工作流

### PNG 转 DDS BC3

```csharp
var bitmap = PngCodec.DecodeRgba8("input.png");
DdsCodec.Encode(bitmap, "output.dds", new DdsEncodingOptions
{
    HeaderKind = DdsHeaderKind.Legacy,
    LegacyPixelFormat = DdsLegacyPixelFormat.Dxt5
});
```

### DDS 转 PNG 预览图

```csharp
var bitmap = DdsCodec.Decode("input.dds");
PngCodec.Encode(bitmap, "preview.png");
```

### 直接处理容器载荷

如果你已经有压缩后的 payload，可以直接创建容器对象并写出文件：

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

## 当前限制

- 文件格式读写目前主要面向 2D texture。
- DDS、KTX v1/v2、PVR v3 支持显式 mip-map chain 和完整 cube map。ASTC mip-map chain、旧版 PVR v1/v2 mip-map chain/cube map、texture array、3D texture 暂未支持。
- PNG 支持常见静态 PNG；Animated PNG 不支持。
- JPEG 支持 baseline JPEG；progressive JPEG 不支持。
- GIF 读取首个图像帧；动画帧序列不作为动画输出。

## 构建与测试

```bash
dotnet restore TextureCompressor.slnx
dotnet build TextureCompressor.slnx --configuration Release --no-restore
dotnet test TextureCompressor.slnx --configuration Release --no-build
dotnet format TextureCompressor.slnx --verify-no-changes --verbosity minimal
```

测试图片位于 `tests/fixtures/images`。纹理 codec 测试会读取 `assets-manifest.json` 中的 `source/*-source.png` 条目。

## 许可证

本项目使用 MIT License 授权。
