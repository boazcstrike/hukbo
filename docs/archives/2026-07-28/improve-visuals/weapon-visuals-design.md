# Weapon Visuals — Design (Visual Improvement Pass, Workstream 1)

> **Archived: reference only.** This document is historical. Its task
> lists, commands, versions, and acceptance criteria are not instructions and
> are not maintained. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, and `docs/development/testing.md`. Note in
> particular that every document in this package quotes the seed-1 reference
> pair as stateHash `27DC94C6E9A01E35` / eventHash `372C9217E5CB8BE9`; that
> pair was already stale before implementation began. The current pair is
> stateHash `A883926A3B93792E` / eventHash `2A9F2D7054CD1805`, recorded in
> `docs/development/testing.md` under "The preset V3 reference pair".

## Status

Design document. Date: 2026-07-28. This document proposes the presentation-only
visual variant set for the four implemented weapons. It consumes
`docs/research/improve-visuals/weapons-shields-historical-research.md` (the
evidence base), `docs/agents/improve-visuals/requirements.md` (workstream 1 and
the cross-cutting R-X requirements), and
`docs/agents/improve-visuals/existing-code-analysis.md` (the current rendering
stack). It is bound by the historical accuracy policy in `CLAUDE.md` section 7.

This document depends on `visual-system-integration-design.md` (authored in
parallel in this same directory) for the variant catalog structure, the stable
identifier contract, the salted selection streams, the fallback-resolution
function, and the missing-variant diagnostics. Where this document names a
catalog entry, an identifier, or a fallback step, the shape of that machinery is
defined there, not here.

Per the workflow in `CLAUDE.md` section 6, a design document authorizes
nothing; the plan document follows it.

## Scope

In scope:

- Presentation-only visual variants for the four implemented weapons:
  `WeaponId.Kampilan`, `WeaponId.Wasay`, `WeaponId.Kalis`, `WeaponId.Itak`.
- One primary battle silhouette per weapon, material and condition tint
  variants, one documented accent (the Wasay rattan lashing band), and the
  inspector/armory-card treatment of the later or provisional forms the
  research bounded (Kampilan K2, Kalis L2 and L3).
- Evidence-tier metadata, player-facing pair-form labels, and inspector notes
  for every variant.
- The per-weapon fallback chain and the manual readability confirmation rows.

Out of scope, explicitly:

- **A new weapon appearance is not a new combat weapon.** No new `WeaponId`
  values, no renumbering or reordering of the existing enum, no change to
  reach, damage, timing, targeting, or any simulation value (R-X.11, R-X.12).
  Everything in this document is client-side presentation.
- The Cordilleran head axe (research W2) appears nowhere in the current roster
  (R-W1.4).
- Sprites, textures, content-pipeline changes, new packages (R-X.15, R-W6.18).
- Warrior body and clothing variation (workstream 3), shields (workstream 2),
  ground and motion (workstreams 4–5).

## Current state

From the existing-code analysis, verified against source on 2026-07-28:

- Every weapon is drawn procedurally from the single 1x1 white texture as a
  grip line, a broad blade line, and a highlight line, with per-weapon
  `gripEnd` / `widthMultiplier` values — Itak 0.30/2.1, Kampilan 0.22/2.45,
  Wasay 0.28/2.9, Kalis 0.16/1.5 — in `PawnRenderer.DrawWeapon` /
  `DrawBlade`, with per-weapon start/end offsets and thickness in
  `PawnGeometry.CreateWeaponLayout`. The Wasay additionally draws a square
  iron head at the far end of the haft as secondary equipment.
- Detail tiers come from `apparentScale = clamp(cameraZoom * 1.35, 0.72,
  2.40) * scaleMultiplier`: Low below 0.95, Medium below 1.80, High at or
  above 1.80. Camera zoom spans 0.05x–12x, so apparent scale saturates at both
  ends.
- `PawnAppearanceFactory.Create(entityId, weapon, shield)` is recomputed for
  every live pawn every frame — appearance is a pure function of
  `(EntityId, CombatLoadout)`, never cached, never stateful. Equipment
  identity (which weapon, whether shielded) comes only from the loadout,
  never from the entity ID; `PawnAppearanceFactoryTests` pins this.
