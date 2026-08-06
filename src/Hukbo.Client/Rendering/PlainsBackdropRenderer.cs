using System.Collections.Immutable;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The per-frame inputs the backdrop needs, grouped so the same five scalars
/// are not threaded positionally through three call levels where a swap would
/// still compile.
/// </summary>
internal readonly record struct PlainsBackdropFrame(
    Rectangle MapBounds,
    Rectangle ArenaBounds,
    int MapWidth,
    int MapHeight,
    ulong Seed);

/// <summary>
/// Draw sink for the plains battlefield backdrop: the ground grid then the
/// scatter decals, both beneath pawns and the arena border. Presentation
/// only — see
/// docs/archives/2026-07-27/2026-07-27-plains-backdrop-design.md. Not unit
/// tested, matching <c>PawnRenderer.Draw</c>.
///
/// The grid loop calls <see cref="PlainsBackdropGeometry.GetGroundCell"/>, the
/// same single formula the unit-tested <c>GetGroundCells</c> uses, so the
/// tested path and the shipped path cannot drift apart. It deliberately avoids
/// <c>GetGroundCells</c> itself, which allocates an array this method would
/// otherwise allocate every frame.
/// </summary>
internal static class PlainsBackdropRenderer
{
    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        in PlainsBackdropFrame frame,
        ImmutableArray<PlainsDecal> decals,
        SpectatorCamera camera,
        UiTheme theme)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(camera);

        DrawGroundGrid(spriteBatch, pixel, frame, theme);
        DrawDecals(spriteBatch, pixel, frame, decals, camera, theme);
    }

    private static void DrawGroundGrid(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        in PlainsBackdropFrame frame,
        UiTheme theme)
    {
        var (columns, rows) = PlainsBackdropGeometry.GetGridDimensions(
            frame.MapWidth,
            frame.MapHeight);
        if (columns <= 0 || rows <= 0 ||
            frame.MapBounds.Width <= 0 || frame.MapBounds.Height <= 0)
        {
            return;
        }

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = PlainsBackdropGeometry.GetGroundCell(
                    frame.MapBounds,
                    columns,
                    rows,
                    column,
                    row,
                    frame.Seed);

                if (!frame.ArenaBounds.Intersects(cell.Bounds))
                {
                    continue;
                }

                spriteBatch.Draw(
                    pixel,
                    cell.Bounds,
                    GetShade(
                        theme,
                        PlainsBackdropGeometry.GroundShadeInterpolation[
                            cell.ShadeIndex]));
            }
        }
    }

    private static void DrawDecals(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        in PlainsBackdropFrame frame,
        ImmutableArray<PlainsDecal> decals,
        SpectatorCamera camera,
        UiTheme theme)
    {
        if (decals.IsDefaultOrEmpty)
        {
            return;
        }

        for (var i = 0; i < decals.Length; i++)
        {
            var decal = decals[i];
            var screenAnchor = camera.WorldToScreen(
                decal.WorldPosition,
                frame.ArenaBounds);
            var bounds = PlainsBackdropGeometry.GetDecalScreenBounds(
                screenAnchor,
                camera.Zoom,
                decal.ScaleFactor);

            // Clip to the map, not just the viewport: a decal centered near a
            // map edge would otherwise bleed half its size past the arena
            // border onto the bare canvas.
            var clipped = Rectangle.Intersect(bounds, frame.MapBounds);
            if (clipped.Width <= 0 || clipped.Height <= 0 ||
                !frame.ArenaBounds.Intersects(clipped))
            {
                continue;
            }

            spriteBatch.Draw(
                pixel,
                clipped,
                GetShade(
                    theme,
                    PlainsBackdropGeometry.DecalKindInterpolation[
                        (int)decal.Kind]));
        }
    }

    private static Color GetShade(UiTheme theme, float interpolation) =>
        Color.Lerp(
            theme.Colors.ArenaSurface,
            theme.Colors.ArenaBorder,
            interpolation);
}
