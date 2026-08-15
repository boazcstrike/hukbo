"""Busog -- ten authored variants of row 5 (`frame.ROLE_ROWS[5]`).

Historical constraint (CLAUDE.md section 7, design section 6,
`WeaponVisualCatalog.BusogB1`): the war bow, evidence tier Documented
-- among the best-attested items in the game. Pigafetta records the
poisoned arrow that struck Magellan through the leg at Mactan on
27 April 1521, and his own 1521 vocabulary records *bossugh* (bosog),
descending from Proto-Austronesian *busuʀ* with a zero-year gap to the
depicted period. Form, from the catalog: "tall bow arc outside the
torso silhouette, pale reed arrows with hardwood (not iron) points,
clearly visible back quiver."

**No quiver is drawn anywhere in this file, and no arrows, and no
bowstring.** The weapon sprite cell is drawn rotated about the grip,
along the `WeaponStart`-to-`WeaponEnd` line, because the bow swings
with the arm; a back quiver is fixed to the torso and would swing with
it if baked into this cell, which is wrong on its own terms even
before noting that nothing in `PawnRenderer` or `PawnGeometry` ever
draws a quiver at all -- the catalog's "clearly visible back quiver"
is descriptive intent recorded in a comment, not shipped geometry, and
this row must not invent one. `GetBowstringLine`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1755`) pulls the string's
midpoint off the stave by `RangedDrawTension`, which is a genuine
per-frame deformation a baked cell cannot carry, and the renderer
keeps drawing two procedural line-quad segments over this sprite
(design section 9). Every variant below draws the stave only. The
stave's two ends -- `box.grip_anchor` at the bottom and the sampled
tip point near the top -- are the same two points `WeaponStart`/
`WeaponEnd` already anchor, so the procedural string's endpoints land
on the drawn stave with no seam.

Variation stays inside the one attested envelope (design section 7)
and lives entirely on the stave, since the row has no quiver axis to
draw on: stave tone (pale vs dark, matching `BusogTintPaleStave` /
`BusogTintDarkStave`), stave length and arc curvature within a
tall-bow envelope, limb taper (four families: standard, thick, thin,
heavy), nock-knob treatment (tip only, or both ends, and knob size),
lashing count and placement along the limb, grip-wrap extent and
material (rattan wrap vs a darker cord wrap, `wrap_tone`), and
edge/surface condition (none, chipped, or a soot-darkened patina). No
variant claims a recurve or composite typology, no variant tips
anything in iron -- there is nothing tipped at all, since the arrows
are gone -- and no variant introduces ornament from a later century.

Coordinate convention, matching `wasay.py`: every point below is
`(u, v)` in the weapon content box's own local space, `u` in
`[0, WEAPON_CONTENT_WIDTH]` (104) and `v` in `[0, WEAPON_CONTENT_HEIGHT]`
(248), `v` growing downward. `_pt` maps a local point through
`box.left`/`box.top` into the cell's absolute SVG coordinates. The
stave's centreline is a single quadratic Bezier from the grip
(bottom-centre, `v = 248`) to a tip near the top (`v` small), bowed
toward positive `u` so the arc reads clearly outside a pawn's torso
silhouette rather than sitting on the centreline where a spear would.
The bow direction is held constant across all ten variants -- only its
magnitude (`belly_offset`), the stave's length (`tip_v`), and its
taper vary -- so direction never becomes an accidental classification
signal.

BOUNDS CHECK (recorded rather than argued, via `max_lateral_excursion`
below, not eyeballed, re-measured after the quiver removal changed
several variants' `belly_offset`/`tip_v`): the widest variant is index
1 and its tail-slot recolour, index 8, both reaching a maximum lateral
excursion of `u = 84.7` near the curve's midpoint, against a 104-wide
content box -- 19.3 content-box pixels (19%) of margin on the wide
side. The tightest is index 5 at `u = 73.1`. None needed margin on the
grip side, since the curve pins `u = cx` exactly at `t = 0`. The
stave's own two tips -- the point sampled at `t = 0` (the grip,
`box.grip_anchor`) and at `t = 1` (the top of the curve, offset
`tip_offset` from centre at `v = tip_v`) -- are exactly where
`GetBowstringLine` must land its two segments; see `_draw_stave`'s
`samples[0]` and `samples[-1]`.
"""

from __future__ import annotations

import math

from tools.weapon_sprites import register
from tools.weapon_sprites.frame import ContentBox, close_cell_svg, open_cell_svg
from tools.weapon_sprites.palette import (
    CHARRED_WOOD_BROWN,
    GRIP_WARM_OCHRE,
    PALM_RATTAN_OCHRE,
    RATTAN_LASHING_TONE,
    RGB,
    rgb,
    rgba,
    shade,
)

