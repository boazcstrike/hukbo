using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class BloodEffectSystemTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacities()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloodEffectSystem(burstCapacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloodEffectSystem(burstCapacity: -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloodEffectSystem(groundMarkCapacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloodEffectSystem(spurtCapacity: -3));
    }

    [Fact]
    public void Intensity_RejectsUndefinedValues()
    {
        var system = new BloodEffectSystem();

        Assert.Equal(GoreIntensity.Stylized, system.Intensity);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Intensity = (GoreIntensity)99);
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var system = new BloodEffectSystem();

        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(null!, Array.Empty<AgentView>()));
        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(Array.Empty<BattleEvent>(), null!));
    }

    [Fact]
    public void Ingest_CreatesOneBurstAndOneMarkPerAttackEventOnly()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];

        system.Ingest(
        [
            AttackEvent(1, source: 2, target: 7, damage: 25),
            DamageEvent(2, target: 7, damage: 25),
            NonAttackEvent(3, BattleEventKind.Move, source: 7),
            NonAttackEvent(4, BattleEventKind.Outcome, source: 0),
        ], agents);

        var burst = Assert.Single(system.ActiveBursts.ToArray());
        var mark = Assert.Single(system.ActiveGroundMarks.ToArray());
        Assert.Empty(system.ActiveSpurts.ToArray());
        Assert.Equal(1, burst.Sequence);
        Assert.Equal(2UL, burst.SourceEntityId);
        Assert.Equal(7UL, burst.TargetEntityId);
        Assert.Equal((300, 0), (burst.XRaw, burst.YRaw));
        Assert.Equal(1, mark.Sequence);
        Assert.Equal((300, 0), (mark.XRaw, mark.YRaw));
    }

    [Fact]
    public void Ingest_DerivesDirectionFromAttackerTowardVictim()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(2, 100, 100), Agent(7, 100, 400)];

        system.Ingest([AttackEvent(1, source: 2, target: 7)], agents);

        var burst = Assert.Single(system.ActiveBursts.ToArray());
        Assert.Equal(0f, burst.DirectionX, precision: 5);
        Assert.Equal(1f, burst.DirectionY, precision: 5);
    }

    [Fact]
    public void Ingest_LeavesDirectionUnsetWhenTheAttackerCannotBeResolved()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(7, 100, 400)];

        system.Ingest([AttackEvent(1, source: 2, target: 7)], agents);

        var burst = Assert.Single(system.ActiveBursts.ToArray());
        Assert.Equal(0f, burst.DirectionX);
        Assert.Equal(0f, burst.DirectionY);
    }

    [Fact]
    public void Ingest_CarriesWeaponHitLocationAndClampedSeverity()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];

        system.Ingest(
        [
            AttackEvent(
                1,
                source: 2,
                target: 7,
                damage: 40,
                weapon: WeaponId.Wasay,
                hitLocation: BodyPart.Neck),
            AttackEvent(2, source: 2, target: 7, damage: 4000),
        ], agents);

        var bursts = system.ActiveBursts.ToArray();
        Assert.Equal(WeaponId.Wasay, bursts[0].Weapon);
        Assert.Equal(BodyPart.Neck, bursts[0].HitLocation);
        Assert.Equal(0.4f, bursts[0].SeverityRatio, precision: 5);
        Assert.Equal(1f, bursts[1].SeverityRatio);
    }

    [Fact]
    public void Ingest_MarksEveryBlowOnAVictimDyingThisTickAsLethal()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents =
        [
            Agent(5, 0, 0),
            Agent(6, 600, 0),
            Agent(7, 300, 0, isAlive: false),
        ];

        system.Ingest(
        [
            AttackEvent(1, source: 5, target: 7, damage: 30),
            AttackEvent(2, source: 6, target: 7, damage: 30),
            DamageEvent(3, target: 7, damage: 60),
            NonAttackEvent(4, BattleEventKind.Death, source: 7),
        ], agents);

        var bursts = system.ActiveBursts.ToArray();
        Assert.Equal(2, bursts.Length);
        Assert.All(bursts, burst => Assert.True(burst.IsLethal));
        Assert.Equal([5UL, 6UL], bursts.Select(x => x.SourceEntityId));
        Assert.Equal([1f, -1f], bursts.Select(x => x.DirectionX));
        Assert.Equal(2, system.ActiveGroundMarks.Length);
        Assert.All(
            system.ActiveGroundMarks.ToArray(),
            mark => Assert.True(mark.IsLethal));
    }

    [Fact]
    public void Ingest_IgnoresAttacksWithoutAResolvableVictim()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(2, 0, 0)];

        system.Ingest([AttackEvent(1, source: 2, target: 7)], agents);

        Assert.Empty(system.ActiveBursts.ToArray());
        Assert.Empty(system.ActiveGroundMarks.ToArray());
    }

    [Fact]
    public void Ingest_WhenFull_ReplacesTheOldestBurstAndMark()
    {
        var system = new BloodEffectSystem(
            burstCapacity: 2,
            groundMarkCapacity: 2);
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];
        system.Ingest([AttackEvent(3, source: 2, target: 7)], agents);
        system.Advance(0.04f);
        system.Ingest([AttackEvent(2, source: 2, target: 7)], agents);
        system.Advance(0.02f);

        system.Ingest([AttackEvent(4, source: 2, target: 7)], agents);

        Assert.Equal(
            [2L, 4L],
            system.ActiveBursts.ToArray().Select(x => x.Sequence).Order());
        Assert.Equal(
            [2L, 4L],
            system.ActiveGroundMarks.ToArray()
                .Select(x => x.Sequence)
                .Order());
    }

    [Fact]
    public void Advance_ExpiresBurstsBeforeTheGroundMarksTheyLeave()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0, isAlive: false)];
        system.Ingest(
        [
            AttackEvent(1, source: 2, target: 7),
            NonAttackEvent(2, BattleEventKind.Death, source: 7),
        ], agents);

        system.Advance(0.7f);

        Assert.Empty(system.ActiveBursts.ToArray());
        Assert.Single(system.ActiveGroundMarks.ToArray());

        // Total elapsed age reaches 8.7s, past the 8s lethal ground-mark
        // lifetime raised 2026-08-13, per the lethal blow legibility
        // design.
        system.Advance(8f);

        Assert.Empty(system.ActiveGroundMarks.ToArray());
    }

    [Fact]
    public void Advance_RejectsNegativeOrNonFiniteElapsedTime()
    {
        var system = new BloodEffectSystem();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Advance(-0.001f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Advance(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Advance(float.PositiveInfinity));
    }

    [Fact]
    public void Clear_EmptiesEveryBuffer()
    {
        var system = new BloodEffectSystem { Intensity = GoreIntensity.Full };
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0, isAlive: false)];
        system.Ingest(
        [
            AttackEvent(1, source: 2, target: 7),
            NonAttackEvent(2, BattleEventKind.Death, source: 7),
        ], agents);

        system.Clear();

        Assert.Empty(system.ActiveBursts.ToArray());
        Assert.Empty(system.ActiveGroundMarks.ToArray());
        Assert.Empty(system.ActiveSpurts.ToArray());
    }

    [Fact]
    public void Off_MakesIngestAndAdvanceNoOpsAndOccupiesNoSlot()
    {
        var system = new BloodEffectSystem { Intensity = GoreIntensity.Off };
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0, isAlive: false)];
        BattleEvent[] events =
        [
            AttackEvent(1, source: 2, target: 7),
            NonAttackEvent(2, BattleEventKind.Death, source: 7),
        ];

        system.Ingest(events, agents);
        system.Advance(0.01f);

        Assert.Empty(system.ActiveBursts.ToArray());
        Assert.Empty(system.ActiveGroundMarks.ToArray());
        Assert.Empty(system.ActiveSpurts.ToArray());
    }

    [Fact]
    public void Off_DiscardsBloodThatWasAlreadyOnScreen()
    {
        var system = new BloodEffectSystem();
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];
        system.Ingest([AttackEvent(1, source: 2, target: 7)], agents);

        system.Intensity = GoreIntensity.Off;

        Assert.Empty(system.ActiveBursts.ToArray());
        Assert.Empty(system.ActiveGroundMarks.ToArray());
    }

    [Fact]
    public void Full_AddsASpurtForLethalBlowsOnly()
    {
        var system = new BloodEffectSystem { Intensity = GoreIntensity.Full };
        AgentView[] agents =
        [
            Agent(2, 0, 0),
            Agent(7, 300, 0, isAlive: false),
            Agent(8, 600, 0),
        ];

        system.Ingest(
        [
            AttackEvent(1, source: 2, target: 7, damage: 30),
            AttackEvent(2, source: 2, target: 8, damage: 30),
            NonAttackEvent(3, BattleEventKind.Death, source: 7),
        ], agents);

        var spurt = Assert.Single(system.ActiveSpurts.ToArray());
        Assert.Equal(1, spurt.Sequence);
        Assert.Equal(7UL, spurt.TargetEntityId);
        Assert.Equal(1f, spurt.DirectionX, precision: 5);
    }

    [Fact]
    public void Full_LeavesDenserLongerLivedGroundMarksThanStylized()
    {
        var stylized = new BloodEffectSystem();
        var full = new BloodEffectSystem { Intensity = GoreIntensity.Full };
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];
        BattleEvent[] events = [AttackEvent(1, source: 2, target: 7)];

        stylized.Ingest(events, agents);
        full.Ingest(events, agents);

        var stylizedMark = Assert.Single(stylized.ActiveGroundMarks.ToArray());
        var fullMark = Assert.Single(full.ActiveGroundMarks.ToArray());
        Assert.False(stylizedMark.IsDense);
        Assert.True(fullMark.IsDense);
        Assert.True(
            fullMark.LifetimeSeconds > stylizedMark.LifetimeSeconds);
    }

    /// <summary>
    /// RED. This system keys on <c>BattleEventKind.Attack</c> rather than on
    /// damage, unlike <see cref="HitEffectSystem"/>, so without the resolution
    /// check every parried blow sprays blood. A landed blow in the same batch
    /// is asserted alongside, so a system that stopped spraying entirely would
    /// not pass this case either.
    /// </summary>
    [Fact]
    public void Ingest_ProducesNothingForANonLandedAttack()
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
            var system = new BloodEffectSystem { Intensity = GoreIntensity.Full };
            AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];

            system.Ingest(
                [
                    AttackEvent(
                        1,
                        source: 2,
                        target: 7,
                        damage: 0,
                        resolution: resolution),
                ],
                agents);

            Assert.Empty(system.ActiveBursts.ToArray());
            Assert.Empty(system.ActiveGroundMarks.ToArray());
            Assert.Empty(system.ActiveSpurts.ToArray());
        }

        var mixed = new BloodEffectSystem { Intensity = GoreIntensity.Full };
        AgentView[] mixedAgents = [Agent(2, 0, 0), Agent(7, 300, 0)];

        mixed.Ingest(
            [
                AttackEvent(
                    1,
                    source: 2,
                    target: 7,
                    damage: 0,
                    resolution: AttackResolution.Parried),
                AttackEvent(2, source: 2, target: 7, damage: 25),
            ],
            mixedAgents);

        var burst = Assert.Single(mixed.ActiveBursts.ToArray());
        Assert.Equal(2, burst.Sequence);
    }

    [Fact]
    public void Ingest_IsIdenticalAcrossTwoIndependentlyConstructedSystems()
    {
        var first = new BloodEffectSystem { Intensity = GoreIntensity.Full };
        var second = new BloodEffectSystem { Intensity = GoreIntensity.Full };
        AgentView[] agents =
        [
            Agent(2, 0, 0),
            Agent(5, 900, 120),
            Agent(7, 300, 0, isAlive: false),
        ];
        BattleEvent[] events =
        [
            AttackEvent(1, source: 2, target: 7, damage: 30),
            AttackEvent(2, source: 5, target: 7, damage: 44),
            NonAttackEvent(3, BattleEventKind.Death, source: 7),
        ];

        foreach (var system in new[] { first, second })
        {
            system.Ingest(events, agents);
            system.Advance(0.05f);
            system.Ingest(events, agents);
            system.Advance(0.05f);
        }

        Assert.Equal(
            first.ActiveBursts.ToArray(),
            second.ActiveBursts.ToArray());
        Assert.Equal(
            first.ActiveGroundMarks.ToArray(),
            second.ActiveGroundMarks.ToArray());
        Assert.Equal(
            first.ActiveSpurts.ToArray(),
            second.ActiveSpurts.ToArray());
    }

    private static AgentView Agent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive = true) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            isAlive,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent AttackEvent(
        long sequence,
        ulong source,
        ulong target,
        int damage = 10,
        WeaponId weapon = WeaponId.Kampilan,
        BodyPart hitLocation = BodyPart.Chest,
        AttackResolution resolution = AttackResolution.Landed) =>
        BattleEvent.Attack(
            sequence,
            tick: 1,
            source,
            target,
            damage,
            factionId: 0,
            weapon,
            ShieldId.None,
            hitLocation,
            resolution);

    private static BattleEvent DamageEvent(
        long sequence,
        ulong target,
        int damage) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            BattleEventKind.Damage,
            target,
            target,
            damage,
            factionId: null);

    private static BattleEvent NonAttackEvent(
        long sequence,
        BattleEventKind kind,
        ulong source) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            kind,
            source,
            targetEntityId: null,
            value: 0,
            factionId: null);
}
