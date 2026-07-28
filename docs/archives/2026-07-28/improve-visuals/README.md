# Visual Improvement Package — Plan Overview

> **Archived: reference only.** This document is historical. Its task
> lists, commands, versions, and acceptance criteria are not instructions and
> are not maintained. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, and `docs/development/testing.md`. Note in
> particular that every document in this package quotes the seed-1 reference
> pair as stateHash `27DC94C6E9A01E35` / eventHash `372C9217E5CB8BE9`; that
> pair was already stale before implementation began. The current pair is
> stateHash `A883926A3B93792E` / eventHash `2A9F2D7054CD1805`, recorded in
> `docs/development/testing.md` under "The preset V3 reference pair".

Date: 2026-07-28. Status: **planning approved by the user on 2026-07-28. All
ten open decisions (OD-1 through OD-10) were resolved that day — each to its
recommended default — and the 23-task first milestone of
`implementation-plan-draft.md` was authorized for implementation. The
post-milestone expansion tasks were subsequently authorized as well; 44 of
the package's 47 tasks have landed and are committed as of this update. Three
further decisions — the resolutions of planner-detected inconsistencies 2, 3,
and 4 — and one implementation-time amendment (amendment A-1, the
renderer-agnostic measurement re-specification) were made along the way and
are recorded in "Post-decision resolutions recorded during implementation"
below. VIS-042 and VIS-044 (human-only manual review sessions) and VIS-047
(final integration verification) remain outstanding; see "What happens
next."**

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
| `README.md` | This overview: map, boundaries, resolved decisions | Written |
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
   reference pair, the outcome, and the ordered event stream are identical
   before and after this work. Presentation reads outward only.

   **Correction, recorded during implementation.** Every document in this
   package, including this one in its original revision, quoted the reference
   pair as stateHash `27DC94C6E9A01E35` / eventHash `372C9217E5CB8BE9`, citing
   the Phase 2 reference pair in `docs/development/testing.md`. That pair was
   already stale when this package began. The V3 combat-preset work
   (`6ffd214`, `d82487c`, merged at `473b12d`) changed the ruleset after the
   Phase 2 pair was recorded, and under `CLAUDE.md` section 5 a new preset
   version requires new golden expectations — which were not written at the
   time of that merge. The correct current pair, measured on untouched `main`
   at `dc9d1c7`, is stateHash `A883926A3B93792E` / eventHash
   `2A9F2D7054CD1805` with `measuredTicks` 1 710 and outcome
   `Faction1Victory`. It is now recorded in `docs/development/testing.md`
   under "The preset V3 reference pair", alongside the Phase 2 pair, which is
   kept as the historical record of its own commit rather than overwritten.

   The boundary itself is satisfied and was verified as a before-and-after
   comparison on the same commit lineage, which is what the boundary actually
   demands: the identical 200-agent / 10 000-tick / seed-1 workload run on
   untouched `dc9d1c7` and on the integrated visual-package tree returned
   byte-identical hashes, outcome, tick count, and `deterministic true`. The
   sibling documents in this package still quote the older pair; they are
   archived as written, and this note is the authoritative correction.
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

## Decisions (resolved 2026-07-28)

OD-1 through OD-8 were carried verbatim from
`docs/agents/improve-visuals/requirements.md`; OD-9 and OD-10 arose from the
independent review. On 2026-07-28 the user resolved all ten, in every case
choosing the recommended default. The outcomes, all Resolved 2026-07-28:

1. **OD-1 — Kalasag label promotion.** The shield ships as plain
   `Tall Hardwood Shield` this pass. The pair-form promotion `Kalasag — Tall
   Hardwood Shield` waits for the attestation verification, which remains
   unscheduled.
2. **OD-2 — Palisay.** The *palisay* name may appear in inspector research
   notes as metadata only, explicitly flagged attestation-pending.
3. **OD-3 — Mindanao/Sulu gap.** The Unscoped-generic preset block is
   accepted as the sole Mindanao/Sulu coverage this pass.
4. **OD-4 — Sprite versus procedural direction.** Fully procedural rendering
   is confirmed for this pass; sprites remain a possible later direction
   under the integration design's re-entry criteria.
5. **OD-5 — Earned red putong (C2).** The earned insignia is excluded from
   this pass; the idea is recorded as a backlog item in
   `docs/plans/TODO.md`.
6. **OD-6 — Default theme ground tint.** The default theme's ground shifts
   toward cogon olive-gold this pass, tagged provisional; exploration of
   jungle/plains ground treatments is recorded as a backlog item in
   `docs/plans/TODO.md`.
