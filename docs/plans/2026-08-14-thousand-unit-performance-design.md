# Thousand-unit performance — design

**Status: design only. This document authorizes nothing.** The plan document
that accompanies it, `docs/plans/2026-08-14-thousand-unit-performance.md`,
carries the ordered task list, and neither document is authorization to build
until the user gives one.

---

## 1. The question this document answers

`ArmyCompositionStepper.MaximumUnitsPerTeam` is 500, so a spectator can already
ask for a 1,000-unit battle today. The two render smoke
rows that exist to check exactly that are `GR-3` (set both teams to 500, watch one
full engagement at all three camera stations) and `GR-5` (watch hit pulses in a
dense 1,000-unit melee). `GR-3` closed `PASS` on 2026-08-15, run by a person at
the desktop, so a 1,000-unit battle has now been watched; `GR-5` remains
`PENDING`, and the preamble in
`docs/development/smoke-checklist.md` records that the reason given for not
running them does not hold: the ceiling is per team, so 500 on each side is the
battle those rows ask for.

So the question is not "can the game field 1,000 units". It can. The question is
whether the frame holds while it does, and where the cost sits if it does not.

This document answers that from measurements taken on `main` at `dc8e901` on
2026-08-14, plus a reading of the code that produces them. It does not propose a
behaviour change of any kind.

---

## 2. What was measured, and what it says

### 2.1 Simulation, headless, seed 1, `main` at `dc8e901`

Run through `./scripts/benchmark.ps1` with the presets the client actually
ships — combat `PrecolonialPhilippinesV5`, movement `CohortLateralSpreadV13` —
one fresh process per point, 10,000 requested ticks:

| Agents | measuredTicks | p50 ms | p95 ms | p99 ms | max ms | outcome | stateHash | eventHash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 500 | 2 386 | 0.4536 | 1.6611 | 3.0504 | 20.2749 | `Faction1Victory` | `9486F45B5BC59B80` | `B2D66B025BD1BBD3` |
| 1 000 | 3 091 | 1.2387 | 3.7039 | 5.5596 | 23.4276 | `Faction1Victory` | `01F9FD533AE0F018` | `9B25A4FA432E4CE8` |
| 2 000 | 10 000 | 5.4250 | 11.1420 | 13.8169 | 46.8327 | `Draw` | not recorded | not recorded |

Every point reported `deterministic: true` and `firstMismatchTick: null`. The
2,000-agent point reaches the tick cap as a draw, so it holds maximum density
for all 10,000 ticks and is not comparable like for like with the two points
above it; it is recorded because it is the shape of the curve past the shipped
ceiling, not because it is a target.

The same 1,000-agent point under the benchmark's own default presets (combat 6,
movement 4) reports p50 1.2944 ms, p95 3.3293 ms, max 21.7403 ms,
`stateHash 745ACF59A8ABF963`, `eventHash 0EC554A3891BCFC4`. The two preset
families cost the same to within noise, which matters: the work below is not
specific to the shipped preset and will not be undone by the next one.

### 2.2 The scaling exponent

Fitting `p50 ∝ agents^k` across the two adjacent points:

| Comparison | p50 ratio | exponent `k` |
| --- | --- | --- |
| 500 to 1 000 | 2.73 | 1.45 |
| 1 000 to 2 000 | 4.38 | 2.13 |

For context, the last recorded figure for this curve — taken on 2026-07-28,
after the collision resolver moved onto its uniform grid — was `k = 3.26`
between 1,000 and 2,000 agents, and the note recorded against it named
`SelectTargetsAndIntents` as the remaining quadratic term and said `k = 3.26` was
"the number for any future target-selection work to beat". It has since fallen
to 2.13 without that work being done, because the presets that shipped in the
interim end the battle sooner and change the density profile. The curve is still
strongly super-linear and the named cause is still there.

### 2.3 Rendering

The last recorded 1,000-unit render measurement is the Phase 2 matrix taken on
2026-07-29, `docs/development/render-baselines/render-matrix-phase2-2026-07-29.json`:

| Station | frame p50 ms | frame p95 ms | quads | submit p50 µs |
| --- | --- | --- | --- | --- |
| minimum-zoom | 4.119 | 4.511 | 18 076 | 1 024.8 |
| default-fit | 2.836 | 3.277 | 18 076 | 635.8 |
| maximum-zoom | 1.519 | 1.748 | 1 028 | 29.6 |

**That measurement is sixteen days stale and every one of those days added
pawn geometry.** Since it was taken the client gained the corpse layer, gait legs
and feet, projectile props, embedded projectiles, armor accents, blood, clash
effects, lethal-blow legibility, and leader marks. The quad count per pawn has
certainly risen and the corpse layer in particular changed the shape of the
curve: before it, a dead agent was skipped outright and a battle got cheaper to
draw as it progressed; now every one of the 1,000 pawns is drawn for the whole
battle, so the late-battle frame costs what the opening frame costs.

