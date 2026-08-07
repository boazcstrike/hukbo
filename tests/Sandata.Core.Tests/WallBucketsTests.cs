using Sandata.Core.Geometry;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 75's test bar: a wall whose endpoint sits exactly on the map's
/// outer edge — the ordinary shape of a perimeter wall, per design section
/// 12's closed-interval validation — must not make
/// <see cref="WallBuckets.Build"/> throw. Before this fix,
/// <c>GridRay.Traverse</c> threw <see cref="ArgumentOutOfRangeException"/>
/// for exactly this shape, because the boundary world-unit value floors to
/// the cell one past the grid's last row or column
/// (<see cref="NavGrid.WorldToCellCoordinate"/>), and that method requires
/// its origin cell to lie strictly inside the grid.
/// </summary>
public sealed class WallBucketsTests
{
    private static string LoadAngleHouseFixtureText()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "angle-house.hkmap");

        Assert.True(
            File.Exists(path),
            $"The angle-house fixture is missing at '{path}'. It is committed under " +
            "tests/Sandata.Core.Tests/Fixtures and copied to the output directory by " +
            "the project's Fixtures item.");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// The regression this task exists to fix: <c>angle-house.hkmap</c>'s
    /// own four perimeter walls (<c>WALL 0 0 0 720 1</c>,
    /// <c>WALL 0 0 640 0 1</c>, <c>WALL 0 720 640 720 1</c>, and
    /// <c>WALL 640 0 640 720 1</c>) each have an endpoint sitting exactly on
    /// the map's outer edge. Building the wall bucket index from the real
    /// fixture — the same call <c>Sandata.Client.SandataGame</c> makes on
    /// startup — must succeed rather than throw.
    /// </summary>
    [Fact]
    public void BuildingFromTheRealAngleHouseFixture_Succeeds()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadAngleHouseFixtureText()));
        var gridRecord = canonical.OfType<GridRecord>().Single();
        var grid = new NavGrid(gridRecord.WidthWu / gridRecord.CellWu, gridRecord.HeightWu / gridRecord.CellWu);
        var walls = canonical.OfType<WallRecord>().ToArray();

        var segmentAX = walls.Select(w => (long)w.X1).ToArray();
        var segmentAY = walls.Select(w => (long)w.Y1).ToArray();
        var segmentBX = walls.Select(w => (long)w.X2).ToArray();
        var segmentBY = walls.Select(w => (long)w.Y2).ToArray();

        var wallBuckets = WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);

        Assert.Equal(walls.Length, wallBuckets.SegmentCount);
    }

    /// <summary>
    /// One wall on each of the grid's four boundaries — x = 0, y = 0,
    /// x = widthWu, y = heightWu — all handled in a single build. Four
    /// separate assertions rather than one, because a fix that only clamps
    /// one axis (for example X but not Y) would still throw for the
    /// boundary this test would otherwise miss.
    /// </summary>
    [Fact]
    public void AWallOnEachOfTheFourBoundaries_AllHandledWithoutThrowing()
    {
        // 4 by 4 cells, 4 wu per cell: widthWu = heightWu = 16.
        var grid = new NavGrid(width: 4, height: 4);

        // Left edge (x = 0), top edge (y = 0), bottom edge (y = 16 == heightWu),
        // right edge (x = 16 == widthWu) — one short wall segment per side.
        long[] segmentAX = [0, 2, 2, 16];
        long[] segmentAY = [2, 0, 16, 2];
        long[] segmentBX = [0, 6, 6, 16];
        long[] segmentBY = [6, 0, 16, 6];

        var wallBuckets = WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);

        Assert.Equal(4, wallBuckets.SegmentCount);

        // Every stored endpoint is the true, unclamped value passed in.
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(segmentAX[i], wallBuckets.SegmentAX(i));
            Assert.Equal(segmentAY[i], wallBuckets.SegmentAY(i));
            Assert.Equal(segmentBX[i], wallBuckets.SegmentBX(i));
            Assert.Equal(segmentBY[i], wallBuckets.SegmentBY(i));
        }

        // Each boundary wall was actually bucketed into the interior cell
        // it touches, not silently dropped.
        Assert.Contains(0, wallBuckets.SegmentsInCell(grid.CellIndex(0, 0)).ToArray());
        Assert.Contains(1, wallBuckets.SegmentsInCell(grid.CellIndex(0, 0)).ToArray());
        Assert.Contains(2, wallBuckets.SegmentsInCell(grid.CellIndex(0, 3)).ToArray());
        Assert.Contains(3, wallBuckets.SegmentsInCell(grid.CellIndex(3, 0)).ToArray());
    }

    /// <summary>
    /// A segment whose both endpoints lie strictly outside the grid's
    /// world-unit bounds — not a boundary wall grazing the edge, but
    /// geometry that never reaches this grid at all — is explicitly
    /// rejected rather than silently clamped into a false interior touch.
    /// </summary>
    [Fact]
    public void ASegmentWithBothEndpointsOutsideTheGrid_IsExplicitlyRejected()
    {
        var grid = new NavGrid(width: 4, height: 4); // widthWu = heightWu = 16

        long[] segmentAX = [100];
        long[] segmentAY = [100];
        long[] segmentBX = [200];
        long[] segmentBY = [200];

        var exception = Assert.Throws<ArgumentException>(
            () => WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY));

        Assert.Contains("both endpoints outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("segment 0", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A segment with only ONE endpoint outside the grid — the other still
    /// legitimately inside or on the boundary — is not rejected. Only "both
    /// endpoints outside" is the rejection rule; this pins the boundary of
    /// that rule against a fix that rejects too aggressively.
    /// </summary>
    [Fact]
    public void ASegmentWithOnlyOneEndpointOutsideTheGrid_IsNotRejected()
    {
        var grid = new NavGrid(width: 4, height: 4); // widthWu = heightWu = 16

        long[] segmentAX = [8];
        long[] segmentAY = [8];
        long[] segmentBX = [200];
        long[] segmentBY = [200];

        var wallBuckets = WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);

        Assert.Equal(1, wallBuckets.SegmentCount);
        Assert.Equal(200, wallBuckets.SegmentBX(0));
        Assert.Equal(200, wallBuckets.SegmentBY(0));
    }

    /// <summary>
    /// Proves the exact narrow phase still receives the segment's true,
    /// unclamped coordinates — never the interior-clamped value the broad
    /// phase alone needs. The wall's B endpoint sits exactly on the grid's
    /// right edge (x = 16 == widthWu); a query that grazes that true point
    /// but never comes near x = 15 (the value <see cref="WallBuckets.Build"/>
    /// clamps to internally for <see cref="GridRay.Traverse"/> alone) is
    /// classified as touching the wall only if the stored geometry is the
    /// real, unclamped endpoint. A fix that instead clamped the *stored*
    /// coordinate would pass the boundary-handling tests above and fail this
    /// one.
    /// </summary>
    [Fact]
    public void NarrowPhaseClassification_UsesTheSegmentsTrueUnclampedCoordinates()
    {
        var grid = new NavGrid(width: 4, height: 4); // widthWu = heightWu = 16
        const long wallAX = 16, wallAY = 0, wallBX = 16, wallBY = 4;

        var wallBuckets = WallBuckets.Build(grid, [wallAX], [wallAY], [wallBX], [wallBY]);

        // The stored geometry is exactly what was passed in - unclamped.
        Assert.Equal(wallAX, wallBuckets.SegmentAX(0));
        Assert.Equal(wallAY, wallBuckets.SegmentAY(0));
        Assert.Equal(wallBX, wallBuckets.SegmentBX(0));
        Assert.Equal(wallBY, wallBuckets.SegmentBY(0));

        // A query segment that touches the wall's TRUE endpoint (16, 0) but
        // whose own coordinates never cross x = 15 -- the value the broad
        // phase alone would have clamped the wall's own X to.
        var relationAgainstStoredGeometry = ExactPredicates.ClassifySegments(
            16, 0, 20, 0,
            wallBuckets.SegmentAX(0), wallBuckets.SegmentAY(0),
            wallBuckets.SegmentBX(0), wallBuckets.SegmentBY(0));

        Assert.NotEqual(SegmentRelation.Disjoint, relationAgainstStoredGeometry);

        // Discriminating control: had the wall's own STORED coordinate been
        // clamped to x = 15 instead of kept at its true value of 16, the
        // identical query would report no relation at all. This is what
        // proves the test above is not vacuously true.
        var relationAgainstWronglyClampedGeometry = ExactPredicates.ClassifySegments(
            16, 0, 20, 0,
            15, wallAY, 15, wallBY);

        Assert.Equal(SegmentRelation.Disjoint, relationAgainstWronglyClampedGeometry);
    }

    /// <summary>
    /// The line-of-sight results for every interior query pair
    /// <see cref="LineOfSightTests"/> already pins must not move. None of
    /// those fixtures put a wall endpoint on a grid boundary, so this is a
    /// deliberate no-op check: the fix touches only how boundary-adjacent
    /// coordinates reach <see cref="GridRay.Traverse"/>, and every interior
    /// case must classify exactly as it did before.
    /// </summary>
    [Fact]
    public void InteriorLineOfSightResultsAreUnchanged()
    {
        var grid = new NavGrid(width: 8, height: 8);
        long[] wallAX = [5];
        long[] wallAY = [0];
        long[] wallBX = [5];
        long[] wallBY = [8];
        var wallBuckets = WallBuckets.Build(grid, wallAX, wallAY, wallBX, wallBY);

        Assert.False(LineOfSight.IsVisible(originX: 0, originY: 4, targetX: 10, targetY: 4, grid, wallBuckets));
        Assert.Equal(0, LineOfSight.FirstBlockingSegment(originX: 0, originY: 4, targetX: 10, targetY: 4, grid, wallBuckets));
    }
}
