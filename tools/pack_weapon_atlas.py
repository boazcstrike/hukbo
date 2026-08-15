"""Pack the weapon sprite atlas's SVG cells into the shipped texture,
and optionally render a review contact sheet.

Run it with:
    uv run python tools/pack_weapon_atlas.py
    uv run python tools/pack_weapon_atlas.py --contact-sheet

Rasterises each of the atlas's 80 cells (8 roles x 10 variants, design
section 4) with ImageMagick's own MSVG renderer -- there is no
rsvg-convert or inkscape on this machine -- and montages them into one
atlas image. A cell with no authored SVG yet (no role registered, or a
role missing a variant) is packed as a fully transparent placeholder
of the same size, so this script is reproducible from a clean checkout
at every stage of authoring: zero roles registered still produces a
correctly shaped, fully transparent atlas. That is what task 1's
"done when" proves; tasks 2 through 9 fill the cells in, and re-running
this script picks them up without any change here.

Toolchain rules, both mandatory on every rasterise call (see
docs/plans/2026-08-15-weapon-sprite.md, "Tooling notes"):
  -background none   -- otherwise the cell comes out on opaque white
  -depth 8            -- this ImageMagick build (7.1.1-45) is Q16 and
                          writes 16-bit PNG otherwise; the atlas must
                          be 8-bit like the existing body atlas
"""

from __future__ import annotations

import argparse
import pathlib
import subprocess
import sys

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from tools.weapon_sprites import frame  # noqa: E402

SVG_DIR = REPO_ROOT / "artifacts" / "weapon-sprites" / "svg"
CELL_DIR = REPO_ROOT / "artifacts" / "weapon-sprites" / "cells"
STAGING_DIR = REPO_ROOT / "artifacts" / "weapon-sprites" / "contact-sheet-staging"
ATLAS_PATH = (
    REPO_ROOT / "src" / "Hukbo.Client" / "Content" / "Textures" / "WeaponSprites.png"
)
CONTACT_SHEET_PATH = REPO_ROOT / "artifacts" / "weapon-sprites" / "contact-sheet.png"

MAGICK = "magick"

# ImageMagick's PNG writer picks the narrowest PNG colour type that
# holds the data -- a fully transparent (or greyscale) source comes
# out as PNG colour type 4 (`graya`), not 6 (`rgba`/`srgba`). The
# shipped atlas must report `srgba` regardless of how much of it is
# painted, matching the existing body atlas, so every write forces
# PNG colour type 6 explicitly.
PNG_RGBA_DEFINE = ["-define", "png:color-type=6"]


def _run(args: list[str]) -> None:
    subprocess.run(args, check=True)


def _identify_size(path: pathlib.Path) -> tuple[int, int]:
    result = subprocess.run(
        [MAGICK, "identify", "-format", "%w %h", str(path)],
        check=True,
        capture_output=True,
        text=True,
    )
    width, height = result.stdout.split()
    return int(width), int(height)


# The inner outline, in authored pixels. Measured rather than guessed:
# the shield row and the pale hafts dissolve into the light theme's
# `arenaSurface` of #E5D4AA, and three widths were rendered at true
# gameplay size and compared. Two is too faint to survive the 5.4x
# downsample, five reads as a drawn frame around the object, three
# separates the silhouette without framing it.
#
# The outline is drawn *inside* the silhouette (ImageMagick's `EdgeIn`)
# rather than dilated outward. An outward outline of this width would
# spill into the four-pixel gutter and sample into the neighbouring
# role's row, which is the one thing the gutter exists to prevent.
OUTLINE_WIDTH = 3
OUTLINE_COLOUR = "#2a2018"


def _trim_box(path: pathlib.Path) -> tuple[int, int, int, int] | None:
    """The tight bounding box of the drawn content, as
    `(left, top, width, height)` in cell pixels, or `None` when the
    cell is empty."""
    result = subprocess.run(
        [MAGICK, str(path), "-trim", "-format", "%w %h %X %Y", "info:"],
        check=True,
        capture_output=True,
        text=True,
    )
    parts = result.stdout.split()
    if len(parts) != 4:
        return None
    width, height = int(parts[0]), int(parts[1])
    if width <= 0 or height <= 0:
        return None
    return int(parts[2].lstrip("+")), int(parts[3].lstrip("+")), width, height


