using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// The consolidated tier and threshold boundary suite (implementation-plan-
/// draft.md VIS-040; visual-system-integration-design.md section 7, R-X.4,
/// R-W3.13, R-W5.6). Every shipped catalog entry's <c>MinimumDetailTier</c>
/// is walked through <see cref="DetailTierGate"/> at both sides of its own
/// apparent-scale threshold, the grass zoom bands
/// (<see cref="GrassGeometry.GetZoomBand"/>) are walked at their exact pinned
/// 0.3/2.0 boundaries, and the decal apparent-scale clamp
/// (<see cref="PlainsBackdropGeometry.GetDecalScreenBounds"/>) is confirmed
/// unchanged at its exact 0.35/3.0 bounds — so a threshold moved anywhere in
/// the visual system fails in this one obvious place rather than being
/// caught piecemeal, or missed, across the per-feature test files that own
/// the underlying formulas (<see cref="DetailTierGateTests"/>,
/// <c>GrassGeometryTests</c>, <c>PlainsBackdropGeometryTests</c>). This suite
/// never asserts a new threshold and never re-derives one: it only walks the
/// exact pinned values those sibling files already own, against the full
/// shipped roster.
/// </summary>
public sealed class DetailTierBoundaryTests
{
    // --- Pawn-layer tier boundaries (R-X.4): the exact 0.95/1.80 apparent-
    // scale values DetailTierGateTests pins for the gate itself; this suite
    // only reuses them to walk every catalog entry's own MinimumDetailTier. ---

    private const float JustBelowMediumThreshold = 0.9499999f;
    private const float MediumThreshold = 0.95f;
    private const float JustBelowHighThreshold = 1.79f;
    private const float HighThreshold = 1.80f;

    // --- Every shipped catalog entry, by identifier, mapped to its own
    // MinimumDetailTier. VisualCatalogEntry and VisualDetailTier are both
    // internal, so the identifier — not the entry or the tier enum itself —
    // is what crosses the public [Theory]/[MemberData] boundary, the same
    // discipline AgentInspectorContentTests.AllWeaponTintIds records for its
    // own internal record type. ---

    private static readonly Dictionary<string, VisualDetailTier> EntryTiersById =
        BuildEntryTiersById();

    private static Dictionary<string, VisualDetailTier> BuildEntryTiersById()
    {
        var tiersById = new Dictionary<string, VisualDetailTier>();

        foreach (var entry in AppearanceComponentCatalog.All)
        {
            tiersById[entry.Catalog.Id] = entry.Catalog.MinimumDetailTier;
        }

        foreach (var entry in BackdropVisualCatalog.All)
        {
            tiersById[entry.Id] = entry.MinimumDetailTier;
        }

        foreach (var entry in AllWeaponEntries())
        {
            tiersById[entry.Id] = entry.MinimumDetailTier;
        }

        foreach (var entry in AllShieldEntries())
        {
            tiersById[entry.Id] = entry.MinimumDetailTier;
        }

        return tiersById;
    }

    private static IEnumerable<VisualCatalogEntry> AllWeaponEntries()
    {
        foreach (var silhouette in WeaponVisualCatalog.KalisSilhouettes)
        {
            yield return silhouette.Catalog;
        }

        foreach (var silhouette in WeaponVisualCatalog.KampilanSilhouettes)
        {
            yield return silhouette.Catalog;
        }

        foreach (var silhouette in WeaponVisualCatalog.WasaySilhouettes)
        {
            yield return silhouette.Catalog;
        }

        foreach (var silhouette in WeaponVisualCatalog.ItakSilhouettes)
        {
            yield return silhouette.Catalog;
        }

        foreach (var silhouette in WeaponVisualCatalog.BangkawSilhouettes)
        {
            yield return silhouette.Catalog;
        }

        foreach (var silhouette in WeaponVisualCatalog.BusogSilhouettes)
        {
            yield return silhouette.Catalog;
        }

        foreach (var silhouette in WeaponVisualCatalog.ArquebusSilhouettes)
        {
            yield return silhouette.Catalog;
        }

        // Every defined PawnWeaponRole's tints, matching
        // AgentInspectorContentTests.AllWeaponTintIds' own enumeration.
        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            foreach (var tint in WeaponVisualCatalog.GetTints(weapon))
            {
                yield return tint.Catalog;
            }
        }

