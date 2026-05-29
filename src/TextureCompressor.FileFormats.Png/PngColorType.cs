namespace TextureCompressor.FileFormats.Png;

public enum PngColorType : byte
{
    Grayscale = 0,
    Truecolor = 2,
    IndexedColor = 3,
    GrayscaleAlpha = 4,
    TruecolorAlpha = 6
}
