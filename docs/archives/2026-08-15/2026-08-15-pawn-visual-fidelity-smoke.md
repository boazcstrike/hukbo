# Pawn visual fidelity smoke — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

**This family closed in full.** Both `PVF` rows were run and passed by a person
at an interactive Windows desktop on 2026-08-15, so the family and its section
left the live checklist whole. The plan and design that created it were archived
on 2026-08-14 under the titles "Pawn visual fidelity — plan" and "Pawn visual
fidelity — design".

| Field | Value |
| --- | --- |
| Rows in family | 2 — `PVF-1` and `PVF-2` |
| Rows closed `PASS` and lifted here | 2 |
| Rows still open in the live checklist | 0 — the section was deleted |
| Written | 2026-08-14, when the package landed |
| Closed and lifted | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path | `./scripts/run.ps1` on an interactive Windows desktop |
| Optional screenshot paths | None recorded |

## What the family was for

`PVF-1` existed because no published source anywhere gives an on-screen pixel
height at which leg motion stops being worth drawing. Two research passes looked
for one and found nothing, so the package measured our own instead. That
measurement is recorded in `docs/development/testing.md` under the pawn gait
leg-motion pixel subsection, and its figures were repeated in the row's
`Expected` column so the tester judged against a number rather than an
impression. The measurement also established that leg motion never fades through
sub-pixel sizes: it disappears as a step function when a pawn resolves
`PawnDetailTier.Low`, where the leg and foot rectangles are empty and nothing is
drawn at all.

`PVF-2` existed because all three projectile colours sat inside the sixty-unit
ground contrast envelope against at least one shipped theme — the shaft at 28.2
and the head at 47.8 against Field Manual, the fletch at 29.9 against Broadcast.
The colours were retuned by search against that metric until all eighteen
colour-to-shade distances cleared sixty, the closest at 62.9. A test pins the
arithmetic; only a person could say whether the result still looks like a wooden
shaft, a metal head, and a feather fletch rather than like three bright markers.

## How the rows closed

The tester reported both rows passing and recorded no separate observation. The
`Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| PVF-1 | Watch warriors walking and running at each of the three camera stations in turn, and say at which station the legs stop reading as legs in motion | Leg motion is legible at the default fit, where a leg draws 10 pixels tall with 3 pixels of walk stride travel and 6 of run travel. It is still legible at maximum zoom, at 18 pixels with 6 and 11. At minimum zoom the pawn resolves `Low` and no legs are drawn at all, which is expected and is not a failure | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| PVF-2 | Cycle every ground theme with missile troops shooting, and watch arrows and shot in flight against each ground | On every theme, the projectile is visible in flight against the ground for its whole travel, and still reads as its own material — amber wood shaft, cool grey-blue metal head, pale cream fletch — rather than as an arbitrary bright marker | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **Both rows asked for an observation that was never written down.** `PVF-1`
  asked which camera station is the last one where the walk and the run are
  distinguishable from each other, and `PVF-2` asked for any theme where the
  projectile vanishes or looks like a glowing dot. Neither answer exists. The
  passes say a person looked and was satisfied, and nothing more. **No agent may
  enrich these cells later.**
- **`PVF-2` passed against the themes shipped on 2026-08-15.** A new ground
  theme is a new background the projectile colours have never been checked
  against, and the sixty-unit envelope is the metric to re-run when one is
  added.
