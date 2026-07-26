# Autonomous Arena

Autonomous Arena is a deterministic, offline, 2D spectator battle built with
.NET 10 and MonoGame DesktopGL. The first milestone simulates two autonomous
factions, renders combatants as colored dots, and exposes Play, Pause, speed,
reset, camera, and exit controls.

## Run the game

Requirements: Windows x64, PowerShell 7, Git, and .NET SDK 10.0.302.

```powershell
./scripts/bootstrap.ps1
./scripts/run.ps1
```

The fallback launch command is:

```powershell
dotnet run --project src/AutonomousArena.Client -c Release
```

Controls:

| Input | Action |
| --- | --- |
| Escape | Open or close the control menu |
| Play button | Resume the battle and close the menu |
| Pause button | Keep the battle paused with the menu visible |
| Exit Game button | Close the game cleanly |
| Space | Toggle play/pause while the menu is closed |
| `1`, `2`, `4` | Set simulation speed |
| `R` | Reset to the same seed |
| WASD or arrow keys | Pan the camera |
| Mouse wheel | Zoom |

Opening the menu pauses logical simulation advancement. The menu is intentionally
plain UI for the first milestone.

## Verify the repository

```powershell
./scripts/verify.ps1
```

That command performs a locked restore, formatting check, Release build, Core
tests, and deterministic 200-agent headless workload. Package the
self-contained Windows client with:

```powershell
./scripts/package.ps1 -Runtime win-x64
```

The self-contained output is written to
`artifacts/packages/client-win-x64/`.

Verification is intentionally local-only. The repository does not use GitHub
Actions or another hosted CI service. Run the canonical gate before integrating
changes and record interactive game behavior with the manual checklist in
`docs/development/testing.md`.

## Documentation

- [Getting started](docs/development/getting-started.md)
- [Testing and verification](docs/development/testing.md)
- [Platform decision](docs/architecture/platform-decision.md)
- [Repository readiness](docs/repository-readiness-report.md)
- [Agent-role evidence index](docs/agents/README.md)
- [Foundation design](docs/plans/2026-07-26-autonomous-arena-foundation-design.md)
- [Orchestration and menu design](docs/plans/2026-07-26-orchestrated-arena-menu-design.md)
- [Approved spectator-clarity design](docs/plans/2026-07-26-spectator-clarity-design.md)
- [Next-phase orchestration plan](docs/plans/2026-07-26-spectator-clarity.md)

v0.1 supports Windows x64 only. Networking, persistence, pathfinding, store
distribution, and non-Windows packaging are intentionally deferred.
