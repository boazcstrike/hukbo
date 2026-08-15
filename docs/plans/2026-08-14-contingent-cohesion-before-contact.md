# Contingent cohesion before contact — plan

Date: 2026-08-14

Executes [`2026-08-14-contingent-cohesion-before-contact-design.md`](2026-08-14-contingent-cohesion-before-contact-design.md).
Where this plan and that design disagree, the design wins, except on the points
recorded in "Findings that contradict the design" below, where the design
describes code that is not on disk.

## Preconditions, verified on disk before planning

The design's section 7 declares itself blocked on the cohort lateral spread
workstream. That block is lifted, and both halves of the claim were checked
against the working tree rather than taken on trust:

- `MovementPresetId.CohortLateralSpreadV13 = 13` is on `main` at
  `src/Hukbo.Core/Movement/MovementPresetId.cs:282`, registered in both registry
  switches at `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:672` and
  `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:691`, with its ruleset
  declared at `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:634-655`.
- It is the shipped client default, at
  `src/Hukbo.Client/Settings/ClientSettingsStore.cs:91-92`, and the canonical
  gate runs it as its fifth benchmark block at `scripts/verify.ps1:105-113`.

The new preset value is therefore **14**, appended after 13, never renumbered.

## Scope boundary

### Files this plan may edit

Simulation and rules, in `Hukbo.Core`:

- `src/Hukbo.Core/Movement/MovementPresetId.cs` — append the value 14 and its doc
  comment. Nothing else in the file is touched.
- `src/Hukbo.Core/Movement/MovementRuleset.cs` — append the new ruleset fields as
  trailing optional constructor parameters, and fold the numeric ones inside a
  version gate in `ComputeContentHash`
  (`src/Hukbo.Core/Movement/MovementRuleset.cs:628-700`).
- `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` — declare the V14 ruleset
  and add its two switch arms. No existing ruleset's registered field values
  change.
- `src/Hukbo.Core/Movement/MovementRules.cs` — **doc comments only**, to correct
  the two remarks blocks that misdescribe which contingents the narrowed scan
  excludes. No executable statement in this file changes, and no method
  signature changes.
- `src/Hukbo.Core/Simulation/BattleSimulation.cs` — admit V14 to the three
  preset-identity gates it inherits from V13; read the cohesion band from the
  ruleset in the straggler test; scale the cohesion square's margin from the
  ruleset. Everything else is comment work.
- `src/Hukbo.Core/Simulation/FormationRules.cs` — add one margin-taking form of
  `IsCohesionSquareWithinBounds` alongside the existing jitter-taking one at
  `src/Hukbo.Core/Simulation/FormationRules.cs:570-584`. The existing method
  keeps its signature and its body so every current caller is unaffected.

Client, in `Hukbo.Client`:

- `src/Hukbo.Client/UI/ArmyCompositionPanel.cs` — append V14 to
  `MovementPresetOptions` (`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:126-138`)
  and its display name to `MovementPresetNames`
  (`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:148-160`).

Tests:

- `tests/Hukbo.Core.Tests/Movement/ContingentCohesionBeforeContactV14Tests.cs` —
  new file.
- `tests/Hukbo.Core.Tests/Movement/ContingentCohesionCalibrationHarness.cs` — new
  file, compiled only behind the `HUKBO_CALIBRATION` preprocessor symbol.
- `tests/Hukbo.Core.Tests/RangedTerminationTests.cs` — append one twenty-seed
  sweep for V14 beside the existing V8 and V10 sweeps.
- `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs` — extend the
  arrow-cycle walk at
  `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs:397-433` so its wrap
  assertion still lands on the real last entry.

Documentation:

- `docs/plans/2026-08-14-contingent-cohesion-before-contact.md` — this plan.
- `docs/plans/2026-08-14-contingent-cohesion-before-contact-design.md` — status
  line, and a correction note in section 7 recording that the block is lifted and
  the value is 14.
- `docs/plans/README.md` — register both documents in the table.
- `docs/development/smoke-checklist.md` — a new row for the V14 observation.
- `docs/development/testing.md` — the gate record and the measured termination
  numbers.

### Files this plan may not edit

Forbidden by rule R4 of the design's section 4, and by its section 3's ban on
regularizing spacing:

