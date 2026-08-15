"""Generate the weapon sprite atlas's authored cells as SVG files.

Run it with:
    uv run python tools/gen_weapon_sprites.py

For every role registered in ``tools.weapon_sprites.REGISTRY`` (see
that package's docstring for the author-module contract), writes ten
SVG files -- one per variant -- under
``artifacts/weapon-sprites/svg/<role>/<role>-<index>.svg``.

A role with no authoring module yet is silently absent from the
registry, not an error: this script imports each of the eight
possible per-role modules for its registration side effect and skips
any that do not exist. With none of them written yet, this scaffold
runs and emits nothing -- that is the task 1 "done when" condition,
not a bug to fix later. ``tools/pack_weapon_atlas.py`` reads whatever
this script wrote (nothing, if no roles are registered) and packs it
into the atlas.

Never runs during build, test, or the canonical gate -- an authoring
tool only, like ``tools/gen_pawn_bodies.py`` on the sibling package's
branch.
"""

from __future__ import annotations

import importlib
import pathlib
import sys

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from tools.weapon_sprites import REGISTRY  # noqa: E402
from tools.weapon_sprites import frame  # noqa: E402

OUTPUT_DIR = REPO_ROOT / "artifacts" / "weapon-sprites" / "svg"

# Every possible per-role module, imported for its registration side
# effect only. Importing a module that does not exist yet is expected,
# not an error -- that role simply is not written this run.
_ROLE_MODULE_NAMES = tuple(
    f"tools.weapon_sprites.{role}" for role in frame.ROLE_ROWS
)


def _import_role_modules() -> None:
    for name in _ROLE_MODULE_NAMES:
        try:
            importlib.import_module(name)
        except ModuleNotFoundError:
            continue


def generate() -> list[pathlib.Path]:
    """Write every registered role's ten SVG cells. Returns the paths
    written, in role-row then variant order."""
    _import_role_modules()
    written: list[pathlib.Path] = []
    for role in frame.ROLE_ROWS:
        builder = REGISTRY.get(role)
        if builder is None:
            continue
        box = frame.content_box_for(role)
        role_dir = OUTPUT_DIR / role
        role_dir.mkdir(parents=True, exist_ok=True)
        for index in range(frame.COLUMNS):
            svg = builder(index, box)
            path = role_dir / f"{role}-{index}.svg"
            path.write_text(svg, encoding="utf-8")
            written.append(path)
    return written


def main() -> int:
    paths = generate()
    print(f"wrote {len(paths)} cell(s) under {OUTPUT_DIR}")
    for path in paths:
        print(f"  {path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
