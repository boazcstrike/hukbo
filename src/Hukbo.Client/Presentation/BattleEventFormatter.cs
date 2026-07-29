using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal static class BattleEventFormatter
{
    public static string Format(BattleEvent battleEvent, ulong scenarioSeed) =>
        $"T{battleEvent.Tick:00000}  " +
        $"{GetActorLabel(battleEvent, scenarioSeed)} " +
        $"{GetActionLabel(battleEvent)}";

    /// <summary>
    /// The full actor label, for the detail pane and the feed's own text
    /// search: the faction, the warrior's personal name, and the entity
    /// identifier.
    /// </summary>
    public static string GetActorLabel(
        BattleEvent battleEvent,
        ulong scenarioSeed) =>
        battleEvent.Kind == BattleEventKind.Outcome
            ? "Battle"
            : $"{GetFactionLabel(battleEvent.FactionId)} " +
                GetWarriorLabel(battleEvent, scenarioSeed);

    /// <summary>
    /// The actor label for a log row, whose column holds roughly fifteen
    /// characters: the warrior's personal name and entity identifier without
    /// the faction word. The faction is not lost — the row draws this text in
    /// the faction's own color, the detail pane below prints a
    /// <c>Faction:</c> line in full, and the battle report names the faction
    /// on every line.
    /// </summary>
    public static string GetRowActorLabel(
        BattleEvent battleEvent,
        ulong scenarioSeed) =>
        battleEvent.Kind == BattleEventKind.Outcome
            ? "Battle"
            : GetWarriorLabel(battleEvent, scenarioSeed);

    /// <summary>
    /// One warrior as a log line names it. Falls back to the bare identifier
    /// for an event carrying no faction, since the faction is what selects the
    /// regional name corpus and guessing one would invent a regional claim.
    /// </summary>
    private static string GetWarriorLabel(
        BattleEvent battleEvent,
        ulong scenarioSeed) =>
        battleEvent.FactionId is { } factionId
            ? WarriorNames.FormatWarrior(
                battleEvent.SourceEntityId,
                factionId,
                scenarioSeed)
            : $"#{battleEvent.SourceEntityId}";

    /// <summary>
    /// The action half of a log line. A target stays a bare identifier: the
    /// event carries the actor's faction but not the target's, and the
    /// faction is what selects the regional name corpus. Adding a target
    /// faction to <c>BattleEvent</c> would change the authoritative event
    /// record — and the event hash — for a cosmetic label, which the
    /// determinism contract does not permit.
    /// </summary>
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
            battleEvent.Shield is not { } shield ||
            battleEvent.HitLocation is not { } hitLocation ||
            battleEvent.Resolution is not { } resolution)
        {
            throw new InvalidOperationException(
                "Attack events must carry weapon, shield, hit location, and " +
                "resolution metadata.");
        }

        // The two-argument overload, so every one of the five lines below
        // carries the pair form and the grip suffix. A turned-aside blow names
        // its weapon exactly the way a landed one does; the resolution changes
        // what happened, not what the warrior was holding.
        var weaponLabel = GetWeaponLabel(weapon, shield);

        return resolution switch
        {
            AttackResolution.Landed =>
                $"hit {target}'s {GetBodyPartLabel(hitLocation)} with " +
                $"{weaponLabel} for {battleEvent.Value}" +
                (battleEvent.ComboPosition is { } position
                    ? $" (combo {position})"
                    : string.Empty),
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
