using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// The corpus half of the warrior personal-name work: these assertions are
/// what keep docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md's exclusions from
/// being decorative. A form that the research reserves — a famous bearer, a
/// parenthood structure, a title, a place, a later-source name, a Christian
/// baptismal name — fails the build if it ever reaches a selectable pool.
/// </summary>
public sealed class WarriorNameCatalogTests
{
    // WarriorNameRegion is internal, and a public test method's parameter may
    // never be less accessible than the method itself (CS0051) while xunit's
    // own analyzer (xUnit1000) requires the test class to stay public — the
    // discipline AgentInspectorContentTests already records for the internal
    // shield and appearance types. Every per-region assertion below therefore
    // loops over the regions inside one [Fact] rather than taking one as a
    // [Theory] parameter.

    [Fact]
    public void EveryRegionHasANonEmptyPool()
    {
        foreach (var region in Enum.GetValues<WarriorNameRegion>())
        {
            Assert.NotEmpty(WarriorNameCatalog.GetPool(region));
        }
    }

    [Fact]
    public void EveryPoolIndexesContiguouslyFromZero()
    {
        foreach (var region in Enum.GetValues<WarriorNameRegion>())
        {
            var pool = WarriorNameCatalog.GetPool(region);

            Assert.Equal(
                Enumerable.Range(0, pool.Count),
                pool.Select(entry => entry.Index));
        }
    }

    [Fact]
    public void EveryEntryDeclaresItsOwnRegion()
    {
        foreach (var region in Enum.GetValues<WarriorNameRegion>())
        {
            Assert.All(
                WarriorNameCatalog.GetPool(region),
                entry => Assert.Equal(region, entry.Region));
        }
    }

