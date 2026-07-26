# Plains Battlefield Backdrop Design

## Goal

Replace the single flat color that currently fills the battle map with a plains
backdrop: a subtly varied ground surface scattered with small grass, dirt, and
stone marks. The battlefield should read as open ground rather than as an empty
rectangle, and it should do so without introducing a single new binary asset.

## What this is not

This backdrop is presentation only. It is **not** the terrain layer that
`CLAUDE.md` section 9 defers and that `SIMULATION-GAME-STANDARDS.md` places
behind Gate 3.

Concretely, the backdrop:

- adds no state to `Hukbo.Core` and does not reference any new Core type;
- does not affect movement, pathfinding, cover, line of sight, targeting,
  damage, retreat, or victory;
- never appears in a snapshot, a replay, the battle event stream, the state
  hash, or the event hash;
- carries no collision and no traversal cost.

A reviewer who sees the word "terrain" near this feature should read this
section first. Nothing here consumes the Gate 3 budget, and nothing here
authorizes starting the real terrain layer later. When gameplay terrain is
eventually built, it will own its own state in Core and this decorative layer
will either be driven by it or replaced by it.

## Approved approach

A hybrid of two procedural layers, both drawn from the existing runtime-generated
one-pixel white texture that `ArenaGame` already creates.

**Layer one, the ground grid.** The projected map rectangle is subdivided into a
fixed grid of cells. Each cell is filled with one of three shades. The shade is
chosen by hashing the cell's map-space column and row together with the scenario
seed, so a given patch of ground keeps its shade for the whole match no matter
where the camera moves or how far it zooms.

**Layer two, the scatter decals.** A bounded number of small marks — grass
tufts, dirt patches, and stones — are generated once per scenario at fixed world
positions and drawn on top of the grid. Each decal projects through the same
`SpectatorCamera.WorldToScreen` call that pawns use, so decals and pawns stay
locked to the same ground.

Both layers draw beneath pawns and beneath the existing arena border.

### Why not an authored texture

The repository has a standing decision, recorded in
`docs/agents/11-content-asset-pipeline.md`, that the only compiled content is the
sprite font and that everything else is generated at runtime. Every visual in the
game today — pawns, hit effects, panels, the event log — is built from tinted
rectangles drawn from that one white pixel.

Introducing a tiled ground bitmap would be the project's first image asset. It
would also require a wrapping sampler state, which conflicts with the
`SamplerState.PointClamp` convention used everywhere else, and a point-filtered
tiled texture shimmers badly across this camera's zoom range of 0.05x to 12x
without mipmaps. The decorative benefit does not justify that cost or that
inconsistency.

### Why no new theme color role

Adding a color role to `UiThemeColors` is a six-place lockstep change: the record
property, the `CreateTheme` read, the `GetColor` switch, the
`requiredColorRoles` list in `ui-theme-standards.json`, an entry in every one of
the five themes, and the catalog validation tests. That is a lot of churn for a
decorative gradient.

Instead the backdrop derives its shades algorithmically by interpolating between
the two ground colors each theme already defines, `ArenaSurface` and
`ArenaBorder`, using `Color.Lerp` at fixed interpolation values. This is the same
technique `PawnRenderer.ApplyState` already uses to derive selection and hover
tints. Every theme therefore gets a backdrop in its own palette for free, and no
theme file changes.

The interpolation values are a small fixed set. This design deliberately rejects
a continuous procedural luminance field, which would grow into a miniature noise
texture system that nothing currently needs.

## Determinism

The simulation's determinism contract is untouched because the backdrop never
reads or writes simulation state. It does, however, need to be stable across runs
so that the same battle looks the same every time it is replayed.

Decal generation uses `Hukbo.Core.Determinism.SplitMix64`, seeded from
`Scenario.Seed` mixed with a fixed presentation salt. `System.Random` is banned
repository-wide and is not used here. The salt keeps the backdrop's number
stream visibly distinct from any simulation stream; because the client never
feeds values back into Core, a collision would be harmless, but a named salt
makes the separation obvious to a reader.

Decals are generated exactly twice in the lifetime of a match: once when the
scenario is first constructed and once on each reset, at the same two places
where `SpectatorCamera` is constructed today. They are never regenerated per tick
or per frame, so nothing pops or crawls.

## Constants and their rationale

