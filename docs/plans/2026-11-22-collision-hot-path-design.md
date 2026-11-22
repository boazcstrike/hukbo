# Collision hot path — design

**Status: implemented 2026-11-22; measured results are in `docs/development/measurement-history.md` under "Collision hot path, before and after".** The user set the goal
"compare three rounds on the standards of implementation with We March as One and
my game, and implement the optimized and carefully good architectures based on
research". The plan document `docs/plans/2026-11-22-collision-hot-path.md`
carries the ordered task list.

**Baseline: `main` at `073f6ca`, 2026-11-22.** File and line references are to
that baseline. Symbol names are the stable reference; line numbers are not
renumbered.

---

## 1. The question this document answers

The first We March as One package (the 2026-11-21 optimizations plan) replaced
the all-pairs target scan with a ring search. Its own after-profile, recorded in
`docs/development/measurement-history.md` under "Where the tick goes at 1,000
agents, after", shows where the tick went next:

| Stage | Share of `AdvanceOneTick` at 1,000 agents |
| --- | --- |
| `ResolveCollisions` | 49.9% |
| `MeasureCollision` | 22.5% |
| `SelectTargetsAndIntents` | 11.8% |
| `GatherMovementProposals` | 7.2% |
| everything else | under 9% combined |

Collision work is now 72% of the tick, and the second-largest single stage,
`MeasureCollision`, produces derived metrics that nothing in the simulation
reads.

Three read-only comparison rounds were run against the We March as One
decompile. Each round compared one area of implementation standards:

1. collision and spatial structures
2. simulation data layout and tick-stage structure
3. client rendering and presentation

The full comparison is in the 2026-11-22 research post on the bo-life dashboard,
titled "We March as One vs Hukbo: three rounds on implementation standards".
This document specifies the three changes the rounds support. All three are
hash-neutral, so no preset version and no golden recapture is needed. A moved
digest means the change is wrong.

## 2. What the rounds found

### 2.1 The collision grid pays a hash lookup for every cell it visits

`CollisionUniformGrid` keys its cells with a `Dictionary<long, int>`
(`CollisionUniformGrid.cs:77`). Inside `ResolveCollisions`, each mover tries up
to 14 candidate positions. Each candidate calls `AnyOverlapUnchecked` on both the
committed and the pending grid, and each call visits 9 cells. That is up to 18
dictionary lookups per candidate, before the per-agent inserts and removals.

We March as One builds its broadphase as dense arrays over a bounded box: count
per cell, prefix-sum, then scatter (`SpatialHashSystem.cs:231-263`), with cell
lookup as an array load. Hukbo already uses the same dense layout in two newer
structures, `TargetingGrid` (`TargetingGrid.cs:275-314`) and `SeparationPass`
(`SeparationPass.cs:341-368`). The collision grid predates both and never adopted
it.

The parallel parts of the We March as One build do not transfer and are not
needed. Its `Interlocked` cell counts and its per-cell salted-hash sort
(`SortCellsJob.cs:22-51`) exist only because a parallel scatter is
nondeterministic. A single-threaded scatter is already deterministic.

### 2.2 The simulation looks up agents by ID through a dictionary

`BattleSimulation._agentIndexes` is a `Dictionary<ulong, int>`
(`BattleSimulation.cs:29`). It is read about 30 times in the file, several of
them per living agent per tick: contingent states (`:2011`, `:2021`),
proposals (`:2315`), footwork (`:2557`), collision measurement (`:5214`,
`:5215`, `:5248`) and attacks (`:5505`, `:5587`).

In every production battle, entity IDs are dense: agent `i` has
`EntityId = i + 1` (`BattleSimulation.cs:812`, `:833`). An array indexed by ID
answers the same question with one bounds check.

### 2.3 `MeasureCollision` rebuilds a second grid and sorts every pair

`MeasureCollision` (`BattleSimulation.cs:5187-5270`) runs once per tick and:

- copies every agent into `_collision.Bodies`
- runs a full `Grid.Rebuild`, which sorts the cell traversal order
  (`CollisionUniformGrid.cs:542`) and then sorts the whole pair list (`:558`)
- for every pair, does two dictionary lookups and one integer square root
- makes a second pass over agents for the attack-reach front

Its output, `CollisionTickMetrics`, is not hashed and is not an event. It is read
only by `HeadlessRunner` (`HeadlessRunner.cs:449`), which accumulates it into
`RunReport`. The client never reads it. Of everything it computes, only two
counts and one maximum come from the pair list. None of them needs the list to
be materialised or sorted:

- **candidate pairs:** the number of pairs within the grid's band
- **contact pairs:** the same count restricted to opposite factions
- **maximum penetration:** `max(2R − ⌊√d²⌋)` over all pairs, with a floor of 0

Integer floor square root does not decrease as its argument grows, so the
maximum of `2R − ⌊√d²⌋` over all pairs equals `2R − ⌊√(min d²)⌋`. One square root
per tick replaces one per pair.