- `src/Hukbo.Core/Simulation/FormationPlanner.cs` — lane geometry, anchors, slot
  geometry, and contingent sizing. Not touched at all, by any task, for any
  reason.
- `src/Hukbo.Core/Movement/ContingentOffset.cs` — the per-member personal offset.
  The jitter value fed to it at
  `src/Hukbo.Core/Simulation/BattleSimulation.cs:3716-3719` must remain
  byte-identical under every preset including V14, because changing it is exactly
  the "make the contingent neater" move section 3 forbids.
- `src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs` — owned by the cohort
  lateral spread workstream.

Forbidden because they are the freeze oracle this change is measured against:

- `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` — the nine replay facts
  for V1 through V9 at
  `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:120-323`. The
  `HUKBO_CALIBRATION` capture routine at
  `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:571-700` may be *invoked*
  but the file is not edited.
- `tests/Hukbo.Core.Tests/FormationDeploymentFreezeTests.cs` — the five
  deployment cases at
  `tests/Hukbo.Core.Tests/FormationDeploymentFreezeTests.cs:49-118`.
- `tests/Hukbo.Core.Tests/Fixtures/**` — every committed digest JSON. Not one
  byte moves.
- `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` — the seven pinned
  `ContentHash` literals at
  `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs:33-106`. If a task needs
  to edit this file, the fold gate was written wrong.
- `tests/Hukbo.Core.Tests/Movement/CohortLateralSpreadV13Tests.cs`,
  `ContingentShapeV12Tests.cs`, `LastStandEngagementV11Tests.cs`,
  `BattlefieldRealismV10Tests.cs` — the four pinned-trajectory test files for the
  presets immediately below this one.

Forbidden because the client default is not flipped:

- `src/Hukbo.Client/Settings/ClientSettingsStore.cs` — `DefaultMovementPreset`
  stays `CohortLateralSpreadV13`.
- `scripts/verify.ps1` — the five benchmark blocks stay as they are. V14 is
  opt-in, and V12 set the precedent that an opt-in preset gets no gate block of
  its own.
- `tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs` — pins those blocks at
  `tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs:85-92` and stays green
  untouched.

Forbidden always:

- `src/Hukbo.Core/Determinism/**` — the pinned SplitMix64 vectors and the state
  hasher's field order.
- Anything under `src/Sandata.*` or `tests/Sandata.*`.

## Task table

