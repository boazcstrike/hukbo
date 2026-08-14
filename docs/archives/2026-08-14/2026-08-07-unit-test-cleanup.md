# Unit test cleanup — what can be removed, and what must not be

**Archived: reference only.** Every task this plan carried is executed: T1
through T5 on 2026-08-07, and T6 and T7 on 2026-08-14 with the canonical gate
green on the branch rebased onto `bb7d229`. Never execute it, never treat it as
a live task list, and never cite it as the reason to make a change. The live
contract for this project remains `CLAUDE.md` and
`docs/development/testing.md`; nothing in this file overrides either of those.
Archived 2026-08-14. One finding it opened outlived it and was carried forward
to `docs/plans/TODO.md` rather than left here: `MotionIntensityManager`,
`GoreIntensityManager`, and `AutoCameraModeManager` are still three
independently copied classes with no shared type behind them, so their twenty
test methods stay where they are.

Status: **T1 through T7 all executed.** The outcome, and the corrections to the
estimates made before the work started, are recorded in section 11; section 12
records T6 and T7.

## 1. Baseline

Measured on `main` at `ae7bf04` with a full Release run
(`dotnet test Hukbo.slnx -c Release`, trx in `artifacts/testresults/cleanup.trx`):

| Metric | Core | Client | Total |
| --- | --- | --- | --- |
| Test cases executed | 2,614 | 3,121 | 5,735 |
| Wall-clock duration | 30 s | 2 s | — |
| Summed per-test duration | 164.7 s | not captured | — |
| `[Fact]` / `[Theory]` methods | 1,161 | 1,672 | 2,833 |
| `[InlineData]` rows | — | — | 1,956 |
| Test source files (excluding `obj/`) | — | — | 185 |
| Test source lines | — | — | 82,367 |

Two facts shape everything below.

The first is that the Client suite is effectively free. Three thousand one
hundred and twenty-one cases finish in two seconds because the Client tests are
pure helpers with no graphics device, exactly as `hukbo-client-ui` requires.
Nothing in the Client suite should be removed to buy runtime, because there is
no runtime to buy. Client removals are justified only by maintenance cost.

The second is that Core runtime is concentrated. Seven classes account for
roughly 136 of the 164.7 summed seconds, and most of that concentration is
legitimate — whole-battle determinism and freeze work that has to run long
battles. The removable runtime is not in the expensive classes. It is in the
movement scenario suites, which are individually fast but collectively execute
the same simulation cell three and four times over.

## 2. Criteria

A test is a removal candidate when at least one of the following holds, and
when nothing in section 6 protects it.

1. **Executed redundancy.** Another test already runs the same inputs through
   the same code path and asserts the same property. Removal changes what runs,
   not what is covered.
2. **Structural duplication.** The same invariant is hand-written once per
   member of a table that the test could iterate instead. Removal is a
   consolidation into a `[Theory]`, and the assertion count stays the same or
   grows.
3. **Dead by construction.** The code has no `[Fact]` or `[Theory]` in the gate
   build, or it exercises a plan that has been archived and closed.
4. **Duplicated because the source is duplicated.** The test file mirrors a
   copy-pasted production type. These are blocked: removing the test before
   consolidating the source deletes real coverage.

## 3. Bucket A — the movement scenario matrix runs three times over

This is the largest single item, in both test count and runtime.

`tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs` defines six
canonical loadouts and enumerates, from them, 21 unordered one-versus-one cells
and 231 team-versus-team matchups. Five weapon suites each take the slice of
that matrix containing their own weapon and run a determinism-and-boundedness
contract over it:

| Suite | Cases | Lines | Duration | Claimed slice |
| --- | --- | --- | --- | --- |
| `TallHardwoodMovementScenarioTests` | 285 | 2,668 | 8.23 s | 11 of 21 one-versus-one, 176 of 231 team |
| `KalisMovementScenarioTests` | 220 | 817 | 6.75 s | 176 of 231 team |
| `ItakMovementScenarioTests` | 212 | 1,087 | 2.54 s | 11 of 21 one-versus-one, 176 of 231 team |
| `WasayMovementTests` (matrix portion) | 120 total | 3,745 | 3.47 s | its own duel and group cells |
| `KampilanMovementTests` (matrix portion) | 65 total | 1,280 | 2.29 s | its own duel and group cells |

