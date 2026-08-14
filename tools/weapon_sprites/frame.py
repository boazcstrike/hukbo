"""Cell geometry for the weapon sprite atlas -- single source of truth.

Every per-role authoring module (kampilan.py, wasay.py, ...) draws
inside the ``ContentBox`` this file hands back, in the coordinate
space of one cell's own SVG viewBox. No module computes atlas-wide
pixel coordinates; the packer places whole cells later.

Authoring orientation is pinned here and nowhere else, per design
section 4: every weapon is drawn pointing UP. The grip sits at
``ContentBox.grip_anchor`` -- bottom-centre of the box -- and the tip
runs toward the box's top edge. The shield is authored upright.
"""

from __future__ import annotations

from dataclasses import dataclass

CELL_WIDTH = 112
CELL_HEIGHT = 256
COLUMNS = 10  # one column per variant
ROWS = 8  # one row per role (seven weapons + shield)
GUTTER = 4  # transparent margin on every side of a cell, all sides

ATLAS_WIDTH = CELL_WIDTH * COLUMNS  # 1120
ATLAS_HEIGHT = CELL_HEIGHT * ROWS  # 2048

WEAPON_CONTENT_WIDTH = CELL_WIDTH - 2 * GUTTER  # 104
WEAPON_CONTENT_HEIGHT = CELL_HEIGHT - 2 * GUTTER  # 248

# The shield's true proportion (4:11) is narrower than the weapon
# content box, so row 7 gets its own centred sub-rectangle rather than
# stretching the shield to fill a weapon-shaped box. Design section 4.
SHIELD_CONTENT_WIDTH = 90
SHIELD_CONTENT_HEIGHT = CELL_HEIGHT - 2 * GUTTER  # 248

# Row order is binding -- design section 4's table and plan row order.
# Index into this tuple is the atlas row.
ROLE_ROWS: tuple[str, ...] = (
    "kampilan",
    "wasay",
    "kalis",
    "itak",
    "bangkaw",
    "busog",
    "arquebus",
    "shield",
)


@dataclass(frozen=True)
class ContentBox:
    """A role row's drawable rectangle, in one cell's own viewBox
    space (top-left origin, Y grows downward, same units as the SVG's
    ``width``/``height``/``viewBox``)."""

    left: float
    top: float
    width: float
    height: float

    @property
    def right(self) -> float:
        return self.left + self.width

    @property
    def bottom(self) -> float:
        return self.top + self.height

    @property
    def center_x(self) -> float:
        return self.left + self.width / 2

    @property
    def grip_anchor(self) -> tuple[float, float]:
        """Bottom-centre of the box. Every weapon's grip sits here;
        the tip runs toward ``top``. Design section 4."""
        return (self.center_x, self.bottom)


WEAPON_BOX = ContentBox(
    left=GUTTER,
    top=GUTTER,
    width=WEAPON_CONTENT_WIDTH,
    height=WEAPON_CONTENT_HEIGHT,
)

SHIELD_BOX = ContentBox(
    left=(CELL_WIDTH - SHIELD_CONTENT_WIDTH) / 2,
    top=GUTTER,
    width=SHIELD_CONTENT_WIDTH,
    height=SHIELD_CONTENT_HEIGHT,
)


def content_box_for(role: str) -> ContentBox:
    """The ``ContentBox`` a given role draws inside: ``SHIELD_BOX`` for
    role "shield", ``WEAPON_BOX`` for every other role in ``ROLE_ROWS``."""
    if role not in ROLE_ROWS:
        raise ValueError(f"unknown role {role!r}; must be one of {ROLE_ROWS}")
    return SHIELD_BOX if role == "shield" else WEAPON_BOX


def open_cell_svg() -> list[str]:
    """Start an SVG document for one cell. viewBox matches the cell
    exactly (``0 0 CELL_WIDTH CELL_HEIGHT``), so a builder's own
    ``ContentBox`` coordinates are usable directly as SVG coordinates.
    Caller appends body elements, then calls ``close_cell_svg``."""
    return [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<svg xmlns="http://www.w3.org/2000/svg" '
        f'width="{CELL_WIDTH}" height="{CELL_HEIGHT}" '
        f'viewBox="0 0 {CELL_WIDTH} {CELL_HEIGHT}">',
    ]


def close_cell_svg(lines: list[str]) -> str:
    """Finish an SVG document started by ``open_cell_svg`` and return
    the complete document as one string, ready to write to disk."""
    lines = [*lines, "</svg>"]
    return "\n".join(lines)
