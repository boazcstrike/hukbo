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

The runtime is assigned exclusively to the Client/Menu workstream. Required
behavior is documented: guarded startup, fixed Core ticks, one sprite batch,
camera/zoom, speed/reset, Space toggle, and Escape menu with Play, Pause, and
Exit Game.

## Files

- `src/AutonomousArena.Client/**` (owned by another workstream)
- `README.md`
- `docs/development/getting-started.md`
- `docs/development/testing.md`

## Verification

No Client runtime verification was run in the delivery worktree because the
entry point and menu are integrated separately. Compilation or runtime success
is not claimed.

## Status

**DEFERRED**

## Limitations

An interactive Windows desktop is required to prove window creation, input,
rendering, pause behavior, and clean exit.

## Next action

After Core-first integration, build Client and execute the full interactive
smoke checklist.
