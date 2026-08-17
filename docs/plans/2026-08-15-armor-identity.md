# Armor identity — plan

Companion to `docs/plans/2026-08-15-armor-identity-design.md`. That document
records the decisions and defines the model; this one carries the ordered task
list and the verification criteria. Where the two disagree, the design document
wins.

## Ground rules for this package

- **Armor becomes authoritative simulation state.** Three hashes move. This is
  not an increment on the existing cosmetic layer.
- **A new combat preset version is mandatory.** Existing presets stay registered
  and unmodified so their replays keep reproducing.
- **Every mitigation and pace number is invented gameplay tuning**, marked
  provisional in its code comment and its test. None is a historical
  measurement.
- **Tasks touching the same file run serially.** Two agents in one file is a
  merge conflict created on purpose.
- **The canonical gate is not delegated.** `./scripts/verify.ps1` runs once,
  after integration, and its real output is the evidence.

## Task list

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T1 | Append `None`, `QuiltedCotton`, `HideCorselet`, `RigidCuirass`, `ImportedIron` to `ArmorId`. Do not renumber or remove `LightOrganic = 1`. | `src/Hukbo.Core/Combat/CombatIdentity.cs` | Six members present with the values in design section 4; doc comment states the append-only contract | — | Core suite builds; a test asserts each member's numeric value |
| T2 | Add `ArmorWeightClass` (Light, Medium, Rigid) and the `ArmorId` → class mapping. | `src/Hukbo.Core/Combat/CombatIdentity.cs` | Every `ArmorId` maps to exactly one class; unmapped value throws | T1 | Test enumerates every `ArmorId` and asserts a class |
| T3 | Add the `(ArmorId, BodyPart)` mitigation table in integer basis points. Bake once at construction, validate totals the way `ValidateResolvedTotals` validates the shield table. | `src/Hukbo.Core/Combat/CombatRuleset.cs` | `ResolveArmorMitigation(ArmorId, BodyPart)` returns basis points; construction rejects an incomplete table | T1, T2 | Test asserts a value per pair and that a gap throws at construction |
| T4 | Apply mitigation at damage application — after hit-location resolution, before damage is written. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | An armored target takes strictly less damage than an unarmored one from the same attack | T3 | Test drives two identical attacks against `None` and `RigidCuirass` targets and asserts the difference |
| T5 | Register the new combat preset version with an armor-bearing roster. Leave every existing preset untouched. | `src/Hukbo.Core/Combat/` new preset file, `CombatIdentity.cs` (`CombatPresetId`) | New preset registered and selectable; all prior presets byte-identical | T3, T4 | Test asserts prior presets' content hashes are unchanged |
| T6 | Apply the weight-class modifier to movement. Do **not** expand the canonical loadout table to per-armor rows. | `src/Hukbo.Core/Movement/MovementRuleset.cs` | A Rigid-class warrior's pace differs from a Light-class one; loadout table keeps its current row count | T2 | Test asserts pace differs by class and that `CanonicalLoadoutIndex` still resolves the existing six tuples |
| T7 | Emit an event when armor changed a damage outcome. | `src/Hukbo.Core/` events; feed formatting in `src/Hukbo.Client/` | Event appears in the ordered stream and renders in the battle feed | T4 | Test asserts the event fires only when mitigation was non-zero; feed formatter test covers the new case |
| T8 | Re-baseline the five pinned content-hash literal sites, including the one embedded in fixture prose. | `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs`, `DeterminismTests.cs`, `Fixtures/seed-1-200-agents-preclash-digest.json` | All five carry the new value; the fixture's `stateHashSpecification` prose matches | T1–T7 | Core suite green; headless seed-1 run reproduces |
| T9 | Add a `GetArmorLabel` case for every new member and cover them in tests. | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | No `ArmorId` value reaches the throwing default | T1 | Test enumerates every `ArmorId` and asserts a label — this is the crash trap, cover it exhaustively |
| T10 | Build the `ArmorVariant` catalog with the ten rows in design section 5, each declaring baseline, label, evidence tier, and notes. | `src/Hukbo.Client/Presentation/Catalogs/` new file | Ten rows present; every row declares a valid baseline and one of the three evidence tiers | T1 | Test asserts every variant's baseline resolves and every tier is one of the three permitted values |
| T11 | Add the evidence-tier line to the inspector's armor row and render variant lines, following `BuildShieldVariantLines`. | `src/Hukbo.Client/UI/AgentInspectorContent.cs` | Armor row shows identity, variant, and tier | T9, T10 | Pure-helper test; no `GraphicsDevice`, no `SpriteBatch` |
| T12 | Have the regional appearance presets select variants. | `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.*.cs` | Each region selects from its plausible variants only | T10 | Test asserts every regional preset's variant selections resolve |
| T13 | Fold the five cosmetic `ArmorF*` entries into the variant catalog so one system remains. | `src/Hukbo.Client/Presentation/Catalogs/AppearanceComponentCatalog.cs`, `src/Hukbo.Client/Rendering/PawnRenderer.cs` | No armor appearance path bypasses the variant catalog | T10, T12 | Test asserts the drawn armor derives from the variant, not from a free-standing roll |
| T14 | Add the per-armor-type count setting: evenly distributed, mirrored across both teams. | `src/Hukbo.Client/Settings/ClientSettings.cs`, `ClientSettingsStore.cs`, scenario construction | Both teams field identical armor composition for a given setting | T5 | Test asserts the two teams' armor composition is identical for several settings values |
| T15 | Add smoke-checklist rows for the interactive behaviour. | `docs/development/smoke-checklist.md` | Rows added as `PENDING` | T11, T13, T14 | Not automatable — a human at an interactive desktop flips these, nobody else |
| T16 | Record the new baselines and the reasoning in the testing record. | `docs/development/testing.md` | New hashes recorded with the reason each moved | T8 | The gate's real output pasted, not summarised |

