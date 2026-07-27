# Weapon Identity and Attributes — Implementation Plan

> **Archived: reference only.** This plan is complete and deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Date: 2026-07-27

**Status: COMPLETE — implemented and verified on 2026-07-27.** T1 through T28
and T30 are done. T29's smoke rows exist in
`docs/development/testing.md` and are `PENDING` by design: no agent may flip
one. See "Completion record" at the end of this document for what was
implemented, what moved, and the two decisions taken during implementation that
this plan did not anticipate.
Design: [`2026-07-27-weapon-identity-and-attributes-design.md`](2026-07-27-weapon-identity-and-attributes-design.md)
Evidence: [`docs/research/HISTORICAL_1500s_WEAPONS.md`](../research/HISTORICAL_1500s_WEAPONS.md)

**Goal:** Give every weapon a Filipino pair-form name and its own damage, reach,
and cooldown, split by grip so that a one-handed weapon fought solo is
mechanically distinct from the same weapon fought with a shield.

**Architecture:** New authoritative configuration in `Hukbo.Core.Combat`. A
`WeaponProfile` record holds damage, reach, and cooldown; a `WeaponGrip` decides
how many profiles a weapon declares; `ResolveWeaponProfile(WeaponId, ShieldId)`
is the single read path. Combat preset V1 is frozen and V2 is added alongside it.
Both hashes move.

If this plan and the design appear to disagree, stop the affected task and
resolve it in the design first.

## Nine questions

Per `SIMULATION-GAME-STANDARDS.md` section 10.

1. **User-visible outcome.** Four weapons carry Filipino pair-form labels and
   four distinct attribute sets instead of one shared set. A one-handed weapon
   appears twice in the army composition panel — solo and shielded — and the two
   fight differently. The battle event feed prints different damage under
   different weapon names, and appends the grip for one-handed weapons.
2. **Tick stage and state read/written.** No new tick stage. Stage 4 (target
   selection) and stage 8 (attack proposals) read reach through the shared reach
   helper; stage 8 reads damage and writes `AttackCooldownRemaining`. Reads
   `CombatLoadout` and the resolved `WeaponProfile`; writes nothing new to
   `AgentState` except the per-agent cooldown ceiling now sourced from the weapon
   rather than the scenario.
3. **Numeric units and bounds.** Damage is an integer in hit points, bounded by
   `Scenario`'s existing combat-value maximum. Reach is world units stored raw as
   `units * FixedPoint.Scale`, bounded below by `2 * BodyRadiusRaw` and above by
   the existing scenario maximum. Cooldown is an integer count of ticks, at least
   one. No same-tick conflict is introduced: damage still accumulates and applies
   simultaneously in stage 9, exactly as now.
4. **Total ordering and random-stream policy.** No new random draws. Grip and
   profiles are static configuration and are never drawn from. Roster order is
   fixed and declared — weapon order first, solo before paired within a weapon —
   and loadout assignment continues to iterate agents in ascending `EntityId`.
5. **Cache.** No cache. `ResolveWeaponProfile` is a lookup over hand-authored
   immutable data built once at ruleset construction. The existing
   `BuildEffectiveWeightTables` precomputation is untouched.
6. **Save, event, and version effect.** New preset version
   `PrecolonialPhilippinesV2 = 2`; V1 frozen and kept. Both the state hash and
   the event hash move — see task T13. `ClientSettings` schema version goes from
   2 to 3 with no migration, by the decision recorded in design section 5.3.
7. **Worst-case complexity and benchmark workload.** `ResolveWeaponProfile` is
   O(1) against a dictionary keyed on `(WeaponId, grip)` built at construction.
   No per-tick allocation is added. Benchmark workload is the canonical
   200-agent, 10 000-tick, seed-1 headless run, plus a 500-agent run.
8. **Spectator explanation.** Battle event feed carries the pair label, the grip
   suffix for one-handed weapons, and the damage. Agent inspector carries the
   pair label, evidence tier, grip, active profile, and all three attribute
   values.
9. **Tests that fail before and pass after.** Enumerated per task in the table
   below; consolidated in the verification criteria.

## Task list

