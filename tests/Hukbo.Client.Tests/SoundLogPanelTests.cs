using Hukbo.Client.Audio;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class SoundLogPanelTests
{
    private static readonly Rectangle PanelBounds = new(900, 400, 420, 300);

    [Fact]
    public void CalculateLayout_KeepsEveryRegionInsideThePanel()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        foreach (var region in Regions(layout))
        {
            Assert.True(
                PanelBounds.Contains(region) || region.IsEmpty,
                $"{region} escaped the panel bounds.");
        }
    }

    [Fact]
    public void CalculateLayout_StacksHeaderPathBindingsAndCuesInOrder()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.True(layout.PathBounds.Top >= layout.HeaderBounds.Bottom);
        Assert.True(layout.BindingsBounds.Top >= layout.PathBounds.Bottom);
        Assert.True(
            layout.BindingRowsBounds.Top >= layout.BindingsBounds.Top);
        Assert.True(layout.CueListBounds.Top >= layout.BindingsBounds.Bottom);
        Assert.True(layout.CueRowsBounds.Top >= layout.CueListBounds.Top);
    }

    [Fact]
    public void CalculateLayout_PutsMuteAtTheRightOfTheHeader()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.Equal(layout.HeaderBounds.Right, layout.MuteBounds.Right);
        Assert.Equal(layout.HeaderBounds.Top, layout.MuteBounds.Top);
        Assert.True(layout.MuteBounds.Left > layout.HeaderBounds.Left);
    }

    [Fact]
    public void CalculateLayout_ReservesScrollbarSpaceBesideTheCueRows()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.True(
            layout.ScrollbarTrackBounds.Left >= layout.CueRowsBounds.Right);
        Assert.Equal(
            layout.CueListBounds.Right,
            layout.ScrollbarTrackBounds.Right);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(40, 20)]
    [InlineData(420, 24)]
    public void CalculateLayout_ProducesNoNegativeSizesAtTinyBounds(
        int width,
        int height)
    {
        var layout = SoundLogPanel.CalculateLayout(
            new Rectangle(10, 10, width, height));

        foreach (var region in Regions(layout))
        {
            Assert.True(region.Width >= 0);
            Assert.True(region.Height >= 0);
        }

        Assert.True(SoundLogPanel.GetVisibleCueRowCount(layout) >= 0);
        Assert.True(SoundLogPanel.GetVisibleBindingRowCount(layout) >= 0);
    }

    [Fact]
    public void GetVisibleRowCounts_DeriveFromTheRowHeights()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.Equal(
            layout.CueRowsBounds.Height / SoundLogPanel.CueRowHeight,
            SoundLogPanel.GetVisibleCueRowCount(layout));
        Assert.Equal(
            layout.BindingRowsBounds.Height / SoundLogPanel.BindingRowHeight,
            SoundLogPanel.GetVisibleBindingRowCount(layout));
    }

    [Fact]
    public void GetRowBounds_AdvancesByOneRowHeightAndRejectsNegativeIndexes()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        var firstCue = SoundLogPanel.GetCueRowBounds(layout, 0);
        var secondCue = SoundLogPanel.GetCueRowBounds(layout, 1);
        Assert.Equal(SoundLogPanel.CueRowHeight, secondCue.Top - firstCue.Top);

        var firstBinding = SoundLogPanel.GetBindingRowBounds(layout, 0);
        var secondBinding = SoundLogPanel.GetBindingRowBounds(layout, 1);
        Assert.Equal(
            SoundLogPanel.BindingRowHeight,
            secondBinding.Top - firstBinding.Top);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundLogPanel.GetCueRowBounds(layout, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundLogPanel.GetBindingRowBounds(layout, -1));
    }

    [Fact]
    public void HitTestMute_MatchesOnlyThePointerInsideTheMuteControl()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.True(
            SoundLogPanel.HitTestMute(layout, layout.MuteBounds.Center));
        Assert.False(
            SoundLogPanel.HitTestMute(
                layout,
                new Point(
                    layout.HeaderBounds.Left + 1,
                    layout.HeaderBounds.Top + 1)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(120, -3)]
    [InlineData(-120, 3)]
    [InlineData(240, -6)]
    [InlineData(40, -3)]
    public void GetScrollRowDelta_TurnsWheelDeltaIntoRows(
        int wheelDelta,
        int expectedRows) =>
        Assert.Equal(
            expectedRows,
            SoundLogPanel.GetScrollRowDelta(wheelDelta));

    [Fact]
    public void GetScrollbarThumb_FillsTheTrackWhenEverythingFits()
    {
        var track = new Rectangle(0, 0, 8, 120);

        Assert.Equal(
            track,
            SoundLogPanel.GetScrollbarThumb(
                track,
                totalEntryCount: 4,
                visibleRowCount: 8,
                scrollStart: 0));
        Assert.Equal(
            track,
            SoundLogPanel.GetScrollbarThumb(
                track,
                totalEntryCount: 0,
                visibleRowCount: 8,
                scrollStart: 0));
    }

    [Fact]
    public void GetScrollbarThumb_TravelsWithTheScrollPosition()
    {
        var track = new Rectangle(0, 100, 8, 200);

        var top = SoundLogPanel.GetScrollbarThumb(track, 40, 10, 0);
        var bottom = SoundLogPanel.GetScrollbarThumb(track, 40, 10, 30);

        Assert.Equal(track.Top, top.Top);
        Assert.Equal(track.Bottom, bottom.Bottom);
        Assert.True(top.Height >= SoundLogPanel.MinimumThumbHeight);
        Assert.True(top.Height <= track.Height);
    }

    [Fact]
    public void ClipText_KeepsTheStartAndMarksTheTrim()
    {
        Assert.Equal("death.wav", SoundLogPanel.ClipText("death.wav", 20));
        Assert.Equal("de...", SoundLogPanel.ClipText("death.wav", 5));
        Assert.Equal("..", SoundLogPanel.ClipText("death.wav", 2));
        Assert.Equal(string.Empty, SoundLogPanel.ClipText("death.wav", 0));
    }

    [Fact]
    public void ClipPathTail_KeepsTheFolderTheOwnerNeedsToSee()
    {
        Assert.Equal(
            "...Content/Audio",
            SoundLogPanel.ClipPathTail("/game/build/Content/Audio", 16));
        Assert.Equal(
            "/short/path",
            SoundLogPanel.ClipPathTail("/short/path", 40));
    }

    [Fact]
    public void StatusColors_AreDistinctSemanticThemeRoles()
    {
        var theme = LoadTheme();

        Assert.Equal(
            theme.Colors.StatusSuccess,
            SoundLogPanel.GetCueStatusColor(
                theme.Colors,
                SoundCueStatus.Played));
        Assert.Equal(
            theme.Colors.StatusWarning,
            SoundLogPanel.GetCueStatusColor(
                theme.Colors,
                SoundCueStatus.Missing));
        Assert.Equal(
            theme.Colors.StatusDanger,
            SoundLogPanel.GetCueStatusColor(
                theme.Colors,
                SoundCueStatus.LoadFailed));
        Assert.Equal(
            theme.Colors.StatusSuccess,
            SoundLogPanel.GetBindingStatusColor(
                theme.Colors,
                SoundBindingStatus.Ready));
        Assert.Equal(
            theme.Colors.StatusDanger,
            SoundLogPanel.GetBindingStatusColor(
                theme.Colors,
                SoundBindingStatus.LoadFailed));
    }

    [Fact]
    public void StatusColors_RejectUndeclaredStatuses()
    {
        var theme = LoadTheme();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundLogPanel.GetCueStatusColor(
                theme.Colors,
                (SoundCueStatus)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundLogPanel.GetBindingStatusColor(
                theme.Colors,
                (SoundBindingStatus)99));
    }

    [Fact]
    public void CalculateLayout_ShowsEveryExpectedFileNameAtTheDefaultSize()
    {
        // The panel is the documentation of what to name a file, so at the
        // layout the client actually uses it must be able to list every slot.
        var layout = SoundLogPanel.CalculateLayout(new Rectangle(0, 0, 420, 288));

        Assert.True(
            SoundLogPanel.GetVisibleBindingRowCount(layout) >=
            SoundCatalog.AllSounds.Count);
    }

    private static IEnumerable<Rectangle> Regions(SoundLogPanelLayout layout)
    {
        yield return layout.HeaderBounds;
        yield return layout.MuteBounds;
        yield return layout.PathBounds;
        yield return layout.BindingsBounds;
        yield return layout.BindingRowsBounds;
        yield return layout.CueListBounds;
        yield return layout.CueRowsBounds;
        yield return layout.ScrollbarTrackBounds;
    }

    private static UiTheme LoadTheme()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        return UiThemeCatalog.Load(path).GetRequired("command");
    }
}
