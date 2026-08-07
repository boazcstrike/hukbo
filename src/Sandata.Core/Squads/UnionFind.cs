namespace Sandata.Core.Squads;

/// <summary>
/// A deterministic disjoint-set (union-find) structure over the dense index
/// range <c>[0, count)</c>, with path compression and union by size. This
/// type knows nothing about entity IDs, factions, or squads — it is the
/// generic mechanism <see cref="SquadGrouping"/> builds squad derivation on
/// top of, kept separate so the algorithm and the domain rule each have one
/// job.
/// </summary>
/// <remarks>
/// <para>
/// Storage is two flat <see cref="int"/> arrays indexed by the same dense
/// index every caller uses, never a <c>Dictionary&lt;</c> or a
/// <c>HashSet&lt;</c>, so enumeration order can never leak into a result —
/// there is no enumeration here at all.
/// </para>
/// <para>
/// <see cref="Find"/> is iterative, not recursive: a chain of union
/// operations can be as long as the caller's whole roster, and an iterative
/// walk with two passes (find the root, then relink every visited node
/// directly to it) cannot overflow the call stack the way a naive recursive
/// path-compression implementation could.
/// </para>
/// <para>
/// <see cref="Union"/> breaks a size tie by attaching the higher-indexed root
/// under the lower-indexed one. This keeps the resulting tree shape a pure
/// function of the index values rather than of visitation order, but the
/// choice is cosmetic for correctness: the final partition into components —
/// the only thing <see cref="SquadGrouping"/> reads back out through
/// repeated <see cref="Find"/> calls — is the same for any union order and
/// any tie-break rule, because set union is commutative and associative.
/// </para>
/// </remarks>
internal sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly int[] _size;

    /// <param name="count">The number of elements, each identified by its index in <c>[0, count)</c>. Every element starts in its own singleton component.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    internal UnionFind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        _parent = new int[count];
        _size = new int[count];

        for (var index = 0; index < count; index++)
        {
            _parent[index] = index;
            _size[index] = 1;
        }
    }

    /// <summary>The number of elements this structure was built over.</summary>
    internal int Count => _parent.Length;

    /// <summary>
    /// Finds the representative (root) of <paramref name="index"/>'s
    /// component, compressing the path from <paramref name="index"/> to that
    /// root so a later call over the same chain is cheaper. Two elements are
    /// in the same component exactly when they return the same root.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    internal int Find(int index)
    {
        var root = index;

        while (_parent[root] != root)
        {
            root = _parent[root];
        }

        while (_parent[index] != root)
        {
            var next = _parent[index];
            _parent[index] = root;
            index = next;
        }

        return root;
    }

    /// <summary>
    /// Merges the components containing <paramref name="left"/> and
    /// <paramref name="right"/>. A no-op when they are already in the same
    /// component. Union by size: the smaller component's root is attached
    /// under the larger's, so no chain of unions can build a path longer than
    /// logarithmic in the component size before <see cref="Find"/>'s path
    /// compression flattens it further.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="left"/> or <paramref name="right"/> is outside <c>[0, Count)</c>.
    /// </exception>
    internal void Union(int left, int right)
    {
        var leftRoot = Find(left);
        var rightRoot = Find(right);

        if (leftRoot == rightRoot)
        {
            return;
        }

        if (_size[leftRoot] < _size[rightRoot] ||
            (_size[leftRoot] == _size[rightRoot] && leftRoot > rightRoot))
        {
            (leftRoot, rightRoot) = (rightRoot, leftRoot);
        }

        _parent[rightRoot] = leftRoot;
        _size[leftRoot] += _size[rightRoot];
    }
}
