using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class ProjectileFlightSystemTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProjectileFlightSystem(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProjectileFlightSystem(capacity: -1));
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var system = new ProjectileFlightSystem(capacity: 8);

        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(1, null!, Array.Empty<AgentView>()));
        Assert.Throws<ArgumentNullException>(
            () => system.Ingest(1, Array.Empty<BattleEvent>(), null!));
    }

    [Fact]
    public void Ingest_RejectsATickEarlierThanThePreviousOne()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        system.Ingest(5, [], []);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.Ingest(4, [], []));
    }

    [Fact]
    public void Ingest_AddsOneEntryPerReleaseEventWithTheSourceAsOrigin()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents =
        [
            Agent(1, xRaw: 1000, yRaw: 2000, isAlive: true),
            Agent(2, xRaw: 5000, yRaw: 6000, isAlive: true),
        ];

        system.Ingest(10, [Release(sequence: 1, source: 1, target: 2, flightTicks: 4)], agents);

        var flight = Assert.Single(system.LiveFlights.ToArray());
        Assert.Equal(1L, flight.Sequence);
        Assert.Equal(2UL, flight.TargetEntityId);
        Assert.Equal(10L, flight.LaunchTick);
        Assert.Equal(4, flight.FlightTicks);
        Assert.Equal((1000, 2000), (flight.OriginXRaw, flight.OriginYRaw));
        Assert.Equal((5000, 6000), (flight.DestinationXRaw, flight.DestinationYRaw));
        Assert.Equal((1000, 2000), (flight.CurrentXRaw, flight.CurrentYRaw));
    }

    [Fact]
    public void Ingest_IgnoresANonReleaseEvent()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];

        system.Ingest(
            1,
            [BattleEvent.NonAttack(1, tick: 1, BattleEventKind.Move, 1, 2, value: 6, factionId: null)],
            agents);

        Assert.Empty(system.LiveFlights.ToArray());
    }

    [Fact]
    public void Ingest_IgnoresAReleaseEventWhoseSourceIsAbsent()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0, true)];

        system.Ingest(1, [Release(1, source: 1, target: 2, flightTicks: 3)], agents);

        Assert.Empty(system.LiveFlights.ToArray());
    }

    [Fact]
    public void Ingest_IgnoresAReleaseEventWithNonPositiveFlightTicks()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];

        system.Ingest(1, [Release(1, source: 1, target: 2, flightTicks: 0)], agents);

        Assert.Empty(system.LiveFlights.ToArray());
    }

    [Fact]
    public void Ingest_FallsBackToOriginWhenTheTargetIsAbsent()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, xRaw: 400, yRaw: 800, isAlive: true)];

        system.Ingest(
            1,
            [Release(1, source: 1, target: 99, flightTicks: 5)],
            agents);

        var flight = Assert.Single(system.LiveFlights.ToArray());
        Assert.Equal(99UL, flight.TargetEntityId);
        Assert.Equal((400, 800), (flight.DestinationXRaw, flight.DestinationYRaw));
    }

    /// <summary>
    /// A shot with a flight of N ticks is live for ticks LaunchTick through
    /// LaunchTick + N - 1 and gone from LaunchTick + N onward — the plan's
    /// "live for N ticks, GONE on N+1" acceptance criterion, checked at
    /// every tick of a small flight rather than only at its boundary.
    /// </summary>
    [Fact]
    public void Ingest_EntryIsLiveForExactlyItsFlightTicksThenGone()
    {
        const int FlightTicks = 3;
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 900, 0, true)];

        system.Ingest(100, [Release(1, source: 1, target: 2, FlightTicks)], agents);
        Assert.Single(system.LiveFlights.ToArray());

        system.Ingest(101, [], agents);
        Assert.Single(system.LiveFlights.ToArray());

        system.Ingest(102, [], agents);
        Assert.Single(system.LiveFlights.ToArray());

        system.Ingest(103, [], agents);
        Assert.Empty(system.LiveFlights.ToArray());
    }

    [Fact]
    public void Ingest_InterpolatesTheCurrentPositionLinearlyByElapsedTicks()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, xRaw: 0, yRaw: 0, isAlive: true), Agent(2, xRaw: 1000, yRaw: 0, isAlive: true)];

        system.Ingest(0, [Release(1, source: 1, target: 2, flightTicks: 4)], agents);
        Assert.Equal(0, system.LiveFlights[0].CurrentXRaw);

        system.Ingest(1, [], agents);
        Assert.Equal(250, system.LiveFlights[0].CurrentXRaw);

        system.Ingest(2, [], agents);
        Assert.Equal(500, system.LiveFlights[0].CurrentXRaw);

        system.Ingest(3, [], agents);
        Assert.Equal(750, system.LiveFlights[0].CurrentXRaw);
    }

    [Fact]
    public void Ingest_NeverGrowsPastCapacity()
    {
        var system = new ProjectileFlightSystem(capacity: 2);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];

        system.Ingest(
            1,
            [
                Release(1, source: 1, target: 2, flightTicks: 50),
                Release(2, source: 1, target: 2, flightTicks: 50),
                Release(3, source: 1, target: 2, flightTicks: 50),
            ],
            agents);

        Assert.Equal(2, system.LiveFlights.Length);
    }

    [Fact]
    public void Ingest_WhenFull_ReplacesTheEntrySoonestToExpire()
    {
        var system = new ProjectileFlightSystem(capacity: 2);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];

        // Sequence 1 expires at tick 1 + 2 = 3 (soonest); sequence 2 expires
        // at 1 + 50 = 51.
        system.Ingest(
            1,
            [
                Release(1, source: 1, target: 2, flightTicks: 2),
                Release(2, source: 1, target: 2, flightTicks: 50),
            ],
            agents);

        system.Ingest(1, [], agents); // no-op, same tick
        system.Ingest(
            2,
            [Release(3, source: 1, target: 2, flightTicks: 50)],
            agents);

        Assert.Equal(
            [2L, 3L],
            system.LiveFlights.ToArray().Select(x => x.Sequence).Order());
    }

    [Fact]
    public void Ingest_WhenFull_UsesLowestSequenceToBreakAnExpiryTie()
    {
        var system = new ProjectileFlightSystem(capacity: 2);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];

        system.Ingest(
            1,
            [
                Release(3, source: 1, target: 2, flightTicks: 10),
                Release(2, source: 1, target: 2, flightTicks: 10),
            ],
            agents);

        system.Ingest(2, [Release(4, source: 1, target: 2, flightTicks: 10)], agents);

        Assert.Equal(
            [3L, 4L],
            system.LiveFlights.ToArray().Select(x => x.Sequence).Order());
    }

    [Fact]
    public void Ingest_SameTickTwiceDoesNotDoubleCount()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];
        BattleEvent[] events = [Release(1, source: 1, target: 2, flightTicks: 5)];

        system.Ingest(1, events, agents);
        system.Ingest(1, events, agents);

        Assert.Single(system.LiveFlights.ToArray());
    }

    [Fact]
    public void Clear_EmptiesTheStoreAndAllowsRestartingAtAnEarlierTick()
    {
        var system = new ProjectileFlightSystem(capacity: 8);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];
        system.Ingest(10, [Release(1, source: 1, target: 2, flightTicks: 5)], agents);

        system.Clear();

        Assert.Empty(system.LiveFlights.ToArray());
        system.Ingest(0, [Release(2, source: 1, target: 2, flightTicks: 5)], agents);
        var flight = Assert.Single(system.LiveFlights.ToArray());
        Assert.Equal(2L, flight.Sequence);
    }

    /// <summary>
    /// Warms the dictionary and array up to their steady-state capacity
    /// before measuring, the same shape
    /// <c>BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations</c>
    /// uses: a cold call can legitimately grow the backing dictionary, but
    /// once every capacity a call could need has been reached, ingest must
    /// allocate nothing at all. Every argument array is built before the
    /// measured window opens, so the only allocations the window could catch
    /// are <c>Ingest</c>'s own.
    /// </summary>
    [Fact]
    public void Ingest_AllocatesNothingOncePrimed()
    {
        const int WarmCalls = 16;
        const int MeasuredCalls = 1_000;

        var system = new ProjectileFlightSystem(capacity: 4);
        AgentView[] agents = [Agent(1, 0, 0, true), Agent(2, 0, 0, true)];

        var warmEvents = new BattleEvent[WarmCalls][];
        for (var warm = 0; warm < WarmCalls; warm++)
        {
            warmEvents[warm] = [Release(warm, source: 1, target: 2, flightTicks: 3)];
        }

        var measuredEvents = new BattleEvent[MeasuredCalls][];
        for (var measured = 0; measured < MeasuredCalls; measured++)
        {
            measuredEvents[measured] =
                [Release(1_000 + measured, source: 1, target: 2, flightTicks: 3)];
        }

        var tick = 0L;
        for (var warm = 0; warm < WarmCalls; warm++)
        {
            system.Ingest(tick++, warmEvents[warm], agents);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var measured = 0; measured < MeasuredCalls; measured++)
        {
            system.Ingest(tick++, measuredEvents[measured], agents);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(
            allocatedBytes == 0,
            $"Ingest allocated {allocatedBytes:N0} bytes once primed; expected 0.");
    }

    [Theory]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Arquebus)]
    public void Ingest_CarriesTheLaunchingWeaponFromTheSourceViewOntoTheFlight(
        WeaponId weapon)
    {
        var system = new ProjectileFlightSystem(capacity: 4);

        // A Release event is classless — BattleEvent.NonAttack forces every
        // combat-context field to null — so the weapon can only come from the
        // launcher's own view, exactly as SoundDirector.ResolveReleaseSound
        // reads it.
        system.Ingest(
            tick: 1,
            [Release(sequence: 1, source: 10, target: 20, flightTicks: 8)],
            [
                Agent(10, xRaw: 0, yRaw: 0, isAlive: true, weapon),
                Agent(20, xRaw: 4_096, yRaw: 0, isAlive: true),
            ]);

        var flight = Assert.Single(system.LiveFlights.ToArray());
        Assert.Equal(weapon, flight.Weapon);
    }

    [Fact]
    public void Ingest_KeepsTheWeaponOfAFlightWhoseLauncherHasLeftTheViewList()
    {
        var system = new ProjectileFlightSystem(capacity: 4);

        system.Ingest(
            tick: 1,
            [Release(sequence: 1, source: 10, target: 20, flightTicks: 8)],
            [
                Agent(10, xRaw: 0, yRaw: 0, isAlive: true, WeaponId.Bangkaw),
                Agent(20, xRaw: 4_096, yRaw: 0, isAlive: true),
            ]);

        // The launcher is gone by the next tick. A draw-time lookup would have
        // nothing to resolve; the value captured at launch does not care.
        system.Ingest(
            tick: 2,
            [],
            [Agent(20, xRaw: 4_096, yRaw: 0, isAlive: true)]);

        var flight = Assert.Single(system.LiveFlights.ToArray());
        Assert.Equal(WeaponId.Bangkaw, flight.Weapon);
    }

    private static AgentView Agent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive,
        WeaponId weapon = WeaponId.Kampilan) =>
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
                weapon,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent Release(
        long sequence,
        ulong source,
        ulong? target,
        int flightTicks) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            BattleEventKind.Release,
            source,
            target,
            value: flightTicks,
            factionId: null);
}
