using System.Runtime.CompilerServices;
using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Receives the candidates a <see cref="TargetingGrid"/> search visits, and
/// tells the search when no unvisited candidate can matter any more.
/// </summary>
/// <remarks>
/// Implemented by a struct and passed by <c>ref</c> to a generic method, so the
/// JIT specialises the search per visitor and a warm tick allocates nothing.
/// </remarks>
internal interface ITargetingVisitor
{
    /// <summary>Considers one living candidate of the grid's faction.</summary>
    void Visit(AgentState candidate);

    /// <summary>
    /// Whether no candidate at a squared distance of at least
    /// <paramref name="lowerBoundSquared"/> can change any result the visitor
    /// needs. The search stops before the next ring when this returns
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="lowerBoundSquared"/> is the squared lower bound of the
    /// next ring: every unvisited candidate is at least that far away. A
    /// visitor that tracks a best squared distance stops on a strict
    /// <c>lowerBoundSquared &gt; best</c>. Strict is a safety margin, not a
    /// requirement: with integer coordinates and half-open cells, a candidate
    /// in the next ring is strictly farther than the bound, so
    /// <c>&gt;=</c> would select the same results. Strict keeps the stop
    /// correct even if an equally distant candidate with a lower entity ID
    /// were ever possible in that ring.
    /// </remarks>
    bool CanStopBefore(long lowerBoundSquared);
}

/// <summary>
/// A per-faction uniform grid over living agents that answers nearest-enemy
/// queries by walking rings of cells outward from the query point.
/// </summary>
/// <remarks>
/// <para>
/// Ring-search targeting design: <c>docs/plans/2026-11-21-ring-search-targeting-design.md</c>.
/// The grid is a derived accelerator under section 6 of
/// <c>SIMULATION-GAME-STANDARDS.md</c> and its "uniform grid is a derived
/// oracle" rule: never hashed, never snapshotted, never persisted. Its only
/// contract is that a search selects exactly what an O(n^2) scan over the same
/// agents would select.
/// </para>
/// <para>
/// Every candidate in a cell whose Chebyshev offset from the query cell is
/// <c>k &gt;= 1</c> lies at least <c>(k - 1) * C</c> from the query point along
/// the axis of that offset, so the search stops before ring <c>k</c> once the
/// visitor reports that bound can no longer matter. The query cell is found
/// with floor division, which gives the tightest bound for a query point left
/// of or above the grid. Truncating division would put that cell closer to the
/// grid and only loosen the bound, so the search would visit more rings and
/// select the same result.
/// </para>
/// <para>
/// Storage is compressed rows: a counting pass, a prefix sum, and a scatter
/// pass that writes agent indexes. The cell-offset array is sized on the
/// first rebuild for the extent the constructor was told to expect, which for
/// a battle is the map, since agents are clamped to it. The two agent-sized
/// arrays are sized by the first rebuild and grow only if a later rebuild needs
/// more slots, which a battle never does because its agent array and living
/// counts do not grow. A warm rebuild in a battle therefore allocates nothing.
/// A box wider than expected, which only a test that places agents outside the
/// map produces, grows the array by doubling. No dictionary is used.
/// </para>
/// <para>
/// The rebuild and search methods are marked to skip tiered compilation. A
/// short battle spends most of its ticks in the unoptimised first tier, and
/// measured there the ring walk cost more than the scan it replaces; jitted
/// optimised on first call it is faster from the first tick.
/// </para>
/// <para>
/// A query coordinate far outside the grid is safe: the lower bound saturates
/// instead of overflowing, and the query cell is computed in <c>long</c> then
/// narrowed with a checked cast. That cast holds for any query coordinate when
/// the cell side is at least 2 raw units, which the default side of
/// <see cref="DefaultCellSizeRaw"/> satisfies by a wide margin.
/// </para>
/// </remarks>
internal sealed class TargetingGrid
{
    /// <summary>
    /// The starting cell side, 32 world units: about four body diameters.
    /// Provisional until the cell-size sweep of the plan's RS-3 task measures
    /// it.
    /// </summary>
    internal const int DefaultCellSizeRaw = 32 * FixedPoint.Scale;

    /// <summary>
    /// The most cells the grid may use along one axis. A faction whose bounding
    /// box would need more has its cell side grown for that tick instead.
    /// </summary>
    internal const int MaximumCellsPerAxis = 256;

    private const int InitialCapacity = 64;

    /// <summary>
    /// The largest value whose square fits in a <see cref="long"/>. A lower
    /// bound above it saturates to <see cref="long.MaxValue"/>, which no real
    /// squared distance can exceed, so the stop test stays exact.
    /// </summary>
    private const long MaximumSquarableValue = 3_037_000_499;

    private readonly int _baseCellSizeRaw;