        yield return WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog;
    }

    private static IEnumerable<VisualCatalogEntry> AllShieldEntries()
    {
        foreach (var skin in ShieldVisualCatalog.TallHardwoodSkins)
        {
            yield return skin.Catalog;
        }

        yield return ShieldVisualCatalog.Default.Catalog;
        yield return ShieldVisualCatalog.ModelCategoryDefault.Catalog;
    }

    public static TheoryData<string> AllCatalogEntryIds()
    {
        var data = new TheoryData<string>();
        foreach (var id in EntryTiersById.Keys)
        {
            data.Add(id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCatalogEntryIds))]
    public void ShouldDraw_AtBothSidesOfTheEntrysOwnMinimumDetailTierThreshold(string id)
    {
        var tier = EntryTiersById[id];
        switch (tier)
        {
            case VisualDetailTier.Low:
                // Low carries no lower threshold at all — it is already the
                // least detailed classification, so it draws at the lowest
                // apparent scale the gate ever classifies.
                Assert.True(DetailTierGate.ShouldDraw(0f, tier), id);
                break;

            case VisualDetailTier.Medium:
                Assert.False(DetailTierGate.ShouldDraw(JustBelowMediumThreshold, tier), id);
                Assert.True(DetailTierGate.ShouldDraw(MediumThreshold, tier), id);
                break;

            case VisualDetailTier.High:
                Assert.False(DetailTierGate.ShouldDraw(JustBelowHighThreshold, tier), id);
                Assert.True(DetailTierGate.ShouldDraw(HighThreshold, tier), id);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(tier), tier, null);
        }
    }

    [Fact]
    public void AllCatalogEntryIds_CoversEveryShippedEntryAcrossEveryCatalog()
    {
        // 69 today: 27 weapon (10 silhouettes: 7 melee + 3 RU-10 ranged;
        // 16 tints: 10 melee + 6 RU-10 ranged; 1 model-category default),
        // 6 shield (4 skins + family default + model-category default),
        // 1 backdrop, 35 appearance. A hard count, not a floor, so a future
        // catalog addition updates this test deliberately rather than
        // silently expanding the "every shipped element" claim below it.
        Assert.Equal(69, EntryTiersById.Count);
    }

    // --- Grass zoom band boundaries (R-W5.6): the exact pinned 0.3/2.0
    // camera-zoom thresholds GrassGeometryTests pins for GetZoomBand itself;
    // this suite only re-confirms them alongside every other threshold in
    // the system. ---

    [Theory]
    [InlineData(0f, GrassZoomBand.Far)]
    [InlineData(0.2999999f, GrassZoomBand.Far)]
    [InlineData(0.3f, GrassZoomBand.Mid)]
    [InlineData(1.9999999f, GrassZoomBand.Mid)]
    [InlineData(2.0f, GrassZoomBand.Near)]
    public void GetZoomBand_AtBothSidesOfEachPinnedThreshold(
        float cameraZoom,
        GrassZoomBand expectedBand)
    {
        Assert.Equal(expectedBand, GrassGeometry.GetZoomBand(cameraZoom));
    }

    [Fact]
    public void GrassZoomBandThresholds_AreUnchangedFromTheirPinnedValues()
    {
        Assert.Equal(0.3f, GrassGeometry.FarZoomThreshold);
        Assert.Equal(2.0f, GrassGeometry.NearZoomThreshold);
    }

    // --- Decal apparent-scale clamp (R-X.4 keeps the existing machinery
    // unchanged): the exact pinned 0.35/3.0 bounds
    // PlainsBackdropGeometryTests pins for GetDecalScreenBounds itself. ---

    [Fact]
    public void DecalApparentScaleClamp_IsUnchangedFromItsPinnedValues()
    {
        Assert.Equal(0.35f, PlainsBackdropGeometry.MinimumDecalApparentScale);
        Assert.Equal(3.0f, PlainsBackdropGeometry.MaximumDecalApparentScale);
    }

    [Fact]
    public void GetDecalScreenBounds_ClampsAtBothSidesOfTheMinimumApparentScale()
    {
        var anchor = new Vector2(100, 100);

        // Just inside the floor: the unclamped scale (cameraZoom *
        // decalScaleFactor) equals the minimum exactly, so it passes through
        // unclamped.
        var atFloor = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: PlainsBackdropGeometry.MinimumDecalApparentScale,
            decalScaleFactor: 1f);
        var expectedAtFloor = Math.Max(
            1,
            (int)MathF.Round(
                PlainsBackdropGeometry.DecalBaseSize *
                PlainsBackdropGeometry.MinimumDecalApparentScale));
        Assert.Equal(expectedAtFloor, atFloor.Width);

        // Just below the floor: the unclamped scale would be smaller, but
        // the clamp holds it at the same floor size.
        var belowFloor = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: 0.01f,
            decalScaleFactor: 0.001f);
        Assert.Equal(expectedAtFloor, belowFloor.Width);
    }

    [Fact]
    public void GetDecalScreenBounds_ClampsAtBothSidesOfTheMaximumApparentScale()
    {
        var anchor = new Vector2(100, 100);

        // Just inside the ceiling: the unclamped scale equals the maximum
        // exactly, so it passes through unclamped.
        var atCeiling = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: PlainsBackdropGeometry.MaximumDecalApparentScale,
            decalScaleFactor: 1f);
        var expectedAtCeiling = Math.Max(
            1,
            (int)MathF.Round(
                PlainsBackdropGeometry.DecalBaseSize *
                PlainsBackdropGeometry.MaximumDecalApparentScale));
        Assert.Equal(expectedAtCeiling, atCeiling.Width);

        // Just above the ceiling: the unclamped scale would be larger, but
        // the clamp holds it at the same ceiling size.
        var aboveCeiling = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: 100f,
            decalScaleFactor: 100f);
        Assert.Equal(expectedAtCeiling, aboveCeiling.Width);
    }
}
