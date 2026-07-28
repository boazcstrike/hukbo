using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class MatchSummaryPanelTests
{
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

    private static MatchSummary CreateSummary() =>
        new(
            WinnerLabel: "Blue",
            BlueSurvivors: 5,
            RedSurvivors: 0,
            TerminalTick: 1200,
            SimulatedDurationSeconds: 20.0,
            Seed: 1);
}
