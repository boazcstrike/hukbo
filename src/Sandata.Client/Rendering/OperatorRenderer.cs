using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Sandata.Client.Rendering;

/// <summary>
/// Draws an <see cref="OperatorLayout"/>. Every decision this type makes has
/// already been made by <see cref="OperatorGeometry.Create"/>; this class
/// only turns the resulting rectangles and points into
/// <c>spriteBatch.Draw</c> calls, the same split
/// <c>Hukbo.Client.Rendering.PawnRenderer.DrawLayout</c>
/// (<c>src/Hukbo.Client/Rendering/PawnRenderer.cs:267-463</c>) keeps from
/// <c>PawnGeometry</c>. Nothing here is unit tested — it cannot be, without a
/// <c>GraphicsDevice</c> and a <c>SpriteBatch</c> — and nothing here may
/// contain a decision an <c>OperatorGeometryTests</c> fact could pin
/// instead.
/// </summary>
internal static class OperatorRenderer
{
    /// <summary>
    /// Draws every one of the sixteen layers on <paramref name="layout"/>
    /// that is not <see cref="Rectangle.Empty"/>, using
    /// <paramref name="pixel"/> — a shared one-by-one texture, the same
    /// convention every draw in <c>Hukbo.Client.Rendering.PawnRenderer</c>
    /// and <c>Sandata.Client.Rendering.WorldRenderer</c> already follows —
    /// stretched into each rectangle. The weapon body is drawn rotated about
    /// <see cref="OperatorLayout.WeaponGripAnchor"/> rather than about its
    /// own center, exactly the pivot <c>PawnRenderer.DrawRotatedBlock</c>
    /// (<c>src/Hukbo.Client/Rendering/PawnRenderer.cs:1095-1121</c>) uses for
    /// a shield sub-element that has to stay rigid with the rest of the
    /// shield as it turns.
    /// </summary>
    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        OperatorLayout layout,
        Color bodyColor,
        Color weaponColor,
        Color muzzleFlashColor,
        Color selectionColor)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);

        DrawGroundRing(spriteBatch, pixel, layout, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.BootsBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.LegsBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.TorsoBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.PlateCarrierBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.ArmsBounds, bodyColor);

        var rotationRadians = layout.DisplayRotationRawUnits /
            Bam16UnitsPerTurn * MathF.Tau;
        DrawRotatedBlock(
            spriteBatch,
            pixel,
            layout.WeaponBodyBounds,
            layout.WeaponGripAnchor,
            rotationRadians,
            weaponColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.WeaponForegripBounds, weaponColor);

        DrawIfNotEmpty(spriteBatch, pixel, layout.HeadBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.HeadPipBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.HelmetBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.NightVisionMountBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.MuzzleFlashBounds, muzzleFlashColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.SlingBounds, bodyColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.SuppressionBracketBounds, weaponColor);
        DrawIfNotEmpty(spriteBatch, pixel, layout.SelectionRingBounds, selectionColor);
    }

    // Sandata.Core.Mathematics.Bam16.UnitsPerTurn's value, restated here as a
    // float constant so this file needs no dependency on Sandata.Core beyond
    // the OperatorLayout it already takes. OperatorGeometryTests pins the
    // relationship between DisplayRotationRawUnits and Bam16 directly, so a
    // mismatch here cannot hide behind an untested draw path.
    private const float Bam16UnitsPerTurn = 65_536f;

    private static void DrawIfNotEmpty(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color)
    {
        if (bounds != Rectangle.Empty)
        {
            spriteBatch.Draw(pixel, bounds, color);
        }
    }

    /// <summary>
    /// Draws <see cref="OperatorLayout.GroundRingBounds"/> plainly whenever
    /// <see cref="OperatorLayout.GroundRingRotationRadians"/> is zero — the
    /// friendly case, and the only case that shipped before faction shape
    /// existed — and rotated about the ring's own center, via
    /// <see cref="DrawRotatedBlock"/>, whenever it is not: the hostile
    /// diamond.
    /// </summary>
    private static void DrawGroundRing(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        OperatorLayout layout,
        Color color)
    {
        if (layout.GroundRingBounds == Rectangle.Empty)
        {
            return;
        }

        if (layout.GroundRingRotationRadians == 0f)
        {
            spriteBatch.Draw(pixel, layout.GroundRingBounds, color);
            return;
        }

        var pivot = new Vector2(layout.GroundRingBounds.Center.X, layout.GroundRingBounds.Center.Y);
        DrawRotatedBlock(spriteBatch, pixel, layout.GroundRingBounds, pivot, layout.GroundRingRotationRadians, color);
    }

    /// <summary>
    /// Draws <paramref name="localBounds"/> rotated by
    /// <paramref name="rotationRadians"/> about <paramref name="pivot"/>
    /// rather than about its own center, so a weapon body offset from its
    /// grip still swings around the grip rather than around its own middle.
    /// </summary>
    private static void DrawRotatedBlock(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle localBounds,
        Vector2 pivot,
        float rotationRadians,
        Color color)
    {
        var localCenter =
            new Vector2(localBounds.Center.X, localBounds.Center.Y) - pivot;
        var cosine = MathF.Cos(rotationRadians);
        var sine = MathF.Sin(rotationRadians);
        var rotatedCenter = new Vector2(
            (localCenter.X * cosine) - (localCenter.Y * sine),
            (localCenter.X * sine) + (localCenter.Y * cosine));

        spriteBatch.Draw(
            pixel,
            pivot + rotatedCenter,
            sourceRectangle: null,
            color,
            rotationRadians,
            new Vector2(0.5f, 0.5f),
            new Vector2(localBounds.Width, localBounds.Height),
            SpriteEffects.None,
            layerDepth: 0f);
    }
}
