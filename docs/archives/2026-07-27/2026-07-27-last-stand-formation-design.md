# Last-Stand Formation — Design

Date: 2026-07-27
Status: Implemented, with two corrections recorded below
Layer: `Hukbo.Core` (authoritative simulation)

## Corrections made during implementation

Two claims in the first draft of this document were wrong, and both were caught
by tests rather than by review. They are recorded here rather than quietly
edited away, because each one is a trap that a future change could walk back
into.

**The packing bound was the wrong number.** The first draft set the maximum
threshold to the bias square's full capacity, sixteen. Capacity is the count
that fits under *perfect* packing, and these offsets are drawn at random, so
demanding capacity guarantees overlap. A sixteen-versus-sixteen battle
gridlocked completely: a forced draw at tick 10,000, both factions still at full
strength, not one casualty, and a longest blocked streak of 9,975 ticks. The
maximum threshold is now capacity divided by a fourfold area margin, and the
jitter multiplier was raised from four to six so that ceiling lands at nine and
still admits the default of six.

**The liveness argument was invalid.** The first draft claimed a last stand can
never stall because the rally agent is exempt and always closes on an enemy
under the unmodified rules. That reasoning ignored collision entirely. A rally
agent is exempt from the *formation*, not from *bodies*. Two separate deadlocks
followed, and both are now fixed by the trail and give-way rules described
below. The general lesson is that in a simulation with solid bodies, exempting
an agent from a behaviour does not exempt it from being physically blocked by
the agents that are still following that behaviour.

## Historical boundary

This document records a **game-design invention**. It is not a historical claim,
and nothing in it may be cited as a documented property of pre-colonial
Philippine warfare. No number below is a measurement.

`docs/research/battles/03-deep-past-formations-and-tactics.md` records that
nothing about formations is documented. Shield walls, ranks, files, wedges,
phalanxes, testudos, and skirmisher screens are listed there explicitly as
arrangements the game must never present as pre-contact Philippine fact. The
only material this feature can lean on at all is the "plausible inference" list
of small-unit cooperation, which includes following a local leader's advance or
withdrawal and regrouping around a boat, an access point, or a leader, together
with the transition table that admits "local cluster to retreat" and "retreat to
regroup" as plausible pressures. That same research then states plainly that
exact thresholds, timings, and shapes are design inventions.

This feature is therefore **compatible with the evidence, not derived from it**.
The evidence supports the idea that people fought near their companions and
rallied on a leader. It supports no threshold, no radius, no shape, and no name.
Every number below was chosen by the designer to make an endgame readable, and
every one of them is a tuning value that may be changed without consulting a
historical source.

Two rules follow and are binding on the implementation. First, no player-facing
string may name a cultural or a foreign formation. The only word a spectator
sees is `Regrouping`, a plain English descriptor, exactly as the weapon policy
shows `Great Blade` rather than `Kampilan`. Second, the research's own stated
preference for emergent local geometry over rigid templates is honoured: the
cluster's shape is produced by a fixed per-warrior positional bias meeting the
existing solid-disc collision resolver, not by a slot table, a grid, or a
template.

## The problem this fixes

A battle currently ends as a trickle rather than as a climax.

Once each faction is down to a handful of survivors,
`BattleSimulation.SelectTargetsAndIntents` gives every survivor its own nearest
living enemy. Because the default perception range is 2,048 world units against
a 1,280 by 720 map, every survivor can see every enemy on the field, so those
choices are effectively unconstrained. Each survivor then walks a straight line
at its own private target through `BuildMovementProposal` and fights a private
duel. Five survivors produce up to five unrelated fights scattered across the
map, arriving at different times. The camera auto-pan added earlier the same day
chases whichever one it can reach. A spectator sees the battle dissolve instead
of concentrate.

## The chosen mechanism

While a faction is in its last stand, one warrior leads and the rest close on
it. Nothing else changes.

### The trigger

