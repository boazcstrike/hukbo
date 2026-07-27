# Battlefield Environment Design — Ground, Grass, and Motion

Date: 2026-07-28. Workstreams 4 and 5 of the Hukbo visual improvement pass.

## Status

Draft design, awaiting review. This document consumes
`docs/research/improve-visuals/battlefield-environment-research.md`,
`docs/agents/improve-visuals/requirements.md` (workstreams 4 and 5 and the
R-X cross-cutting requirements), and
`docs/agents/improve-visuals/existing-code-analysis.md`. It is authored in
parallel with `visual-system-integration-design.md`, which owns the shared
settings, diagnostics, and render-measurement infrastructure this design
depends on. A plan document must follow before any code changes.

On 2026-07-28 the user resolved all ten package open decisions — including
OD-9 (dust downgraded to MAY; VIS-029 unblocked but optional), OD-8 (the
MotionIntensity setting governs all ambient presentation motion), and OD-6
(the default theme's ground shifts toward cogon olive-gold this pass,
provisional-tagged) — and approved the 23-task first milestone for
implementation. The decision record is in the package README.

## Scope

In scope:

- Ground shading improvement: spatially correlated seeded color variation
  replacing the independent per-cell hash, inside the existing lerp caps.
- Grass: batched procedural grass clusters with shared CPU-computed sway —
  the research's recommended option 2 — including deterministic clumped
  placement, trampled/sparse areas, caps, culling, density scaling, and LOD
  by zoom.
- Wind and motion: a presentation-only sway clock on client frame time, a
  reduced-motion setting following the GoreIntensity pattern, zero sway in
  the high-contrast theme, and bounded amplitude and frequency.
- Boundary readability and unit-ground contrast preservation.
- Optional (MAY) scope: dust puffs and disturbance driven by existing battle
  events.

Out of scope:

- Any change to `Hukbo.Core`, simulation state, hashes, events, or RNG
  streams (R-X.11). Everything here is client presentation.
- Shaders, compiled effects, content-pipeline entries, textures, and new
  packages (R-W4.5, R-W6.18).
- Per-blade grass objects or per-blade updates (R-X.14).
- Terrain, elevation, pathfinding, or any ground feature the simulation could
  read — the ground remains purely decorative.
- Rice terraces and paddies, which must not be depicted (R-W4.9).
- Theme color retuning. Per OD-6, resolved 2026-07-28, the default theme's
  ground shifts toward cogon olive-gold this pass, provisional-tagged — but
  it remains a theme-tuning change carried out on its own, not part of this
  renderer work. Exploration of jungle/plains ground treatments is recorded
  as a backlog item in `docs/plans/TODO.md`.

## Current state

Verified against the existing-code analysis and environment research:

- The backdrop is a static flat-color system: a ground grid of up to 48 by 48
  cells, each shaded by one of three lerp steps (0.00 / 0.06 / 0.12) from
  `ArenaSurface` toward `ArenaBorder`, hashed per cell from the scenario seed
  XOR the named plains presentation salt through `SplitMix64`; plus up to 256
  square scatter decals (grass tuft, dirt patch, rock) at lerp values up to
  the hard ceiling `MaximumBackdropInterpolation = 0.22`, generated exactly
  twice per match (creation and reset).
- Everything draws from the single runtime 1x1 white texture inside the one
  arena `SpriteBatch.Begin(Deferred, AlphaBlend, PointClamp)` / `End` pair
  per frame. The logic lives in the tested pure helper
  `PlainsBackdropGeometry`; the untested draw-only sink is
  `PlainsBackdropRenderer` — the established split this design extends.
- There is no motion of any kind in the backdrop, no wind, no grass blades.
  Presentation animation elsewhere already runs on frame elapsed time
  (`SwingAnimationSystem` via `AdvanceEffects`), which is the timing shape
  sway will copy.
- No reduced-motion or motion-off setting exists anywhere in the client; the
  GoreIntensity chain (pinned enum, nullable raw settings field, schema
  version, manager with persist delegate, menu selector, tests) is the fully
  worked precedent for adding one.
- No render or draw-call benchmark exists; the simulation benchmark measures
  ticks, not frames. Every performance number in this document is therefore
  an estimate.
- The camera zoom range is 0.05x to 12x; decal apparent scale clamps to
  [0.35, 3.0].

## Evidence

