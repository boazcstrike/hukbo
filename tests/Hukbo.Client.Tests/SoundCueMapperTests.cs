using Hukbo.Client.Audio;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class SoundCueMapperTests
{
    // Expected slots are ints because xunit requires public test methods and
    // GameSoundId is internal to Hukbo.Client.
    [Theory]
    [InlineData(WeaponId.Kampilan, (int)GameSoundId.AttackGreatBlade)]
    [InlineData(WeaponId.Wasay, (int)GameSoundId.AttackWarAxe)]
    [InlineData(
        WeaponId.Kalis,
        (int)GameSoundId.AttackThrustingBlade)]
    [InlineData(WeaponId.Itak, (int)GameSoundId.AttackWorkBlade)]
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
