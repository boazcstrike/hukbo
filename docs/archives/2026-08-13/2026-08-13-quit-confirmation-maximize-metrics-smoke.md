# Quit confirmation, maximize, and Core faction metrics smoke — rows 156 to 170 closed 2026-08-13

**Archived: reference only.** All fifteen rows below are `PASS` and were moved
out of `docs/development/smoke-checklist.md` on 2026-08-13, the day they
closed. Nothing here is outstanding and nothing here is an instruction.

The source section, "Quit confirmation, maximize, and Core faction metrics
smoke (2026-07-28)," carried sixteen rows, 156 through 171. Rows 156 through
170 were attempted by a person at an interactive Windows desktop on
2026-08-13 and all fifteen passed. Row 171 was not attempted and stays
`PENDING` in the live checklist, so the section itself remains open there,
carrying only that one row. Do not re-run any of the fifteen rows recorded in
this file. If a later change touches the quit confirmation prompt, window
maximize/restore behaviour, or the battle report's faction metrics, write a
fresh row in the live checklist rather than reviving one of these.

| Field | Value |
| --- | --- |
| Rows | 15 |
| Source family | 1, still open |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Why these rows deserved suspicion

The live checklist's own preamble for this section singles out the maximize
and restore rows for particular suspicion: `SDL_MaximizeWindow`,
`SDL_RestoreWindow`, and `SDL_GetWindowFlags` are P/Invokes that compile
cleanly and had never been executed anywhere in this repository. A clean
build was no evidence that any of the three actually worked.
`SDL_MinimizeWindow` had been executed and does work, which said nothing
about these three. Rows 165, 166, and 167 below are the first execution
evidence in this repository that `SDL_MaximizeWindow`, `SDL_RestoreWindow`,
and `SDL_GetWindowFlags` behave as intended — row 165 exercises
`SDL_MaximizeWindow`, row 166 exercises `SDL_RestoreWindow` by way of the
`Max` button toggling back, and row 167 exercises `SDL_GetWindowFlags` by
requiring the button to read the real window state after an external
maximize rather than a tracked flag. The pass recorded below is the first
time any of the three P/Invokes had a human watch them run.

The tester gave a single bulk verdict covering all fifteen rows rather than a
per-row note, so the "Actual" column below reads identically for every row.
No per-row observation beyond that bulk pass was recorded, and none is
invented here.

## Quit confirmation, maximize, and Core faction metrics smoke

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| 156. Click `Close` on the control bar | A confirmation prompt appears. The game does not quit. The battle behind it is dimmed. | Passed 2026-08-13 | PASS |
| 157. Cancel the prompt | The prompt closes and the battle continues untouched. | Passed 2026-08-13 | PASS |
| 158. Confirm the prompt | The game exits. | Passed 2026-08-13 | PASS |
| 159. Open the prompt and press `Enter` immediately | Cancel holds focus, so `Enter` cancels rather than quitting. | Passed 2026-08-13 | PASS |
| 160. Open the prompt and press `Escape` | The prompt cancels. If the menu was open behind it, the menu stays open — `Escape` belonged to the prompt alone. | Passed 2026-08-13 | PASS |
| 161. Open the prompt, press Tab or an arrow key, then `Enter` | Focus moves to Quit and `Enter` then quits. | Passed 2026-08-13 | PASS |
| 162. Open the prompt and click well away from both buttons | Nothing happens. The click does not reach the control bar, the arena, or agent selection underneath. | Passed 2026-08-13 | PASS |
| 163. Menu, then `Exit Game` | The same prompt appears. The menu path does not quit directly. | Passed 2026-08-13 | PASS |
| 164. Press Alt+F4 | Quits immediately with no prompt, by design — it is the guaranteed escape hatch on a borderless window. | Passed 2026-08-13 | PASS |
| 165. Click `Max` | The window maximizes. | Passed 2026-08-13 | PASS |
| 166. Click `Max` again | The window restores to its previous size. | Passed 2026-08-13 | PASS |
| 167. Maximize outside the app (Windows snap or taskbar), then click `Max` | It restores rather than re-maximizing — the button read the real window state instead of a tracked flag. | Passed 2026-08-13 | PASS |
| 168. Check all seven control-bar buttons | Play, Pause, Menu, Sounds, Min, Max, and Close all render fully inside the bar. Close is not clipped at the right edge. | Passed 2026-08-13 | PASS |
| 169. Open the battle report and read a faction line | Attack, hit, and accuracy figures are present, and the estimated figures are marked with a tilde. | Passed 2026-08-13 | PASS |
| 170. Read the battle report disclosure line | It states that attacks and accuracy are simulation-reported while kills, damage, and warrior rows are estimated. | Passed 2026-08-13 | PASS |

## What did not close

Row 171, "Compare the reported faction accuracy against a headless run of the
same seed," stays `PENDING` in the live checklist. It was not attempted in
this session. Its expected result is that the reported figure matches the
simulation's own counters rather than an event-derived approximation, and
checking that requires running the same seed through the headless runner and
comparing its counters against what the battle report displayed — a
comparison against a second, separate run, not something a person can confirm
by looking at the screen once. Because the tester who closed rows 156 through
170 only looked at the screen, row 171 could not be closed alongside them,
and it is not restated here as anything other than open.
