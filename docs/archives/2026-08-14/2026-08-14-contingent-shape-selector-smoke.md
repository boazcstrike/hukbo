# Contingent shape selector smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family closed in full on the day it was written.** Both of its rows were
run and passed by a person at an interactive Windows desktop on 2026-08-14, so
the section was deleted from the live checklist whole under that file's own
rule.

| Field | Value |
| --- | --- |
| Rows in the family | 2 — `CS-1` and `CS-2` |
| Rows closed `PASS` and lifted here | 2 — both |
| Rows still open in the live checklist | None |
| Prior interactive runs | None. This was the family's first and only run |
| Written and closed on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The selector change shipped at `46ea971`, amended at `b8a3f97` |
| Launch path (`source` or package path) | `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## What these rows were for

`MovementPresetId.ContingentShapeV12` was registered on 2026-08-13 but was
absent from the Army Composition panel's option list, so no spectator could
reach it at all — and on 2026-08-14 `CohortLateralSpreadV13` was appended to
that same list while V12 was still missing from it, so the omission happened
twice before it was caught. The fix made V12 selectable and strengthened the
Client suite to enumerate `MovementPresetRegistry` and fail if any registered
preset is missing from the selector.

The automated suite proved that the option list contains every registered
preset, that arrow keys reach V12 and wrap past the end of the list, and that a
seed-1 headless run under V12 terminates deterministically with an army 22 per
cent narrower and 27 per cent shallower than V11's. What it could not prove is
that either of those things reads correctly on screen, which is what these two
rows were for.

## The rows that closed

The tester reported both rows as passing and recorded no separate observation
for either. The `Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CS-1 | Open the Army Composition panel, focus the movement-preset row, and step through the whole list from the default `V13 Cohort Lateral Spread`. Select `V12 Contingent Shape`, apply, then perform a Full Reset | Both `V12 Contingent Shape` and `V13 Cohort Lateral Spread` appear in the selector, V12 immediately before V13, each label legible at the panel's default width without clipping or truncation, and the battle that follows the Full Reset is fought under V12. Failure is either preset being absent from the selector, a label overflowing the row, or the reset producing a battle indistinguishable from the V13 one because the staged preset was not consumed | 2026-08-14, tester at the desktop. Passed, with no separate note recorded | PASS |
| CS-2 | With the same army composition, watch the opening deployment under `V11 Last-Stand Engagement` and then under `V12 Contingent Shape`, both at the default camera fit, and compare how the two armies are grouped | The V12 army reads as more, smaller contingents than the V11 one, and as occupying visibly less width and depth on the field. Failure is the two deployments being indistinguishable at a glance, or the V12 deployment reading as crowded, overlapping, or clipped against the map edge rather than merely tighter | 2026-08-14, tester at the desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **The selector stages a preset; a Full Reset consumes it.** Both rows depend
  on that. A round started before the reset is still running the previous
  preset, and a reader who forgets this will read the wrong battle.
- **`CS-2` asked the tester to record roughly how many separate groups each
  side read as, for both presets. That figure was not recorded.** The row passed
  on the comparison itself. Do not invent the count later.
- **The client default at the time of this run was
  `MovementPresetId.CohortLateralSpreadV13`**, not V12 and not V11. Reaching
  either of the presets these rows name meant selecting it deliberately.
- **The `Actual` column is deliberately thin.** The tester gave a verdict and no
  narrative. No agent may enrich these cells later.
