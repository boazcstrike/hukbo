# Orchestrated Hukbo Execution Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete the deterministic arena scaffold, runnable MonoGame client, Play/Pause/Exit menu, delivery workflows, and role-specific documentation through three non-overlapping agent workstreams.

**Architecture:** `Hukbo.Core` is the only authoritative gameplay layer. Headless and Client consume its stable scenario, snapshot, event, and hash contracts; the Client owns only scheduling, rendering, camera, UI, and application exit.

**Tech Stack:** .NET SDK 10.0.302, C# 14, MonoGame DesktopGL and Content Builder 3.8.5, xUnit 2/VSTest, PowerShell 7, GitHub Actions.

---

## Agent ownership

### Agent A — Simulation and Headless

**Owned paths:**

- `src/Hukbo.Core/**`
- `src/Hukbo.Headless/**`
- `tests/Hukbo.Core.Tests/**`

**Must not edit:** Client, scripts, CI, README, or docs.

### Agent B — Client and Menu

**Owned paths:**

- `src/Hukbo.Client/**`

**Must not edit:** Core, Headless, Core tests, scripts, CI, or docs.

### Agent C — Delivery and Documentation

**Owned paths:**

- `scripts/**`
- `.github/**`
- `README.md`
- `docs/agents/**`
- `docs/architecture/**`
- `docs/development/**`
- `docs/repository-*.md`
- `docs/dependency-*.md`
- `docs/platform-*.md`

**Must not edit:** `src/**`, `tests/**`, or active `docs/plans/**`.

The orchestrator owns root build policy changes, integration, commits, and final
verification.

## Shared Core contract

Agent A implements and Agent B consumes:

```csharp
public sealed class BattleSimulation
{
    public static BattleSimulation Create(Scenario scenario);
    public Scenario Scenario { get; }
    public long Tick { get; }
    public BattleOutcome Outcome { get; }
    public IReadOnlyList<AgentView> Agents { get; }
    public IReadOnlyList<BattleEvent> LastEvents { get; }
    public void AdvanceOneTick();
    public ulong ComputeStateHash();
}

public sealed record Scenario(
    ulong Seed,
    int MapWidth,
    int MapHeight,
    int AgentsPerFaction,
    int TickRate,
    int TickLimit)
{
    public static Scenario CreateDefault(
        ulong seed = 1,
        int totalAgents = 200);
}

public readonly record struct AgentView(
    ulong EntityId,
    int FactionId,
    int XRaw,
    int YRaw,
    int HitPoints,
    int MaximumHitPoints,
    ulong? TargetEntityId,
    AgentIntent Intent,
    bool IsAlive);
```

Names may vary only when both agents agree through the orchestrator. Agent B
must not duplicate simulation models.

### Task 1: Validate scenarios and authoritative state

**Owner:** Agent A

**Files:**

- Create: `src/Hukbo.Core/Simulation/Scenario.cs`
- Create: `src/Hukbo.Core/Simulation/AgentState.cs`
- Create: `src/Hukbo.Core/Simulation/AgentView.cs`
- Create: `src/Hukbo.Core/Simulation/AgentIntent.cs`
- Create: `src/Hukbo.Core/Simulation/BattleOutcome.cs`
- Create: `tests/Hukbo.Core.Tests/ScenarioTests.cs`

**Step 1: Write failing validation tests**

Cover valid default 100-versus-100, invalid faction count, invalid map/tick
bounds, nonpositive health/damage/range/speed, and arithmetic-overflow risk.

**Step 2: Verify expected failure**

Run:

```powershell
dotnet test tests/Hukbo.Core.Tests `
  --filter FullyQualifiedName~ScenarioTests
