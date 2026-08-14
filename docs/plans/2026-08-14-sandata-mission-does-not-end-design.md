# Sandata: the shipped mission freezes at first contact — design

Opened 2026-08-14, immediately after the lowered-weapon and automatic-fire
package. That package made the path search read the baked map for the first
time, so operators stopped walking through walls and started walking the route
the map author drew. This document is what happened when the shipped mission was
then run for three thousand ticks and watched.

It was opened expecting to design a fix for a doorway deadlock. There is no
doorway deadlock. What there is instead is worse, and nothing in the repository
had recorded it.

## 1. What was measured

A temporary harness built exactly what `SandataGame`'s constructor builds — the
`angle-house` map parsed and validated, a `NavGrid` baked from its walls and
doors at body radius 5, wall buckets over the same wall records, the four real
spawns, and `InitialSquadGroups.Build` for the assaulting squad — and ticked it
3,000 times, sampling every operator every 100 ticks. The harness was deleted
once its numbers were recorded; the numbers are below.

The mission has four operators: two assaulting (entity 1 with a rifle, entity 2
with a pistol, spawning together at the bottom of the map) and two defending
(entity 3 at `(120, 520)`, entity 4 at `(500, 120)`, both standing on their
spawns).

| Tick | What the run is doing |
| --- | --- |
| 0 | Everyone `Hold`. The squad has one group with a published-path request outstanding. |
| 100–600 | The squad walks. It crosses the authored doorway, turns west, then north, then east along the top of the map. It never once touches a wall. **The pathfinding fix works.** |
| 604, 610 | Both attackers go weapon-lowered near a wall and stay lowered for the rest of the run. |
| 657–672 | Entity 4 fires four rounds, all hits, at entity 1. |
| ~676 | Entity 1 dies at `(421, 120)`. |
| 676–3000 | **Nothing happens again, ever.** |

The last event on the feed for the whole mission is at tick 672. From tick 676
to tick 3000 — forty-six seconds of wall-clock play — not one event is emitted,
not one operator moves, and not one health value changes.

What each survivor is doing during those 2,300 ticks:

- **Entity 2**, alive at full health, stands at `(412, 119)`. Its selected
  intent is `Hold / HoldingPosition`. It is **88 world units from a living,
  shooting hostile** — inside `ContactMemory.IdentifyRangeWu`, which is 96 —
  and its pistol's `SingleBandMaxWu` is 320, so the range is not the problem. It
  never engages, never advances, never repositions, and is never shot at.
- **Entity 4**, the defender that just killed entity 1, holds intent `Engage`
  and cycles its weapon chain `Aiming → Firing → Resetting → Aiming` forever.
  **It emits no `ShotFired` event after tick 672.** It is running the whole
  firing machine against a target that is already dead.
- **Entity 3** never moves and never selects anything but `Hold` for the entire
  run. The squad walked past it and neither noticed the other.
- `MissionState.Winner` is never set. No outcome resolves. The mission has no
  end condition it can reach.

## 2. Four defects, each verified against source

### 2.1 `OperatorState.Intent` is never written

`IntentSelection.SelectAll` runs every tick as stage 8 and its results are
correct — the measurement above reads them from `SandataSimulation.PendingIntents`
and they say `Advance` while walking, `Engage` on contact, `Dead` when dead.
They are never stored. `OperatorState.Intent` reads `0` for every operator on
every tick of the run, and a grep for an assignment to that member anywhere in
`src/Sandata.Core` returns nothing.

This is not cosmetic. `Intent` is folded into the state hash by
`SandataStateHasher`, so the hash carries a field that is provably constant. It
is snapshotted, so a resumed mission restores an intent that was never true. And
the operator inspector's `Intent:` row — one line above the two rows added
yesterday to make `SD-4` readable — reports `Hold` for an operator that is
engaging.

It is the same shape of defect as the three the 2026-08-12 package closed and
the one task 90 closed: a rule fully implemented, fully tested, and wired to
nothing.

### 2.2 A shooter never re-targets after its target dies

Entity 4 keeps `Engage` and keeps cycling its chain after entity 1 dies, while
emitting no shots. Its contact memory still names a dead operator as its best
contact, and the fire proposal stage refuses to fire at a dead target — correctly
— but nothing tells the shooter to look for another one. Entity 2 is 88 world
units away, in the open, and is never considered.

A weapon chain that cycles without firing is also what the client draws, so a
spectator sees a defender whose weapon twitches indefinitely at a corpse.

### 2.3 The surviving attacker never identifies a hostile it is standing next to

`IntentSelection` grants `Engage` only on `BestContactTier == Identified`. Entity
2 reports `Hold` for 2,300 ticks at 88 world units from entity 4, which is inside
the 96-unit identify range, with a line of sight its dead squadmate demonstrably
had one tick earlier from nine units away.

**Settled 2026-08-14, and both candidates were wrong.** The two offered
explanations were a stale facing and a skipped contact update. It is neither, and
the sensing layer is behaving correctly: there is a wall in the way. The map
carries `WALL 420 60 420 120`, running down to exactly `y = 120`, and
`WALL 420 160 420 200` resuming below it — the gap between them is the entrance
to the objective room. Entity 2 stands at `(412, 119)`: **one world unit north of
that opening, on the far side of the wall.** Entity 1 fell at `(421, 120)`, one
unit east of the same wall line and inside the aperture, which is exactly why it
was visible and was shot. The two operators are nine world units apart with a
wall between them.

