using Hukbo.Core.Determinism;

namespace Hukbo.Client.Audio;

/// <summary>
/// Deterministic selection of one variant index out of the resolved list for a
/// slot and hit class. Hashes the tick and the source entity ID instead of
/// drawing from <c>System.Random</c>, so a replay of the same seed requests
/// the same variant the original run played and no state needs to be stored.
/// </summary>
internal static class SoundVariantSelector
{
    private const ulong MixConstant = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Returns an index in <c>[0, variantCount)</c>. A count of zero or one
    /// always returns zero without touching the generator, since there is
    /// nothing to choose between.
    /// </summary>
    public static int Select(long tick, ulong sourceEntityId, int variantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variantCount);

        if (variantCount <= 1)
        {
            return 0;
        }

        var seed = unchecked((ulong)tick * MixConstant) ^ sourceEntityId;
        var generator = new SplitMix64(seed);
        return generator.NextInt(variantCount);
    }
}
