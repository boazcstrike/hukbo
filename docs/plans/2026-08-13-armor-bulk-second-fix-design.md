# Armor bulk, second fix — design

Date: 2026-08-13. Scope: `Hukbo.Client` presentation only. No simulation type,
no tick stage, and no hash is touched by anything in this document.

## 1. Why this exists

Smoke row 128, "Armored figures read as bulkier, not as shielded", has now
failed twice.

| Run | Verdict | What the tester said |
| --- | --- | --- |
| 2026-08-11 | `FAIL` | "not clear" |
| 2026-08-13 | `FAIL` | "not bulky enough" |

The first failure was diagnosed in
`docs/plans/2026-08-11-armor-accent-trample-legibility-design.md`. That document
found `PawnRenderer.DrawArmor` filling the whole widened capsule solid in
`BarkBrown`, which replaced the torso's dye, outline, and belt with a flat block
— the silhouette a held shield draws. It replaced the slab with two symmetric
flank bars, which fixed the *"does not read as if it were carrying a shield"*
half of the row.

It did not fix the other half. The same document decided, in as many words, to
*"keep `MaxArmorWidthFactor` at 1.18; the ceiling is not the problem"*. The
second failure says the ceiling was not the whole problem but the read still is
not there, and this document explains what the first one missed.

## 2. What the first fix missed

`PawnRenderer.DrawTorso` draws a stepped capsule outline in `OutlineColor` at
`layout.TorsoBounds` and then fills `Inset(TorsoBounds, 1)`. `DrawArmor` runs
afterwards and draws two plain rectangles in `BarkBrown` at the flanks of the
wider `layout.ArmorBounds`.

The consequence is the whole of the second failure. **The pawn's dark silhouette
edge stays at unarmored torso width, and the armor is painted outside it.** A
reader's eye follows the outline first, finds the body edge where an unarmored
body's edge would be, and reads the brown beyond it as something attached to the
outside of the warrior rather than as part of a thicker warrior. Two further
details push the same way: the bars are square rectangles drawn against a
stepped, rounded capsule, so their corners protrude past the body profile like a
rigid plate; and a flat band of a single tone carries no shading, so nothing in
it suggests volume.

The measured widths explain why nothing rescued the read at either station the
row names. Torso width is `round(7 × BuildMultiplier × apparentScale)`
(`PawnGeometry.cs:972`), and `PawnAppearanceFactory.SelectBuild` returns one of
`0.86`, `1.00`, or `1.18` (`PawnAppearanceFactory.cs:171`).

There is no single default-fit station, because `SpectatorCamera.Fit`
(`SpectatorCamera.cs:126`) sets zoom from the window: `min(viewportWidth × 0.88 /
1280, viewportHeight × 0.80 / 720)` against the default 1280 × 720 map, and
`ResolveApparentScale` is `clamp(zoom × 1.35, 0.72, 2.40)`. The station therefore
moves with the display, which matters because the fix has to work at all of them.
At `ArmorWidthFactor` 1.18 and build 1.00:

| Window at default fit | apparentScale | Torso | Armored capsule | Widening | Old flank bar |
| --- | --- | --- | --- | --- | --- |
| 1280 × 720 | 1.08 | 8 px | 9 px | 1 px | 1 px |
| 1920 × 1080 | 1.62 | 11 px | 13 px | 2 px | 2 px |
| 2560 × 1440 | 2.16 | 15 px | 18 px | 3 px | 3 px |
| Maximum zoom | 2.40 | 17 px | 20 px | 3 px | 3 px |

One pixel per side cannot express bulk under any draw. But note the two middle
rows, which is where the first fix's real failure lives: the bar width equals the
widening exactly, so the bar covers the margin outside the torso and laps **zero**
pixels onto the torso's own flank. The torso's dark outline column therefore
survives inside the armor, and a reader sees, from outside in, a dark armor edge,
brown fill, a dark torso outline, then dye. Two dark lines with brown between them
is a plate strapped to the outside of a normal-width body. The old code's doc
comment claimed the bars "lap a pixel or more onto the torso's own flank"; that
claim was false at every station but the smallest.

Armor also never draws at all below `MediumDetailScale = 0.95`, because
`CreateArmor` returns `Rectangle.Empty` at `PawnDetailTier.Low`
(`PawnGeometry.cs:957`, `:1988`). Every number above is inside the range where
armor actually draws.

## 3. What does not move

