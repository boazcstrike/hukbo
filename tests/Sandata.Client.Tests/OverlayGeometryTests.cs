using System.Collections.Immutable;
using System.Linq;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Sandata.Client.Rendering;
using Sandata.Core.Geometry;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers task 45's done-when bar: <see cref="FireConeOverlay"/>'s boundary
/// geometry matches <see cref="VisionCone"/>'s own boundary vectors exactly
/// rather than being recomputed; every one of the three overlays
/// (<see cref="FireConeOverlay"/>, <see cref="OrderPathOverlay"/>,
/// <see cref="BreachMarkerOverlay"/>) returns non-empty geometry at the
/// lowest detail tier — tested here by driving <see cref="SandataCamera"/>
/// down to its clamped minimum zoom, the closest analogue in this file's
/// consumed surface to "the lowest detail tier" an in-world overlay renders
/// at, since none of these three types accept a detail-tier parameter at all
/// (see each type's remarks: tactical decision geometry never fades with
/// zoom the way a decorative operator layer does); and the breach marker
/// appears on exactly the one material-3 wall in the <c>angle-house</c>
/// fixture.
/// </summary>
public sealed class OverlayGeometryTests
{
    // A fixed, arbitrary viewport, matching WorldRendererGeometryTests's own choice.
    private static readonly Rectangle ContentBounds = new(0, 0, 1280, 720);

