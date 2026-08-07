using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the minimal <c>backdrop.*</c> catalog declaration (battlefield-
/// environment-design.md; RF-10 resolution of R-W4.9's labelling
/// obligation): one entry, a well-formed identifier, the provisional
/// evidence tier the ground's technique-only claim carries, and no
/// cultural scope.
/// </summary>
public sealed class BackdropVisualCatalogTests
{
    [Fact]
    public void GrassCluster_HasThePinnedIdentifier()
    {
        Assert.Equal("backdrop.grass.cluster", BackdropVisualCatalog.GrassCluster.Id);
    }

    [Fact]
    public void GrassCluster_IdIsWellFormedByTheSharedGrammar()
    {
        Assert.True(VisualCatalogGrammar.IsWellFormedId(BackdropVisualCatalog.GrassCluster.Id));
    }

    [Fact]
    public void GrassCluster_CarriesTheProvisionalReconstructionEvidenceTier()
    {
        Assert.Equal(
            VisualEvidenceTier.ProvisionalReconstruction,
            BackdropVisualCatalog.GrassCluster.EvidenceTier);
    }

    [Fact]
    public void GrassCluster_MakesNoCulturalScopeClaim()
    {
        Assert.Equal(VisualScopeTag.NotApplicable, BackdropVisualCatalog.GrassCluster.ScopeTag);
    }

    [Fact]
    public void GrassCluster_DisplayLabelNamesNoVegetationRegionOrLandUse()
    {
        // R-W4.9: the ground depicts generic open ground; no player-facing
        // text may name a specific vegetation, region, or land use. This is
        // a targeted denylist on the words the label must never contain,
        // not a claim of exhaustive coverage.
        string[] forbiddenTerms =
        [
            "grass", "cogon", "Philippine", "Visayan", "Tagalog", "Cagayan",
            "rice", "paddy", "terrace", "jungle", "lowland",
        ];

        foreach (var term in forbiddenTerms)
        {
            Assert.DoesNotContain(
                term,
                BackdropVisualCatalog.GrassCluster.DisplayLabel,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void All_ContainsExactlyTheGrassClusterEntry()
    {
        Assert.Equal([BackdropVisualCatalog.GrassCluster], BackdropVisualCatalog.All);
    }
}
