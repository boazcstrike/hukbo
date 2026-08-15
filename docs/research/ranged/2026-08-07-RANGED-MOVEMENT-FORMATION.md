# Ranged Units — Movement and Formation Ground Truth

**Date:** 2026-08-07
**Branch / worktree:** `ranged-units` (`.claude/worktrees/ranged-units`)
**Status:** Read-only research. No code was changed to produce this document.
**Audience:** the planner for the four-ranged-weapon package.

## Purpose and scope

Four ranged weapons are being added to Hukbo. They will need movement mechanics
— an approach to a firing distance, a hold at that distance, a fall-back when an
enemy closes — and a formation concept, meaning a skirmish screen ahead of the
melee line and a shooting rank behind it.

This document records what the movement and formation layers of `Hukbo.Core`
**actually do today**, including the parts that are broken and the parts that
exist only as an aspiration in a design document. It is deliberately descriptive.
It proposes nothing and designs nothing. Where a doc describes behaviour that the
code does not implement, that gap is stated as a gap.

Every claim below carries a `file:line` reference. Line numbers are as of the
`ranged-units` worktree at the time of writing.

### Method note

The repository's `CLAUDE.md` section 8 requires code discovery through the
`tokensave` MCP tools or the `codebase-memory-mcp` graph. Neither tool set was
exposed to the session that produced this document; a tool search for
`tokensave`, `search_graph`, `get_code_snippet`, and `search_code` returned no
matches. Discovery therefore fell back to `Grep`, `Glob`, and `Read` over the
worktree. No `Explore` agent was used. Every symbol named below was read in its
own file rather than inferred from a name.

---

## 1. The movement pipeline as it actually is

### 1.1 Which preset is actually running

Two movement presets matter to any reading of this pipeline, and they are not
the same one.

- The **shipped default** is `MovementPresetId.PersistentContingentsV4`, set as
  the initialiser on `Scenario.MovementPreset` at
  `src/Hukbo.Core/Simulation/Scenario.cs:88-89`. Every battle a spectator starts,
  and the 200-agent seed-1 workload the canonical gate runs, is fought under V4
  unless a caller overrides the property explicitly.
- The **most elaborate presets**, `EquipmentRelativeFootworkV6` and
  `EquipmentRelativeFootworkV7`, exist and are registered, but are reachable only
  through explicit selection. Their own enum documentation says so in as many
  words: "It is reachable only through explicit selection — the shipped default
  stays `PersistentContingentsV4`" at
  `src/Hukbo.Core/Movement/MovementPresetId.cs:112-114` (V6) and
  `src/Hukbo.Core/Movement/MovementPresetId.cs:138-140` (V7).

This distinction runs through everything below. V6 and V7 are where the
approach / hold / fall-back vocabulary already exists — `FootworkPhase.Approach`,
`Engage`, `Disengage`, a per-loadout `PreferredDistanceBasisPoints`. None of it
runs in the default battle.

### 1.2 The stages, in tick order

`BattleSimulation.AdvanceOneTick` is at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:599`. It is the only tick entry
point, and the stage order below is literally the order of the calls in its body.
Three of the stages are gated on `_movementRules.UsesEquipmentRelativeFootwork`
and therefore never execute under the default V4 preset.

| # | Stage | Call site | Definition | Runs under V4? |
| --- | --- | --- | --- | --- |
| 1 | `DecrementCooldowns` | `BattleSimulation.cs:616` | `BattleSimulation.cs:941` | yes |
| 2 | `SelectTargetsAndIntents` | `BattleSimulation.cs:617` | `BattleSimulation.cs:952` | yes |
| 3 | `ResolveContingentStates` | `BattleSimulation.cs:618` | `BattleSimulation.cs:1221` | yes |
| 4 | `ResolveEquipmentPosturesAndProvisionalFootwork` | `BattleSimulation.cs:624` | `BattleSimulation.cs:1613` | **no** — V6/V7 only |
| 5 | `GatherMovementProposals` | `BattleSimulation.cs:627` | `BattleSimulation.cs:1500` | yes |
| 6 | `ResolveFriendlyClearanceConflicts` | `BattleSimulation.cs:634` | `BattleSimulation.cs:2718` | **no** — V6/V7 only |
| 7 | `ResolveCollisions` | `BattleSimulation.cs:637` | `BattleSimulation.cs:3386` | yes |
| 8 | `CommitMovement` | `BattleSimulation.cs:638` | `BattleSimulation.cs:3425` | yes |
| 9 | `MeasureCollision` | `BattleSimulation.cs:639` | `BattleSimulation.cs:3494` | yes |
| 10 | `GatherAndCommitAttacks` | `BattleSimulation.cs:640` | `BattleSimulation.cs:3579` | yes |
| 11 | `ApplyEquipmentAttackFootworkAndDeathCleanup` | `BattleSimulation.cs:646` | `BattleSimulation.cs:2797` | **no** — V6/V7 only |
| 12 | `ResolveOutcome` | `BattleSimulation.cs:649` | `BattleSimulation.cs:3981` | yes |
| 13 | `UpdateViews` | `BattleSimulation.cs:651` | `BattleSimulation.cs:4260` | yes |

This matches the stage list recorded in `SIMULATION-GAME-STANDARDS.md:513-523`,
with the three equipment-relative stages being additions the standards document's
stage block does not list.

### 1.3 Perception and target selection

`SelectTargetsAndIntents` (`BattleSimulation.cs:952`) is an O(n²) scan: for every
living agent it walks the entire `_agentStates` array
(`BattleSimulation.cs:1013`). Three filters apply, in order:

1. Same-faction and dead candidates are skipped at
   `BattleSimulation.cs:1015`. Under V6/V7 only, living allies are diverted into
   a local-context accumulator at `BattleSimulation.cs:1024-1036` before the
   `continue`.
2. Two cheap axis-aligned rejections against the unsquared perception range at
   `BattleSimulation.cs:1050-1061`.
3. The squared perception test at `BattleSimulation.cs:1063-1067`.

`PerceptionRangeRaw` defaults to `2_048 * FixedPoint.Scale`
(`src/Hukbo.Core/Simulation/Scenario.cs:34`) — two thousand and forty-eight world
units, on a map that defaults to 1,280 by 720
(`Scenario.cs:25-26`). **Perception is effectively unlimited on the default
map.** Every living enemy is perceived by every living agent on every tick. There
is no facing cone, no line of sight, and no occlusion anywhere in the selection
loop.

Target selection is nearest-enemy with a tie-break on lower `EntityId`
(`BattleSimulation.cs:1082-1089`). It is unconditional: there is no weapon-aware
target preference, no "shoot the nearest thing I can hit", and no concept of a
target being unreachable.

Intent is then assigned at `BattleSimulation.cs:1112-1115`:

```csharp
agent.Intent = selectedDistance <= CollisionGeometry
    .ContactSquaredDistance(Scenario.BodyRadiusRaw)
    ? AgentIntent.Attacking
    : AgentIntent.Moving;
```

`AgentIntent` has exactly five members —
`Idle`, `Moving`, `Attacking`, `Dead`, `Regrouping` — at
`src/Hukbo.Core/Simulation/AgentIntent.cs:1-24`. There is no `Holding`, no
`Firing`, no `Falling back`.

### 1.4 Proposal building

`GatherMovementProposals` (`BattleSimulation.cs:1500`) dispatches to the
equipment-relative pipeline and returns early when the preset opts in
(`BattleSimulation.cs:1502-1509`). Under V4 it continues into the legacy body.
For each living agent whose `Intent` is `Moving` and which holds a target:

- If a contingent cohesion aim point resolves
  (`TryResolveContingentCohesionAimPoint`, `BattleSimulation.cs:2907`, called at
  `BattleSimulation.cs:1535`), the proposal aims at that point.
- Otherwise the agent takes ordinary pursuit toward its target's live position:
  `BuildMovementProposal(agent, target)` at `BattleSimulation.cs:1573`, unless a
  non-zero stall generation diverts it to `BuildSidesteppingPursuitProposal`
  (`BattleSimulation.cs:1577`, defined at `BattleSimulation.cs:3074`).
- An agent whose `Intent` is `Regrouping` takes `BuildRegroupingProposal`
  (`BattleSimulation.cs:1586`, defined at `BattleSimulation.cs:3115`).

The stage reads tick-start state only and commits nothing, which the method's own
remarks state at `BattleSimulation.cs:1481-1484`.

### 1.5 The step itself

Every proposal funnels into one arithmetic core, the four-argument
`BuildMovementProposal` at `BattleSimulation.cs:4077`. It:

1. Computes the raw delta and integer-square-root distance to the destination
   (`BattleSimulation.cs:4084-4087`).
2. Subtracts `stopShortRaw` and floors at one raw unit
   (`BattleSimulation.cs:4089`).
3. Applies the arrival taper under every preset except `IndependentPursuitV1`
   (`BattleSimulation.cs:4095-4100`).
4. Scales the delta by the movement length with truncating integer division
   (`BattleSimulation.cs:4101-4102`), with a one-raw-unit fallback on the
   dominant axis if both components truncate to zero
   (`BattleSimulation.cs:4104-4114`).
5. Clamps both axes into the map bounds (`BattleSimulation.cs:4116-4123`).

There is **no steering, no separation force, no flocking, no obstacle avoidance,
and no path** anywhere in this function. The proposal is a straight-line step
toward a point. Everything that looks like crowd behaviour in a running battle
comes out of the collision resolver rejecting that straight-line step, not out of
any steering rule.

### 1.6 Collision resolution and commit

`ResolveCollisions` (`BattleSimulation.cs:3386`) builds one
`CollisionMoveRequest` per living agent, attaching a `CollisionPriority.Resolve`
key only for agents that actually proposed a move
(`BattleSimulation.cs:3409-3414`), and hands the list to
`CollisionResolver.Resolve` (`BattleSimulation.cs:3417`). The candidate ladder —
full step, X-only slide, Y-only slide, truncation ladder, hold — is the one
recorded at `SIMULATION-GAME-STANDARDS.md:564-578` and implemented in
`src/Hukbo.Core/Simulation/CollisionResolver.cs`.

`CommitMovement` (`BattleSimulation.cs:3425`) is the single position write. It
copies each result onto `AgentState.XRaw`/`YRaw`
(`BattleSimulation.cs:3446-3448`), records the blocked streak
(`BattleSimulation.cs:3449-3451`), and emits a `Move` event carrying the actual
distance moved (`BattleSimulation.cs:3480-3486`).

`MeasureCollision` (`BattleSimulation.cs:3494`) is pure observation and writes no
agent state, as its own summary says at `BattleSimulation.cs:3490-3493`.

## 2. Formation as implemented today

### 2.1 The short answer

**No positional formation system exists in code.** There is no rank, no file,
no slot, no frontage, no line, and no facing-relative ordering of any kind that
a rule reads or maintains after tick zero. The battle line a spectator sees is
entirely emergent: it is the output of straight-line pursuit meeting a solid-disc
collision resolver, exactly as `SIMULATION-GAME-STANDARDS.md:690-697` describes
it.

This is a deliberate prohibition rather than an oversight.
`SIMULATION-GAME-STANDARDS.md:417-421` states it directly: "Agents are never
assigned to a rank, a file, a slot, or a named formation. Whatever shape a battle
line takes is an emergent consequence of individual movement intent meeting the
contact rule." `ContingentState`'s own type documentation repeats it at
`src/Hukbo.Core/Simulation/ContingentState.cs:8-10`: "This is a behavioural mode,
never a positional assignment — no agent is ever assigned to a rank, a file, or a
named formation slot."

The deciding file:line for "does a formation system exist": there is no type in
`src/Hukbo.Core` that stores a position within a formation. The three types whose
names suggest one do not do it —
`src/Hukbo.Core/Simulation/FormationPlanner.cs` runs once at spawn and is never
consulted again, `src/Hukbo.Core/Simulation/FormationRules.cs` is a bag of
scalar constants and integer geometry helpers, and
`src/Hukbo.Core/Simulation/ContingentState.cs:15-38` is a five-member behavioural
enum. The single load-bearing line is
`src/Hukbo.Core/Simulation/ContingentState.cs:8-10`, quoted above.

### 2.2 What does exist: contingent membership

Four things exist that are formation-adjacent, and it is worth being precise
about each.

**One. A deployment planner that runs once.**
`FormationPlanner.PlanFactionDeployment`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:78`) computes one faction's
starting positions on a staggered lattice and returns a
`(XRaw, YRaw, ContingentId)` triple per warrior. It is called exactly once, from
`BattleSimulation.Create` at `src/Hukbo.Core/Simulation/BattleSimulation.cs:428`.
The lattice itself is explicitly disclaimed as non-carried-forward at
`FormationPlanner.cs:24-31`: "The lattice below is an engineering device for
guaranteeing that no two bodies overlap before the first tick... and it is not
itself carried forward. Contingent *membership* is."

