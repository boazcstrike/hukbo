using System.Collections.Immutable;
using Microsoft.Xna.Framework;

namespace Sandata.Client.Rendering;

/// <summary>
/// Pure geometry for a selected group's order path (design section 11's HUD
/// element list: "Order path overlay... the polyline and waypoints for a
/// selected group", scaffolded — "renders a path when one exists, has no
/// editor"). Same pure-helper shape as <see cref="WorldRenderer"/> and
/// <see cref="FireConeOverlay"/>: no <c>GraphicsDevice</c>, no
/// <c>SpriteBatch</c>, no window.
/// </summary>
/// <remarks>
/// Like <see cref="FireConeOverlay"/>, this is tactical decision geometry
/// and takes no detail-tier parameter: a selected group's order path and
/// waypoints render at every detail tier, never fading with zoom the way a
/// decorative operator layer does.
/// </remarks>
internal static class OrderPathOverlay
{
    /// <summary>
    /// World-unit half-width of the small square
    /// <see cref="WorldRenderer.DrawShape"/> each waypoint marker becomes,
    /// centered on the waypoint. This reuses <see cref="WorldRenderer.DrawShape"/>
    /// and <see cref="WorldRenderer.ToScreenShape"/> rather than a second,
    /// parallel box type — the same square-box-on-a-point shape
    /// <c>WorldRenderer.CreateObjectiveWorldShape</c> already uses to mark an
    /// objective.
    /// </summary>
    internal const float WaypointMarkerRadiusWu = 6f;

    /// <summary>One leg of the order path polyline, in whichever space the caller built it in.</summary>
    internal readonly record struct PathSegment(Vector2 Start, Vector2 End);

    /// <summary>
    /// Builds the polyline's <c>waypointsWu.Length - 1</c> consecutive
    /// segments, each connecting one waypoint to the next in order. Fewer
    /// than two waypoints — no path, or a single standing order with nothing
    /// to connect it to — produces no segments; that is the "renders a path
    /// when one exists" half of the scaffolded status, not an error.
    /// </summary>
    internal static ImmutableArray<PathSegment> CreateWorldSegments(ImmutableArray<Vector2> waypointsWu)
    {
        if (waypointsWu.Length < 2)
        {
            return ImmutableArray<PathSegment>.Empty;
        }

        var segments = ImmutableArray.CreateBuilder<PathSegment>(waypointsWu.Length - 1);
        for (var i = 0; i < waypointsWu.Length - 1; i++)
        {
            segments.Add(new PathSegment(waypointsWu[i], waypointsWu[i + 1]));
        }

        return segments.MoveToImmutable();
    }

    /// <summary>
    /// Converts every segment's two endpoints independently through
    /// <paramref name="camera"/> — the same per-point conversion approach
    /// <see cref="WorldRenderer.ToScreenShape"/> and
    /// <see cref="FireConeOverlay.ToScreenGeometry"/> both use.
    /// </summary>
    internal static ImmutableArray<PathSegment> ToScreenSegments(
        ImmutableArray<PathSegment> worldSegments, SandataCamera camera, Rectangle contentBounds)
    {
        if (worldSegments.IsEmpty)
        {
            return ImmutableArray<PathSegment>.Empty;
        }

        var segments = ImmutableArray.CreateBuilder<PathSegment>(worldSegments.Length);
        foreach (var segment in worldSegments)
        {
            segments.Add(new PathSegment(
                camera.WorldToScreen(segment.Start, contentBounds),
                camera.WorldToScreen(segment.End, contentBounds)));
        }

        return segments.MoveToImmutable();
    }

    /// <summary>
    /// Builds one square <see cref="WorldRenderer.DrawShape"/> per waypoint,
    /// sized by <see cref="WaypointMarkerRadiusWu"/> and centered on it, in
    /// the same order as <paramref name="waypointsWu"/>. Empty in, empty
    /// out — a group with no waypoints draws no markers.
    /// </summary>
    internal static ImmutableArray<WorldRenderer.DrawShape> CreateWaypointWorldShapes(ImmutableArray<Vector2> waypointsWu)
    {
        if (waypointsWu.IsEmpty)
        {
            return ImmutableArray<WorldRenderer.DrawShape>.Empty;
        }

        var shapes = ImmutableArray.CreateBuilder<WorldRenderer.DrawShape>(waypointsWu.Length);
        foreach (var waypoint in waypointsWu)
        {
            shapes.Add(WorldRenderer.DrawShape.FromAxisAligned(new Rectangle(
                (int)MathF.Round(waypoint.X - WaypointMarkerRadiusWu),
                (int)MathF.Round(waypoint.Y - WaypointMarkerRadiusWu),
                (int)MathF.Round(WaypointMarkerRadiusWu * 2f),
                (int)MathF.Round(WaypointMarkerRadiusWu * 2f))));
        }

        return shapes.MoveToImmutable();
    }
}
