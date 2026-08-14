# UiChrome.png — placeholder nine-slice chrome atlas

Authored 2026-08-14 for task CH-T1 of the UI chrome nine-slice package. Its
design document stays live in `docs/plans/` under the name "UI chrome
nine-slice sprite skin — design"; the plan that executed it has been archived.

**This is placeholder programmer art.** It exists so the primitive, the content
pipeline, the toggle, and the scale behaviour can be proven before anyone
spends time on a visual identity. It makes no historical claim of any kind and
carries no cultural identification, so the evidence-tier rules in `CLAUDE.md`
section 7 do not apply to it; the nearest tier if one were ever recorded would
be `VisualEvidenceTier.PresentationOnly`.

## Canvas and regions

The file is 128 by 64 pixels, RGBA, straight alpha. Two 48 by 48 regions sit on
it, both sliced at a **12 pixel margin**:

| Region | Origin | Size | Contents |
| --- | --- | --- | --- |
| `surface` | (0, 0) | 48 × 48 | Solid opaque white across all nine cells |
| `border` | (64, 0) | 48 × 48 | The frame motif, fully transparent in its interior |

A 12 pixel margin over a 48 pixel cell gives nine cells of 12 × 12 each: four
fixed-size corners, four edges stretched along one axis, and one centre
stretched along both.

The remaining canvas below y = 48 is transparent padding. It is there so the
atlas can grow without moving either region's origin, since a moved origin
changes every source rectangle at once.

## Why two regions rather than one

`DrawPanel` takes a surface tint and a border tint separately, and one
`SpriteBatch.Draw` call can apply only one colour. A single region carrying both
the frame and the fill could not tint them independently.

Two regions support either implementation:

- **Eighteen draws.** Nine slices of `surface` tinted with the surface colour,
  then nine slices of `border` tinted with the border colour.
- **Nine draws.** One stretched quad of the existing one-pixel texture for the
  fill, then the eight outer slices of `border`. The `border` centre cell is
  empty, so skipping it costs nothing.

The nine-draw form is the cheaper one and is the expected implementation. The
atlas deliberately supports both so CH-T4 is not forced into a choice by the
asset.

## Everything is white

Both regions are authored in white with varying alpha. `SpriteBatch.Draw`
multiplies by its `Color` argument, so the draw-time theme colour is the only
thing that decides hue. This is exactly how the one-pixel texture is tinted
today, which is what lets all twenty-seven theme roles keep working without a
per-theme atlas.

## The frame motif

Three alpha values appear in the `border` region, and nothing else: 0, 140, and
255.

| Element | Geometry | Alpha |
| --- | --- | --- |
| Outer line | 2 pixels thick along all four edges | 255 |
| Corner chamfer | A diagonal notch cut from each corner, removing every pixel whose Manhattan distance from the corner is under 4 | 0 |
| Inner accent | A 1 pixel line inset 5 pixels from each edge | 140 |
| Interior | Everything else | 0 |

The chamfer and the accent are there on purpose: a nine-slice that looked
exactly like the flat-rectangle border would make smoke rows `CH-2` and `CH-3`
unfalsifiable, because a tester could not tell the two styles apart.

## The stretch constraint

An edge cell is stretched along one axis, so **it must not vary along that
axis** or stretching will smear it. Corner cells are fixed size and may hold
detail freely, which is why the chamfer lives only in the corners.

The generator asserts this before writing the file: every row of the top and
bottom edge cells must hold exactly one distinct colour across x, and every
column of the left and right edge cells exactly one across y. If a future edit
breaks the rule the generator fails rather than producing a subtly smeared
asset.

## Regenerating it

The atlas is produced by a short Pillow script rather than drawn by hand, so it
is reproducible from the constants above: cell 48, margin 12, canvas 128 × 64,
line thickness 2, chamfer 4, accent inset 5, accent alpha 140, surface origin
(0, 0), border origin (64, 0).

Straight alpha is written here. The MonoGame `TextureProcessor` premultiplies at
build time, so do not premultiply in the source file as well.
