using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Diagnostics;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The 16-sector facing model of design section 6: the exact integer vector
/// table, delta resolution with its lower-sector tie, turning with its
/// canonical-clockwise half-turn tie and step cap, circular separation, the
/// direction-band pace caps, and the source-hygiene ban on trigonometry and
/// floating point in <c>FacingRules.cs</c>. The lifted
/// <see cref="FixedPoint.IntegerSquareRoot"/> helper is covered here too,
/// because the route rules that consume these facings are its next caller.
/// </summary>
public sealed class FacingRulesTests
{
    // ----- 6.1 The exact integer vector table -----

    [Theory]
    [InlineData(Facing16.East, 1_024, 0)]
    [InlineData(Facing16.EastSouthEast, 946, 392)]
    [InlineData(Facing16.SouthEast, 724, 724)]
    [InlineData(Facing16.SouthSouthEast, 392, 946)]
    [InlineData(Facing16.South, 0, 1_024)]
    [InlineData(Facing16.SouthSouthWest, -392, 946)]
    [InlineData(Facing16.SouthWest, -724, 724)]
    [InlineData(Facing16.WestSouthWest, -946, 392)]
    [InlineData(Facing16.West, -1_024, 0)]
    [InlineData(Facing16.WestNorthWest, -946, -392)]
    [InlineData(Facing16.NorthWest, -724, -724)]
    [InlineData(Facing16.NorthNorthWest, -392, -946)]
    [InlineData(Facing16.North, 0, -1_024)]
    [InlineData(Facing16.NorthNorthEast, 392, -946)]
    [InlineData(Facing16.NorthEast, 724, -724)]
    [InlineData(Facing16.EastNorthEast, 946, -392)]
    public void TheVectorTableMatchesTheDesignToTheDigit(
        Facing16 facing,
        int expectedX,
        int expectedY)
    {
        var (rawX, rawY) = FacingRules.SectorVector(facing);

        Assert.Equal(expectedX, rawX);
        Assert.Equal(expectedY, rawY);
    }

