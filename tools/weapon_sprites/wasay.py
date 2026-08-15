"""Wasay -- ten authored variants of row 1 (`frame.ROLE_ROWS[1]`).

Historical constraint (CLAUDE.md section 7, design section 6,
`WeaponVisualCatalog.WasayW1`): a hafted battle axe attested among
Tausug and Ibanag groups, evidence tier Documented-form-uncertain,
"an everyman's tool-weapon, not elite kit" -- broad iron head, short
hardwood haft, drawn plain. The Cordilleran head axe form is a
distinct, regionally specific, late-documented shape excluded from
the roster entirely and never drawn here.

Variation stays inside that one attested envelope (design section 7):
proportion (head breadth/depth/edge curve, haft length), lashing count
and placement, grip-wrap extent, edge damage, patina, and haft tone.
No variant introduces a second axe type, a spike, a beard, or any
ornament -- see the per-variant table below.

Coordinate convention: every point below is `(u, v)` in the weapon
content box's own local space, `u` in `[0, WEAPON_CONTENT_WIDTH]`
(104) and `v` in `[0, WEAPON_CONTENT_HEIGHT]` (248), `v` growing
downward. `_pt` maps a local point through `box.left`/`box.top` into
the cell's absolute SVG coordinates; every drawing helper takes `box`
and works in local space so the module never hardcodes the box's own
offset.

REWORK (second pass): a single continuous polygon with a bumped-out
poll and a bumped-out edge still rendered as one symmetric-looking
faceted blob when checked -- a spearhead, not an axe. The fix is
structural, not numeric: the head is now two SEPARATE, separately
filled shapes that both touch the haft at the neck but do not share
one outline. `_HeadShape.poll` is a small, blunt, flat-faced block
flush against the haft on one side; `_HeadShape.blade` is a much
larger curved fan flush against the haft on the other side, with a
straight spine along the haft and a convex cutting-edge arc bulging
far out. Two differently shaped, differently toned regions read as
"axe head" at a glance in a way one blended outline did not, and the
blade is authored at roughly twice the poll's reach from the haft in
every family but the deliberately poll-heavy variant 6.

PROBE MEASUREMENT (task 3, design section 4, remeasured after this
pass): the widest object in any cell is the "broad" blade shape
(variants 1 and 8), which spans from its haft-flush spine at
`u = 62` out to the belly at `u = 97` -- 35 content-box pixels of
reach on the blade side alone, against `u = 26` for that variant's
poll outer face -- 16 pixels of reach on the poll side. The full
silhouette's tight bounding box is `u = 26` to `u = 97`, 71 pixels
against a 104-pixel box, comfortably inside it with an asymmetric
16:35 split either side of the haft.
"""

from __future__ import annotations

from dataclasses import dataclass

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


@dataclass(frozen=True)
class _HeadShape:
    """A head silhouette split into its two separately drawn masses.

    `poll` is a small closed quad flush against the haft's LEFT edge:
    (spine-top, outer-top, outer-bottom, spine-bottom), a short blunt
    block with a roughly flat outer face -- the poll never has a
    curved edge, because a poll is not a cutting surface.

    `blade` is a closed polygon flush against the haft's RIGHT edge,
    always starting and ending on the haft line (spine-top, ...,
    spine-bottom) so its "back" edge is dead straight, with a curved
    run of intermediate points (toe, optional extra curve points,
    belly, heel) bulging out to the true cutting edge. `blade[0]` is
    always the spine-top point and `blade[-1]` is always the
    spine-bottom point, both on the haft line -- every drawing helper
    that touches `blade` relies on that.
    """

    poll: tuple[Point, Point, Point, Point]
    blade: tuple[Point, ...]


# Seven outline families. Every family keeps the poll on the LEFT and
# the blade on the RIGHT so the row stays visually coherent -- a
# spectator who has seen one Wasay cell can read the next one as the
# same kind of object. Only variant 6 (`_HEAD_MIDLASH_BUMP`) narrows
# the poll/blade reach gap, per its own "exaggerated poll bump" note;
# every other family keeps the blade at roughly twice the poll's reach
# from the haft, per the REWORK note above.
_HEAD_BASELINE = _HeadShape(
    poll=((43, 96), (29, 90), (29, 112), (43, 118)),
    blade=((61, 30), (66, 38), (90, 74), (74, 108), (61, 118)),
)
_HEAD_BROAD = _HeadShape(
    poll=((42, 100), (26, 92), (26, 116), (42, 124)),
    blade=((62, 30), (68, 38), (97, 76), (82, 112), (62, 126)),
)
_HEAD_NARROW = _HeadShape(
    poll=((44, 98), (33, 92), (33, 106), (44, 112)),
    blade=((60, 38), (65, 44), (72, 58), (78, 70), (68, 96), (60, 112)),
)
_HEAD_LONGHAFT = _HeadShape(
    poll=((44, 82), (33, 76), (33, 90), (44, 95)),
    blade=((60, 44), (64, 48), (76, 64), (66, 84), (60, 95)),
)
_HEAD_SHORTHAFT = _HeadShape(
    poll=((41, 112), (26, 104), (26, 128), (41, 140)),
    blade=((63, 26), (70, 34), (97, 76), (82, 120), (63, 140)),
)
_HEAD_ROUND_BELLY = _HeadShape(
    poll=((43, 100), (30, 93), (30, 107), (43, 115)),
    blade=((61, 36), (66, 42), (82, 54), (90, 76), (80, 98), (70, 108), (61, 115)),
)
_HEAD_MIDLASH_BUMP = _HeadShape(
    poll=((43, 88), (23, 80), (23, 112), (43, 120)),
    blade=((61, 34), (66, 40), (84, 72), (72, 106), (61, 120)),
)