**No task in the accompanying plan may quote the table above as current.** Its
only role here is to establish that a 1,000-unit frame was inside budget once,
and to give the re-measurement something to be compared against.

### 2.4 The prior verdict this workstream must not re-litigate

The GPU render workstream, closed on 2026-07-29 and since archived, put a
two-clause go/no-go trigger in front of an instanced rendering backend: build it
only if the 1,000-unit default-fit `Draw` p95 exceeds 8.0 ms **and** submission
is at least 50 percent of that frame. It measured 3.28 ms against the 8.0 ms
threshold and returned **NO-GO**. It also wrote down, in advance, what happens
if the budget is later missed somewhere other than submission: the overrun is
characterised and routed to the span that owns it, and if that span is the
simulation's influence on the frame rather than the renderer's, it belongs to
the simulation.

That is exactly the situation this design expects to find. The plan therefore
proposes no rendering-backend change, and a re-measurement that misses budget
does not by itself reopen instancing — it re-runs the same two-clause trigger.

---

## 3. Where the frame budget actually goes at 1,000 units

A 60 Hz frame is 16.67 ms and must contain both `Update` and `Draw`.

`ArenaGame` advances the simulation on an accumulator at the scenario tick rate,
scaled by the playback speed, and the speed control offers 1x, 2x, and 4x. At
20 Hz and 4x that is 80 ticks per wall-clock second, or 1.33 ticks per frame at
60 fps. Using the measured 1,000-agent figures:

| Component | per frame at 1x | per frame at 4x |
| --- | --- | --- |
| Simulation, p50 | 0.41 ms | 1.65 ms |
| Simulation, p95 | 1.23 ms | 4.94 ms |
| Simulation, worst tick | 7.81 ms | 31.24 ms |
| Draw, 2026-07-29 default-fit p95 | 3.28 ms | 3.28 ms |

Two things follow. First, the median frame is comfortable even at 4x: about
5 ms against 16.67. Second, the tail is not, and the worst tick alone exceeds a
whole frame at 4x. `ArenaGame` clamps its accumulator at
`MaximumAccumulatedSeconds` and drops whole ticks rather than running them late,
and it sets `_frameSimulationStarved` when it does — the log can tell a dropped
tick from a slow frame, and from the spectator's chair they look identical.

**So the target for this workstream is the tail, not the median.** A plan that
optimises p50 and leaves max at 23 ms will produce a better number and the same
visible stutter.

---

## 4. Where the cost is, read from the source

Everything in this section is a reading of `main` at `dc8e901`, not a
measurement. Attributing these costs to the numbers in section 2 is Phase 0's
job in the plan, and no task may assume the attribution before it is taken.

### 4.1 `SelectTargetsAndIntents` is genuinely all-pairs, and spatial indexing cannot help it

`BattleSimulation.SelectTargetsAndIntents` walks every agent (line 1277) and, for
each living one, walks the entire agent array again (line 1329). At 1,000 agents
that is 1,000,000 candidate visits per tick. The inner loop opens with an
axis-aligned rejection on `|dx|` and `|dy|` against `PerceptionRangeRaw`, with a
comment explaining that it prunes candidates the squared-distance test would
reject anyway.

**That rejection never fires.** `Scenario.PerceptionRangeRaw` defaults to
2,048 world units and the default map is 1,280 by 720, whose diagonal is about
1,469 world units. Every agent on the field is inside every other agent's
perception range at all times, on every tick, in every battle the client can
start. The pruning branch is two comparisons of pure overhead per pair.

The consequence is the important one: **a uniform grid, a quadtree, or any other
spatial index buys nothing here.** The query is honestly "the nearest living
enemy anywhere on the map", and a spatial index accelerates a bounded-radius
query by not visiting far cells. There are no far cells. Any proposal to index
target selection is a proposal to change what the query means, which changes
targeting, which changes both hashes, which requires a new preset version. That
is a behaviour change and it is out of scope.

What is in scope is the cost of each of the million visits.

### 4.2 The agent array is an array of references

`AgentState` is `internal sealed class` and `_agentStates` is `AgentState[]`. The
inner scan reads `candidate.IsAlive`, `.FactionId`, `.XRaw`, `.YRaw`,
`.EntityId`, and `.Loadout` — six fields on a heap object reached through a
pointer, once per pair, a million times per tick, in an order that touches every
object in the array for every object in the array.

