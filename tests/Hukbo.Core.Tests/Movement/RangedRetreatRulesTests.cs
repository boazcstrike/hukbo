using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The pure threat-radius arithmetic of design section 5.3: deriving the
/// melee-threat radius from a shooter's own standoff distance and deciding
/// whether an observed nearest melee enemy sits inside it. Every value
/// asserted here was computed by hand from the formulas in
/// <see cref="RangedRetreatRules"/>.
/// </summary>
public sealed class RangedRetreatRulesTests
{
    // ----- ThreatRadiusRaw -----

    [Fact]
    public void ThreatRadiusRawIsZeroForAZeroStandoffDistance()
    {
        Assert.Equal(0, RangedRetreatRules.ThreatRadiusRaw(0));
    }

    [Fact]
    public void ThreatRadiusRawIsHalfTheStandoffDistanceOnAnExactCase()
    {
        // 10_000 * 5_000 / 10_000 = 5_000 exactly.
        Assert.Equal(5_000, RangedRetreatRules.ThreatRadiusRaw(10_000));
    }

    [Fact]
    public void ThreatRadiusRawTruncatesTowardZeroOnANonDivisibleCase()
    {
        // 9 * 5_000 / 10_000 = 4.5, truncates to 4.
        Assert.Equal(4, RangedRetreatRules.ThreatRadiusRaw(9));
    }

    [Fact]
    public void ThreatRadiusRawIsBoundedToTheStandoffDistance()
    {
        var standoffRaw = 10_000;
        var radiusRaw = RangedRetreatRules.ThreatRadiusRaw(standoffRaw);

        Assert.InRange(radiusRaw, 0, standoffRaw);
    }

    [Fact]
    public void ThreatRadiusRawRejectsANegativeStandoffDistance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangedRetreatRules.ThreatRadiusRaw(-1));
    }

    [Fact]
    public void ThreatRadiusRawDoesNotOverflowAtTheLargestStandoffDistance()
    {
        // int.MaxValue * 5_000 overflows a 32-bit multiply but not the
        // widened long the implementation uses; the result stays a valid,
        // bounded int.
        var radiusRaw = RangedRetreatRules.ThreatRadiusRaw(int.MaxValue);

        Assert.InRange(radiusRaw, 0, int.MaxValue);
        Assert.Equal(1_073_741_823, radiusRaw);
    }

    // ----- IsThreatened -----

    [Fact]
    public void IsThreatenedIsTrueStrictlyInsideTheRadius()
    {
        Assert.True(RangedRetreatRules.IsThreatened(nearestMeleeSquared: 99, threatRadiusRaw: 10));
    }

    [Fact]
    public void IsThreatenedIsTrueExactlyAtTheRadiusInclusiveBoundary()
    {
        // The boundary is inclusive, matching RangedStandoffV8's own
        // at-or-inside hold comparison (BattleSimulation.cs line 1731): an
        // enemy exactly at the threat radius counts as threatened.
        Assert.True(RangedRetreatRules.IsThreatened(nearestMeleeSquared: 100, threatRadiusRaw: 10));
    }

    [Fact]
    public void IsThreatenedIsFalseJustOutsideTheRadius()
    {
        Assert.False(RangedRetreatRules.IsThreatened(nearestMeleeSquared: 101, threatRadiusRaw: 10));
    }

    [Fact]
    public void IsThreatenedIsFalseForNoObservedMeleeEnemy()
    {
        Assert.False(
            RangedRetreatRules.IsThreatened(
                nearestMeleeSquared: long.MaxValue, threatRadiusRaw: 10));
    }

    [Fact]
    public void IsThreatenedIsFalseForAnyPositiveDistanceAtAZeroRadius()
    {
        Assert.False(RangedRetreatRules.IsThreatened(nearestMeleeSquared: 1, threatRadiusRaw: 0));
    }

    [Fact]
    public void IsThreatenedIsTrueAtZeroDistanceAndZeroRadiusInclusiveBoundary()
    {
        // Zero standoff distance means ThreatRadiusRaw is zero too (design
        // 5.3: a melee weapon never carries a nonzero standoff), so this is
        // the degenerate case of the same inclusive boundary rather than a
        // separate rule.
        Assert.True(RangedRetreatRules.IsThreatened(nearestMeleeSquared: 0, threatRadiusRaw: 0));
    }

    [Fact]
    public void IsThreatenedRejectsANegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangedRetreatRules.IsThreatened(nearestMeleeSquared: 0, threatRadiusRaw: -1));
    }

    [Fact]
    public void IsThreatenedDoesNotOverflowAtTheLargestRadius()
    {
        // threatRadiusRaw squared at int.MaxValue is 4_611_686_014_132_420_609
        // (computed independently, not by calling the method under test),
        // which overflows a 32-bit multiply but not the widened long the
        // implementation uses, and stays well under long.MaxValue.
        const long radiusSquared = 4_611_686_014_132_420_609L;

        Assert.True(
            RangedRetreatRules.IsThreatened(
                nearestMeleeSquared: radiusSquared, threatRadiusRaw: int.MaxValue));
        Assert.False(
            RangedRetreatRules.IsThreatened(
                nearestMeleeSquared: radiusSquared + 1, threatRadiusRaw: int.MaxValue));
    }
}
