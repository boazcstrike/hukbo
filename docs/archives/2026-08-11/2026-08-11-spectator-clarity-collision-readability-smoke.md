# Spectator clarity and collision readability smoke — completed

**Archived: reference only.** These two sections were moved out of
`docs/development/smoke-checklist.md` on 2026-08-11, the day their last rows
closed. All fifty-two spectator clarity rows and all seven collision
readability rows are `PASS`; nothing here is outstanding and nothing here is an
instruction. The file is kept so that a later reader can trace what a person
actually saw of the spectator controls, the event log, the sound panel, the
army composition panel, the blood rendering, and the collision battle line.

Nothing in the repository links to this file. The live checklist holds open
work only and does not point at `docs/archives/`, which is deleted
periodically. Do not add a link back to it, and do not re-run these rows from
this file. If a later change touches any of those areas, write
fresh rows in the live checklist rather than reviving these.

---

## Spectator clarity smoke

**This family is complete: all fifty-two rows `PASS`.** It took two interactive
runs.

The first, on 2026-07-27, was transcribed from the repository owner's report to
the role 17 review. It closed rows 1 and 3 outright and left rows 2, 4, 5, and
15 partly observed — each of those recorded the half that had been seen and
stayed `PENDING`, because a row is a single status and half a row is not a
pass. The remaining forty-six rows were never attempted on that run.

The second run, on 2026-08-11, closed every row the first left open. The
repository owner reported all fifty-two passing, the four partly-observed rows
included, having exercised the halves July had missed.

**What this record does and does not contain.** The 2026-08-11 run was reported
as a whole rather than row by row, so every row closed on it carries the same
attestation in its `Actual` column rather than a distinct sentence describing
what was on screen. That is the level of detail the run produced, and inventing
per-row observations it never reported would be worse than saying so plainly.
The four rows carried over from July keep their July text with the closing
attestation appended to it.

