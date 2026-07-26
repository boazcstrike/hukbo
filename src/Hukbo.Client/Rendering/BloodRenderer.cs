using Hukbo.Client.Presentation;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Quad submission only: every decision has already been made by
/// <see cref="BloodGeometry"/>. Ground marks and bursts are separate entry
/// points so the caller can draw marks beneath pawns and sprays above them.
/// </summary>
/// <remarks>
/// Unlike <see cref="HitEffectRenderer"/>, which relies on the scissor
/// rectangle and therefore discards work only after a quad has been submitted,
/// every record here is culled against <paramref name="arenaBounds"/> first,
/// matching how <c>DrawPawns</c> culls.
/// </remarks>
internal static class BloodRenderer
{
    /// <summary>
    /// Fixed rather than a theme role, following the precedent of
    /// <see cref="HitEffectRenderer"/>'s warm constants and
    /// <c>FactionColorPalette</c>'s pawn colors: these are painted directly on
    /// the arena canvas, not through a themed panel surface. They are darker
    /// and more saturated than the red faction's pawn color so the two stay
    /// distinguishable.
    /// </summary>
    private static readonly Color FreshBlood = new(178, 24, 32);
    private static readonly Color LethalBlood = new(214, 38, 40);
    private static readonly Color GroundBlood = new(104, 20, 24);

    private const float GroundBlotFlatten = 1.3f;

    public static void DrawGroundMarks(
        ReadOnlySpan<GroundMark> marks,
        SpectatorCamera camera,
        Rectangle arenaBounds,
        float cameraZoom,
        SpriteBatch spriteBatch,
        Texture2D pixel)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);

        foreach (ref readonly var mark in marks)
        {
            var center = ToScreen(camera, mark.XRaw, mark.YRaw, arenaBounds);
            var layout = BloodGeometry.CreateGroundMark(mark, cameraZoom);
            if (!IsVisible(arenaBounds, center, layout.BoundingRadius))
            {
                continue;
            }

            for (var blotIndex = 0;
                 blotIndex < layout.VisibleBlotCount;
                 blotIndex++)
            {
                var blot = layout.GetBlot(blotIndex);
                DrawBlot(
                    spriteBatch,
                    pixel,
                    center + new Vector2(blot.OffsetX, blot.OffsetY),
                    blot.Radius,
                    GroundBlood * blot.Alpha);
            }
        }
    }

    public static void DrawBursts(
        ReadOnlySpan<BloodBurst> bursts,
        ReadOnlySpan<LethalSpurt> spurts,
        SpectatorCamera camera,
        Rectangle arenaBounds,
        float cameraZoom,
        SpriteBatch spriteBatch,
        Texture2D pixel)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);

        foreach (ref readonly var burst in bursts)
        {
            var layout = BloodGeometry.Create(burst, cameraZoom);
            var origin = ToScreen(camera, burst.XRaw, burst.YRaw, arenaBounds) +
                new Vector2(0f, layout.OriginOffsetY);
            if (!IsVisible(arenaBounds, origin, layout.BoundingRadius))
            {
                continue;
            }

            var color = burst.IsLethal ? LethalBlood : FreshBlood;
            for (var dropletIndex = 0;
                 dropletIndex < layout.VisibleDropletCount;
                 dropletIndex++)
            {
                var droplet = layout.GetDroplet(dropletIndex);
                DrawLine(
                    spriteBatch,
                    pixel,
                    origin + new Vector2(droplet.StartX, droplet.StartY),
                    origin + new Vector2(droplet.EndX, droplet.EndY),
                    color * droplet.Alpha,
                    droplet.Thickness);
            }
        }

        DrawSpurts(spurts, camera, arenaBounds, cameraZoom, spriteBatch, pixel);
    }

    private static void DrawSpurts(
        ReadOnlySpan<LethalSpurt> spurts,
        SpectatorCamera camera,
        Rectangle arenaBounds,
        float cameraZoom,
        SpriteBatch spriteBatch,
        Texture2D pixel)
    {
        foreach (ref readonly var spurt in spurts)
        {
            var origin = ToScreen(camera, spurt.XRaw, spurt.YRaw, arenaBounds);
            var layout = BloodGeometry.CreateSpurt(spurt, cameraZoom);
            if (!IsVisible(arenaBounds, origin, layout.BoundingRadius))
            {
                continue;
            }

            for (var strandIndex = 0;
                 strandIndex < layout.VisibleStrandCount;
                 strandIndex++)
            {
                var strand = layout.GetStrand(strandIndex);
                DrawLine(
                    spriteBatch,
                    pixel,
                    origin + new Vector2(strand.StartX, strand.StartY),
                    origin + new Vector2(strand.EndX, strand.EndY),
                    LethalBlood * strand.Alpha,
                    strand.Thickness);
            }
        }
    }

    private static Vector2 ToScreen(
        SpectatorCamera camera,
        int xRaw,
        int yRaw,
        Rectangle arenaBounds) =>
        camera.WorldToScreen(
            new Vector2(
                xRaw / (float)FixedPoint.Scale,
                yRaw / (float)FixedPoint.Scale),
            arenaBounds);

    private static bool IsVisible(
        Rectangle arenaBounds,
        Vector2 center,
        float boundingRadius)
    {
        var extent = (int)MathF.Ceiling(MathF.Max(1f, boundingRadius));
        return arenaBounds.Intersects(
            new Rectangle(
                (int)MathF.Floor(center.X) - extent,
                (int)MathF.Floor(center.Y) - extent,
                (extent * 2) + 1,
                (extent * 2) + 1));
    }

    private static void DrawBlot(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 center,
        float radius,
        Color color)
    {
        if (radius <= 0f)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            center,
            sourceRectangle: null,
            color,
            rotation: 0f,
            new Vector2(0.5f, 0.5f),
            new Vector2(
                MathF.Max(1f, radius * 2f),
                MathF.Max(1f, radius * GroundBlotFlatten)),
            SpriteEffects.None,
            layerDepth: 0f);
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
