using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Behavioral coverage for <see cref="RallyOffset"/>: stability, tick
/// independence, the jitter-square bound, distinctness, seed sensitivity,
/// and freedom from the low-corner bias a naive modulo draw would introduce.
/// </summary>
public sealed class RallyOffsetTests
{
    private const int BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw;

    [Fact]
    public void OffsetIsStableAcrossRepeatedCallsForTheSameSeedAndEntity()
    {
        var first = RallyOffset.Compute(seed: 1, entityId: 42, BodyRadiusRaw);
        var second = RallyOffset.Compute(seed: 1, entityId: 42, BodyRadiusRaw);

        Assert.Equal(first, second);
    }

    [Fact]
    public void OffsetDoesNotDependOnTheTick()
    {
        var scenario = Scenario.CreateDefault(seed: 7);
        var simulation = BattleSimulation.Create(scenario);
        var entityId = simulation.Agents[0].EntityId;

        var atStart = RallyOffset.Compute(
            scenario.Seed,
            entityId,
            scenario.BodyRadiusRaw);

        AdvanceToTick(simulation, 1);
        var atTickOne = RallyOffset.Compute(
            scenario.Seed,
            entityId,
            scenario.BodyRadiusRaw);

        AdvanceToTick(simulation, 50);
        var atTickFifty = RallyOffset.Compute(
            scenario.Seed,
            entityId,
            scenario.BodyRadiusRaw);

        AdvanceToTick(simulation, 200);
        var atTickTwoHundred = RallyOffset.Compute(
            scenario.Seed,
            entityId,
            scenario.BodyRadiusRaw);

        Assert.Equal(atStart, atTickOne);
        Assert.Equal(atStart, atTickFifty);
        Assert.Equal(atStart, atTickTwoHundred);
    }

    [Fact]
    public void EveryOffsetInASweepOfTenThousandEntitiesStaysInsideTheJitterSquare()
    {
        const int SweepCount = 10_000;
        var jitter = FormationRules.ComputeRallyJitterRaw(BodyRadiusRaw);

        for (ulong entityId = 1; entityId <= SweepCount; entityId++)
        {
            var (xRaw, yRaw) = RallyOffset.Compute(seed: 3, entityId, BodyRadiusRaw);

            Assert.InRange(xRaw, -jitter, jitter);
            Assert.InRange(yRaw, -jitter, jitter);
        }
    }

    [Fact]
    public void ASweepOfOneThousandEntitiesProducesAtLeastNineHundredDistinctOffsets()
    {
        const int SweepCount = 1_000;
        const int MinimumDistinctOffsets = 900;
        var offsets = new HashSet<(int XRaw, int YRaw)>();

        for (ulong entityId = 1; entityId <= SweepCount; entityId++)
        {
            offsets.Add(RallyOffset.Compute(seed: 13, entityId, BodyRadiusRaw));
        }

        Assert.True(
            offsets.Count >= MinimumDistinctOffsets,
            $"Expected at least {MinimumDistinctOffsets} distinct offsets " +
            $"across {SweepCount} entities, got {offsets.Count}.");
    }

    [Fact]
    public void DifferentSeedsProduceDifferentOffsetsForTheSameEntity()
    {
        var first = RallyOffset.Compute(seed: 1, entityId: 99, BodyRadiusRaw);
        var second = RallyOffset.Compute(seed: 2, entityId: 99, BodyRadiusRaw);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void OffsetsAreSymmetricallyDistributedAboutZeroWithinATolerance()
    {
        const int SweepCount = 10_000;

        // The failure mode this guards against is a naive `value % span`
        // draw without the low-order-bit rejection sampling SplitMix64.NextInt
        // already performs; that failure skews the mean toward the low
        // corner by a large fraction of the jitter radius. 5% of the maximum
        // possible per-axis sum (SweepCount * jitter) is generous headroom
        // for a correctly centered draw while still catching that bias.
        const double ToleranceFraction = 0.05;
        var jitter = FormationRules.ComputeRallyJitterRaw(BodyRadiusRaw);

        long xSum = 0;
        long ySum = 0;
        for (ulong entityId = 1; entityId <= SweepCount; entityId++)
        {
            var (xRaw, yRaw) = RallyOffset.Compute(seed: 5, entityId, BodyRadiusRaw);
            xSum += xRaw;
            ySum += yRaw;
        }

        var maxAllowedAbsoluteSum = (long)(SweepCount * (double)jitter * ToleranceFraction);

        Assert.True(
            Math.Abs(xSum) <= maxAllowedAbsoluteSum,
            $"X-axis sum {xSum} exceeded the {maxAllowedAbsoluteSum} tolerance.");
        Assert.True(
            Math.Abs(ySum) <= maxAllowedAbsoluteSum,
            $"Y-axis sum {ySum} exceeded the {maxAllowedAbsoluteSum} tolerance.");
    }

    private static void AdvanceToTick(BattleSimulation simulation, long targetTick)
    {
        while (simulation.Tick < targetTick &&
               simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
        }
    }
}
