# Shield Visuals — Design (Visual Improvement Pass, Workstream 2)

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

Design document. Date: 2026-07-28. This document proposes the
presentation-only skin set and posture treatment for the implemented shield
(`ShieldId.TallHardwood`; `ShieldId.None` renders nothing and stays that way).
It consumes `docs/research/improve-visuals/weapons-shields-historical-research.md`
(the twelve-entry shield evidence catalog),
`docs/agents/improve-visuals/requirements.md` (workstream 2 and the
cross-cutting R-X requirements), and
`docs/agents/improve-visuals/existing-code-analysis.md` (the current rendering
stack). It is bound by the historical accuracy policy in `CLAUDE.md` section 7.

This document depends on `visual-system-integration-design.md` (authored in
parallel in this same directory) for the skin catalog structure, the stable
identifier contract, the salted selection stream, the fallback-resolution
function, and the missing-skin diagnostics. Where this document names a
catalog entry, an identifier, or a fallback step, the shape of that machinery
is defined there, not here.

Per the workflow in `CLAUDE.md` section 6, a design document authorizes
nothing; the plan document follows it.

On 2026-07-28 the user resolved all ten package open decisions — including
OD-10 (option (a): R-W2.1 amended, the S2/S5 proportion deltas kept), OD-1
(plain label ships this pass), and OD-2 (palisay as flagged inspector
metadata only) — and approved the 23-task first milestone for
implementation. The decision record is in the package README.

## Scope

In scope:

- Presentation-only skins for `ShieldId.TallHardwood`, limited to the four
  research-cleared tall-shield anchors: S1 (Mactan thin-wood shield, 1521),
  S2 (Morga full-body shield, 1609), S3 (Boxer Codex Cagayan shield,
  c. 1590–1595), S5 (Visayan kalasag form, Alcina 1668 / Scott 1994).
- The active-posture treatment from S12 (Hinilawod striking use; Cole's
  tilting grip): drawing the tall shield slightly angled forward of the pawn
  rather than as a passive side slab.
- A status catalog of all twelve researched shield entries, so the plan and
  every later reader can see at a glance what is a skin now, what would be a
  future mechanical shield, and what is excluded on regional grounds.
- Evidence-tier metadata, source anchors, inspector notes, the fallback
  chain, and the manual readability rows.

Out of scope, explicitly:

- **A visual skin is not a mechanical shield.** No new `ShieldId` values, no
  renumbering or reordering, no coverage, protection, or targeting change of
  any kind (R-X.11, R-X.12). A new mechanical shield (breast-high, buckler,
  or anything else) is a separate gameplay design that this pass does not
  authorize and that this document does not start.
- Breast-high, round, buckler, pronged, or tufted shield shapes on any pawn
  (R-W2.4). In particular, a visibly smaller shield skin on a `TallHardwood`
  loadout is forbidden because the simulation would still apply the tall
  shield's full chest-and-abdomen multiplier — the spectator would be shown a
  false cause.
- Any shield-strike mechanic (the S12 posture is drawing, not behavior).
- Sprites, textures, content-pipeline changes, new packages (R-X.15,
  R-W6.18).

## Current state

From the existing-code analysis, verified against source on 2026-07-28:

- The shield is a solid charred-wood-colored block drawn beside and
  deliberately after the torso so it overlaps it, with a lighter vertical
  seam at Medium/High detail (`PawnRenderer.DrawShield`; layout and
  rationale in `PawnGeometry`). It is drawn at **every** detail tier because
  shielded-versus-solo is a spectator-critical distinction — the existing
  rule R-W2.2 preserves.
- Shield presence derives from `CombatLoadout.Shield` only, never from
  `EntityId`; `PawnAppearanceFactoryTests` pins this (shield-from-loadout
  tests).
- `ShieldId` carries explicit pinned numeric values (`None=1,
  TallHardwood=2`) with do-not-renumber doc comments; they are part of the
  deterministic replay contract and are untouched by this design.
