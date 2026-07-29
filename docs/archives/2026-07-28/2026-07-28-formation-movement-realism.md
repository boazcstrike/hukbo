# Formation and movement realism — ordered task list

> **Archived: reference only.** This plan is complete and deprecated. Do not
> execute it, and do not treat its steps, versions, file paths, or
> line-number citations as current. The live contract is `CLAUDE.md` plus the
> skills in `.claude/skills/`. Persistent contingents (`PersistentContingentsV2`)
> is implemented, registered, and is `Scenario`'s default movement preset as of
> T15; `IndependentPursuitV1` stays registered, frozen, and byte-identical so
> its replays remain reproducible. The evidence this workstream produced is
> live and stays where it is, in
> [docs/development/testing.md](../development/testing.md) and
> `SIMULATION-GAME-STANDARDS.md`.

Companion to
[`2026-07-28-formation-movement-realism-design.md`](2026-07-28-formation-movement-realism-design.md).
Read that document first. It carries the reasoning, the exact arithmetic, the
historical position, the determinism argument, the deadlock analysis and the
rejected alternatives; this document carries only the ordered work.

Nothing outside the files each task names may be touched by that task.

## How this list is ordered, and why

The ordering is not arbitrary and it is not negotiable. Three principles decide
it.

**The frozen behaviour's reproducibility is proven before it is at risk.** T1
captures a per-tick trajectory oracle from the completely unmodified build,
before a single production file changes. Every later task that could plausibly
disturb the frozen behaviour is verified against that oracle.

**The preset mechanism and the hash move land before the behaviour.** T2 through
T6 build the version axis, move the state hash exactly once for purely
representational reasons, and prove the move representational by an unchanged
event hash, winner, survivor counts and tick count. Only then does T9 introduce a
rule that actually changes where a body stands.

**File ownership is disjoint among tasks that can run in parallel.** Two tasks
may share a file only when one depends on the other, directly or transitively.
Where a shared file forces sequencing that would otherwise be parallel, the task
says so.

**A task whose tests can fail because of an earlier task's production code owns
that production file as remediation authority.** This follows from the rule
above rather than competing with it — the two tasks are already ordered, so the
sharing is legal — but it has to be written down, because an earlier revision of
this plan wrote several test-only tasks whose file lists contained no production
file at all. A task in that shape is unable to act on its own findings: its tests
locate a defect in code it may not touch, and the plan authorizes nobody to fix
it. The remedy is not a new task; it is naming the file in the list of the task
that would find the defect, with the reason stated. Three tasks carry such an
entry — T11, T12 and T15 — and each says so where the entry appears.

Remediation authority is narrow. It permits repairing a defect the task's own
tests uncovered in a file it depends on. It does not permit new features, does
not permit editing an assertion an earlier task wrote, and does not permit
weakening a test to get green. A defect that turns out to be a design error
rather than an implementation error stops the task and is reported, exactly as
T5 stops rather than re-recording a golden T4 was supposed to leave alone.

**Every task that is not an ancestor of T4 and whose verification runs the full
test suite depends on T5.** T4 deliberately moves the state hash and leaves the
tree red; T5 re-records exactly the goldens T4 invalidated and returns it to
green. Any such task verified by
`./scripts/test.ps1 -Configuration Release` therefore cannot be checked between
those two, because it would see failures in files it does not own. The rule has
five direct instances — T6, T7, T8, T13 and T14 — and neither sharing nor not
sharing a file with T4 changes it: T8, T13 and T14 share none of T4's files and
all three need the edge, while T7 shares two of them and still needs the edge,
because T4's own sequencing stops at T4. T1, T2 and T3 need no edge because they
are ancestors of T4 and always report before it starts, and T9 onward need none
because they descend from T5.

A task marked **behaviour-inert** must leave the seed-1 trajectory byte-identical
and is verified by T1's fixture rather than by a new assertion.

---

## Stage 1 — the oracle and the mechanism

### T1 — Capture the frozen-behaviour trajectory fixture

Capture a per-tick digest of the current, completely unmodified simulation at
seed 1, 200 agents, and add the test that replays it. Follow the schema and test
shape of the existing combat-axis fixture and its two reproduction facts
(`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`;
`tests/Hukbo.Core.Tests/DeterminismTests.cs:704-817,897-996`): one row per tick
carrying `tick`, `eventCount`, `eventFold` and `stateHash`, plus final per-agent
rows carrying `entityId`, `xRaw`, `yRaw`, `hitPoints`, `intent`,
`movementResolution` and `loadout`. Reserve the two extra per-agent columns
(`contingentId`, `contingentState`) in the schema now, written as `0` for this
capture, so T6 does not have to reshape the file.

No production file is touched by this task.

- **Files:** `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1-digest.json` (new), `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` (new)
- **Depends on:** nothing
- **Verification:** `./scripts/test.ps1 -Configuration Release` — the new fact
  `IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest` passes against the
  unmodified `src/` tree, asserting every tick row and every final agent row.
  Running it twice in the same session must pass twice.

### T2 — The movement preset enum, ruleset and registry

Create the new version axis as pure data, wired to nothing. `MovementPresetId`
with `IndependentPursuitV1 = 1` only. `MovementRuleset` as an immutable value
carrying the preset id, a version, the tunable constants named in design section
3, and a `ContentHash` computed the way `CombatRuleset.ContentHash` is.
`MovementPresetRegistry` as an exhaustive static class whose `IsRegistered` and
`Get` switches throw `ArgumentOutOfRangeException` on an unregistered value
rather than falling back to a default — copy the shape of
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs:11-18,56-66` exactly.

**The constant set must be complete at this task, and the list is closed.**
`MovementRuleset` carries `CohesionRadiusMultiplier`, `CloseRadiusMultiplier`,
`MinimumCohesiveMembers`, `CohesionCycleTicks`, `CohesionDutyTicks`,
`ArrivalTaperMultiplier` and `OffsetUnit`. This is not a stylistic preference:
`ContentHash` is computed over the ruleset's fields, T2 pins
`IndependentPursuitV1`'s `ContentHash` to a literal, and the freeze in design
section 6.2 forbids that literal ever being edited. A field added to
`MovementRuleset` in T9 would move V1's `ContentHash` and break the freeze on
the very task that is supposed to be adding V2 without disturbing V1. Every
constant the behaviour will eventually need is therefore declared here, at its
frozen-preset value, even though nothing under V1 reads any of them.

Every constant carries a Provisional-reconstruction statement in its own XML doc
comment, matching `src/Hukbo.Core/Simulation/FormationRules.cs:1-8`.

- **Files:** `src/Hukbo.Core/Movement/MovementPresetId.cs` (new), `src/Hukbo.Core/Movement/MovementRuleset.cs` (new), `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` (new), `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` (new)
- **Depends on:** nothing. Runs in parallel with T1; their file sets are disjoint.
- **Verification:** `./scripts/test.ps1 -Configuration Release` — new facts assert
  that `IsRegistered(MovementPresetId.IndependentPursuitV1)` is `true`, that
  `IsRegistered((MovementPresetId)0)` and `IsRegistered((MovementPresetId)99)` are
  `false`, that `Get` throws `ArgumentOutOfRangeException` for both unregistered
  values, and that
  `MovementPresetRegistry.Get(IndependentPursuitV1).ContentHash` equals a pinned
  hexadecimal literal recorded by this task.

### T3 — `FormationPlanner` returns contingent membership

Change `PlanFactionDeployment` to return contingent membership alongside
positions, computed as
`contingentId = localIndex % ResolveContingentSizes(warriorCount).Length` on
**both** the lattice path and the `PlanDenseBlock` crowded-map fallback, which
currently ignores `contingentSizes` entirely
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:218-253`). Update the single call
site at `src/Hukbo.Core/Simulation/BattleSimulation.cs:173-175`.

Revise `FormationPlanner`'s type-level remarks at
`src/Hukbo.Core/Simulation/FormationPlanner.cs:24-30`. The sentences "it survives
only until tick 1" and "Nothing outside this file should treat a contingent as a
persistent unit" are the statement this workstream overturns and must not be left
standing. Replace them with text that keeps the historical caveat — the lattice
is an engineering device for guaranteeing non-overlap before the first tick and
is not a reconstruction of how anyone stood — while stating that membership is
now carried forward and consumed by the movement preset.

Not one coordinate and not one random draw may change. Contingent count, sizes,
dealing order, lattice, spacing, jitter and anchor rules are all untouched.

Nothing stores the membership yet — `AgentState.ContingentId` does not arrive
until T4. The call site must therefore consume the new return element with a
discard rather than an unused local, so `TreatWarningsAsErrors` does not fail
the build on a value nothing reads. The new fact below reads membership from
`FormationPlanner` directly, not through `BattleSimulation`.

- **Files:** `src/Hukbo.Core/Simulation/FormationPlanner.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/FormationPlannerTests.cs`
- **Depends on:** T1
- **Verification:** `./scripts/test.ps1 -Configuration Release` — T1's
  `IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest` passes byte-identically,
  which proves the positions and the random stream did not move; all twelve
  existing `FormationPlannerTests` facts pass unchanged; and a new fact
  `MembershipDealsRoundRobinAcrossContingentsOnBothPlacementPaths` asserts
  `contingentId == localIndex % contingentCount` for every warrior on the default
  200-agent scenario and on a scenario forced onto the dense-block path.

---

## Stage 2 — the single, deliberate hash move

### T4 — Add the three hashed values, behaviour-inert

Add, in one task so the state hash moves exactly once:

- `Scenario.MovementPreset`, defaulting to `MovementPresetId.IndependentPursuitV1`,
  validated by `MovementPresetRegistry.IsRegistered` inside `Scenario.Validate`
  mirroring the `CombatPreset` check at
  `src/Hukbo.Core/Simulation/Scenario.cs:226-232`, and added by hand to both the
  hand-written `Equals` and the hand-written `GetHashCode` at
  `src/Hukbo.Core/Simulation/Scenario.cs:93-149` — a new property is **not**
  picked up automatically there.
- `AgentState.ContingentId`, written once in `BattleSimulation.Create` from T3's
  membership and never mutated.
- `AgentState.ContingentState`, a new pinned append-only enum with
  `None = 0, Advance = 1, Hold = 2, Close = 3, Break = 4`, left at `None`
  everywhere by this task.
- Both new agent fields projected through `AgentState.ToView()` onto `AgentView`
  as defaulted positional parameters, matching how `MovementResolution` and
  `Level` are defaulted at `src/Hukbo.Core/Simulation/AgentView.cs:19-31` so
  existing presentation tests keep compiling.
- Three `Add` calls in `StateHasher.Compute`: `Scenario.MovementPreset` beside
  `Scenario.CombatPreset` at `src/Hukbo.Core/Determinism/StateHasher.cs:46`, and
  `agent.ContingentId` then `agent.ContingentState` appended after
  `agent.ComboTargetEntityId` at `src/Hukbo.Core/Determinism/StateHasher.cs:75`.

No behaviour changes. Nothing reads either new agent field.

**T4 and T5 are one red-to-green pair and must land in the same integration.**
Moving the state hash necessarily breaks T1's fixture, whose `stateHash` column
was captured before the move, and it breaks the pinned seed-1 `stateHash`
literals in `DeterminismTests`. That is expected and is the whole point of the
task; T5 repairs exactly those goldens and nothing else. So T4's own
verification is the benchmark run below plus the named new `ScenarioTests`
facts passing individually — **not** a green full suite, which is impossible
until T5 lands. No later task may begin from a tree in that intermediate state.

