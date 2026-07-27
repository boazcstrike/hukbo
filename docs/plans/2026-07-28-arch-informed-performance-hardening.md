# Arch-Informed Performance Hardening — Implementation Plan

Date: 2026-07-28

**Status:** not started. Phase 1 is authorized; Phase 3 is authorized per item only by the Gate A verdict that Phase 1 produces.

Design: [`2026-07-28-arch-informed-performance-hardening-design.md`](2026-07-28-arch-informed-performance-hardening-design.md)

Evidence: [`docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md`](../research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md)

**Goal:** Obtain the measurement Gate A requires, remove the per-tick allocations
that are visible in the source without it, and let the resulting profile decide —
item by item — which structural changes are worth making. Every task is
hash-neutral.

**Architecture:** No new project in `Hukbo.slnx`, no new package, no build-flag
change, and no ECS. Measurement instrumentation lives in `Hukbo.Headless` and,
only if the Gate A verdict requires it, in a new hand-run harness under `tools/`.
`Hukbo.Core` gains no reference to the wall clock, the filesystem, or
`Hukbo.Diagnostics`.

If this plan and the design appear to disagree, stop the affected task and
resolve it in the design first.

## Nine questions

Per `SIMULATION-GAME-STANDARDS.md` section 10.

1. **User-visible outcome.** Deliberately none. This workstream is defined by the
   fact that a spectator cannot tell it happened: the same battle plays out, the
   same events appear in the same order, the same faction wins on the same tick.
   The one place where a defect *would* become visible is the battle event feed,
   because T7 changes the lifetime of the collection `LastEvents` returns — if
   that change is wrong, the feed shows stale or empty lines during a live run.
   That is the single manual smoke row this workstream adds. The other outcome is
   an artifact rather than a behaviour: `docs/development/testing.md` gains a
   scaling curve, a Core-only allocation figure, and a stage profile that do not
   exist today.
2. **Tick stage and state read/written.** No new tick stage, and the fixed stage
   order in `BattleSimulation.AdvanceOneTick` (`BattleSimulation.cs:277-301`) is
   unchanged. T6 touches `MeasureCollision`, which the standards already record as
   pure observation that writes no agent state. T7 touches the event accumulator
   threaded through `CommitMovement`, `GatherAndCommitAttacks`, and
   `ResolveOutcome`. T11 and T12 sit inside `SelectTargetsAndIntents`. T13 touches
   the identifier-to-index lookup used by `MeasureCollision`. **No authoritative
   field is added, removed, reordered, or renumbered by any task in this plan.**
3. **Numeric units and bounds.** No new number reaches either hash. The numbers
   this workstream produces are harness-side only: tick durations in
   milliseconds, allocation in bytes, sample counts. T11's axis-delta rejection
   compares raw fixed-point axis deltas against the raw perception range in the
   same `long` arithmetic the existing squared-distance path already uses, and it
   rejects a strict subset of what the existing perception test rejects, so no
   same-tick conflict rule changes.
4. **Total ordering and random-stream policy.** No new random draws, and no
   existing stream is touched. `SplitMix64` is not consulted. Every ordering is
   preserved exactly: the target tie-break at `BattleSimulation.cs:557-561`
   (`eligible desc, distance_squared asc, target_entity_id asc`), the rally
   tie-break at `:624-627`, the give-way sign at `:888`, the collision priority
   key at `CollisionResolver.cs:369-371`, and the normalised, sorted pair order the
   uniform grid produces. Any task that cannot preserve one of these exactly is
   abandoned rather than adjusted.
5. **Cache.** T13 replaces one derived lookup structure with another derived
   lookup structure; both are rebuildable, neither is hashed and neither is
   saved. T12, if it is authorized at all, adds a derived accelerator with the
   same classification the collision grid already carries: rebuilt every tick from
   authoritative positions, bounded by the living agent count, never hashed, never
   snapshotted, never persisted, and proven against a naive oracle for cold-cache
   equivalence. No unbounded cache is introduced. Every other task is "no cache".
6. **Save, event, and version effect.** None. No preset version, no new
   `CombatPresetId`, no golden expectation re-recorded, no snapshot schema change,
   no `ClientSettings` schema change. **Both hashes must be byte-identical at
   every task boundary**, which is verification criterion 2.
