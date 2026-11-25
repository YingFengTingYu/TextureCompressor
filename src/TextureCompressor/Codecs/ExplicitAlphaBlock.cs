using TextureCompressor.Colors;

namespace TextureCompressor.Codecs;

internal static class ExplicitAlphaBlock
{
    internal static void Encode(ReadOnlySpan<Rgba8UNorm> source, Span<byte> block)
    {
        for (int i = 0; i < 8; i++)
        {
            int low = (source[2 * i].Alpha + 8) / 17;
            int high = (source[2 * i + 1].Alpha + 8) / 17;
            block[i] = (byte)(low | (high << 4));
        }
    }

    internal static void Decode(ReadOnlySpan<byte> block, Span<Rgba8UNorm> pixels)
    {
        for (int i = 0; i < 8; i++)
        {
            pixels[2 * i].Alpha = (byte)((block[i] & 15) * 17);
            pixels[2 * i + 1].Alpha = (byte)((block[i] >> 4) * 17);
        }
    }
}