- **Files:** `src/Hukbo.Core/Simulation/Scenario.cs`, `src/Hukbo.Core/Simulation/AgentState.cs`, `src/Hukbo.Core/Simulation/ContingentState.cs` (new), `src/Hukbo.Core/Simulation/AgentView.cs`, `src/Hukbo.Core/Determinism/StateHasher.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/ScenarioTests.cs`
- **Depends on:** T2, T3
- **Verification:** `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`
  reports, in the same run, `eventHash 2A9F2D7054CD1805`, `outcome
  Faction1Victory`, `faction0Survivors 0`, `faction1Survivors 2`,
  `measuredTicks 1710`, `deterministic true`, `firstMismatchTick null`, and a
  `stateHash` that differs from `A883926A3B93792E`. That combination — an
  unchanged event hash, winner, survivor counts and tick count, alongside a moved
  state hash — is the proof that the move is representational. Additionally,
  `./scripts/test.ps1 -Configuration Release` passes new `ScenarioTests` facts
  covering `Validate` accepting `IndependentPursuitV1`, `Validate` rejecting
  `(MovementPresetId)99`, `CreateDefault` selecting `IndependentPursuitV1`, and
  two scenarios differing only in `MovementPreset` comparing unequal with
  different hash codes — the four-part shape
  `tests/Hukbo.Core.Tests/ScenarioTests.cs:400,415-426,473,483` establishes.

### T5 — Re-record every moved golden value

Update only what T4's representational hash move invalidated: the `stateHash`
column of T1's fixture, the pinned seed-1 `stateHash` literals in
`DeterminismTests`, and the recorded baseline block in
`docs/development/testing.md:87-127`. The re-recording note must state plainly
which hash moved, that the event hash did not, and why — quoting T4's actual
benchmark output rather than paraphrasing it.

The `eventFold` column and every final per-agent column in T1's fixture must be
byte-unchanged. If any of them moved, T4 was not behaviour-inert and this task
stops rather than re-recording them.

- **Files:** `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1-digest.json`, `tests/Hukbo.Core.Tests/DeterminismTests.cs`, `docs/development/testing.md`
- **Depends on:** T4
- **Verification:** every stage of `./scripts/verify.ps1 -SkipBootstrap` passes
  except the Core test stage, which still carries the two pre-clash failures
  T5b closes, and a `git diff` of the fixture shows changes confined to the
  `stateHash` column. T5 and T5b together are what return the tree to green
  after T4's deliberate breakage; see the note under T5b for why the work is
  split across two tasks rather than one.

### T5b — Re-record the pre-clash fixture

**Added after implementation began, in response to a blocked T5.** The original
plan assumed T4's change to `StateHasher.Compute` would invalidate only the
goldens this workstream had itself created. That was wrong. `StateHasher.Compute`
folds `Scenario.MovementPreset` and the two new per-agent words into *every*
simulation's state hash, not only into simulations running the new movement
preset, so a third fixture-backed pair of tests broke —
`DeterminismTests.ZeroInterceptionProfile_ReproducesThePreClashDigest` and
`DeterminismTests.ZeroInterceptionProfile_ReproducesTheRecordedStateHash`, both
of which read `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`.

No task in the plan named that fixture, so T5 stopped rather than edit a file it
did not own. That was the correct outcome and this task is the repair. There is
established precedent for the cost: `DeterminismTests.cs`'s own doc comments
record that this same fixture was re-captured by the earlier
`combat-preset-v3-combos` workstream the last time `StateHasher.Compute` gained
a field.

Re-record the fixture's per-tick `stateHash` column and its `terminalStateHash`
by replaying the scenario `CreateZeroInterceptionControlRun` builds —
`Scenario.CreateDefault(seed: 1, totalAgents: 200)` with
`CombatPreset = PrecolonialPhilippinesV1` and `TickLimit = 10000` — computing
`simulation.ComputeStateHash(PreClashContentHash)` at each tick, and update
`DeterminismTests.PreClashTerminalStateHash` to match. Recompute both values
yourself from a fresh run; do not carry over a number reported by an earlier
task.

Every other column of that fixture must be byte-unchanged. The event fold, the
event counts and the final per-agent rows all describe behaviour, and T4 changed
no behaviour. If any of them moved, T4 was not behaviour-inert after all, and
this task stops and reports rather than re-recording them — the same rule T5
carries, applied to the same evidence.

- **Files:** `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`, `tests/Hukbo.Core.Tests/DeterminismTests.cs`
- **Depends on:** T5. `DeterminismTests.cs` is shared with T5, which is why this
  is sequenced after it rather than run alongside it; T5's re-recorded
  `PresetV3` literal must not be disturbed.
- **Verification:** `./scripts/verify.ps1 -SkipBootstrap` completes with every
  stage passing — this is the task that finally returns the tree to green after
  T4's deliberate breakage — and a `git diff` of the pre-clash fixture shows
  changes confined to the `stateHash` column and `terminalStateHash`.

### T6 — `--movement-preset` on the supported entry points

Add a `--movement-preset` switch to `HeadlessRunner`, accepting either the enum
member name or its numeric value and rejecting anything else with exit code 2,
mirroring `TryParsePreset` at
`src/Hukbo.Headless/HeadlessRunner.cs:241-252,275-295`. Add the matching
`-MovementPreset` parameter to `scripts/benchmark.ps1`, passed through the same
way `-Preset` is at `scripts/benchmark.ps1:22-26,57-59`. Add the new switch to the
value-taking-argument list at `src/Hukbo.Headless/HeadlessRunner.cs:547`.

- **Files:** `src/Hukbo.Headless/HeadlessRunner.cs`, `scripts/benchmark.ps1`, `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs`
- **Depends on:** T4 for the `Scenario.MovementPreset` property, **and T5** for
  the re-recorded goldens its own verification reproduces. The T5 edge is not
  optional and is not merely stylistic: without it an orchestrator may legally
  run T6 in parallel with T5, at which point the `stateHash` T6 is asked to
  reproduce does not yet exist in the tree and the task cannot be checked at
  all.
- **Verification:** `./scripts/test.ps1 -Configuration Release` — new
  `HeadlessRunnerTests` facts assert that `--movement-preset IndependentPursuitV1`
  and `--movement-preset 1` both parse to the same value, that
  `--movement-preset 99` and `--movement-preset nonsense` both return exit code 2,
  and that omitting the switch selects the `Scenario` default. Additionally
  `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -MovementPreset IndependentPursuitV1`
  reproduces T5's re-recorded `stateHash` and `eventHash` exactly.

---

## Stage 3 — the shared helpers, still behaviour-inert

### T7 — Generalise the trail and give-way helpers from the rally agent to any leader

Refactor `ComputeRallyDirection`, `ComputeRallyTrailBase` and
`TryComputeGiveWayAimPoint`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:888-1020`) so they take a leader
agent and a trail distance as parameters rather than reading the faction rally
agent and `FormationRules.ComputeRallyTrailRaw` directly. Add the derived
computers to `FormationRules`: `ComputeContingentJitterRaw(bodyRadiusRaw, livingCount)`
returning `bodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)`,
`ComputeContingentTrailRaw(bodyRadiusRaw, jitterRaw)` returning
`((3 * jitterRaw + 1) / 2) + 3 * bodyRadiusRaw`, and their
`IsBodyRadiusWithin*Range` overflow guards, following the pattern at
`src/Hukbo.Core/Simulation/FormationRules.cs:197-299`.

Add the two geometric predicates the behaviour will need, both from design
section 3.5. They land here rather than with the behaviour because they are
derived geometry of exactly the kind this file already owns, and because T9 must
be able to call them without also owning them.

The first is the map-edge open-ground predicate,
`FormationRules.IsCohesionSquareWithinBounds(trailBaseXRaw, trailBaseYRaw, jitterRaw, bodyRadiusRaw, mapWidthRaw, mapHeightRaw)`,
a pure `bool` computed from four non-strict `long` comparisons against
`marginRaw = jitterRaw + bodyRadiusRaw`. No tolerance, no floating point.

The second is the cross-contingent overlap predicate,
`FormationRules.DoCohesionSquaresOverlap(aTrailBaseXRaw, aTrailBaseYRaw, aMarginRaw, bTrailBaseXRaw, bTrailBaseYRaw, bMarginRaw)`,
a pure `bool` that is `true` exactly when
`|aTrailBaseXRaw - bTrailBaseXRaw| <= aMarginRaw + bMarginRaw` **and** the same
holds on Y. All `long`, all exact integer arithmetic — no tolerance, no floating
point, no square root, no distance. Three properties are load-bearing and the
XML doc comment must state all three: the comparisons are **non-strict**, so two
squares in exact edge contact count as overlapping and deny each other cohesion,
which is the safe side because it can only ever remove a cohesion destination;
the predicate is **symmetric** in its two contingents by construction, since
both `Math.Abs` of a difference and a sum of margins are symmetric, so no
ordering rule and no tie-break is needed and both contingents yield together;
and it takes margins rather than jitters so a caller cannot pass a half-side
that disagrees with the one `IsCohesionSquareWithinBounds` uses.

**Wire the overflow guards into `Scenario.Validate`.** An earlier revision of
this task added the `IsBodyRadiusWithin*Range` guards and said they existed "so
`Scenario.Validate` can reject a bad body radius up front" without ever naming
`Scenario.cs` in its file list, which left the guards dead. `Scenario.cs` and
`ScenarioTests.cs` are therefore both in this task's file list. Both are shared
with T4, which this task depends on transitively through T5, so the shared
ownership is legal under the rule at the top of this document; the additions are
purely additive and no fact T4 wrote is edited.

The last-stand path must call the generalised helpers with exactly the arguments
it uses today, producing byte-identical results.

- **Files:** `src/Hukbo.Core/Simulation/FormationRules.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/Scenario.cs`, `tests/Hukbo.Core.Tests/FormationRulesTests.cs`, `tests/Hukbo.Core.Tests/ScenarioTests.cs`
- **Depends on:** T5
- **Verification:** `./scripts/test.ps1 -Configuration Release` — all five existing
  `FormationRulesTests` facts and all of `LastStandFormationTests` pass unchanged,
  and T1's fixture reproduces byte-identically. Seven new `FormationRulesTests`
  facts assert the derived-quantity guarantees numerically. Four cover the
  existing helpers:
  `(IntegerSquareRoot(4 * livingCount) + 1)` squared is strictly greater than
  `4 * livingCount` for every `livingCount` from 1 to 2000;
  `ComputeContingentTrailRaw` strictly exceeds `jitterRaw * sqrt(2) + 2 * bodyRadiusRaw`
  across the full body-radius and living-count ranges, with the comparison
  arranged so it cannot round in the design's favour;
  `IsCohesionSquareWithinBounds` returns `true` when the square fits exactly —
  that is, when a coordinate equals its boundary value — and `false` one raw
  unit beyond, asserted independently for each of the four comparisons; and
  `IsCohesionSquareWithinBounds` returns `false` for every trail base on a map
  smaller than `2 * (jitterRaw + bodyRadiusRaw)` on either axis.
  Three further facts cover `DoCohesionSquaresOverlap`: it returns `true` at
  exact edge contact, where a centre separation equals `aMarginRaw + bMarginRaw`,
  and `false` one raw unit farther apart, asserted independently on each axis;
  it returns `false` whenever the squares are separated on **either** axis
  alone, so overlap requires closeness on both; and it returns the identical
  answer with its two contingents' arguments exchanged, across a sweep of
  separations and deliberately unequal margins, which is what makes the "both
  contingents yield" property of design section 3.5 a tested fact rather than an
  intention. One new
  `ScenarioTests` fact asserts `Validate` rejects a body radius that overflows
  the new derived quantities and accepts the default.

### T8 — `ContingentOffset`

Add the pure offset function
`ContingentOffset.Compute(seed, entityId, jitterRaw)`:
`Fnv1a(ContingentTag, seed, entityId)` seeding a fresh `SplitMix64`, drawing two
unit values in `[-1024, +1024]` from `NextInt(2 * OffsetUnit + 1)`, and scaling
each into raw world units as `unit * jitterRaw / OffsetUnit` before returning,
with `OffsetUnit = 1024` and `ContingentTag = 0x484B424F5F435447` (`HKBO_CTG`).
The scaling lives inside the function, not at the call site, mirroring
`RallyOffset.Compute(seed, entityId, bodyRadiusRaw)`
(`src/Hukbo.Core/Simulation/RallyOffset.cs:43-61`), which likewise returns raw
units rather than a unit vector its caller must remember to scale.

The tick is not a parameter, and the type-level remarks must say why, citing the
jitter-and-stall failure `src/Hukbo.Core/Simulation/RallyOffset.cs:11-21`
records. Nothing calls it yet.

- **Files:** `src/Hukbo.Core/Simulation/ContingentOffset.cs` (new), `tests/Hukbo.Core.Tests/ContingentOffsetTests.cs` (new)
- **Depends on:** T2 for `OffsetUnit`, **and T5** for the same reason T6 and T13
  depend on it. T8 touches no file T4 touches, but its verification is
  `./scripts/test.ps1 -Configuration Release`, which runs the whole suite; run
  between T4 and T5 it would fail on the moved `stateHash` goldens in
  `DeterminismTests` and T1's fixture — files it does not own and must not
  repair. Runs in parallel with T6 and T7; its file set is disjoint from both.
- **Verification:** `./scripts/test.ps1 -Configuration Release` — new facts mirror
  the six in `tests/Hukbo.Core.Tests/RallyOffsetTests.cs:14-101`: the offset is
  stable across repeated calls for the same seed and entity; it does not depend on
  the tick; a sweep of ten thousand entities stays inside
  `[-jitterRaw, +jitterRaw]` on both axes for a fixed `jitterRaw`; a sweep of one
  thousand entities produces at least nine hundred distinct offsets; different
  seeds produce different offsets for the same entity; and offsets are
  symmetrically distributed about zero within a tolerance. Two further facts:
  one asserting `ContingentTag` differs from every existing domain tag in the
  repository, and one asserting **call-count independence** — one entity's
  offset computed in isolation equals the same entity's offset computed after a
  thousand other entities' offsets have been computed. That last fact is what
  makes design section 5.2's argument checkable rather than merely asserted,
  because the number of calls per tick is state-dependent once T9 lands.

---

## Stage 4 — the behaviour

### T9 — `ResolveContingentStates` and the cohesion movement branch

Add the ninth tick stage between `SelectTargetsAndIntents()` and
`GatherMovementProposals()` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:321-322`. Under
`IndependentPursuitV1` it returns on its first line. Under
`PersistentContingentsV2` it performs the two forward passes and the transition
rules of design section 3.4, writing `AgentState.ContingentState` on every living
agent, using preallocated sixteen-slot arrays sized once at construction.

