using Hukbo.Client.Audio;
using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class SoundCueMapperTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, (int)GameSoundId.Death)]
    public void MapContact_ReturnsWeaponAndOptionalOwnedDeathCue(
        bool isLethal,
        int? expectedLethal)
    {
        var request = SoundCueMapper.MapContact(
            new AttackContactBundle(
                Sequence: 1,
                Tick: 12,
                AttackerEntityId: 3,
                DefenderEntityId: 4,
                Damage: 7,
                FactionId: 0,
                WeaponId.Kampilan,
                AttackerShield: ShieldId.None,
                HitLocation: BodyPart.Chest,
                Resolution: AttackResolution.Landed,
                ComboPosition: null,
                isLethal));

        Assert.Equal(GameSoundId.AttackKampilan, request.Contact);
        Assert.Equal(
            expectedLethal is { } sound ? (GameSoundId)sound : null,
            request.Lethal);
    }

    // Expected slots are ints because xunit requires public test methods and
    // GameSoundId is internal to Hukbo.Client.
    [Theory]
    [InlineData(WeaponId.Kampilan, (int)GameSoundId.AttackKampilan)]
    [InlineData(WeaponId.Wasay, (int)GameSoundId.AttackWasay)]
    [InlineData(
        WeaponId.Kalis,
        (int)GameSoundId.AttackKalis)]
    [InlineData(WeaponId.Itak, (int)GameSoundId.AttackItak)]
    [InlineData(WeaponId.Bangkaw, (int)GameSoundId.AttackBangkaw)]
    [InlineData(WeaponId.Busog, (int)GameSoundId.AttackBusog)]
    [InlineData(WeaponId.Arquebus, (int)GameSoundId.AttackArquebus)]
    public void Map_ReturnsTheWeaponSlotForAnAttack(
        WeaponId weapon,
        int expected) =>
        Assert.Equal(
            (GameSoundId)expected,
            SoundCueMapper.Map(
                BattleEvent.Attack(
                    sequence: 1,
                    tick: 12,
                    sourceEntityId: 3,
                    targetEntityId: 4,
                    damage: 7,
                    factionId: 0,
                    weapon,
                    ShieldId.None,
                    BodyPart.Chest)));

    [Theory]
    [InlineData(WeaponId.Kampilan, (int)GameSoundId.ClashShieldKampilan)]
    [InlineData(WeaponId.Wasay, (int)GameSoundId.ClashShieldWasay)]
    [InlineData(WeaponId.Kalis, (int)GameSoundId.ClashShieldKalis)]
    [InlineData(WeaponId.Itak, (int)GameSoundId.ClashShieldItak)]
    [InlineData(WeaponId.Bangkaw, (int)GameSoundId.ClashShieldBangkaw)]
    [InlineData(WeaponId.Busog, (int)GameSoundId.ClashShieldBusog)]
    [InlineData(WeaponId.Arquebus, (int)GameSoundId.ClashShieldArquebus)]
    public void Map_RoutesAShieldBlockToTheMatchingClashSlot(
        WeaponId weapon,
        int expected) =>
        Assert.Equal(
            (GameSoundId)expected,
            SoundCueMapper.Map(
                AttackWith(weapon, AttackResolution.ShieldBlocked)));

    [Theory]
    [InlineData(AttackResolution.Landed)]
    [InlineData(AttackResolution.Parried)]
    [InlineData(AttackResolution.Deflected)]
    [InlineData(AttackResolution.Evaded)]
    public void Map_KeepsTheWeaponSlotForEveryOtherResolution(
        AttackResolution resolution) =>
        Assert.Equal(
            GameSoundId.AttackKampilan,
            SoundCueMapper.Map(AttackWith(WeaponId.Kampilan, resolution)));

    // The Evaded fix: a ranged weapon's missed shot sounds like a shot
    // spending itself in the air, not like the weapon reaching a body, so it
    // diverts to the weapon's miss- slot rather than sharing the impact cue
    // the melee theory above pins. Added beside that theory, not in place of
    // it.
    [Theory]
    [InlineData(WeaponId.Bangkaw, (int)GameSoundId.MissBangkaw)]
    [InlineData(WeaponId.Busog, (int)GameSoundId.MissBusog)]
    [InlineData(WeaponId.Arquebus, (int)GameSoundId.MissArquebus)]
    public void Map_RoutesAnEvadedRangedAttackToTheWeaponsMissSlot(
        WeaponId weapon,
        int expected) =>
        Assert.Equal(
            (GameSoundId)expected,
            SoundCueMapper.Map(
                AttackWith(weapon, AttackResolution.Evaded)));

    [Theory]
    [InlineData(WeaponId.Bangkaw, (int)GameSoundId.ReleaseBangkaw)]
    [InlineData(WeaponId.Busog, (int)GameSoundId.ReleaseBusog)]
    [InlineData(WeaponId.Arquebus, (int)GameSoundId.ReleaseArquebus)]
    public void MapRelease_ReturnsTheWeaponsReleaseSlot(
        WeaponId weapon,
        int expected) =>
        Assert.Equal((GameSoundId)expected, SoundCueMapper.MapRelease(weapon));

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void MapRelease_IsSilentForAMeleeWeapon(WeaponId weapon) =>
        Assert.Null(SoundCueMapper.MapRelease(weapon));

    [Fact]
    public void Map_LeavesAReleaseEventSilentBecauseItCarriesNoWeaponYet() =>
        // BattleEventKind.Release is a non-attack event, so its Weapon is
        // always null by construction; the launching weapon can only be
        // resolved by a future caller with the source agent's loadout, which
        // is why MapRelease is exposed for that caller to use directly.
        Assert.Null(
            SoundCueMapper.Map(NonAttack(BattleEventKind.Release, factionId: 0)));

    [Fact]
    public void Map_LeavesAMissEventSilentBecauseItCarriesNoWeaponYet() =>
        Assert.Null(
            SoundCueMapper.Map(NonAttack(BattleEventKind.Miss, factionId: 0)));

    [Fact]
    public void Map_ReturnsTheDeathSlotForADeath() =>
        Assert.Equal(
            GameSoundId.Death,
            SoundCueMapper.Map(
                NonAttack(BattleEventKind.Death, factionId: 1)));

    [Theory]
    [InlineData(0, (int)GameSoundId.VictoryBlue)]
    [InlineData(1, (int)GameSoundId.VictoryRed)]
    [InlineData(2, (int)GameSoundId.Draw)]
    [InlineData(null, (int)GameSoundId.Draw)]
    public void Map_ReturnsTheOutcomeSlotForAnOutcome(
        int? factionId,
        int expected) =>
        Assert.Equal(
            (GameSoundId)expected,
            SoundCueMapper.Map(
                NonAttack(BattleEventKind.Outcome, factionId)));

    [Theory]
    [InlineData(BattleEventKind.Move)]
    [InlineData(BattleEventKind.Damage)]
    public void Map_LeavesMovementAndDamageSilent(BattleEventKind kind) =>
        Assert.Null(SoundCueMapper.Map(NonAttack(kind, factionId: 0)));

    private static BattleEvent AttackWith(
        WeaponId weapon,
        AttackResolution resolution) =>
        BattleEvent.Attack(
            sequence: 1,
            tick: 12,
            sourceEntityId: 3,
            targetEntityId: 4,
            damage: 7,
            factionId: 0,
            weapon,
            ShieldId.None,
            BodyPart.Chest,
            resolution);

    private static BattleEvent NonAttack(
        BattleEventKind kind,
        int? factionId) =>
        BattleEvent.NonAttack(
            sequence: 1,
            tick: 12,
            kind,
            sourceEntityId: 3,
            targetEntityId: 4,
            value: 7,
            factionId);
}