**Two. Immutable contingent membership.** `AgentState.ContingentId`
(`src/Hukbo.Core/Simulation/AgentState.cs:114`) is a get-only property, written
once at spawn and never mutated — its own documentation says so at
`AgentState.cs:106-113`. It is an integer in `[0, 8)`, bounded by
`FormationPlanner.MaximumContingents = 8`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:56`). It names a group, not a
position within one.

**Three. A per-contingent behavioural state machine.**
`ContingentState` has five values — `None`, `Advance`, `Hold`, `Close`, `Break`
(`ContingentState.cs:22-37`) — resolved once per contingent per tick by
`BattleSimulation.ResolveContingentStates`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1221`) and written onto every
living member. It decides *whether* cohesion applies, never *where* anyone
stands.

**Four. A cohesion aim point.**
`TryResolveContingentCohesionAimPoint`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:2907`) is the closest thing in
the repository to a formation destination. What it actually computes, at
`BattleSimulation.cs:2977-2993`, is the contingent's trail base plus a
per-member jitter offset from `ContingentOffset.Compute`
(`src/Hukbo.Core/Simulation/ContingentOffset.cs:53`). That offset is drawn from
the closed span `[-jitterRaw, +jitterRaw]` independently on each axis
(`ContingentOffset.cs:49-51`, `ContingentOffset.cs:66-70`), keyed on
`(ContingentTag, seed, entityId)` and deliberately **not** on the tick
(`ContingentOffset.cs:11-22`).

The shape a cohering contingent converges on is therefore a **square blob of
pseudo-random offsets trailing behind its leader**, not a rank and not a line.
The half-side of that blob is `FormationRules.ComputeContingentJitterRaw`
(`src/Hukbo.Core/Simulation/FormationRules.cs:400`), which is
`bodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)` — sized so the blob
holds four times as many body-slots as it has bodies
(`FormationRules.cs:372-386`). The trail distance behind the leader is
`FormationRules.ComputeContingentTrailRaw` (`FormationRules.cs:479`).

### 2.3 The contingent-shape trap, checked against the function body

the contingent shape design proposes that a faction's
contingent count and sizes come from how many chiefs it fields rather than from
its headcount. **None of it is implemented.** The design's own status line at
`the contingent shape design:3-9` reads "design only. This
document does not authorize implementation... A future task-planning pass against
this document is required before any of the code below is touched."

More decisively, the design quotes the current body of
`FormationPlanner.ResolveContingentSizes` at
`the contingent shape design:16-26`, and that quoted code is
byte-for-byte what is still in the file today at
`src/Hukbo.Core/Simulation/FormationPlanner.cs:162-178`:

```csharp
var contingentCount = Math.Clamp(
    IntegerSquareRoot(warriorCount) / 2,
    1,
    Math.Min(MaximumContingents, warriorCount));
```

followed by an equal split with the remainder dealt to the earliest contingents
(`FormationPlanner.cs:169-177`). The function takes exactly one argument,
`int warriorCount`. It cannot see the roster, the ranks, or the weapons. Any
planner reading the design document must treat it as an unbuilt proposal.

### 2.4 Membership is deliberately weapon-blind

This matters directly to a ranged design. `PlanFactionDeployment` deals warriors
to contingents **round-robin by faction-local index**
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:104-107`), and the comment
immediately above it explains that the round-robin is there specifically to
prevent weapon-homogeneous contingents (`FormationPlanner.cs:100-103`):

> Warriors are dealt round-robin rather than in contiguous runs. `RosterCounts`
> groups one weapon category into a contiguous run of faction-local indices, so
> contiguous contingents would come out weapon-homogeneous — a stronger claim
> than the evidence supports.

So today, a contingent is a deliberately mixed-weapon group. **There is no way,
under any shipped code path, to produce a contingent that is all one weapon
type**, which is what a "skirmish screen" or a "shooting rank" would need if it
were expressed as a contingent.

### 2.5 The one equipment-aware placement rule, and its limits

`EquipmentDeploymentAssignment.AssignForFaction`
(`src/Hukbo.Core/Movement/EquipmentDeploymentAssignment.cs:56`) is the only
place in the repository where equipment influences where a warrior starts. It
runs only under a preset with `UsesEquipmentRelativeFootwork` — it throws
otherwise, at `EquipmentDeploymentAssignment.cs:64-71` — and it is called only
inside the `if (movement.UsesEquipmentRelativeFootwork)` block at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:455-472`. **It therefore never
runs under the shipped V4 default.**

Even when it does run, it is strictly a permutation *within one contingent*: it
sorts that contingent's existing slots by how isolated they are and its warriors
by `AllyClearanceBodyDiametersBasisPoints` descending, then pairs them
(`EquipmentDeploymentAssignment.cs:164-207`). Its own summary states the limit at
`EquipmentDeploymentAssignment.cs:10-13`: "Nothing about the formation itself
changes: contingent membership, slot coordinates, the lattice, the jitter, and
the SplitMix64 draw count all stay exactly as planned."

It cannot move a warrior forward or backward relative to the enemy. It cannot
put one weapon type in front of another. It ranks by *elbow room*, not by depth.

### 2.6 The last-stand rally

The one behaviour in the codebase that a player would call a formation is the
last-stand rally, contracted at `SIMULATION-GAME-STANDARDS.md:718-786`. Every
surviving member of a faction at or below
`Scenario.LastStandThresholdAgents` (default `6`,
`src/Hukbo.Core/Simulation/FormationRules.cs:104`) that is not the rally agent
and not already in contact is marked `AgentIntent.Regrouping`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1121-1128`) and aims at a
trail-plus-jitter point behind the rally agent
(`BuildRegroupingProposal`, `BattleSimulation.cs:3115`). It is the same
trail-plus-jitter blob shape as contingent cohesion, and it engages only in the
endgame at six or fewer survivors. It is not a battle formation.

## 3. Approach, engage, and disengage

### 3.1 Under the shipped V4 default there is only "approach"

An agent under `PersistentContingentsV4` has exactly one movement decision:
walk toward the nearest enemy until the bodies touch. There is no stop-at-range
behaviour, no hold, and no fall-back.

**The stopping distance is decided in one place**, the two-argument
`BuildMovementProposal` at `src/Hukbo.Core/Simulation/BattleSimulation.cs:4039`,
whose whole content is a delegation carrying one argument that matters:

```csharp
stopShortRaw: checked(2 * Scenario.BodyRadiusRaw));   // BattleSimulation.cs:4054
```

**It is measured centre to centre against the body diameter, not against weapon
reach.** At the default `BodyRadiusRaw` of `4352`
(`src/Hukbo.Core/Simulation/CollisionRules.cs:72`, 4.25 world units) the stop
line is `8704` raw units — 8.5 world units — while the default
`AttackRangeRaw` is `12288` raw (12 world units,
`src/Hukbo.Core/Simulation/Scenario.cs:32`). The comment at
`BattleSimulation.cs:4047-4053` records why: stopping at reach left permanent air
between the two front ranks, so bodies never met.

Three consequences follow, and all three are load-bearing for a ranged design:

1. **An agent inside weapon reach keeps walking in.** The standards document
   states it flatly at `SIMULATION-GAME-STANDARDS.md:476`: "An agent already
   inside reach keeps walking in."
2. **The only thing that ever stops an agent short of body contact is another
   body.** The arrival taper at `BattleSimulation.cs:4095-4100` only shortens the
   step within four body radii of the destination
   (`arrivalTaperMultiplier: 4`,
   `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:181`); it never halts.
3. **There is no code path under V4 that moves an agent away from an enemy.**
   Grepping the V4-reachable proposal builders finds `BuildMovementProposal`
   (toward a target or a point), `BuildSidesteppingPursuitProposal`
   (`BattleSimulation.cs:3074`, an oblique *around* a blocking body, still
   closing), and `BuildRegroupingProposal` (`BattleSimulation.cs:3115`, toward
   the rally agent). Retreat does not exist.

### 3.2 The V6/V7 phase machine, and what it does not do