- Technique evidence: the environment research's MonoGame 3.8.5 DesktopGL
  capability review (custom effects, SpriteBatch effect semantics under
  Deferred versus Immediate sorting, instancing availability) and its
  three-option comparison, checked against current MonoGame documentation
  rather than memory.
- Repository evidence: `PlainsBackdropGeometry` / `PlainsBackdropRenderer`,
  `ArenaGame.Rendering.cs`, `SpectatorCamera`, the presentation effect
  systems, and the `BattleEventKind` stream (`Move`, `Attack`, `Damage`,
  `Death`, `Outcome`).
- Historical evidence: none exists for battlefield ground appearance in the
  period. The reference landscape (cogon grassland, lowland open ground) is
  plausibility guidance only. Everything ground-related is labelled
  **Provisional reconstruction** in metadata; no player-facing text names the
  vegetation, a region, or a land use (R-W4.9), and the theme system — not a
  hard-coded palette — remains the owner of all color.

## Requirements

This design implements R-W4.1 through R-W4.12 and R-W5.1 through R-W5.10, and
is bound by the R-X cross-cutting requirements. The dust scope deviation this
design originally recorded (dust puffs scoped MAY where R-W4.8 said MUST) is
resolved: per OD-9, resolved 2026-07-28, R-W4.8 is amended to MAY, so this
design's optional scoping is now the requirement's own. The most load-bearing
requirements:

- R-W4.2: the 0.22 interpolation ceiling binds every new shade.
- R-W4.4 / R-W6.14: every cap is a named constant with a test, never a
  derived expression.
- R-W4.5: zero GPU draw calls added; one texture, one arena batch, no
  shaders, no content pipeline, no packages.
- R-W5.5: an amplitude scale of zero returns exactly `Vector2.Zero`, making
  the motion-off path bit-identical to a static backdrop.
- R-W6.15: the seed-1 state hash, event hash, outcome, and event stream are
  untouched — nothing here can read into or feed the simulation.

## Alternatives considered

The research compared three techniques for living ground. Recorded here in
full because the choice shapes everything downstream.

### Option 1 — Shader-driven grass movement

A custom HLSL `.fx` effect (compiled via MGFXC for the OpenGL profile)
displaces grass vertices by a time uniform on the GPU.

- Would be the project's first compiled shader and first compiled content
  beyond the sprite font, reversing a recorded content-pipeline decision and
  adding an MGFXC toolchain step.
- Under `SpriteSortMode.Deferred` an effect parameter applies to the whole
  batch, so per-tuft phase variation is impossible without either smuggling
  phase through vertex channels that are already doing real work (color is
  theme tint; texture coordinates are degenerate on a 1x1 texture) or
  switching to `Immediate` sorting, which destroys batching entirely.
- Forces batch breaks in the one-Begin/End arena layer (roughly two to four
  extra GPU draw calls) or a whole custom vertex-buffer render path.
- The sway math would live in HLSL, invisible to xunit; a mirrored C# copy
  would reintroduce exactly the duplicated-formula drift the backdrop
  renderer's single-formula rule exists to prevent.
- Its genuine ceiling (per-pixel wind gradients) is invisible at a 5-pixel
  decal scale across this game's zoom range.

**Rejected.** Re-entry criteria: (a) the art direction moves to real textures
with close-up framing as the default, and (b) the render measurement harness
exists and shows the CPU sway pass or sprite-submission growth actually
breaching budget. Nothing in option 2 is thrown away in that migration —
cluster placement and phase assignment stay CPU-side either way.

### Option 2 — Batched procedural grass clusters with shared CPU oscillation

Clusters generated once per scenario at fixed world positions; each frame a
pure helper computes a small sway offset per cluster from a client time
accumulator and a per-cluster phase; the renderer draws each cluster as two
to four tinted quads at the offset position, inside the existing arena batch,
from the existing texture.

- Uses nothing beyond what ships today: no effect, no pipeline change, no
  packages, no new render path.
- Zero additional GPU draw calls — Deferred SpriteBatch batches every quad
  from the same texture into the same submission; only the bounded
  sprite-submission count grows.
- CPU cost is one trigonometric evaluation per visible cluster per frame —
  hundreds of calls, microseconds, allocation-free.
- Fully GPU-independent testable: the sway function is pure value math, the
  placement is a pure seeded function — the `HitEffectGeometry` /
  `PlainsBackdropGeometry` pattern verbatim.
- At mid and near zoom, desynchronized cluster sway is what sells "alive" at
  this art scale; at far zoom the offsets round to zero pixels and the motion
  is explicitly gated off anyway.

