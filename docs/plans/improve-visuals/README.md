# Visual Improvement Package — Plan Overview

Date: 2026-07-28. Status: **planning only — awaiting user approval. Nothing in
this package authorizes implementation.**

This directory holds the design documents for the Hukbo visual improvement
pass: better weapon and shield rendering, a warrior appearance preset system,
a living battlefield ground, presentation-only motion, and the rendering and
asset infrastructure that all of it stands on. Every document is bound by the
requirements document under `docs/agents/improve-visuals/` and by the
repository's non-negotiables (`CLAUDE.md` sections 5 through 7,
`SIMULATION-GAME-STANDARDS.md` sections 4 and 10).

## Document map

Seven documents make up the package. The integration design is the foundation
the other five build on; read it first.

| Document | Covers | Status |
| --- | --- | --- |
| `README.md` | This overview: map, boundaries, open decisions | Written |
| `visual-system-integration-design.md` | The shared infrastructure: rendering strategy (procedural versus atlas), visual catalogs, deterministic variant selection, fallback chain and diagnostics, pawn layer ordering, animation boundaries, zoom LOD, batching, settings, testing, and the performance measurement plan | Written |
| `weapon-visuals-design.md` | Presentation-only variants of the four weapons (workstream 1) | Written |
| `shield-visuals-design.md` | Skins and posture for the tall hardwood shield (workstream 2) | Written |
| `warrior-appearance-design.md` | The component system and the fifty-plus presets (workstream 3) | Written |
| `battlefield-environment-design.md` | Ground shading, grass clusters, trample, dust, and sway (workstreams 4 and 5) | Written |
| `implementation-plan-draft.md` | The ordered task list with files, verification, and dependencies, per `CLAUDE.md` section 6 | Follows the designs |

## Inputs the package was written from

Research documents (evidence base, read-only):

- `docs/research/improve-visuals/weapons-shields-historical-research.md`
- `docs/research/improve-visuals/warrior-appearance-historical-research.md`
- `docs/research/improve-visuals/battlefield-environment-research.md`
- `docs/research/HISTORICAL_1500s_WEAPONS.md` (the binding accuracy policy)

Agent working documents (requirements and code ground truth):

- `docs/agents/improve-visuals/requirements.md` — 88 numbered requirements
  across six workstreams plus cross-cutting rules; the authority every design
  document in this package answers to.
- `docs/agents/improve-visuals/existing-code-analysis.md` — the verified
  current state of the rendering, presentation, settings, theme, diagnostics,
  and test stack.

## Non-negotiable boundaries

These bind every document in this package and every task in the eventual plan.
They are restated here so no sibling document has to re-derive them.

1. **Nothing visual touches the simulation.** No appearance, variant, or
   environment state of any kind enters `Hukbo.Core`. The recorded seed-1
   reference pair (stateHash `27DC94C6E9A01E35`, eventHash
   `372C9217E5CB8BE9` — `docs/development/testing.md`, Phase 2 reference
   pair), the outcome, and the ordered event stream are identical before and
   after this work. Presentation reads outward only.
2. **No mechanical changes.** No new `WeaponId` or `ShieldId` values, no
   renumbering, no reordering, no visual that implies a mechanical difference
   that does not exist (the false-cause rule).
3. **Deterministic presentation randomness only.** `System.Random`,
   `GetHashCode`, iteration order, and the wall clock are banned as variation
   sources. Variation derives from `EntityId`, `CombatLoadout`, and
   `Scenario.Seed` through SplitMix64-style mixing with new named salts.
4. **Fully procedural rendering.** No textures, no sprite atlas, no content
   pipeline additions, no shaders, no new packages in this pass. Everything
   continues to draw from the single 1x1 white texture inside the existing
   arena sprite batch.
5. **Historical accuracy policy.** Cultural identifications appear only in
   pair form with a recorded evidence tier; names attested more than a century
   after the depicted period are not used; no region's costume is generalized
   to "the Philippines"; tuning values are marked `PROVISIONAL`.
6. **Bounded everything.** Every cap is a named constant with a test; no
   unbounded caches; per-frame paths allocate nothing in steady state.
7. **Testing honesty.** Drawing logic lives in tested pure geometry helpers;
   renderers stay untested draw-only sinks; Client tests never construct
   `ArenaGame`, a graphics device, a sprite batch, or a window. Interactive
   results are proven only by manual checklist rows in
   `docs/development/testing.md`, created `PENDING` and flipped only by a
   human.
8. **Local verification only.** `./scripts/verify.ps1` is the canonical gate;
   its real output is the evidence. No CI workflow.

## Independent review

The package has been through an independent review:
`docs/agents/improve-visuals/review-findings.md`. Its one Critical finding
and two High findings are resolved in this revision; the Medium findings are
resolved in this revision or converted into the new open decisions OD-9 and
OD-10 below.

## Open decisions requiring user approval

OD-1 through OD-8 are carried verbatim from
`docs/agents/improve-visuals/requirements.md`; OD-9 and OD-10 arose from the
independent review. The designs are written to safe defaults where one
exists; none of these is decided by this package.

1. **OD-1 — Kalasag label promotion.** Ship the shield as plain
   `Tall Hardwood Shield`, or commission the attestation verification that
   would allow `Kalasag — Tall Hardwood Shield`.
2. **OD-2 — Palisay.** Whether the pending buckler name may appear in
   inspector research notes (default: inspector metadata only, flagged
   pending).
3. **OD-3 — Mindanao/Sulu gap.** Accept the Unscoped-generic preset block as
   the only coverage, or commission further research first.
4. **OD-4 — Sprite versus procedural direction.** The research and the
   integration design recommend staying fully procedural; user confirmation
   closes the question for this pass.
5. **OD-5 — Earned red putong (C2).** Exclude the earned insignia, or approve
   bounded client-side kill tracking from `Death` events as new
   presentation-state.
6. **OD-6 — Default theme ground tint.** Whether to shift the default theme's
   ground toward cogon olive-gold (a separate theme-tuning review).
7. **OD-7 — Shape-redundant faction marker.** Whether to add a non-hue
   faction channel for color-blind spectators beyond the no-regression floor.
8. **OD-8 — Reduced-motion scope.** Whether the new setting governs only
   grass sway or is defined to cover future ambient motion as well.
9. **OD-9 — Dust scope conflict.** Requirement R-W4.8 marks dust puffs MUST,
   while the battlefield environment design scopes them MAY. The user must
   either approve downgrading R-W4.8 or confirm dust as mandatory, which
   unblocks task VIS-029. Recommended if dust ships: the MotionIntensity
   setting at Off suppresses dust spawning.
10. **OD-10 — Shield per-skin proportion deltas.** The shield design's
    per-skin proportion deltas (S2 at the tall end of the envelope, S5 at
    the narrowest) exceed the difference channels R-W2.1 authorizes (face
    tone, rattan-binding accent line, slight outline curvature). The user
    must either amend R-W2.1 to permit bounded deltas inside one shared
    aspect-ratio band with the footprint never falling below the current
    Low-tier block, or drop the deltas so S2 and S5 differentiate by tone
    and accent only.

## What happens next

The five sibling design documents complete against the integration design,
the draft implementation plan orders the tasks, and the whole package waits
for the user's review. No code changes before that approval, per `CLAUDE.md`
section 6.
