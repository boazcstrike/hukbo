# Weapon Clash, Swing Animation, and Clash Sound — Plan

Date: 2026-07-27
Revision: 6. Revision 5 left the control-run hash unsatisfiable once the content hash moves.
Revision 6 passes a content hash rather than a ruleset, stubs the channel accessor, adds a
per-tick state hash to the fixture, and corrects two more labels.
Design: [2026-07-27-weapon-clash-design.md](2026-07-27-weapon-clash-design.md)
Research: [docs/research/WEAPON_CLASH_1500s.md](../research/WEAPON_CLASH_1500s.md)

## How to read this plan

Every task is a checkbox row carrying an identifier, a one-sentence action, the exact files
it creates or modifies, the tasks it depends on, a workstream label, and the specific named
test or gate output that proves it done. **Every file an agent touches appears in the Files
column of its own task**, including test files; a file named only in a verification column
is a planning defect.

### RED and GUARD

The repository practises test-driven development, but "every new test must fail first" is
false here and stating it without exception forces a dishonest report at the barrier. A
negative assertion such as "no shield never blocks" is satisfied by a stub that never
blocks anything. A bounds test on a type that ships complete in Phase 0 passes
immediately. A termination clause passes comfortably while the resolver is still a stub,
because the stub is the pre-change behaviour.

Every test task is therefore labelled:

- **RED** — must fail before its implementation task and pass after. A RED case that
  passes at the barrier is a defective test.
- **GUARD** — must pass before **and** after. It exists to catch a regression, not to
  drive an implementation. A GUARD case that fails at the barrier means something else
  is already broken.

A red test must in every case fail on an **assertion**, never on a missing type: a test
file referencing a type that does not exist yet fails the whole test assembly to compile
and takes every other test in that assembly with it. That is recorded in
`docs/development/testing.md` for the plains-backdrop re-run. Phase 0 therefore creates
the entire surface, in **both** projects, before Phase 1 writes a single assertion.

### Workstreams

Labels are `CORE`, `CLIENT-ANIM`, `CLIENT-AUDIO`, `AUDIO-ASSETS`, `TESTS`, and `DOCS`. Two
tasks may run in parallel only when they own disjoint files **and** are not mid-edit in
the same test assembly. Ownership and serialisation points are in section 4.

Tasks that **cannot** be verified without a human at an interactive Windows desktop are
marked `HUMAN`. No agent may report one as passing, and no smoke row in
`docs/development/testing.md` may be flipped away from `PENDING` by anything but a person
performing the interaction.

## 1. Phases and barriers

| Phase | Contents | Barrier before the next phase |
| --- | --- | --- |
| Phase 0 — contract, seam, stubs, fixture | Every new type in both projects, the ruleset injection seam, the audio enum and catalog entries, and the pre-change event-stream fixture captured from unmodified `main`. Nothing changes behaviour. | **Hard barrier B0.** `./scripts/format.ps1 -Verify`, a zero-warning Release build of the whole solution including both test projects, and the **entire existing suite green**. |
| Phase 1 — Core tests | Every Core assertion, each labelled RED or GUARD. | **Hard barrier B1.** Format verified, plus a full Release run in which **every RED case fails on an assertion and every GUARD case passes**. Blanket failure is not the criterion and never was. |
| Phase 2 — Core implementation | Existing-test dispositions, resolver, ruleset fold, preset tables and their pinning tests, the attack stage, metrics, event-hash fold, golden re-baseline, acceptance re-tune. | **Hard barrier B2.** Format verified, full suite green, criterion one inside its band, criterion two met, and the seed-1 hash pair **written into `docs/development/testing.md`** by a task, not left in a transcript. |
| Phase 3 — client fan-out | Animation, audio wiring, audio content. | **Soft barrier.** Content may lag; a missing WAV is a silent slot, never a build failure. |
| Phase 4 — gate and record | Canonical gate, acceptance workloads, hash-neutrality proof, oracle re-record, design amendment, smoke rows, skill, standards. | Terminal. |

Phases 0, 1, 2, and 4 are each one agent. Phase 3 is where the parallelism is.

### What research round two removed, and revision 4 added

Round two states its matrices as mass-melee values with crowding, awareness, and fatigue
already applied and directs that the section 5.4 modifiers must not be re-applied. The
crowding modifier is therefore deleted, and with it the two-pass split of the attack
stage, the per-target attacker-count buffer, the crowding word in the mixer key, and the
buffer staleness test. Round two also promoted the void channel from rejected to required,
because the acceptance criterion is measured over it; `Evaded` is a fifth resolution
costing one enum member, one interval, and one event-log line, with no sound slot.

Revision 4 adds three things revision 3 assumed into existence:

**The seam.** Four call sites fetch the ruleset from `CombatPresetRegistry` and no factory
accepts one, so no simulation can be given a `ClashProfile` at all. T08 adds a
`CreateForTesting` overload **and** changes `StateHasher.Compute` to take the ruleset as a
parameter. Without the second half a neutral-ruleset run still folds the shipped
`ContentHash` and the control run proves nothing.

**The fixture.** The control run compares against the pre-change event stream, which stops
existing the moment Phase 2 lands. T10 captures it from unmodified `main` first.

**The audio enum, moved forward.** T12 in revision 3 wrote tests referencing enum members
added by a task that depended on those tests. Because
`SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce` enumerates the enum, the
members and their catalog entries have to land together, and both move to Phase 0.

## 2. Ordered task list

### Phase 0 — contract, seam, stubs, fixture

