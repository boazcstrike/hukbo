using System.Collections.Immutable;

namespace Hukbo.Core.Combat;

/// <summary>
/// Pure run-length expansion of per-roster-entry warrior counts into an
/// ordered array of roster indices: roster index 0 repeated
/// <c>counts[0]</c> times, then roster index 1, and so on. Scenario data
/// only (see <see cref="Hukbo.Core.Simulation.Scenario.RosterCounts"/>); the
/// preset roster itself stays scenario-agnostic.
/// </summary>
public static class RosterCountExpansion
{
    /// <summary>
    /// Expands <paramref name="counts"/> into an array of roster indices,
    /// one entry per warrior, in declared roster-index order. Re-validates
    /// its own input: a negative count is rejected even if the caller
    /// already validated a <c>Scenario</c>.
    /// </summary>
    public static ImmutableArray<int> Expand(ImmutableArray<int> counts)
    {
        ValidateNonNegative(counts);

        var total = 0;
        foreach (var count in counts)
        {
            total = checked(total + count);
        }

        var builder = ImmutableArray.CreateBuilder<int>(total);
        for (var rosterIndex = 0; rosterIndex < counts.Length; rosterIndex++)
        {
            for (var repeat = 0; repeat < counts[rosterIndex]; repeat++)
            {
                builder.Add(rosterIndex);
            }
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Resolves the roster index for one faction-local warrior index.
    /// Re-validates <paramref name="counts"/> and <paramref name="localIndex"/>
    /// as a second public entry point a caller or test can reach directly,
    /// rather than trusting an already-expanded array. O(n), the same as
    /// <see cref="Expand"/>.
    /// </summary>
    public static int ResolveRosterIndex(ImmutableArray<int> counts, int localIndex)
    {
        var expanded = Expand(counts);

        if (localIndex < 0 || localIndex >= expanded.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localIndex),
                localIndex,
                $"Local index must be within [0, {expanded.Length}).");
        }

        return expanded[localIndex];
    }

    private static void ValidateNonNegative(ImmutableArray<int> counts)
    {
        for (var index = 0; index < counts.Length; index++)
        {
            if (counts[index] < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(counts),
                    counts[index],
                    $"Roster count at index {index} must not be negative.");
            }
        }
    }
}
