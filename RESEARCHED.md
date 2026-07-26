# Hukbo - Research Brief

Research date: 2026-07-26  
Repository baseline: [`README.md`](./README.md)  
Purpose: give the Planner Agent an evidence-backed product direction before architecture or feature implementation begins.

## Executive recommendation

Build a **disposable, seeded, spectator-first battle simulator** before building a colony game.

The first product should prove that 200 autonomous combatants can produce a battle that is:

1. deterministic enough to replay and debug;
2. readable at both map and individual-agent scale;
3. tactically non-trivial without player micromanagement; and
4. interesting enough that a viewer can explain why the battle turned.

Use **C# on .NET 10** for the simulation core. Keep the simulation headless and independent from the renderer. This is a recommendation for this repository's stated scale and iteration needs, not a universal claim that C# is the best language for every simulation game. Confirm it with a release-build workload spike before committing to the full stack.

Use a fixed simulation timestep, a project-owned deterministic PRNG, stable entity ordering, and separate versioned command and event streams. Render independently, interpolating between simulation states. Start with 50-agent smoke tests, make **200 simultaneous combatants the first acceptance target**, and treat 1,000 as a measured stretch goal rather than a launch promise.

Do not begin with persistent worlds, physiology, production chains, building, diplomacy, multiplayer, or an all-purpose ECS framework. Those features are valuable only after the arena loop is fun and inspectable.

**Confidence:** high on the milestone shape and simulation/render separation; medium-high on C#/.NET pending a representative benchmark; medium on current community direction because social-source coverage was partial.

## Scope decisions recommended now

These answers resolve the five open questions in the README without expanding its scope.

| README decision | Recommendation | Why |
|---|---|---|
| Disposable matches or persistent world | Disposable seeded matches first | Persistence multiplies save migration, world-state, economy, recovery, and content obligations before combat is proven. |
| First target scale | 200 combatants; smoke-test at 50; design benchmark at 500; 1,000 stretch | 200 is large enough for fronts, flanks, local routs, and reinforcement behavior while still allowing per-agent inspection. |
| Readability or physiology | Readability first | A spectator game fails if the viewer cannot explain deaths, routs, and momentum. Detailed body simulation can be layered later. |
| Pre-battle configuration | Seed, map preset, faction size, doctrine, and loadout preset only | These controls create repeatable experiments without turning the MVP into an editor. |
| Dots: placeholder or identity | Keep dots as a permanent debug/performance view; allow later art to layer on top | The dot view is ideal for scale testing, replay comparison, and tactical readability even if it is not the final art style. |

## What RimWorld fundamentally is

RimWorld should be studied as a **story-producing control system**, not copied as a feature checklist.

### 1. Indirect control over autonomous agents

