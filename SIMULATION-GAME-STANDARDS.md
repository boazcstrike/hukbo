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
7. Commit movement in ascending `EntityId`.
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
| `BodyRadiusRaw` (common to all agents) | `4096` | 4 |
| Body diameter, `2 * BodyRadiusRaw` | `8192` | 8 |
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
units of slack, so a packed front deals damage rather than deadlocking.

`Scenario.Validate` rejects any configuration where `2 * BodyRadiusRaw > AttackRangeRaw`, because
that combination produces bodies that can never reach each other. Intent selection and attack
gathering call one shared reach helper so the two stages cannot disagree.

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
GatherMovementProposals      // reads tick-start positions only
ResolveCollisions            // rebuilds the grid, validates candidates
CommitMovement               // single commit, emits Move events
MeasureCollision             // pure observation, writes no agent state
GatherAndCommitAttacks       // reads resolved positions
ResolveOutcome
```

`MeasureCollision` derives this tick's counters from committed positions. It writes no agent state
and nothing it produces is hashed.

### Priority and the candidate ladder

Movers are resolved in **ascending `EntityId`**. Once an agent's position is committed for the
tick, later movers treat it as an obstacle. A lower `EntityId` therefore wins a contested
destination. This is an explicit, documented identifier priority rather than an accident of
iteration order.

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
requires a new preset version and new golden expectations.

### What the rule actually produces

The shipped behaviour is not shield-to-shield contact between factions. Across an entire 200-agent
battle and an entire 500-agent battle, the number of cross-faction contact pairs was **zero**.
Opposing bodies never touch, because an agent stops advancing once its target is inside the
twelve-world-unit attack reach while a body is only eight world units across, leaving four world
units of permanent air between the two front ranks.

The observable effect of collision is therefore **allies queueing behind their own front line**: a
rear agent trying to advance into the space its own front rank already occupies is refused, holds
position, and reports `Blocked`. That queueing is what constrains frontage and produces a visible
line. It is measured in blocked agent-ticks, not in contacts. The recorded figures are in
[docs/development/testing.md](docs/development/testing.md).

This is the shipped rule, recorded as observed rather than as intended. Anyone tuning the contact
model later should start from the fact that the binding constraint on the battle line is attack
reach, not body radius.
