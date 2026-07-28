using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class FormationRulesTests
{
    [Fact]
    public void DefaultLastStandThresholdIsSix()
    {
        Assert.Equal(6, FormationRules.DefaultLastStandThresholdAgents);
    }

    [Fact]
    public void MaximumLastStandThresholdLeavesAFourfoldAreaMarginUnderTheJitterSquaresCapacity()
    {
        // Bias square side = 2 * jitter = 2 * (RallyJitterRadiusMultiplier * R).
        // Body square side = 2R. Bodies per side is therefore the multiplier,
        // and capacity is that squared. Capacity is NOT a safe headcount:
        // filling the square demands perfect packing from randomly drawn
        // offsets, which gridlocks the cluster and boxes in even the exempt
        // rally agent. The permitted headcount keeps a fourfold area margin.
        var bodiesPerSide =
            (2 * FormationRules.RallyJitterRadiusMultiplier) / 2;
        var capacity = bodiesPerSide * bodiesPerSide;
        var expected = capacity / FormationRules.RallyPackingMargin;

        Assert.Equal(9, FormationRules.MaximumLastStandThresholdAgents);
        Assert.Equal(9, expected);
    }

    [Fact]
    public void TheMaximumThresholdStillAdmitsTheDefaultThreshold()
    {
        Assert.True(
            FormationRules.DefaultLastStandThresholdAgents <=
                FormationRules.MaximumLastStandThresholdAgents,
            "The default last-stand threshold must be configurable, so it " +
            "cannot exceed the maximum the packing margin permits.");
    }

    [Fact]
    public void RallyJitterRadiusForTheDefaultBodyIsTwentyFiveAndAHalfWorldUnits()
    {
        var jitterRaw = FormationRules.ComputeRallyJitterRaw(
            CollisionRules.DefaultBodyRadiusRaw);

        Assert.Equal((51 * FixedPoint.Scale) / 2, jitterRaw);
    }

    [Fact]
    public void ComputeRallyJitterRawRejectsARadiusWhoseSpanOverflowsAnInt32()
    {
        // The span passed to SplitMix64.NextInt is 2 * jitter + 1, which is
        // 2 * multiplier * radius + 1. The largest radius that still fits an
        // Int32 span is (int.MaxValue - 1) / (2 * multiplier).
        var largestSafeRadius =
            (int.MaxValue - 1) / (2 * FormationRules.RallyJitterRadiusMultiplier);

        Assert.True(
            FormationRules.IsBodyRadiusWithinJitterSpanRange(largestSafeRadius));
        Assert.False(
            FormationRules.IsBodyRadiusWithinJitterSpanRange(
                largestSafeRadius + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FormationRules.ComputeRallyJitterRaw(largestSafeRadius + 1));
    }
}
