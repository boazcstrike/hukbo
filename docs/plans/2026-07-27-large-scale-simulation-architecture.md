# Large-Scale Simulation Architecture Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `@executing-plans` to implement this plan task-by-task.

**Goal:** Convert the large-scale simulation research into verified Hukbo
requirements, a current-source baseline, and feature-specific granular
implementation plans ordered by measured value and dependency.

**Architecture:** A Research Agent supplies one evidence packet to independent
Requirements and Existing Code agents. A Task Planner Agent reconciles both
branches and creates one narrow implementation plan per optimization gate;
implementation of simulation behavior is explicitly deferred until the
corresponding feature plan is approved.

**Tech Stack:** Markdown, .NET 10, C#, MonoGame, xUnit, the Hukbo headless
runner, repository knowledge graph with live-source verification, PowerShell,
Git, and primary-source web research.

---

## 1. Locked outcome

This plan produces planning artifacts, requirements, and verified baselines. It
does not itself implement:

- spatial indexing;
- event-buffer changes;
- unit definitions;
- formations;
- pathfinding;
- simulation LOD; or
- parallel simulation.

Each receives a separate dated design and implementation plan only after its
prerequisite gate passes.

Binary completion criteria:

1. The research brief and planning design exist and are mutually linked.
2. Research findings identify evidence class and applicability.
3. Requirements name scenarios, metrics, correctness oracles, and threshold
   status.
4. Existing-code findings cite current live paths and relevant tests.
5. The current benchmark limitations are explicit.
6. The Task Planner produces an ordered backlog with dependencies.
7. The first implementation tranche is measurement, not a broad rewrite.
8. Every later feature has an entry gate and required output plan.
9. A fresh reader can recover the agent flow and task order without this
   conversation.
10. The final diff contains only the approved research and planning files.

## 2. Agent ownership

Use the full contracts in
`docs/plans/2026-07-27-large-scale-simulation-architecture-design.md`.

Writable ownership:

| Agent | Owned artifact | Prohibited overlap |
| --- | --- | --- |
| Research Agent | `docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md` | Source code and numeric requirements |
| Requirements Agent | `docs/plans/YYYY-MM-DD-<feature>-requirements.md` | Existing-code architecture decisions |
| Existing Code Agent | Read-only source map and benchmark notes | Source edits |
| Task Planner Agent | Feature design and implementation plans, excluding `*-requirements.md` | Simulation implementation |
| Independent reviewer | None; read-only | Editing or scope expansion |

If concurrent agents are used, the Research Agent completes the shared evidence
packet first. Requirements and Existing Code may then run concurrently from
that same packet. The Task Planner waits for both branch outputs.

## 3. Task sequence

### Task 1: Record repository and document baseline

**Files:**

- Read: `AGENTS.md`
- Read: `README.md`
- Read: `SIMULATION-GAME-STANDARDS.md`
- Read: `docs/development/testing.md`
- Read: `docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md`
- Read: `docs/research/FORMATION_AND_COLLISION_MECHANICS.md`
- Read:
  `docs/plans/2026-07-27-large-scale-simulation-architecture-design.md`

**Step 1: Record the working tree**

Run:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
```

Expected: all pre-existing changes are visible and none is staged by this task.

**Step 2: Confirm product boundaries**

Verify from `README.md` and `SIMULATION-GAME-STANDARDS.md`:

- tactical battle is the active layer;
- campaign work remains gated;
- determinism is authoritative;
- 200 agents are the acceptance baseline;
- a 500-agent stress result is required; and
- pathfinding, persistence, and multiplayer remain deferred unless separately
  authorized.

Expected: a short scope record in the planner's working notes.

**Step 3: Confirm planned artifacts**

Run:

```powershell
Test-Path docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md
Test-Path docs/plans/2026-07-27-large-scale-simulation-architecture-design.md
Test-Path docs/plans/2026-07-27-large-scale-simulation-architecture.md
```

Expected: all three commands return `True`.

### Task 2: Research Agent evidence audit

**Files:**

- Modify only if a correction is required:
  `docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md`

**Step 1: Build the claim ledger**

Record each external architectural claim as:

```text
Claim:
Evidence class:
Source:
Runtime or content scale:
Applicability to Hukbo:
Limitation:
```

Expected: Total War claims are either confirmed disclosure or clearly marked
inference.

**Step 2: Verify primary links**

Open and verify:

- Creative Assembly siege-AI slides;
- Total War: Arena update-rate interview;
- Warhammer II logic/display interview;
- official Total War optimization material;
- Total War unit-analysis GDC abstract;
- official Warcore FAQ;
- hierarchical pathfinding research;
- continuum crowd research;
- RVO2/ORCA documentation; and
- OpenRA trait documentation.

Expected: each link resolves to the named source or carries a documented access
limitation.

**Step 3: Audit proprietary-detail language**

Search:

```powershell
rg -n -i "uses exactly|source code|implemented as|always|guarantees" `
  docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md
```

