# GPU render smoke — `GR-4` — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family still did not close.** `GR-4` was run and passed at an interactive
Windows desktop on 2026-08-14 and is lifted into this record. `GR-3` and `GR-5`
stay live in the checklist. The record of `GR-1` and `GR-2`, which closed
earlier the same day, is the archived document titled "GPU render smoke —
PARTIAL 2026-08-14", in this same dated folder.

| Field | Value |
| --- | --- |
| Rows in the family | 5 — `GR-1` through `GR-5` |
| Rows closed `PASS` and lifted here | 1 — `GR-4` |
| Rows closed earlier the same day | 2 — `GR-1` and `GR-2`, recorded separately |
| Rows still open in the live checklist | 2 — `GR-3` and `GR-5` |
| Prior interactive runs | One. `GR-4` was attempted on 2026-08-14 and not run; this is the attempt that succeeded |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time carried uncommitted documentation changes on top of `b8a3f97` |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## The row that closed

The tester reported the row as passing and recorded no separate observation. The
`Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-4 | Compare a seed-1 200-unit battle before and after the Phase 2 commits at the same tick and camera station | No visible difference. Phase 2 is pure removal of duplicated work; any visible difference is a defect, not a new baseline | 2026-08-14, tester at the desktop. Passed, with no separate note recorded | PASS |

## Why this row was the hard one

`GR-4` was the only row in this family with an obstacle that no panel setting
removes. It asks for a comparison *between two builds* — the shipped one and a
pre-Phase-2 revision — at the same tick and the same camera station, rather than
for an observation of the shipped game. When the family was first run earlier on
2026-08-14 the row was left `PENDING` rather than `BLOCKED`, on the grounds that
nobody had established it could not be done, only that nobody had written down
how. That reading turned out to be the right one: the comparison was made and
the row passed.

The claim it settles is the one the whole phase rested on. Phase 2 was pure
removal of duplicated work, so a visible difference between the two builds would
have been a defect rather than a new baseline. The automated suite could never
have answered this: `PawnGeometryTests` pins the two-stage geometry path
bit-identical over a 73,728-case grid and `HitEffectSystemTests` proves the
per-frame pulse lookup returns what the per-pawn scan returned, but bit-identical
geometry inputs and an unchanged lookup result are not the same claim as an
unchanged screen.

## What a later reader should be careful of

- **This row's pass is about Phase 2 only.** It says nothing about whether a
  1,000-unit battle is watchable, which is `GR-3`, or about how hit pulses read
  in a dense melee, which is `GR-5`. Both are still open.
- **Do not repeat the family's original stopping reason.** `GR-3` and `GR-5`
  were reported as impossible because the team size cannot be raised above 500.
  That ceiling is per team: `ArmyCompositionStepper.MaximumUnitsPerTeam` is
  `500`, the panel's row is labelled `Units Per Team`, and `ArenaGame` builds the
  scenario with `composition.UnitsPerTeam * 2`. Setting 500 on both sides is the
  1,000-unit battle those rows ask for.
- **The `Actual` column is deliberately thin.** The tester gave a verdict and no
  narrative. No agent may enrich this cell later.
