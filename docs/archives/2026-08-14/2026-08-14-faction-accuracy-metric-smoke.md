# Quit confirmation, maximize, and Core faction metrics smoke — row 171 closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

This is the second and final record for this family. Fifteen of its sixteen rows
were lifted out on 2026-08-13 into the record titled **"Quit confirmation,
maximize, and Core faction metrics smoke — rows 156 to 170 closed 2026-08-13"**,
which is named here in prose rather than linked. Row 171 was the one that stayed
behind, and it was run and passed by a person on 2026-08-14. The family now
stands at 16 of 16 and its section has been deleted from the live checklist.

| Field | Value |
| --- | --- |
| Rows in the family | 16 (numbered 156 to 171) |
| Rows closed in the earlier record | 15 |
| Rows closed here | 1 — row 171 |
| Rows still open anywhere | 0 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time was `7036490` plus uncommitted documentation changes |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## Why this row outlived the other fifteen

Row 171 is not an observation of the screen, and that is why it did not close
with the rest of the family. The other fifteen rows ask what a window does when
it is quit, maximized, or restored, and a tester answers them by looking. This
one compares the faction accuracy the battle report prints against a headless
run of the same seed, so running it means running the game *and* the headless
runner and holding the two figures side by side. A passing gate proved nothing
about it, and no agent could have flipped it.

The claim underneath the row is that the reported accuracy comes from the
simulation's own counters rather than from an approximation reconstructed out of
the event stream. Those are two different numbers whenever the event feed drops
anything, and only running both halves of the comparison distinguishes them.

## The row that closed

The tester reported the row as passing and recorded no separate figures. The
`Actual` column below says exactly that and no more.

| # | Step | Expected | Result | Status |
| --- | --- | --- | --- | --- |
| 171. Compare the reported faction accuracy against a headless run of the same seed | It matches the simulation's own counters rather than an event-derived approximation. | 2026-08-14, tester at the desktop. Passed, with no separate note recorded and no figures captured. | PASS |

## What a later reader should be careful of

- **No numbers were written down.** The pass records that the two figures
  matched on the build of 2026-08-14 for whatever seed the tester used; it does
  not record the seed, the accuracy, or the two runs' hashes. A future reader
  who needs those has to run the comparison again.
- **This pass is about the metric's source, not its value.** It says the report
  reads the simulation's counters. It says nothing about whether those counters
  are themselves correct, which is a question for the Core tests rather than for
  a manual row.
- **The `Actual` column is deliberately thin.** No agent may enrich it later.
