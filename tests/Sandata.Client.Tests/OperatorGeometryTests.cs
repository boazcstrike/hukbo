using System.Linq;
using Microsoft.Xna.Framework;
using Sandata.Client.Rendering;
using Sandata.Core.Mathematics;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers task 37's done-when bar for <see cref="OperatorGeometry"/> and
/// <see cref="OperatorLayout"/>: every one of the fifteen layers design
/// section 11 names
/// (<c>docs/plans/2026-08-07-sandata-scaffold-design.md:1531-1567</c>) pinned
/// at three detail tiers, absence expressed as <see cref="Rectangle.Empty"/>,
/// continuous wrap-safe weapon rotation about a fixed grip anchor, the
/// muzzle anchor as the weapon line's own rotated tip, and the
/// presentation-only smoothing term's exclusion from
/// <see cref="OperatorLayout"/> equality.
/// </summary>
/// <remarks>
/// No member of this class constructs a <c>GraphicsDevice</c>, a
/// <c>SpriteBatch</c>, or a window — only <see cref="OperatorGeometry.Create"/>,
/// a pure function of plain values, and the <see cref="OperatorLayout"/>
/// record it returns. Every expected <see cref="Rectangle"/> below was
/// derived by hand from <see cref="OperatorGeometry"/>'s own published layer
/// constants and offsets, not copied from a first failing run, following the
/// same discipline <c>WorldRendererGeometryTests</c> uses for map geometry:
/// <c>rootPosition</c> (100, 100), <c>apparentScale</c> 1, and a zero aim
/// angle were chosen so every centered rectangle lands on an exact integer
/// with no rounding ambiguity to hide a wrong formula behind.
/// </remarks>
public sealed class OperatorGeometryTests
{
    private static readonly Vector2 RootPosition = new(100f, 100f);
    private const float ApparentScale = 1f;

    // With rootPosition (100, 100) and apparentScale 1, an unrotated weapon
    // (aim angle zero) puts the grip at (100, 89) — rootPosition plus
    // OperatorGeometry.WeaponGripCenterYOffset — and the muzzle sixteen units
    // along +X at (116, 89) — OperatorGeometry.WeaponLength further along a
    // direction vector of (1, 0).
    private static readonly Vector2 ExpectedGripAnchor = new(100f, 89f);
    private static readonly Vector2 ExpectedMuzzleAnchor = new(116f, 89f);

    private static OperatorLayout CreateUnrotated(
        OperatorDetailTier tier, bool isFiring = false, bool isSelected = false) =>
        OperatorGeometry.Create(
            RootPosition,
            ApparentScale,
            tier,
            weaponAimBam: new Bam16(0),
            previousDisplayRotationRawUnits: 0f,
            smoothingFactor: 1f,
            isFiring,
            isSelected);

    [Fact]
    public void LowTierShowsOnlyTheSixCoreLayersAndLeavesEveryOtherLayerEmpty()
    {
        var layout = CreateUnrotated(OperatorDetailTier.Low);

        Assert.Equal(new Rectangle(94, 94, 12, 12), layout.GroundRingBounds);
        Assert.Equal(new Rectangle(97, 96, 6, 4), layout.BootsBounds);
        Assert.Equal(new Rectangle(97, 91, 6, 6), layout.LegsBounds);
        Assert.Equal(new Rectangle(96, 83, 8, 10), layout.TorsoBounds);
        Assert.Equal(new Rectangle(92, 88, 16, 2), layout.WeaponBodyBounds);
        Assert.Equal(new Rectangle(98, 80, 4, 4), layout.HeadBounds);

        Assert.Equal(Rectangle.Empty, layout.PlateCarrierBounds);
        Assert.Equal(Rectangle.Empty, layout.ArmsBounds);
        Assert.Equal(Rectangle.Empty, layout.WeaponForegripBounds);
        Assert.Equal(Rectangle.Empty, layout.HelmetBounds);
        Assert.Equal(Rectangle.Empty, layout.NightVisionMountBounds);
        Assert.Equal(Rectangle.Empty, layout.MuzzleFlashBounds);
        Assert.Equal(Rectangle.Empty, layout.SlingBounds);
        Assert.Equal(Rectangle.Empty, layout.SuppressionBracketBounds);
        Assert.Equal(Rectangle.Empty, layout.SelectionRingBounds);

        Assert.Equal(ExpectedGripAnchor, layout.WeaponGripAnchor);
        Assert.Equal(ExpectedMuzzleAnchor, layout.WeaponMuzzleAnchor);
    }

