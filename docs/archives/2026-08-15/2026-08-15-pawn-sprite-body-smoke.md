# Pawn sprite body smoke — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

**This family closed in full, on the day it was written.** All eight `SB` rows
were added on 2026-08-15 when the feature merged at `21e1abb`, and all eight
were run and passed by a person at an interactive Windows desktop the same day,
so the family and its section left the live checklist whole. The plan and design
that created it were archived on 2026-08-15 under the titles "Pawn sprite body —
plan" and "Pawn sprite body — design", alongside this record.

| Field | Value |
| --- | --- |
| Rows in family | 8 — `SB-1` through `SB-8` |
| Rows closed `PASS` and lifted here | 8 |
| Rows still open in the live checklist | 0 — the section was deleted |
| Written | 2026-08-15, when the package landed |
| Closed and lifted | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The feature landed at `21e1abb` |
| Launch path | `./scripts/run.ps1` on an interactive Windows desktop |
| Optional screenshot paths | None recorded |

## What the family was for

The package draws each warrior's head and torso from an authored sprite atlas of
fifty body cells instead of from flat quads. The mode is off by default and is
switched with the `B` key, which nothing on screen announces, so every row began
by pressing `B`.

Rows `SB-5` through `SB-8` existed because the sprite replaces only the head and
the torso. A failure in any of them would have meant the seam described in the
design's section 3 was broken and the sprite was covering something it was never
meant to cover: the legs, the arms, the weapon, or the shield.

## How the rows closed

The tester reported all eight rows passing and recorded no separate observation.
The `Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SB-1 | Start a battle, let it run, press `B`, then press `B` again | Every warrior's head and torso changes to drawn art on the very next frame, and changes back on the second press. No stall, no flicker, and the battle does not pause or restart | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-2 | Press `B` to enable the mode, quit the game, relaunch | The game comes back up still drawing sprite bodies, because the choice was persisted | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-3 | With sprite bodies on, watch a full engagement at the default camera fit and say whether the two sides stay tellable apart | Team A still reads blue and Team B still reads red at a glance, without relying on the selection boxes. The faction wash is a provisional tuning value at 0.32 | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-4 | Zoom in on one contingent and compare warriors side by side | Warriors visibly differ from one another — skin tone, headband colour and presence, hair, facial hair, tattoos, build. They do not all share one body | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-5 | Watch a warrior die with sprite bodies on | The drawn body rotates and falls with the collapse, staying attached to the legs. It does not stay standing upright while the rest of the pawn falls | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-6 | Watch a warrior walk and run with sprite bodies on | The legs still animate underneath the drawn torso. Gait is unaffected by the mode | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-7 | Watch a warrior fight with sprite bodies on | The weapon arm still swings and still points at the target, drawn over the body rather than under it. The shield still sits in front of the torso | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| SB-8 | Zoom all the way out until pawns resolve the `Low` detail tier | The body falls back to the procedural quads with no flicker and no gap at the changeover. This is expected behaviour, not a defect | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **The discoverability gap is not closed by these passes.** The `B` toggle has
  no on-screen announcement; the menu panel is full, and the design's section 9
  records that as an open gap rather than a finished control. Every row here was
  run by a tester who already knew the key.
- **`SB-3` and `SB-4` asked for recorded detail that was not supplied.** `SB-3`
  asked for a note if the sides blurred together, and `SB-4` asked roughly how
  many distinct bodies could be picked out. Neither answer exists; both rows
  carry a pass verdict only. **No agent may enrich these cells later.**
- **The 0.32 faction wash is a provisional tuning value.** `SB-3` passing means
  a person found the sides tellable apart at that value on that display, not
  that the value is calibrated.
