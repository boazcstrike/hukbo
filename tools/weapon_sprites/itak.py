"""Itak -- ten authored variants of row 3 (`frame.ROLE_ROWS[3]`).

Historical constraint (CLAUDE.md section 7, design section 6,
`WeaponVisualCatalog.ItakI1`): the short, broad, plain work blade. The
catalog's own text closes this row off harder than any other: "the
plainest silhouette in the roster; no other forms exist or are
invented." The broad-blade *class* is Documented, form uncertain, from
Legazpi-era relations (1565-1569); the *itak* name's own period
attestation is unconfirmed, so the whole entry is a Provisional
reconstruction. Inventing a second blade profile, a curved parang
sweep, a second silhouette family, or any ornament is exactly the
failure mode the catalog text forbids -- a work blade is a work blade.

**Because form variation is closed off hardest on this row, variation
here is carried almost entirely by wear, grip, and lashing rather than
by outline (design section 7).** Four blade outlines exist below
(baseline, broad, lean, short-thick) and they stay inside one attested
envelope -- broader or narrower, longer or shorter haft-to-tip, thicker
or thinner spine -- never a different blade shape. One of the four
outlines gets a fifth treatment, a rolled/blunted tip, which is an
edge-condition variant and not a new silhouette. The remaining six of
the ten cells reuse those same outlines and differ only in edge damage
(chips, a rolled tip, rust), grip-wrap extent and material, lashing
count and placement at the ferrule, patina, and a resin/oil sheen --
the axes design section 7 names as legitimate, and the ones this row
leans on hardest because the alternative is inventing form the
catalog has already closed off.

Coordinate convention: every point below is `(u, v)` in the weapon
content box's own local space, `u` in `[0, WEAPON_CONTENT_WIDTH]`
(104) and `v` in `[0, WEAPON_CONTENT_HEIGHT]` (248), `v` growing
downward. `_pt` maps a local point through `box.left`/`box.top` into
the cell's absolute SVG coordinates; every drawing helper takes `box`
and works in local space so the module never hardcodes the box's own
offset -- `WEAPON_BOX` has `left=4, top=4`, not `0,0`.
"""

from __future__ import annotations

from tools.weapon_sprites import register
from tools.weapon_sprites.frame import ContentBox, close_cell_svg, open_cell_svg
from tools.weapon_sprites.palette import (
    CHARRED_WOOD_BROWN,
    GRIP_WARM_OCHRE,
    IRON_WORN_GREY,
    PALM_RATTAN_OCHRE,
    RATTAN_LASHING_TONE,
    RGB,
    rgb,
    rgba,
    shade,
)

Point = tuple[float, float]

# Not in palette.py yet -- mirrors `DyePalette.IronBlueBlack` (56, 66, 73)
# verbatim, the unconditional default blade tone `WeaponVisualCatalog`
# resolves for the Itak's plain-ochre tint (and for most other weapon
# roles' default tints too). Kept local rather than added to the shared
# module to avoid a cross-module edit while sibling role modules are
# authoring in parallel; palette.py's own rule -- mirror what already
# shipped, invent nothing -- is honoured either way.
IRON_BLUE_BLACK: RGB = (56, 66, 73)

# -- Blade silhouettes, one per outline family, in local (u, v) space.
# Point order: base-left (spine side, at the grip) -> spine rising
# toward the tip -> tip apex (offset toward the edge side, as a
# single-edged work blade's point commonly sits) -> edge belly (the
# widest point) -> edge curving back toward the base -> base-right
# corner, closing back to point 0. A polygon approximation reads as
# cleanly at 46px as a true curve and carries no rasteriser risk, per
# the "silhouette first" craft guidance wasay.py already established.
_BLADE_BASELINE: tuple[Point, ...] = (
    (45, 175), (43, 100), (49, 30), (56, 18), (78, 88), (74, 150), (62, 175),
)
_BLADE_BROAD: tuple[Point, ...] = (
    (45, 180), (43, 105), (49, 28), (58, 14), (86, 92), (80, 155), (63, 180),
)
_BLADE_LEAN: tuple[Point, ...] = (
    (46, 185), (44, 110), (50, 45), (55, 10), (70, 95), (67, 160), (60, 185),
)
_BLADE_SHORT_THICK: tuple[Point, ...] = (
    (41, 160), (39, 110), (47, 60), (60, 45), (82, 95), (76, 135), (66, 160),
)
# Same envelope as the baseline outline, tip flattened into a short
# blunt run instead of a sharp apex -- an edge-condition variant, not
# a new silhouette (design section 7's "rolled tip" axis).
_BLADE_ROLLED_TIP: tuple[Point, ...] = (
    (45, 175), (43, 100), (46, 35), (54, 28), (61, 30), (78, 88), (74, 150), (62, 175),
)


