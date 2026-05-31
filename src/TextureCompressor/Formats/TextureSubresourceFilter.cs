namespace TextureCompressor.Formats;

public readonly record struct TextureSubresourceFilter(int? MipLevel = null, int? ArrayLayer = null, TextureCubeFace? Face = null)
{
    public int? FaceIndex => Face is { } face ? (int)face : null;

    public bool IsDefault => MipLevel is null && ArrayLayer is null && Face is null;
}
