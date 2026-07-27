# Requirements — Hukbo Visual Improvement Pass

Date: 2026-07-28. Status: requirements for planning. This document converts the
four research inputs into testable requirements. It authorizes no
implementation; the design and plan documents required by `CLAUDE.md` section 6
follow it.

Inputs consumed:

- `docs/research/improve-visuals/weapons-shields-historical-research.md`
- `docs/research/improve-visuals/warrior-appearance-historical-research.md`
- `docs/research/improve-visuals/battlefield-environment-research.md`
- `docs/agents/improve-visuals/existing-code-analysis.md`
- `SIMULATION-GAME-STANDARDS.md` (section 4 determinism contract, section 10
  nine feature questions)
- `CLAUDE.md` (section 5 non-negotiables, section 7 historical accuracy policy)

## How to read this document

- **MUST** requirements are mandatory; a plan that drops one needs the user's
  explicit approval. **SHOULD** requirements are expected unless the planner
  records a reason. **MAY** requirements are optional scope.
- Every requirement carries an ID of the form `R-Wx.y` (workstream x,
  requirement y). Cross-cutting requirements that bind several workstreams at
  once carry `R-X.y`.
- **Determinism classification**, used throughout:
  - *Pure-presentation*: a pure function of immutable inputs the client
    already holds (`EntityId`, `CombatLoadout`, `FactionId`, `Scenario.Seed`,
    camera zoom, theme). No stored state. Recomputable every frame.
  - *Presentation-state*: bounded, client-only state (fixed-capacity pools,
    frame-time accumulators, persisted client settings) that never reads into
    the simulation and never survives into a snapshot, replay, or hash.
  - *Forbidden*: anything that adds appearance data to `Hukbo.Core`, feeds
    presentation values back into simulation state, hashes, events, or AI, or
    derives gameplay from visual state.
- **Acceptance criteria honesty**: each workstream separates
  *automated-testable* criteria (GPU-independent xunit tests over pure
  geometry/value helpers, hygiene scans, cap-constant pins, determinism
  neutrality runs) from *manual-checklist* criteria (rows added to
  `docs/development/testing.md`, judged only by a human at an interactive
  desktop, left `PENDING` until then). No automated test may claim to prove a
  manual row, per `CLAUDE.md` section 6.

## Cross-cutting requirements (R-X)

### Readability priority at distance

The spectator's read order at distant zoom is fixed. Using the existing
machinery — `apparentScale = clamp(cameraZoom * 1.35, 0.72, 2.40) *
scaleMultiplier`, the three detail tiers (Low below 0.95, Medium below 1.80,
High at or above 1.80), and the camera zoom range 0.05x–12x — the priority
order is: **faction > weapon role > shield presence > state marks
(alive/selected/injured/targeted) > body and clothing variation**.

- **R-X.1 (MUST)** The faction ground ring, the weapon silhouette, the shield
  block, and the dead/selection/hover marks MUST all render at the Low detail
  tier. Nothing added by any workstream may displace or occlude them at Low
  tier.
- **R-X.2 (MUST)** Body and clothing variation (workstream 3) and weapon and
  shield presentation-only variants (workstreams 1–2) MUST be confined to the
  Medium and High tiers, or be sub-threshold at Low tier (tone shifts that do
  not change silhouette classification). At Low tier every Kampilan-armed pawn
  MUST remain classifiable as "Kampilan-armed" and every shielded pawn as
  "shielded" regardless of variant.
- **R-X.3 (MUST)** No new visual element may reduce the distinguishability of
  the four weapon silhouettes pinned by
  `PawnAppearanceFactoryTests` (distinct silhouettes for all four weapons) or
  the shield-at-every-tier rule in `PawnGeometry`.
- **R-X.4 (MUST)** Tier assignment for every new element MUST be a pure
  function of apparent scale (the existing tier switch), testable at the exact
  thresholds 0.95 and 1.80.
- **R-X.5 (MUST)** Pose-blind culling is preserved: `PawnRenderer.GetBounds`
  MUST remain independent of animation phase, and any element that grows a
  pawn's possible extent MUST keep the drawn-pawn set independent of animation
  state.

Automated: tier-threshold tests; silhouette-classification tests on
`PawnGeometry` outputs; bounds-independence tests. Manual: "at minimum zoom
with 200+ pawns, faction and weapon role remain the dominant reads" — a
checklist row, honest answer only from a human.

### Historical accuracy (binds workstreams 1, 2, 3)

- **R-X.6 (MUST)** Player-facing cultural identifications appear only in pair
  form (Filipino name, em dash, plain English descriptor) and only for terms
  the research cleared inside the hundred-year attestation window. Cleared for
  player-facing use by the warrior-appearance research: **Putong — Head
  Wrap**, **Bahag — Loincloth**, **Chinina — Collarless Jacket** (plus the
  four existing weapon labels from combat preset V2). Terms whose attestation
  is PENDING verification — *kalasag*, *palisay*, and the inspector-metadata
  candidates *barote*, *kandit*, *panika*, *kamagi*, *batuk*, *kolombiga*
  (*kolombiga*: attestation clears the bar per the research, Morga 1609;
  held pending spelling review only) — MUST NOT appear in player-facing
  labels until verified; they MAY appear in inspector metadata explicitly
  flagged as pending. Terms that failed or
  cannot be established — *panabas*, *taming*, *salakot* — MUST NOT be used
  at all.