Expected: no sentence implies knowledge of an undisclosed Total War internal.

**Step 4: Audit research-to-recommendation transitions**

Confirm every Hukbo recommendation is labeled as a recommendation and every
external practice retains its source context.

Expected: a reader can distinguish evidence from design choice.

### Task 3: Requirements Agent defines the measurement contract

**Files:**

- Read: `SIMULATION-GAME-STANDARDS.md`
- Read: `docs/development/testing.md`
- Create:
  `docs/plans/YYYY-MM-DD-simulation-stage-metrics-requirements.md`
- Create in a later approved tranche:
  `docs/plans/YYYY-MM-DD-simulation-stage-metrics-design.md`

**Step 1: Define required workloads**

The design must specify at least:

```text
duel-200:
  Purpose: canonical compatibility and determinism

duel-500:
  Purpose: required stress report

spread-500:
  Purpose: broad perception with low contact

perception-1000:
  Purpose: living agents remain mutually perceivable

compressed-front-1000:
  Purpose: dense local-neighbor and combat pressure

render-500:
  Purpose: visible client draw and presentation cost
```

Expected: each workload has seed, map, population, tick count, warm-up, and
termination policy.

**Step 2: Define correctness oracles**

Require:

- ordered events;
- outcome;
- final state hash;
- first mismatch tick;
- optimized-versus-naive target equality;
- cold-cache equivalence; and
- single-thread-versus-parallel equivalence where applicable.

Expected: every future optimization identifies which oracle proves unchanged
behavior.

**Step 3: Define required metrics**

Require:

- total tick p50/p95/p99/max;
- per-stage p50/p95/p99/max;
- alive agents per tick;
- spatial candidates and accepted neighbors;
- paths requested, reused, failed, and expanded;
- events and bytes allocated per tick;
- state-hash cost;
- visible agents, sprite submissions, and frame p95/p99; and
- process working set for soak workloads.

Expected: units and collection cadence are declared.

**Step 4: Classify thresholds**

Every threshold must be one of:

- accepted product requirement;
- provisional hypothesis awaiting baseline; or
- unknown pending user or hardware decision.

Expected: the plan does not convert unsourced values into permanent standards.

**Step 5: Define named hardware**

Record processor, memory, GPU, OS, power mode, runtime, configuration, and
display resolution for client benchmarks.

Expected: performance results are reproducible and comparable.

**Step 6: Publish the requirements packet**

Write the accepted workloads, correctness oracles, metrics, threshold classes,
hardware, compatibility constraints, and non-goals to:

```text
docs/plans/YYYY-MM-DD-simulation-stage-metrics-requirements.md
```

Expected: the Task Planner receives a durable input that exists before the
feature design or implementation plan and does not need to infer requirements
from conversation notes.

### Task 4: Existing Code Agent verifies the simulation map

**Files:**

- Read: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Read: `src/Hukbo.Core/Simulation/Scenario.cs`
- Read: `src/Hukbo.Core/Simulation/AgentState.cs`
- Read: `src/Hukbo.Core/Simulation/BattleEvent.cs`
- Read: `src/Hukbo.Core/Combat/CombatRuleset.cs`
- Read: `src/Hukbo.Client/ArenaGame.cs`
- Read: `src/Hukbo.Client/ArenaGame.Rendering.cs`
- Read: `src/Hukbo.Headless/HeadlessRunner.cs`
- Read: `src/Hukbo.Headless/RunReport.cs`
- Read: `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`
- Read: `tests/Hukbo.Core.Tests/DeterminismTests.cs`
- Read: `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs`

