# Sandata smoke — `SD-4` — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family still did not close.** `SD-4` passed at an interactive Windows
desktop on 2026-08-14 and is lifted into this record. `SD-5` was run in the same
session against the same build and still failed, and stays live in the checklist.
`SD-7b` closed earlier the same day and has its own record.

| Field | Value |
| --- | --- |
| Rows in the family | 9 — `SD-1` through `SD-8` with `SD-7b` |
| Rows closed `PASS` and lifted here | 1 — `SD-4` |
| Rows closed earlier | 6 on 2026-08-12, and `SD-7b` on 2026-08-14 |
| Rows still open in the live checklist | 1 — `SD-5` |
| Prior interactive runs | Three, all failed: 2026-08-11, and twice on 2026-08-12. This was the fourth attempt |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Windows 11 desktop, interactive |
| Source commit | **`1cb7c4d` on branch `sandata-sd4-sd5`, not `main`.** See the warning below |
| Launch path | `./scripts/run.ps1 -Game Sandata -Configuration Debug`, run from the `sandata-sd4-sd5` worktree |
| Optional screenshot paths | None recorded |

## The row that closed

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-4 | Watch a rifle operator cross a doorway, then a pistol operator cross the same one | The rifle operator lowers the weapon and re-raises it; the pistol operator does not | 2026-08-14, fourth attempt, tester at the desktop. Passed on all three checks: the inspector named the firearm, the rifle operator's lowered state flipped across the doorway under single-step, and the pistol operator's never did | PASS |

## Read this before trusting the `PASS`

**The build that passed is not on `main`.** Branch `sandata-sd4-sd5` was unmerged
when this row closed, because another session held uncommitted work across the
main checkout, including `CLAUDE.md` and `AGENTS.md`, which the same package
edits. A person re-running `SD-4` from `main` before that merge lands is running
the build that failed three times, not the one that passed.

## Why it took four attempts

The first three attempts each fixed something real and none of them made the row
observable. The 2026-08-11 attempt found every operator drawing the same
placeholder weapon. The first 2026-08-12 attempt found the weapon tinted with the
faction colour and sitting inside the body's own ground ring. The second found
the genuine simulation defect: stage 11 computed the weapon-lowered condition,
handed it to the weapon chain, and threw the result away, so
`OperatorState.WeaponLowered` was a constant `false` for every run ever recorded.

That last fix was correct and the row still failed, which is the interesting
part. Three separate things were standing between a correct simulation and a
person's eyes, and the fourth attempt had to remove all three:

- **There was no doorway crossing to watch.** `NavSearch` consults only the
  blocked span it is handed and never reads `NavGrid.Passability`, and
  `SandataSimulation` allocated that span once and never wrote to it. A\*
  therefore searched a fully open grid on every map and operators walked through
  walls, so the squad never funnelled through the authored 40-unit aperture. The
  transition still fired, from the wall-proximity branch, at a place that read on
  screen as walking through a wall.
- **Nothing said which operator was which.** Both blue operators walk the route
  together, one with an AK and one with a Glock, and pistols are exempt from the
  lowered rule — so the pistol operator's correct behaviour is nothing happening.
  `src/Sandata.Client/UI` contained no reference to `Firearm` or `WeaponClass`, so
  a tester watching the Glock had no way to discover they were watching the wrong
  operator. The inspector now names the firearm and the lowered state.
- **The transition had no consumer.** `MissionEventKind.WeaponLowered` and
  `WeaponRaised` were emitted and hashed and read by nothing — no `LogEvents`
  constant, both `EventFeed` readers filtering to `ShotFired`, `SandataEventLog`
  never instantiated. A `sim.sandata.weaponState` line now records each
  transition with its tick.

The lesson worth carrying is the one about evidence. Three sessions in a row
shipped green Sandata suites for this row. A green suite said nothing about
whether a person could see the thing it tested.

## What a later reader should be careful of

- **Do not shrink `LoweredWallDistanceWu` to fix anything.** The doorway aperture
  is 40 world units, so its centre is 20 from each jamb; any threshold below 20
  stops a doorway lowering the weapon at all and silently un-passes this row. The
  constant is also folded into `SandataRuleset.ContentHash`, so changing it costs
  a new preset version.
- **The pistol never lowering is the assertion, not a gap.** A change that makes
  every operator lower would break what this row confirms.
- **`SD-5` failed against this same build**, for a cause this row's fixes
  uncovered: the rifle is forced lowered for the whole approach through a
  corridor, so it never fires, so no automatic round has ever been produced on
  this map. That work is live, not archived.
