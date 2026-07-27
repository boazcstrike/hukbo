# Camera Auto-Pan Design

> **Archived: reference only.** This design is implemented and deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

## Goal

Keep the spectator watching the battle. As a fight thins out and the survivors
drift away from wherever the camera happens to sit, the spectator can end up
staring at an empty stretch of field while the last few agents kill each other
off screen. When that happens, the camera should pan itself to the fighting and
stop once the fighting is comfortably in view.

The camera pans only. It never changes zoom, and it never moves while the
spectator can already see someone fighting.

## Current State

`src/Hukbo.Client/SpectatorCamera.cs` holds a world-space `_center` and a
`_zoom`. `Update` reads WASD and the arrow keys, moves `_center` at
`PanPixelsPerSecond / _zoom` world units per second, and applies wheel zoom.
`Fit` runs once to frame the whole map at startup. `_center` is private and
there is no way to move the camera other than key input.

`ArenaGame.UpdateSpectatorInput` calls `_camera.Update` once per frame with the
gate-resolved pan input and the pointer-consumed flag. The arena's on-screen
rectangle is `GetLayout(screenBounds).ArenaBounds`.

The client reads `_simulation.Agents`, an `IReadOnlyList<AgentView>` ordered by
`EntityId`. Each view carries `XRaw`, `YRaw`, `IsAlive`, and `Intent`, where
`AgentIntent.Attacking` is the authoritative statement that the agent is
fighting this tick. World position is `XRaw / FixedPoint.Scale`.

There are no `SpectatorCamera` tests today.

## Definition of "fighting"

An agent is fighting when `IsAlive` is true and `Intent` is
`AgentIntent.Attacking`. That is the simulation's own word for it, so the client
does not have to infer combat from damage events, cooldowns, or proximity.

`AgentIntent.Moving` is deliberately excluded. Two armies closing across an
empty field are not yet a fight, and treating approach as combat would make the
camera chase every march.

## Interaction Model

The controller has two states.

**Idle.** The camera does not move on its own. Every frame it asks whether any
fighting agent falls inside the visible world rectangle. If at least one does,
it stays idle.

**Panning.** Entered when the visible rectangle contains no fighting agent at
all. On entry the controller picks a target point and then moves the camera
centre toward it, frame by frame, at a bounded speed. It returns to idle as soon
as a fighting agent is inside the *inner* rectangle — 70 percent of the visible
rectangle, centred. The inner rectangle is the hysteresis: without it the
controller would stop the instant a fighter crossed the outermost pixel of the
screen and immediately re-engage when that fighter drifted back out, producing a
visible stutter at the screen edge.

If the controller reaches its target and the inner test still fails — the fight
moved while the camera was travelling, or the anchor died — it picks a fresh
target and keeps going.

### Target selection

Chasing the centroid of *all* fighting agents is wrong. With two separate
melees at opposite ends of the map, their centroid is empty ground, and the
camera would settle somewhere no one is fighting.

Instead:

1. Find the fighting agent nearest the current camera centre. Ties break on the
   lower `EntityId`, matching the repo's stable-ordering rule, so the choice
   never depends on list iteration luck.
2. Take the centroid of every fighting agent within `ClusterRadius` world units
   of that anchor.

That centres the nearest actual melee rather than the average of unrelated
ones, and it does not jump between distant fights.

### Manual override

Spectator input wins. If the spectator holds a pan key on a frame, auto-pan
disengages immediately and will not re-engage for `ManualOverrideSeconds`. This
keeps the camera from fighting the spectator for the stick while they look at
something on purpose. Zoom does not suppress auto-pan, because zooming does not
express an opinion about where to look.

Auto-pan also stays idle while the menu is open, because the simulation view is
not the spectator's focus then, and while the match is over and the summary
panel is up.

## Motion

Speed is `min(MaximumScreenSpeed, distance * Responsiveness)` world units per
second, where `MaximumScreenSpeed` is expressed in screen pixels per second and
divided by zoom, exactly like manual panning. The distance term eases the camera
in as it arrives instead of stopping dead. The step is clamped so the camera can
never overshoot its target.

Auto-pan runs on unscaled presentation time, like every other client effect. It
does not gate, pause, or reorder simulation advancement, and it writes nothing
that reaches a state hash.

## Discoverability

The effect is self-evident: the view slides to the fighting on its own, and any
pan key stops it instantly. No HUD element is required to explain a camera that
moves to show the thing the spectator is there to watch. The behaviour is
recorded as a manual smoke-checklist row in `docs/development/testing.md` so a
person confirms it at a real desktop.

## Testability

All decisions live in pure `internal static` helpers in a new
`src/Hukbo.Client/ArenaAutoPan.cs`, taking `Vector2`, `Rectangle`, and
`IReadOnlyList<AgentView>` and returning plain values. `ArenaAutoPanController`
holds the two-state machine and the override timer; it constructs no graphics
device, sprite batch, or window, so tests build it directly.

`SpectatorCamera` gains a `Center` property, a `MoveCenterTo` method, a
`GetVisibleHalfExtents(Rectangle)` helper, and an `Update` return value that
reports whether manual pan input moved the camera this frame. `ArenaGame` only
wires the controller to the camera and the agent list.

## Scope

Out of scope: zoom changes of any kind, following a selected agent, cinematic
easing curves, a settings toggle, and any change to `Hukbo.Core`.
