using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests;

/// <summary>
/// Exercises <see cref="MovementPresetRegistry"/>'s exhaustive-switch shape:
/// the one registered value resolves and the pinned ruleset it resolves to
/// hashes to a literal this task records, while every unregistered value
/// fails loudly through both <see cref="MovementPresetRegistry.IsRegistered"/>
/// and <see cref="MovementPresetRegistry.Get"/> rather than falling back to a
/// default.
/// </summary>
public sealed class MovementPresetRegistryTests
{
    /// <summary>
    /// Pins <c>IndependentPursuitV1</c>'s <see cref="MovementRuleset.ContentHash"/>
    /// to its current field values. This is an identity assertion over the
    /// ruleset's own fields, not a behavioural golden — it does not reach
    /// the state hash, so it moves whenever a task adds a field to
    /// <see cref="MovementRuleset"/>, most recently the four
    /// equipment-relative footwork members (<c>UsesEquipmentRelativeFootwork</c>,
    /// the two context radii, and <c>LoadoutMovementProfiles</c>), and before
    /// that <c>SelectsLeaderByRank</c>, and before that
    /// <c>NarrowsCohesionScanToCohesionCapableContingents</c>, and before that
    /// T2's <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>.
    /// All five V1-through-V5 literals below were recomputed from the built
    /// code when the equipment-relative footwork members landed, never
    /// calculated by hand.
    /// What stays frozen across such a change is the preset's simulated
    /// behaviour, proved instead by
    /// <c>IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest</c>.
    /// </summary>
    private const ulong IndependentPursuitV1ContentHash = 0x36BAA7847258E4E3UL;

    /// <summary>
    /// Pinned by T9 against <c>PersistentContingentsV2</c>'s constant set,
    /// which is identical to <c>IndependentPursuitV1</c>'s field-for-field —
    /// only <see cref="Movement.MovementRuleset.Id"/> differs — so this
    /// literal differs from <see cref="IndependentPursuitV1ContentHash"/>
    /// only through the folded <c>Id</c> field.
    /// </summary>
    private const ulong PersistentContingentsV2ContentHash = 0x0E37E2FD11051440UL;

    /// <summary>
    /// Pinned by T5 against <c>PersistentContingentsV3</c>'s constant set,
    /// which differs from <c>PersistentContingentsV2</c>'s only in
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>
    /// (<c>1, 2</c> instead of <c>0, 1</c>), so this literal differs from
    /// both existing literals.
    /// </summary>
    private const ulong PersistentContingentsV3ContentHash = 0x47E9B3788ACE6783UL;

    /// <summary>
    /// Pinned against <c>PersistentContingentsV4</c>'s constant set, which
    /// differs from <c>PersistentContingentsV3</c>'s only in
    /// <c>NarrowsCohesionScanToCohesionCapableContingents</c>
    /// (<see langword="true"/> instead of <see langword="false"/>), so this
    /// literal differs from all three existing literals.
    /// </summary>
    private const ulong PersistentContingentsV4ContentHash = 0x26302C0CF6885235UL;

    /// <summary>
    /// Pinned against <c>PersistentContingentsV5</c>'s constant set, which
    /// differs from <c>PersistentContingentsV4</c>'s only in
    /// <c>SelectsLeaderByRank</c> (<see langword="true"/> instead of
    /// <see langword="false"/>), so this literal differs from all four
    /// existing literals.
    /// </summary>
    private const ulong PersistentContingentsV5ContentHash = 0x84FA62B037FAC275UL;

    /// <summary>
    /// Pinned against <c>EquipmentRelativeFootworkV6</c>'s constant set,
    /// which differs from <c>PersistentContingentsV5</c>'s in
    /// <c>UsesEquipmentRelativeFootwork</c> (<see langword="true"/>), both
    /// context radii, and the six loadout movement profile rows folded per
    /// design section 5.1, so this literal differs from all five existing
    /// literals.
    /// </summary>
    private const ulong EquipmentRelativeFootworkV6ContentHash = 0x0FFE5D202B324D25UL;

