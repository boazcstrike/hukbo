# Formation and movement realism — persistent contingents

> **Archived: reference only.** This design is complete and the workstream it
> authorized is implemented. Do not execute it, and do not treat its steps,
> versions, file paths, or line-number citations as current. The live contract
> is `CLAUDE.md` plus the skills in `.claude/skills/`. The ordered task list
> that carried out this design is the companion plan document,
> [2026-07-28-formation-movement-realism.md](2026-07-28-formation-movement-realism.md),
> archived alongside it. The evidence this workstream produced is live and
> stays where it is, in
> [docs/development/testing.md](../../development/testing.md) and
> `SIMULATION-GAME-STANDARDS.md`.

Status: **Design only. A `-design.md` does not authorize implementation.** This
document records the reasoning, the arithmetic, the historical position, the
determinism argument, and the alternatives that were rejected. Nothing in it
permits a line of code to be written. The ordered task list that does authorize
implementation is the companion plan document,
[`2026-07-28-formation-movement-realism.md`](2026-07-28-formation-movement-realism.md),
and even that authorizes only the tasks it names, in the order it names them.

---

## 1. Why this document exists, and what was actually asked for

Hukbo deploys each faction at tick 0 as several visually separate groups.
`FormationPlanner` computes those groups, places every warrior on a shared
staggered lattice with bounded jitter, and hands the caller a flat array of
positions. The grouping is then thrown away. `FormationPlanner`'s own type-level
remarks say so in as many words: the lattice "survives only until tick 1, when
ordinary target selection and collision resolution dissolve the groups", and
"nothing outside this file should treat a contingent as a persistent unit"
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:24-30`). From tick 1 onward every
warrior is an independent agent that walks in a straight line at a constant speed
toward whichever living enemy is nearest, and the collision resolver sorts out
the resulting pile-up.

The user asked for three things, and they are not open to renegotiation.

**First, persistent contingents.** The contingents `FormationPlanner` creates at
deployment must survive past tick 0 as real units: each with a leader, a cohesion
pull that keeps its members loosely together, and a shared unit-level state that
its members act on. Alongside that, the per-agent movement itself must improve —
warriors should ease into contact rather than snapping to a stop, should not all
converge on one point, and should give way rather than beeline through a
companion.

**Second, versioning.** All of this moves authoritative simulation state, and
therefore moves the state hash. The chosen path is a new versioned
movement/behaviour preset, registered alongside a frozen preset that preserves
today's behaviour, so a replay recorded against the current rules still
reproduces when it names those rules. This mirrors the combat preset pattern
already in the repository (`CombatPresetId` at
`src/Hukbo.Core/Combat/CombatIdentity.cs:93-114`, `CombatPresetRegistry` at
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs:9-67`).

**Third, a specific and narrow reading of "realistic".** Plausible-for-period
only. Loose kin and boat-crew bands, irregular spacing, mass-then-close,
give-way, and individuals stepping out of a group as an emergent consequence of
who happens to be near whom. Drilled ranks, files, fixed frontage, and shield
walls are explicitly *not* attested for this place and period and must not
appear, in behaviour or in any player-facing label. Every new behavioural rule is
labelled a **Provisional reconstruction** under CLAUDE.md section 7.

This document turns those three requirements into an implementable design that
survives the repository's determinism contract, its performance budget, and its
historical-accuracy policy.

---

## 2. The measured and observed problem

### 2.1 What the simulation does today

Each tick runs eight fixed stages, in this order, at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:320-327`:

```
DecrementCooldowns
SelectTargetsAndIntents
GatherMovementProposals
ResolveCollisions
CommitMovement
MeasureCollision
GatherAndCommitAttacks
ResolveOutcome
```

`SelectTargetsAndIntents`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:612-705`) gives every living
agent the nearest living enemy inside its perception range, breaking a distance
tie on the lower `EntityId`. Intent becomes `Attacking` when the selected
distance is already inside contact distance, otherwise `Moving`. The one existing
collective behaviour then overrides `Moving` with `Regrouping`, but only when the
faction's living count has fallen to the last-stand threshold and only for agents
that are not their faction's rally agent
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:696-703`).

`GatherMovementProposals`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:744-769`) builds one proposal for
each `Moving` agent by calling `BuildMovementProposal`, and one for each
`Regrouping` agent by calling `BuildRegroupingProposal`. Every other agent
proposes nothing.

`BuildMovementProposal`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1649-1687`) is the whole of
individual movement:

```
deltaX          = destinationX - agentX
deltaY          = destinationY - agentY
distanceSquared = deltaX*deltaX + deltaY*deltaY          (checked, long)
distance        = IntegerSquareRoot(distanceSquared)
desiredMovement = Max(1, distance - stopShortRaw)
movement        = Min(agent.MovementSpeedRaw, desiredMovement)
moveX           = deltaX * movement / Max(1, distance)   (checked, truncating)
moveY           = deltaY * movement / Max(1, distance)   (checked, truncating)
```

with a `Math.Sign` fallback on the dominant axis when both components truncate to
zero, and a bounds clamp through `CollisionGeometry.ClampCenterToBounds`.

Three properties of that function are the whole of the problem.

- **There is no arrival taper.** `movement` is `MovementSpeedRaw` on every tick
  until the very last one. A warrior travels at full speed and then stops dead.
- **There is no neighbour term at all.** The only inputs are the agent's own
  position and its target's. Two hundred warriors whose nearest enemy is the same
  enemy all aim at the same point.
- **There is no notion of a companion.** Nothing in the movement path knows that
  another agent exists except through the collision resolver, which runs
  afterwards and only ever says "no".

### 2.2 Why that reads as a blob

The consequence is mechanical and predictable. At tick 0 the two armies are
several readable groups; the "Starting deployment smoke" rows in
`docs/development/testing.md:2539-2542` are written to check exactly that. From
tick 1 the grouping has no representation anywhere, so it decays immediately:
every warrior independently re-solves "who is nearest" and walks straight at the
answer. Because both armies advance toward each other, and because nearest-enemy
is very nearly the same answer for every warrior on the same flank, the straight
lines converge. The contingents interleave, then merge, and by contact the two
armies are one undifferentiated crowd pressed against a contact surface.

The collision resolver then does the only shaping there is. `ResolveCollisions`
accepts a candidate destination only when it produces zero strict penetration
against already-committed bodies and against other movers' start positions, and
falls back through a fixed ladder — full destination, single-axis slide,
single-axis slide on the other axis, a truncation ladder halving the step up to
eleven times, then hold position
(`src/Hukbo.Core/Simulation/CollisionResolver.cs:58-124`). That ladder produces a
front line, and it is a real front line, but it is a front line made of a crowd
being squeezed, not of groups holding together.

This is not a bug. It is the exact scope the collision and formation work
deliberately shipped: the archived formation-and-collision plan lists "cohesion"
among the things it explicitly did not add. The blob is the honest visual
consequence of a movement rule with no companion term.

### 2.3 What the numbers say about the room available

`docs/research/TICK-STAGE-PROFILE.md:68-96,134-136` records the per-stage
inclusive share of `AdvanceOneTick`:

| Agents | `ResolveCollisions` share | `SelectTargetsAndIntents` share |
| --- | --- | --- |
| 200 | 63.11% | at most 16.67% |
| 1000 | 70.11% | at most 16.67% |
| 2000 | 74.77% | at most 16.67% |

At 2000 agents, `CollisionResolver.IsFree` alone is 50.62% of all exclusive tick
time. The tick is collision-bound and the collision stage grows super-linearly
(measured exponents 1.60, 1.97, 2.19). Anything this design adds is stacked on
top of that. The design's response is that every new rule is O(1) per agent or
O(n) per tick with a small constant, that no new spatial query is introduced, and
that the collision resolver is not touched at all.

---

## 3. The design

### 3.1 Shape of the change

One new tick stage is inserted, and one existing stage gains a branch.

```
DecrementCooldowns
SelectTargetsAndIntents
ResolveContingentStates      <-- new, ninth stage
GatherMovementProposals      <-- gains a preset-selected branch
ResolveCollisions
CommitMovement
MeasureCollision
GatherAndCommitAttacks
ResolveOutcome
```

`ResolveContingentStates` sits after `SelectTargetsAndIntents` because it needs
every agent's `Intent` and `TargetEntityId` to be final for the tick, and before
`GatherMovementProposals` because that stage is the first consumer of its output.
It reads tick-start positions only. The only agent field it writes is
`AgentState.ContingentState`, on every living agent; beyond that it writes only
its own preallocated per-contingent scratch arrays — leader, counts, trail base,
margin, and the two geometric gate flags of section 3.5 — which are fully
overwritten every tick and hold nothing across a tick boundary. It commits no
position and emits no event.

Under the frozen preset the new stage returns on its first line and
`GatherMovementProposals` takes its existing path unchanged, so the frozen
behaviour executes exactly the instructions it executes today and draws exactly
the random values it draws today.

The eight-stage list is quoted verbatim in
`SIMULATION-GAME-STANDARDS.md:508-521` and in
`docs/research/TICK-STAGE-PROFILE.md:68-96`. Both must be updated to nine, and
that is a task in the plan, not an afterthought.

### 3.2 Contingent identity

A contingent is identified by the pair `(FactionId, ContingentId)`.
`ContingentId` is a small integer in `[0, 7]`, because `FormationPlanner` caps
contingents at eight per faction (`MaximumContingents = 8`,
`src/Hukbo.Core/Simulation/FormationPlanner.cs:45`).

Membership is decided once, at `BattleSimulation.Create`, and never changes.
`FormationPlanner` already computes it: warriors are dealt round-robin,
`contingent = localIndex % contingentSizes.Length`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:93-95`). That dealing rule is
deliberate — it prevents weapon-homogeneous contingents, because `RosterCounts`
groups one weapon category into a contiguous run of faction-local indices. This
design reuses it exactly and changes nothing about it.

One correction is required. `PlanFactionDeployment` currently discards the
membership, and the crowded-map fallback `PlanDenseBlock`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:218-253`) never consults
`contingentSizes` at all. Membership must therefore be defined independently of
which placement path ran:

```
contingentCount = ResolveContingentSizes(warriorCount).Length
contingentId    = localIndex % contingentCount
```

computed on both paths, returned alongside the positions. This changes
`PlanFactionDeployment`'s return shape but not one coordinate it produces and not
one random draw it takes, which is what makes it provable as behaviour-inert.

`ContingentId` is stored on `AgentState`, projected onto `AgentView`, and folded
into the state hash. It is written once at spawn and never mutated. A dead agent
keeps its `ContingentId`; the leader scan skips it because it is not alive.

### 3.3 The leader

The leader of a contingent is **the living member with the lowest `EntityId`**,
recomputed by a fresh forward scan at the top of `ResolveContingentStates` on
every tick. It is not stored, it is not cached, and it is not carried between
ticks.

This is the pattern `ComputeRallyAgents` already uses for the faction rally agent
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:715-738`), and it is chosen for
four reasons.

- The comparison is against `EntityId` explicitly, never against array position,
  so a permuted agent array yields the same leader. `DeterminismTests` already
  proves this property for the rally agent through
  `RallyAgentSelectionIsUnchangedByAgentArrayPermutation`.
- Promotion on death is free. When a leader dies, the next-lowest living
  `EntityId` becomes the leader on the very next tick, with no event, no
  bookkeeping and no possibility of a dangling reference.
- `EntityId` is unique, so there is no tie to break and no secondary key is
  needed.
- It avoids the cache rules entirely. `SIMULATION-GAME-STANDARDS.md:198-215`
  requires every cache to declare a source, a key, a size bound, a lifetime, an
  invalidation rule, counters, and a cold-cache equivalence test, and CLAUDE.md
  section 9 forbids unbounded caches outright. A value recomputed from scratch
  every tick is not a cache and owes none of that.

A leader is **exempt from its own contingent's cohesion pull**. It keeps ordinary
nearest-enemy pursuit. This is the same exemption the rally agent has
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:696-703`), and section 10 explains
why exemption alone is not a liveness proof.

### 3.4 The four unit states

`ContingentState` is a new pinned enum, appended-only from the day it ships:

| Value | Name | Meaning |
| --- | --- | --- |
| 0 | `None` | Not under a persistent-contingent preset, or the contingent has no living members. |
| 1 | `Advance` | Moving toward the enemy. Cohesion applies **only to a member that has fallen behind the straggler threshold defined below**. Every other member pursues its own nearest enemy exactly as it does under the frozen preset. |
| 2 | `Hold` | Gathering. Cohesion applies to every living, moving, non-leader member. |
| 3 | `Close` | A member has reached engagement distance. Cohesion is off. |
| 4 | `Break` | The contingent has lost too many members to act as one. Cohesion is off permanently. |

The authoritative store is per agent: every living member of a contingent carries
the same `ContingentState` value on its own `AgentState`, written every tick by
`ResolveContingentStates`. There is no parallel per-contingent array holding
state that the hash cannot see. The previous tick's value for a contingent is
read from its current leader, which by construction is a living member and
therefore carries it.

`ContingentState` is a behavioural mode, never a positional assignment. No agent
is ever assigned to a rank, a file, a slot, or a named formation; that is
forbidden without qualification by `SIMULATION-GAME-STANDARDS.md:417-421`.

#### Transition rules

Evaluated once per contingent per tick, in ascending slot index over a dense
array. Let:

- `livingCount` — living members of this contingent.
- `initialCount` — members at spawn, fixed at `Create`.
- `leaderX`, `leaderY` — the leader's tick-start position.
- `spreadSquared` — the maximum, over living non-leader members, of the squared
  distance from that member to the leader. Zero when there are none.
- `nearestEnemySquared` — the minimum, over living members that have a selected
  target, of that member's squared distance to its target. `long.MaxValue` when
  no member has a target.
- `cohesionRadiusRaw` — `CohesionRadiusMultiplier * BodyRadiusRaw`, with
  `CohesionRadiusMultiplier = 24`. A design choice, not a measurement.
- `closeRadiusRaw` — `CloseRadiusMultiplier * BodyRadiusRaw`, with
  `CloseRadiusMultiplier = 16`. A design choice, not a measurement.
- `slot` — `FactionId * MaximumContingents + ContingentId`, in `[0, 15]`.
- `cohesionPhase` — `slot * CohesionCycleTicks / 16`, an integer. With
  `CohesionCycleTicks = 240` and sixteen slots this is `15 * slot`, so the
  sixteen possible contingents are evenly staggered across the cycle.
- `cohesionWindowOpen` — the duty-cycle predicate, defined in full below.

#### The straggler threshold

A single named threshold decides, for one member, whether that member has
fallen behind its contingent. It is **derived from `cohesionRadiusRaw`, not
invented separately**, and the derivation is deliberate: `cohesionRadiusRaw` is
already the radius at which the contingent as a whole is judged too spread out,
and its three-quarters point is already the radius at which the contingent is
judged gathered again. Reusing that same three-quarters point as the individual
threshold makes the two tests say the same thing at the two different scales
they operate on. If no member is straggling, then no member exceeds the
gathered radius, so the contingent's `spreadSquared` cannot exceed it either,
and the hysteresis exit and the straggler test can never disagree about whether
a contingent is gathered.

Let `memberSquared` be the squared distance from a member's tick-start position
to the leader's tick-start position, computed by the existing private
`BattleSimulation.SquaredDistance(AgentState, AgentState)`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1703`) — the same agent-to-agent
primitive `SelectTargetsAndIntents` already calls once for every candidate it
considers (`src/Hukbo.Core/Simulation/BattleSimulation.cs:659-662`). No square
root is taken anywhere in the test; both sides are `long` products, matching
exactly how `SelectTargetsAndIntents` compares `distance > perceptionSquared`
rather than comparing lengths.

```
straggling  <=>  16 * memberSquared > 9 * cohesionRadiusRaw * cohesionRadiusRaw
```

which is the squared, division-free form of "the member is farther than
`3/4 * cohesionRadiusRaw` from its leader" — eighteen body radii at
`CohesionRadiusMultiplier = 24`. It is deliberately the same integer expression
as the hysteresis-exit comparison in the transition rules below, so a reader who
has understood one has understood the other.

**The comparison is strict.** A member at exactly
`16 * memberSquared == 9 * cohesionRadiusRaw * cohesionRadiusRaw` is **not**
straggling and takes independent pursuit. Choosing the non-cohesive side of the
boundary is the safe default: it can only ever remove a cohesion destination,
never add one, so no liveness argument in section 10 depends on which way the
boundary falls. That exact-equality case is pinned by its own test rather than
left to a reader's inference.

The rules, in strict priority order. The first that matches wins.

1. **Break is terminal.** If the previous state was `Break`, the state stays
   `Break`.
2. **Break on attrition.** If `livingCount * 4 <= initialCount` or
   `livingCount < MinimumCohesiveMembers` (with `MinimumCohesiveMembers = 3`),
   the state becomes `Break`. Both comparisons are integer.
3. **Close on contact.** If
   `nearestEnemySquared <= closeRadiusRaw * closeRadiusRaw` (computed as a
   `long` product), the state becomes `Close`.
4. **The cohesion duty cycle has shut.** If `cohesionWindowOpen` is false, the
   state becomes `Advance`. This rule sets the *label* only. What actually
   withholds the cohesion destination from every member — straggler, gathered
   member and leader alike — is gate 3 of the movement branch in section 3.5,
   which tests the same predicate independently. The two are kept in step
   because both compute the same pure function of `Tick` and `slot`; neither
   depends on the other, and an implementation that dropped this rule would
   still be live, merely mislabelled.
5. **Hold to gather.** The state becomes `Hold` when the contingent is more
   spread out than it should be. Entering from a state other than `Hold`
   requires `spreadSquared > cohesionRadiusRaw * cohesionRadiusRaw`, while
   remaining in `Hold` requires only
   `spreadSquared * 16 > 9 * cohesionRadiusRaw * cohesionRadiusRaw`. That is the
   hysteresis band — enter above `R`, leave below `3R/4` — expressed with no
   division and no floating point.
6. **Otherwise, Advance.**

`livingCount == 0` yields `None` and the contingent is skipped for the rest of
the tick.

**The rules above are a pure function and are implemented as one, so a test can
call them directly.** Everything the six rules read is a scalar the calling stage
already holds: the previous state, the living and initial counts, `spreadSquared`,
`nearestEnemySquared`, the two radii, the minimum-members constant, whether the
duty-cycle window is open, and whether both geometric gates passed. None of it
needs an agent array, a simulation, or a tick pipeline. The whole state machine
therefore lives in an internal static — `MovementRules.ResolveContingentState` —
that a test calls with hand-built arguments, which is what makes the priority
order between rules assertable rather than only observable through a whole
battle. The leader-and-living-count forward scan and the duty-cycle predicate are
extracted the same way and for the same reason. This is the testability shape
`FormationRules` already uses for the rally geometry, and the reason
`Hukbo.Core` carries `[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]`
(`src/Hukbo.Core/Properties/AssemblyInfo.cs:3`).