class _Variant:
    """One cell's authored parameters. `blade` fixes the outline;
    `grip_top_v` is that outline's own base row, so the grip always
    meets its blade with no per-variant seam math. `grip_wrap` is
    `None` for a bare grip. `sheen` adds a resin/oil highlight pass;
    it is the recolour-only tail axis, never combined with `wear`."""

    def __init__(
        self,
        blade: tuple[Point, ...],
        grip_top_v: float,
        grip_half_width: float,
        lashing_v: tuple[float, ...],
        grip_wrap: tuple[float, float] | None,
        grip_tone: RGB,
        blade_tone: RGB,
        wear: str,
        sheen: bool,
        note: str,
    ) -> None:
        self.blade = blade
        self.grip_top_v = grip_top_v
        self.grip_half_width = grip_half_width
        self.lashing_v = lashing_v
        self.grip_wrap = grip_wrap
        self.grip_tone = grip_tone
        self.blade_tone = blade_tone
        self.wear = wear
        self.sheen = sheen
        self.note = note


# One entry per variant 0-9. Variants 0, 1, 2, 3, 4 vary the outline or
# the tip; 5 and 6 vary wear/grip on an existing outline; 7, 8, and 9
# are tail slots that reuse an already-drawn outline and vary tint,
# sheen, or wear only, per design section 7's "pure recolour is
# permitted only in the tail slots" rule. 9 matches the catalog's own
# second tint (`ItakTintWornField`) so the "used-up tool" read the
# catalog already names has a cell.
_VARIANTS: tuple[_Variant, ...] = (
    _Variant(  # 0 -- baseline proportion, plain-ochre catalog tint
        _BLADE_BASELINE, 175, 8, (175,), (195, 248),
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "none", False,
        "baseline proportion; single ferrule lashing; wrapped grip; plain-ochre catalog tint",
    ),
    _Variant(  # 1 -- broadest blade in the row
        _BLADE_BROAD, 180, 9, (180, 186), (200, 248),
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "none", False,
        "broadest blade in the row; double ferrule lashing",
    ),
    _Variant(  # 2 -- leanest, longest blade, bare grip
        _BLADE_LEAN, 185, 7, (185,), None,
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "none", False,
        "leanest, longest blade in the row; bare unwrapped grip",
    ),
    _Variant(  # 3 -- shortest, thickest-spined blade, full wrap
        _BLADE_SHORT_THICK, 160, 10, (160, 167, 174), (178, 248),
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "none", False,
        "shortest, thickest-spined blade; triple ferrule lashing; full-length grip wrap",
    ),
    _Variant(  # 4 -- rolled/blunted tip, baseline envelope otherwise
        _BLADE_ROLLED_TIP, 175, 8, (175,), (195, 248),
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "none", False,
        "baseline proportion, rolled/blunted tip -- a resharpened field edge",
    ),
    _Variant(  # 5 -- chipped edge, worn wrap
        _BLADE_BASELINE, 175, 8, (175,), (195, 248),
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "chipped", False,
        "recolour/wear only, baseline outline; two edge chips; worn grip-wrap opacity",
    ),
    _Variant(  # 6 -- heaviest wear, broad outline, bare grip
        _BLADE_BROAD, 180, 9, (180, 186), None,
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "heavy", False,
        "broad outline; heaviest wear: nicks, rust patina, faded lashing; bare worn grip",
    ),
    _Variant(  # 7 -- tail: resin/oil sheen
        _BLADE_BASELINE, 175, 8, (175,), (195, 248),
        GRIP_WARM_OCHRE, IRON_BLUE_BLACK, "none", True,
        "recolour/sheen only, baseline outline; fresh resin/oil sheen along the edge",
    ),
    _Variant(  # 8 -- tail: charred grip
        _BLADE_BASELINE, 175, 8, (175,), (195, 248),
        CHARRED_WOOD_BROWN, IRON_BLUE_BLACK, "none", False,
        "recolour only, baseline outline; charred (soot-dark) grip tone",
    ),
    _Variant(  # 9 -- tail: matches the catalog's worn-field tint
        _BLADE_BASELINE, 175, 8, (175,), (195, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "chipped", False,
        "recolour/wear only, baseline outline; matches catalog's worn-field tint "
        "(iron-worn-grey blade, palm-rattan-ochre grip); light edge wear",
    ),
)


def _pt(box: ContentBox, u: float, v: float) -> Point:
    return (box.left + u, box.top + v)


def _poly_attr(points: tuple[Point, ...], box: ContentBox) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in (_pt(box, u, v) for u, v in points))