**Step 1: Discover through the graph**

Use `search_graph` for:

- `AdvanceOneTick`;
- target selection;
- movement proposals;
- attack proposals;
- state hashing;
- client simulation advancement; and
- headless reporting.

Expected: exact qualified names and relationships are recorded.

**Step 2: Verify graph results against live source**

Open the current `Hukbo.*` paths. If the graph reports former paths or symbols,
mark it stale and do not use stale line numbers as source evidence.

Expected: all final findings cite current live paths.

**Step 3: Record tick-stage reads and writes**

For each current stage, record:

```text
Stage:
Reads:
Writes:
Loops:
Allocations:
Ordering dependency:
Tests:
```

Expected: the Task Planner can identify safe proposal and commit boundaries.

**Step 4: Calculate current asymptotic costs**

At minimum, classify:

- cooldown update;
- target selection;
- movement gather and commit;
- attack gather and commit;
- damage application;
- outcome resolution;
- view update;
- event generation; and
- rendering traversal.

Expected: target selection is identified as quadratic in total combatants.

**Step 5: Record current data layout**

Document:

- `AgentState[]` of class references;
- entity-ID-to-index dictionary;
- reusable damage, movement, attack, and view arrays;
- immutable `CombatRuleset` tables;
- per-tick event-list allocation; and
- snapshot-copy behavior.

Expected: later data-layout proposals preserve public immutability and
determinism.

### Task 5: Existing Code Agent captures the repeatable baseline

**Files:**

- Read: `src/Hukbo.Headless/HeadlessRunner.cs`
- Read: `docs/development/testing.md`
- Create only in the future measurement plan:
  `artifacts/benchmarks/<timestamp>/`

**Step 1: Build Release once**

Run:

```powershell
dotnet build Hukbo.slnx -c Release --no-restore
```

Expected: build passes with no errors.

**Step 2: Run the current scale sample**

Run each workload at least three times after one warm-up:

```powershell
dotnet run --project src/Hukbo.Headless -c Release --no-build -- `
  --agents 200 --ticks 200 --seed 1

dotnet run --project src/Hukbo.Headless -c Release --no-build -- `
  --agents 500 --ticks 200 --seed 1

dotnet run --project src/Hukbo.Headless -c Release --no-build -- `
  --agents 1000 --ticks 200 --seed 1

dotnet run --project src/Hukbo.Headless -c Release --no-build -- `
  --agents 2000 --ticks 200 --seed 1
```

Expected: every run is deterministic and reports no first mismatch.

**Step 3: Record benchmark limitations**

State explicitly:

- one simulation is timed while two run;
- deaths reduce later work;
- there is no warm-up separation inside the runner;
- there are no stage percentiles;
- rendering is excluded; and
- missing future systems make extrapolation unsafe.

Expected: the baseline is not represented as a capacity guarantee.

**Step 4: Do not extrapolate a final agent limit**

Expected: requirements remain separate from current simple-loop results.

### Task 6: Task Planner creates the measurement feature plan

**Files:**

- Read:
  `docs/plans/YYYY-MM-DD-simulation-stage-metrics-requirements.md`
- Create after Tasks 3 through 5:
  `docs/plans/YYYY-MM-DD-simulation-stage-metrics-design.md`
- Create after design approval:
  `docs/plans/YYYY-MM-DD-simulation-stage-metrics.md`

**Step 1: Reconcile requirements and current code**

Record:

```text
Problem:
Required workloads:
Current reporting gap:
Smallest instrumentation surface:
Public report compatibility:
Allocation risk:
Tests:
```

Expected: no spatial, formation, or data-layout changes enter this feature.

**Step 2: Identify exact likely files**

The feature plan must evaluate:

- Modify: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Create or modify:
  `src/Hukbo.Core/Simulation/SimulationStageMetrics.cs`
- Modify: `src/Hukbo.Headless/RunReport.cs`
- Modify: `src/Hukbo.Headless/HeadlessRunner.cs`
- Test: `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs`
- Test: `tests/Hukbo.Core.Tests/DeterminismTests.cs`
- Modify: `docs/development/testing.md`

