# TextureCompressor

## 中文

TextureCompressor 是一个面向 .NET 的纹理编解码库，用于读写和往返转换常见
GPU 纹理数据。仓库包含位图基础类型、纹理格式元数据、内置纹理 coder，以及
用于测试夹具和简单图片 I/O 的 PNG 文件格式包。

### 项目结构

- `TextureCompressor.Bitmap`：像素结构体、位图与 view 抽象。
- `TextureCompressor`：纹理格式定义和纹理编解码器。
- `TextureCompressor.FileFormats.Png`：面向位图数据的 PNG 解码器和编码器。

### 功能

- 支持未压缩、打包、调色板、平面、块压缩等纹理格式。
- 覆盖 S3TC/DXT、RGTC/LATC、BPTC、ETC/EAC、ASTC、ATC、PVRTC、
  FXT1、RGBM/RGBD、YUV、深度/模板和 XR 风格格式中的代表性格式。
- 基于 `IPixel<TPixel>` 的泛型位图 API。
- PNG 解码/编码，可用于加载测试图片和进行简单图片交换。

### 环境要求

- .NET SDK 10.0 或更新版本。

### 快速开始

```csharp
using TextureCompressor.Bitmaps;
using TextureCompressor.Codecs;
using TextureCompressor.Colors;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.Formats;

var source = PngCodec.DecodeRgba8("input.png");
var format = TextureFormats.Bc7UNorm;
var coder = TextureCoderManager.Global.GetCoder(format);

var encoded = new byte[coder.GetEncodedByteCount(source.Width, source.Height)];
coder.Encode(source.AsView(), encoded);

var decoded = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);
coder.Decode(encoded, decoded.AsView());

PngCodec.Encode(decoded, "roundtrip.png");
```

### 构建和测试

```bash
dotnet restore TextureCompressor.slnx
dotnet build TextureCompressor.slnx --configuration Release --no-restore
dotnet test TextureCompressor.slnx --configuration Release --no-build
dotnet format TextureCompressor.slnx --verify-no-changes --verbosity minimal
```

测试套件包含 `tests/fixtures/images` 下的生成 PNG 夹具。纹理 codec 测试会读取
`assets-manifest.json` 中的 `source/*-source.png` 条目。

### 仓库状态

公开 API 仍处于早期阶段，在第一个稳定包发布前可能发生变化。

### 许可证

本项目使用 MIT License 授权。

## English

TextureCompressor is a .NET texture codec library for reading, writing, and
round-tripping common GPU texture payloads. It includes bitmap primitives,
texture format metadata, built-in texture coders, and a PNG file-format package
for test fixtures and image I/O.

### Projects

- `TextureCompressor.Bitmap`: pixel structs and bitmap/view abstractions.
- `TextureCompressor`: texture format definitions and texture coders.
- `TextureCompressor.FileFormats.Png`: PNG decoder and encoder for bitmap data.

### Features

- Uncompressed, packed, paletted, planar, and block-compressed texture formats.
- Representative support for S3TC/DXT, RGTC/LATC, BPTC, ETC/EAC, ASTC, ATC,
  PVRTC, FXT1, RGBM/RGBD, YUV, depth/stencil, and XR-style formats.
- Generic bitmap APIs over `IPixel<TPixel>` implementations.
- PNG decoding/encoding for fixture loading and simple image exchange.

### Requirements

- .NET SDK 10.0 or newer.

### Build And Test

```bash
dotnet restore TextureCompressor.slnx
dotnet build TextureCompressor.slnx --configuration Release --no-restore
dotnet test TextureCompressor.slnx --configuration Release --no-build
dotnet format TextureCompressor.slnx --verify-no-changes --verbosity minimal
```

The test suite includes generated PNG fixtures under `tests/fixtures/images`.
Texture codec tests consume the `source/*-source.png` entries from
`assets-manifest.json`.

### Repository Status

The public API is still early and may change before the first stable package
release.

### License

This project is licensed under the MIT License.
