using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Draws a small bright cross where two weapons, or a weapon and a shield,
/// met. Follows the <see cref="HitEffectRenderer"/> shape: the layout decides
/// every dimension and this file only paints.
/// </summary>
internal static class ClashEffectRenderer
{
    private static readonly Color SparkWhite = new(255, 250, 232);
    private static readonly Color ShieldStrike = new(226, 205, 160);

    public static void Draw(
        ReadOnlySpan<ClashEffect> effects,
        SpectatorCamera camera,
        Rectangle arenaBounds,
        float cameraZoom,
        SpriteBatch spriteBatch,
        Texture2D pixel)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);

        foreach (ref readonly var effect in effects)
        {
            var center = camera.WorldToScreen(
                new Vector2(
                    effect.XRaw / (float)FixedPoint.Scale,
                    effect.YRaw / (float)FixedPoint.Scale),
                arenaBounds);
            var layout = ClashEffectGeometry.Create(effect, cameraZoom);
            var color = (effect.Resolution == AttackResolution.ShieldBlocked
                ? ShieldStrike
                : SparkWhite) * layout.Alpha;
            var arm = layout.ArmLength;

            DrawLine(
                spriteBatch,
                pixel,
                center + new Vector2(-arm, -arm),
                center + new Vector2(arm, arm),
                color,
                layout.ArmThickness);
            DrawLine(
                spriteBatch,
                pixel,
                center + new Vector2(arm, -arm),
                center + new Vector2(-arm, arm),
                color,
                layout.ArmThickness);
        }
    }

    private static void DrawLine(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 start,
        Vector2 end,
        Color color,
        float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= float.Epsilon)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            start,
            sourceRectangle: null,
            color,
            MathF.Atan2(delta.Y, delta.X),
            new Vector2(0f, 0.5f),
            new Vector2(length, MathF.Max(1f, thickness)),
            SpriteEffects.None,
            layerDepth: 0f);
    }
}
