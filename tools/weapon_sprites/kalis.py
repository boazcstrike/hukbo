"""Kalis (row 2) -- ten authored variants of the L1 pawn-scale
silhouette (``WeaponVisualCatalog.KalisL1``,
``src/Hukbo.Client/Presentation/Catalogs/WeaponVisualCatalog.cs:203``).

Historical constraint, binding per CLAUDE.md section 7 and design
section 6: Pigafetta records ``calis`` at Cebu in 1521, and the term
recurs in vocabularies from 1612 onward across language groups --
evidence tier Documented, the strongest of the four melee rows. L1 is
"slim, straight, symmetric, one-handed -- conservative, documented-
for-name-and-class form" and is the *only* pawn-scale Kalis
silhouette. L2 (half-waved) and L3 (fully-waved) exist solely for the
inspector and the armory card and are **never** drawn here -- the
selection stream never returns them, and neither does this module
(``WeaponVisualCatalog.cs:219-258``). This is the hardest prohibition
on this row: the wavy kris is the famous shape, and it is exactly what
these ten cells may not show. No variant below introduces any wave or
any left-right asymmetric profile; every outline is a straight,
mirror-symmetric taper from guard to tip.

Design section 4 ties the Kalis with the Kampilan for the longest
weapon line in the game at 19.1 layout units, so this is the long,
lean row -- the blade run below claims roughly four-fifths of the
content box's own height, more than the guard/grip/pommel stack takes,
and is authored narrower than the Kampilan's uniform-width blade to
keep the "slim" reading distinct from that row's "broad, uniform"
reading (design section 7's per-row distinctness, not a historical
literal-width claim -- exaggeration for 46px legibility is authorized
uniformly across the row by the same section).

Variation stays inside the attested slim-straight-symmetric envelope
(design section 7 and the brief's explicit axis list): blade
proportion (length/width), taper rate (where along the blade the width
is shed), tip geometry (acute point vs a rounded/resharpened flat tip,
both still symmetric), edge damage, grip-wrap extent, guard and
pommel plainness, and surface (patina, resin sheen). Variants 0-6 each
spend one outline axis; variants 7-9 are pure recolour/wear over an
earlier variant's own outline, following the precedent
``wasay.py`` sets for putting edge-condition detail (chips/nicks) in
the tail slots rather than the outline axes. The shipped Kalis catalog
carries exactly two tints -- ``KalisTintFreshIron`` (iron-blue-black
blade, warm-ochre grip) and ``KalisTintDarkHilt`` (iron-blue-black
blade, charred-wood grip) -- and this module invents no third blade
material; every variant below draws the same iron-blue-black blade and
alternates only the grip tone between those two shipped colours.

Coordinate convention: every point below is computed directly in the
cell's absolute SVG space through ``box.left``/``box.top`` via
``ContentBox``'s own properties (``center_x``, ``bottom``, ``top`` on
the *box* it is handed, never a bare literal), matching the pattern
``kampilan.py`` uses. A blade tip is a narrow acute angle; SVG's
default miter join extends a stroke well past the geometric point at
that angle, so every stroked path here uses a round join.
"""

from __future__ import annotations

from tools.weapon_sprites import frame, palette, register

# DyePalette.IronBlueBlack (src/Hukbo.Client/Presentation/Catalogs/
# DyePalette.cs:97), mirrored verbatim -- the fixed blade tone both
# shipped Kalis tints use (WeaponVisualCatalog.cs:279-311). Not in
# palette.py because this file may only touch kalis.py; the value is
# copied from the catalog, not invented.
IRON_BLUE_BLACK: palette.RGB = (56, 66, 73)

# The two shipped Kalis tints (WeaponVisualCatalog.cs:279-311): the
# blade tone never changes, only the grip.
_TINT_FRESH_IRON = (IRON_BLUE_BLACK, palette.GRIP_WARM_OCHRE)
_TINT_DARK_HILT = (IRON_BLUE_BLACK, palette.CHARRED_WOOD_BROWN)

