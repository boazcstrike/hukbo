# Pressure interrupt observability — plan

**Archived: reference only.** This is a finished plan. The movement-preset
selector it exists to build shipped on 2026-08-13, and the eleven rows it
unblocked — `P-1` through `P-10` and `L-7` — were all run by a person on
2026-08-14 and all passed. Never execute it, never treat it as a live task list,
and never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`. Read "How
this closed, 2026-08-14" at the foot of this document before assuming every task
in it was verified the way the table says — task PO-7 was not.

**Date:** 2026-08-13
**Design:** `2026-08-13-pressure-interrupt-observability-design.md`, which
outranks this document wherever the two disagree.

Scope: a staged movement-preset selector in the client, so that the nine
`BLOCKED` `P` rows and the unreachable `L-7` row become executable. No change to
either simulation. No preset added. No hash moved.

## Tasks

| # | Task | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| PO-1 | Add `MovementPresetId MovementPreset` to the settings record | `src/Hukbo.Client/Settings/ClientSettings.cs` | Compiles; every construction site updated | — |
| PO-2 | Persist it: schema bump to 9, default `LastStandEngagementV11`, per-field fallback that also rejects an unregistered id | `src/Hukbo.Client/Settings/ClientSettingsStore.cs` | A schema-8 file still loads; an unregistered id falls back rather than reaching `Scenario.Validate` | PO-1 |
| PO-3 | Add a `SettingsChoiceSelector<MovementPresetId>` to the Army Composition panel, with layout slot, hit-test, and focus index | `src/Hukbo.Client/UI/ArmyCompositionPanel*.cs` | Focus and layout tests in the panel's existing style | PO-1 |
| PO-4 | `BuildScenario` reads the staged preset instead of the hardcoded `LastStandEngagementV11` | `src/Hukbo.Client/ArenaGame.cs:1435-1452`, apply path at `:1350-1372` | With untouched settings the scenario still names `LastStandEngagementV11`; the change applies only on Full Reset | PO-2, PO-3 |
| PO-5 | Tests | `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs`, the panel's test file | Round-trip, out-of-range resets only that field, missing field defaults cleanly, unregistered id falls back, default scenario unchanged | PO-4 |
| PO-6 | Rewrite the `P` section of the smoke checklist: nine rows `BLOCKED` to `PENDING`, `P-5` gains the mark description, `P-8` gains the tie wording, the preamble states how to select V7 and that V7 does not terminate | `docs/development/smoke-checklist.md` | Read by a person; no row flipped | PO-4 |
| PO-7 | Canonical gate | — | `./scripts/verify.ps1` real output, seed-1 baseline unmoved | PO-5 |

## Task PO-6 is blocked on a second session

`docs/development/smoke-checklist.md` was being rewritten by another Claude
session while this plan was written — the file moved four times in ten minutes
and lost 272 lines, and archive records for the persistent-contingent,
quit-confirmation, and shield-clash families were created in the same window.
Two sessions writing that file loses one of their work. PO-6 waits until the
other session has stopped.

## What closes the rows

Nothing in this plan. Every `P` row and `L-7` closes only when a person at an
interactive desktop selects the preset, watches the screen, and says so. The
plan's deliverable is that they are able to.

## Out of scope, deliberately

A preset combining `BattlefieldRealismV10`'s behaviour with the pressure
interrupt. Design section 3 records why it is neither a flag flip nor
constructible as first proposed, and section 6 records what it would owe before
anyone builds it. It is not authorized by this plan.

## How this closed, 2026-08-14

**Task PO-6 was unblocked and done.** The concurrent session that was rewriting
`docs/development/smoke-checklist.md` stopped, the `P` section was rewritten as
the task describes, and the eleven rows stood `PENDING` rather than `BLOCKED`
from 2026-08-13.

**All eleven rows then passed.** On 2026-08-14 a person at an interactive
desktop ran `P-1` through `P-10` and `L-7` for the first time and passed all of
them, reporting the section as a whole with no separate observation for any
individual row. That closed this family at 11 of 11 and took the leader marker
family with it, because `L-7` was that family's last open row. Both sections
were deleted from the live checklist, and the verdicts are recorded in the
2026-08-14 archive titled **"Footwork pressure interrupt smoke — closed
2026-08-14"**, named here in prose rather than linked.

That is the outcome this plan aimed at and could not itself produce. The plan's
deliverable was that the rows became *runnable*; a person made them true.

**Task PO-7, the canonical gate, has no recorded result and this plan is being
archived without one.** No gate output was ever pasted into this document. A run
was attempted on 2026-08-14 and failed at the Release build stage with ten
instances of `error CS7036` in
`tests/Hukbo.Core.Tests/Movement/CohortDeploymentAssignmentTests.cs`, from a
concurrent session's in-progress cohort lateral spread work, which is unrelated
to anything here. What was run instead on that day was
`./scripts/format.ps1 -Verify`, which reported
`[PASS] Formatting verification completed.` over 762 files, and the Client suite,
which reported `Passed! - Failed: 0, Passed: 3791`. Both cover the projects this
plan touched. Neither is the gate, and PO-7 stays unpaid.
