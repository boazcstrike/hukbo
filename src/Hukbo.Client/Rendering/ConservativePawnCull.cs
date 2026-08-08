using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// GPU-015. A conservative, appearance-blind and pose-blind upper bound on
/// where a pawn can draw, expressed as a radius in pixels around its foot
/// anchor at a given camera zoom.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not adopted, and deliberately so.</b> GPU-016, the task that would have
/// moved this bound ahead of appearance resolution in the pawn loop, was
/// dropped on 2026-08-07 and nothing calls this type today. The saving it
/// measured is zero at minimum zoom and zero at default fit, and the only
/// station it could move already renders ten times inside budget. The bound and
/// its brute-force containment proof are kept because
/// <c>ConservativePawnCullTests</c> is the only thing that would catch the
/// mirrored <see cref="PawnGeometry"/> constants below drifting from the real
/// ones. See the archived plan at
/// <c>docs/archives/2026-08-07/gpu-render/2026-07-28-gpu-render.md</c>.
/// </para>
/// <para>
/// The purpose was the reordering described in the GPU render design's section
/// 5.2, fix 2a. Today the pawn loop resolves an appearance for every living
/// agent and only then tests the exact pose-blind bounds against the arena
/// panel, so a battle viewed at maximum zoom still resolves an appearance for
/// every agent on the field. The exact bounds cannot be computed without an
/// appearance, but this bound can: it depends on nothing except the camera
/// zoom, so it can be tested first and the appearance resolved only for the
/// pawns that survive.
/// </para>
/// <para>
/// The bound is a genuine superset, never a replacement. It is strictly wider
/// than <c>PawnRenderer.GetBounds</c>'s result for every appearance the
/// catalogs can produce, so a caller that keeps today's exact test afterward
/// draws exactly the same set of pawns it draws now. Nothing here may ever be
/// used as the only cull.
/// </para>
/// <para>
/// The coefficients below are derived from <see cref="PawnGeometry"/>'s own
/// layout arithmetic rather than chosen by hand, and
/// <c>ConservativePawnCullTests</c> proves the containment claim by brute
/// force over the full catalog cross-product rather than by trusting the
/// derivation. <see cref="PawnGeometry"/>'s scale constants are private, so
/// they are mirrored here the same way <see cref="DetailTierGate"/> mirrors
/// its tier thresholds: read-only ground truth, duplicated deliberately, kept
/// honest by a test rather than by shared mutable state.
/// </para>
/// </remarks>
internal static class ConservativePawnCull
{
    /// <summary>
    /// Mirrors <see cref="PawnGeometry"/>'s own zoom-to-apparent-scale
    /// constants. Pinned against the real thing by
    /// <c>ConservativePawnCullTests.ApparentScale_MatchesPawnGeometry</c>.
    /// </summary>
    private const float ZoomScale = 1.35f;

    /// <summary>See <see cref="ZoomScale"/>.</summary>
    private const float MinimumApparentScale = 0.72f;

    /// <summary>See <see cref="ZoomScale"/>.</summary>
    private const float MaximumApparentScale = 2.40f;

    /// <summary>
    /// The scale-proportional term of the radius, in pixels per unit of
    /// apparent scale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived from the largest extent any layer can reach away from the foot
    /// anchor, over every appearance and every optional layer, before the
    /// selection padding is added. The four directions are not equal, and the
    /// upward one is what sets this number:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Up, 24.2 units.</b> The Kalis weapon line reaches
    /// <c>-21</c> units and carries <c>3.2</c> units of padding
    /// (<c>CreateWeaponLayout</c>), so its rectangle's top is <c>-24.2</c>
    /// units. The head stack is the runner-up at <c>22.2</c> units — one unit
    /// of torso lift, <c>12 * 1.10</c> units of tallest torso, one unit of
    /// head gap, and <c>7</c> units of head.
    /// </description></item>
    /// <item><description>
    /// <b>Right, 19.2 units.</b> The Kampilan weapon line reaches <c>15</c>
    /// units with <c>4.2</c> units of padding. The Wasay's axe head, at
    /// <c>14.4</c> units, is inside that.
    /// </description></item>
    /// <item><description>
    /// <b>Left, 11 units.</b> The shield block hangs at <c>-7</c> units and
    /// is up to <c>4</c> units wide (<c>CreateShield</c>). The Itak's
    /// off-hand secondary, at <c>-8</c> units, is inside that.
    /// </description></item>
    /// <item><description>
    /// <b>Down, 1.2 units.</b> Only the shield block can pass below the foot
    /// anchor, and only because its top is measured from the shortest torso
    /// while its height is the tallest skin's.
    /// </description></item>
    /// </list>
    /// <para>
    /// The selection padding <see cref="PawnGeometry"/> inflates the rendered
    /// bounds by is <c>max(3, ceil(3 * apparentScale))</c>, which is at most
    /// <c>3 * apparentScale + 1</c> everywhere in the clamped scale range, so
    /// the neutral upward extent becomes <c>24.2 + 3 = 27.2</c> units per unit
    /// of apparent scale. That was the radius while a pawn's drawn geometry
    /// could only be neutral.
    /// </para>
    /// <para>
    /// An attack pose reaches further. It aims the weapon at a true heading
    /// rather than the neutral up-and-right one, so the same reach can point
    /// in any direction, and it extends the line by the family's own visual
    /// envelope; the arms, the axe head, the trail, and the shield guard all
    /// travel with it (attack-animation-v2 design section 11). The worst case
    /// is the Kalis, which owns both the longest neutral reach and the largest
    /// extension envelope, on an evaded follow-through: measured at
    /// <c>36.875</c> units per unit of apparent scale over every weapon,
    /// shield, resolution, sixty-four headings, and every zoom sample, by
    /// <c>ConservativePawnCullTests.Radius_ExceedsTheWorstCaseExtentByAFlatFewPixels</c>.
    /// </para>
    /// <para>
    /// The radius stays pose-blind — it is still a function of zoom alone, and
    /// a pose-aware cull would make the drawn set a function of presentation
    /// animation phase — but it is now derived from the largest extent a posed
    /// pawn can reach rather than a neutral one. That is what keeps a warrior
    /// striking at the edge of the panel from being culled while its weapon
    /// would have been on screen. The cost is a wider pre-cull:
    /// <c>ConservativePawnCullTests.AdmittedFraction_IsRecordedForEachCameraStation</c>
    /// records what it actually admits.
    /// </para>
    /// </remarks>
    private const float RadiusUnitsPerApparentScale = 37.6f;

