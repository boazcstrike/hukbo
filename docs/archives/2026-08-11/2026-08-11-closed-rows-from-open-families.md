# Closed rows lifted out of families that are still open — 2026-08-11

**Archived: reference only.** Every row here is `PASS` and was moved out of
`docs/development/smoke-checklist.md` on 2026-08-11. Nothing here is
outstanding and nothing here is an instruction.

These rows differ from the completed families archived beside them in one way:
their own section is **not** finished. Each family below still holds `PENDING`,
`FAIL`, or `BLOCKED` rows in the live checklist, and those rows stayed there.
Only the closed ones left, so that the live file lists work still to do rather
than work already done. The live checklist remains the source of truth for what
is outstanding; this file is the record of what a person saw when these
particular rows closed.

Do not re-run a row from this file. If a later change touches the behaviour a
row describes, write a fresh row in the live checklist instead.

| Field | Value |
| --- | --- |
| Rows | 22 |
| Source families | 4 |
| Lifted on | 2026-08-11 |
| Live checklist | `docs/development/smoke-checklist.md` |

Two of the four families closed their rows under conditions the shipped build
no longer has, and a reader treating an undated `PASS` as current would be
misled by both. The persistent-contingent row closed under
`PersistentContingentsV2`, and the shipped movement preset is now
`BattlefieldRealismV10`. The attack-animation rows were observed at a
fullscreen 2048x1152, which was the virtualised viewport a DPI-unaware process
was handed on that display; the 2026-08-11 DPI awareness fix means the same run
today is at 2560x1440. Where a row turned on legibility, write a fresh row
rather than trusting the one below.

## Sandata smoke (design section 13)

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-3 | Send a squad through the entry door and on into the room behind it | The squad visibly collapses to single file at the door and re-expands inside | 2026-08-11, tester at the desktop: "single file" — the collapse at the door was observed. The re-expansion inside the room was not separately reported, so only the first half of the expected observation is evidenced. | PASS |
| SD-6 | Look at a fire cone at every detail tier, zoomed in and out | The cone reads at every tier and does not fade with zoom | 2026-08-11, tester at the desktop: "readable but not understandable". The row's literal criterion — the cone stays visible at every tier and does not fade with zoom — was met. That it does not communicate *what it means* to a viewer is a real separate finding and is recorded below the table, not folded into this row's status. | PASS |

