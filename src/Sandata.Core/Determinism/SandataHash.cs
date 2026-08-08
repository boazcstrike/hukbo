using Hukbo.Core.Determinism;

namespace Sandata.Core.Determinism;

/// <summary>
/// Sandata's own folding helper over the shared <see cref="Fnv1a"/>
/// primitive declared in <c>Hukbo.Shared.Core</c>. Every Sandata content hash
/// — this ruleset's, the map canonicalisation hash, the firearm catalog hash,
/// and the mission state hasher that follow in later tasks — folds through
/// this type rather than calling <see cref="Fnv1a"/> directly, so the
/// integral-widening rule below is written in exactly one place.
/// </summary>
/// <remarks>
/// <see cref="Fnv1a"/> is <c>internal</c> to <c>Hukbo.Shared.Core</c>, and
/// that assembly declares
/// <c>[assembly: InternalsVisibleTo("Sandata.Core")]</c> in
/// <c>src/Hukbo.Shared.Core/Properties/AssemblyInfo.cs</c> — verified present
/// before this type was written. No member of <see cref="Fnv1a"/> is widened
/// to <c>public</c>, and no new grant was added.
/// </remarks>
internal static class SandataHash
{
    /// <summary>
    /// Starts a new fold at the FNV-1a offset basis. Every Sandata content
    /// hash begins here.
    /// </summary>
    internal static ulong Begin() => Fnv1a.OffsetBasis;

    /// <summary>
    /// Folds one field into <paramref name="hash"/>. <paramref name="value"/>
    /// is <c>long</c> so every field type this shell's callers hold today —
    /// <c>int</c>, <c>ushort</c>, and an enum's underlying value cast to
    /// <c>long</c> — widens into it implicitly, and a signed value folds as
    /// its two's-complement 64-bit pattern, matching the convention
    /// <c>MovementRuleset.ComputeContentHash</c> already uses for a signed
    /// cell offset.
    /// </summary>
    internal static void Fold(ref ulong hash, long value) =>
        Fnv1a.Add(ref hash, unchecked((ulong)value));

    /// <summary>
    /// Folds one boolean field as <c>1</c> or <c>0</c>, matching the
    /// convention <c>MovementRuleset.ComputeContentHash</c> already uses.
    /// </summary>
    internal static void Fold(ref ulong hash, bool value) =>
        Fnv1a.Add(ref hash, value ? 1UL : 0UL);
}
