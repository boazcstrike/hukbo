# 17 — Technical Review and Handoff

## Scope

Review scope, dependencies, machine assumptions, safety, verification honesty,
onboarding, and final handoff readiness.

This report was originally written against the foundation snapshot. It was
re-executed and completed on 2026-07-27 against merge commit `8815a3c`, which is
the state of `main` after the collision-priority fairness change. The current
review is recorded first. The foundation snapshot is preserved below it for
traceability and is superseded.

## Current review — 2026-07-27, commit `8815a3c`

### Inputs inspected

- Every script under `scripts/`, the canonical gate, and the packaging script.
- `README.md`, `CLAUDE.md`, `AGENTS.md`, `docs/development/**`, and the numbered
  role reports.
- `docs/development/testing.md` as the recorded oracle, checked field by field
  against a fresh measurement rather than read as settled.
- `Directory.Packages.props`, `global.json`, `NuGet.config`, the lock files, and
  the pinned `dotnet-mgcb` tool manifest.
- `.gitignore`, the tracked-file set, and the working tree.

### Machine and commit the evidence comes from

Every figure below was produced on one machine, from one tree, with the commit
confirmed both before and after each command. Nothing is carried over from an
earlier run.

| Field | Value |
| --- | --- |
| Commit | `8815a3c` — *Merge collision priority fairness into main* |
| Branch | `main` |
| Working tree | No modification to any tracked file at the time of every command below; verified immediately before the gate |
| Operating system | Microsoft Windows 10.0.26200 (Windows 11 Pro), x64 |
| .NET SDK | 10.0.302 as pinned in `global.json`; runtime 10.0.10 |
| PowerShell | 7.6.4 |
| Git | 2.55.0.windows.3 |
| Processor count | 20 |

An earlier attempt at this review measured commit `6b4f809` and was discarded in
full. A concurrent session merged `feature/collision-priority-fairness` into
`main` while that gate was running, so its figures described a tree that was no
longer `HEAD` by the time they were read. They are not reported here. Every
command below was re-run from the start on `8815a3c`.

### Verification

The canonical gate, `./scripts/verify.ps1 -SkipBootstrap`, passed at all five
stages and ended with `[PASS] Canonical repository verification completed.`

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Formatting reported `Formatted 0 of 197 files`. The Release solution build
produced 0 warnings and 0 errors under repository-wide
`TreatWarningsAsErrors` with nullable enabled.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 418 | 0 | 0 |
| `Hukbo.Client.Tests` | 564 | 0 | 0 |

The 200-agent, 10,000-tick, seed-1 headless workload reproduced the recorded
oracle in `docs/development/testing.md` exactly:

| Field | Measured | Recorded oracle |
| --- | --- | --- |
| Measured ticks | 1154 | 1154 |
| Outcome | `Faction1Victory` | `Faction1Victory` |
| Faction 0 / faction 1 survivors | 0 / 3 | 0 / 3 |
| State hash | `5BEBA7A68F69BE0D` | `5BEBA7A68F69BE0D` |
| Event hash | `D379B60B2E30FFFC` | `D379B60B2E30FFFC` |
| Deterministic | `true` | `true` |
| First mismatch tick | `null` | `null` |
| `longestBlockedStreakTicks` | 47 | 47 |
| `maximumPenetrationRaw` | 0 | 0 |

Every collision metric matched as well: `candidatePairs` 110,970,
`contactPairs` 5,198, `acceptedMoves` 71,780, `blockedAgentTicks` 24,703,
`attackCapableAgentTicks` 9,231, `maximumFrontWidthRaw` 629,652, and
`maximumFrontDepthRaw` 51,086. This is an independent reproduction of the pair
that the collision-priority commit message claims, produced without reading that
message first.

Timing and allocation are the only fields that moved, and they are expected to.
The run allocated 71,704,672 bytes against a recorded 71,698,480, with tick p50
0.1026 ms, p95 1.8032 ms, p99 2.489 ms, and a 8.9851 ms maximum. These are
machine-and-moment measurements, not hashed state, so a small difference here is
noise rather than a determinism signal. Every field that reaches a hash matched
byte for byte.

The report-only 500-agent stress workload was run four times on this commit:

