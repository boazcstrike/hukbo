using Microsoft.Xna.Framework;
using Sandata.Core.Maps;

namespace Sandata.Client.Rendering;

/// <summary>
/// Pure geometry helpers that turn parsed `.hkmap` records (design section 12)
/// into draw shapes, first in world units and then, after one
/// <see cref="SandataCamera"/> pass, in screen pixels.
/// </summary>
/// <remarks>
/// <para>
/// Every member on this type is a plain function of plain values: it takes a
/// <see cref="WallRecord"/>, a <see cref="DoorRecord"/>, a
/// <see cref="CoverRecord"/>, an <see cref="ObjectiveRecord"/>, a
/// <see cref="SandataCamera"/>, or a <see cref="Rectangle"/>, and returns a
/// <see cref="DrawShape"/> — no <c>GraphicsDevice</c>, no <c>SpriteBatch</c>,
/// no window. That is deliberate: it is what lets
/// <c>tests/Sandata.Client.Tests/WorldRendererGeometryTests.cs</c> pin exact
/// pixel rectangles for the <c>angle-house</c> fixture without ever
/// constructing a graphics device, matching the pure-helper/impure-Draw split
/// the <c>hukbo-client-ui</c> skill documents for <c>Hukbo.Client</c>. The
/// actual <c>spriteBatch.Draw</c> calls that paint a <see cref="DrawShape"/>
/// live on <c>SandataGame</c>, are not unit tested, and make no decision this
/// type has not already made.
/// </para>
/// <para>
/// <b>Theme role reuse (provisional).</b> Sandata's 39-role
/// <c>SandataThemeColors</c> (task 13) has no dedicated "wall", "door", or
/// "objective" role. Until a later task adds one, <c>SandataGame</c> paints
/// walls with <c>ArenaBorder</c>, doors with <c>PanelBorder</c>, cover with
/// <c>CoverGood</c>, and objectives with <c>Waypoint</c> — the closest
/// existing roles by intent. This mapping lives as a comment on the call
/// site in <c>SandataGame.cs</c>, not in this file, because this file never
/// touches color.
/// </para>
/// </remarks>
internal static class WorldRenderer
{
    /// <summary>
    /// World-unit thickness drawn for every <see cref="WallRecord"/>,
    /// centered on the record's own zero-thickness line segment. Chosen even
    /// so it halves cleanly at every zoom level the fixture tests use.
    /// </summary>
    internal const float WallThicknessWu = 8f;

    /// <summary>
    /// World-unit thickness drawn for every <see cref="DoorRecord"/>. Half of
    /// <see cref="WallThicknessWu"/> so an open doorway reads as a visibly
    /// thinner gap in the wall line it sits in, per design section 11's
    /// intent for readable breach points.
    /// </summary>
    internal const float DoorThicknessWu = 4f;

    /// <summary>Which of the two shape families a <see cref="DrawShape"/> carries.</summary>
    internal enum DrawShapeKind
    {
        /// <summary>An unrotated box: <see cref="DrawShape.AxisAlignedBounds"/> is valid.</summary>
        AxisAligned,

        /// <summary>
        /// A rotated block: <see cref="DrawShape.RotatedCenter"/>,
        /// <see cref="DrawShape.RotatedLength"/>,
        /// <see cref="DrawShape.RotatedThickness"/>, and
        /// <see cref="DrawShape.RotationRadians"/> are valid.
        /// </summary>
        Rotated,
    }

    /// <summary>
    /// One shape to paint, in whichever of the two families
    /// <see cref="Kind"/> names. Deliberately a single small value type
    /// rather than two, so <see cref="CreateWallWorldShape"/> and
    /// <see cref="ToScreenShape"/> can return either family for the same
    /// wall without the caller needing a discriminated pair of nullable
    /// results.
    /// </summary>
    internal readonly record struct DrawShape(
        DrawShapeKind Kind,
        Rectangle AxisAlignedBounds,
        Vector2 RotatedCenter,
        float RotatedLength,
        float RotatedThickness,
        float RotationRadians)
    {
        public static DrawShape FromAxisAligned(Rectangle bounds) =>
            new(DrawShapeKind.AxisAligned, bounds, Vector2.Zero, 0f, 0f, 0f);

        public static DrawShape FromRotated(Vector2 center, float length, float thickness, float rotationRadians) =>
            new(DrawShapeKind.Rotated, Rectangle.Empty, center, length, thickness, rotationRadians);
    }

    /// <summary>
    /// Builds a wall's world-space draw shape. An axis-aligned wall (a
    /// vertical or horizontal segment — <c>X1 == X2</c> or <c>Y1 == Y2</c>)
    /// produces an axis-aligned box; every other wall, including the
    /// fixture's 26.57-degree wall, produces a rotated block. A wall segment
    /// can never be diagonal in one axis only and orthogonal in the other, so
    /// these two cases are exhaustive.
    /// </summary>
    internal static DrawShape CreateWallWorldShape(WallRecord wall)
    {
        if (wall.X1 == wall.X2 || wall.Y1 == wall.Y2)
        {
            return DrawShape.FromAxisAligned(
                AxisAlignedSegmentBounds(wall.X1, wall.Y1, wall.X2, wall.Y2, WallThicknessWu));
        }

        var (center, length, rotationRadians) =
            SegmentCenterLengthAndRotation(wall.X1, wall.Y1, wall.X2, wall.Y2);
        return DrawShape.FromRotated(center, length, WallThicknessWu, rotationRadians);
    }

