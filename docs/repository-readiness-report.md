# Repository Readiness Report

**Status: CONDITIONALLY READY**

**Evidence snapshot:** integrated branch on 2026-07-26

The toolchain, deterministic simulation, MonoGame client, content pipeline,
workflow scripts, package, and onboarding documentation are integrated.
Non-graphical gates pass and the published client opened and advanced on the
reference Windows machine. Readiness remains conditional because synthetic
keyboard injection could not reach the SDL input layer, so Play/Pause/Exit
still need one direct manual interaction pass.

## Validated

| Gate | Result | Evidence |
| --- | --- | --- |
| Windows developer prerequisites | Passed | Doctor: Windows x64, PowerShell 7.6.4, Git, SDK 10.0.302 |
| Locked NuGet and tool restore | Passed | Four projects and dotnet-mgcb 3.8.5 restored |
| Complete Release build | Passed | 0 warnings, 0 errors; SpriteFont compiled |
| Core and headless tests | Passed | 42/42 Release tests |
| Formatting | Passed | `dotnet format --verify-no-changes`, 0 files changed |
| NuGet vulnerability audit | Passed | No vulnerable direct/transitive packages reported by nuget.org on 2026-07-26 |
| Script parsing | Passed | Every `scripts/*.ps1` parsed with the PowerShell AST parser |
| 200-agent headless determinism | Passed | Same-seed hashes match; Faction 1 victory at tick 235 |
| 500-agent stress | Passed | Deterministic result at tick 309 |
| Windows package | Passed | Self-contained `win-x64` output created |
| Client window smoke | Passed | 1280x720 window opened, simulation advanced, normal close returned exit code 0 |
| Independent technical review | Passed | No remaining Critical or High findings |
| Menu interaction | Conditional | Code and rendering build; automation could not inject keyboard input into SDL |
| GitHub Actions run | Not run | Workflow exists but hosted execution has not occurred |

## Commands executed

```powershell
./scripts/verify.ps1
./scripts/package.ps1 -Runtime win-x64
```

The canonical verification completed formatting, Release build, 42 tests, and
the deterministic 200-agent workload. Packaging completed after reviewed
`win-x64` lock targets were generated for Client and Core.

## Known limitations

- Windows x64 is the only supported v0.1 target.
- The Windows package is self-contained and intentionally larger than a
  framework-dependent publish.
- No multiplayer, persistence, pathfinding, store distribution, or
  non-Windows packaging is included.
- A project license must be selected before public distribution.
- Hosted CI and direct manual clicking of Play, Pause, and Exit Game have not
  yet been recorded.

## Required follow-up

Run the short interactive checklist in `docs/development/testing.md`: open the
menu with Escape, activate Play, Pause, and Exit Game, and record the result.
Then run the committed workflow on GitHub. If both pass, upgrade this report to
`READY`.
