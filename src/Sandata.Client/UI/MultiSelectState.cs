using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Xna.Framework;

namespace Sandata.Client.UI;

/// <summary>
/// One candidate a marquee drag is being tested against: a screen position
/// and whether the underlying entity is hostile. Deliberately built from
/// primitives rather than any <c>Sandata.Core.Simulation.OperatorState</c>
/// type, the same convention <c>ContactList.ContactContent</c> follows, so
/// this file has no compile-time dependency on a record another task owns.
/// </summary>
internal readonly record struct MarqueeCandidate(int EntityId, Point ScreenPosition, bool IsHostile);

/// <summary>
/// Sandata's own multi-select state, design section 11's HUD element list:
/// "<c>AgentSelection</c> is single-entity only today, so Sandata gets its own
/// multi-select state as a pure record with tests." A drag rectangle over
/// friendlies is the only thing this state models in v0.1 — it never
/// includes a hostile, whatever its position, because a marquee is a
/// friendly-command gesture, not a targeting one.
/// </summary>
/// <remarks>
/// A record struct holding an <see cref="ImmutableArray{T}"/> needs a
/// hand-written <see cref="Equals(MultiSelectState)"/>/<see cref="GetHashCode"/>
/// pair, because <see cref="ImmutableArray{T}"/>.Equals compares the
/// underlying array by reference — the same trap documented on
/// <c>Hukbo.Client.UI.ArmyComposition</c>.
/// </remarks>
internal readonly record struct MultiSelectState(ImmutableArray<int> SelectedEntityIds)
{
    /// <summary>No entities selected.</summary>
    internal static readonly MultiSelectState Empty = new(ImmutableArray<int>.Empty);

    /// <summary>
    /// Whether <paramref name="candidate"/> falls inside
    /// <paramref name="marqueeBounds"/> and would be selected by it. A
    /// hostile candidate is never included, regardless of position —
    /// evaluated first so the boundary check below never even runs for one.
    /// <see cref="Rectangle.Contains(int, int)"/> treats the left and top
    /// edges as inclusive and the right and bottom edges as exclusive, so the
    /// last included pixel is one less than <c>Right</c>/<c>Bottom</c> and the
    /// first excluded pixel is exactly <c>Right</c>/<c>Bottom</c>.
    /// </summary>
    internal static bool IsIncluded(Rectangle marqueeBounds, MarqueeCandidate candidate) =>
        !candidate.IsHostile && marqueeBounds.Contains(candidate.ScreenPosition);

    /// <summary>
    /// The selection a completed marquee drag over <paramref name="marqueeBounds"/>
    /// produces, from <paramref name="candidates"/> in the order supplied.
    /// Never mutates <paramref name="candidates"/>; returns a new state.
    /// </summary>
    internal static MultiSelectState FromMarquee(
        Rectangle marqueeBounds,
        IReadOnlyList<MarqueeCandidate> candidates)
    {
        var builder = ImmutableArray.CreateBuilder<int>();
        foreach (var candidate in candidates)
        {
            if (IsIncluded(marqueeBounds, candidate))
            {
                builder.Add(candidate.EntityId);
            }
        }

        return new MultiSelectState(builder.ToImmutable());
    }

    public bool Equals(MultiSelectState other) => AreSelectedEntityIdsEqual(other.SelectedEntityIds);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        if (!SelectedEntityIds.IsDefaultOrEmpty)
        {
            foreach (var entityId in SelectedEntityIds)
            {
                hash.Add(entityId);
            }
        }

        return hash.ToHashCode();
    }

    private bool AreSelectedEntityIdsEqual(ImmutableArray<int> other)
    {
        if (SelectedEntityIds.IsDefaultOrEmpty || other.IsDefaultOrEmpty)
        {
            return SelectedEntityIds.IsDefaultOrEmpty == other.IsDefaultOrEmpty;
        }

        return SelectedEntityIds.AsSpan().SequenceEqual(other.AsSpan());
    }
}
