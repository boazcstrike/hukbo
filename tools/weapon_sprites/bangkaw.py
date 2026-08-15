"""Bangkaw -- ten authored variants of row 4 (`frame.ROLE_ROWS[4]`).

Historical constraint (CLAUDE.md section 7, design section 6,
`WeaponVisualCatalog.BangkawB1`): the long thrown spear -- dark palm
or rattan shaft, oversized leaf-shaped steel point, carried diagonally
beyond the body. Evidence tier Documented: Pigafetta, wounded at
Mactan on 27 April 1521, records bamboo spears, some iron-tipped,
thrown and reused four to six times; his own 1521 vocabulary records
*bancan* (bangcao), a Visayan/Mindanao term -- Tagalog is *sibat*, so
the name is not generalized across the archipelago. `BangkawSilhouettes`
holds exactly one entry, B1, and this module draws that one silhouette
and nothing else.

The point is steel on every variant and is never substituted for
another material, even though the source records "some iron-tipped"
spears among plain bamboo ones -- the catalog fixes one silhouette, so
variation lands on the point's proportion, never on its presence. No
variant draws a barbed point, a second spear type, ornament, or a
socketed-versus-tanged typology claim -- the binding below is drawn as
cordage lashing wrapped over the shaft/point join, never as a metal
collar, precisely so it never asserts a construction the sources do
not attest.

Variation stays inside that one attested envelope (design section 7):
shaft tone, shaft taper, point size and leaf proportion within an
oversized-leaf envelope, ferrule lashing count and placement, binding
cordage, grip-wrap extent, edge damage on the point, patina, and field
wear -- see the per-variant table in `_VARIANTS` below.

Coordinate convention: every point below is `(u, v)` in the weapon
content box's own local space, `u` in `[0, WEAPON_CONTENT_WIDTH]`
(104) and `v` in `[0, WEAPON_CONTENT_HEIGHT]` (248), `v` growing
downward. `_pt` maps a local point through `box.left`/`box.top` into
the cell's absolute SVG coordinates -- the same trap `wasay.py`
documents (`WEAPON_BOX` starts at `left=4, top=4`, not `0,0`) and the
same fix applies here.

Geometry note: `wasay.py`'s own probe measurement already proved the
104-pixel content box fits an axe head -- a wider, squatter shape --
with 25% of margin to spare. A spear's oversized leaf point is
narrower than that axe head at every variant below: the broadest leaf
here (variant 1) spans a 50-pixel tight bounding width (`half_w = 25`
either side of centre) against the same 104-pixel box, comfortably
inside the envelope the probe already validated.
"""

from __future__ import annotations

from tools.weapon_sprites import register
from tools.weapon_sprites.frame import ContentBox, close_cell_svg, open_cell_svg
from tools.weapon_sprites.palette import (
    CHARRED_WOOD_BROWN,
    IRON_WORN_GREY,
    PALM_RATTAN_OCHRE,
    RATTAN_LASHING_TONE,
    RGB,
    rgb,
    rgba,
    shade,
)

Point = tuple[float, float]

# The point's material is fixed across every variant -- CLAUDE.md
# section 7 and design section 6 both say the point stays steel; it is
# never recoloured to imply a different material.
_POINT_TONE: RGB = IRON_WORN_GREY

# "Dark palm" is a darker value of the same rattan-ochre base, not a
# separate hue -- see palette.py's `shade()` docstring. It keeps the
# dark-shaft reading distinct from full charring (`CHARRED_WOOD_BROWN`)
# while staying inside the "dark palm or rattan" pair the catalog
# names, and it is the third shaft tone this row's variants draw from.
_DARK_PALM: RGB = shade(PALM_RATTAN_OCHRE, -0.30)


def _leaf_points(
    cx: float,
    tip_v: float,
    shoulder_v: float,
    half_w: float,
    base_v: float,
    neck_hw: float,
    belly: float = 0.0,
) -> tuple[Point, ...]:
    """A symmetric seven-point leaf-shaped polygon: the neck where the
    point meets the shaft, a curve-approximation point partway up each
    edge, the widest shoulder, and the apex. `belly` bows the lower
    edge outward (> 0) or in (< 0) for a rounder or leaner profile, per
    the "polygon approximation reads as cleanly at 46px as a true
    curve" convention `wasay.py` establishes for its own head shapes."""
    mid_v = (shoulder_v + base_v) / 2
    mid_w = half_w * (0.72 + belly)
    return (
        (cx - neck_hw, base_v),
        (cx - mid_w, mid_v),
        (cx - half_w, shoulder_v),
        (cx, tip_v),
        (cx + half_w, shoulder_v),
        (cx + mid_w, mid_v),
        (cx + neck_hw, base_v),
    )


