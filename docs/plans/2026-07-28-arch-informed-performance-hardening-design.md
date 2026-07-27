# Arch-Informed Performance Hardening — Design

Date: 2026-07-28

Status: design only. This document does not authorize implementation. The ordered task list lives in the companion plan document, [docs/plans/2026-07-28-arch-informed-performance-hardening.md](2026-07-28-arch-informed-performance-hardening.md).

## 1. What this workstream is, and the one thing it is not

An external research pass read the Arch entity-component-system library end to
end — its chunk layout, its entity-location storage, its query enumerators, its
command buffer, its build configuration, its benchmark harness, and its
documentation practices — and asked one question of every technique it found:
*can this be used inside a deterministic, single-threaded, fixed-schema battle
simulation, and can it be used without adopting an ECS?* This document is the
plan that falls out of the answers.

**This workstream does not adopt Arch, does not add an archetype or chunk
system, and takes no package dependency on Arch or on any part of it.**
`CLAUDE.md` section 9 lists "Add a general-purpose ECS framework before a
profiler demands it" among the things this repository does not do, and nothing
below is an argument for relaxing that. The rule is respected here in both its
letter and its spirit: no framework is introduced, and no structural change is
authorized by this design at all — every structural item is gated behind a
profile that does not exist yet.

What is being taken is a set of *techniques*, each of which is a few dozen lines
of ordinary C# that happens to be well demonstrated in Arch's source. A dense
integer array in place of a dictionary is not an ECS. A `ref` return in place of
an indexer is not an ECS. Splitting a record by access pattern is not an ECS. The
research's own headline conclusion about the archetype machinery is that it "pays
off when component composition is dynamic and diverse; a fixed-schema two-faction
battle sim gets the cache win from plain parallel arrays and none of the
archetype-transition cost," and this design agrees with that assessment and acts
on it.

There is a second, quieter reason to write this down. Arch is a mature, popular,
performance-focused library with **no determinism tests of any kind**, no
allocation-budget assertions, and no version-tolerance tests. Several of its
fastest paths — runtime component-id assignment, hash-only equality on query
descriptions, bounds-check-free native collections, machine-partitioned parallel
queries — are outright unusable here, and are unusable for reasons that are not
obvious from reading the code. Recording *why* is worth as much as recording what
to copy, because the next person who reads a fast ECS will meet the same
temptations.

## 2. The governing constraint: two hashes that must not move

`SIMULATION-GAME-STANDARDS.md` section 8 states that optimizations need
same-workload before-and-after data and identical hashes, and that a regression
above ten percent in p95 tick time or working set requires review.

That sentence is the whole shape of this workstream. The recorded seed-1
baseline, from `docs/development/testing.md`, is:

| Field | Value |
| --- | --- |
| `stateHash` | `71211929A44A16CA` |
| `eventHash` | `A2DC3ECA3F7345ED` |
| `measuredTicks` | 1,710 |
| `outcome` | `Faction1Victory` |
| `allocatedBytes` | 93,905,304 |
| Tick p50 / p95 / p99 / max | 0.0812 / 1.5217 / 2.8857 / 9.4846 ms |

**Every task in this workstream is hash-neutral, and that pair of hashes must be
unchanged at every task boundary, not merely at the end.** This is stronger than
the usual rule, and it is stated this way on purpose. A workstream whose only
justification is speed has no business changing what the simulation computes; if
a change moves a hash, the change is either wrong or it is a different feature
wearing a performance costume. Either way it stops.

The corollary matters more than the rule. **A task that would move a hash is out
of scope by definition, not a task with extra steps.** There is no version bump
here, no new golden expectation, no `CombatPresetId` entry, no re-baselining. If
an implementer finds themselves reaching for the `hukbo-determinism-change`
skill's re-baseline procedure, they have left this workstream and need a new
design document.

The two hashes are independent and both must be checked. `StateHasher.Compute`
(`src/Hukbo.Core/Determinism/StateHasher.cs:22`) folds scenario fields, the
ruleset content hash, tick, outcome, event sequence, agent count, and then
eighteen fields per agent **in storage order**. The event hash is computed
separately in `HeadlessRunner.AddEventToHash`
(`src/Hukbo.Headless/HeadlessRunner.cs:503`). The storage-order dependency in
`StateHasher` is the single most layout-sensitive line in the repository and it
is why the agent-layout candidate in section 6 is the most dangerous item here.

## 3. Phase order, and the gates between phases

The work runs in three phases, and the boundaries between them are real gates
rather than a narrative device.

```text
Phase 1 — Measurement           (Gate A: a profile exists)
        |
        v
Phase 2 — Zero-risk hygiene     (no gate; may run alongside Phase 1)
        |
        v
Phase 3 — Structural work       (each item gated individually on the Phase 1 profile)
```

`docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md` already defines Gate A as
"Measurement — Required before structural optimization" and lists what it must
report: stage p50/p95/p99/max, alive agents per tick, spatial candidates and
accepted neighbours, events emitted and bytes allocated, state-hash cadence cost,
render figures, and workloads that preserve worst-case population and perception.
That document also records, at line 432, that "there is no per-stage timing," and
lists "no stage-level benchmark report" among the gaps at line 452. Gate A is
therefore not satisfied today, and nothing structural is authorized today.

Phase 2 sits outside that gate because its items cannot move a hash and do not
need a profile to justify them: they remove allocations that are visible in the
source, or they write down invariants that are currently undocumented. They are
placed second in the narrative but they may be implemented in parallel with
Phase 1, subject only to the file-ownership rules in the plan document.

Phase 3 is gated **per item**, not as a block. Each candidate carries an explicit
precondition naming what the profile must show, and an explicit abandonment
condition naming what result kills it. An item whose precondition is not met is
not deferred to a later sprint — it is closed, with the profile pasted as the
reason.

## 4. Phase 1 — measurement, and how per-stage attribution will be obtained

Three things are missing before any structural claim can be made honestly.

### 4.1 There is no Core-only allocation figure

`RunReport.AllocatedBytes` (`src/Hukbo.Headless/RunReport.cs:25`) is populated
from a `GC.GetAllocatedBytesForCurrentThread()` delta taken at
`HeadlessRunner.cs:290` and closed after the whole measurement loop. The loop
body advances **two** simulations — the run under test and its determinism twin —
computes both hashes, compares the event streams, and accumulates the collision
and combat metrics. The timing figure is narrower: `Stopwatch.GetTimestamp()`
brackets only `left.AdvanceOneTick()` at `HeadlessRunner.cs:297-300`.

So the report's timing and its allocation figure describe different things, and
the allocation figure is not a `Hukbo.Core` number at all. The recorded
93,905,304 bytes across 1,710 ticks — about 54.9 KB per tick — is a harness
total, and the per-simulation share of it is unknown.