    /// <summary>
    /// Builds a door's world-space draw shape. Always axis-aligned:
    /// <c>MapValidator</c> rejects any <see cref="DoorRecord"/> whose
    /// endpoints are not axis-aligned before a record ever reaches this
    /// method, so there is no rotated branch here.
    /// </summary>
    internal static DrawShape CreateDoorWorldShape(DoorRecord door) =>
        DrawShape.FromAxisAligned(
            AxisAlignedSegmentBounds(door.X1, door.Y1, door.X2, door.Y2, DoorThicknessWu));

    /// <summary>
    /// Builds a cover object's world-space draw shape: the record's own box,
    /// with no extra padding. A <see cref="CoverRecord"/> already carries its
    /// four corners; unlike a wall or door it is not a zero-thickness line
    /// that needs one invented.
    /// </summary>
    internal static DrawShape CreateCoverWorldShape(CoverRecord cover) =>
        DrawShape.FromAxisAligned(new Rectangle(
            cover.MinX,
            cover.MinY,
            cover.MaxX - cover.MinX,
            cover.MaxY - cover.MinY));

    /// <summary>
    /// Builds an objective's world-space draw shape: a square box centered on
    /// the objective point, sized by its own <see cref="ObjectiveRecord.RadiusWu"/>.
    /// </summary>
    internal static DrawShape CreateObjectiveWorldShape(ObjectiveRecord objective) =>
        DrawShape.FromAxisAligned(new Rectangle(
            objective.X - objective.RadiusWu,
            objective.Y - objective.RadiusWu,
            objective.RadiusWu * 2,
            objective.RadiusWu * 2));

    /// <summary>
    /// Converts a world-space <see cref="DrawShape"/> (from any of the
    /// <c>Create*WorldShape</c> methods above) into a screen-space one, using
    /// <paramref name="camera"/>'s current zoom and center against
    /// <paramref name="contentBounds"/>. An axis-aligned box's two opposite
    /// corners are each converted independently and the box rebuilt from
    /// them, rather than scaling width/height directly, so the result is
    /// exact under <see cref="SandataCamera.WorldToScreen"/>'s own formula
    /// with no separate rounding path to drift out of step with it. A
    /// rotated block keeps its rotation (the camera never rotates the world)
    /// and scales its center, length, and thickness by <see cref="SandataCamera.Zoom"/>.
    /// </summary>
    internal static DrawShape ToScreenShape(DrawShape worldShape, SandataCamera camera, Rectangle contentBounds)
    {
        if (worldShape.Kind == DrawShapeKind.Rotated)
        {
            var rotatedScreenCenter = camera.WorldToScreen(worldShape.RotatedCenter, contentBounds);
            return DrawShape.FromRotated(
                rotatedScreenCenter,
                worldShape.RotatedLength * camera.Zoom,
                worldShape.RotatedThickness * camera.Zoom,
                worldShape.RotationRadians);
        }

        var bounds = worldShape.AxisAlignedBounds;
        var topLeft = camera.WorldToScreen(new Vector2(bounds.Left, bounds.Top), contentBounds);
        var bottomRight = camera.WorldToScreen(new Vector2(bounds.Right, bounds.Bottom), contentBounds);
        return DrawShape.FromAxisAligned(new Rectangle(
            (int)MathF.Round(topLeft.X),
            (int)MathF.Round(topLeft.Y),
            (int)MathF.Round(bottomRight.X - topLeft.X),
            (int)MathF.Round(bottomRight.Y - topLeft.Y)));
    }

    /// <summary>
    /// The axis-aligned box for a zero-thickness segment inflated by
    /// <paramref name="thicknessWu"/>, centered on the segment. Used for both
    /// walls and doors, whose only difference is which thickness constant the
    /// caller passes.
    /// </summary>
    private static Rectangle AxisAlignedSegmentBounds(int x1, int y1, int x2, int y2, float thicknessWu)
    {
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        var minY = Math.Min(y1, y2);
        var maxY = Math.Max(y1, y2);
        var halfThicknessWu = thicknessWu / 2f;

        if (minX == maxX)
        {
            // Vertical segment: inflate across X, keep the full Y span.
            return new Rectangle(
                (int)(minX - halfThicknessWu),
                minY,
                (int)thicknessWu,
                maxY - minY);
        }

        // Horizontal segment: inflate across Y, keep the full X span.
        // MapValidator has already rejected the degenerate X1==X2 && Y1==Y2 case.
        return new Rectangle(
            minX,
            (int)(minY - halfThicknessWu),
            maxX - minX,
            (int)thicknessWu);
    }

    /// <summary>
    /// The center point, length, and <c>Bam16</c>-consistent rotation
    /// (design section 12: 0 = +X, increasing toward +Y) of a non-orthogonal
    /// segment, for the rotated branch of <see cref="CreateWallWorldShape"/>.
    /// </summary>
    private static (Vector2 Center, float Length, float RotationRadians) SegmentCenterLengthAndRotation(
        int x1, int y1, int x2, int y2)
    {
        var start = new Vector2(x1, y1);
        var end = new Vector2(x2, y2);
        var delta = end - start;
        return ((start + end) / 2f, delta.Length(), MathF.Atan2(delta.Y, delta.X));
    }
}
