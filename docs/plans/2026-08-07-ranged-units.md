# Ranged units — plan

Design: [`2026-08-07-ranged-units-design.md`](2026-08-07-ranged-units-design.md).
Branch: `ranged-units`, in `.claude/worktrees/ranged-units`, based on `main` at
`ae7bf04`.

## 1. What this package is

Hukbo's warriors all fight at arm's length. This package adds warriors who fight
at a distance. Concretely it adds three ranged weapons — `Bangkaw — Long Spear`
in its thrown role, `Busog — War Bow`, and the `Imported Arquebus` — a projectile
that takes a measurable number of ticks to arrive, a movement rule that lets a
warrior deliberately stop short of contact and stay there, procedural pose
animation for the drawing and loosing of each weapon, sixty generated sound
files, and two narrowly scoped pieces of work on the battle-termination standoff.

It is a battle-layer change and nothing else. No campaign state, no economy, no
map generation, and no morale model is introduced, and `Hukbo.Core` gains no
reference to MonoGame, the filesystem, the wall clock, or `Hukbo.Diagnostics`.

Everything this plan schedules is specified in the design document. Where this
plan and the design disagree about *what* is being built, the design wins and the
disagreement is a defect in this plan. Where they disagree about *the order* work
happens in and *which files* a given agent may touch, this plan wins, because
that is what a plan is for.

## 2. Authorization

**The design document does not authorize implementation. This plan does.**

`CLAUDE.md` section 6 makes that split explicit: a design document is written
first and states what a feature is, and an ordered plan document has to follow it
before any code is written. The design document says so in its own first
paragraph. This document is that plan, and from here on an implementing agent is
authorized to write code — but only the code its own task row names, only in the
files its own task row names.

Two standing prohibitions were lifted by the user on 2026-08-07 for this package
and for nothing else:

- `SIMULATION-GAME-STANDARDS.md:27` lists projectiles and ammunition among the
  deferred layers. **The projectile half is lifted. The ammunition half is
  not.**
- `CLAUDE.md` section 9 forbids starting projectile ammunition before an
  authorizing gate. **That clause survives intact**; ammunition, quiver sizes,
  and resupply stay out of scope and are recorded in section 8 below.

Terrain, cover, pathfinding, morale, diplomacy, needs, economy, persistent
worlds, multiplayer, and mods stay exactly as deferred as they were, and
rigid-body physics stays forbidden. RU-01 is the task that makes the written
record of what is deferred true again.

## 3. The known-red window, and why the tree goes red on purpose

The moment RU-03 appends three members to `WeaponId`, two tests in
`tests/Hukbo.Client.Tests/SoundCatalogTests.cs` go red and stay red:
`EveryDefinedWeapon_HasAnAttackSlot` and `EveryDefinedWeapon_HasAShieldClashSlot`
(`SoundCatalogTests.cs:51-98`). They enumerate `Enum.GetValues<WeaponId>()` and
fail until `SoundCatalog` and `SoundCueMapper` have an arm for every new weapon.
Their own comments say this is the designed safety net rather than a defect, and
the design document restates it in section 9.7.

**Correction, recorded 2026-08-07 after RU-03 landed: the two paragraphs above
undercount the window by a factor of fourteen.** RU-03 appended the three
`WeaponId` members and the two `MovementPresetId` members exactly as its row
required, touching only the two enum files, and the measured result was
**twenty-nine red tests, not two** — eighteen in `Hukbo.Core.Tests` and eleven
in `Hukbo.Client.Tests`. This was measured twice: once by the RU-03 agent and
once independently by the orchestrator running both suites on branch `ru-03` at
`5f2e5f6`, against a base run on `f02d012` that was green at 2614 of 2614 Core
and 3121 of 3121 Client.

Every one of the twenty-nine fails through the same mechanism the plan already
accepts for the two named tests: an exhaustive `Enum.GetValues<TId>()` sweep fed
into a ruleset, a registry, or a factory that has no arm for the new value yet.
The Core failures raise
`ArgumentOutOfRangeException: Unknown weapon identity for this combat ruleset.
Actual value was Bangkaw.` from `CombatRuleset.ResolveWeaponWeight`
(`CombatRuleset.cs:213`); the Client failures raise the same exception type from
`PawnAppearanceFactory.ToWeaponRole`. None is a regression, and none was
weakened, skipped, or re-pinned.

The eighteen in `Hukbo.Core.Tests`: `ClashResolverTests` —
`Resolve_TallHardwoodBlocksMoreOftenThanItParries`,
`Resolve_MatchesTheNaiveReferenceAcrossTheWholeRosterMatrix`,
`SplitWeaponChannel_HardPlusSoftEqualsTheRescaledWeaponChannel`,
`Resolve_NeverBlocksWithoutAShield`, `Clamp_NeverExceedsTheCeiling`;
`CombatConfigurationTests` —
`PresetV2_RowMeansMatchTheDesignedTotalInterceptionMatrix` (six parameter cases)
and `WithClashProfile_PreservesEveryFieldExceptTheProfile`;
`WeaponProfileTests` — `EveryCombatPresetIdIsRegistered` and
`EveryProfileOfEveryRegisteredPresetClearsTheReachFloor`;
`HitLocationResolverTests.WeaponOverrides_CanChangeTheResolvedBodyPartForTheSameTuple`;
`PhilippineCombatIntegrationTests` —
`LargeFixedTupleMatrix_TallHardwoodShieldLowersChestAndAbdomenFrequency` and
`LargeFixedTupleMatrix_FourWeaponProfilesProduceDistinctTargetDistributions`;
and
`BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`,
which is the one driven by the unregistered `MovementPresetId` members rather
than by `WeaponId`.

The eleven in `Hukbo.Client.Tests`: the two `SoundCatalogTests` named above,
plus `PawnAppearanceFactoryTests` —
`WeaponLabels_NeverUseTheRejectedPanabasName`,
`EveryWeaponCarriesAnEvidenceNote`,
`Create_NeverDerivesWeaponRoleFromEntityIdAlone`,
`WeaponLabels_NeverCarryACulturalNameWithoutItsDescriptor`,
`Create_MapsAllFourWeaponIdsToDistinctSilhouettes`; `PawnGeometryTests` —
`CreateWithPoseBlindBounds_MatchesCreateAndGetBoundsAcrossTheInputGrid`,
`Create_RenderLoopCallMatchesThePawnRendererParameterDefaults`,
`PoseBlindPrefix_MatchesCreateAndGetBoundsAcrossTheInputGrid`; and
`ConservativePawnCullTests.GeometryAppearances_CoverEverythingTheFactoryCanProduce`.

This changes the risk profile of the window rather than the plan's design. A red
tree of twenty-nine tests is a far worse place to hide a genuine regression than
a red tree of two, so the tasks that add the missing arms should be scheduled as
early as their dependencies allow, and the count above is the baseline any later
agent compares against.

**Fourteen of the twenty-nine had no owner anywhere in this plan's original
thirty-three tasks, and two tasks were added to close them.** Tracing each red
test to the task that closes it produced the following, and the last two rows are
why RU-34 and RU-35 now exist:

| Red tests | Closed by | Was it owned? |
| --- | --- | --- |
| `CombatConfigurationTests`, seven facts | RU-12 | Yes, RU-12 owns the file |
| `WeaponProfileTests`, two facts | RU-07 | Yes |
| `SoundCatalogTests`, two facts | RU-09 and RU-14 | Yes |
| `PawnGeometryTests`, three facts | RU-22 | Yes |
| `BattleSimulationTests`, the leader fact | RU-21 and RU-30, by registering V8 and V9 | Yes, no test edit needed |
| `BattleSimulationTests.AgentIntentNumericValuesArePinned`, the thirtieth | Fixed on the integration branch, see below | No, and it only appears once RU-03 and RU-04 are merged together |
| `ClashResolverTests` (5), `HitLocationResolverTests` (1), `PhilippineCombatIntegrationTests` (2) | **RU-34, added** | **No — no task listed any of those three files** |
| `PawnAppearanceFactoryTests` (5), `ConservativePawnCullTests` (1) | **RU-35, added** | **No — and `PawnAppearanceFactory.cs` itself was unowned** |

### Second correction, recorded after RU-10 landed: the Client baseline is 34, not 11

RU-10 added the three `PawnWeaponRole` members its row required, touching only its
five authorized files, and the Client suite went from eleven red to **thirty-four**.
Measured independently of the task agent on branch `ru-10` at `42002d7`. Thirty-two
of the thirty-four are the same `ArgumentOutOfRangeException` from an exhaustive
`switch` over `PawnWeaponRole` that grew from four members to seven; the other two
are the known `SoundCatalogTests` pair. The new baseline by class:

| Class | Red | Change | Owner |
| --- | --- | --- | --- |
| `PawnGeometryTests` | 11 | +8 | RU-22 |
| `ConservativePawnCullTests` | 10 | +9 | **RU-22 and RU-35 together — see below** |
| `AgentInspectorContentTests` | 6 | +6 | **RU-16, scope amended below** |
| `PawnAppearanceFactoryTests` | 5 | unchanged | RU-35 |
| `SoundCatalogTests` | 2 | unchanged | RU-14 |

Two ownership corrections follow from it, and both were made before the affected
tasks were dispatched rather than after they got stuck.

**`ConservativePawnCullTests.cs` moves from RU-35 to RU-22.** Its failures throw
from two different places — `PawnAppearanceFactory.ToWeaponRole`, which is RU-35's,
and `PawnGeometry.CreateWeaponLayout`, which is RU-22's — confirmed by reading the
stack traces. RU-35 therefore cannot make that file green no matter how completely
it does its own job, and a task whose acceptance criterion is unreachable is a task
that will report BLOCKED after doing correct work. RU-22 owns `PawnGeometry.cs` and
already carries the cull radius as an explicit concern, so the file belongs with it.
RU-35 keeps `PawnAppearanceFactory.cs` and `PawnAppearanceFactoryTests.cs`.

**RU-16 gains three arms in `AgentInspectorContent.GetLaterOrProvisionalForms`**
(`AgentInspectorContent.cs:797-817`). That switch is exhaustive over
`PawnWeaponRole` with a throwing default, and it is the same designed safety net as
everywhere else in this window. RU-16 already owns that file for the `Holding`
reason code, so folding three arms into it is strictly better than a new task
contending for the same file. None of the three ranged weapons has a later or
provisional form to show, so each arm returns an empty sequence — but that is an
evidence claim under `CLAUDE.md` section 7, and RU-16 must state it as one rather
than treating an empty return as a default.

**A thirtieth red test exists that no single branch could see.**
`BattleSimulationTests.AgentIntentNumericValuesArePinned` pins each
`AgentIntent` member's numeric value and then pins the member count at five.
RU-04's `Holding = 5` makes the count six, so the fact fails — but only on a tree
that has RU-04 merged, and RU-04's own verification was scoped to
`BattleEventTests.cs`, so its branch never ran the suite that contains this
assertion. It appeared for the first time when wave 1 was merged into
`ranged-units`, which is the argument for integrating and running the full suite
after every wave rather than only at RU-33.

It was fixed on the integration branch by *extending* the pin rather than
loosening it: `Assert.Equal(5, (int)AgentIntent.Holding)` was added and the count
raised to six. The guard's purpose is to make an enum addition deliberate and
visible, and an authorized addition that updates the pin satisfies that purpose;
deleting the count assertion would not have. Core returned to eighteen red with
that one line, and `./scripts/format.ps1 -Verify` reports `[PASS]`.

The two unowned groups are not the same kind of problem as the owned ones, and
that is the important part. The owned failures close when a registry or a catalog
gains an arm for a new weapon. The eight in RU-34's group cannot close that way
at all: they build their ruleset from `PrecolonialPhilippinesV1` or `V2`, and
those presets are frozen by this plan's own rollback rules, so they will never
declare a ranged weapon. Only a change to how the tests enumerate weapons can
make them pass. An agent that assumed RU-12 would fix them would have waited
through five waves for a green that was never coming.

Three consequences bind every agent working this plan.

1. **The window closes progressively, not all at once at RU-14.** Between RU-03
   and the tasks that add the missing arms, the two suites are expected to be
   red on exactly the twenty-nine tests enumerated above and on nothing else. An
   agent that finds a red test *not on that list* has found a real regression;
   an agent that finds one *on* it has found the designed safety net. Compare
   against the list, never against a remembered count.
2. **Nobody weakens or skips those two tests to get green.** `CLAUDE.md` section
   5 forbids weakening a test, a warning, or an analyzer to get green, and these
   two are the mechanism by which a new weapon cannot ship silently mute.
3. **Nothing is integrated to `main` while the window is open.** The canonical
   gate (RU-33) runs once, at the end, and it must be green in full.

`WeaponVisualCatalogTests` behaves the same way for the visual catalog
(`:262` fails if a defined weapon falls through to a category default), which
RU-10 closes.

## 4. Task list

Thirty-three tasks. Each row names its files, and **the files named in a row are
the only files that row's agent may edit.** A task marked PARALLEL-SAFE has a
file set disjoint from every other task in its wave. A task marked SERIAL shares
at least one file with a task that must land before or after it, and its
"Depends on" column says which and why.

