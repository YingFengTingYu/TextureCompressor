namespace TextureCompressor.FileFormats.Dds;

public enum DdsAlphaMode : uint
{
    Unknown = 0,
    Straight = 1,
    Premultiplied = 2,
    Opaque = 3,
    Custom = 4
}
