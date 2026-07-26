# Weapon Clash, Swing Animation, and Clash Sound — Plan

Date: 2026-07-27
Revision: 3. Revision 2 answered two review gates; revision 3 rebuilds the tuning tasks on
research round two, which corrected a composition error in its own section 5.
Design: [2026-07-27-weapon-clash-design.md](2026-07-27-weapon-clash-design.md)
Research: [docs/research/WEAPON_CLASH_1500s.md](../research/WEAPON_CLASH_1500s.md)

## How to read this plan

Every task is a checkbox row carrying an identifier, a one-sentence action, the exact files
it creates or modifies, the tasks it depends on, a workstream label, and the specific named
test or gate output that proves it done. A criterion that says no more than "it works" is
not acceptable and none is written here. **Every file an agent touches appears in the Files
column of its own task**, including test files; a file named only in a verification column
is a planning defect.

The repository practises test-driven development. A red test must fail on an **assertion**,
never on a missing type: a test file referencing a type that does not exist yet fails the
whole test assembly to compile and takes every other test in that assembly with it. That is
recorded in `docs/development/testing.md` for the plains-backdrop re-run. Phase 0 therefore
creates the entire surface as neutral stubs before Phase 1 writes a single assertion.

Workstream labels are `CORE`, `CLIENT-ANIM`, `CLIENT-AUDIO`, `AUDIO-ASSETS`, `TESTS`, and
`DOCS`. Two tasks may run in parallel only when they own disjoint files **and** are not
mid-edit in the same test assembly. Ownership and serialisation points are in section 4.

Tasks that **cannot** be verified without a human at an interactive Windows desktop are
marked `HUMAN`. No agent may report one as passing, and no smoke row in
`docs/development/testing.md` may be flipped away from `PENDING` by anything but a person
performing the interaction.

## 1. Phases and barriers

| Phase | Contents | Barrier before the next phase |
| --- | --- | --- |
| Phase 0 — contract and stubs | Every new type, in both projects, present and neutral. The naive oracle. Nothing changes behaviour. | **Hard barrier B0.** `./scripts/format.ps1 -Verify`, a zero-warning Release build of the whole solution including both test projects, and the **entire existing suite still green**, since nothing has changed behaviour yet. |
| Phase 1 — Core tests, red | Every Core assertion, written against the stubs so it fails on the assertion. | **Hard barrier B1.** `./scripts/format.ps1 -Verify` plus a full Release test run showing the named cases failing on assertions, with no compile error anywhere. |
| Phase 2 — Core implementation, green | Resolver, preset tables, the attack stage, metrics, event-hash fold, existing-test dispositions, golden re-baseline, and the acceptance-criteria re-tune loop. | **Hard barrier B2.** Format verified, full suite green, both acceptance criteria met, and the seed-1 hash pair **recorded** as the reference the client phases must reproduce. |
| Phase 3 — client fan-out | Three workstreams: animation, audio wiring, audio content. | **Soft barrier.** Content may lag; a missing WAV is a silent slot, never a build failure. |
| Phase 4 — gate and record | Canonical gate, acceptance workloads, hash-neutrality proof for the client work, oracle re-record, design amendment, smoke rows, skill, standards. | Terminal. |

Phases 0, 1, 2, and 4 are each one agent. Phase 3 is where the parallelism is.

### What research round two removed from this plan

Round two states its matrices as mass-melee values with crowding, awareness, and fatigue
already applied, and directs that the section 5.4 modifiers must not be re-applied on top.
The crowding modifier is therefore deleted, and with it: the two-pass split of the attack
stage, the per-target attacker-count scratch buffer, the crowding word in the mixer key,
and the buffer staleness test. Revision 2 carried nine tasks that no longer exist. This is
a simplification driven by evidence, and with no cross-attack input the resolution is
trivially independent of attacker order.

Round two also promoted the void channel from rejected to required, because the acceptance
criterion is measured over shield plus weapon plus void. `Evaded` is a fifth resolution
value. It needs no sound slot and no visual effect, so it costs one enum member, one
interval, and one event-log line.

## 2. Ordered task list

### Phase 0 — contract and stubs