Nothing here changes behaviour, so the whole existing suite must still be green at B0.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T01 | Add the `AttackResolution` enum with pinned values `Landed = 0`, `ShieldBlocked = 1`, `Parried = 2`, `Deflected = 3`, `Evaded = 4`, documented as entering the event hash. | create `src/Hukbo.Core/Combat/AttackResolution.cs`; modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | — | CORE | GUARD `AttackResolution_PinsItsNumericValues` names all five. |
| [ ] | T02 | Add the immutable `ClashProfile`: sixteen-cell weapon matrix, shield intercept, four void values, four hard-share bases, four hard-share multipliers, hard-share clamp bounds, interception ceiling. Per-field range validation, and a `PROVISIONAL` comment recording that all sixteen matrix cells carry zero evidentiary confidence. | create `src/Hukbo.Core/Combat/ClashProfile.cs`; create `tests/Hukbo.Core.Tests/ClashProfileTests.cs` | T01 | CORE | GUARD `Constructor_RoundTripsEveryTable`. |
| [ ] | T03 | Add `ClashProfile.Neutral`, an all-zero-interception profile, and make it the constructor default. | modify `src/Hukbo.Core/Combat/ClashProfile.cs` | T02 | CORE | GUARD `Neutral_ReportsZeroInterceptionForEveryRosterPair`. |
| [ ] | T04 | Add `BattleEvent.Resolution` as a nullable property, **optional on the `Attack` factory defaulting to `Landed`**, forced null on `NonAttack`. Optional because `BattleEvent.Attack` has twenty call sites across eleven files, nine of them test files owned by other workstreams; a required parameter makes B0 unsatisfiable. | modify `src/Hukbo.Core/Simulation/BattleEvent.cs`; modify `tests/Hukbo.Core.Tests/BattleEventTests.cs` | T01 | CORE | GUARD `NonAttack_LeavesTheResolutionNull` and `Attack_RejectsAnUndefinedResolution`, and the solution builds without editing any of the other ten files. |
| [ ] | T05 | Add `CombatMetrics` and its accumulator: accepted attacks, one counter per resolution, and a derived defence-attributable ratio **with a defined value when no attack was accepted**, since the criterion-one band test reads it. **The zero-attack ratio is exactly `0`**, stated here rather than left to the implementer: an implementer choosing the band centre, or `0.33` to avoid divide-by-zero noise, would make the criterion-one test pass vacuously at the first barrier and, worse, leave it permanently unable to fail if metric accumulation ever regressed to zero. **Also stub both consumers**, because T23 dereferences them in Phase 1: `BattleSimulation.LastTickCombat` returning `default(CombatMetrics)` in the style of `LastTickCollision`, and a defaulted `CombatMetrics` property on `RunReport`. Without these two stubs the Phase 1 assembly does not compile and every other Phase 1 case goes down with it, which is the failure mode this plan exists to prevent. | create `src/Hukbo.Core/Simulation/CombatMetrics.cs`; create `tests/Hukbo.Core.Tests/CombatMetricsTests.cs`; modify `src/Hukbo.Core/Simulation/BattleSimulation.cs`; modify `src/Hukbo.Headless/RunReport.cs` | T01 | CORE | GUARD `Accumulator_RejectsNegativeCountsAndResetsToZero` and `Ratio_IsZeroWhenNoAttackWasAccepted`, and `tests/Hukbo.Core.Tests` compiles against both consumer members. |
| [ ] | T06 | Add `ClashResolver` as a neutral stub: `MixClash` returns zero, `Resolve` returns `Landed`, `SplitWeaponChannel` returns a zero pair, and **`ComputeChannels(ClashProfile, attackerWeapon, defenderWeapon, defenderShield)` returns a zeroed `(shield, hard, soft, void)`**. The fourth member is not optional: four boundary cases in T16 and one in T17 assert on the computed channels — that a total is exactly 5500 and unscaled, that a rescale preserves proportions across three channels, that a total never exceeds the ceiling, that a hard share binds at a clamp bound, and that hard plus soft equals the rescaled weapon channel. None of those is observable through a five-valued `Resolve` outcome, since inferring proportions from outcome frequencies is a distribution test and cannot express "exactly 5500". Without the stub, Phase 1 does not compile. | create `src/Hukbo.Core/Combat/ClashResolver.cs` | T02 | CORE | Referenceable from `Hukbo.Core.Tests` through the existing `InternalsVisibleTo`; existing suite stays green; T16 and T17 compile against `ComputeChannels`. |
| [ ] | T07 | Give `CombatRuleset` an **optional** `ClashProfile` constructor parameter defaulting to `ClashProfile.Neutral`, with accessors returning its values. Optional so the named-argument constructions at `CombatConfigurationTests.cs:268` and `:324` keep compiling untouched. **Also add `public CombatRuleset WithClashProfile(ClashProfile profile)`**, returning a copy with every other field preserved. Without it, building the neutral ruleset means hand-reassembling six constructor arguments from the public surface — sixteen `ResolveWeaponWeight` reads per weapon, twenty-six `ResolveDefenseMultiplier` reads, and a hard-coded armor list, because `_armors` has no accessor yet is folded into `ComputeContentHash` at `CombatRuleset.cs:259-263`. That reassembly is faithful today only because `ArmorId` has one member. | modify `src/Hukbo.Core/Combat/CombatRuleset.cs`; modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | T03, T06 | CORE | Warning-free Release build; `CombatConfigurationTests` compiles with no edit to either construction site; and `WithClashProfile_PreservesEveryFieldExceptTheProfile` asserts the copy has the same `Id`, `Version`, roster, and every resolved weight and multiplier as its source. |
| [ ] | T08 | **Open the seam, on both factories.** Add `internal static BattleSimulation CreateForTesting(Scenario, CombatRuleset, params AgentState[])` for the five section 5 dispositions, **and `internal static BattleSimulation Create(Scenario, CombatRuleset)` with the public `Create` delegating to it after its registry fetch** — the control run is seed 1 at 200 agents, which only `Create` can produce, and no agents can be lifted out of a `Create`d simulation because `Agents` and `CreateSnapshot` return `AgentView` and `_agentStates` is private. The new `Create` overload asserts roster equality against the registry entry for `scenario.CombatPreset`, because `Scenario.Validate` at `Scenario.cs:195` still validates against the registry roster. Then change `StateHasher.Compute` to take a **`ulong contentHash`** parameter instead of re-fetching the ruleset at `StateHasher.cs:15`, fixing **both** call sites: `BattleSimulation.cs:198` passes `_rules.ContentHash`, and `DeterminismTests.cs:152` inside `ComputeSingleAgentStateHash`, which backs three existing tests, passes `CombatPresetRegistry.Get(scenario.CombatPreset).ContentHash`. **The parameter is the `ulong`, not the ruleset**, which is both simpler — `rules` is used at exactly one line, line 32 — and durable: `Fnv1a.Add` runs eight multiply rounds per word regardless of value, so once T26 folds the clash tables into `ComputeContentHash` every content hash moves, including a `version: 1` neutral one. Had the parameter been the ruleset, T21 would pass at B1 and then fail mid-Phase-2 over a non-defect, with no task authorised to touch it and section 6 forbidding a golden edit. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs`; modify `src/Hukbo.Core/Determinism/StateHasher.cs`; modify `tests/Hukbo.Core.Tests/DeterminismTests.cs`; modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | T07 | CORE | GUARD: the whole existing suite passes unchanged including `StateHash_ChangesWhenAnyAgentWeaponArmorOrShieldChanges` and its two siblings, and `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` still reports state hash `D78F0B527B7F938F` and event hash `AC3BAAEC684854D5`. **RED** `Create_WithAnInjectedRulesetRejectsARosterThatDisagreesWithTheScenarioPreset`, because the roster-equality guard is new production behaviour and the existing suite exercises only the registry path. `ComputeStateHash` holds `_rules`, whose `ContentHash` is what the registry path would produce, so the hash check is safe by construction and any movement is a defect in T08. |
| [ ] | T09 | Write the independent naive oracle: a six-step reimplementation in `long` that **calls no production helper**, following the conventions of `NaiveCollisionPairs.cs`, with a comment citing the step-6 ordering sentence in design section 3.3 verbatim. | create `tests/Hukbo.Core.Tests/NaiveClashResolution.cs` | T02 | TESTS | Compiles, and a search for `ClashResolver` inside the file returns nothing. |
| [ ] | T10 | **Capture the pre-change fixture. This is the first task executed in the whole plan**, before T01 and before T04, because T04 adds a field to `BattleEvent` and changes what any serialiser emits, and an agent running T10 afterwards would still believe it captured unmodified `main`. Produce the fixture with a **throwaway harness** that walks `simulation.LastEvents` per tick — `HeadlessRunner` emits only a `RunReport` with an `eventHash`, there is no ordered-event output and no `--events` flag, and adding one would be an unowned production change. The harness is not committed; its source is pasted into the fixture provenance header so the capture is reproducible. **Format: one row per tick**, carrying the event count and an FNV-1a fold over the ordered `(Sequence, Tick, Kind, SourceEntityId, TargetEntityId ?? 0, Value, FactionId, Weapon, HitLocation)` tuples, **excluding `Resolution`** — a post-change event carries a field a pre-change one cannot, and including it guarantees a meaningless mismatch. Full serialisation would be megabytes; a single whole-run fold would destroy the event-for-event comparison. **Each row also carries that tick and its `ComputeStateHash()` value**, one more `ulong` per row. The event half of the digest is already complete, but agent state would otherwise be captured only at the terminal tick, and `DeterminismTests.TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick` carries a docstring on exactly that risk: comparing only the final state lets a divergence that cancels itself out pass unnoticed. `MovementResolution` and `Intent` are folded per agent at `StateHasher.cs:53-54` and emit no event, and a harness walking the public API sees only `AgentView`, which exposes eleven of the eighteen fields `StateHasher` folds — the seven it misses include `AttackCooldownRemaining`, the one hashed field this change touches. Also record the terminal tick, the outcome, both survivor counts, and the final per-agent state tuples. | create `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`; modify `tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj` | **none, and executed first** | TESTS | The fixture holds roughly 657 per-tick rows each carrying an event count, an event fold, and a state hash; its provenance header names the `main` commit and carries the harness source; and a smoke assertion reads it back and confirms the recorded terminal tick is 657 with outcome `Faction1Victory`. The csproj gains `<None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`, because that project currently holds no data files and the default SDK glob copies none. |
| [ ] | T11 | Add the client presentation and rendering types as no-op stubs so all three Phase 3 workstreams can write into one shared test assembly without blocking each other. | create `src/Hukbo.Client/Presentation/SwingAnimation.cs`, `src/Hukbo.Client/Presentation/SwingAnimationSystem.cs`, `src/Hukbo.Client/Presentation/ClashEffect.cs`, `src/Hukbo.Client/Presentation/ClashEffectSystem.cs`, `src/Hukbo.Client/Rendering/SwingGeometry.cs`, `src/Hukbo.Client/Rendering/SwingPoseResolver.cs`, `src/Hukbo.Client/Rendering/ClashEffectGeometry.cs` | T04 | CLIENT-ANIM | Warning-free Release build; all existing Client tests pass. |
| [ ] | T12 | Append `ClashBladeHard = 9`, `ClashBladeSoft = 10`, `ClashShield = 11` to `GameSoundId` **and** add all three to `SoundCatalog.AllSounds` with base names `clash-blade-hard`, `clash-blade-soft`, `clash-shield`, leaving `IsHitLocationDriven` false. The two must land together: `SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce` enumerates the enum, so appending members alone fails B0. They move to Phase 0 because the Phase 3b test task references them, and revision 3 had that task depended on by the very task that added them. | modify `src/Hukbo.Client/Audio/AudioTypes.cs`; modify `src/Hukbo.Client/Audio/SoundCatalog.cs`; modify `tests/Hukbo.Client.Tests/SoundCatalogTests.cs` | — | CLIENT-AUDIO | GUARD `AllSounds_ListsEveryDeclaredSlotExactlyOnce`, `AllSounds_ContainsTheThreeClashSlots`, `GetBaseName_NamesEveryClashSlot`, and `GetVariantPrefix` still throws for a clash slot. **Also add three `false` rows to `IsHitLocationDriven_IsTrueOnlyForTheFourWeaponSlots` at `SoundCatalogTests.cs:109-121`**: it is a nine-row `[Theory]` rather than an enumeration, so it silently would not cover the three new slots. No other fixture needs touching — every `AllSounds` consumer in production and in tests is written against `AllSounds.Count` and none hard-codes nine, which is exactly why the enum and the catalog entries land together. |

**Barrier B0.** `./scripts/format.ps1 -Verify`, a zero-warning Release build, and the
**full existing suite green**, plus the T08 hash check. Nothing in Phase 0 changes
behaviour, so a failing pre-existing test here is a Phase 0 defect, not a red test.

### Phase 1 — Core tests

One agent. These files sit in one assembly, so several agents cannot each run the suite
while any of them has an edit in flight.

**Every resolver test constructs an explicit literal `ClashProfile` in the test file using
the design section 3.3 values, and none reads `PhilippineCombatPreset`.** At B1 the
ruleset default is `ClashProfile.Neutral`, so a sweep reading the preset would have the
stub and the oracle agree on all 6,400 tuples and pass green while proving nothing; and a
vector pinned against the preset cannot have its expected values derived until T24
populates the tables, so they would end up pasted from output afterwards. Literal profiles
also decouple every resolver test from the T30 re-tune.

| | ID | Action | Files | Depends on | Workstream | Class and verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T13 | Pin the mixer at the repository standard set by `HitLocationResolverTests.cs:24-32`: at least eight `[InlineData]` rows covering seed 0 and the maximum unsigned seed, tick 0 and the maximum tick, all four weapons and both shields, pinning **both the roll and the resulting resolution**, with the independent derivation method in a comment. **At least one row per resolution value, all five** — void sits near 1000 of 10,000 and is the only interval bounded on both sides, so eight arbitrary tuples may never produce an `Evaded`. | create `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T06, T02 | TESTS | **RED** `MixClash_MatchesEveryPinnedVector`, at least eight rows, all five resolutions represented. |
| [ ] | T14 | Add seven single-word isolation cases, one per folded word other than the constant tag: seed, tick, source id, target id, attacker weapon, defender weapon, defender shield. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T13 | TESTS | **RED**, seven cases: `MixClash_ChangesWhenOnlyTheSeedChanges` and so on. |
| [ ] | T15 | Add the naive-reference sweep, run **twice**: once over an explicit literal of the shipped tables, and once over synthetic over-ceiling profiles, because the shipped tables top out at 4000 against a ceiling of 5500 and never enter the rescale branch. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T13, T09 | TESTS | **RED** `Resolve_MatchesTheNaiveReferenceAcrossTheWholeRosterMatrix` and `Resolve_MatchesTheNaiveReferenceOnOverCeilingProfiles`, each four attacker weapons by four defender weapons by two shields by ticks 1 to 200. |
| [ ] | T16 | Add the boundary cases, **all against synthetic profiles**, since neither clamp is reachable with shipped values: each of the four interval edges, the zero-width channel, a total of exactly 5500 against 5501, a total interception of zero, the hard-share clamp binding at 500 and at 6000, and the rescale invariants. The ceiling case is **split in two**, because the two halves have different classes. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T13 | TESTS | **RED** `Resolve_SelectsTheOutcomeAtEveryIntervalEdge`, `Clamp_LeavesATotalOfExactlyFiveThousandFiveHundredUnscaled`, `Clamp_PreservesChannelProportionsOnRescale`, `HardShare_BindsAtBothClampBounds`. **GUARD** `Resolve_NeverSelectsAZeroWidthChannel` (the stub selects no channel at all), `Resolve_AlwaysLandsAtZeroTotalInterception` (a stub that always lands satisfies it), and `Clamp_NeverExceedsTheCeiling` (satisfied by zeros). |
| [ ] | T17 | Add the distribution and split invariants. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T13 | TESTS | **RED** `Resolve_TallHardwoodBlocksMoreOftenThanItParries`, `SplitWeaponChannel_HardPlusSoftEqualsTheRescaledWeaponChannel`, `HardShare_SpansTheRosterRangeFromHeavyPairToLightPair`. **GUARD** `Resolve_NeverBlocksWithoutAShield`, which a stub that never blocks already satisfies and which exists to catch a future regression. Bands are asserted as bands and commented `PROVISIONAL`. |
| [ ] | T18 | Add the profile exact-bound cases, including that the interception ceiling accepts one and rejects zero. | modify `tests/Hukbo.Core.Tests/ClashProfileTests.cs` | T02 | TESTS | **GUARD** `Constructor_AcceptsTheExactBoundsAndRejectsOneStepOutside`, every field. T02 ships the real type, so these pass immediately and must keep passing. |
| [ ] | T19 | Add the configuration cases, including the thirty-two-row value pinning that follows `PhilippinePreset_UsesApprovedWeaponOverrides` at `CombatConfigurationTests.cs:62`, plus the row-mean check. Nothing else in the plan constrains a transcription error: the naive sweep compares two implementations reading the same profile, and a presence check only proves a value exists. | modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | T07 | TESTS | **RED** `Ruleset_DeclaresNonDefaultClashDataForEveryWeaponAndShield` — it asserts **non-zero** values, not mere presence, because `ClashProfile.Neutral` is a complete all-zero profile and a presence check would pass at B1. Also RED: `ContentHash_ChangesWhenAClashValueChanges`, `Preset_ReportsVersionTwo`, `Preset_UsesApprovedClashValues` with 32 rows, and `Preset_RowMeansMatchTheDesignedTotalInterceptionMatrix` asserting 2925, 2225, 3925, 3925. **GUARD** `ContentHash_IsIndependentOfClashDictionaryOrder`, vacuously true before the fold exists. |
| [ ] | T20 | Add the simulation cases, every one configuring its ruleset through the T08 seam. | modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`; modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | T04, T08 | TESTS | **RED** `ShieldedDefenderTakesLessDamageThanUnshieldedAtTheSameSeed`, `NonLandedAttack_EmitsAValueOfZeroAndNoDamageEvent`, `NonLandedAttack_StillResetsTheAttackerCooldown`, `MixedResolutionsOnOneTarget_AggregateOnlyTheLandedDamage` (two attackers, one landing and one not, exactly one damage event carrying exactly one blow of damage). **GUARD** `CrowdedTarget_ResolvesIdenticallyUnderEveryStorageOrder`, `MutualLethalAttacksStillProduceADrawWhenBothLand`, and `TargetDrivenToZeroByTheAggregateStillEmitsEveryContributingAttack`, the last being a preservation property that is already true on the current build. |
| [ ] | T21 | Add the control run against the T10 fixture, injected through the **`Create(Scenario, CombatRuleset)`** overload at seed 1 and 200 agents, with the ruleset built by `WithClashProfile(ClashProfile.Neutral)` so it is provably the preset except for the profile. Recompute each tick digest with the same tuple and the same **`Resolution` exclusion** T10 used, compare per tick so a failure names a first-divergence tick, and **compare the per-tick state hash on the same rows**, which is what catches a hashed field that emits no event, diverges mid-battle, and reconverges. Then compare the final per-agent state field by field. **Plus one decidable terminal assertion**: pass `0x59FB4CA563D87A49UL` as the content hash and assert `ComputeStateHash()` equals `D78F0B527B7F938F` exactly at the terminal tick. Passing the recorded `ulong` rather than deriving it from the injected ruleset is what makes this survive T26, which moves every content hash including the neutral one. | modify `tests/Hukbo.Core.Tests/DeterminismTests.cs` | T03, T07, T08, T10 | TESTS | **GUARD** `ZeroInterceptionProfile_ReproducesThePreClashDigest` and `ZeroInterceptionProfile_ReproducesTheRecordedStateHash`. Highest-value pair in the plan. GUARD rather than RED because both already pass against the stub and must keep passing through every later task; the moment either fails, something other than the four intended mechanisms has moved. |
| [ ] | T22 | Extend the same-seed determinism test with resolution assertions, make the event-hash difference a theory over **every** resolution pair including the null sentinel, and extend the twenty-seed guard with both termination clauses. | modify `tests/Hukbo.Core.Tests/DeterminismTests.cs`; modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`; modify `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | T05 | TESTS | **RED** `EventHash_DiffersForEveryDistinctResolutionPair`, a theory. **GUARD** `IndependentSameSeedRunsProduceIdenticalEventsAndStateHashes` with resolution equality added, and `SeedsOneThroughTwentyProduceVictoriesForBothFactions` at `BattleSimulationTests.cs:382` with both clauses added: at least nineteen of twenty decide before the cap, and the median decisive tick at or below 5,000. With the stub, seed 1 terminates at tick 657, so both clauses pass at B1; they are guards against the re-tune. **The advance loop at `:386-395` also gains a tick bound.** It is currently `while (Outcome == Ongoing) { AdvanceOneTick(); }` with no guard, unlike `CanonicalTwoHundredAgentBattleTerminatesWithinTheTickLimit` at `:416` which bounds its own. Criterion two exists to catch a stall, and an unbounded loop makes a stall **hang the suite rather than fail it**, which converts the one test that matters into a timeout with no diagnosis. |
| [ ] | T23 | Add the criterion-one band test at the **single enforced band**, the shielded-survivability inequality, and the two metrics patterns the repository already uses for collision metrics. | modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs`; modify `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | T05 | TESTS | **RED** `DefenceAttributableNonLandedShareStaysInsideTheAcceptanceBand`, which **asserts `AcceptedAttacks > 0` before it asserts the band**, failing outside 0.25 to 0.45 and nowhere else; the 0.30 to 0.40 design target is not a second gate. The accepted-attacks guard is what makes it honestly RED at B1 — it fails on "no attacks were counted", which is diagnostic — and what stops it passing vacuously if accumulation ever regresses. Also **RED** `ShieldedRosterEntriesSurviveMoreOftenThanShieldlessOnesAcrossSeedsOneThroughTwenty`, band commented `PROVISIONAL`. **GUARD** `Run_CombatMetricsSurviveAJsonRoundTrip` and `Run_SerializesByteIdenticalCombatMetricsForTwoSameSeedRuns`, mirroring `HeadlessRunnerTests.cs:259` and `:311`: with the T05 stub property present and defaulted, the first serialises an all-zero block and reads back the same zeros, and the second has two runs emit identical all-zero blocks, so both pass at B1. In revision 4 they were RED only by compile error, which was itself the defect. |

**Barrier B1.** `./scripts/format.ps1 -Verify`, then a full Release run in which **every
RED case fails on an assertion and every GUARD case passes**. A compile error anywhere in
either test assembly means Phase 0 was incomplete and Phase 2 may not start. A RED case
that passes, or a GUARD case that fails, is a defective test and blocks the barrier just
as hard as a build error.

### Phase 2 — Core implementation

One agent, strictly serial. T24 runs **before** any production edit to the attack stage.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T24 | Work the nine dispositions in design section 5, **using the T08 seam** to give each clash-affected test a zero-interception ruleset. Do not hand-pick seeds or entity identifiers whose roll happens to land: no shipped pairing is clash-neutral — the minimum is a `HeavyChopper` defending a `ThrustingBlade` at 2000 basis points — and a lucky-roll fixture is silently invalidated by T30 or by any mixer change, converting a preserved regression test into one that stops testing what its name claims. | modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`; modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | T21 | TESTS | All nine rows of design section 5 are accounted for: **six adjusted** through the seam, **two confirmed to survive as written** (`CanonicalTwoHundredAgentBattleTerminatesWithinTheTickLimit` and `Regression_AggregateDamagePerTargetPerTickEqualsSumOfIndividualAttackValuesAcrossAFullBattle`, the latter gaining the load-bearing comment about the zero value), and **one deferred to T32** (the two golden content-hash constants). No assertion is weakened; each adjusted test asserts the same property against a neutral ruleset. |
| [ ] | T25 | Implement `ClashResolver`: the `HKBO_CLS` tag `0x484B424F5F434C53`, the eight-word mixer, `ComputeChannels` performing the six-step computation with the split applied to the **post-rescale** weapon channel, and the fixed five-way interval walk with strictly lower-exclusive comparisons. | modify `src/Hukbo.Core/Combat/ClashResolver.cs` | T13 to T17 | CORE | Every `ClashResolverTests` case passes, including both naive sweeps and all seven boundary cases. |
| [ ] | T26 | Fold every clash value into `ComputeContentHash` in a sorted, order-independent way, and validate the profile against the ruleset roster. | modify `src/Hukbo.Core/Combat/CombatRuleset.cs` | T25 | CORE | `ContentHash_ChangesWhenAClashValueChanges` and `ContentHash_IsIndependentOfClashDictionaryOrder` pass. |
| [ ] | T27 | Populate the thirty-two values from design section 3.3 in the preset, each carrying a `PROVISIONAL` comment and the research statement that all sixteen matrix cells have zero evidentiary confidence, and bump `Version` from 1 to 2. | modify `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` | T26 | CORE | `Preset_UsesApprovedClashValues` passes all 32 rows and `Preset_RowMeansMatchTheDesignedTotalInterceptionMatrix` passes. These two land **before** T33 is permitted to re-tune anything. |
| [ ] | T28 | Resolve the clash inline in the existing gather loop immediately after the hit-location call, accumulating damage only for a landed attack. No new pass and no new buffer. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T27, T20, T24 | CORE | The five RED cases from T20 pass, both T20 guards still pass, and the existing `PackedFront_OpposingBodiesInContactStayInsideReachAndDealDamage` still passes. |
| [ ] | T29 | Carry the resolution into `AddAttackEvent` as a **required** internal parameter, emitting a value of zero for every non-landed attack. Required here, unlike on the public factory, so the `Landed` default can never mask a missing wire-up in production code. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T28 | CORE | `NonLandedAttack_EmitsAValueOfZeroAndNoDamageEvent` passes and `Regression_AggregateDamagePerTargetPerTickEqualsSumOfIndividualAttackValuesAcrossAFullBattle` still passes. |
| [ ] | T30 | Replace the T05 stub accumulation with the real thing in the gather loop. **Record the seed-1 state and event hash pair immediately before this task and assert it byte-identical immediately after**, which is the only place in the plan that proves the metrics reach neither hash. The repository treats derived counters as never hashed, never snapshotted, and never persisted, and the collision proximity band was accepted only on exactly this evidence; without it a metric leaking into `StateHasher` passes every other gate here, because the seam check predates the metrics, the control run does not speak to them, and the Phase 4 comparison is against a Phase 2 pair that already contains them. **Write both hash pairs into `docs/development/testing.md`, under the same heading T34 owns**, so the evidence is an artifact rather than a procedure an agent may quietly skip; nothing else in the plan would notice if it were. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs`; modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`; modify `docs/development/testing.md` | T28, T05 | CORE | `CombatMetrics_CountEveryAcceptedAttackExactlyOnce` asserts `AcceptedAttacks > 0` **before** asserting that accepted equals the sum of the five outcome counters, so it cannot read zero against zero if accumulation is absent. `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` reports the same two hashes before and after this task, byte for byte, and both pairs are written into the document. |
| [ ] | T31 | Fold the resolution into the headless event hash with the existing null sentinel, and thread the combat metrics into the run report. | modify `src/Hukbo.Headless/HeadlessRunner.cs`; modify `src/Hukbo.Headless/RunReport.cs` | T29, T30 | CORE | `EventHash_DiffersForEveryDistinctResolutionPair`, `Run_CombatMetricsSurviveAJsonRoundTrip`, and `Run_SerializesByteIdenticalCombatMetricsForTwoSameSeedRuns` all pass, and a headless run prints a populated combat-metrics object including the defence-attributable share. |
| [ ] | T32 | Re-baseline the two hard-coded golden content-hash constants. **May only run after T19 has gone green**, because editing a golden to match output before the two content-hash behaviour tests pass is the anti-pattern `hukbo-determinism-change` forbids. | modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs`; modify `tests/Hukbo.Core.Tests/DeterminismTests.cs` | T27, T19 | TESTS | Both occurrences of `0x59FB4CA563D87A49UL` are replaced by the one value the new preset computes, the two files agree, and **both** `ZeroInterceptionProfile_ReproducesThePreClashDigest` and `ZeroInterceptionProfile_ReproducesTheRecordedStateHash` still pass. This task is the one most likely to disturb the second of those, so it is named explicitly: the literal `0x59FB4CA563D87A49UL` that T21 passes as a content-hash argument is **not** one of the two goldens being re-baselined and must not be swept up in the edit. |
| [ ] | T33 | Run the two acceptance criteria and re-tune if either fails. **Criterion one** is whatever `DefenceAttributableNonLandedShareStaysInsideTheAcceptanceBand` enforces, 0.25 to 0.45, and nothing else; steer toward the 0.30 to 0.40 design target but do not gate on it. **Criterion two**: at least nineteen of twenty seeds decide before the cap and the median decisive tick is at or below 5,000. If criterion two fails while criterion one passes, examine the attack rate and the damage per landed blow **before** the clash tables, because interception is a multiplier on a stall rather than its cause. **If the two conflict, halt.** Record both measured figures and escalate to a human to amend the design band; do not widen a test band and do not weaken an assertion. | modify `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` only if a criterion fails | T28, T31 | CORE | Both named tests pass, or the run halts with both figures recorded and an escalation raised. |
| [ ] | T34 | Write the Phase 2 reference hash pair into the testing document under a heading naming it as superseded at T39, so that the byte-identity check at the far side of a three-workstream fan-out has a committed comparand rather than a transcript. | modify `docs/development/testing.md` | T33 | DOCS | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` reports `deterministic: true` with a null first mismatch tick, and both hashes appear in the document under "Phase 2 reference pair, superseded at T39". |