The stage also resolves **both** geometric gates of design section 3.5, once per
contingent per tick, and stores each as one `bool` in its own sixteen-entry
array so that `GatherMovementProposals` reads a flag rather than recomputing
anything:

- **Gate 5, the map-edge test.** `FormationRules.IsCohesionSquareWithinBounds`
  against this contingent's unclamped trail base.
- **Gate 6, the cross-contingent test.** After every contingent's leader, living
  count, jitter, trail base and `marginRaw = jitterRaw + BodyRadiusRaw` are
  known for this tick — and not before, because no pair can be evaluated until
  both its members are resolved — walk the same-faction slot pairs and call
  `FormationRules.DoCohesionSquaresOverlap`. The walk is outer index ascending
  over the dense sixteen-slot array and inner index ascending from `outer + 1`,
  restricted to pairs in the same faction. Each contingent's flag is the logical
  OR over its pairs. Because the predicate is symmetric, both contingents of an
  overlapping pair are flagged and no tie-break exists; do **not** introduce an
  asymmetric rule where the lower `ContingentId` keeps cohesion, which design
  section 9 records as considered and rejected. The scan is at most
  `C(8, 2) = 28` pairs per faction and 56 in total, iterates no hash container,
  and allocates nothing.

When either flag denies cohesion, the stage records `Advance` rather than `Hold`
for that contingent, so the state a spectator reads in the inspector never
claims a contingent is gathering while its members are in fact pursuing
independently.

`FormationPlanner.MaximumContingents` is `private` today
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:45`). Widen it to `internal` and
use it for the slot arithmetic and the pair bound rather than declaring a second
`8` that could drift from it. That is the only change this task makes to
`FormationPlanner.cs`; no coordinate, no size, no dealing order and no random
draw moves.

The state machine is the six priority-ordered rules of design section 3.4, in
that order and no other. Rule 4, the cohesion duty cycle, is
`((Tick + cohesionPhase) % CohesionCycleTicks) < CohesionDutyTicks` with
`CohesionCycleTicks = 240`, `CohesionDutyTicks = 180` and
`cohesionPhase = slot * CohesionCycleTicks / 16`. It is a pure function of the
tick and the slot: no counter, no stored field, nothing added to the state hash,
nothing to initialise. It sits **above** the gathering test, so a shut window
forces `Advance` rather than merely competing with `Hold`.

Rule 4 sets the state label. It is **not** what enforces the bound. Gate 3 of
the movement branch below tests the same predicate independently, and that gate
is what the duration bound in design section 10.2's escape 4 rests on. Both
must be implemented. Do not drop gate 3 on the reasoning that rule 4 already
covers it — rule 4 leaves the contingent in `Advance`, and without gate 3 an
`Advance`-state straggler would still be pulled during a shut window, which is
exactly the bound the design claims cannot be exceeded.

Note what that bound is and is not, because the distinction decides which code
may be dropped in a later cleanup. Gate 3 bounds how many consecutive ticks any
agent can be aimed at a cohesion point. It says nothing about whether the
collision resolver grants the resulting movement. The argument that a body has
room to move is the quarter-density packing bound, and gates 5 and 6 are what
make that bound applicable — design section 10.2 sets out the division of
labour. **All three gates ship.** Dropping gate 6 in particular would restore
the exact hole an earlier revision of the design left open for failure shape 2.

**Gate 6's scan covers every *living* contingent, and the qualifier is not
decorative.** A contingent is living when at least one of its members is alive.
Slots whose living count is zero are excluded from the pair walk entirely —
their leader, trail base and margin are stale values from whichever tick they
last had a living member, and comparing against them would deny cohesion on the
strength of a square that no longer exists. Contingents in `Close` and in `Break`
are living and **are** included, even though neither can ever be granted
cohesion. That is deliberate and design section 3.5's chain-denial subsection
records both the reasoning and the pre-analysed narrowing that is the first
remedy if the inertness bar in T11 fails. Do not narrow it here.

Add `MovementPresetId.PersistentContingentsV2 = 2` and its registry arm; leave
`IndependentPursuitV1 = 1` and its arm byte-for-byte unmodified.

**`MovementRuleset.cs` is not edited by this task and is not in its file list.**
An earlier revision listed it with no stated change. Registering a second preset
needs a second `MovementRuleset` *value*, constructed in the registry arm from
the constants T2 already declared; it needs no new field, no new property and no
new method on the type. Adding a field here would move `IndependentPursuitV1`'s
`ContentHash` and break the freeze — that is verification criterion 3a, and it is
the whole reason T2 was required to close the constant set.

Add the cohesion branch to `GatherMovementProposals` implementing design section
3.5 exactly. For a living agent whose `Intent` is `Moving`, **six gates are
evaluated in this order, and each one sends the agent to
`BuildMovementProposal(agent, target)` — the frozen preset's independent pursuit
— rather than to a cohesion destination**:

1. the contingent's state is `None`, `Close` or `Break`;
2. the agent is its contingent's leader (`agent.EntityId == leaderEntityId[slot]`);
3. the duty-cycle window is shut for that slot on this tick;
4. **the state is `Advance` and the agent is not straggling.** Straggling is
   `16 * memberSquared > 9 * cohesionRadiusRaw * cohesionRadiusRaw`, where
   `memberSquared` is the existing private
   `BattleSimulation.SquaredDistance(agent, leader)`
   (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1703`) over tick-start
   positions. Squared comparison, `long` on both sides, no square root — the
   same shape `SelectTargetsAndIntents` uses at
   `src/Hukbo.Core/Simulation/BattleSimulation.cs:659-662`. The inequality is
   **strict**: exact equality is *not* straggling and takes independent pursuit.
   This gate does not apply in `Hold`, where every non-leader moving member is
   pulled;
5. the map-edge flag the stage above computed from
   `FormationRules.IsCohesionSquareWithinBounds(...)` says this contingent's
   bias square does not fit inside the map this tick;
6. the cross-contingent flag the stage above computed from
   `FormationRules.DoCohesionSquaresOverlap(...)` says this contingent's bias
   square overlaps some other living same-faction contingent's this tick.

Only an agent that passes all six gates gets a cohesion aim point. Do not
invent any additional test, and do not fold any of these six into another:
gate 4 in particular is what keeps `Advance` from becoming a loose column, and
design section 4.1 states plainly that the cited research does not support
continuous leader-relative binding of every member on every tick.

Gates 5 and 6 are array reads here, not recomputations. Do not evaluate either
predicate inside this per-agent loop: the cross-contingent one cannot be
evaluated correctly here at all, because the loop reaches agents before it has
reached the other contingents whose squares decide the answer.