- `PawnRenderer.GetBounds` is pose-blind by contract; any layout change must
  keep the drawn-pawn set independent of animation state (R-X.5).
- There is exactly one shield appearance today — no skin concept, no angled
  posture, no shield metadata in the inspector beyond the loadout.

## Evidence

The research's practical conclusion, adopted without expansion: the existing
`TallHardwood` shield has **four honest skins** — S1, S2, S3, S5 — that
differ only in presentation-level detail (face tone, rattan-binding accents,
slight outline curvature), plus **one posture recommendation** (S12).
Everything round or breast-high is a future-mechanics question; everything
Cordilleran or Bagobo is a future-regional-identity question. Key points per
anchor:

- **S1 — Mactan thin-wood shield (1521).** Documented (existence, thinness,
  active use with evasive footwork); Documented, form uncertain (shape).
  Pigafetta describes *thin* wood — the enum name "hardwood" slightly
  overstates this, and the inspector may honestly note it (R-W2.7).
- **S2 — Morga full-body shield (1609).** Documented, form uncertain. Light
  wood, head-to-foot coverage, inside armhole fastening. The "top to toe"
  quotation reached the research through secondary transmission and must be
  verified against Blair & Robertson before any player-facing quotation.
- **S3 — Boxer Codex Cagayan shield (c. 1590–1595).** Documented as a
  late-century visual depiction; form uncertain as construction evidence.
  Tall, gently curved rectangular silhouette; the Codex guides silhouette and
  color only. This image is already the named inspiration for the existing
  tall shield.
- **S5 — Visayan kalasag form (Alcina 1668; Scott 1994).** Documented, form
  uncertain. Long narrow body shield, roughly three times taller than wide
  (proportion, never a measurement — the "50 × 150 cm" figure is an
  untraceable citation chain and is not repeated as fact); light fibrous
  wood, rattan strengthening, resin coating. The *kalasag* name is a
  provisional attachment pending vocabulary verification.
- **S12 — active posture (Hinilawod epic; Cole 1922; Warming comparative).**
  Provisional reconstruction for 1500s practice; the strongest available
  justification for drawing the shield angled forward rather than as a
  static wall. A posture reference, not a silhouette and not a mechanic.

## Requirements

This design implements workstream 2 of the requirements document. Binding
requirements, restated by ID (full text in
`docs/agents/improve-visuals/requirements.md`):

- **R-W2.1** — skins limited to S1, S2, S3, S5, expressed only as face tone,
  a rattan-binding accent line, and slight outline curvature; all four read
  as "tall body shield" at every tier.
- **R-W2.2** — shield presence drawn at every detail tier; no skin reduces
  the block's footprint below current Low-tier legibility.
- **R-W2.3** — skin selection is a pure salted function of `EntityId`;
  shield presence itself comes only from `CombatLoadout.Shield`.
- **R-W2.4 (MUST NOT)** — no breast-high, round, buckler, pronged, or tufted
  shapes; no Bagobo hair tufts; no Cordilleran prongs, on any pawn.
- **R-W2.5 (SHOULD)** — the angled-forward posture, as a fixed layout offset
  in `PawnGeometry`, marked `PROVISIONAL`, bounds-neutral per R-X.5.
- **R-W2.6** — the player-facing label stays `Tall Hardwood Shield`; the
  pair form `Kalasag — Tall Hardwood Shield` is gated on OD-1 and does not
  ship before the name verification succeeds.
- **R-W2.7** — every skin records evidence tier and source anchor in
  inspector metadata; the thin-wood note is permitted.
- **R-W2.8** — skin tones respect the R-W1.7 contrast-envelope discipline so
  the shield never merges with the torso or the ground.
- Cross-cutting: R-X.1–R-X.5 (readability priority, tier purity, bounds),
  R-X.6/R-X.7/R-X.10 (labels, tiers, inspiration tags), R-X.11/R-X.12/
  R-X.15/R-X.16 (prohibited scope).