**Barrier B2.** `./scripts/format.ps1 -Verify`, the full suite green, both acceptance
criteria met, and the reference pair **committed by T34**.

### Phase 3a — CLIENT-ANIM

Owns `src/Hukbo.Client/Rendering/`, the swing and clash presentation systems,
`BloodEffectSystem`, `PresentationCoordinator`, and the render path.

Roughly half the cases below are satisfied by a no-op stub and a pose that never changes,
which is inherent to negative assertions. They are labelled the same way Phase 1 is.

| | ID | Action | Files | Depends on | Workstream | Class and verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T35 | Write the failing swing-system tests, including the missing-view case that follows the `BloodEffectSystem.ResolveDirection` precedent. | create `tests/Hukbo.Client.Tests/SwingAnimationSystemTests.cs` | T29, T11 | TESTS | **RED** `Ingest_CreatesOneSwingPerAttacker`, `Ingest_ReplacesAnInFlightSwingForTheSameAgent`, and `Advance_ExpiresASwingAfterItsTotalDuration` — the last is RED only because it asserts the swing is **present before** expiry and absent after; written as advance-then-assert-empty it would pass against a store that never stores. **GUARD** `Ingest_StaysBoundedUnderAFloodOfAttacks` and `Ingest_IgnoresAnAttackWhoseAttackerOrTargetIsNotInTheAgentViews`, both satisfied by a stub that stores nothing. |
| [ ] | T36 | Write the failing swing-geometry tests. `Landed` gets its own pose branch: a landed blow follows through and stops on the target, an evaded blow follows through past it, and the three contact outcomes recoil. Without that third branch the animation cannot separate `Evaded` from `Landed` at all. | create `tests/Hukbo.Client.Tests/SwingGeometryTests.cs` | T29, T11 | TESTS | **RED** `ResolvePhase_VisitsTheFourPhasesInOrder`, `ResolvePose_SwingsTowardTheTarget`, `ResolvePose_RecoilsOnAContactOutcome`, `ResolvePose_StopsOnTheTargetForALandedBlow`, `ResolvePose_FollowsThroughPastTheTargetForAVoid`. **GUARD** `ResolvePose_IsContinuousAcrossEveryPhaseBoundary`, since a neutral pose that never changes is continuous everywhere. |
| [ ] | T37 | Implement the swing record and system: fixed capacity, one slot per agent, newest wins, advancing on speed-scaled presentation seconds, direction derived from the two agent views. | modify `src/Hukbo.Client/Presentation/SwingAnimation.cs`; modify `src/Hukbo.Client/Presentation/SwingAnimationSystem.cs` | T35 | CLIENT-ANIM | All five `SwingAnimationSystemTests` cases pass. |
| [ ] | T38 | Implement `SwingGeometry` as a pure helper with the four phase shares as named `PROVISIONAL` constants and three pose branches. | modify `src/Hukbo.Client/Rendering/SwingGeometry.cs` | T36 | CLIENT-ANIM | All six `SwingGeometryTests` cases pass. |
| [ ] | T39 | Implement `SwingPoseResolver` as the pure mapping from the swing store and the agent views to a per-pawn pose, **including the lookup shape a draw loop uses to fetch one pose**, since that lookup is the part landing in the untestable render file. | modify `src/Hukbo.Client/Rendering/SwingPoseResolver.cs`; create `tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs` | T37, T38 | CLIENT-ANIM | `Resolve_ReturnsNoPoseForAnAgentWithNoActiveSwing`, `Resolve_ReturnsOnePosePerActiveSwing`, and `TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity` pass. |
| [ ] | T40 | Add the optional swing-pose parameter to `PawnGeometry.Create`, rotating the weapon line about the grip and offsetting the torso lean. | modify `src/Hukbo.Client/Rendering/PawnGeometry.cs`; modify `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | T38 | CLIENT-ANIM | New `Create_WithoutASwingPose_MatchesTheStaticLayout` passes and every existing `PawnGeometryTests` case passes **unmodified**, since no existing case constructs a `PawnLayout` directly. |
| [ ] | T41 | Add the swing arc trail **as a field on `PawnLayout`**, computed once from the pose with no position history, populated only at the medium and high detail tiers, and consumed by the renderer without recomputation. This shape is required by the plains-backdrop review finding recorded in `docs/development/testing.md`, where a duplicated formula left the shipped render loop uncovered. | modify `src/Hukbo.Client/Rendering/PawnGeometry.cs`; modify `src/Hukbo.Client/Rendering/PawnRenderer.cs`; modify `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | T40 | CLIENT-ANIM | `Create_ExposesTheSwingTrailOnTheLayoutRatherThanRequiringTheRendererToRecomputeIt` and `Create_OmitsTheSwingTrailAtTheLowDetailTier` pass, and `PawnRenderer` contains no trail formula of its own. |
| [ ] | T42 | Write the failing clash-effect tests, including the missing-view case and the zoom-scaling bound. | create `tests/Hukbo.Client.Tests/ClashEffectSystemTests.cs`; create `tests/Hukbo.Client.Tests/ClashEffectGeometryTests.cs` | T29, T11 | TESTS | **RED** `Ingest_PlacesTheEffectAtTheContactMidpoint`, `Ingest_EvictsOldestWhenFull`, `Create_ScalesTheCrossWithZoomAndStaysInsideItsBounds`. **GUARD** `Ingest_SkipsLandedAttacks`, `Ingest_SkipsAVoid`, `Ingest_IgnoresAnAttackWhoseAgentsAreMissingFromTheViews`. |
| [ ] | T43 | Implement the clash effect record, system, geometry, and renderer, copying the `HitEffect` family shape. The effect fires for the three contact outcomes only, never for a landed blow and never for a void. | modify `src/Hukbo.Client/Presentation/ClashEffect.cs`; modify `src/Hukbo.Client/Presentation/ClashEffectSystem.cs`; modify `src/Hukbo.Client/Rendering/ClashEffectGeometry.cs`; create `src/Hukbo.Client/Rendering/ClashEffectRenderer.cs` | T42 | CLIENT-ANIM | All six cases from T42 pass. |
| [ ] | T44 | Make `BloodEffectSystem` skip any attack event whose resolution is not `Landed`. | modify `src/Hukbo.Client/Presentation/BloodEffectSystem.cs`; modify `tests/Hukbo.Client.Tests/BloodEffectSystemTests.cs` | T29 | CLIENT-ANIM | **RED** `Ingest_ProducesNothingForANonLandedAttack`. It keys on `BattleEventKind.Attack` at `BloodEffectSystem.cs:112` rather than on damage, so without this every parried blow sprays blood. Highest-value client regression test. |
| [ ] | T45 | Give `PresentationCoordinator` the swing and clash systems, extend `AdvanceEffects` with a speed multiplier used **only** by the swing clock, and clear both on reset. | modify `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`; modify `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` | T37, T43 | CLIENT-ANIM | `AdvanceEffects_ScalesOnlyTheSwingClockByTheSpeedMultiplier` and `ResetFor_ClearsSwingsAndClashEffects` pass, and every existing case in that file passes. |
| [ ] | T46 | Wire the render path across **all four files**, which revision 3 reduced to one. Add the optional pose parameter to `PawnRenderer.Draw` at `:37`; pass the speed multiplier into `AdvanceEffects` and call `SwingPoseResolver` in `ArenaGame.cs`; fetch and hand each pose to `PawnRenderer` inside the actual per-pawn draw loop at `ArenaGame.Rendering.cs:264`; draw the clash layer. The third `Draw` call site, the inspector portrait at `AgentInspectorPanel.cs:109`, deliberately passes no pose and only compiles because the parameter is optional. **`PawnRenderer.GetBounds` at `:26` does not gain the pose**: it feeds the cull at `ArenaGame.Rendering.cs:254`, and a pose-aware cull would make the set of drawn pawns a function of presentation animation phase, so the same tick would render a different draw list depending on where each swing clock sat. That is draw-list determinism, and it is the whole reason. The accepted cost is stated accurately rather than waved away: `arenaBounds` is the scissored arena **panel** at `ArenaGame.Rendering.cs:64-72`, not the screen, so a pawn whose body sits outside the panel while its weapon would sweep into it is dropped entirely and the tip clips at the panel edge while panning. The selection padding does **not** absorb this: `PawnGeometry.cs:115-118` inflates by 3 to 8 pixels while a `GreatBlade` tip sweeps a lever of about 14 units, roughly 34 pixels at the maximum apparent scale. The artefact gets a smoke row rather than an assertion. | modify `src/Hukbo.Client/ArenaGame.cs`; modify `src/Hukbo.Client/ArenaGame.Rendering.cs`; modify `src/Hukbo.Client/Rendering/PawnRenderer.cs`; modify `src/Hukbo.Client/UI/AgentInspectorPanel.cs` | T45, T41, T39, T40 | CLIENT-ANIM | Warning-free Release build; `AgentInspectorPanelTests` and `AgentInspectorContentTests` pass with no edit; the pose lookup in the draw loop is the `TryGetPose` shape T39 pinned. Visual correctness is **`HUMAN`**, smoke rows added by T65. |

