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
    public void BuildBindingRows_AddsOneHeaderRowForAClasslessSlot()
    {
        var bindings = new[]
        {
            new SoundBinding(
                GameSoundId.Death,
                "death.wav",
                ClassCounts: [],
                VariantCount: 10,
                SoundBindingStatus.Ready),
        };

        var rows = SoundLogPanel.BuildBindingRows(bindings);

        var row = Assert.Single(rows);
        Assert.Equal("death.wav", row.Label);
        Assert.Equal("READY (10)", row.StatusText);
        Assert.Equal(SoundBindingStatus.Ready, row.Status);
    }

    [Fact]
    public void BuildBindingRows_AddsOneIndentedSubRowPerClassForAWeaponSlot()
    {
        var classCounts = new[]
        {
            new SoundClassCount(HitClass.Skull, 2, SoundBindingStatus.Ready),
            new SoundClassCount(HitClass.Neck, 0, SoundBindingStatus.Missing),
        };
        var bindings = new[]
        {
            new SoundBinding(
                GameSoundId.AttackKampilan,
                "attack-kampilan.wav",
                classCounts,
                VariantCount: 2,
                SoundBindingStatus.Ready),
        };

        var rows = SoundLogPanel.BuildBindingRows(bindings);

        Assert.Equal(3, rows.Count);
        Assert.Equal("attack-kampilan.wav", rows[0].Label);
        Assert.Equal("READY (2)", rows[0].StatusText);
        Assert.Contains("skull", rows[1].Label);
        Assert.Equal("READY (2)", rows[1].StatusText);
        Assert.Contains("neck", rows[2].Label);
        Assert.Equal("MISSING", rows[2].StatusText);
    }

    [Fact]
    public void BuildBindingRows_ShowsAPlainStatusWithNoCountWhenNotReady()
    {
        var bindings = new[]
        {
            new SoundBinding(
                GameSoundId.Draw,
                "draw.wav",
                ClassCounts: [],
                VariantCount: 0,
                SoundBindingStatus.Missing),
        };

        var rows = SoundLogPanel.BuildBindingRows(bindings);

        Assert.Equal("MISSING", Assert.Single(rows).StatusText);
    }

    [Fact]
    public void BuildBindingRows_RejectsNullBindings() =>
        Assert.Throws<ArgumentNullException>(
            () => SoundLogPanel.BuildBindingRows(null!));

    [Fact]
    public void CalculateLayout_CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight()
    {
        // The expected-files section is a viewport onto the first
        // `SoundCatalog.AllSounds.Count` rows of the bindings list. It is
        // never a view of the whole list: `BuildBindingRows` emits one row
        // per slot plus one indented row per hit class for each of the four
        // location-driven weapon slots, which is thirty-three rows at nine
        // slots, while `CalculateLayout` caps the section at one section
        // header plus `SoundCatalog.AllSounds.Count` binding rows. The panel
        // has therefore never been able to list every row, and no window
        // height can make it.
        //
        // That cap is deliberate. Without it the expected-files section would
        // grow on a tall window until the cue log was left with only its
        // three reserved rows. Reaching the rows past the cap is what
        // scrolling the list is for, not enlarging the window.
        //
        // The other cap on the section is the space actually available, which
        // leaves `H - 216` of row height in a panel of height `H`. The slot
        // count is the binding cap only once the window is tall enough that
        // the available space is not the smaller of the two, that is
        // `H >= 216 + 20 * SoundCatalog.AllSounds.Count`, which comes to 476
        // at thirteen slots and twenty less for every slot fewer. A height of
        // 2000 clears it by a wide margin at any slot count this panel will
        // carry, which is why this test uses it. Below the threshold the
        // layout decides the row count rather than the slot count, and the
        // assertion would be comparing the wrong two numbers.
        var layout = SoundLogPanel.CalculateLayout(new Rectangle(0, 0, 420, 2000));

        Assert.Equal(
            SoundCatalog.AllSounds.Count,
            SoundLogPanel.GetVisibleBindingRowCount(layout));
    }

    [Fact]
    public void CalculateLayout_FitsExactlyTenBindingRowsAtFourHundredAndSixteen()
    {
        // `ArenaGame.SoundLogHeightPercent` is 65 because that is the
        // smallest percentage buying a ten-row viewport, not because ten
        // rows happen to look comfortable there. This test pins both sides
        // of the boundary so a later nudge downwards cannot pass unnoticed.
        //
        // The derivation, for a panel `H` pixels tall:
        //
        //     available      = H - 110
        //     bindingsHeight = min(20 + 13 * 20, available - 6 - 80)
        //                    = min(280, H - 196)
        //     visibleRows    = (bindingsHeight - 20) / 20
        //
        // The 110 is the panel's top chrome plus its bottom padding, the 6
        // is the gap between the two sections, and the 80 is the cue log's
        // section header plus its three minimum-reserved rows. At thirteen
        // slots the slot cap of 280 never binds at these heights, so the
        // available space decides:
        //
        //     H = 415:  min(280, 219) = 219,  (219 - 20) / 20 = 9
        //     H = 416:  min(280, 220) = 220,  (220 - 20) / 20 = 10
        //
        // The panel really is that tall at the shipped percentage: the
        // right column at the default 1280x720 window is
        // 720 - 68 - 12 = 640 pixels, and 640 * 65 / 100 = 416.
        Assert.Equal(
            9,
            SoundLogPanel.GetVisibleBindingRowCount(
                SoundLogPanel.CalculateLayout(new Rectangle(0, 0, 420, 415))));
        Assert.Equal(
            10,
            SoundLogPanel.GetVisibleBindingRowCount(
                SoundLogPanel.CalculateLayout(new Rectangle(0, 0, 420, 416))));
    }

    [Fact]
    public void ClampBindingScroll_ReachesTheLastRow()
    {
        // A thirty-seven row list seen through a thirteen row viewport. The
        // furthest the list can travel is a start of 24, because 24 + 13 == 37
        // puts the final row on the viewport's bottom line. The two figures are
        // written out here rather than read from the catalog on purpose: this
        // is a statement about the clamp, not about how many slots exist.
        Assert.Equal(
            24,
            SoundLogPanel.ClampBindingScroll(
                scrollStart: 24,
                totalRowCount: 37,
                visibleRowCount: 13));
        Assert.Equal(
            24,
            SoundLogPanel.ClampBindingScroll(
                scrollStart: 25,
                totalRowCount: 37,
                visibleRowCount: 13));
    }

    [Fact]
    public void ClampBindingScroll_RefusesToScrollPastEitherEnd()
    {
        Assert.Equal(
            0,
            SoundLogPanel.ClampBindingScroll(
                scrollStart: -5,
                totalRowCount: 37,
                visibleRowCount: 13));
        Assert.Equal(
            24,
            SoundLogPanel.ClampBindingScroll(
                scrollStart: 999,
                totalRowCount: 37,
                visibleRowCount: 13));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-7)]
    [InlineData(999)]
    public void ClampBindingScroll_ReturnsZeroWhenEveryRowFits(int scrollStart)
    {
        Assert.Equal(
            0,
            SoundLogPanel.ClampBindingScroll(
                scrollStart,
                totalRowCount: 9,
                visibleRowCount: 13));
        Assert.Equal(
            0,
            SoundLogPanel.ClampBindingScroll(
                scrollStart,
                totalRowCount: 13,
                visibleRowCount: 13));
    }

    [Fact]
    public void GetWheelTarget_RoutesTheWheelToTheListUnderThePointer()
    {
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.Equal(
            SoundLogScrollTarget.Bindings,
            SoundLogPanel.GetWheelTarget(
                layout,
                layout.BindingsBounds.Center));
        Assert.Equal(
            SoundLogScrollTarget.Cues,
            SoundLogPanel.GetWheelTarget(
                layout,
                layout.CueListBounds.Center));
    }

    [Fact]
    public void GetWheelTarget_FallsBackToTheCueListOutsideBothLists()
    {
        // The header is neither list, and a wheel notch over it must still go
        // somewhere rather than being swallowed, so it keeps the behaviour the
        // panel had before the expected-files list could scroll.
        var layout = SoundLogPanel.CalculateLayout(PanelBounds);

        Assert.Equal(
            SoundLogScrollTarget.Cues,
            SoundLogPanel.GetWheelTarget(
                layout,
                layout.HeaderBounds.Center));
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
        yield return layout.BindingScrollbarTrackBounds;
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