The cohesion aim point is then the trail, the give-way corridor, the personal
offset from `ContingentOffset.Compute(seed, entityId, jitterRaw)`, and the
arrived-guard that proposes no movement when the aim point is already within
contact distance — matching `BuildRegroupingProposal`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:857-865`). The same-tick
conflict order is
`Dead > Attacking > Regrouping > contingent cohesion > ordinary pursuit`.

A warm tick must allocate nothing.

**The decidable arithmetic goes in pure internal statics, not inline, and this
task owns its own assertions.** An earlier revision gave T9 no test file at all
and deferred every functional check to T11, two tasks later. That left the
largest single block of new logic in the workstream — the leader scan, the two
forward passes, six priority-ordered transition rules, the duty cycle, and gates
5 and 6 — verified by nothing but "the frozen preset still reproduces", and the
frozen preset executes none of it. The fix is the same one T10 already carries:
extract what a test can call, then call it.

Create `src/Hukbo.Core/Movement/MovementRules.cs` — the file T10 later adds
`ComputeArrivalStepRaw` to — holding three internal statics. `Hukbo.Core` carries
`[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]`
(`src/Hukbo.Core/Properties/AssemblyInfo.cs:3`), confirmed present at that line,
so all three are directly callable from `Hukbo.Core.Tests` with no other
plumbing.

- `internal static bool IsCohesionWindowOpen(int tick, int slot, int cohesionCycleTicks, int cohesionDutyTicks)`
  returning `((tick + slot * cohesionCycleTicks / 16) % cohesionCycleTicks) < cohesionDutyTicks`.
  Both `ResolveContingentStates` and the gate-3 check in
  `GatherMovementProposals` call this one function, which is what makes the two
  stages structurally unable to disagree about the window.
- `internal static void ScanContingentLeadersAndLivingCounts(AgentState[] agents, ulong[] leaderEntityIdsBySlot, int[] livingCountsBySlot)`
  performing the single forward pass, comparing `EntityId` explicitly rather
  than array position, and writing `0` into both output arrays for a slot with
  no living member. The output arrays are the caller's preallocated sixteen-slot
  arrays, so the helper allocates nothing and the extraction costs no
  performance.
- `internal static ContingentState ResolveContingentState(ContingentState previousState, int livingCount, int initialCount, long spreadSquared, long nearestEnemySquared, long cohesionRadiusRaw, long closeRadiusRaw, int minimumCohesiveMembers, bool windowOpen, bool geometricGatesPass)`
  implementing the six priority-ordered rules of design section 3.4 and nothing
  else. Every argument is a scalar the stage already holds; the function touches
  no agent array, no simulation and no tick pipeline.

The six movement gates are a conjunction of unconditional denials, so their
listed order is a reading and short-circuit order and never a semantic one, as
design section 3.4 now states. Express them as one
`internal static bool IsCohesionEligible(ContingentState state, bool isLeader, bool windowOpen, bool straggling, bool squareFitsMap, bool squareOverlapsAnother)`
on the same type so that the conjunction is assertable directly, and have
`GatherMovementProposals` call it. Do not write a priority test for these six —
there is no priority to assert, and a test claiming one would be asserting a
property the code does not have.

- **Files:** `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Movement/MovementRules.cs` (new), `src/Hukbo.Core/Simulation/FormationPlanner.cs` (access modifier on `MaximumContingents` only), `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs`, `tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs` (new)
- **Depends on:** T6, T7, T8. `src/Hukbo.Core/Simulation/FormationPlanner.cs` is
  shared with T3, which this task depends on transitively through
  T6 → T5 → T4 → T3, so the shared ownership is legal under the rule at the top
  of this document. The change there is a single access modifier; no fact T3
  wrote is edited and T1's fixture proves nothing moved.
- **Verification:** `./scripts/test.ps1 -Configuration Release`.

  **The regression half**, unchanged from the earlier revision: T1's fixture
  still reproduces byte-identically under `IndependentPursuitV1`, proving the
  frozen preset was not disturbed; every existing test passes;
  `BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations` still
  passes at its 16,384-byte ceiling and 4,096-byte warm-window growth tolerance;
  and a new `MovementPresetRegistryTests` fact pins
  `Get(PersistentContingentsV2).ContentHash` to a literal distinct from V1's.
  `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` is in this task's file
  list precisely so that assertion is legal — an earlier revision required the
  fact but did not own the file, which under this document's own file-ownership
  rule made the task unable to satisfy its own verification. The addition is
  purely additive; T2 created the file and none of T2's facts may be edited.

  **The functional half**, new, in `tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs`.
  Every fact below calls a `MovementRules` static directly with hand-built
  arguments; none of them constructs a `BattleSimulation`, so none of them can
  pass because the scenario failed to reach the code:

  - `ContingentStateFallsToNoneWhenNoMemberIsAlive` — `livingCount == 0` yields
    `None` regardless of every other argument.
  - `BreakIsTerminalAndBeatsEveryOtherRule` — a previous state of `Break` yields
    `Break` even when the attrition test is not met, the enemy is inside
    `closeRadiusRaw`, the window is open and the spread exceeds
    `cohesionRadiusRaw`. Rule 1 over rules 2, 3 and 5.
  - `AttritionBreakBeatsCloseOnContact` — `livingCount * 4 <= initialCount`
    yields `Break` even with the enemy inside `closeRadiusRaw`. Rule 2 over
    rule 3. A second case covers `livingCount < minimumCohesiveMembers` with a
    healthy ratio, so the two attrition triggers are asserted independently.
  - `CloseOnContactBeatsTheGatheringTest` — an enemy inside `closeRadiusRaw`
    yields `Close` even with the spread far beyond `cohesionRadiusRaw` and the
    window open. Rule 3 over rule 5.
  - `AShutDutyCycleWindowForcesAdvanceOverHold` — `windowOpen == false` yields
    `Advance` with the spread far beyond `cohesionRadiusRaw`. Rule 4 over
    rule 5.
  - `AGeometricGateDenialForcesAdvanceOverHold` — `geometricGatesPass == false`
    yields `Advance` with the spread far beyond `cohesionRadiusRaw` and the
    window open, which is the property that keeps the inspector from reporting
    `Holding` while every member is pursuing independently.
  - `TheHysteresisBandEntersAboveTheRadiusAndLeavesBelowThreeQuarters` — three
    cases at one spread value strictly between `9/16` and `1` of
    `cohesionRadiusRaw` squared: previous `Hold` stays `Hold`; previous
    `Advance` stays `Advance`; previous `Close` — reachable only through
    rule 3 having lapsed — likewise yields `Advance`.
  - `TheDutyCycleWindowIsOpenExactlyTheDutyFractionOfEveryCycle` — over one full
    `CohesionCycleTicks` at a fixed slot, `IsCohesionWindowOpen` is true on
    exactly `CohesionDutyTicks` ticks, and over three full cycles its longest
    consecutive true run is exactly `CohesionDutyTicks`.
  - `TheSixteenSlotPhasesAreDistinct` — the sixteen values of
    `slot * CohesionCycleTicks / 16` are pairwise distinct, so no two
    contingents release together.
  - `TheLeaderIsTheLowestLivingEntityIdInItsContingent` — a hand-built
    `AgentState[]` spanning two factions and several contingents, asserted
    slot by slot against `ScanContingentLeadersAndLivingCounts`.
  - `LeaderSelectionIsUnchangedByAgentArrayPermutation` — three storage
    permutations of that identical roster produce identical leader and
    living-count arrays, mirroring
    `LastStandFormationTests.RallyAgentSelectionIsUnchangedByAgentArrayPermutation`.
  - `TheLeaderIsPromotedToTheNextLowestLivingEntityIdOnDeath` — the same roster
    with the current leader marked not alive yields the next-lowest living
    `EntityId`, and with every member of a slot dead yields `0` for both that
    slot's leader and its living count.
  - `CohesionEligibilityIsTheConjunctionOfAllSixGates` — an exhaustive sweep
    over the gate inputs asserting `IsCohesionEligible` is true only in the
    single all-permitting combination, and false in every one of the six
    single-denial cases and in every combination of them. The test's own comment
    must state that this is a conjunction rather than a priority order, and why.

  Each functional fact must be demonstrated to fail when the rule it covers is
  temporarily inverted. A fact that passes with its rule broken proves nothing
  and does not count.

### T10 — Arrival slowdown

Add the taper of design section 3.6, active only under
`PersistentContingentsV2`:
`movement = Max(1, Min(MovementSpeedRaw, remaining) * remaining / taperRaw)` when
`remaining < ArrivalTaperMultiplier * BodyRadiusRaw`, otherwise unchanged. All
`long` intermediates, no floating point.

**The arithmetic goes in a pure internal static, not inline.** Add
`internal static long ComputeArrivalStepRaw(long remainingRaw, int movementSpeedRaw, long taperRaw)`
to `src/Hukbo.Core/Movement/MovementRules.cs` — the file **T9 creates**, not a
new one; an earlier revision marked it new here because T9 had no extracted
helpers of its own — and have `BuildMovementProposal` call it under the new
preset. `Hukbo.Core` already carries
`[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]`
(`src/Hukbo.Core/Properties/AssemblyInfo.cs:3`), so the helper is directly
testable, which is the whole reason for extracting it. The addition is purely
additive: none of T9's four statics on that type may be edited.

**This task owns its own assertions.** An earlier revision left T10 with no test
file at all and deferred its correctness checks to T11, which is written
strictly afterwards. At the moment T10 was gate-checked its only live assertion
was that the *frozen* preset still reproduced — and the taper never executes
under the frozen preset, so that assertion proved exactly nothing about the code
the task added. The new test file below fixes that.

Sequenced after T9 rather than parallel to it because both own
`src/Hukbo.Core/Simulation/BattleSimulation.cs` and
`src/Hukbo.Core/Movement/MovementRules.cs`.

- **Files:** `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Movement/MovementRules.cs` (created by T9; this task appends one static), `tests/Hukbo.Core.Tests/ArrivalTaperTests.cs` (new)
- **Depends on:** T9
- **Verification:** `./scripts/test.ps1 -Configuration Release` — T1's fixture
  still reproduces byte-identically under `IndependentPursuitV1`, **and** new
  `ArrivalTaperTests` facts sweep `ComputeArrivalStepRaw` across the full
  remaining-distance range, from one raw unit to well beyond `taperRaw`, at
  several body radii and movement speeds, asserting on every sampled point that
  the result is at least 1, at most `movementSpeedRaw`, and at most the step the
  untapered formula `Min(movementSpeedRaw, remainingRaw)` would have produced.
  A further fact asserts the result is exactly the untapered step for every
  `remainingRaw >= taperRaw`, which is what proves the taper is confined to the
  final approach.

### T11 — The behaviour and liveness test suite

Write the new test file covering every claim design section 10.3 makes, plus the
state machine and the cohesion rule. At minimum:

- the twenty-seed sweep at 200 agents under `PersistentContingentsV2` asserting
  every battle reaches a terminal outcome strictly inside its tick limit,
  mirroring `LastStandFormationTests.cs:733-778`. Note plainly in the test's own
  comment that this sweep is **not** the liveness proof for the engineered
  failure geometries — the two failure shapes and the crossing-traffic residual
  alike; a sweep that passes shows only that twenty particular trajectories
  avoided them. T12 carries all three;
- **the cohesion duty cycle's hard bound**, in two forms over a full 200-agent
  battle: no contingent's `ContingentState` is `Hold` for more than
  `CohesionDutyTicks` consecutive ticks, and — the stronger form, and the one
  escape 4's duration bound in design section 10.2 actually asserts — no agent
  receives a cohesion destination on more than `CohesionDutyTicks` consecutive
  ticks in any state, `Advance` included. Neither form is a liveness proof on
  its own, and the test's own comment must say so: the duty cycle bounds how
  long the aiming lasts, not whether the movement is granted;
- **the inertness bar**, replacing the far weaker check an earlier revision
  carried here. That revision asserted only that "at least one contingent
  reaches `ContingentState.Hold` on at least one tick" of a single battle, which
  a build in which cohesion fired for a moment near deployment and never again
  would pass comfortably. Every guard in this design denies cohesion rather than
  adjusting it, so an inert build is silent by construction and needs an
  assertion that can actually fail. Implement design section 10.3's inertness
  bar exactly:

  Define **a contingent's pre-`Close` window** as the ticks from tick 0 up to but
  excluding the first tick on which that contingent's recorded `ContingentState`
  is `Close` or `Break`. Define **a contingent as cohering on a tick** when at
  least one of its living members passes all six gates of design section 3.5 on
  that tick — the agent-level definition, not `ContingentState == Hold`, because
  the design's ordinary mode is a straggler drawn back while the contingent's
  state is `Advance`, and counting only `Hold` would measure the exception and
  miss the rule.

  Across the same twenty-seed 200-agent sweep the liveness fact uses, assert for
  **every** seed and **every** faction:

  - **coverage** — at least half of the faction's contingents, rounded down and
    never fewer than two, cohere on at least one tick;
  - **persistence** — at least ten percent of the faction's pre-`Close`
    contingent-ticks, summed over its contingents, are cohering ticks;
  - **spread** — at least one cohering tick falls in the later half of the
    faction's pre-`Close` window, so a burst confined to deployment cannot
    satisfy the persistence threshold on its own.

  The test's own comment must record three things verbatim in substance: that
  these are **game-design thresholds, not measurements**, and nothing has been
  measured yet; that ten percent sits deliberately below the duty cycle's own
  ceiling of `CohesionDutyTicks / CohesionCycleTicks`, which is seventy-five
  percent, so the bar cannot collide with a mechanism working as designed; and
  that **if the bar fails, the cause is established before the number moves** —
  the first suspect is chain denial across converging contingents, whose
  pre-analysed remedy is design section 3.5's narrowing of the cross-contingent
  scan. Lowering a threshold to match an observed figure, with no stated reason
  for the figure, turns the bar back into the thing it replaced;

- **chain denial arises from genuine pairwise overlap, not from a propagation
  step**. Construct three contingents of one faction whose squares stand in the
  relation A overlaps B, B overlaps C, A disjoint from C — computing the trail
  bases and margins from `FormationRules.ComputeContingentJitterRaw` and
  `ComputeContingentTrailRaw` rather than guessing distances — and assert both
  that `FormationRules.DoCohesionSquaresOverlap` returns `false` for the A–C
  pair and that all three contingents are nevertheless denied cohesion on that
  tick. Design section 3.5's chain-denial subsection is what this pins: the rule
  is already pairwise and each of the three denials is its own fact;
- **the straggler gate**, in three forms: in `Advance`, a member inside the
  threshold proposes the identical destination it would propose under
  `IndependentPursuitV1`; a member beyond it does not; and a member at exactly
  `16 * memberSquared == 9 * cohesionRadiusRaw * cohesionRadiusRaw` takes the
  independent-pursuit branch, pinning which side of the boundary the strict
  inequality falls on;
- a member placed in its leader's forward corridor steps aside rather than
  through, and the give-way side is stable when it is exactly on the leader's
  axis, mirroring `LastStandFormationTests.cs:789-844,908-954`;
- the leader of a contingent is the lowest living `EntityId` in it, is unchanged
  by three storage permutations of an identical roster, and is promoted to the
  next-lowest on death — asserted here **through a running simulation**, which is
  a different claim from T9's `ScanContingentLeadersAndLivingCounts` facts and
  does not replace them. T9 proves the scan computes the right answer; this
  proves the stage actually calls it and acts on what it returns. Neither
  subsumes the other and both ship;
- living count never increases over a full battle;
- three storage permutations advanced in lockstep produce identical state hashes
  and identical ordered events every tick, mirroring
  `DeterminismTests.InputArrayOrderCannotChangeOrderedResults`
  (`tests/Hukbo.Core.Tests/DeterminismTests.cs:478-544`), with no pinned hash
  literal so the fact survives legitimate hash movement;
- each of the six transition rules in design section 3.4 is observed **in a
  running simulation** to select the state its trigger calls for, including
  `Break` being terminal and a shut duty-cycle window forcing `Advance` where the
  spread would otherwise select `Hold`. The rules' priority order itself is
  **not** re-asserted here: T9 pins it directly against
  `MovementRules.ResolveContingentState` with hand-built arguments, which can
  construct the exact input combinations where two rules compete, and a
  simulation cannot be steered into those combinations reliably. What this file
  adds is that the stage feeds the function the right arguments and writes its
  answer onto every living agent;
- a provisional maximum-blocked-streak bound across twenty seeds, recorded as
  provisional in the test's own comment the way the last-stand suite records its
  125-tick bound.

The arrival-taper properties are **not** in this file. T10 owns them, in
`tests/Hukbo.Core.Tests/ArrivalTaperTests.cs`, so that the task that adds the
taper is also the task that proves it. The unit-level state-machine, duty-cycle
and leader-scan properties are likewise not in this file; T9 owns them in
`tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs`, for the same reason.

- **Files:** `tests/Hukbo.Core.Tests/PersistentContingentTests.cs` (new),
  `src/Hukbo.Core/Simulation/BattleSimulation.cs` and
  `src/Hukbo.Core/Movement/MovementRules.cs` **as remediation authority only**
- **Remediation authority:** this is the first task whose tests exercise T9's and
  T10's behaviour end to end, so it is the first task that can discover a defect
  in them. An earlier revision gave it a test file and nothing else, which meant
  that a defect its own tests found in `BattleSimulation.cs` had no task in the
  plan authorized to fix it — the same shape the plan had already diagnosed and
  repaired for T10 and left standing for T9, whose logic is far larger. T11
  depends on T9 and T10 transitively through T10, so sharing both files is legal
  under the ownership rule at the top of this document. The authority is narrow
  in exactly the way that rule defines: repair a defect these tests uncovered, do
  not add behaviour, do not edit an assertion T9 or T10 wrote, and do not weaken
  a test to get green. A finding that turns out to be a design error rather than
  an implementation error stops this task and is reported.
- **Depends on:** T10
- **Verification:** `./scripts/test.ps1 -Configuration Release` — every new fact
  passes. Each fact must be demonstrated to fail when the rule it covers is
  temporarily disabled; a test that passes with the feature switched off proves
  nothing and does not count.

### T12 — The engineered deadlock tests

The twenty-seed sweep in T11 is a screen, not a proof. It samples twenty
trajectories and shows that none of them stalled; it does not show that the two
failure geometries design section 10.2 identifies are survivable, because a
random seed may simply never produce them. This task constructs both geometries
deliberately.

Write a new test file with **three** engineered scenarios, each running under
`PersistentContingentsV2` and each asserting a terminal outcome — a decisive
`BattleOutcome`, not a forced draw — strictly inside the tick limit, plus a
companion fact for each of the first two that proves the scenario actually
exercised what it exists to exercise.

**`TwoSameFactionContingentsWithOverlappingTrailingSquaresReachATerminalOutcome`.**
This is failure shape 2, and the scenario must construct the **worst** case
deliberately. An arbitrary crossing is not acceptable: two contingents can cross
in ways whose bias squares never come near each other, and a test built that way
would pass without ever loading the mechanism it exists to check. Build it as
follows, and each clause is load-bearing.

- **One distant enemy, shared.** Place the whole opposing faction as a single
  cluster at a point `E`, inside `Scenario.PerceptionRangeRaw`
  (`src/Hukbo.Core/Simulation/Scenario.cs:33`) of both contingents so that every
  member selects a target and advances, and far enough away that
  `nearestEnemySquared` stays above `closeRadiusRaw * closeRadiusRaw` for the
  entire convergence and no casualty is taken. Both `Close` and `Break` are
  therefore silent throughout, which is exactly what makes shape 2 dangerous.
- **The same heading, not opposing headings.** Both contingents must be aimed at
  `E`, so both leaders' directions of travel are broadly the same and each
  contingent's bias square sits behind its own leader on the **same** side. This
  is the property that makes the two trailing regions coincide rather than
  separate, and it is what an arbitrary crossing does not guarantee.
- **Crossing paths.** Offset the two leaders laterally on opposite sides of the
  line to `E` so that advancing toward `E` brings them together rather than
  apart.
- **Squares overlapping from the first tick.** Choose the lateral offset so that
  the two contingents' trail bases start within `aMarginRaw + bMarginRaw` of
  each other on **both** axes, where `marginRaw = jitterRaw + BodyRadiusRaw` for
  each contingent. Compute that from `FormationRules.ComputeContingentJitterRaw`
  and the scenario's body radius rather than guessing a distance.
- **Non-leader members placed in the trailing region.** Put each contingent's
  members behind its own leader relative to `E`, interleaved across the shared
  trailing region, so the pile-up forms where the two squares coincide rather
  than somewhere the gate has no opinion about.
- **No map edge anywhere near.** Size the map so that no contingent's bias
  square can come within one raw unit of any edge at any point in the run. Gate
  5 then provably cannot fire, which is what lets the companion fact below
  attribute any denial to gate 6 alone.

Build the scenario through
`BattleSimulation.CreateForTesting(Scenario, params AgentState[])`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:225-236`), which takes explicit
agents and keeps the positions and `ContingentId` values they carry rather than
running `FormationPlanner`'s deployment. That is the only sanctioned way to
place a warrior at a chosen coordinate; do not attempt to steer the lattice into
the geometry by picking a seed, which would make the test a hostage to any later
tuning change.