This matters immediately, because it is the number that decides whether the
largest Phase 2 item is worth doing. `BattleSimulation.AddEvent`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1463`) and `AddAttackEvent`
(`:1499`) both allocate the tick's event list as
`new List<BattleEvent>(_agentStates.Length * 2)`. At 200 agents that is a
400-slot backing array of 72-byte events, roughly 28.8 KB plus header, allocated
on every event-bearing tick regardless of how many events fire. In a 200-agent
battle nearly every tick emits movement events, so "every event-bearing tick" is
in practice every tick.

Two of those, one per simulation, is on the order of the entire recorded
per-tick figure. **That is an inference, not a measurement**, and it is exactly
the kind of inference this phase exists to replace. Splitting the counter is a
few lines in the harness, it cannot touch `Hukbo.Core`, and it converts a guess
into evidence.

The archived note at
`docs/archives/2026-07-27/2026-07-27-battle-event-allocation-packing.md` measured
a related but different thing — the fixed sixteen-byte-per-event cost of adding
two nullable enum fields — and its numbers predate several changes to how long a
seed-1 battle runs. It is archived, it is reference only, and it is cited here as
context rather than as authorization or as a live budget.

### 4.2 There is no scaling curve

The recorded oracle gives one 200-agent figure and one report-only 500-agent
figure. A single number cannot distinguish a linear cost from a quadratic one,
and the one uncontrolled loop in the tick pipeline is quadratic.

Arch's benchmark harness sweeps entity count through a `[Params]` attribute so
that scaling is visible rather than asserted, and adds a fragmentation-fairness
parameter so the awkward case is measured alongside the friendly one. The first
half of that practice transfers directly and costs nothing: run the existing
headless workload at several agent counts and write the curve down. The existing
runner already supports `--output <json-path>`
(`src/Hukbo.Headless/HeadlessRunner.cs:185`), so the sweep needs no code at all.

### 4.3 There is no per-stage attribution, and Core may not read the clock

`BattleSimulation.AdvanceOneTick` (`BattleSimulation.cs:277-301`) executes eight
sequential passes over the agent array plus one quadratic pass, and every one of
them is already a separately named private method:

```text
DecrementCooldowns
SelectTargetsAndIntents      // contains the O(n^2) target scan
GatherMovementProposals
ResolveCollisions
CommitMovement
MeasureCollision
GatherAndCommitAttacks
ResolveOutcome
```

Attributing tick time to those stages is the single most valuable thing Phase 1
can produce, and it is constrained by `CLAUDE.md` section 3: `Hukbo.Core` must not
reference the wall clock. Instrumentation cannot live inside Core. Two approaches
were evaluated.

#### Option (a) — sample the existing run with `dotnet-trace`

Attach a sampling profiler to the unmodified Release headless workload through
the `dotnet-diag` plugin and read the resulting stage attribution off the call
tree.

*Cost:* no source change of any kind. No new project, no new API surface, no
widened `internal` visibility, no equivalence test to write, and — decisively —
no possibility of moving a hash, because the code under measurement is the code
that ships. It is available today.

*Limitation, stated before the recommendation:* a sampling profiler reports the
distribution of samples across methods. It does **not** produce per-tick
percentiles, so it cannot literally satisfy Gate A's "stage p50/p95/p99/max". It
is also vulnerable to inlining, which can attribute a small stage's samples to
`AdvanceOneTick` itself, and its resolution degrades for stages that are a small
fraction of a short tick. The recorded p50 tick time is 0.0812 ms, which is
coarse relative to a default sampling interval.

#### Option (b) — expose the stages and drive them from outside

Raise the eight private stage methods to `internal`, add an ordered stage table,
and have a harness outside Core call them one at a time with a `Stopwatch` around
each.

*Cost:* higher than it first appears, and the cost is structural rather than
incidental. `AdvanceOneTick` threads a `List<BattleEvent>? events` local through
four of the eight stages by `ref` (`BattleSimulation.cs:286-295`). An external
driver cannot call those stages without Core also exposing that accumulator and
its lifetime, which widens the internal surface of the simulation considerably
and does so specifically around the event stream — the thing the event hash is
computed from. The seam itself is cheap, because `Hukbo.Core` already grants
`InternalsVisibleTo("Hukbo.Headless")`
(`src/Hukbo.Core/Determinism/Fnv1a.cs:3`); the API design is not.

It also introduces a second execution path through the tick, which must then be
proven equivalent to the first. That proof already has a template — the
`FullTraceLoggingDoesNotChangeTheSimulationResult` test in
`tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs` runs the seed-1
workload twice under different logging settings and requires identical state
hash, event hash, outcome, and event stream — but a template is not free.

*Benefit:* real per-tick, per-stage percentiles, which is what Gate A actually
asks for, and which no sampler can give.

#### Recommendation

**Start with option (a). Build option (b) only if option (a) is ambiguous, and
define "ambiguous" before running it.**

The question Phase 3 needs answered is not "what is the p95 of
`GatherMovementProposals`". It is "which stage dominates, and by enough of a
margin to justify restructuring it". A sampling profile answers that whenever one
stage is clearly on top, and it answers it at zero risk to the thing being
measured. Building a parallel execution path through the tick, and widening
Core's internal surface around the event accumulator, in order to learn something
a free tool would also have told us, is the more expensive mistake.

The ambiguity threshold is therefore stated in advance rather than argued about
afterwards: option (b) is authorized if and only if the option (a) profile leaves
no single stage above **thirty percent** of inclusive samples, **or** leaves the
top two stages within **five percentage points** of each other. Either result
means the profile cannot rank the Phase 3 candidates, and the more expensive
instrument is warranted.

When option (b) is built, it goes in `tools/`, which is the established home for
hand-run measurement harnesses: four already live there
(`Hukbo.Tools.CueDemand`, `VoiceStress`, `MixAnalysis`, `WeaponBalance`), none is
listed in `Hukbo.slnx`, and none runs in the canonical gate. That placement keeps
a second tick-execution path out of the shipped assemblies' test surface and out
of the gate's critical path, while still inheriting `Directory.Build.props` and
central package management.

### 4.4 One research figure that did not survive verification

The external research reported "40,000 squared-distance evaluations per tick,
about 26.3 million over the recorded 657-tick seed-1 run" for target selection.
Two parts of that do not hold against the source and the recorded oracle, and the
plan must not carry them forward as facts.

First, the same-faction rejection at `BattleSimulation.cs:546` runs **before** the
squared-distance computation at `:551`. With two equal factions, roughly half of
the 40,000 ordered pairs are rejected without any arithmetic, so the per-tick
count of actual distance computations is on the order of 20,000 at full strength,
not 40,000.

Second, the recorded seed-1 baseline in `docs/development/testing.md` is 1,710
measured ticks, not 657. A total computed against 657 ticks describes some other
build.

Third, and most important, both figures assume full strength for the whole
battle. Agents die, dead agents are skipped by the `continue` at `:532` before the
inner loop is entered at all, and the cost therefore falls monotonically as the
battle progresses.

The honest statement is that target selection is O(n²) in living agents per tick,
that its total cost over a seed-1 run is **unmeasured**, and that measuring it is
Phase 1's job. The observation that survives intact — and it is the important one
— is that this is the **only** loop in `Hukbo.Core` with no spatial acceleration,
while the collision stage has had a uniform grid since
`CollisionUniformGrid.cs:45`.

## 5. Phase 2 — hygiene that needs no profile

Four items. Each is visible in the source, each is hash-neutral by construction,
and none of them depends on Phase 1 reporting anything.

### 5.1 The boxed enumerator in `MeasureCollision`

`CollisionUniformGrid.Pairs` is typed `IReadOnlyList<CollisionPair>`
(`CollisionUniformGrid.cs:126`) over a backing `List<CollisionPair>`. The
`foreach` at `BattleSimulation.cs:1045` therefore binds to
`IEnumerator<CollisionPair>` and boxes `List<T>`'s struct enumerator once per
tick.

This is the cheapest possible instance of a general principle the research draws
out of Arch, which uses `ref struct` enumerators throughout
(`Enumerators.cs`) precisely so a `foreach` allocates nothing and the enumerator
can be neither boxed nor captured. Hukbo cannot make `List<T>` do that, but it can
stop asking for the interface: iterating the concrete type binds to the struct
enumerator and the box disappears.

One boxed enumerator per tick is roughly 68 KB across a 1,710-tick run, which is
under a tenth of a percent of the recorded total. This item is not worth doing for
the bytes. It is worth doing because it is a one-line change with zero behavioural
surface, and because the interface-typed property is a latent trap: any future
per-tick `foreach` over `Pairs` inherits the same box for free.

### 5.2 The per-tick event list and its read-only wrapper

Two allocations per event-bearing tick, both in the same place. The list itself,
pre-sized to `_agentStates.Length * 2` at `BattleSimulation.cs:1463`, and the
`ReadOnlyCollection<BattleEvent>` produced by `events.AsReadOnly()` at `:300`.

The fix is a simulation-owned `List<BattleEvent>` cleared at the top of each tick
and a single `ReadOnlyCollection<BattleEvent>` wrapper created once over it. The
events themselves are unchanged — same values, same order, same sequence numbers
— so neither hash can move.

**The risk is lifetime, not correctness of the values.** `LastEvents` currently
hands a caller a wrapper over a list that will never be written again, so a
caller may retain it indefinitely. Reusing the buffer changes that contract:
the returned collection becomes valid only until the next
`AdvanceOneTick`. `docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md` names
this exact hazard in its Gate C criteria — "no pooled or reused data escapes its
declared lifetime" — and the repository already has one precedent for handling it
well: `CollisionUniformGrid.Pairs` documents at `:121-125` that "the list is owned
by the grid and is overwritten by the next `Rebuild` or `Clear`. Callers read it
within the tick that produced it and never retain it." The event accumulator needs
the same sentence, written at the symbol, and every existing caller of
`LastEvents` audited against it.

This is also, on the inference in section 4.1, very likely the largest single
allocation item in the tick. It is the item that most justifies fixing the
allocation counter first, because a claimed win here is unfalsifiable while the
only available number also counts the determinism twin and the hash computation.

### 5.3 The undocumented `CollisionResolver.Grow` invariant

`CollisionResolver.Grow<T>` (`CollisionResolver.cs:327-335`) replaces a buffer
with a fresh array and **does not copy the old contents**:

```csharp
buffer = new T[Math.Max(requiredLength, buffer.Length * 2)];
```

That is safe today because `Reset` refills every slot before anything reads one.
It is safe by an invariant that is written down nowhere and asserted by no test.

The research flags the general form of this hazard under "safe-if-ordered": Arch
returns pooled arrays without `clearArray: true` and allocates native memory
without zeroing, both of which are deterministic only when every slot is written
before it is read. Hukbo's version is the same bargain. The fix is not to start
copying — copying would be a real cost for no benefit — but to state the
invariant in an XML doc comment at the symbol and add a test that fails if a
grown buffer is ever read before `Reset` refills it.

### 5.4 The standards record

The technique inventory in sections 7 and 8 of this document is reasoning, and
reasoning belongs in a design document. What belongs in
`SIMULATION-GAME-STANDARDS.md` is the *consequence*: a short, durable section
saying which techniques this repository has decided are usable, which are usable
only with a named discipline, and which are forbidden and why. Without that, the
next person to read a fast ECS re-derives the whole argument, and possibly
re-derives it wrong.

The same section carries the explicit non-adoption statement from section 1, so
that "we looked at Arch" can never be misread later as "we are moving toward an
ECS".

## 6. Phase 3 — structural candidates, each gated

Nothing in this section is authorized. Each candidate states what the Phase 1
profile must show before an implementer may begin it, and what result closes it.

### 6.1 Axis-delta rejection in the target scan (cheapest, do first)

Before computing `SquaredDistance` at `BattleSimulation.cs:551`, reject a
candidate whose absolute delta on either axis already exceeds the perception
range. If `|dx| > R` then `dx² + dy² > R²` necessarily, so the rejected set is
exactly a subset of the set the current test rejects, and the surviving
comparison order is untouched.

This preserves the tie-break at `:557-561` — `(eligible desc, distance_squared
asc, target_entity_id asc)` — because it removes only candidates that would have
failed the perception test anyway, in the same order.

*Precondition:* `SelectTargetsAndIntents` appears in the profile at all.
*Abandonment:* the profile shows target selection below five percent of tick
time, in which case even this is not worth the diff.

The research's other suggested precursor, "skip dead agents in the outer loop",
is already done: the `continue` at `BattleSimulation.cs:532` fires before the
inner loop is entered, so a dead agent costs one branch, not 200. There is no
work here.

### 6.2 Spatial acceleration for target selection (Gate B)

Replace the O(n²) scan with a broad phase built on `CollisionUniformGrid`, whose
cell size becomes a construction parameter so that a second instance can be sized
for the perception radius. That radius is larger than the contact radius and
therefore wants larger cells; the alternative of writing a purpose-built second
grid was considered and rejected in section 12, because one implementation and
one naive oracle serving both callers is worth the cost of two live grid
instances per tick.

The acceptance bar is already written. `CollisionUniformGrid`'s documented
contract (`CollisionUniformGrid.cs:12-15`) is that it "produces exactly what an
O(n²) scan over the same bodies would produce, in exactly one order," and the
repository's house pattern for proving that is a hand-written naive oracle marked
"Do not optimise this class" — `tests/Hukbo.Core.Tests/NaiveCollisionPairs.cs`
and `NaiveClashResolution.cs`. Any target-selection index must ship with the same
kind of oracle, and both hashes must be byte-identical afterwards.

The grid is a derived accelerator: rebuilt each tick from authoritative
positions, never hashed, never snapshotted, never persisted. That classification
is already established by section 6 of the standards and by the collision grid's
own treatment, and it is what keeps this change out of the hash.

*Precondition:* the Phase 1 profile attributes at least twenty percent of tick
time to `SelectTargetsAndIntents`, **and** the section 4.2 scaling sweep shows
tick time growing faster than linearly in agent count.
*Abandonment:* either condition fails, or a prototype fails to reproduce the
naive oracle's output exactly. A spatial index that is merely fast is worthless
here.

### 6.3 A dense identifier-to-index map

`BattleSimulation._agentIndexes` is a `Dictionary<ulong,int>`
(`BattleSimulation.cs:19`). It is lookup-only and never enumerated, which is why
it is legal today — the standards forbid hash iteration order deciding gameplay,
not hash lookup.

Arch solves the same problem with `EntityInfoStorage`'s `JaggedArray<EntityData>`:
bucket index by `id >> _bucketSizeShift`, item index by `id & _bucketSizeMinusOne`,
bucket size rounded to a power of two and chosen to fit L1. No hashing, no rehash
stall, and — the property that matters most here — **no iteration order that
could ever exist to leak into gameplay**. A dense array cannot be enumerated in a
nondeterministic order because there is no hashing involved.

Two porting notes. Arch computes its bucket sizes with `Math.Ceiling` on floats
and `Math.Log(x, 2)`; those are harmless in Arch because they only pick
capacities, but a float must not appear in a `Hukbo.Core` file, so the port uses
integer ceiling division and `BitOperations.Log2`. And `EntityId` is a
monotonically increasing `ulong` that is never reused, so a battle's identifiers
are dense from the start and the jagged structure may turn out to be unnecessary
— a single array indexed by `id - firstId` may suffice, and simpler is better.

The broader pattern worth carrying regardless of whether this candidate proceeds
is Arch's separation of a hash container used **for lookup only** from a separate
ordered collection that is the thing actually iterated (`World.GroupToArchetype`
versus the ordered `World.Archetypes`). Hukbo already does this in two places and
documents it in one — `CollisionUniformGrid.cs:74-76` — and the standards section
in item 5.4 should name it as the general rule rather than leaving it as two
local accidents.

*Precondition:* the profile attributes measurable time to `MeasureCollision`'s
per-pair lookups or to any other dictionary lookup on the tick path.
*Abandonment:* dictionary lookup does not appear in the profile. This is the most
speculative of the three and the most likely to be closed unmeasured.

### 6.4 `AgentState` layout — deliberately not a task in this plan

`AgentState` is an `internal sealed class`
(`src/Hukbo.Core/Simulation/AgentState.cs:5`) held in an `AgentState[]`
(`BattleSimulation.cs:18`) allocated once at `:141` and never resized. That is
200 separately heap-allocated objects reached through an array of references —
precisely the layout Arch exists to avoid, and the repository already
demonstrates that it knows the alternative, because `AgentView`
(`AgentView.cs:11`) is a `readonly record struct` in a contiguous array.

Arch's wiki guidance is the most valuable single piece of advice the research
returned, and it applies here directly: keep entities small, apply flyweight, and
**split components by access pattern rather than by semantic relatedness**,
because iterating fields you do not read is wasted cache. Eight sequential passes
per tick, each reading a different handful of the eighteen hashed fields, is
exactly the workload that guidance describes.

**This design does not authorize that change, and the plan document does not
contain it as a task.** The reasons are specific:

- `StateHasher.Compute` folds eighteen fields per agent **in storage order**
  (`StateHasher.cs:53`). Any reordering, any split into parallel arrays, any
  change to what "storage order" means, moves the state hash — which section 2
  puts out of scope by definition.
- `CommitMovement` (`BattleSimulation.cs:972-1013`) consumes collision results
  positionally and advances its result index only for living agents (`:985-986`).
  That is the tightest layout coupling in the pipeline and it is load-bearing.
- The test surface is large and concrete: 24 `CreateForTesting` sites in
  `BattleSimulationTests.cs` with the helper at `:1198`, 15 `AgentState`
  references in `DeterminismTests.cs` including a direct `new AgentState(...)` at
  `:286`, 27 sites in `LastStandFormationTests.cs`, plus
  `CollisionRegressionTests.cs`, `CollisionResolverTests.cs`,
  `CollisionUniformGridTests.cs`, and `PhilippineCombatIntegrationTests.cs`.

A change of that size needs its own design document, its own risk analysis of the
storage-order dependency, and its own plan. What this workstream owes it is the
profile that says whether it is worth attempting at all.

*Precondition for opening that design:* the Phase 1 profile shows the tick
dominated by memory access rather than by a single algorithmic hot spot — that
is, no one stage above thirty percent, but a broad flat distribution across the
eight passes.
*Abandonment:* one stage dominates. Then fix that stage (section 6.2) and leave
the layout alone.

### 6.5 Techniques that depend on 6.4 and are therefore out of scope

`ref`-returning accessors in place of indexers, `MemoryMarshal.CreateSpan` over
the first element, `Unsafe.Add` for bounds-check elimination, and
`[SkipLocalsInit]` on hot accessors are all real techniques and all well
demonstrated in Arch's `Chunk`. Every one of them presupposes a contiguous
value-type layout. Applied to an array of class references they buy nothing,
because the pointer chase they would have to eliminate is still there.

`Unsafe.Add` and `MemoryMarshal.CreateSpan` additionally require
`AllowUnsafeBlocks`, which is absent from `Directory.Build.props` repo-wide.
Adding it is a deliberate weakening of a build guarantee and needs its own
justification in the design that authorizes it, not a line item in this one.

They belong to the 6.4 follow-on design, if that design ever exists.

### 6.6 `Handle<T>` — assessed and declined

Arch.LowLevel's `Handle<T>` / `Resources<T>` pattern — a `readonly record struct`
holding an `int` plus a side table mapping identifier to managed object — lets a
hashable, snapshot-safe component carry an integer while a client resolves it to
a texture or a sound. It is roughly forty lines and the research recommends
taking the pattern rather than the package.

It solves a problem Hukbo does not currently have. Core already carries only
enums and integers across the boundary — `WeaponId`, `ShieldId`, `HitLocation`,
`AttackResolution`, all packed into a single `int` on `BattleEvent` — and the
client resolves those to assets through its own mapping tables
(`SoundCueMapper`, `PawnAppearanceFactory`). There is no Core value today that
wants to be a handle.

Recorded here so that the next person who needs an asset reference in
authoritative state finds the pattern already assessed, rather than inventing a
worse one or reaching for a managed reference in hashed state.

## 7. Techniques judged portable

Consolidated for the standards record, with the discipline each one requires.

| Technique | Portable? | Discipline required |
| --- | --- | --- |
| Structure-of-arrays with a cache-sized block | Yes | Block size chosen from measured cache size; integer arithmetic only |
| Dense `int[]` index in place of a dictionary | Yes | None; strictly safer than a dictionary here |
| `ref` returns instead of indexers | Yes, after a value-type layout exists | Requires 6.4 |
| `MemoryMarshal.CreateSpan` / `Unsafe.Add` | Yes, after 6.4 | Requires `AllowUnsafeBlocks`, which needs its own justification |
| `[SkipLocalsInit]` on hot accessors | Yes, after 6.4 | Requires 6.4 |
| Dense identifier-to-location addressing by shift and mask | Yes | Power-of-two bucket size; `BitOperations.Log2`, never `Math.Log` |
| Hash container for lookup only, ordered collection for iteration | Yes — already the local practice | The ordered collection must be the only thing enumerated, and that must be documented at the symbol |
| Bit-set signature matching for whole-group rejection | Yes | Fixed word order |
| `ref struct` enumerators | Yes | Cannot be boxed or captured — which is the point |
| Struct callback as a generic type parameter instead of a delegate | Yes | Hand-written per stage; **not** via a source generator |
| Deferred structural change through a command buffer | Yes | Fixed playback phase order, one ordered pass |
| Sparse sets for pending-flag membership | Yes | No hashing |
| Reverse iteration | Yes, with discipline | A descending order is still a total order, but pin the direction with a test or a refactor silently moves a hash |
| Swap-remove | Yes, with discipline | Storage position must never break a tie; `EntityId` stays the sort key |
| Pooled or uninitialised buffers | Yes, with discipline | Every slot written before read, invariant documented and tested — see 5.3 |

The struct-callback item deserves its own sentence because the measured number
attached to it is attractive and the packaging around it is not. Arch's
generated struct queries measured about twenty percent better than a delegate on
a two-component system — 57.47 μs to 47.84 μs, single-threaded, on the
Doraku/Ecs.CSharp.Benchmark harness — and that win comes from the JIT
devirtualising and inlining a struct's method, not from anything the generator
does. Hukbo has eight tick stages, not an open-ended set of user systems. Writing
the shape by hand where it helps is strictly better than adding
`Arch.System.SourceGenerator`, which would bring a build dependency, a
generated-code review surface, and a new class of golden file.

## 8. Techniques deliberately not ported

| Technique | Why it is forbidden here |
| --- | --- |
| `World.ParallelQuery`, `JobScheduler`, `[Query(Parallel = true)]` | `RangePartitioner(Environment.ProcessorCount, …)` makes the work split machine-dependent and chunks complete in arbitrary order. Arch's own documentation says a parallel query must not be called from anything but the main thread. Non-negotiable against the single-threaded authoritative schedule |
| Runtime component-identifier assignment | `ComponentRegistry` hands out identifiers from an incrementing counter on first use, via a static constructor, so identifiers depend on which type the JIT touches first — and they feed the archetype signature hash. Any identifier-per-type registry adopted here must draw from an explicit, committed, ordered table, versioned exactly like enum numeric values |
| `QueryDescription.Equals` by hash code only | Hash-only equality over a 32-bit mix. A collision reproducibly returns the wrong entity set — a determinism bug that looks like a logic bug |
| `UnsafeArray`, `UnsafeList`, bounds-check-free collections | In a deterministic simulation an `IndexOutOfRangeException` is an asset: a loud, reproducible failure at an exact tick. Trading it for an out-of-bounds read converts a debuggable crash into a silent hash divergence |
| The archetype and chunk machinery | Pays off when component composition is dynamic and diverse. A fixed-schema two-faction battle gets the cache win from plain parallel arrays and pays none of the archetype-transition cost |
| `Arch.System.SourceGenerator` | The measured win is the inlined struct-query shape, which is hand-writable for eight stages. See section 7 |
| `Arch.Persistence` | No version field, no magic number, fully positional MessagePack, and its own documentation requires component registration order to match across the save boundary. It also pins `MessagePack 2.6.100-alpha` with a known advisory suppressed through `<NoWarn>NU1902</NoWarn>` — a suppression this repository promotes to an error in `Directory.Build.props` |
| Build flags that change behaviour | Arch ships six configurations whose `#if PURE_ECS` / `#if EVENTS` variants change public API and behaviour. A build flag that changes simulation behaviour means a state hash per configuration |