`./scripts/verify.ps1` is the canonical gate. It appears on exactly one row,
RU-33, and it runs once after integration. It is not delegated to a sub-agent and
no sub-agent's report substitutes for its real output.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| RU-01 | Correct two stale documentation figures and one stale deferral list. The enforced per-tick allocation ceiling is **16,384 bytes per 1,000 warm ticks with a 4,096-byte growth tolerance at 12 agents per faction** (`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:393-395`); the 900,000-byte figure recorded at `SIMULATION-GAME-STANDARDS.md:877` and repeated at `docs/development/testing.md:1997` is stale by a factor of fifty-five and an implementer who reads it will believe there is room for a per-projectile heap object. Also amend the deferred-layer list at `SIMULATION-GAME-STANDARDS.md:27` and `CLAUDE.md` section 9 (mirrored in `AGENTS.md`) so that projectiles and projectile flight time read as authorized for this package while ammunition, terrain, cover, pathfinding, and morale read as still deferred. Where the 900,000 figure appears inside a dated historical run record rather than as a live claim, annotate it as superseded rather than rewriting the record. | `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, `CLAUDE.md`, `AGENTS.md` | No live sentence in either document states 900,000 bytes as the current ceiling; both state 16,384 / 4,096 and cite `BattleSimulationTests.cs:393-395`; the deferral lists in all three documents distinguish projectiles from ammunition and name this package as the authorization. | — | `Select-String -Path SIMULATION-GAME-STANDARDS.md,docs/development/testing.md -Pattern '900,?000'` returns only lines explicitly labelled as superseded historical measurements |
| RU-02 | **Verify first, then correct.** The weapon descriptions at `docs/research/HISTORICAL_1500s_WEAPONS.md` lines 41, 43, 44 and 46 are attributed to "a 1569 relation" / "a 1569 account", and line 210 describes volume III as covering "1569-1576". A research agent reported that these descriptions are Diego de Artieda's *Relation of the Western Islands Called Filipinas*, which Blair and Robertson volume III dates **1573**. **That report was never independently confirmed and this task must confirm it before editing anything.** Read Blair and Robertson volume III (Project Gutenberg 13616) and `docs/research/ranged/2026-08-07-RANGED-WEAPONS-EVIDENCE.md`, and establish from a source actually read that the lance, bow, dagger, and blowgun descriptions at those four lines are Artieda's and that Blair and Robertson date the document 1573. **If the attribution cannot be confirmed from a source you actually read, edit nothing and report BLOCKED naming the check that failed. Guessing a date into a historical-accuracy document is a worse outcome than leaving the error in place.** | `docs/research/HISTORICAL_1500s_WEAPONS.md` | Either: every one of the five sites names Artieda's *Relation of the Western Islands Called Filipinas* with the Blair and Robertson date of 1573, the source consulted is named in the task report, and the volume-III span at line 210 is corrected to match; or the task is reported BLOCKED with the specific verification that failed and the file is unchanged. | — | `Select-String -Path docs/research/HISTORICAL_1500s_WEAPONS.md -Pattern '1569'` returns no line attributing a weapon description to a 1569 relation; the named source appears in section 9 of this plan |
| RU-03 | The three ranged `WeaponId` members and the three new preset identities, appended so no existing numeric value moves. `Bangkaw = 5`, `Busog = 6`, `Arquebus = 7` on `WeaponId`, each with a doc comment carrying its confidence tier and its earliest attestation exactly as the four existing members do. `CombatPresetId.PrecolonialPhilippinesV5` with a doc comment naming exactly what it changes relative to V4 and stating that V1 through V4 stay registered and unmodified. Two new `MovementPresetId` values after `EquipmentRelativeFootworkV7 = 7`: `RangedStandoffV8 = 8` for the hold rule and `MonotoneAllyClearanceV9 = 9` for F-B. Enum values only — no registry arm, no preset body. | `src/Hukbo.Core/Combat/CombatIdentity.cs`, `src/Hukbo.Core/Movement/MovementPresetId.cs` | The solution builds; the three `WeaponId` members and all three preset identities exist with doc comments; no existing enum value has moved. The two `SoundCatalogTests` weapon-coverage tests are now red, which is expected — see section 3. | — | `./scripts/format.ps1 -Verify` and a Release build; `dotnet test` on `Hukbo.Core.Tests` |
| RU-04 | `AgentIntent.Holding = 5`, `BattleEventKind.Release = 5`, and `BattleEventKind.Miss = 6`, all appended. `Release` carries the flight time in ticks in its `Value`; `Miss` carries zero. Both are non-attack kinds and must be constructible through `BattleEvent.NonAttack`, which forces all combat context to null — do not relax `NonAttack`. The `Holding` doc comment states, as a contract, that it has exactly one producer, means "this warrior is at the distance it wants to fight from and is deliberately not advancing", and may never be written by a rejection, a collision, a blocked proposal, or a failed route search. No emission site is added by this task. | `src/Hukbo.Core/Simulation/AgentIntent.cs`, `src/Hukbo.Core/Simulation/BattleEvent.cs`, `tests/Hukbo.Core.Tests/BattleEventTests.cs` | `BattleEventTests.cs:288`'s every-non-attack-kind-is-constructible fact covers `Release` and `Miss` and passes; `BattleSimulationTests.cs:1661`'s rule that non-attack events carry no weapon and no hit location still passes; no existing enum value has moved; no state hash or event hash changes, because nothing emits either kind yet. | — | `dotnet test` on `tests/Hukbo.Core.Tests/BattleEventTests.cs` |
| RU-05 | Preset selection for the headless runner and the benchmark script, so a determinism or benchmark workload can be run on a named combat preset and a named movement preset instead of only on `Scenario.CreateDefault`. Add the two options, thread them into scenario construction, and echo the two chosen preset identities into `RunReport` so a recorded run states which presets produced it. This is the task that makes RU-06's F-A measurement and RU-29's ranged determinism workload possible at all. | `src/Hukbo.Headless/Program.cs`, `src/Hukbo.Headless/HeadlessRunner.cs`, `src/Hukbo.Headless/RunReport.cs`, `scripts/benchmark.ps1` | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` with no preset options reproduces the recorded seed-1 baseline byte for byte; the same command naming `PersistentContingentsV4` and `PrecolonialPhilippinesV4` explicitly produces the identical report; an unregistered preset name exits non-zero with a named error rather than falling back to a default. | — | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`, with the report's `stateHash` and `eventHash` compared against the recorded baseline |
| RU-06 | **F-A.** Split `refuseAgentTicks` into four rejection-reason counters — no candidates built, step endpoint rejected, direct candidate omitted, lane not clear — incremented at the four exit sites in `TryProposeEquipmentRoute` (`BattleSimulation.cs:2056`, `:2062`, `:2068`, `:2079`) and surfaced on `MovementBehaviorMetrics` and in `RunReport`. `MovementBehaviorMetrics` is derived observability that reaches neither hash, so **no new preset version is required and no pinned artifact may move**. This is the measurement F-B is judged by, which is why it comes first. | `src/Hukbo.Core/Simulation/MovementBehaviorMetrics.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Headless/RunReport.cs`, `tests/Hukbo.Core.Tests/Movement/MovementBehaviorMetricsTests.cs` | A 200-agent, seed-1, 10,000-tick run on `EquipmentRelativeFootworkV6` reports four counters that **sum to exactly 1,140,221**, reproducing the recorded `refuseAgentTicks`; the seed-1 `stateHash` and `eventHash` on the shipped default are unchanged from the recorded baseline. | RU-05 (owns `RunReport.cs`; F-A adds fields to it) | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` on `EquipmentRelativeFootworkV6`, with the four counters and their sum recorded in section 9 |
| RU-07 | `WeaponProfile` gains three ranged fields — a projectile speed, a standoff distance in raw fixed-point world units, and a per-weapon flight-tick ceiling — all zero for a melee weapon. `CombatRuleset` validates them at construction: a roster entry whose weapon is ranged must declare all three; a standoff distance must sit strictly inside that weapon's own `AttackRangeRaw`, because a warrior standing beyond its own reach can never shoot and one standing exactly at its reach is one collision nudge from being unable to; a melee entry must declare all three as zero. Values are `PROVISIONAL` gameplay tuning, commented as such, and none of them may be cited back into `docs/research/HISTORICAL_1500s_WEAPONS.md` as a measurement. | `src/Hukbo.Core/Combat/WeaponProfile.cs`, `src/Hukbo.Core/Combat/CombatRuleset.cs`, `tests/Hukbo.Core.Tests/WeaponProfileTests.cs` | A ranged profile missing any of the three throws at construction with a named message; a standoff distance at or beyond the profile's own reach throws; a melee profile with a non-zero standoff throws; all four existing presets still construct and `WeaponProfileTests.cs:252`, `:272`, and `:290` are unmodified and pass. | RU-03 | `dotnet test` on `tests/Hukbo.Core.Tests/WeaponProfileTests.cs` |
| RU-08 | Feed formatter cases for `Release` and `Miss`, so the battle event log renders both. `BattleEventFormatterTests` requires the feed to render every event kind, so a missing case is a red test rather than a blank row. The release line states that a shot has left the weapon and how many ticks it will be in the air; the miss line states that the shot spent itself without landing. | `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`, `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs` | Every `BattleEventKind` including the two new ones renders a non-empty, distinct line; the 200-event feed cap is untouched. | RU-04 | `dotnet test` on `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs` |
| RU-09 | Thirteen new `GameSoundId` members, appended so no existing value moves, and their thirteen `SoundCatalog` entries: `release-bangkaw`, `release-busog`, `release-arquebus`, `attack-bangkaw`, `attack-busog`, `attack-arquebus`, `clash-shield-bangkaw`, `clash-shield-busog`, `clash-shield-arquebus`, `miss-bangkaw`, `miss-busog`, `miss-arquebus`, `misfire-arquebus`. `GetBaseName` gains thirteen arms; `IsHitLocationDriven` is extended to the three new `attack-` slots and to nothing else. The `attack-` prefix on the three impact slots is load-bearing and must not be renamed to `impact-`: `scripts/sfx.ps1`'s `-Class` guard at `:626-628` tests that literal string prefix, so renaming would silently disable the guard for the only slots that need it. | `src/Hukbo.Client/Audio/AudioTypes.cs`, `src/Hukbo.Client/Audio/SoundCatalog.cs`, `tests/Hukbo.Client.Tests/SoundCatalogTests.cs` | The catalog holds twenty-six slots; `GetFileName_IsUniqueLowercaseKebabWavForEverySlot` passes for all twenty-six; `IsHitLocationDriven` is true for exactly the seven attack slots and false for the other nineteen; the two weapon-coverage tests are still red pending RU-14, and no other client test is. | RU-03, RU-04 | `dotnet test` on `tests/Hukbo.Client.Tests/SoundCatalogTests.cs` |
| RU-10 | Three new `PawnWeaponRole` members and their `WeaponVisualCatalog` entries: a tint list, an own silhouette, an evidence tier, an evidence note, and a pair-form label for each of the three weapons — `Bangkaw — Long Spear`, `Busog — War Bow`, and `Imported Arquebus` with its `IMPORTED` badge. The catalog is where `CLAUDE.md` section 7's naming policy is mechanically enforced, so every entry carries a defined tier and a non-empty note. Also carry whatever `AppearanceRosterContractTests` and `DetailTierBoundaryTests` need for the three new entries to classify and to stay visually differentiable. **This task adds no geometry**; the four `PawnGeometry` switch expressions are RU-22's. | `src/Hukbo.Client/Presentation/PawnAppearance.cs`, `src/Hukbo.Client/Presentation/Catalogs/WeaponVisualCatalog.cs`, `tests/Hukbo.Client.Tests/WeaponVisualCatalogTests.cs`, `tests/Hukbo.Client.Tests/AppearanceRosterContractTests.cs`, `tests/Hukbo.Client.Tests/DetailTierBoundaryTests.cs` | `WeaponVisualCatalogTests.cs:262` passes — no defined weapon falls through to a category default; `:289` finds a non-empty tint list and `:224` an own silhouette for each of the three; the per-weapon evidence-tier facts at `:141` and `:589-601` cover all seven weapons; `DetailTierBoundaryTests.cs:161`'s sweep classifies the three new entries. | RU-03 | `dotnet test` on `tests/Hukbo.Client.Tests/WeaponVisualCatalogTests.cs` and `AppearanceRosterContractTests.cs` |
| RU-11 | The Client-side in-flight projectile store: a fixed-capacity, tick-advanced store fed by `Release` events, holding the origin, the target entity, the launch tick, and the flight-tick count, and exposing an interpolated screen-space endpoint pair per live projectile. It is presentation only — the simulation holds its own authoritative pool and this copy exists to be drawn. It is advanced by tick rather than by a clock, so it survives pause and playback speed with no scaling, exactly as the gait system does. Entries are dropped on arrival, on round reset, and when the store is full. New files only; no wiring into `ArenaGame` (that is RU-25). | `src/Hukbo.Client/Presentation/ProjectileFlightSystem.cs` (new), `src/Hukbo.Client/Rendering/ProjectileFlight.cs` (new), `tests/Hukbo.Client.Tests/ProjectileFlightSystemTests.cs` (new) | Ingesting a `Release` event with a flight of N adds one entry that is live for N ticks and gone on the N+1th; the store never grows past its capacity and allocates nothing per ingest; `Clear` on round reset empties it; two ingests of the same tick do not double-count. | RU-04 | `dotnet test` on `tests/Hukbo.Client.Tests/ProjectileFlightSystemTests.cs` |
| RU-12 | The V5 combat preset. A whole new file with **every value restated rather than referenced**, in the discipline `PhilippineCombatPresetV4.cs:14-17` states — V4's four melee rows and its shared target-weight profile restated verbatim, plus three ranged rows. All three ranged rows are `WeaponGrip.TwoHanded` with `ShieldId.None`, which satisfies the three existing grip rules with no test change. New arms in both switches of `CombatPresetRegistry` — `IsRegistered` and `Get`. Ranged reach is expressed as a multiple of the longest melee reach — 3x for the Bangkaw, 5x for the Busog, 7x for the Arquebus — and the shot intervals preserve the ordering Bangkaw < Busog << Arquebus. **Every one of those numbers is a provisional starting point that RU-24 owns calibrating; none is a historical measurement and none may be cited as one.** | `src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs` (new), `src/Hukbo.Core/Combat/CombatPresetRegistry.cs`, `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | `CombatPresetRegistry.Get(PrecolonialPhilippinesV5)` returns a seven-entry ruleset instead of throwing; `WeaponProfileTests.cs:32` passes; V1 through V4's content hashes are byte-identical to their pinned values; pairing V5 with `EquipmentRelativeFootworkV6` or `V7` throws at construction, which is the correct outcome and needs no new guard. | RU-07 | `dotnet test` on `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` and `WeaponProfileTests.cs` |
| RU-13 | `AgentView.RangedPhase` and `AgentView.RangedPhaseTicksRemaining`, both **derived projections and both hash-neutral**. A six-member `RangedPhase` enum with `None = 0` so `default` is neutral: `None`, `Ready`, `Load`, `Draw`, `Release`, `Recover`. `UpdateViews` derives both from the pair `(AttackCooldownRemaining, AttackCooldownTicks)` the tick has already produced, with per-weapon phase shares. **Nothing new is stored, nothing new is hashed, nothing new is snapshotted**, and the derivation may not query anything the tick would not otherwise make. This is a deliberate divergence from the pose research, which asked for real per-agent state; section 8.1 of the design explains it and "what could make this design wrong" item 2 records the bet. | `src/Hukbo.Core/Simulation/AgentView.cs`, `src/Hukbo.Core/Simulation/RangedPhase.cs` (new), `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/AgentViewTests.cs` | A melee agent's view reports `RangedPhase.None` at every cooldown value; a ranged agent's view walks `Release`, `Recover`, `Load`, `Draw`, `Ready` as its cooldown counts down and reports a strictly decreasing `RangedPhaseTicksRemaining` within a phase; the seed-1 200-agent 10,000-tick `stateHash` and `eventHash` are unchanged from the recorded baseline, which is the evidence that this is a projection and not state. | RU-06 (shares `BattleSimulation.cs`; RU-06 lands first — see section 5) | `dotnet test` on `tests/Hukbo.Core.Tests/AgentViewTests.cs`, plus `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` showing an unchanged baseline |
| RU-14 | The audio routing, including **the `Evaded` fix**. Three arms in `MapWeapon`, three in `MapShieldClash`, a branch that maps a `Release` event to the launching weapon's release slot, and a new `MapMiss` covering both of its triggers: the `Miss` event of RU-04, and an `Evaded` resolution **for a ranged attacker only**. Melee weapons keep the shared impact cue — this is the design's scope and it is deliberate, because fixing melee too would rewrite a shipped behaviour that `SIMULATION-GAME-STANDARDS.md:884-905` describes and that `SoundCueMapperTests.cs:52` pins with `[InlineData(AttackResolution.Evaded)]`. **That inline datum stays and stays passing for the four melee weapons**, and a new ranged case is added beside it. In `SIMULATION-GAME-STANDARDS.md` section 14, add a **ranged row** to the spectator-channel table rather than editing the melee row, and record honestly that a melee blow meeting empty air still plays a flesh impact. | `src/Hukbo.Client/Audio/SoundCueMapper.cs`, `tests/Hukbo.Client.Tests/SoundCueMapperTests.cs`, `SIMULATION-GAME-STANDARDS.md` | Every one of the seven `WeaponId` values maps to an attack slot and to a shield-clash slot, closing the known-red window of section 3; an `Evaded` resolution on a ranged weapon maps to that weapon's `miss-` slot; an `Evaded` resolution on each of the four melee weapons still maps to the weapon impact slot and `SoundCueMapperTests.cs:52` is unmodified; the standards table carries a ranged row and the melee row is untouched. | RU-01 (shares `SIMULATION-GAME-STANDARDS.md`), RU-04, RU-09 | `dotnet test` on `tests/Hukbo.Client.Tests/SoundCueMapperTests.cs` and `SoundCatalogTests.cs` |
| RU-15 | Authoring-tool support for sixty generations, before a person spends money. Thirteen default-prompt entries in `scripts/sfx.ps1`; an optional nested per-hit-class prompt table on a hit-location driven slot, resolved at `:584-590`, so the twenty-one class-scoped ranged files become reviewable table rows rather than command-line strings typed by hand; and the `-List` counting fix — `-List` currently probes the bare `<slot>.wav` (`:558-564`), so it already reports nine of thirteen shipped slots as `MISSING`, and at twenty-six slots it would report twenty-two of twenty-six as `MISSING` after every file had been generated and paid for. Count matching files with the same prefix rules the game uses. Also update the two audio documents that are the naming contract a person reads. `sfx.ps1` is an authoring tool that no test and no gate touches; do not add it to any pipeline, and never write `ELEVENLABS_API_KEY` into a tracked file, into output, or into a commit message. | `scripts/sfx.ps1`, `src/Hukbo.Client/Content/Audio/README.md`, `src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` | `./scripts/sfx.ps1 -List` reports every shipped slot that has at least one variant file as present, and reports `MISSING` only for slots with no file at all; `-Class` is accepted on the seven `attack-` slots and rejected by name on the other nineteen; the per-class prompt table resolves a prompt for each of the twenty-one class-scoped ranged files; no network call is made by this task. | RU-09 | `./scripts/sfx.ps1 -List` (no generation, no spend) |
| RU-16 | The `Holding` reason code where a spectator can read it. The agent inspector shows `AgentIntent.Holding` as a first-class reason code beside the existing five, reading "holding at range" and distinguishable from "blocked". A per-faction count of holding warriors is added to the battle report, since no live per-faction intent readout exists today. This is one of the two defences against risk 8 — a feature that looks like the standoff bug it hides — and it is not optional. | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`, `src/Hukbo.Client/Presentation/BattleReport.cs`, `src/Hukbo.Client/Presentation/BattleReportAccumulator.cs`, `tests/Hukbo.Client.Tests/BattleReportAccumulatorTests.cs` | An agent view carrying `AgentIntent.Holding` renders a distinct inspector line; an agent whose movement was `Blocked` renders the existing blocked line and never the holding one; the battle report carries a per-faction holding count. | RU-04 | `dotnet test` on `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` |
| RU-17 | **The projectile pool.** A `readonly record struct` — source entity, target entity, launch tick, ticks remaining, origin X and Y raw, weapon, and the damage recorded at launch, every field an integer or a small enum and nothing a reference — in a flat array sized once at construction from a new `Scenario.MaximumProjectilesInFlight`, with a live count, append at the count, and order-preserving compaction on removal. Iteration by index, never by enumerator. **A launch at the ceiling is refused, the shot does not occur, the cooldown is not charged, and a derived counter records the refusal.** New pass A0 at the head of `GatherAndCommitAttacks` advances every countdown and resolves arrivals through `HitLocationResolver.Resolve` and `ClashResolver.Resolve` **folding the launch tick, not the impact tick**, buffering into `_attackProposals` exactly as the melee path does; a projectile whose target died emits `Miss` and is removed. The gather pass gains one branch: a ranged weapon launches instead of resolving, emits `Release` carrying the flight ticks, and charges its cooldown on launch. `BattleSnapshot` gains the pool. `StateHasher.Compute` gains **one conditional tail after the per-agent loop**, gated on a ruleset capability ("this combat ruleset fields at least one ranged weapon") in the shape of the rank-levels gate at `StateHasher.cs:136-139` — a ruleset with no ranged entry folds nothing at all, not even a zero. No new tick stage. No new `AttackResolution` value. No new random-stream domain tag. No line of sight, no friendly fire, no ammunition. A code comment at the launch site records that a Phase 1 projectile passes through allies and through every enemy but its target. | `src/Hukbo.Core/Simulation/Projectile.cs` (new), `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/BattleSnapshot.cs`, `src/Hukbo.Core/Determinism/StateHasher.cs`, `src/Hukbo.Core/Simulation/Scenario.cs` | A projectile launched on tick N resolves on tick N + flight and on no other tick; the clash roll at impact equals the roll the same tuple produces at the launch tick; a launch at the pool ceiling is refused with no cooldown charged and the refusal counted; a V4 scenario's seed-1 `stateHash` and `eventHash` are byte-identical to the recorded baseline; a warm 1,000-tick window with projectiles in flight allocates nothing. | RU-12, RU-13 (shares `BattleSimulation.cs`) | `dotnet test` on `Hukbo.Core.Tests`; `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` on V4 showing an unchanged baseline |
| RU-18 | `RangedPose`, `RangedGeometry`, and `RangedPoseResolver` — new files only, in the pure-helper shape `SwingPoseResolver` and `GaitPoseResolver` share. `RangedPose` is an `internal readonly record struct` whose `default` is neutral. `RangedGeometry` is an `internal static class` of keyframe mathematics over value types, differentiated per weapon: the Bangkaw cocks back past the shoulder with a negative torso lean and its weapon line shortens or vanishes through `Recover`; the Busog holds a near-vertical stave that barely rotates while a `DrawTension` channel rises through `Draw`, holds, and **snaps to zero on `Release`**; the Arquebus spends most of its long interval in a multi-beat `Load` and **holds** its `Release`. `RangedPoseResolver.Resolve` fills a caller-owned dictionary and never allocates one; an agent with no ranged action gets **no entry, never a neutral one**; `TryGetPose` exists so the draw loop's lookup is covered by a test; and the resolver **must** copy `SwingPoseResolver`'s early-out when nothing is active rather than `GaitPoseResolver`'s omission of it. Also a pure, tested helper deciding that a ranged pose suppresses the swing pose for that pawn on that frame. Every pose constant is commented as a provisional reconstruction for gameplay legibility, not a measurement. No animation store — the phase arrives on the view every tick. | `src/Hukbo.Client/Rendering/RangedPose.cs` (new), `src/Hukbo.Client/Rendering/RangedGeometry.cs` (new), `src/Hukbo.Client/Rendering/RangedPoseResolver.cs` (new), `tests/Hukbo.Client.Tests/RangedGeometryTests.cs` (new), `tests/Hukbo.Client.Tests/RangedPoseResolverTests.cs` (new) | Each of the three weapons resolves a visibly distinct pose at each of the five phases; `RangedPhase.None` resolves no entry; resolving twice into one buffer replaces rather than accumulates; the resolver returns immediately when no agent has a ranged phase; the swing-suppression helper returns true exactly when a ranged pose exists for that pawn. | RU-13 (the `AgentView` projection fields must exist), RU-10 | `dotnet test` on `tests/Hukbo.Client.Tests/RangedGeometryTests.cs` and `RangedPoseResolverTests.cs` |
| RU-19 | `SoundDirector` takes the agent view list alongside the event list, so a classless `Release` event can resolve its weapon from the source agent's `AgentView.Loadout`. `UpdateViews` writes a view for every agent including the dead, so the lookup succeeds even for a launcher killed on the same tick. **Do not relax `BattleEvent.NonAttack` to carry a weapon on a release event** — that is the rejected option in design section 5.3, and this signature change is the adopted one. The one guard that matters more than any other test in this area is `SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation` (`:41-74`): the director derives the hit class from `IsHitLocationDriven`, never from the event, and every new classless ranged slot depends on it. | `src/Hukbo.Client/Audio/SoundDirector.cs`, `tests/Hukbo.Client.Tests/SoundDirectorTests.cs` | A `Release` event from a Busog-armed agent produces the `release-busog` cue with a null hit class; the same event from an agent absent from the view list produces no cue and does not throw; `SoundDirectorTests.cs:41-74` is unmodified and passes. | RU-14 | `dotnet test` on `tests/Hukbo.Client.Tests/SoundDirectorTests.cs` |
| RU-20 | Update the mix-analysis harness's replica mapping to match the client's twenty-six-slot mapping and **re-run it at 500 agents**, recording the peak level and the per-slot peak. `docs/research/SOUND-CAPACITY-MEASUREMENTS.md:468-473` requires that if the client's mapping changes this harness changes with it, and adding thirteen slots changes it. The measured headroom at 500 agents is **−0.2 dBFS**, in exactly the configuration that forced `CueVolume` down from 0.8 to 0.65, and the release cue fires on one hundred per cent of shots and lands on one slot per weapon — a concentration the melee mix does not have. **This is the task that must produce a number before anyone pays for sixty files.** If the per-slot cap of sixteen binds, the fix is a raised `DefaultMaximumPerSound`, which moves `SoundCueBudgetTests.cs:59-79` and is a deliberate measured change recorded here, not a guess. | `tools/Hukbo.Tools.MixAnalysis/CueSchedule.cs`, `tools/Hukbo.Tools.MixAnalysis/Mixer.cs`, `tools/Hukbo.Tools.MixAnalysis/Program.cs`, `docs/research/SOUND-CAPACITY-MEASUREMENTS.md` | The harness's slot list, hit-class mapping, fallback chain, and variant draw match the client's for all twenty-six slots; a 500-agent rendering completes and its peak dBFS, its per-slot peak, its total cue count, and its suppression count are recorded in section 9 of this plan. | RU-14 | `dotnet run` on `tools/Hukbo.Tools.MixAnalysis` at 500 agents, with the numbers pasted into section 9 |
| RU-21 | **The hold arm**, and the `RangedStandoffV8` movement preset that carries it. The preset is registered as a **verbatim restatement of `PersistentContingentsV4`'s values plus one rule**, with `usesEquipmentRelativeFootwork: false` and `appliesPressureInterrupt: false`, following the restate-rather-than-edit precedent at `MovementPresetRegistry.cs:163-169`. The rule has three parts, all in the legacy body of `GatherMovementProposals` and **not** in the equipment-relative pipeline: an agent whose target is beyond its weapon's standoff distance builds an ordinary pursuit proposal with `stopShortRaw` set to that standoff distance instead of `2 * BodyRadiusRaw`; an agent whose target is **at or inside** the standoff distance **proposes no movement at all** and is assigned `AgentIntent.Holding`; and a melee weapon, whose standoff distance is zero, behaves byte-identically to V4. `AgentIntent.Holding` gets **exactly one producer in the whole codebase** — the hold arm — and is never written by a rejection, a collision, a blocked proposal, or a failed route search. `PersistentContingentsV4` is not touched and stays the shipped default. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `src/Hukbo.Core/Movement/MovementRuleset.cs`, `tests/Hukbo.Core.Tests/Movement/RangedStandoffTests.cs` (new) | Under `RangedStandoffV8` a melee-only roster produces the same trajectories, the same `stateHash`, and the same `eventHash` as `PersistentContingentsV4` on seed 1; a ranged warrior whose target is inside its standoff distance reports `AgentIntent.Holding`, proposes nothing, and does not move; a warrior whose proposal was rejected reports `Moving`, never `Holding`; a holding warrior never accumulates a blocked streak and the stall escape never fires on it. | RU-17 (shares `BattleSimulation.cs`), RU-07 | `dotnet test` on `tests/Hukbo.Core.Tests/Movement/RangedStandoffTests.cs` |
| RU-22 | `PawnGeometry` learns the three new weapon roles and the third pose. Three new arms in each of the four `switch` expressions over `PawnWeaponRole` — the start, end, and padding switches in `CreateWeaponLayout` (`:1496-1522`), `CreateWeaponThickness` (`:1547-1562`), and `CreateSecondaryBounds` (`:1591-1612`) — noting that the first four throw `ArgumentOutOfRangeException` on an unrecognised role, so a missed arm is a runtime throw rather than a compile error. A third nullable pose parameter threaded to `CompletePosedLayout` and summed into `CreateBodyAnchor`'s existing two lean contributions, which is additive and needs no mutual-exclusion rule for gait. **At most one new rectangle per ranged pawn**, reusing the existing `SecondaryEquipmentBounds` slot the Wasay's axe head already proves out — the bow's stave is the weapon line and the nocked arrow is the secondary rectangle. **The maximum extension any ranged pose reaches must fit inside the existing weapon-line envelope**: the Kalis's upward reach of 24.2 units sizes `ConservativePawnCull`'s radius, and the cull actually in the path is pose-blind on purpose, so a longer line lets a pawn escape its own cull rectangle and be clipped at the panel edge. | `src/Hukbo.Client/Rendering/PawnGeometry.cs`, `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | Every pinned rectangle in `PawnGeometryTests` is unchanged; `PawnGeometryTests.cs:2089` and `:2338` still show the cull rectangle not moving with the pose; every ranged pose at every phase stays inside the existing weapon-line envelope; no `PawnWeaponRole` value throws from any of the four switches. | RU-18 | `dotnet test` on `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` |
| RU-23 | Quad and cull accounting, moved deliberately with the arithmetic stated. The per-pawn High-tier baseline is pinned at exactly 24 quads (`PawnQuadCountTests.cs:57-69`) against a 20,000-quad frame ceiling with 3,968 quads of headroom at 500 units. One new rectangle on a ranged pawn only takes the all-ranged worst case to `25 x 500 + 4,032 = 16,532`, leaving 3,468 quads. **No trail-equivalent may be added** — the swing trail's six stroked quads are the only thing in the budget with that shape. In-flight projectiles are counted separately against the whole-frame estimate, one line each at a bounded population, and that arithmetic is owed explicitly rather than folded into the per-pawn figure. Confirm `ConservativePawnCullTests`' brute-force sweep still contains every pawn's real bounds now that three weapon roles exist. Per the anti-density-creep rule at `SubmissionCount.cs:412-421`, the new pinned values and their arithmetic go in the commit message. | `src/Hukbo.Client/Rendering/SubmissionCount.cs`, `src/Hukbo.Client/Rendering/RenderBudgetEstimate.cs`, `src/Hukbo.Client/Rendering/ConservativePawnCull.cs`, `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs`, `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs`, `tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs` | The quad count asserted for each pawn configuration equals what the renderer submits for it; the 200-unit and 500-unit whole-frame estimates include the bounded projectile population and stay under 20,000; `ConservativePawnCullTests` passes over the full catalog cross-product with seven weapon roles. | RU-22 | `dotnet test` on `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs`, `RenderBudgetEstimateTests.cs`, and `ConservativePawnCullTests.cs` |
| RU-24 | **Calibration, before any golden is pinned.** Tune the V5 ranged rows and the `RangedStandoffV8` standoff fraction against the two quantitative acceptance bands and the termination bar, using a calibration harness in the test project because the headless runner cannot reach `AgentState`. The bands: `CombatMetrics.DefenceAttributableShare` inside **0.25 to 0.45** across seeds 1 through 20 at 200 agents (`PhilippineCombatIntegrationTests.cs:687`), and shielded roster entries still absorbing more blows before dying than shieldless ones (`:797`). The termination bar: at least 19 of 20 seeds decisive before the 5,000-tick cap with a median decisive tick at or below 5,000, each faction winning at least four of twenty seeds, and the ten-cell matrix of seeds {1, 2, 3, 5, 8} at 200 and 500 agents compared against the recorded V4 baseline of 1,279 to 4,405 ticks. The tuning levers, in order: the ranged roster share, the standoff distance as a fraction of reach, the shot intervals, and the ranged cells in the weapon-intercept matrix. **The bands are the acceptance criterion and may not be widened**; every ranged clash cell is marked `PROVISIONAL` because not one sixteenth-century sentence describes a Philippine projectile striking a Philippine shield. | `src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `tests/Hukbo.Core.Tests/RangedCalibrationHarness.cs` (new) | The harness reports a defence-attributable share inside 0.25 to 0.45 for all twenty seeds, the shielded-absorbs-more relationship holding for all twenty, at least 19 of 20 seeds decisive before 5,000 ticks with a median at or below 5,000, both factions winning at least four seeds, and a ten-cell matrix whose terminal ticks are recorded beside the V4 baseline. All values that moved are marked `PROVISIONAL` in source. | RU-12, RU-21 (shares both files) | `dotnet test` on `tests/Hukbo.Core.Tests/RangedCalibrationHarness.cs`, with the twenty-seed table and the ten-cell matrix pasted into section 9 |
| RU-25 | Client wiring, so a spectator can actually reach the feature. A `_rangedPoses` buffer beside `_gaitPoses`, resolved once per frame into a caller-owned dictionary; the draw-loop lookup beside the two existing `TryGetPose` calls, applying the ranged-pose-suppresses-swing rule; the projectile store ingested once per tick and its lines drawn at the same detail tier as the weapon line rather than gated off at Low, because at Low tier the projectile may be the only thing that says a ranged unit exists; `SoundDirector` fed the agent view list alongside the events; and **`ArenaGame.BuildScenario` naming `PrecolonialPhilippinesV5` and `RangedStandoffV8`**, because a feature a spectator cannot reach fails acceptance question 1 outright. The headless default and `Scenario.CreateDefault` stay on V4 — this task does not flip the shipped default. | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`, `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` | A tick ingest reaches the projectile store exactly once; the draw path allocates nothing new per frame; the probe pass and the draw pass still cull identically; the client's scenario reports the two ranged presets; `Scenario.CreateDefault` is unchanged. | RU-11, RU-18, RU-19, RU-21, RU-22 | `dotnet test` on `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs`; `./scripts/run.ps1 -Configuration Debug -LogLevel dbg` and the resulting `artifacts/logs/*.jsonl` read back |
| RU-26 | The preset goldens, captured from real runs after calibration has settled. V5's content hash pinned as a literal and asserted distinct from V1 through V4, in the shape of `DeterminismTests.cs:192`. V5's seed-1 state and event hashes at 20 agents and 200 ticks, in the shape of `:215`, **with the exact command that produced them recorded in the test's comment**. And the **zero-ranged inert control**: a V5 scenario whose `RosterCounts` give every ranged entry zero warriors reproduces V4's seed-1 state and event hashes exactly, in the shape of `ZeroInterceptionProfile_ReproducesTheRecordedStateHash` (`:997`). The control is the cheapest possible proof that the conditional hash gate is on a capability the scenario can switch off, and it is the test that fails loudly if a ranged fold leaks into a melee-only run. `Scenario.Validate` already permits a per-entry roster count of zero, so no validation change is needed. | `tests/Hukbo.Core.Tests/DeterminismTests.cs` | All three pins pass against literals captured from real headless runs; V1 through V4's pinned content hashes and seed-1 hashes are unmodified; `CombatMetrics_ReachesNeitherHash` still passes. | RU-24 | `dotnet test` on `tests/Hukbo.Core.Tests/DeterminismTests.cs` |
| RU-27 | The frozen trajectory digest for `RangedStandoffV8`, in the shape of `MovementPresetFreezeTests.cs:187`. V1 through V7 keep their existing digests byte-identical. | `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` | V8 has its own pinned digest captured from a real run; every existing preset digest is unmodified and passes. | RU-24 | `dotnet test` on `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` |
| RU-28 | The behavioural pins for the projectile, in one new test file so they do not collide with the existing suites. Flight time: a projectile launched on tick N resolves on tick N + flight and on no other tick. Launch-tick roll: the clash roll for an impact equals the roll the same tuple produces at the launch tick. Pool ceiling: a launch at the ceiling is refused, no cooldown is charged, and the refusal is counted. Order independence: a battle with projectiles resolves identically under every agent storage order, in the shape of `BattleSimulationTests.cs:966` and `DeterminismTests.cs:690`. Simultaneity: two projectiles arriving on the same tick that together kill a defender both have their blow recorded, exactly as two melee attackers already do. Dead-attacker delivery: a projectile launched by an agent that dies mid-flight still delivers its recorded damage. Allocation: a warm 1,000-tick window **with projectiles in flight** stays inside 16,384 bytes with the 4,096-byte growth tolerance. Save and resume: a snapshot taken with a projectile mid-flight resumes to an identical state hash. | `tests/Hukbo.Core.Tests/RangedProjectileTests.cs` (new) | All eight pins pass; the allocation pin is run on a ranged roster rather than a melee one; the save-and-resume pin covers a mid-flight projectile. | RU-24 | `dotnet test` on `tests/Hukbo.Core.Tests/RangedProjectileTests.cs` |
| RU-29 | Close the gate's blind spot. A ranged sibling of the twenty-seed termination and both-factions-win tests (`BattleSimulationTests.cs:566`, `:571`) in its own file, run on `PrecolonialPhilippinesV5` and `RangedStandoffV8`; and **a second determinism workload in the canonical gate on the ranged presets**, so the gate exercises the feature instead of running V4 while the Client runs V5. This is the same blind spot that let `EquipmentRelativeFootworkV6` and `V7` draw every seed while the gate stayed green, and the design records it as risk 9. | `tests/Hukbo.Core.Tests/RangedTerminationTests.cs` (new), `scripts/verify.ps1` | At least 19 of 20 seeds are decisive before the 5,000-tick cap on the ranged presets with a median at or below 5,000; each faction wins at least four of the twenty; `./scripts/verify.ps1` runs two determinism workloads, the existing V4 one and a ranged one, and fails if either is non-deterministic. | RU-24 | `dotnet test` on `tests/Hukbo.Core.Tests/RangedTerminationTests.cs`; the gate's own output at RU-33 |
| RU-30 | **F-B.** Make ally clearance a monotonicity constraint instead of a state constraint. `IsLaneClearOfAllies` (`BattleSimulation.cs:2453`) currently rejects a candidate endpoint on its **absolute** distance to every ally, never tests the actor's own current position, and therefore makes an already-violating configuration absorbing: standing still is always legal and the rule punishes movement out of the violation rather than the violation itself. Change the predicate to reject only when the candidate moves the actor **closer** to an ally it is already too close to — `reject if separation < required AND separation < currentSeparationToThatAlly` — which is the shape `ShouldOmitDirectCandidate` already uses twelve lines earlier at `:2416`, with the documented convention that exact equality keeps the candidate. Hoist the actor's own tick-start separation out of the candidate loop; it does not depend on the candidate. This changes end-of-tick positions, so it ships as `MonotoneAllyClearanceV9` with a new registry row and its own frozen digest. V6 and V7 keep their content hashes and digests; `PersistentContingentsV4` is not touched. Every existing `IsLaneClearOfAllies` unit test asserting the absolute rule stays true for V6 and V7 and gains a preset-scoped sibling rather than an edit. **Measured on melee-only rosters first**, so F-B's effect is not confounded with the ranged change; only then on a mixed roster. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs`, `tests/Hukbo.Core.Tests/Movement/LaneClearanceTests.cs` (new) | On a melee-only roster under V9, F-A's lane-not-clear counter **collapses** relative to the V6 measurement — if it does not, the diagnosis was wrong and that is the finding; at least 19 of 20 seeds decisive before the 5,000-tick cap with a median at or below 5,000; the ten-cell matrix recorded beside the V6 baseline for terminal tick, outcome, accepted attacks, and attack-capable agent-ticks; V6 and V7 digests and content hashes unmodified; the mixed-roster measurement recorded separately from the melee-only one. | RU-06 (its counters are how this is measured), RU-21 and RU-24 (share `MovementPresetRegistry.cs`), RU-17 (shares `BattleSimulation.cs`), RU-27 (shares `MovementPresetFreezeTests.cs`) | `dotnet test` on `tests/Hukbo.Core.Tests/Movement/LaneClearanceTests.cs`; `./scripts/benchmark.ps1` across the ten-cell matrix, with the counters and terminal ticks pasted into section 9 |
| RU-31 | **PAID. A HUMAN RUNS THIS. NO AGENT GENERATES SOUNDS.** Generate the sixty sound files with `./scripts/sfx.ps1`, twenty per weapon: `release-<weapon>` at 5 / 6 / 7 takes, `attack-<weapon>` at 9 / 8 / 6 takes across the six hit classes (with `ribcage` present for every weapon, because it is the universal fallback target and a hit-location driven slot without it can resolve `Missing`), `clash-shield-<weapon>` at 3 / 3 / 3, `miss-<weapon>` at 3 / 3 / 2, and `misfire-arquebus` at 2. This task **spends money** on the ElevenLabs text-to-sound-effects API and it is the one task in this plan that talks to a network service. It is scheduled last among the audio work for a load-bearing reason: eighteen of the sixty files cannot be triggered at all until `BattleEventKind.Release` exists, is emitted, and is carried by the mapper, and three more depend on decisions in RU-14. Three operational facts: the API refuses anything under 0.5 seconds, so a short impact is generated long and trimmed; a take peaking below ten per cent of full scale is rejected without writing anything, so re-running the same command is safe and is usually all that is needed; and the script retries a rate-limit response six times with exponential backoff. **No test walks the audio folder to check that a file there is a file the game will read, and no automated test can confirm a sound was heard** — a misnamed file is ignored silently and sixty files can pass the entire gate while being inaudible. Verification is a person listening, and its result is a smoke-checklist row. `ELEVENLABS_API_KEY` comes from the environment or the untracked `.env` and never appears in a tracked file, in output, or in a commit message. | `src/Hukbo.Client/Content/Audio/*.wav` (sixty new files) | Sixty correctly named uncompressed PCM WAV files exist in `src/Hukbo.Client/Content/Audio/`; `./scripts/sfx.ps1 -List` reports every one of the twenty-six slots as present; the game loads without a missing-cue warning; a person has heard at least one take from each of the thirteen new slots. | RU-04, RU-14, RU-15, RU-19, RU-20 (the mix headroom must be measured before the spend), RU-25 | `./scripts/sfx.ps1 -List`, then a human at an interactive desktop; the result is a `PENDING` row in `docs/development/testing.md` that only that human may flip |
| RU-32 | The manual smoke-checklist rows for this package, added to the interactive checklist and **shipped as `PENDING`**. They must cover, at minimum: a projectile visible in flight; the gap between the release cue and the impact cue being audible as the flight time; each of the three five-phase draw sequences reading as that weapon; a ranged warrior visibly stopping while its melee comrades walk in past it; the inspector reading "holding at range" rather than "blocked"; a missed shot not playing a flesh impact; the three new silhouettes distinguishable at each detail tier including Low; the arquebus reading as rare, loud, and distinctive; **and a row that explicitly asks whether an arrow passing through the friendly front rank looks wrong**, because that is the one effect in Phase 1 a spectator cannot discover and it must be looked at deliberately. Under `CLAUDE.md` section 6 rule 4, **no compilation, unit test, or window-opening probe may flip any of these rows to `PASS`, and no agent may flip one at all.** The plan owes the rows; it does not owe the results. | `docs/development/testing.md` | Rows exist, each describes what a person must look at and what they should expect to see or hear, and every one is `PENDING`. | RU-01 (shares the file), RU-25, RU-31 | A human at an interactive desktop |
| RU-33 | The canonical gate, run once after integration. Not delegated to any sub-agent, and no sub-agent's report substitutes for its real output. | — | `./scripts/verify.ps1` output pasted verbatim into section 9, together with the two determinism workloads' `stateHash`, `eventHash`, `deterministic`, and `firstMismatchTick`. | RU-01 through RU-32 | `./scripts/verify.ps1` |
| RU-34 | **Added 2026-08-07, after RU-03 exposed that no task owned these files.** Make three `Hukbo.Core.Tests` suites roster-aware instead of enum-exhaustive. `ClashResolverTests` (five red facts), `HitLocationResolverTests.WeaponOverrides_CanChangeTheResolvedBodyPartForTheSameTuple`, and `PhilippineCombatIntegrationTests` (two red facts) each sweep `Enum.GetValues<WeaponId>()` and feed every value into a ruleset built from `PrecolonialPhilippinesV1` or `V2`. **Those four presets are frozen and will never gain a ranged arm, so no amount of work on V5 can turn these green** — the sweep itself is what has to change, from "every value the enum defines" to "every weapon this ruleset's own roster actually declares". The coverage intent must survive: a sweep that silently skips a weapon a ruleset *does* declare is a weakened test and is forbidden by `CLAUDE.md` section 5. Derive the weapon set from the ruleset under test, do not hard-code a list of four, so that adding an eighth weapon later fails loudly in the right place rather than here. | `tests/Hukbo.Core.Tests/ClashResolverTests.cs`, `tests/Hukbo.Core.Tests/HitLocationResolverTests.cs`, `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | All eight of those facts pass with the three ranged `WeaponId` members defined and unregistered in V1 through V4; each sweep still covers every weapon its ruleset declares, verified by asserting the swept count equals the roster count rather than by inspection; no assertion is deleted, loosened, or marked `Skip`. | RU-03 | `dotnet test` on the three named files |
| RU-39 | **Added 2026-08-07. Found by RU-12. A test that was true only inside a window.** `WeaponProfileTests.EveryProfileOfEveryRegisteredPresetDeclaresAllRangedFieldsZero`, added by RU-07, sweeps every registered preset and asserts all three ranged fields are zero. Its own comment states why it passed: "RU-12's `PrecolonialPhilippinesV5` is what will first field a non-zero declaration and it is deliberately not registered yet, so this loop only ever sees the melee case." Registering V5 makes the loop see a correctly-declared ranged profile and the unconditional assertion fails. The invariant is real but was written one level too broad. Rescope it: a **melee** profile must declare all three as zero, and a **ranged** profile must declare all three non-zero — which is a strictly stronger statement than the original, since it now checks both halves of `CombatRuleset`'s validation rule rather than only the melee half. Do not delete the fact and do not narrow it back to V1 through V4 by name, because a hard-coded preset list would rot at the next preset exactly as this one did. | `tests/Hukbo.Core.Tests/WeaponProfileTests.cs` | The fact passes with V5 registered; a melee profile with any non-zero ranged field still fails it; a ranged profile with any zero ranged field still fails it; no other `WeaponProfileTests` fact is modified. | RU-12 | `dotnet test` on `tests/Hukbo.Core.Tests/WeaponProfileTests.cs` |
| RU-38 | **Added 2026-08-07. Found by RU-16. One argument, and without it RU-16's headline feature reads zero forever.** RU-16 added a per-faction holding count to the battle report, but the live call site `PresentationCoordinator.cs:140` invokes `BattleReportAccumulator.Ingest(events, tickCombatByFaction)` with no agent roster, so `HoldingCount` is always zero in the running game. RU-16 could not fix it because `PresentationCoordinator.cs` was not in its authorized file list; it added `agents` as an optional trailing parameter defaulting to `null` so the existing call kept compiling, which is why nothing failed. The roster is already in scope at that exact line — the next statement is `HitEffects.Ingest(events, agents)`. Pass `agents` through. **Note the failure mode for the future:** an optional trailing parameter let a feature ship structurally complete, fully unit-tested, and functionally dead, with a green suite throughout. A test that asserts the count is non-zero on a roster that contains a holding warrior is what closes that hole, not the wiring alone. | `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`, `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` | `HoldingCount` is non-zero for a faction whose roster contains an `AgentIntent.Holding` warrior, asserted through `PresentationCoordinator` rather than by calling the accumulator directly; a roster with no holding warriors still reports zero; no Client test constructs `ArenaGame`, a graphics device, a sprite batch, or a window. | RU-16 | `dotnet test` on `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` |
| RU-37 | **Added 2026-08-07. Found by RU-06, and RU-30 cannot be measured without it.** Wire F-A's four route-refusal counters through the headless runner so they appear in a real benchmark report instead of reading `0`. `HeadlessRunner.cs:520` owns the `MovementBehaviorMetricsAccumulator` and already calls `RecordConflictDenialTotal(left.MovementConflictDenials)`; the four new counters need the equivalent call from `BattleSimulation`'s four new public properties, recorded the same way and for the same reason. RU-06 could not do this because `HeadlessRunner.cs` was not in its authorized file list — the row named `RunReport.cs`, which is the record definition rather than the population site, which is the identical mistake the RU-05 row made. Nothing here reaches either hash: these are derived observability counters, so **no new preset version and no pinned artifact may move**. | `src/Hukbo.Headless/HeadlessRunner.cs` | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -MovementPreset EquipmentRelativeFootworkV6` reports four non-zero `routeRefusal*` counters summing to exactly 692,750; the shipped-default run still reports `stateHash 1B73FC5923879AA0` and `eventHash AC55684F24D39344`; the same run on `PersistentContingentsV4` reports all four as zero, because that preset never calls `TryProposeEquipmentRoute`. | RU-06 | `./scripts/benchmark.ps1` on V6 and on the shipped default, both reports pasted into section 9 |
| RU-36 | **Added 2026-08-07. Found by RU-07, and it needs a decision before RU-12 builds V5.** `CombatRuleset.AddProfile` (`CombatRuleset.cs:641-648`) folds exactly three fields into the preset content hash — `DamagePerAttack`, `AttackRangeRaw`, `AttackCooldownTicks`. RU-07's three ranged fields are **not** folded, and neither are the pre-existing combo fields. The consequence is not cosmetic: RU-24 calibrates the ranged tuning and RU-26 pins V5's content hash, so under the current fold a calibration pass changes how the game plays while the content hash stays byte-identical. The content hash is what decides whether a saved replay is the same configuration, so two genuinely different tunings would be declared identical and a replay recorded under the old values would be accepted and then diverge. **The obstacle is that the naive fix breaks a frozen invariant:** folding three more values unconditionally changes the content hash of V1 through V4, which this plan requires to stay byte-identical. The candidate resolution is to fold the three ranged values **only when the profile declares any of them non-zero**, so an all-zero melee profile hashes exactly as it does today and V1 through V4 are preserved, while V5 becomes sensitive to its own ranged tuning. Conditional folding has bitten this repository before — check how the V7 work handled it and match that precedent or state why it does not apply. The pre-existing unfolded combo fields are **not** in scope; note them and leave them. | `src/Hukbo.Core/Combat/CombatRuleset.cs`, `tests/Hukbo.Core.Tests/DeterminismTests.cs` | V1 through V4's pinned content hashes are byte-identical to their current values; two V5 rulesets differing only in a ranged tuning value produce **different** content hashes, proven by a test that constructs both; an all-zero melee profile folds exactly as before, proven against a pinned literal. | RU-07, and it must land before RU-12 pins anything | `dotnet test` on `tests/Hukbo.Core.Tests/DeterminismTests.cs` and `WeaponProfileTests.cs` |
| RU-35 | **Added 2026-08-07, same cause as RU-34.** `PawnAppearanceFactory.ToWeaponRole` (`PawnAppearanceFactory.cs:130`) is a total switch over `WeaponId` that throws `ArgumentOutOfRangeException` on `Bangkaw`, and **no task in this plan owned that file** — RU-10 owns the catalog and `PawnAppearance.cs`, RU-22 owns `PawnGeometry.cs`. Add the three arms mapping the ranged weapons to the `PawnWeaponRole` members RU-10 introduces, and repair the five red `PawnAppearanceFactoryTests` facts plus `ConservativePawnCullTests.GeometryAppearances_CoverEverythingTheFactoryCanProduce`. `Create_MapsAllFourWeaponIdsToDistinctSilhouettes` is named for a four-weapon roster and must be renamed and widened to seven rather than left asserting a stale number. The two policy facts — `WeaponLabels_NeverUseTheRejectedPanabasName` and `WeaponLabels_NeverCarryACulturalNameWithoutItsDescriptor` — enforce `CLAUDE.md` section 7 and must keep enforcing it across all seven weapons, not just the original four. | `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`, `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs` — **`ConservativePawnCullTests.cs` was reassigned to RU-22 on 2026-08-07, see the second correction in section 3** | All seven `WeaponId` values resolve to a `PawnWeaponRole` without throwing; the five `PawnAppearanceFactoryTests` facts pass; the distinct-silhouette fact covers seven weapons and still requires distinctness; both naming-policy facts pass for the three new weapons. | RU-10 (introduces the `PawnWeaponRole` members this maps onto) | `dotnet test` on `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs` and `ConservativePawnCullTests.cs` |
| RU-40 | **Added 2026-08-08. Found by the orchestrator while reviewing RU-19.** RU-19 added a public one-argument `SoundDirector.Ingest(events)` that forwards to the required two-argument overload with an empty view list. Its reasoning was sound at the time: twenty-seven pre-existing call sites in `SoundDirectorTests.cs` use the one-argument form, including the guard at `:41-74` that RU-19's own acceptance criteria required be left unmodified, so removing the overload meant editing a protected test. But a public overload that silently resolves every `Release` to no cue is an unvalidated door standing beside the validated one, and that is the same shape of defect that produced RU-38 and RU-19 themselves. Delete the one-argument overload and migrate all twenty-seven test call sites to pass a view list, empty where the test does not care. The guard at `:41-74` keeps its assertions and its semantics untouched; only its call gains an argument, which is not a weakening. | `src/Hukbo.Client/Audio/SoundDirector.cs`, `tests/Hukbo.Client.Tests/SoundDirectorTests.cs` | No one-argument `Ingest` overload exists; every call site passes a view list explicitly; `Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation` still asserts exactly what it asserts today and passes; the Client suite shows no failure outside the known-red list. | RU-19 | `dotnet test` on `tests/Hukbo.Client.Tests/SoundDirectorTests.cs` |

