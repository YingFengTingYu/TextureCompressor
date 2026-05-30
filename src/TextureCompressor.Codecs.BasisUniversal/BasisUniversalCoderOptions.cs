using BasisUniversal;

namespace TextureCompressor.Codecs.BasisUniversal;

public sealed class BasisUniversalCoderOptions
{
    public BasisTextureFormat? Format { get; init; }

    public int QualityLevel { get; init; } = 50;

    public int EffortLevel { get; init; } = 2;

    public BasisCompressionFlags Flags { get; init; } = BasisCompressionFlags.Threaded;

    public float RdoOrDctQuality { get; init; } = 1.0f;

    public BasisDecodeFlags DecodeFlags { get; init; } = BasisDecodeFlags.HighQuality;
}
