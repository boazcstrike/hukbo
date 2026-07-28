using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="RankLabelCatalog"/> against
/// <c>docs/research/HISTORICAL_1500s_RANKS.md</c>'s "Terms cleared for use"
/// table: one entry per declared <see cref="RankId"/> value, every label in
/// pair form (never a bare Filipino name), and every label's text matching
/// the research document's table exactly (CLAUDE.md section 7).
/// </summary>
public sealed class RankLabelCatalogTests
{
    // The research document's own "Terms cleared for use, with tiers" table
    // (docs/research/HISTORICAL_1500s_RANKS.md), reproduced here as the
    // expectation this catalog is pinned against. Namamahay's pair-form label
    // deliberately shortens Plasencia's full "aliping namamahay" to the
    // single distinguishing word, exactly as the table does; the RankId
    // member behind it is still spelled AlipingNamamahay, because the enum
    // member is content-hash identity and the label is display text.
    [Theory]
    [InlineData(RankId.Datu, "Datu — Chief", "Tagalog and Visayan", VisualEvidenceTier.Documented)]
    [InlineData(RankId.Maharlika, "Maharlika — Sworn Freeman", "Tagalog", VisualEvidenceTier.Documented)]
    [InlineData(RankId.Timawa, "Timawa — Bound Freeman", "Visayan", VisualEvidenceTier.Documented)]
    [InlineData(RankId.AlipingNamamahay, "Aliping Namamahay — Householder", "Tagalog", VisualEvidenceTier.Documented)]
    [InlineData(RankId.Ayuey, "Ayuey — Household Dependent", "Visayan", VisualEvidenceTier.DocumentedFormUncertain)]
    public void Get_MatchesTheResearchDocumentsClearedTermsTableExactly(
        RankId rank,
        string expectedLabel,
        string expectedRegion,
        VisualEvidenceTier expectedTier)
    {
        var entry = RankLabelCatalog.Get(rank);

        Assert.Equal(expectedLabel, entry.Label);
        Assert.Equal(expectedRegion, entry.Region);
        Assert.Equal(expectedTier, entry.EvidenceTier);
    }

    [Fact]
    public void Entries_HasExactlyOneEntryPerDeclaredRankIdValue()
    {
        var declaredRanks = Enum.GetValues<RankId>();

        Assert.Equal(declaredRanks.Length, RankLabelCatalog.Entries.Count);

        foreach (var rank in declaredRanks)
        {
            Assert.Contains(
                RankLabelCatalog.Entries,
                entry => entry.Rank == rank);
        }
    }

    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.AlipingNamamahay)]
    [InlineData(RankId.Ayuey)]
    public void Get_ReturnsAnEntryWhoseRankMatchesTheRequestedValue(RankId rank)
    {
        Assert.Equal(rank, RankLabelCatalog.Get(rank).Rank);
    }

    [Fact]
    public void EveryLabel_IsPairFormNeverABareFilipinoName()
    {
        foreach (var entry in RankLabelCatalog.Entries)
        {
            var parts = entry.Label.Split(" — ");

            Assert.True(
                parts.Length == 2,
                $"'{entry.Label}' is not \"Filipino name — plain English " +
                "descriptor\" pair form.");
            Assert.False(string.IsNullOrWhiteSpace(parts[0]));
            Assert.False(string.IsNullOrWhiteSpace(parts[1]));
        }
    }

    [Fact]
    public void EveryEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        foreach (var entry in RankLabelCatalog.Entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
            Assert.False(string.IsNullOrWhiteSpace(entry.Region));
        }
    }

    [Fact]
    public void OnlyAlipingNamamahay_CarriesTheReconstructionNote()
    {
        foreach (var entry in RankLabelCatalog.Entries)
        {
            if (entry.Rank == RankId.AlipingNamamahay)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.ReconstructionNote));
            }
            else
            {
                Assert.Null(entry.ReconstructionNote);
            }
        }
    }

    [Fact]
    public void AlipingNamamahayReconstructionNote_DisclosesTheBattleLineInference()
    {
        // docs/research/HISTORICAL_1500s_RANKS.md's "Gaps and unknowns":
        // fielding this class means the inspector must state that a
        // household dependent in a battle line is an inference, not an
        // attested fact.
        var note = RankLabelCatalog.Get(RankId.AlipingNamamahay).ReconstructionNote;

        Assert.NotNull(note);
        Assert.Contains("inference", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Get_ThrowsForAnUndeclaredRankValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RankLabelCatalog.Get((RankId)0));
    }
}
