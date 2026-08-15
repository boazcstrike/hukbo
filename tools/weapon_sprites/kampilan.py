"""Kampilan (row 0) -- ten authored variants of the K1 pawn-scale
silhouette (``WeaponVisualCatalog.KampilanK1``,
``src/Hukbo.Client/Presentation/Catalogs/WeaponVisualCatalog.cs:330``).

Historical constraint, binding per CLAUDE.md section 7 and design
section 6: Pigafetta attests the *shape* of a large single-edged
cutting sword at Mactan in 1521 but gives no local name for it -- the
Kampilan identification is later tradition. Evidence tier: Documented,
form uncertain. K1 is the *only* pawn-scale silhouette; the ornamented
K2 form (bifurcated pommel, tip spikelet, hair tassels, chain-mail
guard, zoomorphic pommel motif) is 18th-19th century and is never
drawn here -- see ``WeaponVisualCatalog.cs:355-374``. The later
widening-profile / truncated-tip form is likewise not drawn; the blade
holds one uniform width from guard to the tip taper
(``WeaponVisualCatalog.cs:340-344``). Where a pommel is drawn at all
it is a plain, non-zoomorphic curve.

Design section 7's resolution to the art/history tension: ten cells
vary the OUTLINE inside the attested K1 envelope -- proportion, edge
condition, lashing, grip treatment, surface -- never a new blade
profile. Variants 0-6 spend the outline axes (one each, roughly);
variants 7-9 are pure tint/wear atop variant 0's own outline. See the
``_VARIANTS`` table below for which axis each index spends.

Gameplay-legibility choices below are provisional in the same voice
``CreateWeaponBounds`` already uses for the ranged lines: blade width
is exaggerated well past a realistic single-edged sword so the
silhouette reads at the roughly 46px on-screen length the design's
section 4 arithmetic gives a maximum-zoom Kampilan (19.1 layout units
at apparent scale 2.40). Two or three value bands per part, brightest
highlight kept on the cutting edge, per design section 7.
"""

from __future__ import annotations

from tools.weapon_sprites import frame, palette, register

# DyePalette.IronBlueBlack (src/Hukbo.Client/Presentation/Catalogs/
# DyePalette.cs:97), mirrored verbatim -- this is the shipped fresh-
# iron blade tint KampilanTintFreshIron and KampilanTintOchreHilt
# already use (WeaponVisualCatalog.cs:394-407, 432-443). Not in
# palette.py because this file may only touch kampilan.py; the value
# is copied from the catalog, not invented.
IRON_BLUE_BLACK: palette.RGB = (56, 66, 73)

# Three tints, matching the three shipped KampilanTint* entries
# (WeaponVisualCatalog.cs:388-451) so a sprite cell reads as the same
# object the procedural draw already tints.
_TINT_FRESH_IRON = (IRON_BLUE_BLACK, palette.CHARRED_WOOD_BROWN)
_TINT_WORN_IRON = (palette.IRON_WORN_GREY, palette.CHARRED_WOOD_BROWN)
_TINT_OCHRE_HILT = (IRON_BLUE_BLACK, palette.PALM_RATTAN_OCHRE)

_TOP_MARGIN = 9.0  # clearance between the tip and the content box's own top edge
_BOTTOM_INSET = 2.0  # pulls the pommel's stroke back off the box's own bottom edge

# A blade tip is a narrow acute angle; SVG's default miter join extends a
# stroke well past the geometric point at an angle that sharp, which pushed
# drawn content outside frame.WEAPON_BOX during the probe measurement below.
# Every stroked path uses a round join instead, so the drawn extent never
# exceeds the fill outline by more than half the stroke width.
_ROUND_JOIN = 'stroke-linejoin="round"'


def _path_d(points: list[tuple[float, float]]) -> str:
    head, *rest = points
    cmds = [f"M{head[0]:.1f},{head[1]:.1f}"]
    cmds.extend(f"L{x:.1f},{y:.1f}" for x, y in rest)
    cmds.append("Z")
    return " ".join(cmds)