- **R-X.7 (MUST)** Every rendered variant, preset, component, and skin carries
  an evidence tier (**Documented**, **Documented, form uncertain**,
  **Provisional reconstruction**, or an explicit "presentation-only, no
  historical claim" marker) in client-side metadata, surfaced as text in the
  agent inspector, following the existing `WeaponEvidenceTier` /
  `PawnAppearance` precedent.
- **R-X.8 (MUST)** The ten prohibited-combination rules in the
  warrior-appearance research (section 4, "Prohibited combinations") are
  binding: (1) no Cagayan feathered headdress on Visayan or Tagalog presets;
  (2) no full-body or partial tattoos outside Visayan-scoped presets; (3) no
  red putong on Tagalog presets and no red chinina on Visayan presets; (4) no
  brass/bronze plate armor, chain mail, or iron greaves; (5) no *salakot*
  label and no sun hats on elite figures; (6) no gold ensemble on low-status
  presets; (7) no tattoo motifs rendered as recognizable patterns — tone
  shift only; (8) no European elements and no footwear; (9) no later
  Moro-specific kit on 1500s lowland presets; (10) no single region's costume
  presented as "the Philippines" — every preset carries a scope tag (Visayan,
  Tagalog, Cagayan, or Unscoped-generic) shown in the inspector. A
  combination-validation test suite MUST encode these rules and fail on any
  preset that violates them.
- **R-X.9 (MUST)** Gameplay-adjacent tuning values introduced by visual work
  (weights, thresholds, amplitudes) are marked `PROVISIONAL` in code comments
  and never presented as historical measurement, matching the existing
  practice in `PawnGeometry` and `SwingGeometry`.
- **R-X.10 (SHOULD)** Inspiration tags shown to the player name a place and
  time (`Mactan — 1521`, `Manila — c.1590`), never "ancient Philippines".

### Prohibited scope (all workstreams)

- **R-X.11 (MUST NOT)** No appearance, variant, or environment state of any
  kind in `Hukbo.Core`. No change to simulation state, tick stages, hashes,
  events, RNG streams, or AI.
- **R-X.12 (MUST NOT)** No new `ShieldId` or `WeaponId` values, no renumbering
  or reordering of existing enum values, no mechanical changes of any kind.
  Entries the research flags as "future mechanical candidates" (narrow
  breast-high shield, palisay/taming bucklers) are out of scope entirely; in
  particular a visibly shorter or smaller shield skin on a `TallHardwood`
  loadout is forbidden because it would show the spectator a false cause.
- **R-X.13 (MUST NOT)** No CI workflow. Verification stays local through
  `./scripts/verify.ps1`.
- **R-X.14 (MUST NOT)** No per-blade grass objects or per-blade updates; grass
  is bounded batches of primitives only.
- **R-X.15 (MUST NOT)** No copying, tracing, or importing of any external
  image or another game's visual identity. All art remains original
  procedural rendering; external references are reference-only per the
  research source registers.
- **R-X.16 (MUST NOT)** No unbounded caches, no derived render data in
  snapshots, no runtime network access, no `System.Random`, no `Console.*`
  outside the two entry points.

## Workstream 1 — Weapon visual variants

Scope: presentation-only variants of the four implemented weapons, rendered
procedurally through `PawnGeometry` / `PawnRenderer`.

### Requirements

- **R-W1.1 (MUST)** Each weapon keeps exactly one primary battle silhouette,
  per the research: Kampilan K1 (long, forward-heavy, widening blade), Wasay
  W1 (short haft, broad head), Kalis L1 (slim, straight, symmetric,
  one-handed), Itak I1 (shortest, plainest, wide-for-length). Silhouette
  identity remains derived from `CombatLoadout.Weapon` only, never from
  `EntityId` — the pinned `PawnAppearanceFactory` rule.
- **R-W1.2 (MUST)** Presentation-only material and condition variants (K3, W3,
  Kalis and Itak tone tints) vary only color and tone within the documented
  material palette (iron blue-black, palm/rattan ochre, charred wood, warm
  grip ochre). They MUST NOT alter the silhouette classification at any
  detail tier.
- **R-W1.3 (MUST)** Variant selection per pawn is a pure function of
  `EntityId` through a salted SplitMix64-style mix stream (the
  `PawnAppearanceFactory` precedent), with a new named salt constant, stable
  across frames and across replays of the same seed.
- **R-W1.4 (MUST)** The Kampilan K2 bifurcated-pommel form, the Kalis L2
  half-wavy and L3 fully wavy forms, and the Cordilleran head axe W2 MUST NOT
  appear as pawn-scale battle silhouettes. K2, L2, and L3 MAY appear only in
  inspector or armory-card text/art explicitly labelled as later or
  provisional forms; W2 MUST NOT appear anywhere in the current roster.
- **R-W1.5 (SHOULD)** Wasay MAY gain a rattan lashing band accent at the
  head-haft junction at Medium/High tier (documented ubiquitous hafting
  technique, asserts nothing specific).
- **R-W1.6 (MUST)** Each weapon variant records its evidence tier and any
  variant note ("later ornamented form", "half-waved form, later
  attestation") in metadata surfaced by the inspector (R-X.7).
- **R-W1.7 (MUST)** Tint variants MUST stay within a contrast envelope that
  keeps the weapon line legible against the ground shades and the pawn body
  at every detail tier; the envelope values are named constants with tests.
- **R-W1.8 (SHOULD)** Two to three tints per weapon is the target breadth;
  more is not justified by the evidence and adds review load without
  spectator value.
- **R-W1.9 (MAY)** Edge-wear or highlight variation on the existing highlight
  line, provided it is tier-gated (Medium+) and tone-only.

### Acceptance criteria

Automated: variant-stability tests (same `EntityId` + loadout gives the same
variant every call); equipment-identity-from-loadout-only tests extended to
variants; silhouette-classification invariance under tint; palette-envelope
pin tests; evidence-note presence test for every variant (extending the
existing "every weapon carries an evidence note" test). Manual checklist:
"weapon role identifiable per pawn at normal zoom"; "tint variation visible at
High tier without breaking role recognition" — human judgment rows.

### Spectator discoverability

Yes. The variant is visible on the pawn (tone/accent at Medium/High tiers),
and the inspector names the variant, its evidence tier, and its note. A
spectator can click any pawn and learn what they are seeing without reading
source.

### Determinism classification

Pure-presentation. Inputs are `EntityId` and `CombatLoadout` only. Forbidden:
any weapon variant affecting reach, damage, timing, or bounds used by
simulation.

## Workstream 2 — Shield visual variants

Scope: presentation-only skins and posture for the existing
`ShieldId.TallHardwood` block; `ShieldId.None` renders nothing, as today.

### Requirements

- **R-W2.1 (MUST)** Skins are limited to the four research-cleared tall-shield
  anchors — S1 (Mactan thin wood), S2 (Morga full-body), S3 (Boxer Codex
  Cagayan), S5 (Visayan kalasag form) — expressed only as presentation-level
  differences: face tone within the palette, a rattan-binding accent line,
  and slight outline curvature. All four MUST read as "tall body shield" at
  every detail tier. DEVIATION FLAGGED — the shield design proposes per-skin
  proportion deltas beyond this list; tracked as OD-10, user approval
  required (either amend R-W2.1 with bounded deltas inside one shared
  aspect-ratio band, footprint never below the Low-tier block, or the design
  drops the deltas).
- **R-W2.2 (MUST)** Shield presence remains drawn at every detail tier
  (existing rule); no skin may reduce the shield block's footprint below the
  current Low-tier legibility.
- **R-W2.3 (MUST)** Skin selection is a pure salted function of `EntityId`
  (as R-W1.3); shield presence itself comes only from
  `CombatLoadout.Shield`, pinned by the existing tests.
- **R-W2.4 (MUST NOT)** No breast-high, round, buckler, pronged, or tufted
  shield shapes. S4, S6, S7 are future-mechanics questions (R-X.12); S8–S11
  are regionally inappropriate for a generic roster; Bagobo hair tufts and
  Cordilleran prongs MUST NOT appear on any pawn.
- **R-W2.5 (SHOULD)** The shield SHOULD be drawn slightly angled forward of
  the pawn rather than as a passive side slab, per the S12 active-posture
  evidence (Hinilawod, Cole's tilting grip). The angle is a fixed layout
  offset in `PawnGeometry`, marked `PROVISIONAL`, and MUST NOT change
  `GetBounds` behavior contrary to R-X.5.
- **R-W2.6 (MUST)** The player-facing shield label stays `Tall Hardwood
  Shield` (plain descriptor). The pair form `Kalasag — Tall Hardwood Shield`
  is gated on the open decision OD-1 and MUST NOT ship before the name
  verification succeeds.
- **R-W2.7 (MUST)** Each skin records evidence tier and source anchor
  (`Mactan — 1521`, `Manila — c.1590`, etc.) in inspector metadata (R-X.7,
  R-X.10). The inspector MAY note that Pigafetta describes "thin wood" where
  the enum says hardwood.
- **R-W2.8 (MUST)** Skin tones respect the same contrast envelope discipline
  as R-W1.7 so the shield block never merges with the torso or the ground.

### Acceptance criteria

Automated: skin-stability tests; shield-presence-from-loadout-only tests
(existing, extended); geometry tests pinning the angled-posture offset and
its bounds neutrality; classification tests that all skins remain
tall-shield-shaped (aspect-ratio bounds on the layout rectangle). Manual
checklist: "shielded versus unshielded pawns distinguishable at minimum
zoom"; "skins read as variation, not as different equipment" — human rows.

### Spectator discoverability

Yes. The skin differences are visible at Medium/High tier; the inspector
names the skin's anchor and tier. The posture change is visible on every
shielded pawn.

### Determinism classification

Pure-presentation. Forbidden: any skin implying different coverage or
protection (the false-cause rule, R-X.12).

## Workstream 3 — Warrior appearance presets (component system, ≥50 presets)

Scope: a client-side component system (hair, head covering, torso garment,
lower garment, armor layer, sash, accessory, adornment, palette, condition)
composed into at least fifty reviewable presets, replacing today's
three-trait placeholder variation.

### Requirements

- **R-W3.1 (MUST)** Components follow the research categories A–K exactly:
  stature/build (9 combinations, no historical claim), hair (4), head
  covering (6), torso garment (4), lower garment (3), armor layer (5 plus the
  exclusion list), sash (3), accessories (4, two renderable), adornment (8;
  four renderable — I1, I2, I4, I5, with the two tattoo options sharing one
  tone-shift channel — and four as inspector texture), natural-dye palette
  (10 entries), condition (5, presentation-only). Options outside these categories require new research,
  not invention.
- **R-W3.2 (MUST)** At least fifty distinct presets pass the combination
  rules, following the research's block structure: a Visayan block (~20), a
  Tagalog block (~15), a northern Luzon block (~8), and a generic levy block
  (~10). Each preset differs in silhouette or documented color logic, not
  merely hue.
- **R-W3.3 (MUST)** Every preset carries a scope tag (Visayan, Tagalog,
  Cagayan, Unscoped-generic) in metadata, shown in the inspector (R-X.8 rule
  10).
- **R-W3.4 (MUST)** The combination validator encodes the six co-occurrence
  rules and ten prohibitions from the research section 4 (R-X.8), and every
  shipped preset passes it in an automated test.
- **R-W3.5 (MUST)** Preset selection per pawn is a pure salted function of
  `EntityId` (and faction where the roster design scopes blocks by faction),
  never of equipment identity beyond what the loadout provides; equipment
  identity itself stays loadout-only (existing pinned rule).
- **R-W3.6 (MUST)** Pawn-scale rendering of components is confined to the
  three channels the research allows: silhouette changes (head wrap wedge,
  hair knot bump, thickened armor capsule, sash line), color blocks (skin,
  garment dye, armor material), and one or two accent marks (gold pixel, red
  wrap). Fine detail — motifs, embroidery, jewelry detail — is inspector
  text only.
- **R-W3.7 (MUST)** Tattooing renders only as a darker/cooler skin-tone shift
  on Visayan-scoped bare-chested presets (full or partial coverage); no motif
  patterns (R-X.8 rule 7). Facial tattooing is excluded from rendering.
- **R-W3.8 (MUST)** The dye palette uses the research's swatch table (undyed
  cream `#E7D8B7`, indigo `#354D6B`, blue-black `#2A3140`, sappan red
  `#8F3F35`, turmeric yellow `#C9A23F` used sparingly, bark brown `#7A5A3A`,
  gold accent `#D0A64A`, iron blue-black `#384249`, natural skin range,
  tattoo tone shift) as named constants. Faction color stays on the ground
  ring/outline, never on garments.
- **R-W3.9 (MUST)** Status display follows the stratification rule: status is
  shown by adding documented wealth markers (gold accents, dyed cloth) to a
  common base, never by invented class uniforms, never gold on low-status
  presets, and no invented "slave costume".
- **R-W3.10 (MUST)** The red head wrap (C2) is an earned insignia and MUST
  NOT be assigned by random roll. Because `AgentView` carries no kill or
  veteran data, shipping C2 requires the open decision OD-5; until decided,
  C2 stays out of the preset pool.
- **R-W3.11 (MUST)** Condition options (K1–K5) are presentation-only, carry
  the "no historical claim" marker, and are never described to the player as
  historical detail.
- **R-W3.12 (MUST)** Each component option and each preset records its
  evidence tier and its "must not generalize" note in metadata; the
  inspector shows preset name, scope tag, and tier (R-X.7).
- **R-W3.13 (MUST)** Detail tiers: component silhouette elements draw at
  Medium+ (matching today's head-treatment rule); at Low tier a preset
  contributes at most tone variation, preserving R-X.1/R-X.2 priority.
- **R-W3.14 (SHOULD)** Leader/elite presets (denser gold and dye) SHOULD be
  rare in any generated army so the earned/elite markers stay meaningful;
  rarity weights are named `PROVISIONAL` constants.
- **R-W3.15 (MAY)** Inspector flavor text MAY include non-renderable
  documented texture (betel pouch, gold dental work) with tiers, to make the
  inspector humane.

### Acceptance criteria

Automated: combination-validator tests over all shipped presets (every
prohibition has at least one negative test); preset-count test (≥50 valid
presets); selection-stability tests; palette pin tests; scope-tag and
evidence-tier presence tests for every preset; tier-gating tests. Manual
checklist: "fifty presets read as varied but coherent at normal zoom"; "no
preset reads as a different faction or different equipment"; "elite figures
read as denser in gold and dye, not larger" — human rows. Historical review
of the preset list against the research is a human review task, not a test.

### Spectator discoverability

Yes, with a caveat. The variation itself is visible; the *meaning* (scope,
tier, earned markers) is discoverable by selecting a pawn and reading the
inspector, which is the sanctioned channel per SIMULATION-GAME-STANDARDS
section 10 point 8 (inspector field). No effect is inspector-invisible.

### Determinism classification

Pure-presentation, recomputed per frame from immutable identity (existing
`PawnAppearanceFactory` shape). The C2 earned insignia, if approved, would be
the single presentation-state exception (bounded client-side tracking of
Death events); anything beyond that is forbidden.

## Workstream 4 — Battlefield ground and vegetation

Scope: ground shading improvement, clumped grass clusters, trample marks,
dust — all client-side, extending `PlainsBackdropGeometry` /
`PlainsBackdropRenderer`.

### Requirements

- **R-W4.1 (MUST)** Ground shading replaces the independent per-cell hash
  with spatially correlated shading (four corner hashes averaged per cell),
  remaining a pure function of (column, row, scenario seed), theme-derived
  via `Color.Lerp(ArenaSurface, ArenaBorder, t)`.
- **R-W4.2 (MUST)** The backdrop interpolation ceiling 0.22 binds every new
  shade: grass, trample, and dust tones MUST all sit at or below it, pinned
  by tests in the same style as the current shade pins.
- **R-W4.3 (MUST)** Grass clusters are generated once per scenario (creation
  and reset only, never per tick or frame) by two-level placement — cluster
  centers, then tufts with square-root radial falloff — from `SplitMix64`
  seeded by `Scenario.Seed` XOR a **new** named salt. The existing plains
  salt is not reused, so today's decals do not shift.
- **R-W4.4 (MUST)** All caps are named constants with tests: grass clusters
  ≤ 320, quads per cluster ≤ 4, trample marks ≤ 128, live dust puffs ≤ 32,
  existing grid (48x48) and decal (256) caps unchanged. Density scales only
  with map area under the caps, never with agent count or frame rate.
- **R-W4.5 (MUST)** No authored textures, no content-pipeline changes, no new
  packages, no shaders: everything draws from the existing 1x1 white texture
  inside the existing arena `SpriteBatch` Begin/End pair, adding zero GPU
  draw calls (Deferred batching on one texture).
- **R-W4.6 (MUST)** Boundary rules: clusters clip to the map rectangle as
  decals do today; a grass-free margin of one ground cell (64 world units)
  inside the border keeps the border the strongest line; sway offsets
  (workstream 5) are clipped so no tuft crosses the border. All three are
  pure-geometry testable.
- **R-W4.7 (MUST)** Trample marks: a fixed-capacity, oldest-replaced list of
  client-only marks fed by authoritative `Death` events (optionally melee
  `Attack` events, throttled), drawn under grass; clusters within a trample
  radius draw reduced with zero sway. Resets with the scenario; never
  persists; never feeds back.
- **R-W4.8 (MUST)** Dust puffs follow the `HitEffectSystem` lifecycle shape:
  `Death` spawns a brief puff, `Attack` MAY spawn a throttled kick, `Move`
  events MUST NOT spawn per-event effects, and `Outcome` SHOULD stop new
  spawns. Sub-second lifetimes, ground-shade colors. CONTRADICTION FLAGGED —
  the battlefield design scopes dust as MAY; tracked as open decision OD-9,
  user approval required; R-W4.4's dust cap and R-W4.2's dust shade
  obligations are conditional on the same decision.
- **R-W4.9 (MUST)** Historical framing: the ground depicts generic open
  ground labelled **Provisional reconstruction** in metadata; no player-facing
  text names the vegetation, region, or land use; rice terraces and paddies
  MUST NOT be depicted.
- **R-W4.10 (MUST)** All per-frame paths are allocation-free in steady state;
  generation allocates only at scenario creation and reset. New logic lives
  in pure geometry helpers; renderers stay untested draw-only sinks (the
  established split).
- **R-W4.11 (SHOULD)** Cluster distribution leaves deliberate empty ground
  between clumps (sparse-and-clumped, not wallpaper).
- **R-W4.12 (MAY)** If the default theme's ground should shift toward cogon
  olive-gold, that is a separate theme-color tuning change (open decision
  OD-6), not a hard-coded renderer palette.

### Acceptance criteria

Automated: shade-ceiling pins; corner-hash shading determinism (same seed,
same shades); cluster placement determinism and cap tests; border-margin and
clipping geometry tests; trample suppression distance tests; dust lifecycle
and cap tests; event-mapping tests (Death spawns, Move never spawns);
allocation checks on the helper paths where the harness allows. Manual
checklist: "ground reads as living grassland, not checkerboard, at all
zooms"; "border remains the strongest line"; "trampled areas visibly thin
where fighting happened" — human rows.

### Spectator discoverability

Yes. Ground variation and clusters are directly visible; trample marks and
dust appear where deaths and attacks happened, so a spectator can correlate
wear with the battle they watched. The provisional-reconstruction framing is
discoverable wherever backdrop metadata is surfaced (at minimum, no
player-facing claim is made that would need discovering).

### Determinism classification

Ground and clusters: pure-presentation (pure functions of scenario seed).
Trample and dust: presentation-state (bounded pools fed by authoritative
events, client-only, reset with scenario). Forbidden: any of it entering
snapshots, hashes, or the simulation.

## Workstream 5 — Presentation-only wind and motion

Scope: CPU-computed grass sway (research option 2) and its gating. No
shaders, no per-sprite effects, no batch breaks.

### Requirements

- **R-W5.1 (MUST)** Sway is computed by a pure helper of value types —
  `GrassSwayOffset(timeSeconds, phase, amplitudeScale)` shape — evaluated
  per visible cluster per frame, allocation-free, inside the existing arena
  batch. No custom `Effect`, no MGFXC step, no content-pipeline change, no
  `SpriteSortMode.Immediate`.
- **R-W5.2 (MUST)** The motion clock is a client-side float accumulator
  advanced by frame elapsed time (the `AdvanceEffects` /
  `SwingAnimationSystem` pattern). It never touches the simulation and no
  simulation value ever depends on it.
- **R-W5.3 (MUST)** Per-cluster phase derives deterministically from the
  cluster generation stream (seeded, salted), so two replays of the same
  seed show the same phase assignment.
- **R-W5.4 (MUST)** Amplitude stays small and slow: at most 1–2 screen
  pixels at zoom 1 and below 1 Hz, as named `PROVISIONAL` constants with
  tests pinning the bound.
- **R-W5.5 (MUST)** An `amplitudeScale` factor of 0 disables motion entirely
  and MUST return exactly `Vector2.Zero` (asserted by test), making the
  motion-off render path bit-identical to a static backdrop.
- **R-W5.6 (MUST)** Zoom LOD in three bands selected purely by camera zoom:
  far (below roughly 0.3) draws static single-rectangle clusters with no
  sway; mid (roughly 0.3–2) full clusters with sway; near (above roughly 2)
  full clusters with sway and optionally one extra silhouette quad. Band
  thresholds are named constants tested at the exact values.
- **R-W5.7 (MUST)** The high-contrast theme forces the amplitude factor to 0
  regardless of settings, and renders grass with minimal shade spread —
  reusing that theme's eliminate-visual-noise purpose, matching the zero-sway
  precedent of the 0.22 decal ceiling.
- **R-W5.8 (MUST)** The reduced-motion setting (R-W6.8) gates sway: off
  means factor 0; the same factor provides a half-amplitude mode.
- **R-W5.9 (MUST)** Sway is presentation-only in every direction: it reads
  nothing from the simulation except what the client already renders, and
  trample-suppressed clusters sway at zero amplitude (R-W4.7).
- **R-W5.10 (MAY)** A later wind-gust overlay on top of moving grass is
  recorded as out of scope for this pass (the research ranks it a follow-on
  enhancement, weakest as a substitute).

### Acceptance criteria

Automated: exact-zero test for the disabled path; amplitude and frequency
bound pins; phase determinism from seed; LOD band threshold tests;
high-contrast forcing test; border-clip-under-sway geometry test. The
determinism neutrality argument is structural (no simulation input exists),
verified by the canonical gate's unchanged seed-1 hashes. Manual checklist:
"sway reads as alive, not as noise, under 300 moving pawns"; "no motion
visible at minimum zoom"; "high-contrast theme shows zero motion" — human
rows, the biggest product risk in this pass per the research.

### Spectator discoverability

Yes. The motion is directly visible at mid/near zoom, and its controls
(reduced-motion setting, theme choice) are visible UI. Its deliberate absence
at far zoom and in high-contrast is itself the designed behavior.

### Determinism classification

Presentation-state (frame-time accumulator) driving pure-presentation math.
Forbidden: any simulation read-back, any wall-clock input into anything
hashed, any motion state in snapshots.

## Workstream 6 — Rendering and asset infrastructure

Scope: variant catalogs, stable variant identity, fallback chain, missing
asset diagnostics, settings, accessibility, and the performance measurement
that everything above budgets against.

### Catalogs and stable variant identity

- **R-W6.1 (MUST)** Variant and preset catalogs are client-side, immutable
  data (the `PawnAppearance` precedent): each entry carries a stable
  identifier, display label, evidence tier, scope tag, and notes. Catalog
  identifiers are pinned values with a "do not renumber or reword" contract
  and a test, because inspector text, diagnostics, and tests reference them.
- **R-W6.2 (MUST)** Per-pawn variant selection uses the established recipe:
  XOR `EntityId` with a new named salt constant per trait stream, mix with
  the SplitMix64-finalizer pattern, select by modulo. New salts never reuse
  the existing appearance salts or the plains salt, so existing appearance
  and decals do not shift.
- **R-W6.3 (MUST)** Appearance remains a pure function recomputed per frame;
  no persistent per-pawn presentation state is introduced except where a
  requirement explicitly declares presentation-state (trample, dust, motion
  clock, and OD-5 if approved), each with fixed capacity and scenario reset.

### Fallback chain

- **R-W6.4 (MUST)** Every visual lookup resolves through a defined fallback
  chain: **specific variant → weapon (or component) default → model-category
  default → diagnostic placeholder**. The chain is a pure, testable
  resolution function; the diagnostic placeholder is a deliberately
  conspicuous primitive (never invisible, never a crash), and reaching it
  emits the R-W6.5 diagnostic. Resolution MUST be total: for every valid
  `(EntityId, CombatLoadout, FactionId)` input some drawable resolves.
- **R-W6.5 (MUST)** Missing or unresolvable visual data is logged through
  `Hukbo.Diagnostics.DiagnosticLog` on the `assets` channel with new stable
  `LogEvents` constants (for example `assets.visual.variantMissing`,
  `assets.visual.fallback`), at `warn`, following the four enforced rules:
  constant on `LogEvents`, six leading fields in order, flat camelCase
  payload, zero allocation when disabled. Emission MUST be once per distinct
  missing identifier per session, not per frame.

### Settings

- **R-W6.6 (MUST)** A new reduced-motion setting follows the GoreIntensity
  precedent end to end: an enum with explicit pinned numeric values and a
  do-not-renumber comment; a nullable field on `RawClientSettings` validated
  independently so old files survive; a schema version bump (3 → 4) with
  backward-compatible load; a manager with an injected persist delegate;
  a menu selector UI; and tests covering manager, selector, and store
  round-trip.
- **R-W6.7 (SHOULD)** The setting SHOULD offer at least Off / Reduced / Full
  (Reduced maps to the half-amplitude factor). Its exact scope — grass only,
  or also future ambient motion — is open decision OD-8.
- **R-W6.8 (MUST)** Settings failures never lose the saved theme or other
  fields (the independent-validation rule), and every load/save/failure logs
  on the `settings` channel as today.

### Accessibility

- **R-W6.9 (MUST)** High-contrast theme behavior: all new backdrop and
  effect shades obey the 0.22 lerp ceiling (R-W4.2), and motion amplitude is
  forced to zero (R-W5.7). These are the two existing precedents extended,
  both test-pinned.
- **R-W6.10 (MUST)** Color-blind readability: faction identity MUST NOT
  depend on hue alone. The fixed faction constants (blue 64,164,255; red
  255,91,105; gold 231,199,84) stay as designed, so the requirement lands on
  redundancy: faction MUST remain distinguishable by shape or position
  channels (the ground ring form and any future faction mark), and no new
  variant may make garment or ground hues a competing faction signal
  (R-W3.8's faction-color-stays-on-the-ring rule). A shape-redundant faction
  marker beyond the ring is open decision OD-7; at minimum this pass MUST
  NOT worsen the current hue dependence.
- **R-W6.11 (MUST)** Contrast-pair validation in `UiThemeCatalog` continues
  to pass for all five themes with any new theme-derived colors.

### Performance

- **R-W6.12 (MUST)** A render measurement task precedes budget enforcement:
  **no render or draw-call benchmark exists today** (the simulation benchmark
  measures ticks, not frames). The plan MUST include a task to build or
  script a repeatable render measurement (frame time percentiles and sprite
  submission counts at defined camera positions), hand-run like the `tools/`
  harnesses, outside the gate.
- **R-W6.13 (MUST)** Budgets, all marked **ESTIMATE until measured** against
  the R-W6.12 harness on named hardware, at 1080p, across the matrix {200,
  500 visible units} × {minimum zoom 0.05, default fit zoom, maximum zoom
  12} × {grass on, off} × {motion on, off}:
  - frame time at 200 units: p50 ≤ 6 ms, p95 ≤ 10 ms, p99 ≤ 14 ms
    (ESTIMATE);
  - frame time at 500 units: p50 ≤ 8 ms, p95 ≤ 13 ms, p99 ≤ 16 ms
    (ESTIMATE), keeping 60 FPS headroom;
  - sprite submissions in the arena batch: ≤ 12,000 at 200 units, ≤ 20,000
    at 500 units (ESTIMATE; today's counted order is 3,000–8,000 pawn
    submissions plus ~2,600 backdrop, and the grass worst case adds ~1,700);
  - GPU draw calls: the arena remains one Begin/End pair on one texture;
    workstreams 1–5 add **zero** additional GPU draw calls;
  - steady-state allocation: zero heap allocation per frame in the draw and
    sway paths after scenario setup.
  Grass-off and motion-off configurations MUST measurably reduce or equal
  the on-configurations (no paradoxical cost).
- **R-W6.14 (MUST)** Every new cap or budget constant is named, tested, and
  never a derived expression (the anti-density-creep rule).

### Verification and process

- **R-W6.15 (MUST)** The canonical gate `./scripts/verify.ps1` passes after
  integration with its real output recorded; the recorded seed-1 reference
  pair (stateHash `27DC94C6E9A01E35`, eventHash `372C9217E5CB8BE9` —
  `docs/development/testing.md`, Phase 2 reference pair), the outcome, and
  the event stream are untouched by every workstream. The
  `DeterminismTests` zero-interception V1 control-run golden
  (`0x5BEBA7A68F69BE0D`) is a separate, additional guard that must also stay
  green; it is not the gate-workload oracle. If any Client-visible neutrality test in
  the style of the logging-neutrality test is practical for the new systems,
  it SHOULD be added.
- **R-W6.16 (MUST)** All new drawing logic lives in tested pure
  geometry/value types; renderer classes stay untested draw-only sinks;
  Client tests never construct `ArenaGame`, a graphics device, a sprite
  batch, or a window.
- **R-W6.17 (MUST)** New manual-verification rows are added to
  `docs/development/testing.md` for every manual criterion named in this
  document, created as `PENDING`.
- **R-W6.18 (MUST NOT)** No new packages and no content-pipeline entries
  without a separately reviewed dependency change; this pass as scoped
  requires none.

### Acceptance criteria

Automated: fallback-resolution totality tests (every input resolves; each
chain step reachable in tests); `LogEvents` constant and format tests
(existing hygiene suites extended); settings round-trip, schema-migration,
and corrupt-field tests; theme contrast validation; cap-constant pins; the
canonical gate itself. Manual checklist: diagnostic placeholder visibly
conspicuous in a forced-failure run; settings selector operable from the
menu; measurement report produced and budgets reconciled — the measurement
report is evidence, not a test.

### Spectator discoverability

Partially, honestly stated: the fallback placeholder and the settings UI are
directly visible; catalogs and diagnostics are infrastructure whose spectator
face is the inspector text and the placeholder. The missing-asset log is a
developer-facing record by design (`artifacts/logs/`), which is acceptable
because the on-screen placeholder is the spectator-facing signal of the same
condition.

### Determinism classification

Pure-presentation (catalogs, selection, fallback) plus persisted client
settings and the diagnostics side channel — none of which may influence
simulation. Forbidden: catalog data or settings reaching Core, hashes, or
events.

## Requirement summary

| Workstream | Requirements | MUST / MUST NOT | SHOULD | MAY |
| --- | --- | --- | --- | --- |
| X — Cross-cutting | 16 | 15 | 1 | 0 |
| 1 — Weapon variants | 9 | 6 | 2 | 1 |
| 2 — Shield variants | 8 | 7 | 1 | 0 |
| 3 — Appearance presets | 15 | 13 | 1 | 1 |
| 4 — Ground and vegetation | 12 | 10 | 1 | 1 |
| 5 — Wind and motion | 10 | 9 | 0 | 1 |
| 6 — Infrastructure | 18 | 16 | 2 | 0 |
| **Total** | **88** | **76** | **8** | **4** |

## Open decisions requiring user approval

- **OD-1 — Kalasag label promotion.** Upgrade the shield label to `Kalasag —
  Tall Hardwood Shield` only if the vocabulary attestation is verified inside
  the hundred-year window (Scott's *Barangay* warfare chapter or the early
  vocabularies, both unread at page level). Options: commission that
  verification as a research task, or ship with the plain descriptor.
- **OD-2 — Palisay.** The buckler is out of scope mechanically (R-X.12); the
  only live question is whether its pending name may appear in inspector
  research notes. Default: inspector-metadata only, flagged pending.
- **OD-3 — Mindanao/Sulu gap.** The 1500s source set is thin for Mindanao and
  Sulu lowland warriors and the research deliberately leaves them unmodeled.
  Options: accept the Unscoped-generic block as the only coverage, or
  commission further research before any Mindanao-flavored preset exists.
- **OD-4 — Sprite versus procedural direction.** The environment research
  recommends, and the existing-code analysis strongly supports, staying fully
  procedural (no textures, no content-pipeline change, no shader). Sprites
  remain a possible later direction. This document is written to the
  procedural path; confirm it so the design doc does not have to hedge.
- **OD-5 — Earned red putong (C2).** `AgentView` exposes no kill or veteran
  marker. Options: (a) exclude C2 from this pass; (b) add bounded
  presentation-state kill tracking from `Death` events in the client (the
  event's source identity permitting), classified presentation-state and
  reset with the scenario. (b) is attractive — it is exactly the
  spectator-discoverable battle-honor display the sources describe — but it
  is new presentation-state and needs the user's yes.
- **OD-6 — Default theme ground tint.** Whether to shift the default theme's
  ground toward cogon olive-gold is a theme-color tuning change reviewed on
  its own, not part of the renderer work.
- **OD-7 — Shape-redundant faction marker.** Whether to add a non-hue faction
  channel (for example a ring shape difference) for color-blind spectators,
  beyond the no-regression floor in R-W6.10.
- **OD-8 — Reduced-motion scope.** Whether the new setting governs only
  grass sway or is defined broadly enough to later cover other ambient
  motion (swing and hit effects are gameplay-communicating and should stay
  exempt either way).
- **OD-9 — Dust MUST versus MAY.** R-W4.8 marks dust as MUST while the
  battlefield design scopes it as MAY; the two must agree before
  implementation, and dropping the MUST needs the user's explicit approval.
  Recommended if dust ships: MotionIntensity Off suppresses dust spawning;
  Reduced leaves dust unchanged. R-W4.4's dust cap and R-W4.2's dust shade
  obligations follow the same decision.
- **OD-10 — Shield per-skin proportion deltas.** The shield design proposes
  per-skin proportion deltas beyond R-W2.1's authorized channels (face tone,
  rattan-binding accent, slight outline curvature). Either amend R-W2.1 to
  allow bounded deltas inside one shared aspect-ratio band with the skin
  footprint never below the current Low-tier block, or the design drops the
  deltas. User approval required.
