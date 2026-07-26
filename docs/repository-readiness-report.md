# Repository Readiness Report

**Status: CONDITIONALLY READY**

**Evidence snapshot:** spectator clarity plus round-scoring/reset integration
on 2026-07-26

The toolchain, deterministic simulation, MonoGame client, content pipeline,
workflow scripts, package, and onboarding documentation are integrated.
The foundation non-graphical gates passed and the published client opened and
advanced on the reference Windows machine. Spectator-clarity automated
verification, deterministic regression, stress, and Windows packaging now
pass. The round-scoring/reset extension also passes the current local gate and
allocation regression without changing the seed-1 result or hashes. Its fresh
review and direct Windows interaction remain pending, so readiness remains
conditional.

## Validated

| Gate | Result | Evidence |
| --- | --- | --- |
| Windows developer prerequisites | Passed | Doctor: Windows x64, PowerShell 7.6.4, Git, SDK 10.0.302 |
| Locked NuGet and tool restore | Passed | Five projects and dotnet-mgcb 3.8.5 restored |
| Complete Release build | Passed | 0 warnings, 0 errors; SpriteFont compiled |
| Foundation Core and headless tests | Passed | 42/42 Release tests |
| Formatting | Passed | `dotnet format --verify-no-changes`, 0 files changed |
| NuGet vulnerability audit | Passed | No vulnerable direct/transitive packages reported by nuget.org on 2026-07-26 |
| Script parsing | Passed | Every `scripts/*.ps1` parsed with the PowerShell AST parser |
| 200-agent headless determinism | Passed | Same-seed hashes match; Faction 1 victory at tick 235 |
| 500-agent stress | Passed | Deterministic result at tick 309 |
| Foundation Windows package | Passed | Self-contained `win-x64` output created |
| Foundation client window smoke | Passed | 1280x720 window opened, simulation advanced, normal close returned exit code 0 |
| Foundation independent technical review | Passed | No remaining Critical or High findings |
| Current Client tests | Passed | 41/41 presentation and round-lifecycle tests |
| Current repository tests | Passed | Core 45/45; Client 41/41 |
| Current canonical verification | Passed | Formatting; Release build with 0 warnings/errors; both test projects; seed-1 workload |
| Round score lifecycle | Passed | Team A/Blue and Team B/Red victories score separately; ongoing/draw score neither; deterministic seed progression and full reset covered |
| Current deterministic regression | Passed | `Faction1Victory`, tick 235, state `210C5EF8E7BE4D48`, events `CE35EDA4B2A4E5A4` |
| Allocation improvement | Passed | 12,108,304 allocated bytes, below the 19,856,712-byte baseline |
| Spectator-clarity 500-agent stress | Passed | Deterministic result at tick 309 |
| Current Windows package | Passed | Round-scoring build published to `artifacts/packages/client-win-x64/Hukbo.Client.exe` |
| Current package window smoke | Passed | Visible and responsive at score 0-0, seed 1, tick 0, 1x, paused, ongoing; normal window close returned 0 |
| Prior spectator-clarity independent review | Passed | No Critical, High, Medium, or Low findings; no unresolved Critical/High issue |
| Round-scoring independent review | Passed | No code Critical/High/Medium findings; the stale-documentation High and two Low row references were corrected |
| Direct interaction | Pending | All 15 rows in the expanded direct smoke table remain `PENDING` |
| Verification policy | Passed | Owner selected local-only verification; `verify.ps1` is authoritative |

## Commands executed

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
./scripts/test.ps1 -Configuration Release
./scripts/verify.ps1 -SkipBootstrap
./scripts/package.ps1 -Runtime win-x64
```

For the spectator-clarity snapshot, canonical verification completed
formatting and a zero-warning Release build. The current round-scoring/reset
gate passes 45 Core tests and 41 Client tests and preserves the deterministic
200-agent result. That workload now records 12,108,304 allocated bytes, below
the 19,856,712-byte baseline. The prior 500-agent workload remained
deterministic at tick 309.

## Known limitations

- Windows x64 is the only supported v0.1 target.
- The Windows package is self-contained and intentionally larger than a
  framework-dependent publish.
- No multiplayer, persistence, pathfinding, store distribution, or
  non-Windows packaging is included.
- A project license must be selected before public distribution.
- The full direct smoke, including controls, selection, event scrolling,
  summary, score timing, Next Round, Full Reset, modal commands, and clean exit,
  has not yet been recorded.
- Hosted CI is intentionally not configured and is not a readiness gate.

## Required follow-up

Complete the round-scoring/reset independent review, then run and record every
direct row in `docs/development/testing.md`. Upgrade this report to `READY` only
when the review and the hands-on control, selection, event-log, score/reset,
summary, modal Exit Game, and close checks pass.
