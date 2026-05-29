namespace TextureCompressor.FileFormats.Pvr;

public sealed class PvrMetadata
{
    public PvrMetadata(uint devFourCC, uint key, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        DevFourCC = devFourCC;
        Key = key;
        Data = data;
    }

    public uint DevFourCC { get; }

    public uint Key { get; }

    public byte[] Data { get; }
}
