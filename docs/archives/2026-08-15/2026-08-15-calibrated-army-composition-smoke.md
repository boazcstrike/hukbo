# Calibrated army composition smoke — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

**This family closed in full.** All three `AC` rows were run and passed by a
person at an interactive Windows desktop on 2026-08-15, so the family and its
section left the live checklist whole.

| Field | Value |
| --- | --- |
| Rows in family | 3 — `AC-1` through `AC-3` |
| Rows closed `PASS` and lifted here | 3 |
| Rows still open in the live checklist | 0 — the section was deleted |
| Written | 2026-08-14, when the default composition changed |
| Closed and lifted | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path | `./scripts/run.ps1` on an interactive Windows desktop |
| Optional screenshot paths | None recorded |

## What the family was for

The client's default army composition moved off an even four-way split of 250
per team and onto the calibrated rank proportions — Datu 48, Maharlika 47,
Timawa 110, Aliping Namamahay 45. Those four counts are a gameplay tuning value
with no evidentiary confidence behind them and they are marked `PROVISIONAL` in
source; they were never to be read as a measurement of pre-colonial Philippine
army composition.

The change is visible for exactly one reason. All three ranged weapons sit under
Timawa, and the ranged rows carry 25 of that rank's 44 weight, so raising Timawa
from 62 to 110 raises the ranged share of a 250-unit team from 14.1 per cent to
25.0 per cent — from roughly 35 missile-armed warriors per side to roughly 63.
That arithmetic was derived from the shipped weight table in
`ArenaGame.CalibratedRosterEntryWeights` and confirmed against the roster
expansion the client performs.

`ClientSettingsStore.SupportedSchemaVersion` moved from 9 to 10 and the store
discarded every version before 10, on the precedent of the 5-to-6 bump: a saved
composition always wins over the default, so an existing settings file would
have pinned the old even split forever. `AC-2` is the row that proved the
discard happens rather than assuming it. The UI chrome nine-slice package then
took version 11 for its own field, so the accepted window was `[10, 11]` at the
time these rows ran; version 9 and older are discarded whole.

## How the rows closed

The tester reported all three rows passing and recorded no separate
observation. The `Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| AC-1 | Launch the game and start the default battle without touching the Army Composition panel, then open the panel and read the four rank counts | The panel reads Datu 48, Maharlika 47, Timawa 110, Aliping Namamahay 45, summing to 250 per team, rather than the old 63 / 63 / 62 / 62 | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| AC-2 | With a settings file already on disk from a build before this change, launch the game and open the Army Composition panel | The old composition is discarded rather than loaded: the panel shows the calibrated counts, not whatever was saved. The theme, gore, motion, camera, UI scale, startup display, and movement preset choices saved alongside it reset too, which is the accepted cost of the discard | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| AC-3 | Watch one full default battle and judge whether the larger missile contingent reads on screen | Roughly a quarter of each army is visibly missile-armed and holding at range while the melee majority closes past them, rather than the ranged warriors being a rarity a spectator has to look for | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **`AC-3` carried a second question that has no answer here.** As written, it
  asked whether the battle still reads as a battle or whether a quarter of each
  side standing off makes it read as a stalemate, and it asked for whatever was
  observed to be recorded. The tester returned a pass verdict and no narrative.
  The question is therefore unanswered, not answered in the affirmative. **No
  agent may enrich this record later**; if the answer matters, a person watches
  another battle and writes a new row.
- **A pass here is a pass against the build and the shipped defaults of
  2026-08-15.** The composition figures, the schema version window, and the
  ranged share are all things a later change can move without this record
  noticing.
