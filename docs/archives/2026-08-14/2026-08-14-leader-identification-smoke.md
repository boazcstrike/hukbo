# Leader identification smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file overrides either of those.

This record lifts the whole of the "Leader identification smoke (leader
character plan L7)" family — eleven rows, `LC-1` through `LC-11` — out of
`docs/development/smoke-checklist.md`. The family was written for the leader
character package and every row stood `PENDING` until 2026-08-14: the
automated suites behind it proved preset gating, the cache key, the mark
geometry, the quad accounting, and the inspector row, but none of them proved
that a person watching a battle can pick a leader out, which is what these
eleven rows were for.

All eleven rows were run and passed at an interactive desktop on 2026-08-14,
so the family closed `11` of `11` and was deleted whole from the live
checklist that same day, per the checklist's own rule that a family every one
of whose rows is `PASS` leaves the file outright.

**Not part of this closure: `L-7`.** `L-7` belongs to the leader marker
family in name, but it was moved out of this family's table on 2026-08-13,
before this closing run, into the footwork pressure interrupt section of the
live checklist, because the same movement-preset selector that blocks the
pressure-interrupt rows also blocked it. It remains `PENDING` there and is not
one of the eleven rows this record closes.

**Related but separate: six earlier leader-marker rows.** Six other rows from
the leader marker family were run and passed at an interactive desktop on
2026-08-13, a day before the eleven rows in this file. Their record is a
separate archive titled "Leader marker inspector annotation smoke — six rows
closed 2026-08-13", named here rather than linked because that folder is
pruned periodically.

| Field | Value |
| --- | --- |
| Rows | 11 |
| Source family | 1 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

## Evidence — 2026-08-14 closing run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## What this pass does and does not prove

The verdict recorded on 2026-08-14 is a pass on every row, and nothing more
than a pass. No separate written observation was captured for any of the
eleven rows describing what was actually seen at the desktop — no per-row
note on mark shape, faction kit, hand-off behaviour, inspector wording, or
determinism comparison. Each row's own criterion, reproduced below exactly as
it stood in the live checklist, was judged satisfied by the person watching
it, and that judgement is the entire evidence this file carries for each row.

## Leader identification smoke

The rows below are reproduced as they stood in the live checklist. The
`Actual` column was empty there and stays empty here; only `Status` changed,
from `PENDING` to `PASS`.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| LC-1 | Start a battle and watch it at default zoom without clicking anything | Roughly sixteen warriors carry a mark above their head whose shape differs from every other pawn's outline, not merely its colour | | PASS |
| LC-2 | Zoom all the way out to the Low detail tier | The leader marks are still findable; they do not vanish into the mass or read as rendering noise | | PASS |
| LC-3 | Zoom in on one marked warrior in the Visayan-block faction | It wears datu kit — gold-edged head wrap, gold earrings and necklace, a draped shoulder cloth, a red waist sash — and its immediate neighbours do not | | PASS |
| LC-4 | Zoom in on one marked warrior in the Tagalog-block faction | It wears chief or leader kit; if it is the red-chinina row, the red jacket is the single clearest cue at that zoom | | PASS |
| LC-5 | Zoom in on a marked warrior in a Northern Luzon or generic-levy faction | It looks like its neighbours; the above-head mark plus the inspector are the only identification. This is the designed outcome, not a defect | | PASS |
| LC-6 | Watch until a marked warrior dies | Exactly one other warrior in the contingent picks up the mark, and its appearance changes once, cleanly, without flickering back and forth on subsequent frames | | PASS |
| LC-7 | Click the marked warrior | The inspector states that it is leading, and further down names the appearance preset with its scope, tag, and evidence tier — for example "Visayan Datu", Visayan, Documented, form uncertain | | PASS |
| LC-8 | Click the marked warrior, then hover a second one, while a third is breaking off under pressure | The leader mark, the selection ring, and the break-off band are all visible and none overlaps another | | PASS |
| LC-9 | Click a warrior in a battle running the frozen `IndependentPursuitV1` preset, where `ContingentState` is always `None` | No leadership row appears, because no leader is elected under this preset — if one somehow is elected, the row appears rather than being silently dropped | | PASS |
| LC-10 | Watch a full battle to the end and open the battle report | The report is unchanged; its "Leaderboard" still ranks kills and makes no claim about contingent leadership | | PASS |
| LC-11 | Run the same seed twice and compare the same warrior at the same tick in both runs | Identical appearance and identical leader marks; nothing about who leads or how they look differs between the two runs | | PASS |

## Where the plan and the design live

Both documents behind this work are already archived rather than living in
`docs/plans/`: `docs/archives/2026-08-07/2026-08-07-leader-character-design.md`
and `docs/archives/2026-08-07/2026-08-07-leader-character.md`, named here
rather than linked because that folder is pruned periodically. The shipped
client pairs combat preset `PrecolonialPhilippinesV5` with movement preset
`LastStandEngagementV11`, both set explicitly in `ArenaGame.BuildScenario`;
`LC-9` above is judged against the separate, frozen `IndependentPursuitV1`
movement preset instead, which is the point of that row.