| Field | Value |
| --- | --- |
| Measured ticks | 2668 |
| Outcome | `Faction0Victory` |
| Faction 0 / faction 1 survivors | 1 / 0 |
| State hash | `FE44ADA93E0E202A` |
| Event hash | `9C8EF5CB79810560` |
| Deterministic | `true` |
| `longestBlockedStreakTicks` | 54 |
| `maximumPenetrationRaw` | 0 |
| Allocated | 416,546,128 bytes |

All four runs produced identical tick counts, outcomes, survivor counts, and
both hashes. Only `allocatedBytes` varied, across 416,546,104, 416,546,128 twice,
and 416,552,320. Allocation is observed rather than hashed, so that variation is
GC accounting and not a contract breach. These figures match what
`docs/development/testing.md` records for this commit.

Supporting checks, all on the same commit:

| Check | Result |
| --- | --- |
| `./scripts/doctor.ps1` | Every row `[PASS]`: platform, PowerShell, Git, Git LFS, SDK 10.0.302, centrally pinned MonoGame 3.8.5 |
| Script parsing | All 11 scripts under `scripts/` parsed with the PowerShell AST parser; 0 parse errors |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages in any of the five projects |
| `./scripts/package.ps1 -Runtime win-x64` | `[PASS]`; 273 files across 199 top-level entries, 85 MB, `artifacts/packages/client-win-x64/Hukbo.Client.exe` written fresh at this commit |
| Secret hygiene | `.env` is ignored by `.gitignore` and untracked; no tracked file contains a key value, only the `ELEVENLABS_API_KEY` variable name in documentation and in `scripts/sfx.ps1` |
| Package output hygiene | `artifacts/` is ignored; the 85 MB package is not a tracked path |

### Findings

One defect was found and corrected. Nothing Critical or High was found.

**Medium — the recorded Core test count was wrong by six.**
`docs/development/testing.md` recorded `Hukbo.Core.Tests` at 412 passing for
this commit. A direct measurement of
`dotnet test tests/Hukbo.Core.Tests -c Release` at `8815a3c` returned
`Passed: 418, Failed: 0, Skipped: 0`. The same document's own arithmetic already
implied 418, since it states the Core count rises from `main`'s 398 by 20. The
merge commit introduced no test file that the branch tip `c01ea9f` did not
already carry — the two trees list an identical set of 23 files under
`tests/Hukbo.Core.Tests` — so the branch and `main` run the same suite and one
number has to serve both. The table has been corrected to 418 with a note
recording how the figure was obtained. This is the same class of defect that a
previous review caught as a High finding on the plains-backdrop entry, and it is
worth saying plainly that a test count is evidence: a reader comparing counts
across entries to see what a change added is misled by a wrong one.

**Low, not corrected — one unverified intermediate figure remains.**
The same section states that "before it was added the whole 412-case suite
stayed green under that mutation", describing the suite as it stood partway
through the collision-priority work. That figure is now inconsistent with the
corrected 418, but the intermediate state it describes no longer exists in the
tree and its true size cannot be re-derived from the repository. It has
deliberately been left as written rather than replaced with a guess. Whoever
recorded the mutation result should either confirm the number or drop it.

**Not a finding — the 500-agent oracle.**
An early reading of this review appeared to show the 500-agent stress workload
diverging from its recorded oracle. It did not. The recorded figures were read
from the pre-merge tree while the workload was executed on the post-merge tree.
Once both were taken from `8815a3c` they agreed exactly, and four consecutive
runs reproduced the same two hashes. No determinism defect exists here, and the
apparent one is recorded so that a future reader does not rediscover it as
alarming.

### Scope, dependencies, and machine assumptions

Scope is held. `Hukbo.Core` carries no campaign, economy, diplomacy, or
map-generation state, and no MonoGame, filesystem, network, windowing, audio, or
wall-clock reference. Dependencies remain centrally pinned in
`Directory.Packages.props` with lock files committed and restore run in
`--locked-mode`; the `dotnet-mgcb` 3.8.5 tool manifest restores as part of the
gate rather than as a manual prerequisite. Machine assumptions are narrow and
stated: Windows x64 only for v0.1, SDK 10.0.302 pinned in `global.json`, and
`doctor.ps1` fails loudly rather than proceeding on a drifted toolchain.

Local workflows remain repository-relative, locked, and non-destructive. The
packaging script is the only one that removes a directory, and it refuses to
touch any path outside `artifacts/packages` with an explicit assertion before
each delete. `sfx.ps1` remains the only script that reaches a network service,
runs only when a person asks for a sound, and is not part of the gate; the game,
the build, the tests, and the gate are fully offline.

