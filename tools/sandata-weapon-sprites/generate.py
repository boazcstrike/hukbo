"""Generate Sandata's top-down weapon sprites.

Sandata draws untextured primitives everywhere else: a single 1x1 white pixel
stretched into rectangles, tinted by a theme role. These two sprites are the
first real textures in the client, and they follow the same rule so that they
do not fight the theme system. Each pixel is written as a greyscale value with
an alpha channel and nothing else -- no hue at all -- so that drawing the
sprite with a theme colour as the tint reproduces exactly the colour the
primitive weapon bar used to be, with internal shading preserved as
lighter and darker steps of that same colour.

The sprites point along +X, muzzle to the right, and the pivot the renderer
rotates about is the grip anchor recorded in ``GRIP_ANCHORS`` below. Those two
facts are the contract between this file and
``src/Sandata.Client/Rendering/OperatorRenderer.cs``; changing either here
without changing it there will rotate a weapon about the wrong point.

Run it with:

    uv run --with pillow python tools/sandata-weapon-sprites/generate.py

It writes into ``src/Sandata.Client/Content/Sprites/`` and prints what it
wrote. It is an authoring tool: it never runs during a build, a test, or the
canonical gate, and the generated PNG files are committed alongside it so that
a checkout does not need Python to run the game.
"""

from __future__ import annotations

import pathlib
import sys

from PIL import Image

# Greyscale steps. The renderer multiplies these by a theme colour, so 255 is
# the theme colour at full strength and everything below it is a shaded step of
# the same colour rather than a different colour.
BODY = 255  # receiver, slide -- the mass that reads as "weapon"
EDGE = 190  # barrel, stock, grip -- structure that should read slightly back
DETAIL = 140  # magazine, trigger guard -- the parts that only matter up close

# Pixel origin the renderer rotates the sprite about, in sprite pixel space.
GRIP_ANCHORS = {
    "weapon-rifle": (10, 7),
    "weapon-pistol": (4, 6),
}


def _draw(image: Image.Image, boxes: list[tuple[int, int, int, int, int]]) -> None:
    """Fill axis-aligned boxes, later boxes painting over earlier ones.

    Each box is ``(left, top, right, bottom, value)`` with ``right`` and
    ``bottom`` inclusive, which is how the layouts below read most naturally at
    this size.
    """
    pixels = image.load()
    if pixels is None:  # pragma: no cover - Pillow always returns an accessor
        raise RuntimeError("Pillow returned no pixel accessor")
    for left, top, right, bottom, value in boxes:
        for x in range(left, right + 1):
            for y in range(top, bottom + 1):
                pixels[x, y] = (value, 255)


def build_rifle() -> Image.Image:
    """An AK-pattern rifle seen from directly above, 32x14, muzzle at +X.

    The silhouette that has to survive being three pixels tall on screen is:
    long thin barrel forward, a deep receiver in the middle, a magazine
    hanging off one side, and a stock trailing behind the grip. That
    combination is what separates it from the pistol at any zoom.
    """
    image = Image.new("LA", (32, 14), (0, 0))
    _draw(
        image,
        [
            (0, 5, 7, 9, EDGE),  # stock
            (7, 5, 17, 9, BODY),  # receiver and dust cover
            (17, 5, 22, 9, EDGE),  # handguard
            (22, 6, 31, 8, EDGE),  # barrel
            (21, 4, 23, 10, DETAIL),  # gas block and front sight
            # The magazine is drawn as three stepped boxes rather than one, so
            # that the AK's forward curve survives even when the whole sprite
            # is a dozen pixels wide on screen. It is the single most
            # recognisable part of this silhouette.
            (12, 10, 16, 11, DETAIL),
            (13, 11, 17, 12, DETAIL),
            (15, 12, 18, 13, DETAIL),
            (8, 10, 11, 12, EDGE),  # pistol grip
        ],
    )
    return image


def build_pistol() -> Image.Image:
    """A Glock-pattern pistol seen from directly above, 14x10, muzzle at +X.

    Half the rifle's length, no stock, no magazine below the frame, and a
    squat blocky slide. At the zoom levels a spectator actually uses, the
    length difference alone is the readable cue.
    """
    image = Image.new("LA", (16, 10), (0, 0))
    _draw(
        image,
        [
            (2, 4, 13, 7, BODY),  # slide
            (13, 5, 15, 6, EDGE),  # muzzle
            (2, 7, 6, 9, EDGE),  # frame and grip
            (6, 7, 8, 8, DETAIL),  # trigger guard
        ],
    )
    return image


def main() -> int:
    repository_root = pathlib.Path(__file__).resolve().parents[2]
    output_directory = repository_root / "src" / "Sandata.Client" / "Content" / "Sprites"
    output_directory.mkdir(parents=True, exist_ok=True)

    for name, image in (
        ("weapon-rifle", build_rifle()),
        ("weapon-pistol", build_pistol()),
    ):
        destination = output_directory / f"{name}.png"
        image.save(destination, format="PNG", optimize=True)
        grip = GRIP_ANCHORS[name]
        print(
            f"wrote {destination.relative_to(repository_root)} "
            f"({image.width}x{image.height}, grip anchor {grip[0]},{grip[1]})"
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