### Phase 3b — CLIENT-AUDIO

Owns the remaining `src/Hukbo.Client/Audio/` files plus the formatter, feed, and detail
panel. The enum and catalog entries already landed in Phase 0 as T12.

| | ID | Action | Files | Depends on | Workstream | Class and verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T47 | Write the failing audio tests, including the one that would have caught the dead channel: a clash cue must reach `Played`, not merely map to a slot. | modify `tests/Hukbo.Client.Tests/SoundCueMapperTests.cs`; modify `tests/Hukbo.Client.Tests/SoundDirectorTests.cs`; modify `tests/Hukbo.Client.Tests/SoundCueBudgetTests.cs` | T29, T12 | TESTS | **RED** `Map_ReturnsAClashSlotForEveryContactResolution`, `Map_ReturnsNoSlotForAVoid`, `Resolve_ReachesPlayedForAClashCueDespiteANonNullHitLocation`, `Ingest_ResolvesDeathAndOutcomeCuesBeforeAttackAndClashCues`, `Budget_ReservesCapacityForRareCuesAcrossAMultiTickFrame`. **GUARD** `Map_ReturnsTheWeaponSlotForALandedAttack`. |
| [ ] | T48 | Map an attack event to a clash slot by resolution, keeping the per-weapon mapping for a landed blow and returning no slot for a void, because silence is the void signal. | modify `src/Hukbo.Client/Audio/SoundCueMapper.cs` | T47 | CLIENT-AUDIO | The three mapper cases pass. |
| [ ] | T49 | **Force the hit class to null whenever `SoundCatalog.IsHitLocationDriven(sound)` is false.** Without this every clash cue resolves `Missing`: `SoundDirector.cs:72-75` derives a class from the still-non-null hit location, `MonoGameSoundPlayer.GetStatus` at `:78-81` keys on the pair, and `SoundLibrary` registers a classless slot only under a null class. | modify `src/Hukbo.Client/Audio/SoundDirector.cs` | T48 | CLIENT-AUDIO | `Resolve_ReachesPlayedForAClashCueDespiteANonNullHitLocation` passes. Without this task Part C ships permanently silent. |
| [ ] | T50 | Reserve per-frame budget capacity for the rare slots, death and the three outcome cues, that attack and clash cues cannot consume. This is the load-bearing half: `BeginFrame` runs once per frame at `ArenaGame.cs:177` while `Ingest` runs once per tick at `:534`, so at 2x and 4x several ticks share one eight-cue budget and a within-tick reordering alone does not stop starvation. | modify `src/Hukbo.Client/Audio/SoundCueBudget.cs` | T49 | CLIENT-AUDIO | `Budget_ReservesCapacityForRareCuesAcrossAMultiTickFrame` passes with several ticks ingested between two `BeginFrame` calls. |
| [ ] | T51 | Make `SoundDirector.Ingest` walk a tick event batch twice, rare cues first. The authoritative stream is untouched; only the order in which cues ask for the budget changes. | modify `src/Hukbo.Client/Audio/SoundDirector.cs` | T50 | CLIENT-AUDIO | `Ingest_ResolvesDeathAndOutcomeCuesBeforeAttackAndClashCues` passes and the existing budget and mute cases pass unchanged. |
| [ ] | T52 | Assert the consequence of T51 on the cue log: `Append` collapses consecutive rows sharing tick, sound, and status, so both the collapse behaviour and the panel order change, and the sound log will show deaths above attacks while the battle event log still shows attacks first. Name it, do not fix it. | modify `tests/Hukbo.Client.Tests/SoundCueLogTests.cs`; modify `tests/Hukbo.Client.Tests/SoundLogPanelTests.cs` | T51 | TESTS | **RED** `Append_CollapsesRowsUnderTheNewRareFirstOrdering` and `Panel_ListsRareCuesAboveAttackCuesWithinATick`, with a comment recording that the divergence from the battle event log is intentional. |
| [ ] | T53 | Assert that the loader discovers numbered variants for the three classless clash slots with no change to `SoundLibrary`. | modify `tests/Hukbo.Client.Tests/SoundLibraryTests.cs` | T12 | TESTS | **GUARD** `DiscoversNumberedVariantsForEveryClashSlotWithoutALoaderChange`, against a synthetic file-name list. |
| [ ] | T54 | Give the formatter a distinct action label per resolution, suppress or relabel the damage line in the detail panel at `BattleEventLogPanel.Details.cs:120` so a non-landed attack never reads a bare zero, and extend the feed defence-in-depth guard at `BattleEventFeed.cs:418-429` to check the resolution. Without the guard, a default event whose kind reads as `Attack` throws through the text filter once the formatter dereferences the new field. | modify `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`; modify `src/Hukbo.Client/Presentation/BattleEventFeed.cs`; modify `src/Hukbo.Client/UI/BattleEventLogPanel.Details.cs`; modify `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs`; modify `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs` | T29 | CLIENT-AUDIO | **RED** `GetActionLabel_ProducesADistinctLinePerResolution`, five distinct strings none reporting a zero damage figure, and `Details_OmitsTheDamageLineForANonLandedAttack`. **GUARD** `MatchesFilters_DoesNotThrowOnADefaultAttackEvent`. |