Expected: the design decides whether metrics live in Core or are injected by a
diagnostic observer; the implementation plan does not guess before approval.

**Step 3: Require failing report-schema tests**

The implementation plan must first test:

- stage names are stable;
- timing counts equal measured ticks;
- candidate and event counters use documented units;
- metrics do not enter state hashes; and
- existing report fields remain compatible.

Expected: tests fail before instrumentation exists.

**Step 4: Require allocation-impact measurement**

Expected: instrumentation overhead is reported and can be disabled or excluded
from normal client execution if material.

**Step 5: Stop for approval**

Expected: no instrumentation code is written from this umbrella plan.

### Task 7: Task Planner creates the deterministic spatial-grid plans

**Dependencies:** Approved and executed measurement feature plan.

**Files:**

- Create later:
  `docs/plans/YYYY-MM-DD-deterministic-spatial-grid-design.md`
- Create later:
  `docs/plans/YYYY-MM-DD-deterministic-spatial-grid.md`

**Step 1: Lock the behavior oracle**

Require a naive reference target selector that preserves:

- living hostile eligibility;
- perception boundary inclusion;
- squared-distance ordering; and
- `EntityId` tie-breaking.

Expected: generated small worlds can compare naive and grid results.

**Step 2: Classify the grid**

Record it as a derived cache that is:

- rebuildable;
- excluded from state hashes and snapshots;
- populated in stable order; and
- verifiable from authoritative positions.

Expected: cache corruption cannot become silent authoritative truth.

**Step 3: Identify exact likely files**

The design must evaluate:

- Create: `src/Hukbo.Core/Simulation/Spatial/UniformGrid.cs`
- Create: `src/Hukbo.Core/Simulation/Spatial/SpatialCell.cs`
- Modify: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Create: `tests/Hukbo.Core.Tests/UniformGridTests.cs`
- Modify: `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`
- Modify: `tests/Hukbo.Core.Tests/DeterminismTests.cs`
- Modify: `src/Hukbo.Headless/RunReport.cs`

Expected: cell size, storage, traversal, and scratch-buffer ownership are
design decisions supported by the new metrics.

**Step 4: Require edge-case tests**

Plan tests for:

- negative or boundary coordinates rejected by scenario validation;
- cell edges and corners;
- radius spanning multiple cells;
- exact perception range;
- equal distance across different cells;
- dead and friendly agents;
- empty cells;
- all agents in one cell; and
- rebuilt-grid equivalence.

Expected: the optimized path cannot change target outcomes.

**Step 5: Require before/after workloads**

Use identical scenario hashes for:

- spread population;
- all-in-perception population;
- dense front;
- 200, 500, 1,000, and 2,000 agents; and
- at least 20 fixed seeds for determinism.

Expected: state and event hashes match the reference mode.

**Step 6: Stop for approval**

Expected: the spatial implementation remains a separate change.

### Task 8: Task Planner creates the event-storage plans

**Dependencies:** Measurement plan complete. Coordinate ownership with the
spatial-grid implementation because both touch `BattleSimulation.cs`.

**Files:**

- Read: `docs/plans/2026-07-27-battle-event-allocation-packing.md`
- Create later:
  `docs/plans/YYYY-MM-DD-reusable-battle-event-storage-design.md`
- Create later:
  `docs/plans/YYYY-MM-DD-reusable-battle-event-storage.md`

**Step 1: Separate two costs**

Distinguish:

1. `BattleEvent` encoded size; and
2. per-tick backing-array allocation.

Expected: storage reuse does not accidentally change replay encoding or require
a preset version bump.

**Step 2: Preserve lifetime contracts**

Require:

- `LastEvents` remains read-only for its documented lifetime;
- `CreateSnapshot` owns immutable copies;
- presentation ingestion cannot observe reused mutated data; and
- pooled arrays are cleared or bounded as required.

Expected: allocation reduction does not introduce aliasing.

**Step 3: Evaluate minimal alternatives**

Compare:

- double-buffered `List<BattleEvent>`;
- reusable arrays plus count;
- `ArrayPool<BattleEvent>` with explicit lifetime; and
- a bounded ring only if event semantics permit it.

Expected: select the smallest option proven by allocation measurements.