A faction is in its last stand on a given tick when its living count is at or
below `Scenario.LastStandThresholdAgents`, and that value is greater than zero.
The trigger is evaluated per faction and independently for each faction, so one
side can be in its last stand while the other is still at full strength. A
threshold of zero disables the feature entirely, and zero is the property's
default value.

The trigger is **monotone and cannot flap**. Hit points are written in exactly
one place in the simulation, inside `BattleSimulation.GatherAndCommitAttacks`,
and that write is `Math.Max(0, target.HitPoints - damage)`. No code path raises
hit points and there is no respawn, so a faction's living count never increases
inside a battle. Once the trigger arms for a faction, it stays armed until the
battle ends. This removes an entire class of oscillation without needing a
hysteresis band, and it is the single most important property of the design.

### The rally agent

Each faction's rally agent is its living warrior with the lowest `EntityId`.
This is a total order over a finite set with no possible tie, because entity IDs
are unique. It requires no stored state, no cached identity, and no tie-break
rule beyond the ordering itself. It is recomputed by a single forward scan at
the top of intent selection every tick, and the comparison is written against
`EntityId` explicitly rather than relying on the incidental ordering of the
backing array, so a permuted array yields the same rally agent.

A rally agent is **a body standing on the ground**, which is why it was chosen
over the arithmetic mean of the faction's living positions. A mean is a point
nobody occupies, so every survivor would converge on the same empty spot, and
the solid-disc resolver would then block all of them in ascending entity ID.
That permanently seats the lowest ID in the middle and reads as a systematic
artefact rather than as a crowd. A mean also needs an integer division whose
truncation biases the point toward the low corner of the map. A leader has
neither problem: the cluster wraps around an occupied disc because the resolver
already forbids penetration, and no division is involved.

### The rally agent leads

The rally agent is exempt from the formation. It selects its nearest enemy and
advances on it under the existing, unmodified rules. This is what makes the
cluster go somewhere.

Exemption from the formation is **not** on its own a liveness guarantee, and the
first draft of this document wrongly claimed it was. The rally agent is still a
solid body among solid bodies, so its own followers can physically block it. Two
distinct deadlocks were observed before the rules below were added, and in both
of them the battle ran to the tick limit with no casualties at all.

In the first, a follower whose jitter offset happened to point along the leader's
direction of travel parked directly in front of the leader and held station
there. Because the offset is a personal constant, it held that station forever.
The leader was permanently `Blocked`; both factions did it at once; nobody ever
closed.

In the second, which appeared only after the trail rule below was added, a
follower that happened to start *in front* of its leader had to travel backwards
through the leader to reach its trail point. The leader blocked the follower
going backwards and the follower blocked the leader going forwards — a head-on
exchange neither could win.

Liveness comes from the trail rule and the give-way rule together, and it is
asserted directly rather than argued: `NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty`
runs twenty seeds at the maximum threshold and requires every one of them to
reach a terminal outcome before the tick limit.

### Followers trail behind the leader

A follower's aim point is not centred on the leader. It is centred on a point
`RallyTrailRadiusMultiplier` body radii **behind** the leader, opposite the
direction from the leader to its current target, and the jitter offset is then
added to that trailing point. The leader's forward arc therefore stays clear.

The trail distance has to clear the worst forward reach of the jitter. Jitter is
drawn independently per axis from `[-J, +J]`, so its projection onto any single
direction is at most `J * sqrt(2)`, which is about `8.49R` at the chosen
multiplier of six. A trail of `12R` leaves about `3.51R` of clearance, which is
comfortably beyond the `2R` contact distance at which two bodies touch. The
governing inequality is that `RallyTrailRadiusMultiplier` must always exceed
`RallyJitterRadiusMultiplier * sqrt(2) + 2`, and changing either multiplier
requires rechecking it. That inequality is recorded in `FormationRules`.

If the rally agent has no target, there is no direction to trail along, and the
aim point falls back to the leader's own position plus the jitter offset.

### Followers give way

A follower standing in its leader's forward corridor steps sideways out of the
corridor instead of trying to travel back through the leader.

