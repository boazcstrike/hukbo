using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The pawn body atlas: where each authored head-and-torso cell sits inside
/// <c>Content/Textures/PawnBodies.png</c>, and which cell one warrior draws
/// (the 2026-08-15 pawn sprite body design, sections 3 and 4).
/// </summary>
/// <remarks>
/// <para>
/// Pure geometry and pure arithmetic. Nothing here touches a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or the loaded texture, so the
/// whole type is testable under the client presentation test rules — a test
/// may assert every cell rectangle and every selection without a GPU.
/// </para>
/// <para>
/// Nothing here reaches the simulation. Cell selection is a pure function of an
/// entity identifier and one presentation salt, on the presentation side of the
/// boundary; it moves no state hash, no event hash, no snapshot, and no
/// outcome.
/// </para>
/// </remarks>
internal static class PawnSpriteAtlas
{
    /// <summary>
    /// Authored cells in the atlas. Append-only: raising this without
    /// regenerating the texture would index past the last drawn cell and
    /// sample transparent pixels, so the count and the image are one change.
    /// </summary>
    public const int VariantCount = 50;

    /// <summary>Cells per atlas row.</summary>
    public const int Columns = 10;

    /// <summary>Rows of cells in the atlas.</summary>
    public const int Rows = 5;

    /// <summary>Width in pixels of one authored cell.</summary>
    public const int CellWidth = 112;

    /// <summary>Height in pixels of one authored cell.</summary>
    public const int CellHeight = 234;

    /// <summary>Expected atlas width, pinned so a regenerated texture that
    /// changed shape fails a test rather than drawing sheared bodies.</summary>
    public const int AtlasWidth = Columns * CellWidth;

    /// <summary>Expected atlas height, pinned on the same grounds.</summary>
    public const int AtlasHeight = Rows * CellHeight;

    /// <summary>
    /// The fraction of a cell's height occupied by the head, measured from the
    /// authored crop window: the cell spans the hair crown through the bahag
    /// waist wrap, and the neck line sits a little above its midpoint. Used to
    /// align the drawn cell against the layout's own head and torso rectangles
    /// so a sprite body stands where the procedural body stood.
    /// </summary>
    /// <remarks>
    /// Provisional tuning value, not a measurement of anything: it was read off
    /// the generator's crop window (<c>tools/gen_pawn_bodies.py</c>,
    /// <c>BODY_VIEWBOX</c>) and is expected to move if the crop moves.
    /// </remarks>
    public const float HeadFractionOfCell = 0.42f;

    /// <summary>
    /// The source rectangle of one cell, by index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="variantIndex"/> is outside
    /// <c>[0, <see cref="VariantCount"/>)</c>. Thrown rather than clamped: an
    /// out-of-range index means the caller's selection is wrong, and clamping
    /// would hide that behind a warrior who silently shares another's body.
    /// </exception>
    public static Rectangle GetCellBounds(int variantIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variantIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            variantIndex,
            VariantCount);

        return new Rectangle(
            variantIndex % Columns * CellWidth,
            variantIndex / Columns * CellHeight,
            CellWidth,
            CellHeight);
    }

    /// <summary>
    /// The cell one warrior draws, as a pure function of its entity
    /// identifier.
    /// </summary>
    /// <remarks>
    /// The same warrior draws the same body every frame it is on screen and in
    /// every replay of the same battle, which is the stability rule
    /// <see cref="PresentationHash"/> exists to serve. Mixing through that
    /// shared finalizer rather than taking the identifier modulo the count
    /// directly matters: consecutive entity identifiers are handed out in
    /// roster order, so the raw modulo would march bodies through the atlas in
    /// lockstep with the roster and put visibly identical warriors at a fixed
    /// stride across the line.
    /// </remarks>
    public static int GetVariantIndex(ulong entityId)
    {
        var mixed = PresentationHash.Mix(
            entityId ^ PresentationSalts.PawnSpriteVariantSalt);
        return (int)(mixed % VariantCount);
    }

    /// <summary>
    /// Where one warrior's cell is drawn, given the head and torso rectangles
    /// the procedural path already computed for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cell is fitted to the union's height and given the width its own
    /// aspect asks for, then centred horizontally on the torso. Fitting height
    /// first, rather than stretching to the union outright, is deliberate: the
    /// union's width varies with each warrior's build multiplier while the
    /// authored cell does not, so stretching would squash a slight warrior and
    /// smear a broad one. The stated cost, recorded in the design's section 10,
    /// is that build variation reads less strongly in sprite mode.
    /// </para>
    /// <para>
    /// The result is returned in the same unrotated screen space every
    /// <c>PawnLayout</c> rectangle uses, so the caller applies
    /// <see cref="PawnTransform"/> to it exactly as it does to a quad and the
    /// death collapse carries the sprite over with the body.
    /// </para>
    /// </remarks>
    public static Rectangle GetDestinationBounds(
        Rectangle headBounds,
        Rectangle torsoBounds)
    {
        var union = Rectangle.Union(headBounds, torsoBounds);
        if (union.Height <= 0)
        {
            return Rectangle.Empty;
        }

        var width = Math.Max(
            1,
            (int)MathF.Round(union.Height * ((float)CellWidth / CellHeight)));
        var centerX = torsoBounds.IsEmpty
            ? union.Center.X
            : torsoBounds.Center.X;

        return new Rectangle(
            centerX - (width / 2),
            union.Top,
            width,
            union.Height);
    }
}
