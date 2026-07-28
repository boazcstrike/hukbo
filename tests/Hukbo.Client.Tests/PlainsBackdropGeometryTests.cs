using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class PlainsBackdropGeometryTests
{
    private const int DefaultMapWidth = 1_280;
    private const int DefaultMapHeight = 720;
    private const ulong DefaultSeed = 1;

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.37f)]
    [InlineData(1.0f)]
    [InlineData(1.63f)]
    [InlineData(12.0f)]
    public void GetGroundCells_ExactlyCoversTheSuppliedMapRectangle(float zoom)
    {
        var mapBounds = ProjectedMapBounds(DefaultMapWidth, DefaultMapHeight, zoom);

        var cells = PlainsBackdropGeometry.GetGroundCells(
            mapBounds,
            DefaultMapWidth,
            DefaultMapHeight,
            DefaultSeed);

        var union = Rectangle.Empty;
        foreach (var cell in cells)
        {
            union = union == Rectangle.Empty
                ? cell.Bounds
                : Rectangle.Union(union, cell.Bounds);
        }

        Assert.Equal(mapBounds, union);
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.37f)]
    [InlineData(1.0f)]
    [InlineData(1.63f)]
    [InlineData(12.0f)]
    public void GetGroundCells_NoTwoCellsOverlap(float zoom)
    {
        var mapBounds = ProjectedMapBounds(DefaultMapWidth, DefaultMapHeight, zoom);

        var cells = PlainsBackdropGeometry.GetGroundCells(
            mapBounds,
            DefaultMapWidth,
            DefaultMapHeight,
            DefaultSeed);

        long summedArea = 0;
        foreach (var cell in cells)
        {
            summedArea += (long)cell.Bounds.Width * cell.Bounds.Height;
        }

        Assert.Equal((long)mapBounds.Width * mapBounds.Height, summedArea);
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.37f)]
    [InlineData(1.0f)]
    [InlineData(1.63f)]
    [InlineData(12.0f)]
    public void GetGroundCells_AdjacentCellsShareExactBoundary(float zoom)
    {
        var mapBounds = ProjectedMapBounds(DefaultMapWidth, DefaultMapHeight, zoom);
        var (columns, rows) = PlainsBackdropGeometry.GetGridDimensions(
            DefaultMapWidth,
            DefaultMapHeight);

        var cells = PlainsBackdropGeometry.GetGroundCells(
            mapBounds,
            DefaultMapWidth,
            DefaultMapHeight,
            DefaultSeed);

        Assert.Equal(columns * rows, cells.Length);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns - 1; column++)
            {
                var left = cells[(row * columns) + column].Bounds;
                var right = cells[(row * columns) + column + 1].Bounds;
                Assert.Equal(left.Right, right.Left);
            }
        }

        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows - 1; row++)
            {
                var top = cells[(row * columns) + column].Bounds;
                var bottom = cells[((row + 1) * columns) + column].Bounds;
                Assert.Equal(top.Bottom, bottom.Top);
            }
        }
    }

    [Fact]
    public void GetGridDimensions_RespectsTheFortyEightCeilingForAVeryLargeMap()
    {
        var (columns, rows) = PlainsBackdropGeometry.GetGridDimensions(
            1_000_000,
            1_000_000);

        Assert.Equal(PlainsBackdropGeometry.MaximumGridDimension, columns);
        Assert.Equal(PlainsBackdropGeometry.MaximumGridDimension, rows);
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(0, 0)]
    public void GetGroundCells_DegenerateMapRectangleYieldsEmptyResultRatherThanThrowing(
        int mapWidth,
        int mapHeight)
    {
        var mapBounds = new Rectangle(0, 0, mapWidth, mapHeight);

        var cells = PlainsBackdropGeometry.GetGroundCells(
            mapBounds,
            mapWidth,
            mapHeight,
            DefaultSeed);

        Assert.Empty(cells);
    }

    private static Rectangle ProjectedMapBounds(
        int mapWidth,
        int mapHeight,
        float zoom) =>
        new(
            100,
            50,
            (int)MathF.Round(mapWidth * zoom),
            (int)MathF.Round(mapHeight * zoom));

    [Fact]
    public void GenerateDecals_SameSeedAndMapProducesEqualSequences()
    {
        var first = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);
        var second = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        // Compared element-wise on purpose: ImmutableArray<T> implements
        // IEquatable<ImmutableArray<T>> as reference equality of the inner
        // array, so a direct Assert.Equal would compare identity rather than
        // contents and an Assert.NotEqual would always succeed.
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public void GenerateDecals_DifferentSeedsProduceDifferentSequences()
    {
        var first = PlainsBackdropGeometry.GenerateDecals(
            1,
            DefaultMapWidth,
            DefaultMapHeight);
        var second = PlainsBackdropGeometry.GenerateDecals(
            2,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.NotEqual(first.ToArray(), second.ToArray());
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(1_000u)]
    public void GenerateDecals_NeverExceedsTheMaximumCount(ulong seed)
    {
        var decals = PlainsBackdropGeometry.GenerateDecals(
            seed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.True(decals.Length <= PlainsBackdropGeometry.MaximumDecalCount);

        var largeMapDecals = PlainsBackdropGeometry.GenerateDecals(
            seed,
            1_000_000,
            1_000_000);

        Assert.Equal(
            PlainsBackdropGeometry.MaximumDecalCount,
            largeMapDecals.Length);
    }

    [Fact]
    public void GenerateDecals_EveryPositionFallsInsideTheMapBounds()
    {
        var decals = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.NotEmpty(decals);
        foreach (var decal in decals)
        {
            Assert.InRange(decal.WorldPosition.X, 0f, DefaultMapWidth);
            Assert.InRange(decal.WorldPosition.Y, 0f, DefaultMapHeight);
        }
    }

    [Fact]
    public void GenerateDecals_AllThreeKindsAppearAcrossAReasonableSample()
    {
        var decals = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            1_000_000,
            1_000_000);

        var distinctKinds = new HashSet<PlainsDecalKind>();
        foreach (var decal in decals)
        {
            distinctKinds.Add(decal.Kind);
        }

        Assert.Equal(3, distinctKinds.Count);
    }

    [Fact]
    public void GetDecalScreenBounds_ClampsToTheMinimumApparentScaleAtLowZoom()
    {
        var anchor = new Vector2(200, 150);
        var expectedSize = Math.Max(
            1,
            (int)MathF.Round(
                PlainsBackdropGeometry.DecalBaseSize *
                PlainsBackdropGeometry.MinimumDecalApparentScale));

        var first = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: 0.05f,
            decalScaleFactor: 0.001f);
        var second = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: 0.05f,
            decalScaleFactor: 0.02f);

        Assert.Equal(expectedSize, first.Width);
        Assert.Equal(expectedSize, first.Height);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetDecalScreenBounds_ClampsToTheMaximumApparentScaleAtHighZoom()
    {
        var anchor = new Vector2(200, 150);
        var expectedSize = Math.Max(
            1,
            (int)MathF.Round(
                PlainsBackdropGeometry.DecalBaseSize *
                PlainsBackdropGeometry.MaximumDecalApparentScale));

        var first = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: 12f,
            decalScaleFactor: 50f);
        var second = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            cameraZoom: 12f,
            decalScaleFactor: 500f);

        Assert.Equal(expectedSize, first.Width);
        Assert.Equal(expectedSize, first.Height);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetDecalScreenBounds_ApparentScaleIsMonotonicNonDecreasingInZoom()
    {
        var anchor = new Vector2(50, 50);
        float[] zoomValues = [0.05f, 0.37f, 1f, 1.63f, 12f];
        var previousWidth = 0;

        foreach (var zoom in zoomValues)
        {
            var bounds = PlainsBackdropGeometry.GetDecalScreenBounds(
                anchor,
                zoom,
                decalScaleFactor: 1f);

            Assert.True(bounds.Width >= previousWidth);
            previousWidth = bounds.Width;
        }
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.37f)]
    [InlineData(1f)]
    [InlineData(1.63f)]
    [InlineData(12f)]
    public void GetDecalScreenBounds_IsCenteredOnTheAnchorWithPositiveSize(
        float zoom)
    {
        var anchor = new Vector2(400, 250);

        var bounds = PlainsBackdropGeometry.GetDecalScreenBounds(
            anchor,
            zoom,
            decalScaleFactor: 1f);

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
        Assert.True(bounds.Contains(new Point(
            (int)MathF.Round(anchor.X),
            (int)MathF.Round(anchor.Y))));
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.37f)]
    [InlineData(1.0f)]
    [InlineData(1.63f)]
    [InlineData(12.0f)]
    public void GetGroundCell_MatchesTheCorrespondingGetGroundCellsEntry(
        float zoom)
    {
        var mapBounds = ProjectedMapBounds(DefaultMapWidth, DefaultMapHeight, zoom);
        var (columns, rows) = PlainsBackdropGeometry.GetGridDimensions(
            DefaultMapWidth,
            DefaultMapHeight);
        var cells = PlainsBackdropGeometry.GetGroundCells(
            mapBounds,
            DefaultMapWidth,
            DefaultMapHeight,
            DefaultSeed);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var single = PlainsBackdropGeometry.GetGroundCell(
                    mapBounds,
                    columns,
                    rows,
                    column,
                    row,
                    DefaultSeed);

                Assert.Equal(cells[(row * columns) + column], single);
            }
        }
    }

    [Fact]
    public void GroundShadeInterpolationCountMatchesGroundShadeCount()
    {
        Assert.Equal(
            PlainsBackdropGeometry.GroundShadeCount,
            PlainsBackdropGeometry.GroundShadeInterpolation.Length);
    }

    [Fact]
    public void DecalKindInterpolationCoversEveryDecalKind()
    {
        Assert.Equal(
            Enum.GetValues<PlainsDecalKind>().Length,
            PlainsBackdropGeometry.DecalKindInterpolation.Length);
    }

    [Fact]
    public void EveryBackdropInterpolationStaysWithinTheBoundedRange()
    {
        foreach (var interpolation in PlainsBackdropGeometry
            .GroundShadeInterpolation
            .Concat(PlainsBackdropGeometry.DecalKindInterpolation))
        {
            Assert.InRange(
                interpolation,
                0f,
                PlainsBackdropGeometry.MaximumBackdropInterpolation);
        }
    }

    // --- VIS-027: correlated (corner-lattice) ground shading ---

    [Theory]
    [InlineData(0, 0, 1u)]
    [InlineData(5, 3, 1u)]
    [InlineData(10, 10, 1u)]
    [InlineData(10, 10, 2u)]
    [InlineData(11, 10, 1u)]
    [InlineData(47, 47, 12_345u)]
    public void GetCellShadeIndex_IsDeterministicForTheSameColumnRowAndSeed(
        int column,
        int row,
        ulong seed)
    {
        var first = PlainsBackdropGeometry.GetCellShadeIndex(column, row, seed);
        var second = PlainsBackdropGeometry.GetCellShadeIndex(column, row, seed);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0, 0, 1u)]
    [InlineData(5, 3, 1u)]
    [InlineData(10, 10, 1u)]
    [InlineData(10, 10, 2u)]
    [InlineData(11, 10, 1u)]
    [InlineData(47, 47, 12_345u)]
    [InlineData(0, 0, 999u)]
    [InlineData(1, 0, 999u)]
    [InlineData(0, 1, 999u)]
    public void GetCellShadeIndex_StaysWithinTheGroundShadeIndexRange(
        int column,
        int row,
        ulong seed)
    {
        var shadeIndex = PlainsBackdropGeometry.GetCellShadeIndex(column, row, seed);

        Assert.InRange(shadeIndex, 0, PlainsBackdropGeometry.GroundShadeCount - 1);
    }

    // Corner-averaging formula pinned on known values. Each expected index is
    // independently computed from the documented formula — hash the four
    // lattice corners (column, row), (column + 1, row), (column, row + 1),
    // (column + 1, row + 1) under GroundCornerLatticeSalt via SplitMix64,
    // average the four unit-interval results, then floor-and-clamp against
    // GroundShadeCount — so a change to the mixing constants, the corner
    // selection, or the averaging step will move at least one of these
    // pinned indices.
    [Theory]
    [InlineData(0, 0, 1u, 0)]
    [InlineData(5, 3, 1u, 1)]
    [InlineData(47, 47, 12_345u, 1)]
    [InlineData(10, 10, 1u, 0)]
    [InlineData(10, 10, 2u, 1)]
    [InlineData(11, 10, 1u, 0)]
    [InlineData(0, 0, 999u, 0)]
    [InlineData(1, 0, 999u, 1)]
    [InlineData(0, 1, 999u, 0)]
    public void GetCellShadeIndex_CornerAveragingFormulaMatchesPinnedValues(
        int column,
        int row,
        ulong seed,
        int expectedShadeIndex)
    {
        var shadeIndex = PlainsBackdropGeometry.GetCellShadeIndex(column, row, seed);

        Assert.Equal(expectedShadeIndex, shadeIndex);
    }

    [Fact]
    public void GetCellShadeIndex_HorizontallyAdjacentCellsShareTwoLatticeCorners()
    {
        // Cell (0, row) and cell (1, row) share the lattice corners at
        // column 1 (the first cell's right edge is the second cell's left
        // edge), so feeding column 1's corner values into both cells must
        // reproduce the same shade the shared-corner formula pins above:
        // GetCellShadeIndex(1, 0, 999) folds in exactly the two corner
        // values — (1, 0) and (1, 1) — that GetCellShadeIndex(0, 0, 999)
        // already computed as its right-hand corners. Re-deriving cell (1,
        // 0)'s index independently and comparing to the pinned value is the
        // regression guard that the shared-corner correlation, not an
        // independent per-cell hash, is what actually executes.
        var leftCell = PlainsBackdropGeometry.GetCellShadeIndex(0, 0, 999);
        var rightCell = PlainsBackdropGeometry.GetCellShadeIndex(1, 0, 999);

        Assert.Equal(0, leftCell);
        Assert.Equal(1, rightCell);
    }

    [Fact]
    public void GenerateDecals_PlacementIsUnchangedUnderTheOldPlainsBackdropSalt()
    {
        // Regression pin: introducing GroundCornerLatticeSalt for ground
        // shading must not shift the existing decal stream, which stays on
        // the original PlainsBackdropSalt-equivalent salt. Values computed
        // independently from the documented SplitMix64 formula and the
        // existing decal-generation constants (seed 1, 1280x720 map).
        var decals = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.Equal(153, decals.Length);

        var first = decals[0];
        Assert.Equal(1192.169f, first.WorldPosition.X, 1);
        Assert.Equal(120.156f, first.WorldPosition.Y, 1);
        Assert.Equal(1.00471f, first.ScaleFactor, 3);
        Assert.Equal(PlainsDecalKind.Rock, first.Kind);

        var last = decals[decals.Length - 1];
        Assert.Equal(699.595f, last.WorldPosition.X, 1);
        Assert.Equal(670.573f, last.WorldPosition.Y, 1);
        Assert.Equal(0.95057f, last.ScaleFactor, 3);
        Assert.Equal(PlainsDecalKind.GrassTuft, last.Kind);
    }
}
