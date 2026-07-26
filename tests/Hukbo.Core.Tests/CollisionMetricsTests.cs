using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class CollisionMetricsTests
{
    [Fact]
    public void FreshAccumulator_ReportsEveryFieldAsZero()
    {
        var accumulator = default(CollisionMetricsAccumulator);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(default, metrics);
        Assert.Equal(0L, metrics.CandidatePairs);
        Assert.Equal(0L, metrics.ContactPairs);
        Assert.Equal(0L, metrics.AcceptedMoves);
        Assert.Equal(0L, metrics.BlockedAgentTicks);
        Assert.Equal(0L, metrics.AttackCapableAgentTicks);
        Assert.Equal(0, metrics.LongestBlockedStreakTicks);
        Assert.Equal(0, metrics.MaximumFrontWidthRaw);
        Assert.Equal(0, metrics.MaximumFrontDepthRaw);
        Assert.Equal(0, metrics.MaximumPenetrationRaw);
    }

    [Fact]
    public void AddTick_SumsTheRunningTotalsAcrossTicks()
    {
        var accumulator = default(CollisionMetricsAccumulator);

        accumulator.AddTick(
            candidatePairs: 10,
            contactPairs: 4,
            acceptedMoves: 7,
            blockedAgents: 3,
            attackCapableAgents: 12,
            frontWidthRaw: 100,
            frontDepthRaw: 40,
            penetrationRaw: 0);
        accumulator.AddTick(
            candidatePairs: 5,
            contactPairs: 1,
            acceptedMoves: 9,
            blockedAgents: 2,
            attackCapableAgents: 11,
            frontWidthRaw: 60,
            frontDepthRaw: 90,
            penetrationRaw: 0);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(15L, metrics.CandidatePairs);
        Assert.Equal(5L, metrics.ContactPairs);
        Assert.Equal(16L, metrics.AcceptedMoves);
        Assert.Equal(5L, metrics.BlockedAgentTicks);
        Assert.Equal(23L, metrics.AttackCapableAgentTicks);
    }

    [Fact]
    public void AddTick_KeepsFrontAndPenetrationAsRunningMaximaNotSums()
    {
        var accumulator = default(CollisionMetricsAccumulator);

        accumulator.AddTick(0, 0, 0, 0, 0, frontWidthRaw: 100, frontDepthRaw: 40, penetrationRaw: 3);
        accumulator.AddTick(0, 0, 0, 0, 0, frontWidthRaw: 60, frontDepthRaw: 90, penetrationRaw: 1);
        accumulator.AddTick(0, 0, 0, 0, 0, frontWidthRaw: 80, frontDepthRaw: 20, penetrationRaw: 2);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(100, metrics.MaximumFrontWidthRaw);
        Assert.Equal(90, metrics.MaximumFrontDepthRaw);
        Assert.Equal(3, metrics.MaximumPenetrationRaw);
    }

    [Fact]
    public void ObserveBlockedStreak_KeepsTheLongestStreakSeen()
    {
        var accumulator = default(CollisionMetricsAccumulator);

        accumulator.ObserveBlockedStreak(4);
        accumulator.ObserveBlockedStreak(11);
        accumulator.ObserveBlockedStreak(2);

        Assert.Equal(11, accumulator.ToMetrics().LongestBlockedStreakTicks);
    }

    [Fact]
    public void ObserveBlockedStreak_DoesNotDisturbTheRunningTotals()
    {
        var accumulator = default(CollisionMetricsAccumulator);

        accumulator.AddTick(1, 2, 3, 4, 5, 6, 7, 0);
        accumulator.ObserveBlockedStreak(9);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(1L, metrics.CandidatePairs);
        Assert.Equal(2L, metrics.ContactPairs);
        Assert.Equal(3L, metrics.AcceptedMoves);
        Assert.Equal(4L, metrics.BlockedAgentTicks);
        Assert.Equal(5L, metrics.AttackCapableAgentTicks);
        Assert.Equal(9, metrics.LongestBlockedStreakTicks);
    }

    [Fact]
    public void Reset_RestoresTheStateOfAFreshlyConstructedAccumulator()
    {
        var accumulator = default(CollisionMetricsAccumulator);
        accumulator.AddTick(10, 9, 8, 7, 6, 5, 4, 3);
        accumulator.ObserveBlockedStreak(12);

        accumulator.Reset();

        Assert.Equal(default(CollisionMetricsAccumulator).ToMetrics(), accumulator.ToMetrics());
    }

    [Fact]
    public void Reset_AllowsAccumulationToStartAgainFromZero()
    {
        var accumulator = default(CollisionMetricsAccumulator);
        accumulator.AddTick(10, 9, 8, 7, 6, 5, 4, 3);
        accumulator.ObserveBlockedStreak(12);

        accumulator.Reset();
        accumulator.AddTick(1, 1, 1, 1, 1, 1, 1, 0);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(1L, metrics.CandidatePairs);
        Assert.Equal(1, metrics.MaximumFrontWidthRaw);
        Assert.Equal(0, metrics.MaximumPenetrationRaw);
        Assert.Equal(0, metrics.LongestBlockedStreakTicks);
    }

    [Theory]
    [InlineData(0, "candidatePairs")]
    [InlineData(1, "contactPairs")]
    [InlineData(2, "acceptedMoves")]
    [InlineData(3, "blockedAgents")]
    [InlineData(4, "attackCapableAgents")]
    [InlineData(5, "frontWidthRaw")]
    [InlineData(6, "frontDepthRaw")]
    [InlineData(7, "penetrationRaw")]
    public void AddTick_RejectsANegativeArgument(int negativeIndex, string parameterName)
    {
        var values = new int[8];
        values[negativeIndex] = -1;
        var accumulator = default(CollisionMetricsAccumulator);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.AddTick(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7]));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void AddTick_LeavesTheAccumulatorUnchangedWhenAnArgumentIsRejected()
    {
        var accumulator = default(CollisionMetricsAccumulator);
        accumulator.AddTick(3, 3, 3, 3, 3, 3, 3, 0);
        var before = accumulator.ToMetrics();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.AddTick(1, 1, 1, 1, 1, 1, 1, -1));

        Assert.Equal(before, accumulator.ToMetrics());
    }

    [Fact]
    public void ObserveBlockedStreak_RejectsANegativeStreak()
    {
        var accumulator = default(CollisionMetricsAccumulator);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.ObserveBlockedStreak(-1));

        Assert.Equal("streakTicks", exception.ParamName);
    }

    [Fact]
    public void AddTick_AccumulatesBeyondIntegerRangeAcrossManyTicks()
    {
        var accumulator = default(CollisionMetricsAccumulator);
        const int TickCount = 10_000;

        for (var tick = 0; tick < TickCount; tick++)
        {
            accumulator.AddTick(
                candidatePairs: int.MaxValue,
                contactPairs: int.MaxValue,
                acceptedMoves: int.MaxValue,
                blockedAgents: int.MaxValue,
                attackCapableAgents: int.MaxValue,
                frontWidthRaw: int.MaxValue,
                frontDepthRaw: int.MaxValue,
                penetrationRaw: 0);
        }

        var metrics = accumulator.ToMetrics();
        var expected = (long)int.MaxValue * TickCount;

        Assert.Equal(expected, metrics.CandidatePairs);
        Assert.Equal(expected, metrics.ContactPairs);
        Assert.Equal(expected, metrics.AcceptedMoves);
        Assert.Equal(expected, metrics.BlockedAgentTicks);
        Assert.Equal(expected, metrics.AttackCapableAgentTicks);
        Assert.True(expected > int.MaxValue);
        Assert.Equal(int.MaxValue, metrics.MaximumFrontWidthRaw);
    }

    [Fact]
    public void ToMetrics_IsRepeatableAndDoesNotConsumeTheAccumulator()
    {
        var accumulator = default(CollisionMetricsAccumulator);
        accumulator.AddTick(6, 5, 4, 3, 2, 1, 1, 0);
        accumulator.ObserveBlockedStreak(7);

        var first = accumulator.ToMetrics();
        var second = accumulator.ToMetrics();

        Assert.Equal(first, second);
    }
}