| # | Task | Files | Depends on | Done when |
| --- | --- | --- | --- | --- |
| T1 | Rename `WeaponId` members: `GreatBlade`→`Kampilan`, `HeavyChopper`→`Wasay`, `ThrustingBlade`→`Kalis`, `Bolo`→`Itak`. Numeric values 1–4 **unchanged**. Update the XML doc comments to the new evidence tiers | `Combat/CombatIdentity.cs` and every referencing file across `src/` and `tests/` | — | Solution compiles; a grep for the four old identifiers returns nothing outside `docs/archives/`; numeric values untouched |
| T2 | Verify T1 moved no hash: run the seed-1 headless workload and confirm both hashes still match the pre-change baseline | — | T1 | `eventHash` and `stateHash` equal the recorded V1 baseline. A moved hash here means a value was renumbered — stop and fix |
| T3 | Add `WeaponGrip { TwoHanded = 1, OneHanded = 2 }` and the `WeaponProfile` record (damage, raw reach, cooldown ticks) | `Combat/CombatIdentity.cs`, new `Combat/WeaponProfile.cs` | — | Types exist, immutable, no dependency outside `Hukbo.Core` |
| T4 | Add `CombatPresetId.PrecolonialPhilippinesV2 = 2` | `Combat/CombatIdentity.cs` | — | Enum value added, V1 value untouched |
| T5 | Extend `CombatRuleset` to carry per-weapon grip and profiles, expose `ResolveWeaponProfile(WeaponId, ShieldId)`, and enforce the three construction invariants from design section 3.5 | `Combat/CombatRuleset.cs` | T3 | Two-handed weapon with a non-`None` shield throws; one-handed weapon missing its paired profile throws; every profile's reach is validated |
| T6 | Tests for T5 — both resolver branches per one-handed weapon, and all three invariant throws | `tests/Hukbo.Core.Tests/CombatRulesetTests.cs` | T5 | Tests fail before T5's logic, pass after |
| T7 | Author preset V2 in a new file: six profile rows and the six-entry roster from design sections 3.3 and 5.1. **Do not edit `PhilippineCombatPreset.cs`** — V1 is frozen | new `Combat/PhilippineCombatPresetV2.cs` | T3, T4, T5 | V2 builds; V1 file has zero diff |
| T8 | Register V2 | `Combat/CombatPresetRegistry.cs` | T4, T7 | Registry resolves both V1 and V2 |
| T9 | Move the reach floor validation from `Scenario.Validate` into per-profile validation, and keep the scenario-level check as the fallback path for preset-less scenarios | `Simulation/Scenario.cs`, `Combat/CombatRuleset.cs` | T5 | A preset containing any profile with reach at or below `2 * BodyRadiusRaw` throws at construction |
| T10 | Make the **shared reach helper** profile-aware so intent selection and attack gathering still cannot disagree | `Simulation/BattleSimulation.cs` | T5 | Both stages resolve reach through the same profile-aware helper; no second reach path exists |
| T11 | Source damage and cooldown from the resolved profile instead of `Scenario`; keep `Scenario` values as the preset-less fallback and as bounds | `Simulation/AgentState.cs`, `Simulation/BattleSimulation.cs`, `Simulation/Scenario.cs` | T5, T10 | A warrior's cooldown ceiling and blow damage come from its loadout |
| T12 | Extend `RosterCounts` handling for six entries and update its validation | `Simulation/Scenario.cs` | T7 | Six-length composition is accepted; wrong-length input is rejected with a clear message |
| T13 | Record new V2 golden expectations from an actual headless run. Keep the V1 goldens unedited | `tests/Hukbo.Core.Tests/DeterminismTests.cs` | T7–T12 | New goldens recorded from real output, never hand-authored; V1 goldens byte-identical |
| T14 | Reach-floor invariant test across **every profile of every registered preset**, not just V2 | `tests/Hukbo.Core.Tests/CombatRulesetTests.cs` | T9, T13 | Test enumerates the registry and fails if any profile is at or below the floor |
| T15 | Event formatter: pair labels plus grip suffix `(solo)` / `(shielded)` for one-handed weapons, nothing appended for two-handed | `Presentation/BattleEventFormatter.cs` | T1, T5 | Feed distinguishes solo from shielded kalis |
| T16 | Pawn appearance: rename `PawnWeaponRole.HeavyChopper` to the axe role and map it | `Presentation/PawnAppearance.cs`, `Presentation/PawnAppearanceFactory.cs` | T1 | Role renamed, mapping updated |
| T17 | Draw the wasay as an axe rather than a broad blade, including a low-detail-tier decision | `Rendering/PawnGeometry.cs`, `Rendering/PawnRenderer.cs` | T16 | Axe silhouette distinguishable from the kampilan at the detail tiers the renderer supports |
| T18 | Blood spray profile for the axe | `Rendering/BloodGeometry.cs` | T1 | Profile keyed on the renamed weapon |
| T19 | Audio identifiers: `GameSoundId.AttackHeavyChopper` → the axe slot, base name `attack-war-axe` | `Audio/AudioTypes.cs`, `Audio/SoundCueMapper.cs`, `Audio/SoundCatalog.cs` | T1 | Catalog resolves the new base name |
| T20 | Rename the `attack-heavy-chopper-*.wav` family to `attack-war-axe-*` with `git mv`; add a `GENERATED.md` note that these files were authored under the previous slot name and were not regenerated | `src/Hukbo.Client/Content/Audio/*.wav`, `Content/Audio/README.md`, `Content/Audio/GENERATED.md` | T19 | Every renamed file loads; no orphaned slot; no regeneration performed |
| T21 | `ClientSettings`: six counts, schema version 2 → 3, no migration, with the decision noted in the store | `Settings/ClientSettings.cs`, `Settings/ClientSettingsStore.cs` | T7 | Old settings files reset cleanly rather than throwing |
| T22 | Army composition panel: six rows grouped by weapon with the variant labelled, not six flat names | `UI/ArmyCompositionPanel.cs` | T21 | Six rows fit existing layout metrics, or the metric change is recorded |
| T23 | Agent inspector: pair label, evidence tier, grip, active profile, and all three attribute values | `UI/AgentInspectorContent.cs` | T5, T15 | Pure layout helpers only; no `GraphicsDevice` in tests |
| T24 | Client tests for T15, T16, T18, T19, T21, T22, T23 | `tests/Hukbo.Client.Tests/...` | T23 | All fail before, pass after; no test constructs `ArenaGame`, a graphics device, or a window |
| T25 | Amend `CLAUDE.md` section 7 to the pair-form policy in design section 2.4, including the hundred-year attestation clause | `CLAUDE.md` | — | Policy permits pair form and still forbids bare cultural labels |
| T26 | Update `HISTORICAL_1500s_WEAPONS.md` — "Named blade caution" and the closing cross-reference section — to match the amended policy, and record why the panabas was rejected | `docs/research/HISTORICAL_1500s_WEAPONS.md` | T25 | No sentence in the document still claims player-facing UI uses plain descriptors only |
| T27 | `SIMULATION-GAME-STANDARDS.md` cites `Great Blade` as the exemplar of the plain-descriptor policy in its last-stand subsection. Update that sentence to the amended pair-form policy without weakening the surrounding prohibition on named formations | `SIMULATION-GAME-STANDARDS.md` | T25 | The `Regrouping` rule still forbids cultural formation names; only the weapon exemplar changes |
| T28 | `AGENTS.md` is the companion file for non-Claude agents and currently carries no historical-accuracy or weapon-naming rule at all. Add a short section mirroring the amended `CLAUDE.md` section 7, including the hundred-year attestation clause | `AGENTS.md` | T25 | A non-Claude agent reading only `AGENTS.md` learns the pair-form rule and the evidence tiers; the two files agree |
| T29 | `hukbo-sound-effects` skill: update the slot example and the naming-policy line that lists the four plain descriptors | `.claude/skills/hukbo-sound-effects/SKILL.md` | T19, T20 | No stale `attack-great-blade` example, no stale four-descriptor list |
| T30 | `hukbo-determinism-change` skill: re-record the seed-1 baseline with V2's hashes and move the V1 pair into the skill's existing "Dead baseline" table. Check whether its "Hashed fields that force a new preset version" list needs the roster-length and profile fields | `.claude/skills/hukbo-determinism-change/SKILL.md` | T33 | Live baseline is V2's; V1 hashes are present but marked dead, not deleted |
| T31 | Update the "What is true of the game today" section of the plans index, and move the V2 rows to reflect implementation | `docs/plans/README.md` | T33 | Section describes the shipped build, not V1 |
| T32 | Benchmark: 200-agent and 500-agent runs, reporting mean time to kill per weapon profile and per-faction win rate for mirrored and asymmetric rosters | `docs/development/testing.md` | T13 | Numbers recorded. If the design's intended ordering has inverted, retune section 3.3 values and re-run — that is a tuning outcome, not a redesign |
| T33 | Run the canonical gate and paste the actual output, including the **new** hash values | `docs/development/testing.md` | T14, T24, T26, T27, T28, T32 | Five `[PASS]` stages; new hashes recorded verbatim from output |
| T34 | Add smoke rows for the pair labels, the grip suffix, the six-row composition panel, the inspector line, and the axe silhouette — all left `PENDING` | `docs/development/testing.md` | T33 | Rows exist and are `PENDING`. No agent flips a row |
| T35 | Review the complete diff | — | T34 | No enum renumbered, no frozen preset edited, no `Hukbo.Diagnostics` reference in `Hukbo.Core`, no console write outside the two `Program.cs` entry points |