### Phase 3c — AUDIO-ASSETS

Owns `scripts/sfx.ps1` and `src/Hukbo.Client/Content/Audio/`. Touches no C# file.
Generation is not deterministic and never runs in a build, a test, or the gate. **This
group is gated on T12 in Phase 0, not on Phase 3b**, because the slot names it needs now
land at Phase 0.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T55 | Add default prompts for the three clash slots, each asking for one sound event on dry packed earth in open air, no reverb, no music, no voice, and none naming a cultural identification. | modify `scripts/sfx.ps1` | T12 | AUDIO-ASSETS | `./scripts/sfx.ps1 -List` prints twelve slots each with a default prompt, and `./scripts/sfx.ps1 -Slot clash-shield -DryRun` resolves and exits before the key lookup, so this is verifiable without a key. |
| [ ] | T56 | Generate `clash-blade-hard-01.wav` through `-05.wav`. The script rejects any take below ten per cent of full scale and writes the provenance row itself, so the script output **is** the format gate; re-roll on rejection. | create five files under `src/Hukbo.Client/Content/Audio/`; append `src/Hukbo.Client/Content/Audio/GENERATED.md` | T55 | AUDIO-ASSETS | Five `[PASS] Wrote` lines and five provenance rows. Audible quality is **`HUMAN`**, smoke rows added by T65. |
| [ ] | T57 | Generate `clash-blade-soft-01.wav` through `-05.wav`, shorter and brighter than the hard set with a faster decay. | create five files under `src/Hukbo.Client/Content/Audio/`; append `src/Hukbo.Client/Content/Audio/GENERATED.md` | T55 | AUDIO-ASSETS | Same. **`HUMAN`** for quality. |
| [ ] | T58 | Generate `clash-shield-01.wav` through `-05.wav`, a dull wooden board impact with no metallic ring. | create five files under `src/Hukbo.Client/Content/Audio/`; append `src/Hukbo.Client/Content/Audio/GENERATED.md` | T55 | AUDIO-ASSETS | Same. **`HUMAN`** for quality. |
| [ ] | T59 | Record the three new slots and their take counts in the audio folder inventory. | modify `src/Hukbo.Client/Content/Audio/README.md` | T56, T57, T58 | AUDIO-ASSETS | The README lists twelve slots and its stated file count matches the folder. **`HUMAN`**: a person compares a document against a directory, and no automated check performs it. |