7. **Worst-case complexity and benchmark workload.** The known worst case is
   target selection at `BattleSimulation.cs:530`/`:544`, which is O(n²) in living
   agents per tick and is the only loop in `Hukbo.Core` with no spatial
   acceleration. Its cost over a full run is **unmeasured** and T3 measures it.
   The benchmark workload is the canonical 200-agent, 10,000-tick, seed-1 headless
   run, plus the agent-count sweep recorded by T2 and the report-only 500-agent
   run the standards already require.
8. **Spectator explanation.** Not applicable, and that is the point rather than an
   omission. Section 10 item 8 asks whether a spectator can discover an effect
   without reading source code; here there is no effect to discover, because a
   discoverable difference would mean a hash moved. The inspectable evidence that
   the work happened is the recorded before-and-after figures in
   `docs/development/testing.md`, which is the audience this workstream actually
   has.
9. **Tests that fail before and pass after.** Enumerated per task in the table
   below and consolidated in the verification criteria. Note that the hash-neutral
   tasks invert the usual shape: several of them are verified by tests that pass
   *both* before and after, because the contract is that nothing changed. Those
   tasks carry an allocation or timing figure as their falsifiable observable
   instead.

## Task list

| # | Task | Files | Depends on | Done when | Verified by |
| --- | --- | --- | --- | --- | --- |
| T1 | Split the headless allocation counter so a `Hukbo.Core`-only per-tick figure exists. Keep `allocatedBytes` with its current meaning (whole-loop harness total) and add a second field accumulated from a `GC.GetAllocatedBytesForCurrentThread()` delta taken immediately outside the existing `Stopwatch` bracket around `left.AdvanceOneTick()`, so the timing window is unchanged. Do not move the `tickDurations.Add` call inside the allocation window — a `List<double>` grow would be counted as simulation allocation | `src/Hukbo.Headless/HeadlessRunner.cs`, `src/Hukbo.Headless/RunReport.cs`, `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | — | The report carries both figures; at 200 agents / seed 1 the Core figure is strictly less than the harness total and both are recorded verbatim; `stateHash` is still `71211929A44A16CA` and `eventHash` still `A2DC3ECA3F7345ED` | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`, output pasted into `docs/development/testing.md`; a new test in `HeadlessRunnerTests.cs` asserting the Core figure is positive and never exceeds the harness total |
| T2 | Record an agent-count scaling curve using the existing runner and its `--output <json-path>` flag. At minimum 200, 500, 1,000, and 2,000 agents at seed 1, reporting p50/p95/p99/max, the T1 Core allocation figure, `measuredTicks`, and both hashes per point. Construct a fresh simulation per point — never reuse state between sweep points | `docs/development/testing.md` | T1 | A table exists with one row per agent count and states plainly whether tick cost grows linearly or faster in agent count. Each row names its hashes so a later reader can tell which build produced it | The pasted JSON reports; no source file is modified by this task |
| T3 | Produce the Gate A stage profile by sampling the **unmodified** Release headless seed-1 workload with `dotnet-trace` through the `dotnet-diag` plugin. Record inclusive and exclusive sample share for each of the eight named stages in `AdvanceOneTick`, and state explicitly which stages the sampler could not resolve because of inlining | new `docs/research/TICK-STAGE-PROFILE.md` | T1 | A per-stage sample-share table exists for the 200-agent seed-1 Release run, with the sampling interval, the tool version, and the hardware named per `SIMULATION-GAME-STANDARDS.md` section 8. The document opens with a `Limitations:` block stating that these are sample shares and not per-tick percentiles | The trace file and the pasted summary; no source file is modified by this task |
| T4 | Write the Gate A verdict: for each of T11, T12, T13, and the section 6.4 layout question, record `authorized`, `closed`, or `undecided` against the precondition and abandonment condition the design states, quoting the T3 figures as the reason. Also decide T5: option (b) is authorized if and only if no single stage exceeds thirty percent of inclusive samples, or the top two stages are within five percentage points of each other | `docs/plans/2026-07-28-arch-informed-performance-hardening.md` (this file, a new "Gate A verdict" section) | T2, T3 | Every Phase 3 item carries one of the three words and a quoted figure. An item marked `closed` names the number that closed it | Review against the design's stated preconditions; no code is written by this task |
| T5 | **Conditional on T4 only.** Build the staged tick driver: raise the eight stage methods to `internal`, expose the event accumulator across that seam, add a `tools/Hukbo.Tools.TickProfile` harness that times each stage from outside Core, and add an equivalence test in the shape of `FullTraceLoggingDoesNotChangeTheSimulationResult` requiring identical state hash, event hash, outcome, and ordered event stream between `AdvanceOneTick` and the staged path. The harness is **not** added to `Hukbo.slnx` | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, new `tools/Hukbo.Tools.TickProfile/*`, `tools/README.md`, `tests/Hukbo.Core.Tests/DeterminismTests.cs` | T4 | Per-stage p50/p95/p99/max exist for the seed-1 200-agent workload, and the equivalence test passes. `Hukbo.slnx` has zero diff. If T4 marked this `closed`, the done condition is that T4 says so and no code was written | The equivalence test; `./scripts/verify.ps1 -SkipBootstrap` still passing with unchanged hashes |
| T6 | Remove the boxed enumerator in `MeasureCollision` by iterating the concrete backing list rather than the `IReadOnlyList<CollisionPair>`-typed `Grid.Pairs`. Keep `Pairs`'s public contract, its documented ownership note, and its ordering exactly as they are | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs` | — | The `foreach` at the `MeasureCollision` call site binds to a struct enumerator; `RepeatedCollisionTicksHaveBoundedAllocations` still passes with a second-window figure no higher than the 815,312 bytes currently recorded, and the new figure is pasted | `./scripts/test.ps1 -Configuration Release`; the pasted allocation figure; unchanged hashes from `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` |
| T7 | Reuse the per-tick event buffer. Replace the `events ??= new List<BattleEvent>(_agentStates.Length * 2)` allocations at `BattleSimulation.cs:1463` and `:1499` with one simulation-owned list cleared at the top of each tick, and replace the per-tick `events.AsReadOnly()` at `:300` with a single `ReadOnlyCollection<BattleEvent>` created once over it. Document the new lifetime at the symbol in the same form `CollisionUniformGrid.Pairs` already uses at `CollisionUniformGrid.cs:121-125` | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T1, T6 | The T1 Core-only allocation figure for 200 agents / seed 1 falls by a recorded amount, and both hashes are byte-identical to the baseline pair. `LastEvents` returns the same ordered events it did before, asserted over a full seed-1 run | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` before and after, both pasted; `RepeatedQuietTicksHaveBoundedAllocations` and `RepeatedCollisionTicksHaveBoundedAllocations` |
| T8 | Audit every caller of `BattleSimulation.LastEvents` against T7's new lifetime and fix any caller that retains the collection past the next `AdvanceOneTick`. Add a test that pins the contract: a reference captured on one tick must not be assumed valid on the next, and the client's event feed must observe each tick's events within that tick | `src/Hukbo.Client/**` (call sites only), `tests/Hukbo.Client.Tests/**`, `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | T7 | Every `LastEvents` call site is enumerated in the completion record with a verdict of "reads within the tick" or "fixed"; the new test fails against a naive retain-and-compare and passes after | `./scripts/test.ps1 -Configuration Release`; no Client test constructs `ArenaGame`, a graphics device, a sprite batch, or a window |
| T9 | Document and test the `CollisionResolver.Grow<T>` no-copy invariant. Add an XML doc comment at the symbol stating that the buffer is replaced without copying and is legal only because `Reset` refills every slot before any read, and add a test that fails if a grown buffer is read before `Reset` refills it | `src/Hukbo.Core/Simulation/CollisionResolver.cs`, `tests/Hukbo.Core.Tests/CollisionResolverTests.cs` | — | The invariant is stated at the symbol and asserted by a test that fails when the refill is removed | `./scripts/test.ps1 -Configuration Release`; unchanged hashes |
| T10 | Add a "Performance technique inventory" section to the standards document carrying the section 1 non-adoption statement, the portable / portable-with-discipline / forbidden tables from design sections 7 and 8, the lookup-only-hash-container rule, and the requirement that a snapshot header carry a preset version and a schema version with a mismatch as a hard failure | `SIMULATION-GAME-STANDARDS.md` | — | The section states plainly that no ECS is being adopted, names every forbidden technique with its reason, and does not contradict `CLAUDE.md` section 9 | Read-through against design sections 7 and 8; `./scripts/format.ps1 -Verify` |
| T11 | Reject a target-selection candidate on an axis delta before computing its squared distance at `BattleSimulation.cs:551`. The rejected set must be a strict subset of what the existing perception test rejects, so the tie-break at `:557-561` sees an identical surviving order | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T4, T7 | Both hashes are byte-identical to the baseline pair, and p50/p95 tick time at 200 agents / seed 1 does not regress. If T4 marked this `closed`, the done condition is that T4 says so and no code was written | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` before and after, both pasted; existing targeting tests in `BattleSimulationTests.cs` |
| T12 | **Conditional on T4.** Add spatial acceleration to target selection. Per design section 12, make `CollisionUniformGrid`'s cell size a construction parameter and run a second instance sized for the perception radius — do not write a second grid type. The traversal order, the packed cell key, and the sorted pair output must not change, so the existing collision instance keeps byte-identical behaviour. Ship a hand-written naive oracle in the shape of `NaiveCollisionPairs.cs` proving the accelerated path selects exactly the same target for every agent on every tick of a generated worlds suite | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`, new `tests/Hukbo.Core.Tests/NaiveTargetSelection.cs`, `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`, `tests/Hukbo.Core.Tests/CollisionUniformGridTests.cs` | T4, T11 | The oracle test passes across generated worlds; both hashes are byte-identical; the existing `CollisionUniformGridTests.cs` suite passes unchanged against the collision-sized instance; p95 tick time at 200 and 2,000 agents improves by a recorded amount. If T4 marked this `closed`, the done condition is that T4 says so and no code was written | The naive-oracle test; `./scripts/benchmark.ps1` at 200 and 2,000 agents before and after, both pasted |
| T13 | **Conditional on T4.** Replace `Dictionary<ulong,int> _agentIndexes` with a dense identifier-to-index map. Use integer ceiling division and `BitOperations.Log2` — no float may appear in a `Hukbo.Core` file. Confirm first whether the jagged bucket structure is needed at all, given that `EntityId` is monotonically increasing and never reused within a match | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, new `src/Hukbo.Core/Simulation/*` map file, `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | T4, T12 | Every lookup returns the same index the dictionary returned, asserted over a full seed-1 run; both hashes byte-identical; no float literal or `System.Math` floating-point call in the new file. If T4 marked this `closed`, the done condition is that T4 says so and no code was written | New equivalence test; `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` before and after |
| T14 | **Conditional on T4.** If and only if T4 marked the section 6.4 layout question `authorized`, write a **design document** for the `AgentState` layout change — it is out of scope for this plan and must not be implemented here. It must address the storage-order dependency in `StateHasher.Compute` at `StateHasher.cs:53`, the positional coupling in `CommitMovement` at `BattleSimulation.cs:972-1013`, and the test surface the design enumerates | new `docs/plans/YYYY-MM-DD-agent-state-layout-design.md` | T4 | Either the design document exists and does not authorize implementation, or T4 marked the question `closed` and no document was written | Read-through against design section 6.4; no source file under `src/` is modified by this task |
| T15 | Update the plans index so a later reader can tell what this workstream did and did not change, including that it is hash-neutral and adopted no ECS | `docs/plans/README.md` | T14 | The index describes this workstream accurately and does not imply any behavioural change | Read-through |
| T16 | Run the canonical gate and paste the actual output, including the unchanged hash pair and the before-and-after allocation figures | `docs/development/testing.md` | T5, T6, T8, T9, T10, T11, T12, T13, T15 | Five `[PASS]` stages, with `stateHash 71211929A44A16CA` and `eventHash A2DC3ECA3F7345ED` recorded verbatim from the output rather than copied from this plan | `./scripts/verify.ps1` |
| T17 | Add manual smoke rows for the event-feed lifetime change — the feed shows correct ordered events during a live run, survives pause and speed changes, and shows nothing stale after a battle ends — all left `PENDING` | `docs/development/testing.md` | T16 | The rows exist and are `PENDING`. No agent flips a row | Read-through; only a person at an interactive Windows desktop may flip one |
| T18 | Review the complete diff | — | T17 | No hash moved, no enum renumbered, no preset edited, no `AllowUnsafeBlocks`, no new package, no `Directory.Packages.props` or `packages.lock.json` diff, no `Hukbo.Diagnostics` reference in `Hukbo.Core`, no wall-clock or filesystem access in `Hukbo.Core`, no console write outside the two `Program.cs` entry points, no `Hukbo.slnx` diff, no GitHub Actions workflow | `/code-review` on the diff; `./scripts/verify.ps1` output from T16 |

## Verification criteria

Complete only when all of the following hold.

1. `./scripts/verify.ps1` passes all five stages, with the actual output pasted
   into `docs/development/testing.md`.
2. **Neither hash moved.** `stateHash` is `71211929A44A16CA` and `eventHash` is
   `A2DC3ECA3F7345ED` at 200 agents / seed 1, recorded verbatim from the gate
   output. This is the inverse of the usual criterion and it is the whole point:
   a moved hash here means a task changed what the simulation computes, and the
   task is wrong.
3. A `Hukbo.Core`-only per-tick allocation figure exists and is recorded, distinct
   from the harness total.
4. A scaling curve exists across at least four agent counts, and it states plainly
   whether tick cost grows faster than linearly.
5. A stage profile exists and is recorded, with its sampling interval, tool
   version, and hardware named, and with an explicit `Limitations:` block.
6. Every Phase 3 candidate carries a written verdict of `authorized`, `closed`, or
   `undecided`, each quoting the figure that produced it. No candidate is left
   without one.
7. The T1 Core-only allocation figure after T7 is lower than before it, by a
   recorded amount, on the same workload.
8. `RepeatedQuietTicksHaveBoundedAllocations` and
   `RepeatedCollisionTicksHaveBoundedAllocations` both pass, and the second
   window in the collision test is still no greater than the first — the real
   guard on buffer reuse.
9. p95 tick time and the whole-process working set have not regressed by more
   than ten percent against the recorded baseline, per
   `SIMULATION-GAME-STANDARDS.md` section 8. A regression above that threshold
   requires review before integration, regardless of what else improved.
10. `Hukbo.Core` still references neither MonoGame nor `Hukbo.Diagnostics`, still
    touches neither the wall clock nor the filesystem, and
    `DiagnosticLoggingBoundaryTests.cs` and
    `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` both still pass.
11. `Directory.Packages.props` and all ten tracked `packages.lock.json` files have
    zero diff. `Directory.Build.props` has zero diff. `AllowUnsafeBlocks` appears
    nowhere in the repository.
12. `Hukbo.slnx` has zero diff. Anything added under `tools/` is outside the
    solution and outside the gate.
13. No smoke-checklist row was flipped to `PASS` by an agent.
14. No `.github/` workflow was added, and no CI was proposed.

## Risks

**The biggest allocation claim in this plan is currently an inference.** Design
section 4.1 estimates that the per-tick event list is on the order of the entire
recorded per-tick allocation, from a 400-slot array of 72-byte events. That
arithmetic is sound but the recorded 93,905,304 bytes covers two simulations, both
hash computations, the `SequenceEqual` comparison, and the harness's own boxing.
T7's value is therefore unknown until T1 lands. This is the reason T7 depends on
T1 rather than running first, and an implementer who reverses that order will
produce an unfalsifiable claim.

**T7 changes a lifetime, and lifetime bugs do not fail a build.** A caller that
retains `LastEvents` past the next tick will silently observe the wrong events
rather than crash. `docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md` names
this exact hazard in its Gate C criteria. T8 exists because T7 is not finished
when the allocation number improves; it is finished when every caller has been
enumerated and judged. This is also the only part of the workstream a spectator
could see go wrong, which is why T17's smoke rows are about the event feed and
nothing else.

**T5 widens `Hukbo.Core`'s internal surface around the event accumulator**, which
is the input to the event hash. It is conditional for that reason. Design section
12 records the decision that placing the driver in `tools/Hukbo.Tools.TickProfile`
— outside `Hukbo.slnx` and outside the gate — is sufficient containment, because
no shipped assembly can bind to it and the equivalence test fails loudly if the
staged path and `AdvanceOneTick` diverge. That decision does not authorize the
task: if T4 does not authorize it, do not build it "while we're here".

**A sampling profiler can lie by inlining.** T3 may attribute a stage's samples to
`AdvanceOneTick` itself, and the recorded p50 tick time of 0.0812 ms is coarse
relative to a default sampling interval. T3's `Limitations:` block is not
decoration — a Phase 3 item authorized on a misattributed profile is worse than
one left undecided, because it consumes the effort *and* produces a diff that has
to be reviewed and possibly reverted.

**T12 is the task most likely to move a hash by accident.** The target tie-break
at `BattleSimulation.cs:557-561` depends on candidates being visited in a total
order, and a broad phase that visits them in cell order visits them in a
different one. The tie-break is `(eligible desc, distance_squared asc,
target_entity_id asc)`, so an exact-distance tie between two candidates resolves
on entity identifier regardless of visit order — but only if the comparison is
written to depend on nothing else. The naive oracle is what proves this, and it is
not optional.

**T13's shift-and-mask arithmetic is where a float can sneak into `Hukbo.Core`.**
Arch's own bucket-sizing formulas use `Math.Ceiling` on floats and
`Math.Log(x, 2)`. Those are harmless in Arch and would be a determinism hazard
here. Integer ceiling division and `BitOperations.Log2` only.

**`AllowUnsafeBlocks` is a slope, not a step.** It is absent repo-wide and several
attractive techniques need it. Any task that reaches for it has left this
workstream.

## Explicitly out of scope

Carried from the design so it cannot drift in during implementation.

- **Adopting Arch, or any ECS, or any archetype or chunk system.** Forbidden by
  `CLAUDE.md` section 9 until a profiler demands it. This workstream produces the
  profiler output; it does not pre-empt its verdict.
- **Any package dependency.** No edit to `Directory.Packages.props`, no
  regeneration of any `packages.lock.json`.
- **Any build-configuration change.** No `AllowUnsafeBlocks`,
  `ServerGarbageCollection`, `TieredPGO`, `ReadyToRun`,
  `InvariantGlobalization`, or `runtimeconfig.template.json`.
- **The `AgentState` layout change itself.** T14 may produce a *design document*
  for it, conditionally. No task in this plan implements it.
- **`ref` accessors, `Unsafe.Add`, `MemoryMarshal.CreateSpan`, and
  `[SkipLocalsInit]`.** They presuppose the contiguous layout that is out of scope
  above, and two of them need `AllowUnsafeBlocks`.
- **`Handle<T>` and `Resources<T>`.** Assessed and declined in design section 6.6;
  Core has no value today that wants to be a handle.
- **Any parallelism.** `World.ParallelQuery`, `JobScheduler`, and
  `[Query(Parallel = true)]` are forbidden by the single-threaded authoritative
  schedule, permanently and not merely for now.
- **A source generator of any kind.**
- **Any change to a combat preset, a weapon attribute, a clash table, a roster, or
  a golden expectation.** Those are preset work and this is not preset work.
- **Instrumenting `Hukbo.Core` with the wall clock, the filesystem, or
  `Hukbo.Diagnostics`.**
- **Any GitHub Actions workflow or other hosted CI.** There is no CI in this
  repository and none is proposed.
- **Rendering, the client's frame loop, and audio.** The recorded percentiles here
  are simulation tick times from the headless runner; nothing in this workstream
  measures or changes a frame.

## Ordering note

**`src/Hukbo.Core/Simulation/BattleSimulation.cs` is a shared seam and it
serializes most of this plan. That is not a scheduling failure to be worked
around; it is a property of the file.** T5, T6, T7, T11, T12, and T13 all edit it,
and the dependency column reflects that: they run one after another, in that
order, and two implementers must not hold it at the same time. Anyone reading this
plan hoping for six parallel agents on Phase 3 should read the dependency column
first.

What genuinely parallelises is smaller and worth naming precisely:

- **T2 and T3 are disjoint from everything.** T2 writes only
  `docs/development/testing.md`; T3 writes only a new research document. Both are
  measurement against an unmodified build and both may run alongside any Phase 2
  code task.
- **T9 and T10 are disjoint from each other and from the `BattleSimulation.cs`
  chain.** T9 touches `CollisionResolver.cs` and its test file; T10 touches
  `SIMULATION-GAME-STANDARDS.md` only. Either may run at any time.
- **T8 is disjoint from the Core chain by file**, touching only `Hukbo.Client`
  call sites and test files, but it is *not* disjoint by logic: it depends on T7
  having defined the lifetime it audits against.
- **T6 before T7** is a deliberate ordering rather than a technical dependency.
  T6 is a one-line change with an allocation figure attached to it; running it
  first means T7's larger allocation claim is measured against a clean baseline
  rather than against a co-mingled one.

The phase gates are hard. T4 is the only task that may authorize T11, T12, T13, or
T14, and T4 cannot be written before T2 and T3 report. An implementer who begins a
Phase 3 task before T4 has recorded a verdict has skipped Gate A, which
`docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md` defines as required before
structural optimization.

One consequence is worth stating out loud so nobody treats it as a failure: **it
is a legitimate and possibly likely outcome of this plan that T11, T12, T13, and
T14 are all marked `closed` and no structural work happens at all.** In that case
the workstream delivers a Core-only allocation figure, a scaling curve, a stage
profile, three allocations removed, one undocumented invariant written down and
tested, and a standards section that stops the next reader re-deriving the same
argument. That is a complete result, not an abandoned one.