## Responsive menu, startup display, and UI motion smoke

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| UI-1. Minimum-size menu containment | At 1024x720 and UI Scale Auto, the complete two-column menu remains inside the window; its 12 controls, labels, arrows, and helper text neither overlap nor clip. | 2026-08-11, tester at the desktop: the whole two-column menu stayed inside the window with nothing overlapping or clipped | PASS |
| UI-3. Tall-window layout | At 1440x1920, the menu and HUD remain contained and readable without stretched text or misplaced pointer hit targets. | 2026-08-11, tester at the desktop: contained and readable, no stretched text, hit targets landed where they were drawn | PASS |
| UI-5. Windowed startup | Select Windowed, close the game fully, and relaunch. It opens at 1280x720, cannot be resized below 1024x720, and all UI remains contained. | 2026-08-11, tester at the desktop: Windowed persisted across a full close and relaunch, the minimum size held, and the UI stayed contained | PASS |
| UI-7. Keyboard traversal | Open Menu and use Tab, Shift+Tab, W/S, and Up/Down. Focus visits the theme selector, six action buttons, gore, motion, auto camera, UI scale, and startup display exactly once before wrapping. Left/Right changes only the focused selector. | 2026-08-11, tester at the desktop: focus visited every control once and wrapped, and Left/Right moved only the focused selector | PASS |
| UI-8. Motion Off | Select Motion Off. Hover, focus, and press menu and HUD buttons: state changes are immediate, with no animated positional movement, while hit targets remain stable. | 2026-08-11, tester at the desktop: state changes were immediate, nothing animated its position, hit targets held | PASS |
| UI-9. Motion Reduced | Select Motion Reduced. Hover, focus, and press buttons: color transitions remain gentle, no control shifts position, and the setting takes effect immediately. | 2026-08-11, tester at the desktop: colour transitions stayed gentle, no control moved, and the setting applied without a restart | PASS |
| UI-10. Motion Full | Select Motion Full. Hover and press buttons: transitions ease smoothly and a pressed control moves by no more than one active-scale pixel without changing its clickable bounds. | 2026-08-11, tester at the desktop: transitions eased smoothly and a pressed control moved within its bounds without shifting where it could be clicked | PASS |
| UI-11. Cebu 1521 Court theme | Select `Cebu 1521 — Provisional` and confirm the selector label reads `PROVISIONAL RECONSTRUCTION`. The restrained dark hardwood, woven-fibre, warm metal, soot-black, and textile-red palette reads as a provisional early-contact chiefly-court interpretation rather than a generic European-medieval or modern national design; text and faction signals remain legible. | 2026-08-11, tester at the desktop: the selector label read `PROVISIONAL RECONSTRUCTION`, the palette did not read as European-medieval or modern-national, and text and faction signals stayed legible. Every criterion this row states was met. The tester separately dislikes how the theme looks; that is recorded as finding 2 rather than folded into this row's status, because the row asks what the palette reads as and not whether the reader wants to use it | PASS |
| UI-12. Battle event log new-event accent | With a battle running and the event log panel visible, let a new event append while the log is on screen. At Motion Off, the new row's text renders in its final new-event accent colour immediately, with no colour fade. At Motion Reduced and Motion Full, the row's text eases from the new-event accent colour back toward the normal text colour over roughly 200 ms, and the two intensities look identical to each other. Row order, row height, and every other row are unaffected at every intensity. | 2026-08-11, tester at the desktop: the accent snapped at Motion Off and eased back at both Reduced and Full, the two looked identical, and no other row was disturbed | PASS |
| UI-13. Selected-agent inspector accent | Select an agent, open the agent inspector, then select a different agent while the inspector stays open. At Motion Off, the inspector's accent updates to the newly selected agent immediately, with no colour fade. At Motion Reduced and Motion Full, the accent eases in from the emphasis colour over roughly 160 ms before settling, and the two intensities look identical to each other. Re-selecting the agent that is already selected does not retrigger the accent. | 2026-08-11, tester at the desktop: the accent updated instantly at Motion Off and eased in at both Reduced and Full, and re-selecting the same agent did not retrigger it | PASS |
| UI-14. Selector arrow and active-marker interpolation | In the menu, hover the pointer over a selector's previous and next arrows (theme, gore, motion, auto camera, or UI scale) and change the selector's value. At Motion Off, the hovered arrow's highlight and the active-value marker snap instantly with no fade, and hit targets are unaffected. At Motion Reduced and Motion Full, the hovered arrow eases toward its highlighted colour and the marker eases toward its emphasis colour over the selector's pulse duration, and the two intensities look identical to each other. Moving focus without changing the selector's value does not retrigger the marker pulse. | 2026-08-11, tester at the desktop: arrows and the active marker snapped at Motion Off and eased at both Reduced and Full, hit targets were unaffected, and moving focus alone did not pulse the marker | PASS |
| UI-15. Control-bar active strip | On the control bar, toggle play/pause and change the simulation speed. At Motion Off, each button's active strip snaps instantly to its active colour at its existing six-pixel width. At Motion Reduced and Motion Full, the strip's colour eases from the inactive border colour toward the active colour over roughly 120 ms when a button becomes active, and eases back when it deactivates; the two intensities look identical to each other. The strip's width and the button's hit target never change at any intensity. | 2026-08-11, tester at the desktop: the strip snapped at Motion Off and eased both ways at Reduced and Full, and its width and hit target never moved | PASS |
| UI-16. Status-badge emphasis (one-shot, non-looping) | Cause the battle outcome to change, or toggle play/pause so the playing flag changes, and watch the status badge for several seconds afterward. At Motion Off, the badge's fill snaps to the new state's colour immediately with no pulse. At Motion Reduced and Motion Full, the badge briefly pulses toward its emphasis colour and settles back within roughly 450 to 650 ms, and the two intensities look identical to each other; the badge does not pulse again on its own while the state stays unchanged. Toggling the same state change again triggers a fresh, single pulse each time. | 2026-08-11, tester at the desktop: the badge snapped at Motion Off, pulsed once and settled at both Reduced and Full, never pulsed again on its own, and pulsed afresh on each repeat toggle | PASS |

## Persistent contingent smoke

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| 111. Confirm the battle still resolves | A full 200-agent battle reaches a terminal outcome. Neither side stands gathered and unmoving until the tick limit. | The battle reached a terminal outcome and a winner was declared. | PASS |

## Attack animation V2 smoke (2026-08-08)

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| AA-1 | Watch a Kampilan warrior strike at close zoom | The broadest of the four arcs, both hands on the blade, a planted weight transfer | Reads as the broad one. Confirmed as part of the four-way comparison rather than in isolation. | PASS |
| AA-2 | Watch a Wasay warrior strike at close zoom | The head arrives late and stops hard; the support hand anchors the haft; the longest recovery of the four | Reads as the late, heavy one. Confirmed as part of the four-way comparison rather than in isolation. | PASS |
| AA-3 | Watch a Kalis warrior strike at close zoom | A mostly linear extension toward the target rather than a broad cut, with the fastest return | Reads as the linear one. Confirmed as part of the four-way comparison rather than in isolation. | PASS |
| AA-4 | Watch an Itak warrior strike at close zoom | The shortest, quickest chop, alternating side between consecutive blows | Reads as the short one. The combo side alternation was not separately confirmed. | PASS |
| AA-6 | Watch a blow that lands | The weapon reaches the named target, blood and the defender's recoil arrive on the same frame as the weapon | The blow lands on the warrior it names, with blood and recoil on the same frame. | PASS |
| AA-17 | Pause on the frame of a contact | The pose, the effect, the reaction, and the sound freeze together; nothing advances while paused | Everything freezes together. Three pause/resume cycles during combat, at ticks 94, 119 and 147. | PASS |
