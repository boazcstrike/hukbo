using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Unit-level facts about <see cref="ApproachSidestep"/>, the pursuit-path stall
/// escape. The behaviour these pin is the escape's contract rather than its
/// tuning: that it is inert until a warrior has proved its line of approach
/// unwalkable, that it is a pure function of its key, and that what it produces
/// is genuinely lateral and genuinely bounded.
/// </summary>
public sealed class ApproachSidestepTests
{
    private const int BodyRadiusRaw = 4352;

    /// <summary>
    /// The single most load-bearing fact in this file. Every recorded state
    /// hash, event hash and frozen trajectory in the repository belongs to a
    /// battle in which no warrior accumulates a full stall streak, so every one
    /// of them computes this function at generation 0. If generation 0 were to
    /// return anything but exactly zero, every one of those recordings would
    /// move, and the feature would have changed battles it was designed never
    /// to touch.
    /// </summary>
    [Theory]
    [InlineData(1UL, 1UL, 1000L, 0L)]
    [InlineData(1UL, 2UL, 0L, 1000L)]
    [InlineData(7UL, 99UL, -3000L, 4000L)]
    [InlineData(12345UL, 4UL, 500L, -500L)]
    public void GenerationZeroDisplacesNothing(
        ulong seed,
        ulong entityId,
        long deltaXRaw,
        long deltaYRaw)
    {
        var distanceRaw = IntegerSquareRoot((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw));

        var offset = ApproachSidestep.Compute(
            seed, entityId, BodyRadiusRaw, 0, deltaXRaw, deltaYRaw, distanceRaw);

        Assert.Equal((0, 0), offset);
    }

    [Fact]
    public void TheSameKeyAlwaysResolvesToTheSameOffset()
    {
        var first = ApproachSidestep.Compute(
            9, 31, BodyRadiusRaw, 1, 6000, -2000, 6324);
        var second = ApproachSidestep.Compute(
            9, 31, BodyRadiusRaw, 1, 6000, -2000, 6324);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// A warrior that has been blocked for a second full streak has proved the
    /// aim point its first generation drew is unwalkable too, so the second
    /// generation has to draw a different one. An escape that redrew the same
    /// point would leave the warrior exactly as stuck.
    /// </summary>
    [Fact]
    public void ASecondGenerationDrawsADifferentOffset()
    {
        var first = ApproachSidestep.Compute(
            9, 31, BodyRadiusRaw, 1, 6000, -2000, 6324);
        var second = ApproachSidestep.Compute(
            9, 31, BodyRadiusRaw, 2, 6000, -2000, 6324);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// The offset must be perpendicular to the approach, not merely somewhere
    /// else: a displacement with a component along the line of approach moves
    /// the warrior toward or away from its enemy rather than around the body
    /// refusing it.
    /// </summary>
    /// <remarks>
    /// Exact perpendicularity is not available in integer arithmetic. The
    /// perpendicular is computed as <c>(-dy, dx) * magnitude / distance</c>, and
    /// each axis truncates by less than one raw unit, so the dot product with
    /// the approach vector is bounded by the length of the approach vector
    /// itself rather than being zero. That bound is asserted rather than a
    /// hand-picked tolerance.
    /// </remarks>
    [Theory]
    [InlineData(6000L, -2000L)]
    [InlineData(1L, 1L)]
    [InlineData(-40000L, 90000L)]
    [InlineData(0L, 7000L)]
    public void TheOffsetIsPerpendicularToTheApproachWithinTruncation(
        long deltaXRaw,
        long deltaYRaw)
    {
        var distanceRaw = IntegerSquareRoot((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw));

        var (offsetXRaw, offsetYRaw) = ApproachSidestep.Compute(
            17, 5, BodyRadiusRaw, 1, deltaXRaw, deltaYRaw, distanceRaw);

        var dot = (deltaXRaw * offsetXRaw) + (deltaYRaw * offsetYRaw);
        Assert.True(
            Math.Abs(dot) <= Math.Abs(deltaXRaw) + Math.Abs(deltaYRaw),
            $"Offset ({offsetXRaw}, {offsetYRaw}) is not perpendicular to " +
            $"approach ({deltaXRaw}, {deltaYRaw}): dot product {dot}.");
    }

    /// <summary>
    /// Below two body radii the warrior's own body still cannot pass the body
    /// refusing it, so the generation would be spent without changing anything.
    /// Above four it stops reading as stepping around an obstacle.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    public void TheOffsetLengthStaysInsideTheProvisionalSpan(int generation)
    {
        const long DeltaXRaw = 30000;
        const long DeltaYRaw = 40000;
        const long DistanceRaw = 50000;

        var (offsetXRaw, offsetYRaw) = ApproachSidestep.Compute(
            3, 88, BodyRadiusRaw, generation, DeltaXRaw, DeltaYRaw, DistanceRaw);

        var lengthRaw = IntegerSquareRoot(
            ((long)offsetXRaw * offsetXRaw) + ((long)offsetYRaw * offsetYRaw));

        var minimumRaw =
            FormationRules.ApproachSidestepMinimumMultiplier * BodyRadiusRaw;
        var maximumRaw =
            FormationRules.ApproachSidestepMaximumMultiplier * BodyRadiusRaw;

        // One raw unit of slack at each end for the integer truncation the
        // perpendicular projection introduces.
        Assert.InRange(lengthRaw, minimumRaw - 1, maximumRaw + 1);
    }

    /// <summary>
    /// A pursuer standing exactly on its target has no approach vector, so
    /// there is no perpendicular to displace it along.
    /// </summary>
    [Fact]
    public void AZeroLengthApproachDisplacesNothing()
    {
        Assert.Equal((0, 0), ApproachSidestep.Compute(1, 1, BodyRadiusRaw, 1, 0, 0, 0));
    }

    [Fact]
    public void ANegativeGenerationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ApproachSidestep.Compute(1, 1, BodyRadiusRaw, -1, 100, 100, 141));
    }

    /// <summary>
    /// Both perpendiculars must be reachable. If every warrior stepped the same
    /// way, two warriors blocked against each other would step in parallel and
    /// stay blocked, which is the failure mode design section 7 names.
    /// </summary>
    [Fact]
    public void BothSidesOfTheApproachAreDrawnAcrossEntities()
    {
        const long DeltaXRaw = 0;
        const long DeltaYRaw = 10000;
        const long DistanceRaw = 10000;

        var sawPositive = false;
        var sawNegative = false;

        for (ulong entityId = 1; entityId <= 64; entityId++)
        {
            var (offsetXRaw, _) = ApproachSidestep.Compute(
                1, entityId, BodyRadiusRaw, 1, DeltaXRaw, DeltaYRaw, DistanceRaw);

            sawPositive |= offsetXRaw > 0;
            sawNegative |= offsetXRaw < 0;
        }

        Assert.True(sawPositive && sawNegative, "Only one perpendicular is ever drawn.");
    }

    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var root = (long)Math.Sqrt(value);
        while (root > 0 && root * root > value)
        {
            root--;
        }

        while ((root + 1) * (root + 1) <= value)
        {
            root++;
        }

        return root;
    }
}
