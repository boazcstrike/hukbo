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
    /// <see cref="MovementRuleset"/>, most recently T2's
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>.
    /// What stays frozen across such a change is the preset's simulated
    /// behaviour, proved instead by
    /// <c>IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest</c>.
    /// </summary>
    private const ulong IndependentPursuitV1ContentHash = 0x937AB8F6DE2582A3UL;

    /// <summary>
    /// Pinned by T9 against <c>PersistentContingentsV2</c>'s constant set,
    /// which is identical to <c>IndependentPursuitV1</c>'s field-for-field —
    /// only <see cref="Movement.MovementRuleset.Id"/> differs — so this
    /// literal differs from <see cref="IndependentPursuitV1ContentHash"/>
    /// only through the folded <c>Id</c> field.
    /// </summary>
    private const ulong PersistentContingentsV2ContentHash = 0xE1AAE33EB35BE440UL;

    /// <summary>
    /// Pinned by T5 against <c>PersistentContingentsV3</c>'s constant set,
    /// which differs from <c>PersistentContingentsV2</c>'s only in
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>
    /// (<c>1, 2</c> instead of <c>0, 1</c>), so this literal differs from
    /// both existing literals.
    /// </summary>
    private const ulong PersistentContingentsV3ContentHash = 0x4605119141580D43UL;

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
}