**Step 4: Require tests**

Plan failing tests for:

- previous explicit snapshot immutability;
- ordered events unchanged;
- no stale events after a quiet tick;
- capacity growth;
- terminal event retention; and
- identical state and event hashes.

Expected: public behavior remains unchanged.

**Step 5: Stop for approval**

Expected: event encoding changes remain deferred to their existing versioned
debt plan.

### Task 9: Task Planner creates the unit-definition plans

**Dependencies:** Stable measurement is available. Unit-definition planning may
proceed independently from spatial perception when the Requirements Agent
prioritizes content scale and the Existing Code Agent confirms non-overlapping
ownership. Formation work still waits for both applicable prerequisites.

**Files:**

- Create later:
  `docs/plans/YYYY-MM-DD-unit-definition-registry-design.md`
- Create later:
  `docs/plans/YYYY-MM-DD-unit-definition-registry.md`

**Step 1: Lock content requirements**

The Requirements Agent must specify:

- minimum definition count for validation;
- stable identity and versioning;
- allowed composition fields;
- sparse-override policy;
- compatibility failure behavior; and
- content-hash contract.

Expected: "100 types" becomes a testable content contract, not one hundred
handwritten subclasses.

**Step 2: Map current content**

Verify:

- `WeaponId`;
- `ArmorId`;
- `ShieldId`;
- `CombatLoadout`;
- `CombatRuleset`;
- precomputed effective weight tables; and
- scenario-wide movement and combat values.

Expected: the design extends successful immutable precomputation without
duplicating current rules.

**Step 3: Identify likely files**

The design must evaluate:

- Create: `src/Hukbo.Core/Units/UnitDefinitionId.cs`
- Create: `src/Hukbo.Core/Units/UnitDefinition.cs`
- Create: `src/Hukbo.Core/Units/UnitDefinitionRegistry.cs`
- Modify: `src/Hukbo.Core/Simulation/Scenario.cs`
- Modify: `src/Hukbo.Core/Simulation/AgentState.cs`
- Modify: `src/Hukbo.Core/Determinism/StateHasher.cs`
- Create: `tests/Hukbo.Core.Tests/UnitDefinitionRegistryTests.cs`
- Modify: `tests/Hukbo.Core.Tests/ScenarioTests.cs`
- Modify: `tests/Hukbo.Core.Tests/DeterminismTests.cs`

Expected: final paths follow the approved design and current namespace
conventions.

**Step 4: Require validation tests**

Plan tests for:

- stable unique IDs;
- duplicate IDs;
- missing references;
- invalid numeric ranges;
- deterministic content hashes independent of input dictionary order;
- one hundred generated valid definitions;
- sparse overrides;
- unknown schema or definition IDs; and
- same-seed replay identity.

Expected: content errors fail before battle creation.

**Step 5: Require hot-path checks**

Expected: runtime agents use dense validated indexes or direct references and
do not perform reflection or string lookup in per-agent tick loops.

**Step 6: Stop for approval**

Expected: no morale, formations, or abilities are smuggled into the registry
beyond required definition fields.

### Task 10: Task Planner creates the formation plans

**Dependencies:** Unit definitions and spatial perception complete.

**Files:**

- Create later:
  `docs/plans/YYYY-MM-DD-authoritative-formations-design.md`
- Create later:
  `docs/plans/YYYY-MM-DD-authoritative-formations.md`

**Step 1: Define the user-visible capability**

Requirements must state which visible behavior requires formations, such as:

- maintaining a group footprint;
- coordinated advance or withdrawal;
- reserves;
- facing and flank exposure;
- cohesion and rout; or
- shared target allocation.

Expected: formations are not introduced solely as an abstraction.

**Step 2: Define authoritative state**

The design must evaluate:

- formation ID;
- faction;
- member ordering;
- role and tactic;
- anchor, facing, width, and depth;
- destination or target formation;
- slot assignment;
- cohesion and morale; and
- last decision tick.

Expected: every field is authoritative, immutable definition, derived cache, or
presentation state.

**Step 3: Define multirate scheduling**

Specify integer tick schedules for:

- army posture;
- formation tactic;
- member steering; and
- contact combat.

Expected: schedules depend only on authoritative tick and gameplay state.