### PARALLEL-SAFE and SERIAL, per task

The table above is fixed at six columns, so each task's concurrency marking is
recorded here instead. **PARALLEL-SAFE** means the task's file set is disjoint
from every other task dispatched in the same wave. **SERIAL** means the task
shares at least one file with another task in the plan and may never be
dispatched beside it.

- **PARALLEL-SAFE:** RU-02, RU-03, RU-04, RU-05, RU-07, RU-08, RU-09, RU-10,
  RU-11, RU-15, RU-16, RU-18, RU-19, RU-20, RU-22, RU-23, RU-25, RU-26, RU-27,
  RU-28, RU-29, RU-31, RU-34, RU-35.
- **SERIAL:** RU-01 (first owner of `SIMULATION-GAME-STANDARDS.md` and
  `docs/development/testing.md`), RU-06, RU-13, RU-17, RU-21 and RU-30 (the
  `BattleSimulation.cs` chain), RU-12 and RU-24 (`PhilippineCombatPresetV5.cs`),
  RU-14 (`SIMULATION-GAME-STANDARDS.md`), RU-32
  (`docs/development/testing.md`), RU-33 (the gate, run by the orchestrator).

A SERIAL task is still dispatched inside a wave and still runs at the same time
as the PARALLEL-SAFE tasks in that wave. What the marking forbids is dispatching
two tasks from the same chain together, which section 5's second table
enumerates file by file.

