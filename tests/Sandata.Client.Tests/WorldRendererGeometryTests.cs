using System.Collections.Immutable;
using System.Linq;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Sandata.Client.Rendering;
using Sandata.Core.Maps;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers task 33's done-when bar: <see cref="WorldRenderer"/>'s pure
/// geometry helpers for walls, doors, cover objects, and objectives — all
/// four record kinds task 33's own scope names — pinned against the
/// <c>angle-house</c> fixture (design section 12,
/// <c>tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap</c>) at three zoom
/// levels, plus the requirement that a non-orthogonal wall (the
/// fixture's 26.57-degree <c>WALL 120 120 320 220 2</c>) produces a
/// <see cref="WorldRenderer.DrawShapeKind.Rotated"/> block rather than an
/// axis-aligned one.
/// </summary>
/// <remarks>
/// No member of this class constructs a <c>GraphicsDevice</c>, a
/// <c>SpriteBatch</c>, or a <see cref="SandataGame"/> — only
/// <see cref="SandataCamera"/> (a plain, GPU-free presentation-state object)
/// and the record types <c>Sandata.Core.Maps</c> already parses without a
/// window. Every expected <see cref="Rectangle"/> below was derived by hand
/// from <see cref="SandataCamera.WorldToScreen"/>'s own formula, not copied
/// from a first failing run, because the world-unit inputs (wall thickness 8,
/// door thickness 4, the fixture's own coordinates, and zoom levels
/// 0.5/1.0/2.0 against a fixed content-bounds center) were chosen precisely
/// so every axis-aligned result lands on an exact integer with no rounding
/// ambiguity to hide a wrong formula behind.
/// </remarks>
public sealed class WorldRendererGeometryTests
{
    // A fixed, arbitrary viewport the tests hold constant across zoom levels.
    // Its center, (640, 360), is the screen point the camera's own center in
    // world space maps to.
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

    /// <summary>
    /// A fresh, un-fitted camera over the fixture's 640x720 world (its own
    /// <c>GRID</c> record). <see cref="SandataCamera.Fit"/> is deliberately
    /// never called, so <see cref="SandataCamera.Center"/> stays at its
    /// constructor default, the map center (320, 360) — the second input
    /// every expected value below was derived from.
    /// </summary>
    private static SandataCamera CreateCamera(float zoom)
    {
        var camera = new SandataCamera(640, 720);
        camera.SetZoom(zoom);
        return camera;
    }

    private static WallRecord FindWall(ImmutableArray<MapRecord> records, int x1, int y1, int x2, int y2) =>
        records.OfType<WallRecord>()
            .Single(w => w.X1 == x1 && w.Y1 == y1 && w.X2 == x2 && w.Y2 == y2);

    private static DoorRecord FindDoor(ImmutableArray<MapRecord> records, int x1, int y1, int x2, int y2) =>
        records.OfType<DoorRecord>()
            .Single(d => d.X1 == x1 && d.Y1 == y1 && d.X2 == x2 && d.Y2 == y2);

    private static CoverRecord FindCover(ImmutableArray<MapRecord> records, int minX, int minY) =>
        records.OfType<CoverRecord>()
            .Single(c => c.MinX == minX && c.MinY == minY);

    private static ObjectiveRecord FindObjective(ImmutableArray<MapRecord> records, int index) =>
        records.OfType<ObjectiveRecord>()
            .Single(o => o.Index == index);

