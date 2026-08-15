"""Shared colours for the weapon sprite atlas.

Every named tone below is copied verbatim from the tints
``WeaponVisualCatalog.cs`` and ``ShieldVisualCatalog.cs`` already
resolve for these roles, so an authored cell reads as the same object
the procedural draw already tints. A genuinely new tone belongs in one
of those catalog files first; this module only mirrors what already
shipped, it does not invent colour.

Toolchain trap, repeated because it has already cost a session: this
machine's ImageMagick has no rsvg-convert delegate, so rasterisation
falls to ImageMagick's own internal MSVG renderer, and MSVG reads an
8-digit hex colour (``#RRGGBBAA``) as fully OPAQUE and silently drops
the alpha byte -- corrupting the RGB channels too, not just the alpha.
It does not error; the failure is an opaque navy block where a soft
shadow should be. Never write an 8-digit hex literal into an SVG this
package emits. Use ``rgba()`` below, or the SVG ``fill-opacity`` /
``stroke-opacity`` attributes, instead.
"""

from __future__ import annotations

RGB = tuple[int, int, int]

# -- src/Hukbo.Client/Presentation/Catalogs/WeaponVisualCatalog.cs --
GRIP_WARM_OCHRE: RGB = (204, 150, 90)
CHARRED_WOOD_BROWN: RGB = (48, 40, 33)
IRON_WORN_GREY: RGB = (135, 138, 132)
PALM_RATTAN_OCHRE: RGB = (168, 122, 66)
RATTAN_LASHING_TONE: RGB = (214, 168, 84)

# -- src/Hukbo.Client/Presentation/Catalogs/ShieldVisualCatalog.cs --
PALM_WOOD_PALE: RGB = (222, 178, 108)
LIGHT_HARDWOOD_TAN: RGB = (188, 146, 88)
RESIN_BROWN_TONE: RGB = (175, 125, 65)


def rgb(colour: RGB) -> str:
    """SVG ``rgb()`` function string for an opaque fill or stroke."""
    r, g, b = colour
    return f"rgb({r},{g},{b})"


def rgba(colour: RGB, alpha: float) -> str:
    """SVG ``rgba()`` function string for a translucent fill or
    stroke. Use this, never an 8-digit hex literal -- see module
    docstring. ``alpha`` is in [0.0, 1.0]."""
    if not 0.0 <= alpha <= 1.0:
        raise ValueError(f"alpha out of range: {alpha!r}")
    r, g, b = colour
    return f"rgba({r},{g},{b},{alpha})"


def shade(colour: RGB, amount: float) -> RGB:
    """``colour`` moved toward black (``amount`` < 0) or white
    (``amount`` > 0) by ``abs(amount)`` in [0, 1]. Use this to build
    the base/shadow/edge-highlight value bands design section 7 asks
    for, e.g. ``shade(IRON_WORN_GREY, -0.35)`` for a blade's shadow
    band and ``shade(IRON_WORN_GREY, 0.35)`` for its edge highlight."""
    if not -1.0 <= amount <= 1.0:
        raise ValueError(f"amount out of range: {amount!r}")
    target = 0 if amount < 0 else 255
    fraction = abs(amount)
    r, g, b = colour
    return (
        round(r + (target - r) * fraction),
        round(g + (target - g) * fraction),
        round(b + (target - b) * fraction),
    )