## 5. Execution waves

Ten waves. The ceiling is eight parallel agents; the widest wave here is six. No
wave starts before every task in the wave above it has actually reported.

| Wave | Tasks | Parallel? | Note |
| --- | --- | --- | --- |
| 1 | RU-01, RU-02, RU-03, RU-04, RU-05 | PARALLEL-SAFE, five agents | Four disjoint documentation and enum tasks plus the headless option. RU-03 satisfies ordering constraint 1 — the three `WeaponId` members and the V5 preset identity land before anything references a ranged weapon. RU-04 satisfies ordering constraint 2 — the two `BattleEventKind` members land before any audio work. RU-01 is first in the `SIMULATION-GAME-STANDARDS.md` and `docs/development/testing.md` chains. |
| 2 | RU-06, RU-07, RU-08, RU-09, RU-10, RU-11, **RU-34** | PARALLEL-SAFE, seven agents | RU-06 is F-A and is the first owner of `BattleSimulation.cs`; ordering constraint 5 puts it before F-B, and putting it at the head of the file's chain costs nothing because it is the cheapest task in that file and moves no hash. **RU-34 was added to this wave on 2026-08-07.** Its three test files are disjoint from every other task in the wave, it depends only on RU-03, and it closes eight of the twenty-nine red tests that nothing else in the plan could close. RU-35 waits on RU-10 and lands in wave 3. |
| 3 | RU-12, RU-13, RU-14, RU-15, RU-16, **RU-35** | PARALLEL-SAFE, six agents | RU-13 is the second owner of `BattleSimulation.cs`. RU-14 closes the `SoundCatalogTests` part of the known-red window and is the second owner of `SIMULATION-GAME-STANDARDS.md`. **RU-35 was added here on 2026-08-07.** It owns `PawnAppearanceFactory.cs`, which no original task owned, and it cannot start earlier because it maps onto the `PawnWeaponRole` members RU-10 introduces in wave 2. |
| 4 | RU-17, RU-18, RU-19, RU-20 | PARALLEL-SAFE, four agents | RU-17 is the third owner of `BattleSimulation.cs` and is the largest single task in the plan. RU-18 satisfies ordering constraint 6 — the pose work starts only after RU-13 has put the projection fields on `AgentView`. RU-20 satisfies ordering constraint 4 — the mix harness is updated and re-run here, four waves before anyone pays for a sound file. |
| 5 | RU-21, RU-22 | PARALLEL-SAFE, two agents | RU-21 is the fourth owner of `BattleSimulation.cs`. This is the narrowest wave and it is the plan's critical path. |
| 6 | RU-23, RU-24, RU-25 | PARALLEL-SAFE, three agents | RU-24 is calibration and it deliberately runs **before** any golden is pinned, because calibration moves the values a golden would capture. |
| 7 | RU-26, RU-27, RU-28, RU-29 | PARALLEL-SAFE, four agents | Four disjoint test files. Every golden in this wave is captured from a real run after RU-24 settled the values. |
| 8 | RU-30 | SERIAL, one agent | F-B. It shares `BattleSimulation.cs` with RU-17 and RU-21, `MovementPresetRegistry.cs` with RU-21 and RU-24, and `MovementPresetFreezeTests.cs` with RU-27, so it cannot run beside any of them. Ordering constraint 5 also requires F-A's counters to exist first, and they are what F-B is measured by. |
| 9 | RU-31, RU-32 | RU-31 is a human, RU-32 is an agent | RU-31 is the paid ElevenLabs generation and only a person runs it. RU-32 adds the smoke rows and is the second owner of `docs/development/testing.md`. Their files are disjoint, so RU-32 does not wait on RU-31 finishing, only on it starting. |
| 10 | RU-33 | SERIAL, the orchestrator | The canonical gate. Not delegated. |

### Files touched by more than one task, and why they are serialized

Six files are genuinely touched twice or more. In every case the tasks are made
sequential rather than parallel, and this is the list an orchestrator checks
before dispatching a wave.