Four suites each claim 176 of the 231 team matchups. That is 704 team-cell
executions covering at most 231 distinct cells. The one-versus-one slices
overlap the same way: three suites claiming 11 of 21 cells each.

The assertion bodies are three independent implementations of one idea. Read
them side by side:

- `ItakMovementScenarioTests.EveryItakTeamMatchupCellHoldsTheMovementContract`
  builds the cell twice from identical inputs, asserts the two state hashes
  agree, then runs a shared movement contract to a tick bound.
- `KalisMovementScenarioTests.EveryKalisRelevantTeamCellRunsDeterministically`
  builds the roster forward and reversed, runs both for 150 ticks under seeds
  1 and 2, and asserts equal state hashes plus pace ceilings each tick.
- `TallHardwoodMovementScenarioTests.EveryShieldTeamMatchupCellReplaysIdenticallyAtSeedOne`
  runs the cell to completion twice and asserts a shared run contract.

Each checks something the others do not — Itak checks construction determinism,
Kalis checks caller-order independence across two seeds, TallHardwood checks
full-run replay equality. The union is what the matrix deserves. The current
arrangement gives every cell whichever one-third of the union its owning weapon
happened to implement, and pays for the other two-thirds in duplicate execution.

**Proposal.** One `MovementMatrixContractTests` class that enumerates all 21
one-versus-one cells and all 231 team matchups exactly once, applying the union
of the three contracts to every cell. Delete the sliced equivalents from the
five weapon suites; keep everything in those suites that is genuinely
weapon-specific (the duel narratives, the group congestion fixtures, the
lane-and-clearance interactions).

**Expected effect.** Roughly 470 fewer executed cases; coverage per cell goes
up rather than down, because every cell gets all three contracts instead of
one. Around 12 to 15 seconds off the summed Core duration. Around 2,000 lines
of test source removed.

**Risk.** The three contracts have different tick bounds and seed sets. The
merged contract must take the strictest of each, or the consolidation quietly
weakens the shield cells. This is the task that most needs its diff read
carefully.

## 4. Bucket B — the same profile invariant, written once per weapon

The five weapon profile suites assert the same fifteen-odd properties against
different rows of one table.

| File | Tests |
| --- | --- |
| `TallHardwoodMovementProfileTests` | 22 |
| `KalisMovementProfileTests` | 12 |
| `ItakMovementProfileTests` | 8 |
| `WasayMovementProfileTests` | 8 |
| `KampilanMovementProfileTests` | 4 |
| **Total** | **54** |

The repeated invariants, named as they appear in each file:

- the profile exports its approved loadout key
- every rank resolves the same row instance
- the registered row is the exported row instance
- the direction bands cap pace at the approved ratios
- an ordinary turn budget is two sectors and a committed turn is one
- the entry and release equalities enter and leave disengagement
- a ratio strictly between the two thresholds preserves the prior phase
- zero living enemies never enters and never remains in disengagement
- the ratio cross products survive counts that overflow 32 bits
- the preferred distance carries its own offset per opponent
- no desired pace exceeds the shared human baseline
- N committed ticks are followed by exactly N recovery ticks
- an accepted attack during recovery restarts a fresh commitment
- reversed caller order produces the same state and event hash

**Proposal.** One `MovementProfileRowContractTests` with roughly fifteen
`[Theory]` methods, each parameterized over the six canonical rows. Keep, in the
per-weapon files, only the value pins — the "carries every approved value" and
"is the registered canonical index N instance" tests. Those are the freeze
contract for tuning values and must stay per row and stay explicit.

**Expected effect.** About 54 methods collapse to about 18. Executed case count
stays roughly flat, because a theory over six rows runs six cases. The win is
maintenance: adding a seventh loadout for the ranged-units package currently
means hand-writing fifteen more tests, and after this it means adding one row.