None of the other escapes covers this geometry, and the test's own comment must
say so: the leader exemption does not, because a leader can be blocked by
another contingent's mass; `Close` and `Break` do not, because neither an enemy
nor a casualty is present to trigger them; the straggler gate only thins it,
because a member caught in the pile-up is exactly the member that has fallen
behind; and the cohesion duty cycle bounds only how long the aiming lasts, not
whether the movement is granted. The cross-contingent overlap gate of design
section 3.5 is what has to save it, and this is the test that says whether it
does.

**`TheCrossContingentGateFiresInTheConvergingSameFactionScenario`.** Run the
same engineered scenario and assert that on at least one tick there exists a
contingent for which all three of the following hold at once: its living
non-leader spread exceeds `cohesionRadiusRaw * cohesionRadiusRaw`; its
duty-cycle window is open for its slot on that tick, recomputed in the test as
`((tick + slot * CohesionCycleTicks / 16) % CohesionCycleTicks) < CohesionDutyTicks`;
and its recorded `ContingentState`, read from `AgentView`, is nevertheless
`Advance`. Every other route to `Advance` is already excluded: rules 1, 2 and 3
would have recorded `Break` or `Close` rather than `Advance`; rule 4 is excluded
by the window check; rule 5's entry bar is exactly the spread threshold
asserted, and its hysteresis exit bar is lower still, so a contingent that
spread out this far would be in `Hold` under either; and gate 5 is excluded by
the map sizing. Only gate 6 remains. If the assertion never triggers, the
scenario failed to build the worst case and this fact fails — which is the
point. A liveness test that passes because its guard was never needed has tested
nothing, and without this fact the liveness assertion above could not tell the
two situations apart.

**Pair the samples at the right tick boundary.** `ResolveContingentStates` reads
tick-start positions and writes the state during the same tick, so the state
written on tick `T` is a decision about the positions visible *before*
`AdvanceOneTick` ran for `T`, not after. The fact must therefore compare the
spread measured from the snapshot taken before advancing tick `T` against the
`ContingentState` read from the snapshot taken after it, and evaluate the
duty-cycle predicate at `T`. Comparing a spread measured after the movement
commit against a state decided before it would test a different claim and could
pass or fail for the wrong reason.

**`IndependentSameFactionTrafficCrossingAGrantedBiasSquareReachesATerminalOutcome`.**
This is the residual, and it is the only evidence the design has for it. Design
section 3.5 concedes plainly that gate 6 makes a bias square unshared as an *aim
region* and does nothing whatever about bodies standing in it or walking through
it, and that no arithmetic anywhere bounds that traffic. An earlier revision of
the design asserted the traffic "is transient and does not park a second
headcount there"; that assertion is withdrawn, and this scenario replaces it.

The construction must be the worst case, and every clause is load-bearing. An
implementer who builds the easy version — some traffic somewhere near a
contingent that may or may not have been cohering — will produce a fact that
passes without testing anything.

- **Two same-faction contingents on the same heading toward one distant shared
  enemy, one directly behind the other along that heading.** Call the forward one
  **F**, the rear one **R**. Both are faction 0. Place the whole opposing faction
  as a single cluster at a point `E` ahead of both, inside
  `Scenario.PerceptionRangeRaw` (`src/Hukbo.Core/Simulation/Scenario.cs:33`) so
  every member selects a target and advances.
- **F must be granted cohesion, and the geometry must make that provable rather
  than hoped for.** Place F's non-leader members strung out beyond
  `cohesionRadiusRaw` from F's leader, so transition rule 5's entry bar is met
  and the state machine selects `Hold`. Size the map so no contingent's bias
  square can come within one raw unit of any edge at any point in the run, so
  gate 5 provably cannot fire. Give F at least `MinimumCohesiveMembers` living
  members so rule 2 cannot fire, and place `E` far enough away that
  `nearestEnemySquared` stays above `closeRadiusRaw * closeRadiusRaw` and no
  casualty is taken for the whole convergence, so rule 3 cannot fire either.