The `Arch.Persistence` finding has a positive form worth recording, because
Hukbo's snapshot format does not exist yet and Gate 3 will have to author it:
**a Hukbo snapshot must carry a preset version and a schema version in its
header, and a mismatch must be a hard failure.** Section 7 of the standards
already requires a versioned envelope and non-destructive errors for unsupported
versions; Arch is the worked example of what happens without one.

## 9. Build configuration: there is nothing to take from Arch

The research examined Arch's build setup specifically looking for runtime knobs
worth copying, and found none. There is no `Directory.Build.props`, no
`ServerGarbageCollection`, no `TieredPGO`, no `ReadyToRun`, no
`InvariantGlobalization`, no `runtimeconfig.template.json`, no central package
management, and no lock files anywhere in the repository. All of Arch's
performance comes from data layout and code generation, not from configuration.

Where the two repositories differ, Hukbo is stronger on every axis. Arch declares
`LangVersion` and `AllowUnsafeBlocks` twice each, and comments out
`TreatWarningsAsErrors` in its test projects. Hukbo's `Directory.Build.props`
sets `TreatWarningsAsErrors`, `Nullable`, `Deterministic`, `EnableNETAnalyzers`,
`EnforceCodeStyleInBuild`, `RestorePackagesWithLockFile`, `NuGetAudit` at
`moderate` in `all` mode, and promotes `NU1902`, `NU1903`, and `NU1904` to
errors.

