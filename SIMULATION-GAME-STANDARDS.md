# Hukbo Simulation Game Standards

Status: initial baseline  
Scope: deterministic autonomous combat sandbox  
Source direction: `README.md`

## 1. Product baseline

The first milestone proves one promise: autonomous factions fight in an understandable way, and
resetting the same scenario produces the same result.

| Open question | Starting decision | Why |
|---|---|---|
| Match or persistent world? | Disposable matches | Avoid campaign-state complexity before combat is proven. |
| First scale? | 200 combatants; 500-combatant stress report | Both are candidate scales named in the README; 200 forces spatial indexing without making optimization the whole project. |
| Readability or physiology? | Readability | Start with health, range, cooldown, and low-health retreat. |
| Player setup? | Seed, map size, two faction presets, unit count, start regions | Small, reproducible scenario surface. |
| Dots? | v0.1 visual identity | Avoid an asset/animation pipeline before the simulation is fun. |

### v0.1 acceptance outcome

A user can run and inspect a two-faction battle, control the camera/pause/speed, reset the seed, and
see a winner. Repeated headless runs match winner, ordered events, and final state hash.

### Deferred layers

Terrain/pathfinding, cover, projectiles/ammo, morale, diplomacy, body parts, equipment, needs,
economy, persistent worlds, multiplayer, and mods are deferred. Section 11 preserves the future
pathfinding acceptance bar.

## 2. Language and engine decision

There is no universal best simulation language. The best starting choice depends on team
proficiency and the first delivery target.

