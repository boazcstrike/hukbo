using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class BattleReportLayoutTests
{
    [Theory]
    [InlineData(0, 0, 1600, 1000)]
    [InlineData(0, 0, 560, 400)]
    [InlineData(0, 0, 300, 200)]
    public void Calculate_KeepsEveryRegionInsidePanelBounds(
        int left,
        int top,
        int width,
        int height)
    {
        var arenaContentBounds = new Rectangle(left, top, width, height);

        var layout = BattleReportLayout.Calculate(arenaContentBounds);

        AssertInside(arenaContentBounds, layout.Bounds);
        AssertInside(layout.Bounds, layout.HeaderBounds);
        AssertInside(layout.Bounds, layout.CloseButtonBounds);
        AssertInside(layout.Bounds, layout.FactionTotalsBounds);
        AssertInside(layout.Bounds, layout.HighlightsBounds);
        AssertInside(layout.Bounds, layout.LeaderboardHeaderBounds);
        AssertInside(layout.Bounds, layout.LeaderboardListBounds);
        AssertInside(layout.Bounds, layout.ScrollbarBounds);
    }

    [Theory]
    [InlineData(0, 0, 1600, 1000)]
    [InlineData(0, 0, 560, 400)]
    public void Calculate_StacksSectionsTopToBottomWithoutOverlap(
        int left,
        int top,
        int width,
        int height)
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(left, top, width, height));

        Assert.True(layout.HeaderBounds.Bottom <= layout.FactionTotalsBounds.Top);
        Assert.True(layout.FactionTotalsBounds.Bottom <= layout.HighlightsBounds.Top);
        Assert.True(layout.HighlightsBounds.Bottom <= layout.LeaderboardHeaderBounds.Top);
        Assert.True(layout.LeaderboardHeaderBounds.Bottom <= layout.LeaderboardListBounds.Top);
    }

    [Theory]
    [InlineData(0, 0, 1600, 1000)]
    [InlineData(0, 0, 560, 400)]
    public void Calculate_HeaderAndCloseButtonShareTheTopRowWithoutOverlap(
        int left,
        int top,
        int width,
        int height)
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(left, top, width, height));

        Assert.False(layout.HeaderBounds.Intersects(layout.CloseButtonBounds));
        Assert.True(layout.HeaderBounds.Right <= layout.CloseButtonBounds.Left);
    }

    [Theory]
    [InlineData(0, 0, 1600, 1000)]
    [InlineData(0, 0, 560, 400)]
    public void Calculate_LeaderboardListAndScrollbarShareARowWithoutOverlap(
        int left,
        int top,
        int width,
        int height)
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(left, top, width, height));

        Assert.False(layout.LeaderboardListBounds.Intersects(layout.ScrollbarBounds));
        Assert.True(layout.LeaderboardListBounds.Right <= layout.ScrollbarBounds.Left);
        Assert.Equal(layout.LeaderboardListBounds.Top, layout.ScrollbarBounds.Top);
        Assert.Equal(layout.LeaderboardListBounds.Height, layout.ScrollbarBounds.Height);
    }

    [Theory]
    [InlineData(2000, BattleReportLayout.PreferredWidth)]
    [InlineData(600, 560)]
    [InlineData(300, 300)]
    public void Calculate_WidthTracksPreferredMinimumThenArenaClamp(int arenaWidth, int expectedWidth)
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(0, 0, arenaWidth, 1000));

        Assert.Equal(expectedWidth, layout.Bounds.Width);
    }

    [Theory]
    [InlineData(2000, BattleReportLayout.PreferredHeight)]
    [InlineData(300, 260)]
    [InlineData(20, 0)]
    public void Calculate_HeightTracksPreferredThenArenaClamp(int arenaHeight, int expectedHeight)
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(0, 0, 1600, arenaHeight));

        Assert.Equal(expectedHeight, layout.Bounds.Height);
    }

    [Fact]
    public void Calculate_MinimumViewportStillProducesNonNegativeRegions()
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(0, 0, 300, 200));

        Assert.True(layout.HeaderBounds.Width >= 0);
        Assert.True(layout.HeaderBounds.Height >= 0);
        Assert.True(layout.FactionTotalsBounds.Width >= 0);
        Assert.True(layout.FactionTotalsBounds.Height >= 0);
        Assert.True(layout.HighlightsBounds.Width >= 0);
        Assert.True(layout.HighlightsBounds.Height >= 0);
        Assert.True(layout.LeaderboardHeaderBounds.Width >= 0);
        Assert.True(layout.LeaderboardHeaderBounds.Height >= 0);
        Assert.True(layout.LeaderboardListBounds.Width >= 0);
        Assert.True(layout.LeaderboardListBounds.Height >= 0);
        Assert.True(layout.ScrollbarBounds.Width >= 0);
        Assert.True(layout.ScrollbarBounds.Height >= 0);
        Assert.True(BattleReportLayout.GetVisibleRowCount(layout) >= 0);
    }

    [Fact]
    public void GetVisibleRowCount_MatchesListHeightDividedByRowHeight()
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(0, 0, 1600, 1000));

        var expected = layout.LeaderboardListBounds.Height / BattleReportLayout.RowHeight;

        Assert.Equal(expected, BattleReportLayout.GetVisibleRowCount(layout));
        Assert.True(BattleReportLayout.GetVisibleRowCount(layout) > 0);
    }

    [Fact]
    public void GetVisibleRowCount_IsZeroWhenTheListHasNoHeight()
    {
        var layout = BattleReportLayout.Calculate(new Rectangle(0, 0, 300, 20));

        Assert.Equal(0, BattleReportLayout.GetVisibleRowCount(layout));
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
}
