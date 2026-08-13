# Pressure interrupt observability — plan

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
