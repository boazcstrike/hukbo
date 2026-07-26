# Autonomous Arena Foundation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build and verify a deterministic 200-agent headless combat simulation with a MonoGame DesktopGL spectator client on Windows x64.

**Architecture:** A plain `net10.0` core owns all authoritative state and fixed-tick rules. A headless executable measures and verifies the core, while a MonoGame DesktopGL executable renders read-only state and translates spectator input without owning gameplay truth.

**Tech Stack:** .NET SDK 10.0.302, C# 14, MonoGame 3.8.5 DesktopGL, xUnit/VSTest, PowerShell 7, GitHub Actions.

---

### Task 1: Pin the repository toolchain and build policy

**Files:**
- Create: `global.json`
- Create: `AutonomousArena.slnx`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `NuGet.config`
- Create: `.editorconfig`
- Modify: `.gitignore`
- Create: `.gitattributes`

**Step 1: Add the pinned SDK**

Set the SDK to `10.0.302` with `rollForward` set to `latestPatch` and
`allowPrerelease` false.

**Step 2: Add repository-wide build settings**

Target `net10.0`, enable nullable and implicit usings, use C# 14, enable
deterministic builds, and treat warnings as errors.

**Step 3: Add central packages**

Pin MonoGame DesktopGL and MonoGame Content Builder Task to `3.8.5`, then pin
the selected xUnit/VSTest packages. The empty content definition verifies the
pipeline while v0.1 creates its dot texture at runtime.

**Step 4: Add the four projects to the solution**

Add Core, Headless, Client, and Core.Tests with only inward references.

**Step 5: Verify**

Run: `dotnet --version`  
Expected: `10.0.302`

Run: `dotnet restore AutonomousArena.slnx`  
Expected: restore succeeds after the project files exist in Task 2.

**Step 6: Commit**

Commit message: `build: pin .NET and repository configuration`

### Task 2: Scaffold the minimal project boundaries

**Files:**
- Create: `src/AutonomousArena.Core/AutonomousArena.Core.csproj`
- Create: `src/AutonomousArena.Headless/AutonomousArena.Headless.csproj`
- Create: `src/AutonomousArena.Client/AutonomousArena.Client.csproj`
- Create: `tests/AutonomousArena.Core.Tests/AutonomousArena.Core.Tests.csproj`

**Step 1: Create the Core project**

Use an SDK-style class library with no package references.

**Step 2: Create the Headless project**

Use an executable project with one reference to Core.

**Step 3: Create the Client project**

Use an executable project with one reference to Core and one centrally versioned
`MonoGame.Framework.DesktopGL` package reference.

**Step 4: Create the test project**

Use a non-packable test project with references to Core, xUnit, the Visual
Studio runner, and Microsoft.NET.Test.Sdk.

**Step 5: Verify the dependency graph**

Run: `dotnet sln AutonomousArena.slnx list`, followed by
`dotnet reference list --project <project.csproj>` for each project.
Expected: all four projects are solution members; Core has no project
references; all other projects point only to Core.

### Task 3: Write deterministic primitive tests

**Files:**
- Create: `tests/AutonomousArena.Core.Tests/DeterministicRandomTests.cs`
- Create: `tests/AutonomousArena.Core.Tests/FixedPointTests.cs`
- Create: `src/AutonomousArena.Core/Determinism/SplitMix64.cs`
- Create: `src/AutonomousArena.Core/Mathematics/FixedPoint.cs`

**Step 1: Write failing PRNG test vectors**

Assert the first known outputs for a fixed seed and assert that a zero seed is
normalized deterministically.

**Step 2: Run the focused tests**

Run: `dotnet test tests/AutonomousArena.Core.Tests --filter FullyQualifiedName~DeterministicRandomTests`
  
Expected: FAIL because the types do not exist.

**Step 3: Implement the minimum primitives**

Implement a versioned SplitMix64 generator and a checked integer fixed-point
value with explicit scale, comparison, addition, subtraction, multiplication,
and squared-distance helpers.

**Step 4: Re-run the focused tests**

Expected: PASS.

### Task 4: Write scenario and state validation tests