**Dependency.** The ranged-units package kicked off 2026-08-07 and adds four
weapons. This bucket should land **before** those weapons, or it will have to be
done against nine rows instead of six.

## 5. Bucket C — dead by construction

### `tests/Hukbo.Core.Tests/Movement/PressureInterruptCalibrationHarness.cs`

894 lines, zero `[Fact]` and zero `[Theory]` in the gate build. Its own header
says so: the entry point compiles only under the `HUKBO_CALIBRATION`
preprocessor symbol, which no script and no gate stage defines. It reaches a
private `BattleSimulation._pressureInterruptFired` field by reflection. It
implements task E0 of the movement V7 pressure-interrupt plan — an archived
plan, and archived plans are reference only.

V7 closed on 2026-08-06. The measurement this harness exists to produce has been
taken, and its verdict was recorded: no tuning of the pressure interrupt meets
the termination bar, because the cause sits upstream of the interrupt.

**This one needs a decision rather than a default.** The ranged-units package
lists the standoff fix in scope, and this harness is the only instrument in the
repository that measures phase-flip percentage and per-row interrupt firing
counts against a caller-supplied preset. Deleting it and then needing it again
in three weeks is the expensive outcome.

Two defensible answers:

1. **Delete it.** The V7 measurement is done and recorded; rebuild an
   instrument fitted to the ranged work if that work needs one.
2. **Keep it and re-point it.** Move its documentation reference off the
   archived plan and onto whatever live document owns the standoff fix, so the
   next reader is not sent to `docs/archives/`.

Recommendation: option 2 until the ranged-units plan states whether it needs
per-cell phase-flip measurement. It costs nothing at runtime — it does not run.
The only real cost is the reflection dependency on a private field name, and
the harness already throws with that field's name if it is renamed.

### Stale `obj/` artifacts

`tests/*/obj/**/AutonomousArena.*.g.cs` still exist as generated leftovers.
`CLAUDE.md` section 2 is explicit that these are untracked build leftovers to
ignore, not to fix. **No action.** Listed here only so the next reader does not
rediscover them and file them as a finding.

## 6. Bucket D — blocked on a source refactor

`src/Hukbo.Client/UI/MotionIntensitySelector.cs`,
`GoreIntensitySelector.cs`, and `AutoCameraModeSelector.cs` are 271 lines each
and near-identical. `SettingsChoiceSelector<T>` (261 lines) is the generic form
of exactly that behavior and already exists in the same directory.

The test files mirror the duplication precisely:

| File | Tests | Lines |
| --- | --- | --- |
| `MotionIntensitySelectorTests` | 7 | 154 |
| `AutoCameraModeSelectorTests` | 7 | 154 |
| `GoreIntensitySelectorTests` | 7 | 142 |
| `UiThemeSelectorTests` | 5 | 130 |
| `SettingsChoiceSelectorTests` | 4 | 120 |
| `MotionIntensityManagerTests` | 6 | 92 |
| `AutoCameraModeManagerTests` | 6 | 92 |
| `GoreIntensityManagerTests` | 5 | 80 |
| `UiThemeManagerTests` | 3 | 66 |

Seventeen of these test names appear verbatim in three or four files:
`PreviousAndNextWrapAtBothEnds`, `PointerActivationSelectsTargetButHoverDoesNot`,
`SelectChangesTheValueImmediatelyAndPersistsIt`,
`AFailedPersistStillLeavesTheSelectedValueActive`,
`UnrelatedKeysSelectNothingEvenWhenFocused`, and so on.

**These tests must not be removed on their own.** They are the only thing
holding three independently-copied production types to one behavior. Delete them
and the copies drift silently.

**Proposal, in this order.** First make the three concrete selectors delegate to
`SettingsChoiceSelector<T>` — a source change, with its own scope and its own
gate run. Only then collapse the nine test files into
`SettingsChoiceSelectorTests` plus one thin per-type wiring test each
(construction, option names, persistence key). That is roughly 46 methods down
to roughly 16.

