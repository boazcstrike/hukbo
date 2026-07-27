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
            BattleEventKind.Attack => MapWeapon(battleEvent.Weapon),
            BattleEventKind.Death => GameSoundId.Death,
            BattleEventKind.Outcome => MapOutcome(battleEvent.FactionId),
            _ => null,
        };

    /// <summary>
    /// A weapon with no mapping stays silent rather than throwing, because a
    /// missing sound must never interrupt a battle. The catalog test asserts
    /// every defined <see cref="WeaponId"/> has a slot, so a newly added weapon
    /// fails a test instead of failing silently forever.
    /// </summary>
    private static GameSoundId? MapWeapon(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => GameSoundId.AttackGreatBlade,
            WeaponId.Wasay => GameSoundId.AttackWarAxe,
            WeaponId.Kalis => GameSoundId.AttackThrustingBlade,
            WeaponId.Itak => GameSoundId.AttackWorkBlade,
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
