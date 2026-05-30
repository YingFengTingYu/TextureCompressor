using PVRTexLib;

namespace TextureCompressor.Codecs.PVRTexLib;

public sealed class PVRTexLibCompressorOptions
{
    public PVRTexLibCompressorQuality EtcQuality { get; init; } = PVRTexLibCompressorQuality.ETCNormal;

    public PVRTexLibCompressorQuality PvrtcQuality { get; init; } = PVRTexLibCompressorQuality.PVRTCNormal;

    public PVRTexLibCompressorQuality AstcQuality { get; init; } = PVRTexLibCompressorQuality.ASTCMedium;

    public PVRTexLibCompressorQuality BasisQuality { get; init; } = PVRTexLibCompressorQuality.BASISUNormal;

    public bool Dither { get; init; }

    public float MaxRange { get; init; }

    public int MaxThreads { get; init; }
}
