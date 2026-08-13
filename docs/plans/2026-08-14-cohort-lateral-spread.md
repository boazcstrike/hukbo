# Cohort lateral spread — plan

Date: 2026-08-14
Design: `docs/plans/2026-08-14-cohort-lateral-spread-design.md` (that document
wins on any disagreement with this one)

Closes smoke row 58. Corrects and re-runs smoke row 59. Rows 60, 61 and 61a
already passed and are recorded, not re-tested.

## Task list

| # | Task | Files | Done when | Depends on |
| --- | --- | --- | --- | --- |
| 1 | Append `CohortLateralSpreadV13 = 13` with a doc comment stating what it changes relative to V11, and correct the stale V12 doc comment that still says no behaviour is gated on V12 | `src/Hukbo.Core/Movement/MovementPresetId.cs` | Enum value 13 exists; the V12 comment no longer contradicts `BattleSimulation.cs:5202` and `FormationPlanner.cs:233` | — |
| 2 | Register `CohortLateralSpreadV13Ruleset` restating V11's fields verbatim | `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | Both `switch` expressions resolve V13; every field equals V11's | 1 |
| 3 | Add the lateral riffle to `CohortDeploymentAssignment` behind a required `bool spreadCohortsLaterally` parameter; `false` keeps the existing size-descending, id-ascending traversal byte for byte | `src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs` | With `false` the output equals today's for every input; with `true` the traversal is even ids ascending then odd ids ascending | 1 |
| 4 | Admit V13 to `UsesBattlefieldRealism` and `YieldsLastStandEngagement`; pass `spreadCohortsLaterally: preset is CohortLateralSpreadV13` at both call sites | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | V13 inherits V11 whole and is the only preset that riffles | 2, 3 |
| 5 | Update the existing `CohortDeploymentAssignment` call sites in tests for the new parameter, passing `false` | `tests/Hukbo.Core.Tests/Movement/CohortDeploymentAssignmentTests.cs`, `tests/Hukbo.Core.Tests/Movement/ContingentShapeV12Tests.cs` | Both suites compile; no assertion value changes | 3 |
| 6 | V13 registry tests: numeric value 13, registered in both switches, field-equal to V11 | `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` or a new `Movement/CohortLateralSpreadV13Tests.cs` | Three facts, mirroring the V12 tests at `ContingentShapeV12Tests.cs:25-45` | 2 |
| 7 | Lateral-spread property tests on V13 | new `tests/Hukbo.Core.Tests/Movement/CohortLateralSpreadV13Tests.cs` | (a) the run-cut traversal for `n` contingents is even ids then odd ids; (b) no two cohort runs adjacent in the size-ranked sequence land in adjacent lanes except at the wrap; (c) shield-bearing warriors appear in contingents on both halves of the lateral span at 250 a side; (d) a contingent is still dominated by one cohort and splits are still at most `n - 1`; (e) V10 and V11 still produce the ascending traversal | 4 |
| 8 | Shipped-shape mirror test: 250 a side, `PrecolonialPhilippinesV5`, V13, `RosterCounts` derived as `ArenaGame` derives it — assert exact per-index mirror of position, contingent id and loadout at tick 0 | new file under `tests/Hukbo.Core.Tests/Movement/` | Green means the simulation mirrors and row 59 is a re-run; red means a real defect and the work stops for review | 4 |
| 9 | Pin V13's full-battle trajectory the way V10 and V11 are pinned — terminal tick, outcome, state hash, event fold — captured from the implemented build | the file from task 7 | Four literals, recorded from a real run, not copied from V11 | 4 |
| 10 | Flip the client default to V13 and add V13 to the panel options; check whether a persisted settings file would keep a returning player on V11 and say so | `src/Hukbo.Client/Settings/ClientSettingsStore.cs`, `src/Hukbo.Client/UI/ArmyCompositionPanel.cs` | A fresh launch runs V13; V13 is selectable | 2 |
| 11 | Update any Client test that pins the default preset or the panel option list | `tests/Hukbo.Client.Tests/` | Client suite green | 10 |
| 12 | Update the four research divergence notes with one clause about lateral spread | `docs/research/movement/README.md`, `docs/research/movement/tall-hardwood-shield.md`, `docs/research/battles/03-deep-past-formations-and-tactics.md`, `docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` | Each note describes the shipped rule | 4 |
| 13 | Canonical gate, not delegated: `./scripts/verify.ps1`, real output recorded in `docs/development/testing.md` | `docs/development/testing.md` | Gate output pasted, including which of the four stage-5 baselines ran | 1-12 |
| 14 | Smoke checklist, not delegated: rows 60, 61 and 61a to `PASS` with the tester's evidence; rows 58 and 59 to `PENDING` re-run against the V13 build; row 59's rotating-roster premise corrected and the post-tick-0 decay clause added | `docs/development/smoke-checklist.md` | Status column recounted at write time | 13 |

## Verification criteria

- `./scripts/verify.ps1` green, with its real output recorded.
- No V1 through V12 golden expectation moves. If one does, the gate on the new
  behaviour has leaked and the change is wrong.
- `tests/Hukbo.Core.Tests/FormationDeploymentFreezeTests.cs` and
  `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` both green untouched —
  they are the oracle that `FormationPlanner` was not modified.
- Both suites run, Core and Client: a scripts or enum change can redden the
  Client suite on its own.

## What this plan does not do

- It does not flip rows 58 or 59 to `PASS`. Only a person at an interactive
  desktop can do that, and this change is exactly the kind that has to be looked
  at rather than asserted.
- It does not touch `FormationPlanner`'s lane geometry.
- It does not archive the starting-deployment smoke section. That section stays
  open until 58 and 59 are re-run.
