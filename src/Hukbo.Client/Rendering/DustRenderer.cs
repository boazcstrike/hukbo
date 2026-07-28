using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Draw sink for dust puffs: quad submission only, every decision already
/// made by <see cref="DustGeometry"/>. Presentation only — see
/// docs/archives/2026-07-28/improve-visuals/battlefield-environment-design.md, "Dust and
/// disturbed vegetation". Not unit tested, matching
/// <see cref="HitEffectRenderer"/> and <see cref="GrassRenderer"/>. Draws
/// inside the caller's existing arena Begin/End pair from the shared 1x1
/// texture: zero additional draw calls, zero new textures.
/// </summary>
internal static class DustRenderer
{
    public static void Draw(
        ReadOnlySpan<DustPuff> puffs,
        SpectatorCamera camera,
        Rectangle arenaBounds,
        float cameraZoom,
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiTheme theme)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(theme);

        if (puffs.IsEmpty)
        {
            return;
        }

        var shade = Color.Lerp(
            theme.Colors.ArenaSurface,
            theme.Colors.ArenaBorder,
            DustGeometry.ShadeInterpolation);

        foreach (ref readonly var puff in puffs)
        {
            var worldPosition = new Vector2(
                puff.XRaw / (float)FixedPoint.Scale,
                puff.YRaw / (float)FixedPoint.Scale);
            var screenAnchor = camera.WorldToScreen(worldPosition, arenaBounds);
            var layout = DustGeometry.Create(puff, cameraZoom);
            var color = shade * layout.Alpha;

            for (var rectangleIndex = 0;
                rectangleIndex < layout.RectangleCount;
                rectangleIndex++)
            {
                var bounds = layout.GetRectangleBounds(rectangleIndex, screenAnchor);
                if (!arenaBounds.Intersects(bounds))
                {
                    continue;
                }

                spriteBatch.Draw(pixel, bounds, color);
            }
        }
    }
}
