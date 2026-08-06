# Battlefield Environment Research: Grass, Ground, and Disturbance

Research only. This document compares techniques for making the battlefield
ground read as living grassland rather than tinted rectangles, within Hukbo's
constraints. It authorizes no implementation; a design document and plan under
`docs/plans/` must follow before any code changes.

Date: 2026-07-28. Investigated against MonoGame 3.8.5 DesktopGL, .NET 10, and
the repository state at commit `8a3d930`.

## 1. Constraints this research honors

- Presentation only. Nothing here may read into or feed the simulation; the
  state hash, event hash, replays, and snapshots are untouched.
- No per-blade objects. Grass is drawn as bounded batches of primitives, never
  as individually updated entities.
- `TreatWarningsAsErrors` repo-wide; tests stay GPU-independent using the
  pure-geometry-helper pattern (`PlainsBackdropGeometry` / `HitEffectGeometry`
  precedent).
- Offline build, no new network. New dependencies are reviewed changes; every
  option below assumes zero new packages.
- Target: 200 to 500 visible units at 1080p with headroom to spare, across the
  camera zoom range 0.05x to 12x (`SpectatorCamera.MinimumZoom` /
  `MaximumZoom`, `src/Hukbo.Client/SpectatorCamera.cs`).

## 2. Current state of the backdrop

The plains backdrop shipped on 2026-07-27 (design archived at
the plains backdrop design, reference
only). What exists today, verified in source:

- `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs` — pure helpers. A
  ground grid of up to 48 by 48 cells (64 world units per cell target), each
  cell shaded by hashing its column, row, and the scenario seed through
  `SplitMix64` with a presentation salt (`0x504C41494E530001`). Up to 256
  scatter decals (grass tuft, dirt patch, rock) generated once per scenario at
  fixed world positions, one decal per 6,000 square world units. Decal apparent
  scale clamps to [0.35, 3.0] against a 5-pixel base size.
- `src/Hukbo.Client/Rendering/PlainsBackdropRenderer.cs` — the draw sink. It
  loops the grid formula per frame without allocating, culls cells and decals
  against the arena bounds, clips decals to the map rectangle, and colors
  everything by `Color.Lerp(ArenaSurface, ArenaBorder, t)` with t capped at
  0.22 so the backdrop never competes with pawn silhouettes (and never speckles
  the high-contrast theme).
- Everything draws from the single runtime-generated one-pixel white texture
  inside one `SpriteBatch.Begin(Deferred, AlphaBlend, PointClamp, ...)` /
  `End` pair per frame — the arena layer in
  `src/Hukbo.Client/ArenaGame.Rendering.cs` (`DrawArenaLayer`). The UI layer is
  a second Begin/End pair. There is no compiled content beyond the sprite font;
  the recorded content-pipeline decision (cited in the archived design) is that
  everything else is generated at runtime.
- The client already consumes `Hukbo.Core.Determinism.SplitMix64` for
  presentation randomness. The concern that the client "may not need Core RNG"
  is already settled in practice: `PlainsBackdropGeometry` imports it today,
  seeded from `Scenario.Seed` XOR a named presentation salt. That precedent is
  the right one to extend, and it costs nothing — `SplitMix64` is a struct in
  an assembly the client already references, and the salt keeps the stream
  visibly separate from every simulation stream.
- Presentation animation already runs on frame elapsed time:
  `SwingAnimationSystem` ages swings by
  `gameTime.ElapsedGameTime.TotalSeconds` (`src/Hukbo.Client/ArenaGame.cs`
  line 258 feeds it), and `SwingAnimation.TotalSeconds` is a plain float
  constant. A grass sway clock would follow exactly this shape: a float
  accumulator advanced per frame in the client, never touching the simulation.

## 3. MonoGame 3.8.5 DesktopGL capability facts

Checked against the current MonoGame documentation (docs.monogame.net) rather
than answered from memory.