A consequence worth stating, because it changes what "priority" means for the
*movement* gates in section 3.5 as opposed to the transition rules here. The six
transition rules have a genuine priority order: two of them can select different
states from the same inputs, so which one is consulted first decides the answer,
and a test must pin it. The six movement gates do not: every one of them is an
unconditional denial, so the branch taken is the logical conjunction of all six
and no gate can win over another. Their listed order is a reading order and a
short-circuit order, never a semantic one. The right assertion for them is
therefore an exhaustive truth table showing the conjunction, together with the
statement that evaluation order is immaterial — not a priority test, which would
be asserting a property the code does not have.

One interaction between rules 4 and 5 is deliberate and is stated here so it is
not discovered later as a surprise. Because rule 4 forces `Advance`, a
contingent leaving a shut window is entering `Hold` "from a state other than
`Hold`", so it faces the higher entry bar `spreadSquared > cohesionRadiusRaw^2`
rather than the lower exit bar. A contingent that had gathered to somewhere
between `3R/4` and `R` when the window shut therefore does not resume gathering
when the window reopens; it keeps advancing until it strings out past `R` again.
That is the intended shape — gather, release, advance, gather again — and it is
what smoke row 103 is written to look for.

#### The cohesion duty cycle

The duty cycle is the hard bound on how long a contingent can stay in a cohesive
regime, and it earns its place in section 10. It is defined for a contingent in slot `s` as

```
cohesionPhase             = s * CohesionCycleTicks / 16
cohesionWindowOpen(Tick, s) =
    ((Tick + cohesionPhase) % CohesionCycleTicks) < CohesionDutyTicks
```

with `CohesionCycleTicks = 240` and `CohesionDutyTicks = 180`. Both are
game-design inventions, not measurements, and both carry that statement in
their own XML doc comments.

Four properties matter, and the first two are the whole reason the mechanism is
shaped this way. A fifth paragraph states what the third property does *not*
establish, because an earlier revision of this document overstated exactly that
and offered the overstatement as failure shape 2's only liveness escape.

**It is a pure function of the tick and the slot.** It reads nothing the
simulation observes: not spread, not blocking, not enemy proximity, not
casualties, not anything the collision resolver did. There is no counter, no
stored per-contingent field, no additional value in the state hash, and nothing
to initialise at `Create`. Because it is a pure function of two integers that
both `ResolveContingentStates` and `GatherMovementProposals` already hold, the
two stages evaluate it independently in the same tick and cannot disagree.

**It covers `Advance` as well as `Hold`.** An earlier revision of this design
gated only the `Hold` state, which left `Advance` with no timeout at all — and
`Advance` is the state a contingent spends most of a battle in. Rule 4 sits
above rule 5 in the priority order, so a shut window overrides the gathering
test rather than merely competing with it, and the movement branch in section
3.5 consults the same predicate before granting any cohesion destination to any
member in any state.

**Its liveness consequence is a counting argument, not a behavioural one.** The
argument runs through gate 3 of the movement branch in section 3.5, not through
the state machine, and the distinction matters: rule 4 *sets* `Advance` when the
window is shut, so observing the state `Advance` does not by itself imply an
open window. Gate 3 is what carries the guarantee. It tests
`cohesionWindowOpen(Tick, slot)` directly and unconditionally, before any state
is consulted, so *an agent receiving a cohesion destination implies the window
is open* — with no case analysis over states at all. The window is open for at
most `CohesionDutyTicks = 180` consecutive ticks out of every
`CohesionCycleTicks = 240`. It follows that no agent can be given a cohesion
destination for more than 180 consecutive ticks, and that every contingent gets
at least 60 consecutive ticks in every 240 during which every one of its members
is running the frozen preset's individual pursuit. **Cohesion therefore cannot
hold a contingent in a cohesive regime indefinitely.**

**What that counting argument does not establish, stated plainly because an
earlier revision of this document got it wrong.** The duty cycle governs which
*destination* an agent is proposed. It says nothing whatever about whether the
collision resolver will *grant* the movement toward that destination, because
the resolver runs two stages later and the duty cycle is not an input to it. An
earlier revision claimed the duty cycle's conclusion "does not rest on any
argument about what the collision resolver will do" and offered it as the
liveness escape for failure shape 2 in section 10.2. That claim was false, and
it was false in exactly the way section 10.1 names as the governing lesson of
the two prior observed deadlocks: changing the destination an agent proposes
does not exempt that agent from being physically blocked by the agents around
it. Sixty free ticks are sixty ticks of the frozen preset's individual pursuit,
which this repository has never observed to deadlock — but "never observed to
deadlock" is evidence about behaviour under the collision resolver, not a proof
of forward progress. The argument that forward movement actually succeeds is the
quarter-density packing bound, and the duty cycle does not carry it. The two
geometric gates in section 3.5 — the map-edge test and the cross-contingent test
— are what carry it, by guaranteeing that the packing bound is only ever
*applied* to ground it was proved for. Section 10.2 sets out the division of
labour between the three.

Rule 4's job in the state machine is separate and narrower: it keeps the state a
spectator reads in the inspector honest, so a contingent whose members are all
pursuing independently is never labelled `Holding`. Rule 4 could be deleted and
the duration bound would survive; the inspector would simply start lying.

Sixty free ticks is enough to matter rather than merely enough to exist.
`Scenario.Validate` enforces `MovementSpeedRaw <= BodyRadiusRaw`
(`src/Hukbo.Core/Simulation/Scenario.cs:326-343`), so an unobstructed warrior
covers up to sixty body radii in a free window. The widest cluster this design
can produce has half-side `jitterRaw`. On the canonical workload that is nine
body radii: `-Agents 200` is a *total*, so `Scenario.CreateDefault` gives each
faction one hundred warriors
(`src/Hukbo.Core/Simulation/Scenario.cs:156-179`), and
`ResolveContingentSizes(100)` yields five contingents of twenty
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:150-166`), so
`jitterRaw = BodyRadiusRaw * (IntegerSquareRoot(80) + 1) = 9 * BodyRadiusRaw`.
At the largest population the eight-contingent cap admits — a faction of five
hundred warriors, whose largest contingent is sixty-three — it is sixteen body
radii. The free window is therefore between roughly four and nearly seven times
what a member needs to walk clear of the widest cluster it could be standing in.

**The phase term desynchronises the sixteen slots.** With sixteen slots and a
240-tick cycle the phase is `15 * slot`, so no two contingents pause and release
together, and two contingents of the same faction — whose slots differ by one —
have free windows offset by fifteen ticks. The duration bound above does not
depend on that staggering; each contingent's free window is its own. What the
staggering buys is the visual result: "local advance, hesitation, and withdrawal
rather than perfectly simultaneous army-wide motion", which
`docs/research/battles/03-deep-past-formations-and-tactics.md:177-193` records as
plausible for this period.

**`Break` is not a retreat and is not morale.** It means only that the
contingent stops being treated as a unit and its members revert to the frozen
preset's individual pursuit. Nobody flees, nobody withdraws, no `AgentIntent`
value is added, and no fear or resolve variable exists anywhere. This is
deliberate: CLAUDE.md section 9 gates morale behind a milestone that has not been
reached, and a break-as-retreat rule would be a morale system wearing a different
name. It is also the historically honest choice — the sources record that break
and withdrawal happened and are explicitly silent on the procedure
(`docs/research/battles/01-deep-past-overall-warfare.md:280-287`).

### 3.5 The cohesion rule

Under the persistent-contingent preset, inside `GatherMovementProposals`, for a
living agent whose `Intent` is `Moving`, the complete rule is the following.
Every position it reads is a tick-start position, because
`GatherMovementProposals` runs before `CommitMovement` and no agent's position
has moved yet — the invariant that stage's own doc comment already states.

```
slot   = agent.FactionId * MaximumContingents + agent.ContingentId
state  = agent.ContingentState                 // written this tick by stage 3

// --- six gates, each of which sends the agent to independent pursuit ---

1. state is None, Close, or Break
       -> independent pursuit
2. agent.EntityId == leaderEntityId[slot]      // the leader is exempt
       -> independent pursuit
3. not cohesionWindowOpen(Tick, slot)          // the duty cycle has shut
       -> independent pursuit
4. state is Advance and NOT straggling:
       memberSquared = SquaredDistance(agent, leader)
       straggling <=>
           16 * memberSquared > 9 * cohesionRadiusRaw * cohesionRadiusRaw
       -> independent pursuit when the strict inequality does not hold
5. the contingent's bias square does not fit inside the map
   (derived under "The map-edge open-ground test" below)
       -> independent pursuit
6. the contingent's bias square overlaps the bias square of some other living
   same-faction contingent (the cross-contingent test, derived under "The
   cross-contingent test" below)
       -> independent pursuit

// --- otherwise ---

   the agent has a cohesion aim point, computed below, and takes
       BuildMovementProposal(agent, aimX, aimY, leaderEntityId, stopShortRaw: 0)
   unless the aim point is already within contact distance, in which case it
   proposes nothing — the same arrived-guard BuildRegroupingProposal already
   applies (src/Hukbo.Core/Simulation/BattleSimulation.cs:857-865).
```

Gates 5 and 6 are both properties of a *contingent*, not of an agent. Neither is
recomputed here: `ResolveContingentStates` evaluates both once per contingent
per tick and stores each as one `bool` in a preallocated sixteen-entry array, so
at this point each gate is a single array read. Gate 5 is listed before gate 6
because gate 5 is a statement about one contingent on its own while gate 6 is a
statement about a pair, so the single-contingent precondition reads first. Their
relative order cannot change any outcome, because both are unconditional denials
and neither is an input to the other.

"Independent pursuit" means `BuildMovementProposal(agent, target)`, stopping
short by one body diameter — the frozen preset's path, byte for byte. The
cohesion path is the same arithmetic against a different destination. There is
exactly one notion of a step in the simulation and this design does not add a
second.

Gate 4 is what keeps `Advance` from becoming a column. Binding every member to a
computed point behind its leader on every tick of the whole advance would be a
loose column moving in near lockstep, and section 4.1's cited research does not
support that — it supports irregular spacing and locally-judged movement. Under
gate 4 a member that is keeping up is never given a cohesion destination at all;
it re-solves "who is nearest" and walks at the answer, exactly as it does today.
Only a member that has genuinely fallen behind is drawn back toward its group,
and only while the duty cycle permits it. The spacing that results is
individually judged and irregular, which is the behaviour the research actually
describes.

`Attacking` always beats everything. `Regrouping` beats cohesion, because a
faction reduced to the last-stand threshold has at most six living warriors and
therefore has no meaningful contingents left; the whole-faction rally is the
better description of that situation and it is already tested. This gives the
complete same-tick conflict order:

```
Dead
  > Attacking
  > Regrouping
  > contingent cohesion — reached only when the contingent's state is Hold, or
    is Advance and this member is straggling, and in both cases only when the
    agent is not the leader, the duty-cycle window is open, the bias square
    fits inside the map, and that square does not overlap the bias square of
    any other living same-faction contingent
  > ordinary pursuit
```

#### The personal offset

Each member has a **stable personal offset direction** that never changes for the
whole battle, and a **scale that breathes with the contingent's living count**.

```
ContingentOffset.Compute(seed, entityId, jitterRaw):
    hash   = Fnv1a(ContingentTag, seed, entityId)
    random = SplitMix64(hash)
    unitX  = random.NextInt(2 * OffsetUnit + 1) - OffsetUnit   // in [-1024, +1024]
    unitY  = random.NextInt(2 * OffsetUnit + 1) - OffsetUnit
    return (unitX * jitterRaw / OffsetUnit,                    // long, truncating
            unitY * jitterRaw / OffsetUnit)                    // both in [-jitterRaw, +jitterRaw]
```

with `OffsetUnit = 1024` and `ContingentTag = 0x484B424F5F435447` (`HKBO_CTG`),
a fresh 64-bit ASCII domain tag distinct from `HKBO_HIT`, `HKBO_CLS`, the
last-stand tag and the collision-priority tag.

The scaling lives inside the function rather than at the call site, mirroring
`RallyOffset.Compute(seed, entityId, bodyRadiusRaw)`
(`src/Hukbo.Core/Simulation/RallyOffset.cs:43-61`), which likewise returns raw
world units rather than a unit vector its caller has to remember to scale.

**The tick is not an input.** That
is not an accident: `RallyOffset`'s own remarks
(`src/Hukbo.Core/Simulation/RallyOffset.cs:11-21`) record that keying an offset on
the tick makes every follower chase a target that flees a fraction of a unit
every tick, so the formation never converges and the collision resolver spends
its slack fighting a moving goalpost. `RallyOffsetTests.OffsetDoesNotDependOnTheTick`
pins that property for the rally offset and the equivalent test pins it here.

The scale it is given is

```
jitterRaw = BodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)
```

`jitterRaw` is derived, not tuned, and the derivation is the fourfold packing
margin. `FormationRules`' type-level remarks
(`src/Hukbo.Core/Simulation/FormationRules.cs:26-46`) establish that a bias square
of half-side `J = m * R` has capacity `m^2` non-overlapping bodies, and that
capacity is not a safe headcount — the safe headcount is `capacity / 4`, because
offsets drawn at random do not pack perfectly. Solving `m^2 >= 4 * livingCount`
for `m` gives `m = IntegerSquareRoot(4 * livingCount) + 1`, where the `+ 1`
absorbs the integer square root's floor and makes the inequality strict. At the
largest contingent the default 200-agent scenario produces (40 members), `m` is
13 and capacity is 169 against a required 160. At a 500-agent faction the largest
contingent is 63 members, `m` is 16 and capacity is 256 against a required 252.
The rule is correct at every population because it is solved rather than picked.

The scale shrinks as members die, never grows, so a settled member is never
pushed outward by an attrition event; it is only ever drawn slightly inward. The
direction is fixed for the battle. Together those two properties mean the aim
point is monotone and settling, which is the whole point.

#### The trail

Aiming a follower directly at its leader parks the follower in front of the
leader. `FormationRules.cs:48-79` records that failure exactly, including that it
was observed: pre-fix, seeds 5, 6 and 9 stalled the battle at the tick limit with
no casualties. The fix is to aim behind the leader, opposite its direction of
travel, before applying the offset. This design reuses that geometry, generalised
from the rally agent to any contingent leader.

```
leaderDirection = the unit-ish vector from the leader to the leader's own
                  target, as ComputeRallyDirection already builds it; a leader
                  with no target yields the zero sentinel

trailRaw   = ((3 * jitterRaw + 1) / 2) + (3 * BodyRadiusRaw)

trailBaseX = leaderX - leaderDirectionX * trailRaw / leaderDistance
trailBaseY = leaderY - leaderDirectionY * trailRaw / leaderDistance

aimX = trailBaseX + offsetXRaw
aimY = trailBaseY + offsetYRaw
```

with a fallback to the leader's raw position, offset applied, when the leader has
no target and the direction sentinel is zero — matching
`ComputeRallyTrailBase`'s existing behaviour
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:916-931`).

#### The map-edge open-ground test, and why the packing proof needs it

The packing-margin derivation above, under "The personal offset", and the
clearance derivation below are both proofs about an unobstructed square of open
ground, occupied by the aim points of one contingent and nothing else. That
hypothesis fails in two distinct ways, and each needs its own gate: the ground
can run out, near a map edge; or a second contingent's square can arrive on top
of this one. This subsection handles the first and yields gate 5. The next
subsection handles the second and yields gate 6. Neither covers the other's
case, and the escape table in section 10.2 rates them separately for that
reason.

Near a map edge the open-ground hypothesis fails in a way that destroys exactly
the property the proofs establish.

The mechanism is `CollisionGeometry.ClampCenterToBounds`
(`src/Hukbo.Core/Simulation/CollisionGeometry.cs:114`), which pulls a coordinate
back into `[bodyRadiusRaw, dimensionRaw - bodyRadiusRaw]`. Its own remarks state
that "axes are clamped independently; corner contact is simply both axes
clamping in the same tick". Every aim point in the existing last-stand path runs
through it: `BuildRegroupingProposal` clamps its trail-plus-offset aim point at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:848-855`, and
`TryComputeGiveWayAimPoint` clamps its sideways aim point at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1010-1017`. (`ComputeRallyTrailBase`
itself does not clamp; it saturates into `Int32` and hands the result to
`BuildRegroupingProposal`, which clamps. The clamp is real, it is just one frame
further out than it first appears.)

The consequence is that a bias square overhanging a map edge does not stay a
square. Every member whose offset points outward has its aim point pulled onto
the same boundary line, so a two-dimensional spread collapses into one
dimension; in a corner, where both axes clamp, it can collapse toward a single
point. At that moment `capacity = m^2 > 4 * livingCount` is no longer a
statement about anything, because the bodies are no longer being distributed
over a square. This is Deadlock A's exact shape, recreated by geometry rather
than by a bad constant.

This design's exposure to that is strictly worse than the last stand's, and the
comparison is worth stating plainly. The last stand produces one cluster per
faction, late, near wherever the survivors happen to be. This design produces up
to eight per faction and sixteen in play, spread across the whole map, for most
of the battle. Whatever the last stand's residual boundary risk was, this design
multiplies it.

The answer is to **guard the proof with its own hypothesis**. Before any member
of a contingent is given a cohesion destination, the contingent's entire bias
square must be shown to fit inside the legal interval on both axes, computed in
`long` from the unclamped trail base:

```
mapWidthRaw  = MapWidth  * FixedPoint.Scale
mapHeightRaw = MapHeight * FixedPoint.Scale
marginRaw    = jitterRaw + BodyRadiusRaw

fits  <=>  trailBaseX - marginRaw >= BodyRadiusRaw
       and trailBaseX + marginRaw <= mapWidthRaw  - BodyRadiusRaw
       and trailBaseY - marginRaw >= BodyRadiusRaw
       and trailBaseY + marginRaw <= mapHeightRaw - BodyRadiusRaw
```

When `fits` is false, **no member of that contingent receives a cohesion
destination this tick** and every one of them reverts to independent pursuit —
the same total degradation the duty cycle produces, for the same reason and with
the same consequence. `ResolveContingentStates` applies the identical test and
records `Advance` rather than `Hold`, so the inspector never reports a
contingent as `Holding` while its members are in fact pursuing independently.

Four things about this test are deliberate.

**It is four exact integer comparisons.** There is no tolerance, no epsilon, and
no floating point. `marginRaw` adds `BodyRadiusRaw` to `jitterRaw` because the
clamp interval is itself inset by one body radius, so a square that merely
reaches the map edge would still have its outermost aim points clamped.