## Verification criteria

The package is done when all of the following hold, each with real output:

1. `./scripts/verify.ps1` is green — prerequisites and locked restore, format
   verification, Release build, Core plus GPU-independent Client tests, and the
   200-agent / 10,000-tick / seed-1 headless determinism workload.
2. **Both suites are run.** A `scripts/` or Core enum change can redden the
   Client suite, and `GetArmorLabel` is precisely that kind of trap.
3. Every prior combat preset's content hash is **unchanged**. If one moved, an
   existing preset was edited and that is a defect, not a re-baseline.
4. The three hashes that legitimately move — combat ruleset content, movement
   ruleset content, per-tick state — each have a recorded reason.
5. No test was weakened, no warning suppressed, no analyzer disabled to reach
   green.
6. Smoke rows added by T15 remain `PENDING` until a human runs them. No agent
   flips one.

7. **Battles still terminate.** The seed-1 workload must reach a decision inside
   its tick budget. Damage reduction lengthens fights, and this repository has
   already lived through a period in which every battle ran to a 10,000-tick
   standoff draw. If runs start drawing, the mitigation values in design section
   6.1 are too high and come down. **Extending the tick budget to accommodate
   armor is not an acceptable fix** — it hides the regression instead of
   reporting it.

## Sequencing

T1 and T2 are the foundation and run first. T3 through T7 are the Core
mechanic and run serially — they funnel through `CombatRuleset.cs`,
`BattleSimulation.cs`, and the preset registry, so parallelism there buys
nothing and costs conflicts. T9 through T14 are Client work and can run in
parallel with each other once T1 lands, provided each agent owns a
non-overlapping file set. T8 runs only after every Core change is complete,
because re-baselining before the last hash-moving edit means doing it twice.
T15 and T16 close the package.

## Out of scope

Head protection and any helmet identity. Armor durability, degradation, or
repair — a stock-and-consumption economy, forbidden until a gate authorizes it.
Retiring or renumbering `LightOrganic`. Filipino labels for any term the
research graded OPEN.

## Execution amendments, 2026-08-16

The task list above was written before the code was read end to end. Eight of
its assumptions turned out to be wrong or incomplete against the working tree.
Each correction below is recorded with the evidence that forced it, so a later
reader can tell a correction from a preference. Where an amendment contradicts
the task table, the amendment governs.

### A1. The new preset descends from V5, not from V6

`Scenario.CreateDefault` selects `PrecolonialPhilippinesV6`
(`src/Hukbo.Core/Simulation/Scenario.cs:117-118`), but `ArenaGame.BuildScenario`
overrides it with `PrecolonialPhilippinesV5`
(`src/Hukbo.Client/ArenaGame.cs:1642`), and five of the gate's six Hukbo
benchmark workloads also pass `Preset = 'PrecolonialPhilippinesV5'`. V5 is
therefore the preset that actually ships and the preset the gate actually
measures.

V6 descends from V4, not from V5. Its roster is four melee rows with no ranged
weapon and no shield
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV6.cs:250-256`), while V5's is
nine rows including three ranged weapons and two `ShieldId.TallHardwood` rows
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:311-320`). Taking the
highest-numbered preset as the parent would have silently removed ranged units
and shields from the player's build.

**V7 restates V5 verbatim and adds armor.**

### A2. T8 is inverted: the pinned hashes must not move

T8 asks for the pinned content-hash literals to be re-baselined. Verification
criterion 3 in this same document requires that every prior preset's content
hash be unchanged. Both cannot hold, and criterion 3 is the one that reflects
the repository's actual contract.

The resolution is the conditional fold this codebase already uses everywhere
else. `CombatRuleset.ComputeContentHash` folds rank levels only inside
`if (_rankLevels is not null)`, weapon attributes only inside
`if (_weaponAttributes is not null)`, and the clash profile only inside
`if (_declaredClashProfile is not null)`. `MovementRuleset` states the reason
outright in its own remarks: folding only inside the gate is what keeps the
pinned content-hash literals of every earlier preset exactly where they are.
The armor mitigation table is folded on the same terms, so a preset that
declares no mitigation folds nothing at all — not even a zero count.

**T8 becomes a verification task.** If a pinned literal moves, the fold was
written unconditionally and the fold is what gets fixed. A pinned literal is
never re-typed to reach green.

