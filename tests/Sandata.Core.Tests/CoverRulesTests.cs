using Sandata.Core.Combat;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

/// <summary>
/// Golden vectors for the directional cover model (design section 9 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md, task 30 of
/// Sandata's scaffold plan): arc-boundary exactness reusing
/// <see cref="Geometry.VisionCone"/>'s already-pinned boundary vectors, two
/// operators behind one object diverging, the flank-and-rear bypass, the
/// 32,768 all-directions object, the crouched near-immune case, and the
/// pinned integer rounding rule.
/// </summary>
public sealed class CoverRulesTests
{
    // An east-facing cover object, 45 degrees to either side (half-width
    // 8,192 of 65,536 raw units per turn). Reused across several fixtures
    // below because its boundary vectors are already pinned, independently
    // of this file, by VisionConeTests against the same NarrowCentre and
    // NarrowHalfWidth values.
    private static readonly Bam16 EastCentre = new(0);
    private const ushort NarrowHalfWidth = 8192;

    private static CoverState StandingBehind(Bam16 arcCentreBam, ushort arcHalfBam) =>
        new(InCover: true, arcCentreBam, arcHalfBam, CoverPosture.Standing);

    private static CoverState CrouchedBehind(Bam16 arcCentreBam, ushort arcHalfBam) =>
        new(InCover: true, arcCentreBam, arcHalfBam, CoverPosture.Crouched);

    [Fact]
    public void ShotExactlyOnTheArcBoundary_IsCovered()
    {
        // The right boundary vector of EastCentre/NarrowHalfWidth is pinned
        // to (46341, 46341) by VisionConeTests.PointExactlyOnTheRightEdge_IsInside.
        // A defender at the origin with a shooter exactly at that offset is
        // therefore exactly on the edge of the protected arc.
        var cover = StandingBehind(EastCentre, NarrowHalfWidth);

        Assert.True(CoverRules.IsWithinProtectedArc(
            cover.ArcCentreBam, cover.ArcHalfBam,
            shooterX: 46_341, shooterY: 46_341,
            defenderX: 0, defenderY: 0));
    }

    [Fact]
    public void ShotOneBamUnitBeyondTheArcBoundary_IsNotCovered()
    {
        // One raw BAM unit past the same right edge, pinned to (46336, 46345)
        // by VisionConeTests.PointOneBamUnitBeyondTheRightEdge_IsExcluded.
        var cover = StandingBehind(EastCentre, NarrowHalfWidth);

        Assert.False(CoverRules.IsWithinProtectedArc(
            cover.ArcCentreBam, cover.ArcHalfBam,
            shooterX: 46_336, shooterY: 46_345,
            defenderX: 0, defenderY: 0));
    }

    [Fact]
    public void TwoOperatorsBehindTheSameObject_OneInsideTheArcOneOutside_GetDifferentResults()
    {
        // Both operators are affiliated with the identical cover object (same
        // ArcCentreBam, same ArcHalfBam). The same shooter fires at both from
        // the same world position; only their own position relative to that
        // shooter differs.
        var cover = StandingBehind(EastCentre, NarrowHalfWidth);
        const long shooterX = 100;
        const long shooterY = 0;

        // Operator A stands so the shooter is dead ahead of the object's
        // east-facing arc (dx = 100, dy = 0 from A) — inside.
        var reductionForA = CoverRules.ReductionPercent(
            cover, shooterX, shooterY, defenderX: 0, defenderY: 0);

        // Operator B stands on the far side of the same shooter, so the
        // shooter is now directly behind B relative to the object's facing
        // (dx = -100, dy = 0 from B) — outside.
        var reductionForB = CoverRules.ReductionPercent(
            cover, shooterX, shooterY, defenderX: 200, defenderY: 0);

        Assert.Equal(CoverRules.StandingCoverReductionPercent, reductionForA);
        Assert.Equal(0, reductionForB);
        Assert.NotEqual(reductionForA, reductionForB);
    }

