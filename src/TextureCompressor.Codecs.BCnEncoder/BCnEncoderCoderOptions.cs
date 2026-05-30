using BCnEncoder.Encoder;
using BCnEncoder.Shared;

namespace TextureCompressor.Codecs.BCnEncoder;

public sealed class BCnEncoderCoderOptions
{
    public CompressionQuality Quality { get; init; } = CompressionQuality.Balanced;

    public ColorComponent Bc4Component { get; init; } = ColorComponent.R;

    public ColorComponent Bc5Component1 { get; init; } = ColorComponent.R;

    public ColorComponent Bc5Component2 { get; init; } = ColorComponent.G;
}
