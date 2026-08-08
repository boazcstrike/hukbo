using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using Sandata.Client;
using Sandata.Client.Rendering;
using Sandata.Client.UI;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 69's own done-when bar for <see cref="HudComposer"/>: the composed
/// rectangles do not overlap at three window sizes, every element from design
/// section 11's HUD list is present exactly once, and the composer degrades
/// sanely at the smallest supported window rather than producing negative or
/// inverted rectangles. No member of this class constructs a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a <c>SandataGame</c>.
/// </summary>
public sealed class HudComposerTests
{
    // Placeholder inputs this test hands to HudComposer.Compose. None of
    // these are pinned by design or plan; they are this test's own stand-ins
    // for values a running battle would supply — see SandataGame.cs's own
    // PLACEHOLDERS section for the equivalent values it uses at runtime.
    private const int PlaceholderOperatorCount = 4;
    private const int PlaceholderContactCount = 5;

    // Task 71's own placeholder inputs, same convention as the two constants
    // immediately above: non-zero so every clamp path GoCodePanel.CalculateBounds
    // and OrderQueueView.CalculateBounds define actually executes.
    private const int PlaceholderGoCodeCount = 3;
    private const int PlaceholderOrderQueueEntryCount = 2;

    private static readonly NavGrid PlaceholderMinimapGrid = new(width: 20, height: 15);

    // Design section 11's HUD element list, transcribed verbatim from that
    // table's own "Element" column, in the table's own row order — including
    // task 71's amended "Go-code panel" and "Order queue view" rows. A future
    // design edit that adds, removes, or renames a row must edit this array
    // to match — until it does, this test is pinned against today's design,
    // not merely against whatever HudComposer.Compose happens to return.
    private static readonly string[] DesignSection11HudElements =
    [
        "Roster strip",
        "Contact list",
        "Alert indicator",
        "Mission clock and tick counter",
        "Event log",
        "Operator inspector",
        "Go-code panel",
        "Order queue view",
        "Spectator control bar",
        "Fire cone overlay",
        "Order path overlay",
        "Breach-point marker",
        "Minimap",
        "Multi-select marquee",
        "Undo stack",
    ];

