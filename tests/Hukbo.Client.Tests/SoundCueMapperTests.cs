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