Until the source consolidation lands, this bucket is **not a removal
candidate**. It is a source-duplication finding that happens to have surfaced
during a test audit.

## 7. Do not remove

Named explicitly so a later sweep does not have to re-derive the reasoning.

- **The differential oracles.** `NaiveCollisionResolution.cs` (510 lines),
  `NaiveClashResolution.cs`, `NaiveCollisionPairs.cs`,
  `Movement/NaiveConflictPassOracle.cs`, `Movement/NaiveMovementContextQuery.cs`.
  They carry no `[Fact]` of their own, which makes them look like the harness in
  bucket C. They are not. They are the independent implementations the optimized
  code is checked against, and they are referenced by live tests.
- **`MovementPresetFreezeTests`** (22.1 s) and **`DeterminismTests`** (11.6 s).
  These are the frozen trajectory digests and the pinned hashes. Expensive on
  purpose. `hukbo-determinism-change` governs any edit here.
- **`DiagnosticLoggingBoundaryTests`** (15.6 s). Enforces that logging cannot
  change a simulation, which `CLAUDE.md` section 5 requires by name.
- **`SourceHygieneTests`**, **`PresentationNeutralityTests`**,
  **`LogEventCatalogTests`**. Build-enforced repository rules — the console-write
  scan, the Core/Diagnostics assembly boundary, the `LogEvents` catalog.
- **`AppearanceRosterContractTests`** and the per-region preset suites
  (`AppearancePresetsLuzonTests`, `...VisayanTests`, `...TagalogTests`). The
  shared method names across these files look like duplication and are not: each
  pins a different regional block against a different design table, and the
  prohibition tests are the executable form of the historical accuracy policy in
  `CLAUDE.md` section 7. The only real overlap is that `AppearancePresetTests`
  re-pins in aggregate what the three region files pin individually — a handful
  of methods, low value either way. Leave it.
- **`PersistentContingentTests`** (29.9 s), **`PhilippineCombatIntegrationTests`**
  (24.1 s), **`LastStandFormationTests`** (11.4 s), **`BattleSimulationTests`**
  (15.5 s). The most expensive classes in the suite, and the ones that run whole
  battles across seed sweeps. Their cost is their purpose.

## 8. Ordered tasks

Each task is one agent's worth of work, with a non-overlapping file set.

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| T1 | Write `MovementMatrixContractTests` covering all 21 one-versus-one and all 231 team cells with the union of the three existing contracts. Do not delete anything yet. | new `tests/Hukbo.Core.Tests/Movement/MovementMatrixContractTests.cs` | New class passes; total Core case count rises. |
| T2 | Remove the sliced matrix theories from the four weapon scenario suites, keeping every weapon-specific narrative test. | `ItakMovementScenarioTests`, `KalisMovementScenarioTests`, `TallHardwoodMovementScenarioTests`, `WasayMovementTests`, `KampilanMovementTests` | Core case count drops by roughly 470; no class other than these five changes count. |
| T3 | Write `MovementProfileRowContractTests` as theories over the six canonical rows, covering the fourteen shared invariants of section 4. | new `tests/Hukbo.Core.Tests/Movement/MovementProfileRowContractTests.cs` | Passes against all six rows. |
| T4 | Strip the shared invariants from the five per-weapon profile suites, keeping the value pins and canonical-index pins. | the five `*MovementProfileTests.cs` | 54 methods down to the pins only; no assertion from section 4's list is lost. |
| T5 | Decide the `PressureInterruptCalibrationHarness` question with the ranged-units plan owner, then either delete the file or re-point its documentation reference off `docs/archives/`. | `PressureInterruptCalibrationHarness.cs` | Gate case count unchanged either way — it contributes none. |
| T6 | *(separate scope, source change)* Make the three concrete selectors delegate to `SettingsChoiceSelector<T>`. | `src/Hukbo.Client/UI/{MotionIntensity,GoreIntensity,AutoCameraMode}Selector.cs` | All nine existing selector and manager test files still pass **unchanged**. That is the whole point of the task. |
| T7 | *(only after T6)* Collapse the nine selector and manager test files into the generic suite plus per-type wiring tests. | the nine files in section 6 | Client case count drops; behavior coverage per type is still reachable. |

