"""Tall hardwood shield -- ten authored variants of row 7
(`frame.ROLE_ROWS[7]`), the shield row (design section 4, section 6).

Historical constraint (CLAUDE.md section 7, design section 6,
`ShieldVisualCatalog.cs`): four already-shipped, evidence-recorded
skins for `PawnShieldRole.TallHardwood`, and this row's ten cells span
those four rather than invent a fifth:

- MactanThin (ShieldVisualCatalog.cs:130-145) -- Pigafetta, Mactan
  1521. Documented active use with evasive footwork; the wood is
  described as THIN and the catalog's own comment admits "hardwood"
  slightly overstates that. Drawn as thin, numerous planks, never a
  thick slab.
- MorgaFullBody (ShieldVisualCatalog.cs:154-171) -- Manila 1609.
  Documented, form uncertain: light wood, head-to-foot coverage,
  inside-armhole fastening -- a back-of-shield fact this front-facing
  silhouette cannot show and does not attempt to.
- BoxerCagayan (ShieldVisualCatalog.cs:184-201) -- Boxer Codex,
  Manila c.1590-95. Documented as a late-century VISUAL record;
  Documented, form uncertain as construction evidence. The Codex
  guides silhouette and colour ONLY (CLAUDE.md section 7,
  HISTORICAL_1500s_WEAPONS.md) -- a gentle top/bottom edge curvature
  and the kept vertical seam are a colour/silhouette reading, never a
  claim of construction detail the Codex did not document.
- VisayanKalasag (ShieldVisualCatalog.cs:216-234) -- synthesis,
  Alcina 1668 / Scott 1994. Documented, form uncertain: long narrow
  body, light fibrous wood, rattan strengthening, resin coating. The
  *kalasag* name is a PROVISIONAL attachment pending vocabulary
  verification and never appears in the player-facing label -- it is
  a code-comment convenience here exactly as it is in the catalog.

BINDING: "a skin may only ever change how the block looks, never what
it covers" (ShieldVisualCatalog.cs:11-14). Every one of the ten cells
below fills the same content box -- no variant is taller, wider, or a
different outline class. Curvature and seam treatment vary; the
covered rectangle does not.

Variation stays inside the four attested envelopes (design section 7):
face tone across the documented palette, plank count and seam
placement, rattan strengthening bands and lashing pattern, resin
coating sheen, boss/grip treatment, edge binding, and battle damage or
weathering. No variant adds a painted device, heraldry, or ornament
the sources do not support.

Coordinate convention: every point below is `(u, v)` in the SHIELD
content box's own local space, `u` in `[0, box.width]` and `v` in
`[0, box.height]`, `v` growing downward. `_pt` maps a local point
through `box.left`/`box.top` into the cell's absolute SVG coordinates.
Every drawing helper below works in `box.width`/`box.height` rather
than a hardcoded 90/248 -- this row's box is `frame.SHIELD_BOX`
(90x248), NOT the 104-wide `frame.WEAPON_BOX` every other role draws
into, and the caller supplies whichever box is correct. Using the
box's own dimensions rather than a literal makes a wrong box visible
immediately rather than silently letterboxed.
"""

from __future__ import annotations

from tools.weapon_sprites import register
from tools.weapon_sprites.frame import ContentBox, close_cell_svg, open_cell_svg
from tools.weapon_sprites.palette import (
    CHARRED_WOOD_BROWN,
    LIGHT_HARDWOOD_TAN,
    PALM_WOOD_PALE,
    RATTAN_LASHING_TONE,
    RESIN_BROWN_TONE,
    RGB,
    rgb,
    rgba,
    shade,
)

Point = tuple[float, float]


class _Variant:
    """One cell's authored parameters. `skin` names which of the four
    documented `ShieldVisualCatalog` entries this cell derives from,
    for the module's own traceability; it is not read by the drawing
    code. `plank_count` of 1 draws no seam at all (the VisayanKalasag
    reading, whose bold horizontal accent band replaces the vertical
    seam per the catalog's own note)."""

    def __init__(
        self,
        skin: str,
        face_tone: RGB,
        plank_count: int,
        lashing_v: tuple[float, ...],
        accent_band: bool,
        curved: bool,
        resin_sheen: bool,
        boss: bool,
        wear: str,
        note: str,
    ) -> None:
        self.skin = skin
        self.face_tone = face_tone
        self.plank_count = plank_count
        self.lashing_v = lashing_v
        self.accent_band = accent_band
        self.curved = curved
        self.resin_sheen = resin_sheen
        self.boss = boss
        self.wear = wear
        self.note = note