- `PawnAppearance` already carries the pair-form labels (`Kampilan — Great
  Blade` and the other three), the `WeaponEvidenceTier` enum, and evidence
  notes. There is one appearance per weapon today — no variant concept.
- Swing animation: `SwingGeometry` computes four provisional phases
  (Anticipation, Strike, ImpactHold, Recovery); `SwingPoseResolver.Resolve`
  fills a caller-owned pose map allocation-free; `PawnRenderer` rotates the
  weapon lines through the resolved pose and strokes the swing arc trail.
- `PawnRenderer.GetBounds` is pose-blind by contract: culling never depends on
  animation phase.

## Evidence

The research document's honest conclusion, adopted here without expansion: the
sixteenth-century sources describe weapon *classes* with almost no object-level
detail, so the honest pattern is **one bounded period silhouette per weapon
plus restrained presentation-only variants**, with later-object features
explicitly labelled as later comparative projections. In particular:

- **Kampilan** — one bounded period silhouette (K1, Documented weapon class /
  Documented, form uncertain for the shape; anchored to Pigafetta's Mactan
  account, 1521). The bifurcated pommel, tip spikelet, hair tassels, and
  chain-mail guards are documented only on 18th–19th-century objects (K2,
  Provisional reconstruction for any 1500s presence) and the pommel creature
  motifs are culture-specific in the later record. Material and condition
  tints (K3) carry no historical claim.
- **Wasay** — one bounded silhouette (W1, Documented, form uncertain). The
  Cordilleran head axe (W2) is real but regionally specific, late-documented,
  and excluded from this roster on anti-generalization grounds. Tints and a
  rattan lashing band (W3) are presentation-only; rattan lashing is a
  ubiquitous documented hafting technique that asserts nothing specific.
- **Kalis** — the strongest attestation of the four (the word *calis* is
  recorded by Pigafetta in 1521). Three blade silhouettes are honestly
  proposable: straight (L1, Documented for name and class, conservative
  form), half-wavy (L2, Provisional reconstruction for the 1500s), fully wavy
  (L3, Provisional reconstruction, and per the later record itself not the
  most common form). The research itself doubts the wave's legibility at pawn
  scale.
- **Itak** — one bounded silhouette only (I1, composite tier Provisional
  reconstruction: the broad-blade class is Documented, form uncertain from
  Legazpi-era relations, but the *itak* name's period attestation remains
  unconfirmed). Proposing additional "historical" itak variants would be
  invention; the research forbids it and this design does not do it.

No form the research rejected is invented here. The variant counts below are
the research's counts.

## Requirements

This design implements workstream 1 of the requirements document. Binding
requirements, restated by ID (full text in
`docs/agents/improve-visuals/requirements.md`):

- **R-W1.1** — exactly one primary battle silhouette per weapon (K1, W1, L1,
  I1); silhouette identity derives from `CombatLoadout.Weapon` only.
- **R-W1.2** — material/condition variants vary color and tone only, within
  the documented material palette; silhouette classification unchanged at
  every tier.
- **R-W1.3** — variant selection is a pure salted SplitMix64-style function of
  `EntityId`, stable across frames and replays.
- **R-W1.4** — K2, L2, L3 never appear as pawn-scale battle silhouettes; they
  may appear only in inspector or armory-card text/art explicitly labelled as
  later or provisional forms. W2 appears nowhere.
- **R-W1.5** — Wasay may gain a rattan lashing band accent at Medium/High
  tier.
- **R-W1.6** — every variant records its evidence tier and note in
  inspector-surfaced metadata.
- **R-W1.7** — tints stay within a named, tested contrast envelope.
- **R-W1.8** — two to three tints per weapon; no more.
- **R-W1.9 (MAY)** — tier-gated, tone-only edge-wear variation on the
  highlight line.
- Cross-cutting: R-X.1 through R-X.5 (readability priority and tier rules),
  R-X.6/R-X.7/R-X.9/R-X.10 (labels, tiers, provisional marking, inspiration
  tags), R-X.11/R-X.12/R-X.15/R-X.16 (prohibited scope).

## Alternatives considered

1. **Tint-only variation with no variant catalog.** Ship the existing four
   silhouettes and add a color roll per pawn directly in
   `PawnAppearanceFactory`. Cheapest, but it leaves no stable identity for the
   inspector to name, no evidence-tier slot per variant, and no seam for the
   fallback chain — every future visual addition would repeat the ad-hoc
   pattern. Rejected in favor of the catalog infrastructure defined in
   `visual-system-integration-design.md`.
