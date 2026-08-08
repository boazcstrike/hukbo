using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Hukbo.Core.Mathematics;
using Hukbo.Diagnostics;
using Sandata.Client.UI;
using Sandata.Core.Events;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 80: <c>SandataGame</c> and <c>PathDrawTool</c> used to call
/// <see cref="OrderQueue.SubmitValidated"/> directly, bypassing
/// <see cref="SandataSimulation.SubmitOrder"/> — the only production door
/// that also folds a rejection into
/// <see cref="MissionEventKind.OrderRejected"/> on
/// <see cref="MissionState.EventFeed"/>. These tests exercise the client's
/// two order-submission call sites and assert the rejection actually reaches
/// the event feed, reached only through the client helper under test — never
/// by calling <see cref="SandataSimulation.SubmitOrder"/> directly — so a
/// regression back to the old direct-queue call would fail these even though
/// <c>OrderQueue.Orders</c> itself would still look correct.
/// </summary>
public sealed class ClientOrderDoorTests
{
    // Same open-grid / one-wall fixture shape PathDrawToolTests.cs and
    // HudComposerTests.cs already each keep their own copy of — task 80 does
    // not own either file, so this is a third, independent copy.
    private static NavGrid NewOpenGrid(int widthCells = 10, int heightCells = 10)
    {
        var grid = new NavGrid(widthCells, heightCells);
        System.Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets OneWall(NavGrid grid, long ax, long ay, long bx, long by) =>
        WallBuckets.Build(grid, [ax], [ay], [bx], [by]);

    private static SandataSimulation NewSimulation(NavGrid grid, WallBuckets wallBuckets)
    {
        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1UL,
            mapContentHash: 1UL,
            tickPolicy: new MissionTickPolicy(TickLimit: 100, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(FactionId: 0, OperatorCount: 1),
                new MissionFactionSetup(FactionId: 1, OperatorCount: 1)),
            rulesetId: SandataPresetId.ModernTacticalV1);

        var initialState = new MissionState(Tick: 0, Phase: 1, Winner: -1, NextEntityId: 1, NextEventSequence: 0);

        return new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState);
    }

    /// <summary>
    /// A drawn path that crosses a wall, submitted through
    /// <see cref="PathDrawTool.Submit"/> — never
    /// <see cref="SandataSimulation.SubmitOrder"/> directly — produces exactly
    /// one <see cref="MissionEventKind.OrderRejected"/> entry on the
    /// simulation's own event feed. Same wall geometry
    /// <c>OrderValidationTests.ValidateMoveAlongPath_SegmentCrossesWall_ReturnsSegmentCrossesWall</c>
    /// uses.
    /// </summary>
    [Fact]
    public void PathDrawToolSubmit_ARejectedPathProducesExactlyOneOrderRejectedEventOnTheFeed()
    {
        var grid = NewOpenGrid();
        var wallBuckets = OneWall(grid, ax: 20, ay: 0, bx: 20, by: 40);
        var simulation = NewSimulation(grid, wallBuckets);

        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(10, 20));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(30, 20));

        var (_, submitted, rejection) = PathDrawTool.Submit(
            state, simulation, targetTick: 3, factionId: 0, ImmutableArray.Create(1UL));

        Assert.Null(submitted);
        Assert.NotNull(rejection);

        var rejectedEvents = simulation.State.EventFeed.Events
            .Where(missionEvent => missionEvent.Kind == MissionEventKind.OrderRejected)
            .ToArray();
        Assert.Single(rejectedEvents);
    }

    /// <summary>
    /// The rejection half of this fact cannot be exercised through
    /// <see cref="SandataGame.ReleaseGoCode"/> by any input: <see cref="Sandata.Core.Orders.OrderQueue.SubmitValidated"/>
    /// (<c>src/Sandata.Core/Orders/OrderQueue.cs:279</c>) only runs
    /// <see cref="OrderValidation"/> against <see cref="OrderKind.MoveAlongPath"/>
    /// — every other <see cref="OrderKind"/>, including
    /// <see cref="OrderKind.GoCodeRelease"/>, is "accepted unconditionally"
    /// (that method's own remarks, line 217). No addressee list, faction id,
    /// or target tick makes a go-code release fail validation, so there is no
    /// input that drives <see cref="SandataGame.ReleaseGoCode"/> to observe a
    /// rejection; asserting one would require faking behavior no production
    /// path produces. This is a discrepancy against the brief's literal ask
    /// ("a rejected drawn-path submission ... through
    /// SandataGame.ReleaseGoCode"), reported here rather than worked around.
    /// </summary>
    /// <remarks>
    /// What this test proves instead, the same way
    /// <c>SandataSourceHygieneTests.SandataHeadlessProjectIsWiredToTheDiagnosticsAssembly</c>
    /// proves a fact runtime behavior cannot yet demonstrate — by reading the
    /// source directly: <see cref="SandataGame.ReleaseGoCode"/>'s
    /// implementation calls <c>simulation.SubmitOrder</c>, the one production
    /// door that folds a rejection into
    /// <see cref="MissionEventKind.OrderRejected"/>, and never calls
    /// <c>OrderQueue.SubmitValidated</c> or any other bypass directly. If a
    /// future change to <c>OrderValidation</c> ever makes a
    /// <see cref="OrderKind.GoCodeRelease"/> rejection possible,
    /// <see cref="SandataGame.ReleaseGoCode"/> is already wired to surface it
    /// on the feed without any further change, because it goes through the
    /// same shared door <see cref="PathDrawToolSubmit_ARejectedPathProducesExactlyOneOrderRejectedEventOnTheFeed"/>
    /// already proves emits the event.
    /// </remarks>
    [Fact]
    public void SandataGameReleaseGoCode_CallsTheSharedSimulationSubmitOrderDoorNotARawQueueBypass()
    {
        var root = GetRepositoryRoot();
        var sourcePath = Path.Combine(root, "src", "Sandata.Client", "SandataGame.cs");
        var lines = File.ReadAllLines(sourcePath);

        // The declaration itself spans several lines (a multi-line tuple
        // return type), so the anchor is the line where that return type
        // closes and the method name and parameter list begin — the only
        // line in the file matching this exact shape.
        var methodStart = Array.FindIndex(
            lines, line => line.Contains("OrderQueueEntries) ReleaseGoCode(", StringComparison.Ordinal));
        Assert.True(methodStart >= 0, "Could not locate SandataGame.ReleaseGoCode's declaration.");

        var methodBody = string.Join('\n', lines.Skip(methodStart).Take(40));
        Assert.Contains("simulation.SubmitOrder(", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("queue.SubmitValidated", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain(".Orders.Add(", methodBody, StringComparison.Ordinal);

        // The accepted case still exercises ReleaseGoCode's real call site
        // against a live simulation and event feed, even though it cannot be
        // rejected: proves the method runs to completion through the shared
        // door and produces zero spurious rejected events on the honest,
        // reachable path.
        var grid = NewOpenGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var simulation = NewSimulation(grid, wallBuckets);

        var (_, orderQueueEntries) = SandataGame.ReleaseGoCode(
            letter: 'A',
            addressees: ImmutableArray.Create(1UL),
            targetTick: 1,
            factionId: 0,
            simulation,
            existingGoCodeEntries: ImmutableArray<GoCodePanel.GoCodeEntry>.Empty,
            existingOrderQueueEntries: ImmutableArray<OrderQueueView.Entry>.Empty);

        var entry = Assert.Single(orderQueueEntries);
        Assert.False(entry.IsRejected);
        Assert.Empty(simulation.State.EventFeed.Events
            .Where(missionEvent => missionEvent.Kind == MissionEventKind.OrderRejected));
    }

    /// <summary>
    /// Startup map load, proven without ever constructing a window or
    /// <see cref="SandataGame"/>: the fixture
    /// <c>Sandata.Client.csproj</c> links at <c>Maps/angle-house.hkmap</c>
    /// tokenizes and validates to a non-empty record set carrying four
    /// <see cref="SpawnRecord"/> entries (two per faction), and a
    /// <see cref="SandataSimulation"/> built from those spawns — the same
    /// shape <c>SandataGame</c>'s constructor builds via its own private
    /// <c>BuildInitialState</c>, reimplemented here because that method is
    /// <see langword="private"/> and this task's file list does not include
    /// widening its accessibility — carries that same spawn count as its
    /// initial operator roster.
    /// </summary>
    [Fact]
    public void StartupMapFixture_ParsesToTheExpectedNonEmptySpawnSetAndSeedsTheSimulation()
    {
        var root = GetRepositoryRoot();
        var fixturePath = Path.Combine(
            root, "tests", "Sandata.Core.Tests", "Fixtures", "angle-house.hkmap");
        var mapText = File.ReadAllText(fixturePath);

        var records = MapTokenizer.Tokenize(mapText);
        MapValidator.Validate(records);

        Assert.NotEmpty(records);

        var spawns = records.OfType<SpawnRecord>().ToArray();
        Assert.Equal(4, spawns.Length);
        Assert.Equal(2, spawns.Count(spawn => spawn.Faction == 0));
        Assert.Equal(2, spawns.Count(spawn => spawn.Faction == 1));

        var grid = NewOpenGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1UL,
            mapContentHash: 1UL,
            tickPolicy: new MissionTickPolicy(TickLimit: 100, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(FactionId: 0, OperatorCount: spawns.Count(spawn => spawn.Faction == 0)),
                new MissionFactionSetup(FactionId: 1, OperatorCount: spawns.Count(spawn => spawn.Faction == 1))),
            rulesetId: SandataPresetId.ModernTacticalV1);
        var initialState = BuildInitialStateFromSpawns(spawns);
        var simulation = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState);

        Assert.Equal(spawns.Length, simulation.State.Operators.Length);
    }

    // Reimplements SandataGame.BuildInitialState's shape (that method is
    // private) so this test can assert the constructed simulation actually
    // carries the fixture's real spawn data rather than only the parsed
    // record count.
    private static MissionState BuildInitialStateFromSpawns(IReadOnlyList<SpawnRecord> spawns)
    {
        var operators = ImmutableArray.CreateBuilder<OperatorState>(spawns.Count);
        for (var index = 0; index < spawns.Count; index++)
        {
            var spawn = spawns[index];
            var rawFacing = new Bam16((ushort)spawn.FacingBam);

            operators.Add(new OperatorState(
                EntityId: (ulong)(index + 1),
                PositionX: FixedPoint.FromWhole(spawn.X),
                PositionY: FixedPoint.FromWhole(spawn.Y),
                Facing: rawFacing.ToFacing16(),
                AimAngle: rawFacing,
                Health: 100,
                Faction: spawn.Faction,
                Intent: 0,
                IsCrouched: false,
                WeaponLowered: false,
                WeaponChainPhase: 0,
                WeaponChainRemainingTicks: 0,
                MagazineRounds: 30,
                CyclicFireAccumulator: 0,
                SuppressionCounter: 0));
        }

        return new MissionState(
            Tick: 0, Phase: 1, Winner: -1, NextEntityId: (ulong)(spawns.Count + 1), NextEventSequence: 0)
        {
            Operators = operators.MoveToImmutable(),
            FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
            Doors = ImmutableArray<DoorState>.Empty,
            Groups = ImmutableArray<GroupPathState>.Empty,
            RngStreams = ImmutableArray<RngStreamState>.Empty,
        };
    }

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so the fixture cannot be located.");
        return root!;
    }
}
