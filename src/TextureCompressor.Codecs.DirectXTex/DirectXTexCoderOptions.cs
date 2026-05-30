using Hexa.NET.DirectXTex;

namespace TextureCompressor.Codecs.DirectXTex;

public sealed class DirectXTexCoderOptions
{
    public TexCompressFlags Flags { get; init; } = TexCompressFlags.Default;

    public float AlphaWeight { get; init; } = 1.0f;
}
