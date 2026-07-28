# Warrior rank — implementation plan (Phase A)

Date: 2026-07-29
Design: [`2026-07-29-warrior-standing-design.md`](2026-07-29-warrior-standing-design.md)
Evidence: [`docs/research/HISTORICAL_1500s_RANKS.md`](../research/HISTORICAL_1500s_RANKS.md),
[`docs/research/ARMY-COMPOSITION.md`](../research/ARMY-COMPOSITION.md)

This document supersedes [`2026-07-29-warrior-standing.md`](2026-07-29-warrior-standing.md),
which was written before the naming decision and still says `StandingId`
throughout. Read this one; that one is kept only so the earlier reasoning
can be traced.

Phase A makes rank exist, makes it authoritative, and makes it visible.
Leadership — the rank-aware leader scan and the client leader marker — is a
separate, separately gated plan in
[`2026-07-29-leader-rank.md`](2026-07-29-leader-rank.md), and it does not
begin until Phase A has been through the canonical gate.

## Decisions this plan assumes

All five open questions were answered by the user on 2026-07-29 and are
recorded in full in the design document's Decisions section. In short:

1. **Naming is `Rank` everywhere**, in code and in player-facing text.
2. **Weapon-to-rank assignment** is the design's §6.1 table, as written.
3. **Phase B ships**, as its own separately verified step.
4. **The householder class is fielded**, four roster entries, labelled
   `Aliping Namamahay — Householder` in Plasencia's attested full form.
