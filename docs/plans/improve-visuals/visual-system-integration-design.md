# Visual System Integration Design

Date: 2026-07-28. Part of the visual improvement package
(`docs/plans/improve-visuals/README.md`).

## Status

Design for planning. This document decides the shared infrastructure that the
five sibling design documents — weapon visuals, shield visuals, warrior
appearance, battlefield environment, and the implementation plan draft —
depend on. It is written against the requirements in
`docs/agents/improve-visuals/requirements.md` (workstream 6 and the
cross-cutting R-X rules in particular) and the verified code state in
`docs/agents/improve-visuals/existing-code-analysis.md`. It authorizes no
implementation.

On 2026-07-28 the user resolved all ten package open decisions — including
OD-4, confirming this design's fully procedural direction — and approved the
23-task first milestone for implementation. The decision record is in the
package README (`docs/plans/improve-visuals/README.md`).

## Scope

In scope, as infrastructure the other designs consume:

1. The rendering strategy decision: sprite atlas versus continued procedural
   shapes.
2. Visual catalog architecture: stable variant identity, validation, and
   naming conventions.
3. Deterministic variant selection: the salted mixing recipe and its rules.
4. The fallback chain and missing-definition diagnostics.
5. The layer ordering model for composed pawns: anchoring, origins, rotation,
   attachment points, and orientation.
6. Animation state boundaries and how new layers respect them.
7. Zoom LOD: mapping composed layers onto the existing three detail tiers.
8. Batching and draw-call control.
9. Settings (reduced motion), high-contrast behavior, and color-blind
   redundancy.
10. The testing strategy shared by all workstreams.
11. The performance measurement plan and the budget discipline.

Out of scope, deliberately: which weapon variants, shield skins, appearance
presets, and ground features exist and what they look like. Those are the
sibling documents' subjects. This document gives them the rails; it does not
choose their content.

## Current state

Verified against source on 2026-07-28; full detail with file and line
references in `docs/agents/improve-visuals/existing-code-analysis.md`.

- **Rendering is fully procedural.** Every pawn, decal, and effect draws from
  a single runtime-created 1x1 white `Texture2D`. There is no texture or
  sprite loading anywhere in the game; `Content.mgcb` contains exactly six
  spritefonts and nothing else. Audio bypasses the pipeline via
  `SoundEffect.FromStream`.
- **Two sprite batches per frame.** The arena layer runs
  `SpriteSortMode.Deferred`, `AlphaBlend`, `PointClamp`, with a scissor
  rectangle; the UI layer runs Deferred, AlphaBlend, `LinearClamp`. Because
  everything samples one texture, the arena is effectively one GPU batch;
  cost is the per-`Draw` submission count. `layerDepth` is always 0; draw
  order is call order.
- **Appearance is a pure function.** `PawnAppearanceFactory.Create(entityId,
  weapon, shield)` runs for every live pawn every frame. It XORs `EntityId`
  with three named salt constants, runs each through a
  SplitMix64-finalizer-style mix, and takes modulo selections for stature,
  build, head treatment, and colors. The pinned rule: equipment identity
  (weapon role, shield role) comes only from `CombatLoadout`, never from
  `EntityId`.
- **Zoom drives three detail tiers.** Camera zoom spans 0.05x to 12x;
  `apparentScale = clamp(cameraZoom * 1.35, 0.72, 2.40) * scaleMultiplier`
  selects Low (below 0.95), Medium (below 1.80), or High (at or above 1.80).
  `PawnRenderer.GetBounds` is pose-blind by contract, so the drawn-pawn set
  never depends on animation phase.
- **Settings precedent.** `GoreIntensity` is the fully worked pattern for a
  visual setting: pinned enum, nullable field on `RawClientSettings` with
  independent validation, schema-version bump with backward-compatible load,
  a manager with an injected persist delegate, a menu selector, and tests.
  The current schema version is 3.
- **Diagnostics precedent.** `Hukbo.Diagnostics.DiagnosticLog` writes JSON
  Lines to `artifacts/logs/`; the `assets` channel already carries
  `assets.sound.missing`, `assets.theme.fallback`, and their siblings as
  `const` identifiers on `LogEvents`.
- **Themes.** 27 semantic color roles across five validated themes, with a
  high-contrast theme protected by the backdrop interpolation ceiling of
  0.22. Faction pawn colors are fixed constants (blue 64,164,255; red
  255,91,105; gold 231,199,84), theme-independent by recorded design intent.
- **No render benchmark exists.** `scripts/benchmark.ps1` measures simulation
  ticks, not frames. `tools/` holds only hand-run audio and balance
  harnesses, none in the solution or the gate.

## Evidence

- `docs/agents/improve-visuals/existing-code-analysis.md` — the code ground
  truth this design builds on, itself verified against source, including
  `src/Hukbo.Client/Rendering/PawnGeometry.cs`,
  `src/Hukbo.Client/Rendering/PawnRenderer.cs`,
  `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`,
  `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs`,
  `src/Hukbo.Client/ArenaGame.Rendering.cs`,
  `src/Hukbo.Client/SpectatorCamera.cs`,
  `src/Hukbo.Client/Settings/ClientSettingsStore.cs`, and
  `src/Hukbo.Diagnostics/LogEvents.cs`.
- `docs/agents/improve-visuals/requirements.md` — the 88 requirements; this
  design answers R-W6.1 through R-W6.18 directly and provides the machinery
  for R-X.1 through R-X.16, R-W1.3, R-W2.3, R-W3.5, R-W4.3, and R-W5.3.
