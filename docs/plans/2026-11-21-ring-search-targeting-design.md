# Ring-search targeting — design

**Status: design.** The plan document `docs/plans/2026-11-21-wmao-optimizations.md`
carries the ordered task list. The user authorized execution of this package on
2026-11-21, after the documents are written.

**Baseline: `main` at `0ccc0d3`, 2026-11-21.**

---

## 1. The question this document answers

`BattleSimulation.SelectTargetsAndIntents` gives every living agent a target by
scanning the whole agent array: one outer loop at `BattleSimulation.cs:1399` and
one inner loop at `:1451`. At 1,000 agents that is a million candidate visits
per tick. The 2026-08-14 measurements put the cost curve's exponent at 2.13
between 1,000 and 2,000 agents, which is what an all-pairs scan looks like.

Section 4.1 of `docs/plans/2026-08-14-thousand-unit-performance-design.md`
argued that no spatial index can help. Perception (2,048 wu) covers the whole
1,280 × 720 map, so there are "no far cells", and an index would "change what the
query means". That conclusion is right for **radius pruning** and wrong for
**nearest-neighbour search**. This document explains why and specifies a
replacement that selects the identical target for every agent on every tick, so
neither hash moves and no preset version is needed.

The idea comes from We March as One, a Unity DOTS battle game, whose target
search walks rings of grid cells outward from the unit and stops once the best
enemy found is closer than the next ring could be (`SeekJob.cs:982-999` in its
decompiled source). Only the idea transfers. That game uses `float` and its tie
order is not stable. This design uses raw fixed-point integers and keeps
Hukbo's tie-break exactly.

## 2. Why a ring search prunes when radius pruning cannot

A radius-bounded query skips a cell because the cell lies outside the radius.
With perception larger than the map, no cell ever does.

A nearest-neighbour ring search skips a cell for a different reason: **an enemy
closer than anything in that cell has already been found.** The bound comes
from the best candidate so far, not from perception. In a battle, an agent's
nearest enemy is usually within a few cells, so the search ends after a few
rings regardless of how large the perception range is.

### 2.1 The selection rule is a total order

The current comparison at `BattleSimulation.cs:1536-1539` keeps the candidate
with the smallest squared distance, and among equal distances the smallest
`EntityId`. That rule orders every candidate, so the winner does not depend on
the order candidates are visited in. A search that visits a different subset in
a different order selects the same target **if and only if** the true winner is
inside the visited subset.

### 2.2 The termination bound

Let `C` be the cell side in raw units and let the query point `q` lie in cell
`c_q`. Every point in a cell whose Chebyshev offset from `c_q` is `k ≥ 1` lies at
least `(k − 1) · C` from `q` along the axis of that offset. So every candidate in
ring `k` or beyond has squared distance at least `L_k² = ((k − 1) · C)²`.

The search visits rings `k = 0, 1, 2, …` and **stops before ring `k` when
`L_k² > best`**, where `best` is the smallest squared distance found so far.
Every unvisited candidate then has squared distance strictly greater than
`best`, so none can win outright and none can tie. The comparison must be strict
`>`. With `≥`, the search could stop while an equally distant enemy with a lower
`EntityId` sits in the next ring, and the tie-break would change.

**Correction, 2026-11-21, found in implementation.** The last two sentences
overstate the risk. Cells are half-open and coordinates are integers, so a query
point lies at most `C − 1` raw units inside its own cell, and every candidate in
ring `k` is at least `(k − 1) · C + 1` away along the offset axis. The bound
`L_k²` is therefore never attained, and `≥` would give the same answers as `>`.
The implementation keeps the strict comparison anyway, because it costs nothing
and stays correct if the cell assignment ever changes.

The search also stops when `L_k² > perception²`, because no later candidate can
pass the perception test. Each visited candidate still goes through the existing
perception test at `:1501-1505`, unchanged.

The search also stops when ring `k` lies wholly outside the grid's occupied
bounds. Empty rings between the query cell and those bounds are skipped in one
step, because they contain no cells.

### 2.3 What the proof needs from the implementation

- The grid holds **exactly** the living agents of one faction, built from the
  same `XRaw`, `YRaw`, and `IsAlive` values the brute-force scan reads.
- Cell assignment and the ring bound use the same `C` and the same origin, with
  integer arithmetic only.
- `best` is compared in the same checked `long` squared distance
  `SquaredDistance(agent, candidate)` the scan uses today.

## 3. The loop has more than one output

The inner loop at `:1451-1544` produces four results, and each needs its own
treatment.

| Output | Enabled by | Shipped (combat V8, movement V14) | Treatment |
| --- | --- | --- | --- |
| Target and `selectedDistance` (`:1536-1546`) | Always | On | Ring search |
| `nearestMeleeThreatSquared` (`:1529-1534`) | `UsesBattlefieldRealism`, movement V10–V15, and only for a ranged actor (`:1448-1449`) | On for ranged actors | Second minimum in the same traversal, with its own `best` |
| `ObserveEnemy` for every perceivable enemy (`:1514-1518`) | `UsesEquipmentRelativeFootwork`, movement V6, V7, V9 only | Off | Keep the brute-force scan |
| `ObserveAlly` for every living ally (`:1462-1474`) | Same flag | Off | Keep the brute-force scan |

