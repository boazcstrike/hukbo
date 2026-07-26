# 12 — Test Architecture

## Scope

Choose the test platform and define deterministic unit/regression, headless,
and interactive smoke layers.

## Inputs inspected

- Core.Tests project and primitive tests.
- Approved battle/determinism acceptance cases.
- Headless and Client workstream contracts.

## Decisions and work

Retained VSTest with xUnit. Core tests must avoid graphics, audio, input focus,
network, filesystem, wall clock, and engine types. Headless comparison proves
same-seed ordered events/state; interactive UI remains a separate checklist.

## Files

- `tests/AutonomousArena.Core.Tests/**` (owned by Simulation workstream)
- `scripts/test.ps1`
- `scripts/benchmark.ps1`
- `docs/development/testing.md`

## Verification

The existing 7 deterministic primitive tests passed in Release. Scenario,
battle, determinism, and headless workload tests were not present in this
delivery snapshot and are not claimed.

## Status

**CONDITIONALLY COMPLETE**

## Limitations

No automated rendering/input test layer is included in v0.1.

## Next action

Run the complete integrated Core suite and 200-agent headless comparison.