7. **OD-7 — Shape-redundant faction marker.** Deferred; recorded as a
   backlog item in `docs/plans/TODO.md`. This pass holds the R-W6.10
   no-regression floor only.
8. **OD-8 — Reduced-motion scope.** The MotionIntensity setting governs all
   ambient presentation motion — grass sway now, dust and future ambient
   motion included; gameplay-communicating motion stays exempt.
9. **OD-9 — Dust scope conflict.** R-W4.8 is downgraded from MUST to MAY by
   user approval; task VIS-029 is unblocked but optional. If dust ships, the
   MotionIntensity setting at Off suppresses dust spawning and Reduced
   leaves dust unchanged.
10. **OD-10 — Shield per-skin proportion deltas.** R-W2.1 is amended per
    option (a): a fourth authorized channel — bounded per-skin proportion
    deltas of a few layout pixels inside one shared aspect-ratio band, with
    the rendered footprint never falling below the current Low-tier block —
    guarded by a manual false-cause check row (a narrower skin must never
    read as less mechanical coverage, R-X.12). The S2/S5 deltas are kept.

Confirming these ten as shipped: OD-1 through OD-4 and OD-8 through OD-10 are
implemented directly in the code and catalogs that landed with the package.
OD-5 (earned red putong), OD-6's jungle/plains ground-treatment exploration
beyond the default-theme tint shift, and OD-7 (the shape-redundant faction
marker) each produced a backlog item in `docs/plans/TODO.md` instead of code
in this pass — see "What happens next" below for the pointer to each.

## Post-decision resolutions recorded during implementation

