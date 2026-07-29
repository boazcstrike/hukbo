using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Behavioral coverage for <see cref="ContingentOffset"/>: stability, tick
/// independence, the jitter-square bound, distinctness, seed sensitivity,
/// freedom from the low-corner bias a naive modulo draw would introduce, tag
/// distinctness from every other domain tag in the repository, and
/// call-count independence — mirroring
/// <c>tests/Hukbo.Core.Tests/RallyOffsetTests.cs:14-101</c>.
/// </summary>
public sealed class ContingentOffsetTests
{
    private const int BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw;
    private const int LivingCount = 8;

    private static int JitterRaw =>
        FormationRules.ComputeContingentJitterRaw(BodyRadiusRaw, LivingCount);

    [Fact]
    public void OffsetIsStableAcrossRepeatedCallsForTheSameSeedAndEntity()
    {
        var jitter = JitterRaw;

        var first = ContingentOffset.Compute(seed: 1, entityId: 42, jitter);
        var second = ContingentOffset.Compute(seed: 1, entityId: 42, jitter);

        Assert.Equal(first, second);
    }

    [Fact]
    public void OffsetDoesNotDependOnTheTick()
    {
        var scenario = Scenario.CreateDefault(seed: 7);
        var simulation = BattleSimulation.Create(scenario);
        var entityId = simulation.Agents[0].EntityId;
        var jitter = JitterRaw;

        var atStart = ContingentOffset.Compute(scenario.Seed, entityId, jitter);

        AdvanceToTick(simulation, 1);
        var atTickOne = ContingentOffset.Compute(scenario.Seed, entityId, jitter);

        AdvanceToTick(simulation, 50);
        var atTickFifty = ContingentOffset.Compute(scenario.Seed, entityId, jitter);

        AdvanceToTick(simulation, 200);
        var atTickTwoHundred = ContingentOffset.Compute(scenario.Seed, entityId, jitter);

        Assert.Equal(atStart, atTickOne);
        Assert.Equal(atStart, atTickFifty);
        Assert.Equal(atStart, atTickTwoHundred);
    }

    [Fact]
    public void EveryOffsetInASweepOfTenThousandEntitiesStaysInsideTheJitterSquare()
    {
        const int SweepCount = 10_000;
        var jitter = JitterRaw;

        for (ulong entityId = 1; entityId <= SweepCount; entityId++)
        {
            var (xRaw, yRaw) = ContingentOffset.Compute(seed: 3, entityId, jitter);

            Assert.InRange(xRaw, -jitter, jitter);
            Assert.InRange(yRaw, -jitter, jitter);
        }
    }

    [Fact]
    public void ASweepOfOneThousandEntitiesProducesAtLeastNineHundredDistinctOffsets()
    {
        const int SweepCount = 1_000;
        const int MinimumDistinctOffsets = 900;
        var jitter = JitterRaw;
        var offsets = new HashSet<(int XRaw, int YRaw)>();

        for (ulong entityId = 1; entityId <= SweepCount; entityId++)
        {
            offsets.Add(ContingentOffset.Compute(seed: 13, entityId, jitter));
        }

        Assert.True(
            offsets.Count >= MinimumDistinctOffsets,
            $"Expected at least {MinimumDistinctOffsets} distinct offsets " +
            $"across {SweepCount} entities, got {offsets.Count}.");
    }

    [Fact]
    public void DifferentSeedsProduceDifferentOffsetsForTheSameEntity()
    {
        var jitter = JitterRaw;

        var first = ContingentOffset.Compute(seed: 1, entityId: 99, jitter);
        var second = ContingentOffset.Compute(seed: 2, entityId: 99, jitter);

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
        var jitter = JitterRaw;

        long xSum = 0;
        long ySum = 0;
        for (ulong entityId = 1; entityId <= SweepCount; entityId++)
        {
            var (xRaw, yRaw) = ContingentOffset.Compute(seed: 5, entityId, jitter);
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

    [Fact]
    public void ContingentTagDiffersFromEveryOtherDomainTagInTheRepository()
    {
        // Every other Fnv1a-seeded domain tag declared in src/Hukbo.Core, so
        // that ContingentOffset's own tag (0x484B424F5F435447, "HKBO_CTG")
        // provably never collides with an unrelated deterministic draw.
        var otherDomainTags = new ulong[]
        {
            0x484B424F5F434C53UL, // ClashResolver.ClashTag ("HKBO_CLS")
            0x484B424F5F4F504EUL, // ComboResolver.ComboOpenTag ("HKBO_OPN")
            0x484B424F5F434E54UL, // ComboResolver.ComboContinueTag ("HKBO_CNT")
            0x484B424F5F484954UL, // HitLocationResolver.HitLocationTag ("HKBO_HIT")
            0x484B424F5F505249UL, // CollisionPriority.CollisionPriorityTag ("HKBO_PRI")
            0x484B424F5F4C5354UL, // RallyOffset.LastStandTag ("HKBO_LST")
        };
        const ulong ContingentTag = 0x484B424F5F435447UL; // ContingentOffset.ContingentTag ("HKBO_CTG")

        Assert.DoesNotContain(ContingentTag, otherDomainTags);
    }

    [Fact]
    public void OffsetIsIndependentOfHowManyOtherEntitiesWereComputedFirst()
    {
        const ulong Seed = 21;
        const ulong TargetEntityId = 500;
        var jitter = JitterRaw;

        var inIsolation = ContingentOffset.Compute(Seed, TargetEntityId, jitter);

        for (ulong entityId = 1; entityId <= 1_000; entityId++)
        {
            ContingentOffset.Compute(Seed, entityId, jitter);
        }

        var afterAThousandOtherCalls = ContingentOffset.Compute(Seed, TargetEntityId, jitter);

        Assert.Equal(inIsolation, afterAThousandOtherCalls);
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
