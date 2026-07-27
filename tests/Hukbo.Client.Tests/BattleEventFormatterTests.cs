using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class BattleEventFormatterTests
{
    [Fact]
    public void Format_AttackEventRendersWeaponAndHitLocation()
    {
        var battleEvent = BattleEvent.Attack(
            sequence: 1,
            tick: 42,
            sourceEntityId: 7,
            targetEntityId: 12,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Shoulder);

        var formatted = BattleEventFormatter.Format(battleEvent);

        Assert.Equal(
            "T00042  Blue #7 hit #12's shoulder with " +
            "Kampilan — Great Blade for 10",
            formatted);
    }

    [Theory]
    [InlineData(BodyPart.WeaponArm, "weapon arm")]
    [InlineData(BodyPart.ShieldArm, "shield arm")]
    [InlineData(BodyPart.Shoulder, "shoulder")]
    [InlineData(BodyPart.Head, "head")]
    [InlineData(BodyPart.Neck, "neck")]
    [InlineData(BodyPart.Face, "face")]
    [InlineData(BodyPart.Chest, "chest")]
    [InlineData(BodyPart.Abdomen, "abdomen")]
    [InlineData(BodyPart.Thigh, "thigh")]
    [InlineData(BodyPart.Knee, "knee")]
    [InlineData(BodyPart.Shin, "shin")]
    [InlineData(BodyPart.Hands, "hands")]
    [InlineData(BodyPart.Feet, "feet")]
    public void GetActionLabel_UsesLowercaseBodyPartWording(
        BodyPart bodyPart,
        string expectedWording)
    {
        var battleEvent = BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 5,
            factionId: 0,
            WeaponId.Itak,
            ShieldId.None,
            bodyPart);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.Contains(expectedWording, actionLabel);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan, "Kampilan — Great Blade")]
    [InlineData(WeaponId.Wasay, "Wasay — War Axe")]
    [InlineData(WeaponId.Kalis, "Kalis — Thrusting Blade")]
    [InlineData(WeaponId.Itak, "Itak — Work Blade")]
    public void GetActionLabel_UsesTheApprovedPairFormWeaponLabels(
        WeaponId weapon,
        string expectedLabel)
    {
        var battleEvent = BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 5,
            factionId: 0,
            weapon,
            ShieldId.None,
            BodyPart.Chest);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.Contains(expectedLabel, actionLabel);
    }

    [Theory]
    // A one-handed weapon deals different damage solo than shielded, so the
    // feed has to say which: the same bare label would otherwise mean either
    // value. A two-handed weapon appends nothing, having no second form.
    [InlineData(WeaponId.Kalis, ShieldId.None, "(solo)")]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood, "(shielded)")]
    [InlineData(WeaponId.Itak, ShieldId.None, "(solo)")]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood, "(shielded)")]
    public void GetActionLabel_AppendsTheGripForAOneHandedWeapon(
        WeaponId weapon,
        ShieldId shield,
        string expectedSuffix)
    {
        var battleEvent = BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 5,
            factionId: 0,
            weapon,
            shield,
            BodyPart.Chest);

        Assert.Contains(
            expectedSuffix,
            BattleEventFormatter.GetActionLabel(battleEvent));
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    public void GetActionLabel_AppendsNoGripForATwoHandedWeapon(
        WeaponId weapon)
    {
        var battleEvent = BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 5,
            factionId: 0,
            weapon,
            ShieldId.None,
            BodyPart.Chest);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.DoesNotContain("(solo)", actionLabel);
        Assert.DoesNotContain("(shielded)", actionLabel);
    }

    [Fact]
    public void GetActionLabel_MoveEventFormattingIsUnchanged()
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Move,
            sourceEntityId: 5,
            targetEntityId: 9,
            value: 0,
            factionId: 0);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.Equal("moved toward #9", actionLabel);
    }

    [Fact]
    public void GetActionLabel_DamageEventFormattingIsUnchanged()
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Damage,
            sourceEntityId: 9,
            targetEntityId: null,
            value: 10,
            factionId: 0);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.Equal("took 10 damage", actionLabel);
    }

    [Fact]
    public void GetActionLabel_DeathEventFormattingIsUnchanged()
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Death,
            sourceEntityId: 9,
            targetEntityId: null,
            value: 0,
            factionId: 0);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.Equal("died", actionLabel);
    }

    [Fact]
    public void GetActionLabel_OutcomeEventFormattingIsUnchanged()
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: 0,
            factionId: 1);

        var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

        Assert.Equal("Red wins", actionLabel);
    }
}