**No build-configuration change is proposed by this design.** Any runtime knob —
`ServerGarbageCollection`, `TieredPGO`, `ReadyToRun` — is a hypothesis about this
workload that Phase 1 has not tested, and would need its own before-and-after
measurement with identical hashes. It is not free: server garbage collection in
particular changes allocation behaviour under the very budgets the repository
already asserts in `BattleSimulationTests.RepeatedQuietTicksHaveBoundedAllocations`
and `RepeatedCollisionTicksHaveBoundedAllocations`.

Nor is any new package proposed. A package addition edits
`Directory.Packages.props` and regenerates all ten tracked `packages.lock.json`
files, which `CLAUDE.md` section 5 classifies as a reviewed dependency change. The
measurement in Phase 1 uses tooling that is already available.

## 10. Engineering practices worth adopting

Four of Arch's practices are better than the corresponding Hukbo practice, or
fill a gap where Hukbo has none.

**Golden files for mechanically derived output.**
`Arch.System.SourceGenerator.Tests/…/ExpectedGeneration/*.g.cs` are checked in and
diffed, so code-generation drift shows up as a reviewable text diff rather than
an opaque failure. Hukbo has the analogous instinct — the pinned SplitMix64
vectors, the `seed-1-200-agents-preclash-digest.json` fixture — and the general
lesson is worth stating: **a golden file that says what changed is better than a
hash that says only that something did.** A hash tells you determinism broke; a
diff tells you which field moved.