- `docs/research/improve-visuals/battlefield-environment-research.md` — the
  technique comparison (shader versus batched CPU sway versus overlay) and
  the MonoGame 3.8.5 DesktopGL capability facts checked against current
  documentation.
- `docs/research/improve-visuals/weapons-shields-historical-research.md` and
  `docs/research/improve-visuals/warrior-appearance-historical-research.md` —
  the three-channel rendering constraint (silhouette, color blocks, one or
  two accents) and the evidence-labelling obligations every catalog entry
  carries.
- `SIMULATION-GAME-STANDARDS.md` section 4 (deterministic simulation
  contract) and section 10 (the nine feature acceptance questions and
  reviewer checklist).
- `CLAUDE.md` sections 5 (non-negotiables), 6 (workflow), and 7 (historical
  accuracy policy).

## Requirements

This design must satisfy, and the sibling designs inherit:

- **Catalogs and identity**: R-W6.1 (immutable client-side catalogs with
  stable pinned identifiers), R-W6.2 (the salted selection recipe with new
  named salts), R-W6.3 (appearance stays a pure per-frame function; the only
  presentation-state exceptions are the ones the requirements name).
- **Fallback and diagnostics**: R-W6.4 (a total, testable resolution chain
  ending in a conspicuous placeholder), R-W6.5 (missing-definition
  diagnostics on the `assets` channel at `warn`, once per distinct
  identifier, allocation rules honored).
- **Settings**: R-W6.6 through R-W6.8 (reduced motion via the GoreIntensity
  pattern, schema 3 to 4, independent field validation).
- **Accessibility**: R-W6.9 through R-W6.11 (0.22 ceiling, zero motion in
  high-contrast, no worsening of faction hue dependence, theme contrast
  validation keeps passing).
- **Performance**: R-W6.12 through R-W6.14 (a hand-run render measurement
  harness before budget enforcement; all budgets marked ESTIMATE until
  measured; every cap a named, tested constant).
- **Verification**: R-W6.15 through R-W6.18 (gate passes with real output;
  pure-geometry testing; manual rows created `PENDING`; no new packages or
  content-pipeline entries).
- **Cross-cutting**: the readability priority order (R-X.1 through R-X.5),
  the historical metadata obligations (R-X.6, R-X.7), and the prohibitions
  (R-X.11 through R-X.16).

## Alternatives considered

The load-bearing decision is how the new visuals are produced and drawn.
Three strategies were considered end to end; two further options recur inside
individual concerns and are treated in their sections.

### Alternative A — Sprite atlas through the content pipeline

Author real sprite art (weapon blades, shield faces, garments, grass) into
one or more texture atlases, add them to `Content.mgcb`, and draw pawns as
composed textured quads.

- Visual ceiling is the highest: real silhouettes, painted texture, per-pixel
  detail at high zoom.
- Requires everything the codebase deliberately does not have: texture
  loading and lifetime management, atlas packing and coordinate management,
  sampler decisions against the arena's `PointClamp` convention across a
  0.05x-12x zoom range without mipmaps (documented shimmer risk in the
  archived backdrop design), a real fallback story for missing regions, and
  the project's first non-font compiled content.
- Breaks the "one texture, one batch" performance profile. Every additional
  texture is a potential batch break, and **no render measurement exists to
  regress against** — the profile would change before the baseline was ever
  recorded.
- Art authoring is a new external dependency in workflow terms: someone must
  produce, license, and revise the art. The historical accuracy policy makes
  this harder, not easier — authored art asserts more than procedural
  primitives do, so every sprite would carry a heavier evidence burden
  (R-X.15 also forbids copying or tracing any external image).
- GPU-independent testing weakens: what a sprite looks like is not testable
  in xunit at all, whereas procedural layout geometry is fully testable.

### Alternative B — Runtime-loaded textures, bypassing the pipeline

Load PNG files at runtime the way audio loads WAV files
(`Texture2D.FromStream`), avoiding `Content.mgcb` changes.

- Shares every substantive drawback of Alternative A (art authoring, sampler
  and shimmer decisions, batch profile change, untestable visuals, heavier
  evidence burden) while adding runtime file I/O failure modes to the render
  path and losing the pipeline's build-time validation.
- The audio precedent does not transfer cleanly: a missing sound degrades to
  silence; a missing texture degrades to a hole in the scene, which is why
  R-W6.4's fallback chain exists at all.

### Alternative C — Continued procedural shapes (recommended)

Extend the existing `PawnGeometry` / `PawnRenderer` and
`PlainsBackdropGeometry` / `PlainsBackdropRenderer` pattern: all new visuals
are rectangles, line segments, and accents computed by pure geometry helpers
and filled from the existing 1x1 texture inside the existing arena batch.

- Zero new assets, zero new packages, zero content-pipeline changes, zero
  new GPU draw calls, zero new failure modes at load time.
- Every layout decision is a pure function testable without a GPU — the
  established and enforced testing pattern.
- The research is unanimous that this is sufficient: at pawn scale a
  spectator reads silhouette, color blocks, and one or two accent marks, and
  nothing finer (both historical research documents state this as the
  three-channel constraint; the environment research reaches the same
  conclusion for grass at a 5-pixel decal scale).
- The visual ceiling is genuinely lower than an atlas. The designs accept
  that ceiling for this pass and record re-entry criteria below.

## Recommended approach

### 1. Rendering strategy: stay fully procedural

**Decision: Alternative C.** All six workstreams render procedurally from the
existing single white pixel, inside the existing arena batch, with all layout
computed in pure geometry helpers. This is the path the environment research
recommends, the existing-code analysis strongly supports, and the user
confirmed as OD-4, resolved 2026-07-28. Every sibling design document is
written to this decision and must not hedge toward sprites.