# One entry per variant 0-9, three or two per documented skin. Within
# each skin's group the first cell is the outline baseline and the
# rest are tail slots that reuse its exact plank/lashing layout and
# vary tint, wear, or damage only -- design section 7's "pure
# recolour is permitted only in the tail slots" rule.
_VARIANTS: tuple[_Variant, ...] = (
    _Variant(  # 0 -- MactanThin baseline
        "MactanThin", PALM_WOOD_PALE, 6, (58,), False, False, False, False, "none",
        "MactanThin baseline: pale palm-wood tone, six thin planks (numerous "
        "narrow seams standing in for Pigafetta's thin wood, never a thick "
        "slab), single lashing band, straight outline, no accent, no boss.",
    ),
    _Variant(  # 1 -- MactanThin, thinner-reading planking, grip boss
        "MactanThin", PALM_WOOD_PALE, 8, (54, 176), False, False, False, True, "none",
        "MactanThin: even thinner-reading planking (eight planks), two "
        "lashing bands, grip boss shown, straight outline.",
    ),
    _Variant(  # 2 -- MactanThin tail: recolour/wear only
        "MactanThin", PALM_WOOD_PALE, 6, (58,), False, False, False, False, "chipped",
        "MactanThin tail slot: baseline planking and lashing reused; one "
        "edge chip.",
    ),
    _Variant(  # 3 -- MorgaFullBody baseline
        "MorgaFullBody", LIGHT_HARDWOOD_TAN, 4, (46, 200), False, False, False, True, "none",
        "MorgaFullBody: mid light-wood tone, four broader planks, lashing "
        "near both ends, grip boss shown, straight outline (the documented "
        "head-to-foot coverage is a size fact this fixed content box cannot "
        "show and does not attempt to).",
    ),
    _Variant(  # 4 -- MorgaFullBody tail: heaviest weathering
        "MorgaFullBody", LIGHT_HARDWOOD_TAN, 4, (46, 200), False, False, False, True, "heavy",
        "MorgaFullBody tail slot: same planking and lashing reused; heaviest "
        "weathering -- faded grain streaks, dulled boss.",
    ),
    _Variant(  # 5 -- BoxerCagayan baseline
        "BoxerCagayan", CHARRED_WOOD_BROWN, 2, (40, 120, 210), False, True, False, False, "none",
        "BoxerCagayan: existing charred-wood tone, gentle top/bottom edge "
        "curvature, the kept central vertical seam -- Codex silhouette-and-"
        "colour guidance only, no construction claim -- three lashing bands.",
    ),
    _Variant(  # 6 -- BoxerCagayan tail: grain split
        "BoxerCagayan", CHARRED_WOOD_BROWN, 2, (40, 120, 210), False, True, False, False, "split",
        "BoxerCagayan tail slot: same curvature and central seam reused; a "
        "split along the grain.",
    ),
    _Variant(  # 7 -- VisayanKalasag baseline
        "VisayanKalasag", RESIN_BROWN_TONE, 1, (36, 96, 156, 216), True, False, True, True, "none",
        "VisayanKalasag: resin-brown tone, no plank seam (the bold "
        "horizontal rattan-binding accent replaces it, per the catalog), "
        "four rattan strengthening bands, resin sheen, grip boss.",
    ),
    _Variant(  # 8 -- VisayanKalasag tail: arrow strike
        "VisayanKalasag", RESIN_BROWN_TONE, 1, (36, 96, 156, 216), True, False, True, True, "arrow",
        "VisayanKalasag tail slot: same rattan pattern and sheen reused; an "
        "arrow-strike mark.",
    ),
    _Variant(  # 9 -- VisayanKalasag tail: heaviest wear
        "VisayanKalasag", RESIN_BROWN_TONE, 1, (36, 96, 156, 216), True, False, True, True, "heavy",
        "VisayanKalasag tail slot: same rattan pattern reused; faded sheen "
        "and lashing, heaviest weathering.",
    ),
)