| File | Tasks, in order | Why it could not be made disjoint |
| --- | --- | --- |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | RU-06 → RU-13 → RU-17 → RU-21 → RU-30 | This is the tick pipeline. F-A's counters sit in `TryProposeEquipmentRoute`, the ranged phase projection in `UpdateViews`, the projectile pool in `GatherAndCommitAttacks`, the hold arm in `GatherMovementProposals`, and F-B in `IsLaneClearOfAllies` — five different methods in one 4,000-line file. Splitting the file is a refactor this package has no mandate for, and it would move a great deal of code in a change that must not move a hash for the wrong reason. **This chain is the plan's critical path and it is five waves long.** |
| `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | RU-21 → RU-24 → RU-30 | Two new preset rows and a calibration pass over one of them. A registry is a single switch; two agents in it conflict by construction. |
| `SIMULATION-GAME-STANDARDS.md` | RU-01 → RU-14 | RU-01 corrects the stale allocation ceiling and the deferral list; RU-14 adds the ranged row to the spectator-channel table. Different sections, same file. RU-01 goes first because an implementer who reads the stale allocation figure will design the wrong projectile representation. |
| `docs/development/testing.md` | RU-01 → RU-32 | RU-01 corrects the stale allocation figure at `:1997`; RU-32 appends the smoke rows at the end. Different sections, same file. |
| `src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs` | RU-12 → RU-24 | RU-12 creates the preset with provisional values; RU-24 calibrates them. Calibration cannot precede the file existing, and the goldens cannot precede calibration. |
| `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` | RU-27 → RU-30 | One digest for `RangedStandoffV8` and one for `MonotoneAllyClearanceV9`, in one test file. |
| `src/Hukbo.Headless/RunReport.cs` | RU-05 → RU-06 | RU-05 adds the preset echo, RU-06 adds F-A's four counters. Both are field additions to one record. |

## 6. Verification criteria for the package

The package is done when all of the following are true and their real output is
recorded in section 9. An argument that a criterion *should* hold is not
evidence; the command's output is.

**The gate.**

- `./scripts/verify.ps1` passes every stage, and its verbatim output is in
  section 9.
- The gate runs **two** determinism workloads, the existing 200-agent /
  10,000-tick / seed-1 V4 one and a ranged sibling, and both report
  `deterministic: true` with a null `firstMismatchTick`.
- No test was weakened, no analyzer suppressed, no warning downgraded, and no
  pinned golden re-pinned to make a red test green.

**Determinism.**

- V1 through V4's pinned combat content hashes and seed-1 hashes are
  byte-identical to their recorded values.
- V1 through V7's movement content hashes and frozen trajectory digests are
  byte-identical to their recorded values.
- `PersistentContingentsV4` and `PrecolonialPhilippinesV4` on seed 1 reproduce
  the recorded baseline `stateHash` and `eventHash` exactly.
- The zero-ranged inert control passes: a V5 scenario with zero warriors in
  every ranged roster entry reproduces V4's seed-1 state and event hashes.
- V5 and `RangedStandoffV8` have their own pinned goldens, each captured from a
  real run whose exact command is recorded in the test's comment.
- Repeating the ranged workload on the same seed and the same build produces an
  identical state hash, event hash, winner, and ordered event stream.

**Budgets.**

- A warm 1,000-tick window with projectiles in flight allocates no more than
  16,384 bytes, with the 4,096-byte growth tolerance, at 12 agents per faction.
- The whole-frame quad estimate at 500 units, including the bounded in-flight
  projectile population, stays under the 20,000-quad ceiling.
- `ConservativePawnCullTests` passes over the full catalog cross-product with
  seven weapon roles, and no ranged pose extends a weapon line beyond the
  existing envelope.
- The 500-agent mix rendering's peak level and per-slot peak are recorded, and
  if the sixteen-cue per-slot cap binds, the raised limit is recorded with its
  measurement rather than guessed.

**Acceptance bands.**

- `CombatMetrics.DefenceAttributableShare` is inside 0.25 to 0.45 across seeds 1
  through 20 at 200 agents on the ranged presets. **The band is not widened.**
- Shielded roster entries still absorb more blows before dying than shieldless
  ones across seeds 1 through 20.
- At least 19 of 20 seeds are decisive before the 5,000-tick cap on the ranged
  presets, with a median decisive tick at or below 5,000, and each faction wins
  at least four of the twenty.
- F-A's four counters sum to exactly **692,750** on the V6 workload — the
  measured value, replacing the stale 1,140,221 this plan originally asked for.
  See "The refuse-tick baseline was stale" in section 9.
- F-B's lane-not-clear counter collapses relative to that V6 measurement. If it
  does not, the diagnosis was wrong, and that is a finding to record rather than
  a number to tune toward.

**Honesty.**

- Every smoke-checklist row this package adds is `PENDING` unless a human at an
  interactive desktop watched the screen and flipped it.
- Every range, shot interval, standoff fraction, roster share, ranged clash
  cell, and pose constant is marked `PROVISIONAL` in source and is not cited
  anywhere as a historical measurement.
- RU-02 either names the source it verified or reports BLOCKED. A date guessed
  into a historical-accuracy document fails this criterion outright.

## 7. Rollback: what to do if the new presets' goldens cannot be made to pass

There are two different failures hiding under "the goldens will not pass", and
the response to each is different. Diagnose which one you have before doing
anything.

### Failure A — an old preset's golden moved

`PrecolonialPhilippinesV1` through `V4`, or `PersistentContingentsV1` through
`EquipmentRelativeFootworkV7`, no longer reproduce their pinned content hashes,
trajectory digests, or seed-1 hashes.

**This is a bug in this package, always, without exception.** Every one of those
presets is supposed to be byte-identical after this work, and the whole point of
restating values rather than referencing them, of appending enum members rather
than reordering them, and of gating the new hash tail on a capability rather than
on a preset ID is that an older preset's fold cannot move.

**Do not re-pin. Revert to the last commit where the old golden passed and find
the change that moved it.** The likely causes, in the order worth checking:

1. The conditional state-hash tail is gated on something a melee-only ruleset can
   still satisfy, so a melee run folds the projectile section. The zero-ranged
   inert control (RU-26) is the test that catches this, and if it is red this is
   your answer.
2. An enum value was inserted rather than appended, or a doc-comment edit
   reordered members.
3. A "shared" value was referenced from V5 instead of restated, so tuning V5
   reached back into V4.
4. The ranged phase projection in `UpdateViews` (RU-13) wrote state instead of
   deriving it, or made a query the tick would not otherwise make.

### Failure B — a new preset's golden cannot be made stable, or its bands cannot be met

V5 or `RangedStandoffV8` produces a hash that changes between runs, or
calibration (RU-24) cannot find values that hold the defence-attributable share
inside 0.25 to 0.45 while keeping 19 of 20 seeds decisive.

**Two things must not happen, and they are the tempting ones.** Do not re-pin a
golden to whatever the run produced in order to go green — a golden captured to
match a broken run records the breakage. And do not widen an acceptance band, and
in particular do not adopt F-F, the survivor-count tiebreak, to make termination
numbers pass: it is excluded by the design on three grounds, it would decide
battles on noise, and it is the fix that hides the bug applied to the feature most
likely to produce it.

The escalation ladder, in order, stopping at the first rung that works:

1. **Tune, within RU-24's remit.** The levers are the ranged roster share, the
   standoff distance as a fraction of reach, the shot intervals, and the ranged
   cells in the weapon-intercept matrix. All four are labelled gameplay values,
   so moving them costs nothing historically. Record what moved and why.
2. **Shrink the ranged roster share hard.** The evidence supports a ranged
   minority, so a small share is not historically embarrassing. Note that this
   has its own failure mode, recorded as "what could make this design wrong"
   item 3: a feature a spectator rarely sees fails acceptance question 1 for a
   different reason.
3. **Ship V5 without the hold rule.** `PrecolonialPhilippinesV5` on
   `PersistentContingentsV4` is a legal pairing. Ranged weapons would then close
   to body contact and shoot from there, which is a much weaker feature — a
   bowman indistinguishable from a spearman — but it is a shippable one, and it
   isolates whether the termination failure came from the hold rule or from the
   longer reach. If this rung is taken, RU-32's smoke rows about a warrior
   visibly stopping are removed rather than left as rows nothing can satisfy.
4. **Do not ship the ranged presets as the Client default.** Leave
   `ArenaGame.BuildScenario` on V4, keep V5 and V8 registered and reachable by
   explicit selection, and record in section 9 that the feature exists but is not
   the default battle. This is honest, it is reversible, and it keeps the work
   available for the next session. It fails acceptance question 1, so it is a
   held position rather than a finished one.
5. **Revert the movement preset only.** F-B (RU-30) and the hold rule (RU-21)
   are independent of each other and of sections 4 through 9 of the design;
   nothing in the ranged simulation depends on the standoff being fixed. Either
   can be dropped without dropping the other or the ranged work.

At every rung, the state of the world goes in section 9 with the numbers that
produced the decision. A rollback recorded with its measurement is a result; a
rollback recorded as "it did not work" is a lost session.

## 8. Deferred to Phase 2 — not tasks

Nothing in this section has a task ID, a wave, or a file list, and no agent
working this plan may build any of it. Each entry is specified in section 11 of
the design document, which records what it is, why it is deferred, and what would
have to be true before it is taken up.

| Deferred | Why it is not a task here | What would unblock it |
| --- | --- | --- |
| **Line of sight** | User decision: Phase 1 ships hitscan with flight time and no blocking of any kind. Nothing anywhere in the codebase asks what lies between two points, and a segment query needs a new traversal, a new deterministic order, and its own naive reference oracle alongside `NaiveCollisionPairs.cs`. | Phase 1 shipping and being measured. When it is built it goes in `SelectTargetsAndIntents`'s existing pass as an O(n) point-to-segment scan, not as an extension of the uniform grid, with a "first blocker along the segment" total order tie-breaking on `EntityId`. |
| **Friendly fire** | It depends entirely on line of sight — without a blocking rule there is nothing for a projectile to strike on the way — so it cannot ship first. The target scan hard-excludes same-faction candidates at `BattleSimulation.cs:1015`. | Line of sight, plus its own design document. It moves the defence-attributable band, the both-factions-win test, and every intuition about roster balance. |
| **Ammunition** | `CLAUDE.md` section 9 forbids it before an authorizing gate, and the user's 2026-08-07 decision lifted the projectile clause and not the ammunition one. The sources also give nothing to build on: no quantity, no quiver capacity, no resupply, and no statement that a force ran out. | A separate authorization. This is the deferral with the highest historical cost in the package — the best-attested detail about the thrown spear, picking up the same shaft four to six times at Mactan, is an ammunition behaviour. |
| **Regional rosters and scenario place tags** | `Scenario` has no place, no date, and no regional constraint, and `RosterCounts` applies the same proportions to both factions. | Its own design. Until then the *bangkaw* label is Visayan-anchored and used archipelago-wide, which is a known and labelled imprecision. |
| **The Sumpit — Blowgun** | Dropped by user decision on the evidence: the word appears in none of the Blair and Robertson volumes consulted, and every sixteenth-century attestation of the weapon places it at Palawan or Cagayan Sulu rather than in the Visayan and Manila-area engagements the game depicts. | A dated documentary attestation of the word, and the regional scenario tags above, so it appears where it is attested and nowhere else. |

Also deferred, and equally not tasks: **poison** on projectiles; **the melee
sidearm**, which is the design's own first choice for what to build next and is
the single best-attested behavioural fact about missile-armed men in this record;
**the melee `Evaded` cue**, which RU-14 deliberately does not fix; **a morale or
terror model**; **the Bronze Verso**; **thrown stones, mud, and fire-hardened
stakes**; **an arquebus misfire mechanic**, whose two generated sound files have
no Phase 1 trigger and are a recorded, deliberate spend; **a projectile
identifier linking a release to its impact** in the event stream, for which bits
40 through 63 of `BattleEvent._combatContext` are free; and **a sprite-frame
animation pipeline**, already backlogged at `docs/plans/TODO.md:41-55`.

## 9. Results

Nothing here yet. This section is filled in as tasks report, and it is where the
evidence lives rather than where a summary lives. It owes, at minimum:

- The verbatim `./scripts/verify.ps1` output from RU-33.
- Both determinism workloads' `stateHash`, `eventHash`, `deterministic`, and
  `firstMismatchTick`.
- RU-02's verification finding: the source actually read, or the BLOCKED report.
- RU-06's four rejection-reason counters and their sum on the V6 workload.
- RU-20's 500-agent mix numbers: peak dBFS, per-slot peak, total cues,
  suppressions.
- RU-24's twenty-seed calibration table and ten-cell matrix beside the V4
  baseline of 1,279 to 4,405 ticks.
- RU-30's lane-not-clear counter before and after, on melee-only rosters and
  then on a mixed roster.
- The task status table.

### The refuse-tick baseline was stale, and F-A's real number is 692,750

RU-06 reported that this plan's hard acceptance target for F-A — four counters
summing to 1,140,221 — did not match reality, and declined to adjust its exit
sites to force a match. It was right, and the orchestrator confirmed the whole
picture with its own benchmark runs rather than accepting the report:

| Workload, 200 agents / seed 1 / 10,000 ticks | `refuseAgentTicks` |
| --- | --- |
| `EquipmentRelativeFootworkV6`, today | **692,750** |
| `EquipmentRelativeFootworkV7`, today | 1,092,119 |
| The figure this plan asked for | 1,140,221 |

So 1,140,221 reproduces on **no** current preset. Two separate faults produced
it. First, provenance: the number traces to
`docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md:523`, an
**archived** document, and `CLAUDE.md` section 6 states that archived files are
deprecated by definition and may never be cited as justification for a change.
Second, drift: it was recorded on 2026-07-31, and the tree has moved a long way
since — on V6 today `regroupAgentTicks` is 844,387 against the archived 338,634
and `conflictDenials` is 322,705 against 130,844. Even the preset was wrong, since
the archived cell is a V7 baseline and this plan asked for it on V6.

**The diagnosis F-B rests on survives the correction, which is the part that
matters.** `docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md:751` derives a
route-search failure rate of at least 95.61% from the stale number. Recomputed on
today's V6 measurements, 692,750 / (692,750 + 37,414 + 198 + 0) = **94.85%**.
Route-search refusal still overwhelmingly dominates, so F-B remains the right
intervention and RU-30's premise holds. Only the arithmetic needed correcting, and
RU-30 must compare against 692,750 rather than against anything in that archived
file.

### RU-06's counters do not reach a live benchmark report yet

Every one of the four reads `0` in an actual
`./scripts/benchmark.ps1 -MovementPreset EquipmentRelativeFootworkV6` report, even
though RU-06's own unit test correctly reconstructs the sum by driving
`BattleSimulation` directly. The cause is a file-ownership gap of exactly the same
shape as the one RU-05 hit: this plan's RU-06 row names
`src/Hukbo.Headless/RunReport.cs` as where the counters are surfaced, but the
population site is `HeadlessRunner.cs:520`, which owns the
`MovementBehaviorMetricsAccumulator` and calls `RecordConflictDenialTotal` on it.
`HeadlessRunner.cs` was not in RU-06's authorized file list, so RU-06 documented
the gap and correctly stopped. RU-37 closes it, and **it must land before RU-30**,
whose entire acceptance criterion is a benchmark-measured collapse in the
lane-not-clear counter.

### F-A's result: the standoff is a single-cause failure, and F-B targets that cause

With RU-37 wiring RU-06's counters through the headless runner, F-A now reports
real numbers. Measured on `EquipmentRelativeFootworkV6`, 200 agents, seed 1,
10,000 ticks, and reproduced independently by the orchestrator:

| Counter | Value | Share of refusals |
| --- | --- | --- |
| `routeRefusalLaneNotClear` | **692,700** | 99.993% |
| `routeRefusalDirectCandidateOmitted` | 50 | 0.007% |
| `routeRefusalNoCandidatesBuilt` | 0 | none |
| `routeRefusalStepEndpointRejected` | 0 | none |
| Sum | **692,750** | equals `refuseAgentTicks` exactly |

This is a stronger result than the plan expected, and it is worth stating plainly
because it changes how F-B should be judged. The plan split `refuseAgentTicks` four
ways on the assumption that several rejection reasons shared the blame. They do
not. Two of the four never fire at all across ten thousand ticks, a third fires
fifty times, and `IsLaneClearOfAllies` accounts for essentially every refusal in
the run. The standoff is a single-cause failure.

That cause is precisely the predicate RU-30 rewrites, so F-A has confirmed F-B's
premise rather than merely measuring around it. It also sharpens RU-30's acceptance
criterion: the "collapse" that row asks for is a collapse in a counter whose
baseline is now known exactly, **692,700**, and a V9 run that does not move that
number substantially has falsified the diagnosis rather than under-delivered on a
tuning target.

One caution against over-reading it. These counters are agent-ticks in a refused
state, not distinct decisions, so a single warrior stuck for a thousand ticks
contributes a thousand. The number establishes *which* predicate refuses and how
overwhelmingly, not how many separate warriors were affected.

### A Release event cannot name its own weapon, and RU-19 has to resolve it

RU-14 added the mapper branches its row asked for and then reported that they
cannot fire yet. The reason is structural rather than an omission, and it is a
tension between two of this plan's own rows.

RU-04 made `Release` and `Miss` non-attack kinds, constructed through
`BattleEvent.NonAttack`, which was correct and was pinned by tests. But
`NonAttack` takes no weapon parameter at all — verified by reading its signature
at `BattleEvent.cs:331-338`, which accepts only sequence, tick, kind, source,
target, value, and faction. RU-14's row then asks for "a branch that maps a
`Release` event to the launching weapon's release slot". A `Release` event cannot
say which weapon launched it, so `Map(BattleEvent)` returns null for both kinds
and will keep doing so no matter how the mapper is written.

The branches are therefore structurally present and functionally inert until two
later tasks arrive: RU-17 emits the events, and RU-19 must resolve the launching
weapon from `AgentView.Loadout` at the call site rather than expecting it on the
event. RU-14 exposed `MapRelease(WeaponId?)` as `internal` for exactly that
caller, so **the hook already exists and RU-19 should not reinvent it**.

Two things follow for whoever picks up RU-19. First, the sound for a shot is
keyed on state the spectator's view already carries, not on the event, so the
event stream stays free of combat context and RU-04's pinned tests stay true.
Second, nobody should "fix" this by adding a weapon to `NonAttack`: that would
relax the guarantee those tests exist to hold, and `SIMULATION-GAME-STANDARDS.md`
treats a non-attack event carrying combat context as a contract violation.

### RU-12's two judgment calls, both recorded rather than buried

**Rank for the three ranged rows.** Neither the RU-12 row nor its brief said what
`RankId` a Bangkaw, Busog, or Arquebus warrior should carry, and the design
document is explicit at section 2 that no source ties any of the three to a social
rank, so inventing a hierarchy "would be invented". RU-12 assigned all three
`RankId.Timawa` uniformly — the `CombatLoadout` struct default — precisely so that
no differentiated hierarchy is asserted where the record supports none. That is
the right call under `CLAUDE.md` section 7, and it is recorded here because a
uniform assignment can otherwise look like an oversight to a later reader. If
calibration in RU-24 wants ranged warriors distributed across ranks, that is a
tuning decision needing its own justification, not a silent edit.

**Reach and standoff.** Ranged reach is expressed as a multiple of the Kampilan's
16 world units, as the row required: 48 for the Bangkaw, 80 for the Busog, 112 for
the Arquebus. Standoff sits at roughly three quarters of each weapon's own reach —
36, 60, 84 — which is strictly inside it, as `CombatRuleset` now enforces at
construction. Cooldowns preserve the required ordering with the arquebus far out on
its own: 25, 45, 240 ticks. **Every one of those numbers is provisional and RU-24
owns calibrating them.** None is a historical measurement and none may be cited as
one.

One thing RU-12 raised that turned out fine: it could not check whether registering
V5 disturbed the goldens in `DeterminismTests.cs`, because that file was outside its
scope. The merged run answers it — `DeterminismTests` passes in full at `2f785f1`,
so V5's registration moved no pinned value. That is expected rather than lucky,
since V1 through V4 are untouched and V5 has no golden of its own until RU-26.

### Integration baselines, measured after each wave

Every count below is from a real `dotnet test` run in `Release` on the
integration branch itself, after merging that wave's branches. Configuration
matters: `Debug` adds two allocation-budget failures
(`MovementContextObservationTests.RepeatedQuietV6TicksHaveBoundedAllocations` and
`MovementPipelineIntegrationTests.RepeatedVSixCollisionTicksHaveBoundedAllocations`)
that pass under `Release` and have nothing to do with this package, so a red count
quoted without its configuration is not evidence.

| Point | Commit | Core | Client |
| --- | --- | --- | --- |
| Base, before the package | `f02d012` | 0 red / 2614 | 0 red / 3121 |
| Wave 1 merged, `AgentIntent` pin extended | `59c4556` | 18 red / 2618 | 11 red / 3121 |
| Wave 2 merged, RU-06 still outstanding | `04de103` | 10 red / 2627 | 34 red / 3214 |
| Wave 2 complete, RU-06 merged | `d69ab00` | 10 red / 2631 | 34 red / 3214 |
| Wave 3 merged, RU-12 still outstanding | `f0f25f4` | 10 red / 2644 | 21 red / 3248 |
| Wave 3 complete, RU-12 merged | `2f785f1` | **2 red** / 2644 | 21 red / 3248 |

At `2f785f1` only two Core failures remain: `RU-39`'s ranged-fields-zero fact, and
the leader fact that RU-21 and RU-30 close by registering V8 and V9. `DeterminismTests`
passes in full, 22 of 22, so registering V5 disturbed no pinned golden.

At `f0f25f4` the remaining Client failures are exactly `PawnGeometryTests` (11) and
`ConservativePawnCullTests` (10), both RU-22's in wave 5, and the remaining Core
failures are `CombatConfigurationTests` (7) and `WeaponProfileTests` (2), both
RU-12's, plus the leader fact that RU-21 and RU-30 close by registering V8 and V9.
The seed-1 shipped-default `stateHash 1B73FC5923879AA0` and
`eventHash AC55684F24D39344` are unchanged at every point in this table, which is
the standing evidence that nothing in the package has reached a hash.

Wave 2 introduced no cross-branch interaction failure: the merged counts equal the
counts each branch reported alone. Wave 1 did — the `AgentIntent` pin — which is
why this table exists and why the suites are now run on the integration branch
after every wave rather than only at RU-33.

### RU-02's verification finding: CONFIRMED, with one correction to this plan

The attribution was confirmed against Blair and Robertson, *The Philippine
Islands, 1493-1803*, Volume III, read as the Project Gutenberg plain-text
edition 13616 at `https://www.gutenberg.org/cache/epub/13616/pg13616.txt`
(484,486 bytes). The finding was then independently re-checked by the
orchestrator against both the live source and the same local copy, rather than
being accepted on the task agent's report alone.