## Alternatives considered

1. **No skins — keep the single charred-wood block.** Zero risk, zero
   spectator value; leaves the best-documented visual material in the
   research (four period-anchored tall-shield descriptions) unused. Rejected.
2. **Skins expressing different shield *shapes* (breast-high, round,
   pronged).** Maximum variety, but every one of them either implies a
   coverage profile the simulation does not have (S4, S6, S7 — the
   false-cause rule) or projects a named region's late-documented form onto
   a generic roster (S8–S11). Rejected; R-W2.4 forbids it.
3. **One "kalasag" hero skin with carved-face decoration.** The later record
   describes face carving, but fine decoration is illegible at battlefield
   scale and the carving descriptions are 1668-and-later; a decorated
   default would also front-run the OD-1 naming decision. Rejected in favor
   of a restrained rattan-accent treatment on one skin.
4. **The recommended approach below** — four presentation-level skins mapped
   one-to-one onto the research's cleared anchors, plus the angled posture,
   with all twelve research entries cataloged and status-flagged so nothing
   is silently dropped.

## Recommended approach

### Skin catalog for `ShieldId.TallHardwood`

Four skins, selected per pawn by one new salted SplitMix64-finalizer stream
of `EntityId` modulo 4 (R-W2.3; salt is new, never reusing the appearance or
plains salts, per the integration design's recipe). Identifiers follow the
stable-ID contract of `visual-system-integration-design.md`.

**Battlefield-scale rule, binding on all four:** a skin is recognizable by
*silhouette and tone*, never by fine decoration. At battle scale a spectator
can distinguish a tall block, its tone, one accent line, and slight outline
curvature — nothing finer. Carving, resin sheen, and construction detail are
inspector text, not pixels.

| ID (proposed) | Anchor | Tier | Inspiration tag | Visual difference (the only differences) |
| --- | --- | --- | --- | --- |
| `shield.tallHardwood.mactanThin` | S1 | Documented (existence, thinness, active use); Documented, form uncertain (shape) | `Mactan — 1521` | Lightest face tone of the four (pale palm-wood range); straight rectangular outline; no accent line |
| `shield.tallHardwood.morgaFullBody` | S2 | Documented, form uncertain | `Manila — 1609` | Mid light-wood tone; straight outline; proportion at the tall end of the shared envelope (see below) |
| `shield.tallHardwood.boxerCagayan` | S3 | Documented (depiction); Documented, form uncertain (construction) | `Manila — c.1590` | Existing charred-wood tone; **slight outline curvature** — the top and bottom edges inset by one to two layout pixels so the long edges bow gently, reading as the Codex's gentle curve |
| `shield.tallHardwood.visayanKalasag` | S5 | Documented, form uncertain (form); name attachment provisional | `Visayas — 16th c. (synthesis)` | Resin-brown face tone; **one horizontal rattan-binding accent line** across the face at Medium/High tier (replacing the vertical seam on this skin); narrowest proportion within the shared envelope |
| `shield.tallHardwood.default` | — | presentation-only marker on the entry itself | — | The current block exactly as drawn today; the fallback target, not a fifth rolled skin |

Proportion envelope: all four skins share one aspect-ratio band around the
current block's proportions, tight enough that a classification test can
assert "tall body shield" for every skin at every tier (the automated
acceptance criterion below), and tight enough that no skin reads as a
different equipment class. Within the band, per-skin width/height deltas are
a few layout pixels at most. No skin's footprint ever drops below the current
Low-tier footprint (R-W2.2).

**Resolution of the disclosed deviation from R-W2.1 (OD-10, resolved
2026-07-28, option (a)).** The per-skin proportion deltas in the table above
(`morgaFullBody` at "the tall end of the shared envelope", `visayanKalasag`
at the "narrowest proportion within the shared envelope") exceeded the three
difference channels the original R-W2.1 authorized — face tone, a
rattan-binding accent line, and slight outline curvature. This document
surfaced that deviation explicitly as package-level open decision OD-10
rather than shipping it silently. On 2026-07-28 the user resolved OD-10 to
option (a): R-W2.1 is amended with a fourth authorized channel — bounded
per-skin proportion deltas of a few layout pixels inside one shared
aspect-ratio band, with the rendered footprint never falling below the
current Low-tier block — and the S2/S5 deltas are kept. The reviewer's
hazard note is binding under this outcome: proportion variation is the
channel closest to the false-cause rule R-X.12 guards — a "narrowest" skin
must never read as less mechanical coverage than any other skin on the same
`ShieldId.TallHardwood` loadout — so the amendment is guarded by the manual
false-cause check row "skins read as variation, not as different equipment",
and a failure there drops the deltas before it drops any skin.

Tone constants are named `PROVISIONAL` client constants inside the documented
material palette (palm wood, hardwood, resin brown, charred wood), gated by
the same contrast-envelope tests as the weapon tints (R-W2.8): every face
tone stays distinct from all torso clothing colors and all ground shades in
all five themes, at every tier.

### Geometry per detail tier

- **Low (apparent scale < 0.95):** the solid block, in the skin's face tone,
  at the skin's outline (curvature included — it is a silhouette feature and
  survives all tiers; at this scale it degenerates gracefully toward the
  straight block). No seam, no accent. Shielded-versus-solo classification
  is the protected read (R-X.1, R-X.2).