```

Expected: compilation failure because scenario types do not exist.

**Step 3: Implement minimal models**

Use immutable scenario configuration, monotonic `ulong` IDs, integer fixed-point
positions, and explicit validation before simulation allocation.

**Step 4: Verify**

Expected: focused tests pass.

### Task 2: Implement deterministic battle behavior

**Owner:** Agent A

**Files:**

- Create: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Create: `src/Hukbo.Core/Simulation/BattleEvent.cs`
- Create: `src/Hukbo.Core/Simulation/BattleSnapshot.cs`
- Create: `src/Hukbo.Core/Determinism/StateHasher.cs`
- Create: `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`
- Create: `tests/Hukbo.Core.Tests/DeterminismTests.cs`

**Step 1: Write failing tests**

Cover stable nearest-target selection, entity-ID ties, fixed-step movement,
exact-range attacks, cooldown, accumulated simultaneous damage, mutual death,
dead-agent inactivity, single victory event, and same-seed equality.

**Step 2: Verify expected failure**

Run the two focused test classes and confirm missing implementation failures.

**Step 3: Implement the fixed pipeline**

For every tick:

1. select targets in entity-ID order;
2. gather movement proposals;
3. commit movement in entity-ID order;
4. gather attack proposals;
5. accumulate and apply damage simultaneously;
6. resolve death and outcome;
7. emit events in stage/entity order;
8. expose read-only views and hash authoritative fields explicitly.

Use no wall clock, `System.Random`, unordered gameplay iteration, task
parallelism, or MonoGame type.

**Step 4: Verify**

Run all Core tests in Release. Expected: pass without a window, GPU, audio, or
network after restore.

### Task 3: Add deterministic headless execution

**Owner:** Agent A

**Files:**

- Create: `src/Hukbo.Headless/Program.cs`
- Create: `src/Hukbo.Headless/HeadlessRunner.cs`
- Create: `src/Hukbo.Headless/RunReport.cs`

**Step 1: Implement validated arguments**

Support `--agents`, `--ticks`, `--seed`, and optional `--output`. Invalid input
returns a nonzero code with an actionable message.

**Step 2: Implement verification**

Run two independently constructed simulations, compare per-tick or terminal
hashes and ordered events, and report the first mismatch.

**Step 3: Implement measurement**

Emit JSON containing environment, seed, agent count, measured ticks, duration,
tick percentiles, allocations, outcome, survivors, event hash, state hash, and
`deterministic`.

**Step 4: Verify**

```powershell
dotnet run --project src/Hukbo.Headless -c Release -- `
  --agents 200 --ticks 10000 --seed 1
```

Expected: exit zero and `"deterministic": true`.

### Task 4: Build the MonoGame spectator shell

**Owner:** Agent B

**Files:**

- Create: `src/Hukbo.Client/Program.cs`
- Create: `src/Hukbo.Client/ArenaGame.cs`
- Create: `src/Hukbo.Client/SpectatorCamera.cs`
- Create: `src/Hukbo.Client/InputEdges.cs`
- Modify: `src/Hukbo.Client/Content/Content.mgcb`
- Create: `src/Hukbo.Client/Content/Default.spritefont`

**Step 1: Add guarded startup**

Startup exceptions go to standard error and set a nonzero exit code. Normal
window close exits zero.

**Step 2: Add fixed scheduling**

Advance Core at 20 logical ticks per second independently from draw rate.
Support Space play/pause, `1`/`2`/`4` speed, and `R` same-seed reset.

**Step 3: Add rendering and camera**

Draw every live agent with one runtime-created white dot texture and one sprite
batch. Support WASD/arrows and mouse-wheel zoom. Render diagnostic counts,
tick, speed, and outcome with the compiled font.

**Step 4: Verify Client build**

```powershell
dotnet build src/Hukbo.Client -c Release
```

Expected: content and client compile successfully.

### Task 5: Implement Play/Pause/Exit overlay

**Owner:** Agent B

**Files:**

- Create: `src/Hukbo.Client/MenuOverlay.cs`
- Create: `src/Hukbo.Client/MenuButton.cs`
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify: `src/Hukbo.Client/InputEdges.cs`

**Step 1: Isolate menu state**

Menu state contains visibility, pointer hover, focused button, and edge-triggered
activation. It receives screen-space input and returns one of `None`, `Play`,
`Pause`, or `Exit`.

**Step 2: Draw the UI**

Draw a translucent backdrop, centered panel, and labeled Play, Pause, Exit Game
buttons with normal/hover/focus/pressed/disabled states.

**Step 3: Wire behavior**

- Escape opens/closes the overlay.
- Opening pauses simulation scheduling.
- Play resumes and closes.
- Pause leaves the overlay visible and simulation paused.
- Exit invokes the MonoGame exit path exactly once.