`FootworkPhase` (`src/Hukbo.Core/Movement/FootworkPhase.cs:13-66`) is the only
place in the repository with approach/hold/fall-back vocabulary:
`Approach = 1`, `Engage = 2`, `Commit = 3`, `Recover = 4`, `Refuse = 5`,
`Disengage = 6`, `Regroup = 7`, `Pursue = 8`. The phase is resolved by
`WeaponMovementRules.ResolveProvisionalFootwork`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs:565`) through a first-match
ladder:

| Step | Condition | Result | Line |
| --- | --- | --- | --- |
| 1 | dead | `None` | `WeaponMovementRules.cs:581-584` |
| 1a | pressure interrupt fired (V7 only) | `Disengage` | `WeaponMovementRules.cs:598-601` |
| 2 | prior phase `Commit` | `Commit` or `Recover` | `WeaponMovementRules.cs:605-610` |
| 3 | prior phase `Recover`, timer > 1 | `Recover` | `WeaponMovementRules.cs:614-617` |
| 4 | already disengaging, ratio above release | `Disengage` | `WeaponMovementRules.cs:625-630` |
| 5 | enemy-to-ally ratio at or above entry | `Disengage` | `WeaponMovementRules.cs:633-637` |
| 6 | posture `Withdraw` or `Yield` | `Disengage` | `WeaponMovementRules.cs:640-643` |
| 7 | posture `Regroup` | `Regroup` | `WeaponMovementRules.cs:646-649` |
| 8 | target at or inside preferred distance | `Engage` | `WeaponMovementRules.cs:653-656` |
| 9 | has a target | `Approach` | `WeaponMovementRules.cs:659-662` |
| 10 | posture `Pursue` / otherwise | `Pursue` / `None` | `WeaponMovementRules.cs:665-667` |

The preferred distance in step 8 is
`MovementRouteRules.EffectivePreferredDistanceRaw`
(`src/Hukbo.Core/Movement/MovementRouteRules.cs:280`):

```
attackRangeRaw * (PreferredDistanceBasisPoints + opponentOffsetCell) / 10000
```

computed at the call site `src/Hukbo.Core/Simulation/BattleSimulation.cs:1674-1682`
and compared inclusively on squared values.

**The critical fact: the preferred distance is not a stop line.** The code says
so in its own comment, at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:2116-2118`:

> The preferred distance is not a stop line: both phases continue toward the
> target's centre so the existing post-movement reach test stays authoritative.

`Approach` and `Engage` share one arm of the route-candidate switch
(`BattleSimulation.cs:2101-2102`) and both emit the same direct-toward-the-target
candidate (`BattleSimulation.cs:2119-2124`). The only differences `Engage`
produces are (a) an ordering preference for the two oblique candidates when the
local enemy composition holds two or more occupied loadout buckets
(`BattleSimulation.cs:2145-2159`), and (b) whatever pace cap the facing-to-travel
sector separation resolves. **There is no hold-at-range behaviour anywhere in the
repository.** `PreferredDistanceBasisPoints` is a phase-label threshold, not a
standoff distance.

A test pins this deliberately, and names it. The doc comment on
`EngageCrossesThePreferredBandAndAttacksTheSameTick` at
`tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:141-148`
opens "Contract H: Engage is not a stop line," and the case has two warriors open
inside the preferred band (5,520 against a band of 5,888) but outside attack
range (5,120), close 256 units each on the first tick, and land attacks on that
same tick.

Its registered values reinforce that reading: `11_500` for the Kampilan
(`src/Hukbo.Core/Movement/Profiles/KampilanMovementProfile.cs:29`) and `11_000`
for the Itak (`src/Hukbo.Core/Movement/Profiles/ItakMovementProfile.cs:31`) —
115% and 110% of attack reach, with per-opponent offsets bounded to ±2,000 basis
points by construction validation
(`src/Hukbo.Core/Movement/LoadoutMovementProfile.cs:84-91`). The whole expressible
range today sits between roughly 0.9× and 1.35× of a warrior's own melee reach.

### 3.3 Disengage does not mean "back away"

`Disengage` is the only phase that could serve as "fall back when closed on", and
what it actually proposes is not a retreat. `BuildEquipmentRouteCandidates`'s
`Disengage`/`Regroup` arm (`src/Hukbo.Core/Simulation/BattleSimulation.cs:2254`)
emits, in order:

1. A route toward the **nearest living ally**
   (`BattleSimulation.cs:2259-2278`).
2. A route toward the **contingent leader** (`BattleSimulation.cs:2280-2304`).
3. Only if neither anchor exists at all, a route directly away from the threat —
   the "escape fallback", gated at `BattleSimulation.cs:2306-2320`, whose comment
   states "only `Disengage` owns the escape fallback, and only when neither
   anchor exists at all".

In a crowded battle a living ally essentially always exists, so `Disengage` in
practice means *run toward your friends*, which on a converged front line means
sideways or forward, not backward. The nearest genuine backward step in the
codebase is the `Recover` arm at `BattleSimulation.cs:2199-2251`, which steps
opposite the agent's own facing for the duration of the post-attack recovery
timer only.

### 3.4 Summary table for a ranged design

| Behaviour a ranged unit needs | Exists? | Nearest thing | File:line |
| --- | --- | --- | --- |
| Close to a chosen distance | no | closes to body contact only | `BattleSimulation.cs:4054` |
| Stop at a distance and hold | **no** | phase label only, never a stop | `BattleSimulation.cs:2116-2118` |
| Back off when an enemy closes | **no** | `Disengage` moves toward allies | `BattleSimulation.cs:2254-2312` |
| Per-weapon preferred distance | yes, V6/V7 only | `PreferredDistanceBasisPoints` | `LoadoutMovementProfile.cs:195` |
| Per-weapon reach | yes | `WeaponProfile.AttackRangeRaw` via `CreateAgent` | `BattleSimulation.cs:886` |

## 4. The standoff

### 4.0 A correction to the premise, stated first

The task that produced this document described the game as "currently producing
10,000-tick draws". The measurement record does not support that as a statement
about the shipped game, and the distinction changes what a ranged design has to
worry about.

**Under the shipped default `PersistentContingentsV4`, battles terminate.** The
task F2 re-measurement arm at
the movement V7 calibration record (archived)
ran ten cells — seeds 1, 2, 3, 5, 8 at both 200 and 500 agents — on
`PersistentContingentsV4` and every one reached a decisive outcome:

| Agents | Terminal tick range | Outcomes |
| --- | --- | --- |
| 200 | 1,279 – 2,284 | 3 × `Faction0Victory`, 2 × `Faction1Victory` |
| 500 | 2,551 – 4,405 | 4 × `Faction0Victory`, 1 × `Faction1Victory` |

Source: the table at
the movement V7 calibration record (archived),
summarised at line 712: "All ten V4 cells reach a decisive outcome, between
1,279 and 4,405 ticks."

**The 10,000-tick standoff is a property of the equipment-relative footwork
branch — `EquipmentRelativeFootworkV6` and `EquipmentRelativeFootworkV7` — and
of nothing else.** That branch is not the shipped default, and it is exactly the
branch that carries the per-weapon movement vocabulary a ranged weapon would
want to extend. That is the whole significance of this section.

### 4.1 The measured facts

