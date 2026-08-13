# Tactical hit animations smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

Two of the family's nine rows did not leave with the rest and are still live in
the checklist. Read the "What did not close" section below before assuming this
family is done with.

| Field | Value |
| --- | --- |
| Rows in the family | 9 (numbered 90 to 98) |
| Rows closed `PASS` and lifted here | 8 |
| Rows still open in the live checklist | 2 — row 92, which never closed, and row 94, which closed and was reopened the same day |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time was `8da5d92` plus uncommitted documentation and auto-camera changes |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## What these rows were for

The system under test lives entirely in `Hukbo.Client`, so it cannot reach the
simulation by construction. `HitEffectSystemTests.cs` and
`HitEffectGeometryTests.cs` already proved that the effect buffer has a fixed
capacity and replaces its oldest entry in a defined order, that ordinary and
lethal effects expire on their stated schedules, that each damage event produces
exactly one effect, and that a reset clears every effect.

None of that proves that a hit reads as a hit to a person watching the screen,
or that the effects stay legible when the fighting gets crowded. That is the
only thing these rows were ever for, and it is why only a person at an
interactive Windows desktop could close one.

## The rows that closed

The tester reported the run as a whole rather than row by row: rows 90 through
98 were run, and every row except 92 passed. No separate observation was
recorded for any individual passing row, so the `Actual` column below says
exactly that and no more. Nothing here should be read as a detailed finding that
was never made.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 90. Read an ordinary hit at 1x | At normal speed a non-lethal blow produces a brief pulse on the struck pawn, one thin ring, and a small restrained shard burst. The blow is unmistakable without the screen filling with debris. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |
| 91. Check hits survive 4x | At 4x, hits landed on consecutive simulation ticks are each still visible, rather than only the last tick's hit appearing in each drawn frame. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |
| 93. Check readability across the zoom range | At fitted, minimum, and maximum zoom the primary ring stays readable. Zooming out reduces clutter without removing the ring, so a hit is never invisible at any zoom the spectator can reach. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |
| 94. Watch a crowded exchange | With many pawns trading blows at once the effects stay bounded. No persistent trail, smear, or lingering colour builds up on the arena, and the fighting stays legible underneath. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. **Reopened the same day** — see below. | PASS, then reopened |
| 95. Pause and resume | Pausing lets effects already on screen finish while the simulation stops advancing. Resuming produces new effects normally, with no burst of stored-up effects on the first frame. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |
| 96. Reset clears everything | Next Round (`R`) and Full Reset (`Shift+R`) both clear every pulse and burst immediately. No effect from the previous match survives into the new one. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |
| 97. Check the arena edges | Resize the window and zoom in near each arena edge. No ring or shard draws over the status bar, the agent inspector, the event log, the match summary, or the menu overlay. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |
| 98. Confirm the effects change nothing | Run to a terminal result. Effects expire on their own, and the outcome, tick count, state hash, and event hash match a run of the same seed with the effects never observed. | 2026-08-13, tester at the desktop. Run as part of the whole family; passed, with no separate note recorded. | PASS |

## What did not close

**Row 92, "Tell a lethal hit apart", never passed.** It asked whether a killing
blow reads as clearly heavier than an ordinary one. The tester's finding was
that it does not: *"it's not extremely clear, we need improve this so i can
really see, more blood and gore"*. The row stayed in the live checklist and a
change was designed and built in response the same day. That row is not this
file's business and must not be read out of here.

**Row 94 closed and was reopened the same day.** It passed against the effect
values that were shipping when the tester ran it. The change made in response to
row 92 raises the number of primitives a kill draws — more droplets, more
shards, longer-lived blood, and a heavier default gore level — so the crowded
exchange row cannot be assumed to still hold. It went back into the live
checklist as an explicit re-run rather than being quietly carried as passing.
Its `PASS` above is a true record of the older build and is not evidence about
the current one.

## What a later reader should be careful of

- **The eight rows above passed against the pre-2026-08-13 effect values.** If
  the hit or blood presentation is retuned again, they say nothing about the new
  values. Rows 93, 94, and 97 in particular are sensitive to effect size and
  count.
- **The `Actual` column here is deliberately thin.** The tester gave one verdict
  for the family and one finding on one row. No agent may enrich these cells
  later; an invented observation is worse than a thin one.
- **Row 98 is the only interactive check that the effects do not touch the
  simulation.** It passed, and the change built in response to row 92 stayed
  entirely inside `Hukbo.Client` for exactly that reason. A future change that
  reaches into `Hukbo.Core` invalidates that pass.