- **Gate 6 must provably *not* fire on the F–R pair, which is the opposite
  requirement from the converging-squares scenario above.** Place R's leader far
  enough behind F's trail base along the shared heading that
  `|F.trailBaseY - R.trailBaseY| > FMarginRaw + RMarginRaw` on that axis, where
  `marginRaw = jitterRaw + BodyRadiusRaw` for each contingent. Compute both
  jitters from `FormationRules.ComputeContingentJitterRaw` and both trail
  distances from `FormationRules.ComputeContingentTrailRaw`, against the
  scenario's body radius — do not guess a distance and do not pick a seed. That
  separation is what leaves F granted while R's bodies are inside F's square, and
  it is the entire point of the scenario.
- **R's members must be independently pursuing, and the placement mechanism is
  gate 4.** Place every one of R's non-leader members *within* the straggler
  threshold of R's own leader — that is, at
  `16 * memberSquared <= 9 * cohesionRadiusRaw * cohesionRadiusRaw` — so gate 4
  sends each of them to independent pursuit, and so R's own `spreadSquared`
  stays below rule 5's entry bar and R remains in `Advance`. Do **not** try to
  put R into `Break`: `Break` at tick 0 requires fewer than
  `MinimumCohesiveMembers` living members, which is too few to form a stream, and
  the attrition trigger cannot fire before a casualty exists.
- **R's members must be routed through F's square, not merely near it.** Place
  them forward of R's leader, laterally aligned with F's trail base, so the
  straight line from each member to `E` passes through F's bias square. Their
  destinations are computed by the frozen preset's `BuildMovementProposal`
  against `E`, so their path is a straight line and the routing is a placement
  problem rather than a steering problem.

Build it through `BattleSimulation.CreateForTesting(Scenario, params AgentState[])`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:225-236`), which orders the
supplied agents by `EntityId` and constructs the simulation directly without
running `ResolveSpawnPlacement` or `FormationPlanner`, so the positions and
`ContingentId` values the test supplies are the ones the battle starts from.
Choose the `EntityId` values so the intended leader of each contingent is the
lowest living one in it.

The assertion is a terminal outcome — a decisive `BattleOutcome`, not a forced
draw — strictly inside the tick limit.

**`TheCrossingTrafficScenarioReallyGrantsCohesionWhileTheSquareIsOccupied`.** Run
the same engineered scenario and assert that on at least one tick, both of the
following hold at once:

- F's recorded `ContingentState`, read from `AgentView`, is `Hold`; and
- at least **four** of R's living non-leader members lie inside F's bias square,
  that square recomputed in the test from F's leader's tick-start position, F's
  living count, `FormationRules.ComputeContingentJitterRaw` and
  `ComputeContingentTrailRaw`.

A recorded `Hold` is exactly the observable statement "this contingent was
granted cohesion on this tick", and the reasoning is closed rather than
suggestive: rules 1 and 2 would have written `Break`, rule 3 would have written
`Close`, rule 4 would have written `Advance` on a shut window, and the stage
writes `Advance` rather than `Hold` whenever either geometric gate denies. `Hold`
is reachable only when every one of those has been passed. The test's own comment
must set that reasoning out, because it is what makes the fact non-vacuous.

The threshold of four is a **game-design threshold, not a measurement**, chosen
as roughly a quarter of a contingent's living count on this scenario's sizing so
that the foreign headcount is a material fraction of the packing margin rather
than a stray body. It may need adjusting once the scenario is first run, and the
test's comment must say so. What may not change is the shape: the number must be
large enough that the fact fails if the traffic never really entered the square.

**Pair the samples at the same tick boundary the converging-squares companion
fact uses**, and for the same reason: `ResolveContingentStates` reads tick-start
positions and writes the state during the same tick, so the occupancy must be
measured from the snapshot taken before advancing tick `T` and compared against
the `ContingentState` read from the snapshot taken after it.

**This fact has no guard to disable, and that is not an oversight.** It does not
test a mechanism; it measures whether an unbounded residual bites. Do not invent
a switch to satisfy the disable-and-fail demonstration that the other facts in
this file carry — record in the test's own comment that the demonstration does
not apply here, and why. If this scenario fails, the finding is that the fourfold
packing margin does not absorb crossing traffic, which is a design failure and
not an implementation defect: it stops the workstream and is reported against
design section 13's open question 6 rather than repaired locally.

**`AContingentLeaderPinnedInAMapCornerReachesATerminalOutcome`.** Place a
contingent's leader in a map corner with its members behind it, so both axes of
`CollisionGeometry.ClampCenterToBounds`
(`src/Hukbo.Core/Simulation/CollisionGeometry.cs:114`) engage at once. This is
failure shape 3. It exercises T7's map-edge open-ground predicate in situ, and it
exercises the one residual design section 3.5 states honestly rather than
argues away: the give-way aim point is still clamped, and this test is what
proves the clamp does not stall a member against a corner.

Add a sixth fact,
`ACohesionSquareTooLargeForTheMapDegradesToIndependentPursuit`, running a
scenario on a map deliberately too small to hold any contingent's bias square
and asserting the resulting trajectory is identical to the same scenario under
`IndependentPursuitV1` — the total-degradation claim design section 3.5 makes,
asserted rather than assumed.

Every fact **that covers a mechanism** must be shown to fail when that mechanism
is temporarily disabled. A liveness test that passes with the cross-contingent
gate switched off is testing nothing — and for the converging scenario the
demonstration is specific: with gate 6 removed, the terminal-outcome fact must
fail, and with gate 6 removed the gate-fires fact must fail too, because the
contingent that was being labelled `Advance` would then be labelled `Hold`. For
the corner-pin fact the mechanism is gate 5; for the undersized-map fact it is
gate 5 again.

The two crossing-traffic facts are the exception and it is stated rather than
quietly skipped: they cover a residual, not a guard, so there is nothing to
disable. Their comments must record that, and must not carry an invented switch
put there to satisfy the rule.

- **Files:** `tests/Hukbo.Core.Tests/ContingentDeadlockTests.cs` (new),
  `src/Hukbo.Core/Simulation/BattleSimulation.cs`,
  `src/Hukbo.Core/Movement/MovementRules.cs` and
  `src/Hukbo.Core/Simulation/FormationRules.cs` **as remediation authority only**
- **Remediation authority:** the three engineered geometries are the only place
  in the plan where gates 5 and 6 are exercised against the geometry they were
  written for, so this task is where a defect in them is most likely to surface.
  It depends on T7, T9 and T10 transitively through T11, so sharing those three
  production files is legal under the ownership rule at the top of this document,
  and the authority is narrow in the way that rule defines. One boundary is
  specific to this task: a failure of either crossing-traffic fact is a **design**
  failure, not an implementation defect, and remediation authority does not extend
  to it. That case stops the task and is reported against design section 13's
  open question 6.
- **Depends on:** T11. Sequenced rather than parallel because both read the same
  behaviour and T11's helpers, if any, must exist first; their test-file sets are
  nonetheless disjoint.
- **Verification:** `./scripts/test.ps1 -Configuration Release` — all six facts
  pass; each of the four that covers a mechanism is demonstrated to fail with
  that mechanism disabled; and the two crossing-traffic facts carry the recorded
  statement of why the demonstration does not apply to them.

---

## Stage 5 — presentation

### T13 — The agent inspector row

Add one row to `AgentInspectorContent.BuildLowerLines` immediately after the
existing `Intent:` row at
`src/Hukbo.Client/UI/AgentInspectorContent.cs:119`, reading
`Contingent: <n> — <state>` with the states labelled `Advancing`, `Holding`,
`Closing`, `Broken`. The row is omitted entirely when `ContingentState` is
`None`. Raise `MaximumLowerRowCount` from 12 to 13
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:43`) and update its doc comment,
which enumerates the rows by name. The label text makes no cultural claim and
must not imply a historically attested arrangement.

All new logic goes in pure internal static helpers taking values and returning
values, so the tests construct no `GraphicsDevice`, `SpriteBatch` or `ArenaGame`.

- **Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`
- **Depends on:** T4 for `AgentView.ContingentId` and
  `AgentView.ContingentState`, **and T5** for the goldens T4 deliberately
  invalidated. The T5 edge is not stylistic. T4 and T5 are one red-to-green pair
  and no later task may begin from a tree in that intermediate state; this
  task's verification is `./scripts/test.ps1 -Configuration Release`, which runs
  the whole suite, so run between T4 and T5 it would fail on the moved
  `stateHash` goldens in `DeterminismTests` and T1's fixture — files it does not
  own and must not repair. Runs in parallel with T6 through T12 and with T14 —
  but never before T5. Its file set is disjoint from every one of them.
- **Verification:** `./scripts/test.ps1 -Configuration Release` — new
  `AgentInspectorContentTests` facts assert the row's text for each of the four
  non-`None` states, its absence when the state is `None`, its position
  immediately after `Intent:`, and that the total row count never exceeds
  `MaximumLowerRowCount`. No test constructs a graphics device.

### T14 — Per-contingent ground tint

Derive the pawn ground-base tint from the existing `TeamA` and `TeamB` theme
roles by a fixed per-contingent lightness step, contingent 0 being the unmodified
faction colour. No new theme role, no new texture, no content-pipeline addition.
The tint is applied at the existing `DrawGroundBase` call
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:171-184`) and the derivation lives in
a pure helper so it is testable without a graphics device. Under
`ContingentState.None` the tint is the unmodified faction colour, so a run under
the frozen preset looks exactly as it looks today.