**Step 4: Require deterministic tests**

Plan tests for:

- member and slot ordering;
- equal-cost slot ties;
- formation split or merge policy;
- member death;
- blocked slots;
- opposing formation crossing;
- retreat and rout;
- same-seed hashes; and
- spectator reason codes.

Expected: the first formation change is narrow and inspectable.

**Step 5: Stop for approval**

Expected: global pathfinding remains a separate plan.

### Task 11: Task Planner creates navigation and local-steering plans

**Dependencies:** Formation state and representative obstacle maps exist.

**Files:**

- Read: `docs/research/FORMATION_AND_COLLISION_MECHANICS.md`
- Create later:
  `docs/plans/YYYY-MM-DD-formation-navigation-design.md`
- Create later:
  `docs/plans/YYYY-MM-DD-formation-navigation.md`

**Step 1: Resolve contact semantics before choosing a solver**

The Requirements Agent must answer the collision handoff questions in
`docs/research/FORMATION_AND_COLLISION_MECHANICS.md`, including:

- hard bodies versus bounded soft compression;
- body radius policy;
- ally, enemy, corpse, and boundary interaction;
- center-to-center versus surface-to-surface attack range;
- crossing, swapping, and simultaneous convergence;
- stable-ID fairness and blocked-agent recovery; and
- maximum movement or correction budget.

Expected: collision behavior is a product rule with binary acceptance criteria,
not an accidental result of iteration order.

**Step 2: Separate the three layers**

Require independent contracts for:

- global route;
- formation corridor and slot goals; and
- local collision avoidance.

Expected: one algorithm is not expected to solve all three problems.

**Step 3: Start with the simplest global solver**

Plan deterministic A* or navmesh search with stable neighbor and priority-queue
tie-breaking.

Expected: HPA, flow fields, and ORCA remain alternatives triggered by measured
failure, not initial dependencies.

**Step 4: Classify path data**

Record terrain walkability/version as authoritative when dynamic. Record path
results, corridors, and flow fields as derived caches.

Expected: save/load and cache rebuild produce equivalent next ticks.

**Step 5: Require reference tests**

Plan tests for:

- shortest valid path on small maps;
- equal-cost ties;
- unreachable goals;
- narrow passages;
- moving blockers;
- formation-width constraints;
- cache invalidation;
- retreat routes; and
- save/load rebuild equivalence when persistence exists.

Expected: path quality and performance have separate metrics.

Also require the collision regressions identified by the companion research:
exact co-location, head-on swaps, crossings, packed fronts, and map corners.

**Step 6: Stop for approval**

Expected: no third-party navigation library is selected without dependency and
determinism review.

### Task 12: Task Planner creates deterministic-parallelism plans

**Dependencies:** A measured sequential stage materially consumes the tick
budget and its inputs are immutable during proposal gathering.

**Files:**

- Create later:
  `docs/plans/YYYY-MM-DD-deterministic-proposal-parallelism-design.md`
- Create later:
  `docs/plans/YYYY-MM-DD-deterministic-proposal-parallelism.md`

**Step 1: Identify the exact parallel candidate**

Choose one stage only, such as:

- spatial candidate evaluation;
- movement proposal gathering; or
- independent path queries.

Expected: no shared authoritative writes occur inside workers.

**Step 2: Define partition and merge**

Require:

- stable input ranges;
- worker-local bounded output;
- stable proposal keys;
- deterministic merge;
- ordered commit; and
- a single-thread reference mode.

Expected: thread completion order cannot affect results.

**Step 3: Require equivalence tests**

Run sequential and parallel modes across:

- named golden scenarios;
- generated bounded worlds;
- 20 fixed seeds;
- repeated high-contention runs; and
- supported processor counts.

Expected: events, hashes, outcomes, and first mismatch remain identical.

**Step 4: Require measured speedup**

Report total and stage p50/p95/p99/max, scheduling overhead, allocations, and
core utilization.

Expected: parallel code is retained only when the representative workload
improves materially.

**Step 5: Stop for approval**

Expected: concurrency is the last optimization layer in this planning chain.

### Task 13: Task Planner publishes the ordered backlog

**Files:**

