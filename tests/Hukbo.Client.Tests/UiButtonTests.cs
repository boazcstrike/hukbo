using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class UiButtonTests
{
    [Fact]
    public void TimedUpdate_DoesNotChangeTheHitRectangle()
    {
        var bounds = new Rectangle(0, 0, 84, 34);
        var button = new UiButton("Play", ClientCommand.Play)
        {
            Bounds = bounds,
        };

        button.Update(
            new InputEdges(),
            TimeSpan.FromMilliseconds(16),
            MotionIntensity.Full);

        Assert.Equal(bounds, button.Bounds);
    }

    [Theory]
    [InlineData(MotionIntensity.Off, 0)]
    [InlineData(MotionIntensity.Reduced, 0)]
    [InlineData(MotionIntensity.Full, 1)]
    public void PressFeedback_OnlyFullMotionUsesAPositionalInset(
        MotionIntensity intensity,
        int expectedInset)
    {
        var motion = new UiButtonMotion();

        motion.Advance(
            isHovered: true,
            isFocused: false,
            isPressed: true,
            TimeSpan.FromMilliseconds(60),
            intensity);

        Assert.Equal(1f, motion.PressAmount);
        Assert.Equal(expectedInset, motion.DecorativePressInset);
    }

    [Fact]
    public void OffMotion_SnapsBothColorChannelsToTheirTargets()
    {
        var motion = new UiButtonMotion();
        motion.Advance(
            isHovered: true,
            isFocused: false,
            isPressed: true,
            TimeSpan.Zero,
            MotionIntensity.Off);

        Assert.Equal(1f, motion.HoverAmount);
        Assert.Equal(1f, motion.PressAmount);

        motion.Advance(
            isHovered: false,
            isFocused: false,
            isPressed: false,
            TimeSpan.Zero,
            MotionIntensity.Off);

        Assert.Equal(0f, motion.HoverAmount);
        Assert.Equal(0f, motion.PressAmount);
    }

    [Fact]
    public void ReducedMotion_StillInterpolatesColorWithoutPosition()
    {
        var motion = new UiButtonMotion();

        motion.Advance(
            isHovered: true,
            isFocused: false,
            isPressed: false,
            TimeSpan.FromMilliseconds(30),
            MotionIntensity.Reduced);

        Assert.InRange(motion.HoverAmount, 0.01f, 0.99f);
        Assert.Equal(0, motion.DecorativePressInset);
    }

    [Fact]
    public void FocusColor_InterpolatesWhileFocusAvailabilityIsImmediate()
    {
        var motion = new UiButtonMotion();

        motion.Advance(
            isHovered: false,
            isFocused: true,
            isPressed: false,
            TimeSpan.FromMilliseconds(30),
            MotionIntensity.Full);

        Assert.InRange(motion.FocusAmount, 0.01f, 0.99f);
    }

    [Fact]
    public void FullMotion_PressedVisualStaysInsideTheUnchangedHitBounds()
    {
        var hitBounds = new Rectangle(10, 20, 84, 34);
        var motion = new UiButtonMotion();
        motion.Advance(
            isHovered: true,
            isFocused: false,
            isPressed: true,
            UiButtonMotion.PressDuration,
            MotionIntensity.Full);

        var visualBounds = motion.GetVisualBounds(hitBounds);

        Assert.Equal(new Rectangle(10, 21, 84, 33), visualBounds);
        Assert.Equal(new Rectangle(10, 20, 84, 34), hitBounds);
        Assert.True(hitBounds.Contains(visualBounds));
    }

    [Fact]
    public void ActiveAmount_RisesWhileActiveAndFallsExactlyToZeroWhenNot()
    {
        var motion = new UiButtonMotion();

        motion.Advance(
            isHovered: false,
            isFocused: false,
            isPressed: false,
            UiSecondaryMotion.ActiveStripDuration,
            MotionIntensity.Full,
            isActive: true);

        Assert.Equal(1f, motion.ActiveAmount);
        Assert.True(motion.IsActiveSettled);

        motion.Advance(
            isHovered: false,
            isFocused: false,
            isPressed: false,
            UiSecondaryMotion.ActiveStripDuration,
            MotionIntensity.Full,
            isActive: false);

        Assert.Equal(0f, motion.ActiveAmount);
        Assert.True(motion.IsActiveSettled);
    }

    [Fact]
    public void ActiveAmount_OffMotionSnapsInASingleCall()
    {
        var motion = new UiButtonMotion();

        motion.Advance(
            isHovered: false,
            isFocused: false,
            isPressed: false,
            TimeSpan.Zero,
            MotionIntensity.Off,
            isActive: true);

        Assert.Equal(1f, motion.ActiveAmount);
        Assert.True(motion.IsActiveSettled);
    }

    [Fact]
    public void ActiveAmount_MidTransitionLeavesTheStripUnsettled()
    {
        var motion = new UiButtonMotion();
        motion.Advance(
            isHovered: false,
            isFocused: false,
            isPressed: false,
            TimeSpan.Zero,
            MotionIntensity.Off,
            isActive: true);

        var halfDuration = TimeSpan.FromTicks(
            UiSecondaryMotion.ActiveStripDuration.Ticks / 2);
        motion.Advance(
            isHovered: false,
            isFocused: false,
            isPressed: false,
            halfDuration,
            MotionIntensity.Full,
            isActive: false);

        Assert.False(motion.IsActiveSettled);
        Assert.InRange(motion.ActiveAmount, 0.05f, 0.95f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(0.5f)]
    public void GetActiveStripColor_LerpsFromPanelBorderToSelection(
        float activeAmount)
    {
        var colors = new UiThemeColors(
            CanvasBackground: Color.Black,
            ArenaSurface: Color.Black,
            ArenaBorder: Color.Black,
            StatusSurface: Color.White,
            OverlayScrim: Color.White,
            PanelSurface: Color.Black,
            PanelAlternate: Color.Black,
            PanelBorder: Color.Red,
            TextPrimary: Color.White,
            TextSecondary: Color.White,
            TextDisabled: Color.Gray,
            TextInverse: Color.White,
            ActionDefault: Color.White,
            ActionHover: Color.White,
            ActionFocus: Color.White,
            ActionPressed: Color.White,
            ActionActive: Color.White,
            ActionDisabled: Color.Gray,
            StatusInfo: Color.White,
            StatusSuccess: Color.White,
            StatusWarning: Color.White,
            StatusDanger: Color.White,
            TeamA: Color.White,
            TeamB: Color.White,
            OtherFaction: Color.White,
            Selection: Color.Blue,
            NewEvent: Color.White);

        var color = UiButton.GetActiveStripColor(colors, activeAmount);

        Assert.Equal(
            Color.Lerp(colors.PanelBorder, colors.Selection, activeAmount),
            color);
    }
}