_TOP_MARGIN = 9.0  # clearance between the tip and the content box's own top edge
_BOTTOM_INSET = 2.0  # pulls the pommel's stroke back off the box's own bottom edge

# A blade tip is a narrow acute angle; SVG's default miter join extends a
# stroke well past the geometric point at an angle that sharp. Every
# stroked path uses a round join instead, so the drawn extent never
# exceeds the fill outline by more than half the stroke width.
_ROUND_JOIN = 'stroke-linejoin="round"'


def _path_d(points: list[tuple[float, float]]) -> str:
    head, *rest = points
    cmds = [f"M{head[0]:.1f},{head[1]:.1f}"]
    cmds.extend(f"L{x:.1f},{y:.1f}" for x, y in rest)
    cmds.append("Z")
    return " ".join(cmds)


def _edge_chain(
    cx: float,
    sign: float,
    y_guard: float,
    half_guard: float,
    y_mid: float,
    half_mid: float,
    nicks: tuple[float, ...],
) -> list[tuple[float, float]]:
    """One side (``sign`` -1 left, +1 right) of the guard-to-mid run,
    in order from the guard outward. ``nicks`` are fractions ``t`` of
    that run where a small inward bite is cut into the edge -- the
    edge-damage axis. Both edges are built by calling this with the
    same ``nicks``, so damage always reads mirrored and the profile
    stays symmetric."""
    pts: list[tuple[float, float]] = [(cx + sign * half_guard, y_guard)]
    for t in sorted(nicks):
        y = y_guard + t * (y_mid - y_guard)
        hw = half_guard + t * (half_mid - half_guard)
        pts.append((cx + sign * hw, y - 2.4))
        pts.append((cx + sign * (hw - 4.0), y))
        pts.append((cx + sign * hw, y + 2.4))
    pts.append((cx + sign * half_mid, y_mid))
    return pts


def _blade_points(
    cx: float,
    y_guard: float,
    half_guard: float,
    y_mid: float,
    half_mid: float,
    y_tip: float,
    half_tip: float,
    tip_style: str,
    nicks: tuple[float, ...],
) -> list[tuple[float, float]]:
    """Symmetric, straight-taper blade outline: guard -> mid control
    point -> tip -> mirrored mid -> mirrored guard. ``tip_style``
    "acute" closes to a single point; "blunt" (a rolled or resharpened
    tip, still symmetric) closes across a small flat cap of half-width
    ``half_tip``. No point on this outline is ever offset left-right
    from its mirror -- the one thing design section 6 forbids on this
    row is any asymmetric profile."""
    left = _edge_chain(cx, -1.0, y_guard, half_guard, y_mid, half_mid, nicks)
    right = _edge_chain(cx, 1.0, y_guard, half_guard, y_mid, half_mid, nicks)
    if tip_style == "acute":
        tip_pts = [(cx, y_tip)]
    else:  # "blunt" -- resharpened/rolled tip, still mirror-symmetric
        tip_pts = [(cx - half_tip, y_tip), (cx + half_tip, y_tip)]
    return [*left, *tip_pts, *list(reversed(right))]


