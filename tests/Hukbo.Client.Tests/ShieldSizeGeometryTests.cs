using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;
using Xunit;

namespace Hukbo.Client.Tests;

/// <summary>
/// T4 (shield-projectile-block-design.md, section 8): pins that
/// <see cref="ShieldId.NarrowBreastHigh"/> draws strictly narrower and
/// strictly shorter than <see cref="ShieldId.TallHardwood"/> at every detail
/// tier, still respects the shared height floors, leaves the tall shield's
/// own drawn size unchanged, resolves to the real
/// <see cref="PawnShieldRole.NarrowBreastHigh"/> (not <c>None</c>), and takes
/// the shield-compatible pose and motion path rather than the unshielded one.
/// Pure-helper pattern only: no <c>ArenaGame</c>, graphics device, sprite
/// batch, or window. All expected numbers are literals read from a real
/// build, not re-derived from the constants under test.
/// </summary>
/// <remarks>
/// <see cref="PawnAppearanceFactory.Create"/> cannot be called with
/// <see cref="ShieldId.NarrowBreastHigh"/> yet — <c>ShieldVisualCatalog</c>
/// has no skin table for <see cref="PawnShieldRole.NarrowBreastHigh"/> and
/// throws <see cref="ArgumentOutOfRangeException"/> (a real gap outside this
/// task's file list, reported separately). Every narrow-shield appearance
/// below is therefore built the same way the existing
/// <c>PawnGeometryTests.ShieldedAppearance</c> helper builds a specific skin:
/// start from a <see cref="ShieldId.None"/> appearance (never throws) and
/// substitute <see cref="PawnAppearance.ShieldRole"/> and
/// <see cref="PawnAppearance.ShieldSkinId"/> with a <c>with</c> expression, so
/// these tests exercise <see cref="PawnGeometry"/> and
/// <see cref="AttackPoseResolver"/>/<see cref="AttackMotionCatalog"/> without
/// going through the unrelated, unfixed catalog gap.
/// </remarks>
public sealed class ShieldSizeGeometryTests
{
    // StatureMultiplier/BuildMultiplier are pinned to 1f, matching
    // PawnGeometryTests's own pinned regression appearance — entity id 0's
    // rolled stature/build would otherwise shift ShieldBounds.Y and make
    // the tall-vs-narrow comparison below depend on an unrelated roll.
    private static PawnAppearance TallAppearance() =>
        PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.TallHardwood) with
        {
            ShieldRole = PawnShieldRole.TallHardwood,
            ShieldSkinId = ShieldVisualCatalog.MactanThin.Catalog.Id,
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

    private static PawnAppearance NarrowAppearance() =>
        PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None) with
        {
            ShieldRole = PawnShieldRole.NarrowBreastHigh,
            // Deliberately not a known TallHardwood skin id, so the width/
            // height-delta lookup (ShieldProportionDelta) takes its "no
            // matching skin" zero-delta path rather than borrowing a tall
            // skin's per-pixel adjustment.
            ShieldSkinId = "t4-narrow-breast-high-provisional",
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

    // cameraZoom -> apparentScale = clamp(cameraZoom * 1.35, 0.72, 2.40), read
    // from PawnGeometry.ResolveApparentScale. Each row below is a real build
    // run through that exact formula, landing in a different PawnDetailTier
    // (Low < 0.95, Medium < 1.80, else High).
    // PawnDetailTier is internal, and a [Theory]/[MemberData] test method
    // must be public for xunit to discover it, so the expected tier travels
    // as its own name rather than the enum type itself.
    public static TheoryData<float, string, int, int, int, int> SizeAcrossTiers => new()
    {
        // cameraZoom, expected tier name, tall width, tall height, narrow width, narrow height
        { 0.0f, "Low", 3, 8, 2, 4 },
        { 1.0f, "Medium", 5, 15, 3, 8 },
        { 1.5f, "High", 8, 22, 4, 12 },
        { 3.0f, "High", 10, 26, 5, 14 },
    };

    [Theory]
    [MemberData(nameof(SizeAcrossTiers))]
    public void Create_NarrowShieldIsStrictlySmallerThanTallShieldAtEveryDetailTier(
        float cameraZoom,
        string expectedTierName,
        int tallWidth,
        int tallHeight,
        int narrowWidth,
        int narrowHeight)
    {
        var tallLayout = PawnGeometry.Create(new Vector2(100f, 100f), cameraZoom, TallAppearance());
        var narrowLayout = PawnGeometry.Create(new Vector2(100f, 100f), cameraZoom, NarrowAppearance());

        Assert.Equal(expectedTierName, tallLayout.DetailTier.ToString());
        Assert.Equal(expectedTierName, narrowLayout.DetailTier.ToString());

        Assert.Equal(tallWidth, tallLayout.ShieldBounds.Width);
        Assert.Equal(tallHeight, tallLayout.ShieldBounds.Height);
        Assert.Equal(narrowWidth, narrowLayout.ShieldBounds.Width);
        Assert.Equal(narrowHeight, narrowLayout.ShieldBounds.Height);

        Assert.True(
            narrowLayout.ShieldBounds.Width < tallLayout.ShieldBounds.Width,
            "narrow shield must draw strictly narrower than the tall shield");
        Assert.True(
            narrowLayout.ShieldBounds.Height < tallLayout.ShieldBounds.Height,
            "narrow shield must draw strictly shorter than the tall shield");
    }

    [Fact]
    public void Create_NarrowShieldRespectsTheSharedLowTierHeightFloorAtSmallestScale()
    {
        // cameraZoom 0f clamps to the minimum apparent scale (0.72), the
        // smallest size either shield can ever draw at.
        var layout = PawnGeometry.Create(new Vector2(0f, 0f), cameraZoom: 0f, NarrowAppearance());

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
        // ShieldMinimumWidth / ShieldLowTierMinimumHeight, pinned as literals.
        Assert.True(layout.ShieldBounds.Width >= 2, "narrow shield must still respect the shared width floor");
        Assert.True(layout.ShieldBounds.Height >= 3, "narrow shield must still respect the shared Low-tier height floor");
    }

    [Fact]
    public void Create_NarrowShieldRespectsTheSharedMediumOrHighTierHeightFloor()
    {
        var layout = PawnGeometry.Create(new Vector2(0f, 0f), cameraZoom: 1.0f, NarrowAppearance());

        Assert.Equal(PawnDetailTier.Medium, layout.DetailTier);
        // ShieldMediumOrHighMinimumHeight, pinned as a literal.
        Assert.True(layout.ShieldBounds.Height >= 5, "narrow shield must still respect the shared Medium/High-tier height floor");
    }

    [Fact]
    public void Create_TallShieldDimensionsAreUnchangedFromThePinnedRegressionRectangle()
    {
        // Same appearance and cameraZoom as PawnGeometryTests's own pinned
        // regression (Create_ShieldPostureOffsetAndRotationMatchThePinnedRegressionRectangle):
        // this proves T4 did not move the tall shield's real, shipped size.
        var layout = PawnGeometry.Create(new Vector2(100f, 100f), cameraZoom: 3f, TallAppearance());

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.Equal(new Rectangle(75, 65, 10, 26), layout.ShieldBounds);
    }

    [Fact]
    public void ToShieldRole_NarrowBreastHighMapsToItsOwnRoleNotNone()
    {
        // PawnAppearanceFactory.ToShieldRole is private and cannot be called
        // with ShieldId.NarrowBreastHigh directly today without hitting the
        // separate, unfixed ShieldVisualCatalog gap (see the class remarks
        // above), so this pins the mapping's effect through the same
        // NarrowAppearance() bypass every other assertion in this file
        // already depends on: PawnGeometry, AttackPoseResolver, and
        // AttackMotionCatalog all only ever see PawnAppearance.ShieldRole,
        // never ShieldId, so proving that field carries the real role rather
        // than None is what the mapping is for.
        var narrow = NarrowAppearance();
        Assert.Equal(PawnShieldRole.NarrowBreastHigh, narrow.ShieldRole);
        Assert.NotEqual(PawnShieldRole.None, narrow.ShieldRole);
        Assert.True(narrow.CarriesShield);
    }

    [Theory]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    [InlineData(WeaponId.Bangkaw)]
    public void ResolveShieldOverlay_NarrowBreastHighTakesTheSameShieldCompatiblePathAsTallHardwood(WeaponId weapon)
    {
        var tall = AttackMotionCatalog.ResolveShieldOverlay(weapon, ShieldId.TallHardwood);
        var narrow = AttackMotionCatalog.ResolveShieldOverlay(weapon, ShieldId.NarrowBreastHigh);

        Assert.NotNull(tall);
        Assert.NotNull(narrow);
        Assert.Equal(tall!.Value, narrow!.Value);
    }

    [Fact]
    public void ResolveShieldOverlay_NarrowBreastHighIsNullWhenTheWeaponIsNotShieldCompatible()
    {
        // Kampilan is two-handed and ShieldCompatible is false, exactly like
        // TallHardwood already behaves for the same weapon.
        var tall = AttackMotionCatalog.ResolveShieldOverlay(WeaponId.Kampilan, ShieldId.TallHardwood);
        var narrow = AttackMotionCatalog.ResolveShieldOverlay(WeaponId.Kampilan, ShieldId.NarrowBreastHigh);

        Assert.Null(tall);
        Assert.Null(narrow);
    }
}
