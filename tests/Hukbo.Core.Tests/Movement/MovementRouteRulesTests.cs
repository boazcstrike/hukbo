using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The pure route arithmetic of the weapon-relative movement design,
/// sections 6 and 10: the exact <c>StepEndpoint</c> contract of section
/// 10.1 including its zero-move fallback and clamp, the verbatim oblique
/// rotation of section 10.2 with its degenerate table substitution, the
/// side parity of section 10.3, and the retained-pace and clearance
/// arithmetic of sections 4.4 and 6.5. Every value asserted here was
/// computed by hand from the design's integer formulas.
/// </summary>
public sealed class MovementRouteRulesTests
{
    private const int MapWidthRaw = 200 * 1024;
    private const int MapHeightRaw = 100 * 1024;
    private const int BodyRadiusRaw = 512;

    // ----- StepEndpoint (design 10.1) -----

    [Fact]
    public void StepEndpointRejectsAZeroDelta()
    {
        Assert.Null(
            MovementRouteRules.StepEndpoint(
                10_240, 10_240, 0, 0, 256,
                MapWidthRaw, MapHeightRaw, BodyRadiusRaw));
    }

    [Fact]
    public void StepEndpointScalesTheDeltaByPaceOverDistance()
    {
        // Delta (300, 400) has exact distance 500. Pace 100 moves exactly
        // one fifth of each axis: (60, 80).
        var endpoint = MovementRouteRules.StepEndpoint(
            10_000, 20_000, 300, 400, 100,
            MapWidthRaw, MapHeightRaw, BodyRadiusRaw);

        Assert.Equal((10_060, 20_080), endpoint);
    }

    [Fact]
    public void StepEndpointTruncatesTowardZeroOnANonDivisibleCase()
    {
        // Delta (10, 3): distance = isqrt(109) = 10. Pace 7:
        // moveX = 10*7/10 = 7; moveY = 3*7/10 = 2 (2.1 truncates to 2).
        var forward = MovementRouteRules.StepEndpoint(
            10_000, 20_000, 10, 3, 7,
            MapWidthRaw, MapHeightRaw, BodyRadiusRaw);
        Assert.Equal((10_007, 20_002), forward);

        // The mirrored delta truncates toward zero, not toward negative
        // infinity: moveY = -3*7/10 = -2, exactly the negation.
        var mirrored = MovementRouteRules.StepEndpoint(
            10_000, 20_000, -10, -3, 7,
            MapWidthRaw, MapHeightRaw, BodyRadiusRaw);
        Assert.Equal((9_993, 19_998), mirrored);
    }

    [Fact]
    public void StepEndpointZeroMoveFallbackMovesOneRawUnitOnTheGreaterAxis()
    {
        // Pace 0 truncates both axes to zero; |dy| > |dx| moves Y by one
        // raw unit in the delta's direction.
        var endpoint = MovementRouteRules.StepEndpoint(
            10_000, 20_000, 3, -9, 0,
            MapWidthRaw, MapHeightRaw, BodyRadiusRaw);

        Assert.Equal((10_000, 19_999), endpoint);
    }

    [Fact]
    public void StepEndpointZeroMoveFallbackXWinsAnExactTie()
    {
        var endpoint = MovementRouteRules.StepEndpoint(
            10_000, 20_000, -5, 5, 0,
            MapWidthRaw, MapHeightRaw, BodyRadiusRaw);

        Assert.Equal((9_999, 20_000), endpoint);
    }

    [Fact]
    public void StepEndpointClampsTheEndpointToTheMap()
    {
        // An actor already at the eastern clamp limit stepping east stays
        // at mapWidth - bodyRadius.
        var limitX = MapWidthRaw - BodyRadiusRaw;
        var endpoint = MovementRouteRules.StepEndpoint(
            limitX, 20_000, 1_000, 0, 500,
            MapWidthRaw, MapHeightRaw, BodyRadiusRaw);

        Assert.Equal((limitX, 20_000), endpoint);
    }

    // ----- Oblique rotation (design 10.2) -----

    [Fact]
    public void RotateObliqueMatchesTheVerbatimFormulas()
    {
        // East (1024, 0): clockwise = (946, 392), the sector-1 vector;
        // counter-clockwise = (946, -392), the sector-15 vector.
        Assert.Equal(
            (946L, 392L),
            MovementRouteRules.RotateOblique(1_024, 0, clockwise: true));
        Assert.Equal(
            (946L, -392L),
            MovementRouteRules.RotateOblique(1_024, 0, clockwise: false));
    }

