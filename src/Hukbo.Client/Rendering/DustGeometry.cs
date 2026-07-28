using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The on-screen shape of one dust puff at one moment: how many rectangles it
/// draws with, their shared half-extent, and their fade. Every field is
/// already resolved by <see cref="DustGeometry.Create"/>, so
/// <see cref="DustRenderer"/> makes no further decision beyond placing the
/// rectangles at the puff's screen anchor.
/// </summary>
internal readonly record struct DustPuffLayout(
    int RectangleCount,
    float Alpha,
    float HalfExtent)
{
    /// <summary>
    /// PROVISIONAL: screen-pixel offset the second rectangle of a two-
    /// rectangle puff draws at, so a <see cref="DustPuffKind.Death"/> puff
    /// reads as a small loose cluster rather than one stacked square.
    /// </summary>
    private const float SecondRectangleOffsetX = 3f;

    private const float SecondRectangleOffsetY = 1.5f;

    /// <summary>
    /// The on-screen rectangle for rectangle <paramref name="rectangleIndex"/>
    /// of this layout's <see cref="RectangleCount"/>, centered on
    /// <paramref name="screenAnchor"/> — rectangle 0 exactly, any further
    /// rectangle offset by <see cref="SecondRectangleOffsetX"/>/
    /// <see cref="SecondRectangleOffsetY"/>. Pure and allocation free.
    /// </summary>
    public Rectangle GetRectangleBounds(int rectangleIndex, Vector2 screenAnchor)
    {
        if ((uint)rectangleIndex >= (uint)RectangleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rectangleIndex));
        }

        var offset = rectangleIndex == 0
            ? Vector2.Zero
            : new Vector2(SecondRectangleOffsetX, SecondRectangleOffsetY);
        var center = screenAnchor + offset;
        var size = Math.Max(1, (int)MathF.Round(HalfExtent * 2f));

        return new Rectangle(
            (int)MathF.Round(center.X - HalfExtent),
            (int)MathF.Round(center.Y - HalfExtent),
            size,
            size);
    }
}

/// <summary>
/// Pure layout helper for one dust puff (battlefield-environment-design.md,
/// "Dust and disturbed vegetation", VIS-029). Mirrors
/// <see cref="HitEffectGeometry"/>'s shape: an allocation-free struct computed
/// once per puff per frame from <see cref="DustPuff"/> and the camera zoom,
/// consumed only by <see cref="DustRenderer"/>. Touches only value types, so
/// it is fully unit tested with no graphics device.
/// </summary>
internal static class DustGeometry
{
    /// <summary>
    /// PROVISIONAL: the shade interpolation toward <c>ArenaBorder</c> every
    /// dust puff draws with — within the ground shade range
    /// (<see cref="PlainsBackdropGeometry.GroundShadeInterpolation"/>,
    /// 0.00-0.12) and under the shared
    /// <see cref="PlainsBackdropGeometry.MaximumBackdropInterpolation"/>
    /// ceiling of 0.22 (battlefield-environment-design.md, "Dust and
    /// disturbed vegetation").
    /// </summary>
    internal const float ShadeInterpolation = 0.10f;

    private const float MinimumApparentScale = 0.6f;
    private const float MaximumApparentScale = 2.2f;

    // PROVISIONAL: screen-pixel half-extents at apparent scale 1. A death
    // puff kicks up more ground than a single footfall, so it starts and
    // expands larger than an attack kick.
    private const float DeathBaseHalfExtent = 4f;
    private const float DeathExpansionHalfExtent = 5f;
    private const float KickBaseHalfExtent = 2.5f;
    private const float KickExpansionHalfExtent = 2.5f;

    /// <summary>
    /// The layout for <paramref name="puff"/> at its current age, clamped to
    /// <paramref name="cameraZoom"/>'s apparent scale range. Progress runs
    /// from 0 (just spawned) to 1 (about to expire) across
    /// <see cref="DustPuff.LifetimeSeconds"/>: alpha fades linearly from 1 to
    /// 0 and the rectangle half-extent grows from its base size toward the
    /// base plus its expansion range, so the puff visibly expands and fades
    /// together rather than fading in place. Pure and allocation free.
    /// </summary>
    public static DustPuffLayout Create(DustPuff puff, float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        var apparentScale = Math.Clamp(
            cameraZoom,
            MinimumApparentScale,
            MaximumApparentScale);
        var progress = Math.Clamp(
            puff.AgeSeconds / puff.LifetimeSeconds,
            0f,
            1f);
        var alpha = Math.Clamp(1f - progress, 0f, 1f);
        var isDeath = puff.Kind == DustPuffKind.Death;
        var rectangleCount = isDeath ? 2 : 1;
        var baseHalfExtent = isDeath ? DeathBaseHalfExtent : KickBaseHalfExtent;
        var expansionHalfExtent = isDeath
            ? DeathExpansionHalfExtent
            : KickExpansionHalfExtent;
        var halfExtent =
            (baseHalfExtent + (expansionHalfExtent * progress)) * apparentScale;

        return new DustPuffLayout(rectangleCount, alpha, halfExtent);
    }
}
