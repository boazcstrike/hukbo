# Enhanced Autonomous Arena Execution Prompt

> **Historical foundation prompt:** The hosted-CI requirements below were
> superseded by the repository owner's local-only verification decision on
> 2026-07-26. Use
> `docs/plans/2026-07-26-spectator-clarity.md` for the next execution phase.

Use this prompt to coordinate the initial Autonomous Arena milestone. It
replaces a strictly serial 17-role checklist with stage gates, explicit file
ownership, and evidence-based completion.

## Objective

Deliver the smallest production-safe proof of an offline deterministic 2D arena:
two factions, 200 autonomous combatants, fixed-tick combat, a headless
determinism runner, and a MonoGame spectator client with camera, speed, reset,
Play, Pause, and Exit Game controls.

## Locked decisions

- Windows x64 only for v0.1.
- .NET SDK 10.0.302 and C# 14.
- MonoGame DesktopGL and Content Builder 3.8.5.
- `AutonomousArena.Core` is the only authoritative gameplay layer.
- Offline, single-player, disposable seeded matches.
- Two factions and 200 total combatants for acceptance.
- Fixed-point/integer state, project-owned seeded PRNG, stable entity ordering.
- Colored dots and a plain menu are intentional first-milestone visuals.
- Networking, persistence, terrain, pathfinding, final art, stores, and
  non-Windows support are deferred.

Do not reopen a locked decision unless source evidence proves it blocks an
acceptance criterion.

## Safety and scope rules

- Preserve repository-owner files and unrelated work.
- Never reset, clean, stash, or delete the working tree.
- Do not install machine software without explicit user action.
- Never print or commit credentials.
- Prefer the smallest complete change and existing repository conventions.
- Do not add speculative layers, packages, interfaces, or projects.
- Treat a knowledge graph as an index; verify changes against current source.
- Classify environmental failures separately from implementation defects.
- Do not claim a test, CI job, package, or runtime smoke passed unless it ran.

## Orchestration

Use the minimum three non-overlapping implementation workers when concurrency
is available:

### Simulation and Headless owner

- Owns `src/AutonomousArena.Core/**`,
  `src/AutonomousArena.Headless/**`, and
  `tests/AutonomousArena.Core.Tests/**`.
- Implements scenario validation, battle stages, ordered events, state hashing,
  deterministic replay comparison, JSON reporting, and regression tests.
- Must not edit Client, scripts, CI, or documentation.

### Client and Menu owner

- Owns `src/AutonomousArena.Client/**`.
- Consumes Core read-only views; never duplicates or mutates simulation models.
- Implements fixed scheduling, batched dots, camera/input, diagnostics, content,
  and the Escape overlay.
- Menu actions: Play resumes/closes; Pause remains visible/paused; Exit Game
  exits once. Opening the menu pauses scheduling.
- Must not edit Core, Headless, tests, scripts, CI, or documentation.

### Delivery and Evidence owner

- Owns `scripts/**`, `.github/**`, `README.md`, and non-plan operating/evidence
  documentation.
- Implements non-destructive workflows, immutable Windows CI, exact onboarding,
  and one report for each original role.
- Records source-dependent results as planned or conditional until integration.
- Must not edit runtime, simulation, tests, or active plans.

The orchestrator owns root build configuration, interface reconciliation,
integration order, final verification, and final evidence updates.

## Stage gates

### Gate 1: Foundation

- SDK and packages are pinned.
- Solution references point inward to Core.
- Locked restore passes.
- Core has no engine/package dependency.

### Gate 2: Deterministic simulation

- Validation tests cover invalid sizes/ranges and arithmetic risk.
- Stable targeting, movement, cooldown, simultaneous damage/death, victory, and
  dead-agent inactivity are tested.
- Same-seed simulations produce equal ordered events and state hashes.
- Core tests pass without GPU, window, audio, or network after restore.

### Gate 3: Headless workload

- `--agents`, `--ticks`, `--seed`, and optional `--output` validate input.
- 200-agent/10,000-tick seed-1 run exits zero with `deterministic: true`.
- JSON records environment, timing, allocations, outcome, survivors, event
  hash, and state hash.

### Gate 4: Client and menu

- Release Client and content build pass.
- Client draws all live agents in one sprite batch.
- Space, speed, reset, camera, and zoom controls work.
- Escape opens/closes the menu; opening pauses.
- Play, Pause, Exit Game, window close, and guarded startup behave as specified.

### Gate 5: Delivery

- `doctor`, `bootstrap`, `build`, `test`, `run`, `benchmark`, `format`,
  `package`, and `verify` are repository-relative and non-destructive.
- Windows CI uses minimum permissions and full action commit SHAs.
- Windows packaging is an explicit self-contained `win-x64` publish.
- README contains exact launch and control instructions.
- All evidence reports use actual status and limitations.

### Gate 6: Final evaluation

Run:

```powershell
./scripts/verify.ps1
./scripts/package.ps1 -Runtime win-x64
```

Perform the interactive smoke separately. Inspect the whole diff, resolve every
Critical/High finding, and distinguish unrun or environmental checks.

## Evidence contract

Each numbered report in `docs/agents/` must contain:

- scope;
- inputs inspected;
- decisions/work;
- changed or relevant files;
- exact verification and result;
- `COMPLETE`, `CONDITIONALLY COMPLETE`, or `DEFERRED`;
- limitations;
- next action.

Reports are role evidence, not a claim that 17 independent processes changed
the repository. The repository is `READY` only after all required
non-graphical gates, Windows package, CI, and interactive client smoke have
evidence; otherwise use `CONDITIONALLY READY` or `NOT READY`.
