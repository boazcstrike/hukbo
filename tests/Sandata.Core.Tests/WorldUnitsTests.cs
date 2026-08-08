using Hukbo.Core.Mathematics;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 64 of docs/plans/2026-08-07-sandata-scaffold.md: pins
/// <see cref="WorldUnits.FromFixedPoint"/>'s rounding rule — floor toward
/// negative infinity, agreeing with <c>NavGrid.WorldToCellCoordinate</c>'s
/// arithmetic right shift — at zero, at an exact world unit, at a fractional
/// value, and on both sides of zero.
/// </summary>
public sealed class WorldUnitsTests
{
    [Fact]
    public void FromFixedPoint_Zero_IsZero()
    {
        Assert.Equal(0L, WorldUnits.FromFixedPoint(FixedPoint.Zero));
    }

    [Fact]
    public void FromFixedPoint_ExactWorldUnit_RoundTripsExactly()
    {
        var value = FixedPoint.FromWhole(5);

        Assert.Equal(5L, WorldUnits.FromFixedPoint(value));
    }

    [Fact]
    public void FromFixedPoint_ExactNegativeWorldUnit_RoundTripsExactly()
    {
        var value = FixedPoint.FromWhole(-5);

        Assert.Equal(-5L, WorldUnits.FromFixedPoint(value));
    }

    [Fact]
    public void FromFixedPoint_PositiveFraction_FloorsTowardZero()
    {
        // 1535 raw / 1024 = 1.4990234375 -- floors to 1, same as truncation
        // would give for a positive value, so this alone cannot distinguish
        // the two rules. FromFixedPoint_NegativeFraction_FloorsTowardNegativeInfinity
        // below is the case that actually pins flooring over truncation.
        var value = FixedPoint.FromRaw(1_535);

        Assert.Equal(1L, WorldUnits.FromFixedPoint(value));
    }

    [Fact]
    public void FromFixedPoint_NegativeFraction_FloorsTowardNegativeInfinity()
    {
        // -1 raw / 1024 truncates toward zero to 0, but floors to -1. This is
        // the case that would fail if FromFixedPoint used C#'s ordinary '/'
        // instead of the pinned arithmetic-shift floor rule.
        var value = FixedPoint.FromRaw(-1);

        Assert.Equal(-1L, WorldUnits.FromFixedPoint(value));
    }

    [Fact]
    public void FromFixedPoint_NegativeFractionPastOneWholeUnit_FloorsTowardNegativeInfinity()
    {
        // -1025 raw / 1024 = -1.0009765625 -- truncation would give -1, the
        // pinned floor rule gives -2.
        var value = FixedPoint.FromRaw(-1_025);

        Assert.Equal(-2L, WorldUnits.FromFixedPoint(value));
    }

    /// <summary>
    /// Cross-checks <see cref="WorldUnits.FromFixedPoint"/> against
    /// <see cref="IntegerMath.FloorDiv"/> — the project's already-pinned,
    /// general-purpose floor-division reference — over a spread of raw
    /// values on both sides of zero. Agreement here is exactly what "agrees
    /// with <c>NavGrid.WorldToCellCoordinate</c>'s existing shift" means:
    /// both <see cref="WorldUnits"/> and <c>NavGrid</c> implement floor
    /// division for a power-of-two divisor by shifting rather than dividing,
    /// and <see cref="IntegerMath.FloorDiv"/> is the same floor contract
    /// implemented the general way, so the two must never disagree.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(1_024)]
    [InlineData(-1_024)]
    [InlineData(1_535)]
    [InlineData(-1_535)]
    [InlineData(2_048)]
    [InlineData(-2_048)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void FromFixedPoint_AgreesWithIntegerMathFloorDiv(int rawValue)
    {
        var expected = IntegerMath.FloorDiv(rawValue, FixedPoint.Scale);

        var actual = WorldUnits.FromFixedPoint(FixedPoint.FromRaw(rawValue));

        Assert.Equal(expected, actual);
    }
}