The follower is in the corridor when it is in front of the leader and its
lateral distance from the leader's line of travel is under
`RallyCorridorHalfWidthMultiplier` body radii. In that case its aim point is a
pure lateral step to the side it is already on, far enough to clear the corridor
edge by a body radius. Its forward position is left alone, so a giving-way
follower moves sideways only and cannot re-enter the corridor by moving. When
the lateral distance is exactly zero the side is chosen deterministically, so
the outcome never depends on array or iteration order.

This is the one behaviour in this feature with any support in the research at
all: `docs/research/battles/03-deep-past-formations-and-tactics.md` lists
"avoiding blocking a companion's movement or weapon" among its plausible
inferences about small-unit cooperation. It remains a design invention in its
numbers and its geometry.

### Every other survivor regroups

A living warrior of a faction in its last stand that is not the rally agent, and
whose selected target is not already within body-contact distance, is given the
new intent `AgentIntent.Regrouping`. Its movement destination for the tick is
its **aim point**: the rally agent's tick-start position plus that warrior's own
fixed positional bias.

A regrouping warrior that has already arrived — meaning its squared distance to
its aim point is at or inside the contact distance — proposes no movement at
all. A settled cluster therefore stands still and stops emitting `Move` events,
instead of twitching by one raw unit every tick against the movement floor in
`BuildMovementProposal`.

### Attacking is untouched

Attack eligibility is decided entirely by cooldown and by range measured centre
to centre. Intent plays no part in it. A regrouping warrior that walks past an
enemy strikes it, and `GatherAndCommitAttacks` re-marks that warrior `Attacking`
in the same tick, exactly as the collision work already documented for `Moving`.

There is consequently no "break formation to attack" rule, no engagement radius,
no second threshold, and no boundary for a warrior to oscillate across. This is
the main reason the design has so few moving parts.

## The positional bias

Each warrior carries a fixed positional bias for the whole battle, drawn once as
a pure function of the scenario seed, a system tag, and its own entity ID.

```text
key        = Fnv1a(LastStandTag, Seed, EntityId)
generator  = new SplitMix64(key)
spanRaw    = (2 * RallyJitterRaw) + 1
offsetXRaw = generator.NextInt(spanRaw) - RallyJitterRaw
offsetYRaw = generator.NextInt(spanRaw) - RallyJitterRaw
```

`LastStandTag` is the 64-bit constant `0x484B424F5F4C5354`, which is the ASCII
text `HKBO_LST`. It mirrors `HitLocationResolver`'s existing tag
`0x484B424F5F484954`, or `HKBO_HIT`, which is this repository's precedent for a
keyed, stateless deterministic draw.

Three properties matter, and each is load-bearing.

The key **contains no tick**. The bias is a personal constant, not a per-tick
sample. A tick-keyed offset would move each warrior's destination every tick and
produce exactly the jitter-and-stall failure flagged in the steering analysis in
`docs/research/FORMATION_AND_COLLISION_MECHANICS.md`. With a tick-free key, a
warrior's aim point moves only because the rally agent moved, which is smooth,
or because the rally agent died, which happens at most a handful of times in a
battle.

The bias is **computed, never stored**. It is a pure function of values that are
already available, so nothing is added to `AgentState`, nothing is added to
`AgentView`, nothing is added to the per-agent block of `StateHasher`, and
nothing is added to `BattleSnapshot`. Two `NextInt` calls per regrouping warrior
per tick, bounded above by the threshold, is negligible against a stage that
already runs an all-pairs target scan, and `SplitMix64` is a struct, so nothing
allocates.

The draw uses **its own generator instance** seeded from the key. It never
touches the spawn generator created in `BattleSimulation.Create`, so adding this
feature cannot shift spawn positions for any seed. This is the same isolation
property the roster-count work already proved, and the same test shape is used
here.

## Exact numeric contract

Every value is stated in raw fixed-point units. One world unit is
`FixedPoint.Scale`, which is 1,024 raw units. The default body radius is
`4 * FixedPoint.Scale`, which is 4,096 raw units.

