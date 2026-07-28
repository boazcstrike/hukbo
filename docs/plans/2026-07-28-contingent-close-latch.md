# The contingent `Close` latch — ordered task list

Date: 2026-07-28
Design: [2026-07-28-contingent-close-latch-design.md](2026-07-28-contingent-close-latch-design.md)

Eleven tasks in five stages. The design establishes that smoke rows 104 and 114
are one defect — transition rule 3 puts a whole contingent into
`ContingentState.Close` when a single member reaches contact, and the state
never lifts — and that the fix ships as a new movement preset.

The "before" measurement this work is judged against is already recorded, under
"Measurement behind rows 104 and 114" in
[`docs/development/testing.md`](../development/testing.md). Do not regenerate
it; T7 compares against it.

## How this list is ordered, and why

Three constraints set the order.

**Freeze before you change.** `PersistentContingentsV2` is the current default
and has no digest fixture of its own — `IndependentPursuitV1` has one, V2 does
not. The moment the rule body changes, V2's behaviour becomes unreproducible.
T1 therefore captures V2's digest from the tree exactly as it stands at commit
`8f4e426`, before anything else lands, and it is the one task that must not be
run out of order.

**Move the fields before you move the behaviour.** Adding two fields to
`MovementRuleset` moves two pinned `ContentHash` literals but must move no
simulated behaviour at all. Doing that as its own task (T2) makes the claim
falsifiable: both digest fixtures replay byte-identically across it. If that
task moves a digest, something is wrong with the field addition itself and not
with the rule change that follows.

**Change the rule while it is still inert.** T3 and T4 rewrite rule 3 to count
members in contact rather than take a minimum distance, but every registered
preset still carries the fraction that reproduces the old rule exactly, so the
digests must still replay. The behaviour moves in exactly one task, T6, when
the default flips to a preset that carries a different fraction. One deliberate
hash move, in one place, with the old and new values written down.

## The threshold arithmetic, once

Every task below refers to this. Rule 3 becomes, with `contactCount` the number
of living members whose selected target lies within `closeRadiusRaw`:

```
entryThreshold = Max(1, CeilDiv(livingCount * numerator, denominator))
exitThreshold  = Max(1, CeilDiv(livingCount * numerator, 2 * denominator))

close = previousState == Close
    ? contactCount >= exitThreshold
    : contactCount >= entryThreshold
```

`CeilDiv(a, b)` is `(a + b - 1) / b` in `long`, exact for non-negative operands,
with no division of a signed negative and no floating point anywhere.

The `Max(1, ...)` floor is what makes the field addition inert. At
`numerator = 0`, both thresholds collapse to 1, and "one or more members in
contact" is precisely today's rule — a minimum distance at or under the close
radius means at least one member is inside it, and no member inside it means the
minimum is above it. `IndependentPursuitV1` and `PersistentContingentsV2`
therefore register `(0, 1)` and behave exactly as they do now.
`PersistentContingentsV3` registers `(1, 2)`: close at half the living members
in contact, re-open below a quarter.

Both values are **game-design choices, not historical measurements**, and carry
that label in their XML doc comments the way every constant in
`FormationRules` and `MovementRuleset` already does.

## Stage 1 — freeze what exists

### T1 — Capture the `PersistentContingentsV2` trajectory fixture

Capture a per-tick digest of the current, completely unmodified simulation
running `PersistentContingentsV2` at seed 1, 200 agents, and add the fact that
replays it. Follow the schema and test shape of the existing V1 fixture and its
reproduction fact exactly
(`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1-digest.json`;
`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs`), including the
`contingentId` and `contingentState` per-agent columns, which this capture
writes with real values rather than the zeros the V1 capture writes.

No production file is touched by this task.

**This task must land before T2, T3, or T4 changes a single line under `src/`.**
Its whole purpose is to be a photograph of the tree at commit `8f4e426`.

- **Files:** `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v2-digest.json` (new), `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` (edit)
- **Depends on:** nothing, and nothing may precede it
- **Verification:** `./scripts/test.ps1 -Configuration Release` — the new fact
  `PersistentContingentsV2_ReproducesTheFrozenTrajectoryDigest` passes against
  the unmodified `src/` tree, asserting every tick row and every final agent
  row. Running it twice in the same session must pass twice. The captured
  terminal values must equal the ones commit `8f4e426` recorded:
  `measuredTicks` 1064, `Faction0Victory`, `eventHash 8E819FF7B378FEFD`,
  `stateHash C79B76AE81C300CB`.

## Stage 2 — the field addition, behaviour-inert