**Its boundary case is stated and pinned.** The comparisons are non-strict, so a
square that fits exactly — touching the legal interval's endpoint on an axis —
counts as fitting and cohesion proceeds. That is the correct side: at exact
equality no aim point is outside the interval, so `ClampCenterToBounds` returns
its argument unchanged and no collapse occurs. A test asserts the predicate is
true at exact equality and false one raw unit beyond it, on each of the four
comparisons independently.

**It removes the failure rather than shrinking it.** The alternative considered
was reducing `jitterRaw` until the square fits. That was rejected: shrinking the
square raises the density inside it, which is the input to the packing proof, so
it trades a geometric collapse for a packing collapse — and in a corner, where
both axes are constrained, no positive `jitterRaw` avoids the one-dimensional
case. Turning cohesion off degrades to the frozen preset's individual pursuit,
which has no known deadlock, and it is the same escape section 10.2 already
relies on for `Close` and `Break`. Section 9 records the rejection.

**Its degradation is total and safe, including in the degenerate case.** On a
map too small to hold any contingent's bias square, `fits` is false on every
tick for every contingent and the preset behaves exactly as
`IndependentPursuitV1` does. That is a silent loss of a feature, not a stall,
and it is asserted by a test rather than assumed.

One residual is stated honestly rather than argued away. The give-way aim point
computed by `TryComputeGiveWayAimPoint` is still clamped, and the map-edge test
does not cover it, because a straggler can sit outside its own contingent's
bias square. The exposure is bounded: that step is
`corridorHalfWidthRaw + BodyRadiusRaw`, which is three body radii, and it is
purely perpendicular to the leader's line of travel. A clamp can therefore
shorten one member's sideways escape by at most three body radii and can never
place it outside the map. It does not recreate the head-on mutual block, because
the leader is still advancing under its own exemption and the member's position
has changed by at most that same three body radii. The engineered corner test in
section 10.3 exists specifically to exercise this residual rather than to take
the paragraph's word for it.

#### The cross-contingent test, and the combined-density argument

The map-edge gate guards the packing proof against the ground running out. It
does nothing at all about the other way the proof's hypothesis fails: a second
contingent's bias square lying on top of this one. `IsCohesionSquareWithinBounds`
is four comparisons against `mapWidthRaw` and `mapHeightRaw` and it never asks
whether anybody else is there.

That uncovered case is not exotic. It is precisely the geometry of failure shape
2 in section 10.2, and the geometry deserves stating carefully because it is
counter-intuitive. Two contingents of the same faction advancing on the same
enemy share a broadly similar heading. Each places its bias square **behind** its
own leader, opposite its own direction of travel. Because the headings are
similar, "behind" is the same side of the map for both. So when two leaders'
paths cross, their trailing squares do not separate on the far side of the
crossing — the trailing region is the natural place for the two contingents'
followers to overlap, not the natural place for them to stay apart. Body density
in the shared region can then approach twice what the per-contingent packing
bound allows — that is, half the geometric capacity of the square rather than
the quarter `FormationRules` records as the safe headcount. That is not yet
Deadlock A's regime, which was observed at a headcount equal to *full* capacity
and ran 9,975 ticks blocked
(`src/Hukbo.Core/Simulation/FormationRules.cs:26-37`). It is halfway there, and
it spends the whole of the fourfold margin that exists precisely because offsets
drawn at random do not pack. A margin that has been spent is not a margin, and
the packing proof stops being a proof about the region in use.

The gate is therefore the same shape as the map-edge gate, applied to a pair
instead of to a boundary. Every contingent already has, from this tick's own
`ResolveContingentStates` pass, an unclamped trail base and a half-side
`marginRaw = jitterRaw + BodyRadiusRaw` — the identical margin the map-edge gate
uses, for the identical reason. The squares are axis-aligned and centred on
those trail bases, so two of them overlap exactly when their centres are close
enough on **both** axes:

```
overlaps(a, b)  <=>  |aTrailBaseX - bTrailBaseX| <= aMarginRaw + bMarginRaw
                and  |aTrailBaseY - bTrailBaseY| <= aMarginRaw + bMarginRaw
```

When a contingent's square overlaps the square of **any** other living
same-faction contingent, **no member of that contingent receives a cohesion
destination this tick** and every one of them reverts to independent pursuit —
the identical total degradation the map-edge gate and the duty cycle produce,
down the identical path, for the identical reason. `ResolveContingentStates`
records `Advance` rather than `Hold` in that case, exactly as it does for the
map-edge denial, so the inspector never reports a contingent as `Holding` while
its members are in fact pursuing independently.

Seven things about this test are deliberate, and one consequence follows from
them.

**It is exact integer arithmetic on axis-aligned squares.** Two absolute
differences and two comparisons, all `long`. No tolerance, no epsilon, no
floating point, no square root, and no distance — the same discipline the
map-edge gate and the straggler test already keep. Both trail bases are
saturated `Int32` values and both margins are bounded by the
`IsBodyRadiusWithin*Range` guards `Scenario.Validate` enforces, so neither
difference nor either sum can overflow `long`.

**Its boundary case is stated and pinned: exact edge contact counts as
overlapping.** The comparisons are non-strict, so two squares that merely touch
along an edge deny each other cohesion. That is the opposite convention from the
map-edge gate's, and deliberately so, because the safe side is the opposite
side. In the map-edge case, exact equality means no aim point falls outside the
clamp interval, `ClampCenterToBounds` returns its argument unchanged, nothing
collapses, and cohesion is safe to grant. Here, exact contact means the two
squares share a boundary line, and two aim points can land on that line at the
same integer coordinates — so exact contact is the first separation at which the
combined-density statement below is no longer strictly true. Choosing
"overlapping" at equality can only ever remove a cohesion destination, never add
one, which is the same safety direction the straggler test's strictness takes. A
test asserts the predicate is true at exact contact and false one raw unit
farther apart, on each axis independently.

**It is symmetric, and both contingents yield.** `|aTrailBaseX - bTrailBaseX|`
and `aMarginRaw + bMarginRaw` are both symmetric in `a` and `b`, so the
expression that decides whether `a` sees `b` as overlapping is the same
expression, on the same values, that decides whether `b` sees `a`. The two can
never disagree, and no ordering rule is needed to make them agree. Both
contingents therefore lose cohesion on the same tick. The asymmetric
alternative — the lower `ContingentId` keeps cohesion and the higher yields —
was considered and rejected, and section 9 records it: it would leave one full
contingent still parking a headcount's worth of aim points inside the shared
region while the yielding contingent's members are standing in that same region,
so it would not restore the combined-density bound at all. It would buy a little
more cohesion at the cost of the only property the gate exists to establish.

**It has a total order and iterates no hash container.** The pairwise scan walks
the same dense sixteen-slot array every other part of this stage walks, outer
index ascending and inner index ascending from `outer + 1`, over slots belonging
to the same faction **and having at least one living member**. The living filter
is not decorative: a slot whose living count is zero still holds a leader, a
trail base and a margin left over from whichever tick it last had a living
member, and comparing against those stale values would deny cohesion on the
strength of a square that no longer exists. Because the predicate is symmetric and each contingent's
result is a boolean accumulated by logical OR, the answer does not depend on the
scan order at all; the ascending walk is specified so that there is nonetheless
one fixed order, and so that no `Dictionary` or `HashSet` is iterated anywhere.

**It is computed once per contingent per tick, in `ResolveContingentStates`, not
once per agent in `GatherMovementProposals`.** Two reasons, and both are binding
rather than stylistic. First, it is a property of a contingent *pair*, and no
pair can be evaluated until every contingent's leader, living count and trail
base for this tick are known — which is true only after that stage's forward
passes have finished, and is never true part-way through a per-agent loop that
has not yet reached the other contingent's members. Second, evaluating it per
agent would repeat the same bounded set of comparisons up to two hundred times a
tick on the canonical workload to produce an answer that cannot differ between
two agents of the same contingent. The stage stores one `bool` per slot; gate 6
is a single array read.

**It allocates nothing.** The trail bases, the margins and the result flags all
live in arrays sized once at construction, exactly like every other sixteen-slot
array the stage owns. A warm tick allocates nothing, which is what
`BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations` enforces.

**It is not vacuous at deployment, and the arithmetic says so.** A gate that
denied cohesion from tick 0 forever would silently delete the feature.
`FormationPlanner` deploys contingents in lanes stacked along Y — `anchorY =
region.MinY + laneSpan * contingent + laneSpan / 2`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:292-293`) — with alternating
depth along X. On the canonical workload the deployment region is
`720 * FixedPoint.Scale - 2 * BodyRadiusRaw = 729,088` raw units tall, split
across five contingents, so adjacent lane centres are `145,817` raw units apart,
about 35.6 body radii. Two squares touch at `2 * marginRaw = 20` body radii of
centre separation. The lanes are therefore disjoint by a comfortable margin at
tick 0, and the gate fires only when contingents genuinely drift into each other
during the battle — which is the case it exists for. Section 10.4 carries the
residual risk that some other map or population makes it vacuous, and section
10.3 carries the test that would catch it.

**And the consequence — the combined-density argument, stated at exactly the
strength it has.** With cohesion denied to *both* contingents whenever two
same-faction squares overlap, every square that actually grants a cohesion
destination is disjoint from every other same-faction square that grants one. No
point of ground is the aim region of two cohering contingents at once.

That is a precise statement and it is worth separating from the larger statement
it is easy to mistake it for.

**What it proves.** Every bias square that is in use as an aim region satisfies
the quarter-density hypothesis *with respect to its own contingent's aim
points*. The per-contingent bound — bodies covering at most a quarter of the
square, by `capacity = m^2 > 4 * livingCount` — is a bound on the aim points
placed inside that square, not merely on one contingent's arithmetic considered
in isolation, because no second contingent is placing aim points there. Combined
with the map-edge gate, which guarantees the square is a square rather than a
collapsed line, the two gates restore the packing proof's hypothesis on the two
ways that hypothesis can fail *through aim points*.

**What it does not prove.** It says nothing whatever about bodies that are
standing in, or walking through, a granted square without being aimed at it. The
gate makes a square unshared as an aim region; it does not make it unoccupied,
and it never asked whether it was occupied. An earlier revision of this document
closed that gap with the sentence "this, and not the duty cycle, is what carries
the density argument", which claimed more than the gate establishes and repeated,
one section over, the exact error section 10.2 had just retracted for the duty
cycle: an assertion about what the collision resolver would do with agents that
are not aimed at the region, offered as licence for a density argument. That
sentence is withdrawn. Escape 6 is rated **partial** for failure shape 2 in the
section 10.2 table for this reason, not **yes**.

**What the liveness case therefore actually rests on.** It rests on the
combination, and on a test, and it is an argument rather than a proof:

- gates 5 and 6 together bound the *aim-point* density of every granted square
  at a quarter of that square's geometric capacity;
- escapes 1, 2 and 3 reduce how often a square is granted at all;
- escape 4 bounds how long any grant can persist, unconditionally;
- the fourfold packing margin — a quarter, not a half and not the whole — is
  what absorbs the unaimed traffic the gates do not see, and it is deliberately
  four rather than merely sufficient for exactly that reason;
- and the residual that the margin is absorbing rather than excluding is
  exercised by a deliberately engineered test, described in section 10.3, rather
  than left as a sentence.

That is the honest shape of it. This document does not claim a proof that the
collision resolver grants forward movement inside a granted square, and it never
did have one; what it claims is that the density regime the resolver is asked to
work in is the regime the packing margin was solved for, plus a headroom
allowance for traffic, plus a test that says whether the allowance held.

Two residuals are stated rather than argued away.

**Independently-pursuing agents still cross a cohering square, and this is not
bounded.** A member of a `Break` contingent, a member of a `Close` contingent, a
member of a contingent whose duty-cycle window is shut, a non-straggling member
under `Advance`, and a member of a third same-faction contingent whose own square
is disjoint from this one can each walk straight through a granted square. An
earlier revision asserted that such traffic "is transient and does not park a
second headcount there". That assertion is withdrawn: it is a claim about what
the collision resolver does with agents the gate never examined, and no
arithmetic in this document supports it.

What can honestly be said is narrower, and it is stated as a bound on the
*claim*, not on the traffic:

- The traffic's aim points are outside the square, so the traffic contributes no
  aim points to the packing count. Aim-point density inside a granted square is
  therefore still bounded at a quarter of capacity.
- Body density inside a granted square is **not** bounded by anything in this
  design. It is bounded only in the sense that every body in the map is subject
  to the collision resolver's own non-penetration rule, which is a statement
  about overlap, not about forward progress.
- The fourfold margin is the allowance. Three quarters of a granted square's
  geometric capacity is unoccupied by its own contingent's aim points, and that
  headroom is what the traffic is expected to move through. Whether it suffices
  is not derived here.

Because it is not derived, it is tested. Section 10.3 carries a third engineered
scenario — a currently-cohering contingent whose bias square is granted, with a
stream of independently-pursuing same-faction members from a different
contingent routed directly through that square — which asserts a terminal
outcome and, in a companion fact, confirms that the cohering contingent really
was granted cohesion on the ticks when the traffic was inside its square. That
test is the whole of the evidence for this residual. It is a bound obtained by
construction and measurement, not by argument, and if it fails the design does
not ship as written.

**Cross-faction overlap is not tested, and that is a scope decision rather than
an oversight.** Two opposing contingents whose squares converge are, by
construction, walking toward each other's members, which drives
`nearestEnemySquared` down until transition rule 3 fires and `Close` switches
cohesion off for both. Failure shape 2 is dangerous precisely because it has no
enemy to trigger that; the mirrored cross-faction case has one by definition.
Adding faction-crossing pairs to the scan would double the comparison count to
reduce a risk rule 3 already removes.

#### Chain denial, many contingents at once, and the inertness risk

The analysis above covers two contingents. A faction may field eight, and the
case that matters for the product rather than for deadlock safety is what
happens when several of them are close together at the same time. This
subsection settles that case explicitly, because denial is always safe and
therefore never fails loudly — a feature that is switched off everywhere passes
every liveness test in section 10.3 while doing nothing at all, and that is a
product failure even though it is deadlock-safe.

**First, a correction of framing, because the word "chaining" suggests a
mechanism the rule does not have.** Suppose square A overlaps square B, square B
overlaps square C, and A does not overlap C. All three are denied. That is not
transitivity and nothing propagates: A is denied because A genuinely overlaps B,
C is denied because C genuinely overlaps B, and B is denied because it genuinely
overlaps both. Each denial is its own pairwise fact. There is no rule anywhere in
this design under which A is denied *because C was denied*, and consequently
there is no "pairwise rather than transitive" alternative available — the rule is
already pairwise, and the logical OR over a contingent's own pairs is what makes
it so. The alternative that does exist is the asymmetric one, where the lower
`ContingentId` of an overlapping pair keeps cohesion and the higher yields, and
section 9 records why that was rejected: it leaves one contingent parking a full
headcount of aim points in a region the yielding contingent's members are
standing in, which abandons the only property the gate exists to establish. At
chain length three the asymmetric rule is worse still, because the middle
contingent B would keep cohesion against A and yield to C, or the reverse,
depending on an arbitrary index comparison.

**The OR rule therefore stays.** What follows is the cost of keeping it, stated
rather than discovered later.

**The cost is inertness during a converging advance, and it is not bounded by an
argument.** Eight contingents of one faction advancing abreast toward a shared
engagement point are all closing on the same region, and their trailing bias
squares are all on the same side of their leaders because the headings are
similar — the same geometry that makes failure shape 2 dangerous. If their
squares meet, every one of them is denied, simultaneously and for as long as the
convergence lasts. Cohesion would then be inert during exactly the phase of the
battle the user asked it to shape.

Three things push against that, and none of them is a proof:

- **Deployment separation is comfortable on the canonical workload, with room to
  spare.** `FormationPlanner` deploys contingents in lanes stacked along Y, and
  section 3.5 works out that adjacent lane centres are about 35.6 body radii
  apart against a touching threshold of `2 * marginRaw`, which is 20 body radii
  for a twenty-member contingent. The lanes are disjoint at tick 0 by roughly
  three quarters of the threshold.
- **The squares shrink as the battle proceeds.** `jitterRaw` is solved from the
  living count, so attrition strictly reduces `marginRaw` and therefore strictly
  reduces the separation at which two squares touch. A denial caused by crowding
  early is more likely to clear later than to worsen.
- **The advance is along X while the lanes are stacked along Y.** Two contingents
  converge laterally only insofar as their members select the same nearest enemy,
  and nearest-enemy selection is itself lane-local for most of the approach.

What none of that establishes is a bound. Whether the lanes stay separated
through the whole advance is a property of the map, the population and the
enemy's deployment, not of this design, and it is exactly the kind of claim this
document has twice been wrong about when it argued instead of measuring. So the
guard is a measurement with a failing threshold, not a paragraph: section 10.3
carries a multi-seed inertness bar that requires cohesion to be granted to a
stated fraction of each faction's contingents on a stated fraction of the ticks
during which it is possible at all, and that bar fails loudly if the gate goes
universal. Section 10.4 records the risk, and smoke row 114 asks a person
whether the groups read as groups for the whole advance rather than only at
deployment.

**One narrowing was considered and deliberately not adopted, and the reasoning
is recorded so a later measurement can overturn it on evidence.** The scan walks
every **living** contingent — a contingent with at least one living member —
which includes contingents in `Close` and in `Break`. Those contingents can never
receive a cohesion destination, because gate 1 sends every one of their members
to independent pursuit, so their squares park no aim points and their presence
cannot violate the combined-density statement, which is a statement about
*granted* squares only. Restricting the scan to contingents that could actually
be granted cohesion would therefore preserve the density statement exactly, and
would materially reduce the inertness risk described above — a faction whose
leading contingents have reached the enemy and entered `Close` would stop denying
its own rear contingents.

It is not adopted, for one reason: it moves the `Close` and `Break` members
standing in a granted square out of "excluded by gate 6" and into the residual
above, which is the one thing in this design with no bound at all. Loading more
onto an unbounded residual to buy cohesion is the wrong trade to make before the
residual has been measured. **The narrowing is the first remedy if the inertness
bar in section 10.3 fails**, and it is pre-analysed here so that adopting it is a
recorded decision against measured evidence rather than a fresh design argument.
Section 13 asks the user to confirm the ordering.

#### The clearance derivation

This is the load-bearing part of the trail:

The offset is drawn independently per axis from `[-J, +J]`, so its projection
onto any direction — including the leader's direction of travel — is at most
`J * sqrt(2)`, reached on the diagonal. The trail must exceed that projection
plus two body radii of contact distance, or a worst-case offset places a follower
in front of its own leader. `FormationRules.cs:61-79` states the general
inequality as `trailMultiplier > jitterMultiplier * sqrt(2) + 2`.

Here `trailRaw >= (3/2) * jitterRaw + 3R`, because `(3 * jitterRaw + 1) / 2`
rounds up. Since `3/2 = 1.5 > 1.41422 > sqrt(2)`, and `3R > 2R`, the inequality
`trailRaw > jitterRaw * sqrt(2) + 2R` holds strictly for every `jitterRaw >= 0`
and every `R >= 1`. No floating point is used to establish it: the rational `3/2`
is an exact integer-safe upper bound on `sqrt(2)`, and the surplus `R` absorbs the
at-most-one-raw-unit loss from the integer division. A test asserts the inequality
numerically across the full body-radius range rather than trusting this paragraph.

#### The give-way rule

The trail alone is still not enough. `FormationRules.cs:80-95` records the third
observed failure: a follower can start a tick already *ahead* of its leader, in
which case its trail point lies on the far side of the leader's body, so
straight-line movement plus solid collision produces a head-on mutual block that
never clears — the leader blocked forward by the follower, the follower blocked
backward by the leader.

The existing fix is `TryComputeGiveWayAimPoint`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:966-1020`): a follower whose
tick-start position projects forward of its leader along the leader's direction of
travel, and whose lateral distance from that line is under
`RallyCorridorHalfWidthMultiplier * BodyRadiusRaw` (two body radii, the same span
two solid bodies use to decide they are touching), steps purely sideways —
forward position unchanged — toward the side it is already on. A follower exactly
on the axis steps toward the fixed positive perpendicular side, deterministically,
regardless of array order; `LastStandFormationTests.cs:908-954` pins that
tie-break.