| Option | Best fit | Advantages | Risks |
|---|---|---|---|
| **C# library + Godot** | Desktop-first C# team | Fast iteration/debugging; `Span<T>`/`Memory<T>` enable contiguous low-allocation processing ([Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/)) | GC and per-agent engine nodes can become hidden costs |
| **C# library + MonoGame** | Code-first desktop team | Small rendering surface and direct batched-draw control ([MonoGame](https://docs.monogame.net/articles/getting_started/0_getting_started.html)) | More camera, UI, tooling, and content workflow must be built in code |
| **Rust simulation crate + Bevy shell** | Rust-proficient team or strong native/WASM control requirement | Explicit data ownership, layout, and allocation control; Bevy has a fixed gameplay schedule ([Bevy](https://docs.rs/bevy/latest/bevy/prelude/struct.FixedUpdate.html)) | Slower iteration for a new Rust team; dependency migration churn; Bevy systems are parallel and nondeterministic by default ([Bevy ECS](https://docs.rs/bevy_ecs/latest/bevy_ecs/system/index.html)) |
| **TypeScript + Canvas/WebGL** | Browser delivery is the first product requirement | Fast sharing and UI iteration | Background tabs and display-timed callbacks cannot be the authoritative clock; later native/WASM core migration is likely |

### Recommendation

Use a **C#/.NET simulation core by default for desktop**. Use Godot as the conditional shell when
editor/UI tooling matters; use MonoGame when the team prefers a smaller code-first rendering layer.
Choose Rust only when team proficiency or measured C# results justify it. Revisit before
implementation if browser-first is required.

The choice is not final until one candidate passes Gate 0 in a release build:

- Headless 200/500-agent battles for 10,000 measured ticks
- Five minutes of 500 batched dots at 1080p
- Save/load and same-seed replay verification
- Tick/frame percentiles, allocations, working set, and engine version
- No gameplay state or object per agent in the engine scene

If both pass, prefer the team's fastest safe iteration path.

## 3. Starting architecture

```text
scenario/config
      |
      v
authoritative simulation core <--> snapshot/replay codec
      |
      v read-only render snapshot + events
      |
      v
renderer / camera / UI / diagnostics
```

| Boundary | Owns | Must not own |
|---|---|---|
| Simulation core | Authoritative state, rules, tick pipeline, random streams, state hash | Window, wall clock, GPU, filesystem, engine callbacks |
| Scenario | Validated starting state and immutable definitions | Runtime state |
| Spatial index | Reconstructable neighbor-query data | Durable position truth |
| Presentation | Batched dots, camera, selection, interpolation, visual event queue | Targeting, damage, retreat, victory |
| Persistence | Encoding, validation, atomic replacement | Gameplay policy |
| Headless/diagnostics | Tests, replays, benchmarks, timings, hashes | Alternate rules or mutation |

Rendering reads completed-tick snapshots and never feeds interpolated values into gameplay. Speed
controls alter ticks requested per wall-clock second, not logical tick duration.

Do not add rigid-body physics in v0.1; distance checks and hitscan are enough. The solid-disc
contact model recorded in section 13 is not rigid-body physics: it has no velocity, no mass, no
restitution, and no impulse. It is a per-tick legality test on a proposed destination, expressed
entirely in squared integer distances.

## 4. Deterministic simulation contract

### Gate 0 decisions, not permanent constants

Gate 0 measures map size, tick rate, and numeric representation. It may start at 20 Hz and a
2,048-unit square, but these are hypotheses. The chosen values must show:

- legible AI at normal/accelerated speed;
- 200-agent tick-time headroom on named hardware;
- no overflow within validated map, speed, and duration bounds; and
- repeatability at the promised same-build or cross-platform scope.

Godot and Unity document fixed gameplay/physics steps independent of rendered frames
([Godot](https://docs.godotengine.org/en/stable/tutorials/physics/interpolation/physics_interpolation_introduction.html),
[Unity](https://docs.unity3d.com/Manual/physics-optimization-cpu-manual-simulation.html)).

### Tick order

Every logical tick executes in this order:

1. Apply commands ordered by `(target_tick, command_sequence)`.
2. Apply queued spawns/despawns.
3. Rebuild/update the spatial index from authoritative positions.
4. Find eligible hostiles and select targets.
5. Select intent: idle, approach, attack, retreat, or dead.
6. Compute movement proposals.
7. Commit movement in ascending `EntityId`. Resolution order inside the collision stage is the
   per-tick `CollisionPriority` key; the state commit stays in ascending `EntityId`.
8. Create hitscan attack proposals.
9. Apply accumulated damage simultaneously.
10. Resolve death and victory.
11. Emit ordered authoritative/presentation events.
12. Produce scheduled state hash and read-only render snapshot.

New systems declare their stage; incidental call order never decides outcomes. The list above is
the general contract. The stage order the battle simulation actually executes today, including the
collision stages, is recorded in section 13.

### Ordering, numbers, and randomness

- `EntityId` is a monotonically increasing `u64`, unique within a match and never reused.
- Every multi-result query has a total order. Initial target tie-break:
  `(eligible desc, distance_squared asc, target_entity_id asc)`.
- Hash-map/set iteration cannot decide gameplay. Rust, for example, documents `HashMap` iteration
  as arbitrary ([Rust](https://doc.rust-lang.org/std/collections/struct.HashMap.html)).
- Authoritative time is an integer tick, never wall-clock time.
- Gate 0 chooses checked fixed-point/integer coordinates or a documented deterministic float
  policy. Cross-platform replay should prefer fixed-point after range/precision tests.
- Gate 0 chooses and pins a named RNG algorithm with test vectors. Runtime/engine defaults are
  prohibited; .NET does not guarantee `System.Random` sequences across major versions
  ([Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.random)).
- Random streams derive from `(match_seed, system_tag, entity_id or event_id)` so adding a draw in
  one system cannot shift unrelated outcomes.
- Start single-threaded. With Bevy, use a single-threaded authoritative schedule and make
  ambiguities errors. Later parallel work reads an immutable tick-start view, gathers proposals,
  sorts by a total key, then commits deterministically.
- State-hash cadence is configuration recorded in the replay. Gate 0 measures the cost and selects
  the cadence; it is not hard-coded as a design constant.

Determinism is verified, not assumed.

## 5. Minimal domain model

Components are data; systems provide behavior. This uses ECS separation without requiring a
general-purpose ECS
([Unity Entities](https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/concepts-components.html)).

### Match data

| Model | Initial attributes | Invariants |
|---|---|---|
| `Scenario` | format version, seed, map bounds, tick policy, two faction setups, ruleset ID | Immutable after validation |
| `BattleState` | tick, phase, winner?, next entity ID, next event sequence | Tick never decreases; IDs never reuse |
| `DefinitionRegistry` | faction, unit, weapon definitions by stable ID; content hash | Immutable during match |
| `RngState` | algorithm/version, root seed, required stream state | No implicit RNG |

### Faction and agent

| Model/component | Initial attributes | Invariants |
|---|---|---|
| `Faction` | ID, name, display color, spawn region | Exactly two; mutually hostile in v0.1 |
| `Identity` | entity ID, unit definition ID | Unique; definition exists |
| `FactionMember` | faction ID | Faction exists |
| `Position` | x, y | Inside validated map bounds |
| `Body` | one common radius held on `Scenario`, not per agent | Positive; defines solid contact per section 13, never a rigid body |
| `Movement` | speed per tick, retreat speed modifier | Nonnegative and bounded |
| `Vitality` | hp, max hp | `0 <= hp <= max_hp`; hp zero means dead |
| `WeaponState` | definition ID, cooldown remaining | Cooldown never negative |
| `Target` | entity ID? | Living hostile or empty |
| `Intent` | state, target/destination?, chosen tick, reason code | Exactly one state |
| `Lifecycle` | spawn tick, death tick? | Dead agents never act |

Weapon definition: stable ID, range, cooldown ticks, integer damage, optional integer hit chance.
If hit chance is omitted, v0.1 attacks always hit. There is no ammunition or projectile model.

Intent states:

- `Idle`: no eligible hostile.
- `Approach`: target exists outside weapon range.
- `Attack`: target is within range and cooldown permits.
- `Retreat`: hp ratio is at/below the ruleset threshold; move away from nearest hostile.
- `Dead`: terminal.

Initial events: `EntitySpawned`, `TargetAcquired`, `IntentChanged`, `AttackResolved`,
`DamageApplied`, `EntityDied`, and `BattleEnded`. Envelopes contain version, tick, sequence, type,
stable IDs, and reason codes such as `target_died`, `entered_range`, or `low_health`.

## 6. Caching and spatial queries

Every store is classified as:

1. **Authoritative state** — needed to continue exactly; saved and hashed.
2. **Immutable definition** — content-addressed by stable ID.
3. **Derived cache** — rebuildable; neither saved nor hashed.
4. **Presentation state** — camera/UI/interpolation; never gameplay.

Start with a uniform grid rebuilt each tick. Gate 0 measures cell size. Cells/results use stable
order and authoritative math. Prefer rebuilding over incremental invalidation until profiling
proves otherwise.

Do not cache targets in v0.1; validate/select each tick. Definition lookups and reusable scratch
buffers are allowed.

Every cache declares source, key/value, size bound, lifetime, invalidation, counters, and a
cold-cache equivalence test. Unbounded caches are prohibited.

## 7. Storage, snapshots, and replays

### v0.1 artifacts

- **Scenario:** versioned UTF-8 JSON for starting conditions.
- **Snapshot:** versioned binary or JSON envelope containing only authoritative state.
- **Replay:** scenario content hash, seed, ordered simulation commands, simulation/runtime
  compatibility ID, and state-hash checkpoints.
- **User settings:** separate camera/UI/audio preferences.

A snapshot records type/version, scenario/registry hashes, tick, RNG identity/state, payload
length/checksum, and authoritative state. Never save caches, render data, or metrics.

Save after a completed tick to a sibling temporary file; flush, validate, then replace. Failure
must leave the prior save loadable.

Prototype policy: exact schema compatibility and a non-destructive error for unsupported versions.
Define migrations in a separate decision before the first public save format.

Replay truth is scenario plus ordered commands; events are diagnostic. Gate 0 measures checkpoints.

SQLite is deferred until a persistent world/history exists. It remains viable because it provides
cross-platform files and atomic transactions
([SQLite](https://sqlite.org/appfileformat.html)).

## 8. Performance measurement standard

Performance reports must name:

- CPU/RAM/GPU/OS/power mode, engine/runtime, and release profile
- Scenario hash, map, agents, active attacks, tick rate, and speed multiplier
- Warm-up/measured ticks, run count, soak duration, and p50/p95/p99/max
- Stage/frame timings, working set, allocation/tick, candidates, and draw submissions

Profile release builds on target hardware. Editor measurements are exploratory only
([Unity profiling guidance](https://docs.unity3d.com/2022.2/Documentation/Manual/profiler-profiling-applications.html)).
Render dots in batches, never one engine node/object per agent. Godot documents per-node
housekeeping costs and MultiMesh-style batching for large instance counts
([CPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html),
[MultiMesh](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html)).

### Initial Gate 0 workload matrix

These durations and repetitions are a starting measurement protocol, not permanent product
standards. Gate 0 records the reference hardware and replaces any value that is too short to
produce stable percentiles or unnecessarily expensive for routine verification.

| Workload | Purpose | Required report |
|---|---|---|
| `duel-200` | 100 vs 100 in immediate engagement | 500 warm-up + 10,000 measured ticks at 1x and 4x requested speed |
| `duel-500` | 250 vs 250 stress | Same tick report; result may fail the first acceptance budget but must identify the limiting stage |
| `spread-500` | Worst broad-phase perception without immediate combat | Candidate counts and spatial-stage distribution |
| `render-500` | All dots visible and moving at 1080p | Five-minute p95/p99 frame time and submission count |
| `soak-200` | Growth/leak and determinism | At least 30 simulated minutes across 20 fixed seeds |
| `save-500` | Persistence cost | Ten save/load/cache-rebuild runs with p50/p95 and state-hash equivalence |

### Provisional hypotheses to replace after Gate 0

On the named reference machine, test whether:

- `duel-200` at the candidate 20 Hz tick has p95 tick time <= 20 ms and no normal tick > 50 ms.
- `render-500` has p95 frame time <= 16.7 ms at 1080p.
- 4x `duel-200` sustains 80 logical ticks per wall-clock second headlessly.
- Whole-process working set stabilizes below 512 MiB during `soak-200`.
- `save-500` p95 save and load each complete below one second.

These are unsourced planning hypotheses. Gate 0 replaces them using actual target hardware.
Allocation, cache, draw, and checkpoint budgets come from the baseline plus explicit headroom.

Optimizations need same-workload before/after data and identical hashes. A >10% p95 tick-time or
working-set regression requires review.

## 9. Correctness and testing

### Invariants after every tick

- Every live entity has one identity and valid faction.
- Dead entities never move, target, attack, or retreat.
- Position, hp, and cooldown remain within validated bounds.
- Each accepted attack applies damage once.
- Simultaneous lethal attacks resolve before victory, so mutual kills are possible.
- `BattleEnded` emits once, after same-tick damage/death resolution.
- Empty-cache and rebuilt-cache runs match warm-cache state hashes.
- Save/load plus one tick equals uninterrupted execution.
- Same scenario, build, ruleset, and commands produce the same hashes, winner, and ordered events.

### Required tests

- Unit: numeric rounding/bounds, distance, target tie-breaks, intent transitions, cooldown, damage,
  simultaneous death, retreat threshold, victory, scenario validation, RNG test vectors.
- Reference: compare grid queries against naive all-pairs queries on generated small worlds.
- Property: generated bounded scenarios preserve invariants and serialization round-trips preserve
  authoritative hashes.
- Golden replay: small named scenarios for ties, retreat, exact range, cooldown boundary, mutual
  kill, empty faction, and save/resume.
- Persistence: truncated file, bad checksum, unknown schema, interrupted write, and cache rebuild.
- Soak: fixed seeds, invariant checks, no unbounded memory/cache/event growth.

Require cross-platform CI only when cross-platform replay equality is promised. Until then,
identify the supported platform/build.

## 10. Feature and reviewer acceptance

Every feature proposal states:

1. User-visible outcome
2. Tick stage and state read/written
3. Numeric units/bounds and same-tick conflict rule
4. Total ordering and random-stream policy
5. Cache source/invalidation or “no cache”
6. Save/event/version effect or “presentation only”
7. Worst-case complexity and benchmark workload
8. Spectator explanation: reason code, event, or inspector field
9. Tests that fail before implementation and pass afterward

A feature passes only with same-seed repeat, invariants, golden replays, relevant save/resume,
cold-cache equivalence, the 200-agent contract, and a reported 500-agent result.

### Reviewer checklist

**Architecture**

- [ ] Simulation runs headlessly and owns all gameplay truth.
- [ ] Renderer/UI/wall clock cannot change rules.
- [ ] No deferred feature or general abstraction was added without an accepted need.
- [ ] Dependencies and versions are justified and pinned.

**Determinism and logic**

- [ ] Fixed tick stage and every multi-item total order are explicit.
- [ ] Hash iteration, thread timing, engine callbacks, and wall time cannot decide outcomes.
- [ ] Numeric rounding/overflow and RNG algorithm/streams have tests.
- [ ] Invalid/dead references, threshold boundaries, and simultaneous actions are covered.
- [ ] The change exposes an inspectable reason for autonomous behavior.

**Data and persistence**

- [ ] Every field is authoritative, immutable, derived, or presentation.
- [ ] Derived caches are excluded from saves and rebuild without drift.
- [ ] New persisted/event fields declare units, defaults, validation, and compatibility behavior.
- [ ] A failed save preserves the previous valid file.

**Performance**

- [ ] Report identifies hardware, workload, tick rate, speed, percentiles, and duration.
- [ ] Hot-loop allocation and growth are measured.
- [ ] Dots are batched and agent processing uses contiguous data where practical.
- [ ] Before/after hashes are identical.

**Tests**

- [ ] Tests assert outcomes/invariants, not incidental implementation.
- [ ] Optimized spatial logic matches a naive reference.
- [ ] Reproduction records scenario seed/hash, ruleset, build, and first mismatch tick.

## 11. Future pathfinding acceptance gate

Pathfinding begins only after the open-arena proof and must add:

- A deterministic graph/grid representation with stable neighbor and priority-queue tie-breaks
- Walkability/version data as authoritative state; path results as derived caches
- Cache invalidation for changed cells, doors, costs, and agent movement constraints
- Tests for shortest valid path against a reference solver, unreachable goals, equal-cost ties,
  narrow passages, dynamic blockage, retreat paths, and save/load rebuild
- A fixed benchmark matrix naming map density, changed-cell count, concurrent seekers, query
  distance, replanning rate, target hardware, and p50/p95/p99 query and tick-stage time
- A fairness invariant: agents cannot starve indefinitely because of queue order
- Same-seed and cold-cache path hashes identical across repeated runs

Adopt budgets after measuring the reference solver and representative maps. Any time-slicing is
explicit, deterministic, and visible; it may not silently hide missed budgets.

## 12. Milestone gates

1. **Gate 0 — workload and determinism spike:** choose language/engine, tick/numeric/RNG policy,
   target hardware, and measured budgets; repeat 10,000 ticks identically.
2. **Gate 1 — autonomous combat:** two factions, 200 dots, grid perception, targeting, approach,
   hitscan attack, damage, low-health retreat, death, winner, ordered events.
3. **Gate 2 — spectator shell:** batched rendering, camera, pause/speed/reset, selection, intent
   inspector, visible winner.
4. **Gate 3 — durable match:** scenario, snapshot, replay verification, atomic replacement, and
   save/resume equivalence; report 500-agent stress.

Only then plan terrain/pathfinding or another deferred layer.

## 13. Collision and formation contract

This section records the shipped body-contact rule. It is the game-rule statement of the approved
decision record at
[docs/decisions/2026-07-27-collision-policy.md](docs/decisions/2026-07-27-collision-policy.md),
which remains the authority on why each value was chosen and which alternatives were rejected.

### Historical boundary

**The collision policy is a game-design invention. It is not a historical claim.** No value in this
section is a measurement, and nothing here may be cited as a documented property of pre-colonial
Philippine warfare. The supporting research establishes only that frontage was constrained, spacing
was irregular, cooperation was local, and close contact was crowded. It does not establish named
formations, exact ranks, fixed body spacing, or any particular collision solver.

The warning in `docs/research/FORMATION_AND_COLLISION_MECHANICS.md` against **named or slot-based
formations remains in force.** Agents are never assigned to a rank, a file, a slot, or a named
formation. Whatever shape a battle line takes is an emergent consequence of individual movement
intent meeting the contact rule below, and it must never be reported to a player as a historically
attested arrangement.

### The rule

Every living agent is an impenetrable disc. There is exactly one radius, shared by every agent,
held on the immutable `Scenario` rather than duplicated per agent.

| Item | Raw value | World units |
|---|---|---|
| `BodyRadiusRaw` (common to all agents) | `4352` | 4.25 |
| Body diameter, `2 * BodyRadiusRaw` | `8704` | 8.5 |
| `AttackRangeRaw` (default) | `12288` | 12 |
| `MovementSpeedRaw` (default) | `3072` | 3 |

**Tangent contact is clearance, not collision.** Two bodies exactly touching is an accepted resting
position. This is what lets a packed line settle at a stable spacing instead of jittering by one
raw unit forever, and it is why overlap is a strict comparison rather than an inclusive one.

The authoritative invariant, evaluated after every tick in checked `long` arithmetic on raw
fixed-point coordinates:

```text
for every ordered pair of living agents (a, b) with a.EntityId < b.EntityId:
    (bx - ax)^2 + (by - ay)^2  >=  (2 * BodyRadiusRaw)^2
```

Penetration is never permitted between two living agents, in any amount, at the end of any tick.
Exactly one policy value exists, `CollisionPolicy.Solid`, and `Scenario.Validate` rejects any
other. Soft compression and faction-dependent contact were considered and rejected; the resolver
must not grow a code path, enum value, or configuration field that selects a different behaviour.
Adding a second policy value is a new decision record, not an implementation detail.

`PawnRenderer` size stays cosmetic. Presentation may draw a pawn larger or smaller than four world
units without affecting the simulation.

### Attack reach stays centre-to-centre

Attack eligibility compares the squared distance between agent **centres** against `AttackRangeRaw`
squared. No surface-gap subtraction is introduced anywhere. Because the body diameter is strictly
less than the attack range, two agents pressed into contact are always inside reach with four world
units of slack, so a packed front deals damage rather than deadlocking. That same slack is what lets
the rank immediately behind a pressed rank strike past it.

`Scenario.Validate` rejects any configuration where `2 * BodyRadiusRaw > AttackRangeRaw`, because
that combination produces bodies that can never reach each other. Intent selection and attack
gathering call one shared reach helper so the two stages cannot disagree.

The slack governs **reach only**. It does not decide where an advancing agent stops; that is the
approach target below.

### Agents advance until their bodies meet

An advancing agent closes until its body meets its target's body, not merely until its weapon can
reach. `BuildMovementProposal` subtracts `2 * BodyRadiusRaw` from the centre-to-centre distance, so
the movement target is **body contact at one body diameter**, currently 8.5 world units, not attack
range at twelve. An agent already inside reach keeps walking in.

This is what makes opposing front ranks touch. An earlier rule stopped an agent as soon as its
target was inside `AttackRangeRaw`, which left the whole difference between attack range and body
diameter as permanent air between the two front ranks for the whole engagement — four world units at
the four-world-unit body radius in force when that rule was replaced, 3.5 at today's radius — so cross-faction bodies never met and the collision stage only
ever observed allies queueing behind their own line. Attack resolution was not changed by this and
is still centre-to-centre at `AttackRangeRaw`.

**`AgentIntent.Attacking` means the agent has arrived.** Intent selection marks an agent `Attacking`
only when its squared distance to its target is at or inside the contact distance; an agent that is
still closing is `Moving` even when it is already inside weapon reach. An agent that lands a blow
while still closing is re-marked `Attacking` by attack gathering in the same tick, so a spectator
reading the inspector still sees a fighting agent rather than a marching one. The two stages do not
overlap: intent selection describes arrival, attack gathering describes striking.

### Interaction matrix

| Pair | Behaviour |
|---|---|
| Living agent — living ally | Solid. Zero overlap. Identical to the enemy rule. |
| Living agent — living enemy | Solid. Zero overlap. |
| Living agent — corpse (`HitPoints == 0`) | No collision. Corpses are walked over. |
| Corpse — corpse | No collision. Dead agents never move and never block. |
| Living agent — map boundary | Hard clamp of the centre to `[BodyRadiusRaw, dimensionRaw - BodyRadiusRaw]` on each axis independently. |
| Corpse — map boundary | Not applicable; corpses do not move. |

Allies and enemies deliberately share one rule, because a faction-dependent matrix doubles the
regression surface for a feel benefit that has not been demonstrated. Corpses are non-colliding so
that a killing field cannot accumulate permanent immovable obstacles, which is the most likely
source of a late-battle stall. A living agent may finish a tick with its centre exactly on top of a
corpse; this is intentional and is not reported to the spectator.

### Tick stage order

The battle simulation executes these stages, in this order, on every tick:

```text
DecrementCooldowns
SelectTargetsAndIntents
ResolveContingentStates      // no-op under IndependentPursuitV1
GatherMovementProposals      // reads tick-start positions only
ResolveCollisions            // rebuilds the grid, validates candidates
CommitMovement               // single commit, emits Move events
MeasureCollision             // pure observation, writes no agent state
GatherAndCommitAttacks       // reads resolved positions
ResolveOutcome
```

`ResolveContingentStates` returns on its first line under
`MovementPresetId.IndependentPursuitV1`, so that preset's tick pipeline is
unchanged in effect even though the stage now always runs. Under every
persistent-contingent preset it reads each living agent's
position, `FactionId`, `ContingentId`, and selected `TargetEntityId`, plus
`Scenario`'s map dimensions and body radius, to compute — once per contingent
per tick, into preallocated per-slot arrays sized at construction — each
living contingent's leader, living member count, member spread around that
leader, count of members whose selected target lies inside the close radius,
trail-base geometry, and the two geometric
gates (map-edge fit and same-faction square overlap) design section 3.5 of
`docs/plans/2026-07-28-formation-movement-realism-design.md` names gates 5 and
6. It then resolves each living contingent's `ContingentState` through the
six-priority-ordered transition table and writes that state onto every one of
the contingent's living members. The per-slot arrays are working buffers
recomputed from scratch every tick; only the per-agent `ContingentState` write
is authoritative state, and it is what `StateHasher.Compute` observes.

`MeasureCollision` derives this tick's counters from committed positions. It writes no agent state
and nothing it produces is hashed.

### Priority and the candidate ladder

Movers are resolved in **ascending `CollisionPriority` key**, which is
`(Fnv1a(tag, seed, tick, entityId) >> 32) << 32 | entityId`. Once an agent's position is committed
for the tick, later movers treat it as an obstacle, so a lower key wins a contested destination.
The key is a pure hash rather than a draw from any stream, it is recomputed every tick, and its low
half is the entity ID, so the order is strict and total and ties still break on stable `EntityId`.
This is an explicit, documented priority rather than an accident of iteration order.

It replaced a plain ascending-`EntityId` order on 2026-07-27. Faction 0 holds the low IDs, so that
order handed it every cross-faction contest of every battle, and once the mirrored starting
deployment removed the spawn noise that had masked it, one faction won 19 of 20 seeds. See section
9 of [docs/decisions/2026-07-27-collision-policy.md](docs/decisions/2026-07-27-collision-policy.md).

No separate anti-stall or fairness escape rule is added, because being blocked does not remove an
agent from combat: contact happens at eight world units while attack reach is twelve, so a blocked
agent is still attacking. `TickLimit` remains the terminal backstop.

For each mover, the first candidate that satisfies the zero-overlap invariant against all committed
positions and the boundary rule is taken. Candidates are evaluated in this fixed order:

1. The preferred destination at full step. Accepting this reports `Moved`.
2. X-axis-only slide: preferred X, tick-start Y. Reports `Slid`.
3. Y-axis-only slide: tick-start X, preferred Y. Reports `Slid`.
4. A truncation ladder along the preferred direction at lengths `m >> 1, m >> 2, ...` down to and
   including `1`, skipping zero lengths, where `m` is the preferred movement length. Reports
   `Truncated`.
5. Hold the tick-start position. Reports `Blocked`.

Rounding for every truncated candidate uses the existing integer division formulation
`delta * length / distance`, which truncates toward zero. Odd remainders are discarded, never
redistributed; there is nothing to split between two agents because the solid resolver moves one
agent at a time.

Collision resolution may only **reduce** displacement, never add any. Tunneling and path swapping
are forbidden and are made geometrically impossible rather than tested for at run time:
`Scenario.Validate` enforces `MovementSpeedRaw <= BodyRadiusRaw`, so two agents closing head-on
cover at most `2 * MovementSpeedRaw` in a tick, strictly less than the diameter they would have to
cross to swap sides. Swept-disc geometry is therefore out of scope; the resolver performs static
disc-overlap tests only.

There is one exemption from the displacement budget. Two living agents can share a centre only
through a test constructor or an unresolved spawn, never through normal ticking. When that is
detected, the agent with the **higher `EntityId`** is displaced by exactly `2 * BodyRadiusRaw` in
the first legal direction of the fixed order `+X, -X, +Y, -Y`, and reports `Separated`. If no
direction is legal the displacement is skipped and the agent reports `Blocked` rather than
throwing. This is a repair of an invalid input state, applies at most once per agent per tick, and
cannot oscillate.

### The uniform grid is a derived oracle

The collision broad phase is a uniform grid over living bodies, rebuilt each tick. It is a derived
accelerator and never authoritative state: it is not hashed, not snapshotted, and not persisted.

Its only contract is that it produces exactly what an O(n²) scan over the same bodies would
produce, in exactly one order. That equivalence is the acceptance test, in keeping with the
reference-test requirement in section 9. Determinism rests on three properties: occupied cells are
visited in ascending cell Y then ascending cell X, obtained by sorting packed cell keys rather than
by enumerating a dictionary; each cell's neighbourhood is scanned in one fixed three-by-three
offset order; and every emitted pair is normalised to (lower entity identifier, higher entity
identifier) before the finished list is sorted. Dead bodies are filtered out before the grid is
built, so a battlefield full of corpses costs nothing here.

All grid, pair, proposal, and resolution storage is preallocated and reused, growing only when
capacity is insufficient, so a warm collision tick allocates nothing.

### Contact is measured over a proximity band

A solid resolver guarantees that every living pair ends the tick at or beyond `(2R)^2`. "Touching"
would therefore mean a squared distance of **exactly** `(2R)^2`, which on an integer lattice requires
a Pythagorean coincidence between the two axis deltas and the diameter, and is unreachable in
practice. An exact-tangency counter can essentially never fire, whatever the agents are doing, which
is why the first gated run reported zero contact pairs.

Contact metrics therefore use a proximity band of `BodyRadiusRaw + (MovementSpeedRaw / 2)` per body:
a pair counts as in contact when the two bodies are within one movement step of touching. At the
default values that is `6144` raw units per body, so the broad phase pairs bodies whose centres are
within `12288` raw units. This is the honest reading of "pressed together" for a spectator, and it is
stable against the one-raw-unit rounding that truncating integer division produces.

The band is **derived observability only**. No rule consults it: the resolver's own legality tests
use the exact `2 * BodyRadiusRaw` contact distance, unchanged. The band is never hashed, never
snapshotted, and never persisted, and introducing it left both the state hash and the event hash
byte-identical, which is the evidence that it stayed on the derived side of the line.

### Spawn and density

`BattleSimulation.Create` resolves spawn overlaps deterministically. Agents are placed in ascending
`EntityId`; when a generated position overlaps an already placed body or violates the boundary
rule, candidate positions are scanned in fixed ring order around it, at ring radius
`r * 2 * BodyRadiusRaw` for `r = 1, 2, 3, ...`, enumerating the eight compass offsets per ring in
the order `+X, +X+Y, +Y, -X+Y, -X, -X-Y, -Y, +X-Y`. The first legal candidate is taken. The random
stream is not consulted during relocation, so relocation cannot shift the seed sequence. If the
scan exhausts its bound, `Create` throws and names the entity that could not be placed; it never
returns a simulation with overlapping bodies.

Impossible density fails loudly at validation. The conservative square-packing bound is stated
algebraically as `TotalAgents * (2 * BodyRadiusRaw)^2 > mapWidthRaw * mapHeightRaw`, but **that
expression must not be evaluated literally** because at the maximum map dimension the left side
runs far past `long.MaxValue`. The implementation uses the equivalent division form, which is exact
rather than approximate for a positive body area. The map-fit checks run first; that ordering is
load-bearing, because they are what bounds the body area enough for the remaining products to be
safe. Boundary equality is accepted, so only a strictly greater agent count is rejected.

### Hashing, persistence, and observability

These authoritative fields enter the state hash:

- `Scenario.BodyRadiusRaw`
- `Scenario.CollisionPolicy`, as its integer value
- per-agent `MovementResolution`, as its integer value

`MovementResolution` is the spectator explanation required by section 10 item 8. It is authoritative
simulation state written by the collision stage, exposed through `AgentView`, and rendered as a
label in the agent inspector: `Moving`, `Crowded`, `Sliding`, `Blocked`, `Pushed apart`, with
`None` rendering nothing. Its numeric values are pinned. No collision `BattleEvent` kind is added,
because a packed 200-agent front would emit thousands of contacts per tick into a feed that retains
200 events.

The uniform grid, the pair and proposal buffers, and the aggregate collision counters are all
derived. They are never hashed, never snapshotted, and never persisted. `BattleSnapshot` stays a
completed-tick render snapshot; collision configuration remains reachable through
`BattleSimulation.Scenario`.

Because `BodyRadiusRaw`, `CollisionPolicy`, and `MovementResolution` all reach the state hash, and
because constraining movement changes where agents stand, both the state hash and the event hash
moved for every seed when this contract shipped. Changing any of those three fields in future
invalidates every recorded golden expectation and requires a deliberate rebaseline, recorded in the
same commit as the change.

Combat preset versioning does not and cannot cover this. A preset version identifies combat
content — the weapon roster, the attribute profiles, the target weight tables, the clash tables —
through `CombatRuleset.ContentHash`, and none of `BodyRadiusRaw`, `CollisionPolicy`, or
`MovementResolution` feeds that hash: they are `Scenario` fields with defaults supplied by
`CollisionRules`, not preset fields. Cutting a new preset version whose combat content is
unchanged would create the appearance of protection while providing none, because an old replay
naming the unchanged preset would still be replayed under the new collision defaults. The
obligation this section imposes is the rebaseline above, not a preset bump.

Both hashes moved a second time when the approach target changed from attack range to body contact,
because that changes where agents stand. Introducing the contact-metric proximity band moved neither,
because it is derived. The current recorded oracle is in
[docs/development/testing.md](docs/development/testing.md).

### What the rule actually produces

Opposing bodies meet. The two front ranks close all the way rather than halting with air in front
of them.

Alongside that, allies still **queue behind their own front line**: a rear agent trying to advance
into space its own front rank already occupies is refused, holds position, and reports `Blocked`.
That queueing is what constrains frontage and produces a visible line.

**Superseded, pending re-measurement.** The contact-pair and blocked-agent-tick figures below were
recorded against the four-world-unit `BodyRadiusRaw` (`CollisionRules.DefaultBodyRadiusRaw`), before
it moved to 4.25 world units (task C1, `docs/plans/2026-07-28-collision-report-and-shell.md`). The
larger body changes which candidates are legal and how often contact and blocking fire, so these
counts no longer describe the shipped default and must be re-measured against a real run before they
are restated as fact: on the 200-agent, seed-1 acceptance workload the run recorded **5,649
cross-faction contact pairs** against 57,295 candidate pairs, and the 500-agent report-only workload
recorded 14,270 against 280,675; queueing roughly doubled once agents began closing to contact —
14,544 blocked agent-ticks at 200 agents, up from 7,154 under the earlier stop-at-reach rule.

Deepest living-body penetration remained exactly `0` on both workloads, before and after the change
to the approach target. The solid-disc invariant is not affected by where agents choose to stop, and
this property is independent of the superseded figures above.

The recorded figures for both workloads are in
[docs/development/testing.md](docs/development/testing.md). Anyone tuning the contact model later
should start from the fact that the binding constraint on the battle line is now the body diameter,
while attack reach decides who can strike — the two are deliberately different distances.

### Last-stand formation

This subsection records the shipped last-stand rally behaviour. It is the game-rule statement of
the approved design at
[docs/plans/2026-07-27-last-stand-formation-design.md](docs/plans/2026-07-27-last-stand-formation-design.md),
which remains the authority on the numeric derivation and the rejected alternatives.

**This is a game-design invention, not a historical claim, and the prohibition on named or
slot-based formations stated above applies to it without exception.** No source documents a rally
radius, a formation headcount, or a formation shape for this period and region. The only
player-facing word for this behaviour is `Regrouping`, a plain English descriptor in the same
spirit as `Great Blade` for a weapon whose specific cultural identification stays provisional
metadata. No cultural or foreign formation name may ever appear in a player-facing string for it.

A faction enters its last stand, independently of the other faction, on any tick where its living
count is at or below `Scenario.LastStandThresholdAgents` and that value is greater than zero. Hit
points are only ever written downward, in exactly one place, so the trigger is monotone and cannot
flap once armed: a faction's living count never rises inside a battle, so once the threshold is
crossed it stays crossed. A threshold of `0` — the property's own default — disables the feature
entirely; `Scenario.CreateDefault` applies `FormationRules.DefaultLastStandThresholdAgents` (`6`),
and `Scenario.Validate` rejects any threshold above `FormationRules.MaximumLastStandThresholdAgents`
(`9`).

Each faction's rally agent is its living warrior with the lowest `EntityId` — a total order over a
finite, unique set that needs no tie-break. It is recomputed by a single forward scan at the top of
`SelectTargetsAndIntents` every tick and compared explicitly on `EntityId` rather than on array
order, so a permuted agent array yields the same rally agent. The rally agent is exempt from the
formation: it keeps its ordinary nearest-enemy targeting and is never `Regrouping`. Every other
living warrior of a faction that has entered its last stand, whose selected target is not already
within body-contact distance, is marked `AgentIntent.Regrouping` and aims at a point derived from
the rally agent's position instead of at its own enemy.

A follower's aim point is not the rally agent's own position. It trails
`FormationRules.RallyTrailRadiusMultiplier` body radii (`12`) behind the rally agent, opposite the
rally agent's direction of travel, and then adds a fixed per-follower jitter offset drawn
independently on each axis from `[-J, +J]`, where `J` is
`FormationRules.RallyJitterRadiusMultiplier` (`6`) times the body radius. A follower already
standing inside its leader's forward corridor — within `FormationRules.RallyCorridorHalfWidthMultiplier`
(`2`) body radii of the leader's line of travel, and ahead of it — gives way sideways instead of
walking back through the leader to reach its trail point, clearing the corridor by a body radius
and leaving its forward position unchanged. The trail rule and the give-way rule together are what
keep the formation live: a rally agent is exempt from the *formation* but not from *bodies*, and
without both rules a follower or the leader itself can become permanently blocked, running the
battle to the tick limit with no casualties on either side.

The jitter offset is a personal constant for the whole battle, computed by a dedicated
deterministic stream rather than stored on any agent. `Fnv1a(LastStandTag, Seed, EntityId)` seeds a
fresh `SplitMix64`, where `LastStandTag` is the 64-bit constant `0x484B424F5F4C5354`. This satisfies
the `(match_seed, system_tag, entity_id)` random-stream requirement in section 4 above, and the key
deliberately excludes the tick: a tick-keyed offset would move every warrior's aim point every tick
and reproduce the jitter-and-stall failure the steering research already warns against. The draw
uses its own fresh generator instance and never advances the spawn generator, so enabling the
feature cannot shift spawn placement or hit-location resolution for any seed.

`AgentIntent.Regrouping = 4` is the primary spectator channel, appended after `Dead` because the
append-only rule for hashed enum values forbids reordering, not because it is conceptually
terminal. It is authoritative simulation state, written by the intent stage and read by the
existing agent inspector through the enum's own `ToString()`, so `Intent: Regrouping` appears with
no `Hukbo.Client` change. A regrouping warrior's `Move` event also names its rally agent in the
event's target field rather than an enemy, so the battle event log reads the rally rather than a
duel. Attack eligibility is unchanged by any of this: it is decided entirely by cooldown and
centre-to-centre range, so a regrouping warrior that passes an enemy inside reach still strikes it
and is re-marked `Attacking` in the same tick.

Both the state hash and the event hash move whenever a last stand is active. `Scenario.LastStandThresholdAgents`
enters the scenario block of `StateHasher.Compute`, `AgentIntent.Regrouping` enters the state hash
through the existing per-agent `Intent` write, and regrouping survivors stand in different places
than they would under ordinary targeting. The current recorded oracle is in
[docs/development/testing.md](docs/development/testing.md).

## 14. Defensive resolution contract

This section records the shipped weapon-clash mechanic: the step that decides whether an accepted
attack actually lands. It is the game-rule statement of the decisions recorded in
[docs/plans/2026-07-27-clash-preset-v2-integration-design.md](docs/plans/2026-07-27-clash-preset-v2-integration-design.md),
which remains the authority on why each value was chosen and which alternatives were rejected. This
section was dropped from the standards document when the original weapon-clash plan was superseded
by the preset-V2 integration; it is restored here rather than left missing, because the mechanic it
describes is shipped and authoritative.

### Tick stage

Defensive resolution runs inside `GatherAndCommitAttacks`, immediately after an attack has passed the
reach and cooldown gates and after `HitLocationResolver.Resolve` has chosen the struck body part, and
before damage is applied. An attack that fails the reach or cooldown gate never reaches this stage at
all; only an **accepted** attack is resolved against the clash profile.

### The five outcomes

`AttackResolution` is a five-member enum with pinned numeric values, appended-only per the section 4
enum-value rule:

| Value | Name | Meaning |
|---|---|---|
| `0` | `Landed` | The blow struck as gathered; damage applies. |
| `1` | `ShieldBlocked` | The defender's shield took the blow. |
| `2` | `Parried` | The defender's weapon arrested the blow (the hard share of the weapon channel). |
| `3` | `Deflected` | The defender's weapon brushed the blow aside (the soft share of the weapon channel). |
| `4` | `Evaded` | The defender stepped off the line entirely; the blow met empty air. |

Only `Landed` applies damage. The other four are mutually exclusive, jointly exhaustive alternatives
to a landed blow, never summed on top of a separate base probability.

### The `HKBO_CLS` domain tag

`ClashResolver.MixClash` derives a stateless keyed roll from an FNV-1a fold tagged with the ASCII
constant `HKBO_CLS` (`0x484B424F5F434C53`), folding, in order: the domain tag, the seed, the tick,
the source entity ID, the target entity ID, the attacking weapon, the defending weapon, and the
defending shield. This is the same construction `HitLocationResolver` uses under its own `HKBO_HIT`
tag, over an overlapping input tuple; the distinct tags are what keep the two rolls independent
rather than correlated draws off the same stream. Neither draws from `SplitMix64` or any other
shared generator, so adding this stage shifts no pre-existing deterministic behaviour — proven by the
zero-interception control run, which reproduces the pre-change event stream and state hash tick for
tick when every clash channel is held at zero.

Every domain tag in the simulation is a fresh, distinct 64-bit ASCII constant folded first into its
own keyed roll, precisely so that unrelated draws never correlate: `HKBO_CLS` above, `HKBO_HIT` for
`HitLocationResolver`, the last-stand jitter's `LastStandTag` (`0x484B424F5F4C5354`), the
collision-priority key's own tag, and — newest — `HKBO_CTG` (`0x484B424F5F435447`), which
`ContingentOffset.Compute` folds with the seed and entity ID, excluding the tick, to draw each
persistent contingent's per-member cohesion-square jitter offset. Reusing an existing tag for a new
roll would correlate the two draws off the same stream; this paragraph is the inventory a new domain
tag is checked against before it is minted.

### The composition rule

The roll walks a fixed five-way cumulative interval in this order: shield, hard (parry), soft
(deflect), void, landed. Each channel's width is basis points out of `ClashProfile.BasisPointScale`
(10,000). The shield, weapon, and void channels are resolved from `ClashProfile`, keyed by
`(defending weapon, defending shield, attacking weapon)` for the weapon channel and
`(defending weapon, defending shield)` for the void channel; the weapon channel is then split into
its hard and soft halves by a per-weapon hard-share base and multiplier, keyed by weapon alone. If the
summed shield, weapon, and void channels exceed `MaximumInterceptionBasisPoints`, all three are
rescaled proportionally; the residue left by truncation becomes additional `Landed` probability.
Every comparison in the interval walk is strictly lower-exclusive, so a zero-width channel — a
shieldless defender's shield interval, in particular — is stepped over rather than selected.

### The single enforced acceptance band

The defence-attributable share — `(ShieldBlocked + Parried + Deflected + Evaded) / AcceptedAttacks`,
exposed as `CombatMetrics.DefenceAttributableShare` — is a gate, not a report, on preset V2's shipped
tables: it must land inside 0.25 to 0.45 across seeds 1 through 20 at 200 agents. No other acceptance
band on the individual channel values is enforced; the tables may be retuned freely within their
declared per-cell bands as long as the aggregate share and the termination criterion below both hold.

### The termination criterion

At least 19 of 20 seeds must reach a decisive outcome before the 5,000-tick cap, with a median
decisive tick at or below 5,000. Preset V2's shipped tables satisfy both the share band and the
termination criterion; the recorded figures are in
[docs/development/testing.md](docs/development/testing.md).

### The hashed fields

`AttackResolution` packs into bits 24 through 26 of `BattleEvent`'s combined `_combatContext` `int`
(`ResolutionShift = 24`), alongside `Weapon` (bits 16-23), `Shield` (bits 8-15), and `HitLocation`
(bits 0-7). `Landed = 0` contributes nothing to the resolution byte, which is safe only because the
weapon field is non-zero for every attack event and "absent" is tested on the whole field, not on any
one byte — a pinned test guards this reasoning. The event stays at 72 bytes and the collision
allocation ceiling stays at 900,000. `ClashProfile`'s entire tuning surface — the weapon-intercept
matrix keyed by all three key parts, the shield scalar, the void channel, the hard-share rows, and the
clamp bounds — folds into `CombatRuleset.ContentHash` conditionally: only a ruleset actually
constructed with a clash profile folds it, which is what keeps preset V1's pinned content hash
(`0x59FB4CA563D87A49`) unchanged. `CombatMetrics` reaches neither the state hash nor the event hash;
it is derived observability data only.

### Spectator channels

| Channel | `Landed` | `ShieldBlocked` | `Parried` | `Deflected` | `Evaded` |
| --- | --- | --- | --- | --- | --- |
| Event log line | damage line | "stopped by the shield" | "parried" | "turned aside" | "stepped off the line" |
| Blood spray | yes | suppressed | suppressed | suppressed | suppressed |
| Impact ring | yes | absent | absent | absent | absent |
| Clash cross | absent | yes | yes | yes | absent |
| Swing pose | stops on target | recoil | recoil | recoil | follows through |
| Sound cue | weapon impact | `clash-shield-<weapon>` | weapon impact | weapon impact | weapon impact |

A shield block now has a sound channel of its own. It is carried by four classless slots keyed to the
attacking weapon — `clash-shield-kampilan`, `clash-shield-wasay`, `clash-shield-kalis`, and
`clash-shield-itak` — and the matching slot replaces the weapon impact cue that a landed blow would
have played. `ShieldBlocked` is therefore the only one of the five resolutions with a cue of its
own; the other four still share the single weapon impact cue, as the `Sound cue` row above records.
The two remaining clash slots, `clash-blade-hard` and `clash-blade-soft`, are deferred by owner
decision and are not part of this contract.

`Evaded` is still the weakest case: distinguished by one positive channel, the event-log line, and
three absences. It has no sound channel of its own, because it plays the same weapon impact cue as
`Landed`, `Parried`, and `Deflected`, so the reason it is the weakest case is unchanged.

### Historical boundary

**Every value in this contract is a gameplay tuning choice, not a historical measurement.** The
weapon-intercept matrix's sixteen legacy cells and the ten cells added for the shieldless Kalis and
Itak loadouts are all labelled **Provisional reconstruction** in `PhilippineCombatPresetV2`'s own code
comments, naming the band each was drawn from. The shield channel is the only defensive channel with
any sixteenth-century documentary support — anchored only in direction, by documented shield use at
Mactan and Cole's 1922 account of angled deflection (**Documented, form uncertain**) — and its
magnitude of 2,400 basis points is invented and stays labelled as such. No value in this section may
be cited back into `docs/research/HISTORICAL_1500s_WEAPONS.md` or `WEAPON_CLASH_1500s.md` as a
measurement.

## 15. Performance technique inventory

This section is the durable record of
[docs/archives/2026-07-28/2026-07-28-arch-informed-performance-hardening-design.md](docs/archives/2026-07-28/2026-07-28-arch-informed-performance-hardening-design.md)'s
conclusions: which techniques an external research pass over the Arch entity-component-system
library found usable in Hukbo, which are usable only with a named discipline, and which are
forbidden and why. The design document carries the reasoning; this section carries the consequence,
so that a future contributor who reaches for a fast ECS does not have to re-read the design document
to know what already got decided, and does not re-derive the same argument from scratch — possibly
wrong.

### Arch is a reference implementation, not a dependency

The upstream reference baseline for this inventory is
[Arch 2.1.0](https://www.nuget.org/packages/Arch/2.1.0), reviewed on 2026-07-28
against its [official documentation](https://github.com/genaray/arch.docs) and
[tagged source](https://github.com/genaray/Arch/tree/v2.1.0). Revalidate claims
about Arch before changing this inventory to follow a later release.

The research pass read Arch's chunk layout, entity-location storage, query enumerators, command
buffer, lifecycle and capacity controls, build configuration, and benchmark harness. The objective
was to identify practices usable inside a deterministic, single-threaded, fixed-schema battle
simulation without adopting an ECS. **This repository does not adopt Arch, does not add an archetype
or chunk system, and takes no package dependency on Arch or any extension package.**

Hukbo copies a practice only when it solves a measured local problem and preserves the stronger
determinism rules in this document. It does not copy Arch's public API, dynamic component model,
runtime type registry, scheduler, or persistence format. A dense integer lookup, a `ref` accessor, or
a split data layout is a local implementation technique, not evidence that Hukbo is migrating toward
an ECS.

The required profiler evidence now exists in
[docs/research/TICK-STAGE-PROFILE.md](docs/research/TICK-STAGE-PROFILE.md). That profile found collision
resolution to be the dominant stage and closed the then-proposed dense identifier map, `AgentState`
layout change, and target-selection spatial acceleration. It did **not** justify Arch, archetypes, or
chunks. Formation and collision changes can alter that profile, so those stages must be remeasured
before reopening a closed layout decision. Importing Arch or another ECS would still require its own
current profile, design document, compatibility review, and deterministic-oracle benchmark.

### Custom entity and memory handling contract

These rules are the Arch-informed practices Hukbo actually follows:

- `BattleSimulation` owns authoritative entity state. A stable `EntityId` identifies an agent;
  an array index or physical storage slot is only an internal location and never breaks a gameplay
  tie.
- Authoritative iteration uses arrays or explicitly ordered collections. Hash containers are lookup
  aids only and never define update, resolution, event, snapshot, or hash order.
- Capacity is established from scenario size where possible. Hot-path scratch storage is reused,
  bounded by the active simulation, and reset by logical count rather than reallocated every tick.
  Growth must be explicit, overflow-safe, and covered by allocation tests.
- References, spans, and storage indexes are short-lived views. They may not escape the operation
  that obtained them or survive a resize, reset, removal, or slot move.
- Gameplay data may be updated in place during its pinned stage. Entity creation, destruction, or
  shape changes must be gathered and committed later in one deterministic phase. The current fixed
  roster has no recurring structural changes, so a general command buffer would add machinery without
  solving a present problem.
- Scratch buffers, lookup tables, grids, render projections, and metrics are derived state. They are
  rebuilt or cleared on reset and are excluded from snapshots and authoritative hashes.
- Specialised memory layouts, unsafe access, pooling, parallel proposal phases, and new lookup
  structures require measurements against the existing implementation plus the deterministic oracle.

Status as of 2026-07-28:

| Practice | Hukbo status | Evidence or remaining gate |
| --- | --- | --- |
| Stable identity separate from physical storage | Implemented | `EntityId` is the gameplay identity; ordered passes use stable identifiers for ties |
| Reused, capacity-aware transient storage | Implemented | Simulation scratch/event storage and collision buffers are reused; allocation regression tests guard steady-state ticks |
| Ordered iteration with lookup-only hashing | Implemented in behavior; one documentation gap remains | `_agentIndexes` is lookup-only and `AgentState[]` is iterated, but the pairing still needs the symbol-level XML comment required below |
| Deferred structural mutation | Satisfied by the fixed-roster model | No general entity-shape mutation occurs inside authoritative passes; add a deterministic gather/commit phase before introducing any |
| Data-oriented access patterns | Partially implemented | Stable ordered slots and preallocated stage/scratch buffers are present; `AgentState` remains a reference type, so packed component or structure-of-arrays locality is not implemented and remains measurement-gated |
| Dense identifier map, `AgentState` layout split, target spatial index | Measured and closed | Gate A found no qualifying bottleneck; reprofile after material formation or collision changes |
| Collision-stage optimisation | Separate active concern | The existing profile identifies collision as the dominant stage; that finding does not imply an ECS requirement |
| Parallel authoritative queries | Deliberately omitted | Machine-dependent partitioning and completion order conflict with the pinned single-threaded schedule |
| Arch package, archetypes, chunks, runtime component registry | Deliberately omitted | No measured need; fixed-schema Hukbo would assume new complexity and determinism risk |

### Portable techniques (allowlist, not an implementation plan)

The table below says whether a technique can be compatible with Hukbo. It does not say the technique is
implemented, currently beneficial, or authorized. A closed measurement gate takes precedence over an
entry in this allowlist.

| Technique | Compatible? | Gate or discipline required |
| --- | --- | --- |
| Structure-of-arrays with a cache-sized block | Yes | A profile must justify the layout change; block size comes from measured cache size; integer arithmetic only, never a float |
| Dense `int[]` index in place of a dictionary | Yes | A profile must justify the retained-memory trade; Gate A did not justify it |
| `ref` returns instead of indexers | Yes, once a value-type agent layout exists | Not authorized by this section; depends on the `AgentState` layout change the design document assesses separately and does not authorize |
| `MemoryMarshal.CreateSpan` / `Unsafe.Add` | Yes, once a value-type agent layout exists | Requires `AllowUnsafeBlocks`, which is absent from `Directory.Build.props` and needs its own justification, in addition to the same layout change |
| `[SkipLocalsInit]` on hot accessors | Yes, once a value-type agent layout exists | Same dependency as the two rows above |
| Dense identifier-to-location addressing by shift and mask | Yes | Power-of-two bucket size; `BitOperations.Log2`, never `Math.Log` or any other floating-point capacity arithmetic |
| Hash container for lookup only, ordered collection for iteration | Yes — already the local practice | The ordered collection must be the only thing enumerated, and that separation must be documented at the symbol; see the dedicated rule below |
| Bit-set signature matching for whole-group rejection | Yes | Fixed word order |
| `ref struct` enumerators | Yes | Cannot be boxed or captured — which is the entire point of using one |
| Struct callback as a generic type parameter instead of a delegate | Yes | Hand-written per tick stage; never generated |
| Deferred structural change through a command buffer | Yes | Fixed playback phase order, one ordered pass |
| Sparse sets for pending-flag membership | Yes | No hashing |
| Reverse iteration | Yes, with discipline | A descending order is still a total order, but the direction must be pinned by a test; an unpinned refactor can silently move a hash |
| Swap-remove | Yes, with discipline | Storage position must never break a tie; `EntityId` stays the sort key regardless of where an element physically sits |
| Pooled or uninitialised buffers | Yes, with discipline | Every slot must be written before it is read, and the invariant must be documented and tested at the symbol — `CollisionResolver.Grow`'s no-copy invariant is the existing model to follow |

The struct-callback row deserves a caveat beyond the table: its measured advantage over a delegate
comes from the JIT devirtualising and inlining a struct's method call, not from any source generator.
Hukbo has eight fixed tick stages, not an open-ended set of user-authored systems, so writing the
shape by hand where it helps is preferable to adding a source-generator dependency, which would bring
a build dependency, a generated-code review surface, and a new class of golden file to maintain.

### The lookup-only-hash-container rule

A hash container — a `Dictionary`, a `HashSet`, or any structure whose enumeration order is not
part of its contract — may be used inside `Hukbo.Core` for lookup only. Whatever is actually iterated
over must be a separate, ordered collection, and the separation between the two must be documented in
an XML doc comment at the symbol that owns both. This states, as a positive construction rule rather
than only as a prohibition, what section 4's existing determinism contract already implies: hash-set
and dictionary iteration order may not affect gameplay. The rule here asks for more than avoiding a
violation after the fact — it asks that the lookup-only structure and the ordered structure it defers
to be built as a declared pair from the start.

The repository already practices this in two places, one of which documents it and one of which does
not yet. `CollisionUniformGrid` keys its cell lookup by a packed integer while the pairs it produces
come from a separately maintained, ordered list, and the ownership and iteration contract is written
down at the symbol. `BattleSimulation._agentIndexes` is a `Dictionary<ulong, int>` used for
identifier-to-slot lookup only and is never enumerated; the `AgentState[]` array it indexes into is the
thing actually iterated, in storage order. Both are legal because neither hash structure is
enumerated, but only the grid currently states the pairing at the symbol. This is a documentation
conformance gap, not a reason to replace the dictionary. Any future lookup-only hash container must
state and preserve the same separation rather than leaving it as an implicit accident of current call
sites.

### Techniques deliberately not ported

| Technique | Why it is forbidden here |
| --- | --- |
| `World.ParallelQuery`, `JobScheduler`, `[Query(Parallel = true)]` | The partitioner that splits the work does so by processor count, which makes the split machine-dependent, and the resulting chunks complete in an arbitrary order. Arch's own documentation states that a parallel query must not be called from anything but the main thread. This is non-negotiable against the single-threaded authoritative schedule the determinism contract requires |
| Runtime component-identifier assignment | Arch's component registry hands out identifiers from an incrementing counter the first time a type is touched, via a static constructor, so the identifier a type receives depends on which type the runtime happens to touch first — and those identifiers feed the archetype signature hash. Any identifier-per-type registry adopted here would have to draw from an explicit, committed, ordered table instead, versioned exactly like an enum's numeric values already must be |
| `QueryDescription.Equals` by hash code only | Equality decided purely by a 32-bit hash mix means a hash collision reproducibly returns the wrong entity set. That is a determinism bug wearing the appearance of a logic bug, which is the worst kind to debug because nothing about the symptom points at the cause |
| `UnsafeArray`, `UnsafeList`, and other bounds-check-free collections | In a deterministic simulation an `IndexOutOfRangeException` is an asset, not a defect: it is a loud, reproducible failure at an exact tick that points straight at the bug. Removing the bounds check trades that loud failure for a silent out-of-bounds read, which converts a debuggable crash into a silent hash divergence discovered only much later, far from its cause |
| The archetype and chunk machinery | This machinery pays off when component composition is dynamic and diverse across many entity kinds. A fixed-schema two-faction battle simulation gets the cache-locality win from plain parallel arrays and pays none of the archetype-transition cost, because there is no composition change to transition between |
| `Arch.System.SourceGenerator` | The generator's measured win is the inlined struct-query call shape, and that shape is hand-writable for Hukbo's eight fixed tick stages without a code-generation dependency, so the generator buys nothing here that hand-written code does not already provide |
| `Arch.Persistence` | It has no version field and no magic number in its envelope, its layout is fully positional MessagePack, and its own documentation requires component registration order to match exactly across the save boundary with no mechanism to detect a mismatch before deserializing wrong data into the wrong fields. It also pins `MessagePack 2.6.100-alpha`, a version carrying a known security advisory that Arch suppresses through `<NoWarn>NU1902</NoWarn>` — a suppression this repository's `Directory.Build.props` promotes to a build-breaking error instead |
| Build flags that change behaviour | Arch ships six build configurations whose `#if PURE_ECS` and `#if EVENTS` variants change both public API surface and runtime behaviour. A build flag that changes simulation behaviour means, in effect, a separate state hash per configuration, which is a determinism hazard with no corresponding benefit here |

### Snapshot version and schema requirement

Section 7 already requires a snapshot envelope that records "type/version, scenario/registry hashes,
tick, RNG identity/state, payload length/checksum, and authoritative state," and requires a
non-destructive error for an unsupported version rather than a silent misload. This section states the
sharper form that requirement must take once a real snapshot format is authored under Gate 3: **a
Hukbo snapshot header must carry a preset version and a schema version as two distinct fields, and a
mismatch on either one is a hard failure, never a warning and never a best-effort load.** The preset
version identifies which combat ruleset produced the authoritative state being restored — the same
versioning already required of any `CombatPresetId` change under the determinism contract in section
4. The schema version identifies the shape of the envelope itself, independent of which preset wrote
it. The two move independently: a schema change with no preset change means the same combat rules
serialized in a different shape, while a preset change with no schema change means the same envelope
shape now carrying a different ruleset. Collapsing the two into a single field loses that distinction
and lets a stale save look compatible when it is not.

This requirement answers a specific negative example rather than a hypothetical one. `Arch.Persistence`
— the persistence add-on for the very library this section otherwise draws techniques from — has no
version field, no magic number, and a fully positional layout that depends on component registration
order matching exactly across the save boundary, with nothing to detect or reject a mismatch before it
silently deserializes the wrong bytes into the wrong fields. That is precisely the failure mode this
requirement exists to close off before Hukbo authors its own snapshot format, not after a save file has
already demonstrated the gap.
