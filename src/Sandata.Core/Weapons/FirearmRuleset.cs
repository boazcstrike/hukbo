using Sandata.Core.Determinism;

namespace Sandata.Core.Weapons;

/// <summary>
/// Folds <see cref="FirearmCatalog.Rows"/> into a single content hash, the
/// same shape <c>SandataRuleset.ContentHash</c> (Rules/SandataRuleset.cs)
/// already establishes for the ruleset shell. Every field of every row folds
/// in, in <see cref="FirearmDefinition"/>'s declaration order, row by row in
/// <see cref="FirearmId"/> order — so changing a single weapon's aim time,
/// adding a weapon, or reordering the roster all move
/// <see cref="ContentHash"/>, matching the replay-contract rule
/// <c>CLAUDE.md</c> section 5 and <see cref="FirearmId"/>'s own remarks state.
/// </summary>
public sealed class FirearmRuleset
{
    /// <summary>
    /// The one instance v0.1 ships, over <see cref="FirearmCatalog.Rows"/>.
    /// <c>FirearmCatalogTests</c> pins its <see cref="ContentHash"/> to a
    /// recorded value.
    /// </summary>
    public static FirearmRuleset ModernTacticalV1 { get; } = new(FirearmCatalog.Rows);

    /// <summary>
    /// Builds a ruleset over an arbitrary row list. Exposed as public, rather
    /// than hidden behind only the <see cref="ModernTacticalV1"/> singleton,
    /// so a test can fold a deliberately mutated copy of the catalog and show
    /// that exactly one changed field moves <see cref="ContentHash"/>.
    /// </summary>
    public FirearmRuleset(IReadOnlyList<FirearmDefinition> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        Rows = rows;
        ContentHash = ComputeContentHash(rows);
    }

    /// <summary>The row list this ruleset was built over.</summary>
    public IReadOnlyList<FirearmDefinition> Rows { get; }

    /// <summary>
    /// FNV-1a over every field of every row in <see cref="Rows"/>, folded
    /// through <see cref="SandataHash"/> in <see cref="FirearmDefinition"/>'s
    /// fixed declaration order. Reordering rows, reordering fields, adding a
    /// field, or removing one is a new preset version with new golden
    /// expectations.
    /// </summary>
    public ulong ContentHash { get; }

    private static ulong ComputeContentHash(IReadOnlyList<FirearmDefinition> rows)
    {
        var hash = SandataHash.Begin();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            SandataHash.Fold(ref hash, (long)row.Id);
            SandataHash.Fold(ref hash, (long)row.Class);
            SandataHash.Fold(ref hash, (long)row.Caliber);
            SandataHash.Fold(ref hash, (long)row.Mechanism);
            SandataHash.Fold(ref hash, (long)row.Modes);
            SandataHash.Fold(ref hash, row.ReadyMs);
            SandataHash.Fold(ref hash, row.AimBaseMs);
            SandataHash.Fold(ref hash, row.AimPerBamMs);
            SandataHash.Fold(ref hash, row.ResetMs);
            SandataHash.Fold(ref hash, row.TurnBamPerTick);
            SandataHash.Fold(ref hash, row.AutoBandMaxWu);
            SandataHash.Fold(ref hash, row.BurstBandMaxWu);
            SandataHash.Fold(ref hash, row.SingleBandMaxWu);
            SandataHash.Fold(ref hash, row.DispersionAtZeroWu);
            SandataHash.Fold(ref hash, row.DispersionAtMaxWu);
            SandataHash.Fold(ref hash, row.MaxEffectiveWu);
            SandataHash.Fold(ref hash, row.MagazineCapacity);
            SandataHash.Fold(ref hash, row.ReloadMs);
            SandataHash.Fold(ref hash, row.CyclicRpm);
            SandataHash.Fold(ref hash, row.ExemptFromLoweredRule);
        }

        return hash;
    }
}
