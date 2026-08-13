using Hukbo.Client.Presentation;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

internal static class HitEffectRenderer
{
    private static readonly Color WarmWhite = new(255, 244, 214);
    private static readonly Color WarmShard = new(255, 224, 174);

    /// <summary>
    /// PROVISIONAL legibility tuning (2026-08-13,
    /// docs/plans/2026-08-13-lethal-blow-legibility.md), not a historical or
    /// evidentiary claim: a killing blow's ring and shards draw in a hot tone
    /// clearly distinct from the ordinary warm-white hit, rather than a
    /// brighter shade of the same colour.
    /// </summary>
    private static readonly Color LethalHotRing = new(255, 64, 32);
    private static readonly Color LethalHotShard = new(255, 120, 40);

    public static void Draw(
        ReadOnlySpan<HitEffect> effects,
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
            var layout = HitEffectGeometry.Create(effect, cameraZoom);
            var ringAlpha = layout.Alpha *
                (0.78f + (layout.Pulse * 0.22f));
            var primaryColor = (effect.IsLethal ? LethalHotRing : WarmWhite) *
                ringAlpha;
            DrawRing(
                spriteBatch,
                pixel,
                center,
                layout.PrimaryRingRadius,
                effect.IsLethal ? 28 : 18,
                primaryColor,
                layout.RingThickness);

            if (layout.RingCount == 2)
            {
                DrawRing(
                    spriteBatch,
                    pixel,
                    center,
                    layout.SecondaryRingRadius,
                    segments: 20,
                    LethalHotRing * (layout.Alpha * 0.82f),
                    MathF.Max(1f, layout.RingThickness * 0.78f));
            }

            var shardColor = (effect.IsLethal ? LethalHotShard : WarmShard);
            for (var shardIndex = 0;
                 shardIndex < layout.VisibleShardCount;
                 shardIndex++)
            {
                var shard = layout.GetShard(shardIndex);
                DrawLine(
                    spriteBatch,
                    pixel,
                    center + new Vector2(shard.StartX, shard.StartY),
                    center + new Vector2(shard.EndX, shard.EndY),
                    shardColor * shard.Alpha,
                    shard.Thickness);
            }
        }
    }

    private static void DrawRing(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 center,
        float radius,
        int segments,
        Color color,
        float thickness)
    {
        if (radius <= 0f)
        {
            return;
        }

        var previous = center + new Vector2(radius, 0f);
        for (var segment = 1; segment <= segments; segment++)
        {
            var angle = MathF.Tau * segment / segments;
            var current = center + new Vector2(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius);
            DrawLine(
                spriteBatch,
                pixel,
                previous,
                current,
                color,
                thickness);
            previous = current;
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