The date holds in three independent places in the volume. Its contents page
lists "Relation of the Western Islands called Filipinas. Diego de Artieda"
under the year heading `Documents of 1573`. Its preface reads "A Spanish
captain, Diego de Artieda, writes (1573) a 'Relation of the Western Islands.'"
And the in-body document group headed `Documents of 1573` contains the relation
as its third document.

The passage holds by position. The paragraph carrying the lance, cutlass or
dagger, bow, reed-arrow, and blow-gun descriptions sits at text lines 4547 to
4566, which falls inside Artieda's relation (it opens at line 4290 with
"Captain Artieda, who went to those islands for the king, wrote this
relation") and inside the 1573 document group (lines 3959 to 5027). A negative
control confirms the alternative is impossible: the volume's entire
`Documents of 1569` block, lines 484 to 1310, contains no occurrence of lance,
cutlass, dagger, blowgun, arrow, or bow.

**This plan's RU-02 row contained an error, and the task agent was right to
refuse part of it.** The row asked that the volume-III date span at line 210 of
`docs/research/HISTORICAL_1500s_WEAPONS.md` be "corrected to match", implying
that `1569-1576` was wrong. It is not wrong: that span is the volume's own
title page, and its contents run from `Documents of 1569` through
`Documents of 1575-76`. Changing those digits would have introduced a new
error into a historical-accuracy document in the course of fixing an old one.
The span was therefore kept, and the source entry was rewritten instead so that
it no longer implies the weapon descriptions come from anywhere in that span.
The entry now records which descriptions the Artieda paragraph carries and
states explicitly that none of the volume's 1569 documents describes weapons.

One related citation was left alone deliberately. The javelin row at line 42
cites a Luzon account of palm-wood lances, which corresponds to a
`Documents of 1571-72` passage in the same volume rather than to Artieda. It is
accurate as written, so it was not changed, but it is a second distinct
citation into volume III that the source entry does not yet enumerate.

### Wave 4's result, measured on the integration branch after merging

Wave 4 dispatched six tasks — RU-17, RU-18, RU-19 and RU-20 from the plan's own
wave, plus RU-38 and RU-39, which are small, independent, and were pulled forward
because RU-39 was one of only two remaining Core failures and leaving it red made
the baseline harder to read for every wave downstream.

All six merged into `ranged-units` without a conflict. Measured on the merge
commit itself, in `Release`:

```
Core:   Failed: 1, Passed: 2647, Total: 2648
Client: Failed: 21, Passed: 3262, Total: 3283
format: [PASS] Formatting verification completed.
```

Core is down from two red to one. The survivor is
`BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`,
which closes when RU-21 and RU-30 register the V8 and V9 movement presets. The
twenty-one Client failures are the same `PawnGeometryTests` (eleven) and
`ConservativePawnCullTests` (ten) that RU-22 owns in wave 5, and no failure
appeared outside that list.

**RU-17 moved a hash, and the evidence that it moved the right one is the point.**
The shipped-default V4 workload is byte-identical to the recorded baseline, while
the same workload on V5 moved on both hashes:

| Preset | `stateHash` | `eventHash` |
| --- | --- | --- |
| V4, before and after RU-17 | `1B73FC5923879AA0` | `AC55684F24D39344` |
| V5, before RU-17 | `1B2524B9DFEB7FDB` | `673EF3076D2B2EC9` |
| V5, after RU-17 | `CA230133F128B1A9` | `6953A1C982A3014C` |

That pair of results is what proves the capability gate in `StateHasher.Compute`
works as designed: a ruleset with no ranged entry folds nothing at all, so the
frozen presets cannot drift, while a ruleset that fields a ranged weapon folds the
pool and necessarily hashes differently. It is also the end-to-end proof that the
ranged path is genuinely live rather than structurally present and functionally
dead — the failure mode this package has already hit twice. If V5's hashes had not
moved, nothing would be launching.

`StateHasher.Compute` has exactly one production caller,
`BattleSimulation.cs:815`, so its optional `hasRangedWeapon` parameter has no
bypassable call site today.

**RU-13's projection was checked end to end and holds.** Before wave 4 was
dispatched, `RangedPhaseProjection.Derive` was confirmed to be called
unconditionally for every agent at `BattleSimulation.cs:4483`, with no gate and no
optional parameter, and a live V5 battle was driven through the headless runner for
the first time in this package. The projection therefore runs for every agent every
tick. What remains unproven is whether the phases *read* correctly on screen, which
is a wave 6 question for RU-25, not a correctness question about the derivation.

**RU-39 was rejected once and re-done, and the reason is worth recording.** Its
first rescoping derived "is this profile ranged" from the same three fields the
assertion then checked. Because `WeaponProfile.ValidateRangedFields`
(`WeaponProfile.cs:140`) already forces every profile that constructs into
all-zero-or-all-non-zero using the identical predicate, the fact could not fail: a
melee weapon wrongly declaring all three ranged fields non-zero would have been
classified ranged and passed. The suite was green and the fact was worthless. Its
negative proof looked convincing because it exercised a copy of the branch logic
rather than the fact itself.

The accepted version takes its signal from the weapon's identity instead, through
`RangedPhaseProjection.Derive`, and asserts that the two independent declarations
agree. That version fails in both directions, proven against the committed helper
rather than a copy. The general lesson is that a test which re-derives its own
premise from the data under test is not a weaker test, it is not a test at all, and
a green suite will never say so.

**RU-20 did not produce the number it exists to produce.** The harness now matches
the client's twenty-six-slot mapping, and along the way it had to fix two
pre-existing harness defects to claim parity honestly: every `Attack` ignored
`AttackResolution`, so `ShieldBlocked` never routed to a clash slot, and bare-file
resolution never worked, so `victory-*.wav`, `draw.wav` and `ui-click.wav` had
never resolved at all.

But all three ranged release slots measured zero cues and −∞ dBFS. On the base it
was measured against, nothing emitted a `Release` event, because RU-17 had not
landed on that branch, and no ranged sound files exist because RU-31 has not run.
The release-cue concentration that this task exists to quantify is therefore still
unmeasured. **RU-20 must be re-run now that RU-17 has landed, and RU-31 is not
cleared to spend money until that re-run produces a real number.** The plan's
dependency column for RU-20 names only RU-14; that is wrong, and RU-17 belongs
there.

The per-slot cap of sixteen does not bind: zero suppressions out of 6,302 cues
under the shipped policy, with peak concurrent voices of fifty-four against a total
cap of sixty-four. So the raised `DefaultMaximumPerSound` that the RU-20 row
anticipated is not indicated by this data, and `SoundCueBudgetTests.cs:59-79` should
not move.

**A finding outside this package's scope, surfaced by RU-20 and left unfixed.** On
a melee-only 500-agent battle under the shipped policy — sixteen per slot, sixty-four
total, `CueVolume` 0.65 — the mix now measures **+0.9 dBFS with 8 clipped samples**,
against the −0.2 dBFS and zero clipped samples recorded in section 7.2a of
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`. Peak concurrent voices rose from
forty-one to fifty-four. The cause is `Hukbo.Core` combat drift since that
2026-07-27 measurement, not anything this package changed.

The shipped mix is therefore already over full scale before a single ranged cue
exists, and this package intends to add thirteen more slots on top of it. That is a
`CueVolume` and gain question that belongs to whoever owns the audio mix, not to
the ranged-units package, and it is recorded here and in section 7.2b rather than
silently absorbed. It should be settled before RU-31 is paid for.

**RU-19 shipped a seam, accepted deliberately, and RU-40 closes it.** The details
are in the RU-40 row. The short version is that a public one-argument `Ingest`
survives beside the required two-argument one, because removing it meant editing a
test that RU-19's acceptance criteria required be left unmodified. The single
production call site is hard-wired to the two-argument form, so the feature is live
today; the risk is a future caller taking the silent door.

### RU-20's re-run after RU-17: the shipped cap does not bind, and the number this task exists to produce

Re-run on branch `ru-20-rerun` (branched from `ranged-units` at `3a1df74`, which
carries RU-17), `dotnet run -c Release --no-build -- <audio-dir> mix-output 500 1 1
PrecolonialPhilippinesV5`, seed 1, 500 agents, V5 (ranged roster fielded):

```
battle ran 2526 ticks, outcome Faction1Victory, 7929 events mapped, 4188 cues playable
```

The gap between mapped (7929) and playable (4188) is exactly the ranged demand
RU-31 has not produced audio for yet: 1,133 `release-bangkaw`, 729
`release-busog`, and 229 `release-arquebus`, plus their matching `attack-*` and
`miss-*` slots, every one mapped but zero playable.

Under the shipped policy (`CueVolume` 0.65, 16-per-slot cap, 64-total cap, voice-count
compensation):

| policy | played | suppressed | peak voices | peak dBFS | clipped samples | clipped % |
| --- | --- | --- | --- | --- | --- | --- |
| shipped-gain-0.65 | 4188 | 0 | 34 | −0.2 | 0 | 0.000% |

That is the number this task's row asks for: **−0.2 dBFS, zero clipped samples,
zero suppressions, at 500 agents with V5 fielded and RU-17 live.** All three
release slots still measure −∞ dBFS because `playable` is 0 for each — no real
ranged `.wav` has ever entered a render, so the release-cue-concentration loudness
this task was written to warn about is not yet measurable and stays unmeasurable
until RU-31 ships files.

The harness's demand-budget check applies the shipped cap (16/slot, 64 total) to
the full mapped-event stream rather than only the playable subset — the only way
to ask whether the cap would suppress a slot that has no audio at all:

```
shipped-policy cap (16/slot, 64 total) applied to raw demand (not just playable cues): 7929 demanded, 7929 accepted, 0 suppressed
    release slot: 13 release-bangkaw       demanded   1133  suppressed-if-capped      0
    release slot: 14 release-busog         demanded    729  suppressed-if-capped      0
    release slot: 15 release-arquebus      demanded    229  suppressed-if-capped      0
```

Zero suppressions on the full raw demand, including every ranged event the cap has
never had a chance to reject because no file exists. **The 16-per-slot cap does not
bind even under the worst-case assumption that every mapped ranged event would have
played, so `SoundCueBudgetTests.cs:59-79`'s cap should not move** — the wave 4
partial finding holds under this corrected, post-RU-17 measurement.

What this run cannot answer is whether adding real ranged audio pushes the mix over
full scale. The −0.2 dBFS figure is measured with every ranged slot silent; once
RU-31's sixty files exist, 3,741 additional ranged voices (1,414 `attack-*`, 2,091
`release-*`, 236 `miss-*`) enter the same 2,526-tick battle, and the true peak
cannot be known until a render actually includes them. RU-20's mandate — produce a
number before anyone pays for sixty files — is satisfied for the suppression
question, which is what gates spending; the loudness question needs one more pass
over `mix-output` after RU-31 lands, and that is not a new task this plan needs to
add, since RU-31's own acceptance criteria already requires a human to listen.

For completeness: the melee-only clipping regression wave 4 flagged (+0.9 dBFS, 8
clipped samples on a 500-agent V4 battle, against the −0.2 dBFS and zero clipped
samples in `docs/research/SOUND-CAPACITY-MEASUREMENTS.md` section 7.2a) does not
reappear in this V5 measurement — peak voices here is 34, not 54, because this is a
different preset, roster, and combat pattern, not a fix to that regression. It
remains unresolved and out of this package's scope, as already recorded above.

### Wave 5's result, measured on the integration branch after merging

Wave 5 dispatched RU-21 and RU-22 from the plan's own wave, plus RU-20's re-run and
RU-40. All four merged without a conflict. Before dispatching, `main` was merged
into `ranged-units` — 186 commits of Sandata work, one conflict in `scripts/sfx.ps1`
where both sides had added independent content at the same point, resolved by
keeping both.

Measured on the merge commit, in `Release`:

```
Core:   Failed: 1, Passed: 2414, Total: 2415
Client: Failed: 0, Passed: 3293, Total: 3293
format: [PASS] Formatting verification completed.
```

**The Client suite is green.** The known-red window's Client half, twenty-one
failures at its widest, closed exactly where section 3 predicted it would — at
RU-22. The single remaining Core failure is the leader fact, which needs both V8 and
V9 registered; RU-21 supplied V8, so RU-30 closes it in wave 8.

Note that Core's total dropped from 2648 to 2410 when `main` merged in. That is
`fd8435c`, which consolidated the movement-matrix and profile-row suites on `main`.
It is not lost coverage, and an agent comparing against the wave 4 figure without
knowing this will think 238 tests vanished.

**Every frozen preset held, verified across the whole matrix rather than only the
default:**

| Combat | Movement | `stateHash` | `eventHash` |
| --- | --- | --- | --- |
| V4 | V4 | `1B73FC5923879AA0` | `AC55684F24D39344` |
| V5 | V4 | `CA230133F128B1A9` | `6953A1C982A3014C` |
| V4 | V6 | `24EA6F2183A3D05B` | `2B8DE43B3CAAEF92` |
| V4 | V7 | `B6B0AB6C575D2FE6` | `3298D40F15FC43DE` |
| V4 | V8 | `43458DD43FA3F564` | `AC55684F24D39344` |
| V5 | V8 | `216412BC51B838E3` | `B7DAB19F52CB0D67` |

V6 and V7 were measured on the integration branch both before and after RU-21 and
came back byte-identical, which is the evidence that RU-21's code motion was safe.
That check mattered: RU-21 hoisted the `target` lookup in `GatherMovementProposals`
above the contingent-cohesion branch, and that branch runs for every preset except
`IndependentPursuitV1`. A hoist that changed V6 or V7 would have been a silent
regression in a preset nobody in this package was looking at.

**A sixth known-wrong row: RU-21's "same `stateHash` as V4" criterion cannot be
met, by construction.** `StateHasher.Compute` folds `movementContentHash`, so a
distinct movement preset necessarily produces a distinct `stateHash` even when its
behaviour is byte-identical. The row should have asked for the same `eventHash`,
and it gets it — V8 on a melee-only roster returns `AC55684F24D39344`, identical to
V4, which is the real proof that the ordered event stream is unchanged. RU-21 also
supplied a stronger behavioural proof than either hash: a lockstep test running V4
and V8 side by side for 600 ticks, comparing per-agent position, hit points,
intent, liveness, target, and movement resolution every tick.

**`PrecolonialPhilippinesV5` cannot run under V6 or V7.** The combination fails
with `No movement profile is registered for this loadout under movement preset
EquipmentRelativeFootworkV6 ... Actual value was CombatLoadout { Weapon = Bangkaw
... }`. This is expected rather than broken — V8 is the movement preset that carries
ranged behaviour — but it is a real constraint on what RU-25 may name in
`ArenaGame.BuildScenario` and on what any later benchmark may ask for. A ranged
roster runs under V4 or V8 and nothing else.

**RU-21 found and fixed a real defect the plan did not anticipate.** The
contingent-cohesion aim-point branch would have intercepted a ranged agent's turn
with a `continue` before the standoff check ever ran, closing a held warrior onto
its target's body-contact ring and making the hold arm dead on arrival. This is the
same class of failure as the optional trailing parameter — code that compiles,
tests that pass, and a feature that never executes — arriving through a different
mechanism, an earlier `continue` in a shared loop. Worth watching for in RU-25 and
RU-30, both of which add branches to loops that already have several.

### Wave 7's baseline, and a seventh unowned exhaustive switch that arrived from `main`

Before wave 7 was dispatched, `main` was merged into `ranged-units` a third time. The
merge was clean — no conflict in any file — and produced the integration commit
`d59bc08`. It brought thirty-two files of Sandata work plus the first wave of the
`attack-animation-v2` package, which added six new source files under
`src/Hukbo.Client/Presentation/`.

The baseline was then re-measured from scratch rather than carried forward, in
`Release` only, because `Debug` adds two unrelated allocation failures.

```
Core:   Failed: 1, Passed: 2414, Total: 2415
Client: Failed: 2, Passed: 3326, Total: 3328
format: [PASS] Formatting verification completed.
```

Core is unchanged and its single failure is still the leader fact that RU-30 closes by
registering `MonotoneAllyClearanceV9`. The whole six-cell hash matrix was re-run at
200 agents, 10,000 ticks, seed 1, and every cell reproduced its recorded value
byte-for-byte:

| Combat | Movement | `stateHash` | `eventHash` |
| --- | --- | --- | --- |
| V4 | V4 | `1B73FC5923879AA0` | `AC55684F24D39344` |
| V5 | V4 | `CA230133F128B1A9` | `6953A1C982A3014C` |
| V4 | V6 | `24EA6F2183A3D05B` | `2B8DE43B3CAAEF92` |
| V4 | V7 | `B6B0AB6C575D2FE6` | `3298D40F15FC43DE` |
| V4 | V8 | `43458DD43FA3F564` | `AC55684F24D39344` |
| V5 | V8 | `216412BC51B838E3` | `B7DAB19F52CB0D67` |

**The Client suite is no longer green, and the cause is the merge rather than anything
this package did in wave 6.** Two facts in
`tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs` fail:
`Resolve_MapsEveryWeaponToItsDeclaredMotionFamily` and
`Resolve_CarriesBoundedPresentationOnlyMotionData`. Both sweep
`Enum.GetValues<WeaponId>()` and feed it to `AttackMotionCatalog.Resolve`
(`AttackMotionCatalog.cs:74-80`), whose switch has arms for `Kampilan`, `Wasay`,
`Kalis`, and `Itak` and a throwing default. The failure message is `Expected: 7 Actual: 4`.

This is the same unowned-exhaustive-switch mechanism section 3 has now recorded seven
times, arriving through a mechanism none of the earlier six used: a second package,
developed in parallel on `main`, wrote a new exhaustive switch over an enum this package
had already widened on its own branch. Neither branch was wrong in isolation, and
neither branch's suite could see it. The `attack-animation-v2` plan
(`docs/plans/2026-08-08-attack-animation-v2.md:53`) requires its catalog to cover *all
registered* `WeaponId` values, so the obligation is real on both sides; it falls to this
package because this package is the one adding the weapons.

It is filed as RU-44 and is not merely cosmetic. `AttackMotionCatalog.Resolve` has
exactly one production caller, `AttackAnimationSystem.cs:69`, which resolves
`contact.Weapon`; contacts are built from `Attack` events, and an `Attack` event carries
`source.Loadout.Weapon` (`BattleSimulation.cs:4311`). A ranged loadout therefore reaches
the throwing default the moment the animation system is wired into `ArenaGame`. It is not
wired yet — nothing outside `src/Hukbo.Client/Presentation/` references either
`AttackAnimationSystem` or `AttackContactDispatcher` — so today the defect shows only as
two red tests, exactly as RU-42's did not show at all.

### RU-36 is decided: conditional fold. And a ninth known-wrong row, found while preparing RU-24

The user selected **option 1, the conditional fold**, on 2026-08-08, after three waves and
four asks. `CombatRuleset.AddProfile` (`CombatRuleset.cs:641-647`) folds
`ProjectileSpeedRaw`, `StandoffDistanceRaw`, and `FlightTickCeiling` into the preset
content hash **only for a profile that declares them**, and writes nothing at all — not a
zero, not a count — for a profile that does not. That is the shape
`MovementRuleset.cs:647-663` already uses for V7's pressure-interrupt weights, and it is
the shape `ComputeContentHash` already uses twice more in this very file, for
`_weaponAttributes` at `:798` and `_declaredClashProfile` at `:826`, both of which carry
comments explaining that a preset which declares nothing mixes nothing so that version
1's content hash and every replay recorded against it stay where they are. The predicate
is not new either: `WeaponProfile.cs:152-154` and
`BattleSimulation.DetermineHasRangedWeapon` (`:1080-1096`) already test the same fields.

V1 through V4 keep their pinned literals at `DeterminismTests.cs:134`, `:154`, `:177`,
and `:199`. No preset version is bumped. V5 has no pinned content hash yet, and RU-36
must not add one — RU-26 owns that pin and lands after RU-24 has finished moving the
values, so that the literal is captured once from a real run rather than captured, moved,
and re-pinned.

**The ninth known-wrong row is RU-24's own file list.** The row says to tune the
"`RangedStandoffV8` standoff fraction" and names
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs` as one of the three files its agent
may edit. There is nothing in V8 to tune. `RangedStandoffV8Ruleset`
(`MovementPresetRegistry.cs:456-477`) restates every one of `PersistentContingentsV4`'s
tunables verbatim and declares no field of its own, which its own remarks state plainly;
`MovementRuleset` contains no field with "standoff" in its name at all. The standoff
distance is read from the weapon profile, not the movement ruleset:
`BattleSimulation.cs:1725-1728` gates on `Scenario.MovementPreset ==
MovementPresetId.RangedStandoffV8` and then reads
`ResolveAttackerWeaponProfile(agent.Loadout).StandoffDistanceRaw`.

