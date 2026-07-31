using Hukbo.Client.Settings;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

public sealed class UiTransitionTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void AdvanceTo_ProgressesWithEaseOutAndSettlesExactly()
    {
        var transition = new UiTransition();

        transition.AdvanceTo(
            1f,
            TimeSpan.FromMilliseconds(50),
            Duration,
            isMotionEnabled: true);

        Assert.InRange(transition.Value, 0.5f, 0.999f);
        Assert.False(transition.IsSettled);

        transition.AdvanceTo(
            1f,
            TimeSpan.FromMilliseconds(50),
            Duration,
            isMotionEnabled: true);

        Assert.Equal(1f, transition.Value);
        Assert.True(transition.IsSettled);
    }

    [Fact]
    public void AdvanceTo_LargeDeltaSettlesWithoutOverflow()
    {
        var transition = new UiTransition();

        transition.AdvanceTo(
            1f,
            TimeSpan.MaxValue,
            Duration,
            isMotionEnabled: true);

        Assert.Equal(1f, transition.Value);
        Assert.True(transition.IsSettled);
    }

    [Fact]
    public void AdvanceTo_MotionDisabledSnapsToTarget()
    {
        var transition = new UiTransition();

        transition.AdvanceTo(
            1f,
            TimeSpan.Zero,
            Duration,
            isMotionEnabled: false);

        Assert.Equal(1f, transition.Value);
        Assert.True(transition.IsSettled);
    }

    [Fact]
    public void AdvanceTo_ReversalStartsFromTheCurrentValue()
    {
        var transition = new UiTransition();
        transition.AdvanceTo(
            1f,
            TimeSpan.FromMilliseconds(25),
            Duration,
            isMotionEnabled: true);
        var valueBeforeReversal = transition.Value;

        transition.AdvanceTo(
            0f,
            TimeSpan.Zero,
            Duration,
            isMotionEnabled: true);

        Assert.Equal(valueBeforeReversal, transition.Value);

        transition.AdvanceTo(
            0f,
            Duration,
            Duration,
            isMotionEnabled: true);

        Assert.Equal(0f, transition.Value);
        Assert.True(transition.IsSettled);
    }

    [Fact]
    public void EntranceMotion_OffSnapsScrimAndPanelToVisible()
    {
        var entrance = new UiEntranceMotion();
        entrance.Begin();

        entrance.Advance(
            TimeSpan.Zero,
            MotionIntensity.Off,
            UiEntranceMotion.ModalPanelDuration,
            hasScrim: true);

        Assert.Equal(1f, entrance.ScrimOpacity);
        Assert.Equal(1f, entrance.PanelOpacity);
    }

    [Fact]
    public void EntranceMotion_ReducedUsesBoundedOpacityAndSettles()
    {
        var entrance = new UiEntranceMotion();
        entrance.Begin();

        entrance.Advance(
            TimeSpan.FromMilliseconds(40),
            MotionIntensity.Reduced,
            UiEntranceMotion.ModalPanelDuration,
            hasScrim: true);

        Assert.InRange(entrance.ScrimOpacity, 0.001f, 0.999f);
        Assert.InRange(entrance.PanelOpacity, 0.001f, 0.999f);

        entrance.Advance(
            TimeSpan.MaxValue,
            MotionIntensity.Reduced,
            UiEntranceMotion.ModalPanelDuration,
            hasScrim: true);

        Assert.Equal(1f, entrance.ScrimOpacity);
        Assert.Equal(1f, entrance.PanelOpacity);
    }
}
