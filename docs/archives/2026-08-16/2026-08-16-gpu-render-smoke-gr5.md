# GPU render smoke — `GR-5` — family closed in full 2026-08-16

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family has now closed in full.** `GR-5` was run and passed on an
interactive Windows desktop on 2026-08-16 and is lifted into this record. It was
the last open row of the five, so the whole "GPU render smoke (gpu-render Phases
1 and 2)" section was deleted from the live checklist on the same day, prose and
all, under that file's rule that a family every one of whose rows is `PASS` is
deleted outright.

The records for the other four rows are the archived documents titled "GPU
render smoke — PARTIAL 2026-08-14" and "GPU render smoke — `GR-4`", both in the
dated folder for 2026-08-14, and "GPU render smoke — `GR-3`", in the dated
folder for 2026-08-15.

| Field | Value |
| --- | --- |
| Rows in family | 5 — `GR-1` through `GR-5` |
| Rows closed `PASS` and lifted here | 1 — `GR-5` |
| Rows closed earlier | 4 — `GR-1`, `GR-2` and `GR-4` on 2026-08-14, `GR-3` on 2026-08-15, each recorded separately |
| Rows still open in the live checklist | None. The family is closed and its section was deleted |
| Prior interactive runs | One. `GR-5` was attempted on 2026-08-14 and not run, because the tester read the 500-unit ceiling as a per-battle limit rather than a per-team one; this attempt succeeded |
| Lifted on | 2026-08-16 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-16 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time carried uncommitted documentation changes on top of `85ef6e6` |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## The row that closed

The tester reported the row passing and recorded no separate observation. The
`Actual` column below says exactly that and no more. In particular, no pulse
timing, no frame rate, and no camera station was reported, so none is written
here. That is the same shape of evidence every other row in this family closed
on, and it is worth noticing that the rows asking for an observation have
repeatedly come back with a verdict alone.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-5 | In the same 1,000-unit battle `GR-3` uses — `Units Per Team` at its maximum of 500, which is per team and gives 1,000 warriors on the field — wait for the two sides to close and watch hit pulses in the dense melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | 2026-08-16, tester at an interactive Windows desktop. Passed, with no separate note recorded | PASS |

## What this row settled, and what it did not

It settled the question the row was written for. `HitEffectSystemTests` already
proved that the per-frame pulse lookup returns what the per-pawn scan returned;
what it could not prove is that the pulses still *read* the same on a screen
full of warriors, and that is now answered by a person who watched them in the
densest battle the Army Composition panel can produce.

Together with `GR-3`, it also answers the spectator-facing question the
thousand-unit performance design was written around: a 1,000-unit battle is both
watchable and legible at its effects, on the build as it stands, with no
optimisation work done. That design and its plan phrase their own success
condition in exactly those terms — run the two rows, and build nothing if the
frame holds — so this row is the second half of the evidence that condition
asks for. It is not the whole of it: neither the Phase 0 re-measurement of the
render matrix nor the per-stage attribution has been run, so the *measured* half
of that decision is still missing and the plan remains unauthorized and
unstarted.

It did not settle anything about the presentation effect capacities. The design
lists `EmbeddedProjectileSystem.Capacity` at 256 and `TrampleMarkSystem.Capacity`
at 128 as a legibility question at 1,000 units, and no row in this family asked
about starvation of those effects specifically.

## Why the family stalled twice before it closed

Both stalls had one cause, and it is recorded here because it cost two sessions.
`ArmyCompositionStepper.MaximumUnitsPerTeam` is 500 and the ceiling is **per
team**: `ArenaGame` builds the scenario with `composition.UnitsPerTeam * 2`, so
the maximum the panel already offers is the 1,000-unit battle these rows want.
On 2026-08-14 the tester read that ceiling as a per-battle one and reported
`GR-3` and `GR-5` as impossible to run. They were runnable the whole time.
