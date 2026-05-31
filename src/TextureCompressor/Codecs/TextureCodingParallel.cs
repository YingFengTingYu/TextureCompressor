using System.Runtime.CompilerServices;

namespace TextureCompressor.Codecs;

internal static class TextureCodingParallel
{
    private const int MinimumParallelBlocks = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldParallelize(int blockCountX, int blockCountY) =>
        Environment.ProcessorCount > 1
        && blockCountY > 1
        && checked(blockCountX * blockCountY) >= MinimumParallelBlocks;
}
