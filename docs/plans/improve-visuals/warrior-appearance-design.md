# Warrior Appearance Design — Component System and Preset Roster

Date: 2026-07-28. Workstream 3 of the Hukbo visual improvement pass.

## Status

Draft design, awaiting review. This document consumes
`docs/research/improve-visuals/warrior-appearance-historical-research.md`,
`docs/agents/improve-visuals/requirements.md` (workstream 3 and the R-X
cross-cutting requirements), and
`docs/agents/improve-visuals/existing-code-analysis.md`. It is authored in
parallel with `visual-system-integration-design.md`, which owns the shared
catalog, fallback, settings, diagnostics, and measurement infrastructure this
design depends on. A plan document must follow before any code changes.

On 2026-07-28 the user resolved all ten package open decisions — including
OD-5 (the earned red putong stays excluded this pass, recorded as a backlog
item in `docs/plans/TODO.md`) and OD-3 (the Unscoped-generic block accepted
as the sole Mindanao/Sulu coverage this pass) — and approved the 23-task
first milestone for implementation. The decision record is in the package
README.

## Scope

In scope:

- A client-side appearance component system following the research's eleven
  categories (A stature/build, B hair, C head covering, D torso garment,
  E lower garment, F armor layer, G sash/belt, H accessories, I adornment,
  J natural-dye palette, K condition).
- At least fifty authored presets expressed as reviewable component recipes —
  not fifty unique sprites, and not fifty palette swaps. The shipped roster in
  this document contains 53 presets across four regional blocks.
- An objective minimum-differentiation criterion between presets, enforced by
  an automated test.
- Deterministic per-pawn preset selection extending the existing
  `PawnAppearanceFactory` salt pattern.
- Evidence-tier and scope-tag metadata surfaced in the agent inspector.

Out of scope:

- Any change to `Hukbo.Core`, simulation state, hashes, events, RNG streams,
  or AI (R-X.11). Appearance is presentation-only, recomputed per frame from
  immutable identity.
- Weapon and shield variants (workstreams 1 and 2, designed separately).
- The earned red putong (component C2) — excluded per OD-5, resolved
  2026-07-28; recorded as a backlog item in `docs/plans/TODO.md` (see Open
  decisions).
- Sprites, textures, atlases, or content-pipeline changes. Everything renders
  through the existing procedural rectangle-and-line pipeline on the 1x1
  white texture.
- Any new `WeaponId`, `ShieldId`, or `ArmorId` value (R-X.12).

## Current state

Verified against the existing-code analysis:

- Pawns are procedural: a ground ring in fixed faction color, a stepped-capsule
  torso, a stepped-disk head, primitive weapon and shield shapes, all drawn
  from one runtime 1x1 white texture inside the single arena `SpriteBatch`
  pass. There are no sprites anywhere.
- `PawnAppearanceFactory.Create(entityId, weapon, shield)` already produces
  deterministic appearance: the entity ID is XORed with three named salt
  constants, run through a SplitMix64-finalizer-style mix, and moduloed into
  stature (3), build (3), head treatment (3), clothing color (4), accent
  color (3), skin color (3), and head-treatment color (3). Equipment identity
  comes only from the Core loadout, never from the entity ID — pinned by
  `PawnAppearanceFactoryTests`.
- Detail tiers come from `apparentScale = clamp(cameraZoom * 1.35, 0.72,
  2.40) * scaleMultiplier`: Low below 0.95, Medium below 1.80, High at or
  above 1.80. Existing precedent: shield draws at every tier; head treatment
  and the Itak off-hand draw at Medium and above; the belt accent draws at
  High only.
- Faction colors are fixed constants (blue 64,164,255; red 255,91,105; gold
  231,199,84), deliberately theme-independent, painted on the ground ring and
  outline — never on garments.
- `PawnAppearance` already carries player-facing pair-form labels, the
  `WeaponEvidenceTier` enum, and evidence notes: the metadata home for this
  design already exists.

This design replaces the three-trait placeholder clothing variation with the
component system while keeping every pinned invariant.