### T2 — Two fraction fields on `MovementRuleset`, and the corrected freeze comments

Add `CloseFractionNumerator` and `CloseFractionDenominator` to
`MovementRuleset`, folded into `ComputeContentHash` in declaration order after
`CloseRadiusMultiplier`. Register both existing presets with `(0, 1)`. Nothing
reads either field yet.

Re-pin the two `ContentHash` literals in `MovementPresetRegistryTests`
(`IndependentPursuitV1ContentHash`, `PersistentContingentsV2ContentHash`).
**Recompute them from the built code — do not calculate them by hand and do not
guess.** Add a temporary fact that prints the two values, read them from the
test output, write them into the literals, then delete the temporary fact.

Correct the three comments that state the constant set is permanently closed:
`MovementRuleset`'s type-level remarks, the comment on
`MovementPresetRegistry.IndependentPursuitV1Ruleset`, and the paragraph in
`.claude/skills/hukbo-determinism-change/SKILL.md` if it repeats the claim. The
corrected wording states what is actually frozen — each preset's *simulated
behaviour*, proved by its digest fixture — and records why the `ContentHash`
literals are not behavioural goldens: `MovementRuleset.ContentHash` never
reaches the state hash, because `BattleSimulation.ComputeStateHash` folds
`_rules.ContentHash` where `_rules` is the `CombatRuleset`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:18-19,393`), and
`StateHasher.Compute` never receives a `MovementRuleset` at all.

- **Files:** `src/Hukbo.Core/Movement/MovementRuleset.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs`, `.claude/skills/hukbo-determinism-change/SKILL.md`
- **Depends on:** T1
- **Verification:** `./scripts/test.ps1 -Configuration Release` — both digest
  facts (`IndependentPursuitV1_...` and the one T1 added) pass **unchanged**,
  which is this task's real claim: two fields were added and no behaviour moved.
  Both `ContentHash` facts pass against their new literals, and the fact
  asserting the two literals differ from each other still passes.

## Stage 3 — the rule, still inert

### T3 — Rule 3 counts members instead of taking a minimum

Change `MovementRules.ResolveContingentState` to take `contactCount` and the
two fraction values in place of `nearestEnemySquared` and `closeRadiusRaw`, and
implement the threshold arithmetic given above. Rule 3 keeps its position in the
priority order: after rule 1 (`Break` is terminal) and rule 2 (attrition), and
before rule 4 (a shut window or a denied gate forces `Advance`).

Document the exit band in the method's remarks the way rule 5's three-quarter
band is documented, and say plainly that halving the entry fraction is a chosen
value rather than a derived one — design section 7 leaves it open, and T7
measures the state-flip frequency that will settle it.

Add the unit facts. At minimum: one member in contact out of forty living does
not close under `(1, 2)` but does under `(0, 1)`; exactly the entry threshold
closes; a contingent already `Close` and above the exit threshold stays
`Close`; a contingent already `Close` and below the exit threshold leaves
`Close`; a `livingCount` of zero still yields `None` ahead of every rule; a
previous state of `Break` still stays `Break`; and attrition still outranks
contact. Each must fail against the rule body as it stands before this task.

- **Files:** `src/Hukbo.Core/Movement/MovementRules.cs`, `tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs`
- **Depends on:** T2
- **Verification:** `./scripts/test.ps1 -Configuration Release` — the new facts
  pass; the existing `ContingentStateMachineTests` facts pass with their
  arguments adapted to the new signature and **no expected value changed**; both
  digest facts still pass, because every registered preset still carries
  `(0, 1)`.

### T4 — `BattleSimulation` accumulates the contact count

Replace `_contingentNearestEnemySquared` with `_contingentContactCounts` — an
`int[ContingentSlotCount]` preallocated at construction alongside its
neighbours, cleared at the top of `ResolveContingentStates`, and accumulated in
the pass over living agents that computes it today
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:871-905`). A member counts when
it has a selected target that resolves to a living agent and its squared
distance to that target is at or under `closeRadiusSquared`, which is the same
predicate the minimum encoded, evaluated per member instead of folded.

Remove `_contingentNearestEnemySquared` rather than leaving it unread. Design
section 7 records the argument for keeping it against a future approach-distance
rule; an unread field is the deferred abstraction the standards' reviewer
checklist rejects, and reinstating it later is a two-line change.

The stage stays a single `O(N)` pass. No allocation is added on a warm tick.