**Chosen.**

### Option 3 — Static clusters plus a moving overlay layer

Grass stays frozen; sparse light streaks drift across the field suggesting
wind.

- Same machinery cost as option 2, but the ground itself never moves, so the
  effect reads as weather passing over a painting.
- A translucent drifting overlay is mid-grey speckle by another name in the
  high-contrast theme and risks reading as fog or artifacting at low zoom.

**Rejected as a substitute.** Re-entry criterion: as a later *enhancement on
top of* moving grass (a wind-gust pass), which the research ranks as the only
context where an overlay works; recorded as out of scope for this pass
(R-W5.10).

## Recommended approach

### Ground shading

Replace the independent per-cell shade hash with spatially correlated
shading: hash the four corners of each cell (column/row lattice points, with
the scenario seed and a new named corner-lattice salt) and average the four
corner values to pick the cell's interpolation step. Properties, all preserved or
pinned by test:

- Corner lattice hashes mix the scenario seed with a **new named
  corner-lattice salt** — never the existing plains salt
  (`0x504C41494E530001`), per the rule that new features take new named salts
  and never reuse an existing one (R-W6.2). The existing decals stay under
  the old salt, and a test pins that their placement is unchanged.
- Still a pure function of (column, row, scenario seed) — deterministic per
  match, camera-independent, recomputable per frame with zero allocation via
  the single tested formula the renderer calls directly.
- Produces large tonal drifts instead of per-cell confetti — the
  checkerboard read at high zoom disappears.
- Stays inside the existing shade ladder and the 0.22 ceiling; still derived
  from `Color.Lerp(ArenaSurface, ArenaBorder, t)` so all five themes,
  including high-contrast, keep working with no new colors and no new theme
  roles.
- Costs four hashes per cell instead of one — bounded by the unchanged 48x48
  grid cap.
- Tropical-lowland palette intent (cogon olive-gold) is expressly *not*
  hard-coded here. Per OD-6, resolved 2026-07-28, the default theme's ground
  does shift toward cogon olive-gold this pass, with the tuning values
  tagged provisional — carried out as a theme-tuning change on its own,
  through the theme system, never as a renderer palette.

Restrained texture variation means exactly this correlated-shading change
plus the grass clusters below. No authored ground texture: the archived
backdrop design's rejection (first image asset, sampler conflict with
`PointClamp`, shimmer across the 0.05x–12x zoom range) stands unchanged.

### Grass clusters

**Generation** (once per scenario, at creation and reset only — never per
tick or frame):

- Two-level placement from one `SplitMix64` stream seeded by
  `Scenario.Seed` XOR a **new** named salt constant (the existing plains
  salt is not reused, so today's decals and ground shades do not shift —
  the seed-drift rule). First draw cluster centers uniformly across the map
  — a center count scaled by map area with a hard cap; then for each center
  draw a per-center tuft count and scatter tufts around it with square-root
  radial falloff (radius times the square root of a unit draw), biasing
  density toward the center. Clumps with deliberate empty ground between
  them, not uniform noise (R-W4.11).
- Each cluster stores: world position, phase (drawn from the same generation
  stream, satisfying R-W5.3), size class, and tuft layout — roughly 16 bytes
  per entry, in a flat array.
- Named cap constants with tests: at most **320 clusters**, at most **4
  quads per cluster**, cluster-center count in the 24–48 range scaled by map
  area under the cap. Density scales only with map area — never with agent
  count or frame rate (both would be hidden coupling).

**Rendering** (per frame, allocation-free):

- Pure geometry helper computes each visible cluster's quads (two to four
  tinted rectangles suggesting blades); the renderer stays a draw-only sink
  looping the tested formula, exactly the `PlainsBackdropGeometry` /
  `PlainsBackdropRenderer` split.
- All grass shades are `Color.Lerp(ArenaSurface, ArenaBorder, t)` with t at
  or below the 0.22 ceiling, pinned by the same style of test as the current
  shade pins (R-W4.2).
- Everything draws inside the existing arena Begin/End pair from the 1x1
  texture: zero additional GPU draw calls.

**Culling:** a linear per-cluster screen-bounds test against the arena panel
bounds before drawing, as `DrawDecals` does today. At 320 clusters no spatial
structure is warranted, and building one would violate the no-unbounded-cache
rule for no gain.

