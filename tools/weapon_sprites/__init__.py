"""tools/weapon_sprites -- shared package for the weapon sprite atlas.

Holds the geometry (``frame.py``), the colours (``palette.py``), and
the registry every per-role authoring module appends to. See
``docs/plans/2026-08-15-weapon-sprite-design.md`` sections 4, 5, 6.

Author-module contract
-----------------------
Each per-role module (``kampilan.py``, ``wasay.py``, ``kalis.py``,
``itak.py``, ``bangkaw.py``, ``busog.py``, ``arquebus.py``,
``shield.py``) defines exactly one function:

    def build(index: int, box: "frame.ContentBox") -> str:
        '''Return one complete SVG document string for variant
        ``index`` (0 through 9), drawn inside ``box`` (bottom-centre
        of ``box`` is the grip, per ``frame.ContentBox.grip_anchor``;
        weapon points toward ``box.top``).'''

and registers it once, at module import time, with:

    from tools.weapon_sprites import register
    register("kampilan", build)

``role`` must be one of ``frame.ROLE_ROWS``. ``box`` is
``frame.SHIELD_BOX`` for role "shield" and ``frame.WEAPON_BOX`` for
every other role; the caller (``gen_weapon_sprites.py``) supplies it,
a module never selects its own box.
"""

from __future__ import annotations

from typing import Callable

from tools.weapon_sprites.frame import ROLE_ROWS, ContentBox

# One entry per role. A builder takes a 0-based variant index (0-9)
# and the ContentBox to draw inside, and returns one complete SVG
# document string for that single cell.
VariantBuilder = Callable[[int, ContentBox], str]

REGISTRY: dict[str, VariantBuilder] = {}


def register(role: str, builder: VariantBuilder) -> None:
    """Register one role's variant builder. Call once per per-role
    module, at import time. The registry is append-only: an unknown
    role or a second registration for the same role is an error rather
    than a silent overwrite, so two authoring modules can never
    shadow each other by accident."""
    if role not in ROLE_ROWS:
        raise ValueError(f"unknown role {role!r}; must be one of {ROLE_ROWS}")
    if role in REGISTRY:
        raise ValueError(f"role {role!r} is already registered")
    REGISTRY[role] = builder
