# 18 — Spectator Clarity and Round Lifecycle

## Scope

Add spectator-facing presentation without changing authoritative gameplay:
persistent agent selection and inspection, a bounded event feed, always-visible
playback controls, a terminal summary, session-local win scoring, deterministic
next rounds, and a full session reset. Record automated, packaging, review, and
direct Windows evidence without treating one evidence type as a substitute for
another.

## Inputs inspected

- `AGENTS.md`.
- The approved spectator-clarity design and implementation plan.
- The round-scoring, reset, and memory implementation plan.
- Current Client, Core simulation-view, local-script, test, and operating
  documentation surfaces.
- The previous foundation readiness and role evidence.

## Architecture boundary

`Hukbo.Core` remains authoritative for agents, ticks, events, outcomes,
scenario seed, and tick rate. GPU-independent Client presentation state reads
the current authoritative views and owns only disposable UI state. MonoGame UI
components render and return commands. `ArenaGame` alone coordinates input
priority, fixed-tick advancement, event ingestion, round reset, and rendering.

Selection, event-feed history, scroll position, playback state, and match
summary are not gameplay state. `MatchSeries` owns session-local Team A (Blue)
and Team B (Red) wins plus deterministic seed progression. Next Round and Full
Reset each create a fresh simulation without altering Core event ordering,
hashes, or battle outcomes.

## Work completed by component

| Component | Intended result | Evidence status |
| --- | --- | --- |
| Client presentation contracts | Playback, selection, event-feed, summary, round scoring, seed progression, and reset coordination remain GPU-independent. | Implemented; 41/41 Client tests passed |
| Client UI components | Compact controls, inspector, event log, summary, and shared button behavior render without mutating Core. | Implemented; Release build passed |
| `ArenaGame` integration | UI consumes pointer input before arena handling; every advanced tick feeds events; Next Round and Full Reset clear disposable state and pause. | Implemented; canonical build/tests passed |
| Round lifecycle | Terminal victories score Team A/Blue or Team B/Red on Next Round; ongoing/draw rounds do not score; deterministic seed progression and full reset remain Client-owned. | Implemented; focused lifecycle tests passed |
| Camera integration | Arena transforms use the assigned arena-content rectangle so fixed UI panels do not distort selection or camera behavior. | Implemented; canonical build passed |
| Operating documentation | Local-only commands, control semantics, and direct smoke table are recorded. | Complete; manual results remain pending |

## Automated verification

| Check | Command or evidence | Result |
| --- | --- | --- |
| Focused Client presentation tests | `dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release` | PASS — 41/41 |
| Match-series lifecycle | `dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter FullyQualifiedName~MatchSeriesTests` | PASS |
| Repository tests | `./scripts/test.ps1 -Configuration Release` | PASS — Core 45/45; Client 41/41 |
| Canonical local gate | `./scripts/verify.ps1 -SkipBootstrap` | PASS — formatting, Release build, both test projects, deterministic workload |
| Release solution build | Canonical gate | PASS — 0 warnings, 0 errors |
| Quiet-tick allocation guard | Core allocation regression and seed-1 workload | PASS |
| 500-agent stress | Local deterministic workload | PASS — deterministic terminal tick 309 |
| Formatting and final diff | Canonical gate and `git diff --check` | PASS |

Automated checks do not prove mouse, keyboard, window-focus, SDL input, or
visual layout behavior.

## Manual verification