**Custom effects exist and work on DesktopGL.** Effects are written in HLSL
`.fx` files and compiled through the content pipeline (MGCB) or the standalone
MGFXC compiler with `/Profile OpenGL`; MonoGame translates them for OpenGL. On
the OpenGL profile the documented compatibility target is `vs_3_0` /
`ps_3_0`, selected with the standard `#if OPENGL` preprocessor block that every
MonoGame effect template ships with. DesktopGL itself requires only OpenGL 2.0
with `ARB_framebuffer_object`, or OpenGL 3.0.

**`SpriteBatch.Begin` accepts a custom `Effect`.** The signature is
`Begin(SpriteSortMode sortMode = Deferred, BlendState, SamplerState,
DepthStencilState, RasterizerState, Effect effect = null, Matrix?
transformMatrix = null)`. Two documented caveats matter here:

1. With the default `Deferred` sort mode, the effect is applied at
   `SpriteBatch.End`, so an effect parameter changed between `Draw` calls does
   not vary per sprite — only the last value set applies to the whole batch.
2. Varying parameters per sprite requires `SpriteSortMode.Immediate`, which
   disables batching entirely and issues a GPU draw per sprite. For hundreds
   of grass quads that is exactly the draw-call explosion this project must
   avoid.

The practical consequence: a SpriteBatch vertex shader can only vary per-tuft
behavior through data already in the sprite vertices — position, color, and
texture coordinates. Hukbo draws everything from a one-pixel texture, so
texture coordinates are degenerate, and color is already used for theming.
Per-tuft sway phase would have to be smuggled through a channel that is
currently doing real work.

**Custom vertex buffers and instancing exist in the API.**
`GraphicsDevice.DrawUserPrimitives` draws from a CPU vertex array;
`DrawInstancedPrimitives` plus `VertexBufferBinding(vertexBuffer, offset,
instanceFrequency)` provide hardware instancing. Instancing on the OpenGL
backend depends on the driver exposing instanced-array support (broadly
available on any hardware from the last decade, but above DesktopGL's minimum
GL 2.0 floor, so it is a capability the minimum-spec contract does not
currently promise). Both paths bypass SpriteBatch and require either
`BasicEffect` or a custom effect, plus hand-managed vertex declarations.

## 4. The three candidate techniques

### Option 1 — Shader-driven grass movement

A custom `.fx` effect displaces grass-quad vertices (or warps fragments) by a
time uniform, evaluated on the GPU.

- **MonoGame compatibility.** Supported on DesktopGL via `vs_3_0`/`ps_3_0`,
  but it would be the project's first compiled shader and the first compiled
  content beyond the sprite font, reversing a recorded content-pipeline
  decision. The build gains an MGFXC step (offline once tools are restored,
  but a real toolchain addition).
- **Per-tuft variation problem.** Under `Deferred` sorting, one uniform set of
  parameters applies to the whole batch, so all grass sways in lockstep unless
  phase is encoded into vertex data — impossible to do cleanly through
  SpriteBatch with a one-pixel texture and theme-tinted colors. Escaping
  SpriteBatch into custom vertex buffers fixes that but multiplies the
  complexity (vertex declarations, buffer management, a second render path
  with its own state).
- **Batching cost.** The arena layer today is one Begin/End. A different
  effect forces a batch break: End, Begin with the grass effect, draw grass,
  End, Begin again for pawns. Two extra Begin/End pairs, roughly two to four
  extra GPU draw calls — still cheap, but structurally invasive.
- **Testability.** The sway math lives in HLSL, invisible to xunit. A mirror
  C# implementation could be tested, but then two copies of the math must be
  kept in agreement by hand — the exact drift the single-formula rule in
  `PlainsBackdropRenderer` exists to prevent.
- **Visual value.** Highest ceiling: per-pixel wind, smooth gradients across
  blades. At this game's zoom range and 5-pixel decal scale, almost all of
  that ceiling is invisible.

### Option 2 — Batched procedural grass with shared CPU oscillation