Every figure below comes from two archived measurement records. They are
archived, and under `CLAUDE.md` section 6 an archived plan may not be cited as
justification for a change or treated as a current instruction. They are cited
here strictly as *the record of a measurement that was taken*, which is the one
use the archive rules explicitly preserve
(`CLAUDE.md`: archives are "kept only so a past decision can be traced to its
reasoning"). No live document in `docs/research/movement/`,
`docs/development/testing.md`, or `docs/plans/` restates these numbers; a search
of `docs/` for "standoff" returns only the archived files, this document, and its
two sibling ranged-package research files.

**Fact one — all ten V6 cells drew at the tick limit.**

> All ten `EquipmentRelativeFootworkV6` cells ended in `Draw` at the 10,000-tick
> limit. Not one of them reached a decisive outcome.
> — the movement V7 baseline record (archived)

The per-cell table is at
the movement V7 baseline record (archived).
Survivors when the limit arrived ran between 43.4% and 75.5% of the starting
roster (`baseline.md:289-298`), with the two sides finishing within nine warriors
of each other in every 200-agent cell and within five in every 500-agent cell
(`baseline.md:300-305`). The document's own conclusion, at `baseline.md:304-305`:
"This is not a battle that was nearly decided and ran a little long. It is a
standoff."

**Fact two — the ratio. 349 to 1.** From the 200-agent seed-1 cell's
`movementMetrics` block, recorded at
the movement V7 baseline record (archived)
and again at
the movement V7 calibration record (archived):

| Metric | Agent-ticks |
| --- | --- |
| `refuseAgentTicks` | 1,140,221 |
| `regroupAgentTicks` | 338,634 |
| `commitAgentTicks` | 2,216 |
| `recoverAgentTicks` | 2,017 |

That is **1,478,855 agent-ticks refusing or regrouping against 4,233 committing
or recovering — a ratio of about 349 to 1**
(`calibration-record.md:555-557`). Or, as the same document puts it at
`calibration-record.md:764-765`, `FootworkPhase.Refuse` and the regroup posture
are occupied "for 349 ticks out of every 350".

**Fact three — the combat consequence.** Over the same run, V6 recorded
`acceptedAttacks` 851 and `landedAttacks` 566, against 2,612 and 1,778 for the
`PersistentContingentsV4` cell at the same size and seed — a V4 run that lasted
1,279 ticks rather than 10,000. "V6 lands about a third as many blows in roughly
eight times the duration."
(Movement V7 baseline record, archived.)

### 4.2 What was tried, and what it measured

The V7 pressure interrupt was the deliberate attempt to fix this. It adds one
rule: a warrior whose weighted local pressure reaches its profile's threshold
abandons a committed blow and resolves to `FootworkPhase.Disengage`
(step 1a, `src/Hukbo.Core/Movement/WeaponMovementRules.cs:598-601`).

Six candidate tunings were measured over the three shared weights and the six
per-row thresholds, recorded individually at
the movement V7 calibration record (archived):

| Candidate | What it varied | Result |
| --- | --- | --- |
| 1 | the shipped starting values, re-confirmed | `calibration-record.md:176` |
| 2 | maximum-intervention probe: minimum threshold on every row | `calibration-record.md:190` |
| 3 | the shipped values, narrowed | `calibration-record.md:241` |
| 4 | raising the support weight | `calibration-record.md:265` — "Three draws, 758 firings" |
| 5 | halving the shipped thresholds | `calibration-record.md:292` |
| 6 | leaning on incoming damage instead | `calibration-record.md:319` |

The summary of the whole search, at `calibration-record.md:568-573`:

> On the three 200-agent cells measured under every candidate, firings ranged
> from 758 to 3,491 — a factor of 4.6 between the quietest tuning and the
> loudest possible one — and **the terminal tick did not move by a single tick
> in any cell.**

### 4.3 The conclusion: tuning cannot fix it

The reason is structural, not a matter of finding better numbers.
`WeaponMovementRules.ShouldPressureInterrupt` returns `false` unless the agent's
prior phase was `Commit` or `Recover` — the interrupt exists to preempt the
attack lifecycle, so it can only act on a warrior already inside it
(`calibration-record.md:539-543`). The addressable population is therefore the
4,233 commit-or-recover agent-ticks, which the record calls "under three tenths
of one per cent of the run" (`calibration-record.md:557-559`).

The verdict table at `calibration-record.md:717-727` records **FAIL** on the
termination bar (0 of 10 cells decisive against a requirement of 10 of 10 within
6,000 ticks), and **FAIL** on both `p50` budgets (3.44× at 200 agents, 4.02× at
500 against allowances of 2.0× and 2.5×). The closing statement at
`calibration-record.md:756-760`:

> It does not meet the termination bar, and no tuning of the values this
> workstream owns meets it, because the interrupt's addressable population is
> the 0.3% of agent-ticks inside the attack lifecycle and the standoff lives in
> the other 99.7%.

The record explicitly declines to name a cause among the remaining candidates —
"the refuse conditions, the regroup cycle, the cohesion duty window, the
approach-sidestep rules — and declines to choose between them, because nothing
measured here distinguishes them" (`calibration-record.md:765-768`).

### 4.4 What this means for a ranged design

State it plainly, because it is the single most consequential finding in this
document.

The warriors in the V6/V7 branch **already refuse to close at melee reach**.
They spend 349 of every 350 agent-ticks in `Refuse` or `Regroup`, standing at
distance, and the battle runs to its tick cap with half the roster alive. Not one
of them has a *reason* to stand off — every one of them is a melee fighter whose
`PreferredDistanceBasisPoints` sits between 110% and 115% of its own weapon reach
(`src/Hukbo.Core/Movement/Profiles/ItakMovementProfile.cs:31`,
`src/Hukbo.Core/Movement/Profiles/KampilanMovementProfile.cs:29`). They are
supposed to be pressing in. The standoff is an emergent failure of the movement
layer, not a stated preference of any agent.

A ranged unit is a warrior with an *explicit, correct reason* to stop at forty
world units and stay there. Adding one to a system whose measured failure mode is
already "warriors will not close" means:

1. **The failure becomes indistinguishable from the feature.** Today a
   `Refuse`-locked front line is visibly a bug. A ranged rank standing off at
   range is the same picture on screen and in the metrics, so the diagnostic
   signal that identified this bug is destroyed by the feature.
2. **The termination bar gets harder, not easier.** V4 terminates in 1,279 to
   4,405 ticks because bodies meet and damage flows
   (`calibration-record.md:687-698`). Any rule that legitimately holds a
   fraction of the roster at range removes that pressure. The existing bar is
   "every one of ten cells decisive within 6,000 ticks"
   (`calibration-record.md:719`), and V6 already fails it 0 of 10.
3. **Attack resolution is centre-to-centre against `AttackRangeRaw` only.**
   `IsWithinAttackRange` at
   `src/Hukbo.Core/Simulation/BattleSimulation.cs:4132-4139` is the single
   approved reach test and it takes no account of intervening bodies. A ranged
   attack expressed as a longer `AttackRangeRaw` would shoot straight through
   the front rank, which is a separate design problem this document does not
   own but which the movement layer cannot solve for it.
4. **`Scenario.Validate` will reject the naive expression of it.** The
   validation at `src/Hukbo.Core/Simulation/Scenario.cs:306-310` requires
   `PerceptionRangeRaw >= AttackRangeRaw`. That one passes at the default
   perception of 2,048 world units. But nothing today expresses a *minimum*
   engagement distance at all, so "stop at 40" has no field to live in.

### 4.5 Surface area: every file:line a standoff fix would plausibly touch

Listed neutrally, with no opinion on what should change. This is the handoff to
the root-cause investigation, not a proposal.

**The phase ladder that produces `Refuse` and `Regroup`**

- `src/Hukbo.Core/Movement/WeaponMovementRules.cs:565-668` —
  `ResolveProvisionalFootwork`, the ten-step first-match ladder. Steps 4, 5 and
  6 (lines 625-643) are the three that produce `Disengage`; step 7 (lines
  646-649) is the one that produces `Regroup`.
- `src/Hukbo.Core/Movement/WeaponMovementRules.cs:694-709` — `FinalizeFootwork`,
  the only place `FootworkPhase.Refuse` is ever written (line 705).
- `src/Hukbo.Core/Movement/WeaponMovementRules.cs:91` —
  `ResolveTacticalPosture`, which produces the `Regroup`, `Withdraw` and `Yield`
  postures steps 6 and 7 read.
- `src/Hukbo.Core/Movement/FootworkPhase.cs:42-59` — the `Refuse` and
  `Disengage` enum members and their pinned numeric values.

**Lane clearance, which is what turns a phase into `Refuse`**

- `src/Hukbo.Core/Simulation/BattleSimulation.cs:1998-2080` —
  `TryProposeEquipmentRoute`, the loop that walks the candidate table and
  returns `false` when every candidate fails.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2065` — the
  `if (!IsLaneClearOfAllies(...))` rejection.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2428` — `IsLaneClearOfAllies`
  itself.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:1985-1996` — the single
  `FinalizeFootwork` call site and the one authoritative phase write.
- `src/Hukbo.Core/Movement/MovementRouteRules.cs:262-272` —
  `ClearanceRadiusRaw`, which materialises the clearance the lane scan tests
  against.
- `src/Hukbo.Core/Movement/LoadoutMovementProfile.cs:243` —
  `AllyClearanceBodyDiametersBasisPoints`, the per-row input to that radius.

**The route candidate table**

- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2087-2340` —
  `BuildEquipmentRouteCandidates`, one arm per phase.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2101-2162` — the shared
  `Approach`/`Engage` arm, including the "preferred distance is not a stop line"
  comment at lines 2116-2118.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2254-2340` — the
  `Disengage`/`Regroup` arm and its ally-then-leader-then-escape ordering.

**The friendly-clearance conflict pass**

- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2718` —
  `ResolveFriendlyClearanceConflicts`.
- `src/Hukbo.Core/Movement/MovementRouteRules.cs:369` —
  `AcceptFriendlyClearanceConflicts`, which rejects a proposal outright.
- `src/Hukbo.Core/Movement/MovementRouteRules.cs:338-349` —
  `ConflictPhaseSafetyRank`, the ordering that decides who is rejected.

**The cohesion duty cycle and the contingent state machine**

- `src/Hukbo.Core/Movement/MovementRules.cs:49-58` — `IsCohesionWindowOpen`,
  the duty-cycle test.
- `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:179-180` — V4's
  `cohesionCycleTicks: 240` / `cohesionDutyTicks: 180`; the same pair is
  restated on every later preset.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:1221-1447` —
  `ResolveContingentStates`, the six-priority transition table.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:2907-2996` —
  `TryResolveContingentCohesionAimPoint`, the six movement gates.

**The approach-sidestep and stall-escape rules**

- `src/Hukbo.Core/Simulation/ApproachSidestep.cs` (whole file, 116 lines).
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:3074-3113` —
  `BuildSidesteppingPursuitProposal`.
- `src/Hukbo.Core/Simulation/CollisionScratch.cs:162-182` — `RecordBlocked` and
  the stall-generation increment.
- `src/Hukbo.Core/Simulation/FormationRules.cs:143` —
  `StallEscapeStreakTicks = 192`.

**The stopping distance itself**

- `src/Hukbo.Core/Simulation/BattleSimulation.cs:4039-4054` — the
  `stopShortRaw: 2 * BodyRadiusRaw` decision.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs:4077-4125` — the shared step
  arithmetic and the arrival taper.
- `src/Hukbo.Core/Movement/MovementRules.cs:504-518` —
  `ComputeArrivalStepRaw`.

**Anything touched here moves both hashes.** `SIMULATION-GAME-STANDARDS.md:670-674`
records that changing where agents stand moves the state hash and the event hash
for every seed, and `CLAUDE.md` section 5 requires a new preset version plus new
golden expectations rather than an edit to a shipped preset.

## 5. Formation blocking at 500 agents

### 5.1 The parked baseline

The document titled "Formation blocking at 500 agents — backlog entry and
measured baseline" was archived on 2026-08-15 and is reference only. Before it
moved, its own status line read: "Backlog. This document authorizes no
implementation. It records a measured baseline." It is also the one entry
under "From the second-round lag report (2026-07-30)" in
`docs/plans/TODO.md:32-40`. The figures below were transcribed from that
document before it moved.

Both runs are `./scripts/benchmark.ps1 -Agents 500 -Ticks 2000` in `Release`
under combat preset V4, and both reported `deterministic: true`. The figures
requested:

| Figure | Round 1 (seed 1) | Round 2 (seed 11400714819323198486) |
| --- | --- | --- |
| `blockedAgentTicks` | 19,488 | **33,330** |
| `attackCapableAgentTicks` | 28,588 | 27,882 |
| `longestBlockedStreakTicks` | 178 | 168 |
| `measuredTicks` | 2,000 (undecided at cap) | 1,980 |
| `outcome` | `Draw`, 5 against 4 survivors | `Faction0Victory`, 20 against 0 |
| `maximumPenetrationRaw` | 0 | 0 |
| `contactPairs` | 15,406 | 14,511 |
| `candidatePairs` | 421,825 | 595,109 |

Every figure in the table above comes from section 2's measurement table in
the archived baseline document.

The three derived readings the document itself computes:

- Blocked agent-ticks per tick: 9.7 in round 1, 16.8 in round 2.
- **Blocked against attack-capable: 0.68 in round 1, 1.20 in round 2.** The
  document's own gloss: "In the second round the army spent more agent-ticks
  blocked than it spent able to attack, which is the clearest single statement of
  the problem."
- Longest blocked streak in seconds: 178 ticks is 8.9 seconds at a tick rate of
  20; 168 ticks is 8.4 seconds. "One warrior, stationary, for most of ten
  seconds, in plain view."

Two caveats the archived document states about its own numbers: the two seeds are two
samples and not a distribution, and **no cause is identified** — "whether the
blocking comes from contingent shape, from approach geometry, from the rank-led
leadership change, or from the preset's speed and radius values is exactly the
open question".

### 5.2 What a ranged unit in the rear rank would experience

There is no rear rank to be in. Section 2 establishes there is no rank concept
in code, so the question resolves to: what happens to an agent that would prefer
to stand behind its allies?

**It would be blocked, and blocked is the resolver's normal answer for a rear
agent.** `SIMULATION-GAME-STANDARDS.md:695-697` describes exactly this: "allies
still queue behind their own front line: a rear agent trying to advance into
space its own front rank already occupies is refused, holds position, and reports
`Blocked`." The 33,330 blocked agent-ticks above are overwhelmingly that
queueing.

Concretely, for a shooter that wanted to hold a firing position behind the melee
line:

1. **Holding position requires proposing nothing.** The only two ways an agent
   ends a tick where it started are proposing no movement — which V4 does only
   for `Idle`, `Attacking`, and arrived-`Regrouping` agents
   (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1523-1588`) — or being
   refused by the resolver. A ranged unit that keeps its V4 `Moving` intent will
   keep proposing a step toward its target's body every tick, forever.