def _normalise_axial_fill(path: pathlib.Path) -> None:
    """Scale one cell's content so it fills the content box along the
    weapon axis, anchored at the grip.

    The renderer scales a cell by `weaponLineLength / contentHeight`, so
    a cell whose art spans only part of its box draws the weapon at that
    same fraction of its true line length -- the visible tip stops short
    of where the simulation resolves contact. The weapon's real length
    is already carried by its line (the Arquebus runs about 9.5 layout
    units against the Kalis and Kampilan's 19.1); encoding it a second
    time in the art halves it on screen.

    Scaling is about the grip point, never the bounding box centre. The
    grip is the one part of a weapon that must stay in the hand, and an
    asymmetric silhouette -- the Wasay's head sits to one side of its
    haft -- would slide out of the fist if the box were re-centred.

    The cost, stated where it is paid: a variant authored as a shorter
    blade is drawn at the same length as a long one, so length is no
    longer a variation axis. It never legitimately was. Reach is fixed
    per role by the simulation, so a visibly shorter weapon with an
    identical reach was misinformation rather than variety.
    """
    box = _trim_box(path)
    if box is None:
        return

    left, top, width, height = box
    anchor_x = frame.CELL_WIDTH / 2.0
    anchor_y = float(frame.GUTTER + frame.WEAPON_BOX.height)

    span = anchor_y - top
    if span <= 0:
        return
    scale = float(frame.WEAPON_BOX.height) / span

    # Never let the widened art leave the content box. A cell that
    # cannot reach a full fill keeps whatever fill its width allows.
    limit_left = frame.GUTTER
    limit_right = frame.CELL_WIDTH - frame.GUTTER
    if left < anchor_x:
        scale = min(scale, (anchor_x - limit_left) / (anchor_x - left))
    right = left + width
    if right > anchor_x:
        scale = min(scale, (limit_right - anchor_x) / (right - anchor_x))

    if abs(scale - 1.0) < 0.005:
        return

    _run(
        [
            MAGICK,
            str(path),
            "-virtual-pixel",
            "none",
            # Triangle, not Lanczos. Lanczos has negative lobes, and the
            # ringing they produce puts faint content outside the
            # silhouette -- measured at up to three pixels past the
            # content box, into the gutter that exists to stop one row
            # sampling into the next.
            "-filter",
            "Triangle",
            "-distort",
            "SRT",
            f"{anchor_x},{anchor_y} {scale:.6f} 0",
            "+repage",
            "-depth",
            "8",
            *PNG_RGBA_DEFINE,
            str(path),
        ]
    )


def _clamp_to_content_box(path: pathlib.Path, box: "frame.ContentBox") -> None:
    """Force every pixel outside the role's content box transparent.

    Resampling and outlining both put faint pixels a little outside the
    silhouette they started from. Rather than trust each step to stay
    inside its box, the invariant is asserted here once: the gutter is
    empty, so no cell can ever sample into the row above or below it.
    """
    left = int(round(box.left))
    top = int(round(box.top))
    width = int(round(box.width))
    height = int(round(box.height))
    mask = path.with_suffix(".clamp-mask.png")
    try:
        _run(
            [
                MAGICK,
                "-size",
                f"{frame.CELL_WIDTH}x{frame.CELL_HEIGHT}",
                "xc:black",
                "-fill",
                "white",
                "-draw",
                f"rectangle {left},{top} {left + width - 1},{top + height - 1}",
                "-alpha",
                "off",
                str(mask),
            ]
        )
        # Multiply the existing alpha by the box mask rather than
        # replacing it -- `copy_opacity` alone would make the whole
        # content box opaque instead of merely clipping to it.
        _run(
            [
                MAGICK,
                str(path),
                "-alpha",
                "extract",
                str(mask),
                "-compose",
                "multiply",
                "-composite",
                str(mask),
            ]
        )
        _run(
            [
                MAGICK,
                str(path),
                str(mask),
                "-alpha",
                "off",
                "-compose",
                "copy_opacity",
                "-composite",
                "-depth",
                "8",
                *PNG_RGBA_DEFINE,
                str(path),
            ]
        )
    finally:
        mask.unlink(missing_ok=True)