So the standoff lever is `standoffWorldUnits` on the three `RangedProfile` declarations in
`PhilippineCombatPresetV5.cs:242`, `:254`, and `:267`, where all three weapons currently
sit at exactly 0.75 of their reach — Bangkaw 36 of 48, Busog 60 of 80, Arquebus 84 of 112.
An implementer who follows the row literally will go looking for a knob that does not
exist and add one to `MovementRuleset`, which would move the content hash of every
movement preset from V1 to V7 unless it were itself conditionally folded. That is the
exact mess option 1 was chosen to avoid, arriving through a different door. RU-24's agent
is told this and is forbidden to touch `MovementPresetRegistry.cs`.

One more thing RU-24 does not own, recorded so the next wave does not look for it in the
wrong place. The first tuning lever is the ranged roster share, and that is not preset
data. With `Scenario.RosterCounts` left empty the simulation falls back to
`rules.ResolveLoadout(entityId)` (`BattleSimulation.cs:571-574`), which spreads warriors
evenly across all seven roster entries — so every V5 measurement recorded so far,
including the V5/V8 cell of the hash matrix, was taken at three ranged entries of seven.
RU-24 varies the share inside its harness to find a workable value, but the place that
value has to be written down so the running game uses it is `ArenaGame.BuildScenario`,
which is RU-43, and the ranged termination tests, which are RU-29. Both already depend on
RU-24.

### RU-36 landed, and its measured signature is the proof it was scoped correctly

RU-36 merged into `ranged-units` from branch `ru-36` at `d63a368`. The fold sits in
`CombatRuleset.AddProfile`, guarded by `isRangedDeclaration`, copied verbatim from the
`WeaponProfile.cs:152-154` predicate so the hash's notion of "ranged" cannot drift from
the constructor's. The flag itself is not folded, for the reason V7 already recorded:
inside the branch it is always true, so it would contribute a constant and discriminate
nothing.

Three facts were added to `CombatConfigurationTests.cs` and Core rose from 2,415 to 2,418.
None of them pins a literal. One asserts a ranged profile hashes differently from the same
profile with its three ranged values zeroed, which fails if the fold is dropped entirely.
One changes each of the three fields in turn while holding the other two, which fails if
the fold carries only one or two of them. One re-establishes dictionary-order independence
for a melee-only pair, which fails if the guard broke the existing ordering guarantee.

The measured result after merging is the interesting part, because it is the signature a
correct conditional fold has to produce and nothing else does:

| Combat | Movement | `stateHash` | moved? | `eventHash` | moved? |
| --- | --- | --- | --- | --- | --- |
| V4 | V4 | `1B73FC5923879AA0` | no | `AC55684F24D39344` | no |
| V5 | V4 | `8719AA720AE66F91` | **yes**, was `CA230133F128B1A9` | `6953A1C982A3014C` | no |
| V4 | V6 | `24EA6F2183A3D05B` | no | `2B8DE43B3CAAEF92` | no |
| V4 | V7 | `B6B0AB6C575D2FE6` | no | `3298D40F15FC43DE` | no |
| V4 | V8 | `43458DD43FA3F564` | no | `AC55684F24D39344` | no |
| V5 | V8 | `932B7A4F490D139B` | **yes**, was `216412BC51B838E3` | `B7DAB19F52CB0D67` | no |

Every cell whose combat preset is V4 is byte-identical, which is what "a melee profile
mixes nothing here at all" has to mean if it means anything. Both V5 state hashes moved,
because `BattleSimulation.ComputeStateHash` seeds itself with `_rules.ContentHash`
(`BattleSimulation.cs:800`) and V5 is the only preset that declares a ranged profile.
**Both V5 event hashes are unchanged**, which is the load-bearing observation: the event
hash is the ordered event stream, so an unchanged event hash across a changed state hash
says the fold altered the digest and did not alter one tick of gameplay. That is exactly
the claim the fold makes about itself, measured rather than asserted.

Neither of the two new V5 state hashes is a golden. RU-24 retunes the values the fold now
carries, so both will move again before the day is out, and RU-26 captures the literals
once afterwards from a real run.

Core stands at 1 failed, 2,417 passed, 2,418 total on the integration branch; the single
failure is still the leader fact RU-30 owns. Client is 0 of 3,328. Format passes.

### RU-24's result, reproduced independently, and a tenth known-wrong row

RU-24 merged from branch `ru-24` at `9aab100`. Every band below was re-measured by the
orchestrator rather than accepted from the task agent's report, by compiling the harness
with `-p:DefineConstants=HUKBO_CALIBRATION` and running it directly. Every figure matched.

**Only one of the four tuning levers moved, and it was the fourth.** The ranged weapon
profiles are untouched: Bangkaw stays at damage 10, reach 48, cooldown 25, speed 8,
standoff 36, flight 10, and Busog and Arquebus likewise. All three standoffs are still
exactly 0.75 of reach, and the shot intervals still read 25, 45, and 240. What moved is
the ranged half of the weapon-intercept matrix, uniformly: the twelve melee-defender
versus ranged-attacker cells by two and a half times, and the twenty-one ranged-defender
cells by three. A uniform scale preserves every ordering the hand-authored values
established, within each row and across rows, and both blocks carry a `RU-24 CALIBRATION
PASS, PROVISIONAL` comment recording the pre-tuning measurement and stating that this is
a gameplay tuning choice rather than a historical measurement.

The reasoning the agent recorded for skipping the first three levers is worth keeping,
because it is the sort of thing that gets re-derived expensively: melee weapons fire on
four-to-eight-tick cooldowns against the ranged weapons' twenty-five to two hundred and
forty, so most of the attack volume landing on the ranged quarter of the roster passes
through the ranged-defender cells. Those cells were measuring eleven to twenty-one per
cent defended. Raising the ranged population share would have pushed *more* volume through
them and moved the pooled figure further below the floor, not toward it.

Measured, all twenty seeds at two hundred agents:

| Band | Result |
| --- | --- |
| (a) `DefenceAttributableShare` inside 0.25–0.45 | **PASS, 20 of 20.** Range 0.2528 to 0.2853 |
| (b) shielded entries absorb more blows than shieldless | **BLOCKED — see below** |
| (c) 19 of 20 decisive before 5,000 ticks, median at or below 5,000 | **PASS.** 20 of 20 decisive, median 1,489 |
| (d) each faction wins at least four of twenty | **PASS.** Faction 0 wins 13, faction 1 wins 7 |
| (e) ten-cell matrix beside the V4 baseline | **PASS.** 1,264 to 2,416 ticks against V4's 1,279 to 4,405 |

Band (a) passes across the whole seed range but sits in its lower third, between 0.2528
and 0.2853 against a floor of 0.25. The narrower 0.30 to 0.40 that
`PhilippineCombatIntegrationTests.cs:683-692` calls the design target is not met, and that
file is explicit that the narrower range is deliberately not a second gate — so this
passes the criterion it was given. It is worth recording that seed 13 sits 0.0028 above
the floor, which is close enough that a later change touching clash channels should
re-run this harness rather than assume the margin.

**The tenth known-wrong row is RU-24's own band (b), and it cannot be met by tuning.**
`PhilippineCombatPresetV5`'s roster carries `ShieldId.None` on all seven entries
(`PhilippineCombatPresetV5.cs:300-306`), because it restates V4's roster verbatim and V4's
own roster had already dropped every shield `PrecolonialPhilippinesV2` carried
(`PhilippineCombatPresetV4.cs:219-222`). There is no shielded roster entry to measure, so
the shielded total is zero for every roster share the harness can build. The band was
written by carrying the relationship from the V2-era integration test at
`PhilippineCombatIntegrationTests.cs:793-800` into a plan whose target preset has no
shields, and it was unmeetable the day it was written. Nothing regressed: the shield
interception channel and the tall-hardwood multipliers still exist in the ruleset and are
still exercised by the tests that build their own rosters; they are simply unreachable
from V5's roster, exactly as they are unreachable from V4's today. Making the band
measurable means giving a V5 roster entry a shield, which changes gameplay and both
hashes and is a design decision rather than a calibration lever. **It is not in RU-24's
scope and is left open for the user.**

The roster share RU-24 settled on is `[19, 19, 19, 18, 11, 8, 6]` in roster order,
apportioned by largest remainder, which is a twenty-five per cent ranged quarter weighted
toward the Bangkaw. It lives only in the harness's `DefaultRosterWeights`. **RU-24 could
not write it into the running game and did not try** — that is RU-43's job in
`ArenaGame.BuildScenario` and RU-29's in the ranged termination fixtures, and both must
carry this weighting forward or they will measure a different battle than the one
calibrated here.

Measured on the integration branch after merging, with RU-36 also present:

| Combat | Movement | `stateHash` | `eventHash` |
| --- | --- | --- | --- |
| V4 | V4 | `1B73FC5923879AA0` | `AC55684F24D39344` |
| V5 | V4 | `B510FE49839A03B1` | `50D772D4142AF729` |
| V4 | V6 | `24EA6F2183A3D05B` | `2B8DE43B3CAAEF92` |
| V4 | V7 | `B6B0AB6C575D2FE6` | `3298D40F15FC43DE` |
| V4 | V8 | `43458DD43FA3F564` | `AC55684F24D39344` |
| V5 | V8 | `3E003AD847691E00` | `7DAFA4F0959A2503` |

Every V4-combat cell is still byte-identical. Both V5 cells moved on both hashes this
time, because the intercept retune changes gameplay and not merely the digest. The two V5
event hashes match what RU-24 measured on its own branch, while the two V5 state hashes do
not — RU-24 branched before RU-36 merged, so its state hashes lacked the ranged fold.
That the event hashes agree across the two branches and the state hashes differ by exactly
the fold is a consistency check on both tasks at once.

**These six values are the ones RU-26 and RU-27 pin.** Core stands at 1 failed, 2,417
passed, 2,418 total; Client at 0 of 3,328; format passes.

### The user resolved band (b), and RU-28's row turned out to be stale too

**Band (b) is being made measurable rather than written off.** Asked whether V5 should
gain a shielded roster entry, the user said yes on 2026-08-08. That is RU-45. It is not a
one-line roster edit, and the reason is worth recording before anyone estimates it again:
`ClashProfile.ResolveWeaponIntercept` (`ClashProfile.cs:272-293`) and `ResolveVoid`
(`:312-328`) both throw `ArgumentOutOfRangeException` on a key the profile does not carry,
and only the `Neutral` profile resolves an unknown key to zero. V5 declares not one
`TallHardwood` defender key in either table — the whole file mentions `TallHardwood`
exactly once, in `shieldMultipliers`. So a shielded roster entry without its cells is a
runtime throw, not a red test, and this would have been the eighth instance of the
missing-arm shape in this package if it had been discovered by launching the game instead
of by reading the lookup.

RU-45 therefore restates V2's precedent (`PhilippineCombatPresetV2.cs:217-222`, which
paired each one-handed weapon as a shieldless row and a `TallHardwood` row and gave the
two-handed weapons no shielded row at all), taking V5's roster from seven entries to nine,
and adds the fourteen intercept cells and two void-channel entries those two new defender
keys require. It then re-measures all five bands, with (b) live for the first time. Band
(a) is the one to watch: shield intercepts count toward the defended share, so a figure
currently sitting between 0.2528 and 0.2853 moves **toward the 0.45 ceiling**, and the
band may not be widened at either end.

The roster going from seven entries to nine ripples into RU-43, whose four-category
composition panel now has nine roster entries to map onto rather than seven, and into
RU-29, which must build its fixtures from `Rules.Roster.Count` rather than a literal.

**The eleventh known-wrong row is RU-28's.** Its row lists eight projectile pins and says
to put them in one new file "so they do not collide with the existing suites". That was
true when it was written and stopped being true when RU-17 landed:
`tests/Hukbo.Core.Tests/ProjectileTests.cs` already exists at 353 lines and already
carries four of the eight — flight time at `:41`, the launch-tick clash roll at `:108`,
the pool ceiling at `:170`, and bounded allocations at `:219`. An agent following the row
literally would have written four duplicate tests, added suite runtime, and produced no
coverage. RU-28's real scope is the remaining four — order independence, simultaneous
arrival, dead-attacker delivery, and save-and-resume across a mid-flight projectile —
plus confirming the four that exist pin what their names claim and that the allocation pin
runs on a ranged roster rather than a melee one.

Both tasks were dispatched with the same two constraints, because they run at the same
time: neither may pin a hash literal, since RU-26 and RU-27 own every literal and land
afterwards, and neither may hardcode the roster length at seven while RU-45 is moving it
to nine.

### RU-45 and RU-28's results, both reproduced independently

**RU-45 made band (b) real, and band (a) improved rather than degraded.** The roster is
nine entries: the original seven byte-identical and in order, with
`(Kalis, LightOrganic, TallHardwood, Timawa)` and
`(Itak, LightOrganic, TallHardwood, AlipingNamamahay)` appended at the same rank as each
weapon's shieldless row, so rank is not a hidden second variable in the comparison.
Fourteen intercept cells and two void-channel entries were added.

One thing about those values looks wrong at a glance and is not, so it is recorded here to
save the next reader the same double-take. **The new shielded cells are numerically lower
than the shieldless ones** — Kalis with a shield intercepts a Bangkaw at 700 where the
shieldless Kalis row reads 1,750. That is V2's own design inherited intact, not an
inversion: a shielded defender's protection arrives mostly through the separate
`shieldIntercept` channel, which V5 sets at 2,400 basis points, so the weapon channel is
deliberately reduced. The four melee columns are `PhilippineCombatPresetV2.cs:278-286`
verbatim and the two void entries are `V2:315-316` verbatim; the three ranged columns are
new, derived by applying V2's own shielded-to-shieldless ratio — about 0.40 for Kalis and
0.36 for Itak — to V5's post-RU-24 shieldless cells. Those ratios were checked against V2
and hold to within a percentage point, so the ranged columns sit on the same footing as
the melee ones rather than on a fresh invention.

Re-measured by the orchestrator, not taken from the report:

| Band | Before RU-45 | After RU-45 |
| --- | --- | --- |
| (a) share inside 0.25–0.45 | PASS, 0.2528–0.2853 | **PASS, 0.2766–0.3151** |
| (b) shielded absorb more | **BLOCKED, unmeasurable** | **PASS, 20 of 20** |
| (c) decisive before 5,000 | PASS, 20 of 20, median 1,489 | PASS, 20 of 20, median 1,415 |
| (d) each faction wins four | PASS, 13 / 7 | PASS, 14 / 6 |
| (e) ten-cell matrix | PASS | PASS, 1,126–2,727 ticks |

Band (a) moved up into the middle of its range as predicted and is now further from the
0.25 floor than it was, with seed 13's old 0.0028 margin gone. Band (b) measures shielded
entries absorbing 13.4 to 15.4 blows against shieldless 10.7 to 11.9, a ratio between 1.16
and 1.40 — which lands close to the 13.3-against-16.3 figure
`PhilippineCombatIntegrationTests.cs:793-800` reasons from, on a preset that reached it by
a different route.

**One thing in the harness is stricter than the plan and should not be mistaken for a plan
requirement.** RU-45's band (b) check asserts `shieldedMean > shieldlessMean * 1.15`
rather than simply greater. The plan says "absorb more". A fifteen per cent margin is a
tightening, not a widening, and it passes — but it is the harness's own choice, and a
later task that reads 1.15 as the contract will be reading something no plan row says.

**RU-28 added four pins and audited the four that already existed.** All four existing
pins in `ProjectileTests.cs` do pin what their names claim, with no gap found, and the
allocation pin does run on a ranged roster — both agents carry `WeaponId.Bangkaw` against
a `PrecolonialPhilippinesV5` scenario, so it is a live ranged duel rather than a melee one
mislabelled. The four new pins are storage-order independence across three orders,
same-tick simultaneity where two arrivals together kill a target neither would kill alone,
delivery by a shooter zeroed mid-flight, and a mid-flight snapshot compared against an
independently advanced simulation. The save-and-resume pin compares two hashes both
computed live in the test and never a literal, which is what kept it out of RU-26's and
RU-27's territory.

Measured on the integration branch with everything merged:

| Combat | Movement | `stateHash` | `eventHash` |
| --- | --- | --- | --- |
| V4 | V4 | `1B73FC5923879AA0` | `AC55684F24D39344` |
| V5 | V4 | `47EDD2F7515E291D` | `656D132F9F211D54` |
| V4 | V6 | `24EA6F2183A3D05B` | `2B8DE43B3CAAEF92` |
| V4 | V7 | `B6B0AB6C575D2FE6` | `3298D40F15FC43DE` |
| V4 | V8 | `43458DD43FA3F564` | `AC55684F24D39344` |
| V5 | V8 | `C8023D3B5BEB005E` | `F709A345E2F7370E` |

All four V4-combat cells are still byte-identical, five merges into this wave. The V5/V4
cell is the one RU-45 flagged as not measured and left for the orchestrator; it is measured
here. **These six values are final for the wave and are what RU-26 and RU-27 pin** — no
task remaining in the package moves a hash except RU-30, which registers V9 and touches no
V4 or V5 cell.