**Boundary readability:** three rules, all pure-geometry testable — clusters
clip to the map rectangle exactly as decals do; sway offsets are also
clipped so no tuft ever crosses the border; and a grass-free margin of one
ground cell (64 world units) just inside the border keeps the border the
strongest line on the field.

**LOD by zoom** — three bands selected purely by camera zoom, thresholds as
named constants tested at exact values:

| Band | Zoom | Behavior |
| --- | --- | --- |
| Far | below ~0.3 | Clusters draw as single static rectangles (today's decal form); sway fully off — sub-pixel motion is pure flicker. |
| Mid | ~0.3 to ~2 | Full clusters, sway on. |
| Near | above ~2 | Full clusters, sway on, optionally one extra silhouette quad per cluster (within the 4-quad cap). |

### Trampled and sparse areas

A bounded, client-only trample list fed by authoritative events the client
already receives: each `Death` event (and optionally each melee-range
`Attack`, throttled) appends a mark at the agent's world position — a
slightly darker flattened ellipse drawn under grass, shaded within the 0.22
ceiling. Suppression: any cluster whose center lies within a trample radius
draws at reduced height and **zero** sway amplitude. Fixed capacity of **128
marks**, oldest replaced; resets with the scenario; never persists; never
feeds back into anything. Suppression is a pure distance test in a helper,
GPU-independent testable. This converts the battle's own history into visible
wear with no new state anywhere authoritative — the spectator can correlate
thinned ground with the fight they watched.

### Wind and motion

- **The sway function:** `GrassSwayOffset(timeSeconds, phase,
  amplitudeScale)` — a pure function of value types returning a `Vector2`
  offset, evaluated per visible cluster per frame, allocation-free. A sine
  (or cheaper triangle wave, decided at implementation with a pinned choice)
  of sub-1 Hz frequency and an amplitude of at most 1–2 screen pixels at
  zoom 1. Amplitude and frequency bounds are named `PROVISIONAL` constants
  with tests pinning them.
- **The clock:** a client-side float accumulator advanced by frame elapsed
  time in the `AdvanceEffects` / `SwingAnimationSystem` pattern. It never
  touches the simulation, no simulation value depends on it, and it does not
  scale with playback speed (grass is ambiance, not gameplay communication).
- **Phase:** per-cluster, drawn deterministically from the cluster
  generation stream, so two replays of the same seed show identical phase
  assignment.
- **The off switch is exact:** `amplitudeScale = 0` returns exactly
  `Vector2.Zero` (asserted by test), so the motion-off render path is
  bit-identical to a static backdrop.
- **High-contrast theme:** forces the amplitude factor to 0 regardless of
  any setting, and grass renders with minimal shade spread — reusing that
  theme's eliminate-visual-noise purpose as the trigger rather than
  inventing a new flag.
- **Trample interaction:** suppressed clusters sway at zero amplitude.
- Presentation-only in every direction: sway reads nothing from the
  simulation beyond what the client already renders, and nothing it computes
  is ever stored, hashed, snapshotted, or read back.

### Reduced-motion setting

A new setting following the GoreIntensity precedent end to end (the concrete
chain — enum with pinned numeric values and a do-not-renumber comment,
nullable field on `RawClientSettings` validated independently, schema version
bump 3 → 4 with backward-compatible load, manager with injected persist
delegate, menu selector, and manager/selector/store round-trip tests — is
specified in `visual-system-integration-design.md`, which owns the settings
infrastructure; this design consumes the resulting value). Levels: Off /
Reduced / Full, where Off maps to amplitude factor 0, Reduced to a
half-amplitude factor, and Full to 1. Per OD-8, resolved 2026-07-28, the
MotionIntensity setting governs all ambient presentation motion — grass sway
now, dust (if it ships under OD-9) and future ambient motion included;
gameplay-communicating animation (swing, hit effects) stays exempt.

### Dust and disturbed vegetation (optional scope — MAY)

Scoped as optional, and the requirement now agrees: per OD-9, resolved
2026-07-28, R-W4.8 is amended from MUST to MAY, so the pass is complete
without dust and task VIS-029 is unblocked but optional. If dust ships, the
MotionIntensity setting at Off suppresses dust spawning entirely and Reduced
leaves dust unchanged. If included, it follows
the established event-driven presentation shape (the `HitEffectSystem` /
`BloodEffectSystem` precedent) with no new events and no Core changes:

- `Death` → one brief dust puff at the agent's position (alongside the
  trample mark).
