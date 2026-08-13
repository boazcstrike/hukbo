# Last-stand engagement smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file overrides either of those.

This record lifts a single row, `LS-1`, which was the whole of a one-row family
in `docs/development/smoke-checklist.md` titled "Last-stand engagement smoke
(2026-08-13)". The row was written on 2026-08-13, on the day a six-row
last-stand formation family ran and closed `PASS` in full and was itself lifted
out of the live checklist. That earlier family's record is the 2026-08-13
archive titled "Last-stand formation smoke — closed 2026-08-13", named here
rather than linked. `LS-1` existed because the tester's report on row 76 of
that family named a real problem no row in it stated as a criterion: the final
survivors still fought as a sequence of one-on-one duels rather than as a
group.

The row closed one for one on 2026-08-14, and the section was deleted whole
from the live checklist on the same day. Nothing here is outstanding and
nothing here is an instruction. If a later change touches the rally behaviour,
the last-stand threshold, or the movement preset the client selects, write a
fresh row in the live checklist rather than reviving this one.

| Field | Value |
| --- | --- |
| Rows | 1 |
| Source family | 1 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

## Evidence — 2026-08-14 closing run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Last-stand engagement smoke

The row below is reproduced as it stood in the live checklist, including the
`Actual` column, which kept the original 2026-08-13 observation so that the
re-run would be judged against what was actually seen rather than against a
summary of it. The only text added here is the final closing sentence, which
records the 2026-08-14 result that flipped the status.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| LS-1. Confirm the last stand ends as a group fight | Let a full 200-agent battle run to its final handful of warriors on each side and watch the last engagement. Several warriors from each side are in contact at once, so the ending reads as two small bands colliding. Failure is the survivors gathering correctly and then fighting one pair at a time, with the rest standing off and waiting their turn. | 2026-08-13, tester at the desktop, reporting on row 76 of the family that closed the same day: "passed, but not extremely clear. Since I am still seeing 1v1 in the endgame." The cause was measured rather than guessed and is stated below: a follower's aim point is 51 world units behind its rally agent, against a longest melee reach of 16, so only the rally agent ever reaches an enemy. **Fixed the same day** by `MovementPresetId.LastStandEngagementV11`, which the client now selects: a follower stops regrouping and closes on its own enemy once its rally agent is within its own weapon reach of an enemy, or once the follower's own enemy is within its own reach. Back to `PENDING` because only a person watching a final engagement can say whether it now reads as two bands colliding. **Closed 2026-08-14:** a person at an interactive Windows desktop re-ran the row against the shipped build through `./scripts/run.ps1` and passed it. | PASS |

## The measured cause the row was written against

This section carries the live checklist's "Finding" subsection over whole. It
is retained because it is the only written record of why the row existed and of
what the fix had to overcome.

### Finding — followers park three weapon-lengths behind the warrior they gathered on

A regrouping follower does not aim at its rally agent. It aims at a point
`RallyTrailRadiusMultiplier` body radii behind that agent, on the far side from
the enemy the agent is closing on, and it stops on arrival. The multiplier is 12
(`src/Hukbo.Core/Simulation/FormationRules.cs:188`) and the default body radius
is 4.25 world units (`src/Hukbo.Core/Simulation/CollisionRules.cs:72`), so the
aim point sits 51 world units behind the leader. The longest melee reach in the
shipped combat preset is 16
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:188`).

The rally agent is exempt from regrouping
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1426`), so it alone closes and
fights. Both factions do this symmetrically once each is at or below the default
threshold of six living agents, so two rally agents meet and duel while every
other survivor holds station out of reach; when one falls, the next-lowest
living `EntityId` takes over and the same duel repeats. The behaviour is
deterministic and does not depend on the seed.

**Lowering the trail alone is not a fix.** `FormationRules` requires
`RallyTrailRadiusMultiplier` to exceed `RallyJitterRadiusMultiplier * sqrt(2) + 2`
(`src/Hukbo.Core/Simulation/FormationRules.cs:180-188`), which with the jitter
multiplier at 6 puts the floor at about 10.49 — so 12 is already close to it,
and the jitter has to come down with it. Both constants reach the state hash, so
any change here is an authoritative simulation change needing a new preset
version and re-recorded golden expectations under `CLAUDE.md` section 5.

## What was actually changed

Because the constants could not simply be lowered, the repair was made as a new
movement preset rather than as a retune of the existing ones. The preset is
`MovementPresetId.LastStandEngagementV11`. Under it, a follower stops regrouping
and closes on its own enemy in either of two situations: when its rally agent is
within the follower's own weapon reach of an enemy, or when the follower's own
enemy is already within the follower's own reach. The change merged on
2026-08-13 at `d17c8a3`, and the client selects the new preset as its shipped
default through `ClientSettingsStore.DefaultMovementPreset`. That last part is
what made the fix reach a player at all: a preset nobody selects changes
nothing on screen.

## How the row closed

On 2026-08-14, a person at an interactive Windows desktop passed the row. The
launch path in the evidence table above is the one the live checklist already
carried for this row — from source, through `./scripts/run.ps1` — and is
recorded as such rather than as a fresh observation of how this particular run
was started. No machine identification, source commit, screenshot, or written
description of what was seen was recorded with the run, and those fields are
left as "Not recorded" rather than reconstructed after the fact.

## What this pass does and does not prove

The verdict recorded on 2026-08-14 is a pass, and nothing more than a pass. No
separate written observation was captured describing how many warriors were in
contact at once, how long the final engagement lasted, or how the two bands met.
The row's own criterion — that several warriors from each side are in contact at
once, so the ending reads as two small bands colliding rather than a queue of
duels — was judged satisfied by the person watching it, and that judgement is
the entire evidence this file carries.

A later question about the exact shape of the ending therefore needs a fresh
row rather than a reading of this one. Two examples that this pass cannot
answer: whether followers now over-commit, abandoning the gather too eagerly and
arriving piecemeal; and whether any yielding or give-way behaviour changes
anything in the moments before the last stand triggers at all. Neither was
observed, neither was written down, and neither can be inferred from a single
pass verdict.

The observation was made under `MovementPresetId.LastStandEngagementV11`, the
preset the client selects as its default. A later reader should not assume this
pass says anything about the behaviour under any earlier movement preset, all
of which shared the unversioned last-stand code the finding above describes.

## Where the plan and the design live

The two documents behind this work stay in `docs/plans/` rather than joining
this archive batch. They are `docs/plans/2026-08-13-last-stand-engagement.md`,
the twelve-task plan, and
`docs/plans/2026-08-13-last-stand-engagement-design.md`, the design that
measured the cause and offered three candidate remedies.

They stay live because they are cited by path from shipped source and test
files: `src/Hukbo.Core/Movement/MovementPresetId.cs`,
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs`,
`src/Hukbo.Client/ArenaGame.cs`, and
`tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs` all name one or both of them.
The rule in `docs/plans/README.md` keeps a source-cited document in that folder
however finished the work is, because this archive folder is pruned
periodically and a citation into it would become a broken path.