    [Fact]
    public void IndependentPursuitV1IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.IndependentPursuitV1));
    }

    [Fact]
    public void PersistentContingentsV2IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV2));
    }

    [Fact]
    public void PersistentContingentsV3IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV3));
    }

    [Fact]
    public void PersistentContingentsV4IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV4));
    }

    [Fact]
    public void PersistentContingentsV5IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV5));
    }

    [Fact]
    public void EquipmentRelativeFootworkV6IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.EquipmentRelativeFootworkV6));
    }

    [Fact]
    public void TheZeroValueIsNotRegistered()
    {
        Assert.False(MovementPresetRegistry.IsRegistered((MovementPresetId)0));
    }

    [Fact]
    public void AnUnassignedHighValueIsNotRegistered()
    {
        Assert.False(MovementPresetRegistry.IsRegistered((MovementPresetId)99));
    }

    [Fact]
    public void GetThrowsForTheZeroValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovementPresetRegistry.Get((MovementPresetId)0));
    }

    [Fact]
    public void GetThrowsForAnUnassignedHighValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovementPresetRegistry.Get((MovementPresetId)99));
    }

    [Fact]
    public void IndependentPursuitV1ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.IndependentPursuitV1);

        Assert.Equal(IndependentPursuitV1ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV2</c>'s content hash to a literal
    /// distinct from <c>IndependentPursuitV1</c>'s, satisfying T9's own
    /// verification criterion: introducing the second preset must move
    /// nothing about the first.
    /// </summary>
    [Fact]
    public void PersistentContingentsV2ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV2);

        Assert.Equal(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV3</c>'s content hash to a literal
    /// distinct from both existing literals, satisfying T5's own
    /// verification criterion: introducing the third preset must move
    /// nothing about the first two.
    /// </summary>
    [Fact]
    public void PersistentContingentsV3ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV3);

        Assert.Equal(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV4</c>'s content hash to a literal
    /// distinct from all three existing literals: introducing the fourth
    /// preset must move nothing about the first three.
    /// </summary>
    [Fact]
    public void PersistentContingentsV4ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV4);

        Assert.Equal(PersistentContingentsV4ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV5</c>'s content hash to a literal
    /// distinct from all four existing literals: introducing the fifth
    /// preset must move nothing about the first four.
    /// </summary>
    [Fact]
    public void PersistentContingentsV5ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV5);

        Assert.Equal(PersistentContingentsV5ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV4ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>EquipmentRelativeFootworkV6</c>'s content hash to a literal
    /// distinct from all five existing literals: introducing the sixth
    /// preset must move nothing about the first five.
    /// </summary>
    [Fact]
    public void EquipmentRelativeFootworkV6ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

        Assert.Equal(EquipmentRelativeFootworkV6ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV4ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV5ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// The narrowing flag is what separates <c>PersistentContingentsV4</c>
    /// from <c>PersistentContingentsV3</c>, and no earlier preset carries it,
    /// so a preset added later cannot quietly turn it on for a frozen
    /// trajectory without this Fact failing. <c>PersistentContingentsV5</c>
    /// and <c>EquipmentRelativeFootworkV6</c> both carry the flag forward
    /// deliberately — V5 layers rank-aware leader selection on V4's
    /// narrowed scan, and V6 carries V5's cohesion tunables unchanged — and
    /// this Fact states both so neither value can drift silently.
    /// </summary>
    [Fact]
    public void OnlyPersistentContingentsV4NarrowsTheCrossContingentScan()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .NarrowsCohesionScanToCohesionCapableContingents);
    }

    /// <summary>
    /// The rank-selection flag is what separates
    /// <c>PersistentContingentsV5</c> from every earlier preset; no earlier
    /// preset carries it, so a preset added later cannot quietly turn it on
    /// for a frozen trajectory without this Fact failing.
    /// <c>EquipmentRelativeFootworkV6</c> carries the flag forward
    /// deliberately — it carries V5's cohesion tunables unchanged — and this
    /// Fact states that value so it cannot drift silently.
    /// </summary>
    [Fact]
    public void OnlyPersistentContingentsV5SelectsLeaderByRank()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .SelectsLeaderByRank);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .SelectsLeaderByRank);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .SelectsLeaderByRank);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .SelectsLeaderByRank);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .SelectsLeaderByRank);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .SelectsLeaderByRank);
    }

    /// <summary>
    /// The equipment-relative footwork flag is what separates
    /// <c>EquipmentRelativeFootworkV6</c> from every earlier preset; no
    /// earlier preset carries it, so a preset added later cannot quietly
    /// turn it on for a frozen trajectory without this Fact failing.
    /// </summary>
    [Fact]
    public void OnlyEquipmentRelativeFootworkV6UsesEquipmentRelativeFootwork()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .UsesEquipmentRelativeFootwork);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .UsesEquipmentRelativeFootwork);
    }
}