### Phase 4 — gate and record

Strictly serial, one agent, after every Phase 3 workstream has landed.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T60 | Run the canonical gate and paste its exact output rather than summarising it. | none | T46, T54, T59 | DOCS | `./scripts/verify.ps1 -SkipBootstrap` ends with `[PASS] Canonical repository verification completed` and all five stages report `[PASS]`. Test counts recorded as printed. |
| [ ] | T61 | Run the 200-agent, 10,000-tick, seed-1 workload, capture the whole run report, and **assert its hashes are byte-identical to the pair T34 committed**. Any difference means Part B or Part C leaked into the simulation. | none | T60 | DOCS | `deterministic: true`, a null first mismatch tick, both hashes equal to the committed Phase 2 reference pair, the defence-attributable share inside the enforced 0.25 to 0.45 band, and no tick-limit draw. |
| [ ] | T62 | Run the report-only 500-agent stress workload. | none | T61 | DOCS | `./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1` completes and reports `deterministic: true`. Reported, never budgeted. |
| [ ] | T63 | Replace the recorded oracle with the new figures for both workloads, retire the Phase 2 reference-pair heading T34 added, and move **both** superseded pairs into the dead-value table with a one-line reason each. | modify `docs/development/testing.md` | T61, T62 | DOCS | The superseded table names the 200-agent pair `D78F0B527B7F938F` and `AC3BAAEC684854D5` **and** the 500-agent pair `C81B4F48DE54B983` and `D03F1213563DFD49`, and the live tables carry only figures produced by T61 and T62. |
| [ ] | T64 | Amend design sections 2.2 and 3.3 to the values that actually shipped, if T33 re-tuned anything, so the archived design does not describe a model the code does not implement. | modify `docs/plans/2026-07-27-weapon-clash-design.md` | T33, T63 | DOCS | Every basis-point value in design section 3.3 matches `PhilippineCombatPreset`, checked value by value against `Preset_UsesApprovedClashValues`. |
| [ ] | T65 | Add the interactive smoke rows, all `PENDING`. | modify `docs/development/testing.md` | T63 | DOCS | At least thirteen rows covering: a parried blow shows no damage and no blood; the event log distinguishes all five resolutions; the three clash cues are audibly distinct from a wet cut and from each other; **a void is distinguishable from a block**; **a void is distinguishable from a landed blow**, which is the row that decides whether the fifth outcome earns its place; weapons visibly swing rather than sitting static; a swing reads as one countable action at 1x and does not smear at 4x; **a clashed blow visibly recoils, a landed blow stops on the target, and a void follows through past it**; **the swing arc trail is visible at high zoom and absent at low**; **a weapon tip is or is not visibly clipped at the arena panel edge while panning**, which is the accepted cost of pose-blind culling; the clash cross appears only where two weapons meet; a death cue is still audible during the busiest fighting; and shielded warriors visibly outlive shieldless ones. Every row `PENDING`. **`HUMAN`.** The void-versus-landed row has a recorded disposition if it returns FAIL, in design section 3.8: `Evaded` keeps the event-log line and the absent impact ring, and the animation leg is struck from the channel table rather than the outcome being struck from the model. |
| [ ] | T66 | Update the determinism skill baseline and add the clash-bearing fields to its table of hashed fields that force a preset version. | modify `.claude/skills/hukbo-determinism-change/SKILL.md` | T63 | DOCS | The skill names the T61 hashes as the live baseline and lists both previous pairs as superseded, matching `docs/development/testing.md` exactly. |
| [ ] | T67 | Add the defensive resolution contract as standards section 14, following the shape of the section 13 collision contract, including a historical boundary paragraph stating that no value in it is a measurement and reproducing the morale-compensation finding from design section 2.3. | modify `SIMULATION-GAME-STANDARDS.md` | T63 | DOCS | Section 14 states the tick stage, the five outcomes with pinned values, the domain tag, the composition rule, the single enforced acceptance band, the termination criterion, the hashed fields, and the spectator channels. |
| [ ] | T68 | Move both plan documents to the archive with the "Archived: reference only" banner once the feature is integrated. | move `docs/plans/2026-07-27-weapon-clash-design.md` and `docs/plans/2026-07-27-weapon-clash.md` into `docs/archives/` | T64, T65, T66, T67 | DOCS | Both files carry the banner under the title and no longer appear in `docs/plans/`. |

