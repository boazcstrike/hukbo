# Round Scoring, Reset, and Memory Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add session-local Team A/Blue and Team B/Red win scoring, deterministic next-round and full-reset behavior, and lower per-tick allocation pressure without changing battle determinism.

**Architecture:** Keep one disposable `BattleSimulation` per round and place score/seed progression in a small GPU-independent Client presentation class. Integrate reset commands through the current `ClientCommand` UI boundary. Reuse fixed-capacity Core scratch buffers while preserving externally observable event snapshots and state/event hashes.

**Tech Stack:** C# 14, .NET 10, MonoGame DesktopGL, xUnit, PowerShell verification scripts.

---

### Task 1: Specify the round-series lifecycle

**Files:**
- Create: `tests/Hukbo.Client.Tests/MatchSeriesTests.cs`
- Create: `src/Hukbo.Client/Presentation/MatchSeries.cs`

**Step 1: Write the failing tests**

Cover these binary outcomes:

- initial wins are `0 / 0` at seed `1`;
- ordinary reset after `Faction0Victory` increments only Team A;
- ordinary reset after `Faction1Victory` increments only Team B;
- draw or ongoing reset increments neither team;
- each ordinary reset advances to a distinct deterministic seed;
- full reset clears both wins and restores the initial seed;
- repeated full reset is idempotent.

**Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter FullyQualifiedName~MatchSeriesTests
```

Expected: failure because `MatchSeries` does not exist.

**Step 3: Implement the minimal pure class**

Create a session-local class that maps faction 0 to Team A/Blue and faction 1 to Team B/Red. `StartNextRound(BattleOutcome)` records only a terminal victory and advances the seed with deterministic overflow-safe unsigned arithmetic. `FullReset()` clears wins and restores the constructor seed.

**Step 4: Re-run the focused tests**

Expected: all `MatchSeriesTests` pass without creating a window or graphics device.

**Step 5: Commit checkpoint**

```powershell
git add src/Hukbo.Client/Presentation/MatchSeries.cs `
  tests/Hukbo.Client.Tests/MatchSeriesTests.cs
git commit -m "feat(client): add round score lifecycle"
```

Only create this commit if the files can be isolated from the pre-existing dirty rename.

### Task 2: Integrate ordinary and full reset through the current UI

**Files:**
- Modify: `src/Hukbo.Client/Presentation/ClientCommand.cs`
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify: `src/Hukbo.Client/MenuOverlay.cs`
- Modify: `src/Hukbo.Client/UI/MatchSummaryPanel.cs`
- Modify: `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` if reset coordination changes

**Step 1: Add failing command/reset tests**

Prove that disposable presentation state is cleared and playback is paused for both reset types. Do not instantiate `ArenaGame`.

**Step 2: Replace stale menu integration**

Use the current `UiInteraction` and `ClientCommand` contracts; do not resurrect the removed `MenuAction`. Add distinct `NextRound` and `FullReset` commands.

**Step 3: Add one atomic reset path**

- `R` and the menu/summary next-round action record the outgoing terminal win, advance the seed, create a fresh scenario/simulation, preserve scores, clear transient presentation state, and pause.
- `Shift+R` and the menu full-reset action clear scores, restore seed `1`, create the initial scenario/simulation, clear transient presentation state, restore 1x speed/camera fit, and pause.
- Draws and abandoned ongoing rounds do not score.

**Step 4: Render the score**

Show `Team A (Blue)` and `Team B (Red)` wins in the HUD and a compact score plus current seed in the window title. Update help text so both reset shortcuts are discoverable.

**Step 5: Run Client tests and build**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
dotnet build src/Hukbo.Client/Hukbo.Client.csproj -c Release --no-restore
```

Expected: all Client tests pass and the pre-existing `MenuAction` compile failure is gone.

### Task 3: Remove Core hot-loop allocation churn

**Files:**
- Modify: `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`
- Modify: `src/Hukbo.Core/Simulation/BattleSimulation.cs`

**Step 1: Write the failing allocation regression**

Warm a quiet two-faction simulation, measure `GC.GetAllocatedBytesForCurrentThread()` across many ticks, and assert a small bounded total rather than a machine-specific working-set value.

**Step 2: Verify the test fails before optimization**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter FullyQualifiedName~RepeatedQuietTicks
```

Expected: failure from per-tick proposal/view allocations.

**Step 3: Reuse fixed-size buffers**

Allocate movement proposals, attack proposals, and the agent-view backing array once per simulation. Refill/clear them each tick behind stable read-only wrappers. Preserve `LastEvents` snapshot semantics unless a focused test proves callers cannot observe mutation.

**Step 4: Run Core and determinism tests**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release
```

Expected: all tests pass, including mutual-death and single-outcome-event behavior.

### Task 4: Verify fairness, determinism, and allocation improvement

**Files:**
- Modify tests only if a missing regression is found.

**Step 1: Run the seed distribution guard**

Run fixed seeds 1 through 20 and verify at least one victory for each faction. Do not force alternating winners.

**Step 2: Run the identical baseline workload**

```powershell
dotnet src/Hukbo.Headless/bin/Release/net10.0/Hukbo.Headless.dll `
  --agents 200 --ticks 10000 --seed 1
```

Expected invariant results:

- outcome: `Faction1Victory`;
- measured ticks: `235`;
- survivors: Blue `0`, Red `30`;
- event hash: `CE35EDA4B2A4E5A4`;
- state hash: `210C5EF8E7BE4D48`;
- allocated bytes: strictly below the captured `19,856,712` baseline.

**Step 3: Run the full local gate**

```powershell
./scripts/verify.ps1
git diff --check
```

Expected: restore, format, Release build, all tests, and deterministic workload pass.

### Task 5: Update operating documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/development/testing.md`

Document Team A/Blue and Team B/Red scoring, `R` next round, `Shift+R` full reset, score timing, seed progression, and the manual reset/score checks. Keep unperformed interactive rows `PENDING`.

Run:

```powershell
git diff --check
./scripts/verify.ps1 -SkipBootstrap
```

### Task 6: Independent review and commit

Have a fresh reviewer inspect the complete diff for score double-counting, scoring abandoned rounds, seed replay, stale UI commands, event/view retention regressions, allocation-test brittleness, determinism changes, and unrelated dirty-worktree content. Resolve every Critical and High finding.

Stage and commit only paths/hunks attributable to this plan:

```powershell
git commit -m "feat(game): add scored deterministic rounds"
```

If the pre-existing untracked Hukbo rename prevents an isolated coherent commit, leave changes uncommitted and report that exact blocker rather than committing unrelated work.
