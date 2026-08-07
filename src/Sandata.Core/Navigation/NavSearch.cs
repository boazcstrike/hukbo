namespace Sandata.Core.Navigation;

/// <summary>
/// What <see cref="NavSearch.TryFindPath"/> found.
/// </summary>
public enum NavSearchOutcome
{
    /// <summary>A path from start to goal exists and was written out.</summary>
    PathFound = 0,

    /// <summary>No path exists — the start and goal cells are not connected
    /// through passable, non-corner-cutting cells, or the start or goal cell
    /// is itself blocked.</summary>
    Unreachable = 1,
}

/// <summary>
/// Grid A* over a <see cref="NavGrid"/>, ordered by <see cref="NavComparer"/>'s
/// total key. Design section 7, "Shape": "Uniform integer grid A* decides
/// topology." This type owns none of the map's geometry or wall data — it
/// takes passability as an explicit <see cref="ReadOnlySpan{Boolean}"/>
/// parameter on every call, because task 18 (<c>NavBake</c>) is still
/// deciding <see cref="NavGrid"/>'s own passability representation in a
/// parallel worktree at the time this type is written. A caller that already
/// has a baked <c>byte[] passability</c> array converts it to
/// "blocked" booleans at the call site.
/// </summary>
///
/// <remarks>
/// <para>
/// <b>Scratch state, never cleared between searches.</b> <c>gScore</c> and
/// <c>cameFrom</c> are flat <see cref="int"/> arrays sized once to the
/// grid's cell count and reused by every subsequent search on a grid of that
/// size; a monotonically increasing generation counter — design section 7's
/// <c>visitStamp</c> — marks which of their entries are valid for the
/// current search, so nothing needs to be zeroed out between calls. This
/// implementation carries one further generation-stamped array beyond the
/// three design section 7 names (<c>gScore</c>, <c>cameFrom</c>,
/// <c>visitStamp</c>): a "closed" stamp, using the identical technique, that
/// records which nodes have already had their neighbours relaxed in the
/// current search. Without it, a node reachable by more than one predecessor
/// before its own cheapest entry is ever popped would have its neighbours
/// relaxed again every time one of its now-stale, more-expensive open-set
/// entries is later popped and discarded — harmless to the path found, since
/// a stale pop is detected and skipped before it can relax anything, but it
/// would make <see cref="TryFindPath"/>'s <c>expandedNodeSequence</c> output
/// list a node more than once, which is not what "expanded" should mean for
/// an inspector or a test to read.
/// </para>
///
/// <para>
/// <b>Stale open-set entries.</b> <see cref="NavOpenSet"/> freezes a slot's
/// key when it is pushed and never updates it in place, so improving a
/// node's cost means pushing it again rather than mutating its existing
/// slot. When a popped entry's <c>f</c> no longer matches
/// <c>gScore[nodeIndex] + h</c> recomputed at pop time, that entry describes
/// a cost this node has since bettered, and is discarded without being
/// expanded.
/// </para>
/// </remarks>
public sealed class NavSearch
{
    private const int NoPredecessor = -1;
    private const int NoGeneration = 0;

    private int[] _gScore = [];
    private int[] _cameFrom = [];
    private int[] _openGeneration = [];
    private int[] _closedGeneration = [];
    private int _generation = NoGeneration;
    private readonly NavOpenSet _openSet = new();

    /// <summary>
    /// Searches <paramref name="grid"/> for the lowest-cost path from
    /// <paramref name="startCellIndex"/> to <paramref name="goalCellIndex"/>,
    /// under <paramref name="blocked"/> passability and the no-corner-cutting
    /// rule (design section 7 / <see cref="NavNeighbors.IsDiagonalMoveBlocked"/>).
    /// </summary>
    /// <param name="grid">The grid topology to search.</param>
    /// <param name="startCellIndex">The starting cell, a valid index into <paramref name="grid"/>.</param>
    /// <param name="goalCellIndex">The goal cell, a valid index into <paramref name="grid"/>.</param>
    /// <param name="blocked">
    /// One entry per cell in <paramref name="grid"/>, <see langword="true"/>
    /// when the cell cannot be entered. Length must equal
    /// <see cref="NavGrid.CellCount"/>.
    /// </param>
    /// <param name="pathCellIndices">
    /// Cleared, then filled with the found path's cell indices in order from
    /// start to goal (inclusive of both). Left empty when no path is found.
    /// Reused across calls by the caller to avoid a per-search allocation.
    /// </param>
    /// <param name="expandedNodeSequence">
    /// Cleared, then filled with every cell index in the exact order this
    /// search expanded it (popped its cheapest entry and relaxed its
    /// neighbours). This is the sequence the determinism contract cares
    /// about: for the same grid, passability, start, and goal, it is
    /// identical on every run, on every machine, and regardless of this
    /// <see cref="NavSearch"/> instance's prior history.
    /// </param>
    public NavSearchOutcome TryFindPath(
        NavGrid grid,
        int startCellIndex,
        int goalCellIndex,
        ReadOnlySpan<bool> blocked,
        List<int> pathCellIndices,
        List<int> expandedNodeSequence)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(pathCellIndices);
        ArgumentNullException.ThrowIfNull(expandedNodeSequence);

        pathCellIndices.Clear();
        expandedNodeSequence.Clear();

        if (blocked.Length != grid.CellCount)
        {
            throw new ArgumentException(
                $"Passability length {blocked.Length} does not match the grid's {grid.CellCount} cells.",
                nameof(blocked));
        }