Grass clusters are generated once per scenario as fixed world positions (like
today's decals). Each frame, a pure helper computes a small sway offset per
cluster from a client-side time accumulator and a per-cluster phase, and the
renderer draws each cluster as a handful of tinted rectangles (two to four
quads suggesting blades) at the offset position — all inside the existing
arena `SpriteBatch` batch, same one-pixel texture, same `Deferred` sort.

- **MonoGame compatibility.** Uses nothing beyond what ships today. No
  content-pipeline change, no effect, no new render path. Zero new packages.
- **Draw calls.** Zero additional GPU draw calls. SpriteBatch in `Deferred`
  mode batches every quad drawn from the same texture into the same GPU
  submission, and the grass uses the texture the whole arena already uses.
  What grows is the sprite-submission count, which is bounded by the cluster
  cap (section 8 budgets it).
- **CPU cost.** One sine (or cheaper triangle-wave) evaluation per visible
  cluster per frame. At the cap proposed below (320 clusters) that is
  hundreds of `MathF.Sin` calls — microseconds, allocation-free.
- **Testability.** Fully testable without a GPU. `GrassSwayOffset(timeSeconds,
  phase, amplitude)` is a pure function of value types; tests pin exact
  offsets at chosen times, assert the amplitude bound, assert phase
  determinism from the seed, and assert the zero-motion path returns exactly
  zero. This is the `HitEffectGeometry` pattern verbatim.
- **Visual value.** At normal and max zoom, clusters visibly sway with
  desynchronized phases, which is what sells "alive" at this art style's
  resolution. At minimum zoom the offsets round to zero pixels and the
  motion gracefully vanishes on its own (and should be explicitly gated off,
  see section 12).

### Option 3 — Static clusters plus a moving overlay layer

Today's static decals stay frozen; a second, sparser layer of light-colored
streaks drifts slowly across the field to suggest wind passing over grass.

- **MonoGame compatibility.** Same as option 2 — nothing new.
- **Draw calls.** Zero additional GPU draw calls, same reasoning; sprite count
  is lower than option 2 (a few dozen overlay streaks).
- **Testability.** Same pure-helper testability as option 2.
- **Visual value.** Weakest. The grass itself never moves, so the effect reads
  as weather passing over a painting rather than living ground. A translucent
  drifting overlay also fights the high-contrast theme (any overlay alpha is
  mid-grey speckle by another name) and risks reading as fog or artifacting
  at low zoom. Wind-gust overlays work best *on top of* moving grass, as a
  later enhancement, not instead of it.

### Comparison summary

| Criterion | 1. Shader | 2. Batched CPU sway | 3. Static + overlay |
| --- | --- | --- | --- |
| MonoGame 3.8.5 DesktopGL fit | Works, but first compiled shader + MGFXC step | Uses only existing machinery | Uses only existing machinery |
| Implementation complexity | High (content pipeline, batch breaks, or custom vertex path) | Low (extends existing geometry/renderer pair) | Low-medium (second layer, tuning risk) |
| Extra GPU draw calls | ~2–4 (batch breaks) | 0 | 0 |
| Extra memory | Compiled effect + possible vertex buffers | One cluster array (~10 KB), rebuilt twice per match | Two small arrays |
| GPU-independent testability | Poor (HLSL untestable; mirror-math drift) | Full (pure helpers) | Full (pure helpers) |
| Visual value at min zoom | None (sub-pixel) | None, degrades gracefully | Overlay risks reading as artifact |
| Visual value at normal/max zoom | Highest ceiling, mostly invisible at this art scale | Good — desynchronized cluster sway | Weak — ground stays dead |

## 5. Recommendation

**Option 2: batched procedural grass clusters with shared CPU-computed
oscillation.** It is the only option that adds motion to the grass itself while
adding zero GPU draw calls, zero content-pipeline changes, zero packages, and
zero untestable math. It is a straight extension of the
`PlainsBackdropGeometry` / `PlainsBackdropRenderer` pair the codebase already
trusts: generation stays seeded and pure, the per-frame path stays
allocation-free, and every new number is testable in xunit without a graphics
device. Option 1's genuine advantages (per-pixel wind) are invisible at a
5-pixel decal scale and cost the project its "one batch, one texture, no
compiled content" simplicity. Option 3 keeps the ground dead and adds the one
kind of element (translucent moving overlay) most likely to look wrong in the
high-contrast theme.

Option 1 remains the right tool *later* if the art style ever moves to real
textures and close-up zoom as the default framing; nothing in option 2's
geometry would be thrown away in that migration, because cluster placement and
phase assignment stay CPU-side either way.

## 6. Ground color and restrained texture variation

The current three-shade cell grid works but reads as a checkerboard at high
zoom because shade is independent per cell. Two refinements, in order of
preference:

1. **Value-noise-flavored shading, still per cell, still theme-derived.**
   Replace the independent per-cell hash with a hash that mixes the cell's
   neighbors (sample the four corner hashes of each cell and average), giving
   spatially correlated patches — large tonal drifts instead of confetti. This
   is a pure function of (column, row, seed), costs four hashes per cell
   instead of one, keeps the existing 0.22 interpolation ceiling, and needs no
   texture, no new colors, and no new theme roles. Deterministic from the
   scenario seed exactly as today.
2. **A static authored texture** is rejected for the same reasons the archived
   backdrop design rejected it: first image asset, wrapping sampler conflict
   with the `PointClamp` convention, and shimmer across a 0.05x–12x zoom range
   without mipmaps. Nothing has changed since that decision.

Seeding from the scenario seed (as today) rather than a static constant is
correct: the same battle replayed looks identical, different battles look
different, and no per-frame or wall-clock input ever participates.

## 7. Grass cluster distribution

Uniform random scatter (today's decals) produces even salt-and-pepper
coverage. Real grassland clumps. A deterministic, client-side cluster
placement that needs no new dependency:

- **Two-level generation from one `SplitMix64` stream.** First draw N cluster
  centers uniformly (N small, e.g. 24–48 scaled by map area with a hard cap).
  Then for each center, draw a per-center tuft count and scatter tufts around
  the center with a distance falloff (radius times the square root of a unit
  draw biases density toward the center). One stream, one pass, wholly
  reproducible from `Scenario.Seed` XOR a new named salt (keep the existing
  plains salt for the existing decals so their placement does not shift).
- The client should keep using Core's `SplitMix64` for this, as it already
  does. Inventing a second client-side RNG would add code to avoid a
  dependency the client already has, and `System.Random` is banned outright.
  The named-salt convention is the separation mechanism, and it is already
  documented in `PlainsBackdropGeometry`.
- Leave deliberate empty ground between clusters. Sparse and clumped reads as
  a real field; even coverage reads as wallpaper.
- Hard caps stay named constants, never derived expressions, per the archived
  design's rationale (an uncapped density formula is an unbounded per-frame
  cost waiting to be reintroduced).

## 8. Budgets

Proposed named caps, with worst-case arithmetic at 1080p:

| Item | Cap | Worst-case per frame |
| --- | --- | --- |
| Grass clusters | 320 | 320 sway evaluations (pure float math) |
| Quads per cluster | 4 | 1,280 sprite submissions |
| Ground grid (existing) | 48 x 48 cells | 2,304 fills (existing ceiling, unchanged) |
| Existing decals | 256 | 256 fills (unchanged) |
| Trample marks (section 10) | 128 | 128 fills |
| Dust puffs (section 14) | 32 live | ~64 fills |

Total worst case is roughly 4,000 sprite submissions in the arena batch, all
from one texture, which SpriteBatch turns into **zero additional GPU draw
calls** beyond the arena batch that already exists (SpriteBatch breaks a batch
only on texture or state change). SpriteBatch's internal buffer grows to fit
and is reused; at 4,000 sprites the vertex data is on the order of 300 KB of
long-lived reusable buffer, well inside budget. The cluster array itself is
about 320 entries of ~16 bytes — 5 KB, allocated twice per match (creation and
reset), never per frame. CPU cost of the sway pass is under 0.05 ms.

## 9. Trampled and sparse areas

Where fighting happens, grass should thin. Presentation-only mechanism: a
bounded, append-only list of trample marks in the client, fed by battle events
the client already receives (section 14). Each `Death` event (and optionally
each `Attack` at melee range) adds a trample mark at the source or target's
world position — a slightly darker, flattened ellipse drawn *under* grass
clusters, plus suppression: any grass cluster whose center lies within a
trample radius draws at reduced height and zero sway amplitude. The list caps
at 128 marks (oldest replaced), resets with the scenario, never persists, and
never feeds back into anything. Suppression is a pure distance test in a
helper, testable without a GPU. This converts the battle's own history into
visible wear with no new state anywhere authoritative.

## 10. Boundary readability

The arena border must stay the strongest line on the field. Rules to carry
into design: grass clusters clip to the map rectangle exactly as decals do
today (`Rectangle.Intersect` against `MapBounds` in the renderer); sway offset
must also be clipped so a swaying tuft never crosses the border; and a
grass-free margin of one ground cell (64 world units) just inside the border
keeps the boundary line clean. All three are testable as pure geometry.

## 11. Unit-ground contrast preservation

The existing 0.22 interpolation ceiling toward `ArenaBorder` is the load-
bearing contrast guarantee and must bind grass too: every grass shade,
trample shade, and dust shade stays at or below the ceiling, asserted by the
same style of unit test that pins the current shades. Motion adds a second
contrast risk — a moving background steals attention from moving pawns — so
sway amplitude must stay small (1–2 screen pixels at zoom 1) and slow
(sub-1 Hz), and the high-contrast theme should render grass with zero sway and
minimal shade spread, reusing that theme's existing "eliminate visual noise"
purpose as the trigger rather than inventing a new flag.

## 12. Culling, density scaling, and LOD by zoom

- **Culling.** Reuse the existing pattern: per-cluster screen-bounds test
  against `ArenaBounds` before drawing (as `DrawDecals` does). At 320
  clusters a linear cull pass is trivial; no spatial structure is warranted,
  and building one would violate the no-unbounded-cache rule for no gain.
- **LOD by zoom, three bands, chosen by `camera.Zoom` thresholds.** Far
  (below ~0.3): draw clusters as today's single-rectangle decals, no sway —
  sub-pixel motion is pure flicker. Mid (~0.3 to ~2): full clusters, sway on.
  Near (above ~2): full clusters, sway on, optionally one extra quad per
  cluster for silhouette. Band selection is a pure function of zoom, testable
  at the exact thresholds, mirroring how decal apparent scale already clamps.
- **Density scaling.** Density never scales with agent count or frame rate
  (both would be hidden coupling); it scales only with map area under the
  hard cap, as decals do today.

## 13. Reduced motion and disabling motion

No reduced-motion or motion-toggle setting exists in the client today
(verified by search across `src/`). The grass design should introduce the
seam now even if the UI arrives later: the sway helper takes an
`amplitudeScale` factor, where 0 disables motion entirely and must return
exactly `Vector2.Zero` (asserted by test), so the render path with motion off
is bit-identical to a static backdrop. When a settings surface exists (the
menu overlay is the natural home), a single "reduce motion" toggle should
gate grass sway, and the same factor gives an accessibility-friendly
half-amplitude mode for free. The high-contrast theme forces the factor to 0
regardless of setting, per section 11.

## 14. Dust and disturbed vegetation from existing gameplay events

The authoritative event stream (`BattleEventKind` in
`src/Hukbo.Core/Simulation/BattleEvent.cs`) carries exactly five kinds:
`Move`, `Attack`, `Damage`, `Death`, `Outcome`. Client presentation systems
already consume this stream per kind — `SwingAnimationSystem` (Attack),
`HitEffectSystem` (Damage, Death), `BloodEffectSystem` (Attack, Death) — each
spawning bounded, timed, client-only effects. Ground disturbance follows the
identical shape with no new events and no Core changes:

- `Death` → one trample mark (section 9) plus a brief dust puff at the
  agent's position.
- `Attack` → optional small dust kick at the source's feet, throttled the way
  `SoundCueMapper` already reasons about event frequency.
- `Move` fires for most living agents on most ticks and must not spawn
  per-event effects (the same reason `SoundCueMapper` keeps `Move` silent);
  if movement dust is ever wanted, derive it from agent speed sampled at draw
  time, not from `Move` events.
- `Outcome` → optionally stop spawning new dust so the end screen settles.

Dust puffs are short-lived (well under a second), capped at 32 live, drawn as
one or two expanding, fading rectangles in the ground shade range — the
`HitEffectSystem` lifecycle pattern with different colors and durations.

## 15. Palette notes: tropical Philippine lowland plausibility

For color plausibility the reference landscape is lowland open ground in the
1500s archipelago: cogon grassland (*Imperata cylindrica* — olive to
yellow-green in growth, straw-gold to tawny when dry, often in large uniform
sweeps) and the margins of wet-rice land (mud browns, standing-water glints,
vivid young-green edges). Two constraints temper this:

- **The theme system owns color.** All backdrop shades derive from
  `ArenaSurface`/`ArenaBorder` lerps so all five themes work, including
  high-contrast. Grass should keep that derivation. If the *default* theme's
  ground should shift toward cogon olive-gold, that is a theme-color tuning
  change reviewed on its own, not a hard-coded palette in the renderer.
- **The historical-accuracy policy applies.** The research corpus records no
  evidence of battlefield ground appearance for the period
  (`docs/research/HISTORICAL_1500s_WEAPONS.md` scopes terrain out). Grassland
  depiction stays labeled **Provisional reconstruction**, depicts generic
  open ground, claims no region or land use, and no player-facing text names
  the vegetation. Rice terraces and paddies specifically must not be
  depicted, per the archived backdrop design's reasoning.

## 16. Risks

- **Motion distraction** is the biggest product risk: sway that reads as
  noise under 300 moving pawns. Mitigations are baked in above — small
  amplitude, slow frequency, LOD-off at far zoom, zero sway in high-contrast,
  the amplitude-zero seam — but only the manual smoke checklist
  (`docs/development/testing.md`) can judge the result, and those rows stay
  `PENDING` until a human looks.
- **Sprite-count creep**: every cap in section 8 must be a named constant with
  a test, or density will grow unbounded the first time someone enlarges a
  map.
- **Seed drift**: reusing the existing plains salt for new generation would
  silently reshuffle today's decals; new features take new named salts.

## 17. Source summary

Repository evidence: `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs`,
`src/Hukbo.Client/Rendering/PlainsBackdropRenderer.cs`,
`src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/ArenaGame.cs`,
`src/Hukbo.Client/SpectatorCamera.cs`,
`src/Hukbo.Core/Simulation/BattleEvent.cs`,
`src/Hukbo.Client/Presentation/{SwingAnimationSystem,HitEffectSystem,BloodEffectSystem,BattleEventFeed}.cs`,
`src/Hukbo.Client/Audio/SoundCueMapper.cs`, and the archived design
the plains backdrop design (reference
only). MonoGame API facts: current MonoGame documentation via context7
(custom effects and MGFXC `/Profile OpenGL`, `vs_3_0`/`ps_3_0` OpenGL
profiles, `SpriteBatch.Begin` effect parameter and Deferred-versus-Immediate
semantics, `DrawUserPrimitives`, `DrawInstancedPrimitives`,
`VertexBufferBinding` instance frequency, DesktopGL OpenGL 2.0+FBO / 3.0
minimum).