The whole solution, both test projects included, must compile at the end of this phase, and
the existing suite must still be green because nothing here changes behaviour.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T01 | Add the `AttackResolution` enum with pinned values `Landed = 0`, `ShieldBlocked = 1`, `Parried = 2`, `Deflected = 3`, `Evaded = 4`, documented as entering the event hash. | create `src/Hukbo.Core/Combat/AttackResolution.cs`; modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | — | CORE | New `AttackResolution_PinsItsNumericValues` passes and names all five. |
| [ ] | T02 | Add the immutable `ClashProfile` holding the sixteen-cell weapon matrix, the shield intercept, the four void values, the four hard-share bases, the four hard-share multipliers, the hard-share clamp bounds, and the interception ceiling, with per-field range validation and a `PROVISIONAL` comment recording that all sixteen matrix cells have zero evidentiary confidence. | create `src/Hukbo.Core/Combat/ClashProfile.cs`; create `tests/Hukbo.Core.Tests/ClashProfileTests.cs` | T01 | CORE | `ClashProfileTests.Constructor_RoundTripsEveryTable` passes. |
| [ ] | T03 | Add `ClashProfile.Neutral`, an all-zero-interception profile, and make it the default. | modify `src/Hukbo.Core/Combat/ClashProfile.cs` | T02 | CORE | `ClashProfileTests.Neutral_ReportsZeroInterceptionForEveryRosterPair` passes. This value is what the control run and half the existing-test dispositions are built on. |
| [ ] | T04 | Add `BattleEvent.Resolution` as a nullable property, **optional on the `Attack` factory defaulting to `Landed`**, forced null on `NonAttack`. The parameter is optional because `BattleEvent.Attack` has twenty call sites across eleven files, nine of them test files owned by other workstreams, and a required parameter makes barrier B0 unsatisfiable. | modify `src/Hukbo.Core/Simulation/BattleEvent.cs`; modify `tests/Hukbo.Core.Tests/BattleEventTests.cs` | T01 | CORE | `BattleEventTests.NonAttack_LeavesTheResolutionNull` and `Attack_RejectsAnUndefinedResolution` pass, and the solution builds without editing any of the other ten files. |
| [ ] | T05 | Add `CombatMetrics` and its accumulator with counters for accepted attacks and each of the five outcomes, plus a derived defence-attributable non-landed ratio, copying the shape of `CollisionMetrics`. | create `src/Hukbo.Core/Simulation/CombatMetrics.cs`; create `tests/Hukbo.Core.Tests/CombatMetricsTests.cs` | T01 | CORE | `CombatMetricsTests.Accumulator_RejectsNegativeCountsAndResetsToZero` passes, mirroring `CollisionMetricsTests`. |
| [ ] | T06 | Add `ClashResolver` as a neutral stub: `MixClash` returns zero, `Resolve` returns `Landed`, `SplitWeaponChannel` returns a zero pair. | create `src/Hukbo.Core/Combat/ClashResolver.cs` | T02 | CORE | The type is referenceable from `Hukbo.Core.Tests` through the existing `InternalsVisibleTo`, and the existing suite stays green. |
| [ ] | T07 | Give `CombatRuleset` an **optional** `ClashProfile` constructor parameter defaulting to `ClashProfile.Neutral`, with accessors returning its values. Optional so that the named-argument constructions at `CombatConfigurationTests.cs:268` and `:324` keep compiling untouched. | modify `src/Hukbo.Core/Combat/CombatRuleset.cs` | T03, T06 | CORE | `./scripts/build.ps1 -Configuration Release` is warning-free and `CombatConfigurationTests` compiles with no edit to either construction site. |
| [ ] | T08 | Write the independent naive oracle: a six-step reimplementation in `long` that **calls no production helper**, following the conventions of `NaiveCollisionPairs.cs`. | create `tests/Hukbo.Core.Tests/NaiveClashResolution.cs` | T02 | TESTS | The file compiles and a search for `ClashResolver` inside it returns nothing. |
| [ ] | T09 | Add the client presentation and rendering types as no-op stubs so the three Phase 3 workstreams can write into one shared test assembly without blocking each other. | create `src/Hukbo.Client/Presentation/SwingAnimation.cs`, `src/Hukbo.Client/Presentation/SwingAnimationSystem.cs`, `src/Hukbo.Client/Presentation/ClashEffect.cs`, `src/Hukbo.Client/Presentation/ClashEffectSystem.cs`, `src/Hukbo.Client/Rendering/SwingGeometry.cs`, `src/Hukbo.Client/Rendering/SwingPoseResolver.cs`, `src/Hukbo.Client/Rendering/ClashEffectGeometry.cs` | T04 | CLIENT-ANIM | `./scripts/build.ps1 -Configuration Release` is warning-free and all existing Client tests still pass. |

**Barrier B0.** `./scripts/format.ps1 -Verify`, then a zero-warning Release build, then the
**full existing suite green**. Nothing in Phase 0 changes behaviour, so a failing
pre-existing test here is a defect in Phase 0, not a red test.

### Phase 1 — Core tests, red

