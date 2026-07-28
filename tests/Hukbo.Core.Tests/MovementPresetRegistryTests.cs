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
    /// Pinned by this task against the frozen preset's constant set. Per the
    /// freeze recorded in docs/plans/2026-07-28-formation-movement-realism-design.md
    /// section 6.2, this literal never changes again: a later task that adds
    /// a field to <see cref="MovementRuleset"/> would move it, which is
    /// exactly what that freeze forbids for <c>IndependentPursuitV1</c>.
    /// </summary>
    private const ulong IndependentPursuitV1ContentHash = 0x97EC406EB79F61FAUL;

    /// <summary>
    /// Pinned by T9 against <c>PersistentContingentsV2</c>'s constant set,
    /// which is identical to <c>IndependentPursuitV1</c>'s field-for-field —
    /// only <see cref="Movement.MovementRuleset.Id"/> differs — so this
    /// literal differs from <see cref="IndependentPursuitV1ContentHash"/>
    /// only through the folded <c>Id</c> field.
    /// </summary>
    private const ulong PersistentContingentsV2ContentHash = 0xE5AC42AA7FC19301UL;

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
}