    /// <summary>
    /// The scale-independent term of the radius, in pixels: three pixels of
    /// accumulated rounding slack, plus one pixel of the selection padding's
    /// own constant term, plus one pixel of deliberate headroom.
    /// </summary>
    /// <remarks>
    /// <see cref="PawnGeometry"/> rounds, floors, ceilings, and applies whole-
    /// pixel floors at roughly a dozen points between the foot anchor and the
    /// final rectangle. Each contributes at most half a pixel or one pixel in
    /// the outward direction, and the upward chain — torso lift, torso height,
    /// head gap, head size — is the one that accumulates the most. Three
    /// pixels covers that chain, one pixel covers the selection padding's
    /// ceiling, and the last pixel is headroom against a float result landing
    /// exactly on a pixel boundary.
    /// </remarks>
    private const float RadiusConstantPixels = 5f;

    /// <summary>
    /// The apparent scale <see cref="PawnGeometry"/> would use for
    /// <paramref name="cameraZoom"/> at the default scale multiplier of one.
    /// Clamped at both ends, which is why a pawn keeps a floor size when the
    /// spectator zooms fully out and stops growing when they zoom fully in.
    /// </summary>
    public static float ApparentScale(float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        return Math.Clamp(
            cameraZoom * ZoomScale,
            MinimumApparentScale,
            MaximumApparentScale);
    }

    /// <summary>
    /// The radius, in pixels, of a square centered on a pawn's foot anchor
    /// that contains that pawn's pose-blind visual bounds for every
    /// appearance the catalogs can produce. A pure function of
    /// <paramref name="cameraZoom"/> alone, so a caller computes it once per
    /// frame and reuses it for every agent.
    /// </summary>
    public static float RadiusPixels(float cameraZoom) =>
        (ApparentScale(cameraZoom) * RadiusUnitsPerApparentScale) +
        RadiusConstantPixels;

    /// <summary>
    /// <see cref="RadiusPixels"/> as a whole-pixel rectangle around
    /// <paramref name="footAnchor"/>. Floored on the low edges and ceilinged
    /// on the high ones, so the integer rectangle never cuts inside the real
    /// interval the radius describes.
    /// </summary>
    public static Rectangle Bounds(Vector2 footAnchor, float cameraZoom)
    {
        var radius = RadiusPixels(cameraZoom);
        var left = (int)MathF.Floor(footAnchor.X - radius);
        var top = (int)MathF.Floor(footAnchor.Y - radius);
        var right = (int)MathF.Ceiling(footAnchor.X + radius);
        var bottom = (int)MathF.Ceiling(footAnchor.Y + radius);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// The pre-cull test itself: false means this pawn cannot possibly reach
    /// <paramref name="arenaBounds"/> under any appearance, so the caller may
    /// skip it without resolving one. True means only that it might, and the
    /// caller must still run the exact pose-blind test afterward.
    /// </summary>
    /// <remarks>
    /// Takes a pre-computed <paramref name="radiusPixels"/> rather than a
    /// zoom, because the radius is a per-frame value and the whole point of
    /// this helper is that the per-agent path does no arithmetic it can
    /// avoid. It compares the real interval directly rather than building the
    /// <see cref="Bounds"/> rectangle first, which is the same decision:
    /// <paramref name="arenaBounds"/>'s edges are whole numbers, and for a
    /// whole number <c>n</c> both <c>floor(v) &lt; n</c> and <c>v &lt; n</c>
    /// agree, as do <c>ceil(v) &gt; n</c> and <c>v &gt; n</c>. So this returns
    /// exactly what <c>Bounds(footAnchor, zoom).Intersects(arenaBounds)</c>
    /// returns, on the same half-open convention
    /// <see cref="Rectangle.Intersects(Rectangle)"/> uses, without the
    /// rectangle.
    /// </remarks>
    public static bool IsPotentiallyVisible(
        Vector2 footAnchor,
        float radiusPixels,
        Rectangle arenaBounds) =>
        footAnchor.X + radiusPixels > arenaBounds.Left &&
        footAnchor.X - radiusPixels < arenaBounds.Right &&
        footAnchor.Y + radiusPixels > arenaBounds.Top &&
        footAnchor.Y - radiusPixels < arenaBounds.Bottom;
}
