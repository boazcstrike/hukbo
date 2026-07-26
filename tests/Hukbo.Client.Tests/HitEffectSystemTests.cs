using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class HitEffectSystemTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HitEffectSystem(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HitEffectSystem(capacity: -1));
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var system = new HitEffectSystem(capacity: 8);

        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(null!, Array.Empty<AgentView>()));
        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(Array.Empty<BattleEvent>(), null!));
    }

    [Fact]
    public void Ingest_CreatesOneEffectOnlyForEachDamageEvent()
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, xRaw: 1200, yRaw: 3400, isAlive: true)];
        BattleEvent[] events =
        [
            Event(1, BattleEventKind.Attack, source: 2, target: 7),
            Event(2, BattleEventKind.Damage, source: 7, target: 7, value: 18),
            Event(3, BattleEventKind.Move, source: 7, target: null),
        ];

        system.Ingest(events, agents);

        var effect = Assert.Single(system.ActiveEffects.ToArray());
        Assert.Equal(2, effect.Sequence);
        Assert.Equal(7UL, effect.TargetEntityId);
        Assert.Equal(1200, effect.XRaw);
        Assert.Equal(3400, effect.YRaw);
        Assert.Equal(18, effect.Damage);
        Assert.False(effect.IsLethal);
    }

    [Fact]
    public void Ingest_ClassifiesLethalDamageAndCapturesDeadAgentPosition()
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 1200, 3400, isAlive: false)];

        system.Ingest(
        [
            Event(10, BattleEventKind.Damage, 7, 7, value: 40),
            Event(11, BattleEventKind.Death, 7, null),
        ], agents);

        var effect = Assert.Single(system.ActiveEffects.ToArray());
        Assert.True(effect.IsLethal);
        Assert.Equal((1200, 3400), (effect.XRaw, effect.YRaw));
    }

    [Fact]
    public void Ingest_ConsecutiveTickBatchesRetainsEveryDamage()
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200, true), Agent(8, 300, 400, true)];

        system.Ingest([Event(1, BattleEventKind.Damage, 7, 7, 5)], agents);
        system.Ingest([Event(2, BattleEventKind.Damage, 8, 8, 6)], agents);

        Assert.Equal(
            [1L, 2L],
            system.ActiveEffects.ToArray().Select(x => x.Sequence));
    }

    [Fact]
    public void Ingest_ResolvesArbitraryNoncontiguousEntityIds()
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents =
        [
            Agent(900_001, 100, 200, true),
            Agent(17, 300, 400, true),
            Agent(42_424, 500, 600, true),
        ];

        system.Ingest(
        [
            Event(
                1,
                BattleEventKind.Damage,
                source: 42_424,
                target: 42_424,
                value: 9),
        ], agents);

        var effect = Assert.Single(system.ActiveEffects.ToArray());
        Assert.Equal(42_424UL, effect.TargetEntityId);
        Assert.Equal((500, 600), (effect.XRaw, effect.YRaw));
    }

    [Fact]
    public void Ingest_IgnoresDamageWithoutTargetOrMatchingView()
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200, true)];

        system.Ingest(
        [
            Event(1, BattleEventKind.Damage, 7, null, 5),
            Event(2, BattleEventKind.Damage, 8, 8, 6),
        ], agents);

        Assert.Empty(system.ActiveEffects.ToArray());
    }

    [Fact]
    public void Advance_ExpiresOrdinaryAt180Milliseconds()
    {
        var system = CreateSystemWithHit(isLethal: false);

        system.Advance(0.179f);
        Assert.Single(system.ActiveEffects.ToArray());

        system.Advance(0.001f);
        Assert.Empty(system.ActiveEffects.ToArray());
    }

    [Fact]
    public void Advance_ExpiresLethalAt280Milliseconds()
    {
        var system = CreateSystemWithHit(isLethal: true);

        system.Advance(0.279f);
        Assert.Single(system.ActiveEffects.ToArray());

        system.Advance(0.001f);
        Assert.Empty(system.ActiveEffects.ToArray());
    }

    [Fact]
    public void Advance_RejectsNegativeOrNonFiniteElapsedTime()
    {
        var system = new HitEffectSystem(capacity: 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(-0.001f));
        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Advance(float.PositiveInfinity));
    }

    [Fact]
    public void Ingest_WhenFull_ReplacesOldestEffect()
    {
        var system = new HitEffectSystem(capacity: 2);
        AgentView[] agents =
        [
            Agent(1, 100, 200, true),
            Agent(2, 300, 400, true),
            Agent(3, 500, 600, true),
        ];
        system.Ingest([Event(3, BattleEventKind.Damage, 1, 1, 5)], agents);
        system.Advance(0.04f);
        system.Ingest([Event(2, BattleEventKind.Damage, 2, 2, 6)], agents);
        system.Advance(0.02f);

        system.Ingest([Event(4, BattleEventKind.Damage, 3, 3, 7)], agents);

        Assert.Equal(
            [2L, 4L],
            system.ActiveEffects.ToArray().Select(x => x.Sequence).Order());
    }

    [Fact]
    public void Ingest_WhenFull_UsesLowestSequenceToBreakAgeTies()
    {
        var system = new HitEffectSystem(capacity: 2);
        AgentView[] agents =
        [
            Agent(1, 100, 200, true),
            Agent(2, 300, 400, true),
            Agent(3, 500, 600, true),
        ];
        system.Ingest(
        [
            Event(3, BattleEventKind.Damage, 1, 1, 5),
            Event(2, BattleEventKind.Damage, 2, 2, 6),
        ], agents);

        system.Ingest([Event(4, BattleEventKind.Damage, 3, 3, 7)], agents);

        Assert.Equal(
            [3L, 4L],
            system.ActiveEffects.ToArray().Select(x => x.Sequence).Order());
    }

    [Fact]
    public void GetPulseStrength_ReturnsPositiveOnlyForLivingHitWindow()
    {
        var ordinary = CreateSystemWithHit(isLethal: false);
        var lethal = CreateSystemWithHit(isLethal: true);

        Assert.Equal(1f, ordinary.GetPulseStrength(7));
        Assert.Equal(0f, ordinary.GetPulseStrength(8));
        Assert.Equal(0f, lethal.GetPulseStrength(7));

        ordinary.Advance(0.045f);
        Assert.Equal(0.5f, ordinary.GetPulseStrength(7), precision: 5);

        ordinary.Advance(0.045f);
        Assert.Equal(0f, ordinary.GetPulseStrength(7));
    }

    [Fact]
    public void GetPulseStrength_ReturnsMaximumForMultipleEffectsOnEntity()
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200, true)];
        system.Ingest([Event(1, BattleEventKind.Damage, 7, 7, 5)], agents);
        system.Advance(0.06f);
        system.Ingest([Event(2, BattleEventKind.Damage, 7, 7, 6)], agents);

        Assert.Equal(1f, system.GetPulseStrength(7));
    }

    [Fact]
    public void Clear_RemovesEffectsAndAllowsRestartedSequences()
    {
        var system = new HitEffectSystem(capacity: 1);
        AgentView[] agents = [Agent(7, 100, 200, true)];
        system.Ingest([Event(4, BattleEventKind.Damage, 7, 7, 5)], agents);

        system.Clear();
        system.Ingest([Event(1, BattleEventKind.Damage, 7, 7, 6)], agents);

        var effect = Assert.Single(system.ActiveEffects.ToArray());
        Assert.Equal(1, effect.Sequence);
    }

    private static HitEffectSystem CreateSystemWithHit(bool isLethal)
    {
        var system = new HitEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200, isAlive: !isLethal)];
        var events = new List<BattleEvent>
        {
            Event(1, BattleEventKind.Damage, 7, 7, 5),
        };
        if (isLethal)
        {
            events.Add(Event(2, BattleEventKind.Death, 7, null));
        }

        system.Ingest(events, agents);
        return system;
    }

    private static AgentView Agent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive) =>
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
                WeaponId.GreatBlade,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent Event(
        long sequence,
        BattleEventKind kind,
        ulong source,
        ulong? target,
        int value = 0)
    {
        if (kind == BattleEventKind.Attack)
        {
            return BattleEvent.Attack(
                sequence,
                tick: 1,
                source,
                target ?? checked(source + 1),
                value,
                factionId: 0,
                WeaponId.GreatBlade,
                BodyPart.Chest);
        }

        return BattleEvent.NonAttack(
            sequence,
            tick: 1,
            kind,
            source,
            target,
            value,
            factionId: null);
    }
}