class _Variant:
    """One cell's authored parameters. `head` and `haft_base`/`bottom`
    fix the outline; `haft_half_width` and `lashing` are read off the
    same head-base `v` so the haft always meets its own head with no
    per-variant seam math. `grip_wrap` is `None` for a bare grip."""

    def __init__(
        self,
        head: _HeadShape,
        haft_base_v: float,
        haft_half_width: float,
        lashing_v: tuple[float, ...],
        grip_wrap: tuple[float, float] | None,
        haft_tone: RGB,
        blade_tone: RGB,
        wear: str,
        note: str,
    ) -> None:
        self.head = head
        self.haft_base_v = haft_base_v
        self.haft_half_width = haft_half_width
        self.lashing_v = lashing_v
        self.grip_wrap = grip_wrap
        self.haft_tone = haft_tone
        self.blade_tone = blade_tone
        self.wear = wear
        self.note = note


# One entry per variant 0-9. Variants 0-6 vary the outline; 7-9 are
# tail slots that reuse an already-drawn outline and vary tint/wear
# only, per design section 7's "pure recolour is permitted only in the
# tail slots" rule.
_VARIANTS: tuple[_Variant, ...] = (
    _Variant(  # 0 -- baseline proportion, single neck lashing, ochre haft
        _HEAD_BASELINE, 118, 9, (116,), (210, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "baseline proportion; single lashing band at the neck",
    ),
    _Variant(  # 1 -- broadest blade in the row (the probe variant)
        _HEAD_BROAD, 124, 10, (120, 128), (210, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "broadest blade (71px tight bbox, the probe measurement); double neck lashing",
    ),
    _Variant(  # 2 -- leanest head, longest edge curve
        _HEAD_NARROW, 112, 8, (110,), (215, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "leanest head in the row; long curved-belly edge",
    ),
    _Variant(  # 3 -- longest haft, compact head, triple neck lashing
        _HEAD_LONGHAFT, 95, 8, (92, 99, 106), (200, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "longest haft in the row; compact head; triple lashing at the neck",
    ),
    _Variant(  # 4 -- shortest haft, deepest head, thick haft, wide single lashing
        _HEAD_SHORTHAFT, 140, 11, (137,), (225, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "shortest, thickest haft; deepest head; one wide lashing band",
    ),
    _Variant(  # 5 -- rounded convex-belly edge profile
        _HEAD_ROUND_BELLY, 115, 9, (113,), (205, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "rounded convex-belly edge, standard proportions otherwise",
    ),
    _Variant(  # 6 -- mid-haft lashing placement, exaggerated poll bump, bare grip
        _HEAD_MIDLASH_BUMP, 120, 9, (117, 180), None,
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "none",
        "lashing at neck and mid-haft (not neck-only); pronounced poll bump; bare, unwrapped grip",
    ),
    _Variant(  # 7 -- tail: baseline outline, chipped edge, worn iron
        _HEAD_BASELINE, 118, 9, (116,), (210, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "chipped",
        "recolour/wear only, baseline outline; two edge chips, worn-iron patina",
    ),
    _Variant(  # 8 -- tail: broad outline, charred haft
        _HEAD_BROAD, 124, 10, (120, 128), (210, 248),
        CHARRED_WOOD_BROWN, IRON_WORN_GREY, "none",
        "recolour only, broad outline; charred (dark) haft tone",
    ),
    _Variant(  # 9 -- tail: baseline outline, heaviest wear
        _HEAD_BASELINE, 118, 9, (116,), (210, 248),
        PALM_RATTAN_OCHRE, IRON_WORN_GREY, "heavy",
        "recolour/wear only, baseline outline; heaviest wear: rust spots, faded lashing, nicked edge, scuffed grip",
    ),
)


def _pt(box: ContentBox, u: float, v: float) -> Point:
    return (box.left + u, box.top + v)


def _poly_attr(points: tuple[Point, ...], box: ContentBox) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in (_pt(box, u, v) for u, v in points))


def _polygon(points: tuple[Point, ...], box: ContentBox, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{opacity}"' if opacity < 1.0 else ""
    return f'<polygon points="{_poly_attr(points, box)}" fill="{fill}"{op} />'


def _blade_shadow_points(blade: tuple[Point, ...]) -> tuple[Point, ...]:
    """A triangle -- spine-top, toe, spine-bottom -- used as the
    shadow overlay, so the shadow always sits on the haft-facing
    straight edge, opposite the cutting edge."""
    return (blade[0], blade[1], blade[-1])


def _blade_highlight_points(blade: tuple[Point, ...]) -> tuple[Point, ...]:
    """The cutting-edge run -- toe through every curve point to
    spine-bottom -- used for the brightest highlight, per the craft
    rule that the brightest highlight sits strictly on the cutting
    edge, never on the poll."""
    return blade[1:]


def _draw_head(box: ContentBox, v: _Variant) -> list[str]:
    base = rgb(v.blade_tone)
    shadow = rgba(shade(v.blade_tone, -0.35), 0.55)
    highlight = rgba(shade(v.blade_tone, 0.40), 0.65)
    poll_fill = rgb(shade(v.blade_tone, -0.20))
    lines = [
        # The poll: one small, flat-faced, uniformly darker block. It
        # carries no edge highlight -- a poll is blunt, not sharp.
        _polygon(v.head.poll, box, poll_fill),
        # The blade: base fill plus a shadow sliver on the haft-facing
        # spine and a bright highlight on the curved cutting edge.
        _polygon(v.head.blade, box, base),
        _polygon(_blade_shadow_points(v.head.blade), box, shadow),
        _polygon(_blade_highlight_points(v.head.blade), box, highlight),
    ]
    if v.wear in ("chipped", "heavy"):
        # Small dark notches at the edge run, standing in for chip
        # damage. Not a true boolean subtraction (no clip-path here);
        # drawn in the shadow tone so a chip reads as a dark bite
        # rather than a hole. Always placed on the blade's own curve
        # points, never on the poll.
        edge_pts = v.head.blade[1:-1]
        chip_tone = rgba(shade(v.blade_tone, -0.45), 0.85)
        u0, v0 = edge_pts[len(edge_pts) // 2]
        lines.append(
            _polygon(((u0 - 3, v0 - 5), (u0 + 4, v0 - 2), (u0 - 1, v0 + 6)), box, chip_tone)
        )
        if v.wear == "heavy":
            u1, v1 = edge_pts[0]
            lines.append(
                _polygon(((u1 - 4, v1 - 4), (u1 + 3, v1 - 1), (u1 - 2, v1 + 5)), box, chip_tone)
            )
    if v.wear == "heavy":
        # Rust-spot patina: a few small translucent dabs scattered on
        # the blade face, well clear of the poll.
        rust = rgba((150, 96, 60), 0.30)
        spots = [(72, v.haft_base_v - 42), (80, v.haft_base_v - 68), (66, v.haft_base_v - 20)]
        for u, vv in spots:
            cx, cy = _pt(box, u, vv)
            lines.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="3.4" fill="{rust}" />')
    return lines


def _draw_haft(box: ContentBox, v: _Variant, cx: float, bottom_v: float) -> list[str]:
    hw = v.haft_half_width
    base = rgb(v.haft_tone)
    shadow = rgba(shade(v.haft_tone, -0.30), 0.6)
    highlight = rgba(shade(v.haft_tone, 0.30), 0.5)
    top = v.haft_base_v
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
    hw = v.haft_half_width + 1.6
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
    hw = v.haft_half_width
    tone = rgba(shade(v.haft_tone, -0.25), 0.7 if v.wear != "heavy" else 0.4)
    lines = []
    step = 10.0
    vv = top_v
    while vv < bottom_v:
        x0, y0 = _pt(box, cx - hw, vv)
        # Clamp the stroke's far endpoint short of `bottom_v` by half
        # the stroke width -- an unclamped `vv + 5.0` can land past the
        # content box's own bottom edge for a wrap whose `bottom_v`
        # sits close to the haft's own end, and even a clamp to
        # exactly `bottom_v` still bleeds the stroke's own half-width
        # past it, since a butt cap only stops extension along the
        # line's own direction, not across its thickness.
        x1, y1 = _pt(box, cx + hw, min(vv + 5.0, bottom_v - 1.0))
        lines.append(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="{tone}" stroke-width="2" />')
        vv += step
    return lines


def build(index: int, box: ContentBox) -> str:
    """Return one complete SVG document string for Wasay variant
    `index` (0-9), drawn inside `box` per the module docstring."""
    if not 0 <= index <= 9:
        raise ValueError(f"variant index out of range: {index!r}")
    v = _VARIANTS[index]
    cx = box.width / 2  # local-space centre, same x every variant
    bottom_v = box.height

    lines: list[str] = open_cell_svg()
    lines += _draw_haft(box, v, cx, bottom_v)
    lines += _draw_grip_wrap(box, v, cx)
    lines += _draw_head(box, v)
    lines += _draw_lashing(box, v, cx)
    return close_cell_svg(lines)


register("wasay", build)
