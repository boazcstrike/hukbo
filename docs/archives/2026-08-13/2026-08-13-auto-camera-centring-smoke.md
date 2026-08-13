# Auto camera centring smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file is outstanding and nothing in it is an instruction.

This record closes the family whole. `docs/development/smoke-checklist.md` held
a section titled "Auto camera centring smoke (2026-08-12)" with a single row,
`AC-1`. It passed on 2026-08-13 and the section was deleted the same day.

| Field | Value |
| --- | --- |
| Rows | 1 |
| Source family | 1, closed whole |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | Not recorded by the tester. The behaviour under test shipped as `cb81fa3` |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## Why this row existed at all

It was not a new feature's row, and it was not a re-run of one that closed.

The auto camera modes family — rows 149 through 155 — was run by a person at an
interactive desktop on 2026-08-12 and every one of its seven rows passed. Its
record is the 2026-08-12 archive titled **"Auto camera modes smoke — closed
2026-08-12"**. Row 149 passed, but the tester's own passing report named a
separate problem the row had never stated as a criterion:

> "yes, this is good; but usually it is not centered; and fighting usually
> stays at the corner of the screens; we need to fix to center, not when a
> battle happens, only when panning from an empty on-fight screen"

That is a different claim from the one row 149 tested, so it became a fresh row
rather than a reopening of a closed one. `AC-1` is that fresh row, and for the
day it existed it was the only row in the live checklist that was a first check
rather than a re-run of something already seen.

The cause was measured rather than guessed. A pan ended as soon as any fighter
reached seventy per cent of the visible half-extent, which put the fight 13.78
world units off centre on a 20-unit half-extent — comfortably on screen, and
nowhere near the middle of it. The fix separated the two numbers that had been
sharing one constant: Follow mode keeps its on-screen band at `0.7`, now named
`FollowOnScreenFraction`, and a pan that began from an empty screen ends against
a new `CenteredFraction` of `0.2`. It shipped on 2026-08-13 as commit `cb81fa3`.

## Auto camera centring — the closed row

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| AC-1. Confirm a pan lands the fight near the middle | Pan away from every fight until the screen holds no fighting at all, let the assistant take over, and watch where the camera stops. The melee ends up near the middle of the screen — within roughly a fifth of the way from centre toward the edge — rather than pinned in a corner. Then check the other half: zoom in on a fight that is already on screen and leave the camera alone. It must not re-centre that one. Only a pan that began from an empty screen ends centred. | 2026-08-13, tester at the desktop, re-running the row against the fix: "good". No separate note was recorded for either half of the check | PASS |

## What this row proved, and what it does not

It proved that the behaviour the tester complained about on 2026-08-12 is gone
as far as a person watching the screen is concerned. That is the whole point of
the row: `ArenaAutoPanTests` already proved that a pan begun from an empty
screen ends with the melee inside `CenteredFraction` on both axes, and that a
fight already on screen is left alone, and neither of those assertions can say
whether the result looks centred to somebody watching a battle.

It does not carry a per-half verdict. The row asks two things — that a pan from
an empty screen ends centred, and that a fight already on screen is *not*
re-centred — and the tester answered with one word covering both. A later
question about the second half specifically needs a fresh row rather than a
reading of this pass.

It is also not evidence about Follow mode. `AC-1` was written about the
assistant taking over from an empty screen, which is Assisted mode's pan;
Follow mode's own on-screen band was renamed by the same change but was not
altered, and the row that watched Follow mode is row 151 of the family that
closed on 2026-08-12.

## Provenance

The row wording in the table above was read from
`docs/development/smoke-checklist.md` before the section was deleted. The
verdict comes from the person at the interactive desktop on 2026-08-13. No agent
flipped the row, and no automated test was treated as evidence for it.

The plan and design documents behind the fix are archived under the titles
**"Auto camera centring — plan"** and **"Auto camera centring — design"**. The
plan's `AC-T6` section records the canonical gate run at `c15ca63` that covered
this change, and states plainly that the same tree carried five other changes,
so that gate is evidence about the six together rather than about this one
alone.
