using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Sandata.Client.UI;

/// <summary>
/// A typed undo stack: push, pop, and a depth limit, with no producers in
/// v0.1 — design section 11's HUD element list: "a typed stack with push,
/// pop, and a depth limit." Design section 16 is the binding constraint on
/// this file specifically: "Nothing about them crosses into
/// <c>Sandata.Core</c>: they are not snapshotted, they do not fold into any
/// hash, and undoing a drawn node that was never submitted is invisible to
/// the simulation." <typeparamref name="T"/> is therefore fully unconstrained
/// and this file references nothing outside the base class library — a later
/// task's test asserting "no <c>Sandata.Core</c> type is reachable from the
/// undo stack" (task 62's acceptance criterion) holds by construction, not by
/// convention.
/// </summary>
/// <remarks>
/// Task 62 supplies the first real producer — an authored path's unsubmitted
/// nodes. Nothing in v0.1 drives this stack; it exists, is unit tested, and
/// is reachable from the UI project, matching the "scaffolded" reading design
/// section 11 gives the whole interaction layer.
/// </remarks>
internal sealed class UndoStack<T>
{
    /// <summary>
    /// The depth limit used when a caller does not name one. No design or
    /// plan value exists for this — provisional, see this file's remarks and
    /// the task report's PROVISIONAL section.
    /// </summary>
    internal const int DefaultDepthLimit = 50;

    private readonly List<T> _entries = [];

    /// <summary>
    /// Creates a stack that discards its oldest entry once <see cref="Push"/>
    /// would grow it past <paramref name="depthLimit"/> entries.
    /// </summary>
    internal UndoStack(int depthLimit = DefaultDepthLimit)
    {
        if (depthLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depthLimit),
                depthLimit,
                "Depth limit must be positive.");
        }

        DepthLimit = depthLimit;
    }

    /// <summary>The most entries this stack retains at once.</summary>
    internal int DepthLimit { get; }

    /// <summary>The entries currently held, oldest first.</summary>
    internal IReadOnlyList<T> Entries => _entries;

    /// <summary>The number of entries currently held.</summary>
    internal int Depth => _entries.Count;

    /// <summary>
    /// Pushes <paramref name="entry"/> onto the stack. If that grows the
    /// stack past <see cref="DepthLimit"/>, the single oldest entry is
    /// discarded — a bounded stack never grows without limit, the same
    /// "at most N retained" shape <c>SandataEventLog.MaxRetainedEvents</c>
    /// already enforces for the event feed.
    /// </summary>
    internal void Push(T entry)
    {
        _entries.Add(entry);

        if (_entries.Count > DepthLimit)
        {
            _entries.RemoveAt(0);
        }
    }

    /// <summary>
    /// Removes and returns the most recently pushed entry. Returns
    /// <see langword="false"/>, leaving the stack unchanged, when it is
    /// empty.
    /// </summary>
    internal bool TryPop([MaybeNullWhen(false)] out T entry)
    {
        if (_entries.Count == 0)
        {
            entry = default;
            return false;
        }

        var topIndex = _entries.Count - 1;
        entry = _entries[topIndex];
        _entries.RemoveAt(topIndex);
        return true;
    }
}