The player shapes work priorities, zones, equipment, schedules, and emergency orders, while pawns normally choose and execute jobs themselves. The community-maintained modding documentation describes pawn behavior as jobs executed through job drivers and toils; it also warns that this documentation is reverse-engineered because RimWorld has no formal public modding API. Treat this as useful implementation evidence, not an official source contract. See [RimWorld Wiki: Example Mending Job](https://rimworldwiki.com/wiki/Modding_Tutorials/Code_MendingJob) and [Modding Tutorials](https://rimworldwiki.com/wiki/Modding_Tutorials).

**Arena translation:** the player configures the conditions; agents own moment-to-moment movement, targeting, firing, and retreat. Debug overrides are tools, not the normal play loop.

### 2. Pawns carry state that makes outcomes personal

RimWorld publicly describes colonists with moods, needs, wounds, illnesses, addictions, relationships, skills, and body-part changes. These interacting states make the same raid mean different things to different pawns. See the [official RimWorld feature description](https://rimworldgame.com/).

**Arena translation:** the MVP needs only enough persistent agent state to explain combat decisions:

- identity and faction;
- position and movement capability;
- weapon, range, accuracy, cooldown, and damage;
- health and lifecycle state;
- perception/known targets;
- current intent and target;
- morale/cohesion and retreat state in the first post-proof layer.

Needs, relationships, personality, detailed injuries, and memories are later story multipliers, not prerequisites for the first fight.

### 3. A director watches the simulation and injects incidents

RimWorld's official description says its storyteller controls the apparently random events dealt into the story. Tynan Sylvester describes the structure more specifically as watchers that observe state, such as wealth and recent danger, paired with incident generators that choose events and attempt to shape a rising-and-falling pacing curve. See the [official overview](https://rimworldgame.com/) and the direct-developer discussion in [PC Gamer's colony-sim design interview](https://www.pcgamer.com/games/strategy/the-challenges-of-developing-the-colony-sim-from-dungeon-keeper-to-dwarf-fortress-and-beyond/).

**Arena translation:** do not add an AI storyteller to the first proof. First make agents and combat deterministic. Later, add a separate match-director layer that can introduce reinforcements, weather, objectives, or retreat opportunities through recorded commands. Keeping the director outside the core combat rules preserves replayability.

### 4. Stories emerge from interactions, not scripts

The official positioning calls RimWorld a story generator that co-authors tragic, twisted, and triumphant stories. A July 2026 r/RimWorld post about a father attempting to flee after sick quadruplets were born drew thousands of votes because several ordinary systems combined into a memorable anecdote. See [the current community example](https://www.reddit.com/r/RimWorld/comments/1v1mh5e/the_mother_gave_birth_to_four_sick_quadruplets/).

**Arena translation:** a good battle is not merely a winner calculation. It produces explainable beats such as:

- a flank collapsing after its leader dies;
- a squad retreating when isolated;
- a numerically weaker force winning through terrain and focus fire;
- a last survivor delaying an advance;
- reinforcements arriving after a rout begins.

These beats should fall out of rules and be recoverable from the event log.

### 5. Presentation filters complexity into player attention

Sylvester says he resolves design conflicts in favor of the story-generator direction and treats player learning and attention as limited resources. Colony-sim developers repeatedly identify communicating deep simulation state as a harder problem than generating it. See the [direct-developer interview](https://www.pcgamer.com/games/strategy/the-challenges-of-developing-the-colony-sim-from-dungeon-keeper-to-dwarf-fortress-and-beyond/).

**Arena translation:** every new simulated variable must answer two questions:

1. Can it change a battle in a meaningful way?
2. Can the viewer discover that effect without reading source code?

If the second answer is no, the feature is not complete.

## Feature plan

### Milestone 0 - deterministic arena proof

This is the README milestone, made objectively testable.

**Scenario**

- One generated or preset flat open map.
- Two factions with configurable colors and equal default rosters.
- Seeded spawn placement and seeded combat randomness.
- One match objective: eliminate the opposing faction.

**Autonomous agents**

- Enemy perception within a defined radius.
- Deterministic target selection with documented tie-breakers.
- Direct movement toward an attack position.
- Ranged attack with range, cooldown, hit test, damage, health, and death.

**Spectator controls**

- Pan and zoom.
- Pause, 1x, 2x, and 4x speed.
- Reset and replay the same seed.
- Select or hover an agent to see faction, health, target, and current intent.
- Visible surviving counts, elapsed simulation time, and winner.

**Explainability**

- Append-only event log for spawn, target change, shot, hit, death, and victory.
- Optional overlays for faction, target line, perception range, and current path.
- A compact end-of-match summary: winner, survivors, duration, casualties over time, and decisive turning point if a rule-based explanation is reliable.

**Proof criteria**

- Two runs of the same build, scenario seed, and command stream produce the same ordered event stream and final state hash on the same platform.
- 200 combatants sustain the chosen fixed tick rate in a release build on the named reference machine.
- The renderer can remain responsive while the simulation is paused, stepped, or accelerated.
- A reviewer can select any living agent and explain its current action from visible state.
- The match always reaches a winner or a documented stalemate timeout.

### Milestone 1 - autonomous battle behavior

Add only after Milestone 0 passes.

- Morale and suppression.
- Retreat, rally, and surrender/death alternatives.
- Squad membership and local cohesion.
- Two or three doctrines, such as aggressive, balanced, and cautious.
- Obstacles, deterministic pathfinding, line-of-sight, and cover value.
- Melee only if it creates a distinct tactical role.
- Scenario presets with asymmetric force composition.
- Timeline markers for routs, leader deaths, reinforcements, and objective changes.

This is where the game should begin producing stories rather than just particles exchanging damage.

### Milestone 2 - tactical depth and authoring

- Several weapon roles with explicit counters.
- Terrain classes and chokepoints.
- Limited ammunition, healing, or resupply if they create observable decisions.
- Scenario editor with versioned data files.
- Batch simulation and result comparison.
- Replay scrubber and deterministic single-tick stepping.
- Optional director-controlled incidents recorded into the command stream.
- Stable data definitions for content expansion.

### Later, only after evidence of demand

- Persistent campaign/world.
- Named-agent memories and relationships.
- Detailed body-part physiology.
- Colony construction and production.
- Diplomacy and faction simulation.
- Mod scripting API.
- Multiplayer or persistent server.
- Thousands of simultaneously detailed agents.

The recent r/RimWorld community discusses faction diplomacy, multiplayer, multiple colonies, and stronger CPU utilization, but it is divided on which belong in the base game. That is a reason to preserve extension seams, not a reason to put them in the MVP. See [the May 2026 feature discussion](https://www.reddit.com/r/RimWorld/comments/1t98qns/which_feature_would_you_love_the_most_to_see/).

## Simulation language/runtime decision

### Evaluation criteria

The relevant question is not "which language is fastest?" It is "which stack reaches a deterministic, inspectable 200-agent proof fastest, while retaining a credible route to 1,000 agents?"

Weights used for this project:

- iteration speed and testability - 25%;
- predictable simulation performance - 20%;
- deterministic-control ergonomics - 20%;
- profiling and debugging tools - 15%;
- rendering/input ecosystem - 10%;
- long-term maintainability and contributor accessibility - 10%.

Scores below are planning judgments from 1 (weak) to 5 (strong), not benchmark results.

| Candidate | Iteration/test | Throughput ceiling | Deterministic control | Tools | Render ecosystem | Maintainability | Weighted view |
|---|---:|---:|---:|---:|---:|---:|---:|
| **C# / .NET 10** | 5 | 4 | 4 | 5 | 5 | 5 | **4.55** |
| Rust / Bevy ECS | 3 | 5 | 3 | 4 | 3 | 3 | 3.55 |
| C++ / custom or engine core | 2 | 5 | 4 | 5 | 5 | 2 | 3.70 |
| GDScript / Godot | 5 | 2 | 3 | 4 | 5 | 4 | 3.65 |
| TypeScript / browser runtime | 5 | 2 | 3 | 4 | 4 | 4 | 3.60 |

### Firm recommendation: C# on .NET 10

Reasons:

- .NET 10 is the current recommended LTS release and is supported through November 2028. See the [official .NET download and support page](https://dotnet.microsoft.com/en-us/download).
- C# has fast compile-test-profile loops, mature diagnostics, strong testing support, and multiple renderer choices.
- Arrays, spans, value types, pooling, and SIMD-accelerated numeric types allow allocation-conscious and data-oriented hot loops without switching languages. See Microsoft's [`Span<T>` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.span-1?view=net-10.0) and [SIMD guidance](https://learn.microsoft.com/en-us/dotnet/standard/simd).
- The scale target is hundreds to a low thousand of agents, where algorithm choice, tick frequency, pathfinding policy, and data layout are more likely to dominate than language overhead.
- Keep the renderer as a Gate 0 decision. Godot is the default desktop shell when editor and UI tooling matter; a code-first renderer such as MonoGame is the smaller alternative when direct control matters more. MonoGame currently supports .NET 10 and presents itself as a framework rather than an editor-driven engine. See the [MonoGame setup guide](https://docs.monogame.net/articles/tutorials/building_2d_games/02_getting_started/index.html?tabs=windows). Either choice must keep the simulation in a plain .NET library.

### Conditions on the recommendation

- The simulation must be a plain .NET library with no dependency on scene nodes, physics bodies, frame time, or renderer callbacks.
- No per-agent heap allocations in steady-state hot loops without measurement and justification.
- Use contiguous component arrays or similarly cache-friendly storage for frequently scanned data; do not create a complex generic ECS before a profiler demonstrates the need.
- Use a fixed tick and stable system order.
- Use a project-owned PRNG whose algorithm and state are versioned. Microsoft explicitly warns that `System.Random` is not guaranteed to produce the same sequence across major .NET versions even with the same seed. See the [official `System.Random` remarks](https://learn.microsoft.com/en-us/dotnet/api/system.random).
- Do not let unordered collection iteration affect decisions. `HashSet<T>` is explicitly unordered; sort keys or use stable indexed storage when order can affect a result. See the [official collection remarks](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1).

### When Rust should win instead

Choose Rust and a carefully controlled ECS/custom core if:

- the core team is already substantially more productive in Rust;
- measured C# release-build results miss the target after algorithmic fixes;
- memory control and predictable no-GC latency are more important than editor/tool maturity; or
- the intended scale changes from hundreds to tens of thousands of detailed agents.

Do not assume Bevy automatically gives deterministic replay. Bevy's own ECS documentation says systems run in parallel and non-deterministically by default unless ordering is declared. See [Bevy ECS system ordering](https://docs.rs/bevy_ecs/latest/bevy_ecs/system/index.html).

### Release-build spike required before lock-in

Implement a throwaway benchmark, not game architecture:

- 1,000 lightweight agents on a representative map;
- spatial query, target acquisition, simple movement, and attack cooldown;
- 20 fixed simulation ticks per second;
- deterministic state hash after 10,000 ticks;
- pause, accelerated stepping, and no rendering dependency;
- allocation, CPU-time, and memory measurements in release mode.

Pass condition: 200 full-MVP agents comfortably meet the tick budget and 1,000 lightweight agents expose no runtime-level blocker. If the spike fails, profile algorithms and data access before changing language.

## Performance principles supported by current discussion

Recent game-development discussion is unusually consistent on one point: the model does not need to run at render frequency.

A July 2026 r/gamedev thread on 500-unit colony simulations recommends:

- decoupling model ticks from rendered frames;
- interpolating the view between states;
- time-slicing or asynchronously preparing expensive work;
- running different systems at different frequencies;
- profiling before adding multithreading;
- treating pathfinding and repeated reachability checks as major costs.

One experienced developer in that thread reports shipping simulation models with 100-400 ms tick budgets; another describes retrying unreachable jobs only every several seconds. These are practitioner claims, not universal benchmarks, but they strongly support a multi-rate design. See [the full discussion](https://www.reddit.com/r/gamedev/comments/1uccg2b/how_can_colony_management_games_simulate_500/).

Godot's official documentation independently supports fixed-timestep simulation with renderer interpolation and notes that AI/game logic need not execute every rendered frame. See [fixed timestep interpolation](https://docs.godotengine.org/en/stable/tutorials/physics/interpolation/physics_interpolation_introduction.html) and [CPU optimization](https://docs.godotengine.org/en/latest/tutorials/performance/cpu_optimization.html).

**Later optimization candidates, only after the baseline is measured:**

- combat resolution: every simulation tick;
- steering/perception refresh: every 1-4 ticks, staggered by entity ID;
- path replanning: on invalidation or a slower staggered cadence;
- strategic intent and rally decisions: slower cadence;
- rendering: every visual frame using interpolation;
- UI summaries: throttled independently from combat.

## Where the leading communities are aiming

There is no single colony-sim consensus. The genre is separating into several valuable directions.

| Community | Current direction/signal | Implication for Hukbo |
|---|---|---|
| **r/RimWorld** | Emergent character anecdotes remain the strongest engagement magnet. Current discussion also asks for performance, faction diplomacy, multi-colony support, QoL, accessibility, and continued mod freedom. | Make battles generate retellable moments; preserve debug/data extension seams; prioritize CPU headroom and readable UI. |
| **r/gamedev** | Decoupled simulation/render loops, time-sliced AI, staggered updates, pathfinding discipline, profiling, and data-oriented hot paths. ECS is debated as a tool, not treated as a substitute for measurement. | Build a fixed-tick headless core and a benchmark harness before framework abstractions. |
| **r/BaseBuildingGames** | Audience demand spans small, intimate casts; large automation systems; co-op/persistent worlds; and genre hybrids. A May 2026 discussion explicitly describes tension between automation scale and character immersion. | Pick a lane: this project should favor readable autonomous combat and named-agent stories before adding economy or persistent co-op. |
| **r/Oxygennotincluded** | Overlays, bigger zoom, building ranges, better info cards, and automation visualization dominate QoL requests. | Overlays and inspection are part of the simulation feature, not polish to postpone. |
| **r/SongsOfSyx** | Macro-scale logistics and populations in the thousands are the differentiator. Community questions focus on production and distribution breaking down at scale. | Treat thousands of agents as a later macro-simulation mode; do not pay its design cost in the first arena. |
| **r/dwarffortress** | Maximum systemic depth, generated history, and player-retold stories remain the identity. | Borrow consequence and individuality, not interface opacity or unconstrained simulation breadth. |

Representative current discussions:

- [RimWorld feature priorities: performance, diplomacy, multiplayer, and mod freedom](https://www.reddit.com/r/RimWorld/comments/1t98qns/which_feature_would_you_love_the_most_to_see/)
- [RimWorld 1.6 performance and late-game TPS discussion](https://www.reddit.com/r/RimWorld/comments/1u0po2d/the_performance_improvement_of_16_is_genuinely/)
- [RimWorld mod-list load-time failure and tooling](https://www.reddit.com/r/RimWorld/comments/1uq0qhb/why_has_my_loading_time_doubled_i_only_installed/)
- [Blind players using RimWorld accessibility mods](https://www.reddit.com/r/RimWorld/comments/1ub21zo/blind_people_play_rimworld_and_you_wouldnt_know/)
- [Base-building players discussing role-play versus automation scale](https://www.reddit.com/r/BaseBuildingGames/comments/1tb88qb/colony_sims_with_strong_rp_elements_and_good/)
- [Oxygen Not Included QoL overlays and information clarity](https://www.reddit.com/r/Oxygennotincluded/comments/1v38y5r/your_favorite_qol_mods/)
- [Songs of Syx logistics at 2,000-10,000 population](https://www.reddit.com/r/songsofsyx/comments/1sr8v0x/how_to_expand_and_scale_properly/)
- [A current self-hosted co-op colony-sim pitch](https://www.reddit.com/r/BaseBuildingGames/comments/1v6k5l7/coop_self_hosted_base_building_colony_sim/)

### Directional synthesis

The strongest opportunity for this repository is not "RimWorld, but with more units." It is:

> **A battle observatory where deterministic autonomous systems produce legible, replayable war stories at a scale larger than a typical RimWorld raid.**

That framing combines the communities' strongest signals without inheriting their entire feature burden:

- RimWorld's story value;
- Songs of Syx's scale ambition;
- Oxygen Not Included's information overlays;
- game-development practice around fixed ticks and multi-rate work;
- a permanent low-cost dot view that makes the simulation understandable.

## Initial model attributes for planning

These are the minimum conceptual components. They are not a mandate to build a generic ECS.

| Component | Initial fields | Milestone |
|---|---|---|
| Identity | stable entity ID, display name/number, spawn order | 0 |
| Faction | faction ID, color, doctrine ID | 0 |
| Transform | position, previous position, facing/velocity if needed | 0 |
| Mobility | speed, destination, path state, blocked/stuck counter | 0 |
| Perception | range, visible/known target IDs, refresh tick | 0 |
| Combat | weapon ID, range, accuracy, damage, cooldown, target ID | 0 |
| Vitality | health, alive/dead, status flags | 0 |
| Intent | current action, reason code, since-tick | 0 |
| Squad | squad ID, role, leader ID, cohesion | 1 |
| Morale | current morale, suppression, retreat/rally threshold | 1 |
| History | kills, wounds, notable event IDs | 1-2 |

Every attribute that can affect a decision must be serializable into the deterministic state hash or derivable from hashed state.

## Caching and storage implications for the Planner Agent

The simulation should distinguish four kinds of data:

1. **Authoritative state:** the minimum state required to continue the next tick.
2. **Derived cache:** spatial buckets, visibility results, path caches, and UI aggregates that can be rebuilt.
3. **Command stream:** seed plus scenario setup and any external/director commands.
4. **Event stream:** human-readable consequences emitted by authoritative transitions.

For the first proof, a replay is preferably **seed + versioned scenario + command stream**, validated by periodic state hashes. Full snapshots are useful for debugging and replay seeking but should not become the only replay mechanism.

Cache keys must include every input that can change a result. Invalidations should be explicit and measurable. Path and reachability caches are high-value candidates; caching entire AI decisions is not.

Storage formats should be versioned from day one, even if the first serializer is simple. A deterministic replay tied to a specific build is an acceptable Milestone 0 constraint; cross-version replay compatibility is a later product promise.

## Reviewer criteria derived from the research

Any combat or AI feature is incomplete unless reviewers can answer yes to all applicable questions:

- Does the same seed and command stream reproduce the result on the supported determinism boundary?
- Is the system order explicit?
- Can the viewer see or inspect why an agent made its decision?
- Does the feature create a meaningful tactical or story difference?
- Does it stay inside the tick budget at the 200-agent target?
- Does it avoid per-frame work when a slower cadence is sufficient?
- Are ties resolved by stable IDs or another documented deterministic rule?
- Are caches rebuildable and invalidated by all relevant state changes?
- Does the event log report the important consequence without flooding the viewer?
- Can the feature be disabled or isolated for benchmark comparison?
- Is its data versioned for save/replay compatibility?
- Does the implementation add only the attributes and systems required by the approved milestone?

## Assumptions, uncertainty, and evidence quality

### Verified facts

- The README defines a spectator sandbox, autonomous combat, deterministic seeded replay, dot entities, large maps, and the exact first proof feature set.
- RimWorld officially positions itself as a story generator driven by an AI storyteller and lists moods, needs, injuries, relationships, crafting, factions, and mods as interacting systems.
- .NET 10 is the current LTS release.
- Bevy ECS runs systems in parallel and non-deterministically by default unless ordering is declared.
- Godot documents fixed simulation ticks and renderer interpolation as separate concerns.
- Current linked community threads contain the performance, QoL, accessibility, modding, automation, and scale discussions summarized above.

### Design inferences

- C#/.NET 10 is the best fit for this repository's current scale and iteration profile.
- 200 agents is the correct first acceptance target.
- A 20 Hz simulation is a suitable starting hypothesis.
- Godot is the conditional desktop-shell default; MonoGame may win if a code-first renderer better matches the team's workflow.
- Morale/retreat is the first story-producing layer after basic combat.
- Persistent worlds and physiology should wait.

### Coverage limits

The current-community pass covered Reddit and public web well enough to identify directional themes, with supporting TikTok, YouTube, and Instagram evidence in the local raw reports. X/Twitter was unavailable, one companion pass had an Instagram failure, and the research engine's optional LLM reranker fell back locally. Therefore:

- absence from a source is not evidence that a topic is absent from the community;
- engagement figures are snapshots, not market-size estimates;
- community posts are preference signals, not technical benchmarks;
- all runtime choices still require the release-build spike described above.

Raw social-research artifacts (legacy filenames retained as provenance from
before the Hukbo rename):

- `C:\Users\boazs\Documents\Last30Days\rimworld-like-autonomous-arena-simulation-game-design-and-community-priorities-raw-autonomous-arena.md`
- `C:\Users\boazs\Documents\Last30Days\colony-simulation-game-community-priorities-raw-autonomous-arena.md`

## Bottom line for the Planner Agent

Plan one vertical slice:

> Given a seed and two configured factions, run a headless fixed-tick battle of 200 autonomous dot agents, render it smoothly, expose why agents act, record every decisive event, and replay the same outcome.

If that slice is deterministic, performant, legible, and produces memorable turning points, the project has earned deeper RimWorld-like systems. If it is not, more content will only hide the core problem.