`tests/Sandata.Core.Tests/ContactAfterHaltTests.cs` pins all of it: no line of
sight from the survivor's position, line of sight from the squadmate's, both
positions far inside identify range so range is not the difference, and the
aperture's own coordinates so a later fixture edit that closes it is caught here.

**This collapses 2.3 into 2.4.** Nothing needs fixing in
`src/Sandata.Core/Sensing`. What is wrong is that the survivor stands behind that
wall for 2,300 ticks and never steps through the opening its squadmate died in,
because its group's path was consumed and no path is ever re-requested. The fix
is decision D3, and the plan's task 4 — a fix for whichever sensing candidate
this turned out to be — is therefore dropped rather than done.

### 2.4 The squad stops for good when its leader dies

Entity 2 does not resume the walk to the objective after entity 1 dies. The
group's published path is not re-requested, the survivor is not re-slotted, and
`SquadGrouping`'s "leader is the lowest living entity id" rule re-derives a new
leader that then does nothing with the role. This is the same family as the
scaffold plan's open question about a blocked mover never re-planning: nothing in
Sandata re-evaluates a path after the world changes underneath it.

## 3. What this design decides

**D1 — the selected intent is written into authoritative state.** Stage 8 stores
each result into `OperatorState.Intent` in the same pass that produces
`PendingIntents`. The field stops being decoration, the inspector stops lying,
and a resumed mission restores the intent the operator actually held.

This **moves both Sandata hashes**, and unlike the previous package's prediction
this one is real: `Intent` is folded into the state hash, its value changes on
the first tick any operator stops holding, and the golden fixtures tick real
operators through real intents. Section 5 records what must be re-measured.

**D2 — an engagement ends when its target dies.** A contact that is no longer
alive is dropped from contact memory on the tick it dies rather than remembered
as a live target. An operator whose best contact is gone re-selects from what it
can still see, and an operator with nothing to see falls through to the next
intent rule rather than holding an engagement against a corpse. The weapon chain
returns to its resting phase rather than cycling.

This is deliberately **not** "remember where the body was". Design section 8's
memory ghosts are about a contact that has left sight, not one that has stopped
existing. A corpse is not a threat and the shooter has direct evidence of that.

**D3 — a squad whose composition changes re-requests its path.** When a group's
membership changes — the shipped case is a member dying — the group submits a
fresh path request from its surviving leader's current cell to the same goal.
The request record is authoritative and snapshotted exactly as every other one
is, so the derived-path rule and the resume rule both still hold, and the fixed
`PathLatencyTicks` still governs when the result becomes valid. Nothing here
introduces a per-tick search budget.

**D4 — a mission that cannot progress ends.** `OutcomeRules` gains one further
condition: when no operator of a faction can any longer reach the mission's
objectives and no engagement is live, the mission resolves rather than running
forever. The exact predicate is deliberately left to the plan's first task,
because it depends on 2.3's answer, but the bar it must meet is stated here: **a
run that a spectator would call over must reach an outcome the simulation
agrees is over.**

## 4. What this design does not decide

**Whether the weapon-lowered rule should also gate engagement.** Both attackers
are lowered from tick 604 onward because they are near a wall, and they stay
lowered while a firefight happens next to them. Whether a lowered operator should
raise its weapon when it identifies a hostile is a real tactical question and it
is not this document's. It is recorded because the 2026-08-14 tester will see it:
an operator stands beside a shooting enemy with its weapon down.

**Whether entity 3 should ever do anything.** A defender that never moves, never
patrols, and never reacts to a firefight forty world units away is a behaviour
model question, not a defect repair. Sandata has no patrol, no investigate, and
no alert-driven movement, and adding one is its own package.

**Balance of any kind.** Operator health stays 100, damage stays 25, and the
four-round burst ceiling the previous package handed back stays handed back.

## 5. What this costs

D1 moves the state hash, and D2 and D3 move it further by changing what
operators do. That means, in order:

- Both golden fixtures in `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`
  must be **re-measured by running a capture**, never edited by hand to match an
  observed value.
- The recorded seed-1 headless baseline is superseded; the figure it replaces
  moves to `docs/development/measurement-history.md`.
- `MissionStateTests.PreTask79cBaselineHash` is a constructed fixture's digest
  and is not ticked, so D1 alone does not move it — but the fixture's `Intent`
  value is `0` today and a test that constructs a non-zero one must be read
  before that literal is trusted again.

None of this needs a new `SandataPresetId`. No enum value or order changes, no
roster order changes, no weapon weight changes, no tick rate or millisecond
conversion changes, and no hash mixer changes. `SandataRuleset.ContentHash`
stays `8_955_292_433_887_190_872`.

**The gate will not catch a regression in any of this.** The seed-1 workload is
built by `HeadlessRunner.BuildOpenGrid` — no walls, no doors, no map, no
objectives — as the previous package recorded. A wall-bearing golden fixture is
therefore part of this package rather than a nice-to-have, and it is what makes
the doorway walk and the re-planned path testable at all.

## 6. The spectator-discoverability answer

`SIMULATION-GAME-STANDARDS.md` section 10 asks whether a spectator can discover
the effect without reading source code. Today the honest answer for the whole
mission is no: it looks identical from tick 676 to tick 3000, and nothing on
screen says whether that is a stalemate, a bug, or the end.

After D1 through D4 the answer is yes, and by three separate routes: the
inspector names each operator's real intent and its reason code, the mission
reaches an outcome the HUD already draws, and the event feed carries the
re-target and the re-plan rather than falling silent.
