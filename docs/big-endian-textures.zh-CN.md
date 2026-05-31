# 大端序纹理载荷

核心库只暴露标准 `TextureFormat`。PS3/Xbox360 等平台或容器如果需要读写大端序载荷，应在平台/容器 codec 边界用 `TextureCompressor.Utilities.BigEndianByteSwap` 转换字节，再把转换后的标准小端序载荷交给普通 texture coder。

解码大端序载荷前使用 `CopyToLittleEndian(source, destination, mode)`；编码得到标准载荷后使用 `CopyFromLittleEndian(source, destination, mode)`。调用方拥有缓冲区时也可以使用 `SwapInPlace(data, mode)`。各 mode 要求数据长度能被对应的 16 位或 32 位分组整除。

## DXT5 大端序转换例子

标准 DXT5 block 是 16 字节：

- 字节 0-1：alpha 端点
- 字节 2-7：48 位 alpha selector 数据
- 字节 8-9：小端序 RGB565 `color0`
- 字节 10-11：小端序 RGB565 `color1`
- 字节 12-15：32 位 color selector 数据

一些平台载荷会把 DXT5 block 存成 16 位大端序 word。相对于 `TextureFormats.Dxt5Rgba` 消费的标准载荷，每两个字节都会反转：

```text
标准载荷：    [a0 a1] [ai0 ai1] [ai2 ai3] [ai4 ai5] [c0_lo c0_hi] [c1_lo c1_hi] [ci0 ci1] [ci2 ci3]
大端序载荷：  [a1 a0] [ai1 ai0] [ai3 ai2] [ai5 ai4] [c0_hi c0_lo] [c1_hi c1_lo] [ci1 ci0] [ci3 ci2]
```

这种布局对应 `BigEndianByteSwapMode.Swap8In16`。

```csharp
using TextureCompressor.Formats;
using TextureCompressor.Registry;
using TextureCompressor.Utilities;

var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Dxt5Rgba);

// platformPayload 是平台/容器中的一个端序转换后 DXT5 mip level。
// bitmap 是解码目标，或编码时的源图像。
var standardPayload = new byte[coder.GetEncodedByteCount(width, height)];
BigEndianByteSwap.CopyToLittleEndian(platformPayload, standardPayload, BigEndianByteSwapMode.Swap8In16);
coder.Decode(standardPayload, bitmap.AsView());

var encodedStandardPayload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
coder.Encode(bitmap.AsView(), encodedStandardPayload);

var encodedPlatformPayload = new byte[encodedStandardPayload.Length];
BigEndianByteSwap.CopyFromLittleEndian(encodedStandardPayload, encodedPlatformPayload, BigEndianByteSwapMode.Swap8In16);
```

## BigEndianByteSwapMode 参考

下面列表给出这些纹理载荷常见的大端序字节交换 mode。它只是字节交换参考；纹理格式仍然使用标准 `TextureFormats` 名称。

- `None`
    - `Alpha8UNorm`
    - `Luminance8UNorm`
- `Swap8In16`
    - `Dxt1Rgb`
    - `Dxt1Rgba`
    - `Dxt2Rgba`
    - `Dxt3Rgba`
    - `Dxt3A`
    - `Dxt3A1111`
    - `Dxt4Rgba`
    - `Dxt5Rgba`
    - `Dxt5A`
    - `Dxn`
    - `Ctx1`
    - `Luminance16UNorm`
    - `Luminance8Alpha8UNorm`
    - `Luminance32Alpha32UNorm`
    - `Rgb565UNorm`
    - `Rgb655UNorm`
    - `Rg5SNormB6UNormRev`
    - `A1Rgb5UNorm`
    - `X1Rgb5UNorm`
    - `Argb4UNorm`
    - `Xrgb4UNorm`
    - `Rgba4RevSNorm`
    - `Rg8UNorm`
    - `Rg8SNorm`
    - `Rgba16UNorm`
    - `Rgba16SNorm`
    - `Rgba16Float`
    - `R16Float`
    - `Rg32UNorm`
    - `Rg32SNorm`
    - `DepthComponent16`
- `Swap8In32`
    - `Luminance32UNorm`
    - `Luminance16Alpha16UNorm`
    - `Bgra8`
    - `Bgrx8UNorm`
    - `Rgba8UNorm`
    - `Rgba8SNorm`
    - `Rg8SNormB8UNormX8Rev`
    - `Rgb10SNormA2UNormRev`
    - `R10Gb11UNorm`
    - `Rg11B10UNorm`
    - `R10Gb11RevUNorm`
    - `Rg11B10RevUNorm`
    - `Rg11B10RevSNorm`
    - `R10Gb11RevSNorm`
    - `Bgr10A2RevUNorm`
    - `Bgr10X2RevUNorm`
    - `Rgb10A2RevUNorm`
    - `Rg16UNorm`
    - `Rg16SNorm`
    - `Rg16Float`
    - `Rgba32UNorm`
    - `Rgba32SNorm`
    - `R32Float`
    - `Rg32Float`
    - `Rgba32Float`
    - `R11G11B10Float`
    - `Uyvy422UNorm`
    - `Yuy2UNorm`
    - `G8R8G8B8_422UNorm`
    - `R8G8B8G8_422UNorm`
    - `Depth24X8`
    - `Depth24Stencil8`
    - `Depth24FloatStencil8`
- `Swap16In32`
    - 只有在平台/容器布局明确需要交换每个 32 位 word 中的两个 16 位 half-word 时才使用。