| Name | Where | Value | Units | Rationale |
| --- | --- | --- | --- | --- |
| `Scenario.LastStandThresholdAgents` | new hashed scenario field | property default `0`; `Scenario.CreateDefault` applies `6` | living warriors per faction | `0` disables the feature. `6` is roughly the point at which a 100-per-faction battle stops reading as a line and starts reading as scattered duels |
| `FormationRules.DefaultLastStandThresholdAgents` | new constants file | `6` | warriors | The value `CreateDefault` applies |
| `FormationRules.RallyJitterRadiusMultiplier` | new constants file | `6` | multiples of `BodyRadiusRaw` | Sets cluster size relative to a body |
| `FormationRules.RallyPackingMargin` | new constants file | `4` | dimensionless | Bodies may cover at most a quarter of the bias square |
| `FormationRules.MaximumLastStandThresholdAgents` | new constants file | `9` | warriors | Capacity divided by the packing margin, derived below |
| `FormationRules.RallyTrailRadiusMultiplier` | new constants file | `12` | multiples of `BodyRadiusRaw` | How far behind the leader the cluster sits |
| `FormationRules.RallyCorridorHalfWidthMultiplier` | new constants file | `2` | multiples of `BodyRadiusRaw` | Half-width of the leader's protected forward corridor |
| `RallyJitterRaw` (derived) | `6 * Scenario.BodyRadiusRaw` | `24576` at the default radius | raw units | 24 world units. Not a scenario field, so it adds no hash input and no tuning knob |
| `RallyTrailRaw` (derived) | `12 * Scenario.BodyRadiusRaw` | `49152` at the default radius | raw units | 48 world units behind the leader |
| Per-axis offset range | derived | `[-24576, +24576]` inclusive at the default radius | raw units | 49,153 distinct values per axis |
| Cluster extent | derived | `49152` raw across each axis | raw units | 48 world units, about six bodies wide |

### The packing bound

The bias square has side `2 * RallyJitterRaw`, which is
`2 * RallyJitterRadiusMultiplier * BodyRadiusRaw`. A body occupies a square of
side `2 * BodyRadiusRaw`. Dividing one side by the other gives
`RallyJitterRadiusMultiplier`, and squaring it gives the square's *capacity* in
non-overlapping bodies, which is 36 at the chosen multiplier of six. That result
is independent of what `BodyRadiusRaw` actually is, because the multiplier is
fixed.

Capacity is not a safe headcount, and the first draft of this design made
exactly that mistake by setting the maximum threshold to it. Filling the square
to capacity requires perfect packing, and these offsets are drawn at random, so
in practice every follower overlaps somebody, the resolver blocks the whole
cluster, and — since the leader is surrounded by its own followers — even the
exempt leader cannot move. That was not a theoretical concern: at a threshold
equal to capacity, a sixteen-versus-sixteen battle ended in a forced draw at
tick 10,000 with both factions at full strength and a longest blocked streak of
9,975 ticks.

`MaximumLastStandThresholdAgents` is therefore capacity divided by
`RallyPackingMargin`, which is `36 / 4 = 9`. Bodies then cover at most a quarter
of the bias square and the resolver always has room to separate them. At the
default threshold of 6 there is more room still. A threshold above 9 is rejected
by `Scenario.Validate` rather than merely discouraged.

### The overflow bound

`SplitMix64.NextInt` takes an `int` exclusive upper bound, and the span is
`2 * RallyJitterRadiusMultiplier * BodyRadiusRaw + 1`. `Scenario.Validate`
already permits a body radius up to `MaximumMapDimension * FixedPoint.Scale`,
and twelve times that overflows a signed 32-bit integer. `Validate` therefore
additionally rejects, **only when the last stand is enabled**, any
`BodyRadiusRaw` for which that span exceeds `int.MaxValue`. The largest
permitted radius is `(int.MaxValue - 1) / (2 * RallyJitterRadiusMultiplier)`,
which is 178,956,970 raw units, or roughly 174,762 world units. That is far
larger than any body the game will use and far smaller than the point at which
the arithmetic breaks. The bound is expressed through
`FormationRules.IsBodyRadiusWithinJitterSpanRange` rather than as a literal, so
it tracks the multiplier automatically.

