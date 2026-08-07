using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class DustEffectSystemTests
{
    [Fact]
    public void Capacity_IsExactly32()
    {
        Assert.Equal(32, DustEffectSystem.Capacity);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DustEffectSystem(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DustEffectSystem(capacity: -1));
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var system = new DustEffectSystem(capacity: 8);

        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(null!, Array.Empty<AgentView>()));
        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(Array.Empty<BattleEvent>(), null!));
    }

    [Fact]
    public void MotionIntensity_RejectsUndefinedValue()
    {
        var system = new DustEffectSystem(capacity: 8);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.MotionIntensity = (MotionIntensity)99);
    }

    [Fact]
    public void MotionIntensity_DefaultsToFull()
    {
        var system = new DustEffectSystem(capacity: 8);

        Assert.Equal(MotionIntensity.Full, system.MotionIntensity);
    }

    [Fact]
    public void Ingest_DeathEventAddsOnePuffAtTheAgentsPosition()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, xRaw: 1_200, yRaw: 3_400)];

        system.Ingest([DeathEvent(sequence: 1, source: 7)], agents);

        var puff = Assert.Single(system.ActivePuffs.ToArray());
        Assert.Equal(1_200, puff.XRaw);
        Assert.Equal(3_400, puff.YRaw);
        Assert.Equal(DustPuffKind.Death, puff.Kind);
    }

    [Fact]
    public void Ingest_MoveEventNeverAddsAPuff()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest([MoveEvent(sequence: 1, source: 7)], agents);

        Assert.Empty(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_IgnoresDeathWithoutAMatchingAgentView()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest([DeathEvent(sequence: 1, source: 999)], agents);

        Assert.Empty(system.ActivePuffs.ToArray());
    }

    /// <summary>
    /// Sequence 2 and sequence 1 against source entity 7 are pinned fixtures:
    /// the deterministic sequence/entity-id mix the throttle reads from
    /// resolves sequence 2 to a spawn and sequence 1 to no spawn, so this
    /// test exercises both branches of <c>DustEffectSystem</c>'s private
    /// throttle without reaching into it.
    /// </summary>
    [Fact]
    public void Ingest_AttackEventOnlySometimesAddsAThrottledKick()
    {
        var spawningSystem = new DustEffectSystem(capacity: 8);
        var suppressedSystem = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 500, 600)];

        spawningSystem.Ingest([AttackEvent(sequence: 2, source: 7)], agents);
        suppressedSystem.Ingest([AttackEvent(sequence: 1, source: 7)], agents);

        var puff = Assert.Single(spawningSystem.ActivePuffs.ToArray());
        Assert.Equal(DustPuffKind.AttackKick, puff.Kind);
        Assert.Equal((500, 600), (puff.XRaw, puff.YRaw));
        Assert.Empty(suppressedSystem.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_OnlyDeathAndThrottledAttackAddPuffsAmongAMixedBatch()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200), Agent(8, 300, 400)];

        system.Ingest(
        [
            MoveEvent(sequence: 1, source: 7),
            AttackEvent(sequence: 1, source: 7), // suppressed by throttle
            DeathEvent(sequence: 2, source: 8),
            MoveEvent(sequence: 3, source: 8),
        ], agents);

        var puff = Assert.Single(system.ActivePuffs.ToArray());
        Assert.Equal(DustPuffKind.Death, puff.Kind);
        Assert.Equal((300, 400), (puff.XRaw, puff.YRaw));
    }

    [Fact]
    public void Ingest_OutcomeStopsSpawnsForEventsAfterItInTheSameBatch()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200), Agent(8, 300, 400)];

        system.Ingest(
        [
            OutcomeEvent(sequence: 1),
            DeathEvent(sequence: 2, source: 7),
        ], agents);

        Assert.Empty(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_OutcomeDoesNotRetractASpawnEarlierInTheSameBatch()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest(
        [
            DeathEvent(sequence: 1, source: 7),
            OutcomeEvent(sequence: 2),
        ], agents);

        Assert.Single(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_OutcomeSuppressesEverySubsequentBatch()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200), Agent(8, 300, 400)];
        system.Ingest([OutcomeEvent(sequence: 1)], agents);

        system.Ingest([DeathEvent(sequence: 2, source: 8)], agents);

        Assert.Empty(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_MotionIntensityOffSuppressesNewSpawns()
    {
        var system = new DustEffectSystem(capacity: 8) { MotionIntensity = MotionIntensity.Off };
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest([DeathEvent(sequence: 1, source: 7)], agents);

        Assert.Empty(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_MotionIntensityReducedLeavesDustUnchanged()
    {
        var system = new DustEffectSystem(capacity: 8)
        {
            MotionIntensity = MotionIntensity.Reduced,
        };
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest([DeathEvent(sequence: 1, source: 7)], agents);

        Assert.Single(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Ingest_MotionIntensityOffDoesNotClearAlreadyLivePuffs()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];
        system.Ingest([DeathEvent(sequence: 1, source: 7)], agents);

        system.MotionIntensity = MotionIntensity.Off;

        Assert.Single(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Advance_ExpiresADeathPuffAt400Milliseconds()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];
        system.Ingest([DeathEvent(sequence: 1, source: 7)], agents);

        system.Advance(0.399f);
        Assert.Single(system.ActivePuffs.ToArray());

        // A plain +0.001f here lands the cumulative sum a float epsilon
        // below 0.4f (0.399f + 0.001f rounds to 0.39999998f, not exactly
        // 0.4f), which would fail the >= LifetimeSeconds check by rounding
        // noise rather than by design. Advance further past the boundary so
        // the assertion exercises "crossed the lifetime", not float
        // precision at the edge of it.
        system.Advance(0.01f);
        Assert.Empty(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Advance_ExpiresAnAttackKickAt220Milliseconds()
    {
        var system = new DustEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];
        system.Ingest([AttackEvent(sequence: 2, source: 7)], agents);
        Assert.Single(system.ActivePuffs.ToArray());

        system.Advance(0.219f);
        Assert.Single(system.ActivePuffs.ToArray());

        system.Advance(0.001f);
        Assert.Empty(system.ActivePuffs.ToArray());
    }

    [Fact]
    public void Advance_RejectsNegativeOrNonFiniteElapsedTime()
    {
        var system = new DustEffectSystem(capacity: 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(-0.001f));
        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Advance(float.PositiveInfinity));
    }

    [Fact]
    public void Ingest_WhenFull_ReplacesTheOldestPuff()
    {
        var system = new DustEffectSystem(capacity: 2);
        AgentView[] agents =
        [
            Agent(1, 100, 200),
            Agent(2, 300, 400),
            Agent(3, 500, 600),
        ];
        system.Ingest([DeathEvent(sequence: 1, source: 1)], agents);
        system.Advance(0.04f);
        system.Ingest([DeathEvent(sequence: 2, source: 2)], agents);
        system.Advance(0.02f);

        system.Ingest([DeathEvent(sequence: 3, source: 3)], agents);

        Assert.Equal(
            [(300, 400), (500, 600)],
            system.ActivePuffs.ToArray()
                .Select(p => (p.XRaw, p.YRaw))
                .OrderBy(p => p.XRaw));
    }

    [Fact]
    public void Clear_RemovesEveryPuffResetsOutcomeGateAndAllowsRestartedSequences()
    {
        var system = new DustEffectSystem(capacity: 1);
        AgentView[] agents = [Agent(7, 100, 200)];
        system.Ingest(
        [
            DeathEvent(sequence: 4, source: 7),
            OutcomeEvent(sequence: 5),
        ], agents);

        system.Clear();
        system.Ingest([DeathEvent(sequence: 1, source: 7)], agents);

        var puff = Assert.Single(system.ActivePuffs.ToArray());
        Assert.Equal(1, puff.Sequence);
    }

    [Fact]
    public void ShadeInterpolation_StaysWithinTheGroundShadeRangeAndTheCeiling()
    {
        Assert.True(
            DustGeometry.ShadeInterpolation <=
            PlainsBackdropGeometry.GroundShadeInterpolation.Max());
        Assert.True(
            DustGeometry.ShadeInterpolation <=
            PlainsBackdropGeometry.MaximumBackdropInterpolation);
    }

    private static AgentView Agent(ulong entityId, int xRaw, int yRaw) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: false,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent DeathEvent(long sequence, ulong source) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            BattleEventKind.Death,
            source,
            targetEntityId: null,
            value: 0,
            factionId: null);

    private static BattleEvent MoveEvent(long sequence, ulong source) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            BattleEventKind.Move,
            source,
            targetEntityId: null,
            value: 0,
            factionId: null);

    private static BattleEvent OutcomeEvent(long sequence) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: 0,
            factionId: 0);

    private static BattleEvent AttackEvent(long sequence, ulong source) =>
        BattleEvent.Attack(
            sequence,
            tick: 1,
            source,
            targetEntityId: checked(source + 1),
            damage: 5,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Chest);
}