## Verification criteria

Complete only when all of the following hold.

1. `./scripts/verify.ps1 -SkipBootstrap` passes all five stages, with the actual
   output pasted into `docs/development/testing.md`.
2. **Both hashes have moved**, and the new values are recorded from real output
   rather than predicted. The V1 baseline was `eventHash D379B60B2E30FFFC` and
   `stateHash 5BEBA7A68F69BE0D`; an unchanged hash after T7–T12 means the preset
   is not actually being read and the change is wrong.
3. T2 passed at the time it ran — the rename alone moved nothing.
4. V1's golden expectations and `PhilippineCombatPreset.cs` have zero diff.
5. `WeaponId` numeric values are still 1, 2, 3, 4.
6. Every profile in every registered preset has reach strictly greater than
   `2 * BodyRadiusRaw`.
7. A 500-agent run is reported.
8. No new per-tick allocation, verified against the existing allocation budget.
9. `Hukbo.Core` still references neither MonoGame nor `Hukbo.Diagnostics`.
10. No smoke-checklist row was flipped to `PASS` by an agent.
11. No live document still describes the pre-V2 setup as current. Grepping the
    repository for `Great Blade`, `Heavy Chopper`, `Thrusting Blade`,
    `Work Blade`, and `attack-heavy-chopper` returns hits only under
    `docs/archives/`, where staleness is deliberate and documented.