One agent. The test files touch one assembly, so several agents cannot each run the suite
while any of them has an edit in flight.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T10 | Pin the mixer at the existing repository standard: at least eight `[InlineData]` rows covering seed 0 and the maximum unsigned seed, tick 0 and the maximum tick, all four weapons and both shields, pinning **both the roll and the resulting resolution**, with the independent derivation method stated in a comment, exactly as `HitLocationResolverTests.cs:24-32` does. | create `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T06 | TESTS | `MixClash_MatchesEveryPinnedVector` carries at least eight rows and fails on an assertion, not a compile error. |
| [ ] | T11 | Add seven single-word isolation cases, one per folded word other than the constant tag: seed, tick, source id, target id, attacker weapon, defender weapon, defender shield. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T10 | TESTS | Seven cases named `MixClash_ChangesWhenOnlyTheSeedChanges` and so on, one per word. |
| [ ] | T12 | Add the naive-reference sweep across the whole roster matrix and tick range. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T10, T08 | TESTS | `Resolve_MatchesTheNaiveReferenceAcrossTheWholeRosterMatrix` sweeps four attacker weapons by four defender weapons by two shields by ticks 1 to 200. |
| [ ] | T13 | Add the boundary cases: every interval edge, the zero-width channel, a total of exactly 5500 against 5501, a total interception of zero, and the hard-share clamp binding at both 500 and 6000. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T10 | TESTS | `Resolve_SelectsTheOutcomeAtEveryIntervalEdge`, `Resolve_NeverSelectsAZeroWidthChannel`, `Clamp_LeavesATotalOfExactlyFiveThousandFiveHundredUnscaled`, `Clamp_RescalesEveryChannelProportionallyAtOneAbove`, `Resolve_AlwaysLandsAtZeroTotalInterception`, `HardShare_BindsAtBothClampBounds`. |
| [ ] | T14 | Add the distribution and split invariants: no shield never blocks; a tall hardwood shield blocks more often than it parries; hard plus soft sums exactly to the weapon channel; the roster hard-share spread runs from about 0.46 down to about 0.08. | modify `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | T10 | TESTS | `Resolve_NeverBlocksWithoutAShield`, `Resolve_TallHardwoodBlocksMoreOftenThanItParries`, `SplitWeaponChannel_HardPlusSoftEqualsTheWeaponChannel`, `HardShare_SpansTheRosterRangeFromHeavyPairToLightPair`. Bands are asserted as bands and commented `PROVISIONAL`. |
| [ ] | T15 | Add the profile exact-bound cases, including that the interception ceiling accepts one and rejects zero. | modify `tests/Hukbo.Core.Tests/ClashProfileTests.cs` | T02 | TESTS | `Constructor_AcceptsTheExactBoundsAndRejectsOneStepOutside` covers every field. |
| [ ] | T16 | Add the configuration cases: every weapon and shield declares clash data; the content hash responds to a clash-value change; the content hash is independent of dictionary supply order; the preset version is 2. | modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | T07 | TESTS | `Ruleset_DeclaresClashDataForEveryWeaponAndShield`, `ContentHash_ChangesWhenAClashValueChanges`, `ContentHash_IsIndependentOfClashDictionaryOrder`, `Preset_ReportsVersionTwo`. |
| [ ] | T17 | Add the simulation cases: shielded defenders take strictly less damage than shieldless at the same seed; a non-landed attack emits an attack event with value zero, no damage event, and no hit-point change; a non-landed attack still resets the cooldown; a permutation over three storage orders of the same identifiers resolves identically; mutual lethal attacks still draw when both land; an attack against a target killed by another attacker in the same tick still resolves and emits. | modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`; modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | T04, T07 | TESTS | `ShieldedDefenderTakesLessDamageThanUnshieldedAtTheSameSeed`, `NonLandedAttack_EmitsAValueOfZeroAndNoDamageEvent`, `NonLandedAttack_StillResetsTheAttackerCooldown`, `CrowdedTarget_ResolvesIdenticallyUnderEveryStorageOrder`, `MutualLethalAttacksStillProduceADrawWhenBothLand`, `AttackAgainstATargetKilledByAnotherAttackerInTheSameTickStillResolvesAndEmits`. |
| [ ] | T18 | Add the control run: a zero-interception profile must reproduce the pre-change ordered event stream exactly for seed 1, with identical kind, sequence, source, target, and value on every event, every resolution reading `Landed`, and the state hash differing only by the content-hash fold. | modify `tests/Hukbo.Core.Tests/DeterminismTests.cs` | T03, T04 | TESTS | `ZeroInterceptionProfile_ReproducesThePreClashOrderedEventStream` passes. **Highest-value test in the plan**: it separates the four intended hash-movement mechanisms from an unintended fifth. |
| [ ] | T19 | Extend the existing same-seed determinism test with resolution assertions, and extend the twenty-seed guard with both termination clauses: at least nineteen of twenty decide before the cap, and the median decisive tick is at or below 5,000. | modify `tests/Hukbo.Core.Tests/DeterminismTests.cs`; modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | T05 | TESTS | `IndependentSameSeedRunsProduceIdenticalEventsAndStateHashes` additionally asserts resolution equality, and `SeedsOneThroughTwentyProduceVictoriesForBothFactions` additionally asserts both clauses. |
| [ ] | T20 | Add the interception-share criterion as a band over the 200-agent run, and the shielded-survivability inequality across seeds one to twenty. | modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | T05 | TESTS | `DefenceAttributableNonLandedShareStaysInsideTheAcceptanceBand` fails outside 0.25 to 0.45, and `ShieldedRosterEntriesSurviveMoreOftenThanShieldlessOnesAcrossSeedsOneThroughTwenty` asserts an inequality band commented `PROVISIONAL`. |

**Barrier B1.** `./scripts/format.ps1 -Verify`, then a full Release test run in which every
named case above fails **on an assertion**. A compile error anywhere in either test assembly
means Phase 0 was incomplete and Phase 2 may not start.

### Phase 2 — Core implementation, green

One agent, strictly serial. T21 runs **before** any production edit to the attack stage.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T21 | Work the nine dispositions in design section 5: give each clash-affected existing test a zero-interception profile, confirm the two that survive as written, and add the load-bearing comment recording that the aggregate-damage regression survives only because a non-landed attack carries a value of zero. | modify `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`; modify `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | T18 | TESTS | All nine named tests in design section 5 are accounted for: seven adjusted or confirmed, two deferred to T29. No assertion is weakened; each adjusted test asserts the same property against a clash-neutral tuple. |
| [ ] | T22 | Implement `ClashResolver`: the `HKBO_CLS` tag `0x484B424F5F434C53`, the eight-word mixer, the six-step computation, the proportional rescale, and the fixed five-way interval walk with strict lower-exclusive comparisons. | modify `src/Hukbo.Core/Combat/ClashResolver.cs` | T10 to T14 | CORE | Every `ClashResolverTests` case passes, including the naive-reference sweep and all six boundary cases. |
| [ ] | T23 | Fold every clash value into `ComputeContentHash` in a sorted, order-independent way, and validate the profile against the ruleset roster. | modify `src/Hukbo.Core/Combat/CombatRuleset.cs` | T22 | CORE | `ContentHash_ChangesWhenAClashValueChanges` and `ContentHash_IsIndependentOfClashDictionaryOrder` pass. |
| [ ] | T24 | Populate the thirty-two values from design section 3.3 in the preset, each carrying a `PROVISIONAL` comment and the research statement that all sixteen matrix cells have zero evidentiary confidence, and bump `Version` from 1 to 2. | modify `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` | T23 | CORE | `Preset_ReportsVersionTwo` and `Ruleset_DeclaresClashDataForEveryWeaponAndShield` pass, and the computed row means reproduce design section 3.3: 2925, 2225, 3925, 3925. |
| [ ] | T25 | Resolve the clash inline in the existing gather loop immediately after the hit-location call, accumulating damage only for a landed attack. No new pass and no new buffer. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T24, T17, T21 | CORE | The six T17 cases pass and the existing `PackedFront_OpposingBodiesInContactStayInsideReachAndDealDamage` still passes. |
| [ ] | T26 | Carry the resolution into `AddAttackEvent` as a **required** internal parameter, emitting a value of zero for every non-landed attack. Required here, unlike on the public factory, so the `Landed` default can never mask a missing wire-up in production code. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T25 | CORE | `NonLandedAttack_EmitsAValueOfZeroAndNoDamageEvent` passes, and `Regression_AggregateDamagePerTargetPerTickEqualsSumOfIndividualAttackValues` still passes. |
| [ ] | T27 | Accumulate the combat metrics in the gather loop and expose them in the style of `LastTickCollision`. | modify `src/Hukbo.Core/Simulation/BattleSimulation.cs` | T25, T05 | CORE | New `CombatMetrics_CountEveryAcceptedAttackExactlyOnce` asserts accepted equals the sum of the five outcome counters. |
| [ ] | T28 | Fold the resolution into the headless event hash with the existing null sentinel, and thread the combat metrics into the run report. | modify `src/Hukbo.Headless/HeadlessRunner.cs`; modify `src/Hukbo.Headless/RunReport.cs`; modify `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | T26, T27 | CORE | `EventHash_DiffersWhenOnlyTheResolutionDiffers` passes and a headless run prints a populated combat-metrics object including the defence-attributable share. |
| [ ] | T29 | Re-baseline the two hard-coded golden content-hash constants. **May only run after T16 has gone green**, because editing a golden to match output before the two content-hash behaviour tests pass is the anti-pattern `hukbo-determinism-change` forbids. | modify `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs`; modify `tests/Hukbo.Core.Tests/DeterminismTests.cs` | T24, T16 | TESTS | Both occurrences of `0x59FB4CA563D87A49UL` are replaced by the one value the new preset computes, the two files agree, and `T18` still passes. |
| [ ] | T30 | Run the two acceptance criteria and re-tune if either fails. **Criterion one**: the defence-attributable non-landed share over the 200-agent battle must sit in 0.30 to 0.40 and fails outside 0.25 to 0.45. **Criterion two**: across at least twenty seeds at 200 agents, at least 95 per cent must decide before the tick cap **and** the median decisive tick must be at or below 5,000. If criterion two fails while criterion one passes, examine the attack rate and the damage per landed blow **before** the clash tables, because interception is a multiplier on a stall rather than its cause. Any re-tune is labelled as compensation for the absent morale model, per design section 2.3. | modify `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` only if a criterion fails | T25, T28 | CORE | `DefenceAttributableNonLandedShareStaysInsideTheAcceptanceBand` and the extended `SeedsOneThroughTwentyProduceVictoriesForBothFactions` both pass. |

**Barrier B2.** `./scripts/format.ps1 -Verify`, the full suite green, both acceptance
criteria met, and `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` reporting
`deterministic: true` with a null first mismatch tick. **Record the state and event hash
pair here.** Phase 4 must reproduce it byte for byte, and that is the only evidence the
client work stayed in presentation.

### Phase 3a — CLIENT-ANIM

Owns `src/Hukbo.Client/Rendering/`, the swing and clash presentation systems,
`BloodEffectSystem`, `PresentationCoordinator`, and `ArenaGame`.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T31 | Write the failing swing-system tests, including the missing-view case that follows the `BloodEffectSystem.ResolveDirection` precedent. | create `tests/Hukbo.Client.Tests/SwingAnimationSystemTests.cs` | T26, T09 | TESTS | `Ingest_CreatesOneSwingPerAttacker`, `Advance_ExpiresASwingAfterItsTotalDuration`, `Ingest_ReplacesAnInFlightSwingForTheSameAgent`, `Ingest_StaysBoundedUnderAFloodOfAttacks`, `Ingest_IgnoresAnAttackWhoseAttackerOrTargetIsNotInTheAgentViews`. All fail on assertions against the T09 stubs. |
| [ ] | T32 | Write the failing swing-geometry tests, including phase order, continuity at every boundary, direction, recoil on a contact outcome, and swing-through on a void. | create `tests/Hukbo.Client.Tests/SwingGeometryTests.cs` | T26, T09 | TESTS | `ResolvePhase_VisitsTheFourPhasesInOrder`, `ResolvePose_IsContinuousAcrossEveryPhaseBoundary`, `ResolvePose_SwingsTowardTheTarget`, `ResolvePose_RecoilsOnAContactOutcome`, `ResolvePose_FollowsThroughOnAVoid`. |
| [ ] | T33 | Implement the swing record and system: fixed capacity, one slot per agent, newest wins, advancing on speed-scaled presentation seconds, direction derived from the two agent views. | modify `src/Hukbo.Client/Presentation/SwingAnimation.cs`; modify `src/Hukbo.Client/Presentation/SwingAnimationSystem.cs` | T31 | CLIENT-ANIM | All five `SwingAnimationSystemTests` cases pass. |
| [ ] | T34 | Implement `SwingGeometry` as a pure helper with the four phase shares as named `PROVISIONAL` constants, a recoil branch for the three contact outcomes, and a follow-through branch for a void. | modify `src/Hukbo.Client/Rendering/SwingGeometry.cs` | T32 | CLIENT-ANIM | All five `SwingGeometryTests` cases pass. |
| [ ] | T35 | Implement `SwingPoseResolver` as the pure mapping from the swing store and the agent views to a per-pawn pose, so that no loop lives in the untestable `ArenaGame`. | modify `src/Hukbo.Client/Rendering/SwingPoseResolver.cs`; create `tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs` | T33, T34 | CLIENT-ANIM | `Resolve_ReturnsNoPoseForAnAgentWithNoActiveSwing` and `Resolve_ReturnsOnePosePerActiveSwing` pass. |
| [ ] | T36 | Add the optional swing-pose parameter to `PawnGeometry.Create`, rotating the weapon line about the grip and offsetting the torso lean. | modify `src/Hukbo.Client/Rendering/PawnGeometry.cs`; modify `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | T34 | CLIENT-ANIM | New `Create_WithoutASwingPose_MatchesTheStaticLayout` passes and every existing `PawnGeometryTests` case passes **unmodified**, since no existing case constructs a `PawnLayout` directly. |
| [ ] | T37 | Add the swing arc trail **as a field on `PawnLayout`**, computed once from the pose with no position history, populated only at the medium and high detail tiers, and consumed by the renderer without recomputation. This shape is required by the plains-backdrop review finding recorded in `docs/development/testing.md`, where a duplicated formula left the shipped render loop uncovered. | modify `src/Hukbo.Client/Rendering/PawnGeometry.cs`; modify `src/Hukbo.Client/Rendering/PawnRenderer.cs`; modify `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | T36 | CLIENT-ANIM | `Create_ExposesTheSwingTrailOnTheLayoutRatherThanRequiringTheRendererToRecomputeIt` and `Create_OmitsTheSwingTrailAtTheLowDetailTier` pass, and `PawnRenderer` contains no trail formula of its own. |
| [ ] | T38 | Write the failing clash-effect tests, including the missing-view case and the zoom-scaling bound. | create `tests/Hukbo.Client.Tests/ClashEffectSystemTests.cs`; create `tests/Hukbo.Client.Tests/ClashEffectGeometryTests.cs` | T26, T09 | TESTS | `Ingest_SkipsLandedAttacks`, `Ingest_SkipsAVoid`, `Ingest_PlacesTheEffectAtTheContactMidpoint`, `Ingest_EvictsOldestWhenFull`, `Ingest_IgnoresAnAttackWhoseAgentsAreMissingFromTheViews`, `Create_ScalesTheCrossWithZoomAndStaysInsideItsBounds`. |
| [ ] | T39 | Implement the clash effect record, system, geometry, and renderer, copying the `HitEffect` family shape. The effect fires for the three contact outcomes only, never for a landed blow and never for a void. | modify `src/Hukbo.Client/Presentation/ClashEffect.cs`; modify `src/Hukbo.Client/Presentation/ClashEffectSystem.cs`; modify `src/Hukbo.Client/Rendering/ClashEffectGeometry.cs`; create `src/Hukbo.Client/Rendering/ClashEffectRenderer.cs` | T38 | CLIENT-ANIM | All six cases from T38 pass. |
| [ ] | T40 | Make `BloodEffectSystem` skip any attack event whose resolution is not `Landed`. | modify `src/Hukbo.Client/Presentation/BloodEffectSystem.cs`; modify `tests/Hukbo.Client.Tests/BloodEffectSystemTests.cs` | T26 | CLIENT-ANIM | `Ingest_ProducesNothingForANonLandedAttack` passes. It keys on `BattleEventKind.Attack` at `BloodEffectSystem.cs:112` rather than on damage, so without this every parried blow sprays blood. Highest-value client regression test. |
| [ ] | T41 | Give `PresentationCoordinator` the swing and clash systems, extend `AdvanceEffects` with a speed multiplier used **only** by the swing clock, and clear both on reset. | modify `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`; modify `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` | T33, T39 | CLIENT-ANIM | `AdvanceEffects_ScalesOnlyTheSwingClockByTheSpeedMultiplier` and `ResetFor_ClearsSwingsAndClashEffects` pass, and every existing case in that file passes. |
| [ ] | T42 | Wire the client: pass the speed multiplier into `AdvanceEffects`, call `SwingPoseResolver` once per frame, hand each pose to `PawnRenderer`, and draw the clash layer. Wiring only, no logic. | modify `src/Hukbo.Client/ArenaGame.cs` | T41, T35, T36 | CLIENT-ANIM | `./scripts/build.ps1 -Configuration Release` is warning-free, and no loop or formula is added to `ArenaGame`. Visual correctness is **`HUMAN`**, smoke rows added by T57. |

### Phase 3b — CLIENT-AUDIO

Owns `src/Hukbo.Client/Audio/`, `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`,
and the feed and log files the reordering touches. The formatter sits here because it and
the cue mapper are the two places that turn a resolution into something a spectator
perceives.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T43 | Write the failing audio tests, including the one that would have caught the dead channel: a clash cue must reach `Played`, not merely map to a slot. | modify `tests/Hukbo.Client.Tests/SoundCueMapperTests.cs`; modify `tests/Hukbo.Client.Tests/SoundCatalogTests.cs`; modify `tests/Hukbo.Client.Tests/SoundDirectorTests.cs`; modify `tests/Hukbo.Client.Tests/SoundCueBudgetTests.cs` | T26 | TESTS | `Map_ReturnsAClashSlotForEveryContactResolution`, `Map_ReturnsNoSlotForAVoid`, `Map_ReturnsTheWeaponSlotForALandedAttack`, `AllSounds_ContainsTheThreeClashSlots`, `GetBaseName_NamesEveryClashSlot`, `Resolve_ReachesPlayedForAClashCueDespiteANonNullHitLocation`, `Ingest_ResolvesDeathAndOutcomeCuesBeforeAttackAndClashCues`, `Budget_ReservesCapacityForRareCuesAcrossAMultiTickFrame`. |
| [ ] | T44 | Append `ClashBladeHard = 9`, `ClashBladeSoft = 10`, `ClashShield = 11` to `GameSoundId` without renumbering anything. | modify `src/Hukbo.Client/Audio/AudioTypes.cs` | T43 | CLIENT-AUDIO | The existing `AllSounds` completeness case still passes with three new members. |
| [ ] | T45 | Add the three slots to `AllSounds` with base names `clash-blade-hard`, `clash-blade-soft`, and `clash-shield`, leaving `IsHitLocationDriven` false for all three. | modify `src/Hukbo.Client/Audio/SoundCatalog.cs` | T44 | CLIENT-AUDIO | `AllSounds_ContainsTheThreeClashSlots` and `GetBaseName_NamesEveryClashSlot` pass, and `GetVariantPrefix` still throws for a clash slot. |
| [ ] | T46 | Map an attack event to a clash slot by resolution, keeping the per-weapon mapping for a landed blow and returning no slot for a void, because silence is the void signal. | modify `src/Hukbo.Client/Audio/SoundCueMapper.cs` | T45 | CLIENT-AUDIO | The three mapper cases from T43 pass. |
| [ ] | T47 | **Force the hit class to null whenever `SoundCatalog.IsHitLocationDriven(sound)` is false.** Without this every clash cue resolves `Missing`, because `SoundDirector.cs:72-75` derives a class from the still-non-null hit location while `MonoGameSoundPlayer.GetStatus` at `:78-81` keys on the pair and `SoundLibrary` registers a classless slot only under a null class. | modify `src/Hukbo.Client/Audio/SoundDirector.cs` | T46 | CLIENT-AUDIO | `Resolve_ReachesPlayedForAClashCueDespiteANonNullHitLocation` passes. Without this task Part C ships permanently silent. |
| [ ] | T48 | Reserve per-frame budget capacity for the rare slots, death and the three outcome cues, that attack and clash cues cannot consume. This is the load-bearing half: `BeginFrame` runs once per frame at `ArenaGame.cs:177` while `Ingest` runs once per tick at `:534`, so at 2x and 4x several ticks share one eight-cue budget and a within-tick reordering alone does not stop starvation. | modify `src/Hukbo.Client/Audio/SoundCueBudget.cs` | T47 | CLIENT-AUDIO | `Budget_ReservesCapacityForRareCuesAcrossAMultiTickFrame` passes with several ticks ingested between two `BeginFrame` calls. |
| [ ] | T49 | Make `SoundDirector.Ingest` walk a tick event batch twice, rare cues first. The authoritative stream is untouched; only the order in which cues ask for the budget changes. | modify `src/Hukbo.Client/Audio/SoundDirector.cs` | T48 | CLIENT-AUDIO | `Ingest_ResolvesDeathAndOutcomeCuesBeforeAttackAndClashCues` passes and the existing budget and mute cases pass unchanged. |
| [ ] | T50 | Assert the consequence of T49 on the cue log: `Append` collapses consecutive rows sharing tick, sound, and status, so both the collapse behaviour and the panel order change, and the sound log will show deaths before attacks while the battle event log still shows attacks first. Name it, do not fix it. | modify `tests/Hukbo.Client.Tests/SoundCueLogTests.cs`; modify `tests/Hukbo.Client.Tests/SoundLogPanelTests.cs` | T49 | TESTS | `Append_CollapsesRowsUnderTheNewRareFirstOrdering` and `Panel_ListsRareCuesAboveAttackCuesWithinATick` pass, and a comment records that the divergence from the battle event log is intentional. |
| [ ] | T51 | Assert that the loader discovers numbered variants for the three classless clash slots with no change to `SoundLibrary`. | modify `tests/Hukbo.Client.Tests/SoundLibraryTests.cs` | T45 | TESTS | `DiscoversNumberedVariantsForEveryClashSlotWithoutALoaderChange` passes against a synthetic file-name list. |
| [ ] | T52 | Give the formatter a distinct action label per resolution, suppress or relabel the damage line in the detail panel so a non-landed attack never reads a bare zero, and extend the feed defence-in-depth guard to check the resolution. Without the guard, a default event whose kind reads as `Attack` throws through the text filter once the formatter dereferences the new field. | modify `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`; modify `src/Hukbo.Client/Presentation/BattleEventFeed.cs`; modify `src/Hukbo.Client/UI/BattleEventLogPanel.Details.cs`; modify `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs`; modify `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs` | T26 | CLIENT-AUDIO | `GetActionLabel_ProducesADistinctLinePerResolution` asserts five distinct strings and that none reports a zero damage figure, `Details_OmitsTheDamageLineForANonLandedAttack` passes, and `MatchesFilters_DoesNotThrowOnADefaultAttackEvent` passes. |

### Phase 3c — AUDIO-ASSETS

Owns `scripts/sfx.ps1` and `src/Hukbo.Client/Content/Audio/`. Touches no C# file.
Generation is not deterministic and never runs in a build, a test, or the gate.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T53 | Add default prompts for the three clash slots, each asking for one sound event on dry packed earth in open air, no reverb, no music, no voice, and none naming a cultural identification. | modify `scripts/sfx.ps1` | T45 | AUDIO-ASSETS | `./scripts/sfx.ps1 -List` prints twelve slots each with a default prompt, and `./scripts/sfx.ps1 -Slot clash-shield -DryRun` resolves and exits before the key lookup, so this is verifiable without a key. |
| [ ] | T54 | Generate `clash-blade-hard-01.wav` through `-05.wav`. The script rejects any take below ten per cent of full scale and writes the provenance row itself, so the script output **is** the format gate; re-roll on rejection. | create five files under `src/Hukbo.Client/Content/Audio/`; append `src/Hukbo.Client/Content/Audio/GENERATED.md` | T53 | AUDIO-ASSETS | Five `[PASS] Wrote` lines and five provenance rows. Audible quality is **`HUMAN`**, smoke rows added by T61. |
| [ ] | T55 | Generate `clash-blade-soft-01.wav` through `-05.wav`, shorter and brighter than the hard set with a faster decay. | create five files under `src/Hukbo.Client/Content/Audio/`; append `src/Hukbo.Client/Content/Audio/GENERATED.md` | T53 | AUDIO-ASSETS | Same. **`HUMAN`** for quality. |
| [ ] | T56 | Generate `clash-shield-01.wav` through `-05.wav`, a dull wooden board impact with no metallic ring. | create five files under `src/Hukbo.Client/Content/Audio/`; append `src/Hukbo.Client/Content/Audio/GENERATED.md` | T53 | AUDIO-ASSETS | Same. **`HUMAN`** for quality. |
| [ ] | T57 | Record the three new slots and their take counts in the audio folder inventory. | modify `src/Hukbo.Client/Content/Audio/README.md` | T54, T55, T56 | AUDIO-ASSETS | The README lists twelve slots and its stated file count matches the folder. **`HUMAN`**: this is a person comparing a document against a directory, and no automated check performs it. |

### Phase 4 — gate and record

Strictly serial, one agent, after every Phase 3 workstream has landed.

| | ID | Action | Files | Depends on | Workstream | Verification |
| --- | --- | --- | --- | --- | --- | --- |
| [ ] | T58 | Run the canonical gate and paste its exact output rather than summarising it. | none | T42, T52, T57 | DOCS | `./scripts/verify.ps1 -SkipBootstrap` ends with `[PASS] Canonical repository verification completed` and all five stages report `[PASS]`. Test counts recorded as printed. |
| [ ] | T59 | Run the 200-agent, 10,000-tick, seed-1 workload, capture the whole run report, and **assert its state and event hashes are byte-identical to the pair recorded at barrier B2**. Any difference means Part B or Part C leaked into the simulation. | none | T58 | DOCS | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` reports `deterministic: true`, a null first mismatch tick, both hashes equal to the B2 pair, a defence-attributable share inside 0.30 to 0.40, and no tick-limit draw. |
| [ ] | T60 | Run the report-only 500-agent stress workload. | none | T59 | DOCS | `./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1` completes and reports `deterministic: true`. Reported, never budgeted. |
| [ ] | T61 | Replace the recorded oracle with the new figures for both workloads, moving **both** superseded pairs into the dead-value table with a one-line reason each. | modify `docs/development/testing.md` | T59, T60 | DOCS | The superseded table names the 200-agent pair `D78F0B527B7F938F` and `AC3BAAEC684854D5` **and** the 500-agent pair `C81B4F48DE54B983` and `D03F1213563DFD49`, and the live tables carry only figures produced by T59 and T60. |
| [ ] | T62 | Amend design sections 2.2 and 3.3 to the values that actually shipped, if T30 re-tuned anything, so the archived design does not describe a model the code does not implement. | modify `docs/plans/2026-07-27-weapon-clash-design.md` | T30, T61 | DOCS | Every basis-point value in design section 3.3 matches `PhilippineCombatPreset`, verified value by value. |
| [ ] | T63 | Add the interactive smoke rows, all `PENDING`. | modify `docs/development/testing.md` | T61 | DOCS | At least eleven rows covering: a parried blow shows no damage and no blood; the event log distinguishes all five resolutions; the three clash cues are audibly distinct from a wet cut and from each other; **a void is silent and visibly different from a block**; weapons visibly swing rather than sitting static; a swing reads as one countable action at 1x and does not smear at 4x; **a clashed blow visibly recoils while a void swings clean through**; **the swing arc trail is visible at high zoom and absent at low**; the clash cross appears only where two weapons meet; a death cue is still audible during the busiest fighting; and shielded warriors visibly outlive shieldless ones. Every row `PENDING`. **`HUMAN`.** |
| [ ] | T64 | Update the determinism skill baseline and add the clash-bearing fields to its table of hashed fields that force a preset version. | modify `.claude/skills/hukbo-determinism-change/SKILL.md` | T61 | DOCS | The skill names the T59 hashes as the live baseline and lists both previous pairs as superseded, matching `docs/development/testing.md` exactly. |
| [ ] | T65 | Add the defensive resolution contract as standards section 14, following the shape of the section 13 collision contract, including a historical boundary paragraph stating that no value in it is a measurement and reproducing the morale-compensation finding from design section 2.3. | modify `SIMULATION-GAME-STANDARDS.md` | T61 | DOCS | Section 14 states the tick stage, the five outcomes with pinned values, the domain tag, the composition rule, the two acceptance criteria, the hashed fields, and the spectator channels. |
| [ ] | T66 | Move both plan documents to the archive with the "Archived: reference only" banner once the feature is integrated. | move `docs/plans/2026-07-27-weapon-clash-design.md` and `docs/plans/2026-07-27-weapon-clash.md` into `docs/archives/` | T62, T63, T64, T65 | DOCS | Both files carry the banner under the title and no longer appear in `docs/plans/`. |