This design generalises that helper from "the faction's rally agent" to "any
leader" and applies it to contingent cohesion unchanged. It is also, and not
coincidentally, the one give-way behaviour the historical research records as
plausible for this period — avoiding blocking a companion's movement or weapon.
`FormationRules.cs:87-95` already carries that citation and this design inherits it.

### 3.6 Local steering

Three improvements were asked for. Each is scoped tightly, and one is deferred
with a reason rather than half-built.

**Arrival slowdown — in scope.** Under the persistent-contingent preset only,
`BuildMovementProposal` gains a taper on the final approach. The arithmetic
itself lives in a pure internal static,
`MovementRules.ComputeArrivalStepRaw(remainingRaw, movementSpeedRaw, taperRaw)`,
so it can be swept directly by a test rather than only observed through a whole
simulation — the same testability shape `FormationRules` already uses for the
rally geometry, and the reason `Hukbo.Core` carries
`[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]`
(`src/Hukbo.Core/Properties/AssemblyInfo.cs:3`).

```
remaining = Max(1, distance - stopShortRaw)
taperRaw  = ArrivalTaperMultiplier * BodyRadiusRaw        (multiplier = 4)

if remaining < taperRaw:
    movement = Max(1, Min(MovementSpeedRaw, remaining) * remaining / taperRaw)
else:
    movement = Min(MovementSpeedRaw, remaining)
```

All integer, all `long` intermediates. The taper can only ever *reduce* a step,
so it cannot cause tunnelling and cannot break `Scenario.Validate`'s
`MovementSpeedRaw <= BodyRadiusRaw` invariant
(`src/Hukbo.Core/Simulation/Scenario.cs:326-343`). The `Max(1, ...)` preserves the
existing one-raw-unit movement floor, so the arrived-guard semantics that stop a
settled cluster from emitting a `Move` event every tick are unchanged. It is O(1).
Visible effect: warriors ease into contact instead of snapping to a halt against
an enemy body.

**Neighbour separation — in scope, delivered by the offset, not by a force.**
Members never converge on one point, because each has its own stable offset drawn
over a square sized so bodies cover at most a quarter of it. That is the
separation rule. A Reynolds-style repulsion force is explicitly *not* added, and
section 9 records why.

**Flow around blockers — partly in scope, partly deferred.** What ships is the
give-way rule above, which steps a member aside rather than through its own
leader, plus the collision resolver's existing single-axis slide candidates, which
already produce sideways flow around a blocker without any new code. What does
*not* ship is a general "step around whichever ally is in the way" rule. That
needs a bounded neighbour query, which needs a second uniform-grid rebuild inside
the movement stage, and the collision stage is already 63–75% of tick time with a
rising super-linear exponent. `docs/plans/2026-07-28-collision-resolution-scaling-design.md`
has reserved that area for its own optimization pass, which is not yet
implemented. Building a competing second grid before that lands is how two
workstreams collide. It is listed in section 11 as out of scope and in section 13
as an open question for the user.

Nothing here is pathfinding. No graph, no grid search, no route, no waypoint, no
walkability. CLAUDE.md section 9 and `SIMULATION-GAME-STANDARDS.md:372-374` gate
pathfinding behind a milestone that has not been reached, and every rule in this
design is a pure function of the current tick's positions.

### 3.7 Complete ordering and tie-break summary

| Decision | Order | Tie-break |
| --- | --- | --- |
| Contingent slot iteration | ascending `FactionId * 8 + ContingentId`, dense array | none possible |
| Leader selection | one forward scan, compare `EntityId` explicitly | none possible; `EntityId` is unique |
| Spread measurement | maximum over living members of a `long` scalar | value only; the identity of the farthest member is never used |
| Nearest-enemy trigger | minimum over living members of a `long` scalar | value only |
| Unit-state transition | the six numbered rules in 3.4, first match wins | fixed priority, no ambiguity |
| Cohesion duty cycle | a pure function of `Tick` and `slot`; no iteration | none possible |
| Straggler test | per agent, one `long` comparison against a `long` product | strict inequality; exact equality is not straggling |
| Map-edge open-ground test | per contingent, four `long` comparisons against the unclamped trail base | non-strict; a square that fits exactly counts as fitting |
| Cross-contingent overlap test | per same-faction pair, over the dense sixteen-slot array, outer index ascending and inner index ascending from `outer + 1`; result accumulated per contingent by logical OR | non-strict; squares in exact edge contact count as overlapping. The predicate is symmetric, so both contingents yield and no order-dependent tie-break exists |
| Give-way side | sign of the member's existing lateral offset | exactly on axis breaks to the fixed positive perpendicular side |
| Movement proposal order | ascending agent-array index, which is ascending `EntityId` | unchanged from today |
| Collision request order | strictly ascending `EntityId` | unchanged; enforced by `CollisionResolver.Validate` |

No `Dictionary` or `HashSet` is iterated anywhere in the new code. The existing
`_agentIndexes` dictionary is used only for `EntityId`-to-index lookup, which is
the sanctioned lookup-only use documented at
`SIMULATION-GAME-STANDARDS.md:919-940`.

---

## 4. Historical position

Every claim below carries its evidence tier. The tiers are the ones
`docs/research/battles/` uses and CLAUDE.md section 7 requires.

### 4.1 What supports the design

| Design element | Historical basis | Tier | Source |
| --- | --- | --- | --- |
| An army arrives as several practical groups | People who share a boat begin as a group and may remain mutually aware; transport basis is attested | **Plausible inference**, resting on a **Strong reconstruction** | `docs/research/battles/03-deep-past-formations-and-tactics.md:291-298` |
| Followers orient on a known leader, whose movement affects local cohesion | Personalized alliance and following | **Plausible inference**, resting on a **Strong reconstruction** | `docs/research/battles/03-deep-past-formations-and-tactics.md:300-307` |
| Cohesion is local and relational, not an army-wide constant | Personal attachment, kinship, shared risk in the same boat, reputation before peers | **Plausible inference** on a **Strong reconstruction** political model | `docs/research/battles/02-deep-past-forces-and-command.md:236-250` |
| Several contingents with stronger internal than cross-contingent cohesion; irregular spacing; leaders embedded near their own followers; local advance and hesitation rather than simultaneous army-wide motion | Open-ground minimum defensible geometry | **Plausible inference** throughout | `docs/research/battles/03-deep-past-formations-and-tactics.md:177-193` |
| A warrior who has fallen well behind his group closes back up; a warrior who is keeping up simply keeps going | The same passage's "irregular spacing" and "local advance rather than perfectly simultaneous army-wide motion" — the movement is individually judged, not collectively timed | **Plausible inference** | `docs/research/battles/03-deep-past-formations-and-tactics.md:177-193` |
| Mass-then-close: a dispersed approach tightens into a local cluster when a threat appears, then closes as distance falls | Formation transitions table | **Plausible inference**; the travel-to-dispersed step is a **Strong mechanical reconstruction** | `docs/research/battles/03-deep-past-formations-and-tactics.md:394-400` |
| Give way rather than block a companion's movement or weapon | Small-unit cooperation | **Plausible inference** | `docs/research/battles/03-deep-past-formations-and-tactics.md:371-388` |
| Avoid entangling a shaft, avoid striking a companion | Weapon-affordance judgements | **Plausible inference** | `docs/research/battles/04-deep-past-individual-combat.md:159,174` |
| Approach from different available directions; do not crowd a companion so tightly that weapons collide; maintain escape space | Many-versus-one cooperation | **Plausible inference** | `docs/research/battles/04-deep-past-individual-combat.md:286-301` |
| A contingent that loses enough members stops acting as one | Possible withdrawal when a contingent's leader, boat, or expected reward is lost | **Plausible inference** inside a cautious large-force model | `docs/research/battles/02-deep-past-forces-and-command.md:298-308` |
| Individual champions stepping out as an emergent consequence of proximity | Two opponents can become each other's immediate threat inside a melee | **Plausible inference** | `docs/research/battles/04-deep-past-individual-combat.md:262-284` |

The design is careful about what it does **not** claim here. Nothing in the
research supports a group moving as a bound body, and the design does not
produce one: during `Advance`, a member that is keeping up is never given a
cohesion destination at all, so the ordinary state of a contingent on the march
is a set of individually-steering warriors who happen to share an origin and a
rough heading. Cohesion is the exception that fires for a member who has fallen
behind, and it stops firing entirely for sixty ticks out of every two hundred
and forty. That is why the row above claims only what the passage supports.
Continuous leader-relative binding of every member on every tick would be a
loose column marching in near lockstep, which is the drilled coordination
CLAUDE.md section 7 forbids, and it is not what this design specifies.

Every tuning constant introduced by this design — `CohesionRadiusMultiplier`,
`CloseRadiusMultiplier`, `MinimumCohesiveMembers`, `CohesionCycleTicks`,
`CohesionDutyTicks`, `ArrivalTaperMultiplier`, the attrition quarter, and the
hysteresis three-quarters that the straggler threshold also reuses — is a
**Provisional reconstruction**: a game-design invention, not a measurement. No
source describes a cohesion radius, a distance at which a warrior is judged to
have fallen behind, a gathering interval, or a headcount at which a group stops
acting together. Each constant carries that statement in its own XML doc
comment, matching the pattern `FormationRules.cs:1-8` and
`FormationPlanner.cs:14-30` already use.

### 4.2 What is not attested, and is therefore absent

This paragraph exists because the policy is only load-bearing if it excludes
things. The following are explicitly recorded as **not attested** for this place
and period, and none of them appears in this design in behaviour, in a constant,
in a data structure, or in a player-facing label:

**regular files; regular ranks; fixed frontage; fixed depth; a shield wall; a
spear block; a bow or javelin screen; cavalry; a formal reserve; encirclement
doctrine; alternating missile and melee lines; named naval formations;
coordinated boarding sections; standardized dueling lanes; an archipelago-wide
formation system** (`docs/research/battles/03-deep-past-formations-and-tactics.md:61-76`),
and equally **a phalanx-like spear square; a Roman-style shield testudo; a
European pike block; a ranked firing line; a formal wedge; a checkerboard
infantry system; a cavalry wing; a dedicated skirmisher screen; a standardized
naval line or crescent; a national "barangay formation"; a ritualized one-on-one
duel replacing group battle** (`docs/research/battles/03-deep-past-formations-and-tactics.md:425-441`).

Three consequences follow and they constrain the implementation directly.

**No slot, no geometry template.** A member's aim point is the leader's position
plus a trail plus that member's own pseudorandom offset. There is no lattice at
runtime, no ring, no arc, no frontage width, no depth, and no position index. The
tick-0 lattice in `FormationPlanner` remains what its own remarks already say it
is — an engineering device for guaranteeing non-overlap before the first tick, not
a reconstruction of how anyone stood.

**No signalled command.** `docs/research/battles/02-deep-past-forces-and-command.md:220-234`
lists shouted command vocabulary, horns, gongs, drums, flags, fires, messenger
organization, prearranged manoeuvres, a command radius, a formal reserve, and
replacement-of-a-fallen-leader doctrine as unknown or unsupported. The unit state
in this design is therefore a **simulation abstraction for what a group visibly
does**, never a recovered order or signal. It is never labelled a command, an
order, or a signal in code, in a comment, or in the inspector.

**No scripted champion duel.** `docs/research/battles/04-deep-past-individual-combat.md:262-284`
supports only an emergent, unplanned one-versus-one arising inside an ongoing
melee, and explicitly rejects a formal pre-battle challenge, a noninterference
rule, an honour code requiring equal weapons, a referee, a bounded dueling
ground, and a standardized challenge–exchange–surrender sequence. What this design
produces instead is genuinely emergent: a `Break` contingent's members revert to
individual pursuit, and a member whose nearest enemy is also targeting it back
becomes a one-versus-one for as long as the geometry lasts. Nothing scripts it and
nothing protects it.

### 4.3 Naming, and why the labels are plain English

CLAUDE.md section 7 requires a cultural identification to appear only in pair
form — a Filipino name, an em dash, a plain English descriptor — with a recorded
evidence tier, and forbids any name whose earliest attestation postdates the
depicted period by more than a century. That rule is what excluded the panabas.

No attested Filipino-language term for a contingent, for a contingent leader, or
for any of advance, hold, close, or break appears anywhere in the four battle
research documents or in `docs/research/HISTORICAL_1500s_WEAPONS.md`. The only
lexical candidates in the corpus are proto-language reconstructions — `*ayaw`
("raid, go headhunting") and `*bákal` ("spear used in warfare") — and the sources
themselves state that such reconstructions cannot date a specific campaign,
locate a practice in every island, or recover formation, command, ritual, or
technique (`docs/research/battles/01-deep-past-overall-warfare.md:186-206`;
`docs/research/battles/03-deep-past-formations-and-tactics.md:161-167`). They carry
no fixable century-scale attestation date and therefore cannot clear the bar a
dated sixteenth-century document clears.

The honest outcome is that **no legitimate pair-form label can be constructed for
any of these concepts**, and inventing one would be exactly the failure the
panabas exclusion exists to prevent. The player-facing labels are therefore plain
English and make no cultural claim at all: `Contingent`, `Advancing`, `Holding`,
`Closing`, `Broken`. The preset name is likewise plain English and describes the
mechanism rather than a historical arrangement.

---

## 5. The determinism argument

### 5.1 Total orders

Every multi-result query introduced here has one, listed in the table at 3.7. The
two that matter most:

- **Leader selection** is a forward scan comparing `EntityId` explicitly. A
  permuted `_agentStates` array cannot change the answer, and the equivalent
  property is already pinned for the rally agent by
  `DeterminismTests.InputArrayOrderCannotChangeOrderedResults` and
  `LastStandFormationTests.RallyAgentSelectionIsUnchangedByAgentArrayPermutation`.
- **Contingent iteration** is over a dense array of sixteen slots indexed by
  `FactionId * 8 + ContingentId`, walked in ascending index. There is no map, no
  set, and no insertion order.

`SIMULATION-GAME-STANDARDS.md:919-940` permits a hash container for lookup only,
provided whatever is iterated is a separate ordered collection and the separation
is documented at the owning symbol. This design introduces no new hash container
at all, so that rule is satisfied vacuously.

### 5.2 Random draw accounting

The complete inventory of random draws on the movement path today:

