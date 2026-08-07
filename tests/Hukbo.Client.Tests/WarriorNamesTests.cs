using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// The derivation half of the warrior personal-name work: a name is a pure
/// function of the entity identifier, the faction identifier, and the match
/// seed, so it is stable inside a match, identical across a replay of the same
/// seed, and incapable of reaching the simulation.
/// </summary>
public sealed class WarriorNamesTests
{
    [Fact]
    public void ResolveIsStableForTheSameWarriorAndSeed()
    {
        for (ulong entityId = 0; entityId < 128; entityId++)
        {
            var first = WarriorNames.Resolve(entityId, 0, 1);
            var second = WarriorNames.Resolve(entityId, 0, 1);

            Assert.Same(first, second);
        }
    }

    /// <summary>
    /// The seed reaches the name only through the faction's region
    /// assignment, so two seeds that assign a faction the same region name it
    /// the same way, and every seed keeps one faction's warriors inside one
    /// regional grammar.
    /// </summary>
    [Fact]
    public void EveryWarriorInOneFactionDrawsFromOneRegion()
    {
        for (ulong seed = 1; seed <= 16; seed++)
        {
            foreach (var factionId in new[] { 0, 1 })
            {
                var expected = WarriorNameCatalog.SelectRegion(seed, factionId);

                for (ulong entityId = 0; entityId < 200; entityId++)
                {
                    Assert.Equal(
                        expected,
                        WarriorNames.Resolve(entityId, factionId, seed).Region);
                }
            }
        }
    }

    /// <summary>
    /// Two warriors with different identifiers in the same faction are not
    /// forced apart — pools are smaller than a roster, so repeats are honest
    /// — but the stream does spread across the pool rather than collapsing
    /// onto one form.
    /// </summary>
    [Fact]
    public void OneFactionsRosterUsesMoreThanASingleName()
    {
        var forms = new HashSet<string>(StringComparer.Ordinal);
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            forms.Add(WarriorNames.Resolve(entityId, 0, 1).DisplayForm);
        }

        Assert.True(
            forms.Count >= 8,
            $"Expected a spread of names across a 200-warrior roster, saw {forms.Count}.");
    }

    [Fact]
    public void FormatWarriorShowsTheNameAndKeepsTheEntityIdentifier()
    {
        var name = WarriorNames.Resolve(42, 0, 1);

        Assert.Equal(
            $"{name.DisplayForm} #42",
            WarriorNames.FormatWarrior(42, 0, 1));
    }

    /// <summary>
    /// The name stream is its own, salted apart from the appearance streams:
    /// changing nothing but the faction can change the region, and therefore
    /// the name, without touching how the warrior looks.
    /// </summary>
    [Fact]
    public void ChangingOnlyTheFactionCanChangeTheAssignedRegion()
    {
        var differed = false;
        for (ulong seed = 1; seed <= 64 && !differed; seed++)
        {
            differed = WarriorNameCatalog.SelectRegion(seed, 0)
                != WarriorNameCatalog.SelectRegion(seed, 1);
        }

        Assert.True(
            differed,
            "No seed in 1..64 assigned the two factions different regions.");
    }
}
