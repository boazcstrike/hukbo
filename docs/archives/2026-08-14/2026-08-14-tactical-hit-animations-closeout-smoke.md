# Tactical hit animations smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

This is the second and final record for this family. Seven of its nine rows were
lifted out on 2026-08-13 into the record titled **"Tactical hit animations smoke
— closed 2026-08-13"**, which is named here in prose rather than linked. The two
rows that stayed behind — row 92, which had never passed, and row 94, which had
passed and was reopened the same day — were both re-run by a person on
2026-08-14 and both passed. The family now stands at 9 of 9 and its section has
been deleted from the live checklist.

| Field | Value |
| --- | --- |
| Rows in the family | 9 (numbered 90 to 98) |
| Rows closed in the earlier record | 7 |
| Rows closed here | 2 — row 92 and row 94 |
| Rows still open anywhere | 0 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time was `7036490` plus uncommitted documentation changes |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## What these two rows were for

Row 92 asked whether a killing blow reads as clearly heavier than an ordinary
one, and row 94 asked whether a crowded exchange stays legible and bounded. The
automated tests around the hit effect system — `HitEffectSystemTests.cs` and
`HitEffectGeometryTests.cs` — prove that the effect buffer has a fixed capacity,
replaces its oldest entry in a defined order, expires ordinary and lethal
effects on their stated schedules, produces exactly one effect per damage event,
and clears on reset. None of that can tell anyone whether a kill looks like a
kill, which is the only thing these two rows were ever for.

## The rows that closed

The tester reported both rows as passing and recorded no separate observation
for either. The `Actual` column below says exactly that and no more. Nothing
here should be read as a detailed finding that was never made.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 92. Tell a lethal hit apart | A killing blow reads as clearly heavier than an ordinary one, and reads that way without the spectator having been told where to look. The pawn is still on screen for the loudest part of its own death, the blow marks the body it killed, and the blood is heavy enough that a kill is never mistaken for a graze. | 2026-08-14, tester at the desktop, re-running the row against the lethal-blow legibility change. Passed, with no separate note recorded. | PASS |
| 94. Watch a crowded exchange | With many pawns trading blows at once the effects stay bounded. No persistent trail, smear, or lingering colour builds up on the arena, and the fighting stays legible underneath. | 2026-08-14, tester at the desktop, re-running the row against the same change. Passed, with no separate note recorded. | PASS |

## What row 92 was re-run against

Row 92 failed on 2026-08-13 with the finding *"it's not extremely clear, we need
improve this so i can really see, more blood and gore"*. The change built in
response was designed and planned the same day, in the two documents titled
**"Lethal blow legibility — design"** and **"Lethal blow legibility — plan"**,
both archived on 2026-08-14 and named here in prose. Four causes were measured
rather than guessed:

- the pawn was removed after `0.10` seconds while its death ring lived `0.28`
  and its blood burst `0.42`, so most of a kill drew over bare ground;
- lethal blows were the only blows excluded from the hit pulse, so a kill never
  marked the body it killed;
- the lethal and ordinary ring colours were eleven units apart in a single
  channel;
- the default gore level produced no sustained blood at all.

All four were changed, and the change stayed entirely inside `Hukbo.Client`.

Row 94 was reopened on 2026-08-13 precisely because that change raises the
per-kill droplet cap from 8 to 12, lengthens every lethal blood lifetime, and
makes `Full` the default gore level. There is no total-screen quad ceiling in
the effect code to appeal to, only per-record caps, so the question could only
be settled by a person looking at a crowded fight. That is what the pass above
records.

## What a later reader should be careful of

- **These two passes describe the build as it stood on 2026-08-14.** If the hit
  or blood presentation is retuned again, they say nothing about the new values.
  Both rows are directly sensitive to effect size, count, and lifetime.
- **The `Actual` column is deliberately thin.** The tester gave a one-word
  verdict on each row. No agent may enrich these cells later; an invented
  observation is worse than a thin one.
- **Row 98, in the earlier record, is the only interactive check that these
  effects do not touch the simulation.** The lethal-blow change stayed inside
  `Hukbo.Client` for exactly that reason. A future change that reaches into
  `Hukbo.Core` invalidates that pass, and these two rows with it.
