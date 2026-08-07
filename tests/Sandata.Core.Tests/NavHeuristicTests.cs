using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

public sealed class NavHeuristicTests
{
    [Theory]
    [InlineData(NavNeighbors.East, 10)]
    [InlineData(NavNeighbors.SouthEast, 14)]
    [InlineData(NavNeighbors.South, 10)]
    [InlineData(NavNeighbors.SouthWest, 14)]
    [InlineData(NavNeighbors.West, 10)]
    [InlineData(NavNeighbors.NorthWest, 14)]
    [InlineData(NavNeighbors.North, 10)]
    [InlineData(NavNeighbors.NorthEast, 14)]
    public void Octile_OfOneStepInEachOfTheEightCompassDirections_MatchesTheOrthogonalOrDiagonalCost(
        int direction, int expectedCost)
    {
        var deltaX = NavNeighbors.OffsetX(direction);
        var deltaY = NavNeighbors.OffsetY(direction);

        Assert.Equal(expectedCost, NavHeuristic.Octile(deltaX, deltaY));
    }

    [Theory]
    [InlineData(5, 3, 62)] // min 3, max 5: 10 * (5 - 3) + 14 * 3 == 20 + 42
    [InlineData(12, 7, 148)] // min 7, max 12: 10 * (12 - 7) + 14 * 7 == 50 + 98
    public void Octile_OfTwoArbitraryOffsets_MatchesTheHandComputedFormula(int deltaX, int deltaY, int expected)
    {
        Assert.Equal(expected, NavHeuristic.Octile(deltaX, deltaY));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(-5, 3)]
    [InlineData(5, -3)]
    [InlineData(-5, -3)]
    public void Octile_IgnoresTheSignOfEitherAxis_AndAlwaysReturnsTheSameCostAsThePositiveCase(int deltaX, int deltaY)
    {
        Assert.Equal(62, NavHeuristic.Octile(deltaX, deltaY));
    }

    [Fact]
    public void Octile_FromACellToItself_IsZero()
    {
        Assert.Equal(0, NavHeuristic.Octile(0, 0));
    }

    [Fact]
    public void Octile_TwoCoordinateOverload_MatchesTheDeltaOverload()
    {
        Assert.Equal(NavHeuristic.Octile(5, 3), NavHeuristic.Octile(fromX: 2, fromY: 9, toX: 7, toY: 12));
    }

    /// <summary>
    /// Admissibility: the heuristic must never overestimate the true cost of
    /// the cheapest path. The reference here is an independent Dijkstra
    /// search over a fully open 16 by 16 grid, walking the same pinned
    /// <see cref="NavNeighbors"/> offset table and the same two step costs
    /// <see cref="NavHeuristic"/> declares, so this checks the formula
    /// against a second, separately computed value rather than against
    /// itself. On a fully open grid the two values are in fact always equal
    /// — the diagonal-first strategy the octile formula describes is exactly
    /// what an unobstructed search takes — so this test pins that equality
    /// as well as the inequality admissibility requires.
    /// </summary>
    [Fact]
    public void Octile_IsAdmissibleAndExact_AgainstAReferenceDijkstraOnAFullyOpen16By16Grid()
    {
        const int size = 16;
        var trueCost = ComputeReferenceShortestPathCostsFromOrigin(size);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var heuristic = NavHeuristic.Octile(0, 0, x, y);
                var reference = trueCost[(y * size) + x];

                Assert.True(
                    heuristic <= reference,
                    $"Octile(0,0,{x},{y}) == {heuristic} overestimates the reference cost {reference}.");
                Assert.Equal(reference, heuristic);
            }
        }
    }

    /// <summary>
    /// A textbook Dijkstra over the pinned <see cref="NavNeighbors"/> offset
    /// table and <see cref="NavHeuristic"/>'s two step costs, with every cell
    /// passable. Deliberately not grid A* — that is plan task 21 and does
    /// not exist yet — so this reference is independent of the production
    /// search it will eventually stand in for. Test code only: the banned
    /// collection scan in <c>SandataSourceHygieneTests</c> covers
    /// <c>src/Sandata.Core</c>, not this test project, so <see cref="PriorityQueue{TElement,TPriority}"/>
    /// is fine here.
    /// </summary>
    private static int[] ComputeReferenceShortestPathCostsFromOrigin(int size)
    {
        var grid = new NavGrid(size, size);
        var best = new int[grid.CellCount];
        Array.Fill(best, int.MaxValue);

        var originIndex = grid.CellIndex(0, 0);
        best[originIndex] = 0;

        var frontier = new PriorityQueue<int, int>();
        frontier.Enqueue(originIndex, 0);

        while (frontier.TryDequeue(out var cellIndex, out var cost))
        {
            if (cost > best[cellIndex])
            {
                continue;
            }

            var x = grid.CellX(cellIndex);
            var y = grid.CellY(cellIndex);

            for (var direction = 0; direction < NavNeighbors.DirectionCount; direction++)
            {
                var neighbourX = x + NavNeighbors.OffsetX(direction);
                var neighbourY = y + NavNeighbors.OffsetY(direction);

                if (!grid.TryGetCellIndex(neighbourX, neighbourY, out var neighbourIndex))
                {
                    continue;
                }

                var stepCost = NavNeighbors.IsDiagonal(direction)
                    ? NavHeuristic.DiagonalStepCost
                    : NavHeuristic.OrthogonalStepCost;
                var candidate = cost + stepCost;

                if (candidate < best[neighbourIndex])
                {
                    best[neighbourIndex] = candidate;
                    frontier.Enqueue(neighbourIndex, candidate);
                }
            }
        }

        return best;
    }
}