12. `CLAUDE.md` section 7 and `AGENTS.md` state the same weapon-naming policy.
    `CLAUDE.md` requires those two files to stay consistent, and this change is
    the first time `AGENTS.md` carries a historical-accuracy rule at all.

## Risks

**The itak's slack is halved.** The standards document notes that body diameter
being strictly less than attack range leaves four world units of slack, and that
this slack is what lets the rank behind a pressed rank strike past it. The paired
itak's reach of 10 leaves two units, not four. This is above the floor and legal,
but it is the narrowest margin in the game and it may measurably reduce how often
second-rank itak warriors contribute. T27 must report this specifically. If
second-rank contribution collapses, raise the paired itak reach rather than
lowering the body radius.

**T10 is the highest-risk task.** The shared reach helper exists precisely so
intent selection and attack gathering cannot disagree. Making it profile-aware
without accidentally creating a second reach path would reintroduce the exact
class of bug that helper was written to prevent — an agent marked `Attacking`
that cannot strike, or one that strikes while marked `Moving`.

**T20 renames binary assets.** Use `git mv` so history follows the files. A
missed rename produces a silently missing sound rather than a build error.

## Explicitly out of scope

Carried from the design so it cannot drift in during implementation.

- **The shield system.** V2 adds no shield, renames no shield, and changes no
  shield multiplier. `ShieldId` keeps `None = 1` and `TallHardwood = 2`, and
  `shieldMultipliers` and `BuildEffectiveWeightTables` are untouched. That work
  belongs to a separate agent and a separate document.
- **Attack combinations.** Preset V3, separate design.
- **Fighter level.** Introduced by V3, not here.
- **Armor.** `ArmorId` stays single-valued.
- **Combination values on `WeaponProfile`.** V3 adds those fields. V2 authors the
  record without them.
- **Regenerating any sound.** T20 renames files; it does not call
  `scripts/sfx.ps1` and does not contact ElevenLabs.
- **A settings migration.** Decided against in design section 5.3.
- **Damage escalation, miss chance, interrupts, directional defence, terrain.**

## Ordering note

Tasks T1–T14 are `Hukbo.Core` and must land before the shield agent begins any
implementation, because T1, T5, T7, and T9 rewrite `Combat/CombatIdentity.cs`,
`Combat/CombatRuleset.cs`, and the preset files that shield work also needs.
Design documents can proceed in parallel; code cannot.

## Completion record