def _polygon(points: tuple[Point, ...], box: ContentBox, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{opacity}"' if opacity < 1.0 else ""
    return f'<polygon points="{_poly_attr(points, box)}" fill="{fill}"{op} />'


def _spine_shadow_points(blade: tuple[Point, ...]) -> tuple[Point, ...]:
    """The spine-and-base half of a blade silhouette -- up to and
    including the tip apex -- used as the shadow overlay so it always
    sits on the back (non-cutting) side, regardless of which blade
    shape is in play."""
    mid = len(blade) // 2 + 1
    return blade[: mid + 1]


def _edge_highlight_points(blade: tuple[Point, ...]) -> tuple[Point, ...]:
    """The belly-facing half -- tip apex through the edge run to the
    base -- used for the brightest highlight, per the craft rule that
    the brightest highlight sits on the cutting edge."""
    mid = len(blade) // 2
    return blade[mid - 1 :]


def _draw_blade(box: ContentBox, v: _Variant) -> list[str]:
    base = rgb(v.blade_tone)
    shadow = rgba(shade(v.blade_tone, -0.35), 0.55)
    highlight = rgba(shade(v.blade_tone, 0.40), 0.65)
    lines = [
        _polygon(v.blade, box, base),
        _polygon(_spine_shadow_points(v.blade), box, shadow),
        _polygon(_edge_highlight_points(v.blade), box, highlight),
    ]
    if v.sheen:
        # Resin/oil sheen: a thin, brighter, more opaque highlight
        # strip laid directly on the cutting edge, on top of the
        # ordinary highlight band.
        sheen_tone = rgba(shade(v.blade_tone, 0.55), 0.8)
        lines.append(_polygon(_edge_highlight_points(v.blade), box, sheen_tone, opacity=0.35))
    if v.wear in ("chipped", "heavy"):
        # Small dark notches at the edge run, standing in for chip
        # damage. Not a true boolean subtraction (no clip-path here);
        # drawn in the shadow tone so a chip reads as a dark bite
        # rather than a hole.
        edge_pts = _edge_highlight_points(v.blade)
        chip_tone = rgba(shade(v.blade_tone, -0.45), 0.85)
        u0, v0 = edge_pts[len(edge_pts) // 2]
        lines.append(
            _polygon(((u0 - 3, v0 - 5), (u0 + 4, v0 - 2), (u0 - 1, v0 + 6)), box, chip_tone)
        )
        if v.wear == "heavy":
            u1, v1 = edge_pts[1] if len(edge_pts) > 1 else edge_pts[0]
            lines.append(
                _polygon(((u1 - 4, v1 - 4), (u1 + 3, v1 - 1), (u1 - 2, v1 + 5)), box, chip_tone)
            )
    if v.wear == "heavy":
        # Rust-spot patina: a few small translucent dabs scattered on
        # the blade face.
        rust = rgba((150, 96, 60), 0.30)
        spots = [(58, v.grip_top_v - 40), (68, v.grip_top_v - 60), (50, v.grip_top_v - 25)]
        for u, vv in spots:
            cx, cy = _pt(box, u, vv)
            lines.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="3.2" fill="{rust}" />')
    return lines


def _draw_grip(box: ContentBox, v: _Variant, cx: float, bottom_v: float) -> list[str]:
    hw = v.grip_half_width
    base = rgb(v.grip_tone)
    shadow = rgba(shade(v.grip_tone, -0.30), 0.6)
    highlight = rgba(shade(v.grip_tone, 0.30), 0.5)
    top = v.grip_top_v
    left, right = cx - hw, cx + hw
    x0, y0 = _pt(box, left, top)
    x1, y1 = _pt(box, right, bottom_v)
    lines = [f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{x1 - x0:.1f}" height="{y1 - y0:.1f}" fill="{base}" rx="2" />']
    sx0, sy0 = _pt(box, left, top)
    lines.append(
        f'<rect x="{sx0:.1f}" y="{sy0:.1f}" width="{hw * 0.55:.1f}" height="{bottom_v - top:.1f}" fill="{shadow}" />'
    )
    hxo = cx + hw - hw * 0.35
    hx0, hy0 = _pt(box, hxo, top)
    lines.append(
        f'<rect x="{hx0:.1f}" y="{hy0:.1f}" width="{hw * 0.35:.1f}" height="{bottom_v - top:.1f}" fill="{highlight}" />'
    )
    return lines


def _draw_lashing(box: ContentBox, v: _Variant, cx: float) -> list[str]:
    hw = v.grip_half_width + 1.6
    tone = rgb(RATTAN_LASHING_TONE)
    opacity = 0.55 if v.wear == "heavy" else 1.0
    lines = []
    for band_v in v.lashing_v:
        x0, y0 = _pt(box, cx - hw, band_v - 2.4)
        lines.append(
            f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{hw * 2:.1f}" height="4.8" '
            f'fill="{tone}" fill-opacity="{opacity}" rx="1" />'
        )
    return lines


def _draw_grip_wrap(box: ContentBox, v: _Variant, cx: float) -> list[str]:
    if v.grip_wrap is None:
        return []
    top_v, bottom_v = v.grip_wrap
    hw = v.grip_half_width
    tone = rgba(shade(v.grip_tone, -0.25), 0.7 if v.wear != "heavy" else 0.4)
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
    """Return one complete SVG document string for Itak variant
    `index` (0-9), drawn inside `box` per the module docstring."""
    if not 0 <= index <= 9:
        raise ValueError(f"variant index out of range: {index!r}")
    v = _VARIANTS[index]
    cx = box.width / 2  # local-space centre, same x every variant
    bottom_v = box.height

    lines: list[str] = open_cell_svg()
    lines += _draw_grip(box, v, cx, bottom_v)
    lines += _draw_grip_wrap(box, v, cx)
    lines += _draw_blade(box, v)
    lines += _draw_lashing(box, v, cx)
    return close_cell_svg(lines)


register("itak", build)