**Design rationale in XML doc comments at the symbol.** Arch puts the L1-cache
reasoning on `GetChunkSizeInBytesFor` and the thread-safety warning on
`ParallelQuery`, where a caller sees them. Hukbo already does this well in
places, including the ownership note on `CollisionUniformGrid.Pairs` at `:121-125`
and the equivalence contract at `:12-15`. The gap section 5.3 identifies —
`Grow`'s no-copy invariant living only in the implementer's head — is exactly
what this practice prevents.

**A `Dangerous*` naming segregation for API that breaks an invariant**, so
"unsafe to call" is visible at the call site rather than in a document. Hukbo has
no such API today and should not acquire one casually; the convention is recorded
so that if one ever becomes necessary, it is named rather than hidden.

**Documentation pages that open with an explicit `Limitations:` block, before any
example.** This is the practice with the widest applicability here, because it
directly serves `CLAUDE.md`'s honesty rules and the smoke-checklist protocol: a
reader learns what a thing cannot do before they learn how to call it. It costs
three lines per document.

## 11. Benchmark methodology traps

Arch's own harness demonstrates several mistakes worth naming before Phase 1
writes a single number down.

- It sets `ConfigOptions.DisableOptimizationsValidator` and
  `JitOptimizationsValidator.DontFailOnError`, which switch off the guard that
  the assemblies under test were built optimized. **Never copy those two lines.**
  The canonical gate builds Release for exactly this reason.
