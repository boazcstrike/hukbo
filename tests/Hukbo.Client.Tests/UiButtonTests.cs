using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
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
}