class _Variant:
    """One cell's authored parameters. The leaf polygon and the
    shaft's top/bottom half-widths (`shaft_top_hw` at the point's neck,
    `shaft_bottom_hw` at the grip) fix the outline and its taper.
    `lashing_v` are cordage-band centres in the shaft's local `v`;
    `grip_wrap` is `None` for a bare grip."""

    def __init__(
        self,
        leaf: tuple[Point, ...],
        shaft_top_v: float,
        shaft_top_hw: float,
        shaft_bottom_hw: float,
        lashing_v: tuple[float, ...],
        lashing_band_height: float,
        grip_wrap: tuple[float, float] | None,
        shaft_tone: RGB,
        wear: str,
        note: str,
    ) -> None:
        self.leaf = leaf
        self.shaft_top_v = shaft_top_v
        self.shaft_top_hw = shaft_top_hw
        self.shaft_bottom_hw = shaft_bottom_hw
        self.lashing_v = lashing_v
        self.lashing_band_height = lashing_band_height
        self.grip_wrap = grip_wrap
        self.shaft_tone = shaft_tone
        self.wear = wear
        self.note = note


_CX = 52.0  # WEAPON_CONTENT_WIDTH / 2 -- local-space centre, same x every variant

# Seven outline families, one per variant 0-6. Variants 7-9 are tail
# slots that reuse an already-drawn outline and vary tint/wear only,
# per design section 7's "pure recolour is permitted only in the tail
# slots" rule -- the same rule `wasay.py` follows.
_LEAF_BASELINE = _leaf_points(_CX, 20, 64, 20, 104, 7)
_LEAF_BROAD = _leaf_points(_CX, 14, 62, 25, 108, 8, belly=0.05)
_LEAF_NARROW = _leaf_points(_CX, 26, 58, 15, 92, 6, belly=-0.05)
_LEAF_LONGPOINT = _leaf_points(_CX, 16, 50, 18, 132, 9, belly=0.10)
_LEAF_SHORTPOINT = _leaf_points(_CX, 30, 56, 17, 82, 6)
_LEAF_ROUND = _leaf_points(_CX, 18, 66, 19, 106, 7, belly=0.15)
_LEAF_LONG_NARROW = _leaf_points(_CX, 22, 60, 16, 96, 6, belly=-0.08)

