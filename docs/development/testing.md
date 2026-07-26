# Testing and Verification

## Canonical gate

```powershell
./scripts/verify.ps1
```

The gate performs, in order:

1. prerequisite validation and locked restore;
2. formatting verification;
3. Release solution build;
4. Core tests without rebuilding;
5. a 200-agent, 10,000-tick, seed-1 headless determinism workload.

It does not launch a window or alter authoritative game state. It never runs a
destructive Git or filesystem cleanup.

This repository intentionally uses local-only verification. There is no GitHub
Actions workflow or hosted-CI completion gate. Run the canonical gate on the
integration workstation and record its exact result.

## Focused commands

```powershell
./scripts/test.ps1 -Configuration Release
dotnet test tests/AutonomousArena.Core.Tests -c Release `
  --filter FullyQualifiedName~DeterminismTests
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/format.ps1 -Verify
```

Tests must remain independent from GPU, audio hardware, window focus, network,
wall clock, `System.Random`, and MonoGame types. Performance output is evidence,
not a universal frame-time guarantee.

## Interactive smoke checklist

Run `./scripts/run.ps1` in an interactive Windows desktop and record:

- the window opens and colored agents render;
- battle tick advances while playing;
- Space pauses/resumes with the menu closed;
- Escape opens the overlay and stops tick advancement;
- Play resumes and closes the overlay;
- Pause leaves the overlay open and tick unchanged;
- Exit Game closes the process once with exit code zero;
- the window close button exits cleanly;
- WASD/arrows pan, mouse wheel zooms, speed keys change scheduling, and `R`
  resets the same seed.

An interactive smoke is not considered passed merely because the Client
compiled.

## Failure classification

Classify failures as implementation, test, environment/dependency, pre-existing,
incorrect assumption, unrelated, or flaky. Make the narrowest correction, rerun
the focused check, and expand only after it passes.
