using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

[Collection(UiScaleContextCollection.Name)]
public sealed class MatchSummaryPanelTests
{
    [Fact]
    public void Update_AtOneHundredPercentPreservesBaselineGeometry()
    {
        AtScale(UiScale.Percent100, () =>
        {
            var panel = new MatchSummaryPanel();

            panel.Update(
                new InputEdges(),
                CreateSummary(),
                new Rectangle(0, 0, 1600, 1000));

            Assert.Equal(new Rectangle(550, 316, 500, 368), panel.Bounds);
            Assert.Equal(
                new Rectangle(595, 622, 198, 44),
                panel.ButtonBounds[0]);
            Assert.Equal(
                new Rectangle(807, 622, 198, 44),
                panel.ButtonBounds[1]);
            Assert.Equal(
                new Rectangle(595, 564, 410, 44),
                panel.ButtonBounds[2]);
        });
    }

    [Theory]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void Update_ScaledGeometryExpandsWithItsFontTier(UiScale scale)
    {
        AtScale(scale, () =>
        {
            var panel = new MatchSummaryPanel();
            var arena = new Rectangle(0, 0, 3840, 2160);

            panel.Update(new InputEdges(), CreateSummary(), arena);

            Assert.Equal(UiScaleContext.Pixels(500), panel.Bounds.Width);
            Assert.Equal(UiScaleContext.Pixels(368), panel.Bounds.Height);
            Assert.All(
                panel.ButtonBounds,
                bounds =>
                {
                    Assert.True(panel.Bounds.Contains(bounds));
                    Assert.True(bounds.Height >= UiScaleContext.Pixels(44));
                });
        });
    }

    [Fact]
    public void Update_AtTwoHundredPercentClampsButtonsToASmallArena()
    {
        AtScale(UiScale.Percent200, () =>
        {
            var panel = new MatchSummaryPanel();
            var arena = new Rectangle(0, 0, 300, 200);

            panel.Update(new InputEdges(), CreateSummary(), arena);

            Assert.True(arena.Contains(panel.Bounds));
            Assert.All(
                panel.ButtonBounds,
                bounds => Assert.True(panel.Bounds.Contains(bounds)));
            Assert.False(
                panel.ButtonBounds[0].Intersects(panel.ButtonBounds[1]));
            Assert.False(
                panel.ButtonBounds[0].Intersects(panel.ButtonBounds[2]));
        });
    }

    [Fact]
    public void Update_UsesPreferredHeightOf368WhenTheArenaHasRoom()
    {
        var panel = new MatchSummaryPanel();

        panel.Update(new InputEdges(), CreateSummary(), new Rectangle(0, 0, 1600, 1000));

        Assert.Equal(368, panel.Bounds.Height);
    }

    [Fact]
    public void Update_PopulatesTheBattleReportButtonWithToggleBattleReportCommand()
    {
        var panel = new MatchSummaryPanel();

        panel.Update(new InputEdges(), CreateSummary(), new Rectangle(0, 0, 1600, 1000));

        // The full-width button sits directly above the NextRound/OpenMenu
        // row, spanning the panel's horizontal center — the same center
        // point the two-button row straddles at its own vertical mid-line.
        var battleReportPoint = new Point(
            panel.Bounds.Center.X,
            panel.Bounds.Bottom - 98);

        Assert.Equal(
            ClientCommand.ToggleBattleReport,
            panel.GetCommandAt(battleReportPoint));
    }

    [Fact]
    public void Update_LeavesNextRoundAndMenuReachableAtTheirOwnRow()
    {
        var panel = new MatchSummaryPanel();

        panel.Update(new InputEdges(), CreateSummary(), new Rectangle(0, 0, 1600, 1000));

        var nextRoundPoint = new Point(panel.Bounds.Center.X - 106, panel.Bounds.Bottom - 40);
        var menuPoint = new Point(panel.Bounds.Center.X + 106, panel.Bounds.Bottom - 40);

        Assert.Equal(ClientCommand.NextRound, panel.GetCommandAt(nextRoundPoint));
        Assert.Equal(ClientCommand.OpenMenu, panel.GetCommandAt(menuPoint));
    }

    [Fact]
    public void Update_ThePairRowAndTheBattleReportButtonDoNotOverlap()
    {
        var panel = new MatchSummaryPanel();

        panel.Update(new InputEdges(), CreateSummary(), new Rectangle(0, 0, 1600, 1000));

        var battleReportPoint = new Point(panel.Bounds.Center.X, panel.Bounds.Bottom - 98);
        var nextRoundPoint = new Point(panel.Bounds.Center.X - 106, panel.Bounds.Bottom - 40);

        // Directly below the battle-report button's own vertical span (a
        // point in the gap between the two rows) must not resolve to any
        // command.
        var gapPoint = new Point(panel.Bounds.Center.X, panel.Bounds.Bottom - 69);

        Assert.NotEqual(
            panel.GetCommandAt(battleReportPoint),
            panel.GetCommandAt(nextRoundPoint));
        Assert.Null(panel.GetCommandAt(gapPoint));
    }

    [Fact]
    public void Update_WithoutASummaryClearsBounds()
    {
        var panel = new MatchSummaryPanel();
        panel.Update(new InputEdges(), CreateSummary(), new Rectangle(0, 0, 1600, 1000));

        panel.Update(new InputEdges(), summary: null, new Rectangle(0, 0, 1600, 1000));

        Assert.Equal(Rectangle.Empty, panel.Bounds);
    }

    [Fact]
    public void EntranceMotion_PreservesFinalBoundsAndOffSnaps()
    {
        var panel = new MatchSummaryPanel();
        var arena = new Rectangle(0, 0, 1600, 1000);

        panel.Update(
            new InputEdges(),
            CreateSummary(),
            arena,
            TimeSpan.FromMilliseconds(50),
            MotionIntensity.Full);
        var enteringBounds = panel.Bounds;

        Assert.InRange(panel.EntranceOpacity, 0.001f, 0.999f);

        panel.Update(
            new InputEdges(),
            CreateSummary(),
            arena,
            TimeSpan.Zero,
            MotionIntensity.Off);

        Assert.Equal(1f, panel.EntranceOpacity);
        Assert.Equal(enteringBounds, panel.Bounds);
    }

    private static MatchSummary CreateSummary() =>
        new(
            WinnerLabel: "Blue",
            BlueSurvivors: 5,
            RedSurvivors: 0,
            TerminalTick: 1200,
            SimulatedDurationSeconds: 20.0,
            Seed: 1);

    private static void AtScale(UiScale scale, Action action)
    {
        var previous = UiScaleContext.ActiveScale;
        try
        {
            UiScaleContext.Set(scale);
            action();
        }
        finally
        {
            UiScaleContext.Set(previous);
        }
    }
}