    [Fact]
    public void RotateObliqueTruncatesTowardZero()
    {
        // Delta (100, 50), clockwise:
        // x = (946*100 - 392*50)/1024 = 75000/1024 = 73 (73.24)
        // y = (392*100 + 946*50)/1024 = 86500/1024 = 84 (84.47)
        Assert.Equal(
            (73L, 84L),
            MovementRouteRules.RotateOblique(100, 50, clockwise: true));

        // The negated delta produces the exact negation, proving the
        // division truncates toward zero rather than flooring.
        Assert.Equal(
            (-73L, -84L),
            MovementRouteRules.RotateOblique(-100, -50, clockwise: true));
    }

    [Fact]
    public void RotateObliqueSubstitutesTheRotatedTableVectorWhenDegenerate()
    {
        // Delta (1, 0) truncates both oblique axes to zero. Its world
        // sector is 0 (East); one clockwise step is sector 1's table
        // vector, one counter-clockwise step is sector 15's.
        Assert.Equal(
            (946L, 392L),
            MovementRouteRules.RotateOblique(1, 0, clockwise: true));
        Assert.Equal(
            (946L, -392L),
            MovementRouteRules.RotateOblique(1, 0, clockwise: false));
    }

    [Fact]
    public void RotateObliqueRejectsAZeroDelta()
    {
        Assert.Throws<ArgumentException>(
            () => MovementRouteRules.RotateOblique(0, 0, clockwise: true));
    }

    [Fact]
    public void RotateObliqueMirrorsExactly()
    {
        // Reflecting the input across the vertical axis and swapping the
        // rotation direction reflects the output exactly, which is the
        // property the mirrored-duel reflection test relies on.
        var (clockwiseX, clockwiseY) =
            MovementRouteRules.RotateOblique(2_335, 850, clockwise: true);
        var (mirroredX, mirroredY) =
            MovementRouteRules.RotateOblique(-2_335, 850, clockwise: false);

        Assert.Equal(-clockwiseX, mirroredX);
        Assert.Equal(clockwiseY, mirroredY);
    }

    // ----- Perpendicular (design 10.4 escape fallback) -----

    [Fact]
    public void PerpendicularVectorRotatesNinetyDegrees()
    {
        // Positive Y is screen-down, so the clockwise perpendicular of
        // east is south and the counter-clockwise one is north.
        Assert.Equal(
            (0L, 1_024L),
            MovementRouteRules.PerpendicularVector(1_024, 0, clockwise: true));
        Assert.Equal(
            (0L, -1_024L),
            MovementRouteRules.PerpendicularVector(1_024, 0, clockwise: false));
    }

    // ----- Side parity (design 10.3) -----

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(2, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    public void SideAParityIsCanonicalClockwiseForEvenIndexesAndSwapsForFactionOne(
        int factionLocalIndex,
        int factionId,
        bool expectedWorldClockwise)
    {
        Assert.Equal(
            expectedWorldClockwise,
            MovementRouteRules.SideAIsWorldClockwise(
                factionLocalIndex, factionId));
    }

    // ----- The pace model (design 4.4, 6.4, 6.5) -----

    [Fact]
    public void DesiredPaceRawScalesAndTruncates()
    {
        // 512 * 9800 / 10000 = 501 (501.76 truncates).
        Assert.Equal(501, MovementRouteRules.DesiredPaceRaw(512, 9_800));

        // The full 10000 basis points is exactly the speed.
        Assert.Equal(512, MovementRouteRules.DesiredPaceRaw(512, 10_000));
    }

    [Fact]
    public void PaceStepRawFloorsAtOneRawUnit()
    {
        // 1 * 3000 / 10000 truncates to zero; the floor keeps the pace
        // able to move.
        Assert.Equal(1, MovementRouteRules.PaceStepRaw(1, 3_000));
        Assert.Equal(256, MovementRouteRules.PaceStepRaw(512, 5_000));
    }

    [Fact]
    public void AdvanceRetainedPaceNeverOvershootsInEitherDirection()
    {
        // Accelerating from 0 toward 501 by 256: two full steps then the
        // remainder, never past the target.
        Assert.Equal(
            256, MovementRouteRules.AdvanceRetainedPaceRaw(0, 501, 256, 307));
        Assert.Equal(
            501,
            MovementRouteRules.AdvanceRetainedPaceRaw(499, 501, 256, 307));

        // Decelerating from 512 toward 150 by 307: one bounded step, then
        // clamped at the target.
        Assert.Equal(
            205,
            MovementRouteRules.AdvanceRetainedPaceRaw(512, 150, 256, 307));
        Assert.Equal(
            150,
            MovementRouteRules.AdvanceRetainedPaceRaw(205, 150, 256, 307));

        // Equal stays exactly equal.
        Assert.Equal(
            150,
            MovementRouteRules.AdvanceRetainedPaceRaw(150, 150, 256, 307));
    }

    // ----- Clearance and preferred-distance arithmetic (design 4.4) -----

    [Fact]
    public void ClearanceRadiusRawScalesBodyDiameterAndTruncates()
    {
        // Body radius 512 gives diameter 1024; Kampilan's 15000 basis
        // points is exactly 1.5 diameters.
        Assert.Equal(1_536, MovementRouteRules.ClearanceRadiusRaw(512, 15_000));

        // A non-divisible case truncates toward zero:
        // 1024 * 11500 / 10000 = 1177 (1177.6).
        Assert.Equal(1_177, MovementRouteRules.ClearanceRadiusRaw(512, 11_500));
    }

    [Fact]
    public void EffectivePreferredDistanceAppliesTheOpponentOffsetCell()
    {
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);
        var kampilan = ruleset.ResolveLoadoutProfile(
            new CombatLoadout(
                WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));

        // Kampilan against Kampilan (offset 0): 5120 * 11500 / 10000 = 5888.
        Assert.Equal(
            5_888L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                5_120, kampilan, opponentCanonicalIndex: 0));