### Arithmetic rules

The aim point is computed in `long`, saturated into `int` the same way
`CollisionResolver` saturates a coordinate, then passed through
`CollisionGeometry.ClampCenterToBounds` against the map dimension and the body
radius. Every intermediate is `checked`. There is no floating point, no
`FixedPoint` multiply or divide operator — neither exists — and no square root
beyond the existing `BattleSimulation.IntegerSquareRoot`. The approach itself
reuses `BuildMovementProposal` unchanged, including its
`delta * movement / distance` normalisation and its zero-step axis fallback, by
passing it a destination point instead of a target agent.

## Tick-stage placement

No new stage is added. The committed order in `BattleSimulation.AdvanceOneTick`
is unchanged:

```text
DecrementCooldowns
SelectTargetsAndIntents      <- modified
GatherMovementProposals      <- modified
ResolveCollisions
CommitMovement
MeasureCollision
GatherAndCommitAttacks
ResolveOutcome
```

`SelectTargetsAndIntents` gains a short preliminary pass over the agent array
that computes, for each of the two factions, the living count and the lowest
living entity ID. That pass writes only two two-element arrays allocated once in
the constructor, and it completes before any intent is assigned, so no warrior's
intent can depend on the order in which warriors were visited. The existing
target-selection loop is then unchanged except for one added branch that assigns
`Regrouping`.

`GatherMovementProposals` gains a `Regrouping` branch that computes the aim
point and calls the same proposal builder. Both stages run strictly before
`CommitMovement`, which is the only place positions are written, so both read
tick-start positions. No new read-after-write hazard is introduced.

## Total order rule

Three orderings exist, and all three are total.

The rally agent is the minimum `EntityId` over the living warriors of a faction.
Entity IDs are unique, so the minimum is unique and no tie-break is required.

Target selection is unchanged and keeps its existing rule of nearest by squared
distance, breaking ties on the lower `EntityId`.

Collision resolution is unchanged and keeps its documented ascending-`EntityId`
priority.

The positional bias is a pure function of the seed and the entity ID and
involves no ordering at all.

## Random stream

A new stream tag, `LastStandTag = 0x484B424F5F4C5354`. The stream is derived per
warrior from the scenario seed, that tag, and the entity ID, which satisfies the
`(match_seed, system_tag, entity_id)` requirement in
`SIMULATION-GAME-STANDARDS.md` §4. Each draw constructs a fresh `SplitMix64`
from the mixed key, so no generator state persists between calls, between
warriors, or between ticks. Because no existing generator is advanced, adding
this feature cannot shift the sequence any other system consumes.

## Spectator observability

`AgentIntent` gains `Regrouping = 4`, appended after `Dead = 3`. The numeric
ordering is slightly awkward, since `Dead` is conceptually terminal, but the
append-only rule for hashed enum values forbids reordering and that rule wins.

The agent inspector already renders the intent using the enum's own
`ToString()`. The new value therefore appears as `Intent: Regrouping` with **no
change to any `Hukbo.Client` file**. This is the primary spectator channel, and
it is authoritative simulation state, written by the intent stage and included
in the state hash, never inferred by presentation code.

Two further channels come free. A regrouping warrior's `Move` event names the
rally agent in its target field, so the battle event log reads as "entity 7
moved toward entity 2" rather than naming an enemy it is not approaching. And
the behaviour itself is the point: the last few warriors visibly bunch and
advance as a body instead of dispersing.

No headless metric is added. `CollisionMetrics` belongs to the collision
decision record and is not extended here. The per-agent authoritative reason is
the approved observability channel under that record, and this feature uses it.

## Rejected alternatives