The count is also wrong. The task table says five sites carrying one hash; there
are six sites carrying five different hashes — V1 at
`CombatConfigurationTests.cs:177`, `DeterminismTests.cs:25` and `:135`, and
twice more inside `Fixtures/seed-1-200-agents-preclash-digest.json`; then V2 at
`DeterminismTests.cs:155`, V3 at `:178`, V4 at `:200`, and V5 at `:264`. All of
them must hold still.

### A3. The weight class attaches at agent spawn, not to a movement profile

T6 as written is invisible work. `MovementRuleset.LoadoutMovementProfiles` is
empty unless `UsesEquipmentRelativeFootwork` is true, and only three registered
presets set it — V6, V7 and V9. The client ships `EvasiveFootworkV14`
(`src/Hukbo.Client/Settings/ClientSettingsStore.cs:128-129`), for which the flag
is false, so a pace modifier hung on a loadout movement profile would never be
read by the shipped game and would never be measured by the gate. The baseline
gate run confirms it: every field of `movementMetrics` reports zero.

The live seam is `BattleSimulation.CreateAgent`, which resolves per-loadout
values once at spawn and writes them into immutable `AgentState` fields. It
already does exactly this for the weapon profile. Scaling `MovementSpeedRaw`
there by the armor's weight class reaches every movement preset, including the
one that ships, and it touches no movement ruleset data at all.

**Consequence: two hashes move, not three.** `MovementRuleset.ContentHash` does
not move, because nothing in the movement ruleset changes. The combat ruleset
content hash moves only for V7, and the per-tick state hash moves only for runs
that field armor.

### A4. The combo lethality prediction reads unmitigated damage

`BattleSimulation.ResolveComboTransition` decides whether a combo chain
continues by computing `target.HitPoints - source.DamagePerAttack <= 0`. That
value is the raw pre-mitigation damage. Once armor reduces what is actually
applied, this check over-predicts lethality and will break a combo chain on a
blow that armor stopped from killing.

Mitigation is applied at three sites, not two: the melee damage write, the
projectile arrival damage write, and this prediction. If the three disagree, the
divergence is silent. This is folded into T4.

### A5. Three evidence tiers in the design's variant table are too strong

The design's section 5 grades the corded rope corselet, the carabao-hide
corselet, and the hardwood corselet as `Documented`.
`docs/research/HISTORICAL_1500s_ARMOR.md` grades every underlying source for all
three as `Documented, form uncertain`. Under CLAUDE.md section 7, and under the
design document's own section 1, the research document wins on a question of
historical fact.

**All three ship as `DocumentedFormUncertain`.**

### A6. Armor is not currently drawn at all

The premise that a player already sees five distinct armor skins does not hold.
`armorWidthFactor` is a parameter on `PawnRenderer.Draw` defaulting to `1f` with
no call site anywhere passing anything else, and `CreateArmor` returns
`Rectangle.Empty` for any factor at or below `1f`. The armor material tone is a
fixed `DyePalette.BarkBrown` constant, with a comment saying no per-preset tone
exists yet.

So there is no live armor appearance path to reconcile. T13's requirement that
no appearance path bypass the variant catalog is satisfied by making the variant
catalog authoritative, not by unpicking a rendering path that never runs.
Restoring a visible difference on the pawn is a larger change than this package,
and the design's own section 10 does not require it: the inspector and the event
stream are what satisfy spectator discoverability.

### A7. Two tasks the plan does not contain

**T17 — the gate needs a seventh Hukbo workload.** `scripts/verify.ps1` appends
one benchmark invocation each time the client's shipped preset moves; it now
carries six, the last being V5 paired with `EvasiveFootworkV14`. When
`BuildScenario` selects V7, a seventh invocation for V7 paired with
`EvasiveFootworkV14` must be appended, or the gate stops measuring what ships.
`ScriptDefaultsTests` asserts the invocation count and pins each block's preset
pair, so that test's count and its method name move in the same change.

**T18 — a termination guard.** Verification criterion 7 requires that battles
still end, but nothing automates it. `RangedTerminationTests` is the precedent:
it drives seeds 1 through 20 under a named preset pair, requires both factions
to win some of them, and asserts a median decisive tick count. An
`ArmorTerminationTests` built the same way against V7 and `EvasiveFootworkV14`
turns criterion 7 from an observation into a test. If it fails, the section 6.1
mitigation values come down. Raising the tick budget to make it pass is not an
acceptable response.

### A8. The V7 roster is a cross product, and the settings schema bumps

Armor distribution is a client setting, so the roster has to expose armor as an
axis the setting can distribute across. V7's roster is therefore V5's nine
`(weapon, shield, rank)` rows crossed with the five fielded armor baselines, in
armor-minor order, giving forty-five rows. That order is part of the
content-hash contract.

The armor composition setting is a backward-compatible field addition, so
`ClientSettingsStore.SupportedSchemaVersion` moves from 13 to 14 and
`AcceptedSchemaVersions` widens to include it. The window widens; it never
slides, and no previously accepted version is dropped.