`AgentState` is not going to stop being a class; too much of the tick pipeline
mutates it through references and the rewrite would be enormous and risky. But
the *scan* does not need the object. A parallel struct-of-arrays hot slice —
alive, faction, x, y, entity id, packed in flat arrays and refreshed once per
tick before the scan — lets the inner loop read contiguous memory and touch the
heap object only for the small number of candidates that survive the tests. It
reads the same values in the same order, so it is hash-neutral by construction.

### 4.3 `IsLaneClearOfAllies` is the expensive one, and it *is* spatially bounded

`IsLaneClearOfAllies` (line 3145) scans the whole agent array for every route
candidate, and `TryProposeEquipmentRoute` calls it inside its candidate loop
(line 2710), so one agent can pay several full scans in one tick. Per ally
visited, the body does:

- `_movementRules.ResolveLoadoutProfile(ally.Loadout)` — a canonical-index
  computation plus an `ImmutableArray` read returning a struct by value;
- `SquaredClearanceRadius(profile)` — a widening multiply, a division by the
  10,000 basis-point denominator, and an `Int128` square;
- `Int128.Max` against the actor's own clearance;
- an `Int128` squared distance;
- on the monotone path, a second `Int128` squared distance.

`Int128` arithmetic and an integer division, per ally, per candidate, per agent,
per tick.

Three separate observations make this cheap to fix without moving a hash:

1. **The per-ally clearance radius is a battle constant.** `AgentState.Loadout`
   is `{ get; }` with no setter and is assigned once in the constructor, and
   `Scenario.BodyRadiusRaw` does not change during a battle. So
   `SquaredClearanceRadius(ResolveLoadoutProfile(ally.Loadout))` returns the same
   value on tick 1 and tick 9,999. It can be computed once per agent at
   simulation construction and read from a flat array.
2. **The scan is radius-bounded, unlike target selection.** Clearance radii are a
   small multiple of the body diameter, nothing like the map diagonal, so an
   axis-aligned rejection against the maximum clearance radius in the battle
   *does* fire here, and fires for the overwhelming majority of allies. Unlike
   the one in section 4.1, this one earns its two comparisons.
3. **Most of the `Int128` work is provably in `long` range.** Positions are
   `int`, and the map bounds are validated, so a squared separation on the
   default map fits in `long` with room. Any narrowing must be argued from the
   validated bound rather than from the observed values, and the `Int128` form
   must stay wherever the bound is not provable.

Because a full spatial query is available here in a way it is not in section 4.1,
this is also the one place where reusing `CollisionUniformGrid` is a real option
rather than a category error. It should be treated as a later, larger step than
the three above, and only if the three do not clear the tail.

### 4.4 Two more nested scans on the same pattern

`HasAllyWithinPursuitSupport` (line 3207) is a full scan per pursuing agent, and
its `supportSquared` is derived from the actor's own profile and the body
radius — another battle constant recomputed per call.
`FindNearestMeleeThreatPosition` (line 5153) is a full scan per agent that
observes melee threats. Both are radius-bounded or early-exiting and both take
the same treatment as section 4.3.

### 4.5 The client walks the roster twice per frame, and the corpse layer keeps it full

`DrawPawns` calls `DrawPawnPass` twice — once for the dead pass, once for the
living — and each call walks all 1,000 agents, resolving `PawnVisualState` and
skipping the agents belonging to the other pass. That is 2,000 iterations and
2,000 state resolutions to draw 1,000 pawns. Partitioning the roster once per
frame into two index lists and walking each list once draws exactly the same
pawns in exactly the same order.

Separately, `ConservativePawnCull` exists, is proven correct by a brute-force
containment test over the full catalog cross-product, and **is not called by
anything**. It was written to let the pawn loop reject a pawn before resolving
its appearance, and the task that would have adopted it was dropped on
2026-08-07 on the grounds that the saving is zero at minimum zoom and at default
fit. That reasoning was taken at 500 units and should be re-checked at 1,000,
where the appearance resolution it would skip is paid twice per frame rather
than once.

### 4.6 Presentation ingest runs per tick, not per frame

`PresentationCoordinator.IngestTick` is called from inside the tick loop, so at
4x it runs up to four times per frame. `Gait.Ingest(agents)` walks all 1,000
agents and maintains a `Dictionary<ulong, AgentView>`; `Trample.Ingest` and
`Dust.Ingest` take the agent list as well. This is O(n) rather than O(n²) and is
almost certainly small next to sections 4.1 and 4.3, but it is on the per-tick
path and Phase 0 should size it rather than assume it.

---

## 5. What this workstream deliberately does not do

- **It does not change behaviour.** Not targeting, not routing, not clearance,
  not the outcome of any battle. Every change is hash-neutral by construction and
  the acceptance criterion is stated that way in section 6.
- **It does not add a spatial index to target selection.** Section 4.1 shows the
  query has no locality to exploit under the shipped scenario.