    private static HudComposer.Layout ComposeAt(int windowWidth, int windowHeight) =>
        HudComposer.Compose(
            new Rectangle(0, 0, windowWidth, windowHeight),
            PlaceholderOperatorCount,
            PlaceholderContactCount,
            PlaceholderMinimapGrid,
            PlaceholderGoCodeCount,
            PlaceholderOrderQueueEntryCount);

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void Compose_ProducesEveryDesignSection11ElementExactlyOnce(int windowWidth, int windowHeight)
    {
        var layout = ComposeAt(windowWidth, windowHeight);
        var namedElements = HudComposer.ToNamedElements(layout);

        var producedNames = namedElements.Select(e => e.Element).ToList();

        Assert.Equal(DesignSection11HudElements.Length, namedElements.Count);
        Assert.Equal(
            DesignSection11HudElements.OrderBy(n => n, System.StringComparer.Ordinal),
            producedNames.OrderBy(n => n, System.StringComparer.Ordinal));
        // "Exactly once": no duplicate element names in the composed set.
        Assert.Equal(producedNames.Count, producedNames.Distinct().Count());
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void Compose_RectanglesDoNotOverlapAtThreeWindowSizes(int windowWidth, int windowHeight)
    {
        var layout = ComposeAt(windowWidth, windowHeight);
        var rectangles = HudComposer.ToNamedElements(layout).ToList();

        for (var i = 0; i < rectangles.Count; i++)
        {
            for (var j = i + 1; j < rectangles.Count; j++)
            {
                var (nameA, boundsA) = rectangles[i];
                var (nameB, boundsB) = rectangles[j];

                Assert.False(
                    boundsA.Intersects(boundsB),
                    $"'{nameA}' {boundsA} overlaps '{nameB}' {boundsB} at {windowWidth}x{windowHeight}.");
            }
        }
    }

    [Fact]
    public void Compose_DegradesSanelyAtTheSmallestSupportedWindow()
    {
        // This repository defines no named "smallest supported window"
        // constant anywhere in Sandata.Client or the design/plan docs. 320x180
        // is this test's own placeholder floor, deliberately far below every
        // panel's preferred size so every clamp path in every task-38/46
        // helper this composer calls actually executes.
        const int smallestSupportedWidth = 320;
        const int smallestSupportedHeight = 180;

        var layout = ComposeAt(smallestSupportedWidth, smallestSupportedHeight);
        var rectangles = HudComposer.ToNamedElements(layout);

        foreach (var (name, bounds) in rectangles)
        {
            Assert.True(bounds.Width >= 0, $"'{name}' produced a negative width: {bounds}.");
            Assert.True(bounds.Height >= 0, $"'{name}' produced a negative height: {bounds}.");
        }
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void Compose_GoCodePanelAndOrderQueueViewAreBothNonEmptyRectanglesAtThreeWindowSizes(
        int windowWidth, int windowHeight)
    {
        var layout = ComposeAt(windowWidth, windowHeight);

        Assert.True(layout.GoCodePanel.Width > 0, $"GoCodePanel produced a non-positive width: {layout.GoCodePanel}.");
        Assert.True(layout.GoCodePanel.Height > 0, $"GoCodePanel produced a non-positive height: {layout.GoCodePanel}.");
        Assert.True(layout.OrderQueueView.Width > 0, $"OrderQueueView produced a non-positive width: {layout.OrderQueueView}.");
        Assert.True(layout.OrderQueueView.Height > 0, $"OrderQueueView produced a non-positive height: {layout.OrderQueueView}.");
    }

    [Fact]
    public void ToNamedElements_MatchesDesignSection11HudElementListExactly()
    {
        var layout = ComposeAt(1280, 720);
        var producedNames = HudComposer.ToNamedElements(layout).Select(e => e.Element).ToList();

        // Order-sensitive: catches a row silently dropped or renamed even if
        // the count still matches.
        Assert.Equal((IReadOnlyList<string>)DesignSection11HudElements, producedNames);
    }

    // ---- shared fixtures for the three acceptance tests below, matching
    // PathDrawToolTests.cs's own NewOpenGrid/NoWalls fixtures (that file is
    // task 62's and is not this task's to edit, so these are this file's own
    // copies of the same open-grid, no-walls shape). ----

    private static NavGrid NewOpenGrid(int widthCells = 20, int heightCells = 20)
    {
        var grid = new NavGrid(widthCells, heightCells);
        System.Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    // Matches OrderValidationTests.OneWall — a single wall segment, used
    // below to build a genuinely rejectable path (SegmentCrossesWall) rather
    // than only exercising the accepted case.
    private static WallBuckets OneWall(NavGrid grid, long ax, long ay, long bx, long by) =>
        WallBuckets.Build(grid, [ax], [ay], [bx], [by]);

    // Task 80's own copy of PathDrawToolTests.NewSimulation — that file is
    // not this task's to edit either, so this is this file's own fixture for
    // the SandataSimulation ReleaseGoCode/SubmitDrawnPath now require in
    // place of a bare OrderQueue.
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

        return new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState, ImmutableArray<CoverRecord>.Empty);
    }

    [Fact]
    public void TryAddPathNode_APointerDownInsideAnyComposedPanelNeverBecomesAPathNode()
    {
        var layout = ComposeAt(1280, 720);
        var panelBounds = new[]
        {
            layout.RosterStrip,
            layout.ContactList,
            layout.AlertIndicator,
            layout.MissionClock,
            layout.EventLog,
            layout.OperatorInspector,
            layout.GoCodePanel,
            layout.OrderQueueView,
            layout.ControlBar,
            layout.Minimap,
        };

        var pointInsideGoCodePanel = new Point(layout.GoCodePanel.Center.X, layout.GoCodePanel.Center.Y);
        var state = PathDrawState.CreateEmpty();

        var result = SandataGame.TryAddPathNode(
            state, pointInsideGoCodePanel, new Vector2(1, 1), panelBounds);

        Assert.Empty(result.Nodes);
        Assert.True(SandataGame.IsPointerOverAnyPanel(pointInsideGoCodePanel, panelBounds));
    }

    [Fact]
    public void ToOrderPathWaypointsWu_ADrawnPathReachesTheOrderPathOverlay()
    {
        var nodes = ImmutableArray.Create(
            new DrawnPathNode(10, 20),
            new DrawnPathNode(30, 40));

        var waypointsWu = SandataGame.ToOrderPathWaypointsWu(nodes);

        Assert.Equal(2, waypointsWu.Length);
        Assert.Equal((10f, 20f), (waypointsWu[0].X, waypointsWu[0].Y));
        Assert.Equal((30f, 40f), (waypointsWu[1].X, waypointsWu[1].Y));

        var worldSegments = OrderPathOverlay.CreateWorldSegments(waypointsWu);
        Assert.NotEmpty(worldSegments);
    }

    [Fact]
    public void ReleaseGoCode_SubmitsAnOrderCarryingItsOwnTargetTick()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var simulation = NewSimulation(grid, wallBuckets);
        var addressees = ImmutableArray.Create(1UL, 2UL);
        const long targetTick = 42;

        var (goCodeEntries, orderQueueEntries) = SandataGame.ReleaseGoCode(
            letter: 'A',
            addressees,
            targetTick,
            factionId: 0,
            simulation,
            existingGoCodeEntries: ImmutableArray<GoCodePanel.GoCodeEntry>.Empty,
            existingOrderQueueEntries: ImmutableArray<OrderQueueView.Entry>.Empty);

        var submittedOrder = Assert.Single(simulation.State.OrderQueue.Orders);
        Assert.Equal(OrderKind.GoCodeRelease, submittedOrder.Kind);
        Assert.Equal(targetTick, submittedOrder.TargetTick);

        var goCodeEntry = Assert.Single(goCodeEntries);
        Assert.Equal('A', goCodeEntry.Letter);
        Assert.True(goCodeEntry.IsReleased);

        var orderQueueEntry = Assert.Single(orderQueueEntries);
        Assert.Equal(submittedOrder.OrderId, orderQueueEntry.OrderId);
        Assert.Equal(targetTick, orderQueueEntry.TargetTick);
        Assert.False(orderQueueEntry.IsRejected);
    }

