# Battlefield realism — plan

Design document: `docs/plans/2026-08-11-battlefield-realism-design.md`. It
outranks this file wherever the two disagree; this file is the ordered task
list and the verification criteria.

Date: 2026-08-11. Branch: `battlefield-realism`. Base commit: `c13b696`.

## What this package delivers

Three behaviours, all gated behind one new movement preset,
`MovementPresetId.BattlefieldRealismV10 = 10`:

- **A.** Warriors are grouped by weapon, so a body of troops reads as a body of
  one weapon rather than an even slice of the whole army.
- **B.** Shield bearers take the forward-most slots of their own contingent.
- **C.** A ranged warrior with a melee enemy inside a threat radius backs
  directly away and resumes shooting once it is clear or once it is cornered.

All three ship as a labelled **Provisional reconstruction / gameplay model**.
Design sections 2.1 and 2.2 hold the evidence and the divergence register.

## Rules that bind every task

1. **The shipped default does not move.** `PersistentContingentsV4` and
   `PrecolonialPhilippinesV4` stay the scenario defaults. The first gate
   workload's recorded baseline — measured ticks 981, Faction1Victory,
   `stateHash 1B73FC5923879AA0`, `eventHash AC55684F24D39344` — must come back
   byte-identical at the end of this package.
2. **All nine frozen trajectory digests stay green, and none is recaptured.**
   `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1..v9-digest.json`
   plus the pre-clash digest are the leak detector for every V10 gate. A red
   digest is a defect in this change, never a fixture to update. The capture
   siblings at `MovementPresetFreezeTests.cs` lines 603 and 627 are not to be
   run.
3. **The `SplitMix64` draw count and draw order do not change.**
   `FormationPlanner`'s dealing loop, lattice, anchors, and both `NextJitter`
   draws at lines 313 and 314 are not modified by any task. This is an
   acceptance condition on tasks 3, 4, and 6 specifically, not a general hope.
4. **No agent may flip a manual smoke-checklist row to `PASS`.** Rows are added
   and re-worded as `PENDING`; only a person at an interactive desktop may pass
   one.
5. **The canonical gate is not delegated.** `./scripts/verify.ps1` runs once,
   after integration, and its real pasted output is the evidence.
6. **Forbidden vocabulary**, from `docs/research/ARMY-COMPOSITION.md` lines 515
   to 518, in code, comments, UI strings, tests, and docs: *shield wall*,
   *phalanx*, *shield line*, *front rank* as the name of a thing, *squad*,
   *platoon*, *captain*, *sergeant*, *company*, *regiment*.
7. **No new field on `WeaponProfile`, `CombatRuleset`, `MovementRuleset`, or
   `AgentState`.** Design sections 5.3 and 6.2 give the reasoning for each.

