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
    public void RallyJitterRadiusForTheDefaultBodyIsTwentyFourWorldUnits()
    {
        var jitterRaw = FormationRules.ComputeRallyJitterRaw(
            CollisionRules.DefaultBodyRadiusRaw);

        Assert.Equal(24 * FixedPoint.Scale, jitterRaw);
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

    [Fact]
    public void ContingentJitterMultiplierSquaredStrictlyExceedsFourTimesLivingCount()
    {
        // ComputeContingentJitterRaw(1, livingCount) is exactly the
        // multiplier (IntegerSquareRoot(4 * livingCount) + 1), since
        // multiplying by a body radius of 1 leaves it unscaled. The packing
        // proof (FormationRules type-level remarks, "The personal offset" in
        // the design) depends on capacity == multiplier^2 strictly exceeding
        // 4 * livingCount for every living count the game can produce, so
        // this sweeps the full stated range rather than sampling a few
        // points.
        for (var livingCount = 1; livingCount <= 2000; livingCount++)
        {
            var multiplier = FormationRules.ComputeContingentJitterRaw(
                bodyRadiusRaw: 1,
                livingCount);

            var multiplierSquared = checked((long)multiplier * multiplier);
            var fourTimesLivingCount = checked(4L * livingCount);

            Assert.True(
                multiplierSquared > fourTimesLivingCount,
                $"livingCount={livingCount}: multiplier {multiplier} squared " +
                $"({multiplierSquared}) must strictly exceed 4 * livingCount " +
                $"({fourTimesLivingCount}).");
        }
    }

    [Fact]
    public void ContingentTrailRawStrictlyExceedsTheJitterDiagonalPlusTwoBodyRadii()
    {
        // The trail must clear the worst-case forward encroachment a jitter
        // offset alone could produce: jitterRaw * sqrt(2), the Chebyshev
        // bound for an offset drawn independently per axis from
        // [-jitterRaw, +jitterRaw]. sqrt(2) is irrational, so the comparison
        // is squared into exact long arithmetic instead of ever calling
        // Math.Sqrt — squaring cannot round in the design's favour the way a
        // floating-point square root could. Both sides of
        // "clearanceRaw > jitterRaw * sqrt(2)" are non-negative (clearanceRaw
        // is always positive by construction — see ComputeContingentTrailRaw
        // — and a square root is never negative), so squaring preserves the
        // inequality's direction exactly.
        for (var bodyRadiusRaw = 1; bodyRadiusRaw <= 200; bodyRadiusRaw++)
        {
            for (var livingCount = 1; livingCount <= 2000; livingCount++)
            {
                var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                    bodyRadiusRaw,
                    livingCount);
                var trailRaw = FormationRules.ComputeContingentTrailRaw(
                    bodyRadiusRaw,
                    jitterRaw);

                var clearanceRaw = checked((long)trailRaw - (2L * bodyRadiusRaw));
                var clearanceSquared = checked(clearanceRaw * clearanceRaw);
                var jitterDiagonalSquaredTimesTwo =
                    checked(2L * jitterRaw * jitterRaw);

                Assert.True(
                    clearanceRaw > 0,
                    $"bodyRadiusRaw={bodyRadiusRaw}, livingCount={livingCount}: " +
                    "trail clearance must be positive.");
                Assert.True(
                    clearanceSquared > jitterDiagonalSquaredTimesTwo,
                    $"bodyRadiusRaw={bodyRadiusRaw}, livingCount={livingCount}: " +
                    $"trail clearance squared ({clearanceSquared}) must " +
                    "strictly exceed 2 * jitterRaw^2 " +
                    $"({jitterDiagonalSquaredTimesTwo}).");
            }
        }
    }

    [Fact]
    public void IsCohesionSquareWithinBoundsIsTrueAtExactBoundaryFalseOneRawUnitBeyond()
    {
        const int bodyRadiusRaw = 10;
        const int jitterRaw = 20;
        const int marginRaw = jitterRaw + bodyRadiusRaw; // 30
        const int mapWidthRaw = 1000;
        const int mapHeightRaw = 800;
        const int centralXRaw = 500;
        const int centralYRaw = 400;

        // Comparison 1: trailBaseX - marginRaw >= bodyRadiusRaw.
        var xLowBoundaryRaw = bodyRadiusRaw + marginRaw; // 40
        Assert.True(FormationRules.IsCohesionSquareWithinBounds(
            xLowBoundaryRaw, centralYRaw, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));
        Assert.False(FormationRules.IsCohesionSquareWithinBounds(
            xLowBoundaryRaw - 1, centralYRaw, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));

        // Comparison 2: trailBaseX + marginRaw <= mapWidthRaw - bodyRadiusRaw.
        var xHighBoundaryRaw = mapWidthRaw - bodyRadiusRaw - marginRaw; // 960
        Assert.True(FormationRules.IsCohesionSquareWithinBounds(
            xHighBoundaryRaw, centralYRaw, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));
        Assert.False(FormationRules.IsCohesionSquareWithinBounds(
            xHighBoundaryRaw + 1, centralYRaw, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));

        // Comparison 3: trailBaseY - marginRaw >= bodyRadiusRaw.
        var yLowBoundaryRaw = bodyRadiusRaw + marginRaw; // 40
        Assert.True(FormationRules.IsCohesionSquareWithinBounds(
            centralXRaw, yLowBoundaryRaw, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));
        Assert.False(FormationRules.IsCohesionSquareWithinBounds(
            centralXRaw, yLowBoundaryRaw - 1, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));

        // Comparison 4: trailBaseY + marginRaw <= mapHeightRaw - bodyRadiusRaw.
        var yHighBoundaryRaw = mapHeightRaw - bodyRadiusRaw - marginRaw; // 760
        Assert.True(FormationRules.IsCohesionSquareWithinBounds(
            centralXRaw, yHighBoundaryRaw, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));
        Assert.False(FormationRules.IsCohesionSquareWithinBounds(
            centralXRaw, yHighBoundaryRaw + 1, jitterRaw, bodyRadiusRaw,
            mapWidthRaw, mapHeightRaw));
    }

    [Fact]
    public void IsCohesionSquareWithinBoundsIsFalseForEveryTrailBaseOnAMapSmallerThanTwiceTheMargin()
    {
        const int bodyRadiusRaw = 10;
        const int jitterRaw = 20;
        const int marginRaw = jitterRaw + bodyRadiusRaw; // 30
        const int undersizedDimensionRaw = (2 * marginRaw) - 1; // 59
        const int amplyLargeDimensionRaw = 10_000;

        // No trail base on the X axis, however placed, can satisfy both the
        // low and high comparisons when mapWidthRaw is smaller than
        // 2 * marginRaw: the two comparisons combined require
        // mapWidthRaw >= 2 * bodyRadiusRaw + 2 * marginRaw, a strictly
        // larger map than 2 * marginRaw alone, so this is a genuine (if not
        // the tightest possible) sufficient condition for "always false".
        for (var trailBaseXRaw = -100;
            trailBaseXRaw <= undersizedDimensionRaw + 100;
            trailBaseXRaw += 7)
        {
            Assert.False(FormationRules.IsCohesionSquareWithinBounds(
                trailBaseXRaw,
                trailBaseYRaw: amplyLargeDimensionRaw / 2,
                jitterRaw,
                bodyRadiusRaw,
                undersizedDimensionRaw,
                amplyLargeDimensionRaw));
        }

        for (var trailBaseYRaw = -100;
            trailBaseYRaw <= undersizedDimensionRaw + 100;
            trailBaseYRaw += 7)
        {
            Assert.False(FormationRules.IsCohesionSquareWithinBounds(
                trailBaseXRaw: amplyLargeDimensionRaw / 2,
                trailBaseYRaw,
                jitterRaw,
                bodyRadiusRaw,
                amplyLargeDimensionRaw,
                undersizedDimensionRaw));
        }
    }

    [Fact]
    public void DoCohesionSquaresOverlapIsTrueAtExactEdgeContactFalseOneRawUnitFartherApart()
    {
        const int aTrailBaseXRaw = 0;
        const int aTrailBaseYRaw = 0;
        const int aMarginRaw = 40;
        const int bMarginRaw = 25;
        const int marginSumRaw = aMarginRaw + bMarginRaw; // 65

        // X axis, positive direction.
        Assert.True(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            marginSumRaw, 0, bMarginRaw));
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            marginSumRaw + 1, 0, bMarginRaw));

        // X axis, negative direction — Math.Abs must catch this side too.
        Assert.True(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            -marginSumRaw, 0, bMarginRaw));
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            -(marginSumRaw + 1), 0, bMarginRaw));

        // Y axis, positive direction.
        Assert.True(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            0, marginSumRaw, bMarginRaw));
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            0, marginSumRaw + 1, bMarginRaw));

        // Y axis, negative direction.
        Assert.True(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            0, -marginSumRaw, bMarginRaw));
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            0, -(marginSumRaw + 1), bMarginRaw));
    }

    [Fact]
    public void DoCohesionSquaresOverlapRequiresClosenessOnBothAxes()
    {
        const int aTrailBaseXRaw = 0;
        const int aTrailBaseYRaw = 0;
        const int aMarginRaw = 40;
        const int bMarginRaw = 25;
        const int marginSumRaw = aMarginRaw + bMarginRaw; // 65
        const int farRaw = marginSumRaw * 10;

        // Sanity check: close on both axes overlaps, so the far cases below
        // are actually exercising the "requires both" property and not a
        // vacuously false predicate.
        Assert.True(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            1, 1, bMarginRaw));

        // Far on X alone.
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            farRaw, 0, bMarginRaw));

        // Far on Y alone.
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            0, farRaw, bMarginRaw));

        // Far on both.
        Assert.False(FormationRules.DoCohesionSquaresOverlap(
            aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw,
            farRaw, farRaw, bMarginRaw));
    }

    [Fact]
    public void DoCohesionSquaresOverlapReturnsTheIdenticalAnswerWithBothContingentsExchanged()
    {
        // The design's "both contingents yield" property (section 3.5)
        // depends on this predicate being symmetric in its two arguments.
        // Deliberately unequal margins rule out any accidental symmetry that
        // would only hold when the two contingents happened to match.
        var margins = new[] { 1, 5, 17, 40, 100 };

        foreach (var aMarginRaw in margins)
        {
            foreach (var bMarginRaw in margins)
            {
                for (var dx = -250; dx <= 250; dx += 13)
                {
                    for (var dy = -250; dy <= 250; dy += 17)
                    {
                        var forward = FormationRules.DoCohesionSquaresOverlap(
                            0, 0, aMarginRaw,
                            dx, dy, bMarginRaw);
                        var reversed = FormationRules.DoCohesionSquaresOverlap(
                            dx, dy, bMarginRaw,
                            0, 0, aMarginRaw);

                        Assert.True(
                            forward == reversed,
                            $"aMarginRaw={aMarginRaw}, bMarginRaw={bMarginRaw}, " +
                            $"dx={dx}, dy={dy}: exchanging the two " +
                            $"contingents' arguments changed the answer " +
                            $"from {forward} to {reversed}.");
                    }
                }
            }
        }
    }
}