5. **Per-rank attributes run through existing systems only** — loadout,
   level and therefore combo depth, and leadership eligibility. There is no
   per-rank damage or hit-point number. Follower capacity is deferred to
   [`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md).

A task that finds itself guessing at a decision has hit a missing one and
must stop rather than pick.

## Group R — core identity and state

These tasks are strictly sequential. One implementer owns the whole group,
because the file sets genuinely overlap and splitting them would create a
merge conflict on purpose.

### R1. `RankId`, the `CombatLoadout` field, and the V4 enum value

**Files:** `src/Hukbo.Core/Combat/CombatIdentity.cs`

**Bucket:** CORE-IDENTITY

Add the five-value `RankId` enum with numeric values pinned 1 through 5, in
the comment style `WeaponId` already uses: one XML summary per value naming
the region, the evidence tier, and the source, plus the "do not renumber or
reorder" contract note on the enum itself. Copy the tier wording from the
research document rather than paraphrasing it.

The enum's own doc comment must state that `RankId` means social and legal
standing and never a delegated military office. This matters because
`docs/research/ARMY-COMPOSITION.md` §11.4 says "No rank enum", meaning no
graded military-office hierarchy, and a reader who meets `RankId` without
that sentence will think the two contradict each other.

| Value | Pair-form label | Region | Tier |
| --- | --- | --- | --- |
| `Datu = 1` | Datu — Chief | Tagalog and Visayan | Documented |
| `Maharlika = 2` | Maharlika — Sworn Freeman | Tagalog | Documented |
| `Timawa = 3` | Timawa — Bound Freeman | Visayan | Documented |
| `AlipingNamamahay = 4` | Aliping Namamahay — Householder | Tagalog | Documented |
| `Ayuey = 5` | Ayuey — Household Dependent | Visayan | Documented, form uncertain |

`Ayuey` is declared but not rostered, on the same principle preset V3
already uses for its unreachable paired weapon profiles.

In the same task, add `CombatPresetId.PrecolonialPhilippinesV4 = 4`.
`WeaponProfileTests.cs:32` and `:192` auto-iterate
`Enum.GetValues<CombatPresetId>()`, so the enum value and its registry arm
must land together or the build is red between them — the registry arm is
in R5, so R5 must follow immediately and neither is independently gateable.

`CombatLoadout` becomes
`(WeaponId Weapon, ArmorId Armor, ShieldId Shield, RankId Rank = RankId.Timawa)`.
**The default value is required, not cosmetic.** There are 67 `CombatLoadout`
construction sites across 24 test files, plus three preset roster arrays
using target-typed `new(...)` at `PhilippineCombatPreset.cs:132`,
`PhilippineCombatPresetV2.cs:214`, and `PhilippineCombatPresetV3.cs:213`.
The default keeps every one of them compiling untouched, and it is also the
mechanism that keeps V1 through V3 resolving every warrior to a single
`RankId` so their hashes cannot move once R4 gates the fold.

**Depends on:** nothing.

**Done when:** the solution builds clean under `TreatWarningsAsErrors` and
every existing test passes with no test file edited.

**Verified by:** `./scripts/test.ps1 -Configuration Release`. The V1, V2,
and V3 content-hash freeze assertions in `CombatConfigurationTests` and
`DeterminismTests` must pass unchanged. If any of them moves here, the
default-value reasoning is wrong and the work stops.

### R2. Thread rank onto agent state and the view

**Files:** `src/Hukbo.Core/Simulation/AgentState.cs`,
`src/Hukbo.Core/Simulation/AgentView.cs`,
`src/Hukbo.Core/Simulation/BattleSimulation.cs`

**Bucket:** CORE-IDENTITY

`AgentState.Rank` is a get-only property written once in the constructor
from the resolved loadout. It is not a separate constructor parameter — the
loadout already carries it. `AgentView` gains the same field and `ToView`
passes it through.

**Depends on:** R1.

**Done when:** every spawned agent's `Rank` equals its resolved roster
entry's rank.

**Verified by:** `BattleSimulationTests`, plus a new assertion using a
scenario with explicit `RosterCounts`.

### R3. Per-rank levels in the ruleset

**Files:** `src/Hukbo.Core/Combat/CombatRuleset.cs`

**Bucket:** HASHING-PRESETS

Add an optional `IReadOnlyDictionary<RankId, int>? rankLevels` constructor
parameter and a `ResolveLevel(RankId)` accessor, mirroring the existing
optional `weaponAttributes` pattern exactly: nullable field, a
`HasRankLevels` predicate, a defensive copy, validation that every level is
at least 1, and validation that every rank the roster actually fields has a
level.

`BattleSimulation` uses `ResolveLevel` when the preset declares levels and
falls back to `Scenario.PlaceholderFighterLevel` when it does not.

Proposed levels, all provisional tuning values with no evidentiary standing
whatsoever: Datu 3, Maharlika 2, Timawa 2, Aliping Namamahay 1, Ayuey 1.
These may not be cited as historical and the inspector must not present them
as such.

**Depends on:** R1.

**Done when:** both validations reject bad input and `ComboChainTests`
passes unchanged.

**Verified by:** `./scripts/test.ps1 -Configuration Release`.

### R4. Fold rank into the content hash and the state hash

**Files:** `src/Hukbo.Core/Determinism/StateHasher.cs`

**Bucket:** HASHING-PRESETS

The content-hash fold lives in `CombatRuleset.ComputeContentHash` and is
therefore part of R3's file; the state-hash fold lives here. Both follow the
same gating technique that already keeps V1's hash intact after V2 added
weapon attributes and V3 added the clash profile:

- The roster fold gains `loadout.Rank` as a fourth value, contributed only
  when the preset declares rank levels.
- A new rank-level block is folded after every existing block, contributed
  only when the preset declares one.
- `StateHasher` folds `AgentState.Rank`, gated the same way.

**This is the load-bearing determinism check of Phase A.**

**Depends on:** R3.

**Done when:** V1, V2, and V3 hashes are byte-identical to their current
pinned values.

**Verified by:** the pinned literals must not move —
`DeterminismTests.cs:24` `0x59FB4CA563D87A49UL`, `:55`
`0xAE3BEC9EE7BCEDFCUL`, `:151` `0x10AB1CC226AB3636UL`, `:172`
`0xCD790E489293B304UL`. Plus a new test building two rulesets with identical
rank data supplied in different dictionary order, asserting equal hashes.
Plus `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` under preset
V3 reproducing the recorded seed-1 baseline exactly. Follow the
`hukbo-determinism-change` skill if it does not.

### R5. Preset `PrecolonialPhilippinesV4`

**Files:** `src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs` (new),
`src/Hukbo.Core/Combat/CombatPresetRegistry.cs`

**Bucket:** HASHING-PRESETS

Restate every V3 value rather than referencing V3, exactly as V3 restates
V2. The only newly authored data is the rank on each roster entry and the
per-rank level table.

| Roster index | Rank | Weapon | Shield |
| ---: | --- | --- | --- |
| 0 | Datu | Kampilan | None |
| 1 | Maharlika | Wasay | None |
| 2 | Timawa | Kalis | None |
| 3 | Aliping Namamahay | Itak | None |

The class remarks must carry, in the same terms V3 already uses, that the
weapon-to-rank assignment is a provisional gameplay tuning choice and not a
historical claim. No source assigns a weapon to a social class.

`IsRegistered` and `Get` both gain the V4 switch arm. This unblocks
`WeaponProfileTests.cs:32` and `:192`, which go red the moment R1 adds the
enum value.

**Depends on:** R4.

**Done when:** V4 resolves, and V1 through V3 remain byte-identical.

**Verified by:** a new V4 content-hash golden and a new seed-1 state-hash
and event-hash golden, both recomputed from the built code and never
calculated by hand. `PhilippineCombatIntegrationTests` passes.

## Group C — client presentation

Starts once R1 has landed, because the label catalog needs `RankId` to
exist. Owns only `Hukbo.Client` files, so it runs in parallel with the tail
of Group R.

### C1. Rank label catalog

**Files:** `src/Hukbo.Client/Presentation/Catalogs/RankLabelCatalog.cs` (new)

**Bucket:** CLIENT-PRESENTATION

One entry per `RankId`, carrying the pair-form label, a region scope string,
and a `VisualEvidenceTier`. Reuse the existing tier enum and the existing
catalog-entry conventions; do not introduce a parallel vocabulary.

Because the roster fields a dependent class, the `AlipingNamamahay` entry
must additionally carry the note that fielding a household dependent in a
battle line is a reconstruction — `HISTORICAL_1500s_RANKS.md` lines 314-319
require this to be stated wherever the class is shown.

**Depends on:** R1.

**Done when:** every `RankId` value has an entry.

**Verified by:** a catalog test asserting an entry exists for every value,
that no label is a bare Filipino name, and that each label's descriptor
matches the research document's table exactly.

### C2. Inspector rank line

**Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs`,
`src/Hukbo.Client/UI/AgentInspectorPanel.cs`

**Bucket:** CLIENT-PRESENTATION

Add `FormatRankLine` following the pure-helper pattern the
`hukbo-client-ui` skill describes. Insert it at index 2 of the lower lines,
immediately after the contingent line, because rank is a persistent roster
property like contingent rather than a per-tick battlefield fact.

The line shows the pair-form label, the region, and the evidence tier. For
`AlipingNamamahay` it also shows the reconstruction note from C1.

**Depends on:** C1.

**Done when:** the line renders for all five ranks.

**Verified by:** `AgentInspectorContentTests` gains one assertion per rank
value, plus one asserting the row budget is not exceeded. No `ArenaGame`,
graphics device, or sprite batch is constructed.

### C3. Composition panel — decision and scope

**Files:** `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`

**Bucket:** CLIENT-PRESENTATION

`CategoryLabels` at `ArmyCompositionPanel.cs:112` is a hardcoded six-string
array shaped to V2's roster, and `ArmyCompositionPanelTests.cs:33` pins the
panel to `PrecolonialPhilippinesV2`. The panel does not become rank-aware
for free.

**Decision:** retarget the panel in this pass. The design names three
independent discoverability surfaces — the inspector line, the composition
panel categories, and the observable leader change — and dropping one leaves
`SIMULATION-GAME-STANDARDS.md` §10 question 8 answered by two. Two is still
a true answer, but the composition panel is the only surface a spectator
reads *before* the battle starts, which is where a roster claim belongs.

Category labels name the rank alongside the weapon, so the pre-battle roster
is legible without opening the inspector.

**Depends on:** C1, R5.

**Done when:** categories carry rank labels and the panel resolves against
V4.

**Verified by:** `ArmyCompositionPanelTests` and
`MenuOverlayArmyCompositionTests` pass, with an assertion pinning the text of
each category. Note that retargeting the panel's pinned preset is itself a
behavior change to an existing test — update it deliberately, do not weaken
it.

## Group D — research document reconciliation

**Files:** `docs/research/ARMY-COMPOSITION.md`,
`docs/research/HISTORICAL_1500s_RANKS.md`

**Bucket:** RESEARCH-DOCS

Fully independent of all code. Runs in parallel with Group R from the start.
Touches no other file.

### D1. Resolve the label and tier disagreements

`ARMY-COMPOSITION.md` §9 and `HISTORICAL_1500s_RANKS.md` disagree on three
points that the design has already resolved in RANKS's favour. Amend §9 to
match, and state the reason in each case rather than silently editing:

- `Maharlika — Free Warrior` becomes `Maharlika — Sworn Freeman`. The
  descriptor should encode the reciprocal feast-and-spoils obligation both
  sources document, not merely free status plus a combat role.
- `Timawa — Sworn Follower` becomes `Timawa — Bound Freeman`. "Bound"
  matches Loarca's reciprocal service-for-defence description.
- The `ayuey`, `tumaranpoc`, `tomataban` row is tiered **Documented** in §9
  and **Documented, form uncertain** in RANKS. Downgrade §9 to match:
  ARMY-COMPOSITION's own §1 definition of that tier — the institution is
  attested but the exact term is uncertain — describes this row precisely,
  since the spelling rests on a single Spanish transliteration with unsettled
  modern orthography.

### D2. Resolve the `aliping namamahay` UI-clearance conflict

§9 marks the term "not recommended for UI" with no written rationale. RANKS
clears `Namamahay — Householder` at line 283 on the ground that the
descriptor reflects the documented fact that the class held its own houses,
land, and gold.

The user decided on 2026-07-29 that the class is fielded and that the
player-facing label is Plasencia's attested full form,
`Aliping Namamahay — Householder`. Amend §9 to record that decision and its
reason, replacing the bare "not recommended for UI" cell.

Record the surviving caution, which is independent of the label question and
comes from RANKS lines 314-319: whether a household dependent was ever put
in a battle line is an inference either way, so a roster that fields the
class must say in the inspector that this is a reconstruction. C1 and C2
implement that.

### D3. Reword §11.4 so it stops contradicting `RankId`

§11.4 currently reads "No rank enum. There is no attested rank below
'chief', and inventing one would violate §7". With a `RankId` enum shipping,
this reads as a flat self-contradiction, and it also contradicts
ARMY-COMPOSITION's own §4, which catalogues the social classes in detail.

Reword it to say what it means: no graded **military-office** hierarchy —
no captain, sergeant, lieutenant, or corporal, and no named unit below a
chief's personal following. Social and legal standing, which the document
itself documents in §4, is a different thing and is what `RankId` carries.

### D4. Add the missing cross-references, both directions

- Into `ARMY-COMPOSITION.md` §7: Morga's passage that a chief "more
  courageous than others in war… enjoyed more followers and men; and the
  others were under his leadership, even if they were chiefs". It directly
  strengthens §11.1 and §11.2's own argument for unequal, leader-earned
  contingent sizes, and it is the design's stated basis for rank-aware
  leader selection.
- Into `ARMY-COMPOSITION.md` §4.1: the maharlika misconception from RANKS —
  the explicit rejection of the twentieth-century "nobility" or "royalty"
  reading.
- Into `HISTORICAL_1500s_RANKS.md`: the "timawa trap" framing from
  ARMY-COMPOSITION §4.3. RANKS discusses the identical Loarca-versus-Morga
  collision but never names it or gives the do-not-conflate instruction.
- Into `ARMY-COMPOSITION.md` §9: the recorded Loarca dependency-grade
  figures from RANKS's Visayas table, or a citation pointing at them. §9
  currently summarises the obligation without the values.

### D5. Close the reconciliation note

`ARMY-COMPOSITION.md` carries a note near lines 25-33 saying the two
documents have "not yet been reconciled term by term". Once D1 through D4
have landed, replace it with a statement that reconciliation happened on
2026-07-29, naming what was resolved and what deliberately remains open.

**Done when:** no claim is tiered differently in the two documents without
the difference being explained in writing.

**Verified by:** reading. There is no test for prose. The reviewer checks
that every amended row cites its source and that no tier changed without a
stated reason.

## Group G — gate

### G1. Run the canonical gate

**Files:** none

```powershell
./scripts/verify.ps1
```

Run once, after Groups R, C, and D are integrated. Paste the real output. No
sub-agent report and no partial run substitutes for it. Report the 500-agent
result `SIMULATION-GAME-STANDARDS.md` §10 requires, and leave every manual
smoke-checklist row in `docs/development/testing.md` at its honest value.

**Depends on:** R5, C3, D5.

## File ownership

Every file below is owned by exactly one task. No file appears twice.

| File | Owned by |
| --- | --- |
| `src/Hukbo.Core/Combat/CombatIdentity.cs` | R1 |
| `src/Hukbo.Core/Simulation/AgentState.cs` | R2 |
| `src/Hukbo.Core/Simulation/AgentView.cs` | R2 |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | R2 |
| `src/Hukbo.Core/Combat/CombatRuleset.cs` | R3 |
| `src/Hukbo.Core/Determinism/StateHasher.cs` | R4 |
| `src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs` | R5 |
| `src/Hukbo.Core/Combat/CombatPresetRegistry.cs` | R5 |
| `src/Hukbo.Client/Presentation/Catalogs/RankLabelCatalog.cs` | C1 |
| `src/Hukbo.Client/UI/AgentInspectorContent.cs` | C2 |
| `src/Hukbo.Client/UI/AgentInspectorPanel.cs` | C2 |
| `src/Hukbo.Client/UI/ArmyCompositionPanel.cs` | C3 |
| `docs/research/ARMY-COMPOSITION.md` | D1–D5 |
| `docs/research/HISTORICAL_1500s_RANKS.md` | D1–D5 |

Test files are edited by the task whose behavior they verify, and are listed
in that task's verification paragraph rather than in this table.

Groups R and C both reach `Hukbo.Client` and `Hukbo.Core` respectively and
never touch each other's files. Group D touches neither. Group R's internal
tasks are strictly sequential and are executed by a single implementer.

## What this plan deliberately does not do

- **No rank-aware leader selection.** That is
  [`2026-07-29-leader-rank.md`](2026-07-29-leader-rank.md), gated behind
  Phase A's own gate run so that the state hash moves for one reason at a
  time.
- **No per-rank damage, hit-point, hit-rate, or endurance number.** No
  source grades fighting ability by social class.
- **No follower capacity and no contingent-shape change.** Deferred to
  [`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md).
- **No morale, fear, or rout**, under any name.
- **No change to the shipped default combat preset.** That is a separate
  decision, after V4 has been through the gate.
- **No campaign, economy, booty, or polity state** anywhere near
  `Hukbo.Core`.
- **No CI workflow.** Verification here is local and deliberate.