    [Theory]
    [InlineData(0.5f, 478, 180, 4, 360)]
    [InlineData(1.0f, 316, 0, 8, 720)]
    [InlineData(2.0f, -8, -360, 16, 1440)]
    public void VerticalOuterWallProducesThePinnedAxisAlignedRectangle(
        float zoom, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var records = LoadAngleHouseFixture();
        var wall = FindWall(records, 0, 0, 0, 720);
        var camera = CreateCamera(zoom);

        var worldShape = WorldRenderer.CreateWallWorldShape(wall);
        var screenShape = WorldRenderer.ToScreenShape(worldShape, camera, ContentBounds);

        Assert.Equal(WorldRenderer.DrawShapeKind.AxisAligned, screenShape.Kind);
        Assert.Equal(
            new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight),
            screenShape.AxisAlignedBounds);
    }

    [Theory]
    [InlineData(0.5f, 630, 499, 20, 2)]
    [InlineData(1.0f, 620, 638, 40, 4)]
    [InlineData(2.0f, 600, 916, 80, 8)]
    public void HorizontalDoorProducesThePinnedAxisAlignedRectangle(
        float zoom, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var records = LoadAngleHouseFixture();
        var door = FindDoor(records, 300, 640, 340, 640);
        var camera = CreateCamera(zoom);

        var worldShape = WorldRenderer.CreateDoorWorldShape(door);
        var screenShape = WorldRenderer.ToScreenShape(worldShape, camera, ContentBounds);

        Assert.Equal(WorldRenderer.DrawShapeKind.AxisAligned, screenShape.Kind);
        Assert.Equal(
            new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight),
            screenShape.AxisAlignedBounds);
    }

    [Theory]
    [InlineData(0.5f, 580, 280, 30, 20)]
    [InlineData(1.0f, 520, 200, 60, 40)]
    [InlineData(2.0f, 400, 40, 120, 80)]
    public void CoverBoxProducesThePinnedAxisAlignedRectangle(
        float zoom, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var records = LoadAngleHouseFixture();
        var cover = FindCover(records, 200, 200);
        var camera = CreateCamera(zoom);

        var worldShape = WorldRenderer.CreateCoverWorldShape(cover);
        var screenShape = WorldRenderer.ToScreenShape(worldShape, camera, ContentBounds);

        Assert.Equal(WorldRenderer.DrawShapeKind.AxisAligned, screenShape.Kind);
        Assert.Equal(
            new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight),
            screenShape.AxisAlignedBounds);
    }

    [Theory]
    [InlineData(0.5f, 706, 216, 48, 48)]
    [InlineData(1.0f, 772, 72, 96, 96)]
    [InlineData(2.0f, 904, -216, 192, 192)]
    public void ObjectivePointProducesThePinnedAxisAlignedRectangle(
        float zoom, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var records = LoadAngleHouseFixture();
        var objective = FindObjective(records, 0);
        var camera = CreateCamera(zoom);

        var worldShape = WorldRenderer.CreateObjectiveWorldShape(objective);
        var screenShape = WorldRenderer.ToScreenShape(worldShape, camera, ContentBounds);

        Assert.Equal(WorldRenderer.DrawShapeKind.AxisAligned, screenShape.Kind);
        Assert.Equal(
            new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight),
            screenShape.AxisAlignedBounds);
    }

    /// <summary>
    /// The fixture's <c>WALL 120 120 320 220 2</c> is neither vertical nor
    /// horizontal (dx=200, dy=100), so it must come out as a
    /// <see cref="WorldRenderer.DrawShapeKind.Rotated"/> block — never an
    /// axis-aligned rectangle padded around its bounding box, which would
    /// silently widen the drawn wall and misrepresent where it actually
    /// blocks movement and fire. Its rotation is <c>atan2(100, 200)</c>,
    /// 26.565 degrees, the angle this test's name and the plan's done-when
    /// criterion both cite as "26.57 degrees".
    /// </summary>
    [Theory]
    [InlineData(0.5f, 590f, 265f, 111.803398875f, 4f)]
    [InlineData(1.0f, 540f, 170f, 223.60679775f, 8f)]
    [InlineData(2.0f, 440f, -20f, 447.2135955f, 16f)]
    public void DiagonalWallProducesARotatedBlockNotAnAxisAlignedRectangle(
        float zoom, float expectedX, float expectedY, float expectedLength, float expectedThickness)
    {
        var records = LoadAngleHouseFixture();
        var wall = FindWall(records, 120, 120, 320, 220);
        var camera = CreateCamera(zoom);

        var worldShape = WorldRenderer.CreateWallWorldShape(wall);
        var screenShape = WorldRenderer.ToScreenShape(worldShape, camera, ContentBounds);

        Assert.Equal(WorldRenderer.DrawShapeKind.Rotated, screenShape.Kind);
        Assert.Equal(expectedX, screenShape.RotatedCenter.X, precision: 3);
        Assert.Equal(expectedY, screenShape.RotatedCenter.Y, precision: 3);
        Assert.Equal(expectedLength, screenShape.RotatedLength, precision: 3);
        Assert.Equal(expectedThickness, screenShape.RotatedThickness, precision: 3);
        Assert.Equal(MathF.Atan2(100f, 200f), screenShape.RotationRadians, precision: 6);
        Assert.NotEqual(0f, screenShape.RotationRadians);
    }

    [Fact]
    public void AxisAlignedOuterWallIsNeverReportedAsRotated()
    {
        var records = LoadAngleHouseFixture();
        var wall = FindWall(records, 0, 0, 0, 720);

        var worldShape = WorldRenderer.CreateWallWorldShape(wall);

        Assert.Equal(WorldRenderer.DrawShapeKind.AxisAligned, worldShape.Kind);
    }
}
