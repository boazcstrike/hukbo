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
}
