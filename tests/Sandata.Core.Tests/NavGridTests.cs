using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

public sealed class NavGridTests
{
    [Theory]
    [InlineData(0, 0, 0)] // top-left corner
    [InlineData(159, 0, 159)] // top-right corner: (Width - 1, 0)
    [InlineData(0, 179, 28640)] // bottom-left corner: (Height - 1) * Width == 179 * 160
    [InlineData(159, 179, 28799)] // bottom-right corner: (Height * Width) - 1 == (180 * 160) - 1
    public void CellIndex_RoundTripsThroughCellXAndCellY_AtAllFourCorners(int x, int y, int expectedIndex)
    {
        // A 160 by 180 grid: the angle-house fixture's own dimensions
        // (design section 12's worked example, 640 by 720 wu on a 4 wu cell).
        var grid = new NavGrid(width: 160, height: 180);

        var index = grid.CellIndex(x, y);

        Assert.Equal(expectedIndex, index);
        Assert.Equal(x, grid.CellX(index));
        Assert.Equal(y, grid.CellY(index));
    }

    [Fact]
    public void CellIndex_MatchesTheDefiningFormula_YTimesWidthPlusX()
    {
        var grid = new NavGrid(width: 5, height: 7);

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                Assert.Equal((y * grid.Width) + x, grid.CellIndex(x, y));
            }
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(5, 0)] // Width is 5, so 5 is one past the last valid column
    [InlineData(0, 7)] // Height is 7, so 7 is one past the last valid row
    public void CellIndex_ThrowsOutOfRange_ForACoordinateOutsideTheGrid(int x, int y)
    {
        var grid = new NavGrid(width: 5, height: 7);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.CellIndex(x, y));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(5, 0)]
    [InlineData(0, 7)]
    public void TryGetCellIndex_ReturnsFalseAndNegativeOne_ForACoordinateOutsideTheGrid(int x, int y)
    {
        var grid = new NavGrid(width: 5, height: 7);

        var found = grid.TryGetCellIndex(x, y, out var cellIndex);

        Assert.False(found);
        Assert.Equal(-1, cellIndex);
    }

    [Fact]
    public void TryGetCellIndex_ReturnsTrueAndTheSameIndexAsCellIndex_ForAnInBoundsCoordinate()
    {
        var grid = new NavGrid(width: 5, height: 7);

        var found = grid.TryGetCellIndex(3, 4, out var cellIndex);

        Assert.True(found);
        Assert.Equal(grid.CellIndex(3, 4), cellIndex);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(-1, 0, true)]
    [InlineData(0, -1, true)]
    [InlineData(4, 6, false)] // last valid cell of a 5x7 grid
    [InlineData(5, 6, true)] // one past the last valid column
    [InlineData(4, 7, true)] // one past the last valid row
    public void ConstructorRejectsNonPositiveAndAcceptsBoundaryDimensions(int x, int y, bool expectedOutOfBounds)
    {
        var grid = new NavGrid(width: 5, height: 7);

        Assert.Equal(!expectedOutOfBounds, grid.IsInBounds(x, y));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(513)]
    public void Constructor_RejectsAWidthOutsideOneToMaxDimensionCells(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavGrid(width, height: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(513)]
    public void Constructor_RejectsAHeightOutsideOneToMaxDimensionCells(int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavGrid(width: 1, height));
    }

    [Fact]
    public void Constructor_AcceptsTheMaximumSupportedGrid_512By512()
    {
        var grid = new NavGrid(NavGrid.MaxDimensionCells, NavGrid.MaxDimensionCells);

        Assert.Equal(512, grid.Width);
        Assert.Equal(512, grid.Height);
        Assert.Equal(262_144, grid.CellCount);
    }

    // WorldToCellCoordinate is a shift, not a division, so this table is
    // hand-computed from floor(worldUnits / 4) rather than read back from the
    // implementation, including negative inputs to prove it floors rather
    // than truncates even though map-space coordinates are never negative
    // (design section 12).
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(639, 159)] // the angle-house fixture's east wall, one cell short of the edge
    [InlineData(640, 160)] // the angle-house fixture's east wall itself
    [InlineData(-1, -1)] // floor(-1 / 4) == -1, not 0
    [InlineData(-4, -1)]
    [InlineData(-5, -2)]
    [InlineData(-8, -2)]
    public void WorldToCellCoordinate_MatchesTheHandComputedFloorDivisionByFour(long worldUnits, int expectedCell)
    {
        Assert.Equal(expectedCell, NavGrid.WorldToCellCoordinate(worldUnits));
    }

    // The pinned order from design section 7: east, south-east, south,
    // south-west, west, north-west, north, north-east. Asserted element by
    // element against a hand-written table rather than against a loop that
    // could hide a transposition.
    [Fact]
    public void NavNeighbors_OffsetsMatchThePinnedOrder_ElementByElement()
    {
        Assert.Equal(1, NavNeighbors.OffsetX(NavNeighbors.East));
        Assert.Equal(0, NavNeighbors.OffsetY(NavNeighbors.East));

        Assert.Equal(1, NavNeighbors.OffsetX(NavNeighbors.SouthEast));
        Assert.Equal(1, NavNeighbors.OffsetY(NavNeighbors.SouthEast));

        Assert.Equal(0, NavNeighbors.OffsetX(NavNeighbors.South));
        Assert.Equal(1, NavNeighbors.OffsetY(NavNeighbors.South));

        Assert.Equal(-1, NavNeighbors.OffsetX(NavNeighbors.SouthWest));
        Assert.Equal(1, NavNeighbors.OffsetY(NavNeighbors.SouthWest));

        Assert.Equal(-1, NavNeighbors.OffsetX(NavNeighbors.West));
        Assert.Equal(0, NavNeighbors.OffsetY(NavNeighbors.West));

        Assert.Equal(-1, NavNeighbors.OffsetX(NavNeighbors.NorthWest));
        Assert.Equal(-1, NavNeighbors.OffsetY(NavNeighbors.NorthWest));

        Assert.Equal(0, NavNeighbors.OffsetX(NavNeighbors.North));
        Assert.Equal(-1, NavNeighbors.OffsetY(NavNeighbors.North));

        Assert.Equal(1, NavNeighbors.OffsetX(NavNeighbors.NorthEast));
        Assert.Equal(-1, NavNeighbors.OffsetY(NavNeighbors.NorthEast));
    }

    [Fact]
    public void NavNeighbors_DirectionConstants_AreDenseFromZeroInThePinnedOrder()
    {
        Assert.Equal(
            [0, 1, 2, 3, 4, 5, 6, 7],
            new[]
            {
                NavNeighbors.East, NavNeighbors.SouthEast, NavNeighbors.South,
                NavNeighbors.SouthWest, NavNeighbors.West, NavNeighbors.NorthWest,
                NavNeighbors.North, NavNeighbors.NorthEast,
            });
    }

    [Theory]
    [InlineData(NavNeighbors.East, false)]
    [InlineData(NavNeighbors.SouthEast, true)]
    [InlineData(NavNeighbors.South, false)]
    [InlineData(NavNeighbors.SouthWest, true)]
    [InlineData(NavNeighbors.West, false)]
    [InlineData(NavNeighbors.NorthWest, true)]
    [InlineData(NavNeighbors.North, false)]
    [InlineData(NavNeighbors.NorthEast, true)]
    public void NavNeighbors_IsDiagonal_MatchesTheOddIndexParityRule(int direction, bool expectedDiagonal)
    {
        Assert.Equal(expectedDiagonal, NavNeighbors.IsDiagonal(direction));
    }

    [Theory]
    [InlineData(NavNeighbors.SouthEast, NavNeighbors.East, NavNeighbors.South)]
    [InlineData(NavNeighbors.SouthWest, NavNeighbors.South, NavNeighbors.West)]
    [InlineData(NavNeighbors.NorthWest, NavNeighbors.West, NavNeighbors.North)]
    [InlineData(NavNeighbors.NorthEast, NavNeighbors.North, NavNeighbors.East)]
    public void NavNeighbors_OrthogonalComponentsOf_NamesTheFlankingOrthogonalPair(
        int diagonalDirection, int expectedFirst, int expectedSecond)
    {
        var (first, second) = NavNeighbors.OrthogonalComponentsOf(diagonalDirection);

        Assert.Equal(expectedFirst, first);
        Assert.Equal(expectedSecond, second);
    }

    [Theory]
    [InlineData(NavNeighbors.East)]
    [InlineData(NavNeighbors.South)]
    [InlineData(NavNeighbors.West)]
    [InlineData(NavNeighbors.North)]
    public void NavNeighbors_OrthogonalComponentsOf_RejectsAnOrthogonalDirection(int orthogonalDirection)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NavNeighbors.OrthogonalComponentsOf(orthogonalDirection));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void NavNeighbors_IsDiagonalMoveBlocked_RejectsTheDiagonalWhenEitherOrthogonalNeighbourIsBlocked(
        bool firstBlocked, bool secondBlocked, bool expectedBlocked)
    {
        Assert.Equal(expectedBlocked, NavNeighbors.IsDiagonalMoveBlocked(firstBlocked, secondBlocked));
    }
}
