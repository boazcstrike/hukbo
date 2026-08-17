using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the <c>appearance.armorVariant.*</c> catalog
/// (<see cref="ArmorVisualCatalog"/>, armor-identity-design.md section 5):
/// exactly ten declared rows, every row's baseline and evidence tier, the
/// deliberate absence of a row for <see cref="ArmorId.LightOrganic"/>, and
/// the deterministic selection stream.
/// </summary>
public sealed class ArmorVisualCatalogTests
{
    private static ArmorVariantEntry[] AllVariants() =>
    [
        ArmorVisualCatalog.Baluti,
        ArmorVisualCatalog.SleevedCotton,
        ArmorVisualCatalog.Rattan,
        ArmorVisualCatalog.CordedRope,
        ArmorVisualCatalog.CarabaoHide,
        ArmorVisualCatalog.Hardwood,
        ArmorVisualCatalog.WoodAndHorn,
        ArmorVisualCatalog.BambooCuirass,
        ArmorVisualCatalog.ImportedIron,
        ArmorVisualCatalog.BareTorso,
    ];

    // --- Structure: exactly ten rows across the whole catalog ---

    [Fact]
    public void Catalog_HasExactlyTenVariants()
    {
        Assert.Equal(10, AllVariants().Length);
    }

    [Fact]
    public void AllVariants_AcrossEveryBaseline_TotalExactlyTen()
    {
        // Cross-checks the flat AllVariants() list above against the same
        // roster reached through GetVariants(), one ArmorId at a time, so a
        // variant added to one list but not the other is caught here rather
        // than by two tests silently agreeing on the wrong number.
        var total =
            ArmorVisualCatalog.GetVariants(ArmorId.None).Count +
            ArmorVisualCatalog.GetVariants(ArmorId.QuiltedCotton).Count +
            ArmorVisualCatalog.GetVariants(ArmorId.HideCorselet).Count +
            ArmorVisualCatalog.GetVariants(ArmorId.RigidCuirass).Count +
            ArmorVisualCatalog.GetVariants(ArmorId.ImportedIron).Count +
            ArmorVisualCatalog.GetVariants(ArmorId.LightOrganic).Count;

        Assert.Equal(10, total);
    }