def _apply_inner_outline(path: pathlib.Path) -> None:
    """Darken a ring just inside the silhouette's edge.

    Applied here rather than in the eight authoring modules so that one
    change governs all eighty cells and no row can drift out of step
    with the others.
    """
    mask = path.with_suffix(".outline-mask.png")
    layer = path.with_suffix(".outline-layer.png")
    try:
        _run(
            [
                MAGICK,
                str(path),
                "-alpha",
                "extract",
                "-morphology",
                "EdgeIn",
                f"disk:{OUTLINE_WIDTH}",
                str(mask),
            ]
        )
        _run(
            [
                MAGICK,
                "-size",
                f"{frame.CELL_WIDTH}x{frame.CELL_HEIGHT}",
                f"xc:{OUTLINE_COLOUR}",
                str(mask),
                "-alpha",
                "off",
                "-compose",
                "copy_opacity",
                "-composite",
                *PNG_RGBA_DEFINE,
                str(layer),
            ]
        )
        _run(
            [
                MAGICK,
                str(path),
                str(layer),
                "-compose",
                "over",
                "-composite",
                "-depth",
                "8",
                *PNG_RGBA_DEFINE,
                str(path),
            ]
        )
    finally:
        mask.unlink(missing_ok=True)
        layer.unlink(missing_ok=True)


def _rasterise_cell(role: str, index: int, out_path: pathlib.Path) -> None:
    """Rasterise one cell to `out_path`, 8-bit RGBA,
    `frame.CELL_WIDTH` x `frame.CELL_HEIGHT`. Falls back to a fully
    transparent placeholder of the same size when no SVG has been
    authored yet for this role/variant."""
    out_path.parent.mkdir(parents=True, exist_ok=True)
    svg_path = SVG_DIR / role / f"{role}-{index}.svg"
    if svg_path.exists():
        _run(
            [
                MAGICK,
                "-background",
                "none",
                "-depth",
                "8",
                str(svg_path),
                *PNG_RGBA_DEFINE,
                str(out_path),
            ]
        )
    else:
        _run(
            [
                MAGICK,
                "-size",
                f"{frame.CELL_WIDTH}x{frame.CELL_HEIGHT}",
                "-depth",
                "8",
                "xc:none",
                *PNG_RGBA_DEFINE,
                str(out_path),
            ]
        )


def rasterise_all() -> list[pathlib.Path]:
    """Rasterise (or placeholder-fill) every one of the 80 cells.
    Returns the 80 cell paths, in atlas row-major packing order: role
    0..7 (`frame.ROLE_ROWS`), variant 0..9 within each role -- the
    same order `-tile 10x8` fills left to right, top to bottom."""
    paths: list[pathlib.Path] = []
    for role in frame.ROLE_ROWS:
        for index in range(frame.COLUMNS):
            out_path = CELL_DIR / role / f"{role}-{index}.png"
            _rasterise_cell(role, index, out_path)
            if (SVG_DIR / role / f"{role}-{index}.svg").exists():
                # The shield is fitted to `ShieldBounds` rather than to
                # the weapon line, so nothing about its on-screen length
                # depends on how much of its box it fills. Only the seven
                # weapon rows are normalised.
                if role != "shield":
                    _normalise_axial_fill(out_path)
                _apply_inner_outline(out_path)
                _clamp_to_content_box(out_path, frame.content_box_for(role))
            paths.append(out_path)
    return paths


def pack_atlas(cell_paths: list[pathlib.Path], out_path: pathlib.Path) -> None:
    """Montage the 80 cells into the atlas at `out_path`: 8-bit RGBA,
    `frame.ATLAS_WIDTH` x `frame.ATLAS_HEIGHT`."""
    out_path.parent.mkdir(parents=True, exist_ok=True)
    geometry = f"{frame.CELL_WIDTH}x{frame.CELL_HEIGHT}+0+0"
    _run(
        [
            MAGICK,
            "montage",
            *[str(p) for p in cell_paths],
            "-tile",
            f"{frame.COLUMNS}x{frame.ROWS}",
            "-geometry",
            geometry,
            "-background",
            "none",
            "-depth",
            "8",
            *PNG_RGBA_DEFINE,
            str(out_path),
        ]
    )


