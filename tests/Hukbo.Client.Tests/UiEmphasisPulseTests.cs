using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

public sealed class UiEmphasisPulseTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void Trigger_MotionOffProducesNoVisibleAmount()
    {
        var pulse = new UiEmphasisPulse();

        pulse.Trigger(isMotionEnabled: false);

        Assert.Equal(0f, pulse.Amount);
        Assert.True(pulse.IsSettled);
    }

    [Fact]
    public void Trigger_MotionEnabledJumpsToOneThenAdvanceDecays()
    {
        var pulse = new UiEmphasisPulse();

        pulse.Trigger(isMotionEnabled: true);

        Assert.Equal(1f, pulse.Amount);

        pulse.Advance(
            TimeSpan.FromMilliseconds(100),
            Duration,
            isMotionEnabled: true);

        Assert.InRange(pulse.Amount, 0.001f, 0.999f);
        Assert.False(pulse.IsSettled);
    }

    [Fact]
    public void Advance_NeverRaisesTheAmount()
    {
        var pulse = new UiEmphasisPulse();
        pulse.Trigger(isMotionEnabled: true);
        pulse.Advance(
            TimeSpan.FromMilliseconds(50),
            Duration,
            isMotionEnabled: true);
        var amountAfterFirstAdvance = pulse.Amount;

        pulse.Advance(
            TimeSpan.FromMilliseconds(50),
            Duration,
            isMotionEnabled: true);

        Assert.True(pulse.Amount <= amountAfterFirstAdvance);
    }

    [Fact]
    public void Advance_SettledPulseStaysSettledWithoutFurtherTrigger()
    {
        var pulse = new UiEmphasisPulse();
        pulse.Trigger(isMotionEnabled: true);
        pulse.Advance(TimeSpan.MaxValue, Duration, isMotionEnabled: true);

        Assert.Equal(0f, pulse.Amount);
        Assert.True(pulse.IsSettled);

        pulse.Advance(
            TimeSpan.FromMilliseconds(16),
            Duration,
            isMotionEnabled: true);

        Assert.Equal(0f, pulse.Amount);
        Assert.True(pulse.IsSettled);
    }

    [Fact]
    public void Advance_LargeFrameDeltaSettlesExactlyAtZero()
    {
        var pulse = new UiEmphasisPulse();
        pulse.Trigger(isMotionEnabled: true);

        pulse.Advance(TimeSpan.MaxValue, Duration, isMotionEnabled: true);

        Assert.Equal(0f, pulse.Amount);
        Assert.True(pulse.IsSettled);
    }

    [Fact]
    public void Reset_ClearsAmountToZeroAndSettled()
    {
        var pulse = new UiEmphasisPulse();
        pulse.Trigger(isMotionEnabled: true);

        pulse.Reset();

        Assert.Equal(0f, pulse.Amount);
        Assert.True(pulse.IsSettled);
    }
}