- Its published cross-library figures benchmark `Arch 1.2.8.1-alpha` while
  current Arch is `2.1.0`. A performance number is attached to a version or it is
  attached to nothing — which is why every Hukbo figure in
  `docs/development/testing.md` is recorded beside its hashes.
- `[HardwareCounters]` silently no-ops without Windows ETW and administrator
  rights. A measurement instrument that fails quietly is worse than one that is
  absent.
- `QueryBenchmark` mutates a `private static World?` in place across seven
  benchmark methods, with `[GlobalSetup]` running once per `[Params]` value, so
  identical work per iteration is not guaranteed. Hukbo's headless runner does
  better by construction — a fresh simulation per run, hashed at the end — and
  Phase 1 must not regress that by reusing state between sweep points.

The positive lesson from the same harness is the `[Params]` sweep itself, and the
`EntityPadding` fragmentation parameter that measures the awkward case rather than
only the friendly one. Section 4.2 takes the first; the second has a Hukbo
analogue worth considering later, in the form of a workload that keeps agents
spread out so the perception broad phase is stressed without combat thinning the
population — which
`docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md` and the standards already
name as `spread-500`.

## 12. Rejected options and open questions

**Rejected: adopting Arch, or any ECS, as a dependency.** Section 1. It is
forbidden by `CLAUDE.md` section 9 until a profiler demands it, and this design's
entire structure is an argument that the profiler has not spoken yet.