T1 must land before T2. T3 must land before T4. T6 must land before T7. T5 is
independent. T1 through T4 should land before the ranged-units package adds four
more weapons, or both consolidations get more expensive.

## 9. Verification

`./scripts/verify.ps1` after each task, with real output recorded. The specific
things to check beyond green:

1. **Case count moves the way this document predicts.** Record Core and Client
   totals before and after each task. An unexplained drop is a deleted
   assertion, not a deleted duplicate.
2. **The determinism workload is untouched.** Stage five of the gate — 200
   agents, 10,000 ticks, seed 1 — must report the same state hash and event hash
   as it does today. Nothing in this plan touches `src/`, except T6, which is
   flagged as a separate scope for that exact reason.
3. **No smoke checklist row changes.** This is a test-only cleanup. Every row in
   `docs/development/testing.md` keeps whatever status it has.

## 10. What this plan does not claim

The runtime win is real but small: roughly 15 seconds off a 30-second wall
clock, and the gate spends most of its time building, not testing. The honest
justification for buckets A and B is maintenance cost against the four incoming
ranged weapons, not speed. Anyone reading this as a performance plan will be
disappointed by the measurement in section 1.

## 11. Result

### Measured outcome

| Metric | Before | After |
| --- | --- | --- |
| Core test cases | 2,614 | 2,355 |
| Client test cases | 3,121 | 3,121 |
| Core wall-clock duration | 30 s | 22 s |
| Test source lines changed | — | 148 added, 1,806 removed |
| Test files | 185 | 185 (two deleted, two added) |

The canonical gate passes on the branch:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

The 200-agent, 10,000-tick, seed-1 determinism workload reports
`"deterministic": true`, `"firstMismatchTick": null`, state hash
`1B73FC5923879AA0`, event hash `AC55684F24D39344`, outcome `Faction1Victory`.
No file under `src/` was touched, so no state hash could have moved.

### What was done

**T1 and T2 — the matrix.** `MovementMatrixContractTests` now runs all 21
one-versus-one cells and all 231 team matchups exactly once, applying the union
of the three per-weapon contracts: the shield suite's twin-rerun determinism
check over the state hash, ordered event stream, event hash and outcome; its
per-tick step legality, declared-member, timer, and no-progress checks, whose
`RunToCompletion` recorder was carried over as the shared one; and the Kalis
suite's caller-order reversal with its pace-ceiling and distinct-ally-position
assertions. The Itak suite's same-input construction check is subsumed by the
twin rerun. 294 cases, four seconds.

The caller-order check runs over the 21 duels and the 21 team mirrors rather
than all 231 matchups. Caller-order independence is a property of the
simulation's ordering discipline, not of a loadout pairing, and the mirrors
already cover every canonical loadout in the four-agent geometry where the
conflict pass has work to do. Running it over every matchup buys combinations
of a property that does not vary by combination, and it is what would otherwise
have made the merged suite cost *more* than the three slices it replaces.

The sliced equivalents were then removed from `ItakMovementScenarioTests`,
`KalisMovementScenarioTests`, and `TallHardwoodMovementScenarioTests`, along
with each suite's slice-count fact — those existed to justify a slice, and
`MovementScenarioMatrixTests` already pins every count, uniqueness, ordering,
mirror, and shielded-flag invariant of the matrix from the other side. Every
weapon-specific test in all three files was kept: the focused geometries, the
group fixtures, the roster-preservation cases, the curated count boundaries,
and the Kalis liveness duels.

**T3 and T4 — the profile rows.** `MovementProfileRowContractTests`
parameterizes seven invariants over the six canonical rows: export-to-registry
reference identity, the equipment-only loadout key at the default rank, rank
independence across all five ranks, disengagement hysteresis, the committed
pace ceiling, the opponent-offset envelope, and all 36 effective preferred
distances against a hand-computed arithmetic oracle. 96 cases, 27 milliseconds.