`_factionSurvivingCompositions` (`:1431`) is per actor, not per candidate, and is
unaffected.

The melee-threat value is a plain minimum over squared distances with no
tie-break (`:1527-1528` says so), so any exact search that visits every melee
enemy that could beat its running minimum returns the same value. One traversal
serves both results. It continues until `L_k²` exceeds **both** bests that the
actor needs, and it skips the threat bookkeeping for an actor that does not
observe threats.

When `derivesLocalContext` is true, the accumulator observes every perceivable
enemy and every ally. That is all-pairs by definition, so the function keeps the
current scan for those presets. None of them ships, and none runs in the gate,
so the fallback costs nothing where it matters.

Nothing after the loop needs the full candidate set. `YieldsRegroupingToEngagement`
(`:1631-1636`), `ResolveContingentStates` (`:1870`), `GatherMovementProposals`
(`:2162`), and `ApplyEvasiveFootwork` (`:2404`) read only `TargetEntityId` and
`selectedDistance`.

## 4. The grid

A new internal sealed class, `TargetingGrid`, in
`src/Hukbo.Core/Simulation/TargetingGrid.cs`. It is a **derived accelerator**
under section 6 of `SIMULATION-GAME-STANDARDS.md` and the "uniform grid is a
derived oracle" rule: never hashed, never snapshotted, never persisted. Its only
contract is that the selected targets equal those of the O(n²) scan.

### 4.1 Why not reuse `CollisionUniformGrid`

- It is rebuilt in `MeasureCollision` (`BattleSimulation.cs:4943`), after
  `CommitMovement` and before `GatherAndCommitAttacks`. Positions are current
  at the next `SelectTargetsAndIntents`, but **liveness is stale**: agents that
  die in the attack stage are still in it.
- It is a sparse `Dictionary<long, int>` keyed by packed cell and throws on
  negative coordinates (`CollisionUniformGrid.cs:513-517`).
  `BattleSimulation.CreateForTesting` places agents without clamping
  (`:845-865`), so test scenarios can have negative coordinates.
- Its cell is sized for collision (11,776 raw), not for nearest-enemy search.

### 4.2 Layout

- One grid per faction. Hukbo fields exactly two factions, so there are two
  instances.
- **Rebuilt once per tick**, at the top of `SelectTargetsAndIntents`, after
  `ComputeRallyAgents` and `ComputeRallyEngagement`. Positions change only in
  spawn placement (`:1075-1079`, `:1155`) and `CommitMovement` (`:4882-4883`),
  and liveness changes only in the attack stage, so the grid is exact for the
  whole function.
- **Origin and extent come from the faction's living bounding box**, computed in
  the same pass. Negative and unclamped coordinates are therefore fine.
- **Cell index** is `(x − originX) / C` and `(y − originY) / C` in `long`
  arithmetic. Both operands are non-negative, so truncating division is floor.
- **Storage is compressed rows.** A counting pass fills `cellCounts`, a prefix
  sum gives `cellStart`, and a scatter pass writes agent indexes into
  `cellAgents`. The arrays are preallocated and grow only when capacity is
  short, so a warm tick allocates nothing. No dictionary is used.
- **Scatter order is ascending agent index**, which is ascending `EntityId`
  (agents are sorted at creation, `:863`). The selection rule does not need any
  order, but a fixed one keeps debugging reproducible.
- **Cell count is capped** at 256 per axis. If the bounding box would need more,
  `C` grows for that tick by the smallest integer factor that fits. The bound in
  section 2.2 holds for any `C`.

### 4.3 Cell size

`SIMULATION-GAME-STANDARDS.md` says a uniform grid's cell size is measured, not
guessed. The plan's measurement task runs the shipped presets at 500, 1,000, and
2,000 agents with `C` at 16, 32, 64, and 128 wu, and the fastest value becomes
the constant. The starting proposal is 32 wu (32,768 raw): about four body
diameters, and roughly one occupied cell per few agents in a packed front.

### 4.4 The query

For each living actor:

1. Pick the enemy faction's grid.
2. Compute the actor's cell `c_q`, which may lie outside the grid. An actor
   left of or above the enemy's bounding box has a negative offset, and C#
   integer division truncates toward zero, so this step must use floor
   division explicitly. Truncation would put the actor one cell too close and
   break the bound in section 2.2.

   **Correction, 2026-11-21, found in implementation.** Truncation does not
   break the bound. It moves the actor's computed cell toward the grid, and the
   actor is then really farther from every occupied cell than the ring bound
   assumes. The bound only becomes looser and the search visits more rings. A
   mutation test confirmed this: replacing floor division with truncation
   fails no test, while weakening the ring bound itself fails 169. Floor
   division is kept, because it gives the tighter bound.