# Each row: (pommel, grip, guard, half_guard, mid_t, half_mid, tip_style,
# half_tip, grip_half, pommel_half, wrap, lashings, nicks, tint, patina,
# sheen). ``mid_t`` is the fraction of the guard-to-tip run, measured
# from the guard, where the taper's control point sits -- small values
# put the control point near the guard (width sheds fast, then a long
# slender parallel run to the point); large values put it near the tip
# (width holds for most of the blade, then sheds fast right at the
# end). Variants 7-9 reuse an earlier variant's own outline numbers
# verbatim and vary only tint, patina, sheen, or nicks -- the
# recolour/wear tail, per design section 7 and the ``wasay.py``
# precedent for putting edge damage in the tail.
_VARIANTS: tuple[dict, ...] = (
    dict(  # 0 -- baseline proportion, mid-envelope taper, acute point
        pommel=7.0, grip=26.0, guard=6.0, half_guard=11.0, mid_t=0.5,
        half_mid=6.5, tip="acute", half_tip=0.0, grip_half=6.5,
        pommel_half=8.0, wrap=True, lashings=(0.3, 0.7), nicks=(),
        tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 1 -- proportion axis: longer, narrower blade (lean end of the envelope)
        pommel=6.0, grip=22.0, guard=5.0, half_guard=9.0, mid_t=0.5,
        half_mid=5.0, tip="acute", half_tip=0.0, grip_half=5.5,
        pommel_half=7.0, wrap=True, lashings=(0.5,), nicks=(),
        tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 2 -- proportion axis: shorter, broader blade (still the slim envelope's upper bound)
        pommel=9.0, grip=32.0, guard=8.0, half_guard=13.0, mid_t=0.5,
        half_mid=8.0, tip="acute", half_tip=0.0, grip_half=8.0,
        pommel_half=10.0, wrap=True, lashings=(0.25, 0.5, 0.75), nicks=(),
        tint=_TINT_DARK_HILT, patina=0.0, sheen=False,
    ),
    dict(  # 3 -- taper-rate axis: sheds width fast after the guard, long slender run to the point
        pommel=7.0, grip=26.0, guard=6.0, half_guard=11.5, mid_t=0.22,
        half_mid=5.5, tip="acute", half_tip=0.0, grip_half=6.5,
        pommel_half=8.0, wrap=True, lashings=(0.3, 0.7), nicks=(),
        tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 4 -- taper-rate axis: holds width for most of the blade, sheds it near the tip
        pommel=7.0, grip=26.0, guard=6.0, half_guard=11.0, mid_t=0.82,
        half_mid=9.5, tip="acute", half_tip=0.0, grip_half=6.5,
        pommel_half=8.0, wrap=True, lashings=(0.3, 0.7), nicks=(),
        tint=_TINT_DARK_HILT, patina=0.0, sheen=False,
    ),
    dict(  # 5 -- tip-geometry axis: rounded/resharpened flat tip, still symmetric
        pommel=7.0, grip=26.0, guard=6.0, half_guard=11.0, mid_t=0.55,
        half_mid=7.0, tip="blunt", half_tip=2.6, grip_half=6.5,
        pommel_half=8.0, wrap=True, lashings=(0.3, 0.7), nicks=(),
        tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 6 -- grip/guard-plainness axis: bare unwrapped grip, thin plain guard band, no lashings
        pommel=9.0, grip=28.0, guard=4.0, half_guard=10.5, mid_t=0.5,
        half_mid=6.0, tip="acute", half_tip=0.0, grip_half=6.5,
        pommel_half=9.0, wrap=False, lashings=(), nicks=(),
        tint=_TINT_DARK_HILT, patina=0.0, sheen=False,
    ),
    dict(  # 7 -- tail: variant 0's outline, dark-hilt tint, patina wear over the blade
        pommel=7.0, grip=26.0, guard=6.0, half_guard=11.0, mid_t=0.5,
        half_mid=6.5, tip="acute", half_tip=0.0, grip_half=6.5,
        pommel_half=8.0, wrap=True, lashings=(0.3, 0.7), nicks=(),
        tint=_TINT_DARK_HILT, patina=0.35, sheen=False,
    ),
    dict(  # 8 -- tail: variant 1's outline, mirrored edge nicks (edge-damage axis)
        pommel=6.0, grip=22.0, guard=5.0, half_guard=9.0, mid_t=0.5,
        half_mid=5.0, tip="acute", half_tip=0.0, grip_half=5.5,
        pommel_half=7.0, wrap=True, lashings=(0.5,), nicks=(0.3, 0.65),
        tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 9 -- tail: variant 2's outline, fresh-iron tint, resin sheen over the blade
        pommel=9.0, grip=32.0, guard=8.0, half_guard=13.0, mid_t=0.5,
        half_mid=8.0, tip="acute", half_tip=0.0, grip_half=8.0,
        pommel_half=10.0, wrap=True, lashings=(0.25, 0.5, 0.75), nicks=(),
        tint=_TINT_FRESH_IRON, patina=0.0, sheen=True,
    ),
)


def build(index: int, box: frame.ContentBox) -> str:
    v = _VARIANTS[index]
    blade_color, grip_color = v["tint"]

    cx = box.center_x
    y_bottom = box.bottom
    y_tip = box.top + _TOP_MARGIN
    blade_len = box.height - _TOP_MARGIN - v["pommel"] - v["grip"] - v["guard"]
    y_guard_top = y_tip + blade_len
    y_grip_top = y_guard_top + v["guard"]
    y_pommel_top = y_grip_top + v["grip"]
    y_mid = y_guard_top - v["mid_t"] * blade_len

    parts: list[str] = []

    # -- pommel: plain, non-zoomorphic curve only (design section 6) --
    pommel_bottom = y_bottom - _BOTTOM_INSET
    pommel_r = (pommel_bottom - y_pommel_top) / 2.0
    pommel_cy = (y_pommel_top + pommel_bottom) / 2.0
    parts.append(
        f'<ellipse cx="{cx:.1f}" cy="{pommel_cy:.1f}" '
        f'rx="{v["pommel_half"]:.1f}" ry="{max(pommel_r, 2.0):.1f}" '
        f'fill="{palette.rgb(palette.shade(grip_color, -0.15))}" '
        f'stroke="{palette.rgb(palette.shade(grip_color, -0.55))}" '
        f'stroke-width="2.4"/>'
    )

    # -- grip --
    grip_half = v["grip_half"]
    grip_pts = [
        (cx - grip_half, y_grip_top),
        (cx - grip_half, y_pommel_top),
        (cx + grip_half, y_pommel_top),
        (cx + grip_half, y_grip_top),
    ]
    parts.append(
        f'<path d="{_path_d(grip_pts)}" fill="{palette.rgb(grip_color)}" '
        f'stroke="{palette.rgb(palette.shade(grip_color, -0.5))}" '
        f'stroke-width="2.4" {_ROUND_JOIN}/>'
    )
    if v["wrap"]:
        # cord wrap -- a few diagonal ticks, precedent kampilan.py
        step = (y_pommel_top - y_grip_top) / 4.0
        wrap_lines = " ".join(
            f'M{cx - grip_half - 1.0:.1f},{y_grip_top + step * i + step * 0.25:.1f} '
            f'L{cx + grip_half + 1.0:.1f},{y_grip_top + step * i + step * 0.85:.1f}'
            for i in range(4)
        )
        parts.append(
            f'<path d="{wrap_lines}" stroke="'
            f'{palette.rgb(palette.RATTAN_LASHING_TONE)}" stroke-width="2.2" '
            f'fill="none" opacity="0.85"/>'
        )
    else:
        # bare grip: plain vertical grain, no wrap cord
        parts.append(
            f'<path d="M{cx:.1f},{y_grip_top + 2.0:.1f} '
            f'L{cx:.1f},{y_pommel_top - 2.0:.1f}" '
            f'stroke="{palette.rgb(palette.shade(grip_color, -0.3))}" '
            f'stroke-width="1.5" fill="none" opacity="0.6"/>'
        )

    # -- guard / ferrule band --
    guard_half = grip_half + 2.5
    guard_pts = [
        (cx - guard_half, y_guard_top),
        (cx - guard_half, y_grip_top),
        (cx + guard_half, y_grip_top),
        (cx + guard_half, y_guard_top),
    ]
    parts.append(
        f'<path d="{_path_d(guard_pts)}" '
        f'fill="{palette.rgb(palette.PALM_RATTAN_OCHRE)}" '
        f'stroke="{palette.rgb(palette.shade(palette.PALM_RATTAN_OCHRE, -0.5))}" '
        f'stroke-width="2.2" {_ROUND_JOIN}/>'
    )

    # -- lashing bands (count/placement axis, design section 7) --
    for frac in v["lashings"]:
        y = y_guard_top + frac * (y_grip_top - y_guard_top)
        parts.append(
            f'<rect x="{cx - guard_half - 1.3:.1f}" y="{y - 1.5:.1f}" '
            f'width="{(guard_half + 1.3) * 2:.1f}" height="3.0" '
            f'fill="{palette.rgb(palette.RATTAN_LASHING_TONE)}" '
            f'stroke="{palette.rgb(palette.shade(palette.RATTAN_LASHING_TONE, -0.4))}" '
            f'stroke-width="1.1"/>'
        )

    # -- blade: symmetric straight taper, guard -> mid -> tip -- (design section 6)
    blade_pts = _blade_points(
        cx, y_guard_top, v["half_guard"], y_mid, v["half_mid"],
        y_tip, v["half_tip"], v["tip"], v["nicks"],
    )
    parts.append(
        f'<path d="{_path_d(blade_pts)}" fill="{palette.rgb(blade_color)}" '
        f'stroke="{palette.rgb(palette.shade(blade_color, -0.5))}" '
        f'stroke-width="2.8" {_ROUND_JOIN}/>'
    )
    # centre spine shadow -- the medial-ridge cast shadow, kept on the
    # blade's own axis so it never reads as lit from one side (which
    # would look like an asymmetric profile on a weapon this row must
    # keep symmetric)
    parts.append(
        f'<path d="M{cx:.1f},{y_guard_top - 3.0:.1f} '
        f'L{cx:.1f},{y_tip + blade_len * (1.0 - v["mid_t"]) * 0.15:.1f}" '
        f'stroke="{palette.rgb(palette.shade(blade_color, -0.35))}" '
        f'stroke-width="2.6" fill="none" opacity="0.5"/>'
    )
    # cutting-edge highlights -- BOTH edges, because a symmetric
    # thrusting blade cuts on both sides; brightest value on the cell
    # sits here (design section 7's craft rule), mirrored left and
    # right so the highlight itself never becomes an asymmetry
    for sign in (-1.0, 1.0):
        edge_line = " ".join(
            f'{"M" if i == 0 else "L"}{x:.1f},{y:.1f}'
            for i, (x, y) in enumerate((
                (cx + sign * v["half_guard"] - sign * 2.4, y_guard_top - 3.0),
                (cx + sign * v["half_mid"] - sign * 2.0, y_mid),
                (cx + sign * (v["half_tip"] if v["tip"] == "blunt" else 0.0), y_tip + 3.0),
            ))
        )
        parts.append(
            f'<path d="{edge_line}" '
            f'stroke="{palette.rgb(palette.shade(blade_color, 0.5))}" '
            f'stroke-width="2.0" fill="none" opacity="0.9" {_ROUND_JOIN}/>'
        )

    # -- surface axis: patina / resin sheen (tail variants 7 and 9) --
    if v["patina"] > 0.0:
        parts.append(
            f'<path d="{_path_d(blade_pts)}" fill="'
            f'{palette.rgba(palette.CHARRED_WOOD_BROWN, min(v["patina"], 0.6))}"/>'
        )
    if v["sheen"]:
        parts.append(
            f'<path d="M{cx - v["half_mid"] * 0.3:.1f},{y_guard_top - 20.0:.1f} '
            f'L{cx + v["half_mid"] * 0.15:.1f},{y_tip + 24.0:.1f}" '
            f'stroke="{palette.rgba((255, 255, 255), 0.35)}" '
            f'stroke-width="3.4" fill="none"/>'
        )

    return frame.close_cell_svg([*frame.open_cell_svg(), *parts])


register("kalis", build)