        // Kampilan against Itak (offset +500): 5120 * 12000 / 10000 = 6144.
        Assert.Equal(
            6_144L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                5_120, kampilan, opponentCanonicalIndex: 3));

        // A negative offset shortens: Itak against Kampilan (offset -750):
        // 5120 * (11000 - 750) / 10000 = 5248.
        var itak = ruleset.ResolveLoadoutProfile(
            new CombatLoadout(
                WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None));
        Assert.Equal(
            5_248L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                5_120, itak, opponentCanonicalIndex: 0));
    }

    // ----- Canonical opponent index and bucket occupancy -----

    [Fact]
    public void CanonicalOpponentIndexMatchesTheCanonicalOrder()
    {
        Assert.Equal(0, MovementRouteRules.CanonicalOpponentIndex(
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None)));
        Assert.Equal(1, MovementRouteRules.CanonicalOpponentIndex(
            new CombatLoadout(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None)));
        Assert.Equal(2, MovementRouteRules.CanonicalOpponentIndex(
            new CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None)));
        Assert.Equal(3, MovementRouteRules.CanonicalOpponentIndex(
            new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None)));
        Assert.Equal(4, MovementRouteRules.CanonicalOpponentIndex(
            new CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood)));
        Assert.Equal(5, MovementRouteRules.CanonicalOpponentIndex(
            new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood)));
    }

    [Fact]
    public void CanonicalOpponentIndexIsRankIndependentAndThrowsForUnmapped()
    {
        Assert.Equal(
            0,
            MovementRouteRules.CanonicalOpponentIndex(
                new CombatLoadout(
                    WeaponId.Kampilan,
                    ArmorId.LightOrganic,
                    ShieldId.None,
                    RankId.Datu)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovementRouteRules.CanonicalOpponentIndex(
                new CombatLoadout(
                    WeaponId.Kampilan,
                    ArmorId.LightOrganic,
                    ShieldId.TallHardwood)));
    }

    [Fact]
    public void OccupiedLoadoutBucketsCountsNonZeroBuckets()
    {
        Assert.Equal(
            0,
            MovementRouteRules.OccupiedLoadoutBuckets(
                default(LoadoutCompositionCounts)));
        Assert.Equal(
            1,
            MovementRouteRules.OccupiedLoadoutBuckets(
                new LoadoutCompositionCounts(0, 5, 0, 0, 0, 0)));
        Assert.Equal(
            3,
            MovementRouteRules.OccupiedLoadoutBuckets(
                new LoadoutCompositionCounts(1, 0, 2, 0, 0, 7)));
    }

    // ----- The conflict-pass phase order (design 10.6) -----

    [Fact]
    public void ConflictPhaseSafetyOrderIsPinned()
    {
        Assert.Equal(0, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Disengage));
        Assert.Equal(1, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Recover));
        Assert.Equal(2, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Commit));
        Assert.Equal(3, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Regroup));
        Assert.Equal(4, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Engage));
        Assert.Equal(5, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Approach));
        Assert.Equal(6, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Pursue));
        Assert.Equal(7, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.None));
        Assert.Equal(7, MovementRouteRules.ConflictPhaseSafetyRank(FootworkPhase.Refuse));
    }
}