| Constant | Value | Rationale |
| --- | --- | --- |
| Target ground cell size | 64 world units | On the default 1280 by 720 map this yields a 20 by 12 grid, which is 240 fills per frame. Coarse enough to stay cheap, fine enough to break up the flat fill. |
| Maximum grid columns and rows | 48 each | Hard ceiling so an unusually large map cannot drive the per-frame fill count without bound. Worst case is 2,304 fills. |
| Ground shade count | 3 | Enough variation to read as ground, few enough to avoid visual noise. |
| Ground shade interpolation values | 0.00, 0.06, 0.12 toward `ArenaBorder` | Subtle. The backdrop must never compete with pawn silhouettes for attention. |
| Decal interpolation values | 0.10, 0.16, 0.22 toward `ArenaBorder`, for grass tuft, dirt patch, and stone | Spaced above the ground shades so a decal reads as a distinct mark rather than another cell, while staying inside the ceiling below. |
| Maximum backdrop interpolation | 0.22 | A named ceiling on every backdrop shade. The high-contrast theme uses a pure black surface and a pure white border, so an unbounded value would scatter mid-grey speckle across the one theme whose purpose is eliminating visual noise. A unit test asserts every ground and decal value stays within this bound. |
| World area per decal | 6,000 square units | About 153 decals on the default map, which reads as scattered rather than crowded. |
| Maximum decal count | 256 | A named hard cap, not a derived value. Density must never grow without bound as maps get larger. |
| Minimum decal apparent scale | 0.35 | Below this a decal is sub-pixel and only produces flicker. |
| Maximum decal apparent scale | 3.0 | Above this a decal reads as a large blob rather than ground detail. |

The cap being a named constant rather than a derived expression is deliberate.
A future contributor who ties decal density to map area without a ceiling would
silently reintroduce an unbounded per-frame cost, which `CLAUDE.md` section 9
forbids.

## Seam handling

The grid is a subdivision of an already-rounded screen rectangle, and adjacent
cells must not leave a one-pixel gap or overlap at fractional zoom levels.

The implementation computes the column boundary coordinates once as an array of
`columns + 1` integers and the row boundaries once as an array of `rows + 1`
integers. Each cell then spans from one boundary to the next. Because adjacent
cells read the identical boundary value rather than each rounding independently,
contiguity is exact by construction at every zoom level rather than by
approximation. The unit tests assert this numerically at 0.05x, 0.37x, 1x, 1.63x,
and 12x.

## Component boundary

Following the pure-helper pattern that `hukbo-client-ui` documents and that
`PawnGeometry` and `PawnRenderer` already demonstrate:

- `Rendering/PlainsBackdropGeometry.cs` is pure. It computes cell boundaries,
  generates decals, and clamps decal apparent scale. It touches only `Vector2`,
  `Rectangle`, and value types, so it is fully unit tested with no graphics
  device.
- `Rendering/PlainsBackdropRenderer.cs` is the draw sink. It consumes the pure
  results, derives colors from the active theme, culls against the arena bounds
  the way `DrawPawns` already does, and issues the `SpriteBatch` calls. It is not
  unit tested, matching how `PawnRenderer.Draw` is handled today.

If `PlainsBackdropGeometry.cs` approaches 400 lines it splits into separate grid
and decal files rather than growing toward the 800-line limit.

## Historical accuracy

The research corpus contains no evidence describing the visual appearance of
Philippine battlefield ground in the 1500s. `docs/research/battles/01-deep-past-overall-warfare.md`
records only that open ground was where groups could assemble and fight, and
`docs/research/HISTORICAL_1500s_WEAPONS.md` states plainly that terrain is
outside its evidentiary scope.

The backdrop is therefore labeled **Provisional reconstruction**. It depicts
generic open grassland and makes no claim about a specific region, decade, or
land use. It does not depict rice terraces, paddies, or any other culturally
specific landscape, because the sources do not support doing so. No player-facing
text names or identifies the ground.

## Nine questions

1. **User-visible outcome.** The ground beneath the pawns reads as varied open
   plains rather than a single flat color.
2. **Tick stage and state.** None. The backdrop participates in no tick stage and
   writes nothing to simulation state.
3. **Numeric units, bounds, same-tick conflicts.** Not applicable. Decal
   positions are in the same world units as agent positions and are bounded by
   the map dimensions that `Scenario.Validate` already guarantees are positive.
4. **Ordering and random stream.** One `SplitMix64` stream seeded from the
   scenario seed and a fixed presentation salt, separate from every simulation
   stream. Grid cells are filled in row-major order, a trivial total order.
5. **Cache source and invalidation.** Not a cache. The decal set is a
   fixed-capacity presentation array rebuilt wholesale on scenario creation and
   reset, and never persisted.
6. **Save, event, and version effect.** None. Absent from snapshots, replays, the
   event stream, and both hashes. No preset version change.
7. **Worst-case complexity.** Per frame, bounded by the grid ceiling plus the
   decal cap, independent of agent count. The draw path allocates nothing; the
   decal array is allocated twice per match.
8. **Spectator discoverability.** The backdrop is inert scenery with no
   autonomous behavior, so there is nothing hidden for a spectator to discover.
   The visual change itself is immediately apparent on launch.
9. **Tests.** Unit tests for cell boundary contiguity, exact coverage, and
   non-overlap at five zoom values; for decal reproducibility across identical
   seeds, divergence across different seeds, the count cap, and world-bounds
   containment; and for apparent-scale clamping at both zoom extremes. Manual
   smoke rows cover the visual result, which no automated test can judge.
