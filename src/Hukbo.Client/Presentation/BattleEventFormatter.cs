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
            battleEvent.HitLocation is not { } hitLocation)
        {
            throw new InvalidOperationException(
                "Attack events must carry weapon and hit location metadata.");
        }

        return $"hit {target}'s {GetBodyPartLabel(hitLocation)} with " +
            $"{GetWeaponLabel(weapon)} for {battleEvent.Value}";
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
