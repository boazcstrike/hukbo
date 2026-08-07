using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using Sandata.Client;
using Sandata.Client.Rendering;
using Sandata.Client.UI;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;

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
        var addressees = ImmutableArray.Create(1UL, 2UL);
        const long targetTick = 42;

        var (queue, goCodeEntries, orderQueueEntries) = SandataGame.ReleaseGoCode(
            letter: 'A',
            addressees,
            targetTick,
            factionId: 0,
            OrderQueue.Empty,
            grid,
            wallBuckets,
            existingGoCodeEntries: ImmutableArray<GoCodePanel.GoCodeEntry>.Empty,
            existingOrderQueueEntries: ImmutableArray<OrderQueueView.Entry>.Empty);

        var submittedOrder = Assert.Single(queue.Orders);
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
}
