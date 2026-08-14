# Sandata smoke — `SD-7b` — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family still did not close.** `SD-7b` was run and passed at an
interactive Windows desktop on 2026-08-14 and is lifted into this record. `SD-4`
and `SD-5` were run the same day, failed, and stay live in the checklist. The
record of the six Sandata rows that closed earlier is the archived document
titled "Sandata smoke — the closed rows and the first two runs", in the
2026-08-12 folder.

| Field | Value |
| --- | --- |
| Rows in the family | 9 — `SD-1` through `SD-8` with `SD-7b` |
| Rows closed `PASS` and lifted here | 1 — `SD-7b` |
| Rows closed earlier | 6, recorded separately on 2026-08-12 |
| Rows still open in the live checklist | 2 — `SD-4` and `SD-5` |
| Prior interactive runs | `SD-7b` was `BLOCKED` from the day it was written until 2026-08-12, when the theme switcher and the unknown-contact state both shipped. This is its first real attempt |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Windows 11 desktop, interactive |
| Source commit | `8f2207f`, with uncommitted documentation and Hukbo presentation changes in the working tree |
| Launch path | `./scripts/run.ps1 -Game Sandata -Configuration Debug` |
| Optional screenshot paths | None recorded |

## The row that closed

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-7b | View friendly, hostile, and unknown contacts in every shipped theme | All three are distinguishable in `daylight-ops` as well as `night-ops` | 2026-08-14, tester at the desktop. Passed. The tester added one observation: "the red was not there (or was it yellow? or fog of war?)" | PASS |

## The tester's question, answered

The observation is expected behaviour, and it is neither a rendering fault nor
fog of war.

A hostile is drawn by the best contact tier the assaulting faction holds for it,
through `ContactAppearanceResolver.ResolveHostileAppearance` in
`src/Sandata.Client/Rendering/ContactAppearanceResolver.cs`. That resolver has
three outcomes: an identified hostile draws in full, a merely detected one draws
as a facingless marker with no weapon, and a hostile nobody has sensed is not
drawn at all.

`angle-house` spawns two defenders. The one on the top-right objective is on the
blue squad's route, so it is sensed, then identified, and is drawn — that is the
red the row asks about, and seeing it is what makes the row passable. The second
defender, on the bottom-left objective, is out of range of the entire route and
is never sensed by anybody, so nothing about it is ever drawn. The checklist's
own description of the map already says that operator "never does anything".

**It is not fog of war.** The undrawn operator is still in the roster, still
simulated, still able to shoot, and still folded into the state hash. What is
withheld is the drawing, not the simulation. The two coloured squares that remain
visible in that corner are the map's own `OBJECTIVE` records, which are drawn
from the map file rather than from any operator, and are the most likely thing to
have read as "yellow" where a red operator was expected.

## What a later reader should be careful of

- **This row's pass covers contact legibility across both shipped themes and
  nothing else.** It says nothing about `SD-4`'s weapon silhouettes or `SD-5`'s
  automatic fire, both of which were run on the same day and failed.
- **Do not read the missing second defender as a defect.** A future change that
  makes every hostile visible regardless of contact tier would break exactly the
  behaviour this row confirms.
- **`F6` cycles the theme and the choice is not saved.** The next launch starts
  on `night-ops` again, so a re-run has to press it every time.
