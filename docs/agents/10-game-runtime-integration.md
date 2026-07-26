# 10 — Game Runtime Integration

## Scope

Integrate MonoGame windowing, input, fixed scheduling, rendering, diagnostics,
shutdown, and the Play/Pause/Exit overlay without moving gameplay authority out
of Core.

## Inputs inspected

- Approved menu design and shared Core contract.
- Client project/package configuration.
- Client workstream ownership and reported content-tool dependency.

## Decisions and work

The integrated runtime has guarded startup, fixed Core ticks, one sprite batch,
camera/zoom, speed/reset, Space toggle, and an Escape menu with Play, Pause, and
Exit Game. Menu actions affect only client scheduling and lifecycle.

## Files

- `src/AutonomousArena.Client/**`
- `README.md`
- `docs/development/getting-started.md`
- `docs/development/testing.md`

## Verification

Release build and SpriteFont compilation passed with 0 warnings/errors. A
self-contained published executable opened a real 1280x720 window, advanced the
simulation, and closed normally with exit code 0. Synthetic key injection did
not reach MonoGame's SDL input layer, so direct Play/Pause/Exit interaction
remains manual QA.

## Status

**CONDITIONALLY COMPLETE**

## Limitations

Window creation, rendering, simulation advancement, and normal close are
proven. Direct menu activation remains unrecorded.

## Next action

Execute the three-button manual checklist and record the result.