Point = tuple[float, float]
Sample = tuple[float, Point, Point]  # (t, point, tangent)


class _Variant:
    """One cell's authored parameters. The stave centreline is fixed by
    `tip_v` (how far up the tip sits), `belly_offset`/`belly_t` (the
    Bezier control point that bows the curve), and `tip_offset` (the
    tip's own lateral offset from centre). `taper` is a list of
    `(t, half_width)` control points, interpolated piecewise-linear
    along the curve. `grip_wrap` is `None` for a bare grip, else a
    `(t0, t1)` extent near the grip, drawn in `wrap_tone` (rattan vs a
    darker cord, the wrap's "material" axis). `nock_ends` names which
    curve ends ("tip", "grip") carry a small nock knob, sized by
    `nock_scale`. `lashing_ts` is the set of curve parameters `t` at
    which a lashing band crosses the limb."""

    def __init__(
        self,
        tip_v: float,
        belly_offset: float,
        belly_t: float,
        tip_offset: float,
        taper: tuple[tuple[float, float], ...],
        grip_wrap: tuple[float, float] | None,
        wrap_tone: RGB,
        lashing_ts: tuple[float, ...],
        nock_ends: tuple[str, ...],
        nock_scale: float,
        stave_tone: RGB,
        wear: str,
        note: str,
    ) -> None:
        self.tip_v = tip_v
        self.belly_offset = belly_offset
        self.belly_t = belly_t
        self.tip_offset = tip_offset
        self.taper = taper
        self.grip_wrap = grip_wrap
        self.wrap_tone = wrap_tone
        self.lashing_ts = lashing_ts
        self.nock_ends = nock_ends
        self.nock_scale = nock_scale
        self.stave_tone = stave_tone
        self.wear = wear
        self.note = note


_TAPER_STANDARD: tuple[tuple[float, float], ...] = (
    (0.0, 8.5), (0.10, 9.0), (0.5, 5.2), (0.85, 3.4), (1.0, 2.4),
)
_TAPER_THICK: tuple[tuple[float, float], ...] = (
    (0.0, 10.0), (0.12, 10.5), (0.5, 7.0), (0.85, 4.4), (1.0, 3.0),
)
_TAPER_THIN: tuple[tuple[float, float], ...] = (
    (0.0, 7.0), (0.10, 7.4), (0.5, 4.0), (0.85, 2.4), (1.0, 1.8),
)
_TAPER_HEAVY: tuple[tuple[float, float], ...] = (
    (0.0, 9.5), (0.10, 10.0), (0.5, 6.2), (0.85, 3.8), (1.0, 3.0),
)

