using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Five cases. Three are RED against the Phase 0 stub, which stores nothing
/// and expires nothing, and two are GUARD because a store that never stores
/// satisfies a negative assertion by accident.
/// </summary>
public sealed class SwingAnimationSystemTests
{
    /// <summary>RED. The stub stores nothing, so no swing is created.</summary>
    [Fact]
    public void Ingest_CreatesOneSwingPerAttacker()
    {
        var system = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents =
            [Agent(2, 0, 0), Agent(7, 300, 0), Agent(9, 300, 300)];

        system.Ingest(
            [
                AttackEvent(1, source: 2, target: 7),
                AttackEvent(2, source: 9, target: 7),
                NonAttackEvent(3, BattleEventKind.Move, source: 2),
                NonAttackEvent(4, BattleEventKind.Death, source: 7),
            ],
            agents);

        var swings = system.ActiveSwings.ToArray();
        Assert.Equal(2, swings.Length);
        Assert.True(system.TryGetSwing(2, out var fromTwo));
        Assert.Equal(7UL, fromTwo.TargetEntityId);
        Assert.Equal(1f, fromTwo.DirectionX, precision: 5);
        Assert.Equal(0f, fromTwo.DirectionY, precision: 5);
        Assert.True(system.TryGetSwing(9, out var fromNine));
        Assert.Equal(7UL, fromNine.TargetEntityId);
        Assert.Equal(0f, fromNine.DirectionX, precision: 5);
        Assert.Equal(-1f, fromNine.DirectionY, precision: 5);
    }

    /// <summary>
    /// RED. One slot per agent, newest wins, and the replacement restarts the
    /// clock rather than inheriting the age of the swing it displaced.
    /// </summary>
    [Fact]
    public void Ingest_ReplacesAnInFlightSwingForTheSameAgent()
    {
        var system = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents =
            [Agent(2, 0, 0), Agent(7, 300, 0), Agent(9, 300, 300)];

        system.Ingest([AttackEvent(1, source: 2, target: 7)], agents);
        system.Advance(0.05f);
        system.Ingest(
            [AttackEvent(2, source: 2, target: 9, AttackResolution.Parried)],
            agents);

        var swing = Assert.Single(system.ActiveSwings.ToArray());
        Assert.Equal(2, swing.Sequence);
        Assert.Equal(2UL, swing.AttackerEntityId);
        Assert.Equal(9UL, swing.TargetEntityId);
        Assert.Equal(AttackResolution.Parried, swing.Resolution);
        Assert.Equal(0f, swing.AgeSeconds);
    }

    /// <summary>
    /// RED, and RED only because it asserts the swing is present before its
    /// total duration has run. Written as advance-then-assert-empty it would
    /// pass against a store that never stores.
    /// </summary>
    [Fact]
    public void Advance_ExpiresASwingAfterItsTotalDuration()
    {
        var system = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0)];
        system.Ingest([AttackEvent(1, source: 2, target: 7)], agents);

        Assert.Single(system.ActiveSwings.ToArray());

        system.Advance(SwingAnimation.TotalSeconds * 0.5f);

        var midFlight = Assert.Single(system.ActiveSwings.ToArray());
        Assert.Equal(
            SwingAnimation.TotalSeconds * 0.5f,
            midFlight.AgeSeconds,
            precision: 5);

        system.Advance(SwingAnimation.TotalSeconds * 0.5f);

        Assert.Empty(system.ActiveSwings.ToArray());
        Assert.False(system.TryGetSwing(2, out _));
    }

    /// <summary>
    /// GUARD, satisfied by a stub that stores nothing. The array is sized at
    /// construction and an agent cannot accumulate swings, so no flood of
    /// attacks can push the store past its capacity.
    /// </summary>
    [Fact]
    public void Ingest_StaysBoundedUnderAFloodOfAttacks()
    {
        const int Capacity = 4;
        var system = new SwingAnimationSystem(Capacity);
        var agents = new AgentView[12];
        for (var index = 0; index < agents.Length; index++)
        {
            agents[index] = Agent((ulong)index + 1, index * 40, 0);
        }

        for (var round = 0; round < 3; round++)
        {
            var events = new BattleEvent[11];
            for (var index = 0; index < events.Length; index++)
            {
                events[index] = AttackEvent(
                    (round * 11) + index + 1,
                    source: (ulong)index + 1,
                    target: (ulong)index + 2);
            }

            system.Ingest(events, agents);
            system.Advance(0.01f);
            Assert.True(
                system.ActiveSwings.Length <= Capacity,
                $"Round {round} held {system.ActiveSwings.Length} swings.");
        }
    }

    /// <summary>
    /// GUARD, satisfied by a stub that stores nothing. It follows the
    /// <c>BloodEffectSystem.ResolveDirection</c> precedent: an attack event may
    /// name an entity missing from the supplied views, and neither a throw nor
    /// a swing placed at the origin is acceptable.
    /// </summary>
    [Fact]
    public void Ingest_IgnoresAnAttackWhoseAttackerOrTargetIsNotInTheAgentViews()
    {
        var system = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0)];

        system.Ingest(
            [
                AttackEvent(1, source: 2, target: 7),
                AttackEvent(2, source: 5, target: 2),
            ],
            agents);

        Assert.Empty(system.ActiveSwings.ToArray());
        Assert.False(system.TryGetSwing(2, out _));
        Assert.False(system.TryGetSwing(5, out _));
    }

    private static AgentView Agent(ulong entityId, int xRaw, int yRaw) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent AttackEvent(
        long sequence,
        ulong source,
        ulong target,
        AttackResolution resolution = AttackResolution.Landed) =>
        BattleEvent.Attack(
            sequence,
            tick: 1,
            source,
            target,
            damage: resolution == AttackResolution.Landed ? 10 : 0,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Chest,
            resolution);

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