## 3. Parallelisation map

| Group | Tasks | Agents | Notes |
| --- | --- | --- | --- |
| P0 | T10 **first**, then T01 to T09, T11, T12 | 1 | **T10 runs before everything**, including T01, because T04 adds a field to `BattleEvent` and changes what any serialiser emits, and a fixture captured afterwards would not be the pre-change stream however sincerely it was labelled. The rest is one agent: T05 and T08 both touch `BattleSimulation.cs`, and the whole phase is verified by one whole-solution build. |
| P1 | T13 to T23 | **1** | Revision 2 proposed three parallel agents on the grounds of disjoint files. That was unsound: disjoint *files* sit in one *assembly*, and no two agents can each run `./scripts/test.ps1` while either has an uncompilable edit in flight. One agent, or several writers and a single compile-and-run join step. |
| P2 | T24 to T34 | **1, serial by necessity** | T28, T29, and T30 all edit the same method in `BattleSimulation.cs`. T24 must precede T28. T32 must follow T19. T33 may loop back into T27. |
| P3a | T35, T36, T42 in parallel, then T37 to T41 and T43 to T46 | 1, optionally 2 | The swing family and the clash-effect family share no production file, so T42 to T44 can be a second agent with T45 as the join. The Phase 0 stubs are what make concurrent authorship into one test assembly safe. |
| P3b | T47, then T48 to T54 | 1 | `SoundCueMapper`, `SoundDirector`, and `SoundCueBudget` form one chain. T53 and T54 share no file with it and can run alongside. |
| P3c | T55, then T56 to T58 in parallel, then T59 | 3 during generation | Each generation task writes only its own five file names. The one shared append target, `GENERATED.md`, is guarded by the file lock added during the sound-variant work. |
| P4 | T60 to T68 | 1 | Strictly serial. The gate is one machine-level operation and the documents are written from its output. |

**All three Phase 3 groups depend only on Phase 0 and Phase 2, and therefore run at the
same time.** Revision 3 claimed this while gating P3c four tasks into the P3b serial
chain, which was false; moving the enum and catalog entries to T12 is what makes it true.
Their production file sets are disjoint: P3a owns `Rendering/`, the effect systems, and
the render path; P3b owns the remaining `Audio/` files plus the formatter, feed, and
detail panel; P3c owns the script and the content folder. They do write into one shared
`Hukbo.Client.Tests` assembly, which is exactly why T11 and T12 exist: without them, a
non-compiling `SwingAnimationSystemTests.cs` would stop P3b from ever observing its own
red, and a `SoundCueMapperTests.cs` referencing an unadded enum member would stop P3a.

Maximum useful concurrency: one agent in Phases 0, 1, 2, and 4, and five to seven across
Phase 3.

## 4. File ownership and serialisation points

Where two tasks touch one file the row says so, including inside a single workstream.
Revision 3 claimed no two tasks edited the same client test file, which was untrue of
`PawnGeometryTests.cs`; the claim is dropped and the overlaps are listed instead.

| File | Owning workstream | Tasks | Overlap |
| --- | --- | --- | --- |
| `src/Hukbo.Core/Combat/AttackResolution.cs` | CORE | T01 | none |
| `src/Hukbo.Core/Combat/ClashProfile.cs` | CORE | T02, T03 | Same agent, sequential |
| `src/Hukbo.Core/Combat/ClashResolver.cs` | CORE | T06, T25 | **Serialisation point** across a phase barrier |
| `src/Hukbo.Core/Combat/CombatRuleset.cs` | CORE | T07, T26 | **Serialisation point** across a phase barrier |
| `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` | CORE | T27, T33 | **Serialisation point.** T33 may re-tune what T27 wrote |
| `src/Hukbo.Core/Simulation/BattleEvent.cs` | CORE | T04 | none |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | CORE | T05, T08, T28, T29, T30 | **Largest serialisation point in the plan.** T05 adds the `LastTickCombat` stub and T08 the seam, both in Phase 0; T28 to T30 then edit one method. One agent each phase, in order |
| `src/Hukbo.Core/Determinism/StateHasher.cs` | CORE | T08 | none. A production signature change that revision 3 left unowned |
| `src/Hukbo.Core/Simulation/CombatMetrics.cs` | CORE | T05 | none |
| `src/Hukbo.Headless/RunReport.cs` | CORE | T05, T31 | **Serialisation point.** T05 adds the defaulted stub property in Phase 0, T31 populates it in Phase 2 |
| `src/Hukbo.Headless/HeadlessRunner.cs` | CORE | T31 | none |
| `src/Hukbo.Client/Presentation/SwingAnimation.cs`, `SwingAnimationSystem.cs` | CLIENT-ANIM | T11, T37 | Same workstream, sequential |
| `src/Hukbo.Client/Presentation/ClashEffect.cs`, `ClashEffectSystem.cs` | CLIENT-ANIM | T11, T43 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/SwingGeometry.cs` | CLIENT-ANIM | T11, T38 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/SwingPoseResolver.cs` | CLIENT-ANIM | T11, T39 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/ClashEffectGeometry.cs` | CLIENT-ANIM | T11, T43 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/ClashEffectRenderer.cs` | CLIENT-ANIM | T43 | none |
| `src/Hukbo.Client/Rendering/PawnGeometry.cs` | CLIENT-ANIM | T40, T41 | **Serialisation point.** Same agent, sequential |
| `src/Hukbo.Client/Rendering/PawnRenderer.cs` | CLIENT-ANIM | T41, T46 | **Serialisation point.** T41 consumes the trail field, T46 adds the optional pose parameter |
| `src/Hukbo.Client/Presentation/BloodEffectSystem.cs` | CLIENT-ANIM | T44 | none |
| `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` | CLIENT-ANIM | T45 | **Join point.** Needs T37 and T43 |
| `src/Hukbo.Client/ArenaGame.cs`, `ArenaGame.Rendering.cs`, `src/Hukbo.Client/UI/AgentInspectorPanel.cs` | CLIENT-ANIM | T46 | **Join point.** Needs T45, T39, T40. `ArenaGame.Rendering.cs` holds the real draw loop and was unowned in revision 3 |
| `src/Hukbo.Client/Audio/AudioTypes.cs`, `SoundCatalog.cs` | CLIENT-AUDIO | T12 | Both in Phase 0, one task, because the catalog completeness test enumerates the enum |
| `src/Hukbo.Client/Audio/SoundCueMapper.cs` | CLIENT-AUDIO | T48 | none |
| `src/Hukbo.Client/Audio/SoundDirector.cs` | CLIENT-AUDIO | T49, T51 | **Serialisation point.** Same agent, sequential |
| `src/Hukbo.Client/Audio/SoundCueBudget.cs` | CLIENT-AUDIO | T50 | none |
| `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`, `BattleEventFeed.cs`, `src/Hukbo.Client/UI/BattleEventLogPanel.Details.cs` | CLIENT-AUDIO | T54 | none |
| `scripts/sfx.ps1` | AUDIO-ASSETS | T55 | none |
| `src/Hukbo.Client/Content/Audio/*.wav` | AUDIO-ASSETS | T56, T57, T58 | Disjoint file names per task |
| `src/Hukbo.Client/Content/Audio/GENERATED.md` | AUDIO-ASSETS | T56, T57, T58 | **Shared append target**, made safe by the existing file lock. Not a serialisation point |
| `src/Hukbo.Client/Content/Audio/README.md` | AUDIO-ASSETS | T59 | none |
| `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | TESTS | T13 to T17 | Same agent, sequential |
| `tests/Hukbo.Core.Tests/ClashProfileTests.cs` | TESTS | T02, T03, T18 | **Serialisation point** across a phase barrier |
| `tests/Hukbo.Core.Tests/NaiveClashResolution.cs` | TESTS | T09 | none |
| `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json` | TESTS | T10 | none. Captured from unmodified `main` by a throwaway harness, as the **first executed task in the plan**, before T04 changes what any serialiser emits |
| `tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj` | TESTS | T10 | none. Gains the `Fixtures\**` copy item; the project holds no data files today, so the default glob copies nothing |
| `tests/Hukbo.Core.Tests/CombatMetricsTests.cs` | TESTS | T05 | none |
| `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | TESTS | T01, T07, T19, T32 | **Serialisation point**, four tasks across three phases. T07 adds the `WithClashProfile` copy assertion |
| `tests/Hukbo.Core.Tests/BattleEventTests.cs` | TESTS | T04 | none |
| `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | TESTS | T08, T20, T22, T24, T30 | **Serialisation point.** T08 adds the roster-rejection case; T22 extends `SeedsOneThroughTwentyProduceVictoriesForBothFactions` at line 382, which lives in this file and which revision 3 declared nowhere |
| `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | TESTS | T20, T23, T24 | **Serialisation point.** One agent, sequential |
| `tests/Hukbo.Core.Tests/DeterminismTests.cs` | TESTS | T08, T21, T22, T32 | **Serialisation point.** T08 fixes the second `StateHasher.Compute` call site at line 152, which revision 4 changed without declaring the file |
| `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | TESTS | T22, T23 | **Serialisation point.** Same agent, sequential |
| `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | TESTS | T40, T41 | **Serialisation point.** Same agent, sequential |
| `tests/Hukbo.Client.Tests/SoundCatalogTests.cs` | TESTS | T12 | none |
| Remaining `tests/Hukbo.Client.Tests/*` | TESTS | T35, T36, T39, T42, T44, T45, T47, T52, T53, T54 | No further overlaps. All sit in one assembly, which is why T11 and T12 are prerequisites for concurrent Phase 3 authorship |
| `docs/development/testing.md` | DOCS | T30, T34, T63, T65 | **Serialisation point.** T30 writes the metrics before-and-after pairs, T34 the Phase 2 reference pair, T63 retires both |
| `docs/plans/2026-07-27-weapon-clash-design.md` | DOCS | T64, T68 | Same agent, sequential |
| `SIMULATION-GAME-STANDARDS.md` | DOCS | T67 | none |
| `.claude/skills/hukbo-determinism-change/SKILL.md` | DOCS | T66 | none |