def _blade_points(
    cx: float,
    y_guard: float,
    y_tip: float,
    half_width: float,
    tip_style: str,
    nicks: tuple[float, ...],
) -> list[tuple[float, float]]:
    """Uniform-width blade outline (K1's binding constraint -- design
    section 6, ``WeaponVisualCatalog.cs:340-344``): the spine side is
    a straight vertical run from the guard to the tip taper, and the
    only place the outline narrows is the short taper at the very tip
    itself, which every drawn sword needs to terminate at a point and
    is not the later "widening profile". ``nicks`` (edge-condition
    axis, design section 7) carve small inward dents into the edge
    side only, at fractions ``t`` of the guard-to-shoulder run."""
    taper = 16.0 if tip_style == "point" else 11.0
    shoulder_y = y_tip + taper

    points: list[tuple[float, float]] = [(cx - half_width, y_guard)]
    points.append((cx - half_width, shoulder_y))
    if tip_style == "point":
        points.append((cx, y_tip))
    else:  # "blunt" -- a rolled/resharpened tip, edge-condition axis
        flat = half_width * 0.34
        points.append((cx - flat, y_tip))
        points.append((cx + flat, y_tip))
    points.append((cx + half_width, shoulder_y))

    for t in sorted(nicks):
        y = shoulder_y + t * (y_guard - shoulder_y)
        points.append((cx + half_width, y - 2.4))
        points.append((cx + half_width - 4.0, y))
        points.append((cx + half_width, y + 2.4))
    points.append((cx + half_width, y_guard))
    return points


