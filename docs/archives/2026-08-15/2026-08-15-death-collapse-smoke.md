# Death collapse and the prone body smoke — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

**This family closed in full.** All ten `DC` rows were run and passed by a
person at an interactive Windows desktop on 2026-08-15, so the family and its
section left the live checklist whole. The plan and design that created it were
archived on 2026-08-14 under the titles "Death collapse and the prone body —
plan" and "Death collapse and the prone body — design".

| Field | Value |
| --- | --- |
| Rows in family | 10 — `DC-1` through `DC-10` |
| Rows closed `PASS` and lifted here | 10 |
| Rows still open in the live checklist | 0 — the section was deleted |
| Written | 2026-08-14, when the change landed |
| Closed and lifted | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The feature landed at `0d4b34e` |
| Launch path | `./scripts/run.ps1` on an interactive Windows desktop |
| Optional screenshot paths | None recorded |

## What the family was for

A warrior that dies is held in its struck pose for the lethal hold, 0.34
seconds, exactly as before. Then it topples over about its own feet across 0.45
seconds, overshooting slightly as it lands and settling back, and it stays flat
on the ground for the rest of the battle. Before that change it turned grey and
kept standing. The crossed-out dead mark now draws at the lowest detail tier
only, and the corpse desaturation was softened from a 0.68 blend toward grey to
0.40, both because the prone silhouette carries the read that the colour and the
mark used to carry alone.

Two things were deliberately unchanged and are not defects: a corpse's weapon
stays in its hand and turns with the body, and the faction-tinted ground ring
under a corpse stays flat and unrotated, because it marks the ground rather than
the body.

What the automated work proved was that the collapse curve, the transform
algebra, the cull envelope's containment, the ordinal store, and the quad counts
behave as specified. Whether a body falling over reads as a death is not a
property a test can hold an opinion about, which is what these ten rows were
for.

## How the rows closed

The tester reported all ten rows passing and recorded no separate observation.
The `Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| DC-1 | Watch a single warrior die at the default camera station | It visibly topples over rather than changing colour in place | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-2 | Let a fallen body settle, then look at it | The body is flat on the ground — horizontal, head at ground level a body's length from the feet — not leaning and not tilted part of the way | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-3 | Watch several kills where the attacker's side of the screen is unambiguous | Each body falls away from the blow that killed it, not toward the attacker | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-4 | Let a cluster of casualties build up and look at the group | The bodies read as separate warriors lying at slightly different angles, not as one shape stamped repeatedly | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-5 | Pause playback while a body is mid-fall, wait, then resume | The body holds mid-fall while paused and continues from where it stopped on resume | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-6 | Watch a fight taking place beside earlier casualties | Corpses draw beneath the living and never occlude a fight in progress | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-7 | Pan the camera so a corpse sits at the arena panel's edge | The body stays drawn until it is genuinely off screen, rather than disappearing while part of it is still visible | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-8 | Run a 1,000-unit battle (500 per team) to a heavy casualty count | The corpse field is readable as a battlefield rather than visual noise, and the frame rate holds | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-9 | Zoom fully out, to the lowest detail tier, with casualties on the field | A dead warrior is still distinguishable from a living one — this is the tier where the crossed mark is the signal | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |
| DC-10 | Compare a corpse against a living warrior of the same faction at Medium and High tier | The corpse reads as dead rather than as a differently-dyed living warrior; the softened desaturation is enough | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **`DC-8` is not a pass for `GR-3` or `GR-5`.** It ran a 1,000-unit battle and
  judged the corpse field and the frame rate. The two open render rows ask
  different questions about the same battle size, and they are still open.
- **The `Actual` column is deliberately thin.** The tester gave verdicts and no
  narrative. No agent may enrich these cells later.
