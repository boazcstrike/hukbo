# Collision firmness, battle report, and window shell smoke — completed

**Archived: reference only.** This section was moved out of
`docs/development/smoke-checklist.md` on 2026-08-11, the day its last row
closed. All fifteen rows are `PASS`; nothing here is outstanding and nothing
here is an instruction. It is kept so that a later reader can trace why the
window has no operating-system title bar, why the control bar carries its own
minimize and close buttons, and what a person actually saw when the battle
report was first exercised end to end.

The live checklist is `docs/development/smoke-checklist.md`. Do not re-run
these rows from this file.

---

## Collision firmness, battle report, and window shell smoke (2026-07-28)

Added by the collision report and window shell plan. The canonical
gate passed on 2026-07-28 with `stateHash A080E28DA7C79C20`,
`eventHash 2B6FB3A9A9C1960D`, `measuredTicks 1677`, `outcome Faction0Victory`,
`deterministic true`, `maximumPenetrationRaw 0`, and
`longestBlockedStreakTicks 88`. **A passing gate proved none of the rows below.**
Every one of them needed a human at an interactive desktop, and no agent could
flip one to `PASS`.

The minimize row deserved particular suspicion when it was written.
`SDL_MinimizeWindow` is reached through a `[LibraryImport("SDL2")]` P/Invoke
that compiles cleanly and, at the time these rows were added, had never been
executed in this repository. A clean build was no evidence at all that the
native call worked; if it failed, the button would have been dead with no
visible error. Row 137 is the row that settled it, and it settled it the way
the row demanded — by watching the taskbar rather than by watching the button
react.

**The run that closed the family: 2026-08-11.** The repository owner ran the
game at an interactive Windows desktop and reported all fifteen rows passing.
No row failed and no row was left partly observed, so unlike the `CL` clash and
`V2` weapon-identity families this one closed on a single pass with no code
change between attempts.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `4fbbdf9`. The working tree also carried the then-uncommitted process-DPI-awareness change; which of the two builds the tester launched is not recorded, and no row here is sensitive to it |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| # | Step | Expected | Result | Status |
| --- | --- | --- | --- | --- |
| 134. Watch a battle at the enlarged body radius | Crowds pack visibly tighter and the melee front blocks more firmly than at the old four-world-unit radius. No unit is stranded and no line gridlocks. | 2026-08-11, tester at the desktop: passed. | PASS |
| 135. Run several battles to a terminal outcome | Every battle reaches a decisive result or a legitimate draw. None stalls at the tick limit with both factions alive and unable to move. | 2026-08-11, tester at the desktop: passed. | PASS |
| 136. Confirm the OS title bar is gone | The window has no title bar and no operating-system exit, minimize, or maximize buttons. | 2026-08-11, tester at the desktop: passed. | PASS |
| 137. Click the new Min button | The window minimizes to the taskbar. Clicking the taskbar icon restores it. Watch the taskbar — do not infer this from the button reacting. | 2026-08-11, tester at the desktop: passed. The `SDL_MinimizeWindow` P/Invoke has now been executed at least once. | PASS |
| 138. Click the new Close button | The game exits cleanly. | 2026-08-11, tester at the desktop: passed. | PASS |
| 139. Press Alt+F4, and use Escape then Exit Game | Both still quit the game. | 2026-08-11, tester at the desktop: passed. | PASS |
| 140. Confirm the window still resizes | Dragging a window edge resizes the window, and the layout adapts. `AllowUserResizing` was deliberately left true. | 2026-08-11, tester at the desktop: passed. | PASS |
| 141. Check all six control-bar buttons | Play, Pause, Menu, Sounds, Min, and Close all render fully inside the bar. The Close button is not clipped at the right edge. | 2026-08-11, tester at the desktop: passed. | PASS |
| 142. Open the unit setup menu | Every label, including `Kalis — Thrusting Blade (shielded)`, renders fully inside its row and does not overrun the stepper controls. | 2026-08-11, tester at the desktop: passed. | PASS |
| 143. Check the stepper still reads clearly | The unit count, up to its 250 maximum, centres cleanly in the narrowed value column between the two arrows. | 2026-08-11, tester at the desktop: passed. | PASS |
| 144. Play a battle to the end and open the battle report | The Battle Report button appears on the match summary and opens the report panel. It does not crash. | 2026-08-11, tester at the desktop: passed. | PASS |
| 145. Read the battle report numbers | Kills, damage dealt and taken, accuracy, faction totals, and the highlight lines are populated and plausible against the battle just watched. | 2026-08-11, tester at the desktop: passed. | PASS |
| 146. Scroll the kill leaderboard | The leaderboard scrolls and clips correctly inside its section, and the panel stays inside the arena bounds. | 2026-08-11, tester at the desktop: passed. | PASS |
| 147. Confirm weapon names in the report | Every weapon appears in pair form, for example `Kampilan — Great Blade`, never as a bare cultural name. | 2026-08-11, tester at the desktop: passed. This is the historical-accuracy policy's pair-form rule observed on a real screen rather than asserted by a test. | PASS |
| 148. Start a second battle after finishing one | Next Round and Full Reset both clear the report. The second battle reports its own statistics with nothing carried over from the first. | 2026-08-11, tester at the desktop: passed. | PASS |

**What this family does not cover.** The quit-confirmation prompt, the maximize
and restore buttons, and the Core faction metrics are a separate later family
(rows 156 to 171) added on the same day by a different plan. Those rows are
still live in `docs/development/smoke-checklist.md` and several of them exercise
the same class of never-executed SDL P/Invoke — `SDL_MaximizeWindow`,
`SDL_RestoreWindow`, and `SDL_GetWindowFlags`. Row 137 passing here is evidence
about `SDL_MinimizeWindow` alone and says nothing about those three.

If a later change touches collision firmness, the battle report panel, or the
borderless window shell, write fresh rows in the live checklist rather than
re-running the rows above.
