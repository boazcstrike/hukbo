# Army Composition Settings — Plan

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Companion to
[2026-07-27-army-composition-settings-design.md](2026-07-27-army-composition-settings-design.md).
Read the design document first; every decision below is justified there.

## Verification criteria

The feature is complete when all of the following hold.

- [x] `Scenario.Validate()` rejects a roster-count array whose length differs
      from the preset roster count, with `ArgumentException`.
- [x] `Scenario.Validate()` rejects any element outside `[0, AgentsPerFaction]`,
      with `ArgumentOutOfRangeException`.
- [x] `Scenario.Validate()` rejects a sum that is not exactly `AgentsPerFaction`,
      with `ArgumentException`.
- [x] All three checks are skipped when `RosterCounts.IsDefaultOrEmpty`.
- [x] Two `Scenario` values with element-wise equal roster counts compare equal
      and hash equally.
- [x] The default path is byte-identical to today: seed-1, 200 agents produces
      state hash `6EBB1EA63114F6CE`, event hash `941377BD43C556FF`, and
      `Faction1Victory` at tick 235.
- [x] Both factions receive the same category at the same faction-local index,
      irrespective of the entity-ID offset between them.
- [x] `StateHasher.cs` and `CombatRuleset.cs` are untouched, confirmed by diff.
- [x] Category steppers clamp at their bounds and never wrap.
- [x] Apply is disabled whenever `Unassigned != 0`, and whenever the draft equals
      the saved composition.
- [x] `ClientSettingsStore.Load` returns defaults for a schema-version-1 file and
      does not throw.
- [x] No reference to `DefaultAgentCount` remains in the Client.
- [x] `./scripts/verify.ps1` passes, and its exact output is recorded below.
- [x] Manual smoke rows are added to `docs/development/testing.md` and left
      `PENDING` until a human runs them interactively.

## Phase A — Core

Runs after this plan is accepted. Tasks A1 and A2 have no shared file and may run
in parallel.

### A1. Roster count expansion

Files: `src/Hukbo.Core/Combat/RosterCountExpansion.cs` (new),
`tests/Hukbo.Core.Tests/RosterCountExpansionTests.cs` (new).

Failing tests first:

- `ExpandsCountsInDeclaredRosterIndexOrder`
- `ProducesOneEntryPerWarriorForTheGivenTotal`
- `RejectsNegativeCounts`
- `RejectsALocalIndexBeyondTheExpandedLength`

The expansion re-validates its own input rather than trusting the caller, since
it is a second public entry point that a test can reach directly. It is `O(n)`
either way.

### A2. Scenario roster counts

Files: `src/Hukbo.Core/Simulation/Scenario.cs`,
`tests/Hukbo.Core.Tests/ScenarioTests.cs`.

Failing tests first:

- `DefaultRosterCountsAreEmptyAndSkipValidation`
- `ValidateRejectsRosterCountsLengthMismatch`
- `ValidateRejectsRosterCountsElementOutOfRange`
- `ValidateRejectsRosterCountsSumThatIsNotAgentsPerFaction`
- `ValidateAcceptsAnExplicitlyEmptyRosterCountArray`
- `EqualityComparesRosterCountsElementwiseRatherThanByReference`
- `EqualScenariosProduceEqualHashCodes`

The manual `Equals` and `GetHashCode` overrides are the point of the last two
tests. Without them the record compares roster counts by array reference and
nothing fails at compile time.

### A3. Simulation branch

Files: `src/Hukbo.Core/Simulation/BattleSimulation.cs`,
`tests/Hukbo.Core.Tests/BattleSimulationTests.cs`. Depends on A1 and A2.

Failing tests first:

- `CreateUsesRoundRobinLoadoutsWhenRosterCountsAreEmpty`
- `CreateAssignsLoadoutsByFactionLocalIndexWhenRosterCountsAreProvided`
- `BothFactionsReceiveTheSameCategoryAtTheSameFactionLocalIndex`
- `RosterCountsDoNotChangeTheRandomDrawSequenceForSpawnPositions`

`CreateAgent` takes a resolved `CombatLoadout`; the branch stays in `Create`.

### A4. Determinism re-verification

No file changes. Depends on A3.

Run the Core determinism tests and the headless seed-1 workload, and confirm the
recorded oracle has not moved. If it has moved, stop: on the default path that is
a regression, not a legitimate hash change.

## Phase B — Client settings

May run in parallel with Phase A and with Phase C.

### B1. Schema version 2

Files: `src/Hukbo.Client/Settings/ClientSettings.cs`,
`src/Hukbo.Client/Settings/ClientSettingsStore.cs`,
`tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs`.

Failing tests first:

- `LoadTreatsASchemaVersionOneFileAsMissingAndReturnsDefaults`
- `LoadRejectsASchemaVersionNewerThanSupported`
- `SavedCompositionRoundTripsThroughTheStore`
- `LoadReturnsDefaultsForACompositionThatDoesNotSumToItsTotal`
- `AFailedSaveLeavesThePreviousValidFileIntact`

The existing test at `ClientSettingsStoreTests.cs:37` uses the literal
`schemaVersion: 2` as its example of an unsupported version. That literal becomes
the supported version and must be updated, or the test silently stops testing
anything.

