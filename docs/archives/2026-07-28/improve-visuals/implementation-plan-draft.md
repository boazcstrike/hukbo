# Implementation Plan Draft — Hukbo Visual Improvement Package

> **Archived: reference only.** This document is historical. Its task
> lists, commands, versions, and acceptance criteria are not instructions and
> are not maintained. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, and `docs/development/testing.md`. Note in
> particular that every document in this package quotes the seed-1 reference
> pair as stateHash `27DC94C6E9A01E35` / eventHash `372C9217E5CB8BE9`; that
> pair was already stale before implementation began. The current pair is
> stateHash `A883926A3B93792E` / eventHash `2A9F2D7054CD1805`, recorded in
> `docs/development/testing.md` under "The preset V3 reference pair".

Date: 2026-07-28. Part of the visual improvement package
(`docs/plans/improve-visuals/README.md`).

## Status

**Approved: on 2026-07-28 the user resolved all ten package open decisions
(OD-1 through OD-10, each to its recommended default — see the decision
record in `docs/plans/improve-visuals/README.md` and the resolved outcomes in
`docs/agents/improve-visuals/requirements.md`) and approved the 23-task first
milestone, authorizing its implementation.** OD-4 (confirmation of the fully
procedural direction), which every task assumes, is confirmed. The
post-milestone expansion was subsequently authorized as well, and 44 of this
plan's 47 tasks have landed and are committed as of this update. Three
planner-detected inconsistencies below (items 2 through 4) are resolved, and
one further decision — amendment A-1, a user-approved re-specification of the
renderer-agnostic measurement approach for VIS-034 through VIS-036 — was made
during implementation; both are recorded in full in
`docs/plans/improve-visuals/README.md`'s "Post-decision resolutions recorded
during implementation" section. VIS-042, VIS-044 (human-only manual review
sessions), and VIS-047 (final integration verification) remain outstanding.
This document orders the work described by the five design documents in this
directory into granular tasks.

Inputs consumed:

- `docs/plans/improve-visuals/visual-system-integration-design.md` (the shared
  infrastructure every task builds on)
- `docs/plans/improve-visuals/weapon-visuals-design.md`
- `docs/plans/improve-visuals/shield-visuals-design.md`
- `docs/plans/improve-visuals/warrior-appearance-design.md`
- `docs/plans/improve-visuals/battlefield-environment-design.md`
- `docs/plans/improve-visuals/README.md` (open decisions OD-1 through OD-8)
- `docs/agents/improve-visuals/requirements.md` (the 88 requirements; the
  authority this plan answers to)
- `docs/agents/improve-visuals/existing-code-analysis.md` (verified file paths
  and code ground truth; every path named below was checked against it or
  against the tree on disk)

## How to read this document

Every task carries fourteen fields: ID and title; Goal; Dependencies (task
IDs); Files (real paths — new files are marked *(new)*); Historical evidence
dependency; Determinism classification (*pure-presentation*,
*presentation-state*, or *forbidden* — no task in this plan is permitted to be
forbidden, and each classification carries its justification); numbered
Implementation steps; Automated verification; Manual visual verification;
Expected artifacts; Acceptance criteria; Rollback or fallback; Blocking
decisions; Prohibited scope.

Tasks marked **[MILESTONE]** form the first milestone (next section). A task
is sized for one implementing agent. Tasks whose file sets are disjoint may
run in parallel; the summary table at the end makes the file ownership
explicit. Two tasks that touch the same file are never scheduled in parallel.

The canonical gate `./scripts/verify.ps1` is the final integration
verification for the milestone (VIS-045) and for the full package (VIS-047).
**It is never delegated to a sub-agent, and its real pasted output is the only
acceptable evidence** (R-W6.15, `CLAUDE.md` sections 4 and 10).

## First milestone — smallest coherent proof of the full pipeline

The milestone proves every layer of the architecture end to end with the
smallest honest content set: catalog infrastructure with real entries, one
weapon variant family (Kalis, the straight L1 default plus its two tints),
one shield skin (S1 `mactanThin` plus the default entry), five generic-levy
clothing presets, one grass cluster system with sway gated behind the new
motion setting, the missing-asset placeholder with its DiagnosticLog events,
the render measurement harness with a recorded baseline, the cross-cutting
test suites, manual `PENDING` rows with one human review session, and one
canonical gate run.

If the milestone integrates cleanly — gate green, the recorded seed-1
reference pair reproduced (stateHash `27DC94C6E9A01E35`, eventHash
`372C9217E5CB8BE9` — `docs/development/testing.md`, Phase 2 reference pair),
baseline recorded, placeholder conspicuous, sway off-switch exact — the
remaining 24 tasks are content expansion over proven rails, not new
architecture.

**Milestone tasks (23):** VIS-001, VIS-002, VIS-003, VIS-004, VIS-005,
VIS-007, VIS-008, VIS-010, VIS-013, VIS-017, VIS-018, VIS-025, VIS-026,
VIS-030, VIS-031, VIS-032, VIS-034, VIS-035, VIS-037, VIS-038, VIS-041,
VIS-042, VIS-045.

**Post-milestone expansion (24):** VIS-006, VIS-009, VIS-011, VIS-012,
VIS-014, VIS-015, VIS-016, VIS-019, VIS-020, VIS-021, VIS-022, VIS-023,
VIS-024, VIS-027, VIS-028, VIS-029, VIS-033, VIS-036, VIS-039, VIS-040,
VIS-043, VIS-044, VIS-046, VIS-047.

## Planner-detected inconsistencies

Surfaced for the user, not silently resolved. Where a task depends on one of
these, the task names it as a blocking decision.

1. **Dust: MUST versus MAY (package-level open decision OD-9 — resolved
   2026-07-28).** Requirement R-W4.8 marked dust puffs MUST;
   `battlefield-environment-design.md` deliberately scoped them MAY "per
   orchestrator direction" and surfaced the discrepancy itself. The user
   resolved OD-9 on 2026-07-28 by amending R-W4.8 to MAY: task VIS-029 is
   unblocked but optional. Per the review's RF-11, if dust ships, the
   decided setting interaction is that `MotionIntensity` Off suppresses dust
   spawning while Reduced leaves dust unchanged, and VIS-031's truth table
   gains the corresponding row. The milestone and the rest of the package do
   not depend on whether dust ships.
2. **Catalog identifier grammar.** The integration design (section 2)
   specifies lowercase dotted `<domain>.<family>.<variant>` with examples like
   `shield.tallhardwood.s1`. The weapon design mints four-segment camelCase
   IDs (`weapon.kampilan.tint.freshIron`) and the shield design mints
   camelCase family and variant segments (`shield.tallHardwood.mactanThin`).
   **Resolved (RF-05, decision confirmed):** the camelCase IDs minted by the
   weapon and shield design tables, plus the optional `tint.` sub-segment,
   are canonical. The VIS-002 grammar regex must match every shipped table
   ID; the integration design's convention paragraph is amended to match
   (marked as a post-decision edit); VIS-046 records the decision.
3. **Ground-shading salt reuse.** `battlefield-environment-design.md` hashes
   the corner lattice "with the scenario seed and the existing plains salt",
   while the integration design's salt rule (section 3) and R-W6.2 say new
   trait streams take new named salts and never reuse the plains salt.
   Corner-averaged shading changes the ground shades regardless (the formula
   changes), so reuse buys nothing and breaks the uniform rule. **Resolved to
   the recorded default:** the corner lattice takes a new named salt,
   `GroundCornerLatticeSalt` (task VIS-027,
   `src/Hukbo.Client/Presentation/PresentationSalts.cs`), rather than reusing
   the plains salt. The existing decal placement stays pinned unchanged under
   `PlainsBackdropSalt` either way, proved by
   `tests/Hukbo.Client.Tests/PresentationSaltsTests.cs`. VIS-046 records the
   decision.
4. **Adornment renderable count.** R-W3.1 says category I (adornment) has
   "8, three renderable"; the warrior-appearance design renders four entries
   (I1 full tattoo, I2 partial tattoo, I4 earring, I5 collar). The counts
   reconcile only if I1/I2 count as one tattoo channel. Minor; VIS-019
   resolves it against the research document and records the reading in the
   component catalog's doc comments.

---

## Category 1 — Catalog and asset infrastructure

### VIS-001 — Presentation salt registry **[MILESTONE]**

- **Goal:** One static registry listing every presentation salt (the three
  existing appearance salts, the plains backdrop salt, and every new salt this
  package mints), each with a doc comment naming its trait stream, so "never
  reuse a salt" becomes a failing test instead of a review habit (R-W6.2).
- **Dependencies:** None. First task in the package.
- **Files:** `src/Hukbo.Client/Presentation/PresentationSalts.cs` *(new)*;
  `tests/Hukbo.Client.Tests/PresentationSaltsTests.cs` *(new)*. Read-only
  reference: `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`,
  `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs` (the existing salt
  values are listed in the registry but their declaration sites do not move
  in this task).
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — constants only; nothing
  reads or writes simulation state.
- **Implementation steps:**
  1. Create the static registry class exposing all registered salts as an
     enumerable of `(name, value, purpose)` entries.
  2. List the three existing `PawnAppearanceFactory` salts
     (`0xA0761D6478BD642F`, `0xE7037ED1A0B428DB`, `0x8EBC6AF09C88C6E3`) and
     the plains salt (`0x504C41494E530001`) with their current purposes.
  3. Reserve and declare the new salts this package needs (weapon tint
     stream, weapon silhouette stream, shield skin stream, appearance block
     assignment, appearance preset selection, grass generation, ground
     corner lattice pending inconsistency 3), any pairwise-distinct values.
  4. Write the pairwise-distinctness test and a test pinning the four
     existing values so a registry edit cannot silently reshuffle shipped
     visuals.
- **Automated verification:** xunit — pairwise distinctness over all
  registered salts; exact-value pins for the four pre-existing salts.
- **Manual visual verification:** None (no drawable output). No checklist row.
- **Expected artifacts:** The two new source files; green test run output.
- **Acceptance criteria:** All salts registered and pairwise distinct by
  test; existing salt values pinned; every new salt carries a purpose doc
  comment.
- **Rollback:** Revert the commit; nothing else references the registry yet.
- **Blocking decisions:** Inconsistency 3 decides whether the corner-lattice
  salt entry is a new value or an annotated reuse; the registry reserves the
  slot either way.
- **Prohibited scope:** No changes to the existing salt declaration sites or
  their consumers; no `Hukbo.Core` change (R-X.11).

### VIS-002 — Visual catalog entry model and identifier convention **[MILESTONE]**

- **Goal:** The shared immutable catalog entry shape every workstream fills:
  `Id`, `Index`, `DisplayLabel`, `EvidenceTier`, `ScopeTag`, `Notes`,
  `MinimumDetailTier`, per integration design section 2 (R-W6.1, R-X.7), plus
  the finalized identifier grammar.
- **Dependencies:** None (parallel with VIS-001).
- **Files:** `src/Hukbo.Client/Presentation/Catalogs/VisualCatalogEntry.cs`
  *(new)*, `src/Hukbo.Client/Presentation/Catalogs/VisualEvidenceTier.cs`
  *(new — extends the `WeaponEvidenceTier` values in
  `src/Hukbo.Client/Presentation/PawnAppearance.cs` with the explicit
  "presentation-only, no historical claim" marker without altering the
  existing enum)*; `tests/Hukbo.Client.Tests/VisualCatalogEntryTests.cs`
  *(new)*.
- **Historical evidence dependency:** None directly; the entry shape carries
  the evidence-tier and scope-tag obligations of R-X.7/R-X.8 rule 10.
- **Determinism classification:** Pure-presentation — immutable client-side
  data types.
- **Implementation steps:**
  1. Implement the decided grammar (RF-05 resolution): camelCase segments as
     minted by the weapon and shield design tables, with an optional `tint.`
     sub-segment; the acceptance regex must match every shipped table ID.
     Write the grammar into a "do not renumber or reword" doc comment
     mirroring the `CombatIdentity` enum comments.
  2. Define the entry record with the seven mandatory fields; domain designs
     add fields by composition, never by removing these.
  3. Define the `MinimumDetailTier` values (Low, Medium, High) aligned to the
     existing tier switch in `src/Hukbo.Client/Rendering/PawnGeometry.cs`.
  4. Add construction-time argument validation (non-empty ID matching the
     grammar, tier present).
- **Automated verification:** xunit — grammar acceptance and rejection cases;
  mandatory-field validation; the evidence-tier extension leaves
  `WeaponEvidenceTier`'s existing members and numeric values untouched.
- **Manual visual verification:** None. No checklist row.
- **Expected artifacts:** New source and test files; green test output.
- **Acceptance criteria:** Entry type exists with all seven fields; grammar
  test suite passes; existing `PawnAppearance` tests still green.
- **Rollback:** Revert; no consumer exists yet.
- **Blocking decisions:** None remaining — inconsistency 2 is decided
  (RF-05): camelCase table IDs with the optional `tint.` sub-segment are
  canonical, and every later ID pins against that grammar. VIS-046 records
  the decision.
- **Prohibited scope:** No catalog content (that is VIS-010 onward); no Core
  types; no renaming of existing `PawnAppearance` members.

### VIS-003 — Fallback resolution chain and diagnostic placeholder definition **[MILESTONE]**

- **Goal:** One pure, total resolution function per domain implementing the
  four-step chain — specific variant → family default → model-category
  default → diagnostic placeholder — plus the placeholder's fixed conspicuous
  color constant (R-W6.4).
- **Dependencies:** VIS-002.
- **Files:** `src/Hukbo.Client/Presentation/Catalogs/VisualFallbackResolver.cs`
  *(new)*; `tests/Hukbo.Client.Tests/VisualFallbackResolverTests.cs` *(new)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — a pure function over
  catalog data and identity inputs; no state.
