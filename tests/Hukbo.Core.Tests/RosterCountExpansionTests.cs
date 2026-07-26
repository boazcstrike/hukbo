using System.Collections.Immutable;
using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

public sealed class RosterCountExpansionTests
{
    [Fact]
    public void ExpandsCountsInDeclaredRosterIndexOrder()
    {
        var counts = ImmutableArray.Create(2, 1, 3, 0);

        var expanded = RosterCountExpansion.Expand(counts);

        Assert.Equal(new[] { 0, 0, 1, 2, 2, 2 }, expanded.ToArray());
    }

    [Fact]
    public void ProducesOneEntryPerWarriorForTheGivenTotal()
    {
        var counts = ImmutableArray.Create(5, 10, 3, 7);

        var expanded = RosterCountExpansion.Expand(counts);

        Assert.Equal(25, expanded.Length);
    }

    [Fact]
    public void RejectsNegativeCounts()
    {
        var counts = ImmutableArray.Create(1, -1, 2, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RosterCountExpansion.Expand(counts));
    }

    [Fact]
    public void RejectsALocalIndexBeyondTheExpandedLength()
    {
        var counts = ImmutableArray.Create(1, 1, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RosterCountExpansion.ResolveRosterIndex(counts, 2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RosterCountExpansion.ResolveRosterIndex(counts, -1));
    }
}