### 2.4 What the rounds decided not to do

- **A Jacobi (parallel-style) collision resolver.** It changes which move each
  agent gets, so it moves both hashes and needs a preset. V17's `SeparationPass`
  is already the opt-in Jacobi path.
- **A struct-of-arrays split of `AgentState`.** At 1,000 agents the working set
  is roughly 200 KB and fits in L2, so the payoff is small. The blast radius is
  27 full-array loops and many reflection-based tests.
- **Splitting `BattleSimulation` into stage classes.** Tests reach private
  members by reflection on `typeof(BattleSimulation)`, so stage classes would
  break them. A partial-class split by stage is feasible, but it would make
  stale hundreds of `BattleSimulation.cs:NNNN` citations across the live
  documents. It is recorded as a recommendation, not done here.
- **Client rendering changes.** The newest frame measurement (the 2026-07-29
  Phase 2 matrix) puts a 1,000-agent frame at about 1.1 ms of real draw work,
  and an instanced backend was already judged NO-GO. That measurement predates
  several layers, so the right next client step is a re-baseline (plan row TU-1
  of the 2026-08-14 thousand-unit plan), not a speculative optimisation.
- **Skipping `MeasureCollision` when nothing reads it.** This would remove the
  stage from client battles entirely, but it adds a mode in which
  `LastTickCollision` is silently stale. Making the stage cheap (section 5)
  fixes the cost for every caller without that mode. It can be revisited if
  section 7's measurement shows the stage is still large.

## 3. Change A — dense cell index in `CollisionUniformGrid`

### 3.1 Layout

The grid gains optional bounds, given once at construction: a minimum and
maximum cell coordinate on each axis. Inside the bounds, a cell's head slot is
`_denseHeads[(cy − minCy) × width + (cx − minCx)]`, an `int[]` with `NoSlot`
meaning empty. The per-cell chain (`_nextSlotInCell`) is unchanged.

Every cell outside the bounds, and every cell of a grid built without bounds,
goes through the existing dictionary. Correctness therefore never depends on the
bounds being right. They only decide which path is fast. Today's behaviour,
for a grid built without bounds, is exactly the dictionary path.

### 3.2 Who passes bounds

- **`CollisionResolver`** knows the map (`_mapWidthRaw`, `_mapHeightRaw`), and
  clamps every candidate centre into it (`CollisionResolver.cs:658-662`). It
  passes the map rectangle, padded by one cell on every side, to both its grids.
- **`CollisionScratch.Grid`**, the metrics grid, gets the same padded map
  rectangle.

Default map, 1,280 × 720 world units: at the resolver's cell size of `2R`
(8.5 wu at the default body radius of 4.25), the dense array is about 153 × 87,
roughly 13,300 `int` cells, or 53 KB per grid.

### 3.3 Cap

If the padded rectangle would exceed `MaximumDenseCells` (1,048,576 cells,
4 MB), the grid uses the dictionary for everything, exactly as today. A
1,000,000-unit map (`Scenario.MaximumMapDimension`) can never allocate a huge
array.

### 3.4 Clearing

`Clear()` must reset only the dense cells that were used, not the whole array,
or a warm tick would pay 13,300 stores per grid per tick. The grid already keeps
`_cellKeys` and `_cellCount` for the cells it has populated, so `Clear()` walks
those and resets their dense heads. The warm-tick allocation tests must stay at
zero bytes.

### 3.5 What must not change

- The query contract: `AnyOverlap`, `AnyContact` and `AnyCoincident` return the
  same answer as the naive O(n²) oracles in the existing tests
  (`CollisionUniformGridTests`, `CollisionResolverTests` including
  `Resolve_MatchesTheUnacceleratedAlgorithmOnEverySeededLayout`).
- `Pairs` content and order, which the existing pair tests pin.
- Every state and event hash in every gate workload.

## 4. Change B — dense entity index in `BattleSimulation`

### 4.1 Layout

At construction, after the duplicate-ID check, `BattleSimulation` computes the
largest entity ID. If that maximum is no more than
`4 × agents.Length + 16`, it builds `int[] _agentIndexByEntityId` of length
`maxId + 1`, filled with `-1` except at each agent's ID. Otherwise it keeps the
dictionary, so a test that constructs agents with sparse IDs behaves exactly as
today.

Two private helpers replace every use of `_agentIndexes`:

- `TryGetAgentIndex(ulong entityId, out int index)`, which returns `false` for
  ID 0, any ID beyond the array, and any unused slot. That matches
  `Dictionary.TryGetValue`, including the `leaderEntityId == 0` checks.
- `AgentIndexOf(ulong entityId)`, which throws `KeyNotFoundException` when the
  ID is unknown, exactly as the dictionary indexer does.

### 4.2 What must not change

No lookup may change result, and no exception may change type. Every gate
workload's hashes stay exactly as recorded in `docs/development/testing.md`.