**Re-entry criteria for the atlas alternative.** The sprite/atlas direction
is rejected *for this pass*, not forever. It becomes worth re-opening only
when all of the following hold:

1. The render measurement harness (section 11) exists and has recorded a
   procedural baseline on named hardware, so a changed profile has something
   to regress against.
2. A concrete visual requirement exists that the three procedural channels
   (silhouette, color block, accent mark) demonstrably cannot express at the
   zoom range players actually use — established by the manual checklist, not
   assumption.
3. An art sourcing and licensing answer exists that satisfies R-X.15 and the
   historical accuracy policy's evidence burden for authored depictions.
4. The fallback chain, catalog identity, and diagnostics infrastructure from
   this design are already in place — they are texture-agnostic by
   construction and would carry over unchanged.

Point 4 is deliberate: nothing in this design's catalog, selection, fallback,
or LOD architecture assumes procedural drawing. A future atlas would replace
the *drawable* a catalog entry resolves to, not the catalog, the selection,
or the chain.

### 2. Visual catalog architecture

Every variant any workstream ships is an entry in a client-side, immutable,
in-code catalog, following the `PawnAppearance` precedent (presentation
metadata lives in the Client, never in Core).

**Shape.** New code under `src/Hukbo.Client/Presentation/Catalogs/`. Each
catalog is a static class exposing a `static readonly` array (or
`ImmutableArray`) of entry records constructed once. An entry carries, at
minimum:

- `Id` — the stable string identifier (naming below), pinned forever.
- `Index` — the stable ordinal used by modulo selection, pinned forever.
- `DisplayLabel` — player-facing text, pair form where a cultural
  identification is made (R-X.6).
- `EvidenceTier` — the existing `WeaponEvidenceTier` values extended as
  needed with the explicit "presentation-only, no historical claim" marker
  (R-X.7).
- `ScopeTag` — Visayan, Tagalog, Cagayan, or Unscoped-generic where the
  entry depicts culture (R-X.8 rule 10); not applicable for pure-presentation
  entries like condition tints.
- `Notes` — the evidence note or source anchor surfaced by the inspector
  (`Mactan — 1521` style, R-X.10).
- `MinimumDetailTier` — Low, Medium, or High: the tier at which the entry's
  silhouette contribution first draws (section 7).

Sibling designs add domain fields (tint values, layout offsets, component
category) but never remove or repurpose these.

**Identifier naming convention.** *Post-decision amendment (2026-07-28,
review finding RF-05): this paragraph originally specified all-lowercase
three-segment identifiers; it is amended per the decided resolution so the
grammar matches the identifier tables the weapon and shield designs shipped,
because those tables are the reviewed content and their IDs are pinned
forever at VIS-002.* Dotted segments:
`<domain>.<family>.<variant>`, with camelCase within a segment where a name
has more than one word, and an optional `tint.` sub-segment between family
and variant for presentation-only tint variants. Domains are `weapon`,
`shield`, `appearance`, and `backdrop`. Canonical examples from the sibling
designs: `weapon.kampilan.tint.freshIron`, `shield.tallHardwood.mactanThin`,
`appearance.headcovering.putong`, `backdrop.grass.cluster`. The VIS-002
validation regex must match the identifier tables the sibling designs ship.
The research documents' anchor codes (K1, S1, C2, category letters) map into
the `<variant>` segment or its notes so a catalog entry is traceable to its
research paragraph. Identifiers are machine keys in the `LogEvents` spirit:
never reworded, never renumbered, never carrying a value. A `do not renumber
or reword` doc comment sits on every catalog, mirroring the `CombatIdentity`
enum comments.

**There is no asset directory in this pass.** The catalogs are code, because
the drawables are code. The convention above is still written down now so
that (a) diagnostics and inspector text have stable keys today, and (b) if
the atlas re-entry ever happens, asset files mirror the identifier scheme
(`Content/Sprites/weapon/kampilan/k1.png`) without inventing a second naming
system.

**Validation, twice.** First, a startup validation pass in the Client, run
once at load in the `UiThemeCatalog` style: every catalog checks identifier
uniqueness, index contiguity, mandatory metadata presence (evidence tier on
every entry; scope tag on every cultural entry), and the combination rules
that apply to it (the warrior-appearance validator of R-W3.4). A failure
logs on the `assets` channel and falls back per section 4 — it never
crashes the game and never silently drops an entry. Second, the same checks
as GPU-independent xunit tests, plus the pins startup cannot express: exact
identifier strings for every shipped entry (so a reword fails a test), the
preset count floor (R-W3.2), and at least one negative test per prohibition
(R-X.8). The startup pass protects the player; the tests protect the
contract.

### 3. Deterministic variant selection

The selection recipe is the existing `PawnAppearanceFactory` mechanism,
generalized and named as the single sanctioned pattern (R-W6.2):

1. Take the stable identity input. For per-warrior traits that is
   `EntityId`; for per-match features (grass placement, ground shading) it is
   `Scenario.Seed`; where the roster design scopes preset blocks by faction,
   `FactionId` participates as a documented additional input (R-W3.5).
2. XOR with a **new named salt constant, one per trait stream**. New salts
   never reuse the three existing appearance salts (`0xA0761D6478BD642F`,
   `0xE7037ED1A0B428DB`, `0x8EBC6AF09C88C6E3`) or the plains salt
   (`0x504C41494E530001`), so existing appearance and decals do not shift.
3. Mix through the SplitMix64-finalizer pattern (per-warrior traits use the
   private `Mix` shape `PawnAppearanceFactory` already has; scenario-scoped
   generation uses `Hukbo.Core.Determinism.SplitMix64` as the backdrop
   already does).
