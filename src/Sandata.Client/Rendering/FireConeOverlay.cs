using Microsoft.Xna.Framework;
using Sandata.Core.Geometry;
using Sandata.Core.Mathematics;

namespace Sandata.Client.Rendering;

/// <summary>
/// Pure geometry for one operator's fire cone (design section 11's HUD
/// element list: "Fire cone overlay... per-operator vision cone,
/// <c>FireConeFill</c> and <c>FireConeEdge</c>, at every detail tier"). Like
/// <see cref="WorldRenderer"/>, every member here is a plain function of
/// plain values — no <c>GraphicsDevice</c>, no <c>SpriteBatch</c>, no
/// window — so <c>tests/Sandata.Client.Tests/OverlayGeometryTests.cs</c> can
/// pin exact results without a graphics context. The actual
/// <c>spriteBatch.Draw</c> calls that paint a <see cref="ConeGeometry"/> with
/// the <c>FireConeFill</c>/<c>FireConeEdge</c> theme colors are task 69's
/// job, not this file's.
/// </summary>
/// <remarks>
/// Tactical decision geometry — this overlay among it — renders at every
/// detail tier rather than fading with zoom the way
/// <see cref="OperatorLayout"/>'s decorative layers do (see
/// <see cref="OperatorGeometry.Create"/>'s <c>detailTier</c> gating).
/// Accordingly <see cref="CreateWorldGeometry"/> takes no detail-tier
/// parameter at all: unlike a decorative layer, there is no tier at which
/// this overlay produces less than the full cone.
/// </remarks>
internal static class FireConeOverlay
{
    /// <summary>
    /// One operator's fire cone as a triangle: the apex the operator stands
    /// at, and the two points where the cone's left and right boundary rays
    /// (the same edges <see cref="VisionCone.Contains"/> tests a point
    /// against) reach <c>rangeWu</c> away. The far edge between
    /// <see cref="LeftEdgeEnd"/> and <see cref="RightEdgeEnd"/> is a
    /// straight chord rather than the true circular arc
    /// <see cref="VisionCone.Contains"/>'s range cutoff describes — a
    /// documented simplification for the fill triangle a later pass may
    /// refine without changing what "inside the cone" means for targeting,
    /// since <see cref="VisionCone"/> owns that decision and this type never
    /// duplicates it.
    /// </summary>
    internal readonly record struct ConeGeometry(Vector2 Apex, Vector2 LeftEdgeEnd, Vector2 RightEdgeEnd)
    {
        /// <summary>
        /// True only for a degenerate, zero-range cone.
        /// <see cref="CreateWorldGeometry"/> rejects a non-positive range
        /// before it can construct one, so this is always <c>false</c> for
        /// any value that method returns.
        /// </summary>
        internal bool IsEmpty => LeftEdgeEnd == Apex && RightEdgeEnd == Apex;
    }

    /// <summary>
    /// Builds a fire cone's world-space geometry from the same inputs
    /// <see cref="VisionCone.Contains"/> takes for its centre and
    /// half-width: <paramref name="apexWu"/> plays the role of the point
    /// <see cref="VisionCone.Contains"/>'s <c>dx</c>/<c>dy</c> are offset
    /// from, <paramref name="facing"/> is its <c>centre</c>, and
    /// <paramref name="halfWidth"/> is its <c>halfWidth</c> — the exact same
    /// <c>centre - halfWidth</c>/<c>centre + halfWidth</c> boundary angles,
    /// read from the same <see cref="ConeBoundaryTable.BoundaryVector"/> this
    /// method reads from, so a caller can never observe this overlay's edges
    /// disagreeing with what <see cref="VisionCone.Contains"/> would
    /// actually admit for the same inputs.
    /// </summary>
    /// <param name="rangeWu">
    /// How far the two edges extend, in world units. Must be positive;
    /// <see cref="VisionCone.Contains"/>'s own <c>rangeSquared</c> parameter
    /// is this value squared.
    /// </param>
    internal static ConeGeometry CreateWorldGeometry(Vector2 apexWu, Bam16 facing, ushort halfWidth, float rangeWu)
    {
        if (rangeWu <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(rangeWu), rangeWu, "A fire cone's range must be positive.");
        }

        var leftAngle = new Bam16(unchecked((ushort)(facing.Raw - halfWidth)));
        var rightAngle = new Bam16(unchecked((ushort)(facing.Raw + halfWidth)));

        return new ConeGeometry(
            apexWu,
            apexWu + (BoundaryDirection(leftAngle) * rangeWu),
            apexWu + (BoundaryDirection(rightAngle) * rangeWu));
    }

    /// <summary>
    /// Converts a world-space <see cref="ConeGeometry"/> into screen space by
    /// converting all three points independently through
    /// <paramref name="camera"/> — the same per-point conversion approach
    /// <see cref="WorldRenderer.ToScreenShape"/> uses for an axis-aligned
    /// box's two opposite corners.
    /// </summary>
    internal static ConeGeometry ToScreenGeometry(ConeGeometry worldGeometry, SandataCamera camera, Rectangle contentBounds) =>
        new(
            camera.WorldToScreen(worldGeometry.Apex, contentBounds),
            camera.WorldToScreen(worldGeometry.LeftEdgeEnd, contentBounds),
            camera.WorldToScreen(worldGeometry.RightEdgeEnd, contentBounds));

    /// <summary>
    /// The unit direction <paramref name="angle"/> points, derived from
    /// <see cref="ConeBoundaryTable.BoundaryVector"/> by normalising — never
    /// by a second trigonometric lookup of any kind.
    /// <see cref="ConeBoundaryTable"/>'s own remarks note its vector's
    /// magnitude is not a stable contract, only its direction is, so this is
    /// the one place that magnitude gets resolved away, for rendering only.
    /// </summary>
    private static Vector2 BoundaryDirection(Bam16 angle)
    {
        var (x, y) = ConeBoundaryTable.BoundaryVector(angle);
        return Vector2.Normalize(new Vector2(x, y));
    }
}