- `Attack` → optionally a small throttled dust kick at the attacker's feet.
- `Move` → never spawns per-event effects (it fires for most living agents
  most ticks — the same reason the sound mapper keeps `Move` silent).
- `Outcome` → stop spawning new dust so the end screen settles.

Puffs are sub-second, capped at **32 live** (fixed pool, named constant),
drawn as one or two expanding fading rectangles within the ground shade
range and the 0.22 ceiling.

Note: `docs/agents/improve-visuals/requirements.md` originally listed dust
(R-W4.8) as MUST while this design deliberately recorded it as MAY on
direction from the orchestrator; the discrepancy was surfaced under Open
decisions rather than silently absorbed, and the user resolved it on
2026-07-28 (OD-9) by amending R-W4.8 to MAY.

### Budgets — ESTIMATES pending the measurement harness

**No render benchmark exists today**, so every number below is an ESTIMATE
carried from the research's counted (not measured) arithmetic, to be
reconciled against the render measurement harness required by R-W6.12 and
specified in `visual-system-integration-design.md`:

- Extra sprite submissions, worst case: grass 320 × 4 = 1,280, trample 128,
  dust ~64 — roughly **1,300–1,500 added**, bringing the arena batch's
  backdrop total to roughly **4,000** worst case alongside the existing
  2,304-cell grid and 256 decals. All from one texture in one Deferred
  batch: **zero additional GPU draw calls**.
- Cluster array memory: ~320 entries × ~16 bytes ≈ **5 KB**, allocated twice
  per match (creation and reset), never per frame.
- Sway CPU: under **0.05 ms** per frame at the full cluster cap.
- Steady-state heap allocation in the draw and sway paths: **zero** after
  scenario setup.

If measurement contradicts an estimate, the caps shrink — the caps are the
contract, the estimates are not.

## Rejected approaches

- **Option 1 (shader-driven grass)** — rejected with re-entry criteria as
  recorded under Alternatives considered.
- **Option 3 (static grass plus moving overlay)** — rejected as a substitute
  for moving grass, with re-entry only as a later gust enhancement layered on
  top of option 2.
- **Authored ground texture** — rejected again for the archived backdrop
  design's unchanged reasons (first image asset, sampler conflict, zoom
  shimmer). Re-entry criterion: a wholesale art-direction change to textured
  rendering with measurement in place.
- **Per-blade grass entities** — forbidden outright by R-X.14; no re-entry
  criterion within this architecture.
- **Density derived from agent count or frame rate** — rejected as hidden
  coupling; density scales only with map area under hard caps.
- **A spatial index for cluster culling** — rejected; linear scan over ≤320
  entries is trivial and an index is an unbounded-cache hazard. Re-entry
  criterion: a measured cull cost that actually shows up in the harness.

## Dependencies

- `visual-system-integration-design.md` (authored in parallel): the
  reduced-motion settings chain (R-W6.6–R-W6.8), the salt-registry
  convention that guarantees the new grass salt never collides with the
  plains or appearance salts (R-W6.2), the diagnostics conventions
  (R-W6.5), and above all the render measurement harness (R-W6.12) that
  converts this design's ESTIMATE budgets into evidence.
- `docs/research/improve-visuals/battlefield-environment-research.md` — the
  technique evidence and option comparison this design adopts.
- `docs/agents/improve-visuals/requirements.md` — the binding requirement
  set (R-W4.x, R-W5.x, R-X.x).
- Existing code: `PlainsBackdropGeometry` / `PlainsBackdropRenderer` (the
  pair this design extends), `SpectatorCamera` (zoom bands),
  `PresentationCoordinator` and the effect systems (the event-driven and
  frame-time patterns), and the GoreIntensity settings chain (the precedent
  the new setting copies).

## Risks

- **Motion distraction** is the biggest product risk: sway that reads as
  noise under 300 moving pawns. Mitigations are structural — 1–2 px
  amplitude, sub-1 Hz frequency, LOD-off below ~0.3 zoom, zero sway in
  high-contrast, the exact-zero off switch — but only the manual smoke
  checklist can judge the result, and those rows stay `PENDING` until a
  human looks.
- **Sprite-count creep:** every cap must be a named constant with a test or
  density grows unbounded the first time someone enlarges a map (the
  anti-density-creep rule, R-W6.14).
- **Seed drift:** reusing the existing plains salt would silently reshuffle
  today's decals; the new grass salt is a distinct named constant, and a
  test pins that existing decal placement is unchanged.