| Alternative | Reason for rejection |
| --- | --- |
| Cohesion bias blended into the existing steering vector | Needs a blend weight, which is the over-parameterisation risk named in the steering research. Blending two aim points needs a ratio multiply that truncates toward zero, which is the integer-division bias named in the same research. Worst of all, with any weight below one, every survivor still walks toward its own private enemy, so the isolated-duel geometry survives at reduced amplitude rather than being fixed. |
| Centroid of the faction's living positions as the anchor | The centroid is an unoccupied point, so every survivor converges on the same empty spot and the solid resolver blocks them in ascending entity ID, permanently seating the lowest ID at the centre. It also needs an integer division whose truncation biases the point toward the low corner. A leader is an occupied disc and has neither problem. |
| Fixed formation slots assigned by index | This is a rigid template, which the formation research explicitly warns against, and it needs a slot-to-warrior assignment rule that must itself be totally ordered and re-run whenever anyone dies. |
| Tick-keyed jitter | Moves every warrior's destination every tick. This is the jitter-and-stall failure named in the steering research, and it produces visible vibration. |
| Storing the positional bias on `AgentState` | Adds two hashed per-agent fields plus changes to `ToView`, `AgentView`, and the `StateHasher` per-agent block, for no behavioural difference from a pure function. |
| An explicit "break formation" radius | Unnecessary, because attack resolution never consults intent. Adding it would introduce a threshold a warrior can oscillate across and a second destination per tick. |
| A dedicated `BattleEventKind` for regrouping | Unbounded event volume against a feed that retains 200 events, and the per-agent intent already carries the information. This is the same reasoning that rejected collision events. |
| Shrinking perception range late in a battle | Makes survivors stand still rather than cluster, and silently changes an existing hashed scenario value in the middle of a run. |

## The nine standard questions

`SIMULATION-GAME-STANDARDS.md` §10.

**1. User-visible outcome.** When a faction drops to its last few warriors, the
survivors close on their lowest-`EntityId` living comrade and advance as one
loose, irregular clump instead of dispersing into unrelated duels. The endgame
reads as a converging last stand. A spectator can confirm the mechanism by
selecting a survivor: the inspector shows `Intent: Regrouping`.

**2. Tick stage and state read and written.** No new stage.
`SelectTargetsAndIntents` gains a preliminary pass that reads `HitPoints`,
`FactionId`, and `EntityId` for every agent and writes two internal two-element
arrays holding the per-faction living count and rally entity ID; the main loop
then reads those and writes `AgentState.Intent`. `GatherMovementProposals` reads
`Intent`, the rally agent's tick-start `XRaw` and `YRaw`, `Scenario.Seed`,
`Scenario.BodyRadiusRaw`, and `EntityId`, and writes the internal
movement-proposal array. Both stages run before `CommitMovement`, the only
writer of positions, so both read tick-start state. No other stage changes.

**3. Numeric units, bounds, and the same-tick conflict rule.** All positional
values are raw fixed-point units where one world unit is 1,024 raw.
`LastStandThresholdAgents` is a count of living warriors per faction, valid in
`[0, 9]`, default `0` on the property and `6` from `Scenario.CreateDefault`.
The per-axis bias is in `[-6 * BodyRadiusRaw, +6 * BodyRadiusRaw]`, which is
`[-24576, +24576]` at the default radius. The aim point is centred
`12 * BodyRadiusRaw` behind the leader, and a follower inside the leader's
`2 * BodyRadiusRaw` forward corridor steps laterally clear instead. The 9
ceiling is the bias square's capacity divided by the fourfold packing margin.
`Validate` additionally rejects a body radius for
which `2L * RallyJitterRadiusMultiplier * BodyRadiusRaw + 1` exceeds `int.MaxValue`, but only when the
threshold is nonzero. Every computation is `checked`; the aim point is computed
in `long`, saturated to `int`, then clamped by
`CollisionGeometry.ClampCenterToBounds`. The same-tick conflict rule is that
`Attacking` beats `Regrouping`: a warrior already within contact distance of its
enemy fights and does not rally. Two warriors whose aim points collide are
separated by the existing solid-disc resolver under its existing
ascending-`EntityId` priority; this feature adds no conflict rule of its own.