`AppearanceComponentCatalog.MaxArmorWidthFactor` stays at `1.18f`. Its doc
comment claims the value equals `PawnAppearanceFactory`'s own maximum build
roll, and that claim was checked against the code for this document: `SelectBuild`
tops out at exactly `1.18`. The ceiling therefore reuses a bound the appearance
system already lives with, rather than inventing one, and raising it would widen
the armored silhouette past the widest body the roster can roll. No catalog
`ArmorWidthFactor` moves either.

`ConservativePawnCull` and its visual bounds are untouched. Everything this
document changes stays inside `layout.ArmorBounds`, which the cull already
accounts for.

Correction to the earlier document, recorded here because it was load-bearing
for its own reasoning: it states the build multiplier range as `[0.72, 2.40]`.
That is wrong. `[0.72, 2.40]` is the apparent-scale clamp range
(`PawnGeometry.cs:221`); the build multiplier is the three-value set above. The
arithmetic in section 2 of that document is affected, though its conclusion —
that the widening is about a pixel at the default-fit station — happens to
survive.

## 4. The fix

Make the armor carry the silhouette instead of sitting beside one.

**Geometry, in `PawnGeometry.GetArmorFlankBars`.** The bar width becomes

```
barWidth = max(max(2, round(apparentScale)), widening + 1)
```

capped so the bars never meet in the middle. Three rules are folded into that
line. The floor of two exists because one of the bar's pixels is about to become
an outline column, and a one-pixel bar would leave nothing to fill. The
`widening + 1` term is the correction section 2 identified: **a bar must always
exceed the margin by at least one pixel, so that it laps onto the torso's own
flank and covers the torso outline column drawn there.** Without it the armored
pawn keeps an unarmored-width silhouette edge with a plate beside it. The
`round(apparentScale)` term survives from the first fix so the bars do not round
away at the apparent-scale floor.

Two further rules join it: the torso dye strip visible between the bars stays at
least one third of the capsule width, so the pair can never degenerate back into
the slab this replaces; and each bar is inset one pixel at top and bottom so its
square corners no longer poke past the stepped capsule's rounded profile. The
middle-strip cap never binds at a real station — it allows 3, 4, and 6 pixels
where the floor asks for 2, 3, and 4.

**Draw, in `PawnRenderer.DrawArmor`.** Each bar becomes three fills rather than
one:

1. the bar fill in the armor material tone, as today;
2. a one-pixel column in `OutlineColor` on the bar's **outer** edge, which is the
   change that matters — the pawn's dark silhouette edge now sits at armored
   width instead of at torso width, so the body itself reads as wider;
3. at `PawnDetailTier.High` only, a one-pixel column on the bar's **inner** edge
   in a darkened form of the same material tone, so the thickening carries a
   shaded edge and reads as volume rather than as a flat band.

The darker tone is derived by scaling the colour that was already passed in,
rather than from `DyePalette.BarkBrown` directly, so the hit pulse and the dead
state keep applying to it exactly as they apply to the bar itself.

Symmetry is preserved by construction at every step. A single-sided offset block
is what a held shield draws, and row 128's failure clause is precisely that armor
must not read as one.

## 5. Cost

`DrawArmor` cost two quads before this change. It now costs **zero at `Low`, four
at `Medium`, and six at `High`** — zero because `ArmorBounds` is already empty at
`Low` and the draw is a no-op there, and four against six because the darkened
inner-edge column is a `High`-tier detail. `SubmissionCount.CountArmor` mirrors
the renderer quad for quad and took the detail tier as a new parameter to do so.
One pinned total moved with it: `PawnQuadCountTests.Count_PinsTheHighTierFullyLoadedSelectedPawn`,
45 to 49. No GPU budget assertion went red, so none was weakened, skipped, or
re-pinned.

## 6. How this is proven

It is not proven by the gate. This is a presentation change; a green
`./scripts/verify.ps1` shows only that nothing else broke, and the seed-1 hashes
are expected to be unmoved because no simulation state is involved. Automated
tests can show that the bars are at least two pixels wide, symmetric, inside
`ArmorBounds`, and leave a middle strip of the required width — and none of that
is the row.

**Row 128 closes only when a person at an interactive Windows desktop looks at
the screen again and reports that an armored warrior reads as bulkier through
the torso without reading as if it were carrying a shield.** That has now been
attempted twice and failed twice, so the row stays open, carrying the observation
that failed it, until a third run says otherwise.