4. Select by modulo against the catalog's pinned entry count, or consume the
   stream sequentially for generation (grass placement). Distinct bit
   windows of one stream may feed related sub-traits, as the factory does
   today.

**Salt registry.** All presentation salts move behind (or are listed by) a
single static registry class, e.g.
`src/Hukbo.Client/Presentation/PresentationSalts.cs`, each with a doc
comment naming its trait stream. A test asserts all registered salts are
pairwise distinct. This turns "never reuse a salt" from a review habit into
a failing test.

**What is banned as a variation source**, restated because every workstream
touches it: `System.Random` (banned repo-wide), `object.GetHashCode`
(unstable across runs and versions), dictionary or set iteration order,
frame counters, and the wall clock. Equipment identity — which weapon, which
shield — comes only from `CombatLoadout`, never from any mix stream; the
existing `PawnAppearanceFactoryTests` pins stay in force and are extended to
every new trait (variant streams may *style* the weapon the loadout names,
never choose it).

**Stability contract.** For a fixed `(EntityId, CombatLoadout, FactionId,
Scenario.Seed)` the selected variants are identical every frame, every run,
every replay of the same seed, on every machine. This is automated-testable
and is required by R-W1.3, R-W2.3, R-W3.5, R-W4.3, and R-W5.3.

### 4. Fallback chain and missing-definition diagnostics

**The chain (R-W6.4).** Every visual lookup resolves through one pure, total
resolution function per domain:

1. **Specific variant** — the catalog entry the selection recipe picked.
2. **Weapon (or component) default** — the family's designated default entry
   (index 0 by convention), used when the selected index has no entry or the
   entry fails validation.
3. **Model-category default** — the generic drawable for the category: the
   plain grip-and-blade weapon lines with default proportions, the plain
   shield block, the unadorned torso, the bare ground cell. These are
   exactly today's drawables, which is why the chain is cheap: step 3 always
   exists because it is the current game.
4. **Diagnostic placeholder** — a deliberately conspicuous primitive (a
   solid block in a fixed garish color reserved for this purpose, never
   theme-derived, never invisible, never a crash), drawn at the element's
   layout position. Reaching step 4 emits the diagnostic below.

Resolution must be total: for every valid `(EntityId, CombatLoadout,
FactionId)` some drawable resolves, enforced by a test that walks every enum
value plus deliberately out-of-range inputs. In the shipped procedural world
steps 2-4 are nearly unreachable — enums are closed and catalogs are code —
and that is fine: the chain is cheap insurance that becomes load-bearing the
moment catalogs grow data-driven entries or the atlas re-entry happens, and
step 4 plus its diagnostic is how a future bad build announces itself
instead of drawing nothing.

**Diagnostics (R-W6.5).** New constants on `LogEvents`, `assets` channel,
`warn` level, following the four enforced rules (const on `LogEvents`, six
leading fields in order, flat camelCase payload, zero allocation when
disabled):

- `assets.visual.variantMissing` — the selection resolved past step 1;
  payload: `catalogId`, `requestedId`, `resolvedStep`.
- `assets.visual.fallback` — the placeholder (step 4) was reached; payload:
  `catalogId`, `requestedId`.
- `assets.visual.catalogInvalid` — a startup validation failure; payload:
  `catalogId`, `reason` (a stable reason code, not prose; free prose goes in
  the optional `msg`).

Emission is once per distinct missing identifier per session, not per frame:
a small fixed-capacity seen-set (capacity a named constant, on the order of
64 entries; when full, further distinct identifiers stop logging — bounded
by construction, honoring the no-unbounded-cache rule). The seen-set check
happens before any payload work so the disabled and already-seen paths
allocate nothing.

### 5. Layer ordering model for composed pawns

The composed pawn extends the existing back-to-front draw inside
`PawnRenderer.Draw`, keyed off `PawnLayout`. The full order, with the new
layers the sibling designs will populate marked:

| # | Layer | Tier | Status |
| --- | --- | --- | --- |
| 1 | Ground faction ring with shadow inset | Low+ | Existing |
| 2 | Secondary equipment behind the torso (Wasay head; Itak off-hand at Medium+) | Low+/Medium+ | Existing |
| 3 | Torso: outline pass, then body/clothing color passes | Low+ | Existing, colors extended |
| 4 | **Armor layer**: torso-capsule thickening and material color | Silhouette Medium+, tone Low+ | New (warrior appearance) |
| 5 | **Sash line** | Medium+ | New (warrior appearance) |
| 6 | Shield block (after torso so it overlaps; skins and posture per the shield design) | Low+ (always) | Existing, extended |
| 7 | Head disk: outline, then skin color | Low+ | Existing |
| 8 | Head treatment: hair and head coverings | Medium+ | Existing, options extended |
| 9 | **Adornment accents** (gold pixel, accent marks) | High | New (warrior appearance) |
| 10 | Swing arc trail | Medium+ | Existing |
| 11 | Weapon: grip line, blade line, highlight line (variants per the weapon design) | Low+ | Existing, extended |
| 12 | Status overlays: hit-pulse color blend, dead X, selection and hover marks | Low+ (always last) | Existing |

Rules that bind every layer, new and old:

- **Feet anchoring.** The pawn anchors at the feet: the ground ring center is
  the world-position projection, and stature and build multipliers grow the
  figure upward and outward from that anchor, so a tall figure's head rises
  rather than its feet sinking. This matches the warrior-appearance
  research's category A rule ("every figure anchors at the feet") and is a
  testable property of `PawnLayout`.
