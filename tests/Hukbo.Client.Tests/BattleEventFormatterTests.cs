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

        var formatted = BattleEventFormatter.Format(battleEvent, scenarioSeed: 1);

        // The actor carries its warrior name; the target stays a bare
        // identifier because the event records no target faction, and the
        // faction is what selects the regional name corpus.
        Assert.Equal(
            $"T00042  Blue {WarriorNames.FormatWarrior(7, 0, 1)} " +
            "hit #12's shoulder with Kampilan — Great Blade for 10",
            formatted);
    }

    [Fact]
    public void GetRowActorLabel_DropsTheFactionWordForTheNarrowRowColumn()
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 1,
            tick: 42,
            BattleEventKind.Death,
            sourceEntityId: 7,
            targetEntityId: null,
            value: 0,
            factionId: 0);

        Assert.Equal(
            $"Blue {WarriorNames.FormatWarrior(7, 0, 1)}",
            BattleEventFormatter.GetActorLabel(battleEvent, scenarioSeed: 1));
        Assert.Equal(
            WarriorNames.FormatWarrior(7, 0, 1),
            BattleEventFormatter.GetRowActorLabel(battleEvent, scenarioSeed: 1));
    }

    [Fact]
    public void ActorLabels_NameTheBattleItselfForAnOutcomeEvent()
    {
        var outcome = BattleEvent.NonAttack(
            sequence: 1,
            tick: 42,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: 0,
            factionId: 0);

        Assert.Equal(
            "Battle",
            BattleEventFormatter.GetActorLabel(outcome, scenarioSeed: 1));
        Assert.Equal(
            "Battle",
            BattleEventFormatter.GetRowActorLabel(outcome, scenarioSeed: 1));
    }

    /// <summary>
    /// The row column holds roughly fifteen characters, so the row label has
    /// to stay inside that budget for every shipped name at a realistic
    /// roster's entity identifiers — otherwise a name would be drawn
    /// truncated mid-word.
    /// </summary>
    [Fact]
    public void GetRowActorLabel_FitsTheRowColumnForEveryWarriorInALargeRoster()
    {
        for (ulong entityId = 1; entityId <= 500; entityId++)
        {
            foreach (var factionId in new[] { 0, 1 })
            {
                var battleEvent = BattleEvent.NonAttack(
                    sequence: (long)entityId,
                    tick: 1,
                    BattleEventKind.Death,
                    entityId,
                    targetEntityId: null,
                    value: 0,
                    factionId);
                var label = BattleEventFormatter.GetRowActorLabel(
                    battleEvent,
                    scenarioSeed: 1);

                Assert.True(
                    label.Length <= 15,
                    $"Row label '{label}' is {label.Length} characters.");
            }
        }
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

    /// <summary>
    /// RED. Without a line of its own per resolution no spectator can tell a
    /// parry from a block from a landed blow, which is the discoverability
    /// question the repository requires every feature to answer, and the event
    /// log is the only channel that names a void at all.
    /// </summary>
    [Fact]
    public void GetActionLabel_ProducesADistinctLinePerResolution()
    {
        var labels = new List<string>();
        var expectedWeaponLabel = BattleEventFormatter.GetWeaponLabel(
            WeaponId.Kampilan,
            ShieldId.None);

        foreach (var resolution in Enum.GetValues<AttackResolution>())
        {
            var battleEvent = BattleEvent.Attack(
                sequence: 1,
                tick: 42,
                sourceEntityId: 7,
                targetEntityId: 12,
                damage: resolution == AttackResolution.Landed ? 10 : 0,
                factionId: 0,
                WeaponId.Kampilan,
                ShieldId.None,
                BodyPart.Shoulder,
                resolution);

            var actionLabel = BattleEventFormatter.GetActionLabel(battleEvent);

            // Every resolution line carries the same pair-form weapon label —
            // the resolution changes what happened, not what the warrior was
            // holding. A bare English label would violate the historical
            // accuracy policy in CLAUDE.md section 7.
            Assert.Contains(expectedWeaponLabel, actionLabel);
            Assert.DoesNotContain("for 0", actionLabel);
            Assert.DoesNotContain("0 damage", actionLabel);
            if (resolution != AttackResolution.Landed)
            {
                Assert.DoesNotContain(" for ", actionLabel);
            }

            labels.Add(actionLabel);
        }

        Assert.Equal(5, labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(AttackResolution.Landed)]
    [InlineData(AttackResolution.ShieldBlocked)]
    [InlineData(AttackResolution.Parried)]
    [InlineData(AttackResolution.Deflected)]
    [InlineData(AttackResolution.Evaded)]
    public void GetActionLabel_CarriesThePairFormLabelAndGripSuffixForEveryResolution(
        AttackResolution resolution)
    {
        var shieldedEvent = BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: resolution == AttackResolution.Landed ? 5 : 0,
            factionId: 0,
            WeaponId.Kalis,
            ShieldId.TallHardwood,
            BodyPart.Chest,
            resolution);

        var soloEvent = BattleEvent.Attack(
            sequence: 2,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: resolution == AttackResolution.Landed ? 5 : 0,
            factionId: 0,
            WeaponId.Kalis,
            ShieldId.None,
            BodyPart.Chest,
            resolution);

        var shieldedLabel = BattleEventFormatter.GetActionLabel(shieldedEvent);
        var soloLabel = BattleEventFormatter.GetActionLabel(soloEvent);

        Assert.Contains("Kalis — Thrusting Blade", shieldedLabel);
        Assert.Contains("(shielded)", shieldedLabel);
        Assert.Contains("Kalis — Thrusting Blade", soloLabel);
        Assert.Contains("(solo)", soloLabel);
    }

    /// <summary>
    /// RED. The detail block reports a value beside the tick, and a blow the
    /// shield turned aside carries a value of zero, so without this the panel
    /// reads "Value: 0" and invites a spectator to read a landed blow that
    /// happened to do nothing.
    /// </summary>
    [Fact]
    public void Details_OmitsTheDamageLineForANonLandedAttack()
    {
        AttackResolution[] nonLanded =
        [
            AttackResolution.ShieldBlocked,
            AttackResolution.Parried,
            AttackResolution.Deflected,
            AttackResolution.Evaded,
        ];

        foreach (var resolution in nonLanded)
        {
            var battleEvent = BattleEvent.Attack(
                sequence: 1,
                tick: 42,
                sourceEntityId: 7,
                targetEntityId: 12,
                damage: 0,
                factionId: 0,
                WeaponId.Kampilan,
                ShieldId.None,
                BodyPart.Shoulder,
                resolution);

            var summary = BattleEventFormatter.GetDetailSummaryLine(battleEvent);

            Assert.Equal("Tick: 42", summary);
            Assert.DoesNotContain("Value", summary);
        }

        var landed = BattleEvent.Attack(
            sequence: 2,
            tick: 42,
            sourceEntityId: 7,
            targetEntityId: 12,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Shoulder,
            AttackResolution.Landed);
        var damage = BattleEvent.NonAttack(
            sequence: 3,
            tick: 42,
            BattleEventKind.Damage,
            sourceEntityId: 12,
            targetEntityId: 12,
            value: 10,
            factionId: null);

        Assert.Equal(
            "Tick: 42    Value: 10",
            BattleEventFormatter.GetDetailSummaryLine(landed));
        Assert.Equal(
            "Tick: 42    Value: 10",
            BattleEventFormatter.GetDetailSummaryLine(damage));
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
