using Hukbo.Core.Determinism;

namespace Hukbo.Core.Combat;

/// <summary>
/// Stateless deterministic weighted hit-location selection for one
/// accepted attack. The same ruleset, loadouts, seed, tick, and entity-ID
/// tuple always resolves to the same <see cref="BodyPart"/>; no
/// <see cref="System.Random"/>, wall-clock time, or mutable state is used.
/// </summary>
public static class HitLocationResolver
{
    private const ulong HitLocationTag = 0x484B424F5F484954UL;

    /// <summary>
    /// Resolves the authoritative body part struck by one accepted attack.
    /// </summary>
    /// <param name="rules">The combat ruleset governing target weights and defense multipliers.</param>
    /// <param name="attacker">The attacking warrior's loadout. Only <see cref="CombatLoadout.Weapon"/> affects the result.</param>
    /// <param name="defender">The defending warrior's loadout. Only <see cref="CombatLoadout.Shield"/> affects the result.</param>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="tick">The current authoritative tick. Must be nonnegative.</param>
    /// <param name="sourceEntityId">The attacking entity's ID. Must be positive.</param>
    /// <param name="targetEntityId">The defending entity's ID. Must be positive.</param>
    public static BodyPart Resolve(
        CombatRuleset rules,
        CombatLoadout attacker,
        CombatLoadout defender,
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                tick,
                "Tick must be nonnegative.");
        }

        if (sourceEntityId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceEntityId),
                sourceEntityId,
                "Source entity ID must be positive.");
        }

        if (targetEntityId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetEntityId),
                targetEntityId,
                "Target entity ID must be positive.");
        }

        var table = rules.ResolveEffectiveWeights(attacker.Weapon, defender.Shield);
        if (table.Total == 0)
        {
            throw new InvalidOperationException(
                "Resolved target weight total must be positive.");
        }

        var roll = MixAttack(seed, tick, sourceEntityId, targetEntityId, attacker.Weapon) % table.Total;
        var cumulative = 0UL;
        for (var index = 0; index < BodyPartCatalog.Ordered.Length; index++)
        {
            cumulative = checked(cumulative + table.Weights[index]);
            if (roll < cumulative)
            {
                return BodyPartCatalog.Ordered[index];
            }
        }

        throw new InvalidOperationException(
            "Hit-location roll exceeded the resolved target weight total.");
    }

    /// <summary>
    /// Computes the deterministic FNV-1a roll for one attack tuple.
    /// Internal (rather than private) so golden vectors can be verified
    /// independently of the final cumulative-interval selection.
    /// </summary>
    internal static ulong MixAttack(
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId weapon)
    {
        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, HitLocationTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, unchecked((ulong)tick));
        Fnv1a.Add(ref hash, sourceEntityId);
        Fnv1a.Add(ref hash, targetEntityId);
        Fnv1a.Add(ref hash, (ulong)weapon);
        return hash;
    }
}