    [Fact]
    public void MediumTierAddsGearButNotOpticsOrStateGatedLayers()
    {
        var layout = CreateUnrotated(OperatorDetailTier.Medium);

        // The six core layers are unchanged from the low tier.
        Assert.Equal(new Rectangle(94, 94, 12, 12), layout.GroundRingBounds);
        Assert.Equal(new Rectangle(97, 96, 6, 4), layout.BootsBounds);
        Assert.Equal(new Rectangle(97, 91, 6, 6), layout.LegsBounds);
        Assert.Equal(new Rectangle(96, 83, 8, 10), layout.TorsoBounds);
        Assert.Equal(new Rectangle(92, 88, 16, 2), layout.WeaponBodyBounds);
        Assert.Equal(new Rectangle(98, 80, 4, 4), layout.HeadBounds);

        // Five gear layers switch on.
        Assert.Equal(new Rectangle(95, 84, 10, 6), layout.PlateCarrierBounds);
        Assert.Equal(new Rectangle(93, 87, 14, 4), layout.ArmsBounds);
        Assert.Equal(new Rectangle(102, 88, 4, 2), layout.WeaponForegripBounds);
        Assert.Equal(new Rectangle(97, 78, 6, 6), layout.HelmetBounds);
        Assert.Equal(new Rectangle(95, 87, 10, 2), layout.SlingBounds);

        // The two High-only layers and the two state-gated layers stay empty.
        Assert.Equal(Rectangle.Empty, layout.NightVisionMountBounds);
        Assert.Equal(Rectangle.Empty, layout.SuppressionBracketBounds);
        Assert.Equal(Rectangle.Empty, layout.MuzzleFlashBounds);
        Assert.Equal(Rectangle.Empty, layout.SelectionRingBounds);
    }

    [Fact]
    public void HighTierAddsOpticsAndFiringOrSelectionUnlocksTheFinalTwoLayers()
    {
        var layout = CreateUnrotated(OperatorDetailTier.High, isFiring: true, isSelected: true);

        // Every gear layer from the medium tier is still present.
        Assert.Equal(new Rectangle(95, 84, 10, 6), layout.PlateCarrierBounds);
        Assert.Equal(new Rectangle(93, 87, 14, 4), layout.ArmsBounds);
        Assert.Equal(new Rectangle(102, 88, 4, 2), layout.WeaponForegripBounds);
        Assert.Equal(new Rectangle(97, 78, 6, 6), layout.HelmetBounds);
        Assert.Equal(new Rectangle(95, 87, 10, 2), layout.SlingBounds);

        // The two High-only layers switch on.
        Assert.Equal(new Rectangle(99, 79, 2, 2), layout.NightVisionMountBounds);
        Assert.Equal(new Rectangle(111, 88, 2, 2), layout.SuppressionBracketBounds);

        // Firing and selection unlock the last two layers, for fifteen
        // non-empty rectangles in total — every layer design section 11
        // names.
        Assert.Equal(new Rectangle(114, 87, 4, 4), layout.MuzzleFlashBounds);
        Assert.Equal(new Rectangle(92, 92, 16, 16), layout.SelectionRingBounds);

        var nonEmptyLayerCount = new[]
        {
            layout.GroundRingBounds, layout.BootsBounds, layout.LegsBounds,
            layout.TorsoBounds, layout.PlateCarrierBounds, layout.ArmsBounds,
            layout.WeaponBodyBounds, layout.WeaponForegripBounds, layout.HeadBounds,
            layout.HelmetBounds, layout.NightVisionMountBounds, layout.MuzzleFlashBounds,
            layout.SlingBounds, layout.SuppressionBracketBounds, layout.SelectionRingBounds,
        }.Count(bounds => bounds != Rectangle.Empty);
        Assert.Equal(15, nonEmptyLayerCount);
    }

    [Fact]
    public void MuzzleFlashAndSelectionRingAreEmptyByDefaultAtEveryTier()
    {
        foreach (var tier in new[] { OperatorDetailTier.Low, OperatorDetailTier.Medium, OperatorDetailTier.High })
        {
            var layout = CreateUnrotated(tier);
            Assert.Equal(Rectangle.Empty, layout.MuzzleFlashBounds);
            Assert.Equal(Rectangle.Empty, layout.SelectionRingBounds);
        }
    }

    [Fact]
    public void MuzzleAnchorEqualsTheWeaponLineTip()
    {
        var layout = OperatorGeometry.Create(
            RootPosition,
            ApparentScale,
            OperatorDetailTier.Low,
            weaponAimBam: new Bam16(16_384), // an exact quarter turn.
            previousDisplayRotationRawUnits: 0f,
            smoothingFactor: 1f,
            isFiring: false,
            isSelected: false);

        // Independently recomputed from the grip anchor, the same displayed
        // angle the layout itself carries, and the published weapon length —
        // not copied from OperatorGeometry's internal formula, but the same
        // geometric definition of "the tip of a rotated line".
        var rotationRadians = layout.DisplayRotationRawUnits / Bam16.UnitsPerTurn * MathF.Tau;
        var direction = new Vector2(MathF.Cos(rotationRadians), MathF.Sin(rotationRadians));
        var expectedTip = layout.WeaponGripAnchor + (direction * (OperatorGeometry.WeaponLength * ApparentScale));

        Assert.Equal(expectedTip.X, layout.WeaponMuzzleAnchor.X, precision: 3);
        Assert.Equal(expectedTip.Y, layout.WeaponMuzzleAnchor.Y, precision: 3);
    }