## 3. Parallelisation map

| Group | Tasks | Agents | Notes |
| --- | --- | --- | --- |
| P0 | T01 to T09 | 1 | Nine small creations forming one dependency chain through `ClashProfile` and `CombatRuleset`. T09 shares no file with the rest and could be a second agent, but the phase is short and the barrier is a whole-solution build either way. |
| P1 | T10 to T20 | **1** | Revision 2 proposed three parallel agents on the grounds of three disjoint files. That was unsound: three disjoint *files* sit in one *assembly*, and three agents cannot each run `./scripts/test.ps1` while any of them has an uncompilable edit in flight. One agent, or three writers and a single compile-and-run join step. |
| P2 | T21 to T30 | **1, serial by necessity** | T25, T26, and T27 all edit the same method in `BattleSimulation.cs`. T21 must precede T25. T29 must follow T16. T30 may loop back into T24. |
| P3a | T31, T32, T38 in parallel, then T33 to T42 | 1, optionally 2 | The swing family and the clash-effect family share no file, so T38 to T40 can be a second agent with T41 as the join. The Phase 0 stubs are what make concurrent authorship into one test assembly safe. |
| P3b | T43, then T44 to T52 | 1 | `AudioTypes`, `SoundCatalog`, `SoundCueMapper`, `SoundDirector`, and `SoundCueBudget` form one chain. T51 and T52 share no file with it and can run alongside. |
| P3c | T53, then T54 to T56 in parallel, then T57 | 3 during generation | Each generation task writes only its own five file names. The one shared append target, `GENERATED.md`, is guarded by the file lock added during the sound-variant work. |
| P4 | T58 to T66 | 1 | Strictly serial. The gate is one machine-level operation and the documents are written from its output. |