2. **Multiple invented silhouettes per weapon.** Three or four blade shapes
   per weapon would maximize on-screen variety, but the evidence carries
   exactly one bounded period silhouette for Kampilan, Wasay, and Itak. The
   research says inventing more "would be invention"; the policy in
   `CLAUDE.md` section 7 forbids presenting it. Rejected.
3. **Promoting the wavy Kalis forms to pawn-scale silhouettes.** The half-wavy
   and fully wavy blades are the most recognizable kris imagery, but making
   them battle silhouettes would project iconic later Moro forms backwards
   onto 1521 Visayas (the research's explicit warning), and a wavy edge a few
   pixels long degenerates into a blurry line, degrading the thrusting-blade
   read that R-X.3 protects. Rejected; R-W1.4 also forbids it outright.
4. **Sprite or texture-based weapon art.** Would allow real ornament detail,
   but there is no texture pipeline, no atlas, no loader, no measurement
   harness to regress against, and R-X.15/OD-4 point the pass at the
   procedural path. Rejected for this pass.
5. **The recommended approach below** — honest silhouette counts, bounded
   tint variants through the shared catalog, later forms confined to
   inspector/armory art.

## Recommended approach

### Catalog structure

Each weapon contributes entries to the client-side visual catalog defined in
`visual-system-integration-design.md`. Identifiers follow that document's
stable-identifier contract (pinned values, do-not-renumber-or-reword, test
enforced). The identifiers proposed here use the shape
`weapon.<weapon>.<variant>` for silhouettes and
`weapon.<weapon>.tint.<name>` for tint variants.

Selection is two independent salted streams per pawn (R-W1.3, R-W6.2):

- A *silhouette stream* that is degenerate for every weapon in this pass —
  each weapon has exactly one pawn-scale silhouette, so the stream exists for
  catalog totality and future use, not to vary anything today. Kalis L2/L3
  are catalog entries flagged inspector-only and are never selectable by the
  pawn stream.
- A *tint stream* selecting one of the weapon's two or three tint entries by
  modulo, from `EntityId` XOR a new named salt constant, mixed with the
  SplitMix64-finalizer pattern. The salt is new; the three existing
  appearance salts and the plains salt are not reused, so existing appearance
  does not shift (R-W6.2).

### Shared rules for every variant

- **Player-facing name.** Every variant displays the unchanged pair-form
  label of its weapon (`Kampilan — Great Blade`, `Wasay — War Axe`, `Kalis —
  Thrusting Blade`, `Itak — Work Blade`). A variant never introduces a new
  cultural name, never drops the descriptor half, and never shows a bare
  Filipino term (R-X.6). Variant-specific text ("later ornamented form",
  "half-waved form, later attestation") appears only as an inspector note
  alongside the evidence tier.
- **Silhouette and reach consistency.** A variant never changes the weapon's
  blade start/end offsets, thickness class, or width multiplier beyond the
  tolerance that keeps `PawnGeometryTests`' silhouette classification stable.
  Visual reach is pinned: the drawn blade length stays identical across all
  variants of a weapon, because a longer-looking blade on the same
  `WeaponId` would imply a mechanical reach difference that does not exist —
  the same false-cause rule the shield workstream applies (R-X.12 rationale).
  Any per-variant length delta is therefore zero by design, not merely small.
- **Left/right orientation.** The weapon stays on the pawn's existing weapon
  side exactly as `PawnGeometry.CreateWeaponLayout` places it today; the
  shield keeps its existing side. No variant introduces mirroring — facing
  mirroring is deliberately out of scope in `PawnGeometry` (recorded at the
  layout comment) and this design does not reopen it.
- **Idle pose.** The idle weapon is the layout's default line set (grip line,
  blade line, highlight line, plus the Wasay head rectangle), unchanged per
  variant except for color.
- **Attack pose.** Variants integrate with `SwingPoseResolver` by changing
  nothing: the resolver's pose (rotation, phase, impact-hold branch) applies
  to the variant's lines exactly as it applies today, because every variant
  of a weapon shares that weapon's geometry. The swing arc trail keeps its
  current color logic. No variant adds pose channels, phase changes, or
  per-variant timing; `SwingGeometry`'s provisional phase fractions are
  untouched.
