using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Audio;

/// <summary>
/// Translates one authoritative <see cref="BattleEvent"/> into the sound slot
/// it should trigger, or <c>null</c> for an event that is deliberately silent.
/// Read-only over the event stream: nothing here can affect simulation state.
/// </summary>
internal static class SoundCueMapper
{
    /// <summary>
    /// Returns the slot for an event, or <c>null</c> when the event has no
    /// sound.
    /// </summary>
    /// <remarks>
    /// <see cref="BattleEventKind.Move"/> is silent because most living agents
    /// move on most ticks. <see cref="BattleEventKind.Damage"/> is silent
    /// because every damage event is accompanied by the
    /// <see cref="BattleEventKind.Attack"/> event that caused it, so mapping
    /// both would double every hit.
    /// </remarks>
    public static GameSoundId? Map(BattleEvent battleEvent) =>
        battleEvent.Kind switch
        {
            BattleEventKind.Attack => MapAttack(
                battleEvent.Weapon,
                battleEvent.Resolution),
            BattleEventKind.Death => GameSoundId.Death,
            BattleEventKind.Outcome => MapOutcome(battleEvent.FactionId),
            BattleEventKind.Release => MapRelease(battleEvent.Weapon),
            BattleEventKind.Miss => MapMiss(battleEvent.Weapon),
            _ => null,
        };

    /// <summary>
    /// Routes one attack to the slot its resolution deserves. A blow the
    /// defender's shield took sounds like the attacking weapon striking a
    /// board, not like the weapon reaching a body, so
    /// <see cref="AttackResolution.ShieldBlocked"/> takes the weapon's clash
    /// slot. A ranged weapon that is <see cref="AttackResolution.Evaded"/>
    /// takes its <c>miss-</c> slot, because a loosed arrow or bolt that finds
    /// no target sounds like a shot spending itself in the air, not like the
    /// weapon reaching a body — this is the fix this task exists for. A melee
    /// weapon keeps the pre-existing, shared-cue behaviour for every other
    /// resolution, including <see cref="AttackResolution.Evaded"/>:
    /// <see cref="AttackResolution.Landed"/>,
    /// <see cref="AttackResolution.Parried"/>,
    /// <see cref="AttackResolution.Deflected"/>, and
    /// <see cref="AttackResolution.Evaded"/> still share one cue for a melee
    /// weapon. See <c>SIMULATION-GAME-STANDARDS.md</c> section 14 for the
    /// recorded scope of that difference.
    /// </summary>
    private static GameSoundId? MapAttack(
        WeaponId? weapon,
        AttackResolution? resolution)
    {
        if (resolution == AttackResolution.ShieldBlocked)
        {
            return MapShieldClash(weapon);
        }

        if (resolution == AttackResolution.Evaded && IsRanged(weapon))
        {
            return MapMiss(weapon);
        }

        return MapWeapon(weapon);
    }

    /// <summary>
    /// True for the three ranged weapons this package adds. Used only to
    /// decide whether an <see cref="AttackResolution.Evaded"/> resolution
    /// should divert to <see cref="MapMiss"/> instead of the shared melee
    /// impact cue.
    /// </summary>
    private static bool IsRanged(WeaponId? weapon) =>
        weapon is WeaponId.Bangkaw or WeaponId.Busog or WeaponId.Arquebus;

    /// <summary>
    /// A weapon with no mapping stays silent rather than throwing, because a
    /// missing sound must never interrupt a battle. The catalog test asserts
    /// every defined <see cref="WeaponId"/> has a slot, so a newly added weapon
    /// fails a test instead of failing silently forever.
    /// </summary>
    private static GameSoundId? MapWeapon(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => GameSoundId.AttackKampilan,
            WeaponId.Wasay => GameSoundId.AttackWasay,
            WeaponId.Kalis => GameSoundId.AttackKalis,
            WeaponId.Itak => GameSoundId.AttackItak,
            WeaponId.Bangkaw => GameSoundId.AttackBangkaw,
            WeaponId.Busog => GameSoundId.AttackBusog,
            WeaponId.Arquebus => GameSoundId.AttackArquebus,
            _ => null,
        };

    /// <summary>
    /// Each weapon owns its clash slot, so nothing substitutes across weapons:
    /// a clash slot with no file resolves <c>Missing</c> and is silent, which
    /// is visible in the cue log, rather than borrowing another weapon's take,
    /// which would be invisible everywhere. An unmapped weapon stays silent
    /// here for the same reason it does in <see cref="MapWeapon"/>, and
    /// <c>SoundCatalogTests.EveryDefinedWeapon_HasAShieldClashSlot</c> is what
    /// fails when a weapon arrives without one.
    /// </summary>
    private static GameSoundId? MapShieldClash(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => GameSoundId.ClashShieldKampilan,
            WeaponId.Wasay => GameSoundId.ClashShieldWasay,
            WeaponId.Kalis => GameSoundId.ClashShieldKalis,
            WeaponId.Itak => GameSoundId.ClashShieldItak,
            WeaponId.Bangkaw => GameSoundId.ClashShieldBangkaw,
            WeaponId.Busog => GameSoundId.ClashShieldBusog,
            WeaponId.Arquebus => GameSoundId.ClashShieldArquebus,
            _ => null,
        };

    /// <summary>
    /// The slot for a shot that left its launcher. Keyed on the weapon
    /// directly rather than derived only from a <see cref="BattleEvent"/>,
    /// because <see cref="BattleEventKind.Release"/> is a non-attack event
    /// whose <see cref="BattleEvent.Weapon"/> is always <c>null</c> by
    /// construction; a future caller that resolves the launching weapon from
    /// elsewhere (for example the source agent's loadout) can call this
    /// directly with that resolved weapon. Only the three ranged weapons map;
    /// every melee weapon stays silent here.
    /// </summary>
    internal static GameSoundId? MapRelease(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Bangkaw => GameSoundId.ReleaseBangkaw,
            WeaponId.Busog => GameSoundId.ReleaseBusog,
            WeaponId.Arquebus => GameSoundId.ReleaseArquebus,
            _ => null,
        };

    /// <summary>
    /// The slot for a ranged shot that spent itself without landing, whether
    /// it arrives as a <see cref="BattleEventKind.Miss"/> event or as an
    /// <see cref="AttackResolution.Evaded"/> resolution on a ranged weapon's
    /// attack. Only the three ranged weapons map; a melee weapon has no miss
    /// slot and keeps its shared impact cue for <c>Evaded</c> instead, which
    /// <see cref="MapAttack"/> enforces by only reaching this method for a
    /// ranged weapon.
    /// </summary>
    private static GameSoundId? MapMiss(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Bangkaw => GameSoundId.MissBangkaw,
            WeaponId.Busog => GameSoundId.MissBusog,
            WeaponId.Arquebus => GameSoundId.MissArquebus,
            _ => null,
        };

    private static GameSoundId MapOutcome(int? factionId) =>
        factionId switch
        {
            0 => GameSoundId.VictoryBlue,
            1 => GameSoundId.VictoryRed,
            _ => GameSoundId.Draw,
        };
}
