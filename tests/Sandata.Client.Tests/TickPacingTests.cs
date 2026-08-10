using System;
using Sandata.Client.Simulation;

namespace Sandata.Client.Tests;

/// <summary>
/// <see cref="TickPacing"/>'s bar: a frame earns whole 20-millisecond ticks
/// and nothing else, the remainder survives to the next frame so a 60 Hz
/// display does not run the simulation slow, the speed fraction scales real
/// time rather than tick length, and a stalled frame cannot bank an unbounded
/// backlog.
/// </summary>
public sealed class TickPacingTests
{
    private const long SixtyHertzFrameMicroseconds = 16_667;

    [Fact]
    public void Advance_ExactlyOneTickOfRealTime_EarnsOneTickAndKeepsNoRemainder()
    {
        var (remaining, ticks) = TickPacing.Advance(
            accumulatedMicroseconds: 0,
            elapsedMicroseconds: TickPacing.MicrosecondsPerTick,
            speedNumerator: 1,
            speedDenominator: 1,
            maxTicksPerFrame: TickPacing.DefaultMaxTicksPerFrame);

        Assert.Equal(1, ticks);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void Advance_ShorterThanOneTick_EarnsNothingAndBanksTheWholeFrame()
    {
        var (remaining, ticks) = TickPacing.Advance(0, 12_000, 1, 1, TickPacing.DefaultMaxTicksPerFrame);

        Assert.Equal(0, ticks);
        Assert.Equal(12_000, remaining);
    }

    /// <summary>
    /// The reason this type counts in microseconds rather than milliseconds.
    /// A 60 Hz frame is 16,667 µs, which is five sixths of a tick; three such
    /// frames are 50,001 µs and must earn exactly two ticks. Truncating each
    /// frame to 16 ms instead would lose four percent of elapsed time on every
    /// frame, and one simulated minute would run about two and a half seconds
    /// short.
    /// </summary>
    [Fact]
    public void Advance_ThreeSixtyHertzFrames_EarnTwoTicksInTotal()
    {
        var accumulated = 0L;
        var total = 0;

        for (var frame = 0; frame < 3; frame++)
        {
            int ticks;
            (accumulated, ticks) = TickPacing.Advance(
                accumulated, SixtyHertzFrameMicroseconds, 1, 1, TickPacing.DefaultMaxTicksPerFrame);
            total += ticks;
        }

        Assert.Equal(2, total);

        // 50,001 µs of real time, less the 40,000 µs the two earned ticks
        // consumed, leaves 10,001 µs banked for the next frame.
        Assert.Equal(10_001, accumulated);
    }

    /// <summary>
    /// One simulated second is 50 ticks at a <c>TickRate</c> of 50, and it has
    /// to come out of 60 Hz frames as exactly 50 — not 49 and not 51 — or the
    /// simulation clock drifts against the wall clock a spectator is watching.
    /// </summary>
    [Fact]
    public void Advance_OneSecondOfSixtyHertzFrames_EarnsFiftyTicks()
    {
        var accumulated = 0L;
        var total = 0;

        for (var frame = 0; frame < 60; frame++)
        {
            int ticks;
            (accumulated, ticks) = TickPacing.Advance(
                accumulated, SixtyHertzFrameMicroseconds, 1, 1, TickPacing.DefaultMaxTicksPerFrame);
            total += ticks;
        }

        Assert.Equal(50, total);
    }

    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 4)]
    [InlineData(4, 1, 8)]
    public void Advance_ScalesEarnedTicksByTheSpeedFraction(
        int numerator, int denominator, int expectedTicks)
    {
        var (_, ticks) = TickPacing.Advance(
            accumulatedMicroseconds: 0,
            elapsedMicroseconds: TickPacing.MicrosecondsPerTick * 2,
            speedNumerator: numerator,
            speedDenominator: denominator,
            maxTicksPerFrame: TickPacing.DefaultMaxTicksPerFrame);

        Assert.Equal(expectedTicks, ticks);
    }

    /// <summary>
    /// The spiral-of-death guard. A frame that stalled for two whole seconds
    /// has earned a hundred ticks; the ceiling hands back only its own number,
    /// and — the part that actually matters — the accumulator comes back below
    /// one tick so the *next* frame starts level rather than inheriting the
    /// unpaid ninety-two.
    /// </summary>
    [Fact]
    public void Advance_AFrameFarAboveTheCeiling_DiscardsTheSurplusRatherThanBankingIt()
    {
        var (remaining, ticks) = TickPacing.Advance(
            accumulatedMicroseconds: 0,
            elapsedMicroseconds: 2_000_000,
            speedNumerator: 1,
            speedDenominator: 1,
            maxTicksPerFrame: TickPacing.DefaultMaxTicksPerFrame);

        Assert.Equal(TickPacing.DefaultMaxTicksPerFrame, ticks);
        Assert.True(
            remaining < TickPacing.MicrosecondsPerTick,
            $"a clamped frame must not bank a backlog, but {remaining} µs survived, " +
            $"which is {remaining / TickPacing.MicrosecondsPerTick} whole ticks of debt.");
    }

    [Fact]
    public void Advance_ZeroElapsed_EarnsNothingAndChangesNothing()
    {
        var (remaining, ticks) = TickPacing.Advance(7_500, 0, 1, 1, TickPacing.DefaultMaxTicksPerFrame);

        Assert.Equal(0, ticks);
        Assert.Equal(7_500, remaining);
    }

    [Fact]
    public void Advance_RejectsANegativeAccumulatorAndANonPositiveSpeedOrCeiling()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TickPacing.Advance(-1, 0, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TickPacing.Advance(0, -1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TickPacing.Advance(0, 0, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TickPacing.Advance(0, 0, 1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TickPacing.Advance(0, 0, 1, 1, 0));
    }
}