## Evidence

The sole historical evidence base is
`docs/research/improve-visuals/warrior-appearance-historical-research.md`,
governed by `CLAUDE.md` section 7. Its rules bind this design directly:

- Every component option carries the research's evidence tier (Documented;
  Documented, form uncertain; Provisional reconstruction) or an explicit
  "presentation-only, no historical claim" marker (categories A and K).
- Player-facing pair-form labels are limited to the three terms the research
  cleared inside the hundred-year attestation window: **Putong — Head Wrap**,
  **Bahag — Loincloth**, **Chinina — Collarless Jacket**. Pending terms
  (*barote*, *kandit*, *panika*, *kamagi*, *batuk*, *kolombiga*) stay in
  inspector metadata explicitly flagged as pending verification. Failed terms
  (*salakot*) are not used at all.
- The research's six co-occurrence rules and ten prohibited combinations
  (section 4 of the research; R-X.8) are binding and are encoded in an
  automated combination validator.
- A preset's evidence tier is the weakest tier among its rendered components
  (the weakest-link rule), so no preset can launder a provisional component
  under a documented headline.

## Requirements

This design implements R-W3.1 through R-W3.15 and is bound by R-X.1 through
R-X.16 of `docs/agents/improve-visuals/requirements.md`. The requirements it
most directly shapes:

- R-W3.1/R-W3.2: components follow research categories A–K exactly; at least
  fifty presets across the four regional blocks.
- R-W3.4: the combination validator encodes all co-occurrence rules and
  prohibitions and every shipped preset passes it in a test.
- R-W3.5: preset selection is a pure salted function of immutable identity.
- R-W3.6/R-W3.7: rendering confined to silhouette changes, color blocks, and
  at most two accent marks; tattooing is a tone shift only.
- R-W3.13 and R-X.1/R-X.2: tier gating preserves the read-order priority.
- R-X.7: evidence tier surfaced in the agent inspector for every preset.

## Alternatives considered

1. **Fifty hand-drawn sprite variants.** Rejected. There is no sprite
   pipeline, no texture loading, no atlas, and no render benchmark to measure
   the regression such infrastructure would cause. It also violates the
   procedural direction confirmed by open decision OD-4 in the requirements.
