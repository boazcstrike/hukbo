# Collision Policy Decision Record

**Date:** 2026-07-27
**Status:** Approved (product owner approved every value below on 2026-07-27)
**Amended:** 2026-07-27. Sections 1 through 12 are the original approved record
and are preserved unchanged. Two decisions were revised after the first gated
run; they are recorded in the [Amendment](#amendment--2026-07-27) at the end of
this document, which states exactly what it supersedes. Read the amendment
before acting on section 3, section 9, or section 11.
**Plan:** [docs/plans/2026-07-27-formation-collision-mechanics.md](../plans/2026-07-27-formation-collision-mechanics.md)
**Research:** [docs/research/FORMATION_AND_COLLISION_MECHANICS.md](../research/FORMATION_AND_COLLISION_MECHANICS.md)

## Historical boundary

This document records a **game-design invention**, not a historical claim. The
research supports constrained frontage, irregular spacing, local cooperation,
and crowded close contact. It does not support named formations, exact ranks,
fixed body spacing, or any particular collision solver. Nothing in this record
may be cited as a documented property of pre-colonial Philippine warfare, and no
value here is a measurement. The warning against named or slot-based formations
in the research document remains in force.

## 1. Contact policy

**Solid discs.** Every living agent is an impenetrable disc of one common
radius. Penetration between two living agents is never permitted, in any amount,
at the end of any tick.

The authoritative post-tick invariant is:

```text
for every ordered pair of living agents (a, b) with a.EntityId < b.EntityId:
    (bx - ax)^2 + (by - ay)^2  >=  (2 * BodyRadiusRaw)^2
```

Computed in checked `long` arithmetic on raw fixed-point coordinates.

Soft compression and faction-dependent contact are explicitly **rejected**. The
resolver must implement solid contact only, and must not contain a code path,
enum value, or configuration field that selects another behaviour.

The configuration still carries an explicit `CollisionPolicy` value so that the
choice is authoritative, hashed, and legible in a saved scenario. Exactly one
value is defined and accepted: `CollisionPolicy.Solid`. Adding a second value is
a new decision record, not an implementation detail.

## 2. Body model and tangent contact

One common radius for every living agent, held on the immutable `Scenario`, not
duplicated per agent and not exposed through `AgentState` or `AgentView`.

| Item | Value |
| --- | --- |
| `BodyRadiusRaw` | `4 * FixedPoint.Scale` = `4096` raw units (4 world units) |
| Diameter | `8192` raw units (8 world units) |
| Tangent contact | **Legal.** Exactly touching is clearance, not collision. |

Overlap is therefore the strict comparison `squaredDistance < (2R)^2`, and
`squaredDistance == (2R)^2` is an accepted resting position. This choice is what
lets a packed front settle at a stable spacing instead of jittering by one raw
unit forever.

`PawnRenderer` size stays cosmetic and never defines this radius. Presentation
may draw a pawn larger or smaller than 4 world units without affecting the
simulation.

## 3. Attack reach

**Centre-to-centre.** Attack eligibility keeps its current meaning: the squared
distance between agent centres compared against `AttackRangeRaw` squared. No
surface-gap subtraction is introduced anywhere.

| Item | Raw value | World units |
| --- | --- | --- |
| `AttackRangeRaw` (default) | `12288` | 12 |
| Body diameter `2R` | `8192` | 8 |
| Slack at full contact | `4096` | 4 |

Because `2 * BodyRadiusRaw < AttackRangeRaw`, two agents pressed into contact are
always inside attack range with four world units of slack. A packed front
therefore deals damage rather than deadlocking, which is the acceptance
condition in the plan's `Packed front` row.

`Scenario.Validate` must reject any configuration where
`2 * BodyRadiusRaw > AttackRangeRaw`, because that combination produces bodies
that can never reach each other.

Intent selection and attack gathering must call **one** shared reach helper so
the two stages cannot disagree.

## 4. Interaction matrix

| Pair | Behaviour |
| --- | --- |
| Living agent — living ally | Solid. Zero overlap. Identical to the enemy rule. |
| Living agent — living enemy | Solid. Zero overlap. |
| Living agent — corpse (`HitPoints == 0`) | **No collision.** Corpses are walked over. |
| Corpse — corpse | No collision. Dead agents never move and never block. |
| Living agent — map boundary | Hard clamp of the centre to `[R, max - R]` on both axes. |
| Corpse — map boundary | Not applicable; corpses do not move. |

Allies and enemies deliberately share one rule. A faction-dependent matrix was
considered and rejected because it doubles the test surface for a feel benefit
that has not been demonstrated.

Corpses are non-colliding so that a killing field cannot accumulate permanent
immovable obstacles, which is the most likely source of a late-battle stall.

## 5. Boundary rule

Agent centres are clamped to `[BodyRadiusRaw, dimensionRaw - BodyRadiusRaw]` on
each axis independently, where `dimensionRaw` is `MapWidth * FixedPoint.Scale` or
`MapHeight * FixedPoint.Scale`. Bodies are therefore never allowed past the map
edge.

This replaces the current inclusive `[0, dimensionRaw]` centre clamp. Corner
contact is simply both axes clamping in the same tick; there is no special
corner rule.

`Scenario.Validate` must reject a map smaller than one body on either axis, that
is `2 * BodyRadiusRaw > MapWidth * FixedPoint.Scale` or the same for height.

## 6. Movement and correction budget

Collision resolution may only **reduce** displacement. It may never add any.

For every living agent in every tick:

```text
resolvedDisplacement <= MovementSpeedRaw
```

measured as the integer square root of the committed squared displacement, using
the existing `IntegerSquareRoot` helper.

There is exactly one exemption, recorded in section 9: the coincident-centre
separation, which is a repair of an invalid input state rather than movement.
The exemption is bounded, applies at most once per agent per tick, and is
reported to the spectator as `Separated`.

### Tunneling and swapping

Tunneling and path swapping are **forbidden**, and are made geometrically
impossible rather than being tested for at run time.

`Scenario.Validate` must enforce:

```text
MovementSpeedRaw <= BodyRadiusRaw
```

With the approved values that is `3072 <= 4096`. Two agents moving directly at
each other close at most `2 * MovementSpeedRaw = 6144` raw units in one tick,
which is strictly less than the `8192` raw diameter they must cross to swap
sides. Because every intermediate committed position is also validated against
the zero-overlap invariant, no discrete step can pass through another body.

**Consequence for implementation:** swept-disc geometry, second-order
discriminants, and `Int128` arithmetic are **removed from scope**. The plan's
Task 3 implements static disc-overlap tests only. This is the removal that the
plan's Task 1 Step 2 authorises.

## 7. Corpse interaction

Corpses do not collide, do not move, do not propose movement, and do not
participate in pair generation. Pair generation filters to living agents only,
before the grid is built, so a battlefield full of corpses costs nothing in the
collision stage.

A living agent may finish a tick with its centre exactly on top of a corpse.
This is intentional and is not reported to the spectator.

## 8. Observability

**An authoritative per-agent resolved-movement reason**, exposed through
`AgentView` and rendered as a label in the agent inspector panel. No collision
events are added to `BattleEvent`, because a 200-agent packed front would emit
thousands of contacts per tick into a feed that retains 200 events.

The value is authoritative simulation state: it is written by the collision
stage, included in the state hash, and never derived by presentation code.

```csharp
public enum MovementResolution
{
    None = 0,        // agent did not propose movement this tick
    Moved = 1,       // preferred destination accepted unchanged
    Truncated = 2,   // moved along the preferred direction, shorter than intended
    Slid = 3,        // moved along one axis only
    Blocked = 4,     // no legal candidate; the agent held position
    Separated = 5,   // displaced out of an exact co-location
}
```

Enum numeric values are pinned. Reordering or renumbering them requires a new
preset version and new golden expectations.

Inspector labels: `Moving`, `Crowded`, `Sliding`, `Blocked`, `Pushed apart`.
`None` renders nothing.

Aggregate collision counters are additionally reported by the headless runner
(section 10). They supplement the spectator explanation; they do not replace it.

## 9. Resolution order, candidates, and fairness

The collision stage runs between movement intent and attack resolution:

```text
DecrementCooldowns
SelectTargetsAndIntents
GatherMovementProposals      // reads tick-start positions only
ResolveCollisions            // rebuilds grid, validates candidates
CommitMovement               // single commit, emits Move events
GatherAndCommitAttacks       // reads resolved positions
ResolveOutcome
```

### Priority

**Amended 2026-07-27.** Movers are resolved in ascending
**`CollisionPriority` key** order, where the key is
`(Fnv1a(tag, seed, tick, entityId) >> 32) << 32 | entityId`. Once an agent's
position is committed for the tick, later movers treat it as an obstacle, so a
lower key wins a contested destination. The key is recomputed every tick, and
its low half is the entity ID, which keeps the order strict and total when two
mixes collide in their top halves — ties still break on stable `EntityId`
beneath the shuffle. No random stream is consumed; the key is a pure hash, like
hit-location selection, so it reproduces exactly on a resumed save.

Stationary bodies still commit first, in ascending `EntityId`. They contest no
ground with one another; the only decision in that pass is the exact
co-location repair.

The original rule resolved movers in ascending `EntityId`, which this record
accepted as fair enough on the grounds that a blocked agent is still inside
attack reach and therefore still fighting. That reasoning is correct about an
individual agent and wrong about outcomes. Faction 0 holds entity IDs
`1..AgentsPerFaction`, so under the original rule it won **every** cross-faction
contest of every battle. Taking contested ground means advancing into the enemy
mass, where more enemies hold you in reach; damage is simultaneous, so the
faction that always wins the push always takes more damage. Once the mirrored
starting deployment removed the positional noise that had been masking it, the
rule decided 19 of 20 seeds. Measured over seeds 1 to 20 at 200 agents:
1 faction-0 victory before the amendment, 7 after; over seeds 1 to 40 after the
amendment, 16 faction-0 victories, 23 faction-1 victories and 1 draw. Three
attempts to correct it in the deployment geometry instead — independent jitter,
a half-lane offset, and the exact mirror — all produced the same 1-in-20 result,
which is what established that the rule and not the geometry was the cause.

Being blocked still does not remove an agent from combat, so no separate
anti-stall or fairness escape rule is added. `TickLimit` remains the terminal
backstop.

### Candidate order

For each mover, the first candidate that satisfies the zero-overlap invariant
against all committed positions and the boundary rule is taken. Candidates are
evaluated in this fixed order:

1. The preferred destination at full step. Accepting this reports `Moved`.
2. X-axis-only slide: preferred X, tick-start Y. Reports `Slid`.
3. Y-axis-only slide: tick-start X, preferred Y. Reports `Slid`.
4. A truncation ladder along the preferred direction at lengths
   `m >> 1, m >> 2, ... ` down to and including `1`, skipping zero lengths,
   where `m` is the preferred movement length. Reports `Truncated`.
5. Hold the tick-start position. Reports `Blocked`.

The ladder is bounded at eleven entries because `MovementSpeedRaw` fits in
twelve bits at the approved value, so a mover evaluates at most fourteen
candidates per tick.

Rounding for every truncated candidate uses the existing integer division
formulation `delta * length / distance`, which truncates toward zero. Odd
remainders are discarded, never redistributed; there is no correction to split
between two agents because the solid resolver moves one agent at a time.

### Exact co-location fallback

Two living agents may only share a centre through `CreateForTesting` or an
unresolved spawn, never through normal ticking. When it is detected, the agent
with the **higher `EntityId`** is displaced by exactly `2 * BodyRadiusRaw` in a
fixed direction order, taking the first that is legal:

```text
+X, -X, +Y, -Y
```

If none is legal, the displacement is skipped and the agent reports `Blocked`
rather than throwing. This terminates in bounded time, cannot oscillate, and is
exempt from the movement budget as recorded in section 6. It reports `Separated`.

## 10. Spawn, density, and performance budgets

### Spawn

`BattleSimulation.Create` resolves spawn overlaps deterministically. Agents are
placed in ascending `EntityId`. When a generated position overlaps an already
placed body or violates the boundary rule, candidate positions are scanned in
fixed ring order around the generated position, at ring radius
`r * 2 * BodyRadiusRaw` for `r = 1, 2, 3, ...`, enumerating the eight compass
offsets per ring in the order `+X, +X+Y, +Y, -X+Y, -X, -X-Y, -Y, +X-Y`. The
first legal candidate is taken. The random stream is not consulted during
relocation, so relocation cannot shift the seed sequence.

Impossible density fails loudly. `Scenario.Validate` rejects a configuration
where the conservative square-packing bound is exceeded. The bound is stated
here in its algebraic form:

```text
TotalAgents * (2 * BodyRadiusRaw)^2  >  mapWidthRaw * mapHeightRaw
```

**That expression must not be evaluated literally.** At `MaximumMapDimension`
the left side reaches roughly `2.1e22`, far past `long.MaxValue`. The
implementation uses the equivalent division form, which stays in range:

```text
TotalAgents  >  (mapWidthRaw * mapHeightRaw) / (2 * BodyRadiusRaw)^2
```

This is exact rather than approximate for positive `bodyArea`. With
`q = mapArea / bodyArea` and `r = mapArea % bodyArea`, the identity
`T * bodyArea > mapArea  <=>  (T - q) * bodyArea > r` holds because
`0 <= r < bodyArea`.

The map-fit checks of section 5 must run **before** this one. They are what
bounds `bodyArea` enough for the remaining products to be safe; the ordering is
load-bearing, not stylistic.

Boundary equality is accepted: only a strictly greater agent count is rejected.

If the ring scan still exhausts its bound at run time, `Create` throws an
`InvalidOperationException` naming the entity that could not be placed. It never
returns a simulation with overlapping bodies.

### Budgets

| Workload | Requirement |
| --- | --- |
| 200 agents, 10,000 ticks, seed 1 | **Acceptance.** Must pass the canonical gate, remain deterministic across two same-build runs, and terminate within `TickLimit`. |
| 500 agents, 10,000 ticks, seed 1 | **Report only.** Must complete deterministically; timing and allocation are recorded, not gated. |
| Warm-tick allocation | Collision adds **zero** steady-state allocation. All grid, pair, proposal, and resolution storage is preallocated and reused, growing only when capacity is insufficient. |

### Reported metrics

The headless `RunReport` gains: candidate pairs per tick, contact pairs per
tick, accepted movement count, blocked count, longest blocked streak, front
width, front depth, and the number of agents able to attack. All are aggregates
over the run and must be identical across two same-seed runs.

## 11. Persistence and hashing

New authoritative fields entering the state hash:

- `Scenario.BodyRadiusRaw`
- `Scenario.CollisionPolicy` (as its integer value)
- per-agent `MovementResolution` (as its integer value)

The uniform grid, pair buffers, proposal buffers, and collision counters are
**derived**. They are never hashed, never snapshotted, and never persisted.

`BattleSnapshot` stays a completed-tick render snapshot. Collision configuration
remains reachable through `BattleSimulation.Scenario`.

**Baseline hashes change.** Adding these fields and constraining movement will
move both the state hash and the event hash for every seed, including the
recorded seed-1 baseline. This is expected and approved. The canonical seed-1
oracle is re-recorded exactly once, at the end of the plan's Task 10, from a
single final verified run.

## 12. Rejected alternatives

| Alternative | Reason for rejection |
| --- | --- |
| Soft discs with bounded penetration | Requires an invented maximum-penetration constant and an invented iteration count, and yields a weaker invariant that is harder to test than "zero overlap". |
| Faction-dependent contact | Doubles the interaction matrix and regression surface for an undemonstrated feel benefit. |
| Surface-gap attack reach | Pushes effective engagement to 20 world units centre-to-centre and forces a retune of every existing combat-range test. |
| Loadout-specific radii | The research explicitly recommends proving one common radius first. |
| Corpses as solid obstacles | Creates permanent immovable blockers and a plausible late-battle stall mode. |
| Swept-disc tunneling tests | Made unnecessary by the `MovementSpeedRaw <= BodyRadiusRaw` validation; would add `Int128` arithmetic for an impossible case. |
| Collision `BattleEvent` kinds | Unbounded per-contact spam against a 200-event feed. |
| Rigid-body physics, ORCA, velocity, mass | Out of scope by the plan's guardrails and by `CLAUDE.md`. |

## Amendment — 2026-07-27

**Status:** Approved. The product owner approved both changes recorded below on
2026-07-27, after the original policy had shipped and passed the canonical gate.

Sections 1 through 12 above are the original approved record and have not been
edited. This amendment exists because the first gated run of the shipped policy
showed two things that the original record had not anticipated: agents never
actually pressed their bodies together, and the counter that was supposed to
report body contact could not register a contact even in principle. Both are
recorded here as approved revisions. Where this amendment contradicts a statement
above, this amendment is the current rule and the statement above is history.

### A1. Movement targets body contact, not attack range

**What changed.** An advancing agent used to close only until its target was
inside `AttackRangeRaw`, that is until the centre-to-centre distance reached
`12288` raw units. Because a body is `8192` raw units across, two opposing front
ranks that both stopped at reach came to rest with `4096` raw units — four world
units — of permanent air between their surfaces. Bodies therefore never touched
for the whole engagement, and the collision stage only ever observed allies
queueing behind their own front line. `BuildMovementProposal` now subtracts
`2 * BodyRadiusRaw` rather than the attack range from the distance to the target,
so the movement target is **body contact**.

**What this supersedes.** Section 3 presented the `4096`-raw slack between the
body diameter and the attack range as the spacing at which a packed front would
settle, and section 9 leaned on the same figure when it argued that a blocked
agent is still fighting. The slack is **retained, and it is still load-bearing
for attack purposes**: attack resolution is unchanged, still measured
centre-to-centre against `AttackRangeRaw`, and because that reach is wider than
the diameter, a rank pressed into contact fights and the rank immediately behind
it can strike past. What the slack no longer does is govern where an agent stops
advancing. The validation rule in section 3 that rejects
`2 * BodyRadiusRaw > AttackRangeRaw` is unchanged and is now more important, not
less, because the movement target sits exactly at the diameter.

Section 9's conclusion still holds on its own terms: a blocked agent remains
inside reach and remains in combat, so no anti-stall or fairness escape rule is
added, and `TickLimit` remains the terminal backstop.

**`AgentIntent.Attacking` now means "arrived".** Intent selection marks an agent
`Attacking` only once its squared distance to the target is at or inside the
contact distance; an agent still closing is `Moving` even when it is already
inside weapon reach. An agent that lands a blow while still closing is re-marked
`Attacking` by attack gathering in the same tick, so a spectator watching the
inspector or the pawn still sees a fighting agent rather than a marching one.
This preserves the observability requirement in section 8 without making
`Moving` and `Attacking` overlap in the intent-selection stage.

### A2. Contact metrics are measured over a proximity band

**Why the old counter could not work.** A solid resolver guarantees that every
living pair ends the tick at or beyond `(2R)^2`. Counting a pair as "touching"
therefore meant a squared distance of *exactly* `(2R)^2`, which on an integer
lattice requires a Pythagorean coincidence between the two axis deltas and the
diameter. That is unreachable in practice. This is the mechanical reason the
first gated run reported `contactPairs` of `0`: not because the ranks stopped at
reach alone, but because an exact-tangency test can essentially never fire even
once bodies do close.

**What changed.** Contact metrics now use a proximity band of
`BodyRadiusRaw + (MovementSpeedRaw / 2)` per body, so a pair is counted when the
two bodies are within one movement step of touching. That is the honest reading
of "pressed together" for a spectator, and it is stable against the one-raw-unit
rounding that integer truncation produces.

**Determinism status.** The band is derived observability. It is never hashed,
never snapshotted, and never persisted, exactly as section 11 requires of every
collision counter. Both the state hash and the event hash were confirmed
byte-identical before and after the band was introduced, which is the evidence
that it stayed on the derived side of the line.

### A3. The seed-1 oracle is re-recorded

Section 11 said the canonical seed-1 oracle would be re-recorded exactly once, at
the end of the plan's Task 10. This amendment re-records it a second time,
because change A1 moves where agents stand and therefore moves both hashes again.
The current recorded baseline, from one final verified run on the amended branch
at 200 agents and seed 1, is `Faction1Victory` at tick `657` with state hash
`D78F0B527B7F938F` and event hash `AC3BAAEC684854D5`.

The hashes recorded for the pre-amendment run — state `7EE8BF6EC0F11BB2` and
event `9BFC18AD06F4F572` at tick `781` for 200 agents, and state
`7402CCC7C6EC3B50` with event `619CCC872BBB2413` for 500 agents — are
**superseded**. They are kept on record so the transition can be traced; they are
dead values and may not be used as a regression target. The full figures for both
workloads are in
[docs/development/testing.md](../development/testing.md).

### A4. What the amendment actually changed in the numbers

Measured on the 200-agent, seed-1 acceptance workload, before and after:

| Metric | Before the amendment | After the amendment |
| --- | --- | --- |
| `contactPairs` | 0 | 5,649 |
| `blockedAgentTicks` | 7,154 | 14,544 |
| Terminal tick | 781 | 657 |
| `maximumPenetrationRaw` | 0 | 0 |

Bodies now meet, so cross-faction contact is observable for the first time.
Crowding roughly doubled, which is the expected consequence of a front that
closes all the way rather than halting with air in front of it. The battle
resolves sooner because the fighting ranks are closer together and more agents
are in contact at once. Penetration stayed at exactly zero, which is the point:
the solid-disc invariant of section 1 is unaffected by either change.

### A5. Alternatives ruled out by the amendment

These extend the section 12 table rather than replacing any row in it. Every
row above in section 12 was rejected on reasoning; these two were rejected on
evidence from the amended branch.

| Alternative | Reason for rejection |
| --- | --- |
| Raise `BodyRadiusRaw` so that `2 * BodyRadiusRaw == AttackRangeRaw` | Tempting, because it would make "at reach" and "in contact" the same condition and remove the need for a separate approach target. It does not work. The truncating integer division `delta * length / distance` makes a mover under-shoot its computed step, so it lands just beyond tangency rather than on it, and an exact-tangency contact test still never registers. It also destroys the slack that lets the second rank strike past the first. |
| Set `2 * BodyRadiusRaw > AttackRangeRaw` | Two bodies in contact could then never reach each other, so a closed front would deadlock permanently. The `Scenario.Validate` rule recorded in section 3 exists precisely to make this configuration impossible, and the amendment leaves that rule in force. |