**Files:**
- Create: `tests/AutonomousArena.Core.Tests/ScenarioTests.cs`
- Create: `src/AutonomousArena.Core/Simulation/Scenario.cs`
- Create: `src/AutonomousArena.Core/Simulation/SimulationOptions.cs`
- Create: `src/AutonomousArena.Core/Simulation/Faction.cs`
- Create: `src/AutonomousArena.Core/Simulation/AgentState.cs`
- Create: `src/AutonomousArena.Core/Simulation/AgentIntent.cs`

**Step 1: Write failing validation tests**

Cover invalid map bounds, nonpositive faction sizes, invalid health/damage/range,
and overflow-risking values. Cover a valid default 100-versus-100 scenario.

**Step 2: Run the focused tests**

Expected: FAIL because the models do not exist.

**Step 3: Implement immutable configuration and mutable state**

Keep units explicit, entity IDs monotonic, factions restricted to two in v0.1,
and reject invalid data before allocating match state.

**Step 4: Re-run the focused tests**

Expected: PASS.

### Task 5: Implement the fixed-tick battle through regression tests

**Files:**
- Create: `tests/AutonomousArena.Core.Tests/BattleSimulationTests.cs`
- Create: `tests/AutonomousArena.Core.Tests/DeterminismTests.cs`
- Create: `src/AutonomousArena.Core/Simulation/BattleSimulation.cs`
- Create: `src/AutonomousArena.Core/Simulation/BattleEvent.cs`
- Create: `src/AutonomousArena.Core/Simulation/BattleSnapshot.cs`
- Create: `src/AutonomousArena.Core/Simulation/BattleSummary.cs`
- Create: `src/AutonomousArena.Core/Determinism/StateHasher.cs`

**Step 1: Write failing behavior tests**

Cover:

- stable nearest-target selection with entity-ID tie-break;
- fixed-tick approach movement;
- cooldown-gated hitscan attacks;
- simultaneous damage and mutual death;
- one-time victory emission;
- dead agents never acting;
- two same-seed runs producing identical events and final hashes.

**Step 2: Run the focused tests**

Expected: FAIL because the simulation does not exist.

**Step 3: Implement the explicit tick pipeline**

Use stable indexed storage and ascending entity-ID order. Gather movement and
attack proposals before committing them. Accumulate damage before resolving
deaths. Emit monotonically sequenced events. Compute a stable hash from
authoritative fields only.

**Step 4: Run the focused tests**

Expected: PASS.

**Step 5: Run all Core tests**

Run: `dotnet test tests/AutonomousArena.Core.Tests -c Release`  
Expected: PASS without opening a window or requiring graphics.

### Task 6: Add the headless workload runner

**Files:**
- Create: `src/AutonomousArena.Headless/Program.cs`

**Step 1: Add a CLI contract test through process execution if needed**

The runner accepts `--agents`, `--ticks`, and `--seed`; invalid values return a
nonzero exit code.

**Step 2: Implement the runner**

Run two independent simulations, measure elapsed time and tick percentiles,
compare event/final-state hashes, and emit a concise JSON result containing
termination status, winner, survivors, determinism status, and measurements.

**Step 3: Verify 200 agents**

Run:
`dotnet run --project src/AutonomousArena.Headless -c Release -- --agents 200 --ticks 10000 --seed 1`

Expected: exit code 0 and `"deterministic": true`.

**Step 4: Record 500 agents**

Run the same command with `--agents 500`. Treat performance as evidence, not a
hard v0.1 gate.

### Task 7: Build the MonoGame spectator client

**Files:**
- Create: `src/AutonomousArena.Client/Program.cs`
- Create: `src/AutonomousArena.Client/ArenaGame.cs`
- Create: `src/AutonomousArena.Client/SpectatorCamera.cs`
- Create: `src/AutonomousArena.Client/Content/Content.mgcb`

**Step 1: Implement guarded startup**

Construct `ArenaGame`, run it, print initialization failures to standard error,
and set a nonzero process exit code.

**Step 2: Implement the game loop**

Advance the core at a fixed logical rate, with pause and 1x/2x/4x speed.
Rendering continues while paused.

**Step 3: Implement batched rendering**

Create a one-pixel texture at runtime and render every living agent in one
sprite batch using faction colors. Do not create one engine node or texture per
agent.

