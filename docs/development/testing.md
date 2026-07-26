# Testing and Verification

## Canonical gate

```powershell
./scripts/verify.ps1
```

The gate performs, in order:

1. prerequisite validation and locked restore;
2. formatting verification;
3. Release solution build;
4. Core and GPU-independent Client tests without rebuilding;
5. a 200-agent, 10,000-tick, seed-1 headless determinism workload.

It does not launch a window or alter authoritative game state. It never runs a
destructive Git or filesystem cleanup.

This repository intentionally uses local-only verification. There is no GitHub
Actions workflow or hosted-CI completion gate. Run the canonical gate on the
integration workstation and record its exact result.

## Focused commands

```powershell
./scripts/test.ps1 -Configuration Release
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
dotnet test tests/Hukbo.Core.Tests -c Release `
  --filter FullyQualifiedName~DeterminismTests
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/format.ps1 -Verify
```

Client presentation tests must not create an `ArenaGame`, graphics device,
sprite batch, or window. Tests must remain independent from GPU, audio
hardware, window focus, network, wall clock, `System.Random`, and platform input
types. Performance output is evidence, not a universal frame-time guarantee.

## Latest non-interactive result

The post-integration local run on 2026-07-26 recorded:

- 41/41 Client presentation and round-lifecycle tests passed;
- 45/45 Core tests passed;
- `./scripts/verify.ps1 -SkipBootstrap` passed formatting and the Release build
  with 0 warnings and 0 errors;
- the seed-1 200-agent workload ended in `Faction1Victory` at tick 235 with
  state hash `210C5EF8E7BE4D48` and event hash `CE35EDA4B2A4E5A4`;
- the same workload allocated 12,108,304 bytes, below the captured
  19,856,712-byte baseline, with both hashes unchanged;
- the seed-distribution guard for seeds 1 through 20 produced victories for
  both factions rather than a single always-winning faction;
- the 500-agent stress workload remained deterministic and ended at tick 309;
- the earlier spectator-clarity package run produced
  `artifacts/packages/client-win-x64/Hukbo.Client.exe`;
- that packaged Client opened visibly, remained responsive, showed
  `Hukbo — A 0 : 0 B — Seed 1 — Tick 0 — 1x — Paused — Ongoing`, and returned exit code 0
  after a normal window-close request;
- the earlier spectator-clarity independent review reported no Critical, High,
  Medium, or Low findings.

These results prove the non-interactive gate only. They do not change any
hands-on control, selection, event-log, scoring, or reset row below.

## Interactive smoke checklist

Run `./scripts/run.ps1` on an interactive Windows desktop. This repository uses
local-only verification: there is no hosted-CI substitute for this direct
interaction pass. Compilation, automated tests, a window-opening probe, or
synthetic input do not make a manual row pass.

### Spectator clarity smoke

Record the observed value in `Actual` and change `Status` only after performing
the interaction. Use `PASS`, `FAIL`, or `BLOCKED`; leave untouched rows
`PENDING`.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 1. Launch the game | The window opens, agents render, and the match starts paused with tick unchanged. | Not run | PENDING |
| 2. Activate Play | The always-visible Play button advances ticks; Space provides the same toggle while the modal is closed. | Not run | PENDING |
| 3. Activate Pause | The always-visible Pause button stops tick advancement and visibly indicates the paused state. | Not run | PENDING |
| 4. Open Menu | The always-visible Menu button pauses the match and opens the modal; Escape toggles that same menu behavior. | Not run | PENDING |
| 5. Exercise modal commands | Modal Play resumes and closes; modal Pause remains open and paused; Escape closes without resuming; Exit Game, which is available only in the modal, requests one clean shutdown. | Not run | PENDING |
| 6. Select an agent | A primary click on a living agent pins the inspector with ID, faction, alive/dead state, health, intent, target, and position. | Not run | PENDING |
| 7. Move away and observe death | Moving the pointer away does not clear selection; if the selected agent dies, the inspector remains pinned and shows its final `DEAD` state. | Not run | PENDING |
| 8. Check observational behavior | Selecting or inspecting an agent does not alter tick progression or the deterministic battle result; an empty-arena click clears selection and UI clicks do not click through. | Not run | PENDING |
| 9. Exercise event-log scrolling | At 1x and 4x, events remain ordered without duplicates and retain at most 200 rows. The wheel scrolls only the log while the pointer is over it and does not zoom the arena; new events do not steal an upward scroll position; returning to the bottom reveals the newest events; over the arena, the wheel zooms. | Not run | PENDING |
| 10. Reach a terminal outcome | The match pauses and the summary winner, both survivor counts, terminal tick, simulated duration, and seed match the final status and visible arena state; the summary offers Next Round. | Not run | PENDING |
| 11. Check score timing and team mapping | Team A is Blue/faction 0 and Team B is Red/faction 1. Reaching a victory does not change the score immediately; choosing Next Round adds exactly one win to that completed round's winner. Starting the next round after a draw or while the current round is ongoing adds no win. | Not run | PENDING |
| 12. Exercise ordinary Next Round | `R`, modal Next Round, and summary Next Round each preserve the score, speed, and camera; clear selection, event history, scroll state, and summary; and leave the fresh round paused. | Not run | PENDING |
| 13. Check seed progression | Each Next Round changes the seed to a distinct deterministic value. After Full Reset, repeating the same Next Round sequence produces the same seed sequence. | Not run | PENDING |
| 14. Exercise Full Reset | After changing the score, speed, and camera, press `Shift+R`; both win totals become 0, seed returns to 1, speed returns to 1x, the camera fits the arena, disposable UI state clears, and the fresh round is paused. Change state again and confirm modal Full Reset has the same result. | Not run | PENDING |
| 15. Close the window | The operating-system close button exits the process once with exit code 0. | Not run | PENDING |

For round scoring, record Team A (Blue) and Team B (Red) totals before and after
each command together with the outgoing outcome and old/new seeds. Next Round
scores only a terminal victory and always advances the deterministic seed.
Full Reset never scores the outgoing round.

## Failure classification

Classify failures as implementation, test, environment/dependency, pre-existing,
incorrect assumption, unrelated, or flaky. Make the narrowest correction, rerun
the focused check, and expand only after it passes.