**4. Total ordering and random-stream policy.** The rally agent is the minimum
`EntityId` over living warriors of a faction, which is unique and needs no
tie-break. Target selection and collision priority are unchanged. The random
stream is the scenario seed, `LastStandTag = 0x484B424F5F4C5354`, and the entity
ID, mixed through `Fnv1a` and finalised through a fresh `SplitMix64`. It
excludes the tick deliberately. It advances no other generator, so it cannot
shift spawn placement or hit-location resolution for any seed.

**5. Cache source and invalidation.** **No cache.** The rally agent and the
living counts are recomputed from authoritative state by a single forward scan
every tick. The positional bias is recomputed from a pure function every time it
is needed. Nothing is memoised, nothing is stored, and there is no invalidation
to get wrong. This satisfies the `CLAUDE.md` prohibition on caching targets and
on unbounded caches.

**6. Save, event, and version effect.** `Scenario.LastStandThresholdAgents` is a
new authoritative hashed field and enters the scenario block of
`StateHasher.Compute`. `AgentIntent.Regrouping = 4` is appended and enters the
state hash through the existing per-agent `Intent` write. Both the state hash
and the event hash **will move**, because warriors stand in different places and
because a regrouping warrior's `Move` event names the rally agent rather than an
enemy. This is expected and approved. No new `BattleEventKind`, no new
`AgentView` field, no new `AgentState` field, and no `BattleSnapshot` change.
The canonical seed-1 oracle is re-recorded exactly once from a single final
verified run.

**7. Worst-case complexity and benchmark workload.** The rally scan is one
linear pass over the agent array per tick, which is `O(n)` against a stage that
already runs an `O(n^2)` all-pairs target scan, so the added term is
asymptotically free. The bias draw is at most two `NextInt` calls per regrouping
warrior per tick, bounded above by 9 warriors per faction by validation and by
6 at the default, so at most 36 calls per tick in the worst legal configuration.
`SplitMix64` is a struct and the counters are preallocated, so warm-tick
allocation must stay at its current level. Benchmark workloads are unchanged:
200 agents, 10,000 ticks, seed 1 as acceptance, and 500 agents at the same
settings as report-only.

**8. Spectator explanation.** `AgentIntent.Regrouping` in the agent inspector's
intent line, rendered by the existing `ToString()` interpolation with no client
change. Secondarily, a regrouping warrior's `Move` event names the rally agent
as its target in the battle event log. Thirdly, the behaviour itself: the last
survivors visibly converge and advance together. **Yes, a spectator can discover
this without reading source code.**

**9. Tests that fail before and pass after.** Enumerated per task in
`docs/plans/2026-07-27-last-stand-formation.md`. The load-bearing ones are
`TheLowestLivingEntityIdIsTheRallyAgentForItsFaction`,
`AFollowerBelowTheThresholdIsMarkedRegrouping`,
`ARegroupingFollowerMovesTowardTheRallyAgentPlusItsOffset`,
`ARegroupingFollowerStillAttacksAnEnemyInsideReach`,
`OffsetDoesNotDependOnTheTick`,
`LastStandRallyDrawsDoNotChangeSpawnPositions`,
`RallyAgentSelectionIsUnchangedByAgentArrayPermutation`, and
`BothFactionsInASixVersusSixLastStandReachATerminalOutcome`.

## Risks