- **Shield compatibility.** Every variant is compatible with both
  `ShieldId.None` and `ShieldId.TallHardwood`. No variant may occlude the
  shield block or the faction ring at Low tier (R-X.1); the weapon draw order
  relative to the shield stays as today.
- **Bounds.** No variant grows the pawn's possible extent, so
  `PawnRenderer.GetBounds` and pose-blind culling are untouched (R-X.5).
- **Evidence metadata.** Every catalog entry carries its tier — one of
  `Documented`, `Documented, form uncertain`, `Provisional reconstruction`,
  or the explicit `presentation-only, no historical claim` marker — plus its
  note and, where applicable, a place-and-time inspiration tag
  (`Mactan — 1521`), surfaced in the agent inspector (R-X.7, R-X.10).

### Palette

Tints draw from the documented natural-material palette, expressed as named
`PROVISIONAL` client constants (values below are proposals for the plan; the
contrast-envelope tests of R-W1.7 gate the final values):

| Constant (proposed) | Role | Proposed value |
| --- | --- | --- |
| `IronBlueBlack` | fresh iron blade | `#384249` (matches the R-W3.8 swatch) |
| `IronWornGrey` | worn iron blade | a step lighter/duller than `IronBlueBlack`, inside the envelope |
| `PalmRattanOchre` | haft / grip wood | warm ochre in the existing palm/rattan range |
| `CharredWoodBrown` | dark hilt wood | existing charred-wood tone (shared with the shield face) |
| `GripWarmOchre` | plain grips | warm ochre, lighter than `PalmRattanOchre` |
| `RattanLashingTone` | Wasay lashing band | ochre distinct from both haft tones at Medium+ |

The envelope: every blade tone must remain legible against all ground shades
(the backdrop lerp ceiling 0.22 bounds those) and against every pawn clothing
color, at every detail tier, in all five themes. The envelope bounds are named
constants with pin tests (R-W1.7). Faction color never appears on a weapon.

### Historically meaningful versus presentation-only

This separation is explicit and binding on the plan:

- **Historically meaningful (evidence-tiered, silhouette-level):** Kampilan
  K1's length and forward weight; Wasay W1's broad head and short haft; Kalis
  L1's straightness and slimness; Itak I1's shortness and plainness; the
  Kalis wave (which is exactly why L2/L3 stay provisional and off the pawn);
  the Kampilan pommel forms (which is exactly why K2 stays off the pawn).
- **Presentation-only (no historical claim, tone-level):** every tint entry,
  the edge-wear highlight option, and the Wasay lashing band's *color* (the
  band's existence is a documented ubiquitous technique; its rendering here
  asserts nothing about any specific object).

### Per-weapon design

#### Kampilan — Great Blade

Variant set: **1 pawn silhouette + 3 tints; 1 inspector-only form.**

| ID (proposed) | Kind | Tier | Note |
| --- | --- | --- | --- |
| `weapon.kampilan.k1` | Pawn silhouette (default and only) | Documented, form uncertain | `Mactan — 1521`; widening profile and truncated tip read back from later objects, disclosed in the note |
| `weapon.kampilan.tint.freshIron` | Tint | presentation-only, no historical claim | `IronBlueBlack` blade, `CharredWoodBrown` hilt |
| `weapon.kampilan.tint.wornIron` | Tint | presentation-only, no historical claim | `IronWornGrey` blade, `CharredWoodBrown` hilt |
| `weapon.kampilan.tint.ochreHilt` | Tint | presentation-only, no historical claim | `IronBlueBlack` blade, `PalmRattanOchre` hilt |
| `weapon.kampilan.k2` | Inspector/armory art only | Provisional reconstruction | "Later ornamented form"; bifurcated pommel; never a pawn silhouette; if any pommel detail is ever drawn in armory art it is the plain non-zoomorphic curve, never a creature motif |

Geometry (K1), per detail tier:

- **Low (apparent scale < 0.95):** the current three-line composition — grip
  line, broad blade line (width multiplier 2.45), highlight line — at the
  longest blade extent in the roster. This is the tier where "Kampilan-armed"
  must stay classifiable (R-X.2); tints at Low are sub-threshold tone shifts
  only.
