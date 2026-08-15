# Battle event log hidden by default smoke — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

**This family closed in full, on the day it was written.** All five `HEL` rows
were added on 2026-08-15 when the change merged at `8a25abf`, and all five were
run and passed by a person at an interactive Windows desktop the same day, so
the family and its section left the live checklist whole. The plan that created
it was archived on 2026-08-15 under the title "Hide the battle event log by
default — plan", alongside this record.

| Field | Value |
| --- | --- |
| Rows in family | 5 — `HEL-1` through `HEL-5` |
| Rows closed `PASS` and lifted here | 5 |
| Rows still open in the live checklist | 0 — the section was deleted |
| Written | 2026-08-15, when the change landed |
| Closed and lifted | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The change landed at `8a25abf` |
| Launch path | `./scripts/run.ps1` on an interactive Windows desktop |
| Optional screenshot paths | None recorded |

## What the family was for

The battle event log no longer draws on launch. The right-hand column it used to
occupy is given back to the arena, and the log returns either through the
`Events` button on the control bar or through F8. The sound log keeps F9 and its
own button, and the two are independent: either, both, or neither may be shown.

`HEL-5` was the row that mattered most for correctness rather than layout: a
hidden panel that still consumed a click, a scroll, or keyboard focus would have
been a defect invisible to every automated test in the suite.

## How the rows closed

The tester reported all five rows passing and recorded no separate observation.
The `Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| HEL-1 | Launch the client and look at the right-hand side of the window before pressing anything | No event log is drawn, and the arena extends to the right margin of the window rather than stopping short of a log column. The control bar carries a new `Events` button, shown inactive | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| HEL-2 | Press F8, then press F8 again | The event log appears in the right-hand column and the arena narrows to make room for it; the `Events` button reads as active while it is shown. The second press hides it again and the arena returns to full width | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| HEL-3 | Click the `Events` button on the control bar twice | The button toggles the same visibility F8 toggles, and its active state tracks whether the log is shown | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| HEL-4 | With the event log hidden, press F9 to show the sound log | The sound log occupies the whole right-hand column on its own rather than only its usual lower share, and the arena narrows by exactly the column width | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| HEL-5 | With the event log hidden, click and scroll where the log used to be, then press Escape | Nothing in the hidden log reacts: the click reaches the arena beneath it, the wheel drives the camera rather than a log scroll, and Escape is handled by whatever would handle it with no log present | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **The `Actual` column is deliberately thin.** The tester gave verdicts and no
  narrative. No agent may enrich these cells later.
- **These passes are about the log's default visibility, not its contents.** The
  200-event retention bound and the feed's formatting were never in scope for
  any row here.