The three Phase 3 groups run **at the same time**. Their production file sets are disjoint:
P3a owns `Rendering/` and the effect systems, P3b owns `Audio/` plus the formatter, feed, and
detail panel, P3c owns the script and the content folder. They do write into one shared
`Hukbo.Client.Tests` assembly, which is exactly why T09 exists: without the stubs, a
non-compiling `SwingAnimationSystemTests.cs` would stop P3b from ever observing its own red.

Maximum useful concurrency: one agent in Phases 0, 1, 2, and 4, and five to seven across
Phase 3.

## 4. File ownership and serialisation points

| File | Owning workstream | Tasks | Overlap |
| --- | --- | --- | --- |
| `src/Hukbo.Core/Combat/AttackResolution.cs` | CORE | T01 | none |
| `src/Hukbo.Core/Combat/ClashProfile.cs` | CORE | T02, T03 | Same agent, sequential |
| `src/Hukbo.Core/Combat/ClashResolver.cs` | CORE | T06, T22 | **Serialisation point** across a phase barrier |
| `src/Hukbo.Core/Combat/CombatRuleset.cs` | CORE | T07, T23 | **Serialisation point** across a phase barrier |
| `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` | CORE | T24, T30 | **Serialisation point.** T30 may re-tune what T24 wrote |
| `src/Hukbo.Core/Simulation/BattleEvent.cs` | CORE | T04 | none |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | CORE | T25, T26, T27 | **Largest serialisation point in the plan.** Three tasks, one method, one agent, in order |
| `src/Hukbo.Core/Simulation/CombatMetrics.cs` | CORE | T05 | none |
| `src/Hukbo.Headless/HeadlessRunner.cs`, `RunReport.cs` | CORE | T28 | none |
| `src/Hukbo.Client/Presentation/SwingAnimation.cs`, `SwingAnimationSystem.cs` | CLIENT-ANIM | T09, T33 | Same workstream, sequential |
| `src/Hukbo.Client/Presentation/ClashEffect.cs`, `ClashEffectSystem.cs` | CLIENT-ANIM | T09, T39 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/SwingGeometry.cs` | CLIENT-ANIM | T09, T34 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/SwingPoseResolver.cs` | CLIENT-ANIM | T09, T35 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/ClashEffectGeometry.cs` | CLIENT-ANIM | T09, T39 | Same workstream, sequential |
| `src/Hukbo.Client/Rendering/ClashEffectRenderer.cs` | CLIENT-ANIM | T39 | none |
| `src/Hukbo.Client/Rendering/PawnGeometry.cs` | CLIENT-ANIM | T36, T37 | **Serialisation point.** Same agent, sequential |
| `src/Hukbo.Client/Rendering/PawnRenderer.cs` | CLIENT-ANIM | T37 | none |
| `src/Hukbo.Client/Presentation/BloodEffectSystem.cs` | CLIENT-ANIM | T40 | none |
| `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` | CLIENT-ANIM | T41 | **Join point.** Needs T33 and T39 |
| `src/Hukbo.Client/ArenaGame.cs` | CLIENT-ANIM | T42 | **Join point.** Needs T41, T35, T36. Wiring only |
| `src/Hukbo.Client/Audio/AudioTypes.cs` | CLIENT-AUDIO | T44 | none |
| `src/Hukbo.Client/Audio/SoundCatalog.cs` | CLIENT-AUDIO | T45 | none |
| `src/Hukbo.Client/Audio/SoundCueMapper.cs` | CLIENT-AUDIO | T46 | none |
| `src/Hukbo.Client/Audio/SoundDirector.cs` | CLIENT-AUDIO | T47, T49 | **Serialisation point.** Same agent, sequential |
| `src/Hukbo.Client/Audio/SoundCueBudget.cs` | CLIENT-AUDIO | T48 | none |
| `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`, `BattleEventFeed.cs`, `src/Hukbo.Client/UI/BattleEventLogPanel.Details.cs` | CLIENT-AUDIO | T52 | none |
| `scripts/sfx.ps1` | AUDIO-ASSETS | T53 | none |
| `src/Hukbo.Client/Content/Audio/*.wav` | AUDIO-ASSETS | T54, T55, T56 | Disjoint file names per task |
| `src/Hukbo.Client/Content/Audio/GENERATED.md` | AUDIO-ASSETS | T54, T55, T56 | **Shared append target**, made safe by the existing file lock. Not a serialisation point |
| `src/Hukbo.Client/Content/Audio/README.md` | AUDIO-ASSETS | T57 | none |
| `tests/Hukbo.Core.Tests/ClashResolverTests.cs` | TESTS | T10 to T14 | Same agent, sequential |
| `tests/Hukbo.Core.Tests/ClashProfileTests.cs` | TESTS | T02, T03, T15 | **Serialisation point** across a phase barrier |
| `tests/Hukbo.Core.Tests/NaiveClashResolution.cs` | TESTS | T08 | none |
| `tests/Hukbo.Core.Tests/CombatMetricsTests.cs` | TESTS | T05 | none |
| `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | TESTS | T01, T16, T29 | **Serialisation point**, three tasks across three phases |
| `tests/Hukbo.Core.Tests/BattleEventTests.cs` | TESTS | T04 | none |
| `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | TESTS | T17, T21, T27 | **Serialisation point.** Sequential within Phases 1 and 2 |
| `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs` | TESTS | T17, T19, T20, T21 | **Serialisation point.** One agent, sequential |
| `tests/Hukbo.Core.Tests/DeterminismTests.cs` | TESTS | T18, T19, T29 | **Serialisation point.** One agent, sequential |
| `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | TESTS | T28 | none |
| `tests/Hukbo.Client.Tests/*` | TESTS | T31, T32, T35 to T38, T40, T41, T43, T50, T51, T52 | No two tasks edit the same client test file. All of them sit in one assembly, which is why the T09 stubs are a prerequisite for concurrent Phase 3 authorship |
| `docs/development/testing.md` | DOCS | T61, T63 | **Serialisation point.** Same agent, sequential |
| `docs/plans/2026-07-27-weapon-clash-design.md` | DOCS | T62, T66 | Same agent, sequential |
| `SIMULATION-GAME-STANDARDS.md` | DOCS | T65 | none |
| `.claude/skills/hukbo-determinism-change/SKILL.md` | DOCS | T64 | none |

`docs/research/WEAPON_CLASH_1500s.md` is **not modified by any task**. It is evidence, not a
tuning surface, and no number from this plan may be written back into it, including and
especially the morale-compensation reasoning in design section 2.3.

## 5. Verification criteria for the change as a whole

- `./scripts/verify.ps1` passes at all five stages with zero warnings and zero errors.
- `./scripts/format.ps1 -Verify` passes at barriers B0, B1, and B2, not only at the end.
- **Acceptance criterion one.** The defence-attributable non-landed share over the 200-agent
  battle sits inside 0.30 to 0.40 and is outside 0.25 to 0.45 nowhere. Measured as shield
  plus weapon plus void intercepts over accepted attack attempts against a defender in
  reach, never as total non-landed including a future attacker-accuracy failure.
- **Acceptance criterion two.** Across at least twenty seeds at 200 agents, at least 95 per
  cent decide before the tick cap **and** the median decisive tick is at or below 5,000. Both
  clauses required; the median clause is the one that can fail.
- The zero-interception control run reproduces the pre-change ordered event stream exactly,
  with the state hash differing only by the content-hash fold.
- The resolver matches the independent naive reference across the whole roster matrix.
- Both hashes moved, the movement is explained by the four mechanisms in design section 3.6,
  and both new values are recorded in `docs/development/testing.md` and in the skill.
- **The Phase 4 workload reproduces the barrier B2 hash pair byte for byte**, which is the
  only evidence Parts B and C stayed in presentation.
- Both superseded hash pairs, 200-agent and 500-agent, are named as dead values.
- The 500-agent stress workload completes and is reported, never budgeted.
- No Client test constructs `ArenaGame`, a graphics device, a sprite batch, a window, or an
  audio device.
- Every clash tuning value in source carries a `PROVISIONAL` comment and the research
  statement that all sixteen weapon-intercept cells have zero evidentiary confidence.
- Every new interactive smoke row is `PENDING`.

Generated audio format is **not** listed here as a whole-change criterion. Phase 3c has a
soft barrier that explicitly permits a missing WAV, so a criterion asserting file properties
could not be re-checked at the end and could not become a test. The script itself is the
gate: it rejects a take below ten per cent of full scale, writes the RIFF header itself, and
appends the provenance row, so the per-task criterion in T54 to T56 is the enforceable form.

## 6. Things that must not happen

- No new `BattleEventKind` member.
- No facing, heading, morale, fatigue, skill, durability, or per-weapon reach field on
  `AgentState` or `Scenario`.
- No re-application of the research section 5.4 modifiers on top of the round-two matrices;
  the crowding, awareness, and fatigue discounts are already inside them.
- No spear, no buckler, no new `WeaponId`, `ArmorId`, or `ShieldId` member.
- No `System.Random`, and no new `SplitMix64` stream in the tick loop.
- No floating-point value anywhere in the clash path.
- No hit stop, screen shake, or full-screen flash.
- No logic or loop added to `ArenaGame`, which the repository bans from tests.
- No golden constant edited to match output before the behaviour tests that constrain it
  have passed.
- No test assertion weakened to accommodate the new interception; every clash-affected
  existing test gets a clash-neutral tuple instead.
- No hosted CI workflow.
- No agent flipping a smoke row away from `PENDING`.
- No number, and no part of the morale-compensation reasoning, written back into
  `docs/research/WEAPON_CLASH_1500s.md` or `docs/research/HISTORICAL_1500s_WEAPONS.md`.

## 7. Result

Not yet run. Filled in by T58 through T60 with the exact gate output, the exact run report
for both workloads, the measured defence-attributable share against criterion one, the
twenty-seed termination figures against criterion two, and an honest statement of which
interactive rows remain `PENDING`.
