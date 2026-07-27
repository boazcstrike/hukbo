# Hukbo

Hukbo is a deterministic, offline, 2D spectator battle built with
.NET 10 and MonoGame DesktopGL. The first milestone simulates two autonomous
factions, renders combatants as colored dots, and exposes Play, Pause, speed,
camera, persistent agent inspection, a bounded battle-event log, session
scoring and reset controls, and clean exit controls.

Repository: [boazcstrike/hukbo](https://github.com/boazcstrike/hukbo)

## Direction

Hukbo is being built toward a 4X strategy game about warfare in the
pre-colonial and early-contact Philippines. The milestone described below is the
tactical layer of that game: a deterministic battle that a future campaign layer
will configure and score.

| Layer | Status |
| --- | --- |
| Tactical battle: autonomous factions, spectator controls, deterministic replay | shipped as v0.1 |
| Campaign: explore islands, expand polities, exploit trade, exterminate rivals | not started |

The campaign layer is gated behind Gate 3 in
[Simulation game standards](SIMULATION-GAME-STANDARDS.md): scenario, snapshot,
replay verification, save and resume equivalence, and a reported 500-agent
stress result. Until that gate passes, no campaign, economy, or diplomacy state
enters `Hukbo.Core`. The campaign layer will be a separate project that produces
`Scenario` values and consumes `BattleOutcome`.

Historical material follows the confidence labels in
[Historical weapons research](docs/research/HISTORICAL_1500s_WEAPONS.md).
Player-facing labels stay plain descriptors, and specific cultural
identifications are carried as provisional evidence notes rather than
unqualified claims.

## Run the game

Requirements: Windows x64, PowerShell 7, Git, and .NET SDK 10.0.302.

```powershell
./scripts/bootstrap.ps1
./scripts/run.ps1
```

The fallback launch command is:

```powershell
dotnet run --project src/Hukbo.Client -c Release
```

Controls:

| Input | Action |
| --- | --- |
| Escape | Open or close the control menu |
| Play | Resume the battle; modal Play also closes the menu |
| Pause | Pause the battle; modal Pause leaves the menu visible |
| Menu | Pause the battle and open the modal menu |
| Next Round button | Score a terminal victory, advance the seed, and start paused |
| Full Reset button | Clear session wins and restore the initial paused setup |
| Exit Game button | Close the game cleanly |
| Space | Toggle play/pause while the menu is closed |
| `1`, `2`, `4` | Set simulation speed |
| `R` | Start the next round, advance the deterministic seed, and pause |
| `Shift+R` | Reset to 0-0, seed 1, 1x, camera fit, and a paused match |
| WASD or arrow keys | Pan the camera |
| Click an agent | Select it for persistent inspection |
| Click empty arena | Clear the current selection |
| Mouse wheel over arena | Zoom |
| Mouse wheel over event log | Scroll battle-event history |

The game starts paused. Play, Pause, and Menu remain visible above the arena.
The selected-agent inspector retains a dead agent's final authoritative state.
The event feed retains at most 200 ordered events, and a terminal summary shows
the winner, survivors, tick, simulated duration, seed, and Next Round.
Opening the menu pauses logical simulation advancement. The modal contains Next
Round, Full Reset, and Exit Game.

The HUD and window title show session-local wins for Team A (Blue) and Team B
(Red). Next Round records exactly one win for the outgoing terminal victor,
advances to a distinct deterministic seed, clears disposable presentation
state, and starts paused while preserving the score, speed, and camera. An
abandoned ongoing round or a draw does not add a win, but Next Round still
advances the seed. Full Reset clears both win totals, restores seed 1 and 1x
speed, fits the camera, clears disposable presentation state, and starts
paused.

## Verify the repository

```powershell
./scripts/verify.ps1
```

That command performs a locked restore, formatting check, Release build, Core
and GPU-independent Client tests, and a deterministic 200-agent headless
workload. Package the self-contained Windows client with:

```powershell
./scripts/package.ps1 -Runtime win-x64
```

The self-contained output is written to
`artifacts/packages/client-win-x64/`.

Verification is intentionally local-only. The repository does not use GitHub
Actions or another hosted CI service. Run the canonical gate before integrating
changes and record interactive game behavior with the manual checklist in
`docs/development/testing.md`.

## Standards and research

- [Simulation game standards](SIMULATION-GAME-STANDARDS.md) — determinism
  contract, tick order, benchmark workloads, reviewer checklist
- [Research brief](RESEARCHED.md) — product direction evidence and the
  language and runtime decision
- [Historical weapons research](docs/research/HISTORICAL_1500s_WEAPONS.md) —
  sixteenth-century sources with confidence labels
- [Platform support matrix](docs/platform-support-matrix.md)
- [Agent instructions](CLAUDE.md) — the contract coding agents work under

## Documentation

- [Getting started](docs/development/getting-started.md)
- [Testing and verification](docs/development/testing.md)
- [Platform decision](docs/architecture/platform-decision.md)
- [Agent-role evidence index](docs/agents/README.md)
- [Foundation design](docs/archives/2026-07-26/2026-07-26-hukbo-foundation-design.md)
- [Orchestration and menu design](docs/archives/2026-07-26/2026-07-26-hukbo-menu-design.md)
- [Approved spectator-clarity design](docs/plans/2026-07-26-spectator-clarity-design.md)
- [Active spectator-clarity plan](docs/plans/2026-07-26-spectator-clarity.md)
- [Round scoring, reset, and memory plan](docs/archives/2026-07-26/2026-07-26-round-scoring-reset-memory.md)

v0.1 supports Windows x64 only. Networking, persistence, pathfinding, store
distribution, and non-Windows packaging are intentionally deferred.
