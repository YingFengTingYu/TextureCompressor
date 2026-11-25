using TextureCompressor.Bitmaps;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrEncodingOptions : IFileFormatOptions
{
    public TextureFormat? TextureFormat { get; init; }

    public bool GenerateMipmaps { get; init; }

    public MipmapGenerationOptions? MipmapOptions { get; init; }

    public PvrPixelFormat? PvrPixelFormat { get; init; }

    public PvrLegacyPixelType? PvrLegacyPixelType { get; init; }

    public PvrLegacyPixelTypePreference LegacyPixelTypePreference { get; init; }

    public bool IsSrgb { get; init; }

    /// <summary>
    /// Sets the PVR v3 premultiplied-alpha flag. The caller supplies pixels in the declared
    /// representation. Null preserves the flag of a PvrTexture passed to Write.
    /// </summary>
    public bool? IsPremultiplied { get; init; }

    public int Version { get; init; } = 3;
}
