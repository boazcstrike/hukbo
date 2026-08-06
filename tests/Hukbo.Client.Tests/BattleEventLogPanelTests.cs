using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiScaleContextCollectionDefinition
{
    public const string Name = "UI scale context";
}

[Collection(UiScaleContextCollectionDefinition.Name)]
public sealed class BattleEventLogPanelTests
{
    [Theory]
    [InlineData(848, 68, 420, 640)]
    [InlineData(521, 68, 267, 372)]
    public void CalculateLayout_KeepsInspectorRegionsInsidePanel(
        int left,
        int top,
        int width,
        int height)
    {
        var bounds = new Rectangle(left, top, width, height);

        var layout = BattleEventLogPanel.CalculateLayout(bounds);

        AssertInside(bounds, layout.HeaderBounds);
        AssertInside(bounds, layout.KindFilterBounds);
        AssertInside(bounds, layout.FactionFilterBounds);
        AssertInside(bounds, layout.ActorFilterBounds);
        AssertInside(bounds, layout.SearchBounds);
        AssertInside(bounds, layout.ResetBounds);
        AssertInside(bounds, layout.ListBounds);
        AssertInside(bounds, layout.RowsBounds);
        AssertInside(bounds, layout.ScrollbarTrackBounds);
        AssertInside(bounds, layout.DetailsBounds);
        Assert.True(layout.RowsBounds.Bottom <= layout.DetailsBounds.Top);
    }

    [Fact]
    public void CalculateLayout_ActorFilterHasDistinctHitTarget()
    {
        var layout = BattleEventLogPanel.CalculateLayout(
            new Rectangle(848, 68, 420, 640));

        Assert.Equal(
            BattleEventFilterTarget.Actor,
            BattleEventLogPanel.HitTestFilter(
                layout,
                layout.ActorFilterBounds.Center));
        Assert.False(
            layout.KindFilterBounds.Intersects(
                layout.ActorFilterBounds));
        Assert.False(
            layout.FactionFilterBounds.Intersects(
                layout.ActorFilterBounds));
    }

    [Fact]
    public void TryGetActorFilterForClick_UsesSelectedEventOrClearsActiveFilter()
    {
        var selected = CreateEvent(sequence: 1, actorId: 7);

        Assert.True(
            BattleEventLogPanel.TryGetActorFilterForClick(
                activeActorFilter: null,
                selected,
                out var selectedActorFilter));
        Assert.Equal(7UL, selectedActorFilter);
        Assert.True(
            BattleEventLogPanel.TryGetActorFilterForClick(
                activeActorFilter: 7,
                selectedEvent: null,
                out var clearedActorFilter));
        Assert.Null(clearedActorFilter);
    }

    [Fact]
    public void TryGetActorFilterForClick_WithoutSelectionIsInert()
    {
        Assert.False(
            BattleEventLogPanel.TryGetActorFilterForClick(
                activeActorFilter: null,
                selectedEvent: null,
                out var actorFilter));

        Assert.Null(actorFilter);
    }

    [Fact]
    public void GetActorFilterLabel_DescribesSelectionDrivenAction()
    {
        var selected = CreateEvent(sequence: 1, actorId: 7);

        Assert.Equal(
            "SELECT ROW",
            BattleEventLogPanel.GetActorFilterLabel(
                actorId: null,
                selectedEvent: null));
        Assert.Equal(
            "FILTER #7",
            BattleEventLogPanel.GetActorFilterLabel(
                actorId: null,
                selected));
        Assert.Equal(
            "CLEAR #7",
            BattleEventLogPanel.GetActorFilterLabel(
                actorId: 7,
                selectedEvent: null));
    }

    [Fact]
    public void IsActorFilterControlEnabled_RequiresSelectionUnlessActive()
    {
        var selected = CreateEvent(sequence: 1, actorId: 7);

        Assert.False(
            BattleEventLogPanel.IsActorFilterControlEnabled(
                actorId: null,
                selectedEvent: null));
        Assert.True(
            BattleEventLogPanel.IsActorFilterControlEnabled(
                actorId: null,
                selected));
        Assert.True(
            BattleEventLogPanel.IsActorFilterControlEnabled(
                actorId: 7,
                selectedEvent: null));
    }

