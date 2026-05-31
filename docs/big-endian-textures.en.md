# Big-Endian Texture Payloads

The core library exposes standard `TextureFormat` values only. Platform or container codecs that need a big-endian payload should convert the bytes at the platform/container boundary with `TextureCompressor.Utilities.BigEndianByteSwap`, then pass the standard little-endian payload to the normal texture coder.

Use `CopyToLittleEndian(source, destination, mode)` before decoding a big-endian payload. Use `CopyFromLittleEndian(source, destination, mode)` after encoding a standard payload. `SwapInPlace(data, mode)` is also available when the caller owns the buffer. All swap modes require complete 16-bit or 32-bit chunks as appropriate.

## DXT5 Big-Endian Conversion Example

A standard DXT5 block is 16 bytes:

- bytes 0-1: alpha endpoints
- bytes 2-7: 48 bits of alpha selector data
- bytes 8-9: `color0` in RGB565 little-endian order
- bytes 10-11: `color1` in RGB565 little-endian order
- bytes 12-15: 32 bits of color selector data

Some platform payloads store the DXT5 block as 16-bit big-endian words. Relative to the standard payload consumed by `TextureFormats.Dxt5Rgba`, each two-byte word is reversed:

```text
standard:    [a0 a1] [ai0 ai1] [ai2 ai3] [ai4 ai5] [c0_lo c0_hi] [c1_lo c1_hi] [ci0 ci1] [ci2 ci3]
big-endian:  [a1 a0] [ai1 ai0] [ai3 ai2] [ai5 ai4] [c0_hi c0_lo] [c1_hi c1_lo] [ci1 ci0] [ci3 ci2]
```

That layout corresponds to `BigEndianByteSwapMode.Swap8In16`.

```csharp
using TextureCompressor.Formats;
using TextureCompressor.Registry;
using TextureCompressor.Utilities;

var coder = TextureCoderManager.Global.GetCoder(TextureFormats.Dxt5Rgba);

// platformPayload contains one endian-swapped DXT5 mip level.
// bitmap is the decode destination, or the source image when encoding.
var standardPayload = new byte[coder.GetEncodedByteCount(width, height)];
BigEndianByteSwap.CopyToLittleEndian(platformPayload, standardPayload, BigEndianByteSwapMode.Swap8In16);
coder.Decode(standardPayload, bitmap.AsView());

var encodedStandardPayload = new byte[coder.GetEncodedByteCount(bitmap.Width, bitmap.Height)];
coder.Encode(bitmap.AsView(), encodedStandardPayload);

var encodedPlatformPayload = new byte[encodedStandardPayload.Length];
BigEndianByteSwap.CopyFromLittleEndian(encodedStandardPayload, encodedPlatformPayload, BigEndianByteSwapMode.Swap8In16);
```

## BigEndianByteSwapMode Reference

The list below gives common mode choices for payloads that use the same endian layout as these texture formats. It is only a byte-swap reference; the texture formats themselves remain the standard `TextureFormats` names.

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
    - Use this mode only when a platform/container layout swaps 16-bit halves inside each 32-bit word.