2. **Fifty palette swaps over the existing three-trait system.** Rejected. It
   fails R-W3.2 ("differs in silhouette or documented color logic, not merely
   hue"), produces no reviewable historical claims, and wastes the research's
   silhouette evidence (head wraps, armor thickening, waist cloths).
3. **Fully generative combination (no authored presets — roll every category
   independently per pawn).** Rejected. Unconstrained rolls generate
   prohibited combinations (tattooed Tagalogs, gold-laden levies,
   feathered Visayans) faster than a validator can be written to chase them,
   and produce nothing a historical reviewer can review. Authored recipes are
   the review surface; the validator then only has to check a closed list.
4. **Authored component recipes composed from a validated component catalog
   (chosen).** Roughly fifty rows of data, each a named, reviewable claim;
   generation cost near zero because every component renders through the
   existing primitive channels.

## Recommended approach

### Component catalog

Each component option from the research becomes one immutable catalog entry
(client-side, the `PawnAppearance` precedent; catalog infrastructure per
`visual-system-integration-design.md`). Entry fields: stable identifier
(the research's code, for example `C1`), display label (pair form only where
cleared), evidence tier, scope, "must not generalize" note, render channel,
and tier gate. The renderable channels are exactly the research's three:

1. Silhouette: head-wrap wedge (C1/C3), hair knot bump (B1), loose-hair
   fringe (B2), bare disk (B3/B4/C5), flat hat disk (C4), vertical headdress
   accent (C6), widened torso capsule (F2/F3/F4), pale head cap (F5),
   skirted lower block (E3) versus banded (E1/E2), diagonal shoulder stripe
   (H2), waist side-blade accent (H1), sash/belt line (G1/G2/G3).
2. Color blocks: skin tone, tattoo tone shift (I1/I2), garment dye from the
   category-J constants, armor material tone.
3. Accent marks: gold edge pixel (C3), gold ear pixel (I4), gold collar line
   (I5), dyed-bahag gold pixel (E2). At most two accent marks render per
   pawn; excess accents in a recipe are inspector-text only.

Category A (stature and build) stays exactly as implemented today — an
independent salted roll, orthogonal to preset identity, asserting nothing
historical. Category K (condition) renders as tone desaturation/darkening and,
for K5, the loosened-knot variant of B1 at High tier. Non-renderable
adornments (I3, I6, I7, I8, H3) are inspector flavor text only (R-W3.15).

### The dye palette

The category-J swatch table becomes named constants: undyed cream `#E7D8B7`,
indigo `#354D6B`, blue-black `#2A3140`, sappan red `#8F3F35`, turmeric yellow
`#C9A23F` (sparing use), bark brown `#7A5A3A`, gold accent `#D0A64A`, iron
blue-black `#384249`, the existing skin-tone range, and the tattoo tone shift
(skin tone shifted darker and cooler by a fixed, test-pinned delta). Garments
draw from this closed set only. Faction color never appears on any garment;
it stays on the ground ring and outline (R-W3.8).

### Readability priority preservation

The fixed read order — faction > weapon role > shield > state marks > body
and clothing — is preserved by four hard constraints:

1. **The faction marker is untouchable.** No component renders below the
   torso base or over the ground ring; the ring, weapon silhouette, shield
   block, and dead/selection marks keep their current geometry and tiers
   (R-X.1). Appearance never occludes the weapon line or shield block:
   clothing and adornment render only within the torso capsule and head disk
   footprint, plus at most one pixel of accent overhang.
2. **Saturation cap.** Garment colors come only from the closed dye set,
   which is deliberately desaturated relative to the faction constants. A
   contrast-envelope test pins a minimum color distance between every dye
   constant and every faction constant, so no garment can read as a faction
   signal (extends R-W1.7's envelope discipline; supports R-W6.10).
3. **Area cap.** Colored garment area never exceeds the torso capsule area;
   accent marks are at most two per pawn and at most 2 pixels each at
   apparent scale 1. Named constants with tests.
4. **Tier gating.** At Low tier a preset contributes tone only (skin tone,
   tattoo shift, garment base tone folded into the existing torso fill); no
   silhouette component draws. Armor capsule widening is bounded inside the
   existing build-multiplier envelope (width factor at most 1.18) so a
   widened torso can never be misread as a shield block at any tier.

### Zoom tier assignment

Tier assignment is a pure function of apparent scale at the existing 0.95 and
1.80 thresholds (R-X.4):

| Tier | Renders |
| --- | --- |
| Low (below 0.95) | Skin tone, tattoo tone shift, garment base tone, armor material tone folded into torso fill, armor width within the build envelope. Nothing else. |
| Medium (0.95–1.80) | All silhouette components: head-wrap wedge, hair bump/fringe, hat disk, headdress accent, armor widening with material color, skirted versus banded lower garment, sash/belt line, side-blade accent, shoulder stripe, shell-cap accent. Condition desaturation. |
| High (1.80 and above) | Accent marks (gold edge, ear, collar, bahag pixels), the existing belt accent, K5 loosened-knot detail. |

This matches the existing precedent (head treatment at Medium+, belt at High)
and satisfies R-W3.13.

### Deterministic selection

Selection extends the `PawnAppearanceFactory` recipe exactly — XOR with a new
named salt, SplitMix64-finalizer mix, modulo — with two streams and three
immutable inputs (`EntityId`, `CombatLoadout`, `Scenario.Seed`):

1. **Block assignment (per faction, per scenario).**
   `Mix(Scenario.Seed ^ BlockAssignmentSalt ^ (ulong)factionId)` selects each
   faction's regional block from an allowed assignment table (a faction draws
   from exactly one of Visayan, Tagalog, Northern Luzon, or Generic levy per
   match, optionally blended with the levy block at a fixed PROVISIONAL
   ratio). One faction, one block: this is what enforces prohibition 10 —
   no pan-archipelagic pool ever exists at selection time, so no army mixes
   regions within a warrior and no battle presents one region as "the
   Philippines" without the inspector saying which region each preset is.
2. **Preset selection (per pawn).** `Mix(EntityId ^ PresetSelectionSalt)`
   indexes a weighted table over the faction's block, filtered by loadout
   compatibility (see the roster's compatible-loadouts column). Elite and
   leader presets carry small PROVISIONAL rarity weights (target: at most
   roughly 2% each) so earned and elite markers stay meaningful (R-W3.14).

Both salts are new named constants and never reuse the three existing
appearance salts or the plains backdrop salt, so today's stature, build, skin,
and decal outcomes do not shift (R-W6.2). All inputs are immutable for the
match, so selection is stable across frames, replays of the same seed, and
save/resume — no stored state exists to diverge.

### Minimum differentiation criterion

Defined precisely, and enforced by an automated pairwise test **within each
regional block**:

- **Silhouette-affecting categories** are: B (hair), C (head covering),
  D-class (bare-chested versus jacketed), E-class (banded E1/E2 versus
  skirted E3), F (armor layer), H1 (side-blade accent), and H2 (shoulder
  cloth).
- **Countable categories** are all component categories except J (palette)
  and stature/build (A). Condition (K) counts.
- **The criterion:** for every unordered pair of shipped presets *within the
  same block*, either the two recipes differ in at least one
  silhouette-affecting category, or they differ in at least two countable
  categories. A pair that differs only in palette hue, or only in one
  non-silhouette category, fails the test and fails review.

The criterion is deliberately scoped within each block, not across the whole
roster, and cross-block recipe near-duplicates — including recipe-identical
pairs such as VIS-01/LEV-01 — are acceptable. The historical justification:
a plain bare-chested warrior in knotted hair, cream bahag, and a cloth belt
looked much the same everywhere in the archipelago, so a cross-block
near-duplicate is the honest depiction; what differs between those presets is
the scope tag — the claim the inspector makes about who is depicted — not the
clothing. And because block assignment gives each faction exactly one
regional block per match, two blocks never co-exist inside one faction's
army, so a cross-block twin can never appear beside its double in a way that
would waste variation on screen.

This is the "not fifty palette swaps" rule made mechanical.

### Preset roster

Recipe notation uses the research codes. Every preset implicitly includes the
category-A stature/build roll and a skin-tone roll. Evidence tier is the
weakest rendered component (D = Documented, DFU = Documented, form uncertain,
PR = Provisional reconstruction); presentation-only components (K) are
excluded from the weakest-link computation and marked in metadata as "no
historical claim". "Any" in the loadout column means any of the four weapons
with or without the tall shield. Presets containing H1 (a sheathed side
blade) are restricted to Wasay-armed pawns, because the research scopes H1 to
figures whose main weapon is not a blade. Fallback names the preset the
renderer resolves to if a component entry is unresolvable; block bases fall
back to LEV-01, and LEV-01 falls back through the full resolution chain
(model-category default, then the diagnostic placeholder) defined in
`visual-system-integration-design.md`.

#### Visayan block (20 presets, scope tag: Visayan)

| ID | Component recipe | Tier | Scope | Loadouts | Fallback |
| --- | --- | --- | --- | --- | --- |
| VIS-01 | B1 knot, C5 bare head, D1 bare chest, E1 bahag (cream), G2 belt, K1 | DFU | Visayan | Any | LEV-01 |
| VIS-02 | B1, C5, D1 + I2 partial tattoo, E1, G3 cord belt, K1 | PR | Visayan | Any | VIS-01 |
| VIS-03 | B1, C5, D1 + I1 full tattoo, E1, G1 red sash, K2 | DFU | Visayan | Any | VIS-01 |
| VIS-04 | B4 tucked hair, C1 putong (cream), D1 + I2, E1, G2, K1 | DFU | Visayan | Any | VIS-01 |
| VIS-05 | B4, C1 putong (indigo), D1 + I1, E1, G1, K1 | DFU | Visayan | Any | VIS-04 |
| VIS-06 | B4, C1 putong (blue-black), D1, E1, G3, K5 | PR | Visayan | Any | VIS-04 |
| VIS-07 | B3 cropped, C5, D1 + I2, E1, G2, K2 | DFU | Visayan | Any | VIS-01 |
| VIS-08 | B1, C5, D4 abaca jacket, E1, G2, K1 | DFU | Visayan | Any | VIS-01 |
| VIS-09 | B4, C1 (cream), D4, E1, G2, K1 | DFU | Visayan | Any | VIS-08 |
| VIS-10 | B1, C5, D4, E1, G3, K4 | PR | Visayan | Any | VIS-08 |
| VIS-11 | B4, C1 (cream), F2 corded fiber armor over D1 + I2 (arms), E1, G2, K2 | DFU | Visayan | Any | VIS-04 |
| VIS-12 | B1, C5, F2 over D1 + I1 (arms), E1, G1, K5 | DFU | Visayan | Any | VIS-01 |
| VIS-13 | B4, C3 gold-edged putong, D1 + I1, I4 earring, E2 dyed bahag, G1, K1 | DFU | Visayan (elite) | Any | VIS-05 |
| VIS-14 | B4, C3, D1 + I1, I4 + I5 necklace, E2, G1, H1 side blade, K1 | DFU | Visayan (elite) | Wasay only | VIS-13 |
| VIS-15 | B4, C3, D1 + I1, I4 + I5, E3 waist cloth, H2 shoulder cloth, G1, K1 | DFU | Visayan (datu/leader) | Any | VIS-13 |
| VIS-16 | B1, C1 (cream), D1 + I1, E1, G2, K1 | DFU | Visayan | Any | VIS-01 |
| VIS-17 | B3, C1 (indigo), D1, E1, G3, K2 | PR | Visayan | Any | VIS-07 |
| VIS-18 | B1, C5, D4 + I4, E1, G1, K1 | DFU | Visayan (prosperous-freeman) | Any | VIS-08 |
| VIS-19 | B4, C1 (cream), F2 over D1, E1, G2, K4 | DFU | Visayan | Any | VIS-11 |
| VIS-20 | B1, C4 woven sun hat, D1, E1, G2, K2 | PR | Visayan (levy) | Any | VIS-01 |

#### Tagalog block (15 presets, scope tag: Tagalog)

| ID | Component recipe | Tier | Scope | Loadouts | Fallback |
| --- | --- | --- | --- | --- | --- |
| TAG-01 | B1, C5, D2 chinina (indigo), E1, G2, K1 | DFU | Tagalog | Any | LEV-01 |
| TAG-02 | B4, C1 putong (cream), D2 (indigo), E1, G2, K1 | DFU | Tagalog | Any | TAG-01 |
| TAG-03 | B1, C5, D2 (blue-black), E1, G3, K2 | PR | Tagalog | Any | TAG-01 |
| TAG-04 | B3, C5, D2 (blue-black), E1, G2, K1 | DFU | Tagalog | Any | TAG-01 |
| TAG-05 | B1, C5, D1 bare chest, E1, G2, K2 | DFU | Tagalog | Any | TAG-01 |
| TAG-06 | B4, C1, D1, E1, G2, K1 | DFU | Tagalog | Any | TAG-05 |
| TAG-07 | B4, C1 (indigo), D2 (indigo), E1, G3, K4 | PR | Tagalog | Any | TAG-02 |
| TAG-08 | B1, C5, D2 (indigo), E1, H1 side blade, G2, K2 | DFU | Tagalog | Wasay only | TAG-01 |
| TAG-09 | B3, C5, D1, E1, G3, K4 | PR | Tagalog | Any | TAG-05 |
| TAG-10 | B1, C5, F3 hide corselet over D1, E1, G2, K2 | DFU | Tagalog (veteran) | Any | TAG-05 |
| TAG-11 | B4, C1, F3 over D1, E1, G2, K5 | DFU | Tagalog (veteran) | Any | TAG-10 |
| TAG-12 | B1, C5 + F5 shell-set helmet cap, F3 over D1, E1, G2, K2 | DFU | Tagalog (rarity) | Any | TAG-10 |
| TAG-13 | B4, C3 gold-edged putong, D3 red chinina, E2 gold-edged bahag, I4 + I5, G2, K1 | DFU | Tagalog (chief) | Any | TAG-02 |
| TAG-14 | B1, C1 (cream), D2 (indigo), E1, I4, G2, K1 | DFU | Tagalog (prosperous-freeman) | Any | TAG-02 |
| TAG-15 | B4, C3, D2 (indigo), E3 waist cloth, H2 shoulder cloth, I4 + I5, G2, K1 | DFU | Tagalog (leader) | Any | TAG-13 |

#### Northern Luzon block (8 presets, scope tag: Cagayan)

No tattoo tone, no putong, no gold ensemble — the block follows the Boxer
Codex Cagayan and Zambal silhouettes only.

| ID | Component recipe | Tier | Scope | Loadouts | Fallback |
| --- | --- | --- | --- | --- | --- |
| LUZ-01 | B2 loose hair, C5, D1, E1, G2, K1 | DFU | Cagayan | Any | LEV-01 |
| LUZ-02 | B2, C6 feathered headdress, D1, E1, G2, K1 | DFU | Cagayan | Any | LUZ-01 |
| LUZ-03 | B2, C5, D1, E1, G3, K2 | PR | Cagayan | Any | LUZ-01 |
| LUZ-04 | B2, C6, D1, E1, G3, K5 | PR | Cagayan | Any | LUZ-02 |
| LUZ-05 | B1 knot, C5, D1, E1, G2, K2 | DFU | Cagayan (Zambal-referenced) | Any | LUZ-01 |
| LUZ-06 | B3 cropped, C5, D1, E1, G3, K1 | PR | Cagayan | Any | LUZ-01 |
| LUZ-07 | B2, C5, F4 wooden breastplate over D1, E1, G2, K2 | DFU | Cagayan (rare veteran) | Any | LUZ-01 |
| LUZ-08 | B2, C6, D1, E1, H1 side blade, G2, K2 | DFU | Cagayan | Wasay only | LUZ-02 |

#### Generic levy block (10 presets, scope tag: Unscoped-generic)

Minimal kit, undyed cloth only, no tattoos, no gold, no putong, no regional
markers of any kind. All rows are D1 bare chest, E1 cream bahag.

| ID | Component recipe | Tier | Scope | Loadouts | Fallback |
| --- | --- | --- | --- | --- | --- |
| LEV-01 | B1, C5, G2, K1 | DFU | Unscoped-generic | Any | Model-category default, then diagnostic placeholder |
| LEV-02 | B3, C5, G3, K1 | PR | Unscoped-generic | Any | LEV-01 |
| LEV-03 | B1, C4 sun hat, G3, K2 | PR | Unscoped-generic | Any | LEV-01 |
| LEV-04 | B3, C4, G2, K2 | PR | Unscoped-generic | Any | LEV-01 |
| LEV-05 | B1, C5, G3, H1, K2 | PR | Unscoped-generic | Wasay only | LEV-01 |
| LEV-06 | B3, C5, G2, H1, K3 | DFU | Unscoped-generic | Wasay only | LEV-01 |
| LEV-07 | B1, C4, G2, H1, K4 | PR | Unscoped-generic | Wasay only | LEV-01 |
| LEV-08 | B3, C4, G3, H1, K1 | PR | Unscoped-generic | Wasay only | LEV-01 |
| LEV-09 | B3, C5, G2, K5 | DFU | Unscoped-generic | Any | LEV-01 |
| LEV-10 | B1, C4, G3, H1, K3 | PR | Unscoped-generic | Wasay only | LEV-01 |

Total: 53 shipped presets. Every recipe is a reviewable historical claim; the
combination validator and the pairwise differentiation test run over exactly
this list, and the historical review of the list against the research is a
human task, not a test (R-W3 acceptance note).

### Regional grouping and the prohibitions

The block structure is itself the enforcement mechanism for the research's
ten prohibited combinations, and the validator re-checks each mechanically:

1. C6 appears only in the LUZ block (prohibition 1).
2. I1/I2 appear only in the VIS block, always with D1 or as arms-only under
   F2 (prohibition 2; co-occurrence rule 1).
3. The red putong (C2) is absent from the roster entirely (see Open
   decisions); the red chinina (D3) appears only on TAG-13. The two red
   status systems never meet (prohibition 3).
4. No brass/bronze armor, mail, or greaves exist as components
   (prohibition 4); the armor layer is exactly F1–F5.
5. The *salakot* label does not exist; C4 appears only on levy-flavored
   presets, never elite (prohibition 5).
6. Gold components (C3, I4, I5, E2's gold pixel) appear only on presets whose
   scope column marks elite, chief, leader, or (single-accent I4 only)
   prosperous-freeman rows; never in the LEV block (prohibition 6;
   co-occurrence rule 3 — gold clusters).
7. Tattooing is a tone shift only; no motif geometry exists in any render
   channel (prohibition 7).
8. No European component and no footwear component exist in the catalog
   (prohibition 8); every pawn is barefoot by construction.
9. No later Moro kit exists in the catalog (prohibition 9).
10. Every preset carries a scope tag shown in the inspector, and block
    assignment gives each faction one region per match (prohibition 10).

The stratification rule is honored structurally: elite presets are the common
base plus documented wealth markers (denser gold and dye), never a distinct
uniform; the levy block expresses low status by absence only — there is no
invented slave costume, and no preset claims to depict bonded status.

### Inspector surface

Selecting a pawn shows, in the agent inspector: the preset display name (a
plain-English descriptor; pair-form labels appear only on the three cleared
component names), the scope tag, the preset evidence tier, the per-component
tier list with "must not generalize" notes, pending-verification flags on any
inspector-only terms, and the non-renderable flavor lines (betel pouch, tooth
goldwork) where a recipe includes them. This is the sanctioned
spectator-discoverability channel: the variation is visible on the pawn, the
meaning is one click away, and nothing is inspector-invisible.

## Rejected approaches

- **Sprite-based variant art** — no pipeline exists, regression unmeasurable,
  contradicts OD-4. Re-entry criterion: a future art-direction decision to
  adopt textures wholesale, with the render measurement harness in place
  first.
- **Pure palette-swap variation** — fails the differentiation requirement and
  produces no historical review surface. No re-entry criterion; it is simply
  insufficient.
- **Unconstrained per-category random rolls** — cannot honor the prohibitions
  without generating-then-filtering, and yields an unreviewable space.
  Re-entry criterion: none for player-facing content; a constrained generator
  MAY later be used as an authoring aid whose output is still committed and
  reviewed as a static roster.
- **Per-pawn persistent appearance state** (wear accumulating over a match) —
  rejected for this pass; appearance stays a pure function. The condition
  category (K) fakes wear statically instead. Re-entry criterion: the OD-5
  decision, which would introduce the first bounded presentation-state
  appearance input.

## Dependencies

- `visual-system-integration-design.md` (authored in parallel): the catalog
  infrastructure and stable-identifier contract (R-W6.1), the salt-registry
  convention (R-W6.2), the fallback-resolution chain and diagnostic
  placeholder (R-W6.4), the missing-asset diagnostics on the `assets` channel
  (R-W6.5), and the render measurement harness (R-W6.12) that turns this
  design's cost assumptions into numbers.
- `docs/research/improve-visuals/warrior-appearance-historical-research.md` —
  the evidence base; any component this design uses beyond it requires new
  research, not invention.
- `docs/agents/improve-visuals/requirements.md` — the binding requirement
  set.
- Existing code: `PawnAppearanceFactory`, `PawnAppearance`, `PawnGeometry`,
  `PawnRenderer`, and their tests, which define the invariants extended here.

## Risks

- **Visual noise at scale.** Fifty-three presets under 200+ pawns could read
  as clutter. Mitigations: the saturation and area caps, Low-tier tone-only
  gating, and the manual checklist row that only a human can pass.
- **Historical review burden.** Fifty-three rows each carrying claims is a
  real review cost; the weakest-link tier rule and per-row recipes exist to
  make that review line-by-line rather than holistic.
- **Salt collisions or reuse.** Reusing an existing salt would silently
  reshuffle current appearance; mitigated by the salt registry in the
  integration design and a uniqueness test.
- **Prohibition drift.** A future preset added without the validator would
  reopen every mashup hazard; mitigated by the validator running over the
  entire shipped roster in the gate, with at least one negative test per
  prohibition.
- **Loadout-filtered pools shrinking.** The Wasay-only H1 presets thin the
  pool for other weapons; the selection design must prove (by test) that
  every (block, loadout) pair resolves at least one valid preset, or the
  fallback chain covers it.

## Open decisions

- **OD-5 — Earned red putong (C2). Resolved 2026-07-28:** C2 stays excluded
  from this pass. The research documents the red head wrap as an earned
  Visayan insignia that must never be a random roll, and `AgentView` carries
  no kill or veteran data; presenting it would require bounded, client-only
  presentation-state kill tracking fed by `Death` events, which this design
  deliberately does not design. The earned-insignia display is recorded as a
  backlog item in `docs/plans/TODO.md`. If it is ever approved, a reserved
  preset (VIS-R1: VIS-05 with the wrap in sappan red) is the intended shape.
- **OD-1/OD-2 — pending term promotions** (*kalasag*, *palisay*, and the
  inspector-metadata terms): inherited from the requirements; resolved
  2026-07-28 — plain-English labels ship this pass and the pending names
  appear only as flagged inspector metadata, exactly as this design already
  ships.
- **OD-3 — Mindanao/Sulu gap. Resolved 2026-07-28:** the Unscoped-generic
  levy block is accepted as the sole Mindanao/Sulu coverage this pass; this
  roster deliberately contains no Mindanao- or Sulu-flavored presets, per
  the research's scope, and no further research is commissioned now.
- **OD-W3-a — Block-assignment table breadth:** whether both factions in a match may
  draw the same regional block (visually plausible, historically common) or
  must draw distinct blocks (better faction separation). Recommended default:
  same block allowed, since faction identity rests on the ring, not the
  costume — but this is a product-feel call for review.

## Acceptance criteria

Automated (GPU-independent xunit, per the pure-helper pattern):

- Combination validator passes for all 53 presets; at least one negative test
  per prohibition (a deliberately illegal recipe fails).
- Pairwise differentiation test: every same-block preset pair satisfies the
  minimum differentiation criterion as defined above (the criterion is scoped
  within each regional block).
- Preset count test: at least fifty valid presets.
- Selection stability: same `EntityId` + loadout + scenario seed yields the
  same preset on every call; different salts yield streams independent of the
  existing appearance salts (existing outputs unchanged, pinned).
- Pool totality: every (block, loadout) combination resolves at least one
  preset or a defined fallback.
- Palette pins: the ten dye constants match the research values; minimum
  color distance from faction constants holds.
- Tier gating: silhouette components absent below 0.95 apparent scale;
  accents absent below 1.80; thresholds tested at exact values.
- Metadata presence: every preset and component carries an evidence tier,
  scope tag, and (where applicable) must-not-generalize note; pending terms
  are flagged; forbidden terms are absent from all player-facing strings.
- Armor widening stays within the build-multiplier envelope.

Manual checklist rows (added to `docs/development/testing.md` as `PENDING`;
only a human at an interactive desktop may flip them):

- Fifty-plus presets read as varied but coherent at normal zoom.
- No preset reads as a different faction or as different equipment.
- Elite figures read as denser in gold and dye, not larger.
- At minimum zoom, faction and weapon role remain the dominant reads.

Human review task (not a test): line-by-line historical review of the roster
table against the research document.

This document does not authorize implementation. Implementation authority
for the 23 milestone tasks comes from the user's dated approval of
2026-07-28, recorded in the package README.
