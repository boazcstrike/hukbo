using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal static class BattleEventFormatter
{
    public static string Format(BattleEvent battleEvent) =>
        $"T{battleEvent.Tick:00000}  " +
        $"{GetActorLabel(battleEvent)} {GetActionLabel(battleEvent)}";

    public static string GetActorLabel(BattleEvent battleEvent) =>
        battleEvent.Kind == BattleEventKind.Outcome
            ? "Battle"
            : $"{GetFactionLabel(battleEvent.FactionId)} #{battleEvent.SourceEntityId}";

    public static string GetActionLabel(BattleEvent battleEvent)
    {
        var target = battleEvent.TargetEntityId is { } targetId
            ? $"#{targetId}"
            : "none";

        return battleEvent.Kind switch
        {
            BattleEventKind.Move => $"moved toward {target}",
            BattleEventKind.Attack => FormatAttack(battleEvent, target),
            BattleEventKind.Damage =>
                $"took {battleEvent.Value} damage",
            BattleEventKind.Death => "died",
            BattleEventKind.Outcome =>
                GetOutcomeLabel(battleEvent.FactionId),
            _ => "unknown event",
        };
    }

    private static string FormatAttack(BattleEvent battleEvent, string target)
    {
        if (battleEvent.Weapon is not { } weapon ||
            battleEvent.Shield is not { } shield ||
            battleEvent.HitLocation is not { } hitLocation)
        {
            throw new InvalidOperationException(
                "Attack events must carry weapon, shield, and hit location " +
                "metadata.");
        }

        return $"hit {target}'s {GetBodyPartLabel(hitLocation)} with " +
            $"{GetWeaponLabel(weapon, shield)} for {battleEvent.Value}";
    }

    /// <summary>
    /// The pair-form weapon label: the Filipino name, an em dash, and a plain
    /// English descriptor. The descriptor is what the game guarantees; the
    /// Filipino name is what the tradition offers. Never a bare cultural
    /// identification — see CLAUDE.md section 7.
    /// </summary>
    internal static string GetWeaponLabel(WeaponId weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => "Kampilan — Great Blade",
            WeaponId.Wasay => "Wasay — War Axe",
            WeaponId.Kalis => "Kalis — Thrusting Blade",
            WeaponId.Itak => "Itak — Work Blade",
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                null),
        };

    /// <summary>
    /// The pair-form label plus the grip, for a one-handed weapon only.
    /// </summary>
    /// <remarks>
    /// A shielded and an unshielded warrior of the same one-handed weapon
    /// deal different damage, so the bare label would be actively misleading:
    /// the same string could mean either value. A two-handed weapon appends
    /// nothing, because it has no second form to be confused with.
    /// </remarks>
    internal static string GetWeaponLabel(WeaponId weapon, ShieldId shield)
    {
        var label = GetWeaponLabel(weapon);
        return GetGripSuffix(weapon, shield) is { } suffix
            ? $"{label} ({suffix})"
            : label;
    }

    /// <summary>
    /// <c>solo</c> or <c>shielded</c> for a one-handed weapon, and
    /// <c>null</c> for a two-handed one.
    /// </summary>
    internal static string? GetGripSuffix(WeaponId weapon, ShieldId shield)
    {
        // Read from the preset rather than hard-coded here: which weapons are
        // two-handed is authoritative configuration, not a presentation
        // choice. A preset that declares no attributes at all answers null,
        // and no suffix is drawn.
        if (CombatPresetRegistry.TryResolveGrip(weapon) is not
            WeaponGrip.OneHanded)
        {
            return null;
        }

        return shield == ShieldId.None ? "solo" : "shielded";
    }

    private static string GetBodyPartLabel(BodyPart bodyPart) =>
        bodyPart switch
        {
            BodyPart.WeaponArm => "weapon arm",
            BodyPart.ShieldArm => "shield arm",
            BodyPart.Shoulder => "shoulder",
            BodyPart.Head => "head",
            BodyPart.Neck => "neck",
            BodyPart.Face => "face",
            BodyPart.Chest => "chest",
            BodyPart.Abdomen => "abdomen",
            BodyPart.Thigh => "thigh",
            BodyPart.Knee => "knee",
            BodyPart.Shin => "shin",
            BodyPart.Hands => "hands",
            BodyPart.Feet => "feet",
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyPart),
                bodyPart,
                null),
        };

    public static string GetFactionLabel(int? factionId) =>
        factionId switch
        {
            0 => "Blue",
            1 => "Red",
            int value => $"Faction {value}",
            null => "Agent",
        };

    public static string GetKindLabel(BattleEventKind? kind) =>
        kind?.ToString().ToUpperInvariant() ?? "ALL TYPES";

    private static string GetOutcomeLabel(int? factionId) =>
        factionId switch
        {
            0 => "Blue wins",
            1 => "Red wins",
            _ => "Draw",
        };
}
