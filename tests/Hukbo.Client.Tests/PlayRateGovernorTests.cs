using Hukbo.Client.Presentation;

namespace Hukbo.Client.Tests;

/// <summary>
/// The governor decides how fast a heavy battle plays, so what these tests pin
/// is the shape of that decision: it holds back only when the machine cannot
/// keep up, it falls fast and recovers slowly, and it never stops the battle.
/// Costs are synthetic seconds per tick; no clock is read.
/// </summary>
public sealed class PlayRateGovernorTests
{
    private const double FrameSeconds = 1d / 60d;
    private const int TickRate = 20;

    // The cost per tick at which the half-frame budget sustains exactly
    // `speed`x: 0.5 / (speed * TickRate).
    private static double CostForSustainableSpeed(double speed) =>
        PlayRateGovernor.FrameBudgetShare / (speed * TickRate);

    private static double RunFrames(
        PlayRateGovernor governor,
        int frames,
        double costSecondsPerTick,
        int requestedSpeed)
    {
        var effective = governor.EffectiveSpeed;
        for (var frame = 0; frame < frames; frame++)
        {
            effective = governor.Update(
                FrameSeconds,
                costSecondsPerTick,
                ticksRun: 1,
                requestedSpeed,
                TickRate);
        }

        return effective;
    }

    private static int FramesUntil(
        PlayRateGovernor governor,
        double costSecondsPerTick,
        int requestedSpeed,
        Func<double, bool> reached)
    {
        const int Limit = 6_000;
        for (var frame = 1; frame <= Limit; frame++)
        {
            var effective = governor.Update(
                FrameSeconds,
                costSecondsPerTick,
                ticksRun: 1,
                requestedSpeed,
                TickRate);
            if (reached(effective))
            {
                return frame;
            }
        }

        throw new InvalidOperationException("The governor never reached the target.");
    }

    [Fact]
    public void ACheapTickNeverGovernsBelowTheRequestedSpeed()
    {
        var governor = new PlayRateGovernor();

        var effective = RunFrames(governor, 600, costSecondsPerTick: 0.0001d, requestedSpeed: 4);

        Assert.Equal(4d, effective);
        Assert.False(governor.IsGoverned);
    }

    [Fact]
    public void ACostAboveTheBudgetConvergesToTheSustainableSpeed()
    {
        var governor = new PlayRateGovernor();

        var effective = RunFrames(
            governor,
            600,
            CostForSustainableSpeed(2d),
            requestedSpeed: 4);

        Assert.Equal(2d, effective, 2);
        Assert.True(governor.IsGoverned);
    }

    [Fact]
    public void AnAffordableRequestIsNotGovernedEvenWhenFasterIsUnaffordable()
    {
        var governor = new PlayRateGovernor();

        var effective = RunFrames(
            governor,
            600,
            CostForSustainableSpeed(3d),
            requestedSpeed: 2);

        Assert.Equal(2d, effective);
        Assert.False(governor.IsGoverned);
    }

    [Fact]
    public void RecoveryIsSlowerThanTheFall()
    {
        var cost = CostForSustainableSpeed(1d);
        var falling = new PlayRateGovernor();
        var fallFrames = FramesUntil(falling, cost, 4, effective => effective <= 2.5d);

        var rising = new PlayRateGovernor();
        RunFrames(rising, 1_200, cost, requestedSpeed: 4);
        var riseFrames = FramesUntil(rising, 0.0001d, 4, effective => effective >= 2.5d);

        Assert.True(
            riseFrames > fallFrames * 3,
            $"fell in {fallFrames} frames but rose in {riseFrames}");
    }

    [Fact]
    public void TheFloorHoldsWhateverATickCosts()
    {
        var governor = new PlayRateGovernor();
        var lowest = double.MaxValue;

        for (var frame = 0; frame < 600; frame++)
        {
            var effective = governor.Update(
                FrameSeconds,
                simulationWorkSeconds: 10d,
                ticksRun: 1,
                requestedSpeed: 4,
                TickRate);
            lowest = Math.Min(lowest, effective);
        }

        Assert.True(lowest >= PlayRateGovernor.MinimumSpeed);
        Assert.Equal(PlayRateGovernor.MinimumSpeed, governor.EffectiveSpeed, 3);
    }

