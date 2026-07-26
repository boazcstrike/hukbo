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
    public void MaximumLastStandThresholdMatchesTheSquarePackingBoundOfTheJitterSquare()
    {
        // Bias square side = 2 * jitter = 2 * (RallyJitterRadiusMultiplier * R) = 8R.
        // Body square side = 2R. Bodies per side = 8R / 2R = RallyJitterRadiusMultiplier.
        // Bodies per square = bodies-per-side squared.
        var biasSquareSideInBodyRadii = 2 * FormationRules.RallyJitterRadiusMultiplier;
        var bodySquareSideInBodyRadii = 2;
        var bodiesPerSide = biasSquareSideInBodyRadii / bodySquareSideInBodyRadii;
        var expected = bodiesPerSide * bodiesPerSide;

        Assert.Equal(16, FormationRules.MaximumLastStandThresholdAgents);
        Assert.Equal(FormationRules.MaximumLastStandThresholdAgents, expected);
    }

    [Fact]
    public void RallyJitterRadiusForTheDefaultBodyIsSixteenWorldUnits()
    {
        var jitterRaw = FormationRules.ComputeRallyJitterRaw(CollisionRules.DefaultBodyRadiusRaw);

        Assert.Equal(16 * FixedPoint.Scale, jitterRaw);
    }

    [Fact]
    public void ComputeRallyJitterRawRejectsARadiusWhoseSpanOverflowsAnInt32()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FormationRules.ComputeRallyJitterRaw(268_435_456));
    }
}
