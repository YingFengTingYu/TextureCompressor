namespace TextureCompressor.Formats;

public readonly record struct TextureSubresourceSelection(int MipLevel, int ArrayLayer, TextureCubeFace? Face)
{
    public int FaceIndex => Face is { } face ? (int)face : 0;

    public bool HasFace => Face is not null;

    public bool IsDefault => MipLevel == 0 && ArrayLayer == 0 && Face is null;
}
