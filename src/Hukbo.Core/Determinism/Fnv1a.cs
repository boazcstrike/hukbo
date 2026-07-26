using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hukbo.Headless")]

namespace Hukbo.Core.Determinism;

/// <summary>
/// The single canonical FNV-1a 64-bit byte-fold used by every deterministic
/// hash in Hukbo: the state hash, the headless event hash, the combat
/// ruleset content hash, and the hit-location roll mixer. Centralizes what
/// were four independently maintained copies of the same offset basis,
/// prime, and byte-fold loop. Behavior is byte-for-byte identical to each of
/// the prior copies; changing this algorithm changes every hash that uses
/// it and requires a new preset version plus new golden expectations.
/// </summary>
internal static class Fnv1a
{
    internal const ulong OffsetBasis = 14_695_981_039_346_656_037UL;

    private const ulong Prime = 1_099_511_628_211UL;

    internal static void Add(ref ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash = unchecked(hash * Prime);
        }
    }
}
