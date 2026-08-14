"""Arquebus -- ten authored variants for row 6 (`frame.ROLE_ROWS[6]`).

Historical constraint (CLAUDE.md section 7, design section 6,
`WeaponVisualCatalog.ArquebusA1`): Escalante Alvarado (1548) records
"a few arquebuses" near Sarangani/Mindanao circa 1543-45; Legazpi
shipped a physical Chinese arquebus from Cebu, 15 July 1567. Evidence
tier: Documented, form uncertain. **No local name was located in any
source consulted.** `PawnAppearance.WeaponLabel` therefore carries the
plain, unpaired "Imported Arquebus" -- the pair-form rule never
engages because there is no cultural identification to pair.

A consequence of that binds this module uniquely among the eight
rows: the weapon carries **no cultural badge, no regional marking, no
local ornament, no decorative tradition of any kind, in any variant**.
An imported object carries no local identity, and inventing one would
be exactly the unattested cultural identification CLAUDE.md section 7
forbids. This is the plainest row in the atlas by policy, not by
accident. The `IMPORTED` badge and matchcord glow the catalog
mentions are appearance-layer / UI features, never baked into the
silhouette.

Geometry (SECOND rework -- the first rework fixed a flared skirt; the
row still read as "a thin grey rod standing on a small dark block", a
signpost or hammer, because the first pass stacked stock and barrel
end-to-end along the axis instead of overlapping them). The renderer
draws only the segment from `WeaponStart` (the grip) to `WeaponEnd`
(the muzzle) -- nothing behind the hand is representable, so there is
no butt-stock to draw and no flared base to compensate with. The
cell's vertical axis, bottom to top, IS the barrel axis. A real
fore-stock wraps the LOWER portion of the barrel rather than sitting
below it, so for that span the two occupy the same axial range. Read
along it:

  - The barrel is drawn full length, grip to muzzle, as a single
    narrow dark iron tube -- it exists along the whole axis, not just
    the upper stretch.
  - The fore-stock is drawn on top of the barrel's lower ~55% (see
    `_Variant.stock_fraction`), clearly wider than the barrel --
    2.5-3x its width at the transition -- so that span reads as WOOD
    with the barrel hidden inside/under it, never as a block
    beneath a rod.
  - Above the stock's forward (muzzle-side) end, the barrel continues
    bare for the remaining ~45% of the axis, ending in a slight
    ring/thickening at the muzzle so the end reads as an opening.
  - The stock/bare-barrel transition is the single most important
    line in the drawing: the stock's wide silhouette ends abruptly at
    `transition_v` in a clean horizontal step, reinforced on "band"
    variants by a plain iron ferrule collar sitting right at that
    step.
  - A plain, unshaped lock plate sits offset to one side, at the REAR
    of the stock span (near the grip, not the transition), on
    variants that carry `lock_size`; it is the strongest single cue
    that this is a firearm rather than a pole, and it stays
    unornamented, per the no-decoration constraint above.

Variation stays inside this one plain form (design section 7): stock
timber tone and grain, stock taper and width, barrel length and bore,
barrel patina (fresh/blued vs worn iron), ferrule presence, lock-plate
presence and size, wear, powder staining, and the stock/barrel split
ratio itself (`stock_fraction`, roughly 0.50-0.62 of the axial span).

The weapon is the shortest in the game at roughly 9.5 layout units
against Kalis/Kampilan's 19.1, but that shortness is encoded entirely
by `WeaponLine`, the renderer's own scale factor
(`drawnPixels = artPixels * (lineLength / 248)`), never by the art.
Every variant below therefore fills nearly the FULL content height --
grip fixed at `v=248`, muzzle within `v=1..15` -- so the drawn axial
extent is ~233-248px of the 248px box, matching the fill every other
weapon row uses (Kampilan measured 241px, Itak 233px, Busog ~239px).
Filling only a fraction of the box a second time here would halve the
weapon on screen, since the renderer already applies the short-line
scale on top of whatever the art draws.

Coordinate convention: every point below is `(u, v)` in the weapon
content box's own local space, `u` in `[0, WEAPON_CONTENT_WIDTH]`
(104) and `v` in `[0, WEAPON_CONTENT_HEIGHT]` (248), with `v` growing
downward. `_pt` maps a local point through `box.left`/`box.top` into
the cell's absolute SVG coordinates -- `WEAPON_BOX` has `left=4,
top=4`, not `0,0`. `box.grip_anchor` is `(52, 248)` in local space
(bottom-centre); the stock is centred on the same axis so the sprite
pivots naturally about the hand grip.
"""