- **Origins and rotation.** Rectangles are axis-aligned fills; anything
  angled (weapon, trail segments, angled shield posture) is expressed as
  line endpoints or rotated draws computed in geometry, never ad hoc in the
  renderer. The weapon rotates about its grip anchor under the swing pose,
  exactly as today.
- **Attachment points.** `PawnLayout` grows named anchor fields — at minimum
  a weapon grip anchor and a shield anchor — so the weapon and shield
  designs attach to points the layout owns instead of re-deriving offsets.
  Anchors are pure layout outputs and therefore testable.
- **Left/right orientation.** The existing fixed-side convention stands:
  weapon on its established side, shield on the other, no mirroring. Facing
  is deliberately out of scope (recorded in `PawnGeometry`); a future facing
  channel is a `SwingPose` extension, and no new layer may depend on facing
  existing. New layers must therefore either be symmetric or commit to a
  fixed side.
- **Nothing displaces the Low-tier reads.** Layers 1, 6, 11, and 12 are the
  R-X.1 protected set; no new layer may occlude them at Low tier.

### 6. Animation state boundaries

Three time-varying systems exist, and the boundary between them and the new
static layers must stay sharp:

- `SwingAnimationSystem` advances on frame time **multiplied by playback
  speed** (attacks arrive at playback speed; unscaled swings would render
  every warrior permanently mid-swing at 4x).
- `HitEffectSystem`, `BloodEffectSystem`, and `ClashEffectSystem` advance on
  **unscaled** presentation seconds — "wounds already dealt".
- All of them ingest authoritative events in `IngestTick` and age in
  `AdvanceEffects`, in fixed-capacity pools, reset with the scenario.

Rules for the new work:

1. **Composed appearance layers are time-invariant.** Body, clothing, armor,
   sash, shield skin, head treatment, and adornments are pure functions of
   identity and zoom. They do not read the swing phase, the effect clocks,
   or any accumulator. Only the weapon layer (and the existing trail)
   consumes `SwingPose`, exactly as today.
2. **Pose-blind culling is preserved.** No new layer may feed animation
   state into `PawnRenderer.GetBounds`; any layer that grows a pawn's
   possible extent grows the pose-independent bound (R-X.5).
3. **New time-varying visuals are new presentation systems**, never new
   inputs to the appearance function. Dust and trample (battlefield design)
   follow the `HitEffectSystem` lifecycle shape; the grass sway clock is a
   client-side float accumulator in the `AdvanceEffects` pattern.
4. **The clock-scaling rule generalizes**: motion that communicates gameplay
   cause-and-effect in flight scales with playback speed (swings); ambient
   and aftermath motion does not (hit rings, blood, dust, grass sway). Each
   sibling design declares which clock its motion uses, citing this rule.
5. **Hit-pulse coherence.** The 0.09-second white hit pulse blends into pawn
   colors via the existing single blend point. New color layers route
   through the same blend so a pulsing pawn pulses as one object, not as
   mismatched patches.

### 7. Zoom LOD: composed layers on the three detail tiers

The existing machinery is kept unchanged: `apparentScale` clamps to
[0.72, 2.40] times the scale multiplier and selects Low below 0.95, Medium
below 1.80, High at or above 1.80. Tier assignment for every new element is a
pure function of apparent scale, testable at exactly 0.95 and 1.80 (R-X.4).

The readability priority order at distance is fixed (R-X.1, R-X.2):
**faction > weapon role > shield presence > state marks > body and clothing
variation.** Mapped onto tiers:

- **Low tier** draws the protected set only: faction ring, torso and head
  masses, shield block, weapon silhouette, status overlays. New work may
  contribute at most sub-threshold tone shifts here (a variant tint that
  does not change silhouette classification). Every Kampilan-armed pawn
  stays classifiable as Kampilan-armed at Low tier regardless of variant.
- **Medium tier** adds the silhouette-bearing composition: head treatments
  and coverings, armor thickening, sash, secondary equipment, swing trail,
  weapon variant accents (lashing band), shield skin accents.
- **High tier** adds the fine reads: belts, adornment accents, edge-wear
  highlight variation, densest tone work.

Each catalog entry's `MinimumDetailTier` field (section 2) is the single
source of this mapping, so the tier gate is data the tests can walk rather
than logic scattered through the renderer. The backdrop keeps its own
parallel banding: decal apparent scale clamps to [0.35, 3.0] as today, and
grass uses the three camera-zoom bands (far below ~0.3 static and swayless,
mid ~0.3-2 full with sway, near above ~2 full with optional extra silhouette
quad) defined in R-W5.6 with thresholds as named tested constants.

### 8. Batching and draw-call control

**The invariant: the arena stays one `Begin`/`End` pair on one texture, and
workstreams 1-5 add zero GPU draw calls.** Everything new draws from the
existing 1x1 pixel inside the existing deferred arena batch; deferred
batching on a single texture folds all of it into the existing submission.
No new `Begin`/`End`, no `SpriteSortMode.Immediate`, no custom `Effect`, no
second texture. `layerDepth` stays 0; ordering stays call order per
section 5.

What grows is the CPU-side sprite submission count, and that is where
control lands:

- **Submission caps are named constants with tests.** The backdrop caps from
  the environment research (grass clusters ≤ 320, quads per cluster ≤ 4,
  trample marks ≤ 128, live dust ≤ 32, existing 48x48 grid and 256 decals
  unchanged) and a per-pawn worst-case primitive count per tier that the
  geometry tests pin, so per-pawn creep is caught in xunit without a GPU.
  Today's counted per-pawn order is 10-25 submissions depending on tier and
  state; the new layers add single-digit counts at Medium/High and
  approximately zero at Low, and the exact pinned numbers are set by the
  sibling designs within the whole-frame budget.
