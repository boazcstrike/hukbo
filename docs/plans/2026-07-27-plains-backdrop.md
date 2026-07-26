# Plains Battlefield Backdrop Plan

Design document: [2026-07-27-plains-backdrop-design.md](2026-07-27-plains-backdrop-design.md).

The design is settled. This plan is the ordered task list and the verification
criteria. Tasks run in the order listed; each one names the files it owns.

## Scope reminder

Presentation only. `Hukbo.Core` is not modified. No content pipeline entry is
added. No theme color role is added. This is not the Gate 3 terrain layer.

## Task 1 — Failing tests for the ground grid

**Files:** `tests/Hukbo.Client.Tests/PlainsBackdropGeometryTests.cs` (new).

Write tests against a `PlainsBackdropGeometry.GetGroundCells` helper that does
not exist yet, so the suite fails to compile. That compile failure is the RED
state for this task.

Cases:

1. Cells exactly cover the supplied map rectangle: the union of all cell
   rectangles equals the input rectangle.
2. No two cells overlap: the summed area of all cells equals the input
   rectangle's area.
3. Adjacent cells share an exact boundary coordinate: for every neighbouring
   pair, the right edge of one equals the left edge of the next, and likewise for
   top and bottom. Assert at map rectangles produced by zoom values 0.05, 0.37,
   1.0, 1.63, and 12.0.
4. Column and row counts respect the 48 ceiling for a very large map.
5. A degenerate map rectangle of zero width or height yields an empty result
   rather than throwing.

**Done when:** the test file exists, the intended assertions are written, and
`./scripts/test.ps1 -Configuration Release` fails on the missing type.

## Task 2 — Implement the ground grid

**Files:** `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs` (new).

Implement `GetGroundCells`. Compute the column boundary array of `columns + 1`
integers and the row boundary array of `rows + 1` integers first, then build
cells from consecutive boundary pairs, so contiguity holds by construction.
Column and row counts derive from the target cell size of 64 world units and are
clamped to the 48 ceiling.

Add the shade selector: a pure function mapping a cell's map-space column and
row plus the scenario seed to one of three shade indices, using `SplitMix64`.

Keep every function under 50 lines by extracting private static helpers.

**Done when:** all Task 1 tests pass.

**Depends on:** Task 1.

## Task 3 — Failing tests for decal generation

**Files:** `tests/Hukbo.Client.Tests/PlainsBackdropGeometryTests.cs` (append).

Cases against a not-yet-existing `GenerateDecals`:

1. Two calls with the same seed, map width, and map height produce equal
   sequences.
2. Two calls with different seeds produce sequences that are not equal as a
   whole.
3. The returned count never exceeds `MaximumDecalCount` across several seeds and
   across a very large map.
4. Every decal world position falls inside `[0, MapWidth]` by `[0, MapHeight]`.
5. All three decal kinds appear across a reasonable sample, so the kind selector
   is not degenerate.

**Done when:** the new assertions are written and the suite fails.

**Depends on:** Task 2 (same file, sequential).

## Task 4 — Implement decal generation

**Files:** `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs`.

Implement `GenerateDecals` using `SplitMix64` seeded from the scenario seed
mixed with the named presentation salt. Count is world area divided by the
per-decal area constant, clamped to `MaximumDecalCount`. Return an immutable
sequence of a `PlainsDecal` record struct carrying world position, scale factor,
and kind.

**Done when:** all Task 3 tests pass.

**Depends on:** Task 3.

## Task 5 — Failing tests for apparent-scale clamping

**Files:** `tests/Hukbo.Client.Tests/PlainsBackdropGeometryTests.cs` (append).

Cases against a not-yet-existing `GetDecalScreenBounds`:

1. At camera zoom 0.05 the resulting apparent scale is not below the minimum
   constant.
2. At camera zoom 12.0 it is not above the maximum constant.
3. Between the extremes the apparent scale is monotonically non-decreasing in
   zoom, mirroring `PawnGeometryTests` on the same property.
4. The returned rectangle is centred on the supplied screen anchor and has
   positive width and height at every tested zoom.

**Done when:** the new assertions are written and the suite fails.

**Depends on:** Task 4.

## Task 6 — Implement apparent-scale clamping

**Files:** `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs`.

Implement `GetDecalScreenBounds`, clamping with the same shape `PawnGeometry`
uses. If the file is approaching 400 lines at this point, split the decal
functions into `PlainsBackdropDecalGeometry.cs` before continuing.