| # | Task | Files | Done when | Depends on |
| --- | --- | --- | --- | --- |
| 1 | Establish the pre-change freeze baseline. Run the full `Hukbo.Core.Tests` suite at `HEAD` and record, in this plan's own results section, that all nine movement replay facts, all five formation deployment cases, and all four pinned-trajectory tests for V10 through V13 are green before a single line is edited. This is the oracle every later task replays against; capturing it after the first edit would prove only that the build is consistent with itself. | None. Read-only measurement. | The suite is green at `HEAD` and the nineteen freeze facts are named individually in the results section, not summarized as "tests pass". | — |
| 2 | Add the four new ruleset fields, gated. Append `gathersContingentsBeforeContact` (bool), `cohesionBandNumerator`, `cohesionBandDenominator`, and `cohesionSquareMarginBasisPoints` (int) as trailing optional constructor parameters defaulting to `false, 0, 0, 0`, so not one of the thirteen existing registry call sites changes. In `ComputeContentHash`, fold the three numeric fields **inside** `if (GathersContingentsBeforeContact)` and do not fold the gate itself, following the `AppliesPressureInterrupt` precedent at `src/Hukbo.Core/Movement/MovementRuleset.cs:647-663` exactly. Add constructor validation that throws when the gate is true and any numeric is outside its legal range, mirroring `ValidateEquipmentRelativeFootworkCoupling`. | `src/Hukbo.Core/Movement/MovementRuleset.cs` | The four properties exist; the three numerics fold only behind the gate; the seven pinned `ContentHash` literals at `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs:33-106` are unedited and green; all nine movement replay facts and all five deployment cases still reproduce byte-identically. | 1 |
| 3 | Append the enum value and register the ruleset **in one change**. Add `ContingentCohesionBeforeContactV14 = 14` with a doc comment stating what it changes relative to V13 and naming this plan, and declare `ContingentCohesionBeforeContactV14Ruleset` restating every one of V11's registered field values verbatim (`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:573-594`) plus the gate `true` and the three new values at provisional starting settings. Add both switch arms. These cannot be split across two tasks: `ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset` at `tests/Hukbo.Core.Tests/BattleSimulationTests.cs:1732-1744` enumerates `Enum.GetValues<MovementPresetId>()` and asserts every value is registered, so the suite is red in between. | `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | Value 14 exists; both switches resolve it; every field except `Id` and the four new ones equals V11's; the enum-enumerating leader test is green; the nineteen freeze facts are unmoved. | 2 |
| 4 | Admit V14 to the three preset-identity gates V13 already passes, so V14 is a strict superset of the shipped default and an A/B against it isolates R1 through R3 rather than also re-testing the lateral riffle. Add V14 to `UsesBattlefieldRealism` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:5214-5218`), to `YieldsLastStandEngagement` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1532-1535`), and to the `spreadCohortsLaterally` predicate (`src/Hukbo.Core/Simulation/BattleSimulation.cs:708-709`). Extend each of the three doc comments with the same one-sentence justification the V13 admission already carries. Widening a predicate from a closed set to a larger closed set cannot change any member of the old set. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | All three predicates admit V14; V13's own pinned trajectory at `tests/Hukbo.Core.Tests/Movement/CohortLateralSpreadV13Tests.cs:604-614` reproduces its four literals unchanged; V10, V11, and V12's pinned trajectories are unmoved; the nineteen freeze facts are unmoved. | 3 |
| 5 | **R1 — `Advance` pulls in more than stragglers.** In `TryResolveContingentCohesionAimPoint`, replace the hardcoded three-quarters comparison at `src/Hukbo.Core/Simulation/BattleSimulation.cs:3676-3693` with the ruleset band when `GathersContingentsBeforeContact` is true, and execute the byte-identical existing `(Int128)16 * memberSquared > (Int128)9 * cohesionRadiusRaw * cohesionRadiusRaw` statement when it is false. Keep the `Int128` widening and its comment; the overflow argument is unchanged and still load-bearing. Do **not** change `MovementRules.IsCohesionEligible`'s signature — it is called directly from `tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs` and `tests/Hukbo.Core.Tests/PersistentContingentTests.cs`, and gate 4's semantics at `src/Hukbo.Core/Movement/MovementRules.cs:444-447` are correct as written; only the boolean the caller computes changes. Leave `ResolveContingentState`'s rule 5 hysteresis at `src/Hukbo.Core/Movement/MovementRules.cs:303-305` alone — that is a contingent-spread band, not a member-distance band, and R1 does not name it. All arithmetic stays integer or `Int128`; no `float`, no `double`, no `System.Random`, no new draw. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | Under V14 a member inside three-quarters of the cohesion radius but outside the registered band is cohesion-eligible in `Advance`; under V1 through V13 the emitted comparison is unchanged; the nineteen freeze facts and the four pinned trajectories for V10 through V13 are unmoved. | 4 |
| 6 | **R2 — narrow the blanket denial, and record that it is already narrow.** Read `TakesPartInCrossContingentScan` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1934-1943`) against `MovementRules.ParticipatesInCrossContingentScan` (`src/Hukbo.Core/Movement/MovementRules.cs:355-360`) and confirm the excluded set is already exactly `{Close, Break}` and nothing else. Because it is, R2's behavioural half is already satisfied and **no executable statement changes**. Deliver R2 as a pin plus two comment corrections: correct the blanket-denial comment at `src/Hukbo.Core/Simulation/BattleSimulation.cs:1794-1802`, which reads as though the denial were broader than the two states, and correct the second remarks paragraph at `src/Hukbo.Core/Movement/MovementRules.cs:336-345`, which says an excluded square makes a neighbour's grant unsafe when exclusion can only ever relieve a neighbour's overlap, never create one. | `src/Hukbo.Core/Movement/MovementRules.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs` (comments only), `tests/Hukbo.Core.Tests/Movement/ContingentCohesionBeforeContactV14Tests.cs` | A test asserts `ParticipatesInCrossContingentScan` returns false for exactly `Close` and `Break` and true for every other `ContingentState` including `None`; `git diff` on the two source files contains no changed executable line; the nineteen freeze facts are unmoved. | 4 |
| 7 | **R3 — size the claimed square below the packing bound, without touching spacing.** The square is already contingent-sized (see finding 1), so R3 is delivered as a ruleset-tunable scale on the *claimed margin only*. Add a margin-taking form of `IsCohesionSquareWithinBounds` beside the existing one at `src/Hukbo.Core/Simulation/FormationRules.cs:570-584`, leaving that method's signature and body untouched. In `ResolveContingentStates`, compute `_contingentMarginRaw[slot]` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1783`) through `cohesionSquareMarginBasisPoints` when the gate is true, and pass that margin to both gate 5 (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1785-1791`) and gate 6 (`src/Hukbo.Core/Simulation/BattleSimulation.cs:1840-1846`) so the two gates cannot disagree about the square's size. `_contingentJitterRaw[slot]` is **not** scaled: it feeds `ContingentOffset.Compute` at `src/Hukbo.Core/Simulation/BattleSimulation.cs:3716-3719`, and scaling it would change member spacing, which R4 and design section 3 forbid outright. Basis-point arithmetic in `long`; a registered value of `10_000` must be bit-identical to today. | `src/Hukbo.Core/Simulation/FormationRules.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs` | At `10_000` basis points every preset's square is bit-identical and the nineteen freeze facts are unmoved; under V14's registered value the square's half-side is strictly smaller for every living count from 1 to 200; `_contingentJitterRaw` and every offset derived from it are byte-identical under every preset; `FormationPlanner.cs` is untouched. | 4 |
| 8 | Build the calibration harness, gated behind `HUKBO_CALIBRATION` so it adds zero tests to any ordinary build or gate stage — the same shape `tests/Hukbo.Core.Tests/Movement/PressureInterruptCalibrationHarness.cs` and the capture routine at `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:571-700` already use. It must report, per seed across seeds 1 through 20 and for both V13 and V14: the share of living-contingent-ticks resolved to `Hold`, the share of `Advance` members granted a cohesion destination, the tick of first contact, and the terminal tick and outcome. Those four numbers are what turn the band and margin from guesses into measurements. | `tests/Hukbo.Core.Tests/Movement/ContingentCohesionCalibrationHarness.cs` (new) | A clean ordinary build discovers no new test; a `HUKBO_CALIBRATION` build runs the harness and prints the four measures per seed per preset. | 5, 6, 7 |
| 9 | Settle the three tunables from measurement, not from taste. Run task 8's harness and choose `cohesionBandNumerator`, `cohesionBandDenominator`, and `cohesionSquareMarginBasisPoints` so that V14's `Hold` share strictly exceeds V13's and its granted-cohesion share under `Advance` strictly exceeds V13's, while the twenty-seed termination clauses still hold. Record the measured table in this plan's results section and label the chosen values a provisional reconstruction for gameplay tuning, per the design's section 6 question 7 — they are game-design choices, not historical measurements, and no source describes either quantity. If no setting satisfies both the cohesion clause and the termination clause, stop and report rather than loosening the termination clause. | `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | The three values are registered; the measured before-and-after table is in the results section; the cohesion clause and the termination clause both hold at the chosen values. | 8 |
| 10 | Write the V14 registry facts and property tests, mirroring the shape of `tests/Hukbo.Core.Tests/Movement/CohortLateralSpreadV13Tests.cs:40-80`. Registry facts: numeric value 14, registered, own identity, every V11 field carried forward, the gate `true`, and the three new values equal to what task 9 registered. Property tests: a member at a distance between the registered band and three-quarters of the cohesion radius is cohesion-eligible under `Advance` on V14 and not on V13; V14's claimed square half-side is strictly below V13's for the same living count; and V14's `_contingentJitterRaw` equals V13's for the same living count, which is the mechanical proof that spacing did not change. | `tests/Hukbo.Core.Tests/Movement/ContingentCohesionBeforeContactV14Tests.cs` | All registry facts and all three property tests pass; the spacing-invariance test is present and passing, because it is the regression guard for design section 3's prohibition. | 9 |
| 11 | Add the twenty-seed termination sweep. This is the gate on the whole change, and the design's section 5 names it so. Mirror `SeedsOneThroughTwentyProduceVictoriesForBothFactionsUnderBattlefieldRealism` at `tests/Hukbo.Core.Tests/RangedTerminationTests.cs:179-265` exactly — the same `RangedRosterShareWeights`, the same `PrecolonialPhilippinesV5` pairing, the same tick cap, the same three clauses — changing only the movement preset, so V10's result and V14's are read against one yardstick. | `tests/Hukbo.Core.Tests/RangedTerminationTests.cs` | At least nineteen of twenty seeds decide before the cap, the median decisive tick is at or under the cap, and each faction wins at least four seeds. A preset that gathers and never resolves fails here, which is the outcome movement preset V7 was allowed to have and this one is not. | 9 |
| 12 | Add the blocked-streak deadlock guard. A preset that gathers more eagerly parks more aim points closer together, so the failure mode is a warrior walking to tangency and pushing forever, not only a slow battle. Sweep seeds 1 through 20 under V14 and assert the worst blocked streak stays under the same bound `AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty` uses at `tests/Hukbo.Core.Tests/LastStandFormationTests.cs:691-693`. Record in the test's own remarks that twenty seeds is a sample and not a proof, in the same honest register that test already uses. | `tests/Hukbo.Core.Tests/Movement/ContingentCohesionBeforeContactV14Tests.cs` | The worst streak across twenty seeds is below the bound, and the sample's limitation is written down rather than implied. | 9 |
| 13 | Capture and pin V14's full-battle trajectory. Four literals — terminal tick, outcome, state hash, event fold — from a real run of the built code, following `CohortLateralSpreadV13FullBattleReproducesItsPinnedTrajectory` at `tests/Hukbo.Core.Tests/Movement/CohortLateralSpreadV13Tests.cs:604-637`, including the pinned body radius of four world units and the explicit `PrecolonialPhilippinesV2` selection, so the fixture cannot drift when a shipped default moves. This task is last among the simulation tasks by necessity: R1, R2, and R3 each move the trajectory, so a literal captured before task 9 settles the tunables is stale the moment it is written. Never hand-calculate a hash. | `tests/Hukbo.Core.Tests/Movement/ContingentCohesionBeforeContactV14Tests.cs` | The four literals are captured from the built tree and reproduce on a second clean run; the test's remarks name the commit the capture came from. | 9, 10, 11, 12 |
| 14 | Make V14 selectable without flipping the default. Append `MovementPresetId.ContingentCohesionBeforeContactV14` to `MovementPresetOptions` (`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:126-138`) and `"V14 Contingent Cohesion Before Contact"` to `MovementPresetNames` (`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:148-160`), in the same position in both. Extend the arrow-cycle walk at `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs:397-433` by one step so its wrap assertion still lands on the real last entry — that assertion has broken twice already for exactly this reason and its own comment says so. `ClientSettingsStore.DefaultMovementPreset` stays at V13; a tester picks V14 from the panel. | `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`, `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs` | `EveryRegisteredMovementPresetHasAMatchingDisplayName` at `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs:355-386` is green, which is a full sequence equality against the registry and therefore fails if V14 is registered but unselectable; the arrow-cycle test is green; `ClientSettingsStore.cs` is unedited and `ScriptDefaultsTests` is green untouched. | 3 |
| 15 | Documentation. Set the design's status line to executed and add a correction note in its section 7 recording that the block is lifted, that the value is 14, and that the findings below were carried into this plan rather than silently implemented as written. Register both documents in `docs/plans/README.md`. Add a smoke row asking a person to watch a V14 battle and answer the `BR-1` question — do contingents cross the field as bodies — and leave it `PENDING`, because only a person at an interactive desktop may close it. Record the gate and the measured termination numbers in `docs/development/testing.md`. | `docs/plans/2026-08-14-contingent-cohesion-before-contact-design.md`, `docs/plans/README.md`, `docs/development/smoke-checklist.md`, `docs/development/testing.md` | The design no longer claims to be blocked; both documents appear in the README table; the new smoke row exists and is `PENDING`, not `PASS`. | 13, 14 |
| 16 | Run the canonical gate and record the result honestly. `./scripts/verify.ps1` in full, all five stages, with the five benchmark blocks unchanged. Record the outcome in `docs/development/testing.md` whether it is green or red, naming the commit. | `docs/development/testing.md` | The gate is green at a named commit, and the record says so with evidence rather than assertion. | 15 |

## Verification criteria

**No existing preset changes behaviour.** This is the hardest constraint and the
cheapest to check. Nineteen frozen facts must reproduce byte-identically after
every one of tasks 2 through 7: the nine movement replay facts for V1 through V9
at `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:120-323`, the five
formation deployment cases at
`tests/Hukbo.Core.Tests/FormationDeploymentFreezeTests.cs:49-118`, and the four
pinned full-battle trajectories for V10, V11, V12, and V13. If any one of them
moves, the change is wrong and the correct response is to revert the task that
moved it, not to re-record the fixture. Re-recording is what the freeze tests
exist to make unnecessary, and their own type-level remarks say so.

**The seven pinned content hashes do not move.**
`tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs:33-106` pins `ContentHash`
literals for V1 through V7. The version-gated fold of task 2 is what keeps them
still: a preset whose gate is false writes nothing new into the hash at all. If
task 2 is done wrong — folding the numerics unconditionally, or folding the gate
flag itself — all seven literals move at once, which is a loud and unambiguous
failure signal. Do not update the literals to match. Fix the fold.

**SplitMix64 draw counts do not change.** R1, R2, and R3 read state that already
exists. R1 compares an existing squared distance against a ruleset-derived
threshold. R2 changes no executable line. R3 scales an existing margin. None of
the three draws, and the per-member offset at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:3716-3719` is a seed-and-entity-id
derivation rather than a stream draw, so its consumption is unchanged as well.
The proof is the freeze suite: a changed draw count would move V1's trajectory,
and V1 has no cohesion code in it at all.

**Spacing, jitter, and slot geometry are untouched.** `FormationPlanner.cs` does
not appear in any diff. `_contingentJitterRaw` is equal under V13 and V14 for
every living count, asserted directly by task 10's third property test. The
smoke row asserting irregular spacing, which passed, is the behavioural guard the
design's section 3 names, and it must still pass when a person re-runs it.

**Termination.** The twenty-seed sweep of task 11 is the gate on the whole
change. Nineteen of twenty seeds decisive before the cap, median at or under the
cap, each faction winning at least four. Alongside it, the blocked-streak guard
of task 12 catches the different failure where the battle does finish but a
warrior spent hundreds of ticks pushing against an ally it could not get past.
The design's section 5 names movement preset V7 as the precedent: a preset whose
behaviour was interesting and whose termination bar it did not meet was shipped
anyway, frozen at a draw on the ten-thousandth tick, and the fixture that records
that draw is still in the tree. V14 is not permitted to repeat it.

**No banned construct enters `Hukbo.Core`.** No `System.Random`. No `float` or
`double` reaching either hash — the band comparison stays in `Int128` and the
margin scale stays in `long` basis points. No new heap allocation on a warm tick,
which the design's section 6 question 8 already claims and which task 7 must not
quietly break by allocating a scratch array in the per-slot loop.

**The preset is reachable.** `EveryRegisteredMovementPresetHasAMatchingDisplayName`
at `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs:355-386` is a full
sequence equality between the registry and the selector, strengthened on
2026-08-14 precisely because V12 shipped registered and unselectable with a green
suite. It fails if task 14 is skipped, which is the point.

**The client default is unchanged.** `ClientSettingsStore.cs` and `verify.ps1`
appear in no diff. A spectator who has never opened the panel gets V13, exactly
as before.

**A person watches a battle.** The design's section 6 question 9 ends with a
human, and its section 2 answer is that a spectator sees groups crossing the
field together rather than a scatter. No automated check substitutes for that.
The smoke row task 15 adds stays `PENDING` until a person at an interactive
desktop closes it.

## What this plan does not do

It does not flip the shipped client default. The design's section 8 puts that
outside its own scope and its section 5 says the flip happens only after a person
has watched a battle and confirmed the effect. That is a separate decision, taken
later, on evidence this plan produces but does not itself weigh.

It does not touch `FormationPlanner`, lane geometry, anchor rules, or contingent
sizing. R4 forbids it, the cohort lateral spread design owns that surface, and
the design's section 3 rules out anything that regularizes spacing on evidentiary
grounds rather than engineering ones.

It does not make contingents neater. No dressing, no ranks, no files, no fixed
frontage, no shield wall, no prearranged manoeuvre, no command signal. The corpus
names every one of those as unattested, and the only thing it says about spacing
is that spacing is irregular. This change makes a contingent stay together; it
does not make it tidy.

It does not synchronize anything across an army. Each contingent decides for
itself, tick by tick, from its own state. An army-wide halt is barred outright by
the "no army-wide synchronized motion" finding and by the absence of any
command-signal evidence.

It does not change the within-contingent shield-forward rule, which was verified
correct and is not what `BR-2` fails on.

It does not add a benchmark block to `scripts/verify.ps1`. Those blocks exist to
cover the preset the client actually ships, and V14 is opt-in. V12 set that
precedent and V14 follows it.

It does not re-record any frozen fixture. If a fixture moves, the change is
wrong.

It does not settle the tunables by judgement. Task 8 measures and task 9 chooses
from the measurement, and if no setting satisfies both the cohesion clause and
the termination clause, the correct outcome is to stop and report rather than to
loosen the termination clause until something passes.

## Findings that contradict the design

These change what a task has to do, so they are recorded here rather than
discovered mid-implementation.

**1. R3's premise is false: the cohesion square is already sized to the
contingent.** The design's section 2 states that "the cohesion square is sized by
`CohesionRadiusMultiplier`, which is **24** body radii", and R3 builds on that by
arguing "a 24-body-radius square is claimed by a contingent of three and a
contingent of forty alike". Neither is what the code does. The square's half-side
is `_contingentMarginRaw[slot]`, computed at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1783` as
`jitterRaw + BodyRadiusRaw`, where `jitterRaw` comes from
`FormationRules.ComputeContingentJitterRaw` at
`src/Hukbo.Core/Simulation/FormationRules.cs:400-417` and equals
`bodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)`. A contingent of three
therefore claims a half-side of five body radii and a contingent of forty claims
fourteen — already proportional to headcount, and already far below twenty-four.
`CohesionRadiusMultiplier`, which is indeed 24 under V11 and V13
(`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:576` and `:637`), is used for
two entirely different things: rule 5's contingent-spread test at
`src/Hukbo.Core/Movement/MovementRules.cs:291-305` and the member straggler test
at `src/Hukbo.Core/Simulation/BattleSimulation.cs:3680-3692`. It never sizes the
square. Task 7 therefore delivers R3's *stated purpose* — make `Hold` reachable
under a realistic eight-contingent deployment by shrinking what a contingent
claims — through a ruleset-tunable scale on the claimed margin only, rather than
through the mechanism R3 describes, which does not exist. This also means task 7
shrinks the square below the packing bound that `ComputeContingentJitterRaw`'s
own derivation establishes, which is a real trade and the reason task 12's
deadlock guard is in the plan at all.

**2. R2 is a no-op as written.** The design's section 4 says "a slot excluded from
the scan because it is in `Close` or `Break` ... that denial is correct. A slot
excluded for any other reason is denied today for an implementation convenience",
and asks that the blanket marking be restricted to the `Close` and `Break` cases.
There are no other reasons. `TakesPartInCrossContingentScan` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1934-1943` returns true
unconditionally under a preset that does not narrow the scan, and otherwise
delegates to `MovementRules.ParticipatesInCrossContingentScan` at
`src/Hukbo.Core/Movement/MovementRules.cs:355-360`, whose entire body is
`tickStartState != Close && tickStartState != Break`. The excluded set is already
exactly the set R2 wants it restricted to, so R2's behavioural half is already
delivered by the shipped code. Task 6 therefore pins that fact with a test and
corrects the two comments that make the denial read broader than it is, and
changes no executable line. Relatedly, the design's section 2 states that an
excluded contingent's square is "unavailable to relieve anyone else's overlap
either" — that is inverted. A square absent from the pairwise scan at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1812-1855` can only ever cause
*fewer* overlap findings for its neighbours, never more. Exclusion relieves; it
cannot deny.

**3. R1 names the wrong reference point.** R1 says a member is eligible "while its
distance from the contingent's aim point exceeds a cohesion band". The quantity
gate 4 actually consults is the member's distance from its **leader**:
`memberSquared = SquaredDistance(agent, leader)` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:3679`. The design's own section 2
agrees, describing a straggler as "one outside three-quarters of the cohesion
radius from its leader", so section 4 contradicts section 2 rather than
contradicting only the code. Task 5 plans the leader distance, on the strength of
two witnesses against one. Switching to the aim point would be a genuinely
different rule — the aim point is the trail base plus a per-member jitter offset,
so it is not a single point per contingent — and if that is what was meant, it
needs to go back to the design first.

**4. "New golden expectations" means a four-literal pinned trajectory, not a
digest fixture.** The design's section 5 says the new preset "gets its own
registry entry, its own registration test, and new golden expectations", which
reads as a `MovementPresetFreezeTests` digest. It is not available: that file
freezes V1 through V9 only
(`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:86-111`), and V10 through
V13 each pin four literals in their own test file instead, as at
`tests/Hukbo.Core.Tests/Movement/CohortLateralSpreadV13Tests.cs:604-614`. Task 13
follows the V10-through-V13 convention. This changes nothing about the intent,
but it changes which file an implementer opens.

**5. The design's section 7 is stale, as expected.** It is now accurate only as
history: `CohortLateralSpreadV13` has landed on `main`, and the value to append is
14. Task 15 records the correction in the design itself so the next reader is not
misled by a block that no longer exists.

## Results

### Task 1 — the pre-change freeze baseline, 2026-08-15

Captured at `d610990`, on branch `hukbo-cohesion-v14`, in an isolated worktree,
before a single line of source was edited. Two other sessions were working in the
main checkout at the time, which is why this package runs in a worktree at all.

```
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release
Passed!  - Failed: 0, Passed: 2568, Skipped: 0, Total: 2568, Duration: 23 s
```

The nineteen frozen facts, named individually rather than summarized, every one
of them `Passed`:

The nine movement replay digests in `MovementPresetFreezeTests` —
`IndependentPursuitV1`, `PersistentContingentsV2`, `PersistentContingentsV3`,
`PersistentContingentsV4`, `PersistentContingentsV5`,
`EquipmentRelativeFootworkV6`, `EquipmentRelativeFootworkV7`,
`RangedStandoffV8`, and `MonotoneAllyClearanceV9`, each
`_ReproducesTheFrozenTrajectoryDigest`.

The five deployment cases in `FormationDeploymentFreezeTests` — `Default200`,
`EightContingentCeiling`, `MinimumMap`, `HalfNarrowerThanOneBody`, and
`DenseBlockFallback`, each `_MatchesTheFrozenDeployment` and the last of them
also asserting the stream is left untouched.

The five preset-identity facts for V10 through V13 —
`BattlefieldRealismV10FullBattleReproducesItsPinnedTrajectory` and
`LastStandEngagementV11FullBattleReproducesItsPinnedTrajectory`, both of which
live in `ContingentShapeV12Tests.cs`;
`CohortLateralSpreadV13FullBattleReproducesItsPinnedTrajectory`; and V12's two
byte-identity facts,
`ContingentShapeV12ProducesAByteIdenticalFullBattleToLastStandEngagementV11` and
`WithNoLastStandContingentShapeV12RunsByteIdenticallyToLastStandEngagementV11`.

**One correction to this plan's own wording, found while capturing the
baseline.** The verification criteria describe "the four pinned full-battle
trajectories for V10, V11, V12, and V13". There are three: V12 has no
four-literal trajectory of its own and pins byte-identity against V11 instead.
The count of nineteen is right — nine plus five plus three plus V12's two
identity facts — but the composition is not what the criteria say, and an
implementer looking for a `ContingentShapeV12FullBattleReproducesItsPinnedTrajectory`
will not find one. There is no `BattlefieldRealismV10Tests.cs` trajectory test
and no `LastStandEngagementV11Tests.cs` trajectory test either; both of those
literals live in `ContingentShapeV12Tests.cs`.