# One entry per variant 0-9. Variants 0-6 vary the stave outline
# (length, arc, taper, nock, lashing, wrap extent and material); 7-9
# are tail slots that reuse an already-drawn outline and vary
# tone/wear only, per design section 7's "pure recolour is permitted
# only in the tail slots" rule -- matching wasay.py's own variants 7-9.
_VARIANTS: tuple[_Variant, ...] = (
    _Variant(  # 0 -- baseline proportion, moderate arc, plain rattan grip wrap
        40, 34, 0.5, 8, _TAPER_STANDARD, (0.0, 0.10), RATTAN_LASHING_TONE,
        (), ("tip",), 1.4, GRIP_WARM_OCHRE, "none",
        "baseline proportion; moderate arc; plain rattan-wrapped grip",
    ),
    _Variant(  # 1 -- tallest bow, deepest arc (the bounds-check variant)
        14, 52, 0.48, 6, _TAPER_STANDARD, (0.0, 0.09), RATTAN_LASHING_TONE,
        (), ("tip",), 1.4, GRIP_WARM_OCHRE, "none",
        "tallest stave and deepest arc in the row (bounds-check variant)",
    ),
    _Variant(  # 2 -- compact, thick taper, heavier grip wrap
        64, 26, 0.5, 10, _TAPER_THICK, (0.0, 0.14), RATTAN_LASHING_TONE,
        (), ("tip",), 1.4, GRIP_WARM_OCHRE, "none",
        "shortest, thickest stave; longer, heavier grip wrap",
    ),
    _Variant(  # 3 -- slender long bow, bare grip, pronounced nock, two lashings
        10, 40, 0.55, 4, _TAPER_THIN, None, RATTAN_LASHING_TONE,
        (0.30, 0.62), ("tip",), 2.0, GRIP_WARM_OCHRE, "none",
        "slenderest stave; bare grip; pronounced nock knob; two limb lashings",
    ),
    _Variant(  # 4 -- taller arc, three-lashing limb, dark tone, cord-wrapped grip
        22, 44, 0.52, 9, _TAPER_STANDARD, (0.0, 0.10), CHARRED_WOOD_BROWN,
        (0.22, 0.45, 0.68), ("tip",), 1.4, PALM_RATTAN_OCHRE, "none",
        "taller, deeper arc; three lashing bands along the limb; dark tone; dark cord-wrapped grip",
    ),
    _Variant(  # 5 -- stubby heavy-taper stave, bare grip, grip-end nock, chipped
        56, 24, 0.5, 11, _TAPER_HEAVY, None, RATTAN_LASHING_TONE,
        (0.5,), ("grip",), 1.6, PALM_RATTAN_OCHRE, "chipped",
        "stubbiest, heaviest-taper stave; bare grip; nock knob at the grip end; chipped stave edge; dark tone",
    ),
    _Variant(  # 6 -- both-end nock knobs, thicker taper
        46, 32, 0.5, 9, _TAPER_HEAVY, (0.0, 0.11), RATTAN_LASHING_TONE,
        (0.5,), ("tip", "grip"), 1.4, GRIP_WARM_OCHRE, "none",
        "nock knob at both ends; thicker taper; single mid-limb lashing",
    ),
    _Variant(  # 7 -- tail: baseline outline, chipped wear, soot patina
        40, 34, 0.5, 8, _TAPER_STANDARD, (0.0, 0.10), CHARRED_WOOD_BROWN,
        (), ("tip",), 1.4, GRIP_WARM_OCHRE, "patina",
        "recolour/wear only, baseline outline; chipped edge, soot-darkened patina; dark cord-wrapped grip",
    ),
    _Variant(  # 8 -- tail: tallest outline, dark tone
        14, 52, 0.48, 6, _TAPER_STANDARD, (0.0, 0.09), RATTAN_LASHING_TONE,
        (), ("tip",), 1.4, PALM_RATTAN_OCHRE, "none",
        "recolour only, tallest/deepest-arc outline; dark stave tone",
    ),
    _Variant(  # 9 -- tail: baseline outline, heaviest wear
        40, 34, 0.5, 8, _TAPER_STANDARD, (0.0, 0.10), RATTAN_LASHING_TONE,
        (), ("tip",), 1.4, GRIP_WARM_OCHRE, "heavy",
        "recolour/wear only, baseline outline; heaviest wear: nicked edge, faded wrap, weathering dabs",
    ),
)


def _pt(box: ContentBox, u: float, v: float) -> Point:
    return (box.left + u, box.top + v)


def _poly_attr(points: tuple[Point, ...], box: ContentBox) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in (_pt(box, u, v) for u, v in points))