2. **A blocked agent is not removed from combat, and this is deliberate.**
   `SIMULATION-GAME-STANDARDS.md:560-562` says no anti-stall rule was added
   "because being blocked does not remove an agent from combat: contact happens
   at eight world units while attack reach is twelve, so a blocked agent is still
   attacking". That reasoning holds for a melee weapon whose reach exceeds the
   body diameter by 3.5 world units. It does **not** transfer to a shooter whose
   value depends on standing at a distance nothing in the movement layer will
   grant it.
3. **The stall escape will fire and push it sideways.** After 192 consecutive
   blocked ticks (`src/Hukbo.Core/Simulation/FormationRules.cs:143`,
   `StallEscapeStreakTicks`) `CollisionScratch.RecordBlocked`
   (`src/Hukbo.Core/Simulation/CollisionScratch.cs:176-180`) increments a
   monotonic stall generation, and `GatherMovementProposals` diverts that agent
   to `BuildSidesteppingPursuitProposal`
   (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1577`). The sidestep is an
   oblique offset drawn to get *around* the blockage and continue closing — see
   `src/Hukbo.Core/Simulation/ApproachSidestep.cs` — not a decision to stay put.
   Left alone, a ranged unit would eventually work its way to the front.
4. **The front is thin and the crush is deep.** From the same baseline table,
   `maximumFrontWidthRaw` is 639,828 raw and `maximumFrontDepthRaw` is 79,586
   raw in round 1, from the same archived baseline table — a front
   roughly eight times wider than it is deep. At a body diameter of 8,704 raw
   that depth is about nine bodies. A shooting rank would be somewhere inside
   those nine, with nothing holding it there.

The honest summary: **the simulation has no way to express "stay back", and its
one existing anti-stall rule actively works against an agent that wants to.**

## 6. Spawn and initial deployment

### 6.1 How the two factions are placed

`BattleSimulation.Create` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:414`)
places both factions from a single plan.

1. One `SplitMix64` is seeded from the scenario seed
   (`BattleSimulation.cs:421`).
2. `FormationPlanner.PlanFactionDeployment` is called **once**
   (`BattleSimulation.cs:428`) and plans the whole army inside the **left half
   of the map** — `ResolveRegion` sets `maxX = (mapWidthRaw / 2) - radiusRaw`
   at `src/Hukbo.Core/Simulation/FormationPlanner.cs:142`.
3. Faction 0 takes that plan verbatim (`BattleSimulation.cs:474-488`).
4. Faction 1 takes the **mirror across the vertical centre line**:
   `rightX = mapWidthRaw - leftXRaw`, `rightY = leftYRaw`
   (`BattleSimulation.cs:493-494`).
5. `ResolveSpawnPlacement` (`BattleSimulation.cs:516`, defined at
   `BattleSimulation.cs:737`) is a repair pass that relocates any overlapping
   body by scanning compass rings, without consulting the random stream.

The mirror is deliberate and is documented at `BattleSimulation.cs:424-427`:
"One deployment is planned and mirrored across the vertical centre line, so the
two armies open in exactly the same shape... any positional difference at tick 0
would be seed noise that the battle then amplifies."

Within one faction, the shape is: contingents get horizontal lanes of the region
(`anchorY = region.MinY + laneSpan * contingent + laneSpan / 2`,
`FormationPlanner.cs:310`), and each contingent is centred in depth by
`ResolveAnchorX` (`FormationPlanner.cs:330`), which puts even-numbered
contingents forward and odd-numbered ones back
(`FormationPlanner.cs:339-340`) so "the front edge of an army is ragged rather
than a single straight line" (`FormationPlanner.cs:317-322`).

**Note what that means: contingent depth is decided by the parity of the
contingent index, and by nothing else.** Not by weapon, not by rank, not by role.

### 6.2 Can roster composition vary by unit type today?

**Yes for weapon *proportions*, no for weapon *placement*.**

`Scenario.RosterCounts` (`src/Hukbo.Core/Simulation/Scenario.cs:111-112`) is an
`ImmutableArray<int>` with one entry per roster index, applied identically to
both factions. It is validated at `Scenario.cs:275-303`: its length must equal
the combat preset's roster count and its entries must sum to exactly
`AgentsPerFaction`. Empty (the default) falls back to the round-robin
`CombatRuleset.ResolveLoadout` (`src/Hukbo.Core/Combat/CombatRuleset.cs:504-516`),
which is `(entityId - 1) % rosterCount`.

`RosterCountExpansion.Expand`
(`src/Hukbo.Core/Combat/RosterCountExpansion.cs:20`) turns those counts into a
per-warrior array of roster indices "in declared roster-index order" — that is,
**roster index 0 occupies faction-local indices 0..n-1 contiguously**, then
index 1, and so on.

So an author can already say "40 Kampilan, 60 Wasay, 50 Kalis, 50 Itak" for a
200-agent battle. The shipped V4 roster has four entries, all solo and
shieldless, at `src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:217-223`:
Kampilan/Datu, Wasay/Maharlika, Kalis/Timawa, Itak/AlipingNamamahay.

What an author **cannot** do is control where those warriors stand. The
round-robin contingent deal at
`src/Hukbo.Core/Simulation/FormationPlanner.cs:104-107` takes the contiguous
weapon runs `RosterCounts` produces and spreads them evenly across all
contingents, on purpose, for the reason quoted in section 2.4 above. There is no
scenario field, no preset field, and no code path that maps a roster entry to a
deployment depth, a lane, or a contingent.

### 6.3 Where a "ranged warriors deploy behind the line" rule would have to live

There are exactly three places it could go, and each has a different cost.

**Option A — inside `FormationPlanner`.** The planner is the only code that
decides a starting coordinate. A depth rule would attach to `ResolveAnchorX`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:330-346`), which is the single
function that decides how far forward a group deploys, or to `PlaceMember`
(`FormationPlanner.cs:288-315`), which decides an individual offset. The obstacle
is that `PlanFactionDeployment`'s signature is
`(Scenario scenario, ref SplitMix64 random)`
(`FormationPlanner.cs:78-81`) — **it never sees the roster or any warrior's
loadout.** Loadouts are resolved later, in the spawn loops at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:439-442` and `:477`. The planner
would need a new input, and its returned tuple
`(int XRaw, int YRaw, int ContingentId)` would probably need a fourth member.
`the contingent shape design:33-35` calls this surface "the
most heavily tested surface in the repository", quoting its own parent design,
and gives that as the reason it designed the change rather than making it.

**Option B — as a post-plan permutation, the way V6 already does it.**
`EquipmentDeploymentAssignment.AssignForFaction`
(`src/Hukbo.Core/Movement/EquipmentDeploymentAssignment.cs:56`) is the existing
precedent: it takes the planner's canonical deployment plus the resolved
loadouts and returns a permutation, drawing nothing and changing no coordinate.
Its call site is the `if (movement.UsesEquipmentRelativeFootwork)` block at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:455-472`. A depth-aware ranking
would be the same shape — sort slots by X toward the enemy rather than by
isolation, and warriors by a role key rather than by ally clearance. Two
constraints bind it: it permutes only **within one contingent**
(`EquipmentDeploymentAssignment.cs:106-114`), so it cannot move a warrior from
a forward contingent to a rear one; and it must preserve the faction mirror,
which it does by ranking the canonical pre-reflection coordinates
(`EquipmentDeploymentAssignment.cs:16-22`).

**Option C — as a runtime movement rule rather than a deployment rule.** Nothing
today keeps a warrior where it spawned; a rear deployment erodes within a few
hundred ticks as everyone walks toward the nearest enemy. A durable "shooters
stay back" behaviour would have to live in the movement layer, which is
section 8's list.

**A fourth non-option worth stating.** Neither `Hukbo.Core` nor any preset has a
notion of a warrior's *role*. `CombatLoadout` carries weapon, armor, shield, and
rank (`src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:219-222`), and
`RankId` is explicitly social standing rather than a military office —
`docs/research/ARMY-COMPOSITION.md:613-620` forbids inventing a graded military
hierarchy on the same grounds that excluded the panabas. A ranged role would have
to be derived from the weapon, not from a new rank.

### 6.4 One historical constraint on any deployment rule

`docs/research/ARMY-COMPOSITION.md:520-538` lists what the sources do **not**
establish, and two entries bear directly on a skirmish-screen concept:

- "Any unit below the chief's personal following, or any name for one."
- "A reserve, a rearguard, or a designated line of retreat."

A "skirmish screen ahead of the melee line, a shooting rank behind it" is
therefore a game-design invention, and under `CLAUDE.md` section 7 it has to be
labelled as one in code comments and in any player-facing text — the same
treatment `SIMULATION-GAME-STANDARDS.md:411-415` already applies to the whole
collision policy and `SIMULATION-GAME-STANDARDS.md:725-730` applies to the
last-stand rally.

## 7. The uniform grid

### 7.1 There are two grids, not one

This is easy to miss and matters for any query-cost estimate.

| Grid | Constructed at | Cell size | Purpose |
| --- | --- | --- | --- |
| The **metrics** grid, `CollisionScratch.Grid` | `src/Hukbo.Core/Simulation/CollisionScratch.cs:92` | `2 * ContactBandRadiusRaw` | rebuilt once per tick in `MeasureCollision`, produces the contact-pair list |
| The **resolver's** grid | `src/Hukbo.Core/Simulation/CollisionResolver.cs` (owned internally) | its own | indexes committed positions incrementally as the resolver commits them |

`CollisionScratch.cs:103-107` states the split explicitly: "Broad-phase index
over committed positions, used for metrics only. The resolver owns a separate
grid because it indexes positions incrementally as it commits them."

A third, throwaway grid is built inside `ResolveSpawnPlacement` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:747`.

### 7.2 Cell size

The metrics grid's cell size is derived, not configured:

```csharp
ContactBandRadiusRaw = checked(
    scenario.BodyRadiusRaw + (scenario.MovementSpeedRaw / 2));   // CollisionScratch.cs:90-91
Grid = new CollisionUniformGrid(checked(2 * ContactBandRadiusRaw)); // CollisionScratch.cs:92
```