Two files were deleted outright — `KampilanMovementProfileTests` and
`ItakMovementProfileTests` — because every assertion in them is now covered by
that suite or by `MovementProfileRegistrationTests`. The Kalis, Wasay, and
tall-hardwood profile suites were trimmed to what only they can say: the Wasay
approved calibration ranges, and the relational assertions comparing each
shielded row against its solo counterpart.

**One judgment call worth flagging.** Three files carried literal value blocks
against the exported statics, duplicating what `MovementProfileRegistrationTests`
pins through the registry, and two of them documented that overlap as
deliberate. The stated reason was that only an export-side pin catches an
export that has stopped being the registered row.
`EveryExportedRowIsTheInstanceTheRegistryComposes` now answers that concern
directly by reference identity, which is strictly stronger: once the two are
proven to be one object, a value pin on either is a value pin on both. The
duplicated blocks were removed on that basis, and the reasoning is recorded in
the doc comments of the files that lost them. A reader who disagrees should
restore the blocks rather than the argument, since the argument no longer
holds.

A third copy of the shielded effective-distance column pins was found in
`TallHardwoodMovementTests` during the sweep — outside the profile files, and
not in the original survey. It was removed too.

**T5 — the calibration harness.** Kept, and re-pointed, as recommended. There
is no live plan document to point at yet — the ranged-units package has not
produced one — so the two `docs/archives/` references were rewritten to read as
provenance rather than as instruction: the harness records which archived plan
commissioned it, states plainly that the plan is closed and its verdict taken,
and says that a later investigation should re-point the comment at its own live
plan. The task-number references in the emitted report text were removed, since
those tasks no longer exist. The file still contributes zero cases to the gate.

### Corrections to the estimates in sections 3 and 4

- Section 3 says four suites each claim 176 of the 231 team matchups. It is
  **three** — Itak, Kalis, and tall-hardwood. The Wasay suite draws group
  fixtures from `EnumerateTeamCompositions`, not from the matchup enumeration,
  and the Kampilan suite does not touch it at all.
- Section 3 predicts "roughly 470 fewer executed cases" from bucket A. The
  actual figure is **262**. The 470 counted whole suites, including the
  weapon-specific tests that were always going to be kept. 556 sliced cases
  were removed and 294 merged cases added.
- Section 3 predicts 12 to 15 seconds off the summed Core duration. The
  observed wall clock fell from 30 s to 22 s, which is consistent, but the
  summed per-test figure was not re-measured.
- Section 4 counts 54 methods across the five profile suites and predicts the
  case count would stay "roughly flat". It did not: T3 and T4 together removed
  a further **93** cases net. The section also missed that
  `MovementProfileRegistrationTests` was already the table test for the row
  literals, which is why two whole files could be deleted rather than trimmed.

## 12. T6 and T7, executed 2026-08-14

Both remaining tasks are done. The bucket D finding this plan opened — three
independently copied selectors and nine test files holding them to one
behaviour — is closed.

### T6, the source consolidation

`MotionIntensitySelector`, `GoreIntensitySelector`, and
`AutoCameraModeSelector` were 271 near-identical lines each. Each now holds one
`SettingsChoiceSelector<T>`, built in its constructor with its own label,
options, names, and the `ACTIVE` marker prefix, and forwards every member to it.

| File | Before | After |
| --- | --- | --- |
| `MotionIntensitySelector.cs` | 271 | 150 |
| `GoreIntensitySelector.cs` | 271 | 150 |
| `AutoCameraModeSelector.cs` | 271 | 147 |

The per-type `Options` and `Names` arrays and the static `GetDisplayName` stay
where they are. They are the per-type configuration, not the duplication this
task existed to remove.

Section 8's acceptance criterion held exactly as written: all nine selector and
manager test files passed unchanged. That is what proved the delegation
preserved behaviour, and it is why the task was ordered before T7 rather than
merged into it.