## 5. Change C — count-only `MeasureCollision`

### 5.1 New grid query

`CollisionUniformGrid` gains a method that walks the same pairs
`GeneratePairs` would produce and returns, without materialising them:

- the pair count
- the opposite-faction pair count
- the minimum squared distance over all pairs, or "none" when there are no pairs

Its predicate must be exactly the predicate `GeneratePairs` uses. That includes
the band distance, the dead-body handling, and each unordered pair visited once.
Faction is not a `CollisionBody` field, so the caller supplies it, either as a
per-slot faction array or as a callback-free span the grid reads by body index.

`Rebuild` and `Pairs` stay for the tests and for any other caller. The metrics
path stops calling them.

### 5.2 `MeasureCollision`

- It inserts bodies straight from `_agentStates` into the metrics grid with
  `Clear()` and `Insert`. There is no intermediate `Bodies` list unless the
  grid's API requires one.
- It calls the count query, and computes penetration as
  `max(0, 2R − ⌊√minD²⌋)`, or 0 when there are no pairs.
- The attack-reach front pass is unchanged, except that its target lookup uses
  change B's helper.

### 5.3 Equivalence

`CollisionTickMetrics` must be field-for-field identical to today's on every
tick. The proof is a test-only copy of the current `MeasureCollision` logic,
built on the existing naive pair oracle. The test compares the two over seeded
battles under every registered movement preset that can run with the shipped
combat preset, including V17, where penetration is nonzero. Section 6 lists the
other proofs.

## 6. How the change is proven

1. The existing naive-oracle differential tests for the grid and the resolver
   pass unchanged.
2. A new differential test for change B: under seeded battles, every
   `TryGetAgentIndex` and `AgentIndexOf` result equals the dictionary's.
3. A new per-tick equivalence test for change C, as in section 5.3.
4. The warm-tick allocation windows stay at zero.
5. `./scripts/verify.ps1` is green for both games. All seven Hukbo workload hashes
   equal the values in `docs/development/testing.md`. Sandata's
   three `RoomSweepAngleHouseTests` failures were already failing on `main` and
   are reported separately, never as part of this package's result.
6. `HeadlessRunnerTests`, which require byte-identical `RunReport` JSON across
   same-seed runs, pass, and a before/after `RunReport` for seed 1 at 1,000
   agents is identical in every field except timings and allocation.

## 7. How the gain is measured

Interleaved Release binaries, before (`073f6ca`) and after, built with
`dotnet build src/Hukbo.Headless -c Release -o <dir>`. Combat V8, movement V14,
seed 1, at 200, 500, 1,000 and 2,000 agents. Each repeat runs both binaries back
to back, alternating which goes first, with at least nine repeats per size. The
headline is the median across pairs of each run's own p50 and p95. The results
are recorded in `docs/development/measurement-history.md`, with a `dotnet-trace`
stage-share table at 1,000 agents in the same form as the 2026-11-21 one.

**Acceptance:**

- p50 falls by at least 15% at 1,000 agents.
- Neither p50 nor p95 regresses by more than 5% at 200 agents.
- No hash moves.

If acceptance fails, the package does not merge. The measurement is recorded
anyway.

## 8. Section 10 questions

| # | Question | Answer |
| --- | --- | --- |
| 1 | User-visible outcome | None in behaviour. Faster ticks at large battles. |
| 2 | Tick stage, state | `ResolveCollisions` and `MeasureCollision`, plus every agent lookup. No authoritative state changes. |
| 3 | Units, bounds, same-tick rule | Cell coordinates are `floor(raw / cellSize)` as today. Dense bounds are the map padded by one cell. No same-tick rule changes. |
| 4 | Ordering, random stream | Unchanged. The grid's queries are existential, and pair traversal order is not observable in the counts. No randomness. |
| 5 | Cache | The dense cell array and the ID array are derived scratch, rebuilt or reset every tick or at construction, never hashed and never snapshotted. |
| 6 | Save, event, version | Presentation-neutral and hash-neutral. No preset version. |
| 7 | Complexity, benchmark | The same asymptotics with smaller constants. Section 7 is the benchmark. |
| 8 | Spectator explanation | Not applicable: no behaviour changes. |
| 9 | Tests that fail first | Change B's and change C's differential tests are written against the current code first and must pass both before and after. Their job is to catch a difference, not to show one. |

## 9. Risks

- **A stale dense head after `Clear()`.** If one populated cell is missed, a
  later query sees a ghost body. The naive-oracle tests over many seeded layouts
  are the guard. `Clear()` must reset from the populated-cell list, never from a
  separate bookkeeping copy.
- **Bodies outside the map.** These include test layouts and spawn positions
  before the first clamp. They fall through to the dictionary path, so they are
  correct by construction and only slower.
- **Measurement noise.** The 200-agent row has about 15% run-to-run spread. Use
  more repeats there before calling a regression, as the 2026-11-21 measurement
  did.