1. `CollisionPriority.Resolve(seed, tick, entityId)` — a deterministic hash mix,
   not a `SplitMix64` draw, used purely as a sort key. Once per mover per tick,
   inside `ResolveCollisions`, in ascending agent-array order
   (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1039-1071`).
2. `RallyOffset.Compute(seed, entityId)` — one `SplitMix64` initialisation and
   two `NextInt` draws, per `Regrouping` agent whose give-way check failed,
   inside `GatherMovementProposals`, in ascending agent-array order
   (`src/Hukbo.Core/Simulation/RallyOffset.cs:43-61`).
3. `FormationPlanner.NextJitter` — up to two draws per warrior from the caller's
   stream, once at `Create`, in ascending faction-local index
   (`src/Hukbo.Core/Simulation/FormationPlanner.cs:296-297`).

This design adds exactly one:

4. `ContingentOffset.Compute(seed, entityId, jitterRaw)` — one `Fnv1a` mix, one
   fresh `SplitMix64` seeded from it, and two `NextInt` draws. Called exactly
   once for each living agent that passes all six gates in section 3.5 — that
   is, an agent whose `Intent` is `Moving`, which is not its contingent's
   leader, whose contingent's state is `Hold`, or is `Advance` with that agent
   strictly beyond the straggler threshold, whose contingent's duty-cycle
   window is open, whose bias square fits inside the map, and whose bias square
   does not overlap another living same-faction contingent's. It is not called
   for any other agent. The calls happen inside `GatherMovementProposals`, in
   ascending agent-array order, **and only under the persistent-contingent
   preset**. Note that a member that takes the give-way branch does not reach
   the offset at all, exactly as a `Regrouping` follower that takes the
   give-way branch never reaches `RallyOffset.Compute` today.

**The number of these calls per tick varies with the simulation's own state,
and that is safe — but only because of how the draw is constructed, so the
argument is spelled out rather than assumed.** Two facts together settle it.

First, the call count is itself deterministic. Every input to the six gates —
positions, `Intent`, `ContingentState`, living counts, the tick, the slot, and
every contingent's trail base — is
authoritative state that the same seed and the same build reproduce exactly. A
varying count is not a varying *decision*; it is a fixed function of a
reproducible state.

Second, and this is the property that actually matters, the count cannot affect
any value anywhere. `ContingentOffset.Compute` seeds a **fresh** `SplitMix64`
from `Fnv1a(ContingentTag, seed, entityId)` and advances **no shared stream**.
Skipping a call, adding a call, or reordering the calls therefore cannot shift a
single value seen by any other consumer, or by any later call to
`ContingentOffset.Compute` itself. This is the construction
`SIMULATION-GAME-STANDARDS.md:727-733` mandates for exactly this reason and that
`RallyOffset` already uses — and it is why the design needed no restructuring
once the straggler gate made the call count state-dependent. The offset is
*derived*, not drawn from a stream, so the question of "how many draws this
tick" has no consequence to answer. A test pins this directly by computing one
agent's offset in isolation and again after computing a thousand other agents'
offsets, and requiring the same value.

Had the offset instead consumed from a shared per-run generator, a
state-dependent call count would have been a genuine determinism hazard, and the
correct fix would have been to draw every agent's offset once at deployment.
That fix is unnecessary here and is not adopted.

The critical properties:

- It uses its own fresh 64-bit domain tag, `HKBO_CTG` = `0x484B424F5F435447`,
  distinct from `HKBO_HIT`, `HKBO_CLS`, the last-stand tag, and the
  collision-priority tag. Reusing an existing tag would correlate unrelated draws
  (`SIMULATION-GAME-STANDARDS.md:786-792`).
- It seeds a **fresh** generator per call and does not advance any shared stream,
  so it cannot shift the values any other consumer sees. This is the same
  construction `RallyOffset` uses.
- **It excludes the tick, deliberately.** That decision is made explicitly and
  for the reason recorded at `SIMULATION-GAME-STANDARDS.md:729-733` and
  `src/Hukbo.Core/Simulation/RallyOffset.cs:11-21`: a tick-keyed offset moves
  every member's aim point every tick and reproduces a documented jitter-and-stall
  failure. A test pins the property.
- Under the frozen preset the call is never reached, so the frozen preset's draw
  count, draw order, and every drawn value are byte-identical to today's.

`System.Random` appears nowhere. `DeterministicRandomTests.cs:8-28` pins the
`SplitMix64` reference vectors — seed 1 yielding `0x910A2DEC89025CC1`,
`0xBEEB8DA1658EEC67`, `0xF893A2EEFB32555E`, and seed 0 yielding
`0xE220A8397B1DCDAF` — and those literals are never edited to match new output.

### 5.3 Fixed-point only

Every quantity that reaches a hashed value is `int` or `long`. Products that can
grow are `checked`. Lengths use the existing `IntegerSquareRoot`, never
`Math.Sqrt`. The one place a real number appears in the reasoning — the
`sqrt(2)` bound in the trail derivation — is discharged with the exact rational
`3/2`, which is a strict integer-safe upper bound, and is asserted numerically by
a test rather than trusted from prose. `StateHasher`'s `Add` overloads accept
only `int`, `long` and `ulong` (`src/Hukbo.Core/Determinism/StateHasher.cs:81-88`),
so a `float` could not be folded even by accident.

### 5.4 Why the tick stays reproducible

`ResolveContingentStates` reads tick-start positions and the `Intent` and
`TargetEntityId` that `SelectTargetsAndIntents` has already finalised. It writes
only `AgentState.ContingentState`, which nothing before it in the tick reads. It
commits no position and emits no event. `GatherMovementProposals` then reads that
state and produces a destination, which flows into `CollisionResolver` through the
existing `CollisionMoveRequest` contract, in the existing strictly-ascending
`EntityId` order that `CollisionResolver.Validate` enforces
(`src/Hukbo.Core/Simulation/CollisionResolver.cs:264-285`). The resolver, the
candidate ladder, the boundary clamp, and the co-location repair are untouched.

No agent can observe another agent's committed move while proposals are being
formed, which is the invariant `GatherMovementProposals`' own doc comment states
and which this design preserves exactly.

### 5.5 Exactly one deliberate state-hash move

Three new values are folded into the state hash, and they are folded together, in
one task, before any behaviour changes:

- `Scenario.MovementPreset`, folded beside `Scenario.CombatPreset` at
  `src/Hukbo.Core/Determinism/StateHasher.cs:46`;
- `AgentState.ContingentId`, appended to the per-agent block after
  `agent.ComboTargetEntityId` at `src/Hukbo.Core/Determinism/StateHasher.cs:75`;
- `AgentState.ContingentState`, appended immediately after it.

All three are behaviour-inert at that moment: the preset defaults to the frozen
value, `ContingentId` is a label nothing reads, and `ContingentState` is `None`
for every agent because the stage that would set it does not exist yet. The state
hash therefore moves once, for a purely representational reason, and never again
until the behaviour itself lands under the new preset.

**The proof that the move is representational** is that the *event* hash does not
move. Events carry no scenario field and no per-agent contingent field, so the
event fold is untouched. So the acceptance evidence for that task is a headless
seed-1 200-agent run reporting the recorded `eventHash 2A9F2D7054CD1805`, the
recorded `outcome Faction1Victory`, `faction0Survivors 0`, `faction1Survivors 2`,
`measuredTicks 1710`, `deterministic true`, `firstMismatchTick null`, and a new
`stateHash`. An identical event hash, an identical winner, identical survivor
counts, and an identical tick count together mean the trajectory did not move.
This design deliberately chose that over the alternative in section 9 of folding
the preset conditionally to preserve the literal old hash.

### 5.6 The baseline this design measures against

Two documents disagree about the current seed-1 baseline. The
`hukbo-determinism-change` skill records `stateHash 71211929A44A16CA` /
`eventHash A2DC3ECA3F7345ED`. `docs/development/testing.md:87-127` records
`stateHash A883926A3B93792E` / `eventHash 2A9F2D7054CD1805`, 664 of 664 tests
passing, `measuredTicks 1710`, `outcome Faction1Victory`,
`faction0Survivors 0`, `faction1Survivors 2`, `allocatedBytes 521296`,
`coreAllocatedBytes 118896`, and explicitly states that it supersedes the older
figures after the combat-preset-V3 merge.

**`docs/development/testing.md` wins.** CLAUDE.md section 6 names it as part of
the live contract, it marks itself as superseding, and it matches the current
`HEAD` commit `473b12d`. The skill's numbers are stale and re-recording them is a
task in the plan.

A second, smaller contradiction: `docs/plans/README.md:33` describes combat preset
V3 as "Design complete, no plan document", while
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs:16,61` registers it and
`src/Hukbo.Core/Combat/CombatIdentity.cs:105-113` documents it. The registry is the
fact; the README table is stale. Nothing in this design depends on it, but no
task may cite that table as evidence.

---

## 6. Versioning

### 6.1 The new axis

A movement/behaviour preset is a **separate version axis** from a combat preset,
implemented as its own enum and its own registry, mirroring the combat pattern
line for line rather than reusing it.

```
src/Hukbo.Core/Movement/MovementPresetId.cs
src/Hukbo.Core/Movement/MovementRuleset.cs
src/Hukbo.Core/Movement/MovementPresetRegistry.cs
```

A fourth file joins them in the same folder when the behaviour lands:
`src/Hukbo.Core/Movement/MovementRules.cs`, holding the pure internal statics
sections 3.4 and 3.6 describe — the duty-cycle predicate, the leader-and-count
forward scan, the state-machine transition function, the six-gate conjunction,
and the arrival taper. It is not part of the version axis and carries no preset
data; it exists so that the decidable arithmetic of the new behaviour can be
called directly by a test instead of only observed through a whole battle.

`MovementPresetId`:

| Value | Name | Meaning |
| --- | --- | --- |
| 1 | `IndependentPursuitV1` | Today's behaviour, frozen. Every warrior pursues its nearest enemy independently; contingents exist only at deployment; no cohesion, no unit state, no arrival taper. |
| 2 | `PersistentContingentsV2` | This design. |

`MovementRuleset` is an immutable value carrying the preset id, a version, the
tunable constants named in section 3, and a `ContentHash` computed the same way
`CombatRuleset.ContentHash` is.

**The constant set is closed the day the type is created, and this is a
correctness requirement rather than a preference.** `ContentHash` is computed
over the ruleset's fields, and section 6.2 freezes
`IndependentPursuitV1`'s pinned `ContentHash` literal permanently. Adding a
field later — when the behaviour lands, say — would move V1's `ContentHash` and
break that freeze on the very change that is supposed to leave V1 untouched. So
`MovementRuleset` declares `CohesionRadiusMultiplier`, `CloseRadiusMultiplier`,
`MinimumCohesiveMembers`, `CohesionCycleTicks`, `CohesionDutyTicks`,
`ArrivalTaperMultiplier` and `OffsetUnit` from the outset, at their frozen-preset
values, even though nothing under V1 reads any of them.

`MovementPresetRegistry` is an exhaustive static
class with `IsRegistered` and `Get` switches that throw
`ArgumentOutOfRangeException` on an unregistered value rather than falling back to
a default — the exact shape of
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs:11-18,56-66`.

`Scenario.MovementPreset` is a new property with its own validation
(`MovementPresetRegistry.IsRegistered`, mirroring the `CombatPreset` check at
`src/Hukbo.Core/Simulation/Scenario.cs:226-232`), its own entry in the hand-written
`Equals` and `GetHashCode` overrides at
`src/Hukbo.Core/Simulation/Scenario.cs:93-149` — which is a manual override
specifically because `ImmutableArray` equality needed it, so a new property is
*not* picked up automatically — and its own fold in `StateHasher`.

Reusing `CombatPresetId` was considered and rejected; section 9 records why.

### 6.2 What gets frozen, and how

When `PersistentContingentsV2` ships, `IndependentPursuitV1` is frozen. Frozen
means, precisely:

- the enum member `IndependentPursuitV1 = 1` is never renumbered, never
  reordered, and never removed;
- its arm in `MovementPresetRegistry.Get` is never edited;
- the rules class backing that arm is never edited;
- its pinned `ContentHash` test literal is never edited;
- its pinned seed-1 state-hash and event-hash literals are never edited.

This is the discipline `docs/plans/README.md:57-58` states and that
`CombatPresetId.PrecolonialPhilippinesV1 = 1` and its registry arm have followed
across two subsequent versions.

### 6.3 How the frozen behaviour is proven to still reproduce

Three independent proofs, and the plan orders them so that all three exist
*before* the behaviour is at risk.

**Proof one — a per-tick trajectory digest, captured first.** A committed fixture
records, for a seed-1 200-agent run, one row per tick carrying `tick`,
`eventCount`, `eventFold` and `stateHash`, plus final per-agent rows carrying
`entityId`, `xRaw`, `yRaw`, `hitPoints`, `intent`, `movementResolution` and
`loadout`. A test replays the scenario under `IndependentPursuitV1` and asserts
every row. This is the schema and the test shape
`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json` and
`DeterminismTests.ZeroInterceptionProfile_ReproducesThePreClashDigest`
(`tests/Hukbo.Core.Tests/DeterminismTests.cs:704-817,897-996`) already use for the
combat axis, extended with the two new per-agent columns. The fixture is captured
from the *unmodified* build as the very first task, so it is an oracle written
before anything could have moved it.

**Proof two — event-hash and outcome invariance across the scaffolding.** As
section 5.5 sets out, the one representational hash move must leave the event
hash, the outcome, both survivor counts and the measured tick count byte-identical
to `docs/development/testing.md`'s recorded values. That is the evidence that the
trajectory did not move even though its fingerprint did.

**Proof three — a pinned seed-1 pair per version.** After the scaffolding, both
presets carry their own pinned `stateHash`/`eventHash` literals asserted by a
`DeterminismTests` fact that runs `HeadlessRunner.Run` end to end with
`--movement-preset` naming the version, exactly as
`PresetV3_SeedOneStateAndEventHashArePinned`
(`tests/Hukbo.Core.Tests/DeterminismTests.cs:165-190`) does for combat presets.
V1's pair is written once and never edited again.

`HeadlessRunner` and `scripts/benchmark.ps1` gain a `--movement-preset` /
`-MovementPreset` switch parallel to the existing `--preset` / `-Preset`
(`src/Hukbo.Headless/HeadlessRunner.cs:241-252,275-295`;
`scripts/benchmark.ps1:22-26,57-59`), so both behaviours are selectable from the
supported entry points without a code change.

### 6.4 Which preset ships as the default

`Scenario.MovementPreset` defaults to `IndependentPursuitV1` for the whole of the
scaffolding, so no hash moves before it must. Flipping the default to
`PersistentContingentsV2` is its own final task, so the resulting hash move is
attributable to exactly one commit.

There is precedent for a default that is not the newest: `Scenario.CombatPreset`
still defaults to `PrecolonialPhilippinesV2` while V3 is registered
(`src/Hukbo.Core/Simulation/Scenario.cs:57-58`). Whether the shipped default should
be flipped at all in this workstream is a question for the user, recorded as an
open question in section 13.

---

## 7. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10 asks whether a spectator can discover an
effect without reading source code. The answer here has three parts and no part
of it requires a new texture, a new content-pipeline asset, or a new theme role.

**The behaviour itself is the primary channel.** A contingent that gathers,
advances as a body, and then dissolves into individual fighting at contact is a
visible pattern at the default camera fit. That is the whole point of the change
and it is what the new smoke-checklist rows are written to check. Nothing else
here substitutes for it.

**The agent inspector names the unit and its state.** `AgentView` gains
`ContingentId` and `ContingentState`, both defaulted so existing presentation
tests compile without naming them — the same accommodation
`MovementResolution` and `Level` already carry
(`src/Hukbo.Core/Simulation/AgentView.cs:19-31`). `AgentInspectorContent.BuildLowerLines`
gains one row immediately after the existing `Intent:` row
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:111-150`), reading, for example:

```
Contingent: 3 — Holding
```

The row is omitted entirely when `ContingentState` is `None`, so a run under the
frozen preset looks exactly as it looks today. `MaximumLowerRowCount` rises from
12 to 13 and its doc comment, which enumerates the rows by name, is updated with
it. This is the same channel `MovementResolution` uses —
`SIMULATION-GAME-STANDARDS.md:636-643` records that decision and its reason.

**The pawn's ground base is tinted per contingent.** `PawnRenderer.DrawGroundBase`
already draws a solid faction-tinted rectangle beneath every pawn from the shared
one-by-one pixel texture (`src/Hukbo.Client/Rendering/PawnRenderer.cs:171-184`,
geometry at `src/Hukbo.Client/Rendering/PawnGeometry.cs:45-59`). Under the new
preset that tint is derived, presentation-only, from the existing `TeamA` and
`TeamB` theme roles by a fixed per-contingent lightness step: contingent 0 is the
unmodified faction colour and each subsequent contingent steps one fixed
increment. No new theme role is added, so the 27-role catalog and its
cross-theme contrast validation are untouched, which is what the
`hukbo-client-ui` skill's "never hardcode a Color" rule requires. Whether eight
steps stay distinguishable at the default fit is a question only a person watching
can settle, so it is a smoke row, not a test.

**No new `BattleEventKind`.** The event feed retains at most 200 ordered events
(CLAUDE.md section 5) and the last-stand design already rejected a dedicated event
kind for cohesion signalling. State changes are read from the inspector, not from
the feed.

**The client decides nothing.** `ContingentId` and `ContingentState` are
authoritative `Hukbo.Core` state that the client only reads and draws. No
targeting, damage, retreat or victory decision moves to `Hukbo.Client`.

---

## 8. Performance

### 8.1 The budget

The workload is the canonical one: 200 agents, 10,000 requested ticks, seed 1,
Release build, through `scripts/benchmark.ps1`. Two acceptance figures:

- **The new stage's p95 inclusive share of `AdvanceOneTick` must not exceed 5%.**
- **Total tick p95 must not regress by more than 10%** against the same workload
  measured immediately before the behaviour lands. Ten percent is the review
  threshold `SIMULATION-GAME-STANDARDS.md:242-287` sets for a p95 tick-time or
  working-set regression.

Both are reported with the environment block that section requires — CPU, RAM,
OS, build profile, scenario hash, agent count, tick rate, warm-up ticks, measured
ticks, and p50/p95/p99/max per stage.