def _pt(box: ContentBox, u: float, v: float) -> Point:
    return (box.left + u, box.top + v)


def _poly_attr(points: tuple[Point, ...], box: ContentBox) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in (_pt(box, u, v) for u, v in points))


def _polygon(points: tuple[Point, ...], box: ContentBox, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{opacity}"' if opacity < 1.0 else ""
    return f'<polygon points="{_poly_attr(points, box)}" fill="{fill}"{op} />'


def _face_outline(box: ContentBox, curved: bool) -> tuple[Point, ...]:
    """The face silhouette in local space: a straight rectangle
    covering the whole box, or, for the BoxerCagayan reading, one with
    a gentle inward bow on the top and bottom edges
    (ShieldVisualCatalog.cs:180-183 -- the Codex's own note is
    silhouette/colour guidance only, so the bow stays modest rather
    than a construction claim). Either way the covered rectangle --
    the full box -- is identical; only the drawn edge line bows."""
    w, h = box.width, box.height
    if not curved:
        return ((0, 0), (w, 0), (w, h), (0, h))
    bow = h * 0.018  # modest inward bow -- "gentle curvature" per the catalog note
    return (
        (0, 0), (w * 0.5, bow), (w, 0),
        (w, h), (w * 0.5, h - bow), (0, h),
    )


def _draw_face(box: ContentBox, v: _Variant) -> list[str]:
    """Base fill plus a left-shadow / right-highlight value band --
    one consistent light source across the row, per the craft
    guidance's base/shadow/edge-highlight structure."""
    outline = _face_outline(box, v.curved)
    base = rgb(v.face_tone)
    shadow = rgba(shade(v.face_tone, -0.30), 0.45)
    highlight = rgba(shade(v.face_tone, 0.30), 0.35)
    w, h = box.width, box.height
    lines = [_polygon(outline, box, base)]
    shadow_pts = ((0, 0), (w * 0.34, 0), (w * 0.34, h), (0, h))
    highlight_pts = ((w * 0.68, 0), (w, 0), (w, h), (w * 0.68, h))
    lines.append(_polygon(shadow_pts, box, shadow))
    lines.append(_polygon(highlight_pts, box, highlight))
    return lines


def _draw_edge_binding(box: ContentBox, v: _Variant) -> list[str]:
    tone = rgb(shade(v.face_tone, -0.5))
    x0, y0 = _pt(box, 1.5, 1.5)
    return [
        f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{box.width - 3:.1f}" '
        f'height="{box.height - 3:.1f}" fill="none" stroke="{tone}" '
        f'stroke-width="3" stroke-opacity="0.8" />'
    ]


def _draw_planks(box: ContentBox, v: _Variant) -> list[str]:
    """Vertical plank seams -- the row's primary readable structure at
    46px (design section 7). `plank_count` of 1 draws none."""
    if v.plank_count <= 1:
        return []
    tone = rgba(shade(v.face_tone, -0.55), 0.7)
    lines = []
    for i in range(1, v.plank_count):
        u = box.width * i / v.plank_count
        x0, y0 = _pt(box, u, 4)
        x1, y1 = _pt(box, u, box.height - 4)
        lines.append(
            f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" '
            f'stroke="{tone}" stroke-width="1.4" />'
        )
    return lines


def _draw_lashing(box: ContentBox, v: _Variant) -> list[str]:
    tone = rgb(RATTAN_LASHING_TONE)
    opacity = 0.55 if v.wear == "heavy" else 1.0
    lines = []
    for band_v in v.lashing_v:
        x0, y0 = _pt(box, 3, band_v - 3.5)
        lines.append(
            f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{box.width - 6:.1f}" '
            f'height="7" fill="{tone}" fill-opacity="{opacity}" rx="1.5" />'
        )
    return lines


def _draw_accent_band(box: ContentBox, v: _Variant) -> list[str]:
    """The bold horizontal rattan-binding accent that replaces the
    vertical seam on the VisayanKalasag reading
    (ShieldVisualCatalog.cs:207-209, 228-230)."""
    if not v.accent_band:
        return []
    tone = rgb(shade(RATTAN_LASHING_TONE, -0.15))
    band_v = box.height * 0.44
    x0, y0 = _pt(box, 2, band_v - 6)
    return [
        f'<rect x="{x0:.1f}" y="{y0:.1f}" width="{box.width - 4:.1f}" '
        f'height="12" fill="{tone}" rx="2" />'
    ]


def _draw_resin_sheen(box: ContentBox, v: _Variant) -> list[str]:
    if not v.resin_sheen:
        return []
    opacity = 0.10 if v.wear == "heavy" else 0.22
    tone = rgba((255, 250, 235), opacity)
    w, h = box.width, box.height
    pts = ((w * 0.08, h * 0.06), (w * 0.42, h * 0.06), (w * 0.22, h * 0.5), (w * 0.02, h * 0.4))
    return [_polygon(pts, box, tone)]


def _draw_boss(box: ContentBox, v: _Variant) -> list[str]:
    if not v.boss:
        return []
    cx, cy = _pt(box, box.width / 2, box.height / 2)
    base = rgb(shade(v.face_tone, -0.4))
    highlight = rgba(shade(v.face_tone, 0.35), 0.6)
    r = box.width * 0.09
    return [
        f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{r:.1f}" fill="{base}" />',
        f'<circle cx="{cx - r * 0.25:.1f}" cy="{cy - r * 0.25:.1f}" '
        f'r="{r * 0.4:.1f}" fill="{highlight}" />',
    ]


def _draw_damage(box: ContentBox, v: _Variant) -> list[str]:
    lines: list[str] = []
    w, h = box.width, box.height
    if v.wear == "chipped":
        chip_tone = rgba(shade(v.face_tone, -0.5), 0.85)
        lines.append(
            _polygon(((0, h * 0.28), (7, h * 0.30), (2, h * 0.36)), box, chip_tone)
        )
    if v.wear == "split":
        crack = rgba(shade(v.face_tone, -0.55), 0.75)
        x0, y0 = _pt(box, w * 0.62, h * 0.20)
        x1, y1 = _pt(box, w * 0.62 + 3, h * 0.62)
        lines.append(
            f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" '
            f'stroke="{crack}" stroke-width="1.6" />'
        )
    if v.wear == "arrow":
        cu, cv = w * 0.32, h * 0.34
        cx, cy = _pt(box, cu, cv)
        dark = rgba((30, 24, 18), 0.8)
        lines.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="2.6" fill="{dark}" />')
        crack = rgba(shade(v.face_tone, -0.5), 0.6)
        for du, dv in ((6, -3), (-5, 4), (4, 6)):
            x1, y1 = _pt(box, cu + du, cv + dv)
            lines.append(
                f'<line x1="{cx:.1f}" y1="{cy:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" '
                f'stroke="{crack}" stroke-width="1" />'
            )
    if v.wear == "heavy":
        streak = rgba((235, 225, 205), 0.12)
        for i in range(4):
            band_v = h * (0.15 + i * 0.2)
            x0, y0 = _pt(box, 4, band_v)
            x1, y1 = _pt(box, w - 4, band_v)
            lines.append(
                f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" '
                f'stroke="{streak}" stroke-width="2.4" />'
            )
    return lines


def build(index: int, box: ContentBox) -> str:
    """Return one complete SVG document string for shield variant
    `index` (0-9), drawn inside `box` -- always `frame.SHIELD_BOX`
    (90x248) for this role; the caller supplies it, this module never
    picks its own box."""
    if not 0 <= index <= 9:
        raise ValueError(f"variant index out of range: {index!r}")
    v = _VARIANTS[index]

    lines: list[str] = open_cell_svg()
    lines += _draw_face(box, v)
    lines += _draw_planks(box, v)
    lines += _draw_lashing(box, v)
    lines += _draw_accent_band(box, v)
    lines += _draw_resin_sheen(box, v)
    lines += _draw_boss(box, v)
    lines += _draw_damage(box, v)
    lines += _draw_edge_binding(box, v)
    return close_cell_svg(lines)


register("shield", build)