    [Fact]
    public void AllVariants_HaveWellFormedDistinctIds()
    {
        var ids = AllVariants().Select(entry => entry.Catalog.Id).ToArray();

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllVariants_UseTheAppearanceArmorVariantFamily()
    {
        foreach (var entry in AllVariants())
        {
            Assert.StartsWith("appearance.armorVariant.", entry.Catalog.Id);
        }
    }

    // --- Every variant's baseline resolves to a defined ArmorId, and every
    // baseline except LightOrganic carries at least one variant ---

    [Fact]
    public void AllVariants_CarryADefinedArmorIdBaseline()
    {
        foreach (var entry in AllVariants())
        {
            Assert.True(
                Enum.IsDefined(entry.Baseline),
                $"'{entry.Catalog.Id}' baseline {entry.Baseline} is not a " +
                "defined ArmorId member.");
        }
    }

    [Theory]
    [InlineData(ArmorId.None)]
    [InlineData(ArmorId.QuiltedCotton)]
    [InlineData(ArmorId.HideCorselet)]
    [InlineData(ArmorId.RigidCuirass)]
    [InlineData(ArmorId.ImportedIron)]
    public void EveryBaselineExceptLightOrganic_HasAtLeastOneVariant(ArmorId baseline)
    {
        Assert.NotEmpty(ArmorVisualCatalog.GetVariants(baseline));
    }

    [Fact]
    public void GetVariants_ForLightOrganicIsEmptyRatherThanThrowing()
    {
        // LightOrganic is the legacy baseline no new preset fields (design
        // section 4); an old replay must still resolve totally rather than
        // crash the client that renders it.
        Assert.Empty(ArmorVisualCatalog.GetVariants(ArmorId.LightOrganic));
    }

    [Fact]
    public void GetVariants_ForAnUndefinedArmorIdThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArmorVisualCatalog.GetVariants((ArmorId)999));
    }

    // --- Every variant's evidence tier is one of the four VisualEvidenceTier
    // members, and each row carries its exact designed tier ---

    [Fact]
    public void AllVariants_CarryADefinedEvidenceTierAndANonEmptyNote()
    {
        foreach (var entry in AllVariants())
        {
            Assert.True(Enum.IsDefined(entry.Catalog.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Catalog.Notes));
        }
    }

    [Fact]
    public void Baluti_CarriesTheDocumentedEvidenceTierAndBaselinesToQuiltedCotton()
    {
        Assert.Equal(VisualEvidenceTier.Documented, ArmorVisualCatalog.Baluti.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.QuiltedCotton, ArmorVisualCatalog.Baluti.Baseline);
        Assert.Equal("Baluti — Quilted Armor", ArmorVisualCatalog.Baluti.Catalog.DisplayLabel);
    }

    [Fact]
    public void SleevedCotton_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToQuiltedCotton()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.SleevedCotton.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.QuiltedCotton, ArmorVisualCatalog.SleevedCotton.Baseline);
    }

    [Fact]
    public void Rattan_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToQuiltedCotton()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.Rattan.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.QuiltedCotton, ArmorVisualCatalog.Rattan.Baseline);
    }

    [Fact]
    public void CordedRope_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToQuiltedCotton()
    {
        // The design document's own summary table lists this row as
        // "Documented"; the underlying research document
        // (HISTORICAL_1500s_ARMOR.md section 4.1) grades its source
        // "Documented, form uncertain", and CLAUDE.md section 7 makes the
        // research document's grading binding. This pin is the guard that
        // keeps a future edit from "correcting" the tier back to the
        // design table's figure.
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.CordedRope.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.QuiltedCotton, ArmorVisualCatalog.CordedRope.Baseline);
    }

    [Fact]
    public void CarabaoHide_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToHideCorselet()
    {
        // Same research-over-design tier rule as CordedRope above.
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.CarabaoHide.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.HideCorselet, ArmorVisualCatalog.CarabaoHide.Baseline);
    }

    [Fact]
    public void Hardwood_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToRigidCuirass()
    {
        // Same research-over-design tier rule as CordedRope above.
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.Hardwood.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.RigidCuirass, ArmorVisualCatalog.Hardwood.Baseline);
    }

    [Fact]
    public void WoodAndHorn_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToRigidCuirass()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.WoodAndHorn.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.RigidCuirass, ArmorVisualCatalog.WoodAndHorn.Baseline);
    }

    [Fact]
    public void BambooCuirass_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToRigidCuirass()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.BambooCuirass.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.RigidCuirass, ArmorVisualCatalog.BambooCuirass.Baseline);
    }

    [Fact]
    public void ImportedIron_CarriesTheDocumentedFormUncertainEvidenceTierAndBaselinesToImportedIron()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ArmorVisualCatalog.ImportedIron.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.ImportedIron, ArmorVisualCatalog.ImportedIron.Baseline);

        // The contested Scott (1994) reading must be disclosed, not
        // resolved, in the note.
        Assert.Contains(
            "Scott",
            ArmorVisualCatalog.ImportedIron.Catalog.Notes,
            StringComparison.Ordinal);
        Assert.Contains(
            "contested",
            ArmorVisualCatalog.ImportedIron.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BareTorso_CarriesTheDocumentedEvidenceTierAndBaselinesToNone()
    {
        Assert.Equal(VisualEvidenceTier.Documented, ArmorVisualCatalog.BareTorso.Catalog.EvidenceTier);
        Assert.Equal(ArmorId.None, ArmorVisualCatalog.BareTorso.Baseline);
    }

    // --- No row for LightOrganic: an unattested Filipino name added later
    // for it must go red, since GetVariants(LightOrganic) is pinned empty
    // above and every declared variant is enumerated in AllVariants() ---

    [Fact]
    public void NoDeclaredVariant_BaselinesToLightOrganic()
    {
        // Per docs/research/HISTORICAL_1500s_ARMOR.md section 7 and design
        // section 4, LightOrganic is the legacy, unfielded baseline. This
        // pin, together with Catalog_HasExactlyTenVariants above, is what
        // makes a future variant wrongly attached to LightOrganic fail here
        // rather than silently shipping.
        Assert.DoesNotContain(
            AllVariants(),
            entry => entry.Baseline == ArmorId.LightOrganic);
    }

    // --- Deterministic selection stream ---

    [Fact]
    public void SelectVariant_IsStableAcrossRepeatedCallsForTheSameEntityId()
    {
        var first = ArmorVisualCatalog.SelectVariant(23, ArmorId.QuiltedCotton);
        var second = ArmorVisualCatalog.SelectVariant(23, ArmorId.QuiltedCotton);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectVariant_ForQuiltedCottonAlwaysReturnsOneOfItsFourVariants()
    {
        ArmorVariantEntry[] allowed =
        [
            ArmorVisualCatalog.Baluti,
            ArmorVisualCatalog.SleevedCotton,
            ArmorVisualCatalog.Rattan,
            ArmorVisualCatalog.CordedRope,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(
                ArmorVisualCatalog.SelectVariant(entityId, ArmorId.QuiltedCotton),
                allowed);
        }
    }

    [Fact]
    public void SelectVariant_ReachesMoreThanOneVariantAcrossTheSampledEntityIdRange()
    {
        var distinctIds = Enumerable.Range(0, 200)
            .Select(entityId => ArmorVisualCatalog
                .SelectVariant((ulong)entityId, ArmorId.QuiltedCotton)
                .Catalog.Id)
            .Distinct()
            .Count();

        Assert.True(
            distinctIds > 1,
            "Expected the armor variant stream to reach more than one " +
            "variant over 200 sampled entity IDs.");
    }

    [Fact]
    public void SelectVariant_ForASingleVariantBaselineAlwaysReturnsThatVariant()
    {
        for (ulong entityId = 0; entityId < 50; entityId++)
        {
            Assert.Equal(
                ArmorVisualCatalog.CarabaoHide,
                ArmorVisualCatalog.SelectVariant(entityId, ArmorId.HideCorselet));
        }
    }

    [Fact]
    public void SelectVariant_FallsThroughToTheDefaultForLightOrganic()
    {
        Assert.Equal(
            ArmorVisualCatalog.Default,
            ArmorVisualCatalog.SelectVariant(11, ArmorId.LightOrganic));
    }

    [Fact]
    public void SelectVariant_NeverResolvesToTheUnrolledDefaultForAFieldedBaseline()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.NotEqual(
                ArmorVisualCatalog.Default.Catalog.Id,
                ArmorVisualCatalog.SelectVariant(entityId, ArmorId.QuiltedCotton).Catalog.Id);
        }
    }

    [Fact]
    public void Default_CarriesThePresentationOnlyEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.PresentationOnly, ArmorVisualCatalog.Default.Catalog.EvidenceTier);
    }

    /// <summary>
    /// Only <em>baluti</em> may appear as a Filipino armor term in a
    /// player-facing label.
    /// </summary>
    /// <remarks>
    /// CLAUDE.md section 7 permits a cultural identification in player-facing
    /// UI only in the paired form and only where an evidence tier is recorded,
    /// and it excludes outright any name whose earliest attestation postdates
    /// the depicted period by more than a century. Per
    /// docs/research/HISTORICAL_1500s_ARMOR.md section 7, every Filipino
    /// body-armor term except <em>baluti</em> came back OPEN or EXCLUDE:
    /// <em>barote</em>, <em>pakil</em>, <em>batung-batung</em> and
    /// <em>habay-habay</em> rest on a single modern authority, and
    /// <em>kurab-a-kulang</em> names objects from three centuries after the
    /// period. This test exists so that adding one of them to a label fails
    /// the build rather than shipping an unattested cultural claim.
    /// </remarks>
    [Fact]
    public void NoVariantLabelCarriesAnUnattestedFilipinoArmorTerm()
    {
        string[] unattestedTerms =
        [
            "barote",
            "pakil",
            "batung-batung",
            "batungbatung",
            "habay-habay",
            "habayhabay",
            "kurab-a-kulang",
            "kurab",
            "tamin",
            "taming",
            "palisay",
            "carasas",
        ];

        foreach (var entry in AllVariants())
        {
            foreach (var term in unattestedTerms)
            {
                Assert.DoesNotContain(
                    term,
                    entry.Catalog.DisplayLabel,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Exactly one variant label carries a Filipino name, and it is the
    /// quilted-armor row. Pigafetta's own 1521 Visayan vocabulary glosses
    /// <em>baluti</em> as quilted garments used for fighting, which is the
    /// only body-armor attestation in the record strong enough to clear the
    /// paired-label bar.
    /// </summary>
    [Fact]
    public void ExactlyOneVariantLabelCarriesTheOnlyAttestedFilipinoTerm()
    {
        var balutiLabels = AllVariants()
            .Where(entry => entry.Catalog.DisplayLabel.Contains(
                "Baluti",
                StringComparison.Ordinal))
            .ToArray();

        var single = Assert.Single(balutiLabels);
        Assert.Equal("Baluti — Quilted Armor", single.Catalog.DisplayLabel);
        Assert.Equal(ArmorId.QuiltedCotton, single.Baseline);
        Assert.Equal(VisualEvidenceTier.Documented, single.Catalog.EvidenceTier);
    }
}