    private static ImmutableArray<MapRecord> LoadAngleHouseFixture()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory) ??
            throw new InvalidOperationException(
                "Could not find the Hukbo.slnx repository root from the test output directory.");
        var path = Path.Combine(root, "tests", "Sandata.Core.Tests", "Fixtures", "angle-house.hkmap");
        var text = File.ReadAllText(path);
        return MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(text));
    }

    private static SandataCamera CreateCamera(float zoom)
    {
        var camera = new SandataCamera(640, 720);
        camera.SetZoom(zoom);
        return camera;
    }

    // --- Fire cone: boundary geometry must come from ConeBoundaryTable, never be recomputed. ---

    [Theory]
    [InlineData(0, 8192)]
    [InlineData(20000, 4096)]
    [InlineData(50000, 16000)]
    public void FireConeEdgesAreDerivedFromConeBoundaryTableForTheSameAngles(ushort facingRaw, ushort halfWidth)
    {
        var facing = new Bam16(facingRaw);
        var apex = new Vector2(100f, 100f);
        const float rangeWu = 300f;

        var leftAngle = new Bam16(unchecked((ushort)(facing.Raw - halfWidth)));
        var rightAngle = new Bam16(unchecked((ushort)(facing.Raw + halfWidth)));

        var (leftX, leftY) = ConeBoundaryTable.BoundaryVector(leftAngle);
        var (rightX, rightY) = ConeBoundaryTable.BoundaryVector(rightAngle);
        var expectedLeftDirection = Vector2.Normalize(new Vector2(leftX, leftY));
        var expectedRightDirection = Vector2.Normalize(new Vector2(rightX, rightY));
        var expectedLeftEdge = apex + (expectedLeftDirection * rangeWu);
        var expectedRightEdge = apex + (expectedRightDirection * rangeWu);

        var geometry = FireConeOverlay.CreateWorldGeometry(apex, facing, halfWidth, rangeWu);

        Assert.Equal(expectedLeftEdge.X, geometry.LeftEdgeEnd.X, precision: 4);
        Assert.Equal(expectedLeftEdge.Y, geometry.LeftEdgeEnd.Y, precision: 4);
        Assert.Equal(expectedRightEdge.X, geometry.RightEdgeEnd.X, precision: 4);
        Assert.Equal(expectedRightEdge.Y, geometry.RightEdgeEnd.Y, precision: 4);
    }

    [Fact]
    public void FireConeGeometryRejectsNonPositiveRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FireConeOverlay.CreateWorldGeometry(Vector2.Zero, new Bam16(0), 8192, 0f));
    }

    // --- Every overlay is non-empty at the lowest detail tier (lowest zoom). ---

    [Fact]
    public void FireConeIsNonEmptyAtTheLowestZoom()
    {
        var geometry = FireConeOverlay.CreateWorldGeometry(new Vector2(100f, 100f), new Bam16(0), 8192, 300f);
        Assert.False(geometry.IsEmpty);

        var camera = CreateCamera(0f); // clamps to SandataCamera's own minimum zoom.
        var screenGeometry = FireConeOverlay.ToScreenGeometry(geometry, camera, ContentBounds);

        Assert.NotEqual(screenGeometry.Apex, screenGeometry.LeftEdgeEnd);
        Assert.NotEqual(screenGeometry.Apex, screenGeometry.RightEdgeEnd);
    }

    [Fact]
    public void OrderPathIsNonEmptyAtTheLowestZoom()
    {
        var waypoints = ImmutableArray.Create(
            new Vector2(0f, 0f),
            new Vector2(400f, 0f),
            new Vector2(400f, 400f));

        var worldSegments = OrderPathOverlay.CreateWorldSegments(waypoints);
        Assert.Equal(2, worldSegments.Length);

        var camera = CreateCamera(0f);
        var screenSegments = OrderPathOverlay.ToScreenSegments(worldSegments, camera, ContentBounds);

        Assert.Equal(2, screenSegments.Length);
        foreach (var segment in screenSegments)
        {
            Assert.NotEqual(segment.Start, segment.End);
        }

        var worldMarkers = OrderPathOverlay.CreateWaypointWorldShapes(waypoints);
        Assert.Equal(3, worldMarkers.Length);
        foreach (var marker in worldMarkers)
        {
            var screenMarker = WorldRenderer.ToScreenShape(marker, camera, ContentBounds);
            Assert.True(screenMarker.AxisAlignedBounds.Width > 0);
            Assert.True(screenMarker.AxisAlignedBounds.Height > 0);
        }
    }

    [Fact]
    public void BreachMarkersAreNonEmptyAtTheLowestZoomWhenAWallIsBreachable()
    {
        var records = LoadAngleHouseFixture();
        var worldMarkers = BreachMarkerOverlay.CreateWorldShapes(records);
        Assert.Single(worldMarkers);

        var camera = CreateCamera(0f);
        var screenMarker = WorldRenderer.ToScreenShape(worldMarkers[0], camera, ContentBounds);

        Assert.True(screenMarker.AxisAlignedBounds.Width > 0);
        Assert.True(screenMarker.AxisAlignedBounds.Height > 0);
    }

    // --- Pure-function correctness for the two path helpers. ---

    [Fact]
    public void FewerThanTwoWaypointsProducesNoSegments()
    {
        Assert.Empty(OrderPathOverlay.CreateWorldSegments(ImmutableArray<Vector2>.Empty));
        Assert.Empty(OrderPathOverlay.CreateWorldSegments(ImmutableArray.Create(new Vector2(1f, 1f))));
    }

    [Fact]
    public void NoWaypointsProducesNoMarkers()
    {
        Assert.Empty(OrderPathOverlay.CreateWaypointWorldShapes(ImmutableArray<Vector2>.Empty));
    }

    // --- Breach marker: exactly the one material-3 wall in the angle-house fixture. ---

    [Fact]
    public void BreachMarkerAppearsOnExactlyTheOneMaterial3WallInTheAngleHouseFixture()
    {
        var records = LoadAngleHouseFixture();

        // WALL 420 200 600 200 3 is the fixture's only breachable wall.
        var materialThreeWalls = records.OfType<WallRecord>().Where(w => w.Material == 3).ToList();
        Assert.Single(materialThreeWalls);

        var markers = BreachMarkerOverlay.CreateWorldShapes(records);

        Assert.Single(markers);
        Assert.Equal(WorldRenderer.DrawShapeKind.AxisAligned, markers[0].Kind);

        // Midpoint of (420,200)-(600,200) is (510,200); marker radius 12.
        Assert.Equal(new Rectangle(498, 188, 24, 24), markers[0].AxisAlignedBounds);
    }

    [Fact]
    public void NoBreachMarkersWhenNoWallIsMaterialThree()
    {
        var records = ImmutableArray.Create<MapRecord>(
            new WallRecord(1, 0, 0, 100, 0, 1),
            new WallRecord(2, 0, 0, 0, 100, 2));

        Assert.Empty(BreachMarkerOverlay.CreateWorldShapes(records));
    }
}