At the shipped defaults — `BodyRadiusRaw = 4352`
(`src/Hukbo.Core/Simulation/CollisionRules.cs:72`) and
`MovementSpeedRaw = 3072` (`src/Hukbo.Core/Simulation/Scenario.cs:36`) — that is
`ContactBandRadiusRaw = 5888` and a **cell edge of 11,776 raw units, or 11.5
world units**. This matches the standards document's arithmetic at
`SIMULATION-GAME-STANDARDS.md:620-624`.

The cell size is bounded from below by a hard invariant:
`ValidateBodyRadius` (`src/Hukbo.Core/Simulation/CollisionUniformGrid.cs:633-640`)
throws when `2 * bodyRadiusRaw > CellSizeRaw`. That guard is what makes the
three-by-three neighbourhood scan sufficient, as
`CollisionUniformGrid.cs:52-58` explains.

### 7.3 Rebuild cadence

Once per tick, unconditionally, in `MeasureCollision`:
`_collision.Grid.Rebuild(_collision.Bodies, _collision.ContactBandRadiusRaw)` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:3507-3509`. `Rebuild`
(`CollisionUniformGrid.cs:153`) clears, inserts every body, and regenerates the
whole pair list. Dead bodies are skipped on insert
(`CollisionUniformGrid.cs:37-39`). All storage is reused, so a warm tick
allocates nothing (`CollisionUniformGrid.cs:40-43`).

### 7.4 The queries it serves

Exactly four, and all of them are *radius* queries with no directional
component:

| Query | Line | Semantics |
| --- | --- | --- |
| `Pairs` / `PairsList` | `CollisionUniformGrid.cs:126`, `:135` | every unordered contact pair after `Rebuild` |
| `AnyContact` | `CollisionUniformGrid.cs:314` | would a body at (x, y) touch any indexed body? inclusive |
| `AnyOverlap` / `AnyOverlapUnchecked` | `CollisionUniformGrid.cs:379`, `:402` | would it strictly penetrate? |
| `AnyCoincident` / `AnyCoincidentUnchecked` | `CollisionUniformGrid.cs:452`, `:463` | exact centre collision |

Every one of them is hard-wired to the three-by-three neighbourhood
`NeighbourOffsets` (`CollisionUniformGrid.cs:60-71`) and to the body radius. The
type has no method that takes a radius other than a body radius, no method that
takes a facing or an arc, and no method that returns a list of neighbours.

### 7.5 Can it answer "who is within 40 units in this facing arc"?

**No, not as it stands, and not cheaply by extension.** Three independent
blockers:

1. **The neighbourhood is fixed at three-by-three.** Every query loops
   `foreach (var offset in NeighbourOffsets)` — `CollisionUniformGrid.cs:326`,
   `:411`, and the pair generator at `CollisionUniformGrid.cs:561`. At
   `FixedPoint.Scale = 1_024` (`src/Hukbo.Core/Mathematics/FixedPoint.cs:8`),
   forty world units is 40,960 raw, which is 3.48 cells at the 11,776-raw cell
   edge. Covering it safely needs a cell radius of 4, that is a nine-by-nine
   neighbourhood — **81 cells against 9, a ninefold increase in cells visited per
   query** before any body is examined.
2. **There is no facing- or arc-aware predicate anywhere.** `Facing16`
   (`src/Hukbo.Core/Movement/Facing16.cs`) and `FacingRules`
   (`src/Hukbo.Core/Movement/FacingRules.cs`) exist, but they live in the
   `Movement` namespace and the grid never sees them. Filtering an arc would
   have to happen in the caller, after the radius query has already returned
   everything.
3. **The grid returns no neighbour list.** Three of the four queries return
   `bool`. Only `Pairs` returns a collection, and it is the global contact-pair
   list, not a per-agent neighbourhood. A "who is near me" query is a new API,
   not a parameter change.

### 7.6 Cost estimate at 500 agents

Grounded in two measured sources rather than invented.

**The measured facts.** `docs/research/TICK-STAGE-PROFILE.md:111-119` gives the
per-stage inclusive share of `AdvanceOneTick`, measured under the behaviour
later frozen as `IndependentPursuitV1`:

| Stage | 200 agents | 1000 agents | 2000 agents |
| --- | --- | --- | --- |
| `SelectTargetsAndIntents` | 5.04 % | 15.88 % | 16.67 % |
| `ResolveCollisions` | 63.11 % | 70.11 % | 74.77 % |
| `MeasureCollision` | 18.28 % | 9.79 % | 6.53 % |

There is no 500-agent column; 500 sits between the first two. The flat exclusive
profile at 200 agents (`TICK-STAGE-PROFILE.md:136-145`) puts
`CollisionUniformGrid.GeneratePairs` at 1.39 % and at 2000 agents
(`TICK-STAGE-PROFILE.md:158-163`) at 3.17 %. **The grid rebuild itself is
cheap; the resolver's per-candidate `IsFree` loop is what costs — 16.49 % at 200
agents and 50.62 % at 2000.**

The blocking baseline gives the pair volume directly: at 500 agents over 2,000
ticks, `candidatePairs` was 421,825 in round 1 and 595,109 in round 2, from
the same archived baseline document — roughly 211 to 298
candidate pairs per tick. Whole-run simulation cost at that size was 813 ms for
2,000 ticks, p50 0.118 ms per tick, also from that document.

**The estimate.** A per-agent forty-unit neighbourhood query at 500 agents:

- Cells visited: 81 per query instead of 9, so 500 × 81 = 40,500 cell visits per
  tick against the 4,500 a three-by-three sweep would cost.
- Bodies examined: 500 agents in a 1,280 × 720 map is one body per 1,843 square
  world units on average, so a 40-unit disc (about 5,027 square world units)
  holds under three bodies at uniform density. The measured front is far denser —
  15,406 contact pairs over 2,000 ticks, from the same archived baseline
  document, shows bodies packed
  at contact along the line. A 40-unit disc has room for roughly 88 bodies of
  4.25-unit radius at perfect packing, so an agent inside the crush would examine
  some tens of neighbours rather than three. At 500 agents that is on the order
  of 10,000 to 30,000 body examinations per tick.
- Against the measured p50 of 0.118 ms per tick at 500 agents, from the same
  archived baseline document, and given that
  `GeneratePairs` — which today walks a comparable number of body pairs at one
  ninth the cell count — costs 1.39 % of tick time at 200 agents and 3.17 % at
  2,000 (`docs/research/TICK-STAGE-PROFILE.md:145`, `:161`), **a per-agent
  40-unit query is plausibly of the same order as the entire current grid
  rebuild, or several times larger.** That is a meaningful but not obviously
  fatal cost on its own. This is an estimate derived from the measured figures
  above, not a measurement; nobody has run it.

**The caveat that matters more than the number.** `ResolveCollisions` is
already 63 % to 75 % of tick time, and V6's `p50` is already 3.44× V4's at 200
agents and 4.02× at 500
(the movement V7 calibration record, archived).
The V7 record names `ResolveCollisions` as "the flagged suspect" and is explicit
that flagging it is not authorization to touch it
(`calibration-record.md:743-746`). A ranged design that adds a per-agent
neighbourhood query is adding cost to a budget that two shipped presets have
already overrun.

**One cheaper alternative already exists in the code.** `SelectTargetsAndIntents`
is already an O(n²) scan over every agent every tick
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1013`), and V6 already piggybacks
its entire local-context derivation on that single existing pass rather than
adding a second query — the hook comment at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1069-1075` says so: the
already-computed squared distance is reused and no new query is made. Whatever a
ranged unit needs to know about its surroundings, that pass is where the
repository's own precedent puts it.

## 8. What does not exist that a ranged formation needs

Each row names the missing capability, the nearest existing thing to extend, and
its file. Ordered roughly by how hard the gap is to close.

### 8.1 Movement and stance

**1. A stop-at-range rule.** Nothing in the simulation stops an advancing agent
at a chosen distance. Nearest existing thing: the `stopShortRaw` parameter of
`BuildMovementProposal`, `src/Hukbo.Core/Simulation/BattleSimulation.cs:4077-4089`,
which today takes exactly two values — `2 * BodyRadiusRaw` for closing on an
enemy (`BattleSimulation.cs:4054`) and `0` for a point destination
(`BattleSimulation.cs:4075`). It is a parameter, so a third value is
arithmetically trivial; the difficulty is everything downstream in this list.

**2. A hold-at-range behaviour.** An agent that has reached its firing distance
has to stop proposing movement, and nothing expresses that. Nearest existing
thing: the arrived-guard pattern at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1547-1554`, which skips a proposal
when a cohering agent is already within body-contact distance of its aim point;
and `BuildRegroupingProposal`'s null return
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:3115`,
`:3198-3201`). Both compare against the body-contact distance and nothing else.

**3. A fall-back-when-closed-on rule.** Nearest existing thing:
`FootworkPhase.Disengage` (`src/Hukbo.Core/Movement/FootworkPhase.cs:53`), which
as section 3.3 establishes proposes routes toward the nearest ally and then the
leader (`src/Hukbo.Core/Simulation/BattleSimulation.cs:2259-2304`) and only
retreats when no anchor exists. The one genuinely rearward route in the codebase
is the `Recover` arm at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:2199-2251`, which steps opposite
the agent's own facing.

**4. An agent intent that means "holding".** `AgentIntent`
(`src/Hukbo.Core/Simulation/AgentIntent.cs:12-24`) has five members: `Idle`,
`Moving`, `Attacking`, `Dead`, `Regrouping`. It is append-only and enters the
state hash, so a sixth member is a new preset version. Without one, a spectator
cannot see the difference between "shooting from a chosen distance" and
"stuck".

