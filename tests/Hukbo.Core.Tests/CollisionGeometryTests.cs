using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class CollisionGeometryTests
{
    private const int BodyRadiusRaw = 4 * FixedPoint.Scale;
    private const int DiameterRaw = 2 * BodyRadiusRaw;
    private const int MaximumCoordinateRaw =
        Scenario.MaximumMapDimension * FixedPoint.Scale;

    [Fact]
    public void SquaredDistance_ReturnsZeroForCoincidentCentres()
    {
        Assert.Equal(0L, CollisionGeometry.SquaredDistance(17, -23, 17, -23));
    }

    [Fact]
    public void SquaredDistance_HandlesNegativeAndPositiveDeltas()
    {
        Assert.Equal(25L, CollisionGeometry.SquaredDistance(0, 0, 3, 4));
        Assert.Equal(25L, CollisionGeometry.SquaredDistance(0, 0, -3, -4));
        Assert.Equal(25L, CollisionGeometry.SquaredDistance(0, 0, -3, 4));
        Assert.Equal(25L, CollisionGeometry.SquaredDistance(5, -7, 2, -3));
    }

    [Fact]
    public void SquaredDistance_IsSymmetricUnderArgumentSwap()
    {
        var forward = CollisionGeometry.SquaredDistance(-1_234, 5_678, 9_012, -3_456);
        var reversed = CollisionGeometry.SquaredDistance(9_012, -3_456, -1_234, 5_678);

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void SquaredDistance_FitsInLongAtMaximumValidatedCoordinates()
    {
        var distance = CollisionGeometry.SquaredDistance(
            0,
            0,
            MaximumCoordinateRaw,
            MaximumCoordinateRaw);

        Assert.Equal(2L * MaximumCoordinateRaw * MaximumCoordinateRaw, distance);
        Assert.Equal(2_097_152_000_000_000_000L, distance);
        Assert.True(distance < long.MaxValue);
    }

    [Fact]
    public void ContactSquaredDistance_IsTheSquaredDiameter()
    {
        Assert.Equal(
            (long)DiameterRaw * DiameterRaw,
            CollisionGeometry.ContactSquaredDistance(BodyRadiusRaw));
        Assert.Equal(67_108_864L, CollisionGeometry.ContactSquaredDistance(BodyRadiusRaw));
        Assert.Equal(100L, CollisionGeometry.ContactSquaredDistance(5));
        Assert.Equal(0L, CollisionGeometry.ContactSquaredDistance(0));
    }

    [Fact]
    public void Overlaps_IsFalseForSeparatedDiscs()
    {
        Assert.False(
            CollisionGeometry.Overlaps(0, 0, DiameterRaw + 1, 0, BodyRadiusRaw));
        Assert.False(
            CollisionGeometry.Overlaps(0, 0, 0, DiameterRaw + 1, BodyRadiusRaw));
        Assert.False(
            CollisionGeometry.Overlaps(0, 0, DiameterRaw * 10, 0, BodyRadiusRaw));
    }

    [Fact]
    public void Overlaps_IsFalseAtExactTangentInBothDirections()
    {
        Assert.False(CollisionGeometry.Overlaps(0, 0, DiameterRaw, 0, BodyRadiusRaw));
        Assert.False(CollisionGeometry.Overlaps(0, 0, -DiameterRaw, 0, BodyRadiusRaw));
        Assert.False(CollisionGeometry.Overlaps(0, 0, 0, DiameterRaw, BodyRadiusRaw));
        Assert.False(CollisionGeometry.Overlaps(0, 0, 0, -DiameterRaw, BodyRadiusRaw));
    }

    [Fact]
    public void Overlaps_IsFalseAtExactDiagonalTangent()
    {
        // A 3-4-5 triple scaled by two: 6^2 + 8^2 == 10^2 == (2 * 5)^2.
        Assert.False(CollisionGeometry.Overlaps(0, 0, 6, 8, 5));
        Assert.False(CollisionGeometry.Overlaps(0, 0, -6, -8, 5));
        Assert.True(CollisionGeometry.IsContact(0, 0, 6, 8, 5));
    }

    [Fact]
    public void Overlaps_IsTrueAtOneRawUnitOfPenetration()
    {
        Assert.True(
            CollisionGeometry.Overlaps(0, 0, DiameterRaw - 1, 0, BodyRadiusRaw));
        Assert.True(
            CollisionGeometry.Overlaps(0, 0, -(DiameterRaw - 1), 0, BodyRadiusRaw));
        Assert.True(
            CollisionGeometry.Overlaps(0, 0, 0, DiameterRaw - 1, BodyRadiusRaw));
        Assert.True(
            CollisionGeometry.Overlaps(0, 0, 0, -(DiameterRaw - 1), BodyRadiusRaw));
    }

    [Fact]
    public void Overlaps_IsTrueForDeepPenetrationAndCoincidentCentres()
    {
        Assert.True(CollisionGeometry.Overlaps(0, 0, 1, 1, BodyRadiusRaw));
        Assert.True(
            CollisionGeometry.Overlaps(1_000, 2_000, 1_000, 2_000, BodyRadiusRaw));
    }

    [Fact]
    public void Overlaps_IsSymmetricUnderArgumentSwap()
    {
        Assert.Equal(
            CollisionGeometry.Overlaps(7, -11, 7 + DiameterRaw - 1, -11, BodyRadiusRaw),
            CollisionGeometry.Overlaps(7 + DiameterRaw - 1, -11, 7, -11, BodyRadiusRaw));
        Assert.Equal(
            CollisionGeometry.Overlaps(7, -11, 7 + DiameterRaw, -11, BodyRadiusRaw),
            CollisionGeometry.Overlaps(7 + DiameterRaw, -11, 7, -11, BodyRadiusRaw));
    }

    [Fact]
    public void IsContact_IsTrueAtExactTangentInBothDirections()
    {
        Assert.True(CollisionGeometry.IsContact(0, 0, DiameterRaw, 0, BodyRadiusRaw));
        Assert.True(CollisionGeometry.IsContact(0, 0, -DiameterRaw, 0, BodyRadiusRaw));
        Assert.True(CollisionGeometry.IsContact(0, 0, 0, DiameterRaw, BodyRadiusRaw));
        Assert.True(CollisionGeometry.IsContact(0, 0, 0, -DiameterRaw, BodyRadiusRaw));
    }

    [Fact]
    public void IsContact_IsFalseOneRawUnitBeyondTangent()
    {
        Assert.False(
            CollisionGeometry.IsContact(0, 0, DiameterRaw + 1, 0, BodyRadiusRaw));
        Assert.False(
            CollisionGeometry.IsContact(0, 0, 0, -(DiameterRaw + 1), BodyRadiusRaw));
    }

    [Fact]
    public void IsContact_IsTrueForPenetrationAndCoincidentCentres()
    {
        Assert.True(
            CollisionGeometry.IsContact(0, 0, DiameterRaw - 1, 0, BodyRadiusRaw));
        Assert.True(CollisionGeometry.IsContact(0, 0, 0, 0, BodyRadiusRaw));
    }

    [Fact]
    public void IsContact_IsSymmetricUnderArgumentSwap()
    {
        Assert.Equal(
            CollisionGeometry.IsContact(-5, 9, -5 + DiameterRaw, 9, BodyRadiusRaw),
            CollisionGeometry.IsContact(-5 + DiameterRaw, 9, -5, 9, BodyRadiusRaw));
        Assert.Equal(
            CollisionGeometry.IsContact(-5, 9, -5 + DiameterRaw + 1, 9, BodyRadiusRaw),
            CollisionGeometry.IsContact(-5 + DiameterRaw + 1, 9, -5, 9, BodyRadiusRaw));
    }

    [Fact]
    public void IsCoincident_IsTrueOnlyForExactlyEqualCentres()
    {
        Assert.True(CollisionGeometry.IsCoincident(0, 0, 0, 0));
        Assert.True(CollisionGeometry.IsCoincident(-42, 77, -42, 77));
        Assert.True(
            CollisionGeometry.IsCoincident(
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw));
        Assert.False(CollisionGeometry.IsCoincident(-42, 77, -41, 77));
        Assert.False(CollisionGeometry.IsCoincident(-42, 77, -42, 78));
    }

    [Fact]
    public void Overlaps_ClassifiesCorrectlyAtMaximumValidatedCoordinates()
    {
        Assert.False(
            CollisionGeometry.Overlaps(
                0,
                0,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                BodyRadiusRaw));
        Assert.False(
            CollisionGeometry.IsContact(
                0,
                0,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                BodyRadiusRaw));

        Assert.False(
            CollisionGeometry.Overlaps(
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw - DiameterRaw,
                MaximumCoordinateRaw,
                BodyRadiusRaw));
        Assert.True(
            CollisionGeometry.IsContact(
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw - DiameterRaw,
                MaximumCoordinateRaw,
                BodyRadiusRaw));

        Assert.True(
            CollisionGeometry.Overlaps(
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw - DiameterRaw + 1,
                BodyRadiusRaw));
        Assert.True(
            CollisionGeometry.Overlaps(
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                BodyRadiusRaw));
    }

    [Fact]
    public void ClampCenterToBounds_LeavesInteriorValuesUnchanged()
    {
        Assert.Equal(
            500 * FixedPoint.Scale,
            CollisionGeometry.ClampCenterToBounds(
                500 * FixedPoint.Scale,
                1_280 * FixedPoint.Scale,
                BodyRadiusRaw));
    }

    [Fact]
    public void ClampCenterToBounds_ClampsToTheLowBound()
    {
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                0,
                1_280 * FixedPoint.Scale,
                BodyRadiusRaw));
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                -1_000_000,
                1_280 * FixedPoint.Scale,
                BodyRadiusRaw));
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                BodyRadiusRaw,
                1_280 * FixedPoint.Scale,
                BodyRadiusRaw));
    }

    [Fact]
    public void ClampCenterToBounds_ClampsToTheHighBound()
    {
        const int DimensionRaw = 1_280 * FixedPoint.Scale;
        const int HighBoundRaw = DimensionRaw - BodyRadiusRaw;

        Assert.Equal(
            HighBoundRaw,
            CollisionGeometry.ClampCenterToBounds(
                DimensionRaw,
                DimensionRaw,
                BodyRadiusRaw));
        Assert.Equal(
            HighBoundRaw,
            CollisionGeometry.ClampCenterToBounds(
                int.MaxValue,
                DimensionRaw,
                BodyRadiusRaw));
        Assert.Equal(
            HighBoundRaw,
            CollisionGeometry.ClampCenterToBounds(
                HighBoundRaw,
                DimensionRaw,
                BodyRadiusRaw));
    }

    [Fact]
    public void ClampCenterToBounds_DegradesToTheLowBoundWhenTheMapIsNarrowerThanOneBody()
    {
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(0, DiameterRaw - 1, BodyRadiusRaw));
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                int.MaxValue,
                DiameterRaw - 1,
                BodyRadiusRaw));
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(0, 0, BodyRadiusRaw));
    }

    [Fact]
    public void ClampCenterToBounds_CollapsesToTheSinglePointWhenTheMapIsExactlyOneBody()
    {
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(0, DiameterRaw, BodyRadiusRaw));
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                int.MaxValue,
                DiameterRaw,
                BodyRadiusRaw));
    }

    [Fact]
    public void ClampCenterToBounds_ClampsBothAxesIndependentlyAtMaximumMapSize()
    {
        Assert.Equal(
            MaximumCoordinateRaw - BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                MaximumCoordinateRaw,
                MaximumCoordinateRaw,
                BodyRadiusRaw));
        Assert.Equal(
            BodyRadiusRaw,
            CollisionGeometry.ClampCenterToBounds(
                -1,
                MaximumCoordinateRaw,
                BodyRadiusRaw));
    }
}