The direct Windows result table is in
[`../development/testing.md`](../development/testing.md#spectator-clarity-smoke).
All rows remain `PENDING` until a person performs and records the interactions.
A compiled Client, successful headless run, window-opening probe, or synthetic
input is not direct manual evidence.

| Evidence field | Result |
| --- | --- |
| Date | PENDING |
| Machine/platform | PENDING |
| Source commit | PENDING |
| Launch path | PENDING |
| Optional screenshots | None recorded |
| Overall direct smoke | PENDING |

## Deterministic regression result

The canonical seed-1 200-agent workload exactly preserved the approved
deterministic baseline:

| Value | Approved baseline | Final result |
| --- | --- | --- |
| Outcome | Faction 1 victory | `Faction1Victory` — PASS |
| Terminal tick | 235 | 235 — PASS |
| State hash | `210C5EF8E7BE4D48` | `210C5EF8E7BE4D48` — PASS |
| Event hash | `CE35EDA4B2A4E5A4` | `CE35EDA4B2A4E5A4` — PASS |
| Allocated bytes | Below `19,856,712` | `12,108,304` — PASS |

## Packaging result

**PASS.** `./scripts/package.ps1 -Runtime win-x64` completed and the
self-contained executable exists at
`artifacts/packages/client-win-x64/Hukbo.Client.exe`. The packaged Client
opened visibly, remained responsive, and showed
`Hukbo — A 0 : 0 B — Seed 1 — Tick 0 — 1x — Paused — Ongoing`. A normal window-close
request returned exit code 0.

This package/runtime probe predates the round-scoring extension and proves that
spectator build's startup, visible initial paused status, responsiveness, and
normal window close only. The round-scoring plan did not require a new package.
It does not prove hands-on Play/Pause/Menu, selection, event-log, score,
Next Round, Full Reset, summary, or modal Exit Game behavior.

## Independent review findings

**PASS.** The spectator-clarity reviewer reported no findings. The later
round-scoring reviewer found one High stale-documentation gap and two Low row
references; all were corrected. No unresolved Critical, High, Medium, or Low
finding remains. The reviews covered:

- `ArenaGame` input priority, per-tick event ingestion, terminal handling, and
  reset behavior;
- the single playback authority and shared command boundary;
- dead-agent selection persistence;
- bounded event-feed order, scrolling, and reset;
- summary derivation and displayed values;
- UI pointer consumption and modal semantics;
- confirmation that Core gameplay remained unchanged after namespace
  normalization.

The reviewers reran the focused and repository tests, the zero-warning Release
build, exact seed-1 tick and hashes, formatting, and diff checks. The later
review also verified scoring, seed progression, both reset paths, retained
event/snapshot behavior, allocation evidence, and unrelated-worktree scope.

## Acceptance evidence

| Criterion | Evidence type and location | Status |
| --- | --- | --- |
| 1. Click selects the nearest living agent within radius. | Client selection tests; smoke rows 6 and 8 | Automated PASS; manual PENDING |
| 2. Exact-distance ties use lower entity ID. | Client selection test | PASS |
| 3. Empty-arena click clears selection. | Client selection test; smoke row 8 | Automated PASS; manual PENDING |
| 4. UI clicks do not click through to the arena. | Independent input-priority review; smoke row 8 | Review PASS; manual PENDING |
| 5. Selection persists after the selected agent dies. | Client selection test; smoke row 7 | Automated PASS; manual PENDING |
| 6. Next Round and Full Reset clear selection. | Client reset tests; smoke rows 12 and 14 | Automated PASS; manual PENDING |
| 7. Inspector shows every authoritative field. | Independent inspector/summary review; smoke rows 6 and 7 | Review PASS; manual PENDING |
| 8. Every advanced tick contributes its published events. | Multi-tick ingestion test and integration | PASS |
| 9. Feed is ordered, deduplicated, capped at 200, and scrollable. | Event-feed tests; smoke row 9 | Automated PASS; manual PENDING |
| 10. Wheel over the event log does not zoom the arena. | Independent pointer-ownership review; smoke row 9 | Review PASS; manual PENDING |
| 11. Play, Pause, and Menu are always visible and share one command boundary. | Playback/command tests; smoke rows 2–4 | Automated PASS; manual PENDING |
| 12. Modal Play, Pause, Menu, Escape, and Exit retain their semantics. | Independent command/modal review; smoke rows 4 and 5 | Review PASS; manual PENDING |
| 13. Terminal outcome pauses and displays correct final values. | Summary tests; smoke row 10 | Automated PASS; manual PENDING |
| 14. Next Round starts paused, clears disposable UI state, and advances the deterministic seed. | Reset and match-series tests; smoke rows 12 and 13 | Automated PASS; manual PENDING |
| 15. Focused tests, canonical gate, package, manual smoke, and review pass. | Sections above | Automated/package/review PASS; manual PENDING |
| 16. No hosted-CI workflow or completion gate is added. | Active operating-doc search and repository inspection | PASS |
| 17. Only terminal victories score Team A/Blue or Team B/Red; ongoing and draw do not score. | Match-series tests; smoke row 11 | Automated PASS; manual PENDING |
| 18. Next Round preserves score, speed, and camera while advancing the seed and pausing. | Source verification and reset tests; smoke rows 12 and 13 | Automated/source PASS; manual PENDING |
| 19. Full Reset clears wins, restores seed 1 and 1x, fits the camera, and pauses. | Match-series/reset tests and source verification; smoke row 14 | Automated/source PASS; manual PENDING |
| 20. Allocation pressure decreases without changing seed-1 determinism. | Core allocation regression and canonical workload | PASS |

## Status

**CONDITIONALLY COMPLETE**

The implementation, current automated verification, deterministic regression,
allocation guard, package/startup probe, and independent reviews pass. Direct
Windows interaction remains pending, so repository readiness cannot be upgraded
to `READY`.

## Limitations

- Direct Windows interaction cannot be inferred from compilation or synthetic
  input and remains pending until observed.
- Windows x64 remains the only supported target for this phase.
- The missing public-distribution license remains a distribution limitation,
  not a local build failure.
- Hosted CI is intentionally outside the delivery policy and is neither a
  missing gate nor a follow-up item.

## Next action

Perform and record every direct smoke row. If all rows pass, update this report
and the repository readiness report from conditional to complete/ready.
