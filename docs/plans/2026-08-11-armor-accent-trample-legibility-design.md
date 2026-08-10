# Armor bulk, adornment accents, and trample legibility — design

Date: 2026-08-11. Scope: `Hukbo.Client` presentation only. No simulation type,
no tick stage, and no hash is touched by anything in this document.

## 1. Why this exists

The full-package visual smoke session (`VIS-043`) was run by a person at an
interactive Windows desktop on 2026-08-11. Every row passed except three, and
the tester's report on the two tasks behind them was the same word both times:
the effect is not clear.

| Task | Row | What the row asks for | What the tester saw |
| --- | --- | --- | --- |
| `VIS-023` | 128 | Armored figures read as visibly bulkier through the torso, and do not read as if carrying a shield | Not clear |
| `VIS-023` | 129 | Adornment accents are visible at maximum zoom without breaking any read | Not clear |
| `VIS-028` | 131 | Trampled areas visibly thin where fighting happened | Not clear |

Each of the three has a specific, locatable cause in the rendering code. None
of them is a matter of taste, and none of them needs a new visual concept — in
all three cases the feature was built and then drawn in a way that cannot
carry the read the row asks for.

## 2. Cause 1 — armor draws as a recolour, not as bulk

`PawnGeometry.CreateArmor` returns the torso capsule widened by the worn armor
option's `ArmorWidthFactor`, which `AppearanceComponentCatalog` bounds to
`MaxArmorWidthFactor = 1.18` (R-W3.6, "Armor capsule widening is bounded
inside the existing build-multiplier envelope"). `PawnRenderer.DrawArmor` then
fills that whole rectangle solid in `DyePalette.BarkBrown`.

Two consequences, both of which the tester's report is consistent with.

First, the widening itself is invisible at the station the row names. Torso
width is `7 × BuildMultiplier × apparentScale` pixels, and `apparentScale` is
clamped to `[0.72, 2.40]`. At the default-fit station a torso is roughly six
pixels across, so `1.18` buys one pixel of total width — half a pixel per
flank, which rounds away on one side entirely. At maximum zoom a seventeen
pixel torso gains three pixels, which is real but is swamped by the second
consequence.

Second, the solid fill covers the torso. An armored pawn therefore loses its
dye colour, its capsule outline, and its High-tier belt accent, and gains a
flat brown block occupying the whole body. That is not a bulkier warrior; it is
a differently coloured one, and a flat block over the torso is exactly the
silhouette a held shield draws. The row's own failure clause — *does not read
as if it were carrying a shield* — describes what the current draw produces.

**Remedy.** Keep `MaxArmorWidthFactor` at `1.18`; the ceiling is not the
problem and raising it would trade one design constraint for a readability
gain the draw change delivers on its own. Instead draw armor as two flank
bars rather than one slab: the material tone fills the margin outside the
torso plus a shell of the torso's own flanks, on both sides, leaving the
torso's dye, outline, and belt visible down the middle. Bulk is then carried
by two symmetric thickenings of the body rather than by a colour swap, and the
result cannot be confused with a shield, which is single-sided and offset from
the body by construction.

The bar width is `max(round(apparentScale), widening)`, where `widening` is the
whole-pixel difference between the armored and unarmored capsule widths. The
first term keeps a bar from rounding to nothing at the apparent-scale floor;
the second guarantees each bar covers its whole margin outside the torso and
still laps onto the torso's own flank, so the thickening reads as part of the
body rather than as a detached outline. It is a
draw-time thickening of an already-widened capsule, not a further widening:
`PawnLayout.ArmorBounds` is unchanged, so `ConservativePawnCull`'s visual
bounds and every cull invariant resting on them are unaffected.

Quad cost per armored pawn goes from one to two. `PawnQuadCount.CountArmor`
mirrors it, as that file requires of every renderer conditional.

## 3. Cause 2 — the accent cap never scales

`PawnGeometry.CreateAdornmentAccents` sizes an accent mark as

```
min(MaxAccentPixelSizeAtApparentScale1, round(MaxAccentPixelSizeAtApparentScale1 × apparentScale))
```

with `MaxAccentPixelSizeAtApparentScale1 = 2`. Because the constant appears on
both sides of the `min`, the second term can never win: the mark is two pixels
at every apparent scale from the `0.72` floor to the `2.40` ceiling. Row 129
asks a person to observe accents at the maximum-zoom station, and at that
station they are still two pixels — under half a percent of the pawn's drawn
height. `PawnGeometryTests.Create_AdornmentAccentRectanglesNeverExceedTheNamedPixelSizeCap`
pins that behaviour and its doc comment states the reading explicitly: "a hard
ceiling this layer never draws past, regardless of how far apparent scale
climbs above 1."

**This is a design decision, not a bug fix, and it is taken here deliberately.**
R-W3.6's own text is *"at apparent scale 1, an accent mark is at most this many
pixels"*. That sentence qualifies the number with a scale; it caps the accent's
*relative* footprint, not its absolute pixel count. The implementation and its
test took the stricter absolute reading. The design document the requirement
came from, `warrior-appearance-design.md`, is no longer in the repository — the
improve-visuals package was archived and later pruned — so the constant's own
doc comment in `AppearanceComponentCatalog` is the surviving statement of the
rule, and it says "At apparent scale 1, an accent mark is at most this many
pixels" and nothing more.

**Decision: the cap is scale-relative.** An accent mark is
`max(1, round(MaxAccentPixelSizeAtApparentScale1 × apparentScale))` pixels on a
side, which is two pixels at apparent scale 1 exactly as the requirement
states, and five at the `2.40` clamp ceiling. The constant is unchanged and
still pinned to `2`. The test is rewritten to assert the scale-relative
ceiling — that no accent exceeds `MaxAccentPixelSizeAtApparentScale1 ×
apparentScale`, and that it equals the constant at apparent scale 1 — rather
than the absolute one. Weakening a test to get green is prohibited; this is not
that. The test is asserting a different, and correct, invariant, and it still
fails if the accent grows without bound.

Both marks stay inside the parts they are inscribed in at the new size: the
primary is placed against the head disk's right edge, the head is
`7 × apparentScale` pixels across against the accent's `2 × apparentScale`, and
the secondary sits at the torso's top centre with the torso at least
`7 × BuildMultiplier × apparentScale` across. The existing containment test
covers both and is not relaxed.

## 4. Cause 3 — a trample mark is the same colour as the grass on top of it

Every backdrop shade in this package is a single-axis interpolation from
`ArenaSurface` toward `ArenaBorder`, bounded by
`PlainsBackdropGeometry.MaximumBackdropInterpolation = 0.22` (R-W4.2). The
whole band is crowded:

| Layer | Interpolation values |
| --- | --- |
| Ground cells | `0.00`, `0.06`, `0.12` |
| Decals | `0.10`, `0.16`, `0.22` |
| Grass clusters | `0.14`, `0.18`, `0.22` |
| Trample mark | `0.22` |

A trample mark is drawn at `0.22` — identical to a Large grass cluster, and
identical to the brightest decal. It is drawn under the grass, so a trampled
patch is a `0.22` blot with `0.22` tufts standing on it: the boundary between
the worn ground and the grass that is supposed to have thinned has no contrast
at all. The suppression radius is `40` world units against a cluster scatter
radius of `48`, so one mark thins part of one clump rather than an area, and
the height reduction of `0.55` leaves a suppressed tuft over half its original
height.

**Remedy, in three parts, none of which moves the `0.22` ceiling.**

- **Separate the stubble from the mark by tone.** A suppressed cluster draws at
  a new `TrampleStubbleShadeInterpolation = 0.12` instead of its size class's
  `0.14`–`0.22`. That value is already on the ground ladder, so it introduces
  no new point into the shade band and no new case for the faction-signal
  contrast guard, and it sits below every grass shade — trampled stubble now
  reads as closer to bare ground than the untouched grass around it, which is
  the whole claim row 131 makes.
- **Make one mark cover ground rather than a spot.** `TrampleMarkBaseRadius`
  goes from `16` to `28` screen pixels at apparent scale 1, and
  `TrampleSuppressionRadius` from `40` to `80` world units, so the thinning
  spans a clump and its neighbours rather than one clump's edge. Adjacent
  marks then merge into a single smooth worn area whose size grows with the
  number of casualties, which is what makes the effect read as *where fighting
  happened* rather than as one blot per body.
- **Cut the stubble further.** `TrampleHeightReductionFactor` goes from `0.55`
  to `0.28`, so a trampled tuft is unmistakably shorter than an untouched one
  rather than slightly shorter.

`TrampleMarkShadeInterpolation` stays at `0.22`. It is already at the ceiling
and therefore already maximally separated from the `0.00`/`0.06`/`0.12` ground
ladder; the contrast the row needs comes from the stubble tone and the area,
not from making the mark brighter, which the ceiling forbids anyway.

Marks continue to be fed by `Death` events only. The design's declined
`Attack` feed (OD-W4-a) is not revisited here, and row 131's own wording —
"observe the grass around a cluster of `Death` events" — is what the remedy is
aimed at.

## 5. What this does not do

- No constant in `AppearanceComponentCatalog` changes value.
  `MaxArmorWidthFactor` stays `1.18`, `MaxAccentMarksPerPawn` stays `2`,
  `MaxAccentPixelSizeAtApparentScale1` stays `2`, and all three keep their
  pinning tests.
- `MaximumBackdropInterpolation` stays `0.22` and no shade in this change
  exceeds it.
- No new texture, no new `Begin`/`End` pair, no new draw-call class. Every
  quad here is the shared 1×1 pixel inside the caller's existing batch.
- Nothing is added to a snapshot, nothing is cached, and no simulation state is
  read that the renderer did not already hold.
- No row in `docs/development/smoke-checklist.md` is flipped to `PASS` by this
  change. Rows 128, 129, and 131 return to `PENDING` keeping the observation
  that failed them, which is what that file's reopening rule requires of a
  fixed row: an agent may write the fix, only a person may close the row.

## 6. Verification

- `./scripts/verify.ps1` — the canonical gate, whose real output is the
  evidence. It proves the suites are green and the seed-1 determinism workload
  is unmoved; a presentation-only change must not move a hash, and the gate is
  what says so.
- New unit tests: the armor flank-bar helper's geometry (symmetry, minimum
  width at the apparent-scale floor, containment inside `ArmorBounds`, empty
  pair for an unarmored pawn), the scale-relative accent ceiling, and the
  trample stubble shade's position below every grass shade and at or under the
  backdrop ceiling.
- `PawnQuadCount` is updated with the renderer and its pinned per-pawn totals
  move by exactly the armor delta.
- **Rows 128, 129, and 131 can only be closed by a person at an interactive
  desktop.** The gate proves none of them, and this document does not claim
  otherwise.