from __future__ import annotations

from tools.weapon_sprites import register
from tools.weapon_sprites.frame import ContentBox, close_cell_svg, open_cell_svg
from tools.weapon_sprites.palette import (
    CHARRED_WOOD_BROWN,
    IRON_WORN_GREY,
    PALM_RATTAN_OCHRE,
    RGB,
    rgb,
    rgba,
    shade,
)

Point = tuple[float, float]

# Fixed horizontal axis the whole weapon is centred on -- the true
# local centre of the 104-wide content box (WEAPON_CONTENT_WIDTH / 2).
# There is no flared heel needing extra clearance any more, so the
# axis sits on the box's true centre rather than offset from it.
_BARREL_CX = 52.0

# The grip end of the axis -- fixed for every variant; `box.grip_anchor`
# local-space `v` matches this.
_GRIP_V = 248.0


class _Variant:
    """One cell's authored parameters. The barrel is a single tapered
    tube running the FULL axis, grip (`v=248`) to muzzle (`muzzle_v`),
    centred on `_BARREL_CX`. The stock is a second, much wider
    trapezoid drawn ON TOP of the barrel's lower `stock_fraction` of
    that same axial span -- from the grip up to `transition_v` (see
    `_Variant.transition_v`) -- so stock and barrel overlap along the
    axis rather than sitting end-to-end. `lock_size` is `None` for no
    lock plate at all; every lock is a bare rectangle, intentionally
    unshaped and uninlaid, per the module docstring."""

    def __init__(
        self,
        stock_fraction: float,
        stock_w_bottom: float,
        stock_w_top: float,
        barrel_w_breech: float,
        barrel_w_muzzle: float,
        muzzle_v: float,
        stock_tone: RGB,
        barrel_shade: float,
        grain_lines: int,
        ramrod: bool,
        band: bool,
        lock_size: float | None,
        wear: str,
        powder_stain: bool,
        note: str,
    ) -> None:
        self.stock_fraction = stock_fraction
        self.stock_w_bottom = stock_w_bottom
        self.stock_w_top = stock_w_top
        self.barrel_w_breech = barrel_w_breech
        self.barrel_w_muzzle = barrel_w_muzzle
        self.muzzle_v = muzzle_v
        self.stock_tone = stock_tone
        self.barrel_shade = barrel_shade
        self.grain_lines = grain_lines
        self.ramrod = ramrod
        self.band = band
        self.lock_size = lock_size
        self.wear = wear
        self.powder_stain = powder_stain
        self.note = note
        # The step where the wide stock ends and the bare barrel
        # begins -- the single most important line in the drawing.
        self.transition_v = _GRIP_V - stock_fraction * (_GRIP_V - muzzle_v)