Implemented and verified on 2026-07-27. Evidence is in
`docs/development/testing.md` under "Latest non-interactive result — weapon
identity and attributes (preset V2)".

### Verification criteria, checked

- [x] 1. `./scripts/verify.ps1` passed all five stages, 621 tests, output
  recorded.
- [x] 2. Both hashes moved. `eventHash` `D379B60B2E30FFFC` to
  `CF8C3EDBC59C3319`; `stateHash` `5BEBA7A68F69BE0D` to `C669281B67CF8871`.
- [x] 3. T2 passed when it ran: after the symbol rename alone the seed-1
  workload returned the baseline pair byte-identical, confirming the rename is
  hash-neutral.
- [x] 4. V1's `PhilippineCombatPreset.cs` diff is symbol renames only — no
  weight, roster entry, or numeric value changed — and its `ContentHash` still
  equals its pinned `0x59FB4CA563D87A49`. V2's is pinned at
  `0xE653F1802A447662`.
- [x] 5. `WeaponId` values are still 1, 2, 3, 4, asserted by a test.
- [x] 6. Every profile of every registered preset clears the reach floor,
  asserted over the registry rather than over V2 alone.
- [x] 7. 500-agent run reported: `Faction1Victory`, deterministic, p99 4.8983 ms.
- [x] 8. No new per-tick allocation. The event got *smaller* — see the
  packing note below.
- [x] 9. `Hukbo.Core` references neither MonoGame nor `Hukbo.Diagnostics`, and
  no `Console.Write*` exists outside the two `Program.cs` entry points.
- [x] 10. No smoke row was flipped. All ten V2 rows are `PENDING`.

### Two decisions this plan did not anticipate

**The attack event now carries the attacker's shield, and the combat context is
packed.** T15 needs the feed to distinguish a solo blow from a shielded one,
and the shield could not be recovered after the fact: a feed line is read long
after its tick, and loadout assignment depends on the scenario's roster counts,
so an entity ID alone does not determine it. Adding `ShieldId?` beside the
existing nullable `Weapon` and `HitLocation` pushed the collision allocation
budget from its 900,000-byte ceiling to 982,744 bytes. Rather than raise the
ceiling — the budget is a real contract — all three enums were packed into one
`int`. `BattleEvent` went from 80 bytes to 72, so it now carries three
combat-context fields in less space than it carried two.

**The shield block silhouette landed here rather than waiting for the shield
system.** Design section 3.8 requires grip to be visible in the pawn
silhouette, and V2 is the first preset to field a solo and a shielded warrior
*of the same weapon* simultaneously. Without the block those two are
indistinguishable on the battlefield while dealing different damage. It draws
at every detail tier, including the lowest, because a shield changes what the
warrior is rather than how ornamented they are. The later shield system adds a
second silhouette beside it; it does not have to build the mechanism.

### Notes for the shield work

- `ResolveWeaponProfile(WeaponId, ShieldId)` is the single seam, exactly as
  design section 3.7 promised. No call site reads a profile directly, so
  per-shield resolution replaces one rule without touching any of them.
- `CombatRuleset.MinimumProfileReachRawExclusive` and the per-profile
  validation are already registry-wide, so a shield reach multiplier that
  pushed a weapon under the floor would fail at construction.
- `CombatPresetRegistry.TryResolveGrip` exists so presentation can label a grip
  without hard-coding which weapons are two-handed. It answers from the first
  registered preset that declares attributes; if a future preset ever disagrees
  with another about a weapon's grip, that helper needs a preset argument and
  the call sites need plumbing.
- `weaponAttributes` is optional on the `CombatRuleset` constructor and a
  preset that declares none contributes nothing to `ContentHash` — not even a
  zero count. That is what keeps V1's hash exactly where it was, and it must
  stay that way.

### Not done, deliberately

- **T29's smoke rows are `PENDING`.** Ten rows were added covering the feed
  labels, the grip suffix, the inspector, the shield block at two detail tiers,
  the axe silhouette, the six-row composition panel, the settings reset, and
  the war-axe sound. Only a person at an interactive Windows desktop may flip
  one.
- **T27's per-weapon balance measurement was not performed.** The 500-agent
  and 200-agent runs are reported, but mean time to kill per weapon profile and
  per-faction win rate for asymmetric rosters were not measured. The attribute
  values in design section 3.3 are therefore still unvalidated tuning, which
  matters for the V3 and V4 ordering argument: preset V3 should not treat them
  as settled.