    // ==================== Task 73: SubmitDrawnPath ====================

    private static PathDrawState TwoNodeDrawnPath()
    {
        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(10, 20));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(30, 20));
        return state;
    }

    /// <summary>
    /// A drawn path submitted with a non-empty, deliberately unsorted
    /// selection produces exactly one <see cref="OrderKind.MoveAlongPath"/>
    /// order whose addressees come back ascending. Built unsorted rather than
    /// pre-sorted: an implementation that passed the selection through
    /// verbatim, bypassing <see cref="OrderQueue"/>'s own sort, would fail
    /// the ascending assertion below.
    /// </summary>
    [Fact]
    public void SubmitDrawnPath_ANonEmptySelectionSubmitsExactlyOneOrderWithAscendingAddressees()
    {
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var simulation = NewSimulation(grid, wallBuckets);
        var state = TwoNodeDrawnPath();
        var unsortedAddressees = ImmutableArray.Create<ulong>(30, 5, 100, 2, 17);

        var (postSubmitState, orderQueueEntries) = SandataGame.SubmitDrawnPath(
            state,
            unsortedAddressees,
            targetTick: 7,
            factionId: 0,
            simulation,
            ImmutableArray<OrderQueueView.Entry>.Empty);

        var storedOrder = Assert.Single(simulation.State.OrderQueue.Orders);
        Assert.Equal(OrderKind.MoveAlongPath, storedOrder.Kind);
        Assert.Equal(new ulong[] { 2, 5, 17, 30, 100 }, storedOrder.Addressees.ToArray());

        var orderQueueEntry = Assert.Single(orderQueueEntries);
        Assert.False(orderQueueEntry.IsRejected);
        Assert.Equal(storedOrder.OrderId, orderQueueEntry.OrderId);

        // Post-submission state, on the accepted path: a fresh, empty drawn
        // path, so the next frame does not render a phantom polyline.
        Assert.Empty(postSubmitState.Nodes);
    }

    /// <summary>
    /// An empty selection submits nothing at all — not an order with no
    /// addressees. The queue comes back byte-for-byte unchanged (same order
    /// count, same <see cref="OrderQueue.NextOrderId"/>, same
    /// <see cref="OrderQueue.NextOrderSequence"/>) and the drawn path is left
    /// exactly as it was, so a spectator who pressed submit before finishing
    /// a selection keeps the path they already drew. A naive implementation
    /// that submits an addressee-less order anyway would still pass a bare
    /// order-count check but fails the counter assertions below, since
    /// <see cref="OrderQueue.SubmitValidated"/> always advances both counters
    /// for anything it actually stores.
    /// </summary>
    [Fact]
    public void SubmitDrawnPath_AnEmptySelectionSubmitsNothing()
    {
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var simulation = NewSimulation(grid, wallBuckets);
        var state = TwoNodeDrawnPath();

        // A non-empty starting queue, so "unchanged" is a meaningful claim
        // rather than trivially true of an empty OrderQueue.
        var (_, _) = SandataGame.ReleaseGoCode(
            'A',
            ImmutableArray.Create(1UL),
            targetTick: 1,
            factionId: 0,
            simulation,
            ImmutableArray<GoCodePanel.GoCodeEntry>.Empty,
            ImmutableArray<OrderQueueView.Entry>.Empty);
        var queueBefore = simulation.State.OrderQueue;
        var existingOrderQueueEntries = ImmutableArray.Create(
            OrderQueueView.FromSubmittedOrder(queueBefore.Orders[0]));

        var (postSubmitState, orderQueueEntriesAfter) = SandataGame.SubmitDrawnPath(
            state,
            ImmutableArray<ulong>.Empty,
            targetTick: 7,
            factionId: 0,
            simulation,
            existingOrderQueueEntries);

        var queueAfter = simulation.State.OrderQueue;
        Assert.Equal(queueBefore.Orders.Length, queueAfter.Orders.Length);
        Assert.Equal(queueBefore.NextOrderId, queueAfter.NextOrderId);
        Assert.Equal(queueBefore.NextOrderSequence, queueAfter.NextOrderSequence);
        Assert.Equal(existingOrderQueueEntries.Length, orderQueueEntriesAfter.Length);

        // The drawn path is untouched, not reset — nothing was submitted.
        Assert.Equal(state.Nodes.ToArray(), postSubmitState.Nodes.ToArray());
    }

    /// <summary>
    /// A path that crosses a wall is genuinely rejected by
    /// <see cref="OrderValidation.ValidateMoveAlongPath"/>'s
    /// <see cref="OrderRejectReason.SegmentCrossesWall"/> rule, and the
    /// specific reason code — not merely a generic rejection marker — is
    /// retrievable from the order queue view's entry. Same wall geometry
    /// <c>OrderValidationTests.ValidateMoveAlongPath_SegmentCrossesWall_ReturnsSegmentCrossesWall</c>
    /// uses: a vertical wall at x = 20 spanning the grid, crossed by a
    /// horizontal path segment at y = 20.
    /// </summary>
    [Fact]
    public void SubmitDrawnPath_ARejectedSubmissionSurfacesItsReasonCodeInTheOrderQueueView()
    {
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = OneWall(grid, ax: 20, ay: 0, bx: 20, by: 40);
        var simulation = NewSimulation(grid, wallBuckets);
        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(10, 20));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(30, 20));

        var (postSubmitState, orderQueueEntries) = SandataGame.SubmitDrawnPath(
            state,
            ImmutableArray.Create(1UL),
            targetTick: 3,
            factionId: 0,
            simulation,
            ImmutableArray<OrderQueueView.Entry>.Empty);

        Assert.Empty(simulation.State.OrderQueue.Orders);

        var entry = Assert.Single(orderQueueEntries);
        Assert.True(entry.IsRejected);
        Assert.Equal(OrderRejectReason.SegmentCrossesWall, entry.RejectReason);

        // Post-submission state, on the rejected path too: PathDrawTool.Submit
        // resets on either outcome, so a rejected drawing does not linger.
        Assert.Empty(postSubmitState.Nodes);
    }
}
