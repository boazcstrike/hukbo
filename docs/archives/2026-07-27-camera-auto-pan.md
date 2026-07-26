# Camera Auto-Pan Plan

> **Archived: reference only.** This plan is complete and deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Design: [2026-07-27-camera-auto-pan-design.md](2026-07-27-camera-auto-pan-design.md)

## Tasks

1. [x] Add `src/Hukbo.Client/ArenaAutoPan.cs` with pure `internal static`
       helpers: `IsFighting`, `GetWorldPosition`, `HasFighterInside`,
       `TryResolveTarget`, `AdvanceCenter`, plus tuning constants.
2. [x] Add `ArenaAutoPanController` with the idle/panning state machine, the
       manual-override timer, and a single `Update` entry point that returns the
       new camera centre.
3. [x] Extend `SpectatorCamera`: public `Center`, `MoveCenterTo`,
       `GetVisibleHalfExtents(Rectangle)`, and make `Update` return whether
       manual pan input moved the camera this frame.
4. [x] Wire the controller into `ArenaGame.UpdateSpectatorInput`, suppressed
       while the menu is visible and while the match summary is showing.
5. [x] Add `tests/Hukbo.Client.Tests/ArenaAutoPanTests.cs` covering the helper
       decisions and the controller state transitions.
6. [x] Add a manual smoke-checklist row to `docs/development/testing.md`, left
       `PENDING`.
7. [x] Run the canonical gate and paste the real output.

## Verification Criteria

- `./scripts/verify.ps1` passes: format verification, Release build with
  `TreatWarningsAsErrors`, Core and Client tests, and the 200-agent /
  10,000-tick / seed-1 headless determinism workload.
- The headless state hash and event hash are unchanged from the recorded seed-1
  baseline. This change touches only `Hukbo.Client`; a moved hash means
  something is wrong.
- New tests construct no `ArenaGame`, `GraphicsDevice`, `SpriteBatch`, or
  window, keeping the repo's zero-occurrence rule intact.
- The smoke row stays `PENDING` until a person confirms the behaviour at an
  interactive desktop.

## Test Coverage

Helper decisions:

- A living `Attacking` agent counts as fighting; `Idle`, `Moving`, `Dead`, and
  dead-but-`Attacking` agents do not.
- `HasFighterInside` is true only for a fighting agent inside the rectangle, and
  is false when the only fighting agent sits outside it.
- `TryResolveTarget` returns nothing when no one is fighting.
- `TryResolveTarget` picks the melee nearest the camera centre, not the centroid
  of two distant melees.
- Equidistant anchors break the tie on the lower `EntityId`.
- The target is the centroid of the anchor's cluster, excluding fighters beyond
  `ClusterRadius`.
- `AdvanceCenter` never overshoots and returns the target exactly when the step
  covers the remaining distance.

Controller transitions:

- Stays put when a fighter is already on screen.
- Engages and moves toward the fight when none is on screen.
- Stops once a fighter is inside the inner rectangle, not merely inside the full
  rectangle.
- Manual pan input disengages immediately and blocks re-engagement for the
  override window, then allows it again once the window expires.
- Does nothing when the agent list has no fighters at all.