One behavioural difference had to be reconciled rather than assumed away.
`MotionIntensitySelector` and `GoreIntensitySelector` omitted the generic
method's `Bounds.Contains` guard from `GetPointerSelection`;
`AutoCameraModeSelector` already had it. The two agree, because `PreviousBounds`
and `NextBounds` share the top and height of `Bounds` and are anchored inside
its left and right edges, so a pointer inside either arrow rectangle is already
inside `Bounds`. That reasoning is recorded in a doc comment at each call site
rather than left for the next reader to re-derive.

### T7, the test consolidation

Six methods per file — wrap at both ends, the undefined-value fallback, the
four-key focused-activation theory, unrelated-key rejection, pointer activation
versus hover, and the arrow minimum target size — were the same assertions
written three times against what is now one implementation. They moved to
`SettingsChoiceSelectorTests`, asserted against two differently shaped
instantiations rather than one. Each concrete type kept a single wiring test
pinning its own option names and marker text.

| | Methods | Lines |
| --- | --- | --- |
| Before | 50 | 1,030 |
| After | 35 | 743 |

The Client suite fell from 3,792 cases to 3,771. The arithmetic is exact: the
three per-type theories dropped nine cases each and the shared suite gained six.

Two of the nine files were deliberately not folded, and the reason is this
plan's own criterion 4. `UiThemeSelector` does not delegate to
`SettingsChoiceSelector<T>` at all — it carries its own bounds math, swatch
rendering, and the provisional-reconstruction label — so its five tests still
hold real behaviour that nothing else asserts. Nor does any shared manager type
exist: `MotionIntensityManager`, `GoreIntensityManager`, and
`AutoCameraModeManager` are still three independently copied classes. Folding
their twenty test methods would delete real coverage for a source duplication
nobody has consolidated, which is the exact mistake section 6 warned against.
Those four manager files are a live bucket D finding, not finished work.

### What the gate said, in full

Run on the task branch rebased onto `main` at `bb7d229`, every stage green.

| Stage | Result |
| --- | --- |
| Format verification | `[PASS] Formatting verification completed.` |
| Release solution build | `[PASS] Release solution build completed.` Zero warnings, zero errors |
| `Hukbo.Core.Tests` | `Total tests: 2568  Passed: 2568` |
| `Hukbo.Client.Tests` | `Total tests: 3771  Passed: 3771` |
| Determinism workloads | five `[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.` |
| Verdict | `[PASS] Canonical repository verification completed.` |

All five seed-1 workloads report `deterministic: true`, and every digest matches
its recorded baseline:

| Workload | `stateHash` | `eventHash` |
| --- | --- | --- |
| `combatPreset 6` / `movementPreset 4` | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` |
| `combatPreset 5` / `movementPreset 8` | `C8023D3B5BEB005E` | `F709A345E2F7370E` |
| `combatPreset 5` / `movementPreset 10` | `7C145A9E05916E4C` | `77626E104234206C` |
| `combatPreset 5` / `movementPreset 11` | `6225182B4A470F91` | `C4DABE6AF98B6BEC` |
| `combatPreset 5` / `movementPreset 13` | `4A0723BC9A1B924B` | `E0CE32CF8830A864` |

Nothing under `src/Hukbo.Core` was touched, so no hash could have moved, and
these are the measurements that say so rather than the assertion.

**An earlier run of this same branch was red, and the reason is worth keeping.**
Rebased onto `main` at `04b23bc`, four `ClientSettingsStoreTests` methods failed
with `Expected: CohortLateralSpreadV13 / Actual: LastStandEngagementV11` — a
concurrent session's V13 default, half-landed. That was not taken on trust and
not absorbed as this task's problem: the same four were run on unmodified `main`
at `04b23bc` in a detached probe worktree and failed identically, `Failed: 4,
Passed: 37`. They went green on their own once `541b8d6` shipped the V13 default.
A red gate on a shared checkout is a claim about the base until someone proves
otherwise, and proving it costs one worktree.
