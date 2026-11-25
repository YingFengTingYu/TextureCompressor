using Hexa.NET.DirectXTex;

namespace TextureCompressor.Codecs.DirectXTex;

public sealed class DirectXTexCoderOptions
{
    public TexCompressFlags Flags { get; init; } = TexCompressFlags.Default;

    /// <summary>Alpha cutoff used by DirectXTex when encoding BC1 with one-bit transparency.</summary>
    public float AlphaThreshold { get; init; } = 0.5f;

    /// <summary>Compatibility alias for <see cref="AlphaThreshold"/>.</summary>
    public float AlphaWeight
    {
        get => AlphaThreshold;
        init => AlphaThreshold = value;
    }
}
