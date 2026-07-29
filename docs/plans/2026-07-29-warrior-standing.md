# Warrior standing — implementation plan

Date: 2026-07-29
Design: [`2026-07-29-warrior-standing-design.md`](2026-07-29-warrior-standing-design.md)
Evidence: [`docs/research/HISTORICAL_1500s_RANKS.md`](../research/HISTORICAL_1500s_RANKS.md)

Phase A is the whole of this plan's committed scope. Phase B is written out
because sequencing it separately is the point, but it is gated on the user
answering open question 3 in the design document.

Nothing in this plan may begin until the design document's four open
questions are answered. Task A1 records those answers.

## Phase A — standing exists, is authoritative, and is visible

### A1. Record the design decisions

**Files:** `docs/plans/2026-07-29-warrior-standing-design.md`

Append a "Decisions" section recording the user's answer to each of the four
open questions. Every later task assumes those answers; a task that finds
itself guessing has hit a missing decision and should stop rather than pick.

**Verification:** the section exists and names all four answers.

### A2. Add `StandingId`

**Files:** `src/Hukbo.Core/Combat/CombatIdentity.cs`

Add the five-value enum with pinned numeric values 1 through 5, following the
comment style `WeaponId` already uses: one XML summary per value naming the
region, the evidence tier, and the source, plus the "do not renumber or
reorder" contract note on the enum itself. Copy the tier wording from the
research document rather than paraphrasing it.

**Depends on:** A1.

**Verification:** builds clean under `TreatWarningsAsErrors`. No behavior
change yet, so every existing test still passes untouched.

### A3. Add `Standing` to `CombatLoadout`

**Files:** `src/Hukbo.Core/Combat/CombatIdentity.cs`, plus every construction
site the compiler names.

`CombatLoadout` becomes `(WeaponId Weapon, ArmorId Armor, ShieldId Shield,
StandingId Standing)`. Adding a required positional field will break every
existing construction, in presets and in tests. That is deliberate: each site
must state a standing rather than inherit a default.

Presets V1, V2, and V3 declare `StandingId.Timawa` on every roster entry — a
single value across the whole roster, so that those presets carry no standing
differentiation at all and their hashes are provably unmoved once A6 gates
the fold.

**Depends on:** A2.

**Verification:** `./scripts/test.ps1 -Configuration Release` passes with no
test edited except for the mechanical addition of the standing argument.
`CombatConfigurationTests` content-hash freeze assertions for V1, V2, and V3
must pass **unchanged** — if any of them moves here, the fold gating in A6 is
wrong and the work stops.

### A4. Thread standing to the agent and the view

**Files:** `src/Hukbo.Core/Simulation/AgentState.cs`,
`src/Hukbo.Core/Simulation/AgentView.cs`,
`src/Hukbo.Core/Simulation/BattleSimulation.cs`

`AgentState.Standing` is a get-only property written once in the constructor
from the resolved loadout; it is not a separate constructor parameter,
because the loadout already carries it. `AgentView` gains the same field and
`ToView` passes it through.

**Depends on:** A3.

**Verification:** `BattleSimulationTests` passes; a new test asserts that
every spawned agent's `Standing` equals its resolved roster entry's standing
for a scenario using explicit `RosterCounts`.

### A5. Per-standing levels in the ruleset

**Files:** `src/Hukbo.Core/Combat/CombatRuleset.cs`

Add an optional `IReadOnlyDictionary<StandingId, int>? standingLevels`
constructor parameter and a `ResolveLevel(StandingId)` accessor, mirroring
the existing optional `weaponAttributes` pattern exactly: nullable field,
`HasStandingLevels` predicate, defensive copy, validation that every level is
at least 1, and validation that every standing the roster actually fields has
a level.

`BattleSimulation` uses `ResolveLevel` when the preset declares levels and
falls back to `Scenario.PlaceholderFighterLevel` when it does not.

**Depends on:** A4.

**Verification:** new unit tests for the accessor, the at-least-1 validation,
the roster-coverage validation, and the fallback. `ComboChainTests` passes
unchanged.

### A6. Fold standing into the content hash

**Files:** `src/Hukbo.Core/Combat/CombatRuleset.cs`

Two changes to `ComputeContentHash`, both additive and both placed after
every existing block, following the precedent the weapon-attribute and clash
blocks already set:

- the roster fold gains the standing value **only when the ruleset declares
  standing levels**, so a preset that declares none folds exactly the three
  values it folds today;
- a new standing-level block, contributed only by a preset that declares one.

**Depends on:** A5.

**Verification:** this is the load-bearing determinism check of phase A. The
V1, V2, and V3 pinned content hashes in `CombatConfigurationTests` must be
byte-identical to their current values, and that assertion must be run and
its output pasted. A new test builds two rulesets with identical standing
data supplied in different dictionary order and asserts equal hashes.

### A7. Fold standing into the state hash

**Files:** `src/Hukbo.Core/Determinism/StateHasher.cs`

Fold `AgentState.Standing`, gated the same way: contributed only when the
active preset declares standing levels, so V1 through V3 goldens do not move.

**Depends on:** A6.

**Verification:** `DeterminismTests` passes with existing goldens unchanged.
`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` under preset V3
reproduces the recorded seed-1 baseline exactly. Follow the
`hukbo-determinism-change` skill for the diagnosis path if it does not.

### A8. Preset `PrecolonialPhilippinesV4`