- **Implementation steps:**
  1. Implement the generic resolver over catalog entries with the four chain
     steps, returning the resolved entry plus the step reached (so callers
     can emit diagnostics for steps past 1).
  2. Declare the placeholder color as a fixed named constant — conspicuous,
     never theme-derived, never invisible (magenta-class value; exact value is
     the implementer's pick within those constraints, then pinned by test).
  3. Guarantee totality: deliberately out-of-range indices and unknown IDs
     resolve to step 3 or step 4, never throw.
  4. Write test doubles that force each chain step, so steps 2–4 are
     exercised even though shipped catalogs make them nearly unreachable
     (the under-exercised-fallback risk named by the integration design).
- **Automated verification:** xunit — totality walk over every enum value
  plus out-of-range inputs; each chain step reachable under a test double;
  placeholder color constant pinned.
- **Manual visual verification:** Deferred to VIS-008's forced-failure row.
- **Expected artifacts:** New source and test files; green test output.
- **Acceptance criteria:** Resolution is total by test; all four steps
  reachable under test; placeholder constant pinned and not theme-derived.
- **Rollback:** Revert; consumers arrive with VIS-008/VIS-010.
- **Blocking decisions:** None.
- **Prohibited scope:** No rendering (VIS-008 draws the placeholder); no
  logging (VIS-004 owns emission); no caching of resolution results.

### VIS-004 — Missing-visual diagnostics **[MILESTONE]**

- **Goal:** The three new `LogEvents` constants —
  `assets.visual.variantMissing`, `assets.visual.fallback`,
  `assets.visual.catalogInvalid` — emitted at `warn` on the `assets` channel,
  once per distinct identifier per session through a bounded seen-set
  (R-W6.5).
- **Dependencies:** VIS-003 (emission points are the resolver's step
  transitions).
- **Files:** `src/Hukbo.Diagnostics/LogEvents.cs` (three new consts);
  `src/Hukbo.Client/Presentation/Catalogs/VisualDiagnostics.cs` *(new —
  the seen-set and emission helper)*;
  `tests/Hukbo.Client.Tests/VisualDiagnosticsTests.cs` *(new)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Presentation-state — a bounded, client-only
  seen-set (named capacity constant, order of 64; when full, further distinct
  identifiers stop logging). Never read by the simulation, never snapshotted.
- **Implementation steps:**
  1. Add the three constants to `LogEvents` following the catalog rule
     (stable dotted identifiers, machine keys, never reworded).
  2. Implement the fixed-capacity seen-set; the seen/disabled check runs
     before any payload work so those paths allocate nothing.
  3. Payloads flat camelCase per the standard: `catalogId`, `requestedId`,
     `resolvedStep` for variantMissing; `catalogId`, `requestedId` for
     fallback; `catalogId`, `reason` (stable reason code) for catalogInvalid.
  4. Wire emission to the resolver's step-2+/step-4 outcomes at the call
     sites that own a `DiagnosticLog` reference (Client side only).
- **Automated verification:** xunit — once-per-identifier emission; capacity
  cap honored; the existing `LogEvents` hygiene suites (constant naming, six
  leading fields, flat payload) extended to the new constants; zero
  allocation on the disabled and already-seen paths where the existing
  allocation-test pattern can assert it.
- **Manual visual verification:** None (log output, not screen). The
  forced-failure row in VIS-041 checks the log line appears alongside the
  placeholder.
- **Expected artifacts:** Updated `LogEvents.cs`; new helper and tests; a
  sample `artifacts/logs/*.jsonl` line from a forced-failure debug run.
- **Acceptance criteria:** Hygiene suites green; dedup and cap by test;
  emission proven once per distinct ID.
- **Rollback:** Revert; `LogEvents` constants are additive.
- **Blocking decisions:** None.
- **Prohibited scope:** No `Hukbo.Core` reference to diagnostics (enforced by
  the existing boundary tests); no per-frame emission; no unbounded set.

### VIS-005 — Contrast envelope constants and color-distance helpers **[MILESTONE]**

- **Goal:** The shared legibility machinery: named envelope constants and a
  pure color-distance helper used by weapon tints (R-W1.7), shield tones
  (R-W2.8), and the dye-palette faction-distance rule (R-W3.8, R-W6.10).
- **Dependencies:** None (parallel with VIS-001..004).
- **Files:** `src/Hukbo.Client/Presentation/ContrastEnvelope.cs` *(new)*;
  `tests/Hukbo.Client.Tests/ContrastEnvelopeTests.cs` *(new)*. Read-only
  reference: `src/Hukbo.Client/UI/FactionColorPalette.cs`,
  `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs` (the 0.22 ceiling).
- **Historical evidence dependency:** None (the palette values it will later
  check are evidence-bearing; the envelope itself is machinery).
- **Determinism classification:** Pure-presentation — constants and pure
  functions.
- **Implementation steps:**
  1. Implement a pure color-distance function over `Color` values (channel
     distance metric; exact metric is the implementer's pick, then pinned).
  2. Declare the envelope bounds as named `PROVISIONAL` constants: minimum
     distance of any equipment tone from all ground shades at the 0.22
     ceiling, from pawn clothing colors, and minimum dye-to-faction-constant
     distance.
  3. Expose check helpers the catalog validation (VIS-006) and tests call.
- **Automated verification:** xunit — metric pinned on known pairs; envelope
  constants pinned; helper flags a deliberately illegal pair.
- **Manual visual verification:** None directly; legibility itself is judged
  by the VIS-041/VIS-043 zoom rows.
- **Expected artifacts:** New source and test files; green test output.
- **Acceptance criteria:** Helpers pure and pinned; constants named and
  marked `PROVISIONAL` (R-X.9).
- **Rollback:** Revert; no consumer yet.
- **Blocking decisions:** None.
- **Prohibited scope:** No changes to `FactionColorPalette` or theme roles
  (rejected approach in the integration design).

### VIS-006 — Startup catalog validation pass

- **Goal:** A once-at-load Client validation in the `UiThemeCatalog` style:
  identifier uniqueness, index contiguity, mandatory metadata presence, and
  per-catalog combination rules; failures log `assets.visual.catalogInvalid`
  and fall back per the chain — never crash, never silently drop an entry
  (integration design section 2).
- **Dependencies:** VIS-002, VIS-003, VIS-004. Post-milestone (shipped
  catalogs are code and cannot fail these checks at runtime; the xunit copies
  in VIS-037 protect the milestone).
- **Files:** `src/Hukbo.Client/Presentation/Catalogs/VisualCatalogValidator.cs`
  *(new)*; wiring at load in `src/Hukbo.Client/ArenaGame.cs` (LoadContent
  region only); `tests/Hukbo.Client.Tests/VisualCatalogValidatorTests.cs`
  *(new)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — a read-only pass over
  static data at load.
- **Implementation steps:**
  1. Implement the validator over any catalog implementing the shared entry
     shape; return structured failures with stable reason codes.
  2. Wire one invocation per catalog at Client load, logging failures and
     marking failed entries so resolution skips to the family default.
  3. Test with deliberately invalid in-memory catalogs (duplicate ID, index
     gap, missing tier, missing scope tag on a cultural entry).
- **Automated verification:** xunit — each failure class detected with its
  reason code; a valid catalog passes clean; failure marks route resolution
  to step 2.
- **Manual visual verification:** None; runtime effect is only observable in
  a forced-failure build (covered by the VIS-043 forced-failure row).
- **Expected artifacts:** New validator, wiring diff, tests; green output.
- **Acceptance criteria:** All shipped catalogs pass at load with zero log
  lines; every failure class has a test.
- **Rollback:** Revert wiring; the game runs without the pass (tests still
  protect the contract).
- **Blocking decisions:** None.
- **Prohibited scope:** No exceptions thrown to the player; no entry ever
  silently dropped; `ArenaGame` gains wiring only, no logic (the logic lives
  in the testable validator).

## Category 2 — Renderer primitives and layering

### VIS-007 — Detail-tier gate helper **[MILESTONE]**

- **Goal:** One pure function mapping apparent scale to the drawing decision
  for any catalog entry via its `MinimumDetailTier`, testable at exactly 0.95
  and 1.80 (R-X.4), so tier gating is data the tests walk rather than logic
  scattered through renderers.
- **Dependencies:** VIS-002.
- **Files:** `src/Hukbo.Client/Rendering/DetailTierGate.cs` *(new)*;
  `tests/Hukbo.Client.Tests/DetailTierGateTests.cs` *(new)*. Read-only
  reference: the tier switch in `src/Hukbo.Client/Rendering/PawnGeometry.cs`.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — pure function of
  apparent scale and entry data.
- **Implementation steps:**
  1. Implement `ShouldDraw(apparentScale, MinimumDetailTier)` delegating to
     the existing tier thresholds (reuse, do not duplicate, the 0.95/1.80
     switch — the duplicated-formula lesson from the backdrop renderer).
  2. Tests at the exact boundary values on both sides of each threshold.
- **Automated verification:** xunit — boundary tests at 0.95 and 1.80
  exactly; Low/Medium/High mapping for each `MinimumDetailTier` value.
- **Manual visual verification:** None; tier behavior on screen is judged by
  the zoom rows in VIS-041/VIS-043.
- **Expected artifacts:** New source and test files; green output.
- **Acceptance criteria:** Single formula, no duplication; exact-threshold
  tests pass.
- **Rollback:** Revert; no consumer yet.
- **Blocking decisions:** None.
- **Prohibited scope:** No change to the existing tier thresholds or the
  apparent-scale clamp (R-X.4 keeps the existing machinery).

### VIS-008 — Placeholder rendering path **[MILESTONE]**

- **Goal:** Draw the step-4 diagnostic placeholder — a solid block in the
  fixed conspicuous color at the element's layout position — whenever a
  resolver reaches step 4, emitting the `assets.visual.fallback` diagnostic
  once per identifier (R-W6.4, R-W6.5).
- **Dependencies:** VIS-003, VIS-004.
- **Files:** `src/Hukbo.Client/Rendering/PawnRenderer.cs` (placeholder branch
  in the draw path); `src/Hukbo.Client/Rendering/PawnGeometry.cs` (the
  placeholder block's layout rectangle, so the position stays a tested pure
  output); `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` (extended).
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — draw-only branch on the
  resolver's output.
- **Implementation steps:**
  1. Add the placeholder rectangle computation to `PawnLayout` (pure,
     tested).
  2. Add the renderer branch: when resolution reports step 4, fill the
     placeholder rectangle in the fixed color from the existing 1x1 texture
     inside the existing arena batch; no new draw-call class.
  3. Route the diagnostic emission through VIS-004's helper at this branch.
- **Automated verification:** xunit — placeholder rectangle geometry pinned;
  a resolver double forced to step 4 yields the placeholder layout;
  renderer itself stays an untested draw-only sink (R-W6.16).
- **Manual visual verification:** Row (VIS-041): "In a forced-failure debug
  run, the diagnostic placeholder is conspicuously visible at the affected
  element's position and the assets channel logs the fallback once." Created
  `PENDING`; only a human at an interactive desktop may flip it.
- **Expected artifacts:** Diffs to the two rendering files; extended geometry
  tests; a forced-failure screenshot under `artifacts/` when the manual row
  is exercised.
- **Acceptance criteria:** Geometry pinned; step-4 path exercised under a
  test double; no new `Begin`/`End`, no new texture.
- **Rollback:** Revert the branch; resolution still returns step 3's
  drawable, so the screen degrades to today's visuals.
- **Blocking decisions:** None.
- **Prohibited scope:** No formula in the renderer (geometry owns layout); no
  per-frame diagnostic emission.

### VIS-009 — PawnLayout anchors and composed-layer scaffolding

- **Goal:** Named anchor fields on `PawnLayout` (weapon grip anchor, shield
  anchor) and the empty layer slots for armor, sash, and adornment accents in
  the renderer's back-to-front order (integration design section 5), so the
  post-milestone appearance and posture tasks attach to layout-owned points.
- **Dependencies:** VIS-007. Post-milestone.
- **Files:** `src/Hukbo.Client/Rendering/PawnGeometry.cs`,
  `src/Hukbo.Client/Rendering/PawnRenderer.cs`,
  `tests/Hukbo.Client.Tests/PawnGeometryTests.cs`.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — layout outputs only.
- **Implementation steps:**
  1. Add the grip and shield anchor fields to `PawnLayout`, computed in
     `PawnGeometry.Create`, matching where the weapon and shield draw today
     (zero visual change in this task).
  2. Add feet-anchoring property tests: stature and build multipliers grow
     the figure upward and outward from the ground-ring center.
  3. Insert the layer slots (4 armor, 5 sash, 9 adornments) as no-ops in
     `PawnRenderer.Draw`, preserving the documented order.
  4. Confirm `GetBounds` is unchanged and stays pose-blind (R-X.5).
- **Automated verification:** xunit — anchors equal current draw positions;
  feet-anchoring property; `GetBounds` output identical before and after
  (pinned values).
- **Manual visual verification:** Row (VIS-043): "Pawns render identically to
  the pre-package build at all three zoom stations" — a no-visual-change
  check for this task's window.
- **Expected artifacts:** Diffs and extended tests; green output.
- **Acceptance criteria:** Zero rendering difference by construction; anchor
  and bounds tests pass.
- **Rollback:** Revert; nothing consumes the anchors yet.
- **Blocking decisions:** None.
- **Prohibited scope:** No facing or mirroring channel (out of scope by
  recorded `PawnGeometry` decision); no drawable content in the new slots.

## Category 3 — Weapon visuals

### VIS-010 — Kalis variant family (milestone weapon) **[MILESTONE]**

- **Goal:** The first real catalog content: `weapon.kalis.l1` (the straight
  default pawn silhouette), its two tints (`freshIron`, `darkHilt`), and the
  inspector-only `l2`/`l3` entries, selected by the new tint stream and drawn
  through the existing weapon lines (weapon design, Kalis section; R-W1.1,
  R-W1.2, R-W1.3, R-W1.4).
- **Dependencies:** VIS-001, VIS-002, VIS-003, VIS-005, VIS-007.
- **Files:** `src/Hukbo.Client/Presentation/Catalogs/WeaponVisualCatalog.cs`
  *(new)*; `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs` (tint
  stream selection); `src/Hukbo.Client/Presentation/PawnAppearance.cs`
  (variant fields); `src/Hukbo.Client/Rendering/PawnRenderer.cs` (tint
  application in `DrawWeapon`/`DrawBlade`);
  `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs` (extended);
  `tests/Hukbo.Client.Tests/WeaponVisualCatalogTests.cs` *(new)*.
- **Historical evidence dependency:**
  `docs/research/improve-visuals/weapons-shields-historical-research.md` —
  L1 (Documented for name and class, conservative form; `Cebu — 1521`), L2/L3
  (Provisional reconstruction, inspector-only); tints carry the
  presentation-only marker.
- **Determinism classification:** Pure-presentation — pure salted function of
  `(EntityId, CombatLoadout)`; equipment identity stays loadout-only (pinned
  rule extended, never weakened).
- **Implementation steps:**
  1. Author the five Kalis catalog entries with IDs (per the VIS-002
     grammar), indexes, labels (pair form `Kalis — Thrusting Blade`
     unchanged), tiers, notes, and `MinimumDetailTier` (tints tone-only at
     Low; hilt tone visible Medium+; wear High).
  2. Add the tint stream: `EntityId` XOR the registered weapon-tint salt,
     SplitMix64-finalizer mix, modulo the weapon's tint count. The
     silhouette stream exists but is degenerate (one pawn-scale entry).
  3. Mark `l2`/`l3` inspector-only; the selection stream can never return
     them (exclusion by construction plus test).
  4. Apply tint colors in the renderer's existing line draws; zero geometry
     change; blade length, offsets, and width multiplier untouched.
  5. Run tint values through the VIS-005 envelope checks; adjust within the
     documented material palette until they pass.
- **Automated verification:** xunit — variant stability (same inputs, same
  IDs every call); silhouette-classification invariance under both tints at
  every tier; `l2`/`l3` unreachable by the stream; envelope pins; evidence
  tier and note present on every entry; fallback totality for the Kalis
  chain; existing factory pins still green.
- **Manual visual verification:** Rows in VIS-041: minimum zoom — Kalis-armed
  pawns remain classifiable, tints invisible or sub-threshold; normal zoom —
  tints read as material variation, not different weapons; maximum zoom —
  tint visible without breaking role recognition. Human-only flips.
- **Expected artifacts:** New catalog and tests; diffs; green test output.
- **Acceptance criteria:** All listed tests green; drawn blade geometry
  identical to today for both tints (only color differs).
- **Rollback:** Remove the tint application; the factory falls back to the
  single current appearance; catalog entries are inert data.
- **Blocking decisions:** None remaining — OD-W1-b (final tint hex values)
  is decided here under the envelope tests; inherited OD-4 is resolved
  (procedural confirmed 2026-07-28).
- **Prohibited scope:** No wavy blade at pawn scale at any tier (R-W1.4); no
  per-variant length or reach delta (false-cause rule); no new `WeaponId`.

### VIS-011 — Kampilan, Wasay, and Itak variant families

- **Goal:** Complete the weapon catalog: Kampilan k1 + three tints + k2
  inspector-only; Wasay w1 + three tints including the `lashedWorn` rattan
  band accent (Medium+); Itak i1 + two tints; per-weapon fallback chains
  (weapon design tables; R-W1.1 through R-W1.9).
- **Dependencies:** VIS-010 (pattern, shared files). Post-milestone. Runs as
  one task because it shares `WeaponVisualCatalog.cs`, the factory, and the
  renderer across all three weapons — not parallelizable internally.
- **Files:** Same set as VIS-010, plus
  `src/Hukbo.Client/Rendering/PawnGeometry.cs` if OD-W1-c (Kampilan
  Medium-tier forward-widening) is accepted.
- **Historical evidence dependency:** Research entries K1/K2/K3 (`Mactan —
  1521`), W1/W3 (rattan lashing: documented ubiquitous technique), I1
  (composite Provisional reconstruction; the *itak* name attestation
  unconfirmed, disclosed in the note). W2 receives **no identifier**
  (excluded entirely, R-W1.4).
- **Determinism classification:** Pure-presentation, as VIS-010.
- **Implementation steps:**
  1. Author the Kampilan entries; implement forward-widening only if OD-W1-c
     is accepted and the classification tests cleanly bound it, else ship
     today's uniform blade width and record the deferral.
  2. Author the Wasay entries; add the lashing band as one short rectangle at
     the head-haft junction, gated Medium+ through VIS-007.
  3. Author the Itak entries (two tints — the honest ceiling).
  4. Extend the exclusion test: `k2`, `l2`, `l3` unreachable; W2 has no ID.
  5. Envelope-check every tint in all five themes.
- **Automated verification:** As VIS-010 per weapon, plus: lashing band
  absent below 0.95 apparent scale (exact-threshold test); tint count per
  weapon at most three (R-W1.8 pin); every entry carries tier and note
  (extends the existing every-weapon-evidence-note test).
- **Manual visual verification:** VIS-043 rows: the three zoom rows across
  all four weapons; "the Wasay lashing band reads as a band, not damage or a
  new weapon part."
- **Expected artifacts:** Diffs, extended tests, green output.
- **Acceptance criteria:** Full weapon catalog validated; all four weapons
  remain mutually distinguishable by silhouette test at every tier (R-X.3).
- **Rollback:** Per-weapon: drop a family's tint application and its entries;
  each weapon degrades independently to today's single appearance.
- **Blocking decisions:** OD-W1-a (armory-card art for K2/L2/L3 — default
  text-only; no drawing task exists in this plan); OD-W1-c (forward-widening).
- **Prohibited scope:** No pommel motifs, spikelets, tassels, or chain-mail
  guards on any pawn; no Cordilleran head axe anywhere; tint counts capped.

### VIS-012 — Weapon variant inspector surface

- **Goal:** The agent inspector shows, for the selected pawn, the unchanged
  pair-form weapon label plus the variant's evidence tier and note (R-W1.6,
  R-X.6, R-X.7, R-X.10) — the spectator-discoverability channel.
- **Dependencies:** VIS-010, VIS-011. Post-milestone.
- **Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs` (pure content
  composition), `src/Hukbo.Client/UI/AgentInspectorPanel.cs` (layout wiring
  only); `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` (extended
  or new).
- **Historical evidence dependency:** The tier and note strings authored in
  VIS-010/VIS-011; no new claims minted here.
- **Determinism classification:** Pure-presentation — text composition from
  catalog data; strings precomputed at catalog construction, no per-frame
  string building (integration design section 8).
- **Implementation steps:**
  1. Extend the inspector content builder with the variant lines: label,
     tier, note, inspiration tag where present.
  2. Ensure all strings come precomposed from catalog entries.
  3. Inspector-only entries (`k2`, `l2`, `l3`) appear as "later or
     provisional forms" notes on the weapon, explicitly labelled.
- **Automated verification:** xunit over the content builder — variant line
  present and correct for each weapon and variant combination; no bare
  Filipino term without its descriptor half (negative test).
- **Manual visual verification:** VIS-043 row: "Inspector shows, for a
  selected pawn, the pair-form weapon label, the variant's evidence tier, and
  its note." Human-only flip.
- **Expected artifacts:** Diffs, tests, green output; an inspector screenshot
  under `artifacts/` when the row is exercised.
- **Acceptance criteria:** Content tests green; every rendered variant is
  discoverable through the inspector.
- **Rollback:** Revert; inspector shows today's weapon line only.
- **Blocking decisions:** None.
- **Prohibited scope:** No new fonts or UI framework; no `ArenaGame`
  construction in tests (R-W6.16).

## Category 4 — Shield visuals

### VIS-013 — Shield skin infrastructure plus S1 `mactanThin` **[MILESTONE]**

- **Goal:** The shield skin catalog with the S1 skin (lightest pale palm-wood
  tone, straight outline, no accent) and the `default` entry (today's block),
  a new salted skin stream, and tone application in the shield draw — proving
  the shield leg of the pipeline with one skin (shield design; R-W2.1,
  R-W2.2, R-W2.3).
- **Dependencies:** VIS-001, VIS-002, VIS-003, VIS-005, VIS-007.
- **Files:** `src/Hukbo.Client/Presentation/Catalogs/ShieldVisualCatalog.cs`
  *(new)*; `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs` (skin
  stream); `src/Hukbo.Client/Presentation/PawnAppearance.cs` (skin field);
  `src/Hukbo.Client/Rendering/PawnRenderer.cs` (`DrawShield` tone);
  `tests/Hukbo.Client.Tests/ShieldVisualCatalogTests.cs` *(new)*;
  `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs` (extended).
- **Historical evidence dependency:** Research entry S1 — Documented
  (existence, thinness, active use); Documented, form uncertain (shape);
  `Mactan — 1521`. The thin-wood-versus-hardwood note is recorded on the
  entry (R-W2.7).
- **Determinism classification:** Pure-presentation — pure salted function of
  `EntityId`; shield presence stays loadout-only (pinned tests extended).
- **Implementation steps:**
  1. Author the S1 and default entries with IDs, tiers, anchors, notes, and
     `MinimumDetailTier` (tone at every tier; skin differences sub-threshold
     at Low).
  2. Add the skin stream: `EntityId` XOR the registered shield-skin salt,
     mix, modulo the shipped skin count (1 at the milestone — degenerate but
     real, so VIS-014 only grows the modulus).
  3. Apply the face tone in `DrawShield`; footprint, outline, and seam
     unchanged in this task.
  4. Envelope-check the S1 tone against torso colors and ground shades in
     all five themes (R-W2.8).
- **Automated verification:** xunit — skin stability; shield presence from
  loadout only, `ShieldId.None` draws nothing regardless of any stream;
  footprint at Low tier at least the current block (pinned); envelope pins;
  evidence metadata present; fallback totality for the shield chain
  (steps 2 and 3 distinct even though they coincide in effect).
- **Manual visual verification:** VIS-041 rows: minimum zoom — shielded
  versus unshielded distinguishable; normal zoom — the skin reads as the
  same equipment.
- **Expected artifacts:** New catalog and tests, diffs, green output.
- **Acceptance criteria:** All tests green; every shielded pawn shows the S1
  tone at the milestone (the skin stream's modulus is 1, so this is a
  deliberate fleet-wide tone shift from the charred-wood block to the pale
  palm-wood face — stated here so the milestone human review is not
  surprised by it); the `default` entry is the fallback target, not a rolled
  skin, and is unreachable by the stream until VIS-014 grows the modulus.
- **Rollback:** Drop the tone application; the default entry is today's
  drawing.
- **Blocking decisions:** None remaining — inherited OD-4 is resolved
  (procedural confirmed 2026-07-28). OD-1 never blocked this task, and its
  2026-07-28 resolution confirms the label stays `Tall Hardwood Shield`.
- **Prohibited scope:** No footprint or shape change; no new `ShieldId`; no
  breast-high, round, buckler, pronged, or tufted forms (R-W2.4); no kalasag
  label anywhere player-facing.

### VIS-014 — Remaining shield skins and the proportion envelope

- **Goal:** S2 `morgaFullBody` (mid tone, tall-end proportion), S3
  `boxerCagayan` (charred tone, one-to-two-pixel outline curvature), S5
  `visayanKalasag` (resin-brown tone, horizontal rattan accent replacing the
  vertical seam, narrowest proportion), all inside one shared aspect-ratio
  band with per-skin deltas of a few layout pixels (R-W2.1, R-W2.4).
- **Dependencies:** VIS-013. Post-milestone.
- **Files:** `src/Hukbo.Client/Presentation/Catalogs/ShieldVisualCatalog.cs`;
  `src/Hukbo.Client/Rendering/PawnGeometry.cs` (per-skin layout deltas,
  curvature insets); `src/Hukbo.Client/Rendering/PawnRenderer.cs` (accent
  line, edge-tone step at High);
  `tests/Hukbo.Client.Tests/ShieldVisualCatalogTests.cs` and
  `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` (extended).
- **Historical evidence dependency:** S2 (`Manila — 1609`; the "top to toe"
  quotation must not appear player-facing until verified against Blair &
  Robertson), S3 (`Manila — c.1590`), S5 (`Visayas — 16th c. (synthesis)`;
  kalasag name provisional, disclosed in the note).
- **Determinism classification:** Pure-presentation.
- **Implementation steps:**
  1. Author the three entries; grow the skin stream modulus to 4.
  2. Define the shared aspect-ratio band as named constants; express
     per-skin deltas within it.
  3. Implement the S3 curvature (top and bottom edge insets) degrading to
     the straight block at Low tier; the S5 horizontal accent gated Medium+.
  4. Add the High-tier one-pixel edge-tone step for all skins.
  5. Envelope-check all face tones.
- **Automated verification:** xunit — every skin inside the band at every
  tier; Low-tier footprint floor for all four; tier gates at exact
  thresholds (accent and seam Medium+, edge step High); stability and
  loadout-only tests over the modulus of 4; envelope pins; metadata
  presence.
- **Manual visual verification:** VIS-043 rows: "the four skins read as
  variation of one shield, not different equipment"; "the S5 accent reads as
  binding, not damage"; the maximum-zoom and high-contrast rows per the
  shield design.
- **Expected artifacts:** Diffs, extended tests, green output.
- **Acceptance criteria:** Classification band tests green; all four skins
  ship; skin count is exactly four plus default (R-W2.1 pin).
- **Rollback:** Reduce the modulus back to 1; S1-only remains valid.
- **Blocking decisions:** None remaining — **OD-10 (from review finding
  RF-08) is resolved 2026-07-28, option (a):** R-W2.1 is amended with a
  fourth authorized channel, so this task ships the bounded per-skin
  proportion deltas (S2 at the tall end of the band, S5 the narrowest) as
  authorized — a few layout pixels inside the one shared aspect-ratio band,
  footprint never below the current Low-tier block. The amendment is guarded
  by the manual false-cause check row "skins read as variation, not as
  different equipment": a narrower skin must never read as less mechanical
  coverage than any other skin on the same loadout (R-X.12), and a failure
  there drops the deltas before it drops any skin. Also decided in this
  task: OD-W2-a (band values — decided here under the tests, now meaningful
  since the deltas are approved); OD-W2-c (whether `boxerCagayan` keeps the
  vertical seam — decided here, default keep, revisited against the manual
  rows).
- **Prohibited scope:** As VIS-013; additionally no fifth skin (the
  evidence's count is four) and no carved-face decoration.

### VIS-015 — Angled active shield posture (S12)

- **Goal:** Draw the tall shield slightly angled forward of the pawn — a
  fixed layout offset and small fixed rotation in `PawnGeometry`, marked
  `PROVISIONAL`, identical for all skins, constant over time, bounds-neutral
  (R-W2.5, R-X.5).
- **Dependencies:** VIS-009 (shield anchor), VIS-013. Post-milestone.
- **Files:** `src/Hukbo.Client/Rendering/PawnGeometry.cs`,
  `src/Hukbo.Client/Rendering/PawnRenderer.cs`,
  `tests/Hukbo.Client.Tests/PawnGeometryTests.cs`.
- **Historical evidence dependency:** S12 — Provisional reconstruction
  (Hinilawod epic; Cole 1922 grip description as stance inspiration only).
  The constants are a drawing choice, never presented as measurement (R-X.9).
- **Determinism classification:** Pure-presentation — static layout
  constants; not animated, reads no combat state, adds no pose channel.
- **Implementation steps:**
  1. Add the named `PROVISIONAL` offset and rotation constants.
  2. Compute the angled shield placement from the shield anchor in the
     layout; account for the fixed offset in the pose-independent bounds
     once, statically.
  3. Verify Low-tier non-occlusion of the faction ring, weapon line, and
     head with geometry assertions.
- **Automated verification:** xunit — offset and rotation equal the named
  constants; `GetBounds` independent of animation phase and identical across
  all skins; non-occlusion assertions at Low tier.
- **Manual visual verification:** VIS-043 row: "the angled posture reads as
  an active stance, not a layout bug" (maximum zoom).
- **Expected artifacts:** Diffs, extended tests, green output.
- **Acceptance criteria:** Bounds-neutrality and non-occlusion by test;
  constants named and `PROVISIONAL`.
- **Rollback:** Set the offset and rotation constants to zero — the passive
  side-slab drawing returns; no other code changes.
- **Blocking decisions:** OD-W2-b (the angle and offset values — decided
  here under the tests).
- **Prohibited scope:** No animation, no shield-strike behavior, no reaction
  to combat state; no hand, strap, or grip detail drawn.

### VIS-016 — Shield inspector surface and name negative tests

- **Goal:** Inspector entries per skin — plain label `Tall Hardwood Shield`,
  anchor tag, tier, note, pending-verification flags — plus the negative
  test that no player-facing string contains an unverified Filipino shield
  name (R-W2.6, R-W2.7, R-X.6).
- **Dependencies:** VIS-013, VIS-014, VIS-012 (shared inspector files —
  sequenced after, never parallel with it). Post-milestone.
- **Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs`,
  `src/Hukbo.Client/UI/AgentInspectorPanel.cs`;
  `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`.
- **Historical evidence dependency:** The skin anchors and notes from
  VIS-013/VIS-014; the *kalasag* and *palisay* PENDING status from the
  research.
- **Determinism classification:** Pure-presentation — precomposed text.
- **Implementation steps:**
  1. Add the shield block to the inspector content builder: label, anchor
     tag, tier, note.
  2. Flag pending names (*kalasag* on S5's note; *palisay* if OD-2's default
     stands) explicitly as pending verification, inspector metadata only.
  3. Write the negative test: `Kalasag`, `Palisay`, `Taming`, `Salakot`, and
     `Panabas` never appear in any player-facing label string; the first two
     may appear only inside flagged inspector research notes, and the last
     three not at all.
- **Automated verification:** xunit — content correctness per skin; the
  forbidden-name negative suite; pending flags present where required.
- **Manual visual verification:** VIS-043 row: "Inspector shows, for a
  selected shielded pawn, the plain shield label, the skin's anchor tag,
  tier, and note including pending flags."
- **Expected artifacts:** Diffs, tests, green output.
- **Acceptance criteria:** Negative suite green; every skin discoverable.
- **Rollback:** Revert; inspector shows the loadout line only.
- **Blocking decisions:** None remaining — OD-1 is resolved 2026-07-28 (the
  plain label ships; the pair-form promotion waits for the attestation
  verification, unscheduled; a later positive verification changes one
  string); OD-2 is resolved 2026-07-28 (the palisay note is
  inspector-metadata only, flagged attestation-pending).
- **Prohibited scope:** No pair-form shield label; no unverified quotation
  (the Morga "top to toe" passage stays out until verified).

## Category 5 — Warrior appearance composition

### VIS-017 — Component catalog core and dye palette **[MILESTONE]**

- **Goal:** The appearance component catalog's entry types and the milestone
  component subset needed by the generic levy presets (hair B1/B3, head
  covering C4/C5, torso D1, lower garment E1, sash/belt G2/G3, condition
  K1/K2/K3/K4/K5, side-blade accent H1), plus the full category-J dye palette
  as named constants (R-W3.1, R-W3.8, R-W3.11).
- **Dependencies:** VIS-001, VIS-002, VIS-005.
- **Files:**
  `src/Hukbo.Client/Presentation/Catalogs/AppearanceComponentCatalog.cs`
  *(new)*; `src/Hukbo.Client/Presentation/Catalogs/DyePalette.cs` *(new)*;
  `tests/Hukbo.Client.Tests/AppearanceComponentCatalogTests.cs` *(new)*.
- **Historical evidence dependency:**
  `docs/research/improve-visuals/warrior-appearance-historical-research.md` —
  categories A through K with per-option tiers; the category-J swatch table
  (undyed cream `#E7D8B7`, indigo `#354D6B`, blue-black `#2A3140`, sappan red
  `#8F3F35`, turmeric yellow `#C9A23F`, bark brown `#7A5A3A`, gold accent
  `#D0A64A`, iron blue-black `#384249`); condition options carry the "no
  historical claim" marker.
- **Determinism classification:** Pure-presentation — immutable data.
- **Implementation steps:**
  1. Define the component entry type (extends the VIS-002 shape with render
     channel and component category fields).
  2. Author the milestone component options listed in the goal, each with
     its research code as the variant segment, tier, scope, note, render
     channel, and tier gate.
  3. Author the ten dye constants exactly at the research values; run the
     minimum dye-to-faction color distance check from VIS-005.
  4. Record the resolution of inconsistency 4 in the catalog doc comment
     when the full category-I set arrives (VIS-019); the milestone set does
     not include category I.
- **Automated verification:** xunit — palette pins at the exact hex values;
  dye-to-faction distance holds for all ten constants against all three
  faction constants; every component option carries tier and channel;
  condition options carry the no-historical-claim marker.
- **Manual visual verification:** None directly; covered by the preset rows.
- **Expected artifacts:** New source and test files; green output.
- **Acceptance criteria:** Milestone component set complete and validated;
  palette pinned.
- **Rollback:** Revert; no consumer until VIS-018.
- **Blocking decisions:** None at milestone scope.
- **Prohibited scope:** No component invented beyond the research categories
  (R-W3.1); no faction color in any garment constant; no motif geometry.

### VIS-018 — Milestone levy presets and selection streams **[MILESTONE]**

- **Goal:** Five generic levy presets (LEV-01, LEV-02, LEV-03, LEV-04,
  LEV-09), the two new selection streams (block assignment and preset
  selection), loadout filtering, and the combination-validator skeleton with
  the structural checks expressible over the milestone set (R-W3.2 partial,
  R-W3.3, R-W3.4 skeleton, R-W3.5).
- **Dependencies:** VIS-017, VIS-003, VIS-007.
- **Files:**
  `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Levy.cs` *(new)*;
  `src/Hukbo.Client/Presentation/Catalogs/AppearancePresetValidator.cs`
  *(new)*; `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs` (preset
  selection wiring); `src/Hukbo.Client/Presentation/PawnAppearance.cs`
  (preset fields); `src/Hukbo.Client/Rendering/PawnRenderer.cs` (garment
  base-tone application within existing torso/head draws);
  `tests/Hukbo.Client.Tests/AppearancePresetTests.cs` *(new)*;
  `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs` (extended).
- **Historical evidence dependency:** The levy block rows of the
  warrior-appearance design (scope tag Unscoped-generic; minimal kit, undyed
  cloth, no tattoos, no gold, no putong); tiers per row (DFU/PR).
- **Determinism classification:** Pure-presentation — selection is
  `Mix(Scenario.Seed ^ BlockAssignmentSalt ^ factionId)` for the block and
  `Mix(EntityId ^ PresetSelectionSalt)` for the preset; all inputs immutable
  for the match; no stored state. At the milestone the block table maps every
  faction to the levy block (degenerate but real).
- **Implementation steps:**
  1. Author the five preset recipes from the design's levy table, including
     the H1 Wasay-only restriction on LEV-…? (of the five chosen, none
     carries H1 — chosen deliberately so the milestone needs no loadout
     filter edge case; the filter machinery still ships and is tested with a
     synthetic H1 recipe).
  2. Implement block assignment (degenerate levy-only table) and weighted
     preset selection with loadout filtering.
  3. Implement the validator skeleton: scope tag present, weakest-link tier
     computation, differentiation criterion (silhouette category or two
     countable categories, applied pairwise **within a regional block** per
     the decided RF-02 scope — the milestone's single levy block is
     unaffected), loadout-pool totality (every block/loadout pair resolves a
     preset or a defined fallback).
  4. Render the milestone presets through existing channels: head treatment
     mapping (B/C options onto the existing head-treatment draws), garment
     base tone at Low, sash/belt line and hat disk at Medium+ via VIS-007.
  5. Fallbacks per the design: LEV-01 falls back to the diagnostic
     placeholder chain; the other four fall back to LEV-01.
- **Automated verification:** xunit — selection stability across calls;
  existing three-trait outputs unchanged (salt independence pinned);
  validator passes all five presets; differentiation holds pairwise; a
  deliberately illegal synthetic recipe fails; pool totality for the levy
  block across all loadouts.
- **Manual visual verification:** VIS-041 rows: "levy presets read as varied
  but coherent at normal zoom"; "no preset reads as a different faction or
  different equipment."
- **Expected artifacts:** New sources and tests; diffs; green output.
- **Acceptance criteria:** Five presets ship and validate; existing pinned
  appearance behavior unchanged; equipment identity untouched by any new
  stream.
- **Rollback:** Disable preset selection (factory reverts to the three-trait
  variation); catalogs stay as inert data.
- **Blocking decisions:** None at milestone scope (C2 and regional blocks are
  post-milestone).
- **Prohibited scope:** No regional block content; no gold, tattoo, or putong
  components; no per-pawn persistent state (R-W6.3).

### VIS-019 — Full component catalog

- **Goal:** Complete categories A through K: all hair, head covering (C1
  putong, C3 gold-edged putong, C6 feathered headdress), torso garments (D2
  indigo chinina, D3 red chinina, D4 abaca jacket), lower garments (E2 dyed
  bahag, E3 waist cloth), armor layer F1–F5, sashes G1–G3, accessories
  (H1, H2 renderable; H3 flavor), adornment (I1/I2 tattoo tone shift, I4/I5
  gold accents; I3, I6–I8 flavor), condition refinements (R-W3.1, R-W3.6,
  R-W3.7).
- **Dependencies:** VIS-017. Post-milestone.
- **Files:**
  `src/Hukbo.Client/Presentation/Catalogs/AppearanceComponentCatalog.cs`;
  `tests/Hukbo.Client.Tests/AppearanceComponentCatalogTests.cs`.
- **Historical evidence dependency:** Every category A–K option with its
  research tier and must-not-generalize note; pair-form labels only for
  **Putong — Head Wrap**, **Bahag — Loincloth**, **Chinina — Collarless
  Jacket** (R-X.6); pending terms flagged; *salakot* label nonexistent.
- **Determinism classification:** Pure-presentation — data only.
- **Implementation steps:**
  1. Author all remaining component options with metadata; C2 (red putong)
     is deliberately absent pending OD-5.
  2. Resolve inconsistency 4 (adornment renderable count) against the
     research text and record the reading in the doc comment.
  3. Encode render channels: tattoo as tone shift only (fixed test-pinned
     delta), armor widening bounded inside the build-multiplier envelope
     (width factor at most 1.18), accent marks at most two per pawn and at
     most 2 pixels at apparent scale 1 (named constants).
- **Automated verification:** xunit — option counts per category match the
  research; tattoo has no motif channel; armor widening bound pinned;
  accent-count and accent-size caps pinned; label negative tests (no bare
  Filipino term; no salakot string anywhere).
- **Manual visual verification:** None directly; judged through the roster
  rows in VIS-043.
- **Expected artifacts:** Diffs and tests; green output.
- **Acceptance criteria:** Full catalog validates; C2 absent; caps pinned.
- **Rollback:** Revert; the milestone subset remains sufficient for the levy
  block.
- **Blocking decisions:** Inconsistency 4 (resolved within this task against
  the research text). OD-5 is resolved 2026-07-28: C2 stays excluded this
  pass (backlog item in `docs/plans/TODO.md`), so its absence here is the
  decided state, not a wait.
- **Prohibited scope:** No brass/bronze plate, mail, or greaves; no European
  elements; no footwear; no Moro-specific kit; no motif rendering (R-X.8).

### VIS-020 — Visayan preset block

- **Goal:** The twenty Visayan presets (VIS-01 through VIS-20) exactly as
  tabled in the warrior-appearance design, with scope tags, weakest-link
  tiers, loadout restrictions (H1 rows Wasay-only), and fallbacks (R-W3.2).
- **Dependencies:** VIS-018, VIS-019. Post-milestone. Parallelizable with
  VIS-021 and VIS-022 — each block lives in its own file.
- **Files:**
  `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Visayan.cs`
  *(new)*; `tests/Hukbo.Client.Tests/AppearancePresetTests.cs` (extended —
  test file shared across block tasks, so block tasks append in sequence or
  land their assertions in per-block test files if run in parallel:
  `AppearancePresetsVisayanTests.cs` *(new)* is the parallel-safe choice).
- **Historical evidence dependency:** The Visayan block table (tattoo
  coverage I1/I2 Visayan-scoped only; gold C3/I4/I5 on elite rows only; the
  VIS-13/14/15 elite and datu rows; tiers per row).
- **Determinism classification:** Pure-presentation.
- **Implementation steps:**
  1. Author the twenty recipes from the design table as revised per RF-03:
     VIS-18 carries an explicit "prosperous-freeman" scope marker with the
     single I4 accent only, its E2 replaced by E1.
  2. Extend the block-assignment table with the Visayan block.
  3. Assign elite/leader rarity weights as named `PROVISIONAL` constants
     (target at most roughly 2% each, R-W3.14).
  4. Run the full validator and pairwise differentiation over the shipped
     roster so far.
- **Automated verification:** xunit — all twenty pass the validator;
  within-block differentiation holds; tattoo components appear only in this
  block; gold only on elite-, chief-, or leader-marked rows, with the
  single-I4 prosperous-freeman carve-out recognized by the test (VIS-18 as
  revised per RF-03); per-row tier matches the revised design table.
- **Manual visual verification:** VIS-043 rows: "fifty-plus presets read as
  varied but coherent at normal zoom"; "elite figures read as denser in gold
  and dye, not larger."
- **Expected artifacts:** New sources and tests; green output.
- **Acceptance criteria:** Twenty valid presets; roster-wide suites green.
- **Rollback:** Remove the block from the assignment table; factions fall
  back to remaining blocks.
- **Blocking decisions:** None remaining — OD-3 is resolved 2026-07-28 (the
  Unscoped-generic block is the accepted sole Mindanao/Sulu coverage this
  pass); OD-5 is resolved 2026-07-28 (C2 excluded; VIS-R1 reserved, not
  shipped; backlog item in `docs/plans/TODO.md`).
- **Prohibited scope:** No red putong (C2); no preset outside the design
  table; no motif tattoos.

### VIS-021 — Tagalog preset block

- **Goal:** The fifteen Tagalog presets (TAG-01 through TAG-15) as tabled,
  including the chief row TAG-13 (red chinina D3) and veteran armor rows
  (R-W3.2).
- **Dependencies:** VIS-018, VIS-019. Post-milestone. Parallel with VIS-020
  and VIS-022 (own files:
  `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Tagalog.cs`
  *(new)*, `tests/Hukbo.Client.Tests/AppearancePresetsTagalogTests.cs`
  *(new)*).
- **Historical evidence dependency:** The Tagalog block table; the red
  chinina appears only on TAG-13 and no red putong exists, keeping the two
  red status systems apart (prohibition 3).
- **Determinism classification:** Pure-presentation.
- **Implementation steps:** As VIS-020, for the Tagalog table as revised per
  RF-03 (TAG-14 carries the explicit "prosperous-freeman" scope marker with
  the single I4 accent only, its E2 replaced by E1), including the F3 hide
  corselet and F5 shell-set cap components on the veteran rows.
- **Automated verification:** As VIS-020, plus: no tattoo component in this
  block; D3 only on TAG-13 (negative test for prohibition 3); the gold test
  recognizes TAG-14's single-I4 prosperous-freeman carve-out.
- **Manual visual verification:** Shared roster rows in VIS-043.
- **Expected artifacts:** New sources and tests; green output.
- **Acceptance criteria:** Fifteen valid presets; prohibition-3 negatives
  green.
- **Rollback:** As VIS-020.
- **Blocking decisions:** None beyond inherited roster decisions.
- **Prohibited scope:** No tattoos on Tagalog presets; no red putong.

### VIS-022 — Northern Luzon block, remaining levy presets, and faction block assignment

- **Goal:** The eight Cagayan-scoped presets (LUZ-01 through LUZ-08), the
  five remaining levy presets (LEV-05 through LEV-08, LEV-10), and the full
  faction block-assignment table completing R-W3.2 (53 presets total) and
  R-W3.5.
- **Dependencies:** VIS-018, VIS-019. Post-milestone. Parallel with VIS-020
  and VIS-021 (own files:
  `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.NorthernLuzon.cs`
  *(new)*, `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Levy.cs`
  (extended — coordinated with no other concurrent writer),
  `tests/Hukbo.Client.Tests/AppearancePresetsLuzonTests.cs` *(new)*).
- **Historical evidence dependency:** The Northern Luzon table (Boxer Codex
  Cagayan and Zambal silhouettes only; C6 feathered headdress exists only
  here; no tattoo tone, no putong, no gold); levy rows as tabled.
- **Determinism classification:** Pure-presentation. Block assignment takes
  `FactionId` as a documented additional input (integration design
  section 3).
- **Implementation steps:**
  1. Author the thirteen remaining recipes.
  2. Complete the block-assignment table: one region per faction per match,
     drawn from `Scenario.Seed`; optional levy blending at a fixed
     `PROVISIONAL` ratio; record the same-block-allowed default (design open
     decision) for user review.
  3. Re-run roster-wide validator, differentiation, count floor, and pool
     totality (every block/loadout pair resolves).
- **Automated verification:** xunit — C6 only in this block (prohibition 1
  negative); roster count at least 50 (R-W3.2 pin at 53); block assignment
  stability per (seed, faction); pool totality across all four blocks and
  all loadouts.
- **Manual visual verification:** Shared roster rows in VIS-043; "at minimum
  zoom, faction and weapon role remain the dominant reads."
- **Expected artifacts:** New sources and tests; green output.
- **Acceptance criteria:** 53 presets validate; assignment table complete.
- **Rollback:** Remove blocks from the table; levy-only assignment remains a
  working configuration.
- **Blocking decisions:** The same-block-versus-distinct-block product call
  (warrior design open decision; recommended default: same block allowed).
  OD-3 is resolved 2026-07-28: the Unscoped-generic block is the accepted
  sole Mindanao/Sulu coverage this pass, so no Mindanao/Sulu preset waits on
  it.
- **Prohibited scope:** No Mindanao- or Sulu-flavored preset (OD-3); no
  feathered headdress outside this block.

### VIS-023 — Appearance render layers: armor, sash, and adornment accents

- **Goal:** The three new pawn layers from the integration design's table —
  layer 4 armor capsule thickening and material tone, layer 5 sash line,
  layer 9 adornment accents — drawn at their tiers (armor tone Low+,
  silhouette Medium+; sash Medium+; accents High), within the torso/head
  footprint, never occluding the protected Low-tier set (R-W3.6, R-W3.13,
  R-X.1, R-X.2).
- **Dependencies:** VIS-009 (layer slots), VIS-019. Post-milestone.
- **Files:** `src/Hukbo.Client/Rendering/PawnGeometry.cs` (layer layout),
  `src/Hukbo.Client/Rendering/PawnRenderer.cs` (fills),
  `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` (extended).
- **Historical evidence dependency:** Component render-channel definitions
  from VIS-019 (armor forms F2–F5, sash G1–G3, accents C3/I4/I5/E2).
- **Determinism classification:** Pure-presentation — time-invariant layers;
  they read no swing phase, effect clock, or accumulator (integration design
  section 6 rule 1); hit-pulse blending routes through the existing single
  blend point so a pulsing pawn pulses as one object.
- **Implementation steps:**
  1. Compute armor capsule widening in the layout (bounded at 1.18 width
     factor), sash line endpoints, and accent positions from `PawnLayout`
     anchors.
  2. Fill the layers in the documented order (4, 5, 9) in the renderer.
  3. Route all new color through the existing hit-pulse blend.
  4. Keep `GetBounds` pose-blind; widening grows the pose-independent bound
     only.
- **Automated verification:** xunit — layer geometry pinned; armor widening
  bound; accents at most two and at most 2 px at scale 1; tier gates at
  exact thresholds; non-occlusion of ring/shield/weapon/status at Low;
  bounds independence.
- **Manual visual verification:** VIS-043 rows: "armored figures read as
  bulkier, not as shielded"; "accents visible at maximum zoom without
  breaking any read."
- **Expected artifacts:** Diffs, extended tests, green output.
- **Acceptance criteria:** All layer tests green; hit pulse blends uniformly.
- **Rollback:** No-op the three layer slots; presets still select and their
  tone channels still render.
- **Blocking decisions:** None.
- **Prohibited scope:** No time-varying appearance; no layer reading
  animation state; no faction color on garments.

### VIS-024 — Appearance inspector surface

- **Goal:** Selecting a pawn shows preset name (plain-English descriptor),
  scope tag, preset evidence tier, per-component tier list with
  must-not-generalize notes, pending-verification flags, and non-renderable
  flavor lines (R-W3.3, R-W3.12, R-W3.15, R-X.7).
- **Dependencies:** VIS-020, VIS-021, VIS-022, VIS-016 (shared inspector
  files — sequenced after). Post-milestone.
- **Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs`,
  `src/Hukbo.Client/UI/AgentInspectorPanel.cs`;
  `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`.
- **Historical evidence dependency:** All preset and component metadata from
  the roster tasks; pair-form labels only for the three cleared terms.
- **Determinism classification:** Pure-presentation — precomposed text.
- **Implementation steps:**
  1. Extend the content builder with the appearance block.
  2. Precompose all strings at catalog construction.
  3. Extend the forbidden/pending-term negative suite to appearance terms
     (*barote*, *kandit*, *panika*, *kamagi*, *batuk*, *kolombiga* flagged
     pending in metadata only; *salakot* absent everywhere).
- **Automated verification:** xunit — content correctness across blocks;
  negative suites; scope tag always shown for cultural presets.
- **Manual visual verification:** VIS-043 row: "Inspector shows preset name,
  scope tag, tier, and component notes for any selected pawn."
- **Expected artifacts:** Diffs, tests, green output.
- **Acceptance criteria:** Every preset discoverable; negatives green.
- **Rollback:** Revert; appearance renders without inspector detail.
- **Blocking decisions:** None remaining — OD-2-class pending-term handling
  is resolved 2026-07-28: flagged inspector metadata only.
- **Prohibited scope:** No bare Filipino term player-facing; no "ancient
  Philippines" phrasing (R-X.10).

## Category 6 — Battlefield ground and grass

### VIS-025 — Grass cluster generation **[MILESTONE]**

- **Goal:** Deterministic two-level cluster placement (centers, then tufts
  with square-root radial falloff) from `SplitMix64(Scenario.Seed ^ new
  grass salt)`, generated only at scenario creation and reset, with caps,
  phase assignment, border margin, and map clipping (R-W4.3, R-W4.4, R-W4.6,
  R-W5.3).
- **Dependencies:** VIS-001, VIS-002 (the backdrop metadata entry reuses the
  catalog entry shape).
- **Files:** `src/Hukbo.Client/Rendering/GrassGeometry.cs` *(new — placement,
  cluster data, caps)*;
  `src/Hukbo.Client/Presentation/Catalogs/BackdropVisualCatalog.cs` *(new —
  the minimal backdrop metadata declaration)*; `src/Hukbo.Client/ArenaGame.cs`
  (generation wiring at the two existing backdrop-generation sites);
  `tests/Hukbo.Client.Tests/GrassGeometryTests.cs` *(new)*.
- **Historical evidence dependency:**
  `docs/research/improve-visuals/battlefield-environment-research.md` —
  technique evidence only; the ground depicts generic open ground labelled
  **Provisional reconstruction** in metadata; no player-facing text names
  vegetation, region, or land use (R-W4.9).
- **Determinism classification:** Pure-presentation — pure function of
  `Scenario.Seed`; regenerated identically at reset; nothing stored beyond
  the generated flat array (allocated twice per match, never per frame).
- **Implementation steps:**
  1. Implement generation: cluster-center count scaled by map area within
     the named 24–48 range, hard cap 320 clusters, at most 4 quads per
     cluster; per-cluster phase, size class, and tuft layout drawn from the
     same stream (~16 bytes per entry, flat array).
  2. Enforce the one-cell (64 world units) grass-free border margin and map
     rectangle clipping in placement.
  3. Wire generation at scenario construction and reset only.
  4. Register and use the new grass salt (never the plains salt); pin that
     existing decal placement is unchanged.
  5. Give R-W4.9's labelling obligation a real artifact (the RF-10
     resolution): a minimal one-entry backdrop metadata declaration in the
     `backdrop.*` identifier domain, reusing the VIS-002 catalog entry
     shape, with evidence tier **Provisional reconstruction** — the home for
     the ground's provisional framing, with no player-facing text naming
     vegetation, region, or land use.
- **Automated verification:** xunit — same seed yields identical positions,
  phases, counts; caps pinned as named constants; density scales with map
  area only (two map sizes compared; agent count varied with no effect);
  border margin and clipping geometry; existing `PlainsBackdropGeometryTests`
  untouched and green.
- **Manual visual verification:** Covered by VIS-041 ground rows.
- **Expected artifacts:** New sources and tests; diffs; green output.
- **Acceptance criteria:** Deterministic placement by test; caps named and
  pinned; decals unshifted.
- **Rollback:** Skip generation (empty cluster array); the backdrop is
  exactly today's.
- **Blocking decisions:** None (the grass salt is unambiguous; inconsistency
  3 concerns only the ground-shading lattice in VIS-027).
- **Prohibited scope:** No per-blade objects (R-X.14); no generation per
  tick or frame; no density coupling to agent count or frame rate.

### VIS-026 — Grass cluster rendering with zoom LOD **[MILESTONE]**

- **Goal:** Draw visible clusters as two-to-four tinted quads inside the
  existing arena batch, shaded at or below the 0.22 lerp ceiling, with the
  three zoom bands (far: static single rectangle, no sway; mid: full with
  sway; near: full with sway and optional extra silhouette quad) and minimal
  shade spread under the high-contrast theme (R-W4.2, R-W4.5, R-W5.6,
  R-W5.7 shade half).
- **Dependencies:** VIS-025.
- **Files:** `src/Hukbo.Client/Rendering/GrassGeometry.cs` (quad layout and
  band selection — pure); `src/Hukbo.Client/Rendering/GrassRenderer.cs`
  *(new — draw-only sink)*; `src/Hukbo.Client/ArenaGame.Rendering.cs` (one
  draw call site between ground grid and pawns);
  `tests/Hukbo.Client.Tests/GrassGeometryTests.cs` (extended).
- **Historical evidence dependency:** As VIS-025 (Provisional reconstruction
  framing; no naming).
- **Determinism classification:** Pure-presentation — quad layout is a pure
  function of cluster data, camera, theme, and the sway offset input
  (supplied by VIS-030/VIS-031; zero until those land).
- **Implementation steps:**
  1. Implement per-cluster quad layout and the zoom band selection with
     named threshold constants (~0.3 and ~2.0, exact values pinned).
  2. Shade via `Color.Lerp(ArenaSurface, ArenaBorder, t)` with t at or below
     0.22; minimal spread under high-contrast.
  3. Implement the linear screen-bounds cull against the arena panel (the
     `DrawDecals` pattern; no spatial index).
  4. Renderer loops the tested formula; zero allocation in the loop; draws
     from the 1x1 texture inside the existing Begin/End pair.
- **Automated verification:** xunit — shade-ceiling pins for every band and
  theme; band selection at exact thresholds; far band emits one static
  rectangle with zero sway input consumed; cull correctness on synthetic
  bounds; per-cluster quad count at most 4.
- **Manual visual verification:** VIS-041 rows: "ground reads as living
  grassland, not checkerboard, at all zooms"; "the arena border remains the
  strongest line on the field."
- **Expected artifacts:** New renderer, diffs, extended tests, green output.
- **Acceptance criteria:** Zero new Begin/End pairs and zero new textures
  (asserted by code review against the diff); all pins green.
- **Rollback:** Remove the draw call site; generation data is inert.
- **Blocking decisions:** None.
- **Prohibited scope:** No shader, no `SpriteSortMode.Immediate`, no new
  texture, no formula in the renderer.

### VIS-027 — Correlated ground shading

- **Goal:** Replace the independent per-cell shade hash with four-corner
  lattice hashing averaged per cell — large tonal drifts instead of per-cell
  confetti — still a pure function of (column, row, seed), still inside the
  existing shade ladder and 0.22 ceiling (R-W4.1, R-W4.2).
- **Dependencies:** VIS-001 (salt decision), VIS-026 (visual coherence with
  grass judged together). Post-milestone.
- **Files:** `src/Hukbo.Client/Rendering/PlainsBackdropGeometry.cs`,
  `src/Hukbo.Client/Rendering/PlainsBackdropRenderer.cs` (formula call site
  only), `tests/Hukbo.Client.Tests/PlainsBackdropGeometryTests.cs`.
- **Historical evidence dependency:** Provisional reconstruction framing
  only; expressly no hard-coded cogon palette (OD-6, resolved 2026-07-28,
  approves the cogon olive-gold shift as a separate provisional-tagged
  theme-tuning change, never a renderer palette).
- **Determinism classification:** Pure-presentation — pure seeded function;
  camera-independent; zero allocation via the single tested formula.
- **Implementation steps:**
  1. Resolve inconsistency 3: implement the corner lattice under the salt
     the user chose (proposed default: a new named salt).
  2. Implement corner hashing and averaging; keep the shade ladder and
     `Color.Lerp` derivation.
  3. Update the shade pin tests to the new formula; pin decal placement
     unchanged.
- **Automated verification:** xunit — same (column, row, seed) yields the
  same shade; corner-averaging formula pinned on known values; ceiling pins;
  decal placement unchanged; renderer still calls the single formula (no
  duplication).
- **Manual visual verification:** VIS-043 row: "ground reads as living
  grassland, not checkerboard, at all zooms" (re-judged after this task).
- **Expected artifacts:** Diffs, updated tests, green output.
- **Acceptance criteria:** Formula pinned; ceiling held; four hashes per
  cell bounded by the unchanged 48x48 grid cap.
- **Rollback:** Revert to the per-cell hash; both formulas are pure and the
  ladder is unchanged.
- **Blocking decisions:** Inconsistency 3 (salt choice; proposed default a
  new named salt). OD-6, resolved 2026-07-28, stays explicitly out of scope
  here — the approved cogon olive-gold shift is a theme-tuning change
  carried out on its own.
- **Prohibited scope:** No new theme roles; no authored ground texture; no
  change to the grid cap or cell size.

### VIS-028 — Trample marks

- **Goal:** A fixed-capacity (128, named constant), oldest-replaced,
  client-only mark list fed by authoritative `Death` events, drawn as darker
  flattened ellipses under grass within the 0.22 ceiling; clusters within
  the trample radius draw reduced with zero sway; resets with the scenario
  (R-W4.7).
- **Dependencies:** VIS-025, VIS-026, VIS-030 (suppression consumes the sway
  amplitude input). Post-milestone.
- **Files:** `src/Hukbo.Client/Presentation/TrampleMarkSystem.cs` *(new —
  the `HitEffectSystem` lifecycle shape: `IngestTick`, fixed pool, reset)*;
  `src/Hukbo.Client/Rendering/GrassGeometry.cs` (suppression distance test —
  pure); `src/Hukbo.Client/Rendering/GrassRenderer.cs` (mark draw);
  `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` (ownership and
  ingest wiring); `tests/Hukbo.Client.Tests/TrampleMarkSystemTests.cs`
  *(new)*.
- **Historical evidence dependency:** None (battle-history visualization, no
  historical claim).
- **Determinism classification:** Presentation-state — a bounded pool fed by
  authoritative events, client-only, reset with the scenario, never
  persisted, never snapshotted, never read by the simulation.
- **Implementation steps:**
  1. Implement the pool keyed on `Death` events (position from the event's
     agent); `Move` never spawns anything.
  2. Implement the pure suppression distance check consumed by grass layout
     (reduced height, zero sway inside the radius).
  3. Draw marks under grass within the ceiling; reset with scenario.
  4. Default `Death`-only feed; the `Attack`-throttle option is recorded,
     not implemented (design open decision, default declined).
- **Automated verification:** xunit — capacity and oldest-replacement;
  `Death` adds a mark, `Move` never; suppression distance; reset clears;
  shade ceiling pin.
- **Manual visual verification:** VIS-043 row: "trampled areas visibly thin
  where fighting happened."
- **Expected artifacts:** New system and tests; diffs; green output.
- **Acceptance criteria:** All lifecycle tests green; zero allocation after
  construction.
- **Rollback:** Do not wire ingest; the pool stays empty and grass renders
  unsuppressed.
- **Blocking decisions:** The `Attack`-feed option (default: `Death` only).
- **Prohibited scope:** No persistence; no feedback into anything; no
  unbounded growth.

### VIS-029 — Dust puffs *(optional per OD-9, resolved 2026-07-28)*

- **Goal:** Event-driven dust in the `HitEffectSystem` shape: `Death` spawns
  a brief puff, `Attack` may spawn a throttled kick, `Move` never spawns,
  `Outcome` stops new spawns; sub-second lifetimes; 32 live puffs (named
  constant); ground-shade colors under the ceiling (R-W4.8, now MAY per
  OD-9).
- **Dependencies:** VIS-026. Post-milestone. Unblocked by the 2026-07-28
  resolution of OD-9 (inconsistency 1), and **optional**: R-W4.8 is amended
  to MAY, so this task ships only if the schedule allows.
- **Files:** `src/Hukbo.Client/Presentation/DustEffectSystem.cs` *(new)*;
  `src/Hukbo.Client/Rendering/DustGeometry.cs` *(new)*;
  `src/Hukbo.Client/Rendering/GrassRenderer.cs` or a small
  `DustRenderer.cs` *(new)*;
  `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` (wiring);
  `tests/Hukbo.Client.Tests/DustEffectSystemTests.cs` *(new)*.
- **Historical evidence dependency:** None (no historical claim).
- **Determinism classification:** Presentation-state — fixed pool on
  unscaled presentation seconds ("wounds already dealt" clock class), reset
  with scenario.
- **Implementation steps:**
  1. Implement the pool and lifecycle (expand-and-fade one-to-two
     rectangles, sub-second, unscaled clock).
  2. Event mapping per the goal; `Outcome` gates new spawns off.
  3. Shade within the ground range and ceiling.
- **Automated verification:** xunit — lifecycle, cap, event mapping (`Death`
  spawns, `Move` never, `Outcome` stops), ceiling pin, reset behavior.
- **Manual visual verification:** VIS-043 row: "dust reads as impact
  punctuation, not weather" (wording finalized when unblocked).
- **Expected artifacts:** New system, geometry, tests; green output.
- **Acceptance criteria:** All lifecycle tests green; cap pinned.
- **Rollback:** Unwire ingest; no dust, nothing else affected.
- **Blocking decisions:** None remaining — **OD-9 (inconsistency 1) is
  resolved 2026-07-28: R-W4.8 is amended to MAY, so this task is unblocked
  but optional.** If dust ships, the decided behavior applies:
  `MotionIntensity` Off suppresses dust spawning, Reduced leaves dust
  unchanged, and VIS-031's amplitude-resolution truth table gains the
  corresponding dust row in the same diff.
- **Prohibited scope:** No per-`Move` effects; no playback-speed scaling on
  the dust clock; no new events in Core.

## Category 7 — Wind and environmental motion

### VIS-030 — Grass sway helper and motion clock **[MILESTONE]**

- **Goal:** The pure sway function `GrassSwayOffset(timeSeconds, phase,
  amplitudeScale)` returning a `Vector2` — sub-1 Hz, at most 1–2 screen
  pixels at zoom 1, exact `Vector2.Zero` at amplitude 0 — plus the
  client-side frame-time accumulator that drives it (R-W5.1, R-W5.2, R-W5.4,
  R-W5.5).
- **Dependencies:** VIS-025 (phase source). Milestone.
- **Files:** `src/Hukbo.Client/Rendering/GrassSway.cs` *(new — pure math)*;
  `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` (the
  accumulator, advanced in the `AdvanceEffects` pattern, not scaled by
  playback speed); `tests/Hukbo.Client.Tests/GrassSwayTests.cs` *(new)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Presentation-state (the accumulator)
  driving pure-presentation math. The clock never touches the simulation; no
  simulation value depends on it; nothing it computes is stored, hashed, or
  snapshotted.
- **Implementation steps:**
  1. Implement the oscillator (sine or triangle — pick one and pin it by
     test so the formula never drifts; the battlefield design leaves the
     choice to implementation with a pinned outcome).
  2. Declare amplitude and frequency bounds as named `PROVISIONAL`
     constants.
  3. Guarantee the exact-zero path: amplitude 0 returns `Vector2.Zero` with
     no trigonometric evaluation.
  4. Add the accumulator to the coordinator on unscaled frame seconds
     (ambience does not scale with playback speed — the clock-scaling rule).
  5. Clip swayed positions so no tuft crosses the border at maximum
     amplitude (pure geometry, with VIS-025's clip).
- **Automated verification:** xunit — exact values pinned at chosen times;
  amplitude bound at most 2 px at zoom 1; frequency below 1 Hz; exact
  `Vector2.Zero` at zero; phase determinism from the generation stream;
  border clip at maximum amplitude.
- **Manual visual verification:** VIS-041 rows: "sway reads as alive, not as
  noise" (under load); "no motion visible at minimum zoom."
- **Expected artifacts:** New sources and tests; green output.
- **Acceptance criteria:** All pins green; the off path is bit-identical to
  a static backdrop by construction.
- **Rollback:** Feed amplitude 0 permanently; static grass remains.
- **Blocking decisions:** Wave-shape choice (implementation pick, pinned).
- **Prohibited scope:** No wall-clock input; no playback-speed scaling; no
  per-blade computation; no allocation in the per-frame path.

### VIS-031 — Effective amplitude resolution **[MILESTONE]**

- **Goal:** One pure function combining every gate on sway into the single
  amplitude factor the renderer consumes: the reduced-motion setting (Off 0,
  Reduced one-half, Full 1), the high-contrast theme forcing 0, the far zoom
  band forcing 0, and trample suppression forcing 0 per cluster (R-W5.6,
  R-W5.7, R-W5.8, R-W5.9).
- **Dependencies:** VIS-030, VIS-032 (setting value), VIS-026 (band).
  Trample input defaults to none until VIS-028 lands. Milestone.
- **Files:** `src/Hukbo.Client/Rendering/GrassSway.cs` (the resolution
  function); `src/Hukbo.Client/Rendering/GrassRenderer.cs` (consumption);
  `tests/Hukbo.Client.Tests/GrassSwayTests.cs` (extended).
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — a pure function of
  setting value, theme identity, zoom band, and suppression flag.
- **Implementation steps:**
  1. Implement `ResolveAmplitudeFactor(setting, isHighContrastTheme,
     zoomBand, isSuppressed)` with the precedence: any zeroing input wins.
  2. Wire it as the single amplitude source in the grass draw path.
- **Automated verification:** xunit — truth-table tests over all input
  combinations; high-contrast forces 0 regardless of setting; far band
  forces 0; suppression forces 0; Reduced yields exactly one-half.
- **Manual visual verification:** VIS-041 row: "the high-contrast theme
  shows zero motion"; setting operability row in VIS-032's selector.
- **Expected artifacts:** Diffs and tests; green output.
- **Acceptance criteria:** Truth table green; single consumption point.
- **Rollback:** Hard-code factor 0; motion disabled everywhere.
- **Blocking decisions:** None remaining — OD-8 is resolved 2026-07-28: the
  MotionIntensity setting governs all ambient presentation motion (grass
  sway now, dust and future ambient motion included; gameplay-communicating
  motion exempt); the wording and documented scope follow that resolution,
  and the function's shape is unchanged. OD-9 is resolved 2026-07-28: dust
  is optional; if it ships, this task's truth table gains the decided dust
  row (Off suppresses dust spawning, Reduced leaves dust unchanged) via
  VIS-029's diff.
- **Prohibited scope:** No second amplitude path; no gameplay-communicating
  motion (swings, hit effects) routed through this factor.

## Category 8 — Accessibility and settings

### VIS-032 — Reduced-motion setting chain **[MILESTONE]**

- **Goal:** The new visual setting end to end on the GoreIntensity
  precedent: pinned enum (working name `MotionIntensity`: Off = 0,
  Reduced = 1, Full = 2, do-not-renumber comment), nullable
  `RawClientSettings` field with independent validation, schema version bump
  3 to 4 with backward-compatible load, manager with injected persist
  delegate, menu selector, and full test coverage (R-W6.6, R-W6.7, R-W6.8).
- **Dependencies:** None (independent chain; consumed by VIS-031). Milestone.
- **Files:** `src/Hukbo.Client/Settings/MotionIntensity.cs` *(new)*;
  `src/Hukbo.Client/Settings/MotionIntensityManager.cs` *(new)*;
  `src/Hukbo.Client/Settings/ClientSettings.cs`;
  `src/Hukbo.Client/Settings/ClientSettingsStore.cs` (schema 4, raw field,
  validation); `src/Hukbo.Client/UI/MotionIntensitySelector.cs` *(new)*;
  menu wiring in the existing menu overlay call sites;
  `tests/Hukbo.Client.Tests/MotionIntensityManagerTests.cs` *(new)*,
  `tests/Hukbo.Client.Tests/MotionIntensitySelectorTests.cs` *(new)*,
  `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs` (extended).
- **Historical evidence dependency:** None.
- **Determinism classification:** Presentation-state — persisted client
  settings; never read by the simulation.
- **Implementation steps:**
  1. Copy the GoreIntensity chain end to end with the new enum; default
     Full.
  2. Bump `ClientSettingsStore` schema to 4, and change the load path's
     schema check from strict equality (the current
     `SchemaVersion: SupportedSchemaVersion` pattern match, which rejects
     the whole file on any other version) to an accepted-version set
     {3, 4} with an explicit version-3 migration path — this is the one
     place the GoreIntensity precedent does not transfer, because that
     precedent added a field within a schema version while this task
     changes the version itself for the first time. Version-3 files load
     with the new field defaulting; a corrupt field resolves to default
     without losing the saved theme or any other field. The planned
     migration test covers the accepted-version set.
  3. Add the selector to the menu beside the gore selector; persist on
     change; no rollback on save failure; all load/save/failure paths log on
     the `settings` channel as today.
- **Automated verification:** xunit — manager behavior with a fake persist
  delegate; selector interaction tests in the existing selector pattern;
  store round-trip at version 4; version-3 migration test; corrupt-field
  test preserving other fields; enum numeric pins.
- **Manual visual verification:** VIS-041 row: "the motion setting is
  operable from the menu and visibly gates grass sway Off/Reduced/Full."
- **Expected artifacts:** New sources and tests; diffs; green output.
- **Acceptance criteria:** Full chain tests green; schema 4 persists and
  reloads; version-3 file loads clean.
- **Rollback:** Revert to schema-3 code. Stated honestly: the schema-3 load
  path rejects any file whose version is not exactly 3, so a version-4 file
  already on disk is discarded to defaults — the saved theme and other
  settings are lost once, the player re-selects them, and the next save
  rewrites a version-3 file. There is no migration in the rollback
  direction; this one-time settings loss is the accepted cost of reverting.
- **Blocking decisions:** None remaining — OD-8 is resolved 2026-07-28: the
  MotionIntensity setting governs all ambient presentation motion (grass
  sway now, dust and future ambient motion included), and the selector's
  caption and docs are worded to that scope; the plumbing is unchanged.
- **Prohibited scope:** No effect on swings or hit effects (gameplay
  communication stays exempt under the resolved OD-8 scope); no second
  settings file.

### VIS-033 — Color-blind no-regression guard and theme contrast continuity

- **Goal:** The enforcement of R-W6.10 and R-W6.11: faction stays
  distinguishable by the ground-ring shape-and-position channel; no new
  variant makes garment or ground hues a competing faction signal; theme
  contrast-pair validation keeps passing for all five themes.
- **Dependencies:** VIS-005, VIS-017 (dye constants), VIS-026 (ground
  shades). Post-milestone.
- **Files:** `tests/Hukbo.Client.Tests/FactionSignalGuardTests.cs` *(new)*;
  `src/Hukbo.Client/Presentation/Catalogs/AppearancePresetValidator.cs`
  (the faction-color-stays-on-the-ring rule as a validator check);
  `tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs` (re-run confirmation —
  no edit expected).
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — tests and a validator
  rule over static data.
- **Implementation steps:**
  1. Add the validator check: no garment, tint, skin tone, or ground shade
     within the minimum distance of any faction constant (VIS-005 helper).
  2. Sweep every shipped catalog color through the check in one test.
  3. Confirm `UiThemeCatalog` contrast validation passes unchanged (the
     package adds no theme roles, so this is a recorded confirmation run).
- **Automated verification:** xunit — the sweep test over all catalogs; a
  deliberately faction-colored synthetic entry fails; theme validation suite
  green.
- **Manual visual verification:** VIS-043 row: "with 200+ pawns, faction
  remains readable by ring shape and position when hue is disregarded"
  (honest wording: a human with typical color vision judges the shape
  channel; true color-blind verification is out of this pass's scope and
  OD-7 tracks the stronger marker).
- **Expected artifacts:** New test file; validator diff; green output.
- **Acceptance criteria:** Sweep green over the full shipped color set.
- **Rollback:** Tests only; removing them restores no risk mechanically but
  drops the guard — revert not expected.
- **Blocking decisions:** None remaining — OD-7 is resolved 2026-07-28:
  the shape-redundant faction marker is deferred (backlog item in
  `docs/plans/TODO.md`); this task holds the no-regression floor only, as
  planned.
- **Prohibited scope:** No change to the fixed faction constants; no new
  theme roles.

## Category 9 — Performance measurement

### VIS-034 — Submission-counting seam **[MILESTONE]**

- **Goal:** A GPU-independent primitive-count function over the layout
  types (per-pawn per tier, per grass cluster, per backdrop element) so
  xunit pins exact submission counts and catches creep on every gate run —
  the enforcement instrument for the cap constants (integration design
  sections 8 and 11; R-W6.14).
- **Dependencies:** VIS-010, VIS-013, VIS-018, VIS-026 (counts what the
  milestone draws; extended by later feature tasks in their own test
  files). Milestone.
- **Files:** `src/Hukbo.Client/Rendering/SubmissionCount.cs` *(new — pure
  counting functions)*; `tests/Hukbo.Client.Tests/SubmissionCountTests.cs`
  *(new)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — pure functions over
  layout values; never called in the render loop.
- **Implementation steps:**
  1. Implement counting over `PawnLayout` per detail tier and state
     (matching the renderer's documented draw order), and over grass
     cluster layouts and backdrop elements.
  2. Pin the current per-pawn worst-case counts per tier and the milestone
     grass worst case as named expected values.
  3. Document the rule: any task that adds a primitive must update the pin
     deliberately, in the same diff, with the budget arithmetic in the
     commit message.
- **Automated verification:** xunit — pinned per-pawn-per-tier counts;
  pinned per-cluster counts; whole-frame worst-case arithmetic asserted
  against the ESTIMATE budget constants (12,000 at 200 units, 20,000 at
  500 — flagged ESTIMATE in the constant names until VIS-036).
- **Manual visual verification:** None.
- **Expected artifacts:** New sources and tests; green output.
- **Acceptance criteria:** Counts pinned and passing; creep now fails a
  test instead of drifting.
- **Rollback:** Tests only; not expected.
- **Blocking decisions:** None.
- **Prohibited scope:** No counting code in the per-frame render path; no
  silent pin edits (the anti-density-creep rule applies to the pins).

### VIS-035 — Render measurement harness and baseline recording **[MILESTONE]**

- **Goal:** The hand-run harness `tools/Hukbo.Tools.RenderProbe` — outside
  `Hukbo.slnx`, outside the gate — recording frame time p50/p95/p99, arena
  sprite submissions, GC counts and allocated-bytes delta, and the
  configuration fingerprint, as JSON under `artifacts/`; plus a recorded
  **pre-integration baseline** on the current build so every later
  measurement has something to regress against (R-W6.12).
- **Dependencies:** None to build; the baseline run must execute on a build
  **without** the package's visual changes (ideally before milestone
  integration, from the pre-package commit). Milestone.
- **Files:** `tools/Hukbo.Tools.RenderProbe/` *(new project — not added to
  `Hukbo.slnx`)*; a minimal opt-in instrumentation seam in
  `src/Hukbo.Client/ArenaGame.cs` / `ArenaGame.Rendering.cs` (debug-time
  frame-timing hook, absent from the Release render path's cost — the exact
  seam is this task's one open implementation decision, constrained by the
  integration design).
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation tooling — the probe
  observes; it must not alter simulation outcomes (the gate re-verifies the
  seed-1 hashes regardless).
- **Implementation steps:**
  1. Build the probe in the mold of the existing hand-run harnesses
     (`Hukbo.Tools.CueDemand` and siblings): launch the real client against
     a scripted scenario seed, drive the three camera stations (minimum
     zoom, default fit, maximum zoom), record over a fixed frame count.
  2. Implement the instrumentation seam under a debug-time opt-in flag;
     verify by inspection and by the gate that Release cost is unaffected.
  3. Run the baseline on the pre-package build at 1080p on named hardware:
     {200, 500 units} at the three stations; write
     `artifacts/render-baseline-<date>.json`. Because the probe and the
     opt-in seam are themselves package deliverables, the baseline is taken
     on a branch cut from the pre-package commit that carries only the
     probe and the opt-in instrumentation seam and nothing else; that
     branch's diff is recorded alongside the baseline JSON as part of the
     evidence.
- **Automated verification:** The probe project builds; a smoke assertion
  that its report schema round-trips. The probe itself is not in the gate
  (R-W6.12 keeps it hand-run).
- **Manual visual verification:** None (numbers, not looks). Running the
  probe requires a person at an interactive desktop; if no desktop session
  is available the baseline is reported **BLOCKED**, honestly, not faked.
- **Expected artifacts:** The probe project; the baseline JSON under
  `artifacts/`, cited by filename; the recorded diff of the baseline branch
  (pre-package commit plus probe and seam only).
- **Acceptance criteria:** Baseline file exists with fingerprint, frame
  percentiles, submission counts, and GC deltas; probe absent from
  `Hukbo.slnx` and the gate.
- **Rollback:** Delete the tool; remove the seam (a no-op flag in Release
  already).
- **Blocking decisions:** The instrumentation-seam design (implementer's
  pick under the stated constraint).
- **Prohibited scope:** Not in the solution, not in the gate, no CI, no
  network; no measurement code active in Release by default.

### VIS-036 — Full-matrix measurement and budget reconciliation

- **Goal:** Run the full matrix — {200, 500 visible units} x {minimum zoom
  0.05, default fit, maximum zoom 12} x {grass on, off} x {motion on, off}
  at 1080p on named hardware — and reconcile every ESTIMATE budget: confirm
  it, or revise it through recorded review, never silently (R-W6.13,
  R-W6.14).
- **Dependencies:** VIS-035, and all rendering feature tasks that ship
  (VIS-011, VIS-014, VIS-015, VIS-023, VIS-026, VIS-027, VIS-028, VIS-030,
  VIS-031; VIS-029 if unblocked). Post-milestone, near the end.
- **Files:** `tools/Hukbo.Tools.RenderProbe/` (matrix driver);
  `docs/development/testing.md` (measurement results section);
  `tests/Hukbo.Client.Tests/SubmissionCountTests.cs` (budget constants
  updated from ESTIMATE to measured, if revised).
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation tooling.
- **Implementation steps:**
  1. Run the sixteen-cell matrix per unit count; write the report JSON.
  2. Assert the sanity rule: grass-off and motion-off cells measure less
     than or equal to their on counterparts — a paradoxical cost is a
     defect to investigate, not record.
  3. Compare against the budgets; where a budget moves, record the revision
     and its reasoning in the testing document and update the named
     constants in the same reviewed diff.
- **Automated verification:** The updated budget pins in
  `SubmissionCountTests` pass; the gate stays green.
- **Manual visual verification:** None; the run itself is the hand
  procedure. BLOCKED-honest if no desktop.
- **Expected artifacts:** `artifacts/render-matrix-<date>.json`; the
  testing-document results section; any budget-revision diff.
- **Acceptance criteria:** A full-matrix report exists on named hardware;
  every budget is either confirmed or visibly revised; no ESTIMATE label
  remains on a measured number.
- **Rollback:** Not applicable (evidence, not behavior).
- **Blocking decisions:** None.
- **Prohibited scope:** No silent budget rewrites; no CI-hosted benchmark.

## Category 10 — Automated tests

Per-feature tests live inside their feature tasks above. This category holds
the cross-cutting suites that bind the package together.

### VIS-037 — Catalog and infrastructure test suite **[MILESTONE]**

- **Goal:** The shared contract suite over everything the milestone ships:
  exact identifier-string pins for every shipped entry (a reword fails),
  index contiguity and uniqueness across catalogs, metadata presence
  (evidence tier everywhere; scope tag on cultural entries), salt-registry
  distinctness (extending VIS-001), fallback totality per domain, and the
  `LogEvents` hygiene suites extended to the three new constants (R-W6.1,
  R-W6.2, R-W6.4, R-W6.5).
- **Dependencies:** VIS-001 through VIS-005, VIS-010, VIS-013, VIS-017,
  VIS-018. Milestone.
- **Files:** `tests/Hukbo.Client.Tests/VisualCatalogContractTests.cs`
  *(new)*; extensions to the existing hygiene test files where the suites
  already live.
- **Historical evidence dependency:** None (it enforces the metadata's
  presence, not its content).
- **Determinism classification:** Pure-presentation — tests only.
- **Implementation steps:**
  1. Write the pinned-ID table for every shipped entry.
  2. Sweep all catalogs through uniqueness, contiguity, and metadata
     checks.
  3. Extend the hygiene suites to the new `LogEvents` constants.
- **Automated verification:** This task is verification; it is green when
  the suite passes over the milestone content and is extended by each
  post-milestone content task in its own diff.
- **Manual visual verification:** None.
- **Expected artifacts:** The suite file; green output.
- **Acceptance criteria:** Every shipped ID pinned; every prohibition in
  scope has at least one negative test at milestone scope.
- **Rollback:** Not applicable.
- **Blocking decisions:** None.
- **Prohibited scope:** No weakening of any existing test to get green.

### VIS-038 — Determinism neutrality and presentation hygiene scans **[MILESTONE]**

- **Goal:** The structural non-contamination proof: source scans asserting
  no `System.Random`, no `GetHashCode`-based selection, and no
  iteration-order dependence in the new presentation code; confirmation
  that `Hukbo.Core` has zero diffs; a Client-visible neutrality check in
  the logging-neutrality style where practical; and pins that
  `Content.mgcb` and `Directory.Packages.props` are unchanged (R-W6.15
  structure, R-W6.16, R-W6.18, R-X.16).
- **Dependencies:** The milestone implementation tasks (VIS-008, VIS-010,
  VIS-013, VIS-018, VIS-025, VIS-026, VIS-030, VIS-031, VIS-032).
  Milestone.
- **Files:** `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` (extended
  scans); `tests/Hukbo.Client.Tests/PresentationNeutralityTests.cs` *(new,
  if a practical seam exists — otherwise the structural argument is
  recorded in the test file's doc comment instead of a pretend test, per
  the integration design)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — tests and scans.
- **Implementation steps:**
  1. Extend the source scans to the new presentation namespaces.
  2. Pin the content pipeline entry count (six spritefonts, nothing else)
     and the package manifest hash or entry list.
  3. Evaluate whether a run-with-and-without-visual-settings neutrality
     test is practical; implement or record the structural argument.
- **Automated verification:** The scans and pins pass; the gate workload
  reproduces the recorded seed-1 reference pair (stateHash
  `27DC94C6E9A01E35`, eventHash `372C9217E5CB8BE9` —
  `docs/development/testing.md`, Phase 2 reference pair). The
  `DeterminismTests` zero-interception V1 control-run golden
  (`0x5BEBA7A68F69BE0D`) is a separate, additional guard that must also stay
  green — it is not the gate workload's hash.
- **Manual visual verification:** None.
- **Expected artifacts:** Extended test files; green output.
- **Acceptance criteria:** All scans green; zero `Hukbo.Core` diffs in the
  package's cumulative diff.
- **Rollback:** Not applicable.
- **Blocking decisions:** None.
- **Prohibited scope:** No test weakened; no scan exemption added for
  convenience.

### VIS-039 — Full-roster suites

- **Goal:** The post-milestone content contract: all ten prohibition
  negative tests over the full 53-preset roster, the preset count floor
  (at least 50), pairwise differentiation **scoped within each regional
  block** (the decided RF-02 resolution: cross-block near-duplicates are
  acceptable because the scope tag is the differing claim and blocks never
  co-exist inside one faction's army in a match), pool totality across all
  blocks and loadouts, and the pawn-scale exclusion suite for every
  inspector-only entry (R-W3.2, R-W3.4, R-X.8, R-W1.4).
- **Dependencies:** VIS-011, VIS-014, VIS-019, VIS-020, VIS-021, VIS-022.
  Post-milestone.
- **Files:** `tests/Hukbo.Client.Tests/AppearanceRosterContractTests.cs`
  *(new)*; extensions to the per-block test files.
- **Historical evidence dependency:** The research's ten prohibitions and
  six co-occurrence rules (encoded, not reinterpreted).
- **Determinism classification:** Pure-presentation — tests only.
- **Implementation steps:**
  1. One negative test per prohibition (a deliberately illegal recipe per
     rule fails the validator).
  2. Count floor, within-block pairwise differentiation (the suite iterates
     each regional block's pairs, never cross-block pairs), and totality
     over the shipped roster.
  3. Exclusion suite: `k2`, `l2`, `l3` and every inspector-only entry
     unreachable by any selection stream.
- **Automated verification:** The suite passes over the complete roster.
- **Manual visual verification:** None (the line-by-line historical review
  of the roster against the research is a human review task tracked in
  VIS-043, not a test).
- **Expected artifacts:** The suite; green output.
- **Acceptance criteria:** Ten negatives, floor, differentiation, totality
  all green.
- **Rollback:** Not applicable.
- **Blocking decisions:** None.
- **Prohibited scope:** No prohibition encoded more weakly than the
  research states it.

### VIS-040 — Consolidated tier and threshold boundary suite

- **Goal:** One suite walking every new element's tier gate and band
  threshold at the exact boundary values — 0.95 and 1.80 apparent scale for
  pawn layers; the grass zoom bands at their pinned values; decal clamp
  unchanged — so threshold drift anywhere fails one obvious place (R-X.4,
  R-W3.13, R-W5.6).
- **Dependencies:** VIS-011, VIS-014, VIS-023, VIS-026, VIS-031.
  Post-milestone.
- **Files:** `tests/Hukbo.Client.Tests/DetailTierBoundaryTests.cs` *(new)*.
- **Historical evidence dependency:** None.
- **Determinism classification:** Pure-presentation — tests only.
- **Implementation steps:**
  1. Enumerate every catalog entry's `MinimumDetailTier` and assert the
     gate at both sides of each threshold.
  2. Assert the grass band selection at its exact pinned thresholds.
- **Automated verification:** The suite passes across all shipped entries.
- **Manual visual verification:** None.
- **Expected artifacts:** The suite; green output.
- **Acceptance criteria:** Every shipped element covered by a boundary
  assertion.
- **Rollback:** Not applicable.
- **Blocking decisions:** None.
- **Prohibited scope:** No change to the existing thresholds.

## Category 11 — Manual screenshot and interactive review

### VIS-041 — Milestone PENDING rows and the review protocol **[MILESTONE]**

- **Goal:** Add the milestone's manual criteria to
  `docs/development/testing.md` as `PENDING` rows, each naming the review
  protocol: a fixed scenario seed, the three camera stations (minimum zoom
  full field, default fit, maximum zoom close-up), the themes (default and
  high-contrast), and the settings permutations (gore, motion) relevant to
  the row (R-W6.17; integration design section 10 leg 4).
- **Dependencies:** VIS-008, VIS-010, VIS-013, VIS-018, VIS-026, VIS-031,
  VIS-032 (the rows describe what those tasks built). Milestone.
- **Files:** `docs/development/testing.md`.
- **Historical evidence dependency:** None.
- **Determinism classification:** Not applicable (documentation).
- **Implementation steps:**
  1. Author the milestone rows, worded as questions a human answers:
     Kalis tints (three zoom rows); S1 shield rows (two); levy preset rows
     (two); grass and border rows (two); sway rows (three: alive-not-noise,
     no-motion-at-minimum-zoom, high-contrast-zero-motion); the
     motion-setting operability row; the forced-failure placeholder row.
  2. Every row is created `PENDING` with the seed and stations named.
- **Automated verification:** None (a documentation diff; the gate's format
  check still applies to the repository).
- **Manual visual verification:** The rows themselves are the manual
  verification vehicle. **Only a human at an interactive desktop may flip a
  row to PASS; nothing in this plan, no test, and no agent may.** Untouched
  rows stay `PENDING`; obstacles are recorded `BLOCKED`.
- **Expected artifacts:** The testing-document diff with all rows
  `PENDING`.
- **Acceptance criteria:** Every milestone manual criterion from the
  designs has exactly one row; no row is born in any state but `PENDING`.
- **Rollback:** Remove the rows (documentation only).
- **Blocking decisions:** None.
- **Prohibited scope:** No row flipped by this task; no automated claim of
  visual quality.

### VIS-042 — Milestone manual review session *(human)* **[MILESTONE]**

- **Goal:** A human runs the protocol against the milestone build and
  honestly disposes each milestone row: PASS, FAIL (with what was seen), or
  BLOCKED.
- **Dependencies:** VIS-041, VIS-045 (review the gated build). Milestone —
  the milestone is not complete until this session has happened or is
  honestly recorded BLOCKED.
- **Files:** `docs/development/testing.md` (row dispositions);
  screenshots under `artifacts/` (optional evidence).
- **Historical evidence dependency:** None.
- **Determinism classification:** Not applicable.
- **Implementation steps:**
  1. Launch via `./scripts/run.ps1` with the row-named seed.
  2. Visit the stations under both themes and the settings permutations.
  3. Judge each row's question; attach screenshots where useful; record
     dispositions.
  4. For the forced-failure row, run the documented forced-failure debug
     configuration and confirm placeholder plus log line.
- **Automated verification:** None, by definition.
- **Manual visual verification:** This task **is** the manual verification.
  Only the human reviewer flips rows.
- **Expected artifacts:** Updated row states; screenshots under
  `artifacts/`.
- **Acceptance criteria:** Every milestone row disposed or explicitly
  BLOCKED with the reason.
- **Rollback:** Not applicable.
- **Blocking decisions:** Requires an interactive desktop session; BLOCKED
  honestly otherwise.
- **Prohibited scope:** No agent participation in row flipping.

### VIS-043 — Full-package PENDING rows

- **Goal:** Extend the testing document with every remaining manual
  criterion named across the five designs: all-weapon zoom rows, four-skin
  and posture rows, roster coherence and elite-density rows, trample and
  correlated-shading rows, inspector rows per domain, the faction-readability
  row, and the no-visual-change row for VIS-009's window; plus the roster's
  line-by-line historical review recorded as a human review task (not a
  test row).
- **Dependencies:** The post-milestone feature tasks whose behavior the
  rows describe (VIS-009, VIS-011, VIS-012, VIS-014, VIS-015, VIS-016,
  VIS-020 through VIS-024, VIS-027, VIS-028, VIS-033; VIS-029 if
  unblocked). Post-milestone.
- **Files:** `docs/development/testing.md`.
- **Historical evidence dependency:** The historical-review task references
  the warrior-appearance research as its checklist source.
- **Determinism classification:** Not applicable.
- **Implementation steps:** As VIS-041, for the full criterion set; every
  row `PENDING`, protocol named.
- **Automated verification:** None.
- **Manual visual verification:** The rows; human-only flips.
- **Expected artifacts:** The testing-document diff.
- **Acceptance criteria:** Requirement-to-row audit: every manual criterion
  in the requirements document has exactly one row (the audit list is part
  of this task's diff description).
- **Rollback:** Documentation only.
- **Blocking decisions:** None.
- **Prohibited scope:** No row born flipped.

### VIS-044 — Full-package manual review session *(human)*

- **Goal:** The complete review pass over all rows, after full integration,
  including the roster historical review.
- **Dependencies:** VIS-043, VIS-047 (gate first, then review the final
  build). Post-milestone, final.
- **Files:** `docs/development/testing.md`; `artifacts/` screenshots.
- **Historical evidence dependency:** The roster review is judged against
  `docs/research/improve-visuals/warrior-appearance-historical-research.md`.
- **Determinism classification:** Not applicable.
- **Implementation steps:** As VIS-042 across the full row set; the
  historical review is recorded with its findings (a FAIL there routes back
  to a content correction task, not a test change).
- **Automated verification:** None.
- **Manual visual verification:** This task is it; human-only.
- **Expected artifacts:** Final row dispositions; screenshots; the
  historical-review record.
- **Acceptance criteria:** Every row disposed or honestly BLOCKED; the
  package's completion report quotes the row states as they are, never
  better.
- **Rollback:** Not applicable.
- **Blocking decisions:** Interactive desktop required.
- **Prohibited scope:** No agent row flips; no "effectively passed" claims.

## Category 12 — Documentation and final integration verification

### VIS-045 — Milestone integration gate run **[MILESTONE]**

- **Goal:** After the milestone tasks integrate, run the canonical gate
  `./scripts/verify.ps1` once, on the integrated tree, and record its real
  output: prerequisites and locked restore, format verification, Release
  build, Core and Client tests, and the 200-agent / 10,000-tick / seed-1
  headless determinism workload reproducing the recorded seed-1 reference
  pair (stateHash `27DC94C6E9A01E35`, eventHash `372C9217E5CB8BE9` —
  `docs/development/testing.md`, Phase 2 reference pair), with the outcome
  and ordered event stream unchanged (R-W6.15). The `DeterminismTests`
  zero-interception V1 control-run golden (`0x5BEBA7A68F69BE0D`) is a
  separate, additional guard that must also stay green in the test stage.
- **Dependencies:** All other milestone implementation and test tasks
  (VIS-001 through VIS-005, VIS-007, VIS-008, VIS-010, VIS-013, VIS-017,
  VIS-018, VIS-025, VIS-026, VIS-030, VIS-031, VIS-032, VIS-034, VIS-035,
  VIS-037, VIS-038). Milestone.
- **Files:** None edited; the gate reads the tree. The output is recorded
  in the integration commit or the testing document's results section.
- **Historical evidence dependency:** None.
- **Determinism classification:** Not applicable (verification).
- **Implementation steps:**
  1. Run `./scripts/verify.ps1` at the repository root.
  2. Paste the complete real output into the record; on any failure, fix
     and re-run — the gate is re-run in full after any change.
- **Automated verification:** The gate itself.
- **Manual visual verification:** None (VIS-042 follows it).
- **Expected artifacts:** The pasted gate output; the seed-1 reference pair
  (stateHash `27DC94C6E9A01E35`, eventHash `372C9217E5CB8BE9`) quoted from
  it.
- **Acceptance criteria:** Gate green end to end; the reference pair
  reproduced exactly — both the state hash and the event hash. **This run is
  never delegated to a sub-agent and no sub-agent report substitutes for
  it.**
- **Rollback:** Not applicable.
- **Blocking decisions:** None.
- **Prohibited scope:** No claim of verification without the pasted
  output; no partial-gate claims.

### VIS-046 — Documentation updates and decision record

- **Goal:** Bring the package's documentation to its shipped state: update
  the package README's status and document map; confirm the recorded
  decision outcomes and add the plan-level ones. The ten package decisions
  are all **Resolved 2026-07-28** and recorded in the README and the
  requirements document: OD-1 (plain `Tall Hardwood Shield` ships; kalasag
  pair-form promotion waits for attestation verification, unscheduled);
  OD-2 (palisay in inspector research notes as metadata only, flagged
  attestation-pending); OD-3 (Unscoped-generic block accepted as sole
  Mindanao/Sulu coverage this pass); OD-4 (fully procedural rendering
  confirmed); OD-5 (earned red putong excluded; backlog in
  `docs/plans/TODO.md`); OD-6 (default theme ground shifts toward cogon
  olive-gold this pass, provisional-tagged; jungle/plains ground-treatment
  exploration backlog in `docs/plans/TODO.md`); OD-7 (shape-redundant
  faction marker deferred; backlog in `docs/plans/TODO.md`); OD-8
  (MotionIntensity governs all ambient presentation motion — grass sway
  now, dust and future ambient motion included); OD-9 (R-W4.8 downgraded
  MUST to MAY; VIS-029 unblocked but optional; if dust ships,
  MotionIntensity Off suppresses spawning, Reduced leaves it unchanged);
  OD-10 (R-W2.1 amended per option (a) — bounded per-skin proportion deltas
  inside one shared aspect-ratio band, footprint never below the Low-tier
  block, guarded by the manual false-cause row; S2/S5 deltas kept). This
  task also records the plan-level outcomes as they are decided (OD-W1-a/b/c;
  OD-W2-a/b/c; the decided identifier grammar per RF-05 — camelCase table
  IDs with the optional `tint.` sub-segment canonical; the ground-shading
  salt; the block-assignment product call); notes the new setting and the
  review protocol in the appropriate development docs; and amends the
  integration design's identifier paragraph per the RF-05 decision (a
  post-decision edit, marked as such). The OD-5 and OD-7 backlog items live
  in `docs/plans/TODO.md`.
- **Dependencies:** All feature tasks that close a decision (effectively
  everything except VIS-044/VIS-047). Post-milestone.
- **Files:** `docs/plans/improve-visuals/README.md`;
  `docs/plans/improve-visuals/visual-system-integration-design.md`
  (identifier paragraph amendment only, marked as a post-decision edit);
  `docs/development/testing.md` (protocol section);
  this plan document (final task states).
- **Historical evidence dependency:** None new; the decision record cites
  the research where a decision touched evidence (OD-1, OD-2, OD-3).
- **Determinism classification:** Not applicable.
- **Implementation steps:**
  1. Write the decision record with each outcome and its date.
  2. Update statuses; keep all documents in full normal English (no
     compression pass — `CLAUDE.md` section 6).
- **Automated verification:** The gate's format verification passes.
- **Manual visual verification:** None.
- **Expected artifacts:** The documentation diffs.
- **Acceptance criteria:** No open decision remains unrecorded; no stale
  "being authored" status remains in the package.
- **Rollback:** Documentation only.
- **Blocking decisions:** None of the package decisions — OD-1 through
  OD-10 are resolved 2026-07-28 and already recorded; this task confirms
  that record and writes down the remaining plan-level outcomes as they are
  decided.
- **Prohibited scope:** No prose-compression of repository documentation.

### VIS-047 — Final integration verification and archival readiness

- **Goal:** The package's terminal gate: run `./scripts/verify.ps1` on the
  fully integrated tree, record its real output, confirm the cumulative
  diff contains zero `Hukbo.Core` changes, and prepare the plan for
  archival under `docs/archives/<date>/` per the workflow — archival itself
  happens only after the user accepts the package.
- **Dependencies:** Everything except VIS-044 (the final human review
  follows this gate; manual rows may remain honestly `PENDING` or
  `BLOCKED` without blocking the gate, and their states are reported as
  they are). Post-milestone, terminal.
- **Files:** None edited by the gate; the archival move (when authorized)
  relocates `docs/plans/improve-visuals/` plan documents per
  `docs/archives/README.md`.
- **Historical evidence dependency:** None.
- **Determinism classification:** Not applicable.
- **Implementation steps:**
  1. Run the gate; paste the full output.
  2. Diff-audit: `git diff` scoped to `src/Hukbo.Core` is empty across the
     package; `Content.mgcb`, `Directory.Packages.props`, and
     `packages.lock.json` are unchanged.
  3. Report the final state honestly: gate result, hash confirmation, row
     states, measurement artifacts by filename.
  4. On user acceptance, archive the plan documents dated the day of
     archiving with the "Archived: reference only" banner.
- **Automated verification:** The gate itself; the empty-Core-diff audit.
- **Manual visual verification:** VIS-044's rows, reported as-is.
- **Expected artifacts:** The pasted gate output; the diff audit; the
  final report.
- **Acceptance criteria:** Gate green; the recorded seed-1 reference pair
  reproduced (stateHash `27DC94C6E9A01E35`, eventHash `372C9217E5CB8BE9` —
  `docs/development/testing.md`, Phase 2 reference pair) with the outcome
  and ordered event stream unchanged, and the separate `DeterminismTests`
  zero-interception V1 control-run golden (`0x5BEBA7A68F69BE0D`) also green;
  Core untouched; every claim in the report backed by a named artifact.
  **Never delegated.**
- **Rollback:** Not applicable.
- **Blocking decisions:** User acceptance gates archival.
- **Prohibited scope:** No verification claim without pasted output; no
  archival before acceptance.

---

## Summary dependency table

| Task | What | Primary files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| VIS-001 [M] | Salt registry | `Presentation/PresentationSalts.cs` | Distinctness + pins green | — | xunit |
| VIS-002 [M] | Catalog entry model, ID grammar | `Catalogs/VisualCatalogEntry.cs` | Grammar + field tests green | inconsistency 2 decision | xunit |
| VIS-003 [M] | Fallback chain + placeholder def | `Catalogs/VisualFallbackResolver.cs` | Totality + step reachability green | VIS-002 | xunit |
| VIS-004 [M] | Missing-visual diagnostics | `Hukbo.Diagnostics/LogEvents.cs`, `Catalogs/VisualDiagnostics.cs` | Hygiene + dedup green | VIS-003 | xunit |
| VIS-005 [M] | Contrast envelope helpers | `Presentation/ContrastEnvelope.cs` | Metric + bound pins green | — | xunit |
| VIS-006 | Startup catalog validation | `Catalogs/VisualCatalogValidator.cs`, `ArenaGame.cs` | Failure classes tested | VIS-002..004 | xunit |
| VIS-007 [M] | Detail-tier gate helper | `Rendering/DetailTierGate.cs` | Exact-threshold tests green | VIS-002 | xunit |
| VIS-008 [M] | Placeholder rendering | `PawnGeometry.cs`, `PawnRenderer.cs` | Step-4 draw under double | VIS-003, VIS-004 | xunit + manual row |
| VIS-009 | PawnLayout anchors, layer slots | `PawnGeometry.cs`, `PawnRenderer.cs` | Zero visual change, anchors pinned | VIS-007 | xunit + manual row |
| VIS-010 [M] | Kalis variant family | `Catalogs/WeaponVisualCatalog.cs`, factory, renderer | Stability + invariance green | VIS-001..003, 005, 007 | xunit + manual rows |
| VIS-011 | Kampilan/Wasay/Itak families | same as VIS-010 (+`PawnGeometry.cs`) | Four-weapon suites green | VIS-010 | xunit + manual rows |
| VIS-012 | Weapon inspector surface | `UI/AgentInspectorContent.cs`, `UI/AgentInspectorPanel.cs` | Content tests green | VIS-010, VIS-011 | xunit + manual row |
| VIS-013 [M] | Shield skin infra + S1 | `Catalogs/ShieldVisualCatalog.cs`, factory, renderer | Stability + footprint green | VIS-001..003, 005, 007 | xunit + manual rows |
| VIS-014 | Skins S2/S3/S5 + envelope | shield catalog, `PawnGeometry.cs`, renderer | Band + gate tests green | VIS-013 | xunit + manual rows |
| VIS-015 | Angled shield posture | `PawnGeometry.cs`, renderer | Bounds-neutral by test | VIS-009, VIS-013 | xunit + manual row |
| VIS-016 | Shield inspector + name negatives | inspector files | Negative suite green | VIS-012..014 | xunit + manual row |
| VIS-017 [M] | Component catalog core + dyes | `Catalogs/AppearanceComponentCatalog.cs`, `Catalogs/DyePalette.cs` | Palette + distance pins green | VIS-001, 002, 005 | xunit |
| VIS-018 [M] | Levy presets + selection streams | `Catalogs/AppearancePresets.Levy.cs`, validator, factory | Five presets validate | VIS-017, 003, 007 | xunit + manual rows |
| VIS-019 | Full component catalog | component catalog | Category counts + caps green | VIS-017 | xunit |
| VIS-020 | Visayan block (20) | `Catalogs/AppearancePresets.Visayan.cs` | Block validates | VIS-018, 019 | xunit + manual rows |
| VIS-021 | Tagalog block (15) | `Catalogs/AppearancePresets.Tagalog.cs` | Block validates | VIS-018, 019 | xunit + manual rows |
| VIS-022 | Luzon + levy rest + block table | `Catalogs/AppearancePresets.NorthernLuzon.cs`, `.Levy.cs` | 53 presets, table complete | VIS-018, 019 | xunit + manual rows |
| VIS-023 | Armor/sash/accent layers | `PawnGeometry.cs`, `PawnRenderer.cs` | Layer + occlusion tests green | VIS-009, VIS-019 | xunit + manual rows |
| VIS-024 | Appearance inspector surface | inspector files | Content + negatives green | VIS-016, 020..022 | xunit + manual row |
| VIS-025 [M] | Grass generation + backdrop metadata entry | `Rendering/GrassGeometry.cs`, `Catalogs/BackdropVisualCatalog.cs`, `ArenaGame.cs` | Determinism + caps green | VIS-001, VIS-002 | xunit |
| VIS-026 [M] | Grass rendering + LOD | `GrassGeometry.cs`, `Rendering/GrassRenderer.cs`, `ArenaGame.Rendering.cs` | Ceiling + band pins green | VIS-025 | xunit + manual rows |
| VIS-027 | Correlated ground shading | `PlainsBackdropGeometry.cs`, backdrop renderer | Formula pinned, decals unshifted | VIS-001, VIS-026; inconsistency 3 | xunit + manual row |
| VIS-028 | Trample marks | `Presentation/TrampleMarkSystem.cs`, grass files, coordinator | Lifecycle tests green | VIS-025, 026, 030 | xunit + manual row |
| VIS-029 | Dust puffs (optional per OD-9, resolved 2026-07-28) | `Presentation/DustEffectSystem.cs`, `Rendering/DustGeometry.cs` | Lifecycle tests green | VIS-026 | xunit + manual row |
| VIS-030 [M] | Sway helper + clock | `Rendering/GrassSway.cs`, coordinator | Exact-zero + bound pins green | VIS-025 | xunit + manual rows |
| VIS-031 [M] | Effective amplitude resolution | `GrassSway.cs`, grass renderer | Truth table green | VIS-030, 032, 026 | xunit + manual row |
| VIS-032 [M] | Reduced-motion setting chain | `Settings/MotionIntensity*.cs`, store, `UI/MotionIntensitySelector.cs` | Round-trip + migration green | — | xunit + manual row |
| VIS-033 | Faction-signal guard + theme continuity | guard tests, validator | Sweep green | VIS-005, 017, 026 | xunit + manual row |
| VIS-034 [M] | Submission-counting seam | `Rendering/SubmissionCount.cs` | Count pins green | VIS-010, 013, 018, 026 | xunit |
| VIS-035 [M] | RenderProbe + baseline | `tools/Hukbo.Tools.RenderProbe/`, seam | Baseline JSON exists | — (baseline pre-integration) | artifact + build |
| VIS-036 | Full matrix + budget reconciliation | probe, testing doc, count tests | Matrix report, budgets settled | VIS-035 + all rendering tasks | artifact + xunit |
| VIS-037 [M] | Catalog contract suite | `VisualCatalogContractTests.cs` | Suite green | milestone catalogs | xunit |
| VIS-038 [M] | Neutrality + hygiene scans | `SourceHygieneTests.cs` + new | Scans green, Core diff empty | milestone impl tasks | xunit |
| VIS-039 | Full-roster suites | `AppearanceRosterContractTests.cs` | Ten negatives + floor green | VIS-011, 014, 019..022 | xunit |
| VIS-040 | Tier boundary suite | `DetailTierBoundaryTests.cs` | All entries covered | VIS-011, 014, 023, 026, 031 | xunit |
| VIS-041 [M] | Milestone PENDING rows | `docs/development/testing.md` | Rows exist, all PENDING | milestone impl tasks | doc diff |
| VIS-042 [M] | Milestone human review | testing doc, `artifacts/` | Rows disposed or BLOCKED | VIS-041, VIS-045 | human only |
| VIS-043 | Full PENDING rows | testing doc | Criterion-to-row audit complete | post-milestone features | doc diff |
| VIS-044 | Full human review | testing doc, `artifacts/` | Rows disposed or BLOCKED | VIS-043, VIS-047 | human only |
| VIS-045 [M] | Milestone gate run | — (gate) | Gate green, seed-1 reference pair (state `27DC94C6E9A01E35` / event `372C9217E5CB8BE9`) reproduced | all milestone tasks | `./scripts/verify.ps1` output |
| VIS-046 | Docs + decision record | package docs, testing doc | All decisions recorded | decision-closing tasks | format check |
| VIS-047 | Final gate + archival readiness | — (gate) | Gate green, Core diff empty | everything but VIS-044 | `./scripts/verify.ps1` output |

File-ownership note for parallel scheduling: the factory, `PawnAppearance`,
`PawnGeometry`, `PawnRenderer`, and the inspector files are shared surfaces —
tasks touching them (VIS-008/009/010/011/013/014/015/018/023;
VIS-012/016/024) are sequenced, never parallel with each other. Safely
parallel groups include: {VIS-001, VIS-002, VIS-005, VIS-032, VIS-035};
{VIS-020, VIS-021, VIS-022} (per-block files); {VIS-025 with any pawn-side
task}; the test-suite tasks against disjoint test files.

## Requirement coverage map

Every MUST and MUST NOT requirement mapped to at least one task. SHOULD and
MAY requirements are listed only where a task carries them.

| Requirement | Covered by |
| --- | --- |
| R-X.1 | VIS-007, VIS-023, VIS-026, VIS-040, VIS-041/043 rows |
| R-X.2 | VIS-010, VIS-011, VIS-013, VIS-014, VIS-018, VIS-040 |
| R-X.3 | VIS-010, VIS-011 (classification invariance) |
| R-X.4 | VIS-007, VIS-040 |
| R-X.5 | VIS-008, VIS-009, VIS-015, VIS-023 (bounds tests) |
| R-X.6 | VIS-012, VIS-016, VIS-019, VIS-024 (pair-form + negative suites) |
| R-X.7 | VIS-002, VIS-012, VIS-016, VIS-024, VIS-037 |
| R-X.8 | VIS-018 (skeleton), VIS-020..022, VIS-039 (ten negatives) |
| R-X.9 | VIS-005, VIS-010, VIS-015, VIS-025, VIS-030 (`PROVISIONAL` constants) |
| R-X.10 (SHOULD) | VIS-012, VIS-016, VIS-024 (inspiration tags) |
| R-X.11 | VIS-038, VIS-045, VIS-047 (empty Core diff, gate) |
| R-X.12 | VIS-013, VIS-014 (no shape/size change), VIS-038, VIS-047 |
| R-X.13 | VIS-045, VIS-047 (local gate only; no CI task exists) |
| R-X.14 | VIS-025 (batched clusters; no per-blade anything) |
| R-X.15 | All rendering tasks are procedural by construction; VIS-038 pins no new content-pipeline entries |
| R-X.16 | VIS-004 (bounded seen-set), VIS-028 (bounded pool), VIS-038 (scans) |
| R-W1.1 | VIS-010, VIS-011 |
| R-W1.2 | VIS-010, VIS-011 |
| R-W1.3 | VIS-001, VIS-010 |
| R-W1.4 | VIS-010, VIS-011 (exclusion tests; W2 no identifier) |
| R-W1.5 (SHOULD) | VIS-011 (lashing band) |
| R-W1.6 | VIS-010, VIS-011, VIS-012 |
| R-W1.7 | VIS-005, VIS-010, VIS-011 |
| R-W1.8 (SHOULD) | VIS-011 (tint-count pin) |
| R-W1.9 (MAY) | VIS-010, VIS-011 (High-tier wear, tone-only) |
| R-W2.1 | VIS-013, VIS-014 |
| R-W2.2 | VIS-013, VIS-014 (footprint floor) |
| R-W2.3 | VIS-013 |
| R-W2.4 | VIS-014 (band tests), VIS-039 |
| R-W2.5 (SHOULD) | VIS-015 |
| R-W2.6 | VIS-016 |
| R-W2.7 | VIS-013, VIS-016 |
| R-W2.8 | VIS-005, VIS-013, VIS-014 |
| R-W3.1 | VIS-017, VIS-019 |
| R-W3.2 | VIS-020, VIS-021, VIS-022, VIS-039 (floor pin) |
| R-W3.3 | VIS-018, VIS-020..022, VIS-024 |
| R-W3.4 | VIS-018 (skeleton), VIS-039 (full) |
| R-W3.5 | VIS-018, VIS-022 (block table) |
| R-W3.6 | VIS-019, VIS-023 (channel and accent caps) |
| R-W3.7 | VIS-019 (tone-shift only, pinned delta) |
| R-W3.8 | VIS-017 (palette pins), VIS-033 (faction distance) |
| R-W3.9 | VIS-020, VIS-021 (structure), VIS-039 (negatives) |
| R-W3.10 | VIS-019, VIS-020 (C2 absent pending OD-5; negative test in VIS-039) |
| R-W3.11 | VIS-017, VIS-019 (no-historical-claim marker) |
| R-W3.12 | VIS-019, VIS-024 |
| R-W3.13 | VIS-023, VIS-040 |
| R-W3.14 (SHOULD) | VIS-020 (rarity weights) |
| R-W3.15 (MAY) | VIS-024 (flavor lines) |
| R-W4.1 | VIS-027 |
| R-W4.2 | VIS-026, VIS-027, VIS-028, VIS-029 (ceiling pins) |
| R-W4.3 | VIS-025 |
| R-W4.4 | VIS-025, VIS-028, VIS-029, VIS-034 (named caps + pins) |
| R-W4.5 | VIS-026 (one batch, one texture), VIS-034, VIS-038 |
| R-W4.6 | VIS-025, VIS-030 (clip under sway) |
| R-W4.7 | VIS-028 |
| R-W4.8 | VIS-029 — now MAY per OD-9 (resolved 2026-07-28); unblocked, optional |
| R-W4.9 | VIS-025 (the one-entry backdrop metadata declaration, tier Provisional reconstruction; no player-facing naming), VIS-046 |
| R-W4.10 | VIS-025, VIS-026 (allocation-free paths), VIS-035 (GC verify) |
| R-W4.11 (SHOULD) | VIS-025 (clumped placement) |
| R-W4.12 (MAY) | Not scheduled — OD-6 is a separate theme review by design |
| R-W5.1 | VIS-030 |
| R-W5.2 | VIS-030 (clock in coordinator) |
| R-W5.3 | VIS-025, VIS-030 (phase from generation stream) |
| R-W5.4 | VIS-030 (amplitude and frequency pins) |
| R-W5.5 | VIS-030 (exact `Vector2.Zero` test) |
| R-W5.6 | VIS-026, VIS-031, VIS-040 (bands at exact thresholds) |
| R-W5.7 | VIS-026 (shade spread), VIS-031 (forcing) |
| R-W5.8 | VIS-031, VIS-032 |
| R-W5.9 | VIS-028 (suppression), VIS-030, VIS-031, VIS-038 |
| R-W5.10 (MAY) | Not scheduled — recorded out of scope, as the design states |
| R-W6.1 | VIS-002, VIS-037 (pinned IDs) |
| R-W6.2 | VIS-001 (registry + distinctness), every selection task |
| R-W6.3 | VIS-018 (pure selection), VIS-028/029/030/032 (the only declared presentation-state), VIS-038 |
| R-W6.4 | VIS-003, VIS-008 |
| R-W6.5 | VIS-004 |
| R-W6.6 | VIS-032 |
| R-W6.7 (SHOULD) | VIS-032 (Off/Reduced/Full) |
| R-W6.8 | VIS-032 (independent validation tests) |
| R-W6.9 | VIS-026, VIS-027, VIS-031 |
| R-W6.10 | VIS-033 |
| R-W6.11 | VIS-033 |
| R-W6.12 | VIS-035 (measurement precedes enforcement; VIS-036 ordered after) |
| R-W6.13 | VIS-034, VIS-035, VIS-036 (ESTIMATE until measured) |
| R-W6.14 | VIS-025, VIS-028, VIS-034 (named constants, never derived) |
| R-W6.15 | VIS-045, VIS-047 (gate output recorded, never delegated) |
| R-W6.16 | VIS-038 (scan), every task's pure-helper split |
| R-W6.17 | VIS-041, VIS-043 (rows created PENDING) |
| R-W6.18 | VIS-038 (pipeline and package manifest pins) |

**Uncovered MUST requirements: none.** Two notes: **R-W4.8** (dust) was
amended from MUST to MAY by the user's 2026-07-28 resolution of OD-9, so
VIS-029 is unblocked and optional — the amendment is the user-approved
requirement change; **R-W3.10** (earned red putong) is satisfied by
exclusion — C2 ships in no roster and a negative test enforces its absence —
which is exactly what the requirement demands under the 2026-07-28
resolution of OD-5 (excluded; backlog in `docs/plans/TODO.md`).

## Closing statement

On 2026-07-28 the user resolved every package open decision — OD-1 through
OD-10, including OD-9 (dust, inconsistency 1), OD-10 (the R-W2.1
shield-proportion amendment), and OD-4 (the procedural direction) — and
approved the 23-task first milestone, authorizing its implementation.
Inconsistency 2 is decided per RF-05 (camelCase table identifiers are
canonical); the recorded defaults for inconsistencies 3 and 4 stand unless
the user overrides them during implementation. The post-milestone expansion
tasks await the milestone review. The first work performed under this
approval is the milestone; the first evidence produced is VIS-035's
pre-integration render baseline; and the only verification that closes
either the milestone or the package is the real, pasted output of
`./scripts/verify.ps1`, run once, on the integrated tree, by the integrating
session itself.