- **Medium (0.95–1.80):** the block plus one detail element: the lighter
  vertical seam (S1, S2, S3, as today) or the horizontal rattan accent line
  (S5).
- **High (≥ 1.80):** Medium plus a one-pixel edge-tone step on the long
  edges (a slightly darker rim suggesting thickness), tone-only.

### Active posture (S12)

The shield is drawn slightly angled forward of the pawn instead of as a
passive side slab (R-W2.5):

- Implemented as a **fixed layout offset and small fixed rotation** in
  `PawnGeometry` — constants marked `PROVISIONAL` (they are a drawing choice
  justified by S12's posture evidence, not a historical measurement).
- Identical for all four skins and constant over time: the posture is not
  animated, does not react to combat state, and adds no pose channel. It is
  part of the static layout, so `PawnRenderer.GetBounds` stays pose-blind
  and the drawn-pawn set stays independent of animation state (R-X.5). The
  bounds computation accounts for the fixed offset once, statically.
- Grip implication, drawn honestly small: the angled position implies the
  active tilting grip Cole describes; at pawn scale this is conveyed
  entirely by the angle. No hand, strap, or grip detail is drawn; Cole's
  three-finger grip and the Morga armhole fastening are inspector text on
  the relevant skins.
- The posture never occludes the faction ground ring, the weapon line, or
  the head at Low tier (R-X.1); the layout keeps the current draw order
  (shield after torso, before head).

### Full status catalog of the twelve researched entries

The catalog below is normative for this pass: entries marked *skin-now* are
the four skins above; everything else is recorded with the reason it is not
a skin, so no later reader mistakes silence for oversight. "Future
mechanical" is a flag only — **no mechanical addition is authorized by this
document or this pass.**

| Entry | Status this pass | Reason |
| --- | --- | --- |
| S1 Mactan thin-wood shield (1521) | **Skin-now** (`mactanThin`) | Cleared tall-shield anchor (R-W2.1) |
| S2 Morga full-body shield (1609) | **Skin-now** (`morgaFullBody`) | Cleared anchor; best textual fit for the tall-shield identity |
| S3 Boxer Codex Cagayan shield (c. 1590–1595) | **Skin-now** (`boxerCagayan`) | Cleared anchor; the existing shield's named inspiration |
| S4 Narrow breast-high shield (1565–1576) | **Future-mechanical-only; excluded as a skin** | Distinct coverage profile would need its own `ShieldId` and targeting multipliers; drawing it on a `TallHardwood` loadout shows a false cause (R-X.12, R-W2.4) |
| S5 Visayan kalasag form (Alcina/Scott) | **Skin-now** (`visayanKalasag`) | Cleared anchor; best label-upgrade candidate, gated on OD-1 |
| S6 Tagalog palisay round buckler | **Future-mechanical-only; excluded as a skin** | Buckler coverage is a different defensive object (R-X.12); name attestation pending (OD-2) |
| S7 Taming round buckler | **Future-mechanical-only; excluded as a skin** | As S6; the *taming* name's entry date is unestablished — treated as excluded entirely from any player-facing use (R-X.6) |
| S8 Kalinga/Tinguian pronged shield | **Region-locked excluded** | Coverage-compatible but a specific highland-Luzon form tied to a specific practice; generalizing it is the pan-archipelagic-warrior error. Cole's grip/posture description is borrowed as stance inspiration only (feeds S12 treatment) |
| S9 Bontoc blunted-prong shield | **Region-locked excluded** | As S8 |
| S10 Cordilleran variant forms | **Region-locked excluded** | As S8 |
| S11 Bagobo round/oblong tufted shield | **Region-locked excluded** (oblong); round form falls with S6/S7 | Named Mindanao group, late record; hair tufts are culturally specific decoration and appear on no pawn (R-W2.4) |
| S12 Hinilawod offensive posture | **Adopted as posture reference** | Not a silhouette; justifies the angled-forward drawing of the existing shield (R-W2.5) |
| `ShieldId.None` | Unchanged | Absence of equipment; renders nothing |

Region-locked entries are recorded for a possible future campaign layer in
which factions carry real regional identity; they must not resurface as
generic skins.

### Inspector metadata and label handling

- The player-facing shield label stays **`Tall Hardwood Shield`** — the
  plain English descriptor, with no Filipino name attached. This is
  deliberate, and is now decided: per OD-1, resolved 2026-07-28, the plain
  label ships this pass, and the pair-form promotion waits for the
  attestation verification, which remains unscheduled. *Kalasag* stays
  marked **PENDING attestation**, and the hundred-year rule forbids shipping
  an unverified name in pair form. *Palisay* is likewise PENDING and its
  object is not in the game at all; per OD-2, resolved 2026-07-28, it may
  appear in inspector research notes as metadata only, explicitly flagged
  attestation-pending. The inspector uses plain English for the shield; the
  pending names appear only in inspector research notes explicitly flagged
  as pending verification (R-X.6, R-W2.6).
- Each skin's inspector entry shows: the label, the skin's source anchor and
  inspiration tag (`Mactan — 1521`, `Manila — c.1590`, ...), its evidence
  tier, and its note. The `mactanThin` skin's note may state that Pigafetta
  describes thin wood where the internal identifier says hardwood (R-W2.7).
  The `morgaFullBody` note must not quote the "top to toe" passage until it
  is verified against Blair & Robertson (research flag).
- The `visayanKalasag` skin's note discloses that the kalasag name is a
  provisional attachment pending verification — the skin ships under the
  plain label like the others.

### Fallback chain

Every shield visual lookup resolves through the chain defined in
`visual-system-integration-design.md` (R-W6.4):

1. **Specific skin** — the entry the salted stream selected, e.g.
   `shield.tallHardwood.visayanKalasag`.
2. **Shield default** — `shield.tallHardwood.default`, the current block
   exactly as drawn today.
3. **Model-category default** — the generic "tall shield block" drawable
   (the default block with the neutral charred-wood tone), which for this
   single-shield roster coincides with step 2 in effect but remains a
   distinct, testable chain step so the chain shape matches the weapon and
   component chains.
4. **Diagnostic placeholder** — the conspicuous primitive from the
   integration design, plus a once-per-identifier `warn` on the `assets`
   channel via the new `LogEvents` constants (R-W6.5).

`ShieldId.None` never enters the chain — absence of equipment resolves to
"draw nothing" before resolution starts, as today. Resolution is total for
every valid `(EntityId, CombatLoadout)`.

### Readability confirmation

Manual rows added to `docs/development/testing.md`, created `PENDING`,
flippable only by a human at an interactive desktop:

- At **minimum zoom** (0.05x, Low tier) with 200+ pawns: shielded versus
  unshielded pawns remain distinguishable for every skin; skin differences
  are invisible or sub-threshold.
- At **normal zoom** (initial fit, typically Medium tier): the four skins
  read as variation of one shield, not as different equipment; the S5 rattan
  accent reads as binding, not damage.
- At **maximum zoom** (12x, High tier): face tones, curvature, and edge
  steps visible; the angled posture reads as an active stance, not as a
  layout bug.
- High-contrast theme: the shield block remains unambiguous against torso
  and ground for all four skins.

No automated test claims to prove these rows.

## Rejected approaches

- Any skin whose silhouette departs from the tall body shield: breast-high
  (S4), round bucklers (S6, S7), pronged (S8–S10), tufted (S11) — each
  either a future-mechanics question or a regional-identity question, and
  all forbidden on pawns by R-W2.4.
- A visibly smaller or shorter shield skin on a `TallHardwood` loadout —
  the false-cause rule.
- Carved-face or decorated hero skins (alternative 3) — illegible at scale,
  later-record decoration, and front-runs OD-1.
- Shipping the `Kalasag — Tall Hardwood Shield` pair label now — the
  attestation is unverified; the hundred-year rule gates it (OD-1).
- Animating the shield posture or adding shield-strike behavior — S12 is a
  drawing reference; behavior would be presentation-state at best and
  mechanics at worst, neither in scope.
- Hair tufts, prongs, or any culture-diagnostic decoration on generic
  pawns — the anti-generalization rules bind absolutely.

## Dependencies

- **`visual-system-integration-design.md`** (parallel, same directory):
  catalog record shape, stable-ID contract and pin tests, salted stream
  recipe, fallback-resolution function, diagnostic placeholder, `LogEvents`
  additions.
- The weapon design (`weapon-visuals-design.md`, this directory) shares the
  contrast-envelope constants and the tier-threshold test approach; the two
  documents deliberately use the same chain shape and the same manual-row
  style.
- Existing code this design extends: `PawnGeometry` (shield layout, the new
  `PROVISIONAL` posture constants), `PawnRenderer.DrawShield` (draw),
  `PawnAppearanceFactory` (selection precedent; shield-from-loadout-only
  invariant, extended not weakened), `PawnAppearanceFactoryTests` /
  `PawnGeometryTests`.
- Requirements: workstream 2 (R-W2.1–R-W2.8), cross-cutting R-X.*, and the
  workstream 6 infrastructure requirements (R-W6.1–R-W6.5, R-W6.16,
  R-W6.17).
- Open decision OD-1 (kalasag attestation) was an upstream research/user
  decision this design is written to be correct under either outcome of;
  resolved 2026-07-28 — the plain descriptor ships and the verification
  remains unscheduled.
- The canonical gate `./scripts/verify.ps1` after integration; seed-1 hashes
  untouched by design (pure presentation).

## Risks

1. **False-cause perception despite compliance.** Even within one proportion
   envelope, a spectator might read the lighter `mactanThin` tone as a
   "weaker" shield. Mitigated by the tight envelope, the identical footprint
   rule, and the manual row "skins read as variation, not different
   equipment"; if that row fails, tones converge before shapes change.
2. **Posture offset breaking bounds or overlap assumptions.** The angled
   offset shifts the block relative to torso and head. Mitigated by keeping
   it a fixed static layout value, bounds-accounted once, pinned by
   geometry tests for offset and bounds-neutrality.
3. **Curvature illegibility.** The Boxer Codex curve may vanish at small
   scales and read as a rendering artifact at large ones. Mitigated by
   keeping it one to two layout pixels and letting it degrade to the
   straight block at Low tier; if the manual rows report artifacting, the
   curvature drops before the skin does.
4. **Name pressure.** The kalasag name is well known and its absence will
   look like an omission. The inspector's pending-verification note is the
   honest answer; shipping the name early would violate the policy this
   repository treats as load-bearing.
5. **Skin-count creep.** Four skins is the evidence's count. Adding a fifth
   would require new research, not new art.

## Open decisions

- **OD-1 (inherited) — Kalasag label promotion. Resolved 2026-07-28:** ship
  the plain descriptor `Tall Hardwood Shield` this pass. The pair-form
  promotion waits for the attestation verification, which remains
  unscheduled; a later positive verification upgrades the label and nothing
  else.
- **OD-2 (inherited) — Palisay name in inspector research notes. Resolved
  2026-07-28:** inspector-metadata only, flagged attestation-pending. No
  object, no skin.
- **OD-10 (package-level) — Per-skin proportion deltas versus R-W2.1.
  Resolved 2026-07-28, option (a):** R-W2.1 is amended to authorize a fourth
  channel — bounded per-skin proportion deltas of a few layout pixels inside
  one shared aspect-ratio band, footprint never below the current Low-tier
  block. The S2/S5 deltas are kept. The R-X.12 false-cause hazard stands and
  is guarded by the manual check row: no skin — in particular no "narrowest"
  skin — may read as less mechanical coverage than any other skin on the
  same loadout.
- **OD-W2-a — Proportion envelope values.** With OD-10 resolved to option
  (a), the aspect-ratio band and the per-skin deltas are finalized in the
  plan under the classification tests; this document fixes only that one
  shared band exists and that footprint never shrinks below today's
  Low-tier block.
- **OD-W2-b — Posture angle and offset values.** `PROVISIONAL` constants
  finalized in the plan; the requirement fixed here is bounds-neutrality
  and Low-tier non-occlusion, not the numbers.
- **OD-W2-c — Whether `boxerCagayan` keeps the vertical seam at Medium
  tier or trades it for the curvature alone.** Cosmetic; decided in the
  plan against the manual-row results.

## Acceptance criteria

Automated (GPU-independent xunit, per the established split):

- Skin-stability: same `EntityId` + `TallHardwood` loadout yields the same
  skin ID on every call, across frames and replays of the same seed.
- Shield-presence-from-loadout-only (existing tests, extended): no skin
  stream ever affects whether a shield is drawn; `ShieldId.None` draws
  nothing regardless of any stream.
- Classification: every skin's layout rectangle stays inside the shared
  tall-shield aspect-ratio band at every tier; every skin's footprint at Low
  tier is at least the current block's.
- Posture: the angled offset and rotation equal the named `PROVISIONAL`
  constants; `GetBounds` output is independent of animation phase and
  identical across all four skins.
- Tier-gating at the exact thresholds 0.95 and 1.80: seam/accent Medium+,
  edge step High, Low is block-plus-outline only.
- Contrast-envelope pins for all four face tones against torso and ground
  colors in all five themes.
- Evidence metadata presence: every skin entry carries a tier, source
  anchor, and note; the label constant is the plain descriptor and no
  player-facing string contains an unverified Filipino shield name
  (a negative test encoding R-X.6/R-W2.6).
- Fallback totality and per-step reachability for the shield chain.
- The canonical gate passes with the seed-1 state hash, event hash, outcome,
  and event stream unchanged.

Manual (rows in `docs/development/testing.md`, created `PENDING`):

- The four readability rows defined above (minimum / normal / maximum zoom,
  high-contrast theme).
- Inspector shows, for a selected shielded pawn, the plain shield label, the
  skin's anchor tag, tier, and note (including the pending-verification
  flags where applicable).
- Forced-failure run shows the diagnostic placeholder conspicuously (shared
  with the integration design's checklist).

This document does not authorize implementation. Implementation authority
for the 23 milestone tasks comes from the user's dated approval of
2026-07-28, recorded in the package README.