- **Files:** `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `src/Hukbo.Client/UI/FactionColorPalette.cs`, `tests/Hukbo.Client.Tests/FactionColorPaletteTests.cs` (new)
- **Depends on:** T4 for `AgentView.ContingentId` and
  `AgentView.ContingentState`, **and T5** for exactly the reason T13 does: this
  task's verification is `./scripts/test.ps1 -Configuration Release`, which runs
  the whole suite, and between T4 and T5 that suite is deliberately red on
  goldens this task does not own. Runs in parallel with T13 — but never before
  T5. Their file sets are disjoint.
  The tests go in a new file named for the type that owns the derivation rather
  than in the existing `PawnAppearanceFactoryTests.cs`, which covers an
  unrelated type and which an earlier revision named by mistake.
- **Verification:** `./scripts/test.ps1 -Configuration Release` — new facts assert
  that contingent 0 returns the unmodified faction colour, that the eight
  contingent tints are pairwise distinct within a faction, that no tint collides
  with the other faction's base colour, that every tint is derived from a theme
  role rather than a literal, and that the derivation is total across all five
  themes.

---

## Stage 6 — integration, measurement and records

### T15 — Flip the default preset and re-record every golden

Change `Scenario.MovementPreset`'s default to
`MovementPresetId.PersistentContingentsV2`, and re-record the seed-1 state hash,
event hash, outcome, survivor counts, measured ticks and allocation figures that
this legitimately moves. `IndependentPursuitV1`'s own pinned pair is **not**
touched; it stays exactly as T5 recorded it. Add a new pinned pair for
`PersistentContingentsV2`, following
`DeterminismTests.PresetV3_SeedOneStateAndEventHashArePinned`
(`tests/Hukbo.Core.Tests/DeterminismTests.cs:165-190`) and invoking
`HeadlessRunner.Run` with `--movement-preset PersistentContingentsV2`.

This task is separable. Design section 13 question 1 asks the user whether the
shipped default should flip at all; if the answer is no, T15 becomes a
new-pinned-pair-only task and everything after it is unaffected.

This task opens with an inventory step, before it edits anything. Run
`./scripts/test.ps1 -Configuration Release` once with the default already
flipped, list every pre-existing test that newly fails, and classify each one as
a re-recording question, a production defect, or a design failure. That
inventory is written into the task's own notes before a single assertion is
touched, so the eventual file list is a checked fact rather than an open-ended
grant. A test that cannot be placed in exactly one of the three categories stops
the task and is reported.

- **Files:** `src/Hukbo.Core/Simulation/Scenario.cs`, `tests/Hukbo.Core.Tests/DeterminismTests.cs`, `tests/Hukbo.Core.Tests/ScenarioTests.cs`, `docs/development/testing.md`,
  `src/Hukbo.Core/Simulation/BattleSimulation.cs` and
  `src/Hukbo.Core/Movement/MovementRules.cs` **as remediation authority only**,
  and — under the narrow re-recording authority defined immediately below — any
  pre-existing test file named by the inventory step
- **Re-recording authority over pre-existing tests:** flipping the default is
  the first time the repository's own long-standing tests run against
  `PersistentContingentsV2`, and there are 119 `AdvanceOneTick()` call sites
  across eleven test files that no task in this plan owns. Two of them are
  already ruled out by inspection — a two-agent scenario is exempt because
  `livingCount < MinimumCohesiveMembers` forces `Break` before any geometric
  gate is consulted, and every last-stand test is exempt because `Regrouping`
  beats contingent cohesion in the conflict order stated at the top of this
  document — but the remaining nine files are not ruled out, and a hand-built
  scenario with three or more same-faction agents at the default
  `ContingentId` can legitimately enter `Hold` and take a cohesion-pulled step
  that it did not take before. This task may therefore re-record a recorded
  value in such a file, under exactly the discipline it already applies to the
  moved goldens: name which value moved and why it moved, never change the
  shape of an assertion, never add or remove one, and never weaken a test to
  get green. The authority covers re-recording only. A failure traceable to a
  production defect routes through the `BattleSimulation.cs` /
  `MovementRules.cs` clause below; a failure traceable to a design error stops
  the task and is reported against the relevant design open question. If a file
  is edited under this authority, it is named in the task's completion notes
  with the reason, so the change is auditable after the fact.
- **Remediation authority:** flipping the default makes
  `PersistentContingentsV2` the behaviour every test in the repository
  exercises, including tests written long before this workstream that no task
  here has ever run against the new preset. This is therefore the task most
  likely to surface a defect in T9's or T10's production code through a test it
  does not own, and without authority over those two files it would be blocked
  rather than able to act. It depends on both transitively through
  T12 → T11 → T10 → T9, so the sharing is legal under the ownership rule at the
  top of this document, and the authority is narrow in the way that rule
  defines. A pre-existing test that fails only because the *behaviour* changed,
  rather than because the behaviour is wrong, is a re-recording question and is
  handled the way this task already handles the moved goldens — never by
  weakening the test.
- **Depends on:** T12, T13, T14. The edge to T12 is load-bearing: flipping the
  default makes `PersistentContingentsV2` the behaviour every other test in the
  repository exercises, so the engineered deadlock proofs must already be
  passing before that happens.
- **Verification:** `./scripts/verify.ps1 -SkipBootstrap` completes with every
  stage passing, and its literal output is pasted into
  `docs/development/testing.md` as the new current baseline, replacing the
  superseded block and marking the old one superseded rather than deleting it.
  `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -MovementPreset IndependentPursuitV1`
  still reproduces T5's recorded `stateHash` and `eventHash` exactly — this is the
  final proof that the frozen preset survived the whole workstream.

### T16 — Performance measurement and the budget verdict

Measure the 200-agent and 500-agent workloads before and after, with the full
environment block `SIMULATION-GAME-STANDARDS.md:242-287` requires: CPU, RAM, OS,
build profile, scenario hash, agent count, tick rate, warm-up ticks, measured
ticks, and p50/p95/p99/max per stage. Record the new stage's inclusive share and
state a plain verdict against the two acceptance figures in design section 8: the
new stage under 5% of tick p95, and total tick p95 regressing by no more than
10%. A failure is reported as a failure; the numbers are not adjusted to fit.

Update `docs/research/TICK-STAGE-PROFILE.md`'s fixed tick-order list from eight
stages to nine and add the new stage to its per-stage share table.

- **Files:** `docs/development/testing.md`, `docs/research/TICK-STAGE-PROFILE.md`
- **Depends on:** T15
- **Verification:** the recorded before/after tables carry real measured figures
  from `./scripts/benchmark.ps1` runs, and the verdict paragraph names both
  acceptance figures and states whether each was met.

### T17 — Standards and skill documentation

Update the fixed tick-stage list at `SIMULATION-GAME-STANDARDS.md:508-521` from
eight stages to nine, describing what `ResolveContingentStates` reads and writes.
Add the new `HKBO_CTG` domain tag to the tag inventory at
`SIMULATION-GAME-STANDARDS.md:786-792`. Re-record the stale seed-1 baseline in
`.claude/skills/hukbo-determinism-change/SKILL.md:67-82`, which still carries
`stateHash 71211929A44A16CA` / `eventHash A2DC3ECA3F7345ED`, and note in it that
`docs/development/testing.md` is the source of truth when the two disagree.

Runs in parallel with T16; their file sets are disjoint.

- **Files:** `SIMULATION-GAME-STANDARDS.md`, `.claude/skills/hukbo-determinism-change/SKILL.md`
- **Depends on:** T15
- **Verification:** `./scripts/test.ps1 -Configuration Release` still passes (no
  test reads these documents, so this confirms only that nothing was broken), and
  a reviewer confirms the tick-stage list in the standards matches
  `src/Hukbo.Core/Simulation/BattleSimulation.cs`'s `AdvanceOneTick` line for
  line.

### T18 — Smoke-checklist rows

Add a new `### Persistent contingent smoke` section to
`docs/development/testing.md`, numbered from 102 — the current highest row is
101, in `### Event feed lifetime smoke (T17)`.

Place it **after the last existing smoke section**, which is that event-feed
section (its rows end at `docs/development/testing.md:2712`), and before the
`## Failure classification` heading. An earlier revision of this task said to
place it after `### Last-stand formation smoke`, which is wrong on the file as
it stands: that section holds rows 76 through 81 and three further smoke
sections follow it, so inserting there would drop rows 102 onward into the
middle of the numbering. The last-stand section is still the one this new
section must be *compared* against, and the scoping note below does that in
prose.

Include the standard evidence-field table with every field `Not recorded`, and a
scoping note distinguishing persistent-contingent cohesion from the
whole-faction last-stand rally so the two are not conflated.

Also amend the two existing sections this change alters, following the amendment
precedent already used at `docs/development/testing.md:2473`:

- **Collision readability smoke** (rows 19, 20, 21, 21a at
  `docs/development/testing.md:2495-2498`) — the movement labels these rows read
  change meaning and frequency under the new preset. Add an amendment note; leave
  the rows at `PENDING`.
- **Starting deployment smoke** (rows 58 through 61 at
  `docs/development/testing.md:2539-2542`) — the section's premise, that the
  grouping is only an opening-frame property, no longer holds. Add an amendment
  note and a new row confirming the groups stay visually distinct as the battle
  progresses.

Proposed new rows, all `Not run` / `PENDING`:

| Row | What it checks |
| --- | --- |
| 102 | Each side stays readable as several distinct groups well past the opening frame, at the default camera fit, rather than merging into one crowd within a few seconds. |
| 103 | A group that has strung out visibly gathers on one of its own warriors, then resumes advancing, rather than gathering indefinitely or never gathering at all. |
| 104 | The gathered shape is ragged. It is not a ring, a line, an arc, a grid, or any shape that looks placed, and no warrior sits at an obviously exact distance from the one it gathered on. |
| 105 | On reaching the enemy, a group visibly stops holding together and its warriors fight as individuals. The transition reads as arriving, not as the group breaking apart. |
| 106 | Warriors ease into contact rather than travelling at full speed and stopping dead against an enemy body. |
| 107 | A warrior standing in front of the warrior its group has gathered on steps aside rather than being walked through or standing there blocking it. |
| 108 | Selecting any warrior shows a `Contingent: <n> — <state>` row in the inspector, and that state changes over the course of the battle rather than reading the same value throughout. |
| 109 | The eight contingent ground tints within one faction are distinguishable from each other at the default camera fit, and no tint is mistakable for the opposing faction's colour, at all five themes. |
| 110 | Running the same seed under `IndependentPursuitV1` looks exactly as the game looks today: no gathering, no per-contingent tint, and no contingent row in the inspector. |
| 111 | A full 200-agent battle reaches a terminal outcome. Neither side stands gathered and unmoving until the tick limit. |
| 112 | A group whose warriors reach a map edge or a corner keeps moving and fighting there rather than piling into the boundary and staying put. This is the visible face of the map-edge open-ground rule in design section 3.5. |
| 113 | Two groups on the same side that walk into each other come apart again and carry on advancing, rather than jamming into one stationary mass. This is the visible face of the cross-contingent rule in design section 3.5. |
| 114 | Groups read as groups for the whole of the advance, not only in the first few seconds after deployment. Watch a full battle at the default camera fit and judge whether gathering behaviour keeps appearing across several different groups as the armies converge, or whether it happens once near the start and then stops. This is the spectator half of the inertness bar in design section 10.3 — the automated half asserts thresholds on how often cohesion is granted, and only a person can say whether the result looks like several groups advancing or like one crowd that briefly twitched. |

**An agent may never flip one of these rows to `PASS`.** Only a person running
`./scripts/run.ps1` on an interactive Windows desktop may, and compilation, unit
tests and a window-opening probe do not qualify. Rows left untouched stay
`PENDING`; a row that cannot be run is reported `BLOCKED` honestly.

- **Files:** `docs/development/testing.md`
- **Depends on:** T16. Sequenced rather than parallel because both own the same
  file.
- **Verification:** the new section exists with all thirteen rows — 102 through
  114 — at `PENDING`, both amendment notes are present, and no existing row's
  status was changed. **An agent may never flip one of these rows to `PASS`**,
  and a new row is created at `PENDING` and left there.

### T19 — Archive move and index update

Move both documents to `docs/archives/2026-07-28/`, dated for the day of
archiving, and add the `Archived: reference only` banner directly under each
title. Update `docs/plans/README.md` to point at the archived paths and record
the workstream's outcome.

