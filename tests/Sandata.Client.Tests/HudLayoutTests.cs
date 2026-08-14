using System.Linq;
using Microsoft.Xna.Framework;
using Sandata.Client.UI;
using Sandata.Core.Navigation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 38's done-when bar for the HUD layout helpers: every element's
/// rectangle pinned at three window sizes (1280x720, 1600x900, 1920x1080),
/// plus the event log's 200-event retention rule. Every expected
/// <see cref="Rectangle"/> below was derived by hand from each helper's own
/// formula, not copied from a first failing run. No member of this class
/// constructs a <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a
/// <c>SandataGame</c>.
/// </summary>
public sealed class HudLayoutTests
{
    private static readonly Rectangle Window1280x720 = new(0, 0, 1280, 720);
    private static readonly Rectangle Window1600x900 = new(0, 0, 1600, 900);
    private static readonly Rectangle Window1920x1080 = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(1280, 720, 1108, 12, 160, 40)]
    [InlineData(1600, 900, 1428, 12, 160, 40)]
    [InlineData(1920, 1080, 1748, 12, 160, 40)]
    public void MissionClock_BoundsArePinnedAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var bounds = MissionClock.CalculateBounds(new Rectangle(0, 0, windowWidth, windowHeight));

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Fact]
    public void MissionClock_ClampsToANarrowWindow()
    {
        var bounds = MissionClock.CalculateBounds(new Rectangle(0, 0, 100, 20));

        Assert.Equal(new Rectangle(12, 12, 76, 0), bounds);
    }

    [Theory]
    [InlineData(1280, 720, 570, 12, 140, 32)]
    [InlineData(1600, 900, 730, 12, 140, 32)]
    [InlineData(1920, 1080, 890, 12, 140, 32)]
    public void AlertIndicator_BoundsArePinnedAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var bounds = AlertIndicator.CalculateBounds(new Rectangle(0, 0, windowWidth, windowHeight));

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Fact]
    public void AlertIndicator_ShapeAndColourBothDifferAcrossAllThreeLevels()
    {
        var colors = new Sandata.Client.Theming.SandataThemeColors(
            CanvasBackground: Color.Black, ArenaSurface: Color.Black, ArenaBorder: Color.Black,
            StatusSurface: Color.Black, OverlayScrim: Color.Black, PanelSurface: Color.Black,
            PanelAlternate: Color.Black, PanelBorder: Color.Black, TextPrimary: Color.White,
            TextSecondary: Color.White, TextDisabled: Color.Gray, TextInverse: Color.Black,
            ActionDefault: Color.Gray, ActionHover: Color.Gray, ActionFocus: Color.Gray,
            ActionPressed: Color.Gray, ActionActive: Color.Gray, ActionDisabled: Color.Gray,
            StatusInfo: Color.Blue, StatusSuccess: Color.Green, StatusWarning: Color.Yellow,
            StatusDanger: Color.Red, NewEvent: Color.White,
            Friendly: Color.Blue, Hostile: Color.Red, UnknownContact: Color.Gray,
            SelectedTrooper: Color.White,
            Suppressed: Color.Orange, Downed: Color.DarkRed, OrderPath: Color.Cyan,
            Waypoint: Color.Cyan, CoverGood: Color.Green, CoverNone: Color.Gray,
            BreachPoint: Color.Red, FireConeFill: Color.Yellow, FireConeEdge: Color.Orange,
            Weapon: Color.LightGray,
            AlertCalm: Color.Green, AlertRaised: Color.Yellow, AlertBreach: Color.Red);

        var shapes = new[]
        {
            AlertIndicator.GetShape(0),
            AlertIndicator.GetShape(1),
            AlertIndicator.GetShape(2),
        };
        var colours = new[]
        {
            AlertIndicator.GetColor(0, colors),
            AlertIndicator.GetColor(1, colors),
            AlertIndicator.GetColor(2, colors),
        };

        Assert.Equal(3, shapes.Distinct().Count());
        Assert.Equal(3, colours.Distinct().Count());
    }

    [Theory]
    [InlineData(1280, 720, 12, 644, 402, 64)]
    [InlineData(1600, 900, 12, 824, 402, 64)]
    [InlineData(1920, 1080, 12, 1004, 402, 64)]
    public void RosterStrip_BoundsArePinnedAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var bounds = RosterStrip.CalculateBounds(new Rectangle(0, 0, windowWidth, windowHeight), operatorCount: 4);

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Fact]
    public void RosterStrip_ClipsToHowManyTilesFitInANarrowWindow()
    {
        var bounds = RosterStrip.CalculateBounds(new Rectangle(0, 0, 200, 720), operatorCount: 4);

        Assert.Equal(1, RosterStrip.CountVisibleTiles(new Rectangle(0, 0, 200, 720), 4));
        Assert.Equal(new Rectangle(12, 644, 96, 64), bounds);
    }

    [Fact]
    public void RosterStrip_TileBoundsAreLaidOutLeftToRightWithoutOverlap()
    {
        var stripBounds = RosterStrip.CalculateBounds(Window1280x720, operatorCount: 4);

        var tile0 = RosterStrip.CalculateTileBounds(stripBounds, 0);
        var tile1 = RosterStrip.CalculateTileBounds(stripBounds, 1);

        Assert.Equal(new Rectangle(12, 644, 96, 64), tile0);
        Assert.Equal(new Rectangle(114, 644, 96, 64), tile1);
        Assert.False(tile0.Intersects(tile1));
    }

    [Theory]
    [InlineData(1280, 720, 988, 64, 280, 134)]
    [InlineData(1600, 900, 1308, 64, 280, 134)]
    [InlineData(1920, 1080, 1628, 64, 280, 134)]
    public void ContactList_BoundsArePinnedAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var bounds = ContactList.CalculateBounds(new Rectangle(0, 0, windowWidth, windowHeight), contactCount: 5);

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Theory]
    [InlineData(1280, 720, 988, 206, 280, 502)]
    [InlineData(1600, 900, 1308, 206, 280, 682)]
    [InlineData(1920, 1080, 1628, 206, 280, 862)]
    public void SandataEventLog_BoundsArePinnedBelowContactListAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var windowBounds = new Rectangle(0, 0, windowWidth, windowHeight);
        var contactListBounds = ContactList.CalculateBounds(windowBounds, contactCount: 5);

        var bounds = SandataEventLog.CalculateBounds(windowBounds, contactListBounds);

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Fact]
    public void SandataEventLog_DiscardsTheOldestEventBeyondTwoHundred()
    {
        var log = new SandataEventLog();

        for (var tick = 1; tick <= 205; tick++)
        {
            log.Add(new HudEvent(tick, $"event {tick}"));
        }

        Assert.Equal(SandataEventLog.MaxRetainedEvents, log.Events.Count);
        Assert.Equal(6, log.Events[0].Tick);
        Assert.Equal(205, log.Events[^1].Tick);
    }

    [Fact]
    public void SandataEventLog_KeepsEveryEventWhenUnderCapacity()
    {
        var log = new SandataEventLog();

        for (var tick = 1; tick <= 50; tick++)
        {
            log.Add(new HudEvent(tick, $"event {tick}"));
        }

        Assert.Equal(50, log.Events.Count);
        Assert.Equal(1, log.Events[0].Tick);
    }

    [Theory]
    [InlineData(1280, 720, 436, 672, 408, 36)]
    [InlineData(1600, 900, 596, 852, 408, 36)]
    [InlineData(1920, 1080, 756, 1032, 408, 36)]
    public void SandataControlBar_BoundsArePinnedAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var bounds = SandataControlBar.CalculateBounds(new Rectangle(0, 0, windowWidth, windowHeight));

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Fact]
    public void SandataControlBar_ButtonsAreLaidOutLeftToRightWithoutOverlap()
    {
        var barBounds = SandataControlBar.CalculateBounds(Window1280x720);

        var pause = SandataControlBar.CalculateButtonBounds(barBounds, SandataControlBar.Button.Pause);
        var step = SandataControlBar.CalculateButtonBounds(barBounds, SandataControlBar.Button.StepOneTick);
        var speed = SandataControlBar.CalculateButtonBounds(barBounds, SandataControlBar.Button.Speed);
        var restart = SandataControlBar.CalculateButtonBounds(barBounds, SandataControlBar.Button.Restart);

        Assert.Equal(new Rectangle(436, 672, 96, 36), pause);
        Assert.Equal(new Rectangle(540, 672, 96, 36), step);
        Assert.Equal(new Rectangle(644, 672, 96, 36), speed);
        Assert.Equal(new Rectangle(748, 672, 96, 36), restart);
        Assert.False(pause.Intersects(step));
        Assert.False(step.Intersects(speed));
        Assert.False(speed.Intersects(restart));
    }

    /// <summary>
    /// The pinned height was 222 until 2026-08-14, when decision D2 of the
    /// lowered-weapon design added the firearm and weapon-state rows and took
    /// <see cref="OperatorInspector.LineCount"/> from 11 to 13. The panel is
    /// two line heights taller as a result; nothing else about its anchoring
    /// moved, which is what the unchanged x, y, and width columns assert.
    /// </summary>
    [Theory]
    [InlineData(1280, 720, 12, 12, 320, 258)]
    [InlineData(1600, 900, 12, 12, 320, 258)]
    [InlineData(1920, 1080, 12, 12, 320, 258)]
    public void OperatorInspector_BoundsArePinnedAtThreeWindowSizes(
        int windowWidth, int windowHeight, int x, int y, int width, int height)
    {
        var bounds = OperatorInspector.CalculateBounds(new Rectangle(0, 0, windowWidth, windowHeight));

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    /// <summary>
    /// Task 71's own done-when bar for <see cref="HudComposer"/>'s
    /// go-code-panel/order-queue-view stacking math: at a window far smaller
    /// than every panel's preferred size, both of task 71's own synthetic
    /// windows (<c>goCodePanelWindow</c>, <c>orderQueueViewWindow</c> inside
    /// <see cref="HudComposer.Compose"/>) still clamp to a non-negative,
    /// real-window-bounded rectangle. This is distinct from
    /// <c>PathDrawToolTests</c>'s own <c>GoCodePanel_CalculateBounds_ClipsSanelyForATooSmallWindow</c>
    /// and <c>OrderQueueView_CalculateBounds_ClipsSanelyForATooSmallWindow</c>,
    /// which exercise the two panel helpers' own clamp directly against a
    /// too-small window rectangle; this test exercises task 71's own
    /// composition-stacking math instead — the synthetic window each panel is
    /// handed, not the panel helper's own clamp.
    /// </summary>
    [Fact]
    public void HudComposer_GoCodePanelAndOrderQueueViewClipSanelyAtATooSmallComposedWindow()
    {
        var windowBounds = new Rectangle(0, 0, 100, 60);
        var minimapGrid = new NavGrid(width: 4, height: 3);

        var layout = HudComposer.Compose(
            windowBounds, operatorCount: 4, contactCount: 5, minimapGrid, goCodeCount: 4, orderQueueEntryCount: 5);

        Assert.True(layout.GoCodePanel.Width >= 0);
        Assert.True(layout.GoCodePanel.Height >= 0);
        Assert.True(layout.GoCodePanel.Width <= windowBounds.Width);
        Assert.True(layout.GoCodePanel.Height <= windowBounds.Height);

        Assert.True(layout.OrderQueueView.Width >= 0);
        Assert.True(layout.OrderQueueView.Height >= 0);
        Assert.True(layout.OrderQueueView.Width <= windowBounds.Width);
        Assert.True(layout.OrderQueueView.Height <= windowBounds.Height);
    }
}
