namespace Hukbo.Core.Combat;

/// <summary>
/// Exhaustive registry of authoritative combat rulesets keyed by
/// <see cref="CombatPresetId"/>. New presets require a new enum value and
/// a corresponding switch arm here; unregistered values fail loudly rather
/// than silently falling back to a default ruleset.
/// </summary>
public static class CombatPresetRegistry
{
    public static bool IsRegistered(CombatPresetId id) =>
        id switch
        {
            CombatPresetId.PrecolonialPhilippinesV1 => true,
            CombatPresetId.PrecolonialPhilippinesV2 => true,
            CombatPresetId.PrecolonialPhilippinesV3 => true,
            CombatPresetId.PrecolonialPhilippinesV4 => true,
            CombatPresetId.PrecolonialPhilippinesV5 => true,
            _ => false,
        };

    /// <summary>
    /// The grip every registered preset agrees a weapon is fought with, or
    /// <c>null</c> when no registered preset declares attributes for it.
    /// </summary>
    /// <remarks>
    /// Presentation needs the grip to label a blow — a one-handed weapon
    /// deals different damage solo than shielded, so the bare weapon name
    /// would mean either value — but a feed line is read long after the tick
    /// that produced it and carries no preset identity. Answering from the
    /// registry rather than from a hard-coded list in the client keeps grip
    /// preset data, which is what it is.
    /// <para>
    /// <c>CombatPresetRegistryTests</c> asserts every registered preset
    /// agrees on each weapon's grip, so a future preset that disagrees fails
    /// a test rather than silently making this answer arbitrary.
    /// </para>
    /// </remarks>
    public static WeaponGrip? TryResolveGrip(WeaponId weapon)
    {
        foreach (var id in Enum.GetValues<CombatPresetId>().OrderBy(id => (int)id))
        {
            if (!IsRegistered(id))
            {
                continue;
            }

            var rules = Get(id);

            // Ask the preset whether it fields this weapon at all, rather
            // than taking the first preset that declares any profiles and
            // demanding an answer from it. That earlier form threw
            // ArgumentOutOfRangeException for every ranged weapon, because
            // the lowest-numbered preset with profiles is melee-only and had
            // never heard of a Bangkaw. Both callers are presentation, so the
            // exception surfaced as a client crash on the first arquebus
            // attack the event feed tried to describe, with the headless gate
            // green throughout.
            if (rules.TryResolveWeaponGrip(weapon) is { } grip)
            {
                return grip;
            }
        }

        return null;
    }

    public static CombatRuleset Get(CombatPresetId id) =>
        id switch
        {
            CombatPresetId.PrecolonialPhilippinesV1 => PhilippineCombatPreset.Rules,
            CombatPresetId.PrecolonialPhilippinesV2 => PhilippineCombatPresetV2.Rules,
            CombatPresetId.PrecolonialPhilippinesV3 => PhilippineCombatPresetV3.Rules,
            CombatPresetId.PrecolonialPhilippinesV4 => PhilippineCombatPresetV4.Rules,
            CombatPresetId.PrecolonialPhilippinesV5 => PhilippineCombatPresetV5.Rules,
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Combat preset {id} is not registered."),
        };
}
