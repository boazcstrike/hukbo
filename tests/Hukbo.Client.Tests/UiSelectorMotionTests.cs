using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class UiSelectorMotionTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(120);
    private static readonly Rectangle PreviousBounds = new(0, 0, 10, 10);
    private static readonly Rectangle NextBounds = new(20, 0, 10, 10);
    private static readonly Point InsidePrevious = new(5, 5);
    private static readonly Point InsideNext = new(25, 5);
    private static readonly Point OutsideBoth = new(100, 100);

    private static readonly UiThemeColors Colors = new(
        CanvasBackground: Color.Black,
        ArenaSurface: Color.Black,
        ArenaBorder: Color.Black,
        StatusSurface: Color.Black,
        OverlayScrim: Color.Black,
        PanelSurface: Color.Black,
        PanelAlternate: Color.Black,
        PanelBorder: Color.Black,
        TextPrimary: Color.Blue,
        TextSecondary: Color.Red,
        TextDisabled: Color.Black,
        TextInverse: Color.Black,
        ActionDefault: Color.Black,
        ActionHover: Color.Black,
        ActionFocus: Color.Yellow,
        ActionPressed: Color.Black,
        ActionActive: Color.Black,
        ActionDisabled: Color.Black,
        StatusInfo: Color.Black,
        StatusSuccess: Color.Black,
        StatusWarning: Color.Black,
        StatusDanger: Color.Black,
        TeamA: Color.Black,
        TeamB: Color.Black,
        OtherFaction: Color.Black,
        Selection: Color.Green,
        NewEvent: Color.Black);

    [Fact]
    public void AdvanceMotion_PointerOutsideBothSettlesArrowsAtZero()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            OutsideBoth,
            PreviousBounds,
            NextBounds,
            "A",
            TimeSpan.FromMilliseconds(16),
            MotionIntensity.Full);

        Assert.Equal(Colors.TextSecondary, motion.PreviousArrowColor(Colors));
        Assert.Equal(Colors.TextSecondary, motion.NextArrowColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_PointerInsidePreviousRisesThenSettlesAtOne()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            InsidePrevious,
            PreviousBounds,
            NextBounds,
            "A",
            TimeSpan.FromMilliseconds(60),
            MotionIntensity.Full);
        var midColor = motion.PreviousArrowColor(Colors);

        Assert.NotEqual(Colors.TextSecondary, midColor);
        Assert.NotEqual(Colors.TextPrimary, midColor);
        Assert.Equal(Colors.TextSecondary, motion.NextArrowColor(Colors));

        motion.AdvanceMotion(
            InsidePrevious,
            PreviousBounds,
            NextBounds,
            "A",
            Duration,
            MotionIntensity.Full);

        Assert.Equal(Colors.TextPrimary, motion.PreviousArrowColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_PointerInsideNextRisesToOneOnNextArrowOnly()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            InsideNext,
            PreviousBounds,
            NextBounds,
            "A",
            Duration,
            MotionIntensity.Full);

        Assert.Equal(Colors.TextPrimary, motion.NextArrowColor(Colors));
        Assert.Equal(Colors.TextSecondary, motion.PreviousArrowColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_MotionOffSnapsArrowsWithoutIntermediateStep()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            InsidePrevious,
            PreviousBounds,
            NextBounds,
            "A",
            TimeSpan.FromMilliseconds(1),
            MotionIntensity.Off);

        Assert.Equal(Colors.TextPrimary, motion.PreviousArrowColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_FirstObservationDoesNotPulseMarker()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            OutsideBoth,
            PreviousBounds,
            NextBounds,
            "ACTIVE - 1 / 3",
            TimeSpan.FromMilliseconds(16),
            MotionIntensity.Full);

        Assert.Equal(Colors.Selection, motion.MarkerColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_UnchangedMarkerTextNeverPulses()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 1 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Full);
        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 1 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Full);
        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 1 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Full);

        Assert.Equal(Colors.Selection, motion.MarkerColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_MarkerTextChangeTriggersExactlyOnePulse()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 1 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Full);
        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 2 / 3",
            TimeSpan.Zero, MotionIntensity.Full);

        Assert.Equal(Colors.ActionFocus, motion.MarkerColor(Colors));

        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 2 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Full);
        var decayedColor = motion.MarkerColor(Colors);

        Assert.NotEqual(Colors.ActionFocus, decayedColor);
        Assert.NotEqual(Colors.Selection, decayedColor);

        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 2 / 3",
            TimeSpan.MaxValue, MotionIntensity.Full);

        Assert.Equal(Colors.Selection, motion.MarkerColor(Colors));
    }

    [Fact]
    public void AdvanceMotion_MotionOffNeverPulsesMarkerOnChange()
    {
        var motion = new UiSelectorMotion();

        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 1 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Off);
        motion.AdvanceMotion(
            OutsideBoth, PreviousBounds, NextBounds, "ACTIVE - 2 / 3",
            TimeSpan.FromMilliseconds(16), MotionIntensity.Off);

        Assert.Equal(Colors.Selection, motion.MarkerColor(Colors));
    }

    [Fact]
    public void PreviousArrowColor_NullColors_Throws()
    {
        var motion = new UiSelectorMotion();

        Assert.Throws<ArgumentNullException>(() => motion.PreviousArrowColor(null!));
    }
}