def build_contact_sheet(cell_paths: list[pathlib.Path], out_path: pathlib.Path) -> None:
    """Render a human review artifact: all 80 cells on a mid-grey
    checkerboard, each labelled `<role>-<variant>`, tiled 10 wide by 8
    tall in atlas row order. Untracked, under `artifacts/`; not part
    of the shipped atlas or the content pipeline.

    Simplification recorded rather than hidden: this does not render
    the per-role "true gameplay size (46px) beside the procedural
    line" strips design task 10a also asks for -- that comparison
    needs the procedural weapon line `PawnRenderer` draws, which is
    C# and outside this SVG/ImageMagick pipeline. The session running
    task 10a should extend this function, not replace it.
    """
    out_path.parent.mkdir(parents=True, exist_ok=True)
    STAGING_DIR.mkdir(parents=True, exist_ok=True)

    label_height = 16
    tile_height = frame.CELL_HEIGHT + label_height
    labelled_paths: list[pathlib.Path] = []
    for role in frame.ROLE_ROWS:
        for index in range(frame.COLUMNS):
            source = CELL_DIR / role / f"{role}-{index}.png"
            labelled = STAGING_DIR / f"{role}-{index}.png"
            _run(
                [
                    MAGICK,
                    str(source),
                    # Force sRGB before anything grey touches the cell.
                    # `gray20`, `gray50`, and `gray70` are all greyscale
                    # colours, and ImageMagick will quietly narrow the
                    # whole pipeline to greyscale the moment one of them
                    # becomes an image's background -- stripping the
                    # colour out of every cell on a sheet whose only
                    # purpose is for somebody to look at the colours.
                    "-colorspace",
                    "sRGB",
                    "-type",
                    "TrueColorAlpha",
                    "-background",
                    "gray20",
                    "-gravity",
                    "south",
                    "-splice",
                    f"0x{label_height}",
                    "-gravity",
                    "south",
                    "-fill",
                    "white",
                    "-pointsize",
                    "11",
                    "-annotate",
                    "+0+2",
                    f"{role}-{index}",
                    "-depth",
                    "8",
                    str(labelled),
                ]
            )
            labelled_paths.append(labelled)

    montage_path = STAGING_DIR.parent / "contact-sheet-montage.png"
    _run(
        [
            MAGICK,
            "montage",
            *[str(p) for p in labelled_paths],
            "-tile",
            f"{frame.COLUMNS}x{frame.ROWS}",
            "-geometry",
            f"{frame.CELL_WIDTH}x{tile_height}+2+2",
            "-background",
            "none",
            "-depth",
            "8",
            str(montage_path),
        ]
    )

    width, height = _identify_size(montage_path)
    checker_path = STAGING_DIR.parent / "contact-sheet-checker.png"
    _run(
        [
            MAGICK,
            "-size",
            f"{width}x{height}",
            "pattern:checkerboard",
            "-fill",
            "gray70",
            "-opaque",
            "white",
            "-fill",
            "gray50",
            "-opaque",
            "black",
            "-depth",
            "8",
            # The checkerboard is built from two greys, and ImageMagick
            # takes the composite's colourspace from the base image. Left
            # as greyscale it silently strips the colour out of all eighty
            # cells, which is exactly what happened the first time this
            # sheet was rendered: the atlas was correct and the review
            # image was monochrome, which is the worst way round for a
            # tool whose only job is to be looked at.
            "-colorspace",
            "sRGB",
            "-type",
            "TrueColor",
            # And force the PNG colour type on the way out, for the same
            # reason the atlas does: the writer picks the narrowest type
            # the pixels allow, so a checkerboard of two greys is written
            # back as greyscale however it was built in memory, and the
            # next composite inherits that.
            "-define",
            "png:color-type=2",
            str(checker_path),
        ]
    )

    _run(
        [
            MAGICK,
            str(checker_path),
            str(montage_path),
            "-gravity",
            "NorthWest",
            "-compose",
            "Over",
            "-composite",
            "-depth",
            "8",
            "-colorspace",
            "sRGB",
            "-type",
            "TrueColor",
            "-define",
            "png:color-type=2",
            str(out_path),
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--contact-sheet",
        action="store_true",
        help=f"also render {CONTACT_SHEET_PATH.relative_to(REPO_ROOT)}",
    )
    args = parser.parse_args()

    cell_paths = rasterise_all()
    pack_atlas(cell_paths, ATLAS_PATH)
    print(f"wrote {ATLAS_PATH.relative_to(REPO_ROOT)}")

    if args.contact_sheet:
        build_contact_sheet(cell_paths, CONTACT_SHEET_PATH)
        print(f"wrote {CONTACT_SHEET_PATH.relative_to(REPO_ROOT)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