**Step 4: Implement spectator controls**

Support WASD/arrows, mouse-wheel zoom, Space, `1`, `2`, `4`, `R`, and Escape.
Show counts, seed, tick, speed, hovered agent state, and winner in the window
title.

**Step 5: Verify**

Run: `dotnet build src/AutonomousArena.Client -c Release`  
Expected: PASS.

Run interactively when graphics access is available and verify the documented
controls and clean exit.

### Task 8: Add safe developer workflows and CI

**Files:**
- Create: `scripts/bootstrap.ps1`
- Create: `scripts/doctor.ps1`
- Create: `scripts/build.ps1`
- Create: `scripts/test.ps1`
- Create: `scripts/run.ps1`
- Create: `scripts/benchmark.ps1`
- Create: `scripts/format.ps1`
- Create: `scripts/package.ps1`
- Create: `.github/workflows/ci.yml`

**Step 1: Implement doctor and bootstrap**

Detect the pinned SDK, Git, optional Git LFS, Windows x64, restore access, and
MonoGame package availability. Bootstrap installs nothing unless explicitly
requested and otherwise gives the exact official install command.

**Step 2: Add one-command operations**

Each script enables strict mode, resolves the repository root from its own
location, fails on nonzero native exit codes, and performs one named operation.
No script deletes untracked or user files.

**Step 3: Add Windows CI**

Use current stable action majors, install SDK 10.0.302, restore once, build
Release without restore, test without build, verify formatting, run the
200-agent workload, and publish a win-x64 artifact on manual/tagged runs.

**Step 4: Verify locally**

Run doctor, build, test, format, benchmark, and package scripts.

### Task 9: Write operating documentation and agent evidence

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
- Create: `docs/architecture/platform-decision.md`
- Create: `docs/repository-audit.md`
- Create: `docs/dependency-inventory.md`
- Create: `docs/platform-support-matrix.md`
- Create: `docs/development/prerequisites.md`
- Create: `docs/development/getting-started.md`
- Create: `docs/development/testing.md`
- Create: `docs/repository-readiness-report.md`
- Modify: `README.md`

**Step 1: Write the enhanced execution prompt**

Convert the original 17-role catalog into a stage-gated orchestrator prompt with
locked decisions, bounded scope, explicit ownership, objective gates,
non-destructive rules, and a per-agent Markdown evidence contract.

**Step 2: Write each agent report**

Record objective, inputs, work, files, verification, status, limitations, and
next action. Do not claim work or validation that did not occur.

**Step 3: Write operator documentation**

Document exact bootstrap, build, test, run, benchmark, and package commands plus
the v0.1 platform and dependency limitations.

**Step 4: Update the README**

Lead with the runnable outcome and exact commands, then link to design, standards,
agent reports, and readiness status.

### Task 10: Run the evaluator–optimizer loop and final review

**Files:**
- Modify only files directly responsible for a failing targeted check.
- Finalize: `docs/repository-readiness-report.md`
- Finalize: `docs/agents/16-repository-readiness-validation.md`
- Finalize: `docs/agents/17-technical-review-handoff.md`

**Step 1: Run focused verification**

Run failing test groups individually and classify each failure as code, test,
environment, dependency, assumption, unrelated, or flaky.

**Step 2: Run integration verification**

Run:

1. `./scripts/doctor.ps1`
2. `./scripts/build.ps1 -Configuration Release`
3. `./scripts/test.ps1 -Configuration Release`
4. `./scripts/format.ps1 -Verify`
5. `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000`
6. `./scripts/package.ps1 -Runtime win-x64`

**Step 3: Attempt interactive runtime smoke**

Start the client only in an interactive session. Verify window creation,
rendering, input, and clean exit. If automation cannot safely validate the
interactive window, record that limitation without weakening other gates.

**Step 4: Inspect the whole diff**

Confirm there are no unrelated changes, secret values, generated build outputs,
disabled checks, placeholder logic, or orphaned files.

**Step 5: Independent severity review**

Resolve every Critical or High finding. Record Medium and Low findings only when
they are outside the immediate scope.

**Step 6: Commit**

Use focused Conventional Commit messages and leave the branch reviewable.
