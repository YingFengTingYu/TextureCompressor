using AstcEncoder;

namespace TextureCompressor.Codecs.AstcEnc;

public sealed class AstcEncCoderOptions
{
    public float Quality { get; init; } = Astcenc.AstcencPreMedium;

    public AstcencFlags Flags { get; init; } = AstcencFlags.UseDecodeUnorm8;
}