_VARIANTS: tuple[_Variant, ...] = (
    _Variant(  # 0 -- baseline proportion, rattan-ochre shaft, single ferrule lashing
        _LEAF_BASELINE, 104, 6, 8, (108,), 4.8, (210, 248),
        PALM_RATTAN_OCHRE, "none",
        "baseline leaf proportion; rattan-ochre shaft; single lashing band at the ferrule",
    ),
    _Variant(  # 1 -- broadest, most oversized leaf in the row (the geometry-note variant)
        _LEAF_BROAD, 108, 6, 8, (112, 120), 4.8, (210, 248),
        _DARK_PALM, "none",
        "broadest, most oversized leaf point (50px tight bbox); double ferrule lashing; dark-palm shaft",
    ),
    _Variant(  # 2 -- leanest leaf, thinnest shaft, longest exposed shaft
        _LEAF_NARROW, 92, 5, 6, (96,), 4.4, (215, 248),
        PALM_RATTAN_OCHRE, "none",
        "leanest leaf point; thinnest, longest exposed shaft; single ferrule lashing",
    ),
    _Variant(  # 3 -- point set deepest into the shaft, thickest shaft, triple lashing, charred
        _LEAF_LONGPOINT, 132, 8, 10, (136, 144, 152), 4.8, (200, 248),
        CHARRED_WOOD_BROWN, "none",
        "point reaches deepest into the shaft in the row; thickest shaft; triple ferrule lashing; charred shaft",
    ),
    _Variant(  # 4 -- shortest point, most pronounced taper, one wide lashing band, bare grip
        _LEAF_SHORTPOINT, 82, 5, 9, (88,), 8.0, None,
        PALM_RATTAN_OCHRE, "none",
        "shortest point; most pronounced shaft taper; one wide lashing band; bare, unwrapped grip",
    ),
    _Variant(  # 5 -- rounded convex-belly leaf, uniform (untapered) shaft, ferrule plus mid-shaft lashing
        _LEAF_ROUND, 106, 7, 7, (110, 178), 4.8, (210, 248),
        _DARK_PALM, "none",
        "rounded convex-belly point profile; uniform, untapered shaft; second lashing band at mid-shaft",
    ),
    _Variant(  # 6 -- long narrow leaf, longest grip wrap in the row
        _LEAF_LONG_NARROW, 96, 5, 6, (100,), 4.8, (150, 248),
        PALM_RATTAN_OCHRE, "none",
        "long narrow leaf point; longest grip wrap in the row; single ferrule lashing",
    ),
    _Variant(  # 7 -- tail: baseline outline, chipped point, worn-iron patina, dark-palm shaft
        _LEAF_BASELINE, 104, 6, 8, (108,), 4.8, (210, 248),
        _DARK_PALM, "chipped",
        "recolour/wear only, baseline outline; two edge chips on the point; worn-iron patina; dark-palm shaft",
    ),
    _Variant(  # 8 -- tail: broad outline, charred shaft
        _LEAF_BROAD, 108, 6, 8, (112, 120), 4.8, (210, 248),
        CHARRED_WOOD_BROWN, "none",
        "recolour only, broad outline; charred shaft tone",
    ),
    _Variant(  # 9 -- tail: baseline outline, heaviest wear
        _LEAF_BASELINE, 104, 6, 8, (108,), 4.8, (210, 248),
        PALM_RATTAN_OCHRE, "heavy",
        "recolour/wear only, baseline outline; heaviest wear: rust spots on the point, faded lashing, nicked edge, scuffed grip",
    ),
)


def _pt(box: ContentBox, u: float, v: float) -> Point:
    return (box.left + u, box.top + v)


def _poly_attr(points: tuple[Point, ...], box: ContentBox) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in (_pt(box, u, v) for u, v in points))


