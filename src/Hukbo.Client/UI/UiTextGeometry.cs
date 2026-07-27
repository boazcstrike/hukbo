using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Pure whole-pixel text placement geometry. A <c>SpriteFont</c> bake has no
/// hinting and no subpixel rendering, so drawing a glyph atlas at a
/// fractional screen coordinate makes its texels straddle a pixel boundary —
/// under linear filtering that reads as a soft, swimming blur, and under
/// point filtering it reads as inconsistent stem weights between adjacent
/// letters. Every text draw in the client routes through this helper so a
/// position is always rounded to a whole pixel before it reaches
/// <c>SpriteBatch.DrawString</c>.
/// </summary>
internal static class UiTextGeometry
{
    /// <summary>
    /// Rounds a position to the nearest whole pixel on both axes.
    /// </summary>
    public static Vector2 SnapToPixel(Vector2 position) =>
        new(MathF.Round(position.X), MathF.Round(position.Y));

    /// <summary>
    /// Derives a whole-pixel top-left draw origin that centres a
    /// <paramref name="measuredSize"/> string on <paramref name="center"/>.
    /// The halving happens before rounding, so the result is the same
    /// whole-pixel corner a caller would reach by measuring, centring, and
    /// only then snapping — not a snap-then-centre that could reintroduce a
    /// fractional offset.
    /// </summary>
    public static Vector2 GetCenteredTopLeft(Vector2 measuredSize, Vector2 center) =>
        SnapToPixel(center - (measuredSize / 2f));
}
