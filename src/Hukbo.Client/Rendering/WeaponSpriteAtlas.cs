using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The weapon and shield sprite atlas: where each authored cell sits inside
/// <c>Content/Textures/WeaponSprites.png</c>, and which cell one warrior draws
/// (the 2026-08-15 weapon sprite design, sections 4 and 5).
/// </summary>
/// <remarks>
/// <para>
/// Pure geometry and pure arithmetic, mirroring <see cref="PawnSpriteAtlas"/>'s
/// own shape. Nothing here touches a <c>GraphicsDevice</c>, a
/// <c>SpriteBatch</c>, or the loaded texture, so the whole type is testable
/// under the client presentation test rules — a test may assert every cell
/// rectangle and every selection without a GPU.
/// </para>
/// <para>
/// Nothing here reaches the simulation. Row selection comes only from
/// <c>PawnAppearance.WeaponRole</c> and <c>PawnAppearance.ShieldRole</c>,
/// themselves resolved from the authoritative loadout; variant selection
/// within a row is a pure function of an entity identifier and
/// <see cref="PresentationSalts.WeaponSpriteVariantSalt"/>. Neither moves a
/// state hash, an event hash, a snapshot, or an outcome.
/// </para>
/// </remarks>
internal static class WeaponSpriteAtlas
{
    /// <summary>Cells per atlas row: one column per authored variant.</summary>
    public const int Columns = 10;

    /// <summary>
    /// Rows of cells in the atlas: the seven weapon roles plus the tall
    /// hardwood shield.
    /// </summary>
    public const int Rows = 8;

    /// <summary>Width in pixels of one authored cell.</summary>
    public const int CellWidth = 112;

    /// <summary>Height in pixels of one authored cell.</summary>
    public const int CellHeight = 256;

    /// <summary>Expected atlas width, pinned so a regenerated texture that
    /// changed shape fails a test rather than drawing sheared weapons.</summary>
    public const int AtlasWidth = Columns * CellWidth;

    /// <summary>Expected atlas height, pinned on the same grounds.</summary>
    public const int AtlasHeight = Rows * CellHeight;

    /// <summary>
    /// The transparent margin reserved on every side of a cell, so linear
    /// filtering (mipmaps are off, per the content pipeline stanza) never
    /// samples across a cell boundary into a neighbouring role's row (design
    /// section 4).
    /// </summary>
    public const int GutterPixels = 4;

    /// <summary>
    /// The drawn content box width for the seven weapon rows: the cell width
    /// less a gutter on both sides.
    /// </summary>
    public const int WeaponContentWidth = CellWidth - (2 * GutterPixels);

    /// <summary>
    /// The drawn content box height, shared by every row including the
    /// shield row: the cell height less a gutter on both sides.
    /// </summary>
    public const int WeaponContentHeight = CellHeight - (2 * GutterPixels);

    /// <summary>
    /// The shield row's own, narrower content box width — 248 × 4 ÷ 11
    /// rounds to 90, keeping the shield's authored 4:11 proportion honest
    /// rather than stretching it to the weapon-shaped box (design section 4).
    /// </summary>
    public const int ShieldContentWidth = 90;

    /// <summary>
    /// The shield row's content box height. Identical to
    /// <see cref="WeaponContentHeight"/>: only the shield's width is
    /// special-cased, not its height.
    /// </summary>
    public const int ShieldContentHeight = WeaponContentHeight;

    /// <summary>
    /// The horizontal gutter either side of the shield row's content box:
    /// wider than <see cref="GutterPixels"/> because the shield's content box
    /// is narrower than the cell it sits inside.
    /// </summary>
    public const int ShieldHorizontalGutterPixels = (CellWidth - ShieldContentWidth) / 2;

    /// <summary>
    /// The atlas row index holding the tall hardwood shield's ten cells, one
    /// past the seven weapon rows.
    /// </summary>
    public const int ShieldRow = 7;

    /// <summary>
    /// The rotation, in radians, the renderer must add to a weapon line's own
    /// <c>atan2(delta.Y, delta.X)</c> angle before drawing a weapon sprite
    /// rotated about the grip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Worked derivation, because the design document's prose states the
    /// opposite sign and that prose is wrong.</b> Every weapon cell is
    /// authored pointing up: the grip sits at the bottom of the content box
    /// (the larger-Y, later row) and the tip runs to the top (the smaller-Y,
    /// earlier row). The renderer draws the sprite with its origin pinned at
    /// the grip, so the cell's own local "grip to tip" vector, before any
    /// rotation, is <c>(0, -1)</c> in the texture's row/column axes — pointing
    /// toward smaller Y.
    /// </para>
    /// <para>
    /// MonoGame's <c>SpriteBatch.Draw</c> rotates a local offset
    /// <c>(x, y)</c> by <c>(x cosθ − y sinθ, x sinθ + y cosθ)</c>, the same
    /// formula <c>PawnRenderer.DrawLine</c> already relies on when it rotates
    /// its own local <c>(1, 0)</c> by <c>atan2(delta.Y, delta.X)</c> and lands
    /// exactly on <c>delta</c>'s direction. Solving that same formula for our
    /// local <c>(0, -1)</c> to land on <c>(cosφ, sinφ)</c>, where
    /// <c>φ = atan2(delta.Y, delta.X)</c>, gives <c>θ = φ + π/2</c> — not
    /// <c>φ − π/2</c>. A direct sanity check confirms the sign: a weapon
    /// pointing straight up on screen has <c>delta = (0, −L)</c>, so
    /// <c>φ = atan2(−L, 0) = −π/2</c>, and the sprite is already authored
    /// pointing up, so it needs <b>zero</b> added rotation. Only
    /// <c>θ = φ + π/2 = −π/2 + π/2 = 0</c> reaches that; <c>φ − π/2</c> would
    /// give <c>−π</c>, a full flip, which is visibly wrong for a weapon that
    /// already points the way it is drawn.
    /// </para>
    /// </remarks>
    public const float WeaponSpriteRotationOffsetRadians = MathF.PI / 2f;

