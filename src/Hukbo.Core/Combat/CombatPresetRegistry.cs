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
            _ => false,
        };

    public static CombatRuleset Get(CombatPresetId id) =>
        id switch
        {
            CombatPresetId.PrecolonialPhilippinesV1 => PhilippineCombatPreset.Rules,
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Combat preset {id} is not registered."),
        };
}