    [Fact]
    public void CalculateLayout_AllDetailLinesFitAtSmallestSupportedSize()
    {
        var layout = BattleEventLogPanel.CalculateLayout(
            new Rectangle(521, 68, 267, 372));

        for (var index = 0;
             index < BattleEventLogPanel.DetailLineCount;
             index++)
        {
            AssertInside(
                layout.DetailsBounds,
                BattleEventLogPanel.GetDetailLineBounds(layout, index));
        }
    }

    [Fact]
    public void CalculateLayout_ScalesChromeAndRowsAtTwoHundredPercent()
    {
        WithScale(
            UiScale.Percent200,
            () =>
            {
                var bounds = new Rectangle(100, 100, 840, 1280);
                var layout = BattleEventLogPanel.CalculateLayout(bounds);
                var firstDetail =
                    BattleEventLogPanel.GetDetailLineBounds(layout, 0);

                Assert.Equal(60, BattleEventLogPanel.RowHeight);
                Assert.Equal(36, BattleEventLogPanel.MinimumThumbHeight);
                Assert.Equal(20, layout.HeaderBounds.Left - bounds.Left);
                Assert.Equal(70, layout.HeaderBounds.Height);
                Assert.Equal(52, layout.KindFilterBounds.Height);
                Assert.Equal(18, firstDetail.Left - layout.DetailsBounds.Left);
                Assert.Equal(48, firstDetail.Height);
            });
    }

    [Fact]
    public void GetKeyboardFocusTarget_UsesListOrSearchAndNeverLatest()
    {
        var layout = BattleEventLogPanel.CalculateLayout(
            new Rectangle(848, 68, 420, 640));

        Assert.Equal(
            BattleEventKeyboardFocusTarget.Search,
            BattleEventLogPanel.GetKeyboardFocusTarget(
                layout,
                layout.SearchBounds.Center));
        Assert.Equal(
            BattleEventKeyboardFocusTarget.List,
            BattleEventLogPanel.GetKeyboardFocusTarget(
                layout,
                layout.RowsBounds.Center));
        Assert.Equal(
            BattleEventKeyboardFocusTarget.List,
            BattleEventLogPanel.GetKeyboardFocusTarget(
                layout,
                layout.LatestBounds.Center));
    }

    [Fact]
    public void HitTestVisibleRow_MapsOnlyRowsInsideVisibleList()
    {
        var layout = BattleEventLogPanel.CalculateLayout(
            new Rectangle(848, 68, 420, 640));
        var firstRow = new Point(
            layout.RowsBounds.Left + 5,
            layout.RowsBounds.Top + 5);

        Assert.Equal(
            0,
            BattleEventLogPanel.HitTestVisibleRow(
                layout,
                firstRow,
                visibleEntryCount: 3));
        Assert.Equal(
            -1,
            BattleEventLogPanel.HitTestVisibleRow(
                layout,
                new Point(firstRow.X, layout.RowsBounds.Top - 1),
                visibleEntryCount: 3));
        Assert.Equal(
            -1,
            BattleEventLogPanel.HitTestVisibleRow(
                layout,
                new Point(
                    firstRow.X,
                    layout.RowsBounds.Top +
                    (BattleEventLogPanel.RowHeight * 3) +
                    1),
                visibleEntryCount: 3));
    }