def _polygon(points: tuple[Point, ...], box: ContentBox, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{opacity}"' if opacity < 1.0 else ""
    return f'<polygon points="{_poly_attr(points, box)}" fill="{fill}"{op} />'


def _leaf_shadow_points(leaf: tuple[Point, ...]) -> tuple[Point, ...]:
    """The left half of the leaf, neck through the apex -- the shadow
    side, opposite the highlighted edge."""
    return leaf[:4]


def _leaf_highlight_points(leaf: tuple[Point, ...]) -> tuple[Point, ...]:
    """The right half of the leaf, apex through the neck -- the
    brightest highlight sits here, per the craft rule that the
    cutting edge carries the highlight."""
    return leaf[3:]


def _draw_point(box: ContentBox, v: _Variant) -> list[str]:
    base = rgb(_POINT_TONE)
    shadow = rgba(shade(_POINT_TONE, -0.35), 0.55)
    highlight = rgba(shade(_POINT_TONE, 0.40), 0.65)
    lines = [
        _polygon(v.leaf, box, base),
        _polygon(_leaf_shadow_points(v.leaf), box, shadow),
        _polygon(_leaf_highlight_points(v.leaf), box, highlight),
    ]
    if v.wear in ("chipped", "heavy"):
        # Small dark notches on the edge, standing in for chip damage.
        # Not a true boolean subtraction (no clip-path here); drawn in
        # the shadow tone so a chip reads as a dark bite rather than a
        # hole, matching wasay.py's own convention.
        edge_pts = _leaf_highlight_points(v.leaf)
        chip_tone = rgba(shade(_POINT_TONE, -0.45), 0.85)
        u0, v0 = edge_pts[1]  # shoulder-right -- a mid-edge point, never the apex or the neck
        lines.append(
            _polygon(((u0 - 3, v0 - 5), (u0 + 4, v0 - 2), (u0 - 1, v0 + 6)), box, chip_tone)
        )
        if v.wear == "heavy":
            u1, v1 = edge_pts[2]  # further down the same edge
            lines.append(
                _polygon(((u1 - 4, v1 - 4), (u1 + 3, v1 - 1), (u1 - 2, v1 + 5)), box, chip_tone)
            )
    if v.wear == "heavy":
        # Rust-spot patina: a few small translucent dabs on the point face.
        rust = rgba((150, 96, 60), 0.28)
        tip_u, tip_v = v.leaf[3]
        base_v = v.leaf[0][1]
        spots = [
            (tip_u - 6, (tip_v + base_v) / 2 - 6),
            (tip_u + 5, (tip_v + base_v) / 2 + 8),
            (tip_u - 3, base_v - 10),
        ]
        for u, vv in spots:
            cx, cy = _pt(box, u, vv)
            lines.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="3.2" fill="{rust}" />')
    return lines


def _draw_shaft(box: ContentBox, v: _Variant, cx: float, bottom_v: float) -> list[str]:
    top = v.shaft_top_v
    top_l, top_r = cx - v.shaft_top_hw, cx + v.shaft_top_hw
    bot_l, bot_r = cx - v.shaft_bottom_hw, cx + v.shaft_bottom_hw
    base = rgb(v.shaft_tone)
    shadow = rgba(shade(v.shaft_tone, -0.30), 0.6)
    highlight = rgba(shade(v.shaft_tone, 0.30), 0.5)
    poly = ((top_l, top), (top_r, top), (bot_r, bottom_v), (bot_l, bottom_v))
    lines = [_polygon(poly, box, base)]
    shadow_w_top = v.shaft_top_hw * 0.55
    shadow_w_bot = v.shaft_bottom_hw * 0.55
    shadow_poly = (
        (top_l, top), (top_l + shadow_w_top, top),
        (bot_l + shadow_w_bot, bottom_v), (bot_l, bottom_v),
    )
    lines.append(_polygon(shadow_poly, box, shadow))
    hl_w_top = v.shaft_top_hw * 0.35
    hl_w_bot = v.shaft_bottom_hw * 0.35
    hl_poly = (
        (top_r - hl_w_top, top), (top_r, top),
        (bot_r, bottom_v), (bot_r - hl_w_bot, bottom_v),
    )
    lines.append(_polygon(hl_poly, box, highlight))
    return lines


def _draw_lashing(box: ContentBox, v: _Variant, cx: float) -> list[str]:
    # Every lashing band on this row sits within a few pixels of the
    # shaft/point join (`shaft_top_v`), even the "mid-shaft" second
    # band variant 5 adds, so the shaft's own top half-width is the
    # right reference width for all of them -- taper over that short a
    # span is sub-pixel and not worth interpolating.
    tone = rgb(RATTAN_LASHING_TONE)
    opacity = 0.5 if v.wear == "heavy" else 1.0
    hw = v.shaft_top_hw + 1.8
    half_h = v.lashing_band_height / 2
    lines = []
    for band_v in v.lashing_v:
        x0, y0 = _pt(box, cx - hw, band_v - half_h)
        lines.append(
            f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{hw * 2:.1f}" height="{v.lashing_band_height:.1f}" '
            f'fill="{tone}" fill-opacity="{opacity}" rx="1" />'
        )
    return lines


def _draw_grip_wrap(box: ContentBox, v: _Variant, cx: float) -> list[str]:
    if v.grip_wrap is None:
        return []
    top_v, bottom_v = v.grip_wrap
    hw = v.shaft_bottom_hw
    tone = rgba(shade(v.shaft_tone, -0.25), 0.7 if v.wear != "heavy" else 0.4)
    lines = []
    step = 10.0
    vv = top_v
    while vv < bottom_v:
        x0, y0 = _pt(box, cx - hw, vv)
        x1, y1 = _pt(box, cx + hw, vv + 5.0)
        lines.append(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="{tone}" stroke-width="2" />')
        vv += step
    return lines


def build(index: int, box: ContentBox) -> str:
    """Return one complete SVG document string for Bangkaw variant
    `index` (0-9), drawn inside `box` per the module docstring."""
    if not 0 <= index <= 9:
        raise ValueError(f"variant index out of range: {index!r}")
    v = _VARIANTS[index]
    cx = box.width / 2  # local-space centre, same x every variant
    bottom_v = box.height

    lines: list[str] = open_cell_svg()
    lines += _draw_shaft(box, v, cx, bottom_v)
    lines += _draw_grip_wrap(box, v, cx)
    lines += _draw_lashing(box, v, cx)
    lines += _draw_point(box, v)
    return close_cell_svg(lines)


register("bangkaw", build)