**5. A minimum engagement distance anywhere in the data model.** `Scenario`
carries `AttackRangeRaw`, `PerceptionRangeRaw`, `MovementSpeedRaw`,
`BodyRadiusRaw` (`src/Hukbo.Core/Simulation/Scenario.cs:32-40`) and validates
`PerceptionRangeRaw >= AttackRangeRaw` (`Scenario.cs:306-310`). `WeaponProfile`
carries damage, reach, and cooldown
(`src/Hukbo.Core/Combat/WeaponProfile.cs`, consumed at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:886-888`). No type in
`Hukbo.Core` has a field for a distance an agent wants to *keep*.

### 8.2 Formation and deployment

**6. Any positional formation concept at all.** Section 2 covers this. Nearest
existing thing: the cohesion aim point at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:2907-2996`, which is a trailing
jitter blob rather than a shape. Note that
`SIMULATION-GAME-STANDARDS.md:417-421` and
`src/Hukbo.Core/Simulation/ContingentState.cs:8-10` both explicitly forbid rank
and slot assignment, so this is a contract change, not just a code addition.

**7. Role-aware contingent membership.** Nearest existing thing:
`FormationPlanner.PlanFactionDeployment`'s round-robin deal at
`src/Hukbo.Core/Simulation/FormationPlanner.cs:104-107`, which is deliberately
constructed to prevent weapon-homogeneous contingents. Also
`ResolveContingentSizes` (`FormationPlanner.cs:162`), whose only argument is
`int warriorCount`.

**8. Depth-aware deployment.** Nearest existing thing:
`FormationPlanner.ResolveAnchorX` (`src/Hukbo.Core/Simulation/FormationPlanner.cs:330`),
which sets depth from contingent index parity alone
(`FormationPlanner.cs:339-340`); and
`EquipmentDeploymentAssignment.AssignForFaction`
(`src/Hukbo.Core/Movement/EquipmentDeploymentAssignment.cs:56`), which is the
correct precedent for an equipment-aware permutation but ranks by elbow room and
cannot cross a contingent boundary (`EquipmentDeploymentAssignment.cs:106-114`).

**9. A weapon "role" abstraction.** Nearest existing things:
`LoadoutCompositionCounts` (`src/Hukbo.Core/Movement/LoadoutCompositionCounts.cs:19`)
with its three derived role predicates `HasLongClearanceRole`,
`HasMobileBladeRole`, `HasShieldSupportRole` (lines 31, 37, 43) — the only
role-shaped concept in the repository, hard-coded to the six melee loadouts; and
`RankId`, which `docs/research/ARMY-COMPOSITION.md:613-620` explicitly forbids
using as a military office.

### 8.3 Perception and the query layer

**10. A directional or arc-limited neighbourhood query.** Section 7.5 covers
this. Nearest existing things: `CollisionUniformGrid`'s four radius-only,
boolean-or-global-pair-list queries
(`src/Hukbo.Core/Simulation/CollisionUniformGrid.cs:126`, `:314`, `:379`,
`:452`), and `Facing16` / `FacingRules`
(`src/Hukbo.Core/Movement/Facing16.cs`,
`src/Hukbo.Core/Movement/FacingRules.cs`), which the grid never sees.

**11. Any per-agent local awareness under the shipped default.**
`LocalMovementContext`
(`src/Hukbo.Core/Movement/LocalMovementContext.cs:53-61`) carries immediate and
support ally/enemy counts, both compositions, the nearest ally, and the second
threat — everything a "am I being closed on?" test would want. It is derived
only when `UsesEquipmentRelativeFootwork` is true
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:962`), so it is all zeroes under
V4. This is the single largest piece of existing machinery a ranged design could
reuse, and reaching it means reaching V6/V7 and its standoff.

**12. Line of sight, occlusion, or friendly-fire blocking.**
`IsWithinAttackRange` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:4132-4139`)
is the single approved reach test and it is a bare squared-distance comparison
with no notion of what stands between two agents.
`SIMULATION-GAME-STANDARDS.md:456-466` calls this out as the one shared reach
helper so that intent selection and attack gathering cannot disagree; any
occlusion rule would have to go through it.

### 8.4 Hard structural blockers

**13. The six-loadout ceiling.** This one is not a gap to fill but a wall to
demolish. Six independent places hard-code exactly six loadouts in one canonical
order (`KP`, `WA`, `KA`, `IT`, `KS`, `IS`):

| Site | File:line |
| --- | --- |
| `MovementRuleset.CanonicalLoadoutIndex` | `src/Hukbo.Core/Movement/MovementRuleset.cs:376-387` |
| `MovementRuleset.ValidateEquipmentRelativeFootworkCoupling` — "exactly the six canonical rows, each appearing once, in canonical order" | `src/Hukbo.Core/Movement/MovementRuleset.cs:389-396`, `:407` |
| `LoadoutMovementProfile.OpponentDistanceOffsetCount = 6` | `src/Hukbo.Core/Movement/LoadoutMovementProfile.cs:31`, enforced at `:72-80` |
| `MovementRouteRules.CanonicalOpponentIndex` | `src/Hukbo.Core/Movement/MovementRouteRules.cs:301-315` — throws for an unmapped triple |
| `LoadoutCompositionCounts` — six named fields | `src/Hukbo.Core/Movement/LoadoutCompositionCounts.cs:19-49` |
| `MovementRouteRules.OccupiedLoadoutBuckets` | `src/Hukbo.Core/Movement/MovementRouteRules.cs:323-330` |

Adding four ranged weapons to the equipment-relative branch means ten loadouts,
which means ten opponent-distance offset cells per row, which changes the
`MovementRuleset.ContentHash` fold (`src/Hukbo.Core/Movement/MovementRuleset.cs:667`,
`:679`) and therefore every frozen trajectory digest. Note the shipped **combat**
roster is only four entries
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:217-223`), all solo, so the
six movement rows already exceed what the default combat preset fields.

**14. Projectiles.** There is no projectile entity, no travel time, and no
in-flight state anywhere in `Hukbo.Core`. `CLAUDE.md` section 9 states the model:
"Introduce rigid-body physics" is forbidden and "distance checks and hitscan are
the model". A ranged attack has to be hitscan at a longer `AttackRangeRaw` unless
a design decision changes that.

**15. Ammunition.** `CLAUDE.md` section 9 lists "projectile ammunition" among
the things not to start "before the gate that authorizes them".

### 8.5 The two contract-level obligations any of this incurs

**Both hashes move.** `SIMULATION-GAME-STANDARDS.md:670-674` records that
changing where agents stand moves the state hash and the event hash for every
seed and requires a deliberate rebaseline recorded in the same commit.

**A new preset version, not an edit.** `CLAUDE.md` section 5 requires a new
preset version plus new golden expectations for any change to roster order,
weights, or enum values. `MovementPresetRegistry`'s own comments make the pattern
explicit — V4 "lands as a new preset rather than as an edit to
`PersistentContingentsV3Ruleset` because V3 has already shipped as a default"
(`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:163-169`).

## 9. Tests that constrain this area

`tests/Hukbo.Core.Tests` holds roughly 46,500 lines across the root and the
`Movement/` subfolder. What follows is not the whole surface — it is the tests a
ranged movement or formation change would actually have to satisfy or
deliberately rebaseline, grouped by what they pin.

### 9.1 The seven frozen trajectory digests — the hardest constraint

`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` pins one digest per
registered preset. Any behavioural change to a shipped preset breaks the matching
row, and a new preset must be added rather than an existing one edited.

| Test | Line | Constrains |
| --- | --- | --- |
| `IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest` | `:115` | V1 trajectory byte-for-byte |
| `PersistentContingentsV2_ReproducesTheFrozenTrajectoryDigest` | `:145` | V2 |
| `PersistentContingentsV3_ReproducesTheFrozenTrajectoryDigest` | `:166` | V3 |
| `PersistentContingentsV4_ReproducesTheFrozenTrajectoryDigest` | `:187` | **the shipped default** |
| `PersistentContingentsV5_ReproducesTheFrozenTrajectoryDigest` | `:208` | V5 |
| `EquipmentRelativeFootworkV6_ReproducesTheFrozenTrajectoryDigest` | `:231` | V6 |
| `EquipmentRelativeFootworkV7_ReproducesTheFrozenTrajectoryDigest` | `:263` | V7 |

### 9.2 Termination and no-stall guarantees

| Test | File:line | Constraint |
| --- | --- | --- |
| `NoBattleUnderPersistentContingentsStallsAtTheTickLimitAcrossSeedsOneThroughTwenty` | `tests/Hukbo.Core.Tests/PersistentContingentTests.cs:39` | every seed 1–20 under the default preset must reach a terminal outcome before `TickLimit` |
| `NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred` | `tests/Hukbo.Core.Tests/LastStandFormationTests.cs:774` | seeds 1–200; this is the regression lock `CollisionRules.cs:61-71` names for the follower-trailing deadlock |
| `AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty` | `tests/Hukbo.Core.Tests/LastStandFormationTests.cs:690` | longest blocked streak bound (125 ticks, per the record at `docs/development/testing.md:2390-2394`) |
| `APursuerBlockedByAComradeNoLongerHoldsTheBattleOpen` | `tests/Hukbo.Core.Tests/LastStandFormationTests.cs:858` | a blocked pursuer must not keep the battle alive |
| `BothFactionsInASixVersusSixLastStandReachATerminalOutcome` | `tests/Hukbo.Core.Tests/LastStandFormationTests.cs:518` | endgame termination |
| `TwoSameFactionContingentsWithOverlappingTrailingSquaresReachATerminalOutcome` | `tests/Hukbo.Core.Tests/ContingentDeadlockTests.cs:50` | cohesion-square deadlock |
| `IndependentSameFactionTrafficCrossingAGrantedBiasSquareReachesATerminalOutcome` | `tests/Hukbo.Core.Tests/ContingentDeadlockTests.cs:343` | cross-traffic deadlock |
| `AContingentLeaderPinnedInAMapCornerReachesATerminalOutcome` | `tests/Hukbo.Core.Tests/ContingentDeadlockTests.cs:596` | corner pin |
| `ACohesionSquareTooLargeForTheMapDegradesToIndependentPursuit` | `tests/Hukbo.Core.Tests/ContingentDeadlockTests.cs:726` | gate 5 degradation |

**These are the tests a stop-at-range rule is most likely to break.** Every one
of them asserts that agents eventually converge and kill each other; a rule that
legitimately holds warriors apart works directly against them.

### 9.3 The solid-disc invariants

| Test | File:line | Constraint |
| --- | --- | --- |
| `PostTickInvariant_NoTwoLivingAgentsEverStrictlyOverlap` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:64` | zero penetration after every tick |
| `MovementBudget_NoAgentStepsFurtherThanItsSpeedUnlessItWasSeparated` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:87` | collision may only reduce displacement |
| `PackedFront_OpposingBodiesInContactStayInsideReachAndDealDamage` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:218` | a packed front fights rather than deadlocking |
| `PackedFront_DenseLinesThatMarchIntoReachStillDealDamage` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:262` | same, for marching lines |
| `AttackEligibility_AttackingIntentWithAReadyCooldownAlwaysProducesAnAttack` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:140` | intent and attack gathering agree |
| `BoundaryAndCorner_NoCentreLeavesTheLegalBandDuringAWholeBattle` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:409` | map clamp holds |
| `SpectatorClarity_MovementResolutionReachesAgentViewAndExplainsEveryMove` | `tests/Hukbo.Core.Tests/CollisionRegressionTests.cs:509` | every move has a spectator-visible reason |

### 9.4 Deployment and the mirror

| Test | File:line | Constraint |
| --- | --- | --- |
| `BothFactionsDeployAsExactMirrorsAcrossTheVerticalCentreLine` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:33` | the mirror; any depth rule must preserve it |
| `NoTwoBodiesComeWithinContactBeforeTheFirstTick` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:60` | no spawn overlap |
| `EachFactionDeploysInsideItsOwnHalfOfTheMap` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:75` | half-map containment |
| `ADefaultArmyOpensAsFiveSeparatedContingentsOfEqualSize` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:131` | **pins the current `ResolveContingentSizes` output exactly** — the contingent-shape design would have to change this |
| `ALargeArmyStopsAtEightContingents` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:154` | `MaximumContingents` cap |
| `MembershipDealsRoundRobinAcrossContingentsOnBothPlacementPaths` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:338` | **pins the weapon-blind round-robin deal** — the direct obstacle to a homogeneous ranged contingent |
| `ADifferentSeedMovesBodiesWithoutMovingTheContingentStructure` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:190` | structure is seed-independent |
| `ACrowdedPopulationFallsBackToADenseBlockWithoutOverlapping` | `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:294` | the dense-block fallback |
| `V4SpawnPositionsMatchThePlannedDeploymentIdentically` | `tests/Hukbo.Core.Tests/Movement/EquipmentFormationAssignmentTests.cs:456` | V4 never permutes |
| `ReassignmentStaysInsideEachContingent` | `tests/Hukbo.Core.Tests/Movement/EquipmentFormationAssignmentTests.cs:198` | the V6 permutation cannot cross a contingent |
| `V6SpawnsPermuteTheV5SlotsWithZeroAdditionalDraws` | `tests/Hukbo.Core.Tests/Movement/EquipmentFormationAssignmentTests.cs:352` | the permutation draws nothing |
| `SymmetricRosterCountsMirrorExactlyUnderV6` | `tests/Hukbo.Core.Tests/Movement/EquipmentFormationAssignmentTests.cs:423` | equipment-aware placement preserves the mirror |
| `ThrowsForAPresetWithoutEquipmentRelativeFootwork` | `tests/Hukbo.Core.Tests/Movement/EquipmentFormationAssignmentTests.cs:249` | the V6-only gate |