- **Medium (0.95–1.80):** same lines; the blade line may widen slightly
  toward the tip within the existing thickness class to suggest the
  forward-heavy profile (a per-segment width step on the existing blade
  stroke, still rectangles from the 1x1 texture; no new draw-call class).
- **High (≥ 1.80):** Medium plus the optional edge-wear tone variation on the
  highlight line (R-W1.9), tone-only.

K2 is never selectable by the pawn stream. Its catalog entry exists so the
inspector can honestly say the ornamented form exists and is later; whether an
armory-card *drawing* (as opposed to text) ships is an open decision below.

#### Wasay — War Axe

Variant set: **1 pawn silhouette + 3 tints (one carrying the lashing
accent); 1 recorded-excluded form.**

| ID (proposed) | Kind | Tier | Note |
| --- | --- | --- | --- |
| `weapon.wasay.w1` | Pawn silhouette (default and only) | Documented, form uncertain | Broad iron head, hardwood haft; an everyman's tool-weapon, not elite kit |
| `weapon.wasay.tint.ochreHaft` | Tint | presentation-only, no historical claim | `PalmRattanOchre` haft, `IronBlueBlack` head |
| `weapon.wasay.tint.charredHaft` | Tint | presentation-only, no historical claim | `CharredWoodBrown` haft, `IronBlueBlack` head |
| `weapon.wasay.tint.lashedWorn` | Tint + accent | presentation-only, no historical claim | `IronWornGrey` head plus the rattan lashing band at the head-haft junction, Medium/High tier only (R-W1.5) |
| *(no ID assigned)* | Cordilleran head axe (research W2) | excluded | Recorded for a future campaign layer with regional factions; appears nowhere in this roster (R-W1.4) |

Geometry (W1), per detail tier:

- **Low:** the current haft line plus square iron head rectangle, drawn as
  secondary equipment before the torso. Head mass is the classification
  feature; it must stay readable at the 0.72 apparent-scale floor.
- **Medium:** same, plus the lashing band — one short rectangle across the
  haft at the head junction in `RattanLashingTone` — when the selected tint
  carries it.
- **High:** Medium, plus the head may show the worn-iron tone split (edge
  versus body of the head as two tones), tone-only.

The head axe W2 deliberately receives no catalog identifier: an identifier is
a commitment surface, and this form must not be reachable by any fallback.

#### Kalis — Thrusting Blade

Variant set: **3 cataloged silhouettes, of which 1 is pawn-scale; + 2
tints.**

| ID (proposed) | Kind | Tier | Note |
| --- | --- | --- | --- |
| `weapon.kalis.l1` | Pawn silhouette (default and only pawn-scale form) | Documented (name and class); conservative form | `Cebu — 1521` word attestation; slim, straight, symmetric, one-handed |
| `weapon.kalis.l2` | Inspector/armory art only | Provisional reconstruction | "Half-waved form, later attestation"; never a pawn silhouette (R-W1.4) |
| `weapon.kalis.l3` | Inspector/armory art only | Provisional reconstruction | Fully wavy; iconic but not the most common in the later record; never a pawn silhouette (R-W1.4) |
| `weapon.kalis.tint.freshIron` | Tint | presentation-only, no historical claim | `IronBlueBlack` blade, `GripWarmOchre` hilt |
| `weapon.kalis.tint.darkHilt` | Tint | presentation-only, no historical claim | `IronBlueBlack` blade, `CharredWoodBrown` hilt |

Geometry (L1), per detail tier:

- **Low:** the current three-line composition at the slimmest width
  multiplier in the roster (1.5) — slimness *is* the classification feature
  against the Kampilan, so no tint may thicken the read.
- **Medium:** same; hilt tone becomes visible as a distinct grip segment.
- **High:** Medium plus optional highlight-line wear, tone-only. No wave is
  ever drawn on the pawn at any tier.