# Variants 0-6 vary the stock/barrel split ratio, stock taper, barrel
# bore, and furniture; 7-9 are recolour/wear passes over variant 0's
# baseline geometry, per the same convention every sibling module
# uses. `muzzle_v` is now pinned close to 0 for every variant so the
# DRAWN object always spans ~233-248px of the 248px content height --
# the renderer, not the art, is what encodes this weapon's true
# shortness relative to the other rows (`WeaponLine` scales the whole
# cell to the true grip-to-muzzle length; a short-filled cell would
# draw as a half-length weapon on the line). Every variant is checked
# to keep stock_w_top roughly 2.5-3x the barrel's width AT the
# transition (stock_w_top divided by the barrel width interpolated at
# `transition_v`, independent of `muzzle_v`) -- see the ratio noted in
# each comment below.
_VARIANTS: tuple[_Variant, ...] = (
    _Variant(  # 0 -- baseline, 55% stock, worn-iron barrel, ferrule, mid lock; ratio 2.66x
        0.55, 24, 21, 9, 7, 8,
        CHARRED_WOOD_BROWN, 0.05, 2, False, True, 1.0, "light", False,
        "baseline proportions; 55% stock overlap; worn-iron-grey barrel; plain ferrule at the transition",
    ),
    _Variant(  # 1 -- finest-bore barrel, lowest stock fraction (50%), fresh/blued iron, ramrod; ratio 2.83x
        0.50, 20, 17, 7, 5, 1,
        CHARRED_WOOD_BROWN, -0.30, 2, True, True, 0.7, "none", False,
        "finest-bore barrel; lowest stock fraction in the row, so the longest bare-barrel run; fresh blued-iron tone; ramrod carried",
    ),
    _Variant(  # 2 -- heaviest bore, 60% stock, worn iron; ratio 2.5x
        0.60, 30, 27, 12, 10, 15,
        CHARRED_WOOD_BROWN, 0.10, 1, False, True, 1.0, "light", False,
        "heaviest-bore barrel; broad 60% stock wrap, so the shortest bare-barrel run; worn-grey patina; light wear",
    ),
    _Variant(  # 3 -- broadest stock in row (widest cell), 58% stock; ratio 3.3x
        0.58, 34, 26, 9, 7, 6,
        CHARRED_WOOD_BROWN, 0.0, 3, False, False, 1.4, "none", False,
        "broadest fore-stock in row; large plain lock plate; no ferrule, no ramrod",
    ),
    _Variant(  # 4 -- slenderest stock, thicker bore, 50% stock, ramrod; ratio 2.71x
        0.50, 21, 19, 8, 6, 10,
        CHARRED_WOOD_BROWN, -0.15, 1, True, True, 0.7, "none", False,
        "slenderest fore-stock in the row; thicker-than-baseline bore; ramrod carried",
    ),
    _Variant(  # 5 -- pale fresh timber stock, blued barrel, prominent lock, 55% stock; ratio 2.53x
        0.55, 23, 20, 9, 7, 4,
        PALM_RATTAN_OCHRE, -0.30, 2, True, True, 1.4, "none", False,
        "pale fresh-timber stock tone (not the usual charred wood); blued barrel; ramrod",
    ),
    _Variant(  # 6 -- highest stock fraction in the row (60%); ratio 2.79x
        0.60, 22, 19, 8, 6, 12,
        CHARRED_WOOD_BROWN, 0.10, 2, False, False, 1.0, "light", False,
        "highest stock fraction in the row; slim bore kept for width contrast",
    ),
    _Variant(  # 7 -- recolour/wear only, baseline outline
        0.55, 24, 21, 9, 7, 8,
        CHARRED_WOOD_BROWN, 0.10, 2, False, True, 0.7, "moderate", False,
        "recolour/wear only, baseline outline; moderate wear, small lock",
    ),
    _Variant(  # 8 -- recolour/wear only, baseline outline
        0.55, 24, 21, 9, 7, 8,
        shade(CHARRED_WOOD_BROWN, -0.25), -0.30, 3, False, True, 1.0, "none", True,
        "recolour/wear only, baseline outline; darker wood, blued barrel, powder staining",
    ),
    _Variant(  # 9 -- recolour/wear only, baseline outline, heaviest wear
        0.55, 24, 21, 9, 7, 8,
        CHARRED_WOOD_BROWN, 0.15, 1, False, False, 0.4, "heavy", True,
        "recolour/wear only, baseline outline; heaviest wear: faded ferrule, nicked stock, powder staining, small worn lock",
    ),
)


def _pt(box: ContentBox, u: float, v: float) -> Point:
    return (box.left + u, box.top + v)


def _poly_attr(points: tuple[Point, ...], box: ContentBox) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in (_pt(box, u, v) for u, v in points))


