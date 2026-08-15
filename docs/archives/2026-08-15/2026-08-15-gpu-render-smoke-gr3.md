# GPU render smoke — `GR-3` — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing
that already happened. Never execute it, never treat it as a live task list,
and never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family still did not close.** `GR-3` was run and passed on an interactive
Windows desktop on 2026-08-15 and is lifted into this record. `GR-5` stays live
in the checklist. The records for `GR-1` and `GR-2`, and for `GR-4`, all closed
on 2026-08-14, are the archived documents titled "GPU render smoke — PARTIAL
2026-08-14" and "GPU render smoke — `GR-4`", in the dated folder for that day.

| Field | Value |
| --- | --- |
| Rows in family | 5 — `GR-1` through `GR-5` |
| Rows closed `PASS` and lifted here | 1 — `GR-3` |
| Rows closed earlier | 3 — `GR-1`, `GR-2` and `GR-4`, all on 2026-08-14, recorded separately |
| Rows still open in the live checklist | 1 — `GR-5` |
| Prior interactive runs | One. `GR-3` was attempted on 2026-08-14 and not run, because the tester read the 500 ceiling as a per-battle limit; this attempt succeeded |
| Lifted on | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time carried uncommitted documentation changes on top of `cfe0c22` |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## The row that closed

The tester reported the row passing and recorded no separate observation. The
`Actual` column below says exactly that and no more. In particular, no frame
rate, no camera station, and no per-effect note was reported, so none is
written here.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-3 | Set `Units Per Team` to 500 for both teams, start the resulting 1,000-unit battle, and watch one full engagement | The battle renders and remains watchable; pawns, shields, swings, and hit pulses all read correctly at all three camera stations | 2026-08-15, tester at an interactive Windows desktop. Passed, with no separate note recorded | PASS |

## What this row settled, and what it did not

It settled the question the row was written for: a 1,000-unit battle is
watchable rather than merely measurable, and the Army Composition panel still
fits the window with the stepper at its maximum. Until this run, the only
evidence for either was the render probe's 1,000-unit default-fit `Draw` p95 of
3 276.6 microseconds against an 8.0 millisecond budget, which is a measurement
rather than a judgement about what a spectator sees.

It settled one further thing by accident, and that is worth stating because it
is what had stopped the row twice. `ArmyCompositionStepper.MaximumUnitsPerTeam`
is 500 and the ceiling is **per team**: `ArenaGame` builds the scenario with
`composition.UnitsPerTeam * 2`, so the maximum the panel offers already is the
1,000-unit battle. The 2026-08-14 attempt stopped because the tester read that
ceiling as a per-battle one and concluded the row could not be run. It could,
and it was.

It did not settle `GR-5`. That row asks a narrower question — whether hit pulse
strength and timing read as they did before the per-frame lookup replaced the
per-pawn scan — and it is answered from inside this same battle, in the same
launch, rather than from a larger one that does not exist.