    private readonly int _expectedCellCount;

    private AgentState[] _agents = [];

    private int[] _cellStart = [];

    private int[] _cellAgents = new int[InitialCapacity];

    private int[] _cellOfAgent = new int[InitialCapacity];

    private long _originX;

    private long _originY;

    private long _cellSize;

    private int _cellsX;

    private int _cellsY;

    /// <param name="cellSizeRaw">
    /// The starting cell side in raw fixed-point units.
    /// </param>
    /// <param name="expectedExtentRaw">
    /// The widest side of a faction's bounding box that a rebuild is expected
    /// to see, in raw units. The first rebuild sizes the cell-offset array for
    /// a box of that extent, so a later rebuild within it never allocates. A
    /// wider box still works and grows the array.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="cellSizeRaw"/> is zero or negative, or
    /// <paramref name="expectedExtentRaw"/> is negative.
    /// </exception>
    internal TargetingGrid(int cellSizeRaw = DefaultCellSizeRaw, long expectedExtentRaw = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSizeRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedExtentRaw);

        _baseCellSizeRaw = cellSizeRaw;
        var expectedCellsPerAxis = Math.Min(
            (expectedExtentRaw / cellSizeRaw) + 1,
            MaximumCellsPerAxis);
        _expectedCellCount = checked((int)(expectedCellsPerAxis * expectedCellsPerAxis));
    }

    /// <summary>The cell side used by the last <see cref="Rebuild"/>, in raw units.</summary>
    internal long CellSizeRaw => _cellSize;

    /// <summary>Whether the last <see cref="Rebuild"/> found no living agent.</summary>
    internal bool IsEmpty => _cellsX == 0;

    /// <summary>
    /// Discards the previous contents and indexes the living agents of
    /// <paramref name="factionId"/>, reading the same <c>XRaw</c>, <c>YRaw</c>,
    /// and <c>IsAlive</c> values the brute-force scan reads.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void Rebuild(AgentState[] agents, int factionId)
    {
        _agents = agents;
        _cellsX = 0;
        _cellsY = 0;

        var count = 0;
        long minX = long.MaxValue;
        long minY = long.MaxValue;
        long maxX = long.MinValue;
        long maxY = long.MinValue;
        foreach (var agent in agents)
        {
            if (!agent.IsAlive || agent.FactionId != factionId)
            {
                continue;
            }

            count++;
            minX = Math.Min(minX, agent.XRaw);
            minY = Math.Min(minY, agent.YRaw);
            maxX = Math.Max(maxX, agent.XRaw);
            maxY = Math.Max(maxY, agent.YRaw);
        }

        if (count == 0)
        {
            return;
        }

        var extent = Math.Max(maxX - minX, maxY - minY);
        var growth = (extent / ((long)MaximumCellsPerAxis * _baseCellSizeRaw)) + 1;
        _cellSize = _baseCellSizeRaw * growth;
        _originX = minX;
        _originY = minY;
        _cellsX = checked((int)(((maxX - minX) / _cellSize) + 1));
        _cellsY = checked((int)(((maxY - minY) / _cellSize) + 1));

        EnsureCapacity(agents.Length, count, _cellsX * _cellsY);
        Scatter(agents, factionId, count);
    }

    /// <summary>
    /// Visits the grid's agents in rings of cells outward from the query
    /// point, nearest ring first, until the visitor reports that no unvisited
    /// candidate can matter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void Search<TVisitor>(int queryX, int queryY, ref TVisitor visitor)
        where TVisitor : struct, ITargetingVisitor
    {
        if (_cellsX == 0)
        {
            return;
        }

        var cellX = FloorDivide((long)queryX - _originX, _cellSize);
        var cellY = FloorDivide((long)queryY - _originY, _cellSize);
        var lastX = _cellsX - 1;
        var lastY = _cellsY - 1;
        var firstRing = Math.Max(
            DistanceOutside(cellX, lastX),
            DistanceOutside(cellY, lastY));
        var lastRing = Math.Max(
            Math.Max(cellX, lastX - cellX),
            Math.Max(cellY, lastY - cellY));

        for (var ring = firstRing; ring <= lastRing; ring++)
        {
            var lowerBound = ring <= 1 ? 0 : (ring - 1) * _cellSize;
            if (visitor.CanStopBefore(SaturatingSquare(lowerBound)))
            {
                return;
            }

            VisitRing(cellX, cellY, ring, ref visitor);
        }
    }

    private void EnsureCapacity(int agentSlots, int livingCount, int cellCount)
    {
        if (_cellOfAgent.Length < agentSlots)
        {
            _cellOfAgent = new int[Math.Max(agentSlots, _cellOfAgent.Length * 2)];
        }

        if (_cellAgents.Length < livingCount)
        {
            _cellAgents = new int[Math.Max(livingCount, _cellAgents.Length * 2)];
        }

        if (_cellStart.Length < cellCount + 1)
        {
            _cellStart = new int[Math.Max(
                Math.Max(cellCount, _expectedCellCount) + 1,
                _cellStart.Length * 2)];
        }
    }

    /// <summary>
    /// Counting pass, prefix sum, then a scatter that walks the agents in
    /// descending index and pre-decrements each cell's end offset, which leaves
    /// every cell's agents in ascending index and every start offset in place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Scatter(AgentState[] agents, int factionId, int livingCount)
    {
        var cellCount = _cellsX * _cellsY;
        Array.Clear(_cellStart, 0, cellCount + 1);

        for (var index = 0; index < agents.Length; index++)
        {
            var agent = agents[index];
            if (!agent.IsAlive || agent.FactionId != factionId)
            {
                continue;
            }

            var column = (agent.XRaw - _originX) / _cellSize;
            var row = (agent.YRaw - _originY) / _cellSize;
            var cell = (int)((row * _cellsX) + column);
            _cellOfAgent[index] = cell;
            _cellStart[cell]++;
        }

        var runningEnd = 0;
        for (var cell = 0; cell < cellCount; cell++)
        {
            runningEnd += _cellStart[cell];
            _cellStart[cell] = runningEnd;
        }

        _cellStart[cellCount] = livingCount;

        for (var index = agents.Length - 1; index >= 0; index--)
        {
            var agent = agents[index];
            if (!agent.IsAlive || agent.FactionId != factionId)
            {
                continue;
            }

            _cellAgents[--_cellStart[_cellOfAgent[index]]] = index;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void VisitRing<TVisitor>(
        int cellX,
        int cellY,
        int ring,
        ref TVisitor visitor)
        where TVisitor : struct, ITargetingVisitor
    {
        if (ring == 0)
        {
            VisitCells((cellY * _cellsX) + cellX, (cellY * _cellsX) + cellX, ref visitor);
            return;
        }

        var lastX = _cellsX - 1;
        var lastY = _cellsY - 1;
        var firstColumn = Math.Max(cellX - ring, 0);
        var lastColumn = Math.Min(cellX + ring, lastX);

        // Two full rows. A row of cells is contiguous in the scatter array, so
        // each is visited as one run.
        var topRow = cellY - ring;
        if (topRow >= 0 && topRow <= lastY)
        {
            VisitCells((topRow * _cellsX) + firstColumn, (topRow * _cellsX) + lastColumn, ref visitor);
        }

        var bottomRow = cellY + ring;
        if (bottomRow >= 0 && bottomRow <= lastY)
        {
            VisitCells(
                (bottomRow * _cellsX) + firstColumn,
                (bottomRow * _cellsX) + lastColumn,
                ref visitor);
        }

        // Two edge columns, without the corner cells the rows already covered.
        var firstRow = Math.Max(cellY - ring + 1, 0);
        var lastRow = Math.Min(cellY + ring - 1, lastY);
        var leftColumn = cellX - ring;
        if (leftColumn >= 0 && leftColumn <= lastX)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                VisitCells((row * _cellsX) + leftColumn, (row * _cellsX) + leftColumn, ref visitor);
            }
        }

        var rightColumn = cellX + ring;
        if (rightColumn >= 0 && rightColumn <= lastX)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                VisitCells((row * _cellsX) + rightColumn, (row * _cellsX) + rightColumn, ref visitor);
            }
        }
    }

    /// <summary>Visits every agent in the contiguous cell run, inclusive on both ends.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void VisitCells<TVisitor>(int firstCell, int lastCell, ref TVisitor visitor)
        where TVisitor : struct, ITargetingVisitor
    {
        var end = _cellStart[lastCell + 1];
        for (var slot = _cellStart[firstCell]; slot < end; slot++)
        {
            visitor.Visit(_agents[_cellAgents[slot]]);
        }
    }

    /// <summary>
    /// Floor division for a positive divisor. C# integer division truncates
    /// toward zero, which would put a query point left of or above the grid one
    /// cell closer to it than it really is. That only loosens the ring bound
    /// and costs extra rings; floor division is kept because it is tighter.
    /// </summary>
    private static int FloorDivide(long dividend, long divisor)
    {
        var quotient = dividend / divisor;
        if (dividend % divisor != 0 && dividend < 0)
        {
            quotient--;
        }

        return checked((int)quotient);
    }

    /// <summary>How many cells <paramref name="cell"/> lies outside [0, <paramref name="last"/>].</summary>
    private static int DistanceOutside(int cell, int last) =>
        cell < 0 ? -cell : (cell > last ? cell - last : 0);

    private static long SaturatingSquare(long value) =>
        value > MaximumSquarableValue ? long.MaxValue : value * value;
}