| Evidence field | First run | Closing run |
| --- | --- | --- |
| Date | 2026-07-27 | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `8815a3c`; the later `d6818a8` is documentation-only and builds the identical binary | `b1152f7` |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 1. Launch the game | The window opens, agents render, and the match starts paused with tick unchanged. | Window opened; match started paused with the tick counter sitting still. | PASS |
| 2. Activate Play | The always-visible Play button advances ticks; Space provides the same toggle while the modal is closed. | Play advanced the ticks. The Space toggle was not exercised. 2026-08-11, tester at an interactive Windows desktop: re-run in full — the part left unexercised in July was exercised and the whole expected observation was seen. | PASS |
| 3. Activate Pause | The always-visible Pause button stops tick advancement and visibly indicates the paused state. | Pause stopped tick advancement and the paused state was visible on screen. | PASS |
| 4. Open Menu | The always-visible Menu button pauses the match and opens the modal; Escape toggles that same menu behavior. | The Menu button opened the modal. Escape as a toggle was not exercised. 2026-08-11, tester at an interactive Windows desktop: re-run in full — the part left unexercised in July was exercised and the whole expected observation was seen. | PASS |
| 5. Exercise modal commands | Modal Play resumes and closes; modal Pause remains open and paused; Escape closes without resuming; Exit Game, which is available only in the modal, requests one clean shutdown. | Exit Game quit the game cleanly. Modal Play, modal Pause, and Escape-closes-without-resuming were not exercised. 2026-08-11, tester at an interactive Windows desktop: re-run in full — the part left unexercised in July was exercised and the whole expected observation was seen. | PASS |
| 6. Select an agent | A primary click on a living agent pins the inspector with ID, faction, alive/dead state, health, intent, target, and position. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 7. Move away and observe death | Moving the pointer away does not clear selection; if the selected agent dies, the inspector remains pinned and shows its final `DEAD` state. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 8. Check observational behavior | Selecting or inspecting an agent does not alter tick progression or the deterministic battle result; an empty-arena click clears selection and UI clicks do not click through. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 9. Exercise event-log scrolling | At 1x and 4x, events remain ordered without duplicates and retain at most 200 rows. The wheel scrolls only the log while the pointer is over it and does not zoom the arena; new events do not steal an upward scroll position; returning to the bottom reveals the newest events; over the arena, the wheel zooms. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 10. Reach a terminal outcome | The match pauses and the summary winner, both survivor counts, terminal tick, simulated duration, and seed match the final status and visible arena state; the summary offers Next Round. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 11. Check score timing and team mapping | Team A is Blue/faction 0 and Team B is Red/faction 1. Reaching a victory does not change the score immediately; choosing Next Round adds exactly one win to that completed round's winner. Starting the next round after a draw or while the current round is ongoing adds no win. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 12. Exercise ordinary Next Round | `R`, modal Next Round, and summary Next Round each preserve the score, speed, and camera; clear selection, event history, scroll state, and summary; and leave the fresh round paused. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 13. Check seed progression | Each Next Round changes the seed to a distinct deterministic value. After Full Reset, repeating the same Next Round sequence produces the same seed sequence. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 14. Exercise Full Reset | After changing the score, speed, and camera, press `Shift+R`; both win totals become 0, seed returns to 1, speed returns to 1x, the camera fits the arena, disposable UI state clears, and the fresh round is paused. Change state again and confirm modal Full Reset has the same result. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 15. Close the window | The operating-system close button exits the process once with exit code 0. | Closing the window exited the game. The exit code was not captured, so the `0` half of this row is unproven. 2026-08-11, tester at an interactive Windows desktop: re-run in full — the part left unexercised in July was exercised and the whole expected observation was seen. | PASS |
| 16. Check the plains backdrop ground | The battle floor shows varied ground shading with scattered grass, dirt, and stone marks rather than one flat color. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 17. Check backdrop stability at zoom extremes | Zooming fully out and fully in keeps the ground pattern locked to the same patches of map; the pattern does not crawl or shimmer, and decals neither vanish into flicker nor balloon into large blobs. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 18. Check backdrop continuity while panning | Panning the camera across the map shows no seam lines, gaps, or overlapping bright edges between ground cells. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 19. Check readability over the backdrop | Pawn silhouettes, faction ground rings, selection marks, and hit effects all remain clearly readable against the new backdrop. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 20. Cycle every theme against the backdrop | Each theme produces a backdrop in its own palette, with the arena border still distinguishable from the ground. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 21. Check backdrop reseeding on Next Round and Full Reset | Pressing `R` for a new round changes the backdrop with the new seed; pressing `Shift+R` for a full reset returns the seed-1 backdrop identical to the first launch. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 22. Confirm the sound log is hidden by default | On launch, no sound panel is visible and the battle event log occupies the full height of the right column exactly as before. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 23. Toggle the sound log | The `Sounds` control-bar button and `F9` both open and close the sound panel; the button shows an active state while it is open; the right column splits with battle events above and the sound log below, and nothing else on screen moves. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 24. Check the expected-file list with an empty audio folder | With no files in `Content/Audio/`, the panel lists all thirteen expected file names, each marked `MISSING`, shows `MISSING 13/13`, and the game stays silent without errors. The list scrolls with the wheel, so all thirteen names are reachable even though only ten rows are shown at once. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 25. Add one sound file | Drop a PCM WAV named `death.wav` into `Content/Audio/`, relaunch, and confirm that slot reads `READY`, the counter drops to `MISSING 12/13`, and a death audibly plays with a `PLAYED` row in the cue log. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 26. Check an unusable file | Replace `death.wav` with a non-PCM file of the same name, relaunch, and confirm the slot reads `FAILED` rather than `MISSING`, and the game still runs silently for that slot. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 27. Exercise mute and rate limiting | With files present, the panel's `MUTE` toggle silences playback while still logging rows; during a busy tick the cue log shows collapsed `LIMITED xN` rows rather than one row per suppressed cue. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 28. Exercise sound-log scrolling and isolation | The wheel scrolls only the panel under the pointer — sound log, battle log, or arena zoom — and clicks inside the sound panel do not click through to the arena or clear the agent selection. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 29. Check sound-log reset behavior | `R` and `Shift+R` clear the cue log while leaving the expected-file list and its statuses unchanged. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 30. Open the Army Composition panel | Menu opens and the Army Composition button (between Next Round and Full Reset) shows the currently saved units-per-team and category counts in four steppers. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 31. Adjust a category count | Left and Right arrows on a stepper adjust its value; Shift+Left and Shift+Right adjust by 10 instead of 1. The Unassigned readout updates live. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 32. Check Unassigned reaches zero | Adjusting steppers such that category sum equals units-per-team displays Unassigned: 0. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 33. Verify Apply gate behavior | Apply is disabled (ActionDisabled style, dimmed glyph) while Unassigned != 0 and while the draft equals the saved composition; Apply is enabled exactly when balanced and changed. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 34. Check the staged banner | After pressing Apply, the panel closes, the menu shows a one-line notice stating the composition takes effect on the next Full Reset, and Apply remains disabled until a different composition is drafted and applied. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 35. Verify Full Reset fields the chosen army | After applying a composition and pressing Full Reset (or `Shift+R`), the arena resets and both factions field the number and distribution of warriors specified by the staged composition, visible in the agent inspector and event log. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 36. Observe blood at the default fit view | On first launch, with the default gore setting (Stylized) and the default camera fit, a landed blow shows a directional spray and a ground mark that are both plainly visible without zooming the camera in at all. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 37. Check spray direction | Select an agent, watch it get struck, and confirm the spray leaves the victim along the line running from the attacker to the victim — pointing away from the attacker, never back toward it. Confirm this holds for blows arriving from several different directions. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 38. Distinguish a lethal blow from a wound | A blow that kills its victim renders visibly differently from a blow that only wounds: the lethal tier is denser or longer-lived, and only the lethal blow leaves the ground mark described in row 39. A spectator can tell the two apart without reading the event log. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 39. Check ground-mark persistence and fade | A ground mark stays on the battlefield after the fighters involved have moved away, then fades out gradually over time rather than vanishing in a single frame. Marks accumulate where the fighting was heaviest instead of spreading evenly. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 40. Confirm gore Off draws nothing | With the gore setting on Off, no spray, spurt, or ground mark appears anywhere for any blow, including kills, at any camera zoom. The existing warm-white hit-effect ring still draws, so impacts remain readable. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 41. Change gore intensity via the menu | Open Menu; the Gore Intensity control cycles Off, Stylized, Full and wraps at both ends using Left and Right and the pointer arrows. Each choice visibly changes blow rendering: Off shows nothing, Stylized shows spray and a fading mark, and Full additionally shows a sustained spurt on a kill together with denser, longer-lived marks. The change takes effect immediately, without a restart. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 42. Reach the gore selector by keyboard | Inside the menu, `Tab`, `Down`, and `S` move focus from the theme selector through every button and land on the Gore Intensity selector as the final control in the order; continuing past it wraps back to the theme selector. `Up` and `W` reach it from the theme selector by wrapping backwards. While it is focused, Left and Right change the value and no button is activated. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 43. Reach the gore selector by pointer | Hovering the Gore Intensity selector highlights it without changing the value; clicking its previous and next arrows changes the value; and a click on the selector does not click through to the arena or activate any menu button. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 44. Check gore intensity persists across a restart | Set gore to Full, fully close the game, and relaunch it: Full is active from the first blow, without reopening the menu. Repeat with Off and confirm the same. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 45. Check blood clears on Next Round and Full Reset | With sprays and ground marks visible on screen, trigger Next Round (`R`, modal, or summary); all blood clears immediately alongside the event log, inspector, and summary. Repeat separately with Full Reset (`Shift+R` and the modal command) and confirm the same. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 46. Check blood readability across every theme | Cycle all six visual themes while blood is on screen. In every theme, including `datu-court` and `high-contrast`, blood stays clearly distinguishable from the Blue faction pawns, from the Red faction pawns, and from the arena ground surface; no theme makes a spray or a ground mark disappear into a pawn or the backdrop. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 47. Check speed and gore independence | At 1x, 2x, and 4x speed, switch gore between Off and Full and confirm the tick counter in the window title advances at the same visible rate for both settings at each speed. The gore setting never slows, pauses, or reorders simulation advancement. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 48. Confirm variants resolve | Press `F9`. Every attack slot reports `READY` with a per-class breakdown, and the counts match the files in `Content/Audio/`: 10 for each of the four attack slots, 10 for `death`. A class with no take of its own shows its real count rather than a fallback-inflated one. Scroll the expected-files list to the bottom: each of the four clash slots reports `READY` with four takes, sixteen takes across the four. Each weapon is its own slot, so a clash slot with no take shows its real count and no other weapon's takes are substituted for it. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 49. Hear the variation | Watch an unpaused battle for a full minute. Blows do not sound like one repeating sample: cuts to different parts of the body are audibly different, and the same weapon striking the same class does not always play the identical take. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 50. Confirm no human voice | Listen through a full battle including many deaths. No cue contains a scream, grunt, groan, or breath. Pay particular attention to `death-02`, `death-06`, and `death-07`, whose prompt wording carries the highest risk of an accidental vocalisation. Any file that vocalises must be regenerated before release. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 51. Check level consistency | No cue is obviously louder or quieter than its neighbours. The known-quiet takes — `attack-kampilan-ribcage-01`, `attack-kampilan-gut-01`, `attack-wasay-neck-01`, `death-02` — are audible under a busy battle rather than disappearing. Any that vanish need a re-roll. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 52. Verify a partial set falls back | Move one hit class's takes for a single weapon out of `Content/Audio/` and relaunch. That weapon still makes a sound on a hit to that body part, drawn from the fallback class, and the sound log shows the class as missing rather than the whole slot going silent. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |

The section carried this note on round scoring, recorded here as it stood:

For round scoring, record Team A (Blue) and Team B (Red) totals before and after
each command together with the outgoing outcome and old/new seeds. Next Round
scores only a terminal victory and always advances the deterministic seed.
Full Reset never scores the outgoing round.

## Collision readability smoke

**This family is complete: all seven rows `PASS`**, closed on the same
2026-08-11 run. The rows were added by the collision change, revised by the
contact-closing amendment, and amended again by the persistent-contingent
movement change (T18), which changed what rows 19, 20, 21, and 21a should be
expected to show: a second-rank agent's blocked label can read as gathering
toward its contingent rather than purely as blocked by the front rank, and it
can read as easing to a stop under the arrival taper rather than stopping dead.
Row 21 is the contact-closing amendment's whole visible effect — the
pre-amendment behaviour was a persistent gap of open ground between the two
lines — and it passed.

The automated gate, the benchmarks, and the collision regression tests proved
the rule is enforced. None of them proved the resulting battle line is legible
to a person watching it, which is the only thing these rows were ever for.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `b1152f7` |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 16. Read the battle line | Agents form a visible front instead of a shapeless blob, and the shape reads as a consequence of crowding rather than as a snapped grid. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 17. Look for stacking and jitter | No two living pawns visually occupy the same spot, and a pressed front settles instead of vibrating between positions tick after tick. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 18. Confirm combat continues | A packed front keeps dealing damage; the match does not stall into a standoff and reaches a terminal outcome inside its tick limit. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 19. Inspect a blocked agent | Selecting an agent in the second rank shows a movement label explaining why it is not advancing, and that label changes as the situation changes. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 20. Inspect the front rank | Selecting a front-rank agent shows it moving or attacking rather than blocked, and an agent that has arrived at an enemy reads as attacking rather than still marching. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 21. Confirm the ranks actually touch | Opposing front ranks close until their pawn bodies meet, rather than settling with a visible gap of open ground between the two lines. This is the amendment's whole visible effect and the pre-amendment behaviour was a persistent gap. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |
| 21a. Watch a contested push change hands | Added by the collision priority amendment. Select a second-rank agent pressed against the same enemy for a sustained engagement. Its movement label alternates between blocked and moving across ticks rather than reading blocked for the whole engagement, and neither faction's line is the one that always gives way. | 2026-08-11, tester at an interactive Windows desktop: run and observed exactly as the Expected column describes. | PASS |

