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

    /// <summary>
    /// One distinct line per resolution. The event log is the only channel
    /// that names a void at all, and it is the channel a spectator can read
    /// without knowing what to look for, so all five have to be separable
    /// here.
    /// </summary>
    /// <remarks>
    /// A non-landed blow names no body part and reports no damage figure. The
    /// simulation still resolves a hit location for one, but naming it would
    /// say the blow reached a shoulder the shield turned aside, and reporting
    /// the value would print a bare zero.
    /// </remarks>
    private static string FormatAttack(BattleEvent battleEvent, string target)
    {
        if (battleEvent.Weapon is not { } weapon ||
            battleEvent.HitLocation is not { } hitLocation ||
            battleEvent.Resolution is not { } resolution)
        {
            throw new InvalidOperationException(
                "Attack events must carry weapon, hit location, and " +
                "resolution metadata.");
        }

        var weaponLabel = GetWeaponLabel(weapon);

        return resolution switch
        {
            AttackResolution.Landed =>
                $"hit {target}'s {GetBodyPartLabel(hitLocation)} with " +
                $"{weaponLabel} for {battleEvent.Value}",
            AttackResolution.ShieldBlocked =>
                $"swung {weaponLabel} at {target} — stopped by the shield",
            AttackResolution.Parried =>
                $"swung {weaponLabel} at {target} — parried",
            AttackResolution.Deflected =>
                $"swung {weaponLabel} at {target} — turned aside",
            AttackResolution.Evaded =>
                $"swung {weaponLabel} at {target} — stepped off the line",
            _ => throw new ArgumentOutOfRangeException(
                nameof(battleEvent),
                resolution,
                null),
        };
    }

    private static string GetWeaponLabel(WeaponId weapon) =>
        weapon switch
        {
            WeaponId.GreatBlade => "Great Blade",
            WeaponId.HeavyChopper => "Heavy Chopper",
            WeaponId.ThrustingBlade => "Thrusting Blade",
            WeaponId.Bolo => "Work Blade",
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                null),
        };

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

    /// <summary>
    /// The tick-and-value row of the event detail block. A non-landed attack
    /// omits the value entirely rather than printing a bare zero, which a
    /// spectator would otherwise read as a landed blow that happened to do
    /// nothing.
    /// </summary>
    /// <remarks>
    /// It lives here rather than in the panel so that the one place deciding
    /// how an event reads is the one place tested for it; the panel that draws
    /// the block cannot be constructed in a test.
    /// </remarks>
    public static string GetDetailSummaryLine(BattleEvent battleEvent) =>
        battleEvent.Kind == BattleEventKind.Attack &&
        battleEvent.Resolution is not AttackResolution.Landed
            ? $"Tick: {battleEvent.Tick}"
            : $"Tick: {battleEvent.Tick}    Value: {battleEvent.Value}";

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