    [Fact]
    public void EveryIdentifierIsUnique()
    {
        var ids = WarriorNameCatalog.All.Select(entry => entry.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryDisplayFormIsUnique()
    {
        var forms = WarriorNameCatalog.All
            .Select(entry => entry.DisplayForm)
            .ToArray();

        Assert.Equal(
            forms.Length,
            forms.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllListsEveryRegionsPoolExactlyOnce()
    {
        var expected = Enum.GetValues<WarriorNameRegion>()
            .Sum(region => WarriorNameCatalog.GetPool(region).Count);

        Assert.Equal(expected, WarriorNameCatalog.All.Count);
    }

    [Fact]
    public void EveryEntryCarriesASourceCitationAndAReuseNote()
    {
        Assert.All(
            WarriorNameCatalog.All,
            entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.SourceCitation));
                Assert.False(string.IsNullOrWhiteSpace(entry.ReuseNote));
                Assert.False(string.IsNullOrWhiteSpace(entry.RecordedForm));
            });
    }

    /// <summary>
    /// No shipped form claims to be presentation-only: unlike a weapon tint,
    /// a personal name always makes a historical claim of some strength, so
    /// every entry must sit on one of the three real evidence tiers.
    /// </summary>
    [Fact]
    public void NoEntryIsLabelledPresentationOnly()
    {
        Assert.All(
            WarriorNameCatalog.All,
            entry => Assert.NotEqual(
                VisualEvidenceTier.PresentationOnly,
                entry.EvidenceTier));
    }

    /// <summary>
    /// Chirino's 1604 material is the only naming-example source in the
    /// catalog, and the research clears it for a 1500s roster only as a
    /// Provisional reconstruction. A future edit that promotes one of those
    /// forms to Documented fails here.
    /// </summary>
    [Fact]
    public void EveryNamingExampleIsAProvisionalReconstruction()
    {
        Assert.All(
            WarriorNameCatalog.All.Where(
                entry => entry.Kind == WarriorNameKind.NamingExample),
            entry => Assert.Equal(
                VisualEvidenceTier.ProvisionalReconstruction,
                entry.EvidenceTier));
    }

    /// <summary>
    /// Famous historical bearers stay reference-only (research section 3.4,
    /// rule 7) so a roster never reads as a bag of copies of the same handful
    /// of figures.
    /// </summary>
    [Theory]
    [InlineData("Lapulapu")]
    [InlineData("Cilapulapu")]
    [InlineData("Kalipulako")]
    [InlineData("Humabon")]
    [InlineData("Zula")]
    [InlineData("Colambu")]
    [InlineData("Tupas")]
    [InlineData("Sikatuna")]
    [InlineData("Cicatuna")]
    [InlineData("Soliman")]
    [InlineData("Salamat")]
    [InlineData("Limasancay")]
    public void NoFamousBearerIsSelectable(string reservedForm)
    {
        AssertFormIsAbsent(reservedForm);
    }

    /// <summary>
    /// A parenthood name refers to a specific firstborn, so it may only be
    /// generated when that child exists (rule 3). A battle roster has no
    /// family tree, so these three recorded forms stay out of the pools.
    /// </summary>
    [Theory]
    [InlineData("Amanicalao")]
    [InlineData("Amarlangagui")]
    [InlineData("Amaghicon")]
    public void NoParenthoodFormIsSelectable(string parenthoodForm)
    {
        AssertFormIsAbsent(parenthoodForm);
    }

    /// <summary>
    /// Titles encode standing and are never handed to an ordinary warrior for
    /// flavour (rule 2).
    /// </summary>
    [Theory]
    [InlineData("Dato")]
    [InlineData("Datu")]
    [InlineData("Raia")]
    [InlineData("Raja")]
    [InlineData("Raxa")]
    [InlineData("Gat")]
    [InlineData("Lacan")]
    [InlineData("Lakan")]
    [InlineData("Dayang")]
    [InlineData("Magat")]
    public void NoTitleIsSelectableAsAName(string title)
    {
        AssertFormIsAbsent(title);
    }

    /// <summary>
    /// Settlements and places that appear beside chiefs in the same passage
    /// must not leak into a personal-name pool (research section 5.1).
    /// </summary>
    [Theory]
    [InlineData("Cinghapola")]
    [InlineData("Mandaui")]
    [InlineData("Lalan")]
    [InlineData("Lalutan")]
    [InlineData("Matan")]
    [InlineData("Bulaia")]
    [InlineData("Cilumai")]
    [InlineData("Lubucun")]
    [InlineData("Quipit")]
    [InlineData("Mazaua")]
    public void NoPlaceNameIsSelectable(string placeName)
    {
        AssertFormIsAbsent(placeName);
    }

    /// <summary>
    /// Colin's 1663 reputation and friendship material is later comparison
    /// only and is excluded from a historically labelled 1500s roster
    /// (rule 4).
    /// </summary>
    [Theory]
    [InlineData("Bacal")]
    [InlineData("Bayani")]
    [InlineData("Dimatanassan")]
    [InlineData("Dimalapitan")]
    [InlineData("Casolasi")]
    [InlineData("Caytlog")]
    [InlineData("Mati")]
    [InlineData("Sanguy")]
    [InlineData("Damo")]
    public void NoLaterSeventeenthCenturyFormIsSelectable(string laterForm)
    {
        AssertFormIsAbsent(laterForm);
    }

    /// <summary>
    /// Christian baptismal names belong to a dated contact context (rule 5),
    /// which no scenario declares yet, so neither the assigned names nor the
    /// Spanish first names printed beside the Tondo elements are generated.
    /// </summary>
    [Theory]
    [InlineData("Johanna")]
    [InlineData("Catherina")]
    [InlineData("Lisabeta")]
    [InlineData("Isabel")]
    [InlineData("Agustin")]
    [InlineData("Phelipe")]
    [InlineData("Joan")]
    [InlineData("Antonio")]
    [InlineData("Geronimo")]
    [InlineData("Luis")]
    [InlineData("Dionisio")]
    [InlineData("Ignacio")]
    public void NoChristianBaptismalNameIsSelectable(string baptismalName)
    {
        AssertFormIsAbsent(baptismalName);
    }

    /// <summary>
    /// Names the research explicitly refuses to clear — the fraudulent
    /// Kalantiaw tradition, the 1907 <i>Maragtas</i> cast, the disputed
    /// Urduja identification, and the folkloric Humamay — must never appear
    /// however widely they circulate (research section 9).
    /// </summary>
    [Theory]
    [InlineData("Kalantiaw")]
    [InlineData("Puti")]
    [InlineData("Sumakwel")]
    [InlineData("Marikudo")]
    [InlineData("Kapinangan")]
    [InlineData("Paiburong")]
    [InlineData("Bangkaya")]
    [InlineData("Urduja")]
    [InlineData("Humamay")]
    public void NoUnclearedTraditionIsSelectable(string unclearedForm)
    {
        AssertFormIsAbsent(unclearedForm);
    }

    /// <summary>
    /// The one woman's naming example the opened sources supply is present,
    /// and the research's own warning against manufacturing more by suffixing
    /// men's names holds: no other form ends in the same way.
    /// </summary>
    [Fact]
    public void TheOneRecordedWomansExampleIsPresentAndNotGeneralized()
    {
        var womensForms = WarriorNameCatalog.All
            .Where(entry =>
                entry.RecordedGender == WarriorNameGenderEvidence.RecordedWoman)
            .Select(entry => entry.DisplayForm)
            .ToArray();

        Assert.Equal(["Iloguin"], womensForms);
        Assert.Single(
            WarriorNameCatalog.All,
            entry => entry.DisplayForm.EndsWith("guin", StringComparison.Ordinal));
    }

    /// <summary>
    /// The two standalone research notes the inspector always appends are
    /// real text, not placeholders, because they are the only channel through
    /// which a spectator learns what the catalog deliberately does not
    /// generate.
    /// </summary>
    [Fact]
    public void BothStandaloneResearchNotesCarryText()
    {
        Assert.False(string.IsNullOrWhiteSpace(
            WarriorNameCatalog.ParenthoodResearchNote));
        Assert.False(string.IsNullOrWhiteSpace(
            WarriorNameCatalog.WomensNamesResearchNote));
    }

    [Fact]
    public void RegionAssignmentTableListsEveryRegionExactlyOnce()
    {
        Assert.Equal(
            Enum.GetValues<WarriorNameRegion>().Order(),
            WarriorNameCatalog.RegionAssignmentTable.Order());
    }

    [Fact]
    public void EveryRegionCarriesAPlainEnglishLabel()
    {
        foreach (var region in Enum.GetValues<WarriorNameRegion>())
        {
            Assert.False(string.IsNullOrWhiteSpace(
                WarriorNameCatalog.GetRegionLabel(region)));
        }
    }

    [Fact]
    public void RegionAssignmentIsStableForTheSameSeedAndFaction()
    {
        for (ulong seed = 1; seed <= 32; seed++)
        {
            for (var factionId = 0; factionId < 4; factionId++)
            {
                Assert.Equal(
                    WarriorNameCatalog.SelectRegion(seed, factionId),
                    WarriorNameCatalog.SelectRegion(seed, factionId));
            }
        }
    }

    /// <summary>
    /// Every region is reachable, so no pool is dead weight and no seed range
    /// silently collapses onto one dossier.
    /// </summary>
    [Fact]
    public void EveryRegionIsReachableAcrossSeeds()
    {
        var reached = new HashSet<WarriorNameRegion>();
        for (ulong seed = 1; seed <= 256; seed++)
        {
            reached.Add(WarriorNameCatalog.SelectRegion(seed, 0));
            reached.Add(WarriorNameCatalog.SelectRegion(seed, 1));
        }

        Assert.Equal(Enum.GetValues<WarriorNameRegion>().Length, reached.Count);
    }

    [Fact]
    public void SelectedNamesAlwaysComeFromTheAssignedRegionsPool()
    {
        foreach (var region in Enum.GetValues<WarriorNameRegion>())
        {
            var pool = WarriorNameCatalog.GetPool(region);

            for (ulong entityId = 0; entityId < 512; entityId++)
            {
                Assert.Contains(
                    WarriorNameCatalog.SelectName(entityId, region),
                    pool);
            }
        }
    }

    [Fact]
    public void EveryFormInAPoolIsReachable()
    {
        foreach (var region in Enum.GetValues<WarriorNameRegion>())
        {
            var reached = new HashSet<string>(StringComparer.Ordinal);
            for (ulong entityId = 0; entityId < 4096; entityId++)
            {
                reached.Add(WarriorNameCatalog.SelectName(entityId, region).Id);
            }

            Assert.Equal(
                WarriorNameCatalog.GetPool(region).Count,
                reached.Count);
        }
    }

    [Fact]
    public void IdentifiersRejectAMalformedGrammar()
    {
        Assert.Throws<ArgumentException>(() => new WarriorNameEntry(
            "tagalog.salonga",
            0,
            "Salonga",
            "Salonga",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            "source",
            "note"));
    }

    private static void AssertFormIsAbsent(string reservedForm)
    {
        Assert.DoesNotContain(
            WarriorNameCatalog.All,
            entry => string.Equals(
                entry.DisplayForm,
                reservedForm,
                StringComparison.OrdinalIgnoreCase));
    }
}