- **Whole-frame budgets (ESTIMATE until measured, section 11)**: arena batch
  submissions ≤ 12,000 at 200 units and ≤ 20,000 at 500 units, against
  today's counted order of 3,000-8,000 pawn submissions plus roughly 2,600
  backdrop fills, with the grass worst case adding about 1,700.
- **Zero per-frame heap allocation in steady state.** Catalogs are static;
  selection is pure arithmetic; layout uses value types and caller-owned
  buffers (the `SwingPoseResolver` precedent); per-frame paths build no
  strings — all inspector and label text is precomputed into catalog entries
  at construction. Generation-time allocation (cluster arrays, pools)
  happens only at scenario creation and reset. The measurement harness
  verifies the steady state with GC counters; the existing allocation
  discipline (fixed pools, zero-allocation grid loop) is the model.

### 9. Settings, high-contrast, and color-blind redundancy

**Reduced motion (R-W6.6, R-W6.7).** A new setting copies the GoreIntensity
chain end to end:

- A `ReducedMotion`-style enum (final name to the planner) with explicit
  pinned numeric values, a do-not-renumber persisted-contract comment, and
  at least the members Off / Reduced / Full — mapping to amplitude factors
  0, one-half, and 1 consumed by the sway helper, where factor 0 must return
  exactly `Vector2.Zero` (R-W5.5).
- A nullable field on `RawClientSettings`, validated independently so a
  corrupt or missing value resolves to its default without losing the saved
  theme or any other field (R-W6.8).
- Schema version bump 3 to 4 with backward-compatible load of version-3
  files.
- A manager with an injected persist delegate, persisting on change, not
  rolling back on save failure; a menu selector; tests for manager,
  selector, and store round-trip including a version-3-file migration test
  and a corrupt-field test.
- The setting's scope is decided: per OD-8, resolved 2026-07-28, the
  MotionIntensity setting governs all ambient presentation motion — grass
  sway now, with dust (if it ships under OD-9) and any future ambient motion
  included. The enum and plumbing above are unchanged by that scope; the
  decision fixes the wording and documentation. Gameplay-communicating
  motion (swings, hit effects) stays exempt.

**High-contrast behavior (R-W6.9).** Two existing precedents extend,
test-pinned: every new backdrop and effect shade obeys the 0.22 lerp ceiling
toward `ArenaBorder`, and the high-contrast theme forces the motion
amplitude factor to 0 regardless of the setting, reusing that theme's
eliminate-visual-noise purpose rather than inventing a new flag. Theme
contrast-pair validation in `UiThemeCatalog` must keep passing for all five
themes with any new theme-derived colors (R-W6.11).

**Color-blind shape redundancy (R-W6.10).** The fixed faction constants stay
as designed. The binding rule on this pass is the no-regression floor:
faction remains distinguishable by the ground-ring shape-and-position
channel, and no new variant may make garment or ground hues a competing
faction signal — the warrior-appearance palette rule (faction color stays on
the ring and outline, never on garments) is the enforcement point, and the
combination validator carries a check for it. A genuinely shape-redundant
faction marker beyond the ring was open decision OD-7, resolved 2026-07-28
as deferred (backlog item in `docs/plans/TODO.md`), and is not designed
here.

### 10. Testing strategy

Four legs, all inherited by the sibling designs:

1. **Pure geometry and value helpers.** Every new layout, offset, tier gate,
   sway offset, placement, suppression distance, and resolution function is
   a pure function over value types in a geometry or presentation helper,
   tested in the `PawnGeometry` / `PlainsBackdropGeometry` /
   `HitEffectGeometry` pattern. Renderer classes remain untested draw-only
   sinks; any formula found in a renderer is a defect by convention. Client
   tests never construct `ArenaGame`, a graphics device, a sprite batch, or
   a window (R-W6.16).
2. **Catalog validation tests.** The section 2 suite: identifier and index
   pins, uniqueness, metadata presence, combination-rule negatives, preset
   count floor, salt-registry distinctness, fallback totality (every input
   resolves; each chain step reachable under a test double), `LogEvents`
   hygiene extended to the new constants. All GPU-independent.
3. **Determinism non-contamination.** The structural argument: no workstream
   adds code to `Hukbo.Core`, so the simulation cannot read any of it. The
   canonical gate's 200-agent / 10,000-tick / seed-1 headless workload
   verifies it end to end after integration, with the gate's real output
   recorded (R-W6.15): the workload must reproduce the recorded seed-1
   reference pair (stateHash `27DC94C6E9A01E35`, eventHash
   `372C9217E5CB8BE9` — `docs/development/testing.md`, Phase 2 reference
   pair). Separately, the `DeterminismTests` golden
   `0x5BEBA7A68F69BE0D` — the terminal state hash of the zero-interception
   preset-V1 control run, not the gate workload's hash — is an additional
   guard that must also stay green. Where
   a Client-visible neutrality test in the logging-neutrality style is
   practical for a new system, it is added; where it is not, the design
   records the structural argument instead of a pretend test.
4. **Screenshot-based manual review protocol.** Interactive visual results
   are proven only by a human. For every manual criterion named in the
   requirements, a row is added to `docs/development/testing.md`, created
   `PENDING` (R-W6.17). The protocol for a reviewer: launch via
   `./scripts/run.ps1` with a fixed scenario seed named in the row; visit
   the defined camera stations (minimum zoom full field, default fit,
   maximum zoom close-up) under the default and high-contrast themes and the
   relevant settings permutations (gore, reduced motion); judge the row's
   stated question; optionally attach screenshots under `artifacts/` as
   evidence. No automated test, compilation result, or window-opening probe
   may flip a row; rows not exercised stay `PENDING`; obstacles are reported
   `BLOCKED` honestly, per `CLAUDE.md` section 6.

