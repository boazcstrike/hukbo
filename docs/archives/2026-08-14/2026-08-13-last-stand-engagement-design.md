# Last-stand engagement — design

**Archived: reference only.** Remedy C was adopted and executed:
`MovementPresetId.LastStandEngagementV11` ships, the client selects it, and
`LS-1` closed `PASS` on 2026-08-14. The plan that carried the twelve tasks is
archived beside this document. Every path citation to it under `src/`,
`tests/`, `scripts/`, and `docs/` was rewritten on the day it was archived to
name this document in prose, which is what the rule against paths into
`docs/archives/` requires. Never execute it, never treat it as a live task
list, and never cite it as the reason to make a change. The live contract for
this project remains `CLAUDE.md` and `docs/development/testing.md`; nothing in
this file overrides either of those. Archived 2026-08-14.

Written 2026-08-13, out of smoke row `LS-1`. **This document does not authorize
implementation.** It states the problem, the measurement behind it, three
candidate remedies, and the cost each one carries. A plan document and an
explicit decision come after it, because every remedy here moves both hashes.

## 1. The problem

A person ran the last-stand formation smoke family on 2026-08-13. All six rows
passed, and the tester added an observation on row 76 that no row states as a
criterion: "still seeing 1v1 in the endgame." The survivors gather correctly and
then fight one pair at a time while the rest stand off.

Row 78 was written to exclude exactly this — "the fight that follows is a group
fight rather than a sequence of separate duels" — and row 81 to exclude "two
clusters standing apart". The family's rows passed because each of them tests
the part of the behaviour that works: the gathering, the raggedness, the
travelling together, the re-form when a leader falls, the inspector row. Nothing
in the family tests the collision at the end, which is the thing that reads
wrong.

## 2. The measurement

A regrouping follower does not aim at its rally agent. It aims at a trail point
behind that agent, on the far side from the enemy the agent is closing on, and
stops on arrival.

| Quantity | Value | Source |
| --- | --- | --- |
| `RallyTrailRadiusMultiplier` | 12 body radii | `src/Hukbo.Core/Simulation/FormationRules.cs:188` |
| `RallyJitterRadiusMultiplier` | 6 body radii | `src/Hukbo.Core/Simulation/FormationRules.cs:110` |
| Default body radius | 4.25 world units | `src/Hukbo.Core/Simulation/CollisionRules.cs:72` |
| Resulting trail distance | **51 world units** | product of the two above |
| Longest melee reach, shipped preset | 16 world units | `src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:188` |
| Last-stand threshold, default | 6 living agents per faction | `src/Hukbo.Core/Simulation/FormationRules.cs:104`, applied `src/Hukbo.Core/Simulation/Scenario.cs:260` |

The trail base is computed opposite the leader's own direction of travel
(`ComputeRallyTrailBase`, `src/Hukbo.Core/Simulation/BattleSimulation.cs:3853`),
the follower's aim point is that base plus its fixed jitter, and the proposal
returns "no movement" once the follower is within contact distance of that point
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:3784-3792`). The rally agent
itself is exempt from regrouping
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1426`).

So each faction's rally agent closes and fights while every follower holds
station 51 world units back — more than three times the longest weapon reach.
Both factions do this symmetrically. Two rally agents meet, duel, and when one
dies the next-lowest living `EntityId` becomes the rally agent and the same duel
repeats. The behaviour is deterministic and does not depend on the seed.

**The regroup override only yields to body contact, not to weapon reach.** A
follower is pulled onto the trail point whenever its intent is `Moving`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1413-1429`), and intent becomes
`Attacking` only at two body radii. That is a second, smaller instance of the
same theme, and it matters for remedy C below.

## 3. Why the obvious fix is not a fix

Lowering `RallyTrailRadiusMultiplier` alone breaks a stated invariant. The
type-level remarks require

```
RallyTrailRadiusMultiplier > RallyJitterRadiusMultiplier * sqrt(2) + 2
```

(`src/Hukbo.Core/Simulation/FormationRules.cs:180-188`). With the jitter
multiplier at 6 the floor is about 10.49, so 12 sits just above it. Any
meaningful reduction in the trail forces a matching reduction in the jitter, and
the jitter is what makes the clump ragged — which is the criterion row 77 passed
on. A remedy that fixes the duelling by flattening the jitter trades one passing
row for another.

## 4. Candidate remedies

### A. Shrink both multipliers

Set the jitter to 2 and the trail to 5, satisfying the inequality (floor 4.83).
The trail becomes 21 world units, inside a single closing step of weapon reach.

- **For:** smallest possible diff; no new state, no new branch in the tick
  pipeline.
- **Against:** the clump gets markedly tighter and less ragged, which is row 77's
  criterion, and the same two constants are shared by the persistent-contingent
  cohesion path through `ComputeContingentTrailRaw`, so a whole-battle formation
  change rides along with an endgame fix.

### B. Scale the trail by the number of living survivors

Keep the constants, and divide the trail distance by the faction's living count
once the last-stand threshold is crossed, so six survivors trail at 51 and two
at 17.

- **For:** the gathering phase is untouched, and the tighter formation appears
  exactly when the tester says it is missing.
- **Against:** introduces a per-tick dependency on a count that already exists
  but has never fed geometry; the falling trail distance means followers re-aim
  every time a warrior dies, which risks reading as a twitch rather than as a
  closing.

### C. Make the trail state-dependent on whether the leader is engaged

Trail at the current 12 body radii while the rally agent is travelling; when the
rally agent is itself in contact with an enemy, aim followers at their own
nearest enemy instead of at the trail point.

- **For:** it matches the wording of the rows directly — a column on the march,
  a collision at the end — and leaves both multipliers, the raggedness, and the
  contingent cohesion path alone. It also removes the second instance named in
  section 2, because a follower with an enemy inside its own reach stops being
  redirected.
- **Against:** the largest behavioural change of the three, and it adds a branch
  to the regroup proposal that has to be ordered deterministically (nearest
  enemy resolved by the existing total order, ties on `EntityId`).

**Recommendation: C.** It is the only one of the three that fixes what the
tester saw without disturbing a criterion another row already passed on, and it
leaves the shared contingent path untouched.

## 5. What any of them costs

All three change authoritative simulation state, so all three carry the same
overheads under `CLAUDE.md` section 5:

- a new movement preset version, since the shipped preset's behaviour changes;
- new golden expectations — Hukbo's gate runs three baselines at stage 5, not
  one, and each needs re-recording with the changed workload named;
- an entry in `SIMULATION-GAME-STANDARDS.md`'s last-stand subsection, which is
  the game-rule statement of this behaviour;
- the existing last-stand test suite re-read rather than re-pinned. A pinned
  hash that moves is expected here, but the tests asserting the trail geometry
  are asserting the thing being changed and must be rewritten deliberately, not
  adjusted until green.

The historical accuracy policy adds nothing to this change: the rally shape is
already recorded as a game-design invention rather than a documented formation,
in `SIMULATION-GAME-STANDARDS.md` and at
`src/Hukbo.Core/Simulation/FormationRules.cs:3-7`. A tighter or looser clump is a
gameplay decision, not a historical claim, and the labels already say so.

## 6. Open question for the decision

Is the endgame meant to read as two small bands colliding, or as a champion duel
with witnesses? The rows assume the first. The code currently produces the
second, and it produces it consistently enough that it could be adopted
deliberately instead of repaired — in which case the remedy is to rewrite rows
78 and 81 and close `LS-1` as "working as intended", at no cost to any hash.
Nothing below section 4 is worth building until that is answered.