**Rejected: instrumenting `Hukbo.Core` with timing.** Forbidden by the wall-clock
prohibition in `CLAUDE.md` section 3, and unnecessary given section 4.3.

**Rejected: adding a runtime configuration knob on the strength of a general
claim.** Section 9.

**Rejected: `Handle<T>`.** Section 6.6 — a good pattern with no current
application here.

**Resolved, 2026-07-28: `tools/` placement is sufficient containment for the
staged driver.** The question was whether exposing the event accumulator across
the `Hukbo.Core` boundary, so that option (b)'s driver can time each stage from
outside, creates a surface that later code will misuse. It does not, because the
driver lives in `tools/Hukbo.Tools.TickProfile`, which is outside `Hukbo.slnx`
and outside the canonical gate — nothing in a shipped assembly can bind to it,
and the equivalence test required by T5 fails loudly if the staged path and
`AdvanceOneTick` ever diverge. This remains conditional on the Gate A verdict:
if the option (a) profile is unambiguous, the seam is never opened at all.

**Resolved, 2026-07-28: parameterise `CollisionUniformGrid` rather than building
a second grid.** Section 6.2's perception broad phase wants a larger cell size
than the collision grid, because the perception radius is larger than the contact
radius. Two live grid instances per tick is the cost, and it is the cheaper cost:
one implementation and one naive oracle serve both callers, so the proof that the
accelerated path reproduces the O(n²) result exactly is written once rather than
twice, and a future correction to the grid cannot fix one caller and miss the
other. The cell size becomes a construction parameter; nothing about the
traversal order, the packed cell key, or the sorted pair output changes, so the
grid's existing documented contract still holds for both instances.

This decision is reversible on evidence, and the reversal condition is worth
stating: if a prototype shows that the perception-radius instance produces cell
occupancy high enough that the 3×3 neighbourhood walk degenerates toward the
O(n²) scan it replaces, then the perception index wants a different structure
rather than a differently sized grid, and that is a new design question rather
than a parameter change.

**Open question.** The scaling sweep in section 4.2 uses the default even
roster. Preset V2's six loadouts have different reach and cooldown values, so a
roster stacked toward a long-reach loadout will engage differently and produce a
different tick-cost curve. `Hukbo.Tools.WeaponBalance` already sweeps stacked
rosters and its README records the constraint that
`Scenario.RosterCounts` applies identically to both factions. Whether the
performance sweep needs the same treatment is unresolved, and the honest default
is to report the even roster and say so.
