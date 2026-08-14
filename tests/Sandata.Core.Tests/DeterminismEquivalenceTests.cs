using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Diagnostics;
using Sandata.Core.Combat;
using Sandata.Core.Events;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 52a of Sandata's scaffold plan, design section 13's
/// determinism equivalence suite: proves the four properties that "same
/// seed, same build" is actually built out of, each as a relational
/// comparison between two computed results rather than a comparison against
/// a pinned literal (<c>GoldenReplayTests.cs</c>, owned by a parallel task,
/// carries the golden baseline instead).
/// </summary>
/// <remarks>
/// <b>The "fresh process" clause.</b> The task 52 row also names a fresh-
/// process rerun. No test project in this repository spawns a process —
/// confirmed by reading <c>tests/Hukbo.Core.Tests/DeterminismTests.cs</c> in
/// full — and doing so here would import the wall clock, the filesystem, and
/// the build layout into what is otherwise a pure unit suite, none of which
/// this repository's non-negotiables allow into <c>Sandata.Core.Tests</c>.
/// <c>./scripts/benchmark.ps1 -Game Sandata -Seed 1</c> already runs as a
/// fresh process on every invocation; that script, task 55's own evidence,
/// is where the fresh-process property is actually exercised. This file
/// contributes no test for it.
/// </remarks>
public sealed class DeterminismEquivalenceTests
{
    // Small enough to keep this suite fast; large enough that the dense,
    // alternating-faction placement <see cref="HeadlessRunner.BuildInitialState"/>
    // produces (see that method's own remarks) actually drives sensing,
    // collision, and fire resolution within the tick budget below, matching
    // the reasoning HeadlessRunnerTests already states for the same count.
    private const int OperatorCount = 20;
    private const ulong Seed = 1;
    private const int TickCount = 60;
    private const int InitialOperatorTotalHealth = OperatorCount * 100;

    // ---- Shared fixture -------------------------------------------------

