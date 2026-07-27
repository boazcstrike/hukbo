using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class BattleEventTests
{
    [Fact]
    public void CombatContextEnumsFitTheBytesTheyArePackedInto()
    {
        // BattleEvent packs Weapon, Shield, and HitLocation into one int, a
        // byte apiece. Every one of the three must start numbering at one so
        // that a zero weapon field is an unambiguous "absent" marker, and
        // none may exceed 255. An enum value added outside that range would
        // otherwise silently alias another value inside a packed event.
        AssertPackable(Enum.GetValues<WeaponId>().Select(id => (int)id));
        AssertPackable(Enum.GetValues<ShieldId>().Select(id => (int)id));
        AssertPackable(Enum.GetValues<BodyPart>().Select(part => (int)part));

        static void AssertPackable(IEnumerable<int> values)
        {
            foreach (var value in values)
            {
                Assert.InRange(value, 1, 255);
            }
        }
    }

    [Fact]
    public void AttackRoundTripsEveryCombinationOfPackedCombatContext()
    {
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            foreach (var shield in Enum.GetValues<ShieldId>())
            {
                foreach (var hitLocation in BodyPartCatalog.Ordered)
                {
                    var attack = BattleEvent.Attack(
                        sequence: 1,
                        tick: 1,
                        sourceEntityId: 1,
                        targetEntityId: 2,
                        damage: 1,
                        factionId: 0,
                        weapon,
                        shield,
                        hitLocation);

                    Assert.Equal(weapon, attack.Weapon);
                    Assert.Equal(shield, attack.Shield);
                    Assert.Equal(hitLocation, attack.HitLocation);
                }
            }
        }
    }

    [Fact]
    public void Attack_PopulatesWeaponAndHitLocation()
    {
        var attack = BattleEvent.Attack(
            sequence: 1,
            tick: 4,
            sourceEntityId: 10,
            targetEntityId: 11,
            damage: 7,
            factionId: 0,
            WeaponId.Itak,
            ShieldId.None,
            BodyPart.WeaponArm);

        Assert.Equal(1, attack.Sequence);
        Assert.Equal(4, attack.Tick);
        Assert.Equal(BattleEventKind.Attack, attack.Kind);
        Assert.Equal(10UL, attack.SourceEntityId);
        Assert.Equal(11UL, attack.TargetEntityId);
        Assert.Equal(7, attack.Value);
        Assert.Equal(0, attack.FactionId);
        Assert.Equal(WeaponId.Itak, attack.Weapon);
        Assert.Equal(BodyPart.WeaponArm, attack.HitLocation);
    }

    [Fact]
    public void Attack_DefaultsAnUnsuppliedResolutionToLanded()
    {
        // The parameter is optional so that the twenty existing call sites
        // across eleven files keep compiling. Production code never relies on
        // this default: BattleSimulation.AddAttackEvent takes the resolution
        // as a required parameter.
        var attack = BattleEvent.Attack(
            sequence: 1,
            tick: 4,
            sourceEntityId: 10,
            targetEntityId: 11,
            damage: 7,
            factionId: 0,
            WeaponId.Bolo,
            BodyPart.WeaponArm);

        Assert.Equal(AttackResolution.Landed, attack.Resolution);
    }

    [Theory]
    [InlineData(AttackResolution.Landed)]
    [InlineData(AttackResolution.ShieldBlocked)]
    [InlineData(AttackResolution.Parried)]
    [InlineData(AttackResolution.Deflected)]
    [InlineData(AttackResolution.Evaded)]
    public void Attack_CarriesTheSuppliedResolution(AttackResolution resolution)
    {
        var attack = BattleEvent.Attack(
            sequence: 1,
            tick: 4,
            sourceEntityId: 10,
            targetEntityId: 11,
            damage: 0,
            factionId: 0,
            WeaponId.Bolo,
            BodyPart.WeaponArm,
            resolution);

        Assert.Equal(resolution, attack.Resolution);
    }

    [Fact]
    public void Attack_RejectsAnUndefinedResolution()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 1,
            factionId: 0,
            WeaponId.GreatBlade,
            BodyPart.Head,
            (AttackResolution)999));
    }

    [Fact]
    public void Attack_RejectsZeroTarget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 0,
            damage: 1,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Head));
    }

    [Fact]
    public void Attack_RejectsUndefinedWeapon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 1,
            factionId: 0,
            (WeaponId)999,
            ShieldId.None,
            BodyPart.Head));
    }

    [Fact]
    public void Attack_RejectsUndefinedHitLocation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEvent.Attack(
            sequence: 1,
            tick: 1,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 1,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            (BodyPart)999));
    }

    [Fact]
    public void NonAttack_PopulatesFieldsAndLeavesCombatContextNull()
    {
        var move = BattleEvent.NonAttack(
            sequence: 2,
            tick: 5,
            BattleEventKind.Move,
            sourceEntityId: 3,
            targetEntityId: 4,
            value: 12,
            factionId: 1);

        Assert.Equal(BattleEventKind.Move, move.Kind);
        Assert.Equal(3UL, move.SourceEntityId);
        Assert.Equal(4UL, move.TargetEntityId);
        Assert.Equal(12, move.Value);
        Assert.Equal(1, move.FactionId);
        Assert.Null(move.Weapon);
        Assert.Null(move.HitLocation);
    }

    [Theory]
    [InlineData(BattleEventKind.Move)]
    [InlineData(BattleEventKind.Damage)]
    [InlineData(BattleEventKind.Death)]
    [InlineData(BattleEventKind.Outcome)]
    public void NonAttack_LeavesTheResolutionNull(BattleEventKind kind)
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 2,
            tick: 5,
            kind,
            sourceEntityId: 3,
            targetEntityId: 4,
            value: 12,
            factionId: 1);

        Assert.Null(battleEvent.Resolution);
    }

    [Theory]
    [InlineData(BattleEventKind.Move)]
    [InlineData(BattleEventKind.Damage)]
    [InlineData(BattleEventKind.Death)]
    [InlineData(BattleEventKind.Outcome)]
    public void NonAttack_AllowsEveryNonAttackKind(BattleEventKind kind)
    {
        var battleEvent = BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            kind,
            sourceEntityId: 1,
            targetEntityId: null,
            value: 0,
            factionId: null);

        Assert.Equal(kind, battleEvent.Kind);
    }

    [Fact]
    public void NonAttack_RejectsAttackKind()
    {
        Assert.Throws<ArgumentException>(() => BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Attack,
            sourceEntityId: 1,
            targetEntityId: 2,
            value: 1,
            factionId: 0));
    }

    [Fact]
    public void DefaultBattleEvent_IsAnUnvalidatedRecordStructArtifact()
    {
        // C# always synthesizes a public parameterless constructor for a
        // record struct, even with a declared private constructor. This
        // default value bypasses BattleEvent.Attack/NonAttack validation
        // and must never be produced by simulation or presentation code.
        var defaultEvent = default(BattleEvent);

        Assert.Equal(BattleEventKind.Move, defaultEvent.Kind);
        Assert.Null(defaultEvent.Weapon);
        Assert.Null(defaultEvent.HitLocation);
        Assert.Null(defaultEvent.Resolution);
    }
}