    /// <summary>
    /// The atlas row bound to one weapon role. The one deliberately explicit
    /// table design section 4 and the plan both ask for: it happens to agree
    /// with <see cref="PawnWeaponRole"/>'s own declaration order today, but
    /// that is a coincidence of the roster's history, not a contract, so the
    /// mapping is written out rather than taken from
    /// <c>(int)role</c>. This is also the row's binding to the existing
    /// weapon visual catalog entry (design section 8): the row is bound to
    /// the role, and <c>WeaponVisualCatalog.PawnSilhouette(role)</c> is
    /// already the role's one catalog entry, so no second lookup table is
    /// needed here to satisfy that binding.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="role"/> is not a defined <see cref="PawnWeaponRole"/>.
    /// </exception>
    public static int GetRowForWeaponRole(PawnWeaponRole role) => role switch
    {
        PawnWeaponRole.Kampilan => 0,
        PawnWeaponRole.Wasay => 1,
        PawnWeaponRole.Kalis => 2,
        PawnWeaponRole.Itak => 3,
        PawnWeaponRole.Bangkaw => 4,
        PawnWeaponRole.Busog => 5,
        PawnWeaponRole.Arquebus => 6,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role), role, "Unrecognized weapon role."),
    };

    /// <summary>
    /// The atlas row bound to one shield role. Only
    /// <see cref="PawnShieldRole.TallHardwood"/> has authored cells;
    /// <see cref="PawnShieldRole.None"/> means no shield is drawn at all and
    /// is never a valid row lookup.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="role"/> is <see cref="PawnShieldRole.None"/> or is
    /// not a defined <see cref="PawnShieldRole"/>.
    /// </exception>
    public static int GetRowForShieldRole(PawnShieldRole role) => role switch
    {
        PawnShieldRole.TallHardwood => ShieldRow,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role), role, "No atlas row for this shield role."),
    };

    /// <summary>
    /// The full cell rectangle — gutter included — at <paramref name="row"/>
    /// and <paramref name="variantIndex"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="row"/> is outside <c>[0, <see cref="Rows"/>)</c>
    /// or <paramref name="variantIndex"/> is outside
    /// <c>[0, <see cref="Columns"/>)</c>.
    /// </exception>
    public static Rectangle GetCellBounds(int row, int variantIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        ArgumentOutOfRangeException.ThrowIfNegative(variantIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(variantIndex, Columns);

        return new Rectangle(
            variantIndex * CellWidth,
            row * CellHeight,
            CellWidth,
            CellHeight);
    }

    /// <summary>
    /// The drawn content box for one weapon role's cell — the source
    /// rectangle the renderer must sample, not the full cell. Sourcing the
    /// whole cell would let linear filtering pull the four-pixel transparent
    /// gutter, and through it a faint sliver of the neighbouring role's row,
    /// into the drawn sample (design section 4).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="role"/> is not a defined <see cref="PawnWeaponRole"/>
    /// or <paramref name="variantIndex"/> is outside
    /// <c>[0, <see cref="Columns"/>)</c>.
    /// </exception>
    public static Rectangle GetSourceBounds(PawnWeaponRole role, int variantIndex)
    {
        var cell = GetCellBounds(GetRowForWeaponRole(role), variantIndex);
        return new Rectangle(
            cell.X + GutterPixels,
            cell.Y + GutterPixels,
            WeaponContentWidth,
            WeaponContentHeight);
    }

    /// <summary>
    /// The drawn content box for one shield role's cell, narrower than a
    /// weapon row's content box on purpose (design section 4).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="role"/> is not <see cref="PawnShieldRole.TallHardwood"/>
    /// or <paramref name="variantIndex"/> is outside
    /// <c>[0, <see cref="Columns"/>)</c>.
    /// </exception>
    public static Rectangle GetShieldSourceBounds(PawnShieldRole role, int variantIndex)
    {
        var cell = GetCellBounds(GetRowForShieldRole(role), variantIndex);
        return new Rectangle(
            cell.X + ShieldHorizontalGutterPixels,
            cell.Y + GutterPixels,
            ShieldContentWidth,
            ShieldContentHeight);
    }

    /// <summary>
    /// The variant column one warrior draws within its role's row, as a pure
    /// function of its entity identifier.
    /// </summary>
    /// <remarks>
    /// Mixed through the shared <see cref="PresentationHash"/> finalizer
    /// rather than taken as a raw modulo of the entity identifier: consecutive
    /// entity identifiers are handed out in roster order, so a raw modulo
    /// would march variants across the row in lockstep with the roster and
    /// put visibly identical weapons at a fixed stride across the line.
    /// </remarks>
    public static int GetVariantIndex(ulong entityId)
    {
        var mixed = PresentationHash.Mix(
            entityId ^ PresentationSalts.WeaponSpriteVariantSalt);
        return (int)(mixed % Columns);
    }
}
