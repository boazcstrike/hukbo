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