def _polygon(points: tuple[Point, ...], box: ContentBox, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{opacity}"' if opacity < 1.0 else ""
    return f'<polygon points="{_poly_attr(points, box)}" fill="{fill}"{op} />'


def _stock_width_at(v: _Variant, vy: float) -> float:
    """Interpolate the stock's authored width at local-space `vy`,
    clamped to the stock's own span (`transition_v` to `_GRIP_V`)."""
    span = _GRIP_V - v.transition_v
    if span <= 0:
        return v.stock_w_top
    t = (_GRIP_V - vy) / span
    t = max(0.0, min(1.0, t))
    return v.stock_w_bottom + (v.stock_w_top - v.stock_w_bottom) * t


def _draw_barrel(box: ContentBox, v: _Variant) -> list[str]:
    """The barrel drawn FULL length, grip to muzzle -- it is not a
    second segment stacked above the stock, it is the tube the stock
    wraps for its lower span. Drawn first so the wider stock polygon
    painted on top of it hides the wrapped portion entirely."""
    base_tone = shade(IRON_WORN_GREY, v.barrel_shade - 0.15)
    base = rgb(base_tone)
    shadow = rgba(shade(base_tone, -0.25), 0.45)
    highlight = rgba(shade(base_tone, 0.18), 0.40)
    bl_breech = _BARREL_CX - v.barrel_w_breech / 2
    br_breech = _BARREL_CX + v.barrel_w_breech / 2
    bl_muzzle = _BARREL_CX - v.barrel_w_muzzle / 2
    br_muzzle = _BARREL_CX + v.barrel_w_muzzle / 2
    barrel: tuple[Point, ...] = (
        (bl_breech, _GRIP_V), (br_breech, _GRIP_V),
        (br_muzzle, v.muzzle_v), (bl_muzzle, v.muzzle_v),
    )
    lines = [_polygon(barrel, box, base)]
    # Shading kept narrow and low-contrast on purpose: a wide bright
    # sliver reads as a blade's edge highlight, not a round tube.
    lines.append(
        _polygon(
            ((bl_breech, _GRIP_V), (bl_breech + (br_breech - bl_breech) * 0.25, _GRIP_V),
             (bl_muzzle + (br_muzzle - bl_muzzle) * 0.25, v.muzzle_v), (bl_muzzle, v.muzzle_v)),
            box, shadow,
        )
    )
    lines.append(
        _polygon(
            ((br_breech - (br_breech - bl_breech) * 0.22, _GRIP_V), (br_breech, _GRIP_V),
             (br_muzzle, v.muzzle_v), (br_muzzle - (br_muzzle - bl_muzzle) * 0.22, v.muzzle_v)),
            box, highlight,
        )
    )
    # Muzzle ring: a slight thickening past the bore's top edge so the
    # end reads as an opening rather than a cut-off stick.
    cap_tone = rgb(shade(base_tone, -0.50))
    cx0, cy0 = _pt(box, bl_muzzle - 1.5, v.muzzle_v)
    lines.append(f'<rect x="{cx0:.1f}" y="{cy0:.1f}" width="{br_muzzle - bl_muzzle + 3:.1f}" height="4.5" fill="{cap_tone}" />')
    return lines


def _draw_stock(box: ContentBox, v: _Variant) -> list[str]:
    """The fore-stock, drawn ON TOP of the barrel's lower span (grip
    to `transition_v`) -- wider than the barrel underneath by
    ~2.5-3x, so this span reads as WOOD with the barrel hidden inside
    it rather than as a block sitting below a rod."""
    base = rgb(v.stock_tone)
    shadow = rgba(shade(v.stock_tone, -0.32), 0.55)
    highlight = rgba(shade(v.stock_tone, 0.30), 0.45)
    left_bottom = _BARREL_CX - v.stock_w_bottom / 2
    right_bottom = _BARREL_CX + v.stock_w_bottom / 2
    left_top = _BARREL_CX - v.stock_w_top / 2
    right_top = _BARREL_CX + v.stock_w_top / 2
    stock: tuple[Point, ...] = (
        (left_bottom, _GRIP_V), (left_top, v.transition_v),
        (right_top, v.transition_v), (right_bottom, _GRIP_V),
    )
    lines = [_polygon(stock, box, base)]
    # Shadow along the barrel-facing (left) edge, highlight along the
    # opposite (right) edge -- the two-to-three value bands the
    # authoring guidance asks for, no longer an asymmetric flare.
    lines.append(
        _polygon(
            ((left_bottom, _GRIP_V), (left_top, v.transition_v),
             (left_top + 4, v.transition_v), (left_bottom + 6, _GRIP_V)),
            box, shadow,
        )
    )
    lines.append(
        _polygon(
            ((right_bottom - 6, _GRIP_V), (right_top - 4, v.transition_v),
             (right_top, v.transition_v), (right_bottom, _GRIP_V)),
            box, highlight,
        )
    )
    grain_tone = rgba(shade(v.stock_tone, -0.18), 0.30)
    if v.grain_lines > 0:
        span = (right_bottom - left_bottom) / (v.grain_lines + 1)
        for i in range(1, v.grain_lines + 1):
            gx0, gy0 = _pt(box, left_bottom + span * i, v.transition_v + 6)
            gx1, gy1 = _pt(box, left_bottom + span * i * 0.9, 244)
            lines.append(f'<line x1="{gx0:.1f}" y1="{gy0:.1f}" x2="{gx1:.1f}" y2="{gy1:.1f}" stroke="{grain_tone}" stroke-width="1.4" />')
    if v.wear in ("moderate", "heavy"):
        nick_tone = rgba(shade(v.stock_tone, -0.45), 0.85)
        nx = right_bottom - 10
        lines.append(_polygon(((nx, 236), (nx + 5, 233), (nx + 2, 244)), box, nick_tone))
    if v.wear == "heavy":
        nick_tone = rgba(shade(v.stock_tone, -0.45), 0.85)
        nx2 = left_bottom + 8
        lines.append(_polygon(((nx2, 226), (nx2 + 5, 223), (nx2 + 1, 236)), box, nick_tone))
    return lines


def _draw_transition_band(box: ContentBox, v: _Variant) -> list[str]:
    """Ferrule collar at the wood-to-metal step, drawn AFTER the
    stock so it sits visibly on top of the transition line -- sized
    to the barrel's own width there, not the stock's, so it reads as
    a ring around the tube rather than a crossguard."""
    if not v.band:
        return []
    barrel_w_here = v.barrel_w_breech + (v.barrel_w_muzzle - v.barrel_w_breech) * v.stock_fraction
    band_tone = rgb(shade(IRON_WORN_GREY, -0.40))
    bw = barrel_w_here + 5.0
    bx0, by0 = _pt(box, _BARREL_CX - bw / 2, v.transition_v - 2.5)
    return [f'<rect x="{bx0:.1f}" y="{by0:.1f}" width="{bw:.1f}" height="5.5" fill="{band_tone}" rx="1" />']


def _draw_ramrod(box: ContentBox, v: _Variant) -> list[str]:
    """A thin accessory rod carried alongside the barrel, drawn AFTER
    the stock so it stays visible across both the wrapped and bare
    spans -- nearly the full axial length now that the barrel is."""
    if not v.ramrod:
        return []
    rod_tone = rgb(shade(v.stock_tone, -0.15))
    rx0, ry0 = _pt(box, _BARREL_CX + 3.5, _GRIP_V - 10)
    rx1, ry1 = _pt(box, _BARREL_CX + 3, v.muzzle_v + 6)
    return [f'<line x1="{rx0:.1f}" y1="{ry0:.1f}" x2="{rx1:.1f}" y2="{ry1:.1f}" stroke="{rod_tone}" stroke-width="2.2" />']


def _draw_lock(box: ContentBox, v: _Variant) -> list[str]:
    """The lock plate sits at the REAR of the stock span -- near the
    grip, not the wood-to-metal transition -- offset to one side."""
    if v.lock_size is None:
        return []
    tone = rgba(shade(IRON_WORN_GREY, -0.30), 0.85)
    w = 12.0 * v.lock_size
    h = 8.0 * v.lock_size
    ly = _GRIP_V - 0.28 * (_GRIP_V - v.transition_v) - h * 0.5
    stock_w_here = _stock_width_at(v, ly)
    lx = _BARREL_CX + stock_w_here / 2 - 2.0
    x0, y0 = _pt(box, lx, ly)
    return [f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{w:.1f}" height="{h:.1f}" fill="{tone}" rx="1" />']


def _draw_powder_stain(box: ContentBox, v: _Variant) -> list[str]:
    """Staining sits by the pan, next to the lock plate at the rear
    of the stock span."""
    if not v.powder_stain:
        return []
    stain = rgba(shade(CHARRED_WOOD_BROWN, -0.35), 0.30)
    ly = _GRIP_V - 0.28 * (_GRIP_V - v.transition_v)
    cx, cy = _pt(box, _BARREL_CX + 6, ly + 4)
    return [f'<ellipse cx="{cx:.1f}" cy="{cy:.1f}" rx="9" ry="6" fill="{stain}" />']


def build(index: int, box: ContentBox) -> str:
    """Return one complete SVG document string for Arquebus variant
    `index` (0-9), drawn inside `box` per the module docstring."""
    if not 0 <= index <= 9:
        raise ValueError(f"variant index out of range: {index!r}")
    v = _VARIANTS[index]

    lines: list[str] = open_cell_svg()
    lines += _draw_barrel(box, v)
    lines += _draw_stock(box, v)
    lines += _draw_transition_band(box, v)
    lines += _draw_ramrod(box, v)
    lines += _draw_lock(box, v)
    lines += _draw_powder_stain(box, v)
    return close_cell_svg(lines)


register("arquebus", build)