A third figure is a hard pass/fail rather than a budget: **the per-tick allocation
test must still pass unchanged**, at 16,384 bytes per 1,000-tick measurement
window with a 4,096-byte warm-window growth tolerance
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:338-393`). Every array the new
stage needs is sized once at construction; a warm tick allocates nothing.

### 8.2 Why 5% is achievable

The context the budget sits in, from `docs/research/TICK-STAGE-PROFILE.md`:
`ResolveCollisions` is 63.11% of the tick at 200 agents and rises to 74.77% at
2000, `CollisionResolver.IsFree` alone is 50.62% of exclusive tick time at 2000
agents, and `SelectTargetsAndIntents` — which contains a nested scan over all
candidates — never exceeds 16.67%. The tick is collision-bound.

Against that, the complexity of every rule this design proposes:

| Rule | Complexity | Notes |
| --- | --- | --- |
| Leader and living-count scan | O(n) per tick | one forward pass, no allocation |
| Spread and nearest-enemy scan | O(n) per tick | one further forward pass; one `SquaredDistance` per living agent, the same primitive `SelectTargetsAndIntents` calls many times per agent |
| Unit-state transition | O(1) per contingent, at most 16 contingents | integer comparisons only; the duty cycle is one addition, one modulo and one comparison |
| Map-edge open-ground test | O(1) per contingent, at most 16 contingents | four `long` comparisons against the unclamped trail base |
| Cross-contingent overlap test | O(1) per same-faction contingent pair; **at most 28 pairs per faction and 56 in total** | two absolute differences and two `long` comparisons per pair. The bound is `C(8, 2) = 28`, because `FormationPlanner` caps contingents at eight per faction (`MaximumContingents = 8`, `src/Hukbo.Core/Simulation/FormationPlanner.cs:45`). On the canonical workload the real figure is lower still: five contingents per faction give `C(5, 2) = 10` pairs each, 20 in total |
| Straggler test | O(1) per living `Moving` non-leader agent in `Advance` | one `SquaredDistance` and one `long` comparison; the same primitive `SelectTargetsAndIntents` calls once per candidate rather than once per agent |
| Personal offset | O(1) per agent that passes all six gates | one FNV mix, one `SplitMix64` initialisation, two draws |
| Trail and give-way | O(1) per agent that passes all six gates | the same arithmetic `BuildRegroupingProposal` already performs per regrouping agent |
| Arrival taper | O(1) per moving agent | one comparison, one multiply, one divide |

All six gates only ever *remove* per-agent work: an agent that fails a gate
takes the frozen preset's path, which is the path it would have taken without
this design at all. The gates therefore make the realistic cost lower than the
worst case tabulated here, and the worst case — every living member of every
contingent gathering on the same tick — is still a bounded constant per agent.
The one cost a gate adds rather than removes is the cross-contingent scan
itself, and it is a fixed at-most-56 comparisons per tick that does not grow
with agent count at all.

Total added work is two forward passes, at most fifty-six pair comparisons, and
a bounded constant per moving agent —
strictly less than one additional `SelectTargetsAndIntents` pass, which is itself
under a sixth of the tick and contains an O(n) inner loop this design does not.
No new spatial query, no second grid, no new pass over neighbours.

### 8.3 What is deliberately not paid for

A general neighbour-avoidance rule would need a bounded neighbour query, which
would need a second uniform-grid rebuild inside the movement stage. `docs/research/TICK-STAGE-PROFILE.md`
already shows that fixing the collision stage alone will not make the tick linear,
and `docs/plans/2026-07-28-collision-resolution-scaling-design.md` has reserved
that area. Building a competing structure now would be paid for twice. It is out
of scope, and section 13 asks the user to confirm.

---

## 9. Alternatives considered and rejected

Every entry either was considered here or is recorded in this repository as
already tried and rejected. The latter are marked.

| Alternative | Why rejected |
| --- | --- |
| **Reuse `CombatPresetId` and `CombatRuleset` for movement.** | `CombatRuleset`'s constructor enumerates preset id, version, target-weight profiles, armor, shield multipliers, roster, weapon attributes and an optional clash profile (`src/Hukbo.Core/Combat/CombatRuleset.cs:48-57`) — there is no movement, formation or steering field anywhere in the type, so new data would have no home. Worse, it would cross-couple two independent version axes: retuning a weapon would force a new movement version and vice versa. |
| **Fold `MovementPreset` into the state hash conditionally, so V1's literal hash never moves.** | Technically possible, with a precedent in the clash profile's conditional fold. Rejected because a conditional fold is a permanent special case that every future reader must learn, and because the alternative — one deliberate, attributable hash move proven representational by an unchanged event hash, winner, survivor counts and tick count — is both simpler and stronger evidence. |
| **Store the leader on `AgentState` and reassign only on death.** | Creates a stored reference that must be invalidated, which puts it under the cache rules at `SIMULATION-GAME-STANDARDS.md:198-215` and requires a cold-cache equivalence test. Recomputing it each tick costs one forward scan, gets death-promotion for free, and owes none of that. |
| **A centroid as the cohesion anchor instead of a leader.** | *Already rejected in this repository*, in the last-stand design. A centroid moves whenever any member moves, so every member chases a target that its own motion displaces — the moving-goalpost failure. A leader's position moves only when one specific agent moves. |
| **Key the personal offset on the tick.** | *Already rejected in this repository*. `src/Hukbo.Core/Simulation/RallyOffset.cs:11-21` records that a tick-keyed offset makes every follower chase a target that flees a fraction of a unit every tick, so the formation never settles and the collision resolver spends its slack fighting the goalpost. `RallyOffsetTests.OffsetDoesNotDependOnTheTick` pins the fix. |
| **A blended steering vector combining pursuit and cohesion.** | *Already rejected in this repository*, in the last-stand design, and independently cautioned against by `docs/research/FORMATION_AND_COLLISION_MECHANICS.md:183-219`: integer division zeroes small force components and introduces directional bias, and blended desire that overwhelms repulsion oscillates or compresses. This design picks one destination per agent per tick instead of summing forces. |
| **Store the offset bias as a field on `AgentState`.** | *Already rejected in this repository*, in the last-stand design. A pure function of `(seed, entityId)` needs no field, no initialisation, and no hash slot. |
| **A dedicated `BattleEventKind` for contingent state changes.** | *Already rejected in this repository*, in the last-stand design, and independently constrained by the 200-event retention cap in CLAUDE.md section 5. The inspector is the channel, matching `MovementResolution`. |
| **Reynolds-style separation forces, RVO, or ORCA.** | Recommended against by `docs/research/FORMATION_AND_COLLISION_MECHANICS.md:183-250` for this simulation specifically: intentional enemy contact conflicts with the objective of collision-free navigation, and velocity-obstacle methods need an authoritative velocity field. The archived formation-and-collision plan's scope guardrails forbid adding velocity, mass or acceleration outright. Separation here comes from the per-member offset instead. |
| **Modify the collision resolver's candidate ladder to be cohesion-aware.** | *Already rejected outright* by `docs/plans/2026-07-28-collision-resolution-scaling-design.md` section 11: changing which candidate is accepted changes committed positions and therefore moves both hashes, which would entangle two workstreams. This design changes proposals only and leaves the resolver byte-identical. |
| **Parallelise the mover loop to buy back the new stage's cost.** | *Forbidden* — the mover loop is order-dependent and `SIMULATION-GAME-STANDARDS.md` section 15 rules it out. |
| **Independent per-faction jitter so contingents differ between the armies.** | *Already measured and rejected in this repository*: `src/Hukbo.Core/Simulation/FormationPlanner.cs:59-66` records that it changed which bodies stood where without changing which faction won, so it bought nothing and cost the exact mirror symmetry. |
| **A general-purpose ECS or archetype framework to hold per-unit state.** | *Already evaluated end to end and declined*: `SIMULATION-GAME-STANDARDS.md:875-891` records the Arch review concluding this repository adopts none of its archetype or chunk machinery. CLAUDE.md section 9 forbids it before a profiler demands it. Plain preallocated arrays plus two fields on `AgentState` are the sanctioned shape. |
| **Ranks, files, fixed frontage, a shield wall, or any named formation template.** | Forbidden twice over: `SIMULATION-GAME-STANDARDS.md:417-421` states agents are never assigned to a rank, file, slot or named formation, and `docs/research/battles/03-deep-past-formations-and-tactics.md:61-76,425-441` lists every one of them as not attested. |
| **A scripted champion duel when a contingent breaks.** | `docs/research/battles/04-deep-past-individual-combat.md:262-284` explicitly rejects a formal pre-battle duel, a noninterference rule, a referee, a bounded ground, and a standardized challenge sequence. Only an emergent, unplanned pairing is defensible, and that is what a broken contingent's reverted individual pursuit already produces. |
| **`Break` as a retreat or withdrawal state.** | That is a morale system, and CLAUDE.md section 9 gates morale behind a milestone that has not been reached. `Break` here only turns cohesion off. |
| **A hashed per-contingent counter to bound the time cohesion stays active.** | Rejected in favour of the tick-phase duty cycle in 3.4, which gives the same hard bound with no additional hashed field, no additional state to initialise, and a desynchronising phase term for free. A counter would also have to be read by two stages in the same tick, and two readers of one mutable counter is a class of bug the pure-function form cannot have. |
| **Bounding only the `Hold` state's duration, leaving `Advance` untimed.** | *This was an earlier revision of this design and it was wrong.* `Advance` is the state a contingent occupies for most of a battle, and under it a straggler is still being pulled toward a leader who may be physically blocked. A bound that covers only `Hold` leaves the longest-running cohesive state with no timeout at all — precisely the gap that let Deadlock A run to the tick limit in the last-stand work. The duty cycle in 3.4 now gates every state in which cohesion can fire. |
| **Shrinking `jitterRaw` when the bias square overhangs a map edge.** | Rejected in favour of the map-edge open-ground test in 3.5. Shrinking the square raises the density inside it, and density is the input to the very packing proof the shrink is trying to preserve, so it trades a geometric collapse for a packing one. In a corner, where both axes are constrained at once, no positive `jitterRaw` avoids the one-dimensional case. Turning cohesion off for that contingent removes the failure instead of relocating it, and degrades to behaviour with no known deadlock. |
| **Relying on the cohesion duty cycle alone to survive failure shape 2, with no cross-contingent test.** | *This was an earlier revision of this design and it was wrong, in the same way the `Hold`-only bound above was wrong.* The duty cycle decides which destination an agent is proposed; it decides nothing about whether the collision resolver grants the movement. An earlier revision asserted that the escape "does not rest on any argument about what the collision resolver will do" and offered it as shape 2's only cover. Two same-faction contingents whose trailing squares overlap put roughly twice the packing bound's density into the shared region, and the duty cycle does not change that density by one body — it only bounds how long the aiming lasts. The gate in 3.5 removes the *aim-point* density instead — and only that, which is why escape 6 is now rated **partial** rather than **yes** for shape 2 and why the third engineered scenario in 10.3 exists. |
| **Rating the cross-contingent test as fully covering failure shape 2.** | *This was the revision after the one above, and it was wrong in the same family.* It rated escape 6 **yes** for shape 2 on the strength of a summary sentence in 3.5 — "this, and not the duty cycle, is what carries the density argument" — whose supporting paragraph then conceded that independently-pursuing agents still transit a granted square and asserted without proof that such traffic "does not park a second headcount there". That is a claim about what the collision resolver does with agents the gate never examined, offered as licence for a density argument, which is precisely the claim the row above retracts for the duty cycle. Both the sentence and the assertion are withdrawn. The gate bounds aim-point density and nothing else; the fourfold margin is the allowance for unaimed traffic; 10.3's crossing-traffic scenario is the measurement. |
| **Denying cohesion pairwise rather than transitively, to avoid chain denial across several contingents.** | Rejected because it is not an available alternative: the rule is already pairwise. A contingent is denied exactly when it genuinely overlaps some other living same-faction contingent, and the logical OR is over its own pairs. When A overlaps B, B overlaps C, and A does not overlap C, all three are denied by three separate genuine overlaps — nothing propagates and there is no transitive step to remove. The only alternative that does exist is the asymmetric rule one row below, and it is worse at chain length three than at length two. 3.5 works the framing through and 10.3 pins it with a constructed three-contingent fact. |
| **Restricting the cross-contingent scan to contingents that could actually be granted cohesion, excluding `Close` and `Break`.** | Not rejected on merit — *deliberately deferred behind a measurement*, and pre-analysed in 3.5 so that adopting it later is a decision against evidence rather than a fresh argument. It would preserve the combined-density statement exactly, since a `Close` or `Break` contingent parks no aim points anywhere, and it would materially reduce the chain-denial inertness risk. It is not adopted now because it moves those contingents' members from "excluded by gate 6" into the one residual this design cannot bound, and loading an unbounded residual to buy cohesion is the wrong trade before the residual has been measured. It is the named first remedy if 10.3's inertness bar fails. |
| **An asymmetric cross-contingent rule: the lower `ContingentId` keeps cohesion, the higher yields.** | Rejected. It would leave one contingent still parking a full headcount's worth of aim points inside the shared region, while the yielding contingent's members are standing in that same region under independent pursuit, so the combined density in the overlap would not be restored to the packing bound at all — which is the only property the gate exists to establish. The symmetric rule costs a little more cohesion and buys the entire argument. Symmetry also removes a whole class of question: with `|aX - bX| <= aMargin + bMargin` there is no ordering rule to get wrong and no possibility of the two contingents disagreeing about their own pair. |
| **Shrinking `jitterRaw`, or nudging a trail base sideways, when two bias squares overlap.** | Rejected for the same reason shrinking was rejected at a map edge, one row below: density is the input to the packing proof, so shrinking the squares until they separate raises the density inside each one and trades an overlap failure for a packing failure. Nudging a trail base is worse — it moves the aim point away from the leader's trail, which is the geometry Deadlock B's fix depends on. |
| **Testing cross-*faction* bias-square overlap as well.** | Rejected as unnecessary rather than unsafe. Two opposing contingents whose squares converge are by construction closing on each other's members, which drives `nearestEnemySquared` down until transition rule 3 fires and `Close` disables cohesion for both. Shape 2 is dangerous exactly because no enemy is present to trigger that; the cross-faction case has one by definition. Adding those pairs would double the scan to cover a risk rule 3 already removes. |
| **Letting `ClampCenterToBounds` handle the boundary case, as the last-stand path does.** | Rejected on exposure. The last stand produces one cluster per faction, late, and its residual boundary risk was small enough to leave unaddressed. This design produces up to sixteen clusters spread across the map for most of the battle, which multiplies that risk rather than inheriting it. The clamp is still what protects the *coordinates*; the two geometric gates in 3.5 are what protect the *proof*. |
| **A general "step around whichever ally is blocking me" avoidance rule.** | Not rejected — *deferred*. It needs a bounded neighbour query and therefore a second uniform-grid rebuild, in a tick already 63–75% collision-bound, in an area the collision-scaling design has reserved. Section 13 asks the user to confirm the deferral. |
| **Pathfinding, a navigation graph, or terrain-aware routing to flow around blockers.** | Gated. CLAUDE.md section 9 and `SIMULATION-GAME-STANDARDS.md:372-374` place pathfinding behind an unmet milestone requiring a deterministic graph representation, walkability as authoritative state, and its own benchmark matrix. |

---

## 10. Risks, especially deadlock

### 10.1 The three deadlocks this repository has already observed

This is the highest risk in the design, and the reason is that the repository has
already produced three distinct deadlocks from *exactly this class of rule*, in
the last-stand rally formation, and two of them were only found after the design
shipped. `src/Hukbo.Core/Simulation/FormationRules.cs:7-31` and the last-stand
design's own corrections section state plainly that the original liveness argument
— that "a rally agent is exempt from formation so it can never stall" — was
invalid, because *exempting an agent from a behaviour does not exempt it from
being physically blocked by the agents that are still following that behaviour*.
That sentence is the governing lesson of this section.

**Deadlock A — packing.** Filling a bounded bias square to its geometric capacity
demands perfect packing from offsets drawn at random. In practice every follower
overlaps someone, the collision resolver blocks the whole cluster, and because the
leader is surrounded by its own followers even the exempt leader cannot move.
Observed directly: a sixteen-versus-sixteen battle at a threshold equal to
capacity ended in a forced draw at tick 10,000 with both factions at full strength
and a longest blocked streak of 9,975 ticks
(`src/Hukbo.Core/Simulation/FormationRules.cs:26-37`).

**How this design avoids recreating it.** The offset square is not fixed and not
tuned; its half-side is *solved* from the contingent's own living count so that
capacity always exceeds four times the headcount:
`jitterRaw = BodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)`, giving
`capacity = m^2 > 4 * livingCount` by construction, at every population, forever.
Bodies therefore cover under a quarter of the square and the resolver always has
room to separate them. This is the same fourfold margin `RallyPackingMargin = 4`
encodes, generalised from a fixed multiplier to a derived one so that a contingent
of 63 members is as safe as one of six.

**And the proof is guarded by its own hypothesis, on both the ways that
hypothesis can fail.** The paragraph above is a statement about a square of open
ground carrying one contingent's aim points and nobody else's, and it is worth
nothing where the square is not that.

Near a map edge `ClampCenterToBounds` collapses the square onto a line, and in a
corner toward a point, which recreates Deadlock A by geometry rather than by a
bad constant. The map-edge open-ground test in section 3.5 therefore checks, in
four exact `long` comparisons before any member is given a cohesion destination,
that the whole square plus one body radius fits inside the map.

Where two same-faction contingents' squares overlap, the square is open ground
but it is carrying two contingents' aim points, and the headcount in the shared
region can approach twice the safe figure — half the square's capacity rather
than the quarter this margin is solved for, which spends the entire fourfold
allowance that the 9,975-tick failure above is the reason for. The
cross-contingent test in
section 3.5 therefore checks, in two absolute differences and two exact `long`
comparisons per pair, that no other living same-faction contingent's square
overlaps this one.

When either test fails, cohesion is off for that contingent for that tick and
every member reverts to independent pursuit. The packing proof's *aim-point*
hypothesis is consequently only ever applied in the case it was proved for.

Its open-ground hypothesis is restored only against aim points, and that
limitation is load-bearing rather than pedantic. Neither gate examines a body
that is standing in or walking through a granted square without being aimed at
it, and this design has no bound on such traffic. The fourfold margin — three
quarters of every granted square unoccupied by its own contingent's aim points —
is the allowance, and section 10.3's crossing-traffic scenario is the
measurement. Section 3.5 states that residual in full and section 10.2 rates
escape 6 **partial** for shape 2 because of it.

**Deadlock B — follower blocks leader.** An offset drawn uniformly from the bias
square can point straight down the leader's forward arc. A follower landing there
blocks the very agent it is following, forever, because the leader is exempt from
cohesion and never reroutes around its own formation. Two factions doing it
simultaneously deadlock the battle at the tick limit with zero casualties.
Observed pre-fix on seeds 5, 6 and 9
(`src/Hukbo.Core/Simulation/FormationRules.cs:48-59`;
`tests/Hukbo.Core.Tests/LastStandFormationTests.cs:720-731`).

**How this design avoids recreating it.** The unjittered aim point is placed
`trailRaw` behind the leader, opposite the leader's own direction of travel,
before the offset is applied, with
`trailRaw >= (3/2) * jitterRaw + 3 * BodyRadiusRaw`. Since the offset's projection
onto any direction is Chebyshev-bounded by `jitterRaw * sqrt(2)` and
`3/2 > sqrt(2)`, and since `3R > 2R` covers the contact distance, the leader's
forward arc is clear even in the worst case, at every value of `jitterRaw` the
derived formula can produce. The inequality is the general one
`FormationRules.cs:61-79` states; here it holds by construction rather than by a
hand-checked pair of constants, which is strictly safer given that `jitterRaw` now
varies.

**Deadlock C — head-on mutual block.** Even with the trail, a follower can start a
tick already ahead of its leader, so reaching a point behind the leader means
walking backward through the leader's body. Straight-line movement plus solid
collision produces a mutual block that never clears
(`src/Hukbo.Core/Simulation/FormationRules.cs:80-95`;
`src/Hukbo.Core/Simulation/BattleSimulation.cs:796-802`).

**How this design avoids recreating it.** The give-way corridor rule is reused
verbatim, generalised from the rally agent to any leader: a member whose
tick-start position projects forward of its leader and lies within two body radii
of the leader's line of travel steps purely sideways, forward position unchanged,
toward the side it is already on, with a fixed positive-perpendicular tie-break
for the exactly-on-axis case.

### 10.2 The deadlock class this design introduces that last stand did not have

The last stand has exactly one cluster per faction. This design has up to eight
per faction and sixteen in play, so three new failure shapes are possible:

- **Shape 1 — a contingent's leader blocked by a *different* contingent's
  members.**
- **Shape 2 — two contingents of the same faction gathering into each other.**
  This is the dangerous one, and it is worth stating in full because it is
  Deadlock A wearing different clothes: two same-faction leaders converge with
  no enemy anywhere near, each one's followers pile into the other's, nobody
  takes a casualty, so no attrition trigger fires, no engagement trigger fires,
  and the battle runs to the tick limit at full strength.
- **Shape 3 — a contingent gathering against a map edge or in a corner**, where
  the packing proof's open-ground hypothesis does not hold.

**Which escape actually covers which shape** — stated as a table rather than as
prose, because this document has now got shape 2 wrong twice in prose and both
times the error was hidden by a sentence that sounded like coverage. The first
revision listed three escapes and quietly assumed they covered all three shapes,
when in fact none of them covered shape 2. The second added the duty cycle and
rated it **yes** for shape 2 on the strength of a claim that it "does not rest on
any argument about what the collision resolver will do" — a claim that was false,
because a rule about which destination is *proposed* says nothing about which
movement is *granted*. Both errors are recorded in section 9 as rejected
alternatives, and the ratings below are deliberately harsher than a summary
sentence would be.

| Escape | Shape 1 | Shape 2 | Shape 3 |
| --- | --- | --- | --- |
| 1. Leader exemption | partial | **no** | no |
| 2. `Close` and `Break` disable cohesion | partial | **no** | no |
| 3. Straggler gate on `Advance` | partial | partial | partial |
| 4. Cohesion duty cycle | bounds duration only | bounds duration only | bounds duration only |
| 5. Map-edge open-ground test | no | no | **yes** |
| 6. Cross-contingent overlap test | partial | partial | no |

**No escape reads `yes` in the shape 2 column, and that is the honest reading.**
An earlier revision rated escape 4 **yes** there; the revision after it moved the
rating to escape 6. Both were wrong for the same reason, and the second was wrong
one section later than the first — see the retraction under "the combined-density
argument" in section 3.5. Shape 2 is covered by a *combination* of partial
escapes plus an engineered test, not by any single mechanism, and section 10.3 is
where that coverage is actually established.

1. **The leader is exempt and always pursues an enemy.** A leader never has a
   cohesion destination, so it never parks *its own* destination. It remains
   fully blockable by anything physically in front of it, including another
   contingent's mass, which is exactly the lesson quoted at the top of this
   section. It does nothing at all for shape 2.
2. **`Close` and `Break` switch cohesion off completely.** A contingent that
   reaches engagement distance, or that has been reduced past the attrition
   threshold, stops gathering entirely and its members revert to the frozen
   preset's individual pursuit — which is behaviour with no known deadlock. This
   is a strong escape wherever an enemy is near or casualties are being taken.
   Shape 2 has neither: two friendly contingents piling into each other far from
   any enemy never reach `Close` and never take the casualties `Break` needs.
   Escape 2 is silent in exactly the case that matters most.
3. **The straggler gate reduces the exposure without closing it.** Under
   `Advance` only a member beyond the straggler threshold is pulled toward its
   leader, so far fewer agents are aimed at a converging point and the pile-up
   is smaller and slower to form. But it is explicitly **not** a fix for shape
   2, and the reason is worth being blunt about: a member caught in a friendly
   pile-up is precisely the member that has fallen behind, so it is precisely
   the member the gate lets through. The gate makes the failure rarer; it cannot
   make it impossible.
4. **The cohesion duty cycle bounds how long any cohesive regime can last, in
   every state — and that is all it does.** Gate 3 of the movement branch tests
   `cohesionWindowOpen(Tick, slot)` directly, before any state is consulted, so
   *an agent receiving a cohesion destination implies the window is open* — with
   no case analysis over states, and in particular with no reliance on the state
   machine's rule 4, which exists only to keep the inspector honest. The window
   is open for at most `CohesionDutyTicks = 180` consecutive ticks out of every
   `CohesionCycleTicks = 240`. Therefore every contingent, in every state, gets
   at least sixty consecutive ticks in every two hundred and forty during which
   every one of its members — leader, straggler and gathered member alike — runs
   the frozen preset's individual pursuit. The bound depends on nothing the
   simulation observes: not spread, not blocking, not enemy proximity, not
   casualties.

   **What it is not, stated plainly because an earlier revision of this document
   asserted the opposite.** That revision said this escape "does not rest on any
   argument about what the collision resolver will do" and rated it **yes** for
   all three shapes, making it shape 2's only cover. The claim was false. The
   duty cycle decides which *destination* an agent is proposed; it decides
   nothing about whether the resolver will *grant* the movement toward it, and
   the resolver is the thing that has to say yes for a body to change position.
   Believing otherwise is exactly the error section 10.1 names as the governing
   lesson of the two prior observed deadlocks — changing what an agent is aimed
   at does not exempt it from being physically blocked by the agents around it.
   Sixty free ticks are sixty ticks of independent pursuit, which this repository
   has never observed to deadlock; but that is evidence about behaviour under the
   collision resolver, not a proof of forward progress, and in a region packed to
   twice the density the packing bound allows there is no reason to expect the
   frozen preset's behaviour to be any freer than the cohesive one. So the table
   rates escape 4 **bounds duration only**, in every column. It is a real and
   valuable guarantee — nothing can hold a contingent in a cohesive regime
   forever — and it is not the density argument.
5. **The map-edge open-ground test removes shape 3 at its source.** Cohesion is
   granted only when the contingent's whole bias square, plus one body radius,
   fits inside the map on both axes. A contingent gathering against an edge or in
   a corner is given no cohesion destination at all, so the aim-point collapse
   that would invalidate the packing proof cannot occur. Section 3.5 carries the
   derivation and the one residual it does not cover.
6. **The cross-contingent overlap test removes shape 2's *aim-point* doubling,
   and that is the precise extent of it.** Cohesion is granted to a contingent
   only when its bias square is disjoint from the bias square of every other
   living same-faction contingent; when two overlap, both yield and every member
   of both reverts to independent pursuit. Shape 2's geometry — two same-faction
   contingents on a similar heading, each trailing its square behind its own
   leader and therefore on the same side, converging so that the trailing squares
   land on top of one another — is exactly the case this gate denies.

   **What it proves:** no two granted bias squares overlap, so every square in
   use as an aim region satisfies the quarter-density hypothesis with respect to
   its own contingent's aim points. The doubling that made shape 2 Deadlock A in
   different clothes — half a square's capacity instead of a quarter — cannot
   occur through aim points.

   **What it does not prove:** anything at all about agents transiting a granted
   square who are not aimed at it. A `Break` or `Close` contingent's members, a
   contingent whose window is shut, a non-straggling member under `Advance`, and
   a third same-faction contingent whose own square is disjoint from this one can
   all stand in or walk through a granted square, and the gate never examines
   them. The fourfold packing margin is the allowance for that traffic and the
   engineered crossing-traffic scenario in section 10.3 is the evidence that the
   allowance holds; neither is a proof.

   It is therefore rated **partial** for shape 2, not **yes**. An earlier
   revision rated it **yes** on the strength of a summary sentence in section 3.5
   that has since been withdrawn. It is rated **partial** for shape 1 because it
   stops a second contingent from parking a full headcount of aim points across a
   leader's path but does nothing about that contingent's independently-pursuing
   members crossing it, and **no** for shape 3, which is entirely gate 5's job.

**No escape reads `yes` all the way across, and none is meant to. The division
of labour is the argument:**

- Escapes 1, 2 and 3 reduce how often cohesion is granted at all. None of them
  is a bound and none is claimed to be.
- Escape 4 bounds *how long* any cohesive regime can last, unconditionally, with
  a counter that reads nothing the simulation observes. It is a duration bound
  and nothing more.
- Escapes 5 and 6 restore the *aim-point* hypothesis the quarter-density packing
  proof needs — open ground, carrying one contingent's aim points and no other's
  — on the two ways that hypothesis can fail through aim points. They bound the
  aim-point density of every granted square. They do not bound the body density
  of a granted square, because neither gate examines a body that is not aimed at
  the square.
- Nothing here bounds the unaimed traffic. The fourfold packing margin is the
  allowance for it — three quarters of every granted square is unoccupied by its
  own contingent's aim points — and the engineered crossing-traffic scenario in
  section 10.3 is the measurement that says whether the allowance holds. That is
  the residual, and section 3.5 states it as a residual rather than closing it.

Escapes 5 and 6 are geometric preconditions checked in exact integer arithmetic
before the fact; escape 4 is a counter. None of the three is a claim about which
candidate the collision resolver's ladder will accept, which is the class of
claim that produced the last stand's two post-ship deadlocks. But neither are
they a proof that the ladder will accept one, and this document does not assert
that they are. What they establish is that the *aim-point* density regime in
which the resolver is asked to work is the regime the packing margin was solved
for, with three quarters of each granted square left over as headroom for bodies
the gates never counted.

**So the liveness case for shape 2 is an argument plus a test, and it is stated
that way deliberately.** The argument is: gate 6 removes the aim-point doubling,
gates 1 through 4 reduce how often and for how long cohesion is granted at all,
and the fourfold margin is sized to absorb traffic rather than merely to fit one
contingent. The test is the pair of engineered scenarios in section 10.3 — the
converging-squares scenario, which proves the gate fires and the battle still
terminates, and the crossing-traffic scenario, which puts foreign bodies inside a
square that provably *was* granted and asserts the battle still terminates. If
either fails, the design does not ship as written. This document does not claim a
proof, and the repository's standard here is honesty about a bound rather than a
bound invented to look complete.

None of this is accepted on the strength of this section. Section 10.3 pairs each
failure shape with a **deliberately engineered** test that constructs the failing
geometry directly, rather than hoping a random seed sweep stumbles into it. A
seed sweep that happens to pass proves that twenty particular trajectories
avoided the shape, not that the shape is survivable. There are three such
scenarios, not two:

- **the converging squares**, for shape 2's aim-point doubling, required to
  construct the *worst* case rather than an arbitrary crossing, and required to
  demonstrate that the cross-contingent gate actually fired during the run — a
  liveness test that passes because its guard was never needed has tested
  nothing;
- **the crossing traffic**, for the residual escape 6 does not cover: a
  contingent whose square is provably granted, with foreign same-faction bodies
  routed through it. This one has no guard to disable, because it is not testing
  a guard; it is the only evidence for a residual the design states honestly and
  cannot bound;
- **the corner pin**, for shape 3, which also exercises the give-way clamp
  residual section 3.5 states.

### 10.3 The tests that prove it

None of the reasoning above is accepted without a test that would fail if it were
wrong.

| Risk | Proving test |
| --- | --- |
| Packing deadlock | A twenty-seed sweep at 200 agents under `PersistentContingentsV2` asserting every battle reaches a terminal outcome strictly inside its tick limit, mirroring `LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty` (`tests/Hukbo.Core.Tests/LastStandFormationTests.cs:733-778`). A forced draw at the tick limit fails the test. |
| Packing margin, at every population | A numeric assertion that `(IntegerSquareRoot(4 * livingCount) + 1)^2 > 4 * livingCount` across `livingCount` from 1 to 2000. |
| Follower-blocks-leader | A numeric assertion that `trailRaw > jitterRaw * sqrt(2) + 2 * BodyRadiusRaw` across the full body-radius and living-count ranges, with the `sqrt(2)` side evaluated in a way that cannot round in the design's favour. |
| Head-on mutual block | A member placed directly in its leader's forward corridor steps aside rather than through, and the give-way side is stable when it is exactly on the leader's axis — the two assertions `LastStandFormationTests.cs:789-844,908-954` already make for the rally agent, repeated for a contingent leader. |
| Blocked-streak bound | A provisional maximum-blocked-streak bound across twenty seeds under the new preset, matching the existing provisional 125-tick bound the last-stand suite records. |
| **Shape 2 — two same-faction contingents converging with no enemy nearby** | A **deliberately engineered** scenario, not a seed sweep, and required to construct the **worst** case rather than an arbitrary crossing: two contingents of one faction on the *same* heading toward one distant enemy cluster, their leaders offset laterally so the paths cross, their non-leader members placed in the trailing region *behind* each leader relative to that enemy, and the lateral offset chosen so the two trail bases start within `2 * marginRaw` of each other on both axes — that is, so the two bias squares overlap from the first tick. The opposing faction is inside perception range, so both contingents advance, but far enough away that no member reaches `Close` and no casualty is taken for the whole convergence. The map is large enough that no contingent's bias square can approach an edge at any point in the run, so gate 5 provably cannot fire and any denial is attributable to gate 6 alone. The assertion is that the battle reaches a terminal outcome strictly inside its tick limit. A seed sweep cannot substitute for this, because a sweep that passes proves only that twenty particular trajectories did not produce the geometry. |
| **The cross-contingent gate actually fires in that scenario** | A second fact over the same engineered run, so that the liveness fact above cannot pass because its guard was never needed. It asserts that on at least one tick there exists a contingent whose living non-leader spread exceeds `cohesionRadiusRaw^2`, whose duty-cycle window is open for its slot on that tick, and whose recorded `ContingentState` is nevertheless `Advance`. With gate 5 excluded by the map sizing above and gate 3 excluded by the window check, only gate 6 can produce that combination. A run in which the assertion never triggers means the scenario failed to build the worst case, and the fact fails rather than passing vacuously. |
| **The residual — independently-pursuing traffic crossing a granted square** | A **deliberately engineered** scenario, built to the worst case the residual under "the combined-density argument" in section 3.5 describes, and the only evidence this document has for that residual. Two same-faction contingents on the same heading toward one distant shared enemy, one placed behind the other along that heading. The forward contingent is strung out beyond `cohesionRadiusRaw` so the state machine selects `Hold` for it, and the rear contingent's leader is placed far enough back that the two trail bases are separated on the heading axis by more than `aMarginRaw + bMarginRaw`, so gate 6 provably does **not** fire and the forward contingent's square really is granted. The rear contingent's members are all placed within the straggler threshold of their own leader, so gate 4 sends every one of them to independent pursuit, and forward of that leader so their straight-line pursuit path to the shared enemy runs through the forward contingent's bias square. The assertion is a terminal outcome strictly inside the tick limit. |
| **That scenario really grants cohesion while the square is occupied** | A companion fact over the same run, so the liveness assertion above cannot pass because the square was never granted or was never occupied. It asserts that on at least one tick, the forward contingent's recorded `ContingentState` is `Hold` **and** at least a stated number of the rear contingent's living non-leader members lie inside the forward contingent's bias square, that square recomputed in the test from the forward leader's tick-start position, the forward contingent's living count, `FormationRules.ComputeContingentJitterRaw` and `ComputeContingentTrailRaw`. A recorded `Hold` is exactly the observable statement "this contingent was granted cohesion on this tick": rules 1 through 3 would have written `Break` or `Close`, rule 4 would have written `Advance` on a shut window, and the stage writes `Advance` rather than `Hold` whenever either geometric gate denies. Unlike the converging-squares fact, this one has **no guard to disable**, because it does not test a guard — it measures whether an unbounded residual bites. |
| **Chain denial does not arise from a propagation step** | A constructed three-contingent arrangement of one faction in which square A overlaps square B, square B overlaps square C, and A and C are disjoint. Two assertions: `FormationRules.DoCohesionSquaresOverlap` returns `false` for the A–C pair, and all three contingents are nevertheless denied cohesion on that tick. Together they pin that each denial is its own pairwise fact rather than a transitive consequence, which is the framing correction section 3.5 makes. |
| **Shape 3 — a contingent leader pinned in a map corner** | A **deliberately engineered** scenario placing a contingent's leader in a map corner with its members behind it, asserting a terminal outcome strictly inside the tick limit. This also exercises the one residual section 3.5 states honestly: the give-way aim point is still clamped, and this is the test that proves the clamp does not stall a member against a corner. |
| Map-edge open-ground test boundary | A direct assertion on the predicate: true when the bias square fits exactly, false one raw unit beyond, on each of the four comparisons independently. Exact integer comparison, no tolerance. |
| Cross-contingent test boundary | A direct assertion on the predicate: true when two squares are in exact edge contact, false one raw unit farther apart, on each axis independently; and true only when *both* axes are close, so two squares separated on one axis alone are not overlapping. Exact integer comparison, no tolerance. |
| Cross-contingent test symmetry | A direct assertion that the predicate returns the same answer with its two contingents' arguments exchanged, across a sweep of separations and margins. This is what makes "both contingents yield" a property rather than an intention. |
| Cohesion is not practically inert | A multi-seed inertness bar, defined in full under "The inertness bar" below. An earlier revision of this design set the bar at "at least one contingent reaches `Hold` on at least one tick", which a build in which cohesion fired for a moment near deployment and never again would pass. The replacement is stated as three thresholds — coverage, persistence and spread — that a burst confined to deployment cannot satisfy. |
| Straggler test boundary | A direct assertion that a member at exactly `16 * memberSquared == 9 * cohesionRadiusRaw^2` is **not** straggling and takes independent pursuit, and that one raw unit further out it is. |
| Cohesion never outlives its budget | Two assertions over a full 200-agent battle: no contingent's `ContingentState` is `Hold` for more than `CohesionDutyTicks` consecutive ticks, and — the stronger statement, and the one escape 4 actually asserts — no agent receives a cohesion destination on more than `CohesionDutyTicks` consecutive ticks in any state. Neither is a liveness proof on its own; both together are the duration bound and nothing more. |
| Degenerate map degradation | On a map too small to hold any contingent's bias square, the new preset produces the same trajectory as `IndependentPursuitV1`, so the open-ground test's total-degradation claim is asserted rather than assumed. |
| The offset never settles into a chase | An assertion that `ContingentOffset.Compute` does not depend on the tick, mirroring `RallyOffsetTests.OffsetDoesNotDependOnTheTick`. |
| Monotone attrition | Living count never increases, over a full battle under the new preset — the invariant the last-stand suite already locks. |
| Order independence | Three storage permutations of one identical roster advanced in lockstep produce identical state hashes and identical ordered events every tick, mirroring `DeterminismTests.InputArrayOrderCannotChangeOrderedResults` (`tests/Hukbo.Core.Tests/DeterminismTests.cs:478-544`). |

#### The inertness bar

Every guard in this design denies cohesion rather than adjusting it. Denial is
always safe, so a build in which cohesion is never granted passes every liveness
fact in the table above while delivering nothing the user asked for. That failure
is silent by construction, and it therefore needs an assertion that can fail.

Two definitions make the bar precise.

**A contingent's pre-`Close` window** is the ticks from tick 0 up to but
excluding the first tick on which that contingent's recorded `ContingentState` is
`Close` or `Break`. That is the whole of the interval in which cohesion is even
possible for it, and measuring against the whole battle instead would let a fight
that ends in a long melee dilute the ratio to nothing.

**A contingent is cohering on a tick** when at least one of its living members
passes all six gates of section 3.5 on that tick — that is, when at least one
member actually receives a cohesion destination. This is deliberately the
agent-level definition rather than `ContingentState == Hold`, because the design's
ordinary mode is a straggler being drawn back while the contingent's state is
`Advance`; counting only `Hold` would measure the exception and miss the rule.

The bar, asserted across the same twenty-seed 200-agent sweep the liveness fact
uses, and required of **every** seed and **every** faction:

- **Coverage.** At least half of the faction's contingents, rounded down and
  never fewer than two, cohere on at least one tick.
- **Persistence.** At least **ten percent** of the faction's pre-`Close`
  contingent-ticks, summed over its contingents, are cohering ticks.
- **Spread.** At least one cohering tick falls in the later half of the faction's
  pre-`Close` window, so a burst confined to deployment cannot satisfy the
  persistence threshold on its own.

**These three numbers are game-design thresholds, not measurements.** Nothing has
been measured yet, and nothing in the research or in this repository says what
fraction of an advance a contingent ought to spend gathering. Ten percent is
chosen to sit far enough below the duty cycle's own ceiling — the window is open
at most 180 ticks in every 240, so no more than seventy-five percent is reachable
even in principle — that it is unlikely to be a false alarm, and far enough above
zero that "fires once at deployment and never again" fails it.

**They may need adjusting once measured, and that adjustment is a recorded
decision rather than a quiet edit.** If the first real measurement comes in below
the bar, the response is to establish *why* before moving the number: the
first suspect is the chain-denial inertness analysed in section 3.5, whose
pre-analysed remedy is narrowing the cross-contingent scan to contingents that
could actually be granted cohesion. Lowering a threshold to match an observed
figure, without a stated reason for the figure, would convert the bar back into
the thing it replaced. The test's own comment carries that instruction.

Whether the result *looks* like groups to a person is not a thing a test can
settle. Smoke row 114 asks it directly and stays `PENDING` until a person runs it.

### 10.4 Other risks

| Risk | Mitigation |
| --- | --- |
| The new stage pushes tick p95 past the 10% review threshold | Measured before and after on the same workload; the budget in section 8 is an acceptance criterion, not an aspiration. If it fails, the design does not ship as written. |
| Per-tick allocation regresses | Every array sized once at construction; `BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations` is a hard pass/fail. |
| Contingent state flickers between `Advance` and `Hold` at the boundary | The hysteresis band — enter above `R`, leave below `3R/4`, expressed as `spreadSquared * 16 > 9 * R^2` — makes a single tick's jitter unable to cross both thresholds. |
| Persistent cohesion collides with the last-stand rally when a faction is reduced to six | Explicit priority: `Regrouping` beats cohesion. A faction at the last-stand threshold has no meaningful contingents, and the whole-faction rally is already tested. Stated once, in the same-tick conflict order at 3.5. |
| The duty cycle's release makes the whole army visibly stutter in unison | The `cohesionPhase` term staggers the sixteen slots by fifteen ticks each, so no two contingents pause or release on the same tick. Smoke row 103 is written to check that what a spectator sees reads as a group gathering and resuming, not as an army-wide hitch. |
| Cohesion is silently never granted because the map-edge test always fails on some map | The degenerate case degrades to `IndependentPursuitV1` rather than stalling, and a test asserts that degradation explicitly on a deliberately undersized map, so the loss is visible in the suite rather than discovered by a spectator. |
| Cohesion is silently never granted because the cross-contingent test always fails — contingents deploy too close together to ever be disjoint | This is the new gate's own failure mode and it is a silent loss of the feature, not a stall. On the canonical workload it does not occur at deployment: `FormationPlanner` deploys contingents in lanes stacked along Y, and adjacent lane centres are about 35.6 body radii apart against a 20-body-radius touching threshold, as section 3.5 works out. Because that margin is a property of the map and the population rather than of the design, it is not assumed — section 10.3's inertness bar is the assertion that fails loudly if the gate becomes universal. |
| Several contingents converging abreast deny each other simultaneously and persistently, so cohesion is inert for the whole advance | This is the many-contingent form of the row above and it is the more likely one, because eight contingents closing on one engagement point all trail their squares on the same side. Section 3.5's chain-denial subsection analyses it, records that the OR rule stays and why the "pairwise rather than transitive" alternative does not exist, and names the pre-analysed remedy — narrowing the scan to contingents that could actually be granted cohesion. It is a product failure rather than a stall, so the guard is the inertness bar's coverage, persistence and spread thresholds, plus smoke row 114 for the part only a person can judge. |
| Independently-pursuing bodies crossing a granted bias square exhaust the packing margin | **Unbounded, and stated as unbounded.** No arithmetic in this design limits how many agents that are not aimed at a granted square may occupy it. The fourfold packing margin is the allowance — three quarters of every granted square is unoccupied by its own contingent's aim points — and section 10.3's crossing-traffic scenario is the only evidence that the allowance suffices. If that scenario fails, the design does not ship as written; the remedies then available are raising the packing margin above four, or adding an occupancy test to the gate, and neither is designed here. |
| Eight contingent ground tints are indistinguishable at the default fit | A smoke row, honestly marked `PENDING`. No test can settle it and no agent may flip it. |
| `FormationPlanner`'s doc comment contradicts the shipped behaviour | Revising it is an explicit task, not a side effect. Leaving a type-level remark standing that says the opposite of what the code does is how the next reader is misled. |
| The eight-stage tick order is quoted in two documents | Updating `SIMULATION-GAME-STANDARDS.md:508-521` and `docs/research/TICK-STAGE-PROFILE.md:68-96` to nine stages is an explicit task. |
| A future contributor reads a provisional constant as a historical measurement | Every new constant carries a Provisional-reconstruction statement in its own XML doc comment, matching `FormationRules.cs:1-8`. |

---

## 11. Out of scope

Named explicitly so that no task quietly grows into one of them.

- **A general neighbour-avoidance or "step around any blocking ally" rule.** Needs
  a bounded neighbour query and a second uniform-grid rebuild; deferred behind the
  collision-scaling work. See section 13.
- **Any change to `CollisionResolver`** — its candidate ladder, boundary clamp,
  co-location repair, priority key, or committed-position logic. Reserved by
  `docs/plans/2026-07-28-collision-resolution-scaling-design.md`.
- **Pathfinding, navigation graphs, terrain, walkability, or waypoints.** Gated.
- **Morale, fear, resolve, retreat, rout, or pursuit.** Gated. `Break` turns
  cohesion off and nothing more.
- **Velocity, acceleration, mass, momentum, or any rigid-body physics.** Forbidden
  by the formation-and-collision scope guardrails and CLAUDE.md section 9.
- **Any new `AgentIntent` value.** The four unit states live on their own enum;
  `AgentIntent` is untouched.
- **Any new `BattleEventKind`.**
- **Any new theme role.** Contingent tints are derived presentation-only from the
  existing `TeamA` and `TeamB` roles.
- **Save/resume equivalence.** No `BattleSimulation.FromSnapshot` or `Resume`
  exists in `Hukbo.Core`; `CreateSnapshot` is one-way. Building it is Gate 3 work
  and is not attempted here. See section 13.
- **Changing `FormationPlanner`'s positions, spacing, lattice, jitter, contingent
  count, dealing order, or random draw count.** Only its return shape changes.
- **Any campaign, economy, diplomacy or map-generation concept.**
- **Any Filipino-language label for a contingent, a leader, or a unit state.** See
  section 4.3.
- **Re-measuring the stale four-point agent-count scaling sweep** in
  `docs/development/testing.md`. Flagged in section 13; not this workstream's job
  unless the user says so.

---

## 12. The nine feature-acceptance questions

`SIMULATION-GAME-STANDARDS.md:320-330`, answered for the feature as a whole.

**1. What is the user-visible outcome?**
Each faction stays legible as several distinct groups well past the opening frame
instead of merging into one crowd. A group gathers when it strings out, advances
as a body, and dissolves into individual fighting when it reaches the enemy.
Warriors ease into contact rather than snapping to a halt. Selecting any warrior
names its contingent and what that contingent is currently doing. Under the frozen
preset, nothing changes at all.

**2. Which tick stage does it run in, and what state does it read and write?**
A new ninth stage, `ResolveContingentStates`, between `SelectTargetsAndIntents`
and `GatherMovementProposals`. It **reads** tick-start `XRaw`/`YRaw`, `IsAlive`,
`FactionId`, `EntityId`, `ContingentId`, `Intent`, `TargetEntityId`, and the
previous tick's `ContingentState` from each contingent's current leader. It
**writes** `AgentState.ContingentState` on every living agent, and otherwise only
its own preallocated per-contingent scratch arrays — leader entity id, living
count, initial count, trail base, margin, and one flag each for the map-edge and
cross-contingent geometric gates — every one of which is fully overwritten each
tick and holds nothing across a tick boundary.
`GatherMovementProposals` gains a branch that reads `ContingentId` and
`ContingentState`, re-evaluates the duty-cycle predicate — a pure function of
`Tick` and `slot`, so the two stages cannot disagree about it — reads the two
geometric gate flags the stage above already computed rather than recomputing
them, and writes only its existing `_movementProposals` entry.
`BuildMovementProposal` gains the arrival taper under the new preset.
`BattleSimulation.Create` writes `AgentState.ContingentId` once.

**3. What are the numeric units and bounds, and what is the same-tick conflict
rule?**
All positions and radii are raw fixed-point units; all counts and ticks are
integers. `ContingentId` is in `[0, 7]`; the slot index is in `[0, 15]`.
`CohesionRadiusMultiplier = 24`, `CloseRadiusMultiplier = 16`,
`MinimumCohesiveMembers = 3`, `ArrivalTaperMultiplier = 4`,
`CohesionCycleTicks = 240`, `CohesionDutyTicks = 180`, `OffsetUnit = 1024`; the
attrition trigger is `livingCount * 4 <= initialCount`, the hysteresis exit is
`spreadSquared * 16 <= 9 * cohesionRadiusRaw^2`, and the straggler test is the
same expression at the individual scale,
`16 * memberSquared > 9 * cohesionRadiusRaw^2`, strict, with exact equality
counting as not straggling. The derived quantities are
`jitterRaw = BodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)` and
`trailRaw = ((3 * jitterRaw + 1) / 2) + 3 * BodyRadiusRaw`, and the bias square's
half-side is `marginRaw = jitterRaw + BodyRadiusRaw`. Two same-faction bias
squares overlap when `|aTrailBaseX - bTrailBaseX| <= aMarginRaw + bMarginRaw`
and the same holds on Y, non-strict, so exact edge contact counts as
overlapping. `Scenario.Validate`
rejects a body radius for which any of these overflows `Int32`, using the
`IsBodyRadiusWithin*Range` guard pattern `FormationRules.cs:197-299` already
establishes. The same-tick conflict order is
`Dead > Attacking > Regrouping > contingent cohesion > ordinary pursuit`, where
the cohesion branch is reached only after the six gates in 3.5 — state, leader
exemption, duty cycle, straggler test, map-edge open-ground test,
cross-contingent overlap test — have all been passed.
The unit-state transition rules in 3.4 are strictly priority-ordered with the
first match winning.

**4. What total ordering applies, and what is the random-stream policy?**
The full table is at 3.7. One new random draw exists,
`ContingentOffset.Compute(seed, entityId, jitterRaw)`, on its own fresh domain
tag `HKBO_CTG = 0x484B424F5F435447`, seeding a fresh `SplitMix64` per call,
drawing two values, excluding the tick deliberately, taken in ascending
agent-array order inside `GatherMovementProposals`, and reached only under the
new preset and only by an agent that has passed all six gates in 3.5. How many
such calls a tick makes therefore varies with the simulation's own state; that
is safe because the generator is freshly seeded per call and advances no shared
stream, so neither the count nor the order can shift any value anywhere. Section
5.2 sets out that argument in full. No existing draw is moved, reordered, or
reused. `System.Random` appears nowhere.

**5. What cache does it add, or does it add none?**
**None.** The leader and the living count are recomputed by a fresh forward scan
every tick. The personal offset is a pure function of `(seed, entityId)`,
recomputed rather than stored. `ContingentId` is authoritative immutable state,
not a cache. The sixteen-slot scratch arrays are preallocated at construction and
fully overwritten every tick, so they hold nothing across a tick boundary.

**6. What is the save, event, and version effect?**
No new `BattleEventKind` and no change to any event's fields, so the event hash is
unaffected by the scaffolding and moves only when the behaviour itself changes
committed positions. Three new values enter the state hash —
`Scenario.MovementPreset`, `AgentState.ContingentId`, `AgentState.ContingentState`
— folded together in a single behaviour-inert task, so the state hash moves
exactly once for representational reasons and once more when the default preset
flips. `BattleSnapshot` gains the two fields transitively through `AgentView`. A
new version axis, `MovementPresetId`, ships with `IndependentPursuitV1` frozen and
registered so a replay naming it still reproduces.

**7. What is the worst-case complexity and the benchmark workload?**
Two O(n) forward passes per tick plus O(1) per moving agent; at most sixteen
constant-time state transitions; and one pairwise scan over same-faction
contingent slots, bounded at `C(8, 2) = 28` pairs per faction and 56 in total by
`FormationPlanner`'s eight-contingent cap
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:45`), which does not grow with
agent count. No new spatial query and no new pass over
neighbours. The workload is the canonical 200-agent / 10,000-tick / seed-1
headless run through `scripts/benchmark.ps1`, plus the 500-agent stress workload
the standards require, both reported with the full environment block. Acceptance:
the new stage under 5% of tick p95, total tick p95 regressing no more than 10%,
and the per-tick allocation test passing unchanged.

