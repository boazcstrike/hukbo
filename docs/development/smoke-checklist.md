# Interactive smoke checklist

The manual rows for both games, and **the only place a manual `PASS` may be
recorded.** Split out of `docs/development/testing.md` on 2026-08-11, where it
began 4,082 lines into a 5,708-line file.

Run `./scripts/run.ps1` on an interactive Windows desktop. This repository uses
local-only verification: there is no hosted-CI substitute for this direct
interaction pass.

Every section below is Hukbo's. Sandata had a section here until 2026-08-14,
when its last three rows closed and the family was deleted whole; its record is
the archive titled "Sandata smoke — `SD-5`, and the family closing in full".

**Only a person at an interactive desktop may flip a row.** No agent may, for
any reason, including a passing automated test. Compilation, unit tests, a
window-opening probe, and synthetic input do not make a row pass. A row nobody
attempted stays `PENDING`; a row that cannot be attempted is `BLOCKED` with the
reason recorded.

The gate, the current gate results, and the recorded baselines live in
`docs/development/testing.md`. Superseded measurement runs live in
`docs/development/measurement-history.md`.

## Where the checklist stands, 2026-08-14

18 rows across 3 subsections: **5 `PASS`, 1 `BLOCKED`, and 12 `PENDING`, with
no `FAIL` or `DECLINED` row** — recounted from the status column of this file
on 2026-08-14, after ten families closed in full that day and their subsections
were deleted whole, after the death-collapse family added ten new `PENDING`
rows in a subsection of its own later the same day, and after the UI chrome
nine-slice family added six more at the end of it and a person ran all six the
same evening.

Five of those six passed. The sixth, `CH-4`, is the only `BLOCKED` row in this
file, and it is blocked on hardware rather than on code: it asks a tester to
step through four interface-scale tiers, and `UiScalePolicy.Resolve` caps the
reachable tier by viewport, so a 1080p display can only reach two of them. The
sampler question that row exists to settle — whether a bleed halo appears at
the joins between corner and edge cells — is therefore still unanswered, and
the design's decision to leave it to a tester rather than assert it still
stands open. The contingent shape selector family both joined this file
and left it on that day: `CS-1` and `CS-2` were written as two new `PENDING`
rows in a subsection of their own, then run and passed before the day was out.

The count before this one said thirteen rows, all `PENDING`. Eight of those
thirteen closed on 2026-08-14: battlefield realism's last five rows, `BR-1`
through `BR-4` and `BR-10`; the contingent shape selector's `CS-1` and `CS-2`;
and the render family's `GR-4`. The first two families are therefore gone from
this file whole and the render family is down to the two rows below. Their
records are the 2026-08-14 archives titled **"Battlefield realism cohort
smoke"**, **"Contingent shape selector smoke"**, and **"GPU render smoke —
`GR-4`"**, named here in prose rather than linked because that folder is pruned
periodically. No row's status was changed to arrive at this count.

**No row left in this file carries a failing observation.** Five rows did
during 2026-08-14. `BR-1`, `BR-2` and `BR-10` were re-run against the fixes they
had been waiting on and passed. Sandata's `SD-4` and `SD-5` each failed a third
time that day and were moved from `PENDING` to `FAIL`, then passed on a fourth
and a fifth attempt respectively; both left with their family, which closed in
full. The two render rows below sit at `PENDING` because
nobody has watched them, not because somebody watched them fail.

The families that closed on 2026-08-14 went in roughly this order.
The last-stand engagement family went first, at one row. Three more
followed in one sitting: leader identification at eleven rows, movement gait
animation at fourteen, and ranged units at eleven, all thirty-six run and passed
by a person at an interactive Windows desktop. Three more closed later the same
day: tactical hit animations, whose last two rows were 92 and 94; quit
confirmation, maximize, and Core faction metrics, whose last row was 171; and
footwork pressure interrupt, all eleven of whose rows — `P-1` through `P-10` and
`L-7` — were run for the first time and passed together. That last closure took
the leader marker family with it, because `L-7` was its final open row. Starting
deployment closed after them at five rows, and battlefield realism and the
contingent shape selector closed last, at ten rows and two.

Two families were run on 2026-08-14 and did not close. GPU render has passed
three of its five rows — `GR-1` and `GR-2` early in the day, `GR-4` later — and
all three were lifted out; its other two stay below with the reason they were
not run recorded against them, and the section preamble records why that reason
does not hold. Sandata was the other, and it did close: `SD-7b`, then `SD-4`,
then `SD-5` all passed that day, so the family and its section left this file
whole. Its record is the archive titled "Sandata smoke — `SD-5`, and the family
closing in full".

The two render rows left here are something a person can do today: neither is
blocked by the build, and neither is waiting on a feature that does not exist.
245 rows
have been lifted out of this file since it was split out of
`docs/development/testing.md`. A closed row is not described here once it
leaves; its record is the dated archive that carries its family's name, and this
file is only what is left to run.