# Each row: (pommel_len, grip_len, guard_len, blade_half, grip_half,
# pommel_half, tip_style, nicks, lashing fractions, wrap, tint).
# Axis spent noted per row; 7-9 reuse variant 0's own outline
# (pommel/grip/guard/blade_half/grip_half/pommel_half/tip/nicks/
# lashings/wrap) and vary only tint and a surface overlay, per design
# section 7's "pure tint/wear fills 7-9".
_VARIANTS: tuple[dict, ...] = (
    dict(  # 0 -- baseline proportion, mid-envelope
        pommel=8.0, grip=34.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="point", nicks=(), lashings=(0.3, 0.7),
        wrap=True, tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 1 -- proportion axis: longer, narrower blade (still one envelope)
        pommel=6.0, grip=26.0, guard=6.0, blade_half=12.5, grip_half=7.0,
        pommel_half=9.0, tip="point", nicks=(), lashings=(0.5,),
        wrap=True, tint=_TINT_WORN_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 2 -- proportion axis: shorter, broader blade
        pommel=10.0, grip=40.0, guard=7.0, blade_half=18.0, grip_half=9.5,
        pommel_half=12.0, tip="point", nicks=(), lashings=(0.25, 0.5, 0.75),
        wrap=True, tint=_TINT_OCHRE_HILT, patina=0.0, sheen=False,
    ),
    dict(  # 3 -- edge-condition axis: chips/nicks along the cutting edge
        pommel=8.0, grip=34.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="point", nicks=(0.15, 0.4, 0.65),
        lashings=(0.3, 0.7), wrap=True, tint=_TINT_WORN_IRON,
        patina=0.0, sheen=False,
    ),
    dict(  # 4 -- edge-condition axis: rolled/blunted, resharpened tip
        pommel=8.0, grip=34.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="blunt", nicks=(), lashings=(0.3, 0.7),
        wrap=True, tint=_TINT_FRESH_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 5 -- lashing axis: denser bindings, wider ferrule
        pommel=8.0, grip=30.0, guard=10.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="point", nicks=(),
        lashings=(0.15, 0.4, 0.65, 0.9), wrap=True, tint=_TINT_OCHRE_HILT,
        patina=0.0, sheen=False,
    ),
    dict(  # 6 -- grip-treatment axis: bare (unwrapped) grip, plainer flared pommel
        pommel=13.0, grip=30.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=14.0, tip="point", nicks=(), lashings=(),
        wrap=False, tint=_TINT_WORN_IRON, patina=0.0, sheen=False,
    ),
    dict(  # 7 -- surface axis (pure tint/wear): soot patina over variant 0's outline
        pommel=8.0, grip=34.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="point", nicks=(), lashings=(0.3, 0.7),
        wrap=True, tint=_TINT_FRESH_IRON, patina=0.35, sheen=False,
    ),
    dict(  # 8 -- surface axis (pure tint/wear): resin sheen over variant 0's outline
        pommel=8.0, grip=34.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="point", nicks=(), lashings=(0.3, 0.7),
        wrap=True, tint=_TINT_OCHRE_HILT, patina=0.0, sheen=True,
    ),
    dict(  # 9 -- surface axis (pure tint/wear): heavy patina, worn-iron combo
        pommel=8.0, grip=34.0, guard=6.0, blade_half=15.0, grip_half=8.0,
        pommel_half=10.0, tip="point", nicks=(), lashings=(0.3, 0.7),
        wrap=True, tint=_TINT_WORN_IRON, patina=0.5, sheen=False,
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
        f'stroke-width="2.6"/>'
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
        f'stroke-width="2.6" {_ROUND_JOIN}/>'
    )
    if v["wrap"]:
        # cord wrap -- a few diagonal ticks, precedent
        # tools/gen_pawn_bodies.py:163 (spear grip binding)
        step = (y_pommel_top - y_grip_top) / 4.0
        wrap_lines = " ".join(
            f'M{cx - grip_half - 1.0:.1f},{y_grip_top + step * i + step * 0.25:.1f} '
            f'L{cx + grip_half + 1.0:.1f},{y_grip_top + step * i + step * 0.85:.1f}'
            for i in range(4)
        )
        parts.append(
            f'<path d="{wrap_lines}" stroke="'
            f'{palette.rgb(palette.RATTAN_LASHING_TONE)}" stroke-width="2.4" '
            f'fill="none" opacity="0.85"/>'
        )
    else:
        # bare grip: plain vertical grain, no wrap cord
        parts.append(
            f'<path d="M{cx:.1f},{y_grip_top + 2.0:.1f} '
            f'L{cx:.1f},{y_pommel_top - 2.0:.1f}" '
            f'stroke="{palette.rgb(palette.shade(grip_color, -0.3))}" '
            f'stroke-width="1.6" fill="none" opacity="0.6"/>'
        )

    # -- guard / ferrule band --
    guard_half = grip_half + 3.0
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
        f'stroke-width="2.6" {_ROUND_JOIN}/>'
    )

    # -- lashing bands (count/placement axis, design section 7) --
    # anchored around the ferrule/grip seam, spaced by fraction of the
    # guard band's own length
    for frac in v["lashings"]:
        y = y_guard_top + frac * (y_grip_top - y_guard_top)
        parts.append(
            f'<rect x="{cx - guard_half - 1.5:.1f}" y="{y - 1.6:.1f}" '
            f'width="{(guard_half + 1.5) * 2:.1f}" height="3.2" '
            f'fill="{palette.rgb(palette.RATTAN_LASHING_TONE)}" '
            f'stroke="{palette.rgb(palette.shade(palette.RATTAN_LASHING_TONE, -0.4))}" '
            f'stroke-width="1.2"/>'
        )

    # -- blade --
    blade_pts = _blade_points(
        cx, y_guard_top, y_tip, v["blade_half"], v["tip"], v["nicks"]
    )
    parts.append(
        f'<path d="{_path_d(blade_pts)}" fill="{palette.rgb(blade_color)}" '
        f'stroke="{palette.rgb(palette.shade(blade_color, -0.5))}" '
        f'stroke-width="3.2" {_ROUND_JOIN}/>'
    )
    # spine shadow band, inset from the left (spine) side
    parts.append(
        f'<path d="M{cx - v["blade_half"] + 4.0:.1f},{y_guard_top - 4.0:.1f} '
        f'L{cx - v["blade_half"] + 4.0:.1f},{y_tip + 14.0:.1f}" '
        f'stroke="{palette.rgb(palette.shade(blade_color, -0.4))}" '
        f'stroke-width="3.0" fill="none" opacity="0.75"/>'
    )
    # cutting-edge highlight -- the brightest value on the whole cell
    # (design section 7: "brightest highlight kept strictly on the
    # cutting edge")
    parts.append(
        f'<path d="M{cx + v["blade_half"] - 3.2:.1f},{y_guard_top - 4.0:.1f} '
        f'L{cx + v["blade_half"] - 3.2:.1f},{y_tip + 6.0:.1f}" '
        f'stroke="{palette.rgb(palette.shade(blade_color, 0.5))}" '
        f'stroke-width="2.6" fill="none" opacity="0.9"/>'
    )

    # -- surface axis: patina / resin sheen (variants 7-9) --
    if v["patina"] > 0.0:
        parts.append(
            f'<path d="{_path_d(blade_pts)}" fill="'
            f'{palette.rgba(palette.CHARRED_WOOD_BROWN, min(v["patina"], 0.6))}"/>'
        )
    if v["sheen"]:
        parts.append(
            f'<path d="M{cx - v["blade_half"] * 0.3:.1f},{y_guard_top - 20.0:.1f} '
            f'L{cx + v["blade_half"] * 0.15:.1f},{y_tip + 24.0:.1f}" '
            f'stroke="{palette.rgba((255, 255, 255), 0.35)}" '
            f'stroke-width="4.0" fill="none"/>'
        )

    return frame.close_cell_svg([*frame.open_cell_svg(), *parts])


register("kampilan", build)