### 11. Performance measurement plan

**No render benchmark exists today, so measurement precedes enforcement
(R-W6.12).** The plan has two instruments, one automatic and one hand-run:

1. **Submission counting, GPU-independent.** The geometry layer already
   determines how many primitives each element emits; the plan adds a
   counting seam (a primitive-count function or counting visitor over the
   layout types) so xunit can pin per-pawn-per-tier and per-backdrop
   submission counts exactly. This catches creep on every gate run without
   any GPU, and it is the enforcement instrument for the cap constants.
2. **A hand-run render harness under `tools/`** (working name
   `Hukbo.Tools.RenderProbe`; final name to the planner), in the mold of the
   existing hand-run audio and balance harnesses: not in `Hukbo.slnx`, not
   in the gate, run by a person at a desktop. It launches the real client
   against a scripted scenario, drives the defined camera stations, and
   records over a fixed frame count per configuration: frame time p50, p95,
   and p99; arena-batch sprite submissions; GC collection counts and
   allocated bytes delta in steady state (to verify the zero-allocation
   claim); and the configuration fingerprint (hardware name, resolution,
   build). Output is a JSON report under `artifacts/`, cited by filename as
   evidence. Exact instrumentation seams (how the probe hooks frame timing
   without polluting the shipping code path) are an implementation-plan
   decision; the design constraint is that the probe is opt-in, debug-time,
   and absent from the Release render path's cost.

**The measurement matrix**, at 1080p, on named hardware: {200, 500 visible
units} x {minimum zoom 0.05, default fit zoom, maximum zoom 12} x {grass on,
off} x {motion on, off}. Grass-off and motion-off configurations must
measure less than or equal to their on-counterparts — a paradoxical cost is
a defect.

**The budgets (all ESTIMATE until measured, R-W6.13)**: frame time at 200
units p50 ≤ 6 ms, p95 ≤ 10 ms, p99 ≤ 14 ms; at 500 units p50 ≤ 8 ms, p95 ≤
13 ms, p99 ≤ 16 ms, keeping 60 FPS headroom; submissions per the section 8
caps; zero additional GPU draw calls; zero steady-state per-frame heap
allocation in the draw and sway paths. The first harness run on named
hardware converts these from estimates into enforced numbers or into a
recorded, reviewed revision — budgets are never silently rewritten to match
a measurement (the anti-density-creep rule, R-W6.14, applies to budget
constants exactly as to caps). The existing per-tick simulation allocation
discipline (the defended 900,000-byte collision ceiling) is the cultural
model: measured once, pinned, then defended.

## Rejected approaches

- **Sprite atlas through the content pipeline (Alternative A).** Rejected
  for this pass for the reasons in Alternatives considered: it demands new
  loading, lifetime, sampler, and fallback infrastructure; changes the
  performance profile before any baseline exists; weakens GPU-independent
  testing; and raises the historical-evidence burden on every authored
  image. Re-entry criteria are recorded in section 1 of the recommended
  approach; nothing in this design blocks that future.
- **Runtime-loaded textures bypassing the pipeline (Alternative B).**
  Rejected: all of Alternative A's costs plus runtime I/O failure modes and
  the loss of build-time validation, with the audio analogy failing because
  a missing texture is a hole, not silence.
- **A custom shader for motion.** Rejected by the environment research's
  option analysis: first compiled shader, an MGFXC toolchain step, batch
  breaks or an escape from SpriteBatch into custom vertex buffers, and sway
  math in HLSL that xunit cannot see — for per-pixel quality that is
  invisible at this art scale. The batched CPU-sway path achieves the
  product goal inside the existing batch.
- **Caching composed appearance per pawn.** Rejected: appearance stays a
  pure function recomputed per frame (the existing contract, R-W6.3).
  A cache would introduce invalidation state, violate the no-unbounded-cache
  rule or demand eviction machinery, and save microseconds the measurement
  plan has not shown to matter. If the harness ever shows the appearance
  computation on the frame budget's critical path, a bounded cache proposal
  goes through its own reviewed design.
- **A general theme role expansion for pawn colors.** Rejected: pawn body,
  clothing, and faction colors remain fixed constants by recorded design
  intent (`FactionColorPalette`); making them theme roles is a separate
  design question this pass does not open. New garment palette values are
  fixed named constants in the appearance catalog, like the existing ones.
- **`GetHashCode`, `System.Random`, or iteration order as variation
  sources.** Rejected outright; restated in section 3 because it is the
  single most likely accidental defect in this kind of work.
- **A CI-hosted render benchmark.** Rejected: there is no CI by policy;
  measurement is a hand-run harness under `tools/`, like every other
  measurement harness in the repository.

## Dependencies

What the sibling designs consume from this document:

- The procedural decision (section 1) — none of them may assume textures.
- The catalog entry shape, identifier convention, and validation contract
  (section 2).
- The selection recipe and salt registry (section 3).
- The fallback chain and the three new `LogEvents` constants (section 4).
- The layer table, anchors, and orientation rules (section 5).
- The clock-scaling rule and the appearance/animation boundary (section 6).
- The tier mapping and `MinimumDetailTier` field (section 7).
- The submission caps and the whole-frame budget envelope (section 8).
- The reduced-motion setting seam and the high-contrast forcing rule
  (section 9).
- The four-leg testing pattern and the manual review protocol (section 10).
- The measurement matrix their budgets are validated against (section 11).

What this document depends on:

- User confirmation of OD-4 (procedural direction) — satisfied: confirmed
  2026-07-28; the whole package is written to it.
