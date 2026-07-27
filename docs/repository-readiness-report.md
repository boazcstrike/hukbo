# Repository Readiness Report

**Status: CONDITIONALLY READY**

**Evidence snapshot:** role 17 handoff review on 2026-07-27, at merge commit
`8815a3c` on `main`

The toolchain, deterministic simulation, MonoGame client, content pipeline,
workflow scripts, package, and onboarding documentation are integrated. Every
non-graphical gate passes on one pinned commit, and the deterministic 200-agent
oracle was independently reproduced field for field. Readiness remains
conditional for two reasons that no automated gate can clear: no interactive
verification has been performed by a person, and the repository still has no
license.

This report was refreshed by
[role 17 — technical review and handoff](agents/17-technical-review-handoff.md),
which holds the full evidence, the review findings, and the commands. Figures
recorded here for earlier snapshots have been replaced rather than kept, because
this document is a readiness statement rather than a history; the superseded
determinism oracles are traced in
[docs/development/testing.md](development/testing.md).

## Measured at commit `8815a3c`

Environment: Microsoft Windows 10.0.26200 x64, .NET SDK 10.0.302 as pinned in
`global.json`, runtime 10.0.10, PowerShell 7.6.4, Git 2.55.0, 20 processors. No
tracked file was modified while these commands ran, and the commit was confirmed
both before and after each one.

| Gate | Result | Evidence |
| --- | --- | --- |
| Windows developer prerequisites | Passed | `doctor.ps1`: Windows x64, PowerShell 7.6.4, Git 2.55.0, Git LFS, SDK 10.0.302, MonoGame 3.8.5 centrally pinned |
| Locked NuGet and tool restore | Passed | Five projects restored in `--locked-mode`; `dotnet-mgcb` 3.8.5 restored from the tool manifest |
| Formatting | Passed | `Formatted 0 of 197 files` |
| Complete Release build | Passed | 0 warnings, 0 errors under repository-wide `TreatWarningsAsErrors` with nullable enabled |
| Repository tests | Passed | `Hukbo.Core.Tests` 418/418; `Hukbo.Client.Tests` 564/564; 0 failed, 0 skipped |
| Canonical verification | Passed | `./scripts/verify.ps1 -SkipBootstrap` passed all five stages and ended `[PASS] Canonical repository verification completed.` |
| 200-agent headless determinism | Passed | Seed 1, 10,000 ticks: `Faction1Victory` at tick 1154, state `5BEBA7A68F69BE0D`, events `D379B60B2E30FFFC`, `deterministic: true`, `firstMismatchTick: null` |
| Oracle reproduction | Passed | Every hashed field, both hashes, and all nine collision metrics matched `docs/development/testing.md` exactly; only timing and allocation differed, as they must |
| 500-agent stress | Passed | Report only, not gated. `Faction0Victory` at tick 2668, state `FE44ADA93E0E202A`, events `9C8EF5CB79810560`; four consecutive runs produced identical hashes |
| Solid-disc invariant | Passed | `maximumPenetrationRaw` is exactly 0 on both the 200-agent and 500-agent workloads |
| Script parsing | Passed | All 11 scripts under `scripts/` parsed with the PowerShell AST parser; 0 parse errors |
| NuGet vulnerability audit | Passed | `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages in any of the five projects |
| Windows package | Passed | `./scripts/package.ps1 -Runtime win-x64` published 273 files, 85 MB, to `artifacts/packages/client-win-x64/Hukbo.Client.exe` |
| Secret hygiene | Passed | `.env` ignored and untracked; no tracked file carries a key value, only the `ELEVENLABS_API_KEY` variable name |
| Package output hygiene | Passed | `artifacts/`, `bin/`, and `obj/` are ignored; no build or package output is tracked |
| Verification policy | Passed | Owner selected local-only verification; `verify.ps1` is authoritative and there is no hosted CI |
| Direct interaction | **Mostly pending** | 2 of 88 interactive rows are `PASS` — launch-to-paused and Pause, observed by the owner on 2026-07-27 at `8815a3c`. Rows 2, 4, 5, and 15 are partly observed and stay `PENDING`; 82 rows are untouched |
| Project license | **Absent** | No `LICENSE` or `COPYING` file exists |

## Commands executed

```powershell
./scripts/doctor.ps1
./scripts/verify.ps1 -SkipBootstrap
./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1
./scripts/package.ps1 -Runtime win-x64
dotnet test tests/Hukbo.Core.Tests -c Release --no-build
dotnet list Hukbo.slnx package --vulnerable --include-transitive
```

## Inherited evidence, not re-observed at this commit

These results were recorded against earlier snapshots and are carried forward
because nothing since has invalidated them. They were **not** re-observed at
`8815a3c` and should not be read as current measurements.

- The foundation and spectator-clarity independent reviews closed with no
  remaining Critical or High findings.
- The published client opened at 1280x720 on the reference Windows machine,
  advanced its simulation, and returned exit code 0 after a normal window close.
- Round score lifecycle behaviour — Team A/Blue and Team B/Red scoring
  separately, ongoing and drawn rounds scoring neither, deterministic seed
  progression, and full reset — was covered by tests that still pass inside the
  418-case Core suite and the 564-case Client suite above.

The client window smoke in particular is a foundation-snapshot observation. It is
not a substitute for the interactive checklist and does not make any row `PASS`.

## Known limitations

- **Interactive verification has barely started.** 2 of 88 rows are `PASS`, 4
  are partly observed and still `PENDING`, and 82 are untouched. The menu path
  has now been walked by a person without misbehaving, which is the first real
  movement on this since the foundation snapshot, but it is a long way from a
  recorded pass. Compilation, a green gate, benchmarks, a zero-warning build,
  and a window-opening probe do not substitute for the remainder, and synthetic
  input may not be used to flip a row.
- **No project license.** `README.md` links to a public GitHub repository while
  the tree carries no license file. Selecting one is a repository-owner
  decision. Until it is made, the repository is not ready for public
  distribution regardless of gate status.
- Windows x64 is the only supported v0.1 target.
- The Windows package is self-contained and intentionally larger than a
  framework-dependent publish.
- No multiplayer, persistence, pathfinding, store distribution, or non-Windows
  packaging is included.
- Hosted CI is intentionally not configured and is not a readiness gate.
- A concurrent sound-capacity workstream has untracked files in the tree —
  `docs/research/SOUND-CAPACITY-MEASUREMENTS.md` and
  `tools/Hukbo.Tools.MixAnalysis/`. They were not measured by this snapshot and
  should be landed or discarded before handover.

## Required follow-up

Run and record every interactive row in
[docs/development/testing.md](development/testing.md), starting with the
Play/Pause/Menu/Exit rows that have held roles 16, 17, and 18 at conditional
since the foundation snapshot, and add a project license. Upgrade this report to
`READY` only when the hands-on control, selection, event-log, score and reset,
summary, modal Exit Game, and clean-close checks pass and a license is in place.
