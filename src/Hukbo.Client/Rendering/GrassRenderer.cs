using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Draw sink for battlefield grass clusters: quad submission only, every
/// decision already made by <see cref="GrassGeometry"/>. Presentation only —
/// see the battlefield environment design, "Grass
/// clusters". Not unit tested, matching <see cref="PlainsBackdropRenderer"/>
/// and <see cref="PawnRenderer"/>. Draws inside the caller's existing
/// arena Begin/End pair from the shared 1x1 texture: zero additional
/// draw calls, zero new textures.
/// </summary>
internal static class GrassRenderer
{
    /// <summary>
    /// The theme id <see cref="UiThemeCatalog"/> and every shipped theme
    /// document use for the high-contrast theme — the existing
    /// eliminate-visual-noise flag this renderer reuses as the sway/shade
    /// trigger (R-W5.7) instead of inventing a new one.
    /// </summary>
    private const string HighContrastThemeId = "high-contrast";

    /// <summary>
    /// PROVISIONAL: screen-pixel radius, at apparent scale 1, one trample
    /// mark's flattened ellipse draws with (battlefield-environment-design.md,
    /// "Trampled and sparse areas"). Raised from <c>16f</c> on 2026-08-11 so a
    /// single mark covers ground rather than a spot and adjacent marks merge
    /// into one worn area
    /// (docs/plans/2026-08-11-armor-accent-trample-legibility-design.md,
    /// section 4).
    /// </summary>
    private const float TrampleMarkBaseRadius = 28f;

    /// <summary>
    /// PROVISIONAL: vertical squash applied to a trample mark's ellipse,
    /// matching the flattened-blot convention <c>BloodRenderer</c> uses for
    /// ground marks.
    /// </summary>
    private const float TrampleMarkFlatten = 0.55f;