L2 and L3 carry catalog identifiers because the inspector names them and the
armory card may one day draw them; the selection stream can never return them
(enforced by a test in the integration design's totality suite). No later hilt
or pommel forms (horse-hoof, cockatoo, gangya guard) are drawn anywhere —
they are later Sulu/Moro attributions and too small to read regardless.

#### Itak — Work Blade

Variant set: **1 pawn silhouette + 2 tints. No other forms exist or are
invented.**

| ID (proposed) | Kind | Tier | Note |
| --- | --- | --- | --- |
| `weapon.itak.i1` | Pawn silhouette (default and only) | Provisional reconstruction (composite) | Broad-blade class Documented, form uncertain (Legazpi-era relations, 1565–1569); the *itak* name's period attestation unconfirmed, disclosed in the note |
| `weapon.itak.tint.plainOchre` | Tint | presentation-only, no historical claim | `IronBlueBlack` blade, `GripWarmOchre` grip |
| `weapon.itak.tint.wornField` | Tint | presentation-only, no historical claim | `IronWornGrey` blade, `PalmRattanOchre` grip — the "used-up tool" read |

Geometry (I1), per detail tier:

- **Low:** the current composition at the shortest blade extent, width
  multiplier 2.1 — wide-for-length is the classification feature. The
  off-hand piece stays Medium+ as today.
- **Medium:** same plus the off-hand secondary piece and visible grip tone.
- **High:** Medium plus optional highlight wear, tone-only.

The itak deliberately has the narrowest variant set: it is the plainest
weapon in the roster and the research supports nothing more. Two tints, not
three, is the honest ceiling here (R-W1.8 sets three as the maximum, not the
target for every weapon).

### Fallback chain

Every weapon visual lookup resolves through the chain defined in
`visual-system-integration-design.md` (R-W6.4), instantiated per weapon as:

1. **Specific variant** — the tint (and, degenerately, silhouette) entry the
   salted streams selected, e.g. `weapon.kampilan.tint.wornIron`.
2. **Weapon default** — the weapon's primary silhouette entry with its first
   tint (`weapon.kampilan.k1` + `tint.freshIron`), used when a tint
   identifier fails to resolve.
3. **Model-category default** — a generic bladed-weapon drawable (the
   existing three-line composition with `IronBlueBlack`/`GripWarmOchre`
   default tones and Kalis-class proportions), used when the weapon's own
   entries fail to resolve; for the Wasay the category default is the generic
   hafted drawable (haft plus head rectangle).
4. **Diagnostic placeholder** — the deliberately conspicuous primitive from
   the integration design (never invisible, never a crash), with a
   once-per-identifier `warn` on the `assets` channel via the new
   `LogEvents` constants (R-W6.5).

Resolution is total: for every valid `(EntityId, CombatLoadout)` some
drawable resolves, and each chain step is reachable in tests.

### Readability confirmation

Automated tests cannot judge legibility; humans can. The plan adds these rows
to `docs/development/testing.md`, created `PENDING`, flippable only by a human
at an interactive desktop per `CLAUDE.md` section 6:

- At **minimum zoom** (0.05x; apparent scale saturated at the 0.72 floor, Low
  tier) with 200+ pawns: each of the four weapon roles remains classifiable
  and tint variation is invisible or sub-threshold.
- At **normal zoom** (the initial fit zoom, typically Medium tier): weapon
  role identifiable per pawn; tints read as material variation, not as
  different weapons.
- At **maximum zoom** (12x; apparent scale saturated at 2.40, High tier):
  tint and wear variation visible without breaking role recognition; the
  Wasay lashing band reads as a band, not as damage or a new weapon part.

No automated test claims to prove these rows.

## Rejected approaches

- Inventing additional "historical" silhouettes for Kampilan, Wasay, or Itak
  (alternative 2) — the evidence carries one bounded form each.
- Pawn-scale wavy Kalis blades (alternative 3) — forbidden by R-W1.4 and
  illegible at pawn scale.
- Any pommel creature motif, tip spikelet, tassel, or chain-mail guard on any
  pawn — later-object features, culture-specific in the later record;
  drawing them on generic pawns is the generalization the policy forbids.
- The Cordilleran head axe in any form in this roster.
- Per-variant blade length or reach differences — a visual reach change on an
  unchanged `WeaponId` shows the spectator a false cause.
- Sprite/texture art and any new dependency (alternative 4).
- More than three tints per weapon — R-W1.8; review load without spectator
  value.

## Dependencies

- **`visual-system-integration-design.md`** (parallel, same directory):
  catalog record shape, stable-ID contract and pin tests, salted stream
  recipe, fallback-resolution function, diagnostic placeholder, `LogEvents`
  additions. This document's variant tables are content for that machinery.
- Existing code this design extends (no ownership transfer):
  `PawnAppearanceFactory` / `PawnAppearance` (selection precedent, labels,
  tiers), `PawnGeometry.CreateWeaponLayout` (per-weapon layout),
  `PawnRenderer.DrawWeapon` / `DrawBlade` / `DrawSecondaryEquipment` (draw),
  `SwingGeometry` / `SwingPoseResolver` (pose, unchanged),
  `PawnAppearanceFactoryTests` / `PawnGeometryTests` (pinned invariants,
  extended not weakened).
- The requirements document's cross-cutting rules (R-X.*) and workstream 6
  infrastructure requirements (R-W6.1–R-W6.5, R-W6.16, R-W6.17).
- The canonical gate `./scripts/verify.ps1` after integration; the seed-1
  hashes are untouched by design (pure presentation).

## Risks

1. **Tint illegibility or noise at scale.** Two hundred pawns with three
   tints each could read as visual noise rather than material variety.
   Mitigated by the contrast envelope (R-W1.7), sub-threshold-at-Low rule
   (R-X.2), and the manual rows above; the tint count is deliberately small.
2. **Silhouette drift through "small" width tweaks.** The Medium-tier
   forward-widening on the Kampilan could, if overdone, blur the
   Kampilan/Itak distinction. Mitigated by keeping the existing width
   multipliers as the classification anchor and pinning classification in
   geometry tests.
3. **Catalog identifier churn.** Inspector text and tests will reference the
   IDs; renaming later is a breaking change by contract. Mitigated by the
   integration design's pin tests and by treating the IDs above as proposals
   finalized once in the plan.
4. **Scope creep toward armory-card art.** K2/L2/L3 invite drawing. If the
   armory card ships art rather than text, that art must carry the
   later/provisional label visibly; the safer default is text-only (open
   decision below).
5. **Draw-call growth.** Each tint adds zero calls; the widening step and
   lashing band add at most one or two rectangles per pawn at Medium+. The
   render measurement task (R-W6.12) exists precisely because no render
   benchmark measures this today; budgets stay ESTIMATE until it runs.

## Open decisions

- **OD-W1-a — Armory-card art for K2/L2/L3: text only, or drawn?** Default
  recommendation: inspector text only in this pass; drawn armory art is a
  follow-on. (The requirements permit either; drawing raises risk 4.)
- **OD-W1-b — Final tint hex values.** The table above proposes values; the
  contrast-envelope tests and the five-theme check gate the finals. Decided
  in the plan, not here.
- **OD-W1-c — Kampilan Medium-tier forward-widening: include or defer?** It
  is the one geometry (not tone) refinement proposed; if the classification
  tests cannot cleanly bound it, ship K1 with today's uniform blade width
  and keep the widening as a recorded possibility.
- Inherited: **OD-4** (procedural direction confirmation) from the
  requirements document — this design is written to the procedural path.
  Resolved 2026-07-28: the user confirmed fully procedural rendering, so no
  hedge remains.

## Acceptance criteria

Automated (GPU-independent xunit, per the established split):

- Variant-stability: same `(EntityId, CombatLoadout)` yields the same variant
  IDs on every call.
- Equipment-identity-from-loadout-only, extended to variants: no variant
  stream ever changes which weapon silhouette is drawn.
- Silhouette-classification invariance under every tint at every tier.
- Pawn-scale exclusion: the selection stream can never return
  `weapon.kampilan.k2`, `weapon.kalis.l2`, or `weapon.kalis.l3`; W2 has no
  identifier to return.
- Palette/contrast-envelope pins on the named constants.
- Evidence metadata presence: every catalog entry carries a tier and note
  (extending the existing every-weapon-has-an-evidence-note test).
- Fallback totality and per-step reachability for all four weapons.
- Tier-gating tests at the exact thresholds 0.95 and 1.80 (lashing band and
  wear are Medium+; Low is tone-only).
- Bounds independence: `GetBounds` unchanged by variant selection.
- The canonical gate passes with the seed-1 state hash, event hash, outcome,
  and event stream unchanged.

Manual (rows in `docs/development/testing.md`, created `PENDING`):

- The three readability rows defined above (minimum / normal / maximum zoom).
- Inspector shows, for a selected pawn, the pair-form weapon label, the
  variant's evidence tier, and its note.
- Forced-failure run shows the diagnostic placeholder conspicuously (shared
  with the integration design's checklist).

This document does not authorize implementation.