def _polygon(points: tuple[Point, ...], box: ContentBox, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{opacity}"' if opacity < 1.0 else ""
    return f'<polygon points="{_poly_attr(points, box)}" fill="{fill}"{op} />'


def _bezier_point(p0: Point, p1: Point, p2: Point, t: float) -> Point:
    mt = 1.0 - t
    x = mt * mt * p0[0] + 2 * mt * t * p1[0] + t * t * p2[0]
    y = mt * mt * p0[1] + 2 * mt * t * p1[1] + t * t * p2[1]
    return (x, y)


def _bezier_tangent(p0: Point, p1: Point, p2: Point, t: float) -> Point:
    mt = 1.0 - t
    dx = 2 * mt * (p1[0] - p0[0]) + 2 * t * (p2[0] - p1[0])
    dy = 2 * mt * (p1[1] - p0[1]) + 2 * t * (p2[1] - p1[1])
    return (dx, dy)


def _sample_centerline(p0: Point, p1: Point, p2: Point, n: int = 30) -> list[Sample]:
    """`n + 1` evenly-spaced `(t, point, tangent)` samples along the
    curve, save one deliberate override: the grip end's own tangent
    (`t = 0`) is pinned to straight-up (`(0, -1)`) rather than the
    curve's true, slightly-diagonal tangent there. A diagonal tangent
    at the grip end makes the perpendicular half-width offset carry a
    downward component, pushing the drawn polygon past
    `box.grip_anchor` and out of the content box on every variant with
    a nonzero `belly_offset`. A perfectly vertical tangent at that one
    sample makes the offset perpendicular exactly horizontal instead,
    so the stave's base is a flat, square-cut end sitting exactly on
    `box.bottom` -- a small, visually unremarkable change (a grip end
    is plausibly cut flat) that keeps every variant's trimmed bbox
    inside the content box."""
    samples = [
        (i / n, _bezier_point(p0, p1, p2, i / n), _bezier_tangent(p0, p1, p2, i / n))
        for i in range(n + 1)
    ]
    t0, pt0, (_dx0, dy0) = samples[0]
    samples[0] = (t0, pt0, (0.0, dy0 if dy0 < 0 else -1.0))
    return samples


def _half_width(t: float, taper: tuple[tuple[float, float], ...]) -> float:
    if t <= taper[0][0]:
        return taper[0][1]
    for (t0, h0), (t1, h1) in zip(taper, taper[1:]):
        if t0 <= t <= t1:
            frac = 0.0 if t1 == t0 else (t - t0) / (t1 - t0)
            return h0 + (h1 - h0) * frac
    return taper[-1][1]


def _normal(dx: float, dy: float) -> Point:
    length = math.hypot(dx, dy) or 1.0
    return (-dy / length, dx / length)


def max_lateral_excursion(v: _Variant, cx: float, grip_v: float) -> float:
    """The largest `|u - cx|` reached anywhere on `v`'s stave polygon
    (centreline offset plus half-width), used by the bounds check this
    module's docstring records. Exposed at module scope so a caller can
    verify every variant without re-deriving the geometry."""
    p0 = (cx, grip_v)
    p1 = (cx + v.belly_offset, grip_v - v.belly_t * (grip_v - v.tip_v))
    p2 = (cx + v.tip_offset, v.tip_v)
    worst = 0.0
    for t, (x, _y), (dx, dy) in _sample_centerline(p0, p1, p2):
        nx, _ny = _normal(dx, dy)
        hw = _half_width(t, v.taper)
        worst = max(worst, abs(x - cx) + abs(nx) * hw)
    return worst


def _draw_stave(box: ContentBox, v: _Variant, cx: float, grip_v: float) -> tuple[list[str], list[Sample]]:
    p0 = (cx, grip_v)
    p1 = (cx + v.belly_offset, grip_v - v.belly_t * (grip_v - v.tip_v))
    p2 = (cx + v.tip_offset, v.tip_v)
    samples = _sample_centerline(p0, p1, p2)

    left_full: list[Point] = []
    left_inner: list[Point] = []
    right_inner: list[Point] = []
    right_full: list[Point] = []
    for t, (x, y), (dx, dy) in samples:
        nx, ny = _normal(dx, dy)
        hw = _half_width(t, v.taper)
        left_full.append((x + nx * hw, y + ny * hw))
        left_inner.append((x + nx * hw * 0.42, y + ny * hw * 0.42))
        right_inner.append((x - nx * hw * 0.35, y - ny * hw * 0.35))
        right_full.append((x - nx * hw, y - ny * hw))

    base = rgb(v.stave_tone)
    shadow = rgba(shade(v.stave_tone, -0.35), 0.5)
    highlight = rgba(shade(v.stave_tone, 0.35), 0.55)

    base_poly = tuple(left_full) + tuple(reversed(right_full))
    shadow_poly = tuple(left_full) + tuple(reversed(left_inner))
    highlight_poly = tuple(right_inner) + tuple(reversed(right_full))

    lines = [
        _polygon(base_poly, box, base),
        _polygon(shadow_poly, box, shadow),
        _polygon(highlight_poly, box, highlight),
    ]

    if v.wear in ("chipped", "heavy"):
        mid = samples[len(samples) // 2]
        t, (x, y), (dx, dy) = mid
        nx, ny = _normal(dx, dy)
        hw = _half_width(t, v.taper)
        ex, ey = x + nx * hw, y + ny * hw
        chip_tone = rgba(shade(v.stave_tone, -0.45), 0.85)
        lines.append(
            _polygon(((ex - 3, ey - 5), (ex + 4, ey - 2), (ex - 1, ey + 6)), box, chip_tone)
        )
    if v.wear == "heavy":
        rust = rgba((150, 96, 60), 0.28)
        for t in (0.22, 0.4, 0.66):
            _tt, (x, y), _tan = samples[int(t * (len(samples) - 1))]
            cx2, cy2 = _pt(box, x, y)
            lines.append(f'<circle cx="{cx2:.1f}" cy="{cy2:.1f}" r="3.2" fill="{rust}" />')
    if v.wear == "patina":
        # Soot-darkened patina: a broad translucent wash over the
        # lower (grip-ward) half of the stave, standing in for a
        # blade carried a season rather than a discrete chip or spot.
        soot = rgba(shade(v.stave_tone, -0.55), 0.30)
        lower_half = samples[len(samples) // 2 :]
        wash_left: list[Point] = []
        wash_right: list[Point] = []
        for t, (x, y), (dx, dy) in lower_half:
            nx, ny = _normal(dx, dy)
            hw = _half_width(t, v.taper)
            wash_left.append((x + nx * hw, y + ny * hw))
            wash_right.append((x - nx * hw, y - ny * hw))
        wash_poly = tuple(wash_left) + tuple(reversed(wash_right))
        lines.append(_polygon(wash_poly, box, soot))

    return lines, samples


def _draw_grip_wrap(box: ContentBox, v: _Variant, samples: list[Sample]) -> list[str]:
    if v.grip_wrap is None:
        return []
    t0, t1 = v.grip_wrap
    tone = rgba(v.wrap_tone, 0.7 if v.wear != "heavy" else 0.4)
    lines = []
    n = len(samples)
    step = max(1, n // 14)
    for i in range(0, n, step):
        t, (x, y), (dx, dy) = samples[i]
        if not (t0 <= t <= t1):
            continue
        nx, ny = _normal(dx, dy)
        hw = _half_width(t, v.taper) + 0.6
        x0, y0 = _pt(box, x - nx * hw, y - ny * hw)
        x1, y1 = _pt(box, x + nx * hw, y + ny * hw)
        lines.append(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="{tone}" stroke-width="2" />')
    return lines


def _draw_lashing(box: ContentBox, v: _Variant, samples: list[Sample]) -> list[str]:
    if not v.lashing_ts:
        return []
    tone = rgb(RATTAN_LASHING_TONE)
    opacity = 0.55 if v.wear == "heavy" else 1.0
    lines = []
    n = len(samples)
    for lt in v.lashing_ts:
        t, (x, y), (dx, dy) = samples[min(n - 1, int(round(lt * (n - 1))))]
        hw = _half_width(t, v.taper) + 1.6
        deg = math.degrees(math.atan2(dy, dx)) + 90.0
        cx0, cy0 = _pt(box, x, y)
        rw = hw * 2
        rh = 4.6
        rx0 = cx0 - rw / 2
        ry0 = cy0 - rh / 2
        lines.append(
            f'<rect x="{rx0:.1f}" y="{ry0:.1f}" width="{rw:.1f}" height="{rh:.1f}" '
            f'fill="{tone}" fill-opacity="{opacity}" rx="1" '
            f'transform="rotate({deg:.1f} {cx0:.1f} {cy0:.1f})" />'
        )
    return lines


def _draw_nock(box: ContentBox, v: _Variant, samples: list[Sample]) -> list[str]:
    """A small knob at one or both curve ends. The knob's centre is
    pulled back along the centreline, toward the stave's own body, by
    its own radius -- rather than centred exactly on the end sample --
    so its outer edge meets the end point instead of the circle
    bulging past it. That keeps a grip-end knob (a large radius, since
    the taper is widest there) from bleeding the drawn content past
    `box.bottom`; a tip-end knob is unaffected in practice because the
    taper is narrowest at the tip."""
    tone = rgba(shade(v.stave_tone, 0.45), 0.9)
    lines = []
    ends = {"grip": (samples[0], 1.0), "tip": (samples[-1], -1.0)}
    for name in v.nock_ends:
        (t, (x, y), (dx, dy)), sign = ends[name]
        hw = _half_width(t, v.taper)
        r = hw * v.nock_scale * 0.6
        tlen = math.hypot(dx, dy) or 1.0
        ux, uy = sign * dx / tlen, sign * dy / tlen
        cx0, cy0 = _pt(box, x + ux * r, y + uy * r)
        lines.append(f'<circle cx="{cx0:.1f}" cy="{cy0:.1f}" r="{r:.1f}" fill="{tone}" />')
    return lines


def build(index: int, box: ContentBox) -> str:
    """Return one complete SVG document string for Busog variant
    `index` (0-9), drawn inside `box` per the module docstring. Draws
    the stave only -- no quiver, no arrows, no bowstring, ever."""
    if not 0 <= index <= 9:
        raise ValueError(f"variant index out of range: {index!r}")
    v = _VARIANTS[index]
    cx = box.width / 2  # local-space centre, same x every variant
    grip_v = box.height

    lines: list[str] = open_cell_svg()
    stave_lines, samples = _draw_stave(box, v, cx, grip_v)
    lines += stave_lines
    lines += _draw_grip_wrap(box, v, samples)
    lines += _draw_lashing(box, v, samples)
    lines += _draw_nock(box, v, samples)
    return close_cell_svg(lines)


register("busog", build)
