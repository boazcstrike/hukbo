# Visual improvement smoke (VIS) — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`;
nothing in this file overrides either of those.

This record exists because `docs/development/smoke-checklist.md` holds open
work only, not a history of what has already been tested. Once a family has
no open row left, that file's own rule is to delete the family from it
outright rather than let it accumulate closed rows. The improve-visuals
family, `VIS-041` and `VIS-043` together, thirty-two rows in all, closed in
full on this date and is deleted from the live checklist. This file is where
its history goes.

## History

The thirty-two rows were run for the first time by a person at an
interactive Windows desktop on 2026-08-11. Twenty-nine passed and were lifted
out that same day, in a record titled "Visual improvement smoke (VIS-041 and
VIS-043) — passed rows"; find it by title rather than path. Three rows
failed that run: row 128 (armor bulk), row 129 (adornment accents), and row
131 (trampled ground).

All three were fixed and re-run on 2026-08-13. Row 129 passed on that re-run
and was archived separately the same day, in a record titled "Adornment
accent legibility, smoke row 129 — closed 2026-08-13"; find it by title
rather than path. Row 128 failed a second time, on a cause different from the
first. Row 131 could not be attempted at all, because the build did not yet
contain anything matching its own precondition, and it was marked `BLOCKED`
for that reason.

Both remaining rows were addressed in a second round of fixes the same day.
Row 128 got a second fix and then passed on a further re-run. Row 131 got a
corpse placeholder that unblocked its precondition and then passed on a
further re-run. The family is now thirty-two of thirty-two closed.

## Row 128 — armor bulk, two causes

Row 128 failed twice, on two different causes, and each cause got its own
fix.

The first cause, found after the 2026-08-11 run: `PawnRenderer.DrawArmor`
filled the whole widened capsule solid in `BarkBrown`, replacing the torso's
dye, outline, and belt with a flat block. That is a recolour rather than
bulk, and a flat single block over the body is the same silhouette a held
shield draws — so the row failed because armor and shield read alike, not
because the armor failed to widen the body. The widening itself was under a
pixel at the default-fit station. The fix, described in
`docs/plans/2026-08-11-armor-accent-trample-legibility-design.md`, redrew
armor as two symmetric flank bars that thicken the body while leaving the
torso's dye, outline, and belt visible down the middle, instead of one slab
covering them.

The 2026-08-13 re-run failed again, on a different cause: "not bulky enough."
The first fix removed the shield read but left the bulk read unfixed. Its own
stated reasoning was half right: it had kept `MaxArmorWidthFactor` at 1.18 on
the grounds that "the ceiling is not the problem", which was correct as far
as it went, but measurement after this second run found that the flank bar's
width equalled the widening margin at both 1920x1080 and 2560x1440 default
fit, so the bar covered exactly the margin and lapped zero pixels onto the
torso. The torso's own dark outline column survived inside the armor, which
drew a plate strapped to the outside of a normal-width body rather than a
widened one. The second fix, described in
`docs/plans/2026-08-13-armor-bulk-second-fix-design.md`, floors the flank bar
at `widening + 1` pixels so it always laps at least one pixel onto the torso
and covers the torso's own outline column, and gives each bar an outer
`OutlineColor` column so the pawn's dark silhouette edge sits at the armored
width rather than at the original body width. Row 128 passed after this
second fix.

## Row 131 — trampled ground, unattemptable precondition

Row 131 was never waiting on a fix to the trample mark itself; the trample
work that landed on 2026-08-11 may well have been correct, but nobody had
been able to look at it, because what the row actually needed was a build in
which a casualty has a visible location on the field to trample around. The
2026-08-13 attempt was abandoned as "no visible casualty" — literally true of
the build, not a tester error. A fallen warrior was drawn only for its
`0.28`-second lethal reaction plus a `0.10`-second hold and then stopped
being drawn entirely: `GetPawnVisualState` never returned `Dead`, and
`DrawDeadMark` was reachable only from the agent inspector, not from the
battlefield view. There was no corpse layer, no minimap mark, and no position
carried on an event-feed entry, so a spectator had nothing by which to find
"a cluster of `Death` events," and the row's own precondition could not be
met.

It was unblocked the same day by the corpse placeholder described in
`docs/plans/2026-08-13-corpse-placeholder-design.md`, already summarized in
its own record: a fallen warrior now stays on the field for the rest of the
battle, desaturated and marked, so a cluster of bodies shows where the
fighting was. Row 131 passed on the re-run that followed.

## Evidence

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 for the first run; 2026-08-13 for the re-runs |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `4fbbdf9` for the first run; `8da5d92` plus uncommitted working-tree changes for the 2026-08-13 runs |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Rows

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 128. Armored figures read as bulkier, not as shielded | At the default-fit or maximum-zoom station, default theme, compare a pawn wearing an armor-layer component (F2 through F5) against an unarmored pawn and against a shield-bearing pawn. The armored pawn reads as visibly bulkier through the torso, and does not read as if it were carrying a shield. | 2026-08-11, tester at the desktop: `FAIL` — not clear. Investigated after the run and the cause is in the draw, not in the eye: `PawnRenderer.DrawArmor` filled the whole widened capsule solid in `BarkBrown`, replacing the torso's dye, outline, and belt with a flat block — a recolour rather than bulk, and a flat single block over the body is the silhouette a held shield draws. The widening itself was under a pixel at the default-fit station. **2026-08-13, re-run at the desktop: `FAIL` again — "not bulky enough".** The first fix removed the shield read and left the bulk read unfixed, and its own stated reason was wrong: it kept `MaxArmorWidthFactor` at 1.18 on the grounds that the ceiling was not the problem, but measurement after this second run found the bar width equal to the widening margin at 1920 × 1080 and 2560 × 1440 default fit, so the bar covered the margin and lapped zero pixels onto the torso. The torso's own dark outline column survived inside the armor, which draws a plate strapped to the outside of a normal-width body. A second fix is described below. **2026-08-13, tester desktop, after second fix: PASS.** | PASS |
| 131. Trampled areas visibly thin where fighting happened | During or after a battle with visible casualties, observe the grass around a cluster of `Death` events. The grass there reads as visibly thinned or trampled compared to untouched ground elsewhere on the field. | 2026-08-11, tester at the desktop: `FAIL` — not clear. Investigated after the run: a trample mark drew at shade interpolation `0.22`, the exact tone of a Large grass cluster, with the grass drawn on top of it — the worn ground and the grass that was supposed to have thinned had no contrast against each other at all. The suppression radius of 40 world units also thinned part of one clump rather than an area. **2026-08-13, re-run attempted at the desktop and abandoned: "no visible casualty".** That is literally true of the build and is not a tester error. A fallen warrior was drawn for `0.28` seconds of lethal reaction plus a `0.10` second hold and then stopped being drawn entirely — `ArenaGame.Rendering.cs` skips any agent whose `IsAlive` is false once the hold expires, `GetPawnVisualState` never returns `Dead`, and `DrawDeadMark` is reachable only from the agent inspector. There was no corpse layer, no minimap, and no position on an event-feed entry, so a spectator had nothing by which to find "a cluster of `Death` events" and the row's own precondition could not be met. It was `BLOCKED` for that reason. **Unblocked the same day** by the corpse placeholder described above: a fallen warrior now stays on the field for the rest of the battle, so a cluster of bodies marks where the fighting was. **2026-08-13, tester desktop, after corpse placeholder: PASS.** | PASS |

## Closing note

A later change to armor drawing, adornment accents, trample marks, or the
corpse placeholder writes fresh rows in the live checklist rather than
reviving these.
