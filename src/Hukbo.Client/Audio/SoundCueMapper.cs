using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Audio;

/// <summary>
/// The sounds released atomically with one contact. A lethal landed bundle
/// keeps its weapon cue and owns the single Death cue attached by dispatch.
/// </summary>
internal readonly record struct ContactSoundRequest(
    GameSoundId? Contact,
    GameSoundId? Lethal);

/// <summary>
/// Translates one authoritative <see cref="BattleEvent"/> into the sound slot
/// it should trigger, or <c>null</c> for an event that is deliberately silent.
/// Read-only over the event stream: nothing here can affect simulation state.
/// </summary>
internal static class SoundCueMapper
{
    public static ContactSoundRequest MapContact(AttackContactBundle contact) =>
        new(
            MapAttack(contact.Weapon, contact.Resolution),
            contact.IsLethal ? GameSoundId.Death : null);

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
            _ => null,
        };

    /// <summary>
    /// Routes one attack to the slot its resolution deserves. A blow the
    /// defender's shield took sounds like the attacking weapon striking a
    /// board, not like the weapon reaching a body, so
    /// <see cref="AttackResolution.ShieldBlocked"/> takes the weapon's clash
    /// slot. Every other resolution keeps the weapon's impact slot, which is
    /// the pre-existing behaviour: <see cref="AttackResolution.Landed"/>,
    /// <see cref="AttackResolution.Parried"/>,
    /// <see cref="AttackResolution.Deflected"/>, and
    /// <see cref="AttackResolution.Evaded"/> still share one cue.
    /// </summary>
    private static GameSoundId? MapAttack(
        WeaponId? weapon,
        AttackResolution? resolution) =>
        resolution == AttackResolution.ShieldBlocked
            ? MapShieldClash(weapon)
            : MapWeapon(weapon);

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
