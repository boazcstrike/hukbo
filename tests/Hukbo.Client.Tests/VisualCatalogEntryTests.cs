using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the shared catalog entry shape (visual-system-integration-design.md
/// section 2) and its identifier grammar (RF-05 resolution): every ID any
/// sibling design table ships must match, every malformed shape must not,
/// and the seven mandatory fields must be validated at construction.
/// </summary>
public sealed class VisualCatalogEntryTests
{
    // --- Grammar acceptance: every ID the shipped sibling design tables mint ---

    [Theory]
    [InlineData("weapon.kampilan.k1")]
    [InlineData("weapon.kampilan.k2")]
    [InlineData("weapon.kampilan.tint.freshIron")]
    [InlineData("weapon.kampilan.tint.wornIron")]
    [InlineData("weapon.kampilan.tint.ochreHilt")]
    [InlineData("weapon.wasay.w1")]
    [InlineData("weapon.wasay.tint.ochreHaft")]
    [InlineData("weapon.wasay.tint.charredHaft")]
    [InlineData("weapon.wasay.tint.lashedWorn")]
    [InlineData("weapon.kalis.l1")]
    [InlineData("weapon.kalis.l2")]
    [InlineData("weapon.kalis.l3")]
    [InlineData("weapon.kalis.tint.freshIron")]
    [InlineData("weapon.kalis.tint.darkHilt")]
    [InlineData("weapon.itak.i1")]
    [InlineData("weapon.itak.tint.plainOchre")]
    [InlineData("weapon.itak.tint.wornField")]
    [InlineData("shield.tallHardwood.mactanThin")]
    [InlineData("shield.tallHardwood.morgaFullBody")]
    [InlineData("shield.tallHardwood.boxerCagayan")]
    [InlineData("shield.tallHardwood.visayanKalasag")]
    [InlineData("shield.tallHardwood.default")]
    [InlineData("appearance.headcovering.putong")]
    [InlineData("backdrop.grass.cluster")]
    public void IsWellFormedId_AcceptsEveryShippedTableIdentifier(string id)
    {
        Assert.True(VisualCatalogGrammar.IsWellFormedId(id));
    }

    // --- Grammar rejection: malformed shapes must not slip through ---

    [Theory]
    [InlineData("")]
    [InlineData("weapon")]
    [InlineData("weapon.kampilan")]
    [InlineData("Weapon.kampilan.k1")]
    [InlineData("sword.kampilan.k1")]
    [InlineData("weapon.Kampilan.k1")]
    [InlineData("weapon.kampilan.K1")]
    [InlineData("weapon..k1")]
    [InlineData("weapon.kampilan.")]
    [InlineData("weapon.kampilan.k1.extra")]
    [InlineData("weapon.kampilan.color.freshIron")]
    [InlineData("weapon.kampilan.tint.")]
    [InlineData("weapon.kampilan.k-1")]
    [InlineData("weapon.kampilan.1k")]
    [InlineData("weapon.kampilan.k1 ")]
    [InlineData(" weapon.kampilan.k1")]
    public void IsWellFormedId_RejectsMalformedIdentifiers(string id)
    {
        Assert.False(VisualCatalogGrammar.IsWellFormedId(id));
    }

    [Fact]
    public void IsWellFormedId_RejectsNull()
    {
        Assert.False(VisualCatalogGrammar.IsWellFormedId(null!));
    }

    // --- Construction-time validation ---

    // `with` expressions clone through the compiler-generated copy
    // constructor and set init accessors directly — they never re-run the
    // primary constructor's validation body. Every negative case below must
    // therefore go through `new VisualCatalogEntry(...)` directly, not
    // `with`, or the assertion would trivially pass without exercising the
    // guard it claims to test.
    private static VisualCatalogEntry Create(
        string id = "weapon.kalis.l1",
        int index = 0,
        string displayLabel = "Kalis — Thrusting Blade",
        VisualEvidenceTier evidenceTier = VisualEvidenceTier.Documented,
        VisualScopeTag scopeTag = VisualScopeTag.NotApplicable,
        string notes = "Cebu — 1521",
        VisualDetailTier minimumDetailTier = VisualDetailTier.Low) =>
        new(id, index, displayLabel, evidenceTier, scopeTag, notes, minimumDetailTier);

    [Fact]
    public void Constructor_AcceptsAWellFormedEntry()
    {
        var entry = Create();

        Assert.Equal("weapon.kalis.l1", entry.Id);
        Assert.Equal(0, entry.Index);
        Assert.Equal(VisualDetailTier.Low, entry.MinimumDetailTier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a valid id")]
    [InlineData("weapon.kampilan")]
    public void Constructor_RejectsAMalformedId(string id)
    {
        Assert.Throws<ArgumentException>(() => Create(id: id));
    }

    [Fact]
    public void Constructor_RejectsANegativeIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(index: -1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAnEmptyDisplayLabel(string label)
    {
        Assert.Throws<ArgumentException>(() => Create(displayLabel: label));
    }

    [Fact]
    public void Constructor_RejectsNullNotes()
    {
        Assert.Throws<ArgumentNullException>(() => Create(notes: null!));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedEvidenceTier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(evidenceTier: (VisualEvidenceTier)99));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedScopeTag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(scopeTag: (VisualScopeTag)99));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedMinimumDetailTier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(minimumDetailTier: (VisualDetailTier)99));
    }

    // --- The evidence-tier extension must not touch WeaponEvidenceTier ---

    [Fact]
    public void WeaponEvidenceTierStillHasExactlyThreeMembersAtTheirOriginalValues()
    {
        Assert.Equal(0, (int)WeaponEvidenceTier.Documented);
        Assert.Equal(1, (int)WeaponEvidenceTier.DocumentedFormUncertain);
        Assert.Equal(2, (int)WeaponEvidenceTier.ProvisionalReconstruction);
        Assert.Equal(3, Enum.GetValues<WeaponEvidenceTier>().Length);
    }

    [Fact]
    public void VisualEvidenceTierMirrorsWeaponEvidenceTierThenAddsPresentationOnly()
    {
        Assert.Equal(0, (int)VisualEvidenceTier.Documented);
        Assert.Equal(1, (int)VisualEvidenceTier.DocumentedFormUncertain);
        Assert.Equal(2, (int)VisualEvidenceTier.ProvisionalReconstruction);
        Assert.Equal(3, (int)VisualEvidenceTier.PresentationOnly);
        Assert.Equal(4, Enum.GetValues<VisualEvidenceTier>().Length);
    }
}