    [Fact]
    public void RearFire_IgnoresCoverEntirely()
    {
        // The object faces east. A shooter directly west of the defender —
        // dead behind the object's facing — is firing from the rear.
        var cover = StandingBehind(EastCentre, NarrowHalfWidth);

        Assert.False(CoverRules.IsWithinProtectedArc(
            cover.ArcCentreBam, cover.ArcHalfBam,
            shooterX: -100, shooterY: 0,
            defenderX: 0, defenderY: 0));
        Assert.Equal(0, CoverRules.ReductionPercent(
            cover, shooterX: -100, shooterY: 0, defenderX: 0, defenderY: 0));
    }

    [Fact]
    public void FlankFire_IgnoresCoverEntirely()
    {
        // The object faces east with only a 45-degree half-width. A shooter
        // due south of the defender is 90 degrees off the facing direction —
        // well outside the arc, and not the direct-rear case above.
        var cover = StandingBehind(EastCentre, NarrowHalfWidth);

        Assert.False(CoverRules.IsWithinProtectedArc(
            cover.ArcCentreBam, cover.ArcHalfBam,
            shooterX: 0, shooterY: 100,
            defenderX: 0, defenderY: 0));
        Assert.Equal(0, CoverRules.ReductionPercent(
            cover, shooterX: 0, shooterY: 100, defenderX: 0, defenderY: 0));
    }

    [Theory]
    [InlineData(100L, 0L)]    // due east
    [InlineData(-100L, 0L)]   // due west (rear, if this were a narrow arc)
    [InlineData(0L, 100L)]    // due south (flank, if this were a narrow arc)
    [InlineData(0L, -100L)]   // due north
    [InlineData(70L, -70L)]   // an arbitrary diagonal
    public void ArcHalfWidth32768_CoversFromEveryDirection(long dx, long dy)
    {
        // arcCentreBam is deliberately not one of the cardinal directions
        // tested here, to prove the all-directions result does not depend on
        // the candidate offset lining up with the centre.
        var arcCentreBam = new Bam16(12_345);
        const ushort fullCoverageHalfWidth = 32_768;
        var cover = StandingBehind(arcCentreBam, fullCoverageHalfWidth);

        Assert.True(CoverRules.IsWithinProtectedArc(
            cover.ArcCentreBam, cover.ArcHalfBam,
            shooterX: dx, shooterY: dy,
            defenderX: 0, defenderY: 0));
        Assert.Equal(CoverRules.StandingCoverReductionPercent, CoverRules.ReductionPercent(
            cover, shooterX: dx, shooterY: dy, defenderX: 0, defenderY: 0));
    }

    [Fact]
    public void CrouchedOperator_InsideTheArc_IsNearImmune()
    {
        // The shooter is dead ahead of the object's east-facing arc — inside
        // it, the same direction that gives a standing operator the ordinary
        // 50 percent. A crouched operator inside the arc gets the near-total
        // figure instead.
        var cover = CrouchedBehind(EastCentre, NarrowHalfWidth);

        var reduction = CoverRules.ReductionPercent(
            cover, shooterX: 100, shooterY: 0, defenderX: 0, defenderY: 0);

        Assert.Equal(CoverRules.CrouchedCoverReductionPercent, reduction);

        var survivingDamage = CoverRules.ApplyToDamage(
            rawDamage: 100, cover, shooterX: 100, shooterY: 0, defenderX: 0, defenderY: 0);
        Assert.Equal(5, survivingDamage);
    }

    [Fact]
    public void RearFireAgainstACrouchedOperator_GetsZeroReduction()
    {
        // This is the case that would have passed under the earlier,
        // corrected reading of the design doc: a shooter due west of the
        // defender is dead behind this east-facing object's arc. "Fire from
        // the flank or rear ignores cover entirely" is unconditional on
        // posture, so a crouched operator caught from the rear is exactly as
        // exposed as a standing one — zero reduction, not near-total.
        var cover = CrouchedBehind(EastCentre, NarrowHalfWidth);

        var reduction = CoverRules.ReductionPercent(
            cover, shooterX: -100, shooterY: 0, defenderX: 0, defenderY: 0);

        Assert.Equal(0, reduction);

        var survivingDamage = CoverRules.ApplyToDamage(
            rawDamage: 100, cover, shooterX: -100, shooterY: 0, defenderX: 0, defenderY: 0);
        Assert.Equal(100, survivingDamage);
    }