**There is no `PASS` column any more, and that is deliberate.** If a `PASS` ever
appears here it is a row that has just closed and has not yet been lifted — not
a row that belongs.

**Recount before trusting that total.** Every figure here that was ever taken
on faith turned out to be wrong. Count the status column itself, and count
every status — a row that is neither `PENDING` nor a result is still a row.

**This file holds open work only.** It is not a record of what has been tested.
A family every one of whose rows is `PASS` is deleted from this file outright:
it is a record rather than a checklist, and keeping it makes the file longer
without giving a tester anything to do. A family is deleted only when it is
entirely `PASS` — an open `FAIL` or `BLOCKED` row is unfinished work and stays
here where a reader will see it. The Sandata section is the worked example of
this rule being applied to prose as well as to rows: on 2026-08-12 six of its
nine rows had closed, and the paragraphs explaining when and why each of them
closed had grown longer than the three rows a tester still had to run. All of
that history left for the archive in one piece and the section was rewritten
around the three open rows. It was rewritten again on 2026-08-14 around the two
that are left.

**A single passing row is lifted out the same way, without its section.** A
section that still carries open work loses each of its rows as that row closes,
and the section's own preamble names which of its rows closed and what to be
careful of when reading the archived result. Two of the rows lifted out that way
closed under a preset or a viewport that is no longer the shipped one, which is
exactly the trap an undated `PASS` sets for the next reader.

