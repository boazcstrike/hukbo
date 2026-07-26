---
name: hukbo-verify-and-record
description: Runs the Hukbo canonical verification gate and records evidence honestly. Use when asked to verify changes, run the gate or the tests, check whether work is ready to integrate, run a benchmark, interpret verify.ps1 or benchmark output, or update the results and smoke checklist in docs/development/testing.md. Covers the five gate stages, the headless exit codes, which RunReport fields count as evidence, and the rule that only a human at an interactive desktop may flip a smoke-test row to PASS.
---

# Verifying Hukbo and recording the result

## The one gate

```powershell
./scripts/verify.ps1                 # full: includes bootstrap + locked restore
./scripts/verify.ps1 -SkipBootstrap  # normal iteration path
```

`scripts/verify.ps1` runs five stages in a fixed order. Do not run them out of
order or substitute your own commands — stages 3 to 5 deliberately reuse the
previous stage's artifacts.

| # | Stage | Call |
| --- | --- | --- |
| 1 | Prerequisites + locked restore (skippable) | `bootstrap.ps1` |
| 2 | Formatting verification | `format.ps1 -Verify` |
| 3 | Release build, no restore | `build.ps1 -Configuration Release -NoRestore` |
| 4 | Release tests, no build | `test.ps1 -Configuration Release -NoBuild` |
| 5 | Determinism workload, no build | `benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -NoBuild` |

Success prints `[PASS] Canonical repository verification completed.`

Focused commands, for iteration only — they never replace the gate:

```powershell
./scripts/test.ps1 -Configuration Release
dotnet test tests/Hukbo.Core.Tests -c Release --filter FullyQualifiedName~DeterminismTests
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/format.ps1 -Verify
```

## Reading the headless result

`Hukbo.Headless` prints an indented camelCase JSON `RunReport` on **stdout** —
it is not a log line. Exit codes are meaningful:

| Code | Meaning |
| --- | --- |
| 0 | Deterministic run completed |
| 1 | Unhandled exception; message goes to stderr |
| 2 | Argument error; usage goes to stderr |
| 3 | **Determinism mismatch** — read `firstMismatchTick` first |

The runner advances two independent `BattleSimulation` instances from the same
scenario and compares tick, outcome, state hash, and the full `LastEvents`
sequence every tick. A mismatch stops the run at that tick.

Quote these fields as evidence: `deterministic`, `firstMismatchTick`,
`stateHash`, `eventHash`, `outcome`, `faction0Survivors`, `faction1Survivors`,
`measuredTicks`, `allocatedBytes`, `tickPercentiles`, and the whole
`environment` block (OS, framework, architecture, processor count). The
`environment` block is what satisfies the "name the hardware" requirement in
`SIMULATION-GAME-STANDARDS.md` §8.

`allocatedBytes` is measured with `GC.GetAllocatedBytesForCurrentThread` around
the tick loop, so it covers simulation allocation only.

## Recording evidence

Automated results prove the **non-interactive** gate and nothing else. Write
them into the "Latest non-interactive result" section of
`docs/development/testing.md` with the exact numbers, not a summary adjective.

The interactive smoke checklist in the same file is a different claim.

- A row may only become `PASS` after a human ran `./scripts/run.ps1` on an
  interactive Windows desktop and observed the expected behavior.
- Compilation, unit tests, a window-opening probe, or synthetic input **do not**
  qualify. Never flip a row on that basis.
- Leave rows you did not exercise as `PENDING`. Use `BLOCKED` when you could not
  run it, and say why.
- Fill the evidence fields: date, machine and platform, source commit, launch
  path (source or package), optional screenshot paths.
- There is no CI substitute. Never propose a GitHub Actions workflow.

If you did not run the gate, say so plainly instead of implying it passed.

## When something fails

Classify first, using the vocabulary in `docs/development/testing.md`:
implementation, test, environment or dependency, pre-existing, incorrect
assumption, unrelated, or flaky.

A `NuGetAudit` advisory failure (NU1902/NU1903/NU1904) on an unchanged package
is an **environment** failure, not an implementation failure — a newly published
advisory can break a build you did not touch.

Then make the narrowest correction, rerun the focused check, and only widen once
it passes. Do not weaken a test, a warning, or an analyzer to get green.