    [Fact]
    public void GetScrollbarThumb_ReflectsVisibleRangeAndStaysUsable()
    {
        var layout = BattleEventLogPanel.CalculateLayout(
            new Rectangle(848, 68, 420, 640));

        var oldest = BattleEventLogPanel.GetScrollbarThumb(
            layout.ScrollbarTrackBounds,
            totalEntryCount: 200,
            visibleRowCount: 8,
            scrollStart: 0);
        var newest = BattleEventLogPanel.GetScrollbarThumb(
            layout.ScrollbarTrackBounds,
            totalEntryCount: 200,
            visibleRowCount: 8,
            scrollStart: 192);

        Assert.True(oldest.Height >= BattleEventLogPanel.MinimumThumbHeight);
        Assert.Equal(layout.ScrollbarTrackBounds.Top, oldest.Top);
        Assert.Equal(layout.ScrollbarTrackBounds.Bottom, newest.Bottom);
        AssertInside(layout.ScrollbarTrackBounds, oldest);
        AssertInside(layout.ScrollbarTrackBounds, newest);
    }

    [Theory]
    [InlineData(0, 0, (int)BattleEventPanelState.NoEvents)]
    [InlineData(3, 0, (int)BattleEventPanelState.NoMatches)]
    [InlineData(3, 2, (int)BattleEventPanelState.Events)]
    public void GetPanelState_DistinguishesEmptyAndFilteredModes(
        int retainedCount,
        int filteredCount,
        int expected)
    {
        Assert.Equal(
            (BattleEventPanelState)expected,
            BattleEventLogPanel.GetPanelState(
                retainedCount,
                filteredCount));
    }

    [Fact]
    public void ShouldReleaseKeyboardFocus_OnlyOnOutsideClick()
    {
        var bounds = new Rectangle(848, 68, 420, 640);

        Assert.False(
            BattleEventLogPanel.ShouldReleaseKeyboardFocus(
                wasLeftMousePressed: true,
                new Point(900, 100),
                bounds));
        Assert.True(
            BattleEventLogPanel.ShouldReleaseKeyboardFocus(
                wasLeftMousePressed: true,
                new Point(400, 100),
                bounds));
        Assert.False(
            BattleEventLogPanel.ShouldReleaseKeyboardFocus(
                wasLeftMousePressed: false,
                new Point(400, 100),
                bounds));
    }

    [Fact]
    public void GetRowEmphasis_ReturnsZeroAtOrBelowThreshold()
    {
        Assert.Equal(
            0f,
            BattleEventLogPanel.GetRowEmphasis(
                sequence: 5,
                emphasisThreshold: 5,
                pulseAmount: 0.75f));
        Assert.Equal(
            0f,
            BattleEventLogPanel.GetRowEmphasis(
                sequence: 4,
                emphasisThreshold: 5,
                pulseAmount: 0.75f));
    }

    [Fact]
    public void GetRowEmphasis_ReturnsPulseAmountAboveThreshold()
    {
        Assert.Equal(
            0.75f,
            BattleEventLogPanel.GetRowEmphasis(
                sequence: 6,
                emphasisThreshold: 5,
                pulseAmount: 0.75f));
    }

    [Fact]
    public void GetRowEmphasis_ReturnsZeroWhenThresholdIsNull()
    {
        Assert.Equal(
            0f,
            BattleEventLogPanel.GetRowEmphasis(
                sequence: 1,
                emphasisThreshold: null,
                pulseAmount: 1f));
    }

    private static void AssertInside(Rectangle outer, Rectangle inner)
    {
        Assert.True(inner.Width >= 0);
        Assert.True(inner.Height >= 0);
        Assert.True(inner.Left >= outer.Left);
        Assert.True(inner.Top >= outer.Top);
        Assert.True(inner.Right <= outer.Right);
        Assert.True(inner.Bottom <= outer.Bottom);
    }

    private static void WithScale(UiScale scale, Action assertion)
    {
        try
        {
            UiScaleContext.Set(scale);
            assertion();
        }
        finally
        {
            UiScaleContext.Set(UiScale.Percent100);
        }
    }

    private static Hukbo.Core.Simulation.BattleEvent CreateEvent(
        long sequence,
        ulong actorId) =>
        Hukbo.Core.Simulation.BattleEvent.NonAttack(
            sequence,
            tick: sequence,
            Hukbo.Core.Simulation.BattleEventKind.Move,
            sourceEntityId: actorId,
            targetEntityId: null,
            value: 0,
            factionId: null);
}
