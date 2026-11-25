using PVRTexLib;

namespace TextureCompressor.Codecs.PVRTexLib;

public sealed class PVRTexLibCompressorOptions
{
    public PVRTexLibCompressorQuality EtcQuality { get; init; } = PVRTexLibCompressorQuality.ETCNormal;

    /// <summary>
    /// Set to <see langword="true"/> when the input to ETC2 RGBA encoding already stores
    /// RGB multiplied by normalized alpha. The default is <see langword="false"/> for straight alpha.
    /// </summary>
    /// <remarks>
    /// This setting describes the pixels supplied by the caller. For example, a straight red
    /// value of 200 at alpha 128 is supplied as red 100 after premultiplication (8-bit channels).
    /// For an sRGB target, premultiply in linear RGB before the sRGB transfer.
    /// With this setting, images containing transparency use a separate RGB encoding pass
    /// to reduce bright fringes. Raw ETC2 blocks carry no alpha-association flag.
    /// Decode returns premultiplied RGB for this input. Use premultiplied source-over blending
    /// (source factor One, destination factor OneMinusSrcAlpha).
    /// </remarks>
    public bool Etc2InputIsPremultiplied { get; init; }

    public PVRTexLibCompressorQuality PvrtcQuality { get; init; } = PVRTexLibCompressorQuality.PVRTCNormal;

    public PVRTexLibCompressorQuality AstcQuality { get; init; } = PVRTexLibCompressorQuality.ASTCMedium;

    public PVRTexLibCompressorQuality BasisQuality { get; init; } = PVRTexLibCompressorQuality.BASISUNormal;

    public bool Dither { get; init; }

    public float MaxRange { get; init; }

    public int MaxThreads { get; init; }
}