**Files:** `src/Hukbo.Core/Combat/CombatIdentity.cs` (new
`CombatPresetId.PrecolonialPhilippinesV4 = 4`),
`src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs` (new),
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs`

Restate every V3 value rather than referencing V3, exactly as V3 restates V2.
The only new authored data is the standing on each roster entry and the
per-standing level table. The class remarks must carry, in the same terms V3
uses, that the weapon-to-standing assignment is a provisional gameplay
choice with no source behind it and that no value here may be cited back into
the research document.

`Scenario.CombatPreset` keeps its current default. Switching the shipped
default to V4 is a separate decision, made after the gate has run against V4.

**Depends on:** A7.

**Verification:** a new pinned content-hash golden for V4; a new pinned
seed-1 state-hash and event-hash golden for V4;
`PhilippineCombatIntegrationTests` extended to run V4 to a decided outcome.

### A9. Client standing catalog

**Files:** `src/Hukbo.Client/Presentation/Catalogs/StandingLabelCatalog.cs`
(new), and its validator entry in `VisualCatalogValidator.cs` if the existing
validator covers label catalogs

One entry per `StandingId`, carrying the pair-form label, the region scope
string, and a `VisualEvidenceTier`. Reuse the existing tier enum and the
existing catalog-entry conventions; do not introduce a parallel vocabulary.

**Depends on:** A8.

**Verification:** a catalog test asserting an entry exists for every
`StandingId` value, that no label is a bare Filipino name, and that every
label's em dash and descriptor match the research document's table exactly.

### A10. Inspector standing line

**Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs`,
`src/Hukbo.Client/UI/AgentInspectorPanel.cs`

Add `FormatStandingLine`, following the existing pure-helper pattern the
`hukbo-client-ui` skill describes, and place it beside the existing level
line. The line shows the pair-form label, the region, and the tier.

**Depends on:** A9.

**Verification:** `AgentInspectorContentTests` gains one assertion per
standing value. No `ArenaGame`, graphics device, or sprite batch is
constructed, per the client test rule.

### A11. Composition panel labels

**Files:** `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`

Category labels name the standing alongside the weapon, so the pre-battle
roster is legible without opening an inspector.

**Depends on:** A10.

**Verification:** `ArmyCompositionPanelTests` and
`MenuOverlayArmyCompositionTests` pass; a new assertion pins the label text
for each category.

### A12. Cross-reference the documentation

**Files:** `docs/research/HISTORICAL_1500s_RANKS.md`,
`docs/research/HISTORICAL_1500s_WEAPONS.md`, `CLAUDE.md` section 7 if the
standing terms need to appear in the binding rule list

Add the reciprocal cross-reference from the weapons document to the ranks
document, and add a closing section to the ranks document in the same shape
as the weapons document's — naming which preset consumes this evidence, and
restating that none of the preset's numbers may be cited back.

**Depends on:** A11.

**Verification:** links resolve; no compression pass is run over either
document.

### A13. Run the canonical gate

**Files:** none

```powershell
./scripts/verify.ps1
```

Run once, after integration. Paste the real output. No sub-agent report and
no partial run substitutes for it. Then record the result and leave every
manual smoke-checklist row in `docs/development/testing.md` at its honest
value — `PENDING` for anything not actually observed by a person at an
interactive desktop.

**Depends on:** A12.

**Verification:** the gate's own five stages, plus a reported 500-agent
result as `SIMULATION-GAME-STANDARDS.md` section 10 requires.

## Phase B — standing decides who leads

Gated on the user answering open question 3. Do not start it in the same
change as phase A; the whole point of the split is that each state-hash move
has one cause.

### B1. Standing-aware leader scan

**Files:** `src/Hukbo.Core/Movement/MovementRules.cs`,
`src/Hukbo.Core/Movement/MovementPresetId.cs`,
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs`

New `MovementPresetId.PersistentContingentsV5`. Under it,
`ScanContingentLeadersAndLivingCounts` selects the living member with the
lowest `StandingId` numeric value, breaking ties on the lowest entity id.
Every earlier preset keeps the current lowest-entity-id rule, unmodified.

**Verification:** `MovementPresetFreezeTests` proves V1 through V4 unmoved.
A new unit test builds a contingent with hand-placed standings and asserts
the selected leader for: a single chief present, several chiefs present
(lowest id wins), no chief present (ranking survivor wins), and the chief
dead (leadership passes).

### B2. New goldens for V5

**Files:** `tests/Hukbo.Core.Tests/DeterminismTests.cs`,
`tests/Hukbo.Core.Tests/PersistentContingentTests.cs`

Record fresh seed-1 state-hash, event-hash, and outcome goldens for the
V4 combat preset paired with the V5 movement preset.

**Verification:** same-seed repeat, save and resume equivalence, and the
200-agent contract.

### B3. Gate again

```powershell
./scripts/verify.ps1
```

Same rules as A13.

## What this plan deliberately does not do

- No morale, rout, or fear. Deferred by `CLAUDE.md` section 9.
- No standing-aware deployment. Named as phase C in the design and not
  designed here.
- No promotion, experience, or in-battle standing change. Standing is written
  once at spawn and never mutated, exactly like the loadout.
- No campaign, economy, or polity state anywhere near `Hukbo.Core`.
- No change to the shipped default combat preset. That is a separate decision
  after V4 has been through the gate.
- No CI workflow. Verification here is local and deliberate.