**8. How is it explained to a spectator?**
Primarily by the behaviour itself, which is what the new smoke rows check.
Secondarily by a new agent-inspector row after `Intent:` reading
`Contingent: <n> — <state>`, omitted entirely when the state is `None`.
Additionally by a per-contingent lightness step on the existing pawn ground-base
tint, derived presentation-only from the existing `TeamA` and `TeamB` theme roles.
No new event kind, no new texture, no new theme role.

**9. Which tests fail before the implementation and pass afterward?**
The full list is section 10.3 plus: the movement preset registry's
`IsRegistered`/`Get` behaviour and its per-version pinned `ContentHash`;
`Scenario.Validate` accepting a registered movement preset and rejecting an
unregistered one, plus the default-value and equality/hash-code differentiation
tests `ScenarioTests.cs:400,415-426,473,483` establishes as the convention;
`FormationPlanner` returning membership matching `localIndex % contingentCount` on
both the lattice and the dense-block paths; the frozen-behaviour digest fixture
reproducing under `IndependentPursuitV1`; a lockstep double-simulation producing
identical hashes and identical ordered events every tick under the new preset,
with no pinned hash literal so it survives legitimate hash movement; the arrival
taper never increasing a step and never returning zero, swept directly over
`MovementRules.ComputeArrivalStepRaw`; the state machine's six transition rules
asserted directly against the pure
`MovementRules.ResolveContingentState`, including every pair where two rules
would fire and the higher-priority one must win; the six movement gates asserted
as a conjunction over an exhaustive truth table, so their evaluation order is
shown to be immaterial; leader selection and death promotion asserted directly
against the pure leader-and-count scan, and unchanged under array permutation;
the three deliberately engineered deadlock scenarios — two same-faction
contingents converging with no enemy nearby, built to the worst-case
overlapping-trailing-square geometry rather than an arbitrary crossing; a
granted bias square with independently-pursuing same-faction traffic routed
through it; and a contingent leader pinned in a map corner — each reaching a
terminal outcome inside its tick limit; the companion fact that the
cross-contingent gate demonstrably fired during the converging run; the
companion fact that the crossing-traffic run really did grant cohesion while
foreign bodies were inside the square; the three-contingent chain fact; the
multi-seed inertness bar's coverage, persistence and spread thresholds, so a
build in which cohesion is practically inert could not pass unnoticed; the
straggler, map-edge and cross-contingent boundary cases at exact equality, and
the cross-contingent predicate's symmetry; and the inspector row's presence,
absence, ordering and label text as pure-helper assertions that construct no
graphics device.