- **It does not build an instanced rendering backend.** Section 2.4 records why,
  and re-running the two-clause trigger is a measurement, not an authorization.
- **It does not introduce a new preset version.** A new preset would mean the
  change was not hash-neutral, which would mean it changed behaviour, which is
  the first bullet.
- **It does not parallelise the tick.** Determinism forbids anything whose result
  depends on scheduling, and `SIMULATION-GAME-STANDARDS.md` section 4 is the
  contract.
- **It does not raise `MaximumUnitsPerTeam` above 500.** Whether the ceiling
  should move is section 7's open question, and the answer needs the
  measurement this workstream produces.
- **It does not scale the map with the unit count.** Section 7 again: at 1,000
  units the default 1,280 by 720 map is dense, and whether that is the intended
  look is a design question, not a performance one.

---

## 6. The determinism contract for this workstream

Every change proposed here reads the same values in the same order and writes the
same results. That makes the determinism evidence unusually strong: **if a hash
moves, the change is wrong**, with no judgement call about whether the movement
was legitimate. There is no case in this workstream where recapturing a golden
expectation is the right response to a red test.

The acceptance evidence is therefore:

1. **The five canonical gate workloads, byte-identical.** `./scripts/verify.ps1`
   runs 200 agents / 10,000 ticks / seed 1 five times, and the digests recorded
   for `main` are:

   | Workload | Combat / movement preset | Outcome | State hash | Event hash |
   | --- | --- | --- | --- | --- |
   | Canonical | 6 / 4 | `Faction0Victory` | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` |
   | Ranged | 5 / 8 | `Faction1Victory` | `C8023D3B5BEB005E` | `F709A345E2F7370E` |
   | Battlefield realism | 5 / 10 | `Faction0Victory` | `7C145A9E05916E4C` | `77626E104234206C` |
   | Last stand | 5 / 11 | `Faction0Victory` | `6225182B4A470F91` | `C4DABE6AF98B6BEC` |
   | Cohort lateral spread | 5 / 13 | `Faction1Victory` | `4A0723BC9A1B924B` | `E0CE32CF8830A864` |

2. **The two large-agent digests recorded in section 2.1, byte-identical.** The
   gate never runs above 200 agents, and several of the code paths this
   workstream touches only get interesting at high density. The 500- and
   1,000-agent seed-1 digests under the shipped presets are the regression net
   for that, and they are recorded in section 2.1 for exactly this purpose.

3. **The allocation windows unchanged.** `BattleSimulationTests` holds two
   warm-tick ceilings, 8,192 bytes and 16,384 bytes. A hot-slice array allocated
   once at construction does not touch them; one allocated per tick does, and
   would fail.

4. **`maximumPenetrationRaw 0` on every measured point.** The collision invariant
   is the cheapest available proof that a movement-path change did not quietly
   let bodies overlap.

---

## 7. Open questions, none of which this design settles

- **Should `MaximumUnitsPerTeam` move above 500?** The 2,000-agent point in
  section 2.1 costs 5.43 ms at p50, which is a whole frame at 4x on its own. If
  the ceiling is ever to move, the number to beat is that one.
- **Do the presentation effect caps still read correctly at 1,000 units?**
  `EmbeddedProjectileSystem.Capacity` is 256 and `TrampleMarkSystem.Capacity` is
  128. Against 1,000 pawns those are small, and a spectator may see effects
  starve rather than saturate. This is a legibility question, not a performance
  one, and `GR-3` closed `PASS` on 2026-08-15 with a person watching to answer it.
- **Should the map scale with the unit count?** It does not today, so 1,000 units
  fight on the same field 200 units do. That is a look-and-feel decision with a
  large performance consequence in both directions.
- **Is `ConservativePawnCull` worth adopting at 1,000 units?** Its rejection at
  the time was measured at 500. Section 4.5.

---

## 8. The nine questions

`SIMULATION-GAME-STANDARDS.md` section 10 requires every feature proposal to
answer them. The one that matters most here is the discoverability question, and
this workstream's answer is unusual, so it is stated plainly.

**Can a spectator discover this effect without reading source code?** Yes, and
only in one form: a 1,000-unit battle that does not stutter, where today's does
or does not. There is no new visual, no new event, no new
inspector row, and no new sound. The spectator-visible deliverable is `GR-3` and
`GR-5` being runnable and passing; `GR-3` closed `PASS` on 2026-08-15 and
`GR-5` remains open, and the honest statement of that is that if
the re-measurement in Phase 0 shows the frame already holds, **the correct
outcome of this workstream is to run those two rows and build nothing**. That
outcome is a success, not a wasted plan, and the plan document is ordered so
that it is reachable at the end of Phase 0.
