namespace Sandata.Core.Navigation;

/// <summary>
/// Grid A*'s open set: a binary min-heap over node indices, ordered by
/// <see cref="NavComparer"/>'s total key <c>(f, h, nodeIndex)</c>. Design
/// section 7, "Data structures: flat arrays, no dictionaries": the backing
/// storage is three parallel flat <see cref="int"/> arrays — no
/// <c>Dictionary</c>, no <c>HashSet</c>, and no
/// <c>PriorityQueue&lt;TElement, TPriority&gt;</c>, per the plan's standing
/// rule 3 and the banned-token scan in <c>SandataSourceHygieneTests</c>.
/// </summary>
///
/// <remarks>
/// <para>
/// <b>Each slot's key is frozen at the moment it is pushed and never mutated
/// in place afterwards.</b> Grid A* (<see cref="NavSearch"/>) improves a
/// node's cost by calling <see cref="Push"/> again with the node's new,
/// better key, rather than searching the heap for that node's existing slot
/// and updating it there — a classic "decrease-key" operation this type does
/// not implement. <see cref="NavSearch"/> is responsible for recognising and
/// discarding a stale, superseded slot when it is eventually popped, by
/// comparing the popped <c>f</c> against the node's current best known cost.
/// </para>
///
/// <para>
/// This shape is deliberate, not a shortcut. A plain node-index heap whose
/// comparisons read a node's <c>f</c> and <c>h</c> live out of shared,
/// mutable per-node arrays looks simpler, but it is not sound: once a slot's
/// position has been fixed by earlier sift operations, silently changing the
/// value that slot's live lookup returns — without re-sifting that specific
/// slot — can leave a parent/child pair whose *current* values violate the
/// heap invariant, because nothing about updating the shared array ever
/// revisits that slot's position. That failure mode does not show up on
/// every run; it depends on the heap's exact internal shape at the moment of
/// the update, which is precisely the "some machines, some runs, or after an
/// unrelated capacity change" hazard the risk register calls out. Freezing
/// each slot's key at push time sidesteps it entirely: a slot's own key never
/// changes after it is placed, so every comparison the heap has ever made
/// about that slot remains true for as long as the slot survives, regardless
/// of anything pushed or popped elsewhere, and regardless of how many times
/// the backing arrays have grown.
/// </para>
/// </remarks>
public sealed class NavOpenSet
{
    private int[] _nodeIndex;
    private int[] _f;
    private int[] _h;
    private int _count;

    /// <summary>
    /// Creates an open set with room for <paramref name="initialCapacity"/>
    /// entries before its backing arrays first grow. Growth only ever
    /// enlarges the existing arrays (see <see cref="Reset"/>); a search that
    /// never needs more room than a prior search already grew this instance
    /// to allocates nothing.
    /// </summary>
    public NavOpenSet(int initialCapacity = 64)
    {
        if (initialCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialCapacity), initialCapacity, "Initial capacity must be positive.");
        }

        _nodeIndex = new int[initialCapacity];
        _f = new int[initialCapacity];
        _h = new int[initialCapacity];
        _count = 0;
    }

    /// <summary>The number of entries currently in the heap.</summary>
    public int Count => _count;

    /// <summary>
    /// Empties the heap for reuse by a new search. Does not reallocate or
    /// clear the backing arrays — only the count resets — so the arrays stay
    /// exactly as large as the biggest search this instance has ever run, and
    /// a search no bigger than a prior one allocates nothing.
    /// </summary>
    public void Reset() => _count = 0;

    /// <summary>
    /// Pushes <paramref name="nodeIndex"/> onto the heap with the frozen key
    /// <c>(f, h, nodeIndex)</c>. Safe to call more than once for the same
    /// <paramref name="nodeIndex"/> in one search — see the remarks on this
    /// type for why that is the intended way to record an improved cost.
    /// </summary>
    public void Push(int nodeIndex, int f, int h)
    {
        EnsureCapacity(_count + 1);

        var slot = _count;
        _nodeIndex[slot] = nodeIndex;
        _f[slot] = f;
        _h[slot] = h;
        _count++;

        SiftUp(slot);
    }

    /// <summary>
    /// Removes and returns the entry whose frozen key sorts lowest under
    /// <see cref="NavComparer"/>. Returns <see langword="false"/> without
    /// setting the out parameters to anything meaningful when the heap is
    /// empty.
    /// </summary>
    public bool TryPop(out int nodeIndex, out int f, out int h)
    {
        if (_count == 0)
        {
            nodeIndex = 0;
            f = 0;
            h = 0;
            return false;
        }

        nodeIndex = _nodeIndex[0];
        f = _f[0];
        h = _h[0];

        var last = _count - 1;
        _nodeIndex[0] = _nodeIndex[last];
        _f[0] = _f[last];
        _h[0] = _h[last];
        _count--;

        if (_count > 0)
        {
            SiftDown(0);
        }

        return true;
    }

    private void SiftUp(int slot)
    {
        while (slot > 0)
        {
            var parent = (slot - 1) / 2;
            if (CompareSlots(slot, parent) >= 0)
            {
                break;
            }

            Swap(slot, parent);
            slot = parent;
        }
    }

    private void SiftDown(int slot)
    {
        while (true)
        {
            var left = (slot * 2) + 1;
            var right = left + 1;
            var smallest = slot;

            if (left < _count && CompareSlots(left, smallest) < 0)
            {
                smallest = left;
            }

            if (right < _count && CompareSlots(right, smallest) < 0)
            {
                smallest = right;
            }

            if (smallest == slot)
            {
                break;
            }

            Swap(slot, smallest);
            slot = smallest;
        }
    }

    private int CompareSlots(int a, int b) =>
        NavComparer.Compare(_f[a], _h[a], _nodeIndex[a], _f[b], _h[b], _nodeIndex[b]);

    private void Swap(int a, int b)
    {
        (_nodeIndex[a], _nodeIndex[b]) = (_nodeIndex[b], _nodeIndex[a]);
        (_f[a], _f[b]) = (_f[b], _f[a]);
        (_h[a], _h[b]) = (_h[b], _h[a]);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _nodeIndex.Length)
        {
            return;
        }

        var newCapacity = _nodeIndex.Length * 2;
        while (newCapacity < required)
        {
            newCapacity *= 2;
        }

        Array.Resize(ref _nodeIndex, newCapacity);
        Array.Resize(ref _f, newCapacity);
        Array.Resize(ref _h, newCapacity);
    }
}