`docs/research/WEAPON_CLASH_1500s.md` is **not modified by any task**. It is evidence, not
a tuning surface, and no number from this plan may be written back into it, including and
especially the morale-compensation reasoning in design section 2.3.

## 5. Verification criteria for the change as a whole

- `./scripts/verify.ps1` passes at all five stages with zero warnings and zero errors.
- `./scripts/format.ps1 -Verify` passes at barriers B0, B1, and B2, not only at the end.
- At B0 the **entire pre-existing suite is green** and the seed-1 workload still reports
  `D78F0B527B7F938F` and `AC3BAAEC684854D5`, proving the T08 seam changed no value.
- At B1 **every RED case fails on an assertion and every GUARD case passes**.
- **Acceptance criterion one, one enforced band in one place.** The defence-attributable
  non-landed share fails outside **0.25 to 0.45**, enforced only by
  `DefenceAttributableNonLandedShareStaysInsideTheAcceptanceBand`. The 0.30 to 0.40 design
  target is what a re-tune steers toward and is deliberately not a second gate. The
  measured share is expected to sit above the static mean of 0.325, because shielded
  loadouts intercept more and outlive shieldless ones and so receive a rising share of
  attacks as the battle proceeds.
- **Acceptance criterion two.** At least nineteen of twenty seeds decide before the tick
  cap **and** the median decisive tick is at or below 5,000. Both clauses required.
- The zero-interception control run reproduces the **committed pre-change fixture** event
  for event. No state-hash clause is asserted, because differing only by one folded word
  is not a decidable property of a linear FNV-1a fold.
- The resolver matches the independent naive reference across the roster matrix **and**
  across synthetic over-ceiling profiles, the latter being the only path into the rescale
  branch.
- All thirty-two shipped tuning values are pinned by `[InlineData]` and the four row means
  are asserted, both landing before any re-tune is permitted.
- Both hashes moved, the movement is explained by the four mechanisms in design section
  3.6, and both new values are recorded in `docs/development/testing.md` and the skill.
- **The Phase 4 workload reproduces the committed Phase 2 reference pair byte for byte**,
  which is the only evidence Parts B and C stayed in presentation.
- Both superseded hash pairs, 200-agent and 500-agent, are named as dead values.
- No Client test constructs `ArenaGame`, a graphics device, a sprite batch, a window, or
  an audio device.
- Every clash tuning value in source carries a `PROVISIONAL` comment and the research
  statement that all sixteen weapon-intercept cells have zero evidentiary confidence.
- Every new interactive smoke row is `PENDING`.
- The seed-1 hash pair is byte-identical immediately before and immediately after the
  metrics task, proving the combat metrics reach neither hash. This is the same evidence
  the collision proximity band was accepted on.
- The twenty-seed advance loop is bounded, so a stall fails rather than hangs.

**Suite cost, stated because nobody had costed it.** The twenty-seed guard, the
shielded-survivability sweep, and the criterion-one band test add roughly forty-one full
200-agent battles to the ordinary Core suite, each about 1.48 times longer than the
current 657 ticks. That runs inside stage 4 of the canonical gate. The expectation is
seconds rather than minutes, but the figure is measured and recorded at T60 rather than
assumed, and if it materially lengthens the gate the right response is to move the sweeps
behind a trait rather than to weaken them.

Generated audio format is **not** a whole-change criterion. Phase 3c has a soft barrier
that explicitly permits a missing WAV, so a criterion asserting file properties could not
be re-checked at the end and could not become a test. The script is the gate: it rejects a
take below ten per cent of full scale, writes the RIFF header itself, and appends the
provenance row, so the per-task criterion in T56 to T58 is the enforceable form.

## 6. Things that must not happen

- No new `BattleEventKind` member.
- No facing, heading, morale, fatigue, skill, durability, or per-weapon reach field on
  `AgentState` or `Scenario`.
- No per-agent inspector field alongside `MovementResolution`; the event already carries
  the resolution, and a per-agent field would add a fifth value to the state hash for
  information the event already has.
- No re-application of the research section 5.4 modifiers on top of the round-two
  matrices; the crowding, awareness, and fatigue discounts are already inside them.
- No spear, no buckler, no new `WeaponId`, `ArmorId`, or `ShieldId` member.
- No `System.Random`, and no new `SplitMix64` stream in the tick loop.
- No floating-point value anywhere in the clash path.
- No hit stop, screen shake, or full-screen flash.
- No pose-aware frustum culling; the draw list must not depend on animation phase.
- No logic or loop added to `ArenaGame.cs`; the pose lookup used by the draw loop is the
  shape `SwingPoseResolverTests` pins.
- No resolver test that reads `PhilippineCombatPreset`; every one builds an explicit
  literal profile.
- No clash-neutral fixture built by hand-picking seeds or entity identifiers; the T08 seam
  is the only sanctioned mechanism.
- No golden constant edited to match output before the behaviour tests that constrain it
  have passed.
- No test assertion weakened to accommodate the new interception, and **no test band
  widened to accommodate a tuning failure**. If the two acceptance criteria conflict, T33
  halts and escalates to a human to amend the design band.
- No hosted CI workflow.
- No agent flipping a smoke row away from `PENDING`.
- No number, and no part of the morale-compensation reasoning, written back into
  `docs/research/WEAPON_CLASH_1500s.md` or `docs/research/HISTORICAL_1500s_WEAPONS.md`.

## 7. Result

Not yet run. Filled in by T60 through T62 with the exact gate output, the exact run report
for both workloads, the measured defence-attributable share against criterion one, the
twenty-seed termination figures against criterion two, the byte-identity result against
the Phase 2 reference pair, and an honest statement of which interactive rows remain
`PENDING`.