    [Fact]
    public void AFrameWithZeroTicksLeavesTheStateUnchanged()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, CostForSustainableSpeed(2d), requestedSpeed: 4);
        var effective = governor.EffectiveSpeed;
        var cost = governor.SmoothedCostSecondsPerTick;

        for (var frame = 0; frame < 600; frame++)
        {
            governor.Update(
                FrameSeconds,
                simulationWorkSeconds: 5d,
                ticksRun: 0,
                requestedSpeed: 4,
                TickRate);
        }

        Assert.Equal(effective, governor.EffectiveSpeed);
        Assert.Equal(cost, governor.SmoothedCostSecondsPerTick);
        Assert.True(governor.IsGoverned);
    }

    [Fact]
    public void TicksOnOneFrameInThreeGovernAsFastAsTicksOnEveryFrame()
    {
        const int Frames = 60;
        var cost = CostForSustainableSpeed(1d);
        var everyFrame = new PlayRateGovernor();
        var oneInThree = new PlayRateGovernor();

        for (var frame = 0; frame < Frames; frame++)
        {
            everyFrame.Update(FrameSeconds, cost, 1, 4, TickRate);
            var ticks = frame % 3 == 2 ? 1 : 0;
            oneInThree.Update(FrameSeconds, ticks * cost, ticks, 4, TickRate);
        }

        Assert.InRange(
            oneInThree.EffectiveSpeed,
            everyFrame.EffectiveSpeed - 0.01d,
            everyFrame.EffectiveSpeed + 0.01d);
    }

    [Fact]
    public void OneHitchFrameMovesTheSmoothedCostByNoMoreThanTheCappedWeight()
    {
        const double Steady = 0.005d;
        const double Spike = 0.05d;
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, Steady, requestedSpeed: 4);

        governor.Update(0.5d, Spike, ticksRun: 1, requestedSpeed: 4, TickRate);

        // The capped weight is 1 - exp(-0.1 / 0.25), about 0.33; an uncapped
        // 0.5 s frame would carry 0.86 and land near 0.044.
        Assert.InRange(governor.SmoothedCostSecondsPerTick, Steady, 0.02d);
    }

    [Fact]
    public void OneHitchFrameDoesNotCollapseTheGovernedSpeed()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, CostForSustainableSpeed(4d), requestedSpeed: 4);

        var effective = governor.Update(
            0.5d,
            simulationWorkSeconds: 1d,
            ticksRun: 1,
            requestedSpeed: 4,
            TickRate);

        Assert.True(effective > 2.5d, $"one hitch frame dropped the speed to {effective}");
    }

    [Fact]
    public void ASustainableSpeedHoveringAtTheThresholdDoesNotFlipTheGovernedFlag()
    {
        // Three frames in every 120 can sustain only 3.0x, the rest 3.9x: the
        // governed speed hovers near 3.8x, the 5% line for a request of 4x. An
        // unlatched flag flipped four times over this run; the latch flips once.
        const int PeriodFrames = 120;
        const int SlowFrames = 3;
        var governor = new PlayRateGovernor();
        var transitions = 0;
        var previous = governor.IsGoverned;

        for (var frame = 0; frame < 6_000; frame++)
        {
            var speed = frame % PeriodFrames < SlowFrames ? 3.0d : 3.9d;
            governor.Update(FrameSeconds, CostForSustainableSpeed(speed), 1, 4, TickRate);
            if (governor.IsGoverned != previous)
            {
                transitions++;
                previous = governor.IsGoverned;
            }
        }

        Assert.InRange(transitions, 0, 1);
    }

    [Fact]
    public void TheGovernedFlagClearsOnlyOnceTheSpeedIsBackWithinTwoPercent()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, CostForSustainableSpeed(1d), requestedSpeed: 4);
        Assert.True(governor.IsGoverned);

        governor.Update(FrameSeconds, 0d, 0, requestedSpeed: 1, TickRate);
        Assert.False(governor.IsGoverned);

        governor.Update(FrameSeconds, 0d, 0, requestedSpeed: 4, TickRate);
        Assert.True(governor.IsGoverned);
    }

    [Fact]
    public void AReclampWithNoSampleKeepsTheCostHistory()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, CostForSustainableSpeed(2d), requestedSpeed: 4);
        var cost = governor.SmoothedCostSecondsPerTick;

        var lowered = governor.Update(0d, 0d, 0, requestedSpeed: 1, TickRate);
        var raised = governor.Update(0d, 0d, 0, requestedSpeed: 4, TickRate);

        Assert.Equal(1d, lowered);
        Assert.Equal(2d, raised, 2);
        Assert.True(governor.IsGoverned);
        Assert.Equal(cost, governor.SmoothedCostSecondsPerTick);
    }

    [Fact]
    public void ChangingTheRequestedSpeedAppliesAtOnceWithoutASlowClimb()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, CostForSustainableSpeed(3d), requestedSpeed: 1);

        var faster = governor.Update(FrameSeconds, CostForSustainableSpeed(3d), 1, 4, TickRate);
        var slower = governor.Update(FrameSeconds, CostForSustainableSpeed(3d), 1, 1, TickRate);

        Assert.InRange(faster, 2.9d, 3.1d);
        Assert.Equal(1d, slower);
        Assert.False(governor.IsGoverned);
    }

    [Fact]
    public void ZeroTicksStillClampsToALoweredRequest()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 60, 0.0001d, requestedSpeed: 4);

        var effective = governor.Update(FrameSeconds, 0d, 0, requestedSpeed: 2, TickRate);

        Assert.Equal(2d, effective);
    }

    [Fact]
    public void ResetForgetsTheHistory()
    {
        var governor = new PlayRateGovernor();
        RunFrames(governor, 600, CostForSustainableSpeed(1d), requestedSpeed: 4);
        Assert.True(governor.IsGoverned);

        governor.Reset();

        Assert.Equal(1d, governor.EffectiveSpeed);
        Assert.False(governor.IsGoverned);
        Assert.Equal(0d, governor.SmoothedCostSecondsPerTick);
        Assert.Equal(4d, governor.Update(FrameSeconds, 0.0001d, 1, 4, TickRate));
    }

    [Fact]
    public void NegativeInputsAreRejected()
    {
        var governor = new PlayRateGovernor();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => governor.Update(-1d, 0d, 1, 1, TickRate));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => governor.Update(FrameSeconds, -1d, 1, 1, TickRate));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => governor.Update(FrameSeconds, 0d, -1, 1, TickRate));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => governor.Update(FrameSeconds, 0d, 1, 0, TickRate));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => governor.Update(FrameSeconds, 0d, 1, 1, 0));
    }
}