### 9.5 The approach step and the stall escape

| Test | File:line | Constraint |
| --- | --- | --- |
| `EverySampledStepIsAtLeastOneAtMostSpeedAndNeverExceedsTheUntaperedStep` | `tests/Hukbo.Core.Tests/ArrivalTaperTests.cs:53` | the arrival taper never halts and never overshoots |
| `TheStepIsExactlyUntaperedAtAndBeyondTheTaperBand` | `tests/Hukbo.Core.Tests/ArrivalTaperTests.cs:89` | taper band boundary |
| `GenerationZeroDisplacesNothing` | `tests/Hukbo.Core.Tests/ApproachSidestepTests.cs:30` | an unstalled agent takes the unchanged aim point |
| `TheOffsetIsPerpendicularToTheApproachWithinTruncation` | `tests/Hukbo.Core.Tests/ApproachSidestepTests.cs:91` | the sidestep is lateral, never rearward |
| `TheOffsetLengthStaysInsideTheProvisionalSpan` | `tests/Hukbo.Core.Tests/ApproachSidestepTests.cs:117` | bounded by `ApproachSidestepMinimum/MaximumMultiplier` |
| `AFollowerStandingInItsLeadersPathStepsAsideRatherThanThroughIt` | `tests/Hukbo.Core.Tests/LastStandFormationTests.cs:900` | the give-way rule |

### 9.6 The footwork phase ladder (V6/V7 only)

| Test | File:line | Constraint |
| --- | --- | --- |
| `ATargetAtOrInsidePreferredDistanceResolvesEngage` | `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs:288` | step 8 |
| `ATargetBeyondPreferredDistanceResolvesApproach` | `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs:296` | step 9 |
| `AWithdrawOrYieldPostureDisengagesUnconditionally` | `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs:263` | step 6 |
| `EntryEqualityEntersDisengagement` / `ReleaseEqualityLeavesDisengagement` | `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs:162`, `:173` | the hysteresis boundaries |
| `ABlockedLaneRefusesMovementSeekersAndRetainsTheRest` | `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs:366` | `FinalizeFootwork` → `Refuse` |
| `TheFootworkPhaseNumericValuesArePinned` | `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs:385` | enum values are hashed and append-only |
| `ApproachBecomesEngageInsideThePreferredDistance` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:109` | end-to-end phase transition |
| `EngageCrossesThePreferredBandAndAttacksTheSameTick` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:150` | **its own doc comment at `:141-142` names this "Contract H: Engage is not a stop line"** — two warriors open inside the preferred band, close 256 units on the first tick, and attack the same tick. This is the direct obstacle to a hold-at-range rule |
| `RecoverBacksAwayAtTheBackwardBand` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:492` | the one rearward movement |
| `AnApproachWithEveryLaneBlockedFinalisesRefuse` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:281` | lane-clearance refusal |
| `ABlockedDisengageRetainsItsPhaseAndHolds` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:308` | a safety phase survives a blocked lane |
| `ALegacyPresetRunsNoEquipmentStageAtAll` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:50` | **pins that V1–V5 never touch the equipment pipeline** |
| The five conflict-order tests, `DeadOutranksTheBodyContactAttackingHold` through `TheEquipmentRouteReplacesOrdinaryPursuit` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:552`, `:591`, `:623`, `:661`, `:694`, `:718` | the same-tick priority chain a new phase would have to slot into |
| `RepeatedVSixCollisionTicksHaveBoundedAllocations` | `tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:794` | the per-tick allocation ceiling |

### 9.7 The six-loadout coupling

| Test | File:line | Constraint |
| --- | --- | --- |
| `V6CarriesExactlySixProfilesInCanonicalOrder` | `tests/Hukbo.Core.Tests/Movement/MovementProfileRegistrationTests.cs:264` | **the six-row ceiling, asserted directly** |
| `CanonicalOpponentIndexMatchesTheCanonicalOrder` | `tests/Hukbo.Core.Tests/Movement/MovementRouteRulesTests.cs:295` | the `KP, WA, KA, IT, KS, IS` order |
| `CanonicalOpponentIndexIsRankIndependentAndThrowsForUnmapped` | `tests/Hukbo.Core.Tests/Movement/MovementRouteRulesTests.cs:312` | **an unmapped loadout throws** — a fifth weapon fails here first |
| `EffectivePreferredDistanceAppliesTheOpponentOffsetCell` | `tests/Hukbo.Core.Tests/Movement/MovementRouteRulesTests.cs:261` | the preferred-distance arithmetic |
| `LoadoutMovementProfileTests` (whole file, 570 lines) | `tests/Hukbo.Core.Tests/Movement/LoadoutMovementProfileTests.cs` | every construction bound, including the six-cell offset array |

### 9.8 Cohesion behaviour bars

| Test | File:line | Constraint |
| --- | --- | --- |
| `CohesionNeverOutlivesItsDutyCycleBudgetAcrossSeedsOneThroughTwenty` | `tests/Hukbo.Core.Tests/PersistentContingentTests.cs:96` | the 240/180 duty cycle is respected |
| `CohesionCoverageIsNotPracticallyInertAcrossSeedsOneThroughTwenty` | `tests/Hukbo.Core.Tests/PersistentContingentTests.cs:228` | cohesion must actually fire — the bar V3 failed and V4 was created to pass |
| `UnderTheNarrowedScanACloseContingentStopsDenyingItsNeighbours` | `tests/Hukbo.Core.Tests/PersistentContingentTests.cs:567` | V4's distinguishing rule |
| `ChainDenialArisesFromGenuinePairwiseOverlapNotFromPropagation` | `tests/Hukbo.Core.Tests/PersistentContingentTests.cs:440` | gate 6 semantics |
| `ContingentStateMachineTests` (whole file, 732 lines) | `tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs` | the six priority-ordered transitions |

### 9.9 The grid's oracle equivalence

`tests/Hukbo.Core.Tests/CollisionUniformGridTests.cs` (934 lines) is an
oracle-equivalence suite against `NaiveCollisionPairs`. Any change to the
neighbourhood, the cell size, or the query set has to satisfy it. The rows that
bind a widened query directly:

| Test | Line | Constraint |
| --- | --- | --- |
| `Rebuild_RejectsACellSmallerThanTheBodyDiameter` | `:49` | the cell-size floor that makes 3×3 sufficient |
| `Rebuild_AcceptsACellExactlyOneBodyDiameterWide` | `:58` | the exact boundary |
| `AnyContact_FindsContactFromEveryNeighbouringCell` | `:362` | all nine cells are scanned |
| `AnyOverlap_FindsAnOverlapFromEveryNeighbouringCell` | `:566` | same for the strict predicate |
| `Rebuild_ProducesTheIdenticalOrderedResultForEveryInputPermutation` | `:224` | determinism under permutation |
| `Rebuild_MatchesTheOracleForGeneratedWorldsAcrossFixedSeeds` | `:208` | the O(n²) equivalence the standards require at `SIMULATION-GAME-STANDARDS.md:600-602` |

### 9.10 Scenario validation

| Test | File:line | Constraint |
| --- | --- | --- |
| `ValidateAcceptsMovementSpeedEqualToTheBodyRadius` | `tests/Hukbo.Core.Tests/ScenarioTests.cs:131` | the anti-tunnelling bound, at equality |
| `ValidateRejectsMovementSpeedAboveTheBodyRadius` | `tests/Hukbo.Core.Tests/ScenarioTests.cs:144` | **a faster ranged unit cannot simply raise `MovementSpeedRaw`** |
| `ValidateRejectsABodyRadiusWhoseContingentTrailOverflowsAndAcceptsTheDefault` | `tests/Hukbo.Core.Tests/ScenarioTests.cs:551` | the trail-overflow guard |
| `ScenariosDifferingOnlyInBodyRadiusAreNotEqual` | `tests/Hukbo.Core.Tests/ScenarioTests.cs:601` | scenario equality is field-wise |

### 9.11 Determinism

`tests/Hukbo.Core.Tests/DeterminismTests.cs` (1,402 lines) and
`tests/Hukbo.Core.Tests/Movement/MovementStateHashTests.cs` (1,031 lines) pin the
state-hash and event-hash fold orders. `MovementStateHashTests` in particular
pins that V1–V5 fold `null` for the movement content hash and that only V6
folds the five footwork fields and only V7 the three pressure fields — the
gating logic visible at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:686-696`. Any new per-agent field
a ranged unit needs lands here.