**No file in this repository may link to `docs/archives/`.** That folder is
deleted periodically, so a link into it is a link that breaks. Nothing in this
checklist points there, and nothing added to it may. Where a closed row's
evidence is worth naming, it is named in prose — the 2026-08-11 record
**"Closed rows lifted out of families that are still open"** is named that way
here rather than linked. This paragraph used to claim four sections below
referenced that record; a count of the file finds none, and this is the second
figure in this file to have been wrong when taken on faith. Find such a record
by its title rather than by a path, so that a later prune costs a search instead
of the evidence:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'closed-rows-from-open-families'
```

**A fixed row goes back to `PENDING`, never straight to `PASS`.** A row keeps
its `FAIL` observation in `Actual` when it reopens, so the re-run is judged
against what was actually seen. An agent may write the fix; only a person may
close the row.

The families below are grouped by what a single launch can actually
show, because the subsections are ordered by the change that created them
rather than by what is on screen at once, and a person working down the file in
order relaunches the game far more often than they need to. The batch rows below
sum to this file's own total of 18. The Sandata batch left the table on
2026-08-14 when its last three rows closed. They summed to 67 before 2026-08-14, because
two sections had never been given a row here at all; nine more batches left the
table later that day when their families closed in full — the battlefield
realism batch last, and the contingent shape selector batch, which had joined
the table that same morning at two rows, immediately before it. The render batch
shrank rather than leaving.

| Batch | Families | Rows | What one launch has to show |
| --- | --- | --- | --- |
| Render | `GR` 2 of 5 | 2 `PENDING` | Launch-time render behaviour at the largest battle the panel allows. `GR-1`, `GR-2` and `GR-4` passed on 2026-08-14 and were lifted out. Both rows left were attempted that day and not run; the section preamble records why, and why the stated reason does not hold |

**No row in this file is blocked by the build, and this paragraph used to say
the opposite.** Every `SD` row that was once blocked has stopped being so — four
on 2026-08-11 and `SD-7b` on 2026-08-12, each when what it was waiting for was
built. The last rows blocked for any other reason were the eleven movement-preset
rows, which the Army Composition panel's preset selector unblocked on 2026-08-13
and which were run and closed on 2026-08-14. Nothing here is `BLOCKED`, and
every row left is open because nobody has watched it yet.

**One thing a tester still has to set deliberately.** The client's shipped
movement preset is `ClientSettingsStore.DefaultMovementPreset`, which is
`MovementPresetId.CohortLateralSpreadV13` and was `LastStandEngagementV11` until
2026-08-14, and the Army Composition panel lists every registered preset. A
preset chosen there is staged for the **next Full Reset** rather than applied to
the battle in progress, so a round started before the reset is still running the
previous preset. No row left below names a preset, so all of them are read
against that shipped default unless the tester deliberately changes it.

**Controls, so no row has to be attempted by guesswork.** `Space` plays and
pauses; `1`, `2`, and `4` set playback speed; `R` starts the next round and
`Shift+R` is a full reset; `W`/`A`/`S`/`D` or the arrow keys pan; the mouse
wheel zooms; a left click selects a warrior and opens the agent inspector; `F9`
toggles the sound log.

A row moves to `PASS` only when a person at an interactive desktop has seen the
expected result. No agent may flip one, and a passing automated test is not a
substitute for any row here.

## GPU render smoke (gpu-render Phases 1 and 2)

**This family was run for the first time on 2026-08-14 and three of its five
rows closed.** `GR-1` and `GR-2` passed early in the day and were lifted out,
and `GR-4` — the two-build Phase 2 comparison — passed later the same day and
was lifted out with them. Their records are the 2026-08-14 archives titled
**"GPU render smoke — PARTIAL 2026-08-14"** and **"GPU render smoke — `GR-4`"**,
named here in prose rather than linked because that folder is pruned
periodically. The two rows below are what is left, and both are `PENDING`. These rows were
drafted in the plan on 2026-07-28 and moved here on 2026-08-07; they were never
in this file while the workstream ran, which is why no human had worked from
them before that day. This copy is the live one.

**Read this before deciding `GR-3` or `GR-5` cannot be run.** The tester who ran
this family on 2026-08-14 stopped at both of these rows for one stated
reason: the team size cannot be raised above 500. That observation is correct
and it is exactly what `GR-2` asks for, but it is not a blockage, because the
ceiling is **per team, not per battle**.
`ArmyCompositionStepper.MaximumUnitsPerTeam` is `500`, the panel's row is
labelled `Units Per Team`, and `ArenaGame` builds the scenario with
`composition.UnitsPerTeam * 2`. Setting the stepper to its maximum therefore
produces exactly the 1,000-unit battle these rows ask for. `GR-3` and `GR-5` are
runnable today; they have simply not been run.

What the automated work already proves, and what it does not: the render probe
recorded a 1,000-unit default-fit `Draw` p95 of 3 276.6 us against an 8.0 ms
budget, `PawnGeometryTests` pins the two-stage geometry path bit-identical to the
entry points it replaced over a 73,728-case grid, `PawnQuadCountTests` still pins
17, 19, 20 and 40 quads, `PawnAppearanceCacheTests` proves cold-cache equivalence
and the capacity bound, `HitEffectSystemTests` proves the per-frame pulse lookup
returns what the per-pawn scan returned, and `ArmyCompositionStepperTests` proves
the stepper clamps at 500 per team. None of that proves that a 1,000-unit battle
is watchable rather than merely measurable, or that the composition panel still
fits the window at the new maximum. The one claim the whole phase rested on —
that Phase 2 changed no pixel — is no longer open: `GR-4` was run and passed on
2026-08-14, and a person compared the two builds at the same tick and camera
station.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip one of these rows. Compilation, unit tests, and a window-opening probe run
do not. Leave untouched rows `PENDING`; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-3 | Set `Units Per Team` to 500 for both teams, start the resulting 1,000-unit battle, and watch one full engagement | The battle renders and remains watchable; pawns, shields, swings, and hit pulses all read correctly at all three camera stations | Attempted 2026-08-14 and not run. The tester reported that the size cannot be raised above 500; that ceiling is per team, so 500 on each side is the 1,000-unit battle this row wants | PENDING |
| GR-5 | Watch hit pulses in a dense 1,000-unit melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | Attempted 2026-08-14 and not run, for the same reason recorded against `GR-3` | PENDING |

Phase 3's rows GR-6 through GR-10 are deliberately absent. They covered the
instanced backend, which the NO-GO verdict closed and which does not exist.

## Death collapse and the prone body (the 2026-08-14 death-collapse design)

**Ten new rows, all `PENDING`, written on 2026-08-14 when the change landed.**
Nobody has watched any of them. The gate was green on all five stages and the
Client suite went from 3,785 tests to 3,848, and none of that is evidence for a
single row here: what the automated work proves is that the collapse curve, the
transform algebra, the cull envelope's containment, the ordinal store, and the
quad counts behave as specified. Whether a body falling over reads as a death is
not a property a test can hold an opinion about.

What changed, so a tester knows what they are looking at. A warrior that dies is
still held in its struck pose for the lethal hold, 0.34 seconds, exactly as
before. Then — this is new — it topples over about its own feet across 0.45
seconds, overshooting slightly as it lands and settling back, and it stays flat
on the ground for the rest of the battle. Before this change it turned grey and
kept standing. The crossed-out dead mark now draws at the lowest detail tier
only, and the corpse desaturation was softened from a 0.68 blend toward grey to
0.40, both because the prone silhouette now carries the read that the colour and
the mark used to carry alone.

Two things are deliberately unchanged and are not defects: a corpse's weapon
stays in its hand and turns with the body, and the faction-tinted ground ring
under a corpse stays flat and unrotated, because it marks the ground rather than
the body.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip one of these rows. Leave untouched rows `PENDING`; report `BLOCKED`
honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| DC-1 | Watch a single warrior die at the default camera station | It visibly topples over rather than changing colour in place | | PENDING |
| DC-2 | Let a fallen body settle, then look at it | The body is flat on the ground — horizontal, head at ground level a body's length from the feet — not leaning and not tilted part of the way | | PENDING |
| DC-3 | Watch several kills where the attacker's side of the screen is unambiguous | Each body falls away from the blow that killed it, not toward the attacker | | PENDING |
| DC-4 | Let a cluster of casualties build up and look at the group | The bodies read as separate warriors lying at slightly different angles, not as one shape stamped repeatedly | | PENDING |
| DC-5 | Pause playback while a body is mid-fall, wait, then resume | The body holds mid-fall while paused and continues from where it stopped on resume | | PENDING |
| DC-6 | Watch a fight taking place beside earlier casualties | Corpses draw beneath the living and never occlude a fight in progress | | PENDING |
| DC-7 | Pan the camera so a corpse sits at the arena panel's edge | The body stays drawn until it is genuinely off screen, rather than disappearing while part of it is still visible | | PENDING |
| DC-8 | Run a 1,000-unit battle (500 per team) to a heavy casualty count | The corpse field is readable as a battlefield rather than visual noise, and the frame rate holds | | PENDING |
| DC-9 | Zoom fully out, to the lowest detail tier, with casualties on the field | A dead warrior is still distinguishable from a living one — this is the tier where the crossed mark is the signal | | PENDING |
| DC-10 | Compare a corpse against a living warrior of the same faction at Medium and High tier | The corpse reads as dead rather than as a differently-dyed living warrior; the softened desaturation is enough | | PENDING |

## UI chrome nine-slice (the 2026-08-14 UI chrome nine-slice design)

**Six new rows, all `PENDING`, written on 2026-08-14 when the package was
planned.** They are written ahead of the code deliberately, because the design
leaves two questions for a tester to settle rather than for an author to
assert, and both need to be on the checklist before anyone is tempted to decide
them from a screenshot.

What a tester is looking at. A new `PANEL STYLE` selector in the settings menu
switches panel chrome between `Procedural` — the flat rectangles the game has
always drawn — and `NineSlice`, which draws the same panels from a texture
atlas with chamfered corners and an inner accent line. The first cut wires two
panels only, the settings menu panel itself and the confirmation prompt.
Everything else on screen keeps the flat look under both settings, and that is
expected rather than an unfinished edge.

`CH-4` is the one row that decides something. Panel chrome draws inside the
interface batch, which uses `SamplerState.LinearClamp`, and linear filtering on
a pixel-authored atlas can bleed neighbouring texels across the joins between
corner and edge cells. Whether that artefact is visible at any interface scale
is a question for eyes, not for a test, and the answer decides whether the
implementation needs a nested `PointClamp` batch.

The atlas in this first cut is placeholder programmer art. It is not a proposed
visual identity, it makes no historical claim, and "it looks crude" is not a
finding worth recording against these rows.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip one of these rows. Compilation, unit tests, and a window-opening probe do
not make a row pass. Leave untouched rows `PENDING`; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CH-1 | Launch and open the settings menu | A `PANEL STYLE` selector is present, reads `Procedural`, and every panel looks exactly as it did before this package | Selector present and reading `Procedural`; panels unchanged | PASS |
| CH-2 | With the menu open, cycle `PANEL STYLE` to `NineSlice` | The menu panel and the confirmation prompt switch to the sprite skin immediately — no restart, no flicker, no crash — and the chamfered corners are visibly different from the flat border | Both panels switched live and the chamfered corners read as clearly different | PASS |
| CH-3 | Cycle `PANEL STYLE` back to `Procedural` | Both panels revert to the flat-rectangle look, identical to what `CH-1` recorded | Reverted to the flat look | PASS |
| CH-4 | With `NineSlice` active, cycle interface scale through all four tiers and look closely at the joins between corner and edge cells | Corners and margins grow with the interface. Record in the Actual column whether a bleed halo appears at any tier, and at which — this row decides whether a nested `PointClamp` batch is needed | Only two of the four tiers are reachable on the tester's display. `UiScalePolicy.Resolve` caps the configured scale at a ceiling set by the viewport — 125 per cent needs 1920x1080, 150 per cent needs 2560x1440, and 200 per cent needs 3840x2160 — so on a 1080p display 150 and 200 both resolve back to 125. That is pre-existing behaviour and not a chrome defect. Of the reachable tiers the tester reported 125 per cent as ideal and 100 per cent as slightly small, which is a sizing preference rather than the seam observation this row asks for. **The halo question is still unanswered at every tier.** | BLOCKED |
| CH-5 | With `NineSlice` active, cycle through every theme | Chrome recolours with each theme, and no theme leaves the border invisible or illegible against its own panel surface | Recoloured correctly across every theme | PASS |
| CH-6 | Set `NineSlice`, quit, and relaunch | The setting persisted and the sprite skin is active on launch | Persisted across a restart | PASS |
