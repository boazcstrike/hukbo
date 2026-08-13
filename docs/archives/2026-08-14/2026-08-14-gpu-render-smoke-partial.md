# GPU render smoke — PARTIAL 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family did not close.** Two of its five rows were run and passed on
2026-08-14 and are lifted into this record. The other three — `GR-3`, `GR-4`,
and `GR-5` — stay live in the checklist. Read the "What did not close" section
below before assuming this family is done with, and read it before repeating the
tester's reason for stopping, which was based on a misreading of what the
composition panel's ceiling means.

| Field | Value |
| --- | --- |
| Rows in the family | 5 — `GR-1` through `GR-5` |
| Rows closed `PASS` and lifted here | 2 — `GR-1` and `GR-2` |
| Rows still open in the live checklist | 3 — `GR-3`, `GR-4`, `GR-5` |
| Prior interactive runs | None. This was the family's first |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time was `7036490` plus uncommitted documentation changes |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## What these rows were for

The automated work behind this family already recorded a 1,000-unit default-fit
`Draw` p95 of 3 276.6 us against an 8.0 ms budget; `PawnGeometryTests` pins the
two-stage geometry path bit-identical to the entry points it replaced over a
73,728-case grid; `PawnQuadCountTests` still pins 17, 19, 20 and 40 quads;
`PawnAppearanceCacheTests` proves cold-cache equivalence and the capacity bound;
`HitEffectSystemTests` proves the per-frame pulse lookup returns what the
per-pawn scan returned; and `ArmyCompositionStepperTests` proves the stepper
clamps at 500 per team.

None of that proves that a 1,000-unit battle is watchable rather than merely
measurable, that the composition panel still fits the window at the new maximum,
or that Phase 2 changed no pixel — which is the one claim the whole phase rests
on. Two of those three questions are still open.

## The rows that closed

The tester reported both rows as passing and recorded no separate observation
for either. The `Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-1 | Launch the game normally with `./scripts/run.ps1` | The window still runs with vertical retrace enabled; no tearing appears and frame pacing is unchanged. The retrace override from GPU-006 is probe-only and must never reach a normal launch | 2026-08-14, tester at the desktop. Passed, with no separate note recorded. | PASS |
| GR-2 | Open the army composition panel and raise a team to 500 | The stepper reaches 500, refuses to go higher, and every row and both buttons stay fully on screen | 2026-08-14, tester at the desktop. Passed, with no separate note recorded. | PASS |

## What did not close

**`GR-3`, `GR-4`, and `GR-5` were all reported as impossible to run, with the
same reason: the team size cannot be raised above 500.** That is a true
observation of the panel and a correct pass for `GR-2`, but it is not a
blockage, and the three rows were left `PENDING` rather than being recorded
`BLOCKED` because of what the ceiling actually is.

The stepper's ceiling is **per team, not per battle**.
`ArmyCompositionStepper.MaximumUnitsPerTeam` is `500`, its own comment says "500
per team is 1,000 units on the field", the panel's row is labelled `Units Per
Team`, and `ArenaGame` builds the scenario with `composition.UnitsPerTeam * 2`.
Setting the stepper to its maximum therefore produces exactly the 1,000-unit
battle `GR-3` and `GR-5` ask for. Nothing in the build prevents those two rows
from being run; they were simply not run.

`GR-4` is different and is the one row here with a real obstacle that no panel
setting removes. It asks for a seed-1 200-unit battle to be compared *before and
after the Phase 2 commits* at the same tick and camera station, which means
building and running a pre-Phase-2 revision alongside the current one. That is a
two-build comparison, not an observation of the shipped game, and no route to it
has been written down. It stays open and its cost stays unrecorded.

Phase 3's rows `GR-6` through `GR-10` never existed here: they covered the
instanced backend, which the NO-GO verdict closed and which was never built.

## What a later reader should be careful of

- **`GR-2`'s pass is about the ceiling holding, not about the ceiling's value.**
  It proves the stepper stops at 500 and the panel still fits. It says nothing
  about whether a battle at that size renders acceptably, which is `GR-3`.
- **Do not repeat the stopping reason.** A future tester who reads "cannot go
  above 500" and stops has stopped one setting short of the battle the row asks
  for. Set `Units Per Team` to 500 on both teams; the field then holds 1,000.
- **The `Actual` column is deliberately thin.** The tester gave a one-word
  verdict on each of the two closed rows. No agent may enrich these cells later.
