# Orchestrated Arena and Menu Design

**Date:** 2026-07-26  
**Status:** Approved  
**Decision owner:** Repository owner

## Goal

Complete the existing Autonomous Arena foundation through coordinated,
non-overlapping agent work and deliver a runnable MonoGame client with a simple
in-game control menu.

## Approved menu behavior

The first menu is functional UI with intentionally plain visuals. It is not a
final art or UX pass.

- Escape opens and closes a centered overlay.
- Opening the overlay pauses simulation advancement.
- The overlay contains `Play`, `Pause`, and `Exit Game`.
- `Play` resumes the simulation and closes the overlay.
- `Pause` keeps the simulation paused and leaves the overlay visible.
- `Exit Game` closes the MonoGame client cleanly.
- Space continues to toggle play/pause when the overlay is closed.
- The window close button and Escape behavior remain safe exit paths.
- Mouse activation is required; keyboard focus/navigation is included when it
  can be implemented without adding a UI framework.

The menu never mutates authoritative battle state. It changes only the client
scheduler and application lifecycle.

## UI implementation

The client draws a translucent full-window backdrop, a centered panel, and
three button rectangles in the existing MonoGame sprite batch. A small
SpriteFont compiled by the repository content pipeline provides button labels
and diagnostic text. Button hit boxes are calculated in screen coordinates and
remain stable during camera pan or zoom.

Buttons expose normal, hovered, pressed, focused, and disabled colors. UI input
is edge-triggered so holding a mouse button or key cannot activate a command
multiple times.

## Orchestration model

### Simulation Agent

**Objective:** finish deterministic scenario, battle, event, hashing, tests, and
headless execution.

**Owned files or subsystem:**

- `src/AutonomousArena.Core/**`
- `src/AutonomousArena.Headless/**`
- `tests/AutonomousArena.Core.Tests/**`

**Expected output:** buildable deterministic 200-agent combat, regression tests,
and a machine-readable headless result.

**Success condition:** focused and complete Core tests pass; two independent
same-seed runs produce identical results.

**Prohibited scope:** Client, scripts, CI, README, and agent documentation.

### Client and Menu Agent

**Objective:** build the MonoGame renderer, spectator controls, and approved
Play/Pause/Exit overlay.

**Owned files or subsystem:**

- `src/AutonomousArena.Client/**`

**Expected output:** buildable client that consumes Core snapshots, renders
batched dots, supports camera/speed/reset controls, and implements the approved
menu.

**Success condition:** Client Release build and content compilation pass; menu
hit-testing and state transitions are testable without moving authoritative
logic into the client.

**Dependencies:** may consume the documented Core contracts; coordinates any
contract mismatch with the orchestrator instead of editing Core.

**Prohibited scope:** Core, Headless, tests outside Client-owned helpers,
scripts, CI, and documentation.

### Delivery and Documentation Agent

**Objective:** add safe one-command workflows, Windows CI, onboarding, and the
17 role-specific evidence reports.

**Owned files or subsystem:**

- `scripts/**`
- `.github/**`
- `README.md`
- `docs/**`, excluding active plan files owned by the orchestrator
- root dependency-quality configuration assigned explicitly by the orchestrator

**Expected output:** bootstrap/build/test/run/benchmark/format/package/verify
workflows, CI, launch instructions, and evidence-backed agent reports.

**Success condition:** scripts use the pinned repository toolchain, avoid
destructive Git operations, and documentation distinguishes completed,
conditional, and deferred validation.

**Prohibited scope:** runtime and simulation source files.

## Integration order

1. The orchestrator commits this design and the execution plan.
2. Simulation, Client/Menu, and Delivery/Docs work in parallel.
3. Core public-contract changes are integrated first.
4. Client compilation is reconciled against the final Core contract.
5. Delivery scripts and reports are updated against commands that actually
   pass.
6. The orchestrator reviews the entire diff and resolves all Critical and High
   findings.

Each writable path has one active owner. The orchestrator is the only party
that stages or commits integrated work.

## Verification

- Locked restore.
- Release build of the complete solution.
- Core unit and determinism tests.
- Two same-seed 200-agent headless runs.
- Content pipeline and Client Release build.
- Formatting verification.
- Dependency vulnerability report.
- Windows x64 publish.
- Interactive client smoke:
  - window opens;
  - dots render and advance;
  - Escape opens the menu;
  - Pause stops logical advancement;
  - Play resumes it;
  - Exit Game closes with exit code zero.

If interactive UI automation is unavailable, the non-graphical gates remain
objective and the runtime smoke is reported as conditional rather than guessed.