**Done when:** all Task 5 tests pass and the full Client suite is green.

**Depends on:** Task 5.

## Task 7 — The draw sink

**Files:** `src/Hukbo.Client/Rendering/PlainsBackdropRenderer.cs` (new).

A thin static renderer that draws the ground cells then the decals. Colors are
derived per draw by interpolating between `theme.Colors.ArenaSurface` and
`theme.Colors.ArenaBorder` at the fixed values recorded in the design document.
Decals are culled with `arenaBounds.Intersects` exactly as `DrawPawns` does. The
draw path allocates nothing: iterate with `for` over the stored array, no LINQ,
no per-frame list building.

Not unit tested, matching `PawnRenderer.Draw`.

**Done when:** the project builds clean with zero warnings.

**Depends on:** Task 6.

## Task 8 — Wire it into the game

**Files:** `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`.

Add a `_plainsDecals` field alongside the existing presentation fields. Populate
it at the two places where `SpectatorCamera` is constructed today: initial
scenario construction and `ResetSimulation`. Do not regenerate anywhere else.

In `DrawMapSurface`, replace the single flat
`spriteBatch.Draw(pixel, visibleMapBounds, theme.Colors.ArenaSurface)` with a
call to `PlainsBackdropRenderer.Draw`, leaving the existing `DrawBorder` call
after it unchanged.

**Done when:** the game builds, and neither file exceeds the 800-line limit.

**Depends on:** Task 7.

## Task 9 — Manual smoke rows

**Files:** `docs/development/testing.md`.

Append new rows to the interactive checklist table, all with actual `Not run` and
status `PENDING`. Do not touch the recorded gate-evidence section.

Rows to add:

- Launch the game and confirm the battle floor shows varied ground shading with
  scattered grass, dirt, and stone marks rather than one flat color.
- Zoom fully out and fully in; confirm the ground pattern stays locked to the
  same patches of map, does not crawl or shimmer, and that decals neither vanish
  into flicker nor balloon into large blobs.
- Pan the camera across the map and confirm no seam lines, gaps, or overlapping
  bright edges appear between ground cells.
- Confirm pawn silhouettes, faction ground rings, selection marks, and hit
  effects all remain clearly readable against the backdrop.
- Cycle every theme and confirm each one produces a backdrop in its own palette
  with the arena border still distinguishable from the ground.
- Press `R` for a new round and confirm the backdrop changes with the new seed;
  press `Shift+R` for a full reset and confirm the seed-1 backdrop returns
  identical to the first launch.

Only a human running `./scripts/run.ps1` may change these to `PASS`.

**Depends on:** none for writing the rows; the human run depends on Task 8.

## Task 10 — Verification gate

Run, in order:

```powershell
./scripts/format.ps1 -Verify
./scripts/verify.ps1
```

Record the exact output. Do not claim a pass without pasting it.

Checks against the recorded baseline in `docs/development/testing.md`:

- The seed-1 200-agent workload must still end in `Faction1Victory` at tick 235
  with state hash `6EBB1EA63114F6CE` and event hash `941377BD43C556FF`. Any
  movement in either hash means the change has leaked into the simulation and is
  a defect, not a new baseline.
- Client and Core test counts must not decrease.
- Zero warnings and zero errors.

The headless allocation figure covers the simulation, not the render path, so it
should be unchanged. Report it either way.

**Depends on:** Task 8.

## Task 11 — Record and archive

Update the results section of `docs/development/testing.md` only if the gate
numbers differ from the recorded baseline, and add rather than overwrite.

When the work is integrated and the human smoke run is complete, move this plan
and its design document to `docs/archives/` and add the "Archived: reference
only" banner beneath each title, per `CLAUDE.md` section 6.

**Depends on:** Task 10 and the human smoke run.

## Risks carried into implementation

- `PlainsBackdropGeometry.cs` holds three responsibilities. Split it if it passes
  roughly 400 lines rather than letting it drift toward 800.
- The per-frame draw path must stay allocation free. Returning freshly built
  lists from `GetGroundCells` every frame would violate that; prefer filling a
  caller-owned buffer or returning a stack-friendly bounded structure.
- The 48-cell ceiling and the 256-decal cap must remain named constants. A
  derived, uncapped density is an unbounded cost and is forbidden.
- Nothing in this feature may reference `_simulation`, and no new `Hukbo.Core`
  type may be introduced. The only Core types used are the existing `Scenario`
  fields and `SplitMix64`.