### B2. Theme manager call site

Files: `src/Hukbo.Client/Theming/UiThemeManager.cs`, and whatever asserts on the
old `TrySave(string)` signature. Depends on B1. Regression only; existing theme
tests must stay green.

## Phase C — Client UI

C1 and C2 may run in parallel with each other and with Phases A and B.

### C1. Theme standards layout block

Files: `src/Hukbo.Client/Content/Themes/ui-theme-standards.json`,
`src/Hukbo.Client/Theming/UiThemeCatalogDocuments.cs`,
`src/Hukbo.Client/Theming/UiThemeCatalog.cs`,
`src/Hukbo.Client/Theming/UiTheme.cs`,
`src/Hukbo.Client/Theming/UiThemeCatalogFallback.cs`,
`tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs`.

These five source files are one atomic change. The JSON, the document shape, the
validator, the mapping, and the fallback must agree or the catalog rejects its own
content at startup.

Failing tests first:

- `StandardsExposeTheArmyCompositionLayout`
- `ValidationRejectsAMissingArmyCompositionLayout`
- `TheFallbackCatalogIncludesTheArmyCompositionLayout`

No new colour roles. Every state maps onto the existing 27.

### C2. Stepper helper

Files: `src/Hukbo.Client/UI/ArmyCompositionStepper.cs` (new) and its test file.

Failing tests first:

- `ClampsAtTheLowerBoundInsteadOfWrapping`
- `ClampsAtTheUpperBoundInsteadOfWrapping`
- `ShiftMultipliesTheCategoryStepByTen`
- `DistributeEvenlyGivesTheRemainderToTheEarliestCategories`
- `DistributeEvenlyMatchesTheRoundRobinDistributionForTheSameTotal`

The last test is what makes Reset to Default and Distribute Evenly the same
operation rather than two rules that could drift apart.

### C3. Composition panel

Files: `src/Hukbo.Client/UI/ArmyCompositionPanel.cs` (new) and its test file.
Depends on C1 and C2.

Failing tests first:

- `FocusWrapsAcrossTheNinePanelControls`
- `ApplyIsDisabledWhileAnyUnitsAreUnassigned`
- `ApplyIsDisabledWhenTheDraftEqualsTheSavedComposition`
- `ApplyIsEnabledWhenBalancedAndChanged`
- `TheUnassignedReadoutIsTheTotalMinusTheCategorySum`
- `CancelDiscardsTheDraftAndRestoresTheSavedComposition`
- `ResetToDefaultRecomputesTheEvenSplitAtTheCurrentTotal`
- `EnterAndSpaceDoNothingOnAStepperRow`

Tests construct no `GraphicsDevice`, no `SpriteBatch`, and no window. Every
decision under test lives in an `internal static` helper.

### C4. Menu wiring

Files: `src/Hukbo.Client/MenuOverlay.cs`,
`src/Hukbo.Client/Presentation/ClientCommand.cs`, and a new test file. Depends on
C3.

Failing tests first:

- `TheArmyCompositionButtonReturnsItsOwnCommand`
- `TheExistingButtonOrderIsPreservedAroundTheInsertion`

## Phase D — Wiring

### D1. ArenaGame

Files: `src/Hukbo.Client/ArenaGame.cs` only. Depends on A4, B2, and C4.

This is a hard serialisation point: the constructor and `ResetSimulation` both
build the `Scenario` and both live in this file, so they are one task. Remove
`DefaultAgentCount`, build the scenario from the saved composition, route the
panel's visibility, and show the staged banner.

`ArenaGame` cannot be unit tested per `CLAUDE.md` §5. Its verification is the
manual smoke checklist, which remains `PENDING` until a human runs the interactive
tests on a desktop. Implementation of all phases A through D is complete and the
automated gate has been recorded. The pending manual smoke rows are rows 30–35 in
[docs/development/testing.md](../../development/testing.md).

## Phase E — Closeout

### E1. Smoke rows

File: `docs/development/testing.md`. Add rows covering: opening the panel,
adjusting a category, the Unassigned readout, the Apply gate, the staged banner,
and a Full Reset actually fielding the chosen army. Leave every row `PENDING`.

### E2. Gate

Run `./scripts/verify.ps1` and paste the exact output into the section below.
Record whether the seed-1 oracle moved. On the default path it must not.

## Recorded gate result

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
Hukbo.Core.Tests    Total tests: 156   Passed: 156
Hukbo.Client.Tests  Total tests: 429   Passed: 429
seed 1, agentCount 200, requestedTicks 10000, measuredTicks 235
outcome Faction1Victory, faction0Survivors 0, faction1Survivors 30
eventHash  941377BD43C556FF
stateHash  6EBB1EA63114F6CE
deterministic true, firstMismatchTick null
allocatedBytes 15122504
```

Both the state hash (`6EBB1EA63114F6CE`) and event hash (`941377BD43C556FF`) are byte-identical to the previously recorded oracle, confirming that the default round-robin path did not move — which is the required outcome. The test counts include tests belonging to concurrent workstreams (sound system, plains backdrop, blood/gore) that share the same working tree at the time of the run, so they are not attributable to the army-composition feature alone. The Client count rose from 411 to 429 between two gate runs on the same day purely because those other workstreams landed further tests in the interval.
