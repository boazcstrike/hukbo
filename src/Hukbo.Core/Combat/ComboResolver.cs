using Hukbo.Core.Determinism;

namespace Hukbo.Core.Combat;

/// <summary>
/// Stateless deterministic rolls for one attack combination decision:
/// whether a landed blow, not already part of a chain, opens one, and
/// whether an active chain survives past a blow that just landed. Same shape
/// as <see cref="HitLocationResolver.MixAttack"/> and
/// <see cref="ClashResolver.MixClash"/>: the same ruleset, loadouts, seed,
/// tick, and entity-ID tuple always resolves to the same roll; no
/// <see cref="System.Random"/>, wall-clock time, or mutable state is used,
/// and no draw is taken from any other generator, so adding this stage
/// shifts no existing behaviour.
/// </summary>
internal static class ComboResolver
{
    /// <summary>
    /// The domain tag folded first by <see cref="MixCombo"/> for an opening
    /// roll, ASCII <c>HKBO_OPN</c>. Distinct from
    /// <see cref="HitLocationResolver"/>'s <c>HKBO_HIT</c> and
    /// <see cref="ClashResolver"/>'s <c>HKBO_CLS</c>, and from
    /// <see cref="ComboContinueTag"/> below, so none of these four roll
    /// streams ever correlates with any other even though several of them
    /// share the same seed/tick/entity-ID tuple.
    /// </summary>
    /// <remarks>
    /// Internal rather than private, unlike the other resolvers' single tag:
    /// this preset's state machine lives in
    /// <c>BattleSimulation.GatherAndCommitAttacks</c>, outside this class, and
    /// selects which of the two tags to fold depending on whether it is
    /// opening or continuing a chain, so both tags must be reachable from
    /// there.
    /// </remarks>
    internal const ulong ComboOpenTag = 0x484B424F5F4F504EUL;

    /// <summary>
    /// The domain tag folded first by <see cref="MixCombo"/> for a
    /// continuation roll, ASCII <c>HKBO_CNT</c>.
    /// </summary>
    internal const ulong ComboContinueTag = 0x484B424F5F434E54UL;

    /// <summary>
    /// Computes the deterministic FNV-1a roll for one combo-chain decision.
    /// Internal (rather than private) so pinned vectors can be verified
    /// independently of the modulo-and-compare that consumes them.
    /// </summary>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="tick">The current authoritative tick.</param>
    /// <param name="sourceEntityId">The attacking entity's ID.</param>
    /// <param name="targetEntityId">The defending entity's ID.</param>
    /// <param name="weapon">The attacking weapon.</param>
    /// <param name="comboStepsRemaining">
    /// The attacker's <c>AgentState.ComboStepsRemaining</c> at the moment of
    /// this roll — <c>0</c> for an opening roll, the still-active count for a
    /// continuation roll. Folded so the roll for step <em>N</em> of a chain
    /// never coincides with the roll for any other step, even when every
    /// other word in the tuple is identical.
    /// </param>
    /// <param name="salt">
    /// <see cref="ComboOpenTag"/> or <see cref="ComboContinueTag"/>, chosen
    /// by the caller.
    /// </param>
    /// <remarks>
    /// Returns the raw 64-bit hash, unlike
    /// <see cref="ClashResolver.MixClash"/>, which reduces internally: both
    /// the open and continue call sites reduce this same raw value with
    /// <c>% ClashProfile.BasisPointScale</c> themselves, against their own
    /// distinct tag, rather than this method reducing it for only one of
    /// them.
    /// </remarks>
    internal static ulong MixCombo(
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId weapon,
        int comboStepsRemaining,
        ulong salt)
    {
        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, salt);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, unchecked((ulong)tick));
        Fnv1a.Add(ref hash, sourceEntityId);
        Fnv1a.Add(ref hash, targetEntityId);
        Fnv1a.Add(ref hash, (ulong)weapon);
        Fnv1a.Add(ref hash, (ulong)(uint)comboStepsRemaining);
        return hash;
    }
}