| # | Risk | Severity | How it manifests | How it is detected |
| --- | --- | --- | --- | --- |
| R1 | Pre-existing: `BodyRadiusRaw` and `CollisionPolicy` are hashed but absent from the manual `Scenario.Equals` and `GetHashCode`, so two scenarios differing only in body radius compare equal | Medium | Latent today, because `Scenario.Equals` has no behavioural caller outside tests, but it shows the "three places" checklist was already missed once | `ScenariosDifferingOnlyInBodyRadiusAreNotEqual`, fixed in the same task that adds the new field |
| R2 | Trigger flap causing per-tick intent oscillation | Eliminated by construction | Would appear as visible vibration | `LivingCountsNeverIncreaseAcrossAWholeBattle`. Hit points are only ever written as `Math.Max(0, hp - damage)` |
| R3 | Rally-agent death makes the cluster goal jump | Low | A visible pop as everyone re-aims | Bounded: all followers are already within the jitter square of the old leader, and the new leader is one of them, so aim points shift by a bounded amount. `RallyAgentDeathPromotesTheNextLowestLivingEntityId` plus smoke row 61 |
| R4 | Cluster packs tighter than the body radius and thrashes against the collision resolver | **Occurred.** Was High while the threshold was capped at full capacity | Warriors permanently blocked, blocked-streak spikes, a no-casualty draw at the tick limit | Fixed by the fourfold packing margin. `AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty`, measuring a worst streak of 92 ticks across seeds 1 to 20 against a 125-tick bound, plus the blocked-streak figure in the benchmark report. The bound was 60 against a measured 45 until the weapon clash lengthened battles; running the same scenario with a zero-interception ruleset still reproduces 45, which is the evidence that collision behaviour itself is unchanged |
| R5 | Both clusters stall and nobody advances | **Occurred twice.** The original "eliminated by construction" claim was invalid | Battle runs to the tick limit as a draw with no casualties | Not eliminated by construction — a leader is exempt from the formation but not from bodies. Fixed by the trail rule and the give-way rule, and asserted by `NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty` across twenty seeds at the maximum threshold, plus `BothFactionsInASixVersusSixLastStandReachATerminalOutcome` |
| R14 | A follower parks in its leader's line of travel and holds station there permanently | **Occurred.** Was unforeseen | Leader permanently `Blocked`; both factions deadlock; tick-limit draw with zero casualties | Fixed by the trail rule, which places every aim point behind the leader. Covered by `AFollowerAimsBehindTheRallyAgentRelativeToItsDirectionOfTravel` |
| R15 | A follower starting in front of its leader must cross it to reach its trail point, and the two block each other head-on | **Occurred.** Was unforeseen | Six of twenty seeds deadlocked at the tick limit | Fixed by the give-way rule. Covered by `AFollowerStandingInItsLeadersPathStepsAsideRatherThanThroughIt` and the twenty-seed stall lock |
| R6 | One-raw-unit twitch: the movement floor makes a settled warrior step one unit every tick forever and emit a `Move` event per tick | Medium | Event feed floods; event hash carries thousands of meaningless moves | The arrived-guard skips the proposal at contact distance. `ARegroupingFollowerAlreadyAtItsAimPointProposesNoMovementAndEmitsNoMoveEvent` |
| R7 | The jitter span overflows `int` for a large validated radius | Medium | Overflow exception or a negative bound at construction | The validation rule added with the scenario field. `ValidateRejectsABodyRadiusWhoseJitterSpanOverflowsWhenTheLastStandIsEnabled` |
| R8 | The default threshold silently changes every small-scenario Core test | High if the property default were 6 | Dozens of unrelated tests change behaviour | Neutralised by design: the property default is `0`, and `6` is applied only by `Scenario.CreateDefault`, which is the only route production code takes |
| R9 | The endgame lengthens because survivors walk to each other before fighting | Low | Terminal tick rises | Measured by the benchmark against a 10,000-tick limit with large headroom; watch the delta |
| R10 | An existing multi-seed victory test flips | Medium | One faction always wins | The existing test must pass unmodified. If it fails, treat it as a design signal about the threshold, not a test to weaken |
| R11 | Camera auto-pan sees an empty screen during a long regroup march | Low | The view drifts while nobody fights | Auto-pan's melee test requires `Attacking`, so regrouping warriors correctly do not count, and the leader is still fighting. Smoke row 63 |
| R12 | A tick-keyed offset is introduced by mistake | High | Visible per-tick vibration | The offset function takes no tick parameter. `OffsetDoesNotDependOnTheTick` |
| R13 | The rally scan depends on the incidental order of the agent array | Medium | A permuted array yields a different leader and a different hash | The scan compares `EntityId` explicitly. `RallyAgentSelectionIsUnchangedByAgentArrayPermutation` |

## Authorization

This design document does not authorize implementation. The ordered task list
and verification criteria are in
`docs/plans/2026-07-27-last-stand-formation.md`.