    /// <summary>
    /// PROVISIONAL: factor a suppressed cluster's quad height is scaled by,
    /// keeping the bottom edge anchored to the ground so a trampled tuft
    /// reads as shorter rather than floating (battlefield-environment-
    /// design.md, "Trampled and sparse areas"). Cut from <c>0.55f</c> on
    /// 2026-08-11, where a suppressed tuft kept over half its height and read
    /// as slightly shorter rather than as stubble
    /// (docs/plans/2026-08-11-armor-accent-trample-legibility-design.md,
    /// section 4).
    /// </summary>
    private const float TrampleHeightReductionFactor = 0.28f;

    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        ImmutableArray<GrassCluster> clusters,
        ReadOnlySpan<TrampleMark> trampleMarks,
        SpectatorCamera camera,
        Rectangle mapBounds,
        Rectangle arenaBounds,
        UiTheme theme,
        MotionIntensity motionIntensity,
        float swayClockSeconds)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(theme);

        // Drawn first so grass quads paint over the marks beneath them —
        // the design's "drawn under grass" ordering, achieved by draw order
        // alone rather than any layering flag.
        DrawTrampleMarks(spriteBatch, pixel, trampleMarks, camera, arenaBounds, theme);

        if (clusters.IsDefaultOrEmpty ||
            mapBounds.Width <= 0 || mapBounds.Height <= 0)
        {
            return;
        }

        var band = GrassGeometry.GetZoomBand(camera.Zoom);
        var isHighContrastTheme = theme.Id == HighContrastThemeId;

        for (var i = 0; i < clusters.Length; i++)
        {
            var cluster = clusters[i];
            var screenAnchor = camera.WorldToScreen(
                cluster.WorldPosition,
                arenaBounds);

            var cullBounds = GrassGeometry.GetClusterCullBounds(
                cluster,
                screenAnchor,
                camera.Zoom,
                band);
            if (!GrassGeometry.IsClusterVisible(
                cullBounds,
                mapBounds,
                arenaBounds,
                out _))
            {
                continue;
            }

            var isSuppressed = GrassGeometry.IsSuppressedByTrample(
                cluster.WorldPosition,
                trampleMarks);
            var amplitudeFactor = GrassSway.ResolveAmplitudeFactor(
                motionIntensity,
                isHighContrastTheme,
                band,
                isSuppressed);

            // A suppressed cluster drops to the trample stubble tone instead
            // of its size class's own, which for a Large cluster was the very
            // shade the mark beneath it draws at — trampled grass was
            // indistinguishable from the worn ground it stood on until
            // 2026-08-11 (design section 4).
            var shade = GetShade(
                theme,
                isSuppressed
                    ? GrassGeometry.TrampleStubbleShadeInterpolation
                    : GrassGeometry.GetShadeInterpolation(
                        cluster.SizeClass,
                        isHighContrastTheme));

            var swayOffset = GrassSway.GrassSwayOffset(
                swayClockSeconds,
                cluster.Phase,
                amplitudeFactor);

            var quadCount = GrassGeometry.GetQuadCount(cluster, band);
            for (var quadIndex = 0; quadIndex < quadCount; quadIndex++)
            {
                var quadBounds = GrassGeometry.GetQuadBounds(
                    cluster,
                    screenAnchor,
                    camera.Zoom,
                    band,
                    quadIndex,
                    quadCount,
                    swayOffset);
                if (isSuppressed)
                {
                    quadBounds = ApplyTrampleHeightReduction(quadBounds);
                }

                // Clip to the map, not just the viewport, exactly as
                // DrawDecals does: a quad centered near a map edge would
                // otherwise bleed past the arena border onto bare canvas.
                var clipped = Rectangle.Intersect(quadBounds, mapBounds);
                if (clipped.Width <= 0 || clipped.Height <= 0 ||
                    !arenaBounds.Intersects(clipped))
                {
                    continue;
                }

                spriteBatch.Draw(pixel, clipped, shade);
            }
        }
    }

    /// <summary>
    /// Shrinks <paramref name="quadBounds"/> toward stubble height while
    /// keeping its bottom edge fixed, so a suppressed tuft reads as trampled
    /// into the ground rather than shrinking from its base upward.
    /// </summary>
    private static Rectangle ApplyTrampleHeightReduction(Rectangle quadBounds)
    {
        var reducedHeight = Math.Max(
            1,
            (int)MathF.Round(quadBounds.Height * TrampleHeightReductionFactor));
        return new Rectangle(
            quadBounds.X,
            quadBounds.Y + (quadBounds.Height - reducedHeight),
            quadBounds.Width,
            reducedHeight);
    }

    /// <summary>
    /// Draws every live trample mark as a flattened ellipse, mirroring
    /// <c>BloodRenderer.DrawGroundMarks</c>'s bounding-radius cull and
    /// scale-based ellipse draw rather than <see cref="Draw"/>'s
    /// map-clipped rectangles: a mark's position always falls within the
    /// map by construction (it comes from a dying agent), so there is
    /// nothing for a map-bounds clip to catch here.
    /// </summary>
    private static void DrawTrampleMarks(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        ReadOnlySpan<TrampleMark> trampleMarks,
        SpectatorCamera camera,
        Rectangle arenaBounds,
        UiTheme theme)
    {
        if (trampleMarks.IsEmpty)
        {
            return;
        }

        var shade = GetShade(theme, GrassGeometry.TrampleMarkShadeInterpolation);
        var apparentScale = Math.Clamp(
            camera.Zoom,
            PlainsBackdropGeometry.MinimumDecalApparentScale,
            PlainsBackdropGeometry.MaximumDecalApparentScale);
        var radius = TrampleMarkBaseRadius * apparentScale;
        var extent = (int)MathF.Ceiling(MathF.Max(1f, radius));

        for (var index = 0; index < trampleMarks.Length; index++)
        {
            var mark = trampleMarks[index];
            var worldPosition = new Vector2(
                mark.XRaw / (float)FixedPoint.Scale,
                mark.YRaw / (float)FixedPoint.Scale);
            var center = camera.WorldToScreen(worldPosition, arenaBounds);

            var boundingBox = new Rectangle(
                (int)MathF.Floor(center.X) - extent,
                (int)MathF.Floor(center.Y) - extent,
                (extent * 2) + 1,
                (extent * 2) + 1);
            if (!arenaBounds.Intersects(boundingBox))
            {
                continue;
            }

            spriteBatch.Draw(
                pixel,
                center,
                sourceRectangle: null,
                shade,
                rotation: 0f,
                new Vector2(0.5f, 0.5f),
                new Vector2(
                    MathF.Max(1f, radius * 2f),
                    MathF.Max(1f, radius * 2f * TrampleMarkFlatten)),
                SpriteEffects.None,
                layerDepth: 0f);
        }
    }

    private static Color GetShade(UiTheme theme, float interpolation) =>
        Color.Lerp(
            theme.Colors.ArenaSurface,
            theme.Colors.ArenaBorder,
            interpolation);
}