Core stands at 1 failed, 2,421 passed, 2,422 total; Client at 0 of 3,328; format passes.
RU-28 reported honestly that it had not run `format.ps1`; the orchestrator ran it.

### Wave 8's dispatch, and two more wrong rows found while scoping it

RU-26, RU-27, and RU-29 were dispatched together off `19870ff` — three disjoint file sets,
and the hashes had stopped moving, which is what wave 8 was waiting for. Scoping them
turned up the twelfth and thirteenth known-wrong rows, both found by reading the code the
row points at rather than by an agent hitting them.

**The twelfth is RU-26's zero-ranged inert control.** The row asks it to prove that a V5
scenario with every ranged roster entry zeroed "reproduces V4's seed-1 state and event
hashes exactly". The state hash cannot match and never could:
`BattleSimulation.ComputeStateHash` seeds itself with `_rules.ContentHash`
(`BattleSimulation.cs:800`), and V5's content hash necessarily differs from V4's because
V5 declares three ranged weapon profiles and V4 declares none. RU-36's conditional fold
widened that gap further. **This is the identical defect RU-21's row already hit** — its
"same `stateHash` as V4" criterion was recorded in this document as unachievable for
exactly this reason, and the row was written anyway. The control keeps all of its value
by asserting what RU-21's resolution asserted: the *event* hash matches and the state hash
does not. An unchanged ordered event stream across a changed digest is the real proof that
no ranged fold leaked into a melee-only run.

RU-45 also made that control harder in a way its row could not have known. V5's roster is
nine entries now against V4's four, so zeroing only the three ranged entries leaves six
melee entries and a different battle. The two shielded entries have to be zeroed as well.

**The thirteenth is RU-27's file list.** The row names only
`MovementPresetFreezeTests.cs`, but the freeze tests do not compute a digest — `LoadDigest`
(`MovementPresetFreezeTests.cs:430-441`) reads a committed JSON fixture from
`Fixtures/` and asserts the file exists. RU-27 has to create
`seed-1-200-agents-movement-v8-digest.json` as well, and there is no generator anywhere in
the repository: nothing under `tools/` or `scripts/` writes these files, and the seven
existing fixtures record only a `provenance` block naming the commit they were captured
at. RU-27 was told to add a capture routine behind `#if HUKBO_CALIBRATION`, following the
precedent `RangedCalibrationHarness.cs` already set for hand-run measurement code, and to
keep it so the next preset does not have to reinvent it.

Two constraints were given to all three agents, because they run concurrently: only RU-26
may write a hash literal, and none of them may write the roster length as a literal, since
it has already changed size once inside this package.

### Task status

| Task | Status |
| --- | --- |
| RU-01 | Done on branch `ru-01` at `21cd148`, not yet integrated |
| RU-02 | Done on branch `ru-02` at `6320ef1`, not yet integrated — CONFIRMED, see the finding above |
| RU-03 | Done on branch `ru-03` at `5f2e5f6`, not yet integrated — opened the known-red window; see the correction in section 3 |
| RU-04 | Done on branch `ru-04` at `a7cebde`, not yet integrated |
| RU-05 | Done on branch `ru-05` at `8b1a88e` and `2c7f854`, not yet integrated |
| RU-06 | Done on branch `ru-06` at `7a065eb`, merged into `ranged-units` — acceptance number corrected to 692,750; counters not yet in a live report, see RU-37 |
| RU-07 | Done on branch `ru-07` at `746669e`, merged into `ranged-units` |
| RU-08 | Done on branch `ru-08` at `5c4b4b4`, merged into `ranged-units` |
| RU-09 | Done on branch `ru-09` at `84b78dc`, merged into `ranged-units` |
| RU-10 | Done on branch `ru-10` at `42002d7`, merged into `ranged-units` — took the Client window to 34, see the second correction in section 3 |
| RU-11 | Done on branch `ru-11` at `3208c86`, merged into `ranged-units` |
| RU-12 | Done on branch `ru-12` at `9ab3b3e`, merged into `ranged-units` — took Core from 10 red to 2; exposed RU-39 |
| RU-13 | Done on branch `ru-13` at `e4bf289` — both hashes unchanged; end-to-end ranged path untested until V5 is registered |
| RU-14 | Done on branch `ru-14` at `1eb93d5` — closed the `SoundCatalogTests` pair |
| RU-15 | Done on branch `ru-15` at `3e7c33a` and `2fac60d` — `-List` now reports 13 present of 26 instead of 4 |
| RU-16 | Done on branch `ru-16` at `d09e8ee` — inspector line live, per-faction count inert until RU-38 |
| RU-17 | Done on branch `ru-17` at `a9a54c1`, merged into `ranged-units` — the projectile pool is live; V4 hashes held and V5's moved, see the wave 4 result in section 9 |
| RU-18 | Done on branch `ru-18` at `37620c4`, merged into `ranged-units` — five new files, resolver not yet wired into the draw loop, which is RU-25's |
| RU-19 | Done on branch `ru-19` at `ef85d78`, merged into `ranged-units` — release and miss cues fire end to end; left a one-argument `Ingest` seam that RU-40 closes |
| RU-20 | Done — harness parity landed on branch `ru-20` at `e2c73d4`, merged into `ranged-units`; re-run after RU-17 on branch `ru-20-rerun`, not yet integrated — 500 agents, V5, seed 1: **−0.2 dBFS, 0 clipped samples, 0 suppressions**, shipped 16-per-slot cap does not bind even on raw ranged demand. See the RU-20 re-run result in section 9. **The suppression question is cleared for RU-31; the loudness question stays open until RU-31's files exist and get one more mix pass.** |
| RU-21 | Done on branch `ru-21` at `0281e5f`, merged into `ranged-units` — `RangedStandoffV8` registered, `AgentIntent.Holding` has exactly one producer at `BattleSimulation.cs:1741`. Its "same `stateHash` as V4" criterion is unachievable by construction; see the wave 5 result in section 9 |
| RU-22 | Done on branch `ru-22` at `6ff87ab` and `532ae4c`, merged into `ranged-units` — **the Client suite is green, 0 failed of 3293.** Two stale cardinality pins now derive their role factor from the enum rather than carrying a literal |
| RU-23 | Done on branch `ru-23` at `59319b2`, merged into `ranged-units` — Busog pins one quad higher at 25, Bangkaw and Arquebus at 24; the projectile population is counted separately at one quad each against `Scenario.MaximumProjectilesInFlight`. **Its renderer-parity criterion holds for the four melee roles only; see RU-42.** |
| RU-24 | **Done on branch `ru-24` at `9aab100`, merged into `ranged-units`.** Bands (a), (c), (d), and (e) all pass, re-measured independently by the orchestrator rather than taken from the report. Band (b) is **BLOCKED and unmeetable by tuning** — V5's roster fields no shield on any entry, so there is nothing shielded to compare; it is the tenth known-wrong row and needs a user decision, not a retune. Only lever 4 moved: the ranged intercept cells, uniformly, marked `PROVISIONAL` in source. The chosen roster share `[19, 19, 19, 18, 11, 8, 6]` lives only in the harness and **must be carried into RU-43 and RU-29**. See the RU-24 result in section 9 |
| RU-25 | Done on branch `ru-25` at `900dff7` and `ffcabe3`, merged into `ranged-units`. The client now runs `PrecolonialPhilippinesV5` + `RangedStandoffV8`; `Scenario.CreateDefault` and the headless default are untouched on V4. **Its own acceptance is only partly demonstrable: `run.ps1` throws on the first ranged pawn (RU-42), so the projectile draw path has never executed.** Two findings recorded as RU-42's widening and RU-43. |
| RU-26 | Not started |
| RU-27 | Not started |
| RU-28 | **Done on branch `ru-28` at `9e95864`, merged into `ranged-units`.** Its row was the eleventh known-wrong one: `ProjectileTests.cs` already carried four of its eight pins, so the real scope was the remaining four plus an audit of the existing four. All four existing pins hold with no gap, and the allocation pin does run on a ranged roster. Four new pins added, no hash literal among them. See the result in section 9 |
| RU-29 | Not started |
| RU-30 | Not started |
| RU-31 | Not started |
| RU-32 | Not started |
| RU-33 | Not started |
| RU-34 | Done on branch `ru-34` at `7b80c24`, merged into `ranged-units` — took Core from 18 red to 10 |
| RU-35 | Done on branch `ru-35` at `47b0719` — Client 34 red to 29 |
| RU-36 | **Done on branch `ru-36` at `d63a368`, merged into `ranged-units`.** The user selected option 1, the conditional fold, on 2026-08-08 after four asks across three waves. V1 through V4 hold their pinned literals and no preset version was bumped; V5's state hash moved and V5's event hash did not, which is the measured proof the fold changed the digest without changing gameplay. See the RU-36 result in section 9. Three relational facts added, no literal pinned — V5's content-hash literal is still RU-26's to capture after RU-24 |
| RU-37 | Done on branch `ru-37` at `719fbe7` — F-A reports real counters; see the F-A result in section 9 |
| RU-38 | Done on branch `ru-38` at `215bff1`, merged into `ranged-units` — `HoldingCount` now reads a real roster in the running game |
| RU-39 | Done on branch `ru-39` at `53105bd` and `b622c76`, merged into `ranged-units` — Core is down from two red to one; the second commit exists because the first rescoping was tautological, see the wave 4 result in section 9 |
| RU-40 | Done on branch `ru-40` at `24ede78`, merged into `ranged-units` — the one-argument `Ingest` seam is gone and `agents` carries no default |
| RU-41 | Done on branch `ru-23` at `2fc291d`, merged into `ranged-units` — folded into RU-23 rather than dispatched separately, because its only file is one RU-23 already owned. The literal `2` is gone. |
| RU-42 | **Done on branch `ru-42` at `b372670`, merged into `ranged-units`.** The game launches again: a real `run.ps1 -Configuration Debug` run built a 500-agent `PrecolonialPhilippinesV5` scenario and rendered for 52 seconds at 185 fps with zero `err` lines, where before it threw on the first ranged pawn. `WeaponQuadCount` became role-dependent — `5` for Busog, `3` for every other role — because the Busog's arm gained a two-segment `DrawBowstring` so the string can bend with `DrawTension`, and RU-23's Busog pin moved from 25 to **27** as a result. Whole-frame worst case is now 9,944 quads at 200 units and 18,044 at 500; **the 500-unit margin has fallen from 3,468 to 1,956 across RU-23 and RU-42, so the next feature wanting a per-pawn quad owes a fresh measurement rather than an assumption.** One limit on the evidence, not stated by the implementing agent and found by reading the log: `simTicks` was `0` on all 52 frame lines, so the battle never advanced. Ranged pawns were drawn, which is what proves the crash fixed, but every `RangedPhase` stayed neutral — so `WeaponAngleRadians`, `ExtensionRatio`, and `DrawTension` have never been non-zero at runtime and are proven by unit test alone. Whether the five phases actually read as distinct on screen is still open, and it is still RU-32's row and RU-13's bet to settle. Original statement of the defect follows. **Found by RU-23; it blocked RU-25's whole reason for existing.** `PawnRenderer.DrawWeapon` (`src/Hukbo.Client/Rendering/PawnRenderer.cs:1202-1250`) switches over `PawnWeaponRole` with arms for `Itak`, `Kampilan`, `Wasay`, and `Kalis` only, and a `default` arm that throws `ArgumentOutOfRangeException`. It has no arm for `Bangkaw`, `Busog`, or `Arquebus`. The moment RU-25 points `ArenaGame.BuildScenario` at `PrecolonialPhilippinesV5`, the first frame that draws a ranged pawn throws. No task row in this plan owned that switch: RU-22 owned `PawnGeometry.cs`'s four switches and RU-35 owned `PawnAppearanceFactory.cs`'s, and `PawnRenderer.cs` fell between them. This is the same unowned-exhaustive-switch mechanism that section 3 recorded twice already, but it is worse than those, because no test caught it — the Client suite is green at 3,297 with the defect present. `PawnQuadCount.Count` is an independent documentation-driven counting seam that never calls the renderer, so it happily counts `WeaponQuadCount = 3` for a weapon the renderer refuses to draw. That is also why **RU-23's acceptance criterion "the quad count asserted for each pawn configuration equals what the renderer submits for it" is met for the four melee roles and unmet for the three ranged ones**, and why RU-23's 24 / 25 / 24 pins are provisional on this task: a bow or an arquebus whose arm submits a number of quads other than three moves them. The task is to give `DrawWeapon` three real arms, reconcile the quads each submits against `WeaponQuadCount` (making it role-dependent if it must be), and re-pin RU-23's figures against whatever the renderer actually issues. Files: `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `src/Hukbo.Client/Rendering/SubmissionCount.cs`, `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs`. Depends on RU-23 and RU-18 (`RangedPoseResolver` supplies the draw geometry); must land before RU-25 can be smoke-tested and before RU-32's rows can be looked at. **Widened 2026-08-08 after RU-25 merged.** `RangedPose` carries six fields — `Phase`, `WeaponAngleRadians`, `ExtensionRatio`, `TorsoLeanX`, `TorsoLeanY`, `DrawTension` — and only the two torso-lean channels reach the layout, summed into `CreateBodyAnchor` at `PawnGeometry.cs:982-983`. `WeaponAngleRadians`, `ExtensionRatio`, and `DrawTension` are consumed by nothing at all. `PawnGeometry.cs:396-397` documents the limitation honestly, so this is a known partial wiring rather than a silent dead feature, but the consequence is load-bearing and RU-25 made it visible: RU-25 suppresses the swing pose through `RangedPoseResolver.SuppressesSwing`, whose own doc comment justifies suppression on the grounds that *"both poses write the same weapon-line rotation and reach channels into the one weapon line a pawn has"* — and the ranged pose does not write the weapon line. So a warrior in any ranged phase currently draws with a torso lean and an otherwise **neutral** weapon line, and the five draw phases would be largely indistinguishable from one another on screen. RU-42 therefore also owns consuming the three unread channels in the ranged `DrawWeapon` arms. Until it does, RU-32's row "each of the three five-phase draw sequences reading as that weapon" cannot pass, and **RU-13's recorded bet cannot be judged** — the phases would read as arbitrary for a reason that has nothing to do with whether deriving them from the attack cooldown was the right call. |
| RU-43 | **Not started — added 2026-08-08, found and self-reported by RU-25.** The army-composition sliders are inert while V5 is active. `BuildScenario` (`ArenaGame.cs:1402`) no longer sets `RosterCounts = ToRosterCounts(composition)`, because `Settings.ArmyComposition` is fixed at four categories (`ClientSettings.cs:65`) while V5's ruleset fields a seven-entry roster, and `Scenario.Validate` (`Scenario.cs:310`) throws when `RosterCounts.Length != rules.Roster.Count`. Filling it would have traded an unreachable feature for a game that fails to launch, so RU-25 left it unset and `BattleSimulation.Create` falls back to `CombatRuleset.ResolveLoadout`'s cyclic assignment (`BattleSimulation.cs:571-574`), which is what actually guarantees a ranged loadout reaches the roster at all. That was the right call under RU-25's file list and it is not a defect in RU-25. It is, however, a spectator-facing control that silently does nothing, which fails acceptance question 1 exactly as squarely as the problem RU-25 was written to fix. The task is to decide and implement how a four-category composition panel maps onto V5's seven-entry roster — widen `ArmyComposition` to seven categories, or map four onto seven — and restore `RosterCounts`. **Sequencing:** the ranged roster share is RU-24's first tuning lever, so RU-24 settles what the share should be and RU-43 makes the panel able to express it. Files: `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/Settings/ClientSettings.cs`, `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`, and their tests. Depends on RU-24. |
| RU-44 | **Done on branch `ru-44` at `ed34239`, merged into `ranged-units` at `58180ac`. Added 2026-08-08, found by the orchestrator when re-measuring the wave 7 baseline.** The Client suite is green again at 0 failed of 3328, verified independently of the task agent on the integration branch, and the whole six-cell hash matrix reproduced byte-for-byte afterwards, as a Client-only change must. Three families were added rather than one — `OverhandThrow = 4`, `DrawAndRelease = 5`, `BracedDischarge = 6` — on the ground that a hurl, a bowstring release, and a firearm discharge are structurally distinct motions, which also keeps the file's existing one-family-per-weapon shape. The `TrailEligible` judgment call went the way the row anticipated: the blanket `Assert.True` over every `WeaponId` became seven explicit per-weapon pins, four `true` and three `false`, because a trail is the visible sweep of an edge through the air and none of the three ranged releases sweeps one. That is a stricter pin than the blanket assertion it replaced, since each weapon now fails on its own if its flag flips. The four melee profiles were not touched. Original statement of the defect follows. `AttackMotionCatalog.Resolve` (`src/Hukbo.Client/Presentation/AttackMotionCatalog.cs:74-80`) switches over `WeaponId` with arms for the four melee weapons and a throwing default, and it arrived from `main` with the `attack-animation-v2` package after this package had already widened `WeaponId` to seven members. Two facts in `tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs` fail with `Expected: 7 Actual: 4`. The task is to add an `AttackMotionFamily` member and an `AttackMotionProfile` for each of `Bangkaw`, `Busog`, and `Arquebus`, give `Resolve` three real arms, keep the throwing default, and extend the test file's weapon-to-family map and `ShieldCompatible` pins. Every choreography value is a **Provisional reconstruction** under `CLAUDE.md` section 7; the physical weapon classes are Documented and the motions are not. One judgment call is delegated to the implementer and must be reported rather than buried: the bounded-data loop asserts `TrailEligible` is true for every `WeaponId`, which was written when all four weapons swung, so a ranged weapon whose release draws no trail requires that assertion to become per-weapon pins rather than a blanket one. Files: `src/Hukbo.Client/Presentation/AttackMotionCatalog.cs`, `src/Hukbo.Client/Presentation/AttackMotionFamily.cs`, `tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs`. Depends on nothing in this package; must land before RU-33. |
| RU-45 | **Done on branch `ru-45` at `c0bc314`, merged into `ranged-units`.** Band (b) is measurable and passes 20 of 20 at a ratio of 1.16 to 1.40; band (a) improved to 0.2766–0.3151 rather than degrading. The roster is nine entries. Note the harness asserts a 1.15x margin that no plan row requires — see section 9. Added 2026-08-08 by user decision. Makes RU-24's band (b) measurable by giving `PhilippineCombatPresetV5` a shielded roster entry, following V2's one-handed pairing precedent — the roster goes from seven entries to nine, plus the fourteen weapon-intercept cells and two void-channel entries the new `TallHardwood` defender keys require, because both `ClashProfile` lookups throw on a missing key rather than defaulting to zero. Re-measures all five bands, with band (a) now moving toward its 0.45 ceiling rather than its floor. Ripples into RU-43, whose four-category panel now maps onto nine roster entries, and RU-29, which must read `Rules.Roster.Count` rather than carry a literal. Files: `src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs`, `tests/Hukbo.Core.Tests/RangedCalibrationHarness.cs`. Depends on RU-24; must land before RU-26 and RU-27 pin anything. |