    [Fact]
    public void WeaponRotatesContinuouslyAboutAFixedGripAnchorAcrossAFullTurn()
    {
        const int sectorsPerTurn = 16;
        var gripAnchor = new Vector2(50f, 50f);
        var rootPosition = gripAnchor - (new Vector2(0f, OperatorGeometry.WeaponGripCenterYOffset) * ApparentScale);
        var previousRawUnits = 0f;

        for (var sector = 1; sector <= sectorsPerTurn; sector++)
        {
            var targetRaw = (ushort)((sector * Bam16.UnitsPerFacingSector) % Bam16.UnitsPerTurn);
            var layout = OperatorGeometry.Create(
                rootPosition,
                ApparentScale,
                OperatorDetailTier.Low,
                new Bam16(targetRaw),
                previousRawUnits,
                smoothingFactor: 1f,
                isFiring: false,
                isSelected: false);

            // The pivot never moves...
            Assert.Equal(gripAnchor, layout.WeaponGripAnchor);
            // ...and the muzzle stays at a constant radius from it at every
            // one of the sixteen sectors, including the sector that wraps
            // from 65,535 back to 0.
            var radius = Vector2.Distance(layout.WeaponGripAnchor, layout.WeaponMuzzleAnchor);
            Assert.Equal(OperatorGeometry.WeaponLength * ApparentScale, radius, precision: 3);

            previousRawUnits = layout.DisplayRotationRawUnits;
        }

        // The sixteenth sector wraps exactly back to raw unit 0, completing
        // one full turn with no exception and no discontinuity in the
        // fixed-radius circle every sector above pinned.
        Assert.Equal(0f, previousRawUnits, precision: 3);
    }

    [Fact]
    public void WeaponRotationCrossesTheWrapForwardRatherThanSpringingBack()
    {
        var target = new Bam16(2_000);

        // Frame 1: the shortest arc from 64,000 toward the 2,000 target is
        // +1,536 (forward, through the wrap) rather than -62,000 (the long
        // way back through 32,768). A quarter step of that arc lands at
        // 64,884 — still short of the wrap.
        var frame1 = OperatorGeometry.Create(
            Vector2.Zero, ApparentScale, OperatorDetailTier.Low,
            target, previousDisplayRotationRawUnits: 64_000f, smoothingFactor: 0.25f,
            isFiring: false, isSelected: false);
        Assert.Equal(64_884f, frame1.DisplayRotationRawUnits, precision: 3);

        // Frame 2 is the wrap boundary itself: from 64,884, the shortest arc
        // to 2,000 is now +2,652, which crosses 65,536 back to 0 partway
        // through the step. A wrap-unaware float lerp would instead average
        // the two raw numbers directly — lerp(64884, 2000, 0.25) is
        // approximately 49,163, springing the weapon back across most of the
        // circle in the wrong direction. The wrap-aware blend instead lands
        // just past zero, continuing to converge on 2,000 from the near side.
        var frame2 = OperatorGeometry.Create(
            Vector2.Zero, ApparentScale, OperatorDetailTier.Low,
            target, previousDisplayRotationRawUnits: frame1.DisplayRotationRawUnits, smoothingFactor: 0.25f,
            isFiring: false, isSelected: false);

        Assert.Equal(11f, frame2.DisplayRotationRawUnits, precision: 3);

        var naiveLerpResult = 64_884f + ((2_000f - 64_884f) * 0.25f);
        Assert.True(
            MathF.Abs(frame2.DisplayRotationRawUnits - naiveLerpResult) > 40_000f,
            "The wrap-aware blend must diverge sharply from what an unwrapped float lerp would have produced.");
    }

    [Fact]
    public void DisplayRotationRawUnitsIsExcludedFromEqualityAndHashCode()
    {
        var baseLayout = new OperatorLayout(
            OperatorDetailTier.Medium,
            new Bam16(1_234),
            new Vector2(1f, 2f),
            new Vector2(3f, 4f),
            new Rectangle(0, 0, 1, 1),
            new Rectangle(1, 1, 2, 2),
            new Rectangle(2, 2, 3, 3),
            new Rectangle(3, 3, 4, 4),
            new Rectangle(4, 4, 5, 5),
            new Rectangle(5, 5, 6, 6),
            new Rectangle(6, 6, 7, 7),
            new Rectangle(7, 7, 8, 8),
            new Rectangle(8, 8, 9, 9),
            new Rectangle(9, 9, 10, 10),
            Rectangle.Empty,
            Rectangle.Empty,
            new Rectangle(10, 10, 11, 11),
            Rectangle.Empty,
            Rectangle.Empty)
        {
            DisplayRotationRawUnits = 111f,
        };

        var sameExceptSmoothing = baseLayout with { DisplayRotationRawUnits = 999f };

        Assert.NotEqual(baseLayout.DisplayRotationRawUnits, sameExceptSmoothing.DisplayRotationRawUnits);
        Assert.Equal(baseLayout, sameExceptSmoothing);
        Assert.Equal(baseLayout.GetHashCode(), sameExceptSmoothing.GetHashCode());
    }
}
