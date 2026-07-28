using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class TrampleMarkSystemTests
{
    [Fact]
    public void Capacity_IsExactly128()
    {
        Assert.Equal(128, TrampleMarkSystem.Capacity);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrampleMarkSystem(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrampleMarkSystem(capacity: -1));
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var system = new TrampleMarkSystem(capacity: 8);

        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(null!, Array.Empty<AgentView>()));
        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(Array.Empty<BattleEvent>(), null!));
    }

    [Fact]
    public void Ingest_DeathEventAddsOneMarkAtTheAgentsPosition()
    {
        var system = new TrampleMarkSystem(capacity: 8);
        AgentView[] agents = [Agent(7, xRaw: 1_200, yRaw: 3_400)];

        system.Ingest([DeathEvent(source: 7)], agents);

        var mark = Assert.Single(system.ActiveMarks.ToArray());
        Assert.Equal(1_200, mark.XRaw);
        Assert.Equal(3_400, mark.YRaw);
    }

    [Fact]
    public void Ingest_MoveEventNeverAddsAMark()
    {
        var system = new TrampleMarkSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest([MoveEvent(source: 7)], agents);

        Assert.Empty(system.ActiveMarks.ToArray());
    }

    [Fact]
    public void Ingest_OnlyDeathEventsAddMarksAmongAMixedBatch()
    {
        var system = new TrampleMarkSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200), Agent(8, 300, 400)];

        system.Ingest(
        [
            MoveEvent(source: 7),
            DeathEvent(source: 8),
            MoveEvent(source: 8),
        ], agents);

        var mark = Assert.Single(system.ActiveMarks.ToArray());
        Assert.Equal((300, 400), (mark.XRaw, mark.YRaw));
    }

    [Fact]
    public void Ingest_IgnoresDeathWithoutAMatchingAgentView()
    {
        var system = new TrampleMarkSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200)];

        system.Ingest([DeathEvent(source: 999)], agents);

        Assert.Empty(system.ActiveMarks.ToArray());
    }

    [Fact]
    public void Ingest_ConsecutiveTickBatchesRetainEveryMarkUnderCapacity()
    {
        var system = new TrampleMarkSystem(capacity: 8);
        AgentView[] agents = [Agent(7, 100, 200), Agent(8, 300, 400)];

        system.Ingest([DeathEvent(source: 7)], agents);
        system.Ingest([DeathEvent(source: 8)], agents);

        Assert.Equal(
            [(100, 200), (300, 400)],
            system.ActiveMarks.ToArray().Select(m => (m.XRaw, m.YRaw)));
    }

    [Fact]
    public void Ingest_WhenFull_ReplacesTheSingleOldestMarkByInsertionOrder()
    {
        var system = new TrampleMarkSystem(capacity: 2);
        AgentView[] agents =
        [
            Agent(1, 100, 0),
            Agent(2, 200, 0),
            Agent(3, 300, 0),
        ];
        system.Ingest([DeathEvent(source: 1)], agents);
        system.Ingest([DeathEvent(source: 2)], agents);

        system.Ingest([DeathEvent(source: 3)], agents);

        var xValues = system.ActiveMarks.ToArray().Select(m => m.XRaw).ToArray();
        Assert.Equal(2, xValues.Length);
        Assert.DoesNotContain(100, xValues);
        Assert.Contains(200, xValues);
        Assert.Contains(300, xValues);
    }

    [Fact]
    public void Ingest_WhenFull_EvictionOrderStaysFirstInFirstOutAcrossMultipleWraps()
    {
        var system = new TrampleMarkSystem(capacity: 3);
        AgentView[] agents =
        [
            Agent(1, 1, 0),
            Agent(2, 2, 0),
            Agent(3, 3, 0),
            Agent(4, 4, 0),
            Agent(5, 5, 0),
        ];

        for (ulong entityId = 1; entityId <= 5; entityId++)
        {
            system.Ingest([DeathEvent(source: entityId)], agents);
        }

        var xValues = system.ActiveMarks.ToArray().Select(m => m.XRaw).ToArray();
        Assert.Equal([3, 4, 5], xValues.Order());
    }

    [Fact]
    public void Clear_RemovesEveryMarkAndResetsTheRingCursor()
    {
        var system = new TrampleMarkSystem(capacity: 2);
        AgentView[] agents = [Agent(1, 100, 0), Agent(2, 200, 0)];
        system.Ingest([DeathEvent(source: 1)], agents);
        system.Ingest([DeathEvent(source: 2)], agents);

        system.Clear();

        Assert.Empty(system.ActiveMarks.ToArray());

        // A fresh Ingest after Clear behaves exactly as it would on a
        // brand-new system: the ring cursor and count were both reset, not
        // merely the visible span truncated.
        system.Ingest([DeathEvent(source: 1)], agents);
        var mark = Assert.Single(system.ActiveMarks.ToArray());
        Assert.Equal(100, mark.XRaw);
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

    private static BattleEvent DeathEvent(ulong source) =>
        BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Death,
            source,
            targetEntityId: null,
            value: 0,
            factionId: null);

    private static BattleEvent MoveEvent(ulong source) =>
        BattleEvent.NonAttack(
            sequence: 1,
            tick: 1,
            BattleEventKind.Move,
            source,
            targetEntityId: null,
            value: 0,
            factionId: null);
}