3. Set the first ring to the Chebyshev distance from `c_q` to the grid's cell
   bounds. Rings closer than that hold no cells.
4. For each ring `k`, stop if `L_k² > best` for every result the actor needs,
   or if `L_k² > perception²`, or if the ring lies wholly outside the grid.
5. Otherwise visit the ring's cells that intersect the grid. Two full rows and
   two edge columns, clipped to the grid. For each candidate, run the existing
   per-candidate body: perception test, melee-threat minimum, and target
   comparison, unchanged.

The per-candidate body must be one shared method used by both the ring search
and the brute-force fallback. Two copies of the comparison would drift.

### 4.5 Cache declaration

Section 6 of the standards requires every cache to declare these properties.

| Property | Value |
| --- | --- |
| Source | `_agentStates` positions and liveness at the start of `SelectTargetsAndIntents` |
| Key | Faction, then cell |
| Value | Agent indexes |
| Size bound | Living agents of the faction; at most 256 × 256 cells |
| Lifetime | One call of `SelectTargetsAndIntents` |
| Invalidation | Full rebuild every tick |
| Counters | None in Core; cost is measured externally with `dotnet-trace` |
| Cold-cache equivalence | The differential tests in section 6 |

## 5. A second query that can share the grid

`FindNearestMeleeThreatPosition` (`:5944-5979`) is another full scan. It runs in
`GatherMovementProposals` for each ranged agent whose `IsThreatened` is set
(`:2204-2207`), breaks ties on `EntityId`, and applies **no** perception test.
Positions and liveness do not change between `SelectTargetsAndIntents` and
`GatherMovementProposals`, so the same grids are still exact there.

This is a separate, later task. It must keep the "no perception test" semantics,
so its termination uses only `best` and the grid bounds. It moves forward only if
the measurement after the main change shows the function still matters.

## 6. How the change is proven

The change is hash-neutral by construction or it is wrong. **No golden value is
recaptured in this work.** A moved digest means the implementation is wrong.

1. **Unit differential test.** A test-only `NaiveNearestEnemy` reference in
   `tests/Hukbo.Core.Tests/` reproduces today's loop. It is compared against
   `TargetingGrid` over seeded scenarios built with `SplitMix64`. The scenarios
   include: agents on a lattice so that equal distances are common; negative
   coordinates; perception smaller than the map; dead agents; an empty enemy
   faction; coincident agents; and an enemy in a far ring with a lower
   `EntityId` at a distance equal to a near one. That last case is the one the
   strict `>` exists for.
2. **Whole-simulation differential test.** For every registered movement preset
   that does not derive local context, run a 300-agent battle for 2,000 ticks
   twice: once normally and once through an internal test seam that forces the
   brute-force path. Assert the same state hash and event hash on every tick.
3. **Gate.** All seven `./scripts/verify.ps1` Hukbo workloads print their
   recorded hashes unchanged.
4. **Allocation.** A warm-tick allocation window at 1,000 agents allocates
   nothing. The existing windows use two agents and cannot see grid growth.
5. **Speed.** `./scripts/benchmark.ps1` under combat V8 and movement V14 at 200,
   500, 1,000, and 2,000 agents, before and after, recorded in
   `docs/development/measurement-history.md`. Acceptance: tick p50 and p95 fall
   at 1,000 and 2,000 agents, and neither regresses by more than 5% at 200.

The internal test seam in item 2 selects between two implementations of the
**same** behaviour. It is not a configuration field that selects a different
behaviour, and it is not reachable from `Scenario` or the client.

## 7. Risks

- **A wrong ring bound.** An off-by-one in `L_k` or in the starting ring silently
  drops the true winner in rare geometries. Test 1's far-ring tie case and the
  lattice scenarios exist for this.
- **A slow opening.** Before contact, the armies stand far apart and the nearest
  enemy is many rings away. Starting at the enemy grid's bounds (section 4.4,
  step 3) removes the empty rings, and the clipped ring visits only cells inside
  the enemy's bounding box. Measure the opening ticks separately before
  claiming a win.
- **One giant faction against a handful.** When one faction is down to a few
  agents spread across the map, each query may walk many empty cells. The grid
  cap of 256 per axis bounds this, and the measurement includes a last-stand
  phase.

## 8. What this design does not do

- It does not change any target, intent, or event. No preset version.
- It does not stagger, cache, or skip target selection on any tick. A staggered
  rescan (as We March as One does) changes behaviour and needs its own preset.
- It does not touch the footwork scans (`IsLaneClearOfAllies`,
  `HasAllyWithinPursuitSupport`). They run only under V6, V7, and V9.
- It does not parallelize the tick.

## 9. Relation to the thousand-unit performance plan

That plan's TU-8 (a struct-of-arrays slice) makes each candidate visit cheaper.
This design removes most of the visits. The two stack, and this one goes first
because it attacks the exponent rather than the constant. Section 4.1 of the
2026-08-14 design and the plan line that rules out a spatial index get a dated
correction pointing here.