- **Estimate risk:** all budgets are counted, not measured; if the
  measurement harness lands late, implementation proceeds against caps, not
  against frame-time claims, and no performance claim is made without
  harness output.
- **Checkerboard trade:** correlated shading could flatten the ground too
  much (large near-uniform patches); the shade ladder and cell size are the
  tuning levers, and the manual row "ground reads as living grassland, not
  checkerboard, at all zooms" is the judge.

## Open decisions

- **OD-9 — Dust scope (deviation from R-W4.8). Resolved 2026-07-28:** R-W4.8
  is amended from MUST to MAY by user approval; task VIS-029 is unblocked
  but optional. If dust ships, the decided relationship to the setting is:
  `MotionIntensity` Off suppresses dust spawning entirely; Reduced leaves
  dust unchanged.
- **OD-6 — Default theme ground tint. Resolved 2026-07-28:** the default
  theme's ground shifts toward cogon olive-gold this pass, tuning values
  tagged provisional — a theme-color tuning change carried out on its own,
  not part of this renderer work. Exploration of jungle/plains ground
  treatments is recorded as a backlog item in `docs/plans/TODO.md`.
- **OD-8 — Reduced-motion scope. Resolved 2026-07-28:** the MotionIntensity
  setting governs all ambient presentation motion — grass sway now, dust and
  future ambient motion included; gameplay-communicating motion stays
  exempt. Owned by the integration design's settings chain.
- **OD-W4-a — Trample `Attack` feed.** Whether melee `Attack` events (throttled) also
  create trample marks, or `Death` only. Recommended default: `Death` only
  in the first pass; `Attack` throttling adds tuning surface with little
  added read. Not blocked on any package decision; decided in the plan.
- **OD-W4-b — Wave shape.** Sine versus triangle wave for the sway oscillator — a
  pure implementation detail, but the choice must be pinned by test either
  way so the formula never drifts silently. Not blocked on any package
  decision; decided at implementation.

## Acceptance criteria

Automated (GPU-independent xunit, pure helpers only; renderers stay untested
draw-only sinks):

- Shade-ceiling pins: every grass, trample, and dust shade at or below 0.22
  interpolation toward `ArenaBorder`.
- Correlated-shading determinism: same (column, row, seed) yields the same
  shade; corner-averaging formula pinned; existing decal placement unchanged
  under the old salt.
- Cluster placement determinism and caps: same seed yields identical
  cluster positions, phases, and counts; caps (320 clusters, 4 quads, 24–48
  centers, 128 trample marks, 32 dust puffs if included) are named constants
  with pin tests; density scales with map area only.
- Boundary geometry: clusters clip to the map rectangle; the one-cell
  grass-free margin holds; sway offsets never cross the border (tested at
  maximum amplitude).
- Sway math: `GrassSwayOffset` exact values pinned at chosen times;
  amplitude bound ≤ 2 px at zoom 1; frequency bound < 1 Hz;
  `amplitudeScale = 0` returns exactly `Vector2.Zero`; phase determinism
  from seed.
- LOD bands: band selection tested at the exact zoom thresholds; far band
  draws static single rectangles with zero sway.
- High-contrast forcing: amplitude factor 0 and minimal shade spread under
  the high-contrast theme, by test.
- Trample: suppression distance test; capacity and oldest-replacement test;
  `Death` adds a mark, `Move` never does; scenario reset clears the pool.
- Dust (if included): lifecycle, cap, and event-mapping tests in the
  `HitEffectSystem` test style.
- Allocation: helper paths allocation-free in steady state where the harness
  can assert it.
- The canonical gate passes end to end with the recorded seed-1 state hash,
  event hash, outcome, and event stream untouched.

Measurement evidence (not a test): the R-W6.12 render measurement harness
run across the requirement matrix (200/500 units × min/fit/max zoom × grass
on/off × motion on/off), reconciling every ESTIMATE above; grass-off and
motion-off configurations must measurably reduce or equal the on
configurations.

Manual checklist rows (added to `docs/development/testing.md` as `PENDING`;
only a human at an interactive desktop may flip them):

- Ground reads as living grassland, not checkerboard, at all zooms.
- The arena border remains the strongest line on the field.
- Trampled areas visibly thin where fighting happened.
- Sway reads as alive, not as noise, under 300 moving pawns.
- No motion visible at minimum zoom.
- The high-contrast theme shows zero motion.

This document does not authorize implementation. Implementation authority
for the 23 milestone tasks comes from the user's dated approval of
2026-07-28, recorded in the package README.