    [Fact]
    public void TheVectorTableRejectsNone() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.SectorVector(Facing16.None));

    // ----- 6.2 FromDelta -----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AZeroDeltaResolvesToNoneForBothFactions(int factionId) =>
        Assert.Equal(Facing16.None, FacingRules.FromDelta(0, 0, factionId));

    [Theory]
    [InlineData(1_024, 0, Facing16.East)]
    [InlineData(946, 392, Facing16.EastSouthEast)]
    [InlineData(724, 724, Facing16.SouthEast)]
    [InlineData(0, 1_024, Facing16.South)]
    [InlineData(-1_024, 0, Facing16.West)]
    [InlineData(0, -1_024, Facing16.North)]
    [InlineData(946, -392, Facing16.EastNorthEast)]
    [InlineData(7, 0, Facing16.East)]
    [InlineData(0, -3, Facing16.North)]
    public void FactionZeroDeltasResolveToTheirNearestSector(
        long deltaX,
        long deltaY,
        Facing16 expected) =>
        Assert.Equal(expected, FacingRules.FromDelta(deltaX, deltaY, 0));

    /// <summary>
    /// (196, 39) sits exactly on the boundary between sectors 0 and 1: both
    /// dot products are 200,704. The lower numeric sector wins.
    /// </summary>
    [Fact]
    public void AnExactDotProductTieTakesTheLowerSector() =>
        Assert.Equal(Facing16.East, FacingRules.FromDelta(196, 39, 0));

    /// <summary>
    /// (166, 111) ties sectors 1 and 2 at 200,548, away from sector zero, so
    /// the rule is proven to pick the lower of the tied pair rather than
    /// defaulting to zero.
    /// </summary>
    [Fact]
    public void AnExactTieAwayFromSectorZeroStillTakesTheLowerSector() =>
        Assert.Equal(
            Facing16.EastSouthEast, FacingRules.FromDelta(166, 111, 0));

    /// <summary>
    /// Canonicalization makes reflected inputs produce exactly reflected
    /// facings: faction 1 seeing the mirrored delta resolves to the mirrored
    /// sector, for every sector in the table.
    /// </summary>
    [Theory]
    [InlineData(Facing16.East)]
    [InlineData(Facing16.EastSouthEast)]
    [InlineData(Facing16.SouthEast)]
    [InlineData(Facing16.SouthSouthEast)]
    [InlineData(Facing16.South)]
    [InlineData(Facing16.SouthSouthWest)]
    [InlineData(Facing16.SouthWest)]
    [InlineData(Facing16.WestSouthWest)]
    [InlineData(Facing16.West)]
    [InlineData(Facing16.WestNorthWest)]
    [InlineData(Facing16.NorthWest)]
    [InlineData(Facing16.NorthNorthWest)]
    [InlineData(Facing16.North)]
    [InlineData(Facing16.NorthNorthEast)]
    [InlineData(Facing16.NorthEast)]
    [InlineData(Facing16.EastNorthEast)]
    public void ReflectedDeltasProduceReflectedFacings(Facing16 sector)
    {
        var (rawX, rawY) = FacingRules.SectorVector(sector);

        var factionZero = FacingRules.FromDelta(rawX, rawY, 0);
        var factionOne = FacingRules.FromDelta(-rawX, rawY, 1);

        Assert.Equal(sector, factionZero);
        Assert.Equal(
            (Facing16)((8 - (int)factionZero + 16) % 16),
            factionOne);
    }

    /// <summary>
    /// The tie resolves in canonical space for both factions, so even a
    /// sector-boundary delta reflects exactly: faction 0's East tie becomes
    /// faction 1's West tie, never West-northwest.
    /// </summary>
    [Fact]
    public void ASectorBoundaryTieReflectsExactlyBetweenFactions()
    {
        Assert.Equal(Facing16.East, FacingRules.FromDelta(196, 39, 0));
        Assert.Equal(Facing16.West, FacingRules.FromDelta(-196, 39, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void FromDeltaRejectsAnUnknownFaction(int factionId) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.FromDelta(1, 0, factionId));

    // ----- 6.3 Turning -----

    /// <summary>
    /// A turn request exactly at the step cap reaches the desired facing.
    /// </summary>
    [Fact]
    public void ATurnExactlyAtTheStepCapReachesTheDesiredFacing() =>
        Assert.Equal(
            Facing16.SouthSouthEast,
            FacingRules.TurnToward(
                Facing16.East, Facing16.SouthSouthEast, 3, 0));

    /// <summary>
    /// A request one sector beyond the cap advances only by the cap.
    /// </summary>
    [Fact]
    public void ATurnOneSectorBeyondTheStepCapAdvancesOnlyByTheCap() =>
        Assert.Equal(
            Facing16.SouthSouthEast,
            FacingRules.TurnToward(Facing16.East, Facing16.South, 3, 0));

    [Fact]
    public void TheShorterArcWinsWhenItIsCounterClockwise() =>
        Assert.Equal(
            Facing16.NorthEast,
            FacingRules.TurnToward(
                Facing16.EastSouthEast, Facing16.NorthEast, 8, 0));

    [Fact]
    public void ACappedCounterClockwiseTurnAdvancesTowardTheDesiredFacing() =>
        Assert.Equal(
            Facing16.EastNorthEast,
            FacingRules.TurnToward(
                Facing16.EastSouthEast, Facing16.NorthEast, 2, 0));

    [Fact]
    public void TurningCrossesTheSectorWrapInBothDirections()
    {
        Assert.Equal(
            Facing16.EastSouthEast,
            FacingRules.TurnToward(
                Facing16.EastNorthEast, Facing16.EastSouthEast, 8, 0));
        Assert.Equal(
            Facing16.EastNorthEast,
            FacingRules.TurnToward(
                Facing16.EastSouthEast, Facing16.EastNorthEast, 8, 0));
    }

    /// <summary>
    /// Design 6.3: an eight-step tie turns clockwise in canonical space. No
    /// weapon document covers this case, so the shared foundation owns the
    /// test outright.
    /// </summary>
    [Fact]
    public void AnEightStepTieTurnsClockwiseInCanonicalSpace() =>
        Assert.Equal(
            Facing16.SouthSouthEast,
            FacingRules.TurnToward(Facing16.East, Facing16.West, 3, 0));

    /// <summary>
    /// The same tie for faction 1 maps back to a counter-clockwise turn in
    /// world space: West toward East advances to south-southwest (sector 5),
    /// not to north-northwest (sector 11).
    /// </summary>
    [Fact]
    public void TheEightStepTieMapsToCounterClockwiseInWorldSpaceForFactionOne() =>
        Assert.Equal(
            Facing16.SouthSouthWest,
            FacingRules.TurnToward(Facing16.West, Facing16.East, 3, 1));

    [Fact]
    public void AZeroStepCapRetainsTheCurrentFacing() =>
        Assert.Equal(
            Facing16.East,
            FacingRules.TurnToward(Facing16.East, Facing16.West, 0, 0));

    [Fact]
    public void TurningRejectsNoneOnEitherSide()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.TurnToward(Facing16.None, Facing16.East, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.TurnToward(Facing16.East, Facing16.None, 1, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void TurningRejectsAStepCapOutsideTheProfileBound(int steps) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.TurnToward(
                Facing16.East, Facing16.West, steps, 0));

    // ----- 6.4 Direction bands -----

    [Theory]
    [InlineData(Facing16.East, Facing16.East, 0)]
    [InlineData(Facing16.East, Facing16.EastSouthEast, 1)]
    [InlineData(Facing16.East, Facing16.EastNorthEast, 1)]
    [InlineData(Facing16.East, Facing16.West, 8)]
    [InlineData(Facing16.EastNorthEast, Facing16.East, 1)]
    [InlineData(Facing16.NorthEast, Facing16.SouthEast, 4)]
    [InlineData(Facing16.SouthSouthEast, Facing16.NorthNorthWest, 8)]
    public void CircularSeparationIsSymmetricAndAtMostEight(
        Facing16 first,
        Facing16 second,
        int expected)
    {
        Assert.Equal(expected, FacingRules.SectorSeparation(first, second));
        Assert.Equal(expected, FacingRules.SectorSeparation(second, first));
    }

    [Fact]
    public void SectorSeparationRejectsNone() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.SectorSeparation(Facing16.None, Facing16.East));

    [Theory]
    [InlineData(0, 9_800)]
    [InlineData(1, 9_800)]
    [InlineData(2, 8_200)]
    [InlineData(5, 8_200)]
    [InlineData(6, 7_000)]
    [InlineData(8, 7_000)]
    public void TheDirectionBandSelectsTheProfilePaceCap(
        int separationSectors,
        int expectedCapBasisPoints)
    {
        var profile = new LoadoutMovementProfile(
            new CombatLoadout(
                WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            forwardPaceBasisPoints: 9_800,
            lateralPaceBasisPoints: 8_200,
            backwardPaceBasisPoints: 7_000,
            committedPaceBasisPoints: 3_000,
            preferredDistanceBasisPoints: 11_500,
            opponentDistanceOffsetBasisPoints: [0, 0, 0, 0, 0, 0],
            maximumFacingStepsPerTick: 2,
            committedFacingStepsPerTick: 1,
            accelerationBasisPointsPerTick: 5_000,
            decelerationBasisPointsPerTick: 6_000,
            commitmentTicks: 3,
            recoveryTicks: 3,
            allyClearanceBodyDiametersBasisPoints: 15_000,
            disengageEnemyToAllyBasisPoints: 20_000,
            reengageEnemyToAllyBasisPoints: 12_500,
            pursuitSupportBodyDiametersBasisPoints: 12_500);

        Assert.Equal(
            expectedCapBasisPoints,
            FacingRules.DirectionBandPaceCapBasisPoints(
                profile, separationSectors));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void TheDirectionBandRejectsASeparationOutsideTheHalfTurn(
        int separationSectors)
    {
        var profile = new LoadoutMovementProfile(
            new CombatLoadout(
                WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            forwardPaceBasisPoints: 9_800,
            lateralPaceBasisPoints: 8_200,
            backwardPaceBasisPoints: 7_000,
            committedPaceBasisPoints: 3_000,
            preferredDistanceBasisPoints: 11_500,
            opponentDistanceOffsetBasisPoints: [0, 0, 0, 0, 0, 0],
            maximumFacingStepsPerTick: 2,
            committedFacingStepsPerTick: 1,
            accelerationBasisPointsPerTick: 5_000,
            decelerationBasisPointsPerTick: 6_000,
            commitmentTicks: 3,
            recoveryTicks: 3,
            allyClearanceBodyDiametersBasisPoints: 15_000,
            disengageEnemyToAllyBasisPoints: 20_000,
            reengageEnemyToAllyBasisPoints: 12_500,
            pursuitSupportBodyDiametersBasisPoints: 12_500);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FacingRules.DirectionBandPaceCapBasisPoints(
                profile, separationSectors));
    }

    // ----- Source hygiene (design 6.2) -----

    /// <summary>
    /// No trigonometry and no floating point in <c>FacingRules.cs</c>, in the
    /// same spirit as the console-usage scan over <c>src/</c>. The banned
    /// tokens are assembled from fragments so this test file cannot trip a
    /// scan of its own text.
    /// </summary>
    [Fact]
    public void FacingRulesContainsNoTrigonometryAndNoFloatingPoint()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so FacingRules.cs cannot be scanned.");

        var source = File.ReadAllText(Path.Combine(
            root!, "src", "Hukbo.Core", "Movement", "FacingRules.cs"));

        string[] bannedTokens =
        [
            "Math." + "Atan",
            "Math" + "F",
            "dou" + "ble",
            "flo" + "at",
        ];
        foreach (var token in bannedTokens)
        {
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }
    }

    // ----- The lifted IntegerSquareRoot helper (design 10.1) -----

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1L)]
    [InlineData(3L, 1L)]
    [InlineData(4L, 2L)]
    [InlineData(8L, 2L)]
    [InlineData(9L, 3L)]
    [InlineData(1_000_000_000_000_000_000L, 1_000_000_000L)]
    [InlineData(long.MaxValue, 3_037_000_499L)]
    public void TheLiftedIntegerSquareRootTruncatesTowardZero(
        long value,
        long expected) =>
        Assert.Equal(expected, FixedPoint.IntegerSquareRoot(value));

    [Fact]
    public void TheLiftedIntegerSquareRootRejectsANegativeValue() =>
        Assert.Throws<OverflowException>(
            () => FixedPoint.IntegerSquareRoot(-1));
}