- **Files:** `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- **Depends on:** T3
- **Verification:** `./scripts/test.ps1 -Configuration Release` — both digest
  facts still pass byte-identically. This is the last task at which that is
  true, and it is the strongest evidence available that the rewrite of rule 3
  is faithful to the rule it replaced.

## Stage 4 — the preset and the one deliberate hash move

### T5 — Register `PersistentContingentsV3`

Append `PersistentContingentsV3 = 3` to `MovementPresetId`, renumbering
nothing. Add the registry arm carrying every tunable at its
`PersistentContingentsV2` value except `CloseFractionNumerator = 1` and
`CloseFractionDenominator = 2`. Add the `IsRegistered` arm. Pin its
`ContentHash` to a literal recomputed from the build, by the same
print-then-write procedure T2 uses.

The default is **not** changed by this task. V3 is reachable only through
`--movement-preset` until T6.

- **Files:** `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs`
- **Depends on:** T2 (the fields must exist); may run in parallel with T3 and T4, whose file sets are disjoint from this one except for `MovementPresetRegistry.cs` — if T3 and T5 are given to different agents, T5 waits
- **Verification:** `./scripts/test.ps1 -Configuration Release` — `IsRegistered`
  and `Get` facts cover the new value; its pinned `ContentHash` differs from
  both existing literals; both digest facts still pass, because the default is
  still V2.

### T6 — Flip the default to V3 and re-record every moved golden

Change `Scenario`'s default `MovementPreset` to `PersistentContingentsV3`. Every
golden keyed to the default moves in this task and only in this task.

Re-record, and write the before and after values side by side in the commit
message and in `docs/development/testing.md`: the canonical gate's seed-1
200-agent 10 000-tick result (`measuredTicks`, outcome, `eventHash`,
`stateHash`), and any assertion in `DeterminismTests`, `HeadlessRunnerTests`, or
`ScenarioTests` that names a default-preset value. The V2 values to move from
are the ones T1 froze: 1064 ticks, `Faction0Victory`, `eventHash
8E819FF7B378FEFD`, `stateHash C79B76AE81C300CB`.

Both frozen presets keep their fixtures and both facts must still pass. That is
the point of the two captures: the default moved, and nothing else did.

- **Files:** `src/Hukbo.Core/Simulation/Scenario.cs`, `tests/Hukbo.Core.Tests/DeterminismTests.cs`, `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs`, `tests/Hukbo.Core.Tests/ScenarioTests.cs`
- **Depends on:** T4 and T5
- **Verification:** `./scripts/verify.ps1` — the full canonical gate, with its
  real output pasted. `deterministic` must be `true` and the run must report a
  terminal outcome rather than reaching the tick limit. Both digest facts pass.

## Stage 5 — measurement, records, and the human pass

### T7 — Re-measure with `Hukbo.Tools.ContingentShape`

Re-run the harness at the same workload the before-table used, under V3, and
under V2 as the control:

```powershell
dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Release
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5 PersistentContingentsV2
```

Record the before and after tables together in `docs/development/testing.md`,
under the existing measurement section. Report four numbers explicitly, and
report them honestly whichever way they came out:

1. Hold episodes after first `Close` — **must be non-zero**, or the change
   failed at its stated purpose.
2. The `Hold` aspect-ratio distribution — median, p99, and maximum — against
   today's 1.56 / 3.06 / 5.17. A materially worse distribution means mid-battle
   gathers are a different shape from approach gathers, which is new
   information and is a finding, not a thing to quietly tune away.
3. The full denial attribution. Design section 5 predicts the gates and rule 2
   may become the new ceiling; this table says whether they did.
4. The `Close` state-flip frequency, which settles the open question about the
   width of the exit band.

If the harness needs a new counter for item 4, add it to the tool rather than
eyeballing the CSV.

- **Files:** `docs/development/testing.md`, `tools/Hukbo.Tools.ContingentShape/Program.cs` (only if item 4 needs a counter), `tools/README.md` (only if the tool's arguments change)
- **Depends on:** T6
- **Verification:** both tool runs complete and their output is pasted into the
  task's report. If item 1 comes back zero, stop and report it — the remaining
  tasks are not worth running against a change that did not work.

### T8 — Performance and allocation

Run `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` and the
500-agent workload the standards' section 10 requires, under V3 and under V2,
and record the tick-rate and percentile comparison. Update
`docs/research/TICK-STAGE-PROFILE.md` with the `ResolveContingentStates` figures
if they moved.

The change is `O(N)` for `O(N)` with one fewer `long` array, so the expectation
is no measurable movement. **Record what was measured, not what was expected.**

- **Files:** `docs/research/TICK-STAGE-PROFILE.md`
- **Depends on:** T6
- **Verification:** benchmark output pasted, identifying hardware, workload,
  tick rate, and percentiles, per the standards' performance checklist.

### T9 — Documentation and skill updates

Update `.claude/skills/hukbo-determinism-change/SKILL.md` with the new recorded
seed-1 baseline and the third preset. Update `SIMULATION-GAME-STANDARDS.md` only
if the movement-preset section names a preset count or a default. Update
`docs/plans/README.md` to describe this workstream. `CLAUDE.md` and `AGENTS.md`
need no change unless T2's comment correction contradicts something they say —
check both, and keep them consistent with each other if either moves.

- **Files:** `.claude/skills/hukbo-determinism-change/SKILL.md`, `docs/plans/README.md`, `SIMULATION-GAME-STANDARDS.md` (conditional), `CLAUDE.md` and `AGENTS.md` (conditional)
- **Depends on:** T6, T7, T8
- **Verification:** every recorded hash, tick count, and preset name in the
  edited files matches the values T6 and T7 actually produced. Read them back
  against the gate output rather than against this plan.

### T10 — Reset smoke rows 104 and 114 for re-observation

Set rows 104 and 114 back to `PENDING` with their "What the human reported"
cells cleared, and add a line under the table recording that they failed at
commit `8f4e426`, that the cause was transition rule 3, and that they are
awaiting re-observation under `PersistentContingentsV3`. Leave rows 106, 107,
108, 109, 110, 112, and 113 at `PENDING` — this change does not touch what they
test and no agent may run them.

**No agent may flip 104 or 114 to `PASS`.** Only a human at an interactive
desktop, watching a real battle, can do that. If the rows are still failing
after this work, that is the honest outcome and it is recorded as such.

- **Files:** `docs/development/testing.md`
- **Depends on:** T6
- **Verification:** the two rows read `PENDING`, the historical note names the
  commit and the cause, and no other row's status changed. Diff the table and
  confirm exactly two status cells moved.

### T11 — Archive and index

Move this plan and its design document to `docs/archives/2026-07-28/`, dated for
the day of archiving rather than the day of writing, each with the "Archived:
reference only" banner under its title. Update `docs/plans/README.md` and
`docs/archives/README.md`.

Do this only once T10's human pass has actually happened. A plan archived with
its acceptance criteria unmet is a plan that will be cited later as though it
were finished.

- **Files:** `docs/archives/2026-07-28/` (moved files), `docs/plans/README.md`, `docs/archives/README.md`
- **Depends on:** every task above, including the human observation in T10
- **Verification:** both moved files carry the banner, no link in the repository
  points at the old paths, and `docs/plans/` no longer lists this workstream as
  active.

## Dependency summary

```
T1  (freeze V2)              -> T2
T2  (ruleset fields)         -> T3, T5
T3  (rule 3 rewrite)         -> T4
T4  (contact accumulation)   -> T6
T5  (register V3)            -> T6
T6  (flip default)           -> T7, T8, T10
T7  (re-measure)             -> T9
T8  (performance)            -> T9
T9  (docs)                   -> T11
T10 (smoke rows, human)      -> T11
```

T3 and T5 both touch `MovementPresetRegistry.cs`, so they do not go to two
agents in parallel. T7 and T8 are genuinely independent and may run at the same
time. Everything else is a chain.

## Verification criteria

The workstream is done when all of the following are true, each with real
output recorded and none of them asserted from a subagent's summary:

1. `./scripts/verify.ps1` passes, output pasted — prerequisites and locked
   restore, format verification, Release build, Core and GPU-independent Client
   tests, and the seed-1 200-agent 10 000-tick headless determinism workload
   reporting `deterministic: true` and a terminal outcome.
2. `IndependentPursuitV1` replays
   `seed-1-200-agents-movement-v1-digest.json` byte-identically.
3. `PersistentContingentsV2` replays the fixture T1 captured byte-identically,
   including terminal tick 1064, `Faction0Victory`, `eventHash
   8E819FF7B378FEFD`, and `stateHash C79B76AE81C300CB`.
4. The new seed-1 V3 goldens are recorded, with the V2 values they replaced
   written beside them.
5. `Hukbo.Tools.ContingentShape` reports a non-zero count of Hold episodes after
   first `Close`, and the before and after tables sit together in
   `docs/development/testing.md`.
6. The `Hold` aspect-ratio distribution is recorded against the 1.56 / 3.06 /
   5.17 baseline, whichever direction it moved.
7. Benchmark output for 200 and 500 agents is recorded, identifying hardware
   and percentiles.
8. A human has re-observed smoke rows 104 and 114 and recorded the result.
   `BLOCKED` is an acceptable outcome to report. A row flipped by an agent is
   not.