The ten decisions above were resolved before implementation began. Four
further decisions were resolved once implementation was under way — three
planner-detected inconsistencies the plan document flagged for later
resolution (`implementation-plan-draft.md`, "Planner-detected
inconsistencies," items 2 through 4), and one user-approved amendment to the
package's performance-measurement approach. Each is recorded here with its
reasoning, per `implementation-plan-draft.md`'s VIS-046 task.

### Planner inconsistency 2 — catalog identifier grammar, resolved per RF-05

`visual-system-integration-design.md` originally specified an all-lowercase,
three-segment dotted identifier grammar (`<domain>.<family>.<variant>`,
example `shield.tallhardwood.s1`). The weapon and shield design documents
that followed it minted identifiers in a different grammar: camelCase within
a multi-word segment, plus an optional `tint.` sub-segment between family and
variant for presentation-only tint variants (examples:
`weapon.kampilan.tint.freshIron`, `shield.tallHardwood.mactanThin`). The
independent review's finding RF-05 surfaced the mismatch. It is resolved in
favor of the tables that were actually reviewed and shipped: the camelCase
grammar, with the optional `tint.` sub-segment, is canonical, because those
identifier tables are pinned forever once VIS-002 validates against them and
because rewriting the shipped tables to match the original prose would have
been the tail rewriting the dog. `visual-system-integration-design.md`'s
identifier-naming-convention paragraph carries an explicit *post-decision
amendment* note dated 2026-07-28 recording exactly this: the paragraph was
edited after the decision, not before it, and the note says so in place
rather than silently rewriting the original text.

### Planner inconsistency 3 — ground-shading salt reuse, resolved to the recorded default

`battlefield-environment-design.md` originally specified hashing the ground's
corner lattice "with the scenario seed and the existing plains salt," while
the integration design's salt rule and R-W6.2 require every new presentation
trait stream to take its own named salt and forbid reusing an existing one.
Corner-averaged shading changes the ground's shades regardless of which salt
seeds it, so reusing the plains salt would have bought nothing while
breaking the one-salt-per-trait-stream rule. This is resolved to the plan's
own recorded default: the corner lattice takes a new named salt,
`GroundCornerLatticeSalt` (declared in
`src/Hukbo.Client/Presentation/PresentationSalts.cs`, value
`0x5E8C1A4F9D3B7602`), distinct from `PlainsBackdropSalt`. The existing decal
placement stream stays pinned unchanged under `PlainsBackdropSalt` — the
resolution does not touch it — and
`tests/Hukbo.Client.Tests/PresentationSaltsTests.cs` proves that pin directly
by asserting `PlainsBackdropSalt` still equals `0x504C41494E530001`, the same
value `PlainsBackdropGeometry` used before this package touched anything.

### Planner inconsistency 4 — adornment renderable count, resolved by VIS-019

R-W3.1 tallies appearance category I (social and status adornment) as "8,
three renderable," while the shipped `AppearanceComponentCatalog` carries
four renderable entries for that category (I1 full tattoo, I2 partial
tattoo, I4 gold-ear accent, I5 gold-collar accent). VIS-019 resolved the
apparent mismatch against the research document rather than against either
number in isolation. The research document's own reconciled tally
(`warrior-appearance-historical-research.md`, section 3, category I, closing
paragraph — itself a correction of an earlier draft's wrong count, the
research document's own finding RF-07) reads "four are renderable at pawn
scale (I1, I2, I4, and I5, where the two tattoo options I1 and I2 share a
single skin-tone-shift channel)." I1 and I2 both draw through the identical
color-block tone-shift channel — I2 is I1's tone shift applied to a smaller,
staged area (arms and upper torso only), not a distinct channel — so
category I contributes three distinct render channels (the shared tattoo
tone shift, I4's gold-ear accent, I5's gold-collar accent) spread across
four catalog entries. Read this way, "8, three renderable" (channels) and
"four entries" (catalog rows) are the same fact stated two ways, not a
contradiction. The full reasoning, including why I3 (facial tattooing), I6
(gold armlets), I7 (gold dental work), and I8 (tooth filing/blackening) have
no catalog entry at all, is recorded in
`src/Hukbo.Client/Presentation/Catalogs/AppearanceComponentCatalog.cs`'s
class-level doc comment, under the heading "Planner inconsistency 4
(adornment renderable count), resolved."

### Amendment A-1 — renderer-agnostic measurement, approved by the user on 2026-07-28

While the package's rendering and performance-measurement tasks (VIS-034
through VIS-036) were in progress, the user asked whether the tasks as
specified would survive a future switch to a GPU-instanced renderer, since
the original measurement unit — `SpriteBatch.Draw` submission counts — is
meaningful only under the current immediate-mode SpriteBatch backend. The
decision was to continue this package fully procedurally, exactly as OD-4
already settled, but to re-specify VIS-034, VIS-035, and VIS-036 so the
measurement baseline they establish survives a later architecture change
without needing to be re-measured from zero.

The measurement seam now records two tiers of metric. **Tier 1 is
renderer-invariant, and it is the only tier any budget is written against:**
Quads (the primary budget unit — one per filled rectangle and one per
stroked line segment, identical under either an immediate-mode or an
instanced backend), Triangles, `GeometryBuildMicroseconds`,
`SubmitMicroseconds`, and `ManagedBytesAllocated`. **Tier 2 is
backend-specific and purely diagnostic, never a budget:** Submissions,
Batches, TextureBinds, and `BufferUploadBytes`. A Tier 2 metric that does not
apply to the active backend reports zero and is explicitly labelled
not-applicable in the report, so an absent field stays distinguishable from
a genuine zero rather than silently reading as "measured zero." The
measurement seam is an interface rather than a static counter class for
exactly this reason — a future backend implements the interface with its own
Tier 2 semantics instead of extending a class that assumes SpriteBatch — and
the probe report carries an explicit backend fingerprint whose current
recorded value is `"spritebatch-1x1"`.

R-W4.5 ("one batch, one texture") is retained, but it is demoted from a
renderer-invariant budget to a Tier 2 assertion explicitly scoped to the
current SpriteBatch backend: it is a true and useful fact about today's
renderer, and it stops being meaningful the moment a second backend exists,
so it no longer belongs among the numbers a future backend has to satisfy.
VIS-026's zoom-level-of-detail rationale is corrected alongside this: its
justification rests on quad count and fill cost, both of which are real
under any backend, and not on submission count, which is not. In code, this
amendment shows up as `src/Hukbo.Client/Rendering/SubmissionCount.cs`'s
`PawnQuadCount`, `BackdropQuadCount`, and `RenderBudgetEstimate` types (the
file VIS-034 originally specified as a submission-counting seam, redenominated
in quads and renamed in its own doc comments to record the amendment), and in
`docs/development/testing.md`'s "Render performance measurement — full
matrix (VIS-036)" section, which records the Tier 1 quad budgets as the ones
still outstanding.

**Amendment A-1 does not reopen OD-4.** GPU instancing remains out of scope
for this pass: adopting it would require a shader and an MGCB content-pipeline
entry, both forbidden by boundary 4 of this README's non-negotiable
boundaries list, and it would need its own design document and its own new
open decision — A-1 only makes the current pass's measurements survive that
future decision more cheaply, it does not make that future decision here.
Recorded also for that same future decision to weigh: under an instanced
backend, the exact-`Vector2.Zero` sway off-switch (R-W5.5) — which today is
unit-testable as a pure geometry check with no graphics device involved —
would stop being unit-testable in the same way, because verifying it would
require inspecting what an instanced backend's buffer actually uploaded
rather than a plain data structure. That is a real cost a future decision has
to weigh against instancing's benefits; this amendment neither pays it now
nor pretends it does not exist.

### Outstanding work carried forward honestly

The following is not complete, and is recorded here so it reads as
outstanding rather than as finished:

- **No render baseline artifact was produced.** This environment has no
  display and cannot run the MonoGame client. VIS-035's pre-integration
  baseline and VIS-036's full-matrix measured figures remain outstanding.
  Every budget figure in the package — `RenderBudgetEstimate`'s quad
  ceilings and the frame-time figures in the design documents — is labelled
  **ESTIMATE** per R-W6.13, and none of them has been measured. A human at
  an interactive desktop with a GPU must run `Hukbo.Tools.RenderProbe` to
  produce the real baseline and the real matrix; `docs/development/testing.md`
  records this as `BLOCKED, honestly` rather than papering over it.
- **VIS-036's measurement matrix drives only two of its four axes** — agent
  count (200, 500) and camera-zoom station (minimum, default, maximum).
  Grass visibility has no independent override: it is governed entirely by
  the zoom-derived detail tier, so there is no lever to force it on or off at
  a fixed camera station. Motion intensity has no probe-only override either
  — the only lever is the persisted settings file, and driving it from the
  hand-run tool would mean silently overwriting whoever's real settings file
  the tool runs against, which was declined without a reviewed seam decision.
  Extending the seam with true grass and motion overrides is recorded as a
  follow-up, not attempted in this pass.
- **VIS-038 invoked its own design's documented escape hatch** for the
  presentation-neutrality test: no runtime toggle-and-compare seam exists for
  comparing a settings-on run against a settings-off run of the same
  workload, so `PresentationNeutralityTests.cs` records the structural
  argument plus two assembly-reference facts instead of a headless workload
  comparison.
- **VIS-038's no-iteration-order-dependence guard is deliberately scoped** to
  the files that actually decide a presentation variant — the catalogs, the
  resolver, the factory, and the salt registry — rather than banning
  `Dictionary` and `HashSet` across the whole `Hukbo.Client` project, where
  most uses have nothing to do with deterministic variant selection.
- **VIS-021 could not give preset TAG-12 both armor components F3 and F5.**
  The shared preset recipe type carries a single armor slot, and the design
  table's TAG-12 row names both. F3 (Hide Corselet) landed on TAG-10 and
  TAG-11 instead (the row pair the design table itself labels "(veteran)"),
  and F5 (Shell-Set Helmet) landed on TAG-12 alone (the row the table labels
  "(rarity)"). Both components ship as real, rendered recipe components
  across the Tagalog block, matching the design's own summary that the block
  demonstrates both — they simply never appear together on one preset.
- **VIS-034 recorded a tension between two ESTIMATEs, not a resolved
  number.** The combinatorial worst-case per-pawn quad count, multiplied by
  500 units, exceeds the 500-unit ESTIMATE budget by a small margin. The
  budget arithmetic in `RenderBudgetEstimateTests.cs` therefore uses the
  High-tier baseline pawn's quad count instead of the counting seam's own
  combinatorial ceiling, reasoning that five hundred units simultaneously
  hitting that literal combinatorial maximum is not a real battle
  configuration. That reasoning is recorded in the test file, but it is an
  ESTIMATE-versus-ESTIMATE tension all the same, and only the real
  measurement VIS-035/VIS-036 have not yet produced can actually settle it.
- **VIS-042 and VIS-044, the two manual review sessions, are human-only and
  remain outstanding.** Every checklist row created by VIS-041 (the
  milestone smoke rows) and VIS-043 (the full-package smoke rows) in
  `docs/development/testing.md` is `PENDING`, exactly as created, and none of
  them has been flipped by this task or by any other agent.

## What happens next

As of this update, 44 of the 47 tasks in `implementation-plan-draft.md` have
landed and are committed, and this task (VIS-046) is a further one. The
canonical gate (`./scripts/verify.ps1`) is being run separately from this
documentation pass; its real, pasted output — not this document — is the
evidence for whether the integration is clean, per `CLAUDE.md` sections 4 and
6. What remains outstanding after the gate reports is exactly the human-only
work the "Outstanding work carried forward honestly" section above names:
VIS-042 and VIS-044 (the milestone and full-package manual review sessions,
both requiring a human at an interactive desktop) and VIS-047 (final
integration verification and archival readiness, which depends on both of
those and on the gate). Backlog items spun out of the decisions (OD-5 earned
red putong, OD-6 jungle/plains ground exploration, OD-7 shape-redundant
faction marker) live in `docs/plans/TODO.md`.