**Step 4: Verify**

Build Client and, when interactive desktop access is available, verify every
button, Escape, window close, and the absence of simulation advancement while
paused.

### Task 6: Add developer workflows and CI

**Owner:** Agent C

**Files:**

- Create: `scripts/doctor.ps1`
- Create: `scripts/bootstrap.ps1`
- Create: `scripts/build.ps1`
- Create: `scripts/test.ps1`
- Create: `scripts/run.ps1`
- Create: `scripts/benchmark.ps1`
- Create: `scripts/format.ps1`
- Create: `scripts/package.ps1`
- Create: `scripts/verify.ps1`
- Create: `.github/workflows/ci.yml`

**Step 1: Add non-destructive scripts**

Use strict PowerShell, derive the root from `$PSScriptRoot`, inspect every
native exit code, and never reset, clean, stash, or delete user files.

**Step 2: Add canonical verification**

Locked restore, format check, Release build, tests, and 200-agent headless run
must compose through `verify.ps1`.

**Step 3: Add Windows CI**

Use `windows-2025`, minimum permissions, immutable action SHAs, SDK 10.0.302,
NuGet caching, locked restore, verification, and artifact upload.

**Step 4: Verify locally**

Run doctor, bootstrap, build, test, format, benchmark, and package.

### Task 7: Write agent evidence and launch documentation

**Owner:** Agent C

**Files:**

- Create: `docs/agents/00-enhanced-execution-prompt.md`
- Create: `docs/agents/01-game-platform-engine-decision.md`
- Create: `docs/agents/02-repository-discovery-constraints.md`
- Create: `docs/agents/03-toolchain-prerequisites.md`
- Create: `docs/agents/04-native-dependencies-platform-sdk.md`
- Create: `docs/agents/05-dependency-compatibility.md`
- Create: `docs/agents/06-environment-bootstrap.md`
- Create: `docs/agents/07-solution-architecture.md`
- Create: `docs/agents/08-repository-scaffolding.md`
- Create: `docs/agents/09-configuration-package-management.md`
- Create: `docs/agents/10-game-runtime-integration.md`
- Create: `docs/agents/11-content-asset-pipeline.md`
- Create: `docs/agents/12-test-architecture.md`
- Create: `docs/agents/13-static-analysis-quality.md`
- Create: `docs/agents/14-developer-experience.md`
- Create: `docs/agents/15-ci-build-test.md`
- Create: `docs/agents/16-repository-readiness-validation.md`
- Create: `docs/agents/17-technical-review-handoff.md`
- Create: `docs/agents/README.md`
- Modify: `README.md`
- Create: `docs/development/getting-started.md`
- Create: `docs/development/testing.md`
- Create: `docs/repository-readiness-report.md`

**Step 1: Record each role**

Every report contains scope, inputs, decisions, changed files, verification,
status, limitations, and next action. Planned checks are not reported as passed.

**Step 2: Document exact launch**

Primary command:

```powershell
./scripts/run.ps1
```

Fallback command:

```powershell
dotnet run --project src/Hukbo.Client -c Release
```

Document camera, speed, reset, Escape, Play, Pause, and Exit Game.

**Step 3: Record readiness**

Use `READY`, `CONDITIONALLY READY`, or `NOT READY` from actual final evidence.

### Task 8: Integrate, evaluate, and review

**Owner:** Orchestrator

**Step 1: Inspect ownership**

Reject or reconcile any contribution outside its assigned paths.

**Step 2: Integrate Core before Client**

Compile the final shared contract, then correct Client adapters only.

**Step 3: Run the evaluator loop**

Classify failures and change only their cause, with at most three cycles for the
same failure mode.

**Step 4: Run full verification**

```powershell
./scripts/verify.ps1
./scripts/package.ps1 -Runtime win-x64
```

Attempt the interactive UI smoke separately.

**Step 5: Review the whole diff**

Resolve all Critical and High findings; preserve the repository owner's
untracked research documents; remove temporary or generated artifacts.

**Step 6: Finalize evidence and handoff**

Update readiness and agent reports only after final commands complete. Provide
the exact run command and controls.