- The requirements document remaining the requirement authority; if a
  requirement changes, this design is re-checked against it.
- No new packages and no content-pipeline entries — this design is built to
  need neither (R-W6.18), so it has no dependency-review prerequisite.

## Risks

- **Motion distraction** (inherited from the environment research): sway
  that reads as noise under hundreds of moving pawns is the biggest product
  risk in the package. Mitigations are structural — small pinned amplitude,
  sub-1 Hz, LOD-off at far zoom, zero in high-contrast, the exact-zero
  disabled path — but only the manual checklist can judge the result.
- **Submission-count creep.** Every workstream adds primitives; without the
  counting seam and pinned per-tier counts, the whole-frame budget erodes
  one accent at a time. The named-constant-plus-test rule is the defense,
  and the harness is the backstop.
- **Salt collision or reuse** silently reshuffling existing appearance or
  decals. Defended by the salt registry distinctness test; the failure mode
  without it is subtle (everything still deterministic, just visually
  changed) and would burn manual review time.
- **Catalog identifier drift.** Inspector text, diagnostics, and tests all
  key on catalog IDs; a casual reword breaks correlations across logs and
  documents. The pinned-string tests make drift loud.
- **Estimate budgets treated as facts.** Every number in section 11 is an
  ESTIMATE until the harness runs; a plan task that "enforces the budget"
  before measurement would enforce a guess. The plan must order measurement
  before enforcement (R-W6.12), and this design flags it as a sequencing
  hazard for the planner.
- **Manual-verification bottleneck.** The package adds many `PENDING` rows;
  they are only meaningful if a human actually runs the protocol. The risk
  is not technical but procedural: shipping with rows unexercised. The
  honesty rules make that state visible rather than preventing it.
- **Fallback machinery under-exercised.** Because steps 2-4 of the chain are
  nearly unreachable in shipped code, their tests must exercise them through
  deliberate test doubles or they rot. The totality test names this
  explicitly.

## Open decisions

Decisions this design deferred to the user — all resolved 2026-07-28 (full
record in the package README):

- **OD-4 — procedural confirmation. Resolved 2026-07-28:** confirmed. Fully
  procedural rendering stands for this pass; the last hedge is removed from
  the package.
- **OD-7 — shape-redundant faction marker. Resolved 2026-07-28:** deferred;
  recorded as a backlog item in `docs/plans/TODO.md`. This design holds the
  no-regression floor only.
- **OD-8 — reduced-motion scope. Resolved 2026-07-28:** the MotionIntensity
  setting governs all ambient presentation motion — grass sway now, dust and
  future ambient motion included; gameplay-communicating motion stays
  exempt. Plumbing is unchanged; the wording follows this scope.

Decisions this design leaves to the planner or the implementation plan,
recorded so they are not mistaken for settled:

- The final names of the setting enum, the salt registry class, the catalog
  namespace, and the render harness project.
- The exact salt constant values (any pairwise-distinct values pass the
  registry test).
- The placeholder color value (any fixed, conspicuous, non-theme color).
- The seen-set capacity for diagnostic deduplication (a named constant;
  order of 64).
- The probe's instrumentation seam for frame timing, under the constraint
  that Release render cost is unaffected.

## Acceptance criteria

This design is satisfied when the implementation plan derived from it can
show, with the canonical gate's real output and the listed artifacts:

1. All new visual code lives in `Hukbo.Client` (and `tools/` for the
   harness); `Hukbo.Core` is untouched; the gate workload reproduces the
   recorded seed-1 reference pair (stateHash `27DC94C6E9A01E35`, eventHash
   `372C9217E5CB8BE9` — `docs/development/testing.md`, Phase 2 reference
   pair) with the outcome and event stream unchanged (gate output recorded),
   and the separate `DeterminismTests` zero-interception V1 control-run
   golden stays green.
2. Catalogs exist with pinned string IDs, indexes, evidence tiers, scope
   tags, and `MinimumDetailTier`; startup validation and the xunit catalog
   suite both pass; every prohibition has a negative test.
3. All variant selection goes through registered named salts; the
   distinctness test passes; stability tests pin identical selections for
   identical identity inputs; no test or scan finds `System.Random`,
   `GetHashCode`-based selection, or iteration-order dependence in
   presentation code.
4. The fallback resolution function is total under test, each chain step is
   reachable under test, and the three new `LogEvents` constants pass the
   existing hygiene suites with once-per-identifier emission verified.
5. The layer order, feet anchoring, attachment anchors, and pose-blind
   bounds are pinned by geometry tests; tier gates test exactly at 0.95 and
   1.80.
6. The arena renders in one `Begin`/`End` pair on one texture with zero
   added GPU draw calls; every cap and budget constant is named and tested;
   the submission-counting tests pass at the pinned per-tier counts.
7. The reduced-motion setting round-trips through schema version 4, loads
   version-3 files, survives corrupt fields without losing other settings,
   forces zero amplitude in high-contrast, and returns exactly
   `Vector2.Zero` when off; theme contrast validation passes for all five
   themes.
8. The render harness exists under `tools/`, outside the solution and gate;
   at least one full-matrix measurement report exists under `artifacts/` on
   named hardware; budgets are either confirmed or revised through review —
   never silently — and remain labelled ESTIMATE until that report exists.
9. Every manual criterion in the requirements has a `PENDING` row in
   `docs/development/testing.md` with the review protocol's seed and
   stations named; no row was flipped by anything but a human.

This document does not authorize implementation. Implementation authority
for the 23 milestone tasks comes from the user's dated approval of
2026-07-28, recorded in the package README.