---

## 13. Open questions for the user

These could not be decided without the user and are not silently resolved
anywhere above.

1. **Should the shipped default `Scenario.MovementPreset` flip to
   `PersistentContingentsV2` in this workstream, or stay at
   `IndependentPursuitV1` until a later decision?** There is precedent for a
   default one version behind the newest: `Scenario.CombatPreset` still defaults to
   V2 while V3 is registered. The plan carries the flip as its own final task so
   it can be dropped without disturbing anything before it.

2. **Is the general "step around whichever ally is blocking me" rule in scope
   now, or deferred?** It requires a bounded neighbour query and therefore a second
   uniform-grid rebuild, in a tick already 63–75% collision-bound, in an area
   `docs/plans/2026-07-28-collision-resolution-scaling-design.md` has reserved. This
   design recommends deferring it until that work lands, and delivers "flow around
   blockers" through the give-way rule and the collision resolver's existing slide
   candidates in the meantime.

3. **Does the four-point agent-count scaling sweep in
   `docs/development/testing.md` get re-measured before this workstream cites it?**
   It is still keyed to `stateHash 71211929A44A16CA`, which `testing.md` itself
   flags as stale after the combat-preset-V3 merge. This design measures against
   the current baseline instead and does not cite the sweep, but the stale table
   remains in the document.

4. **Is save/resume equivalence built now or explicitly deferred?** No
   `BattleSimulation.FromSnapshot` or `Resume` exists in `Hukbo.Core`;
   `CreateSnapshot` is one-way. CLAUDE.md lists save/resume equivalence as a Gate 3
   requirement, and this design does not attempt it. If it is wanted for this
   feature, it is its own design document, not a task in this plan.

5. **Are eight per-contingent ground tints the right spectator channel, or should
   contingent identity be inspector-only?** Eight lightness steps derived from one
   faction colour may not stay distinguishable at the default camera fit. Only a
   person watching can settle it, and the smoke row is written to ask exactly that.
   The fallback — inspector-only — costs nothing to adopt.

6. **Is an unbounded body-occupancy residual inside a granted bias square
   acceptable, given that the evidence for it is one engineered test rather than
   an argument?** This is the honest open item in the deadlock analysis and it is
   the one the document has now been wrong about twice. Section 3.5 states it in
   full: gates 5 and 6 bound the *aim-point* density of a granted square at a
   quarter of capacity, and nothing in this design bounds how many agents that are
   not aimed at that square may stand in it or walk through it. The fourfold
   packing margin is the allowance and section 10.3's crossing-traffic scenario is
   the measurement. If the user wants a bound rather than an allowance, the two
   candidate mechanisms are raising the packing margin above four, which costs
   spread and therefore visual tightness, or adding a body-occupancy test to the
   cohesion gate, which costs a bounded neighbour query in a tick already 63–75%
   collision-bound. Neither is designed here and neither should be built without
   the measurement first.

7. **Are the inertness bar's three thresholds — half the contingents, ten percent
   of pre-`Close` contingent-ticks, and at least one cohering tick in the later
   half of that window — the right bar?** They are game-design inventions with no
   measurement behind them, chosen so that a build in which cohesion fires briefly
   at deployment and never again fails. They may prove too strict or too loose on
   first measurement. Section 10.3 records that the response to a failure is to
   establish the cause before moving the number, and that the first suspect is
   chain denial across converging contingents.

8. **If the inertness bar fails, should the cross-contingent scan be narrowed to
   contingents that could actually be granted cohesion, excluding `Close` and
   `Break`?** Section 3.5 analyses the narrowing, records that it preserves the
   combined-density statement exactly, and declines to adopt it now because it
   shifts load onto the unbounded residual in question 6. The ordering is
   deliberate — measure the residual first, then narrow — and the user may prefer
   the opposite ordering, or may prefer the narrowing outright on the grounds that
   an inert feature is a worse outcome than a widened residual.
