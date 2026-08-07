using Sandata.Core.Determinism;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 66 of docs/plans/2026-08-07-sandata-scaffold.md: pins
/// <see cref="SandataSystemTag"/>'s membership and every member's numeric
/// value. This is the declaration design section 4 named but never assigned
/// to a task — see the plan's "A fourth unowned declaration: the RNG system
/// tags" section.
/// </summary>
public sealed class SandataSystemTagTests
{
    /// <summary>
    /// Design section 4's "ordering and randomness" paragraph names exactly
    /// four v0.1 system tags. This proves the enum declares exactly those
    /// four members and no others, independent of their numeric values.
    /// </summary>
    [Fact]
    public void DeclaresExactlyTheFourTagsDesignSectionFourNames()
    {
        var members = Enum.GetNames<SandataSystemTag>();

        Assert.Equal(
            new[] { "Accuracy", "Reaction", "Sidestep", "SpawnJitter" },
            members);
    }

    /// <summary>
    /// Every numeric value is part of the replay contract per
    /// <see cref="SandataSystemTag"/>'s own doc comment: changing one re-keys
    /// every draw that system makes. Pinned as a literal, one case per
    /// member, so an accidental renumbering fails loudly rather than
    /// silently invalidating every recorded golden expectation.
    /// </summary>
    [Fact]
    public void NumericValuesArePinned()
    {
        Assert.Equal(0, (int)SandataSystemTag.Accuracy);
        Assert.Equal(1, (int)SandataSystemTag.Reaction);
        Assert.Equal(2, (int)SandataSystemTag.Sidestep);
        Assert.Equal(3, (int)SandataSystemTag.SpawnJitter);
    }
}