- Modify: this plan only if priorities or dependencies change
- Create: feature-specific plans named in Tasks 6 through 12 as gates pass

**Step 1: Publish priority**

Use:

| Priority | Feature | Entry gate | Completion proof |
| ---: | --- | --- | --- |
| 0 | Stage metrics | Requirements and current baseline | Stable metrics with bounded overhead |
| 1 | Spatial perception | Stage metrics | Naive equivalence and measured improvement |
| 1 | Reusable events | Allocation baseline | Immutable behavior and lower allocation |
| 2 | Unit definitions | Stage metrics and accepted content requirement | 100-definition validation and replay stability |
| 3 | Formations | Spatial perception and definition registry | Deterministic coordinated group behavior |
| 4 | Navigation | Representative obstacle maps | Reference-path correctness and budgets |
| 5 | Parallel proposals | Sequential bottleneck measured | Hash equivalence and material speedup |

Expected: independent Priority 1 work is serialized if it overlaps the same
source file.

**Step 2: Publish blockers**

List missing user decisions, unknown thresholds, dependency risks, and
pre-existing failures separately.

Expected: a blocker is not hidden inside an implementation task.

**Step 3: Require approval per plan**

Expected: completion of one gate does not automatically authorize the next
feature.

### Task 14: Reader testing and final verification

**Files:**

- Verify:
  `docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md`
- Verify:
  `docs/plans/2026-07-27-large-scale-simulation-architecture-design.md`
- Verify:
  `docs/plans/2026-07-27-large-scale-simulation-architecture.md`

**Step 1: Check Markdown whitespace**

Run:

```powershell
git diff --check -- `
  docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md `
  docs/plans/2026-07-27-large-scale-simulation-architecture-design.md `
  docs/plans/2026-07-27-large-scale-simulation-architecture.md
```

Expected: no errors.

**Step 2: Check internal paths**

Extract repository-relative Markdown paths and confirm each existing referenced
file resolves. Treat future dated-plan placeholders as declared future outputs,
not broken current links.

Expected: every current internal reference exists.

**Step 3: Ask fresh-reader questions**

Ask an independent reader:

1. What is known versus inferred about Total War?
2. What are the two meanings of scale?
3. What is Hukbo's current first asymptotic bottleneck?
4. Why are Requirements and Existing Code separate branches?
5. What must happen before a spatial-grid implementation?
6. Which changes can be planned independently?
7. How are 100 unit definitions represented without 10,000 special cases?
8. Why do formations precede navigation?
9. When is parallelism allowed?
10. Does this umbrella plan authorize code changes?

Expected: answers point to the correct documents and do not invent approval.

**Step 4: Resolve review findings**

Classify findings:

- Critical: fabricated source, destructive direction, or determinism break;
- High: incorrect dependency, missing correctness oracle, or accidental
  implementation authorization;
- Medium: ambiguity or maintainability issue; and
- Low: optional wording or formatting.

Resolve all Critical and High findings.

**Step 5: Inspect final diff**

Run:

```powershell
git status --short
git diff --stat -- `
  docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md `
  docs/plans/2026-07-27-large-scale-simulation-architecture-design.md `
  docs/plans/2026-07-27-large-scale-simulation-architecture.md
git diff -- `
  docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md `
  docs/plans/2026-07-27-large-scale-simulation-architecture-design.md `
  docs/plans/2026-07-27-large-scale-simulation-architecture.md
```

Expected: only the approved research and planning artifacts appear in this
task's diff.

## 4. Completion report

Return:

```text
Implemented:
- Large-scale simulation research baseline.
- Agent planning pipeline and contracts.
- Ordered feature-plan backlog and gates.

Verification:
- Source and link audit.
- Current-source and benchmark verification.
- Markdown and final-diff checks.
- Independent reader result.

Key decisions:
- Requirements and Existing Code remain independent inputs.
- Instrumentation precedes optimization.
- Algorithmic fixes precede concurrency.
- Every major subsystem receives a separate approved plan.

Files changed:
- Research brief.
- Planning-pipeline design.
- Granular umbrella plan.

Unresolved:
- Thresholds awaiting named hardware or product decisions.
- Feature-specific designs not yet authorized.
```

Do not claim implementation of any simulation feature from completion of this
planning plan.