Onboarding is accurate. `README.md` states the real prerequisites, the real
launch path, and the real controls, and it does not claim hosted CI. The absence
of CI is a repository-owner decision, not a gap, and is not a completion
requirement.

### Files

- `docs/agents/17-technical-review-handoff.md`
- `docs/development/testing.md` — Core test count corrected from 412 to 418
- `docs/repository-readiness-report.md` — refreshed to this commit

### Status

**CONDITIONALLY COMPLETE**

Every non-interactive gate this role owns has been executed on one pinned commit
and passes. The condition is unchanged in kind and is not something this role can
discharge: no interactive verification has been performed by a person.

### Limitations

Automated input injection does not reach the SDL input layer, so it cannot stand
in for the manual checklist, and per `CLAUDE.md` section 6 it may not be used to
flip a row in any case. Compilation, a green gate, a benchmark, a zero-warning
build, and a window-opening probe do not entitle anyone to mark a manual row
`PASS`.

Every interactive row in `docs/development/testing.md` therefore remains
`PENDING`. That is 88 rows across six checklists, counted directly: 52 in the
spectator-clarity smoke, 7 in collision readability including the `21a` row the
collision-priority amendment added, 5 in camera auto-pan, 4 in starting
deployment, 14 in typography, and 6 in the last-stand formation. Not one has
been observed in a live window by a person, and this review did not change that.

No project license is present. The repository has no `LICENSE` or `COPYING`
file, while `README.md` links to a public GitHub repository. Choosing a license
is a repository-owner decision and was not made here. Until one is added, the
repository is not ready for public distribution, whatever the gate says.

A concurrent workstream was active in this working tree throughout the review and
its in-flight files — `docs/research/SOUND-CAPACITY-MEASUREMENTS.md` and
`tools/Hukbo.Tools.MixAnalysis/` — are untracked. They are outside this review's
scope, were not measured, and are named here only so that a reader comparing this
report against `git status` is not surprised by them. They do need to be either
committed or removed before the repository is handed over.

That concurrency is itself worth recording. `main` advanced under this review
once already, and a gate result is only evidence for the commit it ran on. Any
future run of this role should pin the commit before and after every command, as
this one did, and discard the run rather than the discrepancy if the two differ.

### Next action

Three items, in the order that unblocks the most:

1. Run `./scripts/run.ps1` on an interactive Windows desktop and record the
   manual menu result — Play, Pause, Menu, modal commands, and Exit Game — into
   the spectator-clarity smoke table, filling the date, machine, commit, and
   launch-path evidence fields. This is the single item that has held roles 16,
   17, and 18 at conditional since the foundation snapshot.
2. Select and add a project license before any public distribution.
3. Land or discard the in-flight sound-capacity work so the tree is clean at
   handover: `docs/research/SOUND-CAPACITY-MEASUREMENTS.md` and
   `tools/Hukbo.Tools.MixAnalysis/`.

## Foundation snapshot — superseded

Preserved as originally written, for traceability. Its figures describe the
foundation snapshot and are dead values: the 42-test count in particular has been
superseded many times over and is not comparable to the 418 and 564 recorded
above. Do not cite anything in this section as a current baseline.

### Inputs inspected

- Delivery scripts, local verification policy, README, operating docs, and all
  numbered role reports.
- Foundation source/configuration and Git diff/status.
- Reported integration dependency on a pinned `dotnet-mgcb` tool manifest.

### Decisions and work

Kept local workflows Windows-scoped, repository-relative, locked, and
non-destructive. Integrated Core, Headless, Client/Menu, and delivery work.
Documented the missing public-distribution license and direct menu interaction
as explicit follow-ups. Hosted CI was removed by later repository-owner
decision and is not a completion requirement.

### Files

- `README.md`
- `docs/agents/**`
- `docs/repository-readiness-report.md`
- `docs/development/**`

### Verification

PowerShell scripts parse. The canonical gate passes formatting, zero-warning
Release build, 42 tests, and deterministic 200-agent execution. Self-contained
packaging and real window startup/normal close pass. Independent review is
complete with no remaining Critical or High findings. Its two Medium findings
(maximum-map placement overflow and stale local package output) were corrected
and reverified. Direct menu interaction remains conditional.

### Status

**CONDITIONALLY COMPLETE**

### Limitations

Automated input injection could not reach SDL, so manual Play/Pause/Exit
interaction remains required.

### Next action

Record the manual menu result.