    [Theory]
    [InlineData(100L, 0L)]    // due east
    [InlineData(-100L, 0L)]   // due west
    [InlineData(0L, 100L)]    // due south
    [InlineData(0L, -100L)]   // due north
    [InlineData(70L, -70L)]   // an arbitrary diagonal
    public void ArcHalfWidth32768_StillProtectsACrouchedOperatorFromEveryDirection(long dx, long dy)
    {
        var arcCentreBam = new Bam16(12_345);
        const ushort fullCoverageHalfWidth = 32_768;
        var cover = CrouchedBehind(arcCentreBam, fullCoverageHalfWidth);

        Assert.Equal(CoverRules.CrouchedCoverReductionPercent, CoverRules.ReductionPercent(
            cover, shooterX: dx, shooterY: dy, defenderX: 0, defenderY: 0));
    }

    [Fact]
    public void CrouchedOperator_CannotProduceAFireProposal()
    {
        var crouchedInCover = CrouchedBehind(EastCentre, NarrowHalfWidth);
        var crouchedNotInCover = new CoverState(
            InCover: false, ArcCentreBam: default, ArcHalfBam: 0, Posture: CoverPosture.Crouched);

        Assert.False(CoverRules.CanProduceFireProposal(crouchedInCover));
        Assert.False(CoverRules.CanProduceFireProposal(crouchedNotInCover));
    }

    [Fact]
    public void StandingOperator_CanProduceAFireProposal()
    {
        Assert.True(CoverRules.CanProduceFireProposal(StandingBehind(EastCentre, NarrowHalfWidth)));
        Assert.True(CoverRules.CanProduceFireProposal(CoverState.NotInCover));
    }

    [Fact]
    public void OperatorNotInCover_ReceivesNoReduction_StandingOrCrouched()
    {
        Assert.Equal(0, CoverRules.ReductionPercent(
            CoverState.NotInCover, shooterX: 100, shooterY: 0, defenderX: 0, defenderY: 0));

        var crouchedButNotInCover = new CoverState(
            InCover: false, ArcCentreBam: default, ArcHalfBam: 0, Posture: CoverPosture.Crouched);
        Assert.Equal(0, CoverRules.ReductionPercent(
            crouchedButNotInCover, shooterX: 100, shooterY: 0, defenderX: 0, defenderY: 0));
    }

    [Fact]
    public void ApplyPercentageReduction_TruncatesTowardZero_PinnedExample()
    {
        // 25 * 50 / 100 = 1250 / 100 = 12 (12.5 truncates to 12), never 13.
        // This is the rounding rule documented on CoverRules: the surviving
        // value is computed directly and truncated, rather than truncating
        // the removed amount and subtracting.
        Assert.Equal(12, CoverRules.ApplyPercentageReduction(25, 50));
    }

    [Fact]
    public void ApplyPercentageReduction_ExactHalf_HasNoRoundingAmbiguity()
    {
        Assert.Equal(50, CoverRules.ApplyPercentageReduction(100, 50));
    }

    [Fact]
    public void ApplyPercentageReduction_ZeroPercent_ReturnsTheRawValue()
    {
        Assert.Equal(37, CoverRules.ApplyPercentageReduction(37, 0));
    }

    [Fact]
    public void ApplyToHitChancePercent_AppliesTheSameReductionAsDamage()
    {
        var cover = StandingBehind(EastCentre, NarrowHalfWidth);

        var hitChance = CoverRules.ApplyToHitChancePercent(
            rawHitChancePercent: 80, cover,
            shooterX: 100, shooterY: 0, defenderX: 0, defenderY: 0);

        // 80 * 50 / 100 = 40.
        Assert.Equal(40, hitChance);
    }
}
