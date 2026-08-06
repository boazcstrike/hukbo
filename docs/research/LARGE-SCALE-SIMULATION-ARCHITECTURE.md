# Large-Scale Battle Simulation Architecture

Status: research baseline

Scope: deterministic battles with hundreds or thousands of combatants and
potentially more than one hundred unit definitions

Primary consumer: Hukbo requirements, design, and task-planning agents

## Executive conclusion

Large battle simulations scale through hierarchy, bounded local work, and
independent update rates. They do not give every displayed soldier an
unbounded strategic planner.

The reusable pattern is:

1. army-level AI chooses posture and objectives;
2. formations or detachments receive tactics;
3. lower-level systems execute movement, perception, combat, and morale;
4. authoritative state advances at a fixed logical rate;
5. animation and rendering consume buffered state independently; and
6. measurement gates determine when spatial indexing, data-layout changes, or
   parallel execution are justified.

For Hukbo, the recommended order is:

```text
instrumentation
    -> deterministic spatial grid
    -> reusable event storage
    -> unit-definition registry
    -> formation model
    -> global navigation and local steering
    -> measured deterministic parallelism
```

This order preserves the current deterministic foundation and addresses
algorithmic costs before introducing concurrency or a general-purpose ECS.

## Scope and evidence limits

Total War is proprietary. Public material reveals important architectural
boundaries and production practices, but not its complete source, data layout,
job graph, numeric policy, or pathfinding implementation.

This document therefore distinguishes:

- **Confirmed disclosure:** stated by Creative Assembly staff or official
  material.
- **Industry-supported pattern:** established by primary postmortems,
  peer-reviewed work, or inspectable engines.
- **Hukbo recommendation:** an engineering conclusion based on those sources
  and current Hukbo code.

No undocumented Total War internal is presented as fact.

## Two different meanings of scale

The project must track two independent complexity axes.

### Runtime entity scale

This is the number of combatants, formations, projectiles, obstacles, and
effects active in a battle.

Let:

- `N` be individual combatants;
- `U` be formations or controllable units;
- `P` be active projectiles;
- `V` and `E` be navigation-graph nodes and edges; and
- `k` be the average number of relevant nearby candidates.

### Content scale

This is the number of unit definitions, weapons, armor types, abilities,
effects, factions, and special interaction rules.

Let:

- `T` be unit definitions;
- `W` be weapon definitions;
- `A` be abilities; and
- `R` be sparse exceptional interaction rules.

Increasing `N` stresses runtime algorithms and memory bandwidth. Increasing
`T`, `W`, and `A` stresses authoring, validation, compatibility, balancing, and
test coverage. A design can handle 10,000 identical soldiers but collapse under
100 interacting unit definitions, or support 500 definitions while failing at
500 simultaneous agents.

## What Total War publicly discloses

### Hierarchical battle AI

Creative Assembly's public battle-AI model separates player-equivalent command
decisions from the lower battle systems that implement orders. Its disclosed
hierarchy reduces alliance strategy into objectives, detachments, tactics, and
unit orders. Pathfinding, visibility, and combat execution belong to lower
battle systems rather than the high-level commander.

Siege AI adds specialized attack tactics, reserves, entry-point management,
settlement and influence graphs, and level-authored hints. This is evidence for
specialized layers and explicit environmental affordances, not one universal
agent brain.

Sources:

- [Have Fun Storming the Castle: Siege Battle AI in Total War: Warhammer](https://media.gdcvault.com/gdc2016/Presentations/Arsenault_Andre_Have_Fun_Storming.pdf)
- [GDC session abstract](https://www.gdcvault.com/play/1023363/Have-Fun-Storming-the-Castle)

### Low-rate authoritative battle updates

Traditional Total War battle models reportedly updated at 10 Hz. Total War:
Arena reduced its battle model to 5 Hz for CPU headroom while maintaining a
faster display rate.

This supports a multi-rate architecture. It does not establish 5 or 10 Hz as a
universal target for Hukbo.

Source:

- [How Total War got stripped down and sped up for F2P Arena](https://www.gamedeveloper.com/design/how-i-total-war-i-got-stripped-down-and-sped-up-for-f2p-i-arena-i-)

### Logic, animation, and display separation

Creative Assembly described Total War battle logic and animation as decoupled
from display. Logic generated future state while display rendered current
state, allowing complex work to span display frames. The same account describes
dynamic environment and unit LOD, multicore task distribution, physically
simulated projectiles, selective expensive checks, and AI hint lines embedded
in handcrafted maps.

Source:

- [Designing Total War: Warhammer II to handle tons of units and massive battles](https://www.gamedeveloper.com/design/designing-i-total-war-warhammer-ii-i-to-handle-tons-of-units-and-massive-battles)

### CPU work distribution

Total War's technical director identified entity combat, collision, pathfinding,
animation, and draw-matrix construction as significant CPU work. Suitable tasks
are spread across cores, while some AI processing remains harder to
parallelize.

Creative Assembly also documented separating game-logic and rendering threads,
improving its task system, and moving suitable presentation simulation to the
GPU.

Sources:

- [How Does CPU Affect Gaming Experience?](https://www.intel.com/content/www/us/en/gaming/resources/how-cpus-affect-your-gaming-experience.html)
- [Official Total War Optimisation Blog](https://wiki.totalwar.com/w/Optimisation_Blog)

### Approximate matchup evaluation

Creative Assembly has used mathematical approximation for contextual unit
performance. Its disclosed approach linearized performance around a baseline
using situational modifiers rather than resolving every possible battle during
AI deliberation.

This supports precomputed or approximate matchup estimates for strategic AI.
It does not replace authoritative tactical combat.

Source:

- [Understanding Your Enemy: A Mathematical Approach to Unit Analysis](https://www.gdcvault.com/play/1025294/Understanding-Your-Enemy-A-Mathematical)

### Long-term engine cost

Creative Assembly announced Warcore as the future foundation for major Total
War releases and described it as an evolution of its proprietary technology.
The stated reasons include control over performance, scalability, tools, and
franchise-specific systems.

The lesson for Hukbo is narrower: specialized technology can be valuable, but
long-lived engine complexity requires explicit boundaries, tools, tests, and
periodic architectural renewal.

Source:

- [Total War 25th Anniversary Showcase and Warcore FAQ](https://community.creative-assembly.com/total-war/total-war/blogs/91-title-total-war-25th-anniversary-showcase)

## Fundamental implementation model

### Authoritative tick pipeline

A scalable deterministic tick should have declared stages and stable commit
rules:

1. accept commands for the tick;
2. update or rebuild spatial data;
3. update scheduled strategic and formation decisions;
4. gather perception and target proposals;
5. gather route, steering, and movement proposals;
6. commit movement in stable entity order;
7. gather attacks and projectile effects;
8. apply accumulated damage and morale effects;
9. resolve death, rout, and victory;
10. emit ordered events and diagnostics; and
11. publish a completed read-only snapshot.

Parallel workers may gather proposals from an immutable tick-start view.
Authoritative commits remain ordered and deterministic.

### AI hierarchy

```text
Army commander
    posture, objective, reserves, threat allocation
        |
        v
Formation or detachment
    role, destination, target formation, cohesion, tactic
        |
        v
Individual combatant
    slot following, local avoidance, target contact, attack execution
```

Army and formation decisions can run less frequently than contact movement and
combat. Update cadence must depend on tick number and gameplay state, never
camera distance or wall-clock timing.

### Global navigation and local movement

These are separate problems:

- **Global navigation** finds a valid corridor around terrain and major
  obstacles.
- **Formation movement** transforms that corridor into formation position,
  facing, width, and slot goals.
- **Local steering** resolves nearby agents and short-range obstruction.

Running a complete path search for every soldier duplicates work. Prefer one
route per formation or destination group plus bounded local correction.

Hierarchical pathfinding becomes relevant only after representative maps show
that ordinary A* or navmesh queries exceed the budget. HPA-style algorithms
trade small path-quality losses for substantially smaller searches and can use
read-only terrain during query execution.

Source:

- [DHPA* and SHPA*: Efficient Hierarchical Pathfinding in Dynamic and Static Game Worlds](https://ojs.aaai.org/index.php/AIIDE/article/view/12397)

For very dense groups sharing destinations, flow or potential fields can
replace many independent paths. Continuum crowd work demonstrates global
navigation with dynamic obstacles at interactive rates. ORCA provides
high-quality local collision avoidance, but its numeric and ordering behavior
must be reconciled with Hukbo determinism before adoption.

Sources:

- [Continuum Crowds](https://oamonitor.ireland.openaire.eu/rfo/sfi_rfo/search/publication?pid=10.1145%2F1179352.1142008)
- [RVO2 and ORCA documentation](https://gamma-web.iacs.umd.edu/RVO2/documentation/2.0/whatsnew.html)

### Spatial perception and collision

The baseline spatial structure for a bounded 2D battlefield should be a
deterministic uniform grid:

1. clear reusable cell storage;
2. insert living entities in ascending `EntityId`;
3. derive the cell range intersecting a query radius;
4. traverse cells in a declared coordinate order;
5. evaluate candidates with authoritative fixed-point math; and
6. resolve ties with the existing total order.

The grid is a derived cache. It is rebuildable, excluded from authoritative
saves, and verified against a naive reference implementation.

### Data layout

Runtime combat state should be compact and scan-friendly:

- stable entity ID;
- definition index;
- faction and formation index;
- position and velocity;
- health, morale, and cooldown;
- target and intent; and
- small flags or lifecycle state.

Immutable definition data should not be duplicated into every agent unless
profiling proves the copy is beneficial. Hot systems should resolve definition
indexes to precomputed arrays rather than perform repeated dictionary or
reflection lookups.

A general-purpose ECS is not required. Parallel arrays, packed structs, or a
small domain-specific component store are sufficient if they produce measured
benefits and preserve clarity.

### One hundred or more unit definitions

Unit types should be composed from immutable definitions:

```text
UnitDefinition
    Identity and version
    Body and movement profile
    Perception profile
    Weapon references
    Armor and shield references
    Morale profile
    Formation role
    Ability and behavior tags
    Presentation references
```

Runtime agents store a stable definition ID or dense validated index.

Avoid a complete `T x T` table. One hundred definitions create 10,000 ordered
matchups before terrain, fatigue, formation, ability, or veterancy dimensions
are considered. Prefer:

- factorized stats and tags;
- general formulas;
- precomputed weapon/armor or effect tables;
- sparse exceptional overrides; and
- generated validation and representative golden matchups.

OpenRA provides an inspectable example of data-driven actor composition through
traits and rule definitions, although Hukbo does not need to adopt OpenRA's
engine architecture.

Source:

- [OpenRA trait documentation](https://docs.openra.net/en/release/traits/)

### Rendering and animation

Rendering cost must be independent from authoritative complexity where
possible:

- batch or instance combatant geometry;
- cull outside the visible arena;
- reduce visual detail and animation frequency by camera distance;
- keep presentation effects out of the state hash;
- interpolate completed simulation snapshots; and
- never feed interpolated positions back into gameplay.

Simulation LOD is more dangerous. Camera-dependent simulation LOD would make
outcomes depend on presentation. If multi-rate authoritative processing is
introduced, its schedule must be deterministic and based on gameplay state.

## Complexity standards

| System | Naive form | Scalable target |
| --- | --- | --- |
| Target acquisition | `Theta(N^2)` | Expected `Theta(N + N*k)` spatial query |
| Local collision | `Theta(N^2)` | `Theta(N + N*k)` grid or tree-backed neighbors |
| Strategic AI | Full decisions for every soldier | Decisions over `U` formations, where `U << N` |
| Navigation | `N` full path searches | `U` group routes plus bounded local steering |
| Navigation search | `O(N * E log V)` | Approximately `O(U * E log V)` before caching |
| Combat contacts | Every attacker checks all targets | Spatial or contact lists, approximately `O(N*k + P)` |
| Rendering submission | One object or submission per entity | `O(N)` instance data and `O(batch count)` submissions |
| Unit interactions | Dense `T^2` special cases | `O(T + R)` definitions plus sparse overrides |

Worst-case spatial behavior can still degrade toward quadratic density.
Benchmarks must therefore include compressed-front and all-in-perception
scenarios, not only average spread.

## Hukbo baseline

### Existing strengths

Current source already provides:

- a fixed 20 Hz scenario tick;
- an accumulator that advances logical ticks independently from rendering;
- a headless deterministic core;
- fixed-point authoritative positions;
- a project-owned deterministic PRNG;
- monotonically ordered entity IDs;
- gathered movement and attack proposals;
- simultaneous accumulated damage;
- ordered events and state hashing; and
- immutable combat definitions with precomputed weapon/shield weight tables.

These are the correct foundations to retain.

### Current scaling limit

`BattleSimulation.SelectTargetsAndIntents` scans the complete agent array for
every living agent. With two equal living factions:

| Total agents | Complete loop visits/tick | Approximate hostile distance checks/tick |
| ---: | ---: | ---: |
| 200 | 40,000 | 20,000 |
| 500 | 250,000 | 125,000 |
| 1,000 | 1,000,000 | 500,000 |
| 2,000 | 4,000,000 | 2,000,000 |
| 10,000 | 100,000,000 | 50,000,000 |

At 20 Hz, an initial 10,000-agent battle implies about two billion complete
loop visits per wall-clock second before navigation, collision, richer AI,
projectiles, morale, or rendering.

### Measured headless sample

Environment:

- Windows x64 `10.0.26200`;
- .NET `10.0.10`;
- 20 reported logical processors;
- Release configuration;
- seed `1`; and
- 200 requested ticks.

Command family:

```powershell
dotnet run --project src/Hukbo.Headless -c Release --no-restore -- `
  --agents <count> --ticks 200 --seed 1
```

Results:

| Agents | p50 tick | p95 tick | p99 tick | Maximum | Harness allocation |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 200 | 0.130 ms | 0.421 ms | 0.790 ms | 3.794 ms | 12.9 MB |
| 500 | 0.372 ms | 0.710 ms | 0.788 ms | 3.839 ms | 32.1 MB |
| 1,000 | 0.895 ms | 1.693 ms | 4.653 ms | 5.249 ms | 64.1 MB |
| 2,000 | 3.000 ms | 4.669 ms | 7.987 ms | 11.798 ms | 128.1 MB |

Interpretation limits:

- rendering is excluded;
- the harness runs two simulations for determinism but times one;
- deaths reduce later-tick work;
- there is no per-stage timing;
- candidate counts are not recorded; and
- formations, pathfinding, morale, collision, and projectiles are not present.

The allocation curve is almost perfectly linear because each active
simulation creates a tick event list with a backing array sized to twice the
agent count. Existing deferred debt is recorded in
`docs/archives/2026-07-27/2026-07-27-battle-event-allocation-packing.md`.

### Gaps between standards and source

`SIMULATION-GAME-STANDARDS.md` already calls for a deterministic spatial index
and stage metrics, but current source does not yet implement them. Other future
gaps are:

- no formation-level authoritative entity;
- no unit-definition registry beyond combat loadouts;
- no global pathfinding or local collision avoidance;
- no morale or cohesion model;
- no previous/current snapshot interpolation;
- no stage-level benchmark report; and
- no deterministic parallel proposal scheduler.

## Recommended gates

### Gate A: Measurement

Required before structural optimization:

- stage p50/p95/p99/max;
- alive agents per tick;
- spatial candidates and accepted neighbors;
- events emitted and bytes allocated;
- state-hash cadence cost;
- visible agents, draw calls, and render p95/p99; and
- workloads that preserve worst-case population and perception.

### Gate B: Spatial perception

Pass when:

- grid and naive target results match across generated worlds;
- tie-breaking remains identical;
- same-seed hashes and events remain unchanged;
- broad-density and compressed-front workloads are reported; and
- measured improvement justifies retaining the grid.

### Gate C: Event storage

Pass when:

- tick event order and public immutability remain unchanged;
- explicit snapshots remain immutable;
- paired deterministic runs remain equal;
- allocation/tick falls by a reported amount; and
- no pooled or reused data escapes its declared lifetime.

### Gate D: Unit definitions

Pass when:

- definitions use stable IDs, schema versions, and a content hash;
- runtime agents store a compact definition reference;
- all references and numeric bounds validate before the match;
- hot paths use precomputed dense indexes;
- sparse overrides cover exceptional matchups; and
- unknown or incompatible definitions fail non-destructively.

### Gate E: Formations and navigation

Pass when:

- formation orders have stable total ordering;
- slot assignment and movement are deterministic;
- global route and local steering are separate;
- group-path results match a reference solver where applicable;
- narrow passage, crossing, congestion, and unreachable cases are tested; and
- the battle remains inspectable through reason codes and diagnostics.

### Gate F: Parallel proposals

Pass when:

- single-threaded and parallel modes produce identical hashes and events;
- workers read an immutable tick-start state;
- worker-local buffers prevent shared mutation;
- merge and commit use stable keys;
- scaling is measured on named hardware; and
- the parallel path can be disabled for diagnosis.

## Research-driven recommendation

Do not replace the current core with a generic ECS or imitate a proprietary
engine wholesale.

Retain Hukbo's deterministic pipeline and introduce scale in this order:

1. measure the current stages;
2. bound neighborhood work;
3. remove known per-tick allocation;
4. separate immutable unit definitions from mutable agent state;
5. raise strategic decisions to formation level;
6. share global routes across formation members;
7. use presentation LOD independently; and
8. parallelize proposal gathering only after the data and algorithms are ready.

The companion planning documents were:

- `docs/plans/2026-07-27-large-scale-simulation-architecture-design.md`
- `docs/plans/2026-07-27-large-scale-simulation-architecture.md`

**Both were deleted on 2026-07-28 at commit `fea96d4`, whose whole message is
"docs(plans): delete". They were not archived, so neither has a path under
`docs/archives/` and neither can be opened from a working tree.** They are
recoverable from history if the reasoning is ever needed:

```
git show fea96d4^:docs/plans/2026-07-27-large-scale-simulation-architecture.md
git show fea96d4^:docs/plans/2026-07-27-large-scale-simulation-architecture-design.md
```

The paths above are left written out rather than corrected because there is no
correct path to point them at; recording what happened to them is the only
honest repair. This document is the surviving statement of the architecture.

Detailed research on formation semantics, body contact, collision resolution,
and the Requirements-to-Task-Planner handoff is maintained separately in
`docs/research/FORMATION_AND_COLLISION_MECHANICS.md`. That document is the
feature-level evidence input; this document remains the system-level
architecture baseline.
