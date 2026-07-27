# Hukbo Foundation Design

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

**Date:** 2026-07-26  
**Status:** Approved  
**Decision owner:** Repository owner

## Goal

Create the smallest production-safe foundation that proves a deterministic,
offline, 2D spectator battle with 200 autonomous combatants and a MonoGame
DesktopGL presentation shell on Windows x64.

## Approved product boundary

- Disposable, seeded matches rather than a persistent world.
- Two mutually hostile factions.
- 200 simultaneous combatants as the first acceptance target.
- Readability and explainability before detailed physiology.
- Player interaction is spectator-only: camera, pause, speed, reset, and exit.
- Colored dots are the v0.1 visual identity and permanent diagnostic view.
- Networking, persistence, terrain, pathfinding, content authoring, packaging
  stores, and non-Windows support are deferred.

## Evaluated approaches

### MonoGame DesktopGL with a plain .NET core — selected

This is the smallest code-first rendering surface. It gives direct batched
drawing and input control without letting engine objects own simulation state.
The trade-off is that camera and diagnostic UI must be built in code.

### Godot C# with a plain .NET core

Godot offers stronger editor and UI workflows, but it adds an editor/runtime
boundary before the battle simulation is proven. It remains a viable later
shell if authoring needs outweigh the value of a small runtime.

### Custom SDL/OpenGL

A custom shell offers maximum control but duplicates platform, input, audio,
windowing, and packaging work that MonoGame already supplies. It is rejected
for the first milestone.

## Technology decisions

- .NET SDK 10.0.302, pinned in `global.json`.
- `net10.0` for all projects.
- MonoGame Framework DesktopGL 3.8.5.
- Windows x64 as the only supported v0.1 developer and runtime platform.
- Central NuGet package management.
- VSTest with xUnit for the initial test platform.
- GitHub Actions on `windows-latest`.
- No content-pipeline dependency for the first scene; the client creates its
  one-pixel dot texture at runtime.

## Repository architecture

```text
Hukbo.Client
        |
        v
Hukbo.Core <--- Hukbo.Headless
        ^
        |
Hukbo.Core.Tests
```

`Hukbo.Core` owns all authoritative gameplay state and has no
MonoGame, filesystem, network, GPU, audio, wall-clock, or window dependency.

`Hukbo.Client` owns the MonoGame loop, batched dots, camera,
keyboard/mouse controls, interpolation-ready snapshots, and diagnostic window
title. It reads completed core state and never feeds presentation coordinates
back into gameplay.

`Hukbo.Headless` runs deterministic smoke workloads and emits
machine-readable results without opening a window.

`Hukbo.Core.Tests` proves deterministic behavior, stable ordering,
combat invariants, and terminal outcomes without graphics hardware.

No Application, Infrastructure, Platform, generic ECS, dependency-injection,
telemetry, asset-tool, benchmark-framework, or end-to-end project is created
until a measured requirement exists.

## Simulation model and data flow

The simulation uses integer fixed-point positions, integer ticks, stable
monotonic entity IDs, a project-owned versioned PRNG, and ascending-ID commit
order. Each tick:

1. refreshes stable target selection;
2. selects approach or attack intent;
3. creates movement proposals;
4. commits movement in entity-ID order;
5. creates attack proposals;
6. applies accumulated damage simultaneously;
7. resolves deaths and victory;
8. appends ordered events;
9. exposes a read-only render snapshot and stable state hash.

The initial model contains scenario configuration, faction, entity ID,
position, movement speed, perception range, hit points, weapon range, cooldown,
damage, target, intent, and lifecycle state. No rigid-body physics, projectile
entities, ammunition, morale, cover, or pathfinding is included.

## Runtime behavior

The client opens a 1280x720 resizable window and draws every living combatant as
a colored dot in one sprite batch. Controls:

- Arrow keys or WASD: pan.
- Mouse wheel: zoom.
- Space: pause/resume.
- `1`, `2`, `4`: simulation speed.
- `R`: replay the same seed.
- Escape: exit cleanly.

The window title exposes the seed, tick, speed, living counts, hovered entity
state, and winner. Initialization errors are written to standard error and
return a nonzero process exit code.

## Error handling

- Scenario validation rejects invalid bounds, faction sizes, numeric ranges,
  or overflow risks before creating state.
- Headless failures return a nonzero exit code and a concise error.
- Bootstrap and doctor scripts distinguish missing required tools from optional
  tools and never print secrets.
- Scripts are idempotent and do not delete the working tree.
- Runtime smoke failure caused by unavailable graphics/window access is
  classified separately from build or simulation failure.

## Verification design

Focused tests cover scenario validation, PRNG test vectors, distance/target
tie-breaking, fixed-tick movement, cooldown/damage, simultaneous death, victory,
and identical same-seed event/state hashes.

Integration checks run a 200-agent headless match, confirm termination or the
documented tick limit, and compare two independent runs. A 500-agent workload is
reported as a stress result rather than a release promise.

Repository checks are:

1. prerequisite doctor;
2. restore;
3. Release build;
4. unit and integration tests;
5. formatting verification;
6. 200-agent headless smoke/benchmark;
7. client build;
8. client runtime smoke when an interactive graphics session is available;
9. Windows x64 publish.

## Agent evidence contract

`docs/agents/` contains one numbered Markdown report for each of the 17 roles in
the source prompt. Every report records:

- objective and owned scope;
- inputs inspected;
- decisions and work completed;
- created or affected files;
- verification evidence;
- status: `COMPLETE`, `CONDITIONALLY COMPLETE`, or `DEFERRED`;
- risks, limitations, and next action.

These reports are evidence records, not claims that 17 independent long-running
processes modified the repository. Roles may be grouped into bounded
investigations, while file ownership and final integration remain with the
orchestrator.

## Acceptance criteria

- A clean restore and Release build succeed with .NET 10.0.302.
- Core tests pass without GPU, audio, window focus, or internet after restore.
- Two independent same-seed headless runs produce identical ordered event and
  final-state hashes.
- The 200-agent workload completes or reaches a documented stalemate limit
  without invariant failure.
- The client builds, opens a window in an interactive Windows session, draws
  colored agents, accepts the documented controls, displays a winner, and exits
  cleanly.
- The final diff contains only foundation, verification, operation, and agent
  evidence files required by this design.