## Task table

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1 | Add `BattlefieldRealismV10 = 10` to the preset enum and register it as a verbatim restatement of `RangedStandoffV8Ruleset`'s field values with its own id. Doc comment states: gated on preset identity, not on a ruleset field, following the V8 and V9 precedent; carries no new field; all three behaviours are a labelled gameplay model. Both edits land in one commit — the enum value and the registration must not be split, because `BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset` asserts every enum value is registered. | `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | V10 is registered; the seven pinned content hashes in `MovementPresetRegistryTests.cs` at lines 33, 42, 51, 60, 69, 79, 106 are unchanged and green; no content-hash pin is added for V10, matching V8 and V9 | — | `dotnet test` on `Hukbo.Core.Tests`; `MovementPresetRegistryTests`; `MovementPresetFreezeTests` (all nine digests) |
| 2 | Append `AgentIntent.BackingAway = 6` after `Holding = 5`, with its own single-producer contract in the doc comment: written only by the V10 retreat rung, never by a rejection, a collision, a blocked proposal, or a failed route search. Extend `AgentIntentNumericValuesArePinned` with the new value. Renumbering or reordering any existing value is forbidden. | `src/Hukbo.Core/Simulation/AgentIntent.cs`, `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | The pin test asserts `BackingAway == 6` and every prior value keeps its number; all nine digests green | — | `BattleSimulationTests.AgentIntentNumericValuesArePinned`; `MovementPresetFreezeTests` |
| 3 | New pure type `CohortDeploymentAssignment` implementing design sections 4.2 to 4.6: cohort key from the roster index of the resolved loadout; cohorts ordered by size descending then key ascending; contingents ordered by size descending then id ascending; positional pairing; then, inside each contingent, shield bearers first paired against slots ordered `XRaw` descending, `YRaw` ascending, slot index ascending. Never draws, never mutates its input, no `Dictionary`, no `HashSet`, every sort key chain ending in a distinct index. Not yet wired into `BattleSimulation`. | `src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs` (new), `tests/Hukbo.Core.Tests/Movement/CohortDeploymentAssignmentTests.cs` (new) | Unit tests cover: a cohort at least as large as a contingent fills it purely; splits number at most `contingentCount - 1`; shield bearers occupy the forward-most slots of each contingent; the occupied-coordinate set is exactly the input set; identical inputs give identical output across repeated calls; a single-member contingent is the identity | 1 | The new test file; `dotnet test` on `Hukbo.Core.Tests` |
| 4 | New pure type `RangedRetreatRules`: `ThreatRadiusRaw(int standoffDistanceRaw)` and `IsThreatened(long nearestMeleeSquared, int threatRadiusRaw)`, with `ThreatRadiusBasisPoints = 5_000` as a named `const` whose doc comment marks it a provisional gameplay-tuning value under `CLAUDE.md` section 7 and not a historical measurement. Integer arithmetic only; result bounded to `[0, standoffDistanceRaw]`. Not yet wired. | `src/Hukbo.Core/Movement/RangedRetreatRules.cs` (new), `tests/Hukbo.Core.Tests/Movement/RangedRetreatRulesTests.cs` (new) | Unit tests cover zero standoff giving zero radius, the boundary at exactly the radius, and overflow safety at `int.MaxValue` standoff | — | The new test file |
| 5 | Comment-only amendment recording the deliberate, labelled divergence at the two sites that currently prohibit it: `FormationPlanner.cs` lines 20 to 22 (the "not attested" paragraph) and lines 100 to 103 (the round-robin deal comment), and `ContingentState.cs` lines 8 to 10 ("never a positional assignment"). The negative historical claims are **not** weakened; each gains a sentence naming V10 as a labelled gameplay model that diverges, and pointing at the design document. No behaviour changes. | `src/Hukbo.Core/Simulation/FormationPlanner.cs`, `src/Hukbo.Core/Simulation/ContingentState.cs` | Both files build; the diff is comments only; no forbidden vocabulary appears | 1 | `./scripts/format.ps1 -Verify`; `git diff` shows comment lines only |
| 6 | Wire task 3 into `BattleSimulation.Create`: a V10-gated branch beside the existing `movement.UsesEquipmentRelativeFootwork` branch at lines 587 to 604, calling `CohortDeploymentAssignment.AssignForFaction` per faction on the canonical unmirrored deployment, before the faction-1 reflection, resolving loadouts through the existing `ResolveSpawnLoadout`. **Serial:** `BattleSimulation.cs` is edited by tasks 6, 7, and 8 and no two of them may run in parallel. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | V10 spawns are permuted; every other preset spawns byte-identically; all nine digests green; `RosterCountsDoNotChangeTheRandomDrawSequenceForSpawnPositions` green; `FormationPlannerTests` all green including the mirror and no-contact tests | 3 | `MovementPresetFreezeTests`; `FormationPlannerTests`; `EquipmentFormationAssignmentTests`; `BattleSimulationTests` |
| 7 | Add the derived threat-observation scratch: one `long` per agent on `BattleSimulation`, allocated in the constructor, **sized zero unless the preset is V10**, cleared and filled inside the existing `SelectTargetsAndIntents` candidate loop at the point where the squared distance is already computed (line 1241), minimising over living enemies whose weapon is melee, for actors whose own weapon is ranged. Never hashed, never snapshotted, no new scan, no per-tick allocation. **Serial with 6 and 8.** | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | The array is zero-length under V1 through V9; a test asserts the observed nearest-melee distance against a hand-built scenario; all nine digests green; the per-tick allocation figure is unchanged for non-V10 presets | 1, 6 | `MovementPresetFreezeTests`; a new test in `tests/Hukbo.Core.Tests/Movement/RangedRetreatTests.cs` |
| 8 | Insert the retreat rung, turning the V8 two-way ladder at lines 1725 to 1760 into the three-way ladder of design section 5.2, and widen the V8 preset equality test to a two-value predicate so V10 inherits the standoff hold unchanged. Includes the reflected-destination retreat builder and both hazard rules of design section 5.5: the clamp-bites rule (fall back to `Holding`, write no proposal) and the no-stall-consultation rule. **Serial with 6 and 7 — this is the last of the three `BattleSimulation.cs` edits.** | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | A threatened shooter ends the tick further from the threat than it began; a cornered shooter reads `Holding`, never `BackingAway`; a retreating shooter never accumulates a blocked streak and never enters the sidestep path; V8's behaviour is unchanged | 2, 4, 7 | `MovementPresetFreezeTests` (v8 digest is the proof the predicate widening was inert); `RangedStandoffTests` all green; new tests in `RangedRetreatTests.cs` |
| 9 | Equivalence and mirror tests for V10 in Core: (a) a V10 battle whose roster fields no ranged weapon is event-identical and hash-identical to the same battle under V8 except for the deployment permutation; (b) a V10 battle in which no shooter is ever threatened produces the same ordered event stream as V8; (c) under V10 with a populated `RosterCounts` the two factions are exact per-index mirrors; (d) under V10 with the default rotating roster the two factions are positionally equivalent but not per-index identical, written as a positive assertion about grouping and depth so it cannot pass by the permutation silently not running. Design section 7.2 is binding on (d). | `tests/Hukbo.Core.Tests/Movement/BattlefieldRealismV10Tests.cs` (new) | All four hold; no existing mirror assertion is edited or weakened | 6, 8 | The new test file |
| 10 | The twenty-seed termination sweep of design section 8.3: run V10 with `PrecolonialPhilippinesV5` over seeds 1 through 20, 200 agents, 10,000-tick cap, and record `measuredTicks`, `outcome`, survivors, and both hashes per seed. Add a `RangedTerminationTests` sibling for V10 with the same both-factions-win bar the V8 test applies. **Report the real numbers; if the bar fails, tune `ThreatRadiusBasisPoints` downward, re-measure, and if it still fails, report the failure and stop. The bar is never moved to fit the measurement.** | `tests/Hukbo.Core.Tests/RangedTerminationTests.cs` | No seed reaches the tick cap; seed 1 is at or under 1,962 ticks; the median is at or under 3,000 ticks; both factions win at least one of the twenty | 8, 9 | The test itself plus the pasted sweep output |
| 11 | Inspector presentation: map `AgentIntent.BackingAway` to "Backing away from close fighters" in `AgentInspectorContent`, ahead of the catch-all arm at line 430 that would otherwise render it as "Holding". Add the evidence-tier badge and the plain "gameplay model" note to the contingent row and the intent row, using the existing badge mechanism. No forbidden vocabulary. | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | The new string is distinct from "Holding at range"; a test asserts every `AgentIntent` value maps to a distinct non-default string; the badge and note render | 2 | `dotnet test` on `Hukbo.Client.Tests` |
| 12 | Battle-report presentation: add a per-faction `BackingAwayCount` to `BattleReport` and recompute it in `BattleReportAccumulator` on the same terms as `HoldingCount` — replaced, never summed, never retained past the ingest. | `src/Hukbo.Client/Presentation/BattleReport.cs`, `src/Hukbo.Client/Presentation/BattleReportAccumulator.cs`, `tests/Hukbo.Client.Tests/BattleReportAccumulatorTests.cs` | The count is live and per-faction; it resets exactly as `HoldingCount` does; no GPU, window, or wall-clock dependency in any test | 2 | `dotnet test` on `Hukbo.Client.Tests` |
| 13 | Repoint the running game: `ArenaGame.cs` lines 1437 and 1438 to `PrecolonialPhilippinesV5` + `BattlefieldRealismV10`, and update the reasoning comment at lines 1414 to 1416, which currently states V5 is only ever paired with V8. **Serial and alone — `ArenaGame.cs` is a shared seam that other sessions touch, and it is never handed to a parallel agent.** | `src/Hukbo.Client/ArenaGame.cs` | The launched game runs V10; the comment no longer contradicts the line beneath it | 1, 8 | `dotnet test` on `Hukbo.Client.Tests`; `./scripts/run.ps1` launches |
| 14 | Add a third headless block to `scripts/verify.ps1` for `PrecolonialPhilippinesV5` + `BattlefieldRealismV10`, inside the existing `if ($Game -eq 'Hukbo')` guard, leaving the V8 block untouched. Update `ScriptDefaultsTests`: benchmark-invocation count 2 to 3 (line 31), `Game = $Game` pass-through count 3 to 4 (line 97), plus the class summary and the new block's assertions. Design section 9.2 records why a third block rather than a repointed one. | `scripts/verify.ps1`, `tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs` | The gate runs three workloads; both the Core and Client suites are green after the edit, because a `scripts/*.ps1` change can turn the C# Client suite red | 1 | `dotnet test` on both `Hukbo.Core.Tests` and `Hukbo.Client.Tests` |
| 15 | Amend the four research documents to record the deliberate divergence without weakening any negative claim: `docs/research/movement/tall-hardwood-shield.md` (claim THS-08, line 104), `docs/research/movement/README.md` (line 166), `docs/research/battles/03-deep-past-formations-and-tactics.md` (line 65), and `docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` (the sections at lines 836 to 864 and 1138 to 1140). Each gains a short, clearly separated note naming V10, its tier as a gameplay model, and the design document. The evidence tiers, the source citations, and the negative findings themselves are unchanged. Never cite the skirmisher-screen passage at lines 277 to 296 — it is a misread of a Chinese force. | `docs/research/movement/tall-hardwood-shield.md`, `docs/research/movement/README.md`, `docs/research/battles/03-deep-past-formations-and-tactics.md`, `docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` | Every existing tier and claim is byte-identical except for the appended notes; full normal English, no compression | — | Read-back diff; `git diff --stat` shows additions only |
| 16 | The `aliping namamahay` inspector note that `docs/research/ARMY-COMPOSITION.md` lines 503 to 513 requires and that `PhilippineCombatPresetV5.cs` line 320 leaves unmet: a roster fielding a household dependent must say in the inspector that doing so is a reconstruction, not an attested fact. Ask B makes this urgent by putting shield-bearing *namamahay* at the visible front. | `src/Hukbo.Client/UI/AgentInspectorContent.cs` — **serial after task 11, same file** | Selecting an `AlipingNamamahay` warrior shows the reconstruction note; a Client test asserts it | 11 | `dotnet test` on `Hukbo.Client.Tests` |
| 17 | Smoke-checklist edits, part one: reset rows 102, 103, and 105 of the contingent section (lines 4983, 4984, 4986) from `PASS` to `PENDING` with their evidence cells cleared, because V10 changes what a group is made of and the recorded observations no longer describe the shipped pairing. Re-word rows 58, 59, 60, 61, and 61a (lines 4435 to 4439) and RG-6 and RG-7 (line 5477 onward) so they describe V10's behaviour rather than V4's and V8's. **No row may be set to `PASS`.** | `docs/development/testing.md` | The named rows are `PENDING` and worded against V10; no row was passed | 13 | Read-back; row-state grep |
| 18 | Smoke-checklist edits, part two: add new `PENDING` rows for weapon-group legibility, shields at the leading edge, shields taking the first blows, mirror status under V10, ranged warriors backing away, a cornered ranged warrior standing and fighting, whether the back-pedal reads as backing away rather than fleeing, the battle still terminating, and the new intent wording in the inspector. **Serial after 17 — same file. No row may be set to `PASS`.** | `docs/development/testing.md` | Nine or more new `PENDING` rows exist, each naming what failure looks like | 17 | Read-back; row-state grep |
| 19 | Record the gate result and the V10 baseline in `docs/development/testing.md`: the unchanged first-block numbers (981 ticks, Faction1Victory, `1B73FC5923879AA0` / `AC55684F24D39344`), the unchanged V8 block, and a new recorded section for the V10 block in the same form as the existing "Canonical gate result — Hukbo" section at lines 87 to 112, plus the task 10 sweep table. **Serial after 18 — same file. Only real pasted output may be recorded.** | `docs/development/testing.md` | Every number in the section came from a run that actually happened | 10, 18, and the integration gate | The pasted `./scripts/verify.ps1` output |

## Parallel and serial structure

**Parallel-safe, no shared files:** 1, 2, 4, 5, 15 may all run at once. 3
follows 1 but is otherwise independent. 11, 12, and 14 may run at once once
their dependencies are met.

**Strictly serial, one file, one agent at a time:**

- `src/Hukbo.Core/Simulation/BattleSimulation.cs` — tasks **6 → 7 → 8**, in that
  order, never concurrently.
- `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` — task 1 only.
- `src/Hukbo.Client/ArenaGame.cs` — task 13 only, alone, never handed to a
  parallel agent.
- `src/Hukbo.Client/UI/AgentInspectorContent.cs` — tasks **11 → 16**.
- `docs/development/testing.md` — tasks **17 → 18 → 19**.

A suggested wave structure that respects all of the above:

| Wave | Tasks |
| --- | --- |
| 1 | 1, 2, 4, 5, 15 |
| 2 | 3 |
| 3 | 6, then 7, then 8 (serial); 11, 12, 14 in parallel alongside |
| 4 | 9, 16 |
| 5 | 10, 13 |
| 6 | 17, then 18 |
| 7 | Integration gate, then 19 |

## Verification criteria for the package as a whole

The package is finished only when all of the following are true and evidenced by
real pasted output.

1. `./scripts/verify.ps1` is green, run once after integration, with its actual
   output pasted. Three headless workloads run.
2. The default workload returns measured ticks 981, `Faction1Victory`,
   `stateHash 1B73FC5923879AA0`, `eventHash AC55684F24D39344`, deterministic
   true — byte-identical to the recorded baseline.
3. The V8 workload's numbers are unchanged from whatever the pre-change run
   produces on this same base commit, captured before the first task lands so
   there is something to compare against.
4. All nine frozen trajectory digests plus the pre-clash digest are green and
   none was recaptured.
5. All seven pinned movement content hashes and every combat-preset content-hash
   pin are unchanged. `CombatRuleset.ContentHash` did not move and there is no
   `PrecolonialPhilippinesV6`.
6. The twenty-seed termination sweep meets every clause of design section 8.3,
   with the per-seed table recorded.
7. `./scripts/format.ps1 -Verify` passes and the build is warning-free under
   `TreatWarningsAsErrors`.
8. No forbidden vocabulary appears anywhere in the diff.
9. The smoke checklist has its new and re-worded rows, all `PENDING`, and no
   agent flipped any row to `PASS`.

## Known risks

- **Task 8 is the risky one.** It is the change the ranged design already named
  as the most likely thing to break the termination bar. Task 10 exists to catch
  that with a number, and the plan's stated response to a failed bar is to tune
  once, re-measure, and then report failure — not to move the bar.
- **Task 6 changes spawn positions**, so every downstream measured figure for
  V10 differs from V8's. Nothing outside V10 may move, and the digests are what
  prove it.
- **Tasks 6, 7, and 8 all edit `BattleSimulation.cs`.** Running any two of them
  in parallel is a merge conflict created on purpose. The wave table above is
  not a suggestion on this point.
- **Task 14 edits a shell script that the C# Client suite pins.** Both suites
  are run after it, not just the one that looks related.
- **The branch base moves.** `main` gains commits during a session of this size.
  Rebase before the integration gate; a red Core test on a Client-only change is
  a stale branch base until proven otherwise.