        ValidateCellIndex(grid, startCellIndex, nameof(startCellIndex));
        ValidateCellIndex(grid, goalCellIndex, nameof(goalCellIndex));

        EnsureCapacity(grid.CellCount);
        _openSet.Reset();
        _generation++;
        var generation = _generation;

        if (blocked[startCellIndex] || blocked[goalCellIndex])
        {
            return NavSearchOutcome.Unreachable;
        }

        if (startCellIndex == goalCellIndex)
        {
            pathCellIndices.Add(startCellIndex);
            return NavSearchOutcome.PathFound;
        }

        var goalX = grid.CellX(goalCellIndex);
        var goalY = grid.CellY(goalCellIndex);

        _gScore[startCellIndex] = 0;
        _openGeneration[startCellIndex] = generation;
        _cameFrom[startCellIndex] = NoPredecessor;

        var startHeuristic = NavHeuristic.Octile(
            grid.CellX(startCellIndex), grid.CellY(startCellIndex), goalX, goalY);
        _openSet.Push(startCellIndex, startHeuristic, startHeuristic);

        while (_openSet.TryPop(out var nodeIndex, out var poppedF, out var poppedH))
        {
            if (_closedGeneration[nodeIndex] == generation)
            {
                continue;
            }

            var currentF = checked(_gScore[nodeIndex] + poppedH);
            if (currentF != poppedF)
            {
                continue;
            }

            _closedGeneration[nodeIndex] = generation;
            expandedNodeSequence.Add(nodeIndex);

            if (nodeIndex == goalCellIndex)
            {
                BuildReversedPath(nodeIndex, pathCellIndices);
                pathCellIndices.Reverse();
                return NavSearchOutcome.PathFound;
            }

            ExpandNeighbours(grid, nodeIndex, blocked, goalX, goalY, generation);
        }

        return NavSearchOutcome.Unreachable;
    }

    private void ExpandNeighbours(
        NavGrid grid, int nodeIndex, ReadOnlySpan<bool> blocked, int goalX, int goalY, int generation)
    {
        var nodeX = grid.CellX(nodeIndex);
        var nodeY = grid.CellY(nodeIndex);

        for (var direction = 0; direction < NavNeighbors.DirectionCount; direction++)
        {
            var neighbourX = nodeX + NavNeighbors.OffsetX(direction);
            var neighbourY = nodeY + NavNeighbors.OffsetY(direction);

            if (!grid.TryGetCellIndex(neighbourX, neighbourY, out var neighbourIndex))
            {
                continue;
            }

            if (blocked[neighbourIndex])
            {
                continue;
            }

            if (NavNeighbors.IsDiagonal(direction) &&
                IsCornerCut(grid, nodeX, nodeY, direction, blocked))
            {
                continue;
            }

            var stepCost = NavNeighbors.IsDiagonal(direction)
                ? NavHeuristic.DiagonalStepCost
                : NavHeuristic.OrthogonalStepCost;
            var candidateG = checked(_gScore[nodeIndex] + stepCost);

            var neighbourHasCurrentCost = _openGeneration[neighbourIndex] == generation;
            if (neighbourHasCurrentCost && candidateG >= _gScore[neighbourIndex])
            {
                continue;
            }

            _gScore[neighbourIndex] = candidateG;
            _openGeneration[neighbourIndex] = generation;
            _cameFrom[neighbourIndex] = nodeIndex;

            var neighbourHeuristic = NavHeuristic.Octile(neighbourX, neighbourY, goalX, goalY);
            _openSet.Push(neighbourIndex, checked(candidateG + neighbourHeuristic), neighbourHeuristic);
        }
    }

    private static bool IsCornerCut(
        NavGrid grid, int nodeX, int nodeY, int diagonalDirection, ReadOnlySpan<bool> blocked)
    {
        var (first, second) = NavNeighbors.OrthogonalComponentsOf(diagonalDirection);

        var firstBlocked = IsBlockedOrOffGrid(
            grid, nodeX + NavNeighbors.OffsetX(first), nodeY + NavNeighbors.OffsetY(first), blocked);
        var secondBlocked = IsBlockedOrOffGrid(
            grid, nodeX + NavNeighbors.OffsetX(second), nodeY + NavNeighbors.OffsetY(second), blocked);

        return NavNeighbors.IsDiagonalMoveBlocked(firstBlocked, secondBlocked);
    }

    private static bool IsBlockedOrOffGrid(NavGrid grid, int x, int y, ReadOnlySpan<bool> blocked) =>
        !grid.TryGetCellIndex(x, y, out var cellIndex) || blocked[cellIndex];

    private void BuildReversedPath(int goalCellIndex, List<int> pathCellIndices)
    {
        var current = goalCellIndex;
        while (current != NoPredecessor)
        {
            pathCellIndices.Add(current);
            current = _cameFrom[current];
        }
    }

    private void EnsureCapacity(int cellCount)
    {
        if (_gScore.Length >= cellCount)
        {
            return;
        }

        _gScore = new int[cellCount];
        _cameFrom = new int[cellCount];
        _openGeneration = new int[cellCount];
        _closedGeneration = new int[cellCount];
        _generation = NoGeneration;
    }

    private static void ValidateCellIndex(NavGrid grid, int cellIndex, string paramName)
    {
        if (cellIndex < 0 || cellIndex >= grid.CellCount)
        {
            throw new ArgumentOutOfRangeException(
                paramName, cellIndex, $"Cell index must be within [0, {grid.CellCount}).");
        }
    }
}