    /// <summary>
    /// One seeded, densely packed mission fixture, built the same way
    /// <see cref="HeadlessRunner.Execute"/> builds its own: <see
    /// cref="HeadlessRunner.BuildOpenGrid"/> and <see
    /// cref="HeadlessRunner.BuildInitialState"/> are <see langword="internal"/>
    /// members this assembly already reaches through <c>Sandata.Headless</c>'s
    /// own <c>InternalsVisibleTo("Sandata.Core.Tests")</c> grant (also used by
    /// <c>HeadlessRunnerTests.cs</c>). <see cref="Mission"/> itself is built
    /// locally because <c>HeadlessRunner</c>'s own mission builder is private.
    /// Every collection <see cref="MissionState"/> and <see cref="NavGrid"/>
    /// carry is either immutable or, for <see cref="NavGrid.Passability"/>,
    /// never written to again after this method bakes it open — safe to share
    /// as one instance across every simulation this file constructs, exactly
    /// as <see cref="HeadlessRunner.Execute"/>'s own remarks explain for its
    /// <c>left</c>/<c>right</c> pair.
    /// </summary>
    private static (Mission Mission, NavGrid Grid, WallBuckets WallBuckets, MissionState InitialState) BuildFixture()
    {
        var (grid, wallBuckets, packingSide) = HeadlessRunner.BuildOpenGrid(OperatorCount);
        var initialState = HeadlessRunner.BuildInitialState(OperatorCount, Seed, packingSide);
        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: Seed,
            mapContentHash: 1UL,
            // StateHashCadenceTicks: 1 so every RunTick call below leaves a
            // fresh LastStateHash to compare, matching
            // TickPipelineTests.BuildMission's own reason for the same choice.
            tickPolicy: new MissionTickPolicy(TickLimit: 100_000, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(FactionId: 0, OperatorCount: OperatorCount / 2),
                new MissionFactionSetup(FactionId: 1, OperatorCount: OperatorCount / 2)),
            rulesetId: SandataPresetId.ModernTacticalV1);
        return (mission, grid, wallBuckets, initialState);
    }

    private static SandataSimulation NewSimulation(
        Mission mission, NavGrid grid, WallBuckets wallBuckets, MissionState state) =>
        new(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

    /// <summary>
    /// A single authored order, submitted identically to two simulations in
    /// <see cref="SaveAndResumeAcrossASnapshot_MatchesAnUnresumedRun"/>, so
    /// that test exercises design section 16's "an authored polyline is
    /// player input... restored exactly as it was drawn" rather than only the
    /// autonomous-combat path every other test in this file already covers.
    /// Entity 1's own starting cell plus one grid cell to its east — well
    /// inside every fixture this file builds, since
    /// <see cref="HeadlessRunner.BuildOpenGrid"/> pads the grid to fit every
    /// operator with room to spare. <see cref="OrderPathNode"/> coordinates
    /// are plain world units matching <see cref="NavGrid"/>'s own coordinate
    /// space (confirmed by <c>OrderValidationTests.PathOf</c>'s fixtures), not
    /// the <see cref="FixedPoint"/> raw scale operator positions use, so the
    /// entity's raw position is divided back down by <see cref="FixedPoint.Scale"/>
    /// first.
    /// </summary>
    private static ImmutableArray<OrderPathNode> BuildEntityOneEastwardPath(MissionState initialState)
    {
        var entityOne = initialState.Operators.Single(op => op.EntityId == 1UL);
        var startX = entityOne.PositionX.RawValue / FixedPoint.Scale;
        var startY = entityOne.PositionY.RawValue / FixedPoint.Scale;
        return ImmutableArray.Create(
            new OrderPathNode(startX, startY),
            new OrderPathNode(startX + NavGrid.CellSizeWu, startY));
    }

    // ---- Cross-tick comparison helpers -----------------------------------

    /// <summary>
    /// Everything design section 4 names as the observable determinism
    /// surface for one tick: the tick itself, the winner, the cadence-gated
    /// state hash, and the event feed's rolling hash plus its full ordered,
    /// uncapped-by-this-comparison window. Captured once per tick from a
    /// reference run so a later run can be checked against it tick for tick,
    /// not only at the end.
    /// </summary>
    private readonly record struct TickSnapshot(
        long Tick,
        int Winner,
        ulong? StateHash,
        ulong EventHash,
        ImmutableArray<MissionEvent> Events);

    private static TickSnapshot CaptureTick(SandataSimulation simulation) => new(
        simulation.State.Tick,
        simulation.State.Winner,
        simulation.LastStateHash,
        simulation.State.EventFeed.Hash,
        simulation.State.EventFeed.Events);

    private static void AssertTicksAgree(TickSnapshot expected, SandataSimulation actual, long tick)
    {
        Assert.Equal(expected.Tick, actual.State.Tick);
        Assert.Equal(expected.Winner, actual.State.Winner);
        Assert.Equal(expected.StateHash, actual.LastStateHash);
        Assert.Equal(expected.EventHash, actual.State.EventFeed.Hash);
        Assert.True(
            expected.Events.SequenceEqual(actual.State.EventFeed.Events),
            $"Ordered event streams diverged at tick {tick.ToString(CultureInfo.InvariantCulture)}.");
    }

    /// <summary>
    /// Refuses to let any equivalence test above pass because both sides did
    /// nothing: every test-quality rule for this wave requires proving the
    /// compared run was actually active — events emitted and health lost to
    /// combat — before its agreement with another run means anything.
    /// </summary>
    private static void AssertRunWasActive(SandataSimulation simulation)
    {
        Assert.NotEmpty(simulation.State.EventFeed.Events);
        var totalHealth = simulation.State.Operators.Sum(op => op.Health);
        Assert.True(
            totalHealth < InitialOperatorTotalHealth,
            $"Expected combat to reduce total operator health below {InitialOperatorTotalHealth}, " +
            $"but it stayed at {totalHealth.ToString(CultureInfo.InvariantCulture)}.");
    }

    // ---- 1. Same-seed repeat, in process ---------------------------------

    /// <summary>
    /// Holds constant: the mission, ruleset, nav grid, wall buckets, and
    /// initial <see cref="MissionState"/> — one fixture, shared by both
    /// simulations. Varies: nothing except which <see cref="SandataSimulation"/>
    /// instance is asked to run each tick. Asserts: tick, winner, state hash,
    /// event hash, and the full ordered event stream agree after every one of
    /// <see cref="TickCount"/> ticks, and that survivor counts (design
    /// section 4's own named determinism fields) agree at the end.
    /// </summary>
    [Fact]
    public void TwoInProcessRunsOfTheSameSeed_AgreeTickForTick()
    {
        var (mission, grid, wallBuckets, initialState) = BuildFixture();
        var left = NewSimulation(mission, grid, wallBuckets, initialState);
        var right = NewSimulation(mission, grid, wallBuckets, initialState);

        for (var tick = 0; tick < TickCount; tick++)
        {
            left.RunTick(tick);
            right.RunTick(tick);

            AssertTicksAgree(CaptureTick(left), right, tick);
        }

        var leftSurvivors0 = left.State.Operators.Count(op => op.Faction == 0 && DamageResolution.IsAlive(op.Health));
        var leftSurvivors1 = left.State.Operators.Count(op => op.Faction == 1 && DamageResolution.IsAlive(op.Health));
        var rightSurvivors0 = right.State.Operators.Count(op => op.Faction == 0 && DamageResolution.IsAlive(op.Health));
        var rightSurvivors1 = right.State.Operators.Count(op => op.Faction == 1 && DamageResolution.IsAlive(op.Health));

        Assert.Equal(leftSurvivors0, rightSurvivors0);
        Assert.Equal(leftSurvivors1, rightSurvivors1);

        AssertRunWasActive(left);
        AssertRunWasActive(right);
    }

    // ---- 2. Cold-cache equivalence -----------------------------------

    /// <summary>
    /// Holds constant: the same fixture and the same tick range, run to
    /// completion by a reference simulation that has been ticking since
    /// tick 0 (its derived caches — the clearance field baked at
    /// construction, the reused <c>SandataCollisionGrid</c> pair, the
    /// <c>PathService</c> instance — are "warm": built once and carried across
    /// every tick since the start). Varies: a second simulation is
    /// constructed fresh, from the reference's own already-advanced
    /// <see cref="MissionState"/> at the midpoint tick, handed to a brand-new
    /// <see cref="SandataSimulation"/> — a genuinely cold instance, per this
    /// type's own constructor remarks: every derived field (<c>_clearanceField</c>,
    /// <c>_contactGrid</c>, <c>_cohesionGrid</c>, <c>_pathService</c>) is
    /// rebuilt from scratch, none of it copied from the reference. Asserts:
    /// from the midpoint tick onward, the cold instance matches the warm
    /// reference tick for tick, not merely at the final tick.
    /// </summary>
    [Fact]
    public void AFreshlyConstructedSimulation_MatchesAnAlreadyRunningOne_TickForTickAfterTheMidpoint()
    {
        const int MidpointTick = TickCount / 2;

        var (mission, grid, wallBuckets, initialState) = BuildFixture();
        var warm = NewSimulation(mission, grid, wallBuckets, initialState);

        var referenceSnapshotsFromMidpoint = new List<TickSnapshot>(TickCount - MidpointTick);
        for (var tick = 0; tick < TickCount; tick++)
        {
            warm.RunTick(tick);
            if (tick >= MidpointTick)
            {
                referenceSnapshotsFromMidpoint.Add(CaptureTick(warm));
            }
        }

        // Re-run an identical warm instance up to the midpoint so its
        // mid-mission MissionState — already carrying MidpointTick ticks of
        // combat — is what the cold instance below is seeded with, without
        // reusing any of the first warm instance's own derived structures.
        var warmUpToMidpoint = NewSimulation(mission, grid, wallBuckets, initialState);
        for (var tick = 0; tick < MidpointTick; tick++)
        {
            warmUpToMidpoint.RunTick(tick);
        }

        var cold = NewSimulation(mission, grid, wallBuckets, warmUpToMidpoint.State);

        for (var tick = MidpointTick; tick < TickCount; tick++)
        {
            cold.RunTick(tick);
            AssertTicksAgree(referenceSnapshotsFromMidpoint[tick - MidpointTick], cold, tick);
        }

        AssertRunWasActive(warm);
        AssertRunWasActive(cold);
    }

    // ---- 3. Save and resume equivalence across a mid-mission snapshot ----

    /// <summary>
    /// Holds constant: the same fixture, the same authored order (see
    /// <see cref="BuildEntityOneEastwardPath"/>) submitted identically to
    /// both sides, and the same total tick count. Varies: the reference
    /// simulation never stops; the other runs only to the midpoint, is
    /// captured through <see cref="MissionStateSnapshotExtensions.ToSnapshot"/>,
    /// rebuilt through <see cref="MissionSnapshot.ToState"/>, and handed to a
    /// brand-new <see cref="SandataSimulation"/> that inherits no path cache,
    /// no collision grid, and no clearance field from the simulation that
    /// produced the snapshot — proof this is a genuine resume and not merely
    /// the same object continuing. Asserts: tick for tick from the midpoint
    /// onward, the resumed simulation matches the never-stopped reference,
    /// including the entity-1 order's <see cref="OrderAssignment"/> — the one
    /// piece of state that would silently vanish if
    /// <see cref="MissionSnapshot"/> ever dropped <c>OrderQueue</c> or
    /// <c>OrderAssignments</c> from the round trip.
    /// </summary>
    [Fact]
    public void SaveAndResumeAcrossAMidMissionSnapshot_MatchesAnUnresumedRun_TickForTick()
    {
        const int MidpointTick = TickCount / 2;

        // Amended 2026-08-12. This order used to target tick 0, and the
        // assignment it created was still sitting on the operator thirty ticks
        // later because nothing in the pipeline ever advanced or cleared one.
        // Stage 1 now does both, and this fixture's authored path is a single
        // cell long — <see cref="BuildEntityOneEastwardPath"/> ends four world
        // units east of where the operator already stands, well inside the
        // node-arrival radius — so an order given at tick 0 is finished by tick
        // 1 and there would be no live assignment left at the midpoint for the
        // snapshot to round-trip. Targeting the tick before the midpoint keeps
        // exactly what this test exists to check: an assignment that is alive
        // at the moment the snapshot is taken.
        const int OrderTargetTick = MidpointTick - 1;

        var (mission, grid, wallBuckets, initialState) = BuildFixture();
        var pathNodes = BuildEntityOneEastwardPath(initialState);

        var reference = NewSimulation(mission, grid, wallBuckets, initialState);
        var (_, _, referenceRejection) = reference.SubmitOrder(
            targetTick: OrderTargetTick, factionId: 0, addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath, pathNodes: pathNodes);
        Assert.Null(referenceRejection);

        var referenceSnapshotsFromMidpoint = new List<TickSnapshot>(TickCount - MidpointTick);
        for (var tick = 0; tick < TickCount; tick++)
        {
            reference.RunTick(tick);
            if (tick >= MidpointTick)
            {
                referenceSnapshotsFromMidpoint.Add(CaptureTick(reference));
            }
        }

        var stopped = NewSimulation(mission, grid, wallBuckets, initialState);
        var (_, _, stoppedRejection) = stopped.SubmitOrder(
            targetTick: OrderTargetTick, factionId: 0, addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath, pathNodes: pathNodes);
        Assert.Null(stoppedRejection);

        for (var tick = 0; tick < MidpointTick; tick++)
        {
            stopped.RunTick(tick);
        }

        // The order assignment must itself have survived to the midpoint —
        // otherwise the round trip below would trivially preserve nothing of
        // design section 16's authored-polyline guarantee.
        var midpointAssignment = Assert.Single(
            stopped.State.OrderAssignments, a => a.EntityId == 1UL);
        Assert.False(midpointAssignment.PathNodes.IsDefaultOrEmpty);

        var snapshot = stopped.State.ToSnapshot();
        var restoredState = snapshot.ToState();

        var restoredAssignment = Assert.Single(
            restoredState.OrderAssignments, a => a.EntityId == 1UL);
        Assert.Equal(midpointAssignment.PathNodes, restoredAssignment.PathNodes);
        Assert.Equal(midpointAssignment.CurrentNodeIndex, restoredAssignment.CurrentNodeIndex);

        var resumed = NewSimulation(mission, grid, wallBuckets, restoredState);

        for (var tick = MidpointTick; tick < TickCount; tick++)
        {
            resumed.RunTick(tick);
            AssertTicksAgree(referenceSnapshotsFromMidpoint[tick - MidpointTick], resumed, tick);
        }

        AssertRunWasActive(reference);
        AssertRunWasActive(resumed);
    }

    // ---- 4. Logging off versus trc ---------------------------------------

    /// <summary>
    /// Holds constant: the same fixture and tick range, run by two
    /// simulations that are otherwise identical. Varies: after every tick,
    /// the second simulation's observed state (tick, state hash, event hash)
    /// is written through a real, non-<see cref="DiagnosticLog.Disabled"/>
    /// <see cref="LogLevel.Trace"/> logger — <see cref="DiagnosticLog.CreateForWriter"/>
    /// targets an in-memory <see cref="StringWriter"/>, so this never touches
    /// the filesystem design section 5 forbids <c>Sandata.Core</c> from
    /// depending on. Asserts: tick for tick, the logged simulation matches
    /// the unlogged one exactly — <see cref="Sandata.Core"/> never references
    /// <see cref="Hukbo.Diagnostics"/> at all (confirmed by <see
    /// cref="SandataSourceHygieneTests"/>'s sibling coverage of the
    /// production surface), so this is the same "observe, never feed back"
    /// contract CLAUDE.md section 5 states for <c>Hukbo.Core</c>, checked
    /// here for real against Sandata's own headless log call sites.
    /// </summary>
    [Fact]
    public void LoggingAtTraceLevel_DoesNotChangeTheSimulation_ComparedToLoggingOff()
    {
        var (mission, grid, wallBuckets, initialState) = BuildFixture();
        var unlogged = NewSimulation(mission, grid, wallBuckets, initialState);
        var logged = NewSimulation(mission, grid, wallBuckets, initialState);

        using var writer = new StringWriter();
        var traceLog = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Trace, LogChannel.Simulation, DirectoryPath: null), writer);
        Assert.True(traceLog.IsEnabled);

        for (var tick = 0; tick < TickCount; tick++)
        {
            unlogged.RunTick(tick);

            logged.RunTick(tick);
            traceLog.SetTick(tick);
            traceLog.Write(
                LogLevel.Trace,
                LogChannel.Simulation,
                LogEvents.SimTick,
                "tick",
                logged.State.Tick,
                "stateHash",
                logged.LastStateHash?.ToString("X16", CultureInfo.InvariantCulture) ?? "(none)",
                "eventHash",
                logged.State.EventFeed.Hash.ToString("X16", CultureInfo.InvariantCulture));

            AssertTicksAgree(CaptureTick(unlogged), logged, tick);
        }

        // Proves the trace log actually did work every tick, rather than the
        // comparison above passing only because logging was silently a
        // no-op: a disabled log never grows its writer's buffer.
        Assert.True(writer.GetStringBuilder().Length > 0);

        AssertRunWasActive(unlogged);
        AssertRunWasActive(logged);
    }

    // ---- 5. A derived published path across a resume ---------------------

    /// <summary>
    /// Task 90. Test 3 above snapshots an <i>authored</i> polyline, which
    /// design section 16 states is player input and authoritative stored
    /// state; it round-trips as data and proves nothing about design section
    /// 4's separate rule for the <i>derived</i> polyline of an autonomous
    /// group path — "the request is authoritative and snapshotted; the result
    /// is not. On resume, every outstanding and every published path is
    /// recomputed from its stored request record before the first tick
    /// executes." No test in this file exercised that, because no fixture in
    /// it populates <see cref="MissionState.Groups"/> at all.
    /// <para>
    /// This one does, on its own fixture rather than the shared one, for the
    /// reason task 89 measured: in the dense twenty-operator packing every
    /// mover is stalled against another body within a step or two, so a
    /// comparison window there would compare two runs that are both standing
    /// still. One operator on a clear route is the smallest fixture in which
    /// the walk actually continues through the compared window, and the walk
    /// is what this test needs to be real. It carries no combat and therefore
    /// cannot use <see cref="AssertRunWasActive"/>; sustained movement inside
    /// the compared window is its activity check instead, asserted on both
    /// sides.
    /// </para>
    /// </summary>
    [Fact]
    public void ADerivedGroupPathAcrossAResume_KeepsTheOperatorWalkingAndMatchesAnUnresumedRun()
    {
        const int walkerTicks = 60;
        const int midpointTick = 30;

        var (mission, grid, wallBuckets, initialState) = BuildWalkerFixture();

        var reference = NewSimulation(mission, grid, wallBuckets, initialState);
        var referenceSnapshotsFromMidpoint = new List<TickSnapshot>(walkerTicks - midpointTick);
        var referenceMidpointX = 0L;
        for (var tick = 0; tick < walkerTicks; tick++)
        {
            reference.RunTick(tick);
            if (tick == midpointTick)
            {
                referenceMidpointX = WalkerPositionXRaw(reference);
            }

            if (tick >= midpointTick)
            {
                referenceSnapshotsFromMidpoint.Add(CaptureTick(reference));
            }
        }

        var stopped = NewSimulation(mission, grid, wallBuckets, initialState);
        for (var tick = 0; tick < midpointTick; tick++)
        {
            stopped.RunTick(tick);
        }

        // The premise: by the midpoint the group's request has been published
        // and the operator is already walking a real polyline. Without this,
        // everything below would be a statement about a group that never got
        // a path at all.
        var midpointGroup = Assert.Single(stopped.State.Groups);
        Assert.False(
            midpointGroup.HasOutstandingRequest,
            "the request must already have been published by the midpoint");
        Assert.True(
            WalkerPositionXRaw(stopped) > WalkerStartXRaw,
            "the operator must already be walking the published path by the midpoint");

        var restoredState = stopped.State.ToSnapshot().ToState();

        // The request record is what design section 4 says is authoritative,
        // so it must survive the round trip; the polyline it produces is not
        // in the snapshot at all and must be recomputed from this.
        var restoredGroup = Assert.Single(restoredState.Groups);
        Assert.Equal(midpointGroup, restoredGroup);

        var resumed = NewSimulation(mission, grid, wallBuckets, restoredState);
        for (var tick = midpointTick; tick < walkerTicks; tick++)
        {
            resumed.RunTick(tick);
            AssertTicksAgree(referenceSnapshotsFromMidpoint[tick - midpointTick], resumed, tick);
        }

        // The compared window contains real movement, on both sides, proved
        // here rather than by a probe outside the test — task 52a's deleted
        // fifth test agreed tick for tick across thirty ticks in which
        // nothing moved at all.
        var referenceFinalX = WalkerPositionXRaw(reference);
        var resumedFinalX = WalkerPositionXRaw(resumed);
        var minimumTravelRaw = (walkerTicks - midpointTick) / 2 * WalkerStepRaw;

        Assert.True(
            referenceFinalX - referenceMidpointX >= minimumTravelRaw,
            $"the reference run travelled {referenceFinalX - referenceMidpointX} raw inside the compared " +
            $"window, short of the {minimumTravelRaw} raw this test requires to call the window active");
        Assert.True(
            resumedFinalX - referenceMidpointX >= minimumTravelRaw,
            $"the resumed run travelled {resumedFinalX - referenceMidpointX} raw inside the compared " +
            $"window, so its agreement with the reference is agreement between two runs that stopped");
        Assert.Equal(referenceFinalX, resumedFinalX);
    }

    // ---- The walker fixture ----------------------------------------------

    /// <summary>The walker's spawn X, in raw fixed point: nav cell (0, 0)'s own centre.</summary>
    private const long WalkerStartXRaw = 2L * FixedPoint.Scale;

    /// <summary>
    /// One tick's sprint step, re-derived from design section 4's 80 world
    /// units per second and the shipped tick rate rather than read out of the
    /// production constant this assembly cannot see — the same derivation
    /// <c>TickPipelineTests</c> writes for the same figure.
    /// </summary>
    private static readonly long WalkerStepRaw =
        (80L * FixedPoint.Scale) / SandataRuleset.ModernTacticalV1.TickRate;

    private static long WalkerPositionXRaw(SandataSimulation simulation) =>
        simulation.State.Operators.Single(op => op.EntityId == 1UL).PositionX.RawValue;

    /// <summary>
    /// One operator alone on an open grid, with one group carrying an
    /// outstanding path request from its own cell to a cell far to the east.
    /// Deliberately not the shared <see cref="BuildFixture"/>: see the task 90
    /// test's own remarks for why a dense packing cannot supply a mover that
    /// is still moving inside the compared window.
    /// </summary>
    private static (Mission Mission, NavGrid Grid, WallBuckets WallBuckets, MissionState InitialState)
        BuildWalkerFixture()
    {
        // Wide enough to put the second faction's one operator further from
        // the walker's whole route than ContactMemory.DetectRangeWu, so
        // neither ever senses, shoots at, or stands in front of the other —
        // task 89 measured what a body on the route does to a mover, and
        // this test is about the path surviving a resume, not about that.
        var grid = new NavGrid(width: 100, height: 100);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: Seed,
            mapContentHash: 1UL,
            tickPolicy: new MissionTickPolicy(TickLimit: 100_000, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(FactionId: 0, OperatorCount: 1),
                new MissionFactionSetup(FactionId: 1, OperatorCount: 1)),
            rulesetId: SandataPresetId.ModernTacticalV1);

        var walker = NewOperator(entityId: 1UL, faction: 0, positionXWu: 2, positionYWu: 2);
        var distantEnemy = NewOperator(entityId: 2UL, faction: 1, positionXWu: 394, positionYWu: 394);

        var initialState = new MissionState(
            Tick: 0, Phase: 1, Winner: -1, NextEntityId: 3UL, NextEventSequence: 0)
        {
            Operators = ImmutableArray.Create(walker, distantEnemy),
            FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
            Doors = ImmutableArray<DoorState>.Empty,
            Groups = ImmutableArray.Create(new GroupPathState(
                GroupId: 1UL,
                DestinationCellIndex: grid.CellIndex(35, 0),
                HasOutstandingRequest: true,
                StartCellIndex: grid.CellIndex(0, 0),
                GoalCellIndex: grid.CellIndex(35, 0),
                RequestTick: 0)),
            RngStreams = ImmutableArray<RngStreamState>.Empty,
        };

        return (mission, grid, wallBuckets, initialState);
    }

    private static OperatorState NewOperator(ulong entityId, int faction, int positionXWu, int positionYWu) =>
        new(
            EntityId: entityId,
            PositionX: FixedPoint.FromWhole(positionXWu),
            PositionY: FixedPoint.FromWhole(positionYWu),
            Facing: Facing16.East,
            AimAngle: Bam16.FromFacing16(Facing16.East),
            Health: 100,
            Faction: faction,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: 0,
            WeaponChainRemainingTicks: 0,
            MagazineRounds: 30,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0);
}