While editing `docs/plans/README.md`, correct its stale combat-preset-chain row:
line 33 describes preset V3 as "Design complete, no plan document", but
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs:16,61` registers it and
`src/Hukbo.Core/Combat/CombatIdentity.cs:105-113` documents it. The registry is
the fact.

- **Files:** `docs/plans/README.md`, plus moving `docs/plans/2026-07-28-formation-movement-realism-design.md` and `docs/plans/2026-07-28-formation-movement-realism.md` into `docs/archives/2026-07-28/`
- **Depends on:** T17, T18
- **Verification:** both files exist at their archived paths with the banner, no
  file remains at the old path, and every link in `docs/plans/README.md` resolves.

---

## Dependency summary

Every edge below is declared in the task it points into. Read as: a task may
start only when every task with an arrow into it has actually reported.

Written as an adjacency list rather than as ASCII art, because the graph has
crossing edges and a drawing of it is easier to get wrong than to read.

```
T1  <- (nothing)
T2  <- (nothing)
T3  <- T1
T4  <- T2, T3
T5  <- T4
T6  <- T4, T5
T7  <- T5
T8  <- T2, T5
T9  <- T6, T7, T8
T10 <- T9
T11 <- T10
T12 <- T11
T13 <- T4, T5
T14 <- T4, T5
T15 <- T12, T13, T14
T16 <- T15
T17 <- T15
T18 <- T16
T19 <- T17, T18
```

The critical path is
`T1 -> T3 -> T4 -> T5 -> T7 -> T9 -> T10 -> T11 -> T12 -> T15 -> T16 -> T18 -> T19`.

Edge notes, because several of these are easy to get wrong:

- **Every task that is not an ancestor of T4 and whose verification runs the
  full suite depends on T5**, whether or not it shares a file with T4. This is
  one rule with **five direct instances — T6, T7, T8, T13 and T14** — and it is
  worth stating once as a rule so a future task does not have to rediscover it.
  An earlier revision of this note counted four and omitted T7, which does carry
  the edge and is declared with it in its own entry. T4
  deliberately moves the state hash and T5 is the task that re-records the
  goldens; the two are one red-to-green pair, and no later task may begin from a
  tree in that intermediate state. A task that runs
  `./scripts/test.ps1 -Configuration Release` in between sees pre-existing
  failures in `DeterminismTests` and T1's fixture, which are files it does not
  own and must not repair.

  Two sub-points, which the earlier revision ran together and which are not the
  same claim. First, file-set disjointness from T4 is **not** sufficient to
  escape the edge: T8, T13 and T14 share no file with T4 and all three need it
  anyway. Second, sharing a file with T4 does not make the edge redundant
  either: T7 shares `src/Hukbo.Core/Simulation/Scenario.cs` and
  `tests/Hukbo.Core.Tests/ScenarioTests.cs` with T4, which already forces
  sequencing after T4, but the edge it declares is to T5 and it is the T5 edge
  that keeps it out of the red window. T7 is therefore an instance of the rule
  and not an exception to it.

  T9 through T12 and T15 through T19 carry no separate T5 edge and correctly so:
  every one of them descends from T5 transitively, so the edge is already
  implied. T1, T2 and T3 also verify against the full suite and carry no T5 edge,
  and correctly so for the opposite reason: all three are ancestors of T4, so
  every one of them has reported before the tree is ever disturbed.
- **T6 depends on T5, not only on T4**, for the additional reason that T6's
  verification reproduces T5's re-recorded `stateHash` and `eventHash`; without
  the edge an orchestrator may run the two in parallel and T6 has nothing to
  check against.
- **T15 depends on T12** as well as on T13 and T14. Flipping the default makes
  the new preset the behaviour every test in the repository exercises, so the
  engineered deadlock proofs must be green first.
- **T8 depends on T2 and T5**, so it can start as soon as T5 reports, but it
  cannot merge into T9 until T6 and T7 have also reported.
- **T9 shares `FormationPlanner.cs` with T3** for a single access modifier, and
  depends on T3 transitively through T6 → T5 → T4 → T3, so the shared-file rule
  at the top of this document is satisfied.

Genuinely parallel pairs: **T1 with T2**; **T6 with T8**; **T13 with T14**, and
both of those with everything from T6 through T12 taken individually — but not
before T5, which every one of T6, T7, T8, T13 and T14 depends on. **T16 with
T17.**
Everything else is sequenced by a dependency or by shared file ownership.

---

## Verification criteria

The workstream is complete when every one of the following holds, with real
pasted output as the evidence for each.

**Determinism and freezing**

1. `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -MovementPreset IndependentPursuitV1`
   reproduces the `stateHash` and `eventHash` T5 recorded, exactly, with
   `deterministic true` and `firstMismatchTick null`, at the final commit.
2. T1's frozen-behaviour trajectory fixture reproduces byte-identically under
   `IndependentPursuitV1` at the final commit, in every column.
3. `MovementPresetId.IndependentPursuitV1 = 1`, its `MovementPresetRegistry.Get`
   arm, its rules class, and its pinned `ContentHash` literal are byte-for-byte
   identical to what T2 and T5 committed. A `git diff` across the whole workstream
   shows no change to any of them after T5.
3a. `MovementRuleset`'s field set is unchanged after T2. No field was added,
   removed, renamed or reordered by any later task. This is what makes criterion
   3 achievable at all: `ContentHash` is computed over those fields, so a field
   added in T9 would move V1's pinned literal and break the freeze.
4. `PersistentContingentsV2` carries its own pinned `ContentHash` literal and its
   own pinned seed-1 `stateHash`/`eventHash` pair, both distinct from V1's.
5. The `SplitMix64` reference vectors in
   `tests/Hukbo.Core.Tests/DeterministicRandomTests.cs:8-28` are unedited.
6. Three storage permutations of one identical roster, advanced in lockstep under
   `PersistentContingentsV2`, produce identical state hashes and identical ordered
   events on every tick.

**Liveness**

7. Twenty seeds at 200 agents under `PersistentContingentsV2` each reach a
   terminal outcome strictly inside the tick limit. No forced draw.
8. No contingent holds `ContingentState.Hold` for more than `CohesionDutyTicks`
   consecutive ticks in any of those twenty runs, **and** no agent receives a
   cohesion destination on more than `CohesionDutyTicks` consecutive ticks in
   any state, `Advance` included.
9. The packing-margin and trail-clearance inequalities hold numerically across
   their full stated ranges; the map-edge predicate's boundary is
   asserted at exact equality and one raw unit beyond, on each of its four
   comparisons independently; and the cross-contingent predicate's boundary is
   asserted at exact edge contact and one raw unit farther apart, on each axis
   independently, together with the fact that separation on either axis alone
   defeats overlap.
9a. The three deliberately engineered geometries each reach a terminal outcome
   strictly inside the tick limit: two same-faction contingents on the same
   heading whose trailing bias squares overlap from the first tick; a granted
   bias square with a stream of independently-pursuing same-faction members from
   a different contingent routed through it; and a contingent leader pinned in a
   map corner. The first and third are demonstrated to fail with their mechanism
   disabled. The second is not, because it covers a residual rather than a guard,
   and its test records that. A passing twenty-seed sweep does not substitute for
   any of the three.
9b. On a map too small to hold any contingent's bias square, the new preset
   produces a trajectory identical to `IndependentPursuitV1`.
9c. In `Advance`, a member at exactly
   `16 * memberSquared == 9 * cohesionRadiusRaw * cohesionRadiusRaw` takes the
   independent-pursuit branch, and a member one raw unit farther out does not.
9d. The cross-contingent overlap gate is demonstrated to have **fired** during
   the converging-contingents scenario, on a map sized so the map-edge gate
   provably cannot fire, so the liveness result in 9a cannot have been obtained
   by a run in which the guard was never needed.
9e. The cross-contingent predicate returns the identical answer with its two
   contingents' arguments exchanged, so both contingents of an overlapping pair
   yield and no ordering rule decides the outcome.
9f. **The inertness bar passes on every seed and for every faction of the
   twenty-seed 200-agent sweep**, at all three thresholds: at least half the
   faction's contingents cohere on at least one tick; at least ten percent of the
   faction's pre-`Close` contingent-ticks are cohering ticks; and at least one
   cohering tick falls in the later half of the faction's pre-`Close` window.
   Every guard in this design denies cohesion rather than adjusting it, so an
   inert build is silent by construction and this is the only assertion in the
   suite that can catch it. The thresholds are game-design choices rather than
   measurements and the test says so; a threshold lowered to match an observed
   figure, without an established cause for the figure, does not satisfy this
   criterion.
9g. **The crossing-traffic scenario is shown to have really granted cohesion
   while foreign bodies were inside the square**: on at least one tick, the
   cohering contingent's recorded `ContingentState` is `Hold` and at least four
   of the other contingent's living non-leader members lie inside its bias
   square. Without this, the liveness result in 9a could have been obtained by a
   run in which the square was never granted or was never occupied.
9h. **Chain denial is shown to arise from genuine pairwise overlap**: in a
   constructed three-contingent arrangement where A overlaps B, B overlaps C and
   A is disjoint from C, `FormationRules.DoCohesionSquaresOverlap` returns
   `false` for the A–C pair and all three contingents are nevertheless denied.
9i. **The state machine's rule priority and the movement gates' conjunction are
   asserted at unit level, not only observed through a battle**: every pair of
   transition rules that can compete is pinned against
   `MovementRules.ResolveContingentState` with hand-built arguments, and
   `MovementRules.IsCohesionEligible` is pinned by an exhaustive truth table
   showing it is the conjunction of all six gates rather than a priority order.
   Leader selection, permutation invariance and death promotion are pinned the
   same way against `MovementRules.ScanContingentLeadersAndLivingCounts`.

**Boundaries and hygiene**

10. `Hukbo.Core` still references neither `Hukbo.Diagnostics` nor MonoGame, proven
    by `DiagnosticLoggingBoundaryTests.CoreDoesNotReferenceTheDiagnosticsAssembly`
    and `SourceHygieneTests.TheCoreProjectDoesNotImportTheDiagnosticsNamespace`.
11. Only the two `Program.cs` entry points touch the console, proven by
    `SourceHygieneTests.OnlyTheEntryPointsWriteDirectlyToTheConsole`.
12. `DiagnosticLoggingBoundaryTests.FullTraceLoggingDoesNotChangeTheSimulationResult`
    passes unmodified.
13. No new `AgentIntent` value, no new `BattleEventKind`, no new theme role, no
    new texture, no new content-pipeline asset.
14. No file under `src/Hukbo.Core/Simulation/CollisionResolver.cs`,
    `CollisionGeometry.cs`, `CollisionUniformGrid.cs` or `CollisionPriority.cs` was
    modified.
15. No client test constructs `ArenaGame`, a graphics device, a sprite batch or a
    window.

**Performance**

16. `BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations` passes at
    its existing 16,384-byte ceiling and 4,096-byte warm-window growth tolerance,
    unmodified.
17. The new stage's p95 inclusive share of `AdvanceOneTick` is at most 5% on the
    200-agent workload.
18. Total tick p95 regressed by at most 10% on the same workload, with the
    before/after tables and the full environment block recorded.
19. A 500-agent stress result is reported.

**Historical accuracy**

20. Every new tuning constant carries a Provisional-reconstruction statement in
    its own XML doc comment.
21. No player-facing string, code identifier, comment or document introduced by
    this workstream names a rank, a file, a fixed frontage, a shield wall, or any
    other formation the research lists as not attested.
22. No Filipino-language term is used for a contingent, a leader, or any unit
    state.
23. `FormationPlanner`'s type-level remarks no longer state that contingents
    dissolve at tick 1.

**Documentation**

24. `SIMULATION-GAME-STANDARDS.md` and `docs/research/TICK-STAGE-PROFILE.md` both
    list nine tick stages, matching `AdvanceOneTick` line for line.
25. `docs/development/testing.md` carries the new baseline, the before/after
    performance tables, the new smoke section, and both amendment notes.
26. Every new smoke row is `PENDING` or honestly `BLOCKED`. None was flipped to
    `PASS` by an agent.

**The gate**

27. `./scripts/verify.ps1` runs **once, after integration**, and its literal
    pasted output is the evidence. It runs prerequisites and locked restore,
    format verification, Release build, Core plus GPU-independent Client tests,
    then the 200-agent / 10,000-tick / seed-1 headless determinism workload, with
    every stage passing and the headless run exiting 0.

**The canonical gate is not delegated.** No sub-agent's report substitutes for
`./scripts/verify.ps1`'s own output, and no agent may flip a manual
smoke-checklist row. A change is never described as verified without the real
output of the command that verified it.
